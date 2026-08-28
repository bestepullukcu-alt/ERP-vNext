# MOD-0029-FU37 Corporate/Company Registration Amendment — Approval / Ready-for-dev

## 1. Summary

MOD-0029-FU37 decisions are closed and the pack is promoted from `draft` to `ready-for-dev`. Runtime
implementation remains `not-started`. No backend, frontend, AuthService, Gateway/Ocelot or MOD-0028 runtime file
was changed.

## 2. Inputs reviewed

DCP-004 and capability index; FU06 pack and backend/runtime/index-fix audits; FU36 pack; FU36A/FU36B audits; FU37
draft; identity and implementation registries; existing FU36 registration contracts and unified-create form.

## 3. FU06 dependency status

FU06 is `implemented-runtime-green-with-nonblocking-gaps`: Corporate scope, typed owner, provisioning, partition,
deny-by-default access, Mongo-compatible uniqueness and real-Mongo startup are available. Full-fleet authenticated
provisioning/access/cross-tenant smoke remains a non-blocking tracked risk.

## 4. Decisions approved

Approved: DocumentScope; Company and Corporate matrices; ControlledDocument typed ownership; immutable operation
snapshot/retry; partition contracts; instance/folder alignment; governed language/retention; manual-link
compatibility; composed permission model; downstream non-automation boundary.

## 5. Company scope contract

Company requires CompanyId, OwnerCompanyId, a Company CollectionInstance and a folder inside that instance.
Existing FU36 behavior and Company membership/access rules remain backward compatible.

## 6. Corporate scope contract

Corporate sends no CompanyId or OwnerCompanyId. It requires CorporateOwnerId/ScopeOwnerId, an existing FU06
Corporate CollectionInstance, an aligned Corporate folder and explicit governed Corporate access. FU37 does not
provision Corporate instances.

## 7. ControlledDocument ownership decision

ControlledDocument becomes scope-aware. Company retains CompanyId/OwnerCompanyId. Corporate uses DocumentScope,
CorporateOwnerId/ScopeOwnerId and governance owner organization/function/role metadata. Dummy CompanyId and a
nullable-only quick fix are prohibited.

## 8. Operation snapshot/retry decision

The operation snapshots scope, typed owner, instance, folder, partition, relevant baseline/provisioning reference,
and governed language/retention ids/codes. Retry cannot retarget them and cannot create duplicates.

## 9. Storage partition decision

Company: `tenant/{tenantId}/company/{companyId}/folder/{folderId}`.
Corporate: `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`.
Any recomputed value must match the immutable snapshot.

## 10. Folder/CollectionInstance alignment

Company folders must belong to the selected Company instance and owner. Corporate folders must belong to the
selected Corporate instance and owner. Cross-scope, cross-owner and cross-tenant selections are rejected without
leaking target existence.

## 11. Language/Retention governed selection

Both are governed single Select2 values. Missing governed data blocks submission; free-text or fake fallback is
not allowed. Stable id/code values are retained in the operation snapshot.

## 12. Manual link scope compatibility

Manual links are limited to authorized legacy/migration/reconciliation use and require tenant, scope and owner
compatibility. Cross-scope and Company A→Company B links are non-waivable validation failures.

## 13. Permission model

FU37 reuses FU36 registration view/create/reconcile keys and composes Corporate authorization with MOD-0028
Corporate folder access. No AuthService seed is changed. A new Corporate-specific key requires a separate
follow-up only if implementation proves it necessary.

## 14. Downstream governance impact

FU37 does not automate UID allocation, lifecycle, approvals, release gates, signatures, training, retention or
quality events. Scope-awareness gaps in those consumers are implementation follow-ups.

## 15. FU36C/FU36D pause condition

Both remain paused until FU37 implementation completes and is reconciled. This approval resumes neither slice.

## 16. Registry changes

The identity registry moves FU37 to `ready-for-dev` without adding a duplicate row. The implementation-status
registry tracks code-bearing work; no FU37 row was added because runtime remains `not-started`.

## 17. Runtime code changed: No

Only the FU37 pack, DCP-004/capability status notes, identity registry and this audit were changed.

## 18. Remaining risks/gaps

FU06 full-fleet authenticated smoke remains open. Implementation must validate the persistence migration for
typed ControlledDocument ownership, governed lookup availability, downstream scope consumers and composed
Corporate authorization.

## 19. Final recommendation

Accept FU37 as `ready-for-dev / not-started`. Begin implementation only through a new explicitly scoped
orchestration task and keep FU36C/FU36D paused until FU37 implementation reconciliation.
