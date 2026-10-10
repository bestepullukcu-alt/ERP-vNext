# R-4b — recipe traps, each produced on purpose (Web 5001, tenant cedaf26d…, browser zone Europe/Istanbul)

Values are what the in-page measurement returned, abridged only by removing unrelated fields. The `fetch` wrappers are
test instruments injected in the browser for the run, not product code. The three sabotage scripts are in
`sabotage-scripts.diff`; each was served from the scratch copy and then the fixed script was restored (served file equal to
the repository, `SABOTAGE` count 0).

## T0 — the first create: `Loads:ReferenceBaseUrl` absent (found, not planned)

POST `/SupplyChain/Loads/api` → **503 `DEPENDENCY_UNAVAILABLE`** with key `8441b8fa…`, correlation `f44306eb…`. No
configuration file in the tree sets `Loads:ReferenceBaseUrl` (Returns and Claims have theirs), so the Loads create cannot
reach its carrier/shipment references. SupplyChain was restarted with `Loads__ReferenceBaseUrl=http://127.0.0.1:5061/` (env
only; no config file edited). The **same open form** was retried unchanged: same key, same correlation → **201**
`LOAD-659A9B09…`, root `f44306eb…` (`golden-db.txt`). The 503 log of the first run is kept as
`supplychain-run1-no-loads-reference-url.log` in the scratch folder.

## T1 — one Idempotency-Key AND one correlation per opened form (recipes 3.1 / Q403 and 3.2)

The Loads receipt compares the root: a replay under a different `X-Correlation-Id` answers **409
`CORRELATION_ROOT_MISMATCH`** (`LoadRepository.cs:16`). So unlike Carriers, 3.2 bites here and the page keeps the key and the
correlation together.

| run | requests | page | loads stored |
|---|---|---|---|
| (a) lost response, unchanged retry | same key, same correlation: 201 (dropped) → **201 replay** | success | **1** |
| (b) lost response, the user edits a stop (`D-B`) | same key: 201 (dropped) → **409 `IDEMPOTENCY_KEY_REUSED`** | "This request was already received with different values. To make a different request, close this form and open a new one." A third click sent nothing | **1** (the first payload) |
| **S1 SABOTAGE**: a fresh correlation per request | same key, new correlation: 201 (dropped) → **409 `CORRELATION_ROOT_MISMATCH`** | "…no longer matches…", although the load **was committed** | 1, but the user is told it failed |
| **S2 SABOTAGE**: key re-minted when the payload changes | `6bf91f81…` 201 (dropped, correlation `9aa74fe7…`, stop `D-S2`) → new key **`98b47f51…`** → **409 `SHIPMENT_ALREADY_ASSIGNED`** | "A shipment already belongs to another load." | 1 for shipment `6f5ba6ee…` (`LOAD-C98B2175…`): the assignment uniqueness stopped the duplicate, the user got a misleading message |

S2 shows the per-payload key returns the old behaviour (a second write attempt from one intent). On Loads the backend's
shipment-assignment uniqueness hides it as a wrong message instead of a duplicate; a load without that constraint would
store two.

## T2 — a 5xx without a contract envelope (recipe 2.1)

SupplyChain stopped (own pid 33926, later 34365); the gateway answered 502 with an empty body.

| surface | adapter answer | user sees |
|---|---|---|
| list | 503 | "Loads could not be loaded. Try again later. Support reference: 172e76ef-…"; table hidden, **no Add button** (it lives on the list surface), no toast |
| create (form opened while SupplyChain was up, then stopped) | 503 `PERSISTENCE_UNAVAILABLE`, key `ec96138f…`, correlation `de89b1dc…` | form alert "The request outcome is unavailable. Support reference: de89b1dc-…": the reference **is the intent's correlation**; no field marked invalid, no validation text |

Database after: no receipt with key `ec96138f…`, no load with root `de89b1dc…`; loads 6 (`db-final.txt`).

Sabotage (unit, separate scratch copy): the mapping disabled (`if (false)`) turns the 3
`UpstreamServerFailureWithoutContractBody_BecomesPersistenceUnavailable` cases red (`suite-results.txt`).

## T3 — JSON 401 (recipe 2.4)

Unauthenticated `GET /SupplyChain/Loads/api` with `X-Correlation-Id: 55555555-…` → **401**
`{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"55555555-…"},…}`.
The page `/SupplyChain/Loads` → **302** to login. The marker is pinned by
`SupplyChainLoadsControllerTests` (every adapter action carries `[JsonAdapterEndpoint]`).

## T4 — loading and failed-load states (recipes 4.1, 3.6, 3.11)

SupplyChain frozen with `SIGSTOP` (own pid 34527), page reloaded:

| build | t≈1.7 s | t≈17.7 s (after the adapter's 15 s timeout) |
|---|---|---|
| **fixed** | **skeleton visible**, table hidden, no "Loading" text | alert "Loads could not be loaded. Try again later. Support reference: 54546295-…"; skeleton gone; table hidden; no toast |
| **SABOTAGE** (no skeleton; write wording on failure) | no skeleton; table visible with "Loading…" | "The request outcome is unavailable. Support reference: 6326754f-…" on a **read** |

`SIGCONT` afterwards; health 200.

## T5 — local wall clock → UTC; a non-existent time is rejected (recipe 3.4)

Live: `21:59` local (Europe/Istanbul, UTC+3) stored as `2026-10-04T18:59:00Z` (`golden-db.txt`).

Istanbul has no DST, so the gap cannot be produced in the browser. `date-trap.json` runs the **production**
`parseLocalInput` (extracted from `index.js` text, not copied) under `TZ=Europe/Berlin`:

| input | fixed | naive `new Date(raw)` (the sabotage) |
|---|---|---|
| `2026-03-29T02:30` (spring gap) | **null → rejected** | `01:30Z`, a time the user never chose |
| `2026-02-30T10:00` | **null** | rolled to `2026-03-02` |
| `2026-10-25T02:30` (autumn overlap) | `00:30Z` (first occurrence) | same |
| `2026-10-20T10:00` | `08:00Z` | same |

The ambiguous autumn hour is resolved silently to its first occurrence; the recipe only asks for non-existent times to be
rejected. Recorded as a finding.

## T6 — seven languages, wire value English, Dictionary bridge (recipes 5.2 / 5.3)

Culture `ar`: `dir=rtl`, title "الحمولات", headers localized, status options `Draft=مسودة`, `Planned=مخططة`, `Tendered=مطروحة`,
`Accepted=مقبولة`, `Dispatched=مُرسلة`, `Completed=مكتملة`, `Cancelled=ملغاة`. Filtering requested
`/SupplyChain/Loads/api?status=Draft`; 6 rows, badges "مسودة", 0 Latin status words, scroll 1024/1024. The bridge carried 74
keys, none lower-first, none empty; `ListUnavailable` and `StatusDraft` present, `listUnavailable`/`statusDraft` absent; no
missing-key warning in the console.

## UAS-001

`r4b-nokey` (no Loads key), signed in through `/account/login?tenantId=…`: the page shows only "You don't have access to
Loads — Ask your administrator to grant you permission for this page." inside the shell; 0 tables, no skeleton, filter or
Add button, **0 adapter calls**, no redirect.
