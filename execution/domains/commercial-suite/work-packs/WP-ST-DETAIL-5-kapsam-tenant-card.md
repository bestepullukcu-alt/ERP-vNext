# WORK PACKAGE — WP-ST-DETAIL-5 · Detay KAPSAM: "Tüm kiracı" kartı + seçili seviye mockup vurgusu (frontend)

> **CT (SoR).** MOD-0167-FU04, Faz 3 Detay E4 rötuş. Branch `feature/crm-scmm-studio` (`0c0b99df` üstü — WP-ST-DETAIL-4 §37 sonrası). **Kullanıcı E4:** KAPSAM'da tenant-scoped play 3 kartı "—" gösteriyor; **"Tüm kiracı" kartı eksik** ve **seçili seviye mockup gibi vurgulanmıyor** (mockup seçili kart = yumuşak primary dolgu + border). **Frontend** (Details.cshtml KAPSAM section + strategy-create.css? + resx). Backend DEĞİŞMEZ.

## Kanıt
- **Details.cshtml KAPSAM (122-150):** 3 mini-kart (country / legal-entity / business-unit); `effScope = EffectiveScopeType ?? ScopeType`; `ScopeCardClass(level)` → `border-primary` (yalnız border, dolgu yok) `effScope==level` ise. **tenant scope'ta hiçbir kart `border-primary` almıyor** (tenant, 3 seviyeden biri değil) → tüm kartlar "—" + vurgu yok. data-resolve isim çözümü (DETAIL-3) korunur.
- **Mockup:** KAPSAM kartlarından **seçili (effective) olan** yumuşak primary dolgu + border ile vurgulu (lavanta). Tenant play için **"TÜM KİRACI"** seviyesi gösterilmeli.
- **VM:** `ScopeType`/`EffectiveScopeType` = tenant | country | legal-entity | business-unit. DETAIL-3'te `ScopeType_*` friendly resx anahtarları var (reuse).

## NE (frontend; backend DEĞİŞMEZ)
1. **"TÜM KİRACI" kartı ekle:** KAPSAM'a **ilk** mini-kart olarak TÜM KİRACI (tenant seviyesi). Etiket resx `ScopeType_tenant` (varsa reuse; "Tüm kiracı") üstte küçük uppercase; değer sabit "Tüm kiracı" (resolve gerekmez). Böylece 4 kart: **TÜM KİRACI · ÜLKE · TÜZEL KİŞİLİK · İŞ BİRİMİ**. Layout `col-6 col-md-3` (4/satır; dar ekranda 2/satır). country/LE/BU kartları + data-resolve KORUNUR.
2. **Seçili seviye vurgusu (mockup):** `ScopeCardClass` → seçili (`effScope==level`) karta **border-primary + yumuşak primary dolgu** (mockup lavanta). Tema-güvenli: `border-primary` + `bg-label-primary` (Vuexy subtle primary yüzey) VEYA küçük css `.st-scope-card.is-active{ background: rgba(var(--bs-primary-rgb),0.08); border-color: var(--bs-primary) }`. tenant→TÜM KİRACI, country→ÜLKE, legal-entity→TÜZEL KİŞİLİK, business-unit→İŞ BİRİMİ kartı vurgulanır. Seçili olmayan kartlar normal (mockup gibi).
3. **resx:** `ScopeType_tenant` yoksa 7 dil ekle ("Tüm kiracı"/"All tenant"/…). Diğer KAPSAM etiketleri (DETAIL-4) KORUNUR.

## KORU / YAPMA
- Backend/CrmService/DETAIL-1/gateway DEĞİŞMEZ. data-resolve isim çözümü (DETAIL-3) + KAPSAM diğer kartları + Detay'ın geri kalanı (stat/segment/frekans/ürün/içerik/summary/sürüm-geçmişi/header) DOKUNMA. Yalnız KAPSAM section. Tenant scope'ta country/LE/BU "—" kalır (veri yok — doğru); yalnız TÜM KİRACI dolu+vurgulu. Tema/L10n köprüsü. Paylaşımlı resx anahtarı değiştirme (Detay'a özel veya reuse).
- **DUR:** effScope beklenmeyen bir değerse (4 seviyeden biri değil) hiçbir kart vurgulanmaz → DUR gerekmez (fallback), ama TÜM KİRACI kartı eklemek layout'u/başka gösterimi kırıyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: Details.cshtml (+ css/resx gerekirse). **CrmService/backend/details.js(?)/Edit/Create/form.js diff YOK** (details.js yalnız gerekiyorsa; tercihen dokunma).
- **E4 (FLEET RESTART):** KAPSAM'da 4 kart (TÜM KİRACI · ÜLKE · TÜZEL KİŞİLİK · İŞ BİRİMİ); tenant play'de TÜM KİRACI dolu+vurgulu (yumuşak primary), diğerleri "—"; BU/LE/country play'de ilgili kart isimle+vurgulu (mockup gibi). **Razor(+css/resx) → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-DETAIL-5 · Detay KAPSAM "Tüm kiracı" kartı + seçili seviye mockup vurgusu (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 0c0b99df üstü · Worktree: ana checkout

Kullanıcı E4: KAPSAM tenant-scoped play'de 3 kart "—" + vurgu yok; "Tüm kiracı" kartı eksik + seçili seviye mockup gibi (yumuşak primary dolgu+border) gözükmeli. Backend DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-DETAIL-5-kapsam-tenant-card.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/Details.cshtml (KAPSAM 122-150: effScope + ScopeCardClass + 3 kart) · resx (ScopeType_tenant var mı) · strategy-create.css (gerekirse .st-scope-card).

NE (frontend; backend DEĞİŞMEZ):
 1) KAPSAM'a ilk kart TÜM KİRACI (tenant): etiket ScopeType_tenant (reuse/ekle "Tüm kiracı"), değer sabit "Tüm kiracı". 4 kart: TÜM KİRACI·ÜLKE·TÜZEL KİŞİLİK·İŞ BİRİMİ, layout col-6 col-md-3. country/LE/BU + data-resolve KORU.
 2) ScopeCardClass: seçili (effScope==level) karta border-primary + yumuşak primary dolgu (bg-label-primary veya .st-scope-card.is-active{background:rgba(var(--bs-primary-rgb),0.08);border-color:var(--bs-primary)}). tenant→TÜM KİRACI, country→ÜLKE, legal-entity→TÜZEL KİŞİLİK, business-unit→İŞ BİRİMİ. Seçili olmayan normal.
 3) resx ScopeType_tenant yoksa 7 dil ekle.
KORU/YAPMA: backend/CrmService/DETAIL-1/gateway DEĞİŞMEZ; data-resolve (DETAIL-3) + KAPSAM dışı her şey (stat/segment/frekans/ürün/içerik/summary/sürüm-geçmişi/header) DOKUNMA; yalnız KAPSAM section; tenant'ta country/LE/BU "—" doğru; paylaşımlı resx değiştirme; tema/L10n köprüsü; tercihen details.js'e dokunma.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff Details.cshtml(+css/resx); CrmService/backend/Edit/Create/form.js diff yok. Ayrı commit ("feat(strategy): WP-ST-DETAIL-5 — Detay KAPSAM 'Tüm kiracı' kartı + seçili seviye mockup vurgusu (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: TÜM KİRACI kartı layout/başka gösterimi kırıyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: cff41e8b · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stdetail5-verify @cff41e8b
```
- ✅ **Kapsam (1 dosya, +17/−6):** yalnız Details.cshtml. **CrmService/backend/details.js/resx/css/Edit/Create TEMİZ** ✓.
- ✅ TÜM KİRACI kartı (ilk) + 4 kart `col-6 col-md-3`; `ScopeType_tenant` reuse (resx değişmedi); data-resolve korundu; `ScopeCardStyle` seçili karta primary dolgu.
- ✅ **Build+test (CT izole):** Diten.Web.Tests **201/0**.
- ⚠ **E4 rötuş (→ DETAIL-6):** seçili vurgu `rgba(...,0.08)` **çok soluk** — mockup'ta seçili kart net lavanta dolgu + belirgin; hangisinin seçili olduğu anlaşılmıyor. DETAIL-6'da güçlendirilecek.

**WP-ST-DETAIL-5 KOMPLE (E2). Vurgu gücü DETAIL-6'da.**
