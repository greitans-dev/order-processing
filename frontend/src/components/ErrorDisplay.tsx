"use client";

import { OrderErrorResponse } from "@/types/order";

export function ErrorDisplay({ error }: { error: OrderErrorResponse }) {
  return (
    <section className="card failure" role="alert">
      <h2>Payment failed</h2>
      <p>{error.message}</p>
      <p className="hint">
        Order number: <strong>{error.orderNumber}</strong>. You can resubmit this order from your order history below.
      </p>
    </section>
  );
}
