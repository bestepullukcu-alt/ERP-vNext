# R-2 — the known traps, each produced on purpose (browser on Web 5001, zone Europe/Istanbul UTC+03:00)

Values are the JSON the in-page measurement returned, abridged only by removing unrelated fields. The `fetch` wrappers
named below are test instruments injected in the browser for the run, not product code.

## T1 — a 5xx without a contract envelope must not reach the user as a validation message

SupplyChain stopped (this lane's own process 22299). The gateway answered `502` with no body:
`curl …/api/shipment-bundle/returns` → `502`.

| surface | adapter answer | what the user read |
|---|---|---|
| Transition (Authorize → Confirm) | `503 {"error":{"code":"DEPENDENCY_UNAVAILABLE",…}}` | "The shipment reference data is temporarily unavailable. Try the same request again. Support reference: 91530745-…" |
| Create → Look Up | `503 DEPENDENCY_UNAVAILABLE` | the same text; 0 lines left in the form; Save disabled |
| List load | `503` in 13 ms | "Returns are temporarily unavailable. … Support reference: 6677a763-…"; table hidden, no empty state |

Sabotage (unit): mapping an un-enveloped 5xx to `400 INVALID_REQUEST` in the adapter →
`SupplyChainReturnsControllerTests.List_failure_keeps_the_published_code_and_never_relays_an_unpublished_one` fails (40/1/41);
restored 41/41 (`suite-results.txt`).

## T2 — one Idempotency-Key per opened form, and Q393 (an unchanged retry must not get 400)

Lost response simulated by a wrapper that let the create POST commit and then threw.

| run | requests (key / status) | page | Returns in DB for that reason code |
|---|---|---|---|
| unchanged retry | `d9ce9fd8…` 201 (response dropped) → `d9ce9fd8…` **201, replay**; the browser trace differed (`311187eb…` vs `685b60ab…`), the body was identical | "This return was already created by the same request and is shown as completed." | **1** (`T2-LOST-RESPONSE`) |
| edited retry | `ba745f46…` 201 (dropped) → same key, reason edited → **409** | "This request was already used with different values. Start a new action." A third click sent nothing | **1** (`T2-EDITED-RETRY`; the edited text never stored) |
| **SABOTAGE**: the draft's per-payload `index.js` served instead (`createIntent.bodyText !== bodyText` present in the served file) | `5a8cbd69…` 201 (dropped) → **new key** `ac7961d3…` **201** | "Return created." | **2** (`SAB-EDITED-RETRY`, `SAB-EDITED-RETRY-CHANGED`): a duplicate Return from one intent |
| restored | served file equals the repository file (`createIntent.bodyText !== bodyText` count 0) | — | — |

Why the unchanged retry is a replay here and a 400 on Shipments (Q393): the Returns adapter forwards the **Shipment's
lifecycle root** as `X-Correlation-Id` on every mutation (pack §32.4), resolved server-side, so a retry carries the same
correlation whatever trace the browser sends. Web log: `Returns mutation forwarded. Trace 806fdcd2… Root 66b8e14d…`.

## T3 — local wall clock in the field, UTC on the wire, a time that does not exist is rejected

| input (datetime-local) | result |
|---|---|
| prefill on open | field `2026-10-04T19:10`, zone hint `Europe/Istanbul (UTC+03:00)`, browser UTC `16:10:23Z` |
| that prefill, submitted | wire/stored `OccurredAt 2026-10-04T16:10:00Z` (`golden-db.txt`, audit row Requested>Authorized) |
| `2016-03-27T03:30` (Istanbul's skipped spring-forward hour) | "Enter a valid date and time that exists in your time zone."; field `is-invalid`; **0 requests**; no confirm dialog |
| empty | the same message; **0 requests** |
| `2026-10-04T18:00` typed | body `{"targetStatus":"Authorized","occurredAt":"2026-10-04T15:00:00.000Z"}` → 200; stored `2026-10-04T15:00:00.000Z` |
| invalid, then valid (after the alert fix) | alert visible after the invalid try; **not** visible when the confirm dialog opens for the valid one |

## T4 — skeleton while loading, never an empty table under "Loading…"

SupplyChain frozen with `SIGSTOP` (own process):

| t (s since navigation) | skeleton | table | visible "Loading" text | empty-state text | error text |
|---|---|---|---|---|---|
| 1.7 | **visible** | hidden | no | no | — |
| 15.3 | gone | hidden | no | no | "Returns are temporarily unavailable. … Support reference: 42a21d03-…" (adapter call 15 055 ms, 503: Q394's 15 s read deadline) |

Screenshot `shots/T4-skeleton-while-service-frozen.jpg`.

## T5 — seven languages; status labels localized, wire value English; a Dictionary bridge

Culture `ar`: `dir=rtl`, `lang=ar`, title "المرتجعات", badge "مغلق", 8 filter options with English values
(`Requested=مطلوب`, …, `Closed=مغلق`, `Cancelled=ملغى`), **0** Latin status words visible, horizontal scroll 1024/1024.
Applying the filter "مغلق" requested `/SupplyChain/Returns/api?status=Closed`. Bridge keys arrived exactly as the resx
names them: `StatusClosed` present, `statusClosed` absent. Resx parity 106 keys × 7 is pinned by `ReturnFormContractTests`.

## UAS-001 — a user without `supplychain.returns.read`

`r2t-nokey` (tenant `29fb1c14…`): inside the shell, only "You don't have access to Returns"; 0 tables, no skeleton, no
filter, no Add button, 0 adapter calls, no redirect.
