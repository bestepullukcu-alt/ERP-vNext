---
decision_id: I5B-FIX-22-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 38239 (the two C20 / v19 first reports kept by SHA-256 — Codex 79 UYGUN, Claude 73 UYGUN DEĞİL; the favourable one not chosen, Claude's C20-F1 reproduced by Codex root; the plan: the three early comment ends not verified), 38296 (option A — the same masking root also inside raw-text elements and for '<?' / '<![CDATA[', each reproduced by Codex root on the real CLI), 38368 (Codex's component check of d3f9cc7b / 8acc3c36; a comment inside '<!X' under the same inner-comment rule accepted; '<?' is not always a bogus comment in current HTML, its bound is the first '>')
scope: İ5B-FIX-22 — after the C20 audits, ranges the reader masked that HTML ends earlier, in sync_adapters.py. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-21-JOINT-DECISION-01 (the close below the top, which a masked opener got around); I5B-FIX-16 (the comment reader); the first C20 / v19 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-22 — a masked range HTML ends earlier

## Why

The reader masked a range that HTML ends earlier, so a real opener behind it (`<table>`) was never seen; İ5B-FIX-21's
close-below-the-top rule could not act, and `</div>` closed the hidden div at the top while HTML ignores it (no div in
scope). Write / check 0 / 0 where 1 / 1 was expected:

- **Claude Denetim001 (C20-F1).** HTML ends a comment at `--!>`, and at once after `<!-->` or `<!--->`; the reader read
  on to the first `-->` (REF in a block and a paragraph, AGENTS.md).
- **Codex root, the same root.** Inside a raw-text element `<!--` is text (or a script escape a `</script>` still ends),
  but the reader masked it as a comment and so missed the real `</style>` / `</script>`; `<?` and `<![CDATA[` end at
  their first `>` in HTML content, the reader read on to `?>` / `]]>`. Codex measured these on the real CLI.

## What was decided

| Item | Now |
|---|---|
| A comment HTML ends earlier | `comment_end_unread`: a comment that starts with `<!-->` or `<!--->`, or has `--!>` before its first `-->`, is not verified — in the block comment, the comment inside an HTML block and the inline comment alike (`code`: the existing comment not-verified path). Also with no hidden element open: a declared limit. The early end is not followed |
| A comment inside a raw-text element | `code`: not verified. The comment spans scan_comments finds are given to `mask_html` (an optional parameter, passed by its two callers) |
| A comment inside `<?`, `<!X` or `<![CDATA[` | `code`: not verified — HTML reads no comment there, and its real first `>` may be inside it |
| `<?` and `<![CDATA[` | a `>` before the reader's `?>` / `]]>` is `tag`: not verified. In current HTML not every `<?` is a bogus comment (processing-instruction states exist), but both end at the first `>` |
| Not changed | the standard comment, an unclosed comment, the core marker's comment rule, code spans and fences, `<!X` without a comment inside, the inline reader and `TAG_ATTRS`, every consumer policy |

## Known consequences (measured)

- The real files read the same as with İ5B-FIX-21's reader (`--check`: 9 adapters match).
- A comment ending `--!>` or written `<!-->` / `<!--->` is refused even where nothing is hidden. The ends come from the
  WHATWG HTML parsing section and one static parse with an installed html5lib; no browser, renderer or model was run,
  and this is shown for the measured cases only.

## Limits kept open

- Not an HTML, CSS or DOM engine; no general renderer equivalence and no claim that every tree-construction rule is now
  safe. C16-I1 (a diagnosis) stays deferred. P4, EMP-REPORT-01 and daily use stay open.

## Not in this decision

- Following HTML's early comment ends, a processing-instruction or CDATA engine, a raw-HTML ban, a new dependency,
  AGENTS.md or the adapters, a live model call.
