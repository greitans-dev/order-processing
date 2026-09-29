import { newIdempotencyKey } from "./idempotencyKey";

describe("newIdempotencyKey", () => {
  it("returns a non-empty key that is unique per call", () => {
    const a = newIdempotencyKey();
    expect(a.length).toBeGreaterThan(0);
    expect(newIdempotencyKey()).not.toBe(a);
  });

  it("falls back to getRandomValues when randomUUID is unavailable", () => {
    const original = crypto.randomUUID;
    Object.defineProperty(crypto, "randomUUID", { value: undefined, configurable: true });
    try {
      expect(newIdempotencyKey()).toMatch(/^[0-9a-f]{32}$/);
    } finally {
      Object.defineProperty(crypto, "randomUUID", { value: original, configurable: true });
    }
  });
});
