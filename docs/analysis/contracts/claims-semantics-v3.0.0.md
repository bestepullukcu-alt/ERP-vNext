# Claims semantics — SHIPMENT-BUNDLE 3.0.0 / wire v1

Publication proposal: SHIPMENT-BUNDLE 3.0.0 / wire v1. Scope: Claims operations only. The approved reconstructed design is unchanged; candidate-to-final metadata and path deltas are bound separately. Actual publication requires the separate owner release decision.

## Controlling reconciled rules
Withdrawn requires investigate, Closed requires decide. Open target has create permission check then invalid lifecycle. No creator-only exception or mandatory actor separation. Lifecycle 422 INVALID_CLAIM_TRANSITION; payload 409 IDEMPOTENCY_KEY_REUSED; root 409 CLAIM_CORRELATION_MISMATCH. No worker, publisher, transport or finance posting; outbox remains Pending.
All seven frozen arrows preserved. Non-Approved commands with non-null approvedAmount fail422; omission/null retains previous value. Claimed>0, Approved requires 0<=amount<=claimed including zero and negative-zero. BSON string preserves original text; exact coefficient/scale comparison; no rounding/precision/scale/ISO restriction. Definite uncommitted number collision fails atomically503 CLAIM_STORAGE_UNAVAILABLE, no automatic renumber. Unknown commit is not proof of zero writes; resolve receipt or return503 pending recovery.
Root missing/null/empty→503 CLAIM_REFERENCE_INCOMPLETE; malformed→502 CLAIM_REFERENCE_INVALID; different valid UUID→409 CLAIM_CORRELATION_MISMATCH, including nil when different. Same nil is not automatically missing. Source root from published producer seam only, never DB/trace/ID derivation. Transition/replay use saved root. Root check precedes payload conflict. JWT authentication rejection401 precedes trusted-identity403; no assertion that every parser rejects duplicate JWT identity at the same layer.

## Adopted detailed policy
The following source sections are subordinate to the controlling reconciled rules above. Old proposal/required-owner labels identify provenance, not extra approvals or runtime grants. Historical A Withdrawn=create table entries, optional fake transport and root field proposal are superseded explicitly above; no producer field is embedded here.

## D187-01 — reference eligibility and observation boundary

**Required owners:** logistics business + Shipment/Carrier contract owners + security. Sources S1 reference schemas,
S2 ownership, S3 mock-first consumption. Impact: read-only HTTP dependency, historical references, no distributed transaction.

Proposal:
1. Fresh create accepts Shipment status exactly Dispatched, InTransit, Delivered, Exception or Closed.
   Draft, Planned and Cancelled return 422 `CLAIM_SHIPMENT_INELIGIBLE`.
2. Shipment must be retrieved with trusted tenant/LE context. Absent/null carrier means unassigned, never inferred.
   Explicit carrier must equal the Shipment's non-null carrierId, and resolve in the same scope using the published
   **unfiltered GET carrier list** (no invented by-ID endpoint). Active, Suspended and Retired all qualify for historical claims.
3. Evidence remains opaque strings: omission/empty array allowed, order/duplicates/empty strings retained.
   No existence, ownership or binary-content verification is asserted. `null` array is invalid schema (400).
   Verified evidence would require a separately published seam and revised decision, not an invented validator.
4. Resolve only on fresh create, before the Mongo transaction; persist immutable decision-field snapshot and observedAt.
   Transitions and committed replays do not re-query mutable references. Eligibility is **as observed**, not as of commit.
   No copied source master or cross-feature DB access.
5. Missing/foreign reference returns generic 404 `CLAIM_NOT_FOUND`. Structurally malformed source response or mismatched
   requested Shipment identity returns 502 `CLAIM_REFERENCE_INVALID`; schema-valid response missing fields needed for
   a decision (e.g. optional status/carrierId) returns 503 `CLAIM_REFERENCE_INCOMPLETE`.
   Timeout/unavailability returns 503 `CLAIM_REFERENCE_UNAVAILABLE`; none writes business state.

| Scenario | Exact proposed acceptance |
|---|---|
| R01 | Each of 8 frozen Shipment states exercised: five eligible creates succeed; three reject as above; rejection creates zero claim/receipt/audit/outbox |
| R02 | Explicit matching carrier in each of Active/Suspended/Retired succeeds; different or explicit null Shipment carrier rejects 422 `CLAIM_CARRIER_MISMATCH`; omitted source carrier field gives 503 |
| R03 | Missing/foreign Shipment or explicit Carrier gives identical generic 404; no foreign field echoed; outbound method/path/scope captured |
| R04 | Evidence omitted and [] accepted; ["", "e1", "e1"] preserved; null rejected; no evidence lookup or verified flag implied |
| R05 | Source becomes ineligible after captured read: committed snapshot explains permitted create; replay/transition with source offline still works under receipt/lifecycle rules |

## D187-02 — monetary relationships and operational settlement

**Required owners:** logistics business + finance policy + contract. Sources S1 amount fields/lifecycle, S2 non-posting scope.
Impact: no currency conversion, invoice/payment, valuation or total-claims ceiling can be inferred from Shipment data.

Proposal: claimedAmount > 0. Entering Approved requires non-null approvedAmount with
**0 ≤ approvedAmount ≤ immutable claimedAmount**. Zero approval is explicitly permitted; no separate monetary tier
or upper cap beyond this relationship. On every other target approvedAmount must be omitted or null;
non-null rejects 422 `CLAIM_APPROVED_AMOUNT_NOT_ALLOWED`. Existing approved amount is preserved through Settled/Closed.
ResolutionCode/note may be omitted, null or empty; no invented catalogue or mandatory reason rule.
Claimed amount, currency, shipment and carrier cannot be edited by transition. No aggregate amount ceiling across claims.
Settled records an operational resolution, including an approved zero; it does not assert payment occurred or create
ledger/AP/AR/payment/stock movement, settlement-reference input, or a finance integration event.

| Scenario | Exact proposed acceptance |
|---|---|
| M01 | Create claimed -1, 0, -0 → 422 `CLAIM_AMOUNT_INVALID`; smallest representable positive supplied decimal text remains positive and can succeed |
| M02 | Investigating, claimed "250": approved missing/null/-0.01/250.01 → 422 `CLAIM_APPROVAL_AMOUNT_INVALID`; 0, -0, 249.99, 250 valid |
| M03 | Any other valid arrow with non-null approvedAmount →422; null/omission does not clear prior approval; approval cannot be changed by settlement |
| M04 | Approve 0→Settled→Closed and approve250→Settled→Closed retain exact approved text; only frozen Claim events emitted, no finance/stock writes or outbound finance call |
| M05 | Separate keys can create multiple claims against one Shipment; no undocumented sum-of-claims limit; empty resolutionCode/note survives accepted transition audit |

## D187-03 — exact decimal storage, currency and fingerprinting

**Required owners:** data/persistence + contract + business/finance. Sources S1 Decimal/currency; S2 no-float constraint.
Impact: proposed exception to any implicit .NET decimal/BSON Decimal128 representation; arbitrary precision comparison required.

Proposal: persist original validated amount as **BSON string**, compare signed integer coefficient + decimal scale
without rounding or float/.NET decimal/Decimal128 conversion. No fixed p/s is introduced. A finite request/storage
resource failure must fail atomically (503 `CLAIM_STORAGE_UNAVAILABLE` if persistence cannot commit), never truncate
or round; this proposal is not a promise of unlimited transport/document capacity. DEV must measure existing infrastructure
limits before release; any new public precision/length cap is a contract amendment, not a validator default.
Use ASCII decimal digits with optional leading minus and optional nonempty fractional digits; reject exponent, plus,
whitespace, bare dot and JSON numeric tokens. This explicit interoperable lexical profile requires contract-owner ratification.
Leading zeros and negative zero remain intact; numeric comparison determines positivity, not lexical spelling.
Currency accepts exactly `[A-Z]{3}`, including ZZZ; no ISO membership/minor-unit rules, FX, rounding or JPY integer restriction.

Idempotency fingerprint uses original amount strings: "250" ≠ "250.00", "0" ≠ "-0".
JSON object order and string escape spelling do not change the decoded fingerprint. Nullable option omission equals null;
evidence omission equals []; array order and duplicates remain significant. Strings are never trimmed. occurredAt uses
its original valid date-time text in the fingerprint; equivalent instants with different text conflict. Preserve that text in
audit/event without silently losing fractional precision; server observation timestamp is separate.

| Scenario | Exact proposed acceptance |
|---|---|
| P01 | "1234567890123456789012345678901234567890.1234567890123456789012345678901234567890" round-trips exactly through create/list/restart/audit/outbox where applicable; no decimal overflow conversion |
| P02 | claimed "0.0000000000000000000000000000000000000001" passes positivity; "000250.00" compared exactly to approved "250" |
| P03 | "1e2", "+1", "1.", ".1", " 1", non-ASCII digits and JSON number 1 →400 `INVALID_REQUEST`; request-body nullability unchanged |
| P04 | ZZZ and JPY with fractional positive amount succeed; lowercase usd, four-letter USDD →400; currency immutable |
| P05 | Same key "250"→"250.00" gives409 replay conflict; reordered object/escaped equivalent strings replay; evidence reorder conflicts |
| P06 | Inject persistence capacity failure: 503, zero partial documents; no rounded amount or success receipt; actual transport limits reported separately, no fake unlimited-size test claim |

## D187-04 — lifecycle action to permission

**Required owners:** security/RBAC + logistics business. Sources S1 targetStatus; S2 proposed five keys.
Impact: no new role, workflow, financial amount tier or automatic privilege inheritance.
All keys below have prefix `supplychain.claims.`; they require exact existing grant semantics, not role-name guessing.

| Operation / target | Required key | Legal source |
|---|---|---|
| GET claims | read | N/A |
| POST create | create | N/A → Open |
| Investigating | investigate | Open |
| Withdrawn | investigate | Open |
| Approved / Rejected | decide | Investigating |
| Settled | settle | Approved |
| Closed | decide | Rejected or Settled |
| Open on transition | create for authorization, then always422 | None |

No actor separation is imposed in this bounded slice: the creator may investigate/approve/settle only if explicitly granted
each required action. This is an **explicit proposed business/security choice**, not an accepted default.
A settler lacking decide cannot close. No Platform bypass or security-owner approval inferred from Carrier.

Acceptance S01: every target × missing/exact/unrelated grant; denied→403 `FORBIDDEN`, zero documents.
S02: creator with all required grants can execute full legal chain; creator with create alone cannot approve or settle.
S03: all 49 source×target pairs: exactly seven legal pairs, other42→422 `INVALID_CLAIM_TRANSITION` when authorized and payload otherwise valid.
S04: two tenants × two legal entities; foreign ID is404 after authorized request, no state/receipt/event leakage.

## D187-05 — error, headers, root and durable replay

**Required owners:** contract + security + Shipment owner; CT owns shared-contract coordination.
Sources S1 headers/Error/envelope, S2 §23; Carrier/Loads annexes are comparison evidence only.
Impact: Claims operation response declarations must be amended; create requires an authoritative Shipment-root seam.

Proposed exact policy:
- Validate JWT by existing authentication. Anonymous/invalid token→401 `UNAUTHENTICATED`, `WWW-Authenticate: Bearer`.
  Exactly one signed tenant_id/legal_entity_id/sub each, non-nil UUID; missing/duplicate/invalid trusted identity→403.
  Optional X-Tenant-Id/X-Legal-Entity-Id must match signed scope; malformed→400, mismatch→403.
- Correlation header required exactly once, ASCII hyphenated UUID; letter case accepted and nil syntactically allowed.
  Every POST needs exactly one Idempotency-Key string length1–128 Unicode scalar values; no trim; repeated headers reject400.
  Single scalar comma is data, not automatic multi-header splitting. Query scope keys tenantid/legalentityid/tenant_id/
  legal_entity_id/x-tenant-id/x-legal-entity-id (once URL-decoded, ASCII case insensitive) reject400. Unknown ordinary query keys
  do not acquire a new prohibition. Body additional properties already prohibited. No scope from body/query.
- Order: authentication → trusted identity and operation permission (for transition, any one of create/investigate/decide/settle
  before body read) → headers/query scope → media type/body schema → exact target permission → receipt lookup →
  scoped reference/aggregate lookup → root check → business/lifecycle → atomic persistence.
  For found receipt: saved-root check precedes fingerprint check; no reference dependency lookup or lifecycle re-evaluation.
  Unauthorized identity/action has precedence over reference existence. Rejection never reserves a key.
- Create uses original Shipment lifecycle root as its root and must receive matching X-Correlation-Id. Existing ShipmentDetail
  **does not expose it** in this baseline. External Root R2 dependency proposes: `lifecycleCorrelationId` UUID in ShipmentDetail, with exact semantics and
  producer/mock support. Claims requires a populated authoritative value; missing→503 `CLAIM_REFERENCE_INCOMPLETE`.
  No fabricated root, reuse of response trace, or direct Shipment DB read. Transitions/replays must match persisted root;
  wrong root→409 `CLAIM_CORRELATION_MISMATCH` (including nil when different). GET correlation is request trace only.
- Receipt identity = tenant + LE + operation(create/transition) + target (`create` or claimId) + key; actor/root not part of key.
  Same tuple/root/fingerprint returns original201(create)/200(transition), original result plus idempotentReplay=true;
  current authorized different actor allowed, original audit actor unchanged. Different fingerprint→409 `IDEMPOTENCY_KEY_REUSED`.
  Original result replay survives later state changes and same-scope soft deletion; no rehydration from current entity.
  No receipt TTL or key reuse; retention change requires explicit review.
- Response correlation header on all application-owned responses; error.error.correlationId equals that response header.
  Use valid incoming trace; invalid/missing header gets generated trace **only for rejection**, not success/root creation.
  Success body shape remains frozen. Fixed safe error messages; no raw exception, token, evidence or foreign identifiers in details.

| Operation | Proposed declared application statuses |
|---|---|
| queryClaims | 200,400,401,403,500,503 |
| createClaim | 201,400,401,403,404,409,415,422,500,502,503 |
| transitionClaim | 200,400,401,403,404,409,415,422,500,503 |

500 `INTERNAL_ERROR`; 503 persistence `CLAIM_STORAGE_UNAVAILABLE`; 415 `UNSUPPORTED_MEDIA_TYPE`;
400 `INVALID_REQUEST`; 404 `CLAIM_NOT_FOUND`; other exact codes in D187-01/02/04 and this section.
403 `FORBIDDEN` includes scope/identity/action failures. Shared Error shape unchanged; Claims-specific examples must
not use Returns/Loads/Carrier business codes. Public message text proposed below must be frozen in the owner-approved amendment before DEV; no exception text is an acceptable substitute.

| Code | Exact proposed error.message |
|---|---|
| INVALID_REQUEST | Request schema or context is invalid. |
| UNAUTHENTICATED | Authentication is required. |
| FORBIDDEN | The requested operation is not permitted. |
| CLAIM_NOT_FOUND | Claim or referenced resource was not found. |
| UNSUPPORTED_MEDIA_TYPE | Content type is not supported. |
| CLAIM_SHIPMENT_INELIGIBLE | Shipment is not eligible for a claim. |
| CLAIM_CARRIER_MISMATCH | Carrier does not match the shipment. |
| CLAIM_AMOUNT_INVALID | Claimed amount must be greater than zero. |
| CLAIM_APPROVAL_AMOUNT_INVALID | Approved amount must be between zero and the claimed amount. |
| CLAIM_APPROVED_AMOUNT_NOT_ALLOWED | Approved amount is not allowed for this action. |
| INVALID_CLAIM_TRANSITION | Claim transition is not allowed. |
| CLAIM_CORRELATION_MISMATCH | Correlation does not match the lifecycle root. |
| IDEMPOTENCY_KEY_REUSED | Idempotency key was used for a different request. |
| CLAIM_REFERENCE_INVALID | Reference response is invalid. |
| CLAIM_REFERENCE_INCOMPLETE | Required reference data is unavailable. |
| CLAIM_REFERENCE_UNAVAILABLE | Reference service is unavailable. |
| CLAIM_STORAGE_UNAVAILABLE | Claim storage is unavailable. |
| INTERNAL_ERROR | An internal error occurred. |

Acceptance H01: missing/invalid/duplicate headers and trusted claims, permission+badbody conflicts follow order above.
H02: nil valid correlation that differs from root gives409, never a new root; valid GET nil trace echoed.
H03: fresh create with unavailable root gives503/no writes; matching root gives same value in every Claim envelope.
H04: matching receipt replays while Shipment is offline; changed actor+current grant does not alter stored audit;
wrong root+changed payload returns correlation409; same root+changed payload returns replay409.
H05: process restart/lost response and 20 concurrent same-key calls yield one entity/receipt/audit/event, same original result.
H06: Claims family routing must not swallow `/claimsXYZ`; Shipment/Carrier/Loads auth/headers/errors/replay stay unchanged.

## D187-06 — concurrency, durability, event and architecture exceptions

**Required owners:** data/persistence + architecture + logistics business + CT; security reviews receipt retention.
Sources S1 events/arrows; S2 L3 proposal; S4 atomic evidence. Impact: explicit repository and entity-versioning exceptions.

Proposal: claimId UUID; claimNumber `CLM-` + lowercase UUID N representation (32 hex digits), unique in tenant+LE,
never reused even after soft delete. No gapless/date-based sequence and no one-claim-per-Shipment unique constraint.
Mongo replica-set transaction owns exactly claims, claims_receipts, claims_audit, claims_outbox. A successful mutation writes
aggregate + durable response receipt + immutable before/after audit + outbox event atomically. No assignment/entitlement
constraint collection. Unique indexes on scoped number, scoped receipt tuple, scoped eventId; lists/aggregate access enforce
scope and IsDeleted=false. Historical receipt access is separately scoped and does not expose foreign deleted state.
Internal Version CAS; conflict retries re-read state and apply the same lifecycle/business policy. No new If-Match/header/version DTO.
Unknown commit: resolve receipt, otherwise return503 and permit same-key recovery; never report guessed success.

Events only ClaimOpened/Investigating/Approved/Rejected/Settled/Closed/Withdrawn as named in S1 enum (full `Claim` prefix).
Create event occurredAt = server UTC; transition event occurredAt = original validated request date-time (offset allowed,
no invented past/future ordering cutoff). Audit server receivedAt/committedAt separate. Stable persisted eventId/root,
aggregateType=Claim, aggregateId=claimId, contractVersion=v1, causationId=null. Replay emits nothing new.
Local outbox remains pending; no fake transport or worker is authorized by this package; test-only state inspection is allowed. No live Event Bus subscription,
remote delivery acceptance or finance posting. ClaimSettled is not a financial posting instruction.

Architecture exceptions proposed: module-specific atomic repository instead of generic CRUD; immutable transition audit +
internal CAS instead of mutable draft/revision entity-versioning model. Neither exception changes EntityBase or shared rules.

Acceptance C01: inject failure after each of four writes and before commit→zero partial documents; unsupported transaction/
missing indexes fails closed. C02: simultaneous Approved and Rejected from Investigating with distinct keys→one success,
loser422 invalid transition after reread; one lifecycle event and one success receipt. Same target distinct keys likewise one success.
C03: after commit/response loss, restart+same key resolves original result without second event; old successful receipt remains
original even after entity reaches Closed. C04: scope/index collision and deletion tests prove non-reuse/no foreign receipt read.
C05: no finance/stock/source writes; no shared outbox replacement; exact decimals/audit/root persist across fresh process restart.


## R01–R30 precedence
The separate reconciliation-r01-r30.md is the historical comparison, not an alternate policy. Final D187 approval and final-reconciliation.md supersede the old A/B choices. r01-r30-crosswalk.json binds this reconstructed package; root producer patch is external.
