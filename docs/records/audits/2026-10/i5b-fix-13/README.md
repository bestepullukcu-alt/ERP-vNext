# Evidence — İ5B-FIX-13, the context at core:start read on the whole source (2026-10-18)

Authority: `I5B-FIX-13-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-13-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-13` on `98bf161b8d700650524d45865540c3ca15d52d04` (İ5B-FIX-12), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `fence_opener` / `fence_closes`: `outside_fenced_code`'s fence grammar, shared, with its behaviour unchanged. `open_context`: one small scanner on the source as it stands (nothing deleted, no pieces joined). It tracks a fenced code block, an HTML comment and inline code read within its line; inside a fence `<!--` is literal, inside a comment a fence line is not a fence, and inline code holds neither. `core_context_problem`: core:start alone on its physical line (at most three spaces before it), and nothing open at the end of that line, or `CORE-CONTEXT … — no required text is verified` | `7f9c54e9a51813609d266371b5fd0749a2308833c44304c042af9ec601aca920` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_the_core_start_marker_has_no_open_context`; no earlier test changed | `3b63f54403655263aceff8c372346c518ef6bfe321f994eb66ce53947d1ed3b6` |
| `docs/records/decisions/2026-10/i5b-fix-13-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json`, the generated adapters and the other instruction inputs are not changed.
`outside_code` (the import check) keeps its own policy for an unclosed fence. `known_spans` stays an inline-only rule.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `98bf161b8`'s `sync_adapters.py` | the new test fails: 5 fence cases and 2 declared safe refusals pass there. The 42 other tests pass in the same run |
| Guard removal (`guard-removal.txt`, `drivers/harness12.py.txt`) | 20 / 20 proven: the guards of İ5B-FIX-10, -11 and -12 unchanged, and G-C1 (a fence open at core:start not reported), G-C2 (an HTML comment not tracked), G-C3 (the marker not required alone), G-C4 (inline code not read) |
| The final run (`checks.txt`) | on **this branch's two frozen source files**: `test_sync_adapters_budget` 43 / 43; `--check` OK, 9 adapters; source identity the same before and after |
| Architecture, live model calls | **NOT RUN**; no new live call |

**Helper-level positives and end-to-end results, kept apart.** The test's positives say only that CORE-CONTEXT is not
raised. One of them — a closed code example before core:start holding a literal `<!--` — is still refused end to end
(write / check 1): the global heading reader (`visible_text`, then `level2_sections`) removes HTML comments before it
reads fences, so the comment runs to the marker's `-->` and takes the closing fence with it. That is an existing false
refusal of another reader, measured by Codex with the real CLI, kept open and not hidden here. This package does not
claim that every positive passes end to end.

**The comment contract kept.** An HTML comment opened before core:start ends at the marker's own `-->`; the rules after it
stay visible (İ5B-FIX-04's test). The context is read at the end of the marker's line, so such a comment is not refused,
while a fence opened before the marker is still open there and is.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| The outer fence boundary on İ5B-FIX-12's final source (history: before this fix) | a backtick or a tilde fence opened right before core:start and closed after core:end: write / check 0 / 0 where 1 / 1 was expected; the normal file 0 / 0 before and after; every input under 22000 bytes | `a9185d8a5c0b5e3ed75e5a42faa9b87cdc22d315cd000fa2d526b7a45f302d88` |
| The context cases on the same source (history: before this fix) | 30 real CLI; the false passes of the fence forms confirmed. Its "comment opened before the marker" case is not a hiding context (the comment ends at the marker's `-->`), and Codex re-classified it. It also found the separate heading-reader false refusal described above | `475a9c7eda27097493a669ac31154c7df8c73314ac2400e278b0230c9afaaef6` |
| **Final:** the component on the frozen source `7f9c54e9…` / `3b63f544…` (the 17 instruction inputs unchanged) | 102 real CLI calls — the new context 36, the earlier metadata / escape 50, title 8, original position 8: **100 as expected and 2 the existing heading-reader false refusal** (a literal `<!--` inside a closed code example: write / check 1 where 0 was expected). Not "102 / 102 green". The four real outer-fence forms 1 / 1; a comment ended at the marker 0 / 0; the marker after three spaces 0 / 0, after four spaces or a tab 1 / 1. Every input under 22000 bytes, no warning, the sources unchanged; `outside_code`'s AST unchanged | `acb2809c8b3397f11ce854ce3b9c99ba6e8b9b330cad1b815ac24f7bd56bfe78` |
| Codex's classification correction (kept with the earlier file unchanged) | its earlier "open comment before the marker" expectation was wrong for İ5B-FIX-04's contract and is not counted as a real false pass | `c3f4a1ce1843fe1c85fadb196b523babce9bd58da9e37bca48830a34d5adb215` |

## Limits

- One small scanner, not a CommonMark parser. An inline code span is read within its line, so a `<!--` after a code span
  that crosses lines may be refused (safe). A text in front of core:start on its line and a marker indented four or more
  spaces are refused (declared safe refusals; the real file has the marker alone).
- The heading reader's comment-before-fence order is an existing, separate false refusal; a change to it would be its
  own decision.
- The check proves where a required text stands in the source; it does not prove that a reader follows the rule. P4 and
  EMP-REPORT-01 stay open. GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
