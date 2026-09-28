import { PaymentError, requirePayment, positiveId, uuidPattern, integerMinor, readJson, sandboxOnly } from './payment-http.mjs';
import { toLemonAmount } from './lemon-money.mjs';

function jsonApi(result, type) {
  requirePayment(result?.data?.type === type && result.data.attributes && typeof result.data.attributes === 'object',
    'payment_provider_unavailable', 503);
  return result.data;
}

export function validateVariant(result, order) {
  const data = jsonApi(result, 'variants');
  const value = data.attributes;
  requirePayment(data.id === order.provider_price_id && String(value.product_id) === order.provider_product_id
    && value.test_mode === true && value.is_subscription === false && value.has_free_trial === false
    && value.pay_what_you_want === false && ['pending', 'published'].includes(value.status));
  const product = result.included?.find(item => item?.type === 'products' && item.id === order.provider_product_id);
  requirePayment(product?.attributes?.status === 'published' && product.attributes.test_mode === true
    && String(product.attributes.store_id) === order.provider_store_id, 'checkout_not_ready', 503);
  return data;
}

export function validateCheckout(result, order, now = Date.now(), requirePreview = true) {
  const data = jsonApi(result, 'checkouts');
  const value = data.attributes;
  const preview = value.preview;
  const providerAmount = toLemonAmount(order.currency, order.amount_minor);
  requirePayment(uuidPattern.test(data.id) && String(value.store_id) === order.provider_store_id
    && String(value.variant_id) === order.provider_price_id, 'checkout_identity_mismatch', 422);
  requirePayment(value.custom_price === providerAmount, 'checkout_amount_mismatch', 422);
  requirePayment(value.test_mode === true, 'checkout_environment_mismatch', 422);
  // Lemon guarantees preview totals only on the create response. A later GET
  // can omit them or return a partial placeholder, so resume validation uses
  // the persisted checkout identity, amount, environment and expiry instead.
  if (requirePreview) {
    requirePayment(preview?.currency === order.currency
      && integerMinor(preview.subtotal) === providerAmount && integerMinor(preview.discount_total) === 0
      && integerMinor(preview.tax) >= 0 && integerMinor(preview.total) === preview.subtotal + preview.tax,
    'checkout_preview_mismatch', 422);
  }
  if (value.expires_at != null) requirePayment(Number.isFinite(Date.parse(value.expires_at)) && Date.parse(value.expires_at) > now,
    'checkout_closed', 409);
  return data;
}

export function checkoutLink(result, order) {
  const data = jsonApi(result, 'checkouts');
  const url = new URL(data.attributes.url);
  requirePayment(url.protocol === 'https:' && url.hostname === order.provider_checkout_host
    && !url.username && !url.password && !url.hash
    && url.pathname === `/checkout/custom/${data.id}`
    && [...url.searchParams.keys()].every(key => ['expires', 'signature'].includes(key)), 'checkout_unavailable', 503);
  return url.href;
}

export function createLemonClient({ environment, apiKey, fetcher = fetch, now = Date.now }) {
  sandboxOnly(environment);
  const token = typeof apiKey === 'string' ? apiKey.trim() : '';
  if (!token) throw new Error('Lemon Squeezy API key missing');
  if (/\s/.test(token)) throw new Error('Lemon Squeezy API key contains whitespace');
  // Lemon Squeezy does not publish an API-key length contract. Keep only a
  // defensive HTTP-header ceiling and let the provider validate the token.
  if (token.length > 16384) throw new Error('Lemon Squeezy API key too long');
  async function call(path, method = 'GET', body) {
    const response = await fetcher(new URL(path, 'https://api.lemonsqueezy.com'), {
      method, redirect: 'error', signal: AbortSignal.timeout(15000),
      headers: { Accept: 'application/vnd.api+json', 'Content-Type': 'application/vnd.api+json', Authorization: `Bearer ${token}` },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) throw new PaymentError('payment_provider_unavailable');
    return readJson(response, 65536);
  }
  return {
    variant(id) { requirePayment(positiveId(id)); return call(`/v1/variants/${id}?include=product`); },
    checkout(id) { requirePayment(uuidPattern.test(id)); return call(`/v1/checkouts/${id}`); },
    create(order) {
      requirePayment(positiveId(order.provider_store_id) && positiveId(order.provider_price_id));
      return call('/v1/checkouts', 'POST', { data: { type: 'checkouts', attributes: {
        custom_price: toLemonAmount(order.currency, order.amount_minor),
        product_options: { enabled_variants: [Number(order.provider_price_id)] },
        checkout_options: { embed: false, discount: false, skip_trial: true },
        checkout_data: { custom: { source: 'unfold-server', order_id: order.id, environment: 'test' },
          variant_quantities: [{ variant_id: Number(order.provider_price_id), quantity: 1 }] },
        preview: true, test_mode: true, expires_at: new Date(now() + 30 * 60 * 1000).toISOString(),
      }, relationships: {
        store: { data: { type: 'stores', id: order.provider_store_id } },
        variant: { data: { type: 'variants', id: order.provider_price_id } },
      } } });
    },
  };
}
