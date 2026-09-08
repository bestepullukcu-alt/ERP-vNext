---
id: MOD-0060
name: Metric Definitions & Ownership
domain: data-knowledge-intelligence
blueprint_domain: Data, Knowledge & Intelligence
service: UNASSIGNED (pending EA domain/service mapping)
shell: pending
golden_reference: n/a (governance-first scoping pack; no runtime authored)
entity_base: n/a
status: draft
owner: enterprise-architect / data-platform-owner / analytics-owner / platform-team
branch: n/a (governance-first; runtime branch blocked pending EA)
started: 2026-09-07
target: pending-EA
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
identity_class: canonical (Blueprint-backed)
canonicalization_status: canonical / registry-row-created / runtime-pending-EA
runtime_service_candidate: UNASSIGNED
runtime_owner_key: pending (proposed dki.metric-definitions)
permission_namespace:
  - pending (proposed dki.metric-definitions.read)
  - pending (proposed dki.metric-definitions.manage)
runtime_repo_scope: UNASSIGNED (pending EA service assignment)
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0060 - Metric Definitions & Ownership

> Status: draft — GOVERNANCE-FIRST scoping pack. This pack authors the required
> registry row and module-pack governance artifact for the Blueprint-canonical
> `MOD-0060 Metric Definitions & Ownership` (Blueprint domain **Data, Knowledge &
> Intelligence**), per DCP-008's verdict that "registry owner and module-pack
> authoring [are] required before runtime work". **No runtime, service,
> frontend, or gateway is authorized by this pack.** `MOD-0060` is
> Blueprint-canonical (not a candidate); the canonical MOD ID is used directly.
> Repo domain ownership and the owning runtime service remain **PENDING EA
> domain mapping** (DCP-008 lists the Analytics backbone as "Pending EA domain
> mapping"). Runtime is blocked until EA assigns a domain and a service.

## 1. Module Summary

`MOD-0060` is the Blueprint-canonical **Metric Definitions & Ownership**: the
authoring and stewardship layer for concrete metric definitions across the
analytics backbone. Each metric definition binds a metric to the shared
semantic vocabulary of `MOD-0004`, and records its definition/expression
reference, grain, aggregation, unit, calculation semantics, owner/steward,
lifecycle status, and SLA. It owns the **definition and ownership** of metrics —
what a metric *means*, how it is *calculated*, and *who is accountable for it* —
never the metrics' computed or measured values.

It is the metric-authoring layer of the analytics-backbone group
(`MOD-0004`, `MOD-0059`–`MOD-0064`). It binds every concrete definition to the
`MOD-0004 Metric & Semantic Registry` vocabulary and maps each definition to the
physical models of `MOD-0063 Data Warehouse / Lakehouse`. `MOD-0059 KPI Catalog`
curates KPIs from these definitions; `MOD-0061 Scorecards / Dashboards` composes
them; `MOD-0062 Baseline & Experiment Measurement` measures against them;
`MOD-0064 ETL / ELT Pipelines` feeds the physical plane the definitions map onto.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and explicitly
marks its domain ownership as "Pending EA domain mapping". This pack fulfils the
DCP-008 pre-runtime requirement (registry row + module pack) and records the
open EA decisions, without authorizing any runtime.

## 2. Ownership and Boundaries

Owned by this module (future runtime, once EA assigns domain/service):

- Concrete metric definitions: each metric's definition/expression reference,
  grain, aggregation, unit, calculation semantics, and lifecycle status.
- Metric ownership and stewardship: the accountable owner/steward for each
  metric, plus the SLA attached to its definition.
- Definition lifecycle: draft/active/deprecated transitions of concrete metric
  definitions and their versioning.
- The binding of each definition to the `MOD-0004` semantic vocabulary and its
  mapping to `MOD-0063` physical models.

Not owned by this module:

- The canonical semantic vocabulary (metric identity, semantic type, unit,
  grain, aggregation semantics) — owned by `MOD-0004 Metric & Semantic
  Registry`; this module *binds to* it.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`.
- Visual composition — owned by `MOD-0061 Scorecards / Dashboards`.
- Baselines/experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Physical storage and query — owned by `MOD-0063 Data Warehouse / Lakehouse`.
- Data movement/transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- Goal-scoped strategy KPIs/metrics — those live in
  `Diten.EnterpriseStrategyService` (ESBP) and are a *consumer*, not the owner,
  of these metric definitions.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` — a *consumer* facade that
  owns no analytics runtime.

## 3. Owned Objects

Governance-level objects (runtime shapes deferred until EA assigns a service):

| Object | Type | Purpose |
|---|---|---|
| MetricDefinition | Governance concept | Concrete definition of a metric: expression reference, grain, aggregation, unit, calculation semantics. |
| MetricOwnership | Governance concept | Accountable owner/steward for a metric definition. |
| MetricSla | Governance concept | Freshness/availability SLA attached to a metric definition. |
| DefinitionLifecycle | Governance concept | Lifecycle status/version of a concrete metric definition (draft/active/deprecated). |
| SemanticBindingRef | Governance concept | Reference binding a definition to a `MOD-0004` semantic concept. |
| PhysicalMappingRef | Governance concept | Reference mapping a definition to a `MOD-0063` physical model/column. |

Deferred runtime objects (NOT authorized by this pack):

- Any entity/controller/repository/service scaffold — deferred until EA assigns
  a domain and a runtime service.

## 4. Entity Fields / Contract

This is a governance-first pack. **No runtime entity fields are authorized.**
The intended definition contract (for a future EA-approved runtime slice)
centres on: metric-definition metadata — definition/expression reference, grain,
aggregation, unit, calculation semantics, owner/steward, lifecycle status, SLA,
plus the semantic-binding reference to `MOD-0004` and the physical-mapping
reference to `MOD-0063`. All of it is metadata about a metric's *definition and
ownership*, never measured values or PII.

Reserved / out-of-scope for any first runtime slice:

- No computed/measured metric values, no report/dataset content.
- No money field, no PII-heavy field.
- No physical data (that is `MOD-0063`), no semantic vocabulary (that is
  `MOD-0004`).

## 5. Repo Scope

Authorized governance scope (this pack only):

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0060-metric-definitions-ownership.md`
- The `MOD-0060` row in `execution/registries/module-id-registry.md` (already
  added).

Runtime repo scope: **UNASSIGNED** — no `services/**` path is authorized until
EA assigns the owning service.

## 6. Protected Paths

This pack authorizes only its own governance artifacts (this module pack plus
the already-added registry row). It does NOT authorize changes to any
`services/**`, `frontend/**`, or `gateway/**` path. Runtime placement is an open
EA decision.

## 7. Dependencies

Governance dependencies:

- `execution/registries/module-id-registry.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Depends on: `MOD-0004` (Metric & Semantic Registry) for the semantic
  vocabulary each definition binds to, and `MOD-0063` (Data Warehouse /
  Lakehouse) for the physical models each definition maps onto.
- Consumed by: `MOD-0059` (KPI Catalog), `MOD-0061` (Scorecards / Dashboards),
  and `MOD-0062` (Baseline & Experiment Measurement).

Consumer context (not dependencies of this module, they depend on it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped KPI/metric runtime.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade.

## 8. Runtime Guard

**Runtime is BLOCKED.** No API/controller/entity/repository/database/service
scaffold is authorized by this pack. Runtime may be authorized only after:

1. EA assigns the repo domain ownership (currently "Pending EA domain mapping").
2. EA assigns the owning runtime service (no analytics/data-platform service
   exists today; a dedicated data-platform service or an explicit assignment is
   required).
3. A runtime-authorizing pack revision (or successor pack) is approved.

Until then, `MOD-0060` must not appear as a runtime literal, owner key, or
permission namespace in any code.

## 9. Frontend / UX Boundary

No frontend/UX is authorized. Any future admin surface (metric-definition
authoring/stewardship browser/editor) is deferred to a runtime-authorizing
revision under the EA-assigned service.

## 10. Backend / API Boundary

No backend/API is authorized. The intended future surface (metric-definition
CRUD, ownership/stewardship assignment, lifecycle/SLA management, semantic and
physical binding references) is recorded for scoping only and is blocked pending
EA service assignment.

## 11. Data Boundary

No persistence is authorized. When runtime is later approved, the module owns
only *metric-definition metadata and ownership* (definition, grain, aggregation,
unit, calculation semantics, owner/steward, lifecycle, SLA), never computed or
measured metric values, datasets, report content, money, or PII — those belong
to other modules (`MOD-0063` physical data, `MOD-0062` measurements) or are
forbidden outright.

## 12. Permission Boundary

Proposed (not yet active) permission namespace, pending EA service/owner-key
assignment:

- `<service>.metric-definitions.read`
- `<service>.metric-definitions.manage`

No permission is seeded by this pack.

## 13. Tenant / Security Boundary

Future runtime must be tenant-aware and fail closed, resolving tenant
server-side. Metric definitions may be partly platform-global (shared baseline
definitions) with tenant overlays — the tenancy model is an open design decision
for the runtime-authorizing revision.

## 14. Compliance / Privacy Boundary

Metric-definition metadata and ownership only; no PII, no measured data.
Retention/evidence/audit of definition changes (definition lineage, ownership
reassignment) is a design item for the runtime revision.

## 15. Integration Boundary

Deferred/out-of-scope until EA assignment:

- Semantic-binding references to `MOD-0004` vocabulary.
- Physical-mapping references to `MOD-0063` warehouse models.
- Consumption contracts published to `MOD-0059/0061/0062`, ESBP, and HCM
  facades.

No integration implementation is authorized.

## 16. Acceptance Criteria

Governance acceptance criteria (this pack):

1. AC-01: Pack status is `draft` (governance-first).
2. AC-02: Canonical identity is `MOD-0060` (Blueprint-backed; not a candidate).
3. AC-03: Registry row for `MOD-0060` exists with domain
   `data-knowledge-intelligence` and status `planned / pending-runtime`.
4. AC-04: The pack records that runtime service/domain ownership is PENDING EA.
5. AC-05: No runtime/frontend/gateway/service path is authorized or created.
6. AC-06: Analytics-backbone dependency relationships are recorded — depends on
   `MOD-0004` and `MOD-0063`; consumed by `MOD-0059`, `MOD-0061`, `MOD-0062`.
7. AC-07: Consumer context (ESBP, HCM `CAND-CAP-0034`) is recorded as
   non-owning.

Future runtime acceptance criteria are deferred to the runtime-authorizing
revision.

## 17. Test Expectations

Governance validation expectations:

- Confirm the pack file exists with all 20 sections.
- Confirm frontmatter `status: draft` and `id: MOD-0060`.
- Confirm the registry row exists and marks runtime as pending EA.
- Confirm NO `services/**`, `frontend/**`, or `gateway/**` changes were made.
- Confirm `MOD-0060` is not written as a runtime literal anywhere.

No runtime tests are authorized (there is no runtime).

## 18. Governance Approval Checklist

- [x] Blueprint-canonical identity accepted (`MOD-0060`).
- [x] Registry row created (governance-first).
- [x] Module pack authored (this document).
- [x] Runtime explicitly blocked pending EA domain/service assignment.
- [ ] EA domain ownership assigned (OPEN).
- [ ] EA runtime service assigned (OPEN).
- [ ] Runtime-authorizing pack revision approved (OPEN).

## 19. Runtime-Ready Checklist

- [ ] EA repo domain ownership decision.
- [ ] EA runtime service decision (no analytics/data-platform service exists).
- [ ] Owner key + permission namespace decision.
- [ ] Runtime repo scope decision.
- [ ] Definition contract + tenancy model design.
- [ ] Semantic-binding contract to `MOD-0004` and physical-mapping contract to
  `MOD-0063`.

Runtime-ready blockers:

- Domain ownership and runtime service are unassigned (EA decision required).

## 20. Open Blockers / Waivers

Open blockers for runtime:

- EA domain mapping for the Analytics backbone is pending (DCP-008 "Analytics
  backbone — Pending EA domain mapping").
- No owning runtime service exists for the analytics/data plane.

Waivers recorded:

- Candidate gate script `.antigravity/scripts/verify_module_id.py` is not
  executable in this checkout (missing); the specified preflight
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0060`
  cannot be run — recorded as a governance note (same waiver as the HCM packs).
- This is a governance-first pack: it deliberately authorizes no runtime; the
  DCP-008 pre-runtime requirement (registry row + module pack) is satisfied.

Next safe step:

- EA assigns the repo domain and the owning runtime service for the analytics
  backbone; a runtime-authorizing pack revision is then produced.

Runtime literal scan:

- `rg -n "MOD-0060" services frontend gateway` must return no runtime literal
  uses (governance/docs references only).
