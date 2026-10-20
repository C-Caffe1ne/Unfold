import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { createLemonWebhookHandler } from '../functions/_shared/lemon-webhook.mjs';
import { webhookSecret, checkoutHost, orderEvent, webhookRequest } from './fixtures/lemon.mjs';

// A purchase from the public Lemon store page carries no Unfold order. It is linked to the
// Google account with the same verified email, now or when that account first checks access.
let db, sequence = 900;
const catalog = { provider_store_id: '488157', provider_product_id: '1429889', provider_price_id: '2233159',
  currency: 'USD', amount_minor: 299, environment: 'live' };
before(async () => {
  db = new PGlite();
  // Supabase Auth owns its tables and writes identities on every Google sign-in.
  await db.exec(`create role anon; create role authenticated; create role service_role bypassrls; create role supabase_auth_admin;
    create schema auth; create table auth.users(id uuid primary key);
    create table auth.identities(id uuid primary key default gen_random_uuid(), user_id uuid not null references auth.users(id),
      provider text not null, provider_id text not null, identity_data jsonb not null,
      email text generated always as (lower(identity_data->>'email')) stored);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema public, auth to anon, authenticated, service_role;
    grant execute on function auth.uid() to anon, authenticated, service_role;
    alter table auth.users owner to supabase_auth_admin; alter table auth.identities owner to supabase_auth_admin;
    grant usage on schema auth to supabase_auth_admin;`);
  // Free access codes and get_app_access live outside the tracked migrations, as in production.
  await db.exec(`create table public.free_access_grants(id uuid primary key default gen_random_uuid(), user_id uuid, code_id uuid,
      product_id text, status text, expires_at timestamptz, created_at timestamptz default now());
    alter table public.free_access_grants enable row level security;
    create policy own_grants on public.free_access_grants for select to authenticated using (user_id = auth.uid());
    grant select on public.free_access_grants to authenticated;`);
  await db.exec(await readFile(new URL('./fixtures/app-access.sql', import.meta.url), 'utf8'));
  await db.exec('revoke execute on function public.get_app_access() from public, anon; grant execute on function public.get_app_access() to authenticated;');
  for (const file of (await readdir(new URL('../migrations/', import.meta.url))).filter(f => f.endsWith('.sql')).sort()) {
    await db.exec(await readFile(new URL(`../migrations/${file}`, import.meta.url), 'utf8'));
  }
  // Production Live catalog on 2026-10-20.
  await query("delete from public.lemon_prices where environment = 'live'");
  await query(`insert into public.lemon_prices values ('unfold-global','live','488157','2233159','1429889',$1)`, [checkoutHost]);
  await query("update public.prices set live_enabled = (id = 'unfold-global')");
});
after(async () => db?.close());
const query = (sql, args) => db.query(sql, args);
const rpc = async (name, args) => (await query(`select public.${name}(${args.map((_, i) => `$${i + 1}`).join(',')}) as result`, args)).rows[0].result;
const store = {
  find: async id => (await query("select * from public.orders where id=$1 and environment='live'", [id])).rows[0] ?? null,
  apply: (order, fact) => rpc('apply_lemon_event', [order.id, fact.key, fact.providerOrderId, fact.kind,
    order.currency, fact.itemPrice, fact.subtotal, fact.tax, fact.total, fact.refundedTotal]),
  storePrice: async (storeId, productId, variantId) =>
    (await query('select * from public.lemon_store_price($1,$2,$3,$4)', ['live', storeId, productId, variantId])).rows[0] ?? null,
  recordStorePurchase: (price, fact, email) => rpc('record_lemon_store_purchase', ['live', fact.key, fact.providerOrderId, fact.kind,
    email, price.price_id, price.currency, fact.itemPrice, fact.subtotal, fact.tax, fact.total, fact.refundedTotal]),
};
const reports = [];
const webhook = createLemonWebhookHandler({ store, secret: webhookSecret, environment: 'live', report: code => reports.push(code) });
async function account(email, provider = 'google', verified = true) {
  const id = crypto.randomUUID();
  await db.exec('set role supabase_auth_admin');
  try {
    await query('insert into auth.users values ($1)', [id]);
    if (email) await query('insert into auth.identities(user_id, provider, provider_id, identity_data) values ($1,$2,$3,$4)',
      [id, provider, crypto.randomUUID(), JSON.stringify({ email, email_verified: verified })]);
  } finally { await db.exec('reset role'); }
  return id;
}
async function entitlement(user) {
  // The web reads the entitlement row with the user's own token (get-entitlement).
  await query("select set_config('request.jwt.claim.sub', $1, false)", [user]);
  await db.exec('set role authenticated');
  try { return (await query("select status from public.entitlements where environment='live'")).rows[0]?.status ?? 'unowned'; }
  finally { await db.exec('reset role'); }
}
function storeOrder(email, providerOrderId = ++sequence, refunded = 0, attributes = {}) {
  const event = orderEvent(catalog, providerOrderId, refunded, 0);
  delete event.meta.custom_data;
  Object.assign(event.data.attributes, { user_email: email }, attributes);
  return event;
}
async function appAccess(user) {
  // get-app-access calls this RPC with the user's own token.
  await query("select set_config('request.jwt.claim.sub', $1, false)", [user]);
  await db.exec('set role authenticated');
  try { return (await query('select public.get_app_access() as status')).rows[0].status; }
  finally { await db.exec('reset role'); }
}
const deliver = async event => webhook(await webhookRequest(event));

test('a store purchase unlocks the existing Google account with the same email', async () => {
  const owner = await account('buyer.one@example.test');
  const response = await deliver(storeOrder('Buyer.One@Example.test '));
  assert.equal(response.status, 200);
  assert.equal(await appAccess(owner), 'active');
  const orders = (await query("select status, provider, price_id, currency, amount_minor, provider_order_id from public.orders where user_id=$1", [owner])).rows;
  assert.deepEqual(orders.map(o => [o.status, o.provider, o.price_id, o.currency, o.amount_minor]), [['paid', 'lemon', 'unfold-global', 'USD', 299]]);
  assert.equal(reports.some(code => code.includes('example.test')), false);
});

test('a store purchase made before sign-up is linked at the first Google sign-in on any client', async () => {
  const providerOrderId = ++sequence;
  assert.equal((await deliver(storeOrder('later@example.test', providerOrderId))).status, 200);
  const stranger = await account('someone-else@example.test');
  assert.equal(await appAccess(stranger), 'unowned');
  const owner = await account('LATER@example.test');
  assert.equal(await entitlement(owner), 'active');
  assert.equal(await appAccess(owner), 'active');
  assert.equal(await appAccess(owner), 'active');
  assert.equal((await query('select count(*)::int as n from public.orders where provider_order_id=$1', [String(providerOrderId)])).rows[0].n, 1);
});

test('repeated deliveries create one order and a full refund locks the app again', async () => {
  const owner = await account('refund@example.test');
  const providerOrderId = ++sequence;
  const paid = storeOrder('refund@example.test', providerOrderId);
  assert.equal((await deliver(paid)).status, 200);
  const again = await deliver(paid);
  assert.equal(again.status, 200);
  assert.equal((await again.json()).duplicate, true);
  assert.equal((await query('select count(*)::int as n from public.orders where user_id=$1', [owner])).rows[0].n, 1);
  assert.equal((await deliver(storeOrder('refund@example.test', providerOrderId, 299))).status, 200);
  assert.equal(await appAccess(owner), 'revoked');
});

test('a store purchase refunded before sign-in never grants access', async () => {
  const providerOrderId = ++sequence;
  assert.equal((await deliver(storeOrder('refunded-first@example.test', providerOrderId))).status, 200);
  assert.equal((await deliver(storeOrder('refunded-first@example.test', providerOrderId, 299))).status, 200);
  assert.equal(await appAccess(await account('refunded-first@example.test')), 'unowned');
});

test('a failed link never blocks sign-in and the app access check links it later', async () => {
  const providerOrderId = ++sequence;
  assert.equal((await deliver(storeOrder('retry@example.test', providerOrderId))).status, 200);
  await query('alter table public.orders add constraint store_link_outage check (false) not valid');
  let owner;
  try {
    owner = await account('retry@example.test');
    assert.equal(await entitlement(owner), 'unowned');
    assert.equal(await appAccess(owner), 'unowned');
  } finally { await query('alter table public.orders drop constraint store_link_outage'); }
  assert.equal(await appAccess(owner), 'active');
});

test('only a verified Google identity with exactly that email can claim a store purchase', async () => {
  assert.equal((await deliver(storeOrder('claimed-carefully@example.test'))).status, 200);
  assert.equal(await appAccess(await account('claimed-carefully@example.test', 'google', false)), 'unowned');
  assert.equal(await appAccess(await account('claimed-carefully@example.test', 'email')), 'unowned');
  assert.equal(await appAccess(await account('claimed.carefully@example.test')), 'unowned');
  assert.equal(await appAccess(await account(null)), 'unowned');
  assert.equal(await appAccess(await account('claimed-carefully@example.test')), 'active');
});

test('store purchases for another product are ignored and altered payments are rejected', async () => {
  const owner = await account('mismatch@example.test');
  const other = storeOrder('mismatch@example.test');
  other.data.attributes.first_order_item.variant_id = 1;
  assert.equal((await deliver(other)).status, 200);
  for (const change of [
    value => { value.first_order_item.price = 199; },
    value => { value.currency = 'EUR'; },
    value => { value.test_mode = true; value.first_order_item.test_mode = true; },
    value => { value.discount_total = 100; value.total = 199; },
    value => { value.status = 'pending'; },
    value => { delete value.user_email; },
    value => { value.user_email = 'not-an-email'; },
  ]) {
    const event = storeOrder('mismatch@example.test');
    change(event.data.attributes);
    assert.equal((await deliver(event)).status, 422);
  }
  assert.equal(await appAccess(owner), 'unowned');
  // An Unfold order reference still takes the app checkout path and is never treated as a store purchase.
  const missing = storeOrder('mismatch@example.test');
  missing.meta.custom_data = { source: 'unfold-server', order_id: crypto.randomUUID(), environment: 'live' };
  assert.equal((await deliver(missing)).status, 503);
  assert.equal(await appAccess(owner), 'unowned');
});

test('clients cannot read store purchases or record and link them', async () => {
  const owner = await account('private@example.test');
  assert.equal((await deliver(storeOrder('private@example.test'))).status, 200);
  for (const role of ['anon', 'authenticated']) {
    await query("select set_config('request.jwt.claim.sub', $1, false)", [owner]);
    await db.exec(`set role ${role}`);
    try {
      await assert.rejects(query('select * from public.lemon_store_purchases'), /permission denied/);
      await assert.rejects(query("select public.record_lemon_store_purchase('live','x','1','paid','private@example.test','unfold-global','USD',299,299,0,299,0)"), /permission denied/);
      await assert.rejects(query("select * from public.lemon_store_price('live','488157','1429889','2233159')"), /permission denied/);
    } finally { await db.exec('reset role'); }
  }
  await db.exec('set role anon');
  try { await assert.rejects(query('select public.claim_lemon_store_purchases()'), /permission denied/); }
  finally { await db.exec('reset role'); }
});
