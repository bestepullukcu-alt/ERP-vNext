# verify from: /Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-10/mvp6-q447-return-403-test-01 — `shasum -a 256 -c ARTIFACTS.sha256`

# Q447 — the dependency 403 has its own case in Returns and Claims; 401 stays 503 on purpose

- Lane: Q447, integration-agent, single writer on `…/Tests/Returns/ReturnReferenceTests.cs`. Recorded 2026-10-05.
- **Ledger.** CT-QUEUE has no `Q447` row. Row **Q423** (`CT-QUEUE.tsv:521`, `READY (own WP, small)`, source Q420) describes
  this exact work, sentence for sentence, so the lane ran against it. If Q447 was meant to be a separate row, it is
  missing (R8).
- Preflight: `Sun Oct  4 22:08:29 UTC 2026` · `feature/mvp6-logistics` · HEAD `cea01354e` · porcelain 70 · staged 0 · no
  `index.lock`. Both test files equal to HEAD before the edit (pre-edit copies and sha256 in `SOURCE-AS-MEASURED.sha256`).
- Read before writing: the Q420 record (its `readers.diff`, findings F-Q420-1..3), `returns-semantics-v3.0.0.md:23`
  and `:62`, `claims-semantics-v3.0.0.md:128-130`, `:158` and `:163`, both readers (read only).
- **The readers were not touched (Q420 owns them). No contract, no UI. Nothing staged or committed. No secret printed.**
  Agent verdict ≠ CT ACCEPTED.

## Verdict

**DONE.** Returns: **80/0/80**. The suite reads 439/1/440, and the one failure is the known Claims restart-mode test.
Both sabotage directions are caught: undoing Q420 turns all five new 403 cases red, and over-mapping 401 to 403 turns
all three 401 cases red.

## The contract decision the tests encode

| module | dependency 403 | dependency 401 | where it is written |
|---|---|---|---|
| Returns | **403 `INVALID_REQUEST`** | **503 `DEPENDENCY_UNAVAILABLE`** | `returns-semantics-v3.0.0.md:62` "Auth401/context403/schema400/media415 use INVALID_REQUEST"; `:23` places the grant check in the context step |
| Claims | **403 `FORBIDDEN`** | **503 `CLAIM_REFERENCE_UNAVAILABLE`** | `claims-semantics-v3.0.0.md:163` "403 `FORBIDDEN` includes scope/identity/action failures"; `:158` declares 403 for createClaim |

Why 401 stays 503, as Q420 recorded: the consumer has already validated the same token with the same key. A
dependency 401 therefore means the two services disagree about trust. That is an inter-service fault, not the user's
missing permission. Both test files now say this next to the 401 cases, so the next reader does not "fix" it.

## The change (`evidence/change.diff`, +40 / −1, tests only)

**`Returns/ReturnReferenceTests.cs`** (the single-writer file):
- The `InlineData(403, …)` row is **removed** from `OtherProducerFailures_RemainDependencyUnavailable`. The 401 row
  stays there, with a comment giving its reason. The 5xx rows are unchanged.
- **New theory** `ProducerDenial403_IsReportedAsContextDenialNotOutage`, 3 cases:
  - a producer 403 whose body is a real denial body,
  - the old row's body (`SHIPMENT_ROOT_INVALID`),
  - an empty body.

  Each expects **403 `INVALID_REQUEST`**: the producer's body never decides the answer, and its message is not passed on.

**`Claims/ClaimReferenceTests.cs`.** This file is **outside the declared single-writer path**. The dispatch asked for
the Claims equivalent "or say plainly why not". There is no reason not to, and it was clean at HEAD. Its earlier
transport tests (`Reader_TransportFailures_…`, `Reader_OtherProducer5xx_…`) cover refusal, timeout, invalid JSON, 404
and 5xx, but **no 401 or 403** (F-Q420-2). Added:
- **New theory** `Reader_DependencyRefusesCaller_403IsDenial401StaysUnavailable`, 4 cases. Both reads Claims makes
  (Shipment, then Carrier when the claim names one) are refused once with 403 and once with 401.
- Expected: 403 → **403 `FORBIDDEN`**; 401 → **503 `CLAIM_REFERENCE_UNAVAILABLE`**.
- Each case also asserts how many reads happened. A refused Shipment read stops after 1 call; a refused Carrier read
  happens on the 2nd. Each asserts too that the producer's message does not leak.
- **New private transport** `RefusingTransport`, which refuses one named path and answers the other with 200. The
  existing `BranchTransport` applies a fault to every path, so it cannot express "the Carrier read alone is denied".

## Proof

### Suite (Q335 recipe, a scratch copy with 0 differences from the repository, own mongod 57447 with test commands enabled)

| module | before (Q372 resume, Q420) | **after** |
|---|---|---|
| Shipments | 90 / 0 / 90 | **90 / 0 / 90** (`ShipmentTelemetryTests:71`, the Q361 flake, passed) |
| Carriers | 36 / 0 / 36 | 36 / 0 / 36 |
| Loads | 33 / 0 / 33 | 33 / 0 / 33 |
| **Returns** | **77 / 1 / 78** | **80 / 0 / 80** — 78 − 1 moved row + 3 new cases |
| Claims | 128 / 1 / 129 | **132 / 1 / 133** — + 4 new cases; the 1 is the known `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` (restart mode) |
| S&OP | 19 / 0 / 19 | 19 / 0 / 19 |
| Capacity | 48 / 0 / 48 | 48 / 0 / 48 |
| **total** | 431 / 2 / 433 | **439 / 1 / 440** |

The CT range 432/1/433 moves by the 7 new cases: the old range plus 7 is 439/1/440, and that is what was measured.
The dispatch's "78/0/78" assumed a pure move. With three new cases added, the clean Returns figure is 80/0/80.

### Sabotage — filter `ReturnReferenceTests|ClaimReferenceTests`, 66 tests

| copy | readers | result | red cases |
|---|---|---|---|
| fixed (= repository) | working tree (Q420) | **66 / 0 / 66** | — |
| **revert Q420** (`evidence/sabotage-revert-q420.diff`) | HEAD (403 → 503) | **61 / 5 / 66** | all 3 Returns `ProducerDenial403_…` cases, and the Claims 403 rows for Shipment and Carrier |
| **over-map** (`evidence/sabotage-overmap.diff`) | Q420 plus 401 also → 403 | **63 / 3 / 66** | Returns `OtherProducerFailures_…(401)`, and the Claims 401 rows for Shipment and Carrier |

The new tests therefore fail both on a fix that is undone and on a fix that is too wide. The Claims 401 rows are green
on HEAD as well: they pin behaviour that has existed all along and that nothing tested until now. The sabotage
readers lived only in the scratch copy; the repository readers were never written.

Raw output: `evidence/reference-tests-{stack,sab,overmap}.txt` and `evidence/sc-suite.txt`. The suite script is in
`evidence/run-modules.sh.txt`.

## Findings

- **F-Q447-1.** CT-QUEUE has no row Q447. Row Q423 is this work. Either Q447 is a lane id for ledger Q423 (as with
  R-1/Q386), or its row is missing.
- **F-Q447-2.** A second file, `Claims/ClaimReferenceTests.cs`, was written outside the declared single writer, because
  the dispatch asks for the Claims case. It was clean at HEAD and is untouched by every other lane in the porcelain.
- **F-Q447-3.** The Returns figure is 80/0/80, not 78/0/78, because 3 cases were added (above).
- **F-Q447-4.** Q420's live run remains the only end-to-end proof of either module's 403. These tests run the readers
  against a mocked transport; they do not cross the Gateway or a real Shipment or Carrier service.
- **F-Q447-5.** F-Q420-3 (401 → 503) is now pinned by tests in both modules. If the owner ever rules otherwise, these
  rows are the ones to change, and their comments say why they exist.

## Cleanup

- mongod 57447 shut down; no lane process running.
- Scratch `~/mvp6-env/q447-20261005-0112/` (pre-edit copies, suite copy, sabotage copy) is kept (no `rm`).

Nothing committed, nothing pushed, nothing staged.
