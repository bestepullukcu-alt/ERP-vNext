---
decision_id: I5B-FIX-05-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-17
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript after the Employee pilot-01 report (Codex: "Önerdiğin dar İ5B-FIX-05'i aynı yetki kapsamında birlikte yapalım … AGENTS çekirdek madde7 + üretilen agents-core + required_text çapası ve ilgili test/kanıt dışında genişletmeyelim"), the publication dependency ("İ5B-only --discover yokluğu önemli bir YAYIN BAĞIMLILIĞI … komut yoksa sessiz atlama yok, eksik olarak raporlansın"), and the fenced-code counter-example ("required_text'i de fenced-code dışındaki görünür metinden kontrol etmek en küçük ortak düzeltme")
scope: İ5B-FIX-05 — the instruction gap the Employee pilot-01 exposed: a named module request is researched before options are offered (AGENTS.md core item 7), the generated core, its required_text anchor, and core rules read outside fenced code.
relates_to: CT-FIX-04-JOINT-DECISION-01 (Control Tower line); İ5B-FIX-04; DCP-002; the Employee system acceptance scenario (Codex's rubric)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# İ5B-FIX-05 — a named module request is researched first

## Why

Pilot-01 (Codex, the official CLI in a fresh non-nested session, plan mode with Read/Glob/Grep/Bash, on candidate `ff219eb75`,
the only prompt "Employee modülünü yapalım."): the chat read `AGENTS.md`, the existing MOD-0251 pack and its
domain-config, stated the P2-only approval correctly, wrote no code — and asked three scope options without reading the
Blueprint or running discovery, so it did not see the second MOD-0251 pack on `origin/hr-future` or the four adjacent
Blueprint modules. Codex scored it 6 / 10 with EMP-01 and EMP-04 failed; not a wrong dispatch.

Observed instruction gap (not a proven root cause): no entry instruction routes a natural-language module request to
discovery. `--discover` appears in
none of `AGENTS.md`, `CLAUDE.md`, `.antigravity/{agents,workflows,rules}` or `AGENTS-REFERENCE.md`; core item 7 and
DCP-002 tie `verify_module_id.py` to *creating* an id; `add-module` Phase 0 asks for AGENTS, domain-config and the pack —
which is exactly what the chat did. The gap was real; whether closing it changes the behaviour is a separate question
(below).

## What was decided

| Item | Before | Now |
|---|---|---|
| `AGENTS.md` core item 7 | identity check only before a pack is opened | + "**Modül talebi:** Adı verilen yeni modül ya da kapsam istenince önce araştır, sonra öner: Blueprint, `python3 .antigravity/scripts/verify_module_id.py . --discover "<ad>"`, mevcut pack ve domain-config; adayları ve mükerrer kimlikleri göster, gerekçeli ilk kapsamı öner, yalnız açık kararları sor. Keşif koşmaz ya da eksikse bunu söyle; iş kanıtlı yeni iş sayılmaz. Belirsiz talepte kısa netleştirme meşru; küçük düzeltmede tarama gerekmez. Kod, commit ya da dağıtım izni değildir." |
| `.claude/rules/agents-core.md` | — | regenerated from the source (`sync_adapters.py`), digest `063fc7704cdd` |
| `adapters.json` core `required_text` | — | + "Adı verilen yeni modül ya da kapsam istenince önce araştır" |
| `sync_adapters.py` | core rules read with HTML comments removed only | core rules read from the visible text outside fenced code (any indentation; an unclosed fence hides the rest; inline code spans kept) — a rule kept only inside a code block is an example, not a rule (Codex's counter-example) |

## Known consequences

- **Publication dependency:** `verify_module_id.py --discover` exists only on the Control Tower line (PR #147's
  lineage). On the İ5-B line alone the command exits 2 ("unrecognized arguments"); in the combined tree it runs (exit 3
  for "Employee"). The rule is usable only after or together with the Control Tower line; on the İ5-B line alone the
  research cannot be called complete, and the rule itself says to report a discovery that did not run.
- `AGENTS.md` is 21,967 bytes: 33 bytes under the 22,000 warning threshold (the instruction budget test fails on a
  warning). Any further addition to `AGENTS.md` needs text moved to `AGENTS-REFERENCE.md` first (🟡).
- The earlier narrow loading evidence (one headless CLI pair on `c1941dbcc`) does not carry over to the new core.
  Codex's loading-04 on candidate 3 (two sparse instruction copies at the same SHA, the only difference the generated
  core; no tools, no MCP): the positive run named the new core digest `063fc7704cdd`, the negative found none — a
  narrow pass, not a general or memory-isolated loading guarantee.
- **Pilot-02** (the same prompt, rubric and model, a fresh session, on candidate 3 with the rule loaded): 6 / 10 again,
  EMP-01 and EMP-04 not met — the chat read `AGENTS.md`, the MOD-0251 pack and its domain-config but ran neither the
  Blueprint lookup nor discovery. Plan mode's own plan-file step was refused by the CLI (no write tool given) and was not
  worked around. Closing the instruction gap alone did **not** change the first response in this run; the general root
  cause is not proven. A method-controlled run (the same everything, only the permission mode) is Codex's decision and
  is recorded separately.

## Not in this decision

- No new system, no product code, no change to the discovery tool's modes, no other instruction file.
- Pilot-01 is kept as it is; its score is not revised.
