// Aspire orchestration for local development.
// Secrets are Aspire parameters: set them with `dotnet user-secrets` (Parameters:<name>) or enter
// them in the dashboard when prompted. No credentials are hard-coded here.
// Every container image is pinned to an explicit version.

var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Data
// ---------------------------------------------------------------------------------------------
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres-server", password: postgresPassword)
    .WithDataVolume()
    .WithPgAdmin();
var database = postgres.AddDatabase("postgres", databaseName: "modular_monolith");

// Two database identities:
// - the schema owner (the server's superuser here) is given ONLY to the migrations resource;
// - the API connects as "modular_monolith_api", a login that owns nothing and only has the DML
//   privileges granted to the "modular_monolith_app" role. The migrator creates it with this password.
var apiDbPassword = builder.AddParameter("api-db-password", secret: true);
var postgresEndpoint = postgres.Resource.PrimaryEndpoint;
var apiConnectionString = ReferenceExpression.Create(
    $"Host={postgresEndpoint.Property(EndpointProperty.Host)};Port={postgresEndpoint.Property(EndpointProperty.Port)};Database=modular_monolith;Username=modular_monolith_api;Password={apiDbPassword}");

// Valkey: BSD-licensed, Redis-protocol compatible cache (Redis 7.4+ is no longer OSI open source).
var cache = builder.AddValkey("cache")
    .WithDataVolume();

// ---------------------------------------------------------------------------------------------
// Identity provider
// ---------------------------------------------------------------------------------------------
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

// Secret of the "modular-monolith-admin" service account (erasure of Keycloak accounts). Keycloak fills it
// into the realm import; the API uses it for the admin REST API.
var accountErasureSecret = builder.AddParameter("keycloak-admin-client-secret", secret: true);

// Fixed port 8080: the token issuer URL must be identical for browsers, CLIs and the API.
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.3.3")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithEnvironment("KEYCLOAK_ADMIN_CLIENT_SECRET", accountErasureSecret)
    .WithArgs("start-dev", "--import-realm")
    .WithBindMount("../../../deploy/keycloak", "/opt/keycloak/data/import", isReadOnly: true)
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http");
var keycloakHttp = keycloak.GetEndpoint("http");

// ---------------------------------------------------------------------------------------------
// Observability (Grafana LGTM). The Aspire dashboard also receives traces, metrics and logs.
// ---------------------------------------------------------------------------------------------
var tempo = builder.AddContainer("tempo", "grafana/tempo", "2.8.2")
    .WithBindMount("../../../deploy/grafana/tempo.yaml", "/etc/tempo.yaml", isReadOnly: true)
    .WithArgs("-config.file=/etc/tempo.yaml")
    .WithHttpEndpoint(targetPort: 3200, name: "http");

var loki = builder.AddContainer("loki", "grafana/loki", "3.5.3")
    .WithBindMount("../../../deploy/grafana/loki.yaml", "/etc/loki/local-config.yaml", isReadOnly: true)
    .WithArgs("-config.file=/etc/loki/local-config.yaml")
    .WithHttpEndpoint(targetPort: 3100, name: "http");

// The collector tail-samples traces and forwards them to Tempo; logs go to Loki (OTLP).
// The Aspire dashboard still receives everything through the default OTLP exporter.
var collector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib", "0.161.0")
    .WithBindMount("../../../deploy/otel-collector/config.yaml", "/etc/otelcol/config.yaml", isReadOnly: true)
    .WithArgs("--config=/etc/otelcol/config.yaml")
    .WithEndpoint(targetPort: 4317, name: "otlp", scheme: "http")
    .WaitFor(tempo)
    .WaitFor(loki);

var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v3.5.0")
    .WithBindMount("../../../deploy/grafana/prometheus.aspire.yaml", "/etc/prometheus/prometheus.yml", isReadOnly: true)
    .WithHttpEndpoint(targetPort: 9090, name: "http");

var grafanaAdminPassword = builder.AddParameter("grafana-admin-password", secret: true);
builder.AddContainer("grafana", "grafana/grafana", "12.1.1")
    .WithBindMount("../../../deploy/grafana/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", grafanaAdminPassword)
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WaitFor(prometheus)
    .WaitFor(loki)
    .WaitFor(tempo);

// ---------------------------------------------------------------------------------------------
// Migrations: the same project in `migrate` mode, run to completion before the API starts.
// No launch profile: it is a short-lived console process without HTTP endpoints.
// ---------------------------------------------------------------------------------------------
var migrations = builder.AddProject<Projects.ModularMonolith_Host>("migrations", launchProfileName: null)
    .WithArgs("migrate")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__postgres-migrations", database)
    .WithEnvironment("Migrations__ApplicationLoginPassword", apiDbPassword)
    .WaitFor(database);

// ---------------------------------------------------------------------------------------------
// API host
// ---------------------------------------------------------------------------------------------
builder.AddProject<Projects.ModularMonolith_Host>("api")
    .WithEnvironment("ConnectionStrings__postgres", apiConnectionString)
    .WithReference(cache)
    .WithEnvironment("Authentication__Authority", ReferenceExpression.Create($"{keycloakHttp}/realms/modular-monolith"))
    .WithEnvironment("Modules__Identity__AccountErasure__ClientSecret", accountErasureSecret)
    .WithEnvironment("Observability__CollectorEndpoint", collector.GetEndpoint("otlp"))
    .WithEnvironment("Observability__OtlpMetrics", "true")
    .WithEnvironment("Observability__OtlpLogs", "true")
    .WithHttpHealthCheck("/health/ready")
    .WaitForCompletion(migrations)
    .WaitFor(cache)
    .WaitFor(keycloak);

await builder.Build().RunAsync();
