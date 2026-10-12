# Returns semantics — SHIPMENT-BUNDLE 3.0.0 / wire v1
## Authority and activation
The user's latest approval authorizes separate D186/D187 consumer amendment candidates only.
No canonical publication, consumer release consent, guard activation, runtime/Program.cs, Phase1.5,
pack promotion or DEV GO. Existing historical proposals remain unchanged.
Shipment root design is already approved. Consume authoritative persisted lifecycleCorrelationId via
published getShipment; preserve root, never derive from IDs, read trace or shared database.
Real successful fresh create requires published AND implemented AND verified producer seam.
Root schema is composed in this bundle; shipment-root-semantics-v3.0.0.md is its normative annex. Publication does not establish producer runtime uptake. Missing/null503; malformed502; mismatch409.
No new root candidate, worker, publisher or source writes. Pending local outbox only.

## Common validation boundaries (module-specific differences follow)
JWT authentication parser rejection401; successful authentication with unusable/duplicate trusted
identity403. Do not assert every duplicate sub reaches403: parser rejection may be401.
Exact signed tenant_id/legal_entity_id/sub, non-nil UUID; no alias or last-wins duplicate bypass.
UUID ASCII8-4-4-4-12, hex case allowed; no trim/braces/URN/32hex. Nil correlation/path/body
syntactically valid, but never use default nil as proof of authoritative provenance.
Header names case-insensitive; duplicates including identical values rejected400. Correlation required.
POST Idempotency-Key exact decoded string1..128 Unicode scalars; no trim, scalar comma is data.
Once-decoded ASCII-case-insensitive scope-query keys tenantId,tenant_id,X-Tenant-Id,
legalEntityId,legal_entity_id,X-Legal-Entity-Id reject400; ordinary unknown keys do not bind scope.
Body schemas unchanged, additional properties forbidden where frozen requires it.
Auth -> trusted context/base grant -> correlation/scope/key headers -> scope match -> path/query/media/body
-> exact target grant -> committed receipt root then fingerprint -> fresh reference/target/root ->
business/lifecycle -> atomic persistence. Local Returns duplicate/positive checks precede source lookup.
Valid incoming correlation is response trace; rejection-only generated trace if unavailable/invalid.
Error.error.correlationId equals response X-Correlation-Id; fallback never creates business root.
Shared Error and success shapes retained; no Response<T>/ProblemDetails wrapper.401 Bearer challenge.
Receipt=(tenant,LE,operation,target-or-create,key); actor/root not identity. Current grants required.
Same root/fingerprint replays original201/200 with idempotentReplay=true, no new writes or source reread.
Different root precedes changed payload409. Old result survives state changes/deletion within authorized scope.
No receipt TTL or failed receipt. Unknown commit: recover receipt, otherwise503; do not promise rollback.
Fresh failure before commit has no successful receipt/event. No independent roots for legacy records.
## D186-01/02 — eligibility and entitlement
Delivered/Closed only; cap=ShipmentLine.quantity, not separately proven delivered quantity.
Positive arbitrary-precision quantity, exact ordinal line/UoM; duplicate command lines422; no conversion.
Source required omissions/type/enum/mismatched identity/duplicate lines/nonpositive source quantity502;
optional shipmentId/status missing503; selected line absent404. First entitlement snapshot freezes numeric
quantity/UoM/itemId/skuId; later drift409 RETURN_SOURCE_CHANGED. Equivalent numeric representation not drift.
Entitlement key tenant+LE+shipmentId+lineNumber (not UoM). Count Requested/Authorized/InTransit/Received/
Dispositioned/Closed; only Rejected/Cancelled release once. Closed/soft-delete never release.
All lines plus aggregate/receipt/audit/outbox commit atomically; cap10,6+6 race -> one201,one422;
4+6 -> both can commit. Snapshot HTTP read not a distributed lock; transition/replay do not reread.
Frozen arrows ONLY Requested->Authorized|Rejected; Authorized->InTransit|Cancelled;
InTransit->Received; Received->Dispositioned; Dispositioned->Closed.
CORRECTION: prior chat summary mistakenly added InTransit->Cancelled; no-new-arrow scope wins;
that arrow remains422 INVALID_RETURN_TRANSITION. No lifecycle policy expansion authorized.

## D186-03/04 — bounded receiving
Received is same-scope authorized manual assertion, not verified physical Warehouse receipt.
Audit actor, command time, server commit time, manual-assertion evidence type. Opaque evidence strings.
Inventory and Warehouse HTTP calls ZERO, including GET. inventoryTransactionReferenceId omitted/null/empty
retained as opaque unverified text; no stock movement/reservation/verification claim.
Dispositioned requires dispositionCode string length>=1, no trim/catalog; whitespace accepted;
on other targets supplied value audited but does not change business disposition.

## D186-05 — Returns-specific profile
Read=create mapping supplychain.returns.read / .create. Transition requires .transition AND target:
Authorized/Rejected .authorize; InTransit .transit; Cancelled .cancel; Received .receive;
Dispositioned .disposition; Closed .close. Requested requires base, then invalid lifecycle422.
Scope validation headers X-Tenant-Id/X-Legal-Entity-Id required; mismatch404 RETURN_NOT_FOUND.
Auth401/context403/schema400/media415 use INVALID_REQUEST. Foreign/missing target404 RETURN_NOT_FOUND.
Quantity fingerprint numeric exact (1.0=1.00); UUID by value; date-time by instant, no lost fractional
precision. Arrays order significant; strings ordinal. Nullable omission=null; evidence omission=[];
empty distinct; no trim. Raw first accepted values retained in audit/result.
Root codes RETURN_SHIPMENT_ROOT_UNAVAILABLE503 / RETURN_SHIPMENT_ROOT_INVALID502 /
CORRELATION_ROOT_MISMATCH409. Same-root changed valid payload409 IDEMPOTENCY_KEY_REUSED.

## D186-06 — persistence and events
Replica-set L3: returns,return_entitlements,returns_receipts,returns_audit,returns_outbox.
Scoped unique number/receipt/entitlement/event indexes; internal version/CAS; no public concurrency field.
RMA- + uppercase UUID-N. Known uncommitted collision at most3 new UUID attempts, then503;
unknown commit never retries blindly with new ID. Raw decimal string+exact coefficient/scale, no rounding.
One immutable audit/event per successful mutation. Fault after each write before commit rolls back all.
ReturnRequested serverUTC/causation null; transition normalized instant/previous Return eventId causation.
Persist inherited Shipment root, not local create root. Event IDs stable; Pending outbox, no worker/publisher.
