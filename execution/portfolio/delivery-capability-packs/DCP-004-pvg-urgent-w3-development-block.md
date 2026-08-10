---
id: DCP-004
slug: pvg-urgent-w3-development-block
name: PVG Urgent W-3 Development Block
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: pharmacovigilance
owner: NY (ny@gmgroup.ch)
approved: 2026-08-09
branch: feature/pvg/dcp-004-urgent-w3-development-block
created: 2026-08-04
scope_stage: "Urgent W-3 first stage only"
decision_records: "pending support package - not normative until committed"
---

# DCP-004 - PVG Urgent W-3 Development Block (Delivery Capability Pack)

> **Artifact type:** This is a **Delivery Capability Pack** (CAP-001 governance / orchestration contract).
> It is **NOT** a runtime entity, **NOT** a module pack, **NOT** a MOD-0014 runtime Capability Group,
> and **NOT** a business-capability-matrix row. It references member modules **by ID only** and never
> replaces a module pack. See [`.antigravity/rules/capability-pack-standard.md`](../../../.antigravity/rules/capability-pack-standard.md).

> **Premature-coding guard (CAP-001 §7):** Production code for any member starts only when **both** hold:
> (a) this Delivery Capability Pack is `approved` / `ready-for-execution`, **and** (b) the next member's
> module pack is `approved` / `ready-for-dev`.
>
> **2026-08-09 status change:** condition (a) is now satisfied - this pack is `approved`. Condition (b) is
> satisfied for **MOD-0230 only**. MOD-0231, MOD-0232, and MOD-0234 remain `draft` and authorize no
> implementation. Member authorization is further split into a **build/test gate** and an **operational
> runtime gate**; see §10 "Build gate vs operational runtime gate".

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
| Status | `approved` (2026-08-09, NY) |
| Owner domain | `pharmacovigilance` approved 2026-08-04; governance scaffold now exists at `execution/domains/pharmacovigilance/` |
| Owner | NY (ny@gmgroup.ch) - PVG system owner / Enterprise Architect |
| Authoring branch | `feature/pvg/dcp-004-urgent-w3-development-block` (branch short code `pvg` approved by OD-1 on 2026-08-04) |
| Created | 2026-08-04 |
| Standard | CAP-001 - `.antigravity/rules/capability-pack-standard.md` |
| Authority note | This pack does not alter AGENTS.md §1 authority hierarchy. Each member module still requires its own module pack. |
| Decision records | Pending support package - OD-2, OD-4, OD-5, OD-6, and OD-7 are summarized in this DCP and are not normative from a separate file until committed |
| Execution plan | Pending support package - not normative until committed |

**Governance scaffold / member pack reconciliation:**

- PVG governance scaffold exists at `execution/domains/pharmacovigilance/`.
- Member module packs now exist for:
  - MOD-0230 Case Intake & Triage - promoted on 2026-08-09 to `ready-for-dev` for the build/test gate only.
  - MOD-0231 Case Processing.
  - MOD-0232 MedDRA Coding.
  - MOD-0234 Signal Management.
- MOD-0231, MOD-0232, and MOD-0234 remain `draft` and authorize no implementation. MOD-0230 build/test gate
  authorization does not authorize operational runtime.

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

The intended outcome is a governance-ready sequence of module-pack work. After the 2026-08-09 reconciliation,
MOD-0230 may proceed only through its local/dev/CI build-test gate; operational runtime remains closed.

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
| Safety Case intake record | MOD-0230 Case Intake & Triage | Build/test gate open; operational runtime blocked |
| Intake artifact and triage routing decision | MOD-0230 Case Intake & Triage | Build/test gate open; operational runtime blocked |
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

1. **MOD-0230 contract and module pack** - define Case Intake & Triage scope, tenant/security posture, intake artifacts, triage decisions, routing boundary, and evidence-pack contract. As of 2026-08-09, this pack is `ready-for-dev` for the build/test gate only; operational runtime remains closed.
2. **MOD-0231 signal-minimum slice contract and module pack** - canonical module name is Case Processing; delivery slice is **Signal Minimum Scope**. Define only the safety-case fields, lifecycle states, assessments, and trace outputs needed by downstream signal work. Module pack starts as `draft`; no runtime code.
3. **MOD-0232 terminology/coding contract and module pack** - define MedDRA version binding, coded-term assignment model, coding audit trail, diff/export contract, and dictionary-source governance. Module pack starts as `draft`; no runtime code.
4. **MOD-0234 Signal MVP contract, workflow, object model, and interface gates** - define signal hypothesis/evaluation/review-decision objects, data-product and metric interface gates, workflow handoff, evidence links, and telemetry contract. This step does **not** create a runtime shell, UI, service, or gateway route.

## 9. Prerequisites

1. This Delivery Capability Pack is `approved` / `ready-for-execution` by explicit user approval.
2. PVG domain ownership is decided; the governance scaffold now exists, and runtime remains blocked by DCP/member-pack gates.
3. W-3A0-Lite is satisfied for MOD-0230 build/test through fail-closed PVG-owned consumption ports. W-3A0-Full
   remains closed for operational runtime. This DCP does **not** waive those dependencies.
4. Each member module pack is created separately. MOD-0230 is `ready-for-dev` for the build/test gate only;
   MOD-0231, MOD-0232, and MOD-0234 remain `draft`.
5. DCP-002 preflight remains mandatory for any future child/follow-up identity. No new MOD or FU number may be invented.
6. Build/buy/partner decision for PVG is resolved before service boundaries are materialized; Blueprint records these modules as Buy/Partner.

## 10. Architecture decisions

- **Domain status:** `pharmacovigilance` is the approved owner domain. The governance scaffold now exists. MOD-0230
  alone has a build/test gate; operational runtime remains unauthorized for every member.
- **Runtime creation guard:** The MOD-0230 build/test gate is local/dev/CI only. No operational runtime, production
  deployment, supplier qualification, validation, collection, seed, job, permission seed, archive/void, export,
  delete, bulk-delete, or AI implementation is authorized.
- **Canonical naming:** MOD-0230, MOD-0231, MOD-0232, and MOD-0234 use Blueprint names exactly. Delivery-stage labels and slices are planning metadata only.
- **Security baseline:** REG-PV-BASE is mandatory for any runtime member because these modules handle regulated safety data and PHI/PII-sensitive records.
- **Case lifecycle:** MOD-0231 Signal Minimum Scope may define only the minimum lifecycle states and trace outputs required by MOD-0234; full W-4 Case Processing remains out of scope.
- **MedDRA governance:** MOD-0232 cannot hardcode dictionary terms as static UI data. Dictionary version, source, licensing, update cadence, and audit/export behavior must be decided before runtime.
- **Signal MVP:** MOD-0234 starts as contract/workflow/object-model/interface-gate planning only. It must not be implemented as a UI shell or placeholder runtime module.
- **Signal data-product / metric gates:** MOD-0004 and MOD-0063 are hard MOD-0234 Signal MVP runtime gates, not
  downstream-only follow-ups. Signal runtime cannot start without approved semantic metric IDs, thresholds,
  data-product contract IDs, cohort definitions, lineage, and refresh/as-of semantics.
- **AI:** Blueprint marks the members as Governed-AI / High risk. AI extraction, recommendation, summarization, or routing stays blocked until Prompt Registry, HITL, Model Registry, Eval/Drift, and logging gates exist.

### Build gate vs operational runtime gate (added 2026-08-09)

Member authorization is split into two gates that move at different speeds. This split is not new: MOD-0230's
owner-evidence table already carried it in the caveat *"Local non-operational scaffold only; not operational
runtime, not production use, not supplier qualification, not validation approval."* It is now explicit at DCP level.

| Gate | Condition | Authorizes | Current state |
|---|---|---|---|
| **Build / test gate** | DCP-004 `approved` + member pack `approved` / `ready-for-dev` | Backend, tests, gateway route, tenant UI, in local / dev / CI only | **Open for MOD-0230** as of 2026-08-09 |
| **Operational runtime gate** | Build gate + real MOD-0019, MOD-0023, MOD-0031 + named retention / legal-hold owner | Production, supplier qualification, validation | **Closed for every member** - unchanged |

### W-3A0-Lite consumption ports (added 2026-08-09, per OD-2)

Rather than waiting on three unbuilt platform modules, MOD-0230 defines three consumption ports inside its own
boundary with deny-by-default adapters: `IPvgFieldSecurityPolicy` (MOD-0019), `IPvgWorkflowTransitionGate`
(MOD-0023), `IPvgEvidenceLinkPort` (MOD-0031). Every behaviour these dependencies must exhibit for MOD-0230 is a
denial behaviour, so a deny-by-default adapter satisfies the build gate exactly.

Binding constraints: a port is an interface plus a deny default and nothing else. It stores no policy data, hosts
no workflow engine, and persists no evidence. Non-production adapters are configuration-gated and throw at
startup in a Production environment. When a real module ships, one DI registration line changes.
Detailed port contract material remains a pending support package and is not normative until committed.

This waives nothing. The `PVG-MOD0230-FieldSecurity-Contract v1`, `PVG-MOD0230-WorkflowTransitionGate-v1`, and
`PVG-MOD0230-EvidenceLink-v1` evidence rows remain **unapproved** and continue to block operational runtime.

## 11. Scope

**First stage only:**

- Urgent W-3A: MOD-0230 Case Intake & Triage.
- Urgent W-3B: MOD-0231 Case Processing, limited to Signal Minimum Scope.
- Urgent W-3C: MOD-0232 MedDRA Coding.
- Urgent W-3D: MOD-0234 Signal Management, Signal MVP contracts/gates only.

This DCP began as documentation and governance preparation. Later governance reconciliation added the PVG scaffold
and member packs. MOD-0230 alone now has a local/dev/CI build-test gate. MOD-0231, MOD-0232, and MOD-0234 remain
draft. Operational runtime, database, seed, appsettings, jobs, menu, module-catalog, production, supplier
qualification, and validation remain unauthorized.

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
3. OD-2 is resolved (2026-08-09): W-3A0 splits into W-3A0-Lite (PVG-owned consumption ports, gates build/test) and W-3A0-Full (real MOD-0019 / MOD-0023 / MOD-0031 plus a retention / legal-hold owner, owned by `platform-shared-services`, gates operational runtime).
4. OD-4 is resolved (2026-08-09): the MOD-0231 minimum lifecycle is `Received` -> `Triaged` -> `InProcessing` -> `AssessmentComplete` -> `ReadyForSignal` -> `Closed`, plus terminal `Rejected` / `Duplicate` and non-linear `OnHold`.
5. OD-5 is resolved (2026-08-09): MedDRA is licensed only through the MSSO; twice-yearly releases (1 March / 1 September; current V29.0 from 2026-03-01); versioned immutable snapshots; no MedDRA data in source, fixtures, seed, or test data. MOD-0232 stays `draft` until the executed licence is recorded.
6. OD-6 is resolved as an explicit deferral (2026-08-09): MOD-0004 and MOD-0063 registry rows exist for planning traceability only; both remain unowned / missing module packs / without runtime, so no executable hard gate can yet be defined against them. Entry conditions are recorded in this DCP. MOD-0234 stays contract-only.
7. OD-7 is resolved (2026-08-09): hybrid, partner-aware internal control wrapper in a dedicated `Diten.PvgService` on port 5011. MOD-0230 and the MOD-0231 signal-minimum slice are internal build; MOD-0232 and MOD-0234 remain buy/partner leaning.

Full decision-record text remains a pending support package and is not normative until committed.

## 15. Gate criteria

- **DCP approval gate:** CLOSED 2026-08-09. Approved by NY (ny@gmgroup.ch). OD-2, OD-4, OD-5, OD-6, and OD-7 are summarized in this DCP. Full decision-record text remains a pending support package and is not normative until committed.
- **Domain gate:** PVG owner domain and branch short code are decided (`pharmacovigilance` / `pvg`); runtime remains blocked by DCP/member-pack gates.
- **Member module-pack gate:** each member receives a separate module pack with status `draft` first, then user-approved `approved` / `ready-for-dev` before implementation.
- **DCP-002 gate:** each canonical member keeps exact Blueprint ID/name proof; any future FU/child identity must pass `verify_module_id.py`.
- **W-3A0 gate:** split per OD-2. **W-3A0-Lite** (PVG consumption ports with deny-by-default adapters) gates build/test and is satisfiable by PVG itself. **W-3A0-Full** (real MOD-0019, MOD-0023, MOD-0031, plus a named retention / legal-hold owner) gates operational runtime and remains **closed**. Production runtime acceptance is blocked until W-3A0-Full and the relevant CASE-LIFECYCLE, CODESET, or REG-SIGNAL-BASE prerequisites are complete.
- **MOD-0234 no-shell gate:** no Signal Management UI shell, service shell, route shell, menu entry, seed, fake dashboard, or placeholder endpoint may be created under this DCP.
- **Regulated-data gate:** PHI/PII masking hooks, RBAC/ABAC, audit, correlation, error-model, and evidence-link behavior must be testable before runtime.
- **AI gate:** AI behavior remains blocked until governed-AI prerequisites are explicitly available and accepted.

### MOD-0230 Owner-Approval Evidence Intake Template

This template records the evidence required to convert a governance-only packet or approval gate into an
owner-approved MOD-0230 input. Every approval requires owner/team, approver, approval date, evidence artifact/link,
approved version, fail-closed proof, required test evidence, and caveats/exclusions. Empty or placeholder values mean
the approval remains blocked. Recording this template does not approve any owner decision, does not move MOD-0230 to
`ready-for-dev`, and does not authorize operational runtime.

| Approval | Owner/team | Approver | Approval date | Evidence artifact/link | Approved version | Fail-closed proof | Required test evidence | Caveats / exclusions | Readiness decision |
|---|---|---|---|---|---|---|---|---|---|
| `PVG-MOD0230-RBAC-Contract v1` | MOD-0018 / AuthService / Platform access governance | Missing | Missing | Missing | Missing | Missing - required proof: deny on missing actor, tenant, permission, scope, seed/grant catalog, or auth context; cross-tenant reads return 404/empty and mutations/exports deny. | Missing - required tests: role/action allow-deny matrix, missing-permission denial, cross-tenant denial, platform/partner/tenant actor behavior, seed/grant ownership proof, and no delete/bulk-delete surface. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-FieldSecurity-Contract v1` | MOD-0019 masking / row-field security owner | Missing | Missing | Missing | Missing | Missing - required proof: deny or omit/mask when field policy is missing or unavailable; raw PHI/PII/free text cannot enter UI/API output, logs, traces, metrics, audit metadata, validation errors, or exports. | Missing - required tests: all 16 fields across list/detail/create/update/export/audit, missing-policy denial, raw-value leak scans, and cross-tenant checks. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-AuditEvent-v1` | MOD-0021 AuditEvent / audit owner | Missing | Missing | Missing | Missing | Missing - required proof: no unaudited regulated mutation succeeds; audit outage blocks the mutation or uses only an owner-approved durable outbox path; payload redaction happens before persistence/export. | Missing - required tests: create, update, triage, route, archive, export, denial/failure audit events, outbox outage behavior, redaction, and correlation propagation. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-WorkflowTransitionGate-v1` | MOD-0023 Workflow/Inbox owner | Missing | Missing | Missing | Missing | Missing - required proof: gate runs before commit; blocked, unavailable, missing queue, missing assignment policy, missing reason code, tenant/object mismatch, or unapproved `NotApplicable` prevents lifecycle mutation. | Missing - required tests: gate-before-commit, allowed/blocked/not-applicable, outage, missing queue/assignment policy, cross-tenant denial, reason-code validation, correlation propagation, and no-PHI workflow event/log/error checks. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-EvidenceLink-v1` | MOD-0031 Evidence-Link owner | Missing | Missing | Missing | Missing | Missing - required proof: missing required evidence or unavailable Evidence-Link blocks triage, route, archive/void, or handoff unless MOD-0031 owner approves a durable pending-evidence state; no fake pack or duplicated content. | Missing - required tests: link/query shape, completeness, outage, cross-tenant denial, link/unlink audit, correlation propagation, workflow handoff blocked on missing evidence, no duplicated document content, and no-PHI evidence-content checks. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-TraceBundle-v1` | Enterprise Architecture / platform trace authority for Blueprint MOD-0040 / TRACE-BUNDLE | Missing | Missing | Missing | Missing | Missing - required proof: no untraceable regulated mutation succeeds; external IDs are non-authoritative; duplicate or mismatch ambiguity rejects, conflicts safely, or routes only through owner-approved durable duplicate review/outbox. | Missing - required tests: server-generated canonical IDs, client-supplied ID rejection, external-ref non-authority, duplicate/mismatch handling, missing/valid/invalid `X-Correlation-Id`, and trace propagation through intake, audit, workflow, evidence, error, and outbox/events. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-ObservabilityErrorModel-v1` | MOD-0041 / Ops / platform observability and regulated error-model owner | Missing | Missing | Missing | Missing | Missing - required proof: raw PHI/PII/free text never enters logs, traces, metrics, validation errors, or error payloads; missing approved telemetry/error policy blocks regulated mutation or uses an explicitly approved degraded path. | Missing - required tests: trace/log/error redaction, correlation propagation across UI/API/service/audit/workflow/evidence/outbox, invalid/missing correlation behavior, safe metric labels, and telemetry outage behavior. | Packet recorded only; not owner-approved. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` | Compliance / legal-hold / records-retention owner, with MOD-0019, MOD-0021, trace, workflow, and evidence owner alignment where applicable | Missing | Missing | Missing | Missing | Missing - required proof: archive/void remains unavailable before approval; legal hold blocks archive and void; missing retention, legal-hold, masking, audit, trace, workflow, or evidence policy denies or blocks with a regulated safe error and no fallback mutation. | Missing - required tests: archive/void blocked before approval, blocked under legal hold, denied for unauthorized actors, denied or masked when MOD-0019 is unavailable, blocked or queued only if MOD-0021 approves durable audit behavior, required metadata captured on allowed paths, evidence/trace references preserved, and hard delete/bulk delete absent. | Packet recorded only; not owner-approved. No market-specific PV retention period is accepted. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| MOD-0230 operational runtime authorization | User / PVG system owner / Enterprise Architecture, with platform operations and validation approval where required | Missing | Missing | Missing | Missing | Missing - required proof: approved runtime scope, service boundary, port/topology, appsettings policy, tenant isolation, no client `TenantId`, safe telemetry/errors/audit metadata, no delete/bulk-delete, archive/void absent or approved, and all exposed-surface contracts fail closed. | Missing - required tests: startup/config fail-closed checks, no port/appsettings/Gateway/frontend/collection/seed/job without approval, tenant isolation, no-PHI telemetry/errors, RBAC/masking/audit/workflow/evidence/trace outage behavior, and phase-gate evidence for every authorized surface. | Local non-operational scaffold only; not operational runtime, not production use, not supplier qualification, not validation approval. | [ ] Operational runtime authorized |

### MOD-0230 External Owner-Evidence Submission Checklist

External reviewers must answer against the exact MOD-0230 approval artifact named below. A design basis, draft
packet, generic platform capability, or informal note can support review, but it cannot approve the MOD-0230
contract unless the owner explicitly approves the named artifact/version and supplies fail-closed and test evidence.

| Required approval artifact | Who must approve | Evidence reviewers must supply | What cannot count as approval | Exact condition to mark approved |
|---|---|---|---|---|
| `PVG-MOD0230-RBAC-Contract v1` | MOD-0018 / AuthService / Platform access governance | Approver, approval date, artifact/link, approved version, RBAC deny proof, role/action allow-deny tests, cross-tenant denial, platform/partner/tenant actor behavior, seed/grant ownership proof, no delete/bulk-delete proof | Packet recorded only, draft matrix, informal email, unversioned notes, partial permission list | All evidence fields are supplied and owner explicitly approves MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-FieldSecurity-Contract v1` | MOD-0019 masking / row-field security owner | Approver, approval date, artifact/link, version, all 16 field rules, missing-policy fail-closed proof, raw PHI/PII/free-text leak tests, list/detail/create/update/export/audit tests | Sensitivity vocabulary alone, draft field matrix, generic masking standard, untested policy | Owner-approved artifact proves allow/mask/omit/deny behavior and tests pass for every required surface |
| `PVG-MOD0230-AuditEvent-v1` | MOD-0021 AuditEvent / audit owner | Approver, approval date, artifact/link, version, append/event shape, outage fail-closed/outbox proof, redaction proof, create/update/triage/route/archive/export/denial tests | Generic audit capability, unapproved event names, audit notes without failure-mode proof | Owner approves the PVG event contract and required audit/outage/redaction tests are supplied |
| `PVG-MOD0230-WorkflowTransitionGate-v1` | MOD-0023 Workflow/Inbox owner | Approver, approval date, artifact/link, version, gate-before-commit proof, queue/assignment/reason-code behavior, outage and blocked transition tests, cross-tenant denial, no-PHI workflow event/log/error proof | Draft workflow states, queue names without owner approval, untested `NotApplicable` behavior | Owner approves the transition gate contract and all blocked/unavailable/missing-policy paths fail closed |
| `PVG-MOD0230-EvidenceLink-v1` | MOD-0031 Evidence-Link owner | Approver, approval date, artifact/link, version, link/query shape, completeness rules, outage behavior, cross-tenant denial, link/unlink audit, no duplicated content, no-PHI evidence-content tests | Fake evidence pack, duplicated documents, live URL references as authority, generic evidence-link notes | Owner approves the EvidenceLink contract and evidence completeness/outage behavior is test-proven |
| `PVG-MOD0230-TraceBundle-v1` | Enterprise Architecture / platform trace authority for Blueprint MOD-0040 / TRACE-BUNDLE | Approver, approval date, artifact/link, version, canonical ID policy, external ID non-authority proof, duplicate/mismatch handling, correlation header behavior, trace propagation tests | External IDs as primary keys, unversioned trace notes, generic correlation convention only | Owner approves trace identity/correlation behavior and tests prove no untraceable regulated mutation |
| `PVG-MOD0230-ObservabilityErrorModel-v1` | MOD-0041 / Ops / platform observability and regulated error-model owner | Approver, approval date, artifact/link, version, safe reason-code taxonomy, no-PHI telemetry/error policy, redaction tests, correlation propagation, safe metric labels, telemetry outage behavior | Raw exception logging, generic OTel policy, untested error envelope, PHI/PII-bearing logs/traces | Owner approves error/telemetry model and tests prove no PHI/PII/free-text leakage |
| `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` | Compliance / legal-hold / records-retention owner, aligned with MOD-0019, MOD-0021, trace, workflow, evidence owners as applicable | Approver, approval date, artifact/link, version, retention/legal-hold/archive/void rules, archive/void blocked-before-approval proof, legal-hold block proof, no hard delete/bulk-delete proof | Market-generic retention assumption, draft retention matrix, unapproved archive/void wording, missing legal-hold proof | Owner approves class-specific policy and tests prove archive/void/delete paths fail closed as required |
| MOD-0230 operational runtime authorization | User / PVG system owner / Enterprise Architecture, with platform operations and validation approval where required | Approver, approval date, artifact/link, version, approved runtime scope, service boundary, port/topology, appsettings policy, tenant isolation, safe telemetry/errors/audit metadata, no delete/bulk-delete, archive/void status, all exposed-surface fail-closed tests | Local scaffold approval, docs-only planning approval, supplier paper, draft service boundary, untested startup/config behavior | Explicit operational runtime authorization is granted and the row can be marked approved |

## 16. Acceptance criteria

**Acceptance for this DCP sequencing contract:**

1. The artifact is created under `execution/portfolio/delivery-capability-packs/` with `status: approved`.
2. The four requested member IDs use exact Blueprint canonical module names.
3. MOD-0231 records "Signal Minimum Scope" only as a delivery slice / MVP scope.
4. W-3A0 foundation remediation is excluded from this stage and recorded as an external prerequisite / production blocker.
5. MOD-0234 is limited to Signal MVP contract, workflow boundary, object model, and interface gates; no runtime shell is authorized.
6. Explicit exclusions list W-4/W-5 PV modules outside the requested urgent scope.

**Runtime acceptance gates for future member packs (currently blocked where W-3A0 applies):**

| Member | Runtime acceptance status | Blocking prerequisites |
|--------|---------------------------|------------------------|
| MOD-0230 | Operational runtime BLOCKED until W-3A0-Full foundations close | REG-PV-BASE: real MOD-0019, MOD-0023, MOD-0031, retention/legal-hold owner, OTel/correlation/error model evidence |
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
| OD-2 | W-3A0 foundation remediation scope and owner | Split: W-3A0-Lite gates build/test; W-3A0-Full gates operational runtime | NY (PVG) for Lite; `platform-shared-services` for Full | **APPROVED / RESOLVED 2026-08-09** - see decision record |
| OD-3 | Urgent W-3 delivery-slice override for Blueprint W-4 members | Prevents planning-vs-Blueprint ambiguity | User / Portfolio governance | APPROVED / RESOLVED 2026-08-04 - MOD-0231/MOD-0232/MOD-0234 may be planned as urgent W-3 delivery slices while Blueprint W-4 metadata remains unchanged |
| OD-4 | MOD-0231 Signal Minimum Scope state model | Unblocks MOD-0231 pack completion; MOD-0231 stays `draft` | NY (PVG product owner) | **APPROVED / RESOLVED 2026-08-09** - 6 linear + 2 terminal + 1 non-linear state |
| OD-5 | MedDRA source, license, versioning, and import policy | MOD-0232 stays `draft` until the executed MSSO licence is recorded | NY / Legal | **APPROVED / RESOLVED 2026-08-09** - MSSO only; procurement starts Day 1 (longest external lead time) |
| OD-6 | MOD-0234 data product and semantic metric minimum gates | MOD-0234 stays contract-only; MOD-0004 and MOD-0063 registry rows exist for planning traceability only, with no owner-approved pack or runtime | NY / Data architecture | **DEFERRED WITH ENTRY CONDITIONS 2026-08-09** - a hard gate cannot be executed against rows that are still unowned / missing module packs / without runtime |
| OD-7 | Build/buy/partner strategy and integration boundary | Releases MOD-0230 `service` frontmatter | NY (Enterprise Architect) | **APPROVED / RESOLVED 2026-08-09** - hybrid; dedicated `Diten.PvgService` on port 5011 |

## 19. Future follow-ups

- PVG domain config and governance scaffold now exist; keep operational runtime blocked. MOD-0230 service,
  frontend, and gateway work is limited to the local/dev/CI build-test gate.
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
  MOD-0230 was later promoted to build/test `ready-for-dev`; MOD-0231, MOD-0232, and MOD-0234 remain
  draft/planning artifacts and authorize no service, frontend, gateway, runtime, appsettings, seed, menu, or test
  changes.
- 2026-08-04: Cross-pack audit reconciliation strengthened MOD-0004 and MOD-0063 wording as hard MOD-0234 Signal
  MVP runtime gates.
- 2026-08-09: Deep audit findings were reviewed as pending support package material. They are not normative from a separate audit artifact until committed; blocker findings summarized in this DCP are upheld.
- 2026-08-09: Verified blocker map produced. Five of eight REG-PV-BASE legs are already merged, tested code in this repo: MOD-0018 authorization (`Diten.Platform.Common/Authorization`), MOD-0021 audit (`Diten.Platform` audit feature, outbox, redaction, export), MOD-0041 observability (`SensitiveDataRedactor`, `SensitiveDataLogEventEnricher`), correlation (`ICorrelationContext`, `CorrelationIdMiddleware`), tenancy (`ITenantContext`, `TenantResolutionMiddleware`). Three are genuinely absent: MOD-0019, MOD-0023, MOD-0031.
- 2026-08-09: Registry defects found and reconciled. MOD-0019, MOD-0230, MOD-0231, MOD-0232, MOD-0234, MOD-0004, and MOD-0063 had **no rows** in `execution/registries/module-id-registry.md` despite all being Blueprint-canonical. Rows added. MOD-0019 in particular could never have signed `PVG-MOD0230-FieldSecurity-Contract v1` because it had no registered owner.
- 2026-08-09: Route convention defect found. The MOD-0230 pack proposed upstream `/api/v1/pharmacovigilance/case-intake-triage`; NET-001 requires upstream `/api/{resource}` with `v1` on the downstream template only. Corrected to upstream `/api/pv-case-intake-triage`, downstream `/api/v1/pv-case-intake-triage`.
- 2026-08-09: Port band drift found. `.antigravity/rules/ports.md` documents up to 5058, but 5059 (MDM) and 5060 (HCM) are live in `launchSettings.json` and `ocelot.json`. `Diten.PvgService` assigned 5011 (verified free). `ports.md` is a protected path and needs explicit approval before it is updated.
- 2026-08-09: OD-2, OD-4, OD-5, OD-6, and OD-7 decided by NY. This DCP promoted from `draft` to `approved`. MOD-0230 promoted to `ready-for-dev` for the **build/test gate only**; the operational runtime gate stays closed for every member.
- 2026-08-09: MOD-0231, MOD-0232, and MOD-0234 remain `status: draft`. MOD-0234 remains contract-only with `shell: none` and `golden_reference: none`, unchanged.
- Reconciliation: implementation-phase results will be added here as member module packs are executed.
