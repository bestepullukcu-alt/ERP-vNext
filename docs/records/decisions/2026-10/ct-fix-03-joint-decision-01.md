---
decision_id: CT-FIX-03-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single writer, Codex independent verifier, joint local implementation and test; no push, no PR update, no merge); the owner answered "ok" to the joint plan (relayed by Codex)
decided_at_local: 2026-10-17 (scope agreed after the two v4 audits; contract notes agreed during the work)
decided_by: Codex and the AI session; the owner's answers came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 22050 and 22459 (the owner's "ok", the start), 22734 (Codex: an explicit metadata object is not an orphan duplicate), 23001 (sabotage only outside the writing worktree; the overlapping run invalid), 23555 (the GAPS wording), 23612 (VSTest's NotExecuted counter), 23923 (one heavy run after U1–U3), 23983 (U1–U3 scope and measures), 24051 (the contacts boundary), 24121 (the owner: 9 GB is enough), 24225 (a malformed queue row is never skipped by a draft), 24270 (B10's declared prerequisites)
scope: CT-FIX-03 — the v4 audit findings V4A-F2…F8 and V4B-B2, B3, B6, B8 on the Control Tower line, and U1–U3 (a short command entry on the card, a prompt draft, acceptance drafts). The adapter findings V4A-F1 / V4B-B5, B5b and the AGENTS-REFERENCE half of B3 are fixed on the İ5-B line as İ5B-FIX-03 and recorded there.
relates_to: CT-FIX-02-JOINT-DECISION-01; CT-FIX-01-JOINT-DECISION-01; R8 r3 (preparation vs in force); SOP §7 / §7.1; REP-001 §6.1–§6.2; PR #147 (`chore/ct/pqg-01`); PR #149 (İ5-B)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# CT-FIX-03 — one künye grammar, the whole SOP §7 profile, acceptances bound to their pack, trusted run files, a hook that needs a success line, and drafts that never approve

## Why

Two independent v4 audits of 2026-10-17 judged the CT-FIX-02 sources (`46d513e0a`) **UYGUN DEĞİL** and **UYGUN**: a
second `status` line indented or disguised was not seen (07, 11), a states file missing a SOP §7 state or a missing
transition table still dispatched (06), an acceptance pointed at a pack that did not exist and the report counted it
(13, 14), a TRX whose counters said "failed" turned a known red green (16), an empty verifier was read as "no
deviation" (18), a discovery report did not name the bytes of its task-source registry and its check tolerated changed
line endings (09, 10), and a row the owner had not confirmed read as the single target (19). The owner rejected a
usability score near 60/100 and asked for reliability first, then less repeated typing — without invented percentages.

## What was decided

| Item | Before (`46d513e0a`) | Now |
|---|---|---|
| **R1** künye (V4A-F3) | `kunye_status` counted only a column-0 plain `status:`; acceptance and routing kept their own readers | one grammar, `ct_queue.kunye_fields` / `kunye_status`, for `status`, `id`, `work_package`, `module`, `owner`, `queue`: a key written twice in any form (indented under a scalar key or under nothing, quoted, bold, another case, tab-indented) is refused; only `key: value` at column 0 is read; empty, block, multi-line, anchored, aliased, flow and unbalanced-quote values and `<<:` are refused; a field of another key's explicit object, list item or block text is that object's data (Codex); a column-0 `status:` in the body outside closed fences is a contradiction. Used by the prompt gate, require / validate, the effort approval, the acceptance check (level source, pack, chain identity) and the router (DCP, pack) |
| **R2** profile (V4A-F2) | five terminal states and READY checked; a missing transition table was no rows | the sop7 vocabulary is SOP §7's 19 states exactly (13 main flow + 6 exception); the 13 main-flow steps (with VERIFIED → ACCEPTED) are required rows; with a strict queue registered the table is required — in every consumer and in validate (`profile_problems`) |
| **R3** acceptance (V4A-F5) | the normal check read `module_pack` only for its format | a bound pack is read by `parse_pack` (module-check's function): it exists at its commit, its künye is this module's single id with a single approved / ready-for-dev status, every `AC-<n>` row of the list is one of its criteria with the same mandatory flag; a record without `module_pack` stays valid and says so. Every answer after the künye prints the limit: the check proves the code commit exists, not that the tests ran on it |
| **R4** discovery (V4A-F4, F8, V4B-B6) | an empty task cell was skipped; the task-source registry was not named; the report check stripped line endings | an empty `id` / `state` or every matchable cell empty is "tamamlanamadı"; the registry's sha256 and blob are in the report; `parse_report` reads a saved report byte for byte and is shared by `--check-report` and the prompt gate; every visible pack blob's künye id is read only to refuse a repeated or disguised id (the EKSİK line says so; a report of the older tool asks for a new run) |
| **R5** run files (V4A-F6) | a TRX's counters and summary were not compared with its rows | one ResultSummary and one Counters; non-negative integer counters equal to the raw result rows (before two rows with one name are merged), `executed` = rows − NotExecuted, `notExecuted` 0 (VSTest, measured on four real CRM runs) or the real count, an unknown outcome refused, a summary that says Failed exactly when a row failed; on all 45 committed TRX files the old and new answers are identical |
| **R6** hook (V4A-F7) | exit 0 with empty output was "sapma yok" | the verifier prints `GAPS-OK: <n> kontrol, 0 sapma` only when asked (`--ok-line`); the hook asks for it and says "no deviation" only on that one closing line; without the flag the verifier's output is unchanged (152 real list screens, byte for byte) |
| **B8** routing (V4B-B8) | a pending registry row was the single target without a mark | it is stamped "Hazırlık adayı; sahip teyidi bekliyor; yürürlükte değil" on the result line and in the question draft, no line calls it in force, `--in-force` refuses it |
| **D1** documents (V4B-B2, B3) | the card called `require` optional and did not name the profile stop; `orchestrator.md` said 39 rules | `require` is a real stop the gate also makes; a profile change takes effect after it is merged to main; the rule count is dated |
| **CI** | the regression set needed openpyxl the CI runner lacked | phase1 installs `scripts/requirements-tools.txt` (openpyxl 3.1.5, et-xmlfile 2.0.0) into an isolated venv under the runner's temp folder; no new action, no secret; without it the set raises, never skips |
| **U1** | — | the card's "Bugün hangi komut" table; `scripts/tests/test_ct_card_commands.py` checks every command against the tool's own `--help` |
| **U2** | — | `prompt_gate.py draft`: only Repository, Branch, Expected HEAD, Worktree and the WP id, measured after the discovery commit; every judgment field a `<<DOLDUR: …>>` marker; `check` refuses any marker; exit 0 is never a dispatch permission |
| **U3** | — | `acceptance_record.py draft --part record` (format, queue, work_package, module, module_pack) and `--part list --pack` (the pack's AC ID / text / mandatory flag exactly, result `denenmedi`); no lifecycle condition; `check` refuses any marker; both drafts first apply the queue's own structure check |

## Known consequences

- A DCP künye that writes `status` or `id` a second time under a scalar key is now refused (an explicit nested object
  is not). On today's 223 closed front matters there are 23 indented non-list lines, all inside explicit objects.
- MOD-0021's implementation plan, whose body repeats `status:`, reads as "künye dışında status satırı"; the old reader
  did not take it as an authority either.
- A discovery report of the older tool carries the older EKSİK line and the gate asks for a new run.

## Not in this decision

- The contacts address `ct_route --contacts` prints: it comes from the user's own address book outside the repository,
  only when asked; it is the routing purpose, not a publication. No masking code; a real contacts output is never put in
  evidence (Codex).
- B1 (PR descriptions) and B10 (the stored test commit) are produced and verified in this package; publishing a PR
  description needs its own authority.
- GitHub CI: NOT VERIFIED. The pilot of U1–U3 was run by the AI session in the Control Tower role on a synthetic
  fixture, not by a person.
