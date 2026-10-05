# verify from: /Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-10/mvp6-q372-observability-close-01 — `shasum -a 256 -c ARTIFACTS.sha256`

# Q372 (R2, resumed) — MOD-0183 §8.1: Web logs the correlation id; the Shipment meter read live

- Lane: Q372-R2, resumed by the Q372 RESUME dispatch. Agent: integration-agent. Ledger row present at CT-QUEUE.tsv:474.
  CT rulings Q388, Q389 and Q390 are present at :488-490.
- Preflight at resume: `Sun Oct  4 19:58:00 UTC 2026` · `feature/mvp6-logistics` · HEAD `cea01354e` · porcelain 48 ·
  staged 0 · no `.git/index.lock`.
- Live runs: 2026-10-04 11:00–12:48 UTC (14:00–15:48 local), before the pause. Suites: 20:02–20:25 UTC, after the
  resume (see Suites).
- **No exporter registered. Shipments views and scripts not touched. Nothing staged or committed. No token, password or
  secret printed.** Agent verdict ≠ CT ACCEPTED.
- The STOPPED report of 2026-10-03, which this file replaces, is kept verbatim at the end (History).

## Verdict per §8.1 signal

| signal | §8.1 requires | measured | reads MET? |
|---|---|---|---|
| **O-1** | one correlation id across web, gateway and service **in the logs**, for one user action | one Save click: **Web 1 · Gateway 1 · SupplyChain 8** lines carry the one id. Sabotage **0 · 1 · 8**. Restored **1 · 1 · 8** | **MET** |
| **O-2** | p95 **measured and reported** (A9: 500 ms is an observation threshold, not an SLA) | p95 of `shipment.operation.duration` read live with `dotnet-counters`, per operation (table below) | **MET** — reported, not judged against anything |
| **O-3** | four counters, each on its named §13 behaviour | **replays** and **scope denials** moved live on their behaviours. **intake** and **drift** cannot move: their only increment sites are in `WarehouseIntakeCoordinator`, which nothing in the service references (Q389), and `/health` declares `warehouseIntake = "unimplemented"` | **MET under Q388** (wiring). Not demonstrated for intake or drift |
| **O-4** | three counters over the outbox's own states | **pending** moved live, once per enqueued event. **retries** and **dead letters** cannot move: no `IEventTransportPublisher` exists in the service, so the worker logs "transport is not registered" and returns (Q390). `/health` declares `outboxTransport = "integration-owned"` | **MET under Q388** (wiring). Not demonstrated for retries or dead letters |

**Row 13 closes under CT's Q388 reading.** No signal is open on that reading. If the owner overrules Q388 and holds O-3
and O-4 to live demonstration, row 13 stays open on exactly four instruments: `shipment.intake.blocked`,
`shipment.intake.drift`, `shipment.outbox.retries` and `shipment.outbox.dead_letters`. Those four can only move after
Q389 (intake wired) and Q390 (outbox transport) are built.

The pack's §8.1 status column still says "NOT MET" / "partial" / "not measured live". This lane may not write the pack.
The text a pack writer needs is the table above, with this record cited.

## Part A — O-1: Web logs the id that is already there

**Corrected premises, as CT restated them.**
- The controller does not mint an id for a normal request. A request without a valid `X-Correlation-Id` gets **400**
  locally (`TryForwardUuidHeader` → `FailRequest`, `SupplyChainShipmentsController.cs:176-177` today).
- The Shipments scripts send the header in four places (`index.js:10`, `create.js:76`, `details.js` load and send).
- So every proxied request already carries exactly one validated UUID. The only gap was that Web did not write it.

**The change** (`evidence/change.diff`, controller only, +5/−4 lines, made before the pause):
- `ProxyAsync` reads the id that `TryCreateGatewayRequest` validated and forwarded:
  `request.Headers.GetValues(CorrelationHeader).Single()`. This is the same value that goes to the Gateway.
- The four existing log calls (the 5xx-without-contract warning, timeout, request failure, unexpected failure) now
  carry `{CorrelationId}`.
- One new `LogInformation` line on the success path: `Shipment Gateway answered {StatusCode} for {Method} {TargetUrl};
  correlation {CorrelationId}.` A successful user action therefore writes exactly one Web line.

**Why this mechanism.**
- No dependency added. Web has no Serilog, and the default `ILogger` already renders the message template.
- No scope or console option needed. `BeginScope` with `IncludeScopes` would also work, but it needs `Program.cs` or
  appsettings changes and puts the id only where scopes are rendered.
- `Program.cs` and `appsettings.Development.example.json` were allowed and are **not** changed by this lane.

**The edit is now in HEAD.** The owner committed it in `ca9330392` ("MOD-0186 Returns UI, the Web cross-cutting fixes
…"). The controller at HEAD, the working tree and the live-run copy have one sha256, `5db37c67…94ea53`
(`SOURCE-AS-MEASURED.sha256`). The diff base is `ff5777afe`, the HEAD when the lane started.

**Proof — one Create Save click as the tenant Admin, through the browser** (one id per run, read from Details'
lifecycle root, then `grep -F <id>` in each log):

| run | Web build | id | Web | Gateway | SupplyChain |
|---|---|---|---|---|---|
| fixed | working-tree controller | `c5042229-1298-464a-bb4c-548dec805b60` | **1** | 1 | 8 |
| **sabotage** | controller from `ff5777afe` | `70613a5c-e5ea-46fe-83ec-b6c528fafdde` | **0** | 1 | 8 |
| restored | working-tree controller | `c3b6835b-ed11-42b2-9897-849a48bab853` | **1** | 1 | 8 |

Side by side, fixed run (`evidence/o1-fixed.log` has every full line):

```
Web (1)        Shipment Gateway answered 201 for POST http://localhost:5000/api/shipment-bundle/shipments; correlation c5042229-1298-464a-bb4c-548dec805b60.
Gateway (1)    {"Timestamp":"2026-10-04T14:03:37.29…","RenderedMessage":"HTTP \"POST\" \"/api/shipment-bundle/shipments\" responded 201 in 368.8367 ms", … "Properties":{"CorrelationId":"c5042229-…"}}
SupplyChain(8) Request starting … / Executing endpoint … / Handling CreateShipmentCommand … / Executing ObjectResult … /
               Executed action … / Executed endpoint … / Request finished … 201 …, plus the "Route matched" line —
               8 lines, each containing c5042229-…
```

In the sabotage run, the Web log has 0 lines with the id and 0 lines mentioning "Shipment Gateway" at all
(`evidence/o1-sabotage.log`). The Gateway and SupplyChain counts do not change, so the 0 is the Web change alone.

## Part B — the eight instruments, read live with `dotnet-counters`

- Tool: `~/.dotnet/tools/dotnet-counters` 10.0.745401.
- Command: `dotnet-counters monitor --process-id 11090 --refresh-interval 2 --counters Diten.SupplyChainService.Shipments`.
  pid 11090 was the lane's SupplyChain (`Diten.SupplyChainService.Api.dll`, port 5061).
- No exporter, no code change.
- Behaviours driven through the Gateway (5000) as the tenant Admin by `evidence/drive.py`. It prints statuses, ids
  and timings only; the token stays in memory.

**How the screen was captured.**
- `monitor` renders its table only on a terminal. Without one it prints only "Status: Running"
  (`evidence/counters-monitor-nontty.raw`), so it ran in a terminal tab and the screen was read back.
- `collect --format csv` could not be used. Only one metrics session per process is allowed, and the one
  `collect` attempt wrote no file before it was stopped.
- So the raw output is the screen text (`evidence/b1-monitor-screen.txt`, `evidence/b3-monitor-screen.txt`).
- The reader appends in-place cell rewrites to the last line. Those trailing number streams are kept verbatim but not
  interpreted; every figure below comes from a full rendered frame.

**What the frames show** (all counters are cumulative from the moment the monitor session attached):

| instrument | behaviour driven (`evidence/b-drive.jsonl`) | raw frame value | moved? |
|---|---|---|---|
| `shipment.operation.duration` (O-2) | creates, lists, one transition | percentiles per operation, below | **yes** |
| `shipment.idempotency.replays` (O-3) | create, then the same Idempotency-Key, body and correlation id → 201 then **200, same shipment id** | `1` after the first replay; the trailing stream repeats `2 12 1` after the second (replays 2, pending 12, denials 1) | **yes**, once per replay |
| `shipment.scope.denials` (O-3) | list with a foreign `X-Legal-Entity-Id` → **404 `SHIPMENT_NOT_FOUND`** | `1` | **yes** |
| `shipment.outbox.pending` (O-4) | each committed create or transition | `1` after the first create (the replay added none); `2` after one create plus one transition (phase 3) | **yes**, once per event |
| `shipment.intake.blocked` (O-3) | — | never appears | **cannot move**: increment `WarehouseIntakeCoordinator.cs:78`; the class has 0 references in `src/` (Q389) |
| `shipment.intake.drift` (O-3) | — | never appears | **cannot move**: increment `WarehouseIntakeCoordinator.cs:87`; same (Q389) |
| `shipment.outbox.retries` (O-4) | — | never appears | **cannot move**: increment `ShipmentOutboxStore.cs:38` runs only inside a publish attempt; `ShipmentOutboxWorker.cs:11` returns when no transport is registered (Q390) |
| `shipment.outbox.dead_letters` (O-4) | — | never appears | **cannot move**: `ShipmentOutboxStore.cs:38,43`; same (Q390) |

**O-2: p95 as reported.** Read from full frames, in ms. dotnet-counters aggregates each 2 s collection interval
separately.

| frame | operation | p50 | **p95** | p99 |
|---|---|---|---|---|
| phase 1, first replay | CreateShipmentCommand | 105.125 | **105.125** | 105.125 |
| phase 1, during the 40-request load | GetShipmentListQuery | 2.629 | **11.672** | 27.719 |
| phase 3, one create plus one transition | CreateShipmentCommand | 83 | **83** | 83 |
| phase 3, same | TransitionShipmentCommand | 26.031 | **26.031** | 26.031 |
| phase 3, during the 120-request load | GetShipmentListQuery | 3.434 | **5.758** | 5.758 |

These are observations on one developer machine with one lane's data. Under A9 they are not compared with the 500 ms
threshold or with any target; no pass or fail is claimed. A frame with a single sample shows the same value at every
percentile.

The driver also timed the client round trip through the Gateway (p95 19.5, 11.8, 14.1 and 15.2 ms for its four
loads). That is not the instrument, and it is recorded only as context.

**Wiring of the four that cannot move.** The four counters each have a direct increment site (table above) and a test
that moves them on the named behaviour. The tests are in `ShipmentTelemetryTests.cs`: intake `:75`, drift `:98`, `:136` and `:154`,
retries `:243`, dead letters `:260`. They passed in this lane's suite run (Shipments filter 90/0/90). That is what Q388 counts as MET.
It is not a live demonstration.

**The outbox, in the database.** `q372_supplychain.sce_shipment_outbox` held **93 rows, all `Pending`**, at the end of
the live run (`evidence/b-outbox-status.txt`). The service logged at startup: "Shipment outbox transport is not
registered. Durable events remain pending; integration is held." `shipment.outbox.pending` counts enqueues since the
session attached. It is not a gauge of rows currently pending, so 12 or 32 on screen and 93 in the collection do not
contradict each other.

## Suites (resume, on a fresh copy of today's working tree)

- Copy: `~/mvp6-env/q372-20261004-2305/stack`. 0 differences from the repository for `services/Diten.SupplyChainService`,
  `frontend/Diten.Web` and `frontend/Diten.Web.Tests` (`rsync -anc`). The untracked local
  `appsettings.Development.json` was copied in without being read.

| suite | result | note |
|---|---|---|
| `Diten.Web.Tests` | **390 / 0 / 390** | first run 370/20/390: all 20 were "`JwtSettings:Secret` is required" because I had excluded the untracked `appsettings.Development.json` from the copy. Copied in, rerun green. The pre-pause run on the lane-start tree was 163/1/164 (the adapter-challenge test, since fixed by Q394) |
| SupplyChain, Q335 recipe, lane mongod 57373 (`enableTestCommands=1`) | **431 / 2 / 433** | **one more failure than the 432/1/433 range.** Per module: Shipments 90/0, Carriers 36/0, Loads 33/0, Returns 77/1, Claims 128/1, S&OP 19/0, Capacity 48/0. Claims: the known restart-mode test. Returns: `ReturnReferenceTests.OtherProducerFailures_RemainDependencyUnavailable(403, …)` expects 503 and gets 403. **Not this lane:** the working tree's `ReturnReferenceReader.cs` carries another lane's uncommitted "Q420" edit (403 → `INVALID_REQUEST` 403), written 20:00 UTC; the test still pins the old mapping. With the HEAD reader in the scratch copy, `ReturnReferenceTests` are 24/24; with the working-tree reader, 23/1 (`evidence/returns-failure-isolation.txt`). `ShipmentTelemetryTests` (the Q361 flake at `:71` included) passed 10/10 (`evidence/telemetry-tests.txt`). Full output: `evidence/sc-suite.txt` |

## Findings

- **F-Q372-1.** The fix was already committed by the owner (`ca9330392`) while the lane was paused. The lane's proof
  was taken on the same bytes (sha256 equal), so it holds for HEAD.
- **F-Q372-2.** `shipment.outbox.pending` is a counter of enqueued events, not a gauge. A reader who wants the
  backlog must query the collection. On this run that was 93, all Pending.
- **F-Q372-3.** The first replay attempt (14:08:35) got **400**, not 200. The driver had sent a new correlation id on
  the same-key retry, and the service rejects a known key with a different id. Once the driver resent the original
  id, the replay answered 200 with the same shipment. This is the API side of F-R1-2 / Q393, measured here before
  R-1 named it.
- **F-Q372-4.** The first transition attempt (14:13:10) got 400 `INVALID_REQUEST` because the driver did not send the
  shipment's lifecycle correlation id, which `details.js` does send. Once that was fixed, 200.
- **F-Q372-5.** The disk filled during the live run (257 MB free, not from this lane). The lane mongod 57372 died, and
  so did the operational 27017 mongod, which this lane did not touch. On the owner's answer ("Delete my sab/ copy"),
  the lane's own 482 MB sabotage copy `~/mvp6-env/q372-20261004-1359/sab` was deleted with `rm -rf`. That was owner
  approval for one specific folder, made by the pre-pause session. Mongod 57372 restarted on its own data. Phase 3
  was taken after the restart.
- **F-Q372-7.** The SupplyChain suite reads 431/2/433, outside the range, because of another lane's uncommitted
  `ReturnReferenceReader.cs` change (Q420). That lane's `ReturnReferenceTests` theory row `403` must be updated with
  it, or the change reverted. This lane changed no `services/**` file. Isolated in `evidence/returns-failure-isolation.txt`.
- **F-Q372-6.** The dispatch called 57373 "your paused mongod". It is this lane's: it was started by the pre-pause
  session for the suite (`--replSet rsq372s`, dbpath under `q372-20261004-1359`). The summary I resumed from did not
  list it.

## Cleanup

- Live stack stopped before the pause (Auth, Platform, MDM, SupplyChain, Gateway, Web, helper, mongod 57372).
- The browser pane is closed: no tab, no signed-in session.
- After the resume suites, the lane's suite mongod 57373 was shut down. Ports 5000, 5001, 5056, 5057, 5059, 5061,
  5199, 57372 and 57373 are closed, and no process from either Q372 folder is running.
- Scratch folders `~/mvp6-env/q372-20261004-1359/` and `~/mvp6-env/q372-20261004-2305/` are left in place (no `rm`).
  The password and secret files are mode 600. In the resume folder, `ReturnReferenceReader.cs` was swapped to HEAD for
  the isolation run and then copied back; it equals the repository file again (`cmp`).

Nothing committed, nothing pushed, nothing staged.

---

## History — the STOPPED report of 2026-10-03, verbatim

> # Q372 — O-1..O-4 close attempt: STOPPED before any change
>
> Measured 2026-10-03 19:56 UTC, branch `feature/mvp6-logistics` @ `c1f2dffe8`, 5 porcelain lines, 0 staged.
> Nothing was edited, built, started or installed. No token or secret was read or printed.
>
> ## Why it stopped
>
> 1. **No ledger row.** `grep -n "Q37[0-9]" docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` finds only Q370;
>    the file ends at line 473 (Q370). SOP v2.5 §20.1 / R8: a WP whose row is missing stops. Every earlier dispatch
>    stated "Ledger row EXISTS"; this one does not.
> 2. **The reading tool is not installed.** Part B requires `dotnet-counters`; `which dotnet-counters` finds nothing and
>    `~/.dotnet/tools` holds nothing. Installing it (`dotnet tool install --global dotnet-counters`) downloads a package
>    from NuGet, which needs the owner's explicit permission in this environment.
>
> ## CT's four measured premises, checked (read-only)
>
> | # | premise | verdict | evidence |
> |---|---|---|---|
> | 1 | Diten.Web has no Serilog; it uses the default ASP.NET ILogger | **TRUE** | `grep -ci serilog frontend/Diten.Web/Diten.Web.csproj` = 0; the only PackageReference is `System.IdentityModel.Tokens.Jwt`; `Program.cs:107` already calls `AddHttpContextAccessor()`; `appsettings.json:2-7` sets only `Logging:LogLevel` (console scopes not enabled) |
> | 2 | the controller forwards the id when present (:171), mints one when absent (:218-220), copies the gateway's back (:211-213) | **PARTLY FALSE** | `:171` does not forward-when-present: `TryForwardUuidHeader` failing returns **400** locally (`:171-172`), so a request without the header never reaches the gateway. `:218-220` mints an id only inside `ContractFailure` (error path), not for a normal request. `:211-213` copy-back is accurate |
> | 3 | one ILogger, exactly four log calls at :130, :143, :148, :153, all warning/error, none with the id, nothing on success | **TRUE** | `grep -n "_logger\." …SupplyChainShipmentsController.cs` → `:130` LogWarning, `:143` LogWarning, `:148` LogError, `:153` LogError; templates carry StatusCode/TargetUrl only |
> | 4 | the three Shipments scripts do NOT send the header; they only read `correlationId` from error bodies | **FALSE** | all three send it: `index.js:10` (`getAuthHeaders` → `'X-Correlation-Id': uuid()`, `uuid` = `crypto.randomUUID`, `:9`), `create.js:76` (POST), `details.js:108` and `:131` (GET and commands). They also read it back from error bodies (`create.js:65`, `index.js:43`, `details.js:53`) |
>
> ## What the corrected premises change for part A
>
> - Premises 2 and 4 together mean every browser request through these scripts **already carries one UUID** that the
>   Web controller **requires** and forwards to the gateway. A success path therefore always has an id to log; the Web
>   only fails to write it.
> - With no Serilog, the smallest candidates are: (a) put `{CorrelationId}` into the controller's own message templates
>   and add one Information line on success; or (b) `ILogger.BeginScope` around the proxy call plus console
>   `IncludeScopes` — which needs an `appsettings` change, outside this WP's named single-writer files. Neither adds a
>   dependency. Not chosen or implemented: the WP stopped first.
>
> ## Needed to proceed
>
> - A CT-QUEUE row for Q372 (and Q371, which the dispatch sequences after it).
> - Owner permission to install `dotnet-counters` from NuGet, or a ruling that part B may read the meter another way
>   without registering an exporter.
>
> Agent PASS ≠ CT ACCEPTED. Nothing committed, nothing pushed, nothing staged.
>
> ## Owner answers (in the Code session, 2026-10-03)
>
> - Ledger row: **"Dur, satır gelsin"** — Q372 stays STOPPED until CT adds the row and re-dispatches.
> - `dotnet-counters`: **install permitted** ("Evet, kur"). Not installed now, because the WP is stopped; the permission
>   is recorded here for the re-dispatch.
