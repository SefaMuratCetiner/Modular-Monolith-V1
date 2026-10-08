# syntax=docker/dockerfile:1
# Multi-stage build for the API host. The restore layer only depends on project files,
# so source changes do not invalidate the NuGet cache.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Shared/ModularMonolith.Shared.Kernel/ModularMonolith.Shared.Kernel.csproj src/Shared/ModularMonolith.Shared.Kernel/
COPY src/Shared/ModularMonolith.Shared.Infrastructure/ModularMonolith.Shared.Infrastructure.csproj src/Shared/ModularMonolith.Shared.Infrastructure/
COPY src/Modules/Catalog/ModularMonolith.Catalog/ModularMonolith.Catalog.csproj src/Modules/Catalog/ModularMonolith.Catalog/
COPY src/Modules/Catalog/ModularMonolith.Catalog.Contracts/ModularMonolith.Catalog.Contracts.csproj src/Modules/Catalog/ModularMonolith.Catalog.Contracts/
COPY src/Modules/Identity/ModularMonolith.Identity/ModularMonolith.Identity.csproj src/Modules/Identity/ModularMonolith.Identity/
COPY src/Modules/Identity/ModularMonolith.Identity.Contracts/ModularMonolith.Identity.Contracts.csproj src/Modules/Identity/ModularMonolith.Identity.Contracts/
COPY src/Aspire/ModularMonolith.ServiceDefaults/ModularMonolith.ServiceDefaults.csproj src/Aspire/ModularMonolith.ServiceDefaults/
COPY src/Host/ModularMonolith.Host/ModularMonolith.Host.csproj src/Host/ModularMonolith.Host/
RUN dotnet restore src/Host/ModularMonolith.Host/ModularMonolith.Host.csproj

COPY src ./src
RUN dotnet publish src/Host/ModularMonolith.Host/ModularMonolith.Host.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Non-root. Group 0 ownership keeps the image runnable under arbitrary UIDs (Kubernetes/OpenShift style).
RUN chgrp -R 0 /app && chmod -R g=u /app
USER 1001
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
ENTRYPOINT ["dotnet", "ModularMonolith.Host.dll"]
