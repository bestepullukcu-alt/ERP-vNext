# WORK PACKAGE — WP-ST-EDIT-F · KAPSAM buton stili (btn-outline-primary) + BU 2-dropdown yan yana + sub-header (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`e171e89f` üstü). **E4 rötuşu (canlı KAPSAM):** (1) scope-type butonları küçük/gri → **`btn btn-outline-primary`**; (2) İş birimi seçilince 2 dropdown (ülke filtresi + BU) **yan yana**, BU seçili ülkenin territory-model business unit'lerinden dolsun (mevcut davranış — teyit); (3) ScopeHelp'ten "Oyunun adresi." çıkar, kalanı **"KAPSAM — NEREDE" header altı sub-header**, alttaki help kaldır. **Frontend** (_Form.cshtml + form.js + strategy-create.css + resx). Backend DEĞİŞMEZ.

## Kanıt
- `form.js` `renderScopeTypeButtons` (~621): butonlar `st-scope-type`(+is-active) class'ı, title/sub span; custom css (`.st-scope-type` ~298: gri/küçük). Buton click → gizli scopeType.value + applyScopeType.
- `_Form.cshtml` BU block (~35-45): tek `col-12 col-md-4` içinde `#buFilterCountry`(mb-3) + `#businessUnitId` **alt alta**. `buFilterCountry` change → `loadScopeOptions()` → scope-options territory-BU feed'inden `businessUnits` yeniden dolar (ülke-türevli — çalışıyor).
- `_Form.cshtml`: `<h6>ScopeSection</h6>` (mb-4, sub yok) + altta `<div class="form-text">@Localizer["ScopeHelp"]</div>` (~52). ScopeHelp="Oyunun adresi. Bu stratejinin hangi kapsama uygulanacağını belirler: ...".

## NE (frontend; backend DEĞİŞMEZ)
1. **Butonlar → `btn btn-outline-primary`:** `renderScopeTypeButtons` her butonu `class="btn btn-outline-primary"` yapsın; aktif buton `active` (Bootstrap outline→dolu). Başlık + alt-etiket buton içinde kalabilir (btn içinde 2 satır: title + küçük sub) veya sadece başlık; mockup görünümüne göre. Butonlar `d-flex flex-wrap gap-2` container'da, mevcut boyuttan büyük (btn default padding). Mevcut `.st-scope-type*` css'i kaldır/uyumla. Aktif/gizli-input/applyScopeType mantığı KORUNUR.
2. **BU 2-dropdown yan yana:** BU block içindeki `#buFilterCountry` + `#businessUnitId`'yi `row g-2` içinde iki sütuna (`col-6`/`col-md-6`) al; `mb-3` yerine row-gap. buFilterCountry→loadScopeOptions→BU fill (ülkenin territory-model BU'ları) davranışı DEĞİŞMEZ (yalnız layout). Note/label'lar korunur.
3. **Sub-header + help kaldır:** ScopeHelp resx'inden "Oyunun adresi. " öneki çıkar (7 dil) → "Bu stratejinin hangi kapsama uygulanacağını belirler: tüm kiracı ya da tek bir ülke, tüzel kişi veya iş birimi." Bu metni **`<h6>` header'ının hemen altına** sub-header olarak koy (`<p class="text-muted small mb-4">` veya diğer bölüm açıklaması deseni). Alttaki `<div class="form-text">ScopeHelp</div>` (~52) KALDIR (tek yerde kalsın).

## KORU / YAPMA
- Backend DEĞİŞMEZ. scopeType gizli-input + applyScopeType + cascade (country→LE→BU/territory) + single-reference + scope-options feed + buFilterCountry→BU territory-türev doldurma + submit DEĞİŞMEZ (yalnız buton class + BU layout + help konumu). 2b sağ panel + Kimlik/Segment/Frekans/Ürün/İçerik DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. ScopeEditableHint korunur (o ayrı hint). Tema/L10n köprüsü.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + form.js + strategy-create.css + resx. Backend/Controller/ViewModel/Liste/Detay diff YOK.
- **E4:** scope butonları btn-outline-primary (büyük, primary); İş birimi'nde ülke+BU yan yana, BU ülkenin territory BU'larından dolu; header altında sub-header (Oyunun adresi. yok), altta tekrar help yok. **Razor+css+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-F · KAPSAM buton stili + BU yan yana + sub-header (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: e171e89f üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-F-scope-buttons-polish.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (KAPSAM: h6 ScopeSection + scopeTypeButtons + BU block ~35-45 buFilterCountry+businessUnitId + ScopeHelp ~52) · wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderScopeTypeButtons ~621, buFilterCountry ~691) · wwwroot/assets/css/strategy-create.css (.st-scope-type ~298) · resx (ScopeHelp/ScopeEditableHint). Mockup: /c/tmp/mockup-strategy.html Düzenle KAPSAM.

NE (frontend; backend DEĞİŞMEZ):
 1) renderScopeTypeButtons: buton class'ı "btn btn-outline-primary" (aktif=active); başlık+alt-etiket buton içinde; container d-flex flex-wrap gap-2; büyük (btn padding). .st-scope-type* css kaldır/uyumla. Gizli-input/applyScopeType/aktif mantığı KORU.
 2) BU block: #buFilterCountry + #businessUnitId'yi row g-2 içinde col-md-6 yan yana (mb-3 kaldır). buFilterCountry→loadScopeOptions→BU territory-türev fill DEĞİŞMEZ (yalnız layout).
 3) ScopeHelp resx (7 dil): "Oyunun adresi. " öneki çıkar. Bu metni <h6> ScopeSection altına sub-header (<p class="text-muted small mb-4">) koy; alttaki <div class="form-text">ScopeHelp</div> (~52) KALDIR. ScopeEditableHint kalır.
KORU/YAPMA: backend DEĞİŞMEZ; scopeType gizli-input+applyScopeType+cascade+single-reference+scope-options+buFilterCountry→BU+submit DEĞİŞMEZ (yalnız buton class + BU layout + help konum); 2b sağ panel + diğer bölümler DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+form.js+css+resx; backend/Controller/ViewModel/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-F — KAPSAM btn-outline-primary + BU 2-dropdown yan yana + sub-header (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: buton class değişimi applyScopeType/aktif-durumu bozuyorsa; BU layout territory-fill'i kırıyorsa; kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 0f350cd0 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steditf-verify @0f350cd0
```
- ✅ **Kapsam:** _Form.cshtml + form.js + strategy-create.css + 7 resx. **backend/Controller/ViewModel/liste/detay TEMİZ** ✓.
- ✅ Butonlar `btn btn-outline-primary` (+`active`); `.st-scope-type` kart css kaldırıldı; gizli #scopeType + applyScopeType + cascade DEĞİŞMEDİ. BU block `#buFilterCountry`+`#businessUnitId` `row g-2` col-6 yan yana (mb-3 kalktı); territory-türev BU fill korundu. ScopeHelp'ten "Oyunun adresi." (7 dil) çıkarıldı, metin h6 altı `<p text-muted small>` sub-header; alt form-text help kaldırıldı; ScopeEditableHint kaldı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: btn-outline scope butonları + BU yan yana + sub-header. **FLEET RESTART.**

**WP-ST-EDIT-F KOMPLE.**
```
