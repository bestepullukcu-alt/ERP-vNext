---
id: MOD-0028-FU06
name: Corporate Collection Instance Foundation
parent: MOD-0028
previous: MOD-0028-FU05
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: review
owner: platform-shared-services
branch: feature/pss/mod-0028-fu06-corporate-collection-instance-foundation
started: 2026-07-25
target: 2026-08-14
form_field_count: 0
delivery_capability_pack: DCP-004
runtime_implementation: implemented-runtime-green-with-nonblocking-gaps
approved: 2026-07-25
---

# MOD-0028-FU06 — Corporate Collection Instance Foundation

> **Implementation review — runtime green with non-blocking smoke gaps.** The Corporate partial index now uses
> positive `CollectionInstanceStatus.Active` equality. A development-only, exact-name reconciliation replaced the
> obsolete local `$lt` definition without changing documents; Platform health returned 200 with Mongo healthy.
> Authenticated provisioning/access/cross-tenant smoke remains deferred because the Gateway/Auth fleet was not
> running. FU37 remains draft and FU36C/FU36D remain paused.

## 1. Module Summary

Define the MOD-0028 foundation for a real tenant-owned, company-independent Corporate Collection Instance created
from a published baseline. The foundation must preserve existing Company instance behavior while making scope,
storage partition, folder authorization, and provisioning explicit and consistent.

## 2. Ownership and Boundaries

In scope: corporate instance identity, scope-aware folder tree, provisioning contract, partition contract,
authorization seam, tenant isolation, compatibility and migration design.

Out of scope: MOD-0029 registration UI/backend amendment, FU36 registration rewrite, ControlledDocument entity
rewrite, dummy CompanyId, a CompanyId-nullable quick fix, bulk migration, unrelated baseline lifecycle redesign,
hard delete, external DMS/QMS integration, cross-tenant access, and FU36C/FU36D implementation.

## 3. Owned Objects

Approved implementation ownership:

- `Corporate` member of the canonical collection-scope model;
- typed scope-owner value/contract for Collection Instances and folders;
- idempotent Corporate Collection Instance provisioning operation;
- corporate folder access policy contract;
- scope-aware storage partition descriptor;
- dry-run/execute/read APIs and DTOs only if approved in the implementation phase.

Runtime objects are not created by this approval task.

## 4. Entity Fields

| Conceptual field | Required | Rule |
|---|---|---|
| TenantId | Yes | Server context; indexed; never trusted from payload |
| ScopeType | Yes | `Company` or `Corporate` |
| ScopeOwnerId | Yes | Typed owner; cannot represent another tenant |
| CompanyId | Conditional | Required for Company; absent for Corporate |
| BaselineReleaseId | Yes | Published release in same tenant |
| ProvisioningFingerprint | Yes | Stable idempotency input |
| Root/Folder identity | Yes | Bound to the same instance and scope |
| PartitionDescriptor | Yes | Derived, immutable, collision-resistant |

Implementation may use embedded or first-class persisted values according to existing MOD-0028 conventions, but
indexes must enforce one active instance per tenant, CorporateOwnerId, and baseline.

## 5. Repo Scope

Future implementation may touch only approved MOD-0028 paths under:

- `services/Diten.Platform/**/DocumentManagement/**`;
- MOD-0028-specific tests;
- explicitly approved tenant-shell surfaces if a later UI slice is added;
- this pack, DCP-004, registry, and audit evidence.

This authoring task changes governance files only.

## 6. Protected Paths

- `.antigravity/**`;
- `services/Diten.AuthService/**`;
- `gateway/Diten.ApiGateway/**` and Ocelot configuration;
- all MOD-0029 registration, ControlledDocument, FU36/FU37, FU36C, and FU36D runtime paths;
- direct content-storage provider rewrites outside the approved scope-partition contract;
- CompanyId nullable quick fixes and dummy/synthetic company records;
- bulk migration scripts/jobs;
- other domain services;
- archive controllers/views and frozen shared layout.

## 7. Dependencies

- MOD-0028-FU02/FU04 published baseline and definition contracts;
- MOD-0028-FU05 existing Company provisioning behavior;
- authenticated tenant context and non-leaking tenant isolation;
- DCP-004 architecture decisions.

MOD-0029-FU37 depends on this pack's implemented and reconciled contract.

## 8. Runtime Constraints

- MongoDB tenant isolation and soft delete remain mandatory.
- Company partition behavior remains backward compatible.
- Corporate partition must include tenant and an explicit corporate scope owner.
- Company partition remains `tenant/{tenantId}/company/{companyId}/folder/{folderId}`.
- Corporate partition is `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`.
- Both partitions are produced through one scope-aware contract; no default/fallback company is permitted.
- Cross-tenant and scope-mismatched reads do not disclose target existence.
- Provisioning must be idempotent and safe under retry/concurrency.
- One active instance is allowed per tenant, CorporateOwnerId, and baseline.
- No baseline definition is itself an upload folder.

## 9. Layout & Shell Contract

`shell: none` for this backend foundation. No frontend page is authorized. If a management UI is later required,
it needs a separately approved pack/amendment and `_LayoutTenantShell`.

## 10. Backend File Convention

Any later implementation follows the service's existing MOD-0028 CQRS/repository conventions and the approved
five-layer architecture. Exact feature folders and public types must be enumerated during ready-for-dev review;
this draft does not mint runtime names prematurely.

## 11. Frontend File Contract

None. Corporate/Company registration UI belongs to MOD-0029-FU37. A MOD-0028 corporate-instance administration UI
is a future follow-up.

## 12. Validation Rules

| Field/relationship | Required | Rule |
|---|---|---|
| ScopeType | Yes | Corporate operation accepts only Corporate |
| CompanyId | No for Corporate | Presence is a validation error |
| BaselineReleaseId | Yes | Same tenant and Published |
| ScopeOwnerId | Yes | Same tenant, correct owner type |
| Selected definitions | Conditional | Same release; valid tree selection |
| Idempotency key/fingerprint | Yes | Same key with changed inputs is conflict |
| Folder access | Yes | Scope-aware policy; deny by default |

## 13. Failure Path to Verify

- foreign-tenant baseline/instance/folder → non-leaking `404`;
- CompanyId supplied for Corporate → validation failure;
- missing or wrong typed owner → validation failure;
- duplicate concurrent provisioning → one effective instance/result;
- same idempotency key with changed release/selection → conflict;
- unauthorized corporate access → no metadata leak;
- storage descriptor collision between Company and Corporate → impossible by contract/test.

## 14. Authorization Convention

Corporate read, write/provision, and administer access is deny-by-default and requires explicit governed
role/group/policy grants. Company membership alone grants no Corporate read or write access. Exact permission
literals/Auth seed changes are a separate implementation/integration authorization and are not part of this
approval task.

## 15. Gateway / API Routing Decision

No Gateway/Ocelot change is authorized. Later API routes should reuse the existing Document Management gateway
surface where possible; any route addition is an integration-agent task with separate approval.

## 16. Acceptance Criteria

1. Approved scope-owner model distinguishes Company and Corporate without dummy identifiers.
2. Corporate provisioning creates a real instance/folder tree from a Published release.
3. Provisioning is idempotent and concurrency-safe.
4. Partition descriptors cannot collide across tenants or scope types.
5. Corporate folder access is deny-by-default and scope-aware.
6. Existing Company provisioning and paths pass regression tests.
7. Cross-tenant and cross-scope requests do not leak data.
8. Migration/rollout behavior is explicitly approved and tested.

## 17. Test Expectations

Future implementation requires domain/unit tests for scope invariants and partition generation; integration tests
for provisioning, idempotency, authorization, tenant isolation, and Company regression; persistence index tests;
and a live smoke only after runtime implementation. No build/test is required for this governance-only draft.

## 18. Ready-for-dev Checklist

- [x] DCP-004 approved.
- [x] Corporate instance is tenant-owned and company-independent.
- [x] Typed `ScopeType + ScopeOwnerId` representation approved.
- [x] Company and Corporate partition formats approved.
- [x] Corporate access is deny-by-default with explicit governed grants.
- [x] Provisioning uniqueness/idempotency approved.
- [x] Existing Company baseline/import behavior remains unchanged.
- [x] No bulk migration; Corporate instances use new provisioning.
- [x] Repo/API/permission boundaries and protected paths enumerated.
- [x] Pack promoted by explicit user approval.

## 19. Implementation Notes

Approved partition contract:

- Company: `tenant/{tenantId}/company/{companyId}/folder/{folderId}`;
- Corporate: `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`.

`ScopeType + ScopeOwnerId` is required. Corporate has no CompanyId and no company fallback. MOD-0029-FU37 remains
draft and must consume the verified FU06 contract before it can be approved. FU36C/FU36D remain paused.

### Approval Decision Log — 2026-07-25

- Approved decision: Corporate Collection Instance is tenant-owned and company-independent. Dummy CompanyId is
  prohibited.
- Approved decision: Typed scope ownership is required. Company uses CompanyId as scope owner; Corporate uses a
  tenant-owned CorporateOwnerId. Existing Company behavior remains backward-compatible.
- Approved decision: Corporate storage partition uses tenant/corporate/scope-owner/folder semantics and never
  CompanyId or dummy company records.
- Approved decision: Corporate folder access is deny-by-default and requires explicit governed grants.
- Approved decision: Corporate baseline provisioning creates a real instance through an idempotent tenant-scoped
  process.
- Approved decision: One active Corporate Collection Instance is allowed per tenant, CorporateOwnerId, and
  baseline.
- Approved decision: FU06 performs no bulk migration; existing Company instances remain unchanged.
- Approved decision: `DocumentScope` registration behavior belongs to MOD-0029-FU37; FU06 supplies its foundation.

## 20. Follow-up Items

- corporate-instance administration UI, if needed;
- migration execution;
- permission seed/integration task;
- reconciliation evidence for MOD-0029-FU37;
- scope-aware downstream document lifecycle audit.

### Post-implementation reconciliation — 2026-07-25

- [x] Backend implementation completed.
- [x] Application tests completed: 1911/1911 PASS.
- [x] Static FU06 verifier completed: PASS.
- [ ] Runtime smoke: BLOCKED during Platform startup.
- [ ] Mongo index evidence: FAIL — Mongo rejects `$ne` in the Corporate partial unique index.
- [ ] Authenticated provisioning/idempotency/retry smoke: not reached because API could not start.
- [ ] Runtime access and cross-tenant smoke: not reached because API could not start.
- [x] Company compatibility offline tests remain green.
- [x] MOD-0029-FU37 remains draft; FU06 is eligible for FU37 approval review.

Evidence: `docs/audits/mod-0028-fu06-runtime-smoke-reconciliation-2026-07-25.md`.
Compatibility-fix evidence: `docs/audits/mod-0028-fu06-mongo-index-compatibility-fix-2026-07-25.md`.

## 21. DCP-007 Approved Amendment — Import Completion Visibility and Consumer Guardrails

**Amendment status:** `approved`
**Approved by user:** `2026-08-27`
**Runtime authority:** `false`

This is a second, bounded governance coordination for FU06, approved by the user on 2026-08-27. It preserves the
existing DCP-004 relationship and every existing FU06 decision; DCP-007 neither replaces nor weakens DCP-004. The
parent FU06 pack remains `review`, and amendment approval does not authorize implementation.

### Amendment ownership

FU06 owns the completion-guard integration before Corporate provisioning-operation creation, Corporate definition
consumption, Corporate-scoped reconciliation/readiness, and explicit Corporate owner-scope enforcement. It consumes
FU02's combined completion/evidence guard and FU07's completion evidence; it does not own Company behavior, the FU07
operation/manifest aggregates, or the generic reconciliation engine as a business SoR.

### Mandatory execution order

```text
tenant + baseline lookup
→ combined completion/evidence guard
→ CollectionScopeType == Corporate
→ ScopeOwnerId == CorporateOwnerId
→ scope-filtered definition/instance/provider read
→ provisioning/reconciliation
→ side effects
```

### Required behavior

- A failed completion/evidence guard creates no Corporate provisioning operation, outcome, or instance.
- Corporate definition consumption begins only after the guard and explicit owner-scope validation succeed.
- Corporate reconciliation/readiness reads only the requested Corporate owner scope.
- Every generic reconciliation-engine call supplies explicit `CollectionScopeType.Corporate + ScopeOwnerId` and
  validates `ScopeOwnerId == CorporateOwnerId` before any scoped read.
- Company instances never enter Corporate provider, readiness, count, finding, or reconciliation results.
- Scope-less, owner-mismatched, cross-tenant, or incomplete calls fail closed before provisioning/reconciliation.
- Reconciliation/provisioning side effects begin only after completion and scope-owner validation; retries re-evaluate
  both conditions.
- The generic reconciliation engine is an owner-neutral technical component, not authority or a business SoR.
- Existing FU09 annotations are AS-IS drift evidence only and establish no authority.
- DCP-007 authorizes no Company sharing, overlay, local addition, group-node propagation/removal, or template
  propagation behavior.

### Amendment acceptance criteria

- [ ] Corporate provision and retry enforce the mandatory order before operation creation or definition reads.
- [ ] Failed completion/scope validation produces no operation, instance, outcome, or reconciliation finding write.
- [ ] Corporate reconciliation requires explicit Corporate scope and excludes every Company instance sharing the same
      baseline.
- [ ] Provider/readiness queries are owner-scoped and cannot aggregate all same-baseline tenant instances.
- [ ] Scope-less or mismatched Corporate calls fail closed with controlled non-leaking behavior.
- [ ] Generic engine use remains technical under FU06 ownership and becomes no independent canonical owner.
- [ ] Existing DCP-004 membership, Corporate partition/idempotency/access decisions, and Company compatibility remain
      unchanged.

### Amendment test expectations

- Provision/retry tests instrument operation, definition, and instance repositories to prove zero reads/writes after
  completion or owner-scope guard failure.
- Scope tests cover missing scope, non-Corporate scope, mismatched CorporateOwnerId/ScopeOwnerId, and cross-tenant IDs.
- Mixed Company/Corporate fixtures sharing a baseline prove Corporate-only provider/readiness/reconciliation results.
- Concurrency/idempotency tests prove completion/scope changes are re-evaluated before operation creation and retry.
- Negative tests cover guard unavailable, integrity mismatch, owner mismatch, provider scope leakage, and orphan-state
  absence while preserving existing FU06/DCP-004 behavior.

### Amendment governance gates

- DCP-007 remains `under-review`; FU07 remains `draft` with `runtime_code_allowed: false`.
- This amendment is approved at governance level; runtime implementation remains prohibited until DCP-007, FU06's
  parent runtime-readiness gate, and the active member-pack execution gates close.
- DCP-007 G2 is resolved because FU02, FU03, FU05, and FU06 amendments received separate user approval on 2026-08-27.
- This amendment does not close G12, load/lease/heartbeat, retention/audit, FU07 approval, or runtime-evidence gates.
- It creates no permission seed, MOD/FU identity, Gateway change, Company sharing/overlay, or template-propagation scope.

### Approval note

- Approval covers only this amendment's scope, acceptance criteria, and test governance contract.
- Code may start only after DCP-007 and the active member pack pass their separate execution gates.
- This approval is not runtime implementation, deployment, or activation authority.
