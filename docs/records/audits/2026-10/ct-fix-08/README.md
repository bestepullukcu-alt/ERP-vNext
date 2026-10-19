# Evidence — CT-FIX-08, visible evidence text, NUL path listings and a broken registry that stops (2026-10-18)

Authority: `CT-FIX-08-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-08-joint-decision-01.md`). Branch
`chore/ct/ct-fix-08` on `5035a9f2853938ec640b4a96ef47bb3daab85ae8` (SETTINGS-HYGIENE-01 on CT-FIX-07), local only. The
AI session is the single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at
the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | `has_visible_text` (a letter, number, punctuation or symbol; Cf, Z*, Cc and marks alone are not visible); `nul_paths` (strict `-z` listing: ends with NUL, no empty entry, UTF-8 only, `b''` is no entry); registry emptiness by `has_visible_text`; `require` exits 2 on a broken registry; `validate` stops before reading a task file | `39d1e5af109e448bc8d151362ea75f2fdfe48201443d4b25caccde3b4dc869f9` |
| `scripts/acceptance_record.py` | an evidence line is split on LF only (CRLF normalized, no phantom last line) and needs a visible character; `tracked`, `changed` and the v2 record listing read `-z` through `nul_paths` | `45cad5095725cfa3a1339f0c3602a8e9684995a9ffd245909f3e692814c394d6` |
| `scripts/prompt_gate.py` | `files()` reads `ls-tree -r --name-only -z` through `nul_paths` | `763c0ebfc16b33612cde0d373d822d62b330635c1d449c3aa3385e96bbd95f5b` |
| `scripts/status_report_v2.py` | `files_matching` reads `ls-tree -r -z` through `nul_paths` (the helper loaded once) | `f63b1203c7c36e14e65238e51532237545e12a5357cc8b2bd0151d8ea2c851c8` |
| `.antigravity/scripts/verify_module_id.py` | discovery reads `ls-tree -r -z` through `nul_paths` (taken once, not per tree); the open-decisions line comes right after the counts | `79baf4cc36607530380e5eb025d82f775469e77fff73509619d119cc38875b1f` |
| `scripts/tests/test_ct_cli_regression.py` | `CTFIX08_VisibleTextAndPaths` (4); two CTFIX06 tests follow the declared changes (require 2; lines found by prefix) | `c1d87d54a8c2bf2e40eb842bc455ea7b3673ae1a30eb27300ec9c0374b0158bc` |
| `scripts/tests/test_verify_module_id_discover.py` | `test_the_open_decisions_survive_a_wide_query`; the open decisions asserted second | `8ce30d634d958b88b7f127d7c2df8974fa0780bb0333620bc0a9ebd54b1e6293` |
| `docs/records/decisions/2026-10/ct-fix-08-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-data.txt`, `checks.txt`, `drivers/` | in the commit |

The invisible characters in the tests are written as `\u…` escapes, never as literal characters.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test files against `5035a9f28`'s sources | the 6 new or changed tests fail (15 failed subtests and one error: the base has no `nul_paths`); the 2 controls pass |
| Guard removal (`guard-removal.txt`, `drivers/harness7.py.txt`) | 11 / 11 proven on copies whose production files equal the final sources byte for byte; G-V2's red is broader than its one case (noted there) |
| Real data (`real-data.txt`) | "Employee" discovery: every line except ÖZET, `araç:`, `rapor-sha256:` identical; BULGU 15 → 15; the same ÖZET lines, only reordered (open decisions line 5 → 2); `validate` and `summary` output identical to the base tools' |
| The final run (`checks.txt`: the commands, the versions, the log's SHA-256) | in **the CT-FIX-08 worktree** on the frozen sources: the seven self-tests (140, 103, 149, 80, 197, 51, 29) and `scripts/tests` 141 / 141 |
| Architecture, live model calls | **NOT RUN** (a Python-only change); no new live call (joint decision) |

**What the final run is and is not.** It ran once, in the CT-FIX-08 worktree, on these seven files. It is not a run on a
combined candidate: İ5B-FIX-09 is a separate branch, checked in its own folder
(`docs/records/audits/2026-10/i5b-fix-09/`). The CT run, İ5B's 35 unit tests and Codex's 34 İ5B CLI checks are three
results on two trees; together they are not "one full run of a C8". If a C8 is built, its changed blobs must equal the two
tested branch tips; a combined smoke, if one is wanted, stays narrow.

An earlier R0 run on the sources before the last edits is diagnostic only (`checks.txt`), not evidence of these sources.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| CT-08 source snapshot 02 (the final ct_queue `39d1e5af…`) | the 27 CLI repeats pass; 20 real Git-reader API, 2 bad-byte decoder API and 8 NUL-contract API checks green | `0530ccad6c2232a145766e2d6e5a28d26675491d0875ddbd8d6dc4331c0a8863` |
| NUL contract | 8 / 8 on the final decoder (5 / 8 on the earlier copy: a missing final NUL, an inner empty entry and a lone empty record passed) | `3a6caa8f5cf4b03fb70b80f62c57f27e37bcbc764ee30438dd467049d9fbe07a` |
| Pre-commit source binding | 27 CT CLI + 34 İ5B CLI; 30 API apart | `05d4aea44de53924db8982fc49c701da41e09025f901a2efc83478018b55a438` |
| Real SCE comparison with the final ct_queue | C7 and current: the same real data, exit 0 / 0, stdout and stderr byte-equal; 4 data files unchanged | `a9b944a08e9b06f8a24632fb4f843bfcf25fe48faff5b7b3183345ec8d5d5751` |
| Instruction inputs | every manifest source and generated target plus AGENTS, CLAUDE, the reference and the manifest (17 files) byte-equal to C7 — so no new live call | `cd9c16616115dee445907d5f9d20ba6ed036c575227a19cff217428fb1ee3b20` |
| Earlier snapshot (before the helper was hoisted and the decoder tightened) | 27 CLI + 20 Git-reader API + 2 decoder | `d2434f49cc013a3e8977f0f02d5683220a34206fa4f197a603df5f959fe34c03` |

## Limits

- A real Git file name that is not UTF-8 is **NOT VERIFIED**: macOS refused to create one (EPERM); only the decoder on
  bad bytes is proven.
- C7-A (one id in a main pack and a duplicate pack) stays a declared limit, not a stop: three legitimate multi-pack ids
  exist at HEAD (MOD-0024, MOD-0290-FU01, CAND-CAP-0006); a source author can still write a duplicate.
- C7-B (an ancestor rule for the input's freshness) is not taken now; a stale input stays an open risk.
- A registry value of "-" is out of scope (a placeholder policy would be new policy); the real registry has none.
- The P4 "onaylı" wording defect and EMP-REPORT-01 stay open. The C7 component measurement is kept for the same source
  bytes; no claim is made that a C8 normal Employee run or P4 improved.
- `ct_queue.py` line 123 holds a literal BOM from before this package; noted, not changed.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
