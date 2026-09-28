import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { createCheckoutHandler } from '../functions/_shared/checkout.mjs';
import { createLemonWebhookHandler } from '../functions/_shared/lemon-webhook.mjs';
import { now, webhookSecret, checkoutHost, variant, checkout, orderEvent, webhookRequest, checkoutRequest } from './fixtures/lemon.mjs';

let db;
let sequence = 100;
before(async () => {
  db = new PGlite();
  await db.exec(`create role anon; create role authenticated; create role service_role bypassrls;
    create schema auth; create table auth.users(id uuid primary key);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema public, auth to anon, authenticated, service_role;`);
  for (const file of (await readdir(new URL('../migrations/', import.meta.url))).filter(f => f.endsWith('.sql')).sort()) {
    await db.exec(await readFile(new URL(`../migrations/${file}`, import.meta.url), 'utf8'));
  }
  await db.query(`insert into public.lemon_prices(price_id,environment,store_id,variant_id,product_id,checkout_host)
    values ('unfold-kr','test','10','20','30',$1),('unfold-global','test','11','21','31',$1)
    on conflict (price_id,environment) do update set store_id=excluded.store_id,variant_id=excluded.variant_id,
      product_id=excluded.product_id,checkout_host=excluded.checkout_host`, [checkoutHost]);
});
after(async () => db?.close());
const query = (sql, args) => db.query(sql, args);
const rpc = async (name, args) => (await query(`select public.${name}(${args.map((_, i) => `$${i + 1}`).join(',')}) as result`, args)).rows[0].result;
const store = {
  reserve: (user, price, request) => rpc('reserve_lemon_checkout', [user, price, request]),
  bind: (order, checkoutId) => rpc('bind_lemon_checkout', [order, checkoutId]),
  uncertain: order => rpc('mark_lemon_checkout_uncertain', [order]),
  release: order => rpc('release_lemon_checkout', [order]),
  find: async orderId => (await query("select * from public.orders where id=$1 and provider='lemon' and environment='test'", [orderId])).rows[0] ?? null,
  apply: (order, fact) => rpc('apply_lemon_event', [order.id, fact.key, fact.providerOrderId, fact.kind, order.currency,
    fact.subtotal, fact.tax, fact.total, fact.refundedTotal]),
};
async function user() {
  const value = crypto.randomUUID();
  await query('insert into auth.users values ($1)', [value]);
  return value;
}
async function reserved(priceId = 'unfold-kr', owner) {
  const result = await store.reserve(owner ?? await user(), priceId, crypto.randomUUID());
  await store.bind(result.order.id, `00000000-0000-4000-8000-${String(++sequence).padStart(12, '0')}`);
  return store.find(result.order.id);
}
const access = async order => (await query('select status from public.entitlements where user_id=$1 and environment=$2', [order.user_id, 'test'])).rows[0]?.status;
const webhook = createLemonWebhookHandler({ store, secret: webhookSecret, environment: 'test' });
const deliver = async event => webhook(await webhookRequest(event));

test('server checkout → signed order → entitlement → full refund, with duplicate delivery', async () => {
  const owner = await user();
  let creates = 0;
  let saved;
  const lemon = { variant: async () => variant(saved), create: async order => { creates++; return checkout(order, ++sequence); },
    checkout: async id => checkout(await store.find(saved.id), Number(id.slice(-12))) };
  const recording = { ...store, reserve: async (...args) => { const result = await store.reserve(...args); saved = result.order; return result; } };
  const handle = createCheckoutHandler({ store: recording, lemon, reader: { user: async () => owner }, environment: 'test', now: () => now });
  const body = { market: 'KR', request_id: crypto.randomUUID() };
  const first = await handle(checkoutRequest(body));
  assert.equal(first.status, 200);
  const result = await first.json();
  assert.match(result.checkout_url, /^https:\/\/unfold-test\.lemonsqueezy\.com\//);
  assert.deepEqual(await (await handle(checkoutRequest(body))).json(), result);
  assert.equal((await handle(checkoutRequest({ ...body, request_id: crypto.randomUUID() }))).status, 200);
  assert.equal(creates, 1);
  const target = await store.find(result.order_id);
  const paid = orderEvent(target, ++sequence);
  assert.equal((await deliver(paid)).status, 200);
  assert.equal(await access(target), 'active');
  assert.equal((await (await deliver(paid)).json()).duplicate, true);
  assert.equal((await store.find(target.id)).total_minor, 5390);
  assert.equal((await deliver(orderEvent(target, sequence, 5390))).status, 200);
  assert.equal(await access(target), 'revoked');
  assert.equal((await deliver(orderEvent(target, sequence))).status, 200);
  assert.equal(await access(target), 'revoked');
});

test('USD purchase keeps the independent 399-cent principal', async () => {
  const target = await reserved('unfold-global');
  assert.equal((await deliver(orderEvent(target, ++sequence, 0, 0))).status, 200);
  assert.equal((await store.find(target.id)).total_minor, 399);
  assert.equal(await access(target), 'active');
});

test('cumulative partial refunds revoke only when the full charged total is returned', async () => {
  const target = await reserved();
  const providerOrder = ++sequence;
  await deliver(orderEvent(target, providerOrder));
  assert.equal((await deliver(orderEvent(target, providerOrder, 2000))).status, 200);
  assert.equal(await access(target), 'active');
  assert.equal((await deliver(orderEvent(target, providerOrder, 5390))).status, 200);
  assert.equal(await access(target), 'revoked');
  assert.equal((await deliver(orderEvent(target, providerOrder, 2000))).status, 200);
  assert.equal(await access(target), 'revoked');
  assert.equal((await store.find(target.id)).refunded_total_minor, 5390);
});

test('a fully refunded order delivered before paid is terminal; another paid order preserves access', async () => {
  const owner = await user();
  const first = await reserved('unfold-kr', owner);
  const second = await reserved('unfold-global', owner);
  const firstProvider = ++sequence;
  assert.equal((await deliver(orderEvent(first, firstProvider, 5390))).status, 200);
  assert.equal(await access(first), 'revoked');
  assert.equal((await deliver(orderEvent(second, ++sequence, 0, 0))).status, 200);
  assert.equal((await deliver(orderEvent(first, firstProvider))).status, 200);
  assert.equal(await access(first), 'active');
});

test('signed events with changed identity, principal, currency or tax mode never grant access', async () => {
  const target = await reserved();
  const mutations = [
    event => { event.meta.custom_data.environment = 'live'; },
    event => { event.meta.custom_data.order_id = crypto.randomUUID(); },
    event => { event.data.attributes.store_id = 99; },
    event => { event.data.attributes.first_order_item.product_id = 99; },
    event => { event.data.attributes.first_order_item.variant_id = 99; },
    event => { event.data.attributes.currency = 'USD'; },
    event => { event.data.attributes.subtotal = 1; },
    event => { event.data.attributes.discount_total = 1; },
    event => { event.data.attributes.tax_inclusive = true; },
  ];
  for (const mutate of mutations) {
    const event = orderEvent(target, ++sequence); mutate(event);
    assert.equal((await deliver(event)).status, event.meta.custom_data.order_id === target.id ? 422 : 503);
    assert.equal(await access(target), undefined);
  }
});

test('event conflicts, excess refunds and client access to server tables/RPCs are rejected', async () => {
  const target = await reserved();
  const fact = { key: 'order_created:999', providerOrderId: '999', kind: 'paid', subtotal: 4900, tax: 490, total: 5390, refundedTotal: 0 };
  await store.apply(target, fact);
  await assert.rejects(store.apply(target, { ...fact, tax: 491, total: 5391 }), /event conflict/);
  await assert.rejects(store.apply(target, { ...fact, key: 'order_refunded:999:9999', kind: 'refund', refundedTotal: 9999 }), /does not match/);
  const another = await store.reserve(await user(), 'unfold-kr', crypto.randomUUID());
  await assert.rejects(store.bind(another.order.id, target.provider_checkout_id), /unique constraint/);
  for (const role of ['anon', 'authenticated']) {
    for (const sql of [
      'select * from public.lemon_prices', 'select * from public.lemon_events',
      `select public.reserve_lemon_checkout('${target.user_id}','unfold-kr','${crypto.randomUUID()}')`,
      `select public.bind_lemon_checkout('${target.id}','${crypto.randomUUID()}')`,
      `select public.apply_lemon_event('${target.id}','fake','999','paid','KRW',4900,490,5390,0)`,
    ]) {
      await db.exec(`set role ${role}`);
      try { await assert.rejects(db.exec(sql), /permission denied/); } finally { await db.exec('reset role'); }
    }
  }
});

test('reservation retries read-only failures and never repeats an uncertain remote creation', async () => {
  const owner = await user();
  let fails = true, creations = 0, saved;
  const recording = { ...store, reserve: async (...args) => { const result = await store.reserve(...args); saved = result.order; return result; } };
  const lemon = { variant: async () => { if (fails) throw new Error('provider-secret'); return variant(saved); },
    create: async () => { creations++; throw new Error('timeout-after-remote-creation'); } };
  const handle = createCheckoutHandler({ store: recording, lemon, reader: { user: async () => owner }, environment: 'test', now: () => now });
  const body = { market: 'KR', request_id: crypto.randomUUID() };
  const failed = await handle(checkoutRequest(body));
  assert.equal(failed.status, 503); assert.equal((await failed.text()).includes('provider-secret'), false);
  fails = false;
  assert.equal((await handle(checkoutRequest(body))).status, 503);
  assert.equal(creations, 1);
  assert.equal((await handle(checkoutRequest(body))).status, 409);
  assert.equal(creations, 1);
});
