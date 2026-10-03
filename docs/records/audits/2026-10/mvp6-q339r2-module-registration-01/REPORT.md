# Q339-R2 — SupplyChain self-registration, Shipment provider only

- Lane: Q339-R2 (integration-agent), supersedes Q339. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 16:17:43 UTC 2026` · `feature/mvp6-logistics` · HEAD `8f60dc6d3` · dirty 679 · staged 0.
  After the work: dirty 680 (+1 = this record folder), staged 0.
- Step 0 read: `AGENTS.md` (sha256/16 `ce8c12ad80f93bb5`), `.antigravity/rules/git-safety.md` (`4181c697b96dc9f8`),
  `.antigravity/rules/code-style.md` (`6032fbb07819dc47`), `.antigravity/agents/integration-agent.md`
  (`f760471e69efb7dc`), MOD-0183 pack §22 (pack `2285fe0d45dfbce5…`, unchanged by this lane).
- **Agent verdict: DONE, all four proofs green. Agent verdict ≠ CT ACCEPTED.**
- Nothing staged, nothing committed. No secret value appears in any output: every file under `evidence/` was scanned
  for the internal key and for the throwaway JWT secret — 0 files contain either.

## What changed

| file | tracked? | change | sha256 after |
|---|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | yes (already modified by other lanes, +64/−2 before this lane) | +1 using, +6 comment lines, +4 registration lines after `AddHostedService<ShipmentOutboxWorker>()` | `7e9015146bdd8343e371f661fedbdef622583383a52fe05f4f792a9d106e251e` |
| `…Api/appsettings.Development.json` | no (`.gitignore:25`) | + `PlatformRegistration` { BaseUrl `http://localhost:5057`, InternalApiKey = MDM's: **len 45, sha256/8 `e96ab27d`** } | `a0ddb04dfa0dd6db07efc54359094010e0d7b4fc0b52b727cd645f5185977599` |
| `…Api/appsettings.Development.example.json` | yes | + `PlatformRegistration` { BaseUrl `http://localhost:5057`, InternalApiKey `""` } (+4 lines) | `02aef19dd9c8e585278a758325a31e7a1b5152cab792510aed7184cb5e9e6809` |

Pre-edit sha256: `Program.cs` `0627c5dd6446cb7c…`; both Development files `a00f93348444d509…` (they were identical).

The Program.cs diff against the pre-edit copy:

```
21a22
> using Diten.SupplyChainService.Api.ModuleRegistration;
84a86,95
> // Q339-R2 (2026-10-03): ShipmentTrackingPodManifestProvider and ModuleRegistrationHostedService were
> // written and tested, but nothing composed them, so Shipment never reached the Platform module catalog.
> // These four lines follow MdmService Program.cs:88-92. They make the hosted service push the one Shipment
> // manifest to Platform after startup, best-effort: blank config logs a warning and returns, and a dead
> // Platform is retried 5 times and then logged. The API serves either way (measured, Q339-R2 P1-P3).
> // Carrier is absent on purpose: MOD-0184:473 forbids its AddSingleton until Carrier has a UI (none exists).
> builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
> builder.Services.AddHttpClient();
> builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();
> builder.Services.AddHostedService<ModuleRegistrationHostedService>();
```

No Carrier registration. No Capacity or S&OP composition. Nothing under `frontend/**` was touched.

## Step C — boot behaviour, left as it is

`ModuleRegistrationHostedService.cs` handles three cases:

- Blank BaseUrl or blank key: it logs a warning and returns (`:44-48`).
- Platform unreachable: 5 attempts with 2/4/8/16 s backoff, then a "gave up" warning (`:62-75`, `:136-145`).
- Any other failure: logged per module, never rethrown (`:82-86`).

All three paths were measured below (P1, P3b, P3c).

**Without step B, P2 would show no outbound call at all.** The base `appsettings.json` has BaseUrl
`http://localhost:5057` and an empty key, so the service takes the `:44-48` skip path. P3c reproduces exactly that
state: key blank, 0 requests, the skip warning in the log.

## How it was run

- Isolated environment `~/mvp6-env/q339r2-20261003-1919/`, a new timestamped folder. It holds a scratch copy of
  `services/Diten.SupplyChainService` and `services/Diten.Building.Blocks`, plus the contract and the MOD-0183 pack,
  which the tests read.
- Copy check: 443 files, 0 mismatches against the repository (`evidence/copy-manifest.sha256`). Nothing was built
  inside the repository.
- mongod: single-node replica set `rsq339` on **57339**, used only by this lane, with test commands on. It was shut
  down at the end, and 57339 was free again.
- Service on **58339**. Stand-in Platform on **58340**: `evidence/capture.py` records the method, the path and the
  header **names**, and the internal key only as length plus sha256/8. Dead port **59339**, confirmed closed.
  Platform 5057 is not running in this lane.
- JWT: a throwaway secret (`openssl rand -base64 48`, 64 bytes), file mode 600, read by the harness, never printed.
  Issuer and audience are both `Diten`.
- Boot harness `evidence/boot.sh` runs `ASPNETCORE_ENVIRONMENT=Development` from the copy's Api folder, so the copied
  `appsettings.Development.json` (with step B) is loaded. It waits for "Now listening", probes `GET /health`, then
  stops **only its own pid**.

## P1 — three boots in Development, config as written (BaseUrl 5057, Platform down)

```
== p1-boot1 pid=65420 now_listening=yes after_s=3
== p1-boot1 GET /health -> 200
== p1-boot1 alive_before_stop=yes
== p1-boot1 stopped; 58339 free
== p1-boot2 pid=65450 now_listening=yes after_s=1
== p1-boot2 GET /health -> 200
== p1-boot2 alive_before_stop=yes
== p1-boot2 stopped; 58339 free
== p1-boot3 pid=65473 now_listening=yes after_s=1
== p1-boot3 GET /health -> 200
== p1-boot3 alive_before_stop=yes
== p1-boot3 stopped; 58339 free
--- p1-boot1.log (lifetime + registration lines)
[19:20:12 INF] Now listening on: http://127.0.0.1:58339 CorrelationId= TenantId= LegalEntityId=
[19:20:12 INF] Application started. Press Ctrl+C to shut down. CorrelationId= TenantId= LegalEntityId=
[19:20:12 INF] Hosting environment: Development CorrelationId= TenantId= LegalEntityId=
[19:20:12 WRN] Module self-registration attempt 1 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:20:14 WRN] Module self-registration attempt 2 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
--- p1-boot2.log (lifetime + registration lines)
[19:20:17 WRN] Module self-registration attempt 1 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:20:17 INF] Now listening on: http://127.0.0.1:58339 CorrelationId= TenantId= LegalEntityId=
[19:20:17 INF] Application started. Press Ctrl+C to shut down. CorrelationId= TenantId= LegalEntityId=
[19:20:17 INF] Hosting environment: Development CorrelationId= TenantId= LegalEntityId=
[19:20:19 WRN] Module self-registration attempt 2 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
--- p1-boot3.log (lifetime + registration lines)
[19:20:22 WRN] Module self-registration attempt 1 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:20:22 INF] Now listening on: http://127.0.0.1:58339 CorrelationId= TenantId= LegalEntityId=
[19:20:22 INF] Application started. Press Ctrl+C to shut down. CorrelationId= TenantId= LegalEntityId=
[19:20:22 INF] Hosting environment: Development CorrelationId= TenantId= LegalEntityId=
[19:20:24 WRN] Module self-registration attempt 2 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
```

3/3 reached "Now listening" and 3/3 answered `/health` 200. Each boot also shows the hosted service trying to reach
Platform at 5057.

## P2 — the hosted service called Platform (stand-in on 58340; BaseUrl overridden by env for this boot only)

```
== p2 pid=65571 now_listening=yes after_s=1
== p2 GET /health -> 200
== p2 alive_before_stop=yes
== p2 stopped; 58339 free
--- requests received by stand-in Platform on 58340: 1
{"at": "16:20:53", "method": "POST", "path": "/api/internal/module-catalog/register-manifest", "header_names": ["Content-Type", "Host", "Transfer-Encoding", "X-Internal-Api-Key"], "X-Internal-Api-Key": "<redacted len=45 sha8=e96ab27d>", "moduleCode": "shipment-tracking-pod", "pages": ["SHIPMENTS", "SHIPMENT_CREATE", "SHIPMENT_DETAILS"], "body_bytes": 1920}
--- service log
[19:20:53 INF] Module manifest self-registered with Platform. ModuleCode=shipment-tracking-pod Attempt=1 CorrelationId= TenantId= LegalEntityId=
[19:20:53 INF] Now listening on: http://127.0.0.1:58339 CorrelationId= TenantId= LegalEntityId=
```

Exactly **one** manifest was received: `shipment-tracking-pod`, with its three §22 pages. The header name
`X-Internal-Api-Key` is visible and its value is redacted. The key that arrived is step B's key (len 45, `e96ab27d`).

The first P2 attempt also received 1 POST, but the listener did not decode the chunked body (`moduleCode: <unparsed>`,
`body_bytes: 0`). The listener was fixed and P2 was re-run. Both runs are reported; only the second run's output is
kept in `evidence/p2*.txt`.

## P3 — sabotage

### (a) the one `AddSingleton` removed → zero manifests, service still boots

The sabotage was made in a separate copy (`src-sab-a/`, made by `cp -R`), never in the repository:

```
94:builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();
after sabotage: AddSingleton lines=0 ; AddHostedService<ModuleRegistrationHostedService>=1
94d93
< builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();
sab-a build rc=0
    0 Error(s)
== p3a pid=65685 now_listening=yes after_s=1
== p3a GET /health -> 200
== p3a alive_before_stop=yes
== p3a stopped; 58339 free
--- requests received by stand-in Platform on 58340: 0
--- service log, registration lines: 0
[19:21:25 INF] Now listening on: http://127.0.0.1:58339 CorrelationId= TenantId= LegalEntityId=
```

### (b) BaseUrl → dead port 59339 → boot succeeds, /health 200 (probed after the give-up)

```
59339 closed (dead port)
== p3b pid=65742 now_listening=yes after_s=1
== p3b GET /health -> 200
== p3b alive_before_stop=yes
== p3b stopped; 58339 free
[19:21:39 INF] Now listening on: http://127.0.0.1:58339 CorrelationId= TenantId= LegalEntityId=
[19:21:39 WRN] Module self-registration attempt 1 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:21:41 WRN] Module self-registration attempt 2 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:21:45 WRN] Module self-registration attempt 3 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:21:53 WRN] Module self-registration attempt 4 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:22:09 WRN] Module self-registration attempt 5 could not reach Platform; will retry. ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
[19:22:09 WRN] Module self-registration gave up after 5 attempts (Platform unreachable?). ModuleCode=shipment-tracking-pod CorrelationId= TenantId= LegalEntityId=
```

### (c) InternalApiKey blanked (env override to empty) → skip warning at :44-48, boot succeeds

```
== p3c pid=65791 now_listening=yes after_s=1
== p3c GET /health -> 200
== p3c alive_before_stop=yes
== p3c stopped; 58339 free
--- requests received by stand-in Platform on 58340: 0
[19:22:23 WRN] Module self-registration skipped: PlatformRegistration BaseUrl/InternalApiKey not configured. CorrelationId= TenantId= LegalEntityId=
```

**K3 reading:** with the line present, 1 manifest (P2). With it absent, 0 (P3a). So the registration line is what
produces the call. P1, P3b and P3c show that the API serves whether Platform is down, dead or unconfigured.

## P4 — SupplyChain suite vs Q335 baseline (432 / 1 / 433)

The script is Q335's `run-modules.sh` with only the port and the replica-set name changed (diff below). Each module
was run with only its own variable: `MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`,
`RETURNS_MONGO_URI`, `CLAIMS_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `MVP6_MOD0192_MONGO_URI`, all pointing at this
lane's 57339.

```
4c4
< URI='mongodb://127.0.0.1:57335/?replicaSet=rsq335'
---
> URI='mongodb://127.0.0.1:57339/?replicaSet=rsq339'
== build rc=0 seconds=5
    0 Warning(s)
    0 Error(s)
== listed tests: 433
== Shipments rc=0 seconds=10
Passed!  - Failed:     0, Passed:    90, Skipped:     0, Total:    90, Duration: 8 s - Diten.SupplyChainService.Tests.dll (net8.0)
== Carriers rc=0 seconds=7
Passed!  - Failed:     0, Passed:    36, Skipped:     0, Total:    36, Duration: 5 s - Diten.SupplyChainService.Tests.dll (net8.0)
== Loads rc=0 seconds=50
Passed!  - Failed:     0, Passed:    33, Skipped:     0, Total:    33, Duration: 46 s - Diten.SupplyChainService.Tests.dll (net8.0)
== Returns rc=0 seconds=82
Passed!  - Failed:     0, Passed:    78, Skipped:     0, Total:    78, Duration: 1 m 20 s - Diten.SupplyChainService.Tests.dll (net8.0)
== Claims rc=1 seconds=3
Failed!  - Failed:     1, Passed:   128, Skipped:     0, Total:   129, Duration: 989 ms - Diten.SupplyChainService.Tests.dll (net8.0)
== SandopPlans rc=0 seconds=22
Passed!  - Failed:     0, Passed:    19, Skipped:     0, Total:    19, Duration: 20 s - Diten.SupplyChainService.Tests.dll (net8.0)
== CapacityPlans rc=0 seconds=131
Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 2 m 8 s - Diten.SupplyChainService.Tests.dll (net8.0)
wall=311s
  Failed Diten.SupplyChainService.Tests.Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery [1 ms]
  Error Message:
   System.InvalidOperationException : Explicit write/read restart mode required; exclude from ordinary suite.
```

| module | Q335 | Q339-R2 | delta |
|---|---|---|---|
| Shipments | 90/0/90 | 90/0/90 | 0 |
| Carriers | 36/0/36 | 36/0/36 | 0 |
| Loads | 33/0/33 | 33/0/33 | 0 |
| Returns | 78/0/78 | 78/0/78 | 0 |
| Claims | 128/1/129 | 128/1/129 | 0 — same test, same message (restart-mode, by design) |
| SandopPlans | 19/0/19 | 19/0/19 | 0 |
| CapacityPlans | 48/0/48 | 48/0/48 | 0 |
| **TOTAL** | **432/1/433** | **432/1/433** | **none** |

**Delta: none.** The listed test count is 433 in both runs.

Why the suite is unaffected, as far as static reading shows:

- 5 test files match `WebApplicationFactory<Program>`. Five files call `UseEnvironment("Testing")`: ShipmentTests,
  SourceIntakeTests, ShipmentTelemetryTests, LoadContractTests and CarrierContractTests.
- No `appsettings.Testing.json` exists, so those hosts load the base `appsettings.json` (key empty). The hosted service
  then takes the `:44-48` skip path.
- This was **not measured at runtime**. `dotnet test` does not keep console output for passing tests, and the test
  logs hold 0 self-registration lines either way.
- If any test host runs as Development, it would make background attempts to 5057. They are refused and never
  rethrown, so they cannot change a test outcome.
- The only registration test, `Carriers/CarrierModuleRegistrationHostedServiceTests.cs`, exercises the hosted service
  directly, not through `Program.cs`. No test exercises the Shipment registration composed here, and this lane added
  none, because none was asked for.

## The facts in this dispatch

| # | fact | verdict | note |
|---|---|---|---|
| 1 | Carrier is OUT; MOD-0184:473 forbids its AddSingleton until it has a UI | CONFIRMED | `:473` ship rule; 0 Carrier files under `frontend/**` (Q339) |
| 2 | Q339's refusal upheld as Q344 | NOT VERIFIED | ledger row not read by this lane |
| 3 | MOD-0183:572 carries the identical rule | CONFIRMED | `MOD-0183-shipment-tracking-pod.md:572` |
| 4 | `SupplyChainShipmentsController.cs` exists | CONFIRMED | also untracked (`git ls-files` → 0), which the dispatch did not list |
| 5 | all three manifest routes have views on disk | CONFIRMED | `[Route("SupplyChain/Shipments")]` at `:12`; `[HttpGet("")]` `:39` → `Index.cshtml`; `"Create"` `:42` → `Create.cshtml`; `"Details/{shipmentId:guid}"` `:45` → `Details.cshtml`. The dispatch wrote `/Details/{id}`; the route and the manifest both read `{shipmentId:guid}` |
| 6 | HEAD `appsettings.json` has no `PlatformRegistration` section | CONFIRMED | (Q339 F-Q339-2) |
| 7 | suite baseline at `docs/records/audits/2026-10/mvp6-q335-suite-baseline-01/`, 432/1/433 | CONFIRMED | `PER-MODULE.tsv` TOTAL 432/1/433; its `ARTIFACTS.sha256` verifies |
| 8 | every file under `Api/ModuleRegistration` and `Views/SupplyChain/Shipments` is untracked | CONFIRMED | `git ls-files` → 0 and 0 |
| 9 | MDM key length 45, sha256 `e96ab27d` | CONFIRMED | reused; the SupplyChain Development file now carries len 45, sha256/8 `e96ab27d` |
| 10 | MDM `Program.cs:88-92` shape | CONFIRMED | (Q339) |
| 11 | boot behaviour `:44-48`, 5 retries at 2/4/8/16 s, nothing rethrown | CONFIRMED | measured: P3c (skip), P3b (timestamps +2/+4/+8/+16 s, then "gave up") |
| 12 | CT's earlier Q339 record path was invented (Q345) | CONFIRMED | the Q339 report now sits at `docs/records/audits/2026-10/mvp6-q339-module-registration-01/`; `docs/records/2026-10/` no longer exists. This lane did not move it |

## Findings

- **F-Q339R2-1** No test covers the composed Shipment registration (see P4). The K3 evidence for these lines lives
  only in this record. A first draft of this report claimed that every test host runs outside Development; that was
  corrected before publication to what static reading supports.
- **F-Q339R2-2** `SupplyChainShipmentsController.cs` is untracked as well. The manifest's routes depend on it, so it
  must ship in the same commit as the four lines (MOD-0183:572).
- **F-Q339R2-3** `Program.cs` already carried +64/−2 from other lanes before this edit. After it: +75/−2 vs HEAD.
  The owner's commit will mix this lane's 11 lines with those.
- **F-Q339R2-4** The internal key now exists in two untracked files, MDM and SupplyChain, as the same value. It is
  local-dev only and gitignored, but it is shared, so rotating one means rotating both.

## Not done

- No ledger row read (fact 2).
- No real Platform on 5057 was started. P2 used a stand-in that records only what it received; Platform's own
  reconcile of the manifest is not measured here.

Nothing committed, nothing pushed, nothing staged.
