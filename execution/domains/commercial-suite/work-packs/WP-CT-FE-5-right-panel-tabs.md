# WORK PACKAGE — WP-CT-FE-5 · Sağ panel 3 sekme: Bağlantılar / Uyumsuz / Sürümler (frontend, mockup v2)

> **CT (SoR).** MOD-0162 Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup **Ekran 2 — sağ panel**. FE-2'nin sağ-panel yer tutucusunu 3 sekmeyle doldurur; **BE-A diagnostics** + **BE-B resolve** endpoint'lerini tüketen ilk FE. **Frontend** (template-form.js + _TemplateForm.cshtml + **KnowledgeConceptsController proxy 2 additive route** + resx). Backend (CrmService) DEĞİŞMEZ. FE-4 üstüne.

## Kanıt
- **Sağ panel** (`_TemplateForm.cshtml` col-lg-3, ~98-117): `SidePanelComingSoon` placeholder kartı + Reference kartı (tplChainVersion/tplStatus/geçerlilik). FE-5 placeholder → 3 sekme; Reference alanları **korunur** (kaydet için gerekli — sekme altına/içine).
- **Frontend proxy** (`KnowledgeConceptsController.cs`): concept-chain-templates GET/POST/PUT var; **conformance-diagnostics (BE-A) + conformance-resolutions (BE-B) proxy YOK** → additive 2 route.
- **Backend endpoint'ler (mevcut):** BE-A `POST /api/crm/knowledge/concept-chain-templates/conformance-diagnostics` {subjectId, orderedConceptTypeIds[]} → {relationshipId, from/toNodeId+Name, from/toTypeId, relationshipType, result(conforming/order/out), missingTypeIds[]} + sayılar. BE-B `PUT /api/crm/knowledge/concept-chain-templates/{id}/conformance-resolutions` {ignoredRelationshipIds[]} → draft yazar, published→409. Template DTO **ignoredNonConformingRelationshipIds** taşır.
- **template-form.js:** `base='/CRM/KnowledgeConcepts/api'`; template `getJson('/concept-chain-templates/{editId}')` ile yüklenir (editId=saved draft); `spineFromBranches()` anlık omurga; types/nodes/branches/moderator/forwhom state mevcut.
- **Mockup sağ panel:** Bağlantılar (girdi/çıktı + akış şeridi) · Uyumsuz (diagnostics kartları + Dal-ekle/Yok-say/Geri-al) · Sürümler (zaman çizelgesi).

## NE (frontend; CrmService DEĞİŞMEZ)
1. **Proxy (KnowledgeConceptsController, additive 2 route):**
   - `POST api/concept-chain-templates/conformance-diagnostics` → gateway aynı yol (ReadPermission).
   - `PUT api/concept-chain-templates/{id}/conformance-resolutions` → gateway (TemplateManagePermission).
2. **Sağ panel → 3 sekme** (nav-pills/tab, mevcut placeholder yerine):
   - **Bağlantılar:** Girdiler = Konu · tipler ("N / M omurgada") · düğümler (referans sayı) · ilişkiler (toplam + kaç uyumsuz — diagnostics'ten) · hedef kitle · moderatör. Çıktılar = bilgi yolları / journey / ziyaret (**kaynak yoksa "—"; uydurma sayı YOK**). Alt: tek-şerit veri akışı (Konu→Kavram grafiği→Zincir şablonu→Bilgi yolu→Yolculuk→Ziyaret).
   - **Uyumsuz:** diagnostics'i çağır (subjectId + `spineFromBranches()`; omurga değişince/debounce yenile). `order`/`out` kartları: kaynak→ilişki tipi→hedef · neden ("Tip omurgada yok" / "Sıra ters") · missing tipler (out). **Aksiyonlar (yalnız taslak):** "[Tip] · Dal X'e ekle" (yalnız `out`; eksik tipi seçili dala `{min:1,max:1}` ekle → renderBranches + omurga yeniden + diagnostics yeniden) · "Yok say" (ignored'a ekle → **saved draft ise resolve endpoint**, yeni template ise in-memory liste [Create payload'a girer] → published→409) · "Geri al" (ignored'dan çıkar). **İgnore'lar sayaç + omurga-dışı özetinden düşer.** Rozet = çözülmemiş sayısı. İlişki **silinmez** (D8).
   - **Sürümler:** aynı subject+chainCode template'lerini çek (mevcut GET `concept-chain-templates?subjectId=` + client-side chainCode filtre) → zaman çizelgesi (durum/tarih/onaylayan/o sürümün omurgası).
3. **Omurga-dışı özeti (FE-3 ertelenen):** diyagramın sağındaki "omurga dışı" özeti diagnostics `out` tiplerinden (ignored hariç) doldur → tıkla → Uyumsuz sekmesi. (FE-3 diyagram host'una additive.)
4. **Reference alanları** (version/status/dates): korunur (sekme altında veya "Sürümler" yakınında); save akışı bozulmaz.
5. **L10n (7 dil):** sekme başlıkları (Connections/NonConforming/Versions) + girdi/çıktı etiketleri + neden (`ReasonNotOnSpine`/`ReasonWrongOrder`) + aksiyonlar (`AddToBranch`/`Ignore`/`Undo`) + akış şeridi + köprü.

## KORU / YAPMA
- **Backend (CrmService) DEĞİŞMEZ** — yalnız frontend proxy 2 additive route + JS/görünüm. Diyagram (FE-3) render mantığı + drag (FE-4) + veri modeli + submit + spineFromBranches DEĞİŞMEZ (Uyumsuz "Dal ekle" yalnız `branches[].steps` mutasyonu + renderBranches; diagnostics salt-okuma). **Conformance enforce YOK (D8)** — ilişki silinmez, yalnız ignore kaydı. Aksiyonlar **yalnız taslak** (published→salt-okunur; resolve published→409). Çıktı sayıları için **uydurma YOK** (kaynak yoksa "—"). Yayın diyaloğu/salt-okunur bandı = FE-6 (DOKUNMA). Liste(FE-1)/diğer-tab DOKUNMA. Tema/L10n köprüsü.
- **DUR:** çıktı (bilgi-yolu/journey/ziyaret) sayısı için reverse-index/endpoint yoksa "—" göster (uydurma değil) + not; yeni (kaydedilmemiş) template'te resolve endpoint çağrılamıyorsa in-memory→Create payload yolu (BE-B Create ignored taşır) + raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → yeşil. git diff: template-form.js + _TemplateForm.cshtml + KnowledgeConceptsController.cs (+2 proxy route) + resx. **CrmService/backend diff YOK.**
- **E4 (FLEET RESTART + canlı):** editör sağ panelde 3 sekme; **Uyumsuz** = TUTUKON ilişkilerini diagnostics'ten out/order sınıflar (addresses reversal doğru); "Dal ekle" out tipi omurgaya sokar → uyumlu olur; "Yok say" kalıcı (published→409); Sürümler zaman çizelgesi; Bağlantılar girdi/çıktı+akış. Uçtan-uca BE-A/BE-B doğrulanır.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-5 · Sağ panel 3 sekme (Bağlantılar/Uyumsuz/Sürümler) — BE-A diagnostics + BE-B resolve tüketen (MOD-0162, frontend, mockup v2)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Amaç: FE-2 sağ-panel placeholder'ını 3 sekmeyle doldur; BE-A diagnostics + BE-B resolve endpoint'lerini tüket. Frontend (template-form.js + _TemplateForm.cshtml + KnowledgeConceptsController 2 additive proxy route + resx). CrmService DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-5-right-panel-tabs.md · frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml (sağ panel col-lg-3 ~98-117 placeholder+Reference) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (base, getJson, spineFromBranches, renderBranches, state types/nodes/branches/moderator/forwhom, editId) · frontend/Diten.Web/Controllers/CRM/KnowledgeConceptsController.cs (concept-chain-templates GET/POST/PUT ~238-275, proxy deseni) · _TemplateFormL10n + resx (7 dil) · services/.../Concept/ChainTemplate/ChainTemplateConformanceDiagnostics.cs (BE-A yanıt şekli — SALT referans) · memory l10n-bridge-pascalcase-loader.

NE (frontend; CrmService DEĞİŞMEZ):
 1) KnowledgeConceptsController additive: POST api/concept-chain-templates/conformance-diagnostics → gateway (ReadPermission); PUT api/concept-chain-templates/{id}/conformance-resolutions → gateway (TemplateManagePermission).
 2) Sağ panel 3 sekme (placeholder yerine):
    - Bağlantılar: girdiler (Konu/tipler N-of-M omurgada/düğüm sayı/ilişki toplam+uyumsuz/kitle/moderatör) + çıktılar (bilgi-yolu/journey/ziyaret — kaynak yoksa "—", uydurma YOK) + alt akış şeridi.
    - Uyumsuz: diagnostics çağır (subjectId+spineFromBranches; debounce/omurga-değişince yenile); order/out kartları (kaynak→tip→hedef, neden, missing); aksiyon (yalnız taslak): "Dal X'e ekle" (out; eksik tipi {min1,max1} dala ekle→renderBranches+diagnostics yeniden), "Yok say" (ignored'a; saved draft→resolve endpoint, yeni→in-memory Create payload; published→409), "Geri al". İgnore sayaç+omurga-dışından düşer; ilişki SİLİNMEZ (D8); rozet=çözülmemiş.
    - Sürümler: GET concept-chain-templates?subjectId= + client-side chainCode filtre → zaman çizelgesi (durum/tarih/onaylayan/omurga).
 3) Omurga-dışı özeti (FE-3 ertelenen): diyagram sağında diagnostics out tiplerinden (ignored hariç) → tıkla Uyumsuz sekmesi.
 4) Reference (version/status/dates) korunur (save bozulmaz).
 5) L10n 7 dil: sekme başlıkları + girdi/çıktı + ReasonNotOnSpine/ReasonWrongOrder + AddToBranch/Ignore/Undo + akış + köprü.
KORU/YAPMA: CrmService backend DEĞİŞMEZ (yalnız frontend proxy 2 route+JS/görünüm); diyagram(FE-3)/drag(FE-4)/veri modeli/submit/spineFromBranches DEĞİŞMEZ (Dal-ekle yalnız branches[].steps+renderBranches; diagnostics salt-oku); conformance enforce YOK (D8, ilişki silinmez); aksiyon yalnız taslak (resolve published→409); çıktı uydurma YOK ("—"); yayın-diyaloğu/salt-okunur=FE-6 DOKUNMA; liste/diğer-tab DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → yeşil; git diff template-form.js+_TemplateForm.cshtml+KnowledgeConceptsController.cs+resx; CrmService/backend diff yok. Ayrı commit ("feat(crm): WP-CT-FE-5 — chain template right panel tabs (connections/non-conforming diagnostics+resolve/versions) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: çıktı sayısı için endpoint yoksa "—"+not; yeni template'te resolve çağrılamıyorsa in-memory→Create payload+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-25) → **ACCEPTED (E2)**
```
Commit: 8c713a50 · Agent: PASS (229/0, TUTUKON-benzeri uçtan-uca tarayıcı sim) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe5 @8c713a50 → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (11 dosya, +514/−9):** KnowledgeConceptsController (+2 proxy route) · 7 resx (+33 anahtar) · _TemplateForm · _TemplateFormL10n · template-form.js (+243). **CrmService/backend diff YOK.**
- ✅ **Proxy (kod okundu):** `POST …/conformance-diagnostics` (ReadPermission) · `PUT …/{id}/conformance-resolutions` (TemplateManage→Manage fallback) → gateway aynı yol.
- ✅ **Wiring:** `ignoredIds` Set — saved draft→resolve endpoint (tüm set), **published→409 toast**, yeni→in-memory+Create payload; diagnostics POST {subjectId, spineFromBranches} (debounce+cache); `unresolved()` ignored'ı filtreler; **ilişki silinmez (D8)**; "Dal ekle" out tipini {min:1,max:1} dala ekle→renderBranches+diagnostics yeniden; aksiyonlar yalnız taslak.
- ✅ **3 sekme:** Bağlantılar (girdi N-of-M + ilişki toplam/uyumsuz + kitle/moderatör; çıktı **"—" uydurma yok**) · Uyumsuz (backend sınıflar, reversal doğru) · Sürümler (chainCode zaman çizelgesi). Omurga-dışı özeti diyagram altında → Uyumsuz sekmesi. Reference alanları korundu.
- ✅ **Agent tarayıcı E4:** add-to-branch (addresses reversal→uyumlu, rozet düştü) · saved ignore/undo (`["r3"]`/`[]`, 409 toast) · yeni-template ignore (Create payload `["r3"]`, resolve çağrılmadı) · published read-only + yeni-sürüm `["r3"]` miras · versions filtre. **Build+test:** Diten.Web.Tests **229/0** (+ commit-öncesi `{0}/{1}` placeholder bug'ı düzeltildi).
- ⏳ **Not (DUR):** bilgi-yolu/journey/ziyaret çıktı sayısı = "—" (referans kaynağı yok — uydurma yok); Campaign/ContentSet template'e referans veriyor (istenirse ayrı iş).

**WP-CT-FE-5 KOMPLE (E2).** E4 = fleet restart + gerçek TUTUKON ilişkileriyle diagnostics/ignore (BE-A/BE-B uçtan-uca). Sıradaki: FE-6 (yayın diyaloğu + salt-okunur görünüm).

