# WORK PACKAGE — WP-SEG-E · Entity-picker etiketi (reads-as/dropdown GUID → isim) (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`d63befa5` üstü). **Yalnız frontend (`form.js`).** Owner: reads-as ve dropdown'da territory node / global product GUID görünüyor ("Node is 19fb1045-…"). Node **çalışıyor** (doğru match sayısı) — yalnız **görünüm** bozuk.

## Kök neden (CT ölçümü)
- `conditionText` (form.js:703-705): reads-as cümlesi `condition.values`'ı ham basıyor. Reference-set/enum chip'lerde value = **isim** (Nephrology, sorun yok); **entity-picker'da** (territory-node/model, account, global-product, mdm-product/brand) value = **GUID**, isim (label) ayrı ve **saklanmıyor** → reads-as GUID.
- `loadEntityOptions` (168-198): option `text` mapping `x.name || x.text || … || x.nodeName || … || x.id` — **PascalCase yok** (`x.Name`/`x.NodeName`). Endpoint PascalCase döndürürse text=id (GUID) fallback → dropdown da GUID.

## Kapsam (yalnız form.js — payload DEĞİŞMEZ)
1. **Option text PascalCase fallback:** `loadEntityOptions` text mapping'e `x.Name`, `x.NodeName`, `x.AccountName`, `x.ModelName`, `x.TerritoryName`, `x.GlobalProductName`, `x.ProductName`, `x.BrandName` (PascalCase eşleri) ekle. Böylece dropdown gerçek isim gösterir.
2. **Seçili değer etiketini sakla:** entity-picker seçilince UI-state olarak `{value → label}` sakla (ör. `condition.valueLabels` map veya benzeri) — option listesinden seçilen text. **buildNodes payload'a GİRMEZ** (yalnız value/GUID gönderilir; label salt görünüm).
3. **reads-as label kullansın:** `conditionText` entity-picker value'ları için `valueLabels[value]` (varsa) göstersin; yoksa value. Reference-set/enum aynen (value=text).
4. **Restore (edit):** kayıtlı segment açılınca entity-picker seçili GUID'in label'ı option listesinden (loadEntityOptions cache) çözülüp gösterilsin; henüz yüklenmemişse yükleyip eşle.

## KORU / YAPMA
- **buildNodes blok→tree payload byte-identical** (value=GUID gönderilir, label UI-only — payload'a etiket sızmasın). Catalog-driven optgroup/operatör, reference-set chip davranışı, canlı reach (SEG-C), same-origin proxy, SEG-D sample/meta DEĞİŞMEZ. Backend/DTO DOKUNMA. Yeni CSS yok. Başka modül. Yalnız entity-picker etiket görünümü.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Territory node/model, account, global-product seçilince reads-as + dropdown **isim** gösterir (GUID değil); reference-set chip aynen. buildNodes payload byte-identical (git diff: value gönderimi değişmedi, valueLabels payload'a girmedi). Edit'te seçili picker label restore olur.
- **E4:** "Specialty is any of Nephrology AND ALSO Node is TR — Marmara" (GUID yok).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-E · Entity-picker etiketi (reads-as/dropdown GUID→isim) (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (d63befa5 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-E-frontend-picker-label.md · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (loadEntityOptions 168-198, conditionText 703-705, entity-picker value-source + render + hydrateControls).

NE (yalnız form.js — payload DEĞİŞMEZ):
 1) loadEntityOptions text mapping'e PascalCase fallback ekle (x.Name/x.NodeName/x.AccountName/x.ModelName/x.TerritoryName/x.GlobalProductName/x.ProductName/x.BrandName).
 2) Entity-picker seçilince {value→label} UI-state sakla (condition.valueLabels vb.); buildNodes payload'a GİRMEZ.
 3) conditionText (reads-as): entity-picker value'ları için valueLabels[value] (varsa) göster, yoksa value; reference-set/enum aynen.
 4) Edit restore: kayıtlı picker GUID'in label'ı option cache'ten çözülüp gösterilsin (gerekirse yükle+eşle).
KORU/YAPMA: buildNodes blok→tree payload byte-identical (value=GUID gönderilir, valueLabels payload'a sızmasın); catalog optgroup/operatör, reference-set chip, canlı reach (SEG-C), same-origin proxy, SEG-D sample/meta DEĞİŞMEZ; backend/DTO DOKUNMA; yeni CSS yok; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); territory node/model/account/global-product reads-as+dropdown isim gösterir; reference-set chip aynen; buildNodes payload byte-identical (git diff value gönderimi değişmedi, valueLabels payload'da yok); edit restore label. Ayrı commit. §22 TÜRKÇE. K13.
Durma: label saklamak payload'ı değiştiriyorsa (DUR); entity-picker option kaynağı isim vermiyorsa (DUR+raporla); kapsam form.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 99dd078b · Agent: PASS (--no-build) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-segef-verify @996b277e (SEG-E+F birlikte)
```
- ✅ **Scope:** yalnız form.js (+32/−3). Backend/DTO dokunulmadı.
- ✅ **buildNodes payload byte-identical:** valueLabels buildNodes gövdesinde **0 kez** (grep) — value=GUID gönderimi değişmedi, etiket UI-only.
- ✅ **Fix:** loadEntityOptions text mapping PascalCase fallback (Name/NodeName/…); entity-picker seçilince `condition.valueLabels` map (writeSelectValue option.text); conditionText reads-as valueLabels[value] (yoksa value); reference-set/enum aynen; edit restore hydrateControls'ta option cache'ten label çözer (readback + updateReadback tazeler).
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** reads-as "Node is TR — Marmara" (GUID yok).
