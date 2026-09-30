import { fakeReceipt } from "@/testUtils";
import { SubmitOrderRequest } from "@/types/order";
import { createOrderApiClient, OrderApiError } from "./orderApiClient";

const request: SubmitOrderRequest = {
  userId: "u1",
  payableAmount: 12.5,
  currencyCode: "EUR",
  paymentGatewayId: "mock-alpha",
  description: "hi",
};

const receipt = fakeReceipt();

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

function clientWith(fetchMock: jest.Mock, baseUrl = "http://api") {
  return createOrderApiClient(baseUrl, fetchMock as unknown as typeof fetch);
}

function mockFetch(status: number, body?: unknown, baseUrl?: string) {
  const fetchMock = jest.fn().mockResolvedValue(response(status, body));
  return { client: clientWith(fetchMock, baseUrl), fetchMock };
}

const calledUrl = (fetchMock: jest.Mock) => fetchMock.mock.calls[0][0];

const apiError = (message: string) => ({ name: "OrderApiError", message: expect.stringContaining(message) });

describe("submitOrder", () => {
  it("POSTs JSON to /api/v1/orders and returns the receipt", async () => {
    const { client, fetchMock } = mockFetch(200, receipt);

    const result = await client.submitOrder(request, "key-1");

    expect(result).toEqual({ status: "paid", receipt });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api/api/v1/orders");
    expect(init.method).toBe("POST");
    expect(init.headers["Content-Type"]).toBe("application/json");
    expect(init.headers["Idempotency-Key"]).toBe("key-1");
    expect(JSON.parse(init.body)).toEqual(request);
  });

  it.each([
    ["", "/api/v1/orders"],
    ["http://api/", "http://api/api/v1/orders"],
  ])("builds the URL for baseUrl %j", async (baseUrl, expectedUrl) => {
    const { client, fetchMock } = mockFetch(200, receipt, baseUrl);
    await client.submitOrder(request, "key-1");
    expect(calledUrl(fetchMock)).toBe(expectedUrl);
  });

  it("returns a failed outcome (with order number) on 422", async () => {
    const error = { orderNumber: "ORD-1", message: "Declined" };
    const { client } = mockFetch(422, error);

    expect(await client.submitOrder(request, "key-1")).toEqual({ status: "failed", error });
  });

  it("throws OrderApiError with validation messages on 400", async () => {
    const body = { title: "One or more validation errors occurred.", errors: { CurrencyCode: ["Bad currency"] } };
    const { client } = mockFetch(400, body);

    await expect(client.submitOrder(request, "key-1")).rejects.toMatchObject(apiError("Bad currency"));
  });

  it("throws OrderApiError with the problem detail on 409", async () => {
    const { client } = mockFetch(409, { detail: "Key already used." });

    await expect(client.submitOrder(request, "key-1")).rejects.toMatchObject(apiError("Key already used."));
  });

  it("throws a friendly OrderApiError on network failure", async () => {
    const fetchMock = jest.fn().mockRejectedValue(new TypeError("Failed to fetch"));
    await expect(clientWith(fetchMock).submitOrder(request, "key-1")).rejects.toThrow(/reach the server/i);
  });

  it("throws on unexpected status codes", async () => {
    const { client } = mockFetch(500);
    await expect(client.submitOrder(request, "key-1")).rejects.toThrow(/500/);
  });
});

describe("resubmitOrder", () => {
  it("POSTs to the resubmit route and returns the receipt", async () => {
    const { client, fetchMock } = mockFetch(200, receipt);

    const result = await client.resubmitOrder("ORD-1");

    expect(result).toEqual({ status: "paid", receipt });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api/api/v1/orders/ORD-1/resubmit");
    expect(init.method).toBe("POST");
  });

  it("url-encodes the order number", async () => {
    const { client, fetchMock } = mockFetch(200, receipt);
    await client.resubmitOrder("a/b");
    expect(calledUrl(fetchMock)).toBe("http://api/api/v1/orders/a%2Fb/resubmit");
  });

  it("returns failed outcome on 422", async () => {
    const error = { orderNumber: "ORD-1", message: "Declined again" };
    const { client } = mockFetch(422, error);
    expect(await client.resubmitOrder("ORD-1")).toEqual({ status: "failed", error });
  });

  it("throws a not-found error on 404", async () => {
    const { client } = mockFetch(404);
    await expect(client.resubmitOrder("ORD-X")).rejects.toThrow(/not found/i);
  });
});

describe("listOrders", () => {
  it("GETs orders for the (encoded) user", async () => {
    const orders = [{ orderNumber: "ORD-1" }];
    const { client, fetchMock } = mockFetch(200, orders);

    expect(await client.listOrders("a b")).toEqual(orders);
    expect(calledUrl(fetchMock)).toBe("http://api/api/v1/orders?userId=a%20b");
  });

  it("throws on failure", async () => {
    const { client } = mockFetch(500);
    await expect(client.listOrders("u")).rejects.toBeInstanceOf(OrderApiError);
  });
});

describe("listPaymentGateways / listCurrencies", () => {
  it("fetches gateways", async () => {
    const gateways = [{ id: "mock-alpha", name: "Mock Gateway Alpha" }];
    const { client, fetchMock } = mockFetch(200, gateways);

    expect(await client.listPaymentGateways()).toEqual(gateways);
    expect(calledUrl(fetchMock)).toBe("http://api/api/v1/payment-gateways");
  });

  it("fetches currencies", async () => {
    const currencies = [{ code: "EUR", name: "Euro" }];
    const { client, fetchMock } = mockFetch(200, currencies);

    expect(await client.listCurrencies()).toEqual(currencies);
    expect(calledUrl(fetchMock)).toBe("http://api/api/v1/currencies");
  });

  it("throws on failure", async () => {
    const { client } = mockFetch(503);
    await expect(client.listCurrencies()).rejects.toBeInstanceOf(OrderApiError);
  });
});
