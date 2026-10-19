---
decision_id: CT-FIX-06-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 29104 (Codex Denetim001's v7 first verdict; the Employee research gap), 29150 and 29164 (the summary's scope: "olası kimlik çakışması — inceleme gerekli", parent/FU and the deprecated alias kept, per-ref values unresolved, a stated limit; the prompt gate's consumers measured), 29194 (both first verdicts in; the concrete narrow package), 29359 (the summary's three boundary counter-examples: a byte budget, the alias line, blob ids), 29555 and 29589 (EK1-2 and EK1-6 added; the required_evidence_source measurement; no architecture run; candidate 5 as a full tree without a checkout), 29696 (no source objection)
scope: CT-FIX-06 — the v7 audits' findings on the Control Tower line, narrowly. (1) `verify_module_id.py --discover` writes a bounded 'ÖZET' block under the report's header (the Employee pilot's research gap: a collision in a long report was lost when the interface cut it). (2) EK1-2: a cited evidence line in an acceptance list is a real, non-blank line. (3) EK1-6: the branch registry's required columns are filled, in the shared check_registry. The core-format finding (F5) is İ5B-FIX-07 on the İ5-B line.
relates_to: CT-FIX-05-JOINT-DECISION-01; I5B-FIX-07-JOINT-DECISION-01; REP-001 §6.2; SOP §20.2 (discovery report); the v7 audit delivery (Codex's)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# CT-FIX-06 — a bounded discovery summary, real evidence lines, filled registry columns

## Why

The v7 audits (Codex Denetim001: UYGUN DEĞİL, 75 / 100; Claude Denetim001: KOŞULLU, 71 / 100, then one criterion
corrected to KALDI in its addendum) left three narrow items on this line:

- The Employee normal pilot (7 / 10) read a discovery report whose tool output the interface cut at 19,882 characters.
  The PSS `MOD-0251-hris-external-sor` pack on `origin/hr-future` was a visible finding in the report, but the final
  answer skipped it. The report had no short, early place for the collisions and the packs' owners.
- EK1-2: an acceptance list row cited `stdout.txt@E:1` of an empty run output, or line 2 of a one-line output ending in
  LF, and the check allowed ACCEPTED.
- EK1-6: a branch registry row with an empty `scope` passed `validate` and `require --in-force`; only origin/main's
  registry was checked for empty fields (CT-FIX-05 F1).

## What was decided

| Item | Before (`c203303ce`) | Now |
|---|---|---|
| The discovery summary | none; the BULGU lines followed the sources | right under `KEŞİF RAPORU v2`, lines starting `ÖZET ` (no BULGU, no decision, no single-line field of the report contract), at most 3500 UTF-8 bytes and 420 per line, room kept at the end for the closing lines. The order: the counts; each file id on more than one matched pack path as "olası kimlik çakışması — inceleme gerekli (tek başına kesin mükerrer değil)" with those packs' `status` / `approval_status` / `owner` right after; then the other matched packs. A deprecated alias is said in its own "ÖZET BİLGİ" line and its paths are "kimlik çokluğu — takma ad ilişkisi belirsiz"; parent and FU ids stay different (the tool's own file-id reading). Values that differ between blobs are "belirsiz" with blob and ref ids; a pack without a readable künye is "künye okunamadı (bilgi; kapı değil)". Whatever does not fit is counted in "ÖZET EKSİK"; the limit line says that a cut report is a partial review |
| The rest of the report | — | the BULGU / BİLGİ lines, the sources, the refs, the result and the exit code unchanged; `--check-id` and `--candidate` unchanged. A new report's bytes and `rapor-sha256` include the block; a saved report keeps its own tool identity |
| EK1-2 | `1 <= n <= len(content.split("\n"))` | `evidence_line_problem`: `content.splitlines()`; the line exists and is not blank. What the line means is not checked |
| EK1-6 | empty fields checked in `in_force_status` for main only | `check_registry` checks every column of the registry contract (no new column or value); `in_force_status` and `ct_route` use it |
| Not changed | — | `required_evidence_source`: measured — it already requires the evidence level on the cited line, so a blank or phantom line is refused; no refactor |

## Known consequences (measured)

- Real data (`docs/records/audits/2026-10/ct-fix-06/real-data.txt`): the "Employee" report keeps every line other than
  ÖZET, `araç:` and `rapor-sha256:` (BULGU 15 → 15); the block is 2874 bytes and holds the MOD-0251 collision inside
  the first 4 KB; `--check-report` passes on the new report and on the committed tool's report.
- The real registry has 1 row and no empty field; the SCE `validate` output is byte for byte the same (Codex).
- No architecture run for this Python package (NOT RUN): candidate 4's run (137 / 144, raw exit 1) stays historical
  evidence and is not presented as this tree's.

## Not in this decision

- A new core sentence, agent, ledger, legacy migration (the 1103 older acceptance entries, the 92 old packs), wider
  legacy scanning, or a guarantee that every finding fits in 4 KB.
- A live model call: a new Employee normal pilot needs the owner's new data and destination approval (Codex's).
