# WORK PACKAGE — WP-CT-FE-4 · Diyagram sürükle-bırak (tip→dal, adım taşı/sırala, çapraz-dal) (frontend, mockup v2)

> **CT (SoR).** MOD-0162 Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup **Ekran 3 (orta) — sürükle-bırak**. FE-3 tip-şerit ızgarasına **custom HTML5 drag** (SortableJS DEĞİL — ızgarada DOM sırası ≠ sütun; auto-sort uymaz). Paletten tip → dala; mevcut adım kartı → başka dala/sıraya; **tip-tekrar guard**; **yalnız taslak**. FE-3'ün ←/→ (js-step-move) + compose add-row **fallback olarak kalır**. **Frontend.** Veri modeli + submit + spineFromBranches **DEĞİŞMEZ** (drag yalnız `branches[].steps` dizilerini değiştirip renderBranches çağırır). FE-3 üstüne.

## Kanıt
- **FE-3 DOM** (`template-form.js`): palet `.js-palette-type` (data-ct, şu an `draggable="false"`) + `.js-palette-handle`; kart `.js-step-card` (data-b/data-s/data-ct, grid-column); dal `.js-lane`/`.js-lane-body` (data-b, grid konteyner); ←/→ `.js-step-move` (data-b/s/delta → splice, satır 560-566); remove `.js-step-remove`; compose `.js-branch-add-step`/`.js-branch-type-picker`. `LANE_COL`(216)/`LANE_GAP`/`LANE_PAD`; renderBranches (288). `templateReadOnly` bayrağı var (published salt-okunur).
- **Mockup drag mantığı:** sürüklenen `{kind:'type',typeId}` (palet) veya `{kind:'step',bid,sid}` (kart). **`moveTo(dal, insertIndex)`:** hedef dalda **aynı tip başka adımda varsa işlem YOK + uyarı** (aynı adımın kendi dalında sıralanması bu kontrole takılmaz). Adım taşınıyorsa önce eski yerinden çıkar; **aynı dalda ileri taşımada hedef −1** (çıkarılan adım kaydırır). Kartın üstüne bırak → önüne; dalın boş alanına → sona. Paletten tip `{min:1,max:1}` ile eklenir. Sürükleme sırasında hedef dal çerçevesi mor. **Yalnız taslak.**

## NE (frontend; veri modeli+submit DEĞİŞMEZ)
1. **Draggable (taslakta):** `.js-palette-type` + `.js-step-card` → `draggable=true` (published/`templateReadOnly` → draggable kapalı). dragstart payload: palet `{kind:'type', typeId}`, kart `{kind:'step', bid, sid}` (dataTransfer + modül state).
2. **Drop hedefleri:** `.js-lane-body` (dal ızgarası) — bir kartın üstüne bırak → **o adımın steps-index'inden önce**; boş alana → **sona**. insertIndex steps DİZİ sırasından hesaplanır (grid görsel-x'ten değil; hedef kartın data-s'i baz).
3. **`moveTo(targetBranch, insertIndex, payload)`:**
   - **Tip-tekrar guard:** hedef dalda payload tipi zaten var (ve taşınan aynı adım değilse) → **işlem yok + toast uyarı** (`TypeAlreadyInBranch`).
   - **kind=type:** yeni `{conceptTypeId, min:1, max:1}` insertIndex'e ekle.
   - **kind=step:** kaynaktan çıkar; **aynı dal + ileri** ise insertIndex−1; hedefe ekle.
   - Sonra `renderBranches()` (grid+spine+kenar yeniden).
4. **Görsel:** dragover'da hedef `.js-lane-body`/`.js-lane` mor vurgu (`.is-drop-target` class; tema-token border); dragleave/drop'ta temizle. Kart adet-alanı açıkken sürükleme engellenebilir (mockup notu — ops.).
5. **Fallback korunur:** ←/→ (js-step-move), compose add-row, remove, branch ops **aynen çalışır**.
6. **L10n (7 dil):** `TypeAlreadyInBranch`/"Bu tip bu dalda zaten var" + köprü.

## KORU / YAPMA
- **Veri modeli (steps={conceptTypeId,min,max}) + submit + spineFromBranches + tplOrderedConceptTypes + identity/palet/moderator/forwhom DEĞİŞMEZ** — drag yalnız `branches[].steps` mutasyonu + renderBranches. **SortableJS KULLANMA** (custom HTML5 DnD; ızgara auto-sort'a uymaz). **Tip-tekrar guard ZORUNLU** (dalda tip tekrar edemez — backend BE-B ile tutarlı). **Yalnız taslak** (templateReadOnly → drag kapalı). ←/→ + compose + remove + branch ops korunur. FE-5 (sekmeler) / FE-6 (yayın-diyaloğu) DOKUNMA. Backend/liste/diğer-tab DOKUNMA. Tema/L10n köprüsü.
- **DUR:** insertIndex'i grid drop pozisyonundan güvenilir hesaplamak mümkün değilse (yalnız kart-üstü/dal-sonu ayrımı yeterli; ince x-sıra gerekmiyor — ←/→ zaten var); çapraz-dal taşımada kaynak/hedef index kayması moveTo'yu bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → yeşil. git diff: `template-form.js` (+ css/resx). **backend/liste/diğer-tab/submit diff YOK.**
- **E4 (FLEET RESTART + görsel):** taslakta paletten tip sürükle → dala düşer (aynı tip varsa uyarı, düşmez); adım kartını başka dala sürükle → taşınır (tip-tekrar guard); kart üstüne bırak → önüne, boşluğa → sona; hedef dal mor vurgulanır; ←/→ hâlâ çalışır; yayınlanmış şablonda sürükleme kapalı; kaydet payload'ı bozulmaz.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-4 · Diyagram sürükle-bırak (custom HTML5 DnD, tip→dal + adım taşı/sırala + çapraz-dal) (MOD-0162, frontend, mockup v2)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Amaç: FE-3 tip-şerit ızgarasına sürükle-bırak. SortableJS DEĞİL — custom HTML5 drag (ızgarada DOM sırası ≠ sütun). Paletten tip → dal; kart → başka dala/sıraya; tip-tekrar guard; yalnız taslak. ←/→ (js-step-move)+compose fallback kalır. Veri modeli+submit+spineFromBranches DEĞİŞMEZ (drag yalnız branches[].steps + renderBranches).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-4-drag-drop.md · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (js-palette-type ~151, js-step-card ~192, js-lane/js-lane-body ~312/319, js-step-move ~181+splice 560-566, renderBranches 288, templateReadOnly, LANE_COL/GAP/PAD) · _TemplateFormL10n + resx (7 dil) · memory l10n-bridge-pascalcase-loader.

NE (frontend; veri modeli+submit DEĞİŞMEZ):
 1) Taslakta .js-palette-type + .js-step-card → draggable=true (templateReadOnly→kapalı). dragstart payload: palet {kind:'type',typeId}, kart {kind:'step',bid,sid}.
 2) Drop hedefi .js-lane-body: kart üstüne bırak → o adımın steps-index'inden önce; boş alana → sona (insertIndex steps DİZİ sırasından, grid-x'ten değil; hedef kart data-s baz).
 3) moveTo(targetBranch,insertIndex,payload): tip-tekrar guard (hedef dalda aynı tip varsa+taşınan aynı adım değilse → işlem yok+toast TypeAlreadyInBranch); kind=type→yeni {conceptTypeId,min:1,max:1} ekle; kind=step→kaynaktan çıkar, aynı-dal+ileri ise insertIndex−1, hedefe ekle; sonra renderBranches().
 4) Görsel: dragover hedef .js-lane-body/.js-lane mor vurgu (.is-drop-target, tema-token); dragleave/drop temizle.
 5) Fallback: ←/→ + compose + remove + branch ops AYNEN çalışır.
 6) L10n 7 dil: TypeAlreadyInBranch + köprü.
KORU/YAPMA: veri modeli+submit+spineFromBranches+tplOrderedConceptTypes+identity/palet/moderator/forwhom DEĞİŞMEZ (drag yalnız branches[].steps+renderBranches); SortableJS KULLANMA (custom HTML5 DnD); tip-tekrar guard zorunlu (BE-B ile tutarlı); yalnız taslak (templateReadOnly→kapalı); ←/→+compose+remove+branch ops korunur; FE-5(sekmeler)/FE-6(yayın-diyaloğu) DOKUNMA; backend/liste/diğer-tab DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → yeşil; git diff template-form.js(+css/resx); backend/liste/diğer-tab/submit diff yok. Ayrı commit ("feat(crm): WP-CT-FE-4 — chain template diagram drag-and-drop (type→branch, step move, cross-branch, type-repeat guard) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: grid drop'tan insertIndex güvenilir hesaplanamıyorsa (kart-üstü/dal-sonu ayrımı yeterli); çapraz-dal index kayması moveTo'yu bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-24) → **ACCEPTED (E2)**
```
Commit: 71e6f48f · Agent: PASS (229/0, 10 drag senaryosu tarayıcı sim) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe4 @71e6f48f → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (9 dosya, +117/−8):** 7 resx (+1) · _TemplateFormL10n · template-form.js (+114). **backend/liste/diğer-tab diff YOK. SortableJS KULLANILMADI** (custom HTML5 DnD).
- ✅ **moveTo (kod okundu):** `templateReadOnly` guard (draft-only) · **tip-tekrar guard** (hedef dalda tip var + sameStep değilse → toast `TypeAlreadyInBranch`) · kind=type→`{conceptTypeId,min:1,max:1}` · kind=step→çıkar+aynı-dal-ileri −1 · sonra renderBranches. Palet draggable = `!readonly && !archived`; kart draggable = `!ro`. Veri modeli/submit/spine dokunulmadı.
- ✅ **Agent tarayıcı E4 (gerçek DragEvent/DataTransfer, 10 senaryo):** paletten ekleme (kart önüne ×1) · tip-tekrar engel+uyarı · aynı-dal ileri/geri/sona · çapraz-dal (min/max taşınır, kayma yok) · şerit-dışı yok sayıldı · mor vurgu · açık-min/max kartı sürüklenmez · yayınlanmışta hiçbir şey sürüklenmez (zorla drop reddedilir) · kaydet payload sürükleme sonucunu + yeniden-türetilmiş omurgayı taşır.
- ✅ **Fallback korundu:** ←/→ · compose · remove · branch ops. **L10n 7 dil:** TypeAlreadyInBranch + köprü. **Build+test:** Diten.Web.Tests **229/0**.

**WP-CT-FE-4 KOMPLE (E2).** E4 = fleet restart + fareyle canlı sürükle. Sıradaki: FE-5 (sağ sekmeler: Bağlantılar/Uyumsuz[BE-A diagnostics+BE-B resolve]/Sürümler — backend endpoint'lerini tüketen ilk FE).

