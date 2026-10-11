import { createPaymentStore, supabaseServerKey } from '../_shared/payment-store.mjs';
import { createLemonWebhookHandler } from '../_shared/lemon-webhook.mjs';

const get = (name: string) => Deno.env.get(name);
const environment = get('UNFOLD_ENVIRONMENT');
const url = get('SUPABASE_URL') ?? '';
Deno.serve(createLemonWebhookHandler({
  environment,
  secret: get(environment === 'live' ? 'LEMONSQUEEZY_LIVE_WEBHOOK_SECRET' : 'LEMONSQUEEZY_WEBHOOK_SECRET'),
  store: createPaymentStore({ environment, url, secretKey: supabaseServerKey(get), allowLocalGateway: environment === 'test' }),
}));
