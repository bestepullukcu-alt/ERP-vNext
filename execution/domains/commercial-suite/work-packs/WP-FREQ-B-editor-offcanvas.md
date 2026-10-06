# WORK PACKAGE — WP-FREQ-B · Frekans Politikası Create/Edit offcanvas editör (frontend)

> **CT (SoR).** MOD-0165-FU03 VisitFrequencyPolicy. Branch `feature/scmm-content-studio` (`7d5bd14d` üstü). FREQ-A (konsol iskeleti + boş `_CreateEditOffcanvas` placeholder) DONE. Bu WP: **editörü doldur** — "Yeni Politika" + row "Düzenle" ortak; owner mockup'ının (güncel) görsel diliyle, contract-driven + entity-picker hedef. **Backend HAZIR** (create/update/contract) — yalnız frontend.

## Referans
- **Mockup (görsel + alan kaynağı):** `C:\tmp\mockup-freq.html` + `C:\tmp\mockup-freq-script.js` (state: targetType/freqType/count/period/priority/status/context; CTX_DEFS grupları **Organizasyon** [BU/territory] · **Kapsam** [segment/campaign/brand/product] · **Döngü** [cycle/period]; çakışma-priority radio-card'ları *Her şeyin üstünde/Kampanya seviyesi/Standart/Temel/Son çare*; source "nereden geliyor" + "frekansı etkilemez" notu).
- **Editör tasarım dili (Segment ile tutarlı):** `wwwroot/assets/css/segment-create.css` (scoped `.segment-create-scope` deseni: app-card kabuk + iç mockup dili oklch+Inter, **tema-duyarlı `--bs-*`**, accent `--bs-primary`) + `js/CRM/Segments/form.js` (contract-driven vocab + entity-picker `loadEntityOptions` + chip/pill + hydrate/edit-restore). **Bu deseni izle.**
- **Backend alanları:** VisitFrequencyPolicy — PolicyCode/PolicyName/Description/Notes · TargetType+TargetId · bağlam (BusinessUnit/TerritoryNodeId/CampaignId/SegmentId/BrandId/ProductId/CycleId/CyclePeriodId) · FrequencyType/RequiredVisitCount/PeriodType · EffectiveFrom/To · Priority (+ FrequencyPriorityBands) · Source (FrequencySource) · Status. Vocabulary `GET /api/crm/visit-frequency-policies/contract`.

## Kapsam (frontend: _CreateEditOffcanvas + form.js + gerekirse scoped css + L10n)
- **`_CreateEditOffcanvas`** doldur: create + edit **ortak** (Yeni Politika boş; Düzenle → get ile doldur). Offcanvas-end, geniş. Bölümler (mockup): Kimlik (code/name/description) · **Hedef** (TargetType seç → hedef entity-picker) · **Bağlam** (Organizasyon/Kapsam/Döngü — bağlam alanları) · **Frekans kontrolü** (FrequencyType + RequiredVisitCount + PeriodType, "Ayda 4" okunur) · **Çakışma/Priority** (band radio-card → FrequencyPriorityBands; Priority sayısı band'den) · **Source** (dropdown + "frekansı etkilemez" notu) · Effective from/to.
- **Contract-driven:** targetType/freqType/period/priority-band/source vocabulary **contract'tan** (hardcode YOK).
- **Entity-picker hedef:** TargetType'a göre hedef seçimi (segment/territory-node/account/contact/campaign/brand/product) — Segment `loadEntityOptions` deseni (proxy + isim gösterimi; GUID değil). FREQ-A controller'a gereken picker proxy'leri ekle (yoksa).
- **Submit:** backend create (`POST`) / update (`PUT/POST {id}`) — mevcut endpoint; payload backend alanlarına birebir.
- **Görsel:** Segment editör dili (scoped, tema-duyarlı, app-card kabuk + oklch iç). Yeni scoped css gerekirse `visit-frequency-create.css` (segment-create.css deseni) — ya da segment-create.css reuse (karar agent'ın, ama Segment CSS'ini DEĞİŞTİRME).

## KORU / YAPMA
- Backend create/update/contract/resolve/CRUD/soft-delete DEĞİŞMEZ (yalnız frontend + gerekiyorsa picker proxy). Payload backend alanlarıyla birebir (uydurma alan yok). FREQ-A konsol/DataTable/soft-delete/nav DEĞİŞMEZ. Vocabulary hardcode YOK (contract). Priority band contract'tan (mockup etiketleri backend band'lerine maplensin — hardcode değil). Segment dosyalarını (segment-create.css/form.js) DEĞİŞTİRME (reuse edilebilir ama düzenleme yok). Çözümleme/Detay (FREQ-C) bu WP'de YOK. Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). "Yeni Politika" → editör açılır (kimlik/hedef/bağlam/frekans/priority-band/source/effective); Düzenle → dolu; contract-driven vocab; entity-picker hedef isim gösterir; band radio-card contract band'lerine maplenir; submit create/update backend payload'a birebir; tema-duyarlı + app-card kabuk. FREQ-A + Segment dosyaları değişmedi (Segment css/form.js diff yok). git diff: _CreateEditOffcanvas + form.js + (yeni css?) + L10n + (picker proxy).
- **E4:** create/edit uçtan uca; mockup görsel eşleşme; priority band + source + hedef doğru.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-B · Frekans Politikası Create/Edit offcanvas editör (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (7d5bd14d üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-B-editor-offcanvas.md · C:\tmp\mockup-freq.html + C:\tmp\mockup-freq-script.js (görsel+alan kaynağı; CTX_DEFS Organizasyon/Kapsam/Döngü + priority band radio-card + source) · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_CreateEditOffcanvas.cshtml (FREQ-A placeholder) + Controller (proxy) · frontend/Diten.Web/wwwroot/assets/css/segment-create.css (scoped tema-duyarlı desen — DEĞİŞTİRME, reuse/örnek) + js/CRM/Segments/form.js (contract-driven + loadEntityOptions entity-picker + chip/hydrate — desen) · services/.../Api/Controllers/CRM/VisitFrequencyPoliciesController.cs (create/update/contract) + Domain/Entities/VisitFrequencyPolicy.cs (alanlar) + FrequencyPriorityBands/FrequencySource/FrequencyType/PeriodType vocab.

NE (frontend: _CreateEditOffcanvas + form.js + gerekirse scoped css + L10n + picker proxy):
 - _CreateEditOffcanvas create+edit ortak (Yeni boş; Düzenle→get doldur). Bölümler: Kimlik(code/name/description) / Hedef(TargetType→entity-picker) / Bağlam(Organizasyon:BU+territory / Kapsam:segment+campaign+brand+product / Döngü:cycle+period) / Frekans kontrolü(FrequencyType+RequiredVisitCount+PeriodType) / Çakışma-Priority(band radio-card→FrequencyPriorityBands) / Source(dropdown+"frekansı etkilemez" notu) / Effective from-to.
 - Contract-driven: targetType/freqType/period/priority-band/source contract'tan (hardcode yok).
 - Entity-picker hedef (Segment loadEntityOptions deseni): TargetType'a göre segment/territory-node/account/contact/campaign/brand/product seçimi, isim gösterir (GUID değil); gereken picker proxy'leri controller'a ekle.
 - Submit backend create/update payload'a birebir (VisitFrequencyPolicy alanları). Görsel: Segment editör dili (scoped tema-duyarlı app-card kabuk + oklch iç); yeni css gerekirse visit-frequency-create.css (segment-create.css DEĞİŞTİRME).
KORU/YAPMA: backend create/update/contract/resolve/CRUD/soft-delete DEĞİŞMEZ (yalnız frontend + picker proxy); payload birebir (uydurma alan yok); FREQ-A konsol/DataTable/soft-delete/nav DEĞİŞMEZ; vocabulary/band/source hardcode YOK (contract); Segment segment-create.css/form.js DÜZENLEME (reuse ok, edit yok); Çözümleme/Detay (FREQ-C) YOK; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); editör create+edit (tüm bölümler); contract-driven; entity-picker isim; band contract'a maplenir; submit payload birebir; tema-duyarlı+app-card; Segment css/form.js + FREQ-A diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: contract vocabulary alanları eksikse; entity-picker proxy kurulamıyorsa; payload backend alanlarıyla eşleşmiyorsa; Segment dosyası düzenlemek gerekiyorsa (reuse yerine); kapsam VisitFrequencyPolicies dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; editör create+edit (kimlik/hedef/bağlam/frekans/priority-band/source/effective); contract-driven (hardcode yok); entity-picker isim; band→contract map; submit payload birebir; tema-duyarlı+app-card; Segment(segment-create.css/form.js)+FREQ-A değişmedi.

## REVİZE (2026-09-17) — owner kararı: priority band = A (contract additive)
Agent §37-stop ile durdu (contract'ta priority-band vocabulary yok). Owner A'yı seçti → KORU gevşetilir:
- **Backend (additive, izinli):** contract `VisitFrequencyVocabulary`'ye **additive `PriorityBands`** (Domain `FrequencyPriorityBands`'ten kod+değer; backend "küçük kazanır" semantiği). Davranış/resolve/CRUD DEĞİŞMEZ — yalnız mevcut band'leri client'a expose. Etiketler L10n (Türkçe: Her şeyin üstünde/Kampanya seviyesi/Standart/Temel/Son çare → band'lere maplenir; mockup'ın "büyük kazanır" sayıları KULLANILMAZ, gerçek band değerleri).
- **Picker'lar (mevcut endpoint'ler):** segment/account/territory-node/contact/brand/product (Segment deseni) + campaign (CampaignsController) + cycle-period (CampaignsController) + audience-profile (KnowledgeAudienceProfiles) + concept-node (KnowledgeConceptNodes); business-unit = reference-set proxy. Gereken proxy'leri VFP controller'a ekle. account-contact-link mockup'ta yok — atla.
- **Validation kuralları (editör uymalı):** Update'te PolicyCode + TargetType/TargetId **immutable/read-only**; FrequencyType×PeriodType eşleşmeleri (weekly→week, biweekly→week|custom, monthly→month, cycle-based→cycle, custom→hepsi); cycle-based/period=cycle → CycleId|CyclePeriodId; period=campaign-period|source=campaign → CampaignId; source=segmentation → SegmentId; custom FreqType → Notes; RequiredVisitCount>0. TenantId gönderme.
- **Acceptance güncelleme:** CrmService.Application.Tests de baseline-diff (contract additive band testi + sıfır-yeni-fail) + Diten.Web.Tests 137/0.

## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 3429ddbe · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqb-verify @3429ddbe
```
- ✅ Scope: VFP controller (picker proxy) + _CreateEditOffcanvas + YENİ visit-frequency-create.css + YENİ form.js + contract (PriorityBands additive) + entity (PriorityBand record) + test + 7 resx. **Segment (segment-create.css/form.js) + FREQ-A (Index/_DataTable/soft-delete/nav) diff=0.**
- ✅ Backend additive (A kararı): contract VisitFrequencyVocabulary'ye PriorityBands (Domain FrequencyPriorityBands'ten kod+değer, "küçük kazanır"; mockup'ın ters "büyük kazanır" sayıları KULLANILMADI). resolve/handler/CRUD/Suggest/validation DEĞİŞMEDİ (contract +7/-2 + entity band record).
- ✅ Editör: create+edit ortak offcanvas (.vfp-create-scope tema-duyarlı app-card + oklch iç); bölümler Kimlik/Hedef/Bağlam/Frekans-kontrolü/Çakışma-Priority(band radio-card contract'tan)/Source/Effective/Notlar. contract-driven (band+targetType+freqType+period+source; hardcode yok, etiket L10n). Entity-picker isim (GUID değil). Validation: PolicyCode+TargetType/TargetId immutable edit; FrequencyType×PeriodType; cycle/campaign/segment/custom zorunlulukları; TenantId gönderilmez; payload birebir.
- ✅ Picker proxy (mevcut endpoint pass-through): segment/account/contact/territory(+nodes)/campaign/cycle-periods/audience-profiles/concept-nodes/mdm-brands/mdm-products + business-units (MOD-0048 set). account-contact-link atlandı (mockup'ta yok).
- ✅ Build+test (CT izole, Release, GERÇEK): CrmService.Application.Tests ilk-run 1749/1 → **rerun 1750/0/5** (yeni band testi + baseline; ilk-run 1 fail env/sıra flake, no-build rerun temiz) + Diten.Web.Tests **137/0**.
- ⏳ E4: Yeni Politika/Düzenle editör; band radio-card + source + hedef picker; create/edit uçtan uca.

**FREQ-B editör KOMPLE. Sıra: FREQ-C (Detay quickview + Çözümleme tab/resolve).**
