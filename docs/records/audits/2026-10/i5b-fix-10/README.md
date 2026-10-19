# Evidence — İ5B-FIX-10, a required text only in visible text; an escaped '<' and an autolink are not raw HTML (2026-10-18)

Authority: `I5B-FIX-10-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-10-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-10` on `c909fe7a3a6dc2ef70d6979b566dbd6c7b395c1d` (İ5B-FIX-09), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `link_metadata` / `on_rule_line`: a required text does not count in a link's destination or title, a whole image, or a rule line that is a reference definition (`LINK_METADATA`, `RULE_REFERENCE_DEF`). `unreadable_link`: a rule line with a `](` left after the links the check reads proves no required text ("not verified"). `html_scan`: `<` after an odd number of backslashes (`ESCAPED_LT`) and the common URI and e-mail autolinks (`AUTOLINK`) are taken out before the opener check. The CORE-RULE message names each case | `491fed3e0d93d87acfd20a2d7d11bf493ffac20980acfeded14dbf586e282657` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_required_text_only_in_link_metadata_is_lost`, `test_link_syntax_the_check_cannot_read_is_not_verified`, `test_an_escaped_less_than_or_an_autolink_is_not_raw_html` | `338db3eaf48fdc3c724181c01e696315993b1563f03a3175df98a62a6292ed46` |
| `docs/records/decisions/2026-10/i5b-fix-10-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json` and the generated adapters are not changed.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file (the whole suite) against `c909fe7a3`'s `sync_adapters.py` | the 3 new tests fail: 8 hidden-metadata, 8 false-refusal and 5 depth subtests. The 35 earlier tests pass in the same run. The depth test's first red, against the interim `d65dd0cd`, is kept there as history |
| Guard removal (`guard-removal.txt`, `drivers/harness8.py.txt`) | 7 / 7 proven on a copy equal to the final sources byte for byte, G-N1 (the not-verified refusal) included. The interim 6 / 6 on `d65dd0cd` is not counted for these sources |
| The final run (`checks.txt`) | in **the İ5B-FIX-10 worktree**: `test_sync_adapters_budget` 38 / 38; `--check` OK, 9 adapters; source identity the same before and after |
| Architecture, live model calls | **NOT RUN**; no new live call |

**Kinds of evidence, kept apart.** The red-first, guard-removal and final runs here are unit / API checks
(`check_instruction_budget`). The real `--write` / `--check` CLI counter-examples are Codex's (below). A probe the AI
session ran during triage is API level too. Its parenthesized-title case is not used as evidence: a quoted rule that
holds `(en, tr)` is not a valid parenthesized CommonMark title.

**What the final run is and is not.** It is a run on this branch's two frozen source files, in the İ5B-FIX-10
worktree. It is not a measurement of a complete final Git tree. The CT-FIX-09 run is on the CT branch's frozen files
(`docs/records/audits/2026-10/ct-fix-09/checks.txt`). Neither is a run on a combined candidate (C9).

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| İ5B-10 interim, bound to `d65dd0cd…` / `402f9b57…` in a separate 19-file copy (history: an earlier source) | 30 / 30 real write / check CLI: double- and single-quoted titles, an image's alt text and a numbered reference definition refused; a visible label, a reference link, an escaped literal and an autolink pass; raw HTML and an even backslash refused; every file under 22000 bytes, no budget warning | `aecd55f09ce117b246abfd28546ca5baac3eb380cb7278759b7a540d8aeedead` |
| İ5B-10 nested metadata, on the same interim source (history) | 8 real CLI: a label nested three deep and a destination nested two deep, the rule only in the title, passed write / check 0 / 0 — valid CommonMark the one-level reading missed. Normal before and after 0 / 0. These are the counter-examples the not-verified refusal closes | `7d75e420ad6191ce456c61e3767bd08b704ae65dd25267d9f7a036f51844ce7d` |
| İ5B-10 source snapshot 01 (history: the interim source `d65dd0cd` / `402f9b57`) | the two interim files as frozen; the source the same before and after | `2270dd736c9ef37e8147d97b8dacd0960072cc7408bd2ba06a1da4045e7ed07c` |
| **Final:** İ5B-10 source snapshot 02 (`491fed3e` / `338db3ea`) | the two final files as frozen; the source the same before and after | `6fa9f08740f6161e9e043b72d403c7a8ab9964ca8dc9659be2aee1e38fd36d4d` |
| **Final:** the affected metadata cases, 24 real CLI on the final source | the two deep counter-examples, the deep image and the deep visible label refused as NOT VERIFIED; the one-level forms refused with the metadata message; the normal label and plain text pass; every file under 22000 bytes, no budget warning, the source unchanged | `79d3cc7371399196c61c0baa89e1d0ea1e23a36453e02360ea22c56dd76220ae` |
| **Final:** HTML and normal smoke, 6 real CLI on the final source | as expected | `0d2d3da560891a3371768c88ac492244fc5f424e4044d74dbba2a32e8f91be80` |
| **Final:** component review | the 38 / 38 log, before / after, the 7 / 7 guard removal and the two source SHA-256 read independently | `e1bb92435165cfb1d22165826f1df2baf5c06082e494abafdfeacdcdd26e9aa9` |
| C8 extra hidden metadata (before this fix) | 8 real CLI: an image's alt text and a numbered reference definition pass 0 / 0 — the counter-examples this fix closes | `da541a03cf6ae67269771bf414cfbf45ae5d6d95dfa0fda4a6717e5694bd3440` |

## Limits

- The common forms, not a full CommonMark parser. Deeper nesting than one level is not read: such a rule line is
  refused as "not verified", never passed. A deep link whose visible label holds the rule is refused too (a declared
  false refusal); the 11 real core rule lines hold no `](` and are unchanged. HTML entities (`&lt;`) and other link
  syntaxes are not claimed.
- The check proves where a required text stands in the source. It does not prove that a reader follows the rule. No
  claim is made that a natural Employee run or P4 improved.
- GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
