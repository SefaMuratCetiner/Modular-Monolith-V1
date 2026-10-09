-- v4: data minimisation and right to erasure.
-- Never edit an applied migration; add a new script instead.

-- Catalog never needed the seller's e-mail address (identity.user-registered v2 no longer carries it).
-- Expand/contract: the previous release may still insert it while this one rolls out, so the column is
-- emptied and made nullable here and dropped by a later migration.
alter table catalog.sellers alter column email drop not null;

-- FORCE row-level security would hide every row from the owner here (no tenant is set), so it is
-- lifted for this statement only.
alter table catalog.sellers no force row level security;
update catalog.sellers set email = null where email is not null;
alter table catalog.sellers force row level security;

-- Erased users keep their seller row (products reference it), anonymized; erased_at records when.
alter table catalog.sellers add column erased_at timestamptz null;
