# Evidence — CT-FIX-06, a bounded discovery summary, real evidence lines, filled registry columns (2026-10-18)

Authority: `CT-FIX-06-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-06-joint-decision-01.md`). Branch
`chore/ct/ct-fix-06` on `c203303ce2b9b4e10156dfcde3b4897e172fe8cb` (CT-FIX-05), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently, in its own copies (listed by SHA-256
at the end). The core-format fixes of the same audit are İ5B-FIX-07 on the İ5-B line (`i5b-fix-07/`).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/verify_module_id.py` | `report_summary`, `pack_kunye_values`, `_kunye_line`, `_paths_capped`, `_cap`; `discover` inserts the 'ÖZET' block under the header (the old body is `_discover`) | `57555e592618fb7f71ee01fb728e3e4e461a4268dfd58461ce56b5c8078a9eaa` |
| `scripts/acceptance_record.py` | EK1-2 `evidence_line_problem` for a cited `geçti` line; one self-test case | `cb017a3cd53abe62e674b8a7670b666c2f3ef0eb4660faf15e977d398412f0c0` |
| `scripts/ct_queue.py` | EK1-6 `check_registry` checks every registry column; `in_force_status` uses it; one self-test case | `ee8b7eef9c9684206d35e4c4ae19d354fd9015ee30a13085d8b4bf3bd6279354` |
| `scripts/tests/test_verify_module_id_discover.py` | `DiscoverySummary` (5 tests) | see the commit |
| `scripts/tests/test_ct_cli_regression.py` | `CTFIX06_DiscoverySummary` (3), `CTFIX06_EvidenceLineAndRegistry` (2) | see the commit |
| `docs/records/decisions/2026-10/ct-fix-06-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-data.txt`, `c5-python-runs.txt`, `drivers/` | in the commit |

## The cases (real command lines where they matter)

| Item | Normal | Broken (now refused or shown) |
|---|---|---|
| Summary — counts and place | the fixture's report: ÖZET lines right under the header, before `sorgu:`; the BULGU count equals the gate's decisions; the gate 0; `--check-report` 0 | — |
| Summary — collision | parent `MOD-0288` and `MOD-0288-FU06B` are no collision | one file id on two pack paths (the fixture, real `--discover`): "ÖZET olası kimlik çakışması — inceleme gerekli …" first, the second pack's owner right after |
| Summary — alias | — | `MOD-0298` as a deprecated alias on two paths: an "ÖZET BİLGİ" line and "kimlik çokluğu — takma ad ilişkisi belirsiz", never "olası kimlik çakışması" |
| Summary — values | one value group with its blob and ref ids | different `owner` on another ref (real `--discover`): "belirsiz — 2 farklı künye" with the ref; a pack without a künye: "künye okunamadı (bilgi; kapı değil)" |
| Summary — budget | the real "Employee" block: 2874 bytes | 100 paths under one id and 40 other packs with long names: the block ≤ 3500 bytes, every line ≤ 420, the collision inside the first 4 KB, "+k yol" kept, "ÖZET EKSİK" present |
| EK1-2 (`acceptance_record check`, a real run record) | a filled line 1: 0 "ACCEPTED'e izin verir" | an empty output's line 1, the phantom line 2 after a final LF, a line of spaces: 1 |
| EK1-6 (`validate`, `require`, `require --in-force` on the branch registry) | the intact registry: 0 | `scope` or `evidence` empty: 1 "boş zorunlu alan …" |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the new tests against `c203303ce`'s sources | the summary: 10 test methods red (the new unit tests error, the R0 cases fail); EK1-2 / EK1-6: 5 failed subtests. Two old real-registry tests error only because the copy holds no `execution/registries` (environment) |
| Guard removal (`guard-removal.txt`) | 10 / 10 proven: G-S1…S7 (the summary written, the collision named, the alias line, the byte budget, blob ids, per-ref values, the count of paths not shown), G-L1, G-L2, G-R1. **G-L1's limit:** it only swaps `splitlines()` for `split("\n")` while the blank-line guard still refuses, so its red shows the test's sensitivity to the range / message, not that a wrong ACCEPTED comes back on its own; the wrong ACCEPTED itself is shown by red first on the base code |
| Real data (`real-data.txt`) | the committed tool and this one on "Employee": every line except the ÖZET, `araç:` and `rapor-sha256:` lines identical, BULGU 15 → 15, the block 2874 bytes with the MOD-0251 collision inside the first 4 KB; `--check-report` 0 on both reports; the real registry (1 row) has no empty field |
| The worktree | the seven self-tests (ct_queue 138, prompt_gate 103, acceptance_record 149, status_report 80, ct_route 197, known_failures 51, run_record 29); `scripts/tests` 129 / 129 before EK1-2 / EK1-6 (not the final tree), then the changed classes and their neighbours |
| Candidate 5 (below), on its sparse copy (`c5-python-runs.txt`) | **first run:** the seven self-tests passed; `scripts/tests` 165 tests, 1 failure, and `sync_adapters --check` 4 problems. The 4 are measured: the first sparse set had no `.codex/` or `.agents/`, where the generated adapters live. That run kept only the summary, so its failure's name is not in its own log. **Second run** (the set widened with `.agents/`, `.codex/`, `.github/`; full log): `--check` OK 9; `scripts/tests` 165 tests, 164 passed, 1 failure — `test_check_cross_db_enforcement.CrossDbGateTests.test_the_repository_passes`, because the sparse copy has no `services/` and the gate fails closed ("grep: services: No such file or directory"); the tree clean after. That this was also the first run's failure is an inference from the same position, not from its log. The same test on Codex's full copy of candidate 5: passed (Codex). Not claimed: 165 / 165 in one run |
| Architecture | **NOT RUN** for this Python package. Candidate 4's run (137 / 144, raw `dotnet test` exit 1) stays historical evidence and is not this tree's |

### Candidate 5

| What | Identity |
|---|---|
| Base | candidate 4, `3d4f0f4050b1b162555277f2296f42648c299eb7` (unchanged; its scratch clone and checkout were not touched) |
| Overlaid | 5 CT-FIX-06 files (base `c203303ce`) and 2 İ5B-FIX-07 files (base `5cd4cc527`); every base blob equals candidate 4's — a clean overlay (`drivers/build_c5.py.txt`) |
| Candidate 5 | `89eab8a450c6298dbcdcee1b33ec00761f581b88`, tree `831c8879f8f78610d6d3c9910174a2235ed47479`; built in a fresh repository with no checkout whose objects borrow candidate 4's store read-only; 7 files differ from candidate 4 |
| The test copy | a sparse checkout of candidate 5's `scripts/`, `.antigravity/`, `execution/`, `docs/`, `.claude/`, `gateway/`, `tests/`, `AGENTS.md`, `CLAUDE.md` (387 MB); its 7 changed files equal the worktrees' bytes. It is a CLI evidence copy, not the full combined tree, and has no visible head or remote refs |
| After it | this README, the decision record and this folder's files only — candidate 5 is the source snapshot; these documents are not in it |

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| The summary through the real CLI on `57555e59` (normal, `--check-report`, a changed summary refused by the hash, the prompt gate's reading, BULGU / BİLGİ equality, collision and ref / blob ambiguity, 80 long same-id packs, the alias line) | 11 / 11 | `ec89afb554417b7a7bc052ba90947e2bb61939814c5ce7531cce483196f58da3` |
| The real "Employee" report | 15 BULGU, a 2874-byte summary, the source unchanged | `ecb38c77b8c8919088e6199a24143e8a369a64545b6cf7f014ad27777c11c89b` |
| EK1-2 and EK1-6 through the real CLI | 27 / 27 (LF, CRLF and no final LF filled lines accepted; empty, phantom, blank and a real empty line 2 refused; each of the 9 registry columns empty refused by `validate` and `require --in-force`) | `16c1b2dc1ca8631f1c0701cbcf5a707f9a844e5036593ebf4058345ef8a50b93` |
| SCE `validate` with the committed and the new `ct_queue.py` on the same data | exit 0, stdout and stderr byte for byte equal | `e783c3a754d903889202945e5d2fea90377298b865e3741921c01e5cc8c6d95e` |
| Candidate 5's identity | parent candidate 4, 7 allowed deltas, the 7-blob manifest | `3abf29d7148d84b33f628be382a7a9c304d09f1c8ff711b7b6c58a81b9ae3cdd` |

Codex states its own limits: interim snapshots of its fixed counter-example sets, not a final-tree test or a quality
score.

## Limits

- The summary is information. It does not make a reader cite a collision, it does not decide a duplicate, and not
  every finding fits in 4 KB — what does not fit is counted.
- EK1-2 checks that a cited line exists and is not blank, not what it says.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
- The Employee normal pilot on candidate 5 is Codex's, needs the owner's new approval and is recorded separately.
