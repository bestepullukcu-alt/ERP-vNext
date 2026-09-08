---
id: MOD-0059
name: KPI Catalog
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
runtime_owner_key: pending (proposed dki.kpi-catalog)
permission_namespace:
  - pending (proposed dki.kpi-catalog.read)
  - pending (proposed dki.kpi-catalog.manage)
runtime_repo_scope: UNASSIGNED (pending EA service assignment)
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0059 - KPI Catalog

> Status: draft — GOVERNANCE-FIRST scoping pack. This pack authors the required
> module-pack governance artifact for the Blueprint-canonical `MOD-0059 KPI
> Catalog` (Blueprint domain **Data, Knowledge & Intelligence**), per DCP-008's
> verdict that "registry owner and module-pack authoring [are] required before
> runtime work". **No runtime, service, frontend, or gateway is authorized by
> this pack.** `MOD-0059` is Blueprint-canonical (not a candidate); the canonical
> MOD ID is used directly. Repo domain ownership and the owning runtime service
> remain **PENDING EA domain mapping** (DCP-008 lists the Analytics backbone as
> "Pending EA domain mapping"). Runtime is blocked until EA assigns a domain and
> a service.

## 1. Module Summary

`MOD-0059` is the Blueprint-canonical **KPI Catalog**: the curated, governed
catalog of Key Performance Indicators — KPI identity, categorization/taxonomy,
KPI ownership, lifecycle status, and *references* to their backing metric
definitions and to their target/threshold references. It is a **catalog
(governance/curation) layer**, not a computation, measurement, or reporting
engine: it holds KPI *metadata* and references, never computed KPI values or
measured data.

It sits in the analytics-backbone group
(`MOD-0004`, `MOD-0059`–`MOD-0064`) as the curation layer over concrete metric
definitions. `MOD-0004 Metric & Semantic Registry` defines the shared semantic
vocabulary; `MOD-0060 Metric Definitions & Ownership` authors the concrete
metric definitions and formulas; `MOD-0059 KPI Catalog` curates KPIs by
referencing those definitions and their targets/thresholds; `MOD-0061
Scorecards / Dashboards` composes the catalog into visual surfaces; `MOD-0062
Baseline & Experiment Measurement` measures against them; `MOD-0063 Data
Warehouse / Lakehouse` and `MOD-0064 ETL / ELT Pipelines` provide the physical
data plane.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and explicitly
marks its domain ownership as "Pending EA domain mapping". This pack fulfils the
DCP-008 pre-runtime requirement (registry row + module pack) and records the
open EA decisions, without authorizing any runtime.

## 2. Ownership and Boundaries

Owned by this module (future runtime, once EA assigns domain/service):

- The curated KPI catalog: KPI identity, name, and stable catalog identity.
- KPI categorization/taxonomy and grouping (KPI families, categories).
- KPI ownership (who is accountable for a KPI) and lifecycle status
  (active/deprecated/proposed).
- References from a KPI to its backing metric definition(s) — as references
  only, never the definition/formula itself.
- References from a KPI to its target/threshold references — as references
  only, never computed or measured values.

Not owned by this module:

- Concrete metric definitions, formulas, and ownership — owned by `MOD-0060
  Metric Definitions & Ownership`.
- The shared semantic vocabulary — owned by `MOD-0004 Metric & Semantic
  Registry`.
- Scorecard/dashboard composition — owned by `MOD-0061 Scorecards /
  Dashboards`.
- Baselines/experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Physical storage and query — owned by `MOD-0063 Data Warehouse / Lakehouse`.
- Data movement/transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- Goal-scoped strategy KPIs — those live in `Diten.EnterpriseStrategyService`
  (ESBP) and are a *consumer*, not the owner, of the shared KPI catalog.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` — a *consumer* facade that
  owns no analytics runtime.

## 3. Owned Objects

Governance-level objects (runtime shapes deferred until EA assigns a service):

| Object | Type | Purpose |
|---|---|---|
| KpiIdentity | Governance concept | Canonical, stable identity of a KPI (id, name, catalog key). |
| KpiCategory | Governance concept | Taxonomy node/category a KPI is grouped under. |
| KpiOwnership | Governance concept | Accountable owner/steward of a KPI. |
| KpiLifecycleStatus | Governance concept | Lifecycle state of a KPI (proposed/active/deprecated). |
| MetricDefinitionRef | Governance concept | Reference from a KPI to its backing metric definition (MOD-0060). |
| TargetThresholdRef | Governance concept | Reference from a KPI to its target/threshold reference (not a computed value). |

Deferred runtime objects (NOT authorized by this pack):

- Any entity/controller/repository/service scaffold — deferred until EA assigns
  a domain and a runtime service.

## 4. Entity Fields / Contract

This is a governance-first pack. **No runtime entity fields are authorized.**
The intended catalog contract (for a future EA-approved runtime slice) centres
on: KPI identity, category/taxonomy, ownership, lifecycle status, and reference
handles to backing metric definitions and target/threshold references — all
KPI *catalog metadata*, never computed KPI values or measured data.

Reserved / out-of-scope for any first runtime slice:

- No computed KPI values, no measured data, no report/dataset content.
- No money field, no PII-heavy field.
- No metric definitions/formulas (those are `MOD-0060`).
- No physical data (that is `MOD-0063`).

## 5. Repo Scope

Authorized governance scope (this pack only):

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0059-kpi-catalog.md`
- The `MOD-0059` row in `execution/registries/module-id-registry.md` (already
  added).

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

- Depends on: `MOD-0060` (Metric Definitions & Ownership) — the KPI catalog
  references concrete metric definitions authored there.
- Depends on: `MOD-0004` (Metric & Semantic Registry) — the shared semantic
  vocabulary those definitions bind to.
- Consumed by: `MOD-0061` (Scorecards / Dashboards) — composes catalog KPIs
  into visual surfaces.

Consumer context (not dependencies of this module, they depend on it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped strategy KPI runtime.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade.

## 8. Runtime Guard

**Runtime is BLOCKED.** No API/controller/entity/repository/database/service
scaffold is authorized by this pack. Runtime may be authorized only after:

1. EA assigns the repo domain ownership (currently "Pending EA domain mapping").
2. EA assigns the owning runtime service (no analytics/data-platform service
   exists today; a dedicated data-platform service or an explicit assignment is
   required).
3. A runtime-authorizing pack revision (or successor pack) is approved.

Until then, `MOD-0059` must not appear as a runtime literal, owner key, or
permission namespace in any code.

## 9. Frontend / UX Boundary

No frontend/UX is authorized. Any future admin surface (KPI catalog
browser/editor, taxonomy management) is deferred to a runtime-authorizing
revision under the EA-assigned service.

## 10. Backend / API Boundary

No backend/API is authorized. The intended future surface (KPI catalog CRUD,
categorization/taxonomy management, ownership assignment, definition/target
reference management) is recorded for scoping only and is blocked pending EA
service assignment.

## 11. Data Boundary

No persistence is authorized. When runtime is later approved, the module owns
only *KPI catalog metadata* (identity, category, ownership, lifecycle, and
references to metric definitions and target/threshold references), never
computed KPI values, measured data, report content, money, or PII — those
belong to other modules (`MOD-0060/0061/0062/0063`) or are forbidden outright.

## 12. Permission Boundary

Proposed (not yet active) permission namespace, pending EA service/owner-key
assignment:

- `<service>.kpi-catalog.read`
- `<service>.kpi-catalog.manage`

No permission is seeded by this pack.

## 13. Tenant / Security Boundary

Future runtime must be tenant-aware and fail closed, resolving tenant
server-side. The KPI catalog may be partly platform-global (shared/curated KPI
definitions) with tenant overlays — the tenancy model is an open design
decision for the runtime-authorizing revision.

## 14. Compliance / Privacy Boundary

KPI catalog metadata only; no PII, no measured data. Retention/evidence/audit of
catalog changes (KPI identity/ownership/lifecycle lineage) is a design item for
the runtime revision.

## 15. Integration Boundary

Deferred/out-of-scope until EA assignment:

- Reference resolution to `MOD-0060` metric definitions and their
  targets/thresholds.
- Consumption contracts published to `MOD-0061`, ESBP, and HCM facades.

No integration implementation is authorized.

## 16. Acceptance Criteria

Governance acceptance criteria (this pack):

1. AC-01: Pack status is `draft` (governance-first).
2. AC-02: Canonical identity is `MOD-0059` (Blueprint-backed; not a candidate).
3. AC-03: Registry row for `MOD-0059` exists with domain
   `data-knowledge-intelligence` and status `planned / pending-runtime`.
4. AC-04: The pack records that runtime service/domain ownership is PENDING EA.
5. AC-05: No runtime/frontend/gateway/service path is authorized or created.
6. AC-06: Dependency relationships are recorded (depends on `MOD-0060` and
   `MOD-0004`; consumed by `MOD-0061`).
7. AC-07: Consumer context (ESBP, HCM `CAND-CAP-0034`) is recorded as
   non-owning.

Future runtime acceptance criteria are deferred to the runtime-authorizing
revision.

## 17. Test Expectations

Governance validation expectations:

- Confirm the pack file exists with all 20 sections.
- Confirm frontmatter `status: draft` and `id: MOD-0059`.
- Confirm the registry row exists and marks runtime as pending EA.
- Confirm NO `services/**`, `frontend/**`, or `gateway/**` changes were made.
- Confirm `MOD-0059` is not written as a runtime literal anywhere.

No runtime tests are authorized (there is no runtime).

## 18. Governance Approval Checklist

- [x] Blueprint-canonical identity accepted (`MOD-0059`).
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
- [ ] KPI catalog contract + tenancy model design.
- [ ] Reference contract to `MOD-0060` metric definitions and targets/thresholds.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0059`
  cannot be run — recorded as a governance note (same waiver as the HCM packs).
- This is a governance-first pack: it deliberately authorizes no runtime; the
  DCP-008 pre-runtime requirement (registry row + module pack) is satisfied.

Next safe step:

- EA assigns the repo domain and the owning runtime service for the analytics
  backbone; a runtime-authorizing pack revision is then produced.

Runtime literal scan:

- `rg -n "MOD-0059" services frontend gateway` must return no runtime literal
  uses (governance/docs references only).
