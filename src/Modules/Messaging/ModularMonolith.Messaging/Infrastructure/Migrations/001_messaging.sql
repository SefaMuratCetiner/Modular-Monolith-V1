-- Messaging module schema (created by the migrator). It has no business tables yet: it administers the
-- other modules' outboxes (dead letters) through the outbox API. Its own schema holds the standard module
-- tables, above all the audit log of operator actions (dead-letter replays).
-- Never edit an applied migration; add a new script instead.

-- Standard module tables -------------------------------------------------------------------------
-- Audit log, written in the same transaction as every command (who, what, before/after, when, where).
create table messaging.audit_log (
    id          uuid        primary key,
    tenant_id   text        not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    user_id     text        not null,
    action      text        not null,
    payload     jsonb       not null,
    changes     jsonb       null,
    occurred_at timestamptz not null,
    ip_address  text        null,
    trace_id    text        null
);

create index ix_audit_log_tenant_occurred on messaging.audit_log (tenant_id, occurred_at);
create index ix_audit_log_ip_retention on messaging.audit_log (occurred_at) where ip_address is not null;

alter table messaging.audit_log enable row level security;
alter table messaging.audit_log force row level security;
create policy tenant_isolation on messaging.audit_log
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));
-- Lets the owner-run retention function see every tenant's rows (see purge_audit_log below).
create policy owner_maintenance on messaging.audit_log to current_user using (true) with check (true);

-- Outbox (no RLS: the dispatcher drains all tenants; tenant_id travels with each message).
create table messaging.outbox_messages (
    id              uuid        primary key,
    type            text        not null,
    payload         jsonb       not null,
    tenant_id       text        not null,
    occurred_at     timestamptz not null,
    status          text        not null default 'pending' check (status in ('pending', 'processing', 'processed', 'dead')),
    attempts        integer     not null default 0,
    next_attempt_at timestamptz not null default now(),
    processed_at    timestamptz null,
    error           text        null,
    locked_until    timestamptz null,
    trace_parent    text        null
);

create index ix_outbox_messages_pending on messaging.outbox_messages (next_attempt_at, occurred_at) where status = 'pending';
create index ix_outbox_messages_leased on messaging.outbox_messages (locked_until) where status = 'processing';
create index ix_outbox_messages_processed on messaging.outbox_messages (processed_at) where status = 'processed';
create index ix_outbox_messages_dead on messaging.outbox_messages (occurred_at) where status = 'dead';

-- Inbox (idempotent consumers: one row per message and handler).
create table messaging.inbox_messages (
    message_id   uuid        not null,
    handler      text        not null,
    tenant_id    text        not null default current_setting('app.tenant_id', true),
    processed_at timestamptz not null,
    primary key (message_id, handler)
);

create index ix_inbox_messages_processed_at on messaging.inbox_messages (processed_at);

-- Audit retention without UPDATE or DELETE grants (see Catalog's 005 script for the rationale).
-- Single-quoted body (quotes doubled): DbUp substitutes dollar-delimited script variables.
create function messaging.purge_audit_log(p_delete_before timestamptz, p_strip_ip_before timestamptz, p_batch_size integer)
returns integer
language sql
security definer
set search_path = pg_catalog, pg_temp
as '
    with deleted as (
        delete from messaging.audit_log
        where id in (
            select id from messaging.audit_log
            where occurred_at < p_delete_before
              and p_delete_before <= now() - interval ''30 days''
            limit p_batch_size)
        returning 1),
    stripped as (
        update messaging.audit_log
        set ip_address = null
        where id in (
            select id from messaging.audit_log
            where ip_address is not null
              and occurred_at < p_strip_ip_before
              and (p_delete_before is null or occurred_at >= p_delete_before)
            limit p_batch_size)
        returning 1)
    select ((select count(*) from deleted) + (select count(*) from stripped))::integer
';

-- Least-privilege grants for the application role ($app_role$ is substituted by the migrator).
grant usage on schema messaging to $app_role$;
grant select, insert on messaging.audit_log to $app_role$;
grant select, insert, update, delete on messaging.outbox_messages to $app_role$;
grant select, insert, delete on messaging.inbox_messages to $app_role$;
revoke all on function messaging.purge_audit_log(timestamptz, timestamptz, integer) from public;
grant execute on function messaging.purge_audit_log(timestamptz, timestamptz, integer) to $app_role$;
