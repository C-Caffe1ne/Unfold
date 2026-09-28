import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createCheckoutHandler } from '../functions/_shared/checkout.mjs';
import { createLemonClient, validateVariant, validateCheckout, checkoutLink } from '../functions/_shared/lemon.mjs';
import { fromLemonAmount, toLemonAmount } from '../functions/_shared/lemon-money.mjs';
import { createPaymentStore, supabasePublicKey, supabaseServerKey } from '../functions/_shared/payment-store.mjs';
import { createLemonWebhookHandler } from '../functions/_shared/lemon-webhook.mjs';
import { apiKey, webhookSecret, now, checkoutHost, variant, checkout, orderEvent, signature, webhookRequest, checkoutRequest } from './fixtures/lemon.mjs';

const owner = '11111111-1111-4111-8111-111111111111';
const order = { id: crypto.randomUUID(), user_id: owner, product_id: 'unfold', provider: 'lemon', environment: 'test',
  status: 'pending', provider_price_id: '20', provider_product_id: '30', provider_store_id: '10',
  provider_checkout_host: checkoutHost, amount_minor: 4900, currency: 'KRW' };
const makeCheckout = changes => createCheckoutHandler({ environment: 'test', store: {}, reader: { user: async () => owner }, lemon: {}, now: () => now, ...changes });

test('checkout requires a caller, exact input and allowed origin before reserving', async () => {
  let reserves = 0;
  const handle = makeCheckout({ store: { reserve: async () => { reserves++; } } });
  const body = { market: 'KR', request_id: crypto.randomUUID() };
  for (const changed of [{ ...body, amount: 1 }, { ...body, user_id: owner }, { ...body, market: 'JP' }, { ...body, request_id: 'bad' }, null]) {
    assert.equal((await handle(checkoutRequest(changed))).status, 400);
  }
  assert.equal((await handle(checkoutRequest(body, { headers: {} }))).status, 401);
  assert.equal((await handle(checkoutRequest(body, { headers: { Origin: 'https://evil.test' } }))).status, 403);
  assert.equal(reserves, 0);
  assert.equal((await makeCheckout({ enabled: false })(checkoutRequest(body))).status, 503);
  const cors = makeCheckout({ allowedOrigins: ['https://web.example.test'] });
  const result = await cors(new Request('https://example.test', { method: 'OPTIONS', headers: { Origin: 'https://web.example.test' } }));
  assert.equal(result.status, 204);
  assert.equal(result.headers.get('access-control-allow-origin'), 'https://web.example.test');
  assert.throws(() => makeCheckout({ environment: 'live' }), /requires/);
});

test('checkout diagnostics record only a bounded public error code', async () => {
  const reports = [];
  const handle = makeCheckout({
    reader: { user: async () => { throw new Error('server-secret'); } },
    report: code => reports.push(code),
  });
  const response = await handle(checkoutRequest({ market: 'KR', request_id: crypto.randomUUID() }));
  assert.equal(response.status, 503);
  assert.deepEqual(reports, ['request:service_unavailable']);
  assert.equal(JSON.stringify(reports).includes('server-secret'), false);
});

test('variant, preview and hosted checkout validation reject subscriptions, discounts and redirects', () => {
  for (const change of [{ is_subscription: true }, { has_free_trial: true }, { pay_what_you_want: true }, { test_mode: false }, { status: 'draft' }, { product_id: 99 }]) {
    const value = variant(order); Object.assign(value.data.attributes, change);
    assert.throws(() => validateVariant(value, order));
  }
  for (const change of [{ status: 'draft' }, { test_mode: false }, { store_id: 99 }]) {
    const value = variant(order); Object.assign(value.included[0].attributes, change);
    assert.throws(() => validateVariant(value, order), error => error.message === 'checkout_not_ready' && error.status === 503);
  }
  const missingProduct = variant(order); delete missingProduct.included;
  assert.throws(() => validateVariant(missingProduct, order), error => error.message === 'checkout_not_ready');
  for (const mutate of [
    value => { value.data.attributes.preview.discount_total = 1; },
    value => { value.data.attributes.preview.currency = 'USD'; },
    value => { value.data.attributes.custom_price = 1; },
    value => { value.data.attributes.test_mode = false; },
  ]) { const value = checkout(order); mutate(value); assert.throws(() => validateCheckout(value, order, now)); }
  const resumed = checkout(order); delete resumed.data.attributes.preview;
  assert.throws(() => validateCheckout(resumed, order, now));
  assert.doesNotThrow(() => validateCheckout(resumed, order, now, false));
  const partialPreview = checkout(order); partialPreview.data.attributes.preview = {};
  assert.doesNotThrow(() => validateCheckout(partialPreview, order, now, false));
  for (const url of ['https://evil.test/checkout/custom/x', `https://${checkoutHost}/other`,
    `https://${checkoutHost}/checkout/custom/${checkout(order).data.id}?redirect=evil`]) {
    const value = checkout(order); value.data.attributes.url = url;
    assert.throws(() => checkoutLink(value, order));
  }
});

test('Lemon adapter uses server mappings, one-time quantity and no customer identity', async () => {
  const calls = [];
  const client = createLemonClient({ apiKey: `\n${apiKey} \t`, environment: 'test', now: () => now, fetcher: async (url, init) => {
    calls.push([url, init]); return Response.json(checkout(order));
  } });
  await client.create(order);
  assert.equal(calls[0][0].origin, 'https://api.lemonsqueezy.com');
  assert.equal(calls[0][1].headers.Authorization, `Bearer ${apiKey}`);
  const body = JSON.parse(calls[0][1].body).data.attributes;
  assert.equal(body.custom_price, 490000);
  assert.deepEqual(body.checkout_data.variant_quantities, [{ variant_id: 20, quantity: 1 }]);
  assert.equal(body.checkout_options.discount, false);
  assert.equal(body.checkout_options.skip_trial, true);
  assert.equal(body.checkout_data.custom.order_id, order.id);
  assert.equal(body.checkout_data.email, undefined);
  assert.throws(() => createLemonClient({ apiKey: '', environment: 'test' }), /missing/);
  const longKeyCalls = [];
  const longKey = 'x'.repeat(1024);
  const longKeyClient = createLemonClient({ apiKey: longKey, environment: 'test', fetcher: async (url, init) => {
    longKeyCalls.push([url, init]); return Response.json(checkout(order));
  } });
  await longKeyClient.variant('20');
  assert.equal(longKeyCalls[0][0].pathname, '/v1/variants/20');
  assert.equal(longKeyCalls[0][0].searchParams.get('include'), 'product');
  assert.equal(longKeyCalls[0][1].headers.Authorization, `Bearer ${longKey}`);
  assert.throws(() => createLemonClient({ apiKey: 'x'.repeat(16385), environment: 'test' }), /too long/);
  assert.throws(() => createLemonClient({ apiKey: 'test api key with spaces', environment: 'test' }), /contains whitespace/);
  assert.throws(() => createLemonClient({ apiKey, environment: 'live' }));
});

test('Lemon money units preserve won and dollar prices', () => {
  assert.equal(toLemonAmount('KRW', 4900), 490000);
  assert.equal(fromLemonAmount('KRW', 490000), 4900);
  assert.equal(toLemonAmount('USD', 399), 399);
  assert.equal(fromLemonAmount('USD', 399), 399);
  assert.throws(() => fromLemonAmount('KRW', 490001));
  assert.throws(() => toLemonAmount('EUR', 399));
});

test('webhook HMAC verifies raw bytes and rejects tampering before DB access', async () => {
  let reads = 0;
  const handle = createLemonWebhookHandler({ environment: 'test', secret: webhookSecret,
    store: { find: async () => { reads++; return null; } } });
  const event = orderEvent(order);
  const raw = JSON.stringify(event);
  for (const changed of [
    { body: `${raw} `, signature: await signature(raw) },
    { body: raw, signature: await signature(raw, 'wrong-secret') },
    { body: raw, signature: '0'.repeat(64) },
  ]) {
    const request = await webhookRequest(changed.body, { headers: { 'X-Signature': changed.signature, 'X-Event-Name': 'order_created' } });
    assert.equal((await handle(request)).status, 401);
  }
  assert.equal((await handle(await webhookRequest(raw, { headers: {} }))).status, 401);
  assert.equal(reads, 0);
  assert.equal((await handle(await webhookRequest(event))).status, 503);
  assert.equal(reads, 1);
});

test('webhook rejects malformed events, ignores unrelated events and hides store failures', async () => {
  const handle = createLemonWebhookHandler({ environment: 'test', secret: webhookSecret,
    store: { find: async () => { throw new Error('secret-value'); } } });
  assert.equal((await handle(await webhookRequest('x'.repeat(262145)))).status, 413);
  assert.equal((await handle(await webhookRequest('{'))).status, 400);
  const other = { meta: { event_name: 'customer_updated' }, data: {} };
  assert.equal((await handle(await webhookRequest(other))).status, 200);
  const failed = await handle(await webhookRequest(orderEvent(order)));
  assert.equal(failed.status, 503);
  assert.equal((await failed.text()).includes('secret-value'), false);
  assert.throws(() => createLemonWebhookHandler({ environment: 'live', secret: webhookSecret }));
  assert.throws(() => createLemonWebhookHandler({ environment: 'test', secret: 'bad' }));
});

test('Supabase adapter keeps server key private and uses Lemon-specific RPCs', async () => {
  const calls = [];
  const store = createPaymentStore({ url: 'https://db.example.test', secretKey: 'sb_secret_server_only', fetcher: async (url, init) => {
    calls.push([url, init]); return Response.json({ order, acquired: true });
  } });
  const requestId = crypto.randomUUID();
  await store.reserve(owner, 'unfold-kr', requestId);
  assert.equal(calls[0][0].pathname, '/rest/v1/rpc/reserve_lemon_checkout');
  assert.equal(calls[0][1].headers.Authorization, undefined);
  const config = { SUPABASE_SECRET_KEYS: '{"default":"sb_secret_example"}', SUPABASE_PUBLISHABLE_KEYS: '{"default":"sb_publishable_example"}' };
  assert.equal(supabaseServerKey(name => config[name]), 'sb_secret_example');
  assert.equal(supabasePublicKey(name => config[name]), 'sb_publishable_example');
});
