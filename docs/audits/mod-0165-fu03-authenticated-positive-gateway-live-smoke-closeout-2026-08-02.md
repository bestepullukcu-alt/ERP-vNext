# MOD-0165-FU03 — Authenticated Positive Gateway Live Smoke Closeout

**Date:** 2026-08-02
**Module:** MOD-0165 FU03 (Visit Frequency / Call-Cycle Policy)
**Service:** Diten.CrmService (Gateway 5000, CRM 5061)
**Target tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
**Reference:** `docs/audits/mod-0165-fu03-visit-frequency-call-cycle-policy-implementation-2026-08-02.md`
**Smoke driver:** `scripts/smoke-mod0165-fu03-visit-frequency-authenticated.ps1`

---

## 1. Preflight

Fleet restarted this session and healthy (all business calls via Gateway 5000; direct 5061 reserved for `/health`):

| Service | Port | Result |
|---|---|---|
| Gateway | 5000 | up (serving) |
| Web | 5001 | up |
| Auth | 5056 | up |
| Platform | 5057 | up |
| CRM | 5061 | up (booted with FU03 wiring — no crash-loop) |
| MongoDB | 27017 | up (TCP) |

TenantId is never sent in a payload (the create request model has no TenantId field; an injected value is ignored — verified by test `Create_Without_Tenant`/claim binding and by the smoke's TenantId-injection check). Credential/token/password are never written to this report.

## 2. Previous PARTIAL Summary

FU03 shipped **strong PARTIAL**: runtime, 32 FU03 tests, 540-test suite, build, Gateway routing, boot, and the negative/unauthenticated smoke all PASS. The single open item was the authenticated `create → resolve → archive` business flow, which requires a tenant-`97c5` operator credential.

## 3. Secure Credential Handling

**Compliance constraint (why the agent did not log in):** entering a password (or a bearer token) into a field to authenticate is an action the assistant must not perform on the user's behalf, even when authorized. Per that rule the authenticated flow is executed by the **user** via the provided script, which:
- reads the credential with `Get-Credential` (kept in the user's process memory only),
- drops the plaintext password immediately after building the login body,
- never writes the password/token/cookie to any file,
- masks the `Authorization` header (prints `MASKED`; only the JWT `tenant_id` claim is read for verification),
- uses no hardcoded token, no token/cookie bypass, no Mongo-minted session, and no direct-5061 business call.

## 4. Contract Smoke  _(authenticated — executed 2026-08-03)_

`GET /api/crm/visit-frequency-policies/contract` → **200**. `supportsVisitFrequencyPolicy / supportsCallCyclePolicy / supportsFrequencyPolicyPriority / supportsFrequencyPolicyEffectiveWindow / supportsFrequencyPolicyProvider = true` (**PASS**); the seven forbidden flags **absent** (**PASS**).

## 5. Positive Create Smoke  _(authenticated — executed)_

`POST …/visit-frequency-policies` — monthly / account-contact-link / RequiredVisitCount 2 / PeriodType month / EffectiveFrom 2026-08-02 / EffectiveTo 2027-07-31 / Priority 300 / Source manual / Status active, PolicyCode `SMOKE-20260803000711-A`. → **201**, PolicyId `3d88dee1-9f7c-4b58-a71b-deea0023f16d`. TenantId payload (`ffffffff-…`) **ignored** — the row is readable in the claim tenant with `status=active` (**PASS**).

## 6. Resolve Smoke  _(authenticated — executed)_

`GET …/resolve?targetType=account-contact-link&targetId=<A.TargetId>&effectiveAt=2026-08-11T09:00:00Z&businessUnit=gamma&includeDiagnostics=true` → **200**, `FrequencyStatus=resolved`, `SelectedPolicyCode=SMOKE-20260803000711-A`, `RequiredVisitCount=2`, `FrequencyType=monthly`, `PeriodType=month`, `Source=manual`, `CandidatePolicies[]` present, `ReasonCodes=[frequency_policy_resolved, policy_selected_by_priority]`. No default invented; resolve wrote nothing (**PASS**).

## 7. Priority / Specificity Smoke  _(authenticated — executed)_

Policy **B** (same target, Priority 500) loses on priority; policy **C** (segment target, Priority 300, resolved with `segmentId` context) loses on specificity — account-contact-link (rank 1) beats segment (rank 8). Result: **A selected** (`SMOKE-20260803000711-A`), B and C both visible in `CandidatePolicies[]`, `SelectionReason = "Selected SMOKE-20260803000711-A by most specific target type."` No silent/random selection (**PASS**).

## 8. Campaign-Source Smoke

Covered by unit tests only in this closeout (no campaign fixture required, and the pack permits test-limited coverage): `Create_Segmentation_Source_Requires_SegmentId`, `Campaign_Segment_Source_Provenance_Is_Visible`, and the validator rule `Source=campaign ⇒ CampaignId required` (create without CampaignId → 400). No campaign engine / CampaignTarget is opened — provenance/context only.

## 9. Archive Smoke  _(authenticated — executed)_

`POST …/{A}/archive` → **200**; A now `status=archived`, `ArchivedAt=2026-08-02T21:07:28…+00:00` set, still readable. Resolve then no longer selects A and **falls to B** (`SMOKE-20260803000711-B`). A target with no policy → `FrequencyStatus=unknown`, `RequiredVisitCount=null` (never a default). No DELETE used (**PASS**).

## 10. Negative / Auth Guards

**Credential-free — verified live this session (PASS):**

| Check | Expected | Result |
|---|---|---|
| No-token contract | 401 | **401** |
| No-token resolve | 401 | **401** |
| No-token create (POST) | 401 | **401** |
| Garbage bearer token | 401 | **401** |
| `DELETE {policyId}` (unsupported) | 404/405 | **404** |

**Authenticated — executed (PASS):** update archived policy → **409**; RequiredVisitCount≤0 → **400**; EffectiveTo<EffectiveFrom → **400**; unknown TargetType → **400**; TenantId payload **ignored** (row in claim tenant). (All also asserted by unit tests.)

> Note on the smoke harness: the first user run reported 9 "fails" that were all `-1` — the script's status-probe used `-SkipHttpErrorCheck`, a PowerShell 7+ only parameter, so under Windows PowerShell 5.1 those 6 preflight/health probes and 3 negative-auth probes could not read a status code. Every one of them was independently verified live (health up; no-token → 401, garbage → 401, DELETE → 404) and the script was made PS 5.1-compatible. **Zero business assertions failed.**

## 11. Response Shape Guard

Unit test `Resolve_Result_Has_No_Route_Visit_Due_LastVisit_Consent_Fields` asserts the resolve type has none of: `dueStatus, lastVisitDate, routeOrder, distance, travelTime, visitPlanId, consentAllowed`. The script additionally grepped the live authenticated resolve JSON for `dueStatus/lastVisitDate/routeOrder/distance/travelTime/visitPlanId/routeId/consentAllowed/consentStatus/dailyPlanId` → **none present**.

_Live grep result: **PASS** (test-level: PASS)._

## 12. Data Mutation Guard

Unit test `Resolve_Does_Not_Mutate_State` asserts repeated resolves perform zero writes. The script re-checked list `total` before/after two resolves → **6 == 6** (unchanged). No Contact/Account/Campaign/Content/Brand/Product master is mutated; no direct Mongo edit; no hard delete.

_Live count result: **PASS** (test-level: PASS)._

## 13. Tests / Build

- FU03 focused tests: **32 passed / 0 failed** (re-run this session).
- Full CrmService suite: **540 passed / 5 skipped / 0 failed** (prior run; source unchanged).
- CrmService build: **PASS** (isolated-output build, 0 errors); the fleet rebuilt and booted the service cleanly.

## 14. Guard Checks

- Frequency is its own aggregate — never embedded on Contact/Account/Campaign/Content. ✔
- Resolve is read-only / write-free. ✔ (test + live method/negative guards)
- No default frequency invented (no policy ⇒ unknown). ✔ (test; live PENDING)
- DELETE / hard delete absent. ✔ (live 404)
- No due/overdue, last-visit, route/visit planning, consent evaluation, campaign/segment runtime opened. ✔
- No direct-5061 business call; no TenantId payload; credential never persisted. ✔

## 15. Created / Updated Files

**Created**
- `scripts/smoke-mod0165-fu03-visit-frequency-authenticated.ps1` (user-run authenticated smoke driver)
- `docs/audits/mod-0165-fu03-authenticated-positive-gateway-live-smoke-closeout-2026-08-02.md` (this report)

**Unchanged:** all FU03 runtime files from the implementation report (no code change was needed for the smoke).

## 16. Final Verdict

**PASS.** The user ran `scripts/smoke-mod0165-fu03-visit-frequency-authenticated.ps1` against tenant `97c5…` on 2026-08-03; the credential stayed in the user's process memory and no secret was written to this report.

- Authenticated Gateway login **200**, tenant claim = `97c59330-dbc4-4665-b29c-0c26dbb5cc93`. ✔
- Contract flags correct; forbidden flags absent. ✔
- Create policy **201** + PolicyId; TenantId payload ignored. ✔
- Resolve returned the selected policy (`resolved`, 2 / monthly / month / manual) with visible `CandidatePolicies[]` and reason codes. ✔
- Priority/specificity diagnostics correct (A over B on priority, over C on specificity; both losers visible; reasoned selection). ✔
- Archive → 200 + `ArchivedAt`; resolve no longer selects the archived policy and **falls to the next candidate**; no-policy target → **unknown** (never a default). ✔
- DELETE unsupported (404); resolve is write-free (count 6 → 6); response shape carries no route/visit/due/last-visit/consent field. ✔
- Direct 5061 used only for `/health`; no TenantId payload; credential never persisted. ✔
- FU03 tests (32) + full suite (540) + build PASS.

Every business assertion passed. The only "fails" in the first run were a PowerShell 5.1 harness artifact (`-SkipHttpErrorCheck`), cross-verified live and since fixed — no product/runtime failure. No FAIL condition is present. (UI remains a separately-tracked follow-up, out of this smoke-closeout's scope.)

## 17. Next Recommended Prompt

On the user-run smoke returning all PASS:
`MOD-0151-FU09B — Frequency Provider Integration for Route Candidate Readiness`
