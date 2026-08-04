# Pharmacovigilance (PVG) - Domain Config

> Bu dosya domain'in **sinirlarini ve kararlarini** tanimlar. Engineering NASIL kurallari
> [.antigravity/rules/](../../../.antigravity/rules/)'dadir; capability-level sozlesme
> [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md)'tedir.

## Purpose

Pharmacovigilance domain'i, regulated life-sciences safety operations icin Safety Case intake/triage,
signal-minimum case processing, MedDRA coding, and Signal Management contract boundaries sahiplenir.
Ilk kapsam yalnizca DCP-004 tarafindan tanimlanan urgent W-3 first-stage governance block'tur.

## In-Scope Modules

> Canonical identity and names come from the Blueprint and DCP-002 preflight. Wave/status details remain in
> portfolio-level governance; burada sadece PVG sahiplik listesi tutulur.

**First-stage DCP-004 members:**

- MOD-0230 - Case Intake & Triage
- MOD-0231 - Case Processing; urgent W-3 delivery slice: Signal Minimum Scope
- MOD-0232 - MedDRA Coding
- MOD-0234 - Signal Management; Signal MVP contract/object model/workflow/interface gates only

## Out-of-Scope

- W-3A0 foundation remediation development; not waived, still a production blocker.
- Runtime implementation: service scaffold, frontend screens, gateway routes, collections, seed data, jobs, migrations.
- MOD-0234 runtime shell, placeholder dashboard, placeholder endpoint, menu entry, or fake data.
- Full W-4/W-5 PV scope outside DCP-004, including MOD-0233, MOD-0235, MOD-0236, MOD-0237, MOD-0238, and MOD-0239.
- AI summarization, extraction, recommendation, or routing implementation until governed-AI prerequisites are approved.

## Domain-Level Repo Scope

**Authorized now (governance only):**

- `execution/domains/pharmacovigilance/**`
- Draft member module packs under `execution/domains/pharmacovigilance/module-packs/**` for DCP-004 planning only.

**Future only, blocked until an approved Delivery Capability Pack and approved/ready-for-dev member module pack:**

- PVG runtime service path - exact service/deployment boundary TBD.
- PVG frontend paths - exact shell and route surface TBD by member module pack.
- Gateway route changes - only through integration-agent after member module approval.

## Protected Paths

- `.antigravity/**` (global engineering system)
- `services/**` - no PVG runtime service scaffold is authorized by this governance scaffold
- `frontend/**` - no PVG UI is authorized by this governance scaffold
- `gateway/**` - no gateway route is authorized by this governance scaffold
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`
- Other domain governance folders except by explicit user request

## Ownership Boundaries

- **MOD-0230 Case Intake & Triage:** owns Safety Case intake records, intake artifacts, triage state, and routing decision boundary for the first PVG sequence.
- **MOD-0231 Case Processing:** canonical module name remains Case Processing. In DCP-004 first stage, only the Signal Minimum Scope delivery slice may be planned.
- **MOD-0232 MedDRA Coding:** owns coded-term assignment boundaries and MedDRA dictionary-version binding contracts for Safety Case terms. It cannot hardcode dictionary data as local UI/static data.
- **MOD-0234 Signal Management:** owns signal hypothesis, evaluation, review decision, linked evidence boundaries for Signal MVP planning. Runtime shell implementation is explicitly blocked.
- **W-3A0 foundation dependencies:** REG-PV-BASE, CASE-LIFECYCLE, CODESET, and REG-SIGNAL-BASE dependencies remain external prerequisites and production blockers where applicable.
- **Evidence, workflow, audit, masking, data product, semantic metric, and governed-AI controls:** PVG consumes approved contracts from their owning modules; this domain does not reimplement those foundations inside member modules.

## Runtime Decisions

> Tumu gelecekteki member module pack'lerinde somutlastirilir. Bu scaffold runtime uygulama yetkisi vermez.

- **Identity and naming:** Blueprint canonical IDs/names are mandatory. Ref: [DCP-002](../../portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md).
- **Orchestration boundary:** DCP-004 stays the first-stage capability contract until user approval changes it. Ref: [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md).
- **Gateway and ports:** no route or port is reserved by this scaffold. Ref: [.antigravity/rules/ports.md](../../../.antigravity/rules/ports.md), [.antigravity/rules/routes.md](../../../.antigravity/rules/routes.md).
- **Security and regulated data:** PHI/PII masking, row/field security, RBAC/ABAC, audit, correlation, evidence-link, and regulated error-model gates must be resolved before runtime. Ref: [.antigravity/rules/security-jwt.md](../../../.antigravity/rules/security-jwt.md), [.antigravity/rules/multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md).
- **Localization and UI:** future tenant-facing UI, if approved, must follow the repo localization/layout rules; no UI is authorized here. Ref: [.antigravity/rules/localization-standard.md](../../../.antigravity/rules/localization-standard.md), [.antigravity/rules/views-organization.md](../../../.antigravity/rules/views-organization.md).
- **Data and persistence:** no collection/schema/index is authorized here. Future persistence decisions belong in approved member module packs. Ref: [.antigravity/rules/entity-base-template.md](../../../.antigravity/rules/entity-base-template.md), [.antigravity/rules/mongo-indexing.md](../../../.antigravity/rules/mongo-indexing.md).

## Domain Bootstrap Notes

- This PVG governance scaffold exists and contains draft member module packs for:
  - `MOD-0230 Case Intake & Triage`
  - `MOD-0231 Case Processing`
  - `MOD-0232 MedDRA Coding`
  - `MOD-0234 Signal Management`
- All member packs remain governance/planning artifacts until explicitly approved.
- DCP-004 remains `status: draft`; this domain scaffold and its draft member packs do not approve execution.
- Runtime service, frontend, gateway route, database, seed, appsettings, test, menu, and module-catalog work remains
  blocked until DCP-004 and the relevant member module pack pass their approval gates.
