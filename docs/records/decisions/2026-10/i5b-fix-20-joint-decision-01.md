---
decision_id: I5B-FIX-20-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 37432 (the two C18 / v17 first reports kept by SHA-256 — Codex 77 UYGUN DEĞİL, Claude 72 UYGUN DEĞİL; the two roots both named C18-F1; the plan: the opening tag's name boundary in an HTML block, HTML's raw-text element sets in one place, noscript and plaintext not verified), 37566 (the joint decision on both roots recorded from the interim diff), 37587 (Codex's component check of 25c36d62 / 6317404c; the earlier 55 tests and helpers unchanged), 37610 (the new and the moved guards only; no wider CLI rerun; the C18 reports stay as they are)
scope: İ5B-FIX-20 — after the C18 audits, where an opening tag's name ends inside an HTML block and which elements HTML reads as raw text, in sync_adapters.py. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-19-JOINT-DECISION-01 (the closing-tag name boundary this extends to the opening tag); I5B-FIX-16 (the raw-text element reading); the first C18 / v17 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-20 — an opening tag's name, and HTML's raw-text elements

## Why

Two roots under the same contract (a hidden required text must not count), both reported as C18-F1:

- **Codex Denetim001.** In an HTML block an opening tag's name was cut at Python's `\s`: `<div` + NBSP + ` hidden>` was
  stacked as `div`, so `</div>` closed it, while HTML's element (`div` + NBSP) stayed open and hid the section (REF NBSP
  and U+3000, AGENTS.md: 0 / 0 where 1 / 1 was expected). İ5B-FIX-19 had checked the closing side only.
- **Claude Denetim001.** The raw-text elements were script, style, textarea and title only. HTML's tokenizer also reads
  xmp, iframe, noembed and noframes as RAWTEXT, so a `</div>` inside one is text and the outer `<div hidden>` stays open;
  the reader closed it (0 / 0 in a block and in a paragraph, REF and AGENTS.md).

## What was decided

| Item | Now |
|---|---|
| An opening tag on an HTML block line | its name must be followed by `HTML_NAME_END` (TAB, LF, FF, SPACE, `/`, `>`); otherwise HTML reads a longer name the reader does not match, so it is `tag`: not verified — never stacked under a shorter name, never skipped. Inside a raw-text element a tag is text, so it does not count there |
| The raw-text elements | HTML's own sets, in one place with the source named: RCDATA `title`, `textarea`; RAWTEXT `style`, `xmp`, `iframe`, `noembed`, `noframes`; script data `script` (`RAW_TEXT_ELEMENTS`). Inside one only its own closing tag counts, as before |
| noscript, plaintext | their opening tag is `tag`: not verified (`UNREAD_TEXT_ELEMENTS`). noscript is RAWTEXT only when scripting is enabled, and plaintext never ends; the check cannot know the mode. Reading noscript as raw text was not taken: it would pass `<noscript><div hidden></noscript>`, which hides the section when scripting is off |
| Not changed | the inline reader and `TAG_ATTRS` (the measured paragraph case, `<div` + NBSP + ` hidden>` with its `</div>`, passes), the closing-tag rules of İ5B-FIX-18 / -19, the block start, the lexer, the comment reader, every consumer policy |

## Known consequences (measured)

- The real files read the same as with İ5B-FIX-19's reader (`--check`: 9 adapters match).
- The sets come from the WHATWG HTML parsing section (specification reading). No browser, renderer or model was run; this
  is shown for the measured cases only, not a guarantee for every other input.

## Limits kept open

- Not an HTML, CSS or Markdown engine. C16-I1 (a diagnosis) stays deferred. P4, EMP-REPORT-01 and daily use stay open.

## Not in this decision

- A change to the inline reader or `TAG_ATTRS`, a raw-HTML ban, a new engine or dependency, AGENTS.md or the adapters, a
  live model call.
