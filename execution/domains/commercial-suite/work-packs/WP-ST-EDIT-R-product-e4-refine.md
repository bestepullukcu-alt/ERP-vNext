# WORK PACKAGE — WP-ST-EDIT-R · Ürün bölümü E4 rötuş: başlık sırası + Eşit dağıt ikonu + satır-başı opsiyonel SKU metni (mockup) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`5d382c57` üstü — WP-ST-EDIT-Q §37 sonrası). **E4 geri bildirim (canlı + mockup ekran görüntüsü):** (1) başlık "Ne — Ürün ve SKU karması" **yanlış** → mockup **"Ürün + SKU% — Ne"** ({Ad}—{Soru}); (2) **Eşit dağıt ikonu görünmüyor** — `bx-distribute-horizontal` iconify setinde YOK (tofu); (3) **SKU eksik** — mockup'ta SKU = satır başına **tek opsiyonel serbest-metin alanı** (placeholder "opsiyonel"), nested gsku-allocation DEĞİL. Kolonlar: **ÜRÜN | SKU | YÜZDE | (Kaldır)**. **Frontend** (form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx). Backend/contract/MDM/Controller DEĞİŞMEZ.

## Kanıt
- **Mockup ekran görüntüsü (kullanıcı):** başlık "5 Ürün + SKU% — NE"; helper "Tüm satırların toplamı tam olarak %100 olmalı. Ürünler MDM Global Product kataloğundan doğrulanır."; progress (Σ 80%) + Eşit dağıt; uyarı "SKU yüzdesi %100 değil…"; tablo başlıkları **ÜRÜN | SKU | YÜZDE**; satır = ürün adı + MDM-GP kod (sabit) · **SKU = text input placeholder "opsiyonel"** · YÜZDE = % input · Kaldır. Alt: "MDM Global Product ara — ürün adı veya kod…" (picker placeholder).
- **İkon:** `frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml:276` `<i class="bx bx-distribute-horizontal ...">`. iconify-icons.css'te `bx-distribute-horizontal` **tanımsız** (CT doğruladı: yok). Mevcut/geçerli adaylar: **`bx-equalizer`** (anlamca "eşitle"), `bx-columns`, `bx-slider-alt`, `bx-reflect-horizontal`.
- **Contract (StrategyTemplateModels.cs):** `ProductLineInput` (156-162) = `Guid GlobalProductId` + `decimal? LineWeightPercentage` + `string SkuAllocationMode` + `SkuAllocations` + **`string? Notes`**. `SkuAllocationInput.GskuId` = **Guid** (serbest metin DEĞİL). → mockup'ın serbest-metin SKU'su gsku-allocation'a **map EDİLEMEZ**; **`Notes`** alanına yazılır (tek serbest-metin alanı; şu an UI'da kullanılmıyor). SKU picker/izin (F-GSKU-PICKER-PERM) **GEREKMEZ** — bu tur devre dışı.
- **form.js renderProducts (550-571):** satır = `.st-prod-main` (st-prod-name + st-prod-meta sabit) + `.st-prod-weight` (js-weight %) + Kaldır ikon. SKU kolonu YOK.
- **form.js sync (761-773):** `[data-row="product"]` döngüsü **yalnız** `.js-weight` okur (`line.lineWeightPercentage`); globalProductId/skuAllocationMode/skuAllocations OKUNMAZ (KORU). `notes` OKUNMAZ (şu an).
- **_Form.cshtml productSection (~244-280):** başlık `ProductSection` (h6 text-uppercase); `#productWeightBar` progress; `#btnDistributeProducts` (ikon 276); `#productLineList` (JS render); `#productLineEmpty` (.st-prod-empty). Kolon başlık satırı YOK.
- **_IndexL10n.cshtml (17-33):** form.js `L` köprüsü whitelist. form.js'in render edeceği YENİ anahtar (SKU placeholder) buraya eklenmeli.
- **resx (7 dil):** `ProductSection`, `TotalMustBe100` mevcut; `SkuOptionalPlaceholder` + kolon başlıkları YENİ.

## NE (frontend; backend/contract/MDM/Controller DEĞİŞMEZ)
1. **Başlık:** `ProductSection` → **"Ürün + SKU% — Ne"** (7 dil; {Ad}—{Soru} deseni; text-uppercase büyütür). ("Ne — …" sırası ve "karması" metni düzeltilir.)
2. **Eşit dağıt ikonu:** `_Form.cshtml:276` `bx-distribute-horizontal` → **`bx-equalizer`** (iconify'da tanımlı; render olur). Başka yerde `bx-distribute-*` kullanımı varsa değiştir.
3. **Satır SKU kolonu (mockup):** renderProducts satırına **ürün etiketi ile YÜZDE arasında** opsiyonel **SKU text input** ekle: `<input type="text" class="form-control form-control-sm js-sku" placeholder="${esc(L.SkuOptionalPlaceholder||'')}" value="${esc(line.notes ?? '')}"${frozen?' disabled':''} />`. Değer **`line.notes`**'a bağlanır (contract'ta serbest-metin = Notes; GskuId Guid olduğu için gsku-allocation'a map edilmez). Kaldır **ikon** (btn-icon bx-trash) KORUNUR (kullanıcının önceki kararı; mockup'taki "Kaldır" metnine dönme).
4. **sync:** `[data-row="product"]` döngüsüne `line.notes = row.querySelector('.js-sku')?.value?.trim() || null;` ekle (js-weight okuması KORUNUR). globalProductId/skuAllocationMode/skuAllocations OKUNMAZ (KORU aynen). ProductLinesJson = JSON.stringify(state.products) (817) değişmez.
5. **Kolon başlık satırı:** `#productLineList` üstüne mockup başlıkları **ÜRÜN | SKU | YÜZDE** (küçük uppercase text-muted). _Form.cshtml Razor (satır varken görünür ya da her zaman) veya renderProducts başında; kolon genişlikleri satır layout'uyla hizalı. resx `ProductColProduct`/`ProductColSku`/`ProductColWeight` (7 dil) veya kısa reuse.
6. **css:** `.st-prod-row` layout'unu 4 kolona genişlet (ürün-main esnek | sku sabit-genişlik | weight sabit | kaldır); `.js-sku` input stili; kolon başlık satırı hizası. Progress/Σ/Eşit dağıt/uyarı (EDIT-P/Q) KORUNUR.
7. **_IndexL10n köprü:** `SkuOptionalPlaceholder` (ve renderProducts'ın okuduğu diğer yeni anahtar) whitelist'e ekle. PascalCase köprü.

## KORU / YAPMA
- Backend/contract/**MDM/Controller/ViewModel DEĞİŞMEZ**. `ProductLinesJson` sözleşmesi KORUNUR: gelen **sku-allocated satır datası (skuAllocationMode/skuAllocations) WIPE EDİLMEZ** (sync yalnız js-weight + js-sku→notes okur; globalProductId/skuAllocationMode/skuAllocations OKUNMAZ). Line weight Σ=100 + Eşit dağıt + progress + `#productWeightWarn` (EDIT-P/Q) + aramalı "Ürün ekle" picker + sabit ürün etiketi + sayfalı loadAll (EDIT-Q) davranışı KORUNUR. **gsku picker/izin (F-GSKU-PICKER-PERM) BU WP'DE YOK** — SKU serbest metindir, MDM'e gitmez. renderProducts/sync dışı (Segment/Frekans/İçerik/KAPSAM/sağ panel) DOKUNMA. Liste/Detay/details.js/CrmService/MdmService DOKUNMA. Uydurma SKU/ürün yok. Tema/L10n köprüsü.
- **DUR:** `line.notes`'a SKU yazmak mevcut bir Notes kullanımıyla çakışıyorsa (UI'da başka yerde line.notes okunuyorsa) → DUR+raporla. SKU input round-trip'te weight/skuAllocations'ı bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx. **Backend/Controller/ViewModel/MDM/liste/detay diff YOK.**
- **E4 (FLEET RESTART + re-login):** başlık "ÜRÜN + SKU% — NE"; Eşit dağıt ikonu görünür; satırda **ÜRÜN | SKU(opsiyonel) | YÜZDE** kolonları; SKU'ya metin girip kaydet→`line.notes`'a yazılır + tekrar aç→geri gelir; gelen sku-allocated satır (varsa) korunur; Σ/progress/Eşit dağıt/uyarı çalışır. **Razor+css+resx → FLEET RESTART; js → Ctrl+F5.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-R · Ürün E4 rötuş — başlık sırası + Eşit dağıt ikonu + satır-başı opsiyonel SKU metni (mockup) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 5d382c57 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-R-product-e4-refine.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderProducts 550-571, sync product döngüsü 761-773, ProductLinesJson 817) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (productSection ~244-280, Eşit dağıt ikon 276) · _IndexL10n.cshtml (L köprüsü 17-33) · wwwroot/assets/css/strategy-create.css (.st-prod-row 536-561) · 7 resx (Resources/Views/CRM/StrategyTemplates/StrategyTemplatesIndex.<lang>.resx). Mockup: kullanıcı ekran görüntüsü (ÜRÜN|SKU|YÜZDE; SKU=opsiyonel text; başlık "Ürün + SKU% — Ne").

NE (frontend; backend/contract/MDM/Controller DEĞİŞMEZ):
 1) resx ProductSection → "Ürün + SKU% — Ne" (7 dil).
 2) _Form.cshtml:276 Eşit dağıt ikonu bx-distribute-horizontal → bx-equalizer (iconify'da bx-distribute-horizontal YOK → tofu). Diğer bx-distribute-* varsa değiştir.
 3) renderProducts satırına ürün etiketi ile YÜZDE arasına opsiyonel SKU text input: <input type="text" class="form-control form-control-sm js-sku" placeholder="${L.SkuOptionalPlaceholder}" value="${esc(line.notes ?? '')}" ...>. Değer line.notes'a bağlı (contract SkuAllocationInput.GskuId Guid → serbest metin gsku-allocation'a MAP EDİLMEZ; Notes serbest-metin alanı). Kaldır ikon (btn-icon bx-trash) KORU.
 4) sync [data-row="product"] döngüsüne: line.notes = row.querySelector('.js-sku')?.value?.trim() || null; (js-weight okuması KORU; globalProductId/skuAllocationMode/skuAllocations OKUNMAZ). ProductLinesJson=JSON.stringify(state.products) değişmez.
 5) #productLineList üstüne kolon başlık satırı ÜRÜN | SKU | YÜZDE (küçük uppercase text-muted; kolon genişlikleri satır layout'uyla hizalı); resx ProductColProduct/ProductColSku/ProductColWeight (7 dil).
 6) css: .st-prod-row 4 kolon (ürün-main esnek | sku sabit | weight sabit | kaldır) + .js-sku stili + başlık hizası; progress/Σ/Eşit dağıt/uyarı KORU.
 7) _IndexL10n whitelist'e SkuOptionalPlaceholder (+ renderProducts'ın okuduğu diğer yeni anahtar); PascalCase köprü.
KORU/YAPMA: backend/contract/MDM/Controller/ViewModel DEĞİŞMEZ; ProductLinesJson sözleşmesi korunur (gelen sku-allocated satır datası WIPE EDİLMEZ; sync yalnız js-weight + js-sku→notes okur); line weight Σ + Eşit dağıt + progress + #productWeightWarn + aramalı picker + sabit ürün etiketi + loadAll (EDIT-P/Q) korunur; gsku picker/izin (F-GSKU-PICKER-PERM) BU WP'DE YOK (SKU serbest metin); Segment/Frekans/İçerik/KAPSAM/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/MdmService DOKUNMA; uydurma yok; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+_Form+_IndexL10n+css+7 resx; backend/Controller/ViewModel/MDM/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-R — Ürün E4 rötuş: başlık 'Ürün + SKU% — Ne' + Eşit dağıt ikonu + satır opsiyonel SKU metni (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: line.notes başka yerde okunuyorsa (SKU ile çakışma); SKU input round-trip'te weight/skuAllocations'ı bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 6966b524 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitr-verify @6966b524
```
- ✅ **Kapsam (11 dosya, +86/−12):** form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx. **backend/Controller/ViewModel/MDM/liste/detay TEMİZ** ✓.
- ✅ Başlık `ProductSection` → "Ürün + SKU% — Ne" (7 dil). Eşit dağıt ikonu `bx-distribute-horizontal` (iconify'da tanımsız→tofu) → **`bx-equalizer`** (kod tabanındaki tek `bx-distribute-*` kullanımı). Kolon başlığı ÜRÜN|SKU|YÜZDE (satır varken).
- ✅ **SKU serbest metin:** renderProducts satırında `input.js-sku` (maxlength 200, placeholder `SkuOptionalPlaceholder`, value `line.notes`); sync `line.notes = js-sku.trim() || null` (778); **js-weight okuması + skuAllocationMode/skuAllocations DOKUNULMAZ** (KORU) → gelen sku-allocated satır WIPE edilmez; `ProductLinesJson = JSON.stringify(state.products)` değişmez.
- ✅ **DUR temiz:** ürün `line.notes` UI'da başka yerde okunmuyor (yalnız push'ta null seed) → SKU→Notes çakışmasız.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: başlık + ikon + SKU kolonu. **KULLANICI YENİ İSTEK (→ WP-ST-EDIT-S):** SKU serbest-metin → **aramalı dropdown** (seçilen global product'ın SKU'ları; global product picker gibi). Bu, gerçek gsku picker + F-GSKU-PICKER-PERM iznini geri getirir; ayrı WP + izin bağımlılığı değerlendirilecek.

**WP-ST-EDIT-R KOMPLE (E2). SKU-dropdown WP-ST-EDIT-S'de.**
