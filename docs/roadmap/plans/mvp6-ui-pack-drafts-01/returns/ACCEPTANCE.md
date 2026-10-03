# MOD-0186 Returns UI — single acceptance matrix DRAFT

Process v1.0 §4: one row per criterion with expected HTTP / browser / DB result, owner, evidence type and dependency.
Generic DataTable conventions are marked IN or OUT **here, at the start**. OUT rows are proposed scope-change
records, not silent waivers. **No row has run.** Every result below is an expectation. Readiness: READY = can run once DEV exists; BLOCKED = waits on the named gate.

Evidence types: BR = real-Auth browser (separate profiles, no cookie swap); HTTP = captured MVC/Gateway request and
response (redacted); DB = scoped before/after counts in the DB-010 isolated replica set (`returns`,
`return_entitlements`, `returns_receipts`, `returns_audit`, `returns_outbox`); ST = static source/DOM scan.

## Early vertical slice (runs first, before the rest is built)

| ID | Slice | Expected HTTP | Expected browser | Expected DB | Owner | Evidence | Dependency | Readiness |
|---|---|---|---|---|---|---|---|---|
| RU-VS1 | Fresh real-Auth actor (returns.read/create/transition/authorize + shipments.read, T1/LE-A) opens `/SupplyChain/Returns`, resolves a **Delivered** Shipment created via the published Shipment API (with non-null root), creates a Return for 1 line, and the list reload shows it | GET page 200; GET list 200 `items/total/v1`; GET resolve 200; POST create → Gateway `X-Correlation-Id` = Shipment root, `Idempotency-Key` present, X-Tenant/LE from session → 201 `Requested`, `rmaNumber RMA-…`; reload list includes the row | Tenant shell, no console error, L10n loaded, row visible with localized status | +1 return, +1 entitlement per line, +1 receipt, +1 audit, +1 Pending outbox; nothing else | UI writer | BR+HTTP+DB | G-TARGET, gateway routes, G-SHIPREAD grant, seeded Delivered shipment | BLOCKED |

The slice proves DI, route, gateway, L10n, permission snapshot and the root seam end-to-end. Nothing else is built until it passes.

## Matrix

| ID | Criterion | Scope | Expected HTTP | Expected browser | Expected DB | Owner | Evidence | Dependency | Readiness |
|---|---|---|---|---|---|---|---|---|---|
| RU-01 | Same-origin chain | IN | Browser XHR only to `/SupplyChain/Returns/api*`; adapter targets Gateway 5000 only | No request to 5000/5061 in the browser network log | none | UI writer | HTTP+BR | gateway routes | BLOCKED |
| RU-02 | List query parity | IN | Only `shipmentId` and single `status` forwarded; `All` omits `status`; no page/search/sort/unknown params; X-Tenant/LE headers from session | Filter Apply/Reset issue only those params | zero delta | UI writer | HTTP | — | READY |
| RU-03 | Envelope and absent fields | IN | 200 with missing summary field; malformed envelope | Absent → "not provided"; no action without `returnId`; malformed → error state, not empty | zero | UI writer | BR (fault fixture labelled) | — | READY |
| RU-04 | Skeleton / empty / error distinct | IN | 200 `items:[]`; 503 | Skeleton while pending (Slow-3G); localized empty only for `[]`; error with support ref for 503; no infinite skeleton | zero | UI writer | BR | — | READY |
| RU-05 | UAS-001 no read | IN | Authenticated user without read: page 200 with denial body; list adapter 403 | Only `_AccessDenied` in shell: no title, filter, table, skeleton, button, toast, redirect | zero | UI writer | BR+HTTP | shared `_AccessDenied` in target | READY |
| RU-06 | Create CTA gating | IN | read-only user direct POST create → 403 zero-write | No create CTA without create (+ shipments.read) | zero on denial | UI writer | BR+HTTP+DB | G-SHIPREAD | BLOCKED |
| RU-07 | Action gating per target | IN | Each target with/without `.transition` and target key → 200/403; denied zero-write | Only permitted, status-legal actions rendered | zero on denial | UI writer | BR+HTTP+DB | — | READY |
| RU-08 | Create body parity | IN | POST body = `shipmentId, reasonCode, lines[{shipmentLineNumber, quantity, uomId}], evidenceReferenceIds?` only; no tenant/LE/status/root/key in body | Quantity sent as typed text; UoM from source line | 1/1-per-line/1/1/1 | UI writer | HTTP+DB | — | READY |
| RU-09 | Presence ≠ nonempty | IN | Empty `reasonCode` accepted by backend; empty evidence string retained | No HTML `required`/trim/maxlength added; empty reason submits | +1 set with exact empty text in audit | UI writer | BR+HTTP+DB | — | READY |
| RU-10 | Resolve stale-response guard | IN | Two resolves, first delayed | Late first response never populates the panel for the second UUID | zero | UI writer | BR (delay fixture) | — | READY |
| RU-11 | Safe-not-found, create path | IN | Unknown, foreign-LE and soft-deleted Shipment → identical 404 body except correlation | One localized safe-not-found text + support ref; no line table or shipment data left in DOM, network or console | zero | UI writer | BR+HTTP+DB | — | READY |
| RU-12 | Safe-not-found, transition path | IN | Return deleted/foreign after list load → 404 `RETURN_NOT_FOUND` | Same safe-not-found text; action closed; reload; hidden surfaces inert and not keyboard-reachable | zero | UI writer | BR+HTTP+DB | — | READY |
| RU-13 | Cross-LE list isolation | IN | LE-B actor list returns only LE-B rows; `?shipmentId=<LE-A shipment>` → 200 `[]` | LE-B sees localized empty state, no LE-A data | zero | UI writer | BR+HTTP | three real-Auth identities | READY |
| RU-14 | Eligibility display vs server | IN | Direct POST for Draft/Planned/Dispatched shipment → 422, zero-write | Note shown, submit disabled for ineligible status | zero on 422 | UI writer | BR+HTTP+DB | — | READY |
| RU-15 | Root seam | IN | Create/transition carry Shipment `lifecycleCorrelationId`; null-root shipment → 503 `RETURN_SHIPMENT_ROOT_UNAVAILABLE`; malformed → 502 | Plain unavailable/invalid message; root never in DOM, network response to browser, or storage | zero on 5xx | UI writer + Shipment owner | HTTP+DB | producer root uptake in target | BLOCKED |
| RU-16 | Idempotent create/transition | IN | Lost response/503 → same key and body → 201/200 `idempotentReplay:true`; changed body same key → 409 `IDEMPOTENCY_KEY_REUSED` | One pending request on double-click; replay shown as completed then reload; retry stops on 409 | exactly one write set per intent | UI writer | BR+HTTP+DB | DN-01 retry policy | BLOCKED |
| RU-17 | Stale transition | IN | Actor-a authorizes; actor-b's open Reject → 422 `INVALID_RETURN_TRANSITION` | Actor-b: conflict + support ref, no false success, reload shows Authorized | actor-b zero delta | UI writer | BR+HTTP+DB | two browser profiles | READY |
| RU-18 | Received = manual assertion | IN | InTransit→Received 200 with optional inventory ref null/empty/opaque | Label and confirmation say "manual assertion", not warehouse receipt or stock posting | +1 audit with manual-assertion evidence type; zero Inventory/Warehouse HTTP | UI writer | BR+HTTP+DB | — | READY |
| RU-19 | Disposition code | IN | Dispositioned without code or empty → 422; whitespace accepted | Field shown only for Dispositioned; no trim | zero on 422 | UI writer | BR+HTTP+DB | — | READY |
| RU-20 | occurredAt offset | IN | Sent RFC 3339 with offset; instant preserved | Editable, default now; no past/future restriction added | audit instant equals input instant | UI writer | HTTP+DB | — | READY |
| RU-21 | Support reference vs root | IN | Every response has `X-Correlation-Id`; error `correlationId` equals header | Support reference copyable; never labelled as lifecycle root | — | UI writer | BR+HTTP | — | READY |
| RU-22 | Seven languages + RTL | IN | — | en, tr, fr, es, zh, ar, ru: all labels, status, errors, denial, modal, toolbar; no raw key or English placeholder; Arabic RTL | — | UI writer | BR+ST | — | READY |
| RU-23 | Responsive + keyboard | IN | — | 390/768/1024/1440, no horizontal overflow; line/evidence editors keep unique ids/labels; focus/Escape in offcanvas and modal | — | UI writer | BR | — | READY |
| RU-24 | Premium dialogs only | IN | — | Shared SweetAlert2 wrappers; no `alert/confirm`, no inline handlers; no token/cookie in web storage | — | UI writer | ST+BR | — | READY |
| RU-25 | No SoR duplication | IN | Zero Inventory/Warehouse/finance calls from UI or adapter | — | no new collection | UI writer | HTTP+DB | — | READY |
| RU-26 | Regression | IN | Shipment/Carrier/Loads UI and API unchanged; Returns backend 78/78 suite still passes on target | Shipment list/detail still work | — | integration owner + VER | HTTP+BR | G-TARGET | BLOCKED |
| RU-27 | Source→binary→process→browser | IN | Exact owned+shared hashes, build output, DLL, PID/port, browser URL/timestamp | — | before/after per mutation and negative | independent VER | all | — | READY |
| RU-28 | PNG evidence | IN | — | Only a supported save/export; otherwise OPEN, no workaround | — | environment owner | BR | PRES-183-04 | BLOCKED |
| RU-29 | QuickView | IN | No by-ID request when opening QuickView | Row summary + copyable `returnId` only | zero | UI writer | BR+HTTP | — | READY |

## Generic DataTable template items decided OUT at the start (proposed scope-change records)

| ID | Generic item | Decision | Reason |
|---|---|---|---|
| RU-SCR-01 | Checkbox column + BulkActionBar | OUT | No bulk operation is published |
| RU-SCR-02 | Edit / delete / bulk delete | OUT | No update/delete operation |
| RU-SCR-03 | Import / export | OUT | Not published; client export would be an unreviewed data egress |
| RU-SCR-04 | Server paging / search / sort | OUT | `queryReturns` has only `shipmentId` and `status` |
| RU-SCR-05 | Multi-select status filter | OUT | Contract takes one status; single-select with `ShowAll` |
| RU-SCR-06 | Generic `verify_datatable_page.py --reference slim` as acceptance | OUT (record only) | Run and keep its raw log beside the bounded result; it cannot pass OUT items |

Each OUT row stays OUT only if the owner approves it in APPROVAL-DECISION.md. Otherwise it becomes an IN row that must be built and pass.
