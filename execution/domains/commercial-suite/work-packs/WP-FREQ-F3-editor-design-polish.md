# WORK PACKAGE — WP-FREQ-F3 · Editör sayfası tasarım cilası (mockup birebir) (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`e15af217` üstü). FREQ-F2 editör ayrı sayfayı KURDU ama tasarım mockup'a benzemedi (kullanıcı: "olmamış tasarım"). Bu WP = **yalnız görsel cila** — yapı/mantık/payload KORUNUR, CSS + kart/lifecycle render'ı mockup'a hizalanır. **Frontend only** (visit-frequency-create.css + _Editor.cshtml + form.js render + L10n). Mockup CT tarafından render edilip ölçüldü (aşağıdaki spec'ler kesin: oklch hue 295 mor palet — zaten `--vfp-accent`/`--bs-primary` token'larında).

## Kullanıcının gördüğü sorunlar (mevcut vs mockup)
1. **Target type kartları:** şu an 2 satır, BÜYÜK kartlar, Title-Case ("Account Contact Link"), alt-yazı yok, sıra yanlış. Mockup: **tek satır kompakt chip**, specificity sırası, ilk/son'da alt-yazı.
2. **Lifecycle paneli:** şu an **2 durum** (Draft/Active). Mockup: **4 durum** (Taslak/Yayında/Geçici kapalı/Arşivde).
3. Section başlıkları ve genel yoğunluk/cila mockup'tan sönük.

## Kesin spec (CT ölçümü — mockup)
### Target type kartları (`vfpTargetTypeCards` / `.vfp-cardgrid`)
- **Kompakt chip, tek satırda flex-wrap** (her chip ~content-width, ~70px min; büyük grid-kart DEĞİL).
- Chip: `font-size:12px; font-weight:400; border:1px solid var(--bs-border-color); border-radius:6px; padding:8px 12px; background:var(--bs-card-bg); color:var(--bs-secondary-color)`.
- **Seçili:** `color:var(--bs-primary); background:rgba(var(--bs-primary-rgb),0.08)~oklch(0.97 0.02 295) eşdeğeri; border-color:var(--bs-primary)` (mor). (Mockup: metin oklch(0.4 .16 295), bg oklch(0.97 .02 295), border oklch(0.55 .2 295) — token karşılığı --bs-primary tint.)
- **Sıra (specificity, dar→geniş):** account-contact-link · contact · account · campaign-target · concept-node · territory-node · audience-profile · segment. (Sıra contract'tan gelmiyorsa bu sabit sıraya göre dizilebilir — display sıralaması, vocabulary değil.)
- **Etiket:** okunur (humanize) kalabilir; ama kompakt chip stili + alt-yazı şart. (İstersen mockup'taki kebab görünümü yerine humanize kalsın — kritik olan chip formu.)
- **Alt-yazı:** ilk kartta (account-contact-link) küçük soluk "en spesifik" (L10n `MostSpecific`), son kartta (segment) "en geniş" (L10n `Broadest`). ~10px, --bs-secondary-color.

### Lifecycle paneli (sağ, `.vfp-lifecycle`) — 4 durum
Mockup 4 satır (başlık + açıklama):
| kod | başlık | açıklama |
|---|---|---|
| draft | Taslak | plana girmez |
| active | Yayında | plana giriyor |
| inactive | Geçici kapalı | şimdilik durduruldu |
| archived | Arşivde | okunur, silinmez |
- Seçili satır vurgulu (mor sol-şerit/bg `rgba(var(--bs-primary-rgb),0.06)` + renkli durum noktası).
- **Create modunda:** draft+active seçilebilir; inactive+archived görünür ama **disabled/soluk** (yeni kayıt inactive/archived oluşturulamaz). **Edit modunda:** draft↔active↔inactive seçilebilir (update Status); archived seçimi = archive endpoint (mevcut davranış). Not kutusu korunur ("Silme yok. Kapatma Archive ile...").
- L10n: `Lifecycle_draft/active/inactive/archived` başlık + `_Desc` (yukarıdaki tr; 7 dil). (Status_* mevcut olabilir — reuse; açıklamalar yeni.)

### Section başlık badge + başlık
- Badge ("01".."05"): `font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.72px; color:var(--bs-primary); background:rgba(var(--bs-primary-rgb),0.1)~; border-radius:5px; padding` (kare-ish). (Mevcut `.vfp-section-no` var — mockup ölçüsüne getir.)
- Section title: **UPPERCASE** + letter-spacing (mockup: KIMLIK/HEDEF/...). `.vfp-section-title`e `text-transform:uppercase; letter-spacing` ekle.
- Genel: mockup yoğunluğu/cila — bölüm kartı padding/gap, input stili (radius/padding), sağ panel kartları tutarlı; tema-duyarlı (light+dark). Segment css DEĞİŞTİRME; yalnız visit-frequency-create.css.

## KORU / YAPMA
- Form yapısı/alanları/validation/**buildPayload sözleşmesi** DEĞİŞMEZ (yalnız görsel + target-card display sırası + lifecycle 4 satır + subtext). Backend/liste/Detay/Çözümleme/resolve.js/Segment/başka modül DOKUNMA. Contract-driven vocabulary korunur (target/band/source/period contract'tan; sıra display transform). Hardcode vocabulary YOK. Golden Compact/tema/L10n köprüsü korunur. Yalnız: visit-frequency-create.css + _Editor.cshtml + form.js (render) + resx.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: visit-frequency-create.css + _Editor.cshtml + form.js + resx (7 dil). Backend/liste/detay/çözümleme diff YOK.
- **E4:** /Create + /Edit mockup'a benzer: target kompakt tek-satır chip (specificity sıra + en spesifik/en geniş), lifecycle 4 durum (create'te inactive/archived disabled), section badge/başlık uppercase, genel cila; buildPayload aynı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F3 · Editör sayfası tasarım cilası mockup birebir (MOD-0165-FU03, frontend, görsel)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: e15af217 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F3-editor-design-polish.md (kesin spec — CT mockup'ı render edip ölçtü) · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (renderCards + setLifecycle + lifecycle markup) · wwwroot/assets/css/visit-frequency-create.css (mevcut --vfp-accent/.vfp-cardgrid/.vfp-section-no/.vfp-lifecycle token+stilleri).

NE (yalnız görsel; yapı/mantık/payload KORUNUR):
 1) Target type kartları → KOMPAKT tek-satır flex-wrap chip (font 12px/400, border 1px --bs-border-color, radius 6px, padding 8px 12px, bg --bs-card-bg, color --bs-secondary-color; SEÇİLİ mor: color --bs-primary + bg rgba(--bs-primary-rgb,.08) + border --bs-primary). Display sırası specificity: account-contact-link,contact,account,campaign-target,concept-node,territory-node,audience-profile,segment. İlk karta "en spesifik" (L10n MostSpecific), son karta "en geniş" (Broadest) küçük soluk alt-yazı. Etiket humanize kalabilir; kritik olan chip formu+sıra+alt-yazı. Vocabulary yine contract'tan (sıra sadece display).
 2) Lifecycle paneli 4 durum: draft=Taslak(plana girmez)/active=Yayında(plana giriyor)/inactive=Geçici kapalı(şimdilik durduruldu)/archived=Arşivde(okunur,silinmez); seçili vurgu (mor bg .06 + durum noktası). Create: draft+active seçilebilir, inactive+archived disabled/soluk. Edit: draft/active/inactive update Status, archived=archive endpoint (mevcut). L10n Lifecycle_{kod}+_Desc (7 dil). Not kutusu korunur.
 3) Section badge (11px/600/uppercase/ls .72px, color --bs-primary, bg rgba(--bs-primary-rgb,.1), radius 5px) + section-title UPPERCASE+letter-spacing. Genel mockup yoğunluğu/cila (bölüm padding/gap, input radius/padding, sağ panel), tema-duyarlı light+dark.
KORU/YAPMA: form yapısı/alan/validation/buildPayload sözleşmesi DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/Segment/başka modül DOKUNMA; contract-driven vocabulary (sıra display transform, hardcode yok); yalnız visit-frequency-create.css + _Editor.cshtml + form.js(render) + resx; Segment css değiştirme; tema/L10n köprüsü korunur.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız css+_Editor+form.js+resx; backend/liste/detay diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: mockup spec'i buildPayload/validation değiştirmeden uygulanamıyorsa; kapsam editör dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 3325afe6 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqf3-verify @3325afe6
```
- ✅ **Kapsam:** form.js + visit-frequency-create.css + _Editor.cshtml + 7 resx. **backend/liste/_DataTable/_DetailsQuickView/_Resolve/resolve.js/index.js/Segment = 0** (grep 0). **buildPayload = 0 değişiklik** (grep 0) — payload sözleşmesi/validation korundu.
- ✅ **Target chip:** `renderTargetChips` + `TARGET_SPECIFICITY` sırası (account-contact-link→segment; contract vocab korunur, bilinmeyen kod sona); ilk chip MostSpecific ("en spesifik"), son chip Broadest ("en geniş") alt-yazı; `.vfp-chipgrid` flex-wrap kompakt chip + seçili mor (`:has(.vfp-radio-input:checked)`).
- ✅ **Lifecycle 4 durum:** `configureLifecycleForMode` — 4 satır görünür; create'te inactive+archived `.vfp-life-disabled` + input.disabled (soluk, seçilemez); edit'te 4'ü mevcut davranış (archived=archive endpoint). Ajan notu: F2 zaten 4 satırdı ama create'te gizliydi → gerçek boşluk gizleme idi, disabled/soluk yapıldı.
- ✅ **Section badge/başlık:** `.vfp-section-no` kare mor tint uppercase; `.vfp-section-title` uppercase+letter-spacing. Tema-duyarlı (--bs-* token).
- ✅ **L10n:** MostSpecific/Broadest 7 dil; lifecycle açıklamaları zaten resx'te mevcuttu (Status_* reuse, liste/detay paylaşımlı → değiştirilmedi).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** restart+Ctrl+F5 → /Create: kompakt target chip (specificity + en spesifik/en geniş), lifecycle 4 durum (create'te inactive/archived soluk), section uppercase; mockup'a yakınlık kullanıcı onayı.

**FREQ-F3 KOMPLE. MOD-0165-FU03 UI = A→F3 (hepsi CT-E2). Kalan: E4 uçtan uca + kullanıcı tasarım onayı.**
