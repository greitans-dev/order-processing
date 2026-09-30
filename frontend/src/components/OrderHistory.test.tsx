import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { fakeClient, fakeClientWithOrders, fakeReceipt } from "@/testUtils";
import { OrderSummaryResponse } from "@/types/order";
import { OrderHistory } from "./OrderHistory";

const paidOrder: OrderSummaryResponse = {
  orderNumber: "ORD-PAID",
  payableAmount: 10,
  currencyCode: "EUR",
  paymentGatewayId: "mock-alpha",
  description: null,
  status: "Paid",
  failureReason: null,
  receipt: fakeReceipt({ orderNumber: "ORD-PAID" }),
  createdAtUtc: "2026-01-01T10:00:00Z",
};

const failedOrder: OrderSummaryResponse = {
  orderNumber: "ORD-FAIL",
  payableAmount: 20000,
  currencyCode: "EUR",
  paymentGatewayId: "mock-beta",
  description: "big",
  status: "Failed",
  failureReason: "Declined",
  receipt: null,
  createdAtUtc: "2026-01-02T10:00:00Z",
};

function renderHistory(client = fakeClient(), onOutcome = jest.fn()) {
  const element = (refreshKey: number) => (
    <OrderHistory client={client} userId="alice" refreshKey={refreshKey} onOutcome={onOutcome} />
  );
  const { rerender } = render(element(0));
  return { client, onOutcome, reloadWith: (refreshKey: number) => rerender(element(refreshKey)) };
}

const clickResubmit = async () => userEvent.click(await screen.findByRole("button", { name: /resubmit/i }));

it("shows an empty state", async () => {
  renderHistory();
  expect(await screen.findByText(/no orders yet/i)).toBeInTheDocument();
});

it("lists orders with amount and currency, and offers resubmit only on failed ones", async () => {
  const { client } = renderHistory(fakeClientWithOrders([paidOrder, failedOrder]));

  const paid = (await screen.findByText("ORD-PAID")).closest("li")!;
  const failed = screen.getByText("ORD-FAIL").closest("li")!;

  expect(within(paid).getByText("10.00 EUR")).toBeInTheDocument();
  expect(within(paid).queryByRole("button", { name: /resubmit/i })).not.toBeInTheDocument();
  expect(within(failed).getByText("20000.00 EUR")).toBeInTheDocument();
  expect(within(failed).getByText(/Declined/)).toBeInTheDocument();
  expect(within(failed).getByRole("button", { name: /resubmit/i })).toBeInTheDocument();
  expect(client.listOrders).toHaveBeenCalledWith("alice");
});

it("shows orders in the order received with their creation time", async () => {
  renderHistory(fakeClientWithOrders([failedOrder, paidOrder]));

  await screen.findByText("ORD-FAIL");
  const items = screen.getAllByRole("listitem");
  expect(items[0]).toHaveTextContent("ORD-FAIL");
  expect(items[1]).toHaveTextContent("ORD-PAID");
  const time = within(items[0]).getByText((_, el) => el?.tagName === "TIME");
  expect(time).toHaveAttribute("datetime", "2026-01-02T10:00:00Z");
  expect(time.textContent).toBe(new Date("2026-01-02T10:00:00Z").toLocaleString());
});

it("resubmits a failed order, reports the outcome and reloads the list", async () => {
  const client = fakeClientWithOrders([paidOrder, failedOrder]);
  const outcome = { status: "paid", receipt: fakeReceipt({ orderNumber: "ORD-FAIL" }) } as const;
  client.resubmitOrder.mockResolvedValue(outcome);
  const { onOutcome } = renderHistory(client);

  await clickResubmit();

  expect(client.resubmitOrder).toHaveBeenCalledWith("ORD-FAIL");
  expect(onOutcome).toHaveBeenCalledWith(outcome);
  await screen.findByText("ORD-FAIL");
  expect(client.listOrders).toHaveBeenCalledTimes(2);
});

it("reloads when refreshKey changes", async () => {
  const { client, reloadWith } = renderHistory();
  await screen.findByText(/no orders yet/i);

  reloadWith(1);

  await screen.findByText(/no orders yet/i);
  expect(client.listOrders).toHaveBeenCalledTimes(2);
});

it("shows an error when loading fails", async () => {
  renderHistory(fakeClient({ listOrders: jest.fn().mockRejectedValue(new Error("nope")) }));
  expect(await screen.findByRole("alert")).toHaveTextContent(/could not load/i);
});

it("shows an error when resubmit throws", async () => {
  const client = fakeClientWithOrders([paidOrder, failedOrder]);
  client.resubmitOrder.mockRejectedValue(new Error("network"));
  renderHistory(client);

  await clickResubmit();

  expect(await screen.findByRole("alert")).toHaveTextContent(/network/);
});
