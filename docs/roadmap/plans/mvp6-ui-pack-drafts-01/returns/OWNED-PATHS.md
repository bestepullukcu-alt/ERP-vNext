# MOD-0186 Returns UI — proposed owned and protected paths (unapplied)

Paths are repo-relative and prospective. The common checkout has **no** `frontend/Diten.Web/Views/SupplyChain/`
folder today (measured 2026-09-26); Shipment UI lives in isolated baselines. Final paths bind to the target CT picks
(G-TARGET). Nothing here is a write grant.

## Owned by the single Returns UI writer (new files only)

- `frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs`
- `frontend/Diten.Web/Models/SupplyChain/Returns/ReturnViewModels.cs`
- `frontend/Diten.Web/Views/SupplyChain/Returns/ReturnsIndex.cs`
- `frontend/Diten.Web/Views/SupplyChain/Returns/Index.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Returns/_Filter.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Returns/_DataTable.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Returns/_IndexL10n.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Returns/_CreateEditOffcanvas.cshtml` (create-only)
- `frontend/Diten.Web/Views/SupplyChain/Returns/_DetailsQuickView.cshtml` (row summary only)
- `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js`
- `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.l10n.js`
- `frontend/Diten.Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (7 files)
- `frontend/Diten.Web.Tests/Controllers/SupplyChainReturnsControllerTests.cs`
- `frontend/Diten.Web.Tests/Forms/ReturnFormContractTests.cs`
- `frontend/Diten.Web.Tests/JavaScript/ReturnIndexBehaviorTests.cs`

That is 21 files (9 C#/Razor, 2 JS, 7 resx, 3 tests). The transition modal is built in `index.js` with the shared premium wrapper, so it needs no extra partial.
DEV evidence target (proposal): `docs/records/audits/2026-09/mvp6-returns-ui-dev-01/`. Independent VER:
`docs/records/audits/2026-09/mvp6-returns-ui-ver-01/`. If a target exists, CT decides; nothing is overwritten.

## Protected (the UI writer never edits)

- All backend source: `services/Diten.SupplyChainService/**` including `Features/Returns/**` and `Program.cs`.
- Published contracts and annexes: `docs/analysis/contracts/**`.
- `gateway/**` (ocelot.json is integration-agent only; AGENTS.md §4).
- Shared web surfaces: `_LayoutTenantShell.cshtml`, `_Layout.cshtml`, `_ViewStart.cshtml`, `_AccessDenied.cshtml`,
  `NotAuthorized.cshtml`, `_GlobalConfirmation.cshtml`, `_PermissionBootstrap.cshtml`, shared JS/CSS (`dt-defaults.js`,
  personalization client), `SharedResource.*.resx`, frontend `Program.cs`, DI, navigation/module/permission catalogues.
- Shipment, Carrier, Loads and Claims UI files; Golden Reference Slim (read only, never edited).
- Module packs, domain config, DCPs, registries, `.antigravity/**`, guards/architecture tests, existing records.
- Git state: no commit, push, stash or branch change.

A shared change found necessary during DEV stops the writer. The exact diff goes to the integration owner (SHARED-HANDOFF.md).
