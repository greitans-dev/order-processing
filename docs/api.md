# API

Versioned REST API under `/api/v1`. The OpenAPI document is at `/openapi/v1.json` and Swagger UI at `/swagger`, which
are the source of truth for schemas.

| Method | Route | Purpose | Responses |
|---|---|---|---|
| `POST` | `/api/v1/orders` | Submit a new order and attempt payment | `200` receipt; `422` error including `orderNumber`; `400` validation failure (missing fields, amount <= 0, description > 500, unknown gateway, unsupported currency) |
| `POST` | `/api/v1/orders/{orderNumber}/resubmit` | Retry payment idempotently | `200` receipt (existing if already paid); `422` declined again; `404` unknown order |
| `GET` | `/api/v1/orders?userId=` | A user's order history | `200`; `400` if `userId` is missing |
| `GET` | `/api/v1/payment-gateways` | Available gateways (`id`, `name`) | `200` |
| `GET` | `/api/v1/currencies` | Supported currencies (`code`, `name`) | `200` |

A `422` body always contains `orderNumber` and a `message` that is safe to show to the end user.
