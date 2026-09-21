# WORK PACKAGE — WP-SEG-H · Parametre değer kontrolü chip görünümü (channel/purpose) (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`387fbe33` üstü). **Yalnız frontend (`form.js`).** Owner: SEG-G channel/purpose'u `<select>` dropdown yaptı; owner **eligibility ana değeri gibi chip** istiyor ("Pick one · Catalog list" + chip'ler + "or type a value").

## Ölçülmüş girdi (CT)
- `parameterFields` (form.js:449-489): enum parametre için `<select class="seg-value-input js-node-param">` render ediyor (SEG-G). Değer `condition.parameters[name]`'e `js-node-param` change handler'ıyla yazılıyor.
- Ana değer chip deseni MEVCUT: `chipButton` (382-385: `seg-chip js-value-chip is-on` + `data-node/data-value` + ✓), `renderValueControl` chip-wrap (394-403: `seg-chip-wrap js-chips`), hydrateControls chip doldurma, source badge, "or type a value" free-text. **Parametre bunu reuse etmeli** (tek-değer varyantı).
- Parametre value-source: `definition.parameterValueSources[name]` (kind=`enum`, `allowedValues`) — SEG-G'den; source label "Catalog list" (valueSource.kind → source badge, ana değerle aynı eşleme).

## Kapsam (yalnız form.js — payload DEĞİŞMEZ)
1. **`parameterFields` enum → chip kutusu:** `<select>` yerine ana değer chip kutusu görünümü (`seg-value-box` + "Pick one" prompt + **source badge "Catalog list"** + chip'ler + **"or type a value"** free-text). Parametre **tek-değer** → **single-select** chip (biri seçili; başka chip'e tıklayınca değişir; seçili chip'e tekrar tıklayınca temizlenir). Label (`channel */purpose *`) korunur.
2. **Chip handler (parametre):** `js-param-chip` click → `condition.parameters[name] = value` (single). Free-text (`js-param-freetext`) → `condition.parameters[name]` serbest değer (listede olmayan değer seçili chip olarak korunur — SEG-G'deki "hint, kısıt değil" davranışı).
3. **hydrateControls:** enum parametre chip'lerini doldur (seçili = `parameters[name]`); **edit restore** (kayıtlı değer seçili/serbest chip).
4. **Reach:** parametre değişince mevcut reach tetikleme korunur (js-node-param ile aynı debounced /preview).
5. **Value-source'suz parametre** (maxDepth/subjectId — zaten Phase-1 gizli/opsiyonel) → değişmez (bare, mevcut davranış).

## KORU / YAPMA
- **`condition.parameters[name]` payload byte-identical** (buildNodes + parameters map değişmez — yalnız giriş kontrolü chip). Ana değer chip/reference-set, canlı reach (SEG-C), catalog-güdümlülük, same-origin proxy, SEG-D/E/F/G DEĞİŞMEZ. Backend/DTO/katalog DOKUNMA. **Yeni CSS YOK** (mevcut `seg-chip`/`seg-chip-wrap`/`seg-value-box`/`seg-chip-check` reuse). Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). channel/purpose eligibility ana değeriyle **aynı chip görünümü** (source badge "Catalog list" + chip toggle + "or type a value"); single-select (tek değer); value-source'suz parametre bare kalır; `parameters` map + buildNodes byte-identical (git diff: değer yazımı aynı slot); edit restore; reach param değişince yenilenir. Yeni CSS yok.
- **E4:** Eligibility koşulunda channel=email, purpose=medical-visit chip olarak seçilir (dropdown değil).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-H · Parametre değer kontrolü chip görünümü (channel/purpose) (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (387fbe33 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-H-parameter-chip-control.md · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (parameterFields 449-489 SEG-G select; chipButton 382-385, renderValueControl 387-403, hydrateControls chip doldurma, source badge, free-text; js-node-param handler + reach tetikleme).

AMAÇ (owner): SEG-G channel/purpose'u <select> yaptı; eligibility ANA DEĞERİ gibi chip olsun ("Pick one · Catalog list" + chip + "or type a value").

NE (yalnız form.js — payload DEĞİŞMEZ):
 1) parameterFields enum → ana değer chip kutusu görünümü (seg-value-box + prompt + source badge "Catalog list" (valueSource.kind eşlemesi) + chip'ler + "or type a value" free-text). Parametre TEK-DEĞER → single-select chip (biri seçili; başka chip değiştirir; seçiliye tekrar tıkla temizler). Label channel*/purpose* korunur.
 2) js-param-chip click → condition.parameters[name]=value (single); js-param-freetext → parameters[name] serbest (listede olmayan değer seçili chip korunur, SEG-G hint davranışı).
 3) hydrateControls enum parametre chip doldur (seçili=parameters[name]); edit restore.
 4) reach: parametre değişince mevcut debounced /preview tetikleme korunur.
 5) value-source'suz parametre (maxDepth/subjectId) bare kalır (değişmez).
KORU/YAPMA: condition.parameters[name] payload byte-identical (buildNodes+parameters map; yalnız giriş kontrolü); ana değer chip/reference-set, canlı reach (SEG-C), catalog-güdümlülük, same-origin proxy, SEG-D/E/F/G DEĞİŞMEZ; backend/DTO/katalog DOKUNMA; yeni CSS YOK (mevcut seg-chip/seg-chip-wrap/seg-value-box/seg-chip-check reuse); başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); channel/purpose ana değerle aynı chip (source badge + toggle + free-text); single-select; value-source'suz bare; parameters map + buildNodes byte-identical (git diff aynı slot yazımı); edit restore; reach param değişince yenilenir; yeni CSS yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: chip single-select parameters[name]'e yazamıyorsa; payload parameters şekli değişiyorsa (DUR); yeni CSS gerekiyorsa (DUR+raporla); kapsam form.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```text
Commit: 3d1d6383 · Agent: PASS (--no-build) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-segh-verify @3d1d6383
```
- ✅ **Scope:** yalnız form.js (+63/−13). Backend/DTO/katalog/CSS dokunulmadı.
- ✅ **Payload byte-identical:** buildNodes + applyAttribute `kept[]` parameters map değişmedi; `js-param-chip`/`js-param-freetext` değeri **aynı `condition.parameters[name]` slotuna** yazıyor (diff'teki `+condition.parameters ||= {}` yalnız null-guard). Eski select'in yazdığı slot.
- ✅ **Chip görünümü:** `parameterFields` enum dalı → `seg-value-box` + prompt + **source badge "Catalog list"** (ana değerle aynı `sourceBadge` eşlemesi) + `seg-chip`/`js-param-chip` + `seg-freetext` "or type a value". **Single-select** (satır 1155: seçili chip'e tekrar tıkla → temizle). Value-source'suz parametre (maxDepth/subjectId) bare kalır; non-enum text input + change handler korundu.
- ✅ **Reach + restore:** her iki handler `render()→refreshDerived()→schedulePreview()` çağırıyor (debounced /preview korundu); edit restore `condition.parameters[name]` okumasıyla.
- ✅ **Yeni CSS YOK:** tüm sınıflar segment-create.css'te mevcut (diff 0).
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/0**.
- ℹ️ **Karar notu:** prompt metni yeni L10n (7 dil resx) gerektirmesin diye mevcut `L.ValuePick` reuse (WP "yalnız form.js" kısıtı). İşlev single-select. Ayrı "Pick one" metni istenirse ayrı resx WP.
- ⏳ **E4:** channel=email / purpose=medical-visit chip olarak seçilir (dropdown değil).
