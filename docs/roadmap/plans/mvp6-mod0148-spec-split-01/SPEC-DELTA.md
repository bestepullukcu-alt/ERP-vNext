# MOD-0148 Supplier Portal — spec delta

Status: **SPEC TARGET SELECTED / CONCURRENCE OPEN / RUNTIME HELD**

Controlling selection: the user's `MVP6-SUPPLIER-SPEC-SPLIT-01` message selects `DECISION-PACK.md` SS-01…SS-09 option A and AC-01…27 solely as the next-spec policy target. It does not approve a contract version, producer transport, claim carrier, runtime implementation, pack promotion, or another owner's policy.

The module pack remains `draft`. This document proposes the bounded MOD-0148 delta; it does not edit the pack.

## Frozen surface retained

MOD-0148 stays backend-only and owns exactly these five `SUPPLIER-PERFORMANCE` v1 operations:

1. `getSupplierPortalStatus`
2. `listOwnPortalSubmissions`
3. `createOwnPortalSubmission`
4. `getOwnPortalSubmission`
5. `submitOwnPortalSubmission`

No internal review operation, supplier-facing UI, gateway route, permission seed, publisher, or Supplier master is added. `UNDER_REVIEW`, `ACCEPTED`, `REJECTED`, and `SupplierPortalSubmissionReviewed` remain contract declarations without a reachable MOD-0148 command in this bounded slice.

## What existing SUPPLIER v1 supports

| Capability | Current usable authority | Bound |
|---|---|---|
| Opaque Supplier identity lookup | `GET /api/suppliers/{supplierId}` | Tenant-scoped identity, name, and one of `Active/OnHold/Blocked/Inactive` |
| Base Supplier status | `Supplier.status` | Can feed the selected portal status policy only after authoritative Tenant+LE eligibility is separately available |
| Unknown Supplier | `getSupplier` 404 | Base lookup fact; it is not itself the portal actor error mapping |
| Batch identity/status | `POST /api/suppliers/validate` | Present but not selected for single-record portal requests; nullable/required/completeness defects prevent treating it as a strict oracle |

SUPPLIER v1 does **not** establish LegalEntity eligibility, actor binding, revocation, permission/membership currency, or own-record authority. A Supplier `Active` response alone cannot admit a portal request.

## Selected actor-binding target

The next spec targets a security-owned binding keyed by `(validatedIssuer, stableSubject, trustedTenantId, trustedLegalEntityId)` and resolving to an opaque MOD-0140 `SupplierId`, binding identity/revision, and active/revoked state.

- At most one active Supplier binding exists for one actor within one Tenant+LE.
- Multiple actors may bind to the same Supplier.
- One actor may have different bindings in separately authorized Legal Entities; the request must already contain one trusted LE. No first-LE selection is allowed.
- Email, contact, display name, generic user ID, or equality between user ID and Supplier ID never resolves the Supplier.
- JWT, trusted Tenant+LE, permission/membership, current binding, and base eligibility are rechecked before every request and replay.
- Revocation committed before a mutation commit must win through binding-revision fencing: no business write, receipt, or event. If the mutation committed first, its historical result remains, while later access fails.

These are selected consumer semantics. The security-owned provider, endpoint or claim carrier, current-revision query, and atomic revocation fence are unresolved GAPs. No local actor-binding table or invented claim is prospective MOD-0148 scope.

## Authorization, eligibility, and own-record order

The selected decision order is:

1. Authentication: invalid/expired JWT → 401; no downstream lookup.
2. Trusted scope and permission/membership: absent or invalid → 403; no mapping/target lookup.
3. Syntactic request validation: forbidden identity override or invalid headers/body → 400 `INVALID_REQUEST`.
4. Current actor binding and base portal eligibility.
5. For target operations, lookup exact `(TenantId, LegalEntityId, mappedSupplierId, submissionId, IsDeleted=false)`.
6. Existing receipt lookup.
7. For a new mutation, `If-Match` and lifecycle checks.
8. Atomic business write, receipt, audit, and Pending outbox.

Mapping absent, revoked, or ambiguous → 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED`. Mapping-authority timeout targets 503 `PORTAL_IDENTITY_UNAVAILABLE`; that wire behavior still requires contract/security concurrence. After identity resolution, absent, deleted, foreign-tenant, foreign-LE, or foreign-Supplier records uniformly return 404 `UNKNOWN_SUBMISSION` without existence disclosure.

Body or query fields named `supplierId`, `tenantId`, or `legalEntityId` do not affect trusted identity and are rejected. `CreatePortalSubmissionRequest.additionalProperties: false` already supports the body part of this rule. Equivalent query/error coverage is not complete in v1.

## Base status behavior

With a valid current binding and affirmative Tenant+LE eligibility:

| SUPPLIER status | `/portal/status` | list/get/create/submit |
|---|---|---|
| `Active` | 200 `ACTIVE` with actual scoped summary | Allowed subject to normal gates |
| `OnHold`, `Blocked`, `Inactive` | 200 `SUSPENDED`, `openSubmissionCount=0`, `lastSubmissionAt` absent/null | 403; no receipt or record content |

`PENDING` is not produced in this slice. Missing binding is 403, not a successful pending response. Unknown or LE-ineligible mapped Supplier is also 403 identity unresolved. Supplier timeout, malformed/mismatched response, or incomplete authoritative answer targets 503 `SUPPLIER_BASE_UNAVAILABLE` with no business write, receipt, or event. The exact LE carrier and expanded route/error declarations remain common-seam work.

## Submission lifecycle and replay

- Create yields a module-owned `DRAFT`. Submit permits only `DRAFT → SUBMITTED`; no hidden review transition is introduced.
- Submit requires the current version for a new operation. A new key with stale version returns 409 `VERSION_CONFLICT`; a current-version `SUBMITTED → SUBMITTED` attempt returns 422 `INVALID_STATUS_TRANSITION`.
- Receipt scope is `(TenantId, LegalEntityId, mappedSupplierId, validatedIssuer, subject, operationId, targetId-or-CREATE, exactIdempotencyKey)`.
- `Idempotency-Key` is 1..200 characters. Leading/trailing whitespace is rejected; it is not trimmed into an alias. Identity, operation, target, and key comparisons are ordinal/exact.
- Fingerprint is a typed parsed-request snapshot over method, operation, target, body, and `If-Match`; object key order is irrelevant, array order and string bytes are significant, correlation is excluded, and duplicate JSON properties are rejected.
- Same receipt tuple and fingerprint returns 200 with the original domain result, without version/event/write growth. The current request UUID is returned; original correlation remains in receipt/audit evidence. Exact wire representation remains a common contract GAP.
- Same tuple/key with a different fingerprint returns proposed 409 `IDEMPOTENCY_KEY_REUSED`; v1 does not declare it.
- Concurrent identical requests produce one commit/outbox and one replay. A different-fingerprint loser returns 409.
- Remap S1→S2 creates a separate Supplier namespace and cannot reveal an S1 receipt. Another actor or another LE has a separate receipt scope. A later active binding revision for the same actor+Supplier may read the old receipt only after current authorization and eligibility gates pass.
- Receipt retention follows the business record lifetime; expiry cannot reopen duplicate create.

The draft pack's replay index `(TenantId, SupplierId, Operation, IdempotencyKey)` is insufficient for this target because it omits LegalEntity, actor, and target. This is a prospective pack/runtime delta only after the relevant owners concur and promotion/runtime authority exists.

## Persistence and evidence invariants for a future authorized implementation

- Records retain server-resolved TenantId, LegalEntityId, mapped SupplierId, version, timestamps, and correlation evidence; no Supplier profile/KYC/credential replica.
- Every read predicate contains Tenant+LE+mapped Supplier+non-deleted state.
- Create/submit atomically commits the business state, receipt, audit record, and Pending outbox record; publishing is outside this slice.
- Denied operations create no business write, receipt, or lifecycle outbox. Security denial audit, if required by its owner, is counted separately.
- `SupplierPortalSubmissionSubmitted` carries the existing v1 event fields and one server UTC `occurredAt`; new causation or replay wire fields are not invented here.

## Held shared seams

The single Central Control Tower Supplier Seam Owner coordinates all shared changes. MOD-0148 must return, not implement, these needs:

- MOD-0140 Tenant+LE eligibility, status failure/correlation behavior, and live producer proof;
- security actor-binding provider, trusted LE carrier, current permission/membership, revocation fencing, and failure contract;
- successor disposition for route-complete 400/403/409/503 behavior, replay correlation, and any binding metadata carrier;
- exact contract version and strict-consumer compatibility;
- fixture implementation, which must stay test-only and is never live producer evidence.

## Result

This delta is ready for owner/contract concurrence review, not development. Pack status stays `draft`; no Phase 1.5, ready-for-dev, DEV GO, runtime implementation, or publication follows.
