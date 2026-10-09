# OpenAPI contract

`modular-monolith.v1.json` is the published contract of API version 1. It is generated from the code and committed, so every API change shows up as a reviewable diff.

```powershell
# Regenerate after changing endpoints, request/response DTOs or response metadata
dotnet run --project src/Host/ModularMonolith.Host -- openapi docs/openapi/modular-monolith.v1.json

# CI: fail when the committed contract does not match the code
dotnet run --project src/Host/ModularMonolith.Host -c Release -- openapi --check docs/openapi/modular-monolith.v1.json
```

The command builds the application without starting the server or connecting to any database, Valkey or Keycloak.

## Compatibility rules

- Add fields, endpoints and optional parameters freely.
- Do not remove or rename fields, change types, meaning, validation or status codes in `v1`. Introduce `v2` endpoints instead (FastEndpoints `Version(2)`), export `modular-monolith.v2.json`, and keep `v1` until telemetry shows it is unused.
