# WORK PACKAGE — WP-CT-FE-3-REFINE · Dal başlığı (DAL-X) + boş-dal drag-dropzone (mockup fidelity) (frontend)

> **CT (SoR).** MOD-0162 Chain Template v2. Branch `feature/crm-chain-template` (FE-6 `abdaa1f1` üstü). **Kullanıcı E4:** diyagramdaki **dal-ekleme/başlık alanı mockup'la örtüşmüyor** (2 ekran görüntüsü karşılaştırıldı). Mevcut: büyük "Dal adı" input + "0 adım" rozet + boş-dalda compose dropdown satırı ("Seçiniz/En az/En çok/Adım ekle"). Mockup: **"DAL A · Ana akış · 2 adım"** hafif başlık + boş-dalda **"Kavram tipi sürükleyin" dropzone**. **Karar B:** dropzone görünümü + **erişilebilir kompakt "+tip ekle"** (klavye/sürükleyemeyen için; pure-drag DEĞİL). **Frontend** (yalnız lane-header + boş-dal sunumu). Veri modeli/submit/diyagram/drag(FE-4)/min-max DEĞİŞMEZ.

## Kanıt
- **`template-form.js` renderBranches lane** (~312): `<div class="js-lane"><input class="js-branch-name" placeholder="Dal adı">` (büyük input) + "N adım" + `js-branch-remove` + `js-lane-body` (grid, FE-4 drop target) + sticky `addStepRow` (compose: `js-branch-type-picker`+min/max+`js-branch-add-step`).
- **Mockup (image 2):** başlık = "**DAL A**" prefix (sabit etiket) + dal adı hafif inline + "2 adım" + sil. Boş dal (DAL B) = tek **dropzone** kutusu "Kavram tipi sürükleyin" (compose dropdown YOK).
- **FE-4 drop:** `js-lane-body` zaten drop hedefi (paletten tip → dala). → dropzone bu drop'u görselleştirir.

## NE (frontend; veri/submit/drag/diyagram DEĞİŞMEZ)
1. **Lane başlığı (DAL-X stili):** "Dal adı" büyük input → **"DAL {A/B/C…}"** sabit prefix etiketi + **inline-editable hafif ad** (mevcut `js-branch-name` id KORU, ama görsel: küçük/plain, "Ana akış" gibi) + "**{N} adım**" + `js-branch-remove` (≥1 kalır). Harf: dal index → A/B/C (0→A). Yayınlanmışta salt-okunur (mevcut ro).
2. **Boş dal (steps.length==0):** compose satırını **öne çıkarma**; yerine mockup **dropzone**: `js-lane-body` içinde "**Kavram tipi sürükleyin**" placeholder kutusu (kesikli çerçeve, drag hedefi — FE-4 drop zaten çalışıyor). + **kompakt erişilebilir yol (B):** küçük "**+ tip ekle**" link/buton → tıklayınca mevcut compose picker'ı açar/gösterir (klavye yolu korunur). *(compose'u SİLME — gizle/kompaktla; add-step mantığı aynı.)*
3. **Dolu dal:** kartlar + kenarlar (FE-3) aynen; compose add-row **kompakt** "+tip ekle" affordance'ına indir (veya altta ince). Çip (×1/×1-2, tıkla→min/max) FE-3'teki gibi.
4. **L10n (7 dil):** `BranchLabelPrefix`/"DAL", `StepCountLabel`/"{0} adım", `DragTypeHerePlaceholder`/"Kavram tipi sürükleyin", `AddTypeCompact`/"+ tip ekle" + köprü. Mevcut anahtarlar reuse.

## KORU / YAPMA
- **Veri modeli (branches steps={conceptTypeId,min,max}) + submit + spineFromBranches + tplOrderedConceptTypes + diyagram render (FE-3 grid/kenar) + drag (FE-4 moveTo/js-lane-body drop) + min/max (js-step-min/max) + ←/→ + identity/palet/sağ-sekme (FE-5) + yayın/read-only (FE-6) DEĞİŞMEZ.** Yalnız **lane-header markup + boş-dal sunumu** (+ compose'u kompaktla, silme). `js-branch-name`/`js-branch-remove`/`js-branch-add-step`/`js-branch-type-picker` id/class KORU (handler'lar bozulmasın). **Erişilebilirlik:** sürükleyemeyen kullanıcı için "+tip ekle" (klavye) yolu ZORUNLU (pure-drag YAPMA). Backend/liste/diğer-tab DOKUNMA. Tema/L10n köprüsü.
- **DUR:** compose'u kompaktlamak add-step handler'ını (js-branch-add-step/js-branch-type-picker) bozuyorsa → görünür bırak+raporla; DAL-X harf üretimi çok dalda (>26) taşıyorsa sayı fallback.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → 229/0. git diff: `template-form.js` (+ css/resx). **backend/liste/diğer-tab/submit/diyagram-veri diff YOK.** Add-step/drag/min-max/save akışı korunur.
- **E4 (FLEET RESTART + görsel):** dal başlığı "DAL A · {ad} · N adım" (mockup gibi); boş dal "Kavram tipi sürükleyin" dropzone + "+tip ekle" (klavye); paletten sürükle → dropzone'a düşer; "+tip ekle" ile klavyeden de eklenebilir; dolu dal kartları/kenarları aynı; kaydet bozulmaz.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-3-REFINE · Dal başlığı DAL-X + boş-dal drag-dropzone (mockup fidelity) (MOD-0162, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Kullanıcı E4: diyagram dal-başlığı/boş-dal-ekleme mockup'la örtüşmüyor. Mevcut: büyük "Dal adı" input + "0 adım" + compose dropdown satırı. Mockup: "DAL A · ad · N adım" hafif başlık + boş-dalda "Kavram tipi sürükleyin" dropzone. Karar B: dropzone + erişilebilir kompakt "+tip ekle" (pure-drag DEĞİL). Yalnız lane-header + boş-dal sunumu; veri/submit/diyagram/drag DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-3-REFINE-lane-header-empty-dropzone.md · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (renderBranches lane ~312: js-lane/js-branch-name/js-lane-body/addStepRow/js-branch-add-step/js-branch-type-picker; FE-4 drop; FE-3 grid/edges) · _TemplateFormL10n + resx (7 dil) · memory l10n-bridge-pascalcase-loader.

NE (frontend; veri/submit/drag/diyagram DEĞİŞMEZ):
 1) Lane başlığı: "Dal adı" büyük input → "DAL {A/B/C}" prefix + inline hafif ad (js-branch-name id KORU) + "{N} adım" + js-branch-remove(≥1). Dal index→harf (0→A; >26 sayı fallback). Yayınlanmışta ro.
 2) Boş dal (steps.length==0): compose'u öne çıkarma → js-lane-body içinde "Kavram tipi sürükleyin" dropzone (kesikli, FE-4 drop zaten çalışır) + kompakt "+ tip ekle" (tıkla→mevcut compose picker; klavye yolu). compose SİLME, gizle/kompaktla.
 3) Dolu dal: FE-3 kartlar/kenarlar aynı; compose→kompakt "+tip ekle".
 4) L10n 7 dil: BranchLabelPrefix/StepCountLabel/DragTypeHerePlaceholder/AddTypeCompact + köprü.
KORU/YAPMA: veri modeli+submit+spineFromBranches+tplOrderedConceptTypes+diyagram(FE-3)+drag(FE-4 js-lane-body drop)+min/max+←/→+identity/palet/sağ-sekme(FE-5)+yayın/read-only(FE-6) DEĞİŞMEZ; yalnız lane-header+boş-dal; js-branch-name/remove/add-step/type-picker id-class KORU; erişilebilir "+tip ekle" (klavye) ZORUNLU (pure-drag YAPMA); backend/liste/diğer-tab DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 229/0; git diff template-form.js(+css/resx); backend/liste/diğer-tab/submit/diyagram-veri diff yok; add-step/drag/min-max/save korunur. Ayrı commit ("feat(crm): WP-CT-FE-3-REFINE — chain template lane header (DAL-X) + empty-branch drag dropzone (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: compose kompaktlama add-step handler'ını bozuyorsa görünür bırak+raporla; DAL harf >26 taşıyorsa sayı fallback.
```

## §37 CT bağımsız doğrulama (2026-09-25) → **ACCEPTED (E2)**
```
Commit: 170b21fb · Agent: PASS (229/0, tarayıcı görsel+davranış) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe3r @170b21fb → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (9 dosya, +66/−8):** 7 resx (+4 anahtar) · _TemplateFormL10n · template-form.js (+42). **backend/liste/diğer-tab diff YOK.**
- ✅ **Handler id'leri korundu:** `js-branch-name` (3) · `js-branch-add-step` (2) · `js-branch-type-picker` (3) hâlâ mevcut → submit/add-step bozulmadı (compose SİLİNMEDİ, collapse'e alındı).
- ✅ **Lane header (kod okundu):** "Dal adı" büyük input → **DAL {A/B/C…}** badge (`branchLetter`: <26 harf, ≥26 sayı fallback) + inline hafif ad (js-branch-name KORU) + "{N} adım" + `js-branch-remove` + kompakt `js-compose-toggle` "+tip ekle".
- ✅ **Boş dal → dropzone:** `js-lane-dropzone` (kesikli, grid-column 1/-1, FE-4 drop target görünür) "Kavram tipi sürükleyin". **Erişilebilir (B):** "+tip ekle" collapse → addStepRow (compose picker); açılınca odak picker'a; open-state re-render'da korunur (klavye yolu). ro'da hepsi kapalı.
- ✅ **Agent tarayıcı E4:** DAL-A header + boş DAL-B dropzone mockup'la örtüşüyor; klavye yolu (compose açıl→odak→ekle); dropzone drop+vurgu; dal-sil harf yeniden hesap; >26 "Dal 27"; kaydet 3 yol (compose/dropzone/inline-ad) doğru; read-only kapalı. **Build+test:** 229/0.

**WP-CT-FE-3-REFINE KOMPLE (E2).** Kullanıcının E4 fidelity farkı kapandı. Chain Template v2 kalan: **FE-7** (L10n süpürme + save nihai) + canlı E4 + push/PR.

