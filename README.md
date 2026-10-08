# ModularMonolith V1

A .NET 10 **modular monolith** template using **vertical slices** inside each module. Each module enforces clean-architecture dependency rules at its own boundary.

**Stack:**
- **Hosting:** ASP.NET Core with FastEndpoints, Aspire for local orchestration
- **Data:** PostgreSQL (Dapper, DbUp, row-level security) and Valkey
- **Messaging:** Hangfire runs a transactional outbox and inbox
- **Identity:** Keycloak over OpenID Connect and JWT
- **Observability:** OpenTelemetry, Serilog, Grafana, Tempo, Loki and Prometheus

Every dependency is free and open source (see [Third-party licenses](#third-party-licenses)).

---

## Quickstart

```powershell
# Build (warnings are errors, analyzers on)
dotnet build ModularMonolith.slnx

# Install as a `dotnet new` template
dotnet new install .
dotnet new modular-monolith -n MyCompany.Platform

# Run everything with Aspire (PostgreSQL, Valkey, Keycloak, LGTM, API)
# You'll be asked for the secret parameters in the dashboard, or set them once:
dotnet user-secrets --project src/Aspire/ModularMonolith.AppHost set "Parameters:postgres-password" "<pick one>"
dotnet user-secrets --project src/Aspire/ModularMonolith.AppHost set "Parameters:keycloak-admin-password" "<pick one>"
dotnet user-secrets --project src/Aspire/ModularMonolith.AppHost set "Parameters:grafana-admin-password" "<pick one>"
dotnet user-secrets --project src/Aspire/ModularMonolith.AppHost set "Parameters:api-db-password" "<pick one>"
dotnet run --project src/Aspire/ModularMonolith.AppHost      # runs "migrations" to completion, then starts "api"

# Or run the host directly against your own PostgreSQL/Valkey/Keycloak (appsettings.Development.json):
dotnet run --project src/Host/ModularMonolith.Host -- migrate   # schema owner: DDL, roles, grants; then exits
dotnet run --project src/Host/ModularMonolith.Host              # least-privileged login: DML only

# Or without Aspire (CI-like)
copy deploy\.env.example deploy\.env   # then edit the passwords
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.override.yml up --build
```

| Endpoint | Aspire | Compose |
|---|---|---|
| API | `http://localhost:5080` | `http://localhost:8081` |
| Swagger UI / OpenAPI contract (Development) | `/swagger`, `/swagger/v1/swagger.json` | same |
| Aspire dashboard | `https://localhost:15888` | n/a |
| Keycloak | `http://localhost:8080` | `http://localhost:8080` |
| Grafana / Prometheus / Loki / Tempo | see the Aspire dashboard | `:3000` / `:9090` / `:3100` / `:3200` |
| Hangfire dashboard (role `admin`) | `/hangfire` | `/hangfire` |

**Demo users** (dev realm only; the password is the username):

| User | Tenant | Roles |
|---|---|---|
| `alice` | `acme` | user, admin, operator |
| `bob` | `acme` | user |
| `carol` | `globex` | user, admin |

`src/Host/ModularMonolith.Host/ModularMonolith.Host.http` contains a ready-made token request and smoke calls.

---

## Architecture

```mermaid
flowchart LR
    Client["HTTP client"] -->|Bearer JWT| Host
    Browser -->|OIDC + cookie| Host
    subgraph Host["ModularMonolith.Host (composition root)"]
        Auth["JWT / OIDC + fallback policy"] --> Tenant["Tenant from token claim"] --> RL["Rate limit per tenant+user"] --> FE["FastEndpoints /api/v{n}"]
    end
    FE --> Catalog & Identity
    subgraph Catalog["Catalog module (internal)"]
        CSlice["Features/* slices"] --> CDomain["Domain"]
        CSlice --> CInfra["Infrastructure (Dapper, Refit)"]
    end
    subgraph Identity["Identity module (internal)"]
        ISlice["Features/* slices"] --> IDomain["Domain"]
        ISlice --> IInfra["Infrastructure"]
    end
    CInfra & IInfra -->|"SET ROLE app; app.tenant_id"| PG[("PostgreSQL<br/>schemas: catalog, identity<br/>RLS per tenant")]
    CSlice & ISlice --> Valkey[("Valkey<br/>cache + idempotency")]
    PG -->|outbox rows| HF["Hangfire OutboxProcessor<br/>SKIP LOCKED, retries, DLQ"]
    HF --> Bus["In-process event bus"] -->|"inbox-protected, tenant-bound scope"| CSlice & ISlice
    Host -.->|OTLP traces| Tempo & AspireDash["Aspire dashboard"]
    Host -.->|Serilog| Loki
    Prom["Prometheus"] -.->|scrape /metrics| Host
    Tempo & Loki & Prom --> Grafana
```

### Dependency rules

| Project | May reference |
|---|---|
| `Shared.Kernel` | nothing (pure C#) |
| `Shared.Infrastructure` | Kernel |
| `<Module>.Contracts` | Kernel only, as public versioned integration events |
| `<Module>` | Kernel, Shared.Infrastructure, its own Contracts, **other modules' Contracts only** |
| `Host` | Shared.Infrastructure, ServiceDefaults, module projects (it only uses `IModule` entry points) |

- Inside a module everything is `internal`. The single public type is the `IModule` entry point (`CatalogModule`, `IdentityModule`), so the compiler enforces module boundaries rather than convention.
- **Contracts are separated from implementation.**
  - Integration events live only in `*.Contracts`.
  - HTTP request and response DTOs are slice-local records.
  - Read models are separate from aggregates.
  - Domain events never leave their module.

### Request flow for a command

```
HTTP -> HTTP platform                            forwarded headers, HTTPS/HSTS, security headers, compression
     -> Authentication -> tenant -> timeout -> rate limit -> authorization
     -> Endpoint (FastEndpoints)                 thin adapter, Result -> RFC 9457 problem details
     -> IdempotencyPreProcessor                  Idempotency-Key per endpoint policy (Valkey, hashed, tenant+user scoped)
     -> LoggingCommandDecorator
     -> TelemetryCommandDecorator                span + duration histogram
     -> ValidationCommandDecorator               FluentValidation -> 400 before any transaction
     -> TransactionalCommandDecorator            BEGIN (re-run on transient DB failures before COMMIT)
          -> CommandHandler                      business logic, returns Result<T>
          -> Repository                          versioned writes (optimistic concurrency), before/after snapshots
          -> DomainEventDispatcher               in-module handlers (e.g. write to outbox + traceparent)
          -> AuditWriter                         audit row (payload + before/after changes) in the same transaction
        COMMIT -> after-commit callbacks         cache region invalidation, in-process outbox signal
```

---

## Folder structure

```
ModularMonolithV1/
├── ModularMonolith.slnx
├── Directory.Build.props            # nullable, warnings-as-errors, analyzers, docs
├── Directory.Packages.props         # central package management + security pins
├── .editorconfig                    # formatting, style, naming, analyzer severities
├── global.json  NuGet.Config  Dockerfile  .dockerignore  LICENSE
├── .template.config/template.json   # `dotnet new modular-monolith`
├── deploy/
│   ├── docker-compose.yml  docker-compose.override.yml  .env.example
│   ├── keycloak/realm-export.json   # realm, clients, tenant_id + roles mappers, demo users
│   └── grafana/
│       ├── tempo.yaml  loki.yaml  prometheus.yaml  prometheus.aspire.yaml
│       └── provisioning/{datasources,dashboards}/...
└── src/
    ├── Aspire/
    │   ├── ModularMonolith.AppHost/            # Program.cs: containers, parameters, wiring
    │   └── ModularMonolith.ServiceDefaults/    # OTel, health endpoints, service discovery
    ├── Host/ModularMonolith.Host/
    │   ├── Program.cs                          # composition root (module list lives here)
    │   ├── Configuration/                      # HttpPlatform, Security, Logging, BackgroundJobs, ApiConventions,
    │   │                                       # SecurityHeadersMiddleware, GracefulShutdown, KeyRing (Data Protection),
    │   │                                       # *Options + *OptionsValidator
    │   ├── Operations/                         # operator endpoints: outbox dead-letter list and replay
    │   ├── Migrations/                         # `migrate` command, Hangfire schema step
    │   └── appsettings*.json  ModularMonolith.Host.http
    ├── Shared/
    │   ├── ModularMonolith.Shared.Kernel/      # pure building blocks
    │   │   ├── Domain/      Entity<TId>, AggregateRoot<TId>, IVersioned, IDomainEvent, IAuditable, ISoftDeletable
    │   │   ├── Results/     Result, Result<T>, Error, ValidationError, ErrorType
    │   │   ├── Paging/      PageRequest (validated), PagedResult<T>
    │   │   ├── Cqrs/        ICommand, IQuery, I*Handler, IDomainEventHandler
    │   │   ├── Messaging/   IIntegrationEvent, IIntegrationEventHandler, IOutbox
    │   │   └── Context/     ITenantContext, ICurrentUser
    │   └── ModularMonolith.Shared.Infrastructure/
    │       ├── Configuration/  AddValidatedOptions (bind + IValidateOptions + ValidateOnStart)
    │       ├── Serialization/  JsonDefaults (the single JSON convention)
    │       ├── Modules/        IModule, ModuleCatalog
    │       ├── Data/           DbSession (IDbSession + IUnitOfWork), AuditChange, DapperTypeHandlers, SqlIdentifier,
    │       │                   Migrations/001_platform.sql (Data Protection keys)
    │       ├── Migrations/     ModuleMigrator (roles, login, DbUp per schema, advisory lock), MigrationOptions,
    │       │                   IDatabaseSetupStep
    │       ├── Cqrs/           decorators, HandlerRegistration, DomainEventDispatcher, AuditWriter
    │       ├── Messaging/      Outbox, OutboxSignal + OutboxDispatcherService, OutboxProcessor (leases),
    │       │                   InProcessEventBus, Inbox decorator, OutboxAdministration (dead letters),
    │       │                   OutboxMetricsCollector, RetentionJob
    │       ├── Tenancy/        TenantContext, TenantResolutionMiddleware, TenancyOptions
    │       ├── Security/       HttpCurrentUser, ClaimNames, AppRoles, PostgresXmlRepository (key ring store)
    │       ├── Caching/        ICacheStore, CacheRegion/CacheKey, CacheKeyFactory, ValkeyCacheStore (single-flight),
    │       │                   ValkeyConnection (async connect)
    │       ├── Idempotency/    FastEndpoints pre/post processors, IdempotencyPolicy, Valkey store
    │       ├── Localization/   JSON IStringLocalizer per module assembly
    │       ├── Health/         PostgreSQL, Valkey checks
    │       ├── Observability/  ActivitySource/Meter, source-generated log messages
    │       └── Web/            Result -> ProblemDetails, ProblemResponses, PagingRequest, OpenAPI helpers
    └── Modules/
        ├── Catalog/
        │   ├── ModularMonolith.Catalog.Contracts/   ProductCreatedIntegrationEvent
        │   └── ModularMonolith.Catalog/
        │       ├── CatalogModule.cs                 # the only public type
        │       ├── CatalogOptions.cs  CatalogFeatures.cs
        │       ├── Domain/          Product, ProductId, Sku, Money, SellerId, domain events, CatalogErrors
        │       ├── Features/
        │       │   ├── CreateProduct/   Endpoint, Command, Handler, Validator, PublishProductCreated
        │       │   ├── GetProduct/      Endpoint, Query (+ read model incl. version), Handler (target of 201 Location)
        │       │   ├── ChangeProductPrice/ Endpoint, Command, Handler, Validator (optimistic concurrency)
        │       │   ├── ListProducts/    Endpoint, Query (+ read model), Handler (paged, cached per page)
        │       │   ├── GetProductPrice/ Endpoint, Query, Handler (currency conversion)
        │       │   └── SyncSeller/      UserRegisteredHandler (consumes Identity's event)
        │       ├── Infrastructure/  ProductRepository, CatalogCache (regions), ExchangeRates/ (Refit + ACL),
        │       │                    Migrations/001_catalog.sql, 002_catalog_v2.sql, 003_catalog_grants.sql
        │       └── Resources/Catalog.en.json
        └── Identity/
            ├── ModularMonolith.Identity.Contracts/  UserRegisteredIntegrationEvent
            └── ModularMonolith.Identity/
                ├── IdentityModule.cs  IdentityOptions.cs  IdentityFeatures.cs
                ├── Domain/          UserProfile, UserProfileId, Email, SubjectId, UserRegisteredDomainEvent, IdentityErrors
                ├── Features/        RegisterUser/, GetCurrentUser/, ListUsers/
                ├── Infrastructure/  UserProfileRepository, Keycloak/ (Refit + health check), Migrations/001-003
                └── Resources/Identity.en.json
```

---

## Example classes (commented)

These are condensed from the real sources so the patterns are visible in one place.

### 1. Aggregate: invariants, typed identity and value objects

```csharp
// src/Modules/Catalog/ModularMonolith.Catalog/Domain/Product.cs
// Strongly typed id (UUIDv7) and value objects: a ProductId can never be passed where a
// UserProfileId is expected, and an invalid Sku or Money cannot exist.
internal readonly record struct ProductId(Guid Value) { public static ProductId New(DateTimeOffset now) => new(Guid.CreateVersion7(now)); }

internal sealed class Product : AggregateRoot<ProductId>       // internal: invisible outside the module
{
    public string Name { get; }
    public Sku Sku { get; }                                      // value object: normalized, validated
    public Money Price { get; private set; }                     // value object: amount > 0, ISO currency
    public SellerId SellerId { get; }                            // value object: identity-provider subject
    // Inherited: Id, Version (optimistic concurrency), Created*/Modified* audit fields, IsDeleted.

    // Factory returns Result<T> instead of throwing: invalid products cannot exist,
    // whichever entry point creates them (HTTP, message handler, test).
    public static Result<Product> Create(string? name, string? sku, decimal amount, string? currency,
                                         SellerId sellerId, DateTimeOffset now)
    {
        // ... name check, Sku.Create(sku), Money.Create(amount, currency) ...
        var product = new Product(ProductId.New(now), trimmedName, validSku.Value, price.Value, sellerId);
        product.Raise(new ProductCreatedDomainEvent(/* dispatched INSIDE the same transaction */));
        return Result.Success(product);
    }

    // Behaviour lives on the aggregate (tell, do not ask).
    public Result ChangePrice(decimal amount, string? currency, DateTimeOffset now) { /* validate, raise event */ }
}
// Note: there is no TenantId on the entity. Tenancy is enforced by PostgreSQL RLS.
```

```csharp
// Infrastructure/ProductRepository.cs: optimistic concurrency + before/after audit
var before = /* select the audited fields */;
var updated = await connection.ExecuteAsync("""
    update catalog.products set price = @Amount, ..., version = version + 1
    where id = @Id and version = @Version
    """, ...);
if (updated == 0) return false;                                  // someone else won: handler returns 409
unitOfWork.RecordChange(new AuditChange("product", id, AuditChange.Update, before, after));
```

### 2. A complete vertical slice: `Features/CreateProduct`

```csharp
// Command: what the slice does. ICommand<T> = runs in a transaction and is audited.
internal sealed record CreateProductCommand(string Name, string Sku, decimal Price, string Currency) : ICommand<Guid>;

// Validator: fast input checks with localized messages; runs before any transaction is opened.
internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator(IStringLocalizer<CatalogModule> localizer)   // Resources/Catalog.en.json
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage(_ => localizer["NameRequired"]);
        RuleFor(c => c.Price).GreaterThan(0).WithMessage(_ => localizer["PriceInvalid"]);
    }
}

// Handler: pure business flow. No HTTP, no SQL transactions, no outbox or audit code (those are decorators).
internal sealed class CreateProductCommandHandler(
    IProductRepository products, ICurrentUser currentUser,
    IFeatureManager features, TimeProvider clock) : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateProductCommand command, CancellationToken ct)
    {
        if (await features.IsEnabledAsync(CatalogFeatures.WriteDisabled, ct))
            return Result.Failure<Guid>(CatalogErrors.WritesDisabled);              // -> 503
        if (await products.SkuExistsAsync(command.Sku, ct))
            return Result.Failure<Guid>(CatalogErrors.SkuTaken);                    // -> 409

        var created = Product.Create(command.Name, command.Sku, command.Price, command.Currency,
                                     currentUser.AuditId, clock.GetUtcNow());
        if (created.IsFailure) return Result.Failure<Guid>(created.Error);          // -> 400

        await products.AddAsync(created.Value, ct);   // joins the unit of work; tracks the aggregate
        return Result.Success(created.Value.Id);
    }
}

// Endpoint: thin HTTP adapter. The handler is injected directly; there is no mediator.
internal sealed class CreateProductEndpoint(ICommandHandler<CreateProductCommand, Guid> handler)
    : Endpoint<CreateProductRequest, CreateProductResponse>
{
    public override void Configure()
    {
        Post("catalog/products");                    // -> POST /api/v1/catalog/products
        Version(1);
        Options(b => b.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        PreProcessor<IdempotencyPreProcessor<CreateProductRequest>>();                         // Idempotency-Key
        PostProcessor<IdempotencyPostProcessor<CreateProductRequest, CreateProductResponse>>();
        Description(b => b                           // OpenAPI: every success and problem response is documented
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .ProducesProblems(400, 403, 409, 422, 503), clearDefaults: true);   // + 401 and 429 automatically
    }

    public override async Task HandleAsync(CreateProductRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateProductCommand(req.Name, req.Sku, req.Price, req.Currency), ct);
        if (result.IsFailure) { await Send.ResultAsync(result.Error.ToProblem()); return; }   // RFC 9457
        HttpContext.Response.Headers.Location = GetProductEndpoint.LocationFor(result.Value);  // /api/v1/catalog/products/{id}
        await Send.ResponseAsync(new CreateProductResponse(result.Value), StatusCodes.Status201Created, ct);
    }
}

// Slice-local DTOs: never shared with other slices or modules.
internal sealed record CreateProductRequest(string Name, string Sku, decimal Price, string Currency);
internal sealed record CreateProductResponse(Guid Id);
```

### 3. Domain event to integration event, through the outbox, in one transaction

```csharp
// Features/CreateProduct/PublishProductCreated.cs
internal sealed class PublishProductCreated(IOutbox outbox, ITenantContext tenant, IUnitOfWork uow, ICacheStore cache)
    : IDomainEventHandler<ProductCreatedDomainEvent>
{
    public async Task HandleAsync(ProductCreatedDomainEvent e, CancellationToken ct)
    {
        // Written to catalog.outbox_messages in the SAME transaction as the product row:
        // the event exists if and only if the product was committed.
        await outbox.AddAsync(new ProductCreatedIntegrationEvent(
            e.EventId, e.OccurredAt, tenant.TenantId, e.ProductId, e.Name, e.Sku, e.Price, e.Currency, e.SellerId), ct);

        // Side effects that must only happen after a successful commit.
        uow.OnCommitted(token => cache.RemoveAsync(ListProductsQueryHandler.CacheKey, token));
    }
}
```

### 4. Cross-module consumer: Catalog reacts to Identity

```csharp
// Features/SyncSeller/UserRegisteredHandler.cs
// Catalog references ONLY ModularMonolith.Identity.Contracts, never Identity internals or tables.
internal sealed class UserRegisteredHandler(IDbSession session) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public async Task HandleAsync(UserRegisteredIntegrationEvent e, CancellationToken ct)
    {
        // The event bus runs this in a fresh scope bound to e.TenantId, in its own transaction,
        // behind an inbox row (message_id, handler). Redelivery is a no-op.
        var connection = await session.GetConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into catalog.sellers (subject_id, display_name, email, registered_at)
            values (@SubjectId, @DisplayName, @Email, @OccurredAt)
            on conflict (tenant_id, subject_id) do update set display_name = excluded.display_name
            """, e, session.Transaction, cancellationToken: ct));
    }
}
```

### 5. Module entry point: the only public type

```csharp
// src/Modules/Catalog/ModularMonolith.Catalog/CatalogModule.cs
public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";
    public string Schema => "catalog";                                   // schema owned by this module only
    public Assembly Assembly => typeof(CatalogModule).Assembly;          // endpoints, handlers, validators, SQL
    public Assembly ContractsAssembly => typeof(ProductCreatedIntegrationEvent).Assembly;

    // Handlers, validators and endpoints are discovered by scanning. Register only module infrastructure here.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CatalogOptions>().Bind(configuration.GetSection("Modules:Catalog")).ValidateOnStart();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddRefitClient<IExchangeRatesApi>() /* base address from options */;
    }
}

// Host: the single place where modules are composed.
IModule[] modules = [new CatalogModule(), new IdentityModule()];
builder.Services.AddSharedInfrastructure(builder.Configuration, modules);
builder.Services.AddFastEndpoints(o => { o.Assemblies = modules.Select(m => m.Assembly); o.DisableAutoDiscovery = true; });
```

### 6. Migration with row-level security

```sql
-- src/Modules/Catalog/ModularMonolith.Catalog/Infrastructure/Migrations/001_catalog.sql
create table catalog.products (
    id        uuid primary key,
    -- Filled from the session variable; '' or NULL (no tenant) can never be inserted.
    tenant_id text not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    name      varchar(200) not null,
    sku       varchar(64)  not null,
    price     numeric(18, 2) not null check (price > 0)
    -- ...
);
create unique index ux_products_tenant_sku on catalog.products (tenant_id, sku) where not is_deleted;

alter table catalog.products enable row level security;
alter table catalog.products force row level security;   -- applies to the table owner too
create policy tenant_isolation on catalog.products
    using      (tenant_id = current_setting('app.tenant_id', true))  -- reads
    with check (tenant_id = current_setting('app.tenant_id', true)); -- writes
```

---

## Multi-tenancy model

1. **Tenant resolution.** The tenant comes from the validated `tenant_id` claim of the access token, set by a Keycloak user-attribute mapper. Users can't edit that attribute.
   - With `Tenancy:RootDomain` set, a subdomain is used only to cross-check the claim. A mismatch returns 403.
   - The `Host` header is never trusted on its own.
2. **Fail closed.**
   - An authenticated request without a tenant claim gets 403.
   - `Tenancy:AllowDefaultTenant` exists for local experiments only.
3. **Database enforcement.** Every application connection runs `SET ROLE modular_monolith_app` and `set_config('app.tenant_id', …)`.
   - The role is a `NOLOGIN`, non-superuser role that owns nothing, so PostgreSQL RLS applies even when the login is a superuser in development.
   - A bug that forgets a `WHERE tenant_id = …` still can't leak data.
4. **Schema per module, not per tenant.** One migration run covers all tenants. There are no schema-name collisions and no per-tenant provisioning.
5. **Caches and idempotency keys** contain the tenant (and the user for idempotency). Only regions explicitly declared `CacheScope.Shared` (e.g. exchange rates) are tenant-independent.
6. **Logs and traces carry the tenant.** Requests and message consumers add a `TenantId` log property and a `tenant.id` span tag. Metrics never get tenant tags, to keep cardinality bounded.

> **Production:** the application login (`Migrations:ApplicationLogin`, default `modular_monolith_api`) is a non-superuser that owns nothing; the migrator grants it membership of `modular_monolith_app`. Provision its password in your secret store and leave `Migrations:ApplicationLoginPassword` empty. See [Database identities and migrations](#database-identities-and-migrations).

## Messaging guarantees

| Concern | Mechanism |
|---|---|
| Event published if and only if the change commits | Transactional outbox in the publisher's schema, written by in-transaction domain-event handlers |
| Low latency without storage churn | After commit an in-process signal wakes `OutboxDispatcherService` (no job is written per commit); a recurring Hangfire sweep (`Outbox:SweepCron`) is the safety net |
| No locks held while consumers run | A short transaction claims a batch (`FOR UPDATE SKIP LOCKED`) and leases it (`status = 'processing'`, `locked_until`); delivery happens outside any outbox transaction; a crashed worker's lease expires and the batch is reclaimed |
| Multiple replicas | Leased claiming is replica-safe; migrations run under `pg_advisory_lock` |
| All tenants drained | The outbox table has no RLS; each message carries `tenant_id`, and consumers run in a scope bound to it |
| Retries | In-process retry (3), then exponential backoff (`next_attempt_at`), then `status = 'dead'` after `Outbox:MaxAttempts` |
| Exactly-once effect per consumer | Inbox `(message_id, handler)` is inserted in the consumer's transaction; handlers that already succeeded are skipped on redelivery |
| Dead letters | Inspect and replay with the operator endpoints `GET /api/v1/ops/outbox/{module}/dead-letters` and `POST .../dead-letters[/{id}]/requeue` |
| One trace end to end | The producer's W3C `traceparent` is stored with the message; consumer spans are its children |
| Retention | Daily Hangfire job per module (`Retention:*`): delivered outbox rows after 7 days, inbox rows after 30 days; dead letters are never purged. The audit log is append-only for the application, so its retention is a DBA task run with owner rights |
| Safe deserialization | Only types exported by registered `*.Contracts` assemblies can be loaded; there is no `dynamic` dispatch |

## Data access and caching

- **Unit of work.** One connection and transaction per command, owned by the pipeline. Transient failures (connection loss, deadlock, serialization failure) before COMMIT re-run the whole unit of work on a fresh connection (`Database:TransientRetries`). A failure during COMMIT is never retried, because its outcome is unknown.
- **Optimistic concurrency.** Every entity has a `version`. Updates are conditional on it and increment it; a stale version returns `409 concurrency_conflict`. `GET /catalog/products/{id}` returns the version and `PUT /catalog/products/{id}/price` expects it back.
- **Audit trail.** Each command writes one `audit_log` row: who, what (payload), from where (IP), the trace id, and the before/after snapshot of every entity it changed (`changes` column). The application role can only `SELECT` and `INSERT` audit rows, never change or delete them.
- **Cache keys.** Keys are built in one place (`CacheKeyFactory`): `mm:{environment}:{t:{tenant}|shared}:{module}:{resource}:s{schemaVersion}[:g{generation}]:{item}`. Regions are declared per module (e.g. `CatalogCache`). Item parts that are not simple identifiers are hashed, so user input never appears in a key.
- **Stampede protection.** On a miss only one caller per key and instance runs the factory; the others wait and reuse the result. TTLs get up to 10% jitter.
- **Invalidation.** Invalidatable regions have a generation counter: `InvalidateAsync(region)` drops every entry (e.g. all product-list pages) at once.
- **Valkey connection.** Opened asynchronously during startup. The cache degrades to the source when Valkey is down.
- **Idempotency.** Configured per endpoint with `IdempotencyPolicy`: `Default` (header optional, fails open), `Critical` (fails closed with 503 if the store is down; used by CreateProduct) or `Required` (header mandatory). Record keys are SHA-256 hashes.
- **Outbound HTTP.** Each dependency has its own resilience pipeline (timeouts, retries with jitter, circuit breaker) sized for it: exchange rates fail fast; the Keycloak readiness probe never retries for long. There is no global pipeline.

## Observability (one pipeline per signal)

- **Traces** go out over OTLP: to the Aspire dashboard under Aspire, and to Tempo under compose. `Observability:TempoEndpoint` adds Tempo alongside the dashboard.
- **Metrics** are OpenTelemetry metrics exposed at `/metrics` for Prometheus. They can also be pushed over OTLP to the Aspire dashboard when `Observability:OtlpMetrics=true`.
- **Custom metrics:**
  - `modmono.handler.duration`
  - `modmono.outbox.processed`
  - `modmono.outbox.failed`
  - `modmono.outbox.dead_lettered`
  - `modmono.outbox.pending`, `modmono.outbox.dead`, `modmono.outbox.oldest_pending_age` (backlog and consumer lag, sampled every `Outbox:MetricsIntervalSeconds`)
- **Logs** go through Serilog to the console and to Loki (`Observability:LokiUrl`). They also go to OTLP when `Observability:OtlpLogs=true`. Trace and span ids are included.
- **Grafana** datasources are provisioned with fixed UIDs and these links: Tempo → Loki, Loki → Tempo, Prometheus exemplars → Tempo. A starter dashboard is included.

## Security

- **API (`/api/*`):** JWT bearer with an audience check (`modular-monolith-api`). It returns 401 and never redirects.
- **Browser (`/hangfire`):** cookie plus OpenID Connect. The `modular-monolith-web` client is public and uses PKCE, so there's no client secret.
- **Defaults:**
  - The fallback policy requires authentication everywhere.
  - `/health/*` and `/metrics` opt out explicitly.
  - Tenant user management requires role `admin`; cross-tenant tools (Hangfire dashboard, dead-letter replay) require role `operator`, which tenant users never get.
- **Rate limiting:** a fixed window per tenant and user, falling back to the client IP.
- **Secrets:**
  - Aspire uses secret parameters.
  - Compose uses `deploy/.env`, which is git-ignored.
  - Only local-development defaults are committed (`appsettings.Development.json`, dev realm demo users).
- **Data Protection keys** (session cookies, antiforgery) are stored in PostgreSQL (`platform.data_protection_keys`), shared by all replicas and kept across restarts. Outside Development they must be encrypted at rest with a certificate (`DataProtection:CertificatePath` / `CertificatePassword`); the host refuses to start without one.

---

## API conventions

| Concern | Convention |
|---|---|
| Routes and versioning | `/api/v{n}/<module>/<resource>` (FastEndpoints versioning, nouns only) |
| JSON | One configuration (`JsonDefaults`) for HTTP, outbox, cache and idempotency: camelCase, enums as camelCase strings, strict numbers, nulls written explicitly |
| Errors | Always RFC 9457 problem details with `traceId`; human-readable `title`, occurrence-specific `detail`, stable `code` extension. This covers `Result` errors, binding and validation errors, 401, 403, 404, 405, 413, 429 and 504 |
| Validation | 400 with `errors: { field: [messages] }`, the same shape for request binding errors and command validation |
| Status codes | 200, 201 (+ `Location` of the new resource), 400, 401, 403, 404, 409, 413, 422 (idempotency key reuse), 429 (+ `Retry-After`), 503 (feature disabled / dependency down), 504 (timeout) |
| Lists | Always paged: `?page=1&pageSize=20` (max 100), response `{ items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage }`, deterministic ordering |
| Limits | Request timeout `Http:RequestTimeoutSeconds` (504), body size `Http:MaxRequestBodyBytes` (413), rate limit per tenant + user (429) |
| Idempotency | Optional `Idempotency-Key` header on POST |
| OpenAPI | Every endpoint documents its success and problem responses; the contract is served in Development at `/swagger/v1/swagger.json` (Swagger UI is never exposed outside Development) |
| Transport | Outside Development: HTTPS redirection (308) and HSTS, except `/health/*` and `/metrics`; `X-Forwarded-*` honoured only from `ReverseProxy:KnownProxies` / `KnownNetworks`; secure-only cookies |
| Headers | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`, and a CSP (locked down for JSON, relaxed only for the Hangfire and Swagger UIs); no `Server` header; Brotli/gzip compression |

## Configuration

Every section is a strongly typed options class with an `IValidateOptions<T>` validator and `ValidateOnStart()`, so the host refuses to start on missing or invalid settings and lists every problem.

| Section | Validated rules |
|---|---|
| `ConnectionStrings:postgres`, `ConnectionStrings:cache` | Must be present (names configurable in `Database` / `Cache`) |
| `Database` | Connection string name set; `ApplicationRole` is a lowercase SQL identifier; `TransientRetries` 0-5 |
| `Authentication` | `Authority` is an absolute URL, https unless `RequireHttpsMetadata=false` (development only); audience and web client id set |
| `Tenancy` | Claim type set; valid default tenant when the fallback is enabled; `RootDomain` is a host name |
| `Outbox` | Batch size, batches per run, attempts, delays, lease and metrics interval within range; cron has 5 or 6 fields |
| `Retention` | Days are 0 (keep) or positive; inbox kept at least 7 days; batch size and cron valid |
| `Localization` | At least one known culture |
| `RateLimiting` | Permit limit and window within range |
| `Http` | Timeout, body size, HTTPS port, HSTS max-age, shutdown drain and shutdown timeout within range |
| `Migrations` (migrate command only) | Owner connection string present; application role and login are SQL identifiers; a login password requires a login |
| `DataProtection` | Application name set; a certificate file is required when `RequireEncryptionAtRest` (default outside Development) |
| `ReverseProxy` | Proxies are IP addresses, networks are CIDR ranges, forward limit 1–10 |
| `Modules:Catalog`, `Modules:Identity` | Cache TTLs within range; URLs are absolute http(s) |

Behind a TLS-terminating load balancer or ingress, set `ReverseProxy__KnownNetworks__0=<ingress CIDR>` so the client IP (rate limiting, audit) and the https scheme are taken from `X-Forwarded-*`.

## Database identities and migrations

| Identity | Used by | Privileges |
|---|---|---|
| Schema owner (`ConnectionStrings:postgres-migrations`) | Only the `migrate` command | DDL; owns every schema; creates roles |
| `modular_monolith_app` (NOLOGIN role) | Holder of the application's privileges | Exactly what each table needs, granted in the migrations (`$app_role$`) |
| `modular_monolith_api` (login, `ConnectionStrings:postgres`) | The serving application | Member of `modular_monolith_app`; owns nothing; every session also runs `SET ROLE modular_monolith_app`, so row-level security always applies |

Privileges per table (see `003_*_grants.sql` and `001_platform.sql`):

| Table | Application privileges |
|---|---|
| Business tables (`products`, `sellers`, `user_profiles`) | `SELECT, INSERT, UPDATE` (soft delete; no `DELETE`) |
| `audit_log` | `SELECT, INSERT` (append-only) |
| `outbox_messages` | `SELECT, INSERT, UPDATE, DELETE` (dispatch and retention) |
| `inbox_messages` | `SELECT, INSERT, DELETE` (retention) |
| `platform.data_protection_keys` | `SELECT, INSERT` |
| `hangfire.*` | DML only; the schema is installed by the migrator (`PrepareSchemaIfNecessary = false`) |
| `*.schema_versions` (migration journals) | none |

**Migrations run as a separate step**, never inside the serving process: `dotnet ModularMonolith.Host.dll migrate` applies the platform and module scripts (DbUp, advisory-locked), installs the Hangfire schema, ensures the role and login, and exits with 0 or 1.

| Environment | How migrations run |
|---|---|
| Aspire | `migrations` resource (same project, `migrate` argument); the API waits for its completion |
| Compose | one-shot `migrations` service; the API depends on `service_completed_successfully` |
| Production | the same image as a pre-deployment job (CI/CD step or Kubernetes `Job`), with the owner connection string from the secret store |

Keep migrations backward compatible (expand, then contract in a later release), because the previous version keeps running while the new one rolls out.

## Graceful shutdown and container settings

On SIGTERM the host:

1. Turns `/health/ready` unhealthy immediately, so load balancers stop sending traffic.
2. Keeps serving for `Http:ShutdownDrainSeconds` (default 5).
3. Stops accepting connections and gives in-flight requests and background work up to `Http:ShutdownTimeoutSeconds` (default 25) to finish.

The orchestrator's grace period must exceed drain + timeout: compose sets `stop_grace_period: 45s` (use `terminationGracePeriodSeconds` in Kubernetes).

Compose also defines CPU and memory limits for every service and a readiness healthcheck for the API (`/health/ready`, probed with bash because the runtime image has no curl).

---

## How to add …

### … a slice to an existing module

1. Create `Features/<UseCase>/`.
2. Add a `…Command : ICommand<T>` or `…Query : IQuery<T>` (use the module's typed ids, e.g. `ProductId`, inside the module and `Guid` only at the HTTP boundary), a handler, an optional `AbstractValidator<…>`, and a FastEndpoints endpoint with slice-local request and response records. List queries take a `PageRequest` and return `PagedResult<T>`; endpoints document their responses with `Description(... .ProducesProblems(...), clearDefaults: true)`.
3. That's all. Handlers and validators are picked up by assembly scanning and wrapped by the decorator chain automatically.

### … a module

1. Create `src/Modules/<Name>/ModularMonolith.<Name>` and `ModularMonolith.<Name>.Contracts`. Copy a csproj; reference Kernel, Shared.Infrastructure and only other modules' `*.Contracts`.
2. Add a public `<Name>Module : IModule` with a unique `Schema`.
3. Add `Infrastructure/Migrations/001_<name>.sql` with the tables. Include `tenant_id`, `version` and the RLS policy, plus `outbox_messages`, `inbox_messages` and `audit_log` with the columns of Catalog's `001` and `002` scripts, and explicit grants to `$app_role$` like Catalog's `003` script (no schema-wide grants). Never edit an applied migration; add the next numbered script instead.
4. Add `new <Name>Module()` to the module list in `Host/Program.cs` and a project reference in the Host csproj.

---

## Build and code-quality settings

| Setting | Where |
|---|---|
| `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`, deterministic and CI builds | `Directory.Build.props` |
| `TargetFramework` and `LangVersion` pinned explicitly | every `.csproj` |
| Central package versions, transitive security pins (`Newtonsoft.Json`, `MessagePack`) | `Directory.Packages.props` |
| Meziantou.Analyzer for every project | `GlobalPackageReference` |
| File-scoped namespaces (error), formatting (IDE0055), unused usings (IDE0005), braces, naming | `.editorconfig` |
| Justified analyzer overrides (DI-created internals, no `ConfigureAwait` in ASP.NET Core, …) | bottom of `.editorconfig` |

## Third-party licenses

All free for commercial use. There are no paid tiers and no commercial-licensed packages (no MediatR v13+, AutoMapper v15+, MassTransit v9+, FluentAssertions v8+, Duende, Hangfire Pro).

| Component | License |
|---|---|
| .NET, ASP.NET Core, Aspire, Microsoft.FeatureManagement, Microsoft.Extensions.* | MIT |
| OpenTelemetry .NET | Apache-2.0 |
| FastEndpoints (+ Swagger, NSwag) | MIT |
| Dapper | Apache-2.0 |
| Npgsql | PostgreSQL License |
| DbUp | MIT |
| FluentValidation | Apache-2.0 |
| Refit | MIT |
| Serilog (+ sinks) | Apache-2.0 |
| StackExchange.Redis | MIT |
| Hangfire.Core, Hangfire.AspNetCore, Hangfire.PostgreSql | LGPL-3.0 (free) |
| Meziantou.Analyzer | MIT |
| PostgreSQL | PostgreSQL License |
| Valkey (instead of Redis, whose 7.4+ licenses are not OSI open source) | BSD-3-Clause |
| Keycloak | Apache-2.0 |
| Prometheus | Apache-2.0 |
| Grafana, Loki, Tempo (self-hosted OSS) | AGPL-3.0 |
