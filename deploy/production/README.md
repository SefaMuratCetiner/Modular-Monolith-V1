# Production Keycloak

`deploy/docker-compose.yml` and the Aspire AppHost run Keycloak for **development**: `start-dev`, plain HTTP, an in-memory database, and demo users with known passwords. This folder is the production counterpart. Use it as is with Docker Compose, or translate it to your platform (a Kubernetes `Deployment` plus `Secret`s works the same way).

| | Development (`deploy/keycloak`) | Production (`deploy/production`) |
|---|---|---|
| Start command | `start-dev` | `start --optimized` (image built with `kc.sh build`) |
| Transport | HTTP on 8080, `sslRequired=external` | HTTPS only on 8443 (HTTP disabled), `sslRequired=all` |
| Database | in-memory H2, lost on restart | its own PostgreSQL (`keycloak-db`) |
| Users | alice, bob, carol (demo passwords) | none: the bootstrap admin creates real accounts |
| E-mail | off | SMTP; `verifyEmail` and `resetPasswordAllowed` on |
| Secrets | `deploy/secrets/` (generated) | `deploy/production/secrets/` (from your secret store) |

Both realms share every other setting: brute-force protection, the password policy, MFA for `admin` and `operator`, scopes, the device flow, and the erasure service account.

## Files you provide

Put these in `deploy/production/secrets/` (git-ignored), or have your secret store write them there:

| File | Content |
|---|---|
| `keycloak_tls_certificate.pem` | TLS certificate chain for `KEYCLOAK_HOSTNAME` (PEM) |
| `keycloak_tls_key.pem` | Its private key (PEM) |
| `keycloak_db_password` | Password of Keycloak's PostgreSQL database |
| `keycloak_bootstrap_admin_password` | Temporary admin used for the first sign-in |
| `keycloak_admin_client_secret` | Secret of the `modular-monolith-admin` service account. Give the API the same value as `Modules__Identity__AccountErasure__ClientSecret` |
| `smtp_password` | SMTP password |

The non-secret settings (host names, SMTP server) go in `deploy/production/.env`; copy `.env.example`.

## First start

```bash
docker compose -f deploy/production/docker-compose.keycloak.yml --env-file deploy/production/.env up -d --build
```

1. Sign in to `https://<KEYCLOAK_HOSTNAME>/admin` as `bootstrap-admin`.
2. Create your personal administrator in the `master` realm, give it the `admin` role and enrol OTP, then delete `bootstrap-admin`.
3. In realm `modular-monolith`, create users. Give each one the `tenant_id` attribute and the `user` role, plus `admin` or `operator` where needed (those roles force OTP enrolment). Keycloak e-mails each user a link to verify their address.
4. Configure the API:
   - `Authentication__Authority=https://<KEYCLOAK_HOSTNAME>/realms/modular-monolith` (keep `RequireHttpsMetadata=true`).
   - `Modules__Identity__AccountErasure__ClientSecret` from your secret store.

`--import-realm` only creates the realm if it doesn't exist, so it never overwrites changes made in the admin console. Apply later changes in the console, or manage the realm as code with a tool such as keycloak-config-cli.

## Erasure service account

The realm defines a confidential client, `modular-monolith-admin`, used only with client credentials. Its service account holds a single role, `realm-management/manage-users`.

The API uses it to delete (or, with `Modules:Identity:AccountErasure:Mode=Disable`, to disable and log out) a user's Keycloak account after the user's personal data was erased in the application.

`manage-users` covers every user in the realm, so treat this secret like a database password: rotate it, keep it only in the secret store, and alert on admin events from this client. Keycloak's fine-grained admin permissions can narrow the account further.
