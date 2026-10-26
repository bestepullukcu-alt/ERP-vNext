# Q484 — Capacity context middleware: composed only in the test host, env-gated in Program.cs (Option 2′)

- owner: gmg-10 (ny@gmgroup.ch) — decided in session 2026-10-17, mechanism refined 2026-10-18, relayed through CT
- status: ACCEPTED — **Option 2′**: the Capacity context middleware is composed only when the host runs in
  the **`Testing`** environment; the production runtime never composes it. (Worded Option 2 — a test-host
  `IStartupFilter` — was ruled infeasible on verification; see below.)
- module: MOD-0192 Capacity Planning + MOD-0190 S&OP · service `Diten.SupplyChainService` (5066) · packs
  `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` (`:199`, `:273`)
  and `MOD-0190-sop-workflow-signoffs.md` (`:261`)
- code at decision time: `feature/mvp6-logistics` — the Capacity branch was unconditional at `Program.cs:152`
  (sha256 `0d66f096…`), merged via `681bb44d3`
- evidence / argument: `docs/roadmap/plans/mvp6-q484-capacity-middleware-decision-01/DECISION-MEMO.md`;
  `docs/records/audits/2026-10/mvp6-q484-capacity-branch-01/`

## The ruling

The Capacity context middleware **must run** for Capacity requests (without it an authenticated request
answers 500 with a zero correlation id — measured). It is to run **only in the test host**, never in the
production runtime. The branch in `Program.cs` is **gated to the `Testing` environment**:

```csharp
if (string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
    app.UseWhen(CapacityContextMiddleware.IsCapacityPath, b => b.UseMiddleware<CapacityContextMiddleware>());
```

Rationale: Q292 (owner, 2026-10-03) keeps the §22 accepted composition as a **gated target**, uptake
blocked until MOD-0188 + DEMAND v1.1 + a SUPPLY-CONSTRAINTS contract. The owner read Q292 on 2026-10-11 as
binding the production host but **not** a test host. Option 2′ honours both: the production runtime does not
compose the Capacity middleware (gate stays shut), while the test host (`SandopCapacityHttpHost`, which sets
`UseEnvironment("Testing")`) composes it at the correct pipeline position and keeps
`SandopCapacityHostCompositionTests` meaningful.

## Why not worded Option 2 (a test-host IStartupFilter) — measured 2026-10-18

The first implementation moved the branch out of `Program.cs` into an `IStartupFilter` in the test host.
It **failed** `Capacity_controller_is_mapped_and_its_context_middleware_is_composed` with a bare 401.
Cause: `CapacityContextMiddleware.InvokeAsync` reads `http.GetEndpoint()` (needs **UseRouting**) and
`http.User` (needs **UseAuthentication**, `Program.cs:131`); it must run at the interior position where the
other context middlewares sit. A `WebApplicationFactory<Program>` `IStartupFilter` always runs **outermost,
before** routing/auth — so the filter saw a null endpoint and no-opped. The test host reuses Program's
pipeline and cannot inject interior middleware, so the composition code must stay in `Program.cs`. The owner
accepted the env-gate (Option 2′) as the faithful equivalent: the production **runtime** does not run it,
even though the gated line physically remains in the file.

## What this authorises and does not

- **Authorises** the env-gate edit to `services/.../Api/Program.cs` (the branch becomes conditional on the
  `Testing` environment). The `using` and the Shipment catch-all's `!IsCapacityPath` exclusion are **kept**:
  the exclusion gives a non-Testing host a clean 404 on a Capacity path (controller dropped by Q381) rather
  than a 401, and the test host needs it so the Shipment catch-all leaves Capacity paths to the Capacity
  middleware. **No test-host change is needed.**
- **Does NOT** re-pin the composition hashes. `MOD-0192:273` (`50c48a2b…`) and `MOD-0190:261`
  (`a2a216be…`) stay at the Q292 gated target. CT's 2026-10-12 re-pin refusal stands.
- `:199`/`:200` close on a **test-only runtime**. The stale "reverted/restored to `c846af73`" narrative in
  `MOD-0192:199` is superseded by this ruling (same surface as owner-queue item 4, MOD-0192 `:199/:200`).

## Verification (2026-10-18)

`SandopCapacityHostCompositionTests` pass (2/2, incl.
`Capacity_controller_is_mapped_and_its_context_middleware_is_composed`) against a disposable replica set
with the env-gate in place — the test host composes Capacity at the correct position. The env-gate changes
only the pipeline condition, not any DI registration, so the production container
(`CompositionRootGuardTests`) is unaffected.
