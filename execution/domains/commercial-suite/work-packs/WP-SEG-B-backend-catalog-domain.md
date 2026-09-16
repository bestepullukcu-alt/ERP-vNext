# WORK PACKAGE — WP-SEG-B · attribute-catalog Domain alanı (backend, optgroup)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio`. **Backend (CrmService), küçük**. Owner: **biz**. SEG-A'nın attribute optgroup'ları (Doctor profile/Consent/Workplace/Commercial/Activity/Institution) için server-authored `Domain` alanı. **Paketli; dispatch owner'da.**

## Ölçülmüş girdi (CT)
- `Features/Segmentation/Catalog/SegmentAttributeDefinition.cs` record: AttributeCode, AttributeClass, Source, ValueType, Operators, RequiredParameters, OptionalParameters, AllowedSubjectTypes, CrossServiceReferenceKind (+DisplayName?). **Domain/Group/Category alanı YOK.**
- `SegmentAttributeCatalog.cs` her attribute'u `Native(...)`/`Derived(...)` ile tanımlıyor. `GetSegmentAttributeCatalogHandler` katalog DTO'sunu döndürüyor.

## Kapsam (backend, additive)
1. `SegmentAttributeDefinition`'a **`string Domain`** alanı ekle (iş-dili grup: `doctor-profile` / `consent` / `workplace` / `commercial` / `activity` / `institution`).
2. `SegmentAttributeCatalog.cs`'te her attribute'a Domain ata (contact.* → doctor-profile; consent.* → consent; territory.*/account.country/city → workplace; account.type/category → institution; product/brand → commercial; activity/created-at → activity). Mantıklı grupla.
3. Katalog response DTO'suna Domain'i yansıt (`GetSegmentAttributeCatalogHandler` + response contract).

## YAPMA
- Attribute code/operator/value-source/subject-type DEĞİŞTİR (additive alan). Yeni attribute ekleme. Frontend (SEG-A). Başka modül. Domain'i validation'a bağlama (yalnız sunum/gruplama).

## Acceptance
- **E2:** build temiz; katalog response her attribute için Domain taşır; mevcut attribute/operator/value davranışı değişmez; CrmService.Application.Tests baseline-diff sıfır-yeni-fail (varsa katalog testi Domain'i doğrular).

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-B · attribute-catalog Domain alanı (MOD-0167-FU02, backend)
Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD> · Worktree: ana checkout
Önce oku: WP-SEG-B-backend-catalog-domain.md · Features/Segmentation/Catalog/SegmentAttributeDefinition.cs + SegmentAttributeCatalog.cs + Handlers/QueryHandlers/GetSegmentAttributeCatalogHandler.cs + katalog response contract.
NE (backend, additive): SegmentAttributeDefinition'a string Domain ekle; SegmentAttributeCatalog'ta her attribute'a iş-dili Domain ata (doctor-profile/consent/workplace/commercial/activity/institution); katalog response DTO'suna yansıt.
NASIL: yalnız additive alan; mevcut Native/Derived tanımlarına Domain parametresi. Attribute/operator/value-source/subject-type DEĞİŞMEZ.
YAPMA: attribute code/operator/value değiştir; yeni attribute; Domain'i validation'a bağla; frontend; başka modül.
DOĞRULA (E2): build temiz; katalog response Domain taşır; davranış değişmez; TAM CrmService.Application.Tests baseline-diff sıfır-yeni-fail. Ayrı commit. §22 TÜRKÇE. K13.
Durma: DisplayName/Domain katalog contract'ına eklenemiyorsa · kapsam Catalog dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 49a9e7ca · Agent: PASS (1740/0/5) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-segb-verify @49a9e7ca
```
- ✅ **Scope:** 5 dosya (SegmentAttributeDefinition + SegmentAttributeCatalog + GetSegmentAttributeCatalogHandler + SegmentModels DTO + test). `/resolve`/`/preview`/controller/SegmentMembership **dokunulmadı**.
- ✅ **Additive:** `SegmentAttributeDefinition`'a `string Domain` (presentation-only, XML-doc "validation/evaluation okumaz"); Native/Join/Derived'e `domain` param; attribute code/operator/value-source/subject-type/DisplayName **korundu** (`-`/`+` çiftleri yalnız domain param eklenmesi). Domain katalog response DTO'suna yansıdı.
- ✅ **Domain eşlemesi:** doctor-profile / consent / workplace (contact+account coğrafya) / institution (ilişki+account tip) / activity / commercial — 6 optgroup; backend authoritative (SEG-A slug'ları buradan alır).
- ✅ **Build+test (CT izole, Release):** build 0-err; CrmService.Application.Tests **1740/0/5** (baseline 1739→1740, +1 yeni Domain testi, yeni fail yok, flake yok).
