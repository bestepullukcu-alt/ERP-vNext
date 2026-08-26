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
- Operational runtime implementation, production deployment, supplier qualification, validation, collections,
  seed data, jobs, migrations, and any runtime surface outside the MOD-0230/MOD-0231/MOD-0232/MOD-0234 build/test
  gates.
- MOD-0234 runtime shell, placeholder dashboard, placeholder endpoint, menu entry, fake data, fake signal, fake
  metric, or fake cohort.
- Full W-4/W-5 PV scope outside DCP-004, including MOD-0233, MOD-0235, MOD-0236, MOD-0237, MOD-0238, and MOD-0239.
- AI summarization, extraction, recommendation, or routing implementation until governed-AI prerequisites are approved.

## Domain-Level Repo Scope

**Authorized now (updated 2026-08-12 - final all-four PVG governance package; DCP-004 `approved`,
MOD-0230/MOD-0231/MOD-0232/MOD-0234 `ready-for-dev` for build/test):**

- `execution/domains/pharmacovigilance/**`
- `services/Diten.PvgService/**` - dedicated PVG service boundary, port **5011** (OD-7). MOD-0230 slice 1,
  MOD-0231 Signal Minimum Scope, MOD-0232 MedDRA Coding, and MOD-0234 Signal Management class-library
  contracts/tests only. MOD-0231, MOD-0232, and MOD-0234 must stay non-operational: no API host, controllers,
  appsettings, persistence, Mongo, repositories, Gateway route, frontend, seeds, jobs, dictionary import,
  dictionary redistribution, static MedDRA data, cache/search index, data-product stub, fake signal, fake metric,
  fake cohort, partner integration, AI, archive/void/export/delete/bulk-delete, or runtime endpoint.
- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**` plus matching JS / l10n / resource files.
- `gateway/Diten.ApiGateway/**/ocelot.json` - one MOD-0230 route family, integration-agent-owned.
  Upstream `/api/pv-case-intake-triage`, downstream `/api/v1/pv-case-intake-triage` (NET-001).
- `tests/**` for the above.

**All of the above is authorized for the build/test gate only: local, dev, and CI. Production deployment,
supplier qualification, and validation approval remain unauthorized. The MOD-0230 runtime authorization packet is
recorded as draft / not approved and does not open operational runtime.**

**Still blocked:**

- MOD-0231 operational runtime; its build/test gate is open only for non-operational class-library contracts/tests.
- MOD-0232 operational runtime - build/test gate is open only for non-operational class-library contracts/tests.
- MOD-0234 operational runtime - build/test gate is open only for no-shell non-operational class-library
  contracts/tests.
- Archive, void, export, seed data, and background jobs for MOD-0230 (out of slice 1).
- Any AI behaviour.

## Protected Paths

- `.antigravity/**` (global engineering system). `rules/ports.md` needs explicit approval before port 5011 is registered there.
- `services/**` **except `services/Diten.PvgService/**`**
- `frontend/**` **except the MOD-0230 view root and its JS / resource siblings**
- `gateway/**` except the single MOD-0230 route family
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

> MOD-0230, MOD-0231, MOD-0232, and MOD-0234 build/test preparation is governed by their ready-for-dev module packs and
> DCP-004. This domain config does not authorize operational runtime, production use, supplier qualification, or
> validation.

- **Identity and naming:** Blueprint canonical IDs/names are mandatory. Ref: [DCP-002](../../portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md).
- **Orchestration boundary:** DCP-004 stays the first-stage capability contract until user approval changes it. Ref: [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md).
- **Gateway and ports:** MOD-0230 records `Diten.PvgService` on port 5011 and one route family for the build/test
  gate only. MOD-0231, MOD-0232, and MOD-0234 record no additional port and no Gateway route.
  `.antigravity/rules/ports.md` remains protected and has not been updated here. No other PVG route or port is
  reserved. Ref:
  [.antigravity/rules/ports.md](../../../.antigravity/rules/ports.md), [.antigravity/rules/routes.md](../../../.antigravity/rules/routes.md).
- **Security and regulated data:** PHI/PII masking, row/field security, RBAC/ABAC, audit, correlation, evidence-link, and regulated error-model gates must remain fail-closed. Full owner approvals still gate operational runtime. Ref: [.antigravity/rules/security-jwt.md](../../../.antigravity/rules/security-jwt.md), [.antigravity/rules/multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md).
- **Localization and UI:** MOD-0230 tenant UI is limited to the build/test gate if executed by its pack. Other PVG UI remains unauthorized. Approved UI must follow repo localization/layout rules. Ref: [.antigravity/rules/localization-standard.md](../../../.antigravity/rules/localization-standard.md), [.antigravity/rules/views-organization.md](../../../.antigravity/rules/views-organization.md).
- **Data and persistence:** no operational collection/schema/index is authorized here. Future persistence decisions belong in approved member module packs and remain subject to the operational runtime gate. Ref: [.antigravity/rules/entity-base-template.md](../../../.antigravity/rules/entity-base-template.md), [.antigravity/rules/mongo-indexing.md](../../../.antigravity/rules/mongo-indexing.md).

## Domain Bootstrap Notes

- This PVG governance scaffold exists and contains DCP-004 member module packs for:
  - `MOD-0230 Case Intake & Triage` - `ready-for-dev` for the build/test gate only
  - `MOD-0231 Case Processing` - `ready-for-dev` for the build/test gate only; Signal Minimum Scope delivery slice
  - `MOD-0232 MedDRA Coding` - `ready-for-dev` for the build/test gate only; non-operational class-library contracts/tests
  - `MOD-0234 Signal Management` - `ready-for-dev` for the build/test gate only; no-shell non-operational class-library contracts/tests
- **2026-08-09:** DCP-004 is `approved`; MOD-0230 is `ready-for-dev` for the build/test gate.
- **2026-08-10:** MOD-0231 is `ready-for-dev` for the build/test gate only.
- **2026-08-11:** MOD-0232 and MOD-0234 are `ready-for-dev` for the build/test gate only. MOD-0234 keeps
  `shell: none` and authorizes no operational runtime, shell, placeholder dashboard, fake signal, fake metric, or
  fake cohort.
- **2026-08-12:** Final all-four PVG governance package reconciliation recorded. MOD-0230 runtime authorization
  packet remains draft / not approved. Operational runtime remains **NO-GO**; MOD-0231, MOD-0232, and MOD-0234
  operational runtime remain blocked.
- MOD-0230 consumes MOD-0019, MOD-0023, and MOD-0031 through fail-closed PVG-owned ports
  (`IPvgFieldSecurityPolicy`, `IPvgWorkflowTransitionGate`, `IPvgEvidenceLinkPort`) because those modules have no
  runtime. Ports are interface + deny default only; they must never store policy, host a workflow engine, or
  persist evidence. Detailed port contract material remains a pending support package and is not normative until
  committed.
- Operational runtime authorization for every PVG member remains **closed**.
- Seed data, background jobs, menu entries, and module-catalog work remain blocked.
