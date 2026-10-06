# WORK PACKAGE — WP-FREQ-F7 · HEDEF bölümü (02) mockup birebir + app font/field (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`8c7f460b` üstü). Kullanıcı: editör HEDEF kartı (bölüm 02) mockup resim-1'e hizalansın + font/field app (task-create) olsun. **Frontend only** (_Editor.cshtml HEDEF bölümü + form.js render + visit-frequency-create.css + L10n). **buildPayload/id/data-role/validation KORUNUR** (targetId, businessUnit, territoryNodeId, segmentId, campaignId, brandId, productId, cyclePeriodId).

## Somut istekler (kullanıcı)
1. **Kart font:** HEDEF kartı içeriği task-create fontu (app family + size). Inter yalnız gerekli mockup vurgu (kod badge mono) — genel metin app.
2. **Başlık:** `EditorSectionTarget` "HEDEF" → **"HEDEF — KİME UYGULANIYOR"** (mockup: 02 HEDEF — KIME UYGULANIYOR). L10n 7 dil.
3. **Aside:** `<span class="vfp-section-aside">@Localizer["TargetListsNote"]</span>` → app `wcn-act-outcome` sınıfı ile (`.wcn-act-outcome{font-size:.75rem;color:var(--bs-secondary-color)}` — backbone-custom.css). vfp-section-aside yerine wcn-act-outcome.
4. **Hedef türü chip'leri satırı TAM DOLDURSUN:** `#vfpTargetTypeCards` (`.vfp-chipgrid`) chip'ler container genişliğini eşit paylaşıp satırı doldursun (grid `repeat(auto-fit,minmax(0,1fr))` ya da flex `flex:1 1 0`). Chip stili (F3: kompakt, seçili mor, en spesifik/en geniş alt-yazı, specificity sıra) KORUNUR; font app.
5. **"Hangi kayıt" kutusu (picked-target):** picker mockup'ın kesikli-kutusuna dönsün (kullanıcının verdiği HTML — inline stiller scoped CSS + tema token'a çevrilir):
   - Dış: kesikli kenarlık (`1px dashed var(--bs-border-color)`), radius 6, padding 16, bg subtle (`var(--bs-tertiary-bg)` benzeri), flex-col gap 12.
   - Başlık satırı: "Hangi kayıt" (12px/600, `var(--bs-heading-color)`) + sağda "Listeden ara ve seç" (11px, `var(--bs-secondary-color)`).
   - Seçili kayıt satırı (beyaz/`--bs-card-bg` chip, border, radius 6, padding 8/12): **kod badge** (targetType kebab, JetBrains Mono/monospace, `background:rgba(var(--bs-primary-rgb),.12); color:var(--bs-primary)`, radius 4, 11px) + **isim** (13px/500) + sağda **dış kod** (mono, muted, ör SEG-00412 — varsa) + **"Değiştir…"** butonu (border, radius 6, 13px). "Değiştir" seçimi yeniden açar (mevcut davranış).
   - **Boşken:** app select (madde 6) ile seçim; seçilince chip satırı gösterilir. `#vfpTargetPicker [data-role="targetId"]` (buildPayload kaynağı) KORUNUR.
6. **Hedef seçim field'ı:** label `form-label fw-medium`; seçim kontrolü app field (`diten-field` + `form-select`/`form-control`) — task-create ile aynı. (territory-node picker'ı = model+node zorunlu; app select olarak kalır.)
7. **"Nerede geçerli olsun?" alt-bölüm (mockup resim-1 ile hizala):** alt-başlıklar ORGANİZASYON/KAPSAM/ÜRÜN/DÖNEM (uppercase, açıklama satırı) + her alan etiketinin yanında **kod-ipucu badge** (BusinessUnit/TerritoryNodeId/SegmentId/CampaignId/BrandId/ProductId/CycleId/CyclePeriodId — mono, muted küçük) + altında "kısıtlama yok"/"Önce marka seçin"/"Önce cycle seçin" ipuçları. Select'ler app (`form-select`) 2-kolon. Territory model+node 2 select KALIR (fonksiyonel; "Saha alanı" grubu). KAPSAM çip özeti + "Kapsamı temizle" korunur.

## KORU / YAPMA
- form.js/validation/buildPayload/id/data-role DEĞİŞMEZ (yalnız render + sınıf/markup + font). targetId/context select id'leri ve `data-role="targetId"` KORUNUR. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/**diğer bölümler (Kimlik/Frekans/Geçerlilik/Çakışma) DOKUNMA** (yalnız HEDEF bölümü). Hardcode vocabulary YOK (targetType/context contract'tan). Tema-duyarlı (light+dark, --bs-* token; mockup oklch → token karşılığı). Yalnız _Editor.cshtml(HEDEF) + form.js(target render) + visit-frequency-create.css + resx.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + visit-frequency-create.css + resx. Backend/liste/detay/diğer bölüm diff YOK. buildPayload alanları/data-role korunmuş.
- **E4:** HEDEF kartı mockup gibi: başlık "HEDEF — KİME UYGULANIYOR", chip'ler satırı doldurur, "Hangi kayıt" kesikli kutu (kod badge+isim+dış kod+Değiştir), Hedef field app, aside app-muted, alt-bölüm kod-ipuçlu 2-kolon; font app; işlev+payload aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F7 · HEDEF bölümü mockup birebir + app font/field (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 8c7f460b üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F7-target-section-mockup.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (HEDEF bölümü, satır ~194-260) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (renderTargetChips/renderTargetPicker/showPicked/renderScopeSummary/cascadeNodes) · wwwroot/assets/css/visit-frequency-create.css (.vfp-chipgrid/.vfp-chip*/.vfp-picked/.vfp-section-aside) · frontend/Diten.Web/Views/Tasks/Create.cshtml (diten-field+form-select) · backbone-custom.css .wcn-act-outcome.

NE (frontend; buildPayload/id/data-role KORUNUR; yalnız HEDEF bölümü):
 1) Başlık EditorSectionTarget "HEDEF"→"HEDEF — KİME UYGULANIYOR" (L10n 7 dil). Aside vfp-section-aside → wcn-act-outcome sınıfı.
 2) Hedef türü chip'leri satırı TAM DOLDUR: .vfp-chipgrid grid repeat(auto-fit,minmax(0,1fr)) veya flex flex:1 1 0 — chip'ler eşit satır dolu. F3 chip stili (kompakt/seçili mor/en spesifik-en geniş/specificity sıra) + font app KORUNUR.
 3) "Hangi kayıt" kesikli kutu (picked-target): kesikli kenar (1px dashed --bs-border-color), radius6, padding16, bg --bs-tertiary-bg, flex-col gap12; başlık satırı "Hangi kayıt"(12/600 heading)+"Listeden ara ve seç"(11 muted); seçili kayıt chip (--bs-card-bg,border,radius6,pad8/12): kod badge(kebab, monospace, bg rgba(--bs-primary-rgb,.12) color --bs-primary radius4 11px)+isim(13/500)+dış kod(mono muted, varsa)+"Değiştir…" buton(border radius6 13px). Boşken app select ile seç, seçilince chip. #vfpTargetPicker [data-role="targetId"] + form.js currentTargetId sözleşmesi KORUNUR.
 4) Hedef field label form-label fw-medium + diten-field + form-select (task-create). territory model+node app select kalır.
 5) "Nerede geçerli olsun?" alt-bölüm mockup: ORGANİZASYON/KAPSAM/ÜRÜN/DÖNEM uppercase alt-başlık+açıklama; alan etiketi yanında kod-ipucu badge (BusinessUnit/TerritoryNodeId/SegmentId/CampaignId/BrandId/ProductId/CycleId/CyclePeriodId, mono muted küçük); altında kısıtlama-yok/Önce marka seç/Önce cycle seç ipuçları; select'ler app form-select 2-kolon; KAPSAM çip özeti+Kapsamı temizle korunur. context select id'leri KORUNUR.
 6) Kart font app (task-create): HEDEF içeriği app font family+size (kod badge mono hariç).
KORU/YAPMA: form.js validation/buildPayload/id/data-role DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler (Kimlik/Frekans/Geçerlilik/Çakışma) DOKUNMA; hardcode vocabulary yok; tema-duyarlı (--bs-* token); yalnız _Editor(HEDEF)+form.js(target render)+css+resx.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css+resx; backend/liste/detay/diğer bölüm diff yok; buildPayload/data-role korunmuş. Ayrı commit. §22 TÜRKÇE. K13.
Durma: data-role/targetId/buildPayload korunamıyorsa; picker "Hangi kayıt" kutusu seçim işlevini bozuyorsa; kapsam HEDEF dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 29eec9d7 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-freqf7-verify @29eec9d7
```
- ✅ **Kapsam:** _Editor.cshtml(HEDEF) + form.js(target render) + visit-frequency-create.css + 7 resx. backend/liste/_DataTable/_DetailsQuickView/_Resolve/resolve.js/index.js/Segment = 0.
- ✅ **Payload sözleşmesi korundu:** `data-role="targetId"` 9× duruyor; buildPayload alan satırları değişmemiş (grep 0); context select id'leri aynı. form.js diff yalnız target render.
- ✅ **Detay bozulmadı:** shared `EditorSectionTarget` (=`DvSectionTarget`) korundu; başlık için yeni `EditorSectionTargetLong` eklendi → _DetailsQuickView etkilenmedi.
- ✅ **Mockup:** başlık "HEDEF — KİME UYGULANIYOR"; aside `wcn-act-outcome`; chip'ler `flex:1 1 0` satır-dolu (F3 stili korundu); "Hangi kayıt" kesikli kutu (kod badge mono + isim + dış kod + Değiştir, edit'te kilitliyken disable); Hedef field diten-field+form-select; alt-bölüm kod-ipucu badge (payload alan adları) + app form-select 2-kolon; kart içerik app font.
- ✅ **Build+test (CT izole, Release, temiz):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** HEDEF kartı mockup görünümü + seçim/kaydetme işlevi.

**FREQ-F7 KOMPLE.**
