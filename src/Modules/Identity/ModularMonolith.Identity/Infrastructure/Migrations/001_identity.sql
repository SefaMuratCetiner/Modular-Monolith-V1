-- Identity module schema (created by the migrator). See catalog migration for the tenancy model.

create table identity.user_profiles (
    id           uuid         primary key,
    tenant_id    text         not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    subject_id   text         not null,
    email        varchar(254) not null,
    display_name varchar(100) not null,
    is_beta_user boolean      not null default false,
    created_by   text         not null,
    created_at   timestamptz  not null,
    modified_by  text         null,
    modified_at  timestamptz  null,
    is_deleted   boolean      not null default false
);

create unique index ux_user_profiles_tenant_subject on identity.user_profiles (tenant_id, subject_id) where not is_deleted;
create unique index ux_user_profiles_tenant_email on identity.user_profiles (tenant_id, lower(email)) where not is_deleted;

alter table identity.user_profiles enable row level security;
alter table identity.user_profiles force row level security;
create policy tenant_isolation on identity.user_profiles
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));

create table identity.audit_log (
    id          uuid        primary key,
    tenant_id   text        not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    user_id     text        not null,
    action      text        not null,
    payload     jsonb       not null,
    occurred_at timestamptz not null,
    ip_address  text        null,
    trace_id    text        null
);

alter table identity.audit_log enable row level security;
alter table identity.audit_log force row level security;
create policy tenant_isolation on identity.audit_log
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));

create table identity.outbox_messages (
    id              uuid        primary key,
    type            text        not null,
    payload         jsonb       not null,
    tenant_id       text        not null,
    occurred_at     timestamptz not null,
    status          text        not null default 'pending' check (status in ('pending', 'processed', 'dead')),
    attempts        integer     not null default 0,
    next_attempt_at timestamptz not null default now(),
    processed_at    timestamptz null,
    error           text        null
);

create index ix_outbox_messages_pending on identity.outbox_messages (next_attempt_at, occurred_at) where status = 'pending';

create table identity.inbox_messages (
    message_id   uuid        not null,
    handler      text        not null,
    tenant_id    text        not null default current_setting('app.tenant_id', true),
    processed_at timestamptz not null,
    primary key (message_id, handler)
);
