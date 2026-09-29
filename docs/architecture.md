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
- **Application**: `OrderProcessingService` (use cases), the `IOrderRepository`, `IPaymentGateway` and
  `IPaymentGatewayRegistry` abstractions, and use-case DTOs. These DTOs stay separate from the API's wire contracts.
- **Infrastructure**: in-memory repository, the mock gateways, and the gateway registry.
- **Api**: versioned controllers, wire contracts (`Contracts/V1`), `OrderContractMapper`, OpenAPI and Swagger UI.

## Idempotency and concurrency

Submit and resubmit both run through `OrderProcessingService.ProcessPaymentAsync`:

1. Take a per-order-number lock (`OrderNumberLockRegistry`, a singleton holding one `SemaphoreSlim` per order number).
2. Re-fetch the order under the lock.
3. If it is already `Paid`, return the existing receipt without calling the gateway.
4. Otherwise charge the gateway and persist `Paid` or `Failed`.

`Order.MarkPaid` and `Order.MarkFailed` throw if the order is already `Paid`, which is the domain half of the
guarantee. Resubmission reuses the same order number. Two concurrent resubmits therefore call the gateway once.

New submissions are made idempotent by a client-generated `Idempotency-Key` (the `IdempotencyKey` value object,
stored on the `Order`). It is separate from the order number, which is server-generated and unknown to the client
when a request has to be retried. `SubmitNewOrderAsync` works as follows:

1. Take a lock on `(userId, key)` from the same `OrderNumberLockRegistry`.
2. Look the order up with `IOrderRepository.FindByIdempotencyKeyAsync`.
3. If none exists, create the order and process it. `InMemoryOrderRepository` also enforces key uniqueness per user.
4. If one exists and `Order.MatchesRequest` is false, throw `IdempotencyKeyReuseException` (HTTP 409).
5. Otherwise replay: a `Failed` order returns its stored failure without calling the gateway, anything else goes
   through `ProcessPaymentAsync`, which returns the receipt for a `Paid` order.

The `(userId, key)` lock is always taken before the order-number lock, and resubmit only takes the latter, so the two
cannot deadlock.

### Known limitations

- The lock is process-local. It does not prevent a double charge if the API is scaled to several instances.
- The lock dictionary grows with each distinct order number and idempotency key for the life of the process.
- Idempotency keys never expire. They live as long as the order does, that is until restart.
- `GET /api/v1/orders?userId=` has no authorization. Auth is simulated in the frontend and the backend trusts the
  `userId` it receives.
- Persistence is in memory, so orders are lost on restart.
