---
decision_id: CT-FIX-08-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 31125 (items 1–4 of the v8 / C7 fix proposal agreed in a narrow local scope; C7-B not now; C7-A a declared limit; the "-" placeholder out of scope), 31433 (Codex's snapshot 01; an R0 run on older sources is diagnostic only, the one final run is bound to the final sources), 31476 (invisible test characters as explicit escapes; nul_paths refuses a missing final NUL and an empty inner entry, b'' is no entry), 31692 (Codex's snapshot 02 on the final sources; no new live call; the final run named by the tree it ran on, not as a C8)
scope: CT-FIX-08 — after the v8 / C7 audits, items 2–4 of the agreed proposal on the CT side — one visible-text rule for evidence lines and registry values, strict NUL path listings in the four git path consumers, and a broken registry that stops validate and require; the open decisions second in the discovery summary. Five CT tools and two test files.
relates_to: I5B-FIX-09-JOINT-DECISION-01 (item 1, a separate branch); CT-FIX-07-JOINT-DECISION-01; the first v8 / C7 audit reports of Codex Denetim001 and Claude Denetim001 (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# CT-FIX-08 — visible evidence text, NUL path listings and a broken registry that stops

## Why

The v8 / C7 audits reproduced five CT gaps:

- A cited evidence line made of a zero-width space, a BOM, a word joiner, a mark alone or a variation selector passed as
  content, and `splitlines()` split a line at U+2028 or a form feed that git and the record do not treat as line ends.
- A registry value made only of Cf characters passed the emptiness check.
- Git's default listing C-quotes a path with a non-ASCII byte, a quote, a backslash or a control character. The gate,
  the tracked and changed lists, the v2 record index, the report and the discovery read those quoted names and missed
  the file.
- With a broken registry, `validate` went on to read task files (a traceback), and `require` answered 1 like an
  ordinary refusal.
- On a wide query the open-decisions line was pushed down the summary.

## What was decided

| Item | Before (`5035a9f28`) | Now |
|---|---|---|
| Visible text | `strip()`; evidence lines by `splitlines()` | one helper, `has_visible_text`: a letter, number, punctuation or symbol (L, N, P, S). Cf, spaces, controls and marks alone are not visible. Evidence lines are split on LF only (CRLF normalized, a final LF adds no line). The registry emptiness check uses the same helper |
| Path listings | the text listing, quoted names | `-z` in the gate, `tracked` / `changed` / the v2 record index, the report and the discovery. They share one strict decoder, `nul_paths`: the listing must end with NUL, no inner entry may be empty, every entry must be UTF-8, and `b''` is no entry. Any other listing is incomplete, never guessed. The helper is loaded once |
| A broken registry | `validate` read task files; `require` exit 1 | `validate` prints "FAIL kayıt geçersiz — görev dosyaları okunmadı" and returns 1 before reading any queue; `require` exits 2 ("DUR"). A main-profile conflict stays exit 1 |
| Summary order | the open decisions after the collision groups | right after the counts |
| Not changed | — | AGENTS, the core, the manifest, the generated adapters, the product, the old CT, SCE, SOP 36.2; no new gate, authority or schema |

## Known consequences (measured)

- `require` on a broken registry now exits 2, not 1. A CTFIX06 test that expected 1 was updated as a declared contract
  change.
- The ÖZET lines of the real "Employee" discovery are the same set in a new order. Two tests that found lines by position
  now find them by prefix.
- The real queue's `validate` and `summary` outputs are identical to the base tools'. Codex's real SCE comparison is
  byte-equal.

## Not taken, kept open

- **C7-A** (one id in a main pack and in a duplicate pack): a declared limit, not a stop. Three legitimate multi-pack
  ids exist at HEAD (MOD-0024, MOD-0290-FU01, CAND-CAP-0006). A source author can still write a duplicate.
- **C7-B** (an ancestor rule for the freshness of an input): not taken now. A stale input stays an open risk.
- **The "-" placeholder** as an empty registry value: new policy, out of scope. The real registry has none.
- The P4 "onaylı" wording defect and EMP-REPORT-01 stay open. No new live model call is made: Codex measured that the
  instruction inputs are byte-equal to C7's. No claim is made that a C8 Employee run or P4 improved.
