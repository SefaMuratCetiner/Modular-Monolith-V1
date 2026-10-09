-- v4: right to erasure and data minimisation.
-- Never edit an applied migration; add a new script instead.

-- Erased profiles are anonymized and soft-deleted; erased_at records when.
alter table identity.user_profiles add column erased_at timestamptz null;

-- identity.user-registered v1 carried the e-mail address (v2 does not): remove it from stored messages,
-- including dead letters, which retention never purges.
update identity.outbox_messages set payload = payload - 'email' where payload ? 'email';

-- Audit rows written before [PersonalData] redaction: replace the e-mail address and display name.
-- FORCE row-level security would hide every row from the owner here (no tenant is set), so it is
-- lifted for these statements only.
alter table identity.audit_log no force row level security;

update identity.audit_log
set payload = payload || '{"email": "[redacted]", "displayName": "[redacted]"}'::jsonb
where payload ? 'email';

update identity.audit_log as a
set changes = (
    select jsonb_agg(
               case when e.change -> 'after' ? 'email'
                    then jsonb_set(jsonb_set(e.change, '{after,email}', '"[redacted]"'), '{after,displayName}', '"[redacted]"')
                    else e.change
               end
               order by e.position)
    from jsonb_array_elements(a.changes) with ordinality as e(change, position))
where jsonb_typeof(a.changes) = 'array'
  and exists (select 1 from jsonb_array_elements(a.changes) as c(change) where c.change -> 'after' ? 'email');

alter table identity.audit_log force row level security;
