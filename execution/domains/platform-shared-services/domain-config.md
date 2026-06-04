# Platform & Shared Services — Domain Config

> Bu dosya domain'in **sınırlarını ve kararlarını** tanımlar. Engineering NASIL kuralları [.antigravity/rules/](../../../.antigravity/rules/)'da; modül envanteri ve MVP scope [docs/platform/master-plan.md](../../../docs/platform/master-plan.md)'dedir.

## Purpose
Platform & Shared Services (PSS) domain'i, Diten ERP vNext ekosistemi için tenant, subscription, kimlik/yetki, audit, document, evidence, secrets ve internal eventing gibi yatay yetenekleri sahiplenir.

## In-Scope Modules

> Wave/öncelik/durum bilgisi için [docs/platform/master-plan.md](../../../docs/platform/master-plan.md) §2. Burada sadece sahiplik listesi.

**Mevcut (yapılmış / kısmi):** MOD-0009-FU01/FU02/FU03 (Tenant Management; canonicalized from MOD-0043/44/46 per DCP-002), CAND-CAP-0002-FU01 (Module Catalog), CAND-CAP-0002-FU02 (Subscription Plan), CAND-CAP-0002-FU03 (Feature Mgmt), CAND-CAP-0002 (Subscription Lifecycle), CAND-CAP-0002-FU05 (Tenant Module Entitlement), MOD-0017-FU01 (Tenant Login & Security), CAND-CAP-0003 (Platform Administrators)

**Planlanmış:** NEW-001 Secrets, MOD-0009 Tenant Lifecycle Events, MOD-0018 RBAC/ABAC Enforcement, MOD-0026 Job Scheduler, MOD-0035 Event Bus, MOD-0027 Notification, MOD-0028 Document Mgmt, MOD-0021 Audit Trail, MOD-0031 Evidence Linking, MOD-0032 Gateway Hardening, MOD-0033 Quota, MOD-0299 Billing, MOD-0041/42 Logging/Alerting

## Out-of-Scope

- MDM (master data) ana veri modelleri → `master-data-management`
- ESBP iş mantığı, KPI/OKR, performans modülleri → `enterprise-strategy-business-performance`
- Tenant-side ERP işlemleri (HR, Finance, CRM, Inventory)
- External provider console'ları (Vault, broker, SIEM ürünleri)

## Domain-Level Repo Scope

- `execution/domains/platform-shared-services/**`
- `services/Diten.AuthService/**`
- `services/Diten.Platform/**`
- `services/Diten.Platform.Common/**`
- `gateway/Diten.ApiGateway/**`
- `frontend/Diten.Web/**` (Platform admin shell modülleri)

## Protected Paths

- `.antigravity/**` (global engineering system — `working-agreement` zorunlu)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN, archive için)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**`
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` (diğer domain'lerin servisleri)

## Ownership Boundaries

- PSS modülleri **cross-tenant katalog** (Tenant, SubscriptionPlan, FeatureDefinition, ModuleCatalogItem, PlatformAdministrator) ve **tenant-scoped platform kayıtları** (TenantSubscription, TenantModuleEntitlement, AuditEvent) sahiplenir.
- Tenant-side iş modülleri PSS'in çıktılarını yalnızca okuyucu olarak tüketir; PSS aggregate'lerini fork etmez.
- `MOD-0024` (Tasks) ile `MOD-0023` (Approvals) sorumlulukları **birleştirilemez** — Tasks asla approval semantics yazamaz.
- `MOD-0028` (Document storage) ile `MOD-0031` (Evidence linking) ayrı SoR'lara sahiptir.

## Runtime Decisions

> Tüm domain modüllerine uygulanır. Engineering detayları için `.antigravity/rules/` linklerine bak.

- **API Gateway:** Ocelot ([gateway/Diten.ApiGateway](../../../gateway/Diten.ApiGateway/), port 5000) — tüm frontend istekleri Gateway üzerinden geçer. MOD-0032 hardening (rate-limit, quota, policy engine) ertelenmiştir. Ref: [.antigravity/rules/ports.md](../../../.antigravity/rules/ports.md), [routes.md](../../../.antigravity/rules/routes.md)
- **Auth:** `Diten.AuthService` merkezi yetkilendirme; JWT + `[HasPermission("Platform.X.Y")]`. Ref: [.antigravity/rules/security-jwt.md](../../../.antigravity/rules/security-jwt.md)
- **Persistence:** MongoDB tek instance, multi-tenant logical isolation (TenantId zorunlu). Ref: [.antigravity/rules/multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md), [mongo-indexing.md](../../../.antigravity/rules/mongo-indexing.md)
- **Event Bus:** In-process MediatR (lightweight internal seam). Cross-service broker ertelenmiştir.
- **Vault:** appsettings + environment variables (thin abstraction). External vault (NEW-001) ertelenmiştir.
- **Workflow (MOD-0023):** Approvals-focused MVP; BPMN motoru ertelenmiştir.
- **Observability:** `ILogger` + correlation ID middleware. External SIEM/APM ertelenmiştir.
- **Lokalizasyon:** Platform tarafı için yalnızca `en` + `tr` (cross-cutting kural; bkz [master-plan §7.15](../../../docs/platform/master-plan.md)).
- **Layout:** Platform admin modülleri `_LayoutPlatformAdmin.cshtml`, tenant modülleri `_LayoutTenantShell.cshtml`. `_Layout.cshtml` FROZEN.

## Domain Bootstrap Notes

- Teknik standartlar [AGENTS.md](../../../AGENTS.md) ve [.antigravity/rules/](../../../.antigravity/rules/) altındaki global dosyalardan devralınır — burada tekrarlanmaz.
- Modül kimliği: yeni ERP product module paketleri registry-controlled `MOD-NNNN-{slug}` formatını kullanır. Tarihsel `PSS-NNN-{slug}` ve diğer legacy kayıtlar migration boyunca korunur; toplu rename yapılmaz.
- Tarihsel `controls/`, `batches/` ve `decisions/` katmanları [archive/domains/platform-shared-services/](../../../archive/domains/platform-shared-services/) altına taşınmıştır; otorite değildir.
