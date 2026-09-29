import { before, after, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';

const a = '11111111-1111-4111-8111-111111111111';
const b = '22222222-2222-4222-8222-222222222222';
let db;
before(async () => {
  db = new PGlite();
  // Only Auth's identity boundary is simulated. Migration, permissions, RLS and PL/pgSQL are real PostgreSQL.
  await db.exec(`create role anon; create role authenticated; create role service_role bypassrls;
    create schema auth; create table auth.users(id uuid primary key);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema public, auth to anon, authenticated, service_role;
    grant execute on function auth.uid() to anon, authenticated, service_role;
    insert into auth.users values ('${a}'), ('${b}');`);
  for (const file of (await readdir(new URL('../migrations/', import.meta.url))).filter(file => file.endsWith('.sql')).sort()) {
    await db.exec(await readFile(new URL(`../migrations/${file}`, import.meta.url), 'utf8'));
  }
});
after(async () => { await db?.close(); });
async function asRole(role, subject, operation) {
  await db.exec(`set role ${role}`);
  await db.query("select set_config('request.jwt.claim.sub', $1, false)", [subject ?? '']);
  try { return await operation(); }
  finally { await db.exec('reset role'); }
}
const create = (user, price = 'unfold-kr', provider = 'toss', mode = 'test') => asRole('service_role', null, async () =>
  (await db.query('select public.create_pending_order($1, $2, $3, $4) as id', [user, price, provider, mode])).rows[0].id);
const apply = (id, key, kind, { provider = 'toss', mode = 'test', currency = 'KRW', amount = 4900, reference = id } = {}) =>
  asRole('service_role', null, async () => (await db.query('select public.apply_verified_payment($1,$2,$3,$4,$5,$6,$7,$8) as applied',
    [id, provider, mode, key, reference, kind, currency, amount])).rows[0].applied);

test('catalog has exact prices and live checkout is disabled', async () => {
  const rows = await asRole('anon', null, async () => (await db.query('select id,currency,amount_minor,live_enabled from public.prices order by id')).rows);
  assert.deepEqual(rows, [
    { id: 'unfold-global', currency: 'USD', amount_minor: 399, live_enabled: false },
    { id: 'unfold-kr', currency: 'KRW', amount_minor: 4900, live_enabled: false },
  ]);
  await assert.rejects(create(a, 'unfold-kr', 'toss', 'live'), /Live checkout is not configured/);
});

test('Lemon Squeezy test catalog is configured for Korea only', async () => {
  const rows = (await db.query(`select price_id,environment,store_id,variant_id,product_id,checkout_host
    from public.lemon_prices order by price_id`)).rows;
  assert.deepEqual(rows, [{
    price_id: 'unfold-kr', environment: 'test', store_id: '485125', variant_id: '2176689',
    product_id: '1393777', checkout_host: 'dokhustudio.lemonsqueezy.com',
  }]);
});

test('users cannot write orders, prices or entitlements, call trusted functions, or read event receipts', async () => {
  for (const role of ['anon', 'authenticated']) {
    for (const sql of [
      "update public.prices set amount_minor = 1",
      "update public.orders set status = 'paid'",
      "update public.entitlements set status = 'active'",
      `insert into public.account_roles(user_id, role) values ('${a}', 'admin')`,
      'select * from public.payment_events',
      `select public.create_pending_order('${a}', 'unfold-kr', 'toss', 'test')`,
      `select public.apply_verified_payment('${a}', 'toss', 'test', 'fake', 'fake', 'paid', 'KRW', 4900)`,
    ]) await assert.rejects(asRole(role, a, () => db.exec(sql)), /permission denied/);
  }
});

test('account roles are server-managed and visible only to their owner', async () => {
  await asRole('service_role', null, () => db.exec(`insert into public.account_roles(user_id, role) values ('${a}', 'admin')`));
  const own = await asRole('authenticated', a, () => db.query('select user_id,role from public.account_roles'));
  assert.deepEqual(own.rows, [{ user_id: a, role: 'admin' }]);
  assert.equal((await asRole('authenticated', b, () => db.query('select * from public.account_roles'))).rows.length, 0);
  assert.equal((await asRole('anon', null, () => db.query('select * from public.account_roles')).catch(error => error)).message.includes('permission denied'), true);
});

test('RLS isolates accounts, pending orders never grant entitlement', async () => {
  const order = await create(a);
  await create(b);
  const own = await asRole('authenticated', a, async () => (await db.query('select user_id from public.orders')).rows);
  assert.ok(own.length > 0);
  assert.ok(own.every(row => row.user_id === a));
  assert.equal((await asRole('authenticated', b, () => db.query('select * from public.orders where id = $1', [order]))).rows.length, 0);
  assert.equal((await db.query('select * from public.entitlements')).rows.length, 0);
  await apply(order, 'first-payment', 'paid');
  assert.equal((await asRole('authenticated', b, () => db.query('select * from public.entitlements'))).rows.length, 0);
  assert.equal((await asRole('authenticated', a, () => db.query('select status from public.entitlements'))).rows[0].status, 'active');
});

test('duplicate event is idempotent; event-key reuse and mismatched payment are rejected atomically', async () => {
  const order = await create(b);
  for (const invalid of [{ amount: 1 }, { currency: 'USD' }, { provider: 'lemon' }, { mode: 'live' }]) {
    await assert.rejects(apply(order, 'mismatch', 'paid', invalid), /does not match/);
  }
  assert.equal((await db.query('select status from public.orders where id = $1', [order])).rows[0].status, 'pending');
  assert.equal(await apply(order, 'repeatable', 'paid'), true);
  assert.equal(await apply(order, 'repeatable', 'paid'), false);
  await assert.rejects(apply(order, 'repeatable', 'refunded'), /different payment/);
  await assert.rejects(apply(order, 'wrong-reference', 'paid', { reference: 'another-payment' }), /does not match/);
  assert.equal((await db.query("select count(*)::int as n from public.payment_events where event_key = 'repeatable'")).rows[0].n, 1);
});

test('full refund before delayed paid event is terminal', async () => {
  const order = await create(a);
  await apply(order, 'refund-first', 'refunded');
  await apply(order, 'paid-late', 'paid');
  assert.equal((await db.query('select status from public.orders where id = $1', [order])).rows[0].status, 'refunded');
});

test('refunding one purchase preserves another valid purchase, then revokes after final refund', async () => {
  const order1 = await create(a, 'unfold-global', 'lemon');
  const order2 = await create(a, 'unfold-global', 'lemon');
  const options = { provider: 'lemon', currency: 'USD', amount: 399 };
  await apply(order1, 'usd-1-paid', 'paid', options);
  await apply(order2, 'usd-2-paid', 'paid', options);
  await apply(order1, 'usd-1-refund', 'refunded', options);
  assert.equal((await db.query('select status from public.entitlements where user_id = $1', [a])).rows[0].status, 'active');
  await apply(order2, 'usd-2-refund', 'refunded', options);
  // Refund every remaining paid order for A, including the earlier KRW purchase.
  for (const order of (await db.query("select id from public.orders where user_id = $1 and status = 'paid'", [a])).rows) {
    await apply(order.id, `final-${order.id}`, 'refunded');
  }
  assert.equal((await db.query('select status from public.entitlements where user_id = $1', [a])).rows[0].status, 'revoked');
});

test('test purchases cannot create live entitlements and provider references cannot be reused', async () => {
  assert.equal((await db.query("select * from public.entitlements where environment = 'live'")).rows.length, 0);
  const first = await create(b);
  const second = await create(b);
  await apply(first, 'provider-first', 'paid', { reference: 'unique-provider-order' });
  await assert.rejects(apply(second, 'provider-second', 'paid', { reference: 'unique-provider-order' }), /unique constraint/);
  assert.equal((await db.query("select * from public.payment_events where event_key = 'provider-second'")).rows.length, 0);
});
