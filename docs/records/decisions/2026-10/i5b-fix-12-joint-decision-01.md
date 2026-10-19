---
decision_id: I5B-FIX-12-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 33707 (both first C10 reports saved; the boundary finding C10-F1 and the escaped-bang false refusal C10-F2 confirmed by Codex's root; "markers on lines of their own" alone is not enough; a smallest FIX-12 that keeps the physical source context; one shared escape / metadata rule), 33766 (the joint decision: the physical-line approach and one shared range rule; sync_adapters.py and its budget test only; red first, three named mutations, the final identities and the component run; CT-G1 / G2 / G6 not in this package)
scope: İ5B-FIX-12 — after the C10 audits, two findings in the core check of sync_adapters.py: the rule list's shape measured on extract_core's block lost the physical context of the marker line, and an escaped bang before a visible link was refused as an image. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters and the other instruction inputs are not changed.
relates_to: I5B-FIX-11-JOINT-DECISION-01; I5B-FIX-10-JOINT-DECISION-01; the first C10 / v11 reports of Codex Denetim001 and Claude Denetim001 (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-12 — the core's shape on physical source lines; one range rule for links and images

## Why

- **C10-F1 (critical criterion 02).** İ5B-FIX-11 measured the rule list on `extract_core`'s block, which strips the
  block's edges. When core:start sat inside a title opened before it (`[hidden]: AGENTS.md "<!-- core:start -->`), or a
  standalone core:start came right after such an opener, rule 1 became the block's first line. It was inside the title
  and invisible, and the "blank line before rule 1" check was never reached. Write / check gave 0 / 0. The same was in
  C9; it is not a C10 regression.
- **C10-F2.** `\![rule](AGENTS.md)` is a visible link after a literal `!`. The two readers took the `!` as an image
  and hid the label, which was a false refusal (1 / 1).

## What was decided

| Item | Before (`47abe0c64`) | Now |
|---|---|---|
| Where the shape is measured | `extract_core`'s block, its edges stripped | the source's physical lines. The numbered rule lines between the physical marker lines are one consecutive block 1..N, and **the physical line before rule 1 is really blank** (the marker line is not). core:start and rule 1 on one line leaves 2..N and fails the numbering |
| Links and images | each reader decided on its own whether a match was an image | one rule, `known_spans`, used by `link_metadata` and `unreadable_link`. An image only after an even number of backslashes (none included). Otherwise it is a link: the `!` is text, and only the destination and title are hidden |
| Not changed | — | the visible link label, a visible reference link label, short code terms, escaped openers, autolinks, the İ5B-FIX-11 declared false refusals, CORE-HTML, the byte budget, AGENTS.md and the adapters |

## Known consequences (measured)

- The real file passes: core:start alone on physical line 20 (1-based), a real blank line, rule 1 on line 25. `--check`
  reports 9 adapters in sync. No earlier test changed; İ5B-FIX-11's shape test still passes. The message "no blank line
  before rule 1" is now "the physical line before rule 1 is not blank".
- Four guard mutations of earlier packages moved to new anchors (the shared range rule). İ5B-FIX-11's G-S3 is now
  G-P2.

## Limits kept open

- One supported shape, not a CommonMark parser; the local render support is not a conformance proof.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.
- Claude Denetim001's CT findings (C10-G1, C10-G2, C10-G6) are measured and decided apart; none is in this package.

## Not in this decision

- A Markdown engine or a dependency, a rule that markers stand alone (not needed with the physical-line check), a
  change to AGENTS.md or the adapters, a live model call, a full test run of a new candidate.
