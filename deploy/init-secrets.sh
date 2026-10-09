#!/usr/bin/env bash
# Creates the local development secrets (random passwords) once. See init-secrets.ps1 for details.
#   ./deploy/init-secrets.sh [--force] [--skip-user-secrets]
set -euo pipefail

force=false
user_secrets=true
for argument in "$@"; do
  case "$argument" in
    --force) force=true ;;
    --skip-user-secrets) user_secrets=false ;;
    *) echo "unknown option: $argument" >&2; exit 2 ;;
  esac
done

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
secrets="$here/secrets"
mkdir -p "$secrets"

# Letters and digits only: safe inside connection strings. "|| true": tr ends with SIGPIPE under pipefail.
new_password() {
  local value
  value="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 32 || true)"
  printf '%s' "$value"
}

get_or_create() {
  local path="$secrets/$1"
  if [[ -f "$path" && "$force" == false ]]; then
    cat "$path"
  else
    new_password | tee "$path"
  fi
}

set_secret() { printf '%s' "$2" >"$secrets/$1"; }

owner_password="$(get_or_create postgres_password)"
api_password="$(get_or_create app_db_password)"
get_or_create grafana_admin_password >/dev/null
get_or_create keycloak_admin_password >/dev/null
erasure_secret="$(get_or_create keycloak_admin_client_secret)"

# Derived secrets, named after the configuration keys they set inside the containers.
set_secret "ConnectionStrings__postgres-migrations" "Host=postgres;Port=5432;Database=modular_monolith;Username=postgres;Password=$owner_password"
set_secret "ConnectionStrings__postgres" "Host=postgres;Port=5432;Database=modular_monolith;Username=modular_monolith_api;Password=$api_password"
set_secret "Migrations__ApplicationLoginPassword" "$api_password"
set_secret "Modules__Identity__AccountErasure__ClientSecret" "$erasure_secret"

if [[ "$user_secrets" == true ]]; then
  project="$here/../src/Host/ModularMonolith.Host"
  dotnet user-secrets --project "$project" set "ConnectionStrings:postgres-migrations" "Host=localhost;Port=5432;Database=modular_monolith;Username=postgres;Password=$owner_password" >/dev/null
  dotnet user-secrets --project "$project" set "ConnectionStrings:postgres" "Host=localhost;Port=5432;Database=modular_monolith;Username=modular_monolith_api;Password=$api_password" >/dev/null
  dotnet user-secrets --project "$project" set "Migrations:ApplicationLoginPassword" "$api_password" >/dev/null
  dotnet user-secrets --project "$project" set "Modules:Identity:AccountErasure:ClientSecret" "$erasure_secret" >/dev/null
fi

echo "Secrets are in $secrets (git-ignored)."
