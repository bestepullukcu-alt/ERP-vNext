---
id: DCP-006
slug: crm-commercial-delivery
name: CRM & Commercial Delivery
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: commercial-suite
owner: module-pack-author / enterprise-architect (pending)
branch: feature/crm-integration
created: 2026-08-25
canonical_source: "docs/System Capability & Implementation Blueprint - master 7.xlsx#Blueprint_Data"
canonical_source_warning: "Master 8.1 NOT present in repo (only 'master 7.xlsx' + a duplicate copy under commercial-suite). All canonical MOD/FU bindings below are proven against Master 7. Master 8.1 remains the intended business/model authority; re-verify on ingest."
inputs:
  - "execution/registries/module-id-registry.md (Commercial Suite reservation block, 27 IDs)"
  - "execution/domains/commercial-suite/crm-sor-boundary.md"
  - "execution/domains/commercial-suite/crm-build-lanes.md"
  - "execution/domains/commercial-suite/domain-config.md"
  - ".antigravity/scripts/verify_module_id.py (DCP-002 fail-closed gate)"
runtime_code_allowed: false
scope_note: "Governance/orchestration contract only. Mints NO runtime, NO MOD-xxxx, NO CAND-CAP registry row. Classifies a 26-item raw intake against canonical identity, sets the CRM-owned vs consumed boundary, and orders delivery. Awaiting user/EA approval."
---

# DCP-006 — CRM & Commercial Delivery (Delivery Capability Pack)

> **Artifact type:** Delivery Capability Pack (CAP-001 governance / orchestration contract). **NOT** a runtime entity, **NOT** a module pack, **NOT** a MOD-0014 runtime Capability Group.
>
> **Status guard:** `draft`. This pack classifies intake and proposes identities. It does **not** create packs, does **not** write runtime, and does **not** mint any `MOD-xxxx` or reserve any `CAND-CAP-####` registry row. FU-child preflights that pass are **proposals**, not reservations; CAND-CAP candidates that fail closed are **unresolved identity debt** pending Enterprise Architect allocation. Nothing here authorizes implementation.

## 0. Blueprint version notice (mandatory)

Rule 1 requires Master 8.1 as canonical. **Master 8.1 is absent from the repository** — only `docs/System Capability & Implementation Blueprint - master 7.xlsx` (and a duplicate `… master 7 (3).xlsx` under `commercial-suite/`) exist. Every canonical binding in this pack is therefore proven against **Master 7** via `verify_module_id.py`, whose `BLUEPRINT` constant also points at Master 7. **Action required:** re-run all preflights against Master 8.1 once it lands; treat any 8.1 divergence as authoritative over this draft.

## 1. Program scope

This is a **cross-cutting program**, not a single module pack: 10+ Blueprint modules across 5 capability groups (CRM Core, Sales, Marketing, CPQ & Pricing, Service) plus the Business Development group (Deal Desk) and an Order-to-Cash bridge touchpoint. It is delivered as an ordered set of module packs and FU children, most of which do **not exist yet** and must be authored separately under `@module-pack-author` after this pack is approved.

Owner domain: `commercial-suite`. Consumed dependencies stay owned by their canonical domains and are **boundary-only** here (CRM opens no aggregate / CRUD / UI for them).

## 2. Canonical classification of the 26-item intake

Legend for **Class**:
`canonical` = maps 1:1 to a Blueprint MOD (pack may or may not exist yet) · `already-exists` = a repo pack/FU already covers it → **reconcile, do not reproduce** · `needs-identity(FU)` = no pack yet, FU-child identity proposed + gate-passed · `needs-identity(CAND)` = no canonical/FU fit, CAND-CAP proposed, gate **fails closed** pending EA · `consumed` = owned by another domain, boundary-only · `not-a-module` = derived view / phase, no MOD minted.

### 2A. CRM-OWNED — MOD-0162 Knowledge Base cluster

| # | Raw item | Canonical binding | Class | Reconciliation note |
|---|----------|-------------------|-------|---------------------|
| 1 | ConceptGraph Runtime + UI | **MOD-0162-FU03** *Concept Graph Runtime + UI* (status `draft`) | already-exists | Pack present. Do **not** re-author. Concept model boundary is MOD-0162-FU01C (approved). Reconcile FU03 runtime scope against FU01C ownership. |
| 2 | KnowledgePath Runtime + UI | **MOD-0162-FU04** *KnowledgePath Runtime + UI* (proposed) | needs-identity(FU) | FU01A (`KnowledgePath / Content Sequence Boundary`, approved) is **boundary-only** (`runtime_code_allowed:false`). A runtime+UI layer on top of it is new work → new FU04. Gate exit 0. |
| 3 | ContentGraph scope | **MOD-0162-FU01C / FU03** (Concept Graph) — name reconciliation | already-exists (name-recon) — ✅ **CLOSED 2026-08-26** | "ContentGraph" is **not** a canonical name. It resolves to the existing Concept Graph model (FU01C boundary + FU03 runtime). No new identity. Rename intake to "Concept Graph". **Resolution:** no `ContentGraph` phantom identity exists in the registry, packs, or code (grep ∅, control-tower verified); MOD-0162-FU03 *Concept Graph Runtime + UI* is `status: done`. Nothing to build — intake renamed to "Concept Graph", item closed. |
| 4 | EngagementJourney → **ContentEngagementJourney** | **MOD-0162-FU01B** boundary now `status: approved` (F1 resolved 2026-08-26); runtime = **MOD-0162-FU05** (to author) | already-exists (boundary) → runtime FU next | F1 naming clash RESOLVED: canonical name **ContentEngagementJourney**, permanently separated from MOD-0166 *Journeys & Automation* and MOD-0113 *Journey Mapping* (§2.1). Runtime FU (MOD-0162-FU05) implements the boundary, embedded stages (FU04 pattern); prereqs FU02+FU04 shipped. |

### 2B. CRM-OWNED — MOD-0155 Field Sales / Visit Planning cluster (no pack yet)

MOD-0155 is a **single module**; the following are its FU children, not separate modules (rule confirmed).

| # | Raw item | Canonical binding | Class | Reconciliation note |
|---|----------|-------------------|-------|---------------------|
| 5 | Visit Planning / PlannedVisit | **MOD-0155-FU01** *Visit Planning / Planned Visit* (proposed) | needs-identity(FU) | Gate exit 0. MOD-0155 SoR = "visit plans". |
| 6 | Visit Report | **MOD-0155-FU02** *Visit Report* (proposed) | needs-identity(FU) | Gate exit 0. Legacy ActivityReport is the reference business-rule source (frozen). |
| 7 | Route Planning | **MOD-0155-FU03** *Route Planning* (proposed) | needs-identity(FU) | Gate exit 0. **Consumes** MicroZone from MOD-0151 (does not define it). |
| 8 | Visit Content Sequence | **MOD-0155-FU04** *Visit Content Sequence Execution* (proposed) | needs-identity(FU) | **Ownership split (clarified):** the *definition* of a content sequence = **MOD-0162-FU01A** (KnowledgePath). The *application/execution of that sequence during a visit* = **MOD-0155-FU04**, consuming MOD-0162. Intersection is a consume-relationship, not dual ownership. Gate exit 0. |
| 13 | MicroTarget | **MOD-0155-FU05** *MicroTarget* (proposed) | needs-identity(FU) | **Correction to intake:** intake filed MicroTarget under MOD-0167. Per `crm-sor-boundary.md` + `crm-build-lanes.md`, **MicroTarget is owned by MOD-0155** (Visit Plan / MicroTarget SoR). MOD-0167 owns *TargetCustomer* (row 23), a different object. Gate exit 0. |

### 2C. CRM-OWNED — Sales & Marketing core

| # | Raw item | Canonical binding | Class | Reconciliation note |
|---|----------|-------------------|-------|---------------------|
| 9 | Lead Management | **MOD-0152** *Lead Management* | canonical (reserved, pack pending) | Registry `reserved/planned`; no pack yet. Generic CRM core (greenfield). |
| 10 | Opportunity (+ Funnel) | **MOD-0153** *Opportunity & Pipeline Management*; Funnel = **MOD-0153-FU01** or in-module view | canonical (reserved) + needs-identity(FU) | Funnel is a pipeline view of MOD-0153; if broken out, FU01 gate exit 0. Recommend in-module first, FU only if UI warrants. |
| 11 | Forecasting / Pipeline | **MOD-0154** *Forecasting & Quotas* | canonical (reserved) | "Pipeline" belongs to MOD-0153; forecasting math = MOD-0154. Do not duplicate pipeline in 0154. |
| 12 | Segmentation / ICP Scoring | **MOD-0167** *Segmentation / CDP*; ICP Scoring = in-module feature/FU | canonical (reserved) | MOD-0167 SoR = segments; owns Segment/TargetCustomer/UCLN. ICP scoring is a scoring feature of 0167, not a separate MOD. MOD-0167-FU01 (*Segment-Sourced Frequency Policy Authoring*, draft) already exists — reconcile, do not reproduce. |
| 23 | Target Customer | **MOD-0167-FU02** *Target Customer* (proposed) | needs-identity(FU) | Rule-2 ambiguous name. SoR boundary assigns TargetCustomer to MOD-0167. Gate exit 0. |

### 2D. CRM-OWNED — Activity / Interaction / Commitment (Blueprint gap)

| # | Raw item | Canonical binding | Class | Reconciliation note |
|---|----------|-------------------|-------|---------------------|
| 14 | Activity / Interaction / Commitment | **CAND-CAP-0006** *Commercial Activity, Interaction and Commitment* (proposed) | needs-identity(CAND) | **No canonical Blueprint module exists.** Nearest names are non-CRM (MOD-0207 Quality Deviations, MOD-0302 HR Offer). Intake framed it as "MOD-0150 derivative", but MOD-0150 SoR = contacts, not activities → not a clean FU. Candidate gate **BLOCKED (exit 2)** — real identity debt; requires EA registry+ledger reservation before any ID. |
| 15 | Commercial Activity Timeline | derived view over CAND-CAP-0006 (+ MOD-0153/0155 events) | not-a-module | A read-only aggregated timeline view. No MOD minted; renders once the Activity capability and its source aggregates exist. |

### 2E. CRM-OWNED — CPQ / Commercial Terms

| # | Raw item | Canonical binding | Class | Reconciliation note |
|---|----------|-------------------|-------|---------------------|
| 16a | Pricing | **MOD-0156** *Price Lists & Discount Guardrails* | canonical (reserved) | — |
| 16b | Offer | **MOD-0157** *Quote Generation* | canonical (reserved) | "Offer" = Quote in CPQ context. **Not** MOD-0302 *Offer Management* (that is HR/Talent Acquisition). |
| 16c | Cost Model | **MOD-0156-FU01** *Cost Model* (proposed) | needs-identity(FU) | Cost-to-serve / margin basis for discount guardrails → FU of MOD-0156. Gate exit 0. |
| 17a | Commercial Terms / Approval | **MOD-0284** *Deal Desk & Commercial Approvals* | canonical (reserved) | MOD-0284 SoR = "Deal Exception; Approval Case". MOD-0158 *Quote-to-Contract Handoff* is the downstream handoff (not the approval owner). |
| 17b | Deviation Register | **MOD-0284-FU01** *Deviation Register* (proposed) | needs-identity(FU) | **Correction:** not MOD-0207 *Deviations/Nonconformances* (that is Quality Operations). Commercial deviation = Deal Exception register → FU of MOD-0284. Gate exit 0. |
| 24 | Commercial Model / Proposition Routing | **CAND-CAP-0007** *Commercial Model and Proposition Routing* (proposed) | needs-identity(CAND) | Rule-2 ambiguous name; no canonical fit (candidate overlaps MOD-0283 Pursuit/Proposal and MOD-0284 routing but matches neither cleanly). Candidate gate **BLOCKED (exit 2)** — identity debt pending EA. |

### 2F. CONSUMED DEPENDENCY (CRM builds nothing — API consumer only, boundary-only)

| # | Raw item | Canonical owner | Class | Correction / note |
|---|----------|-----------------|-------|-------------------|
| 18 (#22) | Contract / Obligation | **MOD-0217** *Contract Lifecycle Management (CLM)* + **MOD-0219** *Obligations & Renewal Tracking* | consumed | **Correction to intake:** Obligation is **MOD-0219** (Legal Governance, W-4), a module distinct from CLM MOD-0217 (W-3) — not folded into 0217. Both are `Legal & Contracts` suite. CRM consumes both read-only. |
| 19 (#24) | Counterparty-Facing Documents / Controlled Template | **MOD-0028** *Documentation & Evidence Management* + **MOD-0029** *Controlled Documents* | consumed | Confirmed (`Content Lifecycle`). CRM opens no document aggregate/CRUD/UI. |
| 20 (#26) | Records / Retention / Legal Hold / Access Review | **MOD-0030** *Records Management (Retention/Legal Hold)*; Access Review → access-governance (DCP-001 / MOD-0018 family) | consumed | Records/Retention/Legal Hold = MOD-0030 (confirmed). **Correction:** intake's "+MOD-0019" = *Data Masking & Row/Field Security*, which is field-level security, **not** Access Review. Access Review (recertification) is an access-governance concern → DCP-001 / MOD-0018 family, **EA-TBD**, not CRM. |
| 21 (#18) | Counterparty Due Diligence / Screening | **MOD-0270** *Sanctions/KYC Screening Provider* + **MOD-0275** *Fraud/AML Screening Provider* (External Providers) | consumed | Confirmed CRM-external. Screening is an external-provider capability; CRM consumes results only. |

### 2G. PHASE (not a module)

| # | Raw item | Modeled as | Class | Note |
|---|----------|-----------|-------|------|
| 25 (#25) | Integration Wave | Delivery phase / wave (§5) | not-a-module | No MOD minted. Modeled as the cross-cutting integration delivery wave that lands after the CRM-owned aggregates exist. |

## 3. CRM-owned vs consumed boundary (explicit)

**CRM-owned (this program builds):** MOD-0149, MOD-0150, MOD-0151, MOD-0152, MOD-0153, MOD-0154, MOD-0155 (+FU01–FU05), MOD-0156 (+FU01), MOD-0157, MOD-0158, MOD-0162 (+FU03 reconcile, +FU04 new; FU01/FU01A/FU01B/FU01C/FU02 already exist), MOD-0164 (already scaffolded), MOD-0165, MOD-0167 (+FU02), MOD-0284 (+FU01), and — pending EA — CAND-CAP-0006, CAND-CAP-0007.

**Consumed, boundary-only (CRM builds NO aggregate/CRUD/UI):** MOD-0217, MOD-0219, MOD-0028, MOD-0029, MOD-0030, MOD-0270, MOD-0275, and the platform primitives already fixed in `crm-sor-boundary.md` (MOD-0048 reference data, MOD-0288 org/person, MDM product/SKU, MOD-0018 auth). Access Review = EA-TBD (not CRM).

## 4. Delivery / dependency order (foundation-first)

Rule-4 dependency chain, layered on Blueprint waves:

1. **Lane 0 — crm-platform-readiness (P0 blocker, not a module):** MOD-0018 RBAC integration · MOD-0048 reference-data readiness · MOD-0288 org/person · **MOD-0018-FU15 Real DataScopeResolver** (territory/field-force scoping is blocked until FU15 lands).
2. **Account 360** → MOD-0149 *(pack: ready-for-dev)* — W-1 foundation.
3. **Contact** → MOD-0150 *(pack: ready-for-dev)* — depends on 0149.
4. **Territory** → MOD-0151 *(pack: ready-for-dev, FU01–FU09A)* — depends on 0149/0150 + FU15; **defines MicroZone**.
5. **Lead / Opportunity** → MOD-0152, MOD-0153 (+Funnel view) — depends on 0149/0150.
6. **Visit** → MOD-0155-FU01…FU05 — depends on 0151 (MicroZone consume) + MOD-0162-FU01A (content sequence). **MicroTarget = FU05 here.**
7. **CPQ / Terms** → MOD-0156 (+Cost Model FU01), MOD-0157, MOD-0158, MOD-0284 (+Deviation Register FU01) — depends on account/opportunity + MDM product master (consume).
8. **Forecasting** → MOD-0154 — depends on 0153 pipeline data.
9. **Marketing/Knowledge parallel track:** MOD-0164 (consent, early W-2, already scaffolded) · MOD-0167 (+TargetCustomer FU02, ICP) · MOD-0165 (campaign, already scaffolded FU01/FU02/FU05) · MOD-0162 Knowledge (reconcile FU03, add FU04).
10. **Activity capability (EA-gated):** CAND-CAP-0006 — sequence after MOD-0150/0153/0155 exist so the *Commercial Activity Timeline* derived view has real sources.
11. **Integration Wave (#25):** cross-cutting integration/contract standardization phase — lands after the owned aggregates are stable and their Minimum Integration Contracts are published.

> Note: authoring order ≠ Blueprint wave order. Field Sales (MOD-0155) is the highest legacy value but Blueprint W-4 — its **pack/preservation prep is early, implementation is late** (per `crm-build-lanes.md`).

## 5. Identity-debt register (verify_module_id.py results)

Preflights run 2026-08-25 against Master 7 (Master 8.1 absent). FU-child = `--check-id … --parent …`; candidate = `--candidate …`.

| Proposed identity | Name | Mode | Result | Meaning |
|---|---|---|---|---|
| MOD-0162-FU04 | KnowledgePath Runtime + UI | FU/MOD-0162 | **exit 0 — proven** | May author pack after approval. |
| MOD-0155-FU01 | Visit Planning / Planned Visit | FU/MOD-0155 | **exit 0 — proven** | idem |
| MOD-0155-FU02 | Visit Report | FU/MOD-0155 | **exit 0 — proven** | idem |
| MOD-0155-FU03 | Route Planning | FU/MOD-0155 | **exit 0 — proven** | idem |
| MOD-0155-FU04 | Visit Content Sequence Execution | FU/MOD-0155 | **exit 0 — proven** | idem |
| MOD-0155-FU05 | MicroTarget | FU/MOD-0155 | **exit 0 — proven** | idem |
| MOD-0167-FU02 | Target Customer | FU/MOD-0167 | **exit 0 — proven** | idem |
| MOD-0156-FU01 | Cost Model | FU/MOD-0156 | **exit 0 — proven** | idem |
| MOD-0284-FU01 | Deviation Register | FU/MOD-0284 | **exit 0 — proven** | idem |
| MOD-0153-FU01 | Funnel | FU/MOD-0153 | **exit 0 — proven** | Optional; prefer in-module pipeline view first. |
| CAND-CAP-0006 | Commercial Activity, Interaction and Commitment | candidate | **exit 2 — BLOCKED (fail-closed)** | No registry row / no ledger entry. **Unresolved identity debt** — EA must reserve the row before any ID is used. |
| CAND-CAP-0007 | Commercial Model and Proposition Routing | candidate | **exit 2 — BLOCKED (fail-closed)** | idem. EA reservation required. |

FU-child preflight passing means the ID is *mintable*, not *minted*. No registry row is written by this pack. CAND-CAP-0006/0007 next free in the `CAND-CAP-####` namespace (0001–0005 taken); reserving them is an EA action recorded in `execution/registries/module-id-registry.md` + `execution/portfolio/blueprint-master-plan-reconciliation.md`.

## 6. Already-exists packs — reconcile, do NOT reproduce

| Pack | Status | Action |
|---|---|---|
| MOD-0162-FU01 Knowledge Content & Subject Taxonomy | approved | keep; foundation for FU04 |
| MOD-0162-FU01A KnowledgePath/Content Sequence Boundary | approved | boundary source for new FU04 + MOD-0155-FU04 |
| MOD-0162-FU01B EngagementJourney Boundary | draft | intake item 4 → this pack; name-reconcile vs MOD-0166/0113 |
| MOD-0162-FU01C Subject Concept Graph Boundary | approved | concept model owner for FU03 |
| MOD-0162-FU02 Knowledge/Content Runtime + UI | done | shipped; do not touch |
| MOD-0162-FU03 Concept Graph Runtime + UI | draft | intake item 1 → reconcile runtime scope vs FU01C |
| MOD-0164-FU01/FU03 Consent boundary + Admin UI | draft / ready-for-dev | consent track, already scaffolded |
| MOD-0165-FU01/FU02/FU05 Visit-frequency + Campaign targeting | draft/draft/review | campaign track, already scaffolded |
| MOD-0167-FU01 Segment-Sourced Frequency Policy | draft | segmentation track; FU02 (TargetCustomer) is additive |
| MOD-0149 / MOD-0150 / MOD-0151 module packs | ready-for-dev | foundation packs exist |

## 7. Open EA-TBD / follow-ups

- **HCP identity SoR** (doctor/pharmacist/hospital identity: CRM MOD-0149 vs MDM master) — EA-TBD (`crm-sor-boundary.md`).
- **O2C bridge SoR** (MOD-0169/0170/0171/0172 shared with Finance/Order-Mgmt) — EA-TBD. Out of this pack's CRM-owned build scope.
- **Access Review owner** (#26 tail) — access-governance (DCP-001/MOD-0018 family), not CRM, not MOD-0019. EA-TBD.
- **CAND-CAP-0006 / CAND-CAP-0007** reservation — EA action (registry + ledger) before any pack authoring.
- **Master 8.1 ingest** — re-verify every binding when 8.1 lands; 8.1 is authoritative over Master-7 bindings here.
- **Funnel** — decide in-module (MOD-0153) view vs FU01 before authoring.

## 8. Acceptance-criteria coverage (self-check)

- ✅ Every intake capability classified: `canonical | already-exists | needs-identity(FU/CAND) | consumed | not-a-module` (§2, rows 1–25 incl. rule-2 items ContentGraph, MicroTarget, Activity/Interaction/Commitment, Commercial Activity Timeline, Target Customer, Commercial Model/Proposition Routing).
- ✅ CRM-owned vs consumed boundary written (§3); consumed items marked boundary-only.
- ✅ Delivery order / waves written (§4); Integration Wave modeled as a phase (§2G).
- ✅ Identity-debt items carry FU/CAND proposal + `verify_module_id.py` result (§5); no ID minted, no registry row written; candidates fail closed as required.
- ✅ Existing packs reconciled, not reproduced (§6).
- ✅ `status: draft` — awaiting user/EA approval.

## Handoff

Module/capability classification is `draft`. Please review §2 corrections (MicroTarget→MOD-0155, Obligation→MOD-0219, Deviation Register→MOD-0284, screening→MOD-0270/0275, Access Review→EA-TBD) and the two fail-closed candidates (CAND-CAP-0006/0007). On approval: (1) EA reserves CAND-CAP-0006/0007 rows; (2) proposed FU packs are authored individually via `@module-pack-author`; (3) all bindings re-verified against Master 8.1 on ingest. No implementation is authorized by this pack.
