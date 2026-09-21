# WORK PACKAGE — WP-FREQ-F4 · Editör section kabukları → app kartı ("kabuk app, içerik mockup") (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`63d446f1` üstü). Kullanıcı: editör bölümlerinin kabuğu `/Tasks/Create`'teki app kartı gibi olsun (background dahil), içerik mockup kalsın. Bu = **Segment'te onaylanan "kabuk app, içerik mockup" deseninin** Frekans editörüne uygulanması. **Frontend only** (_Editor.cshtml + visit-frequency-create.css; form.js inner id'lere dokunma). Yapı/mantık/payload KORUNUR.

## Hedef kabuk (kullanıcının verdiği app-card deseni — /Tasks/Create)
Her sol bölüm ve sağ panel kartı bu app-native kabuğa dönüşür:
```html
<section class="card mb-4">
  <div class="card-body p-4">
    <h6 class="text-uppercase text-heading fw-semibold mb-4 d-flex align-items-center gap-2">
      <i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i>BÖLÜM BAŞLIĞI
    </h6>
    <!-- mockup içerik: vfp-grid / vfp-input / chip / lifecycle satırları / checklist ... AYNEN kalır -->
  </div>
</section>
```
- **Kaldırılan:** özel `.vfp-section` / `.vfp-section-head` / `.vfp-section-no` (numaralı "01" badge) kabuğu. Yerine app `card mb-4 > card-body p-4` + `h6.text-uppercase.text-heading.fw-semibold` başlık + `bx dt-card-icon` ikon. (Segment editör/detay bu deseni kullanıyor — referans; segment dosyalarına DOKUNMA.)
- **`vfp-section-help`** yardımcı metni başlığın altında kalabilir (`<p class="text-muted small mb-3">`), içerik.
- **İkon önerisi (bx):** Kimlik=bx-info-circle · Hedef=bx-target-lock · Frekans=bx-repeat · Geçerlilik=bx-calendar · Çakışma=bx-git-merge (veya bx-shield-quarter). Sağ panel: Yaşam Döngüsü=bx-been-here · Bu politika ne yapacak=bx-bulb · Kaydetmeden önce=bx-check-shield. (Ajan uygun bx seçebilir.)

## Kapsam
1. **_Editor.cshtml:** tüm `.vfp-section` (sol 5 bölüm) + sağ panel kartları (Yaşam Döngüsü / Bu politika ne yapacak / Kaydetmeden önce) → app `card mb-4 > card-body p-4` kabuğu + `h6.text-uppercase.text-heading.fw-semibold + bx dt-card-icon` başlık. **İç içerik (vfp-grid, vfp-input, vfp-textarea, chip grid, band kartları, lifecycle satırları, özet, checklist, footer butonları) AYNEN korunur** — yalnız kabuk + başlık değişir. Footer butonları (Kaydet ve aktive et / Taslak) mevcut yerinde/kartında kalır.
2. **visit-frequency-create.css:** `.vfp-section*` kabuk stillerini kaldır/nötrle (artık app `.card`). `.vfp-create-scope`in app `.card`/`card-body`/`h6.text-heading` üstüne binmediğinden emin ol (card bg = `--bs-card-bg`, background override YOK). İç `.vfp-*` içerik stilleri (input/chip/lifecycle/grid/reads-as/summary/checklist) KORUNUR. Tema-duyarlı.
3. **form.js:** inner element id'leri (vfpPolicyCode, vfpTargetTypeCards, vfpBandCards, lifecycle, checklist...) DEĞİŞMEZ → dokunma; yalnız section markup'ı taşınırsa id'ler korunur. Numaralı badge kaldırıldığı için form.js bir yeri kırıyorsa minimal uyarla (ör. section-no'ya referans yoksa dokunma).

## KORU / YAPMA
- Form yapısı/alan/validation/**buildPayload sözleşmesi** DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment (segment-create.css dahil)/başka modül DOKUNMA. İç mockup içerik (chip/band/lifecycle/summary/checklist) KORUNUR — yalnız kabuk app kartına döner. Golden Compact/tema/L10n köprüsü korunur. Yalnız _Editor.cshtml + visit-frequency-create.css (+ gerekirse form.js minimal, +resx yeni başlık gerekmez — başlıklar mevcut).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + visit-frequency-create.css (+ form.js minimal ise). Backend/liste/detay/çözümleme/Segment diff YOK.
- **E4:** /Create + /Edit bölümleri app kartı görünümünde (card bg `--bs-card-bg`, `h6.text-uppercase.text-heading` + dt-card-icon başlık — /Tasks/Create ile aynı kabuk); iç içerik (chip/band/lifecycle/özet/checklist) mockup gibi; işlevsellik + payload aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F4 · Editör section kabukları → app kartı (MOD-0165-FU03, frontend, "kabuk app içerik mockup")
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 63d446f1 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F4-app-card-shells.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml · wwwroot/assets/css/visit-frequency-create.css · (referans app-card kabuk deseni) frontend/Diten.Web/Views/Tasks/Create.cshtml (section.card mb-4 > card-body p-4 > h6.text-uppercase.text-heading.fw-semibold + bx dt-card-icon) · (referans "kabuk app içerik mockup") Segment Create/Details view'ları (DEĞİŞTİRME, yalnız desen).

NE (frontend; yapı/mantık/payload KORUNUR):
 _Editor.cshtml: tüm .vfp-section (sol 5 bölüm) + sağ panel kartları (Yaşam Döngüsü/Bu politika ne yapacak/Kaydetmeden önce) → app kabuğu: <section class="card mb-4"><div class="card-body p-4"><h6 class="text-uppercase text-heading fw-semibold mb-4 d-flex align-items-center gap-2"><i class="bx bx-<icon> dt-card-icon"></i>BAŞLIK</h6> ...iç mockup içerik AYNEN... </div></section>. .vfp-section-no numaralı badge kaldır; vfp-section-help başlık altında <p class="text-muted small mb-3"> olarak kalır. İkon: Kimlik=bx-info-circle,Hedef=bx-target-lock,Frekans=bx-repeat,Geçerlilik=bx-calendar,Çakışma=bx-git-merge; sağ panel Yaşam Döngüsü=bx-been-here,Bu politika ne yapacak=bx-bulb,Kaydetmeden önce=bx-check-shield. İÇ İÇERİK (vfp-grid/input/textarea/chip/band kartları/lifecycle satırları/reads-as/özet/checklist/footer butonları) + tüm element id'leri KORUNUR.
 visit-frequency-create.css: .vfp-section* kabuk stillerini kaldır/nötrle (app .card kullanılıyor); .vfp-create-scope app .card/card-body/h6.text-heading bg'sini override ETMESİN (card bg=--bs-card-bg). İç .vfp-* içerik stilleri korunur. Tema-duyarlı.
 form.js: yalnız section markup taşındı; iç id'ler değişmedi → dokunma (numaralı badge referansı kırılırsa minimal uyarla).
KORU/YAPMA: form yapısı/alan/validation/buildPayload sözleşmesi DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment(segment-create.css dahil)/başka modül DOKUNMA; iç mockup içerik korunur (yalnız kabuk app kartı); tema/L10n köprüsü korunur; yalnız _Editor.cshtml + visit-frequency-create.css (+form.js minimal).
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+css(+form.js minimal); backend/liste/detay/Segment diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: app-card kabuğu buildPayload/id'leri/işlevi bozmadan uygulanamıyorsa; kapsam editör dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: ad8d3b39 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqf4-verify @ad8d3b39
```
- ✅ **Kapsam:** yalnız `_Editor.cshtml` + `visit-frequency-create.css` (2 dosya). form.js/backend/liste/_DetailsQuickView/_Resolve/resolve.js/index.js/Segment(segment-create.css) = 0 değişiklik.
- ✅ **App-card kabuk:** 8 kart `card mb-4` + 8 `h6.text-uppercase text-heading fw-semibold` başlık + `bx dt-card-icon` (sol 5 bölüm + sağ 3 panel). Eski `.vfp-section-no/-head` (numaralı badge) = 0 (kaldırıldı). card bg = `--bs-card-bg` (`.vfp-create-scope` override etmiyor).
- ✅ **İç içerik + id'ler korundu:** vfpPolicyCode/vfpTargetTypeCards/vfpBandCards/vfpFrequencyType/vfpSource/vfpEffectiveFrom/vfpStatus vb. hepsi duruyor → form.js dokunulmadı, buildPayload/validation değişmedi.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** restart+Ctrl+F5 → /Create + /Edit bölümleri /Tasks/Create app kartı görünümünde (card bg + h6+dt-card-icon başlık), iç mockup içerik korunmuş.

**FREQ-F4 KOMPLE — "kabuk app, içerik mockup" editöre uygulandı.**
