---
decision_id: CT-FIX-07-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 30182 (the three summary data lines agreed; HEAD presence bound to git's HEAD tree; no new authority), 30271 (Codex Denetim001's narrow view: two wording corrections; the first scope and the owner are not guaranteed), 30305 (an unmeasured presence must not read as absent), 30371 (19 / 19 independent CLI checks on the final tool)
scope: CT-FIX-07 — after the v7 C5 Employee pilot (5 / 10 by the AI session, 7 / 10 by Codex; both first scores kept), three information lines in the discovery summary of verify_module_id.py — the open decisions, the Blueprint candidates' ids, the matched packs' domain-config paths with their presence in HEAD's tree. verify_module_id.py and two test files only.
relates_to: CT-FIX-06-JOINT-DECISION-01; I5B-FIX-07-JOINT-DECISION-01; the C5 pilot assessments (the AI session's and Codex's, kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# CT-FIX-07 — open decisions, Blueprint ids and domain-config presence in the discovery summary

## Why

In the C5 controlled pilot the interface kept the head and the tail of the discovery output. The ÖZET block was in the
head: the MOD-0251 collision and the owner "HCM / HR data owner TBD" were visible. The answer used the collision only in
part, did not mention the owner, did not read the domain-config, and named only MOD-0251 of the five Blueprint
candidates, whose other ids the summary gave only as a count.

## What was decided

| Item | Before (`70c3561d5`) | Now |
|---|---|---|
| Open decisions | — | "ÖZET açık kararlar (bilgi; onay değil)": each identity collision (a deprecated alias says so) and each matched pack whose `owner` is TBD, empty or absent, with every value found on its blobs; right after the collision groups |
| Blueprint candidates | a count only | "ÖZET blueprint adayları": ids and short names from the discovery's own Blueprint findings; the rest counted |
| domain-config | — | "ÖZET domain-config": the path in each matched pack's own domain folder (never guessed), with HEAD's tree only — "HEAD'de var" (a blob), "HEAD'de yok" (git: missing), "HEAD'de ölçülemedi" (git failed or answered otherwise). A file only in the working tree is not in HEAD; the config on the pack's own ref stays undecided; this command does not inspect the content (it cannot know what a reader read elsewhere) |
| Not changed | — | the header, the BULGU lines, the decision count, the exits, --check-id, --candidate, the 3500 / 420 byte budget; no new authority, gate, legacy stop or schema |

## Known consequences (measured)

- Real "Employee" report: every line except ÖZET, `araç:` and `rapor-sha256:` identical to CT-FIX-06's; BULGU 15 → 15;
  the block 3089 bytes with the three lines inside the first 4 KB; two pack lines no longer fit and are counted.
- A test found the künye line by position; it now finds it by prefix (the open decisions may come first) — a declared
  order change.

## The limit kept open

These lines move information to where it is read; they do not make a reader use it. The C5 pilot showed a visible owner
TBD left unused. A reasoned first scope, settling the domain decisions and the relation of the two MOD-0251 records
from the sources before asking a person (Codex Denetim001), is not guaranteed by this change and is not closed.

## Not in this decision

- A new core sentence or agent, a live model call, the settings cleanup (prepared apart, outside the repository).
