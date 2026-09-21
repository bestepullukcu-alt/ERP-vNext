# WORK PACKAGE — WP-ST-EDIT-A · StrategyTemplate Düzenle: KAPSAM scope cascade (Campaign frontend aynası)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`ccb8d8ac` üstü). **Faz 2a** (Düzenle authoring UI'ın fonksiyonel çekirdeği): mockup "KAPSAM — NEREDE" bölümü — ScopeType seçici (tenant/ülke/tüzel kişilik/iş birimi) + cascade **Ülke → Tüzel Kişilik → İş Birimi/Territory** + ÇÖZÜMLENEN KAPSAM. **Frontend** (ViewModel + Web controller mapping + `_Form.cshtml` + `form.js` + resx). Backend (CrmService) DEĞİŞMEZ — WP-ST-SCOPE scope alanları + `scope-options` endpoint + create/update scope validation hazır; scope-options Web proxy WP-ST-LIST'te eklendi. **Faz 2b ayrı:** sağ panel özet/checklist + frekans netleştirme + görsel fidelity.

## İlke: Campaign frontend scope-cascade'ini AYNALA
Campaign scope'u StrategyTemplate gibi **editable** (CyclePeriod'unki immutable-identity → uymaz). Campaign `_Form.cshtml` scopeSection + `form.js` scope cascade + ViewModel scope alanları + controller GET(ScopeOptions doldur)/POST(scope map) deseni birebir kaynak.

## Kanıt (mevcut)
- StrategyTemplate authoring: `Create.cshtml`/`Edit.cshtml` → `_Form.cshtml` (Kimlik/Segment/Frekans/Ürün/İçerik/**Classification**[SubjectType+opak BusinessUnitId]/Lifecycle). **KAPSAM scope bölümü YOK.** `form.js` binding-builder'lar (segment/product/content). ViewModel `StrategyTemplateViewModels.cs` (BusinessUnitId var, ScopeType/Country/Legal YOK).
- Campaign (ayna): `Views/CRM/Campaigns/_Form.cshtml` scopeSection (`data-scope-options-url="/CRM/Campaigns/api/scope-options"` + `asp-for="ScopeType"` + country/LE/BU alanları) + `wwwroot/assets/js/CRM/Campaigns/form.js` (veya scope cascade scripti) + `Models/CRM/CampaignViewModels.cs` (ScopeType/CountryScope/LegalEntityId/ScopeOptions) + `Controllers/CRM/CampaignsController.cs` (GET ScopeOptions doldurur, POST scope map eder).
- StrategyTemplate scope-options Web proxy HAZIR: `[HttpGet("api/scope-options")]` → `/api/crm/strategy-templates/scope-options` (WP-ST-LIST).

## NE (frontend; CrmService DEĞİŞMEZ)
1. **ViewModel** (`StrategyTemplateViewModels.cs` edit VM): `ScopeType`/`CountryScope`/`LegalEntityId` ekle (BusinessUnitId var) + `ScopeOptions` (Campaign VM'deki tip; `StrategyTemplateScopeOptionsViewModel` benzeri — country/LE/territory-BU feed'leri). Mirror Campaign edit VM scope alanları.
2. **Web controller** (`StrategyTemplatesController.cs`): GET Create/Edit → `Model.ScopeOptions`'ı `api/scope-options`'tan doldur (Campaign controller deseni; mevcut edit-VM doldurma akışına ekle). POST Create/Edit → ViewModel scope alanlarını API create/update request'ine map et (scopeType/countryScope/legalEntityId/businessUnitId). Edit'te mevcut değerleri seed et.
3. **_Form.cshtml:** yeni **KAPSAM — NEREDE** section (Campaign scopeSection markup aynası: `data-scope-options-url="/CRM/StrategyTemplates/api/scope-options"` + ScopeType seçici + Ülke/Tüzel Kişilik/İş Birimi-Territory alanları + ÇÖZÜMLENEN KAPSAM özeti). Classification'dan **opak BusinessUnitId input'u KALDIR** (scope'a taşındı; SubjectType Classification'da kalır). Section sırası mockup'a yakın (Kimlik → KAPSAM → Segment → Frekans → Ürün → İçerik) — mümkünse; değilse KAPSAM'ı Kimlik'ten sonra ekle.
4. **form.js:** Campaign scope cascade mantığını aynala — ScopeType değişince alan göster/gizle + scope-options feed'lerini yükle (country→LE→BU/territory cascade; BU listesi country-türevli/gated). Single-reference invariant'a uygun (yalnız seçili seviyenin referansı gönderilir; diğerleri temizlenir). Mevcut binding-builder mantığı (segment/product/content) KORUNUR.
5. **resx (7 dil):** ScopeSection/ScopeSectionHint/ScopeType + ScopeType_tenant/country/legal-entity/business-unit + ÇÖZÜMLENEN KAPSAM etiketi + feed readiness metinleri (Campaign resx anahtarlarını reuse/ayna). PascalCase köprü.

## KORU / YAPMA
- CrmService DEĞİŞMEZ (scope alanları + endpoint + validation hazır). Faz 2b (sağ panel özet/checklist + frekans info-box + tam görsel mockup) BU WP'DE YOK. Segment/Frekans/Ürün/İçerik binding-builder'ları + SKU%/homojen-tip/content-pinned + Lifecycle + form submit akışı DOKUNMA (yalnız KAPSAM ekle + opak BU kaldır + scope map). Liste/Detay/details.js DOKUNMA. Single-reference invariant (backend zaten reddeder ama UI de tek referans göndersin). scope-options yoksa graceful (Campaign davranışı). Tema/L10n köprüsü. Mirror Campaign (CyclePeriod immutable deseni değil).

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: StrategyTemplateViewModels + StrategyTemplatesController + _Form.cshtml + form.js + resx. CrmService diff YOK.
- **E4:** Düzenle'de KAPSAM section — ScopeType seç → cascade (Ülke→LE→BU/Territory) → ÇÖZÜMLENEN KAPSAM; kaydet → scope backend'e gider (create/update, WP-ST-SCOPE validate eder); edit'te mevcut scope yüklenir; opak BU input'u yok. **Razor → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-A · StrategyTemplate Düzenle KAPSAM scope cascade (Campaign aynası) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: ccb8d8ac üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-A-scope-cascade.md · KAYNAK (ayna) Campaign: Views/CRM/Campaigns/_Form.cshtml (scopeSection ~274), wwwroot/assets/js/CRM/Campaigns/form.js (scope cascade), Models/CRM/CampaignViewModels.cs (ScopeType/CountryScope/LegalEntityId/ScopeOptions), Controllers/CRM/CampaignsController.cs (GET ScopeOptions doldur + POST scope map) · HEDEF StrategyTemplate: Views/CRM/StrategyTemplates/{_Form.cshtml,Create.cshtml,Edit.cshtml}, wwwroot/assets/js/CRM/StrategyTemplates/form.js, Models/CRM/StrategyTemplateViewModels.cs, Controllers/CRM/StrategyTemplatesController.cs (scope-options proxy ZATEN VAR). Referans CyclePeriod _Form/form.js scope cascade (ama Campaign editable deseni tercih).

NE (frontend; CrmService DEĞİŞMEZ):
 1) ViewModel: edit VM'e ScopeType/CountryScope/LegalEntityId + ScopeOptions ekle (Campaign VM aynası; BusinessUnitId var).
 2) Controller: GET Create/Edit → Model.ScopeOptions'ı /CRM/StrategyTemplates/api/scope-options'tan doldur (Campaign deseni); POST → scope alanlarını API create/update request'e map et (scopeType/countryScope/legalEntityId/businessUnitId); edit'te mevcut scope seed.
 3) _Form.cshtml: KAPSAM — NEREDE section ekle (Campaign scopeSection aynası, data-scope-options-url="/CRM/StrategyTemplates/api/scope-options", ScopeType seçici + Ülke/Tüzel Kişilik/İş Birimi-Territory + ÇÖZÜMLENEN KAPSAM); Classification'dan opak BusinessUnitId input'u KALDIR (SubjectType kalır). Sıra Kimlik→KAPSAM→Segment→Frekans→Ürün→İçerik.
 4) form.js: Campaign scope cascade aynası (ScopeType→göster/gizle + scope-options feed country→LE→BU/territory, BU country-gated; single-reference: yalnız seçili seviye gönderilir). Mevcut binding-builder (segment/product/content) KORU.
 5) resx 7 dil: ScopeSection/Hint/ScopeType/ScopeType_* + ÇÖZÜMLENEN KAPSAM + readiness (Campaign anahtar aynası). PascalCase köprü.
KORU/YAPMA: CrmService DEĞİŞMEZ; Faz 2b (sağ panel özet/checklist+frekans info+tam görsel) YOK; Segment/Frekans/Ürün/İçerik builder + SKU%/homojen/pinned + Lifecycle + submit akışı DOKUNMA (yalnız KAPSAM ekle + opak BU kaldır + scope map); Liste/Detay/details.js DOKUNMA; single-reference; scope-options yoksa graceful; mirror Campaign (CyclePeriod immutable değil); tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff ViewModels+Controller+_Form+form.js+resx; CrmService diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-A — Düzenle KAPSAM scope cascade (Campaign aynası: country→LE→BU/territory) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: Campaign scope cascade beklenenden farklıysa (shared script vs per-module); scope map create/update request şeklini bozuyorsa; değişiklik kapsam dışına (binding builder/detay/CrmService) taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 55b52feb · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stedita-verify @55b52feb
```
- ✅ **Kapsam (11 dosya, +340/−5):** StrategyTemplateViewModels(EditVM scope alanları + DetailVM scope property additive) + StrategyTemplatesController(ToCreate/UpdatePayload scope map + ToEditModel seed) + _Form.cshtml(KAPSAM section + opak BU kaldırıldı) + form.js(cascade) + _IndexL10n + 7 resx. **CrmService diff YOK** ✓. **Detay-view/details.js/liste index.js dokunulmadı** ✓ (DetailVM'e yalnız property — Faz 3 için additive, render değişmedi).
- ✅ **Campaign aynası:** _Form scopeSection (data-scope-options-url + asp-for ScopeType + country/legal-entity/business-unit data-scope-block'ları + BU country filtresi + data-scope-note); form.js fillScope/applyScopeType/loadScopeOptions Campaign scope-options şemasını tüketir (scopeTypes/countries/legalEntities/businessUnits + readiness flag'leri: ReferenceSetUnpublished/DependencyUnavailable/NoTerritoryPlanMatches).
- ✅ **Kabul edilen kasıtlı sapmalar (agent raporladı, doğru):** (1) scope-options **client-side** yüklenir — Campaign'in GERÇEK deseni (Campaign VM'de ScopeOptions yok), "mirror Campaign" ilkesiyle uyumlu; (2) cycle-period reload atlandı (StrategyTemplate'te yok); (3) single-reference UI'da `clearUnselectedScopeRefs()` ile güçlendirildi (WP istedi); (4) ÇÖZÜMLENEN KAPSAM readout eklendi (WP istedi); (5) edit'te data-selected/kept-option ile mevcut scope korunur.
- ✅ **KORU=0:** Segment/Frekans/Ürün/İçerik binding-builder + SKU%/homojen/pinned + Lifecycle + submit akışı korundu; opak BusinessUnitId Classification'dan kaldırılıp KAPSAM'a taşındı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: Düzenle KAPSAM cascade + kaydet→scope. **Razor+resx → FLEET RESTART.**

**WP-ST-EDIT-A KOMPLE (Faz 2a). Sıra: Faz 2b (sağ panel özet/checklist + frekans info-box + görsel fidelity) + Faz 3 (Detay KAPSAM + sürüm geçmişi).**
```
