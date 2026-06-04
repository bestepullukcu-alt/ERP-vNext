---
id: DCP-002
slug: module-identity-canonicalization
name: Module Identity Canonicalization
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: platform-shared-services
owner: enterprise-architect / platform-team
branch: feature/governance/blueprint-module-id-reconciliation
created: 2026-06-04
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data"
inputs:
  - "docs/audits/blueprint-module-id-reconciliation-2026-06-03.md"
---

# DCP-002 — Module Identity Canonicalization (Delivery Capability Pack)

> **Artifact type:** This is a **Delivery Capability Pack** (CAP-001 governance / orchestration contract). It is **NOT** a runtime entity, **NOT** a module pack, **NOT** a MOD-0014 runtime Capability Group, and **NOT** a business-capability-matrix row.
>
> **Status guard:** `draft`. This pack authorizes the documentation-only canonicalization recorded below (already applied to the working tree on this branch). It does **NOT** approve any new numeric MOD-ID reservation; all unresolved items remain pending explicit Enterprise Architect allocation.

## 1. Identity and status

| Field | Value |
|-------|-------|
| ID | DCP-002 |
| Slug | module-identity-canonicalization |
| Type | Delivery Capability Pack (CAP-001) |
| Status | `draft` |
| Owner domain | platform-shared-services |
| Authoring branch | `feature/governance/blueprint-module-id-reconciliation` |
| Canonical source | Blueprint `Blueprint_Data` (296 MOD rows) |
| Authority note | Does not alter AGENTS.md §1 authority hierarchy. The Blueprint is the canonical enterprise authority for MOD-ID and canonical capability-name validation. |

## 2. Business outcome
A single canonical `MOD-xxxx` namespace aligned to the enterprise Blueprint, eliminating ID drift and collisions, with full historical traceability via deprecated aliases, and a durable preflight gate that prevents future drift.

## 3. Problem statement
Repository module IDs had drifted from the Blueprint: some repo IDs occupied Blueprint numbers reserved for different (SRE/resilience/correlation) modules, several names diverged from Blueprint canonical names, and a parallel legacy namespace (`PSS-*`, `NEW-*`) and repo-only IDs existed without explicit Blueprint reservation. See audit `docs/audits/blueprint-module-id-reconciliation-2026-06-03.md`.

## 4. Capability boundary
In scope: documentation-only canonical alignment of module identities and names; deprecated-alias governance; the unresolved EA reservation ledger; the prevention gate. **Out of scope:** runtime code, Hangfire recurring job IDs, `AuditEvent.SourceModule`, appsettings keys, test assertions, API routes, permission codes — none are modified by this pack.

## 5. Member modules and follow-ups
Referenced **by ID only** (this pack never replaces a module pack):
- Canonicalized (applied): MOD-0288 (+FU01), MOD-0009-FU01/FU02/FU03; retired MOD-0045; reclassified MOD-0013.
- Name-aligned (applied): MOD-0018, 0021, 0023, 0026, 0027, 0028, 0032, 0033, 0034, 0035, 0038, 0039, 0041, 0042, 0262, 0263, 0265, 0287.
- Unresolved (pending EA): MOD-0047, PSS-004…011, NEW-002/003/004, MOD-0266, MOD-0297, MOD-0298, MOD-0299↔MOD-0169, MOD-0008, MOD-0169.

## 6. Ownership map
- Enterprise Architect: canonical MOD-ID reservation decisions (registry owner).
- platform-team: applies documentation-only canonicalization on the governed branch.
- Registry (`module-id-registry.md`): canonical ID + alias system of record.
- Reconciliation ledger (`blueprint-master-plan-reconciliation.md`): Blueprint↔repo mapping + unresolved reservation ledger.

## 7. Dependency graph
Audit (read-only) → DCP-002 canonicalization (docs-only) → [blocked] EA reservations → [conditional] runtime-sensitive migration. The runtime migration is a **separate** PR and depends on explicit EA allocation.

## 8. Ordered delivery sequence
1. Apply documentation-only canonicalization (this pass; PR-1).
2. Add prevention gate (PR-1).
3. EA resolves reservation ledger (separate decisions).
4. Conditional runtime-sensitive migration (PR-2) only if an EA decision renumbers a runtime-bearing ID (MOD-0033/0297/0299) — never bundled with PR-1.

## 9. Prerequisites
- Blueprint present at the canonical path; registry readable; audit complete.
- Branch `feature/governance/blueprint-module-id-reconciliation` (not `main`).

## 10. Architecture decisions
- **Blueprint is canonical** for MOD-ID and canonical name.
- **Single active namespace**: no permanent second repository-local `MOD-xxxx` namespace; legacy/`PSS-`/`NEW-` and repo-only IDs converge to canonical MOD IDs via explicit EA reservation.
- **Deprecated-alias policy**: a superseded ID keeps a registry row with `status: deprecated`, `Deprecated Alias = self`, `Replacement ID = canonical`; it is never deleted (traceability).
- **Targeted rename policy**: active module-pack filenames must equal their canonical frontmatter `id`; renames are targeted and evidence-based (AGENTS §12 forbids only uncontrolled mass rename). Historical audit files, PR references, and branch names remain unchanged.
- **Runtime-sensitive separation**: runtime literals already on a correct canonical ID are never modified; ID renumbering that touches runtime literals is a separate migration.
- **No-guess reservation**: new canonical IDs for repo-only/legacy capabilities require an explicit exact EA allocation; no placeholder, no next-free scan.

## 11. Scope (applied this pass — documentation-only)
The Blueprint↔Repo canonicalization ledger and the rename/alias entries are authoritatively recorded in [blueprint-master-plan-reconciliation.md](../blueprint-master-plan-reconciliation.md#blueprint--repo-canonicalization-ledger-dcp-002-applied) and [module-id-registry.md](../../registries/module-id-registry.md). Active packs renamed: `MOD-0288`, `MOD-0288-FU01`, `MOD-0009-FU01/FU02/FU03`.

## 12. Explicit exclusions
No runtime code / job IDs / audit keys / config keys / test assertions / routes / permission codes changed. `MOD-0047`, `PSS-*`, `NEW-*`, `MOD-0266`, `MOD-0297/0298/0299`, `MOD-0169` are **not** renamed in this pass and receive **no** placeholder IDs.

## 13. Governance drift risks
- Re-use of a Blueprint ID for a different repo concept (mitigated by the prevention gate).
- Aliasing an ID before its exact replacement exists (forbidden; see §10).
- Touching a correct runtime literal during a docs rename (forbidden; see §10).

## 14. Review questions
- Are the EA reservation items (§16) allocated exact canonical IDs?
- Is MOD-0299↔MOD-0169 (SaaS vs ERP billing) resolved before any billing-job rename?
- Is MOD-0047’s canonical home decided (new reservation vs MOD-0288/MOD-0018)?

## 15. Gate criteria
- PR-1 contains zero runtime/test diff (verified).
- Every changed canonical ID passes `verify_module_id.py --check-id`.
- `verify_module_id.py --check-all` lists the unresolved backlog without blocking the applied safe migration.

## 16. Unresolved EA reservation ledger (pending explicit allocation)

> Mirror of the authoritative table in the reconciliation ledger. **Not approved.** No rename, no placeholder ID, no alias-before-replacement, no runtime-literal change. State = pending explicit EA reservation/decision.

| Current ID | Requested canonical capability | Existing Blueprint candidate | Exact new ID? | Runtime-sensitive? | Owner |
|---|---|---|---|---|---|
| MOD-0047 | Tenant User / Identity Foundation primitive | none clean | No — pending | No | EA |
| PSS-004…011 | tenant login security / module catalog / subscription plan / feature mgmt / inspection / admin profile / admin security / lookups | mostly none (PSS-005↔MOD-0008 partial) | No — pending | No | EA |
| NEW-002 | Platform Administrators Management | none | No — pending | No | EA |
| NEW-003 | Notification Template Management UI | possibly under MOD-0027 | No — pending | No | EA |
| NEW-004 | Tenant Impersonation Tooling | none | No — pending | No | EA |
| MOD-0266 | Blob / File Storage Provider | Blueprint MOD-0266 (same ID; confirm) | No — pending confirm | No | EA |
| MOD-0297 | Tenant Subscription Management | none (repo-only) | No — pending | Yes (Hangfire) | EA |
| MOD-0298 | Tenant Module Entitlements | none (repo-only) | No — pending | No | EA |
| MOD-0299 ↔ MOD-0169 | SaaS Billing & Invoicing | Blueprint MOD-0169 (ERP, different domain) | No — pending | Yes (Hangfire owner literal) | EA |
| MOD-0008 | Module Catalog Assignable Expose | Blueprint MOD-0008 Product Catalog | No — pending | No | EA |

See the synchronized authoritative copy and full notes in [blueprint-master-plan-reconciliation.md](../blueprint-master-plan-reconciliation.md#unresolved-ea-reservation-ledger-pending-explicit-mod-id-allocation).

## 16a. Second-pass CAND-CAP resolution (applied)

The §16 items have been resolved in a second governance pass and are no longer "pending unresolved": each was mapped to an existing Blueprint MOD (`PSS-011 → MOD-0048`, `MOD-0266`/`MOD-0008` name-aligned, `PSS-XCUT-SV → MOD-0287`), an FU under a Blueprint parent (`PSS-004 → MOD-0017-FU01`, `NEW-003 → MOD-0027-FU02`), retired (`MOD-0169`), or a temporary `CAND-CAP-####` candidate (governance-only): `CAND-CAP-0001` (Tenant User / Identity Foundation), `CAND-CAP-0002` (SaaS Subscription, Plan & Entitlement Management; +FU01–FU05), `CAND-CAP-0003` (Platform Administration & Operations; +FU01/FU02), `CAND-CAP-0004` (Tenant Impersonation / Support Tooling), `CAND-CAP-0005` (SaaS Billing & Invoicing). The remaining truly-pending decision is the future Enterprise Architect allocation of canonical `MOD-xxxx` for `CAND-CAP-0001…0005`. Runtime literals `MOD-0297` / `MOD-0299` are retained as legacy runtime compatibility literals; candidate IDs are never written into runtime code.

## 17. Acceptance criteria
1. Documentation-only canonicalization applied with zero runtime/test diff.
2. Deprecated aliases recorded for every renamed ID; chains resolve.
3. Unresolved items carry no placeholder IDs and are not aliased.
4. Prevention gate present and documented (fail-closed; does not block the known pre-existing backlog).

## 18. Downstream business-module impacts
None at runtime. Documentation consumers (master plan, DCP-001, domain configs) updated or annotated; old IDs resolve via aliases.

## 19. Open decisions
The §16 ledger items, owned by the Enterprise Architect. PR-2 (runtime migration) stays blocked until any runtime-bearing ID receives an exact new allocation.

## 20. Audit and reconciliation notes
Grounded in `docs/audits/blueprint-module-id-reconciliation-2026-06-03.md` and a read-only backend-architect architecture analysis. This pass is documentation-only; the No-Change proof for runtime paths is recorded in the executing handoff report.

**Workbook supersession (user decision).** The prior planning workbook `execution/modules_pages_planning_v3.xlsx` is **intentionally retired** by explicit user decision and removed from the repository. `docs/System Capability & Implementation Blueprint - master 5.xlsx` (`Blueprint_Data`) is the authoritative canonical enterprise module-ID source going forward. This is a deliberate supersession of authority; it does **not** assert a one-to-one migration of every legacy sheet from the old workbook.

## Prevention gate design (transition rules)

Validation script (added in this pass): `.antigravity/scripts/verify_module_id.py`.

- `--check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]` → **fail-closed** (exit 2) when the Blueprint cannot prove the ID/name, on registry collision, duplicate ID, invalid parent/FU relationship, or a repo-only capability lacking explicit EA reservation evidence. Exit 0 = proven.
- `--check-all` → audit mode; reports unresolved legacy/reservation backlog. Exit 0 (advisory) so the known pre-existing backlog does not block the applied safe migration; exit 1 only on a NEW hard violation (duplicate canonical ID / filename≠id for an active pack).
- Transition rule: pre-existing unresolved backlog (the §16 ledger) is listed, not failed, until reconciled. New module-pack creation must pass `--check-id`.
