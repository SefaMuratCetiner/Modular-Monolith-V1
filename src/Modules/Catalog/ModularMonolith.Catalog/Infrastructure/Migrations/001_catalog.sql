-- Catalog module schema. The schema itself is created by the migrator before this script runs.
-- Tenancy: every business table carries tenant_id, filled from the session variable app.tenant_id
-- and enforced by row-level security. FORCE makes RLS apply to the table owner as well.

-- Products --------------------------------------------------------------------------------------
create table catalog.products (
    id          uuid           primary key,
    tenant_id   text           not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    name        varchar(200)   not null,
    sku         varchar(64)    not null,
    price       numeric(18, 2) not null check (price > 0),
    currency    char(3)        not null,
    seller_id   text           not null,
    created_by  text           not null,
    created_at  timestamptz    not null,
    modified_by text           null,
    modified_at timestamptz    null,
    is_deleted  boolean        not null default false
);

create unique index ux_products_tenant_sku on catalog.products (tenant_id, sku) where not is_deleted;
create index ix_products_tenant_name on catalog.products (tenant_id, name);

alter table catalog.products enable row level security;
alter table catalog.products force row level security;
create policy tenant_isolation on catalog.products
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));

-- Sellers: local read model projected from Identity's UserRegisteredIntegrationEvent -------------
create table catalog.sellers (
    tenant_id     text        not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    subject_id    text        not null,
    display_name  text        not null,
    email         text        not null,
    registered_at timestamptz not null,
    primary key (tenant_id, subject_id)
);

alter table catalog.sellers enable row level security;
alter table catalog.sellers force row level security;
create policy tenant_isolation on catalog.sellers
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));

-- Audit log (written in the same transaction as the command) ------------------------------------
create table catalog.audit_log (
    id          uuid        primary key,
    tenant_id   text        not null default current_setting('app.tenant_id', true) check (tenant_id <> ''),
    user_id     text        not null,
    action      text        not null,
    payload     jsonb       not null,
    occurred_at timestamptz not null,
    ip_address  text        null,
    trace_id    text        null
);

alter table catalog.audit_log enable row level security;
alter table catalog.audit_log force row level security;
create policy tenant_isolation on catalog.audit_log
    using (tenant_id = current_setting('app.tenant_id', true))
    with check (tenant_id = current_setting('app.tenant_id', true));

-- Outbox (no RLS: the dispatcher drains all tenants; tenant_id travels with each message) -------
create table catalog.outbox_messages (
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

create index ix_outbox_messages_pending on catalog.outbox_messages (next_attempt_at, occurred_at) where status = 'pending';

-- Inbox (idempotent consumers: one row per message and handler) ---------------------------------
create table catalog.inbox_messages (
    message_id   uuid        not null,
    handler      text        not null,
    tenant_id    text        not null default current_setting('app.tenant_id', true),
    processed_at timestamptz not null,
    primary key (message_id, handler)
);
