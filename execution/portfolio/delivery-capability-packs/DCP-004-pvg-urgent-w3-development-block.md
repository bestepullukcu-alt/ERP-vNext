---
id: DCP-004
slug: pvg-urgent-w3-development-block
name: PVG Urgent W-3 Development Block
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: pharmacovigilance
owner: TBD
branch: feature/pvg/dcp-004-urgent-w3-development-block
created: 2026-08-04
scope_stage: "Urgent W-3 first stage only"
---

# DCP-004 - PVG Urgent W-3 Development Block (Delivery Capability Pack)

> **Artifact type:** This is a **Delivery Capability Pack** (CAP-001 governance / orchestration contract).
> It is **NOT** a runtime entity, **NOT** a module pack, **NOT** a MOD-0014 runtime Capability Group,
> and **NOT** a business-capability-matrix row. It references member modules **by ID only** and never
> replaces a module pack. See [`.antigravity/rules/capability-pack-standard.md`](../../../.antigravity/rules/capability-pack-standard.md).

> **Premature-coding guard (CAP-001 §7):** Production code for any member starts only when **both** hold:
> (a) this Delivery Capability Pack is `approved` / `ready-for-execution`, **and** (b) the next member's
> module pack is `approved` / `ready-for-dev`. This draft authorizes **no** implementation.

> **User constraint:** This draft does **not** develop Urgent foundation remediation W-3A0, does **not**
> include next-stage W-4/W-5 modules outside the requested urgent slices, and does **not** start
> MOD-0234 runtime implementation as a shell. W-3A0 dependencies are recorded as external prerequisites
> and production blockers, not waived.

---

## 1. Identity and status

| Field | Value |
|-------|-------|
| ID | DCP-004 |
| Slug | pvg-urgent-w3-development-block |
| Name | PVG Urgent W-3 Development Block |
| Type | Delivery Capability Pack (CAP-001) |
| Status | `draft` |
| Owner domain | `pharmacovigilance` approved 2026-08-04; governance scaffold now exists at `execution/domains/pharmacovigilance/` |
| Owner | TBD |
| Authoring branch | `feature/pvg/dcp-004-urgent-w3-development-block` (branch short code `pvg` approved by OD-1 on 2026-08-04) |
| Created | 2026-08-04 |
| Standard | CAP-001 - `.antigravity/rules/capability-pack-standard.md` |
| Authority note | This pack does not alter AGENTS.md §1 authority hierarchy. Each member module still requires its own module pack. |

**Governance scaffold / draft pack reconciliation (2026-08-04):**

- PVG governance scaffold exists at `execution/domains/pharmacovigilance/`.
- Draft member module packs now exist for:
  - MOD-0230 Case Intake & Triage.
  - MOD-0231 Case Processing.
  - MOD-0232 MedDRA Coding.
  - MOD-0234 Signal Management.
- These draft artifacts do not change this DCP status, do not approve execution, and do not authorize runtime work.

**DCP-002 identity proof (2026-08-04):**

| ID | Canonical Blueprint module name | Requested delivery label | Preflight result |
|----|---------------------------------|--------------------------|------------------|
| MOD-0230 | Case Intake & Triage | Urgent W-3A | `OK MOD-0230: proven against Blueprint/registry.` |
| MOD-0231 | Case Processing | Urgent W-3B, delivery slice: Signal Minimum Scope | `OK MOD-0231: proven against Blueprint/registry.` |
| MOD-0232 | MedDRA Coding | Urgent W-3C | `OK MOD-0232: proven against Blueprint/registry.` |
| MOD-0234 | Signal Management | Urgent W-3D | `OK MOD-0234: proven against Blueprint/registry.` |

**Blueprint wave note:** Blueprint rows place MOD-0230 in W-3 and MOD-0231/MOD-0232/MOD-0234 in W-4. This
Delivery Capability Pack records a user-requested urgent W-3 delivery block for **limited slices only**. It does
not rename modules, rewrite Blueprint canonical wave metadata, or authorize full W-4 scope.

## 2. Business outcome

Establish a first-stage Pharmacovigilance (PVG/PV) urgent delivery block that can safely define the minimum
case-intake, signal-minimum case-processing, MedDRA coding, and Signal MVP contracts needed for regulated
life-sciences safety operations, while keeping production runtime blocked until the deferred W-3A0 foundation
dependencies are completed.

The intended outcome is a governance-ready sequence of future module-pack authoring work, not a runtime build.

## 3. Problem statement

The requested PVG work spans multiple regulated safety modules and relies on shared foundations that are not
yet proven as production-ready in this repo: workflow/inbox, audit, row/field security and masking, evidence
linking, case lifecycle traceability, dictionary versioning, data-product contracts, semantic metrics, OTel,
correlation, and regulated error-model conventions.

A normal single module pack would overload the boundary because MOD-0230, MOD-0231, MOD-0232, and MOD-0234
must be sequenced together and because MOD-0234 depends on data-product foundations outside ordinary CRUD scope.
This DCP captures the first-stage boundary and explicitly prevents accidental W-4/W-5 expansion or premature
shell/runtime implementation.

## 4. Capability boundary

**Inside this first-stage DCP:**

- MOD-0230 Case Intake & Triage contract and future module-pack preparation path.
- MOD-0231 Case Processing canonical module, limited to the delivery slice **Signal Minimum Scope**.
- MOD-0232 MedDRA Coding terminology/coding contract and future module-pack preparation path.
- MOD-0234 Signal Management Signal MVP contract, workflow boundary, object model, and interface gates.
- External prerequisite and blocker map for deferred W-3A0 foundation remediation.

**Outside this first-stage DCP:**

- Any production service, frontend, gateway, database, seed, runtime shell, or migration implementation.
- W-3A0 foundation remediation development.
- Full W-4 Case Processing, full W-4 MedDRA workbench, full W-4 Signal Management runtime shell, and all W-4/W-5 PV modules not explicitly listed.
- AI automation beyond recording governed-AI prerequisites and blockers.

## 5. Member modules and follow-ups

| # | Member | Canonical name | Requested stage | Delivery slice / MVP scope | This DCP deliverable |
|---|--------|----------------|-----------------|-----------------------------|----------------------|
| 1 | MOD-0230 | Case Intake & Triage | Urgent W-3A | Intake baseline, triage state/routing decisions, evidence-pack contract boundary | Contract and future module-pack authoring gate |
| 2 | MOD-0231 | Case Processing | Urgent W-3B | **Signal Minimum Scope** only; canonical name remains Case Processing | Signal-minimum slice contract and future module-pack authoring gate |
| 3 | MOD-0232 | MedDRA Coding | Urgent W-3C | Terminology/coding contract, dictionary version binding, coding audit/export contract | Terminology/coding contract and future module-pack authoring gate |
| 4 | MOD-0234 | Signal Management | Urgent W-3D | Signal MVP contract, review workflow boundary, object model, interface gates | Contract-only planning; **no runtime shell implementation** |

Future module packs must use the exact canonical names above. MOD-0231 must express "Signal Minimum Scope" as
a delivery slice or MVP scope only, never as the canonical module name.

## 6. Ownership map

| Object / concern | System of record / owner | Status in this DCP |
|------------------|--------------------------|--------------------|
| Safety Case intake record | MOD-0230 Case Intake & Triage | Draft module pack exists; runtime blocked |
| Intake artifact and triage routing decision | MOD-0230 Case Intake & Triage | Draft module pack exists; runtime blocked |
| Safety Case master record and lifecycle state | MOD-0231 Case Processing | Draft module pack exists for Signal Minimum Scope; runtime blocked |
| Coded terms on Safety Case and MedDRA assignments | MOD-0232 MedDRA Coding | Draft module pack exists; CODESET and dictionary/version contracts required first |
| MedDRA dictionary version references | MOD-0232 MedDRA Coding plus external terminology source governance | External terminology dependency; not waived |
| Signal hypothesis, evaluation, review decision, linked evidence | MOD-0234 Signal Management | Contract/object-model planning only; no shell/runtime |
| Audit event storage | MOD-0021 Audit Trail Service | External prerequisite; repo registry says ready-for-dev / implemented evidence, final consumption gate still required |
| Workflow/inbox/review engine | MOD-0023 Workflow Designer | External prerequisite; registry says review / planned |
| Row/field security and data masking | MOD-0019 Data Masking & Row/Field Security | External prerequisite; no repo pack found during authoring |
| Evidence links and evidence-pack assembly | MOD-0031 Evidence Linking Service | External prerequisite; registry says review / planned |
| Data warehouse/lakehouse | MOD-0063 Data Warehouse / Lakehouse | Hard MOD-0234 Signal MVP runtime gate; contract IDs, cohorts, lineage, and refresh/as-of semantics required |
| Metric and semantic IDs | MOD-0004 Metric & Semantic Registry | Hard MOD-0234 Signal MVP runtime gate; semantic metric IDs and threshold definitions required |
| Governed AI controls | MOD-0068 Prompt Registry, MOD-0069 HITL, MOD-0066 Model Registry, MOD-0067 Eval/Drift, MOD-0041 Logging | AI-related runtime behavior blocked until controls exist |

## 7. Dependency graph

```text
W-3A0 Foundation Remediation (deferred, external prerequisite)
  |-- REG-PV-BASE:
  |     SSO + RBAC/ABAC
  |     PHI/PII masking hooks (MOD-0019)
  |     AuditEvent v1 (MOD-0021)
  |     Workflow/Inbox v1 (MOD-0023)
  |     Evidence-Link (MOD-0031)
  |     OTel + Correlation-ID + Error Model
  |
  |-- CASE-LIFECYCLE:
  |     case lifecycle state machine
  |     audit-grade workflow trace
  |     evidence-pack assembly contract
  |
  |-- CODESET:
  |     MedDRA dictionary version binding
  |     code audit trail
  |     coding diff/export contract
  |
  |-- REG-SIGNAL-BASE:
  |     data product contract IDs (MOD-0063)
  |     metric semantic IDs (MOD-0004)
  |     review workflow + evidence pack + OTel
  |
  v
MOD-0230 Case Intake & Triage
  v
MOD-0231 Case Processing (delivery slice: Signal Minimum Scope)
  v
MOD-0232 MedDRA Coding
  v
MOD-0234 Signal Management (Signal MVP contract/object model/interface gates only)
```

Production runtime acceptance for every downstream member remains blocked wherever the W-3A0 dependency is
required and not completed.

## 8. Ordered delivery sequence

1. **MOD-0230 contract and module pack** - define Case Intake & Triage scope, tenant/security posture, intake artifacts, triage decisions, routing boundary, and evidence-pack contract. Module pack starts as `draft`; no runtime code.
2. **MOD-0231 signal-minimum slice contract and module pack** - canonical module name is Case Processing; delivery slice is **Signal Minimum Scope**. Define only the safety-case fields, lifecycle states, assessments, and trace outputs needed by downstream signal work. Module pack starts as `draft`; no runtime code.
3. **MOD-0232 terminology/coding contract and module pack** - define MedDRA version binding, coded-term assignment model, coding audit trail, diff/export contract, and dictionary-source governance. Module pack starts as `draft`; no runtime code.
4. **MOD-0234 Signal MVP contract, workflow, object model, and interface gates** - define signal hypothesis/evaluation/review-decision objects, data-product and metric interface gates, workflow handoff, evidence links, and telemetry contract. This step does **not** create a runtime shell, UI, service, or gateway route.

## 9. Prerequisites

1. This Delivery Capability Pack moves from `draft` to `approved` / `ready-for-execution` by explicit user approval.
2. PVG domain ownership is decided; the governance scaffold now exists, and runtime remains blocked by DCP/member-pack gates.
3. W-3A0 foundation remediation is completed or each missing dependency is explicitly closed by a production-grade external contract. This DCP does **not** waive those dependencies.
4. Each member module pack is created separately, starts as `draft`, and later reaches `approved` / `ready-for-dev` before any implementation.
5. DCP-002 preflight remains mandatory for any future child/follow-up identity. No new MOD or FU number may be invented.
6. Build/buy/partner decision for PVG is resolved before service boundaries are materialized; Blueprint records these modules as Buy/Partner.

## 10. Architecture decisions

- **Domain status:** `pharmacovigilance` is the approved owner domain. The governance scaffold now exists, but this
  DCP still authorizes no runtime service, frontend, gateway route, collection, seed, or permission implementation.
- **Runtime creation guard:** No `services/Diten.*`, frontend, gateway, route, collection, seed, or permission implementation is authorized by this draft.
- **Canonical naming:** MOD-0230, MOD-0231, MOD-0232, and MOD-0234 use Blueprint names exactly. Delivery-stage labels and slices are planning metadata only.
- **Security baseline:** REG-PV-BASE is mandatory for any runtime member because these modules handle regulated safety data and PHI/PII-sensitive records.
- **Case lifecycle:** MOD-0231 Signal Minimum Scope may define only the minimum lifecycle states and trace outputs required by MOD-0234; full W-4 Case Processing remains out of scope.
- **MedDRA governance:** MOD-0232 cannot hardcode dictionary terms as static UI data. Dictionary version, source, licensing, update cadence, and audit/export behavior must be decided before runtime.
- **Signal MVP:** MOD-0234 starts as contract/workflow/object-model/interface-gate planning only. It must not be implemented as a UI shell or placeholder runtime module.
- **Signal data-product / metric gates:** MOD-0004 and MOD-0063 are hard MOD-0234 Signal MVP runtime gates, not
  downstream-only follow-ups. Signal runtime cannot start without approved semantic metric IDs, thresholds,
  data-product contract IDs, cohort definitions, lineage, and refresh/as-of semantics.
- **AI:** Blueprint marks the members as Governed-AI / High risk. AI extraction, recommendation, summarization, or routing stays blocked until Prompt Registry, HITL, Model Registry, Eval/Drift, and logging gates exist.

## 11. Scope

**First stage only:**

- Urgent W-3A: MOD-0230 Case Intake & Triage.
- Urgent W-3B: MOD-0231 Case Processing, limited to Signal Minimum Scope.
- Urgent W-3C: MOD-0232 MedDRA Coding.
- Urgent W-3D: MOD-0234 Signal Management, Signal MVP contracts/gates only.

This DCP began as documentation and governance preparation. Later governance reconciliation added the PVG scaffold
plus draft member packs, while still authorizing no service, frontend, gateway, database, seed, appsettings, test,
menu, module-catalog, or runtime work.

## 12. Explicit exclusions

| Exclusion | Status | Reason |
|-----------|--------|--------|
| Urgent foundation remediation W-3A0 development | Excluded from this stage | User explicitly excluded it; dependencies are blockers, not waived |
| Any production runtime implementation | Excluded | CAP-001 and user instruction prohibit code/service/frontend/gateway changes |
| MOD-0234 runtime shell | Excluded | User explicitly prohibited starting MOD-0234 as a shell |
| Full MOD-0231 W-4 Case Processing | Excluded except Signal Minimum Scope | User requested only the urgent signal-minimum slice |
| Full MOD-0232 W-4 MedDRA workbench | Excluded beyond terminology/coding contract planning | Requires dictionary/source/version governance first |
| Full MOD-0234 W-4 Signal runtime | Excluded beyond Signal MVP contract/object model/interface gates | Requires data warehouse/lakehouse and semantic metric prerequisites |
| MOD-0233 Reporting & Submissions | Excluded | Next-stage W-4 PV module; not requested |
| MOD-0235 PV Quality | Excluded | Next-stage W-4 PV module; not requested |
| MOD-0236 Dossier/Submissions Management | Excluded | Next-stage W-4 PV module; not requested |
| MOD-0237 Variations & Renewals | Excluded | Next-stage W-4 PV module; not requested |
| MOD-0238 Labeling Lifecycle | Excluded | Next-stage W-4 PV module; not requested |
| MOD-0239 Country Requirements Matrix | Excluded | Next-stage W-4 PV module; not requested |
| W-5 PV or adjacent regulated life-sciences modules | Excluded | User limited this stage to the four listed urgent members |
| AI summarization/extraction/recommendation/routing implementation | Excluded / blocked | Governed-AI gates are external prerequisites |

## 13. Governance drift risks

1. **Wave-label drift:** MOD-0231, MOD-0232, and MOD-0234 are Blueprint W-4 modules. This DCP uses urgent W-3 labels only as delivery-slice sequencing metadata. Future portfolio updates must not silently rewrite Blueprint canonical wave data.
2. **Canonical-name drift:** "Signal Minimum Scope" is not a module name and must not appear as MOD-0231 frontmatter `name`.
3. **Foundation waiver drift:** W-3A0 is excluded from development, but its dependencies remain production blockers. Member packs must not mark runtime acceptance ready by citing this DCP alone.
4. **Shell drift for MOD-0234:** A placeholder Signal Management UI/service would create false progress and bypass data-product gates. Contract-only means contract-only.
5. **Regulated-data risk:** PVG data may include PHI/PII. Runtime scope without masking, row/field security, audit, retention/evidence, and RBAC/ABAC gates is not acceptable.
6. **Dictionary drift:** MedDRA coding without version binding and audit/export traceability would fail regulated traceability expectations.
7. **AI risk drift:** Blueprint AI risk is High. Any AI helper without Prompt Registry, HITL, Model Registry, Eval/Drift, and logging is out of bounds.

## 14. Resolved notes and open questions

1. OD-1 is resolved: owner domain is `pharmacovigilance`, and branch short code is `pvg`.
2. OD-3 is resolved: urgent W-3 delivery-slice override is approved for planning while Blueprint W-4 canonical wave metadata remains unchanged.
3. What exact scope belongs to W-3A0 foundation remediation, and which team owns closing it?
4. For MOD-0231 Signal Minimum Scope, what minimum case lifecycle states must exist before MOD-0234 can consume them?
5. What MedDRA source, license, version-update cadence, and import/validation approach will be accepted for MOD-0232?
6. What data product contract and semantic metric IDs are the minimum viable gates for MOD-0234?
7. Is PVG implementation intended as buy/partner integration, internal build, or hybrid wrapper?

## 15. Gate criteria

- **DCP approval gate:** This DCP remains `draft` and review-ready for governance discussion, but it is not
  `approved` / `ready-for-execution` until explicit user approval.
- **Domain gate:** PVG owner domain and branch short code are decided (`pharmacovigilance` / `pvg`); runtime remains blocked by DCP/member-pack gates.
- **Member module-pack gate:** each member receives a separate module pack with status `draft` first, then user-approved `approved` / `ready-for-dev` before implementation.
- **DCP-002 gate:** each canonical member keeps exact Blueprint ID/name proof; any future FU/child identity must pass `verify_module_id.py`.
- **W-3A0 gate:** production runtime acceptance is blocked until REG-PV-BASE and the relevant CASE-LIFECYCLE, CODESET, or REG-SIGNAL-BASE prerequisites are complete.
- **MOD-0234 no-shell gate:** no Signal Management UI shell, service shell, route shell, menu entry, seed, fake dashboard, or placeholder endpoint may be created under this DCP.
- **Regulated-data gate:** PHI/PII masking hooks, RBAC/ABAC, audit, correlation, error-model, and evidence-link behavior must be testable before runtime.
- **AI gate:** AI behavior remains blocked until governed-AI prerequisites are explicitly available and accepted.

## 15A. Pending approval decision checklist

This checklist records the decisions required for user review. All items remain pending unless separately approved
by the user. Recording this checklist does not change DCP-004 status, approve ready-for-execution, promote any
member module pack, authorize runtime, close OD-2, or close OD-7.

### DCP-level

- [ ] Approve DCP-004 from `draft` to `approved` / `ready-for-execution`.
- [ ] Confirm DCP-004 authorizes governance sequencing only until each member pack separately reaches
      `approved` / `ready-for-dev`.
- [ ] Confirm W-3A0 remains excluded from this delivery stage but is not waived as a production blocker.
- [ ] Confirm urgent W-3 delivery-slice override remains planning metadata only and does not rewrite Blueprint wave
      metadata.
- [ ] Confirm MOD-0231 uses `Signal Minimum Scope` only as a delivery slice, never as the canonical module name.
- [ ] Confirm MOD-0234 remains no-shell / no-placeholder / no-runtime for this first-stage DCP.
- [ ] Close OD-2 by approving W-3A0 foundation remediation scope and owner, or approving explicit
      production-grade external contract substitution.
- [ ] Close OD-7 by approving the PVG build/buy/partner strategy and integration boundary.

### MOD-0230-level

- [ ] Approve MOD-0230 Case Intake & Triage from `draft` to `approved` / `ready-for-dev`.
- [ ] Confirm recorded draft decisions: `shell: tenant`, `entity_base: EntityBase`, `golden_reference: compact`,
      and `form_field_count: 16`.
- [ ] Approve the 16 create/edit fields, required/optional classification, and PHI/PII sensitivity classes.
- [ ] Approve field-level masking, row/field access, audit payload, evidence-link, and fail-closed behavior for each
      intake field.
- [ ] Approve MOD-0230 actor roles and permission matrix with MOD-0018 / AuthService seed-grant ownership.
- [ ] Approve triage/routing states, route targets, and Workflow/Inbox behavior.
- [ ] Approve the MOD-0230 handoff contract consumed by MOD-0231.
- [ ] Approve no delete and no bulk-delete; archive/void remains unavailable until retention/legal-hold approval.

### Runtime authorization

- [ ] Approve the W-3A0 path: close the foundation work directly or approve production-grade external contract
      substitution for REG-PV-BASE dependencies.
- [ ] Approve OD-7 build/buy/partner boundary: internal Diten build, partner system, or hybrid partner-aware Diten
      control wrapper.
- [ ] Approve service boundary: dedicated `Diten.PvgService` or another explicit approved boundary.
- [ ] Explicitly authorize the `Diten.PvgService` scaffold before any service folder, port, DI, appsettings, route,
      collection, seed, job, or test is created.
- [ ] Approve future Gateway ownership and route work assignment to integration-agent.
- [ ] Approve frontend route/profile only after runtime is authorized: tenant shell, same-origin MVC proxy, and no
      direct service-port calls.
- [ ] Approve retention/legal-hold/archive policy: archive reason, actor, UTC timestamp, correlation ID, AuditEvent,
      legal-hold block behavior, and export/read visibility.
- [ ] Approve MOD-0018 RBAC/permissions contract: canonical keys, actor context, tenant authorization, and
      seed/grant ownership.
- [ ] Approve MOD-0019 masking/row-field security contract: sensitivity vocabulary, masking/omit/deny behavior, and
      fail-closed policy.
- [ ] Approve MOD-0021 AuditEvent v1 contract: event shape, payload allow-list, redaction, and failure behavior.
- [ ] Approve MOD-0023 Workflow/Inbox v1 contract: transition gates, inbox routing, assignment, and
      blocked/unavailable behavior.
- [ ] Approve MOD-0031 Evidence-Link contract: object references, evidence completeness, link/query API, and no fake
      evidence fallback.
- [ ] Approve Blueprint MOD-0040 / TRACE-BUNDLE contract: canonical IDs, external IDs, `X-Correlation-Id`, trace
      stitching, and regulated error model.
- [ ] Confirm MOD-0288 is used only if routing or assignment consumes organization/person/position references.

### Still-excluded work

- [ ] W-3A0 remediation implementation remains excluded unless separately approved.
- [ ] MOD-0231, MOD-0232, and MOD-0234 runtime implementation remains excluded until their own packs are
      `approved` / `ready-for-dev`.
- [ ] MOD-0234 shell, dashboard, placeholder endpoint, menu entry, fake data, service scaffold, and route remain
      excluded.
- [ ] MOD-0004 and MOD-0063 are not MOD-0230 runtime blockers unless MOD-0230 emits analytics/data-product outputs;
      they remain hard MOD-0234 Signal MVP gates.
- [ ] AI extraction, summarization, recommendation, routing, or scoring remains excluded until governed-AI controls
      are approved.

## 16. Acceptance criteria

**Acceptance for this draft DCP:**

1. The artifact is created under `execution/portfolio/delivery-capability-packs/` with `status: draft`.
2. The four requested member IDs use exact Blueprint canonical module names.
3. MOD-0231 records "Signal Minimum Scope" only as a delivery slice / MVP scope.
4. W-3A0 foundation remediation is excluded from this stage and recorded as an external prerequisite / production blocker.
5. MOD-0234 is limited to Signal MVP contract, workflow boundary, object model, and interface gates; no runtime shell is authorized.
6. Explicit exclusions list W-4/W-5 PV modules outside the requested urgent scope.

**Runtime acceptance gates for future member packs (currently blocked where W-3A0 applies):**

| Member | Runtime acceptance status | Blocking prerequisites |
|--------|---------------------------|------------------------|
| MOD-0230 | BLOCKED until W-3A0 foundations close or production-grade external contracts are explicitly accepted | REG-PV-BASE: workflow/inbox, audit, masking, RBAC/ABAC, evidence-link, OTel/correlation/error model |
| MOD-0231 Signal Minimum Scope | BLOCKED until MOD-0230 contract + W-3A0 foundations close | Case Intake & Triage, Evidence Linking, CASE-LIFECYCLE |
| MOD-0232 | BLOCKED until MOD-0231 signal-minimum contract + CODESET gates close | Case Processing minimum scope, MedDRA source/version/license, coding audit/export |
| MOD-0234 | BLOCKED for runtime; contract-only allowed after upstream contracts | Hard MOD-0063 Data Warehouse / Lakehouse and MOD-0004 Metric & Semantic Registry gates, review workflow, evidence pack, OTel |

## 17. Downstream business-module impacts

- **MOD-0019 Data Masking & Row/Field Security:** becomes a hard prerequisite for PVG runtime.
- **MOD-0021 Audit Trail Service:** must support audit-grade PV case and coding traces.
- **MOD-0023 Workflow Designer:** must provide Workflow/Inbox v1 or an approved production-grade equivalent before regulated case routing/review runtime.
- **MOD-0031 Evidence Linking Service:** must provide object-to-evidence and evidence-pack contracts before case processing and signal review runtime.
- **MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse:** are hard MOD-0234 Signal MVP
  runtime gates and must supply approved semantic metric IDs, threshold/measure definitions, data-product contract
  IDs, cohort definitions, lineage, refresh/as-of semantics, and access controls before any Signal MVP runtime.
- **MOD-0068/MOD-0069/MOD-0066/MOD-0067:** become blockers for any AI-assisted PVG behavior.
- Future PVG W-4 modules must consume the contracts created by this block rather than redefining Safety Case, coded terms, signals, or evidence-pack ownership.

## 18. Open decisions

| # | Decision | Impact | Owner | Status |
|---|----------|--------|-------|--------|
| OD-1 | PVG owner domain folder and short branch code | Governance scaffold now exists; this DCP still authorizes no runtime service/frontend/gateway work | User / Enterprise Architect | APPROVED / RESOLVED 2026-08-04 - owner domain `pharmacovigilance`, branch short code `pvg` |
| OD-2 | W-3A0 foundation remediation scope and owner | Blocks all runtime acceptance gates | User / Platform / PVG owner | OPEN |
| OD-3 | Urgent W-3 delivery-slice override for Blueprint W-4 members | Prevents planning-vs-Blueprint ambiguity | User / Portfolio governance | APPROVED / RESOLVED 2026-08-04 - MOD-0231/MOD-0232/MOD-0234 may be planned as urgent W-3 delivery slices while Blueprint W-4 metadata remains unchanged |
| OD-4 | MOD-0231 Signal Minimum Scope state model | Blocks MOD-0231 and MOD-0234 contract completion | PVG product owner | OPEN |
| OD-5 | MedDRA source, license, versioning, and import policy | Blocks MOD-0232 runtime readiness | PVG product owner / Compliance | OPEN |
| OD-6 | MOD-0234 data product and semantic metric minimum gates | Blocks Signal MVP runtime readiness | Data / PVG architecture | OPEN |
| OD-7 | Build/buy/partner strategy and integration boundary | Blocks service/runtime architecture | User / Enterprise Architect | OPEN |

## 19. Future follow-ups

- PVG domain config and governance scaffold now exist; keep runtime service/frontend/gateway work blocked.
- Draft member module packs now exist for MOD-0230, MOD-0231, MOD-0232, and MOD-0234; keep all member runtime work
  blocked until DCP/member-pack approval gates are satisfied.
- Prepare a separate W-3A0 foundation remediation pack if the user chooses to plan that blocker later.
- Add portfolio/master-plan linkage only after the urgent W-3 override and owner-domain decision are approved.
- Revisit full W-4 PV modules (MOD-0231 full scope, MOD-0232 full workbench, MOD-0233, MOD-0234 runtime, MOD-0235 to MOD-0239) after this first-stage block is reviewed.
- Define governed-AI PVG enablement separately after Prompt Registry, HITL, Model Registry, Eval/Drift, and logging foundations exist.

## 20. Audit and reconciliation notes

- 2026-08-04: Read `AGENTS.md`, `.antigravity/agents/orchestrator.md`, `.antigravity/workflows/prepare-capability-pack.md`, `.antigravity/rules/capability-pack-standard.md`, `.antigravity/rules/module-pack-standard.md`, `execution/portfolio/master-development-plan.md`, `execution/registries/module-id-registry.md`, and DCP-002/DCP-003 references.
- 2026-08-04: Verified requested MOD IDs with DCP-002 gate:
  - `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"` -> OK.
  - `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0231 --name "Case Processing"` -> OK.
  - `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0232 --name "MedDRA Coding"` -> OK.
  - `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0234 --name "Signal Management"` -> OK.
- 2026-08-04: Blueprint rows inspected for MOD-0230/MOD-0231/MOD-0232/MOD-0234. Result: MOD-0230 is Blueprint W-3; MOD-0231, MOD-0232, and MOD-0234 are Blueprint W-4, treated here only as user-requested urgent W-3 delivery slices.
- 2026-08-04: At original DCP authoring time, repository search found no existing `execution/domains/pharmacovigilance/` domain and no existing member module packs for MOD-0230/MOD-0231/MOD-0232/MOD-0234. This was true at authoring time and is superseded by later governance reconciliation.
- 2026-08-04: Governance approvals recorded: OD-1 approved owner domain `pharmacovigilance` and branch short code `pvg`; OD-3 approved urgent W-3 delivery-slice planning for MOD-0231/MOD-0232/MOD-0234 while preserving Blueprint canonical W-4 metadata. That update created no domain folders, no member module packs, and no runtime authorization.
- 2026-08-04: Later governance reconciliation created the PVG governance scaffold and draft member module packs for
  MOD-0230 Case Intake & Triage, MOD-0231 Case Processing, MOD-0232 MedDRA Coding, and MOD-0234 Signal Management.
  All remain draft/planning artifacts and authorize no service, frontend, gateway, runtime, appsettings, seed, menu,
  or test changes.
- 2026-08-04: Cross-pack audit reconciliation strengthened MOD-0004 and MOD-0063 wording as hard MOD-0234 Signal
  MVP runtime gates.
- 2026-08-07: MOD-0230 readiness audit recorded that DCP-004 remains `draft` / review-ready only, not
  `ready-for-execution`. OD-2 remains open for W-3A0 owner/scope/foundation closure, and OD-7 remains open for
  build/buy/partner strategy and integration boundary. MOD-0230 is the first PVG candidate, but runtime remains
  blocked by DCP/member-pack approval gates, W-3A0 or accepted production-grade external contracts, service
  boundary approval, retention/legal-hold, archive/void policy, and concrete interface contracts for MOD-0018,
  MOD-0019, MOD-0021, MOD-0023, MOD-0031, and Blueprint MOD-0040 / TRACE-BUNDLE. This note authorizes no service
  scaffold, frontend, Gateway, route, collection, seed, appsettings, menu, job, or runtime code.
- Reconciliation: implementation-phase results will be added here only after this DCP is approved and future member module packs are executed.
