# MOD-0188 contract needs (consumer assessment)

This is a consumer needs list, not a contract amendment or product decision. MOD-0188
does not treat an approved file batch or `ReadyToShip` as accepted demand history.
AC-D05 and AC-D06 remain open while the additive 3.3.0 amendment awaits producer
and Ali approval; this document does not freeze that amendment or close any AC.

## 1. MOD-0183 SHIPMENT-BUNDLE

Current baseline: `origin/main:docs/analysis/contracts/shipment-bundle.openapi.yaml`
at `4aeae0671e0aea881573ab473fa319a69bfb99df` declares OpenAPI 3.1.0,
`info.version: 3.2.0`, `x-status: FROZEN`, and `x-contract-version: v1`.
PR #134 (`a814bad0f`) brought the producer work into main. OpenAPI format,
bundle version and payload `contractVersion` are different values. The additive
3.3.0 is **in preparation, not approved or FROZEN**. Field labels below that
are absent from 3.2.0 are consumer needs for the producer to name and review;
they are not claims that the fields already exist.

| Consumer need (14 rows) | Business meaning and 3.2.0 evidence | 3.3.0 disposition | AC |
|---|---|---|---|
| `tenantId` in response | Trusted JWT supplies tenant scope today; `ShipmentDetail`/`ShipmentResponse` does not return the resolved tenant. MOD-0188 needs it on the realized-line response to prevent cross-tenant joining. | 3.3.0'da — additive response scope field; server-derived and checked against the request context. | D05–D06 |
| `legalEntityId` in response | `X-Legal-Entity-Id` arrives with the 3.2.0 request and is checked against active same-tenant MDM reference; it is not a response field. MOD-0188 needs the verified company in the realized-line response. | 3.3.0'da — additive response scope field; do not reinterpret the request hint as proof. | D05–D06 |
| `shipmentId` | Root shipment identity already appears in `ShipmentSummary`/`ShipmentResponse`; it cannot identify one line by itself. | referans — use existing 3.2.0 root ID as parent. | D05–D06 |
| `shipmentLineId` | Immutable realized-line identity across reads/retries is absent. Existing `ShipmentLine.lineNumber` is a line label, not this identity. | 3.3.0'da — new additive line ID; preserve `lineNumber` meaning. | D05–D06 |
| File `sourceRecordKey` link | File rows carry `sourceRecordKey`; 3.2.0 has no producer-approved match to a realized line or source namespace. Ambiguous/unmatched rows stay quarantined. | 3.3.0'da — direct ID correspondence or auditable mapping, including source-system/namespace boundary. | D05 |
| `sourceVersion` | A correction/version marker is needed to reject stale source content. Existing `ShipmentDetail.contractVersion: v1` is a schema marker, not a line-content version. | 3.3.0'da — producer-approved source version semantics. | D05–D06 |
| Line realization state | Existing `ShipmentStatus` includes root-level `Dispatched`; it does not prove each line's realized state. `ReadyToShip` is not realized dispatch. | 3.3.0'da — new line-level realization enum and eligible-state rule; candidate field name is not frozen. | D05–D08 |
| Line `realizedAt` | `TransitionShipmentCommand.occurredAt` is a request field; 3.2.0 `ShipmentDetail` has no persisted line-level realized instant. | 3.3.0'da — authoritative line instant with time-zone interpretation. | D06–D08 |
| Planning Warehouse mapping | `ShipmentDetail.warehouseReferenceId` is opaque; it cannot yet prove a canonical planning Warehouse identity. | bağımlılık — WAREHOUSE-SHIPMENT-TRIGGER is owned by center/MVP-5; 3.3.0 cannot resolve this mapping alone. | D05–D08 |
| `skuId` | `ShipmentLine.skuId` exists in 3.2.0; MDM identity remains the authority. | referans — consume existing 3.2.0 SKU reference. | D05–D08 |
| `realizedQuantity` | Actual dispatched amount must be distinct from planned amount. Existing `ShipmentLine.quantity` is not declared an immutable realized amount. | 3.3.0'da — new additive realized quantity; do not change `quantity` meaning. | D05–D08 |
| Original UoM and conversion rule reference | `ShipmentLine.uomId` exists. MOD-0188 needs original unit and a reference to the MDM-owned conversion rule/version; Shipment does not own the factor or conversion algorithm. | 3.3.0'da — additive MDM rule reference only; base-unit conversion authority remains MDM. | D06–D08 |
| Shipment-line cancellation/correction link | Cancellation/correction has its own identity, source version, kind and link to the original realized `shipmentLineId`; never overwrite the original. 3.2.0 has no such line link. Return linkage is separate. | 3.3.0'da — shipment-line cancellation/correction only; MOD-0186 return linkage ertelendi pending contract-conflict resolution. | D05–D08 |
| Canonical same-content comparison | Same scoped line identity plus same agreed business content has one effect and two lineage entries; different content is quarantined. 3.2.0 does not define comparison fields/version. | 3.3.0'da — producer-reviewed canonical field set and content-version rule. | D05 |

**Kapsam dışı:** Stockout/unmet-demand evidence is not a Shipment-bundle 3.3.0
addition. An independent authoritative producer must establish it; zero shipped
quantity or an absent line is never silently interpreted as zero demand.
**Ertelendi:** MOD-0186 return-to-original-line linkage needs its own contract
resolution. Existing `/returns` and `ReturnLine.shipmentLineNumber` do not settle it.

The existing `warehouse-outbound.openapi.yaml` is a `ReadyToShip` trigger, not proof
of realized dispatch. `outboundId`, `orderLineId`, `shipmentId`, `lineNumber` and
`lifecycleCorrelationId` must not silently become realized-line identities.
MOD-0183/MVP-6 prepares the additive 3.3.0 producer amendment; Ali controls
approval/freeze. Until then AC-D05 and AC-D06 remain open. No MOD-0183 or MVP-6
code or contract is changed here.

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

## 3. Accepted demand history: approved stages and open integration

`DemandHistoryImportBatch` persists raw/validated file rows, quarantine and an
independent batch review decision. Its code explicitly says review does **not**
accept rows into demand history. The manual `DemandRevisionDraft` stores planning
week values, edits and review state; it is a future plan, not observed historic
dispatch. The current fixture forecasting core does not establish a live accepted
history store. Ali's approved first stage permits a **separate**, audited
acceptance of verified file rows; batch approval alone is not row acceptance.
Forecast may consume only that accepted, as-of-bounded snapshot. The second
stage joins file and live realized Shipment rows by a producer-approved identity
with one business effect, two lineage entries and conflict quarantine. The live
join awaits approved 3.3.0 and its Warehouse dependency. The Shipment amendment
does not decide stockout evidence or MDM-owned unit conversion. AC-D05 and
AC-D06 remain open.

## 4. Demand v2 producer draft

`services/Diten.PlanningService/contracts/drafts/demand-v2.openapi.yaml` and
`demand-v2.events.schema.json` remain MOD-0188-owned **DRAFT** files. OpenAPI
describes manual Draft creation, scoped read, week edit, review/approval/rejection,
reopen and series exclusion; current Published-by-period, revision status,
manifest and revision-fixed row pages; restricted Invalidated historical audit
read; rollback Draft creation, publish and invalidate. The event draft requires
Published, separate Superseded when replacing a baseline, and Invalidated intents; events carry
revision identity, scope, state version and integrity summary, not 52-week rows.

MOD-0188 produces snapshot/status and event intent. MOD-0189 owns MRP run binding,
period choice, status rechecks, result/transfer safety and reconciliation. An
event is not the authoritative status; a Superseded content read needs approved
bound-run evidence. The canonical checksum definition is in the owned DRAFT;
the current HTTP projection still lacks fields for independent recomputation.
Open work includes cursor encoding/expiry, idempotency retention, outbox delivery,
the separate Superseded outbox implementation, live Platform/MDM scope and central
contract freeze/publication. Frozen Demand v1 is unchanged.
