import { CurrencyResponse } from "@/types/currency";
import {
  OrderErrorResponse,
  OrderReceiptResponse,
  OrderSummaryResponse,
  SubmitOrderRequest,
} from "@/types/order";
import { PaymentGatewayResponse } from "@/types/paymentGateway";

export type OrderOutcome =
  | { status: "paid"; receipt: OrderReceiptResponse }
  | { status: "failed"; error: OrderErrorResponse };

export class OrderApiError extends Error {
  constructor(message: string, readonly statusCode?: number) {
    super(message);
    this.name = "OrderApiError";
  }
}

export interface OrderApiClient {
  /** Repeating a call with the same idempotency key and request never creates a second order or charge. */
  submitOrder(request: SubmitOrderRequest, idempotencyKey: string): Promise<OrderOutcome>;
  resubmitOrder(orderNumber: string): Promise<OrderOutcome>;
  listOrders(userId: string): Promise<OrderSummaryResponse[]>;
  listPaymentGateways(): Promise<PaymentGatewayResponse[]>;
  listCurrencies(): Promise<CurrencyResponse[]>;
}

const API_ROOT = "/api/v1";

export function createOrderApiClient(baseUrl = "", fetchImpl: typeof fetch = (...args) => fetch(...args)): OrderApiClient {
  const root = baseUrl.replace(/\/+$/, "") + API_ROOT;

  async function send(path: string, init?: RequestInit): Promise<Response> {
    try {
      return await fetchImpl(root + path, init);
    } catch {
      throw new OrderApiError("Could not reach the server. Please check your connection and try again.");
    }
  }

  async function readJson<T>(response: Response): Promise<T> {
    return (await response.json()) as T;
  }

  async function validationMessage(response: Response): Promise<string> {
    try {
      const body = await readJson<{ title?: string; errors?: Record<string, string[]> }>(response);
      const messages = Object.values(body.errors ?? {}).flat();
      if (messages.length > 0) return messages.join(" ");
      if (body.title) return body.title;
    } catch {
      // fall through to the generic message
    }
    return "The order was rejected as invalid.";
  }

  async function conflictMessage(response: Response): Promise<string> {
    try {
      const body = await readJson<{ detail?: string }>(response);
      if (body.detail) return body.detail;
    } catch {
      // fall through to the generic message
    }
    return "This order conflicts with an earlier submission. Please review it and try again.";
  }

  async function processOrder(path: string, init: RequestInit): Promise<OrderOutcome> {
    const response = await send(path, init);
    switch (response.status) {
      case 200:
        return { status: "paid", receipt: await readJson<OrderReceiptResponse>(response) };
      case 422:
        return { status: "failed", error: await readJson<OrderErrorResponse>(response) };
      case 400:
        throw new OrderApiError(await validationMessage(response), 400);
      case 404:
        throw new OrderApiError("Order not found.", 404);
      case 409:
        throw new OrderApiError(await conflictMessage(response), 409);
      default:
        throw new OrderApiError(`Unexpected server response (${response.status}).`, response.status);
    }
  }

  async function getList<T>(path: string): Promise<T[]> {
    const response = await send(path);
    if (!response.ok) {
      throw new OrderApiError(`Unexpected server response (${response.status}).`, response.status);
    }
    return readJson<T[]>(response);
  }

  return {
    submitOrder: (request, idempotencyKey) =>
      processOrder("/orders", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
        body: JSON.stringify(request),
      }),
    resubmitOrder: (orderNumber) =>
      processOrder(`/orders/${encodeURIComponent(orderNumber)}/resubmit`, { method: "POST" }),
    listOrders: (userId) => getList<OrderSummaryResponse>(`/orders?userId=${encodeURIComponent(userId)}`),
    listPaymentGateways: () => getList<PaymentGatewayResponse>("/payment-gateways"),
    listCurrencies: () => getList<CurrencyResponse>("/currencies"),
  };
}

/** Client wired to the configured backend; empty base URL means same-origin (Docker / wwwroot). */
export const orderApiClient = createOrderApiClient(process.env.NEXT_PUBLIC_API_BASE_URL ?? "");
