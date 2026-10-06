# WORK PACKAGE — WP-ST-SCOPE · StrategyTemplate kapsam (scope) modeli — Campaign aynası (backend, FAZ 1)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (güncel main üstü). **Kullanıcı kararı:** StrategyTemplate'e Campaign-mirror scope hiyerarşisi (tenant / country / legal-entity / business-unit). Country SoT = **COUNTRY_CODES** (kilitli). Mockup: `Düzenle` "KAPSAM — NEREDE" bölümü (cascade Ülke→Tüzel Kişilik→İş Birimi/Territory). **Backend only** (entity + scope rules/validator/reason-codes/reference-sets + create/update handler + scope-options selector + DTO + DI + test). **Frontend FAZ 2'de** (bu WP'de DOKUNULMAZ). Analiz: `docs/analysis/strategy-template-targeting-model-analysis.md` §3 + §8-madde-1.

## İlke: MIRROR, reuse değil (Campaign'in bilinçli deseni)
Scope **RULES** (anlam) aynalanır (`CampaignScopeRules`→`StrategyTemplateScopeRules`), çünkü period=identity/immutable, campaign+template=editable-attribute — anlamları ayrışabilir. **Outbound bağımlılık seam'leri REUSE edilir** (anlam değil, tek HTTP client/timeout): `ICyclePeriodLegalEntityValidator`, `ICyclePeriodLegalEntityCatalog`, `ITerritoryBusinessUnitCatalog`, `IReferenceDataValidator`, `IReferenceDataCatalogReader` (hepsi DI'da kayıtlı, Campaign zaten reuse ediyor).

## Kaynak (aynalanacak) + hedef
| Campaign (kaynak) | StrategyTemplate (yeni) |
|---|---|
| `CampaignScopeTypes` (tenant/country/legal-entity/business-unit + Normalize/IsKnown/All/ByPrecedence) | `StrategyTemplateScopeTypes` |
| `Rules/CampaignScopeRules.cs` (pure Normalize + single-reference invariant + DeriveScopeType + Apply + SameScope/ScopeRef) | `Rules/StrategyTemplateScopeRules.cs` |
| `Services/CampaignScopeWriteValidator.cs` (country/BU-changed-only/LE fail-closed, persist ÖNCESİ) | `Services/StrategyTemplateScopeWriteValidator.cs` |
| `CampaignScopeReferenceSets` (CountrySet=COUNTRY_CODES, BusinessUnitSet=business-unit) | `StrategyTemplateScopeReferenceSets` |
| `CampaignReasonCodes` scope kodları (ScopeTypeUnknown/ScopeAmbiguous/ScopeReferenceRequired/CountryInvalid/CountryUnknown/BusinessUnitUnknown/ReferenceSetUnpublished/LegalEntityValidationUnavailable/LegalEntityNotReferenceable) | StrategyTemplate reason-code sınıfına scope kodları ekle |
| `Handlers/QueryHandlers/CampaignScopeQueryHandlers.cs` (`GetCampaignScopeOptionsHandler`: country IReferenceDataCatalogReader + LE ICyclePeriodLegalEntityCatalog + territory-BU ITerritoryBusinessUnitCatalog, 3 readiness flag) + `Queries/CampaignScopeQueries.cs` + `CampaignScopeOptionsDto` | `GetStrategyTemplateScopeOptionsHandler` + Query + `StrategyTemplateScopeOptionsDto` |

## NE (backend; frontend DEĞİŞMEZ)
1. **Entity** `StrategyTemplate.cs`: `ScopeType` (default `""`), `CountryScope` (string?), `LegalEntityId` (Guid?), `BusinessUnitId` (MEVCUT — kalır). `EffectiveScopeType()` + `ScopeRef()` helper (Campaign.cs deseni: değeri olmayan eski satırlar `DeriveScopeType` ile tenant/business-unit'e düşer, migration YOK). **⚠ Mongo class-map:** `LegalEntityId` yeni **Guid FK** → `RegisterClassMaps`'e MUTLAKA eklenmezse binary/boş sorgu ([[crm-new-aggregate-classmap-guid]] tuzağı) — class-map güncelle + GUID subtype-4 ([[mongo-guid-subtype-write-recipe]]).
2. **ScopeTypes + Rules + ReferenceSets + ReasonCodes:** Campaign'den birebir aynala (yukarıdaki tablo). `Normalize` single-reference invariant + `DeriveScopeType` geriye-uyum (ScopeType yoksa: country/LE varsa 400 "ScopeTypeRequired", yoksa BU→business-unit / hiç→tenant). Country ISO alpha-2 upper.
3. **ScopeWriteValidator:** Campaign aynası — normalize → country (COUNTRY_CODES, IReferenceDataValidator) → BU (**yalnız referans DEĞİŞİNCE**, business-unit set) → LE (`ICyclePeriodLegalEntityValidator` **REUSE**, fail-closed: DependencyUnavailable→503 hiçbir şey yazılmaz, !IsReferenceable→400). Persist ÖNCESİ.
4. **Create/Update handler'lara scope entegrasyonu:** `CreateStrategyTemplateHandler` mevcut sıra `code→shape→binding→cross-service(MDM ürün) proof→InsertAsync LAST` (memory: MDM fail-closed BEFORE persist). Scope validation'ı bu **cross-service-proof aşamasına** ekle (LE de dış servis; 503 → hiçbir şey persist edilmez). `UpdateStrategyTemplateHandler`: scope validate (`current` ile BU-changed-only) — **freeze guard'dan önce/uyumlu**: scope **editable metadata** (binding DEĞİL; bugünkü opak BusinessUnitId gibi düzenlenebilir, dondurulmuş sürümde bile — analiz §3 "editable attribute"). `StrategyTemplateScopeRules.Apply` ile ata (create+update tek yerden).
5. **DTO + request'ler:** Create/Update request'e `scopeType`/`countryScope`/`legalEntityId` ekle (`businessUnitId` var). `GetStrategyTemplateByIdDto` + `ListStrategyTemplatesDto` satırına scope alanları (ScopeType + EffectiveScopeType + CountryScope + LegalEntityId + BusinessUnitId + ScopeRef). İsim çözümü (LE adı/breadcrumb) FAZ 2 frontend'de (selector/lookup) — backend ham alan + kod döner (Campaign gibi).
6. **Scope-options selector:** `GetStrategyTemplateScopeOptionsHandler` (GetCampaignScopeOptions aynası: country/LE/territory-BU üç feed + üç readiness flag; hardcode liste YOK) + Query + DTO. Controller `GET api/crm/strategy-templates/scope-options`. Gateway: `/api/crm/strategy-templates` + `/{everything}` route zaten var (FU04 F-GATEWAY) → alt-path kapsanır; teyit et.
7. **DI wiring:** yeni `StrategyTemplateScopeWriteValidator` + `GetStrategyTemplateScopeOptionsHandler` kayıt; bağımlılıklar (IReferenceDataValidator/Reader, ICyclePeriodLegalEntityValidator/Catalog, ITerritoryBusinessUnitCatalog) zaten kayıtlı — sadece yeni tipleri register et.
8. **Testler (Application.Tests):** (a) Rules normalize: her scope tipi + single-reference invariant (ikinci referans → 400 ambiguous) + DeriveScopeType geriye-uyum (eski satır tenant/business-unit); (b) WriteValidator: country published/unpublished(set-missing farklı kod)/unknown-value, BU changed-only (aynı kod update'te MDM'e gitmez), LE 503-fail-closed + not-referenceable; (c) Create/Update scope ile end-to-end + LE 503 → hiçbir şey persist edilmez; (d) scope-options 3 feed + readiness; (e) **mevcut FU04 testleri + verifier 85/9 + tüm CRM suite YEŞİL kalır** (additive).

## KORU / YAPMA
- **Frontend DEĞİŞMEZ** (Views/CRM/StrategyTemplates + js — FAZ 2). Binding/freeze/version-clone/MDM-ürün/SKU%=100/segment-homojen/content-pinned mantığı DOKUNMA (additive scope). Scope = **editable metadata** (binding değil, dondurma kapsamında değil). **Mirror** (Rules/Validator/ScopeTypes/ReasonCodes/ReferenceSets ayrı) — Campaign'i **reuse etme**; **seam'leri** (LE validator/catalog, territory-BU catalog, reference validator/reader) **reuse et** (klonlama). COUNTRY_CODES (territory `country` seti DEĞİL). Tenant izolasyonu her okuma/yazmada. **Fail-closed persist öncesi** (LE/MDM 503 → sıfır yazma). Mongo class-map LegalEntityId GUID register (aksi halde sessiz boş sorgu). Uydurma scope/vocabulary YOK (governed set + MDM). Contract flag'leri (SupportsBrandBinding:false vb.) bozma.

## Acceptance
- **E2:** CrmService.Application.Tests yeşil (yeni scope testleri + mevcut FU04, PII order-flake hariç) + verifier `--area CRM --module StrategyTemplates` 85/9 baseline korunur. git diff: entity + scope rules/validator/reason/reference-sets + create/update handler + scope-options handler/query/dto + controller + DI + test. **Frontend diff YOK.**
- **E4 (FAZ 2 sonrası):** Düzenle'de KAPSAM cascade; şimdilik backend smoke (create tenant/country/legal-entity/business-unit; LE 503 fail-closed; scope-options 3 feed).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-ST-SCOPE · StrategyTemplate kapsam modeli — Campaign aynası (MOD-0167-FU04, backend FAZ 1)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: güncel main üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-SCOPE-strategy-template-scope.md · docs/analysis/strategy-template-targeting-model-analysis.md (§3,§7,§8) · KAYNAK (aynalanacak) services/Diten.CrmService/src/Diten.CrmService.Application/Features/Campaign/{Rules/CampaignScopeRules.cs, Services/CampaignScopeWriteValidator.cs, CampaignScopeReferenceSets.cs, Handlers/QueryHandlers/CampaignScopeQueryHandlers.cs, Queries/CampaignScopeQueries.cs} + Domain/Entities/Campaign.cs (ScopeType/CountryScope/LegalEntityId/BusinessUnitId + EffectiveScopeType()/ScopeRef()) + CampaignScopeTypes/CampaignReasonCodes · REUSE seam'ler Features/CyclePeriod/{Services/ICyclePeriodLegalEntityValidator.cs, Read/ICyclePeriodLegalEntityCatalog.cs, Read/ITerritoryBusinessUnitCatalog.cs} + Common/ReferenceValidation (IReferenceDataValidator/IReferenceDataCatalogReader) · HEDEF Features/StrategyTemplate/** (Domain/Entities/StrategyTemplate.cs, Handlers/CommandHandlers/{CreateStrategyTemplateHandler.cs,UpdateStrategyTemplateHandler.cs}, DTO/Queries, DependencyInjection, RegisterClassMaps).

NE (backend; frontend DEĞİŞMEZ):
 1) Entity StrategyTemplate.cs: ScopeType(""), CountryScope(string?), LegalEntityId(Guid?), BusinessUnitId(mevcut kalır); EffectiveScopeType()/ScopeRef() (Campaign deseni, eski satır DeriveScopeType→tenant/business-unit, migration yok). Mongo class-map'e LegalEntityId GUID EKLE (yoksa sessiz boş sorgu; subtype-4).
 2) StrategyTemplateScopeTypes + Rules/StrategyTemplateScopeRules.cs + StrategyTemplateScopeReferenceSets + reason-code scope kodları = CampaignScope* birebir ayna (pure Normalize + single-reference invariant + DeriveScopeType + Apply). COUNTRY_CODES.
 3) Services/StrategyTemplateScopeWriteValidator.cs = CampaignScopeWriteValidator aynası: country(IReferenceDataValidator COUNTRY_CODES)→BU(business-unit, YALNIZ changed)→LE(ICyclePeriodLegalEntityValidator REUSE, 503 fail-closed/not-referenceable). Persist öncesi.
 4) CreateStrategyTemplateHandler: scope validate'i mevcut cross-service(MDM) proof aşamasına ekle (LE 503→persist yok, sıra code→shape→binding→scope+MDM proof→InsertAsync LAST). UpdateStrategyTemplateHandler: scope validate (current ile BU changed-only), freeze guard ile uyumlu — scope EDITABLE metadata (binding değil, dondurulmuşta bile). StrategyTemplateScopeRules.Apply.
 5) Create/Update request'e scopeType/countryScope/legalEntityId; GetById + List DTO'ya scope alanları (ScopeType/EffectiveScopeType/CountryScope/LegalEntityId/BusinessUnitId/ScopeRef). İsim çözümü FAZ 2.
 6) GetStrategyTemplateScopeOptionsHandler + Query + DTO (GetCampaignScopeOptions aynası: country IReferenceDataCatalogReader + LE ICyclePeriodLegalEntityCatalog + territory-BU ITerritoryBusinessUnitCatalog, 3 readiness flag, hardcode YOK). Controller GET api/crm/strategy-templates/scope-options. Gateway /{everything} kapsıyor (teyit).
 7) DI: yeni validator + scope-options handler register (bağımlılıklar zaten kayıtlı).
 8) Testler: Rules normalize+invariant+derive; WriteValidator country/BU-changed/LE-503; Create/Update scope + LE 503 persist-yok; scope-options 3 feed; mevcut FU04 + verifier 85/9 + CRM suite YEŞİL.
KORU/YAPMA: frontend DEĞİŞMEZ; binding/freeze/version/MDM-ürün/SKU%/segment-homojen/content-pinned DOKUNMA (additive); scope=editable metadata (dondurma dışı); MIRROR (Campaign reuse etme) ama SEAM'leri reuse et (LE validator/catalog+territory-BU catalog+reference validator/reader klonlama); COUNTRY_CODES; tenant izolasyon; fail-closed persist öncesi; class-map LegalEntityId GUID; uydurma yok; contract flag'leri bozma.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo (yeni + mevcut yeşil; PII order-flake hariç). Frontend diff YOK. Ayrı commit ("feat(strategy): WP-ST-SCOPE — StrategyTemplate kapsam modeli (Campaign aynası: tenant/country/legal-entity/business-unit) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
DUR: freeze modeli scope'u dondurma kapsamına almak gerekiyorsa (analiz editable diyor ama kod farklıysa); class-map/GUID beklenmeyen kırılma; scope-options seam imzaları Campaign'den saparsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 6652edf1 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stscope-verify @6652edf1
```
- ✅ **Kapsam (29 dosya, +1648/−26):** hepsi CrmService (entity + Rules/Services/Queries/Handlers/Api-Controller+Model + Persistence class-map + 12 test). **Frontend diff YOK** (grep boş — Views/CRM/StrategyTemplates + js dokunulmadı, FAZ 2).
- ✅ **Mirror (Campaign'den ayrı sınıflar):** `StrategyTemplateScopeTypes`/`ScopeLimits` (BU ceiling = mevcut MaxBusinessUnitIdLength=64) + `Rules/StrategyTemplateScopeRules.cs` (pure Normalize + single-reference invariant + DeriveScopeType + Apply) + `Services/StrategyTemplateScopeWriteValidator.cs` (country **COUNTRY_CODES** → BU changed-only → LE fail-closed 503/not-referenceable) + `ScopeReferenceSets` + 9 scope reason-code (`StrategyTemplateErrorCodes.All`) + `GetStrategyTemplateScopeOptionsHandler` (country/LE/territory-BU 3 feed + 3 readiness, hardcode yok).
- ✅ **Seam REUSE (klon yok):** ICyclePeriodLegalEntityValidator/Catalog + ITerritoryBusinessUnitCatalog + IReferenceDataValidator/Reader.
- ✅ **Entity+handler:** ScopeType/CountryScope/LegalEntityId eklendi (BusinessUnitId korundu) + EffectiveScopeType()/ScopeRef()/HasConsistentScope(); migration yok. Create sıra code→shape→binding→**scope+MDM proof(fail-closed)**→InsertAsync LAST. Update: scope **her zaman** re-validate (editable metadata, dondurulmuş/aktif sürümde bile, freeze guard'a takılmaz — binding'ler hâlâ 409 frozen); `Apply` ile ata. Komut param'leri opsiyonel-varsayılan → mevcut çağrılar kırılmadı.
- ✅ **Class-map:** `LegalEntityId` → `NullableSerializer<Guid>(stringGuid)` (subtype-4; yeni-alan GUID tuzağı önlendi). DI: ScopeWriteValidator kayıtlı; scope-options MediatR scan.
- ✅ **KORU=0:** binding/freeze/version-clone/MDM-ürün/SKU%=100/segment-homojen/content-pinned dokunulmadı (additive); contract flag'leri korundu; tenant izolasyon; fail-closed persist öncesi. **Verifier 85/9 inherently korunur (UI değişmedi).**
- ✅ **Build+test (CT izole, Release):** Application.Tests **1830/0/5** (yeni scope testleri: Rules/WriteValidator/Integration[frozen-scope-editable+LE-503-persist-yok]/ScopeOptions dahil; PII order-flake tetiklenmedi).
- ⏳ E4: FAZ 2 (Düzenle KAPSAM cascade UI) sonrası. **Backend → FLEET RESTART.**

**WP-ST-SCOPE KOMPLE (FAZ 1 backend). Sıra: FAZ 2 — Düzenle (authoring) UI + KAPSAM cascade → Detay → Liste.**
```
