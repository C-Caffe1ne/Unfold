import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createEntitlementHandler, createSupabaseReader, ServiceError } from '../functions/_shared/entitlement.mjs';

const userId = '11111111-1111-4111-8111-111111111111';
const request = (suffix = '', options = {}) => new Request(`https://example.test/get-entitlement${suffix}`, {
  headers: { Authorization: 'Bearer user-token' }, ...options,
});

test('authenticated subject and server environment override all URL inputs', async () => {
  const calls = [];
  const reader = createSupabaseReader({ url: 'https://example.supabase.co', publicKey: 'public-key', fetcher: async (url, options) => {
    calls.push([url, options]);
    return Response.json(calls.length === 1 ? { id: userId } : calls.length === 2 ? [{ status: 'active' }] : [{ role: 'admin' }]);
  }});
  const handle = createEntitlementHandler({ reader, environment: 'live' });
  const response = await handle(request('?user_id=other&environment=test&isPaid=true'));
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { schema_version: 1, user_id: userId, product_id: 'unfold', environment: 'live', status: 'active', role: 'admin' });
  assert.equal(calls[1][0].searchParams.get('user_id'), `eq.${userId}`);
  assert.equal(calls[1][0].searchParams.get('environment'), 'eq.live');
  assert.equal(calls[2][0].searchParams.get('user_id'), `eq.${userId}`);
  assert.ok(calls.every(([, options]) => options.headers.Authorization === 'Bearer user-token'));
  assert.ok(calls.every(([, options]) => options.redirect === 'error'));
  assert.equal(response.headers.get('cache-control'), 'no-store');
});

test('unknown orders are unowned, revoked remains distinct from connectivity failures', async () => {
  for (const [status, expected] of [['unowned', 200], ['revoked', 200], ['failure', 503]]) {
    const reader = { user: async () => userId, entitlement: async () => {
      if (status === 'failure') throw new Error('secret-token-provider-payload');
      return status;
    }, role: async () => 'member' };
    const response = await createEntitlementHandler({ reader, environment: 'test' })(request());
    assert.equal(response.status, expected);
    const body = await response.text();
    assert.equal(body.includes('secret-token'), false);
    assert.equal(body.includes('unowned'), status === 'unowned');
  }
});

test('missing token, invalid session and disallowed origin cannot query purchases', async () => {
  let reads = 0;
  const reader = { user: async () => { throw new ServiceError('unauthorized', 401); }, entitlement: async () => { reads++; }, role: async () => { reads++; } };
  const handle = createEntitlementHandler({ reader, environment: 'test' });
  assert.equal((await handle(request('', { headers: {} }))).status, 401);
  assert.equal((await handle(request())).status, 401);
  assert.equal((await handle(request('', { headers: { Origin: 'https://evil.test' } }))).status, 403);
  assert.equal((await handle(request('', { method: 'POST' }))).status, 405);
  assert.equal(reads, 0);
});

test('CORS only permits explicitly configured origins', async () => {
  const handle = createEntitlementHandler({ reader: {}, environment: 'test', allowedOrigins: ['http://localhost:8765'] });
  const result = await handle(request('', { method: 'OPTIONS', headers: { Origin: 'http://localhost:8765' } }));
  assert.equal(result.status, 204);
  assert.equal(result.headers.get('access-control-allow-origin'), 'http://localhost:8765');
  assert.throws(() => createEntitlementHandler({ reader: {}, environment: undefined }));
});

test('upstream malformed data and non-401 errors never become an unowned account', async () => {
  for (const result of [Response.json({}), Response.json([{ status: 'trial' }]), Response.json([{ status: 'active' }, { status: 'active' }]), new Response('', { status: 500 })]) {
    const reader = createSupabaseReader({ url: 'https://example.supabase.co', publicKey: 'key', fetcher: async () => result });
    await assert.rejects(reader.entitlement('token', userId, 'live'));
  }
  const reader = createSupabaseReader({ url: 'https://example.supabase.co', publicKey: 'key', fetcher: async () => Response.json({ id: userId, is_anonymous: true }) });
  await assert.rejects(reader.user('token'), /unauthorized/);
  assert.throws(() => createSupabaseReader({ url: 'http://remote.test', publicKey: 'key' }), /HTTPS/);
  assert.throws(() => createSupabaseReader({ url: 'http://kong:8000', publicKey: 'key' }), /HTTPS/);
  assert.doesNotThrow(() => createSupabaseReader({ url: 'http://kong:8000', publicKey: 'key', allowLocalGateway: true }));
  assert.throws(() => createSupabaseReader({ url: 'http://remote.test', publicKey: 'key', allowLocalGateway: true }), /HTTPS/);
});

test('role lookup grants only an isolated admin row and rejects malformed data', async () => {
  const admin = createSupabaseReader({ url: 'https://example.supabase.co', publicKey: 'key',
    fetcher: async () => Response.json([{ role: 'admin' }]) });
  const member = createSupabaseReader({ url: 'https://example.supabase.co', publicKey: 'key',
    fetcher: async () => Response.json([]) });
  assert.equal(await admin.role('token', userId), 'admin');
  assert.equal(await member.role('token', userId), 'member');
  for (const rows of [[{ role: 'owner' }], [{ role: 'admin' }, { role: 'admin' }], {}]) {
    const malformed = createSupabaseReader({ url: 'https://example.supabase.co', publicKey: 'key',
      fetcher: async () => Response.json(rows) });
    await assert.rejects(malformed.role('token', userId));
  }
});
