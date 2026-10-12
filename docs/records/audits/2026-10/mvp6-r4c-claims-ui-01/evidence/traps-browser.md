# R-4c — recipe traps, each produced on purpose (Web 5001, tenant a83097e6…, browser zone Europe/Istanbul, UTC+3)

Values are what the in-page measurement returned, abridged only by removing unrelated fields. The `fetch` wrappers are
test instruments injected in the browser for the run, not product code: "response dropped" means the request reached the
server and committed, and the wrapper threw instead of handing the response to the page. The sabotage scripts are in
`sabotage-scripts.diff`. Each was served from the scratch copy, then the fixed script was restored (served file equal to the
repository, sabotage markers 0).

## T1 — one Idempotency-Key per opened form or panel (recipe 3.1 / Q403)

Claims' adapter resolves the Shipment root server-side and sends it as `X-Correlation-Id`, so only the key decides.

| run | requests (key / status) | page | stored (`traps-db.txt`) |
|---|---|---|---|
| create: lost response, then the user changes the amount 100.00 → 110.00 | `4a0113c3…` 201 (dropped) → **`4a0113c3…` 409** | "This request was already received with different values. To make a different request, close this form and open a new one." A third click sent nothing | shipment `37dcd93c…`: **1** claim (100.00) |
| **SABOTAGE S1**: the draft's `index.js` served (`createIntent.bodyText !== bodyText`) | `0bc2c5bd…` 201 (dropped) → **new key `f9372f03…` 201** | "Claim created." | shipment `3091b3ee…`: **2 claims from one opened form** (100.00 and 110.00) |
| transition: lost response on Investigate, then the user edits the note | `dba53e3b…` 200 (dropped) → **`dba53e3b…` 409**; a third click sent nothing | the Q403 text | claim `cc07b692…`: one Investigating row (v2) |
| **SABOTAGE S1, transition** (draft served) | `a3f977db…` 200 (dropped) → **new key `d0b8d23c…` 422 `INVALID_CLAIM_TRANSITION`** | "The claim has changed in the meantime and this status change is no longer possible." The user's own first click was the change | claim `6c6d0a8c…`: Investigating (v2) |

## T2 — the unchanged retry (recipe 3.2)

Lost response, then Save again unchanged: `8babe890…` 201 (dropped) → **`8babe890…` 201 replay**. The page showed "This claim
was already created by the same request and is shown as completed.", and shipment `50105142…` holds **1** claim. Each request
carried a different browser trace. The adapter sends the Shipment root, so Claims' root rule (`ClaimRepository.cs:70`) never
sees the trace. 3.2 is closed by design here, as for Returns.

## T3 — a 5xx without a contract envelope (recipe 2.1) and read wording (3.11)

SupplyChain stopped (own pid 39866). The gateway answered **502 with an empty body**.

| surface | adapter answer | user sees |
|---|---|---|
| list, fixed | **503** `{"error":{"code":"CLAIM_STORAGE_UNAVAILABLE",…,"correlationId":"66666666-…"},"contractVersion":"v1"}` | "Claims could not be loaded." + support reference `1f15f5d4-…`; table hidden; no toast |
| list, **SABOTAGE** (draft `index.js`) | same | "Claims are temporarily unavailable. **The outcome is not confirmed; try the same request again.**" (write wording on a read) |

Controller sabotage (unit, separate scratch copy): the draft controller makes the 3
`Upstream_server_failure_without_a_contract_body_becomes_storage_unavailable` cases red, plus
`A_published_server_code_passes_through_with_its_status`. The draft relayed the list's raw 502 and re-enveloped a mutation's
as 502 `INTERNAL_ERROR`, which the page maps to "reference data invalid / unexpected error" rather than "outcome unknown,
retry the same request".

## T4 — skeleton while loading (recipes 4.1, 3.6)

SupplyChain frozen with `SIGSTOP` (own pid 42804), page reloaded:

| build | t≈1.7 s | t≈17.7 s (after the adapter's 15 s deadline) |
|---|---|---|
| fixed (the draft's own D1 handling, kept) | **skeleton visible** (opacity 1), table hidden, no "Loading" text | "Claims could not be loaded."; skeleton gone; no toast |
| **SABOTAGE** (display override and fade stop removed) | **nothing visible**: the skeleton is `display:none` from the shared CSS, and no table or error | the error state |

`SIGCONT` afterwards; health 200.

## T5 — a 403 on the list is the denied card (recipe 3.6)

The page gate (`Perms.Has(read)`) means a real user without `read` never reaches the list call, so the list 403 was
**simulated with a test instrument** that answered the list GET with an enveloped 403.

| build | result |
|---|---|
| fixed | `claimsListDenied` visible ("You don't have access to Claims…"), `claimsListSurface` hidden and inert, no error state, no toast |
| **SABOTAGE** (draft) | error state "You are not authorized for this action." with the title and filter still drawn |

## T6 — JSON 401 (recipe 2.4)

Unauthenticated, with `X-Correlation-Id: 55555555-…`: `GET /SupplyChain/Claims/api` → **401** JSON;
`GET …/api/shipments/{id}` → 401 JSON; `POST …/api` → 401; page → **302** (`json401.txt`). The draft already carried
`[JsonAdapterEndpoint]` on all four adapters (R-4a's defect 3 is absent here). The code in the 401 body is the shared
challenge's `INVALID_REQUEST`, not the Claims annex's `UNAUTHENTICATED` (finding).

## T7 — `occurredAt` is the local wall clock with its offset (recipe 3.4 shape)

The transition field is plain text with an explicit offset (pack G-DATETIME), prefilled from the local clock.

| build | prefill | stored `OccurredAt` vs `ReceivedAt` (`date-trap.txt`) |
|---|---|---|
| fixed (golden flow, 4 transitions) | `2026-10-04T23:06:37+03:00` | `23:06:37+03:00` = 20:06:37Z vs received 20:06:40Z (3 s apart) |
| **SABOTAGE** (UTC wall clock next to the local offset, the Q374 shape) | `2026-10-04T20:12:46+03:00` | **17:12:46Z** vs received 20:12:48Z: **3 h early**, a regulated record |

No non-existent-time case applies: the offset is explicit, so every text names one instant.

## T8 — seven languages, wire value English, Dictionary bridge (recipes 5.2 / 5.3)

- **Culture `ar`:**
  - `dir=rtl`; title "المطالبات"; headers localized.
  - Status options `Open=مفتوحة`, `Investigating=قيد التحقيق`, `Approved=معتمدة`, `Rejected=مرفوضة`, `Settled=مُسوّاة`, `Closed=مغلقة`, `Withdrawn=مسحوبة`.
  - Filtering requested `/SupplyChain/Claims/api?status=Investigating`; 3 rows with "قيد التحقيق".
  - 0 Latin status words; UUIDs and amounts in LTR `bdi`; scroll 1024/1024.
- **Bridge:** 109 keys, none lower-first or empty, `ErrIdempotencyKeyReused` in Arabic Q403 wording, `errIdempotencyKeyReused` absent.
- **Empty table (finding):** it shows the shared `DtEmptyTable` text ("لا توجد بيانات متاحة في الجدول"), not the module's `EmptyState` ("لا توجد مطالبات تطابق عامل التصفية الحالي."). `DtDefaults.create` overwrites the module's `language.emptyTable`.

No live sabotage for the bridge. The draft's `index.l10n.js` re-capitalized camel-cased names, so the anonymous object
could not bite on these keys; the Dictionary is defence. It is pinned by `L10n_bridge_exposes_every_module_key`, which the
draft view turns red.

## UAS-001

`r4c-nokey` (no Claims key), signed in through `/account/login?tenantId=…`:
- the page shows only "You don't have access to Claims — Ask your administrator to grant you permission for this page." inside the shell;
- 0 tables, no skeleton, filter, Add button or Claims script;
- **0 adapter calls**, no redirect.
