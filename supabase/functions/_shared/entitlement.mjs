const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export class ServiceError extends Error {
  constructor(code, status) { super(code); this.status = status; }
}

// Uses the caller's token for BOTH Auth and PostgREST; RLS remains in force.
export function createSupabaseReader({ url, publicKey, allowLocalGateway = false, fetcher = fetch }) {
  const base = new URL(url);
  const loopback = ['localhost', '127.0.0.1', '[::1]'].includes(base.hostname);
  const localGateway = allowLocalGateway && base.hostname === 'kong' && base.port === '8000';
  if (base.protocol !== 'https:' && !(base.protocol === 'http:' && (loopback || localGateway))) throw new Error('HTTPS required');
  if (base.username || base.password || base.pathname !== '/' || base.search || base.hash || !publicKey) throw new Error('Invalid configuration');
  async function read(path, token) {
    const response = await fetcher(new URL(path, base), {
      headers: { apikey: publicKey, Authorization: `Bearer ${token}`, Accept: 'application/json' },
      signal: AbortSignal.timeout(8000), redirect: 'error',
    });
    if (response.status === 401) throw new ServiceError('unauthorized', 401);
    if (!response.ok) throw new ServiceError('service_unavailable', 503);
    return response.json();
  }
  return {
    async user(token) {
      const user = await read('/auth/v1/user', token);
      if (!uuid.test(user?.id ?? '') || user.is_anonymous === true) throw new ServiceError('unauthorized', 401);
      return user.id;
    },
    async entitlement(token, userId, environment) {
      const query = new URLSearchParams({
        select: 'status', user_id: `eq.${userId}`, product_id: 'eq.unfold', environment: `eq.${environment}`, limit: '2',
      });
      const rows = await read(`/rest/v1/entitlements?${query}`, token);
      if (!Array.isArray(rows) || rows.length > 1 || (rows.length && !['active', 'revoked'].includes(rows[0]?.status))) {
        throw new ServiceError('service_unavailable', 503);
      }
      return rows[0]?.status ?? 'unowned';
    },
  };
}

export function createEntitlementHandler({ reader, environment, allowedOrigins = [] }) {
  if (!['test', 'live'].includes(environment)) throw new Error('UNFOLD_ENVIRONMENT must be test or live');
  return async function handle(request) {
    const headers = { 'Cache-Control': 'no-store', Vary: 'Origin' };
    const origin = request.headers.get('Origin');
    const reply = (status, body) => Response.json(body, { status, headers });
    if (origin && !allowedOrigins.includes(origin)) return reply(403, { error: 'origin_not_allowed' });
    if (origin) {
      headers['Access-Control-Allow-Origin'] = origin;
      headers['Access-Control-Allow-Headers'] = 'authorization, apikey, content-type, x-client-info';
      headers['Access-Control-Allow-Methods'] = 'GET, OPTIONS';
    }
    if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers });
    if (request.method !== 'GET') return reply(405, { error: 'method_not_allowed' });
    const authorization = request.headers.get('Authorization') ?? '';
    if (!/^Bearer [^\s]+$/i.test(authorization) || authorization.length > 16384) return reply(401, { error: 'unauthorized' });
    const token = authorization.slice(7);
    try {
      const userId = await reader.user(token);
      const status = await reader.entitlement(token, userId, environment);
      return reply(200, { schema_version: 1, user_id: userId, product_id: 'unfold', environment, status });
    } catch (error) {
      // Never return provider errors, tokens or payloads to a caller or logs.
      return reply(error instanceof ServiceError ? error.status : 503,
        { error: error instanceof ServiceError ? error.message : 'service_unavailable' });
    }
  };
}
