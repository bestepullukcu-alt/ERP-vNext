# WORK PACKAGE — WP-ST-EDIT-V · İçerik bölümü DÜZELTME: checkbox kart ızgarası (mockup) — EDIT-U picker'ını değiştirir (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`34297ab0` üstü — WP-ST-EDIT-U §37 sonrası). **E4 geri bildirim (kullanıcı, mockup vs bizimki):** EDIT-U İçerik'i **yanlış desene** çevirdi (boş-dashed + "+ İçerik bağla" picker). Mockup İçerik = **checkbox kart ızgarası**: TÜM içerik listelenir; **yayında** olanlar yeşil "yayında · sabitlenir" + seçilebilir checkbox; **taslak/arşiv** olanlar gri "taslak · seçilemez"/"arşiv · seçilemez" + PASİF; altta mono not "SİLME YOK — oyunlar yalnızca arşivlenir; aktif sürümler değişmezdir." **Segment picker deseni DEĞİL.** **Frontend** (form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx). Backend/contract/Controller DEĞİŞMEZ.
>
> **CT ön-araştırma (canlı):** `knowledge_paths`=0, `content_engagement_journeys`=0 (DitenERP_Dev) → ızgara, içerik master verisi seed edilene kadar **boş** gelir (SKU gibi; kullanıcı deseni istiyor, veri sonra).

## Kanıt
- **Mockup ekran görüntüsü (kullanıcı, resim 1):** başlık "6 İçerik — HANGİ HİKÂYE"; helper "Yalnızca yayınlanmış knowledge-path veya content-engagement-journey bağlanabilir; referanslar sürüme sabitlenir **(pinned)**."; grid `repeat(auto-fit,minmax(270px,1fr))`; kart = **checkbox** + ad (fw) + kind·kod (mono, "knowledge-path · KP-114") + **durum rozeti**: yayında→yeşil "yayında · sabitlenir" (checkbox aktif), taslak→gri "taslak · seçilemez" (pasif+soluk), arşiv→gri "arşiv · seçilemez" (pasif+soluk). Altta: **"SİLME YOK — oyunlar yalnızca arşivlenir; aktif sürümler değişmezdir."** (mono uppercase). "+ İçerik bağla" butonu / dashed boş-hal / picker YOK.
- **EDIT-U (değişecek):** `_Form.cshtml contentSection` = `#contentBindingList` + `#contentBindingEmpty` (dashed) + `#contentPicker` + `#btnAddContentBinding`. form.js: `options.path/journey` `.filter(status==='published')` (182/192 — **taslak/arşiv düşüyor**, mockup için gerekli); `renderContents` (653) sadece bağlı olanları kart yapar; `contentPickerOpen`/`renderContentPicker`/`updateAddContentBtnLabel`/toggle (770-796); `contentPool()` (617); `contentById`/`contentKindLabel`.
- **contract:** ContentBindingsJson = `contentRefType` (knowledge-path|content-engagement-journey) + `contentRefId` + `sortOrder` + `notes`. Typed+published+pinned. DEĞİŞMEZ. path status=`pathStatus`, journey=`journeyStatus`.

## NE (frontend; backend/contract/Controller DEĞİŞMEZ)
1. **Load — published filtresini KALDIR:** `options.path`/`options.journey` mapping'i TÜM öğeleri tutsun (status + code/name/kind ile); `.filter(o => o.status === 'published')` **kaldırılır** (taslak/arşiv de ızgarada gri gösterilecek). status ham değeri korunur (published/draft/archived).
2. **renderContents → checkbox kart ızgarası (mockup):** `contentPool()` (tüm path+journey) üzerinde kart grid: her kart = `<label class="st-content-choice">` + `<input type="checkbox" class="js-content-check" data-id="${id}" data-kind="${kind}">` + ad + kind·kod (mono) + **durum rozeti**. 
   - **yayında** (`status==='published'`): checkbox **aktif**; yeşil rozet `ContentStatusPublished`="yayında · sabitlenir"; `checked` = `state.contents`'te var mı.
   - **arşiv** (`status==='archived'`): checkbox **disabled** + kart soluk (`is-disabled`); gri rozet `ContentStatusArchived`="arşiv · seçilemez".
   - **taslak** (diğer/`draft`): checkbox **disabled** + soluk; gri rozet `ContentStatusDraft`="taslak · seçilemez".
   - frozen → tüm checkbox disabled. `contentById` çözülür (pool'dan). Grid `repeat(auto-fit,minmax(270px,1fr))`.
3. **Checkbox toggle handler:** `.js-content-check` change → checked ise `state.contents.push({ contentRefType: kind, contentRefId: id, sortOrder: state.contents.length*10, notes:null })` (yalnız published seçilebilir); unchecked ise `state.contents`'ten o contentRefId'yi çıkar. renderContents (sadece rozet/checked tazelenir; ağır yeniden-render şart değilse checked set etmek yeter). frozen'da inert.
4. **Picker/ekle/dashed KALDIR:** `#contentPicker` + `#btnAddContentBinding` + `#contentBindingEmpty`(dashed) + form.js `contentPickerOpen`/`renderContentPicker`/`updateAddContentBtnLabel`/toggle handler/js-content-choice pick → **kaldır**. Yerine ızgara + footer notu.
5. **Boş-hal:** `contentPool()` boşsa (0 içerik) → kısa mesaj (`ContentGridEmpty`="Bağlanabilir içerik yok." veya benzeri) — dashed değil, sade metin.
6. **Footer notu:** section altına mono uppercase **"SİLME YOK — oyunlar yalnızca arşivlenir; aktif sürümler değişmezdir."** (`ContentNoDeleteNote`, 7 dil).
7. **Helper:** `ContentPublishedOnlyHelp` sonuna **"(pinned)"** ekle (mockup).
8. **css + resx (7 dil) + L köprüsü:** `.st-content-choice` (choice-box/kart, checkbox hizası, `is-disabled` soluk), durum rozetleri (yeşil `bg-label-success` / gri `bg-label-secondary`), footer notu. Yeni resx: `ContentStatusPublished`/`ContentStatusDraft`/`ContentStatusArchived`/`ContentNoDeleteNote`/`ContentGridEmpty` + güncel `ContentPublishedOnlyHelp`. form.js'in okuduğu YENİ anahtarları `_IndexL10n` whitelist'e ekle. Kullanılmayan `ContentPinned`/`AddContentBinding`/`NoContentBindings`/`ContentPickerClose`/`ContentPickerEmpty` bırakılabilir (zararsız) veya temizlenebilir. PascalCase köprü.

## KORU / YAPMA
- Backend/contract/**Controller/ViewModel DEĞİŞMEZ**. ContentBindingsJson sözleşmesi (contentRefType/contentRefId/sortOrder/notes) KORUNUR; **yalnız yayında içerik seçilebilir/bağlanır** (typed+published+pinned ilkesi — taslak/arşiv checkbox disabled, asla state'e girmez); contentRefType seçilenin kind'inden (uydurma yok). `ContentBindingsJson = JSON.stringify(state.contents)` şekli DEĞİŞMEZ. renderContents/content-load/content-events **dışındaki her şey DOKUNMA**: Segment/Frekans/KAPSAM/Ürün+SKU/sağ panel + segment picker + loadOptions'ın segment/policy/product/gsku dalları. Liste/Detay/details.js/CrmService/MdmService DOKUNMA. Tema/L10n köprüsü.
- **DUR:** published filtresini kaldırmak başka bir tüketiciyi (contentOptionsFor vb.) bozuyorsa (options.path/journey'i published sanan başka kod varsa) → DUR+raporla. Checkbox toggle ContentBindingsJson round-trip'i bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx. **Backend/Controller/ViewModel/liste/detay diff YOK.**
- **E4 (FLEET RESTART):** İçerik = checkbox kart ızgarası; yayında→yeşil "yayında · sabitlenir" seçilebilir, taslak→"taslak · seçilemez" pasif, arşiv→"arşiv · seçilemez" pasif; altta "SİLME YOK …" notu; checkbox işaretle→kaydet→ContentBindingsJson'a girer + tekrar aç→checked; picker/ekle butonu YOK. **`knowledge_paths`/`journeys`=0 → ızgara boş (beklenen; içerik seed edilince dolar).** Razor+css+resx → FLEET RESTART.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-V · İçerik bölümü DÜZELTME — checkbox kart ızgarası (mockup), EDIT-U picker'ını değiştirir (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 34297ab0 üstü · Worktree: ana checkout

CT ön-araştırma: knowledge_paths=0, content_engagement_journeys=0 → ızgara veri gelene kadar boş (beklenen). Mockup İçerik = checkbox kart ızgarası (segment picker DEĞİL); EDIT-U yanlış deseni kurdu, bu WP değiştirir.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-V-content-checkbox-grid.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (content load 172-192 .filter(published), renderContents 653-682, contentPool 617, contentById/contentKindLabel, contentPickerOpen/renderContentPicker/updateAddContentBtnLabel/toggle 770-796, content remove 828) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (contentSection: #contentBindingList/#contentBindingEmpty/#contentPicker/#btnAddContentBinding) · _IndexL10n.cshtml (L köprüsü) · wwwroot/assets/css/strategy-create.css (.st-content-*/.st-segment-choice; choice-box referans backbone-custom.css :has(:checked)) · 7 resx. Mockup: kullanıcı ekran görüntüsü (checkbox kart: ad + kind·kod + durum rozeti; yayında yeşil seçilebilir / taslak-arşiv gri pasif; footer "SİLME YOK …").

NE (frontend; backend/contract/Controller DEĞİŞMEZ):
 1) options.path/journey load: .filter(o=>o.status==='published') KALDIR → TÜM öğeler (status ham + code/name/kind korunur).
 2) renderContents → checkbox kart ızgarası (contentPool tüm path+journey): kart = <label.st-content-choice> + <input type=checkbox .js-content-check data-id data-kind> + ad + kind·kod (mono) + durum rozeti. published→checkbox aktif + yeşil ContentStatusPublished, checked= state.contents'te var; archived→disabled+soluk+gri ContentStatusArchived; diğer/draft→disabled+soluk+gri ContentStatusDraft. frozen→hepsi disabled. grid repeat(auto-fit,minmax(270px,1fr)).
 3) .js-content-check change: checked→state.contents.push({contentRefType:kind,contentRefId:id,sortOrder:len*10,notes:null}); unchecked→o contentRefId'yi çıkar. yalnız published seçilebilir; frozen inert.
 4) KALDIR: #contentPicker + #btnAddContentBinding + #contentBindingEmpty(dashed) + form.js contentPickerOpen/renderContentPicker/updateAddContentBtnLabel/toggle+choice-pick handlerları.
 5) contentPool boşsa kısa mesaj (ContentGridEmpty), dashed değil.
 6) footer mono uppercase not: "SİLME YOK — oyunlar yalnızca arşivlenir; aktif sürümler değişmezdir." (ContentNoDeleteNote, 7 dil).
 7) ContentPublishedOnlyHelp sonuna "(pinned)".
 8) css + resx 7 dil + L köprüsü: .st-content-choice (checkbox hizası, is-disabled soluk) + yeşil/gri rozet + footer not; yeni ContentStatusPublished/ContentStatusDraft/ContentStatusArchived/ContentNoDeleteNote/ContentGridEmpty + güncel ContentPublishedOnlyHelp; form.js'in okuduğu YENİ anahtarları _IndexL10n whitelist'e ekle; PascalCase köprü.
KORU/YAPMA: backend/contract/Controller/ViewModel DEĞİŞMEZ; ContentBindingsJson sözleşmesi korunur; yalnız published seçilebilir/bağlanır (taslak/arşiv disabled, state'e girmez); contentRefType kind'den; ContentBindingsJson=JSON.stringify(state.contents) şekli değişmez; Segment/Frekans/KAPSAM/Ürün+SKU/sağ panel + segment picker + loadOptions segment/policy/product/gsku dalları DOKUNMA; Liste/Detay/details.js/CrmService/MdmService DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+_Form+_IndexL10n+css+7 resx; backend/Controller/ViewModel/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-V — İçerik checkbox kart ızgarası (mockup); EDIT-U picker'ını değiştirir (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: published filtresini kaldırmak başka tüketiciyi bozuyorsa; checkbox toggle ContentBindingsJson round-trip'i bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 1f738a09 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitv-verify @1f738a09
```
- ✅ **Kapsam (11 dosya, +156/−138):** form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx. **backend/Controller/ViewModel/liste/detay TEMİZ** ✓. form.js'te **diğer bölümler (segment/frekans/scope/product/bindSelect2/loadAll) dokunulmamış** (KORU) ✓.
- ✅ **Checkbox ızgara (mockup):** load `.filter(status==='published')` KALDIRILDI → `contentPool()` tüm path+journey; renderContents kart = `<label.st-content-choice>` + `.js-content-check` checkbox + ad + kind·kod + durum rozeti. `isPublished` (635) → `disabled = frozen || !isPublished` (636): yayında checkbox aktif+yeşil `ContentStatusPublished`, arşiv/taslak disabled+soluk+gri. Grid repeat(auto-fit,minmax(270px,1fr)).
- ✅ **KORU=0 (published-only + sözleşme):** disabled checkbox → yalnız published state'e girer (typed+published+pinned korunur); toggle (741) checked→push({contentRefType:kind,contentRefId,sortOrder:len*10}), unchecked→splice; `ContentBindingsJson=JSON.stringify(state.contents)` (860) şekli DEĞİŞMEZ, sortOrder normalize (825). Picker/ekle/dashed + ölü document-click referansı temizlendi. Footer "SİLME YOK…" (`ContentNoDeleteNote`), helper "(pinned)".
- ✅ **DUR temiz:** `contentOptionsFor` ölüydü (kaldırıldı); hiçbir tüketici `options.path/journey`'i published-only saymıyor; yan panel `state.contents.length` sayar.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: checkbox ızgara + durum rozetleri + footer. **`knowledge_paths`/`journeys`=0 → ızgara boş (beklenen; içerik seed edilince dolar).** FLEET RESTART.

**WP-ST-EDIT-V KOMPLE (E2). İçerik mockup-doğru (checkbox ızgara). Düzenle sayfası tam. EDIT-U picker'ı değiştirildi.**
