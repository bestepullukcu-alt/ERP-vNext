---
id: MOD-0063
name: Data Warehouse / Lakehouse
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
runtime_owner_key: pending (proposed dki.data-warehouse)
permission_namespace:
  - pending (proposed dki.data-warehouse.read)
  - pending (proposed dki.data-warehouse.manage)
runtime_repo_scope: UNASSIGNED (pending EA service assignment)
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0063 - Data Warehouse / Lakehouse

> Status: draft — GOVERNANCE-FIRST scoping pack. This pack authors the required
> registry row and module-pack governance artifact for the Blueprint-canonical
> `MOD-0063 Data Warehouse / Lakehouse` (Blueprint domain **Data, Knowledge &
> Intelligence**), per DCP-008's verdict that "registry owner and module-pack
> authoring [are] required before runtime work". **No runtime, service,
> frontend, or gateway is authorized by this pack.** `MOD-0063` is
> Blueprint-canonical (not a candidate); the canonical MOD ID is used directly.
> Repo domain ownership and the owning runtime service remain **PENDING EA
> domain mapping** (DCP-008 lists the Analytics backbone as "Pending EA domain
> mapping"). Unlike the metric/KPI modules, there is **no existing service** for
> this cross-cutting data-plane infrastructure: a dedicated data-platform service
> (or an explicit EA/architecture decision) is required. Runtime is blocked until
> EA assigns a domain and a service.

## 1. Module Summary

`MOD-0063` is the Blueprint-canonical **Data Warehouse / Lakehouse**: the
physical **data-plane foundation** of the analytics backbone — the warehouse /
lakehouse storage and query substrate. It owns the analytical schemas and models
(fact/dimension tables or lakehouse tables), the storage/query layer, and the
physical targets that `MOD-0004` semantic bindings and `MOD-0060` metric
definitions map onto. It is **cross-cutting data infrastructure**, not a feature
module.

It is the physical root of the analytics-backbone group
(`MOD-0004`, `MOD-0059`–`MOD-0064`). `MOD-0004 Metric & Semantic Registry`
defines the shared vocabulary whose semantic bindings target this substrate;
`MOD-0060 Metric Definitions & Ownership` binds concrete metric definitions to
physical models here; `MOD-0062 Baseline & Experiment Measurement` reads its
measurement data from here; `MOD-0064 ETL / ELT Pipelines` feeds this warehouse
by ingesting and transforming source operational data into these analytical
models.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and explicitly
marks its domain ownership as "Pending EA domain mapping". This pack fulfils the
DCP-008 pre-runtime requirement (registry row + module pack) and records the
open EA decisions, without authorizing any runtime.

## 2. Ownership and Boundaries

Blueprint-reconciliation / name-collision context: during blueprint
reconciliation, a legacy mis-numbered `MOD-0008` was name-matched to "Data
Warehouse". **`MOD-0063` is the canonical Data Warehouse / Lakehouse identity**;
the legacy `MOD-0008` name collision is superseded and is not this module.

Owned by this module (future runtime, once EA assigns domain/service):

- The analytical **storage / query substrate** — the warehouse / lakehouse
  storage and query layer itself.
- The **physical analytical schemas / models**: fact/dimension tables (star /
  snowflake) or lakehouse tables that hold analytical data.
- The physical targets that semantic bindings (`MOD-0004`) and metric
  definitions (`MOD-0060`) resolve onto.

Not owned by this module:

- The semantic vocabulary — owned by `MOD-0004 Metric & Semantic Registry`.
- Concrete metric definitions, formulas, thresholds — owned by `MOD-0060 Metric
  Definitions & Ownership`.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`.
- Visual composition / scorecards — owned by `MOD-0061 Scorecards / Dashboards`.
- Baselines / experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Data movement / transformation (ETL/ELT) — owned by `MOD-0064 ETL / ELT
  Pipelines`, a distinct upstream module.
- Source operational databases (the systems of record that feed the warehouse).
- `Diten.EnterpriseStrategyService` (ESBP) — a consumer of analytics, not the
  owner of this data plane.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` and other facades — consumers
  that own no analytics runtime.

## 3. Owned Objects

Governance-level objects (runtime shapes deferred until EA assigns a service):

| Object | Type | Purpose |
|---|---|---|
| AnalyticalSchema | Governance concept | Physical analytical schema/namespace for warehouse or lakehouse models. |
| FactModel | Governance concept | Fact table / measure-bearing analytical model. |
| DimensionModel | Governance concept | Dimension table / conformed dimension. |
| LakehouseTable | Governance concept | Lakehouse-format table (open table format) holding analytical data. |
| StorageQueryLayer | Governance concept | The physical storage + query engine substrate the models live in. |
| PhysicalBindingTarget | Governance concept | The physical column/model a semantic binding (MOD-0004) or metric definition (MOD-0060) resolves onto. |

Deferred runtime objects (NOT authorized by this pack):

- Any entity/controller/repository/service scaffold — deferred until EA assigns
  a domain and a runtime service.

## 4. Entity Fields / Contract

This is a governance-first pack. **No runtime entity fields are authorized.**
This module *is* the physical data plane, but this pack authorizes no runtime and
persists nothing. The intended physical contract (for a future EA-approved
runtime slice) centres on: analytical storage, schema/model definitions, and the
query layer that downstream semantic bindings and metric definitions resolve
onto.

Because the future runtime owns analytical storage/schema/query, the
runtime-authorizing revision **must resolve strong governance concerns** for a
data warehouse before persistence is approved:

- **PII minimization** in analytical models (no more than necessary; prefer
  derived/aggregated over raw personal data).
- **Tenant isolation of analytical data** (see §13).
- **Retention** of analytical data (see §14).

Reserved / out-of-scope for any first runtime slice:

- No metric definitions, no semantic vocabulary (those are `MOD-0060` /
  `MOD-0004`).
- No data-movement/transform logic (that is `MOD-0064`).
- No money/PII field is authorized by this pack.

## 5. Repo Scope

Authorized governance scope (this pack only):

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0063-data-warehouse-lakehouse.md`
- The `MOD-0063` row in `execution/registries/module-id-registry.md` (already
  added).

Runtime repo scope: **UNASSIGNED** — no `services/**` path is authorized until
EA assigns the owning service. Note there is **no existing service** to place
this data-plane infrastructure into.

## 6. Protected Paths

This pack authorizes only its own governance artifacts. It does NOT authorize
changes to any `services/**`, `frontend/**`, or `gateway/**` path. Runtime
placement is an open EA decision — and, for this module, a decision that likely
requires a **new dedicated data-platform service** rather than an existing one.

## 7. Dependencies

Governance dependencies:

- `execution/registries/module-id-registry.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Fed by: `MOD-0064 ETL / ELT Pipelines` (ingests/transforms source data into
  the warehouse/lakehouse models).
- Consumed by: `MOD-0060 Metric Definitions & Ownership` (physical binding of
  metric definitions) and `MOD-0062 Baseline & Experiment Measurement`
  (measurement data).
- Bound onto by: the semantic bindings of `MOD-0004 Metric & Semantic Registry`
  target this physical substrate.

Consumer context (not dependencies of this module, they depend on / consume it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped KPI/metric runtime.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade.

## 8. Runtime Guard

**Runtime is BLOCKED.** No API/controller/entity/repository/database/service
scaffold is authorized by this pack. Runtime may be authorized only after:

1. EA assigns the repo domain ownership (currently "Pending EA domain mapping").
2. EA assigns the owning runtime service. **No analytics/data-platform service
   exists today, and this data-plane infrastructure must NOT be assumed to belong
   to `Diten.EnterpriseStrategyService` or `Diten.Platform`.** A **dedicated
   data-platform service** (or an explicit EA/architecture decision recording
   where this substrate lives) is required.
3. A runtime-authorizing pack revision (or successor pack) is approved.

Until then, `MOD-0063` must not appear as a runtime literal, owner key, or
permission namespace in any code.

## 9. Frontend / UX Boundary

No frontend/UX is authorized. Any future admin surface (warehouse/lakehouse
model browser, schema/catalog viewer) is deferred to a runtime-authorizing
revision under the EA-assigned (likely dedicated data-platform) service.

## 10. Backend / API Boundary

No backend/API is authorized. The intended future surface (analytical schema/
model management, storage/query administration, physical binding target
registration) is recorded for scoping only and is blocked pending EA service
assignment.

## 11. Data Boundary

No persistence is authorized by this pack — it persists nothing. This module
**is** the physical data plane, so when runtime is later approved the future
runtime owns **analytical storage, schema/model definitions, and the query
layer**. Because that runtime will hold analytical data, the runtime-authorizing
revision must resolve strong data-warehouse governance concerns before
persistence is approved: **PII minimization, tenant isolation of analytical
data, and retention** (see §4, §13, §14). This pack records these as open runtime
design items and authorizes none of them.

## 12. Permission Boundary

Proposed (not yet active) permission namespace, pending EA service/owner-key
assignment:

- `<service>.data-warehouse.read`
- `<service>.data-warehouse.manage`

Proposed owner key: `dki.data-warehouse` (pending). No permission is seeded by
this pack.

## 13. Tenant / Security Boundary

Future runtime must be tenant-aware and fail closed, resolving tenant
server-side. **Tenant isolation of analytical data is an open runtime design
item** and a strong governance concern for a data warehouse: the model must
guarantee that one tenant's analytical rows are never queryable by another
(physical partition, row-level security, per-tenant schema, or equivalent). The
isolation strategy is an open decision for the runtime-authorizing revision and
must be resolved before persistence is approved.

## 14. Compliance / Privacy Boundary

A data warehouse concentrates data, so the runtime-authorizing revision must
resolve, as open design items: **PII minimization** (store only what analytics
requires; prefer aggregated/derived over raw personal data), **retention**
(defined lifecycle and purge policy for analytical data), and audit/evidence of
schema and access changes. These are recorded here as open runtime design items;
this pack authorizes no data and resolves none of them.

## 15. Integration Boundary

Deferred/out-of-scope until EA assignment:

- Inbound feed contract from `MOD-0064` (ETL / ELT Pipelines) writing into
  analytical models.
- Physical binding targets exposed to `MOD-0004` semantic bindings and
  `MOD-0060` metric definitions.
- Read contracts for `MOD-0062` measurement data.

No integration implementation is authorized.

## 16. Acceptance Criteria

Governance acceptance criteria (this pack):

1. AC-01: Pack status is `draft` (governance-first).
2. AC-02: Canonical identity is `MOD-0063` (Blueprint-backed; not a candidate).
3. AC-03: Registry row for `MOD-0063` exists with domain
   `data-knowledge-intelligence` and status `planned / pending-runtime`, records
   dependencies (fed by `MOD-0064`; consumed by `MOD-0060`/`MOD-0062`), and notes
   that a dedicated data-platform service is required.
4. AC-04: The pack records that runtime service/domain ownership is PENDING EA,
   with explicit emphasis that no existing service (including
   `Diten.EnterpriseStrategyService` / `Diten.Platform`) is assumed.
5. AC-05: No runtime/frontend/gateway/service path is authorized or created.
6. AC-06: Analytics-backbone dependency relationships are recorded
   (`MOD-0004`, `MOD-0060`, `MOD-0062`, `MOD-0064`).
7. AC-07: Consumer context (ESBP, HCM `CAND-CAP-0034`) is recorded as
   non-owning; legacy `MOD-0008` name collision is recorded as superseded.

Future runtime acceptance criteria are deferred to the runtime-authorizing
revision.

## 17. Test Expectations

Governance validation expectations:

- Confirm the pack file exists with all 20 sections.
- Confirm frontmatter `status: draft` and `id: MOD-0063`.
- Confirm the registry row exists and marks runtime as pending EA (and notes the
  dedicated-service requirement).
- Confirm NO `services/**`, `frontend/**`, or `gateway/**` changes were made.
- Confirm `MOD-0063` is not written as a runtime literal anywhere.

No runtime tests are authorized (there is no runtime).

## 18. Governance Approval Checklist

- [x] Blueprint-canonical identity accepted (`MOD-0063`).
- [x] Registry row created (governance-first).
- [x] Module pack authored (this document).
- [x] Runtime explicitly blocked pending EA domain/service assignment.
- [x] Dedicated data-platform service requirement recorded (no existing service
      assumed).
- [ ] EA domain ownership assigned (OPEN).
- [ ] EA runtime service assigned — dedicated data-platform service decision
      (OPEN).
- [ ] Runtime-authorizing pack revision approved (OPEN).

## 19. Runtime-Ready Checklist

- [ ] EA repo domain ownership decision.
- [ ] EA runtime service decision — **a dedicated data-platform service (or
      explicit EA/architecture decision) is required; no analytics/data-platform
      service exists, and this must not default to
      `Diten.EnterpriseStrategyService` or `Diten.Platform`.**
- [ ] Owner key + permission namespace decision (proposed `dki.data-warehouse`).
- [ ] Runtime repo scope decision.
- [ ] Analytical storage/schema/query technology + model design.
- [ ] Tenant isolation, PII minimization, and retention design for analytical
      data.
- [ ] Inbound feed contract from `MOD-0064` and physical binding targets for
      `MOD-0004`/`MOD-0060`.

Runtime-ready blockers:

- Domain ownership and runtime service are unassigned (EA decision required),
  and there is no service to host this cross-cutting data-plane infrastructure.

## 20. Open Blockers / Waivers

Open blockers for runtime:

- EA domain mapping for the Analytics backbone is pending (DCP-008 "Analytics
  backbone — Pending EA domain mapping").
- **No owning runtime service exists for the analytics/data plane, and a
  dedicated data-platform service (or explicit EA/architecture decision) is
  required.** This data-plane infrastructure must not be assumed to belong to
  `Diten.EnterpriseStrategyService` or `Diten.Platform`.

Waivers recorded:

- Candidate gate script `.antigravity/scripts/verify_module_id.py` is not
  executable in this checkout (missing); the specified preflight
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0063`
  cannot be run — recorded as a governance note (same waiver as the HCM packs).
- This is a governance-first pack: it deliberately authorizes no runtime; the
  DCP-008 pre-runtime requirement (registry row + module pack) is satisfied.

Next safe step:

- EA assigns the repo domain and decides the owning runtime service — expected to
  be a **dedicated data-platform service** — for the analytics data plane; a
  runtime-authorizing pack revision is then produced.

Runtime literal scan:

- `rg -n "MOD-0063" services frontend gateway` must return no runtime literal
  uses (governance/docs references only).
