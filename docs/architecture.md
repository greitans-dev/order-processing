# Architecture

Clean Architecture / DDD with dependencies pointing inward only:

```
Domain          -> (nothing)
Application     -> Domain
Infrastructure  -> Application, Domain
Api             -> Application, Infrastructure, Domain   (Infrastructure only for the composition root)
```

The `OrderProcessing.ArchitectureTests` project enforces this: the Domain has no framework or outward
dependencies, Application does not reference Infrastructure or ASP.NET MVC, controllers do not reference
Infrastructure, `IPaymentGateway` implementations live only in `Infrastructure.Payments`, and `IOrderRepository`
implementations only in `Infrastructure.Persistence`.

## Layers

- **Domain**: `Order` aggregate (`Pending` / `Paid` / `Failed`), `OrderNumber`, `Receipt`, `Money` (amount plus
  currency code), `PaymentGatewayId`, and the static `SupportedCurrencies` list (`EUR` only).
- **Application**: one class per use case, `OrderPaymentProcessor` (the locked charge step they share), the
  `IOrderRepository`, `IPaymentGateway` and `IPaymentGatewayRegistry` abstractions, and use-case DTOs.
  These DTOs stay separate from the API's wire contracts.
- **Infrastructure**: in-memory repository, the mock gateway, and the gateway registry.
- **Api**: versioned controllers, wire contracts (`Contracts/V1`), `OrderContractMapper`, OpenAPI and Swagger UI.

## Idempotency and concurrency

Submit and resubmit both run through `OrderPaymentProcessor.ProcessAsync`:

1. Take a per-order-number lock (`OrderLockRegistry`).
2. Re-fetch the order under the lock.
3. If it is already `Paid`, return the existing receipt without calling the gateway.
4. Otherwise charge the gateway and persist `Paid` or `Failed`.

`Order.MarkPaid` and `Order.MarkFailed` throw if the order is already `Paid`, which is the domain half of the
guarantee. Resubmission reuses the same order number. Two concurrent resubmits therefore call the gateway once.

New submissions are made idempotent by a client-generated `Idempotency-Key` (the `IdempotencyKey` value object,
stored on the `Order`). It is separate from the order number, which is server-generated and unknown to the client
when a request has to be retried. `SubmitOrderUseCase` works as follows:

1. Take a lock on `(userId, key)` from the same `OrderLockRegistry`.
2. Look the order up with `IOrderRepository.FindByIdempotencyKeyAsync`.
3. If none exists, create the order and process it. `InMemoryOrderRepository` also enforces key uniqueness per user.
4. If one exists and `Order.MatchesRequest` is false, throw `IdempotencyKeyReuseException` (HTTP 409).
5. Otherwise replay: a `Failed` order returns its stored failure without calling the gateway, anything else goes
   through `OrderPaymentProcessor.ProcessAsync`, which returns the receipt for a `Paid` order.

The `(userId, key)` lock is always taken before the order-number lock, and resubmit only takes the latter, so the two
cannot deadlock.

## Logging

The use cases and `OrderPaymentProcessor` log through `ILogger` with source-generated `[LoggerMessage]` methods, kept
in a `*.Logging.cs` partial next to each class (unique event ids from 1000). Every line carries the order number and,
where relevant, the user and gateway. Because all charges go through the processor, gateway calls are logged in one
place (duration and outcome) and any `IPaymentGateway` is covered without extra code. Declines and gateway timeouts are
warnings; a gateway that throws is an error and leaves the order `Pending`.

The description and other free text are never logged. Expected failures (`IdempotencyKeyReuseException`,
`OrderNotFoundException`, `UnknownPaymentGatewayException`, `UnsupportedCurrencyException`,
`PaymentGatewayTimeoutException`) are mapped to problem+json
answers by `ApplicationExceptionHandler` in `Api/ErrorHandling`, so controllers have no `try`/`catch` for them and
they are not logged as errors. Add new mappings there. Unhandled exceptions are logged once by the exception-handler
middleware, and clients get an `application/problem+json` 500 with a `traceId` and no details. Console scopes are on,
so each line carries the trace id of its request (a `Scopes` array in JSON). Local runs use the plain text console
format, the Docker image uses JSON.

Scopes are configured twice in `appsettings.json` on purpose. A named formatter (`json`, `simple`) reads
`Logging:Console:FormatterOptions:IncludeScopes`; the older `Logging:Console:IncludeScopes` only applies when no
formatter is named. `LoggingConfigurationTests` guards the JSON and simple formatters.

## Scope and non-goals

Real authentication and durable persistence are out of scope on purpose: the app must work out of the box with mocked
gateways, with no identity provider, database or secrets. Both gaps sit behind narrow seams.

- **Authentication:** the frontend "signs in" with any user id (kept in `localStorage`), and the backend trusts the
  `userId` in the payload and in `GET /orders?userId=`. Any caller can act as, and read the orders of, any user. To add
  it, take the user id from a token and authorize `GET /orders` to the caller's own orders.
- **Persistence:** `InMemoryOrderRepository` is the only `IOrderRepository`, and `OrderLockRegistry` holds its
  locks in process memory. Orders and keys are lost on restart, and the no-double-charge guarantee holds for a single
  API instance only. To add it, implement `IOrderRepository` in `Infrastructure.Persistence` with unique constraints on
  the order number and on `(UserId, IdempotencyKey)`, and replace the process-local lock with a distributed lock or
  database-level concurrency control.

## Known limitations

- The lock is process-local. It does not prevent a double charge if the API is scaled to several instances.
- Idempotency keys never expire. They live as long as the order does, that is until restart.
- The other limitations come from the missing authentication and persistence, described above.
