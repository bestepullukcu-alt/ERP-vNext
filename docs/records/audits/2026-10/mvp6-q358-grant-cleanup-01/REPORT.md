# MVP-6 — Q358 grant clean-up runner (C-13 follow-up) — lane measurement

- **Decision:** `docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md` (C-13 retraction); XMC-001 consent for
  AuthService given by the owner in chat (2026-10-19), deletion semantics chosen by the owner: **soft delete + reactivation**.
- **Predecessor:** `docs/records/audits/2026-10/mvp6-c13-q358-retraction-01/` (the template entry removed; existing grants left).
- **Branch:** `feature/sce/mod-0183-q358-grant-cleanup` (from `origin/main` 5e99c1c32). Not committed at the time of writing.
- **Lane:** `~/mvp6-env/c13-220606` — lane replica set `rsc13` on 35994 (mongod 8.0.18, Homebrew), lane RabbitMQ 5672,
  Platform 5057, Auth 5056, DB `c13_auth`. Never 27017. Raw outputs in `lane/`.

## Why a runner is needed (measured before writing it)

| # | Fact | Evidence |
|---|---|---|
| M1 | An **entitled** tenant (C) keeps its five Shipment keys as **System** after the entitlement event is delivered: the sync skips a (role, permission) pair that already has a grant of any source. | `lane/q-cleanup-m1-overlap.txt` |
| M2 | Soft-deleting one of C's grants and replaying `tenant.activated` fails with `DuplicateRolePermissionException` — the unique (RoleId, PermissionId, TenantId) index is not partial, so a soft-deleted row blocks the re-grant. C stays 4/5. | `lane/q-cleanup-m2-softdelete-trap.txt` |
| — | `PlatformTenantEntitlementClient.GetEntitledModuleCodesAsync` returns an EMPTY list on any failure → unusable for a deletion decision. The runner uses `ReadEntitledModulesWithPermissionKeysAsync` (`IsAuthoritative`). | code read |

## What was built

| File | Role |
|---|---|
| `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/Q358ShipmentGrantRetraction.cs` | Pure decision: mode parse (anything unrecognised = Off), plan per tenant (entitled → ConvertToModule, not entitled → SoftDelete, unknown → Skip), deterministic audit ids. |
| `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/Q358ShipmentGrantRetractionRunner.cs` | Mongo executor: tenant system `Admin` roles only; System grants of the five keys only; audit row first (deterministic id), conditional change (still live AND still System), `…_not_applied` row when the change did not happen, role-assignment version bump per changed tenant. |
| `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Authorization/Q358ShipmentGrantRetractionHostedService.cs` (+ `DependencyInjection.cs` one line) | Runs once at start, in the background, only when `Q358Retraction:Mode` = `DryRun` / `Apply`. Default (absent) = Off: nothing read. |
| `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs` | `AssignAsync`: on duplicate key, revives a **soft-deleted** row (conditional on `IsDeleted == true`) with the new grant's source; a live duplicate still throws `DuplicateRolePermissionException`. |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Authorization/Q358ShipmentGrantRetractionTests.cs` | 15 tests (9 pure, 6 on an owned replica set). |

Soft delete sets `IsDeleted = true`, `UpdatedAt`, `UpdatedBy = system`. The entity has **no `DeletedAt` field** (`GlobalEntityBase`);
adding one is a cross-entity change not made here — open item below.

## Lane measurement (2026-10-20)

Lane-only fixture: tenant B (`…b8d2de805cb0`, not entitled, activated after C-13) had no Shipment grants, so the
non-entitled path could not be measured on real data. Five System grants were inserted on B's Admin to simulate the
pre-C-13 widening (`lane/q-cleanup-m3-fixture.txt`). Everything else is the state the earlier C-13 runs left.

| # | Run | Result | Evidence |
|---|---|---|---|
| M3 | Before | C Admin 4/5 System (read soft-deleted by M2); A Admin 5/5 Module; B Admin 5/5 System (fixture); Viewer read System everywhere; SuperAdmin 25 keys; 0 Q358 audit rows | `lane/q-cleanup-m3-before.txt` |
| M4 | `DryRun` | Plan: C 4 × ConvertToModule, B 5 × SoftDelete; A not a candidate; **0 changed, 0 audit rows** (holdings identical) | `lane/q-cleanup-m4-dryrun.txt`, `lane/q-cleanup-m4-after-dryrun.txt` |
| M5 | `Apply`, Platform internal key deliberately wrong | Both tenants **unknown → skipped**; 0 changed, 0 audit rows | `lane/q-cleanup-m5-unknown.txt` |
| M6 | `Apply` | C 4 → Module (`shipment-tracking-pod`), B 5 → soft-deleted; 4 `role_permission_source_corrected` + 5 `role_permission_revoked`; 0 not applied; versions of C and B = 1; A, Viewers, SuperAdmin unchanged | `lane/q-cleanup-m6-apply1.txt` |
| M7 | `Apply` again (restart) | 0 planned, 0 changed, audit counts unchanged | `lane/q-cleanup-m7-apply2.txt` |
| M8 | Replay `tenant.activated` for C | `processed`; C Admin **5/5 Module** — the soft-deleted `read` revived; no `DuplicateRolePermissionException` in the Auth log (M2 trap closed) | `lane/q-cleanup-m8-reactivation.txt`, `lane/replay-c3.*.json` |

## Tests and sabotage

Owned mongod (`DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod`, `DITEN_REQUIRE_OWNED_MONGO=1`, so a missing mongod is red):
`FullyQualifiedName~Q358ShipmentGrantRetraction` → **15 passed, 0 skipped**.

| Sabotage (production code, restored after) | Red test |
|---|---|
| Unknown entitlement treated as "entitled to nothing" | `Entitled_converts_a_normalized_code_counts_unknown_skips` |
| `AssignAsync` reactivation removed | `A_soft_deleted_grant_comes_back_when_granted_again_a_live_one_stays_a_duplicate` |
| Conditional change drops "still System" | `A_grant_changed_between_the_audit_row_and_the_write_is_left_as_it_is_and_the_row_withdrawn` |
| Audit-first seam removed (change before the audit point) | `A_run_stopped_after_the_audit_row_touches_no_grant…`, `A_grant_changed_between…` |

Restored sources were byte-compared with their saved copies (`cmp`).

## Gate

Local run `~/mvp6-env/q358-gate-005239` (owned mongod required, skips = red):

| Suite | Result | Note |
|---|---|---|
| Auth `Diten.AuthService.Application.Tests` | 2919 passed, **1 failed**, 0 skipped | The red is `ClosedWriterInventory_WhenAuthorityMutationCallChanges_RequiresPackCorrection`: four NEW authority-writer calls (`RolePermissionRepository` `Collection.UpdateOneAsync`; runner `rpCol.UpdateOneAsync`, `versions.IncrementAsync`, `collection.InsertOneAsync`). The guard is correct — the inventory is closed and needs an **owner correction**; it was NOT edited. |
| `TenantArchitecture.ArchitectureTests` | 141 passed, 3 failed | The same 3 as the C-13 baseline (`DocsPathGuard`, two `AuditTrailStandard`); none names a file of this change. |
| `scripts/tests` | 217 OK | — |

**Owner correction (2026-10-20, approved in chat):** the four rows were added by hand as `OwnerCorrection20261020Q358CleanupWriterCalls` in `ProductIdentityEntitlementReconciliationCommandContractTests.cs` (not a regeneration). Re-run `ProductIdentityEntitlementReconciliationCommandContractTests` + Q358 tests: **77 passed, 0 skipped**. The guard's red in the full run above is its own sabotage proof: without the rows it fails and names exactly these four calls.

## Not done / open

- **Not committed, not pushed** — owner approval required (GIT-002).
- **Production run** is an operator step: set `Q358Retraction__Mode=DryRun`, read the log, then `Apply`, then remove the setting.
  Who runs it, and when, is the owner's decision (TBD).
- **`DeletedAt`**: AGENTS core rule 2 names `IsDeleted` + `DeletedAt`; `GlobalEntityBase` has no `DeletedAt`. Soft delete here records
  `UpdatedAt`/`UpdatedBy`. Adding the field is an Auth-wide entity change — owner decision.
- **Other hard-delete paths** (`RevokeAsync`, `RemoveByIdAsync`, `RemoveByPermissionIdAsync`) are unchanged.
- `EntitlementReconciliationOperationStore` reads `rolePermissions` raw; its retirement-steward path was not re-measured against
  soft-deleted rows (not a Shipment path).
- Lane mongod is 8.0.18; CI uses 7.0.28 — not measured on 7.0.28.
