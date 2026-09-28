import { OrderApiClient } from "@/services/orderApiClient";

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
