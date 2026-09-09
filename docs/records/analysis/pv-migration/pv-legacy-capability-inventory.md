# PV Legacy Capability Inventory (A + B + C)

> Salt-okuma analizi. Her satır dosya-yolu kanıtıyla. Runtime smoke yapılmadı → "kod-yolu kanıtı".

## A. Proje envanteri

| Alan | Bulgu (kanıt) |
|---|---|
| Teknoloji | .NET 8 (`net8.0`), C#, ASP.NET Core Web API × 5; ASP.NET Core MVC frontend × 1 |
| Proje tipi | Clean Architecture katmanları: `*.Domain / *.Application / *.Infrastructure / *.Persistence / *.Core / *.<Service>` |
| Bağımlılıklar | MediatR (CQRS), AutoMapper, MongoDB.Driver, Flurl.Http, Newtonsoft.Json |
| Authentication | **Yok** (endpoint seviyesinde). `UserService` JWT üretir (`AuthController`) ama diğer servisler doğrulamaz. Kanıt: `OrganizationService/Program.cs` `UseAuthorization()` var, `UseAuthentication()`/JWT **yok** |
| Authorization | **Yok** (tek `[Authorize]`: `DitenPvUser/.../EmailController.cs`). `User.RoleIds` modeli var, veri API'lerinde uygulanmıyor |
| Tenant/company izolasyonu | **Host-based** (`TenantResolutionMiddleware.cs`: `Request.Host.Host` → `Tenant.Domain/SubDomain` → `HttpContext.Items["TenantId"]`) |
| Veritabanı | MongoDB `mongodb://localhost:27017`; DB'ler: `PvOrganization`,`PvTenant`,`PvUser`,`PvLookup`,`PvSurvey`. SQL Server bağlantı string'i var ama `DatabaseType=MongoDb` (ölü) |
| Config | `appsettings.json` (bağlantı + `DatabaseName` + `DatabaseType`) |
| Background jobs | **Yok** (`Hangfire/BackgroundService/IHostedService` = 0) |
| Message/event | **Yok** (`RabbitMQ/Kafka/MassTransit/IEventBus` = 0). Entegrasyon senkron HTTP (Flurl) |
| Dosya/doküman | Servis diskine (`wwwroot/SafetyReport`), `Document` entity (File/FilePath/FileSize) |
| Audit/logging | **Audit trail yok** (`AuditTrail/AuditLog` = 0). Sadece `BaseEntity.CreatedBy/ModifiedBy` + `ILogger` |
| Notification/email | `DitenPvUser`: Gmail API (Google) + `System.Net.Mail` (`ReadEmailHandler`). Merkezi notification servisi yok |
| Lookup/reference | `DitenPvLookup` yalnızca `Country`; tenant-scoped reference `DitenPvTenant` (Authority, ActiveIngredient, Brand, PharmaceuticalForm, Country) |
| Workflow/state | **Motor yok**; enum durumlar manuel (`SafetyReportStatus`, `MaStatus 0-5`, `RegulatoryReportTask.StatusId 1-3`) |
| Import/export | Belirgin toplu import/export bulunamadı (kanıtlanamadı) |
| Raporlama | Yapısal read-model/KPI motoru yok; liste ekranları DataTables |
| UI | Sneat/Bootstrap MVC; controller'lar `return View()`, veri `Pagejs/**` fetch |
| Validation | Minimal null/boş kontrolü (ör. `CreateSafetyReportHandler`: tenant/CountryId/GlobalSkuId → 409). Yapısal PV validasyonu yok |
| Test | **Tek proje**: `Diten.Pv.TenantService.Tests` (reference-data). Çekirdek PV testsiz |
| Dış entegrasyon | Google/Gmail (`ApplicationName="Di10-PV"`); gateway; başka yok |
| Hardcoded | `http://localhost:5000` (TenantAPIs/UserAPIs); `CORS AllowAll` |
| Mimari borç | Host-based tenant, auth boşluğu, monolitik god-entity, testsizlik, senkron kuplaj, local-disk ek |

## B. Uçtan uca akışlar (kod-yolu)

### B1. Safety Report intake — PROVEN persistence
| Adım | Kanıt |
|---|---|
| Aktör | PV kullanıcısı |
| UI | `Views/SafetyReport/AddSafetyReport.cshtml` (`@section Scripts` → `Pagejs/SafetyReport/AddSafetyReport.js`) |
| Frontend | `AddSafetyReport.js:679` `POST .../PvOrganization/SafetyReport/CreateSafetyReport` (JSON) |
| Endpoint | `SafetyReportController.CreateSafetyReport([FromBody] CreateSafetyReportCommand)` |
| Application | `CreateSafetyReportHandler.Handle` → AutoMapper → `SafetyReport` |
| Persistence | `_repository.Create(safetyReport)` → Mongo `PvOrganization` |
| Validation | tenant/CountryId/GlobalSkuId boşsa 409; başka yok |
| Authorization | **Yok** |
| Audit | **Yok** (yalnızca CreatedDate/By set) |
| Notification | **Yok** |
| Workflow | **Yok** (SafetyStatus=Pending default) |
| Çıktı | 201 + yeni `Id` |
| Ek | `CreateSafetyReportDocuments` → `wwwroot/SafetyReport` disk + `Document` koleksiyonu |

### B2. Safety Report list (tenant filtre)
`GetSafetyReports` → `tenantId = _tenantAPIs.GetTenantId()` → `_repository.GetAll(x => x.Status && x.TenantId==tenantId)` + country/sku/patient/user/organization zenginleştirme. **İzolasyon host-based tenant'a bağlı.**

### B3. LCPPV Monthly Reconciliation (= anket, DB-mutabakatı değil)
`Views/PvSystem/AddLcppv.cshtml` → `Pagejs/PvSystem/AddLcppv.js` → `GetQuestions` + `CreateLcppv`/`CreateLcppvDocuments` → `LcppvMonthlyReconcilationController`. "Reconciliation" burada **aylık soru-cevap formu**dur.

### B4. Marketing Authorization (Registration)
`MaController` + `MarketingAuthorization` entity (QPPV user, PSMF number, ATC, `MaStatus 0-5`, `MaDetail`, `MarketingAuthorizationReRegistration`). CRUD PROVEN (kod-yolu).

### B5. Regulatory Report + Task board
`RegulatoryReportController` + `RegulatoryReportTaskController` → `RegulatoryReport` (authority publish link/summary) + `RegulatoryReportTask` (status/priority/assignee/parent) + comments. **Hafif görev takip**; otorite E2B gönderimi değil.

## C. Kabiliyet matrisi

| # | Capability | Legacy status | Evidence | Runtime proof | Business importance | Technical debt |
|---|---|---|---|---|---|---|
| 1 | PV tenant/org yönetimi | IMPLEMENTED_AND_PROVEN | `DitenPvTenant`, `Organization` entity | kod-yolu | Yüksek | Host-based izolasyon |
| 2 | PV user/role | PARTIAL | `User.RoleIds`, `AuthController` | kod-yolu | Yüksek | Rol API'de uygulanmıyor |
| 3 | PV system / safety DB tanımı | CONFIGURATION_ONLY | `appsettings DatabaseName` | — | Orta | "Di10-PV" marka, DB değil |
| 4 | Product & pharmaceutical form | IMPLEMENTED_AND_PROVEN | `GlobalSku`, `TenantPharmaceuticalForm` | kod-yolu | Yüksek | SoR dağınık |
| 5 | Registration / regulatory affairs | PARTIAL | `MarketingAuthorization`, `RegulatoryReport` | kod-yolu | Yüksek | Task board seviyesi |
| 6 | Safety report / AE intake | IMPLEMENTED_AND_PROVEN | `SafetyReport` + Create/Update handler | kod-yolu | Kritik | God-entity |
| 7 | Patient | PARTIAL | `Patient : BaseEntity` | kod-yolu | Yüksek | String `PatientId` bağı |
| 8 | Reporter | UI_ONLY/DATA_MODEL_ONLY | `SafetyReport.Reporter` (string) | kod | Yüksek | Yapısal değil |
| 9 | Suspect/concomitant products | PARTIAL | `ProductDescription`, `Patient.ConcomitantMedications: List<string>` | kod | Yüksek | Yapısal değil |
| 10 | Event/reaction | PARTIAL | `SafetyReport.AdverseReaction` (string) | kod | Kritik | Kodsuz serbest metin |
| 11 | Seriousness | PARTIAL | `isSerious` (bool) | kod | Kritik | Kriter yok |
| 12 | Expectedness | PARTIAL | `isUnexpected` (bool) | kod | Kritik | Kriter yok |
| 13 | Causality | PARTIAL | `CausalityAssessment` enum | kod | Kritik | Tek boyut |
| 14 | Case narrative | UI_ONLY | `SafetyComments`/`Summary` (string) | kod | Yüksek | Yapısal değil |
| 15 | Attachments/documents | IMPLEMENTED_AND_PROVEN | `CreateSafetyReportDocumentsHandler` (local disk) | kod-yolu | Yüksek | Disk depolama |
| 16 | Case versioning | DATA_MODEL_ONLY | `Version` (string) | kod | Yüksek | Gerçek versiyon yok |
| 17 | Follow-up | UI_ONLY | `FollowUpTracker` (string alan); sınıf persist edilmiyor | kod | Yüksek | Yapısal değil |
| 18 | Duplicate detection | NOT_FOUND | `IsDuplicate` (manuel bool) | kod | Yüksek | Otomatik yok |
| 19 | Case assignment | IMPLEMENTED_AND_PROVEN | `AssignedTo/Reviewer/Assessor` alanları | kod-yolu | Orta | Serbest alanlar |
| 20 | Workflow/review/approval | NOT_FOUND | enum durumlar, motor yok | — | Kritik | Manuel |
| 21 | Reconciliation | PARTIAL | `LcppvMonthlyReconcilation` (anket) | kod-yolu | Orta | DB-mutabakatı değil |
| 22 | Authority reporting | UI_ONLY | `RegulatoryReport` (link/özet) | kod | Kritik | E2B/gateway yok |
| 23 | Reporting & KPIs | NOT_FOUND | read-model yok | — | Orta | — |
| 24 | Audit trail | NOT_FOUND | `AuditTrail/AuditLog`=0 | — | Kritik (GxP) | Yok |
| 25 | E-signature | NOT_FOUND | `ESignature`=0 | — | Kritik (GxP) | Yok |
| 26 | Notifications | PARTIAL | Gmail/SMTP (`UserService`) | kod | Orta | Servis değil |
| 27 | Lookup/reference | PARTIAL | `DitenPvLookup`=`Country`; `DitenPvTenant` reference | kod-yolu | Yüksek | Dağınık |
| 28 | Import/export | NOT_FOUND | bulunamadı | — | Orta | — |
| 29 | Integration | PARTIAL | Google/Gmail, gateway | kod | Orta | Senkron kuplaj |
| 30 | Data retention/archive | NOT_FOUND | soft-delete (`Status`) var, politika yok | kod | Yüksek | Yok |
| 31 | Security/tenant isolation | PARTIAL | host-based middleware | kod-yolu | Kritik | Zayıf |
| 32 | Validation/data quality | PARTIAL | null kontrolleri | kod | Yüksek | Minimal |
| 33 | Search/list/filter | IMPLEMENTED_AND_PROVEN | DataTables + Get*Query | kod-yolu | Orta | — |
| 34 | Dashboard | NOT_FOUND (PV) | — | — | Düşük | — |
| 35 | MedDRA | NOT_FOUND | 0 eşleşme | — | Kritik | Yok |
| 36 | WHO Drug | NOT_FOUND | 0 eşleşme | — | Kritik | Yok |
| 37 | Signal management | NOT_FOUND | `SignalDetectionParticipation` (bool) | kod | Yüksek | Sadece bayrak |
| 38 | Literature monitoring | UI_ONLY | `SafetyReport.Literature*` alanları | kod | Orta | İzleme değil |
| 39 | Compliance evidence | NOT_FOUND | validation/IQ-OQ-PQ yok | — | Kritik | Yok |
