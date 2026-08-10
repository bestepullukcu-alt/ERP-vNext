# Pharmacovigilance (PVG)

**Kisaltma:** `PVG`
**Kisa kod (branch):** `pvg`
**Delivery Capability Pack:** [DCP-004 - PVG Urgent W-3 Development Block](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md)
**Runtime status (2026-08-09):** DCP-004 is `approved` for sequencing. **MOD-0230 Case Intake & Triage is
`ready-for-dev` for the build/test gate only**, because that status is already recorded in the current MOD-0230
and DCP-004 governance docs. Service `Diten.PvgService` (port 5011), tenant UI, and one gateway route family are
limited to local / dev / CI build-test preparation. **Operational runtime, production, supplier qualification,
and validation remain unauthorized** for every member. MOD-0231, MOD-0232, and MOD-0234 remain `draft`. Seed
data, background jobs, menu entries, module-catalog work, archive/void, export, and all AI behaviour remain blocked.
Any detailed fast-track plan remains a pending support package and is not normative until committed.

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
- Operational runtime, production deployment, supplier qualification, validation, database collection, seed, job,
  migration, or any runtime surface outside the MOD-0230 build/test gate.
- Member module-pack execution or ready-for-dev promotion without explicit approval.
- MOD-0234 runtime shell or placeholder UI/service.
- Full W-4/W-5 PV modules outside DCP-004 first-stage scope.

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md) - sinirlar, repo scope, protected paths, ownership boundaries, runtime decision links
- MOD-0230 slice-1 work-pack details - pending support package; not normative until committed
- [module-packs/](module-packs/) - member module packs; MOD-0230 is build/test `ready-for-dev`, while the other
  DCP-004 members remain `draft`:
  - MOD-0230 Case Intake & Triage
  - MOD-0231 Case Processing
  - MOD-0232 MedDRA Coding
  - MOD-0234 Signal Management
- [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md) - approved
  sequencing contract; operational runtime remains closed

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
3. `/prepare-module-pack` is requested for the next member that still needs pack authoring or reconciliation.
4. New or unreconciled module packs start as `status: draft`.
5. Local build/test work starts only after the DCP and the member module pack pass the build/test gate. Operational
   runtime starts only after the separate operational runtime gate is approved.
