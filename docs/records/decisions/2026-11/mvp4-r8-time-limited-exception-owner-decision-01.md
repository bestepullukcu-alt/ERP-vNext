---
decision_id: MVP4-R8-TIME-LIMITED-EXCEPTION-OWNER-DECISION-01
status: approved — in force when this record is on main
decided_at_local: 2026-11-11 (Europe/Istanbul)
decided_by: the repository owner (the user of the AI session; this record adds no name)
decision_source: owner answer in the Claude Code session that acts as the MVP-4 coordination tower (session 4e81c85c), 2026-11-11, given as a button choice on a question the session asked. The transcript is on the owner's machine, not in the repository.
scope: R8 (control-tower-sop.md revision r3, "No registered task source") for the MVP-4 developer lane only
relates_to: CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01; CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01
recorded_by: the AI session (Claude Code) on 2026-11-11, on the owner's instruction; this record makes no decision of its own
---

# MVP-4 — time-limited R8 exception (registered task source)

## Facts measured before the decision (origin/main 9166569ba, 2026-11-11)

- `execution/delivery/ct-queue-registry.tsv` has no row for MVP-4 (rows: sce-mvp6, pvg, pss-ui, treasury-intake,
  sce-mvp5-wms, pss-wf, hcm-employee, hcm-leave, mg, mdm). The `sce-mvp6` source is "SCE / MVP6 only".
- R8 r3 reached main with PR #147 (merge 913d725a1, 2026-10-19). The 30-calendar-day transition therefore ends on
  **2026-11-18**.
- MVP-4 lane work in flight: MOD-0188 (#227, #228, #256), MOD-0189 (#410), MOD-0191 scope proposal
  (branch `feature/sce/mod-0191-activation-override-ui-scope`, f35b85d7e). New work packages for MOD-0191 activation /
  override / UI and MOD-0189 run binding are expected to be approved after 2026-11-18.

## Question asked (verbatim)

> MVP-4'ün kayıtlı iş listesi yok; geçiş süresi yaklaşık 14 Kasım'da bitiyor. Sonrasında onaylanan yeni işler için ne yapalım?

Option chosen by the owner (label and description verbatim):

> **Süreli istisna (Önerilen)** — MVP-4 hattı, yalnız MOD-0188/0189/0191 için 31 Aralık 2026'ya kadar istisna; tek karar kaydı. İş uzarsa kule ve kuyruk sonra kurulur.

Correction recorded after the answer: the question said "yaklaşık 14 Kasım"; the measured end of the transition is
2026-11-18 (see Facts). The correction does not change the chosen option, its scope or its end date.

## Exception (the five elements SOP r3 requires)

| element | value |
|---|---|
| Control Tower | MVP-4 coordination tower (the owner's Claude Code session acting for the MVP-4 developer lane, `ElikPenco` / Codex) |
| reason | MVP-4 has no registered task source; its remaining work (estimated 5–8 weeks) continues past the R8 transition end; a separate tower and queue is heavier than the remaining scope needs |
| scope | MOD-0188 Demand Planning, MOD-0189 MRP & Replenishment, MOD-0191 Safety Stock Optimization only, within each module's approved Module Pack version (CT-APR-01/02 still apply to every pack change); no other module, no new capability |
| start | 2026-11-11 |
| end | 2026-12-31 (inclusive) |

## What this exception does not do

- It does not approve any Module Pack, work package, PR or code. Every pack revision still needs an independent review
  and an approval row (CT-APR-01/02); every PR still needs the owner's explicit approval.
- It does not register a task source and adds no row to `ct-queue-registry.tsv`.
- It ends on 2026-12-31 without further action. If MVP-4 work continues after that date, a registered task source
  (own queue + registry row) is opened before any new work package is dispatched.
- REP-001 reports for MVP-4 after 2026-11-18 name this record instead of the line
  `Kayıtlı görev kaynağı yok (R8 geçiş süresi … bitti)`.
