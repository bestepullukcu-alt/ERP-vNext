# Pharmacovigilance (PVG)

**Kisaltma:** `PVG`
**Kisa kod (branch):** `pvg`
**Delivery Capability Pack:** [DCP-004 - PVG Urgent W-3 Development Block](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md)
**Runtime status:** Governance scaffold exists. DCP-004 remains `status: draft`; runtime service, frontend,
gateway route, database, seed, appsettings, test, menu, and module-catalog implementation remain blocked.

## Is Tanimi

Pharmacovigilance domain'i; regulated life-sciences safety operations icin Safety Case intake, triage,
case-processing minimum signal slice, MedDRA coding, and Signal Management contract boundaries sahiplenir.
Ilk governance kapsami DCP-004 tarafindan sinirlandirilan urgent W-3 delivery block'tur.

## Kapsam (Yuksek Seviye)

- MOD-0230 Case Intake & Triage
- MOD-0231 Case Processing - urgent W-3 delivery slice: Signal Minimum Scope
- MOD-0232 MedDRA Coding
- MOD-0234 Signal Management - Signal MVP contract, object model, workflow boundary, and interface gates only

## Kapsam Disi

- W-3A0 foundation remediation development; dependency olarak kalir, build kapsamina girmez.
- Runtime service scaffold, frontend screen, gateway route, database collection, seed, or migration.
- Member module-pack execution or ready-for-dev promotion without explicit approval.
- MOD-0234 runtime shell or placeholder UI/service.
- Full W-4/W-5 PV modules outside DCP-004 first-stage scope.

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md) - sinirlar, repo scope, protected paths, ownership boundaries, runtime decision links
- [module-packs/](module-packs/) - draft member module packs for:
  - MOD-0230 Case Intake & Triage
  - MOD-0231 Case Processing
  - MOD-0232 MedDRA Coding
  - MOD-0234 Signal Management
- [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md) - capability-level draft orchestration contract

## Otorite Hiyerarsisi (Yeni Modul Yazarken)

1. **Module Pack** - [module-packs/{ID}.md](module-packs/)
2. **Domain Config** - [domain-config.md](domain-config.md)
3. **AGENTS.md** - repo kontrati
4. **`.antigravity/rules/`** - engineering NASIL
5. **`execution/portfolio/master-development-plan.md`** - high-level wave plan and module inventory

## Yeni Modul Eklerken

Tam akis icin: [docs/agent-usage-guide.md](../../../docs/agent-usage-guide.md). Kisa hali:

1. DCP-004 scope, blockers, and ordered delivery sequence are reviewed.
2. DCP-002 preflight is run with the exact Blueprint ID and canonical name.
3. `/prepare-module-pack` is requested for the next approved member, starting with `MOD-0230 Case Intake & Triage`.
4. The generated module pack starts as `status: draft`.
5. Runtime work starts only after the Delivery Capability Pack and the member module pack pass their approval gates.
