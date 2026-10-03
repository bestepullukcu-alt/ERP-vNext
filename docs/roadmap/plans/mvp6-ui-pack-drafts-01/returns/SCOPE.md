# MOD-0186 Returns — tenant UI scope DRAFT (Q33)

2026-09-26 · Documents only. **This draft authorizes nothing:** no pack edit, UI DEV, gateway, shared registration,
pack promotion or dispatch. Measured on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.

## 1. Identity (DCP-002)

| Question | Answer |
|---|---|
| New module, FU/child, or revision? | **UI revision inside the existing MOD-0186 pack.** The UI presents the same owned objects (ReturnOrder/ReturnLine) and only the three published operations the accepted backend already serves. It adds no new capability, object or operation, so AGENTS.md DCP-002 step 4 does not justify a new ID. Pack §11 ("no UI/form shape is invented") and §9 (`shell: none`) are the sections a future revision replaces. A `MOD-0186-FUxx` would be required only if the owner later asks for a capability outside these three operations (for example warehouse-verified receiving); no FU ID is proposed or minted here. |
| Canonical ID/name | `MOD-0186` / `Reverse Logistics` (unchanged). |
| Command to run before the pack revision | `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0186 --name "Reverse Logistics"` |
| Result observed in this lane | `OK  MOD-0186: proven against Blueprint/registry.` exit 0, 2026-09-26 ~01:14 Istanbul (read-only run; the script writes nothing). Must be rerun by the pack author against the final target. |
| Pack preimage | **Open gate G-TARGET.** The common-checkout pack reads `status: draft` (SHA-256 `07a8a015…b7`). The accepted isolated pack is `1c80cca7…` (`mvp6-mod0186-wp-acceptance-01/SOP-22.md`). CT names which preimage the UI revision patches; this draft does not choose. |

Proposed frontmatter change in the revision (unapplied): `shell: tenant`, `golden_reference: slim`,
`form_field_count: 6`. The backend `ready-for-dev` state of the isolated pack does not transfer to the UI; the UI
section needs its own approval.

## 2. Published operations bound (nothing else)

Contract: `docs/analysis/contracts/shipment-bundle.openapi.yaml` SHIPMENT-BUNDLE **3.0.0 / wire v1**, SHA-256
`5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` (matches the accepted WP record). Returns annex
`returns-semantics-v3.0.0.md` SHA-256 `00990a28…11` (matches).

| Operation | Use in UI |
|---|---|
| `queryReturns` GET `/returns?shipmentId=&status=` | List, filter, reload after mutation |
| `createReturn` POST `/returns` | Create offcanvas |
| `transitionReturn` POST `/returns/{returnId}/transition` | Row lifecycle action |
| `getShipment` GET `/shipments/{shipmentId}` (consumed, Shipment-owned) | Resolve shipment number/status/lines for create; resolve `lifecycleCorrelationId` server-side for create/transition correlation |

**Not in the contract, therefore not in the UI:** Return by-ID GET, edit, delete, bulk, import/export, server paging,
search or sort, reason/disposition catalogues, remaining-entitlement query, Inventory or Warehouse calls.

## 3. Key finding: transition UI is feasible (unlike Loads ROOT-UI-01)

The Returns annex binds every Return to the **Shipment's persisted `lifecycleCorrelationId`** ("Persist inherited
Shipment root, not local create root"). Create must send `X-Correlation-Id` equal to that root, and transitions
must match the saved root (mismatch → 409 `CORRELATION_ROOT_MISMATCH`). The published `getShipment` exposes
`lifecycleCorrelationId`, and `ReturnSummary` carries `shipmentId`. The MVC adapter can therefore obtain the root
from a published seam, server-side, without deriving it from IDs, traces or the database. The root is never sent to
or accepted from the browser. If the Shipment root is null/missing, the backend returns 503
`RETURN_SHIPMENT_ROOT_UNAVAILABLE` and the UI shows that plainly; it does not retry or substitute.

The browser-supplied `shipmentId` on a transition is only a lookup hint. The authoritative check stays in the backend
(wrong or foreign shipment → 404 from `getShipment`, or 409 root mismatch). Both fail closed with zero writes.

**Dependency G-SHIPREAD:** the adapter's `getShipment` call runs with the actor's token, so create/transition
actors also need `supplychain.shipments.read`. Whether that is acceptable, or the integration owner provides another
published path, is an owner/security decision (see APPROVAL-DECISION.md).

## 4. Screens, routes and permissions

Tenant shell `_LayoutTenantShell.cshtml`, area folder `Views/SupplyChain/Returns/`, no `/Platform` prefix.
Browser → same-origin MVC → Gateway 5000 → SupplyChain 5061. The browser never calls 5000/5061 and never holds a bearer token.

| Surface | MVC route (proposed) | Gateway downstream | UI display gate | Backend authority |
|---|---|---|---|---|
| Page | GET `/SupplyChain/Returns` | — | `supplychain.returns.read` | — |
| List adapter | GET `/SupplyChain/Returns/api?shipmentId=&status=` | GET `/api/shipment-bundle/returns` | read | `queryReturns` |
| Shipment resolve adapter | GET `/SupplyChain/Returns/api/shipments/{shipmentId:guid}` | GET `/api/shipment-bundle/shipments/{shipmentId}` | returns.create + shipments.read | `getShipment` (projection to number, status, lines only; root not returned to browser) |
| Create adapter | POST `/SupplyChain/Returns/api` | GET shipment root, then POST `/api/shipment-bundle/returns` | returns.create (+ shipments.read) | `createReturn` |
| Transition adapter | POST `/SupplyChain/Returns/api/{returnId:guid}/transition` | GET shipment root, then POST `/api/shipment-bundle/returns/{returnId}/transition` | returns.transition AND target key (+ shipments.read) | `transitionReturn` |
| Unsupported detail/edit/delete/bulk/import/export | absent | absent | — | no route, control, proxy or request |

Headers the adapter sets: `Authorization` (server session only); `X-Correlation-Id` = valid UUID (list: a fresh
request trace; create/transition: the resolved Shipment root); `Idempotency-Key` = the browser's per-intent key
(1..128 scalars, forwarded exactly); `X-Tenant-Id`/`X-Legal-Entity-Id` (required for Returns) from the **signed
session claims only**, never from browser input. Antiforgery on every POST.

## 5. List (bounded DataTable profile, decided up front)

Columns: `rmaNumber`, `shipmentId` (opaque UUID, LTR-isolated, copyable), status (localized label; wire token
preserved in data), actions. `returnId` is available in QuickView and copy, not as a primary column. `ReturnSummary`
has no required properties: an absent field shows "not provided"; a row without `returnId` gets no actions.
The UI checks the envelope (`items`, `total`, `contractVersion: v1`) and shows a malformed response as an error, never as an empty list.

- API parameters: only `shipmentId` (single UUID text input; not a lookup) and `status` (single value; `All` omits the
  parameter). No multi-status, fan-out, paging, search or sort parameters are sent.
- `serverSide: false`; client search, sort and paging apply only to the returned set; the `total` shown is the API total.
- Shared personalization (Save View, column visibility, Reset) is consumed if present in the target baseline;
  no localStorage alternative is written.
- Skeleton, empty and error states are distinct (VIEW-001 §3.1). A valid `items: []` is the only empty state.
- **OUT, recorded as scope-change rows in ACCEPTANCE.md:** checkbox column, BulkActionBar, edit/delete, import,
  export, server paging, multi-select status filter. The generic `verify_datatable_page.py --reference slim` result is
  kept as a record, not as acceptance (same treatment as Shipment UI183-A14-GENERIC-GATE).
- QuickView (Slim): renders **only the selected row's summary** plus copyable `returnId`; it makes no by-ID request.

## 6. Create offcanvas (Slim)

**Field count, create form only (AGENTS.md §6 rule):** 6 schema field types — `shipmentId`, `reasonCode`,
`lines[].shipmentLineNumber`, `lines[].quantity`, `lines[].uomId`, `evidenceReferenceIds`. That is ≤ 8, so
**GoldenReferenceSlim**. The lines container is not counted separately (the Loads precedent). Correlation, key,
tenant/LE, IDs, audit and status are not user fields. Displayed editable controls: 2 fixed (`shipmentId`,
`reasonCode`) + per selected line 1 (`quantity`) + a repeatable evidence list. With 2 lines and 2 evidence entries
that is 6 inputs.

Flow:
1. User enters a Shipment UUID and presses Resolve. The adapter returns shipment number, status and lines.
   A newer input cancels an older pending resolve; **a late response for a previous UUID never populates the panel**.
2. Resolve failure: 404 shows the localized safe-not-found text with a support reference, and no line table stays on
   screen. 403 shows access denial for the create action. 5xx shows a dependency error.
3. Eligibility (annex D186-01: Delivered/Closed only) is shown as an informational note. Submit is disabled for
   other statuses as a **display decision only**; the backend 422 stays authoritative and is proven by direct POST in acceptance.
4. The line table has one row per source line: select, `lineNumber` (read-only), source quantity (read-only, labelled
   "shipped quantity", **not** "remaining": no API exposes remaining entitlement), return quantity (text input,
   `inputmode=decimal`, exact lexical string sent, no locale conversion, rounding or float), `uomId` (read-only from the source line).
5. `reasonCode`: free text, sent as typed. Schema presence ≠ nonempty: no HTML `required`, trim or maxlength is added.
6. `evidenceReferenceIds`: optional repeatable text. The field is omitted when the list is empty (the annex treats omission as `[]`); order, duplicates and empty strings are preserved.
7. Submit: one pending request; per-intent `Idempotency-Key`; network/500/503 retry reuses the same key and body.
   Editing the payload starts a new intent. 409 `IDEMPOTENCY_KEY_REUSED` stops retry. 201 (including
   `idempotentReplay: true`) shows success, closes the panel and reloads the list. The replay snapshot is never shown as current state.

## 7. Transition action (row → premium modal form)

The row action set is derived from the row status and the frozen arrows only. Each action needs
`supplychain.returns.transition` **and** the target key. The server decides; hidden controls are not the only guard.

| Current | Target(s) | Target key | Extra modal fields |
|---|---|---|---|
| Requested | Authorized, Rejected | `.authorize` | — |
| Authorized | InTransit / Cancelled | `.transit` / `.cancel` | — |
| InTransit | Received | `.receive` | optional `inventoryTransactionReferenceId` (opaque, unverified) |
| Received | Dispositioned | `.disposition` | `dispositionCode` (length ≥ 1, no trim, whitespace allowed), optional `inventoryTransactionReferenceId` |
| Dispositioned | Closed | `.close` | — |
| Rejected, Cancelled, Closed | none | — | no action rendered |

Every modal has `occurredAt` (date-time with explicit offset, default "now" in the browser offset, editable; sent as
RFC 3339 text, the instant preserved). Optional fields not listed for a target are omitted, which is valid. Modals use the shared
Premium SweetAlert2 wrapper (MOD-0013); no native dialog, no inline handler.
**Received is labelled "Received (manual assertion)"** and its confirmation text says it is not warehouse-verified
receipt or stock posting (annex D186-03/04).

Outcomes: 200 → success, reload. 422 `INVALID_RETURN_TRANSITION` (stale row) → localized conflict + support
reference + reload (Shipment A08 lesson). 404 `RETURN_NOT_FOUND` → safe-not-found, action closed, reload. 409 root/key → §8.

## 8. Error, replay and concurrency presentation (from the annex; no client tightening)

| Response | UI behaviour |
|---|---|
| 400 / 415 `INVALID_REQUEST` | Localized form summary; field mapping only where a safe field is identified; no raw details |
| 401 | HTML: standard login; JSON adapters: 401 JSON surface (Shipment A03 pattern); no redirect loop |
| 403 | Page: `_AccessDenied` inside the shell (UAS-001). Action: close/disable the action and show localized denial |
| 404 `RETURN_NOT_FOUND` / shipment 404 | **One identical safe-not-found text + support reference** for unknown, foreign-LE and soft-deleted targets; no existence hint; no stale surface re-exposed by a late response |
| 409 `CORRELATION_ROOT_MISMATCH` | "The request no longer matches the shipment record" + support reference; no automatic new key |
| 409 `IDEMPOTENCY_KEY_REUSED` | Stop retry; require a new user intent |
| 409 `RETURN_SOURCE_CHANGED` | Source shipment changed since the first return; show the conflict; no automatic adjustment |
| 422 (lifecycle, quantity, UoM, eligibility, duplicate line, disposition) | Localized message per code; exact code set bound at Phase 1.5 from the accepted backend; unknown code → generic localized text + support reference |
| 502 `RETURN_SHIPMENT_ROOT_INVALID` | Shipment reference data invalid; no retry loop |
| 503 root unavailable / dependency / storage | "Temporarily unavailable" + same-key retry offer; an **unknown commit is never shown as rolled back** |

`X-Correlation-Id` of each response is shown as a copyable **support reference**. It is never labelled or reused as the lifecycle root.

## 9. Language, layout, accessibility

Seven tenant languages (en, tr, fr, es, zh, ar, ru) in `ReturnsIndex.{lang}.resx`. Status and error labels are
localized and wire tokens stay unchanged. No English placeholder in non-English files, no hardcoded fallback. Arabic RTL.
UUIDs and decimals are LTR-isolated. 390 / 768 / 1024 / 1440 px with no horizontal overflow. The repeatable line and evidence
editors keep unique ids and accessible names after add/remove (the Shipment PRES-183-01 lesson). Keyboard focus and Escape
work in the offcanvas and modal.

## 10. Explicitly out of scope

Warehouse-verified receiving, Inventory posting/validation, credit/finance, remaining-entitlement display,
reason/disposition catalogues (MOD-0048 reference data would be a separate decision), Return detail page, links from
the Shipment detail page (Shipment UI is protected), gateway/nav/permission edits by the UI writer, E5/G5, rollout.

## 11. Open gates carried to CT (not decided here)

G-TARGET pack preimage and integration baseline · G-SHIPREAD shipments.read dependency · DN-01 (Shipment A10)
same-key retry policy is inherited, not re-decided · DN-02 bounded DataTable profile acceptance · nav module/page
codes reconciled with the registry by the integration owner (no literal invented here) · Shipment producer root
uptake in the final target · durable PNG capability (PRES-183-04).
