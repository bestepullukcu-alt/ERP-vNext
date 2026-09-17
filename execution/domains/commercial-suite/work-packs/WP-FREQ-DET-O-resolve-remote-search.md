# WORK PACKAGE — WP-FREQ-DET-O · Çözümleme Kayıt picker'ında contact/account server-side (ajax) arama (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (DET-M landing sonrası HEAD üstü — resolve.js SIRALI). **E4 bug:** Kayıt picker'ı (DET-L select2) aramalı ama contact/account listesi `pageSize=200` ile capped; 128K contact / 43K account'ta aranan kayıt (ör. "Bülent Akgün") ilk 200'de yoksa bulunamıyor. **Frontend only** (`resolve.js`). Backend/proxy DEĞİŞMEZ.

## Kök neden (CT doğruladı)
- `resolve.js` READERS: `contact: () => getJson('/contacts?pageSize=200')`, `account: () => getJson('/accounts?pageSize=200')` — ilk 200 kayıt client-side yüklenir, select2 yalnız o 200 içinde arar.
- Backend hazır: `ContactController.List` ve `AccountController.List` `[FromQuery] string? search` alıyor; `ListContactsQuery` handler (ContactQueryHandlers.cs:188) ve dedicated `SearchContactsHandler` (280) **aynı** `_contacts.ListAsync(tenantId, Search, page, pageSize, …)`'i çağırıyor → List'in `search`'ü isim aramasıdır. Web proxy `/contacts`→`/api/crm/contacts{QueryString}`, `/accounts`→`/api/crm/accounts{QueryString}` zaten `search`'ü iletir. **Backend/proxy değişikliği YOK.**

## NE (frontend; yalnız resolve.js)
1. **Remote select2 helper** `bindSelect2Remote(select, kind)` (DET-L helper setinin yanına): select2 `ajax` ile — arama terimini backend'e gönderir:
   - `ajax`: `delay: 250`, sorgu `getJson('/'+plural+'?search='+encodeURIComponent(term)+'&pageSize=20')` (envelope→`data.items`), sonuç `mapOption` ile `{id:value, text}` → select2 `results`. (custom `transport` veya `url`+`data`+`processResults`; getJson/envelope/mapOption REUSE.)
   - `plural`: `{ contact:'contacts', account:'accounts' }[kind]`.
   - `minimumInputLength: 0` (boş açılışta ilk 20), `placeholder`, `allowClear` opsiyonel, `dropdownParent: $(select).parent()`, `change.vfpBridge`→native change (DET-L köprüsü REUSE → showPicked çalışır).
   - jQuery/select2 yoksa sessiz no-op (düz select'e degrade — DET-L gibi).
2. **renderTargetPicker** (satır ~386-410): `REMOTE_KINDS = new Set(['contact','account'])`. targetType REMOTE_KINDS içindeyse: `<select class="form-select select2" id="vfpRsTargetId" data-role="targetId"><option value=""></option></select>` (fillSelect/loadOptions preload YOK) + `bindSelect2Remote(el('vfpRsTargetId'), targetType)` + mevcut `change→showPicked` listener KORU. Diğer kind'ler (segment/campaign/concept-node/audience-profile/territory-node/…) DET-L davranışını (loadOptions preload + rebindSelect2) KORUR — DEĞİŞMEZ.
3. **id/sözleşme korunur:** `vfpRsTargetId` + `data-role="targetId"` + `targetIdValue()` + showPicked-chip + DET-K SCENARIO_SELF_CONTEXT query DEĞİŞMEZ. Ajax seçiminde select2 seçilen `<option selected>`'ı DOM'a ekler → `selectedOptions[0].text` (showPicked) ve `targetIdValue()` çalışır.

## KORU / YAPMA
- Backend/proxy/L10n/css DEĞİŞMEZ. Liste/index.js/editör/form.js/detay/details.js/_Resolve.cshtml/Segment/resolve engine DOKUNMA (yalnız resolve.js). READERS'ın diğer kind'leri + loadOptions/nameOf (detay read-view kullanır) DEĞİŞMEZ — yalnız renderTargetPicker'ın contact/account dalı remote'a geçer. DET-K query + DET-L select2 (diğer kind'ler) + DET-M verdict DEĞİŞMEZ. Uydurma veri YOK (gerçek /contacts,/accounts search). select2 salt sunum+arama; değer=seçilen GUID.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff **yalnız resolve.js**. Backend/proxy/index.js/form.js/detay/_Resolve/css/resx diff YOK.
- **E4:** contact senaryosu (ör. "Bir doktor") Kayıt picker'ında "Bülent Akgün" yazınca backend'den gelir ve seçilebilir; account senaryosunda da isim/kod ile arama tüm kayıtlarda çalışır; seçince picked-chip + "değiştir"; "Frekansı göster" doğru targetId. **wwwroot JS → Ctrl+F5 yeter.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-M landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-O · Çözümleme Kayıt picker contact/account server-side (ajax) arama (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-M commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-O-resolve-remote-search.md · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/resolve.js (getJson/envelope ~56-61, READERS ~64-76, mapOption ~78-92, loadOptions ~93, select2 helper DET-L ~125-155, renderTargetPicker ~379-410) · (referans) services/.../ContactController.cs (List search) + ContactQueryHandlers.cs (ListContactsQuery→ListAsync search).

KÖK NEDEN: READERS.contact/account pageSize=200 capped → 128K/43K'da arama tam gelmiyor. Backend List `search` hazır, proxy iletir. Backend/proxy DEĞİŞMEZ.

NE (frontend; yalnız resolve.js):
 1) bindSelect2Remote(select, kind) helper (DET-L helper'larının yanına): select2 ajax — delay:250, term→getJson('/'+plural+'?search='+encodeURIComponent(term)+'&pageSize=20') (envelope data.items), mapOption→{id:value,text}→select2 results (getJson/envelope/mapOption REUSE). plural={contact:'contacts',account:'accounts'}[kind]. minimumInputLength:0, placeholder, dropdownParent:$(select).parent(), change.vfpBridge→native change (DET-L köprüsü reuse). jQuery yoksa no-op.
 2) renderTargetPicker: REMOTE_KINDS=Set(['contact','account']). targetType bunlardaysa <select class="form-select select2" id="vfpRsTargetId" data-role="targetId"><option value=""></option></select> (preload YOK) + bindSelect2Remote(el('vfpRsTargetId'),targetType) + change→showPicked KORU. Diğer kind'ler DET-L davranışını (loadOptions+rebindSelect2) KORUR.
 3) vfpRsTargetId + data-role + targetIdValue() + showPicked + DET-K query DEĞİŞMEZ. Ajax seçimi <option selected> ekler → selectedOptions[0].text + targetIdValue çalışır.
KORU/YAPMA: backend/proxy/L10n/css DEĞİŞMEZ; liste/index.js/editör/form.js/detay/details.js/_Resolve.cshtml/Segment/resolve engine DOKUNMA (yalnız resolve.js); READERS diğer kind'ler + loadOptions/nameOf DEĞİŞMEZ (yalnız renderTargetPicker contact/account dalı remote); DET-K/DET-L(diğer kind)/DET-M korunur; uydurma veri YOK; select2 salt sunum+arama.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 137/0; git diff YALNIZ resolve.js. Ayrı commit ("feat(freq): WP-FREQ-DET-O — Çözümleme Kayıt picker contact/account server-side arama (ajax select2) (MOD-0165-FU03)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: List search isim eşleştirmiyorsa (beklenmiyor, ListAsync search); ajax showPicked/targetIdValue'yu bozuyorsa; değişiklik resolve.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: 5c0a8708 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detmon-verify @5003e3ae
```
- ✅ **Kapsam:** yalnız resolve.js (+54). `REMOTE_KINDS=Set(['contact','account'])` + `REMOTE_PLURAL` + `bindSelect2Remote(select,kind)` (select2 ajax.transport → `getJson('/'+plural+'?search='+encodeURIComponent(term)+'&pageSize=20')`, envelope/mapOption reuse, minimumInputLength:0, change.vfpBridge reuse); renderTargetPicker REMOTE_KINDS dalı preload'suz + bindSelect2Remote. **KORU=0** (backend/proxy dokunulmadı; List `?search` zaten mevcut; READERS diğer kind'ler + loadOptions/nameOf + DET-K query + DET-L(diğer kind) + DET-M korundu; vfpRsTargetId/data-role/targetIdValue/showPicked değişmedi).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ E4: contact/account picker'da isim yazınca backend'den gelir (128K/43K tam kapsam). **wwwroot JS → Ctrl+F5 (ama DET-M ile birlikte restart).**

**DET-O KOMPLE (büyük-liste arama server-side).**
```
