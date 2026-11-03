---
id: MOD-0186
name: Reverse Logistics
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "Owner-promoted isolated pack 1c80cca7… (PHASE15-CLOSE-01, 2026-09-21) bound to CT-accepted bounded work package MVP6-MOD0186-WP-ACCEPTANCE-01, accepted on published SHIPMENT-BUNDLE 3.0.0 and re-pinned to 3.1.0 / wire v1 (§31, Q380). Common-checkout integration, gateway, live producer uptake, UI, E5/G5 and rollout remain open. r10 (slice B DL-3, DL-4; customer shown from customerRef, SHIPMENT-BUNDLE 3.5.0, per the MVP-1 CT answer of 2026-11-03 accepted by Natig K1): draft, not approved; needs CT-APR-02 and a CT-APR-01 row before code. DL-5 (K2) is not yet in force."
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

**Requests (E-7, owner decision 2026-10-21; revision §34):** at return receipt, one `CUSTOMER_RETURN` movement per return line into `QUARANTINE`, posted through the frozen INVENTORY `postMovement`. Inventory stays the balance owner; this module keeps only the resulting movement reference (`InventoryTransactionReferenceId`) — never a local or shadow balance.

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
carry an opaque `InventoryTransactionReferenceId`; it is a reconciliation reference, never a local balance. **Superseded for `Received` by §34 (WP-P1 r4):** `Received` posts `CUSTOMER_RETURN` movements into QUARANTINE and the service sets each line's reference; the old behaviour remains only for the pre-WP-P1 case of §34.2.

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
| INVENTORY v1 | Frozen consumed (1.5.0) | Read/reference, movement reconciliation and — §34 only — `postMovement` with `movementType: CUSTOMER_RETURN`, `toStockStatus: QUARANTINE` |
| `SHIPMENT-BUNDLE` v1 | Frozen | Reverse API/lifecycle/mock authority |
| Warehouse outbound | Frozen read-only upstream seam exists | No inbound return, receiving or automatic ingress implied; see §21 |

## 8. Runtime Constraints

Prospective mutations are tenant/LE scoped, idempotent and correlation-preserving. Receipt requests the §34 `CUSTOMER_RETURN` movements and records their references; it never writes INVENTORY persistence or
holds a balance. Invalid/terminal lifecycle transitions produce no event or partial write. Draft
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

- [ ] Runtime remains absent while `draft`. — **⊘ SUPERSEDED** (pack :582 item 01 superseded for the isolated scope; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [ ] Future code consumes MOD-0183/INVENTORY strictly by frozen contract/mock. — **⚠ PARTIAL** (reference by mocked transport; BOUNDARY-REFS B-04 (`ShipmentScope` type coupling) unruled; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Return quantity invariants and lifecycle are atomic and deterministic. — **✅ MET** (`ReturnLifecycleTests.…All64Pairs…`, `ReturnConcurrencyTests.Create_Races…`, `ReturnAtomicityTests.*`; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] No stock collection, projection or movement SoR exists in MOD-0186. — **✅ MET** (`ReturnEventCorrelationTests.A_received_and_dispositioned_return_creates_no_stock_record` (2026-10-19); G5 DB check; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Correlation ID remains unchanged through reverse lifecycle events. — **✅ MET** (`ReturnEventCorrelationTests.Every_return_event_type_carries_the_shipment_root` (8/8, 2026-10-19); G5 run; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)

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

> Note (WP-P1 r4): this quotes SHIPMENT-BUNDLE 3.3.0. SHIPMENT-BUNDLE 3.4.0 changes that description for `Received` (§34); the quote stays as history.

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

**§34 DL-1 and DL-2 additions (WP-P1 r3, MOD-0186 review F-2).** Written only when §34 is approved; still no parent
wildcard. New: `S.Domain/Features/Returns/ReturnReceiptAttempt.cs`, `S.Application/Features/Returns/Receipt/ReturnReceiptCoordinator.cs`,
`S.Application/Features/Returns/Receipt/CustomerReturnMovementMapper.cs`, tests
`services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnReceiptInventoryTests.cs` and
`…/Returns/ReturnReceiptRetryTests.cs`. Changed beyond the list above: `S.Infrastructure/Features/Returns/ReturnReferenceReader.cs`
(reads `issueTransactionId`, `skuLevel`, `lotId` of the completed dispatch attempt over the existing shipment seam — the
**only** way MOD-0186 reads MOD-0183 data; no Shipment source file is read or written, the prohibition above stands),
`S.Persistence/Features/Returns/ReturnRepository.cs`, `S.Persistence/Features/Returns/ReturnSchema.cs`, and for DL-2 the
refusal texts `frontend/Diten.Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx` and
`frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.l10n.js`. Used, not written: MOD-0183's INVENTORY client
`S.Application/Features/Shipments/Dispatch/IInventoryMovementClient.cs` and its implementation (MOD-0183 DL-1).

**§35 DL-3 and DL-4 additions (r10; Natig K1 decision 6, 2026-11-03).** Written only when §35 DL-3 / DL-4 are approved; still
no parent wildcard, and nothing of DL-5 (35.7) is granted here.

- **DL-3 (pickers and line view; UI only):** changed `frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs` (the
  eligible-shipment, reason and location adapters of 35.3 and the widened resolve projection of 35.2),
  `frontend/Diten.Web/Models/SupplyChain/Returns/ReturnViewModels.cs` (`ReturnShipmentProjection`), the views
  `frontend/Diten.Web/Views/SupplyChain/Returns/{Index.cshtml, _Filter.cshtml, _DataTable.cshtml, _CreateEditOffcanvas.cshtml, _DetailsQuickView.cshtml, _IndexL10n.cshtml}`,
  `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/{index.js, index.l10n.js}`,
  `frontend/Diten.Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, and the tests
  `frontend/Diten.Web.Tests/{Controllers/SupplyChainReturnsControllerTests.cs, Forms/ReturnFormContractTests.cs, JavaScript/ReturnIndexBehaviorTests.cs}`.
  No backend file.
- **DL-4 (details page, `getReturn`, customer copy):** new `S.Application/Features/Returns/Queries/GetReturnByIdQuery.cs`,
  `S.Application/Features/Returns/Handlers/QueryHandlers/GetReturnByIdHandler.cs`,
  `S.Application/Features/Returns/Validators/GetReturnByIdValidator.cs` (35.6); changed `S.Api/Features/Returns/ReturnsController.cs`
  (the GET by-ID action), `S.Domain/Features/Returns/ReturnOrder.cs` (`CustomerRef`), `S.Application/Features/Returns/ReturnModels.cs`
  (`customerRef`, `reasonCode`, `createdAt`, detail lines), `S.Application/Features/Returns/IReturnReferenceReader.cs`,
  `S.Application/Features/Returns/ReturnSourceSnapshot.cs` and `S.Infrastructure/Features/Returns/ReturnReferenceReader.cs` (read
  the shipment's `customerRef` over the existing shipment seam), `S.Application/Features/Returns/Handlers/CommandHandlers/CreateReturnHandler.cs`
  (copy it onto the return at create), `S.Persistence/Features/Returns/ReturnRepository.cs` and `ReturnSchema.cs`; tests
  `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnDetailsReadTests.cs` (new) and
  `…/Returns/ReturnReferenceTests.cs`. UI: new `frontend/Diten.Web/Views/SupplyChain/Returns/Details.cshtml` and
  `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/details.js`, new `frontend/Diten.Web.Tests/JavaScript/ReturnDetailsBehaviorTests.cs`,
  and the DL-3 UI files above again (Details link, QuickView link, resx keys). The manifest provider change for `RETURN_DETAILS`
  (RB-21) follows section "Self-registration".
- Not written by DL-3 or DL-4: `shipment-bundle.openapi.yaml` (3.5.0 lands with MOD-0183 DL-3), service `Program.cs`, the Gateway
  route for `getReturn` (integration owner), and any Shipment source file.

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
  calls in recommended bounded slice. GET /transactions is real but insufficient for exact reconciliation. **Superseded for `Received` by §34 (WP-P1 r4):** `Received` posts `CUSTOMER_RETURN` movements into QUARANTINE and the service sets each line's reference; the old behaviour remains only for the pre-WP-P1 case of §34.2.
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
| R06 | optional Inventory ref null/empty accepted unverified; no Inventory/Warehouse outbound HTTP and no stock writes; disposition null/empty422 only at targetDispositioned — **superseded for `Received` by §34 (R34-1…R34-12)** |
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
| InTransit | Received | `.receive` | **§34 (WP-P1 r4):** posts `CUSTOMER_RETURN` per line into QUARANTINE; the client's `inventoryTransactionReferenceId` is refused (400); label "Received". Only a pre-WP-P1 shipment keeps the old "Received (manual assertion)", marked (§34.2) |
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
| RU-17…RU-25 | Stale transition; Received = stock posting into quarantine (§34; manual assertion only for pre-WP-P1 shipments, marked); disposition code; occurredAt offset; support ref vs root; 7 languages + RTL; responsive + keyboard; premium dialogs/no secrets; no SoR duplication | READY |
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

## 34. Customer return to Inventory

> E-7 — revision draft WP-P1 r3 (§34.3 adds to §26). Owner decisions: E-7 (Beste, 2026-10-21, integration plan §2.1) and G5 / G8 (Natig, 2026-10-22,
> `docs/roadmap/plans/mvp6-integration-wp-p1-01/DECISION-DRAFT.md` §3a), revised after the MVP-1 CT's CT-APR-02 review of
> 2026-10-23 (`docs/records/reviews/MOD-0186/2026-10-23-6811f18c.md`, KOŞULLU): the receipt stores its requests, dates and
> target location at the first attempt (F-1), a retry after partial success leaves completed lines untouched (F-2), the
> company header comes from the return (F-4), the issue is read over the shipment seam (F-5), serial-tracked returns are
> out of v1; r3 closes F-2 (§25 file list), F-3 (slice table, two slices), F-6 (contract and resx named); **r4** (2026-10-26)
> closes the independent r3 review (`docs/records/reviews/MOD-0186/2026-10-26-ec08008e.md`, KOŞULLU): old "no Inventory" text
> superseded and per-line reference on the wire F-1, correctable unposted lines and permanent-refusal exit F-2, pre-WP-P1
> shipments received the old way (Natig) F-3, wire code and 400 mapping F-4; **r5** closes the review of r4
> (`docs/records/reviews/MOD-0186/2026-10-26-0c63a1f3.md`): per-line states and what a command may change, Unknown and
> Blocked lines, completion rule, `RETURN_LINE_NOT_UNPOSTABLE`, stored pre-WP-P1 distinguisher, duplicate YAML keys removed; **r6** (simpler, Natig): one key per request version `a{k}`, *sent* marker, Blocked only for
> 400 `INVALID_REQUEST`, `stockIssued` as the pre-WP-P1 marker, per-operation examples (review `2026-10-26-5c7be454.md`);
> **r7**: late writer compares its version `k` and reverses a superseded receipt, stored fields named (review `2026-10-27-e2b9d57d.md`);
> **r8**: a key answered 422 is never resent; state-based late-writer rule; late REVERSAL dates, failure and append-only audit named
> (review `2026-10-27-64b325a5.md`). It changes §2, §7, §8 and the `InTransit → Received` row of the transition table above (the client's
> `inventoryTransactionReferenceId` is refused on `Received`). **Draft revision — not approved.** Needs a new CT-APR-02 round
> (MVP-1 CT) and a CT-APR-01 row (high risk: integration and product code path) before any code.

### 34.1 What is posted

| Field (INVENTORY 1.5.0 `MovementRequest`) | Value |
|---|---|
| `movementType` | `CUSTOMER_RETURN` |
| `toStockStatus` | `QUARANTINE` (E-7). A Blocked / Expired lot is accepted into QUARANTINE by INVENTORY CT-R10 |
| `itemId`, `skuId`, `skuLevel`, `lotId`, `uomId` | from the **completed dispatch attempt's** issue of the shipment line (G8, MOD-0183 §23.4), read over the shipment seam: SHIPMENT-BUNDLE 3.4.0 `ShipmentLine.issueTransactionId` (read-only) plus the line's `skuLevel` / `lotId` (F-5) — never re-entered, never guessed |
| `serialIds` | not sent: MOD-0183 v1 issues no serial-tracked stock (§23.1), so no v1 shipment line can hold serials; a return line whose SKU INVENTORY treats as serial-tracked is refused by INVENTORY (`SERIAL_COUNT_MISMATCH`, shown as the refusal) — serial-tracked returns are out of v1 |
| `quantity` | the return line quantity (frozen decimal string; never above the shipped quantity, existing rule) |
| `toWarehouseId`, `toLocationId` | entered at return receipt (G8) — SHIPMENT-BUNDLE 3.4.0 `TransitionReturnCommand.toWarehouseId` / `toLocationId`, required when `targetStatus = Received` |
| `sourceModule` / `sourceType` | `MOD-0186` / `CUSTOMER_RETURN` |
| `sourceDocumentId` / `sourceLineId` | return id / shipment line number |
| `documentGroupId` | shipment id (joins the return to its shipment's issue movements) |
| `transactionDate` / `postingDate` | the validated `occurredAt` of the receipt command that **opened the attempt**; posting day = UTC day (E-6); stored, and changed only for a **Refused** line that a later command corrects — under a new key (below). The `ReturnReceived` event and the receipt carry the `occurredAt` of the command that completes `Received` (r6) |
| `idempotencyKey` / `Idempotency-Key` | deterministic per line and request version: `{returnId}:{shipmentLineNumber}:customer-return:a{k}`, `k` = 1 for the first request and **+1 each time a Refused line's request is corrected** (r6, review F-1), so a key is never sent with two different bodies and `IDEMPOTENCY_KEY_REUSED` cannot arise from a correction |
| `X-Legal-Entity-Id` | the return's stored, validated `LegalEntityId`, always sent (F-4) |
| `X-Correlation-Id` | the receipt command's correlation id, forwarded (E-1) |

The caller's token is forwarded (G5): the receiving role needs `inventory.movements.create`, granted in Roles →
Permissions (the BL-652 interim path), never through the default role template. An INVENTORY 403 is answered as 403
`INVALID_REQUEST`, never 503 (F-4).

### 34.2 Receipt attempt and failure behaviour

- **Stored requests; what a later command may change (F-1; r5, review F-1 / F-2).** The first `Received` command stores on
  the return a `ReceiptAttempt`: per line the full §34.1 request body, the dates and the target warehouse / location, the
  **request version `k`** and the **sent marker** (key and body, r7), the **last INVENTORY answer** of that line, plus a
  single-writer lease with an id. Each line is in one of these states, and a
  later `Received` command treats them so:
  - **Posted** (has a `transactionId`) or **NotPosted** (`unpostedLines` / `PRE_WP_P1`) → always sent / kept with its stored
    request; the command's values never apply to it.
  - **Refused** (its last answer is an INVENTORY **422**) → **correctable**: the command's `toWarehouseId`, `toLocationId`
    and `occurredAt` replace that line's stored request, which is then sent under the **next key `a{k+1}`** (r6) — so
    a key is never reused with another body.
  - **Unknown** (sent, no recorded answer — r6, review F-1): before each INVENTORY call the lease holder marks the line
    *sent* with its key `a{k}` and exact body, conditional on the lease; the state changes only when an answer is recorded.
    A timeout, 503 or lost lease therefore leaves the line Unknown, never Refused. The next command first resends the
    stored request with the same key: a replay makes it Posted (with the `transactionId` a lost answer carried), a 422 makes
    it Refused and correctable on the following command. A late writer follows the rule below.
  - **Blocked** (INVENTORY 400 `INVALID_REQUEST` — our request was malformed, an integrity fault) → exits only through
    `unpostedLines` with `reasonCode = INTEGRITY_FAULT`, by a user holding `.receive`, recorded in the central-audit record
    with the INVENTORY answer. An `IDEMPOTENCY_KEY_REUSED` can no longer come from a correction (one body per key); if it is
    ever seen, the line stays Blocked and is **not** unpostable — INVENTORY holds a movement for that key, so receiving the
    line as `NotPosted` would hide stock; it is investigated with the MOD-0173 owner (logged, audit record).
  A command with different values when **no line is Refused and none is Unknown** → 409 `RETURN_RECEIPT_IN_PROGRESS` with the stored values in
  `details`. A concurrent command while another holds the lease → the same 409.
- **A key answered 422 is never sent again (r8, review F-1 on r7).** Once a line's key `a{k}` has a recorded 422, the
  line is Refused and its next request — with corrected or with unchanged values — goes under `a{k+1}`.
- **Late writer (r7, state-based in r8 — review F-1 on r7).** A writer whose lease expired and who gets a 201 it cannot
  record re-reads the return and looks at **its own version `k`, the line's stored `k` and the line's state**, the same way
  MOD-0183 §23.4 does for a dispatch:
  - stored `k` **equal** and the line **Unknown**, or **Posted with this very `transactionId`** → it does nothing but log: the
    lease holder resends that key and body and gets the same `transactionId` by replay (or already has it);
  - stored `k` **greater**, or equal and the line **Refused, NotPosted or Blocked** (nobody will resend `a{k}`) → its
    `CUSTOMER_RETURN` is a second, unrecorded receipt: it **reverses it** with a REVERSAL — `movementType = REVERSAL`;
    `itemId`, `skuId`, `skuLevel`, `quantity`, `uomId` equal to that request; `sourceModule` / `sourceType` `MOD-0173` /
    `REVERSAL`; `sourceDocumentId` = that `transactionId`; `documentGroupId` = the shipment id; `transactionDate` /
    `postingDate` = the moment the late writer received its 201 (UTC), reused unchanged if the same process retries; no
    `lotId` / `serialIds` / `from*` / `to*` — under the unused key `{returnId}:{shipmentLineNumber}:customer-return-reversal:a{k}`.
  - It then **appends** the pair (receipt and reversal `transactionId`s) to the return's audit history — an append-only
    write that needs no lease — and logs an integrity fault.
  - **If that REVERSAL fails** (422, 503, crash): the receipt `transactionId` is still appended to the audit history and the
    integrity-fault log, and the stock is corrected in Inventory by its owner. This, and the short window between the lease
    holder's 422 and its record, is the stated **residual risk** (as MOD-0183 §23.4).
  This is the only REVERSAL §34 sends; whether INVENTORY reverses a `CUSTOMER_RETURN` out of QUARANTINE with this body is
  asked of the MVP-1 CT (R-1 extended).
- **Permanent refusal (r4, review F-2).** When INVENTORY refuses a line with a code that no retry can change
  (`ITEM_NOT_ACTIVE`, `SKU_LEVEL_NOT_STOCKABLE`, `UNKNOWN_ITEM`), or the line is Blocked (above), a user holding `.receive`
  may receive that line **without stock posting** by naming it in the 3.4.0 field `TransitionReturnCommand.unpostedLines`
  (`shipmentLineNumber`, `reasonCode`); the line keeps `inventoryTransactionReferenceId = null` and `inventoryPosting =
  NotPosted`, the central-audit record carries the reason and the last INVENTORY answer, and the stock is corrected in
  Inventory by its owner. For any other line the field is refused with **422 `RETURN_LINE_NOT_UNPOSTABLE`**
  (`details.shipmentLineNumber`, `details.lastInventoryCode`; SHIPMENT-BUNDLE 3.4.0 example; its `ContractErrorCodes` constant
  ships with the 3.4.0 contract in MOD-0183 DL-1, because `ContractErrorCodeParityTests` pins contract and constants together).
- **Posting and completion.** Lines are posted in the order they are stored on the return. When **every line is `Posted` or
  `NotPosted` with a reason** (r5, review F-4a), the `Received` transition commits with its receipt, central audit, the frozen `ReturnReceived` event and each line's
  `InventoryTransactionReferenceId` (set by the service; a client value on `Received` → 400 `INVALID_REQUEST`).
- **Partial success (F-2).** A line refused by INVENTORY (any 422) leaves the return `InTransit` and answers **422
  `INVENTORY_REFUSED`** with `details.shipmentLineNumber` and `details.inventoryCode` (the MOD-0183 code; r4, review F-4). Lines already posted **stay posted** and their stored `transactionId` is never changed:
  the goods are physically in quarantine and no REVERSAL is used. A retry resends every Unknown line with its stored request and key (a Refused line goes under `a{k+1}`, above);
  posted lines answer `idempotentReplay: true` (success, same `transactionId`), only the missing lines are posted.
- INVENTORY unreachable / 5xx / timeout → 503 `DEPENDENCY_UNAVAILABLE`; no transition; posted lines stay posted. INVENTORY
  400 (`INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED`) is an integrity fault: logged, the attempt is blocked for the line, the
  command answers 500 (r4, review F-4). INVENTORY 403 → 403 `INVALID_REQUEST`. 3.4.0 declares 403 and 500 for
  `transitionReturn`.
- **Pre-WP-P1 shipments (Natig 2026-10-26, review F-3).** The distinguishing fact is stored, not guessed (r5, review F-4c):
  MOD-0186 reads it over the shipment seam as SHIPMENT-BUNDLE 3.4.0 `ShipmentDetail.stockIssued` (r6, review F-2): a
  `Dispatched`-or-later shipment with `stockIssued = null` has no dispatch attempt — it was dispatched before MOD-0183 §23
  DL-1 — and its return lines are
  received **the old way**: no INVENTORY call,
  `inventoryTransactionReferenceId = null`, `inventoryPosting = NotPosted`, reason `PRE_WP_P1`, recorded in the central-audit
  record and shown on the return ("received without stock posting — shipped before stock integration"). Nothing gets stuck in
  `InTransit`. A shipment with `stockIssued = true` whose line has no `issueTransactionId` (an integrity fault) is refused with
  422 `RETURN_SOURCE_MOVEMENT_MISSING`; no movement, no lot guessed.
- **Per-line reference on the wire (r4, review F-1).** The frozen wire has no return detail; SHIPMENT-BUNDLE 3.4.0 adds a
  read-only `ReturnResponse.lines[]` (`shipmentLineNumber`, `inventoryTransactionReferenceId`, `inventoryPosting` `Posted` /
  `NotPosted`, `inventoryPostingReason`) to the transition answer, so each line's `CUSTOMER_RETURN` (or its absence) can be
  read. 3.4.0 also rewrites the `transitionReturn` description ("the only Inventory HTTP call is the CUSTOMER_RETURN posting on
  Received") and adds `TransitionReturnCommand.unpostedLines`.

### 34.3 Return acceptance

| AC | Measurable acceptance |
|---|---|
| R34-1 | Receipt of a 1-line and an N-line return posts N `CUSTOMER_RETURN` movements into QUARANTINE with the §34.1 fields and `X-Legal-Entity-Id` = the return's LE; the stock rises in QUARANTINE, not AVAILABLE (MVP-1 live tour "iade → karantina"); `ReturnResponse.lines[]` shows each line's `inventoryTransactionReferenceId` |
| R34-2 | Replaying the receipt command posts no second movement and no second event |
| R34-3 | A refused line: return state unchanged, posted lines kept with their `transactionId`, retry completes with replay for them; the INVENTORY code and `RETURN_SOURCE_MOVEMENT_MISSING` / `RETURN_RECEIPT_IN_PROGRESS` and the `NotPosted` / `PRE_WP_P1` labels shown localized in all 7 languages of `ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (DL-2) |
| R34-4 | INVENTORY unavailable → 503, no transition, no event; INVENTORY 403 → 403, never 503 |
| R34-5 | Cross-tenant / cross-LE return id → 404, no movement |
| R34-6 | Shipment dispatched after DL-1 but a line has no completed issue → 422 `RETURN_SOURCE_MOVEMENT_MISSING`, no movement, no lot guessed |
| R34-10 | **Pre-WP-P1 shipment** (`stockIssued = null` on the shipment detail): its return is received with no INVENTORY call; lines `inventoryPosting = NotPosted`, reason `PRE_WP_P1` in the audit record and in `ReturnResponse.lines[]`; sabotage: treating a shipment with `stockIssued = true` (or reading MOD-0183's store directly) as `PRE_WP_P1` makes the test red |
| R34-11 | **Correcting a refused line:** line 1 posted, line 2 refused `UNKNOWN_LOCATION`; a command with a new `toLocationId` posts line 2 with it, line 1 is resent with its stored request (replay, unchanged) |
| R34-13 | **Unknown is not correctable:** line 2's post times out; a command with a new `toLocationId` first resends line 2's stored request unchanged (replay → Posted, or 422 → Refused); its new value applies only on a later command and only if line 2 became Refused |
| R34-14 | **Blocked line exit:** INVENTORY answers 400 `INVALID_REQUEST` for a line → 500, line Blocked; `unpostedLines` with `INTEGRITY_FAULT` completes `Received`, audit carries the INVENTORY answer; `unpostedLines` for a Posted, Refused-but-fixable or `IDEMPOTENCY_KEY_REUSED` line → 422 `RETURN_LINE_NOT_UNPOSTABLE` |
| R34-16 | **Late writer with a superseded version (r7):** A marks line 2 sent under `a1` and pauses; B resends `a1` → 422, line Refused; C corrects under `a2` → 201, line Posted; A wakes and sends `a1` → 201 → stored `k` (2) is greater → A reverses its movement with `…:customer-return-reversal:a1`; QUARANTINE net +quantity once. Sabotage: removing the `k` comparison makes the test red |
| R34-17 | **Late writer, same version (r8):** A marks line 2 sent under `a1` and pauses; B resends `a1` → 422, line Refused (`k` = 1); A wakes, sends `a1` → 201 `C2`, sees `k` equal and the line Refused → reverses `C2` with `…:customer-return-reversal:a1`; C's correction goes under `a2` (never `a1`). Variant: B's 422 is `ITEM_NOT_ACTIVE`, the item is reactivated, A's 201 → reversed, the user's `unpostedLines` hides no stock. Sabotage: dropping the state condition makes the test red |
| R34-15 | **Lost answer on a corrected line (review F-1):** line 2 Refused, corrected with `toLocationId` X under key `a2`; INVENTORY posts `C2`, the answer is lost (line Unknown); the next command with Y first resends `a2` with X → replay `C2`, line Posted at X — never a second posting, never `NotPosted`. Sabotage: removing the *sent* marker makes the test red |
| R34-12 | **Permanent refusal:** line refused `ITEM_NOT_ACTIVE` → `unpostedLines` with a reason completes `Received`, line `NotPosted`, audit carries the code; the same field for a line refused `UNKNOWN_LOCATION` → 422 |
| R34-7 | **Stored requests (F-1):** when no line is Refused and none is Unknown, a command with another `toLocationId` or `occurredAt` → 409 `RETURN_RECEIPT_IN_PROGRESS`; a command with the same values resends the stored requests byte-for-byte (dates included) |
| R34-8 | **Joint scenario with MOD-0183 (S23-16):** three-line shipment, line quantities 10 / 10 / 10. (1) Dispatch attempt a1, line 2 refused → line 1 issued (`T1`) and reversed (`R1`), AVAILABLE net 0. (2) Re-dispatch a2 → issues `T2`, `T3`, `T4`, all different from `T1`, AVAILABLE net −30. (3) Return line 1 (4 units) and line 3 (10 units); line 3 refused → line 1 posted into QUARANTINE (`C1`, +4). (4) Retry → line 1 replays `C1` unchanged, line 3 posted (`C2`, +10). Final: AVAILABLE −30, QUARANTINE +14; the return reads `T2` and `T4` (attempt a2), never `T1` |
| R34-9 | Serial-tracked SKU on a return line → INVENTORY's serial refusal shown; nothing posted for that line |

Each rule above gets a sabotage proof (remove the posting, change the status, drop the key, recompute the dates on retry,
read attempt a1's issue → the matching test is red). The full SupplyChain test assembly runs unfiltered.

## 35. Screen slice B and return approval

> **Draft revision r10 — not approved.** Owner decisions K2 and K4, and the MVP-1 CT's answer on customer identity (MOD-0177 owner,
> 2026-11-03, accepted by Natig K1 the same day): `docs/records/decisions/2026-11/mvp6-screens-and-return-approval-owner-decisions-2026-11-02.md`.
> r10 changes how the customer is carried and shown (`customerRef`, SHIPMENT-BUNDLE 3.5.0) and, by Natig K1's decisions of the same
> day, moves `getReturn` (item 1 of "Contract changes needed") into 3.5.0 and extends the owned-files list; what changed is listed in 35.13.
> Needs CT-APR-02 and a CT-APR-01 row before any code; that approval covers DL-3 and DL-4 only (35.7 is not yet in force). Facts below are measured on `main` `49012bd7b` unless marked. Slice A is
> PR `bestepullukcu-alt/ERP-vNext#355` (branch `feature/sce/mvp6-screen-fixes-a-01`, `1ca0e36cc`); this section is written on top of
> it. Whether #355 is merged is **not measured** (no `gh` here); slice B starts only after it is on `main`.
> When this section is approved it replaces the sentences it names in sections "Tenant UI scope", "Self-registration" and
> "Exact frozen operation and field parity". Nothing else in those sections changes.

### 35.1 What this section replaces

| Section and sentence | Replaced by |
|---|---|
| "Bound operations": no Return by-ID GET, detail page, reason catalogue | 35.5, 35.6, 35.3 |
| "Screens, routes, permissions": Shipment resolve adapter projection = number, status, lines (lineNumber, quantity, uomId) | 35.2 |
| "List — bounded DataTables v2 profile": `shipmentId` UUID text filter; QuickView shows the row summary only | 35.3, 35.5 |
| "Create offcanvas": `shipmentId` UUID text + Resolve; `reasonCode` free text, no catalogue | 35.3 |
| "Transition action": Received `toWarehouseId` / `toLocationId` as text | 35.3 |
| "Exact frozen operation and field parity": "no GET-by-ID" | 35.6 |
| "Bound operations": "no … Inventory or Warehouse call" | 35.2, 35.3, 35.5: read-only TRACE, LOCATION and QUARANTINE reads. No Warehouse call and no stock write are added |
| "Screens, routes, permissions": row "Unsupported detail/edit/delete/bulk/import/export" | 35.5 opens the detail route only. Edit, delete, bulk, import and export stay absent |

The `Requested` row of "Transition action", its `Received → Dispositioned` row, and the `AUTHORIZE` and `REJECT` actions of "Self-registration" stay in force as written. Only an approved DL-5 can replace them (35.7, 35.8).

### 35.2 Line fields of the return screens

The Shipment resolve adapter (GET `/SupplyChain/Returns/api/shipments/{shipmentId:guid}`) widens its projection. The class today is
`ReturnShipmentProjection` in `frontend/Diten.Web/Models/SupplyChain/Returns/ReturnViewModels.cs`; it carries `lineNumber`, `quantity`, `uomId`.

| Projected per line | Source | Shown as |
|---|---|---|
| `lineNumber`, `quantity`, `uomId` | `getShipment` (today) | as today |
| `itemId`, `skuId`, `skuLevel`, `lotId` | `getShipment` `ShipmentLine` (SHIPMENT-BUNDLE 3.4.0; never shown as a GUID) | not shown |
| `skuName`, `skuCode` | PRODUCT-MASTER `POST /api/product-master/skus/bulk-read`, server-side in the adapter: `name`, `canonicalCode` | product / SKU, one name and one code |
| `lotNumber` | TRACE `GET /api/trace/lots/{lotId}`, server-side in the adapter | lot number; empty when the line has no lot |

- Names are read at display time and **are not stored on the return**: MOD-0186 keeps no product or lot master.
- `skuLevel` and `lotId` are already read by `ReturnReferenceReader` for §34; the adapter only adds them to the browser projection.
- `BulkSkuReadItem.name` is derived at read time: the item's name, or the Global Product name with pack quantity and unit, plus the market code on an LSKU. It already names the product, so one name and one code are shown. No separate product code is shown: bulk-read returns none, and a Global Product id answers `notFound` there. A per-line `/products/{itemId}` read is not added.
- PRODUCT-MASTER is `1.1.0-draft.16`, not frozen; a read for display and selection only is accepted (OD-186-5, decided). Field names above are re-checked at Phase 1.5.
- A failed name read shows a localized "name unavailable" text with the row kept. It never blocks a return and never shows a GUID as a label. A permission failure on the read is treated the same way and logged.
- New UI prerequisites for create and transition actors, beside `supplychain.shipments.read`: `mdm.product-master.read` (already on the tour's permission list), `trace.lots.read`, `inventory.locations.read` (receiving pickers) and `crm.account.read` (customer name and code, resolved from `customerRef`). Without `crm.account.read` the shipment picker and details show number and date only, never the id. Details also uses `quarantine.holds.read` (35.5); workflow keys are in 35.7.
- Return lines themselves stay `shipmentLineNumber`, `quantity`, `uomId` (`ReturnLine` is unchanged). `shipmentLineNumber` remains the only key; no GUID line identity is added.

### 35.3 Pickers

Every picker is a Select2 search field inside `.diten-field`, served by a same-origin MVC adapter (NET-001). The browser never calls the Gateway or a service. No picker text is a GUID.

| Field | Replaces | Source | Rule |
|---|---|---|---|
| Shipment (create form) | UUID text + Resolve | new adapter GET `/SupplyChain/Returns/api/eligible-shipments` → `queryShipments` with `status=Delivered`, then `status=Closed` | shows number, customer name and code (resolved from `ShipmentSummary.customerRef` by the MOD-0183 section "Screen slice B" display rule; a shipment without `customerRef` shows "old record"), date. No id is shown. Choosing runs the existing resolve with its stale-response guard. The eligibility note and backend 422 stay |
| Shipment (list filter) | UUID text input | same adapter, without the status restriction | sends the frozen `shipmentId` parameter. The company comes from the shared selector of slice A |
| Reason code | free text | closed module list, below | sends the code through the frozen `reasonCode` string |
| Receiving warehouse | `toWarehouseId` text | `GET /api/locations/search` (`searchLocations`) with `search`, `type=Warehouse`, `status=Active` | the company comes from `LocationRepository.Scope()`, not from a parameter |
| Receiving location | `toLocationId` text | tree picker below, from the chosen warehouse | disabled until a warehouse is chosen; cleared when the warehouse changes. `toLocationId` stays required on Received. `GET /api/locations` is not used: it accepts only `type`, `parentId`, `legalEntityId` and would ignore these filters |

- **Tree picker.** `parentId` returns direct children only (`LocationRepository`), and the contract has no ancestor filter, so the contract does not change. The picker opens the tree one layer at a time with `GET /api/locations/search`, `parentId` = the opened node, `status=Active`, **without** `isStockable`, so non-stockable parents stay visible: Warehouse → StorageLocation → Zone → Bin, or StorageLocation → Bin (`LocationHierarchyRules`, K-01). Only a node with `isStockable = true` can be chosen; a StorageLocation may be both chosen and opened. `search` narrows the open layer only. (OD-186-4, closed.)
- In 3.4.0 `queryShipments` has no text search, and its summary carries no `customerRef` (`ShipmentProjection.Summary`). The UUID input goes away, so the picker needs both: 35.9 item 5 (SHIPMENT-BUNDLE 3.5.0, applied with MOD-0183 DL-3) is a **precondition of DL-3**, not an option. It also needs no per-row `getShipment`. `search` finds a shipment by number or source document, not by customer name (names are not stored).
- **Old records (SB-19 of MOD-0183, accepted limit).** A shipment or return without `customerRef` is never linked to an account by a match on `shipToReference`; it is shown separately, marked "old record", with no CRM call.
- **Reason list (OD-186-2, decided):** a closed, module-owned code set in v1: `DAMAGED`, `WRONG_ITEM`, `WRONG_QUANTITY`, `EXPIRED`, `QUALITY_COMPLAINT`, `CUSTOMER_REFUSED`, `OTHER`, with labels in `ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx`. No lookup key exists for return reasons in `Diten.Platform` or MDM. The backend stays a free string, so old returns keep their text, shown as written.
- `RETURN_RECEIPT_IN_PROGRESS` keeps its behaviour: stored warehouse and location ids from `details` fill the pickers, shown by name.
- The transition panel and create form keep one `Idempotency-Key` per opened form or panel (section "Errors, replay and concurrency").

### 35.4 Form shape

`form_field_count` stays 6 and `golden_reference` stays `slim`: shipment, `shipmentLineNumber`, `quantity`, `uomId`, reason, `evidenceReferenceIds`. Each line row shows product / SKU and lot read-only (35.2). `ICON_MAP` entries for changed fields go to the integration owner (G-ICONMAP).

### 35.5 Return details page and quarantine link

A declared difference from the Slim file set: Slim has no Details page, and this module gains one because the list row cannot hold lines and receipt results.

| Surface | MVC route | Gateway downstream | Display gate | Backend |
|---|---|---|---|---|
| Details page | GET `/SupplyChain/Returns/Details/{returnId:guid}` | — | `supplychain.returns.read` | — |
| Details adapter | GET `/SupplyChain/Returns/api/{returnId:guid}` | GET `/api/shipment-bundle/returns/{returnId}` | read | new `getReturn` (35.9 item 1) |
| Quarantine link adapter | GET `/SupplyChain/Returns/api/{returnId:guid}/holds` | GET `/api/quarantine/holds/by-source?sourceModule=MOD-0186&sourceDocumentId={returnId}` | read + `quarantine.holds.read` | MOD-0175 `listHoldsBySource` |

- New files: `Views/SupplyChain/Returns/Details.cshtml`, `wwwroot/assets/js/SupplyChain/Returns/details.js`, resx keys in the existing `ReturnsIndex` set. `Layout = "_LayoutTenantShell"`. Skeleton, empty and error are three states. Cards use `card backbone-preview-section` and `row g-6`; `dl.row`; no edit button (Details template).
- **Summary card:** RMA number, status, shipment number linking to the shipment details page, customer name and code, reason, created date. A GUID is not a label. The customer comes from the return's own `customerRef` (35.9 item 6); a return created before it has none, and then the shipment's `customerRef` is used; when neither is set the card shows "old record" and the shipment's `shipToReference` as stored. A failed name read shows only the localized "name unavailable" text, never the id.
- **Lines sub-table:** line number, product / SKU name and code, lot, quantity, UoM, `inventoryPosting` label, reason, quarantine record.
- **Quarantine (KRN) link:** the `CUSTOMER_RETURN` movement carries `sourceDocumentId` = return id (§34.1), so a hold is found by source. Each row shows `holdNumber` (`KRN-YYYY-NNNN`) linking to the MOD-0175 hold page, plus hold status. Whether MOD-0175 stores `sourceModule` / `sourceDocumentId` from this movement for this query is **not measured at runtime**. A line `NotPosted` has no hold and shows why. Without `quarantine.holds.read` the block is not drawn (UAS-001).
- Known defect carried: after a REVERSAL the hold stays Open (F-2, owner MOD-0175; `QuarantineIntake.TargetOf` returns `None` for a reversal). The link shows the hold as stored and draws no conclusion from it.
- List: the row's `Details` link is navigation, not a manifest action. QuickView stays and gains the link. New manifest page `RETURN_DETAILS`, route above, `supplychain.returns.read`, parent `RETURNS`, not navigation-visible. Tests M-04 and M-07 of "Self-registration" apply to it.
- Help text: the page needs a "?" help entry (UXH-001), or a debt-ledger row.

### 35.6 Read model

`getReturn` adds the first by-ID read. It is tenant + company scoped: a foreign or unknown id answers 404 `RETURN_NOT_FOUND`, one text for both. Its handler, query and validator follow the folder rule of the section "Backend File Convention" (`Queries/GetReturnByIdQuery.cs`, `Handlers/QueryHandlers/GetReturnByIdHandler.cs`, `Validators/GetReturnByIdValidator.cs`). The response uses the unwrapped contract form.

**Customer on the return (r10, 35.9 item 6).** When a return is created, the service copies the shipment's `customerRef` from the
`getShipment` answer it already reads at create (`ReturnReferenceReader`, called through `CreateReturnHandler`) onto the return,
as given; it is never taken from the client and the service does not call CRM. `ReturnSummary.customerRef` (so also the
create answer, the list and `getReturn`) returns it read-only. Returns created before this are not back-filled; their
`customerRef` is null. Measured on `main` `49012bd7b`: `ReturnReferenceReader.ParseShipment` checks named top-level fields and
duplicates but has no top-level allow-list, so a 3.5.0 `ShipmentDetail` with `customerRef` is already accepted; DL-4 adds the
read of the field and a test.

### 35.7 Authorization through Platform Workflow (K2) — not yet in force

> **Draft of DL-5, outside the approval of this revision.** K2 stands, but its effect is postponed. Until DL-5 is approved, the `Requested` row of "Transition action" and the `AUTHORIZE` and `REJECT` actions stay in force, and return approval runs through the existing `.authorize` transition. K2 is therefore **not met live** until DL-5 ships. OD-186-1 and OD-186-6 are decided when DL-5 opens, and DL-5 then takes its own CT-APR-02 and CT-APR-01. Nothing below may be coded before that.

Dispatch of a shipment is not touched: K2 names no workflow there.

**Pattern.** The MOD-0175 pattern is the measured one, and SupplyChain has no consumer today (`Program.cs`): `GatewayWorkflowClient` (Inventory) calls MOD-0023 over the Gateway at `/api/v1/workflow/*` with the **caller's** token, and `QuarantineWorkflowOutcomeConsumer` applies `platform.workflow.instance.completed.v1` with an inbox. The CRM `ClaimWorkflowOutcomeConsumer` is the same shape. Approval tasks reach the Task Center through Platform's `WorkflowApprovalWorkItemProvider`; no module bridge class is written (WC-D1).

| Step | Behaviour |
|---|---|
| Submit | A user holding `supplychain.returns.authorize` and `platform.workflow.instances.start` sends a `Requested` return for approval: new command, new contract operation (35.9 item 2). It starts a MOD-0023 instance (`POST /api/v1/workflow/instances`, caller's token) with object type `return.authorization`, object id = return id, object ref = RMA number, template code `SCE-RETURN-AUTHORIZATION` (seed resource under `Application/Features/Returns/Workflow/`; the template record is MOD-0023's), candidates as below, idempotency key `return:return.authorization:{returnId}:r{n}`, `n` = the submission number. Stored on the return: `workflowInstanceId`, `authorizationState = PendingApproval`, `n` |
| No candidate left | 409 `RETURN_APPROVERS_NOT_CONFIGURED`, nothing started. This also covers Platform's `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED`, which the MOD-0175 client maps to template missing: the Returns client maps it here |
| No active template | 409 `RETURN_WORKFLOW_TEMPLATE_MISSING`, no state change. There is no fallback to direct authorization |
| Platform 401 / 403 | 403 `RETURN_WORKFLOW_FORBIDDEN`, no state change; the panel names the missing workflow permission in localized text |
| Platform refuses the body | 400 `RETURN_WORKFLOW_REJECTED`, no state change |
| Workflow unreachable | 503 `DEPENDENCY_UNAVAILABLE`, no state change |
| Approved | The return moves `Requested → Authorized`, `ReturnAuthorized` event, audit, with `occurredAt` = the event's `CompletedAt` and the return's stored correlation root |
| Rejected | `Requested → Rejected`, `ReturnRejected`; entitlement released as today, once |
| Cancelled / timed out | The return stays `Requested`, `authorizationState = None`; it can be submitted again with `n + 1`. The user sees the outcome |
| Four-eyes violation | The approver is the person who opened the return. Nothing moves: the return stays `Requested`, `authorizationState = None`, `lastAuthorizationOutcome = FourEyesViolation`, entitlement unchanged. It can be submitted again with `n + 1`, and the screen says a new approval is needed. The instance id stays in the audit record |
| Approver unreadable | The outcome is not applied (OD-186-3). The return stays `PendingApproval` and the next reconciliation reads the history again, as MOD-0175 does. An instance whose approver can never be read needs its owner: stated residual risk |
| Direct call | `transitionReturn` with target `Authorized` or `Rejected` answers 422 `RETURN_APPROVAL_REQUIRED`, whatever the caller's permissions |

- **Approver source.** Candidates are sent by this module, the MOD-0175 shape (`QuarantineWorkflow.StartAsync`): a per-company list of `position:{guid}` / `user:{id}`, 1 to 10. The opener (`ReturnOrder.CreatedBy`) and the submitter are removed from it before the start, so neither becomes the approver. Candidates in the template's first step would win over the request (`StartWorkflowInstanceHandler`), so the template's step must stay empty. The CRM pattern (template positions, empty request) is not used, because it cannot exclude the opener. A position may still resolve to the opener; the four-eyes row catches that. **Where the list is kept and who edits it is OD-186-6:** no settings surface is specified here.
- **Trust.** Platform publishes unsigned (`QuarantineWorkflowOutcomeConsumer` comment). The event is applied only when the tenant owns the return and the event's instance id equals the stored `workflowInstanceId` of a return still `PendingApproval`. Inbox: `returns_event_inbox`, one row per `EventId`.
- **Reconciliation is best effort, not guaranteed.** MOD-0175 measured that with the broker down a completion event is dead-lettered and lost. The next read of the Returns list or details by a user holding `platform.workflow.instances.view` reads `GET /api/v1/workflow/instances/by-objects`, then the history, and applies the outcome through the same path; a "Refresh status" action does the same. There is no background reconciler, because no service token exists. Until such a read, the return stays `PendingApproval`: this is safe and shown as such. A user without the key sees the stored state, and no request is sent.
- **Outside permissions, not declared by this module:** `platform.workflow.instances.start` (submitter), `platform.workflow.tasks.approve` (approver), `platform.workflow.instances.view` (reconciliation), read from `WorkflowDefinitionsController`. They are tenant-scope keys today (`SeedOwnedTenantScopeKeys.cs`); whether a default role holds them is **not measured**, so the tenant grants them.
- **Four-eyes (OD-186-3, decided):** the approver may not be the person who opened the return. Checked when the outcome is applied, as in MOD-0175, on both the consumer and the reconciliation path; outcomes are in the table above.
- **Rollout.** Returns already `Requested` use the same submit action. Returns already `Authorized` or later are unaffected.
- **Screens.** Row action "Send for approval" replaces Authorize and Reject. The list and the details summary card show the approval state, localized in 7 languages. The manifest actions `AUTHORIZE` and `REJECT` are replaced by `SUBMIT_AUTHORIZATION` (`supplychain.returns.authorize`, RowAction, not dangerous). The action is shown only with `platform.workflow.instances.start` as well. No new `supplychain.returns.*` key is added by this table. Approvers act in the Task Center, not on this page.
- **Shared file.** The consumer and the transport switch need the service `Program.cs` (single writer) and the broker configuration. The tenant's template seed is an operational step.

### 35.8 Binding to the MOD-0175 decision — open, written with Beste, part of DL-5

K2 fixes the rule: the quality decision stays in MOD-0175, the return's disposition follows it, and one set of goods gets one quality approval. **The binding is not specified here.** It is written together with Beste, and MOD-0175's pack is not changed by this revision. Until it is written, section "Transition action" keeps its `Received → Dispositioned` row and `dispositionCode` stays as today.

To settle together:

1. What moves a return to `Dispositioned`: an event from MOD-0175 (the contract's `x-events` lists only the consumed workflow event), or a read of the decision at transition time.
2. A return with several lines and several holds; a partial decision (`HoldDecision` 1 or 2 lines, Release / Reject / Block).
3. How `dispositionCode` is derived from the decision, and whether a user still enters it.
4. Lines `NotPosted` (`PRE_WP_P1`, `unpostedLines`): they have no hold and no MOD-0175 decision.
5. The Open-hold defect F-2 when a receipt is reversed.

### 35.9 Contract changes needed (SHIPMENT-BUNDLE; no contract file is touched here)

Additive, single writer MVP-6 CT; `ContractErrorCodeParityTests` pins new codes to `ContractErrorCodes`. **Items 1, 5 and 6
are SHIPMENT-BUNDLE 3.5.0** (Natig K1, 2026-11-03: `getReturn` joins 3.5.0; proposal
`docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.5.0-PROPOSAL.md`, patch `SHIPMENT-BUNDLE-3.5.0.patch` beside it).
The whole of 3.5.0 is applied by MOD-0183 DL-3 together with its code; MOD-0186 DL-3 and DL-4 start after it. `getReturn` is
declared with `x-served-from: MOD-0186 DL-4`, because the contract lands before DL-4 serves it. **Items 2–4 are DL-5 and are not
in 3.5.0:** they go in a later contract version opened together with DL-5, whose owner decisions are pending (OD-186-1,
OD-186-6). 3.5.0 adds no error code and no authorization field; nothing of DL-5 enters `ContractErrorCodes` before then.

| # | Change | For |
|---|---|---|
| 1 | New `getReturn` GET `/returns/{returnId}` (`ReturnDetail`): summary fields (with `customerRef`, item 6), `reasonCode` and `createdAt` (both on `ReturnDetail` only, needed by the 35.5 summary card: RB-8 and RB-9), `lines[]` (`shipmentLineNumber`, `quantity`, `uomId`, `inventoryTransactionReferenceId`, `inventoryPosting`, `inventoryPostingReason`); statuses 200, 404 | DL-4 (3.5.0) |
| 2 | New `submitReturnAuthorization` POST `/returns/{returnId}/submit-authorization`, `Idempotency-Key`; statuses 200, 400, 403, 404, 409, 422, 503 | DL-5 (later version, not 3.5.0) |
| 3 | `ReturnSummary` gains optional `authorizationState`, `lastAuthorizationOutcome`, `reasonCode`, `createdAt`; `getReturn` gains `authorizationState`, `lastAuthorizationOutcome`, `workflowInstanceId` | DL-5 (later version, not 3.5.0; `reasonCode` and `createdAt` are already on `ReturnDetail` from 3.5.0 by item 1, and go on the summary only if DL-5 needs them) |
| 4 | New codes `RETURN_APPROVAL_REQUIRED`, `RETURN_WORKFLOW_TEMPLATE_MISSING`, `RETURN_APPROVERS_NOT_CONFIGURED`, `RETURN_WORKFLOW_FORBIDDEN`, `RETURN_WORKFLOW_REJECTED`; `transitionReturn` description says Authorized / Rejected come only from workflow | DL-5 (later version, not 3.5.0) |
| 5 | Items 1–3 of MOD-0183 section "Contract changes needed": `search` on `queryShipments`, `shipToReference`, `warehouseReferenceId` and `customerRef` on `ShipmentSummary` | DL-3 precondition (3.5.0) |
| 6 | `ReturnSummary.customerRef`: read-only, the same `CustomerRef` schema as shipments (Account.Id, Guid "D", lower case, compared case-insensitively), copied by the service from the shipment at create (35.6); never in `CreateReturnCommand` | DL-4 (3.5.0; the field is optional, so it is valid while MOD-0186 does not fill it yet) |

Gateway: the route for `getReturn` and the submit route go to `integration-agent`. Today `/api/shipment-bundle/returns` and its transition route exist; no by-ID route is measured.

### 35.10 Audited Events

Written for DL-5, which adds write commands (AUD-001). DL-3 and DL-4 add none. `SupplyChainCentralAuditScope` is registered in the service's `Program.cs`; Path **b**. Existing commands use `SupplyChainCentralAudit.For("reverse-logistics", Create | LifecycleTransition, "ReturnOrder")`, and the new ones follow that form. The scope reads a `RequestContext` that only the request gate fills, so the consumer path must fill it, or no central row is written.

| Command | Event | Object | Path | Fields | Exception |
|---|---|---|---|---|---|
| `CreateReturnCommand` | `reverse-logistics` / Create | ReturnOrder | b | After: RmaNumber, ShipmentId, ReasonCode, line count | — |
| `TransitionReturnCommand` | `reverse-logistics` / LifecycleTransition | ReturnOrder | b | Before/After: Status; line posting results on Received | — |
| `SubmitReturnAuthorizationCommand` | `reverse-logistics` / LifecycleTransition | ReturnOrder | b | After: AuthorizationState, WorkflowInstanceId | — |
| `ApplyReturnAuthorizationOutcomeCommand` | `reverse-logistics` / LifecycleTransition | ReturnOrder | b | Before/After: Status, AuthorizationState; outcome, approver id | — |

Evidence references and the customer name are not written to the audit record.

### 35.11 Follow-ups and open decisions

- **SHP / RMA record codes:** `RMA-` + GUID today (`ReturnRepository.cs`, `49012bd7b`), against RCS-001. A separate FU after MOD-0040-FU02, which is not on `main` (`approval_status: pending` on `origin/chore/ct/mod0178-dl1-accept-01`). No identity is minted here. RCS-001's table has no `RMA` row.
- **OD-186-1** MOD-0175 binding (35.8): owner Natig and Beste; decided when DL-5 opens.
- **OD-186-2** Reason code list and its source (35.3): **decided** (Natig, 2026-11-03): closed module list; old returns as written.
- **OD-186-3** Four-eyes on return approval (35.7): **decided** (Natig, 2026-11-03): yes; an unreadable approver is not applied.
- **OD-186-4** Depth of the location picker (35.3): **closed.** Measured: `LocationRepository` filters `ParentId` by equality, `location.openapi.yaml` has no ancestor filter, K-01 and `MayBeStockable` allow stock on Bin and StorageLocation only. Solved by the tree picker, no contract change.
- **OD-186-5** PRODUCT-MASTER is a draft: **decided** (Natig, 2026-11-03): a draft read is accepted for display and selection only; field names are bound at Phase 1.5.
- **Customer identity:** **decided** (MVP-1 CT as MOD-0177 owner, 2026-11-03; accepted by Natig K1): the customer is `customerRef` on shipments and returns (SHIPMENT-BUNDLE 3.5.0), never `shipToReference`; no GUID on screen; old records shown separately; CRM read contractless and measured until CRM decides, nothing from CRM in logs. The switch to a contracted CRM endpoint is open, owner CRM / MOD-0149 owner (MOD-0183 section "Sender and recipient", open item 5).
- **OD-186-7** Customer choice on the return form: **open, owner MVP-1 CT.** Decision point 2 says the customer is chosen on the return form too. Default until MVP-1 answers: the return form has no customer picker; the customer is the chosen shipment's (shown by name and code in the shipment picker) and the service copies the shipment's `customerRef` onto the return (35.6). `CreateReturnCommand` gets no `customerRef`.
- **OD-186-8** Address in the customer picker list: **open, owner MVP-1 CT** (the same question as MOD-0183 OD-183-5). Default until MVP-1 answers: the list shows name and code, the address follows the choice (MOD-0183 24.1).
- **OD-186-6** Approver list (35.7): where the per-company list is kept and who edits it. A new settings surface, its permission key, manifest page and contract operations are not covered by K2. Decided when DL-5 opens.
- **Plan mapping:** the plan file is not in the repository; no plan identity is written for the new slices. Whether they add a new plan MVP (which would require an FU) is asked of the planning side.

### 35.12 Acceptance, tests and sabotage

Tests live in the owned files named in section "Owned UI paths" and `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/`, plus new `ReturnAuthorizationWorkflowTests.cs`, `ReturnDetailsReadTests.cs` and `frontend/Diten.Web.Tests/JavaScript/ReturnDetailsBehaviorTests.cs`. Each row gets a test on production code; a row with a sabotage is red when that sabotage is applied.

| AC | Measurable acceptance | Sabotage |
|---|---|---|
| RB-1 | Resolve projection carries `skuName` (`BulkSkuReadItem.name`), `skuCode` (`canonicalCode`) and lot number per line, for Item, Gsku, Lsku and FinishedGood lines; `itemId`, `skuId` never appear as a label | Dropping the projection fields turns it red |
| RB-2 | Name read fails or is denied → row kept, localized "name unavailable", create still works | Making the failure throw turns it red |
| RB-3 | Eligible-shipment picker lists only Delivered and Closed, finds a shipment beyond the first page by `search`, and draws the customer name and code from the summary's `customerRef` with no per-row `getShipment`; a shipment without `customerRef` shows "old record"; without `crm.account.read` it shows number and date; a stale response never fills a changed choice | Removing the status filter, or the stale guard, turns it red |
| RB-4 | List filter sends `shipmentId` from the picker; no UUID text input remains on list or form | Restoring the input fails `ReturnFormContractTests` |
| RB-5 | Reason select sends a listed code; 7 languages have every label; an old free-text return shows its stored text | Removing one language's key turns parity red |
| RB-6 | Received: warehouse and location pickers; location disabled until a warehouse is chosen and cleared on change; empty location sends nothing and stops at `receiptPlaceMissing`; the adapter calls `/api/locations/search`, an Inactive place is not offered, and against the real INVENTORY query a Bin under Zone under StorageLocation can be reached and chosen while the Zone cannot be chosen | Keeping the old location on warehouse change, or sending `isStockable=true` on the layers, turns it red |
| RB-7 | `RETURN_RECEIPT_IN_PROGRESS` fills the pickers from `details`, by name | Showing raw ids turns it red |
| RB-8 | `getReturn`: own company 200 with the fields of contract item 1, and no workflow field before DL-5; other tenant, other company, unknown → 404 with one text | Dropping the company predicate turns it red |
| RB-9 | Details page (DL-4 changes the assertion in `ReturnFormContractTests` that `Details.cshtml` does not exist): no `.read` → only `_AccessDenied` inside the shell; skeleton, empty and error are distinct; 390 / 768 / 1024 / 1440 without page overflow; Arabic RTL | Drawing the page before the permission check turns it red |
| RB-10 | KRN link lists `holdNumber` per line from `listHoldsBySource`; no `quarantine.holds.read` → block absent, no request; `NotPosted` line shows its reason | Sending the request without the permission turns it red |
| RB-11 | Submit: `Requested` → instance started, `PendingApproval`, status unchanged; replay starts no second instance | Removing the idempotent check starts two, red |
| RB-12 | No active template → 409 `RETURN_WORKFLOW_TEMPLATE_MISSING`; no candidate left, or Platform's `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED` → 409 `RETURN_APPROVERS_NOT_CONFIGURED`; Platform 403 → 403 `RETURN_WORKFLOW_FORBIDDEN`; workflow down → 503; no change in any. The opener and the submitter are never in the sent candidates | Falling back to direct authorization, or sending the opener as a candidate, turns it red |
| RB-13 | Approved outcome → `Authorized` once, `ReturnAuthorized` once, audit once; duplicate event → inbox stops it | Removing the inbox check turns it red |
| RB-14 | Rejected outcome → `Rejected`, entitlement released once. Cancelled or timed out → stays `Requested`, submit works again | Releasing on every delivery turns it red |
| RB-15 | Event with a foreign tenant, or an instance id that differs from the stored one, or a return not `PendingApproval` → ignored, no write | Removing the instance-id comparison turns it red |
| RB-16 | Lost event: a read by a user with `platform.workflow.instances.view` applies the outcome once, with one central audit row; a user without the key sends no request and the return stays `PendingApproval` | Removing the reconciliation path turns it red |
| RB-17 | `transitionReturn` to `Authorized` or `Rejected` → 422 `RETURN_APPROVAL_REQUIRED`, even with `.authorize` | Restoring the old arrow for `.authorize` turns it red |
| RB-18 | Real flow on the integrated target, consumer off and on: a user with `.authorize` + `platform.workflow.instances.start` submits, a different user with `platform.workflow.tasks.approve` sees the task in the Task Center and approves, the list shows `Authorized` (E4; no bridge class added) | — |
| RB-19 | Shipment dispatch has no workflow call (K2) | Adding a workflow client to `Features/Shipments` fails an architecture test |
| RB-20 | Audited Events table: one audit row per command in the same commit as the outcome; `.close` etc. unchanged | Moving the write outside the commit turns it red |
| RB-21 | Manifest. DL-4: `RETURN_DETAILS` page added, `AUTHORIZE` and `REJECT` still present. DL-5: `SUBMIT_AUTHORIZATION` replaces them. M-02…M-08 pass in both | Leaving `AUTHORIZE` in the manifest fails M-05 |
| RB-22 | Texts of every new state, picker, error and label exist in all 7 `ReturnsIndex` languages (parity by MATCH count); no hardcoded text | Removing a key from `ar` turns it red |
| RB-23 | Approver equals the opener, on the event and the reconciliation path → nothing moves, return `Requested`, `lastAuthorizationOutcome = FourEyesViolation`, entitlement unchanged, and a new submit starts `r{n+1}`. Approver unreadable → not applied, `PendingApproval`, applied on a later read once readable (OD-186-3) | Removing the approver comparison, or leaving the violation in `PendingApproval`, turns it red |
| RB-24 | **No GUID on screen.** Picker, list, QuickView and details hold no visible text, option label, tooltip or `title` with `customerRef` or any account id; a failed or denied customer name read shows only the localized text | Rendering the id as the picker's option text, or in the failure cell, turns it red |
| RB-25 | **Old records shown separately (MOD-0183 SB-19).** A shipment or return without `customerRef` shows "old record" and, on details, the shipment's `shipToReference` as stored; no CRM call and no match on `shipToReference` links it to an account | Adding a name lookup from `shipToReference` turns it red |
| RB-26 | **Nothing from CRM in logs.** With a capturing logger on the Returns adapters that resolve the customer, no log line holds the account name, code, any address field, person, phone or e-mail | Adding a log line with the CRM response turns it red |
| RB-27 | **Customer copied, never sent (DL-4).** A return created for a shipment with `customerRef` returns the same value read-only in the create answer, the list and `getReturn`; `CreateReturnCommand` has no `customerRef` (it stays `additionalProperties: false`), and a value sent anyway is never stored; a shipment without `customerRef` gives a null; returns created earlier stay null; the service makes no CRM call | Taking the value from the request, or dropping the copy, turns it red |

Also: `verify_datatable_page.py --reference slim` is run and recorded (RU-SCR-06 stands); the full SupplyChain and Web test assemblies run unfiltered; one real-browser pass on the integrated target with the tour's permission set plus `trace.lots.read`, `inventory.locations.read`, `crm.account.read`, `quarantine.holds.read`, and for DL-5 the three workflow keys of 35.7. The MOD-0175 binding (35.8) has no acceptance row until it is written.

### 35.13 Revision r10 — what changed and why

Cause: the MVP-1 CT's answer as MOD-0177 owner on customer identity (2026-11-03), accepted by Natig (K1) the same day,
recorded in the decision record's section "2026-11-03 — MVP-1 CT answer on customer identity (MOD-0177)". The customer is
carried in a separate `customerRef` on shipments and returns (SHIPMENT-BUNDLE 3.5.0), never in `shipToReference`. r9
(`df328109…`) passed its last narrow review (`docs/records/reviews/MOD-0186/2026-11-03-df328109.md`, UYGUN); none of its
closed findings is reopened: DL-5 stays outside this approval, the workflow fields stay in item 3 / DL-5, and the location
tree picker stays as written.

| Place | r9 | r10 |
|---|---|---|
| Front matter `status_note` | r9 draft | r10 draft; `approved_by` not set |
| 35.2 permissions bullet | customer name | customer name and code from `customerRef`; never the id |
| 35.3 Shipment picker row and bullets | customer name; summary lacks `shipToReference` | customer from `ShipmentSummary.customerRef`; "old record" for shipments without it; item 5 = 3.5.0 via MOD-0183 DL-3; `search` does not match names |
| 35.5 Summary card | customer name | name and code from the return's `customerRef`, then the shipment's; "old record" otherwise; no id in the failure cell |
| 35.6 Read model | `getReturn` only | the service copies the shipment's `customerRef` onto the return at create; read-only; no back-fill; reader tolerance measured |
| 35.9 Contract changes | item 5 = `search`, `shipToReference`, `warehouseReferenceId`; all items in "the next version after 3.4.0" | item 5 adds `customerRef`; new item 6 `ReturnSummary.customerRef`; items 1, 5 and 6 are SHIPMENT-BUNDLE 3.5.0, landed with MOD-0183 DL-3 (Natig K1, 2026-11-03: `getReturn` only; DL-5 surface kept out); `reasonCode`, `createdAt` on `ReturnDetail` only; items 2–4 in a later version opened with DL-5 |
| §25 owned files | §34 additions only | new "§35 DL-3 and DL-4 additions" list (Natig K1 decision 6, 2026-11-03) |
| 35.11 | — | customer-identity decision line; OD-186-7 (customer choice on the return form) and OD-186-8 (address in the picker list) open with MVP-1 CT, current behaviour as default |
| 35.12 | RB-3 | RB-3 reworded; new RB-24 (no GUID on screen), RB-25 (old records shown separately), RB-26 (nothing from CRM in logs), RB-27 (customer copied, never sent) |
| Slice note | DL-3 needs MOD-0183 items 1 and 2 | DL-3 needs MOD-0183 items 1–3 (3.5.0); DL-4 needs items 1 and 6 and starts after MOD-0183 DL-3; DL-5 needs items 2–4 from a later contract version; new rows assigned |

The two change sets of r10 (customer identity; then Natig K1's four decisions of 2026-11-03 on the open questions) are one draft revision, not reviewed in between. The slice table, every YAML block, §1–§24, §26–§34, 35.1, 35.4, 35.7, 35.8 and 35.10 are unchanged. Scope stays DL-3 and DL-4; DL-5 stays
outside this approval.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | iade → karantina | Exact prospective DEV owned files and shared exception proposal, Customer return to Inventory, What is posted, Receipt attempt and failure behaviour, Return acceptance | önceki sürümde | plan kimliği yok | plan satırı yok |
| DL-2 | ret metinleri | Receipt attempt and failure behaviour, Return acceptance | önceki sürümde | plan kimliği yok | plan satırı yok |
| DL-3 | iade seçicileri ve satır görünümü | Screen slice B and return approval: Line fields of the return screens, Pickers, Form shape | E3 | plan kimliği yok | plan satırı yok |
| DL-4 | iade ayrıntı sayfası ve KRN bağı | Return details page and quarantine link, Read model, Contract changes needed | E4 | plan kimliği yok | plan satırı yok |
| DL-5 | iade onayı iş akışı (K2); taslak, henüz yürürlükte değil | Authorization through Platform Workflow, Contract changes needed, Audited Events | E4 | plan kimliği yok | plan satırı yok |

DL-1 covers R34-1, R34-2, R34-4…R34-17 and the non-text part of R34-3; DL-2 covers the R34-3 texts. DL-1 is dispatchable only
after MOD-0183 §23 DL-1 is on `main`, because §34.1 reads the completed attempt's per-line issue (`issueTransactionId`,
`lotId`) that MOD-0183 stores and SHIPMENT-BUNDLE 3.4.0 exposes. CT-APR-01 note: "only DL-1, DL-2; after MOD-0183 DL-1".

DL-3 covers RB-1…RB-7, the picker part of RB-24…RB-26 and the matching part of RB-22; it needs contract item 5 (MOD-0183 contract items 1–3, SHIPMENT-BUNDLE 3.5.0, applied by MOD-0183 DL-3) first. DL-4 covers RB-8…RB-10, RB-27, the list and details part of RB-24…RB-26, the DL-4 part of RB-21 and the matching part of RB-22, and needs contract items 1 and 6 (SHIPMENT-BUNDLE 3.5.0, landed with MOD-0183 DL-3) and the Gateway route of item 1; it starts after MOD-0183 DL-3. DL-5 covers RB-11…RB-20, the DL-5 part of RB-21, RB-23, needs contract items 2–4 (a later contract version opened with DL-5, not 3.5.0), and opens only after OD-186-1 and OD-186-6 are decided, with its own CT-APR-02 and CT-APR-01. All three follow slice A (#355) and need no MOD-0183 §23 change; the customer name in DL-3 and DL-4 needs MOD-0183 DL-3. Until DL-5 ships, return approval runs through the existing `.authorize` transition and K2 is not met live. CT-APR-01 note for r10: "only DL-3, DL-4"; evidence levels are proposals.
