# General Audit Trail API

Module: MOD-0021  
Gateway base path: `/api/platform/audit`  
Frontend surfaces: `/Platform/AuditLog`, `/Platform/AuditRetention`

## Access

Audit endpoints are restricted to `PlatformAdminOnly` in the current implementation. Partner admin audit scope is explicitly deferred until scoped filtering is implemented.

## Endpoints

| Method | Path | Permission | Purpose |
|---|---|---|---|
| GET | `/api/platform/audit/events` | `Platform.Audit.Read` | List audit events with filters. |
| GET | `/api/platform/audit/events/{id}` | `Platform.Audit.Read` | Return a single audit event. |
| GET | `/api/platform/audit/export` | `Platform.Audit.Export` | Export audit events as a file. |
| GET | `/api/platform/audit/export-limits` | `Platform.Audit.Read` | Return export row/day limits. |
| GET | `/api/platform/audit/retention` | `Platform.Audit.Retention.Update` | Read retention policies. |
| PUT | `/api/platform/audit/retention` | `Platform.Audit.Retention.Update` | Update retention policies. |
| POST | `/api/platform/audit/redact-actor` | `Platform.Audit.RedactActor` | Redact actor PII in audit data. |

## Export Notes

Export responses return file content and set `X-Audit-Export-Row-Count`. Failed exports return the standard response envelope.

