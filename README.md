# Order Processing

An order-submission app for "XYZ Inc.": a Next.js frontend and an ASP.NET Core (.NET 10) backend that forwards orders
to a payment gateway and returns a receipt or a user-displayable error. It works out of the box with two mocked
gateways, with no API keys or setup.

## Prerequisites

- .NET 10 SDK
- Node.js 20+ and pnpm (frontend)
- Docker (only for the container path)

## Run locally

Backend (plain HTTP on `http://localhost:5080`, Swagger UI at `http://localhost:5080/swagger`):

```sh
cd backend/src/OrderProcessing.Api
dotnet run -lp dev
```

Frontend (`http://localhost:3000`):

```sh
cd frontend
pnpm install
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 pnpm dev
```

## Run with Docker

```sh
docker compose up --build
```

Only port 8080 is exposed: UI at `http://localhost:8080`, Swagger UI at `http://localhost:8080/swagger`.

## Tests

```sh
dotnet test backend/OrderProcessing.sln
cd frontend && pnpm test
```

## Try it

Sign in with any user ID. A mock gateway approves amounts below 10,000.00 and declines 10,000.00 and above, so
`9999.99` succeeds and `10000.00` fails. Failed orders can be resubmitted from the order history; resubmitting a paid
order never charges again.

## Documentation

- [Architecture](docs/architecture.md)
- [API](docs/api.md)
- [Payment gateways](docs/payment-gateways.md)
- [Running locally](docs/running-locally.md)
- [Deployment](docs/deployment.md)
