# WORK PACKAGE — WP-ST-EDIT-U · İçerik bölümü mockup rework: kompakt sabitlenmiş kartlar + inline "İçerik ekle" picker (segment deseni) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`6298f600` üstü — WP-ST-EDIT-T §37 sonrası). **Kullanıcı isteği:** "Hangi sunum — İçerik" bölümünü mockup'a göre düzenle. Mockup İçerik = **kompakt kartlar** (ad + kind·kod + **"sabitlenmiş"** rozet) + Segment gibi **inline picker** ile ekleme — mevcut ağır form-satırı (type/ref/sortOrder dropdown) DEĞİL. **Frontend** (form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx). Backend/contract/Controller DEĞİŞMEZ.

## Kanıt
- **Mockup (standalone, dContents):** başlık **"İçerik — hangi hikâye"**; helper **"Yalnızca yayınlanmış knowledge-path veya content-engagement-journey bağlanabilir; referanslar sürüme sabitlenir."**; grid `repeat(auto-fit,minmax(270px,1fr))`; her kart = `{{c.name}}` (ad) + `{{c.kind}}` (mono uppercase = tür·kod, ör. "journey · CEJ-40") + **"sabitlenmiş"** rozet. Ayrı type/ref/sortOrder alanı YOK (segment gibi kompakt).
- **form.js content option load (172-185):** `options.path` (knowledge-paths, `status==='published'`) + `options.journey` (content-engagement-journeys, `status==='published'`); şu an `{id, text: code—name, status}` (code/name/kind AYRI değil).
- **form.js renderContents (602-639):** ağır form-satırı — `vocabSelect(contentRefTypes,'js-content-type')` (623) + ref `<select js-content-ref>` (612) + `<input js-sort>` (631) + remove. `contentOptionsFor(type)` (600). `#contentBindingEmpty` (604) düz text-muted.
- **form.js add/sync/change:** `btnAddContentBinding` (727) boş binding push; sync (805-809) js-content-type/ref/sort okur; change handler js-content-type (824).
- **SEGMENT DESENİ (referans — bunu aynala):** `segPickerOpen` (270) + `renderSegmentPicker` (292: `.st-segment-choice` havuz, name+badge+code, `chosen` filtresi) + `updateAddSegBtnLabel` (315) + renderSegments display satır (name+badge+code+remove) + `.st-segment-empty` dashed (css:400) + events (btnAddSegmentBinding toggle 658, js-seg-choice pick 677). `#btnAddSegmentBinding` (_Form) `st-segment-add`.
- **contract:** ContentBindingsJson satırı = `contentRefType` (knowledge-path|content-engagement-journey) + `contentRefId` + `sortOrder` + `notes`. Typed + published + pinned (ship invariant). DEĞİŞMEZ.

## NE (frontend; backend/contract/Controller DEĞİŞMEZ)
1. **Başlık:** `ContentSection` → **"İçerik — hangi hikâye"** (7 dil; {Ad}—{Soru}). **Helper:** `ContentPublishedOnlyHelp` → **"Yalnızca yayınlanmış knowledge-path veya content-engagement-journey bağlanabilir; referanslar sürüme sabitlenir."** (7 dil).
2. **Content option zenginleştir (load):** `options.path` ve `options.journey` mapping'ine `code`/`name` (segment/ürün gibi split) + `kind` (`'knowledge-path'` / `'content-engagement-journey'`) ekle. `status==='published'` filtresi KORUNUR. Birleşik lookup `contentById(id)` (path+journey) + `contentKindLabel(kind)`.
3. **Kompakt kartlar (renderContents):** ağır form-satırını **kaldır** → her bağlı içerik = kart: **ad** (fw) + **kind·kod** (mono uppercase, `st-content-meta`) + **"sabitlenmiş"** pill rozet + **Kaldır ikon** (btn-icon bx-trash). Grid/liste segment display gibi. `sortOrder` = otomatik index (düzenlenebilir input YOK). `contentById` çözmezse ham contentRefId göster (veri kaybı yok).
4. **Inline "+ İçerik ekle" picker (segment aynası):** `contentPickerOpen` state + `renderContentPicker` — **yayınlanmış path + journey birleşik havuzu**, her seçenek: ad + **kind rozet** + kod; `chosen` filtresi (zaten ekli contentRefId tekrar önerilmez). Seçim → `state.contents.push({ contentRefType: <seçilenin kind'i>, contentRefId: id, sortOrder: state.contents.length*10, notes:null })` + picker kapan + renderContents. `#btnAddContentBinding` toggle (segment gibi label değişir: ekle ↔ kapat). `!can('knowledge-path') && !can('content-engagement-journey')` → picker note `PickerUnavailable`. `maxContentBindings` dolunca kapalı/uyarı.
5. **Dashed boş-hal:** `#contentBindingEmpty` → `.st-segment-empty` gibi dashed bg-body kutu; `NoContentBindings` → **"Henüz içerik yok — oyunun hangi hikâyeyi anlatacağını seçin."** (7 dil).
6. **sync (ContentBindingsJson KORU):** kartlar salt-görüntü → sync js-content-type/ref/sort OKUMAZ; `state.contents` zaten pick'ten contentRefType/contentRefId taşır; `binding.sortOrder = i * 10` (otomatik index). ContentBindingsJson = JSON.stringify(state.contents) şekli DEĞİŞMEZ (contentRefType/contentRefId/sortOrder/notes). Eski js-content-type change handler (824) KALDIR.
7. **css + resx (7 dil) + L köprüsü:** kompakt kart + kind rozet + "sabitlenmiş" pill + picker havuzu + dashed boş-hal; yeni resx `ContentPinned`="sabitlenmiş", `ContentKindKnowledgePath`/`ContentKindJourney` (kısa tür etiketi), `ContentPickerClose`/`ContentPickerEmpty` (segment karşılıkları gibi), güncel `ContentSection`/`ContentPublishedOnlyHelp`/`NoContentBindings`; **form.js'in okuduğu YENİ anahtarları `_IndexL10n` whitelist'e ekle**. PascalCase köprü.

## KORU / YAPMA
- Backend/contract/**Controller/ViewModel DEĞİŞMEZ**. ContentBindingsJson sözleşmesi (contentRefType/contentRefId/sortOrder/notes) KORUNUR; içerik **typed + yayınlanmış + pinned** ilkesi korunur (picker yalnız `status==='published'` path+journey sunar; contentRefType seçilenin kind'inden gelir — uydurma tür/ref yok). renderContents/content-picker/content-events/content-option-load **dışındaki her şey DOKUNMA**: Segment/Frekans/KAPSAM/Ürün+SKU/sağ panel + segment picker deseni (paylaşımlı `.st-segment-*` sınıflarını içerik için REUSE edebilirsin ama segment davranışını değiştirme) + loadOptions'ın segment/policy/product/gsku dalları. Liste/Detay/details.js/CrmService/MdmService DOKUNMA. Tema/L10n köprüsü.
- **DUR:** sortOrder'ı otomatik index'e çevirmek mevcut sıralamayı/round-trip'i bozuyorsa; içerik türünü kind'den türetmek contentRefType sözleşmesini bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx. **Backend/Controller/ViewModel/liste/detay diff YOK.**
- **E4 (FLEET RESTART + re-login):** başlık "İçerik — hangi hikâye"; helper mockup metni; içerikler **kompakt kart** (ad + kind·kod + "sabitlenmiş" + Kaldır); ayrı type/ref/sortOrder alanı YOK; "+ İçerik ekle" birleşik aramalı/havuz picker (yayınlanmış path+journey, chosen filtresi); boş-hal dashed; kaydet→ContentBindingsJson doğru (typed+pinned korunur); tekrar aç→geri gelir. **Razor+css+resx → FLEET RESTART; js → Ctrl+F5.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-U · İçerik bölümü mockup rework — kompakt "sabitlenmiş" kartlar + inline "İçerik ekle" picker (segment deseni) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 6298f600 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-U-content-compact-rework.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (content option load 172-185, renderContents 602-639, contentOptionsFor 600, add btnAddContentBinding 727, sync content 805-809, change js-content-type 824; SEGMENT DESENİ referans: segPickerOpen 270, renderSegmentPicker 292 + .st-segment-choice/chosen, updateAddSegBtnLabel 315, renderSegments display, events 658/677, .st-segment-empty css:400) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (contentSection 275-286, segmentSection referans) · _IndexL10n.cshtml (L köprüsü 24-36) · wwwroot/assets/css/strategy-create.css (.st-segment-* 386+) · 7 resx (Resources/Views/CRM/StrategyTemplates/StrategyTemplatesIndex.<lang>.resx). Mockup: /c/tmp/mockup-strategy.html Düzenle İçerik (server 127.0.0.1:8799) — kart: ad + kind·kod + "sabitlenmiş"; başlık "İçerik — hangi hikâye".

NE (frontend; backend/contract/Controller DEĞİŞMEZ):
 1) resx ContentSection → "İçerik — hangi hikâye"; ContentPublishedOnlyHelp → "Yalnızca yayınlanmış knowledge-path veya content-engagement-journey bağlanabilir; referanslar sürüme sabitlenir." (7 dil).
 2) options.path/journey mapping'ine code/name (split) + kind ('knowledge-path'/'content-engagement-journey') ekle; status==='published' filtresi KORU; contentById(id) birleşik lookup + contentKindLabel(kind).
 3) renderContents ağır form-satırını KALDIR → kompakt kart: ad (fw) + kind·kod (mono uppercase st-content-meta) + "sabitlenmiş" pill + Kaldır ikon (btn-icon bx-trash); sortOrder otomatik index; contentById çözmezse ham contentRefId.
 4) inline "+ İçerik ekle" picker (segment aynası): contentPickerOpen + renderContentPicker (yayınlanmış path+journey birleşik havuz, her seçenek ad + kind rozet + kod, chosen filtresi); seçim → state.contents.push({contentRefType:<kind>, contentRefId:id, sortOrder:len*10, notes:null}) + kapan + renderContents; #btnAddContentBinding toggle (ekle↔kapat); ikisi de yoksa PickerUnavailable; maxContentBindings dolunca kapalı.
 5) #contentBindingEmpty → .st-segment-empty dashed; NoContentBindings → "Henüz içerik yok — oyunun hangi hikâyeyi anlatacağını seçin." (7 dil).
 6) sync: content döngüsü js-content-type/ref/sort OKUMASINI KALDIR → binding.sortOrder=i*10 (state zaten contentRefType/contentRefId taşır); ContentBindingsJson=JSON.stringify(state.contents) şekli DEĞİŞMEZ. js-content-type change handler (824) KALDIR.
 7) css + resx 7 dil + L köprüsü: kompakt kart + kind rozet + "sabitlenmiş" + picker + dashed; yeni ContentPinned/ContentKindKnowledgePath/ContentKindJourney/ContentPickerClose/ContentPickerEmpty + güncel ContentSection/ContentPublishedOnlyHelp/NoContentBindings; form.js'in okuduğu YENİ anahtarları _IndexL10n whitelist'e ekle; PascalCase köprü.
KORU/YAPMA: backend/contract/Controller/ViewModel DEĞİŞMEZ; ContentBindingsJson sözleşmesi (contentRefType/contentRefId/sortOrder/notes) korunur; typed+published+pinned ilkesi korunur (picker yalnız published path+journey, contentRefType kind'den, uydurma yok); Segment/Frekans/KAPSAM/Ürün+SKU/sağ panel + segment picker davranışı + loadOptions segment/policy/product/gsku dalları DOKUNMA (.st-segment-* REUSE edilebilir ama segment davranışı değişmez); Liste/Detay/details.js/CrmService/MdmService DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+_Form+_IndexL10n+css+7 resx; backend/Controller/ViewModel/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-U — İçerik bölümü kompakt sabitlenmiş kartlar + inline İçerik ekle picker (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: sortOrder otomatik index round-trip'i bozuyorsa; contentRefType'ı kind'den türetmek sözleşmeyi bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 34297ab0 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitu-verify @34297ab0
```
- ✅ **Kapsam (11 dosya, +205/−67):** form.js + _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + 7 resx. **backend/Controller/ViewModel/liste/detay TEMİZ** ✓. form.js diff'inde **diğer bölümlere (segment/frekans/scope/product/bindSelect2/loadAll) dokunulmamış** (KORU) ✓.
- ✅ Başlık "İçerik — hangi hikâye" + helper "…referanslar sürüme sabitlenir" (7 dil). options.path/journey mapping'i code/name/kind ile zenginleşti (`status==='published'` KORU); contentPool/contentById/contentKindLabel/canAnyContent.
- ✅ **Kompakt kart:** renderContents ağır form-satırı (type/ref/sortOrder dropdown) KALDIRILDI → ad + kind·kod (mono `.st-content-meta`) + "sabitlenmiş" pill + Kaldır ikon; `contentById` çözmezse ham contentRefId (veri kaybı yok). Inline "+ İçerik ekle" picker (contentPickerOpen 612 + renderContentPicker; birleşik yayınlanmış path+journey havuz, chosen filtresi 631; toggle). `.st-segment-*` reuse. Dashed boş-hal.
- ✅ **KORU=0 (ContentBindingsJson):** picker yalnız `status==='published'` ref sunar; `contentRefType` seçilenin kind'inden (uydurma yok); sync content döngüsü js-content-type/ref/sort OKUMAZ → `sortOrder=i*10` (otomatik index, segment deseni), state pick'ten contentRefType/contentRefId taşır; `ContentBindingsJson = JSON.stringify(state.contents)` şekli DEĞİŞMEZ. typed+published+pinned ilkesi korundu.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: başlık + kompakt kart + "sabitlenmiş" + picker + dashed. **FLEET RESTART.**

**WP-ST-EDIT-U KOMPLE (E2). Düzenle sayfasının TÜM bölümleri mockup-tam (Kimlik/KAPSAM/Segment/Frekans/Ürün+SKU/İçerik). Sonraki: Faz 3 Detay page · Campaign · (ertelenen) GSKU seed + F-GSKU-PRODUCT-SCOPE.**
