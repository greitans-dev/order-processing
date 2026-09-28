import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrderApiError } from "@/services/orderApiClient";
import { OrderForm } from "./OrderForm";
import { fakeClient } from "./testUtils";

const receipt = {
  orderNumber: "ORD-1",
  paidAmount: 49.9,
  currencyCode: "EUR",
  paidAtUtc: "2026-01-01T00:00:00Z",
  paymentConfirmation: "ALPHA-1",
};

async function renderForm(client = fakeClient(), onOutcome = jest.fn()) {
  render(<OrderForm client={client} userId="alice" onOutcome={onOutcome} />);
  await screen.findByRole("option", { name: "Mock Gateway Alpha" });
  return { client, onOutcome };
}

it("populates gateway and currency selects from the API", async () => {
  await renderForm();
  expect(screen.getByRole("option", { name: "Mock Gateway Beta" })).toBeInTheDocument();
  expect(screen.getByRole("option", { name: /EUR/ })).toBeInTheDocument();
});

it("submits a valid order and reports the outcome", async () => {
  const client = fakeClient();
  client.submitOrder.mockResolvedValue({ status: "paid", receipt });
  const { onOutcome } = await renderForm(client);

  await userEvent.type(screen.getByLabelText(/amount/i), "49.90");
  await userEvent.selectOptions(screen.getByLabelText(/payment gateway/i), "mock-beta");
  await userEvent.type(screen.getByLabelText(/description/i), "my notes");
  await userEvent.click(screen.getByRole("button", { name: /submit order/i }));

  expect(client.submitOrder).toHaveBeenCalledWith({
    userId: "alice",
    payableAmount: 49.9,
    currencyCode: "EUR",
    paymentGatewayId: "mock-beta",
    description: "my notes",
  });
  expect(onOutcome).toHaveBeenCalledWith({ status: "paid", receipt });
});

it("passes a decline through as an outcome", async () => {
  const client = fakeClient();
  const error = { orderNumber: "ORD-2", message: "Declined" };
  client.submitOrder.mockResolvedValue({ status: "failed", error });
  const { onOutcome } = await renderForm(client);

  await userEvent.type(screen.getByLabelText(/amount/i), "20000");
  await userEvent.click(screen.getByRole("button", { name: /submit order/i }));

  expect(onOutcome).toHaveBeenCalledWith({ status: "failed", error });
});

it("blocks invalid input before any API call", async () => {
  const { client, onOutcome } = await renderForm();

  await userEvent.type(screen.getByLabelText(/amount/i), "1.999");
  await userEvent.click(screen.getByRole("button", { name: /submit order/i }));

  expect(await screen.findByText(/2 decimal places/i)).toBeInTheDocument();
  expect(client.submitOrder).not.toHaveBeenCalled();
  expect(onOutcome).not.toHaveBeenCalled();
});

it("blocks an empty amount", async () => {
  const { client } = await renderForm();
  await userEvent.click(screen.getByRole("button", { name: /submit order/i }));
  expect(await screen.findByText(/amount is required/i)).toBeInTheDocument();
  expect(client.submitOrder).not.toHaveBeenCalled();
});

it("shows an error when the API call throws", async () => {
  const client = fakeClient();
  client.submitOrder.mockRejectedValue(new OrderApiError("Could not reach the server."));
  const { onOutcome } = await renderForm(client);

  await userEvent.type(screen.getByLabelText(/amount/i), "10");
  await userEvent.click(screen.getByRole("button", { name: /submit order/i }));

  expect(await screen.findByRole("alert")).toHaveTextContent("Could not reach the server.");
  expect(onOutcome).not.toHaveBeenCalled();
});

it("limits description length in the textarea", async () => {
  await renderForm();
  expect(screen.getByLabelText(/description/i)).toHaveAttribute("maxlength", "500");
});

it("shows a load error when gateways cannot be fetched", async () => {
  const client = fakeClient({ listPaymentGateways: jest.fn().mockRejectedValue(new OrderApiError("boom")) });
  render(<OrderForm client={client} userId="alice" onOutcome={jest.fn()} />);
  expect(await screen.findByRole("alert")).toHaveTextContent(/boom/);
});
