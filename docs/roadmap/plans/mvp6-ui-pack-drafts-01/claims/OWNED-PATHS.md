# MOD-0187 Claims UI — proposed owned and protected paths (unapplied)

Prospective, repo-relative. The common checkout has no `Views/SupplyChain/` folder today. Paths bind to the CT target (G-TARGET). Nothing here is a write grant.

## Owned by the single Claims UI writer (new files only)

- `frontend/Diten.Web/Controllers/SupplyChainClaimsController.cs`
- `frontend/Diten.Web/Models/SupplyChain/Claims/ClaimViewModels.cs`
- `frontend/Diten.Web/Views/SupplyChain/Claims/ClaimsIndex.cs`
- `frontend/Diten.Web/Views/SupplyChain/Claims/Index.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Claims/_Filter.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Claims/_DataTable.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Claims/_IndexL10n.cshtml`
- `frontend/Diten.Web/Views/SupplyChain/Claims/_CreateEditOffcanvas.cshtml` (create-only)
- `frontend/Diten.Web/Views/SupplyChain/Claims/_DetailsQuickView.cshtml` (row summary only)
- `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.js`
- `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.l10n.js`
- `frontend/Diten.Web/Resources/Views/SupplyChain/Claims/ClaimsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (7 files)
- `frontend/Diten.Web.Tests/Controllers/SupplyChainClaimsControllerTests.cs`
- `frontend/Diten.Web.Tests/Forms/ClaimFormContractTests.cs`
- `frontend/Diten.Web.Tests/JavaScript/ClaimIndexBehaviorTests.cs`

21 files (9 C#/Razor, 2 JS, 7 resx, 3 tests). DEV evidence (proposal): `docs/records/audits/2026-09/mvp6-claims-ui-dev-01/`;
independent VER: `mvp6-claims-ui-ver-01/`.

## Protected

The same protected set as Returns (returns/OWNED-PATHS.md): all backend source including `Features/Claims/**` and
`Program.cs`; contracts/annexes; `gateway/**`; shared layouts, partials, JS/CSS, SharedResource, frontend
`Program.cs`/DI, nav/permission catalogues; Shipment/Carrier/Loads/**Returns** UI files; Golden Slim; packs,
domain config, DCPs, registries, `.antigravity/**`, guards, existing records; Git state.
