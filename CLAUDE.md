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
- CI: `.github/workflows/sonarqube.yml` runs the tests with coverage and sends the analysis to SonarQube on pushes to `main` (details in `docs/ci.md`). Scanner settings are `/d:` arguments in the workflow, not a `sonar-project.properties`.
- Docs go in `docs/`. The root `README.md` is brief and links to them.

## SonarQube analysis results

The CI analysis results (quality gate, coverage, issues) can be read through the SonarQube REST API. The host URL, project key and token are in the git-ignored `.env.agent-secrets` in the repo root, as `SONARQUBE_HOST_URL`, `SONARQUBE_PROJECT_KEY` and `SONARQUBE_TOKEN`. Read them from that file, never copy the values into tracked files, commits or logs. Authenticate with the token as the basic-auth user and an empty password:

```sh
set -a; . ./.env.agent-secrets; set +a
curl -s -u "$SONARQUBE_TOKEN:" "$SONARQUBE_HOST_URL/api/qualitygates/project_status?projectKey=$SONARQUBE_PROJECT_KEY"
curl -s -u "$SONARQUBE_TOKEN:" "$SONARQUBE_HOST_URL/api/measures/component?component=$SONARQUBE_PROJECT_KEY&metricKeys=coverage,bugs,vulnerabilities,code_smells,duplicated_lines_density"
curl -s -u "$SONARQUBE_TOKEN:" "$SONARQUBE_HOST_URL/api/issues/search?componentKeys=$SONARQUBE_PROJECT_KEY&resolved=false"
```

The three calls above are verified to work with the current token. `api/measures/component_tree` (per-file coverage) and `api/qualitygates/get_by_project` answer "Insufficient privileges", so the token only has browse-level access. Use `api/issues/search` (add `&types=VULNERABILITY`, `&ps=100`) for per-file findings.

The analysis is produced by `.github/workflows/sonarqube.yml` on pushes to `main`, so results reflect the last pushed commit, not local changes.

## Architecture (big picture)

Clean Architecture / DDD with strict inward dependencies: `Domain → nothing`, `Application → Domain`, `Infrastructure → Application + Domain`, `Api → all` (the Infrastructure reference is for the composition root only). The architecture-test project enforces this. Controllers must not reference Infrastructure, `IPaymentGateway` implementations live only in `Infrastructure.Payments`, and `IOrderRepository` implementations only in `Infrastructure.Persistence`.

**Payment gateways are pluggable.** `IPaymentGateway` (with `GatewayId`) is implemented in Infrastructure. `IPaymentGatewayRegistry` builds a dictionary from `IEnumerable<IPaymentGateway>` injected by DI. The order payload's `PaymentGatewayId` string picks the gateway at runtime. Adding or removing a gateway means one class plus one DI registration line, with no other layer touched. There are two mocks, `mock-alpha` and `mock-beta`. Both decline deterministically when `amount >= 10000.00`, so `9999.99` succeeds and `10000.00` fails.

**Idempotency and concurrency.** `OrderProcessingService.ProcessPaymentAsync` is the single path for both submit and resubmit:
- It takes a per-order-number lock (`OrderNumberLockRegistry`, a singleton of `SemaphoreSlim`s).
- It re-fetches the order under the lock.
- If the order is already `Paid`, it returns the existing receipt without calling the gateway.
- `Order.MarkPaid` and `Order.MarkFailed` throw if the order is already `Paid`, which is the domain half of the guarantee.

`POST /orders` requires an `Idempotency-Key` header (client-generated, max 255 chars, scoped per `UserId`). It is a value object (`IdempotencyKey`) stored on the `Order`, and it is deliberately separate from the server-generated `OrderNumber`, which is unknown to the client until the response arrives. `SubmitNewOrderAsync` takes a lock on `(UserId, key)` and looks the order up by key. The same key with the same payload never creates a second order: it replays the outcome (`Paid` -> receipt, `Failed` -> the original 422 with the same `OrderNumber`, so a failed payment is retried through resubmit). The same key with a different payload throws `IdempotencyKeyReuseException`, which the API maps to 409. Resubmission reuses the same `OrderNumber`. The lock is process-local, a documented limitation. The key tests are "resubmitting a Paid order never calls `ChargeAsync`" and "two concurrent resubmits call `ChargeAsync` exactly once", plus the same-key equivalents: "a repeated submit with the same key charges once" and "concurrent submits with the same key charge once".

**Logging.** Application services log through `ILogger<T>` with `[LoggerMessage]` source-generated methods. Never log an order description or full payloads. Gateway calls are logged centrally in `OrderProcessingService`, not in the gateway implementations. `UseExceptionHandler` + `AddProblemDetails` turn unhandled exceptions into logged `problem+json` 500s. The Docker image logs JSON (`Logging__Console__FormatterName=json`); keep `IncludeScopes` set under both `Logging:Console` and `Logging:Console:FormatterOptions` in `appsettings.json`, because a named formatter ignores the former (that is what dropped the trace id from the JSON logs).

**Other confirmed decisions:**
- Persistence is the repository interface with an in-memory singleton (`ConcurrentDictionary`) implementation. Durable persistence is intentionally out of scope (see `docs/architecture.md`, "Scope and non-goals"), so do not add a database unprompted.
- `Money` carries a currency code. Only `EUR` is supported, via a static `SupportedCurrencies` allow-list. Currency is a selectable field in the payload and UI.
- Auth is simulated on the frontend only (a `userId` in localStorage). The backend trusts the `UserId` it is sent. Real authentication is intentionally out of scope (same section), so do not add it unprompted.
- Application DTOs stay distinct from the Api's wire contracts (`Api/Contracts/V1`), and `OrderContractMapper` converts between them.
- `SubmitOrderRequest` uses data annotations that mirror the frontend validation (required fields, amount > 0, description max 500).

**API.** It is versioned (`Asp.Versioning.*`, routes `api/v1/...`). Endpoints:
- `POST /orders` requires the `Idempotency-Key` header (400 if missing or invalid). It returns 200 with a receipt, 422 with an error that always includes `OrderNumber`, or 409 if the key was already used with a different payload.
- `POST /orders/{orderNumber}/resubmit`
- `GET /orders?userId=` returns the user's orders newest first (by `CreatedAtUtc`).
- `GET /payment-gateways`
- `GET /currencies`

OpenAPI comes from the built-in `AddOpenApi("v1")` (`/openapi/v1.json`). Descriptions come from XML doc comments (`GenerateDocumentationFile` in the Api project), so every endpoint and contract needs `<summary>`, `<param>` and `<response>` comments; the `Idempotency-Key` header is explained in the remarks of `OrdersController.Submit` and completed by `IdempotencyKeyOperationTransformer`, because the generator ignores `<param>` on header parameters. Swagger UI comes from `Swashbuckle.AspNetCore.SwaggerUi` only, with no SwaggerGen. `Program.cs` needs `public partial class Program;` for `WebApplicationFactory`.

**Single-origin deployment.** The frontend is built as a static export (`output: 'export'`) and copied into the API's `wwwroot/`. Kestrel serves both UI and API on one port, so no reverse proxy is needed. The multi-stage Dockerfile is at `backend/src/OrderProcessing.Api/Dockerfile`. The frontend API client uses relative URLs unless `NEXT_PUBLIC_API_BASE_URL` is set (needed for local dev, where the frontend runs on :3000). CORS allows `http://localhost:3000`.

**Frontend.** Data access goes through an injectable, testable `createOrderApiClient(baseUrl, fetchImpl)`. Plain DTOs are TypeScript interfaces mirroring the backend contracts. Validation is a pure `validateOrderInput` function. React Testing Library is used alongside Jest.
