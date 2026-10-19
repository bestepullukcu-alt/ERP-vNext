# Evidence — CT-FIX-01, the Control Tower gates refuse broken or conflicting input (2026-10-17)

Authority: `CT-FIX-01-JOINT-DECISION-01` (joint decision of Codex and the AI session; the owner said "ok" — relayed by
Codex). Branch `chore/ct/ct-fix-01` on `f5e6562014b887505d681519b1215c5aaa40ed7c` (PR #147's head), local only. The AI
session is the single writer of this branch; Codex verified independently.

## What changed

| File | Finding | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | 05, 06 | `ed3bb9b61187fc694c1991bcd41f39af92a3a4dd3d064cc46cb63b9d69c4edcd` |
| `scripts/prompt_gate.py` | 05, 11, 13 | `627776e91b778fc3854ac182de0d95c4f25ab683a9022603b0c19e3e5744a52a` |
| `scripts/acceptance_record.py` | 13 | `4c71c1f737b5cb5f9e097ffb1a7e13d3a737b124dc6ac9de6b39d52a8f2f028d` |
| `scripts/status_report_v2.py` | 05, 06, 13 | `1cfe0e1ab592266c41a73ce173d7f4a8ecd6000775199bba76612fc35e377cd0` |
| `.antigravity/scripts/verify_module_id.py` | 09 | `e3f6bb24927358fdd58ef0b66a8c197f35dd125b0f02b68213ad45a518c77bca` |
| `scripts/ct_route.py` | — (one self-test fixture value: `"not-a-commit"` → `"0" * 40`, so the case still tests "not a commit") | `37c12f648a818af2d7b3d97a4e22de698f053cfe70ea3d1c7c37f0c2054d2278` |
| `scripts/tests/test_verify_module_id_discover.py` (new) | 09 | `ccca243f31adcc9befeb86cebda9a38423c420f256e4569c8d0c22c5ed3407c0` |

## Checks and results

| Check | Result |
|---|---|
| Red trials before any source edit (`red-before.txt`, driver `red_trials.py.txt`) | 15 / 15 RED on the unmodified f5e656201 / c02b28c16 code: 05a, 05b, 06a, 06b, 09a, 09b, 11a–e, 13 ×3, B |
| Same inputs after the fixes (`green-after.txt`, driver `green_trials.py.txt`) | 0 / 15 RED, through the consumer paths (`load_for_dispatch`, `dispatch_problems`, `profile_terminal`, `commit_id`, discovery, `check_authority`) |
| Sabotage — one fix undone at a time (`sabotage.txt`, driver `sabotage_fix.py.txt`) | 21 / 21: the named test failed every time, every file restored to its bytes (A01–A20 here, B01 on the İ5-B line) |
| Self-tests (`python3 -B <tool> --self-test`) | ct_queue 81 · acceptance_record 101 · prompt_gate 85 · status_report 77 · list_screen_touch_hook 21 · known_failures 24 · ct_route 167 — all passed |
| `scripts/tests` (the phase1 command) | 44 / 44 on this branch |
| Real data, old vs new reading | 167 module packs: 92 approved before and after, no decision changed. 79 decision records: 38 → 37, only `mvp6-loads-uptake-ph15-owner-decision-01.md` (see the decision record's narrow meaning). The registry's 249 data rows and its 3 headers pass the discovery check |
| Merged tree | `a48e73abcee0fd339cb28741a9b1bd7ceb2ae629` (main + #147 + #148 + İ5-B C3) plus this branch's 7 files and the İ5-B fix's 4 files: tree `aa0f2bad7bd09ac30421911bd12bb038654e88ed`, built by the AI session (temporary index, no commit on a source branch) and equal to the tree Codex built independently |
| Architecture suite on the merged tree (one run, `run.sanitized.trx`) | 144 tests, 137 passed, 7 failed; `known_failures.py check` BİLİNEN=7, YENİ=0, YENİ-AYRINTI=0, exit 0 on the raw and on the sanitised TRX; counters, outcomes and messages equal in both |

### The architecture run

- Fixture: a ref-less commit `fba0eaebbde175087c0e233b945666f133b76d06` of tree `aa0f2bad…` in a scratch clone, checked
  out in a scratch worktree; `dotnet 8.0.416`, restore from the local package cache only.
- Lock: the shared wrapper `ct-heavy.py` was missing from its folder when the run started; why is NOT VERIFIED. The
  first start therefore failed before `dotnet` ran — no build, no test, no TRX; it is not a test result. The run then
  used `locked_run.py` (SHA-256 `9fa85857fee462256751518a7a8ebab9d63e69348b834e43a09ea08bd68728e5` as run), which follows
  the wrapper's documented contract: an exclusive `fcntl.flock` on the same `<SHARED-TMP>/ct-heavy.lock`, the inode
  checked after acquiring, `MSBUILDDISABLENODEREUSE=1`. The copy here (`locked_run.py.txt`) has the lock path replaced
  by `<SHARED-TMP>`.
- Raw TRX SHA-256 `458acc0550b24d887f6a2d6eb2f391e4f3e181a93c595ef114010dbcb79b0378` (not in Git: it holds local paths).
- Sanitisation, two steps:
  1. `normalize_root.py.txt` (SHA-256 `b5a28fbfce96ac4db669bf2d2b3d2708866183b1e61599b6ab107691ba43af28`): N1 the
     fixture root → `__TEST_REPO__` (318, case-insensitive: the TRX writes `storage` in lower case), N2 the scratch
     root → `__SCRATCH__` (0). Output `0636b9755ea558c64e44a84d5ab05a5e479ea2f276f07b9e4d1b5e54a33f9bed`.
  2. `docs/records/audits/2026-10/ct-pre-merge-fixes-01/sanitize_trx.py.txt` (unchanged, SHA-256 `b015a9b4…`):
     R1 = 0, R2 = 151, R3 = 1, R4 = 1; leftover home 0, user 0, unsanitised `computerName` 0.
- `run.sanitized.trx` SHA-256 `4150dced74b7e39821bc3631a904db39596c930803c650353864ad4e239d9474`: no user name, home,
  `/private/` or `/var/folders` path.

## Codex, independently (files not copied — they hold local paths; their SHA-256 only)

| What | Result | File SHA-256 |
|---|---|---|
| Real CLI fixture runs on the final sources | 48 / 48 | `914e9817833f8ece9e6b0bd48413b1a91397b930bf2eb5327b5eba345e886443` |
| Adapter checks | 6 / 6 | `21845d69a0a11bd1c8c957e689d04a57404eed18ea4ec90fde46b5148ebbcc45` |
| SCE parity, the current queue file | 500 ids, old vs new difference 0, 40 dispatchable (not a claim for every ref) | `b90ee893c8a277101c321ccf40e1721866ce14112b82db43ec18c5d495d4d95a` |
| Merged fixture (snapshot) | the 9 changed files' SHA-256 equal the table above; tree `aa0f2bad…` | `24a892f6dc5fac6743bd79de95709e8b0f71342dce2cc1a4f2ce586c4a84aede` |
| Suite on that fixture | self-tests 81 / 101 / 85 / 77 / 21 / 24 / 167, unittest 51 / 51, adapters 9 / 9 | `3c995d07a01cd93def9759defa166062dce3896f9da3c9c2200f73e13d67260a` |
| The architecture TRX replayed: `normalize_root.py` + `sanitize_trx.py` applied to the raw file again | sanitised bytes identical (`4150dced…`); counters 144 / 137 / 7 and every test name + outcome + message equal in raw and sanitised; `known_failures` exit 0 on both, BİLİNEN 7 / YENİ 0 / YENİ-AYRINTI 0 / KOŞMADI 0; N1 318, N2 0, R1 0, R2 151, R3 1, R4 1, leftover home / user / host 0 | `c064a99f76f43baa94e3ac22855e92d9288b7d400120b9720dc5ea097533106c` |
| The earlier joint pilot replayed on the final runtime sources | 29 / 29 expected, 0 findings; origin/main was a test imitation — no proof of the remote state or of CI | `b4922d25f00ef4ace3bb56de46be0da91e1343aae692ed4ae4a2d3e210cd7b5e` |

## Limits

- CI on these sources has not run (NOT VERIFIED). CI–local full fingerprint parity, the earlier E0 gaps and the old
  second run's source tree stay open, as before.
- The new İ5-B core's live loading is not measured (NOT VERIFIED).
- `ct-heavy.py`'s disappearance: cause NOT VERIFIED; the wrapper was not recreated in the shared folder.
