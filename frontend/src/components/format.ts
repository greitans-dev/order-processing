export function formatAmount(amount: number, currencyCode: string): string {
  return `${amount.toFixed(2)} ${currencyCode}`;
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString();
}
