-- v3: least-privilege grants for the application role ($app_role$ is substituted by the migrator).
-- Replaces the schema-wide "all tables" grants of earlier versions. Every new table must be granted
-- explicitly in the migration that creates it.
revoke all on all tables in schema identity from $app_role$;
revoke all on all sequences in schema identity from $app_role$;
grant usage on schema identity to $app_role$;

-- Business tables: soft delete only, so no DELETE.
grant select, insert, update on identity.user_profiles to $app_role$;

-- Audit log is append-only for the application.
grant select, insert on identity.audit_log to $app_role$;

-- Outbox: written by commands, claimed/updated by the dispatcher, delivered rows purged by retention.
grant select, insert, update, delete on identity.outbox_messages to $app_role$;

-- Inbox: written by consumers, purged by retention.
grant select, insert, delete on identity.inbox_messages to $app_role$;

-- identity.schema_versions (migration journal): no access.
