import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrderSummaryResponse } from "@/types/order";
import { OrderHistory } from "./OrderHistory";
import { fakeClient } from "./testUtils";

const receipt = {
  orderNumber: "ORD-PAID",
  paidAmount: 10,
  currencyCode: "EUR",
  paidAtUtc: "2026-01-01T00:00:00Z",
  paymentConfirmation: "ALPHA-1",
};

const orders: OrderSummaryResponse[] = [
  { orderNumber: "ORD-PAID", payableAmount: 10, currencyCode: "EUR", paymentGatewayId: "mock-alpha", description: null, status: "Paid", failureReason: null, receipt, createdAtUtc: "2026-01-01T10:00:00Z" },
  { orderNumber: "ORD-FAIL", payableAmount: 20000, currencyCode: "EUR", paymentGatewayId: "mock-beta", description: "big", status: "Failed", failureReason: "Declined", receipt: null, createdAtUtc: "2026-01-02T10:00:00Z" },
];

it("shows an empty state", async () => {
  render(<OrderHistory client={fakeClient()} userId="alice" refreshKey={0} onOutcome={jest.fn()} />);
  expect(await screen.findByText(/no orders yet/i)).toBeInTheDocument();
});

it("lists orders with amount and currency, and offers resubmit only on failed ones", async () => {
  const client = fakeClient({ listOrders: jest.fn().mockResolvedValue(orders) });
  render(<OrderHistory client={client} userId="alice" refreshKey={0} onOutcome={jest.fn()} />);

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
  const client = fakeClient({ listOrders: jest.fn().mockResolvedValue([orders[1], orders[0]]) });
  render(<OrderHistory client={client} userId="alice" refreshKey={0} onOutcome={jest.fn()} />);

  await screen.findByText("ORD-FAIL");
  const items = screen.getAllByRole("listitem");
  expect(items[0]).toHaveTextContent("ORD-FAIL");
  expect(items[1]).toHaveTextContent("ORD-PAID");
  const time = within(items[0]).getByText((_, el) => el?.tagName === "TIME");
  expect(time).toHaveAttribute("datetime", "2026-01-02T10:00:00Z");
  expect(time.textContent).toBe(new Date("2026-01-02T10:00:00Z").toLocaleString());
});

it("resubmits a failed order, reports the outcome and reloads the list", async () => {
  const client = fakeClient({ listOrders: jest.fn().mockResolvedValue(orders) });
  client.resubmitOrder.mockResolvedValue({ status: "paid", receipt: { ...receipt, orderNumber: "ORD-FAIL" } });
  const onOutcome = jest.fn();
  render(<OrderHistory client={client} userId="alice" refreshKey={0} onOutcome={onOutcome} />);

  await userEvent.click(await screen.findByRole("button", { name: /resubmit/i }));

  expect(client.resubmitOrder).toHaveBeenCalledWith("ORD-FAIL");
  expect(onOutcome).toHaveBeenCalledWith({ status: "paid", receipt: expect.objectContaining({ orderNumber: "ORD-FAIL" }) });
  await screen.findByText("ORD-FAIL");
  expect(client.listOrders).toHaveBeenCalledTimes(2);
});

it("reloads when refreshKey changes", async () => {
  const client = fakeClient();
  const { rerender } = render(<OrderHistory client={client} userId="alice" refreshKey={0} onOutcome={jest.fn()} />);
  await screen.findByText(/no orders yet/i);

  rerender(<OrderHistory client={client} userId="alice" refreshKey={1} onOutcome={jest.fn()} />);

  await screen.findByText(/no orders yet/i);
  expect(client.listOrders).toHaveBeenCalledTimes(2);
});

it("shows an error when loading fails", async () => {
  const client = fakeClient({ listOrders: jest.fn().mockRejectedValue(new Error("nope")) });
  render(<OrderHistory client={client} userId="alice" refreshKey={0} onOutcome={jest.fn()} />);
  expect(await screen.findByRole("alert")).toHaveTextContent(/could not load/i);
});

it("shows an error when resubmit throws", async () => {
  const client = fakeClient({ listOrders: jest.fn().mockResolvedValue(orders) });
  client.resubmitOrder.mockRejectedValue(new Error("network"));
  render(<OrderHistory client={client} userId="alice" refreshKey={0} onOutcome={jest.fn()} />);

  await userEvent.click(await screen.findByRole("button", { name: /resubmit/i }));

  expect(await screen.findByRole("alert")).toHaveTextContent(/network/);
});
