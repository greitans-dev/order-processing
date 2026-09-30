import { OrderApiClient } from "@/services/orderApiClient";
import { OrderReceiptResponse, OrderSummaryResponse } from "@/types/order";

export function fakeClient(overrides: Partial<OrderApiClient> = {}): jest.Mocked<OrderApiClient> {
  return {
    submitOrder: jest.fn(),
    resubmitOrder: jest.fn(),
    listOrders: jest.fn().mockResolvedValue([]),
    listPaymentGateways: jest.fn().mockResolvedValue([
      { id: "mock-alpha", name: "Mock Gateway Alpha" },
      { id: "mock-beta", name: "Mock Gateway Beta" },
    ]),
    listCurrencies: jest.fn().mockResolvedValue([{ code: "EUR", name: "Euro" }]),
    ...overrides,
  } as jest.Mocked<OrderApiClient>;
}

export function fakeClientWithOrders(orders: OrderSummaryResponse[]): jest.Mocked<OrderApiClient> {
  return fakeClient({ listOrders: jest.fn().mockResolvedValue(orders) });
}

export function fakeReceipt(overrides: Partial<OrderReceiptResponse> = {}): OrderReceiptResponse {
  return {
    orderNumber: "ORD-1",
    paidAmount: 12.5,
    currencyCode: "EUR",
    paidAtUtc: "2026-01-01T00:00:00Z",
    paymentConfirmation: "ALPHA-1",
    ...overrides,
  };
}
