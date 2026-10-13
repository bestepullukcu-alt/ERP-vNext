# NOT VERIFIED — Q91 MOD-0186 Returns tenant UI draft overlay

**DRAFT — not built, not tested, not run, not writer-complete.** This chat lane has no .NET, browser, Gateway or database.
Everything below belongs to the future Mac lane (Q65b) in the isolated environment, then to an independent VER.

| # | Not verified here | Where it is verified |
|---|---|---|
| NV-01 | `dotnet build` of frontend `Diten.Web` + `Diten.Web.Tests` and of the SupplyChain Api + tests with the overlay (Razor compile, analyzers, nullable warnings) | Q65b, isolated env (README "Build note for Q65b") |
| NV-02 | `SupplyChainReturnsControllerTests`, `ReturnFormContractTests`, `ReturnIndexBehaviorTests` (the file-based assertions were dry-run in STATIC-CHECKS; the controller tests were not) | Q65b |
| NV-03 | `ReverseLogisticsManifestProviderTests` (M-01…M-08) against the accepted `ReturnPermissions` and `ReturnStatus` | Q65b |
| NV-04 | Shared guards W-01…W-04 and R-01…R-04 with the `_shared-integration/` items applied to the environment copy | Q65b / integration owner |
| NV-05 | Every RU row of pack §32.11 at runtime (real Auth, Gateway, SupplyChain, Mongo): `runtime-scenarios/returns-ui.spec.mjs` | Q65b |
| NV-06 | RU-03 envelope/absent fields, RU-12 cross-LE list, RU-13 transition safe-not-found, RU-15 root seam (BLOCKED producer uptake), RU-16 replay / `IDEMPOTENCY_KEY_REUSED` (BLOCKED DN-01), RU-17 stale transition 422, RU-19 whitespace disposition code, 503 same-key retry, RU-26 family routing `/returnsXYZ` + 78/78 Returns regression | Q65b (fault injection / seeded states / second profile) |
| NV-07 | RTL rendering, LTR isolation of UUIDs/quantities, 390/768/1024/1440 without horizontal overflow — including the line picker table inside the Slim offcanvas (§32.13 layout gate) — and keyboard/Escape focus in the three offcanvases and the shared confirmation | Q65b (runtime-scenarios RU-22/RU-23) |
| NV-08 | `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module Returns --reference slim` and the `quality-gate-datatable` result (record only, RU-SCR-06) | Q65b on the composed tree |
| NV-09 | PNG evidence through a supported export (RU-28 BLOCKED by PRES-183-04) | later |
| NV-10 | Independent VER on a frozen source | separate session |

## Check these first on the Mac (compile- or runtime-sensitive points a static check cannot see)

1. `JsonAdapterEndpointAttribute`, `PermissionClaims`, `IPermissionSnapshot`, `AuthTokenCookies` and the JSON challenge branch resolve from the composed tree (A12 360 overlay; README F3).
2. The controller compiles: the private `Scope` record, `out IActionResult? failure` with `return failure!`, collection expressions in `TryResolveScopeClaim([...])`, the tuple deconstruction `(status, code) = status switch { … }` and `DefaultMessage(code)` on a `string?` (nullable warning only).
3. `ReturnPermissions.ForTarget(...)!` inside the provider's collection expression, and `Enum.GetNames<ReturnStatus>().Select(ReturnPermissions.ForTarget).OfType<string>()` in the provider tests (method-group conversion to `Func<string, string?>`).
4. `window.DtDefaults.create({ ajax: loadReturns, … })` with an async ajax function, `exportButtons(…)` filtering, and Save View arming at the end of `initComplete` (no timer); `personalizationClient` codes (A6).
5. `window.showConfirm(title, callback, { subtext, type, confirmButtonText })` on the target (MOD-0013) and the Playwright accept selector (A10).
6. The Shipment lookup header policy on the target: `X-Tenant-Id`/`X-Legal-Entity-Id` + browser trace on `GET /api/shipment-bundle/shipments/{id}` (A12 Shipment adapter policy).
7. That the Returns backend echoes the root in `X-Correlation-Id`/`error.correlationId` exactly as read (`ReturnContextMiddleware.cs:52-54, 60`) and that the adapter's replacement keeps RU-21 true at the browser boundary (F8).
8. xUnit `InlineData` with `new[] { … }` const-string arrays and the static file readers finding `AGENTS.md` from the test output folder.
