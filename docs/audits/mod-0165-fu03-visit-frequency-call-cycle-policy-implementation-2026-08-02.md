# MOD-0165-FU03 — Visit Frequency / Call-Cycle Policy Implementation

**Date:** 2026-08-02
**Module:** MOD-0165 (Visit Frequency / Call-Cycle Policy), co-authored by MOD-0167
**Service:** Diten.CrmService (port 5061, Gateway 5000)
**Target tenant (smoke):** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
**Branch:** `feature/crm-integration`

---

## 1. Preflight

- Verified the CrmService clean-architecture layout (Domain / Persistence / Application / Infrastructure / Api) and reused the MOD-0150 FU07 (ContactAvailability) and MOD-0151 (Territory) aggregates as the implementation template.
- Confirmed the string-Guid class-map convention (subtype-4 avoidance) and the DateTimeOffset-as-BSON-array pitfalls (no parallel-array index/sort) from prior CRM aggregates.
- Confirmed MediatR auto-registers handlers/validators from the Application assembly (no manual handler wiring needed).
- Isolated-output builds were used throughout because the running fleet holds the normal `bin` DLLs.

## 2. Dependency Confirmation

All listed prerequisites are treated as PASS and were **not modified** by this task:

| Dependency | Status | Interaction in FU03 |
|---|---|---|
| MOD-0165-FU01 Ownership | PASS | This is the runtime of the FU01-authorized model |
| MOD-0165-FU02 Campaign/Targeting Boundary | PASS | `Source=campaign` + `CampaignId` provenance only; no campaign CRUD |
| MOD-0167-FU01 Segment-sourced authoring boundary | PASS | `Source=segmentation` + `SegmentId` provenance; no membership calc |
| MOD-0164-FU01 Consent boundary | PASS | Untouched; no consent read/write/eval |
| MOD-0150 Contact Availability | PASS | Template only; not modified |
| MOD-0151 FU09A Visit/Route Readiness | PASS | Not modified; FU09B follow-up opened (§18) |
| MOD-0162-FU01/A/B/C Knowledge/Concept | PASS | `concept-node` / `audience-profile` future target types; no traversal |
| MOD-0290-FU01 Brand/Product master | PASS | `BrandId`/`ProductId` optional context; no brand/product CRUD |

Per the FU01 note, this implementation is delivered as **MOD-0165-FU03** (FU02 was consumed by the Campaign/Targeting boundary).

## 3. Scope Confirmation

Implemented: VisitFrequencyPolicy aggregate; create/update/archive/read/list; effective-window validation; TargetType validation; FrequencyType/PeriodType/Source/Status validation; priority/conflict resolution; read-only resolve provider; contract flags; tests; gateway route. **UI deferred** (follow-up, §19).

NOT implemented (boundary preserved): visit plan, route plan, due/overdue engine, last-visit history, campaign engine, segmentation/membership, consent evaluation, digital detailing, content recommendation, journey progress, target assignment, brand/product/knowledge CRUD, workflow/approval, import/export, patient data.

## 4. Implementation Summary

A new **`VisitFrequencyPolicy`** aggregate (its own collection `visit_frequency_policies`) with:
- Full CRUD-minus-delete (create / update / archive / get / list) over MediatR handlers.
- A pure, deterministic, **read-only resolve engine** exposed at `GET …/resolve`.
- A contract endpoint advertising exactly the five allowed frequency flags.
- Structural (in-domain) vocabulary validation, so the runtime needs no MOD-0048 publish.
- Dedicated Gateway routes.

## 5. Domain Model

`VisitFrequencyPolicy : EntityBase` (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/VisitFrequencyPolicy.cs`):

`PolicyId` (=`Id`), `TenantId` (claim-only), `PolicyCode` (stable), `PolicyName`, `Description?`, `TargetType`, `TargetId`, `BusinessUnit?`, `TerritoryNodeId?`, `CampaignId?`, `SegmentId?`, `BrandId?`, `ProductId?`, `CycleId?`, `CyclePeriodId?`, `FrequencyType`, `RequiredVisitCount`, `PeriodType`, `EffectiveFrom`, `EffectiveTo?`, `Priority`, `Source`, `Status`, `Notes?`, `CreatedAt/By`, `UpdatedAt/By`, `ArchivedAt?/By?`.

Rules enforced: TenantId never from payload; **no hard delete**; archive is soft lifecycle (stamps `ArchivedAt/By`); PolicyCode stable, rename via PolicyName; `EffectiveTo < EffectiveFrom → 400`; `RequiredVisitCount ≤ 0 → 400`; only `active` is resolvable; out-of-window/draft/archived excluded from resolve but archived stays readable.

## 6. TargetType Policy

Supported (in-domain `FrequencyTargetType`): `account`, `contact`, `account-contact-link`, `segment`, `territory-node`, `campaign-target`, `concept-node`, `audience-profile`.
- `account-contact-link` is the **most specific** field target (specificity rank 1).
- `contact` with no location context is surfaced via reason `contact_location_context_absent` (never a failure).
- `segment` stored as target only — **membership never computed**.
- `campaign-target` may carry a `CampaignId` reference.
- `concept-node` / `audience-profile` supported as **future-compatible** targets; **no graph traversal**.
- `TargetId` empty → 400. Unknown `TargetType` → 400.

**Specificity order (decision, smaller = more specific = wins tie):** account-contact-link(1) < contact(2) < campaign-target(3) < account(4) < territory-node(5) < concept-node(6) < audience-profile(7) < segment(8).

## 7. Frequency / Period Policy

`FrequencyType`: weekly, biweekly, monthly, cycle-based, custom. `PeriodType`: day, week, month, quarter, cycle, campaign-period, custom.

Allowed matrix (conflicting combos → 400):

| FrequencyType | Allowed PeriodType |
|---|---|
| weekly | week |
| biweekly | **week or custom** (decision: a fortnight is a 2-week window or an explicit custom period) |
| monthly | month |
| cycle-based | cycle (+ `CycleId` or `CyclePeriodId` required) |
| custom | any (and **`Notes` required** for the free-form case) |

`PeriodType=campaign-period` ⇒ `CampaignId` required. Enums are central domain constants; MOD-0048 set codes are exposed on the contract for eventual alignment but **not published in FU03**.

## 8. Source / Status Policy

`Source`: campaign, segmentation, manual, legacy-import, business-rule, manager-override, other — audit-visible on every read and resolve candidate.
- **Decision:** `Source=campaign` ⇒ `CampaignId` **required**; `Source=segmentation` ⇒ `SegmentId` **required** (strong provenance).
- `manager-override` evaluated in the highest suggested priority band (100).

`Status`: draft, active, inactive, archived. Only `active` is selectable by the resolve provider; inactive/archived/draft are read-only history.

## 9. Priority / Conflict Resolution

`Priority` is a required numeric field (≥ 1); **smaller wins**. Suggested bands (`FrequencyPriorityBands`, UI hint only, never auto-defaulted): manager-override 100, campaign-target 200, account-contact-link 300, contact 400, account 500, segment 600, territory-node 700, concept-node 750, audience-profile 775, business-rule 800.

Deterministic order: (1) active + effective filter → (2) target match → (3) lowest Priority → (4) most specific TargetType → (5) latest EffectiveFrom → (6) stable PolicyId. No silent/random selection. `SelectedPolicy` + eliminated `CandidatePolicies[]` with per-candidate reason are returned. No policy ⇒ `unknown` (never a default). A same-band tie (priority + specificity + effectiveFrom equal) is resolved deterministically by PolicyId and flagged **`FrequencyStatus=conflict` (200 + diagnostics)**.

## 10. Resolve Provider Contract

`GET /api/crm/visit-frequency-policies/resolve` — **read-only, zero writes**. Query: `targetType, targetId, effectiveAt, businessUnit?, territoryNodeId?, campaignId?, segmentId?, brandId?, productId?, conceptNodeId?, audienceProfileId?, includeDiagnostics`.
Response: `FrequencyStatus, SelectedFrequencyPolicyId, SelectedPolicyCode, SelectedPolicyName, SelectionReason, RequiredVisitCount, FrequencyType, PeriodType, CycleId, CyclePeriodId, EffectiveFrom, EffectiveTo, Priority, Source, CandidatePolicies[], ReasonCodes[]`.
`FrequencyStatus` ∈ {resolved, unknown, conflict, not_applicable}. Reason codes per pack §H implemented in `FrequencyReasonCodes`. The result type carries **no** dueStatus/lastVisitDate/routeOrder/distance/travelTime/visitPlanId/consentAllowed field (asserted by a test).

## 11. CRUD / Authoring Endpoints

```
GET    /api/crm/visit-frequency-policies                    (list; filters targetType/targetId/status/source)
GET    /api/crm/visit-frequency-policies/contract
GET    /api/crm/visit-frequency-policies/resolve            (read-only provider)
GET    /api/crm/visit-frequency-policies/{policyId}
POST   /api/crm/visit-frequency-policies
PUT    /api/crm/visit-frequency-policies/{policyId}
POST   /api/crm/visit-frequency-policies/{policyId}/archive
```
No DELETE. Archive = soft lifecycle. Update of an archived policy → controlled 409. Duplicate non-archived PolicyCode → controlled 409. TenantId payload forbidden (claim-resolved). Exposed via Gateway (dedicated ocelot routes).

## 12. Contract Flags

Present (all true): `supportsVisitFrequencyPolicy`, `supportsCallCyclePolicy`, `supportsFrequencyPolicyPriority`, `supportsFrequencyPolicyEffectiveWindow`, `supportsFrequencyPolicyProvider`.
Absent (never emitted, even as false; asserted by a reflection test): `supportsVisitPlanning`, `supportsRoutePlanning`, `supportsDueOverdueEngine`, `supportsDigitalDetailing`, `supportsRecommendationEngine`, `supportsConsentEvaluationEngine`, `supportsWorkflowApproval`.

## 13. Consent Boundary

No consent evaluation engine; no consent record read/write; no campaign-target consent filter; unknown consent is never treated as granted. Frequency policy is a cadence rule only (MOD-0164/MOD-0155 own consent).

## 14. Campaign / Targeting Boundary

No campaign/CampaignTarget CRUD, no target snapshot, no dynamic target resolution. `Source=campaign` + `CampaignId` provenance and `TargetType=campaign-target` are supported; `campaignId` is a resolve context filter.

## 15. Segmentation Boundary

No segment engine, no membership computation, no CDP runtime. `Source=segmentation` + `SegmentId` provenance and `TargetType=segment` supported; segment-sourced provenance is visible in reads and resolve candidates.

## 16. Brand/Product Boundary

No brand/product master CRUD or duplication. `BrandId`/`ProductId` are optional context/filter fields; a policy with no brand/product is fully valid (non-pharma supported — covered by a test).

## 17. Knowledge/Content/Concept Boundary

No KnowledgeContent CRUD, no path/journey selection, no concept graph traversal, no recommendation engine. `concept-node`/`audience-profile` are future-compatible target types; `conceptNodeId`/`audienceProfileId` are resolve context ids only.

## 18. MOD-0151 FU09A Integration

MOD-0151 was **not modified**. FU09A still returns `FrequencyStatus=unknown` today because no provider is wired into it. Integrating this provider into FU09A route-candidate readiness is deferred to the follow-up **MOD-0151-FU09B — Frequency Provider Integration for Route Candidate Readiness** (read-only/feature-flagged; DueStatus must stay unknown, LastVisitDate null, no visit/route field, no invented default).

## 19. UI Boundary

API-only in this FU. The CRM/Campaign admin list/detail/create/edit/archive screen + resolve test panel + 7-language RESX parity are a **follow-up (PARTIAL on UI)**. No route/due/visit/campaign-target/consent UI is introduced.

## 20. Tests

`services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/VisitFrequencyPolicyTests.cs` — **32 tests, all green**. Full CrmService suite: **540 passed, 5 pre-existing skips, 0 failed**. Coverage maps to the pack §R checklist: create valid; tenant-from-claim; RequiredVisitCount≤0→400; EffectiveTo<EffectiveFrom→400; cycle-based without cycle→400; campaign-period without campaign→400; unknown TargetType→400; active/effective resolves; draft/archived not selected; out-of-window not selected; priority lower wins; specificity tie-break; latest EffectiveFrom tie-break; CandidatePolicies diagnostics include losers; no policy→unknown; same-band tie→conflict (deterministic); archive removes from resolve + preserves read; DELETE unsupported (no endpoint/command exists); brand/product optional; campaign/segment provenance visible; resolve is write-free; contract flags true; forbidden flags absent; no route/visit/due/lastVisit/consent field in the resolve result shape.

## 21. Authenticated Gateway Live Smoke

The fleet was restarted (`watch-diten-bg.ps1`) so the new code + ocelot routes load; Mongo was up. The service **booted cleanly with the new persistence wiring** (class-map + indexes) — the most important runtime check, since a bad partial index (`$ne`) or a missing class map would crash-loop the service at startup. It did not.

**Credential-free live checks (via Gateway 5000) — all PASS:**

| Check | Expected | Result |
|---|---|---|
| Fleet health (gateway / crm) | up | up (404 root, alive) |
| New route wired (contract) unauthenticated | 401 | **401** |
| List unauthenticated | 401 | **401** |
| Resolve unauthenticated | 401 | **401** |
| Get by id unauthenticated | 401 | **401** |
| `DELETE {policyId}` (unsupported) | 404/405 | **404** (no delete route exists) |
| Create (POST) unauthenticated | 401 | **401** |
| Garbage bearer token | 401 | **401** |

These prove: (a) the service boots with the FU03 aggregate, class map and indexes; (b) the Gateway `visit-frequency-policies` routes reach the authenticated CrmService endpoints (401, not 404); (c) auth is enforced; (d) DELETE is genuinely absent; (e) the unauthenticated negative case is 401.

**Authenticated positive business flow — BLOCKED on operator credential (PARTIAL).** Steps 4–8 of the pack §S smoke (login 200 → contract flags → create account-contact-link policy → resolve → priority/specificity policy → campaign-source policy → archive + re-resolve) require a valid **tenant `97c5…` operator credential** logging in at `POST /api/tenant-auth/login` + `X-Tenant-Id`. This is the exact credential dependency that gated the MOD-0151 FU09A smokes (see `mod-0151-fu09a-authenticated-gateway-live-smoke-retry-secure-credential-2026-08-02.md`), and entering a password to authenticate is outside what this agent may do on the user's behalf. The full create→resolve→archive semantics are instead proven by the **32 unit tests** in §20, which exercise the same handlers/engine end-to-end with an in-memory repository. When a credential is supplied (as in the prior FU09A closeout), the same authenticated flow can be run against these live routes with no code change.

## 22. Response Shape Guard

`VisitFrequencyResolveResult` reflection test asserts none of `dueStatus, lastVisitDate, routeOrder, distance, travelTime, visitPlanId, consentAllowed` exist on the type. The resolve DTO carries only frequency + provenance + diagnostics fields.

## 23. Data Mutation Guard

`Resolve_Does_Not_Mutate_State` asserts the repository write-count is unchanged across repeated resolves. The resolve handler calls only `ListActiveByTargetsAsync` (a read) and the engine is a pure function.

## 24. Guard Checks

- Frequency is its OWN aggregate — never embedded on Contact/Account/Campaign/Content. ✔
- No invented default frequency (no policy ⇒ unknown). ✔
- Resolve endpoint is GET/read-only, no writes. ✔
- No due/overdue, last-visit, route/visit planning, consent evaluation, campaign/segment runtime opened. ✔
- No brand/product/knowledge duplication. ✔
- No DELETE / hard delete. ✔
- Tests + build PASS. ✔

## 25. Created / Updated Files

**Created**
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/VisitFrequencyPolicy.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IVisitFrequencyPolicyRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/VisitFrequencyPolicyRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitFrequencyPolicy/VisitFrequencyPolicyReferenceSets.cs`
- `…/VisitFrequencyPolicy/VisitFrequencyPolicyPermissions.cs`
- `…/VisitFrequencyPolicy/VisitFrequencyPolicyValidation.cs`
- `…/VisitFrequencyPolicy/VisitFrequencyPolicyDtos.cs`
- `…/VisitFrequencyPolicy/VisitFrequencyPolicyMapper.cs`
- `…/VisitFrequencyPolicy/Commands/VisitFrequencyPolicyCommands.cs`
- `…/VisitFrequencyPolicy/Queries/VisitFrequencyPolicyQueries.cs`
- `…/VisitFrequencyPolicy/Handlers/VisitFrequencyPolicyCommandHandlers.cs`
- `…/VisitFrequencyPolicy/Handlers/VisitFrequencyPolicyQueryHandlers.cs`
- `…/VisitFrequencyPolicy/Resolve/VisitFrequencyResolveContracts.cs`
- `…/VisitFrequencyPolicy/Resolve/VisitFrequencyResolveEngine.cs`
- `…/VisitFrequencyPolicy/Contract/VisitFrequencyContract.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Models/CRM/VisitFrequencyPolicyRequests.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/VisitFrequencyPoliciesController.cs`
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/VisitFrequencyPolicyTests.cs`
- `docs/audits/mod-0165-fu03-visit-frequency-call-cycle-policy-implementation-2026-08-02.md`

**Updated**
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs` (repo registration, class map, indexes)
- `gateway/Diten.ApiGateway/ocelot.json` (visit-frequency-policies routes)

## 26. Final Verdict

**PARTIAL** (strong): the VisitFrequencyPolicy runtime is fully implemented and the API is production-shaped and green.

PASS elements:
- VisitFrequencyPolicy runtime implemented as its own aggregate (never embedded on Contact/Account/Campaign/Content).
- CRUD/read/archive implemented **without DELETE**; archive is soft lifecycle; PolicyCode stable.
- Resolve provider implemented **read-only** (write-free, asserted by test + live 401/method guards).
- Priority/conflict rules deterministic; `CandidatePolicies[]` diagnostics visible; no policy ⇒ `unknown` (never a default); same-band tie ⇒ deterministic `conflict`.
- Contract flags correct; forbidden planning/detailing/recommendation/consent/workflow flags absent.
- No route/visit/due/lastVisit/consent field leaked; existing MOD-0150/0151/0162/0164/0167/0290 boundaries preserved.
- **Tests + build PASS** (32 FU03 tests; 540 full suite; 0 failures). Service **boots** with the new wiring; Gateway routes wired; unauthenticated → 401; DELETE → 404.

PARTIAL elements (both explicitly allowed by the pack's PARTIAL criteria):
- **UI not completed** — API-only; CRM admin list/detail/create/edit/archive + resolve panel + 7-lang RESX is a follow-up.
- **Authenticated positive live smoke limited** — boot + routing + negative-auth + method guards pass live; the create→resolve→archive business flow is blocked on a tenant-`97c5` operator credential (same blocker as FU09A) and is covered by the unit tests.

No FAIL condition is present (frequency is its own aggregate; no invented default; resolve is write-free; no due/overdue/last-visit/route/visit/consent/campaign/segment runtime opened; no brand/product/knowledge duplication; no DELETE; tests/build pass).

## 27. Next Recommended Prompt

`MOD-0151-FU09B — Frequency Provider Integration for Route Candidate Readiness`
