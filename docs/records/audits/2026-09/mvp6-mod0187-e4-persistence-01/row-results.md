# R08 / R19–R20 / R22–R26 process-boundary results

| Row | Verdict | Fresh observation |
|---|---|---|
| R08 | PASS | A coefficient with 50 integer and 50 fractional digits was accepted over signed HTTP, persisted byte-exact, then read and replayed by a second API process against the same DB. Counts stayed 1/1/1/1. |
| R19 | PASS | Twenty same-key requests returned 20×201 with one claim/receipt/audit/outbox. Changed payload returned 409 `IDEMPOTENCY_KEY_REUSED` without writes. The same transition key on two different target IDs independently produced two 200 results. |
| R20 | PASS | A same-actor token with the create grant removed returned 403 before receipt replay. A different authorized actor replayed the original 201 result without audit actor rewrite. Replay after Closed and after direct soft-delete returned the original Open receipt with unchanged counts. |
| R22 | GAP | The running API binds `NoOpClaimCommitProbe`; no authorized configuration can force its generated claim ID/number. A true definite `claim_number` collision therefore cannot be selected without Program/shared-probe or product changes. No synthetic PASS was recorded. |
| R23 | PASS | Twenty distinct keys for one Shipment produced 20×201, 20 distinct IDs and exact 20/20/20/20 collection counts. The separate 20 same-key run produced one durable write group. |
| R24 | PASS | Two separate 20-request races were run. Mixed Approved/Rejected and same-target Approved each produced exactly one 200 and nineteen 422 `INVALID_CLAIM_TRANSITION`; each race added exactly one receipt/audit/outbox and retained one aggregate. |
| R25 | GAP | Distinct stage and unknown-commit mechanisms exist behind `IClaimCommitProbe`, but the composed API registers `NoOpClaimCommitProbe` and exposes no authorized activation seam. Component/model failpoint evidence was not promoted to process evidence. No DB query failure was classified as zero writes. |
| R26 | PASS | Create and transition produced two Pending events. Create timestamp had UTC offset; transition timestamp exactly preserved `2026-09-21T10:11:12.123456789+03:00`. Root, null causation, Claim payload, aggregate identity and wire `v1` were exact. |

All PASS rows bind source → fresh build → binary → API process → signed HTTP → scoped Mongo observations. R22 and R25 remain explicit product/configuration seams rather than evidence-harness substitutions.
