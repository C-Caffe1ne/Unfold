import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { createCheckoutHandler } from '../functions/_shared/checkout.mjs';
import { createLemonWebhookHandler } from '../functions/_shared/lemon-webhook.mjs';
import { now, webhookSecret, checkoutHost, variant, checkout, orderEvent, webhookRequest, checkoutRequest } from './fixtures/lemon.mjs';

const usdMigration = '20261015134723_lemon_usd_price.sql';
const migration = file => readFile(new URL(`../migrations/${file}`, import.meta.url), 'utf8');
let db, sequence = 700, krwOrder, krwProvider;
before(async () => {
  db = new PGlite();
  await db.exec(`create role anon; create role authenticated; create role service_role bypassrls;
    create schema auth; create table auth.users(id uuid primary key);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema public, auth to anon, authenticated, service_role;`);
  const files = (await readdir(new URL('../migrations/', import.meta.url))).filter(f => f.endsWith('.sql')).sort();
  for (const file of files.filter(file => file !== usdMigration)) await db.exec(await migration(file));
  // Production state before the price change: the KRW price sells Live and already has a paid order.
  await query("update public.prices set live_enabled=true where id='unfold-kr'");
  await query(`insert into public.lemon_prices values ('unfold-kr','live','10','20','30',$1)`, [checkoutHost]);
  const reservation = await store.reserve(await user(), 'unfold-kr', crypto.randomUUID());
  await store.bind(reservation.order.id, crypto.randomUUID());
  krwOrder = await store.find(reservation.order.id);
  krwProvider = ++sequence;
  assert.equal((await webhook(await webhookRequest(orderEvent(krwOrder, krwProvider)))).status, 200);
  await db.exec(await migration(usdMigration));
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
const webhook = createLemonWebhookHandler({ store, secret: webhookSecret, environment: 'live' });
async function user() { const id = crypto.randomUUID(); await query('insert into auth.users values ($1)', [id]); return id; }
const access = async owner => (await query("select status from public.entitlements where user_id=$1 and environment='live'", [owner])).rows[0]?.status;

test('the Live sale moves from KRW 4,900 to the same variant at US$2.99', async () => {
  assert.deepEqual((await query('select id,currency,amount_minor,live_enabled from public.prices order by id')).rows, [
    { id: 'unfold-global', currency: 'USD', amount_minor: 299, live_enabled: true },
    { id: 'unfold-kr', currency: 'KRW', amount_minor: 4900, live_enabled: false },
  ]);
  assert.deepEqual((await query("select price_id,store_id,variant_id,product_id from public.lemon_prices where environment='live'")).rows,
    [{ price_id: 'unfold-global', store_id: '10', variant_id: '20', product_id: '30' }]);
  await assert.rejects(store.reserve(await user(), 'unfold-kr', crypto.randomUUID()), /catalog not configured/);
});

test('a GLOBAL checkout charges 299 cents and a tax-inclusive USD receipt grants access', async () => {
  const owner = await user();
  let saved, sent;
  const recording = { ...store, reserve: async (...args) => { const result = await store.reserve(...args); saved = result.order; return result; } };
  const lemon = { variant: async () => variant(saved), create: async order => { sent = order; return checkout(order, ++sequence); } };
  const handle = createCheckoutHandler({ store: recording, lemon, reader: { user: async () => owner }, environment: 'live', now: () => now });
  const result = await (await handle(checkoutRequest({ market: 'GLOBAL', request_id: crypto.randomUUID() }))).json();
  assert.equal(checkout(sent).data.attributes.custom_price, 299);
  const target = await store.find(result.order_id);
  assert.deepEqual([target.currency, target.amount_minor, target.provider_price_id], ['USD', 299, '20']);
  const event = orderEvent(target, ++sequence);
  Object.assign(event.data.attributes, { tax_inclusive: true, subtotal: 299, tax: 27, total: 299 });
  assert.equal((await webhook(await webhookRequest(event))).status, 200);
  assert.equal(await access(owner), 'active');
  const overcharged = orderEvent(target, ++sequence);
  overcharged.data.attributes.first_order_item.price = 399;
  assert.equal((await webhook(await webhookRequest(overcharged))).status, 422);
});

test('the earlier KRW purchase keeps its receipt and can still be fully refunded', async () => {
  assert.equal(await access(krwOrder.user_id), 'active');
  const refund = orderEvent(krwOrder, krwProvider, 5390);
  assert.equal((await webhook(await webhookRequest(refund))).status, 200);
  assert.equal(await access(krwOrder.user_id), 'revoked');
});
