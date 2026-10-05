# R-4a — recipe traps, each produced on purpose (Web 5001, tenant a84ebe0a…, browser zone Europe/Istanbul)

Values are the JSON the in-page measurement returned, abridged only by removing unrelated fields. The `fetch` wrappers are
test instruments injected in the browser for the run, not product code.

## T0 — the first create, on the rescued code (found, not planned)

POST `/SupplyChain/Carriers/api` → **201** (`R4A-CARRIER-1` stored Active), yet the page toasted "The request outcome is
unavailable. Retry without changing the form…" and the table stayed empty.

- Console: `TypeError: xhr.abort is not a function at __reload (dataTables.mjs:7463) … at submitCreate (index.js:373)`.
- Cause: `ajax: loadRows` with `loadRows` async; `ajax.reload()` calls `.abort()` on the returned Promise. This is the Returns
  draft's UI-PM-03.
- Fixed: `ajax: (data, callback) => { void loadRows(data, callback); }`.
- Re-measured: "Carrier created successfully.", and the list reloads in place.
- The sabotage run in T2 (rescued script served again) reproduced the same false "outcome unavailable" after a 201.

## T1 — a 5xx without a contract envelope

SupplyChain stopped (own pid 30180); gateway 502 with an empty body.

| surface | adapter answer | user sees |
|---|---|---|
| list | 503 in 35 ms | "Carriers could not be loaded. Try again later. Support reference: 82284db5-…"; table hidden; no toast |
| create | 503 `PERSISTENCE_UNAVAILABLE` | "The request outcome is unavailable. Support reference: 29f78e68-…", the write wording, and not the validation text |

Sabotage (unit): the rescued controller makes 3 `UpstreamServerFailureWithoutContractBody_…` cases red, plus the marker
test (`suite-results.txt`).

## T2 — one Idempotency-Key per opened form (recipe 3.1 / Q403), and the unchanged retry (3.2)

| run | requests (key / status) | page | carriers stored |
|---|---|---|---|
| lost response, then the user fixes a typo in `carrierCode` | `11c9a8e9…` 201 (dropped) → **`11c9a8e9…` 409** | "This request was already received with different values. To make a different request, close this form and open a new one." A third click sent nothing | **1** (`R4A-TYPO-CODE`; the corrected code never stored) |
| lost response, then an unchanged retry | `c6017743…` 201 (dropped) → `c6017743…` **201 replay** | "This carrier request was already completed successfully." | **1** (`R4A-UNCHANGED-RETRY`) |
| **SABOTAGE**: rescued `index.js` served (`existing?.signature === signature` present) | `7f924cd6…` 201 (dropped) → **new key `b233902b…` 201** | "The request outcome is unavailable…" (the T0 defect again, after a commit) | **2** (`R4A-SAB-TYPO`, `R4A-SAB-FIXED`): two carriers from one intent |
| restored | served file equals the repository (`signature === signature` count 0) | — | — |

The unchanged retry replayed although each request carried a different browser `X-Correlation-Id`.
`CarrierRepository.cs:48` compares only the fingerprint, so Q393's 400 cannot occur on Carriers.

## T3 — no `[JsonAdapterEndpoint]` on the adapter (recipe 2.4, found)

Before: unauthenticated `GET /SupplyChain/Carriers/api` → **302** to `/account/login`.

After adding the marker to the three `api` actions: **401**,
`{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"55555555-…"},"contractVersion":"v1"}`.
The page `/SupplyChain/Carriers` still → 302.

Pinned by `SupplyChainCarriersControllerTests.EveryAdapterActionCarriesTheJsonAdapterMarker`; the rescued controller turns it red.

## T4 — loading and failed-load states (recipes 4.1, 3.6, 3.11)

SupplyChain frozen with `SIGSTOP`:

| build | t≈2 s | at the 15 s deadline (503, 15 057 ms) |
|---|---|---|
| **rescued** (the sabotage evidence) | no skeleton; table visible with "Loading…" in its empty cell | table still visible and empty; toast "The request outcome is unavailable. Support reference: c383af3d-…" |
| **fixed** | **skeleton visible**, table hidden, no "Loading" text | alert "Carriers could not be loaded. Try again later. Support reference: 6dd1b5f8-…"; table hidden; **no toast** |

## T5 — seven languages, wire value English, Dictionary bridge

Culture `ar`: `dir=rtl`, badge "متقاعد", options `Active=نشط`, `Suspended=معلّق`, `Retired=متقاعد`. Filtering requested
`/SupplyChain/Carriers/api?status=Retired`. 0 Latin status words visible; scroll 1024/1024. Bridge keys arrived exact
(`StatusActive` present, `statusActive` absent).

## UAS-001

`r4a-nokey`: only "You don't have access to Carriers" inside the shell; 0 tables, no skeleton, filter or Add button, 0 adapter
calls, no redirect.
