# Phase1.5 activation review

Nine design rows below checked against current source and published exact YAML/annex. PASS for bounded architecture/design; NOT runtime tests. Owner explicitly approved exact delta plus attached isolated scope and conditioned promotion on this review. Repository/versioning/envelope exceptions remain module-specific; no shared changes. Current Application DI uses assembly scan/four behaviors; EntityBase reused read-only.

# Phase1.5 delta — proposal, approval remains HELD

Old PREP02 design48paths →47exact proposed runtime/test paths: remove ClaimOutboxWorker.cs only. No new broad wildcard or shared runtime file. Existing helper/amount/reference snapshot/test paths retained. 47paths are a future allowance proposal, not this INS write grant; unused files need not be fabricated.

|Phase1.5 check|Exact proposal|Approval boundary|
|---|---|---|
|1 Entity fields|Claim inherits existing Domain/Common/EntityBase read-only; owns LegalEntityId, actors, original decimal text, immutable references/evidence/snapshot, nullable approved amount, internal Version/CAS|No base serializer/entity edits; final schema parity|
|2 Names|Exact final CreateClaimCommand/TransitionClaimCommand/list/status/event names and wirev1; CLM-+lowercase UUID-N number|No new settlement reference/stock/Supplier/return fields|
|3 Repository|Module-specific IClaimRepository/ClaimRepository transaction across claims,claims_receipts,claims_audit,claims_outbox; tenant+LE every access; IsDeleted=false normal aggregates/list; separately scoped immutable receipts|REPO-001 exception needs explicit architecture approval; real unique indexes/transaction/CAS tests|
|4 Entity versioning|Immutable before/after transition audit + internal CAS; no generic Draft/Revision statuses, public version field or If-Match|Explicit entity-versioning exception, no shared implementation change|
|5 CQRS/envelope|Existing assembly scanning/four behaviors read-only; separated command/query handlers and validators; Claims-owned raw wire/error adapter, no Response<T> extra wrapper|Response-envelope exception applies only to final contract operations|
|6 Golden|Backend Slim command/handler/validator naming inspected; no CRUD cloning|shell:none,form0,golden_reference:none; no UI scope|
|7 UI sections|No Razor/DataTable/modal/RESX/browser assets|N/A, not an unimplemented promised screen|
|8 Parity|D187 design no longer undecided; final YAML/annex exact grant/header/error/root/replay matrix|Publication+pack/Phase1.5+explicit runtime grant still required|
|9 Lookup|No PSS lookup or ISO catalogue; Shipment/Carrier reference-only, evidence opaque|No other master/DB/HTTP client|


Disposition: rows1–6,8–9 design PASS; row7 UI N/A. Future runtime evidence belongs to R01–R30. Shared composition approval remains separate; it does not block disjoint layer implementation.
