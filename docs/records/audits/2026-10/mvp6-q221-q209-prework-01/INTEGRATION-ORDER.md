# Q221 — Integration order for Q209 (proposal; nothing applied)

Each step names what it changes, who owns it, and what it waits for. "Before" means the step must be in the tree first,
or the next step cannot compile or cannot be verified. Static reasoning only (E1): no step was tried.

## Steps

| # | Step | Owner | Must land before | Waits for |
|---|---|---|---|---|
| 0 | **Decisions.** OD-1 and OD-2 (executor), OD-3 (S&OP indexes), OD-4 (reference URLs). OD-5 and OD-6 can be answered in parallel | CT + the owners in `OPEN-DECISIONS.tsv` | steps 2-4 | — |
| 1 | **Already in the tree — no action.** The Returns, Claims, S&OP and Capacity sources (Q202a), the registration foundation under `Api/ModuleRegistration/` (5 files), and the Api `.csproj` (equal to the BASE member, already references `ModuleRegistration.Abstractions`) | — | step 3 | — |
| 2 | **Module-side prerequisites, inside owned paths.** (a) Q220's change to `AddSandopPersistence()` if it adds index creation (OD-3). (b) Only if CT answers OD-1 or OD-2 with "fix first": the executor change in the Capacity paths | Q220 lane; Capacity DEV lane | step 3 | step 0 |
| 3 | **`Program.cs` → P0** (BASE verbatim, 123 lines, sha256 `33027bcd…`). One file, one writer. Then a build and one start of the service in `Development` (OD-11), and the SupplyChain test suite | Q209 integration owner (single writer, SR-D4) | steps 4-6 | steps 0-2 |
| 4 | **Service configuration:** `Returns:ReferenceBaseUrl`, `Claims:ReferenceBaseUrl` (OD-4). Without them Returns create answers 503 `DEPENDENCY_UNAVAILABLE` and Claims answers 503 `CLAIM_REFERENCE_UNAVAILABLE`; nothing else breaks | integration owner (shared `appsettings.json` / environment) | any Returns or Claims run-time check | step 0 (OD-4); if the value is the Gateway, step 5 |
| 5 | **Gateway.** In one integration-agent change: the BASE `ocelot.json` (CRM 35 routes 5061 → 5065, 6 supply-chain routes), the BASE `OcelotConfigurationTests.cs` (it carries the port set and the count guard), the fragment routes for the modules being released, and the guard number that results (`OCELOT-PROPOSAL.md` §6) | **integration-agent only** (`AGENTS.md:108`) | any request through port 5000 | step 3 for the supply-chain routes to be useful. The CRM port move does not depend on step 3 — see "What can go independently" |
| 6 | **Per module, with its UI:** provider file + its `AddSingleton` line (`Program.cs` P1 lines 90-93) + the 2 nav keys × 7 languages, in one change (W-03; DCP-009 §21.4) | integration owner; UI writer for the draft | the module's sidebar entry and permission self-registration | Q202b release for that module (HELD); step 3 |
| 7 | **Permissions.** Keys reach the catalogue by self-registration (needs step 6 and a configured `PlatformRegistration:InternalApiKey`) or by a seed (OD-12). Roles after that | platform owner | any real user reaching a module | step 6 or a seed decision |
| 8 | **Run-time verification** per module on the integrated tree (T2 Mac lane) | CT dispatch | — | steps 3-7 for that module |

## What must land before `Program.cs` is patched

1. The answers to **OD-1 / OD-2**. Line 77 starts a background worker at every start of the service. It is the only
   proposed line that begins doing work on its own.
2. **OD-3 / Q220.** Line 73 calls `AddSandopPersistence()`. Whatever Q220 leaves in that method is what start-up will run.
   If Q220 is still open when `Program.cs` is patched, S&OP is wired without its unique indexes: the three invariants of
   F-Q212-4 are then unprotected on a reachable endpoint, which is worse than today (unreachable).
3. Nothing else. Every type P0 names is in the tree (step 1).

## What can follow

- Step 4 (configuration), step 5 (gateway), step 6 (provider lines and nav keys), step 7 (permissions), in that order
  per module.
- The four provider lines (P1) are independent of each other. A module whose UI is not released keeps its line out.

## What can go independently of `Program.cs`

- **The CRM port move** in `ocelot.json` (35 routes, 5061 → 5065). It corrects the gateway against `AGENTS.md:86-87`
  and touches no supply-chain code. It depends on the CRM service actually listening on 5065, which this lane did not
  check (outside the scope; the CRM `Program.cs` comment change is listed in `SHARED-SEAM-PATCH-NEEDS.md:55`).
  Until it lands, the Gateway sends `/api/crm/**` to port 5061, the port `AGENTS.md:86` assigns to the SupplyChain service.
- **The BASE gateway test file.** It must land with or before the BASE `ocelot.json`; alone it would fail on today's
  `ocelot.json` by reading (it asserts 35 CRM routes on 5065 and 6 supply-chain routes; today there are 0 and 0).

## Order hazards, by reading

| Hazard | Why | Avoided by |
|---|---|---|
| Gateway routes before `Program.cs` P0 | The routes would expose controllers whose dependencies are not registered (Q210-Q213: "half-wired") | step 3 before the supply-chain part of step 5 |
| Claims count patch (6 → 8) applied as written | Wrong as soon as the Returns routes are present (10) | one guard number computed for the routes actually merged (`OCELOT-PROPOSAL.md` §6) |
| A provider line without its class | Compile error; the four classes are not in the tree | step 6 as one change per module |
| Two writers on `Program.cs` | SR-D4 single-writer rule; Q220 and Q209 are both near this seam | Q220 stays inside `SandopPersistenceRegistration.cs`; only Q209 writes `Program.cs` |
| Evaluate route released while the executor is not registered | Evaluations would stay Accepted for ever and block their scenario | keep line 77 and route 18 together |

## Paths this proposal touches that are protected, and who owns them (task 5)

| Path | Protection | Owner | What this lane did |
|---|---|---|---|
| `gateway/Diten.ApiGateway/ocelot.json` | `AGENTS.md:108` — only `integration-agent` modifies it; rule `routes.md:124` | **integration-agent**. Q209 must not edit it directly; it needs its own integration-agent work package or an explicit assignment inside Q209 | read only; proposal text in `OCELOT-PROPOSAL.md` |
| `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` | not named in `AGENTS.md` §4; shared file under SR-D4 (single integration owner); `gateway/**` is read-only for this WP | integration-agent, same change as `ocelot.json` | read only |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | not in the `AGENTS.md` §4 table (it is the domain's own service, `AGENTS.md:109`), but every pack assigns it to the integration owner (MOD-0190 :99; MOD-0192 :437-438) | Q209 single integration owner | read only; proposal text in `PROGRAM-CS-PROPOSAL.md` |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.json` | shared service configuration | integration owner | read only; two keys named, no value chosen (OD-4) |
| The 35 `/api/crm` routes inside `ocelot.json` | another domain's routes (Commercial Suite), `AGENTS.md:109` in spirit; the port itself is decided (`AGENTS.md:87`, `:90`) | integration-agent, with the CRM owner informed | read only |
| `…/Features/CapacityPlans/**` (executor, lease store) | MOD-0192 owned paths | Capacity DEV lane, only if OD-1 / OD-2 require a change | read only; no change proposed |
| `…/Persistence/Features/SandopPlans/SandopPersistenceRegistration.cs` | MOD-0190 owned path; Q220 is working on it | Q220 lane | read only, twice (start and end hash in `SOP-22.md` §5) |
| `.antigravity/**` | `AGENTS.md:104` | owner approval | read only. Two stale points noticed and **not** edited: `ports.md:14` still gives the band as 5011-5064 and its table has no 5061, 5062 or 5065 row; `AGENTS.md:86-87` is the authority |
