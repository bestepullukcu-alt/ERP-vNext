# Evidence — İ5B-FIX-17, a tag ends outside quotes; a closing tag needs evidence (2026-10-18)

Authority: `I5B-FIX-17-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-17-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-17` on `eca57404d57350523da69bd54a33a62ff2454edf` (İ5B-FIX-16), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `TAG_ATTRS`: one quote-aware attribute part for `HTML_TAG` and `HTML_TYPE7`; in `mask_html`, `close_unproven`: a closing tag that would close an open element or a raw-text element counts only in an HTML block or on a proven plain paragraph line, else `close` — not verified; its message in `unreadable_html` | `0e456b0f2ef0e6ba542479a263637d8f2fce6c0841dde63c262df305c4f8b200` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_tag_ends_outside_quotes_and_a_close_needs_evidence` (the reports' cases, the controls); no earlier test changed | `e1a57d60acaf9699a07286462a04250fc7db69d601920398ac97f837a0778895` |
| `docs/records/decisions/2026-10/i5b-fix-17-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-cli.txt`, `checks.txt`, `drivers/` | in the commit |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against C15's `sync_adapters.py`, before the code | 14 cases fail, all in the new test; the 52 earlier tests pass |
| Real CLI (`real-cli.txt`) — 20 cases, write and `--check`, before and after | before (`93a51469…`) 8 of 20, after (`0e456b0f…`) 20 of 20. One work copy, restored before every case. The unreadable-link case exits 1 on C15 from DEADLINK, not from the visibility reader; its new rule is shown by the unit test and G-Z6 |
| Guard removal (`guard-removal.txt`) | on the final source **24 of 24**: 8 new (G-Z1, G-Z3 … G-Z9) and the 16 İ5B-FIX-16 guards whose line is in `mask_html` or `unreadable_html`. Run 01 on the first frozen source (24 of 25: G-Z2 redundant, removed) is kept. The other 42 of İ5B-FIX-16's 58 guards were not run again: their anchors are all still in the final source once and their code is untouched |
| The narrow run (`checks.txt`) | on **this branch's two frozen files**: `test_sync_adapters_budget` 53 / 53 (52 earlier, 1 new); `--check` OK, 9 adapters; branch, status and the two SHA-256 the same before and after (HEAD was not printed in that identity file). The real files read the same as with C15's reader |
| Full `scripts/tests`, architecture, live model calls | **NOT RUN** (the agreed narrow check; no new live call) |

**Two frozen sources.** `16a11a3a…` (the first) held a separate indented-code check; it was removed as redundant. Their
whole difference is `drivers/16a11a3a-to-0e456b0f.diff.txt`. The test did not change.

## Codex, independently (its files hold local paths; their names or SHA-256 only)

| What | Result | SHA-256 / file |
|---|---|---|
| The C15 / v14 first reports (history) | Codex 78 / 100, UYGUN DEĞİL 19/1/0; Claude 71 / 100, UYGUN DEĞİL 18/1/1; the same criterion 02 | `819d91614db4aa842cae03c1fd4c1c5617aa3da1147114a84355b1e56406c623`, `5ec2394452f86a4340e743cd35a09a349855db97e83d96e300c6782020771e03` |
| The two short solution notes | one quote-aware tag end, a closing tag with evidence or not verified, no attribute grammar | `a90b63c3f164c801b5af6434d6e4b0c3f78693ca565eb7389a9124947ab5f765`, `b74bfdcc819e798c9d688f2af6ddb20084072fb871ff9c13ab989b45461840a6` |
| Triage before this fix (history) | the quoted-tag cases, Claude's indented-code and link-title cases rendered and run (4 bad 0 / 0 where 1 / 1 was expected), and Codex's own expectation corrected | `C15-own-quoted-tag-01.json`, `C15-own-indent-link-02.json`, `C15-own-triage-correction-03.json` |
| The first frozen source `16a11a3a…` | the two-file diff read; 13 scenarios / 26 real CLI all as expected | `I5B17-frozen-component-independent-01.json`, `I5B17-own-fixed-C15-context-01.json` |
| **The final source `0e456b0f…`** | the removed check's whole difference read; the same 26 real CLI on a copy of the final source all as expected | `I5B17-own-fixed-C15-context-02.json` |

## Limits

- Not an HTML, CSS or Markdown engine. Where the reader cannot prove a closing tag's context it is not verified — a
  refusal, never a pass. Containers are not tracked beyond the listed rules; an opener in indented code or link
  metadata still opens an element (the declared over-refusal).
- The check proves where a required text and a heading stand in the source; it does not prove that a reader follows the
  rules. P4 and EMP-REPORT-01 stay open. GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
