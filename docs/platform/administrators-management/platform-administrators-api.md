# Platform Administrators Management API

Module: NEW-002 Platform Administrators Management  
Service: Diten.Platform  
Base route: `/api/platform/administrators`

## Authorization

All endpoints require JWT authentication with policy `PlatformActor`. Mutating endpoints require the matching `Platform.Administrators.*` permission.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/platform/administrators` | Paged list with search, status, actor type, role and invitation filters. |
| GET | `/api/platform/administrators/stats` | KPI counts excluding soft-deleted records. |
| GET | `/api/platform/administrators/{id}` | Detail and audit-ready metadata. |
| POST | `/api/platform/administrators` | Invite a platform or partner administrator. |
| PUT | `/api/platform/administrators/{id}` | Update identity, scope, status and roles with version check. |
| POST | `/api/platform/administrators/{id}/suspend` | Suspend with required reason and version check. |
| POST | `/api/platform/administrators/{id}/reactivate` | Reactivate with version check. |
| DELETE | `/api/platform/administrators/{id}` | Soft-delete with version check. |
| DELETE | `/api/platform/administrators/bulk` | Soft-delete selected rows with per-row version check. |
| POST | `/api/platform/administrators/{id}/roles` | Assign roles with version check. |
| POST | `/api/platform/administrators/{id}/resend-invite` | Regenerate invite metadata unless invite is accepted. |

## Notes

- `TenantId` is never accepted by this module. Records use `GlobalEntity`.
- Email is trimmed, lowercased and enforced through `NormalizedEmail`.
- `PartnerAdmin` requires `PartnerId` and at least one `AllowedTenantIds` value.
- Invite delivery is currently logged as queue intent until notification/outbox integration is available.
- Gateway route `/api/platform/administrators` is required in `ocelot.json`; route addition is protected by the integration-agent ownership rule.
