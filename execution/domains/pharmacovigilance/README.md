# Pharmacovigilance (PVG)

**Kisaltma:** `PVG`
**Kisa kod (branch):** `pvg`
**Delivery Capability Pack:** [DCP-004 - PVG Urgent W-3 Development Block](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md)
**Runtime status (2026-08-12):** DCP-004 is `approved` for sequencing. This branch is reconciled as the final
all-four PVG governance package for MOD-0230, MOD-0231, MOD-0232, and MOD-0234 build/test sequencing. **MOD-0230 Case Intake & Triage,
MOD-0231 Case Processing, MOD-0232 MedDRA Coding, and MOD-0234 Signal Management are `ready-for-dev` for the
build/test gate only.** MOD-0231 is limited to the Signal Minimum Scope delivery slice, MOD-0232 is limited to
non-operational MedDRA Coding class-library contracts/tests under `Diten.PvgService`, and MOD-0234 is limited to
no-shell Signal MVP class-library contracts/tests under `Diten.PvgService`.
The MOD-0230 runtime authorization packet remains **draft / not approved**.
MOD-0230 service `Diten.PvgService` (port 5011), tenant UI, and one gateway route family remain limited to local /
dev / CI build-test preparation. **Operational runtime, production, supplier qualification, and validation remain
unauthorized** for every member. MOD-0234 keeps `shell: none` and `golden_reference: none`; no runtime shell,
placeholder dashboard, fake signal, fake metric, fake cohort, Gateway route, frontend, appsettings, data product
stub, seed, job, collection, migration, partner integration, export/delete/bulk-delete, or AI behavior is
authorized. MedDRA dictionary import, dictionary redistribution, static MedDRA data, cache/search index, seed data,
background jobs, menu entries, module-catalog work, archive/void, export, delete, bulk-delete, and all AI behaviour
remain blocked.
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
  migration, or any runtime surface outside the member build/test gates.
- Member operational runtime execution without explicit approval.
- MOD-0234 runtime shell, placeholder UI/service, fake signal, fake metric, or fake cohort.
- Full W-4/W-5 PV modules outside DCP-004 first-stage scope.

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md) - sinirlar, repo scope, protected paths, ownership boundaries, runtime decision links
- MOD-0230 slice-1 work-pack details - pending support package; not normative until committed
- [module-packs/](module-packs/) - member module packs; MOD-0230, MOD-0231, MOD-0232, and MOD-0234 are build/test
  `ready-for-dev`:
  - MOD-0230 Case Intake & Triage
  - MOD-0231 Case Processing - Signal Minimum Scope delivery slice only
  - MOD-0232 MedDRA Coding - non-operational class-library contracts/tests only
  - MOD-0234 Signal Management - no-shell Signal MVP class-library contracts/tests only
- [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md) - approved
  sequencing contract and final all-four PVG governance package; operational runtime remains closed

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
