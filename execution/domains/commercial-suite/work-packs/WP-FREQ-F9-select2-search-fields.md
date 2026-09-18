# WORK PACKAGE — WP-FREQ-F9 · Editör select'leri select2-arama'lı (task-create) + "Hangi kayıt" bg-body (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F8 landing sonrası HEAD üstü** — F8 ile aynı dosyalara dokunur, SIRALI). Kullanıcı: editör HEDEF + kapsam select'lerinin **hepsi** task-create field'ları gibi **select2 arama'lı** olsun; "Hangi kayıt" kutusu arka planı `bg-body`. **Frontend only** (_Editor.cshtml + form.js + visit-frequency-create.css). buildPayload/id/cascade KORUNUR.

## İstek (kullanıcı)
1. **"Hangi kayıt" kutusu arka planı** → `bg-body` (`--bs-body-bg`); şu an `--bs-tertiary-bg`.
2. **Tüm select'ler select2 arama'lı** (task-create "Görev türü" deseni: `select2 form-select` + arama kutusu):
   - Hedef seçimi (target picker select, her targetType için dinamik).
   - Kapsam select'leri: İş birimi, Saha alanı (model + node), Segment, Kampanya, Marka, Ürün, Cycle, Cycle dönemi.
3. Field'lar task-create görünümünde (diten-field/select2 sarmalı — Görev türü gibi).

## Kritik teknik notlar (select2 + dinamik + cascade)
- Select'ler form.js tarafından **dinamik** doldurulur (fetch/contract). select2 **options yüklendikten SONRA** init edilir; cascade'de (model→node, brand→product) select yeniden doldurulunca select2 **destroy + re-init** (yoksa eski options kalır).
- form.js `.value` okur ve `change` dinler → select2 `change` tetikler; **buildPayload/currentTargetId/cascade davranışı BİREBİR korunur**. data-role="targetId" select2 sonrası da okunabilir olmalı.
- select2 app-genel yüklü (Task/Create kullanıyor). dropdownParent/tema uyumu (mevcut EligibilityPolicies/DataTable select2 desenleri referans).
- Arama gerekmeyen kısa listelerde bile arama açılabilir (kullanıcı hepsini istedi); çok kısa olanlarda `minimumResultsForSearch` opsiyonel.

## Kapsam
- `_Editor.cshtml`: "Hangi kayıt" kutusu bg → `bg-body`/`--bs-body-bg`. Select'lere `select2` sınıfı/işareti (veya form.js init hedefi).
- `form.js`: target picker + scope select'lerini populate sonrası select2 init; cascade'de re-init; change/value/buildPayload/cascade KORUNUR. "Değiştir…" akışı korunur.
- `visit-frequency-create.css`: select2 + scoped tema uyumları (gerekirse); "Hangi kayıt" bg-body.

## KORU / YAPMA
- buildPayload/id/data-role/cascade/validation DEĞİŞMEZ (select2 yalnız sunum + arama; değer/olay aynı). Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA. Hardcode vocabulary yok. Tema-duyarlı (light+dark, select2 dahil). Yalnız _Editor + form.js + css (+resx gerekmez muhtemelen).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + visit-frequency-create.css. Backend/liste/detay/diğer bölüm diff YOK. buildPayload/cascade korunmuş.
- **E4:** HEDEF + kapsam select'leri select2 arama'lı (task-create gibi); "Hangi kayıt" bg-body; cascade (model→node, brand→product) + kaydetme + Değiştir işlevi aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F8 landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F9 · Editör select'leri select2-arama'lı + "Hangi kayıt" bg-body (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F8 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F9-select2-search-fields.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (HEDEF picker + #vfpScopeBody select'leri) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (renderTargetPicker/populate/cascadeNodes/brand-product/showPicked) · frontend/Diten.Web/Views/Tasks/Create.cshtml + ilgili js (select2 "Görev türü" init deseni) · EligibilityPolicies/DataTable select2 tema/dropdownParent deseni.

NE (frontend; buildPayload/id/data-role/cascade KORUNUR):
 1) "Hangi kayıt" kutusu bg → bg-body (--bs-body-bg).
 2) Tüm select'ler select2 arama'lı (task-create Görev türü gibi): Hedef seçimi (dinamik per targetType) + İş birimi/Saha model+node/Segment/Kampanya/Marka/Ürün/Cycle/Cycle dönemi. select2 options YÜKLENDİKTEN sonra init; cascade'de (model→node, brand→product) destroy+re-init; change/.value/buildPayload/currentTargetId/data-role BİREBİR korunur. dropdownParent+tema uyumu.
 3) Field'lar task-create görünümü (select2 form-select).
KORU/YAPMA: buildPayload/id/data-role/cascade/validation DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; hardcode vocabulary yok; tema-duyarlı (select2 light+dark); yalnız _Editor+form.js+css.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css; backend/liste/detay/diğer bölüm diff yok; buildPayload/cascade/data-role korunmuş. Ayrı commit. §22 TÜRKÇE. K13.
Durma: select2 cascade/buildPayload/data-role'ü bozuyorsa; init options sonrası çalışmıyorsa; kapsam editör dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 31958ba4 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-freqf9-verify @31958ba4
```
- ✅ **Kapsam:** _Editor.cshtml + form.js + visit-frequency-create.css (3 dosya). backend/liste/detay/çözümleme/resolve.js/index.js/Segment = 0.
- ✅ **Payload korundu:** `data-role="targetId"` 10× duruyor; buildPayload alan satırları silinmemiş (grep 0). Native-change köprüsü (`change.vfpBridge` + originalEvent guard) select2→native change yeniden dağıtıyor → cascade (model→node, brand→product) + showPicked + currentTargetId aynen çalışır; options sonrası init + cascade'de destroy/re-init (`rebindSelect2`).
- ✅ **bg-body + select2:** "Hangi kayıt" `.vfp-picked-box` → --bs-body-bg; HEDEF + kapsam select'leri select2 arama'lı (task-create); dropdownParent sarmalayıcı; tema-duyarlı.
- ✅ **Build+test (CT izole, Release, temiz):** Diten.Web.Tests **137/0**.
- ⚠️ **E4 önemli:** select2 change-köprüsü + cascade + Değiştir + kaydet tarayıcıda bizzat test edilmeli (kod doğru; runtime davranışı kritik).

**FREQ-F9 KOMPLE.**
