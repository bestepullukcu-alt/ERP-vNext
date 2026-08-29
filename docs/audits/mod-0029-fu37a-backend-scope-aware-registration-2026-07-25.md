# MOD-0029-FU37A Backend Scope-Aware Registration — 2026-07-25

## 1. Final Verdict

**PASS_WITH_GAPS.** FU37A backend foundation is implemented and automated evidence is green. The remaining gaps
belong to the approved later surfaces: FU37B frontend/localization, manual-link and reverse-navigation hardening,
governed lookup runtime availability, and authenticated full-fleet smoke. Whole FU37 is not complete.

## 2. Initial Audit Summary

The approved FU37 pack, DCP-004, FU36A registration foundation, MOD-0028-FU06 Corporate Collection Instance
contract, storage/versioning seams, access evaluators and existing tests/verifiers were audited before changes.
The existing repository model represents folder nodes as `CollectionInstance` records; FU37 consumes those
existing records and does not provision a Corporate instance.

## 3. Backend Scope Delivered

The Platform backend now accepts Company or Corporate registration, preserves missing-scope requests as Company
for FU36 compatibility, validates scope-specific ownership and target alignment, freezes target identity for
retry, evaluates the correct access policy and hands the exact validated partition to content storage.

## 4. DocumentScope Contract

`DocumentScope` is a domain enum with `Company = 0` and `Corporate = 1`. The API accepts the scope as an additive
field; omission maps to Company, while unknown values reach validation and are rejected. `TenantId` is not exposed
in the request and continues to come only from server tenant context.

## 5. ControlledDocument Ownership Amendment

`ControlledDocument` records typed scope ownership using `DocumentScope`, `ScopeOwnerId` and conditional
`CorporateOwnerId` or Company fields. Corporate registration persists no dummy CompanyId/OwnerCompanyId. Folder,
partition and governance-owner metadata are stored with the document.

## 6. Request Validation Matrix

Company requires `CompanyId` and `OwnerCompanyId` and rejects `CorporateOwnerId`. Corporate requires
`CorporateOwnerId`, rejects Company ownership fields and requires an explicit folder. Both scopes require a
collection target and governed language/retention stable values. Unknown scope is invalid.

## 7. Operation Snapshot / Retry Immutability

Registration operations capture scope, scope owner, conditional owners, instance, folder, partition, baseline and
provisioning references, governed language/retention and governance-owner metadata. A deterministic scope
fingerprint prevents reuse of an idempotency key with a different target. Retry reconstructs only the persisted
snapshot and cannot switch scope, owner, folder, instance or partition.

## 8. CollectionInstance / Folder Alignment

The selected folder is resolved through the existing MOD-0028 read seam. Scope type, scope owner and conditional
Company identity must match the request. Mismatch returns non-leaking 404. The target must be usable. FU37 does not
create or provision Corporate collection instances.

## 9. Storage Partition Alignment

Company uses `tenant/{tenantId}/company/{companyId}/folder/{folderId}`. Corporate uses
`tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`. The content gateway accepts the validated
partition additively, rejects traversal and requires the active tenant prefix. Existing callers without a
partition retain the previous Company object-key behavior.

## 10. Permission / Access Model

Existing registration endpoint permissions are unchanged. Company folder creation continues through the existing
Company access evaluator. Corporate creation uses `CorporateCollectionFolderAccessEvaluator` with the explicit
`CreateDocument` action; Company membership is not treated as Corporate access. No AuthService seed was changed.

## 11. Tests / Verifier

- `ControlledDocumentRegistration`: 6/6 passed.
- `Corporate`: 9/9 passed.
- `Fu37`: 7/7 passed.
- Full Platform Application suite: 1919/1919 passed.
- FU37A verifier: passed.
- FU36A regression verifier: passed.
- MOD-0028-FU06 verifier with `-SkipBuild`: passed.

## 12. Build Results

`Diten.Platform.API.csproj` Debug isolated-output build passed with 0 errors. Existing repository warnings remain,
including nullable file-input warnings, repository member-hiding warnings and framework obsolescence warnings.
The FU37A service nullable-flow warning was removed before final verification.

## 13. Pack / Registry Reconciliation

FU37 remains `ready-for-dev`; only `runtime_implementation` is reconciled to
`backend-foundation-implemented`. The pack explicitly leaves frontend, manual-link/reverse-navigation and runtime
smoke open. DCP-004 Phase 3 records FU37A completion while FU36C/FU36D remain paused. The code-truth registry has a
partial `Sadece-backend` FU37 row.

## 14. Guardrail Verification

No frontend, AuthService, Gateway/Ocelot or MOD-0028-owned provisioning mutation was made for FU37A. No DELETE
route, approval/release/signature/training automation, dummy CompanyId, client TenantId, commit or push was added.
Pre-existing unrelated dirty-worktree changes were preserved.

## 15. Remaining Gaps

FU37B must implement the conditional Company/Corporate form and localization. Governed language and retention
catalog availability still needs runtime proof. Manual-link and reverse-navigation compatibility enforcement is
not part of FU37A. Authenticated provisioning/access/cross-tenant and end-to-end registration smoke remain for the
approved runtime phase.

## 16. Files Changed

Changes are limited to FU37 backend domain/API/application/infrastructure seams, FU37 tests/verifier, this audit,
the FU37 pack, DCP-004 and the module implementation status registry. Exact paths are available in the scoped
verifier and working-tree diff; unrelated existing changes are not attributed to FU37A.

## 17. Confirmations

Frontend unchanged for this task. AuthService unchanged. Gateway/Ocelot unchanged. MOD-0028 provisioning behavior
unchanged. No commit or push performed. Corporate registration does not send a fake CompanyId and does not upload
directly to a baseline definition.

## 18. Next Recommended Step

Proceed with FU37B conditional frontend + MVC proxy + localization under its approved scope, then implement
manual-link/reverse-navigation compatibility and finish with FU36D authenticated runtime smoke. Keep FU36C/FU36D
paused until their explicit predecessor gates are closed.
