# Tenant Management API

Module: MOD-0043 / MOD-0044 / MOD-0046  
Gateway base path: `/api/admin/tenants`  
Frontend surface: `/Platform/Tenants`

## Access

Tenant Management is a Platform Admin surface and uses the `PlatformActor` authorization policy. Platform admin routes do not require `X-Tenant-Id` from the browser.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/admin/tenants` | List tenants with `search`, `status`, `region`, `page`, `pageSize`, and `sort`. |
| GET | `/api/admin/tenants/stats` | Return registry KPI counts. |
| GET | `/api/admin/tenants/{id}` | Return tenant details. |
| POST | `/api/admin/tenants` | Register a tenant. |
| PUT | `/api/admin/tenants/{id}` | Update tenant profile fields. |
| PUT | `/api/admin/tenants/{id}/branding` | Update tenant branding values. |
| POST | `/api/admin/tenants/{id}/suspend` | Suspend a tenant, optionally with a reason. |
| POST | `/api/admin/tenants/{id}/reactivate` | Reactivate a tenant, optionally with a reason. |
| DELETE | `/api/admin/tenants/{id}` | Soft-delete a tenant. |
| DELETE | `/api/admin/tenants/bulk` | Soft-delete multiple tenants. |
| GET | `/api/admin/tenants/{id}/modules` | Return tenant module summary. |
| GET | `/api/admin/tenants/{id}/users/summary` | Return tenant user summary. |
| GET | `/api/admin/tenants/{id}/admin-users` | List tenant admin users. |
| POST | `/api/admin/tenants/{id}/admin-users` | Create tenant admin user metadata. |
| PUT | `/api/admin/tenants/{id}/admin-users/{adminUserId}` | Update tenant admin user metadata. |
| DELETE | `/api/admin/tenants/{id}/admin-users/{adminUserId}` | Delete tenant admin user metadata. |
| POST | `/api/admin/tenants/{id}/admin-users/{adminUserId}/invite` | Trigger tenant admin invitation flow. |
| GET | `/api/admin/tenants/{id}/settings` | Return tenant operational settings. |
| PUT | `/api/admin/tenants/{id}/settings` | Update tenant operational settings. |
| GET | `/api/admin/tenants/{id}/login-settings` | Return tenant login/security settings. |
| PUT | `/api/admin/tenants/{id}/login-settings` | Update tenant login/security settings. |

## Commercial Endpoints

Tenant subscription lifecycle and module entitlements are rendered inside Tenant Details > Commercial.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/tenants/{tenantId}/commercial/subscription` | Current subscription. |
| GET | `/api/platform/tenants/{tenantId}/commercial/subscription/history` | Subscription history. |
| GET | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}` | Subscription detail. |
| GET | `/api/platform/tenants/{tenantId}/commercial/subscription/active` | Active subscription check. |
| GET | `/api/platform/tenants/{tenantId}/commercial/subscription/entitlements` | Effective subscription entitlements. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription` | Assign a plan. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/activate` | Activate a subscription. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/cancel` | Cancel a subscription. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/renew` | Renew a subscription. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/expire` | Expire a subscription. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/suspend` | Suspend a subscription. |
| POST | `/api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/reactivate` | Reactivate a subscription. |

## Response Contract

Platform API responses use the shared `Response<T>` envelope except file download flows. Not-found tenant reads return a 404 response envelope.

