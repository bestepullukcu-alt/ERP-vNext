# PVG Control Tower Development Plan - 2026-08-13

> This plan continues PVG from the current measured state. It supersedes the execution posture in
> `docs/plans/pvg-fast-track-execution-plan-2026-08-09.md` where that plan assumes WP-01 has not started.
> It does not approve operational runtime.

## 1. Planning Basis

Control Tower method:

- Use the supplied Control Tower SOP and Operating Card as the daily gate model.
- Treat code/runtime reality as evidence, not authority.
- Keep `DCP-004` and member module packs as the execution authority.
- Separate Agent Lane workspaces by writer scope.
- Close every Control Tower turn with replan + next-work calculation.

Authority baseline:

| Artifact | Current state |
|---|---|
| `DCP-004` | `approved`; build/test only; operational runtime `NO-GO` |
| MOD-0230 | `ready-for-dev`; build/test gate open; operational runtime closed |
| MOD-0231 | `ready-for-dev`; non-operational class-library contracts/tests only |
| MOD-0232 | `ready-for-dev`; non-operational class-library contracts/tests only |
| MOD-0234 | `ready-for-dev`; no-shell class-library contracts/tests only |

Measured implementation baseline:

| Area | Baseline |
|---|---|
| REG-PV-BASE ports / MOD-0230 guardrail contracts | Source present; latest tests passed: 38 |
| MOD-0231 Case Processing contracts | Source present; tests passed: 21 |
| MOD-0232 MedDRA Coding contracts | Source present; tests passed: 27 |
| MOD-0234 Signal Management contracts | Source present; tests passed: 33 |
| MOD-0230 API host and business API route family | Source present; latest API guardrail/negative-contract tests passed: 26 |
| MOD-0230 persistence boundary | Source present; RegPvBase tests passed: 38; in-memory local/dev store only |
| MOD-0230 Gateway route family | Source present; Gateway Ocelot tests passed: 19; approved four-template matrix only |
| MOD-0230 tenant UI | Source present; frontend build passed; static regulated-scope, accessibility, i18n, and regression guardrails present; authenticated browser/runtime smoke passed with safe fail-closed denial |

## 1.1 Status Dashboard

> Percentages are Control Tower delivery tracking estimates for local/dev/CI build-test progress. They are not
> production, supplier qualification, or validation readiness.

| Area | Status | Evidence |
|---|---:|---|
| Governance baseline / DCP-004 | 100% | DCP approved; member build/test gates open |
| PVG class-library contracts/tests | 100% | Latest PVG relevant class-library/test-only validation passed: 159/159; commit `de4fca61` |
| MOD-0230 API host shell (`PVG-0230-BE-01`) | 100% | API build passed; 5 host guardrail tests passed |
| MOD-0230 persistence boundary (`PVG-0230-BE-02`) | 100% | RegPvBase 37 passed; API guardrails 5 passed |
| MOD-0230 CQRS/controller endpoints (`PVG-0230-BE-03`) | 100% | API route/context tests 9 passed; lane report total 127 passed |
| MOD-0230 read-context hardening (`PVG-0230-BE-04`) | 100% | `VER-PVG-0230-READ-CONTEXT-01` PASS; API tests 22/22; prior full local PVG suite 140/140 |
| MOD-0230 read permission/context hardening (`DEV-PVG-0230-BE-05`) | 100% | `VER-PVG-0230-BE05-UI02-01` PASS; API tests 23/23; RegPvBase tests 38/38 |
| MOD-0230 failure-path verification (`PVG-0230-VER-01`) | 100% | E2 passed; E4-ready gaps recorded |
| MOD-0230 Gateway route (`PVG-0230-GW-01`) | 100% | Superseded by GW-02 narrowing after live route-scope gap |
| MOD-0230 Gateway verification (`PVG-0230-GW-VER-01`) | 100% | Correctly failed old broad route and produced GW-02 follow-up |
| MOD-0230 API + Gateway live smoke (`PVG-0230-INT-API-GW-01`) | 70% | E3 runtime smoke passed for health and approved paths; route-scope gap prevents clean acceptance |
| MOD-0230 Gateway route narrowing (`PVG-0230-GW-02`) | 100% | Superseded by GW-03 reserved-segment API guard |
| MOD-0230 narrowed Gateway verification (`PVG-0230-GW-VER-02`) | 100% | Verification completed and correctly failed GW-02 reserved-word-as-ID behavior |
| MOD-0230 reserved segment denial (`PVG-0230-GW-03`) | 100% | API boundary guard returns `404` for reserved IDs; API 21/21 and Gateway 19/19 passed |
| MOD-0230 API bind diagnosis (`PVG-0230-RUN-01`) | 100% | Root cause recorded; explicit Kestrel startup; local health smoke passed on `5011` with elevated socket permission |
| MOD-0230 API + narrowed Gateway live smoke (`PVG-0230-INT-API-GW-02`) | 25% | Previous attempt blocked; superseded by next rerun after GW-03/RUN-01 |
| MOD-0230 API + Gateway rerun (`PVG-0230-INT-API-GW-03`) | 100% | E3 passed; forbidden methods 404 at Gateway; reserved operation names 404 before business route |
| MOD-0230 tenant UI (`PVG-0230-UI-01`) | 100% | Runtime/MVC proxy smoke passed with browser-network caveat |
| MOD-0230 UI verification (`PVG-0230-UI-VER-01`) | 100% | Static + compile verification passed with scoped DataTable exceptions |
| MOD-0230 UI smoke (`PVG-0230-UI-SMOKE-01`) | 65% | Runtime path partially passed; list page failed HTTP 500 before UI-FIX-01 |
| MOD-0230 UI list partial fix (`PVG-0230-UI-FIX-01`) | 100% | Absolute partial paths applied; frontend build passed |
| MOD-0230 UI smoke retry (`PVG-0230-UI-SMOKE-02`) | 100% | E3 MVC proxy path passed; E4-ready forbidden-surface absence with shared-shell caveat |
| MOD-0230 UI controlled-response hardening (`DEV-PVG-0230-UI-02`) | 100% | `VER-PVG-0230-BE05-UI02-01` PASS; Web build passed with existing `NU1900` warning |
| MOD-0230 UI state hardening (`DEV-PVG-0230-UI-03`) | 100% | UI loading/error/empty/in-flight state guardrails pushed |
| MOD-0230 UI static guardrails (`DEV-PVG-0230-UI-04`) | 100% | Accessibility/interaction static guardrails pushed |
| MOD-0230 UI i18n guardrails (`DEV-PVG-0230-UI-05`) | 100% | Seven-resource localization coverage guardrails pushed |
| MOD-0230 negative-surface tests | 100% | API/UI negative-surface tests pushed; latest API tests 26/26 |
| MOD-0230 API negative contracts (`DEV-PVG-0230-BE06-NEGATIVE-CONTRACT-01`) | 100% | Request DTO, route-method, safe reason-code, and fail-closed tests pushed |
| MOD-0230 UI regression tests | 100% | Static UI regression tests pushed |
| PVG deep audit update (`AUD-PVG-DEEP-2026-08-21`) | 100% | Source/gov/static audit complete; plan updated |
| MOD-0231/0232/0234 downstream drift inspection (`INS-PVG-DOWNSTREAM-02`) | 100% | No downstream runtime drift; composition-only DI watch item recorded |
| MOD-0230 staging scope manifest (`VER-PVG-STAGING-SCOPE-02`) | 100% | Exact source-only staging list prepared; no staging performed; `.claude/settings.local.json` excluded |
| MOD-0230 local integration smoke (`PVG-0230-INT-01`) | 100% | E5 local/dev PASS-with-gap superseded by authenticated proxy proof |
| MOD-0230 authenticated MVC proxy proof (`PVG-0230-AUTH-PROXY-01`) | 100% | Tenant login succeeded; Web -> MVC proxy -> Gateway -> PVG API returned safe `PVG_PERMISSION_DENIED` under local/dev deny adapters |
| Operational runtime / production / validation | 0% | Blocked by owner/runtime gates |

Overall MOD-0230 local/dev/CI static/build-test slice estimate: **100% PASS**.
Overall MOD-0230 local/dev runtime proof estimate: **100% local/dev PASS**, with operational runtime still blocked.
Overall PVG operational readiness: **0% / NO-GO**.

## 2. Non-Negotiable Boundaries

- MOD-0230 is the only member that may progress toward a local/dev/CI runtime slice now.
- MOD-0231, MOD-0232, and MOD-0234 stay non-operational class-library contract/test work.
- No production deployment, supplier qualification, validation approval, permission seed, menu entry, module catalog
  registration, background job, archive, void, export, delete, bulk-delete, fake signal, fake metric, fake cohort, or
  AI behavior is authorized.
- No MedDRA term/code/hierarchy fragment may be added to source, docs, fixtures, seeds, tests, or UI.
- `frontend` must call MOD-0230 only through same-origin MVC proxy -> Gateway -> `Diten.PvgService`, never a service
  port directly.
- Operational runtime stays closed even if the local MOD-0230 slice passes.

## 3. Control Tower Lanes

| Lane | Type | Scope | Writer rule |
|---|---|---|---|
| `DEV-PVG-0230-GW-02` | DEV | Narrow Gateway templates/methods to the approved MOD-0230 API surface | Single writer on Gateway config/tests |
| `VER-PVG-0230-GW-02` | VER | Verify narrowed Gateway route surface and forbidden path non-routing | Done; NO-GO finding |
| `DEV-PVG-0230-GW-03` | DEV | Block or disambiguate reserved operation words before `{intakeDraftId}` | Done |
| `DEV-PVG-0230-RUN-01` | DEV/DEBUG | Diagnose PVG API process starts but does not bind `5011` | Done |
| `DEV-PVG-0230-UI-01` | DEV | Tenant Compact UI after Gateway route exists | Implemented; pending browser/runtime acceptance |
| `VER-PVG-0230-UI-01` | VER | Static UI and compile verification | Done |
| `INT-PVG-0230-API-GW-02` | INT | Live local API + narrowed Gateway smoke | Attempted; blocked by PVG API bind failure and GW-03 |
| `INT-PVG-0230-API-GW-03` | INT | Live local API + Gateway smoke after GW-03/RUN-01 | Done |
| `INT-PVG-0230-UI-SMOKE-01` | INT | Browser/local UI smoke through MVC proxy -> Gateway -> PVG API | Attempted; failed list page |
| `DEV-PVG-0230-UI-FIX-01` | DEV | Fix Case Intake/Triage list-page partial path resolution | Done |
| `INT-PVG-0230-UI-SMOKE-02` | INT | Retry browser/local UI smoke after UI-FIX-01 | Done with caveats |
| `DEV-PVG-0230-BE-05` | DEV | Backend read permission/context hardening for list/detail | Done; verified with UI-02 |
| `DEV-PVG-0230-UI-02` | DEV | Tenant UI/proxy controlled-response hardening for `401`/`403`/`409` and same-origin guardrails | Done; verified with BE-05 |
| `DEV-PVG-0230-UI-03` | DEV | UI loading, empty, error, and in-flight state hardening | Done |
| `DEV-PVG-0230-UI-04` | DEV | Static accessibility and interaction guardrails | Done |
| `DEV-PVG-0230-UI-05` | DEV | Static i18n/resource guardrails | Done |
| `DEV-PVG-0230-BE06-NEGATIVE-CONTRACT-01` | DEV/TEST | API tests-only negative-contract hardening | Done |
| `DEV-PVG-0230-UI-REGRESSION-01` | DEV/TEST | UI tests-only regression guardrails | Done |
| `DOC-PVG-DOWNSTREAM-CL-01` | DOC | Record MOD-0231/0232/0234 class-library/test-only downstream hardening evidence | Done |
| `AUD-PVG-DEEP-2026-08-21` | INS/DOC | Deep PVG source/governance audit and plan update | Done |
| `VER-PVG-0230-BE05-UI02-01` | VER | Combined BE-05 + UI-02 source/build/test verification | Done; PASS |
| `VER-PVG-STAGING-SCOPE-02` | VER | Source-only staging manifest before staging | Done; no staging performed |
| `INS-PVG-DOWNSTREAM-02` | INS | MOD-0231/MOD-0232/MOD-0234 contract drift and blockers | Done; composition-only DI watch |
| `INT-PVG-0230-SMOKE-01` | INT | End-to-end local runtime smoke after API/Gateway/UI | Done with auth gap |

Parallelism:

- Backend and downstream inspection can run in parallel.
- Local E5 closeout is complete with one auth-context gap.
- Source-only staging manifest is prepared; use exact path list only and keep `.claude/settings.local.json` excluded.
- Downstream inspection is complete and can be treated as a watch item only.
- Authenticated MVC proxy traversal is recorded complete for local/dev evidence only.
- No two writer lanes touch the same shared runtime surface.

## 4. Work Package Plan

| Seq | WP ID | Profile | Module | Scope | Depends on | Evidence target | Status |
|---|---|---|---|---|---|---|---|
| 0 | `PVG-CT-00` | C | DCP-004 | Re-baseline current class-library evidence and stale work-pack assumptions | None | E1/E2 | Done by audit |
| 1 | `PVG-0230-BE-01` | B | MOD-0230 | Create API host shell for local/dev/CI only: API project, Program, health, DI, config refusal for production-like unauthorized mode | Current class libraries | E2 achieved | Done |
| 2 | `PVG-0230-BE-02` | B | MOD-0230 | Add persistence boundary: entity/repository/index tests, tenant isolation, no client `TenantId`, no delete/bulk/archive/export | BE-01 | E2 achieved; E4-ready | Done |
| 3 | `PVG-0230-BE-03` | B | MOD-0230 | Add CQRS/controller route set: create/update/list/detail/triage/route only, response envelope, RBAC/guardrails/audit intents | BE-02 | E2 achieved | Done |
| 3.1 | `PVG-0230-BE-04` | B | MOD-0230 | Harden list/detail read endpoints to require tenant, actor, and correlation context before service invocation, matching create/update/triage/route | BE-03 | E2 achieved; E4-ready improvement | Done |
| 4 | `PVG-0230-VER-01` | C | MOD-0230 | Failure-path and leak-scan verification: missing policy, workflow denial, evidence pending, tenant mismatch, unsafe text absence | BE-03 | E2 achieved; E4-ready gaps | Done |
| 5 | `PVG-0230-GW-01` | B | MOD-0230 | Add one Gateway route family: upstream `/api/pv-case-intake-triage`, downstream `/api/v1/pv-case-intake-triage` | BE-03 | E2 achieved; live gap found | Done with corrective follow-up |
| 6 | `PVG-0230-GW-VER-01` | C | MOD-0230 | Independently verify Gateway route family and forbidden Gateway route absence | GW-01 | E2 achieved; E3 route-scope gap | Done with gap |
| 7 | `PVG-0230-INT-API-GW-01` | INT | MOD-0230 | Live local API + Gateway smoke for health and routed business paths | GW-01, BE-03 | Partial E3 | Done with gap |
| 8 | `PVG-0230-GW-02` | B | MOD-0230 | Replace broad Gateway catch-all/method set with explicit approved root/detail/triage/route templates and methods only | INT-API-GW-01 gap | Partial E2; NO-GO reserved segment | Done with failed acceptance |
| 9 | `PVG-0230-GW-VER-02` | C | MOD-0230 | Verify `PATCH`, export, archive, void, bulk, and downstream MOD-0231/0232/0234 paths do not route through Gateway | GW-02 | E2 failed clean acceptance | Done; escalated GW-03 |
| 10 | `PVG-0230-UI-01` | A | MOD-0230 | Tenant Compact UI under `Views/Pharmacovigilance/CaseIntakeTriage/**`, 7-language l10n, no delete/export/archive | GW-01 | E4-ready static; browser E4 pending | Done with runtime gap |
| 11 | `PVG-0230-UI-VER-01` | C | MOD-0230 | Verify UI source, l10n, same-origin proxy, no direct service port, no forbidden action exposure, and documented DataTable verifier exceptions | UI-01 | E2/E4-ready achieved | Done |
| 12 | `PVG-0230-INT-API-GW-02` | INT | MOD-0230 | Live local API + Gateway smoke after GW-02 narrowing | GW-02 | E3 not achieved | Attempted; blocked |
| 13 | `PVG-0230-GW-03` | B | MOD-0230 | Reserved-segment denial/disambiguation for `export`, `archive`, `void`, `bulk`, `bulk-delete`, `delete` before `{intakeDraftId}` | GW-VER-02 finding | E2 achieved; E3 pending | Done |
| 14 | `PVG-0230-RUN-01` | B/DEBUG | MOD-0230 | Diagnose why `Diten.PvgService.Api` starts but does not bind `5011`; make minimal local/dev/CI host fix if needed | INT-API-GW-02 failure | E2/E3 achieved | Done |
| 15 | `PVG-0230-INT-API-GW-03` | INT | MOD-0230 | Full live local API + Gateway smoke after GW-03 and RUN-01 | GW-03, RUN-01 | E3 achieved; E4-ready forbidden operations | Done |
| 16 | `PVG-0230-UI-SMOKE-01` | INT | MOD-0230 | Browser/runtime UI smoke through MVC proxy -> Gateway -> PVG API after narrowed Gateway routes | INT-API-GW-03, UI-VER-01 | Partial E3; list page failed | Attempted; failed |
| 17 | `PVG-0230-UI-FIX-01` | B | MOD-0230 | Fix list page partial lookup by using absolute partial paths | UI-SMOKE-01 finding | E2 achieved | Done |
| 18 | `PVG-0230-UI-SMOKE-02` | INT | MOD-0230 | Retry browser/runtime UI smoke after list-page fix | UI-FIX-01 | E3 achieved; E4-ready with caveats | Done |
| 19 | `VER-PVG-STAGING-SCOPE-02` | C | MOD-0230 / DCP-004 | Produce exact source-only staging list excluding local settings/generated output; do not stage | Package audit | E1/E2 manifest | Done |
| 20 | `PVG-0230-INT-01` | INT | MOD-0230 | End-to-end local runtime smoke after API/Gateway/UI | INT-API-GW-03, UI-SMOKE-02 | E5 local/dev PASS-with-gap | Done |
| 21 | `PVG-0230-AUTH-PROXY-01` | INT | MOD-0230 | Prove authenticated MVC proxy traversal with valid tenant-shell auth cookie/token | Valid local tenant context | E3/E4-ready achieved; safe fail-closed denial | Done |
| 22 | `PVG-DOWNSTREAM-WATCH-01` | C | MOD-0231/0232/0234 | Reconfirm downstream modules remain non-operational after GW-03/RUN-01 | GW-03, RUN-01 | E1/E2 achieved | Done |
| 23 | `PVG-0230-DOC-01` | C/DOC | MOD-0230/0231 | Record `MOD0230HandoffReference v0.1 build/test`, future v1 operational decision, and downstream DI composition-only note | BE-01, BE-02, downstream inspection | E1 | Done locally |
| 24 | `DEV-PVG-0230-BE-05` | B | MOD-0230 | Backend read permission/context hardening: list/detail carry tenant + actor + correlation and use `pvg.mod0230.intake.read` before field policy/store access | BE-04 | E2 achieved | Done |
| 25 | `DEV-PVG-0230-UI-02` | A | MOD-0230 | Tenant UI/proxy controlled-response hardening for safe `401`/`403`/`409` display, same-origin proxy guardrails, and l10n completeness | UI-SMOKE-02, BE-05 | E2 achieved | Done |
| 26 | `VER-PVG-0230-BE05-UI02-01` | C | MOD-0230 | Combined read-only verification of BE-05 + UI-02 scope and forbidden-surface absence | BE-05, UI-02 | E2 achieved | Done |
| 27 | `DEV-PVG-0230-UI-03` | A/TEST | MOD-0230 | Harden list/detail/form loading, empty, error, and in-flight UI states with static guardrails | UI-02 | E2 achieved | Done |
| 28 | `DEV-PVG-0230-UI-04` | TEST | MOD-0230 | Add accessibility and interaction static guardrails for alerts, aria-live regions, list load hooks, and disabled-state rules | UI-03 | E2 achieved | Done |
| 29 | `DEV-PVG-0230-UI-05` | TEST | MOD-0230 | Add static i18n/resource guardrails for seven Case Intake/Triage cultures and safe UI state keys | UI-04 | E2 achieved | Done |
| 30 | `DEV-PVG-0230-NEGATIVE-SURFACE-01` | TEST | MOD-0230 | Add API/UI tests proving forbidden delete/bulk/archive/void/export, AI, MedDRA dictionary/import/search, and downstream runtime exposure remain absent | UI-05, BE-05 | E2 achieved | Done |
| 31 | `DEV-PVG-0230-BE06-NEGATIVE-CONTRACT-01` | TEST | MOD-0230 | Add API tests for broader client-tenant field rejection, approved method matrix, and safe fail-closed reason codes across all six endpoints | Negative surface tests | E2 achieved | Done |
| 32 | `DEV-PVG-0230-UI-REGRESSION-01` | TEST | MOD-0230 | Add static UI regression checks for localized loading/empty states, safe alert display, bounded reason codes, same-origin proxy, and disabled buttons | UI-05 | E2 achieved | Done |
| 33 | `AUD-PVG-DEEP-2026-08-21` | INS/DOC | PVG | Deep audit current PVG governance, route, API, UI, downstream, and package cleanliness; update this Control Tower plan | Latest pushed branch | E2 achieved | Done |

## 5. Completed WP: `PVG-0230-BE-01`

Goal: create the minimum local/dev/CI API host boundary needed before persistence, Gateway, and UI work.

Result:

- `services/Diten.PvgService/src/Diten.PvgService.Api/**` added.
- `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/**` added.
- `/health/live` and `/health/ready` defined.
- Existing Domain/Application/Infrastructure libraries wired.
- Deny-by-default local/dev/CI adapters wired.
- Production-like startup refusal guards implemented.
- Downstream MOD-0231 / MOD-0232 / MOD-0234 service registration is composition-only for health-host DI wiring;
  it exposes no downstream endpoint and authorizes no downstream runtime.
- No MOD-0230 business controllers, persistence, Gateway, frontend, seeds, jobs, menu/module catalog registration,
  delete, bulk-delete, archive, void, export, AI, or MedDRA data.

Evidence:

- API build passed with `0` warnings and `0` errors.
- API host guardrail tests passed: `5/5`.
- Evidence level achieved: **E2**.
- E3 was not claimed because no local host process was started.

Handoff note:

- Current implemented handoff evidence is `MOD0230HandoffReference v0.1` build/test only:
  `IntakeDraftId`, `IntakeNumber`, `ReceivedAtUtc`, `TriageOutcomeCode`, `RouteTargetQueueCode`, and
  `EvidenceLinkReferenceIds`.
- Future v1 operational handoff remains owner-approval blocked. `TenantId` and correlation / trace context are
  external server context, not client-supplied handoff fields. Source context, restricted PHI/PII fields,
  seriousness / priority detail, workflow instance metadata, and evidence completeness are not produced by BE-01 or
  BE-02.

## 5.1 Original Gate: `PVG-0230-BE-02`

Goal: add the MOD-0230 persistence boundary without exposing operational runtime.

Allowed paths:

- `services/Diten.PvgService/src/Diten.PvgService.Domain/**` for MOD-0230 persistence entity contracts only.
- `services/Diten.PvgService/src/Diten.PvgService.Infrastructure/**` for repository/index abstractions or in-memory
  local/dev/CI persistence wiring chosen by the work package.
- `services/Diten.PvgService/src/Diten.PvgService.Application/**` only where needed to consume the persistence boundary.
- `services/Diten.PvgService/tests/**` for persistence, tenant isolation, no-delete, and no-client-tenant tests.

Protected / forbidden:

- `.antigravity/**`
- `gateway/**`
- `frontend/**`
- Gateway routes, UI, seed data, jobs, menu/module catalog registration.
- Delete, bulk-delete, archive, void, export, AI, MedDRA dictionary data.

Acceptance criteria:

- Persistence boundary builds.
- Tenant isolation is enforced by design and tests.
- Client-supplied `TenantId` is absent from public create/update request contracts.
- Cross-tenant reads return not-found/empty semantics without existence leak.
- No delete, bulk-delete, archive, void, or export persistence methods exist.
- Existing five PVG test suites remain green when run serially.

Required evidence:

- E2: build + persistence/unit tests.
- E4-ready evidence for tenant isolation and no forbidden mutation surface.

## 5.2 Completed WP: `PVG-0230-BE-02`

Goal: add the MOD-0230 persistence boundary without exposing operational runtime.

Result:

- `IPvgIntakeDraftStore` added as the application-facing persistence contract.
- `InMemoryPvgIntakeDraftRepository` added for local/dev/CI persistence.
- `PvgIntakeDraftIndexCatalog` added with tenant-first index definitions.
- `PvgIntakeDraftApplicationService` now uses the store instead of a private dictionary.
- API host DI registers the local/dev/CI in-memory store.
- No Gateway, frontend, `.antigravity`, seed, job, menu/module catalog, controller, DbContext, Mongo, migration,
  archive, void, export, delete, bulk-delete, AI, or MedDRA data work was added.

Evidence:

- RegPvBase tests passed: `37/37`.
- API host guardrail tests passed after persistence DI: `5/5`.
- Lane report records full serial PVG total: `123 passed / 0 failed / 0 skipped`.
- Evidence level achieved: **E2**.
- E4-ready evidence exists for tenant isolation and no forbidden persistence surface, but E4 is not claimed until
  state-changing API/runtime smoke exists.

## 5.3 Completed WP: `PVG-0230-BE-03`

Goal: expose the approved MOD-0230 business route set through the local/dev/CI API host.

Allowed paths:

- `services/Diten.PvgService/src/Diten.PvgService.Api/**`
- `services/Diten.PvgService/src/Diten.PvgService.Application/RegPvBase/**`
- `services/Diten.PvgService/tests/**`

Protected / forbidden:

- `.antigravity/**`
- `gateway/**`
- `frontend/**`
- Appsettings/launchSettings unless a separate host configuration work package authorizes them.
- Seed data, jobs, menu/module catalog registration.
- Delete, bulk-delete, archive, void, export, AI, MedDRA dictionary data.

Acceptance criteria:

- Route family is downstream-only under `/api/v1/pv-case-intake-triage`.
- Only approved operations exist: create, update, list, detail, triage, route.
- No `DELETE`, bulk, archive, void, or export route is present.
- API responses use the repo response-envelope pattern or an explicitly local equivalent if shared dependency is not
  available in this service yet.
- Tenant context, actor context, and correlation context are resolved server-side or fail closed.
- No public request DTO accepts client-supplied `TenantId`.
- Existing five PVG test suites remain green when run serially.

Required evidence:

- E2: build + controller/route/unit tests.
- E3 only if a local host process is started and health/business route smoke is run.

Result:

- `PvgCaseIntakeTriageEndpoints` added under the local/dev/CI API host.
- Downstream route family added under `/api/v1/pv-case-intake-triage`.
- Approved operations only: create, update, list, detail, triage, route.
- Request context fails closed before service execution when tenant, actor, or correlation context is missing where
  required.
- Public create/update/triage/route request DTOs do not expose `TenantId`.
- No Gateway, frontend, appsettings/launchSettings, seed, job, menu/module catalog, delete, bulk-delete, archive,
  void, export, AI, or MedDRA data was added.

Evidence:

- API tests passed: `9/9`.
- Lane report records serial PVG total: `127 passed / 0 failed / 0 skipped`.
- `git diff --check` passed.
- Evidence level achieved: **E2**.
- E3 was not claimed because no local host process was started.

## 5.3.1 Completed WP: `PVG-0230-BE-04`

Goal: harden MOD-0230 list/detail read-context behavior so read endpoints require the same server-resolved tenant,
actor, and correlation context as create, update, triage, and route.

Scope:

- Require tenant context for MOD-0230 list/detail read endpoints.
- Require actor context for MOD-0230 list/detail read endpoints.
- Require valid correlation context for MOD-0230 list/detail read endpoints.
- Keep create, update, triage, and route behavior aligned with the same fail-closed context rule.

Result:

- `VER-PVG-0230-READ-CONTEXT-01` passed.
- All six MOD-0230 business endpoints now fail closed on missing actor or correlation context before service
  invocation.
- No Gateway, frontend, appsettings, launchSettings, seed, job, migration, collection, Mongo, DbContext, repository,
  AI, MedDRA data, or downstream MOD-0231/0232/0234 runtime exposure was introduced.
- Operational runtime remains **NO-GO**.
- At this point in the sequence, `PVG-0230-AUTH-PROXY-01` was still blocked until a valid tenant-shell session was
  provided or seeded local development login use was explicitly approved for verification only. It is later recorded
  complete in §5.17.

Evidence:

- API tests passed: `22/22`.
- Prior full local PVG suite after implementation passed: `140/140`.
- `git diff --check` passed.
- Evidence level achieved: **E2**.
- E4-ready improvement: all six business endpoints now fail closed on missing actor/correlation before service
  invocation.

## 5.4 Completed WP: `PVG-0230-VER-01`

Goal: independently verify failure paths and leak-sensitive behavior after BE-03.

Scope:

- Read-only inspection and tests under `services/Diten.PvgService/**`.
- Verify missing policy, missing workflow approval, pending evidence, missing tenant, missing actor, missing/invalid
  correlation, cross-tenant reads, and forbidden route absence.
- Check that PHI/PII/free-text fields do not appear in route names, reason codes, logs/test output assertions,
  validation reason codes, or audit-intent metadata.

Required evidence:

- E2: focused failure-path tests.
- E4-ready: state-changing API paths proven to fail closed by route/unit tests. E4 remains unclaimed until live
  runtime + persistence smoke exists.

## 5.5 Completed WP With Gap: `PVG-0230-GW-01`

Goal: add the single approved Gateway route family after BE-03 route contract exists.

Allowed path:

- `gateway/Diten.ApiGateway/ocelot.json` only, integration-agent-owned.

Original acceptance criteria:

- Upstream `/api/pv-case-intake-triage` maps to downstream `/api/v1/pv-case-intake-triage`.
- Upstream `/api/pv-case-intake-triage/{everything}` maps to downstream `/api/v1/pv-case-intake-triage/{everything}`.
- No Gateway route exists for delete, bulk-delete, archive, void, export, downstream MOD-0231/0232/0234, AI, or
  MedDRA dictionary data.
- Gateway route tests or static ocelot route inspection pass.

Result:

- Static Gateway route evidence passed and Ocelot route tests passed.
- Live smoke proved health and approved MOD-0230 paths route from Gateway to PVG API.
- Live smoke also proved the current catch-all route forwards unapproved-looking paths to PVG instead of failing at
  the Gateway:
  - `PATCH /api/pv-case-intake-triage/{id}` reached PVG and returned API `405`.
  - `GET /api/pv-case-intake-triage/export` reached PVG as a detail route and returned `409`.
  - `archive`, `void`, and `bulk` paths reached PVG and returned API `404/405`.
- `DELETE` root/detail returned `404`, so delete is not routable.

Control Tower status:

- `PVG-0230-GW-01` remains acceptable as static E2 route evidence.
- Clean E3 Gateway acceptance is blocked until `PVG-0230-GW-02` narrows route templates and methods.

## 5.6 Completed WP With Runtime Watch: `PVG-0230-GW-02`

Goal: narrow the MOD-0230 Gateway route family so only the approved intake route templates and HTTP methods can reach
the PVG API through Gateway.

Allowed paths:

- `gateway/Diten.ApiGateway/ocelot.json`
- `gateway/Diten.ApiGateway.Tests/**`

Protected / forbidden:

- `services/**`
- `frontend/**`
- `.antigravity/**`
- Appsettings, launch settings, seeds, jobs, menu/module catalog registration.
- Any MOD-0231, MOD-0232, MOD-0234 Gateway route.
- Delete, bulk-delete, archive, void, export, AI, or MedDRA dictionary route.

Target Gateway templates:

- `GET, POST /api/pv-case-intake-triage`
- `GET, PUT /api/pv-case-intake-triage/{intakeDraftId}`
- `POST /api/pv-case-intake-triage/{intakeDraftId}/triage`
- `POST /api/pv-case-intake-triage/{intakeDraftId}/route`

Acceptance criteria:

- No `PATCH` method is configured for the MOD-0230 Gateway route family.
- No broad `{everything}` catch-all remains for MOD-0230 PVG intake.
- `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete` are not routable through Gateway.
- Gateway tests prove the exact approved template/method matrix and forbidden-route absence.
- If local runtime is started, live smoke proves approved routes reach PVG and forbidden paths fail at Gateway.

Evidence target:

- E2 required: Ocelot config parse + focused Gateway tests + `git diff --check`.
- E3 desired: live API + Gateway smoke after narrowing.

Result:

- Broad MOD-0230 PVG catch-all route was replaced with four explicit Gateway templates:
  - `GET, POST /api/pv-case-intake-triage`
  - `GET, PUT /api/pv-case-intake-triage/{intakeDraftId}`
  - `POST /api/pv-case-intake-triage/{intakeDraftId}/triage`
  - `POST /api/pv-case-intake-triage/{intakeDraftId}/route`
- Focused Gateway tests now assert the exact approved method/template matrix.
- Focused Gateway tests assert no `PATCH`, `DELETE`, `OPTIONS`, `{everything}`, `export`, `archive`, `void`,
  `bulk`, `bulk-delete`, `meddra`, or `ai` PVG route template.
- Gateway tests passed: `19/19`.
- `git diff --check` passed.

Residual runtime watch:

- There are no explicit forbidden PVG route templates now.
- A single-segment URL such as `/api/pv-case-intake-triage/export` may still be syntactically matchable by
  `{intakeDraftId}` at runtime unless a Gateway deny mechanism, service-side reserved-ID rule, or live smoke proves
  otherwise.
- This does not reopen the old broad catch-all, but it blocks clean E3 forbidden-subpath acceptance until
  `PVG-0230-GW-VER-02` / `PVG-0230-INT-API-GW-02`.

## 5.7 Completed WP: `PVG-0230-UI-VER-01`

Goal: verify the implemented MOD-0230 tenant UI as regulated-scope static/compile evidence while Gateway narrowing is
being corrected separately.

Scope:

- `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs`
- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`
- `frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/**`
- `frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/**`

Acceptance criteria:

- UI uses `_LayoutTenantShell`.
- Browser-side JavaScript calls same-origin MVC routes only.
- MVC proxy forwards server-side to Gateway `/api/pv-case-intake-triage`.
- No client-supplied `TenantId`.
- No delete, bulk-delete, archive, void, export, AI, MedDRA data, direct service-port calls, menu/module catalog, seed,
  or job surface.
- DataTable verifier failures are either fixed or recorded as scoped exceptions with reason and compensating control.

Evidence target:

- E2/E4-ready static: frontend build, static forbidden scan, l10n/resource scan, documented DataTable verifier outcome.
- E3/E4 browser evidence waits for `PVG-0230-GW-02`.

Result:

- `_LayoutTenantShell` confirmed on Index, Create, Edit, and Details.
- Browser JavaScript uses same-origin MVC proxy only.
- MVC proxy forwards server-side to Gateway `/api/pv-case-intake-triage`.
- No client-supplied `TenantId`; tenant header is server-side only.
- No scoped UI delete, bulk, archive, void, export, AI, MedDRA, menu, seed, job, appsettings, launchSettings, Mongo,
  or DbContext surface found.
- Seven resource cultures exist: `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.
- Frontend build passed with `0` errors.
- DataTable verifier result remains `68 passed / 15 failed`; failures are recorded scoped exceptions because the
  generic Compact verifier expects bulk/delete/export/direct-Gateway patterns that are explicitly forbidden for this
  regulated slice.

## 5.8 Completed WP: `PVG-0230-GW-VER-02`

Goal: independently verify the narrowed MOD-0230 Gateway route surface after GW-02.

Scope:

- Read-only inspection of `gateway/Diten.ApiGateway/ocelot.json`.
- Focused Gateway test execution.
- Live local API + Gateway smoke if ports `5011` and `5000` can be started.

Acceptance criteria:

- Exact approved templates/methods are present and point to `localhost:5011`.
- No `PATCH`, `DELETE`, `OPTIONS`, `{everything}`, downstream MOD-0231/0232/0234, AI, or MedDRA route exists for PVG.
- Forbidden subpaths `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete` do not route through Gateway
  during live smoke, or the residual single-segment match risk is escalated to `PVG-0230-GW-03`.

Evidence target:

- E2 required: static route inspection + Gateway tests.
- E3 desired: live Gateway + PVG API smoke.

Result:

- Verification verdict: **NO-GO**.
- Exactly four PVG templates exist and point to `localhost:5011`.
- Approved method matrix is narrowed correctly:
  - root: `GET`, `POST`
  - detail: `GET`, `PUT`
  - triage: `POST`
  - route: `POST`
- No `PATCH`, `DELETE`, `OPTIONS`, or `{everything}` PVG route exists.
- No MOD-0231, MOD-0232, MOD-0234, AI, or MedDRA Gateway route exists.
- Failure: forbidden operation names still route as `{intakeDraftId}` through the detail template for `GET` and `PUT`:
  `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete`.
- Gateway tests passed `19/19`, but current tests do not catch reserved-word-as-ID behavior.
- `git diff --check` passed.
- Live smoke was not clean because PVG API did not remain reachable on `5011`; Gateway could route one approved PVG
  path but returned `502` due unavailable downstream.

Control Tower status:

- `PVG-0230-GW-02` is not accepted as clean narrowed Gateway behavior.
- `PVG-0230-GW-03` is promoted from conditional to required next work.

## 5.9 Attempted WP: `PVG-0230-INT-API-GW-02`

Goal: rerun live local API + Gateway smoke against the narrowed route family.

Required checks:

- PVG API health on `127.0.0.1:5011`.
- Gateway health on `127.0.0.1:5000`.
- Approved routes reach PVG and return expected fail-closed local/dev responses.
- `PATCH`, `DELETE`, `OPTIONS`, `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete` do not produce an
  accepted Gateway-to-PVG business route.
- All processes are stopped after smoke and port state is reported.

Result:

- Verification verdict: **NO-PASS**.
- Initial ports `5011` and `5000` were free.
- PVG API process started by `dotnet run`, but did not open a listener on `5011`.
- PVG API direct built-DLL retry behaved the same: process briefly alive, no listener.
- Gateway process started and briefly showed a listener, but health probing was not reliable.
- PVG `/health/live` and `/health/ready` were not reached.
- Approved Gateway business route smoke was not completed because PVG downstream was unavailable.
- Final port state after cleanup: `5011` closed, `5000` closed.

Control Tower status:

- E3 was not achieved.
- `PVG-0230-RUN-01` is required before another runtime smoke can be accepted.

## 5.10 Completed WP: `PVG-0230-GW-03`

Goal: prevent reserved operation words from being accepted as `intakeDraftId` by the MOD-0230 Gateway/API route
family.

Allowed paths:

- Prefer Gateway-only if Ocelot supports a clean deny/priority mechanism in this repo:
  `gateway/Diten.ApiGateway/ocelot.json`, `gateway/Diten.ApiGateway.Tests/**`.
- If Gateway cannot express it safely, use service-side fail-closed handling under:
  `services/Diten.PvgService/src/Diten.PvgService.Api/**`,
  `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/**`.

Protected / forbidden:

- No frontend work.
- No `.antigravity/**`.
- No MOD-0231/0232/0234 runtime route or surface.
- No archive, void, export, delete, or bulk-delete operation implementation.

Acceptance criteria:

- `GET`/`PUT /api/pv-case-intake-triage/export`, `/archive`, `/void`, `/bulk`, `/bulk-delete`, and `/delete` are not
  treated as intake draft detail/update business routes.
- No `PATCH`, `DELETE`, `OPTIONS`, or broad `{everything}` PVG Gateway route is reintroduced.
- Tests prove reserved words fail before any application service mutation/read contract is invoked, or prove Gateway
  negative routing if implemented there.
- Existing Gateway and PVG API route tests remain green.

Evidence target:

- E2 required: static route/model tests plus API/Gateway focused tests.
- E3 desired after `PVG-0230-RUN-01`: live smoke proves reserved words do not become accepted business routes.

Result:

- Gateway-only blocking was not cleanly expressible with the current Ocelot placeholder route.
- API boundary guard was added for reserved intake draft ID segments:
  `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete`.
- Reserved segments return `404` before they can reach the intake application service as `intakeDraftId`.
- Focused API coverage proves `GET`, `PUT`, triage, and route fail closed for reserved IDs.
- No forbidden operation endpoint or business behavior was added.
- PVG API focused tests passed: `21/21`.
- Gateway Ocelot focused tests passed: `19/19`.
- `git diff --check` passed.

Control Tower status:

- E2 is achieved for reserved-segment denial.
- E3 remains pending until `PVG-0230-INT-API-GW-03` runs through Gateway against a live PVG API.

## 5.11 Completed WP: `PVG-0230-RUN-01`

Goal: diagnose and fix the local/dev/CI PVG API startup path that starts a process but does not bind `5011`.

Allowed paths:

- `services/Diten.PvgService/src/Diten.PvgService.Api/**`
- `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/**`
- docs-only notes if no code change is needed.

Protected / forbidden:

- No production appsettings or launchSettings unless separately authorized.
- No Gateway changes unless the diagnosis proves Gateway startup is the problem.
- No MOD-0231/0232/0234 runtime exposure.
- No seed, job, menu, catalog, persistence, archive, void, export, delete, bulk-delete, AI, or MedDRA data.

Acceptance criteria:

- Root cause is recorded from process output/logs.
- Local/dev/CI API can be started on `127.0.0.1:5011`.
- `/health/live` and `/health/ready` return expected local/dev/CI health payloads.
- Production-like refusal guards remain intact.
- Processes are stopped after verification and port state is reported.

Evidence target:

- E2 required if code changes are made: API build + API host tests + `git diff --check`.
- E3 required for completion: live local health smoke on `5011`.

Result:

- Root cause recorded: sandboxed local execution failed to bind with `System.Net.Sockets.SocketException (13):
  Permission denied`.
- `Program.cs` was changed to explicit `WebHostBuilder` / Kestrel startup with default `http://127.0.0.1:5011`,
  while preserving PVG service registration and route mapping.
- API build passed with `0` errors.
- API focused tests passed: `21/21`.
- API started on `http://127.0.0.1:5011` with elevated local socket permission.
- `/health/live` returned `200 OK` with `operationalRuntimeAuthorized: false`.
- `/health/ready` returned `200 OK` with `nonProductionAdaptersEnabled: true`.
- Production-like refusal guard passed direct startup check.
- PVG API process was stopped and final `5011` state was closed / no listener.

Control Tower status:

- E2 and direct API E3 health evidence are achieved.
- Full Gateway-route E3 still requires `PVG-0230-INT-API-GW-03`.

## 5.12 Completed WP: `PVG-0230-INT-API-GW-03`

Goal: run the full local API + Gateway route smoke after GW-03 and RUN-01.

Required checks:

- Start PVG API on `127.0.0.1:5011` with the local socket permission mode required by RUN-01 evidence.
- Start Gateway on `127.0.0.1:5000`.
- PVG `/health/live` and `/health/ready` return expected local/dev/CI payloads.
- Gateway health is reachable.
- Approved Gateway routes reach PVG:
  - `GET, POST /api/pv-case-intake-triage`
  - `GET, PUT /api/pv-case-intake-triage/{intakeDraftId}`
  - `POST /api/pv-case-intake-triage/{intakeDraftId}/triage`
  - `POST /api/pv-case-intake-triage/{intakeDraftId}/route`
- Forbidden method/path probes do not become accepted business routes:
  `PATCH`, `DELETE`, `OPTIONS`, `export`, `archive`, `void`, `bulk`, `bulk-delete`, `delete`.
- All processes are stopped after smoke and port state is reported.

Evidence target:

- E3: live local API + Gateway route behavior.
- E4-ready: forbidden operation names fail closed before business service execution.

Result:

- Verification verdict: **PASS**.
- Temporary PVG API and Gateway runtime processes were started and stopped after smoke.
- PVG API health passed:
  - `GET /health/live` -> `200`
  - `GET /health/ready` -> `200`
  - health payload confirmed `operationalRuntimeAuthorized: false`.
- Gateway health passed:
  - `GET /health/live` -> `200`.
- Approved Gateway routes reached PVG:
  - `GET /api/pv-case-intake-triage` -> `409` PVG fail-closed envelope
  - `POST /api/pv-case-intake-triage` -> `409` PVG fail-closed envelope
  - `GET /api/pv-case-intake-triage/smoke-id` -> `409` PVG fail-closed envelope
  - `PUT /api/pv-case-intake-triage/smoke-id` -> `409` PVG fail-closed envelope
  - `POST /api/pv-case-intake-triage/smoke-id/triage` -> `400` PVG validation envelope
  - `POST /api/pv-case-intake-triage/smoke-id/route` -> `409` PVG fail-closed envelope
- Forbidden `PATCH`, `DELETE`, and `OPTIONS` probes across root/detail/triage/route returned `404` with Gateway
  no-route evidence.
- Reserved operation words `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete` returned `404` for
  `GET` and `PUT`.
- Final port state: `5000` closed, `5011` closed.
- `git diff --check` passed.

Control Tower status:

- E3 is achieved for live local API + Gateway route behavior.
- E4-ready evidence is achieved for forbidden operation fail-closed behavior.
- UI browser smoke is now unblocked.

## 5.13 Attempted WP: `PVG-0230-UI-SMOKE-01`

Goal: run browser/runtime UI smoke through MVC proxy -> Gateway -> PVG API after successful API + Gateway route smoke.

Required checks:

- Start required local runtime processes, then stop them after smoke.
- Case Intake/Triage page loads under the tenant shell.
- Browser JavaScript uses same-origin MVC proxy only.
- No browser-side direct service-port or direct Gateway URL calls.
- MVC proxy reaches Gateway and Gateway reaches PVG API for approved paths.
- List/detail/create/update validation paths behave fail-closed.
- No delete, export, archive, void, bulk, bulk-delete, AI, or MedDRA UI surface appears.
- DataTable verifier exceptions remain documented and scoped to forbidden regulated actions.

Evidence target:

- E3: browser/runtime UI smoke.
- E4-ready: UI does not expose forbidden regulated actions and uses the approved proxy path.

Result:

- Verification verdict: **FAIL for full UI smoke**.
- Runtime path partially worked:
  - PVG API `5011`, Gateway `5000`, and MVC frontend `5001` started.
  - PVG health passed.
  - Gateway health passed.
  - Create page loaded under tenant shell with HTTP `200`.
  - PVG browser JavaScript used same-origin MVC proxy paths and no direct service-port calls.
  - MVC proxy reached Gateway, and Gateway reached PVG for list/detail/create/update validation paths.
- Full UI smoke failed because `/Pharmacovigilance/CaseIntakeTriage` returned HTTP `500`.
- Root cause: `Index.cshtml` used relative partials `_Filter`, `_DataTable`, and `_IndexL10n`; MVC searched the
  controller-named view folder instead of the nested PVG folder.
- No delete/export/archive/void/bulk/bulk-delete/AI/MedDRA PVG UI surface was found.
- Processes were stopped and ports `5000`, `5001`, and `5011` were closed.
- `git diff --check` passed.

Control Tower status:

- E3 is partially achieved for API/Gateway/MVC proxy runtime path.
- Full UI E3 is blocked until `PVG-0230-UI-FIX-01` and retry smoke.

## 5.14 Completed WP: `PVG-0230-UI-FIX-01`

Goal: fix the Case Intake/Triage list page partial lookup failure found by UI smoke.

Result:

- `Index.cshtml` now uses absolute partial paths:
  - `~/Views/Pharmacovigilance/CaseIntakeTriage/_Filter.cshtml`
  - `~/Views/Pharmacovigilance/CaseIntakeTriage/_DataTable.cshtml`
  - `~/Views/Pharmacovigilance/CaseIntakeTriage/_IndexL10n.cshtml`
- Frontend build passed with `0` errors.
- Build warnings are pre-existing/unrelated warnings in NuGet metadata lookup and non-PVG Razor files.
- `git diff --check` passed for the fixed UI file and plan file.

Control Tower status:

- E2 fix evidence is achieved.
- `PVG-0230-UI-SMOKE-02` is the next runtime gate.

## 5.15 Completed WP: `PVG-0230-UI-SMOKE-02`

Goal: rerun browser/runtime UI smoke after the list-page partial path fix.

Required checks:

- Start required local runtime processes, then stop them after smoke.
- `/Pharmacovigilance/CaseIntakeTriage` returns HTTP `200`.
- Index, Create, Edit, and Details load under tenant shell where authentication permits.
- Browser JavaScript uses same-origin MVC proxy only.
- No browser-side direct service-port or direct Gateway URL calls.
- MVC proxy reaches Gateway and Gateway reaches PVG API for approved paths.
- List/detail/create/update validation paths behave fail-closed.
- No delete, export, archive, void, bulk, bulk-delete, AI, or MedDRA UI surface appears.
- Platform navigation `502` from missing Platform `5057` may be recorded separately if it does not block the PVG page.

Evidence target:

- E3: browser/runtime UI smoke.
- E4-ready: UI does not expose forbidden regulated actions and uses the approved proxy path.

Result:

- Verification verdict: **PASS with caveats**.
- Runtime processes were started for PVG API `5011`, Gateway `5000`, and MVC frontend `5001`, then stopped.
- Health passed:
  - PVG `/health/live` -> `200`
  - PVG `/health/ready` -> `200`
  - Gateway `/health` -> `200`
- Tenant-shell pages passed:
  - `/Pharmacovigilance/CaseIntakeTriage` -> `200`
  - `/Pharmacovigilance/CaseIntakeTriage/Create` -> `200`
  - `/Pharmacovigilance/CaseIntakeTriage/Edit/smoke-id` -> `200`
  - `/Pharmacovigilance/CaseIntakeTriage/Details/smoke-id` -> `200`
- MVC proxy reached Gateway, and Gateway reached PVG API:
  - list -> `409 PVG_FIELD_SECURITY_POLICY_UNAVAILABLE`
  - detail -> `409 PVG_FIELD_SECURITY_POLICY_UNAVAILABLE`
  - create validation -> `400 PVG_FIELD_VALUE_INVALID / PVG_REQUIRED_FIELD_MISSING`
  - update validation -> `400 PVG_FIELD_VALUE_INVALID / PVG_REQUIRED_FIELD_MISSING`
- Forbidden MVC UI/API surfaces returned `404`: delete, export, archive, void, bulk, bulk-delete, AI, and MedDRA.
- Final port state: `5000`, `5001`, and `5011` closed.
- `git diff --check` passed.

Caveats:

- Authenticated in-app browser network capture was limited by tooling, so browser-network evidence is partial.
- PVG module JavaScript uses same-origin MVC proxy only: `/Pharmacovigilance/CaseIntakeTriage/api`.
- Shared tenant shell still contains global dev Gateway config (`window.ApiBaseUrl` -> `:5000`) and generic
  notification/delete helper strings. No PVG-specific direct service/Gateway calls or PVG forbidden action surfaces
  were found.
- Authenticated checks were completed through live MVC HTTP requests because the in-app browser page execution was
  read-only for cookie injection.

Control Tower status:

- E3 is achieved for live MVC -> Gateway -> PVG runtime path.
- E4-ready evidence is achieved for PVG forbidden-surface absence, with the shared-shell caveat.
- Local E5 closeout is now unblocked.

## 5.16 Completed WP: `PVG-0230-INT-01`

Goal: local/dev end-to-end closeout after API/Gateway/UI runtime evidence.

Required checks:

- Reuse the successful startup shape for PVG API, Gateway, and MVC frontend.
- Confirm API/Gateway health and route behavior did not regress.
- Confirm Case Intake/Triage UI entry points still load.
- Confirm MVC proxy -> Gateway -> PVG API still returns expected fail-closed responses.
- Confirm forbidden operations remain absent from Gateway/API/UI surfaces.
- Confirm MOD-0231, MOD-0232, and MOD-0234 still have no runtime exposure.
- Confirm source-only package manifest remains valid and excludes `.claude/settings.local.json`, generated output,
  and local artifacts.
- Stop all runtime processes and report final port state.

Evidence target:

- E5 local/dev only. This does not authorize operational runtime, production, supplier qualification, or validation.

Result:

- Verification verdict: **PASS-with-gap**.
- PVG API, Gateway, and MVC Web were built and started from fresh local binaries.
- Health passed:
  - PVG API `5011` `/health/live` -> `200`
  - PVG API `5011` `/health/ready` -> `200`, with `operationalRuntimeAuthorized: false`
  - Gateway `5000` `/health` -> `200 Healthy`
- Direct API fail-closed behavior passed:
  - no tenant -> `409 PVG_TENANT_CONTEXT_REQUIRED`
  - tenant context -> `409 PVG_FIELD_SECURITY_POLICY_UNAVAILABLE`
- Gateway fail-closed behavior passed:
  - `GET /api/pv-case-intake-triage` -> `409 PVG_FIELD_SECURITY_POLICY_UNAVAILABLE`
  - `POST /api/pv-case-intake-triage` -> `409 PVG_PERMISSION_DENIED`
- UI entry points reached Web and were protected:
  - Index/Create/Edit/Details redirected to `/account/login`
  - unauthenticated MVC proxy `/Pharmacovigilance/CaseIntakeTriage/api/list` returned controlled `401` / login
    behavior
- Forbidden operations remained absent:
  - Gateway `export`, `archive`, `bulk-delete`, and DELETE detail probes returned `404`
  - Direct API `bulk-delete` returned `404`
  - Direct API DELETE detail returned `405 Allow: GET, PUT`
  - UI forbidden route `/Pharmacovigilance/CaseIntakeTriage/export` returned `404`
- MOD-0231, MOD-0232, and MOD-0234 still have no Gateway route, frontend route, API endpoint/controller,
  persistence, jobs, seeds, AI, fake signal, fake metric/cohort, or MedDRA runtime exposure.
- `git diff --check` passed.
- Source-only staging manifest remains valid if `.claude/settings.local.json`, `bin/obj`, and local artifacts stay
  excluded.
- Final port state: `5011`, `5000`, and `5001` stopped / no listener.

Gap:

- Authenticated MVC proxy -> Gateway -> PVG API traversal was not proven in this closeout because no valid
  tenant-shell auth cookie was available. A generated token cookie still redirected to login. This gap is later
  closed by §5.17.

Control Tower status:

- E5 local/dev closeout was achieved as **PASS-with-gap** at this point in the sequence.
- This is local/dev evidence only and does not authorize operational runtime.
- The remaining technical evidence gap was isolated to `PVG-0230-AUTH-PROXY-01`; it is later recorded complete in
  §5.17.

## 5.17 Completed WP: `PVG-0230-AUTH-PROXY-01`

Goal: prove authenticated MVC proxy traversal with a valid tenant-shell auth session.

Result:

- Tenant login succeeded with the supplied local-dev admin account.
- Web tenant-shell Case Intake/Triage page loaded.
- MVC proxy reached Gateway and Gateway reached `Diten.PvgService.Api`.
- PVG returned safe fail-closed `PVG_PERMISSION_DENIED` under local/dev deny adapters.
- Forbidden delete, export, archive, void, and bulk routes remained closed.
- No operational runtime authorization was granted.
- No MOD-0231, MOD-0232, or MOD-0234 runtime exposure was introduced.
- Services were stopped and local smoke ports were confirmed closed.

Evidence target:

- E3/E4-ready local/dev evidence achieved for authenticated Web -> MVC proxy -> Gateway -> PVG API traversal.
- This remains local/dev evidence only and does not authorize operational runtime, production use, supplier
  qualification, or validation.

## 5.18 Completed WP: `VER-PVG-STAGING-SCOPE-02`

Goal: perform read-only source package audit before staging.

Result:

- Verification verdict: source-only staging manifest prepared.
- No staging was performed; staging area remains empty.
- No tracked or untracked `bin/obj` candidates were found; physical generated folders exist but are ignored and must
  stay out of staging.
- `.claude/settings.local.json` is dirty and outside the approved PVG package. It is explicitly excluded from the
  source-only staging manifest.
- Generated/local artifacts such as `.DS_Store`, screenshots, and spreadsheets are excluded.
- Exact source-only `git add -- ...` path list was prepared for approved PVG service, Gateway, UI, and governance/doc
  scope.
- MOD-0231, MOD-0232, and MOD-0234 remain non-operational: no downstream Gateway, frontend, endpoints,
  persistence/Mongo, jobs, seeds, menu entries, AI integration, MedDRA dictionary data, fake signal, fake metric, or
  fake cohort implementation found in the dirty package.

Control Tower status:

- Source-only package staging is ready only by using the exact manifest path list.
- Do not use blind stage-all while `.claude/settings.local.json` remains dirty.

## 5.19 Completed WP: `PVG-DOWNSTREAM-INS-02`

Goal: confirm MOD-0231, MOD-0232, and MOD-0234 remain non-operational after MOD-0230 API/Gateway/UI work.

Result:

- No downstream Gateway, frontend, controller, endpoint, repository, DbContext, Mongo, migration, collection, job,
  seed, menu, AI, or MedDRA dictionary/import/search/cache drift found.
- Program maps only health and MOD-0230 intake endpoints.
- Watch item: `PvgServiceApiHost.cs` still registers MOD-0231/0232/0234 in-memory application services in DI. This
  remains composition/build-test wiring only because no downstream endpoint or route is mapped.
- Relationship to MOD-0230 remains handoff compatibility and downstream contract shaping only, not runtime
  authorization.

## 5.20 Completed WP: `DEV-PVG-0230-BE-05`

Goal: harden MOD-0230 backend read permission and context behavior for list/detail reads.

Scope:

- MOD-0230 backend read permission/context hardening only.
- List/detail query contracts and application service guardrail behavior.
- Focused API and RegPvBase test coverage.

Result:

- Read list/detail queries now carry tenant, actor, and correlation context into the application layer.
- Read list/detail use `pvg.mod0230.intake.read`.
- Read permission guardrails run before field policy or store access.
- Missing actor/correlation and denied read permission fail closed with safe reason codes and no data.
- No Gateway, appsettings, launchSettings, seed, job, migration, collection, Mongo, DbContext, repository,
  menu/module catalog, AI, MedDRA data, delete, bulk-delete, archive, void, export, or MOD-0231/0232/0234 runtime
  exposure was introduced.

Evidence:

- `VER-PVG-0230-BE05-UI02-01` verdict: **PASS**.
- `git diff --check` passed.
- API tests passed: `23/23`.
- RegPvBase tests passed: `38/38`.
- Evidence level achieved: **E2 source/build/test evidence**.
- No live runtime/browser smoke was run for this verification package.

Control Tower status:

- BE-05 is complete for local/dev build-test hardening.
- Operational runtime remains **NO-GO**.

## 5.21 Completed WP: `DEV-PVG-0230-UI-02`

Goal: harden MOD-0230 tenant UI/proxy controlled-response behavior and same-origin browser guardrails.

Scope:

- MOD-0230 tenant UI/proxy controlled-response hardening only.
- Safe display behavior for controlled `401`, `403`, and `409` responses.
- Same-origin proxy guardrails in list/form/detail scripts.
- Case Intake/Triage l10n completeness for new controlled-response keys.

Result:

- MVC proxy reads require actor context.
- Browser JavaScript calls only same-origin `/Pharmacovigilance/CaseIntakeTriage/api/...`.
- UI handles `401`, `403`, and `409` with safe controlled-response alerts.
- Displayed reason/validation codes are sanitized.
- Same-origin proxy URL guardrails exist in list, form, and detail scripts.
- New l10n keys exist in all seven Case Intake/Triage resource files.
- No browser JavaScript service-port `5011`, Gateway-port `5000`, `window.API`, `document.cookie`, or browser-created
  Authorization header behavior was introduced.
- No Gateway, appsettings, launchSettings, seed, job, migration, collection, Mongo, DbContext, repository,
  menu/module catalog, AI, MedDRA data, delete, bulk-delete, archive, void, export, or MOD-0231/0232/0234 runtime
  exposure was introduced.

Evidence:

- `VER-PVG-0230-BE05-UI02-01` verdict: **PASS**.
- `git diff --check` passed.
- Web build passed with an existing `NU1900` warning from restricted NuGet vulnerability metadata access.
- Evidence level achieved: **E2 source/build/test evidence**.
- No live runtime/browser smoke was run for this verification package.

Control Tower status:

- UI-02 is complete for local/dev build-test hardening.
- Operational runtime remains **NO-GO**.
- At this point in the sequence, `PVG-0230-AUTH-PROXY-01` was still blocked until a valid tenant-shell session was
  available or explicit seeded local development login approval was granted for verification only. It is later
  recorded complete in §5.17.

## 5.22 Completed WP: `DEV-PVG-0230-UI-03`

Goal: harden MOD-0230 UI loading, empty, error, and command in-flight states without adding new runtime surface.

Result:

- List/detail pages include `aria-live` status hooks and alert regions.
- List loading skeleton is hidden on success and error paths.
- Detail triage/route controls remain disabled until detail load succeeds and during command submission.
- Create/update submit button is disabled while submission is in flight.
- Static UI test coverage was added for same-origin MVC proxy use, forbidden action absence, and no downstream
  MOD-0231/MOD-0232/MOD-0234 UI exposure.

Evidence:

- `git diff --check` passed during package verification.
- JavaScript syntax checks passed for the PVG list, detail, form, and static test files.
- Vitest remained blocked because `frontend/Diten.Web/node_modules/.bin/vitest` was unavailable and no dependency
  install was authorized.
- Evidence level achieved: **E2 static/source evidence**.

## 5.23 Completed WP: `DEV-PVG-0230-UI-04`

Goal: add static accessibility and interaction guardrails for the MOD-0230 Case Intake/Triage tenant UI.

Result:

- Static assertions cover `role="alert"` and `aria-live` regions on list/detail pages.
- Static assertions cover DataTable/list load hooks: `preXhr`, `xhr`, `initComplete`, `showLoading`, `hideLoading`,
  and empty-state status.
- Static assertions cover disabled detail triage/route controls before successful detail load and during command
  submission.
- Static assertions cover disabled/busy create/update submit behavior during submission.

Evidence:

- UI04 package touched only `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js`.
- `git diff --check` and JavaScript syntax checks passed.
- Vitest remained blocked by missing local frontend dependencies.
- Evidence level achieved: **E2 static/source evidence**.

## 5.24 Completed WP: `DEV-PVG-0230-UI-05`

Goal: add static localization/resource guardrails for MOD-0230 Case Intake/Triage UI.

Result:

- Static test verifies all seven tenant cultures exist: `en`, `fr`, `es`, `zh`, `ar`, `ru`, and `tr`.
- Static test verifies safe UI state keys in each resource file: `ControlledBlock`, `SessionExpired`,
  `NotAuthorized`, `Forbidden`, `InvalidProxyEndpoint`, `ReasonCode`, `Loading`, and `NoRecords`.
- Static test verifies `_IndexL10n.cshtml` exposes those keys through `window.PvgCaseIntakeTriageL10n`.
- Static test verifies list/form/detail JavaScript localization references are backed by PVG resource keys or shared
  localizer keys.

Evidence:

- UI05 package touched only `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js`.
- `git diff --check` and JavaScript syntax checks passed.
- Vitest remained blocked by missing local frontend dependencies.
- Evidence level achieved: **E2 static/source evidence**.

## 5.25 Completed WP: `DEV-PVG-0230-NEGATIVE-SURFACE-01`

Goal: add tests-only negative-surface hardening for MOD-0230 API and UI.

Result:

- API tests check route templates, endpoint names, and display names do not expose forbidden surfaces:
  delete, bulk-delete, bulk, archive, void, export, AI, MedDRA dictionary/import/search, or downstream
  MOD-0231/MOD-0232/MOD-0234 runtime routes.
- UI static tests check browser/view surfaces for the same forbidden operations and downstream module names.
- Existing same-origin MVC proxy and no browser token handling checks remain in place.

Evidence:

- API tests passed: `24/24` at package time.
- `node --check frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js` passed.
- No runtime/source, Gateway, docs, execution, appsettings, persistence, seed/job/migration/collection/Mongo,
  DbContext, repository, menu/catalog, AI, MedDRA data, or downstream runtime surface was changed.
- Evidence level achieved: **E2 tests/source evidence**.

## 5.26 Completed WP: `DEV-PVG-0230-BE06-NEGATIVE-CONTRACT-01`

Goal: add tests-only backend negative-contract hardening for MOD-0230.

Result:

- Request DTO tests reject broader client tenant field names, not only `TenantId`.
- Route metadata tests allow only approved methods: `GET`, `POST`, and `PUT`.
- Route metadata tests exclude `DELETE`, `PATCH`, and `OPTIONS`.
- All six business endpoints fail closed with safe `PVG_*` reason-code envelopes before service invocation when
  tenant, actor, or correlation context is missing.
- Forbidden operation word coverage remains in place for `delete`, `bulk-delete`, `archive`, `void`, and `export`.

Evidence:

- Latest API tests passed during deep audit: `26/26`.
- `git diff --check HEAD~1..HEAD` passed during package verification.
- No runtime/source, Gateway, frontend views/scripts/resources, docs, execution, appsettings, persistence,
  seed/job/migration/collection/Mongo, DbContext, repository, menu/catalog, AI, MedDRA data, or downstream runtime
  surface was changed.
- Evidence level achieved: **E2 tests/source evidence**.

## 5.27 Completed WP: `DEV-PVG-0230-UI-REGRESSION-01`

Goal: add tests-only static UI regression coverage after UI03/UI04/UI05.

Result:

- Static UI test coverage now checks localized loading and empty states.
- Alert visibility and safe message display are checked.
- Reason-code display is sanitized and bounded.
- Strict same-origin MVC proxy behavior is checked across list/detail/form paths.
- Detail command buttons are checked for disabled behavior during detail loading and command submission.

Evidence:

- Latest commit `f0819509 PVG MOD-0230 harden UI regression tests` touched only
  `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js`.
- `git diff --check HEAD~1..HEAD` passed during package verification.
- `node --check frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js` passed during deep audit.
- Vitest remained blocked by missing local frontend dependencies.
- Evidence level achieved: **E2 static/source evidence**.

## 5.28 Completed WP: `AUD-PVG-DEEP-2026-08-21`

Goal: perform deep PVG audit from current local/remote branch state and update this plan.

Audit baseline:

- Branch: `feature/pvg/all-four-nonoperational-scaffold-final`.
- Local HEAD: `f0819509e020da31d624e445ab6d97402740d8a7`.
- Local upstream-tracking ref: `f0819509e020da31d624e445ab6d97402740d8a7`.
- Local dirty file before this plan update: `.claude/settings.local.json` only.
- No staged files before this plan update.

Deep-audit findings:

- High severity: none.
- Medium severity: none.
- Low / watch:
  - `.claude/settings.local.json` remains local-only and must stay excluded from package staging.
  - `PvgServiceApiHost` registers MOD-0231, MOD-0232, and MOD-0234 application services for composition/build-test
    wiring. No endpoint maps, Gateway routes, frontend surfaces, persistence, jobs, seeds, AI, MedDRA data, fake
    signal, fake metric, or fake cohort were found for those downstream modules.
  - Vitest remains unavailable locally because `frontend/Diten.Web/node_modules/.bin/vitest` is not present.

Confirmed runtime/surface posture:

- PVG API maps only health plus MOD-0230 Case Intake/Triage routes:
  `/api/v1/pv-case-intake-triage`, `/{intakeDraftId}`, `/{intakeDraftId}/triage`, and
  `/{intakeDraftId}/route`.
- Gateway PVG route matrix remains limited to:
  - `GET, POST /api/pv-case-intake-triage`
  - `GET, PUT /api/pv-case-intake-triage/{intakeDraftId}`
  - `POST /api/pv-case-intake-triage/{intakeDraftId}/triage`
  - `POST /api/pv-case-intake-triage/{intakeDraftId}/route`
- API reserved-segment guard still rejects `export`, `archive`, `void`, `bulk`, `bulk-delete`, and `delete` before
  business service invocation.
- Browser PVG scripts remain on same-origin MVC proxy paths and do not create browser `Authorization` / `Bearer`
  headers or read `document.cookie`.
- No appsettings, launchSettings, menu/module catalog, seed, job, migration, collection, Mongo, DbContext, real
  repository, delete/export/archive/void/bulk runtime action, AI, or MedDRA dictionary/import/search/display surface
  was found.

Fresh validation run on 2026-08-21:

| Check | Result |
|---|---:|
| API tests | `26/26` passed |
| RegPvBase tests | `38/38` passed |
| CaseProcessing tests | `21/21` passed |
| MedDRA Coding tests | `27/27` passed |
| SignalManagement tests | `33/33` passed |
| PVG service total | `145/145` passed |
| Gateway Ocelot tests | `19/19` passed |
| Web build | Passed, `0` errors; existing unrelated warnings only |
| `git diff --check` before plan edit | Passed |
| UI static test syntax | Passed |

Control Tower status after audit:

| Area | % | Status |
|---|---:|---|
| Branch/head control | 100% | local and upstream-tracking refs match `f0819509e020da31d624e445ab6d97402740d8a7` |
| MOD-0230 local/dev API hardening | 100% | pushed; latest API tests `26/26` |
| MOD-0230 Gateway route matrix | 100% | approved four-template matrix only; Gateway tests `19/19` |
| MOD-0230 tenant UI static hardening | 100% | UI03/UI04/UI05/regression guardrails present |
| PVG class-library build/test contracts | 100% | latest PVG relevant class-library/test-only validation `159/159`; commit `de4fca61` |
| Package cleanliness | 99% | only `.claude/settings.local.json` was dirty before this plan update |
| Authenticated MVC proxy proof | 100% | local/dev tenant-shell proxy smoke passed with safe `PVG_PERMISSION_DENIED` |
| Operational runtime | 0% | NO-GO |
| MOD-0231 runtime | 0% | blocked |
| MOD-0232 runtime / MedDRA data | 0% | blocked |
| MOD-0234 runtime / signal shell | 0% | blocked |

Next Agent Lane prompts:

```text
INS-PVG-DOWNSTREAM-WATCH-03

Read-only downstream drift watch for PVG after AUTH-PROXY local/dev evidence. Verify no MOD-0231/MOD-0232/MOD-0234 Gateway,
frontend, endpoint, persistence, job, seed, AI, MedDRA dictionary/import/search, fake signal, fake metric, or fake
cohort exposure. Do not edit, stage, commit, push, fetch, merge, restore, install, or clean files.
```

```text
VER-PVG-0230-REVIEW-PACKET-02

Read-only reviewer packet refresh for branch feature/pvg/all-four-nonoperational-scaffold-final. Confirm local/remote
head, dirty files, latest pushed commits, validation evidence, approved Gateway matrix, operational NO-GO, AUTH-PROXY
blocker, and downstream runtime closure. Do not edit, stage, commit, push, fetch, merge, install, or clean files.
```

Recommendation:

- Pause new runtime code.
- Continue only read-only/static/test-only work unless the owner supplies AUTH-PROXY prerequisites or runtime
  authorization evidence.
- Do not start operational runtime, Gateway expansion, menu/catalog, persistence, appsettings, jobs, seeds, AI,
  MedDRA dictionary data, or downstream runtime work without a new approved gate.

## 5.29 Completed WP: `DOC-PVG-DOWNSTREAM-CL-01`

Goal: record completed local/dev class-library/test-only downstream hardening progress after commit
`de4fca61 PVG harden downstream class-library contracts`.

Recorded progress:

- MOD-0231 Case Processing read/guard/handoff contract hardening completed.
- MOD-0232 MedDRA Coding opaque reference contract hardening completed.
- MOD-0234 Signal Management threshold/data-product placeholder contract hardening completed.
- PVG relevant tests passed: `159/159`.

Scope controls:

- Operational runtime remains **NO-GO**.
- MOD-0231, MOD-0232, and MOD-0234 runtime remains blocked.
- No Gateway, frontend, API endpoint, persistence, appsettings, seed, job, menu/catalog, AI, MedDRA
  data/import/search/cache, export, delete, archive, void, or bulk runtime exposure is authorized or recorded.

## 6. Verification Commands

Run PVG tests serially to avoid shared build-output locks:

```bash
dotnet test services/Diten.PvgService/tests/Diten.PvgService.RegPvBase.Tests/Diten.PvgService.RegPvBase.Tests.csproj -c Debug --no-restore -m:1
dotnet test services/Diten.PvgService/tests/Diten.PvgService.CaseProcessing.Tests/Diten.PvgService.CaseProcessing.Tests.csproj -c Debug --no-restore -m:1
dotnet test services/Diten.PvgService/tests/Diten.PvgService.MeddraCoding.Tests/Diten.PvgService.MeddraCoding.Tests.csproj -c Debug --no-restore -m:1
dotnet test services/Diten.PvgService/tests/Diten.PvgService.SignalManagement.Tests/Diten.PvgService.SignalManagement.Tests.csproj -c Debug --no-restore -m:1
```

Identity gates:

```bash
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0231 --name "Case Processing"
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0232 --name "MedDRA Coding"
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0234 --name "Signal Management"
```

## 7. Blockers and Decisions to Track

| Blocker | Owner | Effect |
|---|---|---|
| Authenticated MVC proxy traversal with valid tenant-shell auth context | PVG / integration owner | Remaining local/dev evidence gap |
| Dirty `.claude/settings.local.json` outside package | Local workspace owner | Blocks safe staging if blind stage-all is used |
| MOD-0019 real masking/row-field security owner contract | Platform/PSS owner needed | Blocks operational runtime and export |
| MOD-0023 real workflow/inbox contract | Platform/PSS owner needed | Blocks operational runtime routing/handoff |
| MOD-0031 real evidence-link contract | Platform/PSS owner needed | Blocks operational runtime handoff/evidence |
| Retention/legal-hold owner | Compliance/legal owner needed | Blocks archive/void and production validation |
| MedDRA license/source/version policy | PVG/legal/procurement | Blocks MOD-0232 runtime and any dictionary display/import |
| MOD-0004 semantic metric contracts | Metric owner | Blocks MOD-0234 runtime |
| MOD-0063 data-product/cohort contracts | Data platform owner | Blocks MOD-0234 runtime |

## 8. Closure Rule

At the end of each Control Tower turn, record:

- Agent verdict.
- Verification verdict.
- Control Tower status.
- Evidence level achieved.
- Files changed.
- Tests run and results.
- Newly unblocked work.
- Still-blocked work with owner/date if known.

Commit or local test success is not acceptance. Acceptance requires the evidence target defined for that work
package and does not change the PVG operational runtime gate unless the module pack explicitly changes.
