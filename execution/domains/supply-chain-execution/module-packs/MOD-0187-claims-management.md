---
id: MOD-0187
name: Claims Management
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "Owner-promoted isolated pack d035d420… (PACK-ACTIVATE-01, 2026-09-20) bound to CT-accepted bounded work package MVP6-MOD0187-CT-ACCEPT-01, accepted on published SHIPMENT-BUNDLE 3.0.0 and re-pinned to 3.1.0 / wire v1 (§31, Q380). Common-checkout integration, gateway, live producer uptake, UI, E5/G5 and rollout remain open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 6
---

# MOD-0187 — Claims Management

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"`
> returned `OK` again on 2026-09-17 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** `status: draft`; no runtime implementation, scaffold, commit or dispatch is authorized.

## 1. Module Summary

MOD-0187 owns logistics claims raised against an existing MOD-0183 shipment, with evidence references, claimed and
approved decimal amounts and an auditable resolution lifecycle. It does not own finance settlement or shipment facts.

## 2. Ownership and Boundaries

**Owns:** claim identity/number, shipment/carrier references, reason/evidence, claimed/approved amount and lifecycle.

**Consumes:** MOD-0183 shipment/POD; optional MOD-0184 carrier reference; existing frozen **WAREHOUSE-OUTBOUND** as upstream context only (see §21; no direct receiving/ingress authority).

**Does not own:** shipment, POD, carrier, warehouse trigger, INVENTORY, Accounts Payable/Receivable or payment SoR.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `Claim` | Logistics claim identity, amount, evidence and status |
| Commands | Create and transition claim |
| Queries | List claims by shipment/status; no by-ID GET |
| Events | Opened/investigating/approved/rejected/settled/closed/withdrawn |
| Permissions | Claims read/create/investigate/decide/settle |

## 4. Entity Fields

`Claim` has server UUID, server-resolved TenantId and request-borne, MDM-validated LegalEntityId, unique `ClaimNumber`, required `ShipmentId`, optional
`CarrierId`, reason, positive decimal-string `ClaimedAmount`, three-uppercase-letter currency (ISO-4217 membership is not specified by the schema), evidence refs, nullable approved amount,
resolution code, status/version and audit/soft-delete fields.

## 5. Repo Scope

The current proposed future file allowlist is §29 / FINAL-PACK-DELTA-01 (47 paths, no worker). §§25–28 are historical proposals where superseded. No broad wildcard or shared composition edit is authorized. This INS lane prepares a patch only under docs/roadmap/plans/mod-0187-final-pack-delta-01/; it does not apply this pack change.
Frozen contracts remain read-only. Future DEV requires explicit pack and shared-file release.

## 6. Protected Paths

- `.antigravity/**`, gateway/shared registrations, archive/frontend shell files.
- MOD-0183/MOD-0184 persistence/features and every other SupplyChain feature.
- Finance/payment services and all foreign-domain data stores.
- Frozen and central Warehouse/Supplier contracts.
- `docs/analysis/contracts/shipment-bundle.openapi.yaml`, `docs/analysis/contracts/claims-semantics-v3.0.0.md` and `docs/analysis/contracts/shipment-root-semantics-v3.0.0.md` (published, frozen; read-only for this module).

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| MOD-0183 Shipment/POD | Required before runtime | Claim must reference an existing eligible shipment |
| MOD-0184 Carrier | Optional reference | No Carrier mutation or local copy |
| `SHIPMENT-BUNDLE` v1 | Frozen | Claim schema/lifecycle/event/mock authority |
| Warehouse outbound | Frozen read-only upstream seam exists | No inbound return, receiving or automatic ingress implied; see §21 |

## 8. Runtime Constraints

Prospective amounts use decimal strings, never float. Mutations are tenant/LE scoped, idempotent and carry UUID
correlation unchanged into events. Settlement state records an operational outcome and does not execute payment; no settlement-reference input is defined.
This draft grants no runtime authority.

## 9. Layout & Shell Contract

`shell: none`; no UI, form, localization or DataTable.

## 10. Backend File Convention

Prospective root: `Features/Claims/` with standard Commands, Queries, separated handlers, Validators and
`ClaimModels.cs`. Evidence is stored as immutable references, not binary content.

## 11. Frontend File Contract

Not applicable; a future claims UI requires an approved pack revision.

## 12. Validation Rules

Historical draft business proposals below; exact frozen schema and unresolved approvals are distinguished in §§22–24.

- Shipment must resolve in the same tenant/LE; carrier reference, if supplied, must match/relate to the shipment.
- Claimed amount is positive decimal; currency is three uppercase letters.
- Approved amount is required only for Approved and cannot exceed claimed amount.
- Lifecycle follows Open→Investigating|Withdrawn; Investigating→Approved|Rejected; Approved→Settled;
  Rejected→Closed; Settled→Closed.
- Tenant/LE payload fields are rejected; mutation headers are mandatory.

## 13. Failure Path to Verify

Unknown shipment, mismatched carrier, invalid amount/currency, approved amount overflow, invalid transition and
idempotency mismatch return deterministic errors with no partial write/event; cross-scope IDs return 404.

## 14. Authorization Convention

Prospective permissions: `supplychain.claims.read`, `.create`, `.investigate`, `.decide`, `.settle`.

## 15. Gateway / API Routing Decision

Use frozen `SHIPMENT-BUNDLE` `/claims**`; only a separate integration-agent WP may add gateway routes.

## 16. Acceptance Criteria

- [ ] No runtime implementation exists while status is `draft`.
- [ ] Future payloads, responses, errors and events match frozen v1.
- [ ] Shipment/POD is consumed through MOD-0183 contract/reference only.
- [ ] Amount precision and lifecycle transitions are deterministic and atomic.
- [ ] Settled status does not create a payment/AP/AR record.
- [ ] Correlation ID remains unchanged through all claim events.

## 17. Test Expectations

DCP-002, OpenAPI/mock, MOD-0183 dependency, decimal/currency validation, lifecycle, idempotency, tenant isolation,
RBAC, evidence reference, atomic outbox and no-finance-SoR architecture tests.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] MOD-0183 dependency and frozen contract authority are explicit.
- [x] Backend-only slice and ownership boundaries are bounded.
- [ ] MOD-0183 executable verification is available.
- [ ] Owner approval changed status from `draft`.

## 19. Implementation Notes

**ASSUMPTION (documented scope):** v1 claim is a logistics operational record; settlement is a status. No settlement-reference field exists in the command; do not invent one. Financial
posting, insurer/carrier EDI and recovery accounting are follow-up integrations.

## 20. Follow-up Items

- Verify MOD-0183 contract/runtime before MOD-0187 dispatch.
- WAREHOUSE-OUTBOUND exists; Claims needs no direct Warehouse call. No invented trigger/adapter.
- Separate integration WP links claim outcome to finance or supplier performance without duplicating their SoR.


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
| `queryClaims` | GET `/claims` | 200 |
| `createClaim` | POST `/claims` | 201, 404, 409, 422 |
| `transitionClaim` | POST `/claims/{claimId}/transition` | 200, 404, 422 |


Exact GET query parameters (all optional; not nullable unless specified):

- `shipmentId`: `{"type":"string","format":"uuid"}`.
- `status`: `{"$ref":"#/components/schemas/ClaimStatus"}`.

All command/nested input objects listed below have additionalProperties:false. Required absence and null
are distinct. No extra TenantId/LegalEntityId/version/audit/actor field is accepted.

| Schema.field | Required | Exact schema (not an invented validator limit) |
|---|---|---|
| `CreateClaimCommand.shipmentId` | yes | `{"type":"string","format":"uuid"}` |
| `CreateClaimCommand.carrierId` | no | `{"type":["string","null"],"format":"uuid"}` |
| `CreateClaimCommand.reasonCode` | yes | `{"type":"string"}` |
| `CreateClaimCommand.claimedAmount` | yes | `{"$ref":"#/components/schemas/Decimal"}` |
| `CreateClaimCommand.currency` | yes | `{"type":"string","pattern":"^[A-Z]{3}$"}` |
| `CreateClaimCommand.evidenceReferenceIds` | no | `{"type":"array","items":{"type":"string"}}` |
| `TransitionClaimCommand.targetStatus` | yes | `{"$ref":"#/components/schemas/ClaimStatus"}` |
| `TransitionClaimCommand.occurredAt` | yes | `{"type":"string","format":"date-time"}` |
| `TransitionClaimCommand.resolutionCode` | no | `{"type":["string","null"]}` |
| `TransitionClaimCommand.approvedAmount` | no | `{"anyOf":[{"$ref":"#/components/schemas/Decimal"},{"type":"null"}]}` |
| `TransitionClaimCommand.note` | no | `{"type":["string","null"]}` |

`ClaimStatus`, `TransportMode` and `Decimal` use frozen enums/patterns. Decimal is a **string** matching
`^-?\d+(\.\d+)?$`; it admits signed/zero values and gives no precision/scale cap. Positive quantity/amount
is a draft business condition (§24), not a JSON-schema fact. No binary float, rounding or invented length cap.
Optional string|null fields permit absence/null/empty unless an explicitly approved business decision says otherwise.
Optional arrays allow omission but **not null** and have no minItems/uniqueItems unless specified above.
Required string without minLength permits empty text; no blanket NotEmpty/trim rule.
Date-time accepts valid offsets, not only literal Z; UTC storage must preserve the instant. Past/future/time-order
restrictions are not in schema. UUID nil rejection and array duplicate rules cannot be copied from other modules.

Responses: `ClaimListResponse` requires items,total,contractVersion. `ClaimSummary` has **no required list**;
listed non-null property types do not imply their presence is schema-required. The implementation proposal
emits all summary properties for useful reloads, without rewriting the contract. `ClaimResponse` must follow
its exact schema, including allOf where used. No outer data envelope; internal Response<T> is adapted at API.

Exact response schema: `{"type":"object","required":["claimId","claimNumber","shipmentId","status","idempotentReplay","contractVersion"],"properties":{"claimId":{"type":"string","format":"uuid"},"claimNumber":{"type":"string"},"shipmentId":{"type":"string","format":"uuid"},"status":{"$ref":"#/components/schemas/ClaimStatus"},"approvedAmount":{"anyOf":[{"$ref":"#/components/schemas/Decimal"},{"type":"null"}]},"idempotentReplay":{"type":"boolean"},"contractVersion":{"$ref":"#/components/schemas/ContractVersion"}}}`.

Frozen transition description (authority, no extra arrows):

> Izinli gecisler: Open->Investigating|Withdrawn; Investigating->Approved|Rejected; Approved->Settled; Rejected->Closed; Settled->Closed. Diger gecisler INVALID_CLAIM_TRANSITION ile reddedilir.

Create success starts **Open**. Same-state/new-key and all non-listed arrows are invalid; terminal
states have no outgoing arrow. Current-state check must be atomic; no request Version/If-Match exists.
Exact replay precedes new transition evaluation only after current auth/scope/schema checks, subject to
owner resolution of the replay policy below. List filtering uses only frozen parameters, not an invented detail query.

## 23. Errors, security, headers and replay — covered vs unresolved

Policy proposal: `[Authorize]` plus server-side `[HasPermission]`; actor tenant_user in validated tenant/LE context.
Existing draft keys are `supplychain.claims.read`, `supplychain.claims.create`, `supplychain.claims.investigate`, `supplychain.claims.decide`, `supplychain.claims.settle`. Read/create mapping is clear as a proposal; closure/withdrawal and approval separation require D187-04.
No permission inferred from a valid token alone; test scoped actors independently from platform bypass.

| Case | Frozen fact / bounded requirement | Exact unresolved decision / acceptance oracle |
|---|---|---|
| Headers | GET and both POST require UUID X-Correlation-Id; POST key is string length1..128 | Nil UUID, duplicate header, parser/whitespace behavior, validation/auth ordering and fallback trace must be owner-bound for this family; no Carrier-default import |
| JWT + scope | Bearer; server-resolved tenant; LegalEntityId request-borne and MDM-validated fail-closed (R-2, SHIPMENT-BUNDLE 3.2.0); global JWT/header matching; no payload-selected scope | Security owner binds trusted claim names, missing/duplicate claim handling, scope-header validation and precedence. Test two tenants×two LEs and missing permission; all lookups fail closed |
| Missing/cross-scope target | POST create/transition declare404 NotFound; list declares200 only | Target code `CLAIM_NOT_FOUND` exists in shared examples; reference-specific404 mapping/disclosure must be chosen; list no-leak filtering required |
| Schema / auth / persistence failure | Shared Error requires code/message/correlationId plus contractVersion:v1, details optional | GET lacks declared errors; POST lacks400/401/403/5xx (and415). Owner supplies exact status/code/header matrix and decides clarification vs versioned amendment before DEV |
| Duplicate / changed payload | Create declares409 Conflict; shared IDEMPOTENCY_KEY_REUSED example | Transition **does not declare409**; cannot substitute422 to conceal it or silently add409. Business duplicate code/error is separately unresolved |
| Lifecycle | Transition422 + INVALID_CLAIM_TRANSITION frozen | Verify all source×target pairs and zero partial writes/events; create422 examples from other modules are not local business scenarios |
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
**Reviewable bounded DEV scope:** create Open Claim, list, frozen claim transitions; scoped Shipment read
and optional Carrier read, exact decimal amount evidence, immutable audit/success receipt/outbox.
Settled means an **operational claim status**, never AP/AR posting, payment, credit note, recovery accounting
or bank settlement. No settlementReference field exists; do not add one to command/entity as a claimed wire field.
Optional carrierId resolves through scoped Carrier list; no get-by-ID invented. Shipment/POD and evidence
are reference-only; no files copied and no Supplier/Carrier/evidence master created.

| Decision ID / accountable owner | Exact decision still required before runtime | Acceptance once bound |
|---|---|---|
| D187-01 business + contract | Eligible Shipment statuses; nullable Carrier relationship when Shipment.carrierId absent/different; whether inactive/retired carrier allowed for historical claim; evidence existence policy | Carrier absent/null, same/mismatched/cross-scope; past claim against retired carrier; unavailable evidence cannot silently validate |
| D187-02 business/finance + contract | Confirm claimedAmount>0; Approved requires approvedAmount and define lower bound (zero allowed?) plus <=claimed; behavior if approvedAmount supplied at other targets; resolutionCode condition | negative/zero/equal/over-bound, missing/null Approved, gratuitous amount on Closed; correct business reject/error or preserve policy without finance side effects |
| D187-03 contract + data | Decimal arbitrary schema length vs chosen storage capacity; lexical replay equality for 250/250.0, negative zero and leading zeros; currency membership (schema only A-Z3, not ISO catalog) | Very large/precise schema-valid values must not silently overflow/round; no exponent/JSON number accepted; no invented two-decimal cap or ISO whitelist |
| D187-04 security + business | Map all target states to read/create/investigate/decide/settle or owner-approved additional key; closure/withdrawal, self-approval and Workflow requirement not defined | Actor/action matrix including Approved/Rejected/Settled/Closed/Withdrawn; fail closed uncovered permission; no fabricated approver roles or limits |
| D187-05 contract/security | §23 exact error/replay/root policy and dependency error mapping, conditional validation status/codes | Changed amount/key conflict, different-root replay, same-state/new-key; no undocumented transition409 added |
| D187-06 data/CT | Atomic claim+receipt+audit+outbox, unique claimNumber generator/index; allowed duplicate claims vs create idempotency; root/event time and immutable amount history | Racing create same key once; different keys same Shipment follow approved duplicate policy (not assumed unique); lifecycle races cannot settle twice |

Existing draft positive amount and approved<=claimed are **proposed business rules**, not Decimal schema
constraints. Exact arithmetic must use arbitrary-precision representation or an explicitly owner-approved
bounded-domain amendment; selecting .NET decimal/Decimal128 alone does not cover unrestricted wire precision.
Persist original accepted decimal text plus exact arithmetic representation if needed; preserve wire type string.
No currency FX, financial posting, balance, rounding policy or automatic payment inferred from operational settlement.
Evidence IDs are opaque strings in contract; existence/auth lookup requires an actual published seam and decision,
not a mock invented solely to make a dependency pass. No new currency/evidence lookup endpoint in this pack.

## 25. Exact prospective DEV owned files and shared exception proposal

This is a design allowlist for **future approval**, not this PREP's write grant. Prefix S expands literally to
`services/Diten.SupplyChainService/src/Diten.SupplyChainService`.
No parent wildcard or another feature is writable. Only the following module paths are proposed:

`S.Domain/Features/Claims/` exact files:

- `Claim.cs`
- `ClaimStatus.cs`
- `ClaimScope.cs`
- `ClaimLifecycle.cs`
- `ClaimAuditEntry.cs`
- `ClaimMutationResult.cs`
- `IClaimRepository.cs`

`S.Application/Features/Claims/` exact files:

- `Commands/CreateClaimCommand.cs`
- `Commands/TransitionClaimCommand.cs`
- `Queries/GetClaimListQuery.cs`
- `Handlers/CommandHandlers/CreateClaimHandler.cs`
- `Handlers/CommandHandlers/TransitionClaimHandler.cs`
- `Handlers/QueryHandlers/GetClaimListHandler.cs`
- `Validators/CreateClaimValidator.cs`
- `Validators/TransitionClaimValidator.cs`
- `Validators/GetClaimListValidator.cs`
- `ClaimModels.cs`
- `ClaimRequestContext.cs`
- `ClaimRequestFingerprint.cs`
- `IClaimReferenceReader.cs`

`S.Api/Features/Claims/` exact files:

- `ClaimsController.cs`
- `ClaimContextMiddleware.cs`
- `ClaimContractError.cs`

`S.Persistence/Features/Claims/` exact files:

- `ClaimRepository.cs`
- `ClaimSchema.cs`
- `ClaimPersistenceRegistration.cs`
- `ClaimOutboxStore.cs`
- `IClaimCommitProbe.cs`
- `NoOpClaimCommitProbe.cs`

`S.Infrastructure/Features/Claims/` exact files:

- `ClaimReferenceReader.cs`
- `ClaimPermissions.cs`
- `ClaimPermissionAttribute.cs`
- `ClaimOutboxWorker.cs`

Tests prefix `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/`:
`ClaimContractTests.cs`, `ClaimLifecycleTests.cs`, `ClaimIsolationTests.cs`, `ClaimReplayTests.cs`,
`ClaimConcurrencyTests.cs`, `ClaimAtomicityTests.cs`, `ClaimReferenceTests.cs`.
Probes prefix `services/Diten.SupplyChainService/tests/claims/`:
`runtime_probe.py`, `restart_probe.py`, `verify_evidence.py`.
No other test or historical evidence edits. DEV/VER report destinations must be separately assigned by CT;
this allowlist grants no new docs record writes. New files beyond this list require versioned scope review.

Existing shared file requiring **separate explicit authorization and single writer**:
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`.
Proposal: register this module's scoped context/reference client, owned Persistence registration and local
outbox worker; exact family-only middleware/model-error adapter. Preserve existing Carrier branch and exclude
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
by this module: `claims`, `claims_receipts`, `claims_audit`, `claims_outbox`;
scope tenant+LE on entity/receipt/outbox/audit reads and mutations; soft-deleted aggregates excluded from
normal reads, no delete API. Internal Version CAS/retry revalidates state; no new client version/header.
Unique scoped business number, receipt identity and eventId indexes; assignment/entitlement unique identity
where applicable. Number format/generation and retention are owner decisions, examples are not sequence specs.

Successful commit = aggregate + receipt + audit + lifecycle event outbox (+ constraint record where applicable).
Injected failure at each boundary rolls back all; committed response loss replays without another event.
Unknown commit resolves by durable receipt lookup, never blind insert. Success replay must retain original
result even after subsequent state changes; exact transport/correlation decisions still need §23 approval.

Envelope: LifecycleEventEnvelope with eventId, eventType, occurredAt, correlationId, aggregateType=`Claim`,
aggregateId, payload=`ClaimEventPayload`, contractVersion=v1; optional nullable causationId.
Only frozen enum events: ClaimOpened, ClaimInvestigating, ClaimApproved, ClaimRejected, ClaimSettled, ClaimClosed, ClaimWithdrawn. Initial event and each permitted target event map exactly to lifecycle; no duplicate event on replay.
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


## 28. MVP6-MOD0187-PREP-02 — current owner review (2026-09-19)

**Status remains draft; DEV/VER HELD; Phase1.5 approval BLOCKED.** No runtime, scaffold, shared composition,
canonical publication, finance posting or pack promotion is authorized by this preparation.

Current preparation source: [PREP-02 package](../../../../docs/roadmap/plans/mod-0187-prep-02/README.md).
The [single decision set](../../../../docs/roadmap/plans/mod-0187-prep-02/owner-decisions.md) refines existing
D187-01–06 into exact proposed rules/scenarios with required owners; all remain UNAPPROVED.
It supersedes unresolved proposal descriptions in §§12,23–27 for this review, without converting any into a default.
[Phase1.5 and closed future ownership](../../../../docs/roadmap/plans/mod-0187-prep-02/phase-1.5-and-scope.md)
supersede §25's allowlist and §27's historical readiness references. Earlier observations remain historical context.

Current frozen SHIPMENT-BUNDLE metadata2.0.0 / wirev1 SHA256:
`93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571`.
Claims operations/schema/arrows remain as enumerated in §22; old §21 metadata1.1.0/hash and v4.0 pending Carrier
sequencing are superseded by the exact published-input/authority reconciliation in PREP-02 README/inputs.
Carrier bounded CT acceptance and Loads publication remain intact; neither grants Claims runtime acceptance or
approval of Claims business/security rules. No MOD-0186 unapproved decision is consumed.

[Contract GAPs](../../../../docs/roadmap/plans/mod-0187-prep-02/contract-gaps.md) route root seam, amount/reference
semantics, errors/headers/replay/RBAC, architecture exceptions, exact publication/consumer uptake and composition
to CT. ShipmentDetail currently exposes no authoritative original lifecycle root: no fabricated value or direct DB
access is permitted. Any producer seam change is a separate authorized WP, not MOD-0183 ingress or a hidden DEV edit.

[HELD DEV v1.0](../../../../docs/roadmap/plans/mod-0187-prep-02/dev-held-v1.0.md) and
[HELD VER v1.0](../../../../docs/roadmap/plans/mod-0187-prep-02/ver-held-v1.0.md) are review-only templates.
Activation requires exact owner decisions, amendments/publication/mock uptake, approved Phase1.5/exceptions,
single-writer Program.cs scope and explicit pack promotion/new prompt version. All existing prompts preserved.
[SOP§22 evidence report](../../../../docs/records/audits/2026-09/mod-0187-prep-02/report.md) distinguishes static
preparation checks from unperformed runtime tests. Existing Shipment/Carrier/**Loads** regressions are mandatory;
historical architecture test totals are not a future waiver.


## 29. FINAL-PACK-DELTA-01 — proposed final-release binding (2026-09-20)

This section supersedes contrary current-readiness statements in §§12,23–28 **only when this proposed pack patch is separately accepted/applied**. Earlier proposals remain historical. Status stays draft. Source package: `docs/roadmap/plans/mod-0187-final-pack-delta-01/`; authority-and-contract.md binds the actual user approval, runtime-acceptance-R01-R30.md covers every partial/declaration row, prospective-owned-paths.txt is the closed47-file proposal, phase-1.5-and-integration.md lists pending architectural grants; DEV-v1.0-HELD.md and VER-v1.0-HELD.md are not dispatches.

D18701–06 reconciled design is already approved for consumer amendment preparation, not runtime execution; do not re-open those business choices. Current canonical remains2.0.0/wirev1 hash93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571. Proposed final target3.0.0/wirev1 YAML5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c, Claims annex16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63, root annex7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af. Final release/VER technical PASS is not publication/consent. Publication→approved pack/Phase1.5→runtime DEV remains the integration order.

Exactly GET/POST /claims and POST /claims/{claimId}/transition. Reference eligibility and all wire/error/header rules follow the final Claims annex, not old Carrier/Returns policy. Withdrawn=investigate; Closed=decide; granted self-approval permitted; zero approved amount valid; distinct-key duplicate claims and no cross-claim total cap; exact BSON decimal strings, lexical amount/time fingerprint; no ISO/precision/scale restriction added. Lifecycle422 INVALID_CLAIM_TRANSITION; payload409 IDEMPOTENCY_KEY_REUSED; root409 CLAIM_CORRELATION_MISMATCH.

Authentication and current grant precede receipt access. Receipt key=(tenant,LE,operation,target-or-create,key); actor/root excluded. Found receipt: saved-root check before fingerprint; wrongroot+changedpayload returns root409. Identicalrequest returns original201/200 plus replay flag without reread/newwrite even after state change. Fresh create inherits authoritative Shipment root only; missing/null/empty503 CLAIM_REFERENCE_INCOMPLETE; malformed502 CLAIM_REFERENCE_INVALID; differing validroot409; authoritative nil preserved. No trace/ID derivation or internalDB. Final schema seam availability does not imply producer uptake. Direct dependency Shipment; optionalCarrier, no MOD0185/0186 acceptance gate invented.

Successful persistence is exactly four owned collections: claims,claims_receipts,claims_audit,claims_outbox. No assignment/entitlement collection. Unique scoped number/receipt/event IDs; CLM-lowercaseUUIDN number, known collision503 atomically, no automatic renumber. Separate aggregate→receipt→audit→outbox stage failpoints prove reachedstage and rollback on known precommit faults. Unknowncommit/postcommitresponse loss is not zero-write: resolve durable receipt/samekey recovery. InternalCAS/reread;49lifecycle pairs and20way races require real Mongo evidence. Original approved amount retained; createevent serverUTC, transition original validateddate-time, inheritedroot, stableeventID, causationnull. No worker/publisher/fake transport; outbox Pending throughrestart. Settled is not finance posting.

Exact prospective scope removes ClaimOutboxWorker.cs from PREP02's48paths; remaining47paths only. Program.cs is NOT Claims-owned: separate integration-owner proposal limited to Claims DI/family middleware/error adapter and exact exclusion from generic Shipment branch, no worker/sharedoutbox/globalJWT changes. Every other source/test/contract/pack/registry/guard path protected. R01–R30 model declarations/partial proofs remain runtime-unverified until explicit RT-Rxx acceptance. No new source files are authorized by this proposed patch.

Remaining activation gates: exact canonical release disposition/publication; separate pack/47path/Phase1.5 exceptions approval and explicit runtime grant; fresh isolated execution checkout with current baseline and separate Program.cs lease; writer-complete then independent VER/CT. Contract-faithful mocks can support bounded isolated DEV after those grants; real consumer create/rollout requires separately verified producer seam. No repeated PREP or reapproval of D187 business defaults is requested. No runtime/canonical/guard/pack promotion occurs in this preparation.

## 30. PACK-ACTIVATE-01 — controlling isolated dispatch disposition

This section supersedes older draft/HELD/publication-pending wording only for the approved bounded Claims slice. Actual user conditional grant 2026-09-20T11:44:45.025Z is extracted in docs/roadmap/plans/mod-0187-runtime-dispatch-01/owner-authority.md. Exact delta afe36ff9123088593ec920191657176e5343270655b7b0a2be4106bc05820187 applied without conflict. Canonical SHIPMENT-BUNDLE3.0.0/wirev1 and Claims/root annex hashes match publication handoff. Phase1.5 nine-row checklist is recorded in the dispatch package; module-specific repository, internal CAS/audit and contract-envelope exceptions are limited to this owner-approved slice.

Isolated DEV GO applies only to the47 Claims paths in the dispatch allowlist; no worker/publisher, finance posting or shared edits. R01–R30 acceptance remains mandatory and UNEXECUTED at activation. Actual HTTP/JWT/Mongo/concurrency/restart and consumer uptake are DEV/VER outputs, not prerequisites requiring a finished consumer. Root producer isolated acceptance does not prove final-target uptake. Program.cs remains integration-owner-only; HTTP integration requiring its separately approved exact diff remains gated. Independent VER starts after writer-complete. No rollout/migration/backfill/E5/G5/full-module acceptance. Common checkout pack is not promoted by this isolated copy.

## 31. Accepted bounded scope binding

Approved: `docs/records/decisions/2026-09/mvp6-claims-pack-signoff-owner-decision-01.md`

This section records, without changing any business rule above, the Control Tower acceptance of the bounded work package built under this pack. It is a proposal until the owner decision in `docs/roadmap/plans/mvp6-pack-alignment-02-claims/PROMOTION-DECISION.md` is recorded.

| Binding | Exact value |
|---|---|
| Owner-promoted isolated pack (this pack before §31, apart from the §6 contract line) | SHA-256 `d035d42059141f17edb90bb97183892a3c98c171a16770795ce2557f834ac9b0` = shared pack `a342054c…1c1f` + `docs/roadmap/plans/mod-0187-runtime-dispatch-01/pack-activation.patch` (SHA-256 `9b5327e2ff93ee6498459f496f855e80513c784ef2d95b72930680f0e5988a16`), which contains the owner-approved delta `afe36ff9123088593ec920191657176e5343270655b7b0a2be4106bc05820187` |
| Owner authority | `docs/roadmap/plans/mod-0187-runtime-dispatch-01/owner-authority.md` (SHA-256 `8a89e5d040a1c2fde4d4053a0226542786c79e28011a09211780dce9f72212c6`), user message 2026-09-20T11:44:45Z |
| Controlling CT acceptance | `docs/records/audits/2026-09/mvp6-mod0187-ct-accept-01/SOP-22.md` SHA-256 `30c9233e3d8ac170e53517531b75c616df427d9ceee76aa6bee7c4ea1bab7942` — ACCEPTED, approved isolated bounded work package only |
| Row-level acceptance | `R01-R30.tsv` SHA-256 `e8e0856fc28b335846710510d7567fd46e185353893d2edcecc3b50b80b1216f`; controlling requirement file `runtime-acceptance-R01-R30.md` SHA-256 `75f3f178b9f4b0169f459de3a43c76fe5568db9b14d83c444b473d6e44a4661b` |
| Accepted source | 341-entry manifest `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80`; archive `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21` |
| Accepted composition (`Program.cs`, integration-owner surface) | `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` (R14 patch `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0`) |
| Published contracts at acceptance | SHIPMENT-BUNDLE 3.0.0 YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`; Claims annex `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63`; root annex `7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af`; wire `contractVersion: v1` |
| Published contracts today (Q380) | SHIPMENT-BUNDLE 3.1.0 YAML `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`; Claims annex and root annex unchanged (same hashes as above); wire `contractVersion: v1`. re (`docs/records/audits/2026-10/mvp6-q218-contract-pin-01/VERSION-DELTA.md`, re-measured in `docs/records/audits/2026-10/mvp6-q380-contract-repin-01/`) |

**Precedence over stale wording:** SHIPMENT-BUNDLE 3.0.0 was published at acceptance; this pack is now bound to the canonical 3.1.0 (`6dc1dd48…96aa2`), re-pinned by Q380 because every Claims operation and the `getShipment` and `queryCarriers` reads are identical in both versions (Q218). The sentence in §29 that the current canonical "remains 2.0.0 / `93c696e2…`" and the §5 note that this lane "does not apply this pack change" are historical. §30's "R01–R30 … UNEXECUTED at activation" is superseded by the accepted matrix above. No business rule in §§21–30 is changed.

**Accepted boundary:** R01–R30 are satisfied only at the evidence class stated per row in `R01-R30.tsv`. R22/R25 are accepted only at the separately authorised `ClaimsEvidence` E4 boundary; normal composition keeps `NoOpClaimCommitProbe`. The historical broad `~Shipment` 51-test run with 12 serializer failures stays recorded and is not relabelled PASS.

**Owned paths:** the 47 paths of `docs/roadmap/plans/mod-0187-runtime-dispatch-01/owned-paths.txt` (SHA-256 `eebaf0ac75206a664c168c072411224dc22e4f9b295cf70ad79031eb72e99517`). All 47 occur in the accepted 341-entry manifest; none exists in the common checkout today. `Program.cs` and `ClaimOutboxWorker.cs` are not owned paths.

**Still open:** common-checkout source uptake and composition, gateway and shared permission registration, real producer authoritative-root uptake beyond the accepted seam, publisher/Event Bus delivery, finance posting, UI, migration/backfill, rollout, E5/G5 and full-module acceptance. This section is not `done` status and grants no new DEV scope.

## 32. Tenant UI scope (UI-REVISION-01) — GoldenReferenceSlim

This section adds the tenant UI scope approved in `docs/records/decisions/2026-09/mvp6-returns-claims-ui-scope-owner-decision-01.md`
(SHA-256 `309319173e77bcc5da1d4587082742dfa4feb31b0d5e6337b04309646acfaee9`), bound to the draft package
`docs/roadmap/plans/mvp6-ui-pack-drafts-01/` (SHA256SUMS `b568c94f7f524dd0bff1e2fa3277571fed174c6214ac0a9efd3ab13200abb57f`;
`claims/APPROVAL-DECISION.md` `ea11f670af31355e67e939c219c8d27142977c2ea79c09398efded4a47bf4813`). It stacks on §31 and changes
**no** business, contract, backend, owned-backend-path or acceptance rule in §§1–31. The frontmatter changes `shell: tenant`,
`golden_reference: slim` and `form_field_count: 6` apply to the UI only. For the UI, §9 ("`shell: none`") and §11 ("not applicable")
are superseded by this section; for the backend they remain as written. `status` stays `ready-for-dev` (backend scope, per §§30–31);
UI code is authorized only as §32.14 states.

### 32.1 Identity

UI revision inside MOD-0187; no FU, child or new ID. The pack author runs
`python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"` before applying; non-zero exit stops.
Observed 2026-09-26: `OK`, exit 0.

### 32.2 Layout and shell contract

- `shell: tenant` → the Claims page view (`Index.cshtml`) states `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`)
  set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02); `_ViewStart.cshtml` unchanged.
- View folder `frontend/Diten.Web/Views/SupplyChain/Claims/`; route `/SupplyChain/Claims`; no `/Platform` prefix; no `Areas/`.
- Skeleton, empty and error are three distinct states (VIEW-001 §3.1); no spinner or "Loading" text.

### 32.3 Bound operations (nothing else)

Published SHIPMENT-BUNDLE 3.1.0 / wire v1 (`6dc1dd48…96aa2`; accepted on 3.0.0, see §31) and Claims annex (`16e65c26…4eb63`) only:
`queryClaims` (list/filter/reload), `createClaim` (create offcanvas), `transitionClaim` (row action), and Shipment-owned
`getShipment` consumed read-only (status, `carrierId`, and the lifecycle root resolved **server-side**). No Claim by-ID GET, detail page,
edit, delete, bulk, import/export, server paging/search/sort, carrier lookup, evidence upload/verification, currency catalogue or finance call.

### 32.4 Screens, routes, permissions

Browser → same-origin MVC (`proxy-profile`, NET-001) → Gateway 5000 → SupplyChain 5061. The browser never calls 5000/5061 or holds a bearer token.

| Surface | MVC route | Gateway downstream | UI display gate | Backend |
|---|---|---|---|---|
| Page | GET `/SupplyChain/Claims` | — | `supplychain.claims.read` | — |
| List adapter | GET `/SupplyChain/Claims/api?shipmentId=&status=` | GET `/api/shipment-bundle/claims` | read | `queryClaims` |
| Shipment resolve adapter | GET `/SupplyChain/Claims/api/shipments/{shipmentId:guid}` | GET `/api/shipment-bundle/shipments/{shipmentId}` | `.create` + `supplychain.shipments.read` | `getShipment`; projection = number, status, carrierId; root never returned to the browser |
| Create adapter | POST `/SupplyChain/Claims/api` | root via `getShipment`, then POST `/api/shipment-bundle/claims` | `.create` + `supplychain.shipments.read` | `createClaim` |
| Transition adapter | POST `/SupplyChain/Claims/api/{claimId:guid}/transition` | root via `getShipment`, then POST `/api/shipment-bundle/claims/{claimId}/transition` | exact target key + `supplychain.shipments.read` | `transitionClaim` |
| Unsupported detail/edit/delete/bulk/import/export | absent | absent | — | no route, control, proxy or request |

Permission keys stay exactly as published and approved: `supplychain.claims.read`, `.create`, `.investigate`, `.decide`, `.settle`
(annex D187-04 target map), plus the approved UI prerequisite **`supplychain.shipments.read`** for create and transition actors (G-SHIPREAD).
The UI check is a display decision (UAS-001 §4); `[HasPermission]` on the backend stays the authority.

Adapter headers: `Authorization` server-side only; `X-Correlation-Id` = fresh UUID trace for GET, **the Shipment's
`lifecycleCorrelationId`** for create/transition; `Idempotency-Key` = browser per-intent key forwarded exactly. Optional
`X-Tenant-Id`/`X-Legal-Entity-Id` and scope query keys are never sent. Antiforgery on every POST.
The browser-supplied `shipmentId` on a transition is a lookup hint only; the backend root check (409 `CLAIM_CORRELATION_MISMATCH`) is authoritative.

**Measured 2026-10-03 (Q279, ledger rows Q279 and Q285 in `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`):** the root rule
above is enforced by the backend for every caller of `createClaim`, not only this adapter. Against a started service, a create with a
fresh `X-Correlation-Id` returned 409 `CLAIM_CORRELATION_MISMATCH`; the same create carrying the Shipment's own `lifecycleCorrelationId`
returned 201. Source of the check: `ClaimRepository.cs:70` (create) and `:101` (transition). Any client must read `lifecycleCorrelationId`
from the Shipment detail and send it. The Claims UI is not built (§33 open gap 3), so no built client has been checked against this rule.

### 32.5 List — bounded DataTables v2 profile

`data-dt-standard="v2"`, `DtDefaults.create()`, explicit `stateSave: false`, `serverSide: false`; client search/sort/paging over the
returned set only; `total` shown as returned. Filter (`_Filter.cshtml` inline collapse): `status` single Select2 with `ShowAll`
(omits the parameter), `shipmentId` UUID text input (not a lookup). No other query parameter. Save View / column visibility / colReorder / Reset
through the shared `personalizationClient` (`moduleKey`/`pageKey` codes assigned by the integration owner); no localStorage.
Columns: `claimNumber`, `shipmentId` (LTR, copyable), localized status, `claimedAmount` (exact wire text, LTR, no grouping/rounding),
`currency` (as sent), actions. Absent summary field → "not provided"; no action without `claimId`; malformed envelope → error, never empty.
QuickView (`_DetailsQuickView.cshtml`) shows the row summary and copyable `claimId` only, with no by-ID request and no Edit button.
`approvedAmount` is not in `ClaimSummary` and is not listed (gap §32.13).

### 32.6 Create offcanvas — field count and validation

`form_field_count: 6` → **GoldenReferenceSlim**, create-only `_CreateEditOffcanvas.cshtml` (no edit mode), `.diten-field` + icon per field.

| Field | Required | UI rule (no client tightening; backend authoritative) |
|---|---|---|
| `shipmentId` | yes | UUID text + Resolve; a late response for a previous UUID never populates; 404 → safe-not-found, no shipment data left in DOM |
| `carrierId` | no | Checkbox "link the shipment's carrier", enabled only when the resolved Shipment has non-null `carrierId`; checked sends that UUID, unchecked omits |
| `reasonCode` | yes (presence) | Free text sent as typed; empty allowed; no HTML `required`, trim or maxlength |
| `claimedAmount` | yes | Text, `inputmode=decimal`, sent as a JSON **string** exactly as typed; no conversion, locale comma handling, formatting or float |
| `currency` | yes | Text sent as typed; no ISO list, no auto-uppercase |
| `evidenceReferenceIds` | no | Repeatable text; omitted when empty; order, duplicates, empty strings preserved; no upload |

Eligibility (Dispatched, InTransit, Delivered, Exception, Closed) is shown as a note and submit is disabled for other statuses
(display only; backend 422 `CLAIM_SHIPMENT_INELIGIBLE` stays authoritative). Required-contract parity follows the backend validator
(presence ≠ nonempty).

### 32.7 Transition action

Shown only when the row status permits the arrow and the actor holds the exact key. `occurredAt` (date-time with explicit offset;
default now; exact text kept for retries) on every target; optional `resolutionCode`/`note` on every target.

| Current | Target | Key | Extra |
|---|---|---|---|
| Open | Investigating / Withdrawn | `.investigate` | — |
| Investigating | Approved | `.decide` | `approvedAmount` required, text, 0 ≤ x ≤ claimed (server-checked) |
| Investigating | Rejected | `.decide` | — |
| Approved | Settled | `.settle` | label "Settled (operational status, no payment posted)" |
| Rejected / Settled | Closed | `.decide` | — |
| Withdrawn, Closed | none | — | no action |

`approvedAmount` is sent only for Approved. Confirmations use the shared premium wrapper (`window.showConfirm`, MOD-0013); no native
dialog, manual `Swal.fire`, or inline handler (form surface: gap §32.13 G-MODAL).

### 32.8 Errors, replay and concurrency (annex D187-05; localized, never raw text)

| Code (HTTP) | UI behaviour |
|---|---|
| `INVALID_REQUEST` (400), `UNSUPPORTED_MEDIA_TYPE` (415) | Localized form summary; safe field mapping only |
| `UNAUTHENTICATED` (401) | Standard session surface; JSON adapters return 401 JSON without redirect |
| `FORBIDDEN` (403) | Page: `_AccessDenied` in shell. Action: close/disable + localized denial |
| `CLAIM_NOT_FOUND` (404) | One identical safe-not-found text + support reference for unknown, foreign-scope and soft-deleted targets; action closed; list reload; hidden surfaces inert and not keyboard-reachable; late responses never re-expose them |
| `CLAIM_SHIPMENT_INELIGIBLE`, `CLAIM_CARRIER_MISMATCH`, `CLAIM_AMOUNT_INVALID`, `CLAIM_APPROVAL_AMOUNT_INVALID`, `CLAIM_APPROVED_AMOUNT_NOT_ALLOWED` (422) | Specific localized message; inputs kept |
| `INVALID_CLAIM_TRANSITION` (422) | Stale-state conflict + support reference + reload |
| `CLAIM_CORRELATION_MISMATCH` (409) | "Request no longer matches the shipment record"; no automatic new key |
| `IDEMPOTENCY_KEY_REUSED` (409) | Stop retry; new user intent required |
| `CLAIM_REFERENCE_INVALID` (502) | Reference data invalid; no retry loop |
| `CLAIM_REFERENCE_INCOMPLETE`, `CLAIM_REFERENCE_UNAVAILABLE`, `CLAIM_STORAGE_UNAVAILABLE` (503) | Temporarily unavailable; same-key retry; unknown commit never shown as rolled back |
| `INTERNAL_ERROR` (500) | Sanitized error; explicit same-intent retry |

Per intent: one pending request; the same key and **identical body text** on network/500/503 retry.
**An intent is one opened create form or transition panel, not one payload** (amended 2026-10-04, Q403). The UI mints the `Idempotency-Key` when that form or panel
opens and keeps it unchanged across edits, failures and network/500/503 retries until it closes; only a newly opened form or panel is a new
intent with a new key. A user who edits while the outcome is unknown resends under the same key, so a committed first attempt answers
409 `IDEMPOTENCY_KEY_REUSED` instead of creating a second record. On that 409 the UI stops, keeps the inputs, tells the user the
request was already received with different values and that a different request needs a new form, and never mints a key to get past it.
Measured on Returns (MOD-0186), whose create and command surfaces have this shape, by R-2 (`docs/records/audits/2026-10/mvp6-r2-returns-ui-01/evidence/traps-browser.md` §T2): a key re-minted per payload created **two Returns from one intent**; one key per opened form gave 409 and **one** Return.
A 201/200 with `idempotentReplay: true` is shown as completed, then the list reloads; a replay snapshot is never shown as current state.
The response `X-Correlation-Id` (= `error.correlationId`) is a copyable support reference, never labelled as the lifecycle root.

### 32.9 Localization, UAS-001, accessibility

Seven tenant languages (en, tr, fr, es, zh, ar, ru) in `Resources/Views/SupplyChain/Claims/ClaimsIndex.{lang}.resx`, marker class
`ClaimsIndex`, `_IndexL10n.cshtml` JSON bridge + `index.l10n.js`; mandatory keys `ClaimsTitle`, `PageDescription`, `AddNewClaims`
and column headers; shared toolbar vocabulary from `SharedResource`; no English placeholder in other languages, no hardcoded fallback; all 18 annex codes localized.
Arabic RTL; UUIDs/amounts/currency LTR-isolated; 390/768/1024/1440 without horizontal overflow; keyboard focus/Escape in offcanvas
and dialogs. UAS-001: without `.read` only `_AccessDenied` inside the shell (no title, filter, table, skeleton, button, toast or redirect);
no create CTA without `.create` + `supplychain.shipments.read`; no action without its key.

### 32.10 Owned UI paths (21, new files only), protected paths, shared handoff

Owned: `frontend/Diten.Web/Controllers/SupplyChainClaimsController.cs`; `frontend/Diten.Web/Models/SupplyChain/Claims/ClaimViewModels.cs`;
`frontend/Diten.Web/Views/SupplyChain/Claims/{ClaimsIndex.cs, Index.cshtml, _Filter.cshtml, _DataTable.cshtml, _IndexL10n.cshtml, _CreateEditOffcanvas.cshtml, _DetailsQuickView.cshtml}`;
`frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/{index.js, index.l10n.js}`;
`frontend/Diten.Web/Resources/Views/SupplyChain/Claims/ClaimsIndex.{en,tr,fr,es,zh,ar,ru}.resx`;
`frontend/Diten.Web.Tests/{Controllers/SupplyChainClaimsControllerTests.cs, Forms/ClaimFormContractTests.cs, JavaScript/ClaimIndexBehaviorTests.cs}`.
These are separate from, and add nothing to, the 47 backend owned paths of §31.

Protected for the UI writer: all backend source (including `Features/Claims/**` and service `Program.cs`), contracts/annexes,
`gateway/**`, shared layouts/partials/JS/CSS, `SharedResource.*.resx`, frontend `Program.cs`/DI, navigation/module/permission catalogues,
`frontend/Diten.Web/tests/diten-field-icons.test.js`, other modules' UI files, Golden Slim (read only), packs, registries, `.antigravity/**`,
guards, existing records and Git state.

One CT-appointed **integration owner** (the same owner for Returns when its UI revision exists) alone delivers exact diffs for:
gateway routes (**listed, not edited here**): GET+POST `/api/shipment-bundle/claims`, POST `/api/shipment-bundle/claims/{claimId}/transition`,
and confirmation of GET `/api/shipment-bundle/shipments/{shipmentId}`, explicit routes with OPTIONS (NET-001), `/claimsXYZ` not matched;
page/permission registration and G-SHIPREAD role design; tenant navigation and Ctrl+K with registry-reconciled module/page codes; shared
L10n nav keys in 7 languages; personalization confirmation; the `ICON_MAP` entries; Shipment root emission in the target; and uptake
of the accepted Claims backend (47 paths + approved `Program.cs` composition) into the integrated target.

### 32.11 Single acceptance matrix (UI) — early vertical slice first

Full row text with HTTP/browser/DB expectations, owner, evidence type and dependency is controlling in
`docs/roadmap/plans/mvp6-ui-pack-drafts-01/claims/ACCEPTANCE.md` (bound by the draft SHA256SUMS above). No row has run.

| ID | Criterion | Readiness |
|---|---|---|
| CU-VS1 | Early vertical slice: real-Auth actor resolves a Dispatched Shipment (non-null root, no carrier), creates a claim `250.00 EUR`; 201 Open with Gateway `X-Correlation-Id` = Shipment root; +1 claim/receipt/audit/Pending outbox; reload shows `250.00` | BLOCKED (target, gateway, G-SHIPREAD, seed) |
| CU-01…CU-03 | Same-origin chain; list query parity; envelope/absent fields | BLOCKED (gateway) / READY |
| CU-04…CU-06 | Exact amount text; skeleton/empty/error; UAS-001 no read | READY |
| CU-07, CU-08 | Create CTA gating (BLOCKED G-SHIPREAD); action gating matrix with 403 zero-write | BLOCKED / READY |
| CU-09…CU-13 | Create body parity; carrier link; amount/currency lexical; presence ≠ nonempty; resolve stale-response guard | READY |
| CU-14…CU-17 | Safe-not-found (create, transition); cross-LE list; eligibility display vs server 422 | READY |
| CU-18, CU-19 | Root seam (BLOCKED producer uptake); idempotency (BLOCKED DN-01) | BLOCKED |
| CU-20…CU-26 | Approval amount; stale transition; Settled wording; support ref vs root; 7 languages + RTL; responsive + keyboard; premium dialogs/no secrets | READY |
| CU-27, CU-28 | Family routing `/claimsXYZ`; regression incl. Claims backend suites on target | BLOCKED |
| CU-29 | Source→binary→process→browser binding | READY |
| CU-30 | PNG via supported export only | BLOCKED (PRES-183-04) |
| CU-31 | QuickView without by-ID request | READY |

OUT at the start (scope-change rows, approved by the owner decision above): CU-SCR-01 checkbox + BulkActionBar; CU-SCR-02 edit/delete/bulk
delete (incl. QuickView Edit button); CU-SCR-03 import/export; CU-SCR-04 server paging/search/sort; CU-SCR-05 multi-select status;
CU-SCR-06 generic `verify_datatable_page.py --reference slim` as acceptance (run and kept as a record only).

### 32.12 Test expectations (UI)

Module tests in the three owned test files; `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module Claims --reference slim`
run and recorded (CU-SCR-06); `quality-gate-datatable` result recorded against the bounded profile; RESX parity 7/7 with no placeholder;
build of frontend, gateway and SupplyChain service on the integrated target; real-Auth browser smoke with three identities and separate
profiles; `grep` scans for native dialogs, inline handlers and "Permission denied"/"Forbidden" text; independent VER on a frozen source.

### 32.13 Remaining gaps (recorded, not decided here)

- **Approved-amount list gap:** `ClaimSummary` has no `approvedAmount`; it shows only in the transition success message. Contract change not authorized.
- **G-MODAL:** MOD-0013 §4 requires `window.showConfirm` and forbids manual `Swal.fire` on layout pages; whether it can host the transition inputs is unverified. If not, a module-owned form surface (one more owned partial) needs a scope amendment.
- **G-ICONMAP:** FORM template requires an icon per field registered in shared `diten-field-icons.test.js`; that edit belongs to the integration owner.
- **G-DATETIME:** `occurredAt` needs date-time with offset; the shared date field is date-oriented; component choice is a Phase 1.5 item.
- **Verifier tension:** module-pack standard §15 expects DataTable verifier PASS; the approved OUT rows make the generic verifier record-only until a scope-aware profile (DN-02 / PRES-183-03) exists.
- G-TARGET integrated target; Claims backend absent from the common checkout; DN-01 retry policy; producer root uptake; PNG; nav/personalization codes; Loads 3.1.0 forward drift of the SHIPMENT-BUNDLE pin (closed by the Q380 re-pin to 3.1.0); `module-implementation-status.md` update is a separate path.

### 32.14 Effort and authorization boundary

O/M/P (person-hours, replacement of 0187-1/4-REMAINING and the UI sub-items of 0187-5/6-REMAINING, not additive): pack/Phase 1.5 4/8/16;
frontend 30/50/84; shared integration 8/16/28; independent UI VER 14/24/40; **total 56/98/168**; with the non-UI remainders 68/118/200
(net +6.8/+16/+14.4 against the old rows). Joint delivery with Returns lowers the shared parts (draft README).

Authorized UI work (owner decisions of 2026-09-26): Phase 1.5 table PH15-UI-187 (`docs/records/decisions/2026-09/mvp6-claims-ui-ph15-owner-decision-01.md`,
SHA-256 `95a5c4f40d1a912c29b95d82c5c83fc817ed9fef578d885ae064e480de30f74e`); module-first delivery in an isolated environment with integration at the end
(`docs/records/audits/2026-09/mvp6-ct-owner-decisions-modules-first-2026-09-26.md`, SHA-256 `63e8601ec5bf28225fa0793e1a882e533139adc4696c4af2fa32ebb378d9df32`);
DRAFT code overlays written in chat lanes and built, tested and runtime-verified later on the Mac, not writer-complete
(`docs/records/decisions/2026-09/mvp6-draft-overlays-owner-decision-01.md`, SHA-256 `050336246812218e2f22bb9cfb40529b6969d1ab96717113599a7efbaf8da67c`).
UI code is written only through a CT-dispatched lane; applying it to the shared target waits for the final integration.

Not authorized by this section: gateway, permission, navigation, L10n or icon-map edits except by the single integration owner; contract
changes; `done` status; commit or push.

## 33. Self-registration

Approved: `docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A; pack preparation only). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md).
**Foundation:** DCP-009 §21 (interface, hosted service, options, `PlatformRegistration` settings via the existing `X-Internal-Api-Key`, project reference and `Program.cs` lines — single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `claims-management` / `ClaimsManagement` / `Claims Management` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, seed-once) | 430 / `bx-receipt` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/ClaimsManagementManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/ClaimsManagementManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `CLAIMS` | Claims | `/SupplyChain/Claims` (approved in §32.4; not a route yet) | `supplychain.claims.read` | — | **true** | List | 10 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|---|
| `CLAIMS` | `CREATE` | Create Claim | `supplychain.claims.create` | Toolbar | no |
| `CLAIMS` | `INVESTIGATE` | Investigate | `supplychain.claims.investigate` | RowAction | no |
| `CLAIMS` | `WITHDRAW` | Withdraw | `supplychain.claims.investigate` | RowAction | **yes** |
| `CLAIMS` | `APPROVE` | Approve | `supplychain.claims.decide` | RowAction | no |
| `CLAIMS` | `REJECT` | Reject | `supplychain.claims.decide` | RowAction | **yes** |
| `CLAIMS` | `SETTLE` | Settle (no payment posted) | `supplychain.claims.settle` | RowAction | no |
| `CLAIMS` | `CLOSE` | Close | `supplychain.claims.decide` | RowAction | no |

Existing keys only: `ClaimPermissions` constants `Read`, `Create`, `Investigate`, `Decide`, `Settle` (accepted isolated source, `normal-source.tar.gz` `edb759a0…`). Pages and buttons mirror the §32 GoldenReferenceSlim tenant UI scope.

### Navigation keys (NAV-L10N-KEYS.tsv; 0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Module.CLAIMSMANAGEMENT` | en, tr, fr, es, zh, ar, ru | this provider |
| `Nav.Page.CLAIMS` | en, tr, fr, es, zh, ar, ru | this provider |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `ClaimPermissions` |
| M-03 | Every reflected key is in the manifest or on the API-only allow-list with a reason: none expected (every constant is modeled; any addition needs a reason) |
| M-04 | Manifest RoutePaths = the frontend view-route set of `SupplyChainClaimsController` (§32.4; to be built), counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target (Q14/Q15), never ahead of it.

### Open gaps (carried, not solved)

1. `ClaimPermissions.cs` exists only in the accepted isolated source, not in the common checkout.
2. `CREATE` and every row action also need `supplychain.shipments.read` (G-SHIPREAD); the single-key action model cannot express that conjunction.
3. The Claims UI is approved (§32) but not built; the route and actions are re-checked against the built UI (M-04/M-05, W-01/W-02).
4. Runtime tests R-02…R-04 need a native executor and the integrated target.
