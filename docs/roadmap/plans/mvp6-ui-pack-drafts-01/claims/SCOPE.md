# MOD-0187 Claims — tenant UI scope DRAFT (Q33)

2026-09-26 · Documents only. **This draft authorizes nothing:** no pack edit, UI DEV, gateway, shared registration,
pack promotion or dispatch. Measured on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.

## 1. Identity (DCP-002)

| Question | Answer |
|---|---|
| New module, FU/child, or revision? | **UI revision inside the existing MOD-0187 pack.** Pack §11 already states "a future claims UI requires an approved pack revision". The UI covers the same Claim object and only the three published operations the accepted backend serves; no new capability, so no FU/new ID under DCP-002 step 4. A `MOD-0187-FUxx` would be needed only for a capability outside these operations (for example finance settlement or evidence upload); none is proposed or minted. |
| Canonical ID/name | `MOD-0187` / `Claims Management` (unchanged). |
| Command to run before the pack revision | `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"` |
| Result observed in this lane | `OK  MOD-0187: proven against Blueprint/registry.` exit 0, 2026-09-26 ~01:14 Istanbul (read-only run). Must be rerun against the final target. |
| Pack preimage | **Open gate G-TARGET.** The common-checkout pack reads `status: draft` (SHA-256 `a342054c…1c`). The approved isolated `ready-for-dev` pack belongs to the accepted WP (`mvp6-mod0187-ct-accept-01/SOP-22.md`). CT names the preimage. |

Proposed frontmatter change (unapplied): `shell: tenant`, `golden_reference: slim`, `form_field_count: 6`.
Backend `ready-for-dev` does not transfer to the UI.

## 2. Published operations bound (nothing else)

Contract SHIPMENT-BUNDLE **3.0.0 / wire v1**, SHA-256 `5dfe7c1b…d9b21c` (matches the accepted WP). Claims annex
`claims-semantics-v3.0.0.md` SHA-256 `16e65c26…eb63` (matches).

| Operation | Use in UI |
|---|---|
| `queryClaims` GET `/claims?shipmentId=&status=` | List, filter, reload |
| `createClaim` POST `/claims` | Create offcanvas |
| `transitionClaim` POST `/claims/{claimId}/transition` | Row lifecycle action |
| `getShipment` GET `/shipments/{shipmentId}` (consumed) | Resolve shipment number, status and `carrierId` for create; root resolved server-side for create/transition |

**Not in the UI:** Claim by-ID GET, edit, delete, bulk, import/export, server paging/search/sort, carrier lookup
(`queryCarriers` is not needed, see §6), evidence upload or verification, currency catalogue, finance posting.

## 3. Root: transition UI is feasible

Claims annex D187-05: "Create uses original Shipment lifecycle root as its root and must receive matching
X-Correlation-Id… Transitions/replays must match persisted root; wrong root→409 `CLAIM_CORRELATION_MISMATCH`."
`ClaimSummary` carries `shipmentId`, and published `getShipment` exposes `lifecycleCorrelationId`. So the MVC
adapter resolves the root server-side from a published seam; it is never shown to or accepted from the browser.
Missing/null root → 503 `CLAIM_REFERENCE_INCOMPLETE`; malformed → 502 `CLAIM_REFERENCE_INVALID`. The UI shows
these plainly. The browser-supplied `shipmentId` on a transition is only a lookup hint; the backend root check is
authoritative and fails closed. **G-SHIPREAD** applies here exactly as for Returns (actors need `supplychain.shipments.read`).

## 4. Screens, routes and permissions

Tenant shell, `Views/SupplyChain/Claims/`, same-origin MVC → Gateway 5000 only.

| Surface | MVC route (proposed) | Gateway downstream | UI display gate | Backend |
|---|---|---|---|---|
| Page | GET `/SupplyChain/Claims` | — | `supplychain.claims.read` | — |
| List adapter | GET `/SupplyChain/Claims/api?shipmentId=&status=` | GET `/api/shipment-bundle/claims` | read | `queryClaims` |
| Shipment resolve adapter | GET `/SupplyChain/Claims/api/shipments/{shipmentId:guid}` | GET `/api/shipment-bundle/shipments/{shipmentId}` | claims.create + shipments.read | `getShipment` (projection: number, status, carrierId; no root to browser) |
| Create adapter | POST `/SupplyChain/Claims/api` | root via getShipment, then POST `/api/shipment-bundle/claims` | claims.create (+ shipments.read) | `createClaim` |
| Transition adapter | POST `/SupplyChain/Claims/api/{claimId:guid}/transition` | root via getShipment, then POST `/api/shipment-bundle/claims/{claimId}/transition` | exact target key (+ shipments.read) | `transitionClaim` |
| Unsupported detail/edit/delete/bulk/import/export | absent | absent | — | none |

Headers: `Authorization` (server only); `X-Correlation-Id` (list: fresh trace; mutations: Shipment root);
`Idempotency-Key` per intent, forwarded exactly. The optional `X-Tenant-Id`/`X-Legal-Entity-Id` are **not sent**
(the signed JWT scope is the authority; sending them only adds mismatch paths). Scope query keys are never sent. Antiforgery on every POST.

## 5. List (bounded DataTable profile, decided up front)

Columns: `claimNumber`, `shipmentId` (UUID, LTR, copyable), status (localized), `claimedAmount` (**exact wire
text**, LTR, no locale formatting, grouping or rounding), `currency` (code as sent; no symbol mapping), actions.
`claimId` appears in QuickView/copy. `ClaimSummary` has no required properties: an absent field shows "not provided",
and a row without `claimId` gets no actions. **`approvedAmount` is not in `ClaimSummary`,** so the list cannot show it after reload. The
UI shows it only in the transition success message. This is a recorded contract gap, not something to derive.

API parameters: only `shipmentId` and single `status`. `serverSide:false`, and client search/sort/paging runs over the returned set.
Personalization, the distinct skeleton/empty/error states and the envelope check work as in Returns. OUT items (scope-change rows in
ACCEPTANCE.md): checkbox/bulk, edit/delete, import/export, server paging/search/sort, multi-select status, generic
verifier as acceptance. QuickView: row summary plus copyable `claimId`; no by-ID request.

## 6. Create offcanvas (Slim)

**Field count:** 6 — `shipmentId`, `carrierId`, `reasonCode`, `claimedAmount`, `currency`,
`evidenceReferenceIds` → ≤ 8 → **GoldenReferenceSlim**.

1. Shipment UUID + Resolve, with the stale-response guard (a late response for a previous UUID never populates). 404 → the
   identical safe-not-found text + support reference, and no shipment data stays in the DOM.
2. Eligibility (annex D187-01: Dispatched, InTransit, Delivered, Exception, Closed): informational note, submit
   disabled for Draft/Planned/Cancelled (display decision only; backend 422 `CLAIM_SHIPMENT_INELIGIBLE` stays authoritative).
3. `carrierId`: a checkbox "Link the shipment's carrier", enabled only when the resolved Shipment has a non-null
   `carrierId`. Checked sends that UUID; unchecked omits the field. The annex allows only omission or the Shipment's own
   carrier, so no carrier list or lookup is needed. Mismatch rejection (422 `CLAIM_CARRIER_MISMATCH`) is still proven by direct POST.
4. `reasonCode`: free text, sent as typed (presence ≠ nonempty; no required/trim/maxlength).
5. `claimedAmount`: text input, `inputmode=decimal`, **sent as a JSON string exactly as typed**. No conversion,
   trimming, locale decimal comma handling, auto-formatting or float. The hint explains the ASCII form (`1234.50`).
   Positivity and lexical checks stay server-side (400 `INVALID_REQUEST` / 422 `CLAIM_AMOUNT_INVALID`). The UI does
   not pre-block, because lexical edge cases (`-0`, `000.0`) must behave exactly as the annex says.
6. `currency`: text, sent as typed. No ISO list, no automatic uppercasing (lowercase is a server 400).
7. `evidenceReferenceIds`: optional repeatable text; order, duplicates and empty strings preserved; no upload.
8. Submit and retry as in Returns: one pending request, per-intent key, retry resends the **identical body text**
   (the fingerprint is lexical: `250` ≠ `250.00`), 409 `IDEMPOTENCY_KEY_REUSED` stops, and 201 replay is shown as completed, then the list reloads.

## 7. Transition action (row → premium modal form)

The action is shown only when the row status allows the arrow and the actor holds the exact target key. The backend first
requires any one of create/investigate/decide/settle, then the exact target key.

| Current | Target | Key | Modal fields (besides `occurredAt`) |
|---|---|---|---|
| Open | Investigating | `.investigate` | optional `resolutionCode`, `note` |
| Open | Withdrawn | `.investigate` | optional `resolutionCode`, `note` |
| Investigating | Approved | `.decide` | **`approvedAmount` required**, text, 0 ≤ x ≤ claimed (server-checked); optional `resolutionCode`, `note` |
| Investigating | Rejected | `.decide` | optional `resolutionCode`, `note` |
| Approved | Settled | `.settle` | optional `resolutionCode`, `note` |
| Rejected / Settled | Closed | `.decide` | optional `resolutionCode`, `note` |
| Withdrawn, Closed | none | — | no action |

`approvedAmount` is never sent except for Approved; a non-null value elsewhere is a server 422, so it is omitted.
`occurredAt` is editable, defaults to now with its offset, and its exact text is kept for retries. Empty
`resolutionCode`/`note` are valid. **Settled is labelled "Settled (operational status, no payment posted)"** and the
confirmation says no AP/AR/payment record is created (annex D187-02).

## 8. Error presentation (annex D187-05 message table; UI shows localized equivalents, never raw text)

| Code | HTTP | UI behaviour |
|---|---|---|
| `INVALID_REQUEST` | 400 | Form summary; safe field mapping only |
| `UNAUTHENTICATED` | 401 | Standard session surface; JSON adapters return 401 JSON |
| `FORBIDDEN` | 403 | Page: `_AccessDenied` in shell. Action: close/disable + localized denial |
| `CLAIM_NOT_FOUND` | 404 | **Identical safe-not-found text + support reference** for unknown, foreign-scope and soft-deleted claim/shipment/carrier; action closed; reload |
| `UNSUPPORTED_MEDIA_TYPE` | 415 | Generic request error |
| `CLAIM_SHIPMENT_INELIGIBLE` / `CLAIM_CARRIER_MISMATCH` / `CLAIM_AMOUNT_INVALID` / `CLAIM_APPROVAL_AMOUNT_INVALID` / `CLAIM_APPROVED_AMOUNT_NOT_ALLOWED` | 422 | Specific localized message; inputs kept |
| `INVALID_CLAIM_TRANSITION` | 422 | Stale-state conflict + support ref + reload (A08 lesson) |
| `CLAIM_CORRELATION_MISMATCH` | 409 | "Request no longer matches the shipment record"; no automatic new key |
| `IDEMPOTENCY_KEY_REUSED` | 409 | Stop retry; new user intent required |
| `CLAIM_REFERENCE_INVALID` | 502 | Reference data invalid; no retry loop |
| `CLAIM_REFERENCE_INCOMPLETE` / `CLAIM_REFERENCE_UNAVAILABLE` / `CLAIM_STORAGE_UNAVAILABLE` | 503 | Temporarily unavailable; same-key retry offer; **unknown commit is never shown as rolled back** |
| `INTERNAL_ERROR` | 500 | Sanitized error; explicit same-intent retry |

Support reference = response `X-Correlation-Id` (equal to `error.correlationId`), copyable. It is never labelled as the lifecycle root.

## 9. Language, layout, accessibility

Same as Returns: 7 languages in `ClaimsIndex.{lang}.resx`, Arabic RTL, amounts/UUIDs/currency codes LTR-isolated,
390/768/1024/1440, keyboard and focus rules, unique ids in the repeatable evidence editor.

## 10. Explicitly out of scope

Finance/AP/AR/payment, currency FX, evidence upload/verification, carrier picker, claim detail page, approved
amount history view, workflow/dual control, links from Shipment/Carrier UIs, gateway/nav/permission edits by the UI writer, E5/G5, rollout.

## 11. Open gates carried to CT

G-TARGET · G-SHIPREAD · DN-01 retry policy (inherited) · DN-02 bounded DataTable profile · nav codes via the
integration owner · Shipment root uptake in the target · accepted isolated Claims WP (47 paths + Program.cs)
integrated into the target (the common checkout lacks 44 Claims paths per the CT record) · PNG capability · the
`approvedAmount` list gap (a contract question if the owner wants it displayed later).
