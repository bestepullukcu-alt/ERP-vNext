---
decision_id: CT-FIX-09-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 32184 (Codex's C8 triage: G1 and G7 reproduced, a narrow same-scope follow-up proposed), 32362 (the scope agreed; acceptance_record only if its own real CLI shows a broken line; two details asked: no global Stop, one_line before the budget), 32412 (both details agreed: stdout captured in the gate's queue read, fail and the CLI unchanged; one_line for display only, before the byte budget), 32556 (acceptance_record's output injection confirmed on the real CLI — one output point, exit 1 kept)
scope: CT-FIX-09 — after the C8 / v9 audits, two CT findings — C8-G1, the gate's diagnosis of a queue it could not read, and C8-G7, a file name with a control character in the discovery report and the acceptance check. ct_queue.py, prompt_gate.py, verify_module_id.py, acceptance_record.py and one test file.
relates_to: I5B-FIX-10-JOINT-DECISION-01 (C8-F1 / F2, a separate branch); CT-FIX-08-JOINT-DECISION-01; the first C8 / v9 audit reports of Codex Denetim001 and Claude Denetim001 (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# CT-FIX-09 — the gate names why a queue could not be read; a file name stays on one output line

## Why

Both findings came from CT-FIX-08 itself.

- **G1.** CT-FIX-08 made `ct_queue`'s dispatch loader print its stop ("DUR: …") and exit 2. The gate reads the queue in
  process and took the `SystemExit` text as its reason. The reason was therefore printed before the hashed report, and
  the report kept only "ENGEL kuyruk: 2". The gate also had no queue row in this case. It still judged the discovery's
  binding and added a false "sorgu … bu işin adı ya da modül adı değil". A queue file that is not UTF-8 made the gate exit
  2 with no report at all.
- **G7.** CT-FIX-08 read git's `-z` listings, so a real file name reached the output. A name with a line feed or a
  carriage return then added lines: five "sonuç:" and "çıkış:" lines in a discovery report whose hash `--check-report`
  accepted, and a false "sonuç: ACCEPTED'e izin verir" / "çıkış: 0" in an acceptance check that exits 1. Before CT-FIX-08
  the same pack was silently missed (BULGU 5), so this is a new exposure, not an old one.

## What was decided

| Item | Before (`65c53153e`) | Now |
|---|---|---|
| The gate's queue read | `str(SystemExit)`; the stop printed before the report | `real_queue` reads the queue with `ct_queue`'s stdout captured. A stop is named by what it printed, or by a `SystemExit` text, inside the hashed report as "ENGEL kuyruk: DUR: …". Output on a successful load is a queue error, never swallowed. `ct_queue.fail()` and the `ct_queue` CLI are unchanged (no global change) |
| The discovery binding without a queue row | "sorgu … bu işin adı ya da modül adı değil" | "keşif: kuyruk okunamadı — keşfin bu işe bağı ölçülemedi" |
| A queue file that is not UTF-8 | exit 2 and no report | the full report, "ENGEL kuyruk: DUR: q.tsv UTF-8 değil …", "sonuç: engel", "çıkış: 1". Never green |
| A control character in a dynamic value | written raw | `ct_queue.one_line`: Cc, U+2028 and U+2029 are shown as `\n`, `\r`, `\t`, `\xNN`, `\uNNNN`. It applies to each ÖZET item before its 420-byte cap and the 3500-byte budget, to each logical line of the discovery report, and at the acceptance check's one output point. Structural line ends are untouched: one logical line is one physical line, with one "sonuç:" and one "çıkış:" |
| Not changed | — | the BULGU count, exits 0 / 1 / 2 / 3, the report hash rule, the byte budget, the gate's normal 0, `ct_queue`'s CLI output; no new policy |

## Known consequences (measured)

- Real "Employee" discovery: 627 lines either way, BULGU 15 → 15. Only the `araç:` and `rapor-sha256:` lines differ,
  the tool's own identity. No claim is made that the new report equals the old one.
- On the real registry the gate's queue read prints nothing: closed and unverified rows answer 1, the first
  dispatchable row (Q62) 0.

## Limits kept open

- `one_line` is display only. A backslash is kept, so `a\n` written in a real name and an escaped line feed look the
  same. The shown name is not unique and is never decoded back or used as evidence of the real name.
- Only the measured output points are covered: the discovery report and the acceptance check. `module-check` and other
  output points are not claimed.
- A real file name that is not UTF-8 is still NOT VERIFIED on macOS (EPERM). Only Git-object fixtures are measured.

## Not in this decision

- A global change of `ct_queue.fail()`, an invisible-character blacklist, an approval or freshness policy, a model or
  architecture run.
