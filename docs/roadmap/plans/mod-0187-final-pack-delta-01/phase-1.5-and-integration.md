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

## Separate integration-owner proposal — NOT Claims-owned

Only shared target: services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs.
Proposed change after separate CT lease: Claims namespace imports; AddClaimPersistence registration; Claims scoped context and HttpClient reference reader; exact Claims-family middleware; family-local invalid-model response adapter; exclude exact Claims family from generic Shipment middleware. Match exact /api/shipment-bundle/claims or slash descendants; /claimsXYZ must not match. Preserve Carrier/Loads branches, Shipment/SourceIntake routes, all JWT/CORS/health/global behavior and existing Shipment worker. NO Claims worker/publisher registration, NO shared IEventOutboxStore replacement, NO second MongoClient.

Application/DependencyInjection.cs already scans handlers/validators and registers four behaviors: read-only reuse. No csproj/solution/shared DI/config/permission registry edit inferred. Missing dependency/config capability becomes an exact integration GAP with source evidence, not a silent file expansion.

Claims DEV cannot edit Program.cs, even in a shared checkout. Integration owner supplies its separately approved exact composition patch to a designated isolated verification checkout after Claims writer-complete. Any runtime smoke needing that composition remains HELD until its lease/diff is approved. DEV can implement owned layers within granted isolated scope; full HTTP acceptance follows composition. No parallel writer leases on Program.cs.

## Remaining start conditions only

1. Single publication owner publishes/records exact3.0.0 proposal (or explicitly dispositions a changed hash); final consumer/publication/guard decisions remain separately missing per final VER. This lane does not execute or waive guard.
2. CT/user approves this narrow pack delta, exact47path allowance and Phase1.5 exceptions, promotes pack through a separate action and explicitly releases a new runtime prompt. D187 business decisions are not re-requested.
3. Assign actual isolated DEV execution worktree/branch/fresh baseline and a separate integration-owner composition lease. Current INS snapshot is not a runtime worktree grant; old HELD prompt remains historical.
4. For real consumer integration/rollout, obtain authoritative Shipment root emission/uptake evidence; bounded mock-first isolated DEV must label this unproven. No dependency on MOD0186/0185 acceptance invented.
5. Independent VER begins after DEV writer-complete; CT acceptance is separate. No new PREP loop unless exact contract/scope drift changes these reviewed inputs.
