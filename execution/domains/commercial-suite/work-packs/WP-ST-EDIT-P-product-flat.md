# WORK PACKAGE — WP-ST-EDIT-P · Ürün + SKU% bölümü DÜZ (mockup): ürün+% Σ=100 + Eşit dağıt + uyarı kutusu (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`4e4c7706` üstü). **Kullanıcı kararı:** Ürün bölümü mockup gibi **DÜZ** — her satır ürün + yüzde (LineWeightPercentage), tüm satırlar Σ=100; **SKU-dağılımı (sku-allocated mod) UI'dan kaldırılır** (backend yine destekler, bu ekranda düzenlenmez). Σ badge (≠100 kırmızı) + **"Eşit dağıt"** + Σ≠100 **uyarı kutusu** (mockup). **Frontend** (form.js + _Form.cshtml + strategy-create.css + resx). Backend/contract DEĞİŞMEZ (ProductLinesJson: product-only satırlar).

## Kanıt
- `form.js` renderProducts (~433): iç içe builder — her ürün satırı `skuAllocationMode` toggle (product-only/sku-allocated) + `lineWeightPercentage` + SKU-allocation alt-satırları (SKU + % ürün-içi Σ=100). `lineTotal` = satır-içi SKU toplamı.
- `_Form.cshtml` productSection: başlık + "Ekle" + `TotalMustBe100` + containment alert + `#productLineList` + empty. Σ badge/Eşit dağıt/uyarı kutusu YOK.
- Contract (StrategyTemplateModels): ProductLineInput = GlobalProductId + LineWeightPercentage(decimal?) + SkuAllocationMode + SkuAllocations. product-only modda SkuAllocations boş, LineWeightPercentage taşır. **Mockup düz = product-only satırlar, LineWeightPercentage Σ=100.**
- Mockup: "Ürün + SKU% — NE / Tüm satırların toplamı tam olarak %100 olmalı… / Σ 99.99% / Eşit dağıt / [uyarı] SKU yüzdesi %100 değil. Hesaplanan toplam 99.99% — fark −0.01 puan. Kaydetmek için satırları düzeltin. / ÜRÜN | SKU(MDM-GP kod) | YÜZDE | Kaldır".

## NE (frontend; backend/contract DEĞİŞMEZ)
1. **Düz satır (renderProducts):** her ürün satırı = **ürün picker** (MDM Global Product; ad + alt-satır MDM-GP kod) + **YÜZDE** input (lineWeightPercentage) + **Kaldır ikon** (segment gibi btn-icon bx-trash). **Mode toggle + SKU-allocation alt-satırları KALDIR** (UI). Satır state: `skuAllocationMode='product-only'`, `skuAllocations=[]`, `lineWeightPercentage=%`. **Edit veri kaybı önlemi:** gelen satır sku-allocated ise skuAllocations ProductLinesJson'da KORUNUR (wipe yok; DUR gerekirse) — flat UI ürün+ağırlık gösterir.
2. **Σ badge:** başlıkta canlı `Σ N%` (Σ lineWeightPercentage); ≠100 → kırmızı (danger), =100 → success/muted. round2.
3. **"Eşit dağıt" butonu:** 100'ü satırlara eşit böl (lineWeightPercentage = round2(100/N), kalan artık ilk/son satıra; toplam tam 100). Frozen'da pasif.
4. **Uyarı kutusu (Σ≠100):** mockup metni — "Ürün yüzdesi %100 değil. Hesaplanan toplam {N}% — fark {±X} puan. Kaydetmek için satırları düzeltin." (danger/warning kutu, `.st-prod-warn`). =100'de gizli. (FU04 "canlı toplam display; runtime ≠100 reddeder" ilkesi korunur — uyarı UI rehberi.)
5. **css + resx (7 dil):** düz satır layout + Σ badge + Eşit dağıt + uyarı kutusu; EqualDistribute, ProductTotalWarning (format), Σ etiket. Başlık `ProductSection` "Ürün + SKU% — Ne" (mockup {Ad}—{Soru}; gerekirse). PascalCase köprü.

## KORU / YAPMA
- Backend/contract DEĞİŞMEZ. ProductLinesJson (globalProductId/lineWeightPercentage/skuAllocationMode/skuAllocations) sözleşmesi KORUNUR — UI product-only yazar; gelen sku-allocated satır datası WIPE EDİLMEZ. renderProducts dışı (Segment/Frekans/İçerik/KAPSAM/sağ panel) DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. Ürün picker MDM load mantığı KORUNUR. Uydurma % yok. Tema/L10n köprüsü.
- **DUR:** gelen sku-allocated satır datasını product-only flat UI'da veri kaybı olmadan taşımak mümkün değilse; Eşit dağıt/Σ ProductLinesJson'ı bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + _Form.cshtml + strategy-create.css + resx. Backend/Controller/liste/detay diff YOK.
- **E4:** Ürün satırları düz (ürün + MDM-GP kod + % + Kaldır ikon); Σ badge ≠100 kırmızı; "Eşit dağıt" 100'ü böler; Σ≠100 uyarı kutusu mockup metniyle; kaydet→ProductLinesJson product-only doğru. **Razor+css+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-P · Ürün bölümü DÜZ (mockup) ürün+% Σ=100 + Eşit dağıt + uyarı kutusu (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 4e4c7706 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-P-product-flat.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderProducts ~433, lineTotal ~431, addProduct ~585, product picker/MDM load) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (productSection) · services/.../StrategyTemplate/StrategyTemplateModels.cs (ProductLineInput/SkuAllocation — contract, DEĞİŞTİRME) · wwwroot/assets/css/strategy-create.css · resx. Mockup: /c/tmp/mockup-strategy.html Düzenle Ürün (server 127.0.0.1:8799).

NE (frontend; backend/contract DEĞİŞMEZ):
 1) renderProducts DÜZ: her satır ürün picker (ad + MDM-GP kod alt) + YÜZDE input (lineWeightPercentage) + Kaldır ikon (btn-icon bx-trash, segment gibi). Mode toggle + SKU-allocation alt-satırlar KALDIR. Satır state product-only (skuAllocationMode='product-only', skuAllocations=[]). Gelen sku-allocated satır datası ProductLinesJson'da KORUNUR (edit veri kaybı yok).
 2) Başlıkta Σ badge canlı (Σ lineWeightPercentage; ≠100 kırmızı/danger, =100 success/muted; round2).
 3) "Eşit dağıt" butonu: 100'ü satırlara eşit böl (round2(100/N), kalanı bir satıra ekle→tam 100). Frozen pasif.
 4) Σ≠100 uyarı kutusu (.st-prod-warn danger): "Ürün yüzdesi %100 değil. Hesaplanan toplam {N}% — fark {±X} puan. Kaydetmek için satırları düzeltin." =100'de gizli.
 5) css + resx 7 dil: düz satır + Σ badge + Eşit dağıt + uyarı; EqualDistribute/ProductTotalWarning/Σ etiket; ProductSection "Ürün + SKU% — Ne" (gerekirse). PascalCase köprü.
KORU/YAPMA: backend/contract DEĞİŞMEZ; ProductLinesJson sözleşmesi korunur (UI product-only yazar, gelen sku-allocated datası WIPE EDİLMEZ); Segment/Frekans/İçerik/KAPSAM/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; ürün picker MDM load korunur; uydurma yok; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+_Form+css+resx; backend/Controller/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-P — Ürün bölümü düz (ürün+% Σ=100) + Eşit dağıt + uyarı kutusu (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: sku-allocated satır datasını flat UI'da veri kaybı olmadan taşımak mümkün değilse; Eşit dağıt/Σ ProductLinesJson'ı bozuyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: b6a1b3bc · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitp-verify @b6a1b3bc
```
- ✅ **Kapsam (11 dosya, +185/−90):** form.js + _Form.cshtml + _IndexL10n + strategy-create.css + 7 resx. **backend/Controller/ViewModel/StrategyTemplateModels/liste/detay TEMİZ** ✓.
- ✅ renderProducts DÜZ: satır = ürün picker (ad + MDM-GP kod) + js-weight (lineWeightPercentage) + Kaldır ikon; mode toggle + SKU alt-satırları kalktı. `productWeightTotal` = Σ lineWeightPercentage; Σ badge (boş muted/=100 success/≠100 danger). "Eşit dağıt" round2(100/N)+artık son satıra→tam 100. `.st-prod-warn` uyarı kutusu (Σ≠100). 
- ✅ **Contract KORU=0:** sync ürün döngüsü YALNIZ globalProductId+lineWeightPercentage okur; **skuAllocationMode/skuAllocations HİÇ okunmuyor/silinmiyor** (eski wipe satırı kaldırıldı) → gelen sku-allocated satır datası ProductLinesJson'da byte-for-byte korunur (edit veri kaybı yok). Eşit dağıt yalnız lineWeightPercentage set eder.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: düz ürün satırları + Σ + Eşit dağıt + uyarı. **FLEET RESTART.**

**WP-ST-EDIT-P KOMPLE.**
```
