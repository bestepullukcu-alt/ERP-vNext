---
module: MOD-0192
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# G4 — MOD-0190/0192 composition spec (fixture-demand interim) — for the integration-agent

    prepared  : 2026-10-18 by CT (gmg-10) on the §22 gate-open approval
                (docs/records/decisions/2026-10/mvp6-0190-0192-section22-gate-open-owner-decision-01.md)
    for       : @integration-agent. `Program.cs`, `Application/DependencyInjection.cs` are the §22
                integration-owner surface + hash-pinned; the integration-agent applies and re-pins. CT does
                NOT edit them (the reverted mistake, MOD-0192 `:199`).
    goal      : compose 0190/0192 in production against the EXISTING fixtures — a real composed endpoint,
                fixture data — so the UI (G6) has a live backend. Real demand (G3) swaps in when v2 freezes.
    honesty   : the CLEAN changes below are exact and mechanical. THREE items (A/B/C) need a build to settle
                and are the integration-agent's to resolve with CI — CT cannot compile/test here, so a
                byte-perfect patch would be speculative. This spec is the preparation; the build closes it.

## Clean changes (exact, mechanical)

### C1 — `Application/DependencyInjection.cs`: stop excluding the two modules from MediatR
Remove both entries from `MediatRExcludedFeatureNamespaces`:
```
-        "Diten.SupplyChainService.Application.Features.SandopPlans",
-        "Diten.SupplyChainService.Application.Features.CapacityPlans",
```
(The array's own comment sets the condition: "Remove an entry only when Program.cs registers that module's
persistence AND its readers" — C2 does that. After removal the `ComposedFeatureControllerFilter` auto-keeps
their controllers, so no explicit controller registration is needed.)

### C2 — `Program.cs`: register persistence, the fixture readers, and the request contexts
Beside the existing `AddReturnPersistence()/AddClaimPersistence()` block:
```
builder.Services.AddSandopPersistence();
builder.Services.AddCapacityPersistence();
builder.Services.AddScoped<SandopRequestContext>();      // if the controller takes it from DI (verify ctor)
builder.Services.AddScoped<CapacityRequestContext>();    // CapacityPlansController ctor dep (MOD-0192 :199)
// fixture readers (interim; swapped for DemandV2HttpReader at G3):
builder.Services.AddSingleton<SandopPlans.IDemandFixtureReader, SandopPlans.DemandFixtureReader>();
builder.Services.AddSingleton<CapacityPlans.IDemandFixtureReader, CapacityPlans.DemandFixtureReader>();
builder.Services.AddSingleton<CapacityPlans.IConstraintFixtureReader, CapacityPlans.ConstraintFixtureReader>();
builder.Services.AddSingleton<IEnumerable<SandopPlans.DemandFixture>>(/* seed — see item A */);
```

### C3 — `Program.cs`: un-gate the Capacity middleware (reverse Q484)
Replace the Testing-gated branch:
```
-if (string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
-    app.UseWhen(CapacityContextMiddleware.IsCapacityPath, branch => branch.UseMiddleware<CapacityContextMiddleware>());
+app.UseWhen(CapacityContextMiddleware.IsCapacityPath, branch => branch.UseMiddleware<CapacityContextMiddleware>());
```
(The Shipment catch-all already excludes `CapacityContextMiddleware.IsCapacityPath`, so the Capacity branch
is now a production peer of Carrier/Load/Return/Claim. No catch-all change for Capacity.)

## Items that need a build to settle (integration-agent)

- **A — fixture seed values.** `DemandFixtureReader(IEnumerable<DemandFixture> fixtures)` returns false for
  everything if the set is empty (Program.cs:88 comment). For the interim to actually accept a plan, seed
  the known fixture tuple(s) the module tests use (Capacity's `ConstraintFixtureReader` already hard-codes
  `line-4-hours/8 → line-4/2027-W03/80.000/HOUR`; the Sandop/Capacity `DemandFixtureReader` need the DEMAND
  tuple — e.g. Capacity's `dp-2027 / v3 / sha256:ee56d4f9a3c8`). Confirm the exact seed from the suites and
  register it in C2. (Interim data only; replaced at G3.)
- **B — /sandop and the Shipment catch-all.** `SandopContextMiddleware` is a **static** `Resolve(...)` the
  controller calls itself (no pipeline branch, no `IsSandopPath`). The Shipment catch-all excludes
  Carrier/Load/Return/Claim/Capacity but **not** `/sandop`, so a composed `/sandop` request currently runs
  `ShipmentContextMiddleware`. Verify with a build whether that harms Sandop (it may 400 on missing shipment
  scope before the controller runs). If so, add an `IsSandopPath` and exclude it from the catch-all — the
  same shape as the Q271/Q272 Returns/Claims fix. This is the one real pipeline question.
- **C — manifest providers don't exist for 0190/0192.** No `*ManifestProvider` class exists for
  sop-workflow-signoffs or capacity-planning (grep: none). §24 self-registration to the Platform catalog
  needs them written (follow the five shipment `*ManifestProvider` + the `AddSingleton<IModuleManifestProvider, …>`
  lines). The API serves endpoints without them (manifest push is best-effort, Q339-R2), so catalog
  registration can be a follow-up — but it is part of "composed" per §24. Decide: write them now or defer.

## Re-pin (mandatory, part of §22)

After applying, recompute `shasum -a 256 Program.cs` and update the Accepted-composition pins: MOD-0190
`:261` (`a2a216be…` → new) and MOD-0192 `:273` (`50c48a2b…` → new). Also close MOD-0192 `:199/:200` (the
Capacity correlation + cross-scope lines the gate was blocking) once the branch is live and green.

## Acceptance (integration-agent, with CI)

Build `Diten.SupplyChainService`; container validation passes at `builder.Build()` (the C1/C2 condition);
an authenticated `/api/supply-chain/sandop-plans` and `/capacity-plans` create against the seeded fixture
answers 201 (not 404/500); a cross-scope read 404s and a missing permission 403s (MOD-0192 :200); the five
shipment suites stay green (byte-identical production behaviour, as the :199 measurement showed). Then G5
(activate the reserved gateway routes) and G6 (UI) proceed.

## Files

- `G4-COMPOSITION-SPEC.md` — this. CT prepared; integration-agent applies + re-pins. No `Program.cs` edit by CT.
