import { test } from 'node:test';
import assert from 'node:assert/strict';

test('Edge entrypoint reads server mode, supports local gateway and always installs auth-checking handler', async () => {
  const values = {
    SUPABASE_URL: 'http://kong:8000', SUPABASE_ANON_KEY: 'local-public-key',
    UNFOLD_ENVIRONMENT: 'test', UNFOLD_ALLOWED_ORIGINS: 'http://localhost:8765',
  };
  let handler;
  // Entry-point wiring check under Node; not a claim that the Deno gateway was run.
  globalThis.Deno = { env: { get: name => values[name] }, serve: callback => { handler = callback; } };
  try {
    await import('../functions/get-entitlement/index.ts?test-config');
    assert.equal(typeof handler, 'function');
    const response = await handler(new Request('https://example.test/get-entitlement?environment=live'));
    assert.equal(response.status, 401);
    delete values.UNFOLD_ENVIRONMENT;
    values.SUPABASE_URL = 'https://example.supabase.co';
    await assert.rejects(import('../functions/get-entitlement/index.ts?missing-config'), /UNFOLD_ENVIRONMENT/);
  } finally { delete globalThis.Deno; }
});
