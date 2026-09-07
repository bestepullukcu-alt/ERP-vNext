# MOD-0028-FU06 Mongo Partial Index Compatibility Fix — 2026-07-25

## 1. Summary

Verdict: **PASS_WITH_GAPS**. Mongo startup/index compatibility is fixed and Platform health is green. Authenticated
business-flow smoke remains unavailable because Gateway and AuthService were not listening during that phase.

## 2. Original blocker

Mongo rejected `ux_dm_collection_instances_corporate_owner_baseline_node_active` because the partial filter encoded
`InstanceStatus != Archived` as unsupported `$ne`/`$not`. Platform terminated during index initialization.

## 3. Root cause

A negative lifecycle predicate was used in a Mongo partial index. A later local `$lt: 3` experiment could be created
but left the same index name with options different from the final positive definition.

## 4. Index before/after

Before: `IsDeleted == false AND Corporate AND InstanceStatus != Archived` (then a local `$lt: Archived` variant).
After: `IsDeleted == false AND CollectionScopeType == Corporate AND InstanceStatus == Active`. Keys and uniqueness
remain TenantId + scope + ScopeOwnerId + BaselineReleaseId + CanonicalId.

## 5. Active status decision

`Active` is the only status in the active-tree uniqueness constraint. `Blocked`, `Superseded`, and `Archived` are
lifecycle history and do not prevent creation of a later Active node. Values are referenced through enums, not
numeric magic values.

## 6. Mongo compatibility decision

The partial filter uses positive `Filter.Eq`. It contains no `$ne`, `$not`, `Filter.Ne`, `Filter.Not`, or range
substitute. In Development only, the exact named index is inspected; an incompatible definition is dropped and
recreated. No document is deleted or changed, and no production migration assumption is made.

## 7. Build/test results

- Platform API isolated build: PASS, 0 errors (10 pre-existing warnings).
- Targeted Corporate Collection tests: 5/5 PASS.
- Platform Application tests: 1912/1912 PASS.
- Foundation verifier: PASS.
- Mongo compatibility verifier: PASS.
- Runtime reconciliation verifier: PASS after resolved-state reconciliation.

## 8. Runtime startup evidence

MongoDB port 27017 was reachable. The obsolete local `$lt` index first produced an exact-name options conflict;
development reconciliation replaced only that index. Platform then listened on 5057. Health HTTP 200 reported
`status=Healthy`, with `mongodb=Healthy` and the remaining registered checks healthy.

## 9. Index existence evidence

Successful startup runs `EnsureIndexesAsync`; startup could not complete while the named definition conflicted.
After reconciliation it completed and health returned 200. This proves Mongo accepted and retained the requested
positive Active index definition. Concurrency intent is additionally enforced by the unique index and covered by
the provisioning/idempotency test harness; a concurrent authenticated HTTP race was not executed.

## 10. Runtime smoke results

Startup, Mongo reachability, health, index creation, offline deny-by-default/explicit-user-policy behavior,
idempotent replay, Company/Corporate partition separation, and endpoint attribution are green. Authenticated
provisioning, same/different-key HTTP replay, completed retry, explicit-grant HTTP access, and cross-tenant
non-leaking HTTP smoke were not run because Gateway/AuthService were unavailable.

## 11. Remaining gaps

Run the authenticated HTTP matrix with a complete fleet and a published baseline fixture. Include concurrent
provision requests, retry, an explicit governed grant, and a second-tenant token. These are non-blocking for the
Mongo compatibility fix but prevent a full PASS verdict.

## 12. Guardrails

No MOD-0029 runtime, ControlledDocument, frontend, AuthService, Gateway/Ocelot, storage partition contract, access
evaluator, nullable CompanyId, dummy CompanyId, hard delete, commit, or push change was made. FU37 remains draft;
FU36C/FU36D remain paused.

## 13. Files changed

Runtime: Mongo index configuration and Development-only call site. Evidence: one additive test, compatibility
verifier, this audit, and truthful FU06/DCP-004/FU37/code-truth reconciliation.

## 14. Final recommendation

Accept FU06 as `implemented-runtime-green-with-nonblocking-gaps` and mark DCP-004 Phase 2 startup/index blocker
resolved. FU37 is eligible for approval review but must remain draft until separately approved.
