# Evidence — İ5B-FIX-12, the core's shape on physical source lines; one range rule for links and images (2026-10-18)

Authority: `I5B-FIX-12-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-12-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-12` on `47abe0c64e89a07f68c9f885639ba1bfe9cc56b3` (İ5B-FIX-11), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `core_shape_problem` now reads the whole source and measures the numbered rule lines between the physical marker lines. The physical line before rule 1 must be really blank, and the marker line is not. `known_spans` is the one range rule for the links and images the check knows: an image only when its `!` has an even number of backslashes before it (none included); otherwise a link whose destination and title alone are hidden. `link_metadata` and `unreadable_link` both use it | `c5a88c38c5c7a9a674745fe05928cf7fdae1cdc08f789959f550b357df200621` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_the_core_markers_keep_their_physical_context`, `test_an_escaped_bang_before_a_link_is_text`; no earlier test changed | `15c3a8167368eebddf92bdd09fce52789107fb7f6e57969d5123c7fae843cebc` |
| `docs/records/decisions/2026-10/i5b-fix-12-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json`, the generated adapters and the other instruction inputs are not changed. In the real
file the marker is alone on physical line 20 (1-based), and rule 1 is on line 25 after a real blank line; the shape
passes.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file (the whole suite) against `47abe0c64`'s `sync_adapters.py` | the 2 new tests fail: 4 marker-boundary cases and 2 escaped-bang cases. Their controls and the 40 other tests pass in the same run |
| Guard removal (`guard-removal.txt`, `drivers/harness10.py.txt`) | 16 / 16 proven on a copy equal to the final sources: İ5B-FIX-10's and İ5B-FIX-11's guards again (four on new anchors), and this package's G-P1 (the line before rule 1 not judged when it is the marker line — the old block view), G-P2 (no blank physical line needed), G-B1 (an escaped bang read as an image) |
| The final run (`checks.txt`) | on **this branch's two frozen source files** in the İ5B worktree: `test_sync_adapters_budget` 42 / 42; `--check` OK, 9 adapters; source identity the same before and after |
| Architecture, live model calls | **NOT RUN**; no new live call |

**Kinds of evidence, kept apart.** Everything here is unit / API level (`check_instruction_budget`). The real
`write` / `--check` CLI counter-examples and the local render support are Codex's (below). The AI session reproduced the
two findings with the real CLI in its own copy before the fix; some of those copies were over 22000 bytes and warned, and
they are not presented as under-budget evidence.

**What the final run is and is not.** It is a run on this branch's two frozen files, not a measurement of a complete
final Git tree and not a combined-candidate run. C9's 185 / 185 and C10's narrow 40 / 40 are not tests of this source.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| C10 triage (history: before this fix) | core:start inside a reference title on the same line, rule 1 right after: write / check 0 / 0 where 1 / 1 was expected; an escaped bang before a visible inline link: 1 / 1 where 0 / 0 was expected; the normal file 0 / 0 before and after | `7885e1a4ee0131a69079d83a5203f836fbb3f11b763bea132c4293694fa5d472` |
| Its CLI evidence for it | the boundary and escaped-link inputs, all under 22000 bytes | `9cc830346ba4f2bf8c9d62cb0efaf93a1a9a5d063ff1a6244f19c0a9aec384f4` |
| Local render support (not a CommonMark conformance proof) | rule 1 and core:start inside the reference title; the rule's label absent from the rendered text | `bd76b581996aac19976b3ddee81711a92394b7b7a3cb8a4e48a272b66df8c3a3` |
| The objection to "markers on lines of their own" alone (history) | a standalone core:start after a title opener still hid rule 1 (0 / 0); the visible rules after a real blank line stay 0 / 0 | `3ab1b3b5430d61bf8f86ad2be78abd2e3c86a3316a99aa93d8ea3449ae084417` (triage EK-02), `b108a589d00e12c6a914ab499e309dcfb6878631ace044d6756584be3805143b` (its CLI) |

## Limits

- One supported shape, not a CommonMark parser. The İ5B-FIX-11 declared false refusals stay. The check proves where a
  required text stands in the source; it does not prove that a reader follows the rule. P4 and EMP-REPORT-01 stay open.
- Claude Denetim001's CT findings (C10-G1, C10-G2, C10-G6) are not in this package. They are measured apart and decided
  apart.
- GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
