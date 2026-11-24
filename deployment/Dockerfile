# syntax=docker/dockerfile:1
# Local Invoice Manager — single container (API + React UI).
# Build context: repository root.  docker compose -f deployment/docker-compose.yml up -d --build

# 1) Frontend
FROM --platform=$BUILDPLATFORM node:24-alpine AS web
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

# 2) Backend
# Portable (framework-dependent) publish: runs on amd64 and arm64, so it is built once on the build machine.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
COPY backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/ ./src/
RUN dotnet restore src/LocalInvoiceManager.Web/LocalInvoiceManager.Web.csproj --locked-mode
RUN dotnet publish src/LocalInvoiceManager.Web/LocalInvoiceManager.Web.csproj -c Release -o /app --nologo -p:UseAppHost=false

# 3) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG APP_UID=1000
LABEL org.opencontainers.image.source="https://github.com/mgmehdiforme/local-invoice-manager" \
      org.opencontainers.image.description="Local Invoice Manager: invoices, quotations and payments that stay on your computer" \
      org.opencontainers.image.licenses="MIT"
WORKDIR /app
COPY --from=api --chmod=755 /app ./
COPY --from=web --chmod=755 /src/frontend/dist ./wwwroot
RUN mkdir -p /data /backups && chown -R ${APP_UID}:${APP_UID} /data /backups
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Storage__DataPath=/data \
    Storage__BackupPath=/backups \
    TZ=UTC \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
USER ${APP_UID}
EXPOSE 8080
HEALTHCHECK --interval=60s --timeout=10s --start-period=30s --retries=3 \
  CMD ["dotnet", "LocalInvoiceManager.Web.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "LocalInvoiceManager.Web.dll"]
