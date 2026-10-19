# Evidence — CT-FIX-07, open decisions, Blueprint ids and domain-config presence in the discovery summary (2026-10-18)

Authority: `CT-FIX-07-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-07-joint-decision-01.md`). Branch
`chore/ct/ct-fix-07` on `70c3561d5007cc738a4d1971867447a26d186258` (CT-FIX-06), local only. The AI session is the single
writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/verify_module_id.py` | `report_summary(…, blueprint, domain_configs)`: the open decisions, the Blueprint candidates, the domain-config line; `domain_config_paths`, `domain_config_at_head` (True / False / None), `PRESENCE`, `_list_capped` | `a63ea76bc90faaf3b19d7cc9028a71dc5774bd98e642ea92e6c166dafc82541b` |
| `scripts/tests/test_verify_module_id_discover.py` | 3 new tests; the künye line found by prefix | `4eb97c3b2944371bbbff75e768099e21695232c6a35a4e37c7d917df408613fa` |
| `scripts/tests/test_ct_cli_regression.py` | `CTFIX07_SummaryDecisions` (2) | `92127c6f731045af524100feb3f12154a3fdbb121cbbcf55dcc50012f84835ee` |
| `docs/records/decisions/2026-10/ct-fix-07-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-data.txt`, `checks.txt`, `drivers/` | in the commit |

## The cases

| Item | Shown |
|---|---|
| Open decisions | a collision ("kimlik MOD-0251 — olası çakışma (2 paket yolu)", or "takma ad ilişkisi belirsiz" for an alias); an owner TBD, empty or absent (with every value its blobs carry); nothing when there is none |
| Blueprint candidates | ids and names; 300 long names stay under 420 bytes with "+k aday" |
| domain-config | HEAD has it: "var"; only in the working tree: "yok"; outside a repository (git cannot answer): "ölçülemedi"; the real fixture's discovery before and after the file is committed |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — against `70c3561d5`'s sources | the 3 new unit tests and the 2 new R0 tests fail or error; the earlier summary tests and the controls pass |
| Guard removal (`guard-removal.txt`, `drivers/harness5.py.txt`) | 5 / 5 proven: the decisions line, the unset-owner check, the Blueprint list, presence read from the working tree, a failed measurement read as absent |
| Real data (`real-data.txt`) | "Employee": every line except ÖZET, `araç:` and `rapor-sha256:` identical; BULGU 15 → 15; 3089 bytes; the three lines inside the first 4 KB; `--check-report` 0 |
| The worktree, once, on the final sources (`checks.txt`: the command, the versions, the raw log's SHA-256, the warnings) | `scripts/tests` 136 / 136 |
| Architecture | **NOT RUN** (a Python-only change); candidate 4's run stays historical evidence |

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| domain-config presence through the production CLI (a real absence, a working-tree-only file, a file in HEAD, a git measurement failure) | 8 / 8; a first fault-injection wrapper missed `git -C` and produced no fault — kept with its correction | `373c3ad9c10f24cbe9f34eb2c4eb2b4ea8fd61116b6581b49d77a108e470dbee` |
| The summary through the production CLI (the report hash, the gate's reading, BULGU equality, the budget, the alias line) | 11 / 11 | `3d6dc7246fff32804b2f435b9607c2623a32c242cacb8f7e4dc54e3139992aaf` |

## Limits

- The lines are information. They do not make a reader read the domain-config, name the owner or propose a first
  scope; the C5 pilot showed a visible owner TBD left unused. Not closed.
- The domain-config presence is HEAD's only; a pack's own ref is not measured.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
