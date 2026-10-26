---
id: MOD-0147
name: Supplier Performance & Risk
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Review draft; v1 scope revision 2026-10-22, D2 eligibility and RCS-001 added 2026-10-26 (§0.0): v1 = supplier performance scorecard with evaluator-entered metric values, risk part after MOD-0216 TPRM. No runtime code authority until this revision has CT-APR-02 and CT-APR-01 and the §0.0 gates (DC-02 / MOD-0140 1.3.0, PR #198, permissions, replay receipt) are met. Placement CONCURRED (DC-01, 2026-10-18). SUPPLIER-BASE frozen (supplier.openapi.yaml v1.2.0, 780f8368a)."
owner: supply-chain-execution / control-tower
branch: feature/sce/mod-0147-supplier-performance-risk
started: 2026-09-15
target: 2026-12-31
form_field_count: 0
---

# MOD-0147 — Supplier Performance & Risk

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0147 --name "Supplier Performance & Risk"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and again on 2026-10-19 against Blueprint 8.8 and the module ID
> registry.
>
> **Phase A only:** this draft records the proposed initial Phase B backend/contract slice. It does not authorize
> runtime code, service scaffold, gateway, permission-registry or integration changes.
>
> **Placement (CONCURRED, DC-01 2026-10-18):** MOD-0147 is a Blueprint Procurement Suite (P2P) module placed in
> `supply-chain-execution` / `Diten.SupplyChainService` for delivery (`docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md`). MOD-0140 remains the Supplier
> master owner (domain `procurement`). Placement grants no runtime, contract, gateway or permission ownership.

## 0.0 v1 scope — performance scorecard now, risk after TPRM (owner decision 2026-10-22) — revision draft

> `docs/records/decisions/2026-10/mvp6-mod0147-v1-scorecard-split-owner-decision-2026-10-22.md` (Natig). **Draft revision —
> not approved**; needs CT-APR-02 and a new CT-APR-01 row (high risk: first product code). Where this section and an older
> line below disagree about v1, this section wins.

| Area | v1 (now) | Later |
|---|---|---|
| Operations | `createSupplierEvaluation`, `submitSupplierEvaluation`, `GET /evaluations[/{id}]`, `GET /scorecards[/{id}]` | risk operations (`POST /risks`, `POST /risks/{id}/status`, `GET /risks`) — after MOD-0216 TPRM, reading TPRM's result |
| Objects | `SupplierEvaluation`, `EvaluationMetricResult`, `SupplierScorecard` | `SupplierRisk` |
| Metric policy (DC-04) | pack-held revision `supplier-score-policy/1` = §0.4 set | Metric Registry (MOD-0004) when it exists |
| Metric values | **entered by the evaluator** per metric (frozen v1 contract shape); server enforces codes, weights, ranges, 3-of-4 rule | **v1.1:** computed from Procurement facts (read contracts MOD-0141/0142/0143 + QC source; XMC-001); 5-receipt-line minimum applies from v1.1 |
| Supplier (DC-02) | tenant-wide supplier from MOD-0140 1.3.0; eligibility per **DC-02 D2** (below) | — |
| Permissions | `create` → `.evaluate`; `submit` → `.publish`; the four GETs → `.read`; one person may hold both `.evaluate` and `.publish` (no separation of duty in v1 — Natig, 2026-10-22) | `.risk-manage` |
| Audit (§21) | path b, `supplychain-merkezi-iletim` (central forwarder) — **valid from the moment PR #198 is on `main`**; written so now, so the merge does not change this pack | risk commands with the risk part |
| UI | none in v1 (backend first, as §9); a scorecard screen is a later revision with 7-language scope | — |

**Still gates for v1 code:** DC-02 (MOD-0140 1.3.0 contract **frozen on `main`** — MOD-0140 Revision 2 DL-1), PR #198 on
`main` (audit path b), this revision's CT-APR-02 + CT-APR-01, the permission/composition single writer (§14), and the replay
receipt amendment (Phase 1.5, §0 table).

**Supplier eligibility — DC-02 D2 (Natig 2026-10-23 Q1/Q2, 2026-10-26 Q3/Q4; Cihan's MOD-0140 Revision 2,
`origin/feature/mvp2/supplier-tenant-01` @ `7f937c812`, §3.1 `SupplierEligibility`):**

- At create, the service reads `GET /api/suppliers/{supplierId}/eligibility` (SUPPLIER 1.3.0) through the Gateway with the
  caller's token and `X-Legal-Entity-Id` = the evaluation's validated LE (XMC-001, read only; no copy of Procurement data).
  The evaluating role needs `procurement.suppliers.eligibility.read` in addition to `.evaluate`, granted through Roles →
  Permissions (the BL-652 interim path).
- `relationshipExists = false` (unknown supplier or no relationship with the caller's company) → **404** (declared for
  create; the same answer as a supplier of another tenant or company, so nothing leaks), no draft.
- Any relationship `status` (Active, OnHold, Blocked, Inactive) and any `onboardingStatus` **may be evaluated** (Q1, Q2): a
  block stops new purchasing (`eligibleForTransactions`), not the record of past performance. `eligibleForTransactions` is
  not used by MOD-0147.
- The evaluation stores the **canonical** `supplierId` returned by the answer (a legacy code or uuid in the request is
  resolved), so one supplier never gets two evaluation series.
- Procurement unreachable / 5xx / timeout → 503 `DEPENDENCY_UNAVAILABLE` (declared). Procurement 403 (the caller lacks
  `procurement.suppliers.eligibility.read`) → 403 `INVALID_REQUEST` — an authorization outcome, never hidden as 503; create
  declares no 403, so this is the same framework-level exception as the endpoint's own `[HasPermission]` 403 and goes into
  the contract successor's list. No draft in any case.
- Publish (`submitSupplierEvaluation`) does not re-read eligibility: the evaluation records a past period.

**Visible code (RCS-001, BL-565):** evaluations and scorecards are new record types, but the shared record-code service
(BL-565) does not exist yet. v1 shows **no visible code** — only the frozen contract's opaque `evaluationId` /
`scorecardId` — and never creates its own counter or format. When BL-565 exists, a later revision takes the visible code
(`MG-…` form) from that service; the opaque ids stay the keys.

**Rules that bind v1** (owner decisions 2026-10-21 and 2026-10-22; where §0.1 / §0.2 say ÖNERİLEN or BLOKE, this list wins
for v1):

- From §0.1: unique codes; `PERCENT`, `0..100`, `HIGHER_IS_BETTER`; at most four fractional digits; negative or excess
  values → 422, no silent rounding; aggregate in exact decimal with no intermediate rounding; overall rounded **once** to
  two decimals, round-half-even; band from the **unrounded** aggregate: `[90,100] LOW`, `[75,90) MEDIUM`, `[50,75) HIGH`,
  `[0,50) CRITICAL` (bands decided by Natig 2026-10-22); the band never creates or changes a `SupplierRisk`.
- **Missing metrics (decided rule, implementation confirmed by Natig 2026-10-22):** the client sends only the metrics that
  have data, each with its policy weight `25`; `overallScore = Σ(measuredValue × weight) / Σ(weight)` over the sent metrics,
  exact decimal — a re-scaled weight never enters the computation and the response `weight` is the policy weight (25); a
  missing metric is shown by its absence from `metrics[]` (the v1 wire has no other marker). Fewer than **3** metrics, an
  unknown code, a duplicated code, a fifth metric or a weight other than 25 → **422 `BUSINESS_RULE_VIOLATION`** at create,
  and no `DRAFT` is created. This replaces §0.1's "total weight exactly 100" and "sum / 100" for v1.
- **Exact computation (review round 2, F-1):** with three metrics the quotient need not be a finite decimal (e.g.
  6025 / 75 = 80.3333…). So the band is decided **without dividing**: `Σ(value × 25) ≥ threshold × Σ(25)` against 90, 75 and
  50; and `overallScore` is produced by **one** decimal division rounded half-even to two places
  (`round_half_even(Σ(value × 25) / Σ(25), 2)`), never by dividing first at a chosen precision and rounding again.
- Codes are exactly `OTD`, `QTY_ACC`, `QUAL_ACC`, `INV_MATCH`. The frozen contract's examples (`ON_TIME_DELIVERY`,
  `QUALITY_ACCEPTANCE`) are not normative and answer 422 in v1; fixtures use the v1 codes.
- **Period:** `periodStart` = the first day and `periodEnd` = the last day of the same calendar month (UTC dates), otherwise
  422; the contract's quarterly example is a 422 in v1. From §0.2: at most one active evaluation per Tenant + LE + supplier +
  period (`DRAFT`/`PUBLISHED`), second create → 409. There is no correction path (`CorrectionOf` is not in the v1 wire) and
  no `CANCELLED` (no cancel operation).
- Not applicable in v1: the live registry pointer, validity/withdrawal oracle and the 503 "metric authority unavailable"
  path (AC-21) — the policy is pack-held.

v1 acceptance examples (policy `supplier-score-policy/1`; §0.1's 60/40 and 100-weight examples are unit tests of the
arithmetic only, not v1 inputs — under v1 they are 422):

| v1 input | Result |
|---|---|
| OTD 90, QTY_ACC 80, QUAL_ACC 100, INV_MATCH 70 (each weight 25) | 85.00, MEDIUM |
| OTD 90, QTY_ACC 80, INV_MATCH 70 (QUAL_ACC absent) | 240·25/75 = 80 → 80.00, MEDIUM |
| OTD 90, QTY_ACC 90, INV_MATCH 90 (QUAL_ACC absent) | 90.00, **LOW** |
| OTD 90, QTY_ACC 80, INV_MATCH 71 (QUAL_ACC absent) | 6025/75 = 80.3333… → 80.33, MEDIUM (band: 6025 ≥ 75 × 75 = 5625 and < 90 × 75 = 6750) |
| OTD 89.995, QTY_ACC 89.995, QUAL_ACC 89.995, INV_MATCH 89.995 | 90.00, **MEDIUM** (band from the unrounded 89.995) |
| two metrics | 422 `BUSINESS_RULE_VIOLATION`, no draft |
| a weight of 33.3333 or 40 | 422 |
| period 2026-07-01 … 2026-09-30 | 422 (not one calendar month) |

v1 test IDs (added with this revision; the r2 test map stays unchanged and these IDs go into the next test-map revision, r3,
when the folder's hash manifests are renewed with it): T-0147-V1-SCORE-01 (the table above),
T-0147-V1-MISS-01 (3-of-4 and absence), T-0147-V1-CODE-01 (codes, duplicates, fifth metric, weight 25),
T-0147-V1-PER-01 (calendar month), T-0147-V1-BAND-01 (bands from the unrounded value), T-0147-V1-PERM-01 (§14 mapping),
T-0147-V1-ELIG-01 (D2: no relationship → 404; OnHold, Blocked, Inactive and not-approved
onboarding → 201; legacy code stored as the canonical `supplierId`; Procurement 403 → 403, 503 → 503, no draft; sabotage:
skipping the read makes the no-relationship case red).

## 0. Requirement status and traceability (review 2026-10-19)

Every requirement below carries one of three labels. A label is a claim about authority, not about code (there is none).

| Label | Meaning |
|---|---|
| **ONAYLI** | Fixed today: the frozen wire surface of `SUPPLIER-PERFORMANCE` v1.0.0 FROZEN (`docs/analysis/contracts/supplier-performance.openapi.yaml`, `eb1306590`, sha256 `e311cd102bed0041…`), or a CONCURRED gate. |
| **ÖNERİLEN** | Selected target written in `docs/roadmap/plans/mvp6-mod0147-spec-split-01/SPEC-DELTA.md` (`ce3396656`, sha256 `68732d5ae31740e0…`). **Not merged into this pack** — that delta may be merged only after its named owner artifacts are exact and hash-bound. Reviewable, not binding. |
| **BLOKE** | Depends on an OPEN gate in `docs/roadmap/plans/mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md`; the named owner must close it. A fixture or mock never closes it. |

Where this pack's own text (sections 1–21) states a behaviour as a rule and the label below says ÖNERİLEN or BLOKE, **the
label wins**: the text is the target, not an approved rule.

| Endpoint (operationId) | Wire | Behaviour | Gate / decision owner | Tests (`ACCEPTANCE-TEST-MAP.r2.tsv`) |
|---|---|---|---|---|
| `POST /evaluations` (createSupplierEvaluation) | ONAYLI | **BLOKE** — Supplier eligibility (DC-02, MOD-0140). v1: metric policy = pack-held `supplier-score-policy/1` and the v1 rules of §0.0 (DC-04 no longer blocks v1) | MOD-0140 owner | AC-03, AC-04; v1: T-0147-V1-SCORE-01, -MISS-01, -CODE-01, -PER-01, -BAND-01; AC-18, AC-19, AC-22 as far as they agree with §0.0; **AC-20 and AC-21 do not apply in v1** |
| `POST /evaluations/{id}/submit` (submitSupplierEvaluation) | ONAYLI | v1: snapshot of `supplier-score-policy/1` (DC-04 no longer blocks v1); atomic publication ÖNERİLEN (§0.3) | MOD-0147 owner | v1: T-0147-V1-SCORE-01, T-0147-V1-BAND-01; AC-22 (revision snapshot = `supplier-score-policy/1`) |
| `GET /evaluations`, `GET /evaluations/{id}` | ONAYLI | ÖNERİLEN — Tenant+LE isolation, 404 without leakage | MOD-0147 owner | T-0147-ISO-01 |
| `GET /scorecards`, `GET /scorecards/{id}` | ONAYLI | ÖNERİLEN — immutable publication | MOD-0147 owner | AC-22 (revision snapshot), T-0147-ISO-01, T-0147-IMM-01 |
| `POST /risks` (registerSupplierRisk) | ONAYLI | **BLOKE** — risk taxonomy (DC-05, Risk Register — deferred), DC-02; lifecycle ÖNERİLEN | Risk Register owner; MOD-0140 owner | AC-23…AC-26 |
| `POST /risks/{id}/status` (changeSupplierRiskStatus) | ONAYLI | ÖNERİLEN — linear `OPEN→ACKNOWLEDGED→MITIGATED→CLOSED`; **BLOKE** — taxonomy snapshot (DC-05) | Risk Register owner | AC-23, AC-24 |
| `GET /risks` | ONAYLI | ÖNERİLEN — Tenant+LE isolation | MOD-0147 owner | T-0147-ISO-01 |
| Risk source `PORTAL_SUBMISSION` | ONAYLI enum | **BLOKE** — portal source resolution (with MOD-0148) | MOD-0148 owner; Risk owner | AC-25 |
| Replay receipt tuple / error precedence (all four mutations) | not declared in v1 | **BLOKE** — MOD-0147 Phase 1.5 amendment; 409 `IDEMPOTENCY_KEY_REUSED` not in v1 | MOD-0147 owner; CT contract writer | — (Phase 1.5) |
| Permissions, Program.cs composition | — | **BLOKE** — security/permission single writer | security owner, integration owner | — |
| Placement / Supplier pointer / frozen contract hash | — | ONAYLI (DC-01) / pointer re-pinned to SUPPLIER v1.2.0 / hash guard | — | AC-01 (r2: CONCURRED), AC-02, AC-07 (hash only) |
| Fixtures (`SIMULATED-ONLY`) | — | never evidence for a live producer | CT fixture coordinator | AC-06, AC-27 |


**Test map revision r2 (2026-10-19).** The original `ACCEPTANCE-TEST-MAP.tsv` and `SHA256SUMS` stay byte-identical: four records pin their hashes. The controlled revision is additive — `ACCEPTANCE-TEST-MAP.r2.tsv` (AC-01 oracle refreshed for DC-01; the `T-…` rows below added) and `SHA256SUMS.r2`, which records the folder's current bytes. The original `SHA256SUMS` already failed on `CONCURRENCE-OPEN.md` since `12416679d` (DC-01 edit, sums not updated); r2 records that file's current hash. r2 is the map this pack follows.

Test IDs added in r2 (MOD-0147) — the behaviours are proposed (§0.2, §0.3); the IDs are in `ACCEPTANCE-TEST-MAP.r2.tsv`:

| ID | Asserts | Rule |
|---|---|---|
| T-0147-ISO-01 | Every read (evaluations, scorecards, risks) with a foreign Tenant or LE → 404, no existence detail | §8 |
| T-0147-IMM-01 | A published scorecard never changes; a newer metric/policy revision does not rewrite it | §0.1, §8 |
| T-0147-PER-01 | Second create for the same Supplier+period **and the same `CorrectionOf`** (sequential and concurrent) → 409, one active record | §0.2 |
| T-0147-PER-02 | A `DRAFT` and a `PUBLISHED` for one Supplier+period **and the same `CorrectionOf`** cannot coexist; an original and its correction (different `CorrectionOf`) can | §0.2 |
| T-0147-COR-01 | A correction target is checked in this order and the first failure wins: (1) not a `PUBLISHED` scorecard of the same Tenant+LE+Supplier+period → 422; (2) superseded — it already has a published correction → 422; (3) an active `DRAFT` correction of it exists → 409. Only a target passing all three gets a new correction — BLOKE until the contract successor | §0.2 |
| T-0147-TXN-01 | Transaction abort leaves no state, receipt, audit or outbox record | §0.3 |
| T-0147-TXN-02 | A duplicate delivery of one outbox event is ignored by the consumer (EventId) | §0.3 |
| T-0147-TXN-03 | A crash between commit and publish is recovered by the publisher from the Pending record | §0.3 |

### 0.1 Score policy — ÖNERİLEN (SPEC-DELTA "Evaluation delta"), BLOKE on DC-04/DC-05

The selected target, by reference — not merged:

- Metrics: unique codes, unit `PERCENT`, direction `HIGHER_IS_BETTER`, measured `0..100`, weights `>0..100`, **total weight
  exactly 100**, at most four fractional digits; negative values or excess scale → 422, no silent rounding. Client weight
  must equal the registry policy weight (`supplier-score-policy/1`).
- Aggregate `sum(measuredValue × weight) / 100` in exact decimal, no intermediate rounding; overall rounded **once** to two
  decimals, **round-half-even**; display scores never feed the aggregate.
- Band from the **unrounded** aggregate: `[90,100] LOW`, `[75,90) MEDIUM`, `[50,75) HIGH`, `[0,50) CRITICAL`. The band never
  creates or changes a `SupplierRisk`.
- Evaluation and scorecard carry the metric revision, policy revision, weights, inputs, Tenant+LE scope and source hash
  snapshot; a newer revision never rewrites them. Missing, stale or unverifiable authority → no evaluation, receipt, audit
  success record or outbox item.

**Decided 2026-10-21 (Natig, management; §0.4):** the v1 metric set, `policy1` weights (25 each), the period (calendar
month), the missing-metric rule (replaces "every policy metric is required") and the minimum volume.
**Still for the Metric/Risk owners to decide (BLOKE) — after v1:** the live registry pointer and immutable revision shape
(until the Metric Registry — MOD-0004 — exists, the §0.4 set is the pack-held revision `supplier-score-policy/1`); validity
and withdrawal oracle. Band consent for v1 is given (Natig, 2026-10-22, §0.0).

Derived acceptance examples (from the proposed policy; they become acceptance examples only when it is approved):

| Inputs (value × weight) | Aggregate (exact) | Overall (half-even, 2dp) | Band (from exact) |
|---|---|---|---|
| 92.5 × 60, 80 × 40 | 87.5 | 87.50 | MEDIUM |
| 89.995 × 100 | 89.995 | 90.00 | **MEDIUM** (band uses the unrounded value) |
| 75.125 × 100 | 75.125 | 75.12 (tie → even digit 2) | MEDIUM |
| 49.995 × 100 | 49.995 | 50.00 | **CRITICAL** (unrounded 49.995 < 50) |
| 60 × 60, 40 × 39.9999 | — | — | 422 (total weight 60 + 39.9999 = 99.9999 ≠ 100) |
| 60 × 60, 39.9999 × 40 | 51.99996 | 52.00 | HIGH (weights sum to 100 — a valid input, not a negative case) |
| 50.12345 × 100 | — | — | 422 (five fractional digits) |

### 0.4 v1 fixed metric set — DECIDED (Natig, 2026-10-21) — revision draft

> Source: `docs/records/decisions/2026-10/mvp6-mod0147-v1-metric-set-owner-decision-2026-10-21.md`. Timing changed on
> 2026-10-22 (`mvp6-mod0147-v1-scorecard-split-owner-decision-2026-10-22.md`): v1 (this scorecard) starts now; only the risk
> part waits for MOD-0216 TPRM. This section is the pack text of the metric decision and needs CT-APR-02 and a new CT-APR-01
> row like any pack change. It authorizes no code by itself. In v1 the values are entered by the evaluator; the formula
> column is what v1.1 computes (and what an evaluator follows when entering values).

Scope per Tenant + LE + supplier + period; every metric `PERCENT`, `0..100`, `HIGHER_IS_BETTER` (§0.1).

| code | metric | formula (period) | weight | source (read contract + XMC-001 needed) |
|---|---|---|---|---|
| `OTD` | On-time delivery rate | receipt lines received on or before the promised date — up to 3 days early counts as on time — ÷ receipt lines × 100 | 25 | MOD-0141 PO line promised date, MOD-0142 GRN receipt date |
| `QTY_ACC` | Quantity accuracy | receipt lines whose received quantity is within ±2 % of the ordered quantity ÷ receipt lines × 100 | 25 | MOD-0141 ordered qty, MOD-0142 received qty |
| `QUAL_ACC` | Quality acceptance rate (= 100 − reject rate) | accepted quantity ÷ inspected quantity × 100 | 25 | QC inspection result — **source module not yet identified** |
| `INV_MATCH` | Invoice match rate (= 100 − mismatch rate) | invoices passing 3-way match without exception ÷ invoices × 100 | 25 | MOD-0143 Invoice Capture & 3-Way Match |

- **Period:** calendar month (§0.2's duplicate-period rule applies to it).
- **Missing data:** a metric with no data in the period is left out and the remaining metrics carry the score (the exact
  v1 computation is in §0.0: Σ(value × 25) / Σ(25)); an evaluation needs **at least 3 of the 4** metrics, and the missing
  metric is shown by its absence.
- **Minimum volume (from v1.1):** fewer than **5 receipt lines** in the period → "insufficient data", no score, no band. The
  v1 contract carries no receipt count, so v1 cannot enforce it.
- Rounding and bands stay as in §0.1 (exact aggregate, two decimals round-half-even once, band from the unrounded value).
- Changing weights, tolerances or the period later is a new policy revision; published scorecards are never rewritten.

Acceptance examples of the decided rules (in addition to §0.1's):

| Inputs | Result |
|---|---|
| OTD 90, QTY_ACC 80, QUAL_ACC 100, INV_MATCH 70 (each × 25) | aggregate 85 → 85.00, MEDIUM |
| OTD 90, QTY_ACC 80, QUAL_ACC no data, INV_MATCH 70 | Σ(value × 25) / 75 = 80 → 80.00, MEDIUM; QUAL_ACC absent from `metrics[]` |
| two of four metrics with data | 422 at create, no draft |
| 4 receipt lines in the month (from v1.1) | "insufficient data", no score |
| a line received 3 days before the promised date | on time; 4 days early → not on time |
| ordered 100, received 102 / 103 | within ±2 % / outside |

### 0.2 Evaluation period, correction and CANCELLED — proposal of this review (not in SPEC-DELTA), MOD-0147 owner decides

- Duplicate period (proposed): at most **one active** evaluation per `(TenantId, LegalEntityId, SupplierId, PeriodStart,
  PeriodEnd, CorrectionOf)`, active = `DRAFT` or `PUBLISHED`. Enforced by a unique index on exactly those fields — **`Status` is
  not in the key**, so a `DRAFT` and a `PUBLISHED` for one period cannot coexist — whose partial filter selects only active
  records (a server-maintained `IsActivePeriod` boolean, true for `DRAFT`/`PUBLISHED`; the exact filter form is verified when
  implemented). A second create — sequential or concurrent — gets 409 and nothing commits. `CorrectionOf` is `null` for an
  original evaluation. In v1 every period is one calendar month (§0.0), so periods never partly overlap.
- `CANCELLED` (proposed): v1 has **no cancel operation**, so this slice never produces `CANCELLED`; the enum value stays
  unused until a cancel command is approved in a contract successor.
- Correction (BLOKE): scorecards are immutable; a correction is a new evaluation and publication with `CorrectionOf` =
  the scorecard it corrects. Because `CorrectionOf` is part of the uniqueness key, a correction does not collide with the
  original's active record, and only one active correction per corrected scorecard exists.
  - **Target rule (proposed):** `CorrectionOf` must name a `PUBLISHED` scorecard of the **same Tenant, LE, Supplier and
    period** as the correction; anything else is 422 without disclosing the foreign record.
  - **Chain rule (proposed):** corrections form one linear chain per original — a correction may target only the **latest**
    published scorecard of that chain (the original, or the newest published correction); there are no forks. Every
    scorecard stays immutable; "current" means the latest in the chain.
  - **Error precedence (proposed):** a correction target is checked in this order and the first failure wins: (1) not a `PUBLISHED` scorecard of the same Tenant+LE+Supplier+period → 422; (2) superseded — it already has a published correction → 422; (3) an active `DRAFT` correction of it exists → 409. Only a target passing all three gets a new correction.
    A foreign-scope target is reported as 422 without disclosing that it exists.
  - The field is not in v1 — contract successor (CT contract writer); until then no correction path exists.

### 0.3 Transaction, outbox and delivery — ÖNERİLEN (SPEC-DELTA "Failure, isolation and atomicity targets")

- One MongoDB multi-document transaction commits business state, idempotency receipt, audit record and Pending outbox
  item; no compensation path. Replay adds no state, version or event.
- "Exactly once" applies to the **state and the outbox record**. **Delivery** (outbox publisher, outside this slice) is
  at-least-once; consumers deduplicate by EventId.
- Acceptance tests to add when authorised: transaction abort leaves zero of the four records; publisher retry delivers a
  duplicate that the consumer ignores; a crash between commit and publish is recovered by the publisher from the Pending
  record.

## 1. Module Summary

MOD-0147 owns tenant/legal-entity scoped supplier evaluations, published scorecards and supplier risk lifecycle.
Its initial proposed Phase B slice is backend-only and contract-first against
`docs/analysis/contracts/supplier-performance.openapi.yaml` (`SUPPLIER-PERFORMANCE` v1). Supplier identity is an
opaque external reference; this module never becomes the base Supplier system of record.

## 2. Ownership and Boundaries

**Owns:** evaluation periods and metric results, scorecard publication, overall score/risk classification,
supplier-risk records, lifecycle transitions, idempotent commands, audit/outbox evidence and correlation propagation.

**Consumes:** Metric Registry and Risk Register building blocks, Event Bus and
**CONSUMES: SUPPLIER-BASE (merkez üretecek)** through a frozen mock contract.

**Does not own:** MOD-0140 Supplier master/onboarding, supplier names/status/profile/KYC, MOD-0148 portal submissions,
source procurement facts, gateway/shared permission seams or other services' persistence.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `SupplierEvaluation` | Period-bound metric evaluation for an opaque `supplierId` |
| `EvaluationMetricResult` | Metric Registry code, score and weight snapshot |
| `SupplierScorecard` | Immutable publication derived from a submitted evaluation |
| `SupplierRisk` | Module-owned risk record and lifecycle |
| Commands | Create/submit evaluation; register/change status of supplier risk |
| Queries | List/get evaluations; list/get scorecards; list risks |
| API | Evaluation, scorecard and risk paths in `SUPPLIER-PERFORMANCE` v1 |
| Events | Evaluation published; risk raised/status changed |
| Permissions | `supplychain.supplier-performance.read`, `.evaluate`, `.publish`, `.risk-manage` |

## 4. Entity Fields

### SupplierEvaluation / SupplierScorecard

| Field | Rule |
|---|---|
| `Id`, `EvaluationId`, `ScorecardId` | Server-minted stable identifiers |
| `TenantId`, `LegalEntityId` | Required, server-resolved; never mutation payload |
| `SupplierId` | Required opaque MOD-0140 reference; no Supplier fields copied |
| `PeriodStart`, `PeriodEnd` | ISO dates; start must not exceed end |
| `Metrics` | v1: 3 or 4 of the four policy codes, each once, weight 25 — the full rule is §0.0 and §12; decimal-string score/weight |
| `OverallScore` | Derived 0..100 decimal string; client cannot override |
| `RiskLevel` | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |
| `Status` | `DRAFT`, `PUBLISHED`, `CANCELLED` |
| `Version` | Optimistic concurrency token |
| Soft-delete/audit fields | Repo-standard `EntityBase` fields |

### SupplierRisk

| Field | Rule |
|---|---|
| `RiskId`, `SupplierId` | Server ID + opaque supplier reference |
| `Category` | Contract enum: delivery/quality/compliance/financial/continuity/other |
| `Level`, `Status` | Frozen contract enums; transition matrix enforced server-side |
| `SourceType`, `SourceId` | Traceable source reference; source record is not copied |
| `Summary` | Required, trimmed, max 1000 |
| `DetectedAt`, `ResolvedAt` | UTC; resolution only for terminal/mitigated states |
| `CorrelationId` | Required UUID propagated to audit/outbox event |

Proposed indexes are tenant-first: evaluation `(TenantId, LegalEntityId, SupplierId, PeriodStart, PeriodEnd)`,
scorecard `(TenantId, LegalEntityId, SupplierId, PeriodEnd)`, risk `(TenantId, LegalEntityId, SupplierId, Status, Level)`.

## 5. Repo Scope

After all ready-for-dev gates pass, the proposed Phase B slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/SupplierPerformance/**`.
- `services/Diten.SupplyChainService/tests/**/SupplierPerformance/**`.
- Module-specific API reference/audit evidence paths explicitly assigned by the implementation WP.
- This module pack's status/evidence fields.

The frozen `supplier-performance.openapi.yaml` is read-only implementation authority in Phase B. This draft grants
no current write permission to runtime paths.

## 6. Protected Paths

- `.antigravity/**`, all existing frozen contract files and MOD-0140 Supplier contract/schema.
- `gateway/Diten.ApiGateway/**/ocelot.json`, shared permission registries and shared registrations.
- Archive controllers/views, frozen layout and all frontend paths in this backend-only slice.
- Other domain services and non-MOD-0147 features, including MOD-0148 portal runtime paths.
- Supplier master/profile/onboarding persistence and any direct foreign database access.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SUPPLIER-PERFORMANCE` v1 | FROZEN | Owned contract; implementation must match exactly |
| `SUPPLIER-BASE` | FROZEN in `docs/analysis/contracts/supplier.openapi.yaml` (x-owner MOD-0140, `780f8368a`, v1.2.0, sha256 `373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`; first pinned at `c8badff96` v1.0) — identity surface only; consumption authority open (DC-02) | **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; no local substitute |
| Metric Registry / Risk Register | Building-block dependency | Contract adapters only; no local registry clone |
| Event Bus | Platform dependency | Correlation-preserving outbox publication |
| Domain ownership | **CONCURRED** (DC-01, 2026-10-18) | Placement recorded in domain-config, DCP-009 `placed_non_canonical_modules`, module-id registry |

## 8. Runtime Constraints

- Proposed service is `Diten.SupplyChainService` on port 5066 (AGENTS.md §3; 5061 was CRM's port, corrected 2026-10-19); this draft does not authorize runtime work.
- MongoDB records are tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- `TenantId` and `LegalEntityId` are server-resolved and absent from mutation payloads.
- Commands require `Idempotency-Key` and UUID `X-Correlation-Id`; replay creates no duplicate state/event.
- Scorecards are immutable publications; corrections require a new evaluation/publication trail.
- State, receipt, audit and outbox commit in one transaction (ÖNERİLEN, §0.3); silent partial success is forbidden.
- SUPPLIER-BASE unavailability fails closed; supplier validation is never silently skipped.

## 9. Layout & Shell Contract

`shell: none`. The proposed first slice has no Razor UI, route, menu, DataTable or localization surface.

## 10. Backend File Convention

Proposed feature root is `Features/SupplierPerformance/` with separate `Commands`, `Queries`,
`Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators` and one models file. Each public command, query,
handler and validator remains in its own file. The service's five-layer CQRS, pipeline and response-envelope standards
apply after authorization; this section is not scaffold permission.

## 11. Frontend File Contract

Not applicable. Any UI requires an approved pack revision with tenant shell, exact fields, DataTable decision,
golden reference and seven-language localization scope.

## 12. Validation Rules

| Field/Header | Required | Rule | Pre-check |
|---|---|---|---|
| `Idempotency-Key` | Mutations | 1..200 — normalisation (trim or reject) and receipt tuple **BLOKE** with the Phase 1.5 amendment (§0) | Receipt tuple per the Phase 1.5 amendment |
| `X-Correlation-Id` | All operations | UUID | Propagate to response/audit/event |
| `SupplierId` | Create evaluation/risk | Non-empty opaque ID | SUPPLIER-BASE frozen mock; fail closed |
| Period | Evaluation | v1: `periodStart` first day and `periodEnd` last day of the same calendar month, else 422 | Duplicate = same Supplier+period in DRAFT/PUBLISHED → 409 (§0.0) |
| Metrics | Evaluation | v1: 3 or 4 of the codes `OTD`, `QTY_ACC`, `QUAL_ACC`, `INV_MATCH`, each once, weight exactly 25, value 0..100 with ≤ 4 fractional digits, else 422 | pack-held policy `supplier-score-policy/1` (§0.0, §0.4) |
| `ExpectedVersion`/`If-Match` | Lifecycle mutation | Current positive version | Conditional update |
| Risk transition | Status command | Frozen state matrix | Terminal state cannot reopen silently |

## 13. Failure Path to Verify

- Missing/unknown supplier returns the contract error; unavailable SUPPLIER-BASE fails closed with no write.
- Duplicate idempotency key returns the original result without a second evaluation/risk/event.
- Invalid metric, period or transition returns declared 400/422 error with UUID correlation.
- Stale `If-Match` returns 409 and never overwrites concurrent state.
- Cross-tenant/cross-legal-entity lookup returns 404; unauthorized actor returns 403.
- Scorecard publication failure leaves no partial publication/outbox state.

## 14. Authorization Convention

Actor type: tenant user/service actor. Proposed server-side permissions:

- `supplychain.supplier-performance.read`
- `supplychain.supplier-performance.evaluate`
- `supplychain.supplier-performance.publish`
- `supplychain.supplier-performance.risk-manage` (risk part, after TPRM)

v1 mapping: `createSupplierEvaluation` → `.evaluate`; `submitSupplierEvaluation` → `.publish`; `GET /evaluations[/{id}]` and
`GET /scorecards[/{id}]` → `.read`. No separation of duty in v1 (one person may hold `.evaluate` and `.publish`; Natig,
2026-10-22). Grants go through Roles → Permissions, never the default role template. Permission definition/seed is a
single-writer shared seam and is not authorized by this draft.

## 15. Gateway / API Routing Decision

The frozen contract base is `/api/supplier-performance`. A future `integration-agent` WP must own Ocelot changes and
verify auth, tenant/legal-entity context, correlation and idempotency propagation. This draft grants no gateway write.

## 16. Acceptance Criteria

- [ ] Implementation payloads and declared failures match `SUPPLIER-PERFORMANCE` v1 contract tests.
- [ ] Evaluation creation derives a score from the v1 policy `supplier-score-policy/1` (§0.0) and persists one audit/outbox chain.
- [ ] Submit publishes exactly one immutable scorecard and `SupplierEvaluationPublished` event.
- [ ] **Later slice (risk, after TPRM):** risk create/status commands follow the frozen lifecycle and preserve correlation.
- [ ] Idempotent replay and stale version paths have no duplicate or overwritten side effects.
- [ ] Tenant/legal-entity isolation returns 404 and server-side permission denial returns 403.
- [ ] SUPPLIER-BASE is consumed only through its central frozen mock; no Supplier master/schema is local.
- [ ] G5 evidence links scorecard/risk feedback to MVP-6 integration without overriding Supplier or procurement SoRs. **BLOKE** — needs live DC-02/DC-04/DC-05 producers; not part of a first slice.

## 17. Test Expectations

- DCP-002 identity verifier PASS and OpenAPI syntax/reference/mock validation.
- Domain tests for score calculation, publication immutability and risk transitions.
- Handler/API tests for idempotency, optimistic concurrency, permission and tenant/legal-entity isolation.
- Contract tests for every success/error response and lifecycle event payload.
- Mongo integration tests follow DB-010 isolated database naming.
- E4 module runtime evidence when authorized; G5 remains open until cross-module E5 integration passes.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] Owned OpenAPI is frozen as `SUPPLIER-PERFORMANCE` v1.
- [x] Backend-only proposed first slice, paths, objects, failures and acceptance are explicit.
- [x] Central CT publishes/freezes SUPPLIER-BASE and mock — done 2026-09-15 in `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, x-owner MOD-0140, `c8badff96`), the day before this pack was committed.
- [x] Placement: DC-01 CONCURRED 2026-10-18; domain-config / DCP-009 / registry aligned 2026-10-19.
- [ ] Supplier base consumption + Tenant/LE eligibility (DC-02) — MOD-0140 owner; build seam, not built.
- [ ] Metric policy (DC-04) — no Metric Registry service exists; **deferred**. For v1 met by the pack-held policy `supplier-score-policy/1` (§0.4) once this revision is approved.
- [ ] Risk taxonomy (DC-05) — no Risk Register service exists; **deferred**. Blocks risk registration — **out of v1** (§0.0, risk part after MOD-0216).
- [ ] Shared contract successor, replay/concurrency receipt, permission definition/composition, fixture, portal-source
      resolution — OPEN in `docs/roadmap/plans/mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md`.
- [ ] SupplyChain service audit trail accepted under AUD-001 (§21) — **met when PR #198 merges** (ledger `Diten.SupplyChainService.md`, trail `supplychain-merkezi-iletim`, path b).
- [ ] Module owner reviews and explicitly changes `status` to `ready-for-dev`.
- [ ] Runtime implementation WP supplies base HEAD, agent lane and bounded allowed paths.

## 19. Implementation Notes

- **ASSUMPTION:** scores and weights are decimal strings in range 0..100; overall score is server-derived.
- **ASSUMPTION:** the initial slice is backend-only and uses `SupplierPerformance` as feature root.
- **Resolved 2026-10-19:** the ownership ASSUMPTION is replaced by DC-01 placement (above); the governance records are
  aligned in the same change.
- **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; only opaque `supplierId` crosses this boundary.

## 20. Follow-up Items

- SUPPLIER-BASE contract/mock owned by MOD-0140 is published and frozen: `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, `c8badff96`, 2026-09-15), offering `listSuppliers`, `getSupplier` and `validateSuppliers`. What remains open is the authority to consume it — DC-02, AWAITING-CT per the 2026-10-05 CT ruling — not the contract's existence.
- ~~Reconcile MOD-0147 ownership in domain-config and central records~~ — done 2026-10-19 (DC-01 governance alignment).
- Separate permission/gateway integration WP after endpoints exist.
- Future UI pack revision if an operator surface is approved.
- G5 cross-module verification with MOD-0148 and the MVP-6 logistics/planning feedback flow.

## 21. Audited Events

AUD-001 (`audit-trail-standard.md`). The evaluation commands use **path b, trail `supplychain-merkezi-iletim`** (the
service's central forwarder, ledger `tests/architecture/audit-ledger/Diten.SupplyChainService.md`), **valid from the moment
PR #198 is on `main`**; until then it is a v1 gate (§0.0). The risk commands keep the dependency note until the risk part.
No command here is K2 class.

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateSupplierEvaluationCommand | supplier-evaluation.created | SupplierEvaluation | b — supplychain-merkezi-iletim (PR #198 main'de) | AfterState: SupplierId, PeriodStart, PeriodEnd, Metrics, OverallScore, RiskLevel | — |
| SubmitSupplierEvaluationCommand | supplier-evaluation.published | SupplierEvaluation → SupplierScorecard | b — supplychain-merkezi-iletim (PR #198 main'de) | Before/After: Status; AfterState: ScorecardId, OverallScore | — |
| RegisterSupplierRiskCommand | supplier-risk.registered | SupplierRisk | — (bagimlilik: SupplyChain izi) | AfterState: SupplierId, Category, Level, Status, SourceType, SourceId | — |
| ChangeSupplierRiskStatusCommand | supplier-risk.status-changed | SupplierRisk | — (bagimlilik: SupplyChain izi) | Before/After: Status, Level, ResolvedAt | — |

Not recorded: no credential or token is part of these commands. Free text may carry personal or sensitive data, so the
audit record keeps its **length and SHA-256, not the text**: in v1 the evaluation `notes` (max 2000, create) and the submit
`comment` (max 1000); with the risk part `Summary` (max 1000) and `resolutionNote` (max 2000). The business record keeps the
text under `supplychain.supplier-performance.read`. (The central forwarder records no request body at all; this line binds
any module-local trail.)

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | değerlendirme çekirdeği ve okumalar | §0.0, §0.4, §3 (değerlendirme/scorecard), §4 SupplierEvaluation/Scorecard, §12, §13 (oluşturma), §14 (v1 eşlemesi), dört GET |
| DL-2 | gönderim, yayın ve değişmezlik | §0.3, §8, §13 (gönderim), §21 (değerlendirme komutları), submit |

v1 dışı işler (v1.1 hesaplanan metrikler — okuma sözleşmeleri ve XMC-001; risk kısmı — MOD-0216 TPRM ve DC-05) bu sürümde dilim
**değildir**; ayrı bir paket revizyonu ve onayıyla gelir. CT-APR-01 satırının not alanı: "yalnız DL-1, DL-2".
