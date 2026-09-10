# Platform Admin Password & MFA Security API

Module: PSS-010  
Primary service: AuthService  
Related Platform surface: `/Platform/Account` and Platform administrator security flows

## Current Scope

This module is partially live. Platform account/profile APIs are documented in [Platform Account API](../platform-account/api.md). MFA and password challenge behavior is implemented primarily in AuthService and consumed by platform login/security flows.

## Related Platform Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/account/me` | Read current platform actor profile. |
| PUT | `/api/platform/account/me` | Update current platform actor profile fields. |

## Security Notes

- MFA challenge hashing requires `Mfa:HashSecret`; it must not fall back to the JWT signing secret.
- Platform administrator status can affect login through internal Platform/AuthService checks.
- Sensitive secrets are validated by the MOD-0012 secrets foundation.

