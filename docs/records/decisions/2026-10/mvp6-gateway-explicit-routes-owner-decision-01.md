# Gateway explicit-routes re-derivation — owner ruling (shipment-bundle swap + supply-chain reserve)

> **OWNER RULING RECORDED. Not yet in effect.** `gateway/**` and `tests/**` are Protected Paths; the two
> patches below are the integration-agent's to apply, not CT's. CT built them from the current contracts and
> verified both apply clean against HEAD; the guard's expectations were data-cross-checked against the
> generated route set (0 mismatches). Argument: `docs/roadmap/plans/mvp6-gateway-routes-conversion-01/PREP.md`.

- owner: **ny@gmgroup.ch** (product/integration owner) — decision relayed by the Control Tower, 2026-10-18
- module: gateway (`Diten.ApiGateway`, 5000) → `Diten.SupplyChainService` (5066) · MOD-0183/0184/0185/0186/0187
  (A) and MOD-0190/0192 (B)
- precedent / rules: OD-P1 (SupplyChain = 5066), NET-001 / OD-8 (explicit routes carry OPTIONS), OD-9
  (sandop-plans collection POST-only), Q484/Q381 (S&OP + Capacity not composed in production)

## The ruling

- **A — ACCEPTED: the explicit swap.** Replace the two `/api/shipment-bundle` catch-all routes with **12
  explicit per-operation routes** on 5066, each carrying OPTIONS. Accepted consequence: an **undeclared**
  path under `/api/shipment-bundle/...` now **404s at the gateway** instead of proxying through — the
  NET-001 intent (only declared operations are routed).
- **B — RESERVE NOW: the 10 supply-chain routes.** Add the **10** `/api/supply-chain` routes (S&OP +
  Capacity) now as a documented **"reserved, 404-until-composed"** set. They reach 5066 and 404 until the
  §22/Q292 composition gate opens (S&OP/Capacity are not composed in production). The route surface is
  complete ahead of composition; no behaviour depends on them until then.
- **OD-7 guard — ACCEPTED.** A test asserts exactly 12 shipment-bundle + 10 supply-chain routes on 5066,
  each with OPTIONS and the exact method set per route (so a dropped method or a resurrected catch-all
  fails the build).

Net route delta: **+20** (323 → 343; 2 catch-all removed, 22 explicit added).

## The two patches (integration-agent applies; CT cannot — Protected Paths)

```
git apply docs/roadmap/plans/mvp6-gateway-routes-conversion-01/patches/ocelot-explicit-routes.patch
git apply docs/roadmap/plans/mvp6-gateway-routes-conversion-01/patches/od7-guard-new-test.patch
```

- `ocelot-explicit-routes.patch` — edits `gateway/Diten.ApiGateway/ocelot.json`: removes the two
  `/api/shipment-bundle` catch-all routes, adds the 22 explicit routes (12 A + 10 B). Result validated as
  JSON (343 routes). Applies clean against HEAD.
- `od7-guard-new-test.patch` — adds `gateway/Diten.ApiGateway.Tests/Mvp6ExplicitRoutesTests.cs` (the OD-7
  guard, self-contained loader mirroring `OcelotConfigurationTests`). Applies clean against HEAD. CT
  verified by inspection + a data cross-check that it passes once the ocelot patch is also applied; the
  integration-agent compiles and runs `Diten.ApiGateway.Tests` in CI to confirm.

Apply **both together** — the guard asserts the explicit set and would fail against the pre-swap catch-all.

## Scope / out of scope

- OD-8, OD-9 already resolved by the current contracts (no further call).
- OD-12 (permission keys) is platform-owner and unrelated to routing — not in these patches.
- The supply-chain routes being reserved does **not** compose S&OP/Capacity or change their 404 behaviour;
  they remain fixture-bound per `mvp6-0190-0192-fixture-bound-owner-decision-01`.
