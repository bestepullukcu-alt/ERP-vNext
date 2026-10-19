# Evidence — CT-FIX-10, no invented module mismatch without the queue row; writers names a queue that is not UTF-8 (2026-10-18)

Authority: `CT-FIX-10-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-10-joint-decision-01.md`). Branch
`chore/ct/ct-fix-10` on `56166f10790e54ec183b263302e0c4c3eb1f6dfd` (CT-FIX-09), local only. The AI session is the single
writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/prompt_gate.py` | `check_authority(…, row_known)`: when the queue could not be read (`q["error"]`), a product pack's own künye and file-name identity are still checked (its own module given to the shared `pack_identity`), and the pack's relation to the work is written as "kuyruk okunamadı — paketin bu işin modülüne bağı ölçülemedi". The shared `pack_identity` is unchanged | `dd1bb6e60eba04eb45cdeb156f51c1c5b122f20c7b3104784e8b863af17d0d36` |
| `scripts/ct_queue.py` | `writers`: the base, a ref and the merge base are read through the existing `decode(data, what)`, with the ref and the path in `what` — a queue that is not UTF-8 is a named DUR, exit 2 | `e27c7a89e4c9b9226da105b7d8876dc4f8686939ab80a4820304e856e87052d3` |
| `scripts/tests/test_ct_cli_regression.py` | `CTFIX10_UnknownRowAndWriters` (2 tests, 8 cases) | `1547b2c1b8d8a85811103ab1471c895d6622bcf6077c0c1499865d6e0c4b655b` |
| `docs/records/decisions/2026-10/ct-fix-10-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-data.txt`, `checks.txt`, `drivers/` | in the commit |

The pre-existing literal BOM at `ct_queue.py:123` is unchanged.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `56166f107`'s sources | 5 cases fail: 2 G1 cases (the false "(yok)" mismatch, no "ölçülemedi") and 3 writers contexts (a traceback). The controls pass there. Two test mistakes found after the first red were corrected in the test, not the code (written there) |
| Guard removal (`guard-removal.txt`, `drivers/harness11.py.txt`) | 6 / 6 proven: G-U1 the queue error ignored, G-U2 the unmeasured relation not written, G-U3 the pack's own module not given, G-W1 / G-W2 / G-W3 the base / a ref / the merge base read with a raw decode |
| Real data (`real-data.txt`) | `writers --queue sce-mvp6` on the real refs: no DUR, no traceback; exit 1 from its existing collision report. The AI session ran only the new tool and did not compare it with CT-FIX-09's; Codex compared the two byte for byte (below) |
| The narrow component run (`checks.txt`) | on **the CT branch's three frozen files**: the `ct_queue` and `prompt_gate` self-tests (141, 105) and 16 tests (CTFIX10, CTFIX09, CTFIX08, CTFIX04_I3, Controls), all OK; source identity the same before and after |
| Full `scripts/tests`, architecture, live model calls | **NOT RUN** (the agreed narrow check; no new live call) |

**What the run is and is not.** It is a narrow run on this branch's three files, not a full `scripts/tests` run and not
a combined-candidate run.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| C10 triage of G1 / G2 (history: before this fix) | 6 real CLI: the normal product gate 0; an empty registry path 1 with the real queue reason kept but a false "(yok)" pack ENGEL added; a real file / künye identity conflict 1. writers normal 0, a traceback (1) only when a local ref holds a queue that is not UTF-8, 0 once that ref is removed | `09702c80430c007da89eb50c8973d13e8186dfc9a8f195f54785faea3e954629` |
| writers in three contexts, before this fix (history) | a bad ref, a bad `--base`, and only a bad merge base between two sound tips: each 1 with a traceback; 0 after | `30a268e9e0d309e018add7d4e0e39549dfb15cc566db1c011af3cff87b539b05` |
| **Final:** the gate's authority boundaries, 7 real CLI on the frozen copy (with the 5 writers calls below, 12 in all) | normal 0 before and after; a broken queue with a sound, a draft or an identity-conflicted pack: 1, no false "(yok)" message, the real DUR and the report hashes right, draft and künye / file defects still refused apart; an intact queue: the real identity conflict and the missing W9 still 1 | `d6d03ff59a90bbcb05f9a356ff1bbbbe3ae3cf1527e9ba730149fc52dc6cdf35` |
| **Final:** writers in three contexts | normal 0; a bad ref, base or merge base: a named DUR, exit 2, no traceback; normal after 0 | `82bfd14fd98fe524de20d6c71b355c99496beee2d9f827683c17347b30da2464` |
| **Final:** CT-10 source snapshot | the three files as frozen | `ad132cca2fab27022de5aa42365ad8cac5d9a95a44d389ab9d9fceda94231a87` |
| Real SCE writers, CT-FIX-09's code (`56166f107`) and this package's, run read-only with the same real `__file__` / ROOT and the same arguments (the CLI file not changed) | both exit 1, stderr empty, stdout byte-equal (SHA-256 `1a39d103266822468e1fbf6e75c0471c3dc58b32f8dac2316ee2230f0ebc4fc3`, 23 lines, 6 ÇAKIŞMA lines). A writers comparison on real data only — not a whole SCE check, no new ref or fetch | `6a164c3938c3d8b4cb3941972da9ac7bf84e7450180f2ee0b1ba938dcdde906e` |

## Limits

- Only the product pack's module relation is marked "ölçülemedi". A governance record's binding with the queue row
  unknown was not shown misleading in this fixture (the künye's `work_packages` still binds), and it is not changed.
- `writers` stops at the first queue that is not UTF-8. It does not go on to report the other refs.
- C10-G6 (a cut path in a message) and other older items are not in this package.
- GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
