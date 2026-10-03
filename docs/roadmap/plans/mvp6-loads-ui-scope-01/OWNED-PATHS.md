# Önerilen UI allowlist — uygulanmamış

Bütün yollar repo köküne göredir. Tek UI writer yalnız aşağıdaki yeni module-owned dosyalara, ayrıca kendi bounded evidence klasörüne yazabilir; bu liste şu an DEV yetkisi vermez.

- frontend/Diten.Web/Controllers/SupplyChainLoadsController.cs
- frontend/Diten.Web/Models/SupplyChain/Loads/LoadViewModels.cs
- frontend/Diten.Web/Views/SupplyChain/Loads/LoadsIndex.cs
- frontend/Diten.Web/Views/SupplyChain/Loads/Index.cshtml
- frontend/Diten.Web/Views/SupplyChain/Loads/_Filter.cshtml
- frontend/Diten.Web/Views/SupplyChain/Loads/_DataTable.cshtml
- frontend/Diten.Web/Views/SupplyChain/Loads/_IndexL10n.cshtml
- frontend/Diten.Web/Views/SupplyChain/Loads/_CreateEditOffcanvas.cshtml
- frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Loads/index.js
- frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Loads/index.l10n.js
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.en.resx
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.tr.resx
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.fr.resx
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.es.resx
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.zh.resx
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.ar.resx
- frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.ru.resx
- frontend/Diten.Web.Tests/Controllers/SupplyChainLoadsControllerTests.cs
- frontend/Diten.Web.Tests/Forms/LoadFormContractTests.cs
- frontend/Diten.Web.Tests/JavaScript/LoadIndexBehaviorTests.cs

DEV evidence önerisi: docs/records/audits/2026-09/mvp6-loads-ui-dev-01/; bağımsız VER: mvp6-loads-ui-ver-01/. Target mevcutsa overwrite değil yeni dispatch değerlendirmesi. Proje/Program.cs/config/DI değişikliği listede yok; gerekli çıkarsa integration owner'a exact diff gönderilir.

Protected: Shipment/Carrier controller/view/JS/resource/test dosyaları; backend Features/Loads ve diğer backend kaynakları; tüm published contracts, authority/canonical/guard, pack/domain/DCP/registry; gateway; ortak layouts/_ViewStart/shared JS/CSS/L10n; navigation/module/permission catalogs; .antigravity; Git. Golden Slim kodu okunur, değiştirilmez.
