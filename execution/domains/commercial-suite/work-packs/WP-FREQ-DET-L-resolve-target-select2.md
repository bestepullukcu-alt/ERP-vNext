# WORK PACKAGE — WP-FREQ-DET-L · Çözümleme "Kayıt" picker'ı aramalı (select2) (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`bcf77215` üstü). **E4 isteği:** Çözümleme senaryosundaki "Kayıt" dropdown'u düz select — kullanıcı aradığını bulamıyor ("Kayıt fieldındaki dropdownlar search'lü select olsun"). Editördeki/task-create fieldları gibi select2 aramalı olacak. **Frontend only** (`resolve.js` renderTargetPicker + renderScenarioContext + küçük select2 helper). Backend/L10n/css DEĞİŞMEZ.

## Bağlam / referans
- resolve.js picker'ları düz `<select class="form-select">` + `fillSelect` (satır 119; boş `<option value="">` placeholder zaten üretiliyor → select2 hazır). Yükseltme YOK.
- **Editör deseni (form.js, birebir kopyalanacak yaklaşım):** `bindSelect2`/`unbindSelect2`/`rebindSelect2`/`syncSelect2` — `$s.select2({ dropdownParent: $s.parent() })` + `change.vfpBridge` köprüsü (select2'nin jQuery-sentez change'ini native bubbling change'e taşır; `change.select2` re-read tetiklemez). form.js satır 44-82.
- select2 + jQuery Index sayfasında YÜKLÜ (index.js `initSelect2` inline-filter'da kullanıyor) → resolve.js kullanabilir.
- Picker akışı: select değişince `addEventListener('change', … showPicked(text))` → picker gizlenir, picked-chip gösterilir. select2 seçimi bu native change'i tetiklemeli (köprü ile).

## NE (frontend; yalnız resolve.js)
1. **Küçük select2 helper** (resolve.js içine, form.js deseninden): `hasSelect2()` + `bindSelect2(select)` (zaten bound ise atla; `select2({dropdownParent:$(select).parent()})` + `change.vfpBridge`→native `change` CustomEvent bubble) + `rebindSelect2(select)` (destroy+bind, re-fill sonrası snapshot için). jQuery yoksa sessiz no-op (düz select'e degrade).
2. **renderTargetPicker** (satır 352-381): picker `<select>`'lerine `select2` class ekle (`vfpRsTargetId`, `vfpRsTargetTerModel`). `fillSelect` ÇAĞRISINDAN SONRA `rebindSelect2(el(...))` çağır (options snapshot). Mevcut `change` listener (showPicked) KORUNUR — select2 seçimi köprü ile native change tetikler → picked-chip çalışır. territory-node: model select değişince node select re-fill + `rebindSelect2(node)`. Manuel-id (kind yok) dalı düz `<input>` kalır (select2 değil).
3. **renderScenarioContext** (satır 384-401, contact-territory saha bağlamı): `vfpRsCtxTerModel`/`vfpRsCtxTerNode` de aramalı olsun (tutarlılık) — `select2` class + re-fill sonrası `rebindSelect2`.
4. **id/sözleşme korunur:** `vfpRsTargetId` + `data-role="targetId"` + `targetIdValue()` (norm(el('vfpRsTargetId')?.value)) DEĞİŞMEZ. DET-K query kurgusu (SCENARIO_SELF_CONTEXT) DEĞİŞMEZ.

## KORU / YAPMA
- Backend/L10n/css DEĞİŞMEZ. Liste/index.js/editör/form.js/detay/details.js/_Resolve.cshtml markup/Segment/resolve engine DOKUNMA (yalnız resolve.js). `vfpRsTargetId`/`data-role="targetId"`/`targetIdValue`/showPicked-chip akışı + DET-K query korunur. select2 SADECE sunum+arama (değer/GUID sözleşmesi bit-bit aynı). jQuery yoksa düz select'e degrade (kırılma yok). dropdownParent = select'in `.diten-field` parent'ı (editör gibi).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff **yalnız resolve.js**. Backend/index.js/form.js/detay/_Resolve/css/resx diff YOK.
- **E4:** Çözümleme "Kayıt" dropdown'u aramalı (yazıp filtreleme); seçince picked-chip + "değiştir" çalışır; territory-node model+node ikisi de aramalı; contact-territory saha bağlamı aramalı; "Frekansı göster" doğru targetId ile çalışır. **wwwroot JS → Ctrl+F5 yeter.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-L · Çözümleme "Kayıt" picker'ı aramalı (select2) (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: bcf77215 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-L-resolve-target-select2.md · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/resolve.js (fillSelect ~119, renderTargetPicker ~352-381, renderScenarioContext ~384-401, showPicked ~318-347) · (referans DESEN — kopyala) form.js satır 44-82 (bindSelect2/unbindSelect2/rebindSelect2/syncSelect2 + change.vfpBridge, dropdownParent:$s.parent()) + form.js ~285-320 target picker select2 kullanımı.

NE (frontend; yalnız resolve.js):
 1) resolve.js'e küçük select2 helper (form.js deseninden): hasSelect2()+bindSelect2(select) [bound ise atla; $(select).select2({dropdownParent:$(select).parent()}); change.vfpBridge→native change CustomEvent bubble] + rebindSelect2(select) [destroy+bind]. jQuery/select2 yoksa sessiz no-op (düz select'e degrade).
 2) renderTargetPicker: <select>'lere "select2" class ekle (vfpRsTargetId, vfpRsTargetTerModel); fillSelect SONRASI rebindSelect2(el(...)). Mevcut change→showPicked listener KORU (köprü native change tetikler). territory-node: model change→node re-fill + rebindSelect2(node). Manuel-id (kind yok) dalı düz <input> kalır.
 3) renderScenarioContext (contact-territory saha): vfpRsCtxTerModel/vfpRsCtxTerNode de select2 class + re-fill sonrası rebindSelect2.
 4) vfpRsTargetId + data-role="targetId" + targetIdValue() + DET-K SCENARIO_SELF_CONTEXT query kurgusu DEĞİŞMEZ.
KORU/YAPMA: backend/L10n/css DEĞİŞMEZ; liste/index.js/editör/form.js/detay/details.js/_Resolve.cshtml/Segment/resolve engine DOKUNMA (yalnız resolve.js); id/data-role/targetIdValue/showPicked-chip/DET-K query korunur; select2 salt sunum+arama (değer/GUID sözleşmesi bit-bit aynı); jQuery yoksa degrade; dropdownParent=.diten-field parent.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 137/0; git diff YALNIZ resolve.js. Ayrı commit ("feat(freq): WP-FREQ-DET-L — Çözümleme Kayıt picker aramalı (select2) (MOD-0165-FU03)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: select2 native-change köprüsü showPicked'i tetiklemiyorsa (picked-chip çalışmıyor); değişiklik resolve.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: fbab7625 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detl-verify @fbab7625
```
- ✅ **Kapsam:** yalnız `resolve.js` (+42/-5). select2 helper seti (jq/hasSelect2/bindSelect2[dropdownParent:$parent + change.vfpBridge→native, originalEvent guard]/unbindSelect2/rebindSelect2); renderTargetPicker + renderScenarioContext select2 class + fillSelect sonrası rebind; territory-node model→node rebind; manuel-id düz input kaldı. jQuery yoksa sessiz no-op. **KORU=0** (vfpRsTargetId/data-role/targetIdValue/showPicked köprü/DET-K query korundu; backend/index.js/form.js/detay/_Resolve.cshtml/Segment dokunulmadı).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⚠️ **E4 bulgusu → DET-O:** contact/account picker `pageSize=200` capped → 128K/43K'da arama tam gelmiyor ("Bülent Akgün" bulunamadı). Fix DET-O (server-side/ajax select2 arama).

**DET-L KOMPLE (Kayıt picker aramalı). Kalan büyük-liste arama sorunu → DET-O.**
```
