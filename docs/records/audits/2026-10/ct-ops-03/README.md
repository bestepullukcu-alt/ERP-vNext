# Evidence — CT-OPS-03, acceptance and evidence contract (2026-10-15)

Authority: `CT-OPS-03-ACCEPTANCE-CONTRACT-JOINT-DECISION-01` (joint decision of Codex and the AI session under
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`). Branch `chore/ct/pqg-01` on 168a46142, local only. The ControlTower
(Legacy) session gave a single-writer agreement for the seven shared files before they were written.

## Files

| File | What it is |
|---|---|
| `checks.txt` | Outputs of the checks below, on the final files. Local paths are replaced by `__SCRATCH__` / `__HOME__` |
| `run.sanitized.trx` | The architecture suite with all CT-OPS-03 files applied, sanitised (step-1 tool and rules) |

## What was built

| Item | Where |
|---|---|
| The acceptance record check: künye v2 (strict reader in front of REP-001's own reader), required evidence level and its approved, bound source line, decision / completion / conditions, the verified commit, committed evidence and acceptance list (§6.2 rows, evidence bound to each criterion), supersedes chain on the same ref, verifier ≠ implementer, waivers never automatic | `scripts/acceptance_record.py` |
| ACCEPTED rows of strict queues must name an allowing acceptance record; Y3 reads its own `reassessment_record` column and needs an approved / decided decision | `scripts/ct_queue.py`, `execution/delivery/ct-queue-template.tsv` |
| Decision and completion as two report columns, with the transition note for the report script; künye v2; acceptance list row format | REP-001 §2, §6.1, §6.2 |
| Decision, queue state and completion kept apart; the written relation; conditional, rejected, waiver, required level, -R3 | SOP §29.5 |
| Y3 column, the ACCEPTED check, the CLOSE command | operating card v1.8 |
| One "done" definition: SOP §29 is the only Done vocabulary; three lists point to it | `add-module.md` Faz 6, `orchestrator.md`, `release-checklist.md` |
| The acceptance list's author | `product-owner.md` |
| The joint decision | `docs/records/decisions/2026-10/ct-ops-03-acceptance-contract-joint-decision-01.md` |

## Checks and results

| Check | Result |
|---|---|
| `acceptance_record.py --self-test` | 54/54 |
| `ct_queue.py --self-test` · `validate` | 64/64 · OK |
| `prompt_gate.py --self-test` | 71/71 (unchanged gate) |
| Codex's reviews — each case written first (red), then fixed (green) | three false passes (source not approved or bound; a missing separator hiding the first failed criterion; an older version of a path whose current version is a rejection), then a late v2 header line, repeated source fields, broken records of other work; Y3 and acceptance sharing one column |
| The AI session's own finds | the REP-001 example with inline comments would fail the strict reader (now a drift test); three guards whose tests did not isolate them (tightened) |
| Sabotage: each guard removed in turn | 33 guards (30 in `acceptance_record.py`, 3 in `ct_queue.py`); every removal turns at least one named test red |
| End to end, real git, scratch clone (9 scenarios) | ACCEPTED with an accepted record passes; without a record, or with a conditional one, refused; -R3 READY with an approved reassessment dispatchable, without one DUR, with a correction record DUR; -R3 ACCEPTED with both records passes, without the reassessment refused; a record checked against an FU row refused |
| SCE / MVP6 | 168a46142's `ct_queue.py` against this one, every ref holding the queue: 56 refs, 3 distinct queue files, 56,056 decisions (normal and `--new-version`) and the validate errors: 0 differences (Codex measured the same independently: 57 refs, 28,528 ids, 0 differences) |
| `status_report.py` | unchanged; its output on this repository is identical before and after; on v2 records it still prints its legacy table |
| SOP §36.2 to the end of the file | byte-identical to 2481004b1 |
| Architecture suite (one run, all files pinned) | 131/138; the 7 failures are the 7 registered known failures (`known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised); fingerprints identical to CT-PQG-01's run |

## Limits — NOT VERIFIED or not machine-checkable

- **Owner confirmation for the five `.antigravity` files.** `AGENTS.md` §4 and the operating card §7 ask for the
  owner's explicit approval of `.antigravity/**` changes. These rest on `CT-COMPLETION-DELEGATION-OWNER-DECISION-01`,
  the owner's words as relayed by Codex. The ControlTower (Legacy) session noted that it has not verified that
  authority and will ask the owner. The commit is local; the owner's confirmation is expected before any merge.
- **Another branch touches two of the files.** `e66404988` (İ1-A.2b, the AI session's own earlier commit, present on
  other Control Towers' branches but not on `main`) changes `add-module.md` lines 11, 49–60, 104 and `orchestrator.md`
  line 87. This commit adds lines after line 166 and before the closure note; the regions do not overlap. To be
  measured again at merge preparation.

- **Semantics.** The scripts check the integrity of the record and its evidence; they do not re-run tests. Whether the
  acceptance list and the evidence are enough is the independent verifier's and the Control Tower's call.
- **Identities.** `verifier` and `implementer` are declared, not authenticated.
- **The report script (CT-OPS-04).** Measured in the scratch clone: for MOD-0151 the legacy `status_report.py` shows the
  conditional W10 record although W9 is accepted — it picks the highest evidence level and, on a tie, file order, and
  folds FU ids into the base module. This is CT-OPS-04's work; the legacy output is not changed here.
- **Dispatch / acceptance in force.** Only once the chain is on `main`.
- **CI and remote `main`.** Not run / not read; no fetch was made.
- **Not in scope.** İ4's remaining part (pack "Teslimatlar", delivery check script, record-code lock); migration of old
  records (none).
