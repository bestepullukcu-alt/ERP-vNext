# PSS · MOD-0027-FU04A Tenant Management Notification Event Opt-in — Smoke & Closeout Audit

- **Module:** MOD-0027-FU04A — Tenant Management Notification Event Opt-in (parent MOD-0027)
- **Domain:** Platform & Shared Services (PSS) · **Service:** Diten.Platform · **Shell:** none (seed content only)
- **Date:** 2026-07-08
- **Verification type:** Live fleet (dotnet watch rebuild + startup seed) + **Mongo live persistence check** + build/test static evidence
- **Final status:** **PASS (with note)** — the 3 tenant events are seeded **Active / PlatformSeed** into the live catalog with the exact contract (Mongo-verified), tests 1153/0, app healthy. The note: the authenticated API JSON list/slots fetch was not exercised live (no restricted-actor token) — compensated by the Mongo persistence proof + unit tests.
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0027-FU04A-tenant-management-notification-event-opt-in.md`
- **Depends on:** [MOD-0027-FU03A bridge (completed)](pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md) · **Related:** [MOD-0027-FU03 smoke](pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md)

## 1. Scope summary
FU04A adds the 3 Platform Admin fixed-page tenant events to the Notification Event Catalog as **PlatformSeed** content — **without** touching Module Catalog / IModuleManifestProvider / PlatformNavigationCatalog. It reuses the FU03A generic seed foundation (planner + seeder + clobber guards + startup wiring); **no foundation code changed**. Runtime `eventCode → dispatch` is **out of scope** (FU04B).

**Changed files (exactly 2):**
| File | Change |
|---|---|
| `…/Application/Features/Notifications/Services/NotificationEventSeeding.cs` | `NotificationEventSeedCatalog.PlatformSeedDefinitions` filled from empty → 3 tenant `NotificationEventSeedDefinition` (shared `TenantEvent(...)` helper) |
| `…/tests/…/Notifications/NotificationEventCatalogTests.cs` | +5 FU04A tests |

*(No DI change — FU03A already wired `NotificationEventSeed.EnsureSeededAsync` in both startup seed blocks; the now-non-empty catalog seeds automatically.)*

## 2. Seeded events (live — Mongo `diten_personalization_dev.notification_event_definitions`)
| EventCode | SourceType | Status | DefaultTemplateKey | Severity | RequiredVariables | RequiredPolicy | RequiredPermissionKey | TargetRoute · OwnerArea |
|---|---|---|---|---|---|---|---|---|
| tenant.user.invited | PlatformSeed (1) | **Active** (1) | tenant.invite.email | Info | TenantDisplayName | PlatformActor | null | /Platform/Tenants · PlatformAdmin |
| tenant.lifecycle.suspended | PlatformSeed (1) | **Active** (1) | tenant.suspended.email | Warning | TenantDisplayName, Reason, SuspendedAtUtc | PlatformActor | null | /Platform/Tenants · PlatformAdmin |
| tenant.lifecycle.reactivated | PlatformSeed (1) | **Active** (1) | tenant.reactivated.email | Success | TenantDisplayName, ReactivatedAtUtc | PlatformActor | null | /Platform/Tenants · PlatformAdmin |

Common: `OwnerModuleId=MOD-0009`, `OwnerDisplayName=Tenant / Environment Management`, `Channel=Email`, `CanTenantOverride=true`, `UsageType=SystemEvent`. Templates are the **existing FU02-seeded** ones — no new template created.

## 3. Build / test evidence
| Gate | Result |
|---|---|
| `dotnet watch` rebuild on FU04A save | **Platform.API rebuilt & restarted with 0 build error** (self-registration + startup seed ran clean) |
| `dotnet build Platform.API` | 0 error (watch rebuild is live proof) |
| `dotnet build Diten.Web` | Unaffected (FU04A touched only Application) — prior 0-error build stands |
| NotificationEventCatalogTests (filtered) | **22 passed / 0 failed** (17 FU03/FU03A + **5 new FU04A**) |
| Diten.Platform.Application tests (full) | **1153 passed / 0 failed / 0 skipped** |

**New FU04A tests (5):** catalog contains exactly 3 tenant defs (+ field assertions) · 3 events seed as Active (policy-gated, templates resolve) · idempotent (2nd run 0 created / 3 updated / no duplicate) · `active-template-slots` includes the 3 tenant events · tenant seed does **not** clobber a Manifest record on code collision.

## 4. Live smoke evidence
| Check | Evidence | Result |
|---|---|---|
| App healthy after seed | Web `/Platform/NotificationEvents` **302** (login redirect, no 500); API `…/events` + `…/template-slots` **403** (auth enforced, wired, no 500) | PASS |
| 3 events Active + PlatformSeed | Mongo: all 3 `SourceType=1, Status=1` with correct OwnerArea/TargetRoute/RequiredPolicy/RequiredPermissionKey/Template | PASS |
| Total PlatformSeed count | **exactly 3** | PASS |
| No duplicate | per-code count = 1 for each (idempotent across multiple watch restarts) | PASS |
| Startup seed exceptions | none — Platform booted through startup + `PlatformModuleSelfRegistrationWorker` and serves requests | PASS |

**Authenticated smoke note:** the API JSON list/`template-slots` payload (showing the 3 events + `sourceType`/`ownerArea`/`targetRoute`) was **not** fetched live — no seeded restricted-actor token, SuperAdmin auto-passes, and the login flow was not exercised. Compensated by: **Mongo live persistence** (§2) + the `Active_template_slots_include_the_three_tenant_events_after_seed` unit test (proves `ListActiveAsync` → slots returns the 3) + the DTO/mapping additive fields (FU03A) covered by tests.

## 5. Scope / guardrail verification (grep + git)
| Guardrail | Result |
|---|---|
| Files touched by FU04A | **only** `NotificationEventSeeding.cs` (catalog content) + `NotificationEventCatalogTests.cs` |
| Runtime eventCode dispatch adapter | **NOT added** (FU04B) |
| `QueueEmailNotificationCommand` | **UNMODIFIED** |
| `TenantManagementManifestProvider` / `IModuleManifestProvider` | **NOT added** (grep: comment-only references) |
| Module Catalog `tenant-management` / `ModuleCatalogSeed` | **UNMODIFIED** |
| ModulePages `TENANTS` / descriptors | **NOT written** |
| `PlatformNavigationCatalog.cs` | **NOT modified by FU04A** (its working-tree `M` is the pre-existing FU03 nav entry, present since session start) |
| Gateway `ocelot.json` / `Diten.ApiGateway` | **UNMODIFIED** |
| Workflow / document / import event codes | **NONE added** (grep clean) |
| New notification template | **NOT created**; existing FU02 templates reused (`NotificationTemplateSeed` untouched) |
| Tenant producer/template/consumer flow | **UNMODIFIED** (`TenantCreatedV1/SuspendedV1/ReactivatedV1` mappers untouched) |

## 6. Final decision
MOD-0027-FU04A is **PASS (with note)**: the 3 tenant events are declared as PlatformSeed and **live-seeded Active** into the catalog with the exact policy-gated contract (Mongo-verified, exactly 3, no duplicate), all via the FU03A foundation with **zero forbidden side-effect** (no runtime adapter, no manifest provider, no Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway change, no new template). Tests **1153/0** (5 new). The note is the un-exercised authenticated API payload fetch, compensated by Mongo live proof + unit tests.

## 7. Remaining follow-ups
- **FU04B — runtime eventCode dispatch adapter:** `eventCode → active event → defaultTemplateKey → QueueEmailNotificationCommand` (still OPEN; FU04A is catalog visibility only).
- **Tenant producer migration:** move the tenant producers from hard-coded templateKey to eventCode dispatch (rides on FU04B).
- **Manifest-driven Workflow/document/import event opt-in:** producers declare `notificationEvents` in their manifests (each producer's own pack).
- **FU04D — producer runtime wiring:** end-to-end eventCode-driven send once FU04B lands.
- **Authenticated API/UI smoke:** fetch the list/slots JSON + visual `/Platform/NotificationEvents` confirmation with a PlatformActor token (closeout tail).
