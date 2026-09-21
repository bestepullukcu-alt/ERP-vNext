# WORK PACKAGE — WP-FREQ-DET-E · DURUM AKIŞI aktör-isim + tasarım + ETKİ rakam stili (backend+frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**DET-D landing sonrası HEAD üstü** — analysis handler + Details/details.js/css SIRALI). Kullanıcı: (1) DURUM AKIŞI'nda aktör **GUID** görünüyor ("c5769c62…") → isim ("M. Arslan") + tasarım mockup gibi (renkli nokta/tarih/isim); (2) ETKİ rakamları mockup gibi (büyük-kalın + binlik ayraç). **Backend (timeline aktör-isim) + frontend (timeline tasarım + ETKİ stil).**

## Kök neden
`GetVisitFrequencyPolicyAnalysisHandler.BuildTimelineAsync` timeline entry'lerinde `e.By`/`policy.CreatedBy`/`policy.ArchivedBy`'ı **ham** (actor GUID) geçiyor; isim çözümü yok. `IUserDisplayNameResolver`/`AuthUserDisplayNameClient` (DETAILS6, S2S fail-closed) MEVCUT ve Segment'te (`GetSegmentByIdHandler.EnrichWithDisplayNamesAsync`) kullanılıyor — reuse edilecek.

## Kapsam
### 0) Frontend — TEK KART layout (mockup, DET-D dağınık kaldı)
Mockup: sol **TEK** `card` içinde: header (name/status/code/desc + Düzenle/Archive) → **4'lü stat şeridi** (kart-içi bordered hücre, ayrı kart DEĞİL) → **BAĞLAM** (kart-içi bölüm) → **NOTES** (kart-içi bölüm). Altında ÇAKIŞAN POLİTİKALAR ayrı kart. Sağ kolon (col-lg-4): DURUM AKIŞI + ETKİ — header ile aynı üst hizada başlar. Mevcut DET-D: header ayrı kart + stat'lar ayrı ayrı kart + BAĞLAM/NOTES ayrı kart (dağınık) → **hepsini sol tek karta birleştir**; 2-kolon row (sol col-lg-8 tek ana kart + altında çakışanlar / sağ col-lg-4 timeline+etki). Details.cshtml + css; id'ler korunur (details.js/DET-A/DET-C tüketimi aynı).

### 1) Backend — timeline aktör-isim (analysis handler)
- `GetVisitFrequencyPolicyAnalysisHandler`'a `IUserDisplayNameResolver` enjekte et. `BuildTimelineAsync`: tüm entry'lerin `By` (event.By + CreatedBy + ArchivedBy) GUID'lerini topla → **bulk** `ResolveAsync` → display name'e çevir. Çözülemeyen (fail-closed) → kısa id (uydurma yok). TimelineEntryDto.By = çözülen isim (veya kısa id). GetSegmentByIdHandler desenini reuse. Tenant-scoped. **Diğer analysis mantığı (impact/conflicts/next-eval) DEĞİŞMEZ.**

### 2) Frontend — DURUM AKIŞI tasarım (mockup)
- `details.js` timeline render + `visit-frequency-details.css`: mockup gibi — her giriş **renkli nokta** (tipine göre: created=turuncu/gri, published=yeşil, weight-changed=mor, deactivated/archived=gri, next-eval=soluk) + başlık (weight-changed→"Ağırlık {from}→{to}") + tarih + **aktör adı** (artık isim). Dikey timeline çizgisi + nokta hizası mockup gibi. Tema-duyarlı.

### 3) Frontend — ETKİ rakam stili (mockup)
- ETKİ bloğu: sayı **büyük-kalın** (`312`, `1.248` — **binlik ayraç** tr locale/`toLocaleString`) + altında/yanında **küçük muted** açıklama ("hedef bu politikayı alıyor" / "planlanan ziyaret / çeyrek"). Şu an düz inline metin → mockup büyük-sayı formatına getir. computable=false → Note metni (DET-D).

## KORU / YAPMA
- analysis impact/conflicts/next-eval MANTIĞI DEĞİŞMEZ (yalnız timeline By→isim). resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/DET-A counter/DET-C event append DOKUNMA (yalnız analysis handler timeline By resolve + Details/details.js/css). Uydurma isim/rakam YOK (fail-closed→kısa id). AuthUserDisplayNameClient reuse (yeni client yok). app-card kabuk; tema/L10n. TenantId.

## Acceptance
- **E2:** CrmService.Application.Tests baseline-diff sıfır-yeni-fail (PII order-flake hariç) + Diten.Web.Tests 137/0. git diff: analysis handler (By resolve) + details.js + css (+ test?). impact/conflicts/next-eval + resolve/CRUD/liste/editör diff YOK.
- **E4:** DURUM AKIŞI aktör **isimle** (GUID değil) + mockup tasarım (renkli nokta/tarih); ETKİ büyük-kalın rakam + binlik ayraç.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-D landing SONRASI)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-FREQ-DET-E · DURUM AKIŞI aktör-isim + tasarım + ETKİ rakam stili (MOD-0165-FU03, backend+frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-D commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-E-timeline-actor-etki-style.md · services/.../VisitFrequencyPolicy/Handlers/GetVisitFrequencyPolicyAnalysisHandler.cs (BuildTimelineAsync, By ham geçiyor) · services/.../Application/Common/IUserDisplayNameResolver.cs + Infrastructure/Auth/AuthUserDisplayNameClient.cs + Segmentation/.../GetSegmentByIdHandler.cs (EnrichWithDisplayNamesAsync deseni — REUSE) · frontend/.../details.js + Details.cshtml + visit-frequency-details.css.

NE:
 0) Frontend TEK KART layout (Details.cshtml+css): sol TEK card = header(name/status/code/desc+Düzenle/Archive) → 4'lü kart-içi bordered stat şeridi (ayrı kart DEĞİL) → BAĞLAM kart-içi bölüm → NOTES kart-içi bölüm; altında ÇAKIŞAN POLİTİKALAR ayrı kart; sağ col-lg-4 DURUM AKIŞI+ETKİ (header üst hizasında). DET-D'nin dağınık ayrı-kartlarını sol tek karta BİRLEŞTİR. id'ler korunur (details.js/DET-A/DET-C tüketimi aynı).
 1) Backend: GetVisitFrequencyPolicyAnalysisHandler'a IUserDisplayNameResolver enjekte; BuildTimelineAsync tüm By (event.By+CreatedBy+ArchivedBy) GUID'lerini bulk ResolveAsync ile display name'e çevir (fail-closed→kısa id; uydurma yok). TimelineEntryDto.By=çözülen isim. GetSegmentByIdHandler desenini reuse. impact/conflicts/next-eval MANTIĞI DEĞİŞMEZ. Tenant-scoped.
 2) Frontend DURUM AKIŞI (details.js+css): mockup — renkli nokta (created turuncu/gri, published yeşil, weight-changed mor, deactivated/archived gri, next-eval soluk) + başlık (weight-changed "Ağırlık from→to") + tarih + aktör adı; dikey timeline çizgisi. Tema-duyarlı.
 3) Frontend ETKİ (details.js+css): sayı büyük-kalın + binlik ayraç (toLocaleString tr) + küçük muted açıklama; computable=false→Note.
KORU/YAPMA: analysis impact/conflicts/next-eval mantığı DEĞİŞMEZ (yalnız timeline By→isim); resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/DET-A counter/DET-C event append DOKUNMA; AuthUserDisplayNameClient reuse (yeni client yok); uydurma isim/rakam yok; app-card/tema/L10n; TenantId.
DOĞRULA (E2): CrmService.Application.Tests baseline-diff sıfır-yeni-fail (ContactLocationPiiHardeningTests PII PRE-EXISTING order-flake — yeni sayma) + Diten.Web.Tests 137/0 (Release); git diff analysis handler+details.js+css(+test); impact/conflicts/resolve/liste/editör diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: display-name resolver reuse edilemiyorsa; timeline By resolve impact/conflicts'i etkiliyorsa; kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: b9e6b830 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-dete-verify @b9e6b830
```
- ✅ **Kapsam:** analysis handler (timeline By→isim) + test + details.js + css. **KORU=0** (Details.cshtml/resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/DET-A counter/DET-C event dokunulmadı).
- ✅ **Layout:** Details.cshtml ZATEN tek-kart (DET-D `c7617529`): sol tek `<section class="card">` içinde header + 4 `vfp-det-stat-cell` + BAĞLAM/NOTES `vfp-det-inner-section`; altında çakışanlar; sağ col-lg-4 timeline+ETKİ. **Kullanıcının dağınık-kart gördüğü ekran/HTML STALE'di (DET-D öncesi render).**
- ✅ **Timeline aktör-isim:** `ResolveActorNamesAsync` — By GUID'leri bulk `IUserDisplayNameResolver.ResolveAsync` ile isme (GetSegmentByIdHandler reuse); fail-closed→kısa id. impact/conflicts/next-eval değişmedi.
- ✅ **DURUM AKIŞI tasarım:** TIMELINE_TONE renkli nokta + dikey çizgi (#vfpDetTimeline scope'lu; Resolve trail etkilenmez). **ETKİ:** büyük-kalın 28px + `toLocaleString('tr-TR')` binlik ayraç.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0** + CrmService.Application.Tests **1786/0/5**.
- ⚠️ **AÇIK BUG (DET-F):** `.vfp-dv-tl-title`/`.vfp-dv-tl-detail` span+`display:block` YOK (css satır 134-135) → başlık ve tarih·aktör **aynı satırda bitişik**. Kullanıcının "tarih·aktör alt satırda olmalı" isteği → DET-F 1-2 satır css fix.

**DET-E KOMPLE. Sıra: DET-F (timeline title/detail stacking css fix).**
```
