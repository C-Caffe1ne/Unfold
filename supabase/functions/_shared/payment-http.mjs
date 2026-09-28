export const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const positiveId = value => typeof value === 'string' && /^[1-9][0-9]{0,18}$/.test(value);
export class PaymentError extends Error {
  constructor(code, status = 503) { super(code); this.status = status; }
}
export function requirePayment(condition, code = 'payment_mismatch', status = 422) {
  if (!condition) throw new PaymentError(code, status);
}
export function minor(value) {
  requirePayment(typeof value === 'string' && /^(0|[1-9][0-9]{0,9})$/.test(value));
  const amount = Number(value);
  requirePayment(Number.isSafeInteger(amount) && amount <= 2147483647);
  return amount;
}
export function integerMinor(value) {
  requirePayment(Number.isSafeInteger(value) && value >= 0 && value <= 2147483647);
  return value;
}
export function serverUrl(value, allowLocalGateway = false) {
  const url = new URL(value);
  const local = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)
    || (allowLocalGateway && url.hostname === 'kong' && url.port === '8000');
  if ((url.protocol !== 'https:' && !(url.protocol === 'http:' && local))
    || url.username || url.password || url.pathname !== '/' || url.search || url.hash) throw new Error('Invalid server URL');
  return url;
}
export async function boundedBytes(body, limit = 262144) {
  const length = body.headers.get('content-length');
  if (length && (!/^\d+$/.test(length) || Number(length) > limit)) throw new PaymentError('payload_too_large', 413);
  if (!body.body) return new Uint8Array();
  const reader = body.body.getReader();
  const chunks = [];
  let size = 0;
  try {
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > limit) { await reader.cancel(); throw new PaymentError('payload_too_large', 413); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const result = new Uint8Array(size);
  let offset = 0;
  for (const chunk of chunks) { result.set(chunk, offset); offset += chunk.byteLength; }
  return result;
}
export async function readJson(body, limit) {
  const bytes = await boundedBytes(body, limit);
  try { return JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes)); }
  catch { throw new PaymentError('invalid_json', 400); }
}
export function safeFailure(error, headers = {}) {
  return Response.json({ error: error instanceof PaymentError ? error.message : 'service_unavailable' }, {
    status: error instanceof PaymentError ? error.status : 503,
    headers: { 'Cache-Control': 'no-store', ...headers },
  });
}
export function sandboxOnly(environment) {
  if (environment !== 'test') throw new Error('Payment server currently requires UNFOLD_ENVIRONMENT=test');
}
