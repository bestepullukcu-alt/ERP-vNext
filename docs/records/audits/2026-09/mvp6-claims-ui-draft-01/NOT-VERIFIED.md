# NOT-VERIFIED — Q64a MOD-0187 Claims UI draft overlay

This lane has no .NET SDK, no MongoDB and no Playwright browser run (proxy blocks them). Everything below is
**unverified** and belongs to Q64b (Mac lane) or later gates. "Static" results are in `STATIC-CHECKS.txt`.

| # | Not verified | Why | Who/where |
|---|---|---|---|
| N1 | `dotnet build` of frontend `Diten.Web`, `Diten.Web.Tests`, SupplyChain Api and Tests with the overlay | no .NET SDK in this lane | Q64b, isolated env |
| N2 | Roslyn analyzers, nullable warnings, code style | same | Q64b |
| N3 | Execution of the 4 xUnit files (3 frontend + 1 service, M-01…M-08) | same; only a Python dry-run of the file-based string assertions (29/29) was possible | Q64b |
| N4 | Razor compilation of the 7 views (tag helpers, `@inject`, `Perms.Has` method group, `_AccessDenied` model) | no Razor compiler; only comment/div balance and tenant-shell checks | Q64b |
| N5 | Runtime chain: MVC routing, antiforgery header `RequestVerificationToken`, JSON 401 challenge, Gateway routes, Claims backend, Shipment root emission, Mongo, Auth | no runtime here | Q64b |
| N6 | Playwright scenarios (`runtime-scenarios/claims-ui.spec.mjs`), PNG capture | never executed | Q64b |
| N7 | Shared browser helpers at runtime: `DtDefaults.create/exportButtons/updateVisualState/handleUnauthorized`, `personalizationClient`, `showToast`, `showConfirm(title, cb, {subtext,type,confirmButtonText})`, Select2 | checked by **reading** `dt-defaults.js`, `_GlobalNotification.cshtml`, `_GlobalConfirmation.cshtml` in the checkout only | Q64b |
| N8 | `verify_datatable_page.py --reference slim` and `quality-gate-datatable` on an applied tree | record-only per CU-SCR-06; needs a full frontend tree with the overlay applied | Q64b (record only) |
| N9 | `NavManifestL10nGuardTests` with the provider + nav fragments applied (W-03) | needs integrated SharedResource | integration owner |
| N10 | Native-speaker / l10n-agent review of tr, fr, es, zh, ar, ru text | lane author wrote the translations | l10n agent |
| N11 | Responsive/RTL visual result at 390/768/1024/1440 | no browser | Q64b |
| N12 | Build composition of Claims accepted source + A12 360 overlay (both touch SupplyChain `Program.cs`/`.csproj`) | FINDING F3 | Q64b / CT |
| N13 | DataTables `language.emptyTable`: `DtDefaults.create` overrides it with the shared `DtEmptyTable` text when present, so the module `EmptyState` text may not show | read from `dt-defaults.js:501-520` | Q64b (visual) |
| N14 | Gateway fragment merge into `ocelot.json` and `OcelotConfigurationTests` | shared seam | integration owner |
