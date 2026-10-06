# TASARIM BRIEF — SCMM-10 ChainTemplate Moderator/ForWhom template-seviyesine + reference-driven

> **Control Tower §14 (SoR).** Kullanıcı manuel-test model geri bildirimi. Chain Template'in step-seviyesi Moderator/ForWhom'u **template-seviyesi, reference-driven** modele taşınır. Module: **MOD-0162** (ConceptChainTemplate = SCMM-10, shipped). Branch: `feature/scmm-content-studio`. Kapsam kararı: **kullanıcı (a) — şimdi Faz 1 (manuel test duraklatıldı)**.

## 1. Ne değişiyor (semantik)
- **ESKİ (step-seviyesi, SCMM-10):** her `ConceptChainStep` kendi `AllowedRoleRefs` (kim YAZAR/sahiplenir) + `AudienceDimensionRefs` (kime) taşıyordu — governance/authoring, opak string.
- **YENİ (template-seviyesi, delivery):** kitap (ChainTemplate) bütününde:
  - **Moderator = kitabı KİM SUNAR** — bir **rol tipi**: `position` / `client` / `system-auto` (yeni reference set).
  - **ForWhom = kitabın HEDEF KİTLESİ** — **AudienceProfile** referans(lar)ı (dün kurulan reference-driven profiller, ör. "Nefrolog").
- Step-seviyesi `AllowedRoleRefs`/`AudienceDimensionRefs` **kaldırılır** — **downstream-güvenli** (ölçüldü: yalnız ChainTemplate'in kendi CQRS/API/DTO'su tüketiyor; Content Set/resolver/eligibility OKUMUYOR).

## 2. KARARLAR — KİLİTLİ (2026-09-15, owner onayı)
| # | Karar | Değer |
|---|---|---|
| **D-a** | Moderator/ForWhom yeri | **Template-seviyesi** (Identity & Classification), step-seviyesi DEĞİL |
| **D-b** | Moderator kaynağı | Yeni reference set **`content-moderator-role`** (`position`/`client`/`system-auto`); select-by-name/store-code |
| **D-c** | Moderator=position → Position | **Faz 2** (Organization positions lookup, MOD-0288). Faz 1 yalnız rol-tipini saklar |
| **D-d** | ForWhom kaynağı | **AudienceProfile** (reuse, audience-profiles lookup); çoklu (0+ AudienceProfileId) |
| **D-e** | Step-seviyesi refs | **Kaldır** (tüketen yok, güvenli) |
| **D-f** | Publish-freeze | Yeni template-seviyesi Moderator/ForWhom de publish'te Branches gibi **donar** (değişiklik→yeni sürüm) |

## 3. Model (Faz 1)
**ConceptChainTemplate EKLE:**
- `ModeratorRoleType` (string?) — `content-moderator-role` ValueCode (position/client/system-auto); null=belirsiz.
- `ForWhomAudienceProfileIds` (List<Guid>) — AudienceProfile refs (0+).
- *(Faz 2:* `ModeratorPositionRef` (string?) — yalnız ModeratorRoleType=position iken.*)*

**ConceptChainStep KALDIR:** `AllowedRoleRefs`, `AudienceDimensionRefs` (+ `ConceptChainStepInput` alanları + DTO + API request + frontend step UI).

## 4. Faz 1 WP'leri (sıralı)
1. **WP-A (backend, backend-architect):** ConceptChainTemplate template-seviyesi `ModeratorRoleType` + `ForWhomAudienceProfileIds`; step-seviyesi refs kaldır; CQRS (Create/Update command + StepInput) + API request + DTO + validation (ModeratorRoleType boş-veya-bilinen; ForWhom AudienceProfile var-mı hafif kontrol, SubjectId deseni); publish-freeze kapsamına al. Migration: mevcut template'lerde step refs boş→sorunsuz; template alanları null/boş default.
2. **WP-B (reference data, ops/CT):** `content-moderator-role` set + değerler (position/client/system-auto) BRD catalog loader ile (global-vs-tenant scope_key). CT seed+publish.
3. **WP-C (frontend, frontend-ui-ux):** Identity & Classification'da Moderator (content-moderator-role select2) + ForWhom (AudienceProfile multi-select2); step satırından Moderator/ForWhom kaldır → **Branches = Tasks-checklist compose-then-add** (AUD-UI deseni: `.diten-checkitem`, compose-row Concept Type + Min/Max + Add). WP-A+B sonrası.

## 5. Faz 2 (sonra)
Moderator=position → Organization positions lookup (cross-module, MOD-0288) → `ModeratorPositionRef` picker (cascade).

## 6. Kaynaklar
- Ölçüm: ConceptChainTemplate.cs (step-level refs, no template-level) · ConceptChainTemplateCommands.cs (StepInput refs) · consumer taraması (step refs yalnız kendi CQRS/API/DTO) · business_reference_data_sets (68 set, moderator-role YOK) · AudienceProfile (reference-driven, dün) · Positions=Organization/MOD-0288.
- Desen: AUD-UI reference-driven + cascade + checklist compose-then-add · SUBJECT-UI global-product picker · BRD catalog loader (brd-catalog-loader-seed-path).
