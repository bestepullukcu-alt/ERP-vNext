# Evidence — İ5B-FIX-23, a comment inside a tag (2026-10-18)

Authority: `I5B-FIX-23-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-23-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-23` on `12c4080c8904c05f5efe8362be7608cd16e206b6` (İ5B-FIX-22), local only. The AI session is the
single writer; Codex (root) measured the real CLI and tried the rule as a design outside the repository. As agreed, this
is one short record; the drivers and raw outputs stay outside the repository and are named by SHA-256 below.

## Final sources (SHA-256)

| File | What | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | in `mask_html`, right after a tag's paragraph / block limit: a comment span starting inside the tag is `tag`: not verified (three lines and a docstring bullet) | `bec0a238e873fca2b08575d7e345808d49295594e4083e0a4dd27be19c841362` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_comment_inside_a_tag_is_not_verified` (one new method); no earlier test or helper changed | `5b09dbb51580ef9dd7d4fe2c47b690182bb02388aed02bac21c538fe4e278ce9` |

## Steps, in order

| Step | Sources | Result |
|---|---|---|
| red first: the new method on C21's code | `d3f9cc7b…` / `5b09dbb5…` | `Ran 1 test · FAILED (failures=11)` — double / single / unquoted values, `<template>`, a paragraph, Codex's two `<span>` forms, a closing tag that ends at its first `>`, the declared literal comment, two AGENTS.md cases; the five positives green |
| the final sources | `bec0a238…` / `5b09dbb5…` | `Ran 59 tests · OK`; `--check` 9 adapters match |

The closing-tag case (`</span <!-- > <table> -->>`) was added by the writer; it was a false pass on C21 too.

## Guard removal — one run in one restored copy

Each guard undone alone; the named test must fail with an assertion and a control test must pass. **3 of 3.**

| Guard | Result |
|---|---|
| G-A1 a comment inside a tag not checked | proven (11) |
| G-A2 a comment right after a tag counted as inside it | proven (1) |
| G-A3 only an opening tag checked | proven (1) |

No earlier guard anchor moved (135 checked against C21's source); the earlier guards were not run again.

## Real CLI — Codex (root), not run by the writer

Red on C21: the two first reports and Codex's root record (5 scenarios / 10 calls: double / single quote REF and AGENTS
0 / 0, normal and code 0 / 0). Codex's design trial of the same three lines outside the repository (9 scenarios / 18
calls, as expected, the literal-attribute case refused as declared) is a design record, not a run on these sources.

## Raw records (outside the repository, SHA-256)

| Record | What | SHA-256 |
|---|---|---|
| `red-01.log` | red first | `24fb2de9a8146ba98232fc661fd6fc1e8e931235ffdf78bc4d16fbcf1ef7a3f2` |
| `full-01.log` | 59 tests on the final sources | `d5da142e35620620d4cdc029d9cccd83822864d41d395789dd18cafc2e984e8f` |
| `guard/harness23.py` · `guard/i5b23-guard-01.json` · `.log` | guard run | `78dc378f6fe70f17a428ced1df99eed0428673059ea947cea56b97fcee231a15` · `2a53d8f3a438040e8afc9e90e32b672c93d5212f7b2bd4ca16d77d337ce9eb7f` · `a5186c1b1b30165c5b3901bb93905b160f38873d13c35010461a3482d1d7f131` |
| Codex C21 / v20 first report | 78, UYGUN DEĞİL (kept unchanged) | `226f8fba94f3f5a90d05a6626937fb72bf7eae6741056f2e05ddf06ce5d34083` |
| Claude C21 / v20 first report | 72, UYGUN DEĞİL (kept unchanged) | `b05d27b30eecccb4818f40ac1c5ed08029d42fef296656b36d331e0e8e2b379c` |
| `C21-own-attribute-comments-CLI-01.json` | Codex root, red on C21 | `6a01999ea748fcff31b68f2887d919fa0e4e1db17db411dbb47e70eb77eed10d` |
| `C21-attribute-spec-basis-01.txt` | Codex root, the specification reading | `f3b0cd12f9fb34f07a4f10f0724588d27c1a43fa008c7f859c2c4478eb0a1798` |
| `C21-tag-comment-overlap-DESIGN.py` · `C21-tag-comment-overlap-DESIGN-CLI-01.json` | Codex root, the design trial | `89ec43322e96fe9a16e7200c4f7c46bf9772e507f9b93f3185b1ea85c9da5300` · `62142793f56594ab2915e7fa6afa1402442a9f2b62517d14a6854aa1f1d2291a` |

Codex (root) is to bind its own 9 / 18 set to the final sources; the one recorded run on the combined candidate (C22)
is planned after the commit. Neither is a result yet.
