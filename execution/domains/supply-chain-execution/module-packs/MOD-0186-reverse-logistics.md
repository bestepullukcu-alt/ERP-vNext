---
id: MOD-0186
name: Reverse Logistics
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "Owner-promoted isolated pack 1c80cca7… (PHASE15-CLOSE-01, 2026-09-21) bound to CT-accepted bounded work package MVP6-MOD0186-WP-ACCEPTANCE-01, accepted on published SHIPMENT-BUNDLE 3.0.0 and re-pinned to 3.1.0 / wire v1 (§31, Q380). Common-checkout integration, gateway, live producer uptake, UI, E5/G5 and rollout remain open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 6
---

# MOD-0186 — Reverse Logistics

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0186 --name "Reverse Logistics"`
> returned `OK` again on 2026-09-17 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** this is a `draft` backend/contract spec; runtime implementation is not authorized.

## 1. Module Summary

MOD-0186 owns Return/RMA identity and lifecycle for items returned from an existing MOD-0183 shipment. It references
frozen INVENTORY transactions but never stores or adjusts a shadow stock balance.

## 2. Ownership and Boundaries

**Owns:** Return/RMA number, reason, shipment-line quantities, evidence references, disposition and reverse lifecycle.

**Consumes:** MOD-0183 shipment/POD; frozen INVENTORY movement references; existing frozen **WAREHOUSE-OUTBOUND** as upstream context only (see §21; no direct receiving/ingress authority).

**Does not own:** shipment source, warehouse receiving trigger, inventory movement/balance, Supplier, carrier or claim.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `ReturnOrder` / `RMA` | Reverse transaction identity and status |
| `ReturnLine` | Original shipment-line reference and return quantity |
| Commands | Create return and transition lifecycle |
| Queries | List returns by shipment/status; no by-ID GET |
| Events | Requested/authorized/rejected/in-transit/received/dispositioned/closed/cancelled |

## 4. Entity Fields

The aggregate has server UUID, server-resolved TenantId and request-borne, MDM-validated LegalEntityId, unique `RmaNumber`, required `ShipmentId`,
reason, non-empty return lines, evidence references, status/version and audit fields. Received/dispositioned states may
carry an opaque `InventoryTransactionReferenceId`; it is a reconciliation reference, never a local balance.

## 5. Repo Scope

The exact prospective file allowlist is §25. No broad service/features wildcard, generated documentation
or shared composition edit is authorized. This PREP lane edits this pack only for this module.
Frozen contracts remain read-only. Future DEV requires explicit pack and shared-file release.

## 6. Protected Paths

- `.antigravity/**`, gateway, shared registrations, archive/frontend shells.
- MOD-0183 Shipment/POD runtime paths and all other SupplyChain features.
- INVENTORY persistence/contracts and every other domain service.
- Central Warehouse and Supplier contracts; never define their schema locally.
- `docs/analysis/contracts/shipment-bundle.openapi.yaml`, `docs/analysis/contracts/returns-semantics-v3.0.0.md` and `docs/analysis/contracts/shipment-root-semantics-v3.0.0.md` (published, frozen; read-only for this module).

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| MOD-0183 Shipment | Required before runtime | Return references an existing eligible shipment/line |
| INVENTORY v1 | Frozen consumed | Read/reference and movement reconciliation only |
| `SHIPMENT-BUNDLE` v1 | Frozen | Reverse API/lifecycle/mock authority |
| Warehouse outbound | Frozen read-only upstream seam exists | No inbound return, receiving or automatic ingress implied; see §21 |

## 8. Runtime Constraints

Prospective mutations are tenant/LE scoped, idempotent and correlation-preserving. Receipt does not itself mint or
modify INVENTORY movement truth. Invalid/terminal lifecycle transitions produce no event or partial write. Draft
status forbids runtime implementation.

## 9. Layout & Shell Contract

`shell: none`; backend/contract only.

## 10. Backend File Convention

Prospective root is `Features/Returns/` with standard Commands, Queries, separated handler folders, Validators and
`ReturnModels.cs`; outbox and idempotency persistence stay tenant-first.

## 11. Frontend File Contract

Not applicable; no UI/form shape is invented.

## 12. Validation Rules

Historical draft business proposals below; exact frozen schema and unresolved approvals are distinguished in §§22–24.

- Shipment and shipment line must resolve through MOD-0183 and remain in the same tenant/LE.
- Return quantity is positive and cannot exceed shipped quantity net of existing active returns.
- Lifecycle follows Requested→Authorized|Rejected; Authorized→InTransit|Cancelled; InTransit→Received;
  Received→Dispositioned; Dispositioned→Closed.
- INVENTORY reference is opaque and required only when the frozen lifecycle/schema requires it.

## 13. Failure Path to Verify

Unknown shipment/line, excess quantity, duplicate active return, invalid transition and invalid inventory reference
return deterministic errors with unchanged state; cross-tenant/cross-LE resources return 404.

## 14. Authorization Convention

Prospective permissions: `supplychain.returns.read`, `.create`, `.authorize`, `.receive`, `.disposition`.

## 15. Gateway / API Routing Decision

Use frozen `SHIPMENT-BUNDLE` `/returns**`; route publication is a separate integration-agent task.

## 16. Acceptance Criteria

- [ ] Runtime remains absent while `draft`.
- [ ] Future code consumes MOD-0183/INVENTORY strictly by frozen contract/mock.
- [ ] Return quantity invariants and lifecycle are atomic and deterministic.
- [ ] No stock collection, projection or movement SoR exists in MOD-0186.
- [ ] Correlation ID remains unchanged through reverse lifecycle events.

## 17. Test Expectations

DCP-002, OpenAPI/mock, shipment dependency, quantity invariant, lifecycle, idempotency, tenant isolation, RBAC,
outbox and explicit no-shadow-balance architecture tests.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] MOD-0183 and INVENTORY consumption boundaries are explicit.
- [x] Frozen contract defines reverse lifecycle and mock examples; receiving semantics are not thereby complete.
- [ ] MOD-0183 dependency has executable verified evidence.
- [ ] Owner approval changed status from `draft`.

## 19. Implementation Notes

**ASSUMPTION:** v1 supports customer return against shipment lines; supplier-return and financial credit settlement
are outside scope. Warehouse receipt and INVENTORY posting remain external consumed seams.

## 20. Follow-up Items

- Verify MOD-0183 before runtime dispatch.
- WAREHOUSE-OUTBOUND exists; inbound-return receiving remains an uncovered seam, not a synonym for outbound. See §24.
- Separate integration evidence must reconcile received return with frozen INVENTORY movement.


## 21. NEXT-PREP-01 v1.0 — historical baseline (current authority: §28)

This refinement supersedes imprecise scope/readiness/default claims in §§1–20. Earlier business rules
are draft proposals, not proof of frozen wire semantics. Status remains draft. No runtime file is writable
in this PREP lane; this paragraph records the earlier PREP-01 scope. Current PREP-02 authority and outputs are §28.

Measured 2026-09-17: branch `feature/mvp6-logistics`, HEAD
`4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Existing dirty publication, Carrier runtime/Program.cs,
0184 pack and records are protected inputs. No commit/push/stash. Current plan:
`docs/roadmap/plans/mvp6-development-plan-v4.0.md` §7; WP-MVP6 is intake, not runtime authority.
DCP-009 and inventory capability report §§15/21/22 establish ownership and contract-first/mock-first
execution; historical G3/en-son text does not block this spec preparation or authorize runtime.

Frozen authority `docs/analysis/contracts/shipment-bundle.openapi.yaml`: **info.version 1.1.0**,
**wire contractVersion v1**, base `/api/shipment-bundle`. Publication SHA-256
`ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f`.
Carrier annex SHA-256 `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee`.
That annex applies to Carrier, not Loads/Returns/Claims: no inherited permission to copy Carrier
error precedence, response headers, cross-correlation replay or 409 transition semantics.

`docs/analysis/contracts/warehouse-outbound.openapi.yaml` is FROZEN 1.0.0, wire v1, MOD-0178 owned.
Real operations: GET `/api/warehouse/outbound-shipments` (`listOutboundShipments`, status/warehouseId/cursor)
and GET `/api/warehouse/outbound-shipments/{outboundId}` (`getOutboundShipment`).
OutboundShipmentReadyEvent schema exists; it is not a POST ingress operation or a return receipt.
It exposes pick/pack-ready outbound lines, packages and shipTo; no inbound-return receiving, disposition,
stock posting or command acknowledgement. The bundle's historical WAREHOUSE-SHIPMENT-TRIGGER marker
is not proof of absent outbound contracts. Its correlation string/UUID, trusted LE and OAS3.1 nullable
cursor issues remain separate MOD-0183 integration GAPs; do not modify or silently work around them.
This module consumes Shipment's published seam, not that upstream event or Warehouse DB.

MOD-0183 bounded E4 is centrally accepted (2026-09-16 CT report). MOD-0184 has independent VER02/03
and published uptake, but bounded CT acceptance is separate (`mod-0184-ct-continuation-2026-09-17/README.md`).
Do not re-request already granted Carrier publication approvals or claim Carrier CT acceptance.
DocsPathGuard disposition is not a prerequisite for drafting these packs. Historical architecture14 PASS/4 FAIL
is not a fresh run, waiver or green gate. Future runtime sequencing follows plan v4.0 and explicit CT disposition.

## 22. Exact frozen operation and field parity

Only the following three operations belong to this slice. No GET-by-ID, edit/delete/bulk, new lookup,
paging/sorting/search, assignment command or integration endpoint may be invented.
Operation and schema names below are literal keys under `paths` / `components.schemas` in frozen YAML.

| Operation ID | Method / relative path after base | Declared statuses |
|---|---|---|
| `queryReturns` | GET `/returns` | 200 |
| `createReturn` | POST `/returns` | 201, 404, 409, 422 |
| `transitionReturn` | POST `/returns/{returnId}/transition` | 200, 404, 422 |


Exact GET query parameters (all optional; not nullable unless specified):

- `shipmentId`: `{"type":"string","format":"uuid"}`.
- `status`: `{"$ref":"#/components/schemas/ReturnStatus"}`.

All command/nested input objects listed below have additionalProperties:false. Required absence and null
are distinct. No extra TenantId/LegalEntityId/version/audit/actor field is accepted.

| Schema.field | Required | Exact schema (not an invented validator limit) |
|---|---|---|
| `CreateReturnCommand.shipmentId` | yes | `{"type":"string","format":"uuid"}` |
| `CreateReturnCommand.reasonCode` | yes | `{"type":"string"}` |
| `CreateReturnCommand.lines` | yes | `{"type":"array","minItems":1,"items":{"$ref":"#/components/schemas/ReturnLine"}}` |
| `CreateReturnCommand.evidenceReferenceIds` | no | `{"type":"array","items":{"type":"string"}}` |
| `TransitionReturnCommand.targetStatus` | yes | `{"$ref":"#/components/schemas/ReturnStatus"}` |
| `TransitionReturnCommand.occurredAt` | yes | `{"type":"string","format":"date-time"}` |
| `TransitionReturnCommand.inventoryTransactionReferenceId` | no | `{"type":["string","null"],"description":"Frozen INVENTORY movement reference; stok bakiyesi degildir."}` |
| `TransitionReturnCommand.dispositionCode` | no | `{"type":["string","null"]}` |
| `ReturnLine.shipmentLineNumber` | yes | `{"type":"string"}` |
| `ReturnLine.quantity` | yes | `{"$ref":"#/components/schemas/Decimal"}` |
| `ReturnLine.uomId` | yes | `{"type":"string"}` |

`ReturnStatus`, `TransportMode` and `Decimal` use frozen enums/patterns. Decimal is a **string** matching
`^-?\d+(\.\d+)?$`; it admits signed/zero values and gives no precision/scale cap. Positive quantity/amount
is a draft business condition (§24), not a JSON-schema fact. No binary float, rounding or invented length cap.
Optional string|null fields permit absence/null/empty unless an explicitly approved business decision says otherwise.
Optional arrays allow omission but **not null** and have no minItems/uniqueItems unless specified above.
Required string without minLength permits empty text; no blanket NotEmpty/trim rule.
Date-time accepts valid offsets, not only literal Z; UTC storage must preserve the instant. Past/future/time-order
restrictions are not in schema. UUID nil rejection and array duplicate rules cannot be copied from other modules.

Responses: `ReturnListResponse` requires items,total,contractVersion. `ReturnSummary` has **no required list**;
listed non-null property types do not imply their presence is schema-required. The implementation proposal
emits all summary properties for useful reloads, without rewriting the contract. `ReturnResponse` must follow
its exact schema, including allOf where used. No outer data envelope; internal Response<T> is adapted at API.

Exact response schema: `{"allOf":[{"$ref":"#/components/schemas/ReturnSummary"},{"type":"object","required":["idempotentReplay","contractVersion"],"properties":{"idempotentReplay":{"type":"boolean"},"contractVersion":{"$ref":"#/components/schemas/ContractVersion"}}}]}`.

Frozen transition description (authority, no extra arrows):

> Izinli gecisler: Requested->Authorized|Rejected; Authorized->InTransit|Cancelled; InTransit->Received; Received->Dispositioned; Dispositioned->Closed. Diger gecisler INVALID_RETURN_TRANSITION ile reddedilir. INVENTORY posting bu API'nin disindadir ve frozen INVENTORY contract'ina korele referansla yapilir.

Create success starts **Requested**. Same-state/new-key and all non-listed arrows are invalid; terminal
states have no outgoing arrow. Current-state check must be atomic; no request Version/If-Match exists.
Exact replay precedes new transition evaluation only after current auth/scope/schema checks, subject to
owner resolution of the replay policy below. List filtering uses only frozen parameters, not an invented detail query.

## 23. Errors, security, headers and replay — covered vs unresolved

Policy proposal: `[Authorize]` plus server-side `[HasPermission]`; actor tenant_user in validated tenant/LE context.
Existing draft keys are `supplychain.returns.read`, `supplychain.returns.create`, `supplychain.returns.authorize`, `supplychain.returns.receive`, `supplychain.returns.disposition`. Read/create mapping is clear as a proposal; all transition target mappings and missing actions require D186-05.
No permission inferred from a valid token alone; test scoped actors independently from platform bypass.

| Case | Frozen fact / bounded requirement | Exact unresolved decision / acceptance oracle |
|---|---|---|
| Headers | GET and both POST require UUID X-Correlation-Id; POST key is string length1..128 | Nil UUID, duplicate header, parser/whitespace behavior, validation/auth ordering and fallback trace must be owner-bound for this family; no Carrier-default import |
| JWT + scope | Bearer; server-resolved tenant; LegalEntityId request-borne and MDM-validated fail-closed (R-2, SHIPMENT-BUNDLE 3.2.0); global JWT/header matching; no payload-selected scope | Security owner binds trusted claim names, missing/duplicate claim handling, scope-header validation and precedence. Test two tenants×two LEs and missing permission; all lookups fail closed |
| Missing/cross-scope target | POST create/transition declare404 NotFound; list declares200 only | Target code `RETURN_NOT_FOUND` exists in shared examples; reference-specific404 mapping/disclosure must be chosen; list no-leak filtering required |
| Schema / auth / persistence failure | Shared Error requires code/message/correlationId plus contractVersion:v1, details optional | GET lacks declared errors; POST lacks400/401/403/5xx (and415). Owner supplies exact status/code/header matrix and decides clarification vs versioned amendment before DEV |
| Duplicate / changed payload | Create declares409 Conflict; shared IDEMPOTENCY_KEY_REUSED example | Transition **does not declare409**; cannot substitute422 to conceal it or silently add409. Business duplicate code/error is separately unresolved |
| Lifecycle | Transition422 + INVALID_RETURN_TRANSITION frozen | Verify all source×target pairs and zero partial writes/events; create422 examples from other modules are not local business scenarios |
| Correlation | LifecycleEventEnvelope requires UUID equal to first command root, preserved in chain | Different-root subsequent mutation/replay response body/header/root semantics need owner decision; never hash/replace root or copy Carrier cross-root policy |
| Same key, same valid payload/root | idempotentReplay field exists | Proposal: durable original result, no new mutation/audit/event; create201/transition200 proposed but no dedicated replay status description. Owner confirms |
| Same key/payload, different correlation | Not specified for this family | Decide same-root restriction vs accepted retry trace policy consistent with immutable event root; exact failure status/code if denied required |
| Same key, different payload | Shared conflict example | Scope tuple, actor policy, canonicalization (decimal representations, order, null/omission), retention and transition response mapping must be approved |
| Concurrent same-key / lost response | Durable L3 and idempotency required | One committed entity/receipt/audit/event. After commit loss retry returns original receipt; before commit rollback leaves none. Uncertain commit never reported as definitely rolled back |

No new error code or undocumented status is declared resolved by this draft. Owner decision must attach
operation/schema pointer and a failing/passing scenario, then update or publish affected authority if necessary.
Error.code being open string is not permission to invent business policy. Required output schema stays plain Error;
Carrier no-details rule is not universal. Error correlation for invalid/missing header is currently undefined here.

Proposed internal key identity: (TenantId,LegalEntityId,operationId,targetId-or-create,key), durable success receipt,
no TTL until approved retention; auth validated before receipt access. These are **review proposals**, not defaults.
Replay tests must cover same key/same payload/same root; different root; changed payload; different tenant/LE/target;
parallel requests; postcommit response loss; original receipt after later lifecycle and restart. Invalid input
must not consume a success key; error retention/caching policy needs explicit owner confirmation.

## 24. Module-specific boundaries and exact owner decisions
**Reviewable bounded DEV scope:** create Requested ReturnOrder against Shipment lines, list and frozen
transitions, module-owned return entitlement bookkeeping, durable receipt/audit/outbox. Receipt/Disposition
are operational states only; no stock movement, reservation, balance, quarantine ledger or financial credit.
Consumed Shipment detail provides lines.quantity/uomId and POD/status; not all delivered/net quantities are
separately exposed. ShipmentLine.lineNumber is the reference key; do not invent GUID shipment-line identity.

`INVENTORY-BUNDLE` FROZEN `/api/inventory` has GET `/transactions` (getTransactions, filters itemId/lotId/
locationId/from/to/page/pageSize), GET `/availability`, GET `/balance`; POST movements/reservations/release
exist but are forbidden for this pack. There is **no GET transaction-by-ID**. Do not fake `/transactions/{id}`.
No direct Inventory call is required merely to retain an opaque optional inventoryTransactionReferenceId.
If validation is required, owner must define reference-to-TransactionItem mapping and authenticated scope,
pagination/completeness/time-window; existing list filters do not establish an exact reconciliation lookup.
Availability/balance cannot prove receipt or valid movement. Mock data must remain frozen-schema compatible.

| Decision ID / accountable owner | Exact decision still required before runtime | Acceptance once bound |
|---|---|---|
| D186-01 business + contract | Eligible Shipment states; shipped/delivered quantity denominator; duplicate line treatment; UoM equality vs conversion; positive quantity business rule beyond Decimal | Missing/foreign line, zero/negative, duplicate line, UoM mismatch, boundary exact quantity and excess; no silent rounding/conversion |
| D186-02 business + data | Which Return statuses count toward net-return entitlement? Treatment of Requested/Rejected/Cancelled/Closed, soft delete and rejected retry; does closed consumed quantity ever release? | For source quantity10, race6+6 permits at most one success; race4+6 permits total10 if approved. Reject/cancel/release races never double-release; replay never debits twice |
| D186-03 Warehouse + contract | How is Received proven without inbound-return contract? Decide authorized manual assertion for bounded scope or require published receiving evidence. Outbound ready event is not receipt | Fake outbound ID cannot authorize received state; no automatic inbound listener. If external proof mandatory and absent, Received path remains blocked, not simulated as a business success |
| D186-04 Inventory + contract | When are inventoryTransactionReferenceId/dispositionCode required/nonempty? Both optional nullable in frozen command; define cross-reference validation using actual GET-only seam or explicitly opaque storage | Missing/null/empty inputs accepted by schema; any state-specific business rejection is owner-bound. Foreign/wrong movement/unavailable lookup cannot post stock or silently pass |
| D186-05 security + contract | Full §23 matrix and exact target-state permission map: authorize/reject, transit/cancel, receive, disposition, close; existing key list omits some transitions | Test each target with right/wrong grants and source scopes; no generic permission guessed for uncovered actions |
| D186-06 data/CT | L3 multi-document entitlement+aggregate+receipt+audit+outbox transaction; decimal representation/range; unique RMA number/generation; event root/time/causation | Inject failure at each write; zero partial state; restart exact quantities and one receipt/event; decimal precision never reduced |

Net-return invariant once D186-01/02 settled:
for each (tenant,LE,shipmentId,shipmentLineNumber,UoM), sum of counted returns including this command
must not exceed authoritative source quantity; all quantities positive under approved business rule.
Proposed entitlement record is a return-request cap, **not stock**: no on-hand/available/valuation or SKU/location
stock dimensions. It serializes competing ReturnOrder writes locally. Counter release requires atomic state
transition; a sum query followed by independent insert is unsafe. Authoritative source snapshot/version policy
must be selected: GET plus local transaction cannot prevent remote source quantity amendment. Record source
reference/snapshot evidence only, not a second live Shipment master. No new stock API or inventory collection.

## 25. Exact prospective DEV owned files and shared exception proposal

This is a design allowlist for **future approval**, not this PREP's write grant. Prefix S expands literally to
`services/Diten.SupplyChainService/src/Diten.SupplyChainService`.
No parent wildcard or another feature is writable. Only the following module paths are proposed:

`S.Domain/Features/Returns/` exact files:

- `ReturnOrder.cs`
- `ReturnStatus.cs`
- `ReturnScope.cs`
- `ReturnLifecycle.cs`
- `ReturnAuditEntry.cs`
- `ReturnMutationResult.cs`
- `IReturnRepository.cs`
- `ReturnLine.cs`
- `ReturnEntitlement.cs`

`S.Application/Features/Returns/` exact files:

- `Commands/CreateReturnCommand.cs`
- `Commands/TransitionReturnCommand.cs`
- `Queries/GetReturnListQuery.cs`
- `Handlers/CommandHandlers/CreateReturnHandler.cs`
- `Handlers/CommandHandlers/TransitionReturnHandler.cs`
- `Handlers/QueryHandlers/GetReturnListHandler.cs`
- `Validators/CreateReturnValidator.cs`
- `Validators/TransitionReturnValidator.cs`
- `Validators/GetReturnListValidator.cs`
- `ReturnModels.cs`
- `ReturnRequestContext.cs`
- `ReturnRequestFingerprint.cs`
- `IReturnReferenceReader.cs`
- `ReturnSourceSnapshot.cs`

`S.Api/Features/Returns/` exact files:

- `ReturnsController.cs`
- `ReturnContextMiddleware.cs`
- `ReturnContractError.cs`

`S.Persistence/Features/Returns/` exact files:

- `ReturnRepository.cs`
- `ReturnSchema.cs`
- `ReturnPersistenceRegistration.cs`
- `ReturnOutboxStore.cs`
- `IReturnCommitProbe.cs`
- `NoOpReturnCommitProbe.cs`

`S.Infrastructure/Features/Returns/` exact files:

- `ReturnReferenceReader.cs`
- `ReturnPermissions.cs`
- `ReturnPermissionAttribute.cs`
- `ReturnOutboxWorker.cs`

Tests prefix `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/`:
`ReturnContractTests.cs`, `ReturnLifecycleTests.cs`, `ReturnIsolationTests.cs`, `ReturnReplayTests.cs`,
`ReturnConcurrencyTests.cs`, `ReturnAtomicityTests.cs`, `ReturnReferenceTests.cs`.
Probes prefix `services/Diten.SupplyChainService/tests/returns/`:
`runtime_probe.py`, `restart_probe.py`, `verify_evidence.py`.
No other test or historical evidence edits. DEV/VER report destinations must be separately assigned by CT;
this allowlist grants no new docs record writes. New files beyond this list require versioned scope review.

Existing shared file requiring **separate explicit authorization and single writer** (not granted by PREP):
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`.
Proposal: register this module's scoped context/reference client, owned Persistence registration and local
outbox worker; exact family-only middleware/model-error adapter. Preserve existing Carrier branch and exclude
only approved module routes from broad Shipment middleware; never use naive prefix matching (e.g. returnsXYZ). Preserve both Carrier and Loads branches.
No health/JWT/CORS/Shipment worker/global error rewrite. No source edit made here.
Application assembly scans already find validators/handlers. Persistence extension reuses existing MongoClient
and database; no second client, cross-feature collection or service scaffold. Project references/configuration
changes are not assumed; if needed stop and extend owner allowlist explicitly. Existing base controller/entity
read-only reuse; family wire adapter cannot invoke Shipment-specific context/error policy accidentally.

Protect **every other repository path**, especially Program.cs until separately released, all existing Carrier/
Shipment/SourceIntake source/tests/evidence, all frozen contracts/annexes, .antigravity, gateway, registries,
DCPs, other packs/domains/services, shared permission registry, frontend/shells and shared outbox/DI files.
Generic CRUD cannot implement this atomic multi-document operation alone; module-specific repository exception
under REPO-001 is proposed with mandatory tenant+LE+soft-delete predicates, subject to Phase1.5 approval.
Gateway publication/shared permission seeding/live Event Bus transport are separate integration-owner WPs.

## 26. Transaction/event and verification contract (future acceptance)

Proposed durability L3: replica-set Mongo transaction, no standalone/in-memory fallback. Collections owned only
by this module: `returns`, `returns_receipts`, `returns_audit`, `returns_outbox`;
additional `return_entitlements` for approved net-return cap, never inventory stock.
scope tenant+LE on entity/receipt/outbox/audit reads and mutations; soft-deleted aggregates excluded from
normal reads, no delete API. Internal Version CAS/retry revalidates state; no new client version/header.
Unique scoped business number, receipt identity and eventId indexes; assignment/entitlement unique identity
where applicable. Number format/generation and retention are owner decisions, examples are not sequence specs.

Successful commit = aggregate + receipt + audit + lifecycle event outbox (+ constraint record where applicable).
Injected failure at each boundary rolls back all; committed response loss replays without another event.
Unknown commit resolves by durable receipt lookup, never blind insert. Success replay must retain original
result even after subsequent state changes; exact transport/correlation decisions still need §23 approval.

Envelope: LifecycleEventEnvelope with eventId, eventType, occurredAt, correlationId, aggregateType=`Return`,
aggregateId, payload=`ReturnEventPayload`, contractVersion=v1; optional nullable causationId.
Only frozen enum events: ReturnRequested, ReturnAuthorized, ReturnRejected, ReturnInTransit, ReturnReceived, ReturnDispositioned, ReturnClosed, ReturnCancelled. Initial event and each permitted target event map exactly to lifecycle; no duplicate event on replay.
Event root must preserve first command UUID. occurredAt on transitions comes from validated command; creation
server time/causation choice needs explicit policy. Local pending outbox can be restart-tested; no registration
as the shared IEventOutboxStore that would replace Shipment's store. Delivery semantics/transport/dedup at
remote consumer remain separate integration decisions. No live publish or E5 claim in bounded mock scope.

| AC | Future measurable acceptance / failure test |
|---|---|
| A01 wire | Exactly three listed routes, exact frozen request fields/nullability and responses; all inline examples validated. No by-ID/update/delete endpoints. Compare current published metadata2.0.0 vs wire v1; proposed future amendment separately versioned |
| A02 schema | Every required field missing/null/type error; optional null vs omission, additional properties; preserve allowed empty strings/arrays/decimal strings. No generic NotEmpty tightening |
| A03 identity/security | Real JWT tests: anonymous, missing operation/action grant, invalid/duplicate trusted claims, body/query scope injection; two tenants×two LEs. Cross-scope ID cannot leak or mutate; exact wire results follow signed §23 matrix |
| A04 reference | Published mock shape only; unknown/foreign/malformed/timeout/unavailable references fail closed without transaction success; observe outbound HTTP method/path/headers, no DB sharing or source writes |
| A05 state | Enumerate every source×target pair against frozen arrows; create initial state exact; no terminal reopen; same-state/new-key invalid; real concurrent transitions yield allowed serial history only |
| A06 replay | Same/split root, changed payload, scoped key isolation, concurrent duplicate, old result after new state, restart and lost response. At most one success receipt/audit/event; do not infer PASS from HTTP alone |
| A07 atomic | Fault after aggregate, receipt, audit, event and constraint write and before commit rolls back all. After commit/response loss returns original receipt. Mongo unavailable/index failure/unsupported transaction fails closed |
| A08 module business | Each §24 decision row gets boundary and concurrent negative/positive tests; no unresolved business decision silently skipped or converted into an invented default |
| A09 persistence | Restart fresh process; list reload, scoped DB counts, exact source references/decimal text, Version and audit/outbox/constraint contents retained. Replays unchanged; no stale binary evidence |
| A10 no SoR duplication | No Warehouse/Inventory mutation, stock/shadow-stock/finance/Supplier master collection; source service source and DB unchanged; HTTP reads only for approved dependencies |
| A11 regression | Existing Shipment and Carrier suites/runtime golden flow unchanged after separately approved composition; compare fresh architecture failures to historical14/4; no waiver or rewritten historical evidence |
| A12 evidence | Capture actual sent request bytes/status/body/headers (bodyless GET remains empty), exact source/binary/input hashes and process IDs, command exits and AC mapping; manifest complete and immutable |

Future commands: `dotnet build services/Diten.SupplyChainService/Diten.SupplyChainService.sln`;
`dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj`;
`dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` plus owned HTTP/restart probes.
DB-010 isolated test DB rules apply. Evidence target E2+E3+E4; no current runtime evidence generated by PREP.
Logs carry operation/result/correlation/replay, never token/connection string/raw evidence or confidential payload.
Rollback removes only this future module's approved composition; retain business/audit/receipt/outbox data,
no shared DB drop or unplanned destructive migration. Readiness fails if transaction/index guarantees unavailable.

## 27. Phase 1.5 proposal and DEV preconditions

| # | Mandatory architectural check | Plan (not implementation evidence or approval) |
|---|---|---|
| 1 | Entity fields | Preserve every §22 business field; server Id/TenantId/LegalEntityId/CreatedBy/UpdatedBy/UTC audit/soft-delete/internal Version; command-only occurredAt/reason/note history in audit; no DTO tenant |
| 2 | Global names | Exact frozen names; no local aliases, invented Supplier/stock/finance fields |
| 3 | Repository isolation | §25 repository scoped tenant+LE+IsDeleted=false; indexes and atomic unit §26; specific-repository exception needs approval |
| 4 | Entity base | Existing Domain/Common/EntityBase read-only, tenant-owned; LegalEntityId and actor fields module-owned; no GlobalEntity |
| 5 | CQRS | Exact Commands/Queries/Handlers/CommandHandlers/Handlers/QueryHandlers/Validators paths §25; four existing pipeline behaviors reused, response adapted without wire wrapper |
| 6 | Golden reference | N/A UI; shell:none/form_field_count:0/golden_reference:none; Slim backend naming reference only, no copied CRUD endpoints |
| 7 | Compact sections | N/A; no Razor/forms/details/pages/RESX/DataTable/browser verifier |
| 8 | Required parity | §22 schema matrix covers all inputs; validators must await §23/24 business/security decisions; **BLOCKED**, not assumed complete |
| 9 | Platform lookup | None. Frozen enum/reference consumption only; no PSS lookup or locally invented master |

Ready design: bounded three-operation backend slice, exact file ownership, transaction/event boundaries,
field/nullability matrix and acceptance A01–A12 ready for owner review. No UI/integration/optimization scope.
Open exact decisions: §23 common security/errors/replay plus every D-row §24. These are concrete runtime
blockers, not reasons to defer this completed specification. No undocumented default is selected.

DEV prerequisites, all required:
- [x] Existing canonical module ID/name verified DCP-002 exit0 on2026-09-17; no new/FU ID minted.
- [x] Frozen operation/schema parity measured; outdated outbound-absence statement reconciled.
- [x] Exact proposed ownership and protected-file/shared-composition separation recorded.
- [ ] §23/24 owner decisions signed with exact test outcomes; any required amendment published and consumed.
- [ ] CT confirms bounded dependency acceptance under v4.0 sequence (0184 independent PASS is not CT acceptance).
- [ ] Phase1.5 full approval, specific repository/index/reference-snapshot choices and Program.cs single writer released.
- [ ] User/CT explicitly promotes this draft and issues new SOP§17 versioned DEV prompt with fresh dirty snapshot hashes.
- [ ] Independent VER assigned after completed DEV; CT acceptance follows VER, not inferred from agent PASS.

ASSUMPTION: backend-only and contract-first/mock-first derive from v4.0 §7 and the existing draft, not from
runtime availability. No supplier/stock/live-ingress/financial posting implied. Historical DCP/board status
reconciliation belongs to central CT, not this pack lane. No new writer/subagent launched.


## 28. PREP-02 v1.0 — current authority and one owner decision set

2026-09-19; branch feature/mvp6-logistics; HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c.
This section updates historical readiness/version statements in §21/§27. DCP-002 freshly passed exit0
for existing MOD0186 Reverse Logistics. No new identity, no runtime grant. Status remains **draft**.
Current frozen SHIPMENT-BUNDLE **info.version2.0.0**, wire **contractVersionv1**;
SHA25693c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571.
Returns three operations and schemas in §22 remain unchanged by Loads publication. Carrier/Loads
annexes apply only to their own families. No inherited Returns error/replay/permission authority.
Final-pack delta binding (candidate until CT disposition): published YAML SHA256 `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`; Returns annex SHA256 `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11`; guard authority SHA256 `768757bb1fb9e39a324960e25c0e3943adb4bd6231d3dd50bf098915274406fb`. These exact bindings establish packaging parity only; they do not grant runtime uptake.

Settled sources: MOD0183 bounded CT acceptance (2026-09-16); MOD0184 bounded CT acceptance
`docs/records/audits/2026-09/mvp6-mod0184-ct-accept-02-2026-09-18/README.md`;
Loads publication/uptake `docs/records/audits/2026-09/mvp6-loads-r2-publication-2026-09-18/README.md`.
These supersede §21's old pending0184/publication state. MOD0185 runtime exists with DEV/VER evidence;
**MOD0185 CT acceptance is not assumed**. Concurrent CT record
`docs/records/audits/2026-09/mvp6-mod0185-ct-review-01/README.md` reports **REWORK** for A04 persisted-count
evidence; read-only input, never modified here. Historical architecture counts are not fresh tests/waivers.

Single review source: [owner decisions v1.0](../../../../docs/roadmap/plans/mod-0186-prep-02/owner-decisions-v1.0.md).
All D186-01…06 there are **proposals, not approvals**. They refine §23/§24 open questions, without
reopening settled upstream decisions. No Returns-specific prior owner signature found in inspected records.

- D186-01: Delivered/Closed Shipment, exact source line quantity cap, positive arbitrary precision quantities,
  exact ordinal UoM/no conversion, duplicate lines rejected; source GET-only snapshot policy.
- D186-02: Requested/Authorized/InTransit/Received/Dispositioned/Closed count; Rejected/Cancelled release;
  soft-delete never releases; all-line transaction+CAS serializes entitlement, not Inventory stock.
- D186-03: proposed Received means signed, authorized manual operational assertion audited as such;
  no invented Warehouse inbound receipt or proof of physical stock posting. Verified alternative blocked by seam.
- D186-04: optional Inventory reference preserved as opaque/unverified, including null/empty; no Inventory
  calls in recommended bounded slice. GET /transactions is real but insufficient for exact reconciliation.
  Dispositioned needs length>=1 dispositionCode only if owner approves this business amendment.
- D186-05: Returns-specific security/target grants/root/replay/error matrix in owner record §4;
  transition409 is absent frozen and requires publication. Parser401 vs post-auth unusable claims403 explicit.
- D186-06: L3 scoped Return+entitlement+receipt+audit+pending outbox; exact decimal strings, unique RMA,
  immutable root/local event causation, no live publisher or source mutation.

WORKSPACE ownership for this PREP only: this pack, docs/roadmap/plans/mod-0186-prep-02/ and
 docs/records/audits/2026-09/mod-0186-prep-02/. Every other file protected including Program.cs/contracts.
No writer agents launched. Inventory/Warehouse/Supplier/Product schemas consumed read-only; no master cloned.
Supplier has no direct role in this bounded customer-return slice; Inventory GET-only is an upper boundary,
not permission to introduce speculative calls. Warehouse outbound presence does not resolve inbound receiving.

## 29. PREP-02 Phase 1.5 / exact scope / acceptance refinements

Exact prospective DEV source list remains §25, expanded in
[owned paths](../../../../docs/roadmap/plans/mod-0186-prep-02/owned-paths-v1.0.md).
Additional proposed Domain/Features/Returns/ReturnQuantity.cs encapsulates exact coefficient/scale
arithmetic; never System.Decimal/Decimal128 rounding. This is the only added prospective source path.
Program.cs is separately requested, not owned by this PREP or an unreleased DEV draft: scoped context,
reference client,owned persistence/local pending outbox only; exact Returns family adapter, preserve
Shipment/Carrier/Loads paths. No shared JWT/CORS/global model-error rewrite or IEventOutboxStore replacement.

| Phase1.5 check | Exact proposal / gate |
|---|---|
| 1 entity fields | §22 all wire fields; EntityBase fields reused, module LE/actor; source snapshots and command history; no DTO tenant |
| 2 global names | Frozen Return/Shipment names, no GUID line ID/stock/Supplier substitute |
| 3 repository | Scoped module-specific transactional repository exception; unique source-line key excludes UoM to prevent double cap; §26 collections; approval OPEN |
| 4 entity base | Existing Domain/Common/EntityBase read-only; no GlobalEntity, no duplicate base fields |
| 5 CQRS | §25 exact standard naming, four existing pipeline behaviors; wire-specific Error adapter; approval OPEN |
| 6 golden reference | Backend Slim command/handler naming inspected; no CRUD endpoint expansion; shell none,UI N/A |
| 7 compact/UI | N/A: no frontend/RESX/DataTable/layout/new lookup |
| 8 required/null parity | Frozen §22 measured; D186 business conditions and new security responses await owner+publication; BLOCKED |
| 9 lookup boundary | No PSS lookup/catalog; reason/disposition text not locally invented master |

| AC | Exact additional future oracle (not executed runtime) |
|---|---|
| R01 | SD required omission502; optional shipmentId/status absent503; wrong identity/duplicate source lines502; source404 preserved |
| R02 | source10:6+6 race one201/one422;4+6 total10; multi-line failure rolls all lines back; UoM mismatch cannot create second cap |
| R03 | all8Return states contribution table; Rejected/Cancelled decrement once;Closed/deleted still count;replay no debit/release |
| R04 | >28scale/>30digits exact, raw strings retained; numeric equal source representations no false drift; quantity0/-1 business422 |
| R05 | authorized InTransit→Received200 manual assertion audit; outbound/evidence text is not verified receipt; wrong grant403 |
| R06 | optional Inventory ref null/empty accepted unverified; no Inventory/Warehouse outbound HTTP and no stock writes; disposition null/empty422 only at targetDispositioned |
| R07 | 64 lifecycle pairs against7allowed arrows; same-state new-key422; race serialize or safe503 without partial commit |
| R08 | §4 permission/preference matrix: base then parsed action,401 parser vs403 post-auth,scope404,root409/key409;all outputs current trace |
| R09 | original201/200 replay after status change/restart;unknown commit receipt recovery;same key across tenant/LE isolated;no target leakage |
| R10 | five transaction groups fault injection; source GET not atomically locked; drift409/no cap reset;pending events survive restart,no publisher |
| R11 | actual request bytes/status/header evidence; build/source/input hashes; no secret/raw business payload in logs; nonReturns regressions |

DoR: identity PASS, frozen inventory PASS, bounded ownership/AC/design READY FOR REVIEW;
D186-01…06 OPEN; Returns amendment publication/uptake OPEN; Phase1.5+Program.cs writer OPEN;
CT prerequisite disposition (including0185 status) OPEN; future data/migration gate OPEN;
pack promotion and dispatch NOT AUTHORIZED. Manual/opaque proposal approval would not confer E5/G5.
Versioned [DEV](../../../../docs/roadmap/plans/mod-0186-prep-02/dev-prompt-v1.0-HELD.md) and
[VER](../../../../docs/roadmap/plans/mod-0186-prep-02/ver-prompt-v1.0-HELD.md) remain **HELD**;
CT must issue new released versions after exact missing gates close; do not edit HELD into READY silently.

## 30. PHASE15-CLOSE-01 — controlling isolated core dispatch

Existing user conditionalruntime grant2026-09-20 and exact replacement grant2026-09-21 are verified in docs/records/audits/2026-09/mvp6-mod0186-phase15-close-01/authority.md. Phase1.5 designchecks and physicalentity/index/reference mappings are recorded there; allPASS orUI/lookupN/A. Published3.0.0/wirev1 YAML5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c and Returnsannex00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11 govern olderdraft/proposal labels.

Effective46pathallowlist in that package explicitly excludes ReturnOutboxWorker.cs; no workerpublisherorstockwrites. Physicaldesign selects existing approved fivecollectiontransactions and firstentitlementsnapshotdrift policy; no newbusinessdefault. Sourcehistoricalsections remain provenance, not instructions to reopenD186. Ready-for-dev is limited to this isolatedcore; Program.cs/shared/config/schema/guard/Returnsotherlane files protected. NoClaims policyinheritance.

R01–R11 are future runtime acceptance, not currentPASS. HTTP/JWT/liveproduceruptake and APIrestart await separatelyapprovedcomposition; corework canproceed. No migration/backfill/rollout/E5G5/fullmodule acceptance. Commonpack remains unchanged.

## 31. Accepted bounded scope binding

Approved: `docs/records/decisions/2026-09/mvp6-returns-pack-signoff-owner-decision-01.md`

This section records, without changing any business rule above, the Control Tower acceptance of the bounded Returns work package built under this pack. It is a proposal until the owner decision in `docs/roadmap/plans/mvp6-pack-alignment-03-returns/SIGN-OFF-DECISION.md` is recorded.

| Binding | Exact value |
|---|---|
| Owner-promoted isolated pack (this pack before §31, apart from `status_note` and the §6 contract line) | SHA-256 `1c80cca70ef774d18f5d30174566e68c9b6332b61dd1f564d54de3429886482f` = shared pack `07a8a015…b7` + the approved replacement delta `docs/roadmap/plans/mvp6-mod0186-pack-patch-repair-01/proposed-pack-v2.patch` (SHA-256 `2a1843eb52b8fd0b61a553c06a75d07b32dda57bf020734d946c004b82893709`; its binding line is kept, its `status_note` is superseded) + `status: ready-for-dev` + §30 PHASE15-CLOSE-01. Byte-identical copies are archived in `docs/records/audits/2026-09/mvp6-mod0186-http-01/authority/`, `mvp6-mod0186-r01-rework-01/authority/` and `mvp6-mod0186-r01-independent-ver-01/authority/` |
| Owner authority | Conditional pack/Phase 1.5/isolated-runtime grant, user message 2026-09-20T11:44:45Z: `docs/records/audits/2026-09/mvp6-mod0186-patch-disposition-02/authority-source.md` (SHA-256 `447fb83b5418104430ab0d5be59dd26443cf692719b1cba8b980d6d43ad6667d`). Exact replacement `2a1843eb…` → `745e9cc7…` grant, 2026-09-21: `docs/records/audits/2026-09/mvp6-mod0186-http-composition-01/authority-source.md` item 2 (SHA-256 `a57efad020a7070d3b83f0504489e4ebd8bcd5266ee38afe889318568005f979`) |
| Phase 1.5 closure | Nine-row record, PASS for isolated core, rows 6/7 UI N/A: copy `docs/records/audits/2026-09/mvp6-mod0186-http-01/authority/PHASE15.md` SHA-256 `c921c423e6c354753b550e4520940479d260d8705b7f937210abc91f228ef0a7` (the hash the CT acceptance cites). The original `mvp6-mod0186-phase15-close-01/` package named in §30 is not in the common checkout |
| Controlling CT acceptance | `docs/records/audits/2026-09/mvp6-mod0186-wp-acceptance-01/SOP-22.md` SHA-256 `3a61b6e5bb3ed039cee0c76e158a929160583212b286bbfaa35f1d42100d310e` — ACCEPTED, approved isolated bounded Returns work package only |
| Row-level acceptance | `ACCEPTANCE-MATRIX.tsv` SHA-256 `55733e998d772a9bc6df34763ab5c4098586784bbfa832b6fe3d282004b5610f` — R01–R11 PASS_BOUNDED; pack §16 items 02–05 PASS_BOUNDED; item 01 superseded for the isolated scope |
| Accepted source and evidence | R01 independent VER `docs/records/audits/2026-09/mvp6-mod0186-r01-independent-ver-01/`: 341-entry target manifest `manifests/source-final-341.tsv` `60ab3d68de8196d3a390a087884ca46c1e1530d64fa0a72e3074a15d62561052`; product patch `0bb36d02d3972f65d6644a5b12eaabc7a8820a1a50da13b17769ef7a5148e82c`; raw archive `1191b5d9c8e0f46a304ce65a131a462143e0c015e2e088de0576258fa8f85473`; Release API binary `daeefa6c9b4b2b7159cabcf397852b23c82c962a3b0e18262b4e8032004549b7`; 52/52 HTTP/DB, 4/4 restart, 78/78 Returns regression |
| Published contracts at acceptance | SHIPMENT-BUNDLE 3.0.0 YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`; Returns annex `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11`; root annex `7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af`; wire `contractVersion: v1` |
| Published contracts today (Q380) | SHIPMENT-BUNDLE 3.1.0 YAML `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`; Returns annex and root annex unchanged (same hashes as above); wire `contractVersion: v1`. re (`docs/records/audits/2026-10/mvp6-q218-contract-pin-01/VERSION-DELTA.md`, re-measured in `docs/records/audits/2026-10/mvp6-q380-contract-repin-01/`) |

**Precedence over stale wording:** SHIPMENT-BUNDLE 3.0.0 was published and bound at acceptance; this pack is now bound to the canonical 3.1.0 (`6dc1dd48…96aa2`), re-pinned by Q380 because every Returns operation and the `getShipment` read are identical in both versions (Q218). The §28 statements "status remains draft", the 2.0.0 `93c696e2…` pin and "D186 proposals, not approvals", the §29 "pack promotion and dispatch NOT AUTHORIZED" and HELD-prompt wording, and the "candidate until CT disposition" label on the §28 binding line are historical. §30 governs the isolated promotion, and this section records its accepted outcome. §30's "R01–R11 are future runtime acceptance, not current PASS" is superseded by the accepted matrix above. No business rule in §§21–30 is changed.

**Accepted boundary:** R01–R11 are satisfied only at the evidence class stated per row in `ACCEPTANCE-MATRIX.tsv` (core + selected HTTP for R03/R07; core for zero/negative and long-scale R04; test-injected failpoints for R10). Received is a **manual assertion**, not warehouse-verified receipt; no Inventory/Warehouse HTTP, stock SoR, worker or publisher exists.

**Owned paths:** the 46 effective paths listed in `docs/records/audits/2026-09/mvp6-mod0186-r01-independent-ver-01/authority/returns46.json` (SHA-256 `96f43bd7fa66256ee55e5aea2c4c412887238ea586fd96ceb05f8adb69485969`; 36 source + 10 test/probe paths). `Program.cs` and `ReturnOutboxWorker.cs` are not owned paths. Per the CT record, 43 Returns paths are absent from the common checkout.

**Still open:** common-checkout source uptake and composition, gateway and shared permission registration, real producer authoritative-root uptake beyond the accepted seam, verified inbound receiving and Inventory reconciliation, publisher/Event Bus delivery, UI, migration/backfill, rollout, E5/G5 and full-module acceptance. This section is not `done` status and grants no new DEV scope.

## 32. Tenant UI scope (UI-REVISION-01) — GoldenReferenceSlim

This section adds the tenant UI scope approved in `docs/records/decisions/2026-09/mvp6-returns-claims-ui-scope-owner-decision-01.md`
(owner decision MVP6-RETURNS-CLAIMS-UI-SCOPE-OWNER-DECISION-01, 2026-09-26, "approve both as drafted"), bound to the draft package
`docs/roadmap/plans/mvp6-ui-pack-drafts-01/` (SHA256SUMS `b568c94f7f524dd0bff1e2fa3277571fed174c6214ac0a9efd3ab13200abb57f`;
`returns/APPROVAL-DECISION.md` `edb2b06df9d3c67958eade30dd16d826173455d45e5a2ffbdb773bb668d2b3b3`). It stacks on §31 and changes
**no** business, contract, backend, owned-backend-path or acceptance rule in §§1–31. The frontmatter changes `shell: tenant`,
`golden_reference: slim` and `form_field_count: 6` apply to the UI only. For the UI, §9 ("`shell: none`"), §11 ("not applicable") and
Phase 1.5 rows 6/7 ("UI N/A") are superseded by this section; for the backend they remain as written. `status` stays `ready-for-dev`
(backend scope, per §§30–31); UI code is **not** authorized by this section alone (§32.14).

### 32.1 Identity

UI revision inside MOD-0186; no FU, child or new ID. The pack author runs
`python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0186 --name "Reverse Logistics"` before applying; non-zero exit stops.
Observed 2026-09-26: `OK`, exit 0.

### 32.2 Layout and shell contract

- `shell: tenant` → the Returns page view (`Index.cshtml`) states `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02; UI-PM-01); `_ViewStart.cshtml` unchanged.
- View folder `frontend/Diten.Web/Views/SupplyChain/Returns/`; route `/SupplyChain/Returns`; no `/Platform` prefix; no `Areas/`.
- Skeleton, empty and error are three distinct states (VIEW-001 §3.1); no spinner or "Loading" text.

### 32.3 Bound operations (nothing else)

Published SHIPMENT-BUNDLE 3.1.0 / wire v1 (`6dc1dd48…96aa2`; accepted on 3.0.0, see §31) and Returns annex (`00990a28…8a11`) only:
`queryReturns` (list/filter/reload), `createReturn` (create offcanvas), `transitionReturn` (row action), and Shipment-owned
`getShipment` consumed read-only (number, status, lines, and the lifecycle root resolved **server-side**). No Return by-ID GET, detail page,
edit, delete, bulk, import/export, server paging/search/sort, reason/disposition catalogue, remaining-entitlement display, Inventory or Warehouse call.

### 32.4 Screens, routes, permissions

Browser → same-origin MVC (`proxy-profile`, NET-001) → Gateway 5000 → SupplyChain 5061. The browser never calls 5000/5061 or holds a bearer token.

| Surface | MVC route | Gateway downstream | UI display gate | Backend |
|---|---|---|---|---|
| Page | GET `/SupplyChain/Returns` | — | `supplychain.returns.read` | — |
| List adapter | GET `/SupplyChain/Returns/api?shipmentId=&status=` | GET `/api/shipment-bundle/returns` | read | `queryReturns` |
| Shipment resolve adapter | GET `/SupplyChain/Returns/api/shipments/{shipmentId:guid}` | GET `/api/shipment-bundle/shipments/{shipmentId}` | `.create` + `supplychain.shipments.read` | `getShipment`; projection = number, status, lines (lineNumber, quantity, uomId); root never returned to the browser |
| Create adapter | POST `/SupplyChain/Returns/api` | root via `getShipment`, then POST `/api/shipment-bundle/returns` | `.create` + `supplychain.shipments.read` | `createReturn` |
| Transition adapter | POST `/SupplyChain/Returns/api/{returnId:guid}/transition` | root via `getShipment`, then POST `/api/shipment-bundle/returns/{returnId}/transition` | `.transition` + target key + `supplychain.shipments.read` | `transitionReturn` |
| Unsupported detail/edit/delete/bulk/import/export | absent | absent | — | no route, control, proxy or request |

Permission keys stay exactly as in the annex (D186-05): `supplychain.returns.read`, `.create`, `.transition` plus target keys `.authorize`,
`.transit`, `.cancel`, `.receive`, `.disposition`, `.close`, plus the approved UI prerequisite **`supplychain.shipments.read`** for create and
transition actors (G-SHIPREAD). The UI check is a display decision (UAS-001 §4); `[HasPermission]` on the backend stays the authority.

Adapter headers: `Authorization` server-side only; `X-Correlation-Id` = fresh UUID trace for GET, **the Shipment's `lifecycleCorrelationId`**
for create/transition (the Return inherits the Shipment root; annex "Persist inherited Shipment root"); `Idempotency-Key` = browser per-intent
key forwarded exactly; **`X-Tenant-Id`/`X-Legal-Entity-Id` required by the Returns family, filled only from the signed session claims**, never
from browser input; scope query keys never sent. Antiforgery on every POST. The browser-supplied `shipmentId` on a transition is a lookup hint
only; the backend root check (409 `CORRELATION_ROOT_MISMATCH`) is authoritative.

**Measured 2026-10-03 (Q279, ledger rows Q279 and Q285 in `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`):** the root rule
above is enforced by the backend for every caller of `createReturn`, not only this adapter. Against a started service, a create with a
fresh `X-Correlation-Id` returned 409 `CORRELATION_ROOT_MISMATCH`; the same create carrying the Shipment's own `lifecycleCorrelationId`
returned 201. Source of the check: `ReturnRepository.cs:56` (create) and `:92` (transition). Any client must read `lifecycleCorrelationId`
from the Shipment detail and send it. The Returns UI is not built (§33 open gap 4), so no built client has been checked against this rule.

### 32.5 List — bounded DataTables v2 profile

`data-dt-standard="v2"`, `DtDefaults.create()`, explicit `stateSave: false`, `serverSide: false`; client search/sort/paging over the
returned set only; `total` shown as returned. Filter (`_Filter.cshtml` inline collapse): `status` single Select2 with `ShowAll`
(omits the parameter), `shipmentId` UUID text input (not a lookup). No other query parameter. Save View / column visibility / colReorder / Reset
through the shared `personalizationClient` (`moduleKey`/`pageKey` codes assigned by the integration owner); no localStorage.
Columns: `rmaNumber`, `shipmentId` (LTR, copyable), localized status, actions. Absent summary field → "not provided"; no action without
`returnId`; malformed envelope → error, never empty. QuickView (`_DetailsQuickView.cshtml`) shows the row summary and copyable `returnId` only,
with no by-ID request and no Edit button.

### 32.6 Create offcanvas — field count and validation

`form_field_count: 6` (schema field types; the `lines` container is not counted) → **GoldenReferenceSlim**, create-only
`_CreateEditOffcanvas.cshtml` (no edit mode), `.diten-field` + icon per field.

| Field | Required | UI rule (no client tightening; backend authoritative) |
|---|---|---|
| `shipmentId` | yes | UUID text + Resolve; a late response for a previous UUID never populates; 404 → safe-not-found, no line table or shipment data left in DOM |
| `lines[].shipmentLineNumber` | yes | Chosen by selecting a resolved source line (one row per line, so no duplicates from the UI); read-only text |
| `lines[].quantity` | yes | Text, `inputmode=decimal`, sent as a JSON **string** exactly as typed; no conversion, locale comma handling, rounding or float; the source quantity is labelled "shipped quantity", not "remaining" |
| `lines[].uomId` | yes | Read-only from the selected source line (exact ordinal; no conversion) |
| `reasonCode` | yes (presence) | Free text sent as typed; empty allowed; no HTML `required`, trim or maxlength; no catalogue |
| `evidenceReferenceIds` | no | Repeatable text; omitted when empty; order, duplicates, empty strings preserved; no upload |

Eligibility (Delivered, Closed) is shown as a note and submit is disabled for other statuses (display only; backend 422 stays
authoritative). Required-contract parity follows the backend validator (presence ≠ nonempty).

### 32.7 Transition action

Shown only when the row status permits the arrow and the actor holds `.transition` and the target key. `occurredAt` (date-time with explicit
offset; default now; exact text kept for retries) on every target.

| Current | Target | Target key | Extra |
|---|---|---|---|
| Requested | Authorized / Rejected | `.authorize` | — |
| Authorized | InTransit / Cancelled | `.transit` / `.cancel` | — |
| InTransit | Received | `.receive` | optional `inventoryTransactionReferenceId` (opaque, unverified); label "Received (manual assertion)" — not warehouse receipt or stock posting |
| Received | Dispositioned | `.disposition` | `dispositionCode` required, length ≥ 1, no trim (whitespace allowed); optional `inventoryTransactionReferenceId` |
| Dispositioned | Closed | `.close` | — |
| Rejected, Cancelled, Closed | none | — | no action |

InTransit→Cancelled does not exist (annex correction). Optional fields not listed for a target are omitted. Confirmations use the shared premium wrapper
(`window.showConfirm`, MOD-0013); no native dialog, manual `Swal.fire`, or inline handler (form surface: gap §32.13 G-MODAL).

### 32.8 Errors, replay and concurrency (Returns annex; localized, never raw text)

| Code (HTTP) | UI behaviour |
|---|---|
| `INVALID_REQUEST` (400 schema, 401 auth, 403 context, 415 media) | 400/415: localized form summary; 401: standard session surface, JSON adapters return 401 JSON without redirect; 403: page `_AccessDenied` in shell, action closed/disabled with localized denial |
| `RETURN_NOT_FOUND` (404) and shipment 404 | One identical safe-not-found text + support reference for unknown, foreign-scope and soft-deleted targets; action closed; list reload; hidden surfaces inert and not keyboard-reachable; late responses never re-expose them |
| `INVALID_RETURN_TRANSITION` (422) | Stale-state conflict + support reference + reload |
| Other 422 (quantity, UoM, eligibility, duplicate line, disposition) | Specific localized message where the code is known; exact code set bound from the accepted backend at Phase 1.5; unknown code → generic localized text + support reference; inputs kept |
| `CORRELATION_ROOT_MISMATCH` (409) | "Request no longer matches the shipment record"; no automatic new key |
| `IDEMPOTENCY_KEY_REUSED` (409) | Stop retry; new user intent required |
| `RETURN_SOURCE_CHANGED` (409) | Source shipment changed since the first return; show the conflict; no automatic adjustment |
| `RETURN_SHIPMENT_ROOT_INVALID` (502) | Shipment reference data invalid; no retry loop |
| `RETURN_SHIPMENT_ROOT_UNAVAILABLE`, dependency or storage unavailable (503) | Temporarily unavailable; same-key retry; unknown commit never shown as rolled back |

Per intent: one pending request; the same key and identical body on network/500/503 retry.
**An intent is one opened create form or transition panel, not one payload** (amended 2026-10-04, Q403). The UI mints the `Idempotency-Key` when that form or panel
opens and keeps it unchanged across edits, failures and network/500/503 retries until it closes; only a newly opened form or panel is a new
intent with a new key. A user who edits while the outcome is unknown resends under the same key, so a committed first attempt answers
409 `IDEMPOTENCY_KEY_REUSED` instead of creating a second record. On that 409 the UI stops, keeps the inputs, tells the user the
request was already received with different values and that a different request needs a new form, and never mints a key to get past it.
Measured by R-2 (`docs/records/audits/2026-10/mvp6-r2-returns-ui-01/evidence/traps-browser.md` §T2): a key re-minted per payload created **two Returns from one intent**; one key per opened form gave 409 and **one** Return.
A 201/200
with `idempotentReplay: true` is shown as completed, then the list reloads; a replay snapshot is never shown as current state. The response
`X-Correlation-Id` (= `error.correlationId`) is a copyable support reference, never labelled as the lifecycle root.

### 32.9 Localization, UAS-001, accessibility

Seven tenant languages (en, tr, fr, es, zh, ar, ru) in `Resources/Views/SupplyChain/Returns/ReturnsIndex.{lang}.resx`, marker class
`ReturnsIndex`, `_IndexL10n.cshtml` JSON bridge + `index.l10n.js`; mandatory keys `ReturnsTitle`, `PageDescription`, `AddNewReturns`
and column headers; shared toolbar vocabulary from `SharedResource`; no English placeholder in other languages, no hardcoded fallback; status,
error and manual-assertion texts localized. Arabic RTL; UUIDs/decimals LTR-isolated; 390/768/1024/1440 without horizontal overflow;
the repeatable line and evidence editors keep unique ids and accessible names after add/remove; keyboard focus/Escape in offcanvas and
dialogs. UAS-001: without `.read` only `_AccessDenied` inside the shell (no title, filter, table, skeleton, button, toast or redirect); no
create CTA without `.create` + `supplychain.shipments.read`; no action without its keys.

### 32.10 Owned UI paths (21, new files only), protected paths, shared handoff

Owned: `frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs`; `frontend/Diten.Web/Models/SupplyChain/Returns/ReturnViewModels.cs`;
`frontend/Diten.Web/Views/SupplyChain/Returns/{ReturnsIndex.cs, Index.cshtml, _Filter.cshtml, _DataTable.cshtml, _IndexL10n.cshtml, _CreateEditOffcanvas.cshtml, _DetailsQuickView.cshtml}`;
`frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/{index.js, index.l10n.js}`;
`frontend/Diten.Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx`;
`frontend/Diten.Web.Tests/{Controllers/SupplyChainReturnsControllerTests.cs, Forms/ReturnFormContractTests.cs, JavaScript/ReturnIndexBehaviorTests.cs}`.
These are separate from, and add nothing to, the 46 backend owned paths of §31.

Protected for the UI writer: all backend source (including `Features/Returns/**` and service `Program.cs`), contracts/annexes,
`gateway/**`, shared layouts/partials/JS/CSS, `SharedResource.*.resx`, frontend `Program.cs`/DI, navigation/module/permission catalogues,
`frontend/Diten.Web/tests/diten-field-icons.test.js`, other modules' UI files, Golden Slim (read only), packs, registries, `.antigravity/**`,
guards, existing records and Git state.

One CT-appointed **integration owner** (the same owner as for Claims §32) alone delivers exact diffs for:
gateway routes (**listed, not edited here**): GET+POST `/api/shipment-bundle/returns`, POST `/api/shipment-bundle/returns/{returnId}/transition`,
and confirmation of GET `/api/shipment-bundle/shipments/{shipmentId}`, explicit routes with OPTIONS (NET-001), with passthrough of
Authorization, X-Correlation-Id, Idempotency-Key, X-Tenant-Id and X-Legal-Entity-Id, and `/returnsXYZ` not matched;
page/permission registration and G-SHIPREAD role design; tenant navigation and Ctrl+K with registry-reconciled module/page codes; shared
L10n nav keys in 7 languages; personalization confirmation; the `ICON_MAP` entries; Shipment root emission in the target; and uptake
of the accepted Returns backend (46 paths + its separately approved `Program.cs` composition) into the integrated target.

### 32.11 Single acceptance matrix (UI) — early vertical slice first

Full row text with HTTP/browser/DB expectations, owner, evidence type and dependency is controlling in
`docs/roadmap/plans/mvp6-ui-pack-drafts-01/returns/ACCEPTANCE.md` (bound by the draft SHA256SUMS above). No row has run.

| ID | Criterion | Readiness |
|---|---|---|
| RU-VS1 | Early vertical slice: real-Auth actor resolves a Delivered Shipment (non-null root) and creates a Return for one line; 201 Requested with Gateway `X-Correlation-Id` = Shipment root and X-Tenant/LE from session; +1 return, +1 entitlement per line, +1 receipt/audit/Pending outbox; reload shows the row | BLOCKED (target, gateway, G-SHIPREAD, seed) |
| RU-01, RU-02, RU-03 | Same-origin chain (BLOCKED gateway); list query parity; envelope/absent fields | BLOCKED / READY |
| RU-04, RU-05 | Skeleton/empty/error; UAS-001 no read | READY |
| RU-06, RU-07 | Create CTA gating (BLOCKED G-SHIPREAD); action gating per target with 403 zero-write | BLOCKED / READY |
| RU-08…RU-10 | Create body parity; presence ≠ nonempty; resolve stale-response guard | READY |
| RU-11…RU-14 | Safe-not-found (create, transition); cross-LE list; eligibility display vs server 422 | READY |
| RU-15, RU-16 | Root seam (BLOCKED producer uptake); idempotency (BLOCKED DN-01) | BLOCKED |
| RU-17…RU-25 | Stale transition; Received = manual assertion; disposition code; occurredAt offset; support ref vs root; 7 languages + RTL; responsive + keyboard; premium dialogs/no secrets; no SoR duplication | READY |
| RU-26 | Regression incl. Returns backend 78/78 on target | BLOCKED (target) |
| RU-27 | Source→binary→process→browser binding | READY |
| RU-28 | PNG via supported export only | BLOCKED (PRES-183-04) |
| RU-29 | QuickView without by-ID request | READY |

OUT at the start (scope-change rows, approved by the owner decision above): RU-SCR-01 checkbox + BulkActionBar; RU-SCR-02 edit/delete/bulk
delete (incl. QuickView Edit button); RU-SCR-03 import/export; RU-SCR-04 server paging/search/sort; RU-SCR-05 multi-select status;
RU-SCR-06 generic `verify_datatable_page.py --reference slim` as acceptance (run and kept as a record only).

### 32.12 Test expectations (UI)

Module tests in the three owned test files; `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module Returns --reference slim`
run and recorded (RU-SCR-06); `quality-gate-datatable` result recorded against the bounded profile; RESX parity 7/7 with no placeholder;
build of frontend, gateway and SupplyChain service on the integrated target; real-Auth browser smoke with three identities and separate
profiles; `grep` scans for native dialogs, inline handlers and "Permission denied"/"Forbidden" text; independent VER on a frozen source.

### 32.13 Remaining gaps (recorded, not decided here)

- **G-MODAL:** MOD-0013 §4 requires `window.showConfirm` and forbids manual `Swal.fire` on layout pages; whether it can host the transition inputs (`occurredAt`, `dispositionCode`, `inventoryTransactionReferenceId`) is unverified. If not, a module-owned form surface (one more owned partial) needs a scope amendment.
- **G-ICONMAP:** FORM template requires an icon per field registered in shared `diten-field-icons.test.js`; that edit belongs to the integration owner.
- **G-DATETIME:** `occurredAt` needs date-time with offset; the shared date field is date-oriented; component choice is a Phase 1.5 item.
- **Line picker in Slim offcanvas:** the repeatable line table inside the offcanvas must pass the 390/768 layout gate; if it does not, a Compact switch would need a scope decision (not silently changed).
- **422 code set:** the Returns annex names `INVALID_RETURN_TRANSITION` but not every quantity/UoM/eligibility code; the exact set is bound from the accepted backend at Phase 1.5.
- **Verifier tension:** module-pack standard §15 expects DataTable verifier PASS; the approved OUT rows make the generic verifier record-only until a scope-aware profile (DN-02 / PRES-183-03) exists.
- G-TARGET integrated target; Returns backend absent from the common checkout; DN-01 retry policy; producer root uptake; PNG; nav/personalization codes; Loads 3.1.0 forward drift of the SHIPMENT-BUNDLE pin (closed by the Q380 re-pin to 3.1.0); `module-implementation-status.md` update is a separate path.

### 32.14 Effort and authorization boundary

O/M/P (person-hours, replacement of 0186-1/4-REMAINING and the UI sub-items of 0186-5/6-REMAINING, not additive): pack/Phase 1.5 4/8/16;
frontend 32/52/88; shared integration 8/16/28; independent UI VER 14/24/40; **total 58/100/172**; with the non-UI remainders 70/120/204
(net +13.6/+26/+34.4 against the old rows). Joint delivery with Claims lowers the shared parts (draft README).

Not authorized by this section: UI code until an integrated target exists and a versioned UI dispatch is released; gateway, permission,
navigation, L10n or icon-map edits except by the single integration owner; contract changes; `done` status; commit or push.

## 33. Self-registration

Approved: `docs/records/decisions/2026-09/mvp6-self-registration-patch05-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A; pack preparation only). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md).
**Foundation:** DCP-009 §21 (interface, hosted service, options, `PlatformRegistration` settings via the existing `X-Internal-Api-Key`, project reference and `Program.cs` lines — single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

> **Base note.** Written against the current pack text (sha256 `07a8a015…`, last section §29). If the pending Returns sign-off (Q39: alignment-03 → `f4396e8a…` adding §30/§31, then the UI revision → `6c8fbe28…` adding §32) is applied first, this patch must be **rebased** (section number becomes §33) before it is applied.

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `reverse-logistics` / `ReverseLogistics` / `Reverse Logistics` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, seed-once) | 420 / `bx-undo` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/ReverseLogisticsManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/ReverseLogisticsManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `RETURNS` | Returns | `/SupplyChain/Returns` (approved; not a route yet) | `supplychain.returns.read` | — | **true** | List | 10 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous | Key source |
|---|---|---|---|---|---|---|
| `RETURNS` | `CREATE` | Create Return | `supplychain.returns.create` | Toolbar | no | const |
| `RETURNS` | `AUTHORIZE` | Authorize | `supplychain.returns.authorize` | RowAction | no | `ForTarget` |
| `RETURNS` | `REJECT` | Reject | `supplychain.returns.authorize` | RowAction | **yes** | `ForTarget` |
| `RETURNS` | `MARK_IN_TRANSIT` | Mark In Transit | `supplychain.returns.transit` | RowAction | no | `ForTarget` |
| `RETURNS` | `CANCEL` | Cancel Return | `supplychain.returns.cancel` | RowAction | **yes** | `ForTarget` |
| `RETURNS` | `RECEIVE` | Receive (manual assertion) | `supplychain.returns.receive` | RowAction | no | `ForTarget` |
| `RETURNS` | `DISPOSITION` | Disposition | `supplychain.returns.disposition` | RowAction | no | `ForTarget` |
| `RETURNS` | `CLOSE` | Close | `supplychain.returns.close` | RowAction | no | `ForTarget` |

Existing keys only: `ReturnPermissions` constants `Read`, `Create`, `Transition`, and the values returned by `ReturnPermissions.ForTarget` (Authorized/Rejected → `authorize`, InTransit → `transit`, Cancelled → `cancel`, Received → `receive`, Dispositioned → `disposition`, Closed → `close`). **D5 = A:** the completeness test reflects `ReturnPermissions.ForTarget`; no constant is added and no Returns backend change is made. QuickView is navigation, not an action.

### Navigation keys (NAV-L10N-KEYS.tsv; 0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Module.REVERSELOGISTICS` | en, tr, fr, es, zh, ar, ru | this provider |
| `Nav.Page.RETURNS` | en, tr, fr, es, zh, ar, ru | this provider |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `ReturnPermissions` **plus** every non-null value of `ReturnPermissions.ForTarget` over the frozen `ReturnStatus` enum (D5 = A) |
| M-03 | Every reflected key is in the manifest or on the API-only allow-list with a reason: `supplychain.returns.transition` (conjunction key; enforced by backend and UI, not representable as a single action key) |
| M-04 | Manifest RoutePaths = the frontend view-route set of the Returns controller (to be built), counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target (Q14/Q15), never ahead of it.

### Open gaps (carried, not solved)

1. `ReturnPermissions.cs` exists only in the accepted isolated source (`returns46.json` entry `9c3f64ed…`), not in the common checkout.
2. The six target keys are MAP-ONLY (`ForTarget`), handled by D5 = A.
3. Every row action also needs `supplychain.returns.transition`, and `CREATE` plus every row action need `supplychain.shipments.read` (G-SHIPREAD); the single-key action model cannot express these conjunctions — the declared key is the target key.
4. The Returns UI is approved but not built; the route and actions are re-checked against the built UI (M-04/M-05, W-01/W-02).
5. Rebase required if the Q39 Returns sign-off changes this pack's base first (see the base note).
6. Runtime tests R-02…R-04 need a native executor and the integrated target.
