# verify from: shasum -a 256 -c ARTIFACTS.sha256 (this folder); then rebuild with evidence/probe.sh against the Program.cs bytes named in evidence/installed-*.txt

# Q381 — routes of uncomposed modules answer 404, not 500

| Field | Value |
|---|---|
| Lane / agent | Q381 · integration-agent · single writer on `SupplyChainService.Api/Program.cs` |
| Authority | owner decision 2026-10-04 §4 (`docs/records/decisions/2026-10/mvp6-five-owner-decisions-01.md:45-57`) > MOD-0183 > AGENTS.md |
| Placement | Claude app → Code tab → Local, Darwin. G2 placement waiver applied — Cowork withdrawn by owner. |
| Read first | `AGENTS.md`, `.antigravity/rules/git-safety.md`, `.antigravity/rules/code-style.md`, `.antigravity/agents/integration-agent.md`, `Program.cs` composition comment, decision §4 |
| Branch / HEAD | `feature/mvp6-logistics` @ `c1f2dffe8`; no `.git/index.lock`; staged 0 at start and end |
| Start / end (Europe/Istanbul) | 2026-10-04 12:28:53 +03 / 13:12 +03 |
| Verdict | **DONE in the tree, unstaged, uncommitted.** Agent verdict ≠ CT ACCEPTED. |

## 1. What the two routes returned before the change (measured, not inferred)

Service started from a hash-verified copy of the tree (442 files, manifest `da13db5e…0af8`). Development, port
58381, own mongod 57381 (`rsq381`, `enableTestCommands=1`). `Program.cs` = `7e901501…251e` (the tree's bytes).
The token was minted per run against a per-run random secret (never printed), with the read/create permissions
of both modules plus `supplychain.shipments.read`.

| Request | Status | Body |
|---|---|---|
| GET `/api/supply-chain/capacity-plans/{id}`, no token | 401 | empty |
| GET same, token | **500** | `System.InvalidOperationException: Unable to resolve service for type '…CapacityPlans.CapacityRequestContext' while …` |
| POST `/api/supply-chain/capacity-plans`, no token | 401 | empty |
| POST same, token | **500** | same activator exception |
| GET `/api/supply-chain/sandop-plans/{id}`, no token | 401 | empty |
| GET same, token | **500** | `System.InvalidOperationException: No service for type 'MediatR.IRequestHandler`2[…SandopPlans.Queries.GetSandopPlanQuery…` |
| POST `/api/supply-chain/sandop-plans`, no token | 401 | empty |
| POST same, token | **500** | `No service for type 'MediatR.IRequestHandler`2[…SandopPlans.Commands.CreateSandopPlanCommand…` |
| GET `/api/shipment-bundle/shipments`, token | 200 | `{"items":[],"page":1,"pageSize":50,"total":0,"contractVersion":"v1"}` |

CT's inference is confirmed for authenticated callers, with the two failure points CT described:
- Capacity fails in the controller activator.
- S&OP fails later, in MediatR.

Unauthenticated callers got 401, because `[Authorize]` is evaluated before the controller is built — the module
looked present but locked. In Development the 500 body is the developer exception text, which names internal types.
Four unhandled exceptions were logged for the four authenticated calls. Full output: `evidence/probes-RED.txt`.

## 2. The change

`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`, sha256 `f2476380…ab64`. Only this
file was changed: 51 lines added, 1 changed.

- `:100-104` — `AddControllers()` gains
  `.ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new ComposedFeatureControllerFilter(builder.Services)))`.
- `:140-180` — comment `:140-145`, then `file sealed class ComposedFeatureControllerFilter : IApplicationFeatureProvider<ControllerFeature>`.
  - It removes a controller under `*.Features.<Module>` from the MVC controller feature, so `MapControllers()`
    never maps it, unless both conditions below hold:
    - **(a)** MediatR registered an `IRequestHandler<,>` / `IRequestHandler<>` for at least one request type of that
      module;
    - **(b)** every `Diten.*` constructor parameter has a registration.
  - Controllers outside `Features` (Shipments) are always kept.
- `:29-30`, `:32-33` — four `using` lines.

**One place for the fact (K6).** The filter holds no module names and no routes. It reads the `IServiceCollection`
that `Program.cs` and `AddApplication()` built: the persistence calls in `Program.cs` decide (b), and MediatR's
registrations decide (a). Composing Capacity or S&OP later (Q273) maps their controllers again with no edit here.

What it does not change: the MediatR exclusion list in `Application/DependencyInjection.cs:15-25` still exists and
still mirrors `Program.cs` by comment. That is a pre-existing second statement of the same fact. The filter reads
its *result* (which handlers are registered), so it adds no third list.

Build: 0 warnings, 0 errors.

## 3. After the change (GREEN), sabotage, restore

| Request | RED | GREEN | SABOTAGE (`7e901501…`) | RESTORED (`f2476380…`) |
|---|---|---|---|---|
| Capacity GET, no token | 401 | **404** | 401 | **404** |
| Capacity GET, token | 500 | **404** | 500 | **404** |
| Capacity POST, no token | 401 | **404** | 401 | **404** |
| Capacity POST, token | 500 | **404** | 500 | **404** |
| S&OP GET, no token | 401 | **404** | 401 | **404** |
| S&OP GET, token | 500 | **404** | 500 | **404** |
| S&OP POST, no token | 401 | **404** | 401 | **404** |
| S&OP POST, token | 500 | **404** | 500 | **404** |
| Shipments GET list, token | 200 | 200 | 200 | 200 |
| Shipments GET list, no token | 401 | 401 | 401 | 401 |
| Carriers GET, token (no carrier permission) | — | 403 | 403 | 403 |
| Loads / Returns / Claims GET, no token | — | 401 / 401 / 401 | 401 / 401 / 401 | 401 / 401 / 401 |
| `/api/supply-chain/nothing-here` (never existed) | — | 404 | 404 | 404 |
| Unhandled exceptions logged | 4 | 0 | 4 | 0 |

The 404 for Capacity and S&OP has an empty body, byte-identical to a route that never existed. It answers before
authentication, so an unauthenticated caller can no longer tell that the module exists.

The other five modules answer exactly as in the sabotage run, which proves the exclusion is narrow. The Carriers,
Loads, Returns, Claims and unknown-route rows were added after the RED run; the sabotage run is RED's bytes and
supplies their "before". Full output: `evidence/probes-*.txt`; the probe script is `evidence/probe.sh`.

## 4. Suite

SupplyChain tests built on the GREEN copy. Every module URI and `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI` were
pointed at 57381; restart variables unset.

| Run | Passed | Failed | Total | Failures |
|---|---:|---:|---:|---|
| 1 | 429 | 6 | 435 | the Claims restart test, plus 5 `FileNotFoundException`s from `RepositoryFile.cs:14`: my copy held only `services/`, and those tests read `docs/analysis/contracts/shipment-bundle.openapi.yaml` and `MOD-0183-shipment-tracking-pod.md` |
| 2 (both files placed in the copy, `cmp`-identical to the tree) | **434** | **1** | **435** | `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` — "Explicit write/read restart mode required", by design |

Run 2 is inside the gate. Its one failure is the known one, and Q361's intermittent assertion passed this run.
`CompositionRootGuardTests`: both tests passed. trx sha256: run 1 `e756a745…6c2c`, run 2 `41a6e560…8917`.

## 5. Findings

- **F-Q381-1:** CT's "500 for both" holds only for authenticated callers. Unauthenticated callers got 401, which
  advertised the module as present. Evidence: `evidence/probes-RED.txt`.
- **F-Q381-2:** In Development the 500 body was the developer exception text naming internal types
  (`CapacityRequestContext`, the MediatR handler type). Evidence: `evidence/probes-RED.txt`, rows `cap-*-auth` and
  `sop-*-auth`.
- **F-Q381-3 (Q366 guard):** The `KnownUncomposed` exception for `CapacityPlansController`
  (`CompositionRootGuardTests.cs:44-47`) is **still required**. The guard enumerates controllers by reflection over
  the assembly (`:97-99`), not over the mapped set, so it still sees the unconstructible controller. The guard passes
  with the entry, and the entry is not stale. Its comment, "MapControllers() still maps its controller, so its routes
  exist and cannot be served" (`:41-42`), **is no longer true** after this change. Not edited — the test is not this
  lane's, and the entry is the Q273 marker.
- **F-Q381-4:** The guard does not cover `SandopPlansController`. Its only constructor parameter, `ISender`, is
  registered; its failure was in MediatR, which a constructor check cannot see. This filter covers it through
  condition (a).
- **F-Q381-5:** The suite total is 435 results (434 unique names), not the 433 in the dispatch's gate. The tree has
  grown by two results since the gate was set. Evidence: `evidence/suite-run2.txt`. Not a regression: no failure
  other than the by-design one.
- **F-Q381-6:** The dispatch cites `Program.cs:73-83` for the Capacity/S&OP comment. At start it was `:74-84`; after
  this change it is `:78-88`.
- **F-Q381-7:** Two statements of "which modules are composed" remain: `Program.cs` (persistence calls) and the
  MediatR exclusion list in `Application/DependencyInjection.cs:15-25`. This change reads the second's outcome rather
  than adding a third list, but the first two can still drift from each other (K6).

## 6. Not done

- No stage, no commit.
- Capacity and S&OP are not composed (Q273 open).
- Q361's `ShipmentTelemetryTests.cs`, `CompositionRootGuardTests.cs` and `frontend/**` were not touched.
- The scratch copy `~/mvp6-env/q381-20261004-1230/` stays (no `rm`). My mongod is stopped; 57381 and 58381 are closed.

Return to CT; CT decides.
