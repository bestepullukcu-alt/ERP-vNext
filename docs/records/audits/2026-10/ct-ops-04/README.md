# Evidence — CT-OPS-04, status report new mode (2026-10-16)

Authority: `CT-OPS-04-REPORTING-JOINT-DECISION-01` (joint decision of Codex and the AI session under
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`). Branch `chore/ct/pqg-01` on 7597f364c, local only. The ControlTower
(Legacy) session gave single-writer agreements for REP-001 and the operating card, and for
`.antigravity/scripts/verify_module_id.py`, before they were written.

## Files

| File | What it is |
|---|---|
| `checks.txt` | Outputs of the checks below, on the final files. Local paths are replaced by `__SCRATCH__` / `__HOME__` |
| `run.sanitized.trx` | The architecture suite with all CT-OPS-04 files applied, sanitised (step-1 tool and rules) |
| `sce-comparison-codex.json` | Codex's independent SCE / MVP6 comparison of `ct_queue.py`, copied unchanged |
| `vmi-parity-codex.json` | Codex's independent comparison of `verify_module_id.py`'s old and new loaders on real data, copied unchanged |
| `boundaries-after-codex.json` | Codex's independent real-git re-test of the three findings in its own fixture clone, with the tools' sha256, copied unchanged |

## What was built

| Item | Where |
|---|---|
| `--mode v2`: one fixed SHA for every input; a registered strict queue (else exit 2); TABAN KAPSAMI percentages from an optional approved effort baseline; the report fields; a closed-schema machine block the visible report is generated from; the previous-report check and difference | `scripts/status_report_v2.py`; six router lines in `scripts/status_report.py` |
| Module id data and -R3 decisions from the fixed commit's blobs, parsed by the tools' own parsers | `verify_module_id.py` (three loaders split into wrapper + pure parser); `ct_queue.py` (`known_modules_from`, optional `read_file`) |
| The optional baseline template | `execution/delivery/effort-ledger-template.md` |
| The documents | REP-001 §7.1 and one sentence in §2; operating card §6 |

## Checks and results

| Check | Result |
|---|---|
| `status_report.py --self-test` (the new mode) | 75/75 |
| `ct_queue.py --self-test` · `validate` | 69/69 · OK (sce-mvp6: 0 errors, 1 warning) |
| `acceptance_record.py --self-test` · `prompt_gate.py --self-test` | 54/54 (unchanged) · 71/71 (unchanged) |
| Tests written first (red), then fixed (green) | Codex's inputs: a row hidden before the ledger header; an invalid baseline shown as "no baseline"; a refused ACCEPTED row in the done list; percentages, SHAs and level counts that only had to have the right type; open schema and repeated JSON keys with the hash regenerated; the fixed-source reader for -R3 decisions; then three real-git findings — an approval withdrawn at the selected SHA still honoured, a queue without its `state` column crashing (exit 1), a previous report with forged numbers and a regenerated hash accepted as verified. The AI session's own: the ledger template drift test; a key pattern without digits (`ledger_content_sha256` was unreadable); a lambda that returned a tuple; a test helper that wrote `_extra` into the künye; forged reports rendered from the wrong data (tests that passed for the wrong reason) |
| Sabotage: each of 36 guards of the new mode removed in turn; the three `build()` wirings of the last fixes removed in turn in the real-git end to end | every removal turns at least one named test red (self-test), and each wiring its end-to-end expectation |
| End to end, real git, scratch clone, fresh branch, 18 asserted expectations | all pass: no baseline; approved baseline 8/16 · 4/4 · 4/8 and overall 65.0 by the approved weights; a changed baseline not compared as progress; a number changed after approval suppresses the percentages with the reason; an open W9-R1 over an accepted W9 is not counted; the historical report (`--ref S3`) and a dirty working copy give identical business data (only the dirty note changes, at HEAD); an altered previous report is refused; a previous report with forged numbers and a regenerated hash is refused (rebuilt from its own source SHA); an approval withdrawn at the selected SHA suppresses the percentages; a queue without its `state` column exits 2 with the reason; a non-strict queue exits 2 |
| The old mode | byte-identical to 7597f364c's script: this repository (default, `--ref origin/main`, SCE packs) and the scratch clone with v2 records |
| `verify_module_id.py` | HEAD's version against this one on real data: the three loaders equal; 172 active ids (344 check-id comparisons), check-all and 3 candidates: 0 differences; discovery findings, sources, gaps, result and exit identical. Codex independently: `load_registry` 249 · `load_blueprint` 357 · `load_packs` 163 equal, the pure parsers equal (`vmi-parity-codex.json`) |
| SCE / MVP6, 7597f364c's `ct_queue.py` against this one | 56 refs: 0 differences (this session); Codex independently: 56 refs, 56,056 decisions, 0 differences (`sce-comparison-codex.json`) |
| Architecture suite (one run, all files pinned) | 131/138; the 7 failures are the 7 registered known failures (`known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised); fingerprints identical to CT-OPS-03's run. Code pinned before the run, unchanged after; raw sha256 `845a5d93…`, sanitised `044b7cc5…` (full values in `checks.txt`) |

## Limits — NOT VERIFIED or not machine-checkable

- **What a percentage means.** It is progress of the approved baseline's accepted effort, not of the whole Control
  Tower and not of quality. Rows outside the baseline are listed apart.
- **Decisions.** "Bağlı karar kayıtları" lists only records reachable from the queue, the acceptance records and the
  baseline; "karar bekleyenler" only what is mechanically known. Owner decisions taken in chat are not seen.
- **Publication.** A report of a commit that is not on `origin/main` — and any local report — is not published
  evidence (REP-001 §3); the remote state is never verified.
- **Owner confirmation for `.antigravity`.** REP-001 and `verify_module_id.py` rest on the relayed delegation; the
  owner's confirmation is expected before any merge.
- **CI and remote `main`.** Not run / not read; no fetch was made.
- **Not in scope.** The R8 flag for the old mode; migration of old records.
