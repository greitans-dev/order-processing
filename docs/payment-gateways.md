# Payment gateways

Gateways are pluggable. `IPaymentGateway` (with a `GatewayId`) is implemented in `Infrastructure.Payments`.
`PaymentGatewayRegistry` builds a dictionary from every registered `IPaymentGateway`, and the order's
`paymentGatewayId` selects one at runtime. Unknown ids surface as `400`.

## Mock gateways

`mock-alpha` and `mock-beta` decline deterministically when the amount is `>= 10000.00` and approve anything lower
(`9999.99` succeeds, `10000.00` fails). Confirmation codes are prefixed `ALPHA-` and `BETA-`.

## Adding a gateway

1. Add a class implementing `IPaymentGateway` in `backend/src/OrderProcessing.Infrastructure/Payments/`.
2. Add one line to `AddInfrastructure()`: `services.AddSingleton<IPaymentGateway, MyGateway>();`

No other layer changes, and the frontend picks it up from `GET /api/v1/payment-gateways`. To remove a gateway, delete
the registration line and the class.

## Currencies

`SupportedCurrencies` is a static list for the sake of simplicity. The frontend reads the list from `GET /api/v1/currencies`.
