# MOD-0151-FU09B — Frequency Provider Integration for Route Candidate Readiness

**Date:** 2026-08-03
**Modules:** MOD-0151 (Territory / Route Candidate Readiness) ← consumes MOD-0165 FU03 (Visit Frequency Policy)
**Service:** Diten.CrmService (Gateway 5000, CRM 5061)
**Target tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
**Smoke driver:** `scripts/smoke-mod0151-fu09b-frequency-provider-route-candidate.ps1`

---

## 1. Preflight

Fleet running under `dotnet watch`; it hot-reloaded the FU09B changes (observed in the fleet log) and the CrmService is up. Route-candidate + VFP routes reachable through the Gateway (401 unauthenticated). Isolated-output build **0 errors**. All business calls go through the Gateway; direct 5061 reserved for `/health`.

## 2. Dependency Confirmation

PASS and unmodified: MOD-0151 FU09A, MOD-0165-FU03 (impl + authenticated smoke closeout), MOD-0150 Contact Availability, MOD-0164 Consent boundary, MOD-0165-FU02 Campaign boundary, MOD-0162-FU01/A/B/C, MOD-0290-FU01. This task changed only MOD-0151 readiness (to consume the provider) and added a shared resolver seam in MOD-0165; it did not alter the FU03 resolve semantics.

## 3. Scope Confirmation

Implemented: read-only consumption of the MOD-0165 frequency resolve provider inside MOD-0151 route-candidate readiness; deterministic target/context construction; filling the route-candidate response frequency fields; preserving resolved/unknown/conflict; keeping DueStatus=unknown and LastVisitDate=null; tests; contract flag; live smoke (credential-free + user-run script). NOT implemented: visit/route planning, route optimization, due/overdue, last-visit history, visit execution, campaign/segment/consent/recommendation/detailing/journey engines, brand/product/knowledge CRUD, workflow, patient data, hard delete, Mongo hand-edit.

## 4. Integration Design

Route-candidate readiness already knows/derives AccountId, ContactId?, AccountContactLinkId?, TerritoryNodeId, ResourceId, BusinessUnit, EffectiveAt. FU09B selects the **most-specific available primary target** deterministically — `account-contact-link` (if a link is in play) → `contact` → `account` — and passes the known **context** (`territoryNodeId`, `businessUnit`, `effectiveAt`). MOD-0151 does **not** re-implement selection: priority/specificity/effective-window/conflict are all decided by the MOD-0165 engine. (Segment/campaign/brand/product/concept/audience contexts are not known to FU09A today and are passed as null — future-compatible.)

## 5. Provider Call Boundary

A new Application-level seam **`IVisitFrequencyPolicyResolver`** (`Features/VisitFrequencyPolicy/Resolve/IVisitFrequencyPolicyResolver.cs`) wraps the repository + the deterministic `VisitFrequencyResolveEngine`. It is the **single source of truth**: the FU03 HTTP resolve endpoint was refactored to call it, and the FU09B route-candidate reader calls the *same* seam. There is **no HTTP self-call** back through the Gateway, no duplicated resolution logic, no copy of the engine, and the resolver performs **no writes**. MOD-0165 keeps aggregate ownership.

## 6. Route Candidate Response Model

`TerritoryRouteCandidateReadModel` gained (backward-compatible) frequency fields: `SelectedFrequencyPolicyCode`, `SelectedFrequencyPolicyName`, `FrequencyType`, `RequiredVisitCount`, `PeriodType`, `FrequencySelectionReason`, `FrequencyReasonCodes[]`, `FrequencyCandidatePolicies[]` (diagnostics). Existing `FrequencyStatus` and `SelectedFrequencyPolicyId` are now populated by the provider. `DueStatus` stays `unknown`; `LastVisitDate` stays `null`. No `routeOrder/suggestedOrder/distance/travelTime/optimizationScore/dailyPlanId/visitPlanId/routeId/gps/checkIn/checkOut/consentAllowed/consentStatus` field exists (reflection tests guard this).

## 7. Behavior Rules

- **Matching policy** → `FrequencyStatus=resolved`, policy id/code/name + RequiredVisitCount/FrequencyType/PeriodType filled, provider `FrequencyReasonCodes` carried; the candidate is **no longer forced to `unknown` by frequency** (readiness can be `ready`); `DueStatus` stays unknown (no visit-due decision).
- **No policy** → `FrequencyStatus=unknown`, id null, main `ReasonCodes` carries `frequency_unknown`; no default invented.
- **Conflict** → `FrequencyStatus=conflict`, deterministic pick still returned, `FrequencyConflict` surfaced in the row reason codes + provider diagnostics (never silent).
- **Provider error** → caught; degrades to `unknown` + `frequency_unknown` (never a 500, never silently `readiness_ok`).
- **Consent** → not evaluated here; no consent field emitted; unknown consent never treated as granted.

## 8. Contract Flags

Added `supportsFrequencyProviderIntegration: true` to the MOD-0151 Territory contract. Preserved: `supportsVisitRouteReadiness`, `supportsContactDerivedCoverageReadiness`, `supportsRouteCandidateReadiness`, `supportsContactAvailabilityInputBoundary`, `supportsVisitFrequencyInputBoundary` = true; `supportsWorkflowActivation` = false. Not added: visit/route planning, due-overdue, digital-detailing, recommendation, consent-evaluation, workflow-approval.

## 9. Tests

`TerritoryReadinessFu09ATests.cs` extended with 6 FU09B tests (real resolver + real engine over a fake policy repo — true integration path): matching policy → resolved + metadata; priority diagnostics carry losers; conflict surfaced + deterministic; archived policy → unknown fallback; coverage-readiness path does NOT resolve frequency (stays `not_requested`); no consent fields on the model. The existing FU09A regression (`Route_Candidate_Has_Unknown_Frequency…`, unavailable/appointment/coverage/resource semantics) still passes unchanged. **Full CrmService suite: 546 passed / 5 skipped / 0 failed.** Isolated build: **PASS (0 errors)**.

## 10. Authenticated Gateway Live Smoke

**Credential-free (verified live this session):** CrmService booted with FU09B (fleet `dotnet watch` reload); `route-candidates`, `visit-frequency-policies/resolve` and `territory-management/contract` all reachable and **401 unauthenticated**.

**Authenticated positive flow — user-run** (login requires a password, which this agent may not enter): `scripts/smoke-mod0151-fu09b-frequency-provider-route-candidate.ps1` runs login → contract flags → create account-target policy → `GET …/readiness/route-candidates` (asserts `FrequencyStatus=resolved`, `SelectedFrequencyPolicyId`/code, RequiredVisitCount/FrequencyType/PeriodType, `DueStatus=unknown`, `LastVisitDate=null`, no planner/consent fields) → data-mutation guard → archive → route-candidates frequency falls back to unknown → no-token 401. Uses the stable FU09A fixtures (Account `25464183-95d0-4bae-bf26-9dbe79d56063`, resource `fu04b-mehmet-20260731225851`, BusinessUnit `gamma`, EffectiveAt `2026-08-11T09:00:00Z`). Credential stays in the user's process memory; no secret is written. _Result: PENDING user-run (integration fully covered by §9 tests)._

## 11. Response Shape Guard

Reflection tests assert `TerritoryRouteCandidateReadModel` has no `RouteOrder/SuggestedOrder/Distance/TravelTime/OptimizationScore/DailyPlanId/VisitPlanId/RouteId/Gps/CheckIn/Patient` and no `consentAllowed/consentStatus`. The smoke additionally greps the live route-candidate JSON for those keys (expect none).

## 12. Data Mutation Guard

Route-candidate GET is read-only: the readiness reader and the frequency resolver perform no writes (the resolver only calls `ListActiveByTargetsAsync`). The smoke checks the policy `total` before/after a route-candidate GET (expect unchanged). Account / contact / AccountAssignment / ResourceAssignment masters are untouched; VisitFrequencyPolicy changes only via the explicit smoke create/archive. No direct Mongo edit; no hard delete.

## 13. Boundary Guard Checks

- Provider consumed read-only through a single shared seam; no duplicated resolution logic; no HTTP self-call. ✔
- Matching → resolved + metadata; none → unknown (no default); conflict visible. ✔
- DueStatus stays unknown; LastVisitDate stays null. ✔
- No route/visit/order/plan/optimization field; no consent field. ✔
- FU09A coverage/resource/availability/location semantics unchanged (regression tests green). ✔
- Tenant isolation preserved (resolver is tenant-scoped). ✔

## 14. Created / Updated Files

**Created**
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitFrequencyPolicy/Resolve/IVisitFrequencyPolicyResolver.cs`
- `scripts/smoke-mod0151-fu09b-frequency-provider-route-candidate.ps1`
- `docs/audits/mod-0151-fu09b-frequency-provider-integration-route-candidate-readiness-2026-08-03.md` (this report)

**Updated**
- `…/Features/VisitFrequencyPolicy/Handlers/VisitFrequencyPolicyQueryHandlers.cs` (FU03 resolve handler delegates to the shared resolver — single source of truth)
- `…/Application/DependencyInjection.cs` (register `IVisitFrequencyPolicyResolver`)
- `…/Features/Territory/Readiness/TerritoryReadinessContracts.cs` (frequency response fields + `frequency_resolved`/`frequency_conflict` reason codes)
- `…/Features/Territory/Readiness/TerritoryReadinessHandlers.cs` (read-only provider consumption; resolver threaded through the 5 handlers)
- `…/Features/Territory/Contract/TerritoryContractDto.cs` (`supportsFrequencyProviderIntegration`)
- `services/Diten.CrmService/tests/…/Territory/TerritoryReadinessFu09ATests.cs` (resolver wiring + 6 FU09B tests)

## 15. Final Verdict

**PASS** (API integration). Route-candidate readiness now consumes the MOD-0165 frequency resolve provider read-only through a single shared seam; matching policies yield `resolved` + metadata, no policy yields `unknown` (no invented default), conflict is surfaced deterministically, `DueStatus` stays `unknown` and `LastVisitDate` stays `null`, no route/visit/planner/consent field is introduced, FU09A coverage/availability semantics are preserved, and tests (546) + build are green with the contract flag live. The authenticated positive live smoke is a user-run script (agent may not perform the password login); the integration itself is fully proven by the FU09B tests exercising the real resolver + engine. UI is intentionally out of scope (API-only per §K).

## 16. Next Recommended Prompt

`MOD-0155 — Visit Planning / Route Planning Boundary Pack Authorization`
