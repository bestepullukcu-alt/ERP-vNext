# MOD-0018-FU13 Permission Convention + Cache Invalidation Events - Status Reconciliation

## Metadata
- **Date:** 2026-08-06
- **Domain:** Platform Shared Services
- **Module:** MOD-0018-FU13 Permission Convention + Cache Invalidation Events
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0018-FU13-permission-convention-cache-invalidation.md`
- **Status:** Implementation evidence recorded; live horizontal fan-out proof pending

## Scope
This note reconciles governance/status after finding that FU13 Groups A-C implementation evidence already exists locally. It records evidence only; it does not authorize or add runtime changes.

## Implementation Evidence
| Group | Evidence |
|---|---|
| Group A - Platform fan-out | `EntitlementCacheInvalidationConsumer` is registered with a per-instance temporary endpoint using `PlatformInstanceIdentity.InstanceId`; the other tenant lifecycle consumers remain on the default competing-consumer topology. |
| Group B - user-role removal | `RevokeRoleCommandHandler` revokes the user-role assignment, increments the tenant role-assignment version, and calls `IRefreshTokenRepository.RevokeAllByUserAsync(userId, tenantId, ct)` for the affected user. |
| Group C - role-permission removal | `RevokePermissionCommandHandler` rejects non-manual grants, revokes the manual role-permission, increments the tenant role-assignment version, resolves tenant-scoped holders through `IUserRoleRepository.GetUserIdsByRoleAsync`, and revokes each distinct holder's refresh tokens. |
| Repository seam | `UserRoleRepository.GetUserIdsByRoleAsync` filters server-side by `RoleId`, `TenantId`, and `IsDeleted == false`, projects `UserId`, and returns distinct holder IDs. |
| Data-scope guard | No cross-request data-scope cache was added; request-fresh authorization context behavior remains the intended v1 posture. |

## Validation Evidence
- `Diten.Platform.API` build: PASS, 0 errors.
- `Diten.Platform.Application.Tests`: PASS, 557/557.
- `Diten.Platform.Eventing.Tests`: PASS, 56/56, with 3 pre-existing skipped.
- `Diten.AuthService` Application + Persistence + API builds: PASS, 0 errors.
- `Diten.AuthService.Application.Tests`: PASS, 30/30.

## Boundaries Confirmed
- No frontend change is required for FU13 v1.
- No Gateway change is required.
- No appsettings, seed/grant, migration, fixture-data, AuthService seed/grant, or `.antigravity` change is part of this reconciliation.
- No deny-list, blacklist, Redis, distributed cache, new cross-service role-change event, or cross-request data-scope cache is introduced by FU13 v1.

## Remaining Blocker
FU13 must not be marked done until live two-instance RabbitMQ fan-out proof verifies that one entitlement invalidation event reaches both Platform instances and evicts both local `IMemoryCache` instances, while the other three consumers remain once-per-cluster and do not duplicate side effects.
