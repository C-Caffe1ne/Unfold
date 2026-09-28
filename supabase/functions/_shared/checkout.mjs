import { ServiceError } from './entitlement.mjs';
import { PaymentError, requirePayment, uuidPattern, readJson, safeFailure, sandboxOnly } from './payment-http.mjs';
import { validateVariant, validateCheckout, checkoutLink } from './lemon.mjs';

export function createCheckoutHandler({ reader, store, lemon, environment, allowedOrigins = [], enabled = true, now = Date.now }) {
  sandboxOnly(environment);
  return async request => {
    const headers = { 'Cache-Control': 'no-store', Vary: 'Origin' };
    const reply = (status, body) => Response.json(body, { status, headers });
    let acquiredOrder;
    let createAttempted = false;
    try {
      const origin = request.headers.get('Origin');
      requirePayment(!origin || allowedOrigins.includes(origin), 'origin_not_allowed', 403);
      if (origin) Object.assign(headers, { 'Access-Control-Allow-Origin': origin,
        'Access-Control-Allow-Headers': 'authorization, apikey, content-type, x-client-info', 'Access-Control-Allow-Methods': 'POST, OPTIONS' });
      if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers });
      requirePayment(request.method === 'POST', 'method_not_allowed', 405);
      const authorization = request.headers.get('Authorization') ?? '';
      requirePayment(/^Bearer [^\s]+$/i.test(authorization) && authorization.length <= 16384, 'unauthorized', 401);
      requirePayment((request.headers.get('Content-Type') ?? '').split(';')[0].trim() === 'application/json', 'unsupported_media_type', 415);
      const userId = await reader.user(authorization.slice(7));
      requirePayment(enabled, 'checkout_not_ready', 503);
      const body = await readJson(request, 2048);
      requirePayment(body && !Array.isArray(body) && Object.keys(body).length === 2
        && ['KR', 'GLOBAL'].includes(body.market) && uuidPattern.test(body.request_id ?? ''), 'invalid_checkout_request', 400);
      const reserved = await store.reserve(userId, body.market === 'KR' ? 'unfold-kr' : 'unfold-global', body.request_id);
      const order = reserved?.order;
      requirePayment(order?.user_id === userId && order.provider === 'lemon' && order.environment === 'test'
        && order.product_id === 'unfold' && order.status === 'pending' && uuidPattern.test(order.id)
        && typeof reserved.acquired === 'boolean', 'payment_store_unavailable', 503);
      let checkout;
      if (reserved.acquired) {
        acquiredOrder = order.id;
        validateVariant(await lemon.variant(order.provider_price_id), order);
        createAttempted = true;
        checkout = await lemon.create(order);
        const data = validateCheckout(checkout, order, now());
        checkoutLink(checkout, order);
        await store.bind(order.id, data.id);
        acquiredOrder = undefined;
      } else {
        requirePayment(order.checkout_state === 'ready' && order.provider_checkout_id, 'checkout_pending', 409);
        checkout = await lemon.checkout(order.provider_checkout_id);
        validateCheckout(checkout, order, now());
      }
      const data = checkout.data;
      return reply(200, { schema_version: 1, order_id: order.id, checkout_id: data.id,
        environment: 'test', checkout_url: checkoutLink(checkout, order) });
    } catch (error) {
      if (acquiredOrder) await (createAttempted ? store.uncertain(acquiredOrder) : store.release(acquiredOrder)).catch(() => {});
      if (error instanceof ServiceError) error = new PaymentError(error.message, error.status);
      return safeFailure(error, headers);
    }
  };
}
