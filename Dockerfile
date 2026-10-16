# syntax=docker/dockerfile:1
# Multi-stage build for the API host (also used by the one-shot `migrate` job).
#
# - Restore is a separate layer that depends only on project and lock files, so source changes do not
#   invalidate the NuGet cache. `--locked-mode` fails the build if a package differs from packages.lock.json.
# - The runtime image is Ubuntu "chiseled": no shell, no package manager, no curl, non-root by default.
#   The "-extra" flavour adds ICU and tzdata (culture-aware formatting and time zones).
# - Both base images are pinned to exact versions; Dependabot (.github/dependabot.yml) proposes updates.

FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /src

COPY NuGet.Config global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Shared/ModularMonolith.Shared.Kernel/*.csproj src/Shared/ModularMonolith.Shared.Kernel/packages.lock.json src/Shared/ModularMonolith.Shared.Kernel/
COPY src/Shared/ModularMonolith.Shared.Infrastructure/*.csproj src/Shared/ModularMonolith.Shared.Infrastructure/packages.lock.json src/Shared/ModularMonolith.Shared.Infrastructure/
COPY src/Modules/Catalog/ModularMonolith.Catalog/*.csproj src/Modules/Catalog/ModularMonolith.Catalog/packages.lock.json src/Modules/Catalog/ModularMonolith.Catalog/
COPY src/Modules/Catalog/ModularMonolith.Catalog.Contracts/*.csproj src/Modules/Catalog/ModularMonolith.Catalog.Contracts/packages.lock.json src/Modules/Catalog/ModularMonolith.Catalog.Contracts/
COPY src/Modules/Identity/ModularMonolith.Identity/*.csproj src/Modules/Identity/ModularMonolith.Identity/packages.lock.json src/Modules/Identity/ModularMonolith.Identity/
COPY src/Modules/Identity/ModularMonolith.Identity.Contracts/*.csproj src/Modules/Identity/ModularMonolith.Identity.Contracts/packages.lock.json src/Modules/Identity/ModularMonolith.Identity.Contracts/
COPY src/Modules/Privacy/ModularMonolith.Privacy/*.csproj src/Modules/Privacy/ModularMonolith.Privacy/packages.lock.json src/Modules/Privacy/ModularMonolith.Privacy/
COPY src/Modules/Messaging/ModularMonolith.Messaging/*.csproj src/Modules/Messaging/ModularMonolith.Messaging/packages.lock.json src/Modules/Messaging/ModularMonolith.Messaging/
COPY src/Aspire/ModularMonolith.ServiceDefaults/*.csproj src/Aspire/ModularMonolith.ServiceDefaults/packages.lock.json src/Aspire/ModularMonolith.ServiceDefaults/
COPY src/Host/ModularMonolith.Host/*.csproj src/Host/ModularMonolith.Host/packages.lock.json src/Host/ModularMonolith.Host/
RUN dotnet restore src/Host/ModularMonolith.Host/ModularMonolith.Host.csproj --locked-mode

COPY src ./src
RUN dotnet publish src/Host/ModularMonolith.Host/ModularMonolith.Host.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble-chiseled-extra AS runtime
WORKDIR /app
# Files stay owned by root and read-only for the app user; the application writes nothing to disk
# (Data Protection keys live in PostgreSQL), so any UID works (Kubernetes runAsUser, OpenShift).
COPY --from=build /app/publish .
# Non-root user "app" (UID 1654) provided by the image.
USER $APP_UID
# The image already listens on ASPNETCORE_HTTP_PORTS=8080.
EXPOSE 8080
# No shell in the image: the application probes its own readiness endpoint.
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=6 \
    CMD ["dotnet", "ModularMonolith.Host.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "ModularMonolith.Host.dll"]
