# WORK PACKAGE — WP-CT-FE-2 · Editör 3-sütun iskelet + sol panel (kimlik + tip paleti) (frontend, mockup v2)

> **CT (SoR).** MOD-0162 Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup **Ekran 2 — düzen + sol panel**. Mevcut route-based editör (`TemplateCreate`/`TemplateEdit` → `_TemplateForm.cshtml` + `template-form.js`) **2-sütundan (col-lg-8 + yan) 3-sütuna** dönüştürülür; sol panele kimlik **taşınır** + **tip paleti** eklenir. Orta = mevcut branches-builder (FE-3 diyagramı buraya gelecek — placeholder korunur), sağ = boş sekme yer tutucu (FE-5). **Sürükle-bırak = FE-4** (bu WP'de sadece palet markup'ı, drag YOK). **Frontend.** Backend DEĞİŞMEZ (yalnız mevcut nodes read'i ek fetch). BE-A/B + FE-1 üstüne.

## Kanıt
- **`_TemplateForm.cshtml`:** `row g-4 > col-12 col-lg-8` [Identity card + Branches card] + yan sütun (Reference/actions). 3-sütun DEĞİL.
- **`template-form.js`:** `types` yüklü (`typeNameById/typeCodeById/typeNameOnlyById`); `branches=[{name,steps:[{conceptTypeId,min,max}]}]`; **`spineFromBranches()`** omurgayı türetir; steps **tip modeli** (D1=B ✓). **nodes YÜKLENMİYOR** → palet "düğüm sayısı" için additive nodes fetch (console'un kullandığı concept nodes read; subject-scoped).
- **Mockup sol panel:** Kimlik (Konu · Kod-kilit · Ad · Açıklama) · Moderatör (açılır) · Kime (çoklu etiket) · **Tip paleti:** her tip için **düğüm sayısı** · **kaç dalda kullanıldığı** · omurgada değilse **"omurgada yok" rozeti** · (taslakta sürüklenebilir — drag FE-4).

## NE (frontend; backend DEĞİŞMEZ)
1. **3-sütun düzen** (`_TemplateForm.cshtml`): `row` → **sol (col-lg-3) · orta (col-lg-6) · sağ (col-lg-3)** (dar ekranda stack; responsive). Yayınlanmışta salt-okunur bandı yeri (FE-6) için üst şerit alanı bırak.
2. **Sol panel — kimlik (taşı) + tip paleti:**
   - Kimlik alanlarını (Subject/Code-kilit/Name/Description/Moderator/ForWhom) sol sütuna taşı (mevcut input id'leri + select2 + L10n korunur).
   - **Tip paleti** (yeni): seçili konunun concept tiplerini listele; her tip satırı: ad + **düğüm sayısı** (o tipteki node sayısı) + **dal-kullanımı** (kaç dalda o tipte adım var — `branches`'ten) + **omurga-dışı rozeti** (`spineFromBranches()`'te yoksa). `draggable`-hazır markup + handle (drag davranışı FE-4; bu WP'de tıklanınca no-op veya "dala ekle" mevcut compose akışına köprü opsiyonel).
3. **Orta panel:** mevcut **branches-builder** (tplBranches + pager + add-step) buraya taşınır — **işlevsel kalır** (kaydet/submit/spine türetme bozulmaz). FE-3 diyagramı bunun yerine geçecek (şimdilik placeholder olarak builder çalışır).
4. **Sağ panel:** boş **yer tutucu kart** ("Bağlantılar / Uyumsuz / Sürümler — yakında" veya sade boş) — FE-5 sekmeleri buraya gelecek.
5. **nodes fetch (additive):** subject seçilince/yüklenince concept nodes'u çek (console deseni), tip başına sayıyı hesapla. MDM/başka çağrı yok.
6. **L10n (7 dil):** palet etiketleri — `TypePalette`/"Tipler", `NodeCount`/"{0} düğüm", `BranchUsage`/"{0} dalda", `NotOnSpine`/"omurgada yok" + köprü.

## KORU / YAPMA
- **Backend DEĞİŞMEZ** (yalnız mevcut nodes read'i ek fetch). **Branches-builder işlevi (kaydet/submit/spineFromBranches/step add/min-max/pager) korunur** — yalnız orta sütuna taşınır. Step modeli **tip+min/max** (D1=B). Moderator/ForWhom select2 + tüm mevcut input id'leri korunur (submit bozulmaz). **Sürükle-bırak YOK** (FE-4) — palet yalnız görünüm+veri. **Diyagram (FE-3) + sekme içeriği (FE-5) + yayın diyaloğu/salt-okunur (FE-6) bu WP'de YOK** (yer tutucu). Tema/L10n köprüsü. Liste (FE-1)/diğer tab/editör-dışı DOKUNMA.
- **DUR:** nodes read subject-scoped değilse veya editör sayfasında erişilemiyorsa (ekstra endpoint gerek) additive ekle; 3-sütun mevcut branches-builder pager'ını/submit'i bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → yeşil. git diff: `_TemplateForm.cshtml` + `template-form.js` (+ css/resx). **backend/liste/diğer-tab diff YOK.** Mevcut kaydet/submit/spine testleri (varsa) korunur.
- **E4 (FLEET RESTART):** editör 3-sütun; sol = kimlik + tip paleti (düğüm sayısı · dal-kullanımı · omurga-dışı rozeti); orta = çalışan branches-builder (kaydet çalışır); sağ = yer tutucu. TUTUKON konusunda paletteki tipler doğru sayılarla görünür.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-2 · Editör 3-sütun iskelet + sol panel (kimlik + tip paleti) (MOD-0162, frontend, mockup v2)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Amaç: Zincir Şablonu v2 editörünü (TemplateCreate/Edit → _TemplateForm.cshtml + template-form.js) 2-sütundan 3-sütuna çevir; sol panele kimlik taşı + tip paleti ekle. Orta = mevcut branches-builder (işlevsel placeholder, FE-3 diyagramı sonra), sağ = boş sekme yer tutucu (FE-5). Sürükle-bırak YOK (FE-4). Backend DEĞİŞMEZ (yalnız mevcut nodes read ek fetch).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-2-editor-skeleton-type-palette.md · frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml (row/col-lg-8 + Identity/Branches kartları) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (types yüklü, branches steps={conceptTypeId,min,max}, spineFromBranches, moderator/forwhom, submit) · concept-slim.js (nodes read deseni — subject nodes fetch) · _TemplateFormL10n.cshtml + KnowledgeConceptsIndex resx (7 dil) · memory l10n-bridge-pascalcase-loader.

NE (frontend; backend DEĞİŞMEZ):
 1) 3-sütun: sol col-lg-3 · orta col-lg-6 · sağ col-lg-3 (responsive stack); üstte salt-okunur bandı yeri (FE-6) bırak.
 2) Sol: kimlik alanlarını (Subject/Code-kilit/Name/Description/Moderator/ForWhom — mevcut input id + select2 + L10n KORU) sola taşı + tip paleti: her tip → ad + düğüm sayısı (nodes'tan) + dal-kullanımı (branches'ten) + omurga-dışı rozeti (spineFromBranches'te yoksa); draggable-hazır markup (drag FE-4, şimdilik no-op).
 3) Orta: mevcut branches-builder (tplBranches+pager+add-step) buraya taşı, işlevsel kalsın (kaydet/submit/spine bozulmaz).
 4) Sağ: boş yer tutucu kart (FE-5 sekmeleri gelecek).
 5) nodes additive fetch (subject-scoped, console deseni) → tip başına sayı.
 6) L10n 7 dil: TypePalette/NodeCount/BranchUsage/NotOnSpine + köprü.
KORU/YAPMA: backend DEĞİŞMEZ (yalnız nodes read); branches-builder işlevi (kaydet/submit/spineFromBranches/add-step/min-max/pager) korunur, yalnız taşınır; step modeli tip+min/max; moderator/forwhom+tüm input id korunur (submit bozulmaz); sürükle-bırak YOK (FE-4); diyagram(FE-3)/sekme-içeriği(FE-5)/yayın-diyaloğu(FE-6) YOK (placeholder); tema/L10n köprüsü; liste/diğer-tab DOKUNMA.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → yeşil; git diff _TemplateForm.cshtml + template-form.js (+css/resx); backend/liste/diğer-tab diff yok. Ayrı commit ("feat(crm): WP-CT-FE-2 — chain template editor 3-column skeleton + identity + type palette (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: nodes read editörde erişilemiyorsa additive ekle; 3-sütun mevcut branches-builder/submit'i bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-24) → **ACCEPTED (E2)**
```
Commit: 264f9bbf · Agent: PASS (229/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe2 @264f9bbf → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (10 dosya, +149/−8):** 7 resx (+6 anahtar) · _TemplateForm.cshtml (+38) · _TemplateFormL10n (+4) · template-form.js (+73). **backend/liste(FE-1)/diğer-tab diff YOK.** Frontend-only (nodes = mevcut `/concept-nodes?subjectId=` proxy, yeni endpoint yok).
- ✅ **3-sütun:** sol col-lg-3 (kimlik+palet) · orta col-lg-6 (branches-builder taşındı) · sağ col-lg-3 (FE-5 yer tutucu); üstte `tplReadOnlyBand` şeridi (FE-6). Responsive stack.
- ✅ **Submit sağlam:** 8 identity input id (tplSubjectId/Code/Name/Description/Moderator/ForWhom/OrderedConceptTypes/Branches) korundu; spineFromBranches/submit/branches çekirdeği silinmedi (yalnız types= satırı nodes için düzenlendi). Agent stub-run ile add-step + PUT payload (branches/min-max/spine/moderator/forwhom) sağlam doğruladı.
- ✅ **Tip paleti (kod okundu):** tip → ad + düğüm sayısı (arşivsiz; yüklenemezse **"—" fake 0 değil**) + dal-kullanımı + omurga-dışı rozeti; subject-scoped tek fetch (double-fire de-dupe); draggable-hazır hook'lar (js-palette-type/handle), **drag no-op (FE-4)**.
- ✅ **L10n 7 dil:** 6 anahtar (TypePalette/NodeCount/BranchUsage/NotOnSpine/TypePaletteEmpty/SidePanelComingSoon) + köprü. **Build+test:** Diten.Web.Tests **229/0**.
- ⏳ **Sapma (kabul):** Reference kartı (sürüm/durum/geçerlilik) silinmedi → sağ sütuna parkedildi; nihai yeri **FE-5/FE-6**'da kararlaşacak.

**WP-CT-FE-2 KOMPLE (E2).** E4 = fleet restart. Sıradaki: FE-3 (tip-şerit diyagramı — orta panel builder'ı diyagrama çevirir, en zor).

