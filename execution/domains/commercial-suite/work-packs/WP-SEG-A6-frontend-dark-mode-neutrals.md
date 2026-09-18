# WORK PACKAGE — WP-SEG-A6 · Segment nötr renklerini tema-duyarlı (light+dark) yap (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (SEG-A5 `64b9c956` / docs `43768a4f` üstü). **Yalnız frontend (Diten.Web), tek dosya: `wwwroot/assets/css/segment-create.css`.** Owner: *"dark modda tasarım sorunları var — bazıları okunmuyor, bazılarının background'ı beyaz kalmış."* Kök neden: SEG-A3 nötr renkleri **sabit light-mode oklch** yazdı (beyaz bg `oklch(1 0 0)`, koyu metin `oklch(0.3 0.02 285)`); SEG-A5 accent'i tema'ya bağladı ama nötrleri kasıtlı sabit bıraktı → **dark mode'da beyaz input/kart + koyu metin okunmuyor**. (CT'nin A5'teki eksik değerlendirmesi: "nötr tema-bağımsız" light için doğruydu, light/dark ekseni atlandı.)

## Ölçülmüş girdi (CT)
- App **Bootstrap 5.3 `data-bs-theme=dark`** (core.css `[data-bs-theme=dark]` bloğu; `--bs-body-bg` light `#f5f5f9` → dark `#232333`). Aşağıdaki değişkenler **light+dark tanımlı** (otomatik ters döner): `--bs-body-bg` / `--bs-card-bg` / `--bs-body-color` / `--bs-heading-color` / `--bs-secondary-color` / `--bs-border-color` / `--bs-tertiary-bg` / `--bs-secondary-bg` / `--bs-body-color-rgb`.
- `.segment-create-scope` sarmalayıcı app shell içinde → `data-bs-theme` miras alır; bu değişkenleri kullanınca hem light hem dark doğru çözülür.
- Accent (A5: `--bs-primary`) + success/danger zaten dark-aware — DOKUNMA.

## Kapsam (yalnız segment-create.css nötr renk değerleri)
Tüm **düşük-chroma (≤0.04) hue-285 nötr oklch** + **beyaz** değerlerini tema-duyarlı bs değişkenlerine bağla:
| seg nötr (oklch) | rol | → |
|---|---|---|
| `oklch(1 0 0)` **kart/section yüzeyi** | surface | `var(--bs-card-bg)` |
| `oklch(1 0 0)` **input/textarea/select + seçili-olmayan pill/chip/every-any aktif btn** | kontrol yüzeyi | `var(--bs-body-bg)` |
| `oklch(0.28 0.02 285)` | başlık | `var(--bs-heading-color)` |
| `oklch(0.3 / 0.35 / 0.42 …02 285)` | ana metin / label | `var(--bs-body-color)` |
| `oklch(0.5 / 0.55 / 0.58 / 0.62 …015 285)` | muted/lead/help | `var(--bs-secondary-color)` |
| `oklch(0.87 / 0.89 / 0.9 / 0.92 / 0.93 / 0.94 / 0.95 …00-0.008 285)` | kart/input/blok/koşul border + ayraç çizgi | `var(--bs-border-color)` |
| `oklch(0.975 0.004 285)` readonly-input bg / JSON pre bg | girintili yüzey | `var(--bs-secondary-bg)` |
| `oklch(0.98 / 0.982 / 0.985 …003-0.012 285)` value-container / block-header / reads-as / section tint bg | hafif yüzey | `var(--bs-tertiary-bg)` |
| `oklch(0.94 / 0.945 0.005-0.006 285)` every-any track / funnel track | girintili yüzey | `var(--bs-secondary-bg)` |

**Beyaz `oklch(1 0 0)` bağlam ayrımı zorunlu:** her kullanımın hangi element olduğunu CSS'te oku — `.seg-card`/section = `--bs-card-bg`; `.seg-input`/kontrol/seçili-olmayan pill/chip/toggle-aktif-btn = `--bs-body-bg`.

**Warning/Info (dark'ta da okunur yap):** seg-* içindeki amber warning (hue 70-85) + blue info (hue 250-260) not kutuları varsa Bootstrap subtle değişkenlerine bağla (`--bs-warning-bg-subtle`/`--bs-warning-text-emphasis`, `--bs-info-bg-subtle`/`--bs-info-text-emphasis`) ki dark'ta okunsun. (app `alert alert-*` class'ları zaten dark-aware — onlara dokunma.)

## KORUNACAK / YAPMA
- **DOKUNMA:** accent (A5: `--bs-primary`/`rgba(--bs-primary-rgb)`/color-mix), success `--bs-success`, danger `--bs-danger` — dark-aware zaten. `_Form.cshtml`/`form.js`/backend/L10n. Markup/yapı/font-ayrımı (A4)/`.segment-create-scope` izolasyon/payload/catalog/reach. `.card`/global tema override etme (yalnız `seg-*` nötr renk değerleri). Hafif violet-tint kaybı kabul (tutarlılık > tint). Başka modül.
- **DİKKAT:** dark'ta accent tint alfa (radio-card selected `rgba(--bs-primary-rgb,0.04)`) çok soluk kalıyorsa alfa'yı hafif artırabilirsin (0.04→0.08) — ama bu accent değil kontrast düzeltmesi, minimal tut.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0; CSS-only). git diff yalnız segment-create.css. Sabit beyaz bg `oklch(1 0 0)` = 0 (hepsi `--bs-card-bg`/`--bs-body-bg`); sabit koyu metin nötr oklch = 0 (hepsi `--bs-body-color`/`--bs-heading-color`/`--bs-secondary-color`); border nötr = `--bs-border-color`; yüzey tint = `--bs-tertiary-bg`/`--bs-secondary-bg`. Accent(A5)/font-ayrımı(A4)/scope korundu. `.card` override yok.
- **E4:** dark mode (`data-bs-theme=dark`) — tüm section okunur (input/kart koyu yüzey, metin açık); light mode bugünküyle aynı (beyaz kart/koyu metin).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-A6 · Segment nötr renklerini tema-duyarlı (light+dark) yap (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (SEG-A5 64b9c956 / docs 43768a4f üstü)> · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SEG-A6-frontend-dark-mode-neutrals.md (bu WP — değişken tablosu)
2. frontend/Diten.Web/wwwroot/assets/css/segment-create.css (nötr oklch'ler: bg/metin/border/yüzey)
3. frontend/Diten.Web/wwwroot/assets/vendor/css/core.css ([data-bs-theme=dark] bloğu — --bs-body-bg/card-bg/body-color/border-color/tertiary-bg light+dark değerleri, referans)

AMAÇ (owner): dark modda seg-* okunmuyor / background beyaz kalmış. Nötr renkler sabit light-mode oklch (beyaz bg + koyu metin) olduğu için. Nötrleri app'in light/dark-duyarlı bs değişkenlerine bağla (accent/success/danger A4/A5'te zaten bağlı — DOKUNMA).

NE (yalnız segment-create.css nötr renk değerleri):
 - Beyaz oklch(1 0 0): kart/section yüzeyi→var(--bs-card-bg); input/textarea/select + seçili-olmayan pill/chip + every-any aktif btn (kontrol yüzeyi)→var(--bs-body-bg). (her kullanımın hangi element olduğunu CSS'te oku, bağlama göre seç.)
 - Metin: başlık oklch(0.28…)→var(--bs-heading-color); ana metin/label oklch(0.3/0.35/0.42…02 285)→var(--bs-body-color); muted/lead/help oklch(0.5/0.55/0.58/0.62…015 285)→var(--bs-secondary-color).
 - Border: oklch(0.87/0.89/0.9/0.92/0.93/0.94/0.95…00-008 285) (kart/input/blok/koşul border + ayraç çizgi)→var(--bs-border-color).
 - Yüzey: readonly/JSON-pre bg oklch(0.975 0.004 285)→var(--bs-secondary-bg); value-container/block-header/reads-as/section tint oklch(0.98/0.982/0.985…)→var(--bs-tertiary-bg); every-any track + funnel track oklch(0.94/0.945…)→var(--bs-secondary-bg).
 - Warning/info seg-* not kutuları (varsa) amber(hue70-85)→--bs-warning-bg-subtle/--bs-warning-text-emphasis, blue(hue250-260)→--bs-info-bg-subtle/--bs-info-text-emphasis (dark okunur). app alert-* class'larına DOKUNMA.
 - (dark'ta radio-card selected rgba(--bs-primary-rgb,0.04) çok soluksa 0.08'e çıkarabilirsin — minimal kontrast düzeltmesi.)
KORU/YAPMA: accent(--bs-primary)/success/danger DOKUNMA (dark-aware); _Form.cshtml/form.js/backend/L10n DOKUNMA; markup/yapı/font-ayrımı(A4)/scope izolasyon/payload/catalog/reach değişmez; .card/global tema override etme (yalnız seg-* nötr renk); başka modül.
NASIL: her nötr oklch'i rol+bağlamına göre uygun bs değişkenine çevir. Düşük-chroma(≤0.04 hue285)+beyaz = nötr→çevir; accent/success/danger = zaten tema.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); git diff yalnız segment-create.css; sabit beyaz oklch(1 0 0)=0, sabit koyu nötr metin oklch=0 (hepsi --bs-*); border=--bs-border-color, yüzey=--bs-tertiary/secondary-bg; accent(A5)/font(A4)/scope korundu; .card override yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: nötr/accent ayrımı belirsizse (DUR+sor); beyaz bağlam (card vs input) çözülemiyorsa; scope/font-ayrımı bozuluyorsa; _Form/form.js gerekiyorsa; kapsam segment-create.css dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 49958460 · Agent: PASS (test env-blocked) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-a6-verify @e7273e71
```
- ✅ **Scope:** yalnız `segment-create.css` (102+/91−). form.js/_Form/backend/L10n diff YOK.
- ✅ **Sabit beyaz background = 0:** literal `oklch(1 0 0)` background/border deklarasyonu kalmadı. Kalan beyaz = yalnız accent/success FILL üstündeki ön-plan (pill/chip/radio-mark metni + ✓ tik) — iki temada da doğru beyaz.
- ✅ **Nötr → tema-duyarlı bs değişkenleri:** `--bs-body-color` ×15 (metin/label), `--bs-secondary-color` ×17 (muted/lead/help), `--bs-border-color` ×24 (border/ayraç), tertiary/secondary-bg ×10 (yüzey tint/track), `--seg-card-bg=var(--bs-card-bg)` + `--seg-control-bg=var(--bs-body-bg)` (token-merkezli: kart yüzeyi vs kontrol yüzeyi ayrıştırıldı). Warning amber→`--bs-warning-*-subtle/emphasis`, info blue→`--bs-info-*` (dark okunur).
- ✅ **Korundu:** accent (A5: `--bs-primary` ×12), success/danger, A4 font-ayrımı (Inter seçici ×2), `.segment-create-scope` scope (×177). `.card`/global tema override yok. Kontrast düzeltmesi: radio-card selected tint 0.04→0.08 (WP-izinli, dark'ta soluk kalmasın).
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/137** (agent fleet DLL-kilidi yüzünden koşamamıştı; CT izole worktree'de gerçek rebuild ile teyit).
- ⏳ **E4:** dark mode (`data-bs-theme=dark`) canlı görsel — tüm section okunur; light bugünküyle aynı.

**Segment Create/Edit tam tema-uyumlu: light+dark + primary-duyarlı. SEG-A/A2/A3/A4/A5/A6 + B/C hepsi CT-E2.**
