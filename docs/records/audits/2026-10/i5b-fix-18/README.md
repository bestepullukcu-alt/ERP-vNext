# Evidence — İ5B-FIX-18, a closing tag has the CommonMark form (2026-10-18)

Authority: `I5B-FIX-18-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-18-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-18` on `2de454f495d2a4cf274c2d7fabec7e8d05f8c67f` (İ5B-FIX-17), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by name or SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `CLOSE_SPACE`, `CLOSE_WS` and `CLOSING_TAG` (CommonMark's closing-tag form); the closing arms of `HTML_TYPE6` and `HTML_TYPE7` take the same ASCII whitespace; in `mask_html` a paragraph closing tag counts only when `CLOSING_TAG` matches at the same position with the same end, else its `<` is text | `54c647a2adef2b084cb50cd799bb5118cd02fbf059a08a926863f74f24d840cf` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_closing_tag_has_the_commonmark_form`; no earlier test changed | `4fc7cd62fe256c419ad6e22ca8c35021e8032425d076f1d4ba0360be2ee348ba` |
| `docs/records/decisions/2026-10/i5b-fix-18-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-cli.txt`, `checks.txt`, `drivers/` | in the commit |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the test against C16's `sync_adapters.py`, before the code | the final test: 9 cases fail, all in the new test; the 53 earlier tests pass. Run 01 (10, one of them a wrong positive) is kept |
| Real CLI (`real-cli.txt`) — 15 cases, write and `--check`, before and after | before (`0e456b0f…`) 6 of 15, after (`54c647a2…`) 15 of 15. One work copy, restored before every case |
| Guard removal (`guard-removal.txt`) | **20 of 20**: 6 new (G-Q1 … G-Q4, G-Q6, G-Q7) and the 14 earlier guards whose path runs through the closing branch or an HTML block's start line. The "same end" condition is kept as a defensive check with no guard that proves it: a seeded random search found no input where it matters (3591 valid closing tags, 0 with another end) — finite support, not a proof for every input |
| The real files (`checks.txt`) | read the same as with C16's reader |
| The bound run | as agreed, the one recorded run is the combined candidate's after the commit (C17); no separate component run is recorded here |
| Full `scripts/tests`, architecture, live model calls | **NOT RUN** |

## Codex, independently (its files hold local paths; their names only)

| What | Result | File |
|---|---|---|
| The C16 / v15 first reports (history) | Codex 79 / 100, UYGUN 20/0/0; Claude 72 / 100, UYGUN DEĞİL 18/1/1 (C16-F1) | `codex-c16-dar-kapanis-eki-v15.txt` (`b8cd68ba…`), `claude-c16-dar-kapanis-eki-v15.md` (`4acdbd62…`) |
| The root reproduction before this fix (history) | 10 scenarios / 20 real CLI: the three div forms 0 / 0 where 1 / 1 was expected; the controls 0 / 0 | `C16-own-invalid-close-CLI-01.json` |
| **The frozen source `54c647a2…` / test `4fc7cd62…`** | the two files read; the 53 earlier tests and their helpers AST-equal; 12 scenarios / 24 real write / check all as expected — the three div forms, inline and standalone NBSP and the backtick case hidden, the normal, nothing-open, raw-block, real-close, spaced and one-line-end cases 0 / 0. An instrumentation error in Codex's own driver (a variable overwritten when the final JSON was written) is recorded apart; it is not a product defect | `I5B18-own-closing-token-01.json`, `I5B18-own-probe-instrumentation-error-01.txt` |

## Limits

- Not an HTML, CSS or Markdown engine. NBSP follows the normative CommonMark model. C16-I1, a diagnosis, is deferred.
- The check proves where a required text and a heading stand in the source; it does not prove that a reader follows the
  rules. P4 and EMP-REPORT-01 stay open. GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
