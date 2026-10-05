---
id: MOD-0185
name: Routing & Load Planning
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "2026-09-18 explicit user Phase1.5, promotion and bounded mock-first DEV approval; effective section28 supersedes historical proposals. Pending-only outbox; no operational rollout. Tenant UI scope (list + create) owner-approved 2026-10-04 (section 30); UI DEV released to R-4b; independent UI VER open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 7
---

# MOD-0185 — Routing & Load Planning

> **Effective authority: §28.** Historical draft/HELD statements below are retained as design history and superseded for the approved bounded scope. Published SHIPMENT-BUNDLE2.0.0 + Loads annex are frozen authority; no contract edits in DEV.

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0185 --name "Routing & Load Planning"`
> returned `OK` again on 2026-09-17 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** `status: draft`; no runtime code, scaffold or dispatch is authorized.

## 1. Module Summary

MOD-0185 owns route stops and load-plan lifecycle that groups MOD-0183 shipments under a MOD-0184 carrier.
Its first prospective slice is backend/contract only and conforms to `SHIPMENT-BUNDLE` v1.

## 2. Ownership and Boundaries

**Owns:** load identity/number, carrier and shipment references, transport mode, ordered stops, planned departure and
Draft→Planned→Tendered→Accepted→Dispatched→Completed lifecycle.

**Consumes:** MOD-0184 carrier; MOD-0183 shipment references; existing frozen **WAREHOUSE-OUTBOUND** as upstream context only (see §21; no direct receiving/ingress authority).

**Does not own:** Carrier/Shipment aggregates, warehouse pick/pack/trigger, inventory truth, POD, return or claim.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `LoadPlan` | Shipment grouping, carrier, mode and current lifecycle |
| `LoadStop` | Ordered pickup/delivery/return reference |
| Commands | Create load and transition lifecycle |
| Queries | List load plans; no by-ID GET in frozen contract |
| Events | Load created/planned/tendered/accepted/dispatched/completed/cancelled |

## 4. Entity Fields

`LoadPlan` carries server UUID, server-resolved TenantId/LegalEntityId, unique `LoadNumber`, required `CarrierId`,
one or more `ShipmentIds`, `Mode`, UTC `PlannedDepartAt`, at least two ordered stops, status/version and audit fields.
`LoadStop` contains positive unique sequence, opaque location reference and Pickup/Delivery/Return action.

## 5. Repo Scope

The exact prospective file allowlist is §25. No broad service/features wildcard, generated documentation
or shared composition edit is authorized. This PREP lane edits this pack only for this module.
Frozen contracts remain read-only. Future DEV requires explicit pack and shared-file release.

## 6. Protected Paths

- `.antigravity/**`, gateway routes, shared registrations, archive/frontend shells.
- Carrier and Shipment persistence/features and every other domain service.
- Frozen contracts and central Warehouse/Supplier contracts.
- Any inventory balance, reservation or warehouse trigger implementation.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| MOD-0184 Carrier | Required before runtime | Carrier must exist and be Active; contract/reference only |
| MOD-0183 Shipment | Required reference | Eligible shipment states only; no shipment SoR mutation |
| `SHIPMENT-BUNDLE` v1 | Frozen | Load schema, transitions, event and mock authority |
| Warehouse outbound | Frozen read-only upstream seam exists | No inbound return, receiving or automatic ingress implied; see §21 |

## 8. Runtime Constraints

Prospective runtime is tenant/legal-entity scoped, idempotent and event-backed. UUID correlation propagates unchanged
from command to load lifecycle events. Terminal Completed/Cancelled loads cannot transition. This draft grants no
runtime code authority.

## 9. Layout & Shell Contract

`shell: none`; no frontend or DataTable work.

## 10. Backend File Convention

Prospective root: `Features/Loads/` with the standard Commands, Queries, separated handler folders, Validators and
`LoadModels.cs`. Repository and outbox logic must preserve tenant predicates and atomic transitions.

## 11. Frontend File Contract

Not applicable; any planning UI requires a separate approved revision and golden reference decision.

## 12. Validation Rules

Historical draft business proposals below; exact frozen schema and unresolved approvals are distinguished in §§22–24.

- Carrier reference is required and must resolve to Active MOD-0184 carrier.
- Shipment IDs are unique/non-empty and resolve through MOD-0183 without internal DB sharing.
- Stop sequence is unique, contiguous and starts at 1; at least one Pickup and one Delivery.
- Lifecycle accepts only the transition matrix frozen in `SHIPMENT-BUNDLE` v1.
- All mutations require correlation/idempotency headers; tenant/legal entity never comes from payload.

## 13. Failure Path to Verify

Unknown/inactive carrier, duplicate shipment assignment, invalid stop order and invalid lifecycle return deterministic
errors with correlation UUID and no partial writes/events; cross-scope resources return 404.

## 14. Authorization Convention

Prospective permissions: `supplychain.loads.read`, `.create`, `.transition`. Shared registration is integrator-owned.

## 15. Gateway / API Routing Decision

Use the `SHIPMENT-BUNDLE` `/loads**` surface. Gateway changes belong only to a later integration-agent WP.

## 16. Acceptance Criteria

- [ ] No runtime code exists while this pack is `draft`.
- [ ] Future implementation consumes verified MOD-0184 and MOD-0183 seams by contract.
- [ ] Load commands/responses/events match frozen v1 examples and schemas.
- [ ] Invalid transitions and duplicate assignment are atomic failures.
- [ ] Correlation UUID is preserved from command through all load events.

## 17. Test Expectations

DCP-002 PASS, OpenAPI/mock validation, route/stop invariants, lifecycle matrix, idempotency, tenant isolation, RBAC,
outbox atomicity and dependency contract tests with MOD-0184/MOD-0183 mocks.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] Frozen contract and MOD-0184/MOD-0183 dependency edges are explicit.
- [x] Backend-only slice and paths are bounded.
- [ ] MOD-0184 executable dependency gate is satisfied.
- [ ] Owner changed this draft to `ready-for-dev`.

## 19. Implementation Notes

**ASSUMPTION:** route optimization is deterministic ordered-stop planning in v1; maps, pricing, tender marketplace and
external optimization engines are follow-ups. This avoids inventing an external carrier/warehouse contract.

## 20. Follow-up Items

- Wait for MOD-0184 approval and executable seam before MOD-0185 runtime dispatch.
- Historical trigger-absence wording is superseded by §21: outbound exists; live ingress remains separate.
- Separate integration WP assigns load reference into Shipment and wires gateway/events.


## 21. NEXT-PREP-01 v1.0 — current authority, bounded outcome and consumed identities

This refinement supersedes imprecise scope/readiness/default claims in §§1–20. Earlier business rules
are draft proposals, not proof of frozen wire semantics. Status remains draft. No runtime file is writable
in this PREP lane; only this pack is modified for this module. Preparation is serial 0185→0186→0187.

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
| `queryLoads` | GET `/loads` | 200 |
| `createLoadPlan` | POST `/loads` | 201, 404, 409, 422 |
| `transitionLoad` | POST `/loads/{loadId}/transition` | 200, 404, 422 |


Exact GET query parameters (all optional; not nullable unless specified):

- `status`: `{"$ref":"#/components/schemas/LoadStatus"}`.
- `carrierId`: `{"type":"string","format":"uuid"}`.

All command/nested input objects listed below have additionalProperties:false. Required absence and null
are distinct. No extra TenantId/LegalEntityId/version/audit/actor field is accepted.

| Schema.field | Required | Exact schema (not an invented validator limit) |
|---|---|---|
| `CreateLoadCommand.carrierId` | yes | `{"type":"string","format":"uuid"}` |
| `CreateLoadCommand.shipmentIds` | yes | `{"type":"array","minItems":1,"items":{"type":"string","format":"uuid"}}` |
| `CreateLoadCommand.mode` | yes | `{"$ref":"#/components/schemas/TransportMode"}` |
| `CreateLoadCommand.plannedDepartAt` | yes | `{"type":"string","format":"date-time"}` |
| `CreateLoadCommand.stops` | yes | `{"type":"array","minItems":2,"items":{"$ref":"#/components/schemas/LoadStop"}}` |
| `TransitionLoadCommand.targetStatus` | yes | `{"$ref":"#/components/schemas/LoadStatus"}` |
| `TransitionLoadCommand.occurredAt` | yes | `{"type":"string","format":"date-time"}` |
| `TransitionLoadCommand.note` | no | `{"type":["string","null"]}` |
| `LoadStop.sequence` | yes | `{"type":"integer","minimum":1}` |
| `LoadStop.locationReferenceId` | yes | `{"type":"string"}` |
| `LoadStop.action` | yes | `{"type":"string","enum":["Pickup","Delivery","Return"]}` |

`LoadStatus`, `TransportMode` and `Decimal` use frozen enums/patterns. Decimal is a **string** matching
`^-?\d+(\.\d+)?$`; it admits signed/zero values and gives no precision/scale cap. Positive quantity/amount
is a draft business condition (§24), not a JSON-schema fact. No binary float, rounding or invented length cap.
Optional string|null fields permit absence/null/empty unless an explicitly approved business decision says otherwise.
Optional arrays allow omission but **not null** and have no minItems/uniqueItems unless specified above.
Required string without minLength permits empty text; no blanket NotEmpty/trim rule.
Date-time accepts valid offsets, not only literal Z; UTC storage must preserve the instant. Past/future/time-order
restrictions are not in schema. UUID nil rejection and array duplicate rules cannot be copied from other modules.

Responses: `LoadListResponse` requires items,total,contractVersion. `LoadSummary` has **no required list**;
listed non-null property types do not imply their presence is schema-required. The implementation proposal
emits all summary properties for useful reloads, without rewriting the contract. `LoadResponse` must follow
its exact schema, including allOf where used. No outer data envelope; internal Response<T> is adapted at API.

Exact response schema: `{"type":"object","required":["loadId","loadNumber","status","idempotentReplay","contractVersion"],"properties":{"loadId":{"type":"string","format":"uuid"},"loadNumber":{"type":"string"},"status":{"$ref":"#/components/schemas/LoadStatus"},"idempotentReplay":{"type":"boolean"},"contractVersion":{"$ref":"#/components/schemas/ContractVersion"}}}`.

Frozen transition description (authority, no extra arrows):

> Izinli gecisler: Draft->Planned|Cancelled; Planned->Tendered|Cancelled; Tendered->Accepted|Cancelled; Accepted->Dispatched|Cancelled; Dispatched->Completed. Diger gecisler INVALID_LOAD_TRANSITION ile reddedilir.

Create success starts **Draft**. Same-state/new-key and all non-listed arrows are invalid; terminal
states have no outgoing arrow. Current-state check must be atomic; no request Version/If-Match exists.
Exact replay precedes new transition evaluation only after current auth/scope/schema checks, subject to
owner resolution of the replay policy below. List filtering uses only frozen parameters, not an invented detail query.

## 23. Errors, security, headers and replay — covered vs unresolved

Policy proposal: `[Authorize]` plus server-side `[HasPermission]`; actor tenant_user in validated tenant/LE context.
`supplychain.loads.read` for GET, `supplychain.loads.create` for create and `supplychain.loads.transition` for each transition (existing draft proposal; no shared catalog write).
No permission inferred from a valid token alone; test scoped actors independently from platform bypass.

| Case | Frozen fact / bounded requirement | Exact unresolved decision / acceptance oracle |
|---|---|---|
| Headers | GET and both POST require UUID X-Correlation-Id; POST key is string length1..128 | Nil UUID, duplicate header, parser/whitespace behavior, validation/auth ordering and fallback trace must be owner-bound for this family; no Carrier-default import |
| JWT + scope | Bearer; server-resolved tenant/LE; global JWT/header matching; no payload-selected scope | Security owner binds trusted claim names, missing/duplicate claim handling, scope-header validation and precedence. Test two tenants×two LEs and missing permission; all lookups fail closed |
| Missing/cross-scope target | POST create/transition declare404 NotFound; list declares200 only | Target code `LOAD_NOT_FOUND` exists in shared examples; reference-specific404 mapping/disclosure must be chosen; list no-leak filtering required |
| Schema / auth / persistence failure | Shared Error requires code/message/correlationId plus contractVersion:v1, details optional | GET lacks declared errors; POST lacks400/401/403/5xx (and415). Owner supplies exact status/code/header matrix and decides clarification vs versioned amendment before DEV |
| Duplicate / changed payload | Create declares409 Conflict; shared IDEMPOTENCY_KEY_REUSED example | Transition **does not declare409**; cannot substitute422 to conceal it or silently add409. Business duplicate code/error is separately unresolved |
| Lifecycle | Transition422 + INVALID_LOAD_TRANSITION frozen | Verify all source×target pairs and zero partial writes/events; create422 examples from other modules are not local business scenarios |
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
**Reviewable first bounded DEV scope:** create Draft LoadPlan, list by status/carrierId and all frozen
transitions; scoped published Shipment/Carrier read adapters; immutable source-reference snapshot,
assignment constraint, local lifecycle audit/receipt/outbox. No route optimization engine, tender transport,
Shipment write or automatic dispatch. No separate Carrier/Supplier master. Active Carrier is the existing pack
business requirement; Carrier `GET /carriers?status=Active` is the only frozen read seam (no getCarrierById).
Resolve carrierId from that scoped list, verify mode against supportedModes; no database/internal-type sharing.
Shipment has GET `/shipments/{shipmentId}` and list; read the authoritative detail for every selected ID.
List absence/ambiguous ID, malformed data or dependency failure must not become a fabricated Active reference.

| Decision ID / accountable owner | Exact decision still required before runtime | Acceptance once bound |
|---|---|---|
| D185-01 business + contract | Define eligible Shipment status set (draft pack says only "eligible"); whether existing carrierId must match, mode compatibility, duplicate IDs and repeated stop sequences; confirm contiguous sequence1..N and Pickup+Delivery requirement beyond schema | Enumerate every ShipmentStatus accepted/rejected; mismatched carrier/mode; two same IDs; missing Pickup/Delivery; no local write on reject |
| D185-02 business + data | At what transitions recheck Carrier Active/Shipment eligibility? Authoritative read-at-command snapshot vs stronger guarantee: published seams have no lease/version compare API | Mock changes Active→Suspended or Shipment status between read/commit; result matches approved snapshot semantics. Cannot promise cross-aggregate serializability with HTTP GET alone |
| D185-03 business + data | Define assignment reservation state set, release on Cancelled, Completed and soft-delete; reassign/cancel races. No Shipment mutation is allowed | Two different keys/loads claim same shipment concurrently: at most one blocking assignment; loser approved conflict; lifecycle/release and reservation commit atomically |
| D185-04 contract/security | Resolve §23 full operation error/header/replay matrix, root propagation, remote403/404/timeout mapping, and action permissions | Recorded fixtures and real negative HTTP tests; transition409 amendment if chosen; no Carrier semantics leakage |
| D185-05 data/CT | Approve internal uniqueness for loadNumber, generator/retry, same-scope indexes and specific repository transaction; root event time/causation policy and local outbox with no publisher | Unique number collision retries without partial state; exactly one correct envelope per successful state change |

Proposed assignment design for D185-03: one load-owned constraint record keyed by tenant+LE+shipmentId
for the approved blocking states, in the same Mongo transaction as LoadPlan/receipt/audit/outbox.
This record is not stock, a Shipment shadow master or availability projection. No quantities or inventory
reservation are stored. A pre-check alone is insufficient; unique index/write-conflict arbitration is required.
Completed assignment history and immutable idempotency history are retained; release eligibility is not assumed.
Local transaction does not atomically freeze a remote Carrier or Shipment. Shipment.loadId/carrierId changes,
if later desired, require a separate approved integration contract/WP; they are not hidden in createLoadPlan.

## 25. Exact prospective DEV owned files and shared exception proposal

Effective DEV allowlist is approved by the user on 2026-09-18, limited by §28. Prefix S expands literally to
`services/Diten.SupplyChainService/src/Diten.SupplyChainService`.
No parent wildcard or another feature is writable. Only the following module paths are proposed:

`S.Domain/Features/Loads/` exact files:

- `LoadPlan.cs`
- `LoadStatus.cs`
- `LoadScope.cs`
- `LoadLifecycle.cs`
- `LoadAuditEntry.cs`
- `LoadMutationResult.cs`
- `ILoadRepository.cs`
- `LoadStop.cs`
- `LoadAssignment.cs`

`S.Application/Features/Loads/` exact files:

- `Commands/CreateLoadCommand.cs`
- `Commands/TransitionLoadCommand.cs`
- `Queries/GetLoadListQuery.cs`
- `Handlers/CommandHandlers/CreateLoadHandler.cs`
- `Handlers/CommandHandlers/TransitionLoadHandler.cs`
- `Handlers/QueryHandlers/GetLoadListHandler.cs`
- `Validators/CreateLoadValidator.cs`
- `Validators/TransitionLoadValidator.cs`
- `Validators/GetLoadListValidator.cs`
- `LoadModels.cs`
- `LoadRequestContext.cs`
- `LoadRequestFingerprint.cs`
- `ILoadReferenceReader.cs`
- `LoadReferenceSnapshot.cs`

`S.Api/Features/Loads/` exact files:

- `LoadsController.cs`
- `LoadContextMiddleware.cs`
- `LoadContractError.cs`

`S.Persistence/Features/Loads/` exact files:

- `LoadRepository.cs`
- `LoadSchema.cs`
- `LoadPersistenceRegistration.cs`
- `LoadOutboxStore.cs`
- `ILoadCommitProbe.cs`
- `NoOpLoadCommitProbe.cs`

`S.Infrastructure/Features/Loads/` exact files:

- `LoadReferenceReader.cs`
- `LoadPermissions.cs`
- `LoadPermissionAttribute.cs`
- `LoadOutboxWorker.cs` is **excluded**: no worker/publisher is authorized; no such file is to be created.

Tests prefix `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Loads/`:
`LoadContractTests.cs`, `LoadLifecycleTests.cs`, `LoadIsolationTests.cs`, `LoadReplayTests.cs`,
`LoadConcurrencyTests.cs`, `LoadAtomicityTests.cs`, `LoadReferenceTests.cs`.
Probes prefix `services/Diten.SupplyChainService/tests/loads/`:
`runtime_probe.py`, `restart_probe.py`, `verify_evidence.py`.
No other test or historical evidence edits. DEV/VER report destinations must be separately assigned by CT;
this allowlist grants no new docs record writes. New files beyond this list require versioned scope review.

Existing shared file requiring **separate explicit authorization and single writer**:
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`.
Approved: register this module's scoped context/reference client and owned Persistence registration;
exact family-only middleware/model-error adapter. No outbox worker/publisher registration. Preserve existing Carrier branch and exclude
only approved module routes from broad Shipment middleware; never use naive prefix matching (e.g. loadsXYZ).
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
by this module: `loads`, `loads_receipts`, `loads_audit`, `loads_outbox`;
additional `load_assignments` for the approved constraint, never inventory reservations.
scope tenant+LE on entity/receipt/outbox/audit reads and mutations; soft-deleted aggregates excluded from
normal reads, no delete API. Internal Version CAS/retry revalidates state; no new client version/header.
Unique scoped business number, receipt identity and eventId indexes; assignment/entitlement unique identity
where applicable. Number format/generation and retention are owner decisions, examples are not sequence specs.

Successful commit = aggregate + receipt + audit + lifecycle event outbox (+ constraint record where applicable).
Injected failure at each boundary rolls back all; committed response loss replays without another event.
Unknown commit resolves by durable receipt lookup, never blind insert. Success replay must retain original
result even after subsequent state changes; exact transport/correlation decisions still need §23 approval.

Envelope: LifecycleEventEnvelope with eventId, eventType, occurredAt, correlationId, aggregateType=`Load`,
aggregateId, payload=`LoadEventPayload`, contractVersion=v1; optional nullable causationId.
Only frozen enum events: LoadCreated, LoadPlanned, LoadTendered, LoadAccepted, LoadDispatched, LoadCompleted, LoadCancelled. Initial event and each permitted target event map exactly to lifecycle; no duplicate event on replay.
Event root must preserve first command UUID. occurredAt on transitions comes from validated command; creation
server time/causation choice needs explicit policy. Local pending outbox can be restart-tested; no registration
as the shared IEventOutboxStore that would replace Shipment's store. Delivery semantics/transport/dedup at
remote consumer remain separate integration decisions. No live publish or E5 claim in bounded mock scope.

| AC | Future measurable acceptance / failure test |
|---|---|
| A01 wire | Exactly three listed routes, exact frozen request fields/nullability and responses; all inline examples validated. No by-ID/update/delete endpoints. Compare version1.1.0 metadata vs wire v1 |
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


## 28. Effective bounded Phase1.5 / ready-for-dev release — 2026-09-18

Authority: user's explicit current approval of bounded Phase1.5, pack promotion, §25 Loads files,
specific transactional repository and minimal Loads-only Program.cs composition; development against
frozen mocks and isolated test MongoDB. This supersedes draft/HELD/open-decision language in §§1–27.
D18501–05 and R185-C01–05 remain approved. Exact release and canonical uptake were completed in
`docs/records/audits/2026-09/mvp6-loads-r2-publication-2026-09-18/README.md`.

Frozen canonical YAML info.version2.0.0, wire contractVersionv1:
SHA25693c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571.
Loads annex SHA256a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1.
The annex's preserved candidate/publication-status preamble is historical; the exact publication record
and current user approval govern release status. Normative business/security behavior is unchanged.
Carrier annex remains87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee.
No frozen artifact may be edited by DEV. Static uptake does not prove runtime enforcement.

**Exact implemented target:** queryLoads/createLoadPlan/transitionLoad only; GET/POST loads and
POST loads/{loadId}/transition. No public by-ID/edit/delete/bulk/search endpoint. §22 fields/schemas
remain; operation error/header/root/replay matrix is the published Loads annex, not historical §23 gaps.
Fresh create and transitions to Planned/Tendered/Accepted/Dispatched read scoped Carrier Active list and
Shipment details through published HTTP mocks. Complete Carrier validation; Shipment optional decision
fields absent→503, malformed→502. Immutable timestamped reference snapshots; no cross-aggregate lock claim.
Create Draft; all frozen arrows only; assignment retained through Completed/soft-delete, released only on
Cancelled with old-owner conditional predicate. Receipt root checked before fingerprint; fresh transition
root immutable; historical replay skips dependencies. All strings/arrays, null equivalence and UUID/instant
normalization follow annex exactly. Server number LOAD- plus uppercase UUID N; bounded3 true-collision attempts.

**Persistence:** approved specific transactional repository, scoped loads/load_assignments/loads_receipts/
loads_audit/loads_outbox; atomic aggregate+constraint+receipt+audit+one lifecycle event. Replica-set transactions,
version/current-state retry and durable receipt recovery. Source Carrier/Shipment objects and DBs not shared.
Outbox status only Pending, including after restart. LoadOutboxStore may persist/query owned pending entries;
no LoadOutboxWorker, hosted publishing task, shared IEventOutboxStore replacement, delivery ACK or live transport.

**Single writer:** lane AL-MVP6-MOD0185-DEV01, sole runtime writer assigned through DEV prompt v1.0.
§25 exact filenames only, excluding LoadOutboxWorker.cs. Program.cs only adds Loads context/client/persistence,
Loads family invalid-model mapping and exact family middleware exclusion from broad Shipment middleware.
Carrier branch, Shipment worker, health/JWT/CORS/global errors remain unchanged. No project/reference/config
file edits; use module-owned configuration binding. Any required expansion stops affected work for scope review.
DEV evidence may be added only under docs/records/audits/2026-09/mod-0185-dev-01/.
DEV API notes may be placed there; shared service README remains protected. UI/manual/browser gates N/A.

### Approved Phase1.5 checklist

| # | Approved plan |
|---|---|
|1|Entity: Id, TenantId, LegalEntityId, base audit/soft-delete/Version; LoadNumber, CarrierId, ShipmentIds, Mode, PlannedDepartAt, Stops; Status, immutable CorrelationRoot, previous eventId; scoped reference snapshots/audit/receipt/pending event and assignment records. All business fields retained.|
|2|Frozen DTO names unchanged; internal operational fields never leak into wire. No invented masters.|
|3|§25 LoadRepository.cs: tenant+LE on all reads/writes; IsDeleted=false for fresh aggregate operations; authorized historical receipt replay before fresh target lookup; unique scoped constraints and conditional old-load release.|
|4|Domain/Features/Loads/LoadPlan.cs inherits existing EntityBase; tenant-owned, no GlobalEntity.|
|5|§25 separate Commands/Queries/Handlers/Validators; existing MediatR and four pipeline behaviors reused. Specific repository explicitly approved.|
|6|N/A shell:none, form_field_count0, golden_reference:none.|
|7|N/A no UI/Razor/details/forms.|
|8|§22 exact required/nullability + published2.0.0 operation schemas; no NotEmpty tightening for allowed empty strings; optional note missing/null equal, empty distinct. Web/tracker N/A.|
|9|No Platform lookup; only frozen enums and approved Carrier/Shipment mock HTTP reads.|

**DoR:** DCP002 fresh PASS2026-09-18; existing domain/DCP009 ownership; user-approved pack and Phase1.5;
canonical publication/binding/uptake verified; bounded dependencies have Carrier/Shipment independent runtime
evidence and user authorizes mock-first DEV. Carrier CT recommendation is not silently changed into CT ACCEPTED.
Branch feature/mvp6-logistics HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c; fresh baseline in dispatch record.
A01–A12 apply with current architecture50/3 baseline and Pending-only outbox. Agent PASS is not CT acceptance.

ASSUMPTION-185-DEV-A: isolated fixture execution is not operational rollout. Operational data inventory,
existing-data/migration decision remains a separate pre-rollout gate; no operational connection/migration.
ASSUMPTION-185-DEV-B: module-owned reference base URL configuration is explicit and fixture-configured;
missing/unreachable endpoint fails closed. No implicit live host, token fabrication or service-DB fallback.

Versioned DEV: docs/roadmap/plans/mod-0185-dev-01-prompt-v1.0.md (READY by current user grant).
Versioned VER: docs/roadmap/plans/mod-0185-ver-01-prompt-v1.0.md (HELD until completed DEV handoff).
Protected: frozen contracts/17seals/authority, other modules/source/tests, gateway, UI, shared catalogs,
DCP/registry/board and .antigravity. No commit/push/stash. No E5/G5 or integrated consumer acceptance.

## 29. Self-registration — after Loads UI approval

Approved: `docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A; pack preparation only). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md).
**Foundation:** DCP-009 §21 (interface, hosted service, options, `PlatformRegistration` settings via the existing `X-Internal-Api-Key`, project reference and `Program.cs` lines — single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

> **After Loads UI approval.** Nothing in this section is actionable until the Loads UI scope (`mvp6-loads-ui-scope-01`, HELD) is approved by the owner and built. Until then no provider, no `AddSingleton` line and no `Nav.Module.ROUTINGLOADPLANNING` / `Nav.Page.LOADS` values ship.
>
> **Disposition 2026-10-04:** the scope was owner-approved (§30) and the UI built by R-4b; this section shipped with it (provider,
> `AddSingleton`, `Nav.Module.ROUTINGLOADPLANNING` / `Nav.Page.LOADS` in seven languages).

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `routing-load-planning` / `RoutingLoadPlanning` / `Routing & Load Planning` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, seed-once) | 410 / `bx-map-alt` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/RoutingLoadPlanningManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/RoutingLoadPlanningManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `LOADS` | Loads | `/SupplyChain/Loads` (proposed; not a route yet) | `supplychain.loads.read` | — | **true** | List | 10 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|---|
| `LOADS` | `CREATE` | Create | `supplychain.loads.create` | Toolbar | no |
| `LOADS` | `CHANGE_STATUS` | Change Load Status | `supplychain.loads.transition` | RowAction | no |

All keys are existing constants in `Infrastructure/Features/Loads/LoadPermissions.cs`. **Amended 2026-10-04 (R-4b):** `supplychain.loads.transition`
was "not modeled" here because the UI scope has no transition button (ROOT-UI-01). But `LoadsController.Transition` enforces it, and
Platform syncs to Auth only declared keys, so an undeclared key could never be granted and that endpoint would answer 403 to every
caller — the defect R-2 measured for Returns (`docs/records/audits/2026-10/mvp6-r2-returns-ui-01/` F-R2-1) and the R-3 guard
(`EnforcedPermissionDeclarationGuardTests`) rejects. It is therefore declared as `CHANGE_STATUS`. This UI slice still renders no
transition control; the declaration makes the key holdable, not a button.

### Navigation keys (NAV-L10N-KEYS.tsv; 0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Module.ROUTINGLOADPLANNING` | en, tr, fr, es, zh, ar, ru | this provider, only after the Loads UI is approved and built |
| `Nav.Page.LOADS` | en, tr, fr, es, zh, ar, ru | this provider, only after the Loads UI is approved and built |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `LoadPermissions` |
| M-03 | Every reflected key is in the manifest (amended 2026-10-04: the allow-list is empty; `supplychain.loads.transition` is the `CHANGE_STATUS` action) |
| M-04 | Manifest RoutePaths = the frontend view-route set of the Loads controller (to be built), counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target (Q14/Q15), never ahead of it. For this module that means: only after the Loads UI is approved **and** built.

### Open gaps (carried, not solved)

1. Loads UI scope is HELD and unbuilt; the RoutePath and the action table are proposals and must be re-checked against the approved UI before this section is applied.
2. ~~If a transition button is later approved, `supplychain.loads.transition` moves from the allow-list into the action table.~~ Done
   2026-10-04 for a different reason (the key must be holdable at all); a transition **button** still needs its own approved scope.
3. Runtime tests R-02…R-04 need a native executor and the integrated target.

## 30. Tenant UI scope — MVP6-LOADS-UI-SCOPE-PREP-01 (approved 2026-10-04)

**Approved** by the owner on 2026-10-04: `docs/records/decisions/2026-10/mvp6-carrier-loads-ui-scope-owner-decision-01.md`. The full scope
text is `docs/roadmap/plans/mvp6-loads-ui-scope-01/SCOPE.md` (`ce1cadef…f7b65`, Turkish) with `PHASE15-PROPOSED.md` (`e1e579fa…d900d287`);
this section states what it binds. It changes no backend, contract or acceptance rule in §§1–29; the backend `ready-for-dev` scope stays.

### 30.1 Surface and operations

- Page `/SupplyChain/Loads` in the tenant shell (`_LayoutTenantShell`), GoldenReferenceSlim, DataTables v2 (`serverSide: false`; client
  search/sort/paging over the returned set). Bound operations: `queryLoads` (list, refresh, filter) and `createLoadPlan` (create
  offcanvas). **No transition UI** in this slice (ROOT-UI-01); no detail, edit, delete, bulk, import, lookup or optimisation.
- Browser → same-origin MVC adapter → Gateway 5000. The adapter alone holds the token and sends `X-Tenant-Id`/`X-Legal-Entity-Id` from the
  signed claims; scope never comes from browser input.
- List columns: `loadNumber`, `carrierId`, `shipmentIds`, `status` (no enrichment; opaque ids shown escaped and LTR; an absent summary field
  reads "not provided"). Filters: `status` (single; ShowAll omits it) and `carrierId` (UUID text, not a lookup). Save View / column
  visibility / reset through the shared `personalizationClient`; no browser storage.

### 30.2 Create form — 7 field types (Slim)

`carrierId` (UUID), `shipmentIds` (repeatable UUIDs, at least one, order kept), `mode` (Road/Air/Sea/Rail/Parcel, labels localized),
`plannedDepartAt` (local wall clock in a `datetime-local` field, sent as the same instant in UTC; a time that does not exist in the
user's zone is rejected), `stops[]` with `sequence` (positive integer), `locationReferenceId` (opaque string, empty allowed, no trim or
length cap) and `action` (Pickup/Delivery/Return); at least two stops. Correlation, idempotency key, tenant/LE, number, audit and version are
not user fields. The backend is authoritative for every business rule (stop numbering, Pickup+Delivery, eligibility, carrier mode).

### 30.3 Intent, correlation and replay

**An intent is one opened create form, not one payload** (Q403; `docs/records/audits/2026-10/mvp6-r2-returns-ui-01/evidence/traps-browser.md`
§T2). The UI mints the `Idempotency-Key` **and** the `X-Correlation-Id` when the form opens and keeps both across edits, failures and
retries until it closes: the create's correlation is the new Load's root (`CreateLoadHandler` passes `context.CorrelationId`), and a replay
under a different root answers 409 `CORRELATION_ROOT_MISMATCH` (`LoadRepository.cs:16`). On 409 `IDEMPOTENCY_KEY_REUSED` or
`CORRELATION_ROOT_MISMATCH` the form stops, keeps the inputs and never mints a key to get past it. A 201 with `idempotentReplay: true` is
shown as completed and the list reloads.

### 30.4 Localization, states, access

Seven tenant languages; status, mode and stop-action labels localized, wire values the contract's English names. Skeleton while the first
load runs; a failed load is an alert in place of the table, worded as a read failure; a 403 shows the denied card. UAS-001: without
`supplychain.loads.read`, only `_AccessDenied` inside the shell. The create CTA appears only with `supplychain.loads.create`.

### 30.5 Open items carried

- **LIVE-185:** the create reads Carrier and Shipment references live with the caller's token (`LoadReferenceReader`), so a creator also
  needs `supplychain.carriers.read` and `supplychain.shipments.read`, and Carrier's provider must be registered for the first to exist.
- **ROOT-UI-01:** a transition UI needs its own approved scope. Independent UI VER remains open.
