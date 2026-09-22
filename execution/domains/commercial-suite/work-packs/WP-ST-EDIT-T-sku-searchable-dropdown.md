# WORK PACKAGE — WP-ST-EDIT-T · Ürün satırı SKU hücresi: serbest metin → aramalı gsku select2 (mockup: global product gibi) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`6966b524` üstü — WP-ST-EDIT-R §37 sonrası). **Kullanıcı isteği:** SKU hücresi (EDIT-R serbest metin) → **aramalı dropdown** (global product picker gibi), seçilen SKU gerçek MDM GSKU referansı. **Frontend** (form.js + strategy-create.css + gerekirse _IndexL10n). Backend/contract/MDM/izin DEĞİŞMEZ.
>
> **CT ön-araştırma (canlı):** (a) 97c5 Admin izinleri TAM — `mdm.finished-goods.create`/`.read` + `mdm.gskus.read` grant=1 → gsku picker gate (`can('gsku')`) **zaten açık**; F-GSKU-PICKER-PERM Admin için engel değil. (b) EDIT-Q gsku'yu **zaten** `loadAll('/gskus')` ile `options.gsku`'ya yüklüyor. (c) **`mdm_gskus` = 0 doküman** → dropdown, GSKU master verisi seed edilene kadar BOŞ gelir (kullanıcı kabul etti). (d) **Ürün-scoping ERTELENDİ (F-GSKU-PRODUCT-SCOPE):** "seçilen ürünün SKU'ları" için Gsku.ProductDefinitionRevisionId→ProductDefinitionRevision.GlobalProductId join'i + selector'a globalProductId filtresi gerekir; 0 GSKU + 0 product-definition verisine karşı inşa/E2-doğrulanamaz. Veri gelince ayrı backend WP. Bu WP **tüm-SKU aramalı dropdown** kurar (mockup davranışı; veriyle scoping sonra).

## Kanıt
- `form.js` renderProducts (550-571): SKU hücresi şu an `<input type="text" class="js-sku" value="${line.notes}">` (EDIT-R). Kaldırılıp gsku select2 gelecek.
- `form.js` sync product döngüsü (773-779): `line.notes = js-sku.value` (EDIT-R). Kaldırılıp skuAllocations/mode okumasına çevrilecek.
- `form.js` `pickerSelect(kind, value, allowed, unavailableText, listOverride)` (237): `<select class="form-select form-select-sm" data-kind>` + head boş option `—` + options[kind]. `options.gsku` (198-203) `loadAll` ile dolu ({id, text: gskuCanonicalCode||canonicalCode}). `can('gsku')` (30).
- `form.js` select2: `bindSelect2(select, {search})` (61) + `unbindSelect2` (76); satır-içi bağlama deseni segment rolde: `el('segmentBindingList').querySelectorAll('.js-role').forEach(bindRoleSelect2)` (87). renderSegments sonrası bind; native change köprüsü.
- `contract` StrategyTemplateModels: `ProductLineInput.SkuAllocations` = `[{GskuId(Guid), Percentage(decimal)}]` + `SkuAllocationMode`. Tek-SKU satır = `[{gskuId, 100}]` + mode `sku-allocated`. `lineTotal` (473) = Σ satır-içi SKU% (tek SKU=100 → geçer). Σ-doğrulama (1036): sku-allocated satırda lineTotal===100.
- `lineWeightPercentage` (YÜZDE, satırlar-arası Σ=100) = EDIT-P/Q, DEĞİŞMEZ.

## NE (frontend; backend/contract/MDM/izin DEĞİŞMEZ)
1. **SKU hücresi → aramalı gsku select2:** renderProducts satırında `.js-sku` text input'u **kaldır**, yerine gsku select: `pickerSelect('gsku', skuId, can('gsku'), L.GskuPickerUnavailable)` + `.js-sku` sınıfı (pickerSelect'e opsiyonel extraClass param ekle **veya** SKU select'i inline pickerSelect deseniyle kur; head boş option `—` = SKU opsiyonel). `skuId = (line.skuAllocations || [])[0]?.gskuId`. Placeholder = `L.SkuOptionalPlaceholder` (reuse; "opsiyonel"). `!can('gsku')` → disabled + `GskuPickerUnavailable`. frozen → disabled.
2. **select2 bağlama:** renderProducts sonunda `el('productLineList').querySelectorAll('.js-sku').forEach(s => bindSelect2(s, { search: true }))` (aramalı; global product picker gibi). Yeniden render öncesi teardown gerekiyorsa `unbindSelect2` (EDIT-O deseni). `dropdownParent` satır hücresi. jQuery/select2 yoksa düz select degrade.
3. **sync (storage → skuAllocations):** product döngüsünde `line.notes = js-sku…` satırını **KALDIR**. Yerine: `const g = row.querySelector('.js-sku')?.value || '';` → `if (g) { line.skuAllocations = [{ gskuId: g, percentage: 100 }]; line.skuAllocationMode = 'sku-allocated'; } else { line.skuAllocations = []; line.skuAllocationMode = 'product-only'; }`. `js-weight`→lineWeightPercentage okuması KORUNUR. globalProductId OKUNMAZ (KORU). `notes` artık SKU taşımaz (null bırak).
3b. **Değer okuma (renderProducts):** satır SKU değeri = `(line.skuAllocations||[])[0]?.gskuId` (EDIT-R'nin `line.notes` okuması kaldırılır).
4. **css:** `.st-prod-sku` kolonunda select2 genişliği/hizası (mevcut 160px veya mockup'a uygun genişlet); select2 container `.st-prod-row`/`.st-prod-head` hizasını bozmasın. Progress/Σ/Eşit dağıt/uyarı KORUNUR.
5. **L köprüsü:** yeni anahtar gerekmiyorsa dokunma (SkuOptionalPlaceholder + GskuPickerUnavailable zaten köprüde). Gerekirse ekle.

## KORU / YAPMA
- Backend/contract/**MDM/izin/Controller/ViewModel DEĞİŞMEZ**. `lineWeightPercentage` (YÜZDE) + Σ + Eşit dağıt + progress + `#productWeightWarn` + aramalı "Ürün ekle" picker + sabit ürün etiketi + sayfalı loadAll (EDIT-P/Q/R) + başlık/ikon/kolon (EDIT-R) DEĞİŞMEZ. **Tek-SKU model (mockup):** satır bir GSKU taşır → skuAllocations=[{gskuId,100}]. **Gelen çok-SKU'lu satır (>1 skuAllocation) varsa** dropdown ilkini gösterir ve sync tek'e indirger — 0 GSKU verisi olduğu için şu an kayıp yok; **veri kaybı riski varsa DUR+raporla.** Ürün-scoping (F-GSKU-PRODUCT-SCOPE) BU WP'DE YOK (tüm-SKU). renderProducts/sync dışı (Segment/Frekans/İçerik/KAPSAM/sağ panel) DOKUNMA. Liste/Detay/details.js/CrmService/MdmService DOKUNMA. Uydurma SKU yok. Tema/L10n köprüsü.
- **DUR:** gelen çok-SKU'lu satırı tek select2'ye indirgemek veri kaybediyorsa; SKU select2 sync'i skuAllocations/lineWeightPercentage'ı bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + strategy-create.css (+ _IndexL10n gerekirse). **Backend/Controller/ViewModel/MDM/liste/detay diff YOK.**
- **E4 (FLEET RESTART/js Ctrl+F5):** SKU hücresi **aramalı select2** (global product picker gibi); `mdm_gskus` boş olduğu için **liste boş** (beklenen — veri seed edilince dolar); SKU seçilirse kaydet→`skuAllocations=[{gskuId,100}]`+mode `sku-allocated`, tekrar aç→geri gelir; SKU boş→product-only; YÜZDE/Σ/progress çalışır. **Not:** dropdown'ı dolu görmek için MDM'e GSKU master verisi (ProductDefinitionRevision+Gsku) gerekir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-T · Ürün satırı SKU hücresi serbest metin → aramalı gsku select2 (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 6966b524 üstü · Worktree: ana checkout

CT ön-araştırma: izinler TAM (gsku picker açık), EDIT-Q gsku'yu options.gsku'ya loadAll ile yüklüyor, mdm_gskus=0 (dropdown veri gelene kadar boş — beklenen). Ürün-scoping ERTELENDİ (bu WP tüm-SKU aramalı dropdown).

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-T-sku-searchable-dropdown.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderProducts 550-571 SKU input EDIT-R, sync product 773-779, pickerSelect 237, options.gsku 198-203, can 30, bindSelect2/unbindSelect2 61/76, segment js-role bind 87) · wwwroot/assets/css/strategy-create.css (.st-prod-sku) · (gerekirse) _IndexL10n.cshtml. contract StrategyTemplateModels ProductLineInput.SkuAllocations (GskuId Guid + Percentage) + SkuAllocationMode — DEĞİŞTİRME.

NE (frontend; backend/contract/MDM/izin DEĞİŞMEZ):
 1) renderProducts: .js-sku text input KALDIR → gsku select: pickerSelect('gsku', (line.skuAllocations||[])[0]?.gskuId, can('gsku'), L.GskuPickerUnavailable) + .js-sku sınıfı (pickerSelect'e opsiyonel extraClass ekle veya inline kur; head boş option '—' = opsiyonel); placeholder L.SkuOptionalPlaceholder; !can('gsku')→disabled+GskuPickerUnavailable; frozen→disabled.
 2) renderProducts sonunda el('productLineList').querySelectorAll('.js-sku').forEach(s=>bindSelect2(s,{search:true})); yeniden render öncesi gerekiyorsa unbindSelect2 (EDIT-O). jQuery yoksa düz select degrade.
 3) sync product döngüsü: line.notes=js-sku… satırını KALDIR → g=row.querySelector('.js-sku')?.value||''; g ? (line.skuAllocations=[{gskuId:g,percentage:100}], line.skuAllocationMode='sku-allocated') : (line.skuAllocations=[], line.skuAllocationMode='product-only'). js-weight→lineWeightPercentage okuması KORU; globalProductId OKUNMAZ; notes SKU taşımaz (null).
 4) css: .st-prod-sku select2 genişlik/hiza (.st-prod-row/.st-prod-head bozulmasın); progress/Σ/Eşit dağıt/uyarı KORU.
 5) L köprüsü: yeni anahtar gerekmiyorsa dokunma (SkuOptionalPlaceholder/GskuPickerUnavailable zaten köprüde).
KORU/YAPMA: backend/contract/MDM/izin/Controller/ViewModel DEĞİŞMEZ; lineWeightPercentage/Σ/Eşit dağıt/progress/#productWeightWarn/aramalı Ürün ekle picker/sabit ürün etiketi/loadAll/başlık/ikon/kolon (EDIT-P/Q/R) DEĞİŞMEZ; tek-SKU model (skuAllocations=[{gskuId,100}]); gelen >1 skuAllocation'lı satırı tek'e indirgeme veri kaybediyorsa DUR; ürün-scoping (F-GSKU-PRODUCT-SCOPE) YOK; Segment/Frekans/İçerik/KAPSAM/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/MdmService DOKUNMA; uydurma yok; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+css(+_IndexL10n); backend/Controller/ViewModel/MDM/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-T — Ürün satırı SKU hücresi aramalı gsku select2 (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: gelen çok-SKU'lu satırı tek select2'ye indirgemek veri kaybediyorsa; SKU select2 sync'i skuAllocations/lineWeightPercentage'ı bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 6298f600 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitt-verify @6298f600
```
- ✅ **Kapsam (2 dosya, +35/−14):** form.js + strategy-create.css. **backend/Controller/ViewModel/MDM/resx/_IndexL10n/liste/detay TEMİZ** ✓.
- ✅ **SKU → aramalı gsku select2:** renderProducts satırında `pickerSelect('gsku', skuId, can('gsku'), GskuPickerUnavailable, null, 'js-sku')` (576; `pickerSelect`'e opsiyonel `extraClass` eklendi, 237); `skuId=(line.skuAllocations||[])[0]?.gskuId` (568); head boş `—` = opsiyonel; `!can('gsku')`→disabled+reason; frozen→disabled. renderProducts sonunda `.js-sku`→`bindSelect2(s,{search:true})` (593; global product picker gibi aramalı).
- ✅ **KORU=0 (storage + wipe koruması):** sync ürün döngüsü `if (can('gsku') && !frozen)` (796) → seçili gsku `skuAllocations=[{gskuId,100}]`+`sku-allocated`, boş `[]`+`product-only`; **izinsiz/frozen aktörde gelen skuAllocations/mode DOKUNULMAZ** (WIPE yok — disabled dalın tek `<option>`'ı reason metni, gskuId sanılmaz). `js-weight`→lineWeightPercentage KORU; globalProductId OKUNMAZ; notes SKU taşımaz. Tek-SKU=100 → Σ-check (1055) geçer. lineWeightPercentage/Σ/Eşit dağıt/progress KORU.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4 (js Ctrl+F5): SKU aramalı select2; **`mdm_gskus`=0 → liste boş (beklenen)**; seçilirse round-trip skuAllocations. Dolu görmek için GSKU master verisi (ProductDefinitionRevision+Gsku) gerekir → **F-GSKU-PRODUCT-SCOPE** (ürüne-göre süzme + veri) ayrı backend WP.

**WP-ST-EDIT-T KOMPLE (E2). Ürün+SKU bölümü mockup-tam. Kalan: GSKU verisi seed + ürün-scoping (veri gelince).**
