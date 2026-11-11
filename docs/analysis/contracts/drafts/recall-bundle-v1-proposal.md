# RECALL-BUNDLE v1 — proposal (MOD-0177 Recall Readiness)

> **Status: PROPOSAL (draft) — not a contract.** No recall contract exists in `docs/analysis/contracts/` (no root file, no
> draft, no README row; "RECALL" occurs nowhere under `docs/analysis/contracts/`), although the inventory report marks
> RECALL-BUNDLE "✓" (`inventory-capability-scope-and-dependency-report.md:232`: "Recall trace | MOD-0177 | evidence |
> RECALL-BUNDLE ✓ | —"). After CT acceptance, R1 writes this as `docs/analysis/contracts/recall.openapi.yaml`
> (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0177`, `x-owner-service: Diten.InventoryService`) in ONE
> single-writer step and adds the README row. After the freeze only additive changes are allowed (K16).
>
> **Source:** pack `execution/domains/supply-chain-execution/module-packs/MOD-0177-recall-readiness.md` (§8 is the behaviour
> authority; this file is the wire shape). Owner decisions KR-A…KR-H (2026-10-13). Open CT decisions CT-R1…CT-R18 (pack §19.5)
> are marked where they change the wire.
>
> **Frozen contracts consumed and not changed here:** INVENTORY-BUNDLE 1.2.1 (+ 1.3.0 proposed by 0176 F2), TRACE 1.3.0
> (+ 1.4.0 proposed by 0176 F2), QUARANTINE 1.2.0, ESIGN-BUNDLE 1.0.1, FEFO-BUNDLE 1.0.0, SHIPMENT-BUNDLE 3.2.0 (MVP-6). The
> additive changes 0177 needs from other owners are in §9 and are **not** part of this contract.

## 1. Rules

1. Server `/api/recall` (Gateway → Diten.InventoryService **5067**, new route INT-RECALL). Tenant shell callers only
   (`actor_type = tenant_user`).
2. `TenantId` from the token. Company from header `X-Legal-Entity-Id` (W-0173-02), validated against MDM. Another company →
   403 `CROSS_LE_FORBIDDEN`; another tenant / unknown id → 404. Every read and write is scoped to tenant + company.
   Exception: `getLotRecallStatus` answers for the lot in every company of the tenant (a recalled lot is recalled
   everywhere — CT-R7) but returns only case number / status, never case content of another company.
3. Envelope: InventoryService `Response<T>` (`{ data, error: { code, message, details } }`).
4. Every write takes the `Idempotency-Key` header, equal to body `idempotencyKey` (8–100). Same key + same body → first
   result (200). Same key + different body → 400 `IDEMPOTENCY_KEY_REUSED`.
5. Mutable records (case, drill plan, drill run, settings) need `If-Match: "<version>"` on change (`"0"` creates the first
   settings row). Stale → 409 `RECALL_CONFLICT`. Append-only records (notification, consumption, return link, segregation,
   comments of the timeline) take no If-Match.
6. **This contract moves no stock, writes no lot, no genealogy link, no hold decision, no shipment, no return, no
   signature.** Its consequences on other modules happen in-process: quarantine requests through MOD-0175's command (pack
   §8.4), the exit block inside INVENTORY `postMovement` (pack §8.5, §9.1 here).
7. Every command writes an audit row (pack §8.12) and a case / run event where relevant. Signatures are MOD-0022 references
   only (`signatureId`).
8. No new stock status or movement type is defined.
9. Quantities are decimal strings in the SKU's base unit; quantities of different SKUs / units are never summed.
10. Dates: `date` = calendar day (UTC), `date-time` = UTC instant.

## 2. Schemas

### 2.1 `TraceRequest` / `TraceResult`
```json
// request (query or body)
{ "rootLotId": "string", "direction": "Backward | Forward | Both", "asOfUtc": "date-time|null" }
// result
{ "rootLotId": "…", "rootLotNumber": "LKT-25K004", "direction": "Both", "asOfUtc": "…",
  "computedAtUtc": "…", "durationMs": 1800, "truncated": false,
  "limits": { "maxDepth": 20, "maxNodes": 5000 },
  "nodes": [ TraceNode ], "edges": [ { "from": "nodeKey", "to": "nodeKey", "kind": "Genealogy | Holds | Issued | DeliveredTo | Input",
             "transformationReference": "…|null", "quantityConsumed": "…|null", "uomId": "…|null", "cycle": false } ],
  "gaps": [ TraceGap ],
  "summary": { "finishedGoodLots": 2, "stock": [ { "skuId": "…", "baseUomId": "EA", "warehouses": 2, "quantity": "750" } ],
               "shipped": [ { "skuId": "…", "baseUomId": "EA", "customers": 4, "quantity": "1150" } ],
               "inTransitShipments": 1, "gapCount": 0, "otherCompanyActivityCount": 0 },
  "canonicalVersion": "rt-v1", "sha256": "hex" }
```

### 2.2 `TraceNode`
```json
{ "nodeKey": "stable string (kind + ids)", "parentKey": "…|null", "level": 2,
  "kind": "RawMaterialLot | FinishedGoodLot | Lot | Input | Stock | Shipment | Customer | Gap | Disposal",
  "lot": { "lotId": "…", "lotNumber": "…", "status": "Draft|Released|Blocked|Expired", "trackingMode": "Lot|LotAndSerial" } ,
  "item": { "itemId": "…", "skuId": "…", "skuLevel": "…", "code": "…", "name": "…", "lotControlled": true },
  "untracked": false,
  "warehouse": { "warehouseId": "…", "code": "D01", "name": "…" }, "locationId": "…|null", "stockStatus": "AVAILABLE|…|null",
  "holdId": "…|null",
  "quantity": "200", "baseUomId": "EA",
  "movement": { "transactionId": "…", "transactionNumber": "HRK-…", "movementType": "…", "postingDate": "…",
                "sourceModule": "…", "sourceDocumentId": "…|null" },
  "shipment": { "shipmentId": "…", "read": "Ok | Forbidden | NotFound | Unavailable | NotApplicable",
                "status": "Dispatched|InTransit|Delivered|…|null", "actualDeliverAt": "…|null", "shipToReference": "…|null" },
  "customerRef": "…|null", "serials": [ "…" ],
  "badges": [ "OnHand | Quarantined | Blocked | Shipped | InTransit | Delivered | Returned | Untracked | Gap | CaseRoot" ],
  "scope": { "included": true, "exclusionReason": "…|null", "new": false, "outOfScopeFinding": false } }
```
`untracked: true` = "Takip dışı" (input SKU with `lotControlled = false`), never a gap. `scope` is present only in case reads.

### 2.3 `TraceGap`
```json
{ "code": "LOTLESS_OUTBOUND | UNRESOLVED_TYPED_LOT | MISSING_GENEALOGY_LINK | CONSUMED_WITHOUT_OUTPUT_LINK | NO_CUSTOMER_REFERENCE | LIMIT_REACHED | OTHER_COMPANY_ACTIVITY",
  "nodeKey": "…", "transactionId": "…|null", "transactionNumber": "…|null", "skuId": "…|null",
  "quantity": "…|null", "baseUomId": "…|null", "date": "…|null", "text": "…" }
```

### 2.4 `CaseRef` (list row)
```json
{ "caseId": "…", "number": "RC-2026-0004", "type": "Real | Drill", "status": "Draft | PendingScopeApproval | Active | EffectivenessCheck | Closed | Cancelled",
  "class": 2, "level": "Wholesale | Pharmacy | Patient", "product": { "skuId": "…", "name": "…" },
  "rootLot": { "lotId": "…", "lotNumber": "…" }, "openedOn": "date", "owner": { "userId": "…", "name": "…" },
  "affectedQuantity": [ { "skuId": "…", "baseUomId": "EA", "quantity": "2100" } ],
  "progress": [ { "skuId": "…", "baseUomId": "EA", "returned": "180", "shipped": "1150", "percent": "15.7" } ],
  "drillResult": { "passed": true, "durationMinutes": 102 } }
```
Drill rows (`type: Drill`) come from completed / cancelled drill runs (union read, pack §8.10); `drillResult` only for them.

### 2.5 `CaseDetail` = `CaseRef` +
```json
{ "reason": "…", "reasonReference": "…|null", "direction": "Both", "scopeRevision": 1,
  "submittedSnapshot": SnapshotRef|null, "approvedSnapshot": SnapshotRef|null,
  "scopeSubmittedBy": UserAt|null, "scopeApprovedBy": UserAt|null, "scopeApprovalSignatureId": "…|null",
  "effectiveness": EffectivenessView|null, "effectivenessCompletedBy": UserAt|null, "effectivenessSignatureId": "…|null",
  "closure": { "summary": "…", "openQuantityAcceptance": "…|null", "by": UserAt, "signatureId": "…|null" } | null,
  "cancellation": { "reason": "…", "by": UserAt } | null,
  "pendingAction": { "action": "ApproveScope | CompleteEffectiveness | Close", "userId": "…", "expiresAtUtc": "…" } | null,
  "counts": { "stockLines": 3, "quarantineRequests": { "Held": 1, "Pending": 0, "BlockedByReservation": 1, "NotNeeded": 1, "Failed": 0 },
              "customers": 4, "notified": 2, "returnsLinked": 1, "outOfScopeFindings": 0 },
  "version": 7 }
```
`SnapshotRef` = `{ snapshotId, revision, purpose, sha256, computedAtUtc, truncated, gapCount }`; `UserAt` = `{ userId, name, atUtc }`.

### 2.6 Tab payloads
- `ScopeView`: `{ revision, snapshot: TraceResult (nodes with scope), excludedCount, editable: bool }` (Draft: live trace +
  marks; later: approved snapshot).
- `ScopeDiff`: `{ approvedSha256, liveSha256, added: [TraceNode], removed: [nodeKey], computedAtUtc }`.
- `StockLine`: `{ nodeKey, warehouse, locationId, lot, quantity, baseUomId, liveOnHand: [ { stockStatus, quantity } ],
  request: QuarantineRequest|null, segregation: { by, atUtc, note }|null }`.
- `QuarantineRequest`: `{ requestId, status: Pending|Held|PartiallyHeld|BlockedByReservation|NothingAvailable|Failed|NotNeeded,
  notNeededReason: AlreadyQuarantined|AlreadyBlocked|LotBlocked|LotExpired|Excluded|null, holdIds: [ … ],
  holdStatuses: [ { holdId, status } ] (read live from MOD-0175), heldQuantity, error: { code, message, details }|null,
  reservations: [ { reservationId, sourceModule, sourceDocumentId, remainingQuantity } ], attempts, lastAttemptAtUtc }`.
- `CustomerRow`: `{ customerRef, label, shipments: [ { shipmentId|sourceDocumentId, number, read, status, actualDeliverAt,
  lotId, lotNumber, quantity, baseUomId, transactionNumber } ], notification: Notification|null, consumption: [ … ],
  returned: [ { skuId, baseUomId, quantity } ] }`.
- `Notification`: `{ notificationId, customerRef, notifiedOn, channel: Phone|ExternalEmail|Fax|InPerson|Other, note,
  recordedBy: UserAt, supersedesId }`.
- `ConsumptionDeclaration`: `{ declarationId, customerRef, skuId, skuLevel, baseUomId, quantity, declaredOn, evidence,
  recordedBy: UserAt, supersedesId }`.
- `ReturnLink`: `{ linkId, source: Mod0186Return|ManualCustomerReturn, returnReference, transactionId, transactionNumber, lot,
  skuId, quantity, baseUomId, toStockStatus, holdId, customerRef, linkedBy: UserAt }`.
- `ReturnCandidate`: `{ transactionId, transactionNumber, postingDate, lot, skuId, quantity, baseUomId, toStockStatus,
  holdId, sourceModule, sourceDocumentId, eligible: bool, ineligibleReason: NotQuarantined|NotInScope|BeforeApproval|AlreadyLinked|null }`.
- `EffectivenessView`: `{ class, targetPercent, rows: [ { customerRef, skuId, baseUomId, shipped, returned, consumed, open,
  percent, met, notified } ], totals: [ { skuId, baseUomId, shipped, returned, consumed, open, percent, met } ], computedAtUtc }`.
- `TimelineEntry`: `{ seq, kind, atUtc, actor: { userId, name }|"system", text, refs: {…}, signatureId }`.

### 2.7 `DashboardView`
```json
{ "openCases": { "total": 2, "byClass": { "1": 0, "2": 1, "3": 1 } },
  "lastDrill": { "runId": "…", "number": "RC-2026-D011", "durationMinutes": 102, "targetHours": 4, "met": true } | null,
  "lotCoverage90d": { "percent": "99.9", "withLot": 1522, "total": 1524 },
  "gaps90d": { "count": 2, "transactionNumbers": [ "HRK-2026-08877", "HRK-2026-08902" ] },
  "returnedOpenCases": [ { "caseNumber": "…", "skuId": "…", "baseUomId": "EA", "quantity": "280" } ],
  "drillHistory": [ { "number": "…", "endedOn": "date", "lotNumber": "…", "durationMinutes": 168, "met": true } ],
  "drillFrequency": { "lastCompletedOn": "date|null", "daysSince": 129, "warningDays": 180, "overdue": false },
  "openCaseRows": [ CaseRef ] }
```

### 2.8 `DrillPlan`, `DrillRun`
```json
{ "planId": "…", "code": "DP-01", "name": "…", "frequency": "Yearly1 | Yearly2 | Quarterly | CustomDays", "customDays": null,
  "scopeKind": "RandomFinishedGoodLot | RawMaterialLot | SelectedLot", "nextDueOn": "date", "owner": { "userId": "…", "name": "…" },
  "active": true, "lastRun": { "number": "…", "passed": true, "durationMinutes": 102 } | null, "version": 2 }
{ "runId": "…", "number": "RC-2026-D012", "planId": "…", "status": "Running | Completed | Cancelled",
  "selectionMode": "Auto | Manual", "lot": { "lotId": "…", "lotNumber": "…" },
  "startedAtUtc": "…", "steps": [ { "step": "Trace | Scope | CustomerList", "completedAtUtc": "…|null" } ],
  "endedAtUtc": "…|null", "durationMinutes": null, "elapsedMinutes": 37, "targetHours": 4, "passed": null,
  "gapCount": 0, "snapshot": SnapshotRef|null, "summary": "…", "customers": [ { "customerRef": "…", "shipment": "…", "quantity": "…", "baseUomId": "…" } ],
  "resultNote": "…|null", "resultSignatureId": "…|null", "version": 3 }
```
`elapsedMinutes` is computed by the server at read time (clock never trusted from the client).

### 2.9 `EvidencePreview`, `EvidenceExport`
```json
{ "subject": { "kind": "Case | DrillRun | Period", "caseId": "…", "runId": "…", "from": "date", "to": "date" },
  "company": { "legalEntityId": "…", "name": "…" }, "generatedAtUtc": "…",
  "sections": {
    "1_summary": { … case or drill summary … },
    "2_trace": { "snapshot": SnapshotRef, "rows": [ { "kind", "lotNumber", "place", "quantity", "baseUomId", "document", "date" } ] },
    "3_quarantine": [ QuarantineRequest ],
    "4_customers": [ CustomerRow (notification only) ],
    "5_returnsAndEffectiveness": { "returns": [ ReturnLink ], "effectiveness": EffectivenessView },
    "6_timeline": [ TimelineEntry ],
    "7_signatures": [ { "action": "…", "signatureId": "…", "signer": "…", "meaning": "…", "signedAtUtc": "…" } ] } }
```
Drill evidence: sections 1 (duration / target / result), 2 (trace), 7 (signature); 3–5 are "uygulanmaz (salt-okunur prova)".
Period evidence: list of drills in the period (number, date, lot, duration, result). Section 7 is read from MOD-0022
(`getSignature`) at generation time — 0177 stores only ids.
```json
{ "exportId": "…", "subject": { … }, "format": "Pdf | Csv", "fileName": "RC-2026-0004_kanit.pdf",
  "sha256": "hex of the produced file bytes", "sizeBytes": 48211, "exportedBy": UserAt, "dmsRef": null }
```

### 2.10 `RecallSettings`, `SettingsHistoryEntry`
```json
{ "legalEntityId": "…", "isDefault": true, "drillTargetHours": 4, "drillFrequencyWarningDays": 180,
  "effectivenessTargetPercent": { "1": "100", "2": "95", "3": "90" }, "traceMaxDepth": 20, "traceMaxNodes": 5000,
  "updatedAtUtc": "…|null", "updatedBy": UserRef|null, "version": 0 }
{ "field": "drillFrequencyWarningDays", "oldValue": "180", "newValue": "120", "reason": "…(10–1000)",
  "changedBy": UserAt, "settingsVersion": 2, "signatureId": "…|null" }
```

### 2.11 `LotRecallStatus`
```json
{ "lotId": "…", "underRecall": true,
  "cases": [ { "caseId": "…|null (other company → null)", "number": "RC-2026-0004", "status": "Active", "sameCompany": true } ] }
```

### 2.12 `PendingActionResponse`
```json
{ "signatureRequired": true, "actionCode": "recall.scope.approve", "meaningCode": "APPROVE", "visualMode": "savedAccepted",
  "subject": { "module": "recall", "subjectType": "recall.case", "subjectId": "…" }, "expiresAtUtc": "…" }
```

### 2.13 `SignableSubject` (provider, ESIGN-BUNDLE §6 shape)
`{ display (≤ 300), summary[] (≤ 20), legalEntityId, fields{} }` — field lists `rc-v1`, `dr-v1`, `rs-v1` (pack §8.8).

## 3. Permissions (namespace `recall.*`, owner MOD-0177)

`recall.dashboard.read`, `recall.trace.run`, `recall.trace.export`, `recall.cases.read`, `recall.cases.create`,
`recall.cases.update`, `recall.cases.approve`, `recall.cases.cancel`, `recall.cases.close`, `recall.effectiveness.complete`,
`recall.quarantine-requests.create`, `recall.segregations.confirm`, `recall.notifications.create`,
`recall.consumptions.create`, `recall.returns.link`, `recall.drills.read`, `recall.drills.manage`, `recall.drills.run`,
`recall.evidence.export`, `recall.settings.read`, `recall.settings.manage`, `recall.lots.status.read` (API-only).
External, used not declared: `esign.signatures.sign` (MOD-0022), `trace.lots.read` (MOD-0174, link), `quarantine.holds.read`
(MOD-0175, link), inventory posting keys (MOD-0173).

## 4. Operations

Common errors on every operation: 401, 403 `FORBIDDEN` (missing key), 403 `CROSS_LE_FORBIDDEN`, 404 (other tenant / unknown
id), 400 `INVALID_REQUEST`, 400 `IDEMPOTENCY_KEY_REUSED` (writes), 409 `RECALL_CONFLICT` (If-Match writes).

### Reads

| # | operationId | Method + path | Permission | Query / notes |
|---|---|---|---|---|
| 4.1 | `getDashboard` | `GET /api/recall/dashboard` | `recall.dashboard.read` | → `DashboardView` |
| 4.2 | `runTrace` | `GET /api/recall/trace` | `recall.trace.run` | `rootLotId` (required), `direction` (default Both), `asOfUtc?` → `TraceResult`. 404 `UNKNOWN_LOT`. Writes no record except the audit row `recall.trace.run`. p95 ≤ 2 s (3 levels / 500 nodes), ≤ 8 s at the node limit. |
| 4.3 | `exportTrace` | `GET /api/recall/trace/export` | `recall.trace.export` | same query + `format=csv` → CSV (columns: Düğüm türü; Ürün; Lot; Depo / Müşteri; Miktar; Birim; Hareket / Sevkiyat no; Tarih; Boşluk) UTF-8 BOM, `;`. Not logged as evidence. |
| 4.4 | `listCases` | `GET /api/recall/cases` | `recall.cases.read` | `type? (Real|Drill)`, `status?` (multi), `class?`, `skuId?`, `openedFrom?`, `q`, `page`, `pageSize ≤ 200`, `sort (-openedOn|number)` → `Page<CaseRef>` + `statusCounts` |
| 4.5 | `getCase` | `GET /api/recall/cases/{caseId}` | `recall.cases.read` | → `CaseDetail` + `ETag` |
| 4.6 | `getCaseScope` | `GET /api/recall/cases/{caseId}/scope` | `recall.cases.read` | `revision?` → `ScopeView` |
| 4.7 | `getCaseScopeDiff` | `GET /api/recall/cases/{caseId}/scope/diff` | `recall.cases.read` | live re-trace vs approved snapshot → `ScopeDiff` (writes `OutOfScopeFinding` events once per new node) |
| 4.8 | `getCaseStock` | `GET /api/recall/cases/{caseId}/stock` | `recall.cases.read` | → `[StockLine]` (+ in-scope AVAILABLE stock without a request, `request: null`) |
| 4.9 | `getCaseCustomers` | `GET /api/recall/cases/{caseId}/customers` | `recall.cases.read` | → `[CustomerRow]` (shipments re-read live, KR-E degrade) |
| 4.10 | `getCaseReturns` | `GET /api/recall/cases/{caseId}/returns` | `recall.cases.read` | → `{ links: [ReturnLink], candidates: [ReturnCandidate] }` |
| 4.11 | `getCaseEffectiveness` | `GET /api/recall/cases/{caseId}/effectiveness` | `recall.cases.read` | → `EffectivenessView` (live until completed; then the stored result) |
| 4.12 | `getCaseTimeline` | `GET /api/recall/cases/{caseId}/timeline` | `recall.cases.read` | paging → `Page<TimelineEntry>` |
| 4.13 | `getLotRecallStatus` | `GET /api/recall/lots/{lotId}/status` | `recall.lots.status.read` | → `LotRecallStatus` (consumers: 0174 lot badge, 0173 movement band, 0175 decision warning CT-R9, 0176 / 0178) |
| 4.14 | `listDrillPlans` | `GET /api/recall/drill-plans` | `recall.drills.read` | → `[DrillPlan]` |
| 4.15 | `listDrillRuns` | `GET /api/recall/drill-runs` | `recall.drills.read` | `status?`, `from?`, `to?`, paging → `Page<DrillRun>` |
| 4.16 | `getDrillRun` | `GET /api/recall/drill-runs/{runId}` | `recall.drills.read` | → `DrillRun` + `ETag` |
| 4.17 | `previewEvidence` | `GET /api/recall/evidence/preview` | `recall.evidence.export` | `caseId` \| `runId` \| `from`+`to` → `EvidencePreview` |
| 4.18 | `listEvidenceExports` | `GET /api/recall/evidence/exports` | `recall.evidence.export` | paging → `Page<EvidenceExport>` |
| 4.19 | `getEvidenceFile` | `GET /api/recall/evidence/exports/{exportId}/file` | `recall.evidence.export` | the stored bytes (same SHA-256) |
| 4.20 | `getSettings` | `GET /api/recall/settings` | `recall.settings.read` | → `RecallSettings` + `ETag` (defaults when none stored) |
| 4.21 | `getSettingsHistory` | `GET /api/recall/settings/history` | `recall.settings.read` | paging → `Page<SettingsHistoryEntry>` |
| 4.22 | `getSignableSubject` | `GET /api/recall/esign-subjects/{subjectType}/{subjectId}` | `recall.cases.read` (case) / `recall.drills.read` (run) / `recall.settings.read` (settings change), signer's JWT, company must match | `subjectType ∈ {recall.case, recall.drill-run, recall.settings-change}` → `SignableSubject`; 404 when not visible |

### Writes — cases

| # | operationId | Method + path | Permission | Body | Success | Errors | Idempotency / concurrency | Audit |
|---|---|---|---|---|---|---|---|---|
| 4.30 | `openCase` | `POST /api/recall/cases` | `recall.cases.create` | `{ idempotencyKey, rootLotId, direction, class (1–3), level, reason (10–1000), reasonReference? }` | 201 `CaseDetail` (Draft; scope items from the live trace) | 400 `REASON_REQUIRED`; 404 `UNKNOWN_LOT`; 409 `RECALL_CASE_ALREADY_OPEN` (details.caseId) | key | `recall.case.opened` |
| 4.31 | `markScopeItems` | `POST /api/recall/cases/{id}/scope/marks` | `recall.cases.update` | `{ idempotencyKey, marks: [ { nodeKey, included, exclusionReason? } ] (1–500) }` | 200 `ScopeView` | 400 `RECALL_SCOPE_EXCLUSION_REASON_REQUIRED` (< 10), `RECALL_SCOPE_ITEM_NOT_MARKABLE` (root / customer / input node); 409 `RECALL_INVALID_TRANSITION` (not Draft) | key + If-Match | `recall.scope.marked` |
| 4.32 | `submitScope` | `POST /api/recall/cases/{id}/scope/submit` | `recall.cases.update` | `{ idempotencyKey, note? }` | 200 `CaseDetail` (PendingScopeApproval, `submittedSnapshot`) | 409 `RECALL_INVALID_TRANSITION` | key + If-Match | `recall.scope.submitted` |
| 4.33 | `returnScope` | `POST /api/recall/cases/{id}/scope/return` | `recall.cases.approve` | `{ idempotencyKey, reason (10–1000) }` | 200 (Draft) | 409 `RECALL_INVALID_TRANSITION` | key + If-Match | `recall.scope.returned` |
| 4.34 | `prepareCaseAction` | `POST /api/recall/cases/{id}/pending-action` | per action: `recall.cases.approve` / `recall.effectiveness.complete` / `recall.cases.close` | `{ idempotencyKey, action: ApproveScope|CompleteEffectiveness|Close, payload: { note? | summary, openQuantityAcceptance? } }` | 200 `PendingActionResponse` (from MOD-0022 `getEffectivePolicy`) | 409 `RECALL_INVALID_TRANSITION`, `RECALL_SOD_VIOLATION` (CT-R1), `RECALL_SCOPE_CHANGED` (ApproveScope: live trace hash ≠ submitted; details = diff) | key + If-Match; replaces the caller's previous pending action; expires 15 min | — |
| 4.35 | `approveScope` | `POST /api/recall/cases/{id}/scope/approve` | `recall.cases.approve` | `{ idempotencyKey, note?, signatureId? }` (must equal the pending action) | 200 `CaseDetail` (Active; `approvedSnapshot`; lot blocks; quarantine requests `Pending` → dispatched after commit) | 400 `RECALL_SIGNATURE_REQUIRED`; 409 `RECALL_SIGNATURE_MISMATCH`, `RECALL_PENDING_ACTION_MISMATCH`, `RECALL_SCOPE_CHANGED`, `RECALL_SOD_VIOLATION`, `RECALL_INVALID_TRANSITION` | key + If-Match | `recall.scope.approved` (+ signatureId, snapshot SHA-256) |
| 4.36 | `reviseScope` | `POST /api/recall/cases/{id}/scope/revisions` | `recall.cases.update` | `{ idempotencyKey, addNodeKeys: [ … ] (from `getCaseScopeDiff`), marks?, reason (10–1000) }` | 201 revision r+1 pending approval (case stays Active) — CT-R5 | 409 `RECALL_INVALID_TRANSITION` (not Active / revision already pending) | key + If-Match | `recall.scope.revised` |
| 4.37 | `cancelCase` | `POST /api/recall/cases/{id}/cancel` | `recall.cases.cancel` | `{ idempotencyKey, reason (10–1000) }` | 200 (Cancelled) | 409 `RECALL_INVALID_TRANSITION` (only Draft / PendingScopeApproval) | key + If-Match | `recall.case.cancelled` |
| 4.38 | `requestQuarantine` | `POST /api/recall/cases/{id}/quarantine-requests` | `recall.quarantine-requests.create` | `{ idempotencyKey, nodeKeys: [ … ] (stock lines without a request, 1–50) }` | 202 `[QuarantineRequest]` (Pending → dispatched) | 409 `RECALL_INVALID_TRANSITION` (not Active / EffectivenessCheck), `RECALL_QUARANTINE_REQUEST_EXISTS` | key | `recall.quarantine.requested` |
| 4.39 | `retryQuarantineRequest` | `POST /api/recall/cases/{id}/quarantine-requests/{requestId}/retry` | `recall.quarantine-requests.create` | `{ idempotencyKey }` | 202 `QuarantineRequest` | 409 `RECALL_QUARANTINE_REQUEST_NOT_RETRYABLE` (Held / NotNeeded / Pending) | key (0175 key `…:a{n}`, CT-R2) | `recall.quarantine.retried` |
| 4.40 | `confirmSegregation` | `POST /api/recall/cases/{id}/segregations` | `recall.segregations.confirm` | `{ idempotencyKey, requestId \| nodeKey, locationId, quantity, note? }` | 201 | 409 `RECALL_INVALID_TRANSITION` (Closed / Cancelled); 422 `RECALL_SEGREGATION_NOT_HELD` (request not Held / PartiallyHeld / NotNeeded) | key | `recall.segregation.confirmed` |
| 4.41 | `recordNotification` | `POST /api/recall/cases/{id}/notifications` | `recall.notifications.create` | `{ idempotencyKey, customerRef, notifiedOn (date ≤ today), channel, note? (≤ 1000), supersedesId? }` | 201 `Notification` | 404 `RECALL_UNKNOWN_CUSTOMER` (not in the case's customers); 409 `RECALL_INVALID_TRANSITION` (Draft / Pending / Closed / Cancelled) | key | `recall.customer.notified` |
| 4.42 | `declareConsumption` | `POST /api/recall/cases/{id}/consumption-declarations` | `recall.consumptions.create` | `{ idempotencyKey, customerRef, skuId, skuLevel, quantity (> 0), declaredOn, evidence (≤ 1000), supersedesId? }` | 201 | 404 `RECALL_UNKNOWN_CUSTOMER`; 422 `RECALL_QUANTITY_EXCEEDS_SHIPPED` (consumed + returned > shipped for that customer + SKU); 409 transition | key | `recall.consumption.declared` |
| 4.43 | `linkReturn` | `POST /api/recall/cases/{id}/return-links` | `recall.returns.link` | `{ idempotencyKey, transactionId, customerRef, returnReference? }` | 201 `ReturnLink` | 422 `RECALL_RETURN_NOT_ELIGIBLE` (not `CUSTOMER_RETURN` / lot not in scope / before approval), `RECALL_RETURN_NOT_QUARANTINED` (into AVAILABLE); 409 `RECALL_RETURN_ALREADY_LINKED`; 404 `RECALL_UNKNOWN_CUSTOMER` | key | `recall.return.linked` |
| 4.44 | `startEffectivenessCheck` | `POST /api/recall/cases/{id}/effectiveness/start` | `recall.effectiveness.complete` | `{ idempotencyKey }` | 200 (EffectivenessCheck) | 409 `RECALL_INVALID_TRANSITION` (not Active, or a scope revision pending) | key + If-Match | — (event) |
| 4.45 | `completeEffectivenessCheck` | `POST /api/recall/cases/{id}/effectiveness/complete` | `recall.effectiveness.complete` | `{ idempotencyKey, note (10–1000), signatureId? }` (= pending action) | 200 (`effectiveness` stored, flag set) | 400 `RECALL_SIGNATURE_REQUIRED`, `REASON_REQUIRED`; 409 `RECALL_SIGNATURE_MISMATCH`, `RECALL_PENDING_ACTION_MISMATCH`, `RECALL_INVALID_TRANSITION` | key + If-Match | `recall.effectiveness.completed` |
| 4.46 | `closeCase` | `POST /api/recall/cases/{id}/close` | `recall.cases.close` | `{ idempotencyKey, summary (10–2000), openQuantityAcceptance? (10–1000; required when any open > 0), signatureId? }` (= pending action) | 200 (Closed; lot blocks released) | 400 `RECALL_OPEN_QUANTITY_ACCEPTANCE_REQUIRED`, `RECALL_SIGNATURE_REQUIRED`; 409 `RECALL_SOD_VIOLATION` (closer = effectiveness completer, CT-R1), `RECALL_SIGNATURE_MISMATCH`, `RECALL_PENDING_ACTION_MISMATCH`, `RECALL_INVALID_TRANSITION` (effectiveness not completed) | key + If-Match | `recall.case.closed` |

### Writes — drills, evidence, settings

| # | operationId | Method + path | Permission | Body | Success | Errors | Idempotency / concurrency | Audit |
|---|---|---|---|---|---|---|---|---|
| 4.50 | `createDrillPlan` | `POST /api/recall/drill-plans` | `recall.drills.manage` | `{ idempotencyKey, name, frequency, customDays?, scopeKind, nextDueOn, ownerUserId }` | 201 `DrillPlan` (`DP-NN`) | 400 `INVALID_REQUEST` | key | `recall.drill-plan.saved` |
| 4.51 | `updateDrillPlan` | `PUT /api/recall/drill-plans/{planId}` | `recall.drills.manage` | same + `active` | 200 | 409 `RECALL_CONFLICT` | key + If-Match | `recall.drill-plan.saved` |
| 4.52 | `startDrill` | `POST /api/recall/drill-runs` | `recall.drills.run` | `{ idempotencyKey, planId, selectionMode: Auto|Manual, lotId? (Manual) }` | 201 `DrillRun` (Running, server `startedAtUtc`, `targetHours` from settings) | 409 `RECALL_DRILL_ALREADY_RUNNING`; 422 `RECALL_DRILL_NO_CANDIDATE` (Auto, empty set); 404 `UNKNOWN_LOT` | key | `recall.drill.started` |
| 4.53 | `completeDrillStep` | `POST /api/recall/drill-runs/{runId}/steps/{step}` | `recall.drills.run` | `{ idempotencyKey }` | 200 `DrillRun` (step Trace stores the snapshot; step CustomerList sets `endedAtUtc`) | 409 `RECALL_DRILL_STEP_ORDER`, `RECALL_INVALID_TRANSITION` | key + If-Match | `recall.drill.step` |
| 4.54 | `prepareDrillResult` | `POST /api/recall/drill-runs/{runId}/pending-result` | `recall.drills.run` | `{ idempotencyKey, resultNote (10–2000) }` | 200 `PendingActionResponse` | 409 `RECALL_INVALID_TRANSITION` (steps not complete) | key + If-Match | — |
| 4.55 | `signDrillResult` | `POST /api/recall/drill-runs/{runId}/result` | `recall.drills.run` | `{ idempotencyKey, resultNote, signatureId? }` | 200 (Completed) | 400 `RECALL_SIGNATURE_REQUIRED`; 409 `RECALL_SIGNATURE_MISMATCH`, `RECALL_PENDING_ACTION_MISMATCH` | key + If-Match | `recall.drill.completed` |
| 4.56 | `cancelDrill` | `POST /api/recall/drill-runs/{runId}/cancel` | `recall.drills.run` | `{ idempotencyKey, reason? }` | 200 (Cancelled, kept — CT-R17) | 409 `RECALL_INVALID_TRANSITION` | key + If-Match | `recall.drill.cancelled` |
| 4.57 | `createEvidenceExport` | `POST /api/recall/evidence/exports` | `recall.evidence.export` | `{ idempotencyKey, caseId \| runId \| from+to, format: Pdf|Csv }` | 201 `EvidenceExport` + `Location` of the file (4.19) | 400 `RECALL_EVIDENCE_RANGE_INVALID` (> 366 days) | key (same key → same file + hash) | `recall.evidence.exported` |
| 4.58 | `prepareSettingsChange` | `POST /api/recall/settings/pending-change` | `recall.settings.manage` | `{ idempotencyKey, settings (writable members), reason (10–1000) }` | 200 `PendingActionResponse` (`signatureRequired` per MOD-0022 row `recall.settings.update`, default false — CT-R16) | 400 `RECALL_SETTINGS_INVALID`; 409 `RECALL_NO_CHANGES`, `RECALL_CONFLICT` | key + If-Match | — |
| 4.59 | `updateSettings` | `PUT /api/recall/settings` | `recall.settings.manage` | `{ idempotencyKey, settings, reason (10–1000), signatureId? }` | 200 `RecallSettings` (version + 1) | 400 `RECALL_SETTINGS_INVALID`, `REASON_REQUIRED`, `RECALL_SIGNATURE_REQUIRED`; 409 `RECALL_CONFLICT`, `RECALL_NO_CHANGES`, `RECALL_SIGNATURE_MISMATCH` | key + If-Match (`"0"` creates) | `recall.settings.updated` + one history row per changed field |

Ranges: `drillTargetHours` 1–72; `drillFrequencyWarningDays` 30–730; `effectivenessTargetPercent[1..3]` 0–100;
`traceMaxDepth` 2–50; `traceMaxNodes` 100–20000.

Not offered (by decision): reason-based exit permission (KR-C), e-mail to customers (KR-F), Workflow routing (KR-A),
movement / return creation, deletion of any record, a drill that writes anything (pack §8.10).

### 4.60 Error codes (owned by RECALL-BUNDLE)

| Code | HTTP |
|---|---|
| `RECALL_SETTINGS_INVALID` | 400 |
| `RECALL_SCOPE_EXCLUSION_REASON_REQUIRED` | 400 |
| `RECALL_SCOPE_ITEM_NOT_MARKABLE` | 400 |
| `RECALL_SIGNATURE_REQUIRED` | 400 |
| `RECALL_OPEN_QUANTITY_ACCEPTANCE_REQUIRED` | 400 |
| `RECALL_EVIDENCE_RANGE_INVALID` | 400 |
| `RECALL_UNKNOWN_CUSTOMER` | 404 |
| `RECALL_CONFLICT` | 409 |
| `RECALL_INVALID_TRANSITION` | 409 |
| `RECALL_CASE_ALREADY_OPEN` | 409 |
| `RECALL_SCOPE_CHANGED` | 409 |
| `RECALL_SOD_VIOLATION` | 409 |
| `RECALL_SIGNATURE_MISMATCH` | 409 |
| `RECALL_PENDING_ACTION_MISMATCH` | 409 |
| `RECALL_NO_CHANGES` | 409 |
| `RECALL_QUARANTINE_REQUEST_EXISTS` | 409 |
| `RECALL_QUARANTINE_REQUEST_NOT_RETRYABLE` | 409 |
| `RECALL_RETURN_ALREADY_LINKED` | 409 |
| `RECALL_DRILL_ALREADY_RUNNING` | 409 |
| `RECALL_DRILL_STEP_ORDER` | 409 |
| `RECALL_RETURN_NOT_ELIGIBLE` | 422 |
| `RECALL_RETURN_NOT_QUARANTINED` | 422 |
| `RECALL_SEGREGATION_NOT_HELD` | 422 |
| `RECALL_QUANTITY_EXCEEDS_SHIPPED` | 422 |
| `RECALL_DRILL_NO_CANDIDATE` | 422 |
| shared: `INVALID_REQUEST`, `REASON_REQUIRED`, `IDEMPOTENCY_KEY_REUSED`, `UNKNOWN_LOT`, `CROSS_LE_FORBIDDEN`, `FORBIDDEN` | 400 / 404 / 403 |
| owned by INVENTORY 1.4.0 (proposal §9.1), returned by `postMovement`: `LOT_UNDER_RECALL` | 422 |

## 5. Case state machine

```
Draft ──submitScope──▶ PendingScopeApproval ──approveScope(+sig)──▶ Active ──startEffectivenessCheck──▶ EffectivenessCheck
  ▲                            │                                       │                                     │
  └─────────returnScope────────┘                                       └─reviseScope (r+1 pending, stays Active)
Draft | PendingScopeApproval ──cancelCase──▶ Cancelled          EffectivenessCheck ──completeEffectivenessCheck(+sig)──▶ (flag)
                                                                EffectivenessCheck + flag ──closeCase(+sig)──▶ Closed
```
Terminal: `Closed`, `Cancelled`. Lot blocks: active from `approveScope` to `closeCase`. Four-eyes (CT-R1): approver ≠
submitter; closer ≠ effectiveness completer.

## 6. Events

MVP-1 publishes no bus event (RabbitMQ is off on the MVP-1 machine; no consumer asks yet). Audit rows (pack §8.12) and case /
run events are the record. Future integration events `recall.case.activated` / `recall.case.closed` (for MVP-6 shipment
holds) are additive.

## 7. Signable actions (registered, ESIGN-BUNDLE §4.23)

Module `recall`: `recall.scope.approve`, `recall.effectiveness.complete`, `recall.case.close`, `recall.drill.result`
(`defaultRequired: true`), `recall.settings.update` (`defaultRequired: false`, CT-R16) — default meaning `APPROVE`, visual
mode `savedAccepted`, names in 7 languages. Provider base URL from operator config `ESign:SubjectProviders:recall:BaseUrl`.
Subject types `recall.case` (`rc-v1`), `recall.drill-run` (`dr-v1`), `recall.settings-change` (`rs-v1`) — pack §8.8.

## 8. Non-functional

- `runTrace` p95 ≤ 2 s at 3 levels / 500 nodes, ≤ 8 s at the node limit; shipment reads 2 s timeout, ≤ 8 parallel, no cache.
- Exit block adds ≤ 10 ms p95 to a posting (one indexed read of `recall_lot_blocks` per lot, in the transaction).
- Lists server-paged, `pageSize ≤ 200`.
- Quarantine dispatcher: after commit + sweeper every 60 s for `Pending` older than 2 min.
- 7 languages for every user-visible name (statuses, classes, levels, node kinds, gap kinds, channels, steps, actions).

## 9. Additive notes for other owners (NOT part of this contract)

### 9.1 INVENTORY-BUNDLE 1.4.0 (owner MOD-0173; numbered after 0176's 1.3.0)
- New 422 code on `postMovement`: `LOT_UNDER_RECALL` — details `{ lotId, lotNumber, recallCaseId, recallCaseNumber,
  movementType }`; joins the closed `InventoryErrorCodes.All` set with the version.
- `postMovement` description: a lot under an active recall refuses `GOODS_ISSUE_SALES`, `GOODS_ISSUE_PROD`,
  `WAREHOUSE_TRANSFER` (CT-R4); every other type passes the recall rule; the exemption is decided by movement type only,
  never by `sourceModule`; the recall rule runs before the FEFO rule.
- Optional (CT-R13; not needed by 0177): `getTransactions` filters `movementType` (multi) and `sourceDocumentId`.

### 9.2 QUARANTINE 1.3.0 (owner MOD-0175)
- `HoldSource` += `Recall` (`quarantine.openapi.yaml:397`).
- Recall holds: `sourceDocument = { module: "MOD-0177", type: "RECALL_CASE", id: <caseId>, number: "RC-YYYY-NNNN" }`
  (existing shape `:438-440`) → readable through `listHoldsBySource` (`:67-69`).
- Description note on `quarantineStock`: MOD-0177 calls the command in-process on scope approval, one bucket per call, with
  the approver as actor (CT-R2).
- Fallback (CT-R3): no enum value; `Source = Manual` + the `sourceDocument` above (no wire change).
- Optional (CT-R9): 0175 decision screen reads `getLotRecallStatus` and warns before releasing recalled stock (UI only).

### 9.3 TRACE — none. Optional description note on `getGenealogy`: the full recall trace is RECALL-BUNDLE `runTrace` (depth cap 2 unchanged). Lot-rule request CT-R10 (`CUSTOMER_RETURN` into QUARANTINE / QI for held lots) is a behaviour note on INVENTORY `postMovement` owned by MOD-0174's lot rules — owner step if accepted.

### 9.4 SHIPMENT-BUNDLE — requests to the MVP-6 team (owner decision KR-E: requests, not needs)
1. `ShipmentLine` optional `lotId`, `serialIds[]`, `skuLevel` (lines carry none today, `shipment-bundle.openapi.yaml:3042-3052`).
2. `GET /shipments?lotId=` filter.
3. Structured customer `partyId` (+ display name) next to `shipToReference` (`:141`).
4. Return `reasonCode` value `RECALL` defined (free text today, `:3246-3254`) + optional `recallCaseReference` on
   `CreateReturnCommand`, echoed on the posted `CUSTOMER_RETURN` (`sourceDocumentId` / reference) so 0177 links automatically.
5. Accept InventoryService tenant tokens (no `legal_entity_id` claim today) — or define the claim mapping — so
   `GET /shipments/{id}` can be read by 0177 on the user's behalf.
6. Optional: `ShipmentDelivered` / `PodCaptured` carry lot ids when lines do.
0177 works without any of them (pack §8.6: ledger + read-time shipment, missing fields marked).

### 9.5 FEFO-BUNDLE 1.1.0 (owner MOD-0176) — optional (CT-R12)
`getFefoOrder` exclusion reason `RECALLED` for lots with an active recall block.

### 9.6 ESIGN — none (consumer only). PRODUCT-MASTER — none (`getSku.LotControlled` / `SerialControlled` read as today).
