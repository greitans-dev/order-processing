export function formatAmount(amount: number, currencyCode: string): string {
  return `${amount.toFixed(2)} ${currencyCode}`;
}
