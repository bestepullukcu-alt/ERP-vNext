---
id: MOD-0004
name: Metric & Semantic Registry
domain: data-knowledge-intelligence
blueprint_domain: Enterprise Control Points
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
runtime_owner_key: pending (proposed dki.metric-semantic-registry)
permission_namespace:
  - pending (proposed dki.metric-semantic-registry.read)
  - pending (proposed dki.metric-semantic-registry.manage)
runtime_repo_scope: UNASSIGNED (pending EA service assignment)
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0004 - Metric & Semantic Registry

> Status: draft — GOVERNANCE-FIRST scoping pack. This pack authors the required
> registry row and module-pack governance artifact for the Blueprint-canonical
> `MOD-0004 Metric & Semantic Registry` (Blueprint domain **Enterprise Control
> Points**), per DCP-008's verdict that "registry owner and module-pack
> authoring [are] required before runtime work". **No runtime, service,
> frontend, or gateway is authorized by this pack.** `MOD-0004` is
> Blueprint-canonical (not a candidate); the canonical MOD ID is used directly.
> Repo domain ownership and the owning runtime service remain **PENDING EA
> domain mapping** (DCP-008 lists the Analytics backbone as "Pending EA domain
> mapping"). Runtime is blocked until EA assigns a domain and a service.

## 1. Module Summary

`MOD-0004` is the Blueprint-canonical **Metric & Semantic Registry**: the
authoritative semantic layer that defines the shared vocabulary of the analytics
backbone — canonical metric identities, semantic entities/dimensions/measures,
units, grains, and the semantic bindings that let every downstream metric,
KPI, scorecard, and experiment mean the same thing across the platform.

It is the semantic root of the analytics-backbone group
(`MOD-0004`, `MOD-0059`–`MOD-0064`). `MOD-0060 Metric Definitions & Ownership`
authors concrete metric definitions against this registry's vocabulary;
`MOD-0059 KPI Catalog` curates KPIs from those definitions; `MOD-0061
Scorecards / Dashboards` composes them; `MOD-0062 Baseline & Experiment
Measurement` measures against them; `MOD-0063 Data Warehouse / Lakehouse` and
`MOD-0064 ETL / ELT Pipelines` provide the physical data plane the semantics map
onto.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and explicitly
marks its domain ownership as "Pending EA domain mapping". This pack fulfils the
DCP-008 pre-runtime requirement (registry row + module pack) and records the
open EA decisions, without authorizing any runtime.

## 2. Ownership and Boundaries

Owned by this module (future runtime, once EA assigns domain/service):

- The canonical semantic vocabulary of metrics: metric identity, semantic type,
  unit, grain/granularity, aggregation semantics, dimensionality, and semantic
  relationships.
- The registry of semantic entities, dimensions, and measures shared across the
  analytics backbone.
- Semantic versioning and lineage of metric identities (rename/deprecate/alias).
- The contract other analytics-backbone modules bind their definitions to.

Not owned by this module:

- Concrete metric definitions, formulas, thresholds, and ownership — owned by
  `MOD-0060 Metric Definitions & Ownership`.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`.
- Visual composition — owned by `MOD-0061 Scorecards / Dashboards`.
- Baselines/experiments — owned by `MOD-0062`.
- Physical storage and query — owned by `MOD-0063 Data Warehouse / Lakehouse`.
- Data movement/transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- Goal-scoped strategy KPIs/metrics — those live in
  `Diten.EnterpriseStrategyService` (ESBP) and are a *consumer*, not the owner,
  of the shared semantic registry.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` — a *consumer* facade that
  owns no analytics runtime.

## 3. Owned Objects

Governance-level objects (runtime shapes deferred until EA assigns a service):

| Object | Type | Purpose |
|---|---|---|
| MetricIdentity | Governance concept | Canonical, stable identity of a metric (id, name, semantic type). |
| SemanticEntity | Governance concept | Business entity a metric is measured over. |
| SemanticDimension | Governance concept | Dimension/axis a metric can be sliced by. |
| SemanticMeasure | Governance concept | Unit/aggregation-bearing measure a metric resolves to. |
| SemanticBinding | Governance concept | Mapping from a semantic concept to a physical warehouse column/model. |
| SemanticVersion | Governance concept | Version/lineage of a metric identity (alias/deprecate). |

Deferred runtime objects (NOT authorized by this pack):

- Any entity/controller/repository/service scaffold — deferred until EA assigns
  a domain and a runtime service.

## 4. Entity Fields / Contract

This is a governance-first pack. **No runtime entity fields are authorized.**
The intended semantic contract (for a future EA-approved runtime slice) centres
on: metric identity, semantic type, unit, grain, aggregation, dimensionality,
version/lineage, and physical binding references — all metadata about metric
*meaning*, never measured values or PII.

Reserved / out-of-scope for any first runtime slice:

- No measured metric values, no computed results, no report/dataset content.
- No money field, no PII-heavy field.
- No physical data (that is `MOD-0063`).

## 5. Repo Scope

Authorized governance scope (this pack only):

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0004-metric-semantic-registry.md`
- The `MOD-0004` row in `execution/registries/module-id-registry.md`.

Runtime repo scope: **UNASSIGNED** — no `services/**` path is authorized until
EA assigns the owning service.

## 6. Protected Paths

This pack authorizes only its own governance artifacts. It does NOT authorize
changes to any `services/**`, `frontend/**`, or `gateway/**` path. Runtime
placement is an open EA decision.

## 7. Dependencies

Governance dependencies:

- `execution/registries/module-id-registry.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Downstream consumers: `MOD-0060` (Metric Definitions & Ownership), `MOD-0059`
  (KPI Catalog), `MOD-0062` (Baseline & Experiment Measurement).
- Physical-plane dependency (for bindings): `MOD-0063` (Data Warehouse /
  Lakehouse), fed by `MOD-0064` (ETL / ELT Pipelines).

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

Until then, `MOD-0004` must not appear as a runtime literal, owner key, or
permission namespace in any code.

## 9. Frontend / UX Boundary

No frontend/UX is authorized. Any future admin surface (semantic registry
browser/editor) is deferred to a runtime-authorizing revision under the
EA-assigned service.

## 10. Backend / API Boundary

No backend/API is authorized. The intended future surface (semantic vocabulary
CRUD, binding management, version/lineage) is recorded for scoping only and is
blocked pending EA service assignment.

## 11. Data Boundary

No persistence is authorized. When runtime is later approved, the module owns
only *semantic metadata* (metric meaning), never measured values, datasets,
report content, money, or PII — those belong to other modules
(`MOD-0060/0062/0063`) or are forbidden outright.

## 12. Permission Boundary

Proposed (not yet active) permission namespace, pending EA service/owner-key
assignment:

- `<service>.metric-semantic-registry.read`
- `<service>.metric-semantic-registry.manage`

No permission is seeded by this pack.

## 13. Tenant / Security Boundary

Future runtime must be tenant-aware and fail closed, resolving tenant
server-side. The semantic registry may be partly platform-global (shared
vocabulary) with tenant overlays — the tenancy model is an open design decision
for the runtime-authorizing revision.

## 14. Compliance / Privacy Boundary

Semantic metadata only; no PII, no measured data. Retention/evidence/audit of
semantic changes (metric identity lineage) is a design item for the runtime
revision.

## 15. Integration Boundary

Deferred/out-of-scope until EA assignment:

- Physical bindings to `MOD-0063` warehouse models.
- Consumption contracts published to `MOD-0059/0060/0062`, ESBP, and HCM
  facades.

No integration implementation is authorized.

## 16. Acceptance Criteria

Governance acceptance criteria (this pack):

1. AC-01: Pack status is `draft` (governance-first).
2. AC-02: Canonical identity is `MOD-0004` (Blueprint-backed; not a candidate).
3. AC-03: Registry row for `MOD-0004` exists with domain
   `data-knowledge-intelligence` and status `planned / pending-runtime`.
4. AC-04: The pack records that runtime service/domain ownership is PENDING EA.
5. AC-05: No runtime/frontend/gateway/service path is authorized or created.
6. AC-06: Analytics-backbone dependency relationships are recorded
   (`MOD-0059`–`MOD-0064`).
7. AC-07: Consumer context (ESBP, HCM `CAND-CAP-0034`) is recorded as
   non-owning.

Future runtime acceptance criteria are deferred to the runtime-authorizing
revision.

## 17. Test Expectations

Governance validation expectations:

- Confirm the pack file exists with all 20 sections.
- Confirm frontmatter `status: draft` and `id: MOD-0004`.
- Confirm the registry row exists and marks runtime as pending EA.
- Confirm NO `services/**`, `frontend/**`, or `gateway/**` changes were made.
- Confirm `MOD-0004` is not written as a runtime literal anywhere.

No runtime tests are authorized (there is no runtime).

## 18. Governance Approval Checklist

- [x] Blueprint-canonical identity accepted (`MOD-0004`).
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
- [ ] Semantic contract + tenancy model design.
- [ ] Physical binding contract to `MOD-0063`.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0004`
  cannot be run — recorded as a governance note (same waiver as the HCM packs).
- This is a governance-first pack: it deliberately authorizes no runtime; the
  DCP-008 pre-runtime requirement (registry row + module pack) is satisfied.

Next safe step:

- EA assigns the repo domain and the owning runtime service for the analytics
  backbone; a runtime-authorizing pack revision is then produced.

Runtime literal scan:

- `rg -n "MOD-0004" services frontend gateway` must return no runtime literal
  uses (governance/docs references only).
