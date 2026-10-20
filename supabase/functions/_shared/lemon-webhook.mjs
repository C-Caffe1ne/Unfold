import { PaymentError, requirePayment, positiveId, boundedBytes, safeFailure, paymentEnvironment, uuidPattern } from './payment-http.mjs';
import { fromLemonAmount, lemonAmountMicros, toLemonAmount } from './lemon-money.mjs';

const emailPattern = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export async function verifyLemonSignature(bytes, signature, secret) {
  requirePayment(typeof signature === 'string' && /^[a-f0-9]{64}$/i.test(signature), 'invalid_signature', 401);
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['verify']);
  const digest = Uint8Array.from(signature.match(/../g), pair => parseInt(pair, 16));
  requirePayment(await crypto.subtle.verify('HMAC', key, digest, bytes), 'invalid_signature', 401);
}

function inclusiveGrossMatches(value, currency, expected, reported) {
  if (reported === lemonAmountMicros(expected)) return true;
  // Lemon settles a non-USD price in rounded USD cents, then converts it back.
  // Prove that rounding using the signed USD totals and exchange rate instead
  // of accepting an arbitrary tolerance around the contracted item price.
  if (currency === 'USD' || typeof value.currency_rate !== 'string'
    || !/^\d{1,8}(\.\d{1,12})?$/.test(value.currency_rate)) return false;
  const rate = Number(value.currency_rate);
  const usd = value.total_usd;
  return Number.isFinite(rate) && rate > 0
    && Number.isSafeInteger(usd) && usd > 0 && usd <= 2147483647
    && value.subtotal_usd === usd
    && Number.isSafeInteger(value.tax_usd) && value.tax_usd >= 0 && value.tax_usd <= usd
    && (value.setup_fee_usd ?? 0) === 0 && (value.discount_total_usd ?? 0) === 0
    && Math.round(expected * rate) === usd
    && Math.abs(reported / 1_000_000 - usd / rate) <= 1;
}

function fact(event, order) {
  const name = event.meta?.event_name;
  const data = event.data;
  const value = data?.attributes;
  const item = value?.first_order_item;
  const providerSubtotal = lemonAmountMicros(value?.subtotal);
  const providerTax = lemonAmountMicros(value?.tax);
  const providerTotal = lemonAmountMicros(value?.total);
  const providerRefunded = lemonAmountMicros(value?.refunded_amount ?? 0);
  const providerItemPrice = lemonAmountMicros(item?.price);
  const expectedSubtotal = toLemonAmount(order.currency, order.amount_minor);
  requirePayment(data?.type === 'orders' && positiveId(data.id) && value && typeof value === 'object', 'payment_object_mismatch');
  requirePayment(value.test_mode === (order.environment === 'test') && item?.test_mode === (order.environment === 'test'),
    'payment_environment_mismatch');
  requirePayment(String(value.store_id) === order.provider_store_id && String(item.product_id) === order.provider_product_id
    && String(item.variant_id) === order.provider_price_id, 'payment_product_mismatch');
  requirePayment(value.currency === order.currency, 'payment_currency_mismatch');
  requirePayment(typeof value.tax_inclusive === 'boolean', 'payment_tax_mode_mismatch');
  requirePayment(!value.tax_inclusive || inclusiveGrossMatches(value, order.currency, expectedSubtotal, providerTotal),
    'payment_gross_mismatch');
  requirePayment(providerItemPrice === lemonAmountMicros(expectedSubtotal), 'payment_item_price_mismatch');
  requirePayment(lemonAmountMicros(value.discount_total) === 0, 'payment_discount_mismatch');
  requirePayment(lemonAmountMicros(value.setup_fee ?? 0) === 0, 'payment_setup_fee_mismatch');
  // Inclusive subtotal already contains VAT. Store a net subtotal so the
  // persisted receipt's total = net subtotal + tax contract remains intact.
  const netSubtotal = providerSubtotal - (value.tax_inclusive ? providerTax : 0);
  requirePayment(netSubtotal > 0 && (value.tax_inclusive
    ? providerTotal === providerSubtotal
    : providerTotal === providerSubtotal + providerTax), 'payment_totals_mismatch');
  const itemPrice = fromLemonAmount(order.currency, item.price);
  const subtotal = fromLemonAmount(order.currency, netSubtotal / 1_000_000);
  const tax = fromLemonAmount(order.currency, value.tax);
  const total = fromLemonAmount(order.currency, value.total);
  const refundedTotal = fromLemonAmount(order.currency, value.refunded_amount ?? 0);
  if (name === 'order_created') {
    requirePayment(value.status === 'paid' && !value.refunded && providerRefunded === 0);
    return { key: `order_created:${data.id}`, providerOrderId: data.id, kind: 'paid',
      itemPrice, subtotal, tax, total, refundedTotal: 0 };
  }
  requirePayment(name === 'order_refunded' && ['paid', 'partial_refund', 'refunded'].includes(value.status));
  requirePayment(providerRefunded > 0 && providerRefunded <= providerTotal
    && (value.refunded === true) === (providerRefunded === providerTotal));
  return { key: `order_refunded:${data.id}:${refundedTotal}`, providerOrderId: data.id, kind: 'refund',
    itemPrice, subtotal, tax, total, refundedTotal };
}

// A purchase from the public store page carries no Unfold order. It must match a catalog price
// exactly; the server then keeps it for the Google account with the buyer's email.
async function storePurchase(store, environment, event, signal) {
  const value = event.data?.attributes;
  const item = value?.first_order_item;
  const ids = [value?.store_id, item?.product_id, item?.variant_id].map(id => String(id ?? ''));
  requirePayment(ids.every(positiveId), 'payment_object_mismatch');
  const price = await store.storePrice(...ids, signal);
  if (!price) return { received: true, ignored: true };
  const [storeId, productId, variantId] = ids;
  const payment = fact(event, { environment, provider_store_id: storeId, provider_product_id: productId,
    provider_price_id: variantId, currency: price.currency, amount_minor: price.amount_minor });
  const email = typeof value.user_email === 'string' ? value.user_email.trim().toLowerCase() : '';
  requirePayment(email.length <= 320 && emailPattern.test(email), 'payment_email_mismatch');
  const recorded = await store.recordStorePurchase(price, payment, email, signal);
  requirePayment(typeof recorded === 'boolean', 'payment_store_unavailable', 503);
  return { received: true, duplicate: !recorded };
}

export function createLemonWebhookHandler({ store, secret, environment, report = () => {} }) {
  paymentEnvironment(environment);
  if (typeof secret !== 'string' || secret.length < 6 || secret.length > 128) throw new Error('Lemon Squeezy webhook secret required');
  return async request => {
    const reply = body => Response.json(body, { headers: { 'Cache-Control': 'no-store' } });
    let stage = 'request';
    try {
      requirePayment(request.method === 'POST', 'method_not_allowed', 405);
      const signature = request.headers.get('X-Signature');
      const eventName = request.headers.get('X-Event-Name');
      requirePayment(signature && eventName, 'invalid_signature', 401);
      const bytes = await boundedBytes(request);
      stage = 'signature';
      await verifyLemonSignature(bytes, signature, secret);
      let event;
      try { event = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes)); }
      catch { throw new PaymentError('invalid_event', 400); }
      stage = 'event';
      requirePayment(eventName === event?.meta?.event_name, 'invalid_event', 400);
      if (!['order_created', 'order_refunded'].includes(eventName)) return reply({ received: true, ignored: true });
      const custom = event.meta?.custom_data;
      const orderId = custom?.order_id;
      const signal = AbortSignal.timeout(4000);
      if (!uuidPattern.test(orderId ?? '')) {
        requirePayment(custom?.source !== 'unfold-server', 'order_not_ready', 503);
        stage = 'store';
        return reply(await storePurchase(store, environment, event, signal));
      }
      stage = 'order';
      const order = await store.find(orderId, signal);
      if (!order) {
        requirePayment(custom?.source !== 'unfold-server', 'order_not_ready', 503);
        return reply({ received: true, ignored: true });
      }
      requirePayment(custom?.source === 'unfold-server' && custom?.environment === environment
        && order.provider === 'lemon' && order.environment === environment && order.product_id === 'unfold');
      stage = 'payment';
      const applied = await store.apply(order, fact(event, order), signal);
      requirePayment(typeof applied === 'boolean', 'payment_store_unavailable', 503);
      return reply({ received: true, duplicate: !applied });
    } catch (error) {
      report(`${stage}:${error instanceof PaymentError ? error.message : 'service_unavailable'}`);
      return safeFailure(error);
    }
  };
}
