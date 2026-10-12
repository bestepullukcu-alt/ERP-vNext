# Q382 — Shipment Details date round trip under Node, at two fixed zones

- Lane: Q382, `frontend-ui-ux`, single-writer on `details.js` and `frontend/Diten.Web.Tests/**`. Authority: owner decision
  2026-10-04 §2 (`docs/records/decisions/2026-10/mvp6-five-owner-decisions-01.md`).
- Placement: Claude app → Code tab → Local, `uname -s` = Darwin. G2 placement waiver applied — Cowork withdrawn by owner.
- Preflight 2026-10-04 12:29:03 +03: `feature/mvp6-logistics` · HEAD `c1f2dffe8` · `git status --short` 26 · no
  `index.lock`. `frontend/**` Edit = ask (not deny).
- Step 0 read: `AGENTS.md`, `git-safety.md`, `code-style.md`, `frontend-ui-ux.md`, Q374 `REPORT.md`, owner decision §2.
- **Nothing staged or committed. No `services/**` file touched. No existing test file changed** (only new files were
  added under `frontend/Diten.Web.Tests/`, so no file of Q360/Q361 is touched). No browser harness added to the repo
  or the suite. Agent verdict ≠ CT ACCEPTED.
- Start / end (Europe/Istanbul): 2026-10-04 12:29:03 +03 / see `ARTIFACTS.sha256` header.

## Verdict

**DONE, with one live check against the real file in Chromium.** The two pure date functions run under Node at
`TZ=Europe/Istanbul` and `TZ=UTC`, against the shipped `details.js` itself, and CI runs them through the existing
`dotnet test frontend/Diten.Web.Tests` step. Restoring the old UTC prefill turns the Istanbul run RED and leaves the
UTC run GREEN — the measured reason both zones are required.

## What changed

| file | change |
|---|---|
| `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js` | `instant(id)` split into a pure `parseLocalInput(raw)` and a one-line DOM wrapper `instant(id) => parseLocalInput(document.getElementById(id).value)`; `localInputValue` and `parseLocalInput` added to the object the module already returns. +5 / −3, `evidence/details-js.diff`. sha256 `44895025…` → `38b66485…` |
| `frontend/Diten.Web.Tests/JavaScript/Node/shipment-details-datetime.test.mjs` (new) | `node:test`, no dependency. Loads the shipped `details.js` in a `vm` context with a `document` that has no `#shipment-details` host, so `init()` returns at once, and takes the two functions from `ShipmentDetails` |
| `frontend/Diten.Web.Tests/JavaScript/ShipmentDetailsDateNodeTests.cs` (new) | xunit Theory, one case per zone: runs `node --test` with `TZ` set, requires exit 0, `# pass 6` and `# fail 0` |
| `frontend/Diten.Web.Tests/README.md` (new) | what a green run of this project proves and does not prove — the owner-accepted DOM gap |

### Why the split does not change runtime behaviour

The parse body is moved, not edited: the same regular expression, the same `new Date(y, m-1, d, h, mi, s)` and the same
exactness check, now fed `raw` instead of `document.getElementById(id).value`. `instant(id)` passes exactly that value.
`prefillNow` already called `localInputValue`; it is untouched. The returned object gains two members that nothing
calls in the page.

Measured, not only argued — `evidence/live-dom-check.txt`: the **original** bytes (`44895025`) and the **current** bytes
(`38b66485`) driven in Chromium (Playwright, network stubbed, scratch only) at both zones. Change Status and Capture POD
were clicked, the prefilled field read and the form submitted; the request body was captured.

| bytes | zone | field shown | zone hint | instant sent | = real minute |
|---|---|---|---|---|---|
| original | Europe/Istanbul | `2026-10-04T13:15` | `Europe/Istanbul (UTC+03:00)` | `2026-10-04T10:15:00.000Z` | yes (both forms) |
| original | UTC | `2026-10-04T10:15` | `UTC (UTC+00:00)` | `2026-10-04T10:15:00.000Z` | yes (both forms) |
| current | Europe/Istanbul | `2026-10-04T13:15` | `Europe/Istanbul (UTC+03:00)` | `2026-10-04T10:15:00.000Z` | yes (both forms) |
| current | UTC | `2026-10-04T10:15` | `UTC (UTC+00:00)` | `2026-10-04T10:15:00.000Z` | yes (both forms) |

That is the Q374 result — local wall clock in the field, the same instant on the wire — on the field-to-request leg.
The server leg is unchanged by this lane (no `services/**` edit); Q374 proved the server stores the instant it receives.
The full six-service stack was not restarted.

## The Node tests (6 cases, run at both zones)

| # | case | Istanbul expects | UTC expects |
|---|---|---|---|
| 1 | `TZ` is one of the two, and really in effect (`getTimezoneOffset` of a fixed instant) | −180 | 0 |
| 2 | a Date prefills to the local wall clock | `20:38Z` → `23:38`; `2026-01-15T23:30Z` → `2026-01-16T02:30` (date boundary) | `20:38`; `2026-01-15T23:30` |
| 3 | the prefilled string parses back to the same instant | 4 instants, exact | 4 instants, exact |
| 4 | a typed wall clock with seconds is read as local | `23:38:15` → `20:38:15Z` | `20:38:15` → `20:38:15Z` |
| 5 | out-of-range and malformed values are rejected, not shifted | 11 inputs → `null` | 11 inputs → `null` |
| 6 | a daylight-saving gap hour is rejected | `2016-03-27T03:30` → `null` (Istanbul's last spring-forward, 03:00→04:00); `04:30` → `01:30Z` | the same string is valid: `03:30Z` |

The expected values are written out by hand from the zones' rules, not computed with the code under test. Case 1 fails
the run if `TZ` is missing or ignored, so an "Istanbul" run cannot silently be a second UTC run (checked: TZ unset →
5 of 6 fail, message `TZ must be Europe/Istanbul or UTC, got undefined`).

## Proof

| run | Istanbul | UTC | evidence |
|---|---|---|---|
| after the change | **6/6 pass** | **6/6 pass** | `final-*.tap` |
| **SABOTAGE**: `localInputValue` restored to the old `when.toISOString().slice(0, 16)` in the repo file (sha256 `c9619a8c…`) | **RED, 4/6**: case 2 `expected '2026-10-03T23:38'`, `actual '2026-10-03T20:38'`; case 3 off by 10,800,000 ms (3 h) | **GREEN, 6/6** | `sabotage-*.tap`, `sabotage-hash.txt` |
| sabotage through the CI path (`dotnet test --filter ShipmentDetailsDateNodeTests`) | **Failed** | **Passed** | `suite-results.txt` |
| restored (sha256 back to `38b66485…`) | 6/6 | 6/6 | `restored-*.tap` |

**The UTC run stays green with the defect present.** That is the owner's Q361 point measured on this code: at UTC the
UTC wall clock is the local wall clock, so the old prefill is indistinguishable from the fix. Only the Istanbul run
can fail for the reason the test exists.

## Frontend suite

| state | result | evidence |
|---|---|---|
| before (copy with the original `details.js`, `44895025`, and without the new files) | **161 / 1 / 162** | `suite-results.txt` |
| after (final bytes) | **163 / 1 / 164** — the 162 unchanged plus the two zone cases, both passing | `suite-results.txt` |

The one failure in both is `ShipmentJsonAdapterChallengeTests.Unauthenticated_adapter_uses_json_401_while_page_keeps_login_redirect`
(Expected `Unauthorized`, Actual `Found`), pre-existing as Q371 and Q374 recorded. Runs were made in scratch copies
under `~/mvp6-env/q382-20261004-1304/`, not in the repository's `bin/obj`.

## How it runs in CI

CI runs `.github/workflows/phase1-gates.yml` → `scripts/run_phase1_gates.sh`, whose line 42 is
`dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug`. The bridge test lives in that project, so the
existing step now runs both zones; no CI file was changed (both are outside this lane). Requirements: `node` 18+ on the
runner's PATH. If `node` is missing, the two cases **fail** (`Process.Start` throws) — they cannot skip. **Not verified on
the CI runner by this lane**; GitHub's `ubuntu-latest` image is documented to ship Node, but no CI run was observed.

## The coverage gap, made visible

`frontend/Diten.Web.Tests/README.md` states, next to the suite: DOM behaviour is proven only by live lane runs, never by
this suite; the source-text tests execute nothing and can pin a defect; and a figure like 161/1/162 (now 163/1/164)
means the server side and the two date conversions passed, not that frontend behaviour was tested. The bridge test's
header and the Node file's header say the same.

## Findings

- **F-Q382-1** — At UTC the sabotaged prefill passes all six cases; at Istanbul it fails two. A UTC-only guard would be blind to the Q374 defect. `sabotage-UTC.tap`, `sabotage-Europe-Istanbul.tap`.
- **F-Q382-2** — CT's premise "there is NO JS engine" holds for `frontend/Diten.Web.Tests`, but the repository already has a **second, Node-based test suite**: `frontend/Diten.Web/package.json` declares vitest + jsdom (`"test": "vitest run"`), and `frontend/Diten.Web/tests/` holds `*.test.js` files with a `load-script` helper, including `date-field-never-dies-silently.test.js`. CI never runs it (no `npm`/`vitest` in `scripts/run_phase1_gates.sh` or the workflow), and its `node_modules` is not installed in this checkout. Outside this lane's paths; not touched. Two Node test homes now exist; whether to run, merge or retire that suite is a CT/owner question.
- **F-Q382-3** — `details.js` calls `crypto.randomUUID()` on load (`:14`, used by `load()`). That API exists only in a secure context (HTTPS or localhost). Served over plain HTTP on any other host, the Details page would fail to load the shipment and show only an empty error alert. Measured by accident in the scratch check (first attempt on `http://q382.local`). Not changed here.
- **F-Q382-4** — `node --test <directory>` fails on Node 21+ (the argument is taken as a module path). The first draft of the README and the Node file's header gave that form; both were corrected to name the file. The bridge was never affected.
- **F-Q382-5** — Running this suite in a copy needs all of `services/`: `NavManifestL10nGuardTests` scans manifest providers repository-wide, and a copy with only `Diten.Building.Blocks` produced 4 false failures (159/5/164). Recipe note for future lanes.
- **F-Q382-6** — The baseline copy accidentally included the new `README.md` (exclude pattern slip). It holds no test; the 161/1/162 count is unaffected.
- **F-Q382-7** — `prefillNow`, `zoneLabel`, `instant` and the submit handlers remain outside the suite, as the owner accepted. This lane covered them once, live, in `live-dom-check.txt`; nothing re-runs that.

## Not done

- No CI run observed. No change to the workflow or `run_phase1_gates.sh` (outside the lane).
- The six-service stack of Q374 was not restarted; the server leg is unchanged and was not re-measured.
- No other module's date code was examined.
