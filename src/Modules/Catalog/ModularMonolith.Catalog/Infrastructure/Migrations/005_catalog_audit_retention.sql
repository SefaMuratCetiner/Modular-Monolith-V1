-- v5: audit-log retention without giving the application UPDATE or DELETE on the audit log.
-- Never edit an applied migration; add a new script instead.
--
-- The application role stays append-only on catalog.audit_log. Retention (RetentionJob) calls this function,
-- which runs with the owner's rights (SECURITY DEFINER) and can only:
--   - delete rows older than p_delete_before, and never anything younger than 30 days;
--   - remove the client IP address (personal data) from rows older than p_strip_ip_before.
-- NULL leaves the respective step out. Each call handles at most p_batch_size rows per step.
create function catalog.purge_audit_log(p_delete_before timestamptz, p_strip_ip_before timestamptz, p_batch_size integer)
returns integer
language sql
security definer
set search_path = pg_catalog, pg_temp
-- Single-quoted body (quotes doubled): DbUp substitutes dollar-delimited script variables, so no dollar quoting.
as '
    with deleted as (
        delete from catalog.audit_log
        where id in (
            select id from catalog.audit_log
            where occurred_at < p_delete_before
              and p_delete_before <= now() - interval ''30 days''
            limit p_batch_size)
        returning 1),
    stripped as (
        update catalog.audit_log
        set ip_address = null
        where id in (
            select id from catalog.audit_log
            where ip_address is not null
              and occurred_at < p_strip_ip_before
              and (p_delete_before is null or occurred_at >= p_delete_before)
            limit p_batch_size)
        returning 1)
    select ((select count(*) from deleted) + (select count(*) from stripped))::integer
';

revoke all on function catalog.purge_audit_log(timestamptz, timestamptz, integer) from public;
grant execute on function catalog.purge_audit_log(timestamptz, timestamptz, integer) to $app_role$;

-- FORCE row-level security also applies to the owner, so the function (running as the owner, without a
-- tenant) would see no rows. This policy lets the owner maintain all tenants' rows; it grants nothing new,
-- because the owner could disable row-level security anyway. The application role is not affected.
create policy owner_maintenance on catalog.audit_log to current_user using (true) with check (true);

create index ix_audit_log_ip_retention on catalog.audit_log (occurred_at) where ip_address is not null;
