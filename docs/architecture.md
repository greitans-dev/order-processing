# Architecture

Clean Architecture / DDD with dependencies pointing inward only:

```
Domain          -> (nothing)
Application     -> Domain
Infrastructure  -> Application, Domain
Api             -> Application, Infrastructure, Domain   (Infrastructure only for the composition root)
```

The `OrderProcessing.ArchitectureTests` project (NetArchTest) enforces this: the Domain has no framework or outward
dependencies, Application does not reference Infrastructure or ASP.NET MVC, controllers do not reference
Infrastructure, `IPaymentGateway` implementations live only in `Infrastructure.Payments`, and `IOrderRepository`
implementations only in `Infrastructure.Persistence`.

## Layers

- **Domain**: `Order` aggregate (`Pending` / `Paid` / `Failed`), `OrderNumber`, `Receipt`, `Money` (amount plus
  currency code), `PaymentGatewayId`, and the static `SupportedCurrencies` list (`EUR` only).
- **Application**: one class per use case, `OrderPaymentProcessor` (the locked charge step they share), the `IOrderRepository`, `IPaymentGateway` and
  `IPaymentGatewayRegistry` abstractions, and use-case DTOs. These DTOs stay separate from the API's wire contracts.

  `Application/Orders` is grouped by use case, and each folder has a matching namespace (`OrderProcessing.Application.Orders.<Folder>`):

  | Folder | Contents |
  |---|---|
  | `Submit/`, `Resubmit/`, `Listing/` | One use case each (`SubmitOrderUseCase`, `ResubmitOrderUseCase`, `GetUserOrdersUseCase`, each with `ExecuteAsync`), its `*.Logging.cs` partial, and its input type (`SubmitOrderCommand`, `ResubmitOrderCommand`, `GetUserOrdersQuery`). `Listing/` also holds `OrderSummaryDto` |
  | `Payments/` | `OrderPaymentProcessor`, the charge step shared by submit and resubmit, and the result types it produces (`OrderProcessingResult`, `OrderProcessingOutcome`, `OrderProcessingError`, `OrderReceiptDto`) |
  | `Locking/` | `OrderNumberLockRegistry` |

  `OrderDtoMapper` stays in `Orders/` because several folders use it. Tests mirror this layout.
- **Infrastructure**: in-memory repository, the mock gateways, and the gateway registry.
- **Api**: versioned controllers, wire contracts (`Contracts/V1`), `OrderContractMapper`, OpenAPI and Swagger UI.

## Idempotency and concurrency

Submit and resubmit both run through `OrderPaymentProcessor.ProcessAsync`:

1. Take a per-order-number lock (`OrderNumberLockRegistry`, a singleton holding one `SemaphoreSlim` per order number).
2. Re-fetch the order under the lock.
3. If it is already `Paid`, return the existing receipt without calling the gateway.
4. Otherwise charge the gateway and persist `Paid` or `Failed`.

`Order.MarkPaid` and `Order.MarkFailed` throw if the order is already `Paid`, which is the domain half of the
guarantee. Resubmission reuses the same order number. Two concurrent resubmits therefore call the gateway once.

New submissions are made idempotent by a client-generated `Idempotency-Key` (the `IdempotencyKey` value object,
stored on the `Order`). It is separate from the order number, which is server-generated and unknown to the client
when a request has to be retried. `SubmitOrderUseCase` works as follows:

1. Take a lock on `(userId, key)` from the same `OrderNumberLockRegistry`.
2. Look the order up with `IOrderRepository.FindByIdempotencyKeyAsync`.
3. If none exists, create the order and process it. `InMemoryOrderRepository` also enforces key uniqueness per user.
4. If one exists and `Order.MatchesRequest` is false, throw `IdempotencyKeyReuseException` (HTTP 409).
5. Otherwise replay: a `Failed` order returns its stored failure without calling the gateway, anything else goes
   through `OrderPaymentProcessor.ProcessAsync`, which returns the receipt for a `Paid` order.

The `(userId, key)` lock is always taken before the order-number lock, and resubmit only takes the latter, so the two
cannot deadlock.

## Logging

The use cases and `OrderPaymentProcessor` log through `ILogger` with source-generated `[LoggerMessage]`
methods (event ids 1000+; 1000-1002 in `SubmitOrderUseCase`, 1003 in `ResubmitOrderUseCase`, 1004 and 1010-1013 in the
processor). Every line carries the order number and, where relevant, the user
and gateway. Because all charges go through the processor, gateway calls are logged in one place (duration and outcome)
and any `IPaymentGateway` is covered without extra code.

| Level | Events |
|---|---|
| Information | order created, idempotent replay, resubmit requested, order already paid, charge succeeded |
| Warning | idempotency key reused with a different payload, charge declined (with the reason) |
| Warning | a gateway did not answer within `Payments:GatewayTimeout`; the order stays `Pending` (HTTP 504) |
| Error | a gateway threw; the order stays `Pending` and the exception is rethrown |
| Debug | charge started |

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

Real authentication and durable persistence were left out of scope on purpose. The task is an order-submission flow
that works out of the box with mocked gateways, so the app needs no identity provider, no database and no secrets. Both
gaps are isolated behind narrow seams, so they can be closed without reworking the design.

### Authentication

- **What exists:** the frontend "signs in" with any user id, which `UserContext` keeps in `localStorage`. The backend has
  no authentication or authorization. It trusts the `userId` in the submit payload and in `GET /orders?userId=`.
- **Consequences:** any caller can submit orders as, and read the order history of, any user. Idempotency keys are
  scoped to the `userId`, so they are only as trustworthy as that value.
- **To add it:** authenticate requests on the API, take the user id from the token instead of the payload and query
  string, authorize `GET /orders` to the caller's own orders, and replace the `localStorage` login in the frontend.

### Persistence

- **What exists:** `InMemoryOrderRepository`, a singleton `ConcurrentDictionary`, is the only `IOrderRepository`.
  `OrderNumberLockRegistry` holds its locks in process memory.
- **Consequences:** orders and idempotency keys are lost on restart. The lock is process-local, so the no-double-charge
  guarantee holds for a single API instance only.
- **To add it:** implement `IOrderRepository` in `Infrastructure.Persistence` (the only place the architecture tests
  allow it) with unique constraints on the order number and on `(UserId, IdempotencyKey)`. Replace the process-local
  lock with a distributed lock or database-level concurrency control, and decide how long idempotency keys live.

## Known limitations

- The lock is process-local. It does not prevent a double charge if the API is scaled to several instances.
- The lock dictionary grows with each distinct order number and idempotency key for the life of the process.
- Idempotency keys never expire. They live as long as the order does, that is until restart.
- The limitations that come from the missing authentication and persistence are described above.
