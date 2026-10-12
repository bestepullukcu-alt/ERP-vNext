# Q371 — Shipment UI: server failures, the Details skeleton, idempotent create, localized statuses

- Lane: Q371, frontend-ui-ux, single-writer on `frontend/Diten.Web/**`. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 19:51:02 UTC 2026` · `feature/mvp6-logistics` · HEAD `c1f2dffe8` (contains `235bfcbc3` and
  `18c5ab23a`) · dirty 3 · staged 0.
- Step 0: `AGENTS.md`, `git-safety.md`, `code-style.md`, `frontend-ui-ux.md` (read in full), the pack (§11, §12) and the
  Q362 REPORT are unchanged since this session's reads (sha256/16 `ce8c12ad…`, `4181c697…`, `6032fbb0…`, `90247ddc…`,
  `2285fe0d…`, `9f5996ca…`).
- **No `services/**` file touched, AdminModules untouched, contract unchanged. Nothing staged or committed. No
  token or secret printed.** Agent verdict ≠ CT ACCEPTED.

## Fix 1 — a 502 is not the user's mistake

**Where "Check the entered values." comes from.** It is not hard-coded in a view, a script or `SharedResource`. It is
the module resource key **`ValidationError`** in
`frontend/Diten.Web/Resources/Views/SupplyChain/Shipments/ShipmentsIndex.{en,…}.resx`. Each of those files is a single
XML line, which is likely why a grep of the views and the shared resources did not find it.

Path to the user:

1. `_IndexL10n.cshtml` bridges it to `L.validationError`.
2. It is the **fallback for any error code the page does not recognise**, in three places: `create.js:60`,
   `details.js:52` (the status fallback) and `details.js:125`/`rejectInvalid`.

**Root cause.** When SupplyChain is down, the Gateway answers **502 with an empty body**. The Web adapter
`SupplyChainShipmentsController.ProxyAsync` passed it through unchanged, so the page found no `error.code` and fell back
to the validation text.

**Fix.** In `ProxyAsync`, an upstream **5xx without the contract Error envelope** now returns the adapter's existing
`ContractFailure(503)`: `PERSISTENCE_UNAVAILABLE`, carrying the request's correlation id. That is the same envelope
the adapter already sent for `HttpRequestException` and timeouts. A 5xx that **does** carry the envelope passes
through untouched. The page then shows its existing server text, "The request outcome is unavailable. Retry the same
intent.", with the support reference. Validation failures keep the validation text.

`create.js` also maps **403** to `L.accessDenied` instead of the validation fallback, as `details.js` already did.

**Measured live.** The stack was a scratch copy of the edited tree with all six services; the tenant Admin's token
carried `legal_entity_id` (`evidence/fixture.json`). SupplyChain was stopped (this lane's own process) for each case:

| surface | before (Q362, and sabotage S1) | after |
|---|---|---|
| Create → Save | 502 → "Check the entered values. Support reference: …" | 503 `PERSISTENCE_UNAVAILABLE` → "The request outcome is unavailable. Retry the same intent. Support reference: …" |
| Details → Change Status → Confirm | 502 → "Check the entered values." | 503 → the same server message in the panel |
| Details → page load | 502 → "Check the entered values." under a stuck "Loading…" with empty cards | 503 → heading "Unavailable", server message, no cards |

Screenshots: `shots/F1-after-*`, `shots/S1-sabotage-*`.

## Fix 2 — Details draws the skeleton, not empty values

- `Details.cshtml` now renders the shared `.backbone-skeleton` (`.shimmer .skeleton-row`, revealed through `display`,
  per UI-PM-05). The card row and the action bar start `hidden inert aria-hidden="true"`.
- `details.js` hides the skeleton when the first load ends, whatever the outcome. The cards appear only with data. On
  a failure the heading reads `L.unavailable` instead of staying on "Loading…".

| measure (SupplyChain held with SIGSTOP) | before (sabotage S2) | after |
|---|---|---|
| skeleton | absent | visible |
| cards | visible, **9 empty value cells** | hidden, **0** empty value cells |
| after the load | — | skeleton gone, cards with real values, heading = shipment number |

Shots: `shots/F2-after-details-loading-skeleton.jpg`, `shots/S2-sabotage-details-loading-empty-cards-return.jpg`.

## Fix 3 — one Idempotency-Key per Create intent

**The contract**, `shipment-bundle.openapi.yaml`, operation notes at about lines 322–346:

- "Different valid payload under a committed key returns 409 IDEMPOTENCY_KEY_REUSED."
- "No receipt for failed requests."
- "Unknown commit uses 503 and same-key recovery."

It has **no** rule against two shipments per source document. `DUPLICATE_SHIPMENT` is used only for a shipment
repeated inside a Load (`LoadRepository.cs:36`).

**The defect.** `create.js:70` re-minted the key whenever the payload changed. After an unknown commit (the response
was lost), any edited retry got a fresh key and **created a second shipment**, and `IDEMPOTENCY_KEY_REUSED` was
unreachable.

**What the UI now sends.** **One key per Create form instance**, minted at page load (`const intentKey`). It is kept
across edits and failures and never reset by the page. A new Create page is a new intent. No new key scheme was
invented; this is the contract's "same-key recovery".

**Measured live.** "Lost response" was simulated by a test-only `fetch` wrapper that let the commit happen and then
threw a network error, which is exactly how the page experiences a timeout after commit:

| run | 1st Save | edit, 2nd Save | shipments in DB for that source document |
|---|---|---|---|
| **after** (`SO-Q371-LOST`) | 201, response dropped → "outcome unavailable" | **same key `04441819…` → 409 IDEMPOTENCY_KEY_REUSED** | **1** |
| **sabotage S3**, old `create.js` (`SO-Q371-SAB3`) | 201, response dropped | **new key `d76c81b2…` (1st was `bd5fe309…`) → 201** | **2** |
| **after**, retry after a 503 with no commit (`SO-Q371-A`) | 503 (SupplyChain down), key `726681c1…` | edit, SupplyChain back → **same key → 201** | 1 |

**Not changed:** two deliberate Create sessions for one source document still make two shipments. The contract
allows it, and partial shipments of one order are normal. Forbidding it would be a contract or service decision
(F-Q371-3).

## Fix 4 — the eight status names in all seven tenant languages

- **`ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`.** Each gains `StatusDraft … StatusCancelled` (8 keys; 64 → 72 per
  file). Each language agrees with its own word for "shipment": fr *expédition* f., ru *отгрузка* f., es *envío* m.,
  ar الشحنة f. The existing seven-file parity test passes.
- **`_IndexL10n.cshtml`.** Exposes `statuses` as a **`Dictionary<string,string>`** keyed by the contract's English name.
- **`_Filter.cshtml`.** `<option value="Draft">@Localizer["StatusDraft"]</option>` and so on: the label is localized,
  the wire value stays English.
- **`index.js` (row badge) and `details.js` (Status field, Change Status options).** The label comes from
  `L.statuses[status]`; the value stays English.

**One mistake of this lane's, caught live and fixed.** The first version used an anonymous object. MVC's
`Json.Serialize` camel-cases its property names (`draft`, `inTransit`), so the lookup by the API's `Draft`/`InTransit`
missed and the **ar badges stayed English** (`shots/F4-own-bug-ar-badges-english-camelcase-keys.jpg`). Dictionary keys
are not renamed. Re-measured after the change:

| language | filter options | row badges | English status words visible |
|---|---|---|---|
| ar (RTL, scroll 593/593) | كل الحالات, مسودة, مخططة, مُرسلة, قيد النقل, مُسلَّمة, استثناء, مغلقة, ملغاة | مخططة, مسودة | **0** (sabotage S4: **12**) |
| zh | 所有状态, 草稿, 已计划, 已发运, 运输中, 已交付, 异常, 已关闭, 已取消 | 已计划, 草稿 | **0** |
| fr | Tous les statuts, Brouillon, Planifiée, Expédiée, En transit, Livrée, Exception, Clôturée, Annulée | Planifiée, Brouillon | **0** ("Exception" is the French word) |
| fr Details | Status field "Planifiée"; Change Status options `Dispatched=Expédiée`, `Cancelled=Annulée` | — | — |

The filter still sends the contract value: choosing مسودة requests `…?status=Draft`.

## C-08 — the silent cut at 1000

Both note fields now carry a live counter (`n / 1000`, `aria-live="polite"`, red at the limit). The counter is
language-neutral text, so no new key was needed.

- **Live:** 1056 typed characters left 1000, and the counter read **"1000 / 1000"** in red.
- **Sabotage:** no counter, and no text mentioning 1000 anywhere in the panel.

Shots: `shots/C08-after-note-counter-at-limit.jpg`.

## The list timeout — measured, recommended, **not changed**

- The adapter uses the default transient `HttpClient` (`Program.cs`, the "FE-A-harden (A5)" registration), with no
  explicit timeout, so it inherits **100 s**.
- Q362 measured the Gateway answering 503 at **90 s**. Ocelot has no QoS timeout configured, so its default applies.
  For the whole 90 s the user sees "Loading…".

**Recommendation:** a named `HttpClient` for the Shipment adapter only, with **15 s** for reads and **30 s** for
writes. That is ~10–15× the slowest measured healthy call (Loads/Returns tests ~1–3 s per request; live Shipment calls
5–440 ms).

**Cost:**

- A write that times out after commit becomes an unknown outcome (503). Fix 3 makes that safe: the same key recovers
  it or yields 409, never a duplicate.
- A slow-but-healthy read past 15 s would show the failure alert instead of data.

**Why it is not changed here.** The client is the shared default registration, so changing it would touch every
controller that uses it. A dedicated client is a small, separate change that the owner should approve.

## Suites

| suite | result | delta |
|---|---|---|
| `Diten.Web.Tests`, fixed tree | **161 / 1 / 162** | +4 new tests (adapter 5xx ×3 cases + pass-through), all pass. The 1 failure is pre-existing: the same test fails on a HEAD copy (157/1/158), `ShipmentJsonAdapterChallengeTests.Unauthenticated_adapter_uses_json_401_while_page_keeps_login_redirect` (302 vs 401) |
| `Diten.Web.Tests`, changed test | `ShipmentDetailActionTests.StableIntentPreservesPayloadAndKey` **updated** | it pinned the old per-payload key (`intent.payload !== serialized`), i.e. the defect fix 3 removes. It now pins one key per form instance. Details' per-action rule is unchanged |
| Unit sabotage (new and updated tests against HEAD Web code) | **4 red / 1 green** | 3 adapter 5xx cases and the key pin fail; the pass-through case passes on both, as it should |
| SupplyChain, Q335 recipe, own mongod 57373 | **432 / 1 / 433** + guard 2/2 (435 listed) | **identical to Q335.** Claims: the restart-mode test, as always. `ShipmentTelemetryTests.cs:71` passed this run |

## SOP §18.0 — rows 12 and L10n

**Row 12, UX states: the three Q362 defects are closed and measured live.**

- save-failed shown as validation, ×2: now a server message;
- Details loading: now a skeleton;
- the silent note cut: now a counter.

D-1, D-2 and C-07 were already falsified by Q362. **What remains before the row can close**, as CT's ruling:

- Fix 1's server text says "Retry the same intent." even on a Details page **load**, where there is no intent
  (wording, F-Q371-5).
- After a failed **re**-load following an action, the earlier cards stay visible under the "Unavailable" heading:
  stale data with an honest warning (F-Q371-6).
- The Create conflict state is reachable now (409) only through a lost response. Its message, "The request identity
  belongs to different content.", is accurate but cryptic (F-Q371-4).

**L10n row: the status-name gap is closed** in all seven languages: filter, row badges, Details status field and
Change Status options, measured live in ar, zh and fr. **Remaining, outside this module:** the shell search
placeholder "Search [CTRL + K]" is English in every language (Q362 F-12). No l10n review of the new 56 strings is
recorded; an `l10n` reviewer should sign them off.

## Findings

- **F-Q371-1.** The text Q362 saw was the resx key `ValidationError`, used as the catch-all fallback. Root cause: the
  adapter passed the Gateway's empty-bodied 502 through.
- **F-Q371-2.** `datetime-local` prefill uses `toISOString()`, which is UTC (`details.js` "Change Status" and "Capture
  POD" openers), and is parsed back as local time. On a +03:00 machine an untouched prefill sends a time 3 h early.
  Seen live again (20:07 shown at 23:07). Q362 F-11 confirmed as a data defect, **not fixed** here (outside the four).
- **F-Q371-3.** The contract permits several shipments per source document. Duplicate prevention across separate
  Create sessions is a contract/service decision.
- **F-Q371-4.** `IdempotencyKeyReused` wording is cryptic for a user.
- **F-Q371-5.** `PersistenceUnavailable` wording ("Retry the same intent") also appears for a failed read.
- **F-Q371-6.** A failed re-load after an action leaves stale cards under the "Unavailable" heading.
- **F-Q371-7.** `#shipmentActions` keeps `d-flex`, whose `display:flex !important` defeats the `hidden` attribute.
  Harmless (it is empty until data), but the attribute is ineffective there.
- **F-Q371-8.** `L.empty` / `EmptyState` is dead. `DtDefaults.create` deliberately overrides `emptyTable` with the
  shared, localized `DtEmptyTable` (`dt-defaults.js:517-523`), which is why Q362 saw "No data available in table".
  Not a defect; the key could be dropped.
- **F-Q371-9.** One pre-existing Web test failure (`…uses_json_401…`, 302 vs 401) fails identically on HEAD.
  Unrelated; not investigated.

## Files changed

18 files (+104/−27), all under `frontend/`. sha256 values are in `CHANGED-FILES.txt`; the full diff is in
`evidence/diffs/frontend.diff`.

| file | change |
|---|---|
| `Controllers/SupplyChainShipmentsController.cs` | fix 1 |
| `wwwroot/assets/js/SupplyChain/Shipments/{create,details,index}.js` | fixes 1–4, C-08 |
| `Views/SupplyChain/Shipments/{Details,_Filter,_IndexL10n,_TransitionOffcanvas,_PodOffcanvas}.cshtml` | fixes 2 and 4, C-08 |
| `Resources/Views/SupplyChain/Shipments/ShipmentsIndex.*.resx` ×7 | fix 4 |
| `Diten.Web.Tests/Controllers/SupplyChainShipmentsControllerTests.cs` | +2 tests, 4 cases |
| `Diten.Web.Tests/JavaScript/ShipmentDetailActionTests.cs` | 1 pin updated |

## Environment

- Scratch `~/mvp6-env/q371-20261003-2258/`. The copy was checked equal to the repository; the HEAD-equivalent Web copy
  for sabotage was checked equal to `git show HEAD:` for every changed file.
- One lane mongod for the stack (57372) and one for the suite (57373). Both are stopped, every process this lane
  started is stopped, and ports 5000, 5001, 5056, 5057, 5059, 5061, 5199, 57372 and 57373 are free.
- The browser was signed out and its viewport reset.
- The `fetch` wrappers used to simulate a lost response and to log requests were test instruments injected in the
  browser. They are not product code.

Nothing committed, nothing pushed, nothing staged.
