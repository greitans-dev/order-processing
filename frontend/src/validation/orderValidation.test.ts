import {
  MAX_DESCRIPTION_LENGTH,
  OrderFormInput,
  validateOrderInput,
} from "./orderValidation";

const valid: OrderFormInput = {
  payableAmount: "99.90",
  paymentGatewayId: "mock-alpha",
  currencyCode: "EUR",
  description: "notes",
};

describe("validateOrderInput", () => {
  it("returns no errors for valid input", () => {
    expect(validateOrderInput(valid)).toEqual({});
  });

  it("accepts integer amounts and an empty description", () => {
    expect(validateOrderInput({ ...valid, payableAmount: "10", description: "" })).toEqual({});
  });

  it.each(["", "   "])("requires an amount (%j)", (payableAmount) => {
    expect(validateOrderInput({ ...valid, payableAmount }).payableAmount).toMatch(/required/i);
  });

  it.each(["abc", "1e5", "1,5", "--3", "12.", ".5"])("rejects non-numeric amount %j", (payableAmount) => {
    expect(validateOrderInput({ ...valid, payableAmount }).payableAmount).toMatch(/valid amount/i);
  });

  it.each(["0", "0.00", "-5"])("rejects zero or negative amount %j", (payableAmount) => {
    expect(validateOrderInput({ ...valid, payableAmount }).payableAmount).toMatch(/greater than zero/i);
  });

  it("rejects more than two decimal places", () => {
    expect(validateOrderInput({ ...valid, payableAmount: "1.999" }).payableAmount).toMatch(/2 decimal/i);
  });

  it("requires a payment gateway", () => {
    expect(validateOrderInput({ ...valid, paymentGatewayId: "" }).paymentGatewayId).toMatch(/required/i);
  });

  it("requires a currency", () => {
    expect(validateOrderInput({ ...valid, currencyCode: "" }).currencyCode).toMatch(/required/i);
  });

  it("accepts a description of exactly the maximum length", () => {
    const description = "x".repeat(MAX_DESCRIPTION_LENGTH);
    expect(validateOrderInput({ ...valid, description }).description).toBeUndefined();
  });

  it("rejects a description over the maximum length", () => {
    const description = "x".repeat(MAX_DESCRIPTION_LENGTH + 1);
    expect(validateOrderInput({ ...valid, description }).description).toMatch(/500/);
  });

  it("reports several errors at once", () => {
    const errors = validateOrderInput({ payableAmount: "", paymentGatewayId: "", currencyCode: "", description: "" });
    expect(Object.keys(errors).sort()).toEqual(["currencyCode", "payableAmount", "paymentGatewayId"]);
  });
});
