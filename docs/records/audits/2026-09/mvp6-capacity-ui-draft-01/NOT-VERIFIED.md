# NOT VERIFIED — Q88 MOD-0192 Capacity tenant UI draft overlay

**DRAFT — not built, not tested, not run, not writer-complete.** This chat lane has no .NET, browser, Gateway, database or
executor. Everything below belongs to the future Mac lane (Q88b) in the isolated environment, then to an independent VER.

| # | Not verified here | Where it is verified |
|---|---|---|
| NV-01 | `dotnet build` of frontend `Diten.Web` + `Diten.Web.Tests` and of the SupplyChain Api + tests with the overlay (Razor compile, analyzers, nullable warnings) | Q88b, isolated env (README "Build note for Q88b") |
| NV-02 | `SupplyChainCapacityPlansControllerTests`, `CapacityPlanFormContractTests`, `CapacityPlanDetailsBehaviorTests` | Q88b |
| NV-03 | `CapacityPlanningManifestProviderTests` (M-01…M-08) against the accepted `CapacityPermissions` | Q88b |
| NV-04 | Shared guards W-01…W-04 and R-01…R-04 with the `_shared-integration/` items applied to the environment copy | Q88b / integration owner |
| NV-05 | Every CP row of pack §23.11 at runtime (real Auth, Gateway, SupplyChain, Mongo, executor): `runtime-scenarios/capacity-ui.spec.mjs` | Q88b |
| NV-06 | CP-03 per-panel error states, CP-15 422 `INVALID_CONSTRAINT_REFERENCE`, CP-16 503 same-key retry, CP-14 replay / `IDEMPOTENCY_KEY_REUSED` (BLOCKED by DN-01), CP-18 family routing `/capacity-plansXYZ`, an evaluation reaching Failed, foreign-scope and soft-deleted resources (CP-13 full) | Q88b (fault injection / fixtures / second profile) |
| NV-07 | RTL rendering, LTR isolation of IDs and decimals, 390/768/1024/1440 without horizontal overflow, keyboard/Escape focus in the three offcanvases | Q88b (runtime-scenarios CP-17) |
| NV-08 | `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module CapacityPlans --reference slim` (record only, CP-SCR-10; verifier tension §23.13) | Q88b on the composed tree |
| NV-09 | PNG evidence through a supported export (CP-20 BLOCKED by PRES-183-04) | later |
| NV-10 | Independent VER on a frozen source | separate session |

## Check these first on the Mac (compile- or runtime-sensitive points a static check cannot see)

1. `JsonAdapterEndpointAttribute`, `PermissionClaims`, `IPermissionSnapshot`, `AuthTokenCookies` and the JSON challenge branch resolve from the composed tree (A12 360 overlay; README F2).
2. The controller compiles: collection expressions `[]` / `[capacityPlanId, scenarioId]` for the `Guid[] routeIds` parameter (C# 12), the tuple deconstruction `(status, code) = status switch { … }`, `DefaultMessage(code, status)` with `StatusCodes.*` constants in the nested switch, and no nullable warning on `idempotencyKey`.
3. `@model Guid` in `Details.cshtml` with `View(path, capacityPlanId)`; `href="~/SupplyChain/CapacityPlans"` resolves through the URL-resolution tag helper.
4. `window.DtDefaults.create({ data: [], buttons: [] … })` accepts a client-side table with no toolbar buttons, `orderable: false` decimal columns and `rows.add(...).draw()` on re-render inside a panel that starts hidden (`#bottlenecks-host`); check column widths after the panel becomes visible.
5. The Refresh button: one `getCapacityEvaluation` per click, disabled while pending, hidden for Completed/Failed; no request happens without a click (spec waits 10 s).
6. The date inputs send `yyyy-MM-dd` in every culture (A4); `sourceCapturedAt` typed text reaches the backend `DateTimeOffset` binding unchanged (A5).
7. xUnit theory data with `TheoryData<string, string, string, int>` and the static file readers finding `AGENTS.md` from the test output folder.
8. The SupplyChain Api `.csproj` reference to `Diten.BuildingBlocks.ModuleRegistration.Abstractions` and the registration foundation in the composed tree (BC-SOURCE's Api `.csproj`/`Program.cs` have neither — checked by grep).
9. `EVALUATION_ALREADY_ACTIVE` and the session guard (A13) on a real executor; CP-11 is timing-sensitive (README F9).
10. Ocelot placeholder matching of the six explicit routes on the target version (README F5).
