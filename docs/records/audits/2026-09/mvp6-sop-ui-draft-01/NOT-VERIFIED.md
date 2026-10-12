# NOT VERIFIED — Q84 MOD-0190 S&OP tenant UI draft overlay

**DRAFT — not built, not tested, not run, not writer-complete.** This chat lane has no .NET, browser, Gateway or database.
Everything below belongs to the future Mac lane (Q84b) in the isolated environment, then to an independent VER.

| # | Not verified here | Where it is verified |
|---|---|---|
| NV-01 | `dotnet build` of frontend `Diten.Web` + `Diten.Web.Tests` and of the SupplyChain Api + tests with the overlay (Razor compile, analyzers, nullable warnings) | Q84b, isolated env (README "Build dependency note") |
| NV-02 | `SupplyChainSandopPlansControllerTests`, `SandopPlanFormContractTests`, `SandopPlanDetailsBehaviorTests` | Q84b |
| NV-03 | `SopWorkflowSignoffsManifestProviderTests` (M-01…M-08) against the accepted `SandopPermissions` | Q84b |
| NV-04 | Shared guards W-01…W-04 and R-01…R-04 with the `_shared-integration/` items applied to the environment copy | Q84b / integration owner |
| NV-05 | Every SU row of pack §23.11 at runtime (real Auth, Gateway, SupplyChain, Mongo): `runtime-scenarios/sop-ui.spec.mjs` | Q84b |
| NV-06 | SU-03 per-section error state, SU-16 503 same-key retry, SU-14 replay / `IDEMPOTENCY_KEY_REUSED` (BLOCKED by DN-01), SU-18 family routing `/sandop-plansXYZ` | Q84b (fault injection / second profile) |
| NV-07 | RTL rendering, LTR isolation, 390/768/1024/1440 without horizontal overflow, keyboard/Escape focus | Q84b (runtime-scenarios SU-17) |
| NV-08 | `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module SandopPlans --reference slim` (record only, SU-SCR-09) | Q84b on the composed tree |
| NV-09 | PNG evidence through a supported export (SU-20 BLOCKED by PRES-183-04) | later |
| NV-10 | Independent VER on a frozen source | separate session |

## Check these first on the Mac (compile- or runtime-sensitive points a static check cannot see)

1. `JsonAdapterEndpointAttribute`, `PermissionClaims`, `IPermissionSnapshot`, `AuthTokenCookies` and the JSON challenge branch resolve from the composed tree (A12 360 overlay; README F2).
2. The controller's tuple deconstruction `(status, code) = status switch { … }` and `ForwardAsync` compile without nullable warnings (`idempotencyKey` is non-null on the POST path).
3. `@model Guid` in `Details.cshtml` with `View(path, sandopPlanId)`; `href="~/SupplyChain/SandopPlans"` resolves through the URL-resolution tag helper.
4. `window.DtDefaults.create({ data: [], buttons: [] … })` accepts a client-side table with no toolbar buttons; `rows.add(...).draw()` on re-render.
5. `window.showConfirm(title, callback, { subtext, type, confirmButtonText })` signature on the target (MOD-0013); the Playwright accept selector (A9).
6. The date inputs send `yyyy-MM-dd` in every culture (A4).
7. xUnit theory data with `TheoryData<string, string, string>` and the static file readers finding `AGENTS.md` from the test output folder.
8. The SupplyChain Api `.csproj` reference to `Diten.BuildingBlocks.ModuleRegistration.Abstractions` in the composed tree (the accepted S&OP source lacks it).
