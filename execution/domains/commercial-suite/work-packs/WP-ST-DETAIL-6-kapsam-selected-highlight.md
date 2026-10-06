# WORK PACKAGE — WP-ST-DETAIL-6 · Detay KAPSAM seçili kart vurgusunu güçlendir (mockup lavanta) (frontend)

> **CT (SoR).** MOD-0167-FU04, Faz 3 Detay E4 rötuş. Branch `feature/crm-scmm-studio` (`cff41e8b` üstü — WP-ST-DETAIL-5 §37 sonrası). **Kullanıcı E4:** DETAIL-5 4 kartı ekledi ama **seçili kart belli değil** — `rgba(...,0.08)` dolgu çok soluk; mockup'ta seçili kart **net lavanta dolgu + belirgin border + kalın değer**. **Frontend** (yalnız Details.cshtml KAPSAM ScopeCardClass/ScopeCardStyle). Backend/resx DEĞİŞMEZ.

## Kanıt
- **Details.cshtml (127-130):** `ScopeCardClass(level)` → `border-primary`; `ScopeCardStyle(level)` → `background: rgba(var(--bs-primary-rgb), 0.08)` (çok soluk). effScope==level ise uygulanıyor (tenant→TÜM KİRACI, country→ÜLKE, legal-entity→TÜZEL KİŞİLİK, business-unit→İŞ BİRİMİ).
- **Mockup (kullanıcı resim 2):** seçili kart belirgin **lavanta dolgu** + border + **değer kalın/primary**; seçili olmayan kartlar düz beyaz. Hangi seviyenin seçili olduğu net anlaşılır.

## NE (frontend; yalnız KAPSAM kart stili)
1. **Seçili kart dolgu/border güçlendir:** `ScopeCardStyle` seçili → `background: rgba(var(--bs-primary-rgb), 0.14); border-color: var(--bs-primary);` (belirgin lavanta) + `ScopeCardClass` seçilide `border-primary` KORU (gerekirse `border-2` ile kalınlaştır). Seçili olmayan kartlar değişmez (düz).
2. **Seçili değer vurgusu:** seçili kartın **değer** metni `fw-semibold text-primary` (mockup'taki kalın/renkli değer). Etiket (üst küçük uppercase) muted kalır. Seçili olmayan kartlarda değer normal.
3. (ops.) Seçili kartın sol üstüne küçük bir işaret gerekmiyor — mockup yalnız dolgu+kalın değerle ayırıyor; dolgu güçlenince yeterli.

## KORU / YAPMA
- Backend/CrmService/DETAIL-1/gateway/resx/details.js DEĞİŞMEZ. DETAIL-5 4-kart yapısı (TÜM KİRACI·ÜLKE·TÜZEL KİŞİLİK·İŞ BİRİMİ, col-6 col-md-3) + data-resolve isim çözümü (DETAIL-3) + KAPSAM dışı her şey DOKUNMA. Yalnız seçili-kart görsel vurgusu (dolgu/border/değer). Tema-güvenli (rgba var token; text-primary). Tenant'ta TÜM KİRACI vurgulu, diğerleri "—".
- **DUR:** yok (salt görsel).

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: yalnız Details.cshtml. **backend/resx/details.js/css diff YOK** (inline stil).
- **E4 (FLEET RESTART):** KAPSAM'da seçili seviye kartı **net lavanta dolgu + belirgin border + kalın primary değer** (mockup gibi); hangisinin seçili olduğu bir bakışta anlaşılır; diğer kartlar düz. **Razor → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-DETAIL-6 · Detay KAPSAM seçili kart vurgusunu güçlendir (mockup lavanta) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: cff41e8b üstü · Worktree: ana checkout

Kullanıcı E4: DETAIL-5 seçili kart vurgusu (rgba 0.08) çok soluk; mockup'ta seçili kart net lavanta dolgu + border + kalın değer. Yalnız KAPSAM kart stili; backend/resx DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-DETAIL-6-kapsam-selected-highlight.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/Details.cshtml (ScopeCardClass/ScopeCardStyle 127-130 + KAPSAM 4 kart 135+).

NE (frontend; yalnız KAPSAM kart stili):
 1) ScopeCardStyle seçili → "background: rgba(var(--bs-primary-rgb), 0.14); border-color: var(--bs-primary);" (belirgin lavanta); ScopeCardClass seçilide border-primary KORU (gerekirse border-2).
 2) Seçili kartın değer metni fw-semibold text-primary (mockup kalın/renkli değer); etiket muted kalır; seçili olmayan değer normal.
KORU/YAPMA: backend/CrmService/DETAIL-1/gateway/resx/details.js DEĞİŞMEZ; DETAIL-5 4-kart + data-resolve + KAPSAM dışı her şey DOKUNMA; yalnız seçili-kart dolgu/border/değer; tema-güvenli (rgba token, text-primary); tenant'ta TÜM KİRACI vurgulu.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff yalnız Details.cshtml; backend/resx/details.js/css diff yok. Ayrı commit ("feat(strategy): WP-ST-DETAIL-6 — Detay KAPSAM seçili kart vurgusu güçlendirildi (mockup lavanta) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 815d1f29 · Agent: PASS · CT: ACCEPTED E2 (HEAD atası, kapsam-temiz) · git show --stat + merge-base
```
- ✅ **Kapsam (1 dosya, +9/−7):** yalnız `Details.cshtml`. **backend/CrmService/DETAIL-1/gateway/resx/details.js/css TEMİZ** (commit-stat tek dosya) ✓. HEAD atası (`merge-base --is-ancestor` YES).
- ✅ `ScopeCardStyle` seçili: `rgba(...,0.08)` → **`0.14` + `border-color: var(--bs-primary)`** (belirgin lavanta); `ScopeCardClass` seçilide `border-primary` korundu; yeni `ScopeValueClass` seçili değer = **`fw-semibold text-primary`**, seçili olmayan `fw-medium`, etiket muted korundu (4 KAPSAM kartına uygulandı). DETAIL-5 4-kart + data-resolve dokunulmadı; tema-güvenli.
- ✅ **Build+test:** Diten.Web.Tests **201/0**.
- ✅ **Co-Authored-By** doğru satır mevcut.

**WP-ST-DETAIL-6 KOMPLE (E2).** E4 (fleet restart → lavanta vurgu görsel) kullanıcıda.

