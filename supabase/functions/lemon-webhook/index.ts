import { createPaymentStore, supabaseServerKey } from '../_shared/payment-store.mjs';
import { createLemonWebhookHandler } from '../_shared/lemon-webhook.mjs';

const get = (name: string) => Deno.env.get(name);
const environment = get('UNFOLD_ENVIRONMENT');
const url = get('SUPABASE_URL') ?? '';
Deno.serve(createLemonWebhookHandler({
  environment,
  secret: get('LEMONSQUEEZY_WEBHOOK_SECRET'),
  store: createPaymentStore({ url, secretKey: supabaseServerKey(get), allowLocalGateway: environment === 'test' }),
  report: code => console.error(`lemon-webhook failed: ${code}`),
}));
