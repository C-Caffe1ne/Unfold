import { integerMinor, requirePayment } from './payment-http.mjs';

const scales = Object.freeze({ KRW: 100, USD: 1 });

function scale(currency) {
  const value = scales[currency];
  requirePayment(Number.isSafeInteger(value), 'unsupported_currency', 422);
  return value;
}

export function toLemonAmount(currency, amount) {
  integerMinor(amount);
  const value = amount * scale(currency);
  requirePayment(Number.isSafeInteger(value) && value <= 2147483647, 'invalid_amount', 422);
  return value;
}

export function fromLemonAmount(currency, amount) {
  integerMinor(amount);
  const divisor = scale(currency);
  requirePayment(amount % divisor === 0, 'payment_mismatch', 422);
  return amount / divisor;
}
