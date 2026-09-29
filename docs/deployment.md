# Deployment

`docker compose up --build` builds and runs everything in one container, exposing one port (`8080`) and binds to address `127.0.0.1`.

## Design

The frontend uses client components only, so it is built as a static export (`output: 'export'`) and copied into the
API's `wwwroot/`. Kestrel serves both the UI and the API, so there is one process, one image, and one port. The
frontend and API are always same-origin, so no reverse proxy is needed. This was chosen over a multi-container setup
with a reverse proxy for simplicity.

## Dockerfile

`backend/src/OrderProcessing.Api/Dockerfile` (build context: repo root) has three stages:

1. `frontend-build` (Node): `pnpm install --frozen-lockfile && pnpm build`, producing `frontend/out`.
2. `backend-build` (.NET SDK): `dotnet publish` of the API.
3. `final` (ASP.NET runtime): the publish output plus the static export in `wwwroot/`, listening on `http://+:8080`.

`NEXT_PUBLIC_API_BASE_URL` is left unset in the image so the bundle calls the API with relative URLs.

## Logs

The container writes one JSON object per log line to stdout (`Logging__Console__FormatterName=json`), including the
trace id of the request. Read them with `docker compose logs`. Set `Logging__Console__FormatterName=simple` to get
readable text, and `Logging__LogLevel__OrderProcessing=Debug` to also see each gateway charge start.

## URLs

- UI: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger`
- API: `http://localhost:8080/api/v1/...`
