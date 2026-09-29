import { PaymentError, requirePayment, serverUrl, readJson, positiveId, uuidPattern } from './payment-http.mjs';

export function createPaymentStore({ url, secretKey, allowLocalGateway = false, fetcher = fetch }) {
  const base = serverUrl(url, allowLocalGateway);
  if (!secretKey) throw new Error('Supabase server key required');
  async function call(path, body, signal) {
    const headers = { apikey: secretKey, 'Content-Type': 'application/json' };
    // New sb_secret keys belong in apikey only; the legacy service_role is a JWT.
    if (!secretKey.startsWith('sb_secret_')) headers.Authorization = `Bearer ${secretKey}`;
    const response = await fetcher(new URL(path, base), {
      method: body === undefined ? 'GET' : 'POST', headers,
      body: body === undefined ? undefined : JSON.stringify(body), redirect: 'error',
      signal: signal ?? AbortSignal.timeout(8000),
    });
    if (!response.ok) {
      // Never reflect PostgREST details (or service credentials) to callers.
      if (response.status === 400) {
        const result = await readJson(response, 16384).catch(() => ({}));
        const known = { 'Already purchased': ['already_purchased', 409], 'Checkout request conflict': ['checkout_conflict', 409],
          'Checkout rate limited': ['rate_limited', 429], 'Lemon catalog not configured': ['checkout_not_ready', 503] }[result.message];
        if (known) throw new PaymentError(...known);
      }
      throw new PaymentError('payment_store_unavailable');
    }
    if (response.status === 204) return null;
    return readJson(response, 65536);
  }
  const rpc = (name, data, signal) => call(`/rest/v1/rpc/${name}`, data, signal);
  return {
    reserve(userId, priceId, requestId) {
      return rpc('reserve_lemon_checkout', { p_user_id: userId, p_price_id: priceId, p_request_id: requestId });
    },
    bind(orderId, checkoutId) { return rpc('bind_lemon_checkout', { p_order_id: orderId, p_checkout_id: checkoutId }); },
    uncertain(orderId) { return rpc('mark_lemon_checkout_uncertain', { p_order_id: orderId }); },
    release(orderId) { return rpc('release_lemon_checkout', { p_order_id: orderId }); },
    async find(orderId, signal) {
      requirePayment(uuidPattern.test(orderId));
      const query = new URLSearchParams({ select: 'id,user_id,product_id,provider,environment,provider_order_id,provider_checkout_id,provider_price_id,provider_product_id,provider_store_id,provider_checkout_host,currency,amount_minor,tax_minor,total_minor,refunded_total_minor,status',
        id: `eq.${orderId}`, provider: 'eq.lemon', environment: 'eq.test', limit: '2' });
      const rows = await call(`/rest/v1/orders?${query}`, undefined, signal);
      requirePayment(Array.isArray(rows) && rows.length <= 1, 'payment_store_unavailable', 503);
      return rows[0] ?? null;
    },
    apply(order, event, signal) {
      requirePayment(positiveId(event.providerOrderId));
      return rpc('apply_lemon_event', { p_order_id: order.id, p_event_key: event.key, p_provider_order_id: event.providerOrderId,
        p_kind: event.kind, p_currency: order.currency, p_item_price: event.itemPrice,
        p_subtotal: event.subtotal, p_tax: event.tax,
        p_total: event.total, p_refunded_total: event.refundedTotal }, signal);
    },
  };
}

export function supabaseServerKey(get) {
  const json = get('SUPABASE_SECRET_KEYS');
  if (json) {
    const value = JSON.parse(json).default;
    if (typeof value === 'string' && value.startsWith('sb_secret_')) return value;
  }
  return get('SUPABASE_SERVICE_ROLE_KEY') ?? '';
}
export function supabasePublicKey(get) {
  const json = get('SUPABASE_PUBLISHABLE_KEYS');
  if (json) {
    const value = JSON.parse(json).default;
    if (typeof value === 'string' && value.startsWith('sb_publishable_')) return value;
  }
  return get('SUPABASE_ANON_KEY') ?? '';
}
