# MOD-0028-FU06 Runtime Smoke & Post-Implementation Reconciliation — 2026-07-25

## 1. Summary

**Verdict: BLOCKED.** Offline build/tests/verifier remain green, but the real Mongo-backed Platform startup fails
while creating the FU06 Corporate unique partial index. Runtime business logic was not changed during this smoke
task, so the index defect remains for a separately authorized fix.

## 2. Runtime Environment

- MongoDB `127.0.0.1:27017`: reachable/listening.
- Gateway `5000`: listening.
- Web `5001`: listening.
- AuthService `5056`: listening.
- Platform `5057`: initially down; safe hidden start attempted with Development configuration and real local Mongo.
- Platform result: process terminated during index initialization before Kestrel could listen.

## 3. Authenticated Route Smoke

Not reached. Platform could not complete startup, so no token was sent and no authenticated success was claimed.
The credentials supplied in prior user context were not persisted or logged.

## 4. Provisioning Happy Path

Not reached through HTTP because Platform startup failed. The prior application harness remains 4/4 green, but it
is not reported as authenticated runtime proof.

## 5. Idempotency Evidence

Offline targeted tests prove same-key replay returns the existing CollectionInstance without another tree.
Real-Mongo/API idempotency smoke was not reached and is an open blocking gap.

## 6. Retry Evidence

The retry endpoint and offline implementation verifier exist, but completed/failed/unknown runtime retry smoke was
not reached. No fake PASS is claimed.

## 7. Mongo Index Evidence

Mongo was reached and executed `createIndexes`. It rejected:

`ux_dm_collection_instances_corporate_owner_baseline_node_active`

The partial filter contains `IsDeleted = false`, `CollectionScopeType = Corporate`, and
`InstanceStatus != Archived`. Mongo returned `Expression not supported in partial index` for the `$ne` expression.
This is direct real-Mongo evidence and blocks Platform startup. The idempotency/lookup indexes after this creation
point could not be runtime-confirmed in this run.

## 8. Concurrency Evidence

Not executed. Since index initialization failed, concurrent HTTP requests could not be sent. Offline repository
and service logic is not substituted for real-Mongo concurrency evidence.

## 9. Folder Access Evidence

Offline test evidence remains green: no grant returns false, Company membership alone gives no Corporate access,
and an explicit user policy grants the requested action. Runtime 403/success tests were not reached.

## 10. Cross-Tenant Evidence

Not executed. The Platform API was unavailable. No cross-tenant success or non-leaking runtime claim is made.

## 11. Company Compatibility Evidence

The full 1911-test Application suite was green before this smoke and existing Company construction records
`ScopeOwnerId = CompanyId`. No runtime Company request was possible after the startup failure.

## 12. Storage Partition Evidence

Offline exact-literal tests remain green:

- Company: `tenant/{tenantId}/company/{companyId}/folder/{folderId}`
- Corporate: `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`

Corporate provisioning assigns no CompanyId. CompanyId was not made nullable and no dummy value was introduced.

## 13. Pack / Registry Reconciliation

- DCP-004 remains `approved`; Phase 2 is marked runtime-blocked.
- MOD-0028-FU06 remains `review`; runtime status is `implemented-with-runtime-gaps`.
- Code-truth registry records the Mongo startup blocker.
- MOD-0029-FU37 remains `draft` and is not eligible for approval review.
- FU36C/FU36D remain paused.

## 14. Remaining Gaps

1. Correct the unsupported Corporate partial unique index filter in a separately authorized runtime-fix task.
2. Confirm all intended indexes exist on real Mongo.
3. Rerun authenticated provisioning, same/different-key idempotency, completed/unknown retry, and concurrency.
4. Rerun explicit-grant/deny and cross-tenant non-leaking API smoke.
5. Rerun Company runtime regression.

## 15. Guardrails

- Runtime business logic changed during smoke: No.
- Dummy CompanyId introduced: No.
- Nullable-only CompanyId fix introduced: No.
- MOD-0029 runtime changed: No.
- Frontend/AuthService/Gateway/Ocelot changed: No.
- DELETE/hard delete/Mongo hand-edit used: No.
- Token/password persisted: No.
- Commit/push: No.

## 16. Files Changed

Only governance reconciliation, this audit, and an additive evidence verifier are changed by the smoke task.
Pre-existing implementation/runtime working-tree changes are preserved.

## 17. Final Recommendation

Authorize a narrowly scoped MOD-0028-FU06 Mongo index compatibility fix. After that fix builds and tests, rerun this
runtime smoke from the beginning. Do not promote or implement FU37 and do not resume FU36C/FU36D before a green
Mongo-backed reconciliation.
