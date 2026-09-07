# Consumer / Quota Model API

Module: MOD-0033  
Platform base path: `/api/platform/tenants/{tenantId}/quotas`  
Internal base path: `/api/internal/quotas`

## Platform Admin Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/tenants/{tenantId}/quotas` | Return all quota statuses for a tenant. |
| GET | `/api/platform/tenants/{tenantId}/quotas/{quotaKey}` | Return a single quota status. |
| POST | `/api/platform/tenants/{tenantId}/quotas/initialize` | Initialize tenant quotas from an approved source. |
| POST | `/api/platform/tenants/{tenantId}/quotas/sync-limits` | Sync quota limits from subscription configuration. |

## Internal Service Endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/internal/quotas/consume` | Atomically consume quota for a tenant/key. |
| POST | `/api/internal/quotas/release` | Release previously consumed quota. |
| POST | `/api/internal/quotas/reset-period` | Reset a quota period. |
| POST | `/api/internal/quotas/recalculate` | Recalculate quota usage where supported. |

## Security

Internal endpoints require `X-Internal-Api-Key`, valid tenant ID, and source. Correlation ID is accepted through request data or `X-Correlation-Id`.

