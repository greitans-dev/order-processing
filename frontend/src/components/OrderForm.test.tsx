import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrderApiError } from "@/services/orderApiClient";
import { fakeClient, fakeReceipt } from "@/testUtils";
import { OrderForm } from "./OrderForm";

const receipt = fakeReceipt({ paidAmount: 49.9 });
const paid = { status: "paid", receipt } as const;
const unreachable = () => new OrderApiError("Could not reach the server.");

async function renderForm({ client = fakeClient(), onOutcome = jest.fn(), waitForGateways = true } = {}) {
  render(<OrderForm client={client} userId="alice" onOutcome={onOutcome} />);
  if (waitForGateways) await screen.findByRole("option", { name: "Mock Gateway Alpha" });
  return { client, onOutcome };
}

const clickSubmit = () => userEvent.click(screen.getByRole("button", { name: /submit order/i }));

async function submitAmount(amount: string) {
  await userEvent.type(screen.getByLabelText(/amount/i), amount);
  await clickSubmit();
}

function submittedKeys(client: ReturnType<typeof fakeClient>) {
  const [firstKey, secondKey] = client.submitOrder.mock.calls.map((c) => c[1]);
  return { firstKey, secondKey };
}

function failFirstSubmitThenSucceed(client: ReturnType<typeof fakeClient>) {
  client.submitOrder.mockRejectedValueOnce(unreachable());
  client.submitOrder.mockResolvedValueOnce(paid);
}

it("populates gateway and currency selects from the API", async () => {
  await renderForm();
  expect(screen.getByRole("option", { name: "Mock Gateway Beta" })).toBeInTheDocument();
  expect(screen.getByRole("option", { name: /EUR/ })).toBeInTheDocument();
});

it("submits a valid order and reports the outcome", async () => {
  const client = fakeClient();
  client.submitOrder.mockResolvedValue(paid);
  const { onOutcome } = await renderForm({ client });

  await userEvent.type(screen.getByLabelText(/amount/i), "49.90");
  await userEvent.selectOptions(screen.getByLabelText(/payment gateway/i), "mock-beta");
  await userEvent.type(screen.getByLabelText(/description/i), "my notes");
  await clickSubmit();

  expect(client.submitOrder).toHaveBeenCalledWith({
    userId: "alice",
    payableAmount: 49.9,
    currencyCode: "EUR",
    paymentGatewayId: "mock-beta",
    description: "my notes",
  }, expect.any(String));
  expect(onOutcome).toHaveBeenCalledWith(paid);
});

it("passes a decline through as an outcome", async () => {
  const client = fakeClient();
  const error = { orderNumber: "ORD-2", message: "Declined" };
  client.submitOrder.mockResolvedValue({ status: "failed", error });
  const { onOutcome } = await renderForm({ client });

  await submitAmount("20000");

  expect(onOutcome).toHaveBeenCalledWith({ status: "failed", error });
});

it("blocks invalid input before any API call", async () => {
  const { client, onOutcome } = await renderForm();

  await submitAmount("1.999");

  expect(await screen.findByText(/2 decimal places/i)).toBeInTheDocument();
  expect(client.submitOrder).not.toHaveBeenCalled();
  expect(onOutcome).not.toHaveBeenCalled();
});

it("blocks an empty amount", async () => {
  const { client } = await renderForm();
  await clickSubmit();
  expect(await screen.findByText(/amount is required/i)).toBeInTheDocument();
  expect(client.submitOrder).not.toHaveBeenCalled();
});

it("shows an error when the API call throws", async () => {
  const client = fakeClient();
  client.submitOrder.mockRejectedValue(unreachable());
  const { onOutcome } = await renderForm({ client });

  await submitAmount("10");

  expect(await screen.findByRole("alert")).toHaveTextContent("Could not reach the server.");
  expect(onOutcome).not.toHaveBeenCalled();
});

it("limits description length in the textarea", async () => {
  await renderForm();
  expect(screen.getByLabelText(/description/i)).toHaveAttribute("maxlength", "500");
});

it("shows a load error when gateways cannot be fetched", async () => {
  const client = fakeClient({ listPaymentGateways: jest.fn().mockRejectedValue(new OrderApiError("boom")) });
  await renderForm({ client, waitForGateways: false });
  expect(await screen.findByRole("alert")).toHaveTextContent(/boom/);
});

it("reuses the idempotency key when retrying the same order after an error", async () => {
  const client = fakeClient();
  failFirstSubmitThenSucceed(client);
  await renderForm({ client });

  await submitAmount("10");
  await screen.findByRole("alert");
  await clickSubmit();

  const { firstKey, secondKey } = submittedKeys(client);
  expect(secondKey).toBe(firstKey);
});

it("uses a new idempotency key when the order was edited after an error", async () => {
  const client = fakeClient();
  failFirstSubmitThenSucceed(client);
  await renderForm({ client });

  await submitAmount("10");
  await screen.findByRole("alert");
  await submitAmount("5");

  const { firstKey, secondKey } = submittedKeys(client);
  expect(secondKey).not.toBe(firstKey);
});

it("uses a new idempotency key for the next order after a definitive outcome", async () => {
  const client = fakeClient();
  client.submitOrder.mockResolvedValue(paid);
  await renderForm({ client });

  await submitAmount("10");
  await submitAmount("10");

  const { firstKey, secondKey } = submittedKeys(client);
  expect(secondKey).not.toBe(firstKey);
});
