# Evidence — CT-FIX-09, the gate names why a queue could not be read; a file name stays on one output line (2026-10-18)

Authority: `CT-FIX-09-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-09-joint-decision-01.md`). Branch
`chore/ct/ct-fix-09` on `65c53153e3d9a1e40372f15df1509a34d582184d` (CT-FIX-08), local only. The AI session is the single
writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | `one_line`: Cc, U+2028 and U+2029 shown as `\n`, `\r`, `\t`, `\xNN`, `\uNNNN`; everything else kept. Its self-test case. `fail()` and the CLI are unchanged | `9c2d6d2d80130b2032ecca53bb8cd3f6582baaa566408f105993b9dd2a5f8c09` |
| `scripts/prompt_gate.py` | `real_queue` reads the queue with ct_queue's stdout captured (`_real_queue` is the unchanged body). A stop is named inside the hashed report, and output on a successful load is a queue error. `check_discovery` says "kuyruk okunamadı — keşfin bu işe bağı ölçülemedi" when there is no queue row. Self-test cases for the printed stop and the unexpected output | `239d41144793634e915eea0c3527a0dc20ac6e4508d37df4290f131d3fe1d182` |
| `.antigravity/scripts/verify_module_id.py` | `_one_line` (the shared helper, loaded once; without it every line-starting character becomes `?`). It applies to each ÖZET item before `_cap` and the budget, and to each logical report line | `a6e2cf294fbe44a57f5471458c22350253cf3cd5b9ec0ec13639bc305b3bcddb` |
| `scripts/acceptance_record.py` | `check`'s one output point through `one_line` | `2fae62f3b13ed241124cfcac94c930f740aad79f48fb4cda9a64c3c6a8a46726` |
| `scripts/tests/test_ct_cli_regression.py` | `CTFIX09_GateDiagnosisAndOneLine` (4): the gate on an empty path, a Cf-only control_tower and a non-UTF-8 queue file; pack names with LF, CR, TAB, U+2028 and 240 line feeds (252 bytes); an acceptance check with an LF file name; `one_line` itself | `8aaa34366a5ca31126f80b7207a33f4a352ae351d1deaf88052c7d524686239d` |
| `docs/records/decisions/2026-10/ct-fix-09-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-data.txt`, `checks.txt`, `drivers/` | in the commit |

The invisible characters in the tests are written as `\u…` / `\x…` escapes. An editing tool had turned some of them into
literal characters; they were written back as escapes before the freeze, and the freeze scan found none. The
pre-existing literal BOM at `ct_queue.py:123` is unchanged.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `65c53153e`'s sources | the 4 new tests fail (9 failures; 12 errors where the base has no `one_line`); the 2 controls pass |
| Guard removal (`guard-removal.txt`, `drivers/harness8.py.txt`) | 7 / 7 proven on a copy equal to the frozen sources. A first run is not counted: G-Q3's red was a crash of an incomplete copy. The rerun needs the named self-test case to report FAIL without a crash |
| Real data (`real-data.txt`) | "Employee" discovery: 627 lines, BULGU 15 → 15; only `araç:` and `rapor-sha256:` differ. The gate's queue read on the real registry prints nothing: closed and unverified rows answer 1, Q62 answers 0 |
| The final run (`checks.txt`: the commands, the full log's SHA-256, before / after) | in **the CT-FIX-09 worktree** on the frozen sources: the seven self-tests (141, 105, 149, 80, 197, 51, 29) and `scripts/tests` 145 / 145; 3 existing ResourceWarnings. Before and after: the same HEAD, branch, modified files and SHA-256. The status differs only by the decision record, written while the run went on |
| Architecture, live model calls | **NOT RUN** (a Python-only change); no new live call |

**What the final run is and is not.** It ran once, in the CT-FIX-09 worktree, on these files. It is **not** a run on a
combined candidate. İ5B-FIX-10 is checked in its own folder. C8's 178 / 178 is not a test of this version, and a
combined candidate (C9) has not been built or run.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| CT-09 source snapshot (the four code files and the test as frozen) | the sources the same before and after | `117cbf8225cef1fbe0f92f977e29cf2267c5b6edfc9147c32f77e2366419a6cb` |
| The gate, 3 real CLI | normal 0; a broken registry 1 with the real DUR inside the report, nothing printed before it, no false name diagnosis | `ccda4dceb84560ace0a3c0edf495ec564f45b7eec403b2c897c529ca92e2c2a4` |
| Discovery and report, 7 real CLI | an LF pack name: one result and one exit line, `--check-report` shows the right result; a complete report with a decision for every finding: gate 0; a non-UTF-8 Git name still discovery 2 | `35ea36921d6edefd80aa5fdb714e0c74012955f6a2ffe6cbec278de5dc4e166b` |
| The acceptance check, 2 real CLI | normal 0; an LF file name outside the evidence: 1, the false ACCEPTED on one physical line | `50e52227b35b8c6f413a0d8c21315086426c89829b9b7960577742dab6e9147e` |
| Output boundaries, 3 real CLI | the 252-byte / 240-LF name: ÖZET at most 420 bytes a line, 1778 in total, the physical count right; without the shared reader: discovery 2, a full report, no traceback | `77bfb070ade886aead1b77ec33411477bdbf52d6c9bd82533978cefb65fba3e4` |
| Structural review (AST) | the queue loader's body and `ct_queue.fail` unchanged; VMI `--check-id` / `--candidate` and the acceptance rules unchanged; the 17 instruction / adapter inputs equal to C8's. Not a 172-id, historical SCE or live measurement | `f6a77a8a0f6b36e393417b0b22bdea0e68738a4f5b70eecd577ae12b05bf3280` |
| C8 acceptance output (before this fix) | 2 real CLI: normal 0; an LF file name 1 and a false ACCEPTED result line — the counter-example this fix closes | `63bf3a16e287a2564f54795e7f4202d81b735a66fce450a4f9654e1e87341f13` |

## Limits

- `one_line` is display only and is not unique: a backslash is kept, so a real `\n` in a name and an escaped line feed
  look the same. It is never decoded back or used as evidence of the real name.
- Covered output points: the discovery report and the acceptance check. `module-check` and other output points are not
  claimed.
- A real file name that is not UTF-8 is NOT VERIFIED on macOS (EPERM). The Git-object fixture gives discovery 2.
- The P4 wording defect and EMP-REPORT-01 stay open. C7-A, C7-B and the "-" placeholder stay as declared in CT-FIX-08.
- GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
