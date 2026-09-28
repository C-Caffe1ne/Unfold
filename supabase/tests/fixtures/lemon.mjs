export const apiKey = 'ls_test_api_key_not_a_real_secret';
export const webhookSecret = 'lemon-test-webhook-secret';
export const now = 1790575200000;
export const checkoutHost = 'unfold-test.lemonsqueezy.com';
const scale = currency => currency === 'KRW' ? 100 : 1;
const providerAmount = (order, amount) => amount * scale(order.currency);
export function variant(order) {
  return { data: { type: 'variants', id: order.provider_price_id, attributes: {
    product_id: Number(order.provider_product_id), status: 'pending', test_mode: true,
    is_subscription: false, has_free_trial: false, pay_what_you_want: false,
  } }, included: [{ type: 'products', id: order.provider_product_id, attributes: {
    store_id: Number(order.provider_store_id), status: 'published', test_mode: true,
  } }] };
}
export function checkout(order, number = 1, tax = order.currency === 'KRW' ? 490 : 0) {
  const id = `00000000-0000-4000-8000-${String(number).padStart(12, '0')}`;
  return { data: { type: 'checkouts', id, attributes: {
    store_id: Number(order.provider_store_id), variant_id: Number(order.provider_price_id),
    custom_price: providerAmount(order, order.amount_minor), test_mode: true,
    preview: { currency: order.currency, subtotal: providerAmount(order, order.amount_minor), discount_total: 0,
      tax: providerAmount(order, tax), total: providerAmount(order, order.amount_minor + tax) },
    expires_at: new Date(now + 30 * 60 * 1000).toISOString(),
    url: `https://${order.provider_checkout_host}/checkout/custom/${id}?expires=1790577000&signature=${'a'.repeat(64)}`,
  } } };
}
export function orderEvent(order, providerOrderId = 1, refundedAmount = 0, tax = order.currency === 'KRW' ? 490 : 0) {
  const total = order.amount_minor + tax;
  const refunded = refundedAmount === total;
  const name = refundedAmount > 0 ? 'order_refunded' : 'order_created';
  return { meta: { event_name: name, custom_data: { source: 'unfold-server', order_id: order.id, environment: 'test' } },
    data: { type: 'orders', id: String(providerOrderId), attributes: {
      store_id: Number(order.provider_store_id), currency: order.currency,
      subtotal: providerAmount(order, order.amount_minor), discount_total: 0,
      tax: providerAmount(order, tax), total: providerAmount(order, total), tax_inclusive: false,
      status: refunded ? 'refunded' : 'paid', refunded,
      refunded_amount: providerAmount(order, refundedAmount), test_mode: true,
      first_order_item: { product_id: Number(order.provider_product_id), variant_id: Number(order.provider_price_id),
        price: providerAmount(order, order.amount_minor), test_mode: true },
    } } };
}
export async function signature(raw, secret = webhookSecret) {
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const bytes = new Uint8Array(await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(raw)));
  return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
}
export async function webhookRequest(body, options = {}) {
  const raw = typeof body === 'string' ? body : JSON.stringify(body);
  const eventName = typeof body === 'string' ? 'order_created' : body.meta?.event_name ?? 'order_created';
  return new Request('https://example.test/lemon-webhook', { method: 'POST', body: raw,
    headers: { 'X-Signature': await signature(raw), 'X-Event-Name': eventName, 'Content-Type': 'application/json' }, ...options });
}
export function checkoutRequest(body, options = {}) {
  return new Request('https://example.test/create-checkout', { method: 'POST', body: JSON.stringify(body),
    headers: { Authorization: 'Bearer user-token', 'Content-Type': 'application/json' }, ...options });
}
