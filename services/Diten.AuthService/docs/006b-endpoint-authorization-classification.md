# 006B — AuthService privileged-endpoint authorization classification

> **Scope:** S5 / AG-STEP-006B (AG-INFRA-COMPLETION). Per-endpoint classification of AuthService
> controllers to decide where `[HasPermission]` enforcement belongs. **Outcome: 0 controllers need a
> new guard** — every currently-unguarded endpoint is public-auth, self-service, or internal
> service-to-service, where adding `[HasPermission]` would lock out a legitimate caller or break a
> no-JWT S2S call. Authority: BME-001, PKS-001. MDM is already 100% guarded and untouched.

## Decision rule

- **(a) Admin-op** — operates on *other* subjects / tenant-wide state, called by a user with a JWT →
  **must** carry `[HasPermission("<module>.<resource>.<action>")]` (default-deny, PKS-001).
- **(b) Auth-lifecycle / self-service** — acts only on the caller's own identity, or the caller may not
  yet hold any permission (login, forced password change) → `[Authorize]` (or `[AllowAnonymous]`).
  `[HasPermission]` **must not** be added — it would lock the user out of the flow that grants access.
- **(c) Internal S2S** — called by another service with `X-Internal-Api-Key`, no user JWT →
  `[HasPermission]` is **N/A** (no permission claim) and would break the caller. The internal key is
  the guard. (Internal-key hardening is a separate security-review topic, not a 006B permission gap.)

## Classification (live @ this branch)

| Controller | Endpoint | Legitimate caller | Class | Enforcement |
|---|---|---|---|---|
| UsersController | all admin-op actions (incl. resend-invite) | tenant admin | (a) | `[HasPermission("auth.users.*")]` — already present |
| UsersController | set-password (invitation redemption) | invited user, no JWT yet | (b) | `[AllowAnonymous]` — keep (user located by token hash; guarding would lock out) |
| RolesController | all 8 actions (incl. S4 GET …/permissions) | tenant admin | (a) | `[HasPermission("auth.roles.*")]` — already present |
| PermissionsController | all 2 actions | tenant admin | (a) | `[HasPermission("auth.permissions.read"/"auth.roles.read")]` — already present |
| AuthController | login, register, refresh-token | anyone | public | `[AllowAnonymous]` — keep |
| AuthController | logout, change-password, me | authenticated self | (b) | `[Authorize]` only — keep (no HasPermission) |
| PlatformAuthController | login, forgot-password, reset-password | anyone | public | `[AllowAnonymous]` — keep |
| PlatformAuthController | platform-admins/provision, platform-admins/sync | Platform service (X-Internal-Api-Key) | (c) | internal key — keep (HasPermission N/A) |
| PlatformAuthController | change-password/forced | authenticated self (pre-access, forced flow) | (b) | `[Authorize]` only — keep (guarding would lock out) |
| TenantAuthController | login, mfa/verify, mfa/resend, register | anyone | public | `[AllowAnonymous]` — keep |
| InternalEventsController | tenant-activated, tenant-admin-invited | Platform service (X-Internal-Api-Key) | (c) | internal key — keep (HasPermission N/A) |

## Why no retrofit

The admin-op surface (Users / Roles / Permissions) was already fully `[HasPermission]`-guarded before
S5. The remaining endpoints are all class (b) or (c). Adding `[HasPermission]`:

- to **(b)** would deny the caller the very flow that lets them obtain access (e.g. forced password
  change requires the user to act *before* they have normal permissions) → lock-out;
- to **(c)** would require a JWT permission claim the Platform service does not send → broken S2S.

The `EndpointAuthorizationClassificationTests` regression test pins this: admin-op controllers stay
fully `[HasPermission]`-guarded, and the named (b)/(c) endpoints stay free of `[HasPermission]`, so a
future change cannot silently introduce an unguarded admin-op or lock out a lifecycle/S2S flow.
