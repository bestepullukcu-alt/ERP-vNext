# MOD-0151 FU04A — Resource Assignment Lifecycle, Replacement and Operational Visibility Implementation

Tarih: 2026-07-30  
Target tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
Module: `MOD-0151 — Territory Management`

## 1. Preflight

- Root `AGENTS.md`, Commercial Suite domain config, MOD-0151 module pack ve FU04/FU04A audit kaynakları okundu.
- Module pack `ready-for-dev`; `FU04A-resource-assignment-lifecycle-replacement-operational-visibility` runtime scope authorization PASS.
- Gerçek repo yapısı (`services/`, `frontend/`, `gateway/`) ve gateway-only trafik kuralı esas alındı.
- Çalışma ağacı başlangıçta yoğun biçimde kirli/untracked durumdaydı. Mevcut kullanıcı değişiklikleri korunarak yalnız FU04A kapsamına odaklanıldı.
- Protected path, RBAC seed/grant, gateway route ve `.antigravity` değişikliği yapılmadı.

## 2. Scope confirmation

Uygulanan kapsam:

- Draft/proposed ve active/operational ayrımı
- Aktivasyonda proposed → active geçişi
- Active create, end, replace ve transfer lifecycle
- Position tabanlı validation, conflict ve sorgular
- Current responsibility ve history API/UI yüzeyleri
- Contract flag’leri, testler ve gateway-only canlı smoke

Kapsam dışı tutulanlar:

- FU05 AccountTerritoryAssignment apply/history
- Account/Contact master mutation
- Workflow approval, evidence, import/export
- Visit/route planning
- Brand/Product master veya selector
- RBAC seed/grant ve MOD-0048 publish

## 3. Role-to-position implementation summary

- Canonical kimlik `TerritoryPositionRef` ve `PositionCode` olarak uygulandı.
- Snapshot: `PositionId`, `PositionCode`, `PositionTitle`, `PositionType`, `SourceSystem`, ayrıca policy validation metadata.
- Yeni write, current responsibility, history ve conflict mantığı `RoleCode` kullanmıyor.
- Eski düz `PositionId/PositionCode/PositionName` alanları mevcut doküman uyumluluğu için mirror olarak bırakıldı.
- UI terminolojisi Role yerine Position oldu.

## 4. Data model changes

- Assignment aggregate’ine canonical `Position` snapshot’ı eklendi.
- Replacement provenance:
  - `ReplacedAssignmentId`
  - `ReplacementAssignmentId`
  - `ReplacementReason`
  - `PreviousPositionCode`
  - `NewPositionCode`
- Transfer provenance:
  - `TransferFromAssignmentId`
  - `TransferToAssignmentId`
  - `TransferReason`
- Correlation, lifecycle reason, effective dates ve ended kayıtlar korunuyor.
- Hard delete eklenmedi.

## 5. API summary

Additive yüzeyler:

- `POST /api/crm/territory-models/{modelId}/resource-assignments/{assignmentId}/replace`
- `POST /api/crm/territory-models/{modelId}/resource-assignments/{assignmentId}/transfer`
- `GET /api/crm/territory-models/{modelId}/resource-responsibilities/current`
- `GET /api/crm/territory-models/{modelId}/resource-assignments/history`
- `GET /api/crm/resources/{resourceId}/territory-responsibilities`

Mevcut create/update/end/list endpointleri korundu. Frontend bütün çağrıları Gateway `:5000` üzerinden yapıyor.

## 6. Lifecycle behavior

- Draft model create sonucu `proposed`; planning-only olarak işaretleniyor.
- Proposed kayıtlar current responsibility sorgusuna girmiyor.
- Aktivasyon öncesinde Position policy ve conflict raporu fail-closed çalışıyor.
- Geçerli proposed kayıtlar model/nodes ile birlikte active duruma geçiriliyor; correlation ve reason korunuyor.
- Active model create ve end, reason + effective date gerektiriyor.
- Archived, inactive veya soft-deleted model/node mutation kabul etmiyor.
- Kritik active kayıt alanları doğrudan update ile ezilmiyor; replace/transfer/end command’leri kullanılıyor.

Replica set veya mongos topolojisinde native Mongo transaction kullanılıyor. Yerel standalone Mongo smoke ortamı için snapshot/compensation fallback’i eklendi; hata halinde önceki model/node/assignment durumu geri yükleniyor.

## 7. Replacement behavior

- Kaynak active assignment silinmeden `ended` durumuna getiriliyor.
- Yeni resource için aynı Position/scope temelinde yeni active assignment açılıyor.
- Effective date ve reason zorunlu.
- Eski-yeni kayıtlar çift yönlü provenance ve correlation ile bağlanıyor.
- Repository geçişi native transaction veya standalone compensation ile all-or-nothing davranıyor.
- Rollback davranışı fake repository/UoW testiyle doğrulandı.

## 8. Transfer behavior

- Source assignment ended oluyor.
- Aynı resource ve Position ile target node üzerinde yeni active assignment açılıyor.
- Effective date ve reason zorunlu.
- `TransferFromAssignmentId`, `TransferToAssignmentId`, `TransferReason` ve Position bilgisi history’de korunuyor.
- Conflict ve Position/node-level validation target snapshot üzerinde yeniden çalışıyor.

## 9. Current responsibility behavior

Current sorgusu yalnız şu kayıtları döndürüyor:

- model active
- assignment active
- requested `effectiveAt` tarihini kapsıyor
- soft-deleted/ended değil
- Position, node, BU ve primary filtreleriyle eşleşiyor

Model, node ve resource/person bakışları eklendi. Draft/proposed, future, expired ve ended kayıtlar dışlanıyor.

## 10. History behavior

- Model-level ve resource/person-level history eklendi.
- Ended kayıtlar görünür.
- Replacement ve transfer link/reason/provenance alanları DTO’ya taşınıyor.
- UI Assignment History paneli active ve ended kayıtları birlikte gösteriyor.
- History-preserving command yaklaşımı kritik active güncellemelerin yerini aldı.

## 11. Position validation behavior

- `PositionCode` ve `PositionTitle` zorunlu.
- `PositionId` directory mevcutsa kullanılabilir; planning snapshot için zorunlu değil.
- Deterministic policy seam:
  - Medical Representative → zone/microzone
  - Area Manager → area
  - Regional Manager → region
  - Product Manager → BU-wide/nodeless
  - HOC/Commercial Manager → broad/model-wide
- Bilinmeyen fakat eksiksiz snapshot draft planning için uyarıyla kabul ediliyor.
- Bilinmeyen veya doğrulanmamış operational policy activation/create sırasında fail-closed.
- Policy kaynağı `fu04a-deterministic-position-policy-v1` olarak açıkça işaretlendi.

## 12. Conflict/override behavior

- Aynı node + Position + BU + overlapping period + primary duplicate: daima 409.
- Bu exact duplicate override ile aşılamıyor.
- Aynı resource + Position + farklı BU primary overlap: varsayılan 409; yalnız izinli override source + reason ile aşılabiliyor.
- Aynı resource + Position + aynı BU üzerinde birden fazla primary node: warning.
- Non-primary kayıtlar primary exclusivity guard’dan muaf.
- Conflict report artık uygun senaryolarda warning üretiyor.

## 13. Contract flags

`true` olarak eklendi:

- `supportsResourceAssignmentLifecycle`
- `supportsResourceReplacement`
- `supportsResourceTransfer`
- `supportsCurrentResponsibility`
- `supportsPositionBasedResourceAssignment`

Mevcut `supportsResourceAssignments` ve `supportsAccountAssignmentApply` korunuyor; `supportsWorkflowActivation` değiştirilmedi ve `false`.

## 14. UI summary

- Position label/snapshot alanları
- Planned / Active / Ended badge’leri
- Draft için “Planning only / Not operational” uyarısı
- Active create/end/replace/transfer action’ları
- SweetAlert2 effective date + zorunlu reason akışları
- Current Responsibility paneli
- Assignment History paneli
- Replacement/transfer provenance gösterimi
- EN/TR/FR/ES/RU/ZH/AR RESX parity
- Frontend proxy current/history çağrıları dahil gateway-only

## 15. Tests

Son doğrulama:

- CRM Application Tests: **343 passed, 5 skipped, 0 failed; total 348**
- İlk tam tekrar sırasında FU04A dışındaki mevcut PII masking testi bir kez kararsız düştü; tekil tekrar geçti ve ikinci tam paket tamamen geçti.
- CRM API build: **PASS**, 0 error, FU05 Account assignment dosyalarında mevcut 2 nullable warning.
- Diten.Web build: **PASS**, 0 error, mevcut 14 nullable warning.
- `resource-assignments.js` syntax check: **PASS**
- TerritoryManagement modül-geneli DataTable verifier: **73 PASS / 18 FAIL**. Fail’ler FU04A Resource Assignments yüzeyine özgü değil; mevcut compact/offcanvas/bulk/quick-view şablon borçlarıdır ve bu scope’ta değiştirilmedi.

FU04A test kapsamı:

- activation transition ve conflict fail-closed
- proposed/current ayrımı
- active create/end
- replace/transfer success, reason, provenance ve rollback
- Position field/policy kuralları
- exact duplicate/cross-BU override/non-primary/multi-node warning
- current/history/effectiveAt
- contract flags

## 16. Live smoke

Chrome’daki mevcut authenticated tenant oturumu ve yalnız Gateway/Frontend yüzeyi kullanıldı.

1. Draft `SETONDA-AZ` modelinde proposed assignment oluşturuldu:
   - Ayşe FU04A Smoke
   - `medical-representative`
   - `AZ-Z-SABIRABAD`
   - `alpha`
2. UI Planned ve planning-only uyarısını gösterdi; Current Responsibility boştu.
3. Model aktive edildi; assignment Active oldu.
4. Current Responsibility paneli Ayşe + Position + node bilgisini gösterdi.
5. Active `DENEME` modelinde Ayşe için regional-manager/Marmara assignment oluşturuldu.
6. Ayşe → Mehmet replacement yapıldı; history Ayşe Ended, Mehmet Active ve replacement reason gösterdi.
7. Mehmet Marmara → Karadeniz transfer edildi; current panel target node’u, history eski ve yeni kayıtları gösterdi.
8. Frontend current/history proxy değişiklikleri son reload sonrasında canlı sayfada yeniden doğrulandı.
9. Contract endpoint Gateway üzerinden 200 döndü; flag değerleri contract testleriyle ayrıca doğrulandı.

Live conflict/override mutasyonu tekrarlanmadı; aynı davranış unit/integration-style handler testlerinde doğrulandı. Smoke kayıtları yalnız MOD-0151 resource assignments alanında oluşturuldu.

## 17. Guard checks

- Account ve Contact master mutation: yok
- FU05 AccountTerritoryAssignment apply/history değişikliği: yok
- Workflow/evidence/import-export/visit/route/brand/product eklemesi: yok
- Hard delete / `DeleteOne`: yok
- Mongo hand-edit: yok
- Gateway route değişikliği: yok
- RBAC seed/grant ve forbidden permission: yok
- Frontend direct `5061`: yok; tek eşleşme controller açıklama yorumudur
- Frontend `TenantId` payload: yok
- `crm.territory.delete` / `crm.micro-zone.manage`: eklenmedi

## 18. Created/updated files

Backend:

- `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/TerritoryResourceAssignment.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ITerritoryResourceAssignmentRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ITerritoryActivationUnitOfWork.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/TerritoryResourceAssignmentContracts.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/TerritoryPositionPolicy.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/TerritoryResourceAssignmentValidation.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/TerritoryResourceConflictEngine.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/Handlers/TerritoryResourceAssignmentHandlers.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/Handlers/TerritoryResourceAssignmentQueryHandlers.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/Models/Handlers/TerritoryLifecycleHandlers.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/Contract/TerritoryContractDto.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/TerritoryResourceAssignmentRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/TerritoryActivationUnitOfWork.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/TerritoryModelsController.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/TerritoryResourcesController.cs`

Frontend:

- `frontend/Diten.Web/Controllers/CRM/TerritoryManagementController.cs`
- `frontend/Diten.Web/Models/CRM/TerritoryViewModels.cs`
- `frontend/Diten.Web/Views/CRM/TerritoryManagement/ResourceAssignments.cshtml`
- `frontend/Diten.Web/Views/CRM/TerritoryManagement/_ResourceAssignments.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/CRM/TerritoryManagement/resource-assignments.js`
- `frontend/Diten.Web/Resources/Views/CRM/TerritoryManagement/TerritoryManagementResources.{en,tr,fr,es,ru,zh,ar}.resx`

Tests/evidence:

- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/FakeTerritoryInfrastructure.cs`
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryLifecycleTests.cs`
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryResourceAssignmentTests.cs`
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryResourceAssignmentFu04ATests.cs`
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryContractTests.cs`
- `docs/audits/mod-0151-fu04a-resource-assignment-lifecycle-replacement-operational-visibility-implementation-2026-07-30.md`

## 19. Final verdict

**PARTIAL — FU04A core/runtime PASS; external hardening dependencies remain.**

Position-based lifecycle, proposed → active transition, active create/end, replacement, transfer, current responsibility, history, conflict/override, UI, build, tests ve gateway-only smoke çalışıyor.

PARTIAL gerekçeleri:

1. Canonical Position Directory policy metadata henüz runtime authority olarak erişilebilir değil; açıkça işaretlenmiş deterministic Position policy seam kullanılıyor.
2. Yerel Mongo standalone topolojisinde native transaction mümkün değil; compensating rollback çalışıyor. Kesin crash-atomicity için deployment Mongo replica set/mongos olmalı.
3. TerritoryManagement modül-geneli DataTable verifier’da FU04A dışı 18 mevcut şablon borcu bulunuyor.
4. Live conflict/override mutasyonu bu turda tekrarlanmadı; otomatik test kapsamı PASS.

## 20. Next recommended prompt

`MOD-0151 FU06 — Workflow Approval + Controlled Activation`

