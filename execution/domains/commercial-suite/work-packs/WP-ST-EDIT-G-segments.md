# WORK PACKAGE — WP-ST-EDIT-G · Segmentler bölümü mockup hizalama (header + boş-hal kutusu + satır tasarımı + rol toggle) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (WP-ST-EDIT-F §37 sonrası HEAD üstü). **E4 rötuşu:** (1) header "Kim — segmentler *" → **"Segmentler — Kim"** (reorder + * kaldır); (2) boş-hal **dashed kutu** (mockup) ama **bg-body**; (3) segment satır/ekleme tasarımı mockup: ad + tip badge + SEG kod + rol segmented toggle (birincil/ikincil/hariç-not) + Kaldır, üstte "N segment · {tip} tipi". **Frontend** (_Form.cshtml + form.js + strategy-create.css + resx). Backend DEĞİŞMEZ.

## Kanıt
- `_Form.cshtml` (~161): `<h6>@Localizer["SegmentSection"] <span text-danger>*</span></h6>` (SegmentSection="Kim — segmentler") + `SegmentBindingHelp` sub + `#segmentBindingList` + `#segmentBindingEmpty` (düz metin "NoSegmentBindings") + "+ Segment bağla" (`AddSegmentBinding`).
- `form.js` `renderSegments` (~170): her satır `select`(pickerSelect) + rol `vocabSelect` + sort `<input number>` + Kaldır. Segment option: id/text(code — name)/subjectType/archived (**count YOK**).
- **Backend count YOK:** `SegmentModels.cs:6` — segment grid DTO bilinçle MemberCount/MemberIds taşımaz (üyelik canlı/kural-bazlı). → satırlarda **hedef sayısı gösterilemez** (uydurma yok). Ad + tip badge + SEG kod ile gidilir.
- Mockup Düzenle segment: "Segmentler — KİM" + "Tüm segmentler aynı tipte olmalı…" + "N segment · {tip} tipi" + satır: ad + "SEG-kod · N hedef" (count) + tip badge (kişi/hekim, kurum/hesap) + rol toggle (birincil/ikincil/hariç-not) + Kaldır + "+ Segment ekle". **Not: mockup count gösterir ama backend sağlamadığından count ATLANIR** (ileride ayrı membership-count WP; VFP PreviewClone deseni gibi).

## NE (frontend; backend DEĞİŞMEZ)
1. **Header:** `SegmentSection` "Kim — segmentler" → "Segmentler — Kim" (7 dil); `<h6>` yanındaki `<span text-danger>*</span>` KALDIR (zorunluluk sub-header + checklist'te). SegmentBindingHelp sub-header kalır.
2. **Boş-hal kutusu:** `#segmentBindingEmpty` düz metin → **dashed kutu** (`padding:17px; text-align:center; border:1px dashed var(--bs-border-color); border-radius:6px; color:var(--bs-secondary-color); background: var(--bs-body-bg)`) + metin "Henüz segment yok — oyunun kime uygulanacağını seçin." (NoSegmentBindings güncelle, 7 dil). Stiller css (.st-segment-empty), bg-body.
3. **Segment satır tasarımı (renderSegments):** mockup display satırı — ad (fw-medium) + **tip badge** (contact→"kişi / hekim"/primary-mavi, account→"kurum / hesap"/warning-turuncu; SubjectType friendly) + **SEG kod** (mono muted; count YOK) + **rol segmented toggle** (birincil/ikincil/hariç-not — vocabSelect yerine buton-grup/pill; cfg.bindingRoles) + **Kaldır**. sortOrder input KALDIR (otomatik index-tabanlı, gizli/data'da tutulur; mockup göstermez). Üstte **"N segment · {tip} tipi"** özet (aktif SubjectType friendly). Homojen kural + SubjectType türetme (activeSubjectType/syncSubjectType) KORUNUR.
4. **Segment ekleme (picker):** "+ Segment ekle" ile segment seçimi mockup gibi (ad + tip badge + SEG kod; count YOK). Mevcut pickerSelect/segmentOptionsFor (homojen filtre) korunur; görünüm mockup'a hizalanır (select2/liste). Rol/sort veri sözleşmesi (SegmentBindingsJson) KORUNUR.
5. **css + resx (7 dil):** boş-hal kutusu + satır + rol-toggle + tip badge stilleri; SegmentSection/NoSegmentBindings + tip badge etiketleri (SubjectTypeContact/Account reuse ya da "kişi / hekim"/"kurum / hesap"). PascalCase köprü.

## KORU / YAPMA
- Backend DEĞİŞMEZ (segment count YOK — gösterme, uydurma). SegmentBindingsJson veri sözleşmesi (segmentId/bindingRole/sortOrder) KORUNUR (sortOrder UI'dan kalksa da data'da otomatik). Homojen tip + SubjectType türetme (EDIT-C) + segment picker homojen filtre KORUNUR. Frekans/Ürün/İçerik/KAPSAM/sağ panel/submit DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. Tema/L10n köprüsü.
- **DUR:** rol toggle / satır redesign SegmentBindingsJson sözleşmesini veya SubjectType türetmeyi bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + form.js + strategy-create.css + resx. Backend/Controller/ViewModel/Liste/Detay diff YOK.
- **E4:** header "Segmentler — Kim" (* yok); boş-hal dashed kutu bg-body; segment satırları mockup (ad + tip badge + SEG kod + rol toggle + Kaldır); "N segment · tip" özet; ekleme picker mockup; homojen tip çalışır. **Razor+css+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-ST-EDIT-F §37 sonrası)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-G · Segmentler bölümü mockup hizalama (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: <WP-ST-EDIT-F §37 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-G-segments.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (SegmentSection ~161-167) · wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderSegments ~170, segmentOptionsFor ~138, activeSubjectType/syncSubjectType ~119, pickerSelect ~146, segment load ~73-79) · wwwroot/assets/css/strategy-create.css · resx (SegmentSection/NoSegmentBindings/BindingRole/SubjectTypeContact/Account). Mockup: /c/tmp/mockup-strategy.html Düzenle Segmentler.

NE (frontend; backend DEĞİŞMEZ; COUNT YOK — backend sağlamaz, gösterme):
 1) Header SegmentSection "Kim — segmentler"→"Segmentler — Kim" (7 dil); h6 yanındaki <span text-danger>*</span> kaldır. SegmentBindingHelp sub kalır.
 2) #segmentBindingEmpty → dashed kutu (.st-segment-empty: padding17; text-center; border 1px dashed var(--bs-border-color); radius6; color var(--bs-secondary-color); background var(--bs-body-bg)) + "Henüz segment yok — oyunun kime uygulanacağını seçin." (NoSegmentBindings güncelle 7 dil).
 3) renderSegments satır → display: ad(fw-medium) + tip badge (contact→"kişi / hekim"/primary, account→"kurum / hesap"/warning) + SEG kod (mono muted; count YOK) + rol segmented toggle (birincil/ikincil/hariç-not, cfg.bindingRoles; select değil buton-grup) + Kaldır. sortOrder input kaldır (otomatik index, data'da tut). Üstte "N segment · {tip} tipi" özet. Homojen + activeSubjectType/syncSubjectType KORU.
 4) "+ Segment ekle" picker mockup görünümü (ad + tip badge + SEG kod; count yok); segmentOptionsFor homojen filtre + SegmentBindingsJson sözleşmesi KORU.
 5) css + resx 7 dil (boş-hal/satır/rol-toggle/badge + etiketler). PascalCase köprü.
KORU/YAPMA: backend DEĞİŞMEZ (count yok, uydurma yok); SegmentBindingsJson (segmentId/bindingRole/sortOrder) KORU; homojen tip + SubjectType türetme + picker filtre KORU; Frekans/Ürün/İçerik/KAPSAM/sağ panel/submit DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+form.js+css+resx; backend/Controller/ViewModel/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-G — Segmentler bölümü mockup (header, dashed boş-hal, satır+rol toggle) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: rol toggle/satır redesign SegmentBindingsJson veya SubjectType türetmeyi bozuyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: ef6799d0 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steditg-verify @ef6799d0
```
- ✅ **Kapsam (11 dosya, +337/−46):** _Form.cshtml + _IndexL10n + strategy-create.css + form.js + 7 resx. **backend/Controller/ViewModel/liste/detay/details.js TEMİZ** ✓.
- ✅ Header "Segmentler — Kim" (* kaldırıldı) + "N segment · {tip} tipi" özet; dashed boş-hal `.st-segment-empty` bg-body ("Henüz segment yok…"); satır display (ad + SEG kod mono + tip badge contact→"kişi / hekim"/primary, account→"kurum / hesap"/warning; **count YOK** — backend taşımıyor) + rol segmented toggle (js-role-btn → gizli js-role input) + Kaldır; sortOrder input kalktı (otomatik i*10). "+ Segment ekle" inline picker.
- ✅ **KORU=0:** SegmentBindingsJson (segmentId/bindingRole/sortOrder) sözleşmesi + homojen filtre + activeSubjectType/syncSubjectType korundu. Frekans/Ürün/İçerik/KAPSAM/sağ panel dokunulmadı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ℹ️ Bilinçli: rol mockup'ta select'ti → WP segmented toggle istedi (WP üstün). Segment count backend'de yok → gösterilmedi (ileride membership-count WP).
- ⏳ E4: "Segmentler — Kim" + dashed boş-hal + satır+rol toggle. **FLEET RESTART.**

**WP-ST-EDIT-G KOMPLE.**
```
