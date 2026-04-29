# Platform & Shared Services — Domain Config

## Purpose
Platform & Shared Services (PSS) domain'i, Diten ERP vNext ekosistemi için kimlik yönetimi, yetkilendirme, iş akışı, denetim izi ve entegrasyon gibi yatay yeteneklerin merkezi yönetimini ve standartlarını tanımlar.

## In-Scope Modules
- `MOD-0014-module-boundary-registry` — Module Boundary Registry / Global Platform Catalog Foundation
- `MOD-0012-secrets-configuration-vault` — Secrets & Configuration Vault
- `MOD-0018-rbac-abac-authorization` — RBAC / ABAC Authorization
- `MOD-0021-audit-trail-service` — Audit Trail Service
- `MOD-0023-workflow-designer` — Workflow Designer (Approvals/SLAs/Escalations)
- `MOD-0024-task-checklist-engine` — Task & Checklist Engine
- `MOD-0028-document-management` — Document Management (Templates/Versioning)
- `MOD-0031-evidence-linking-service` — Evidence Linking Service
- `MOD-0032-api-gateway` — API Gateway
- `MOD-0035-event-bus-message-queue` — Event Bus / Message Queue
- `MOD-0037-integration-monitoring` — Integration Monitoring & Reconciliation
- `MOD-0041-logging-monitoring` — Logging & Monitoring
- `MOD-0042-alerting-incident-runbooks` — Alerting & Incident Runbooks

## Out-of-Scope
- MDM (Master Data Management) ana veri modelleri
- ES&BP (Enterprise Strategy & Business Performance) iş mantığı ve performans modülleri
- Domain dışı servislerin iç detayları ve veri saklama yapıları

## Ownership Boundaries
- PSS modülleri, platform genelinde paylaşılan teknik altyapı nesnelerini (Role, Permission, AuditEvent, Secret vb.) sahiplenir.
- Diğer domain'ler bu nesneleri merkezi servisler üzerinden tüketir.
- Domainlar arası veri izolasyonu `TenantId` bazlı sağlanır.

## Shared Dependencies
- Kimlik yönetimi için JWT tabanlı RBAC yapısı.
- Loglama için `ILogger` ve merkezi OpenTelemetry temelleri (MVP aşamasında hafifletilmiş).
- MongoDB multi-tenant veri saklama modeli.
- MediatR tabanlı iç olay (internal event) yönetimi.

## Domain-Level Repo Scope
- `execution/domains/platform-shared-services/**`
- `services/Diten.AuthService/**`
- `services/Diten.Platform/**`
- `gateway/Diten.ApiGateway/**`
- `frontend/Diten.Web/**` (Platform modülleri için)

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `services/Diten.MdmService/**`
- `services/Diten.EnterpriseStrategyService/**`

## Runtime Decisions
- **API Gateway:** Ocelot (Port 5000) zorunludur. Tüm istekler buradan geçer.
- **Auth:** `DitenAuthService` merkezi yetkilendirme otoritesidir.
- **Workflow:** Başlangıç seviyesinde onay odaklı MVP, BPMN motoru ertelenmiştir.
- **Logging:** CorrelationId takibi tüm servislerde zorunludur.

## Domain Bootstrap Notes
- Teknik standartlar `AGENTS.md` ve `.antigravity/rules/` altındaki global dosyalardan devralınır.
- `MOD-XXXX-slug` kimlik yapısı modül paketlerinde birincil referanstır.
