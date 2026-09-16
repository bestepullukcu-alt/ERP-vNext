# MOD-0183 readiness reconciliation and development prompt

Date: 2026-09-15. Owner: MVP-6 Control Tower. WP `MVP6-MOD0183-DEV-01`, prompt `MVP6-MOD0183-P01` v1.
Status: planning complete for review; runtime acceptance **NOT READY**. This prompt is recorded, not dispatched.
The user approved the MOD-0183 reconciled scope and Phase 1.5 checklist in this session, then requested independent
acceptance before advancing to MOD-0184. No code, build, persistence or runtime acceptance is inferred from approval.

## 1. Authority and inspected baseline

- Module: [MOD-0183 pack](../../../execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md), `ready-for-dev`, canonical name `Shipment Tracking & POD`.
- [Domain config](../../../execution/domains/supply-chain-execution/domain-config.md), [AGENTS.md](../../../AGENTS.md), [orchestrator](../../../.antigravity/agents/orchestrator.md), [add-module](../../../.antigravity/workflows/add-module.md), [CT SOP](../../guides/operations/control-tower-sop.md).
- Identity: Blueprint 8.1 `Blueprint_Data` and registry. DCP-002 command returned `OK`, exit 0. Existing MOD parent; no new ID/FU/reservation.
- [DCP-009](../../../execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md) OD-4 explicitly selects MOD-0183 first and authorizes the approved-pack scaffold trigger. Its `runtime_code_allowed: false` / “no member ready” summary is stale inventory, not a new override of the approved pack. Central CT owns that record reconciliation; this lane does not edit it or claim it was updated.
- [SHIPMENT-BUNDLE](../../analysis/contracts/shipment-bundle.openapi.yaml), [WAREHOUSE-OUTBOUND](../../analysis/contracts/warehouse-outbound.openapi.yaml), [SUPPLIER](../../analysis/contracts/supplier.openapi.yaml): all v1/FROZEN; files remain byte-for-byte unchanged.
- Repository/worktree: `/Users/natig/Projects/ERP-vNext-recovery`; branch `feature/mvp6-logistics`; HEAD `bc109afa4c016877dc4ecf203f8a8e91b512e4dd`.
- Baseline: untracked `shipment-bundle.openapi.yaml`, `sandop-capacity.openapi.yaml`, `supplier-performance.openapi.yaml` under `docs/analysis/contracts/`; nine packs under the SCE `module-packs/` directory; `docs/records/audits/2026-09/mvp6-phase-a-spec-freeze-report-2026-09-15.md`; `docs/roadmap/plans/mvp6-logistics-development-plan.md`. These are expected task inputs, preserved. Staging empty. Only one listed worktree. Re-run preflight at dispatch; unknown changes stop the affected writer.

## 2. Exact scope and dependency reconciliation

Approved core: five-layer `Diten.SupplyChainService` on 5061; `GET/POST /api/shipment-bundle/shipments`,
`GET /shipments/{shipmentId}`, `POST /shipments/{shipmentId}/transition`, `POST /shipments/{shipmentId}/pod` under that base.
Shipment creation, read model, lifecycle, POD, tenant/LE isolation, RBAC, replay, immutable history and durable audit/outbox.
No separate lifecycle/reconcile/assign-carrier/assign-load route exists in v1; do not invent one. Reconciliation is internal.
No UI, carrier/load/return/claim runtime, inventory write or Supplier lookup is needed by this core.

| Dependency | Actual state | Effect |
|---|---|---|
| SHIPMENT-BUNDLE v1 | Available; local references and explicit examples validate | Core authority; preserve wire envelope exception |
| INVENTORY-BUNDLE, Product, Location | Published frozen mock sources | Read-only reference validation; never foreign DB/internal entities |
| WAREHOUSE-OUTBOUND v1 | Present; read-only list/detail + ready event | Missing-contract note resolved; automatic intake has the precise gates below |
| Event Bus | Guid correlation/event IDs; nullable tenant; no LegalEntityId in common metadata | Reuse transport/outbox; do not mistake transport identity for full business scope |
| SUPPLIER v1 | Present; MOD-0140 identity/lookup/validate | N/A for 0183; 0147/0148 still need actor binding and ownership review |
| Gateway / shared permissions / catalog | Separate integration-owner work | Backend acceptance distinct from integrated/module completion |
| Carrier / Load | Optional future references | No invented master, assignment command or premature dependent module |

Historical Phase-A reports are immutable evidence. Stale prose and error examples inside frozen SHIPMENT-BUNDLE
are recorded here as annotation debt, not rewritten. The current central contract name is WAREHOUSE-OUTBOUND;
`WAREHOUSE-SHIPMENT-TRIGGER` is the historical dependency label, not an additional missing API.

## 3. Warehouse-to-Shipment map (no wire changes)

Read `GET /api/warehouse/outbound-shipments?status=ReadyToShip` with cursor, then read authoritative
`GET /api/warehouse/outbound-shipments/{outboundId}`. A notification is a hint to fetch; it does not contain lines/address.
Every fetch uses an authenticated, validated tenant/LE context. Only `ReadyToShip` is eligible for new intake.

| Warehouse fact | Shipment target / treatment | Default or constraint |
|---|---|---|
| `outboundId` | `sourceDocumentId`; internal source-link identity | Never use `orderRef` as the dedup key |
| MOD-0178 producer | `sourceModule=MOD-0178`, `sourceType=WAREHOUSE_OUTBOUND` | ASSUMPTION A1: local adapter discriminator, not a new central enum |
| `warehouseId` | `warehouseReferenceId` | Location reference, not outbound identity; compare event and fetched warehouse IDs |
| `orderRef` | Internal immutable source snapshot | Optional; not a substitute shipment/source ID |
| structured `shipTo` | `shipToReference=warehouse-outbound:{escaped outboundId}:ship-to` | ASSUMPTION A2: opaque reference to the scoped immutable shipment source snapshot; not a Customer/Address master ID or new endpoint |
| `lines[].orderLineId` | Retained in snapshot; `lineNumber` assigned as below | Optional and not guaranteed unique; never assume stable unique producer line ID |
| source line array | Local `lineNumber` strings `1..N` in first accepted snapshot order | ASSUMPTION A3: deterministic for that immutable snapshot; persist index→original line map; subsequent reorder/change is drift, not remapping |
| `itemId`, `skuId`, `quantity`, `uomId` | Same-named ShipmentLine fields | Preserve IDs and decimal strings without float conversion; validate frozen schema |
| `skuLevel`, `lotNumber`, `serialIds` | Internal provenance snapshot only | ShipmentLine forbids extra properties; no traceability master/stock truth |
| `packages` | Internal package reference snapshot | Not injected into create DTO, no package lifecycle ownership |
| `readyAt` | `plannedShipAt` when available | ASSUMPTION A4: readiness time is initial planning default, not actual dispatch time |
| missing detail `readyAt` | Event `readyAt` if trusted and consistent; otherwise first successful fetch time UTC | ASSUMPTION A4: persist selected value and provenance once; never recompute on retry |
| absent planned delivery | `plannedDeliverAt=null` | Optional in frozen command; no guessed service-level date |
| absent reservation/transaction ref | `inventoryReferenceId=null` | Do not use outbound/order/lot identity as an Inventory reference |
| `status=ReadyToShip` | New Shipment remains `Draft` | Warehouse readiness is not dispatch; use frozen transitions subsequently |
| `Shipped` / `Cancelled` / other source status | Do not create; reconcile existing source link | No automatic backward transition or Warehouse write |
| `contractVersion` | Snapshot version + mapping version | v1 required by selected adapter profile; missing version is recorded as absent, not producer-attested |

Source snapshots are confidential transaction evidence, tenant/LE scoped and append-only. They are not mutable copies
of Warehouse, Supplier or Inventory masters. All added fields stay inside owned persistence, never frozen request/event payloads.

## 4. Correlation, causation and business context

Observed transport: `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/EventMetadata.cs` has
`Guid EventId`, `Guid CorrelationId`, `Guid? CausationId`, `Guid? TenantId`; no LE field.
`EventTransportMessage.cs` rejects empty event/correlation UUIDs. `TrustedTransportMetadata.cs` allows signature headers
only; it is not an arbitrary LE carrier. `ICanonicalIntegrationEvent` permits exact canonical payload bytes.

1. HTTP core validates non-empty UUID `X-Correlation-Id` and Idempotency-Key. Tenant/LE and actor derive from validated
   authentication/context, with supplied scope headers checked against authorized claims. Bodies cannot supply scope.
2. Preserve the first accepted command's UUID as shipment root correlation and on lifecycle/POD events. Caller flow must
   propagate that root UUID on later lifecycle commands, satisfying both frozen header-to-event and immutable-chain descriptions.
   **ASSUMPTION A5:** the approved happy path uses one root correlation. A different later command UUID exposes conflicting
   frozen descriptions; do not silently overwrite the root or invent a new response contract. Report GAP-0183-04 for that case.
3. Warehouse event with non-empty UUID payload correlation and matching trusted envelope correlation: preserve the UUID.
   If payload correlation is absent, a valid trusted envelope UUID may supply it (A5, provenance recorded).
   If a supplied string is non-UUID, empty, or conflicts with the envelope, preserve original bytes in restricted evidence and
   quarantine the event; do not hash it into a UUID, replace it, or claim lossless propagation. GAP-0183-01 applies.
4. Poll-only core intake can establish a new locally generated root UUID once and persist it in the source link (ASSUMPTION A6).
   This is a new local chain, not proof of upstream correlation. If an event later supplies a different upstream UUID, retain both
   in reconciliation evidence; never overwrite the accepted root. Full event/poll correlation equivalence stays gated.
5. Event causation uses trusted incoming EventId; local command causation uses a persisted command UUID. Retries retain original
   command/event IDs. A polling run has a recorded local trigger; no invented upstream event ID.
6. Emitted transport metadata carries tenant and the same UUID IDs/correlation as canonical Shipment payload. Common transport
   does not prove LE scope. Incoming event intake needs an existing trusted LE resolution mechanism; never infer LE from warehouse ID,
   orderRef, user-default LE, unsigned headers or an arbitrary payload extension. GAP-0183-02 blocks automatic event intake.

## 5. Event/poll deduplication and source reconciliation

**ASSUMPTION A7:** one shipment per `(TenantId, LegalEntityId, sourceModule=MOD-0178, sourceType=WAREHOUSE_OUTBOUND,
sourceDocumentId=outboundId)` for this slice; split shipments require a later approved contract. Unique source-link index
is durable, including cancelled/soft-deleted shipments; deletion does not reopen the source for duplicate creation.

- Both paths execute the same intake coordinator and unique source-link claim. Transport EventId dedup alone is insufficient.
- Idempotency uniqueness is `(TenantId, LegalEntityId, operation, IdempotencyKey)`; intake uses a stable key (SHA-256 of unambiguous length-prefixed source tuple, below 128 chars).
  Preserve source tuple separately; correlation is not part of uniqueness. Same HTTP key/different normalized payload is 409
  `IDEMPOTENCY_KEY_REUSED`; same key/same payload returns the stored result without a second history/outbox append.
- Store original source snapshot/hash, mapping version, selected defaults, source root/local root, command ID, created ShipmentId,
  processing state and receipt references. Hash the canonical source values, excluding transient transport metadata.
- Concurrent first deliveries race on the unique source link; one winner creates the shipment. Losers load the committed result;
  never mark a pending claim successful. A crashed pending claim resumes using its persisted defaults/UUID and lease/version guard.
- **ASSUMPTION A8:** use a Mongo transaction for source claim + shipment + lifecycle + POD + replay + audit/outbox writes;
  startup verifies transaction support. Do not silently fall back to unrelated sequential inserts on standalone Mongo.
- Publishing is at-least-once: persist immutable event IDs/canonical bytes once, retry outbox delivery using the same IDs;
  consumers dedup. “One durable event record” does not mean “one broker delivery.”
- A repeated identical snapshot is a no-op even with a different EventId or polling run. Changed fields under the same source identity
  create a source-drift record and stop intake; do not create a second shipment or overwrite delivered/POD facts.
- Poll cursors are opaque, scoped per tenant/LE/filter and advanced only after every item is durably recorded as processed or blocked.
  On restart resume safely. Since v1 has no changed-since/version query, rescan from the beginning periodically with durable dedup;
  also fetch known source details to see Shipped/Cancelled changes excluded by ReadyToShip polling. No undocumented cursor ordering guarantee.
- Detail 404, timeout, 503, missing lines, conflicting warehouse, or untrusted scope means blocked/retry evidence, not success or deletion.
  Distinguish an absent source from a dependency outage. Preserve cancellation/tombstone source links.
- Reconcile outbound ID/warehouse/order/address/line quantity/UoM/lot/serial/package references with the stored snapshot; compare Inventory
  only through frozen reads when a real reference exists. Inventory failure cannot mutate stock or silently modify Shipment.
- SUPPLIER is irrelevant to 0183 reconciliation. Later 0147/0148 validate known/status through MOD-0140; Supplier identity is not actor binding.

Required future scenarios: event→poll and poll→event; duplicate EventId; distinct EventIds/same source; simultaneous deliveries;
crash before/after transaction commit; lost HTTP reply; outbox retry; reordered/changed lines; null readyAt; same outboundId in two scopes;
unknown/cross-scope source; cancelled source; unavailable dependency; late conflicting correlation. None is claimed executed here.

## 6. Central CT GAP register

These are repository handoff records, not a claim that a message was sent to another CT task.

| ID | Exact incompatibility / missing authority | Affected scope / required owner action |
|---|---|---|
| GAP-0183-01 | WAREHOUSE `OutboundShipmentReadyEvent.correlationId` optional unconstrained string vs Shipment header/event and Event Bus non-empty UUID | Automatic event intake/full-chain proof blocked for legal non-UUID inputs and late poll/event root mismatch. Central CT must define a compatible correlation bridge or approve versioned producer/consumer change. Requiring UUID in existing central v1 would narrow valid inputs and is breaking; do not edit it locally. |
| GAP-0183-02 | Warehouse event has no trusted LE binding; common EventMetadata has TenantId but no LegalEntityId | Central integration/security owner supplies an authenticated scope resolution contract. Never guess LE. Core HTTP scoped commands unaffected. |
| GAP-0183-03 | OAS 3.1 Warehouse list example has `nextCursor:null` but schema `type:string, nullable:true`; strict JSON Schema rejects it | Central owner repairs/version-controls schema compatibility. Current Prism example smoke is not strict validation or consumer acceptance. Supplier has the same cursor defect plus `results[1].status:null` against non-null SupplierStatus. Supplier defect is N/A for 0183. |
| GAP-0183-04 | Shipment header says each operation's UUID maps exactly to emitted correlation; lifecycle schema says first command UUID cannot change | Core same-root path is defined. Different-root later mutation needs contract-owner error/propagation decision before that path can be accepted. No silent root replacement. |
| CT-0183-05 | DCP summary and registry remain historical/planned despite approved local pack; frozen Shipment annotation says missing trigger | Central CT record reconciliation only; OD-4 and pack explicitly authorize the core. Do not rewrite DCP, registry or frozen bundle here. |
| INT-0183-06 | Gateway routes, shared permissions/registration and live dependencies not delivered | Integration owner WP then E5. No full module/G5 closure from local mocks. |

## 7. MOD-0183 DoR — all SOP §8.1 items

PASS below means a documented development input, never a runtime test result. Overall all-path MOD-0183 DoR is
**NOT READY** while contract ambiguity remains. The bounded same-root HTTP core has documented, approved scope;
automatic Warehouse intake and incompatible-correlation behavior are explicitly held, not waived or quietly dropped.

| DoR item | Result / evidence |
|---|---|
| Canonical identity | PASS: DCP-002 exit 0; existing Blueprint/registry MOD-0183 |
| Owner / objects | PASS: pack §§2–4; Shipment/POD/history/replay/audit/outbox only |
| Approved pack | PASS: ready-for-dev + explicit user approval in this session |
| Cross-module capability boundary | PASS: DCP-009 OD-4 and domain; central record drift tracked |
| Contracts present and unambiguous | PARTIAL: core same-root surface mapped; GAP-01/02/03/04 prevent unrestricted PASS |
| Dependencies satisfied/waived | PARTIAL: mock sources present; automatic intake gates open, no waiver claimed |
| Allowed/protected paths | PASS: pack §§5–6 and prompt below |
| Branch/worktree/base | PASS: measured §1; fresh check required at dispatch |
| Dirty inventory | PASS: §1; expected untracked inputs preserved |
| Prompt profile | PASS: Profile B, backend/contract |
| Contract flow | PASS: create Draft→Planned→Dispatched→POD Delivered→Closed, reload and audit; failure cases below |
| Measurable AC | PASS: pack §16 plus §§3–5; implementation boxes remain open |
| Persistence | PASS plan: L3 durable audit/evidence; atomic transaction prerequisite A8 |
| Security/audit/evidence | PASS plan: JWT + six pack permission keys, tenant/LE fail-closed, E4; no fabricated scope |
| Parallel safety | PASS assessment: serialized single DEV writer; no concurrent writer approved; core composition shared |
| Risk | PASS: HIGH, tenant/LE security + regulated POD + retry/consistency |
| Conditional gates | PASS plan: §8; unmeasured runtime prerequisite stays explicit |
| Target/entry fields | PASS: orchestrator + add-module, complete §10 fields |
| Paste-ready prompt | PASS artifact: §10 with measured context; held, not dispatched |

Pack §18 checks: identity, domain/DCP read, frozen owned contract, backend-only choice, service/port/base/paths,
commands/events, security/consistency gates, failure paths, gateway ownership and owner approval all checked by inspection.
Warehouse absence is corrected to present-with-compatibility-gates. Its new readiness checkbox remains open; none of
pack §16's runtime acceptance checkboxes is checked by this reconciliation.

## 8. Conditional gates and defaults

- Security/privacy: recipient/address/note/evidence references confidential/PII; tokens and full payloads never logged.
  Audit actor, scope, operation, entity, old/new state, command/event IDs and safe failure code; restrict snapshot access.
- Observability: correlation trace, operation latency, intake/replay/drift/denial counts, outbox pending/retry/dead-letter counts;
  event names exactly the frozen Shipment/POD event names. Do not label a retry as a second business event.
- Persistence/migration: new owned collections/indexes only. No legacy backfill/shared seed. A8 fail-fast transaction support.
  Rollback disables ingress/publisher without deleting regulated history; retention follows shared evidence policy, no invented TTL.
  DB-010 tests use one fixed test DB and unique tenant IDs, not a new GUID-named DB per test.
- Concurrency: atomic current-state predicate and stored replay identity (pack exception to public version/ETag);
  inherited technical Version is internal, not a new wire requirement.
- NFR: frozen list page=1/pageSize=50/max=200; bounded cursor iteration. ASSUMPTION A9: existing 500ms performance
  warning threshold is an initial observation threshold, not a measured p95 SLA. Measure and report p95, no invented acceptance SLA.
- Deprecation: none; frozen contracts unchanged. Any required wire change goes to owner/version review.
- UI/accessibility/export/financial valuation: N/A, backend-only, no export or money. Decimal-string quantities retained losslessly.
  POD uses immutable evidence references; no local binary store or invented retention rule.

## 9. Mimari Onay (Phase 1.5) — completed plan

All answers describe planned architecture, not existing code. User approval received in this session.
Root `S=services/Diten.SupplyChainService/src`; projects `Diten.SupplyChainService.{Api,Application,Domain,Persistence,Infrastructure}`.

| # | Check | Plan: answer |
|---|---|---|
| 1 | All pack fields | Plan: Yes. `Domain/Features/Shipments/Shipment.cs`: inherited Id/TenantId/IsDeleted/DeletedAt/CreatedAt/UpdatedAt/Version; LegalEntityId, ShipmentNumber, SourceModule/SourceType/SourceDocumentId, SourceSystem/ExternalRef, WarehouseReferenceId, CarrierId, LoadPlanId (wire loadId), ShipToReference, Lines, PlannedShipAt/PlannedDeliverAt/ActualDeliverAt, Status, CreatedBy/UpdatedBy and root correlation. `ShipmentLine.cs`: LineNumber/ItemId/SkuId/Quantity/UomId/InventoryReferenceId. `ProofOfDelivery.cs`: Id/ShipmentId/ReceivedAt/RecipientName/EvidenceReferenceIds/Note/CorrelationId plus scope/base/audit. `ShipmentLifecycleEntry.cs`: sequence, from/to, occurredAt, reason/note, actor, correlation/causation/event IDs. `ShipmentException.cs`: transition reason/note/time evidence. Source snapshot/link, replay and outbox retain §§3–5 evidence; never serialize internal fields into frozen DTOs. |
| 2 | Global naming | Plan: Yes. PascalCase internal / frozen camelCase wire. `LoadPlanId→loadId`, `Id→shipmentId` in explicit projection; preserve ShipmentNumber and canonical contract names. No wire rename. |
| 3 | Isolation / soft-delete | Plan: Yes. `Persistence/Features/Shipments/ShipmentRepository.cs` and scoped source/replay/outbox stores; all business access filters TenantId+LegalEntityId+IsDeleted=false. Unique source identity survives soft deletion; append-only history cannot be edited/deleted by a public API. Tenant-first indexes from pack plus source key and operation replay key. Cross-scope IDs return 404. |
| 4 | Base | Plan: Yes. `Domain/Common/EntityBase.cs`, inherited by Shipment/POD/lifecycle entities, mandatory tenant-aware base. No GlobalEntity or foreign stock entity. Driver/serialization mappings confined to Persistence. |
| 5 | Separate CQRS | Plan: Yes. Application `Features/Shipments/Commands/{CreateShipmentCommand,TransitionShipmentCommand,CapturePodCommand}.cs`; `Queries/{GetShipmentListQuery,GetShipmentByIdQuery}.cs`; matching `{CreateShipment,TransitionShipment,CapturePod}Handler.cs` under `Handlers/CommandHandlers/` and `{GetShipmentList,GetShipmentById}Handler.cs` under `Handlers/QueryHandlers/`; each validator separate under `Validators/`; `ShipmentModels.cs` DTO contract. ShipmentRepository uses pack-approved specific repository for transactional append/replay. Four Application behaviors in prescribed order. Infrastructure clients through Application interfaces; controller derives CustomBaseController, handlers return Response<T>, wire adapter applies pack exception. |
| 6 | DataTable choice | Plan: N/A. shell none, golden_reference none, form_field_count 0. No Slim/Compact/UI invention. |
| 7 | Compact form/detail parity | Plan: N/A. No Razor/Create/Edit/Details or offcanvas. |
| 8 | Required / optional parity | Plan: Yes for backend. Create requires SourceModule/SourceType/SourceDocumentId/WarehouseReferenceId/ShipToReference/PlannedShipAt/Lines; lines require LineNumber/ItemId/SkuId/Quantity/UomId. Transition TargetStatus/OccurredAt; POD RecipientName/ReceivedAt/EvidenceReferenceIds. UUID correlation all operations; key mutations. Nullable PlannedDeliverAt/InventoryReferenceId/ReasonCode/Note and response CarrierId/LoadId/ActualDeliverAt/POD per schema. No ViewModel/Razor/tracker; progress N/A. Use frozen required/null rules plus pack business checks, no invented public fields. |
| 9 | Platform lookup | Plan: N/A. No dropdown/filter/default UI or PSS lookup extension; Product/Location/Inventory are read-only external references. Supplier not consumed. |

## 10. SOP §17 work package and paste-ready development prompt

| Metadata | Value |
|---|---|
| WP / prompt / version | MVP6-MOD0183-DEV-01 / MVP6-MOD0183-P01 / 1; undispatched |
| Capability / module / sequence | DCP-009 MVP-6 / MOD-0183 / 1, before MOD-0184 |
| Build lane / agent lane / type | MVP6 logistics / MVP6-MOD0183-DEV-01 / DEV |
| Entry / risk / profile | @orchestrator + /add-module / HIGH / B |
| Branch / expected HEAD / worktree | feature/mvp6-logistics / bc109afa4c016877dc4ecf203f8a8e91b512e4dd / /Users/natig/Projects/ERP-vNext-recovery |
| Dirty baseline | §1 plus this reconciliation's documentation files; inventory freshly before dispatch |
| Depends on | Approved pack, frozen mock sources, same-root flow; automatic intake held by §6 |
| Parallel-safe with | None approved; single writer to initial service composition/persistence |
| Integration order | core → independent E4 → integration-owner routes/permissions → live Warehouse/dependents E5/G5 |
| Authority | §1 identity/DCP/domain/pack; this reconciliation and dated audit |
| Allowed paths | Initial service root and MOD-0183 features/tests only; owned pack; module API narrative/audit |
| Protected | All frozen contracts and README; DCP-009; .antigravity; foreign service/features; gateway/shared permissions/catalog; frontend/UI |

```text
@[.antigravity/agents/orchestrator.md] + /add-module
WP: MVP6-MOD0183-DEV-01 · Prompt MVP6-MOD0183-P01 v1
Status: recorded development prompt; re-run DoR/preflight before dispatch.

Repository: /Users/natig/Projects/ERP-vNext-recovery
Branch: feature/mvp6-logistics
Expected HEAD: bc109afa4c016877dc4ecf203f8a8e91b512e4dd
Worktree: /Users/natig/Projects/ERP-vNext-recovery
Dirty baseline: section 1 and this reconciliation's documentation changes; preserve all.

Read in order:
execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md
execution/domains/supply-chain-execution/domain-config.md
AGENTS.md
.antigravity/agents/orchestrator.md
.antigravity/workflows/add-module.md
.antigravity/rules/{multi-tenancy,security-jwt,git-safety,code-style,docs-organization,
handler-design,repository-standard,response-envelope,pipeline-behaviors,entity-base-template,
entity-versioning,mongo-indexing,api-conventions,routes,ports,erp-architecture,configuration-safety,
permission-key-standard,business-module-enforcement-standard,module-self-registration-standard}.md
execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md
docs/analysis/contracts/{shipment-bundle,warehouse-outbound,supplier,inventory-bundle}.openapi.yaml
docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md
docs/guides/operations/control-tower-sop.md

Pack status: ready-for-dev. Domain: supply-chain-execution.
Service: Diten.SupplyChainService; port 5061.
Shell: none; golden_reference: none (backend-only N/A); form_field_count: 0.
Profile B; risk HIGH; persistence L3; consistency atomic.
Expected specialist chain in one serialized DEV lane:
business-analyst checks pack/AC → orchestrator confirms Phase 1.5 approval →
data-agent + backend-architect with disjoint files → security-agent → testing-agent →
code-quality-agent/documentation-writer → independent read-only verifier.
Integration-agent receives a separate WP for shared routes/permissions; no UI/l10n lane.

NE: Implement the approved same-root HTTP Shipment/POD core and initial five-layer service.
Only queryShipments/createShipment/getShipment/transitionShipment/captureProofOfDelivery
and corresponding Shipment/POD event production. Required fields and files are section 9.
Do not implement incompatible automatic Warehouse intake while section 6 gates remain open.

NEDEN: MOD-0183 is the owner-approved first MVP-6 lane under DCP-009 OD-4.
Persist one Shipment per successful create; preserve lifecycle, evidence and replay safety.

NASIL: Follow sections 3–9 and pack. Application CQRS returns Response<T>; Api derives
CustomBaseController and emits exact unwrapped frozen v1 JSON. Use five-layer architecture,
four ordered pipeline behaviors, JWT plus HasPermission, trusted tenant/LE context.
Permissions: supplychain.shipments.read/create/dispatch/pod.capture/cancel/reconcile.
Only exposed operations need their corresponding permission; cancellation through transition
must require cancel permission, not merely dispatch. Reconcile remains internal, no new route.
Mongo driver only in Persistence. Fail fast for absent configuration or transaction support.
Atomic shipment/history/POD/replay/audit/outbox; same key/different payload → frozen 409.
Bound source-aware replay across event and poll as section 5; never claim broker exactly-once.
Mock dependencies use frozen specs. Clients use interfaces; no foreign DB or stock balance.
Backend-only: no UI pattern, tracker, menu, fabricated catalog page or frontend route.
Conditional gates: section 8, including privacy, redaction, pagination and DB-010 cleanup.

YAPMA: Do not edit frozen contracts, contracts/README.md, DCP-009, .antigravity,
other modules/services, gateway or shared permission/registration files. Do not invent
carrier/load assignment endpoints, public reconciliation/history routes, Supplier master,
new event envelope fields, legal-entity context, non-UUID correlation conversion or placeholders.
No commit, push, stash, branch switch or unsolicited shared configuration changes.
Allowed: services/Diten.SupplyChainService initial composition + Features/Shipments and
corresponding tests; module API narrative under docs/reference/architecture/api;
owned module pack and dated evidence under docs/records/audits.

DOĞRULA: DCP-002 identity gate; frozen examples/ref/schema parity; build all five layers;
service tests plus dotnet test tests/architecture/TenantArchitecture.ArchitectureTests.
Golden flow with one correlation UUID: create Draft → reload → Planned → Dispatched →
POD Delivered → reload POD → Closed. Verify every frozen allowed/forbidden transition.
Run denial/missing scope/cross-tenant/cross-LE, soft-delete invisibility, invalid UUID,
invalid quantities/schema, duplicate POD, replay/payload conflict and concurrent mutation tests.
Inject failure before/after commit and outbox publish; restart and prove durable state,
one replay result/history transition and immutable event IDs. Compare emitted canonical payloads
against the frozen schema. Verify Inventory failure has no inventory/partial Shipment write.
Warehouse incompatible inputs must remain blocked; no fabricated trigger can create a shipment.
Test DB-010: fixed test database, unique tenant per test. No operational database cleanup.
Required acceptance E4: API results + persisted scope/state + actor/audit/outbox + failures/restart;
mocks provide contract confidence only. Gateway/live integrations and full G5 require E5 later.

Stop affected work on missing/ambiguous contract, ownership or protected-path conflict,
branch/HEAD mismatch, unexpected dirty edits, unplanned migration or unsupported transaction
runtime. Report exact GAP and remaining safe scope; do not silently broaden or waive a gate.
Output SOP §22 report: verdict; branch/HEAD; changed files; decisions; commands/results;
golden flow and failure evidence; persistence/RBAC/tenant/audit/outbox/restart proof;
remaining gaps; integration status; exact accepted scope and next gate.
Developer PASS is not CT acceptance. Independent E4 verification must precede MOD-0184 readiness.
```

## 11. Replan / next work

**Current gate: NOT READY for runtime acceptance or MOD-0184 advancement.** Service directory and implementation
acceptance evidence are absent. The concrete review artifact is this MOD-0183 map, completed architecture plan and
held development prompt. User approval is recorded, not requested again. Central CT resolves §6 while the approved
core remains the first implementation task. After actual independent E4 acceptance, inspect/reconcile MOD-0184;
its draft status cannot be promoted by this planning record. Then follow 0184 → {0185,0186,0187} → {0190,0192} → {0147,0148}.
No next-module development prompt is issued as ready while that prerequisite fails.
