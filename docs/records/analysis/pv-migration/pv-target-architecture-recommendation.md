# PV Target Architecture Recommendation (G + K)

## G. System-of-Record & boundary önerisi

**İlke:** Yatay kabiliyetler (RBAC, Workflow, Audit, Evidence, Notification, Reference) **paylaşımlı platform modüllerinde kalır**; PV modülü bunları yeniden üretmez.

| Object | Proposed SoR module | Legacy owner | ERP-vNext current owner | Conflict | Recommendation |
|---|---|---|---|---|---|
| PV Organization / MAH | Yeni PV domain (org profili) + **MOD-0288** (person/position) | DitenPvOrganization `Organization` | MOD-0288 (done) | Person seam çakışması yok | MAH profili PV'de, kişi/pozisyon MOD-0288'den referans |
| PV System / Safety Database | Yeni PV domain (mantıksal) | appsettings `DatabaseName` | — | "Di10-PV" marka karışıklığı | PV modülü kendi store'u; "validated DB" iddiası ayrı ele |
| Safety Case | **Yeni PV Safety domain** | DitenPvOrganization `SafetyReport` | — | Yok (greenfield) | Case aggregate PV'de |
| Safety Report (submission çıktısı) | PV Safety domain | `SafetyReport` alanları | — | Yok | Case'in alt kavramı |
| Patient | PV Safety domain (Case içinde) | `Patient` | — | Yok | Case aggregate alt varlığı |
| Reporter | PV Safety domain | string | — | Yok | Yapısal Reporter |
| Adverse Event / Reaction | PV Safety domain + **MedDRA** referans | string | — | Yok | Kodlanmış reaksiyon |
| Product involvement | PV Safety domain → **MOD-0290** referans | `GlobalSku`/`ProductDescription` | MOD-0290 (draft) | SoR MDM'de | Product master MDM; case yalnızca referans |
| Regulatory Submission | Yeni **Regulatory Affairs** domain | `RegulatoryReport` | — | Yok | Ayrı submission aggregate |
| Registration (Marketing Authorization) | Regulatory Affairs domain | `MarketingAuthorization` | — | Yok | MA lifecycle aggregate |
| Pharmaceutical Form | **MOD-0048** Reference Data | `TenantPharmaceuticalForm` | MOD-0048 (ready) | Yok | Reference-data SoR |
| Reconciliation record (LCPPV) | PV Safety domain (süreç) | `Lcppv` | — | Yok | Süreç aggregate; DB-mutabakatı değil |
| Case document | **MOD-0028** Document/Evidence | local disk | MOD-0028 | Legacy disk çakışması | DocMgmt SoR; disk RETIRE |
| Workflow instance | **MOD-0023** Workflow | (yok) | MOD-0023 | Yok | Workflow motoru |
| Audit event | **MOD-0021** Audit | (yok) | MOD-0021 | Yok | Audit servis |
| Permission | **MOD-0018** RBAC | `User.RoleIds` (uygulanmıyor) | MOD-0018 | Legacy auth çakışması | Merkezi RBAC; legacy auth RETIRE |
| Notification | **MOD-0027** Notification | Gmail/SMTP | MOD-0027 | Yok | Notification servis |
| Reference data | **MOD-0048** | DitenPvLookup/Tenant | MOD-0048 | DitenPvLookup RETIRE | Merkezi reference |

## K. Hedef mimari

### Önerilen domain boundary
İki bounded context öner (legacy'nin 5-servis parçalanması **teknik parçalanmadır, gerçek context değildir**):

1. **Pharmacovigilance (Safety) domain** — Safety Case aggregate (intake→assessment→follow-up→closure), LCPPV reconciliation süreci, sinyal/literatür (ileri faz).
2. **Regulatory Affairs domain** — Marketing Authorization (Registration) lifecycle, Authority Submission, regulatory task/tracker.

> **DitenPvLookup / DitenPvUser / DitenPvTenant ayrı servis olmamalı:** Lookup→MOD-0048, User/Auth→AuthService+MOD-0018, Tenant→MOD-0009. Bunlar legacy teknik parçalanmadır; ERP-vNext'te ayrı PV servisi olarak yeniden yaratılmamalı. **DitenPvSurvey** yalnızca LCPPV için genel anket — PV'ye gömülü süreç veya paylaşımlı survey yeteneği olabilir.

### Aggregate'ler (Safety)
`SafetyCase` (root) → `CaseIdentity` (immutable case number) · `PatientInfo` · `Reporter` · `ProductInvolvement[]` (MOD-0290 ref) · `Reaction[]` (MedDRA ref) · `Assessment` (seriousness+expectedness+causality, kriterli) · `FollowUp[]` · `CaseVersion[]` · `SubmissionPlan`.

### API grupları
`/safety/cases` (CRUD + lifecycle transitions), `/safety/cases/{id}/followups`, `/safety/cases/{id}/versions`, `/regulatory/authorizations`, `/regulatory/submissions`, `/safety/reconciliations`.

### Event'ler
`CaseCreated`, `CaseAssessed`, `SeriousnessDetermined`, `FollowUpAdded`, `CaseVersioned`, `SubmissionDue`, `SubmissionSent`, `MAStatusChanged` → **MOD-0021 audit + MOD-0027 notification** tüketir. (Legacy'de event YOK; bu yeni.)

### Workflow / permissions / audit / evidence / notification
- Workflow: case lifecycle + submission approval → **MOD-0023**.
- Permissions: `pv.case.*`, `regulatory.ma.*` → **MOD-0018** (tenant-scoped JWT).
- Audit: her mutasyon → **MOD-0021**.
- Evidence: ekler → **MOD-0028/0029** (disk DEĞİL).
- Notification: deadline/submission → **MOD-0027 + MOD-0026 scheduler**.

### Product/registration ownership
Product/SKU SoR = **MDM/MOD-0290**; case yalnızca referans (local kopya YOK).

### Tenant/company izolasyonu
JWT tenant-claim tabanlı (host-based RETIRE); reads `X-Tenant-Id`/token ile.

### Reporting/read model · import/migration · observability · retention · validation
- Read model: PV KPI (case counts, overdue submissions) ayrı projeksiyon.
- Migration: idempotent Mongo→ERP (ExternalReference natural-key).
- Observability: MOD-0041.
- Retention/archive: politika tanımı (P2).
- **Validation strategy:** GxP için IQ/OQ/PQ + controlled release + traceability — kod varlığı validasyon kanıtı DEĞİL.
