# PSS · MOD-0027-FU04B EventCode Dispatch Adapter — Smoke & Closeout Audit

- **Module:** MOD-0027-FU04B — EventCode Dispatch Adapter (parent MOD-0027)
- **Domain:** Platform & Shared Services (PSS) · **Service:** Diten.Platform · **Shell:** none (adapter/contract only)
- **Date:** 2026-07-08
- **Verification type:** Unit "proof" (fake IMediator + fake event repo) + build/test static evidence + live fleet boot (no 500 / DI clean)
- **Final status:** **PASS (with note)** — the adapter resolves eventCode → Active event → defaultTemplateKey, validates, and delegates to the existing `QueueEmailNotificationCommand` (13/0 tests, 1166/0 suite, Platform.API 0 error). The note: the **unit proof only covers resolution + delegation**; real template render/provider send is owned by the existing FU02 `QueueEmailNotificationHandler` and was not exercised end-to-end live.
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0027-FU04B-eventcode-dispatch-adapter.md`
- **Depends on:** [MOD-0027-FU04A (completed)](pss-mod-0027-fu04a-tenant-management-notification-event-opt-in-smoke-2026-07-08.md) · [FU03A](pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md) · [FU03](pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md)

## 1. Scope summary
FU04B adds a **generic Application-layer adapter** so producers can start a notification by canonical `eventCode` instead of a raw `templateKey`. **Adapter only** — it resolves + validates + **delegates to the existing pipeline** (no new pipeline/provider/template/tracking model, no producer touched). Producer migration is **separate** (FU04B-Tenant, FU04D).

## 2. Adapter structure
| Element | File |
|---|---|
| `INotificationEventDispatchAdapter` + `NotificationEventDispatchRequest` + `NotificationEventDispatchAdapter` | `…/Application/Features/Notifications/Services/NotificationEventDispatchAdapter.cs` (new) |
| `DispatchNotificationByEventCodeCommand` (thin MediatR wrapper) | `…/Features/Notifications/Commands/DispatchNotificationByEventCodeCommand.cs` (new) |
| `DispatchNotificationByEventCodeHandler` (delegates to adapter, no logic) | `…/Handlers/CommandHandlers/DispatchNotificationByEventCodeHandler.cs` (new) |
| DI registration (`INotificationEventDispatchAdapter` scoped) | `…/Application/DependencyInjection.cs` (edited, 1 line + comment) |
| Tests (13) | `…/tests/…/Notifications/NotificationEventDispatchAdapterTests.cs` (new) |

**Flow:** `eventCode` → `GetByEventCodeAsync` → Active check → `DefaultTemplateKey` resolve → RequiredVariables validate → recipient check → build `QueueEmailNotificationRequest` → `IMediator.Send(QueueEmailNotificationCommand)` → return `Response<NotificationDispatchDto>` **unchanged**. Template EXISTENCE is left to the existing handler (`GetBestActiveByKeyAsync`: tenant → platform-default → neutral-locale fallback), so the adapter does not double-check it.

## 3. Failure path evidence (13 tests, all controlled — mediator NEVER called on failure)
| Reason code | HTTP | Verified |
|---|---|---|
| INVALID_EVENT_CODE | 400 | ✅ mediator not called |
| EVENT_NOT_FOUND | 404 | ✅ |
| EVENT_NOT_ACTIVE | 409 | ✅ Draft / Deprecated / Archived (Theory ×3) |
| TEMPLATE_KEY_MISSING_OR_INVALID | 422 | ✅ |
| REQUIRED_VARIABLE_MISSING | 422 | ✅ missing names returned (`Reason`, `SuspendedAtUtc`) |
| RECIPIENT_MISSING | 400 | ✅ empty To |
| PROVIDER_FAILURE (from handler) | 400 | ✅ passed through unchanged |
| SUCCESS (from handler) | 201 | ✅ `Response<NotificationDispatchDto>` returned unchanged; optional variables pass through |

## 4. 3 tenant event proof (Theory — correct resolution + delegation)
| EventCode | → DefaultTemplateKey | Result |
|---|---|---|
| tenant.user.invited | tenant.invite.email | ✅ mediator received `QueueEmailNotificationCommand` with the correct templateKey |
| tenant.lifecycle.suspended | tenant.suspended.email | ✅ |
| tenant.lifecycle.reactivated | tenant.reactivated.email | ✅ |

## 5. Build / test evidence
| Gate | Result |
|---|---|
| NotificationEventDispatchAdapterTests (filtered) | **13 passed / 0 failed** (re-confirmed at closeout) |
| Diten.Platform.Application tests (full) | **1166 passed / 0 failed / 0 skipped** (1153 + 13) |
| `dotnet build Platform.API` | **0 error** |
| `Diten.Web` | **Not affected** — FU04B touched only Application (adapter/DI); no UI/view/JS/resx change → regression-only (prior 0-error build stands) |
| Live fleet rebuild (`watch-diten-bg.ps1`) | see §6 |

## 6. Live smoke evidence
Fleet restarted (`watch-diten-bg.ps1`) — all services rebuilt & listening.

| Check | Evidence | Result |
|---|---|---|
| Platform.API boots with new DI | Platform reached `PlatformPermissionAutoRegistrationWorker` (Total=151) and is listening on 5057 → the DI container built with the new `INotificationEventDispatchAdapter` scoped registration **without exception** | PASS |
| App healthy (no 500) | Web `/Platform/NotificationEvents` **302** (login redirect); API `…/events` + `…/template-slots` **403** (auth enforced, wired) | PASS |
| Existing handler behavior preserved | notification endpoints still 403 (unchanged); `QueueEmailNotificationHandler` untouched | PASS |
| FU04A seed intact | Mongo PlatformSeed total = **3** (FU04B did not disturb the tenant events) | PASS |

**Adapter is an internal Application service (no HTTP endpoint), so there is no direct adapter smoke URL.** Its live evidence is the clean DI boot + preserved endpoints. **Real end-to-end render/send was NOT exercised live** — there is no eventCode dispatch trigger yet (no endpoint, no producer calls the adapter in FU04B). That is the note in §8 and a FU04B-Tenant/FU04D follow-up.

## 7. Scope / guardrail verification (git + grep)
| Guardrail | Result |
|---|---|
| FU04B changed files | DI (`DependencyInjection.cs`, +1 line) + 4 new files (adapter, command, handler, tests) — **nothing else** |
| `QueueEmailNotificationCommand` (eventCode field / behavior) | **UNMODIFIED** — 0 eventCode references; no field added |
| `QueueEmailNotificationHandler` | **UNMODIFIED** |
| Producers (`AdminUserInvitationService`, `TenantLifecycleNotificationConsumer`, `TenantCreatedV1/SuspendedV1/ReactivatedV1` mappers) | **UNMODIFIED** (git) — no producer migration |
| `NotificationTemplateSeed` / `NotificationEventSeedCatalog` (still exactly 3) | **UNMODIFIED** |
| `ModuleCatalogSeed` / ModulePages / `PlatformNavigationCatalog` / Gateway (`ocelot.json`) | **UNMODIFIED** (git; `PlatformNavigationCatalog` M is the pre-existing FU03 nav entry, not FU04B) |
| New dispatch pipeline / provider / tracking model / template | **NONE** — adapter has no `new NotificationDispatch` / `SendEmailAsync` / `new NotificationTemplate` (grep); it only delegates |
| Workflow / document / import wiring | **NONE** |

## 8. Note — unit proof scope (important)
The unit "proof" uses a **fake IMediator + fake event repository**. It proves **only** `eventCode → Active event → DefaultTemplateKey` **resolution + validation + delegation** (that the correct `QueueEmailNotificationCommand` is sent with the right templateKey/variables/recipients, and the handler's result is returned unchanged). **Real template render and provider send are NOT proven here** — they remain owned by the existing FU02 `QueueEmailNotificationHandler` / dispatch pipeline (unchanged). End-to-end render/send is an optional live-smoke follow-up.

## 9. Final decision
MOD-0027-FU04B is **PASS (with note)**: the eventCode dispatch adapter is built, unit-proven (**13/0**, all failure paths + 3 tenant-event resolution + passthrough), regression-clean (**1166/0**, Platform.API 0 error), and free of any forbidden side-effect (no producer migration, no `QueueEmailNotificationCommand/handler` change, no new pipeline/provider/template, no Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway change). The note is the un-exercised end-to-end render/send (owned by FU02 handler; optional live smoke).

## 10. Remaining follow-ups
- **FU04B-Tenant — tenant producer migration:** wire `AdminUserInvitationService` → `tenant.user.invited`; `TenantLifecycleNotificationConsumer`/`TenantSuspendedV1`/`TenantReactivatedV1` → `tenant.lifecycle.*` through this adapter (changes producer behavior — separate careful pack).
- **FU04M — manifest-driven workflow/document/import event opt-in:** producers declare `notificationEvents` in their manifests (each producer's own pack).
- **FU04D — producer runtime wiring:** end-to-end eventCode-driven send once producers adopt the adapter.
- **Authenticated / live render smoke:** exercise a real eventCode dispatch end-to-end (template render + provider) with a PlatformActor context, if desired.
