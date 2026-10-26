# FEFO-BUNDLE v1 — proposal (MOD-0176 Expiry / FEFO Management)

> **Status: PROPOSAL (draft) — not a contract.** No FEFO contract exists in `docs/analysis/contracts/` (no root file, no
> draft, no README row — README `:9-25`), although the inventory report marks FEFO-BUNDLE "✓"
> (`inventory-capability-scope-and-dependency-report.md:230`: "FEFO constraint | MOD-0176 | 0178 pick, 0172 ATP, 0188
> Demand | FEFO-BUNDLE ✓ | allocation constraint (consumer yön)"). After CT acceptance, F1 writes this as
> `docs/analysis/contracts/fefo.openapi.yaml` (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0176`,
> `x-owner-service: Diten.InventoryService`) in ONE single-writer step and adds the README row. After the freeze only
> additive changes are allowed (K16).
>
> **Source:** pack `execution/domains/supply-chain-execution/module-packs/MOD-0176-expiry-fefo-management.md` (§8 is the
> behaviour authority; this file is the wire shape). Owner decisions KF-A…KF-H (2026-10-12).
>
> **Frozen contracts consumed and not changed here:** INVENTORY-BUNDLE 1.2.1, TRACE 1.3.0, QUARANTINE 1.2.0, ESIGN-BUNDLE
> 1.0.1, PRODUCT-MASTER 1.1.0-draft.11 (`getSku` shelf-life fields). The additive changes 0176 needs from INVENTORY and
> TRACE are in §9 and are **not** part of this contract.

## 1. Rules

1. Server `/api/expiry` (Gateway → Diten.InventoryService **5067**, new route INT-EXPIRY). Tenant shell callers only
   (`actor_type = tenant_user`); consumers 0178 / 0172 / 0188 call with the user's token.
2. `TenantId` from the token. Company from header `X-Legal-Entity-Id` (W-0173-02), validated against MDM. Another company →
   403 `CROSS_LE_FORBIDDEN`; another tenant → 404. Every read and write is scoped to tenant + company.
3. Envelope: InventoryService `Response<T>` (`{ data, error: { code, message, details } }`).
4. Every write takes the `Idempotency-Key` header, equal to body `idempotencyKey` (8–100). Same key + same body → first
   result (200). Same key + different body → 400 `IDEMPOTENCY_KEY_REUSED`.
5. Mutable records (policy, exception case, date override) need `If-Match: "<version>"` on change. Stale → 409
   `EXPIRY_CONFLICT`.
6. **This contract moves no stock and writes no lot.** FEFO order is read-only (KF-F). Hook outcomes (FEFO bypass,
   min-remaining, date override) happen inside INVENTORY `postMovement` and TRACE `createLot` (§9).
7. Every command writes an audit row (pack §8.10) and an exception event where relevant. Signatures are MOD-0022 references
   only (`signatureId`, KF-E).
8. No new stock status or movement type is defined.
9. Dates: `date` = calendar day (UTC), `date-time` = UTC instant.

## 2. Schemas

### 2.1 `ExpiryPolicy`
```json
{ "legalEntityId": "guid", "isDefault": false,
  "approachingDays": 90, "criticalDays": 30,
  "fefoOutboundMode": "Warn | RequireReason | Block",
  "belowMinRemainingMode": "Warn | AutoQuarantine | Reject",
  "dateOverrideMode": "Reason | ReasonAndSignature",
  "slaHours": { "FEFO_BYPASS": 24, "BELOW_MIN_REMAINING": 72, "EXPIRY_DATE_OVERRIDE": 48, "EXPIRED_ON_HAND": 120 },
  "expiredOnHandThresholdDays": 14,
  "updatedAtUtc": "…|null", "updatedBy": { "userId": "…", "name": "…" },
  "version": 3 }
```
Defaults (no stored row, `isDefault: true`): 90 / 30 / RequireReason / AutoQuarantine / ReasonAndSignature (KF-A…KF-D).

### 2.2 `PolicyHistoryEntry`
```json
{ "field": "criticalDays", "oldValue": "30", "newValue": "21", "reason": "…(10–1000)",
  "changedBy": { "userId": "…", "name": "…" }, "changedAtUtc": "…", "policyVersion": 4 }
```

### 2.3 `LotExpiryRow` (approaching / expired lists)
```json
{ "lotId": "string", "lotNumber": "PRS-26A014", "lotStatus": "Draft|Released|Blocked|Expired",
  "item": { "itemId": "guid", "skuId": "guid", "skuLevel": "string", "skuCode": "…", "name": "…" },
  "warehouse": { "id": "guid", "code": "D01", "name": "…" },
  "expiryDate": "date", "retestDate": "date|null", "manufacturingDate": "date|null",
  "daysToExpiry": 17, "daysSinceExpiry": null,
  "severity": "Expired | Critical | Approaching | Ok",
  "remainingShelfLifePct": 9.3, "shelfLifeDays": 730,
  "onHand": { "available": 120, "held": 0, "baseUom": { "id": "guid", "code": "KUTU" } },
  "heldReasons": ["QUARANTINED"], "openExceptionId": "guid|null" }
```
`remainingShelfLifePct = daysToExpiry ÷ shelfLifeDays × 100` (null when `shelfLifeDays` null).
`severity` uses the company policy horizons (KF-A), not TRACE `expiryState`'s fixed 90 days.

### 2.4 `FefoOrder`
```json
{ "legalEntityId": "guid", "warehouseId": "guid", "itemId": "guid", "skuId": "guid", "skuLevel": "string",
  "asOf": "date", "basis": "onHandAvailable",
  "skuAvailable": 340,
  "eligible": [ { "rank": 1, "lotId": "…", "lotNumber": "…", "expiryDate": "date|null", "manufacturingDate": "date|null",
                   "retestDate": "date|null", "available": 120, "severity": "Critical", "allocated": 120 } ],
  "excluded": [ { "lotId": "…", "lotNumber": "…", "expiryDate": "date|null",
                   "reason": "EXPIRED | RETEST_DUE | BLOCKED | HELD | QUARANTINED | BELOW_MIN_REMAINING",
                   "onHand": 40 } ],
  "requestedQuantity": 200, "allocatedQuantity": 200, "shortfall": 0 }
```
Order and exclusion rules: pack §8.2 (expiry asc, nulls last; tie mfg → retest → lotNumber → lotId). `allocated` present
only when `quantity` is sent. `skuAvailable` = INVENTORY `getAvailability.available` for the SKU (reservations are not
lot-bound).

### 2.5 `ExceptionRef`
```json
{ "exceptionId": "guid", "number": "FX-2026-0042",
  "type": "FEFO_BYPASS | BELOW_MIN_REMAINING | EXPIRY_DATE_OVERRIDE | EXPIRED_ON_HAND",
  "origin": "System | Manual",
  "status": "Open | InReview | Accepted | Fixed | Closed | Cancelled",
  "resolution": "AcceptedWithReason | Fixed | Cancelled | null",
  "item": { }, "lot": { "lotId": "…", "lotNumber": "…", "expiryDate": "date|null", "retestDate": "date|null" },
  "warehouse": { }, "quantity": 50, "baseUom": { },
  "openedAtUtc": "…", "dueAtUtc": "…", "late": false, "dueSoon": true,
  "assignee": { "userId": "…", "name": "…" } , "version": 2 }
```
`late = status ∈ {Open, InReview} && now > dueAtUtc`; `dueSoon` = active and < 6 h left.

### 2.6 `ExceptionDetail` = `ExceptionRef` +
```json
{ "sourceMovement": { "transactionId": "…", "movementType": "GOODS_ISSUE_SALES", "sourceModule": "MOD-0183", "sourceDocumentId": "…" },
  "facts": { "firstEligibleLotNumber": "…", "firstEligibleExpiry": "date", "bypassReason": "…" },
  "resolutionReason": "…|null", "resolutionSignatureId": "…|null",
  "pendingResolution": { "resolution": "Fixed", "reason": "…", "setAtUtc": "…", "setBy": { } } ,
  "events": [ { "seq": 1, "action": "Opened|Taken|Commented|Resolved|Closed|Cancelled|SlaBreached", "actor": { }, "atUtc": "…", "text": "…", "signatureId": null } ],
  "links": { "holdId": "guid|null", "reversalTransactionId": "…|null" } }
```
`facts` per type (pack §4.3): FEFO_BYPASS → first eligible lot / expiry / bypass reason; BELOW_MIN_REMAINING → remaining
days, required days, applied stock status; EXPIRY_DATE_OVERRIDE → suggested / entered expiry and retest, reason,
override signatureId; EXPIRED_ON_HAND → days since expiry, threshold.

### 2.7 `DashboardView`
```json
{ "asOf": "date", "period": { "from": "date", "to": "date" }, "warehouseId": "guid|null",
  "kpis": { "fefoCompliancePct": 96.4, "fefoIssues": 412, "fefoBypasses": 15,
            "critical": { "lots": 6, "available": 820, "held": 40 },
            "approaching": { "lots": 21, "available": 3100, "held": 300 },
            "expiredOnHand": { "lots": 3, "quantity": 95 },
            "openExceptions": 9, "lateExceptions": 2, "avgResolutionHours": 18.5 },
  "expiryByMonth": [ { "month": "2026-11", "available": 500, "held": 20 } ],
  "topRisk": [ LotExpiryRow ], "lastSweepAtUtc": "…" }
```
Formulas: pack §8.8. `fefoCompliancePct` = compliant snapshots ÷ all snapshots in period (null when 0 issues).

### 2.8 `DateOverride`
```json
{ "overrideId": "guid", "itemId": "guid", "skuId": "guid", "skuLevel": "string", "lotNumber": "…",
  "manufacturingDate": "date", "suggestedExpiry": "date|null", "enteredExpiry": "date|null",
  "suggestedRetest": "date|null", "enteredRetest": "date|null", "reason": "…",
  "signatureRequired": true, "status": "Pending | Consumed | Expired", "expiresAtUtc": "…", "version": 1 }
```

### 2.9 `EvidenceExport`
```json
{ "exportId": "guid", "period": { "preset": "q | d30 | ytd | custom", "from": "date", "to": "date" },
  "scope": { "warehouseIds": [], "types": [] }, "format": "Csv | Pdf",
  "fileName": "fefo-kanit-2026Q3.csv", "sha256": "hex(64)", "rowCount": 57,
  "exportedBy": { }, "exportedAtUtc": "…" }
```
CSV columns (mockup): `istisna_no;tur;urun;lot;depo;acilis;durum;cozum;imza` (`imza` = signatureId or empty). `sha256` is
of the produced file bytes.

### 2.10 `SignableSubject` (provider, ESIGN-BUNDLE §6 shape)
- `expiry.exception` field list **`fx-v1`** and `expiry.date-override` field list **`do-v1`** — exact key lists in pack
  §8.6. Golden vectors (canonical JSON + SHA-256) are written into `fefo.openapi.yaml` at F3.

## 3. Permissions (namespace `expiry.*`, owner MOD-0176)

`expiry.dashboard.read`, `expiry.fefo-order.read`, `expiry.exceptions.read`, `expiry.exceptions.create`,
`expiry.exceptions.comment`, `expiry.exceptions.review`, `expiry.exceptions.resolve`, `expiry.exceptions.close`,
`expiry.policies.read`, `expiry.policies.manage`, `expiry.evidence.export`, `expiry.date-overrides.create` (API-only).
External, used not declared: `esign.signatures.sign` (MOD-0022), `trace.lots.create` (MOD-0174), inventory posting keys
(MOD-0173), `quarantine.holds.read` (link to hold).

## 4. Operations

Common errors on every operation: 401, 403 `FORBIDDEN` (missing key), 403 `CROSS_LE_FORBIDDEN`, 404 (other tenant /
unknown id), 503 `PRODUCT_MASTER_UNAVAILABLE` where getSku is read.

### Reads

| # | operationId | Method + path | Permission | Query / notes |
|---|---|---|---|---|
| 4.1 | `getDashboard` | `GET /api/expiry/dashboard` | `expiry.dashboard.read` | `warehouseId?`, `periodDays` 1–366 (default 30) → `DashboardView` |
| 4.2 | `listApproaching` | `GET /api/expiry/lots/approaching` | `expiry.dashboard.read` | `warehouseId?`, `skuId?`, `severity? (Critical|Approaching)`, `horizonDays?` (≤ policy approachingDays; default = policy), `includeHeld` (default true), `q`, `page`, `pageSize ≤ 200`, `sort (expiryDate|-expiryDate|lotNumber)` → `Page<LotExpiryRow>` |
| 4.3 | `listExpired` | `GET /api/expiry/lots/expired` | `expiry.dashboard.read` | `warehouseId?`, `skuId?`, `minDaysSinceExpiry?`, paging → `Page<LotExpiryRow>` (`daysSinceExpiry` set) |
| 4.4 | `getFefoOrder` | `GET /api/expiry/fefo-order` | `expiry.fefo-order.read` | **required** `warehouseId`, `skuId`, `skuLevel`, `itemId`; optional `quantity > 0`, `asOf` (date, default today) → `FefoOrder`. Errors: 400 `INVALID_REQUEST`, 404 `UNKNOWN_SKU`. Consumer operation for MOD-0178 / 0172 / 0188. Read-only (KF-F): no reservation is created. |
| 4.5 | `listExceptions` | `GET /api/expiry/exceptions` | `expiry.exceptions.read` | `type?`, `status?` (multi), `late?`, `warehouseId?`, `assignedToMe?`, `q`, paging, `sort (dueAtUtc|-openedAtUtc)` → `Page<ExceptionRef>` |
| 4.6 | `getException` | `GET /api/expiry/exceptions/{exceptionId}` | `expiry.exceptions.read` | → `ExceptionDetail` + `ETag` |
| 4.7 | `getPolicy` | `GET /api/expiry/policy` | `expiry.policies.read` | → `ExpiryPolicy` + `ETag` (defaults when none stored). **Hooks read the policy in-process**, not through this operation. |
| 4.8 | `getPolicyHistory` | `GET /api/expiry/policy/history` | `expiry.policies.read` | paging → `Page<PolicyHistoryEntry>` |
| 4.9 | `previewEvidence` | `GET /api/expiry/evidence/preview` | `expiry.evidence.export` | `preset` / `from` / `to`, `warehouseIds`, `types` → 4 sections (summary KPIs, exceptions, policy changes in period, FEFO bypasses) |
| 4.10 | `listEvidenceExports` | `GET /api/expiry/evidence/exports` | `expiry.evidence.export` | paging → `Page<EvidenceExport>` |
| 4.11 | `getSignableSubject` | `GET /api/expiry/esign-subjects/{subjectType}/{subjectId}` | `expiry.exceptions.read` (exception) / `expiry.date-overrides.create` (override), signer's JWT, company must match | `subjectType ∈ {expiry.exception, expiry.date-override}` → `SignableSubject`; 404 when not visible (ESIGN provider contract) |

### Writes

| # | operationId | Method + path | Permission | Body | Success | Errors | Idempotency / concurrency | Audit |
|---|---|---|---|---|---|---|---|---|
| 4.12 | `openException` | `POST /api/expiry/exceptions` | `expiry.exceptions.create` | `{ idempotencyKey, type, lotId, warehouseId?, quantity?, description (10–1000) }` | 201 `ExceptionDetail` (`origin: Manual`) | 400 `INVALID_REQUEST`, `REASON_REQUIRED`; 404 `UNKNOWN_LOT`; 409 `EXPIRY_EXCEPTION_ALREADY_OPEN` (same type + lot + warehouse active; details.exceptionId) | key | `expiry.exception.opened` |
| 4.13 | `takeException` | `POST /api/expiry/exceptions/{id}/take` | `expiry.exceptions.review` | `{ idempotencyKey }` | 200 (status `InReview`, assignee = caller) | 409 `EXPIRY_INVALID_TRANSITION`, `EXPIRY_CONFLICT` | key + If-Match | `.taken` |
| 4.14 | `commentException` | `POST /api/expiry/exceptions/{id}/comments` | `expiry.exceptions.comment` | `{ idempotencyKey, text (1–2000) }` | 201 event | 409 when `Closed`/`Cancelled` | key (no If-Match; append-only) | `.commented` |
| 4.15 | `prepareResolution` | `POST /api/expiry/exceptions/{id}/pending-resolution` | `expiry.exceptions.resolve` | `{ idempotencyKey, resolution, reason (10–1000) }` | 200 `{ signatureRequired, actionCode, meaningCode, visualMode, subject: { module: "expiry", subjectType: "expiry.exception", subjectId } }` (from `getEffectivePolicy`) | 409 `EXPIRY_INVALID_TRANSITION`, `EXPIRY_CONFLICT` | key + If-Match; replaces a previous pending resolution of the same user; expires 15 min | — |
| 4.16 | `resolveException` | `POST /api/expiry/exceptions/{id}/resolve` | `expiry.exceptions.resolve` | `{ idempotencyKey, resolution, reason, signatureId? }` (must equal the pending resolution) | 200 status `Accepted` / `Fixed` / `Cancelled` | 400 `REASON_REQUIRED`, `EXPIRY_SIGNATURE_REQUIRED`; 409 `EXPIRY_SIGNATURE_MISMATCH` (signer ≠ caller, action ≠ resolution's, meaning ≠ policy's, stale hash, invalidated, reused), `EXPIRY_PENDING_RESOLUTION_MISMATCH`, `EXPIRY_INVALID_TRANSITION`, `EXPIRY_CONFLICT` | key + If-Match | `.resolved` (+ signatureId) |
| 4.17 | `closeException` | `POST /api/expiry/exceptions/{id}/close` | `expiry.exceptions.close` | `{ idempotencyKey, note? }` | 200 `Closed` | 409 `EXPIRY_INVALID_TRANSITION` (only from `Accepted`/`Fixed`), `EXPIRY_CONFLICT` | key + If-Match | `.closed` |
| 4.18 | `updatePolicy` | `PUT /api/expiry/policy` | `expiry.policies.manage` | full `ExpiryPolicy` writable members + `reason (10–1000)`, `idempotencyKey` | 200 `ExpiryPolicy` (version + 1) | 400 `EXPIRY_POLICY_INVALID` (critical ≥ approaching, ranges), `REASON_REQUIRED`; 409 `EXPIRY_CONFLICT`; 409 `EXPIRY_NO_CHANGES` | key + If-Match (`"0"` creates the first row) | `expiry.policy.updated` + one history row per changed field |
| 4.19 | `createDateOverride` | `POST /api/expiry/date-overrides` | `expiry.date-overrides.create` | `{ idempotencyKey, itemId, skuId, skuLevel, lotNumber, manufacturingDate, expiryDate?, retestDate?, reason (10–1000) }` | 201 `DateOverride` (suggestions computed from getSku; `signatureRequired` from policy `dateOverrideMode`) | 400 `INVALID_REQUEST`, `REASON_REQUIRED`; 422 `EXPIRY_NO_SUGGESTION` (no `shelfLifeDays`/`retestDays` or no manufacturing date — nothing to override); 422 `EXPIRY_DATE_EQUALS_SUGGESTION`; 503 `PRODUCT_MASTER_UNAVAILABLE` | key | `expiry.date-override.prepared` |
| 4.20 | `createEvidenceExport` | `POST /api/expiry/evidence/exports` | `expiry.evidence.export` | `{ idempotencyKey, preset|from+to, warehouseIds, types, format }` | 201 `EvidenceExport` + `Location` of the file (`GET /api/expiry/evidence/exports/{id}/file`, same key) | 400 `INVALID_REQUEST` (range > 366 days) | key (same key → same file + hash) | `expiry.evidence.exported` |

Not offered (by decision): lot reservation (KF-F), product / market rules (KF-G, KF-H), manual SLA override, deletion of
any record.

### 4.21 Error codes (owned by FEFO-BUNDLE)

| Code | HTTP |
|---|---|
| `EXPIRY_POLICY_INVALID` | 400 |
| `EXPIRY_SIGNATURE_REQUIRED` | 400 |
| `EXPIRY_SIGNATURE_MISMATCH` | 409 |
| `EXPIRY_PENDING_RESOLUTION_MISMATCH` | 409 |
| `EXPIRY_INVALID_TRANSITION` | 409 |
| `EXPIRY_CONFLICT` | 409 |
| `EXPIRY_NO_CHANGES` | 409 |
| `EXPIRY_EXCEPTION_ALREADY_OPEN` | 409 |
| `EXPIRY_DATE_OVERRIDE_NOT_USABLE` | 409 (consumed / expired — returned by TRACE `createLot` through the shared rule) |
| `EXPIRY_NO_SUGGESTION` | 422 |
| `EXPIRY_DATE_EQUALS_SUGGESTION` | 422 |
| shared: `INVALID_REQUEST`, `REASON_REQUIRED`, `IDEMPOTENCY_KEY_REUSED`, `UNKNOWN_LOT`, `UNKNOWN_SKU`, `CROSS_LE_FORBIDDEN`, `PRODUCT_MASTER_UNAVAILABLE` | 400 / 404 / 403 / 503 |

## 5. Exception state machine

```
Open ──take──▶ InReview
Open | InReview ──resolve(AcceptedWithReason)──▶ Accepted ──close──▶ Closed
Open | InReview ──resolve(Fixed)──────────────▶ Fixed    ──close──▶ Closed
Open | InReview ──resolve(Cancelled)──────────▶ Cancelled
```
Resolve signature: by MOD-0022 effective policy per action (`expiry.exception.accept` / `.fix` / `.cancel`), default
required for all three (KF-E). Close: no signature. Terminal: `Closed`, `Cancelled`.

System opening (not operations of this contract — they happen inside INVENTORY / TRACE writes and the sweeper):
`FEFO_BYPASS` (outbound hook), `BELOW_MIN_REMAINING` (GR hook), `EXPIRY_DATE_OVERRIDE` (createLot), `EXPIRED_ON_HAND`
(sweeper). Dedup: one active case per `{type}:{transactionId}` or `{type}:{lotId}:{warehouseId}`.

## 6. Events

MVP-1 publishes no bus event (domain-config Event Bus is consumed backbone; no consumer asks yet). Audit rows (pack §8.10)
and exception events (`ExceptionDetail.events`) are the record. A future `expiry.exception.opened` integration event is
additive.

## 7. Signable actions (registered, ESIGN-BUNDLE §4.23)

Module `expiry`: `expiry.lot-date.override`, `expiry.exception.accept`, `expiry.exception.fix`, `expiry.exception.cancel`
— `defaultRequired: true`, default meaning `APPROVE` (CT-F8 for `REVIEW`), visual mode `savedAccepted`, names in 7
languages. Provider base URL from operator config `ESign:SubjectProviders:expiry:BaseUrl`.

## 8. Non-functional

- `getFefoOrder` p95 ≤ 150 ms at 200 lots per SKU and warehouse; hooks add ≤ 50 ms p95 to a posting (pack §16 F2).
- Lists server-paged, `pageSize ≤ 200`.
- Indexes: pack §8.8.
- Sweeper: at start + every 900 s (`Inventory:Expiry:SweepIntervalSeconds`), tenant + company scoped, idempotent.
- 7 languages for every user-visible name (types, statuses, resolutions, exclusion reasons, action names).

## 9. Additive notes for other owners (NOT part of this contract)

### 9.1 INVENTORY-BUNDLE 1.3.0 (owner MOD-0173)
- `MovementRequest.fefoBypassReason` — string 10–1000, optional; part of the idempotency fingerprint.
- `MovementResponse.expiryChecks` — optional `{ fefo: Compliant|Bypassed|NotApplicable, minRemaining: Ok|NotDefined|Warned|Quarantined|NotApplicable, appliedToStockStatus, exceptionIds[] }`.
- 422 codes: `FEFO_BYPASS_REASON_REQUIRED`, `FEFO_ORDER_VIOLATION`, `LOT_BELOW_MIN_REMAINING_SHELF_LIFE` (details carry
  first eligible lot / remaining and required days).
- `postMovement` description: under the company expiry policy a `GOODS_RECEIPT_PO` below the SKU's
  `minRemainingShelfLifeDays` may be booked into `QUARANTINE` (KF-C; **CT-F1**; fallback = 422 with
  `details.requiredToStockStatus`).
- Description-only: add `LOT_EXPIRED` / `LOT_BLOCKED` examples to `UnprocessableMovement` (`:487-499` lists neither).

### 9.2 TRACE 1.4.0 (owner MOD-0174)
- `CreateLotRequest.dateOverride` — optional `{ overrideId, signatureId? }`.
- `LotRef.expiryOverride` (optional) and `LotRef.suggestedExpiryDate` / `suggestedRetestDate` (optional).
- History action `ExpiryOverridden`.
- `createLot` codes: 422 `LOT_DATE_OVERRIDE_REQUIRED`, 400 `LOT_DATE_OVERRIDE_SIGNATURE_REQUIRED`, 409
  `LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH`, 409 `LOT_DATE_OVERRIDE_MISMATCH`, 409 `EXPIRY_DATE_OVERRIDE_NOT_USABLE`.
- `expiryState` keeps its fixed 90-day window; description note that company horizons are in FEFO-BUNDLE (FU-0176-TRACE-01).
- CT-Q17 (`updateLot` retest later on non-Draft → 400 `LOT_KEY_IMMUTABLE`) unchanged.

### 9.3 QUARANTINE — none required (optional `HoldRef.originReason`, CT-F9).
### 9.4 ESIGN — none (consumer only).
### 9.5 PRODUCT-MASTER — none; needs `shelfLifeDays`, `retestDays`, `minRemainingShelfLifeDays` on `getSku` frozen/live (`:729-732`).
