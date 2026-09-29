import { createOrderApiClient, OrderApiError } from "./orderApiClient";
import { SubmitOrderRequest } from "@/types/order";

const request: SubmitOrderRequest = {
  userId: "u1",
  payableAmount: 12.5,
  currencyCode: "EUR",
  paymentGatewayId: "mock-alpha",
  description: "hi",
};

const receipt = {
  orderNumber: "ORD-1",
  paidAmount: 12.5,
  currencyCode: "EUR",
  paidAtUtc: "2026-01-01T00:00:00Z",
  paymentConfirmation: "ALPHA-1",
};

function response(status: number, body?: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => {
      if (body === undefined) throw new Error("no body");
      return body;
    },
  } as Response;
}

function clientWith(fetchImpl: jest.Mock, baseUrl = "http://api") {
  return createOrderApiClient(baseUrl, fetchImpl as unknown as typeof fetch);
}

describe("orderApiClient", () => {
  describe("submitOrder", () => {
    it("POSTs JSON to /api/v1/orders and returns the receipt", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(200, receipt));

      const result = await clientWith(fetchMock).submitOrder(request, "key-1");

      expect(result).toEqual({ status: "paid", receipt });
      const [url, init] = fetchMock.mock.calls[0];
      expect(url).toBe("http://api/api/v1/orders");
      expect(init.method).toBe("POST");
      expect(init.headers["Content-Type"]).toBe("application/json");
      expect(init.headers["Idempotency-Key"]).toBe("key-1");
      expect(JSON.parse(init.body)).toEqual(request);
    });

    it("uses relative URLs when baseUrl is empty", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(200, receipt));
      await clientWith(fetchMock, "").submitOrder(request, "key-1");
      expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/orders");
    });

    it("strips a trailing slash from baseUrl", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(200, receipt));
      await clientWith(fetchMock, "http://api/").submitOrder(request, "key-1");
      expect(fetchMock.mock.calls[0][0]).toBe("http://api/api/v1/orders");
    });

    it("returns a failed outcome (with order number) on 422", async () => {
      const error = { orderNumber: "ORD-1", message: "Declined" };
      const fetchMock = jest.fn().mockResolvedValue(response(422, error));

      expect(await clientWith(fetchMock).submitOrder(request, "key-1")).toEqual({ status: "failed", error });
    });

    it("throws OrderApiError with validation messages on 400", async () => {
      const body = { title: "One or more validation errors occurred.", errors: { CurrencyCode: ["Bad currency"] } };
      const fetchMock = jest.fn().mockResolvedValue(response(400, body));

      const promise = clientWith(fetchMock).submitOrder(request, "key-1");

      await expect(promise).rejects.toBeInstanceOf(OrderApiError);
      await expect(promise).rejects.toThrow("Bad currency");
    });

    it("throws OrderApiError with the problem detail on 409", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(409, { detail: "Key already used." }));

      const promise = clientWith(fetchMock).submitOrder(request, "key-1");

      await expect(promise).rejects.toBeInstanceOf(OrderApiError);
      await expect(promise).rejects.toThrow("Key already used.");
    });

    it("throws a friendly OrderApiError on network failure", async () => {
      const fetchMock = jest.fn().mockRejectedValue(new TypeError("Failed to fetch"));
      await expect(clientWith(fetchMock).submitOrder(request, "key-1")).rejects.toThrow(/reach the server/i);
    });

    it("throws on unexpected status codes", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(500));
      await expect(clientWith(fetchMock).submitOrder(request, "key-1")).rejects.toThrow(/500/);
    });
  });

  describe("resubmitOrder", () => {
    it("POSTs to the resubmit route and returns the receipt", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(200, receipt));

      const result = await clientWith(fetchMock).resubmitOrder("ORD-1");

      expect(result).toEqual({ status: "paid", receipt });
      const [url, init] = fetchMock.mock.calls[0];
      expect(url).toBe("http://api/api/v1/orders/ORD-1/resubmit");
      expect(init.method).toBe("POST");
    });

    it("url-encodes the order number", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(200, receipt));
      await clientWith(fetchMock).resubmitOrder("a/b");
      expect(fetchMock.mock.calls[0][0]).toBe("http://api/api/v1/orders/a%2Fb/resubmit");
    });

    it("returns failed outcome on 422", async () => {
      const error = { orderNumber: "ORD-1", message: "Declined again" };
      const fetchMock = jest.fn().mockResolvedValue(response(422, error));
      expect(await clientWith(fetchMock).resubmitOrder("ORD-1")).toEqual({ status: "failed", error });
    });

    it("throws a not-found error on 404", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(404));
      await expect(clientWith(fetchMock).resubmitOrder("ORD-X")).rejects.toThrow(/not found/i);
    });
  });

  describe("listOrders", () => {
    it("GETs orders for the (encoded) user", async () => {
      const orders = [{ orderNumber: "ORD-1" }];
      const fetchMock = jest.fn().mockResolvedValue(response(200, orders));

      expect(await clientWith(fetchMock).listOrders("a b")).toEqual(orders);
      expect(fetchMock.mock.calls[0][0]).toBe("http://api/api/v1/orders?userId=a%20b");
    });

    it("throws on failure", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(500));
      await expect(clientWith(fetchMock).listOrders("u")).rejects.toBeInstanceOf(OrderApiError);
    });
  });

  describe("listPaymentGateways / listCurrencies", () => {
    it("fetches gateways", async () => {
      const gateways = [{ id: "mock-alpha", name: "Mock Gateway Alpha" }];
      const fetchMock = jest.fn().mockResolvedValue(response(200, gateways));

      expect(await clientWith(fetchMock).listPaymentGateways()).toEqual(gateways);
      expect(fetchMock.mock.calls[0][0]).toBe("http://api/api/v1/payment-gateways");
    });

    it("fetches currencies", async () => {
      const currencies = [{ code: "EUR", name: "Euro" }];
      const fetchMock = jest.fn().mockResolvedValue(response(200, currencies));

      expect(await clientWith(fetchMock).listCurrencies()).toEqual(currencies);
      expect(fetchMock.mock.calls[0][0]).toBe("http://api/api/v1/currencies");
    });

    it("throws on failure", async () => {
      const fetchMock = jest.fn().mockResolvedValue(response(503));
      await expect(clientWith(fetchMock).listCurrencies()).rejects.toBeInstanceOf(OrderApiError);
    });
  });
});
