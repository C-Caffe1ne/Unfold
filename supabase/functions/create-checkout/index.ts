import { createSupabaseReader } from '../_shared/entitlement.mjs';
import { createCheckoutHandler } from '../_shared/checkout.mjs';
import { createLemonClient } from '../_shared/lemon.mjs';
import { createPaymentStore, supabasePublicKey, supabaseServerKey } from '../_shared/payment-store.mjs';

const get = (name: string) => Deno.env.get(name);
const environment = get('UNFOLD_ENVIRONMENT');
const url = get('SUPABASE_URL') ?? '';
const enabled = get('UNFOLD_CHECKOUT_ENABLED') === 'true';
Deno.serve(createCheckoutHandler({
  environment,
  enabled,
  reader: createSupabaseReader({ url, publicKey: supabasePublicKey(get), allowLocalGateway: environment === 'test' }),
  store: createPaymentStore({ url, secretKey: supabaseServerKey(get), allowLocalGateway: environment === 'test' }),
  lemon: enabled ? createLemonClient({ environment, apiKey: get('LEMONSQUEEZY_TEST_API_KEY') }) : {},
  allowedOrigins: (get('UNFOLD_ALLOWED_ORIGINS') ?? '').split(',').map(value => value.trim()).filter(Boolean),
}));
