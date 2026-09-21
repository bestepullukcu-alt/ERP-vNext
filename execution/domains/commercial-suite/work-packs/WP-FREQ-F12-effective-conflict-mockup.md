# WORK PACKAGE — WP-FREQ-F12 · GEÇERLİLİK (04) + ÇAKIŞMA OLURSA (05) mockup birebir + app field (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F9/F10/F11 landing sonrası HEAD üstü** — aynı dosyalar, SIRALI; F10+F11 ile birlikte tek ajanda). **Frontend only** (_Editor.cshtml GEÇERLİLİK+ÇAKIŞMA + form.js renderBandCards/source + visit-frequency-create.css + L10n). buildPayload/id/priority KORUNUR (vfpEffectiveFrom/vfpEffectiveTo/vfpPriority/vfpSource).

## Somut istekler (mockup resim-2 vs mevcut resim-1)
1. **Kart içerik app font (task-create):** GEÇERLİLİK + ÇAKIŞMA kartları içeriği app font family/size (HEDEF'teki .vfp-target-section deseni gibi — Inter yalnız gerekli vurgu). Başlıklar zaten app (F5) — içerik de app.
2. **GEÇERLİLİK alanları:** EffectiveFrom/EffectiveTo → app field (label `form-label fw-medium` + diten-field + `form-control` date, ikon bx-calendar). EffectiveTo placeholder "süresiz". Not muted. (Mevcut vfp-label/vfp-input → app.)
3. **ÇAKIŞMA band kartları (kritik):** şu an YATAY kartlar + değer badge (100/300…). Mockup = **DİKEY stacked radio-kart**: her satır tam genişlik, solda radio + başlık (bold) + açıklama; seçili = mor border+bg. **Değer badge YOK** (mockup). Band value→priority eşlemesi (buildPayload vfpPriority) KORUNUR — yalnız görsel dikey radio-kart. 5 tier (override-all/campaign-level/standard/baseline/last-resort) contract'tan + Band_+Desc L10n.
4. **Source ("Bu kural nereden geliyor?"):** → **select2 arama'lı** (task-create). Not muted korunur.

## Kapsam
- `_Editor.cshtml` (GEÇERLİLİK + ÇAKIŞMA bölümleri): app field (date), band kart host, source select. Kart içerik app font (wrapper sınıfı).
- `form.js`: `renderBandCards` → dikey radio-kart markup (radio+title+desc, seçili mor); source select2 init (options sonrası). Band value/priority/checkedValue('vfpPriority')/validation/buildPayload/id KORUNUR.
- `visit-frequency-create.css`: dikey band radio-kart (`.vfp-band-*` stacked, seçili mor), GEÇERLİLİK app field, source select2, kart içerik app font. Tema-duyarlı.
- L10n: başlık/not mevcut; Band_+Desc F1'de mevcut.

## KORU / YAPMA
- buildPayload/id/priority(band value)/validation DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler (Kimlik/Hedef/Frekans) DOKUNMA (yalnız GEÇERLİLİK+ÇAKIŞMA). Hardcode vocabulary/band yok (contract+L10n). Tema-duyarlı (--bs-* token). Yalnız _Editor(04+05)+form.js(band/source)+css+resx.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + css + resx. Backend/liste/detay/diğer bölüm diff YOK. priority/buildPayload korunmuş.
- **E4:** GEÇERLİLİK app date field (süresiz placeholder); ÇAKIŞMA dikey radio-kart (seçili mor, değer badge yok); Source select2 arama'lı; kart içerik app font; priority/kaydetme aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F9/F10/F11 landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F12 · GEÇERLİLİK (04) + ÇAKIŞMA OLURSA (05) mockup birebir + app field (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F11 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F12-effective-conflict-mockup.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (GEÇERLİLİK+ÇAKIŞMA) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (renderBandCards/checkedValue vfpPriority/source) · frontend/Diten.Web/Views/Tasks/Create.cshtml (diten-field/select2).

NE (frontend; buildPayload/id/priority KORUNUR; yalnız 04+05):
 1) GEÇERLİLİK: EffectiveFrom/EffectiveTo → app field (form-label fw-medium + diten-field + form-control date, bx-calendar); EffectiveTo placeholder "süresiz"; not muted.
 2) ÇAKIŞMA band kartları → DİKEY stacked radio-kart (tam genişlik satır: radio+başlık bold+açıklama; seçili mor border+bg; DEĞER BADGE YOK). renderBandCards markup dikey; band value→priority/checkedValue('vfpPriority')/buildPayload KORUNUR; 5 tier contract'tan + Band_+Desc L10n.
 3) Source "Bu kural nereden geliyor?" → select2 arama'lı (options sonrası init); not korunur.
 4) GEÇERLİLİK+ÇAKIŞMA kart içerik app font.
KORU/YAPMA: buildPayload/id/priority/validation DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; hardcode vocabulary/band yok; tema-duyarlı; yalnız _Editor(04+05)+form.js(band/source)+css+resx.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css+resx; backend/liste/detay/diğer bölüm diff yok; priority/buildPayload korunmuş. Ayrı commit. §22 TÜRKÇE. K13.
Durma: priority(band value)/buildPayload/id bozuluyorsa; select2 source'u etkiliyorsa; kapsam 04+05 dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)** — F11 ile birleşik commit
```
Commit: 56344521 (F11+F12) · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-fa-verify @56344521
```
- ✅ GEÇERLİLİK: EffectiveFrom/EffectiveTo app date field (diten-field bx-calendar, "süresiz" placeholder). ÇAKIŞMA: band → dikey stacked radio-kart (`.vfp-band-*`, seçili mor, değer badge kaldırıldı); band value→priority/buildPayload korundu (weightText selector güncellendi, salt görsel). Source select2 arama (F9 köprüsü). Kart içerik app font (`.vfp-app-section`).
- ✅ buildPayload/priority/allow-map silinmemiş (grep 0). Kapsam: _Editor+form.js+css+7 resx; backend/detay/liste/diğer bölüm = 0. Diten.Web.Tests 137/0.
- ⚠️ E4: band seçimi→priority + select2 + cadence tarayıcıda test.
```
