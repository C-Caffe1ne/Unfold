import { PaymentError, requirePayment, positiveId, integerMinor, boundedBytes, safeFailure, sandboxOnly, uuidPattern } from './payment-http.mjs';

export async function verifyLemonSignature(bytes, signature, secret) {
  requirePayment(typeof signature === 'string' && /^[a-f0-9]{64}$/i.test(signature), 'invalid_signature', 401);
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['verify']);
  const digest = Uint8Array.from(signature.match(/../g), pair => parseInt(pair, 16));
  requirePayment(await crypto.subtle.verify('HMAC', key, digest, bytes), 'invalid_signature', 401);
}

function fact(event, order) {
  const name = event.meta?.event_name;
  const data = event.data;
  const value = data?.attributes;
  const item = value?.first_order_item;
  requirePayment(data?.type === 'orders' && positiveId(data.id) && value && typeof value === 'object'
    && value.test_mode === true && String(value.store_id) === order.provider_store_id
    && item?.test_mode === true && String(item.product_id) === order.provider_product_id
    && String(item.variant_id) === order.provider_price_id
    && value.currency === order.currency && value.tax_inclusive === false
    && integerMinor(value.subtotal) === order.amount_minor && integerMinor(value.discount_total) === 0
    && integerMinor(value.tax) >= 0 && integerMinor(value.total) === value.subtotal + value.tax);
  if (name === 'order_created') {
    requirePayment(value.status === 'paid' && !value.refunded && (value.refunded_amount == null || value.refunded_amount === 0));
    return { key: `order_created:${data.id}`, providerOrderId: data.id, kind: 'paid',
      subtotal: value.subtotal, tax: value.tax, total: value.total, refundedTotal: 0 };
  }
  requirePayment(name === 'order_refunded' && ['paid', 'refunded'].includes(value.status));
  const refundedTotal = integerMinor(value.refunded_amount);
  requirePayment(refundedTotal > 0 && refundedTotal <= value.total
    && (value.refunded === true) === (refundedTotal === value.total));
  return { key: `order_refunded:${data.id}:${refundedTotal}`, providerOrderId: data.id, kind: 'refund',
    subtotal: value.subtotal, tax: value.tax, total: value.total, refundedTotal };
}

export function createLemonWebhookHandler({ store, secret, environment }) {
  sandboxOnly(environment);
  if (typeof secret !== 'string' || secret.length < 6 || secret.length > 128) throw new Error('Lemon Squeezy webhook secret required');
  return async request => {
    const reply = body => Response.json(body, { headers: { 'Cache-Control': 'no-store' } });
    try {
      requirePayment(request.method === 'POST', 'method_not_allowed', 405);
      const signature = request.headers.get('X-Signature');
      const eventName = request.headers.get('X-Event-Name');
      requirePayment(signature && eventName, 'invalid_signature', 401);
      const bytes = await boundedBytes(request);
      await verifyLemonSignature(bytes, signature, secret);
      let event;
      try { event = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes)); }
      catch { throw new PaymentError('invalid_event', 400); }
      requirePayment(eventName === event?.meta?.event_name, 'invalid_event', 400);
      if (!['order_created', 'order_refunded'].includes(eventName)) return reply({ received: true, ignored: true });
      const custom = event.meta?.custom_data;
      const orderId = custom?.order_id;
      if (!uuidPattern.test(orderId ?? '')) {
        requirePayment(custom?.source !== 'unfold-server', 'order_not_ready', 503);
        return reply({ received: true, ignored: true });
      }
      const signal = AbortSignal.timeout(4000);
      const order = await store.find(orderId, signal);
      if (!order) {
        requirePayment(custom?.source !== 'unfold-server', 'order_not_ready', 503);
        return reply({ received: true, ignored: true });
      }
      requirePayment(custom?.source === 'unfold-server' && custom?.environment === 'test'
        && order.provider === 'lemon' && order.environment === 'test' && order.product_id === 'unfold');
      const applied = await store.apply(order, fact(event, order), signal);
      requirePayment(typeof applied === 'boolean', 'payment_store_unavailable', 503);
      return reply({ received: true, duplicate: !applied });
    } catch (error) { return safeFailure(error); }
  };
}
