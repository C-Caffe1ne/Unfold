import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { createCheckoutHandler } from '../functions/_shared/checkout.mjs';
import { createLemonWebhookHandler } from '../functions/_shared/lemon-webhook.mjs';
import { now, webhookSecret, checkoutHost, variant, checkout, orderEvent, webhookRequest, checkoutRequest } from './fixtures/lemon.mjs';

let db, sequence = 500;
before(async () => {
  db = new PGlite();
  await db.exec(`create role anon; create role authenticated; create role service_role bypassrls;
    create schema auth; create table auth.users(id uuid primary key);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema public, auth to anon, authenticated, service_role;`);
  for (const file of (await readdir(new URL('../migrations/', import.meta.url))).filter(f => f.endsWith('.sql')).sort())
    await db.exec(await readFile(new URL(`../migrations/${file}`, import.meta.url), 'utf8'));
});
after(async () => db?.close());
const query = (sql, args) => db.query(sql, args);
const rpc = async (name, args) => (await query(`select public.${name}(${args.map((_, i) => `$${i + 1}`).join(',')}) as result`, args)).rows[0].result;
const store = {
  reserve: (user, price, request) => rpc('reserve_lemon_checkout', [user, price, request, 'live']),
  bind: (order, id) => rpc('bind_lemon_checkout', [order, id]),
  uncertain: order => rpc('mark_lemon_checkout_uncertain', [order]),
  release: order => rpc('release_lemon_checkout', [order]),
  find: async id => (await query("select * from public.orders where id=$1 and environment='live'", [id])).rows[0] ?? null,
  apply: (order, fact) => rpc('apply_lemon_event', [order.id, fact.key, fact.providerOrderId, fact.kind,
    order.currency, fact.itemPrice, fact.subtotal, fact.tax, fact.total, fact.refundedTotal]),
};
async function user() { const id = crypto.randomUUID(); await query('insert into auth.users values ($1)', [id]); return id; }
const access = async (owner, environment) => (await query('select status from public.entitlements where user_id=$1 and environment=$2', [owner, environment])).rows[0]?.status;

test('migration leaves live disabled and a test mapping can never create a live order', async () => {
  const owner = await user();
  assert.equal((await query("select live_enabled from public.prices where id='unfold-kr'")).rows[0].live_enabled, false);
  assert.equal((await query("select * from public.lemon_prices where environment='live'")).rows.length, 0);
  await assert.rejects(store.reserve(owner, 'unfold-kr', crypto.randomUUID()), /catalog not configured/);
  await query("update public.prices set live_enabled=true where id='unfold-kr'");
  await assert.rejects(store.reserve(owner, 'unfold-kr', crypto.randomUUID()), /catalog not configured/);
  for (const role of ['anon', 'authenticated']) {
    await db.exec(`set role ${role}`);
    try { await assert.rejects(store.reserve(owner, 'unfold-kr', crypto.randomUUID()), /permission denied/); }
    finally { await db.exec('reset role'); }
  }
});

test('live reservation → hosted checkout → signed payment → partial/full refund stays isolated from test', async () => {
  const owner = await user();
  await query(`insert into public.lemon_prices values ('unfold-kr','live','10','20','30',$1)`, [checkoutHost]);
  let saved, creates = 0;
  const recording = { ...store, reserve: async (...args) => { const result = await store.reserve(...args); saved = result.order; return result; } };
  const lemon = { variant: async () => variant(saved), create: async order => { creates++; return checkout(order, ++sequence); },
    checkout: async id => checkout(saved, Number(id.slice(-12))) };
  const handle = createCheckoutHandler({ store: recording, lemon, reader: { user: async () => owner }, environment: 'live', now: () => now });
  const body = { market: 'KR', request_id: crypto.randomUUID() };
  const result = await (await handle(checkoutRequest(body))).json();
  assert.equal(result.environment, 'live');
  assert.equal((await handle(checkoutRequest(body))).status, 200); assert.equal(creates, 1);
  const target = await store.find(result.order_id); assert.equal(target.environment, 'live');
  const webhook = createLemonWebhookHandler({ store, secret: webhookSecret, environment: 'live' });
  const deliver = async event => webhook(await webhookRequest(event));
  const provider = ++sequence;
  assert.equal((await deliver(orderEvent({ ...target, environment: 'test' }, provider))).status, 422);
  assert.equal(await access(owner, 'live'), undefined);
  assert.equal((await deliver(orderEvent(target, provider))).status, 200);
  assert.equal(await access(owner, 'live'), 'active'); assert.equal(await access(owner, 'test'), undefined);
  assert.equal((await (await deliver(orderEvent(target, provider))).json()).duplicate, true);
  assert.equal((await deliver(orderEvent(target, provider, 2000))).status, 200); assert.equal(await access(owner, 'live'), 'active');
  assert.equal((await deliver(orderEvent(target, provider, 5390))).status, 200); assert.equal(await access(owner, 'live'), 'revoked');
  assert.equal((await deliver(orderEvent(target, provider))).status, 200); assert.equal(await access(owner, 'live'), 'revoked');
  await query("update public.prices set live_enabled=false where id='unfold-kr'");
  await assert.rejects(store.reserve(owner, 'unfold-kr', crypto.randomUUID()), /catalog not configured/);
  await assert.rejects(store.reserve(owner, 'unfold-kr', body.request_id), /catalog not configured/);
});

 test('Live converted fractional totals preserve exact receipts and revoke only at full refund', async () => {
  await query("update public.prices set live_enabled=true where id='unfold-kr'");
  const owner = await user();
  const reservation = await store.reserve(owner, 'unfold-kr', crypto.randomUUID());
  await store.bind(reservation.order.id, crypto.randomUUID());
  const target = await store.find(reservation.order.id);
  const webhook = createLemonWebhookHandler({ store, secret: webhookSecret, environment: 'live' });
  const provider = ++sequence;
  const event = orderEvent(target, provider);
  Object.assign(event.data.attributes, { subtotal: 489999.25, tax: 49000.75, total: 539000 });
  assert.equal((await webhook(await webhookRequest(event))).status, 200);
  assert.equal(await access(owner, 'live'), 'active');
  const stored = await store.find(target.id);
  assert.equal(Number(stored.total_minor), 5390);
  assert.equal(Number(stored.tax_minor), 490.0075);
  const bad = structuredClone(event); bad.data.attributes.first_order_item.price = 1;
  assert.equal((await webhook(await webhookRequest(bad))).status, 422);
  const refund = structuredClone(event);
  refund.meta.event_name = 'order_refunded';
  Object.assign(refund.data.attributes, { status: 'refunded', refunded: true, refunded_amount: 539000 });
  assert.equal((await webhook(await webhookRequest(refund))).status, 200);
  assert.equal(await access(owner, 'live'), 'revoked');
  assert.equal(await access(owner, 'test'), undefined);
});

test('Live tax-inclusive converted receipt grants app access and stays revoked after a full refund', async () => {
  const owner = await user();
  const reservation = await store.reserve(owner, 'unfold-kr', crypto.randomUUID());
  await store.bind(reservation.order.id, crypto.randomUUID());
  const target = await store.find(reservation.order.id);
  const provider = ++sequence;
  const event = orderEvent(target, provider);
  // Anonymized provider amount shape: inclusive subtotal is gross and USD
  // cent rounding changes the reported KRW total from the contracted price.
  Object.assign(event.data.attributes, {
    tax_inclusive: true, subtotal: 490603, tax: 44235, total: 490603,
    currency_rate: '0.00074602', subtotal_usd: 366, tax_usd: 33, total_usd: 366, setup_fee: 0,
  });
  event.data.attributes.first_order_item.quantity = 1;
  const webhook = createLemonWebhookHandler({ store, secret: webhookSecret, environment: 'live' });
  const deliver = async payload => webhook(await webhookRequest(payload));
  assert.equal((await deliver(event)).status, 200);
  assert.equal(await access(owner, 'live'), 'active');
  assert.equal(await access(owner, 'test'), undefined);
  const saved = await store.find(target.id);
  assert.equal(saved.status, 'paid');
  assert.equal(Number(saved.total_minor), 4906.03);
  assert.equal(Number(saved.tax_minor), 442.35);
  const receipt = (await query('select subtotal_minor from public.lemon_events where order_id=$1', [target.id])).rows[0];
  assert.equal(Number(receipt.subtotal_minor), 4463.68);
  assert.equal((await (await deliver(event)).json()).duplicate, true);
  const refund = structuredClone(event);
  refund.meta.event_name = 'order_refunded';
  Object.assign(refund.data.attributes, { status: 'partial_refund', refunded_amount: 200000 });
  assert.equal((await deliver(refund)).status, 200);
  assert.equal(await access(owner, 'live'), 'active');
  Object.assign(refund.data.attributes, { status: 'refunded', refunded: true, refunded_amount: 490603 });
  assert.equal((await deliver(refund)).status, 200);
  assert.equal(await access(owner, 'live'), 'revoked');
  assert.equal((await deliver(event)).status, 200);
  assert.equal(await access(owner, 'live'), 'revoked');
});
