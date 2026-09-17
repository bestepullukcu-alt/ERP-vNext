# WORK PACKAGE — WP-FREQ-D · Frekans konsolu L10n casing hotfix ("Yeni Politika" butonu görünmüyor)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`eacd66f2` üstü). **CANLI BUG (E4).** FREQ-A/B/C tamam ama `/CRM/VisitFrequencyPolicies` sayfasında **"Yeni Politika" butonu render edilmiyor** + satır-aksiyon/onay metinleri fallback'e düşüyor.

## Kök neden (CT tarafından canlı console ile teşhis edildi)
- `_IndexL10n.cshtml` payload'ı **`@Json.Serialize(new { NewPolicy = … })`** ile üretiyor → MVC'nin camelCase JSON policy'si anahtarları camelCase yapıyor (`"newPolicy"`, `"edit"`, `"archive"` …).
- Ama `index.js` üst-düzey anahtarları **PascalCase** okuyor: `L.NewPolicy`, `L.Actions`, `L.Archive`, `L.ArchiveConfirm`, `L.Delete`, `L.DeleteConfirm`, `L.Details`, `L.Edit`, `L.EmptyState`, `L.ErrorState`, `L.Filter`, `L.Loading`, `L.PerPeriod`, `L.QuickView`, `L.RecordArchived`, `L.RecordDeleted`, `L.Reset`, `L.ShowAll`.
- `dt-defaults.js:822` butonu **yalnız `addNewText` truthy ise** üretiyor. `L.NewPolicy === undefined` → buton yok.
- Nested map'ler (`statusLabels` — ve editör/detay payload'undaki `verdictLabels/reasonLabels/bandLabels`) camelCase okunuyor ve JSON'da da camelCase → **bunlar çalışıyor**; bu yüzden JSON'ı tümüyle PascalCase yapmak onları bozar.
- Canlı kanıt (console): `Object.keys(VfpL10n)` = `…,newPolicy,…,statusLabels` (camelCase); `VfpL10n.NewPolicy === undefined`; `.add-new` sayısı 0.
- Bu, memory `l10n-bridge-pascalcase-loader` gotcha'sının aynısı. Çalışan referans EligibilityPolicies `Dictionary + System.Text.Json.JsonSerializer.Serialize` kullanıp PascalCase korur.

## Kapsam (frontend, tek dosya — bridge)
`frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/index.l10n.js`: parse sonrası her **üst-düzey** anahtar için PascalCase (ilk harf büyük) alias üret; hem camelCase hem PascalCase çözülsün. Aliaslı objeyi `window.VfpL10n` (freeze) yap ve `window.L10n`'a merge et. Nested obje değerleri (statusLabels vb.) olduğu gibi kalsın (yalnız üst-düzey anahtar aliaslanır; nested map'in iç anahtarları dokunulmaz).

Örnek gövde:
```js
(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('vfp-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[VisitFrequencyPolicies] Localization payload could not be parsed.', error); }
    // Razor payload'ı anahtarları camelCase serialize eder (MVC JSON policy); konsol scriptleri ise
    // üst-düzey skaler anahtarları PascalCase (L.NewPolicy…), nested map'leri camelCase (L.statusLabels) okur.
    // Her üst-düzey anahtarın iki yazımını da sun → her iki okuma da çözülür (bkz memory: l10n-bridge-pascalcase-loader).
    const bridged = {};
    Object.keys(values).forEach(function (k) {
        bridged[k] = values[k];
        const pascal = k.charAt(0).toUpperCase() + k.slice(1);
        if (!(pascal in bridged)) bridged[pascal] = values[k];
    });
    window.L10n = Object.assign({}, window.L10n || {}, bridged);
    window.VfpL10n = Object.freeze(bridged);
})(window, document);
```

## KORU / YAPMA
- SADECE `index.l10n.js`. `_IndexL10n.cshtml`, `index.js`, resx, backend, FREQ-A/B/C diğer dosyaları, Segment, başka modül **DOKUNMA**. Nested map anahtarlarını (statusLabels/verdict/reason/band iç kodları) değiştirme. Yeni anahtar/vocabulary ekleme yok.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). git diff yalnız `index.l10n.js`.
- **E4:** `/CRM/VisitFrequencyPolicies` → DataTable toolbar'da **"Yeni Politika"** butonu görünür (tıkla→create offcanvas); satır aksiyon menüsü (Detay/Düzenle/Arşivle/Sil) + onay metinleri doğru; status rozeti hâlâ çalışır. Console: `window.VfpL10n.NewPolicy === "New Policy"`, `document.querySelectorAll('.add-new').length === 1`.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-D · Frekans konsolu L10n casing hotfix (MOD-0165-FU03, frontend, tek dosya)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: eacd66f2 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-D-l10n-casing-hotfix.md · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/index.l10n.js · (referans) frontend/Diten.Web/wwwroot/assets/js/CRM/EligibilityPolicies/index.l10n.js.

NE: SADECE index.l10n.js. Payload parse'ı sonrası her ÜST-DÜZEY anahtara PascalCase (ilk harf büyük) alias ekle (WP'deki gövdeyi kullan). window.VfpL10n = freeze(bridged); window.L10n'a bridged merge. Nested obje değerleri (statusLabels vb.) olduğu gibi; yalnız üst-düzey anahtar aliaslanır. Kök neden: index.js L.NewPolicy (PascalCase) okur ama Razor camelCase (newPolicy) üretir → dt-defaults.js:822 addNewText falsy → "Yeni Politika" butonu yok.
KORU/YAPMA: _IndexL10n.cshtml/index.js/resx/backend/FREQ-A/B/C diğer dosyaları/Segment/başka modül DOKUNMA. Yeni anahtar/vocabulary yok. Nested iç anahtarları değiştirme.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); git diff yalnız index.l10n.js. Ayrı commit. §22 TÜRKÇE. K13.
Durma: fix tek dosyada çözülemiyorsa; index.js/Razor değiştirmek gerekiyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: ded8209d · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqd-verify @ded8209d
```
- ✅ **Kapsam:** yalnız `index.l10n.js` (M). Başka tracked değişiklik yok (docs untracked hariç). index.js/Razor/resx/backend/FREQ-A/B/C/Segment değişmedi.
- ✅ **Bridge doğru:** parse sonrası her üst-düzey anahtara PascalCase alias (`k.charAt(0).toUpperCase()+k.slice(1)`, çakışma yoksa) → hem `newPolicy` hem `NewPolicy` çözülür; nested obje değerleri (statusLabels vb.) olduğu gibi; `window.VfpL10n = freeze(bridged)` + `window.L10n`'a merge. Kök neden (camelCase JSON vs PascalCase okuma) giderildi → `L.NewPolicy` truthy → dt-defaults.js:822 add-new grubu üretilir.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0** (8 s). JS-only değişiklik C# suite'i etkilemez, teyit edildi.
- ⏳ **E4:** hard-refresh sonrası `/CRM/VisitFrequencyPolicies` toolbar'da "Yeni Politika" butonu + satır menüsü/onay metinleri doğru; console `VfpL10n.NewPolicy==="New Policy"`, `.add-new`=1.

**Not:** Bu casing uyuşmazlığı FREQ-A ajanının referans desenden (Dictionary+JsonSerializer PascalCase) sapıp `@Json.Serialize(new{})` (camelCase) kullanmasından doğdu; kalıcı ders memory `l10n-bridge-pascalcase-loader`.
