# WORK PACKAGE — WP-ST-EDIT-C · Düzenle layout temizliği (Sınıflandırma+orta-Lifecycle kaldır, tarih→sağ panel, checklist ikon, font) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`b787f41a` üstü). **E4 rötuşu (Faz 2b canlı):** sayfa 3-kolon oldu (eski _Form Classification+Lifecycle bölümleri orta kolon olarak kaldı). Mockup 2-kolon (sol bölümler + sağ panel). **Frontend** (_Form + _SidePanel + form.js + css + resx). Backend DEĞİŞMEZ.

## İstekler (kullanıcı, canlı ekran)
1. **SINIFLANDIRMA kartı KALDIR** + **Özne tipi (SubjectType) seçimi KALDIR.** Mockup'ta yok — SubjectType **ilk bağlı segmentten türetilir** (gizli alan; backend zorunlu ve homojen: mismatch→400). Segment section zaten "kişi/kurum tipi"ni gösterir.
2. **Orta YAŞAM DÖNGÜSÜ (eski _Form Lifecycle) kartı KALDIR;** **Geçerlilik başlangıcı* / Geçerlilik bitişi** alanlarını **sağ panel YAŞAM DÖNGÜSÜ** kartına taşı (Durum + Kaydet/Taslak/Yeni sürüm/Arşivle zaten sağ panelde — 2b).
3. **BÖLÜMLER checklist ikonları görünmüyor** → düzelt (glyph). `_SidePanel` `<i class="bx st-check-icon">` spesifik boxicon glyph'i taşımıyor; form.js state'e göre (✓/!/–) doğru `bx-*` class'ı eklemeli (ör. tamam=bx-check-circle/text-success, eksik=bx-error-circle/text-warning, opsiyonel=bx-minus-circle/muted) veya css glyph. İkon her durumda render olmalı.
4. **Sağ panel fontları → Task Center create fontları.** `/Tasks/Create` kart tipografisiyle hizala (aile=tema `var(--bs-body-font-family)`, kart başlığı uppercase + boyut/ağırlık, label/value boyutları). Sağ panel şu an farklı tipografi kullanıyor.

## Kanıt
- `_Form.cshtml`: Classification card (SubjectType select + [2a sonrası opak BU kaldırılmıştı]) + Lifecycle card (TemplateStatus disabled + EffectiveFrom* + EffectiveTo). Bunlar orta kolon.
- `_SidePanel.cshtml` (2b): sağ panel — BU OYUN NE YAPACAK + BÖLÜMLER (`st-check` `<i class="bx st-check-icon">` glyph'siz) + YAŞAM DÖNGÜSÜ (Durum + aksiyonlar; tarih YOK).
- `form.js`: `el('SubjectType')?.value` (satır 635) + segment filtre `cfg.subjectType || o.subjectType` (satır 75). SubjectType manuel select'ten.
- `strategy-create.css`: `.st-check-icon` yalnız renk; glyph yok.

## NE (frontend; backend DEĞİŞMEZ)
1. **_Form.cshtml:** SINIFLANDIRMA card'ı KALDIR — SubjectType'ı **gizli input**'a çevir (`asp-for="SubjectType"` hidden; edit'te mevcut değer korunur, create'te form.js segmentten set eder). Lifecycle card'ı KALDIR — TemplateStatus zaten sağ panelde; EffectiveFrom/EffectiveTo input'larını sağ panele taşı (aşağıda). Sonuç: sol kolon yalnız Kimlik/KAPSAM/Segment/Frekans/Ürün/İçerik.
2. **_SidePanel.cshtml:** YAŞAM DÖNGÜSÜ kartına **Geçerlilik başlangıcı* + Geçerlilik bitişi** date input'ları (asp-for EffectiveFrom/EffectiveTo, flatpickr-date) + mevcut Durum + aksiyonlar. Form içi (submit korunur).
3. **form.js:** SubjectType manual select yerine — segment binding builder ilk segment eklendiğinde `#SubjectType` (hidden) değerini o segmentin subjectType'ına set etsin; segment picker: SubjectType boşken tüm segmentler, set olunca aynı tiple filtre (homojen). Tüm segmentler kaldırılınca SubjectType tekrar boş/serbest. `whatLabel`/summary SubjectType okumaları hidden'dan çalışır. **checklist ikon glyph fix**: her check state'inde doğru `bx-*` glyph class'ı ekle (render görünür).
4. **strategy-create.css:** sağ panel tipografisi Task Center create kartlarıyla hizala (font-family tema; kart başlık/label/value boyut-ağırlık `/Tasks/Create` kartlarındaki gibi). checklist ikon görünürlüğü (glyph + boyut) garanti.
5. **resx:** SubjectType/Classification label'ları artık kullanılmıyorsa kaldırma zorunlu değil (bırakılabilir); yeni metin gerekmiyorsa dokunma.

## KORU / YAPMA
- Backend DEĞİŞMEZ (SubjectType hâlâ gönderilir — gizli+türev; EffectiveFrom/To submit korunur). 2a KAPSAM cascade + segment/frekans/ürün/içerik binding-builder + SKU%/homojen/pinned + submit/create-update + Yeni sürüm/Arşiv DOKUNMA (yalnız Classification/Lifecycle kaldır + tarih taşı + SubjectType türet + ikon/font). Liste/Detay/details.js/index.js/CrmService/Controller/ViewModel DOKUNMA (SubjectType ViewModel'de kalır, gizli render). Homojenlik: farklı-tip segment eklenmesi engellenir veya backend reddeder (mevcut davranış korunur). Uydurma değer yok. Tema/L10n köprüsü.
- **DUR:** SubjectType türetme segment binding builder'ı bozuyorsa (edit immutable + create derive temiz kurulamıyorsa); tarih taşıma submit/model-binding'i kırıyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form + _SidePanel + form.js + css (+ resx gerekirse). CrmService/Controller/ViewModel/Liste/Detay diff YOK.
- **E4:** Düzenle 2-kolon (SINIFLANDIRMA + orta Lifecycle yok); tarih sağ panelde; BÖLÜMLER ikonları görünür (✓/!/–); sağ panel fontları Task Center gibi; SubjectType segmentten türeyip kaydediliyor. **Razor+css → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-C · Düzenle layout temizliği + checklist ikon + font (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: b787f41a üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-C-layout-cleanup.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/{_Form.cshtml (Classification+Lifecycle card'ları), _SidePanel.cshtml (YAŞAM DÖNGÜSÜ+checklist)} + wwwroot/assets/js/CRM/StrategyTemplates/form.js (SubjectType ~635, segment filtre ~75, checklist update) + wwwroot/assets/css/strategy-create.css (.st-check-icon ~182) · (font referansı) /Tasks/Create kart tipografisi (Views/Tasks/Create.cshtml + ilgili css). Mockup: /c/tmp/mockup-strategy.html Düzenle.

NE (frontend; backend DEĞİŞMEZ):
 1) _Form.cshtml: SINIFLANDIRMA card KALDIR — SubjectType'ı hidden input yap (asp-for, edit değer korunur). Lifecycle card KALDIR (TemplateStatus sağ panelde; EffectiveFrom/EffectiveTo sağ panele taşınır). Sol kolon: Kimlik/KAPSAM/Segment/Frekans/Ürün/İçerik.
 2) _SidePanel.cshtml: YAŞAM DÖNGÜSÜ kartına Geçerlilik başlangıcı*+bitişi date input (asp-for EffectiveFrom/EffectiveTo flatpickr) + mevcut Durum + aksiyonlar. Form içi (submit korunur).
 3) form.js: SubjectType manuel select yerine ilk bağlı segmentten #SubjectType(hidden) set; picker boşken tüm segment, set olunca aynı tiple filtre; segment kalmayınca serbest. checklist ikon glyph fix: her state'e doğru bx-* (tamam bx-check-circle/success, eksik bx-error-circle/warning, opsiyonel bx-minus-circle/muted) — render görünür.
 4) strategy-create.css: sağ panel tipografi Task Center create kartları gibi (font-family tema var(--bs-body-font-family); başlık/label/value boyut-ağırlık); checklist ikon görünürlüğü.
KORU/YAPMA: backend DEĞİŞMEZ (SubjectType gizli+türev gönderilir; tarih submit korunur); 2a cascade + binding-builder + SKU%/homojen/pinned + submit/create-update + yeni-sürüm/arşiv DOKUNMA; Liste/Detay/details.js/index.js/CrmService/Controller/ViewModel DOKUNMA; homojenlik korunur; uydurma yok; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+_SidePanel+form.js+css(+resx); CrmService/Controller/ViewModel/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-C — Düzenle 2-kolon (Sınıflandırma/orta-Lifecycle kaldır, tarih→sağ panel), checklist ikon + font (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: SubjectType türetme segment builder'ı bozuyorsa; tarih taşıma submit/binding'i kırıyorsa; kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 7c175a1e · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steditc-verify @7c175a1e
```
- ✅ **Kapsam (4 dosya, +95/−79):** _Form.cshtml + _SidePanel.cshtml + strategy-create.css + form.js. **backend/liste/detay/controller/ViewModel/details.js/index.js TEMİZ** ✓. resx değişmedi.
- ✅ **SINIFLANDIRMA + Özne tipi kaldırıldı:** SubjectType artık `<input type="hidden" asp-for="SubjectType">`; `activeSubjectType()` create'te ilk bağlı segmentten türetir (edit'te immutable server değeri), `syncSubjectType()` her renderSegments/sync'te hidden'a yazar (submit'te otoriter). Segment picker: tip boşken tüm segment, set olunca aynı tiple filtre (homojen), boşalınca serbest. Sol kolon tam-genişlik.
- ✅ **Orta Lifecycle kaldırıldı:** TemplateStatus hidden (display-only); EffectiveFrom*/EffectiveTo → sağ panel YAŞAM DÖNGÜSÜ (aside form-dışı → `form="strategyTemplateForm"` ile POST'a dahil; kaydet butonlarıyla aynı ilişki).
- ✅ **Checklist ikon KÖK NEDEN fix:** `norm()` form.js'de tanımsızdı → `updateSidePanel` ReferenceError atıp hiç çalışmıyordu (ikonlar + tüm panel güncellemesi kırıkmış). `norm` helper eklendi → `setCheck` bx-check-circle/bx-error-circle/bx-minus-circle stampliyor.
- ✅ **Font:** `.st-side-panel { font-family: var(--bs-body-font-family) }` + px→rem (Task Center `.text-heading.fw-semibold`/`.form-label.fw-medium` boyut-ağırlık); checklist ikon 1rem.
- ✅ **KORU=0:** 2a cascade + binding-builder + SKU%/homojen/pinned + submit/new-version/archive dokunulmadı.
- ⚠️ **Kabul edilen minör:** EffectiveFrom artık `<form>` DOM-dışı (form= ile bağlı) → jQuery unobtrusive inline client-validation yok; server `[Required]` + validation-span POST'ta enforce/gösterir. Fonksiyonel sorun yok (test yeşil).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: 2-kolon + tarih sağ panel + checklist ikonları görünür + font + SubjectType türeyip kaydediliyor. **Razor+css → FLEET RESTART.**

**WP-ST-EDIT-C KOMPLE. Düzenle sayfası mockup'a hizalı (2-kolon). Kalan: Faz 3 (Detay).**
```
