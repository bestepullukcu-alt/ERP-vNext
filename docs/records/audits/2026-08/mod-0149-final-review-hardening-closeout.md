# MOD-0149 — Final Review / Hardening Closeout — Customer 360 / Account Hierarchy

**Date:** 2026-07-17 · **Verdict:** **PASS** — MOD-0149 Account Foundation is implemented and **Review-ready**. Backend + Gateway + reference-data + frontend compact vertical + catalog/navigation/permission hardening + browser golden flow and failure paths all re-verified live. No release blockers; remaining items are non-blocking follow-ups.

## Scope

Final closeout only — no new feature, no Contact/Consent/Territory/Visit/Lead/Opportunity/Campaign/Segment, no scope
widening, no Zone/MicroZone/Territory/SalesRep field, no hardcoded fallback, no CRM local seed, no fake readiness.

## Final verification

| Area | Evidence | Result |
|---|---|---|
| CrmService build | `Diten.CrmService.Api` 0 errors | ✅ |
| CrmService tests | 19/19 | ✅ |
| Gateway build | `Diten.ApiGateway` 0 errors | ✅ |
| Platform build | `Diten.Platform.API` 0 errors | ✅ |
| Web build | `Diten.Web` 0 errors | ✅ |
| Compact verifier | 94 / 0 PASS | ✅ |
| RESX parity | 7 languages, 52 keys each | ✅ |
| Health | Auth 5056 / Platform 5057 / Gateway 5000 / CRM 5061 = 200 (Web 5001 has no /health; login 200) | ✅ |

## Backend / Gateway / Reference proof

| Check | Evidence | Result |
|---|---|---|
| Gateway route | `/api/crm/accounts` + `/api/crm/accounts/{everything}` in ocelot.json | ✅ |
| account-type published-values | 9 (organization…other) | ✅ |
| account-status published-values | 5 (draft…archived) | ✅ |
| No hardcoded fallback | controller returns [] + dependency message when a set is unavailable | ✅ |
| Location sets (operator-published) | country 15 / city 81 / district 9 (Edirne starter) / account-category 6 | ✅ |

## Frontend golden flow proof (tenant-97c5 CRM Admin, live cookie session)

| Step | Evidence | Result |
|---|---|---|
| `/CRM/Accounts` renders | 200 | ✅ |
| List via Gateway | 200 (cookie→Bearer) | ✅ |
| Create, AccountCode empty | 302 → **ACC-2026-000006** (auto `ACC-{YYYY}-{NNNNNN}`) | ✅ |
| Details/360 | 200 (address + `MOD-0151` coverage placeholder) | ✅ |
| Menu item | static tenant-shell `<li>` gated by `crm.account.read` | ✅ |

## Catalog / navigation / permission proof

| Object/Route | Expected | Observed | Status |
|---|---|---|---|
| CRM catalog Origin | SelfRegistered | `SelfRegistered` | ✅ |
| CRM IsTenantAssignable | true | `true` | ✅ |
| Page descriptor route | /CRM/Accounts | `/CRM/Accounts` | ✅ |
| Descriptor permission | crm.account.read | `crm.account.read` | ✅ |
| IsNavigationVisible | false (no double-menu) | `false`; dynamic nav excludes CRM | ✅ |
| AccountsController guards | per-action | 8/8 actions guarded (`RequirePage`/`RequireJson`) | ✅ |
| Authorized 97c5 admin | 200 | `200` | ✅ |
| Unauthenticated | 302 login | `302 /account/login` | ✅ |
| Non-tenant / permission-less | 403 | `403` | ✅ |

## Failure path proof

| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| Duplicate AccountCode | 409 friendly | re-render + "already exists for this tenant." | ✅ |
| Invalid AccountType | 400 friendly | "not a valid published value of reference set 'account-type'." | ✅ |
| Unknown / cross-tenant id | 404/redirect | Details → 302 → Index | ✅ |
| Soft-delete reload | hidden | DELETE 200 → Details 302 | ✅ |

## Boundary / SoR proof

| Object/Capability | Owner | Evidence | Status |
|---|---|---|---|
| Account master | MOD-0149 | `services/Diten.CrmService` Account entity | ✅ |
| Contact / Consent / Territory / Visit / Lead / Opportunity / Campaign / Segment | later modules | none implemented | ✅ absent |
| ZoneId/MicroZoneId/TerritoryId/SalesRepId | MOD-0151 | absent from entity + form + view (only "NEVER persisted" comment) | ✅ |
| Coverage/Territory | MOD-0151 | Details shows read-only "MOD-0151" placeholder | ✅ |
| Reference values | MOD-0048/PSS-012 | consumed via Gateway published-values | ✅ |
| Permission engine | MOD-0018 | `crm.account.*` keys | ✅ |
| MVC permission guard | frontend standard | inline `PermissionClaims` (matches 16 controllers) | ✅ |
| Page descriptor | Platform catalog | self-registration manifest (like Organization/Workflow) | ✅ |
| Audit | seam/logging only | no separate audit store | ✅ |
| Country/City/District | MOD-0048 | published via governance API; **no CRM local seed** | ✅ |
| Direct 5061 browser call | — | none | ✅ absent |
| crm.account.360.read | — | none | ✅ absent |
| CrmService forced manifest | — | provider is Platform-side | ✅ absent |
| Tenant isolation | Platform | 97c5-scoped throughout | ✅ preserved |

## Validation commands

| Command | Result | Notes |
|---|---|---|
| build CrmService.Api | ✅ 0 errors | — |
| test CrmService.Application.Tests | ✅ 19/19 | — |
| build ApiGateway | ✅ 0 errors | — |
| build Diten.Platform.API | ✅ 0 errors | scratchpad output (running instance holds bin) |
| build Diten.Web | ✅ 0 errors | scratchpad output |
| compact verifier | ✅ 94/0 | — |
| RESX parity (7 lang) | ✅ 52/52 each | — |
| published-values account-type/status | ✅ 9 / 5 | — |
| module-catalog query | ✅ Origin=SelfRegistered, IsTenantAssignable=true | — |
| page-descriptor query | ✅ /CRM/Accounts, crm.account.read, nav-visible=false | — |
| permission guard smoke | ✅ 200 / 302 / 403 | — |
| browser golden flow + failure paths | ✅ all pass (ACC-2026-000006) | — |

## Out-of-scope guard

| Forbidden Item | Found? | Status |
|---|---|---|
| ZoneId/MicroZoneId/TerritoryId/SalesRepId | No (only comment) | ✅ |
| crm.account.360.read | No | ✅ |
| direct 5061 in frontend | No | ✅ |
| _CreateEditOffcanvas / _DetailsQuickView | No | ✅ |
| hardcoded account-type/status fallback | No | ✅ |
| CRM local reference seed | No (source clean; only compiled-binary false positives) | ✅ |
| Action with only `[Authorize]` | No (8/8 guarded) | ✅ |
| fake page/module descriptor | No (real reconcile + real descriptor) | ✅ |

## Open follow-ups (none block release)

| Item | Severity | Owner | Blocks Release? | Notes |
|---|---|---|---|---|
| MOD-0285 nav migration (nav-visible=true + remove static `<li>`) | Medium | frontend/platform | No | double-menu already prevented via nav-visible=false |
| Catalog `Service=DITENPPMSERVICE` legacy value | Low | operator | No | SOFT/operator-owned; manifest can't overwrite on re-push |
| Module-code `CRM` vs permission-module `crm.account` naming | Low | platform gov | No | cosmetic; descriptor permission is authoritative |
| import/export endpoints | Low | MOD-0149 | No | deferred |
| MOD-0021 audit HTTP wiring | Low | MOD-0021 | No | logging seam today |
| Platform `MatchesScope` unit tests | Low | Platform | No | fix already live-proven |
| watch-diten-bg.ps1 hot reload | Low | user/ops | No | Web+Platform currently standalone `dotnet run`; re-run fleet to restore watch |
| Per-permission live discrimination test | Low | QA | No | needs a read-but-not-create tenant user |
| Dev residue (orphan `account_code_sequences`, leftover test accounts ACC-…000003–000005) | Low | dev | No | harmless; no hand-edit |
| External Reference gap (SourceSystem dropdown + multiple refs + lookup endpoint) | Medium | MOD-0149 | No | see external-reference-gap.md |

## Registry / status transition

- **Previous:** Backend+Frontend, 92%
- **New:** **Review-ready, 95%**
- **Reason:** Full vertical (backend + gateway + reference + frontend compact + catalog/nav/permission hardening) and browser golden flow + failure paths re-verified live; boundary clean; only non-blocking follow-ups remain.

## Verdict: PASS

MOD-0149 Customer 360 / Account Hierarchy is implemented and **Review-ready**. Next: land the minor follow-ups in a backlog, plan MOD-0285 nav migration separately, then start MOD-0150 Contact & Relationship Management module-pack authoring (W-3). Credentials used as runtime input only, masked, never persisted; no Mongo hand-edit, no SoD bypass, no tenant-isolation weakening.
