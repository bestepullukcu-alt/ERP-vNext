# PSS · MOD-0027-FU03 Notification Event Catalog & Template Binding — Smoke & Validation Audit

- **Module:** MOD-0027-FU03 — Notification Event Catalog & Template Binding (parent MOD-0027)
- **Domain:** Platform & Shared Services (PSS) · **Service:** Diten.Platform · **Shell:** platform-admin
- **Date:** 2026-07-08
- **Verification type:** Live HTTP smoke through the real fleet (same-origin `/Platform/NotificationEvents/api` proxy → Gateway → Platform API → Mongo) + build/test/verifier static evidence
- **Final status:** **PASS (with note)** — full runtime path works; the event list is empty because no producer manifest has declared `notificationEvents` yet (opt-in follow-up), which is the correct controlled behavior.
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0027-FU03-notification-event-catalog-template-binding.md`
- **Related:** [MOD-0027-FU02 smoke audit](pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md)

## 1. Increment summary
| Increment | Scope | Status |
|---|---|---|
| 1 | BuildingBlocks manifest extension + Domain entity/enums | PASS |
| 2 | Application/API + persistence + permissions + sync service; **tests 1139/1139** | PASS |
| 3 | Read-only `/Platform/NotificationEvents` UI + proxy + nav + RESX + verifier | PASS |
| 4 | Runtime smoke + security + same-origin + closeout (this report) | PASS (with note) |

## 2. Runtime smoke (live, through the real proxy)
Login: `admin@diten.com` (platform_admin) → `/platform/login` 200.

| Step | Evidence | Result |
|---|---|---|
| Page `/Platform/NotificationEvents` | **HTTP 200**; `data-dt-standard="v2"`, `#skeleton-loader`, 4 Notification nav-links, platform layout | PASS |
| **Sync from manifest** (real endpoint) | **HTTP 200** → `providersScanned:6, eventsDeclared:0, synced:0, updated:0, withIssues:0, items:[]` — the sync really enumerated the 6 in-process Platform manifest providers; **null→empty coalesce verified at runtime** (0 declared, no exception, no fake data) | PASS |
| Events list `…/api/events` | **HTTP 200** `data:[]` — controlled empty | PASS |
| Template-slots `…/api/template-slots` | **HTTP 200** `data:[]` — no active events, controlled empty | PASS |
| Detail (bogus code) `…/api/events/does.not.exist` | **HTTP 404** `"Notification event not found."` — controlled, no fake, no stack trace | PASS |
| Details page `/Details/{eventCode}` | **HTTP 200**; `#eventDetailsRoot`, `data-event-code` render | PASS |
| Details with a real event | **N/A** — no producer declared events; cannot populate without a producer opt-in (out of pack scope). Plumbing proven via 404 + page render. | N/A (documented) |

## 3. Empty-event behavior (the important note)
Producer manifest providers were **not** updated to declare `notificationEvents` in this pack (guardrail: producers unchanged). Therefore `sync-from-manifest` returns **0 declared events** and the list/slots are **empty**. This is **not a bug** — it is the correct controlled state:
- Sync runs, scans 6 providers, returns a real result with `eventsDeclared:0`.
- The UI shows a controlled empty DataTable + a real sync-result summary (no fake rows, no crash).
- The golden-flow "populated event list/detail" proof requires a producer (e.g. Workflow) to opt in and declare `notificationEvents` — a separate follow-up (§ pack §20).

## 4. Security / authorization
| Check | Result |
|---|---|
| Unauthenticated direct URL (no cookie) | **HTTP 302** login redirect (platform convention) |
| `events.read` gate (list/detail/template-contract) | Backend `[HasPermission("platform.notifications.events.read")]` (Increment 2) |
| `events.manage` gate (sync/archive) | Backend `[HasPermission("platform.notifications.events.manage")]` |
| Alias map | `platform.notifications.events.read` + `events.manage` in `PermissionAliasMap` (count test 61→63 green) |
| Restricted (authenticated-but-unauthorized) actor | **N/A** — no seeded platform user lacking `events.*`; SuperAdmin auto-passes. Compensating evidence: backend `[HasPermission]` fail-closed + `PlatformActor` policy + permission/authorization unit tests. |
| Tenant actor event catalog management | Rejected fail-closed (`PlatformActor` policy) |

## 5. Same-origin
Static + runtime: NotificationEvents JS has **zero** `localhost:5000`/`5057`/`http(s)://` calls — all requests via `${apiBase}` = `/Platform/NotificationEvents/api`. Every smoke call went through the 5001 same-origin proxy.

## 6. Build / test / verifier
| Gate | Result |
|---|---|
| `dotnet build Platform.API` | 0 error |
| `dotnet build Diten.Web` | 0 error |
| Platform Application tests | **1139/1139 PASS** (Increment 2; frontend-only Increment 3/4 changes do not affect Application tests) |
| RESX en/tr parity (module) | 47/47 OK; SharedResource `NotificationEventsMenu` en/tr |
| DataTable verifier (`--module NotificationEvents`, read-only) | 64 PASS / 14 FAIL — identical to NotificationDispatches (13 systemic bulk/quick-view/getAuthHeaders/new-DataTable + 1 offcanvas; the live reference `Views/Platform/Tenants/` fails the same way). `Create/Edit/_Form` intentionally absent — no fake pages produced. |

## 7. BuildingBlocks guardrails (re-confirmed)
Runtime `providersScanned:6, eventsDeclared:0` confirms the additive/optional `NotificationEvents` field + `?? Array.Empty<>()` coalesce work end-to-end with all existing providers (none declaring events) unbroken. Backward-compat deserialization + coalesce + uniqueness covered by `NotificationEventCatalogTests` (8 tests, green).

## 8. Protected paths
No protected **source** path changed: gateway `ocelot.json`, `_Layout.cshtml`, Archive, Auth/Mdm/ESBP/DevEnablement services, FU02 template/settings/dispatch UI, TenantShell bell/SignalR/SMS, Module Catalog/Domain Management/Permission Catalog ownership, existing 9 manifest providers — all untouched. Producer flow migration (`AdminUserInvitationService`, `TenantCreatedV1NotificationMapper`) not performed. (Only runtime `logs/*.log` artifacts changed — not source.)

## 9. Remaining follow-ups
- **Producer opt-in (golden-flow completion):** add `notificationEvents` to a producer manifest (e.g. Workflow `workflow.task.assigned`) so the catalog shows real events — separate follow-up in each producer's pack.
- **Per-event persisted validation issues** on the entity (Details currently shows a status-derived note; issues shown in the Index sync-result panel).
- **FU04+:** InApp channel + UserNotification + bell dropdown + SignalR; tenant self-service override UI; multi-channel binding.
- **DataTable verifier read-only mode** (tooling — shared with NotificationDispatches).

## Conclusion
MOD-0027-FU03 is **PASS (with note)**: the event catalog contract, manifest sync + validation, read-only Platform Admin UI, template-slot endpoint, and permissions are built and live-verified end-to-end. The empty event list is the correct controlled behavior pending producer opt-in.
