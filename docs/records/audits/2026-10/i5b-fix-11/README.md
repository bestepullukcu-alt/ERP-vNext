# Evidence — İ5B-FIX-11, one supported shape for the rule list; a reference image proves no required text (2026-10-18)

Authority: `I5B-FIX-11-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-11-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-11` on `214c12db504df20b478405b119eb33ddcbf76f0b` (İ5B-FIX-10), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `core_shape_problem`: at the real line positions of the core block (before the comment and fence views, which drop lines), every numbered rule line is in one consecutive block numbered exactly 1..N, after a blank line; otherwise `CORE-SHAPE … — no required text is verified`. `unreadable_link`: a known image is masked whole, and of a known link only its `(destination "title")`, so its visible label stays read; an unescaped `![` or `](` left over makes the line "not verified" (`UNESCAPED_LINK_OPENER`) | `4326a1d9e72e96f172f91e6234b339f62cc9c3ed6829e6d445246a80327de333` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_the_rule_list_has_one_supported_shape`, `test_a_reference_image_proves_no_required_text`; no earlier test changed | `2f918602cb60739c96f3f9fab692b052ed4a30086280e44b1935638920299680` |
| `docs/records/decisions/2026-10/i5b-fix-11-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json`, the generated adapters and the other 17 instruction inputs are not changed. The
real core passes the shape: rules 1..11 at core-block lines 4..14 (0-based), a blank line before rule 1, no `![` and no `](`.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file (the whole suite) against `214c12db5`'s `sync_adapters.py` | the 2 new tests fail: 10 shape cases, 5 reference-image cases, and 1 escaped-link-tail control that İ5B-FIX-10 refused. The 38 other tests pass in the same run |
| Guard removal (`guard-removal.txt`, `drivers/harness9.py.txt`) | 14 / 14 proven on a copy equal to the final sources (a run on the first freeze was 14 / 14 too and is not counted): İ5B-FIX-10's 7 guards again, and this package's 7 (the shape check; the gap, blank-line and 1..N parts of it; the image opener; the kept link label; the escape parity) |
| The final run (`checks.txt`) | on **this branch's two frozen source files** in the İ5B worktree: `test_sync_adapters_budget` 40 / 40; `--check` OK, 9 adapters; source identity the same before and after |
| Architecture, live model calls | **NOT RUN**; no new live call |

**Two freezes.** The first freeze (`fb65dbfd…` / `1a49310b…`) was narrowed before the commit, at Codex's request: the
docstring and one test label now say "a continuation line between two numbered rules" (a line after the last rule is
outside the block and is not judged), and one passing control pins that. An AST comparison shows `sync_adapters.py`'s
code, docstrings aside, equal between the two freezes. Every run above is on the final freeze.

**Kinds of evidence, kept apart.** Everything here is unit / API level (`check_instruction_budget`). The real
`write` / `--check` CLI counter-examples are Codex's (below), and so is the render support. The AI session's own CLI probes
during triage are history: some of its copies were over 22000 bytes and warned, and they are not presented as
under-budget evidence.

**What the final run is and is not.** It is a run on this branch's two frozen files, not a measurement of a complete
final Git tree and not a combined-candidate run. C9's 185 / 185 is not a test of this source.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| Reference images, on İ5B-FIX-10's final source (history: before this fix) | the rule only in a reference image's alt text (`![…][lang]`, the definition apart) passed write / check 0 / 0, 21981 bytes; the collapsed form 0 / 0 too, but over 22000 bytes with a warning | `8512a0cbbd314695388324b4ac3afc1e60c96435b4d8a27acb0f23c7d70cf2c9` |
| Multi-line titles, 12 real CLI (history: before this fix) | a reference or inline title opened before rule 4, directly or through a four-space bridge line: 0 / 0 where 1 / 1 was expected; the normal controls 0 / 0; all under 22000 bytes, no warning | `9d875054bd61834494838f32f4a0d8e483eff90a869c3c2c1c91d113efe094d0` |
| Local render support (not a CommonMark conformance proof) | in the four title cases rule 4 is not visible; in the two controls it is; the six inputs byte-equal to the CLI ones | `67029f8ebaf06a91b33c132efb2b714eb05f50d2e233cfec9c88b760b2896e7d` |
| **Final contract**, 25 inputs × write / check on the final source `4326a1d9…` / `2f918602…` | 50 / 50 as expected; every file under 22000 bytes, no warning, the source unchanged; the 17 instruction inputs equal to the earlier copy | `e36604f6e1941eb1eafc9d54ae9ab5c47926b650bc3df2c9308e72733b5dff6b` |
| **Final:** İ5B-11 source snapshot 02 (`4326a1d9…` / `2f918602…`) | the two final files as frozen; the source the same before and after | `3fcb68d9b0ed90e8e6563a17c30cfaacf5015555ccd11956b1505c245c9fd33e` |
| The same contract on the first freeze `fb65dbfd…` / `1a49310b…` (history: an earlier document identity) | 50 / 50 as expected. Its `-01` file, kept, had two apparent failures from a driver mistake (one backslash where two were meant); `-02` corrects only that input | `3b2c1bd11e9f65abb57997020591f3edededba9b77fee19ccecedbd009d1e807` |
| Codex's record of its own driver mistake (not a production defect; the old result and driver kept) | the `-01` input held one backslash, not two | `9f453d54f0c2405e6a680a03a358159f919748bf2c584c4d3115065f4dd06e29` |
| A reference image inside a visible link label, 8 real CLI (history: before this fix) | `4. [X ![…][lang]](AGENTS.md)`, the definition after the core: 0 / 0; masking a known link whole would lose this opener | `dab779564727ebbc8fed1f6556247ca5082ed218883ad5b47cdd5efce55983f4` |

## Limits

- One supported shape, not a CommonMark parser. Refused although visible, and declared: a loose list (a blank line
  between rules), a continuation line between two numbered rules, a second numbered list inside the core, and an
  undefined literal `![x]` on a rule line. A line after the last rule is outside the block and is not judged by the shape.
- An escaped `\![` or `\](` (an odd number of backslashes) is text and stays allowed. İ5B-FIX-10 refused an escaped `\](`;
  this is a declared widening.
- The check proves where a required text stands in the source. It does not prove that a reader follows the rule; P4 and
  EMP-REPORT-01 stay open.
- GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
