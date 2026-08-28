---
id: MOD-0029-FU37
name: Corporate/Company Registration Amendment
parent: MOD-0029
previous: MOD-0029-FU36
amends: MOD-0029-FU36
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: BaseEntity
status: ready-for-dev
owner: platform-shared-services
branch: feature/pss/mod-0029-fu37-corporate-company-registration-amendment
started: 2026-07-25
target: 2026-08-21
form_field_count: 17
delivery_capability_pack: DCP-004
runtime_implementation: implemented-with-runtime-gaps
approved: 2026-07-25
---

# MOD-0029-FU37 — Corporate/Company Registration Amendment

> **Approved amendment to MOD-0029-FU36.** DCP-004 is approved and MOD-0028-FU06 is
> `implemented-runtime-green-with-nonblocking-gaps`. FU37 is `ready-for-dev`; FU37A backend foundation is
> implemented; FU37C manual-link scope guardrails and downstream fail-closed readiness are implemented while
> reverse-navigation is hardened through FU36C; authenticated runtime-smoke work remains open. FU36D remains paused.

## 1. Module Summary

Amend the FU36 governed controlled-document registration flow so a user explicitly chooses Company or Corporate
scope. Company behavior remains backward compatible. Corporate registration targets a real MOD-0028 corporate
instance and never sends a dummy CompanyId.

## 2. Ownership and Boundaries

In scope: registration request/validation, durable operation snapshot, controlled-document ownership amendment,
scope-aware storage handoff, retry rules, conditional Compact form, governed language/retention lookups, and
scope-mismatch behavior for linking/navigation.

Out of scope: MOD-0028 provisioning implementation, storage provider rewrite, approval/effective/signature
automation, external integration, UID allocation changes, hard delete, and unrelated FU36 redesign.

## 3. Owned Objects

Planned amendments:

- canonical `DocumentScope = Company | Corporate` usage;
- FU36 request and response contracts;
- registration-operation immutable scope snapshot;
- controlled-document scope/owner representation;
- scope-aware validation/orchestration;
- conditional Company/Corporate form selectors;
- manual-link and reverse-navigation scope compatibility checks.

MOD-0028 remains owner of instances, folders, provisioning, and partition descriptor semantics.

## 4. Entity Fields

| Conceptual field | Company | Corporate |
|---|---|---|
| DocumentScope | `Company` | `Corporate` |
| CompanyId | Required | Absent |
| OwnerCompanyId | Required/current behavior | Replaced by approved governance owner |
| ScopeOwner/Partition identity | Company | Corporate owner |
| CollectionInstanceId | Company instance | Corporate instance |
| FolderId | Same company instance | Same corporate instance |
| BaselineReleaseId | Optional/current contract | Required to resolve corporate structure unless instance is explicit |
| Operation scope fingerprint | Required | Required |

Typed scope ownership is approved. Legacy company fields may become conditionally nullable only together with
`DocumentScope` and typed owner invariants; a nullable-only shortcut is prohibited.

## 5. Repo Scope

Future implementation may touch:

- MOD-0029 FU36 registration Application/Domain/Infrastructure/API paths in `services/Diten.Platform/**`;
- MVC proxy/controller/view/JS/localization paths for the Master Register create flow;
- MOD-0029-specific tests and verifier;
- governance/audit files.

No runtime path is changed by this draft.

## 6. Protected Paths

- `.antigravity/**`;
- MOD-0028 mutation/provisioning code except through its approved public contract;
- `services/Diten.AuthService/**`;
- Gateway/Ocelot;
- other domains;
- archive paths and frozen shared layout;
- direct storage-provider implementation.

## 7. Dependencies

- DCP-004 approved;
- MOD-0028-FU06 approved, implemented, tested, and reconciled;
- MOD-0029-FU36 existing registration foundation;
- existing identifier, storage, Master Register, Controlled Document, and version contracts;
- governed language and retention lookup sources.

FU06's remaining authenticated full-fleet smoke is a tracked non-blocking risk, not an approval blocker.

## 8. Runtime Constraints

- TenantId comes only from authenticated server context.
- Registration is all-or-clear-failure and retryable without duplicates.
- Retry is pinned to its initial scope fingerprint.
- Company paths remain backward compatible.
- Corporate never falls back to company partition/access rules.
- Mismatched tenant, scope, instance, or folder produces no data leak or partial success.

## 9. Layout & Shell Contract

`shell: tenant`; all Razor pages explicitly use `Layout = "_LayoutTenantShell"`. With a maximum of 17 user-facing
fields across conditional paths, `golden_reference: compact` remains applicable. Create/Edit are separate page
surfaces; Compact offcanvas create/edit is prohibited.

## 10. Backend File Convention

Later work amends the existing FU36 feature using its current CQRS, handler, validator, repository, and controller
conventions. Shared scope semantics must have one canonical owner selected during approval; parallel enums with
different numeric/string values are prohibited.

## 11. Frontend File Contract

The FU36 Compact form gains:

- single `DocumentScope` selector first;
- Company path: Company → Collection Instance → Folder;
- Corporate path: QMS Baseline/Documentation Structure → Corporate Instance (hidden only when uniquely resolved)
  → Folder;
- governed single-select Governing Language and Retention Class;
- dependent reset/filter rules and accessible loading/empty/error states.

All labels/messages require the tenant module's seven-language RESX and `window.L10n` bridge. No hardcoded fallback
option may impersonate missing backend data.

## 12. Validation Rules

| Rule | Expected behavior |
|---|---|
| Company + missing CompanyId | Reject |
| Company + non-company instance/folder | Reject without leak |
| Corporate + CompanyId present | Reject |
| Corporate + missing corporate instance/folder | Reject |
| Scope owner/partition differs from instance | Reject |
| Folder not under selected instance | Reject |
| Retry key with changed scope fingerprint | Conflict; never retarget |
| Manual link scopes differ | Reject |
| Tenant mismatch anywhere | Non-leaking `404` |
| Language/retention outside governed lookup | Reject |

## 13. Failure Path to Verify

Verify invalid conditional payloads, tampered folder IDs, foreign tenants, mixed Company/Corporate links,
unavailable corporate instance, storage failure, operation restart, duplicate concurrent submission, and
downstream partial-write compensation/reconciliation. No success response is allowed until register, document,
version, binary reference, and link agree on scope.

## 14. Authorization Convention

Registration requires existing FU36 create rights plus permission to view/use the selected instance/folder.
Corporate registration additionally requires MOD-0028 Corporate folder access. Authorization is scope-aware and
deny-by-default. FU37 reuses the existing registration `view`, `create`, and `reconcile` keys; this approval adds no
AuthService seed. If implementation proves a Corporate-specific key necessary, it requires a separate permission
seed follow-up before runtime use.

## 15. Gateway / API Routing Decision

No Gateway/Ocelot change is authorized. MVC proxies continue through Gateway port 5000. Any missing route is a
separate integration-agent task after explicit approval.

## 16. Acceptance Criteria

1. `DocumentScope = Company | Corporate` is explicit; unknown scope is rejected.
2. The Company validation matrix preserves the FU36 Company contract.
3. The Corporate validation matrix consumes an existing FU06 Corporate Collection Instance without CompanyId.
4. ControlledDocument ownership is scope-aware and uses typed owner invariants; no dummy-company shortcut exists.
5. Operation snapshot and retry pin scope, owner, instance, folder and storage partition.
6. Company and Corporate storage partition contracts are enforced.
7. Company/Corporate CollectionInstance and folder alignment is validated without leakage.
8. Manual linking blocks cross-scope and cross-owner relationships.
9. Language and Retention Class are governed single Select2 selections with stable id/code snapshots.
10. Existing registration permissions plus MOD-0028 Corporate folder authorization are enforced.
11. FU06 is consumed as the approved Corporate target contract.
12. FU36C/FU36D remain paused until FU37 implementation completion/reconciliation.
13. Approval produces no runtime implementation; implementation status remains `not-started`.

## 17. Test Expectations

Future work requires unit/validator tests for the field matrix; integration tests for orchestration,
idempotency, partition handoff, compensation, authorization, and tenant isolation; Company regression tests;
frontend JS tests for cascading resets; seven-language resource verification; DataTable/Compact verifier where
applicable; builds for Platform and Diten.Web; and authenticated live smoke in FU36D.

## 18. Ready-for-dev Checklist

- [x] DCP-004 approved.
- [x] MOD-0028-FU06 implemented and reconciled.
- [x] DocumentScope canonical owner approved.
- [x] Corporate owner semantics approved.
- [x] Storage partition handoff approved.
- [x] Corporate access policy approved.
- [x] Company/Corporate field matrix approved.
- [x] Migration and rollout strategy approved: no bulk migration; manual link is restricted and scope-compatible.
- [x] Downstream release/training/retention/signature impacts dispositioned as non-automation/follow-up scope.
- [x] Existing FU36 permission model and Corporate access composition approved.
- [x] Pack promoted by explicit user approval.
- [x] FU37A backend foundation implemented; FU36C/FU36D remain paused.

### FU37A Backend Reconciliation — 2026-07-25

- [x] Scope-aware API/request, typed ownership and Company/Corporate validation matrix implemented.
- [x] Immutable operation snapshot, scope fingerprint and retry mismatch protection implemented.
- [x] Existing-instance/folder alignment, Corporate explicit-grant evaluation and exact storage partitions implemented.
- [x] Company backward-compatibility default retained; Corporate uses no dummy CompanyId.
- [x] Targeted tests, full Platform Application suite and FU37A/FU36A/FU06 verifiers pass.
- [x] FU37B conditional frontend selectors, governed lookup UI and seven-language localization implemented.
- [x] Manual-link scope/owner compatibility enforcement and reverse-navigation hardening.
- [ ] Authenticated runtime/full-fleet smoke and FU36D reconciliation.

### FU37B Frontend Reconciliation — 2026-07-25

- [x] Company-default DocumentScope selector and localized scope guidance implemented.
- [x] Company and Corporate fields are mutually exclusive and omitted from the opposite payload.
- [x] Company/Corporate CollectionInstance → Folder cascades use same-origin MVC proxies.
- [x] Corporate instance absence blocks submission and does not expose provisioning.
- [x] Language and Retention Class use governed single Select2 sources; unavailable lookup blocks submission.
- [x] Completed-only success, retry and localized 409/403/404 handling preserved.
- [x] FU24–FU29, FU36A/B, FU37A/B and FU06 verifier regressions and isolated Web build pass.
- [ ] Governed language lookup availability for the tenant actor requires authenticated runtime confirmation.
- [x] Manual-link/reverse-navigation hardening implemented.
- [ ] Authenticated runtime smoke remains pending.

The pack remains `ready-for-dev`: backend and conditional frontend foundations are implemented, but this is not
whole-FU37 completion.

## 19. Implementation Notes

The existing FU36 16-field Company contract remains the compatibility baseline. `DocumentScope` adds one leading
choice; fields are then conditional rather than all simultaneously required. Corporate Instance may be hidden
only if the selected baseline deterministically resolves to exactly one usable instance. Owner Company is not
forced onto Corporate documents; governance owner organization/function/role metadata replaces it.

### Approval Decision Log — 2026-07-25

- Approved decision: MOD-0029 registration becomes scope-aware through `DocumentScope = Company | Corporate`.
  Company preserves existing FU36 behavior; Corporate consumes the MOD-0028-FU06 Corporate Collection Instance.
  Unknown scope is rejected, scope is explicit in the request, and retry cannot change it.
- Approved decision: Company registration keeps `CompanyId`/`OwnerCompanyId` required and validates Company →
  CollectionInstance → Folder alignment. Storage remains
  `tenant/{tenantId}/company/{companyId}/folder/{folderId}`.
- Approved decision: Corporate registration uses no `CompanyId`, `OwnerCompanyId`, or dummy company. It requires
  `CorporateOwnerId`/`ScopeOwnerId`, an existing Corporate CollectionInstance and aligned Corporate folder.
  Storage is `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`.
- Approved decision: ControlledDocument ownership becomes scope-aware. Company documents keep
  `CompanyId`/`OwnerCompanyId`; Corporate documents use `DocumentScope`, `CorporateOwnerId`/`ScopeOwnerId`, and
  governance owner organization/function/role metadata. Conditional nullability is valid only with this typed
  invariant; a nullable-only quick fix and dummy CompanyId are prohibited.
- Approved decision: Registration operations persist DocumentScope, ScopeOwnerId, conditional CorporateOwnerId or
  CompanyId/OwnerCompanyId, CollectionInstanceId, FolderId, StoragePartition, Corporate BaselineReleaseId and
  provisioning reference when applicable, plus governed language and retention ids/codes. Retry cannot switch
  scope, owner, folder, instance, or partition; mismatch is rejected and no duplicate create is allowed.
- Approved decision: Company flow is Company/LegalEntity → Company CollectionInstance → folder → Company partition
  → ControlledDocument + Version + Master Register link. A Corporate folder is invalid.
- Approved decision: Corporate flow consumes an existing MOD-0028 Corporate CollectionInstance: Corporate owner
  and instance → Corporate folder → Corporate partition → ControlledDocument + Version + Master Register link.
  FU37 does not provision Corporate instances; missing/ineligible provisioning blocks submission. Company
  membership is not Corporate access.
- Approved decision: Language and Retention Class are governed single Select2 selections, not free text. Missing
  governed data blocks submission; stable ids/codes are stored in the operation snapshot with no fake fallback.
- Approved decision: Manual link enforces tenant, scope, and owner compatibility. Cross-scope, Company A→Company B,
  and cross-owner links are non-waivable failures. Manual link remains limited to authorized
  legacy/migration/reconciliation use.
- Approved decision: FU37 makes registration scope-aware but does not automate identifiers, lifecycle, approvals,
  release gates, signatures, training, retention, or quality events. Downstream adjustments are implementation
  gaps/follow-ups.
- Approved decision: FU37 uses existing registration permissions
  (`platform.document-management.master-register.registration.view`,
  `platform.document-management.master-register.registration.create`,
  `platform.document-management.master-register.registration.reconcile`) and relies on MOD-0028 Corporate folder
  access. No AuthService seed change is performed by this approval.

## 20. Follow-up Items

- FU36C reverse navigation and legacy-bypass hardening after this amendment;
- FU36D authenticated runtime smoke and commit-separation audit;
- scope-awareness audits for release gate, training, retention, signature, and reporting;
- migration execution if approved;
- corporate access administration UI if needed.
- authenticated FU06 full-fleet provisioning/access/cross-tenant smoke risk;
- Corporate-specific permission seed follow-up only if implementation proves the composed model insufficient.
