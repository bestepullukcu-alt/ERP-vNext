# PVG Development Replanning Audit - 2026-08-09

## Executive Summary

Pharmacovigilance (PVG) is not ready for runtime development. The current repo state is a governance scaffold:
`DCP-004` is `draft`, all four PVG member module packs are `draft`, and the domain config explicitly blocks
service, frontend, gateway, database, seed, appsettings, menu, module-catalog, and runtime test implementation.

The correct replanning move is not to start `Diten.PvgService` or tenant UI work. The next stage should be a
controlled planning and approval sequence that closes owner decisions, W-3A0 foundation contracts, and member
pack readiness gates before implementation starts.

Recommended planning posture:

1. Keep PVG runtime blocked.
2. Promote `DCP-004` from `draft` only after owner, build/buy/partner, W-3A0, and foundation dependency decisions are recorded.
3. Prepare MOD-0230 as the first executable member only after DCP-004 is `approved` / `ready-for-execution` and
   MOD-0230 reaches `approved` / `ready-for-dev`.
4. Do not implement MOD-0231, MOD-0232, or MOD-0234 before their upstream contracts are approved.
5. Keep MOD-0234 contract-only until MOD-0004 and MOD-0063 hard gates are closed.

## Scope Audited

Audited files and surfaces:

- `AGENTS.md`
- `.antigravity/agents/orchestrator.md`
- `.antigravity/workflows/read-only-audit.md`
- `.antigravity/rules/capability-pack-standard.md`
- `.antigravity/rules/module-pack-standard.md`
- `execution/domains/pharmacovigilance/README.md`
- `execution/domains/pharmacovigilance/domain-config.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0232-meddra-coding.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0234-signal-management.md`
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/master-development-plan.md`
- `execution/delivery/platform-delivery-board.md`
- `services/`, `frontend/`, and `gateway/` PVG runtime search surface

This audit created only this report. It did not approve, stage, commit, scaffold, or modify any runtime work.

## Current PVG State

| Area | Current state | Audit judgment |
|---|---|---|
| Domain scaffold | Exists under `execution/domains/pharmacovigilance/` | Valid governance scaffold |
| Delivery Capability Pack | `DCP-004`, status `draft` | Blocks implementation |
| Member packs | MOD-0230, MOD-0231, MOD-0232, MOD-0234 all `draft` | Blocks implementation |
| Runtime service | No tracked PVG source files found | Runtime not implemented |
| Ignored PVG service folders | `services/Diten.PvgService/**` exists only as ignored `bin` / `obj` generated metadata | Confusing artifact; not executable source |
| Frontend PVG UI | No tracked PVG Razor/JS surface found | Runtime not implemented |
| Gateway PVG routes | No tracked PVG route surface found | Runtime not implemented |
| Registry / portfolio linkage | DCP exists; no PVG module rows found in registry/master plan search | Acceptable while draft, but must be reconciled before execution tracking |
| Canonical ID checks | MOD-0230, MOD-0231, MOD-0232, MOD-0234 all passed `verify_module_id.py` | Identity gate passes |

## Key Findings

### F1 - PVG is governance-only and must remain blocked

Evidence:

- `DCP-004` frontmatter is `status: draft`.
- PVG domain config says runtime service, frontend, gateway route, database, seed, appsettings, test, menu, and
  module-catalog work remain blocked.
- All member module packs are `status: draft`.

Impact:

No production code should be written for PVG until both gates are satisfied:

- DCP-004 is `approved` / `ready-for-execution`.
- The next member module pack is `approved` / `ready-for-dev`.

### F2 - DCP-004 is structurally complete but not execution-ready

`DCP-004` has the required 20 Delivery Capability Pack sections and correctly separates Delivery Capability Pack
scope from module-pack execution. It also records the correct sequence:

1. MOD-0230 Case Intake & Triage.
2. MOD-0231 Case Processing, limited to Signal Minimum Scope.
3. MOD-0232 MedDRA Coding.
4. MOD-0234 Signal Management, contract/object model/interface gates only.

Remaining open decisions block promotion:

- OD-2: W-3A0 foundation remediation scope and owner.
- OD-4: MOD-0231 Signal Minimum Scope state model.
- OD-5: MedDRA source, license, versioning, and import policy.
- OD-6: MOD-0234 data product and semantic metric minimum gates.
- OD-7: build/buy/partner strategy and integration boundary.

### F3 - Member packs are well shaped but intentionally non-executable

All four PVG module packs include the 20 required module-pack sections. The major blockers are in frontmatter and
readiness data:

- `service: TBD` on all member packs.
- `owner: TBD` on all member packs.
- `target: TBD` on all member packs.
- MOD-0234 also has `entity_base: TBD`, `form_field_count: TBD`, `shell: none`, and `golden_reference: none`.

This is acceptable as draft planning, but these values must not remain unresolved when a pack moves to
`approved` or `ready-for-dev`.

### F4 - Runtime scaffold drift exists as ignored generated folders

The folder `services/Diten.PvgService/` exists, but `git ls-files services/Diten.PvgService` returns no tracked
source files. `git status --ignored --short services/Diten.PvgService` reports it as ignored, and the contents are
`bin` / `obj` generated restore/build metadata.

Impact:

This is not a tracked service implementation, but it creates audit noise because PVG governance says no service
scaffold is authorized. Before real scaffold work starts, clean or document this ignored artifact so later agents do
not confuse generated folders with an approved service.

### F5 - PVG runtime dependencies are not ready

DCP-004 and the member packs correctly identify foundation blockers:

- MOD-0018 RBAC / ABAC authorization.
- MOD-0019 masking / row-field security.
- MOD-0021 audit trail.
- MOD-0023 workflow / inbox.
- MOD-0031 evidence linking.
- MOD-0040 / TRACE-BUNDLE semantics for identity, correlation, trace stitching, and regulated error model.
- MOD-0041 observability.
- CODESET and MedDRA source/license/version governance.
- MOD-0004 metric / semantic registry and MOD-0063 data warehouse / lakehouse for MOD-0234.
- Governed-AI controls before any AI-assisted PVG behavior.

Registry/master-plan search shows several prerequisites are planned, partial, in review, or not recorded as closed
for PVG consumption. PVG should treat each dependency as unavailable until its owner supplies an explicit,
versioned, fail-closed consumption contract.

### F6 - MOD-0234 must not become a placeholder UI or shell

MOD-0234 is intentionally `shell: none` and `golden_reference: none`. The DCP and module pack both prohibit a
Signal Management runtime shell, fake dashboard, placeholder endpoint, menu entry, or fake data.

Planning implication:

MOD-0234 should remain a contract/object-model/interface-gate workstream until upstream PVG contracts plus
MOD-0004 and MOD-0063 are closed. A visual shell would create false progress and bypass hard signal-runtime gates.

### F7 - Adjacent PV references are not PVG application delivery

The repo contains adjacent PV text in Platform/QMS document-management fixtures and Enterprise Strategy goal
template catalog entries. These are terminology or document-management support surfaces, not PVG runtime modules.

Planning implication:

Do not count QMS folder fixtures, goal template categories, or generic "Pharmacovigilance" text as progress toward
MOD-0230 through MOD-0234.

## Replanning Recommendation

### Stage 0 - Stabilize Governance Baseline

Goal: make the PVG planning surface consistent before any implementation request.

Actions:

- Keep all PVG runtime paths blocked.
- Decide whether the ignored `services/Diten.PvgService/**` generated folders should be cleaned in a separate
  maintenance task.
- Update DCP-004 review status only through explicit owner approval.
- Do not alter `services/`, `frontend/`, or `gateway/`.

Exit criteria:

- DCP-004 remains draft or moves to `under-review` with owner assigned.
- No runtime source is introduced.
- Open decisions OD-2, OD-4, OD-5, OD-6, and OD-7 have named owners and due dates.

### Stage 1 - Close DCP-004 Approval Gate

Goal: make the cross-module PVG delivery sequence executable in principle.

Required decisions:

- Confirm PVG build/buy/partner/hybrid strategy.
- Decide the dedicated PVG service boundary, or explicitly defer service materialization.
- Define W-3A0 remediation scope and owner.
- Confirm that urgent W-3 delivery slices for MOD-0231, MOD-0232, and MOD-0234 remain planning-only overrides to
  Blueprint W-4 metadata.
- Define how owner evidence will be captured and versioned.

Exit criteria:

- DCP-004 status becomes `approved` or `ready-for-execution`.
- Portfolio tracking records the approved delivery sequence.
- No member implementation starts yet.

### Stage 2 - Prepare MOD-0230 for Ready-for-Dev

Goal: make Case Intake & Triage the first executable PVG member.

Required closures:

- Assign `owner`, `target`, and concrete `service`.
- Approve runtime boundary: dedicated `Diten.PvgService` or approved partner-wrapper architecture.
- Close MOD-0230 owner-evidence templates for RBAC, masking, audit, workflow, evidence-link, TRACE-BUNDLE,
  observability/error model, retention/legal hold/archive/void, and operational runtime authorization.
- Convert option-set placeholders into owned lookup/reference contracts.
- Confirm no delete/bulk-delete and define archive/void only if retention/legal hold approval exists.

Exit criteria:

- MOD-0230 status can move to `approved` / `ready-for-dev`.
- Runtime file scope, tests, gateway owner task, and tenant UI scope are concrete.
- DCP-004 remains approved/ready-for-execution.

### Stage 3 - Implement MOD-0230 Only

Goal: deliver the minimum PV intake baseline.

Allowed only after Stage 2 exits.

Implementation constraints:

- Dedicated PVG service path only if explicitly approved.
- Tenant shell only, using `_LayoutTenantShell`.
- Compact UI pattern because `form_field_count: 16`.
- Same-origin MVC proxy profile for frontend API.
- No direct service-port calls from frontend.
- No client-supplied `TenantId`.
- No hard delete or bulk delete.
- Fail closed when RBAC, masking, audit, workflow, evidence, trace, or telemetry contracts are unavailable.

Validation required:

- Unit and integration tests for tenant isolation, missing permissions, missing dependency contracts, redaction,
  audit failure, workflow gate failure, evidence incompleteness, and no PHI/PII leak paths.
- DataTable verifier only after UI exists.
- Runtime smoke only after gateway and local runtime are approved.

### Stage 4 - Prepare MOD-0231 Signal Minimum Scope

Goal: make Case Processing executable only for the signal-minimum slice.

Required closures:

- MOD-0230 handoff contract approved and tested.
- Minimum lifecycle state model approved.
- CASE-LIFECYCLE owner contract approved.
- MOD-0234 consumption contract drafted enough to define signal handoff readiness.
- Evidence, workflow, audit, masking, TRACE-BUNDLE, and retention behavior closed.

Exit criteria:

- MOD-0231 moves to `approved` / `ready-for-dev`.
- Scope remains Signal Minimum Scope only; full W-4 Case Processing remains out of scope.

### Stage 5 - Implement MOD-0231 Only

Goal: deliver post-intake signal-minimum processing without full case-processing expansion.

Implementation constraints:

- Compact UI pattern because `form_field_count: 16`.
- Must consume, not re-own, MOD-0230 intake artifacts and triage decisions.
- Must fail closed when MOD-0230 handoff, workflow, evidence, audit, or masking contracts are unavailable.
- Must not create assumed lifecycle progress from incomplete or cross-tenant intake data.

### Stage 6 - Prepare MOD-0232 MedDRA Coding

Goal: make terminology coding executable without licensing or traceability gaps.

Required closures:

- MOD-0231 source-term candidate contract approved.
- CODESET authority approved.
- MedDRA source, license, versioning, import, storage, display, search, export, cache, and logging policy approved.
- Coding workflow, review, audit, evidence, masking, and diff/export contracts approved.

Exit criteria:

- MOD-0232 moves to `approved` / `ready-for-dev`.
- `golden_reference: slim` remains valid unless field count changes.
- No MedDRA terms are hardcoded into source, UI, fixtures, or seed data unless the approved license/source policy
  explicitly permits that exact use.

### Stage 7 - Implement MOD-0232 Only

Goal: deliver version-bound coding assignments and bounded diff/export contracts.

Implementation constraints:

- Slim UI pattern because `form_field_count: 7`.
- Assignments must bind to immutable dictionary source/version.
- Recoding/version updates must be append-only and auditable.
- Missing source contract, CODESET, MedDRA license, workflow, evidence, audit, or masking must fail closed.

### Stage 8 - Prepare MOD-0234 Contract Completion

Goal: close Signal MVP contracts without creating a runtime shell.

Required closures:

- MOD-0230, MOD-0231, and MOD-0232 consumption contracts approved.
- MOD-0004 metric IDs, threshold definitions, observation windows, and insufficient-data rules approved.
- MOD-0063 data-product contract IDs, cohort definitions, lineage, refresh/as-of semantics, quality status, and
  aggregate privacy rules approved.
- Signal review workflow, evidence set, audit, masking, TRACE-BUNDLE, observability, and regulated error-model
  contracts approved.

Exit criteria:

- MOD-0234 can move from draft to a contract-ready status only if no-shell remains explicit.
- Any later runtime pack must revise `shell`, `golden_reference`, `entity_base`, and `form_field_count` with an
  explicit UI/runtime approval.

### Stage 9 - Separate Future Runtime for MOD-0234

Goal: start Signal Management runtime only as a new approved execution slice.

This must be a separate planning event after Stage 8. It should not be bundled into the current DCP-004 first-stage
contract unless the DCP is revised and reapproved.

Exit criteria:

- New or revised approved module pack.
- Concrete runtime surface and field count.
- Data-product and metric gates closed.
- No fake data, placeholder dashboard, or shell-only progress.

## Blocker Register

| Blocker | Blocks | Required owner evidence |
|---|---|---|
| DCP-004 status `draft` | All PVG runtime | Explicit approval / ready-for-execution |
| Member pack statuses `draft` | Each member implementation | Member pack approval / ready-for-dev |
| `service: TBD` | Backend implementation | Approved PVG service or partner-wrapper boundary |
| W-3A0 scope and owner open | All runtime acceptance | REG-PV-BASE, CASE-LIFECYCLE, CODESET, REG-SIGNAL-BASE closure plan |
| RBAC/ABAC contract not owner-approved for PVG | All regulated operations | Permission keys, role/action matrix, tenant scope, deny tests |
| Masking/row-field security not owner-approved | All PHI/PII surfaces | Field sensitivity matrix and leak tests |
| AuditEvent contract not owner-approved | All regulated mutations | Event shape, redaction, outage behavior |
| Workflow/Inbox contract not owner-approved | Triage, processing, coding review, signal review | Transition gates and assignment/queue semantics |
| Evidence-Link contract not owner-approved | Triage, processing, coding, signal evidence | Link/query shape, completeness, outage behavior |
| TRACE-BUNDLE / regulated error model open | Identity, correlation, traceability | Canonical ID, correlation, reason-code, redaction tests |
| MedDRA source/license/version policy open | MOD-0232 and downstream MOD-0234 | Provider/license/version/import/display/export rules |
| MOD-0004 / MOD-0063 hard gates open | MOD-0234 runtime | Metric IDs, thresholds, data products, cohorts, lineage |
| Governed-AI controls unavailable | Any AI PVG behavior | Prompt registry, HITL, model registry, eval/drift, logging gates |

## Recommended Near-Term Backlog

1. Create a PVG governance review task for DCP-004 owner assignment and OD-2/OD-4/OD-5/OD-6/OD-7 closure.
2. Create a W-3A0 foundation remediation planning pack, if PVG is intended to move toward runtime.
3. Create owner-evidence collection tasks for MOD-0230 first, using the evidence template already present in the
   DCP and MOD-0230 pack.
4. Decide whether ignored `services/Diten.PvgService/**` generated folders should be cleaned in a separate
   maintenance task.
5. After DCP approval, reconcile portfolio/master-plan/module registry tracking for the urgent PVG sequence.

## Audit Commands and Results

- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"`:
  `OK MOD-0230: proven against Blueprint/registry.`
- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0231 --name "Case Processing"`:
  `OK MOD-0231: proven against Blueprint/registry.`
- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0232 --name "MedDRA Coding"`:
  `OK MOD-0232: proven against Blueprint/registry.`
- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0234 --name "Signal Management"`:
  `OK MOD-0234: proven against Blueprint/registry.`
- `git ls-files services/Diten.PvgService`: no tracked files.
- `git status --ignored --short services/Diten.PvgService`: ignored generated folder present.
- Source-file search found no tracked PVG runtime service, frontend, or gateway implementation.

## Final Planning Decision

Do not begin PVG implementation now. The next correct development stage is DCP-004 governance approval and
MOD-0230 readiness preparation, not runtime scaffolding.
