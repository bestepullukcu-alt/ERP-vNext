# Commercial Suite — MOD-0018 RBAC / ABAC Integration Plan

> **Bu plan yeni bir RBAC sistemi kurmaz.** CRM, mevcut **MOD-0018 / Diten.AuthService / Platform** permission
> altyapısına entegre olur. Otorite: [PKS-001 Permission-Key Standard](../../../.antigravity/rules/permission-key-standard.md).
> Bu doküman governance önerisidir; kod/seed/migration içermez.

## 1. Mevcut RBAC mimarisi (özet)

| Katman | Gerçek durum (koddan) |
|---|---|
| Permission key format | **PKS-001**: `module.resource.action` — lowercase-dotted, hyphen-in-segment, ≥3 segment. `Permission.Key` runtime'da `ToLowerInvariant()` ile zorlanır. |
| Enforcement | Her serviste `PermissionAuthorizationHandler` (exact, **case-sensitive** match) + `[HasPermission("…")]` attribute. `actor_type=platform_admin` bypass. |
| JWT | `permission` claim'i seeded key'lerden gelir (lowercase-dotted). |
| Scope / escalation | `PermissionScope.Tenant` vs `PermissionScope.PlatformAdmin`. Tenant rolleri platform.* alamaz (curated self-service istisnası hariç). |
| Default roller | `DefaultRolePermissionTemplate`: `SuperAdmin` / `Admin` / `Viewer`. Seed (`DataSeeder`) + runtime (`RoleProvisioningService`) tek şablondan beslenir (drift yok). |
| Namespace ownership | Her namespace tek modül sahibidir. **`crm.*` PKS-001 §4'te future business-domain olarak önceden ayrılmıştır.** |
| Entitlement bridge | `EntitlementPermissionSyncService` / `ModulePermissionResolver` / `ITenantEntitlementClient` → tenant module entitlement → permission köprüsü **mevcut**. |
| Data scope (ABAC) | **Real DataScopeResolver = MOD-0018-FU15 = `planned/reserved`** (eski NEW-MOD-0041). Territory/team scoping için gerçek resolver **henüz yok**. |
| Module-page permission validator | `ModulePageDescriptorNormalizer` + `IsCanonicalPermission` (bugün tam-3-segment; PKS-001 ≥3'e genişletme AG-STEP-004B). Testlerde zaten `crm.*` örnekleri var. |
| Frontend visibility | `Perms.Has("…")` helper; menü `<li>` guard'ı. Salt UX — güvenlik backend enforce eder. |

## 2. Permission namespace standardı (normalize edilmiş)

Task'ta önerilen anahtarlar **`.view`** kullanıyordu; PKS-001 action sözlüğü `view → read` (alias) der. Tüm CRM
anahtarları PKS-001'e normalize edildi:

- `view` → **`read`** (Tier-1 canonical)
- `create` / `update` / `delete` / `export` / `import` / `approve` / `activate` → PKS-001 Tier-1/Tier-2 aynı kalır
- `qualify`, `convert`, `disqualify`, `change-stage`, `close-won`, `close-lost`, `grant`, `revoke`, `link-account`,
  `assign-rep`, `assign-account`, `submit`, `complete`, `report`, `cancel`, `generate`, `override`, `execute`,
  `publish`, `evaluate`, `pause`, `stop`, `manage` → **Tier-3 domain-owned** (owning module pack'te kayıtlı; segment
  grammar `^[a-z][a-z0-9-]*$` sağlanır)
- `history.view` → **`history.read`** (nested resource path geçerli)

**Namespace kararı:**
- **`crm.*`** — CRM Core / Sales / Marketing / Field Sales (MOD-0149…0155, 0164…0167). PKS-001'de önceden ayrılmış.
- **`commercial.*`** — CPQ / Service / O2C / BizDev (MOD-0156…0163, 0168…0172, 0282…0284). PKS-001 §4'te henüz
  listeli değil → **namespace reservation EA/owner onayına tabi** (blocker). Alternatif: hepsi `crm.*` altında.

## 3. CRM permission listesi (PKS-001 normalize)

**MOD-0149 Account:** `crm.account.read` · `crm.account.create` · `crm.account.update` · `crm.account.delete` · `crm.account.export` · `crm.account.import`
**MOD-0150 Contact/Relationship:** `crm.contact.read` · `crm.contact.create` · `crm.contact.update` · `crm.contact.delete` · `crm.contact.link-account` · `crm.relationship.manage`
**MOD-0151 Territory:** `crm.territory.read` · `crm.territory.create` · `crm.territory.update` · `crm.territory.assign-rep` · `crm.territory.assign-account` · `crm.micro-zone.manage`
**MOD-0152 Lead:** `crm.lead.read` · `crm.lead.create` · `crm.lead.update` · `crm.lead.qualify` · `crm.lead.convert` · `crm.lead.disqualify`
**MOD-0153 Opportunity:** `crm.opportunity.read` · `crm.opportunity.create` · `crm.opportunity.update` · `crm.opportunity.change-stage` · `crm.opportunity.close-won` · `crm.opportunity.close-lost`
**MOD-0154 Forecast/Quota:** `crm.forecast.read` · `crm.forecast.manage` · `crm.quota.read` · `crm.quota.manage` · `crm.quota.approve`
**MOD-0155 Field Sales:** `crm.visit-plan.read` · `crm.visit-plan.create` · `crm.visit-plan.update` · `crm.visit-plan.submit` · `crm.visit-plan.approve` · `crm.visit-plan.cancel` · `crm.visit.read` · `crm.visit.create` · `crm.visit.update` · `crm.visit.report` · `crm.visit.complete` · `crm.visit.cancel` · `crm.route-plan.generate` · `crm.route-plan.override`
**MOD-0164 Consent:** `crm.consent.read` · `crm.consent.grant` · `crm.consent.revoke` · `crm.consent.history.read`
**MOD-0165 Campaign:** `crm.campaign.read` · `crm.campaign.create` · `crm.campaign.update` · `crm.campaign.publish` · `crm.campaign.execute` · `crm.campaign.cancel` · `crm.campaign.results.read`
**MOD-0166 Journey:** `crm.journey.read` · `crm.journey.create` · `crm.journey.update` · `crm.journey.activate` · `crm.journey.pause` · `crm.journey.stop`
**MOD-0167 Segmentation:** `crm.segment.read` · `crm.segment.create` · `crm.segment.update` · `crm.segment.evaluate` · `crm.segment.publish` · `crm.target-customer.manage`

## 4. Commercial adjacent permission listesi (namespace EA onayına tabi)

`commercial.pricing.*` · `commercial.quote.*` · `commercial.product-config.*` · `commercial.case.*` · `commercial.sla.*` ·
`commercial.knowledge-base.*` · `commercial.csat.*` · `commercial.order.*` · `commercial.billing.*` · `commercial.return.*` ·
`commercial.dispute.*` · `commercial.allocation.*` · `commercial.partner.*` · `commercial.pursuit.*` · `commercial.deal-desk.*`
(her biri Tier-1/2/3 aksiyonlarıyla; `read/create/update/delete` + domain aksiyonları — pack'lerde detaylanır).

## 5. Role / Profile mapping (mevcut MOD-0018 modeline)

> Yeni global role sistemi **kurulmaz**. Bunlar mevcut role-permission modeline mapping önerisidir; tenant roller
> platform.* alamaz. `Admin` baseline `DefaultRolePermissionTemplate.AdminModules` curated listesine bağlıdır.

| Role/Profile | Permission Groups | Data Scope | Default Grant? | Yüksek riskli izinler |
|---|---|---|---|---|
| CRM Admin | tüm `crm.*` (+ commercial.* opsiyonel) | tenant-geneli | Hayır (opt-in) | `*.delete`, `micro-zone.manage`, `consent.revoke`, `campaign.execute` |
| CRM Manager | `crm.*` read/create/update + approve | tenant-geneli (org kapsamı) | Hayır | `quota.approve`, `visit-plan.approve` |
| Sales Manager | `crm.lead.*` `crm.opportunity.*` `crm.forecast.*` `crm.quota.*` | kendi ekibi (team scope) | Hayır | `quota.approve`, `opportunity.close-won/lost` |
| Sales Representative / MR | `crm.lead.*` `crm.opportunity.*` `crm.visit*.*` `crm.account.read` | kendi territory/micro-zone | Hayır | `route-plan.override` (verilmez) |
| Field Force Manager | `crm.visit-plan.*` `crm.visit.*` `crm.route-plan.*` `crm.territory.read` | kendi bölge ekibi | Hayır | `visit-plan.approve`, `route-plan.override` |
| Marketing Manager | `crm.campaign.*` `crm.journey.*` `crm.segment.*` | tenant-geneli | Hayır | `campaign.execute`, `journey.activate` |
| Campaign Manager | `crm.campaign.*` `crm.segment.read` | tenant-geneli | Hayır | `campaign.publish/execute` (consent.revoke **YOK**) |
| Consent Administrator | `crm.consent.*` | tenant-geneli | Hayır | `consent.revoke`, `consent.history.read` |
| Read-only CRM Viewer | tüm `crm.*.read` | tenant-geneli (read) | Evet (Viewer benzeri) | yok |

## 6. ABAC / Data Scope Matrix

> **Kritik bağımlılık:** Aşağıdaki data-scope kurallarının gerçek uygulaması **MOD-0018-FU15 Real DataScopeResolver**
> gerektirir; bugün `planned/reserved`. FU15 gelene kadar scope enforcement yalnız permission (coarse) düzeyindedir →
> **field-force ince scoping bloklu.**

| Scenario | Required Permission | Data Scope Rule | Owner Module | Enforcement Layer | Failure Response | Audit |
|---|---|---|---|---|---|---|
| Rep sadece kendi territory/micro-zone account'ları | `crm.account.read` | account.territoryId ∈ rep.assignedZones | MOD-0151/0149 | AuthZ handler + query filter (FU15) | 404 (cross-scope) | Hayır |
| MR sadece atanmış target/account'a ziyaret planı | `crm.visit-plan.create` | target ∈ rep.assignedTargets | MOD-0155/0167 | Handler validation | 403/422 | Evet |
| Manager kendi ekibinin account/visit/opportunity | `crm.*.read` | record.owner ∈ manager.teamMembers | MOD-0153/0155 | Query filter (FU15) | 404 | Hayır |
| Campaign manager publish edebilir, consent revoke edemez | `crm.campaign.publish` (⊅ `crm.consent.revoke`) | permission ayrımı | MOD-0165/0164 | AuthZ handler | 403 | Evet |
| Consent history yalnız yetkiliye | `crm.consent.history.read` | tenant + consent scope | MOD-0164 | AuthZ handler | 403 | Evet |
| Forecast/quota approval role+org kapsamı | `crm.quota.approve` | approver.orgScope ⊇ quota.orgScope | MOD-0154 | Handler + FU15 | 403 | Evet |
| Tenant isolation (her query/write) | (tümü) | TenantId zorunlu | tümü | Persistence filter | 404 | — |
| Cross-tenant ID erişimi | (tümü) | tenant mismatch | tümü | Persistence filter | **404** (standart) | Evet |
| Visit route override ayrı izin | `crm.route-plan.override` | ayrı permission gate | MOD-0155 | AuthZ handler | 403 | Evet |
| MicroZone assignment ayrı izin | `crm.micro-zone.manage` | ayrı permission gate | MOD-0151 | AuthZ handler | 403 | Evet |

## 7. Blocker

- **MOD-0018-FU15 Real DataScopeResolver `planned/reserved`.** Territory/team/micro-zone tabanlı ince data-scope
  (rep kendi bölgesi, manager kendi ekibi) bu resolver olmadan enforce edilemez. CRM foundation permission düzeyinde
  ilerleyebilir; **field-force scoping ve manager-team görünürlüğü FU15'e bağlı**.
- **`commercial.*` namespace reservation** PKS-001 §4'te yok → EA/owner onayı gerekli.
- **AG-STEP-004B** (permission-key migration + validator ≥3 genişletme) tamamlanmadan yeni CRM key'leri PKS-001
  canonical formatta yazılmalı (§9 authoring rule "new code canonical").
