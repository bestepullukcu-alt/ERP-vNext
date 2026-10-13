# MOD-0187 Claims UI DRAFT — v1 → v2 changes (Q64c)

v1 = `mvp6-claims-ui-draft-01/claims-ui-draft-overlay.tar.gz` `bc0f5819de94273ebca162ed6ede2a62555f888f314c799c27e56a1c75b82e60`
(38 files: 36 archive files plus the 2 open `runtime-scenarios/` files).
v2 = `claims-ui-draft-overlay-v2.tar.gz` (39 files; the hash is in `SHA256SUMS`).

The comparison was made file by file in `~/mvp6-env/q64c/diff-232549/` (`v1-v2-diff.tsv`
`164d89db68f6878ce82bac925117913822c6933b5d052eda18fa8491649ad644`).
Totals: **1 added · 11 modified · 0 removed · 27 unchanged.**

| # | File (archive path) | Change | Lines | Reason |
|---|---|---|---|---|
| C-01 | `overlay/frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.js` | `ajax: loadClaims` → non-async wrapper | part of +113/−54 | **Q64b D-01 (HIGH).** DataTables 2 keeps the ajax function's return value as its jqXHR and calls `.abort()` on reload. The async function returned a Promise, so `ajax.reload()` threw and the list never reloaded after create, transition, filter or retry. Verbatim from Q64b `FIXES.patch` FIX-02. |
| C-02 | `overlay/frontend/Diten.Web/Views/SupplyChain/Claims/_{CreateEditOffcanvas,DataTable,DetailsQuickView,Filter,IndexL10n}.cshtml`, `overlay/frontend/Diten.Web.Tests/Forms/ClaimFormContractTests.cs` | `Layout = "_LayoutTenantShell"` removed from the 5 partials; the layout test now requires it on the page view and forbids it in partials | +1/−2 per partial; test part of +20/−1 | **Q64b D-02 (HIGH).** ASP.NET Core applies an explicit Layout to a partial, which rendered 6 full shells. Row dropdowns never opened, so no transition was reachable. Verbatim from `FIXES.patch` FIX-03. Pack §32.2 wording: see F-PACK-32.2 (CT / pack owner). |
| C-03 | `_CreateEditOffcanvas.cshtml`, `Index.cshtml`, `overlay/frontend/Diten.Web/Models/SupplyChain/Claims/ClaimViewModels.cs`, `ClaimFormContractTests.cs` | `@model …ClaimCreateFormViewModel`. The new sealed class has the 6 form fields as `string?`/`bool`/`IReadOnlyList<string>?` and **no validation attributes**. `Index.cshtml` passes `model="new ClaimCreateFormViewModel()"`. New test `Create_form_is_strongly_typed_without_validation_metadata`. | +1/−0, +1/−1, +20/−0, test part of +20/−1 | **Q101 F06 real gap** ("`_CreateEditOffcanvas.cshtml` has no `@model`"). Pack §32.6 forbids client tightening, so the model only types the form. The adapter never binds it: the body is still forwarded as typed (§32.8), and the form still has no `asp-for`, `required`, `maxlength` or `pattern`. The explicit model instance keeps the partial from inheriting the page model. |
| C-04 | `index.js` | Save View / Reset rewritten on the golden-reference slim shape: `emptyFilters`, `defaultColVis`, `baseOrder`, `getCurrentView`, `normalizeViewState`, `getResetBaselineState`, `isDirtyComparedToDefault`, `applySavedTableState`, `saveDefaultView` (name fallback `saved name ‖ L.SaveView ‖ 'Default'`). Apply and Reset recalculate Save View visibility against the saved default. Reset restores the full factory state (filters, search, column visibility, column order, sort) in one step. The column-event handler uses the same dirty check instead of "any change = dirty". | rest of +113/−54 | **Q101 F06 real gaps:** the verifier checks "Apply click updates Save View visibility", "Save View payload has non-empty default name", "Reset click recalculates Save View dirty visibility", "Reset baseline is factory table state" and "Reset click restores full factory table state". Claims difference: filters are server query parameters (§32.5), so a filter change in `applySavedTableState` reloads through the adapter, and other changes redraw locally. Saved view shape changes from `columnVisibility[]` to `colVis{}` + `columnOrder` (v1 never ran outside a lane DB). |
| C-05 | `overlay/_shared-integration/gateway-tests-ocelot-count.patch.txt` (**added**), `overlay/_shared-integration/README.md` | Q64b FIX-01 (6 → 8 route-count guard) as an SR-D4 handoff hunk, listed in the handoff index and in the apply order | new; +2/−1 | **Q64b F-SHARED-01.** The target is a shared A12 file (`gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`), so it is not a module file. Checked: it applies to the A12 copy in `~/mvp6-env/claims` and reproduces Q64b's fixed file byte for byte. |
| C-06 | `runtime-scenarios/claims-ui.spec.mjs` | New helpers `waitForList(page)` (table visible, skeleton hidden, first `tbody tr`) and `openRowAction(action)` (opens the "⋮" of the action's own `<tr>`). The QuickView, Open→Investigating and stale-transition tests use them; B's Reject is located by A's `data-claim-id`. The header's run path now points at the archive. | +24/−7 | **Q64b S-01** (`.dropdown-toggle.first()` hit the shell navbar) and **S-02** (`test.skip(!count)` evaluated before the table rendered). |

## Not changed

- **Item 4, Q101 F13 (PageDescription key): not applicable to Claims.** F13 names S&OP (`SandopPlansPageDescription`) and
  Capacity (`CapacityPlansPageDescription`). Claims already uses the standard `PageDescription` key in all 7 `ClaimsIndex.*.resx`
  (1/1 each), in `Index.cshtml:30` and in `_IndexL10n.cshtml:29`. A rename would have introduced a non-standard key, so nothing
  was renamed.
- **Endpoints, permissions and routes:** none added. `supplychain.carriers.read` was not added (pack decision Q114).
- **Q101 F06, other verifier FAILs:** left for CT (Q107). They are:
  - pack-OUT items: Active/Passive/Unknown/Edit/BulkDelete/Export/Import L10n keys, bulk, and select-all;
  - direct-gateway rules that do not apply to the proxy profile: `getAuthHeaders`, `window.API`;
  - the shared `personalization-client.js` tenant-header rule;
  - `reloadWithToast`;
  - the `Actions` and `ViewDetails` L10n keys: Claims uses `ActionsHeader` and `QuickView`.
- The other 27 files are byte-identical to v1.
