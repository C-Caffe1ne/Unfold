import { createEntitlementHandler, createSupabaseReader } from '../_shared/entitlement.mjs';

const environment = Deno.env.get('UNFOLD_ENVIRONMENT');
const handler = createEntitlementHandler({
  reader: createSupabaseReader({
    url: Deno.env.get('SUPABASE_URL') ?? '',
    publicKey: Deno.env.get('SUPABASE_ANON_KEY') ?? '',
    allowLocalGateway: environment === 'test',
  }),
  environment,
  allowedOrigins: (Deno.env.get('UNFOLD_ALLOWED_ORIGINS') ?? '').split(',').map(value => value.trim()).filter(Boolean),
});
Deno.serve(handler);
