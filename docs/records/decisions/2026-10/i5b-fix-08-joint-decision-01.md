---
decision_id: I5B-FIX-08-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 30713 (the core answer contract tightened within the byte budget; no measured-behaviour guarantee), 30736 (the right to ask kept: B9; the contract parts made required texts of the production check)
scope: İ5B-FIX-08 — AGENTS.md core item 7's module-request answer contract (B9), the generated core, five new required texts in adapters.json, and their tests. Nothing else in AGENTS.md, no threshold, no other rule.
relates_to: I5B-FIX-07-JOINT-DECISION-01; CT-FIX-07-JOINT-DECISION-01; the C4, C5 and C6 Employee pilot assessments (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-08 — the module-request answer: one first scope, every open decision, the right to ask

## Why

In the C4, C5 and C6 normal Employee pilots (one model answer each; C5 was assessed twice, by the AI session and by Codex) two
non-critical criteria stayed unmet in every assessment: EMP-06 (a menu instead of
one reasoned first scope) and EMP-08 (an ownership gap — "owner TBD" — left out although the discovery summary showed
it). The reports also mixed inference with fact and did not always say "kısmi inceleme" when the discovery was partial.

## What was decided

| Item | Before (`60878562a`) | Now (B9) |
|---|---|---|
| The four parts | okunan kaynaklar; koşmayan ya da eksik keşif ve nedeni (öyleyse "kısmi inceleme"); adaylar, mükerrerler ve gerekçeli ilk kapsam; açık onay/sahiplik kararları | okunan kaynaklar (çıkarım ayrı); koşmayan ya da eksik keşif ve nedeni ("kısmi inceleme"); adaylar/mükerrerler, gerekçeli ilk kapsam (tek öneri); tüm açık onay/sahiplik kararları (TBD dahil) |
| An unclear request | Belirsiz talepte kısa netleştirme meşru | Belirsizde sor, salt-okunur adım öner — the right to ask kept (EMP-01 asks for it); a safe preparation step proposed alongside, no product scope forced |
| What is read first | Blueprint, discovery, mevcut pack ve domain-config | Blueprint, discovery, pack ve domain-config ("mevcut" dropped for the byte budget) |
| The production check | the module-request anchor only | + the five contract parts as required texts in `adapters.json` — removing one, or hiding it in an HTML comment, fails `sync_adapters --check` by name |

`AGENTS.md` 21,993 bytes (7 under the 22,000 warning); the core's source digest `abb98eaa05b5…` (İ5B-FIX-07: `d207e869f167…`).

## Known consequences

- The earlier loading evidence (load05 on `d207e869f167`) does not carry over; a new narrow loading pair is needed, and
  any behaviour claim needs a new normal pilot — both need the owner's new, specific approval (not the C6 one).
- The required texts protect the instruction source against loss; they do not show that a model follows the contract.
- A further addition to `AGENTS.md` needs text moved to `AGENTS-REFERENCE.md` first.

## Not in this decision

- No new authority, gate, schema, legacy change, CT data change or other instruction file; no live call.
