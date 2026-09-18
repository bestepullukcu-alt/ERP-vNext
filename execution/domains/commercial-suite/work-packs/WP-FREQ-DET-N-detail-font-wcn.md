# WORK PACKAGE — WP-FREQ-DET-N · Detay sayfası fontları WCN header tipografisiyle hizala (frontend, CSS)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (DET-M landing sonrası HEAD üstü — `visit-frequency-details.css` SIRALI, DET-M aynı dosyaya dokunur). **E4 isteği:** Detay sayfasındaki başlık ("Çözüm Test Eski") ve SIKLIK/HEDEF/GEÇERLİLİK/AĞIRLIK hücrelerindeki bold değerler WorkCenterNext header tipografisine uymuyor. **Frontend only** (`visit-frequency-details.css`). JS/Razor/backend DEĞİŞMEZ.

## Kök neden (CT doğruladı)
- `visit-frequency-details.css` satır 24-30: `.vfp-detail-scope [class*="vfp-dv-/vfp-rs-/vfp-det-"]:not(.vfp-mono) { font-family: 'Inter', Helvetica, Arial, sans-serif; }` → tüm detay+çözümleme UI'ı **Inter** fontunda render oluyor. WCN ve uygulamanın geri kalanı tema varsayılanı (`--bs-body-font-family`, Public Sans) kullanıyor → görünen font farkı bu.
- WCN referans (backbone-custom.css): `.wcn-detail-title` (full: `.wcn-full-detail-surface .wcn-detail-title`) = `font-size: 1.25rem; font-weight: 600; color: var(--bs-heading-color)`; font-family override YOK → tema varsayılanı.
- Detay mevcut: `.vfp-det-name` (satır 254) = `18px / 650`; `.vfp-det-stat-value` (satır 328) = `19px / 650`. `650` standart-dışı ağırlık (WCN `600`).

## NE (frontend; yalnız visit-frequency-details.css)
1. **Font-family (asıl fix):** satır 24-30 bloğundaki `font-family: 'Inter', Helvetica, Arial, sans-serif;` → **`var(--bs-body-font-family)`** (WCN/uygulama varsayılanı; token yoksa graceful fallback: `var(--bs-body-font-family, 'Public Sans', -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif)`). `.vfp-mono` bloğu (satır 31-33) DEĞİŞMEZ (monospace kalır). Bu değişiklik tüm detay+çözümleme scope'unu WCN fontuna hizalar (kullanıcının işaret ettiği başlık+stat bold'lar dahil).
2. **Ağırlık (WCN header'a hizala):** `.vfp-det-name` (254) `font-weight: 650` → `600`; `.vfp-det-stat-value` (328) `font-weight: 650` → `600`. Renkler (`--vfp-heading` = `--bs-heading-color`) zaten WCN ile aynı — korunur.
3. **(Opsiyonel, WCN full-detail title'a birebir):** `.vfp-det-name` `font-size: 18px` → `1.25rem` (WCN full-detail title). Şüphe varsa 18px bırakılabilir; ağırlık+aile birincil. Uygulanırsa da kabul.

## KORU / YAPMA
- Yalnız `visit-frequency-details.css`. JS (details.js/resolve.js)/Razor (Details.cshtml)/backend/L10n DOKUNMA. `.vfp-mono` monospace DEĞİŞMEZ. DET-M `.vfp-rs-answer--*` tonları + DET-I/DET-G stat-strip yapısı (grid/hairline/hücre bg) + `--vfp-*` token'lar KORUNUR. Renk token'ları (`--vfp-heading`/`--vfp-muted`) DEĞİŞMEZ. Tema-duyarlı (`--bs-body-font-family` shell'den gelir). Yeni font YÜKLEME yok (tema fontu zaten yüklü).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff **yalnız visit-frequency-details.css**. JS/Razor/backend diff YOK.
- **E4:** Detay başlığı ("Çözüm Test Eski") + SIKLIK/HEDEF/GEÇERLİLİK/AĞIRLIK bold değerleri WCN header fontuyla aynı (Inter değil, Public Sans/tema; ağırlık 600). Tema-duyarlı. **wwwroot CSS → Ctrl+F5 yeter.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-M landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-N · Detay sayfası fontları WCN header tipografisiyle hizala (MOD-0165-FU03, frontend CSS)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-M commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-N-detail-font-wcn.md · frontend/Diten.Web/wwwroot/assets/css/visit-frequency-details.css (font-family bloğu satır 24-30, .vfp-det-name ~254, .vfp-det-stat-value ~328) · (referans) backbone-custom.css .wcn-detail-title (6244) + .wcn-full-detail-surface .wcn-detail-title (6287).

KÖK NEDEN: detail css satır 24-30 tüm .vfp-det-/.vfp-dv-/.vfp-rs- sınıflarını 'Inter' fontuna zorluyor; WCN tema varsayılanını (--bs-body-font-family) kullanıyor → font farkı.

NE (frontend; yalnız visit-frequency-details.css):
 1) Satır 24-30 bloğu: font-family: 'Inter', Helvetica, Arial, sans-serif; → var(--bs-body-font-family, 'Public Sans', -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif). .vfp-mono bloğu (31-33) DEĞİŞMEZ.
 2) .vfp-det-name (254): font-weight 650→600. .vfp-det-stat-value (328): font-weight 650→600. (renkler --vfp-heading korunur.)
 3) (opsiyonel) .vfp-det-name font-size 18px→1.25rem (WCN full-detail title). Şüphede 18px bırak.
KORU/YAPMA: yalnız visit-frequency-details.css; JS/Razor/backend/L10n DOKUNMA; .vfp-mono monospace DEĞİŞMEZ; DET-M .vfp-rs-answer--* tonları + stat-strip yapısı + --vfp-* token'lar KORUNUR; yeni font yükleme yok; tema-duyarlı.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 137/0; git diff YALNIZ visit-frequency-details.css. Ayrı commit ("feat(freq): WP-FREQ-DET-N — detay fontları WCN header tipografisiyle hizala (Inter→tema fontu, 650→600) (MOD-0165-FU03)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: değişiklik css dışına taşarsa; .vfp-mono/DET-M tonlarını bozarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: 5003e3ae · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detmon-verify @5003e3ae
```
- ✅ **Kapsam:** yalnız visit-frequency-details.css (+5/-4). Satır 24-30 font-family `'Inter',…` → `var(--bs-body-font-family, 'Public Sans', …)`; `.vfp-det-name` 18px/650 → 1.25rem/600; `.vfp-det-stat-value` 650→600. **KORU=0** (`.vfp-mono` monospace değişmedi; DET-M `.vfp-rs-answer--*` tonları bozulmadı — doğrulandı; renk token'ları korundu; JS/Razor/backend dokunulmadı).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ E4: başlık + stat bold'lar WCN header fontu (Inter değil). **wwwroot CSS → Ctrl+F5 (DET-M ile birlikte restart).**

**DET-N KOMPLE.**
```
