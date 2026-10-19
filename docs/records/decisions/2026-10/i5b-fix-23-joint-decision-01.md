---
decision_id: I5B-FIX-23-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based line 38545 (the two C21 / v20 first reports kept by SHA-256 — Codex 78 UYGUN DEĞİL, Claude 72 UYGUN DEĞİL; both found the comment inside an attribute independently, Codex root reproduced it on the real CLI; the short overlap rule chosen over a one-pass refactor, its cost — a literal comment wholly inside an attribute refused — declared; a design trial outside the repository, not a production change)
scope: İ5B-FIX-23 — after the C21 audits, a comment span inside a tag, in sync_adapters.py. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-22-JOINT-DECISION-01 (comment spans given to mask_html; a comment inside a raw-text element or a bogus comment); the first C21 / v20 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-23 — a comment inside a tag

## Why

The comment reader took a `<!--` inside an attribute value for a comment and masked it. The masked view then showed one
long tag (`<span title="        ">`) where HTML ends the tag at the quote, so the real `<table>` after it was never seen
and `</div>` closed the hidden div at the top (HTML ignores it). Write / check 0 / 0 where 1 / 1 was expected (Claude
Denetim001 and Codex Denetim001, C21-F1, found independently; REF and AGENTS.md). The same holds for a closing tag that
ends at its first `>` (`</span <!-- > <table> -->>`), measured here.

## What was decided

| Item | Now |
|---|---|
| A comment span starting inside a tag the reader reads | `tag`: not verified — checked right after the tag's paragraph / block limit, before any opening or closing branch, with the comment spans İ5B-FIX-22 already gives `mask_html`. HTML reads no comment inside a tag |
| A literal comment wholly inside an attribute | refused too: a declared narrowing — it passed before (Codex's input 27) |
| Not changed | an ordinary attribute, a comment right after a tag or between tags, code spans and fences, every İ5B-FIX-22 rule; no new comment or attribute lexer |

A single-pass reader (comments recognised by `mask_html` itself) was proposed by Claude Denetim001; it is a larger
refactor of the context rules and was not made a condition for closing this gap.

## Known consequences (measured)

- The real files read the same as with İ5B-FIX-22's reader (`--check`: 9 adapters match).
- With this rule a masked comment no longer starts inside any of the three token kinds `mask_html` reads (a tag, a
  raw-text element, `<?` / `<!X` / `<![CDATA[`). This is shown for the measured cases; no browser, renderer or model was
  run, and no general renderer equivalence is claimed.

## Limits kept open

- Not an HTML, CSS or DOM engine. G-C12 (İ5B-FIX-22) stays a test-sensitivity limit. C16-I1 stays deferred. P4,
  EMP-REPORT-01 and daily use stay open.

## Not in this decision

- A one-pass reader, an attribute grammar, a raw-HTML ban, a new dependency, AGENTS.md or the adapters, a live model
  call.
