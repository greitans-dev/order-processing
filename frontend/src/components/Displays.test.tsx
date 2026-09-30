import { render, screen } from "@testing-library/react";
import { fakeReceipt } from "@/testUtils";
import { ErrorDisplay } from "./ErrorDisplay";
import { ReceiptDisplay } from "./ReceiptDisplay";

it("shows receipt details", () => {
  render(<ReceiptDisplay receipt={fakeReceipt({ paidAmount: 5, paymentConfirmation: "ALPHA-9" })} />);
  expect(screen.getByText("ORD-1")).toBeInTheDocument();
  expect(screen.getByText("5.00 EUR")).toBeInTheDocument();
  expect(screen.getByText("ALPHA-9")).toBeInTheDocument();
});

it("shows the error message and order number", () => {
  render(<ErrorDisplay error={{ orderNumber: "ORD-2", message: "Declined: limit" }} />);
  expect(screen.getByRole("alert")).toHaveTextContent("Declined: limit");
  expect(screen.getByRole("alert")).toHaveTextContent("ORD-2");
});
