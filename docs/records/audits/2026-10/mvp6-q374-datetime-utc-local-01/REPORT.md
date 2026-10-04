# Q374 — Shipment Details date fields: local wall clock in the field, UTC on the wire

- Lane: Q374, frontend-ui-ux, single-writer on `wwwroot/assets/js/SupplyChain/Shipments/details.js` and the two
  offcanvas partials. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 20:27:59 UTC 2026` · `feature/mvp6-logistics` · HEAD `c1f2dffe8` · porcelain 23 · staged 0.
  Ledger row present (CT-QUEUE.tsv:476).
- Step 0 read: `AGENTS.md`, `git-safety.md`, `code-style.md`, `frontend-ui-ux.md`, MOD-0183 §12, Q371 `REPORT.md`
  (F-Q371-2 is the timezone note; the dispatch's "F-Q362-11" is not a label in that report).
- **No `services/**` file, no Web controller, no contract and no server validation touched. Nothing staged or
  committed. No token, password or secret printed.** Agent verdict ≠ CT ACCEPTED.

## Verdict

**FIXED and measured live at UTC+03:00 (Europe/Istanbul).** The field shows the user's local wall clock, the request
carries the same instant in UTC, the stored value equals the real click to the minute, and the Details page displays
it back as the same local time. Reverting the change brings the three-hour shift back.

## CT's measured premises, checked

| # | premise | verdict | evidence |
|---|---|---|---|
| 1 | `details.js:93` / `:98` prefill a UTC wall clock (`toISOString().slice(0,16)`) into a `datetime-local` input | **TRUE** | pre-edit `details.js:93,98`; inputs at `_TransitionOffcanvas.cshtml:4`, `_PodOffcanvas.cshtml:4` |
| 2 | `:147` / `:153` read the field through `instant()` (`new Date(value)` → local), `:150` / `:157` send `.toISOString()` | **TRUE** | pre-edit `details.js:62,147,150,153,157` |
| 3 | the sent instant is shifted by the browser offset (−3 h at UTC+3) | **TRUE, measured** | sabotage run below: real 20:43:25Z → stored 17:43:00Z |
| 4 | an untouched prefill can land a `ReceivedAt` **before** the dispatch it follows | **FALSE as worded** | the server refuses that: `CapturePodHandler.cs:19` rejects `ReceivedAt < DispatchedAt` with 422 `INVALID_SHIPMENT_TRANSITION`. What the bug actually did: `DispatchedAt` is the transition's own client `OccurredAt` (`TransitionShipmentHandler.cs:21`), so a dispatch made through this UI was stored 3 h early too, and both sat 3 h before the real events. A dispatch recorded with a correct time (another client) would instead make every UI POD of the next 3 h fail with 422 |
| 5 | the display path `details.js:82` (`date(pod.receivedAt)`) | **correct — no mirror bug** | `date()` formats `new Date(<ISO with Z>)` with `Intl.DateTimeFormat`, i.e. the stored UTC instant in the browser's zone. A wrong stored value therefore displayed **wrong** (3 h early) and was visible, not hidden |

## The change (`evidence/change.diff`, +22 / −5)

- `details.js`: `prefillNow(id)` fills the field with the **local** wall clock (`localInputValue`, built from
  `getFullYear()…getMinutes()`), and writes the browser's zone into a hint; `instant(id)` now parses
  `YYYY-MM-DDTHH:mm[:ss]` explicitly as **local** time with `new Date(y, m-1, d, h, mi, s)` instead of relying on the
  engine's string parsing, and **rejects** a wall-clock time that does not exist (out-of-range parts or a skipped
  daylight-saving hour) instead of shifting it. `.toISOString()` at the send sites stays: that is the one UTC boundary.
- The two partials: the input keeps its `datetime-local` type and gains `aria-describedby` pointing to a new
  `form-text` (`occurredAtZone`, `receivedAtZone`) that the script fills with the zone and offset, e.g.
  `Europe/Istanbul (UTC+03:00)`. It is data from the browser, not a translatable sentence, so no resx key was needed;
  the resx files are outside this lane.

**Why this implementation.** A `datetime-local` value is, to the browser and the user, a local wall clock. The defect
was only that the prefill wrote a UTC clock into it; the read side was already local. Filling it with the local clock
makes the field honest, and keeping a single conversion (local → `toISOString()`) at the request is the smallest change
that makes field, wire and display one instant. Switching the input to text/UTC would have put a UTC clock in front of
users who think in local time.

**A user whose machine clock or zone is wrong.** The browser is the source of "now" and of the zone. A wrong clock
pre-fills a wrong time, visibly, in the field the user can correct. A wrong zone with a right wall clock sends an
instant off by the zone error; the new hint shows the zone the browser believes, so the user can see it. Neither case
can produce a POD before its stored dispatch: the server still rejects that (reverse test below).

## Live proof — UTC+03:00, browser zone `Europe/Istanbul`, offset 180 min

Stack: scratch copy `~/mvp6-env/q374-20261003-2330/stack` (8,050 files, 0 mismatches against the repository; manifest
sha256 `8f482773…`), six services (Auth 5056, Platform 5057, MDM 5059, SupplyChain 5061, Gateway 5000, Web 5001) on a
lane mongod 57374 (`rsq374`), one throwaway JWT secret (mode 600, never printed). Users through Auth's invite
endpoint (Q371's fixture method); the tenant Admin's token carried `legal_entity_id` and all five shipment keys
(`evidence/fixture-summary.json`). The browser signed in with the password served to the page by a local helper; no
value passed through the transcript.

| run | build | shipment | field shown (local) | real instant of the action | stored (database) |
|---|---|---|---|---|---|
| **a** Change Status → Planned, prefill untouched | fixed | `67e8f05a` | `2026-10-03T23:38`, hint `Europe/Istanbul (UTC+03:00)` | opened 20:38:09Z, confirmed 20:38:30Z | `OccurredAt 2026-10-03T20:38:00Z` — **same minute** |
| **b** Dispatch, then Capture POD at once, both untouched | fixed | `67e8f05a` | 23:38 / 23:38 | 20:38:54Z / 20:38:57Z | `DispatchedAt 20:38:00Z`, `ReceivedAt 20:38:00Z` — not before dispatch (equal at minute precision) |
| **b′** Dispatch, wait 65 s+, POD untouched | fixed | `5386ddfb` | 23:39 / 23:41 | 20:39:34Z / 20:41:15Z | `DispatchedAt 20:39:00Z`, `ReceivedAt 20:41:00Z` — **after** dispatch |
| **reverse** POD dated 2 h before now | fixed | `8219d001` | set by hand to `21:41` local | — | **rejected**: page shows "The current status no longer permits this transition." (422); status stays Dispatched, no POD stored |
| **sabotage** (a) with the three files reverted | pre-edit | `d52bda07` | `2026-10-03T20:43` (UTC clock), no hint | opened 20:43:25Z | `OccurredAt 2026-10-03T17:43:00Z` — **3 h early** |
| **restored** (a) again | fixed | `6607431b` | `23:45`, hint present | opened 20:45:04Z | `OccurredAt 2026-10-03T20:45:00Z` |

The Details page displayed the POD of run b as "Oct 3, 2026, 11:38 PM", the local time of the stored 20:38Z.
Database readings: `evidence/a-fixed-db.txt`, `b-fixed-db.txt`, `b2-c-db.txt`, `a-sabotage-db.txt`, `a-restored-db.txt`.
Conversion logic outside the browser: `evidence/convert-check-istanbul.txt` (fixed: delta 0 min; old: −180 min).

## Suites

| suite | result | note |
|---|---|---|
| `Diten.Web.Tests` (scratch copy) | **161 / 1 / 162** | the failure is `ShipmentJsonAdapterChallengeTests.Unauthenticated_adapter_uses_json_401_while_page_keeps_login_redirect`, which Q371 recorded as failing on a HEAD copy too (161/1/162 there). No test references these date fields (`grep toISOString\|occurredAt\|receivedAt` in the test project → 0) |
| SupplyChain, Q335 recipe, own mongod 57375 | **432 / 1 / 433** | within the range; the one failure is the known Claims restart-mode test. `--list-tests` shows 435: the two Q366 guard tests sit outside the seven module filters, as Q371 recorded |

## Does any stored shipment carry a broken-path timestamp?

- **This environment has no production database.** Every `sce_shipment_history` with transitions on the operational
  instance (27017, read only, no write) is test or lane data: `diten_mod0183_tests` (148), `diten_r04` (3),
  `diten_supplychain_q279` (9). Their latest values are round, fixed instants (`…15:42:00Z`, `…18:00:00Z`,
  `…10:00:00Z`), the pattern of API-seeded fixtures, not UI clicks. A per-row attribution is not possible: the
  history row carries no field that says which client wrote it.
- The UI-written rows Q362 and Q371 saw "three hours early" live in those lanes' own scratch mongods (stopped,
  under `~/mvp6-env/`), which are throwaway.
- **Answer for the owner:** no shipment in any database I can see is known to carry a broken-path timestamp outside
  throwaway lane data. If a non-test environment has ever taken Change Status or Capture POD through this page from
  a browser outside UTC, its `OccurredAt`, `DispatchedAt`, `ActualDeliverAt` and `ReceivedAt` values are early by that
  browser's offset, and that would be a data question bigger than this lane.

## Findings

- **F-Q374-1.** Premise 4 is false as worded: a POD before its stored dispatch cannot be stored (`CapturePodHandler.cs:19`, 422). The real harm was that dispatch and POD were both stored early by the browser offset; mixed with a correctly-timed dispatch it blocks the POD instead.
- **F-Q374-2.** The display path (`details.js:82`, `date()` at `:21`) was already correct; there was no mirror bug hiding the defect.
- **F-Q374-3.** The read side was already local; only the prefill was wrong. The fix also makes the parse explicit and rejects non-existent local times, which `new Date(string)` would have shifted silently.
- **F-Q374-4.** No automated test guards these fields (`grep` in `frontend/Diten.Web.Tests` → 0). Test files are outside this lane; a behaviour test that runs the prefill under a non-UTC `TZ` would pin it.
- **F-Q374-5.** The "Required: 0 / 2" tracker in the Change Status offcanvas shows 0 while both fields are filled by script, until a field is touched (screenshot during run a). Pre-existing (the old prefill also set `.value` without an input event); not changed here.
- **F-Q374-6.** The dispatch cites "F-Q362-11" in the Q371 report; that report labels the item **F-Q371-2**.
- **F-Q374-7.** The browser tab once switched to the Create page while a 65-second in-page wait was running (run b′); the POD of that attempt never ran. It was redone; the database shows exactly one POD for that shipment.

## Cleanup

Stopped by recorded pid: web, gateway, supplychain, mdm, platform, auth, the helper, and both lane mongods (57374,
57375). Ports 5000, 5001, 5056, 5057, 5059, 5061, 5199, 57374 and 57375 are free. The browser was signed out. The
scratch folder `~/mvp6-env/q374-20261003-2330/` is left in place (no `rm`); the password and secret files in it are
mode 600.
