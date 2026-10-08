# MOD-0188 contract needs (read-only assessment)

This is a consumer needs list, not a contract amendment or product decision. MOD-0188
does not treat an approved file batch or `ReadyToShip` as accepted demand history.
AC-D05-D08 remain open. The producer owner and central Control Tower must agree on
field names, semantics and publication before file/live reconciliation can be accepted.

## 1. MOD-0183 SHIPMENT-BUNDLE

Compared with `origin/feature/mvp6-logistics:docs/analysis/contracts/shipment-bundle.openapi.yaml`
at `e785d6367878fbade360d11ba3e5edc50993d1d6`: OpenAPI 3.1.0, `info.version`
3.1.0, `x-status: FROZEN`, `x-contract-version: v1`. The last two values are distinct
from a proposed, not yet approved 3.2.0 additive amendment. Paths below refer to
the 3.1.0 file. Proposed names are discussion labels, not approved producer fields.

| Needed field / proposed name | Type | Meaning | Required for reconciliation? | Example | AC | Present in 3.1.0? |
|---|---|---|---|---|---|---|
| `tenantId` | UUID | Authoritative tenant scope, server-derived | Yes | `aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa` | D05-D08 | Server JWT context stated in `info.description`; not a `ShipmentDetail` field |
| `legalEntityId` | UUID | Authoritative company scope, server-derived | Yes | `bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb` | D05-D08 | Server JWT context stated in `info.description`; not a `ShipmentDetail` field |
| `shipmentId` | UUID | Shipment root; insufficient alone as a line key | Yes, as parent | `18300000-0000-0000-0000-000000000001` | D05-D06 | `ShipmentSummary.shipmentId` |
| `shipmentLineId` | UUID or producer-approved opaque ID | Immutable realized shipment *line* identity across reads and retries | Yes | `line-183-1` | D05-D06 | No; `ShipmentLine.lineNumber` exists, but is not declared immutable business identity |
| `sourceRecordKey` mapping | Producer-approved ID or auditable mapping reference | Match imported file row to exactly one realized line; unmatched or ambiguous rows stay quarantined | Yes when joining file/live | `line-183-1` | D05 | No; `ShipmentLine` has no file key or authoritative mapping contract |
| `sourceVersion` | Monotonic integer or opaque revision | Detect source corrections and prevent stale overwrite | Yes | `2` | D05-D06 | No; `ShipmentDetail.contractVersion` is schema version `v1`, not source revision |
| `realizationStatus` | Enum with explicit eligible states | Prove that a line was actually dispatched, not merely ready to ship | Yes | `Dispatched` | D05-D08 | `ShipmentStatus` contains `Dispatched`, but only at shipment root; no line-level realization rule |
| `realizedAt` | UTC date-time | Business-effective dispatch instant, converted by approved calendar/time-zone rule | Yes | `2026-09-20T08:15:00Z` | D06-D08 | `TransitionShipmentCommand.occurredAt` exists on request; no authoritative persisted line-level realized instant in `ShipmentDetail` |
| `warehouseId` / canonical mapping | Producer-approved Warehouse ID | Resolve the authorized planning warehouse; reject ambiguous references | Yes | `wh-01` | D05-D08 | `ShipmentDetail.warehouseReferenceId` is an opaque reference pending warehouse trigger contract; canonical mapping absent |
| `skuId` | UUID | Shipped SKU | Yes | `c3d4e5f6-0000-0000-0000-000000000002` | D05-D08 | `ShipmentLine.skuId` |
| `realizedQuantity` | Decimal string | Actual dispatched amount, distinct from planned amount | Yes | `30.000` | D05-D08 | `ShipmentLine.quantity` exists; contract does not say it is immutable realized quantity after dispatch |
| `uomId` and base-UoM conversion lineage | String plus approved conversion reference/version | Convert to MDM base unit without losing original amount | Yes | `EA`, `uom-rule-v2` | D06-D08 | `ShipmentLine.uomId` exists; conversion factor, rule ID and version absent |
| `recordKind` and `relatedShipmentLineId` | Enum + line ID | Separate cancellation/correction/return effect from original line; never overwrite original | Yes when applicable | `Correction`, `line-183-1` | D05-D08 | No line-level correction link. `/returns` and `ReturnLine.shipmentLineNumber` exist, but MOD-0186 return boundary needs owner agreement |
| `canonicalContentVersion` / comparison fields | Version + agreed field set | Decide same key/same business content versus same key/conflicting content | Yes | `v1:sku,warehouse,time,qty,uom,kind` | D05 | No canonical comparison rule for MOD-0188 reconciliation |
| stockout / unmet-demand evidence | Separate authoritative source ID and quantity/state | Distinguish zero shipment from zero demand or unknown lost sales | Yes before interpreting absent sales | `unknown` | D06-D08 | No; Shipment bundle is not an authority for historic stockouts or unmet demand |

The existing `warehouse-outbound.openapi.yaml` is a `ReadyToShip` trigger, not proof
of realized dispatch. `outboundId`, `orderLineId`, `shipmentId`, `lineNumber` and
`lifecycleCorrelationId` must not silently be promoted to canonical realized-line
identities. MOD-0183 owns the producer amendment; MOD-0186 must confirm return
semantics. The producer and MOD-0188 consumer must review an additive amendment
before central freeze/merge. No MOD-0183 or MVP-6 code or contract is changed here.

## 2. Platform: "my authorized companies"

MOD-0188 needs a token-bound, server-authoritative complete LegalEntity set for
single selection. At each operation, Platform must resolve current active,
effective-dated user Position assignments, their Organization Units and valid
LegalEntity references, apply exclusions and revocations, and return only the
companies this actor may select. Demand action permission is a separate RBAC check;
an ID from a form/header or an earlier list response is not authorization. Empty,
stale or unavailable scope must fail closed. Tenant and canonical identity must be
checked independently; no unapproved alias rewrite is allowed.

`services/Diten.Platform/src/Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs`
already resolves active assignments, Positions and own Organization Units, and
emits LegalEntity scope only after MDM reference validation. It skips archived
nodes, returns no scope for missing assignments, and does not infer action
permissions from Position. It is **not** the approved token-facing complete-company
endpoint. Its LegalEntity calculation uses the assigned Position's own Org Unit,
not every descendant Org Unit; no MOD-0188 exclusion result or revocation-proof
contract is shown there. The central owner must define the endpoint, complete set,
exclusion semantics, freshness/error signaling and consumer trust boundary. MDM
referenceability alone cannot grant user access. No Platform endpoint is designed
or implemented by this work pack.

## 3. Accepted demand history: product decision pending

`DemandHistoryImportBatch` persists raw/validated file rows, quarantine and an
independent batch review decision. Its code explicitly says review does **not**
accept rows into demand history. The manual `DemandRevisionDraft` stores planning
week values, edits and review state; it is a future plan, not observed historic
dispatch. The current fixture forecasting core does not establish a live accepted
history store. The source identity, correction handling, stockout evidence and
base-UoM lineage must be settled before acceptance.

| Product option (not selected) | Effect / unresolved risk |
|---|---|
| Approved import rows | Can onboard historical files before live shipment integration, but batch approval alone is not row acceptance; identity, correction, UoM, quarantine resolution and audit need a separate acceptance gate. |
| MOD-0183 realized shipment lines | Gives producer-owned live lineage after its contract amendment, but cannot reconstruct pre-integration history or treat missing shipments as zero demand. |
| Both, reconciled | Covers file and live periods; requires canonical line identity/mapping, source version, same-content rule, dual lineage, conflict quarantine and non-duplication under retry/race. |

Ali/product owner must decide which source(s) can become accepted history, the
acceptance transition and how unmet demand/stockouts are represented. This document
does not choose among them or close AC-D05-D08.

## 4. Demand v2 producer draft

`services/Diten.PlanningService/contracts/drafts/demand-v2.openapi.yaml` and
`demand-v2.events.schema.json` remain MOD-0188-owned **DRAFT** files. OpenAPI
describes manual Draft creation, scoped read, week edit, review/approval/rejection,
reopen and series exclusion; current Published-by-period, revision status,
manifest and revision-fixed row pages; restricted Invalidated historical audit
read; rollback Draft creation, publish and invalidate. The event draft proposes
Published and Invalidated events plus a candidate Superseded shape; events carry
revision identity, scope, state version and integrity summary, not 52-week rows.

MOD-0188 produces snapshot/status and event intent. MOD-0189 owns MRP run binding,
period choice, status rechecks, result/transfer safety and reconciliation. An
event is not the authoritative status; a Superseded content read needs approved
bound-run evidence. Open decisions include checksum canonicalization, cursor
encoding/expiry, idempotency retention, outbox delivery, Superseded notification,
live Platform/MDM scope, and central contract freeze/publication. No v2 draft
file or frozen Demand v1 file is changed here.
