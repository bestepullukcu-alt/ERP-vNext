# Legacy Enterprise Strategy Parity Gate

Status: **OPEN / USER-ACCEPTANCE-PENDING**

Authority: Control Tower user decision — legacy and new surfaces must remain side-by-side until page-level acceptance.

Evidence rule: legacy code is evidence, not canonical authority.

## Gate rules

1. Legacy `Diten.EnterpriseStrategyService` and `EnterpriseStrategyBusinessPerformance` surfaces are not
   deleted, changed, retired or declared fully replaced by this ledger.
2. Every row requires old and new browser evidence on different ports before user acceptance.
3. `Same`, `Replaced`, `Missing`, `Intentionally Removed` and `Out of Scope` are the only dispositions.
4. A placeholder or mock surface is never proof of implemented business behavior.
5. Foreign-owned fields are mapped to typed references; their truth is not copied into the new owner.
6. Retirement remains blocked while any required row is `Missing`, lacks browser evidence, or lacks user
   acceptance.

## Page-level ledger — subdomains 1.3, 1.4 and 1.6

| Area | Old URL | Old entities / fields | Commands, lifecycle, permissions, integrations | New owner / module | New URL | Field mapping | Disposition | Migration requirement | Browser evidence |
|---|---|---|---|---|---|---|---|---|---|
| 1.3 Portfolio Management | `/management-governance/modules/mod-portfolio-management` | Registry metadata only; no authoritative portfolio aggregate evidenced | Placeholder navigation; no proven command/lifecycle/permission | MOD-0117 Portfolio | TBD | Registry label is not migrated; compare only intended capability | Replaced | No placeholder-data migration | Pending |
| 1.3 Investment Governance | `/management-governance/modules/mod-investment-governance` | Registry metadata only | Placeholder navigation; approval truth must remain MOD-0023-owned | MOD-0117 InvestmentCase; MOD-0023 approval refs | TBD | Investment identity/decision references map to typed contracts | Replaced | No placeholder-data migration; decision refs require contract mapping | Pending |
| 1.3 Benefits & Value Tracking | `/management-governance/modules/mod-benefits-value` | Registry metadata only | No proven lifecycle | MOD-0117 BenefitCommitment + MOD-0072 Outcome | TBD | Commitment stays MOD-0117; measured outcome stays MOD-0072 | Missing | Define page projection; do not copy Outcome truth | Pending |
| 1.3 Capital Allocation | `/management-governance/modules/mod-capital-allocation` | Registry metadata only | No proven lifecycle; local approval forbidden | MOD-0136 Budgeting typed reference from MOD-0117 | TBD | Financial amount/hash/reference only; Budgeting remains SoR | Out of Scope | Typed reference mapping only | Pending |
| 1.4 Initiatives list | `/management-governance/delivery-execution/initiatives` | Initiative link plus name, description, owner/sponsor, company/person/position refs, dates, wave, status, priority, complexity, benefit, KPI, budget and governance fields | Create/edit/detail, link to Objective, readiness/traceability; current frontend adapter may be mock | MOD-0117 Initiative; MOD-0352 strategy-link projection | TBD | Initiative lifecycle fields → MOD-0117; ParentObjective/Goal and contribution → MOD-0352 projection; person/org → MOD-0288 refs; budget → MOD-0136 refs | Missing | Field-by-field migration/quarantine plan required | Pending |
| 1.4 Initiative create/edit/detail | `/management-governance/delivery-execution/initiatives/new`, `/initiatives/{id}/edit`, `/initiatives/{id}` | Same rich Initiative contract | Draft/edit/readiness and strategy anchor | MOD-0117 + MOD-0352 read-only alignment | TBD | Same ownership split as list; no second lifecycle | Missing | Exact create/update payload mapping and rejected-field ledger | Pending |
| 1.4 Projects list | `/management-governance/delivery-execution/projects` | Project link plus owner/PM, sponsor, status/phase, dates, methodology, readiness/risk/health, budget/funding/cost center, approval/evidence refs | Create/detail/link; non-draft local validation exists; current frontend adapter may be mock | MOD-0117 Project; MOD-0352 link-only projection | TBD | Project lifecycle → MOD-0117; strategy lineage → MOD-0352; approval → MOD-0023; budget → MOD-0136; evidence → shared typed ref | Missing | Field-by-field migration/quarantine plan required | Pending |
| 1.4 Project create/detail | `/management-governance/delivery-execution/projects/new`, `/projects/{id}` | Same rich Project contract | Draft/non-draft lifecycle and budget governance | MOD-0117 + typed owner references | TBD | Full lifecycle stays MOD-0117; MOD-0352 cannot require foreign local fields for link-only projection | Missing | Exact payload and state-transition mapping | Pending |
| 1.4 Programs | `/management-governance/delivery-execution/programs` | Shell cards only; no authoritative legacy Program model evidenced | Navigation shell | MOD-0117 Program | TBD | No legacy data assumed | Replaced | None unless later data evidence appears | Pending |
| 1.4 Delivery Map / Dependencies | `/management-governance/delivery-execution/delivery-map` | Shell plus legacy structural/dependency evidence | Current surface does not prove critical-path or scheduling ownership | MOD-0354 structural DWS; MOD-0024 tasks; MOD-0117 delivery refs | TBD | Pure hierarchy/order/dependency → MOD-0354; task/status/due date/owner → MOD-0024/MOD-0117; FS/SS/FF/SF requires separate owner decision | Missing | Quarantine ambiguous rows; no blind migration | Pending |
| 1.6 Process Architecture | `/management-governance/modules/mod-process-architecture` | Registry placeholder only | No proven backend lifecycle | MOD-0355 | TBD | No legacy entity migration evidenced | Replaced | None | Pending |
| 1.6 Process Design & Modeling | `/management-governance/modules/mod-process-design` | Registry placeholder only | No proven backend lifecycle | MOD-0355 | TBD | New model/version/activity/control-point contract is authoritative | Replaced | None | Pending |
| 1.6 Process Performance | `/management-governance/modules/mod-process-performance` | Registry placeholder only | No proven backend lifecycle | Future owner; MOD-0355 minimal core excludes performance | TBD | No mapping yet | Missing | New bounded pack required | Pending |
| 1.6 Process Improvement | `/management-governance/modules/mod-process-improvement` | Registry placeholder only | No proven backend lifecycle | Future owner; MOD-0355 minimal core excludes improvement | TBD | No mapping yet | Missing | New bounded pack required | Pending |

## Field-level mandatory follow-up

Before browser acceptance, the Initiative and Project rows must be expanded into one row per field with:

- exact legacy source property and persistence location;
- exact new owner/module and target property or typed reference;
- null/empty rendering behavior on the new page;
- Same/Replaced/Missing/Intentionally Removed/Out of Scope disposition;
- conversion, quarantine and rollback rule;
- test evidence and user browser evidence.

In particular, owners/sponsors, person-position-company references, dates/waves, status/phase/methodology,
benefit hypotheses, risk/readiness/health/compliance, budgets/funding/cost center, approval, decision and
evidence references cannot be silently dropped.

## Browser acceptance procedure

For each row:

1. Run the legacy and new applications on different approved local ports.
2. Open the exact old URL and capture page, empty state, loaded state and permitted actions.
3. Open the exact new URL and capture the same states.
4. Verify network calls reach real backend contracts rather than mock adapters or fixture-only data.
5. Compare every visible field, action, error state and lifecycle transition.
6. Record the evidence links and user verdict in this ledger.
7. Do not retire the old route until every required row is accepted.

## Current retirement verdict

**BLOCKED.** New URLs and browser evidence are incomplete; Initiative/Project field mapping is incomplete;
Process Performance and Process Improvement are missing. Legacy retirement, deletion and “full parity
complete” are prohibited.
