# WORK PACKAGE — WP-FREQ-E · Frekans liste konsolu: mockup kolonları + Save View (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`581fad16` üstü). FREQ-A/B/C/D tamam. Bu WP = **liste konsolu iki iş**: (1) DataTable kolonlarını mockup'a hizala (hedef **isim**, AĞIRLIK band etiketi, GEÇERLİLİK, KAYNAK, frekans altyazı) + bizim **Actions** kolonu kalır; (2) **Save View** çalışsın (VFP'ye hiç bağlanmamış — EligibilityPolicies'den porte). **Frontend + resx only; backend değişmez** (liste DTO'sunda tüm alanlar zaten var).

## Referans
- **Mockup (liste):** kolonlar `POLİTİKA` (PolicyName büyük + PolicyCode küçük) · `HEDEF` (targetType çipi + **hedef adı**) · `FREKANS` (badge/"N / dönem" + altyazı **"dönem sınırlı"** [EffectiveTo dolu] / **"süresiz geçerli"** [EffectiveTo boş]) · `GEÇERLİLİK` (EffectiveFrom → EffectiveTo|"süresiz", **dd.MM.yyyy**) · `AĞIRLIK` (band etiketi — ham sayı değil) · `KAYNAK` (source etiketi) · `DURUM` (status badge). Bizim ek: `Actions` (Detay/Düzenle/Arşivle/Sil — mevcut korunur).
- **Hedef isim çözümü:** `resolve.js`'teki `READERS`/`nameOf`/`TARGET_KIND` deseni (picker proxy'leri: segment/account/contact/campaign/concept-node/audience-profile/territory-model+node...). Liste satırları için grup-bazlı çöz, **cache**, bulunamazsa `shortId`'ye düş (uydurma isim YOK).
- **AĞIRLIK band etiketi:** `contract.vocabulary.priorityBands` (`FrequencyPriorityBand` = Code+Value). Satırın `priority` (int) → `band.Value == priority` olan band'ı bul → `Band_{band.Code}` L10n etiketi (bu anahtarlar **zaten var**: Band_segment, Band_account, ...). Eşleşme yoksa ham sayıya düş. **Hardcode band YOK** (contract-driven).
- **Save View (çalışan referans):** `EligibilityPolicies/index.js` — `personalizationContext` (moduleKey/pageKey), `saveDefaultView`/load, `currentView`, `setSaveFilterVisible`, `saveFilterBtn` (exportButtons'a geç), filtre/colvis/sıra değişince "Save View" butonunu göster. Aynı personalization backend'i kullanılır (yeni endpoint YOK).
- **Golden Compact v2:** kolon index'leri değişince `exportColumns`/`colvisColumns`/`colReorder`/`columnDefs` ve `_DataTable.cshtml` başlıkları güncellenir; `data-dt-standard="v2"`, `dt-inline-filter-host`, `updateVisualState` gotcha'ları korunur (memory).

## Kapsam (frontend: _DataTable.cshtml + index.js + _IndexL10n + resx)
1. **Kolonlar (mockup):** POLİTİKA(ad+kod) / HEDEF(çip+isim) / FREKANS(+altyazı) / GEÇERLİLİK / AĞIRLIK(band) / KAYNAK / DURUM / Actions. `_DataTable.cshtml` `<th>` seti + index.js `columns`/`columnDefs`/renderer'lar. Tarih **dd.MM.yyyy**, "→", EffectiveTo boş→"süresiz".
2. **Hedef isim:** targetCell → tip çipi + çözülen isim (resolve.js deseni reuse; cache; degrade shortId).
3. **AĞIRLIK:** priority→band etiketi (contract priorityBands value-match + Band_ L10n). Ham sayı fallback.
4. **KAYNAK:** source→`Source_{code}` L10n (additive, **7 dil**; mockup dili: Kampanya kararı/Saha yöneticisi/Segmentasyon ekibi/Otomatik iş kuralı/Elle girildi/Eski sistemden — contract source kodlarına eşle). Hardcode vocab YOK; kod contract'tan, etiket L10n.
5. **FREKANS altyazı:** "dönem sınırlı"/"süresiz geçerli" (EffectiveTo dolu/boş) — yeni L10n anahtarları (7 dil).
6. **Save View:** EligibilityPolicies personalization wiring'ini VFP'ye porte (saveFilterBtn + context moduleKey=`crm`/pageKey=`visit-frequency-policies` [Elig deseniyle tutarlı] + save/load/currentView + setSaveFilterVisible + change-tetikleyici). L10n `SaveView` zaten var.
7. Filtre (status/targetType/source) + colvis/export yeni index'lerle çalışmaya devam eder.

## KORU / YAPMA
- **Backend DEĞİŞMEZ** (liste DTO tam; yeni endpoint yok). FREQ-B editör (form.js/_CreateEditOffcanvas) + FREQ-C detay/çözümleme (_DetailsQuickView/_Resolve/resolve.js) **DAVRANIŞI DEĞİŞMEZ** — resolve.js'ten desen **reuse** ok ama dosyayı bozma (ortak yardımcı gerekirse index.js'e kopyala/paylaş, resolve.js'i kırma). Segment dosyaları/başka modül DOKUNMA. Golden Compact v2 standardı + gotcha'lar birebir. Vocabulary/band/verdict/source **hardcode YOK** (contract + L10n). Uydurma hedef ismi YOK (degrade shortId). Editör'ün ayrı-sayfa dönüşümü bu WP'de YOK (FREQ-F). buildConfig payload/proxy sözleşmesi bozulmaz.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). git diff: `_DataTable.cshtml` + `index.js` + `_IndexL10n.cshtml` + resx (7 dil) (+ gerekirse css). FREQ-B/C/D + Segment + backend diff yok.
- **E4:** `/CRM/VisitFrequencyPolicies` liste mockup kolonlarıyla gelir: HEDEF isim (GUID değil), AĞIRLIK band etiketi (ham sayı değil), GEÇERLİLİK dd.MM.yyyy/süresiz, KAYNAK etiketi, FREKANS altyazı; Actions çalışır; **Save View** butonu görünüp kaydeder/yükler.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-E · Frekans liste konsolu — mockup kolonları + Save View (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 581fad16 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-E-list-columns-saveview.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/{Index,_DataTable,_IndexL10n}.cshtml + wwwroot/assets/js/CRM/VisitFrequencyPolicies/{index.js,index.l10n.js,resolve.js} · frontend/Diten.Web/wwwroot/assets/js/CRM/EligibilityPolicies/index.js (Save View personalization deseni — referans, DEĞİŞTİRME) · services/.../VisitFrequencyPolicy/VisitFrequencyPolicyDtos.cs (liste alanları) + Contract/VisitFrequencyContract.cs (priorityBands+sources). Mockup görselleri: liste kolonları POLİTİKA/HEDEF/FREKANS/GEÇERLİLİK/AĞIRLIK/KAYNAK/DURUM.

NE (frontend + resx only; BACKEND DEĞİŞMEZ):
 1) Kolonlar (mockup): POLİTİKA(PolicyName+PolicyCode) / HEDEF(targetType çipi + ÇÖZÜLEN isim) / FREKANS(badge + "N / dönem" + altyazı "dönem sınırlı"[EffectiveTo dolu]/"süresiz geçerli"[boş]) / GEÇERLİLİK(EffectiveFrom → EffectiveTo|"süresiz", dd.MM.yyyy) / AĞIRLIK(band etiketi) / KAYNAK(source etiketi) / DURUM(status badge) / Actions(mevcut Detay/Düzenle/Arşivle/Sil KORUNUR). _DataTable.cshtml <th> + index.js columns/columnDefs/renderer; exportColumns/colvisColumns/colReorder yeni index'lere göre.
 2) Hedef isim: resolve.js READERS/nameOf/TARGET_KIND desenini reuse (grup-bazlı, cache, bulunamazsa shortId — uydurma YOK). resolve.js'i BOZMA (ortak kod index.js'e kopyalanabilir).
 3) AĞIRLIK: contract.vocabulary.priorityBands value-match → Band_{code} L10n (mevcut). Eşleşme yoksa ham sayı. Hardcode YOK.
 4) KAYNAK: Source_{code} L10n ekle (additive 7 dil; mockup dili Kampanya kararı/Saha yöneticisi/Segmentasyon ekibi/Otomatik iş kuralı/Elle girildi/Eski sistemden — contract source kodlarına eşle).
 5) FREKANS altyazı L10n (dönem sınırlı/süresiz geçerli, 7 dil).
 6) Save View: EligibilityPolicies wiring'ini porte (saveFilterBtn + personalizationContext moduleKey=crm/pageKey=visit-frequency-policies + saveDefaultView/load/currentView + setSaveFilterVisible + filtre/colvis/sıra değişince göster). Yeni backend endpoint YOK (ortak personalization).
KORU/YAPMA: backend/DTO/contract/proxy DEĞİŞMEZ; FREQ-B(form.js/_CreateEditOffcanvas)+FREQ-C(_DetailsQuickView/_Resolve/resolve.js) davranışı DEĞİŞMEZ (reuse ok, kırma); Segment/başka modül DOKUNMA; Golden Compact v2 + dt-inline-filter-host + updateVisualState gotcha'ları birebir; vocabulary/band/source hardcode YOK (contract+L10n); uydurma isim YOK (degrade shortId); editör ayrı-sayfa dönüşümü bu WP'de YOK; L10n camelCase/PascalCase köprüsü (index.l10n.js) korunur — yeni okunan üst-düzey anahtarlar PascalCase erişilebilir olmalı.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); git diff yalnız _DataTable+index.js+_IndexL10n+resx(+css?); FREQ-B/C/D+Segment+backend diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: liste DTO'sunda alan eksikse (backend gerekirse); hedef isim picker proxy'leri yetmiyorsa; Save View personalization backend'i yoksa/bağlanamıyorsa; kapsam VisitFrequencyPolicies dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: ac2c6480 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqe-verify @ac2c6480
```
- ✅ **Kapsam (10 dosya):** `_DataTable.cshtml` + `_IndexL10n.cshtml` + `index.js` + 7 resx. FREQ-B(form.js/_CreateEditOffcanvas) + FREQ-C(_DetailsQuickView/_Resolve/**resolve.js**) + FREQ-D(index.l10n.js) + Segment + backend/DTO/contract = **0 değişiklik** (grep sayacı 0). Frontend + resx only.
- ✅ **Kolonlar mockup:** POLİTİKA(ad+kod) / HEDEF(çip + çözülen **isim**) / FREKANS(badge + "N / dönem" + altyazı FreqBounded/FreqOpenEnded) / GEÇERLİLİK(EffectiveFrom → EffectiveTo|süresiz, **dd.MM.yyyy**) / AĞIRLIK(band etiketi) / KAYNAK(source etiketi) / DURUM / İşlemler(mevcut Detay/Düzenle/Arşivle/Sil korundu).
- ✅ **Contract-driven / hardcode yok:** AĞIRLIK `bandByWeight = Map(vocab.priorityBands[value→code])` → `bandLabels[code]` (Band_ L10n); eşleşme yoksa ham sayı. KAYNAK `sourceLabels[code]` (Source_ L10n additive 7 dil). Vocabulary kodları contract'tan.
- ✅ **Hedef isim:** resolve.js READERS/mapOption/TARGET_KIND deseni index.js'e kopyalandı (grup-bazlı çöz + cache; bulunamazsa `shortId` — uydurma yok). resolve.js dosyası değişmedi.
- ✅ **columnDefs sort/filter doğru:** target sort=type+name, validity sort=effectiveFrom, weight sort=numeric priority, display≠sort ayrımı korunmuş.
- ✅ **Save View:** EligibilityPolicies deseni porte — `personalizationContext {crm, visit-frequency-policies}` + `currentView`/`saveDefaultView`/`loadDefaultView`/`isDirtyComparedToDefault` + `setSaveFilterVisible` + `saveFilterBtn` (exportButtons); `window.personalizationClient` (global, _LayoutTenantShell). Yeni endpoint yok.
- ✅ **L10n köprüsü:** yeni skaler anahtarlar camelCase serialize + FREQ-D bridge PascalCase alias'lıyor (`L.FreqBounded`/`L.Policy`… çözülür); band/sourceLabels nested Dictionary (camelCase, index.js `L.bandLabels`/`L.sourceLabels`). NavManifestL10nGuard etkilenmez.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0** (6 s).
- ⏳ **E4:** hard-refresh sonrası liste kolonları + Save View kaydet/yükle.

**FREQ-E KOMPLE. Sıra: FREQ-F (editör offcanvas → ayrı Golden Compact Create/Edit sayfası, mockup tam tasarım).**
