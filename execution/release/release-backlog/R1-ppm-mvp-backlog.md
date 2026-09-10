# Release Backlog: R1 PPM MVP

## Purpose and authority

This matrix records fourteen Project Workspace planning needs from the preserved ES review, reconciled
against main `db2e2ef2781d94ca5bbc56ee419f1ac88125c6d9`. It is not implementation authorization, a locked
release commitment or a completion record. Product Owner / PMO retains release planning and update
coordination; business acceptance belongs to the Portfolio Governance Process Owner (PPM Business Owner).

The [MOD-0117 pack][ppm] is the implementation authority; the [domain config][domain] provides broader
context. Where they differ, the more specific pack governs. The current slice does not authorize PPM task
instances or scheduling. This backlog creates no module identity, endpoint, permission or owner decision.
It is not the canonical Module ID Registry or the general wave roadmap.

## Evidence and status meaning

Historical planning input: `execution/release/release-backlog/R1-ppm-mvp-backlog.md` inside
`/Users/alitufanoglu/ERP-vNext/.git-backups/es-preservation-20260908-144837-1c083b78/tracked-files.tar.gz`.
The fourteen panel needs were retained; historical phase, ownership and model claims were re-evaluated.

- **Mevcut / Existing:** the named narrow surface exists in source; live acceptance remains open.
- **Kısmi / Partial:** supporting PPM implementation exists, but the Project Workspace relationship or panel
  is not implemented or authorized.
- **Ertelenmiş / Deferred:** the Workspace panel is absent and depends on its listed owner/contract gates.

The [Workspace view][workspace] contains only Overview and Charter. Its [script][workspace-js] reads the
saved Project and its Initiative or Program parent. The [Project model][project] has identity, description,
parent, lifecycle and visibility fields, with no schedule, progress, financial or deliverable-acceptance fields.

Gate L implements separate InvestmentCase and BenefitCommitment aggregates and tenant surfaces:
one Portfolio has many InvestmentCases; each BenefitCommitment belongs to exactly one InvestmentCase.
Gate I's bounded contracts do not establish an activated integration or a Project association. The pack's
later §4.9 amendments and §12 gate summary govern its default-off state. Historical phase wording and
BenefitValueLink must not replace the current model or imply completion.

## Project Workspace connection matrix

These are future, testable acceptance conditions, not tests executed in this documentation update.
Deferred status concerns the Workspace panel, not the whole owner module.

| Panel / user need | Owner or explicit open decision | Required connection | Entry condition | Testable acceptance condition | Status |
|---|---|---|---|---|---|
| **Overview and Charter** — understand the saved project's identity and context | MOD-0117 [pack][ppm] | Existing Project and Initiative/Program parent reads through the same-origin PPM proxy | Existing field/permission scope; business and human UX acceptance of this narrow surface | Authorized Project shows its actual code, name, description, lifecycle, visibility, referenceability and parent; invalid/invisible references disclose no foreign record; no fabricated dates, progress or KPI | **Mevcut** — narrow source surface; live acceptance open |
| **Structure / WBS** — inspect decomposition and structural dependencies | MOD-0354 [DWS pack][dws] | MOD-0117 `ppm.external-context-reference` and an owner-approved structural read/navigation contract | Bilateral provider/security evidence and separate DWS frontend/consumer authority; default-off DWS code alone is insufficient | Structure resolves to the correct same-tenant Project; denied/unavailable references disclose no structure; DWS nodes/baselines imply no schedule, dates, progress or deliverable acceptance | **Ertelenmiş** |
| **Project Tasks** — find and initiate associated work | Generic task/checklist: MOD-0024; PPM-specific task-instance scope requires a separate business-owner/pack decision. WorkCenter is projection/overlay only [task pack][tasks] | Exact typed Project context and declared source-provider read/create contract; separately admitted WorkCenter projection if needed | Owner/lifecycle decision, executable task contract and applicable DCP-004/Gate 2 evidence before hazardous actions | Provider owns task identity, assignment, lifecycle and effective actions; creation validates the same-tenant Project reference; Workspace/WorkCenter creates no competing task or approval state | **Ertelenmiş** |
| **Approvals** — see authoritative approval state and permitted actions | MOD-0023 [approval pack][approvals] | Exact versioned approval/workflow contract for Project context | Bilateral Project-specific contract and applicable Gate 2/runtime authority; InvestmentCase ApprovalOutcome remains blocked/ExcludedV1 and is not a Project contract | Only authoritative effective actions appear and denied actions cannot execute; neither task status nor PPM lifecycle implies approval; PPM/WorkCenter owns no approval decision | **Ertelenmiş** |
| **Decisions** — find the rationale governing a project | MOD-0007; MOD-0117 consumes references [pack §4.8–4.9][ppm] | Owner-approved Project-to-decision reference/read contract | Project cardinality/context agreed; existing InvestmentCase decision wrappers cannot be relabelled as Project links | Visible decisions resolve the exact owner revision and tenant/context; hidden/unavailable decisions disclose no content; PPM has no duplicate decision log | **Ertelenmiş** |
| **Budget and Financials** — inspect authorized funding and financial context | Budget MOD-0136; scenario/comparator MOD-0138; other metrics require their owner's decision [pack][ppm] | Gate I-B InvestmentCase references only where that context applies; Project relationship needs separate approval. Portfolio integration uses the [existing single tracking record](#existing-portfolio-budget-tracking-record) | Financial custody, Project cardinality, executable bilateral contracts and activation evidence; no duplicate Portfolio–Budget item | Source identity/version/permissions are respected; no copied budget, actuals, forecast or selected-baseline truth; missing sources produce no synthetic amounts | **Kısmi** — bounded Gate I-B support; Project panel/relationship deferred |
| **Benefits and Value** — distinguish planned commitments from realized outcomes | InvestmentCase/BenefitCommitment MOD-0117; outcome/realization MOD-0072 [pack][ppm] | Approved Project-to-investment/benefit relationship, then the existing BenefitCommitment outcome-reference boundary where applicable | Business-owner Project cardinality/visibility decision and bilateral MOD-0072/runtime gates | Commitments resolve through their real InvestmentCase/Portfolio relationship; realized values retain MOD-0072 provenance; absent Project linkage is unavailable, never inferred; no BenefitValueLink restoration | **Kısmi** — Gate L and bounded Gate I-C support; Project panel/relationship deferred |
| **Resources and Capacity** — understand demand and allocation | Resource/capacity owner and canonical identity remain an EA/business-owner decision; MOD-0117 excludes allocation/scheduling [pack][ppm] | Owner-approved demand/allocation read and planner navigation contract | Owner/identity, units, aggregation meaning, permissions and executable contract agreed | Totals reconcile to owner data, units and as-of state; hidden allocations are not exposed; PPM stores no competing assignment, utilization or scheduling truth | **Ertelenmiş** |
| **Deliverables, Documents and Evidence** — inspect expected outputs and supporting records | Deliverable definition/acceptance owner open. Metadata MOD-0028; controlled document/version MOD-0029; evidence linking/completeness MOD-0031; binary provider needs its approved contract [documents][documents], [task boundary][tasks] | Separately agreed deliverable relation and minimal document/version/evidence references with authorized content navigation | Deliverable semantics/acceptance owner decided; owner contracts and access/download rules executable | Links resolve the correct tenant and exact record/version; owner supplies evidence completeness; PPM stores no binary/version copy; DWS baseline existence is not deliverable acceptance | **Ertelenmiş** |
| **RAID** — inspect risks, assumptions, issues and dependencies | Fields, lifecycle, scoring and owner remain a PPM business-owner/EA decision; structural dependencies stay MOD-0354 and decisions MOD-0007 | Approved typed owner links; a local register needs separately approved scope | Explicit ownership/pack scope and scoring definitions without overlap with task/approval/compliance domains | Every entry identifies its kind and owner; scores are reproducible from approved rules; structural dependencies are not scheduling dependencies; no hidden task/compliance lifecycle | **Ertelenmiş** |
| **Compliance** — see applicable controls/findings and status | Canonical control/finding/policy owner remains an EA/business-owner decision | Approved control/finding references, visibility and status provenance | Owner/identity, applicability rules and executable contract accepted | Status matches the authoritative record/provenance; inaccessible findings disclose no metadata; PPM computes no independent compliance verdict | **Ertelenmiş** |
| **Recent Activity and Audit** — understand who changed project data | Immutable audit MOD-0021; PPM producer-local audit intent/outbox is technical evidence [pack §8][ppm] | Owner-approved, actor-safe Project audit read projection; producer delivery is not a Workspace query contract | MOD-0021 query/access contract and required delivery/runtime evidence | Each event matches the requested tenant/Project and an authoritative accepted event; actor visibility policy applies; retries/outbox state never becomes business lifecycle | **Ertelenmiş** — producer foundation exists; Workspace audit reader absent |
| **Related / Child Projects** — navigate legitimate relationships | MOD-0117; Project-to-Project relationship remains a business/EA decision | Current Project parent is Initiative or Program only [model][project-parent]; related/child links need approved scope/contract | Relationship meaning, cardinality and lifecycle consequences decided | Current parents remain Initiative/Program; no inferred SubProject or Project parent; any later approved relation rejects foreign/invisible and structurally invalid links | **Ertelenmiş** — current parent display belongs to Overview |
| **Complete Project Dashboard** — combine reliable delivery summaries | Each source retains its state; MOD-0117 owns only approved composition | Authorized projections with source/as-of/error information | Each panel passes its own gate; schedule/milestone/progress metrics require an explicit owner/contract, never assignment to DWS | Cards reconcile to named source/timestamp; loading, empty, denied and unavailable are distinct; missing integrations never show fake zeroes, sample progress or false overall completion | **Ertelenmiş** |

Count: **14 panels — 1 mevcut, 2 kısmi, 11 ertelenmiş**. No row is marked complete.

## Existing Portfolio–Budget tracking record

The single tracking source is §10.5, **“Bağlantı backlog'u — Portfolio ↔ MOD-0136 Bütçe”**, in
[the PPM Control Tower plan](../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-budget-integration).

The plan was absent from the fixed main SHA above and is included in this same feature delivery under
the user's explicit scope extension. The repository-relative link resolves within this worktree and does
not depend on another developer's local checkout. Inclusion here does not claim a completed main merge.
Its state remains **OPEN: business decision and executable owner contract pending**.
Budget implementation alone does not close the Portfolio integration.
The record retains financial custody, version/currency meaning, mutation-time validation and acceptance
requirements; this delivery does not implement its Portfolio budget-ceiling gate.

The tracking record is documentation only; this delivery grants no budget implementation authority.
Do not open another Portfolio–Budget item. Reciprocal references in the MOD-0117 and MOD-0136
packs remain for their owners' permitted amendment round, outside this delivery.

## Open decisions and review rule

- Project task admission/native ownership, resource/capacity ownership, deliverable acceptance, RAID,
  compliance and Project-to-Project relationships remain explicitly open.
- InvestmentCase contracts do not establish Project relationships. Cross-module panels require bilateral
  cardinality, visibility, provenance and error-behavior decisions.
- DWS receives no schedule/date/progress/deliverable-acceptance ownership. WorkCenter may present source
  projections and personal overlay; it owns neither task nor approval truth.
- Future module-pack, global-backlog or seam-registry changes require exact separately authorized scope.
  This file widens no such scope and does not launch governance v1.5.7.
- Product Owner / PMO must decide release inclusion after the gates and user acceptance close. Automated
  tests and builds alone cannot establish live Workspace acceptance.

[ppm]: ../../domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md
[domain]: ../../domains/portfolio-delivery/domain-config.md
[dws]: ../../domains/management-governance/module-packs/MOD-0354-decomposition-work-structuring-engine.md
[tasks]: ../../domains/platform-shared-services/module-packs/MOD-0024-task-checklist-engine.md
[approvals]: ../../domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md
[documents]: ../../domains/platform-shared-services/module-packs/MOD-0028-document-management.md
[workspace]: ../../../frontend/Diten.Web/Views/PPM/Projects/Workspace.cshtml
[workspace-js]: ../../../frontend/Diten.Web/wwwroot/assets/js/PPM/Projects/workspace.js
[project]: ../../../services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/Project.cs
[project-parent]: ../../../services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/ProjectParentType.cs
