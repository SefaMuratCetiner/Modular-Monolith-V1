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

// Valkey: BSD-licensed, Redis-protocol compatible cache (Redis 7.4+ is no longer OSI open source).
var cache = builder.AddValkey("cache")
    .WithDataVolume();

// ---------------------------------------------------------------------------------------------
// Identity provider
// ---------------------------------------------------------------------------------------------
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

// Fixed port 8080: the token issuer URL must be identical for browsers, CLIs and the API.
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.3.3")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
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
    .WithHttpEndpoint(targetPort: 3200, name: "http")
    .WithEndpoint(targetPort: 4317, name: "otlp", scheme: "http");

var loki = builder.AddContainer("loki", "grafana/loki", "3.5.3")
    .WithBindMount("../../../deploy/grafana/loki.yaml", "/etc/loki/local-config.yaml", isReadOnly: true)
    .WithArgs("-config.file=/etc/loki/local-config.yaml")
    .WithHttpEndpoint(targetPort: 3100, name: "http");

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
// API host
// ---------------------------------------------------------------------------------------------
builder.AddProject<Projects.ModularMonolith_Host>("api")
    .WithReference(database)
    .WithReference(cache)
    .WithEnvironment("Authentication__Authority", ReferenceExpression.Create($"{keycloakHttp}/realms/modular-monolith"))
    .WithEnvironment("Observability__LokiUrl", loki.GetEndpoint("http"))
    .WithEnvironment("Observability__TempoEndpoint", tempo.GetEndpoint("otlp"))
    .WithEnvironment("Observability__OtlpMetrics", "true")
    .WithEnvironment("Observability__OtlpLogs", "true")
    .WithHttpHealthCheck("/health/ready")
    .WaitFor(database)
    .WaitFor(cache)
    .WaitFor(keycloak);

await builder.Build().RunAsync();
