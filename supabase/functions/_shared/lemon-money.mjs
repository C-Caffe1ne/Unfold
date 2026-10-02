import { integerMinor, requirePayment } from './payment-http.mjs';

const scales = Object.freeze({ KRW: 100, USD: 1 });
const decimalScale = 1_000_000;

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

export function lemonAmountMicros(amount) {
  requirePayment(typeof amount === 'number' && Number.isFinite(amount) && amount >= 0 && amount <= 2147483647);
  const value = Math.round(amount * decimalScale);
  // Scaling a valid decimal can introduce binary residue at larger amounts.
  // Round-trip the integer micros instead, preserving the six-decimal limit.
  requirePayment(Number.isSafeInteger(value) && value / decimalScale === amount);
  return value;
}

export function fromLemonAmount(currency, amount) {
  const value = lemonAmountMicros(amount);
  return Number((value / decimalScale / scale(currency)).toFixed(8));
}
