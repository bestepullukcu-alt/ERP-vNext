# MOD-0147 — v1 performance scorecard now, risk after TPRM (owner decision, 2026-10-22)

- owner: **Natig** (`ny@gmgroup.ch`, management) — decided in the MVP-6 Control Tower chat 2026-10-22, recorded by the CT
- status: **ACCEPTED**
- module: MOD-0147 Supplier Performance & Risk (`execution/domains/supply-chain-execution/module-packs/MOD-0147-supplier-performance-risk.md`, `status: draft`)
- supersedes in part: the timing line of `mvp6-mod0147-v1-metric-set-owner-decision-2026-10-21.md` ("MOD-0147 starts after
  MOD-0216 TPRM") — now true for the **risk part only**
- related: Ali 2026-10-20 ("MOD-0147 v1 = performance record only"), plan v10.0 C5

## Why

MOD-0216 (TPRM) has no pack, no module-registry row and no code on `main` (measured 2026-10-22, `91a1d6031`; it is named
only in MVP-6 documents). "Start after TPRM" therefore meant waiting for a module nobody has started.

## Decision

1. **MOD-0147 v1 = supplier performance scorecard, starting now.** Evaluations and published scorecards
   (`SUPPLIER-PERFORMANCE` v1 evaluation/scorecard operations) with the fixed metric set of 2026-10-21 (OTD, QTY_ACC,
   QUAL_ACC, INV_MATCH, 25 each, calendar month, 3-of-4 publication rule).
2. **The risk part waits for MOD-0216 TPRM.** `POST /risks`, `POST /risks/{id}/status`, `GET /risks` and the
   `SupplierRisk` object are out of v1; as already decided, the risk part reads TPRM's result and holds no risk itself.
3. The decision authorizes the **pack revision** (draft → ready-for-dev candidate) and the **read-contract request to the
   Procurement owner**. It authorizes no code: code waits for the revised pack's CT-APR-02 review and CT-APR-01 approval
   and for the remaining gates (DC-02 supplier eligibility on MOD-0140 1.3.0).

## Consequences

- DC-05 (risk taxonomy) no longer blocks v1; DC-04 (Metric Registry) is met for v1 by the pack-held policy revision
  `supplier-score-policy/1` (§0.4) until MOD-0004 exists.
- **Confirmed by Natig the same day:** **v1 metric values entered by the evaluator**
  on the frozen contract (it takes metric codes and measured values), **v1.1 computed from Procurement facts** once read
  contracts exist (MOD-0141 PO, MOD-0142 GRN, MOD-0143 3-way match; QC source to be identified). The 5-receipt-line
  minimum needs a receipt count the v1 contract does not carry, so it applies from v1.1.

## Follow-up decisions after the independent review (Natig, 2026-10-22)

The CT-APR-02 review (`docs/records/reviews/MOD-0147/2026-10-22-9b5e0a77.md`, KOŞULLU) found two blocking points; Natig
answered them the same day:

- **Bands (F-2):** v1 uses `[90,100] LOW`, `[75,90) MEDIUM`, `[50,75) HIGH`, `[0,50) CRITICAL`, from the unrounded score.
- **Missing metrics (F-1):** the client sends only the metrics with data, each at weight 25;
  score = Σ(value × weight) / Σ(weight), exact; fewer than 3 metrics → 422 and no draft; a missing metric is shown by its
  absence from `metrics[]`.
- **Separation of duty (F-10):** none in v1 — one person may create and publish.

This record authorizes no code and no pack change by itself.
