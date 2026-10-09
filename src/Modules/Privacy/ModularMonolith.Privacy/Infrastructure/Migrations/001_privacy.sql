-- Privacy module schema (created by the migrator). See Catalog's scripts for the tenancy model.
-- Never edit an applied migration; add a new script instead.

-- Data-subject requests: evidence that each request was handled and when (GDPR Art. 5(2) accountability).
-- Only pseudonymous subject ids are stored.
create table privacy.data_subject_requests (
    id           uuid        primary key,
    tenant_id    text        not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    subject_id   text        not null,
    request_type text        not null check (request_type in ('export')),
    requested_at timestamptz not null,
    completed_at timestamptz null
);

create index ix_data_subject_requests_subject on privacy.data_subject_requests (tenant_id, subject_id, requested_at);

alter table privacy.data_subject_requests enable row level security;
alter table privacy.data_subject_requests force row level security;
create policy tenant_isolation on privacy.data_subject_requests
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));

grant select, insert, update on privacy.data_subject_requests to $app_role$;

-- Standard module tables -------------------------------------------------------------------------
-- Audit log, written in the same transaction as every command (who, what, before/after, when, where).
create table privacy.audit_log (
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

create index ix_audit_log_tenant_occurred on privacy.audit_log (tenant_id, occurred_at);
create index ix_audit_log_ip_retention on privacy.audit_log (occurred_at) where ip_address is not null;

alter table privacy.audit_log enable row level security;
alter table privacy.audit_log force row level security;
create policy tenant_isolation on privacy.audit_log
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));
-- Lets the owner-run retention function see every tenant's rows (see purge_audit_log below).
create policy owner_maintenance on privacy.audit_log to current_user using (true) with check (true);

-- Outbox (no RLS: the dispatcher drains all tenants; tenant_id travels with each message).
create table privacy.outbox_messages (
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

create index ix_outbox_messages_pending on privacy.outbox_messages (next_attempt_at, occurred_at) where status = 'pending';
create index ix_outbox_messages_leased on privacy.outbox_messages (locked_until) where status = 'processing';
create index ix_outbox_messages_processed on privacy.outbox_messages (processed_at) where status = 'processed';
create index ix_outbox_messages_dead on privacy.outbox_messages (occurred_at) where status = 'dead';

-- Inbox (idempotent consumers: one row per message and handler).
create table privacy.inbox_messages (
    message_id   uuid        not null,
    handler      text        not null,
    tenant_id    text        not null default current_setting('app.tenant_id', true),
    processed_at timestamptz not null,
    primary key (message_id, handler)
);

create index ix_inbox_messages_processed_at on privacy.inbox_messages (processed_at);

-- Audit retention without UPDATE or DELETE grants (see Catalog's 005 script for the rationale).
-- Single-quoted body (quotes doubled): DbUp substitutes dollar-delimited script variables.
create function privacy.purge_audit_log(p_delete_before timestamptz, p_strip_ip_before timestamptz, p_batch_size integer)
returns integer
language sql
security definer
set search_path = pg_catalog, pg_temp
as '
    with deleted as (
        delete from privacy.audit_log
        where id in (
            select id from privacy.audit_log
            where occurred_at < p_delete_before
              and p_delete_before <= now() - interval ''30 days''
            limit p_batch_size)
        returning 1),
    stripped as (
        update privacy.audit_log
        set ip_address = null
        where id in (
            select id from privacy.audit_log
            where ip_address is not null
              and occurred_at < p_strip_ip_before
              and (p_delete_before is null or occurred_at >= p_delete_before)
            limit p_batch_size)
        returning 1)
    select ((select count(*) from deleted) + (select count(*) from stripped))::integer
';

-- Least-privilege grants for the application role ($app_role$ is substituted by the migrator).
grant usage on schema privacy to $app_role$;
grant select, insert on privacy.audit_log to $app_role$;
grant select, insert, update, delete on privacy.outbox_messages to $app_role$;
grant select, insert, delete on privacy.inbox_messages to $app_role$;
revoke all on function privacy.purge_audit_log(timestamptz, timestamptz, integer) from public;
grant execute on function privacy.purge_audit_log(timestamptz, timestamptz, integer) to $app_role$;
