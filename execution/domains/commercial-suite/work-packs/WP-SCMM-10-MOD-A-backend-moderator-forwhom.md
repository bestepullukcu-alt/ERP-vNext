# WORK PACKAGE — WP-SCMM-10-MOD-A · ChainTemplate Moderator/ForWhom template-seviyesine (backend)

> **Control Tower kaydı (SoR).** Faz 1 kritik yol. §14 brief: DESIGN-SCMM-10-moderator-forwhom-template-level. Module: **MOD-0162** (ConceptChainTemplate, SCMM-10). Branch: `feature/scmm-content-studio` (HEAD `9764da6f`). **Backend only** (Domain+CQRS+API+DTO+persistence+test); frontend=WP-C, reference-data=WP-B ayrı.

## Ölçülmüş girdi (CT)
- **Domain** `ConceptChainTemplate.cs`: template = SubjectId/ChainCode/ChainName/Description/OrderedConceptTypes/Branches/Status/ChainVersion/Effective.. (template-seviyesi moderator/forwhom **YOK**). `ConceptChainStep` = ConceptTypeId/MinSelection/MaxSelection/**AllowedRoleRefs**/**AudienceDimensionRefs**.
- **CQRS** `ConceptChainTemplateCommands.cs`: `ConceptChainStepInput(...,AllowedRoleRefs,AudienceDimensionRefs)` · `ConceptChainBranchInput` · Create/Update command (Branches).
- **API** `ConceptGraphRequests.cs`: `ConceptChainStepRequest(...,AllowedRoleRefs,AudienceDimensionRefs)` · Create/UpdateConceptChainTemplateRequest.
- **DTO** `ConceptGraphDtos.cs`: `ConceptChainStepDto(...,AllowedRoleRefs,AudienceDimensionRefs)` · `ConceptChainTemplateDto`.
- **Handler** `ConceptChainTemplateHandlers.cs`: publish-freeze (OrderedConceptTypes+Branches donuyor).
- **Consumer taraması:** step-level refs'i **yalnız** bu ChainTemplate CQRS/API/DTO tüketiyor → Content Set/resolver/eligibility okumuyor → **kaldırmak downstream-güvenli**.
- **Desen:** SubjectId var-mı validasyonu (`ValidateSubjectAsync`) → ForWhom AudienceProfile existence için aynı desen. class-map GUID trap (List<Guid> serialize — RegisterClassMaps).

## Kapsam (backend)
1. **Domain — ConceptChainTemplate EKLE:**
   - `ModeratorRoleType` (string?) — `content-moderator-role` ValueCode (position/client/system-auto); null default.
   - `ForWhomAudienceProfileIds` (List<Guid>) — AudienceProfile refs (0+), default boş.
2. **Domain — ConceptChainStep KALDIR:** `AllowedRoleRefs`, `AudienceDimensionRefs`.
3. **CQRS:** `ConceptChainStepInput`'tan refs kaldır; Create/Update command'e `ModeratorRoleType` + `ForWhomAudienceProfileIds` ekle; handler map + publish-freeze kapsamına al (yayımlı template'te ikisi de değişemez → 409, mevcut Branches-freeze deseni).
4. **Validation:** `ModeratorRoleType` verildiyse boş-değil (vocab kontrolü WP-B ref set'e bırakılır — backend yalnız non-empty/trim); `ForWhomAudienceProfileIds` verilen her id **var + non-archived** AudienceProfile (SubjectId `ValidateSubjectAsync` deseni; fail-closed, persist'ten önce). Boş liste geçerli.
5. **API:** `ConceptChainStepRequest`'ten refs kaldır; Create/UpdateConceptChainTemplateRequest'e `ModeratorRoleType`+`ForWhomAudienceProfileIds` ekle; controller map.
6. **DTO:** `ConceptChainStepDto`'dan refs kaldır; `ConceptChainTemplateDto`'ya `ModeratorRoleType`+`ForWhomAudienceProfileIds` ekle.
7. **Persistence:** class-map/RegisterClassMaps — yeni `ForWhomAudienceProfileIds` List<Guid> subtype-4 serialize (GUID trap). Read-time migration: mevcut doc'larda alanlar yok→null/boş (additive, kırmaz).

## YAPMA
- Frontend DOKUNMA (WP-C). Reference-data set oluşturma (WP-B). Faz 2 `ModeratorPositionRef` EKLEME (bu Faz 1). Content Set/eligibility/resolver DEĞİŞTİRME (step refs'i okumuyorlar). Moderator'a sabit vocab hardcode (ref set WP-B; backend non-empty yeter). Yeni aggregate. Başka modül.

## Acceptance
- **E2:** build temiz; unit — template create/update `ModeratorRoleType`+`ForWhomAudienceProfileIds` map+persist; step'te artık refs yok; ForWhom var-olmayan AudienceProfile→fail (persist yok); publish sonrası Moderator/ForWhom değişimi→409; read-time migration (eski doc=null/boş); class-map GUID (ForWhom query round-trip).
- **Regresyon:** tam CrmService.Application.Tests — bilinen PII flake HARİÇ yeni fail YOK (baseline-diff). Step-refs kaldırıldığı için ilgili eski testler güncellenir (kaldırılan alanlar).
- Kapsam: yalnız ConceptChainTemplate backend. Content Set/frontend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-10-MOD-A · Prompt v1.0  (ChainTemplate Moderator/ForWhom template-seviyesine — MOD-0162, backend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 13d24a62 (main-sync sonrası; kod dosyaları değişmedi) · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-A-backend-moderator-forwhom.md (bu WP) + DESIGN-SCMM-10-moderator-forwhom-template-level.md (kararlar D-a..f)
2. services/Diten.CrmService/.../Domain/Entities/ConceptChainTemplate.cs (ConceptChainTemplate + ConceptChainStep AllowedRoleRefs/AudienceDimensionRefs)
3. services/Diten.CrmService/.../Application/Features/Knowledge/Concept/ChainTemplate/ConceptChainTemplateCommands.cs + ConceptChainTemplateHandlers.cs (StepInput refs, publish-freeze, ValidateSubjectAsync deseni)
4. services/Diten.CrmService/.../Api/Models/CRM/ConceptGraphRequests.cs (ConceptChainStepRequest refs) + Application/Features/Knowledge/Concept/ConceptGraphDtos.cs (ConceptChainStepDto/ConceptChainTemplateDto)
5. AudienceProfile repo (ForWhom existence kontrolü) + RegisterClassMaps (GUID subtype trap)

NE (backend):
 1) ConceptChainTemplate EKLE: ModeratorRoleType (string?), ForWhomAudienceProfileIds (List<Guid>, default boş).
 2) ConceptChainStep KALDIR: AllowedRoleRefs, AudienceDimensionRefs.
 3) CQRS: ConceptChainStepInput'tan refs kaldır; Create/Update command'e ModeratorRoleType+ForWhomAudienceProfileIds; handler map + publish-freeze (yayımlıda ikisi de değişemez→409, Branches-freeze deseni).
 4) Validation: ModeratorRoleType verildiyse non-empty/trim (vocab WP-B'de); ForWhomAudienceProfileIds her id var+non-archived AudienceProfile (ValidateSubjectAsync deseni, fail-closed persist-öncesi); boş liste OK.
 5) API: ConceptChainStepRequest'ten refs kaldır; Create/UpdateConceptChainTemplateRequest'e yeni alanlar; controller map.
 6) DTO: ConceptChainStepDto'dan refs kaldır; ConceptChainTemplateDto'ya yeni alanlar.
 7) Persistence: class-map ForWhomAudienceProfileIds List<Guid> subtype-4; read-time migration eski doc→null/boş (additive).
NASIL: mevcut Branches publish-freeze + ValidateSubjectAsync + class-map GUID desenini birebir izle. Additive (eski content kırılmaz). Step-refs kaldırma downstream-güvenli (yalnız bu aggregate tüketiyordu).
YAPMA: frontend; reference-data set (WP-B); Faz2 ModeratorPositionRef; Content Set/eligibility/resolver DEĞİŞTİR; Moderator vocab hardcode; yeni aggregate; başka modül.
DOĞRULA (E2):
 - build temiz; unit: template ModeratorRoleType+ForWhom map/persist; step refs yok; ForWhom var-olmayan AudienceProfile→fail; publish→Moderator/ForWhom değişimi 409; read-time migration; class-map GUID round-trip.
 - TAM CrmService.Application.Tests: bilinen ContactLocationPii flake HARİÇ yeni fail YOK (baseline-diff); kaldırılan step-refs eski testleri güncelle.
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT bağımsız doğrular.

Durma koşulları: step-refs'i beklenenden fazla yer tüketiyorsa (raporla) · publish-freeze/ValidateSubject deseni uygulanamıyorsa · class-map GUID round-trip başarısızsa · kapsam ChainTemplate backend dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 8e313911 (tek) · Agent: PASS · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-wpa-verify @8e313911
```
- ✅ **Scope:** 9 dosya, hepsi CrmService (Domain/Application/Api/Persistence + test). Frontend/reference-data/başka modül sızıntısı YOK.
- ✅ **Logic (commit blob):** Domain `ModeratorRoleType`(string?)+`ForWhomAudienceProfileIds`(List<Guid>) eklendi, step `AllowedRoleRefs`/`AudienceDimensionRefs` kaldırıldı. Handler `NormalizeModerator`(blank→null) + `ValidateForWhomAsync`(her id var+non-archived, fail-closed 400, ValidateSubject deseni) + `ForWhomEqual`(order-insensitive set) + **D-f publish-freeze** (yayımlıda Moderator/ForWhom değişimi→409, no-op reorder=200).
- ✅ **DI class-map (iki bilinen tuzak da kapatıldı):** `ForWhomAudienceProfileIds`→`EnumerableInterfaceImplementerSerializer` stringGuid (binary-vs-string silent-empty trap [[crm-new-aggregate-classmap-guid]]); `ConceptChainStep` map'e `SetIgnoreExtraElements(true)` (kaldırılan refs'li eski doc STRICT-map FormatException atmasın = read-time migration [[crm-classmap-rejects-unknown-elements]]).
- ✅ **Build+test (CT izole, Release):** build 0-err; **CrmService.Application.Tests 1729 başarılı / 0 fail / 5 skip** (baseline-diff temiz, yeni fail yok, PII flake görülmedi). ConceptGraphRuntimeTests dahil.
- ⏳ **E4:** WP-B ref set + WP-C frontend sonrası A2d manuel test.

## Kalan (bu WP dışı)
- **WP-B** content-moderator-role reference set (CT/ops seed+publish) · **WP-C** frontend (Identity/Classification Moderator+ForWhom + Branches checklist). WP-A doğrulandı → sıradaki WP-B.
