# Blueprint-Master Plan Reconciliation

## Purpose
This document acts as the reconciliation tracking file between the high-level business capabilities (Enterprise Blueprint) and the technical module development plan. It ensures mapping alignment and guards against missing dependencies, Source of Record (SoR) collisions, page coverage issues, and release scope drift.

## Scope
Maintains mapping records, coverage status, and reconciliation governance rules between `execution/portfolio/enterprise-blueprint.md` and `execution/portfolio/master-development-plan.md`.

## Not the source for
- Writing code or module packages (use Module Packs).
- Individual module specifications or schemas (use docs/reference/modules/ or Module Packs).

## Current status
Active. The Blueprint↔Repo canonicalization ledger and the unresolved EA reservation ledger (below) are established per **DCP-002 — Module Identity Canonicalization**. Blueprint (`docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx`, `Blueprint_Data`) is the canonical MOD-ID + name authority. Canonical IDs not yet allocated by the Enterprise Architect remain **unresolved** (no placeholder IDs are assigned).

## Blueprint ↔ Repo Canonicalization Ledger (DCP-002, applied)

Documentation-only canonical alignment already applied to the working tree. Old IDs are deprecated aliases in [module-id-registry.md](../registries/module-id-registry.md) (chain resolves).

| Prior Repo ID | Prior Name | Canonical ID | Canonical Name | Type | Runtime literal touched? |
|---|---|---|---|---|---|
| MOD-0040 | Tenant Organization Foundation | **MOD-0288** | Organization, Person & Position Directory | rename (Blueprint canonical) | No (none in code) |
| MOD-0040-FU01 | Position Assignment User Reference Validation | **MOD-0288-FU01** | Position Assignment User Reference Validation | rename (child) | No |
| MOD-0043 | Tenant Architecture Foundation | **MOD-0009-FU01** | Tenant Architecture Foundation | tenant decomposition (child of MOD-0009) | No |
| MOD-0044 | Tenant Manager Backend | **MOD-0009-FU02** | Tenant Manager Backend | tenant decomposition | No |
| MOD-0046 | Tenant Core UI | **MOD-0009-FU03** | Tenant Core UI | tenant decomposition | No |
| MOD-0045 | Tenant Mgmt Legacy / Gap Reference | — (retired) | — | retire-legacy (non-executable) | No |
| MOD-0013 | Premium Modal Standard | — (standard) | (≈ Blueprint MOD-0286 UI Design System & Page Pattern Governance) | convert-to-standard; removed from module namespace | No |
| name-drift set (MOD-0018/0021/0023/0026/0027/0028/0032/0033/0034/0035/0038/0039/0041/0042/0263/0265/0287) | various | same IDs | Blueprint canonical names | retain-id, name-align only | No (correct runtime literals unchanged) |
| MOD-0262 | Docs Repository (SharePoint/M365/Google Drive) [External Provider] | **MOD-0262** | Internal Document Repository Service | **reclassify-external-to-internal** (retain-id; historical external row retained as `retired / reclassified`) | No (no runtime literal exists) |

> **MOD-0262 — why this is a separate row and not part of the name-drift set above.** MOD-0262 was included in the 2026-06-03/04 name-drift set, which aligned it to the *then-current* Blueprint name `Docs Repository (SharePoint/M365/Google Drive) [External Provider]`. Blueprint **v6.1 (2026-06-16)** subsequently reclassified the capability from an external provider to an internal platform service (`External Systems Register`: "Internalized / No External Integration … Reclassified as internal document repository service MOD-0262"), which **re-opened** the alignment. That change was never propagated to repo records until **2026-09-07**, when it was re-closed under CT decision D-1. This is a capability conversion (outside → inside), not a rename, so it is tracked distinctly from the pure name-align set. `DCP-002 §5` still lists MOD-0262 among "name-aligned (applied)"; that statement is left unedited because it accurately records what that pack applied on its own date.

## Unresolved EA Reservation Ledger (pending explicit MOD-ID allocation)

> These records are **not** renamed in this pass. No placeholder MOD IDs are assigned. They are **not** converted to deprecated aliases until an exact replacement ID exists. No runtime literal is touched. State = pending explicit Enterprise Architect reservation/decision.

| Current ID | Current Meaning | Requested Canonical Capability | Existing Blueprint Candidate | Exact New ID Assigned? | Runtime Sensitive? | Next Decision Owner | Notes |
|---|---|---|---|---|---|---|---|
| MOD-0047 | Tenant User Foundation (AuthService read-only lookup-validation) | Tenant User / Identity Foundation primitive | none clean (≠ MOD-0288 org dir, ≠ MOD-0261 ext IdP, ≠ MOD-0018 authz) | **No — pending EA** | No (literals already correct) | Enterprise Architect | Squats Blueprint MOD-0047 (Business Continuity). Keep MOD-0047 active until exact new ID allocated; do not rename now. |
| PSS-004 | Tenant Login Security Settings | Tenant login/MFA security settings | none (platform-ops; not in Blueprint) | **No — pending EA** | No | EA | repo-only legacy prefix. |
| PSS-005 | Tenant Module Catalog | Tenant module catalog | partial: Blueprint MOD-0008 "Enterprise Capability / Product Catalog" (different scope) | **No — pending EA** | No | EA | MOD-0008 is a capability label within PSS-005 (see registry/DCP-002). |
| PSS-006 | Tenant Subscription Plan Catalog | Subscription plan catalog | none direct | **No — pending EA** | No | EA | repo-only. |
| PSS-007 | Platform Subscription Feature Management | Feature catalog / plan-feature mapping | none direct | **No — pending EA** | No | EA | repo-only. |
| PSS-008 | Module Details Assignment Inspection | Read-only assignment inspection | none | **No — pending EA** | No | EA | repo-only. |
| PSS-009 | Platform Admin Profile & Settings | Platform admin profile/settings | none | **No — pending EA** | No | EA | repo-only. |
| PSS-010 | Platform Admin Password & MFA Security | Platform admin security | none | **No — pending EA** | No | EA | repo-only. |
| PSS-011 | Lookups / Reference Data | Platform reference data / lookups | none direct | **No — pending EA** | No | EA | repo-only. |
| NEW-002 | Platform Administrators Management | Platform admin lifecycle | none | **No — pending EA** | No | EA | repo-only legacy prefix. |
| NEW-003 | Notification Template Management UI | Notification template CRUD/UI | possibly under MOD-0027 Notification Service | **No — pending EA** | No | EA | EA-003: standalone vs MOD-0027-FU; no active pack. |
| NEW-004 | Tenant Impersonation Tooling | Support/admin impersonation | none | **No — pending EA** | No | EA | EA-004: scope + security model undefined; no active pack. |
| MOD-0266 | Blob / File Storage Provider | Cloud/blob storage abstraction | Blueprint MOD-0266 "Cloud Infrastructure (AWS/Azure/GCP) [External Provider]" (same ID; likely same concept) | **No — pending EA confirm** | No | EA | Likely retain ID + name-align; confirm scope before applying. |
| MOD-0297 | Tenant Subscription Management | SaaS subscription lifecycle | none (not in Blueprint) | **No — pending EA** | **Yes** (Hangfire `TrialExpiryScanJob`, `SubscriptionRenewalJob`) | EA | repo-only; do NOT rename job literals. |
| MOD-0298 | Tenant Module Entitlements | Tenant module entitlements | none (not in Blueprint) | **No — pending EA** | No | EA | repo-only. |
| MOD-0299 ↔ MOD-0169 | SaaS Billing & Invoicing (repo MOD-0299) | SaaS billing capability | Blueprint MOD-0169 "Billing & Invoicing" (ERP / O2C — different domain) | **No — pending EA** | **Yes** (Hangfire `SubscriptionRenewalJob` owner literal `"MOD-0297/MOD-0299"`) | EA | EA-001 / EA-005: disambiguate SaaS vs ERP billing; do NOT rename job owner literal. |
| MOD-0008 | Module Catalog Assignable Expose | Assignable-expose contract within module catalog | Blueprint MOD-0008 "Enterprise Capability / Product Catalog" | **No — pending EA** | No | EA | Recommended: consolidate as capability label within PSS-005; confirm before aliasing. |
| MOD-0169 | Platform Reference (repo; vague) | (clarify) | Blueprint MOD-0169 "Billing & Invoicing" (ERP) | **No — pending EA** | No | EA | EA-005: clarify repo MOD-0169 vs Blueprint ERP billing vs MOD-0299 SaaS billing. |

## Blueprint Internal-Consistency Correction Requests (EA-owned — RAISED, NOT APPLIED)

> **Nothing in this section has been edited in the workbook.** `docs/System Capability & Implementation Blueprint - master 8.1.xlsx` is EA/SoT-owned; this repo raises requests against it and never modifies it. Each row is a measured contradiction **inside** the workbook, i.e. two Blueprint sheets disagreeing with each other — not a repo↔Blueprint drift.
>
> Raised 2026-09-07 during the MOD-0262 canonicalization (CT decision D-1 / open decision D-2).

| Req | Sheet | What it currently says | What `Blueprint_Data` says | Requested correction | Blocks what? |
|---|---|---|---|---|---|
| **EA-BP-001** | `Module Pages` (2 rows for MOD-0262) | Domain `7) External Systems & Providers`, Suite `External Systems Register`, Capability Group `External Provider`; soft pages `External System Profile`, `Integration/Contract Profile` | Domain `1) Platform & Shared Services`, Suite `Documentation & Evidence Services`, Capability Group `Internal Document Repository`; soft pages `Repository Admin; Evidence Package Storage; Document Binary Store; Repository Health` | Re-point the two `Module Pages` rows to Domain-1 and replace the two external-provider pages with the **four** canonical soft pages | Pack authoring — the module's page scope cannot be derived unambiguously from the SoT while two sheets disagree |
| **EA-BP-002** | `Blueprint_Data`, column `Min Integration Contract Codes` (MOD-0262) | `EXT-BASE` | Same row's `Integration Contracts` = `DOCS-BASE + DOC-REPO-BUNDLE`; `SoR Notes` = "native platform ownership" | Replace the residual `EXT-BASE` with `DOC-REPO-BUNDLE` | Contract derivation; `EXT-BASE` is meaningless for an internal Build service |
| **EA-BP-003** ⛔ | `Contract Bundle Dictionary` | **No row for `DOC-REPO-BUNDLE`; no row for `DOCS-BASE`** (only `EXT-BASE` exists) | `Blueprint_Data` mandates `DOCS-BASE + DOC-REPO-BUNDLE` for MOD-0262 | Add dictionary rows defining both codes, sourced from the module's SoR (`document binaries; evidence packages; repository objects`) | ⛔ **HARD BLOCKER for `/prepare-capability-pack`** — a contract cannot be written against an undefined contract code |
| **EA-BP-004** | `Release_Candidates` vs `Blueprint_Data` (MOD-0262) | `Release_Candidates` Implementation Phase = `Platform Foundation` | `Blueprint_Data` Implementation Phase = `TBD` | Reconcile to one value | Low — phase reporting only |
| **EA-BP-005** | `Dependencies` / `Dependencies_Normalized` | MOD-0028 named `Document Management (Templates/Versioning)` | `Blueprint_Data` MOD-0028 = `Documentation & Evidence Management` | Align the dependency sheets to the canonical name | Low — cosmetic, but feeds name-based lookups |

**Root cause (EA-BP-001…005):** Blueprint **v6.1 (2026-06-16)** internalized MOD-0262 in `Blueprint_Data` and `External Systems Register`, but the change was not propagated to the derived sheets. `EA-BP-003` is the only one of those five that blocks delivery.

### EA-BP-006 — MOD-0030 registry identity reservation (raised 2026-09-07)

> Unlike EA-BP-001…005 this is **not** a workbook contradiction. It is a **missing repo registry reservation** for a Blueprint-canonical module, raised as an EA request because MOD-ID reservation is EA-owned (`module-id-registry.md` § Owner / update rule).

| Req | Subject | Measured state | Requested action | Blocks |
|---|---|---|---|---|
| **EA-BP-006** | **MOD-0030 — Records Management (Retention/Legal Hold)** | Blueprint 8.1 `Blueprint_Data`: MOD-0030, Suite "Content, Records & Evidence", W-3, Deployment Unit "Platform Backbone", contract `PLAT-BASE`, SoR = retention policies. **`module-id-registry.md` contains no row for MOD-0030 at all** — while its runtime behaviour partially lives inside `MOD-0029-FU15`. | Reserve the canonical `MOD-0030` registry row (name Blueprint-exact) and rule on the SoR boundary between MOD-0030 and the retention/disposition behaviour already shipped under MOD-0029-FU15. | **MOD-0262-FU05** (physical disposition execution). Per DCP-008 OD-6, FU05 **waits** for this identity; MOD-0262 does **not** take temporary ownership of physical destruction, because destruction is irreversible and `purge.execute` is deliberately segregated from `manage` (DCP-008 AD-7). |

**Raised by:** DCP-008 (Internal Document Repository Service), CT decision **OD-6**, 2026-09-07.

## CAND-CAP Resolution (second pass — applied)

The unresolved items above have been mapped: `PSS-011 → MOD-0048`, `PSS-004 → MOD-0017-FU01`, `NEW-003 → MOD-0027-FU02`, `PSS-XCUT-SV → MOD-0287`; `MOD-0266`/`MOD-0008` name-aligned to Blueprint; `MOD-0169` retired; and the remainder to the temporary candidate namespace — `CAND-CAP-0001` (Tenant User / Identity Foundation ← MOD-0047), `CAND-CAP-0002` (SaaS Subscription, Plan & Entitlement ← MOD-0297, PSS-005/006/007/008) with child `CAND-CAP-0002-FU05` (Tenant Module Entitlements ← MOD-0298), `CAND-CAP-0003` (Platform Administration & Operations ← NEW-002, PSS-009/010), `CAND-CAP-0004` (Tenant Impersonation / Support Tooling ← NEW-004), `CAND-CAP-0005` (SaaS Billing & Invoicing ← MOD-0299). Candidate IDs are temporary governance identities pending Enterprise Architect canonical `MOD-xxxx` allocation; lifecycle: legacy → CAND-CAP alias → EA MOD-xxxx. Runtime literals `MOD-0297` / `MOD-0299` remain unchanged as legacy compatibility literals. `CAND-CAP-0008` (Working Calendar & Public Holidays) is a NEW shared-foundation candidate with **no legacy origin** — the country-based working-day + public-holiday SoR (weekends + official holidays, auto-fetch from an external provider) consumed READ-ONLY by `MOD-0155` (MicroTarget / Field Sales) and future field/project/finance schedulers; it is NOT reference-data and NOT CRM-owned. Numbers `CAND-CAP-0006`/`0007` are reserved-in-intent for CRM per DCP-006 and were skipped. EA reservation authorised by user 2026-08-26.

### CAND-CAP-0006 reservation (Work Aggregation / Task Center — Görev Merkezi)

`CAND-CAP-0006` is a **newly reserved** temporary candidate identity (not a legacy migration like
`CAND-CAP-0001…0005`). It governs the cross-module personal work-aggregation surface (code name
`WorkCenterNext`; user-facing product name "Görev Merkezi / Task Center", SAP Task Center line). The Blueprint
(`Blueprint_Data`) has **no** matching MOD row — verified — so no `MOD-xxxx` is invented. Its governance charter
is [DCP-004](delivery-capability-packs/DCP-004-work-aggregation-task-center.md). Lifecycle: candidate →
pending-EA → future EA `MOD-xxxx`. The future canonical Blueprint `MOD-xxxx` allocation is a **separate
Enterprise Architect decision** (EA follow-up); the candidate ID is never written into runtime literals.
Identity gate: `verify_module_id.py --candidate CAND-CAP-0006 --name "Work Aggregation / Task Center (Görev Merkezi)"` → exit 0 (2026-07-24).

## Minimal SoR Reconciliation Records

| Field | Value |
|---|---|
| Business capability | Legal Entity Management |
| Canonical system-of-record | MDM Legal Entity capability |
| Reserved module ID | `MOD-0220` confirmed |
| Reservation basis | Explicit user decision after authoritative planning Excel mapping confirmation |
| Schema review | MOD-0220 minimal backend schema reconciliation reviewed and approved; Legal Entity Foundation is ready-for-dev for the narrow backend-only slice |
| Remaining reconciliation | Authoritative Enterprise Blueprint repository migration pending as non-blocking follow-up |
| Owner domain | MDM |
| Consumer module | `MOD-0288 Organization, Person & Position Directory` (canonicalized from MOD-0040 per DCP-002) |
| Consumer relationship | Read-only `LegalEntityId` reference / lookup validation dependency; validation contract locked and unchanged |
| Forbidden duplication | No Legal Entity aggregate, persistence, lifecycle, API or UI under MOD-0288 |
| Decision gate | `OD-MOD-le-contract` resolved; MOD-0288 pack ready-for-dev for the minimal backend-only v1 slice |
| Related follow-up | Complete authoritative Enterprise Blueprint repository migration; define MDM business-country reference source distinct from PSS-011 |

## Boundary Notes

- PSS-011 countries lookup is Platform provisioning/support only.
- It is not the MDM business-country system of record.
- MDM business-country reference ownership remains a separate follow-up.

## Source / migration note
New target file designed for governance alignment between the business capability matrix (Excel blueprint) and technical implementation modules.

**Workbook supersession (user decision).** The prior planning workbook `execution/modules_pages_planning_v3.xlsx` is intentionally retired and removed from the repository by explicit user decision; `docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx` is the authoritative canonical enterprise module-ID source going forward. No claim is made that every historical sheet was migrated one-to-one.

## Owner / update rule
- Owner: Enterprise Architect / PMO
- Update Rule: Updated during wave planning alignment sessions and before release gate reviews. New canonical MOD-ID allocations for unresolved items above require explicit Enterprise Architect reservation recorded in [module-id-registry.md](../registries/module-id-registry.md) before any rename.
