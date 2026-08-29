---
id: DCP-004
slug: corporate-collection-controlled-document-registration-scope
name: Corporate Collection Instance & Controlled Document Registration Scope Enablement
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: platform-shared-services
owner: platform-shared-services / enterprise-architect
branch: feature/pss/dcp-004-corporate-document-scope
created: 2026-07-25
approved: 2026-07-25
members:
  - MOD-0028-FU06
  - MOD-0029-FU37
---

# DCP-004 — Corporate Collection Instance & Controlled Document Registration Scope Enablement

> **Approved governance contract.** DCP-004 approves the cross-module architecture and delivery sequence recorded
> below; it does not itself authorize or perform runtime changes. MOD-0028-FU06 is
> `implemented-runtime-green-with-nonblocking-gaps`; MOD-0029-FU37 is `ready-for-dev / not-started`.

## 1. Identity and status / Capability Summary

This cross-cutting Delivery Capability Pack coordinates a real tenant-owned corporate collection target in
MOD-0028 with a scope-aware controlled-document registration contract in MOD-0029. It does not replace either
member module pack.

## 2. Business outcome

Users can eventually register either Company or Corporate controlled documents without a dummy company, without
weakening tenant isolation, and without allowing folder, storage, or retry state to cross the selected scope.

## 3. Problem statement

The current runtime contracts require `CompanyId` on collection instances and controlled documents, partition
content by company, and authorize folders through company membership. `CollectionScopeType` has no Corporate
member and no provisioning operation turns a corporate baseline into a real corporate collection instance.
Making `CompanyId` nullable in isolation or sending a synthetic company would make identity, authorization,
partitioning, and audit semantics disagree.

## 4. Capability boundary / Current Contract Constraints

In scope is the governance design for:

- a tenant-owned, company-independent Corporate Collection Instance;
- a scope-aware owner and storage partition contract;
- corporate folder authorization and provisioning;
- `DocumentScope = Company | Corporate`;
- scope-aware registration validation, operation snapshots, retries, and conditional UI;
- compatibility with existing Company registrations.

No entity, API, storage provider, access evaluator, frontend, AuthService, Gateway, or Ocelot code changes are
authorized by this draft.

## 5. Member modules and follow-ups / Affected Modules

| Member | Responsibility | Status |
|---|---|---|
| `MOD-0028-FU06` | Corporate Collection Instance Foundation | `implemented-runtime-green-with-nonblocking-gaps` |
| `MOD-0029-FU37` | Corporate/Company Registration Amendment to FU36 | `ready-for-dev / not-started` |
| `MOD-0029-FU36C` | Reverse navigation and legacy-bypass hardening | paused |
| `MOD-0029-FU36D` | Runtime smoke and commit-separation audit | paused |

FU37 amends FU36 without changing FU36's existing `ready-for-dev` registry status.

## 6. Ownership map / Target Scope Model

| Concern | System of record |
|---|---|
| Baseline, definition tree, instance, folder scope | MOD-0028 |
| Registration operation and controlled-document birth | MOD-0029 |
| Tenant context | server-authenticated context; never request payload authority |
| Permission definitions | MOD-0018/Auth governance, only in a later explicitly approved task |
| Content partition implementation | MOD-0028 storage boundary |

Target invariant: every instance, folder, controlled document, version, and registration operation has one
effective scope: `Company(companyId)` or `Corporate(tenant corporate owner)`.

## 7. Dependency graph / MOD-0028 Pack: Corporate Collection Instance Foundation

```text
MOD-0028-FU06 approved + implemented
  └─ corporate instance + folder + partition + access contract
       └─ MOD-0029-FU37 approved + implemented
            └─ FU36C
                 └─ FU36D
```

MOD-0028-FU06 owns the corporate target. MOD-0029 must consume that target and must not reproduce its
provisioning or folder-policy logic.

## 8. Ordered delivery sequence / MOD-0029-FU36 Pack: Corporate/Company Registration Amendment

1. MOD-0028-FU06 Approval / Ready-for-dev — completed by this governance approval.
2. MOD-0028-FU06 Backend Implementation.
3. MOD-0029-FU37 Approval / Ready-for-dev.
4. MOD-0029-FU37 Backend + Frontend Amendment.
5. MOD-0029-FU36C Reverse Navigation & Legacy Bypass Hardening.
6. MOD-0029-FU36D Runtime Smoke & Commit Separation Audit.

## 9. Prerequisites / Storage Partition Decision Options

The approved storage-safe model is a typed scope owner. Immutable `(TenantId, ScopeType, ScopeOwnerId)` produces
  `tenant/{tenantId}/company/{companyId}/folder/{folderId}` or
  `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`.

Partition generation has one canonical contract. Nullable-only and synthetic/dummy `CompanyId` solutions are
prohibited.

## 10. Architecture decisions / Authorization and Folder Access Policy Options

Approved decisions:

1. Corporate Collection Instance is tenant-owned and company-independent. Dummy CompanyId is prohibited.
2. Typed scope ownership is required. Company uses `ScopeType=Company`, `ScopeOwnerId=CompanyId`; Corporate uses
   `ScopeType=Corporate`, `ScopeOwnerId=CorporateOwnerId`. Existing Company behavior remains backward-compatible.
3. Company partition remains `tenant/{tenantId}/company/{companyId}/folder/{folderId}`. Corporate partition is
   `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}` and never uses a company fallback.
4. Corporate folder access is deny-by-default and requires explicit governed role/group/policy grants. Company
   membership alone grants neither Corporate read nor write access. The evaluator must be scope-aware.
5. Corporate baseline provisioning creates a real Corporate Collection Instance through an idempotent,
   tenant-scoped, retry-safe, audit-visible process.
6. One active Corporate Collection Instance is allowed per tenant, CorporateOwnerId, and baseline. Multiple active
   instances require a separate lifecycle/versioning approval.
7. MOD-0028-FU06 performs no bulk migration. Existing Company instances remain unchanged.
8. `DocumentScope` registration behavior belongs to MOD-0029-FU37. FU06 provides the instance, folder, partition,
   access, and provisioning foundation consumed by FU37.
9. Cross-tenant or cross-scope misses return non-leaking `404`.
10. Registration retry is pinned to its original scope snapshot and cannot switch scope.

## 11. Scope / Data Model Impact

The future implementation is expected to introduce or amend:

- `CollectionScopeType.Corporate`;
- a required typed scope owner on Collection Instance and derived folder nodes;
- a corporate provisioning identity and immutable baseline-release provenance;
- `DocumentScope` on registration requests, operations, documents, and relevant audit projections;
- Company fields that are conditionally required for Company, absent for Corporate;
- a corporate governance owner model for `OwnerCompanyId` replacement rather than a fake legal entity.

The exact implementation shape may follow existing MOD-0028 conventions, but it must preserve the approved typed
scope invariants. No bulk migration is part of FU06.

## 12. Explicit exclusions / Request Validation Impact

Out of scope:

- runtime implementation in either module;
- direct storage-provider implementation;
- unrelated MOD-0028 baseline lifecycle redesign;
- external DMS/QMS integration;
- UID/code-allocation changes;
- automatic approval, effective-date, signature, training, retention, or release completion;
- hard delete or cross-tenant access.

Future validation must require:

- Company: Company, company instance, and a folder belonging to both;
- Corporate: corporate instance and folder belonging to it; no CompanyId;
- all identifiers resolved under server TenantId;
- mismatched scope/instance/folder/partition snapshots rejected without partial writes.

## 13. Governance drift risks / Retry and Idempotency Impact

- Treating a baseline definition as an upload target instead of provisioning an instance.
- Allowing nullable CompanyId to silently select a global partition.
- Letting folder authorization remain company-only while documents claim Corporate scope.
- Retrying an operation against a newly selected scope or folder.
- Duplicating `DocumentScope` enums independently across modules with different semantics.
- Updating FU36 UI before the backend contract and target instance exist.

The durable operation snapshot must include `DocumentScope`, typed scope owner/partition identity,
`CollectionInstanceId`, `FolderId`, and `CompanyId` only for Company. Same idempotency key plus a different scope
fingerprint must fail deterministically.

## 14. Review questions / UI Impact

The unified form will begin with one governed selector: `Corporate` or `Company`.

- Corporate: QMS baseline/documentation structure → resolved/selected Corporate Collection Instance → folder.
- Company: legal entity/company → Collection Instance → folder.
- Changing an upstream selector clears every dependent value.
- Governing Language and Retention Class are governed single-select lookups, not free text.
- The UI must not offer Corporate until the backend advertises a usable corporate target contract.

Only one active instance per tenant, CorporateOwnerId, and baseline is allowed in this delivery. The FU37 UI may
resolve that instance deterministically; multiple-active-instance selection is out of scope.

## 15. Gate criteria / Migration and Compatibility

- Existing Company registrations retain their current effective behavior and storage paths.
- No existing record is reclassified automatically.
- Migration analysis inventories records whose company semantics are absent or inconsistent; migration execution
  is a separately approved task.
- Corporate support is feature-gated until provisioning, partitioning, and access evaluation are all deployed.
- Company path regression tests pass before Corporate is enabled.

## 16. Acceptance criteria / Risks

1. Both member packs have independently approved contracts before implementation.
2. No dummy CompanyId is generated or accepted.
3. Tenant, scope, instance, folder, partition, document, and retry invariants are specified end to end.
4. Corporate access is deny-by-default and testable.
5. Company behavior remains backward compatible.
6. FU36C/FU36D remain paused until their predecessor gates close.
7. Audit evidence proves no runtime change was made during this authoring task.

Primary risks are partition collision, authorization leakage, audit misattribution, non-idempotent provisioning,
and partial registration across inconsistent scopes.

## 17. Downstream business-module impacts / Approval Gates

The scope-owner, partition, deny-by-default access, provisioning uniqueness, no-bulk-migration, and member
ownership decisions are approved. Exact Corporate document owner semantics, FU37 UI field matrix, canonical
`DocumentScope` location, and downstream lifecycle impacts remain FU37 approval gates, not FU06 blockers.

Training, retention, signature, release-gate, and manual-link consumers must later be audited for scope awareness;
they are not implemented by this pack.

## 18. Open decisions / Recommended Delivery Sequence

FU06 uses a tenant-owned governance `CorporateOwnerId`; its directory/source representation is an implementation
detail constrained by tenant isolation. Exact grant catalog entries, Corporate document owner semantics,
canonical `DocumentScope` location, and manual-link behavior remain MOD-0029-FU37 approval questions.

The six-phase sequence in §8 is approved; no phase may skip its predecessor gate.

## 19. Future follow-ups / Out of Scope

Potential later packs cover migration execution, corporate access administration UI, governed owner directories,
scope-aware downstream lifecycle/training/signature behavior, and reporting. They are not implied approvals.

## 20. Audit and reconciliation notes / Ready-for-dev Checklist

Initial blocker evidence is recorded in
`docs/audits/mod-0029-fu36-corporate-scope-governance-blocker-2026-07-25.md`.

- [x] DCP-004 approved.
- [x] MOD-0028-FU06 decisions approved and pack promoted to `ready-for-dev`.
- [x] MOD-0028-FU06 implementation verified and reconciled.
- [x] MOD-0029-FU37 decisions and pack approved independently.
- [x] Storage partition model approved; implementation threat tests remain FU06 acceptance evidence.
- [x] Corporate folder policy approved as deny-by-default with explicit governed grants.
- [x] No-bulk-migration and Company compatibility decision recorded.
- [ ] FU36C/FU36D pause released explicitly.
- [x] Nullable-only and dummy CompanyId semantics prohibited.

Approval evidence:
`docs/audits/dcp-004-mod-0028-fu06-approval-ready-for-dev-2026-07-25.md`.

Historical post-implementation reconciliation (2026-07-25): DCP-004 remained `approved`, but Phase 2 was runtime-blocked.
Real Mongo rejected the FU06 Corporate partial unique index (`InstanceStatus != Archived` is unsupported in a
partial filter), preventing Platform startup and authenticated smoke. Phase 3/FU37 remains blocked until a
separately authorized runtime fix and successful smoke close this gap. Evidence:
`docs/audits/mod-0028-fu06-runtime-smoke-reconciliation-2026-07-25.md`.

Mongo compatibility reconciliation (2026-07-25): Phase 2 startup/index blocker is resolved. The positive Active
partial filter was created successfully after development-only exact-index reconciliation, and Platform health
returned 200 with Mongo healthy. Authenticated provisioning/access/cross-tenant cases remain non-blocking smoke
gaps. Phase 3 is eligible for FU37 approval review; FU37 remains `draft`, and FU36C/FU36D remain paused. Evidence:
`docs/audits/mod-0028-fu06-mongo-index-compatibility-fix-2026-07-25.md`.

Phase 3 approval reconciliation (2026-07-25): MOD-0029-FU37 is `ready-for-dev` with runtime implementation
`not-started`; implementation may begin under its approved pack. FU36C/FU36D remain paused until FU37
implementation completes and is reconciled. FU06 authenticated full-fleet smoke remains a tracked non-blocking
risk.

Phase 3 FU37A backend reconciliation (2026-07-25): the scope-aware backend foundation is implemented and verified.
Typed Company/Corporate ownership, immutable retry scope, existing-instance/folder alignment, explicit Corporate
folder authorization and exact scope partitions are present. FU37B frontend, FU37C manual-link guardrails and
FU36C reverse-navigation hardening are implemented; authenticated runtime smoke remains open under FU36D.

Phase 3 FU37B frontend reconciliation (2026-07-25): the Company-default scope selector, mutually exclusive
Company/Corporate payloads, same-origin CollectionInstance/folder cascades, governed Language/Retention Select2
controls and Completed-only response handling are implemented and verified. Governed language lookup availability
for the tenant actor still requires authenticated runtime proof. Manual-link and reverse-navigation hardening are
implemented; runtime smoke remains open under FU36D.

Phase 3 FU36C reconciliation (2026-07-25): Controlled Document Details now consumes the permission-protected
reverse lookup through a same-origin MVC proxy, displays compatible, legacy/unlinked and unverified/incompatible
states without offering a link mutation, and routes normal document creation exclusively to the unified Master
Register flow while preserving template and version operations. FU36D authenticated runtime smoke remains paused.

Phase 6 FU36D/FU37D runtime reconciliation (2026-07-25): **BLOCKED/PARTIAL**. Fleet and tenant authentication are
healthy; normal-create redirect, template preservation and the 409 legacy-create bypass are runtime-green.
Company/Corporate submission is fail-closed because governed Language returns 403 and Retention Class returns 500;
the Corporate instance list is empty and Controlled Documents list returns 500. FU36/FU37 remain
`implemented-with-runtime-gaps`; final completion and commit preparation are not approved.
