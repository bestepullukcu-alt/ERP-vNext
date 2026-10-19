# Evidence — İ5B-FIX-21, a close below the top and foreign content (2026-10-18)

Authority: `I5B-FIX-21-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-21-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-21` on `25110ed9cc49d2dfb1b3589d797c2da74264d594` (İ5B-FIX-20), local only. The AI session is the
single writer; Codex (root) measured the real CLI and the AST. As agreed, this is one short record; the drivers and raw
outputs stay outside the repository and are named by SHA-256 below.

## Final sources (SHA-256)

| File | What | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | a closing tag matching an element below the top: `close`: not verified; `FOREIGN_ELEMENTS` (svg, math): `tag`: not verified; the `mask_html` docstring and the type-7 comment made local (comments only) | `492a033076e37a3cd4664ecaa303dea2a53cfac214c62eb679813128b3b24444` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_close_below_the_top_and_foreign_content_are_not_verified` (one new method, with four void / closing cases); in `test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it` four positive input strings balanced (labels and assertions unchanged); nothing else changed | `3c5ca63dd05a575172b13973b79a033da72e2d149380e06141a892d9facdc657` |

## Steps, in order — each kept as it ran

| Step | Sources | Result |
|---|---|---|
| red first: the new method (without the four void / closing cases) on İ5B-FIX-20's code | `25c36d62…` / `5044c255…` | `Ran 1 test · FAILED (failures=13)` — six closes below the top (four scope elements in a block, one in a paragraph, a reopened `<b hidden>`), five foreign-content cases (svg xmp / noembed, math xmp, a paragraph, a closed valid SVG), two AGENTS.md cases; the eight positives green |
| the whole suite on the first code | `28b7305c…` / `5044c255…` | `Ran 57 tests · FAILED (failures=4)` — the four İ5B-FIX-19 positives that relied on an implicit pop (`<p>` / `<p class>` / `<x title>` / `<x>` open above the div). Kept as it was |
| the four inputs balanced (`<p></p>` on the start line, `</x>` on the next line) and the docstring | `c8fafaee…` / `06c34835…` | `Ran 57 tests · OK`; `--check` 9 adapters match |
| the final sources: the type-7 comment and four void / closing cases | `492a0330…` / `3c5ca63d…` | the module's AST equal to `c8fafaee…` (two comment lines); the new method on İ5B-FIX-20's code `FAILED (failures=13)` (the same 13; the four new cases are already a refusal there), on the final code `OK` |

The full suite was not run again on the final sources: the one recorded run of 57 + 9 is planned on the combined
candidate (C20). Codex (root) found the other 55 methods, every helper and the module AST-equal, with the four input
strings as the only change in the earlier method.

## Guard removal — three runs in one restored copy, each kept as it ran

Each guard undone alone; the named test must fail with an assertion and a control test must pass.

| Run | Guards | Result |
|---|---|---|
| 01 | G-S1 a close below the top closes it · G-S2 an svg / math opener read as HTML · G-S3 math not foreign · G-S4 svg not foreign · G-V1 / G-V2 a type-6 / type-7 opening line never starts a block (the four balanced inputs still prove it) · İ5B-FIX-19 G-W5..G-W10 (their test's inputs changed) · İ5B-FIX-17 G-X5' (the closing-match order the new rule meets) | **8 of 13.** Proven: G-S1 (7), G-S2 (5), G-S3 (1), G-S4 (4), G-V1 (2), G-V2 (2), G-W7 (1), G-W10 (1). **NOT PROVEN: G-W5, G-W6, G-W8, G-W9, G-X5'** |
| 02 | G-X5' only, bound to the new method as well (its old negative is now refused by the close rule) | 1 of 1 (failures=3) |
| 03 | G-W5, G-W6, G-W8, G-W9 only, after the four void / closing cases were added to the new method | 4 of 4 (1, 1, 3, 1) |

Why run 01 did not prove those four: their old cases open a non-void element (`<p`, `<x`), so a mutant's wrong block
start now ends in a new not-verified result (İ5B-FIX-20's opening name, or the close below the top) instead of a pass.
A void element or a closing tag stacks nothing, so the four new cases show the wrong start again. The current source
had no false pass there; the gap was the test's sensitivity to a future change. G-W9 was probably unproven since
İ5B-FIX-20 already; that was not measured. The runs are not added up into one number.

## Real CLI — Codex (root), not run by the writer

Red on C19: the two first C19 reports and Codex's root records (scope boundaries 8 inputs / 16 calls; foreign content 6
inputs / 12 calls). A top-of-stack design mutant still passed the three SVG cases — two REF, one AGENTS.md (12 calls; its first try
failed with a SyntaxError and counts for nothing). Green: Codex's component record on `c8fafaee…` (16 scenarios / 32
calls, all as expected — the closed valid SVG and MathML refused as declared, an SVG inside an HTML `xmp` passes). The
final source differs from `c8fafaee…` in two comment lines only (module AST equal), so those calls were not repeated and
are not counted as calls on `492a0330…`. None of this is a run on a combined candidate or the full tree.

## Raw records (outside the repository, SHA-256)

| Record | What | SHA-256 |
|---|---|---|
| `red-01.log` | red first | `f19c0afe5b61b8d3562717a44d508ad1361e01fe42ed66ffc7c009ce1d768250` |
| `full-01.log` | 57 tests, 4 failures (kept) | `3e74dcdfa42353c0924ee3b2afa72cd7fe5387afcdb94341f09177db638c2c88` |
| `fixture-02.diff` | the four balanced inputs | `04a8f16252d942e7652f1dc3eefb34b881c6472b281df0be1f2bbf1a8b07b957` |
| `full-02.log` | 57 tests OK | `17bc08e345c2c137c0dcf820b3ac94b33badda30bc1426376e4f000025830ce4` |
| `newmethod-parent-01.log` / `newmethod-final-01.log` | the new method on İ5B-FIX-20's code / the final code | `7d430dab865186db653fc31ed7cdbf4953c165faa716436806724f1aebab5b8a` / `dea2039c964ccfa502feb2cb94658611af95aacb718e1adc8688dbd2fc0d8b1f` |
| `static-parse-01.txt` | one static html5lib parse of the structures (no browser) | `03d85fa0944e2fc37e03656d519af7b9a50064db57546c50473173c44793f939` |
| `guard/harness21.py` · `guard/i5b21-guard-01.json` · `.log` | run 01 | `b33e39d62e43f45061addf1923c57b14e32db8ff280807d5a638be5a94939465` · `0eaf0d9007c68ef0effb57223a41972e7a2ce8e09eb5feda17845d7afb354f4b` · `7b76c05b4f8d941950359bf568d8d56b090d8713c7aa3631634c028447c19606` |
| `guard/harness21b.py` · `guard/i5b21-guard-02.json` · `.log` | run 02 | `f7dc1ae9188f6b5aed6258a836c448076bbcd5d236ee9cb0ec7bf25b345e8b70` · `caebbf7ff09c88a86ca4dfcf6e45ce62d014cf5a3c38b99f08729b34b12f91d6` · `7451c9734c1963e5966747c4b63e52cda252392b3ac48809cb532be30c20e408` |
| `guard3/harness21c.py` · `guard3/i5b21-guard-03.json` · `.log` | run 03 | `dc167cba925d9c0fe7e0f3ba5d48354fce2cc6ace0353550ff59de66f5cefe96` · `b8ee4168316d522ed909e1beb5d703e0b969ca0405e6f73ad43bfedcd70d7dd8` · `0a7ecaa156d96213c91f0d84c74ecb6ad52755542cd9888d120c2c5323522b5c` |
| Codex C19 / v18 first report | 77, UYGUN DEĞİL (kept unchanged) | `dc789ff991b336d159f2c9429278b7150020fcbc23cd3d49263fc175154f6282` |
| Claude C19 / v18 first report | 73, UYGUN DEĞİL (kept unchanged) | `67ddf01985c824a8629f5cb998ce52a20b68a4144ad0eedabff2a959235c61d7` |
| `C19-own-scope-boundaries-CLI-01.json` | Codex root, red on C19 | `8a46753e67dac26f82f8bc327bb29ed6d518938c16868b18356228c4c1c7777a` |
| `C19-own-foreign-and-top-only-CLI-01.json` | Codex root, red on C19 (its design part invalid, kept) | `b2be5652a51e81e31bee7beeaf943c90b83b45c18f4026e33e359df57e82a04d` |
| `C19-own-top-only-design-CLI-02.json` | Codex root, the top-only design mutant | `a9ce9481c0cfc3184d5fcfc3f51f7c886914b6b606e6924a1f8ef52b24dadf70` |
| `C19-tree-context-spec-basis-01.txt` | Codex root, the specification reading | `e7b703d390d7be30c410d607662d72e718fcd3b044a38092b8fd7da5571ea453` |
| `FIX21-own-two-families-component-01.json` | Codex root, green on `c8fafaee…` | `9f1deecfd78d46c5cca0d8bdfdc17f07fedf0420a05608cc58ccc2d8549234ce` |
| `FIX21-old-tests-four-fixture-AST-02.json` | Codex root, the earlier tests AST-equal but the four strings | `3d9eb1cd25c95a2acc4328b8777dab911245de425b9ac92c6cb831c7d0ba7355` |
| `FIX21-final-comments-and-four-cases-review-03.json` | Codex root, the final comment-only and four-case difference | `7242ec2db5857f6bd5be16c19944eb582fec2ff56a2a93efc763c3ffbbdd6bac` |

The one recorded run on the combined candidate (C20) is planned after the commit; it is not a result yet.
