"use client";

import { useCallback, useEffect, useState } from "react";
import { OrderApiClient, OrderOutcome } from "@/services/orderApiClient";
import { OrderSummaryResponse } from "@/types/order";
import { formatAmount, formatDateTime } from "./format";

interface Props {
  client: OrderApiClient;
  userId: string;
  /** Bump to force a reload, e.g. after a new order was submitted. */
  refreshKey: number;
  onOutcome: (outcome: OrderOutcome) => void;
}

export function OrderHistory({ client, userId, refreshKey, onOutcome }: Props) {
  const [orders, setOrders] = useState<OrderSummaryResponse[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [resubmitting, setResubmitting] = useState<string | null>(null);

  const load = useCallback(async (isCancelled: () => boolean = () => false) => {
    try {
      const result = await client.listOrders(userId);
      if (isCancelled()) return;
      setOrders(result);
      setLoadError(null);
    } catch {
      if (!isCancelled()) setLoadError("Could not load your orders.");
    }
  }, [client, userId]);

  useEffect(() => {
    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- data fetch on mount / refreshKey change
    void load(() => cancelled);
    return () => {
      cancelled = true;
    };
  }, [load, refreshKey]);

  async function resubmit(orderNumber: string) {
    setActionError(null);
    setResubmitting(orderNumber);
    try {
      onOutcome(await client.resubmitOrder(orderNumber));
    } catch (e) {
      setActionError(e instanceof Error ? e.message : "Resubmit failed.");
    } finally {
      setResubmitting(null);
    }
    await load();
  }

  return (
    <section className="card" aria-label="Order history">
      <h2>Your orders</h2>
      {loadError && <p role="alert" className="error">{loadError}</p>}
      {actionError && <p role="alert" className="error">{actionError}</p>}
      {orders?.length === 0 && <p className="hint">No orders yet.</p>}
      <ul className="orders">
        {orders?.map((o) => (
          <li key={o.orderNumber}>
            <div>
              <strong>{o.orderNumber}</strong>{" "}
              <span className={`status status-${o.status.toLowerCase()}`}>{o.status}</span>
            </div>
            <div className="hint"><time dateTime={o.createdAtUtc}>{formatDateTime(o.createdAtUtc)}</time></div>
            <div>{formatAmount(o.payableAmount, o.currencyCode)}</div>
            {o.status === "Failed" && o.failureReason && <div className="error">{o.failureReason}</div>}
            {o.status === "Failed" && (
              <button type="button" disabled={resubmitting === o.orderNumber} onClick={() => resubmit(o.orderNumber)}>
                {resubmitting === o.orderNumber ? "Resubmitting…" : "Resubmit"}
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
