# Carrier tenant UI flow

## 1. Page entry and list

1. `GET /SupplyChain/Carriers` requires an authenticated tenant context and `supplychain.carriers.read`.
2. No authenticated session uses the established full-page 401 surface. An authenticated user without the read grant sees only `_AccessDenied`; no page title, cards, table, filters, create button, row actions, toast, or redirect are rendered.
3. The server-side MVC adapter calls Gateway `5000` only, with bearer token, signed tenant/LE context, and a valid `X-Correlation-Id`.
4. The table loads `GET /api/shipment-bundle/carriers` with no query or one exact `status=Active|Suspended|Retired`. Search, sort, and paging are client-side and never become unknown backend query parameters.
5. `items:[]`, `total:0` is the normal localized empty state. The create action is visible only with `.create`.

Columns are carrier code, display name, localized status label, localized supported-mode labels, and actions. Wire tokens remain unchanged. `externalReference` is absent from `CarrierSummary` and is not displayed or inferred. Quick view uses only the selected row summary; it performs no by-id request.

## 2. Create

The Slim create-only offcanvas has:

| Field | UI rule |
|---|---|
| `carrierCode` | required nonempty string; preserve exact value; no trim/case conversion/max/pattern |
| `displayName` | required nonempty string; preserve exact value; no trim/max |
| `supportedModes` | required multi-select, at least one; Road/Air/Sea/Rail/Parcel wire values; order and duplicates are not silently normalized |
| `externalReference` | optional nullable opaque string; missing/null/empty remain distinguishable where the control state allows it |

Create begins in backend status `Active`; status is not a create field. The adapter sends the exact JSON body plus a stable per-intent `Idempotency-Key`. Double-click is suppressed while in flight. Transport loss/500/503 retry uses the same key and exact payload. A definitive result or a user-edited payload begins a new intent and therefore a new key. A 201 replay (`idempotentReplay:true`) is shown as an already-completed success, followed by a fresh list reload.

## 3. Status action

The row action is visible only with `supplychain.carriers.status.change`. The modal sends exact `targetStatus` and required string `reasonCode`; empty reason is valid. UI choices follow the frozen lifecycle:

- Active → Suspended or Retired
- Suspended → Active or Retired
- Retired → no fresh transition

The backend still decides concurrency and validity. A 422 `INVALID_CARRIER_TRANSITION` reloads the list and explains that the current state no longer permits the request. No ETag/version field is invented. Retry and replay follow the same stable-key rules as create; status success reloads the list.

## 4. Errors

- 400/415 `INVALID_REQUEST`: field-level mapping only where the response identifies a safe field; otherwise localized form summary. Do not reveal raw details.
- 401: end the action and enter the standard unauthenticated surface; no silent redirect loop.
- 403: render the read-denied surface for page/list denial; for a stale action grant, close/disable that action and show localized access denial without rendering unauthorized alternatives.
- 404 `CARRIER_NOT_FOUND`: generic not-found/scope-safe message, then reload. Tenant/LE mismatch is never exposed.
- 409 `CARRIER_CODE_CONFLICT`: preserve create values and let the user revise the code; revised payload uses a new key.
- 409 `IDEMPOTENCY_KEY_REUSED`: stop automatic retry; tell the user that the request identity belongs to different content and require a new user intent.
- 422 `INVALID_CARRIER_TRANSITION`: show lifecycle conflict and refresh.
- 500 `INTERNAL_ERROR`: sanitized localized error; retain exact intent for an explicit safe retry where appropriate.
- 503 `PERSISTENCE_UNAVAILABLE`: offer same-key retry with unchanged body.

All application responses must expose `X-Correlation-Id`; the UI records/displays a safe support reference without treating it as the business root or original receipt correlation.
