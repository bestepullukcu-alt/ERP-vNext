# WORK PACKAGE — WP-ST-EDIT-L · Segment rol toggle → single select2 dropdown + font küçült (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`fab3451b` üstü). **E4 rötuşu:** (1) segment fontları hâlâ büyük → **0.8125rem (WCN)**; (2) rol seçimi (birincil/ikincil/hariç-not) **toggle** yerine **single select2 dropdown**. **Frontend** (form.js + strategy-create.css). Backend/resx DEĞİŞMEZ (BindingRole_* L10n mevcut).

## Kanıt
- `form.js` **select2 altyapısı YOK** (plain select'ler; vocabSelect ~164 = form-select). Rol render (~234-250): `.st-seg-role js-role-btn` toggle buton grubu + hidden `.js-role` input; toggle tıklama → bindingRole set + re-render. SegmentBindingsJson (segmentId/bindingRole/sortOrder) sync'ten `.js-role` okunur.
- Ayna (select2 deseni): VFP editör `wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js` — bindSelect2/unbindSelect2/rebindSelect2 (`$s.select2({dropdownParent:$s.parent()})` + `change.vfpBridge`→native change). select2+jQuery tenant shell'de yüklü.
- İsim `.st-seg-name`/`.st-segment-choice-name` 0.875rem (EDIT-K) — kullanıcı hâlâ büyük diyor.

## NE (frontend; backend/resx DEĞİŞMEZ)
1. **Rol → single select2 dropdown:** toggle buton grubu (`.st-seg-role`/`.st-seg-roles`) yerine tek `<select class="select2 form-select form-select-sm js-role">` — seçenekler `cfg.bindingRoles` (primary/secondary/exclusion-note) + L10n `BindingRole_*` (birincil/ikincil/hariç-not). Rol her zaman dolu (boş seçenek yok; varsayılan yeni segmentte ilk=primary/sonraki=secondary korunur). **select2 init:** form.js'e küçük select2 helper (VFP deseni: bindSelect2 `dropdownParent:$s.parent()` + `change.vfpBridge`→native) ekle; `renderSegments` sonrası segment listesindeki `.js-role` select'lere init/rebind. Rol değişimi: state.segments[i].bindingRole güncelle (mümkünse **tam re-render yapmadan**, select2 flicker olmasın; select change → state + gerekiyorsa yalnız o satır). jQuery/select2 yoksa düz select'e degrade. SegmentBindingsJson sözleşmesi (js-role → bindingRole) KORUNUR.
2. **Font küçült:** `.st-seg-name` + `.st-segment-choice-name` 0.875rem → **0.8125rem** (WCN/13px). role select form-select-sm zaten küçük. Section h6 (SEGMENTLER — KİM) standart kalır (diğer bölümlerle tutarlı).
3. **İsim rengi daha siyah:** EDIT-K'da `var(--bs-heading-color)` yapıldı ama bu temada koyu-gri kalıyor (pür siyah değil). `.st-seg-name` + `.st-segment-choice-name` color → **`var(--bs-emphasis-color)`** (Bootstrap 5.3 yüksek-vurgu ~siyah; tema-duyarlı, dark'ta açık). emphasis-color yoksa fallback koyu (`#2f3349`/near-black). Gerekirse font-weight 500 (isim `.st-seg-name` zaten 500; picker name'e de 500 eklenebilir okunurluk için).
4. **Kaldır → sadece ikon:** segment satırındaki "Kaldır" butonu (`btn btn-label-danger` + `<i bx-trash> Kaldır` metinli) → **sadece ikon** (liste DataTable row-action detay/ikon butonları gibi): `<i class="bx bx-trash">` tek ikon, metin yok; küçük ikon-buton stili (row-action deseni, ör. `btn btn-icon btn-sm` veya listedeki `renderActions` ikon buton görünümü). `title`/`aria-label` = Kaldır (erişilebilirlik). js-remove davranışı KORUNUR.

## KORU / YAPMA
- Backend/resx DEĞİŞMEZ (BindingRole_* mevcut). renderSegments state/SegmentBindingsJson/homojen/SubjectType türetme/picker/Kaldır DEĞİŞMEZ (yalnız rol kontrolü toggle→select2 + font). KAPSAM/Frekans/Ürün/İçerik/sağ panel/resolved DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. Beyaz bg (card-bg)/badge rounded-pill korunur. Tema/L10n köprüsü. `.st-seg-role*` toggle css kaldırılır/kullanılmaz.
- **DUR:** select2-on-dynamic-row bindingRole sync'i veya SegmentBindingsJson'ı bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + strategy-create.css. Backend/resx/_Form/Controller/liste/detay diff YOK (mümkünse _Form dokunma).
- **E4:** rol single select2 dropdown (birincil/ikincil/hariç-not; seçim çalışır, kaydedilir); fontlar küçük (0.8125rem). **js/css → Ctrl+F5.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-L · Segment rol → single select2 + font küçült (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: fab3451b üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-L-role-select2-font.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderSegments rol ~234-250, vocabSelect ~164, sync/state) · AYNA select2 deseni frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (bindSelect2/rebindSelect2 + change.vfpBridge) · frontend/Diten.Web/wwwroot/assets/css/strategy-create.css (.st-seg-name/.st-segment-choice-name ~386, .st-seg-role* ~392-412).

NE (frontend; backend/resx DEĞİŞMEZ):
 1) Rol toggle (.st-seg-role/.st-seg-roles buton grubu) → tek <select class="select2 form-select form-select-sm js-role"> (cfg.bindingRoles + L10n BindingRole_*; boş seçenek yok; varsayılan primary/secondary korunur). form.js'e VFP deseninde küçük select2 helper (bindSelect2 dropdownParent:$s.parent() + change.vfpBridge→native) ekle; renderSegments sonrası .js-role select'lere init/rebind. Rol değişimi state.segments[i].bindingRole güncelle (select2 flicker için tam re-render yapma; select change→state). jQuery/select2 yoksa düz select degrade. SegmentBindingsJson (js-role→bindingRole) KORU.
 2) .st-seg-name + .st-segment-choice-name 0.875rem → 0.8125rem. .st-seg-role* toggle css kaldır/kullanma.
 3) .st-seg-name + .st-segment-choice-name color → var(--bs-emphasis-color) (daha siyah; heading-color koyu-gri kalıyor). emphasis yoksa near-black fallback.
 4) Segment satırı "Kaldır" butonu (btn btn-label-danger + ikon+metin) → SADECE İKON (liste row-action ikon butonu gibi: btn btn-icon btn-sm / renderActions deseni, <i class="bx bx-trash">, metin yok, title/aria-label=Kaldır). js-remove davranışı KORU.
KORU/YAPMA: backend/resx DEĞİŞMEZ; renderSegments state/SegmentBindingsJson/homojen/SubjectType/picker/js-remove davranışı DEĞİŞMEZ (yalnız rol kontrol + font + isim rengi + Kaldır ikon); KAPSAM/Frekans/Ürün/İçerik/sağ panel/resolved DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; beyaz bg/badge korunur; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+css; backend/resx/_Form/Controller/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-L — segment rol single select2 + font küçült (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: select2-on-dynamic bindingRole sync/SegmentBindingsJson bozuluyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 78c23a4a · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitl-verify @78c23a4a
```
- ✅ **Kapsam:** form.js + strategy-create.css. **backend/resx/_Form/Controller/liste/detay TEMİZ** ✓.
- ✅ Rol toggle → single `<select class="select2 js-role">` (VFP deseni bindRoleSelect2 dropdownParent + change.stRoleBridge→native, minimumResultsForSearch:Infinity; jQuery yoksa düz select degrade; **presentation-only — .js-role/sync/SegmentBindingsJson byte-for-byte korundu, tam re-render yok, flicker yok**). Font 0.875→0.8125rem; isim color → var(--bs-emphasis-color) (near-black fallback); Kaldır → `btn btn-icon btn-sm btn-label-danger` sadece `bx-trash` (title/aria=Kaldır, js-remove korundu). Toggle css kalktı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: rol select2 dropdown + küçük font + siyah isim + Kaldır ikon. **js/css → Ctrl+F5.**

**WP-ST-EDIT-L KOMPLE.**
```
