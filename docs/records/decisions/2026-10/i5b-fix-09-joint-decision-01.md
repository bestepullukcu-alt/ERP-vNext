---
decision_id: I5B-FIX-09-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 31125 (item 1 of the v8 / C7 fix proposal agreed in a narrow local scope: the CommonMark raw-HTML opener class; the short-term exception for one whitespace-free token only), 31247 (Codex's 34 / 34 interim CLI checks on the writer source), 31692 (no new live call: the instruction inputs are byte-equal to C7's)
scope: İ5B-FIX-09 — the raw-HTML check of sync_adapters.py's instruction budget and the short-term exception of on_rule_line, with their tests. AGENTS.md, the core, adapters.json and the generated adapters are not changed.
relates_to: CT-FIX-08-JOINT-DECISION-01 (items 2–4, a separate branch); I5B-FIX-08-JOINT-DECISION-01; I5B-FIX-07-JOINT-DECISION-01; the first v8 / C7 audit reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-09 — the raw-HTML opener class and the one-token short-term exception

## Why

The v8 / C7 audits found two gaps in the core check:

- The raw-HTML check matched complete tags only. A `<![CDATA[`, a `<?` processing instruction, a `<!X` declaration or an
  open tag without `>` before `core:start` or on a rule line opened an HTML block that hid the core rules, and the check
  passed.
- The short-term exception let a required text count when it was an inline code span of its own. A natural-language
  phrase written as one span was shown as code, not stated as a rule, and still counted.

## What was decided

| Item | Before (`f829ac5fd`) | Now |
|---|---|---|
| Raw HTML | `RAW_HTML_TAG`: complete tags | `RAW_HTML_OPENER`: the CommonMark opener class. `<letter`, `</letter`, `<!--`, `<!LETTER`, `<![CDATA[`, `<?`, in the core block and before it, after comments, fences (before the core) and inline code are taken out |
| Short-term exception | any span equal to the required text | only a span that is one token without whitespace (`TenantId`, a path) |
| Not changed | — | AGENTS.md, the core, the manifest, the generated adapters, the byte budget, every other rule |

## Known consequences (measured)

- The real AGENTS.md passes unchanged: every required text still counts, and `--check` reports 9 adapters in sync.
- A test case for an unclosed comment opener was dropped while the tests were written. An unclosed comment hides the
  rules (CORE-RULE), so CORE-HTML was the wrong expectation; the hiding is covered by the earlier tests.

## Not in this decision

- A new core sentence, a new required text, a live model call. No claim is made that a reader's behaviour changed.
