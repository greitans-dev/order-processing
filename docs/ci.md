# CI

`.github/workflows/sonarqube.yml` analyzes code quality and publishes test coverage for the backend and the frontend to
a self-hosted SonarQube server. It runs on every push to `main` and can be started by hand from the Actions tab
(`workflow_dispatch`). Pull requests are not analyzed.

## What it does

One job, one analysis, one SonarQube project:

1. Fails immediately with a clear message if a required secret is missing.
2. Sets up Java (needed by the scanner), .NET 10, Node 22 and pnpm, and installs `dotnet-sonarscanner`.
3. `dotnet sonarscanner begin`, then builds and tests the backend with coverlet coverage (OpenCover format), then runs
   the frontend Jest tests with coverage (lcov).
4. `dotnet sonarscanner end` uploads the analysis of the C# and TypeScript sources and both coverage reports, and waits
   for the quality gate. The workflow fails when the quality gate fails.

All scanner settings are passed as `/d:` arguments in the workflow. There is no `sonar-project.properties`.

## Configuration

Repository settings (Settings, Secrets and variables, Actions):

| Kind | Name | Value |
|---|---|---|
| Secret | `SONAR_TOKEN` | An analysis token for the project (SonarQube: My Account, Security) |
| Secret | `SONAR_HOST_URL` | Base URL of the server, for example `https://sonarqube.example.com` |
| Variable (optional) | `SONAR_PROJECT_KEY` | Project key in SonarQube. Defaults to `order-processing` |

Create the project in SonarQube first (manually, with the same key) so the token can analyze it.

## Reproducing the coverage locally

```sh
dotnet test backend/OrderProcessing.sln --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
cd frontend && pnpm test --coverage
```

The backend reports appear as `backend/tests/*/TestResults/*/coverage.opencover.xml` and the frontend report as
`frontend/coverage/lcov.info`. Plain `pnpm test` does not collect coverage.
