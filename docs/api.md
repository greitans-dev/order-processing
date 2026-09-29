# API

Versioned REST API under `/api/v1`. The OpenAPI document is at `/openapi/v1.json` and Swagger UI at `/swagger`, which
are the source of truth for schemas.

| Method | Route | Purpose | Responses |
|---|---|---|---|
| `POST` | `/api/v1/orders` | Submit a new order and attempt payment. Requires the `Idempotency-Key` header | `200` receipt; `422` error including `orderNumber`; `409` the key was already used with a different payload; `400` validation failure (missing or invalid `Idempotency-Key`, missing fields, amount <= 0, description > 500, unknown gateway, unsupported currency) |
| `POST` | `/api/v1/orders/{orderNumber}/resubmit` | Retry payment idempotently | `200` receipt (existing if already paid); `422` declined again; `404` unknown order |
| `GET` | `/api/v1/orders?userId=` | A user's order history, newest first (each item has `createdAtUtc`) | `200`; `400` if `userId` is missing |
| `GET` | `/api/v1/payment-gateways` | Available gateways (`id`, `name`) | `200` |
| `GET` | `/api/v1/currencies` | Supported currencies (`code`, `name`) | `200` |

## Idempotency key

`POST /api/v1/orders` needs an `Idempotency-Key` header: any non-blank string of at most 255 characters without control
characters, for example a UUID. The client generates it once per order attempt and reuses it when it retries the same
request, for example after a timeout. It is not the order number, which the server generates and the client only learns
from the response.

- The first request with a key creates the order and charges it.
- A repeat with the same key and the same payload never creates another order or charge. It returns the original
  outcome: the receipt, or the same `422` with the same `orderNumber`. To retry a failed payment, use the resubmit
  endpoint.
- The same key with a different payload returns `409`.
- Keys are scoped to the `userId` in the payload.

A `422` body always contains `orderNumber` and a `message` that is safe to show to the end user.
