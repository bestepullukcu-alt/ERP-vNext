# Evidence — İ5B-FIX-22, a masked range HTML ends earlier (2026-10-18)

Authority: `I5B-FIX-22-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-22-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-22` on `ca0525998bbfb1af62f8d3b4444d38420ddfdedf` (İ5B-FIX-21), local only. The AI session is the
single writer; Codex (root) measured the real CLI and the AST. As agreed, this is one short record; the drivers and raw
outputs stay outside the repository and are named by SHA-256 below.

## Final sources (SHA-256)

| File | What | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `comment_end_unread` (`<!-->`, `<!--->`, `--!>`) used by scan_comments' three comment readers; `mask_html` takes the comment spans (optional parameter, passed by `visible_text` and `unreadable_html`): a comment inside a raw-text element or inside `<?` / `<!X` / `<![CDATA[` is `code`: not verified; a `>` before the reader's end of `<?` / `<![CDATA[` is `tag`: not verified | `d3f9cc7b45e4039066e87061d74d95f92cdf77dee268ca07bcf2cc52a58b3d61` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_masked_range_html_ends_earlier_is_not_verified` (one new method); no earlier test or helper changed | `8acc3c364a2cd825c27e4cdb140c0c15156d9579a98b98dd47d47a599da6ba33` |

## Steps, in order — each kept as it ran

| Step | Sources | Result |
|---|---|---|
| before the code: the reader of C20 and an installed html5lib, in memory (not the CLI) | `492a0330…` | four more variants of the same root: the reader counts the section, html5lib puts the heading in the hidden div (a comment inside `<style>` / `<script>`, `<? x > … ?>`, `<![CDATA[ x > … ]]>`); a standard comment control is clean. A second probe: a comment inside `<? … ?>` and `<!X … >` too; a comment inside a quoted attribute value was already refused |
| red first: the new method on C20's code | `492a0330…` / `21798340…` | `Ran 1 test · FAILED (failures=16)` — the six `--!>` / `<!-->` / `<!--->` cases (block, paragraph), three comments inside raw-text elements, `<?` / `<![CDATA[` with an earlier `>` (block, paragraph), two declared cases with nothing open, three AGENTS.md cases; the six positives green |
| red first again, after two cases were added (a comment inside `<? … ?>` and inside `<!X … >`), in the one restored copy | `492a0330…` / `8acc3c36…` | `FAILED (failures=18)` |
| the final sources | `d3f9cc7b…` / `8acc3c36…` | `Ran 58 tests · OK`; `--check` 9 adapters match |

Codex (root) found the 57 earlier tests, every helper and the module AST-equal to C20 once the one new method is taken
out.

## Guard removal — one run in one restored copy

Each guard undone alone; the named test must fail with an assertion and a control test must pass. **13 of 14.**

| Guard | Result |
|---|---|
| G-C1 the helper always false · G-C2 `<!-->` / `<!--->` not checked · G-C3 `--!>` not checked | proven (9, 4, 5) |
| G-C4 / G-C5 / G-C6 the comment in an HTML block / the block comment / the inline comment not checked | proven (6, 1, 2) |
| G-C7 a comment inside a raw-text element counts · G-C8 a comment inside `<?` / `<!X` / `<![CDATA[` counts | proven (4, 1) |
| G-C9 `<?` / `<![CDATA[` end at the reader's marker after a `>` | proven (3) |
| G-C10 / G-C11 the visible-text / the message reader gets no comment spans | proven (5, 4) |
| **G-C12 a raw-text element's text taken from the file start (`raw_from` not set)** | **NOT PROVEN** — this mutant can only refuse more (a comment anywhere before a raw-text element); no test has one. Kept as it ran; no test was added |
| İ5B-FIX-16 G-H1' and G-H1'' on their moved anchor (`visible_text` now keeps the comment spans) | proven (6, 6) |

The earlier guards untouched by this change were not run again.

## Real CLI — Codex (root), not run by the writer

Red on C20: Claude's first report and Codex's root records (comment ends 8 scenarios / 16 calls; raw-text and
processing / CDATA ends 6 scenarios / 12 calls). Green on `d3f9cc7b…` / `8acc3c36…`: Codex's component record — the ten
earlier false passes 1 / 1, the controls 0 / 0. A component measurement bound to the two SHA-256 above, not a run on a
combined candidate or the full tree.

## Raw records (outside the repository, SHA-256)

| Record | What | SHA-256 |
|---|---|---|
| `probe-c20-01.txt` | C20 reader in memory + html5lib, four more variants | `f1a7f38c18d882c1addf9311dd277b293ecc82446ee0cde8aae87211d2e4bf67` |
| `probe-c20-02.txt` | the same, comments inside a tag, `<? … ?>`, `<!X … >` | `3caea5bab99f338700c3d69d93f888fb707d32afa4ac844669fc7c3feb4be7e2` |
| `red-01.log` | red first (16) | `63d07a05fd2eaa70724eec357afc547275a73c2d48411bc99cebf59fbc3c7778` |
| `red-02.log` | red first on the final test | `655ca316abf5b222de3d232d863a79be3c896fb2ff5627fdca8b6ad79d0e14aa` |
| `full-01.log` | 58 tests on the final sources | `408fa2fa5c0c5273bd7354cf9578278cd9056977e96b649babdde5423ac9e659` |
| `guard/harness22.py` · `guard/i5b22-guard-01.json` · `.log` | guard run | `a2577d898a650c6fe9276e4a7b9cf2dbce9e98a0096df3c609b2846f4fd9c217` · `427337df8cddf77bba84077cd5202c787416cf8572e08a84c7d8a0b54e74dfc6` · `95fb620ddd2b0db402cc9b8dcc7dad1209caef0d43def26fd5692cf7db9de4ee` |
| Codex C20 / v19 first report | 79, UYGUN (kept unchanged) | `6b1ec66ee940c221e4648635ceaf86471207c94077acadee1a6a31b8b4a77ef1` |
| Codex C20 result-impact addendum | 02 KALDI, UYGUN DEĞİL 19 / 1 / 0, 78 (the first stays 79) | `df21de3ba6685dc0f32d54f3120c7e497d94a487626b1c4e321b152c5d035e59` |
| Claude C20 / v19 first report | 73, UYGUN DEĞİL (kept unchanged) | `f02ce25e10eb148b01d6ead73bc800d1a777cbc72131a8af6b78f878adcb8311` |
| `C20-own-comment-boundaries-CLI-01.json` | Codex root, red on C20 | `fc48753a86c82ae02c7d85d3eb8b961e09cd85eea788845bf1ac94394560e9d1` |
| `C20-own-raw-other-boundaries-CLI-01.json` | Codex root, red on C20 | `8e898bf1017b53df0974d2d54b0a39276c047123e9d2711d4b79e67041528ae8` |
| `C20-comment-spec-basis-01.txt` · `C20-raw-other-spec-basis-01.txt` | Codex root, the specification reading | `3eec2e3ebe54fce00f67f99717720c03a6de808e39399dfe53a2e2c381989102` · `dccbed0fb5e954d2c03d5291469913bd59ed0c37e8f494880aebdfbcb8d0e225` |
| `FIX22-own-masking-boundaries-component-01.json` | Codex root, green on the final sources, and the AST | `3161ac5efefc61dea3cab1d063fc150428c801d29826105563f19509c530a4df` |

The one recorded run on the combined candidate (C21) is planned after the commit; it is not a result yet.
