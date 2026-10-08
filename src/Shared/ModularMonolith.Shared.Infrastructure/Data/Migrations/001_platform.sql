-- Platform tables shared by the host (not owned by any module). The migrator creates the schema.

-- ASP.NET Core Data Protection key ring (cookie/session and antiforgery keys). Persisting it here
-- keeps sessions valid across restarts and replicas; keys are encrypted at rest with a certificate
-- outside Development (see DataProtection settings).
create table platform.data_protection_keys (
    id            bigint generated always as identity primary key,
    friendly_name text        not null,
    xml           text        not null,
    created_at    timestamptz not null default now()
);

-- Least privilege: the application may read and add keys, never change or delete them.
revoke all on all tables in schema platform from $app_role$;
grant usage on schema platform to $app_role$;
grant select, insert on platform.data_protection_keys to $app_role$;
