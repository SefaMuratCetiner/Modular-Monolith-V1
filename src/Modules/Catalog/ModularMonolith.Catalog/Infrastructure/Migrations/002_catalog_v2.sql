-- v2: optimistic concurrency, before/after audit, outbox leases and trace propagation, retention indexes.
-- Never edit an applied migration; add a new script instead.

-- Optimistic concurrency token: every update is "where version = @expected" and increments it.
alter table catalog.products add column version integer not null default 1 check (version > 0);

-- Before/after snapshots of the entities a command changed.
alter table catalog.audit_log add column changes jsonb null;
create index ix_audit_log_tenant_occurred on catalog.audit_log (tenant_id, occurred_at);

-- Outbox: leased claiming (no locks held while consumers run) and W3C trace context of the producer.
alter table catalog.outbox_messages add column locked_until timestamptz null;
alter table catalog.outbox_messages add column trace_parent text null;
alter table catalog.outbox_messages drop constraint outbox_messages_status_check;
alter table catalog.outbox_messages add constraint outbox_messages_status_check
    check (status in ('pending', 'processing', 'processed', 'dead'));
create index ix_outbox_messages_leased on catalog.outbox_messages (locked_until) where status = 'processing';
create index ix_outbox_messages_processed on catalog.outbox_messages (processed_at) where status = 'processed';
create index ix_outbox_messages_dead on catalog.outbox_messages (occurred_at) where status = 'dead';

-- Inbox retention.
create index ix_inbox_messages_processed_at on catalog.inbox_messages (processed_at);
