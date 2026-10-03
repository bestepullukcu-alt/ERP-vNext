# MOD-0187 Claims UI — single acceptance matrix DRAFT

Process v1.0 §4. **No row has run;** every result is an expectation. Evidence types: BR (real-Auth browser,
separate profiles), HTTP (redacted MVC/Gateway capture), DB (scoped counts in the DB-010 isolated replica set:
`claims`, `claims_receipts`, `claims_audit`, `claims_outbox`), ST (static scan).

## Early vertical slice (runs first)

| ID | Slice | Expected HTTP | Expected browser | Expected DB | Owner | Evidence | Dependency | Readiness |
|---|---|---|---|---|---|---|---|---|
| CU-VS1 | Fresh real-Auth actor (claims.read/create + shipments.read, T1/LE-A) opens `/SupplyChain/Claims`, resolves a **Dispatched** Shipment created through the published Shipment API (non-null root, no carrier), creates a claim with `claimedAmount "250.00"`, `currency "EUR"`, and the reload shows it | Page/list/resolve 200; POST create → Gateway `X-Correlation-Id` = Shipment root, key present, body without carrierId → 201 `Open`, `claimNumber CLM-…`; list row `claimedAmount` exactly `"250.00"` | Tenant shell, localized, amount rendered as `250.00` (no reformat) | +1 claim, +1 receipt, +1 audit, +1 Pending outbox | UI writer | BR+HTTP+DB | G-TARGET, gateway, G-SHIPREAD, seeded shipment | BLOCKED |

## Matrix

| ID | Criterion | Scope | Expected HTTP | Expected browser | Expected DB | Owner | Evidence | Dependency | Readiness |
|---|---|---|---|---|---|---|---|---|---|
| CU-01 | Same-origin chain | IN | Browser XHR only to `/SupplyChain/Claims/api*`; adapter → Gateway 5000 | No browser request to 5000/5061 | none | UI writer | HTTP+BR | gateway | BLOCKED |
| CU-02 | List query parity | IN | Only `shipmentId` + single `status`; no scope headers or scope query keys; no paging/search/sort | Filter Apply/Reset only those | zero | UI writer | HTTP | — | READY |
| CU-03 | Envelope/absent fields | IN | 200 with missing fields; malformed envelope | "Not provided"; no action without `claimId`; malformed → error | zero | UI writer | BR | — | READY |
| CU-04 | Exact amount text | IN | List returns `"000250.00"` / long 80-digit value | Rendered byte-exact, LTR, no grouping/rounding/truncation | — | UI writer | BR+HTTP | — | READY |
| CU-05 | Skeleton/empty/error | IN | `[]`; 503 | Distinct states; no infinite skeleton | zero | UI writer | BR | — | READY |
| CU-06 | UAS-001 no read | IN | Page denial; list adapter 403 | Only `_AccessDenied` in shell | zero | UI writer | BR+HTTP | shared partial in target | READY |
| CU-07 | Create CTA gating | IN | No-create direct POST → 403 zero-write | No CTA without create (+ shipments.read) | zero | UI writer | BR+HTTP+DB | G-SHIPREAD | BLOCKED |
| CU-08 | Action gating matrix | IN | Each target × missing/exact/unrelated grant → 200/403; create-only Withdrawn 403; settle-only Close 403 | Only permitted legal actions rendered | zero on denial | UI writer | BR+HTTP+DB | — | READY |
| CU-09 | Create body parity | IN | Body = `shipmentId, carrierId?, reasonCode, claimedAmount (string), currency, evidenceReferenceIds?`; amount never a JSON number; no tenant/LE/root/key in body | — | 1/1/1/1 | UI writer | HTTP+DB | — | READY |
| CU-10 | Carrier link | IN | Checked → Shipment carrierId sent → 201; unchecked → omitted → 201; direct POST different carrier → 422 `CLAIM_CARRIER_MISMATCH` | Checkbox disabled when the Shipment has no carrier | zero on 422 | UI writer | BR+HTTP+DB | — | READY |
| CU-11 | Amount and currency lexical | IN | `1e2`, `+1`, ` 1`, `1.`, `.1` → 400; `0`, `-0`, `-1` → 422 `CLAIM_AMOUNT_INVALID`; `usd` → 400; `ZZZ` → 201 | Typed text sent unchanged; no auto-uppercase; errors localized | zero on reject | UI writer | BR+HTTP+DB | — | READY |
| CU-12 | Presence ≠ nonempty | IN | Empty `reasonCode`, empty evidence string, empty resolution/note accepted | No added required/trim/maxlength | exact empty text in audit | UI writer | BR+HTTP+DB | — | READY |
| CU-13 | Resolve stale-response guard | IN | Delayed first resolve | Late response never populates | zero | UI writer | BR | — | READY |
| CU-14 | Safe-not-found, create | IN | Unknown / foreign-LE / soft-deleted Shipment → identical 404 except correlation | One localized text + support ref; no shipment data in DOM/network/console | zero | UI writer | BR+HTTP+DB | — | READY |
| CU-15 | Safe-not-found, transition | IN | Claim foreign/deleted after load → 404 `CLAIM_NOT_FOUND` | Same text; action closed; hidden surfaces inert, not keyboard-reachable; reload | zero | UI writer | BR+HTTP+DB | — | READY |
| CU-16 | Cross-LE list | IN | LE-B list excludes LE-A; `?shipmentId=<LE-A>` → `[]` | Localized empty | zero | UI writer | BR+HTTP | three identities | READY |
| CU-17 | Eligibility display vs server | IN | Direct POST for Draft/Planned/Cancelled → 422 `CLAIM_SHIPMENT_INELIGIBLE` | Note + disabled submit for those | zero | UI writer | BR+HTTP+DB | — | READY |
| CU-18 | Root seam | IN | Mutations carry Shipment root; null root → 503 `CLAIM_REFERENCE_INCOMPLETE`; malformed → 502 | Plain messages; root never reaches the browser | zero on 5xx | UI writer + Shipment owner | HTTP+DB | producer uptake in target | BLOCKED |
| CU-19 | Idempotency | IN | Same key+identical text → 201/200 replay; `"250"`→`"250.00"` same key → 409 `IDEMPOTENCY_KEY_REUSED`; retry keeps exact `occurredAt` text | Single pending; replay → completed + reload; retry stops on 409 | one write set per intent | UI writer | BR+HTTP+DB | DN-01 | BLOCKED |
| CU-20 | Approval amount | IN | Approved with missing/`-0.01`/`250.01` → 422 `CLAIM_APPROVAL_AMOUNT_INVALID`; `0`, `-0`, `250` → 200; `approvedAmount` never sent on other targets | Field only on Approved; response `approvedAmount` shown in the success message | exact text in audit | UI writer | BR+HTTP+DB | — | READY |
| CU-21 | Stale transition | IN | Actor-a Approves; actor-b's open Reject → 422 `INVALID_CLAIM_TRANSITION` | Actor-b conflict + support ref + reload | actor-b zero | UI writer | BR+HTTP+DB | two profiles | READY |
| CU-22 | Settled wording | IN | Approved→Settled 200 | Label/confirmation say no payment/AP/AR | no finance call or collection | UI writer | BR+HTTP+DB | — | READY |
| CU-23 | Support ref vs root | IN | Header equals `error.correlationId` | Copyable support reference; never labelled root | — | UI writer | BR+HTTP | — | READY |
| CU-24 | Seven languages + RTL | IN | — | Full coverage incl. all 18 annex codes' localized messages; no raw key/placeholder | — | UI writer | BR+ST | — | READY |
| CU-25 | Responsive + keyboard | IN | — | 390/768/1024/1440; evidence editor ids; focus/Escape | — | UI writer | BR | — | READY |
| CU-26 | Premium dialogs, no secrets | IN | — | Shared wrappers only; no native dialogs/inline handlers; no token in storage | — | UI writer | ST+BR | — | READY |
| CU-27 | Family routing | IN | `/api/shipment-bundle/claimsXYZ` not routed to Claims; `/SupplyChain/ClaimsXYZ` 404 | — | — | integration owner | HTTP | gateway | BLOCKED |
| CU-28 | Regression | IN | Shipment/Carrier/Loads/Returns unchanged; Claims 36/36 reference + same-host suite green on target | Shipment UI works | — | integration + VER | HTTP+BR | G-TARGET | BLOCKED |
| CU-29 | Source→binary→process→browser | IN | Hashes, build, DLL, PID/port, URL/timestamp | — | before/after per mutation/negative | independent VER | all | — | READY |
| CU-30 | PNG | IN | — | Supported export only; else OPEN | — | environment owner | BR | PRES-183-04 | BLOCKED |
| CU-31 | QuickView | IN | No by-ID request | Row summary + copyable `claimId` | zero | UI writer | BR+HTTP | — | READY |

## Generic DataTable items decided OUT at the start (proposed scope-change records)

| ID | Generic item | Decision | Reason |
|---|---|---|---|
| CU-SCR-01 | Checkbox + BulkActionBar | OUT | No bulk operation |
| CU-SCR-02 | Edit/delete/bulk delete | OUT | Not published |
| CU-SCR-03 | Import/export | OUT | Not published; export would be unreviewed egress of amounts |
| CU-SCR-04 | Server paging/search/sort | OUT | `queryClaims` has only `shipmentId`, `status` |
| CU-SCR-05 | Multi-select status | OUT | Single status in contract |
| CU-SCR-06 | Generic verifier as acceptance | OUT (record only) | Raw log kept beside the bounded result |
