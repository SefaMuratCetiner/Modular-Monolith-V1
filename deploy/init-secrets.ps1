<#
.SYNOPSIS
    Creates the local development secrets (random passwords) once.

.DESCRIPTION
    Writes one file per secret to deploy/secrets/ (git-ignored). Compose mounts them as Docker secrets
    under /run/secrets, where the host reads them as configuration keys (file name = key, "__" = ":").
    Unless -SkipUserSecrets is given, it also stores localhost connection strings in the host project's
    user secrets, so `dotnet run -- migrate` and `dotnet run` work against the compose PostgreSQL.

    Existing passwords are kept. -Force generates new ones; PostgreSQL only applies its password when
    the data volume is created, so recreate it afterwards (docker compose down -v).

.EXAMPLE
    ./deploy/init-secrets.ps1
#>
param(
    [switch] $Force,
    [switch] $SkipUserSecrets
)

$ErrorActionPreference = "Stop"
$secrets = Join-Path $PSScriptRoot "secrets"
New-Item -ItemType Directory -Force -Path $secrets | Out-Null

function New-Password {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    # Letters and digits only: safe inside connection strings without quoting.
    return ([Convert]::ToBase64String($bytes) -replace '[^A-Za-z0-9]', '').Substring(0, 32)
}

function Get-OrCreate([string] $name) {
    $path = Join-Path $secrets $name
    if ((Test-Path $path) -and -not $Force) { return (Get-Content -Raw $path).Trim() }
    $value = New-Password
    [System.IO.File]::WriteAllText($path, $value)
    return $value
}

function Set-Secret([string] $name, [string] $value) {
    [System.IO.File]::WriteAllText((Join-Path $secrets $name), $value)
}

$ownerPassword = Get-OrCreate "postgres_password"
$apiPassword = Get-OrCreate "app_db_password"
Get-OrCreate "grafana_admin_password" | Out-Null
Get-OrCreate "keycloak_admin_password" | Out-Null
$erasureSecret = Get-OrCreate "keycloak_admin_client_secret"

# Derived secrets, named after the configuration keys they set inside the containers.
Set-Secret "ConnectionStrings__postgres-migrations" "Host=postgres;Port=5432;Database=modular_monolith;Username=postgres;Password=$ownerPassword"
Set-Secret "ConnectionStrings__postgres" "Host=postgres;Port=5432;Database=modular_monolith;Username=modular_monolith_api;Password=$apiPassword"
Set-Secret "Migrations__ApplicationLoginPassword" $apiPassword
Set-Secret "Modules__Identity__AccountErasure__ClientSecret" $erasureSecret

if (-not $SkipUserSecrets) {
    $hostProject = Join-Path $PSScriptRoot "../src/Host/ModularMonolith.Host"
    dotnet user-secrets --project $hostProject set "ConnectionStrings:postgres-migrations" "Host=localhost;Port=5432;Database=modular_monolith;Username=postgres;Password=$ownerPassword" | Out-Null
    dotnet user-secrets --project $hostProject set "ConnectionStrings:postgres" "Host=localhost;Port=5432;Database=modular_monolith;Username=modular_monolith_api;Password=$apiPassword" | Out-Null
    dotnet user-secrets --project $hostProject set "Migrations:ApplicationLoginPassword" $apiPassword | Out-Null
    dotnet user-secrets --project $hostProject set "Modules:Identity:AccountErasure:ClientSecret" $erasureSecret | Out-Null
}

Write-Host "Secrets are in $secrets (git-ignored)." -ForegroundColor Green
