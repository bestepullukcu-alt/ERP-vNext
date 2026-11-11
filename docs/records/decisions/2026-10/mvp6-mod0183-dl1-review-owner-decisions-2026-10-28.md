# MOD-0183 DL-1 — three owner decisions from the code reviews (owner decision, 2026-10-28)

- owner: **Natig** (`gmg-10`, K1, management) — decided in chat 2026-10-28, recorded by the MVP-6 Control Tower
- source: the independent read-only code reviews of Q500 (MOD-0183 DL-1) and the worker's evidence note
  `docs/roadmap/plans/mvp6-integration-wp-p1-01/DL1-EVIDENCE-MOD0183.md` ("Open points")
- applied in: MOD-0183 pack revision **r7** (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` §23)

| # | Question | Decision |
|---|---|---|
| 1 | Failed dispatch outcomes (compensation 422 / 500 / 409, `ALREADY_REVERSED`, the exit's reversal ids, late-writer reversals) cannot reach the central audit: BuildingBlocks `CentralAuditBehavior` (BL-501, AUD-001) records only successes. | **(a) v1 interim is enough:** they are written to the shipment's in-product audit (`sce_shipment_audit`) and attempt history in the same Mongo transaction as the outcome; the pack says so (§23.4). The BuildingBlocks / Platform change is requested from its owner separately (XMC-001) and is not a v1 gate. |
| 2 | Two pack sentences lag the code: §23.3 / S23-4 "before the first post nothing changes" and §23.4 "the late writer reverses its issue". | **Fix now** in r7: only a definite answer leaves nothing changed; an ambiguous 5xx / timeout keeps the line *sent* and the attempt `InProgress`; the late writer reverses only an **unrecorded** issue. |
| 3 | An attempt blocked by `IDEMPOTENCY_KEY_REUSED` has no operator path; the shipment stays `CompensationFailed`. | **(a) Stays blocked in v1:** every recorded issue is still reversed; only the line behind that key stays open; the movement is investigated with the INVENTORY (MOD-0173) owner. An operator close action is a later revision. |

## Effect

- r7 is text only and matches the code on `feature/sce/mod0183-dl1-01`; it needs CT-APR-02 and a CT-APR-01 row before the
  DL-1 code PR merges (the code PR then cites r7).
- Q500's redispatch trigger ("pack changes hash") is answered by this record: the code is not re-dispatched, it is checked
  against r7 by the review.
