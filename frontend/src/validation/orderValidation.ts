export const MAX_DESCRIPTION_LENGTH = 500;

export interface OrderFormInput {
  payableAmount: string;
  paymentGatewayId: string;
  currencyCode: string;
  description: string;
}

export type OrderValidationErrors = Partial<Record<keyof OrderFormInput, string>>;

const AMOUNT_PATTERN = /^-?\d+(\.\d+)?$/;

export function validateOrderInput(input: OrderFormInput): OrderValidationErrors {
  const errors: OrderValidationErrors = {};

  const amount = input.payableAmount.trim();
  if (amount === "") {
    errors.payableAmount = "Amount is required.";
  } else if (!AMOUNT_PATTERN.test(amount)) {
    errors.payableAmount = "Enter a valid amount, e.g. 49.90.";
  } else if (Number(amount) <= 0) {
    errors.payableAmount = "Amount must be greater than zero.";
  } else if ((amount.split(".")[1] ?? "").length > 2) {
    errors.payableAmount = "Amount can have at most 2 decimal places.";
  }

  if (input.paymentGatewayId.trim() === "") {
    errors.paymentGatewayId = "Payment gateway is required.";
  }
  if (input.currencyCode.trim() === "") {
    errors.currencyCode = "Currency is required.";
  }
  if (input.description.length > MAX_DESCRIPTION_LENGTH) {
    errors.description = `Description must be at most ${MAX_DESCRIPTION_LENGTH} characters.`;
  }

  return errors;
}
