---
id: MOD-0062
name: Baseline & Experiment Measurement
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
runtime_owner_key: pending (proposed dki.baseline-experiment)
permission_namespace:
  - pending (proposed dki.baseline-experiment.read)
  - pending (proposed dki.baseline-experiment.manage)
runtime_repo_scope: UNASSIGNED (pending EA service assignment)
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0062 - Baseline & Experiment Measurement

> Status: draft — GOVERNANCE-FIRST scoping pack. This pack authors the required
> registry row and module-pack governance artifact for the Blueprint-canonical
> `MOD-0062 Baseline & Experiment Measurement` (Blueprint domain **Data,
> Knowledge & Intelligence**), per DCP-008's verdict that "registry owner and
> module-pack authoring [are] required before runtime work". **No runtime,
> service, frontend, or gateway is authorized by this pack.** `MOD-0062` is
> Blueprint-canonical (not a candidate); the canonical MOD ID is used directly.
> Repo domain ownership and the owning runtime service remain **PENDING EA
> domain mapping** (DCP-008 lists the Analytics backbone as "Pending EA domain
> mapping"). Runtime is blocked until EA assigns a domain and a service.

## 1. Module Summary

`MOD-0062` is the Blueprint-canonical **Baseline & Experiment Measurement**
module: the framework that defines **baselines** and **experiments** (e.g. A/B,
cohort, before/after) over metrics. It owns baseline identity/definition,
experiment design metadata — hypothesis reference, variant/cohort definitions,
measurement windows, guardrail-metric references — and the binding of
experiments to metric definitions. It defines **how measurement is set up**; it
does **not** store the measured results/datasets or run the statistics engine.

It is part of the analytics-backbone group
(`MOD-0004`, `MOD-0059`–`MOD-0064`). It measures against the semantic vocabulary
of `MOD-0004 Metric & Semantic Registry` and the concrete definitions of
`MOD-0060 Metric Definitions & Ownership`, and it reads measurement data from
`MOD-0063 Data Warehouse / Lakehouse`. `MOD-0059 KPI Catalog` and `MOD-0061
Scorecards / Dashboards` sit alongside it as sibling consumers of the same
definitions; `MOD-0064 ETL / ELT Pipelines` provides the physical data plane
feeding `MOD-0063`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and explicitly
marks its domain ownership as "Pending EA domain mapping". This pack fulfils the
DCP-008 pre-runtime requirement (registry row + module pack) and records the
open EA decisions, without authorizing any runtime.

## 2. Ownership and Boundaries

Owned by this module (future runtime, once EA assigns domain/service):

- Baseline definitions: baseline identity, the metric a baseline measures, the
  baseline window/period, and the reference/expected value semantics.
- Experiment design and measurement-setup metadata: hypothesis reference,
  variant/cohort definitions, assignment/segmentation criteria, measurement
  windows, and start/stop conditions.
- Guardrail-metric references attached to an experiment.
- The binding of experiments and baselines to metric definitions (`MOD-0060`)
  and semantic identities (`MOD-0004`).

Not owned by this module:

- Concrete metric definitions, formulas, thresholds, and ownership — owned by
  `MOD-0060 Metric Definitions & Ownership`.
- The shared semantic vocabulary — owned by `MOD-0004 Metric & Semantic
  Registry`.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`.
- Visual composition — owned by `MOD-0061 Scorecards / Dashboards`.
- Physical/measured measurement data and datasets — owned by `MOD-0063 Data
  Warehouse / Lakehouse`.
- Data movement/transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- The statistical computation runtime that computes experiment results and
  significance — NOT owned here (this module sets up measurement, it does not
  run the statistics engine).
- Goal-scoped strategy KPIs/metrics — those live in
  `Diten.EnterpriseStrategyService` (ESBP) and are a *consumer*, not the owner,
  of experiment/baseline setup.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` — a *consumer* facade that
  owns no analytics runtime.

## 3. Owned Objects

Governance-level objects (runtime shapes deferred until EA assigns a service):

| Object | Type | Purpose |
|---|---|---|
| BaselineDefinition | Governance concept | Canonical identity/definition of a baseline over a metric (metric ref, window, reference-value semantics). |
| ExperimentDesign | Governance concept | Experiment identity and design metadata (type: A/B, cohort, before/after). |
| HypothesisReference | Governance concept | Reference to the hypothesis an experiment tests. |
| VariantCohortDefinition | Governance concept | Definition of variants/cohorts and their assignment/segmentation criteria. |
| MeasurementWindow | Governance concept | The time window(s) over which a baseline/experiment is measured. |
| GuardrailMetricReference | Governance concept | Reference to a guardrail metric an experiment must not regress. |
| ExperimentMetricBinding | Governance concept | Binding of an experiment/baseline to a metric definition (`MOD-0060`). |

Deferred runtime objects (NOT authorized by this pack):

- Any entity/controller/repository/service scaffold — deferred until EA assigns
  a domain and a runtime service.

## 4. Entity Fields / Contract

This is a governance-first pack. **No runtime entity fields are authorized.**
The intended contract (for a future EA-approved runtime slice) centres on
baseline/experiment **design metadata and references** only: baseline identity,
metric references, measurement windows, experiment type, hypothesis reference,
variant/cohort definitions, guardrail-metric references, and metric-definition
bindings — all metadata about how measurement is *set up*, never measured
results or PII.

Reserved / out-of-scope for any first runtime slice:

- No measured results/datasets, no computed statistics, no significance/lift
  outputs, no report content.
- No money field, no PII-heavy field.
- No physical measurement data (that is `MOD-0063`); no statistical computation
  runtime.

## 5. Repo Scope

Authorized governance scope (this pack only):

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0062-baseline-experiment-measurement.md`
- The `MOD-0062` row in `execution/registries/module-id-registry.md` (already
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

- Depends on: `MOD-0060` (Metric Definitions & Ownership) for the concrete
  metrics measured, `MOD-0004` (Metric & Semantic Registry) for semantic
  identities, and `MOD-0063` (Data Warehouse / Lakehouse) for measurement data.
- Physical-plane dependency (for measurement data): `MOD-0063` (Data Warehouse /
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

Until then, `MOD-0062` must not appear as a runtime literal, owner key, or
permission namespace in any code.

## 9. Frontend / UX Boundary

No frontend/UX is authorized. Any future admin surface (baseline/experiment
designer, measurement-window editor) is deferred to a runtime-authorizing
revision under the EA-assigned service.

## 10. Backend / API Boundary

No backend/API is authorized. The intended future surface (baseline CRUD,
experiment design CRUD, guardrail/variant/cohort management, metric-definition
binding) is recorded for scoping only and is blocked pending EA service
assignment.

## 11. Data Boundary

No persistence is authorized. When runtime is later approved, the module owns
only baseline/experiment **design metadata and references**, never measured
results/datasets, computed statistics, report content, money, or PII — those
belong to other modules (`MOD-0060/0063`), to the statistical computation
runtime, or are forbidden outright.

## 12. Permission Boundary

Proposed (not yet active) permission namespace, pending EA service/owner-key
assignment:

- `<service>.baseline-experiment.read`
- `<service>.baseline-experiment.manage`

No permission is seeded by this pack.

## 13. Tenant / Security Boundary

Future runtime must be tenant-aware and fail closed, resolving tenant
server-side. Baseline/experiment definitions are tenant-scoped design metadata;
whether any templates are platform-global with tenant overlays is an open design
decision for the runtime-authorizing revision.

## 14. Compliance / Privacy Boundary

Design metadata only; no measured data, no PII. Retention/evidence/audit of
baseline and experiment design changes is a design item for the runtime
revision.

## 15. Integration Boundary

Deferred/out-of-scope until EA assignment:

- Reads of measurement data from `MOD-0063` warehouse models.
- Bindings to `MOD-0060` metric definitions and `MOD-0004` semantic identities.
- Handoff to the statistical computation runtime that computes experiment
  results.

No integration implementation is authorized.

## 16. Acceptance Criteria

Governance acceptance criteria (this pack):

1. AC-01: Pack status is `draft` (governance-first).
2. AC-02: Canonical identity is `MOD-0062` (Blueprint-backed; not a candidate).
3. AC-03: Registry row for `MOD-0062` exists with domain
   `data-knowledge-intelligence` and status `planned / pending-runtime`.
4. AC-04: The pack records that runtime service/domain ownership is PENDING EA.
5. AC-05: No runtime/frontend/gateway/service path is authorized or created.
6. AC-06: Analytics-backbone dependency relationships are recorded (depends on
   `MOD-0060`, `MOD-0004`, `MOD-0063`).
7. AC-07: Consumer context (ESBP, HCM `CAND-CAP-0034`) is recorded as
   non-owning.

Future runtime acceptance criteria are deferred to the runtime-authorizing
revision.

## 17. Test Expectations

Governance validation expectations:

- Confirm the pack file exists with all 20 sections.
- Confirm frontmatter `status: draft` and `id: MOD-0062`.
- Confirm the registry row exists and marks runtime as pending EA.
- Confirm NO `services/**`, `frontend/**`, or `gateway/**` changes were made.
- Confirm `MOD-0062` is not written as a runtime literal anywhere.

No runtime tests are authorized (there is no runtime).

## 18. Governance Approval Checklist

- [x] Blueprint-canonical identity accepted (`MOD-0062`).
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
- [ ] Baseline/experiment design contract + tenancy model design.
- [ ] Measurement-data read contract to `MOD-0063` and binding contract to
  `MOD-0060`/`MOD-0004`.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0062`
  cannot be run — recorded as a governance note (same waiver as the HCM packs).
- This is a governance-first pack: it deliberately authorizes no runtime; the
  DCP-008 pre-runtime requirement (registry row + module pack) is satisfied.

Next safe step:

- EA assigns the repo domain and the owning runtime service for the analytics
  backbone; a runtime-authorizing pack revision is then produced.

Runtime literal scan:

- `rg -n "MOD-0062" services frontend gateway` must return no runtime literal
  uses (governance/docs references only).
