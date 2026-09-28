"use client";

import { OrderReceiptResponse } from "@/types/order";
import { formatAmount } from "./format";

export function ReceiptDisplay({ receipt }: { receipt: OrderReceiptResponse }) {
  return (
    <section className="card success" aria-label="Receipt">
      <h2>Payment successful</h2>
      <dl>
        <dt>Order number</dt>
        <dd>{receipt.orderNumber}</dd>
        <dt>Amount paid</dt>
        <dd>{formatAmount(receipt.paidAmount, receipt.currencyCode)}</dd>
        <dt>Paid at</dt>
        <dd>{new Date(receipt.paidAtUtc).toLocaleString()}</dd>
        <dt>Payment confirmation</dt>
        <dd>{receipt.paymentConfirmation}</dd>
      </dl>
    </section>
  );
}
