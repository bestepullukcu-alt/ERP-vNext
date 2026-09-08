---
id: MOD-0061
name: Scorecards / Dashboards
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
runtime_owner_key: pending (proposed dki.scorecards-dashboards)
permission_namespace:
  - pending (proposed dki.scorecards-dashboards.read)
  - pending (proposed dki.scorecards-dashboards.manage)
runtime_repo_scope: UNASSIGNED (pending EA service assignment)
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0061 - Scorecards / Dashboards

> Status: draft — GOVERNANCE-FIRST scoping pack. This pack authors the required
> registry row and module-pack governance artifact for the Blueprint-canonical
> `MOD-0061 Scorecards / Dashboards` (Blueprint domain **Data, Knowledge &
> Intelligence**), per DCP-008's verdict that "registry owner and module-pack
> authoring [are] required before runtime work". **No runtime, service,
> frontend, or gateway is authorized by this pack.** `MOD-0061` is
> Blueprint-canonical (not a candidate); the canonical MOD ID is used directly.
> Repo domain ownership and the owning runtime service remain **PENDING EA
> domain mapping** (DCP-008 lists the Analytics backbone as "Pending EA domain
> mapping"). Runtime is blocked until EA assigns a domain and a service.

## 1. Module Summary

`MOD-0061` is the Blueprint-canonical **Scorecards / Dashboards** module: the
composition and presentation layer of the analytics backbone that arranges
curated KPIs (`MOD-0059`) and metric definitions (`MOD-0060`) into scorecards
and dashboards — scorecard/dashboard identity, layout and composition metadata,
tile/widget references to KPIs and metrics, and sharing/visibility scoping.

It composes *references* to already-owned analytics concepts; it is **not** a
rendering engine and stores **no** measured or computed data. Its place in the
analytics-backbone group (`MOD-0004`, `MOD-0059`–`MOD-0064`) is directly
downstream of KPI and metric ownership: `MOD-0004 Metric & Semantic Registry`
defines the shared vocabulary, `MOD-0060 Metric Definitions & Ownership` authors
concrete metric definitions, `MOD-0059 KPI Catalog` curates KPIs from those
definitions, and `MOD-0061` arranges those KPIs and metrics into scorecard and
dashboard compositions. `MOD-0062 Baseline & Experiment Measurement` measures
against the definitions, while `MOD-0063 Data Warehouse / Lakehouse` and
`MOD-0064 ETL / ELT Pipelines` provide the physical data plane. `MOD-0061`
holds only composition metadata and tile references — never the underlying
values.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and explicitly
marks its domain ownership as "Pending EA domain mapping". This pack fulfils the
DCP-008 pre-runtime requirement (registry row + module pack) and records the
open EA decisions, without authorizing any runtime.

## 2. Ownership and Boundaries

Owned by this module (future runtime, once EA assigns domain/service):

- Scorecard and dashboard identity (canonical id, name, type).
- Composition and layout metadata: how tiles/widgets are arranged, grouped,
  ordered, and sized on a scorecard or dashboard.
- Tile/widget references that point at KPIs (`MOD-0059`) and metric definitions
  (`MOD-0060`) — references only, not the definitions or their values.
- Sharing and visibility scoping of a scorecard/dashboard (who may see it).

Not owned by this module:

- KPI definitions — owned by `MOD-0059 KPI Catalog`.
- Metric definitions, formulas, thresholds, and ownership — owned by
  `MOD-0060 Metric Definitions & Ownership`.
- The shared semantic vocabulary — owned by `MOD-0004 Metric & Semantic
  Registry`.
- Physical storage and query, and data movement/transformation — owned by
  `MOD-0063 Data Warehouse / Lakehouse` and `MOD-0064 ETL / ELT Pipelines`.
- The actual chart/visual rendering runtime — `MOD-0061` composes references and
  is explicitly **not** a rendering engine.
- ESBP goal-scoped scorecards — a **distinct** consumer surface living in
  `Diten.EnterpriseStrategyService`. Those goal-scoped strategy scorecards are
  separate from `MOD-0061`'s general analytics-backbone scorecards/dashboards;
  ESBP is a *consumer* of composition concepts, not this module and not owned by
  it.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` — a *consumer* facade that
  owns no analytics runtime.

## 3. Owned Objects

Governance-level objects (runtime shapes deferred until EA assigns a service):

| Object | Type | Purpose |
|---|---|---|
| ScorecardIdentity | Governance concept | Canonical, stable identity of a scorecard (id, name, type). |
| DashboardIdentity | Governance concept | Canonical, stable identity of a dashboard (id, name, type). |
| CompositionLayout | Governance concept | Layout/arrangement metadata for tiles/widgets on a scorecard or dashboard. |
| TileReference | Governance concept | A tile/widget reference pointing at a KPI (`MOD-0059`) or metric definition (`MOD-0060`). |
| VisibilityScope | Governance concept | Sharing/visibility scoping of a scorecard/dashboard. |
| CompositionVersion | Governance concept | Version/lineage of a scorecard/dashboard composition. |

Deferred runtime objects (NOT authorized by this pack):

- Any entity/controller/repository/service scaffold — deferred until EA assigns
  a domain and a runtime service.

## 4. Entity Fields / Contract

This is a governance-first pack. **No runtime entity fields are authorized.**
The intended composition contract (for a future EA-approved runtime slice)
centres on: scorecard/dashboard identity, composition/layout metadata,
tile/widget references to KPIs and metric definitions, visibility scope, and
version/lineage — all metadata about *composition and presentation*, never
measured values, embedded datasets, report content, or PII.

Reserved / out-of-scope for any first runtime slice:

- No measured/computed values, no embedded datasets, no report content.
- No money field, no PII-heavy field.
- No chart rendering runtime, no physical data (that is `MOD-0063`).

## 5. Repo Scope

Authorized governance scope (this pack only):

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0061-scorecards-dashboards.md`
- The `MOD-0061` row in `execution/registries/module-id-registry.md`.

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

- Depends on: `MOD-0059` (KPI Catalog) — `MOD-0061` tiles reference curated
  KPIs.
- Depends on: `MOD-0060` (Metric Definitions & Ownership) — `MOD-0061` tiles
  reference concrete metric definitions.
- Upstream vocabulary (transitive): `MOD-0004` (Metric & Semantic Registry).
- Physical-plane (not a direct dependency of composition metadata): `MOD-0063`
  (Data Warehouse / Lakehouse), fed by `MOD-0064` (ETL / ELT Pipelines).

Consumer context (not dependencies of this module, they depend on / relate to
it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped scorecard runtime — a
  distinct consumer surface, non-owning.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade — a consumer facade, non-owning.

## 8. Runtime Guard

**Runtime is BLOCKED.** No API/controller/entity/repository/database/service
scaffold is authorized by this pack. Runtime may be authorized only after:

1. EA assigns the repo domain ownership (currently "Pending EA domain mapping").
2. EA assigns the owning runtime service (no analytics/data-platform service
   exists today; a dedicated data-platform service or an explicit assignment is
   required).
3. A runtime-authorizing pack revision (or successor pack) is approved.

Until then, `MOD-0061` must not appear as a runtime literal, owner key, or
permission namespace in any code.

## 9. Frontend / UX Boundary

No frontend/UX is authorized. Any future admin/authoring surface (scorecard and
dashboard composition editor) is deferred to a runtime-authorizing revision
under the EA-assigned service. Note that composition metadata is distinct from
the chart-rendering runtime, which this module does not own.

## 10. Backend / API Boundary

No backend/API is authorized. The intended future surface (scorecard/dashboard
composition CRUD, tile/reference management, visibility scoping, version/lineage)
is recorded for scoping only and is blocked pending EA service assignment.

## 11. Data Boundary

No persistence is authorized. When runtime is later approved, the module owns
only *composition and layout metadata plus references* to KPIs and metrics,
never measured/computed values, embedded datasets, report content, money, or
PII — those belong to other modules (`MOD-0059/0060/0062/0063`) or are forbidden
outright.

## 12. Permission Boundary

Proposed (not yet active) permission namespace, pending EA service/owner-key
assignment:

- `<service>.scorecards-dashboards.read`
- `<service>.scorecards-dashboards.manage`

No permission is seeded by this pack.

## 13. Tenant / Security Boundary

Future runtime must be tenant-aware and fail closed, resolving tenant
server-side. Scorecards/dashboards are expected to be tenant-scoped, with
sharing/visibility scoping enforced server-side — the exact tenancy and sharing
model is an open design decision for the runtime-authorizing revision.

## 14. Compliance / Privacy Boundary

Composition metadata and references only; no PII, no measured data. Retention,
evidence, and audit of composition changes (scorecard/dashboard lineage and
sharing changes) is a design item for the runtime revision.

## 15. Integration Boundary

Deferred/out-of-scope until EA assignment:

- Reference contracts to `MOD-0059` KPIs and `MOD-0060` metric definitions.
- Handoff to the (separately owned) chart-rendering runtime.
- Consumption contracts published to ESBP goal-scoped scorecards and HCM
  facades.

No integration implementation is authorized.

## 16. Acceptance Criteria

Governance acceptance criteria (this pack):

1. AC-01: Pack status is `draft` (governance-first).
2. AC-02: Canonical identity is `MOD-0061` (Blueprint-backed; not a candidate).
3. AC-03: Registry row for `MOD-0061` exists with domain
   `data-knowledge-intelligence` and status `planned / pending-runtime`.
4. AC-04: The pack records that runtime service/domain ownership is PENDING EA.
5. AC-05: No runtime/frontend/gateway/service path is authorized or created.
6. AC-06: Dependency relationships on `MOD-0059` (KPI Catalog) and `MOD-0060`
   (Metric Definitions & Ownership) are recorded.
7. AC-07: Consumer context (ESBP distinct goal-scoped scorecards, HCM
   `CAND-CAP-0034`) is recorded as non-owning.

Future runtime acceptance criteria are deferred to the runtime-authorizing
revision.

## 17. Test Expectations

Governance validation expectations:

- Confirm the pack file exists with all 20 sections.
- Confirm frontmatter `status: draft` and `id: MOD-0061`.
- Confirm the registry row exists and marks runtime as pending EA.
- Confirm NO `services/**`, `frontend/**`, or `gateway/**` changes were made.
- Confirm `MOD-0061` is not written as a runtime literal anywhere.

No runtime tests are authorized (there is no runtime).

## 18. Governance Approval Checklist

- [x] Blueprint-canonical identity accepted (`MOD-0061`).
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
- [ ] Composition contract + tenancy/sharing model design.
- [ ] Reference contracts to `MOD-0059` KPIs and `MOD-0060` metric definitions.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0061`
  cannot be run — recorded as a governance note (same waiver as the HCM packs).
- This is a governance-first pack: it deliberately authorizes no runtime; the
  DCP-008 pre-runtime requirement (registry row + module pack) is satisfied.

Next safe step:

- EA assigns the repo domain and the owning runtime service for the analytics
  backbone; a runtime-authorizing pack revision is then produced.

Runtime literal scan:

- `rg -n "MOD-0061" services frontend gateway` must return no runtime literal
  uses (governance/docs references only).
