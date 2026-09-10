# Platform Account API

Module: PSS-009  
Gateway base path: `/api/platform/account`  
Frontend surface: `/Platform/Account/Profile`, `/Platform/Account/Settings`

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/account/me` | Return the current platform actor profile. |
| PUT | `/api/platform/account/me` | Update the current platform actor profile. |

## Frontend Proxy

The MVC controller exposes:

| Method | Path | Purpose |
|---|---|---|
| GET | `/Platform/Account/api/me` | Same-origin proxy to current profile. |
| PUT | `/Platform/Account/api/me` | Same-origin proxy to update current profile. |
| POST | `/Platform/Account/api/invalidate-snapshot` | Clear the local profile snapshot. |

## Rules

- Profile updates apply to the current platform actor only.
- Browser JavaScript uses the same-origin MVC proxy.
- API responses use `Response<T>`.

