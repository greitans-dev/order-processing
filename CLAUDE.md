# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

This file is a living document maintained by both the human and you, the AI agent. Update this document
whenever there are significant changes in the project - architecture shift, refactoring, upgrades e.t.c.

If the file becomes too long, split it into separate parts under `docs/` directory and store only links to these
files here.

## Current state

The backend and frontend are implemented and verified (backend: `dotnet test backend/OrderProcessing.sln`; frontend: `cd frontend && pnpm test`; `docker compose up --build` serves UI and API on port 8080). The spec is `task.md` and the plan is `plan.md`, both in the git-ignored `.local/` directory. The layout and commands below match the code. User-facing docs live in `docs/`, linked from `README.md`.

Notes:
- The frontend was scaffolded by `create-next-app`, which added `frontend/AGENTS.md` and `frontend/CLAUDE.md`. Next.js 16 docs ship in `frontend/node_modules/next/dist/docs/`.
- The frontend runs with `NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 pnpm dev` against the local backend.

## Committing the changes

Never add `Co-Authored-By:` trailer or other attributions to the commit messages.

Keep the commit message short. Briefly describe *what* was changed and *why*, if it is not obvious.

When working on multi-step or multi-phase changes, try to commit the changes in each step separately, making sure that the code is always in a deployable state.


## What is being built

An order-submission web app for "XYZ Inc.": a Next.js frontend (TypeScript, client components only, Jest) and an ASP.NET Core backend (.NET 10, C#, xUnit + Moq + Shouldly + NetArchTest) that forwards orders to one of several payment gateways and returns a receipt or a user-displayable error. It must work out of the box with mocked gateways, with no API keys.

## Layout and commands

- `backend/` holds `OrderProcessing.sln` and `src/{Domain,Application,Infrastructure,Api}`, with matching `tests/*.Tests` projects. Architecture tests live in their own project, `OrderProcessing.ArchitectureTests`, as the spec requires.
- `frontend/` holds the Next.js app under `src/{app,components,context,services,types,validation}`.
- Backend: `cd backend/src/OrderProcessing.Api && dotnet run -lp dev` serves plain HTTP on `http://localhost:5080`, with Swagger UI at `/swagger`.
- Backend tests: `dotnet test backend/OrderProcessing.sln`. Single test: `dotnet test backend/OrderProcessing.sln --filter "FullyQualifiedName~<TestName>"`.
- Frontend: `cd frontend && pnpm install && pnpm dev`, then `pnpm test` (Jest).
- Docker: `docker compose up --build` exposes only port 8080.
- Docs go in `docs/`. The root `README.md` is brief and links to them.

## Architecture (big picture)

Clean Architecture / DDD with strict inward dependencies: `Domain → nothing`, `Application → Domain`, `Infrastructure → Application + Domain`, `Api → all` (the Infrastructure reference is for the composition root only). The architecture-test project enforces this. Controllers must not reference Infrastructure, `IPaymentGateway` implementations live only in `Infrastructure.Payments`, and `IOrderRepository` implementations only in `Infrastructure.Persistence`.

**Payment gateways are pluggable.** `IPaymentGateway` (with `GatewayId`) is implemented in Infrastructure. `IPaymentGatewayRegistry` builds a dictionary from `IEnumerable<IPaymentGateway>` injected by DI. The order payload's `PaymentGatewayId` string picks the gateway at runtime. Adding or removing a gateway means one class plus one DI registration line, with no other layer touched. There are two mocks, `mock-alpha` and `mock-beta`. Both decline deterministically when `amount >= 10000.00`, so `9999.99` succeeds and `10000.00` fails.

**Idempotency and concurrency.** `OrderProcessingService.ProcessPaymentAsync` is the single path for both submit and resubmit:
- It takes a per-order-number lock (`OrderNumberLockRegistry`, a singleton of `SemaphoreSlim`s).
- It re-fetches the order under the lock.
- If the order is already `Paid`, it returns the existing receipt without calling the gateway.
- `Order.MarkPaid` and `Order.MarkFailed` throw if the order is already `Paid`, which is the domain half of the guarantee.

Resubmission reuses the same `OrderNumber`. The lock is process-local, a documented limitation. The key tests are "resubmitting a Paid order never calls `ChargeAsync`" and "two concurrent resubmits call `ChargeAsync` exactly once".

**Other confirmed decisions:**
- Persistence is the repository interface with an in-memory singleton (`ConcurrentDictionary`) implementation.
- `Money` carries a currency code. Only `EUR` is supported, via a static `SupportedCurrencies` allow-list. Currency is a selectable field in the payload and UI.
- Auth is simulated on the frontend only (a `userId` in localStorage). The backend trusts the `UserId` it is sent.
- Application DTOs stay distinct from the Api's wire contracts (`Api/Contracts/V1`), and `OrderContractMapper` converts between them.
- `SubmitOrderRequest` uses data annotations that mirror the frontend validation (required fields, amount > 0, description max 500).

**API.** It is versioned (`Asp.Versioning.*`, routes `api/v1/...`). Endpoints:
- `POST /orders` returns 200 with a receipt, or 422 with an error that always includes `OrderNumber`.
- `POST /orders/{orderNumber}/resubmit`
- `GET /orders?userId=`
- `GET /payment-gateways`
- `GET /currencies`

OpenAPI comes from the built-in `AddOpenApi("v1")` (`/openapi/v1.json`). Swagger UI comes from `Swashbuckle.AspNetCore.SwaggerUi` only, with no SwaggerGen. `Program.cs` needs `public partial class Program;` for `WebApplicationFactory`.

**Single-origin deployment.** The frontend is built as a static export (`output: 'export'`) and copied into the API's `wwwroot/`. Kestrel serves both UI and API on one port, so no reverse proxy is needed. The multi-stage Dockerfile is at `backend/src/OrderProcessing.Api/Dockerfile`. The frontend API client uses relative URLs unless `NEXT_PUBLIC_API_BASE_URL` is set (needed for local dev, where the frontend runs on :3000). CORS allows `http://localhost:3000`.

**Frontend.** Data access goes through an injectable, testable `createOrderApiClient(baseUrl, fetchImpl)`. Plain DTOs are TypeScript interfaces mirroring the backend contracts. Validation is a pure `validateOrderInput` function. React Testing Library is used alongside Jest.
