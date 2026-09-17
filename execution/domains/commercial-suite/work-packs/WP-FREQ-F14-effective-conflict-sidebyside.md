# WORK PACKAGE — WP-FREQ-F14 · GEÇERLİLİK + ÇAKIŞMA kartları yan yana (frontend, mini)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`4f492c43` üstü). Kullanıcı: GEÇERLİLİK (04) + ÇAKIŞMA OLURSA (05) kartları **yan yana** (2-kolon), alt alta değil (mockup böyle). **Frontend only** (_Editor.cshtml + gerekirse css). İçerik/id/mantık KORUNUR.

## Kapsam
- `_Editor.cshtml`: GEÇERLİLİK (04) `section.card` + ÇAKIŞMA (05) `section.card` → bir `row g-4` içinde iki sütun (`col-12 col-xl-6` her biri; geniş ekranda yan yana, dar ekranda alt alta). Kartların iç içeriği/ikon/başlık/alanlar/id AYNEN korunur; yalnız iki kartı saran 2-kolon wrapper eklenir. `mb-4` çift-boşluk olmasın (row g-4 gap yönetir; kartlardan `mb-4` kaldırılıp row gap'e bırakılabilir veya col içinde h-100).
- Gerekirse `visit-frequency-create.css`: iki kart eşit yükseklik/hizalama (col içinde `.card{height:100%}` opsiyonel). Tema-duyarlı.

## KORU / YAPMA
- Kart içerikleri (EffectiveFrom/To, band radio-kartları, Source, notlar) + id'ler + form.js/buildPayload/validation DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler (Kimlik/Hedef/Frekans) DOKUNMA. Sağ panel (Yaşam Döngüsü/özet/checklist) düzeni DEĞİŞMEZ. Yalnız _Editor.cshtml (04+05 sarma) + css (opsiyonel).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff yalnız _Editor.cshtml (+css opsiyonel). Backend/form.js/liste/detay diff YOK.
- **E4:** GEÇERLİLİK + ÇAKIŞMA geniş ekranda yan yana (2-kolon), dar ekranda alt alta; içerik/işlev aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F14 · GEÇERLİLİK + ÇAKIŞMA kartları yan yana (MOD-0165-FU03, frontend, mini)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 4f492c43 üstü · Worktree: ana checkout

Önce oku: frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (GEÇERLİLİK 04 + ÇAKIŞMA 05 section'ları) · wwwroot/assets/css/visit-frequency-create.css.

NE (frontend; içerik/id/mantık KORUNUR): GEÇERLİLİK (04) + ÇAKIŞMA (05) iki section.card'ı bir <div class="row g-4"> içinde iki sütuna al (col-12 col-xl-6 her biri; geniş yan yana, dar alt alta). Kart iç içeriği/başlık/ikon/alanlar/id AYNEN. Çift dikey boşluğu önle (mb-4 yerine row gap; gerekirse col içinde .card height:100% eşit yükseklik). Tema-duyarlı.
KORU/YAPMA: kart içerikleri/id/form.js/buildPayload/validation DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler + sağ panel DOKUNMA; yalnız _Editor.cshtml(04+05 sarma)+css(opsiyonel).
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor(+css). Ayrı commit. §22 TÜRKÇE. K13.
Durma: yan yana düzen kart içeriğini/id/işlevi bozuyorsa; kapsam 04+05 dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests 137/0; 04+05 row g-4 col-xl-6 yan yana (dar alt alta); kart içerik/id/form.js/buildPayload/backend/diğer bölüm değişmedi; git diff yalnız _Editor(+css).
```
