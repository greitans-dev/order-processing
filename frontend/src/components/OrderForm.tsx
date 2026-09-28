"use client";

import { FormEvent, useEffect, useState } from "react";
import { OrderApiClient, OrderOutcome } from "@/services/orderApiClient";
import { CurrencyResponse } from "@/types/currency";
import { PaymentGatewayResponse } from "@/types/paymentGateway";
import { MAX_DESCRIPTION_LENGTH, OrderValidationErrors, validateOrderInput } from "@/validation/orderValidation";

interface Props {
  client: OrderApiClient;
  userId: string;
  onOutcome: (outcome: OrderOutcome) => void;
}

function messageOf(e: unknown): string {
  return e instanceof Error ? e.message : "Something went wrong. Please try again.";
}

export function OrderForm({ client, userId, onOutcome }: Props) {
  const [gateways, setGateways] = useState<PaymentGatewayResponse[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyResponse[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [payableAmount, setPayableAmount] = useState("");
  const [paymentGatewayId, setPaymentGatewayId] = useState("");
  const [currencyCode, setCurrencyCode] = useState("");
  const [description, setDescription] = useState("");

  const [errors, setErrors] = useState<OrderValidationErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Promise.all([client.listPaymentGateways(), client.listCurrencies()])
      .then(([g, c]) => {
        if (cancelled) return;
        setGateways(g);
        setCurrencies(c);
        setPaymentGatewayId((current) => current || g[0]?.id || "");
        setCurrencyCode((current) => current || c[0]?.code || "");
      })
      .catch((e) => {
        if (!cancelled) setLoadError(messageOf(e));
      });
    return () => {
      cancelled = true;
    };
  }, [client]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitError(null);

    const validation = validateOrderInput({ payableAmount, paymentGatewayId, currencyCode, description });
    setErrors(validation);
    if (Object.keys(validation).length > 0) return;

    setSubmitting(true);
    try {
      const outcome = await client.submitOrder({
        userId,
        payableAmount: Number(payableAmount.trim()),
        currencyCode,
        paymentGatewayId,
        description: description.trim() === "" ? undefined : description,
      });
      setPayableAmount("");
      setDescription("");
      onOutcome(outcome);
    } catch (err) {
      setSubmitError(messageOf(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="card" onSubmit={handleSubmit} noValidate>
      <h2>New order</h2>
      {loadError && <p role="alert" className="error">Could not load payment options: {loadError}</p>}

      <label htmlFor="amount">Amount</label>
      <input
        id="amount"
        type="number"
        step="0.01"
        min="0"
        inputMode="decimal"
        value={payableAmount}
        onChange={(e) => setPayableAmount(e.target.value)}
      />
      {errors.payableAmount && <p className="field-error">{errors.payableAmount}</p>}

      <label htmlFor="currency">Currency</label>
      <select id="currency" value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value)}>
        {currencies.map((c) => (
          <option key={c.code} value={c.code}>{c.code} ({c.name})</option>
        ))}
      </select>
      {errors.currencyCode && <p className="field-error">{errors.currencyCode}</p>}

      <label htmlFor="gateway">Payment gateway</label>
      <select id="gateway" value={paymentGatewayId} onChange={(e) => setPaymentGatewayId(e.target.value)}>
        {gateways.map((g) => (
          <option key={g.id} value={g.id}>{g.name}</option>
        ))}
      </select>
      {errors.paymentGatewayId && <p className="field-error">{errors.paymentGatewayId}</p>}

      <label htmlFor="description">Description (optional)</label>
      <textarea
        id="description"
        rows={3}
        maxLength={MAX_DESCRIPTION_LENGTH}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
      />
      {errors.description && <p className="field-error">{errors.description}</p>}

      {submitError && <p role="alert" className="error">{submitError}</p>}
      <button type="submit" disabled={submitting}>{submitting ? "Processing…" : "Submit order"}</button>
    </form>
  );
}
