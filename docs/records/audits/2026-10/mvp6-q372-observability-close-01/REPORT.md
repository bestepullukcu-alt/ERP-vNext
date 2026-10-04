# Q372 — O-1..O-4 close attempt: STOPPED before any change

Measured 2026-10-03 19:56 UTC, branch `feature/mvp6-logistics` @ `c1f2dffe8`, 5 porcelain lines, 0 staged.
Nothing was edited, built, started or installed. No token or secret was read or printed.

## Why it stopped

1. **No ledger row.** `grep -n "Q37[0-9]" docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` finds only Q370;
   the file ends at line 473 (Q370). SOP v2.5 §20.1 / R8: a WP whose row is missing stops. Every earlier dispatch
   stated "Ledger row EXISTS"; this one does not.
2. **The reading tool is not installed.** Part B requires `dotnet-counters`; `which dotnet-counters` finds nothing and
   `~/.dotnet/tools` holds nothing. Installing it (`dotnet tool install --global dotnet-counters`) downloads a package
   from NuGet, which needs the owner's explicit permission in this environment.

## CT's four measured premises, checked (read-only)

| # | premise | verdict | evidence |
|---|---|---|---|
| 1 | Diten.Web has no Serilog; it uses the default ASP.NET ILogger | **TRUE** | `grep -ci serilog frontend/Diten.Web/Diten.Web.csproj` = 0; the only PackageReference is `System.IdentityModel.Tokens.Jwt`; `Program.cs:107` already calls `AddHttpContextAccessor()`; `appsettings.json:2-7` sets only `Logging:LogLevel` (console scopes not enabled) |
| 2 | the controller forwards the id when present (:171), mints one when absent (:218-220), copies the gateway's back (:211-213) | **PARTLY FALSE** | `:171` does not forward-when-present: `TryForwardUuidHeader` failing returns **400** locally (`:171-172`), so a request without the header never reaches the gateway. `:218-220` mints an id only inside `ContractFailure` (error path), not for a normal request. `:211-213` copy-back is accurate |
| 3 | one ILogger, exactly four log calls at :130, :143, :148, :153, all warning/error, none with the id, nothing on success | **TRUE** | `grep -n "_logger\." …SupplyChainShipmentsController.cs` → `:130` LogWarning, `:143` LogWarning, `:148` LogError, `:153` LogError; templates carry StatusCode/TargetUrl only |
| 4 | the three Shipments scripts do NOT send the header; they only read `correlationId` from error bodies | **FALSE** | all three send it: `index.js:10` (`getAuthHeaders` → `'X-Correlation-Id': uuid()`, `uuid` = `crypto.randomUUID`, `:9`), `create.js:76` (POST), `details.js:108` and `:131` (GET and commands). They also read it back from error bodies (`create.js:65`, `index.js:43`, `details.js:53`) |

## What the corrected premises change for part A

- Premises 2 and 4 together mean every browser request through these scripts **already carries one UUID** that the
  Web controller **requires** and forwards to the gateway. A success path therefore always has an id to log; the Web
  only fails to write it.
- With no Serilog, the smallest candidates are: (a) put `{CorrelationId}` into the controller's own message templates
  and add one Information line on success; or (b) `ILogger.BeginScope` around the proxy call plus console
  `IncludeScopes` — which needs an `appsettings` change, outside this WP's named single-writer files. Neither adds a
  dependency. Not chosen or implemented: the WP stopped first.

## Needed to proceed

- A CT-QUEUE row for Q372 (and Q371, which the dispatch sequences after it).
- Owner permission to install `dotnet-counters` from NuGet, or a ruling that part B may read the meter another way
  without registering an exporter.

Agent PASS ≠ CT ACCEPTED. Nothing committed, nothing pushed, nothing staged.

## Owner answers (in the Code session, 2026-10-03)

- Ledger row: **"Dur, satır gelsin"** — Q372 stays STOPPED until CT adds the row and re-dispatches.
- `dotnet-counters`: **install permitted** ("Evet, kur"). Not installed now, because the WP is stopped; the permission
  is recorded here for the re-dispatch.
