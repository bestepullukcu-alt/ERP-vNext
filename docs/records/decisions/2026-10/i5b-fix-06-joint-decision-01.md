---
decision_id: I5B-FIX-06-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-17
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 27327 (Codex Denetim001's v6 report, F5: an indented core rule passed), 27413 (F5 agreed; the Employee answer decided separately), 27589 (`<pre>`, `<script>`, `<div hidden>` around the numbered lines passed), 27820 (a code span across lines passed), 27954 (the Employee pair's method), 28444 (load05: the v6 loading pair does not carry over to the new core), 28628 (the owner's explicit data and destination approval for the loading and Employee pairs), 28726 (load05), 28756 (the blocked run; the normal run NOT VERIFIED at the login step; the staged closure)
scope: İ5B-FIX-06 — F5 (a required core text counts only on its numbered rule line; raw HTML and code spans across lines in the core are refused) and the Employee final-answer contract (core item 7: the answer's four parts, EMP-REPORT-01), with their generated core and tests
relates_to: I5B-FIX-05-JOINT-DECISION-01; CT-FIX-05-JOINT-DECISION-01 (Control Tower line); DCP-002; the Employee system acceptance scenario (Codex's rubric and fixed pair protocol)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# İ5B-FIX-06 — core rules on their numbered lines, and the module-request answer in four parts

## Why

The v6 audit (Codex Denetim001, F5): `sync_adapters --check` accepted a core whose numbered rules were indented by four
spaces or a tab — Markdown renders that as a code block, so the rule text was there and the rule was not. Codex then
showed the same for the numbered lines wrapped in `<pre>`, `<script>` or `<div hidden>`, and inside one inline code
span across lines. Separately, Employee pilot-03 (EMP-REPORT-01) gave a "Keşif Raporu" without saying that its
discovery had been refused: the module-request rule asked for research, not for an answer that names what was not
researched.

## What was decided

| Item | Before (`a7cb60992`) | Now |
|---|---|---|
| Where a core rule counts (F5) | anywhere in the visible text outside fenced code | only on a numbered rule line at column 0 (`^\d{1,2}\. \S`) of the visible, fence-free core; text elsewhere is reported "not on a numbered core rule line" |
| Raw HTML in the core (F5) | not checked | refused, `CORE-HTML` (a tag outside inline code; a tag shown in inline code is an example) |
| An inline code span across lines (F5) | not checked | refused, `CORE-SPAN` |
| Core item 7, the module request | research first; report a discovery that did not run | the final answer has four parts: the sources read; the discovery that did not run or is incomplete and why (then "kısmi inceleme"); candidates, duplicates and a reasoned first scope; the open approval and ownership decisions. A short clarification stays legitimate; a small fix needs no scan; no permission to code, commit or dispatch |
| `.claude/rules/agents-core.md` | digest `063fc7704cdd` | regenerated, digest `d207e869f167` |

## Known consequences (measured)

- The real core: 25 required texts, all on its 11 numbered rule lines; no raw HTML, no code span across lines;
  `sync_adapters --check` OK 9. `AGENTS.md` is 21,966 bytes, 34 under the warning threshold.
- The publication dependency is unchanged: `--discover` exists only on the Control Tower line.
- **Live measurements on candidate 4** (`3d4f0f405`; Codex's, with the owner's explicit approval to send the fixed
  candidate's instructions and the Employee research content to Anthropic for these two pairs only; the official CLI,
  default approval protections, no lasting permission; Codex's status file `028e5065c6e16e85dcb33c0890b15e391a94dd7fb1c29407de01921e0fe7a810`):
  - load05 — a narrow pass: the positive copy named `d207e869f167`, the negative "YOK"; no tools, no MCP, one turn
    each, the sources unchanged. Memory isolation and a start-context dump NOT VERIFIED. The first attempt failed on
    the sandbox network before any model response and is kept.
  - Employee blocked (default headless) — the bounded checks passed: the four parts, "Kısmi inceleme", the refused
    discovery named, no source change. It read only the domain-config and the placeholder Blueprint page, and its
    final answer asks whether a pack exists instead of reading it. That stays an open daily-use limit; the run is not
    repeated to pick a green result.
  - Employee normal (interactive, a one-time approval of the read-only discovery command) — **NOT VERIFIED**: one
    attempt failed on the network, one stopped at the CLI's login step before any model response. The login is the
    owner's step; no login or credential was entered by an agent.
- These commits close the source fixes as local technical commits. They are not a general acceptance, a normal-pilot
  success or a quality score; the normal pilot's state is settled (a login, or an explicit NOT VERIFIED) before the
  package goes to the independent auditors.

## Not in this decision

- No new system, no product code, no change to the discovery tool, no other instruction file, no permission or
  allowlist change.
- The earlier pilots and their scores are kept as they are.
