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

test('Lemon Squeezy entrypoints require server secrets and keep user JWT and webhook HMAC boundaries separate', async () => {
  const values = {
    SUPABASE_URL: 'http://kong:8000', SUPABASE_PUBLISHABLE_KEYS: '{"default":"sb_publishable_test"}',
    SUPABASE_SECRET_KEYS: '{"default":"sb_secret_test"}', UNFOLD_ENVIRONMENT: 'test',
    LEMONSQUEEZY_TEST_API_KEY: 'ls_test_api_key_test_only',
    LEMONSQUEEZY_WEBHOOK_SECRET: 'lemon-test-webhook-secret',
    UNFOLD_CHECKOUT_ENABLED: 'true',
  };
  let handler;
  globalThis.Deno = { env: { get: name => values[name] }, serve: callback => { handler = callback; } };
  try {
    await import('../functions/create-checkout/index.ts?wiring');
    assert.equal((await handler(new Request('https://example.test/create-checkout', { method: 'POST' }))).status, 401);
    await import('../functions/lemon-webhook/index.ts?wiring');
    assert.equal((await handler(new Request('https://example.test/lemon-webhook', { method: 'POST' }))).status, 401);
    delete values.LEMONSQUEEZY_WEBHOOK_SECRET;
    await assert.rejects(import('../functions/lemon-webhook/index.ts?missing-secret'), /webhook secret required/);
    values.UNFOLD_ENVIRONMENT = 'live';
    values.SUPABASE_URL = 'https://example.supabase.co';
    await assert.rejects(import('../functions/create-checkout/index.ts?live-guard'), /requires/);
    const { readFile } = await import('node:fs/promises');
    const config = await readFile(new URL('../config.toml', import.meta.url), 'utf8');
    assert.match(config, /\[functions.create-checkout\]\s+verify_jwt = true/);
    assert.match(config, /\[functions.lemon-webhook\]\s+verify_jwt = false/);
  } finally { delete globalThis.Deno; }
});
