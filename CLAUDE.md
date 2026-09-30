# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

This file is a living document maintained by both the human and you, the AI agent. Update this document
whenever there are significant changes in the project - architecture shift, refactoring, upgrades e.t.c.

It is loaded in every session, so keep it short: working rules and pointers only. Detail belongs in `docs/`, and this
file links to it and says when to read it. If a fact is already in `docs/`, link to it instead of repeating it here.

## Current state

User-facing docs live in `docs/`, linked from `README.md`.

Notes:

- The frontend was scaffolded by `create-next-app`, which added `frontend/AGENTS.md` and `frontend/CLAUDE.md`. Next.js 16 docs ship in `frontend/node_modules/next/dist/docs/`.

## Committing the changes

Never add `Co-Authored-By:` trailer or other attributions to the commit messages.

Keep the commit message short. Briefly describe *what* was changed and *why*, if it is not obvious.

When working on multi-step or multi-phase changes, try to commit the changes in each step separately, making sure that the code is always in a deployable state.

## What is being built

An order-submission web app for "XYZ Inc.": a Next.js frontend (TypeScript, client components only, Jest) and an ASP.NET Core backend (.NET 10, C#, xUnit + Moq + Shouldly + NetArchTest) that forwards orders to one of several payment gateways and returns a receipt or a user-displayable error. It must work out of the box with mocked gateways, with no API keys.

## Where to look

Read the matching doc before working in that area:

| Doc | Covers | Read before |
|---|---|---|
| [`docs/architecture.md`](docs/architecture.md) | Layers and dependency rules, idempotency and concurrency design, logging, scope and non-goals, known limitations | changing domain or application code, locking, or logging |
| [`docs/api.md`](docs/api.md) | Endpoints, status codes, the `Idempotency-Key` contract | changing controllers, contracts or OpenAPI |
| [`docs/payment-gateways.md`](docs/payment-gateways.md) | Mock gateway rules, adding or removing a gateway, currencies | touching gateways or currencies |
| [`docs/running-locally.md`](docs/running-locally.md) | Running the backend and frontend (POSIX and PowerShell), `NEXT_PUBLIC_API_BASE_URL`, CORS | running the app |
| [`docs/deployment.md`](docs/deployment.md) | Single-container design, Dockerfile stages, JSON logs, URLs | touching Docker, compose, or logging config |
| [`docs/ci.md`](docs/ci.md) | The SonarQube workflow, its secrets and variable | touching `.github/` |

## Layout and commands

- `backend/` holds `OrderProcessing.sln` and `src/{Domain,Application,Infrastructure,Api}`, with matching `tests/*.Tests` projects. Architecture tests live in their own project, `OrderProcessing.ArchitectureTests`, as the spec requires. Tests mirror the namespace of the class under test with `.Tests` added to the assembly part, so `OrderProcessing.Application.Orders.Locking.OrderLockRegistry` is tested in `OrderProcessing.Application.Tests.Orders.Locking` (same folder structure, one `<Class>Tests` per class).
- `frontend/` holds the Next.js app under `src/{app,components,context,services,types,validation}`.
- Backend tests: `dotnet test backend/OrderProcessing.sln`. Single test: `dotnet test backend/OrderProcessing.sln --filter "FullyQualifiedName~<TestName>"`.
- Frontend tests: `cd frontend && pnpm test` (Jest).
- Docker: `docker compose up --build` exposes only port 8080.
- Docs go in `docs/`. The root `README.md` is brief and links to them.

## Guardrails

- **Architecture:** dependencies point inward only (`Domain` -> nothing, `Application` -> `Domain`, `Infrastructure` -> both, `Api` -> all, Infrastructure only for the composition root). The architecture tests enforce it. Controllers never reference Infrastructure, `IPaymentGateway` implementations live only in `Infrastructure.Payments`, and `IOrderRepository` implementations only in `Infrastructure.Persistence`. See [architecture](docs/architecture.md).
- **Idempotency:** never break "resubmitting a Paid order never calls `ChargeAsync`" and "concurrent resubmits, and concurrent submits with the same key, charge exactly once". `POST /orders` needs the `Idempotency-Key`, which is deliberately separate from the server-generated `OrderNumber`. See [architecture](docs/architecture.md#idempotency-and-concurrency) and [API](docs/api.md#idempotency-key).
- **Logging:** log through `ILogger<T>` with `[LoggerMessage]` methods, never log an order description or full payloads, and keep `IncludeScopes` set under both `Logging:Console` and `Logging:Console:FormatterOptions` in `appsettings.json` (a named formatter ignores the former). See [architecture](docs/architecture.md#logging).
- **Out of scope, do not add unprompted:** real authentication and durable persistence. See [architecture](docs/architecture.md#scope-and-non-goals).
- **API:** every endpoint and contract needs XML `<summary>`, `<param>` and `<response>` comments, because they feed the OpenAPI document. The exception is the `Idempotency-Key` header: the generator ignores `<param>` on header parameters, so its description is completed by `IdempotencyKeyOperationTransformer`. Swagger UI comes from `Swashbuckle.AspNetCore.SwaggerUi` only, with no SwaggerGen. `Program.cs` keeps `public partial class Program;` for `WebApplicationFactory`. Application DTOs stay separate from the wire contracts in `Api/Contracts/V1` (`OrderContractMapper` converts between them), and `SubmitOrderRequest` data annotations mirror the frontend validation. See [API](docs/api.md).

## Frontend

Data access goes through an injectable, testable `createOrderApiClient(baseUrl, fetchImpl)`. Plain DTOs are TypeScript interfaces mirroring the backend contracts. Validation is a pure `validateOrderInput` function. React Testing Library is used alongside Jest.

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
