# WORK PACKAGE — WP-FREQ-DET-J · Liste konsolu tab'ları WorkCenter tab-card stili (frontend, mini)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**DET-I landing sonrası HEAD üstü** — Index.cshtml SIRALI). Kullanıcı: Politikalar/Çözümleme tab'ları WorkCenter vNext tab-card gibi (`card mb-3 wcn-tabcard` + `nav-pills wcn-tabs` + `nav-link border shadow-none wc-tab-compact` + ikon). **Frontend only** (Index.cshtml tab markup). **CSS global** (`wc-tab-compact`/`wcn-tabcard`/`wcn-tabs` backbone-custom.css'te, _LayoutTenantShell yüklü) → css değişikliği YOK.

## Kapsam
- `Index.cshtml` (satır ~197-214 `nav-align-top` bloğu): tab çubuğunu WorkCenter tab-card'a sar:
  - `<section class="card mb-3 wcn-tabcard"><div class="card-body p-3 d-flex align-items-center gap-3 flex-wrap">`
  - `<ul class="nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs" role="tablist">`
  - Tab butonları: mevcut `data-bs-toggle="tab" data-bs-target="#tab-policies|#tab-resolve"` + role/aria KORUNUR; sınıflara `border shadow-none wc-tab-compact d-inline-flex align-items-center` eklenir; ikon `bx ... wc-tab-icon me-md-1` + etiket (WorkCenter mobilde `d-none d-md-inline` gizler — opsiyonel, freq'te etiket kalabilir). active tab `active`.
  - **View-tools grubu (liste/tablo/split/takvim) ve count badge YOK** (WorkCenter'a özel; freq konsolunun kendi DataTable toolbar'ı var, 2 tab count'suz).
- canResolve gate (Çözümleme tab) KORUNUR. tab-content (#tab-policies/#tab-resolve) DEĞİŞMEZ.

## KORU / YAPMA
- Tab switch mekanizması (Bootstrap data-bs-toggle="tab") DEĞİŞMEZ (WorkCenter'ın data-wcn-tab JS'i DEĞİL). tab id'leri (#tab-policies/#tab-resolve) + canResolve + tab-content + liste/çözümleme içerikleri DOKUNMA. CSS global — yeni css YOK (wcn-tabcard/wc-tab-compact backbone-custom.css). Yalnız Index.cshtml tab markup. Backend/resolve.js/details.js/Segment DOKUNMA.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff yalnız Index.cshtml (tab markup). 
- **E4:** Politikalar/Çözümleme tab'ları WorkCenter tab-card görünümünde (kart içinde bordered compact pill + ikon); tab geçişi + Çözümleme gate çalışır; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-I landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-J · Liste konsolu tab'ları WorkCenter tab-card stili (MOD-0165-FU03, frontend mini)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-I commit> üstü · Worktree: ana checkout

Önce oku: frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/Index.cshtml (nav-align-top ~197-214) · (referans) WorkCenter tab markup (card mb-3 wcn-tabcard > card-body p-3 > ul.nav.nav-pills.wcn-tabs > button.nav-link.border.shadow-none.wc-tab-compact) · backbone-custom.css .wc-tab-compact/.wcn-tabcard (GLOBAL — css değiştirme).

NE (frontend; yalnız Index.cshtml tab markup): nav-align-top tab çubuğunu WorkCenter tab-card'a sar: <section class="card mb-3 wcn-tabcard"><div class="card-body p-3 d-flex align-items-center gap-3 flex-wrap"><ul class="nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs">…. Tab butonlarına "border shadow-none wc-tab-compact d-inline-flex align-items-center" ekle; mevcut data-bs-toggle="tab"+data-bs-target(#tab-policies/#tab-resolve)+role/aria+ikon KORU; active tab active. View-tools/count badge YOK. canResolve gate + tab-content KORUNUR.
KORU/YAPMA: Bootstrap tab-toggle mekanizması DEĞİŞMEZ (data-wcn-tab JS değil); tab id/canResolve/tab-content/içerik DOKUNMA; css GLOBAL (yeni css yok); backend/resolve.js/details.js/Segment DOKUNMA; yalnız Index.cshtml.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız Index.cshtml. Ayrı commit. §22 TÜRKÇE. K13.
Durma: wc-tab-compact global değilse (css eklenmesi gerekirse); tab-toggle bozuluyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: 0b55ef25 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detkj-verify @0b55ef25
```
- ✅ **Kapsam:** yalnız `Index.cshtml` (+20/-15). git diff: `nav-align-top` → `<section class="card mb-3 wcn-tabcard"><div class="card-body p-3 d-flex align-items-center gap-3 flex-wrap"><ul class="nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs">`; tab butonlarına `border shadow-none wc-tab-compact d-inline-flex align-items-center` + ikon `wc-tab-icon me-md-1`. **KORU=0**: `data-bs-toggle="tab"` + `data-bs-target`(#tab-policies/#tab-resolve) + role + active + canResolve gate + tab-content DEĞİŞMEDİ; backend/resolve.js/details.js/Segment dokunulmadı. View-tools/count badge eklenmedi.
- ✅ **CSS global (yeni css yok):** `wc-tab-compact` (backbone-custom.css:2599), `wcn-tabs` (5581), `wcn-tabcard` (list-page yüzeyi) — üçü de _LayoutTenantShell'de yüklü global.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ E4: Politikalar/Çözümleme tab'ları WorkCenter tab-card görünümü + tab geçişi + Çözümleme gate. **Razor .cshtml → FLEET RESTART şart** (Ctrl+F5 yetmez, view recompile).

**DET-J KOMPLE.**
```
