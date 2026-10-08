-- v2: optimistic concurrency, before/after audit, outbox leases and trace propagation, retention indexes.
-- Never edit an applied migration; add a new script instead.

alter table identity.user_profiles add column version integer not null default 1 check (version > 0);

alter table identity.audit_log add column changes jsonb null;
create index ix_audit_log_tenant_occurred on identity.audit_log (tenant_id, occurred_at);

alter table identity.outbox_messages add column locked_until timestamptz null;
alter table identity.outbox_messages add column trace_parent text null;
alter table identity.outbox_messages drop constraint outbox_messages_status_check;
alter table identity.outbox_messages add constraint outbox_messages_status_check
    check (status in ('pending', 'processing', 'processed', 'dead'));
create index ix_outbox_messages_leased on identity.outbox_messages (locked_until) where status = 'processing';
create index ix_outbox_messages_processed on identity.outbox_messages (processed_at) where status = 'processed';
create index ix_outbox_messages_dead on identity.outbox_messages (occurred_at) where status = 'dead';

create index ix_inbox_messages_processed_at on identity.inbox_messages (processed_at);
