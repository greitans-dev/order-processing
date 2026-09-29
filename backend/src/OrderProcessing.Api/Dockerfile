# Build context is the repository root (see docker-compose.yml).

# 1. Frontend: static export to /app/frontend/out
FROM node:22-alpine AS frontend-build
RUN npm install -g pnpm@11.9.0
WORKDIR /app/frontend
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ ./
# NEXT_PUBLIC_API_BASE_URL is intentionally unset: the bundle calls the API via relative URLs (same origin).
RUN pnpm build

# 2. Backend: publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app/backend
COPY backend/ ./
RUN dotnet publish src/OrderProcessing.Api -c Release -o /app/publish

# 3. Final image: API + static frontend in wwwroot, one process, one port
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/frontend/out ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
# One JSON object per log line in containers; override with Logging__Console__FormatterName=simple
ENV Logging__Console__FormatterName=json
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderProcessing.Api.dll"]
