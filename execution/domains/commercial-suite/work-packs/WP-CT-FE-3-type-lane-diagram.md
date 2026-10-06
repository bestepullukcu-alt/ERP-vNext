# WORK PACKAGE — WP-CT-FE-3 · Orta panel: tip-şerit diyagramı (grid + SVG kenarlar) (frontend, mockup v2)

> **CT (SoR).** MOD-0162 Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup **Ekran 2 — orta panel (tip-şerit diyagramı)**. Mevcut tek-dal-sayfalı dikey builder (`template-form.js` `renderBranches` + `stepRow` + `pager` + `addStepRow`) yerine **tüm dalları aynı anda gösteren ızgara** gelir: sütunlar = omurga tipleri, satırlar = dallar, adımlar tip sütununda kart, dal-akışı SVG kenarlarla. **Frontend.** Veri modeli (steps={conceptTypeId,min,max}) + submit + spineFromBranches **DEĞİŞMEZ** — yalnız orta panel RENDER değişir. **Drag = FE-4** (SortableJS yüklü, burada wiring YOK). **Omurga-dışı özeti + uyum renklendirme = FE-5** · **yayın diyaloğu/salt-okunur = FE-6**. FE-2 üstüne.

## Kanıt
- **`template-form.js`:** `renderBranches()` (satır 239) **tek dal/sayfa** (branchPage + `pager`), host `tplBranches`, `setVal('tplOrderedConceptTypes', spineFromBranches().join(','))`. `stepRow` (diten-checkitem: order#/ad/kod-badge/min-max collapse `js-step-min`/`js-step-max`/remove `js-step-remove`), `addStepRow` (compose: tip + `js-compose-min/max` + add). `cardinality(s)` çipi. branches=[{name,steps:[{conceptTypeId,min,max}]}].
- **Mockup orta panel:** ızgara — sütunlar omurga tipleri, satırlar dallar; kart = sıra no + tip adı + **×min-max çipi (tıkla→min/max düzenle)** + "×" sil; **kart yatay = 16 + colIndex×216px**, dalda tip tekrar etmez → satır başına tip başına ≤1 kart. **Omurga başlığı** + durum (Geçerli / ≥2 tip gerekli / Donmuş) + yayında sütun kilidi. **Kenarlar (SVG, kartların altında):** ardışık iki adım → sonraki sütun & arada kart yok = düz ok · sonraki sütun ama arada kart var = üst-kavis · geri = alt-kavis (Tweaks'ten kapatılabilir). Dal işlemleri: "Dal ekle" · yerinde ad düzenle · "Dalı sil" (≥1 kalır). Dal yüksekliği açık min/max kartı + geri-kavise göre ayarlanır.
- **min/max:** BE-B bounds (Min 0-9, Max 1-9); çip min==max → "×N", değilse "×min–max".

## NE (frontend; veri modeli + submit DEĞİŞMEZ)
1. **`renderBranches` → tip-şerit ızgarası** (orta panel, tüm dallar; **pager kaldır**):
   - Omurga = `spineFromBranches()` (tip id sırası). **Başlık satırı** = omurga tip ADLARI (sütunlar) + durum rozeti (Geçerli / ≥2 tip gerekli / Donmuş). Boş omurga → uyarı.
   - Her dal = satır; her adım kendi tipinin **sütununda kart** (yatay 16+col×216px). Kart: sıra no · tip adı · **min/max çipi** (tıkla→inline min/max, `js-step-min`/`js-step-max` KORU) · "×" sil (`js-step-remove` KORU).
   - **SVG kenar katmanı** (kartların altında, dal-içi ardışık adımlar): ileri-düz / atlamalı-üst-kavis / geri-alt-kavis. Kenarlar **yalnız dal akışı** (ilişki değil — tip diyagramında ilişki etiketi YOK).
   - **Dal işlemleri:** "Dal ekle" · yerinde ad düzenle · "Dalı sil" (≥1 kalır). Adım **ekleme** = mevcut compose (`addStepRow`: tip + min/max + add) her dalda korunur (drag=FE-4).
   - `setVal('tplOrderedConceptTypes', …)` + submit payload + branch/step veri yapısı **AYNEN** (yalnız görsel).
2. **Tweaks (ops.):** geri-kavis aç/kapat (mockup). Tema-güvenli SVG (stroke/fill token; her iki temada okunur).
3. **L10n (7 dil):** diyagram etiketleri — `SpineStatusValid`/"Geçerli", `SpineStatusNeedsTwo`/"En az 2 tip gerekli", `SpineStatusFrozen`/"Donmuş", `AddBranch`(var)/`DeleteBranch`/"Dalı sil", `BackEdgesToggle`/"Geri okları göster" + köprü. Mevcut adım/min-max/compose anahtarları reuse.

## KORU / YAPMA
- **Veri modeli (branches steps={conceptTypeId,min,max}) + submit payload + `spineFromBranches` + `tplOrderedConceptTypes` hidden + tüm identity/palet (FE-2) + moderator/forwhom DEĞİŞMEZ.** Yalnız **orta panel render** (dikey per-page → grid). Adım ekleme/silme/min-max **çalışır kalır** (compose korunur). Tip-tekrar (dalda) davranışı korunur. **Sürükle-bırak YOK (FE-4)** — SortableJS yüklü ama wiring yok. **Omurga-dışı özeti + conformance renklendirme YOK (FE-5)** · **yayın diyaloğu/salt-okunur bandı içeriği YOK (FE-6)** — yalnız başlık durum rozeti + yayında sütun-kilit ikonu (görsel). Backend/liste/diğer-tab DOKUNMA. Tema/L10n köprüsü. Yatay taşma → kart genişliği/sarma (mockup 216px; dar ekranda scroll konteyner).
- **DUR:** çok sayıda tip/dal yatay taşmayı yönetilemez kılıyorsa (scroll konteyner ile çöz); SVG kenar yerleşimi kart yükseklik/açık-min-max ile hizalanamıyorsa → basit düz çizgiye düş + raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → yeşil. git diff: yalnız `template-form.js` (+ gerekirse `_TemplateForm.cshtml` grid host/SVG + css + resx). **backend/liste/diğer-tab/identity-submit diff YOK.** Submit payload (branches/spine/min-max/moderator/forwhom) korunur.
- **E4 (FLEET RESTART):** editör orta panelde tip-şerit ızgarası (sütun=omurga tipleri başlıklı+durum, satır=dallar, kart=min/max çipli); dal ekle/ad-düzenle/sil; adım ekle (compose) + min/max düzenle çalışır; kenarlar ileri/atlamalı/geri doğru çizilir; kaydet payload'ı bozulmadan gider. VASORA benzeri 2-dal + atlamalı akış doğru görünür.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-3 · Orta panel tip-şerit diyagramı (grid + SVG kenarlar) (MOD-0162, frontend, mockup v2)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Amaç: Zincir Şablonu v2 orta panel. Mevcut tek-dal-sayfalı dikey builder (renderBranches+stepRow+pager+addStepRow) yerine tüm dalları gösteren tip-şerit ızgarası: sütun=omurga tipleri, satır=dallar, adım=tip sütununda kart, dal-akışı SVG kenarlar. Veri modeli (steps={conceptTypeId,min,max})+submit+spineFromBranches DEĞİŞMEZ — yalnız render. Drag=FE-4 (yok), omurga-dışı+conformance=FE-5, yayın-diyaloğu/salt-okunur=FE-6.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-3-type-lane-diagram.md · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (renderBranches 239, stepRow 170, pager 196, addStepRow 205, cardinality 97, spineFromBranches 85, js-step-min/max/remove + js-compose) · _TemplateForm.cshtml (orta panel host tplBranches) · _TemplateFormL10n + resx (7 dil) · memory l10n-bridge-pascalcase-loader.

NE (frontend; veri modeli+submit DEĞİŞMEZ):
 1) renderBranches → ızgara (pager KALDIR): omurga=spineFromBranches; başlık satırı=omurga tip adları (sütun)+durum rozeti (Geçerli/≥2 tip gerekli/Donmuş). Her dal=satır; adım kendi tipinin sütununda kart (yatay 16+col×216px): sıra no+tip adı+min/max çipi(tıkla→inline js-step-min/max KORU)+"×"(js-step-remove KORU). SVG kenar katmanı (dal-içi ardışık adım): ileri-düz/atlamalı-üst-kavis/geri-alt-kavis (yalnız dal akışı, ilişki etiketi YOK). Dal işlemleri: Dal ekle/yerinde ad/Dalı sil(≥1). Adım ekleme=mevcut compose (addStepRow) korunur. setVal('tplOrderedConceptTypes',…)+submit+branch/step veri AYNEN.
 2) Tweaks (ops): geri-kavis aç/kapat; tema-güvenli SVG (token stroke/fill).
 3) L10n 7 dil: SpineStatusValid/NeedsTwo/Frozen, DeleteBranch, BackEdgesToggle (AddBranch var) + köprü.
KORU/YAPMA: veri modeli+submit+spineFromBranches+tplOrderedConceptTypes+identity/palet(FE-2)+moderator/forwhom DEĞİŞMEZ; yalnız orta panel render; adım ekle/sil/min-max çalışır (compose korunur); tip-tekrar davranışı korunur; sürükle-bırak YOK (FE-4); omurga-dışı+conformance YOK (FE-5); yayın-diyaloğu/salt-okunur içeriği YOK (FE-6, yalnız başlık durum+yayında sütun-kilit görsel); backend/liste/diğer-tab DOKUNMA; yatay taşma scroll konteyner; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → yeşil; git diff template-form.js(+_TemplateForm.cshtml/css/resx); backend/liste/diğer-tab/identity-submit diff yok; submit payload korunur. Ayrı commit ("feat(crm): WP-CT-FE-3 — chain template type-lane diagram (grid + SVG branch-flow edges) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: çok tip/dal yatay taşmayı yönetilemez kılıyorsa scroll konteyner; SVG kenar kart yükseklik/açık-min-max ile hizalanamıyorsa düz çizgiye düş+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-24) → **ACCEPTED (E2)**
```
Commit: 2867f39a · Agent: PASS (229/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe3 @2867f39a → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (9 dosya, +220/−91):** 7 resx (+7 anahtar) · _TemplateFormL10n · template-form.js (render rewrite). **backend/liste/diğer-tab/identity diff YOK.**
- ✅ **Veri modeli + submit KORUNDU (kod okundu):** `steps={conceptTypeId,min,max}` · `spineFromBranches` · **`setVal('tplOrderedConceptTypes', spine.join(','))` yeni render satır 297** · submit/toPayload bloğu diff'te değişmedi (yalnız render). js-step-min/max (0-9/1-9) · js-compose-min/max · js-step-remove korundu.
- ✅ **Izgara (kod+agent tarayıcı E4):** pager kaldırıldı, tüm dallar satır; sütun=omurga tipleri (grid-column), kart x=16+col×216px; başlık durum rozeti (Geçerli/≥2/Donmuş) + yayında sütun-kilit; kart min/max çipi→inline; **SVG kenarlar** ileri-düz/atlamalı-üst-kavis/geri-alt-kavis (Tweaks toggle, tema-token); dal ekle/ad-düzenle/sil(≥1). Agent VASORA 3-dal senaryosunu tarayıcıda doğruladı (x=16/232/448/664, kenarlar, min/max açılınca satır uzayıp kenar yeniden hizalandı, PUT payload bozulmadı, frozen görünüm kilitli).
- ⏳ **Sapma (kabul):** eski step-içi SortableJS reorder kaldırıldı (grid'de çalışmaz, drag=FE-4) → yerine kartlarda **←/→ (öne/sona al)** düğmeleri (reorder drag'siz çalışır).
- ✅ **L10n 7 dil:** SpineStatusValid/NeedsTwo/Frozen, DeleteBranch, BackEdgesToggle, MoveEarlier/Later + köprü. **Build+test:** Diten.Web.Tests **229/0**.

**WP-CT-FE-3 KOMPLE (E2).** E4 = fleet restart + canlı ızgara. Sıradaki: FE-4 (sürükle-bırak: tip→dal + adım yeniden sıra + çapraz-dal + tip-tekrar guard).

