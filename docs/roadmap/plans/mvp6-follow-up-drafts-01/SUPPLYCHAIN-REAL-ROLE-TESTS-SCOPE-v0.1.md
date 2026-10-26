# SupplyChain tests with real role assignment instead of minted tokens — scope (v0.1)

    created  : 2026-10-21 · status: SCOPE, not approved work · asked by Ali (BL-652 reply, 2026-10-21)
    measured : origin/main 9d44e8a1f, read-only

## 1 · Where tokens are hand-made today

| kind | where | count | what they prove |
|---|---|---|---|
| In-process HTTP tests (C#) — a test host signs a JWT with `permission` claims (e.g. `ReturnsClaimsHttpHost.Token(…)`, `ReturnsClaimsHttpHost.cs:123-147`) | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/` — `CapacityPlans/CapacityGateLegalEntityTests.cs`, `Carriers/CarrierContractTests.cs`, `Claims/ClaimIsolationTests.cs`, `Common/LegalEntityMiddlewareWiringTests.cs`, `Common/ReturnsClaimsHttpHost.cs`, `Common/SandopCapacityHttpHost.cs`, `Loads/LoadContractTests.cs`, `ShipmentTests.cs`, `Shipments/ShipmentTelemetryTests.cs`, `SourceIntakeTests.cs` | 10 files | SupplyChain's **own** authorization and isolation (`[HasPermission]`, tenant/LE, 403/404) with MDM and Auth replaced |
| Runtime probes (Python) against a running stack | `services/Diten.SupplyChainService/tests/{carriers,claims,loads,returns,root_uptake}/` (`runtime_probe.py`, `restart_probe.py`, `failure_probe.py`) | 13 files in 5 folders | the real HTTP path through the gateway — **this is where the BL-652 coupling (MDM `mdm.legal-entities.read`) shows up**, and where a minted token hides it |

## 2 · Proposal

- **Keep** the 10 in-process test files as they are. They test SupplyChain's own permission rules with a fixed claim
  set; a real Auth + Platform + MDM stack there would turn unit/contract tests into slow integration tests and test
  other services' behaviour.
- **Change** the runtime / acceptance proof: in a lane stack, a tenant administrator grants the needed keys (the four
  SupplyChain modules' keys **and** `mdm.legal-entities.read`) to a role through the tenant's **Roles → Permissions**
  screen (or its API), a user with that role **logs in**, and the probes run with **that login token**. Negative case:
  a user whose role lacks `mdm.legal-entities.read` → create answers 503 (the documented BL-652 state), not 201.
- The probes accept the token from the environment instead of minting it; the minting helper stays for the
  in-process tests only.

## 3 · Question for Ali (one line)

"Real role assignment" — for the **runtime / acceptance runs** (proposal above), or also for the in-process unit and
contract tests?

## 4 · Not measured

Which probe mints and which already reads a token from the environment; whether the Roles → Permissions API can be
driven headless in a lane stack; the run time of a full stack-based acceptance run.
