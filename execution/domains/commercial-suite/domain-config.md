# Commercial Suite (CRM + O2C) — Domain Config

> Bu dosya domain'in **sınırlarını ve kararlarını** tanımlar. Engineering NASIL kuralları
> [.antigravity/rules/](../../../.antigravity/rules/)'da; modül envanteri ve wave sıralaması
> [master-development-plan.md](../../portfolio/master-development-plan.md)'de; permission convention
> [PKS-001](../../../.antigravity/rules/permission-key-standard.md)'de. Burada tekrarlanmaz.

## Purpose

Commercial Suite domain'i, ERP-vNext ticari/müşteri yaşam döngüsünü (CRM Core + Sales + Marketing + CPQ + Service +
Order-to-Cash bridge + Business Development) sahiplenir. Çekirdek generic CRM + pharma field-force uzantısı stratejisiyle
tasarlanır. Bu bir governance scaffold'dur; hiçbir runtime servis/entity/endpoint yaratmaz.

## In-Scope Modules

> Wave/öncelik/durum bilgisi için [master-development-plan.md](../../portfolio/master-development-plan.md) ve
> [crm-build-lanes.md](crm-build-lanes.md). Burada sadece sahiplik + Blueprint canonical ad.

**CRM Core:** MOD-0149 Customer 360 / Account Hierarchy · MOD-0150 Contact & Relationship Management · MOD-0151 Territory Management
**Sales:** MOD-0152 Lead Management · MOD-0153 Opportunity & Pipeline Management · MOD-0154 Forecasting & Quotas · MOD-0155 Field Sales / Visit Planning
**Marketing:** MOD-0164 Consent & Preference Management · MOD-0165 Campaign Management · MOD-0166 Journeys & Automation · MOD-0167 Segmentation / CDP
**CPQ & Pricing:** MOD-0156 Price Lists & Discount Guardrails · MOD-0157 Quote Generation · MOD-0158 Quote-to-Contract Handoff · MOD-0159 Product Configuration
**Service:** MOD-0160 Case Management · MOD-0161 SLA Routing & Escalation · MOD-0162 Knowledge Base · MOD-0163 Customer Satisfaction Loop
**Order-to-Cash (Bridge):** MOD-0168 Order Capture · MOD-0169 Billing & Invoicing · MOD-0170 Returns (RMA) · MOD-0171 Disputes / Claims · MOD-0172 Allocation & ATP/CTP
**Business Development & Partnerships:** MOD-0282 Partner & Alliance Management · MOD-0283 Pursuit & Proposal Management (RFP/RFI) · MOD-0284 Deal Desk & Commercial Approvals

> **Not (SoR sınırı):** MOD-0169 Billing & Invoicing, MOD-0170/0171/0172 O2C "bridge" modülleridir. Bunların bir kısmı
> Finance/Order-Management domain'i ile paylaşımlı SoR gerektirebilir; kesin sahiplik EA refinement'e tabidir
> (bkz. [crm-sor-boundary.md](crm-sor-boundary.md)). Bu scaffold yalnız CRM tarafındaki commercial-front sahipliğini varsayar.

## Out-of-Scope

- Country / City / District ve tüm generic lookup/reference set → **MOD-0048 Reference Data**.
- Employee / Sales Rep master, Position/Org master → **MOD-0288 Organization, Person & Position Directory** (HR/Org).
- Brand / Product / SKU master → **MDM / Product** (MOD-0220 ailesi + gelecekteki product master).
- Auth / Role / Permission evaluation engine → **MOD-0018 / Diten.AuthService** (CRM yalnız tüketir, yeni RBAC kurmaz).
- Navigation loader/engine → tenant shell + (gelecekte) ModulePageDescriptor loader.
- Gateway global routing policy → `integration-agent` (ocelot.json).
- HCP identity SoR ambiguity (doktor/eczacı kimliği CRM mi MDM mi) → **EA-TBD** open question.

## Domain-Level Repo Scope (gelecekte, pack onayı sonrası)

- `execution/domains/commercial-suite/**` (bu scaffold — bugün geçerli)
- `services/Diten.CrmService/**` (henüz yok; ilk module pack onayına kadar oluşturulmaz)
- `frontend/Diten.Web/**` (yalnız CRM tenant-shell modül surface'leri)
- `gateway/Diten.ApiGateway/**` (yalnız `integration-agent` üzerinden CRM route ekleme)

## Protected Paths

- `.antigravity/**` (global engineering system — working-agreement zorunlu)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` (yalnız Adım-9 permission-guard'lı `<li>` — bu task'ta değişmez)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**` (FROZEN — legacy CRM buradan taşınmaz)
- `gateway/.../ocelot.json` (yalnız `integration-agent`)
- Diğer domain servisleri: `services/Diten.AuthService/**`, `services/Diten.Platform/**`, `services/Diten.MdmService/**`, `services/Diten.HcmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`

## Ownership Boundaries (özet — tam matris [crm-sor-boundary.md](crm-sor-boundary.md))

- **Consent** MOD-0150 (Contact) içinde sahiplenilmez → **MOD-0164** sahiplenir.
- **Segment / TargetCustomer** MOD-0165 (Campaign) içinde sahiplenilmez → **MOD-0167** sahiplenir.
- **Visit / MicroTarget** MOD-0149 (Account) içine gömülmez → **MOD-0155** sahiplenir.
- **MicroZone** yapısı MOD-0151'de tanımlanır; MOD-0155 rota planlama için **tüketir**.
- **Campaign execution** MOD-0165'e; **Journey automation** MOD-0166'ya aittir.
- Lead/Opportunity generic CRM çekirdeğidir; pharma field-sales içine gömülmez.
- CRM, MOD-0048/MOD-0288/MDM/MOD-0018 aggregate'lerini **fork etmez**, yalnız okuyucu olarak tüketir.

## Runtime Decisions (domain geneli; global kurallar devralınır)

- **Persistence:** MongoDB tek instance, multi-tenant logical isolation, `TenantId` **zorunlu**, cross-tenant 404.
  Ref: [.antigravity/rules/multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md).
- **Auth:** `Diten.AuthService` merkezi; JWT + `[HasPermission("crm.resource.action")]` — PKS-001 lowercase-dotted.
  CRM yeni RBAC engine kurmaz. Ref: [PKS-001](../../../.antigravity/rules/permission-key-standard.md).
- **Permission namespace:** `crm.*` (PKS-001 §4'te future business-domain olarak önceden ayrılmış) + `commercial.*`
  (CPQ/Service/O2C/BizDev; namespace reservation EA onayına tabi). Detay: [crm-rbac-integration-plan.md](crm-rbac-integration-plan.md).
- **Data scope (ABAC):** Territory / MicroZone / team scoping **MOD-0018-FU15 Real DataScopeResolver**'a bağlıdır ve o
  bugün `planned/reserved`. CRM field-force scoping **bu bağımlılık karşılanana kadar bloklu** (bkz. RBAC planı §7).
- **Entitlement bridge:** Tenant module entitlement → permission köprüsü mevcut (EntitlementPermissionSyncService /
  ModulePermissionResolver / ITenantEntitlementClient). CRM modülleri bu köprüyü tüketir.
- **API Gateway:** Ocelot (port 5000); tüm frontend istekleri Gateway üzerinden. CRM route ekleme yalnız `integration-agent`.
- **Layout:** CRM tenant modülleri `_LayoutTenantShell.cshtml`; `_Layout.cshtml` FROZEN.
- **Navigation:** Bugün genel modüller için dinamik loader **yok**; menü `_LayoutTenantShell.cshtml`'e elle
  `@if (Perms.Has("crm.*.read"))` guard'lı `<li>` ile eklenir (how-to-add-a-module Adım 9).
- **Localization:** Global 7-dil standardı (`.resx` + `window.L10n` bridge) devralınır.
- **DataTable:** v2 kontratı + Golden Slim/Compact seçimi zorunlu.

## Domain Bootstrap Notes

- Teknik standartlar AGENTS.md ve `.antigravity/rules/`'dan devralınır — burada tekrarlanmaz.
- Modül kimliği: registry-controlled `MOD-NNNN-{slug}`; her ID DCP-002 gate'inden geçer; yeni MOD uydurulmaz.
- Blueprint'te olmayan bir yetenek gerekirse `CAND-CAP-####` governance candidate kullanılır; runtime literal'a yazılmaz.
