# PSS · MOD-0027-FU03A Notification Event SourceType & PlatformSeed Bridge — Smoke & Closeout Audit

- **Module:** MOD-0027-FU03A — Notification Event SourceType & PlatformSeed Bridge (parent MOD-0027)
- **Domain:** Platform & Shared Services (PSS) · **Service:** Diten.Platform · **Shell:** none (foundation bridge)
- **Date:** 2026-07-08
- **Verification type:** Live fleet rebuild + HTTP smoke (Web 5001 / Gateway 5000 / Platform API 5057) + full build/test static evidence
- **Final status:** **PASS (with note)** — foundation is built, unit-proven (1148/0) and live-boot-clean; the note is that the authenticated DTO JSON round-trip + visual UI were not exercised live (no restricted-actor token), compensated by the test suite + clean startup + endpoint probes. Bridge ships **no tenant event content** (empty seed catalog) — the correct controlled state; content is FU04A scope.
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0027-FU03A-notification-event-sourcetype-platformseed-bridge.md`
- **Related:** [MOD-0027-FU03 smoke](pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md) · [MOD-0027-FU02 smoke](pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md)

## 1. Scope summary
FU03A turns the FU03 Notification Event Catalog from **manifest-only** into a **source-typed** foundation so a future PlatformSeed/SystemSeed event can live in the same catalog **without** touching Module Catalog / IModuleManifestProvider. It ships the **generic machinery only** — **no tenant event content** (that is FU04A).

| Increment | Scope | Status |
|---|---|---|
| 1 | `NotificationEventSourceType { Manifest=0, PlatformSeed=1, SystemSeed=2 }` enum + `NotificationEventDefinition` additive fields (SourceType/OwnerArea/OwnerDisplayName/TargetRoute/ModuleCatalogRef/RequiredPolicy) | PASS |
| 2 | Manifest sync guardrail (create writes `SourceType=Manifest`; clobber guard skips PlatformSeed/SystemSeed) | PASS |
| 3 | Generic PlatformSeed validation planner (Module Catalog/ModulePages bypass; policy-gated model; §5.1 layer rule) + idempotent `NotificationEventSeeder` + **empty** `NotificationEventSeedCatalog` + startup `NotificationEventSeed.EnsureSeededAsync` | PASS |
| 4 | Additive read-only DTO/mapping/UI (`sourceType`/`ownerArea`/`targetRoute`) + en/tr RESX | PASS |
| 5 | Build/test + live smoke + closeout (this report) | PASS (with note) |

## 2. Build / test evidence
| Gate | Result |
|---|---|
| Fleet clean rebuild (`watch-diten-bg.ps1`, 7 services) | **0 build errors**; all 7 ports listening (5000/5001/5056/5057/5058/5059/5060) — live proof Platform.API + Diten.Web compiled & booted with FU03A changes |
| `dotnet build Platform.API` | **0 error** (compiled as part of test build + fleet rebuild) |
| `dotnet build Diten.Web` | **0 error** (Razor + RESX validated) |
| Diten.Platform.Application tests | **1148 passed / 0 failed / 0 skipped** (this session, pre-restart) |
| NotificationEventCatalogTests (filtered) | **17 passed / 0 failed** (7 pre-existing FU03 + **10 new FU03A**) |

**New FU03A tests (10):** enum `Manifest==0` + entity default · sync `create` writes `SourceType=Manifest` · sync does **not** clobber PlatformSeed (skip + "Cross-source collision" issue) · PlatformSeed validation accepts policy-gated null-permission (Active) · permission-gated → Draft (API-side pass) · missing template → issue · generic seed does **not** clobber Manifest · generic seed idempotent (HARD reconcile + SOFT preserve + no duplicate) · `active-template-slots` source-agnostic.

> **Note (fleet lock):** in-place `dotnet test`/`build` were **not re-run during closeout** to avoid file locks + `dotnet watch` restarts while the fleet was live for smoke. Compensating evidence: the fresh clean fleet rebuild (0 errors, all services up) + the 1148/0 run earlier this session.

## 3. Smoke evidence (live, through the running fleet)
Unauthenticated probes (no seeded restricted-actor token available; SuperAdmin auto-passes):

| Step | Evidence | Result |
|---|---|---|
| Web `/Platform/NotificationEvents` | **HTTP 302** → login redirect (renders; NOT 500) — additive Details.cshtml/RESX/JS did not break the page | PASS |
| Platform API `…/events` (direct 5057) | **HTTP 403** — endpoint wired, `PlatformActor` fail-closed (NOT 500) | PASS |
| Gateway `…/events` (5000) | **HTTP 403** — gateway route intact | PASS |
| `…/template-slots` | **HTTP 403** — endpoint preserved, source-agnostic (NOT 500) | PASS |
| `…/events/sync-from-manifest` (POST) | **HTTP 403** — manifest sync endpoint wired (NOT 500) | PASS |
| Platform startup (seed no-op) | Platform.API **booted to `PlatformPermissionAutoRegistrationWorker` (Total=151)** and is listening on 5057 → the startup DI seed block (incl. `NotificationEventSeed.EnsureSeededAsync`) completed **without exception**; empty catalog ⇒ silent no-op | PASS |

**Not exercised live (the note):** authenticated JSON round-trip showing `sourceType`/`ownerArea`/`targetRoute` in the DTO, and visual confirmation of the 3 read-only Details fields. Compensated by: mapping compiles + 1148/0 tests (incl. DTO/mapping) + 302/403 proving the serialization/auth pipeline is intact up to the auth gate.

## 4. Regression evidence
- Existing FU03 manifest-driven behavior **unchanged**: Manifest branch of the sync service is byte-for-byte preserved; only additive `SourceType=Manifest` on create + a pre-persistence clobber-guard were added. Covered by the 7 pre-existing FU03 tests (still green) inside the 1148/0 suite.
- `active-template-slots` remains **source-agnostic** (`ListActiveAsync` unchanged; new test asserts Manifest + PlatformSeed Active events both returned).
- Backward-compat: `SourceType` default `Manifest (0)`; existing Mongo docs with no `SourceType` field deserialize to Manifest — **no migration**. `null→empty` coalesce preserved.
- Full suite **1148/0** — no regressions from the additive change.

## 5. Scope / guardrail verification (grep + git + file inspection)
| Guardrail | Result |
|---|---|
| `tenant.user.invited` seed content | **NOT added** — appears only in explanatory comments; not in code |
| `tenant.lifecycle.suspended` seed content | **NOT added** — comments only |
| `tenant.lifecycle.reactivated` seed content | **NOT added** — comments only |
| `NotificationEventSeedCatalog.PlatformSeedDefinitions` | **`Array.Empty<…>()`** (empty; FU04A fills) |
| `QueueEmailNotificationCommand` eventCode adapter | **NOT added / not modified** |
| `TenantManagementManifestProvider` | **NOT created** |
| `IModuleManifestProvider` (new) | **NOT added** — referenced only in a "uses NO IModuleManifestProvider" comment; seeder is outside `INotificationEventManifestSyncService` |
| Module Catalog `tenant-management` entry / `ModuleCatalogSeed` | **UNMODIFIED** (git clean) |
| ModulePages `TENANTS` | **NOT written** |
| `PlatformNavigationCatalog.cs` | FU03A added **nothing**; its only working-tree diff is the **pre-existing FU03** NotificationEvents nav entry (already `M` at session start) — no FU03A/SourceType/PlatformSeed content |
| Gateway `ocelot.json` / `Diten.ApiGateway` | **UNMODIFIED** (git clean) |

## 6. Layer rule (§5.1) — re-confirmed
The startup seed (`NotificationEventSeed`, Infrastructure) performs **no `HasPermissionReflector` reflection** and takes **no dependency on Platform.API**. Policy-gated seeds (`RequiredPermissionKey=null` + `RequiredPolicy`) go Active without reflection; permission-gated seeds are written **Draft** for an API-side activation pass. Enforced in `NotificationEventSeedPlanner.Validate` and covered by tests.

## 7. Remaining follow-ups
- **FU04A — tenant seed content:** add the 3 tenant events (`tenant.user.invited`, `tenant.lifecycle.suspended`, `tenant.lifecycle.reactivated`) to `NotificationEventSeedCatalog.PlatformSeedDefinitions` + template alignment + startup wiring. Foundation is ready; FU04A no longer touches the FU03 contract.
- **FU04B — runtime eventCode dispatch:** `eventCode → defaultTemplateKey → QueueEmailNotificationCommand` adapter (out of FU03A/FU04A scope).
- **API-side activation pass** for permission-gated seeds (only needed once a permission-gated SystemSeed exists; policy-gated tenant events do not need it).
- **Authenticated DTO/UI smoke** (the note in §3) — exercise once a seeded restricted-actor or the FU04A content lands.

## 8. Final decision
MOD-0027-FU03A is **PASS (with note)**: the SourceType/PlatformSeed foundation (enum + additive fields + clobber-guarded sync + generic validation/seed machinery + additive read-only DTO/UI) is built, unit-proven (**1148/0**, incl. 10 new tests), live-boot-clean (fleet up, seed no-op, no 500s), and free of any forbidden side-effect (no tenant content, no runtime adapter, no Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway change). The note is the un-exercised authenticated DTO/UI round-trip, compensated by tests + clean startup + endpoint probes. **FU04A is now unblocked** (its "FU03A implement + merge + regression gate" dependency is satisfied at the code level).
