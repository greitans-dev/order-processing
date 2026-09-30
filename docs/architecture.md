# Architecture

Clean Architecture / DDD with dependencies pointing inward only:

```
Domain          -> (nothing)
Application     -> Domain
Infrastructure  -> Application, Domain
Api             -> Application, Infrastructure, Domain   (Infrastructure only for the composition root)
```

The `OrderProcessing.ArchitectureTests` project (NetArchTest) enforces these rules, so it is the source of truth for them.

## Layers

- **Domain**: the `Order` aggregate and its value objects, including `Money` and the static list of supported currencies.
- **Application**: one class per use case, the `OrderPaymentProcessor` charge step they share, and the abstractions for
  repositories and payment gateways. Its DTOs stay separate from the API's wire contracts.
- **Infrastructure**: the in-memory repository, the mock gateway and the gateway registry.
- **Api**: versioned controllers, wire contracts, OpenAPI and Swagger UI.

## Idempotency and concurrency

All charges go through `OrderPaymentProcessor.ProcessAsync`. It takes a per-order lock (`OrderLockRegistry`), re-reads
the order, returns the existing receipt if the order is `Paid`, and otherwise charges the gateway and saves `Paid` or
`Failed`. `Order.MarkPaid` and `MarkFailed` throw on a paid order, which is the domain half of the guarantee. Resubmit
goes straight to the processor, so concurrent resubmits charge once.

Submit is made idempotent by a client-generated `Idempotency-Key`, which is separate from the server-generated order
number. `SubmitOrderUseCase` takes a `(userId, key)` lock and looks the order up by key:

- No order: create it and process it.
- An order with a different payload: `IdempotencyKeyReuseException` (409).
- Otherwise a replay: a `Failed` order returns its stored failure without a charge, and anything else goes through the
  processor.

The key lock is always taken before the order lock, and resubmit takes only the latter, so they cannot deadlock.

## Logging

- Log through `ILogger<T>` with `[LoggerMessage]` methods, kept in a `*.Logging.cs` partial next to each class. Never
  log the order description or full payloads.
- Expected failures are mapped to problem+json by `ApplicationExceptionHandler` and are not logged as errors. Add new
  mappings there. Unhandled exceptions are logged once and answered with a 500 that has a `traceId` and no details.
- `IncludeScopes` is set under both `Logging:Console` and `Logging:Console:FormatterOptions` in `appsettings.json` on
  purpose: a named formatter ignores the former. `LoggingConfigurationTests` guards this.

## Scope and non-goals

Real authentication and durable persistence are out of scope: the app must work out of the box with mocked gateways.
Both gaps sit behind narrow seams.

- **Authentication:** the frontend "signs in" with any user id, and the backend trusts the `userId` it is given, so any
  caller can act as or read the orders of any user. To add it, take the user id from a token and authorize
  `GET /orders` to the caller's own orders.
- **Persistence:** `InMemoryOrderRepository` is the only `IOrderRepository`, and `OrderLockRegistry` is process-local.
  Orders and idempotency keys are lost on restart and never expire, and the no-double-charge guarantee holds for one
  API instance only. To add it, implement `IOrderRepository` in `Infrastructure.Persistence` with unique constraints
  on the order number and on `(UserId, IdempotencyKey)`, and replace the lock with a distributed one.
