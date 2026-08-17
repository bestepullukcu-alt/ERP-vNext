# Tenant Login & Security API

Module: PSS-004  
Gateway base path: `/api/admin/tenants/{id}/login-settings`  
Frontend surface: `/Platform/Tenants/Security`

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/admin/tenants/{id}/login-settings` | Read tenant login and security settings. |
| PUT | `/api/admin/tenants/{id}/login-settings` | Update tenant login and security settings. |

## Internal AuthService Consumption

AuthService consumes tenant login settings through the Platform service to enforce tenant-specific login policy. Internal service communication uses configured internal credentials rather than browser tokens.

## Rules

- Settings are tenant-specific.
- Missing tenant login settings return a 404 response envelope.
- Browser JavaScript reaches these settings through the Platform MVC proxy/Gateway path, not direct service ports.

