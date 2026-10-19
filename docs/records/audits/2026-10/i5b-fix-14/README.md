# Evidence — İ5B-FIX-14, one scanner masks comments in place for every reader of the instruction files (2026-10-18)

Authority: `I5B-FIX-14-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-14-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-14` on `bb2a6e7412bb35b8c49bf5e8514b9c7be66ba682` (İ5B-FIX-13), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `scan_comments`: one small scanner on the source as it stands. It masks every HTML comment in place (spaces, line ends kept), outside fences and outside inline code read within its line, and returns the comments' offsets and the context open at the end. Fences follow each reader's own grammar: the heading reader's (`heading_fence_opener` / `heading_fence_closes`, 0–3 spaces, as in `level2_sections`) and the core's (`fence_opener` / `fence_closes`). Its readers: `visible_text` (the required headings and sections), `heading_in_comment` (the message), the core block's view, the text before the core, and `open_context`. The `HTML_COMMENT` regex is gone | `e5b55f888c1f9520cc2f40997024f216fb0f25fdef902af94f163f51fe2cb8e6` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_the_heading_reader_reads_comments_and_fences_in_order`, `test_the_core_view_masks_comments_outside_fences`; İ5B-FIX-13's test comment corrected (the AST unchanged) | `985c21d97f045432e5ee53f545aae1fac7fb1b44c8fe9cab1061494357655c31` |
| `docs/records/decisions/2026-10/i5b-fix-14-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json`, the generated adapters and the other instruction inputs are not changed.
`outside_code` (imports) and `known_spans` are unchanged; `level2_sections` itself is unchanged.

## Real data (measured)

- `AGENTS.md`: 15 headings, the same; every section's non-blank body lines the same. The raw visible text differs because
  a comment is now masked with spaces rather than deleted, as agreed; byte equality was not the aim.
- `CLAUDE.md` and `AGENTS-REFERENCE.md`: the visible text byte-equal; REF's 8 required sections pass, as before.
- The core block has no comment: its view and its rule lines are the same. The real budget is `([], [])`.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `bb2a6e741`'s `sync_adapters.py` | the 2 new tests fail: 8 cases. A fence line inside a real comment passes there too, as it should. The 43 other tests pass in the same run |
| Guard removal (`guard-removal.txt`, `drivers/harness13.py.txt`) | 25 / 25 proven: the earlier guards (G-C1, G-C2, G-C4 on their new anchors in `scan_comments`), and G-H1 (comments deleted before fences again), G-H2 (deleted and joined, not masked), G-H3 (a fence line inside a comment opens a fence), G-H4 (the core view read the old way), G-H5 (the old comment diagnosis) |
| The final run (`checks.txt`) | on **this branch's two frozen source files**: `test_sync_adapters_budget` 45 / 45; `--check` OK, 9 adapters; source identity the same before and after |
| The AI session's real CLI probe (supporting) | the new positives write / check 0 / 0, and `#<!--x--># 2. …` in REF 1 / 1. Some of these copies were over 22000 bytes and warned; Codex's under-budget runs are the evidence |
| Architecture, live model calls | **NOT RUN**; no new live call |

**Two test identities.** The first freeze (`fd440838…`) carried İ5B-FIX-13's test comment, which was no longer true
after this fix. Only that comment was corrected; the AST is equal, and the production code is the same `e5b55f88…`. Red
first, guard removal and the final run were run again on the final test (`985c21d9…`); the first runs gave the same
results and are kept as history.

**İ5B-FIX-13's known false refusal is closed here.** A closed code example holding a literal `<!--` before core:start, and
a `<!--` shown in inline code or escaped, now pass end to end. The core's own rule still refuses raw HTML inside the core,
even in a fenced example (İ5B-FIX-09: a fenced example is free before the core, not inside it). The rules are no longer
hidden by such an example, though.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| The visible reader before this fix (history), 26 real CLI on `7f9c54e9…` | a literal `<!--` in a closed backtick or tilde example, in inline code or escaped: 1 / 1 where 0 / 0 was expected; `#<!--x--># 2. Proje Klasör Yapısı` in REF: 0 / 0 where 1 / 1 was expected (comment deletion made a heading); the normal controls and a code example kept in its section body pass | `4f81e03108d1504326c6b08ce209c4090348d206cbd3f6a2ec77c877a0be4fad` |
| **Final:** the visible reader, 26 real CLI on `e5b55f88…` | all as expected: the false heading 1 / 1; the literal-comment positives in closed code, inline code and escaped 0 / 0; the real hidden-heading and comment negatives and the code example's body kept | `862a2cc06898552d9d568b6794538762419cef97ba4c19671960329603b54fc9` |
| **Final:** the component, 128 real CLI calls on `e5b55f88…` | 128 / 128 as expected: the visible reader 26, the context 36, the metadata / escape 50, title 8, original position 8. The two earlier heading-reader false refusals now 0 / 0; the false REF heading 1 / 1. Every input under 22000 bytes, no warning, the source unchanged | `b541893043e6b221bddd53c6cfca57c2e30877f49ca13e94330824c4838a274c` |
| **Final:** the readers' meaning on the three real files | every section key and non-blank body the same; the 15 headings, the 8 sections, the core rule lines and the required-text decisions the same; `outside_code` and `known_spans` AST the same | `25dac59c86f572d4e46e472995eed9220d0a953110be6dd631961065f42021a0` |
| The test comment binding | the final test differs only by a comment (AST equal); the 128 CLI driver reads the production script and the 17 inputs, not the test file, so it was not rerun | `581f4ed3bb9150527f2b335f13ccdb15d0cb0da736dcb443676e31d8212ed213` |

## Limits

- One small scanner, not a CommonMark parser. An inline code span is read within its line, so a `<!--` after a span
  that crosses lines is a comment (a safe refusal).
- The heading reader keeps its own fence grammar (0–3 spaces), and the core keeps its own (any indentation). They are
  parameters of one scanner and are not merged; merging them would change what a heading is.
- The check proves where a required text and a heading stand in the source; it does not prove that a reader follows the
  rules. P4 and EMP-REPORT-01 stay open. GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
