# Personal data inventory

Where the application stores personal data, why, for how long, and what export and erasure do with it.
Update this file in the same pull request as any change that stores a new personal field.

## Inventory

| Data | Where | Why | Retention | Export | Erasure |
|---|---|---|---|---|---|
| E-mail address | `identity.user_profiles.email` | Contact and uniqueness per tenant | Until erased | `identity` section | Replaced by `erased-<id>@erased.invalid`, row soft-deleted |
| Display name | `identity.user_profiles.display_name` | Shown to other users | Until erased | `identity` section | Replaced by `Erased user` |
| Display name (copy) | `catalog.sellers.display_name` | Seller name on product listings | Until erased | `catalog` section | Replaced by `Erased user` (`identity.user-erased` consumer) |
| Display name (in transit) | `identity.outbox_messages.payload` (`identity.user-registered`) | Event delivery | Delivered rows purged after `Retention:ProcessedOutboxDays` (7) | n/a | Purged by retention |
| Subject id (`sub`) | All modules (`subject_id`, `seller_id`, `created_by`, `audit_log.user_id`) | Pseudonymous link to the identity provider | Kept | `subjectId` | Kept: it identifies no one without Keycloak |
| IP address | `*.audit_log.ip_address` | Security investigations | Audit retention (set per deployment) | No | Kept (legitimate interest; document it in your record of processing) |
| Account, credentials, MFA | Keycloak | Authentication | Keycloak | Keycloak account console | Disable or delete the user in Keycloak |

What is **not** stored any more:

- `identity.user-registered` v2 no longer carries the e-mail address. Stored v1 messages are upcast, and migration `004_identity_personal_data.sql` removed it from existing outbox rows and dead letters.
- `catalog.sellers.email` is emptied by `004_catalog_personal_data.sql` and dropped in a later release (expand/contract).
- Audit rows store `[PersonalData]` fields as `[redacted]`. Rows written before that were redacted by `004_identity_personal_data.sql`.

## Controls

| Control | Implementation |
|---|---|
| Marking | `[PersonalData]` (`Shared.Kernel/Privacy`) on command, snapshot and contract properties. On positional records, use `[property: PersonalData]` |
| Audit trail | `AuditWriter` serializes with `PersonalDataRedaction.AuditOptions`: marked strings become `[redacted]` and other marked values are omitted |
| Logs | `PersonalDataDestructuringPolicy` redacts marked properties of objects logged with `{@Value}`. `PersonalDataMaskingEnricher` masks properties with sensitive names (`Email`, `DisplayName`, `Password`, tokens, …) and e-mail addresses inside any string, for every sink |
| Exceptions | Npgsql leaves parameter values and row details out of exception messages. Keep `Include Error Detail` off in connection strings |
| Right of access / portability | `GET /api/v1/privacy/me/personal-data` combines every module's `IPersonalDataExporter` into one JSON document |
| Right to erasure | `DELETE /api/v1/identity/users/me` (self-service) or `DELETE /api/v1/identity/users/{subjectId}` (tenant admin). Identity anonymizes its profile in the command transaction and publishes `identity.user-erased` through the outbox in the same transaction. Every other module consumes it (inbox-protected) and anonymizes its copy |

## Adding personal data to a module

1. Ask whether the module needs the value, or whether the subject id is enough.
2. Mark the field `[PersonalData]` wherever it appears in a command, an audit snapshot or a contract.
3. Implement `IPersonalDataExporter` in the module (an internal class; it is discovered automatically).
4. If the module copies data from another module, consume `UserErasedIntegrationEvent` and anonymize the copy.
5. Add a row to the inventory above.
