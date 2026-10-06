# WORK PACKAGE — WP-SEG-A5 · Segment accent'ini app tema primary'sine bağla (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (SEG-A4 `1518c2be` / docs `b3fdd565` üstü). **Yalnız frontend (Diten.Web), tek dosya: `wwwroot/assets/css/segment-create.css`.** Owner: *"tema özelleştiricisinden renk seçince app ikonları/butonları değişiyor ama bizim mor-ağırlıklılar (pill, radio-card, recipe chip, Preview butonu) değişmiyor — onlar da değişmeli."* Kök neden: SEG-A3 accent'i **sabit oklch mor** (`oklch(0.55 0.19 285)`) yazdı; app primary `--bs-primary`/`--bs-primary-rgb`'yi takip etmiyor. **Çözüm: accent-türevi renkleri app tema değişkenlerine bağla** (nötr griler oklch kalır).

## Ölçülmüş girdi (CT)
- App primary = **`--bs-primary`** + **`--bs-primary-rgb`**; `template-customizer.js` runtime'da `:root`'ta günceller (canlı). Vuexy default primary ≈ mor (`#7367f0`) → default görünüm bugünküyle neredeyse aynı, ama artık tema-duyarlı.
- `backbone-custom.css` zaten bizim deseni kullanıyor: focus-ring `box-shadow: 0 0 0 .15rem rgba(var(--bs-primary-rgb), .14)`; açık tint `rgba(var(--bs-primary-rgb), 0.08)` / `0.035`.
- `segment-create.css` başındaki token bloğu (satır ~14-30) + **11 inline accent oklch** (token dışı) + açık-mor tint'ler.

## Kapsam (yalnız segment-create.css)
**1) Token bloğu → app değişkenleri:**
| token | eski | yeni |
|---|---|---|
| `--seg-accent` | `oklch(0.55 0.19 285)` | `var(--bs-primary)` |
| `--seg-accent-hover` | `oklch(0.49 0.19 285)` | `color-mix(in srgb, var(--bs-primary), #000 12%)` |
| `--seg-accent-dark` | `oklch(0.45 0.13 285)` | `var(--bs-primary)` |
| `--seg-accent-ring` | `oklch(0.55 0.19 285 / 0.14)` | `rgba(var(--bs-primary-rgb), 0.14)` |
| `--seg-ok` | `oklch(0.72 0.14 150)` | `var(--bs-success)` |
| `--seg-danger` | `oklch(0.55 0.17 25)` | `var(--bs-danger)` |
| `--seg-required` | `oklch(0.58 0.2 25)` | `var(--bs-danger)` |

**2) Token kullanmayan inline accent/tint oklch'leri app primary'ye çevir** (yüksek-chroma hue 285 + açık mor tint'ler):
- funnel fill `oklch(0.6 0.15 285)` → `var(--bs-primary)`
- pill hover border `oklch(0.72 0.1 285)` → `var(--bs-primary)`
- radio-card selected bg `oklch(0.985 0.012 285)` → `rgba(var(--bs-primary-rgb), 0.04)`; ring `oklch(… / 0.1)` → `rgba(var(--bs-primary-rgb), 0.1)`
- recipe chip / reach chip bg `oklch(0.96 0.018 285)` → `rgba(var(--bs-primary-rgb), 0.08)`; hover `oklch(0.92 0.035 285)` → `rgba(var(--bs-primary-rgb), 0.16)`
- sample avatar bg `oklch(0.955 0.02 285)` → `rgba(var(--bs-primary-rgb), 0.1)`; avatar metin → `var(--bs-primary)`
- accent metin/ikon (recipe text, reach chip text, source-badge accent, "AND ALSO" pill) yüksek-chroma 285 → `var(--bs-primary)` (koyu okunur)
- success tint'ler hue 150 (`oklch(0.4/0.94/0.96 … 150)`) → `var(--bs-success)` / `rgba(var(--bs-success-rgb), α)`
- danger tint'ler hue 25 (`oklch(0.965 0.02 25)` hover bg, `oklch(0.96 0.03 25)`) → `rgba(var(--bs-danger-rgb), α)`

**3) NÖTR OKLCH KALIR (tema-bağımsız, DOKUNMA):** kart border `oklch(0.93 0.006 285)`, section bg beyaz `oklch(1 0 0)`, heading/text/muted/label (chroma ≤0.02), input border `oklch(0.87/0.89 0.008 285)`, readonly bg, blok/değer-kabı nötr grileri, every/any track, JSON pre bg. **Ayrım kuralı: chroma ≥0.1 (hue 285) = accent → değiştir; chroma ≤0.04 = nötr → kalır. hue 150=success, hue 25=danger.**

## KORUNACAK / YAPMA
- `_Form.cshtml` + `form.js` + backend + L10n **DOKUNMA** (yalnız segment-create.css renk değerleri). Markup/yapı/font-ayrımı (SEG-A4) + scope izolasyon (`.segment-create-scope`) + payload/catalog/reach değişmez. Nötr gri oklch'leri app değişkenine bağlama (temayla değişmemeli). `.card`/global tema override etme. Yeni token/selektör ekleme dışında yapı değiştirme. Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0; CSS-only). git diff yalnız segment-create.css renk değerleri. Accent-türevi tüm renkler `var(--bs-primary)` / `rgba(var(--bs-primary-rgb), α)` / `color-mix(var(--bs-primary))`; success=`--bs-success`, danger/required=`--bs-danger`; nötr griler oklch kalır. Scope izolasyon + font-ayrımı (A4) korundu. `.card` override yok.
- **E4:** tema özelleştiriciden Primary Color = yeşil/mavi seçince pill/radio-card/recipe chip/reach chip/funnel/focus-ring/avatar **canlı** app primary'ye döner (Save butonu ile tutarlı); default'ta görünüm bugünküyle aynı (mor).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-A5 · Segment accent'ini app tema primary'sine bağla (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (SEG-A4 1518c2be / docs b3fdd565 üstü)> · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SEG-A5-frontend-theme-primary-binding.md (bu WP — token tablosu + ayrım kuralı)
2. frontend/Diten.Web/wwwroot/assets/css/segment-create.css (token bloğu satır ~14-30 + inline oklch'ler)
3. frontend/Diten.Web/wwwroot/assets/css/backbone-custom.css (satır ~572/780: app'in rgba(var(--bs-primary-rgb), .14/.08) deseni — referans)

AMAÇ (owner): tema özelleştiriciden Primary Color değişince app ikonları/butonları değişiyor ama seg-* accent (mor) sabit kaldığı için değişmiyor. Accent-türevi renkleri app tema değişkenlerine (--bs-primary / --bs-primary-rgb / --bs-success / --bs-danger) bağla ki canlı takip etsin. Nötr griler oklch kalır.

NE (yalnız segment-create.css renk değerleri):
 1) Token bloğu: --seg-accent→var(--bs-primary); --seg-accent-hover→color-mix(in srgb, var(--bs-primary), #000 12%); --seg-accent-dark→var(--bs-primary); --seg-accent-ring→rgba(var(--bs-primary-rgb),0.14); --seg-ok→var(--bs-success); --seg-danger→var(--bs-danger); --seg-required→var(--bs-danger).
 2) Token dışı inline accent/tint (yüksek-chroma hue 285): funnel fill oklch(0.6 0.15 285)→var(--bs-primary); pill hover border oklch(0.72 0.1 285)→var(--bs-primary); radio-card selected bg oklch(0.985 0.012 285)→rgba(var(--bs-primary-rgb),0.04) + ring 0.1→rgba(var(--bs-primary-rgb),0.1); recipe/reach chip bg oklch(0.96 0.018 285)→rgba(var(--bs-primary-rgb),0.08) + hover oklch(0.92 0.035 285)→rgba(var(--bs-primary-rgb),0.16); sample avatar bg oklch(0.955 0.02 285)→rgba(var(--bs-primary-rgb),0.1) + metin→var(--bs-primary); accent metin/ikon/AND-ALSO pill (yüksek-chroma 285)→var(--bs-primary). success hue150→var(--bs-success)/rgba(var(--bs-success-rgb),α); danger hue25→rgba(var(--bs-danger-rgb),α).
 3) NÖTR OKLCH KALIR: kart border oklch(0.93 0.006 285), beyaz oklch(1 0 0), heading/text/muted/label (chroma≤0.02), input border oklch(0.87/0.89 0.008 285), readonly bg, blok/değer-kabı grileri, every/any track, JSON pre bg. Ayrım: chroma≥0.1(285)=accent değiştir; chroma≤0.04=nötr kalır; hue150=success; hue25=danger.
KORU/YAPMA: _Form.cshtml/form.js/backend/L10n DOKUNMA (yalnız segment-create.css renk); markup/yapı/font-ayrımı(A4)/scope izolasyon/payload/catalog/reach değişmez; nötr grileri app değişkenine bağlama; .card/global tema override etme; başka modül.
NASIL: accent oklch'leri var(--bs-primary)/rgba(var(--bs-primary-rgb),α)/color-mix ile değiştir; mümkünse token merkezli (token'ı bağla, kullanımlar zaten var(--seg-accent) üzerinden geçiyorsa otomatik). rgba alfa değerleri backbone-custom deseninden (.08/.14 vb.).
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); git diff yalnız segment-create.css renk; accent→--bs-primary/rgba(--bs-primary-rgb)/color-mix, success→--bs-success, danger→--bs-danger; nötr griler oklch kaldı; scope+font-ayrımı(A4) korundu; .card override yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: accent renkleri app değişkenine bağlanamıyorsa; nötr/accent ayrımı belirsizse (DUR+sor); scope/font-ayrımı bozuluyorsa; _Form/form.js'e dokunmak gerekiyorsa; kapsam segment-create.css dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 64b9c956 · Agent: PASS (137/0, --no-build) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-sega5-verify @64b9c956
```
- ✅ **Scope:** yalnız `segment-create.css` (form.js/_Form/backend/L10n diff YOK).
- ✅ **Accent → app tema değişkeni:** `var(--bs-primary)` ×12 + `rgba(var(--bs-primary-rgb), α)` ×12 + `color-mix(in srgb, var(--bs-primary), #000 12%)` ×1 (hover); `--bs-success` ×3 (✓ checklist/include badge), `--bs-danger` ×5 (required/exclude/remove hover). **Yüksek-chroma accent oklch (hue 285) kalmadı = 0.**
- ✅ **Nötr korundu:** 26 nötr oklch (kart/input border, heading/text/muted/label chroma≤0.02, beyaz, track, JSON pre) — temayla değişmez. Agent iki isabetli karar: `source-badge` gerçek CSS değeri nötr gri (chroma 0.015) → dokunmadı; amber warning (hue 70-85) + blue info (hue 250-260) WP kapsamı (accent/success/danger) dışı → oklch bıraktı.
- ✅ **İzolasyon korundu:** `.card`/`:root` global tema override=0; `.segment-create-scope` scope + font-ayrımı (A4) değişmedi.
- ✅ **Build+test (CT izole, Release, GERÇEK build):** build 0-err; **Diten.Web.Tests 137/137** (agent `--no-build` yaptı fleet DLL-kilidi yüzünden; CT izole worktree'de gerçek rebuild ile teyit).
- ⏳ **E4:** tema özelleştiriciden Primary=yeşil/mavi → pill/radio-card/chip/funnel/ring canlı döner; default (mor #7367f0) görünüm bugünküyle aynı.

**Segment Create/Edit tema-duyarlı: accent artık app primary'yi takip ediyor. SEG-A/A2/A3/A4/A5 + B/C hepsi CT-E2.**
