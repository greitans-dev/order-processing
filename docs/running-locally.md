# Running locally

Commands are shown for a POSIX shell and for PowerShell. They differ only where noted.

## Backend

```sh
cd backend/src/OrderProcessing.Api
dotnet run -lp dev
```

Serves plain HTTP on `http://localhost:5080` (launch profile `dev`). Swagger UI: `http://localhost:5080/swagger`.

## Frontend

PowerShell:

```powershell
cd frontend
pnpm install
$env:NEXT_PUBLIC_API_BASE_URL = "http://localhost:5080"
pnpm dev
```

The variable stays set for the rest of that PowerShell session. Remove it with
`Remove-Item Env:NEXT_PUBLIC_API_BASE_URL` before building for a same-origin run.

Linux shell:

```sh
cd frontend
pnpm install
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 pnpm dev
```

The UI runs on `http://localhost:3000`. `NEXT_PUBLIC_API_BASE_URL` points the API client at the backend. When it is
unset the client uses relative URLs, which is what the Docker build relies on. Because the dev server and API are on
different ports, the backend allows CORS from `http://localhost:3000`.

## Tests

```sh
dotnet test backend/OrderProcessing.sln
cd frontend
pnpm test
```

Run a single backend test with `dotnet test backend/OrderProcessing.sln --filter "FullyQualifiedName~<TestName>"`.
