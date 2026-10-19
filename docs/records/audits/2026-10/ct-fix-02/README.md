# Evidence — CT-FIX-02, one approval reader, profile conflicts with main, a trusted known-failures registry (2026-10-17)

Authority: `CT-FIX-02-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-02-joint-decision-01.md`). Branch
`chore/ct/ct-fix-02` on `0dae2dcc6cd5c5b2ecc648fcbc1b434ba6da886c` (PR #147's head), local only. The AI session is the
single writer of this branch; Codex verified independently.

## What changed

| File | Item | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | K1 the shared reader (`kunye_status`, `GOV_APPROVED`, quotes), K3 `main_profile_problems` in `load_for_dispatch` and validate, `strict=yes` ⇒ `sop7`, K4 behaviour tests, the missing-file DUR | `c59656d3178ca986203c5259062dae26d1094145f143a7318704f43a4b5634b0` |
| `scripts/prompt_gate.py` | K1 the gate binds ct_queue's reader (its own copy removed); one quote self-test | `25e71a81aed9f7d2fc7a8fc85ef3500bf9b651c54ee05ee2716d8bc9bd32cd35` |
| `scripts/status_report_v2.py` | K1 the effort approval through the shared reader, K3 a profile conflict is an incomplete reason (in `read_strict_queue`, so module-check too), K4 behaviour tests | `220724bf028891615c0b375bab496d9df61d2ea236f9bac005dfb5b205aaac81` |
| `scripts/known_failures.py` | K2 `check` and `should-rerun` apply `validate()` and the item checks first; a check without `--suite` refuses a name open in two suites | `709d337d14083180ac5d842e8d078444e798699ff9aa6b1c6132b0523096e811` |
| `docs/records/decisions/2026-10/ct-fix-02-joint-decision-01.md` (new) | the decision | `5f5118272a87358a72c2781c59abf7c786e962a793f1ecc58ae33741ac736978` |

## Checks and results

| Check | Result |
|---|---|
| Command-line trials before the edit (`red-before.txt`, driver `ctf2_trials.py.txt` `eb04a8c8b6b368783b31ffbc2b387f49550cc5f93f4145146a6fb171de3be641`) | on `0dae2dcc6`'s tools in a fixture clone outside the repository (fixture commits, an origin/main imitation): 42 target cases, 38 RED; 13 controls, all as expected. The 4 targets already safe before are quote forms the old -R / report regex happened to refuse |
| The same driver after (`green-after.txt`) | 42 targets, 0 RED; 13 controls unchanged |
| One designed change (D-01) | an -R3 record `status: "approved"` (one balanced pair) was refused by the old -R reader and resolves now — the gate already read it so |
| Sabotage, one fix undone at a time (`sabotage.txt`, driver `sabotage_fix02.py.txt` `cdf69288bd4303b76cd1e86646404890cc7506e7fd2440fc6e242c4786a58114`) | 17 / 17: the named self-test failed every time, every file restored to its bytes. B11 and B12 are the two old wiring tests' blind spot (the searched text kept, the wiring broken) |
| Self-tests (`python3 -B <tool> --self-test`) | ct_queue 90 · acceptance_record 101 · prompt_gate 86 · status_report_v2 79 · list_screen_touch_hook 21 · known_failures 28 · ct_route 167 — all passed |
| `scripts/tests` (the phase1 command) | 44 / 44 |
| Real data, old vs new reading (`real-data.txt`, driver `real_data_compare.py.txt` `9ceb5bce64db636116507941f1cffa597abe229b4fdc16e664f95d3f9469db8e`) | 80 decision records: -R3 reader 39 → 38 and report reader 39 → 38, only `mvp6-loads-uptake-ph15-owner-decision-01.md`; gate 38 → 38. 163 module packs: 92 → 92. Registry: no problem, origin/main has no registry. SCE: 500 ids, 40 dispatchable, no decision changed; summary and validate output identical. known_failures: validate OK; the committed CT-FIX-01 TRX BİLİNEN=7, identical output with and without `--suite` |
| Architecture suite on the merged tree (one run, `run.sanitized.trx`) | tree `1199b3ed07f8a21eb5e57b99b90c88d24abcd902` (commit `ca2bf5891f3e3cea642ea53f2c6f05075a7c76ab`, ref-less, scratch clone) = CT-FIX-01's tested tree `aa0f2bad…` plus 8 paths: the 7 existing ones (this branch's 4 scripts, İ5B-FIX-02's 3 files) were, before the overlay, equal in `aa0f2bad` and in the parent heads `0dae2dcc6` / `c82d7db2c`; the new decision record is the 8th. 144 tests, 137 passed, 7 failed; `known_failures.py check` BİLİNEN=7, YENİ=0, exit 0 on the raw and on the sanitised TRX; counters, outcomes and messages equal in both (143 / 143) |

### The architecture run

- Lock: `locked_run.py` (SHA-256 `9fa85857fee462256751518a7a8ebab9d63e69348b834e43a09ea08bd68728e5`, recorded as
  `docs/records/audits/2026-10/ct-fix-01/locked_run.py.txt` with the lock path replaced): an exclusive `fcntl.flock` on
  the shared `ct-heavy.lock`, `MSBUILDDISABLENODEREUSE=1`. `dotnet 8.0.416`, restore from the local package cache only.
- Raw TRX SHA-256 `f53179cfc90298b01460661f8a413b3e72f392297ff62590f279bcb5ccf56ec9` (not in Git: it holds local paths).
- Sanitisation, two steps, the recorded scripts unchanged: `ct-fix-01/normalize_root.py.txt` (`b5a28fbf…`) N1 = 318,
  N2 = 0 → `343a54669c9ed71a40c7a1c4f9c9fb59a4bc6498c32fc62946bd10d41bc78763`; `ct-pre-merge-fixes-01/sanitize_trx.py.txt`
  (`b015a9b4…`) R1 = 0, R2 = 151, R3 = 1, R4 = 1; leftover home, user and host 0.
- `run.sanitized.trx` SHA-256 `8524198e64107d20cc00f98e1b18178e75555b65f620445783eccd29dc700286`.
- The evidence files of this folder are not in the tested tree; the decision record is.

## Codex, independently (files not copied — they hold local paths; their SHA-256 only)

| What | Result | File SHA-256 |
|---|---|---|
| Its main counter-example matrix on the final sources | 28 / 28 = 23 real command-line runs + 5 production-function calls; the sources did not change during the run | `76512f19c645bb7d3334c8d02618dc61bc7ef0c8497a4438843147449f992555` |
| Its quote and approval matrix | 19 / 19 = 5 report command-line runs + 14 production-function calls | `9746d42f578e1c401e2b56252dce6d6647e3787f7efcc318518e6cd1f41a96a5` |
| Real data | SCE 500 / 500 ids, 40 / 40 dispatchable, difference 0; the original raw CT-FIX-01 TRX: old and new check output identical | `32df3548d51509d9f458e11b22d8b1d87b35df2f4c885800f9dbb307100031d7` |
| Together | 47 / 47 = 28 real command-line runs + 19 production-function calls; neither matrix built a full §36.1 prompt pack for the gate's command line | — |
| The final source SHA-256 it verified | equal to the table above (ct_queue `c59656d3…`, known_failures `709d337d…`, prompt_gate `25e71a81…`, status_report_v2 `220724bf…`) | — |

## Limits

- CI on these sources has not run (NOT VERIFIED). CI–local full fingerprint parity, the earlier E0 gaps and the old
  second run's source tree stay open, as before.
- The prompt gate's quote handling is tested through its production function `check_authority` and its self-test; the
  AI session's driver did not build a full §36.1 prompt pack for the gate's command line. The comment in
  `ctf2_trials.py.txt` that "the gate's CLI path is Codex's independent driver" is not correct for the current tests:
  neither of Codex's two current matrices built a full §36.1 prompt pack for the gate's command line either. The driver's
  bytes are left as they ran; an earlier version's command-line measurement is not carried over to these sources.
- The fixture's origin/main is an imitation; nothing here proves the remote state.
- F-6 to F-9 of the v3 audits are not in this change.
