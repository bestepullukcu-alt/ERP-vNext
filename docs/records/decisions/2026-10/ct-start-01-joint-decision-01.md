---
decision_id: CT-START-01-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave ("ok yapalım yap"; Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-19
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 38841 (the owner's request: start a module's Control Tower from one file / command; propose a scope when the module does not exist; check a document against code and evidence when it does; a start pilot that touches no product code), 38944 (Codex agrees to the six-file plan; budget sections must refuse an empty body; --check-report proves integrity, not freshness; the full suite only on the combined candidate), 38991 (three content fixes: reuse the existing identity and pack; Blueprint without a pack → a draft pack step, not code; no Blueprint entry → parent / FU, then the CAND-CAP gate; the MOD-0251 example kept as a dated historical one)
scope: CT-START-01 — one skill, `control-tower`, that starts a module's Control Tower read-only. Its canonical source and the two adapters `sync_adapters.py` generates from it, and one item and one budget entry in `.antigravity/adapters.json`. The CT tools, the operating card, AGENTS.md, the tests and the product code are not changed.
relates_to: the C7 Employee pilot (EMP-03 the approval scope, EMP-06 one reasoned first scope, EMP-REPORT-01 a partial reading named as such — historical evidence, not re-scored here); the CONTROL TOWER operating card (the commands the skill points to)
recorded_by: the AI session (Claude Code) on 2026-10-19
---

# CT-START-01 — one read-only start for a module's Control Tower

## Why

The owner wants a new conversation to start a module's Control Tower by calling one file or command: an existing
module is checked against its code and evidence; a module that does not exist gets candidates and a first scope.
The C7 Employee pilot showed where a natural answer goes wrong: the pack's `approved` field read as approval for the
whole module (EMP-03), no reasoned first scope (EMP-06), a partial reading reported as a full one (EMP-REPORT-01).

## What was decided

| Item | Now |
|---|---|
| One source | `.antigravity/skills/control-tower/SKILL.md`; `sync_adapters.py` generates `.claude/skills/control-tower/SKILL.md` and `.agents/skills/control-tower/SKILL.md` (frontmatter from the source: name and description only; no `$ARGUMENTS`) |
| Call | Claude Code `/control-tower <name>`, Codex `$control-tower <name>`, else `@.antigravity/skills/control-tower/SKILL.md <name>`; the module name is read from the call |
| What the skill asks | measure branch / HEAD / status and which tools exist in this tree; read only the related inputs; reuse a discovery report only for the same work with HEAD, dirty source bytes and ref identities measured unchanged (`--check-report` proves integrity, not freshness); read the pack's current `approval_status` and scope text, never spread an approval to the whole module; reuse the existing identity and pack; Blueprint without a pack → propose a draft pack step; no Blueprint entry → parent / FU, then the `CAND-CAP` gate; one reasoned first scope marked "a proposal, not approval"; sources read and not read, facts apart from inferences |
| What it does not do | write to a queue or a registry, `guard`, a prompt draft file, product code, a commit, push or PR; it gives no scope, pack, code or dispatch authority; the commands stay in the operating card and are not copied |
| A guard on the skill itself | the existing budget check: `max_bytes` 6500 and six `required_sections` with one anchor sentence each (a section emptied, a heading removed or an anchor lost is refused); `--check` refuses a deleted or changed generated copy. No new code |

## Known consequences (measured)

- Eleven adapters instead of nine; the nine earlier generated files are byte-identical; the two new ones equal the
  source except the generated-copy banner.
- On the İ5B tip the CT tools (`ct_queue.py`, `prompt_gate.py`, `ct_route.py`, …) do not exist — they live on the CT
  branch. The skill measures what exists and says what does not; publishing needs both branches together.

## Limits kept open

- No live model call: whether a model follows the skill, a natural Employee answer (EMP-01…10) and any human or token
  benefit are NOT VERIFIED. The C7 results stay historical.

## Not in this decision

- A new launcher, command copy or framework, a new CT authority, a change to the CT tools, the operating card,
  AGENTS.md, the tests or product code, a live model call.
