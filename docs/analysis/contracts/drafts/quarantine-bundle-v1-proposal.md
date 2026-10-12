# QUARANTINE-BUNDLE v1 — proposal (MOD-0175 Quarantine / Blocked Stock)

> **Status: PROPOSAL (draft) — not a contract.** No quarantine contract exists in `docs/analysis/contracts/` (README
> `:7-42`), although the inventory report marks QUARANTINE-BUNDLE "✓" (`inventory-capability-scope-and-dependency-report.md:229`).
> After CT acceptance, Q1 writes this as `docs/analysis/contracts/quarantine.openapi.yaml`
> (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0175`, `x-owner-service: Diten.InventoryService`) in ONE
> single-writer step and adds the README row. After the freeze only additive changes are allowed (README "Freeze süreci"
> `:60-65`, K16).
>
> **Source:** Blueprint 8.1 `Blueprint_Data` row 176 col L "QUARANTINE-BUNDLE (quarantine states, release approvals, audit
> hooks, evidence export)". Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0175-quarantine-blocked-stock.md`
> (§8 is the behaviour authority; this file is the wire shape).
>
> **Frozen contracts consumed and not changed:** INVENTORY-BUNDLE 1.0.1, TRACE 1.2.0, LOCATION 1.1.0, ESIGN-BUNDLE v1
> (accepted proposal). The only additive notes 0175 needs from other owners are in §9 and are **not** part of this
> contract.

## 1. Rules

1. Server `/api/quarantine` (Gateway → Diten.InventoryService 5066, new route INT-QUARANTINE). Tenant shell callers only
   (`actor_type = tenant_user`).
2. `TenantId` from the token. Company from the W-0173-02 scope: header `X-Legal-Entity-Id`, validated against MDM. Another
   company → 403 `CROSS_LE_FORBIDDEN`; another tenant → 404.
3. Envelope: the InventoryService `Response<T>` shape used by INVENTORY / TRACE (`{ data, error: { code, message, details } }`).
4. Every write takes the `Idempotency-Key` header, equal to body `idempotencyKey` (8–100). Same key + same body → the first
   result (200). Same key + different body → 400 `IDEMPOTENCY_KEY_REUSED`.
5. Mutable records (hold, blocked stock, settings) need `If-Match: "<version>"`. Stale → 409 `QUARANTINE_CONFLICT`.
6. **No call here moves stock directly except** the no-workflow safety actions (§4.4 quarantine, §4.15 manual block). Every
   release / reject / block decision, disposition, unblock and lot release moves stock **only after Workflow approval**
   (DEC-INV-08, KQ-F).
7. Every command writes an audit row and a history entry. Signatures are MOD-0022 references only (`signatureId`).
8. Stock statuses and movement types are INVENTORY-BUNDLE values (`inventory-bundle.openapi.yaml:252,255`). This contract
   defines **no** new value of either.

## 2. Schemas

### 2.1 `HoldRef`
```json
{ "holdId": "guid", "holdNumber": "KRN-2026-0412", "legalEntityId": "guid",
  "item": { "itemId": "guid", "skuId": "guid", "skuLevel": "string", "skuCode": "ILC-PRC500", "name": "Parasetamol 500 mg Tablet · 10'lu" },
  "lot": { "lotId": "string|null", "lotNumber": "PRS-26A014|null", "expiryDate": "date-time|null", "retestDate": "date-time|null", "coaReference": "string|null", "lotStatus": "Draft|Released|Blocked|Expired|null" },
  "warehouse": { "id": "guid", "code": "D01", "name": "Merkez Depo" }, "location": { "id": "guid", "code": "KRN-01" },
  "stockStatus": "QUARANTINE | QUALITY_INSPECTION", "quantity": 1200, "baseUom": { "id": "guid", "code": "KUTU" },
  "source": "GoodsReceipt | Production | CustomerReturn | Manual | Retest | Rework | OtherInbound",
  "sourceDocument": { "module": "MOD-0142|null", "type": "string|null", "id": "string|null", "number": "MK-2026-1184|null", "text": "string|null" },
  "enteredAtUtc": "…", "createdBy": { "userId": "…", "name": "…" },
  "reviewState": "Waiting | InReview", "status": "Open | DecisionPending | Decided",
  "waitDays": 15, "targetDays": 10, "late": true,
  "activeDecision": { "decisionId": "guid", "decisionNumber": "QKR-2026-0101", "state": "PendingApproval" } ,
  "version": 7 }
```

### 2.2 `HoldDetail` = `HoldRef` +
```json
{ "analysisNote": "string|null", "attachments": [ { "name": "Tedarikçi CoA (PDF)", "dmsRef": "DMS-2026-08812" } ],
  "reviewStartedAtUtc": "…|null", "reviewStartedBy": { }, "decidedAtUtc": "…|null",
  "history": [ HistoryEntry ], "movements": [ MovementLink ], "decisions": [ DecisionRef ] }
```

### 2.3 `HistoryEntry`
```json
{ "atUtc": "…", "by": { "userId": "…", "name": "Ayşe Demir" },
  "action": "Quarantined | ReviewStarted | DecisionSubmitted | Approved | Rejected | Cancelled | TimedOut | Withdrawn | Applied | ApplyFailed | FourEyesViolation | Blocked | ReviewCompleted | Disposed | Unblocked | SampleRetained",
  "reason": "string|null", "decisionId": "guid|null", "signatureId": "string|null", "approverSignatureId": "string|null" }
```

### 2.4 `MovementLink`
```json
{ "transactionId": "…", "transactionNumber": "…", "movementType": "STATUS_TRANSFER", "atUtc": "…",
  "fromStockStatus": "QUARANTINE", "toStockStatus": "AVAILABLE", "fromLocationId": "…", "toLocationId": "…",
  "quantity": 1200, "byUserName": "…" }
```
The values are those of the INVENTORY transaction (sourceModule `MOD-0175`, sourceDocumentId = decision/hold number).

### 2.5 `DecisionRequest` (draft)
```json
{ "kind": "HoldDecision | Disposition | Unblock",
  "targetId": "holdId | blockedStockId",
  "lines": [ { "lineNo": 1, "decision": "Release | Reject | Block", "quantity": 700 },
             { "lineNo": 2, "decision": "Reject", "quantity": 500 } ],
  "blockReasonCode": "QUALITY_REJECT|null", "reason": "≥10 chars", "coaChecked": true,
  "newRetestDate": "date|null",
  "disposition": { "kind": "Destroy | ReturnToSupplier | Rework | Sample", "quantity": 150,
                   "supplierId": "guid|null", "returnDocumentNo": "string|null",
                   "destructionMethod": "string|null", "destructionDate": "date|null" },
  "idempotencyKey": "…" }
```
- `lines` is used only for `HoldDecision`: 1 line (quantity = hold quantity) or 2 lines (partial, KQ-A).
- `disposition` is used only for `Disposition`.
- `Unblock` uses `reason` only.
- `LotRelease` decisions are created only by the lot gate (§4.19), never by this request.

### 2.6 `Decision`
```json
{ "decisionId": "guid", "decisionNumber": "QKR-2026-0101", "kind": "HoldDecision | Disposition | Unblock | LotRelease",
  "holdId": "guid|null", "blockedStockId": "guid|null", "lotId": "string|null", "legalEntityId": "guid",
  "fromStockStatus": "QUARANTINE", "lines": [ { "lineNo": 1, "decision": "Release", "quantity": 700, "toStockStatus": "AVAILABLE" } ],
  "blockReasonCode": "…|null", "reason": "…", "coaChecked": true, "coaReference": "…|null", "newRetestDate": "…|null",
  "disposition": { }, "lotStatusTarget": "Released|null",
  "plannedMovements": [ { "lineNo": 1, "movementType": "STATUS_TRANSFER", "fromStockStatus": "QUARANTINE", "toStockStatus": "AVAILABLE", "fromLocationId": "…", "toLocationId": "…", "quantity": 700 } ],
  "decider": { "userId": "…", "name": "…" }, "decidedAtUtc": "…", "deciderSignatureId": "string|null",
  "esignActionCode": "quarantine.hold.release", "signatureRequired": true,
  "state": "Draft | Submitted | PendingApproval | Approved | Applied | Rejected | Cancelled | TimedOut | Withdrawn | StartFailed | ApplyFailed | FourEyesViolation",
  "workflow": { "instanceId": "guid|null", "templateCode": "INV-QUARANTINE-RELEASE", "outcome": "approved|rejected|cancelled|timed-out|null", "approver": { "userId": "…", "name": "…" }, "approverSignatureId": "…|null", "completedAtUtc": "…|null" },
  "appliedMovementIds": [ "…" ], "error": { "code": "LOT_BLOCKED", "message": "…" } }
```
- `plannedMovements` is computed at draft time from pack §8 (stock-effect table) and shown in the UI ("Onaylanınca
  oluşacak hareket").
- `esignActionCode` / `signatureRequired` come from the effective MOD-0022 policy.

### 2.7 `BlockedStockRef`
```json
{ "blockedId": "guid|null", "blockedNumber": "BLK-2026-0071|null", "legalEntityId": "guid", "item": { }, "lot": { },
  "warehouse": { }, "location": { }, "quantity": 150, "baseUom": { },
  "blockReason": { "code": "QUALITY_REJECT", "name": "Kalite ret" }, "note": "…|null", "blockedAtUtc": "…",
  "origin": "QuarantineDecision | ManualBlock | Rework | OtherInbound | LotBlocked",
  "originRef": { "holdNumber": "KRN-2026-0377|null", "decisionNumber": "…|null" },
  "pendingDisposition": true, "reviewed": true, "status": "Open | Closed | SampleRetained",
  "allowedActions": [ "Dispose:Destroy", "Dispose:ReturnToSupplier", "Dispose:Rework", "Dispose:Sample", "Unblock", "CompleteReview" ],
  "activeDecision": { }, "version": 3 }
```
`origin = LotBlocked` rows are virtual (read from the lot): `blockedId` is null and `allowedActions = ["Dispose:Destroy"]`
(CT-D4).

### 2.8 `RetestItem`
```json
{ "lotId": "…", "lotNumber": "LK-0611-02", "item": { }, "retestDate": "…", "daysRelative": -7,
  "state": "Upcoming | DueClosed | Quarantined", "availableQuantity": 210, "baseUom": { },
  "buckets": [ { "warehouseId": "…", "locationId": "…", "quantity": 210 } ], "holdIds": [ "…" ] }
```

### 2.9 `Settings`
```json
{ "legalEntityId": "guid|null", "waitTargetDays": 10, "fourEyes": true,
  "approverCandidates": [ "position:6f1c…" ], "sampleLocations": { "<warehouseId>": "<locationId>" },
  "lotReleaseRequiresApproval": true, "reconcileAfterSeconds": 30,
  "workflowTemplate": { "code": "INV-QUARANTINE-RELEASE", "publishedVersion": 1, "status": "Published | Missing" },
  "version": 2 }
```

### 2.10 `BlockReason`
```json
{ "code": "QUALITY_REJECT", "names": { "en": "Quality rejection", "tr": "Kalite ret", "fr": "…", "es": "…", "zh": "…", "ar": "…", "ru": "…" }, "isActive": true, "isLocked": true }
```

### 2.11 `LotQualityStatus` (consumer read)
```json
{ "lotId": "…", "retestDue": false, "quarantinedQuantity": 0, "qualityInspectionQuantity": 0, "blockedQuantity": 150,
  "openHolds": [ HoldRef ], "openBlocked": [ BlockedStockRef ], "pendingLotRelease": { "decisionId": "…", "decisionNumber": "…" } }
```

### 2.12 `SignableSubject` for e-signature (ESIGN-BUNDLE §6; subjectType `quarantine.decision`, field list `qd-v1`)
```json
{ "display": "Karantina kararı KRN-2026-0412 · Lot PRS-26A014 · 1.200 Kutu Serbest Bırak",
  "summary": [ { "label": "Ürün", "value": "…" }, { "label": "Karar", "value": "Serbest Bırak · 1.200 Kutu → Kullanılabilir" }, { "label": "Gerekçe", "value": "…" } ],
  "legalEntityId": "guid",
  "fields": { "subjectVersion": "qd-v1", "decisionNumber": "…", "decisionKind": "HoldDecision", "holdNumber": "…", "blockedNumber": null,
              "legalEntityId": "…", "itemId": "…", "skuId": "…", "skuCode": "…", "lotId": "…", "lotNumber": "…",
              "warehouseCode": "…", "locationCode": "…", "fromStockStatus": "QUARANTINE",
              "line1.decision": "Release", "line1.quantity": "700", "line1.toStockStatus": "AVAILABLE",
              "line2.decision": "Reject", "line2.quantity": "500", "line2.toStockStatus": "BLOCKED",
              "baseUom": "KUTU", "blockReasonCode": "QUALITY_REJECT", "reason": "…", "coaChecked": "true", "coaReference": "…",
              "dispositionKind": null, "dispositionQuantity": null, "supplierId": null, "returnDocumentNo": null,
              "destructionMethod": null, "destructionDate": null, "sampleLocationCode": null,
              "newRetestDate": null, "lotStatusTarget": null, "deciderUserId": "…" } }
```
- All `fields` keys are always present (null when not applicable), so the canonical form is stable.
- Values are strings (decimals in invariant culture, dates UTC "O", bools `true`/`false`), following ESIGN §7.
- Field names are frozen with this contract. A change is a new `subjectVersion`.

## 3. Permissions (namespace `quarantine.*`, owner MOD-0175; all Scope = Tenant)

`quarantine.holds.read` · `quarantine.holds.create` · `quarantine.holds.review` · `quarantine.decisions.create` ·
`quarantine.decisions.withdraw` · `quarantine.decisions.apply` · `quarantine.blocked.dispose` · `quarantine.blocked.unblock` ·
`quarantine.blocked.block` · `quarantine.settings.manage`.

Outside keys a caller may additionally need (not owned here):
- `platform.workflow.instances.start` (submit);
- `platform.workflow.instances.view` (reconcile, best effort);
- `platform.workflow.tasks.cancel` (withdraw);
- `platform.workflow.definitions.manage/publish` (template preparation);
- `esign.signatures.sign`;
- `trace.lots.change-status` (KQ-I).

## 4. Operations

### 4.1 `GET /holds` — `listHolds`
- Key `quarantine.holds.read`.
- Query: `q`, `warehouseId`, `stockStatus` (QUARANTINE|QUALITY_INSPECTION), `source`, `status` (Open|DecisionPending|Decided),
  `reviewState`, `lateOnly`, `lotId`, `sourceDocumentId`, `page`, `pageSize` (≤ 100), `sort`.
- 200 `{ items: HoldRef[], total, kpis: { openCount, inReviewCount, quantityByUom: [{uomCode, quantity}], lateCount, expirySoonCount } }`.
- Side effect: read-time reconciliation of pending decisions (pack §8), best effort and never failing the read.

### 4.2 `GET /holds/{holdId}` — `getHold`
- Key `quarantine.holds.read`. 200 `HoldDetail` + ETag. 404 `QUARANTINE_HOLD_NOT_FOUND`.
- Reconciles the active decision (best effort).

### 4.3 `PATCH /holds/{holdId}` — `updateHold`
- Key `quarantine.holds.review`. If-Match.
- Body `{ analysisNote?, attachments?, idempotencyKey }`.
- 200 `HoldDetail`. Errors: 409 `QUARANTINE_HOLD_CLOSED`, 409 `QUARANTINE_CONFLICT`.

### 4.4 `POST /holds` — `quarantineStock` (no workflow — safety direction)
- Key `quarantine.holds.create`.
- Body `{ "mode": "Manual | Retest", "lotId": "…|null", "buckets": [ { "itemId","skuId","skuLevel","warehouseId","locationId","quantity" } ] | null, "reason": "≥10", "signatureId": "…|null", "idempotencyKey": "…" }`.
  - `mode = Retest` takes `lotId` and quarantines every AVAILABLE bucket of the lot.
  - `mode = Manual` takes explicit buckets.
- Posts `STATUS_TRANSFER AVAILABLE→QUARANTINE` per bucket (sourceType `QUARANTINE_HOLD`). 201 `HoldRef[]`.
- Errors:
  - 400 `REASON_REQUIRED`;
  - 409 `QUARANTINE_STOCK_RESERVED` (`details.reservations[]`);
  - 409 `QUARANTINE_NOTHING_AVAILABLE`;
  - 422 ledger codes passed through (`INSUFFICIENT_STOCK`, `LOT_BLOCKED`, `POSTING_PERIOD_CLOSED`, …);
  - 400 `QUARANTINE_SIGNATURE_REQUIRED` when the policy row `quarantine.hold.create` requires it.

### 4.5 `POST /holds/{holdId}/review` — `startReview`
- Key `quarantine.holds.review`. If-Match. Body `{ note?, idempotencyKey }`. 200 `HoldRef` (reviewState InReview).
- Error: 409 `QUARANTINE_HOLD_CLOSED`.

### 4.6 `POST /decisions` — `createDecision` (draft; nothing moves)
- Key `quarantine.decisions.create` (`quarantine.blocked.dispose` for Disposition, `quarantine.blocked.unblock` for Unblock).
- Body `DecisionRequest`. 201 `Decision` (state Draft).
- Errors:
  - 400: `QUARANTINE_DECISION_INVALID`, `QUARANTINE_PARTIAL_SUM_MISMATCH`, `REASON_REQUIRED`, `QUARANTINE_COA_NOT_CONFIRMED`,
    `QUARANTINE_BLOCK_REASON_REQUIRED`, `QUARANTINE_RETEST_DATE_REQUIRED`, `LOT_DATES_INVALID`, `QUARANTINE_QUANTITY_INVALID`,
    `QUARANTINE_SUPPLIER_REQUIRED`, `QUARANTINE_DESTRUCTION_METHOD_REQUIRED`;
  - 404 `QUARANTINE_HOLD_NOT_FOUND` / `QUARANTINE_BLOCKED_NOT_FOUND`;
  - 409: `QUARANTINE_DECISION_ALREADY_PENDING`, `QUARANTINE_HOLD_CLOSED`, `QUARANTINE_LOT_HELD`,
    `QUARANTINE_SAMPLE_LOCATION_NOT_SET`, `QUARANTINE_REVIEW_NOT_COMPLETED`, `QUARANTINE_STOCK_CHANGED`.
- A Draft that is never submitted expires after 24 h (event `Withdrawn`, reason "draft expired").

### 4.7 `POST /decisions/{decisionId}/submit` — `submitDecision` (starts the Workflow)
- Key `quarantine.decisions.create` (or the kind's key, as 4.6). Caller = the decision's decider.
- Body `{ "signatureId": "…|null", "idempotencyKey": "…" }`.
- 202 `Decision` (state PendingApproval, `workflow.instanceId`). Replay → same result.
- Errors:
  - 400 `QUARANTINE_SIGNATURE_REQUIRED`;
  - 409 `QUARANTINE_SIGNATURE_MISMATCH`, `QUARANTINE_APPROVERS_NOT_CONFIGURED`, `QUARANTINE_WORKFLOW_TEMPLATE_MISSING`,
    `QUARANTINE_DECISION_NOT_DRAFT`, `QUARANTINE_STOCK_CHANGED`;
  - 403 `QUARANTINE_WORKFLOW_FORBIDDEN` (the caller lacks `platform.workflow.instances.start`);
  - 400 `QUARANTINE_WORKFLOW_REJECTED`;
  - 503 `QUARANTINE_WORKFLOW_UNAVAILABLE` (state `StartFailed`; resubmit with the same key retries the start).
- Workflow call (user token): `POST /api/v1/workflow/instances`:
  - `templateCode = INV-QUARANTINE-RELEASE`, `objectType = quarantine.decision`, `objectId = decisionId`,
    `objectRef = decisionNumber`;
  - `candidatePrincipalIds = settings.approverCandidates`, `reasonCode = QUARANTINE_{KIND}`;
  - `idempotencyKey = quarantine:quarantine.decision:{decisionId:D}:r1`;
  - `dueAt = hold.enteredAt + waitTargetDays`;
  - `displayContext = { title, subtitle, sourceModule: "MOD-0175", deepLinkUrl: "/Quarantine/Details/{holdId}", chips }`.

### 4.8 `GET /decisions/{decisionId}` — `getDecision`
- Key `quarantine.holds.read`. 200 `Decision` (reconciles first if PendingApproval). 404.

### 4.9 `POST /decisions/{decisionId}/refresh` — `refreshDecision` ("Durumu yenile")
- Key `quarantine.holds.read`. 200 `Decision`.
- Calls `GET /api/v1/workflow/instances/by-objects` (and history for the approver). A terminal outcome is applied
  (approved → apply engine; others → reopen). Idempotent.
- 503 `QUARANTINE_WORKFLOW_UNAVAILABLE` when Platform is unreachable (state unchanged).

### 4.10 `POST /decisions/{decisionId}/withdraw` — `withdrawDecision`
- Key `quarantine.decisions.withdraw`; decider only. Body `{ reason ≥10, idempotencyKey }`.
- Cancels the open workflow task with the caller's token. 200 `Decision` (Withdrawn).
- Errors: 403 `QUARANTINE_NOT_DECIDER`, 409 `QUARANTINE_DECISION_NOT_PENDING`.

### 4.11 `POST /decisions/{decisionId}/apply` — `reapplyDecision` ("Yeniden uygula")
- Key `quarantine.decisions.apply`. Only for state `ApplyFailed`. Re-runs the apply engine with the same ledger idempotency
  keys. 200 `Decision`.
- Error: 409 `QUARANTINE_DECISION_NOT_APPLICABLE`; 422 ledger codes.

### 4.12 `GET /blocked` — `listBlocked`
- Key `quarantine.holds.read`. Query `q`, `blockReasonCode`, `pendingOnly`, `origin`, `status`, `warehouseId`, `page`,
  `pageSize`.
- 200 `{ items: BlockedStockRef[], total }`. Includes virtual `LotBlocked` rows.

### 4.13 `GET /blocked/{blockedId}` — `getBlocked`
- Key `quarantine.holds.read`. 200 `BlockedStockRef` + history + movements + decisions.

### 4.14 `POST /blocked/{blockedId}/review-complete` — `completeBlockedReview`
- Key `quarantine.holds.review`. If-Match. Body `{ reason ≥10, idempotencyKey }`. 200 (`reviewed = true`).
- Error: 409 `QUARANTINE_BLOCKED_CLOSED`.

### 4.15 `POST /blocked` — `blockStock` (manual block, no workflow — safety direction)
- Key `quarantine.blocked.block`.
- Body `{ buckets[], blockReasonCode, note?, reason ≥10, signatureId?, idempotencyKey }`.
- Posts `STATUS_TRANSFER AVAILABLE→BLOCKED`. 201 `BlockedStockRef[]` (origin ManualBlock, reviewed false).
- Errors as 4.4 plus 400 `QUARANTINE_BLOCK_REASON_REQUIRED`.

### 4.16 `GET /retest` — `listRetest`
- Key `quarantine.holds.read`. Query `horizonDays` (default 30, ≤ 90), `state`, `warehouseId`. 200 `RetestItem[]`.

### 4.17 `GET /settings` · `PUT /settings` — `getSettings` / `saveSettings`
- GET: key `quarantine.holds.read`. PUT: key `quarantine.settings.manage`.
- Query/body `legalEntityId` (null = tenant default). PUT needs If-Match. Body `Settings` without `workflowTemplate`.
- 200 `Settings`. Errors: 400 (`waitTargetDays` 1–60, candidates 1–10 with prefix `position:`/`user:`, sample location
  not Active/stockable/same warehouse → `QUARANTINE_SAMPLE_LOCATION_INVALID`); 409 `QUARANTINE_CONFLICT`.

### 4.18 `POST /settings/workflow-template` — `prepareWorkflowTemplate`
- Key `quarantine.settings.manage` (+ the caller's Platform `definitions.manage` / `definitions.publish`).
- Body `{ idempotencyKey }`. Creates the draft (`POST /api/v1/workflow/definitions`; `DUPLICATE_TEMPLATE_CODE` = already
  exists) and publishes the shipped JSON (§7) with the caller's token.
- 200 `{ code, templateId, publishedVersion, created: bool }`.
- Errors: 403 `QUARANTINE_WORKFLOW_FORBIDDEN`, 503 `QUARANTINE_WORKFLOW_UNAVAILABLE`.

### 4.19 Block reasons — `GET /block-reasons` · `POST /block-reasons` · `PUT /block-reasons/{code}`
- GET: key `quarantine.holds.read` (`activeOnly`). POST/PUT: key `quarantine.settings.manage`.
- 200/201 `BlockReason`.
- Errors: 400 `QUARANTINE_BLOCK_REASON_LANGUAGE_MISSING`, 409 `QUARANTINE_BLOCK_REASON_EXISTS` /
  `QUARANTINE_BLOCK_REASON_IN_USE` (code change or deactivation of a used code).

### 4.20 Lot release gate (KQ-I) — no new path
- The gate answers through TRACE `POST /api/trace/lots/{lotId}/status` → **202 `PendingApproval { lotId, pendingRequestId,
  contractVersion }`** (already in TRACE 1.2.0, `trace.openapi.yaml:153,168,432-437`). `pendingRequestId` =
  QUARANTINE `decisionId` (kind LotRelease, state Draft).
- The requester then calls §4.7 submit (and signs per policy `quarantine.lot.release`).

### 4.21 `GET /lots/{lotId}/quality-status` — `getLotQualityStatus` (consumers 0142, 0176, 0177, Lot pages)
- Key `quarantine.holds.read`. 200 `LotQualityStatus`. 404 `UNKNOWN_LOT`.

### 4.22 `GET /holds/by-source?sourceModule=&sourceDocumentId=` — `listHoldsBySource` (0142 GRN, 0194)
- Key `quarantine.holds.read`. 200 `HoldRef[]` (≤ 200).

### 4.23 `GET /esign-subjects/{subjectType}/{subjectId}` — `getSignableSubject` (called by Platform, MOD-0022 ESIGN §6)
- Caller's JWT (the signer). Key `quarantine.holds.read`; the company must match the decision's LegalEntity (else 404).
- `subjectType` ∈ {`quarantine.decision`}. 200 `SignableSubject` (§2.12). 404 for unknown / invisible.

### 4.24 Error codes

| Code | HTTP | Meaning |
|---|---|---|
| `QUARANTINE_HOLD_NOT_FOUND` / `QUARANTINE_BLOCKED_NOT_FOUND` / `QUARANTINE_DECISION_NOT_FOUND` | 404 | unknown or other tenant |
| `CROSS_LE_FORBIDDEN` | 403 | other company (W-0173-02) |
| `QUARANTINE_CONFLICT` | 409 | stale If-Match |
| `IDEMPOTENCY_KEY_REUSED` | 400 | same key, different body |
| `REASON_REQUIRED` | 400 | reason < 10 |
| `QUARANTINE_DECISION_INVALID` | 400 | lines / kind shape |
| `QUARANTINE_PARTIAL_SUM_MISMATCH` | 400 | KQ-A total ≠ quantity |
| `QUARANTINE_COA_NOT_CONFIRMED` | 400 | release without CoA check |
| `QUARANTINE_BLOCK_REASON_REQUIRED` | 400 | reject/block without reason code |
| `QUARANTINE_RETEST_DATE_REQUIRED` | 400 | retest release without new date |
| `QUARANTINE_QUANTITY_INVALID` | 400 | disposition quantity |
| `QUARANTINE_SUPPLIER_REQUIRED` | 400 | return without supplier / doc no |
| `QUARANTINE_DESTRUCTION_METHOD_REQUIRED` | 400 | destroy without method |
| `QUARANTINE_SIGNATURE_REQUIRED` | 400 | policy requires a decider signature |
| `QUARANTINE_SIGNATURE_MISMATCH` | 409 | signature not by caller / not this decision / wrong meaning / invalidated |
| `QUARANTINE_DECISION_ALREADY_PENDING` | 409 | one active decision per record |
| `QUARANTINE_DECISION_NOT_DRAFT` / `_NOT_PENDING` / `_NOT_APPLICABLE` | 409 | wrong state |
| `QUARANTINE_NOT_DECIDER` | 403 | withdraw by someone else |
| `QUARANTINE_HOLD_CLOSED` / `QUARANTINE_BLOCKED_CLOSED` | 409 | record decided / closed |
| `QUARANTINE_LOT_HELD` | 409 | lot Blocked/Expired: only Destroy (CT-D4) |
| `QUARANTINE_SAMPLE_LOCATION_NOT_SET` / `_INVALID` | 409 / 400 | KQ-E location missing / wrong |
| `QUARANTINE_REVIEW_NOT_COMPLETED` | 409 | unblock before review |
| `QUARANTINE_STOCK_CHANGED` | 409 | bucket no longer covers the quantity |
| `QUARANTINE_STOCK_RESERVED` | 409 | reservations would be uncovered (`details.reservations[]`) |
| `QUARANTINE_NOTHING_AVAILABLE` | 409 | nothing to quarantine |
| `QUARANTINE_APPROVERS_NOT_CONFIGURED` | 409 | settings without approver candidates |
| `QUARANTINE_WORKFLOW_TEMPLATE_MISSING` | 409 | template not published |
| `QUARANTINE_WORKFLOW_FORBIDDEN` | 403 | caller lacks the Platform workflow key |
| `QUARANTINE_WORKFLOW_REJECTED` | 400 | Platform 4xx |
| `QUARANTINE_WORKFLOW_UNAVAILABLE` | 503 | Platform 5xx / transport |
| `QUARANTINE_BLOCK_REASON_LANGUAGE_MISSING` / `_EXISTS` / `_IN_USE` | 400 / 409 / 409 | block-reason list |
| ledger codes (`INSUFFICIENT_STOCK`, `LOT_BLOCKED`, `LOT_EXPIRED`, `LOT_RETEST_DUE`, `POSTING_PERIOD_CLOSED`, `UNKNOWN_LOCATION`, …) | 422 | passed through from INVENTORY on the no-workflow actions; recorded on `ApplyFailed` |

## 5. Decision state machine (normative)

```
Draft --submit--> Submitted --workflow accepted--> PendingApproval
Submitted --start error--> StartFailed --submit (same key)--> Submitted
PendingApproval --approved--> Approved --apply ok--> Applied
                                      --apply error--> ApplyFailed --apply--> Applied
                                      --approver == decider & fourEyes--> FourEyesViolation
PendingApproval --rejected / cancelled / timed-out--> Rejected / Cancelled / TimedOut
PendingApproval --withdraw--> Withdrawn
Draft --24 h--> Withdrawn
```
- Terminal states: Applied, Rejected, Cancelled, TimedOut, Withdrawn, FourEyesViolation.
- On every terminal state except Applied, the hold or blocked record is reopened.
- No movement is posted before `Approved`.

## 6. Events

- **Consumed:** `platform.workflow.instance.completed.v1` (MOD-0023), filtered on `objectType = quarantine.decision`.
  - Inbox `quarantine_event_inbox` (`_id` = eventId).
  - Applied only if the instance id equals the decision's instance id.
  - Optional transport: registered only when `Eventing:Transport = RabbitMQ`.
  - Read-time reconciliation (§4.1, §4.2, §4.8, §4.9) is the guaranteed path.
- **Published:** none in v1. InventoryService has no transport (`InventoryOutboxMessage.cs:4-6`). Consumers read §4.21 /
  §4.22. A `quarantine.decision.applied.v1` event is a follow-up.

## 7. Workflow template `INV-QUARANTINE-RELEASE` (shipped JSON, published by the tenant Admin — MOD-0022 CT-D6)
```json
{ "stages": [ { "code": "QA", "steps": [ { "code": "QA_APPROVAL", "name": "QA onayı", "type": "approval",
  "assignment": { "candidatePrincipalIds": [] },
  "requirements": { "commentRequired": false, "evidenceRequired": false,
                    "signatureRequired": true, "signatureMeaning": "APPROVE" },
  "sla": { "dueInMinutes": 14400 } } ] } ] }
```
- Published with `schemaVersion: workflow_schema_v1`, `expressionVersion: workflow_expr_v1` (as the CRM templates,
  `WP-CL-CFG-1…md:169`).
- The candidates are empty on purpose: 0175 sends the company's approver candidates at start, because template candidates
  would win (`StartWorkflowInstanceHandler.cs:121-123`).
- `signatureRequired` / `signatureMeaning` are tolerated by MOD-0023 today (unknown keys kept) and enforced once MOD-0022
  E4 lands.

## 8. Non-functional

- List reads ≤ 500 ms p95 for 10 000 open holds per company.
- Reconciliation adds at most one `by-objects` call per read (≤ 100 decisions).
- Apply engine: one ledger posting per line, idempotent keys `quarantine:{decisionId}:{lineNo}`.
- Nothing is deleted: holds, decisions, events, blocked records and history are retained.

## 9. Additive notes needed from other owners (NOT part of this contract)

| Contract | Owner | Additive change | Why |
|---|---|---|---|
| INVENTORY-BUNDLE 1.0.1 | MOD-0173 | new error-code value `LOT_RETEST_DUE` (422) on `POST /movements` and reservations; `available` description gains "excluding retest-due lots" (description-only, like 1.0.1's CT-D1 note) | KQ-H hard block (pack CT-Q3) |
| TRACE 1.2.0 | MOD-0174 | none on the wire (202 `PendingApproval` already reserved); behaviour note: 202 is answered when the company's `lotReleaseRequiresApproval` is on | KQ-I |
| LOCATION 1.1.0 | MOD-0173-FU02 | none (sample location = a normal stockable location chosen in 0175 settings) | KQ-E |
| ESIGN-BUNDLE v1 | MOD-0022 | none (0175 is a provider + consumer under §5/§6) | KQ-G |

No new StockStatus value and no new MovementType are requested.
