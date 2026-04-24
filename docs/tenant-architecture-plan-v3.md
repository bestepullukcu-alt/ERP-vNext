# ERP-vNext — Tenant-Aware Architecture Plan v3 (Final Foundation)

> Bu versiyon v2 + tüm kabul edilmiş revizyonları + implementation contract seviyesine indirilmiş kritik mekanizmaları içerir. Karar metni değil, uygulama sözleşmesidir.

---

## 1. Hedef Mimari Resmi

Dört katman, sert sınırlar:

**A. Platform Core (cross-cutting, kütüphane)**
Tenant context, auth context, audit context, eventing context, observability. Runtime servis değil; her servise DI ile enjekte edilen building-block.

**B. Backbone Services (platform işleri)**
- `Diten.PlatformService` — tenant registry, lifecycle, config, feature flag, quota
- `Diten.IdentityService` — auth, login, SSO, MFA, tenant membership, role assignment
- `Diten.AuditService` — audit event consumer + immutable store + retention
- `Diten.WorkflowService` — definition + runtime (tek servis, iki modül)
- `Diten.NotificationService` — email/sms/push, tenant-scoped templates
- `Diten.DocumentService` — metadata + blob path + signed URL + classification

**C. Domain Services (ERP iş mantığı, tenant-aware kullanıcı, tenant yöneticisi DEĞİL)**
- `Diten.MdmService`, `Diten.ProcurementService`, `Diten.InventoryService`, `Diten.SalesService`, `Diten.LogisticsService`, `Diten.FinanceService`, `Diten.PlanningService`

**D. Edge / Delivery Layer**
- `Diten.ApiGateway` (Ocelot)
- `Diten.Web` (MVC, mevcut)
- Per-service worker process'leri (`*.Worker`, servisin kendi sahibinde)

---

## 2. Servis Sınırları

### 2.1 PlatformService — internal bounded contexts (zorunlu disiplin)

Tek servis, ama içinde **dört ayrı modül**, her biri ayrı namespace + ayrı handler set'i + ayrı endpoint prefix:

```
Diten.PlatformService.Application/
  TenantRegistry/
    Commands/  Queries/  Handlers/  DTOs/
  TenantConfig/
    Commands/  Queries/  Handlers/  DTOs/
  FeatureFlags/
    Commands/  Queries/  Handlers/  DTOs/
  Quotas/
    Commands/  Queries/  Handlers/  DTOs/
```

**Sınır kuralı (ArchUnit ile zorlanır):**
- TenantRegistry handler'ı FeatureFlags repository'sine erişemez (cross-module reference yasağı).
- Modüller arası iletişim **domain event** ile (`tenant.activated` → FeatureFlags consumer default flag set'ler).
- API endpoint'leri: `/admin/tenants/...`, `/admin/config/...`, `/admin/flags/...`, `/admin/quotas/...` — birbirine karışmaz.

İçine konmayacak: login, domain master data, workflow execution, audit storage, document blob.

DB: `diten_platform`.

### 2.2 IdentityService — User Core / Extension Ayrımı (sert boundary)

**ADR**: Identity user profile ≠ business user profile. Bu cümle değişmez.

**IdentityService içinde tutulan (auth-core minimum):**
```
diten_identity:
  platform_users          # platform admin (tenant context'siz)
  tenant_users            # email, display_name, status, locale, timezone
  tenant_user_memberships # email + tenant_id mapping (aynı email N tenant'ta)
  credentials             # password hash, mfa_secret (encrypted)
  refresh_tokens
  sso_configs             # tenant-scoped IdP config
  roles                   # tenant-scoped role definitions
  role_assignments        # user_id → role_id (tenant-scoped)
```

**Domain servislerinde tutulan (business profile extension):**
```
diten_procurement:
  user_procurement_profiles  # user_id, approval_limit, default_cost_center, supplier_groups
diten_inventory:
  user_inventory_profiles    # user_id, default_plant, default_warehouse, allowed_locations
diten_finance:
  user_finance_profiles      # user_id, posting_authority, sod_role, gl_segment_access
diten_sales:
  user_sales_profiles        # user_id, sales_org, channel, customer_groups
```

**Kural**: IdentityService domain profile'ları bilmez. Domain servis kendi profile'ını yönetir, `user_id` foreign key ile IdentityService'e bağlanır. IdentityService user disable ederse → domain event (`user.disabled`) → domain'ler kendi profile'larını invalidate eder.

### 2.3 Authorization — Building Block, Servis DEĞİL

`Diten.BuildingBlocks.Authorization` her servise DI ile enjekte. Runtime evaluation in-process. (Detaylı cache contract: §10)

### 2.4 AuditService

Async consumer. Producer'lar **outbox pattern** ile event publish eder. (Detaylı contract: §11)

### 2.5 WorkflowService — tek servis, iki modül

```
Diten.WorkflowService.Application/
  Definition/   # template CRUD, version, publish
  Runtime/      # instance start, task assignment, state, SLA
```

Split tetikleyicisi: 3+ servis definition'a okur veya runtime saatte 100K+ instance üretir.

### 2.6 DocumentService

Metadata + path strategy (`{tenant_id}/{entity_type}/{entity_id}/{file_id}`) + signed URL + retention. İçine domain mantığı gömülmez.

### 2.7 Domain Services

Tenant-aware **kullanıcı**:
- Tenant CRUD/suspend/quota yönetmez
- Kendi DB'sinin sahibi, başka domain DB'sine bağlanmaz
- Cross-domain veriyi §13 access policy ile alır

---

## 3. Solution Yapısı

```
src/
  building-blocks/
    Diten.BuildingBlocks.Core/
    Diten.BuildingBlocks.Application/
    Diten.BuildingBlocks.Domain/
    Diten.BuildingBlocks.Infrastructure/
    Diten.BuildingBlocks.Tenancy/
    Diten.BuildingBlocks.Security/
    Diten.BuildingBlocks.Authorization/
    Diten.BuildingBlocks.Audit/
    Diten.BuildingBlocks.Events/           # outbox publisher + consumer base
    Diten.BuildingBlocks.Documents/        # client SDK
    Diten.BuildingBlocks.Observability/
    Diten.BuildingBlocks.Persistence.Mongo/
    Diten.BuildingBlocks.Caching/          # tenant-aware cache wrapper

  platform/
    Diten.PlatformService.{Api,Application,Domain,Infrastructure,Worker}

  identity/
    Diten.IdentityService.{Api,Application,Domain,Infrastructure}

  audit/
    Diten.AuditService.{Api,Application,Domain,Infrastructure,Worker}

  workflow/
    Diten.WorkflowService.{Api,Application,Domain,Infrastructure,Worker}

  document/
    Diten.DocumentService.{Api,Application,Domain,Infrastructure}

  notification/
    Diten.NotificationService.{Api,Application,Domain,Infrastructure,Worker}

  domains/
    mdm/         Diten.MdmService.{Api,Application,Domain,Infrastructure,Worker}
    procurement/ Diten.ProcurementService.{...,Worker}
    inventory/   Diten.InventoryService.{...,Worker}
    sales/       Diten.SalesService.{...,Worker}
    logistics/   Diten.LogisticsService.{...,Worker}
    finance/     Diten.FinanceService.{...,Worker}
    planning/    Diten.PlanningService.{...,Worker}

  gateway/  Diten.ApiGateway/
  web/      Diten.Web/

tests/
  unit/  integration/  contract/  performance/
  architecture/   # ArchUnit kuralları (tenant_id, hybrid whitelist, no cross-DB)
  tenancy/        # cross-tenant 404, header zorunlu, audit stamp
```

---

## 4. Mongo Database Düzeni (per-service, shared cluster)

```
diten_global              # permission_catalog (code-seeded), role_templates, currencies, countries, languages, uoms, system_settings
diten_platform            # tenants, tenant_settings, tenant_feature_flags, tenant_quotas, tenant_lifecycle_events, tenant_provisioning_jobs
diten_identity            # platform_users, tenant_users, tenant_user_memberships, credentials, refresh_tokens, roles, role_assignments, sso_configs
diten_audit               # audit_events (tenant-scoped + platform_audit ayrı partition)
diten_workflow            # workflow_definitions (hybrid), workflow_instances, workflow_tasks
diten_document            # documents
diten_notification        # notification_templates, notification_log
diten_mdm                 # mdm_items, suppliers, customers, item_categories, ...
diten_procurement         # purchase_requests, purchase_orders, goods_receipts, vendor_invoices, user_procurement_profiles
diten_inventory           # stock_movements, stock_balances, plants, warehouses, user_inventory_profiles
diten_sales               # sales_orders, deliveries, customer_invoices, user_sales_profiles
diten_logistics           # shipments, routes, carriers
diten_finance             # journal_entries, gl_accounts, fiscal_periods, payments, user_finance_profiles
diten_planning            # demand_forecasts, mrp_runs, supply_plans
```

**Kural**: Cross-DB query YASAK. Bir servis başka servisin DB'sine bağlanmaz. Veri ihtiyacı → §13 access policy.

---

## 5. Tenant Context Canonical Contract

| Katman | Taşıyıcı | Zorunluluk |
|---|---|---|
| HTTP request | `X-Tenant-Id` header | Tenant endpoint'lerde zorunlu |
| JWT | `tenant_id` claim | Tenant token'larda zorunlu |
| Inter-service call | `TenantPropagationHandler` | Otomatik |
| Event | envelope `tenant_id` | Validation'lı zorunlu |
| Background job | payload `tenant_id` | Scheduler enforcement |
| Logs | scope `tenant_id` | Middleware otomatik |
| Audit | record `tenant_id` (+`target_tenant_id` admin için) | Zorunlu |
| Cache key | `t:{tenant_id}:...` prefix | Cache wrapper zorunlu |
| Object storage | `{tenant_id}/...` prefix | DocumentService enforcement |

Platform admin: `actor_type=platform_admin` claim, `target_tenant_id` parametre, audit'te ikisi de stamp.

---

## 6. Tenancy Building Block

```
Diten.BuildingBlocks.Tenancy/
  ITenantContext              # TenantId, IsPlatformContext, TargetTenantId
  TenantContext, TenantContextAccessor
  TenantMiddleware            # header → context resolution (priority §16)
  TenantPropagationHandler    # outgoing HTTP injection
  TenantScope                 # using-block scope (worker / consumer)
  TenantGuard                 # explicit assertion
  TenantResolutionStrategy    # JWT / Header / Subdomain
```

Sert kurallar (architecture test ile zorlanır):
- Controller method signature'ında `tenantId` parametresi YASAK
- Application service constructor'da `tenantId` parametresi YASAK
- Repository implementasyonunda manuel `tenant_id` filter YASAK
- Worker handler `TenantScope.Begin(tenantId)` açmadan iş yapamaz

---

## 7. Repository ve Persistence Standardı

```csharp
BaseEntity                         // Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, Version
GlobalEntity : BaseEntity
TenantScopedEntity : BaseEntity    // TenantId (non-null), IsDeleted
HybridEntity : BaseEntity          // TenantId (nullable; null=global default)

IGlobalRepository<T> where T : GlobalEntity
ITenantRepository<T> where T : TenantScopedEntity
IHybridRepository<T> where T : HybridEntity
```

Kurallar:
- Manuel `IMongoCollection<T>` injection YASAK
- Aggregation pipeline `BuildAggregationStart(tenantId)` ile başlar
- Roslyn analyzer: tenant-scoped collection'a tenant_id'siz query → compile error
- Soft delete partial index ile (`{tenant_id:1, key:1} where IsDeleted=false`)
- Compound index ilk alan **her zaman** `tenant_id`

---

## 8. Authorization Cache — Implementation Contract

**Cache yapısı:**
- Provider: Redis (production), MemoryCache (test/dev)
- Key format: `auth:{tenant_id}:{user_id}:v{role_assignment_version}`
- Value: `{ permissions: string[], roles: string[], abac_attributes: object, expires_at }`
- TTL: 300 sn (5 dk)

**Invalidation flow:**
1. IdentityService role_assignment değişikliği → DB write + version increment + outbox event (`role.assignment.changed`)
2. Outbox publisher event'i bus'a iletir
3. Tüm servisler consumer'ı vardır → `auth:{tenant}:{user}:v*` pattern'iyle key invalidate
4. Backup mekanizma: version mismatch — request geldiğinde cache'deki version IdentityService'in kanonik version'ından küçükse → cache miss + reload

**Source of truth:** IdentityService DB. Cache sadece read accelerator.

**Fail policy (kritik karar):**
- Domain default: **fail-closed**. Cache miss + IdentityService unreachable → 503 (login session devam etmez).
- Finance domain: **fail-closed, kesin**. Hiçbir koşulda cache miss bypass edilmez.
- Read-only public endpoint'ler (örn. health, metadata): fail-open kabul edilebilir, audit'le.

**Warm-up:** Login sonrası cache populate. Background refresh expires_at - 60sn'de.

**Test contract:**
- Auth cache invalidation latency < 5sn (95p) — test: role revoke → 5sn içinde tüm servislerde 403
- Cache miss + DB down → 503 (fail-closed) test
- Stale version → reload test

---

## 9. Audit — Outbox Pattern Contract

**Problem**: dual-write (DB + event bus) atomik değil. Sync write başarılı + event publish fail = audit gap.

**Çözüm — Transactional Outbox:**

**Producer (her servis)**:
1. Domain operation + audit record + outbox row aynı **MongoDB transaction**'ında yazılır
2. Outbox collection: `{tenant_id, event_id, event_envelope, status: pending, created_at, retry_count, next_retry_at}`
3. Outbox publisher (her servisin worker'ında) polling: pending event'leri çeker, bus'a publish eder, status=published yapar
4. Publish fail → retry (exponential backoff, max 10), 10 sonra status=failed + alert

**Consumer (AuditService)**:
- At-least-once delivery: idempotency key = `event_id`
- AuditService duplicate event_id görürse no-op
- `audit_events` collection'a immutable insert
- Append-only: update/delete YASAK (MongoDB role-level enforcement)

**Forensic audit (sync + outbox):**
- Finance post, payment approval, permission grant, sensitive data read için
- Operation transaction'ında **hem** local audit table'a write **hem** outbox'a write
- Local audit kayıt asla silinmez; outbox'tan çıkan event AuditService'e iletilir
- İki kayıt periyodik reconcile (gece job): mismatch → alert

**Retention:**
- Tenant-config'ten gelir (KVKK 10 yıl, IFRS 7 yıl, default 5 yıl)
- Retention policy enforcement `Diten.AuditService.Worker` günlük job

**Test contract:**
- Outbox publisher kill mid-publish → restart sonrası event tekrar publish + consumer idempotent test
- AuditService down → producer outbox şişer ama domain operation devam eder
- Outbox max retry → DLQ + alert test

---

## 10. Eventing + Schema Governance

### Event envelope (zorunlu format)
```json
{
  "event_id": "uuid",
  "event_name": "purchase_order.created",
  "event_version": 1,
  "tenant_id": "TEN-001",
  "correlation_id": "uuid",
  "causation_id": "uuid",
  "occurred_at": "2026-04-16T10:00:00Z",
  "producer": "Diten.ProcurementService",
  "actor": { "user_id": "...", "actor_type": "tenant_user" },
  "payload": { }
}
```

### Schema governance
- Her event JSON Schema ile tanımlı, `events/schemas/{event_name}/v{N}.json`
- Schema Registry: build-time validation (Faz 1) + runtime validation (Faz 2)
- CI gate: yeni event PR'ında schema dosyası zorunlu
- Schema değişikliği:
  - **Backward compatible** (alan ekleme, optional): minor revision, version aynı kalır
  - **Breaking** (alan kaldırma, type değişimi, semantik değişim): yeni `event_name.v{N+1}` + 6 ay overlap
- Producer N+1 yayınlamaya başlasa bile N'i 6 ay yayınlamaya devam eder
- Consumer N ve N+1'i aynı anda destekler (handler dispatch event_name + version key'iyle)

### Enforcement
- CI: schema yoksa build fail
- Runtime: producer schema'ya uymayan event publish edemez (envelope validator)
- Consumer: bilinmeyen version → DLQ + alert (silently drop YASAK)

### Partitioning
- Partition key = `tenant_id`
- Cross-tenant subscriber YASAK (sadece platform-level audit)

---

## 11. Cross-Service Data Access Policy

Sınırlı, hiyerarşik, enforce edilebilir kurallar:

### Seviye 1 — DİREKT DB ACCESS: YASAK
- Bir servis başka servisin DB'sine **hiçbir koşulda** bağlanmaz
- CI gate: connection string analizi, sadece kendi DB'sini referans alabilir
- ArchUnit test: `MongoClient` instantiation sadece kendi DB için

### Seviye 2 — SYNC READ: API üzerinden
- Idempotent GET, cacheable
- Caller circuit breaker + timeout (5sn) zorunlu
- Cache local (per-service Redis), TTL 60sn
- Use case: lookup, reference data fetch

### Seviye 3 — ASYNC STATE SYNC: event + local projection
- Provider domain event yayınlar (örn. `customer.updated`)
- Consumer kendi DB'sinde read-only projection tutar (`local_customer_view`)
- Eventually consistent (saniyeler)
- Use case: sık-okunan domain entity'leri (customer, supplier, item)

### Seviye 4 — REPORTING / ANALYTICS: ayrı projection DB
- CDC veya scheduled ETL ile servislerden ayrı `diten_analytics` DB'sine besleme
- Domain servise dokunmaz
- BI tool, dashboard, executive report buradan beslenir
- Tenant filter zorunlu (BI rapor query'sinde tenant_id mandatory)

### Seviye 5 — BULK DATA EXPORT: streaming API
- Rate-limited, paginated, signed
- KVKK Right of Portability, integration partner
- Audit'lenir, tenant admin onayı

**Karar matrisi:**
| Use case | Seviye |
|---|---|
| Login akışında user role çekme | 2 (cached) |
| PO oluştururken supplier doğrulama | 2 |
| Customer master sürekli okunuyor | 3 (projection) |
| Aylık satış raporu | 4 |
| Tenant migration export | 5 |
| "Procurement servisi finance journal'ı sorgulasın" | **YASAK** — domain event'i ile reaksiyon |

---

## 12. Workflow / Saga / Job Decision Tree

Her async iş için sıralı sorular:

```
1. Human approval / task assignment var mı?
   EVET → WorkflowService
   HAYIR → 2

2. Birden fazla servis state mutate ediyor + rollback gerekli mi?
   EVET → Saga pattern (initiating domain'in worker'ında)
   HAYIR → 3

3. Tek servis içinde async heavy iş mi (MRP, bulk import)?
   EVET → o servisin Worker'ı (background job)
   HAYIR → 4

4. Cross-tenant platform operasyonu mu (retention purge, billing run)?
   EVET → Diten.PlatformService.Worker
   HAYIR → 5

5. Periyodik schedule ile tetikleniyor + tenant-scoped iş mi?
   EVET → ilgili domain Worker (cron + tenant iteration)
   HAYIR → muhtemelen sync API call yeterli
```

**Örnekler:**
- PO approval chain (manager onayı) → **WorkflowService**
- PO create → inventory reserve → finance commit → **Saga** (Procurement.Worker)
- MRP run → **Inventory/Planning Worker** (background job)
- Tenant deprovisioning purge → **PlatformService.Worker**
- Aylık fatura kesimi tüm tenantlar için → **PlatformService.Worker** (orchestrator) + **FinanceService.Worker** (per-tenant execution)
- Item bulk import → **MdmService.Worker**

**Anti-pattern:**
- Approval workflow'ünü Hangfire job'a sokma → WorkflowService
- Saga compensating action'ı human task engine'e sokma → Saga handler
- Cross-tenant job'ı domain worker'da iterate etme → PlatformService.Worker

---

## 13. Hybrid Entity — Whitelist Enforcement

Hybrid pattern (`tenant_id nullable`, null=global default) **istisna**, default değil.

**Whitelist (ADR ile sabit):**
1. `workflow_definitions` — global template + tenant override
2. `permission_catalog` — code-defined global, tenant özel permission yok (whitelist'te ama runtime'da tenant override almaz)
3. ~Boş~ — yeni eklenecekse yeni ADR şart

**Enforcement:**
- ArchUnit test: `HybridEntity` türevleri yalnızca whitelist'teki sınıflar olabilir
- Yeni hybrid eklenirse architecture test fail
- Whitelist `Diten.BuildingBlocks.Domain.HybridWhitelist` constant'ında, kod review'da PR ile değişir

---

## 14. Gateway Tenant Resolution Priority

Sıra (yukarıdan aşağıya):

1. **JWT `tenant_id` claim** — auth'lu istek, **authoritative**
2. **`X-Tenant-Id` header** — login/public endpoint için
3. **Subdomain** (`acme.diten.com`) — public landing page

**Çelişki kuralları:**
- JWT var + header var + farklı → JWT kazanır, header değeri security event olarak loglanır
- JWT var + subdomain farklı → JWT kazanır, subdomain log
- Hiçbiri yok + tenant endpoint → 400 Bad Request
- Hiçbiri yok + public endpoint → kabul (login, health, etc.)

**Implementation:** Gateway middleware `TenantResolutionStrategy` chain. Sıra config-driven değil, kod-level sabit.

---

## 15. Test Strategy + Seeding Framework

### Test paketleri
```
tests/
  unit/              # her servis için, fast
  integration/       # API + Mongo testcontainers
  architecture/      # ArchUnit
    TenantIdRequiredOnEntities
    NoManualTenantFilterInRepos
    NoCrossDatabaseAccess
    HybridEntityWhitelistOnly
    HasPermissionAttributePresent
  tenancy/           # MUTLAK CI gate
    TenantHeaderRequiredTests
    CrossTenant404Tests
    CrossTenantMutationBlockedTests
    TenantScopedQueryTests
    AuditTenantStampTests
    EventTenantPropagationTests
    BackgroundJobTenantPropagationTests
    PlatformAdminAttributionTests
    AuthCacheInvalidationLatencyTests
    OutboxResilienceTests
  contract/          # Pact-style service-to-service
  performance/       # baseline + per-tenant load
```

### Seeding framework

**TestTenant factory:**
```csharp
var tenant = await TestTenantFactory.CreateAsync(opts => {
    opts.Tier = "Standard";
    opts.SeedRoles = ["TenantAdmin", "Viewer"];
    opts.SeedUsers = 1;  // returns admin user
    opts.SeedReferenceData = true;
});
// returns: TestTenantContext with tenant_id, admin_token, cleanup callback
```

**Kurallar:**
- Her test method kendi izole tenant'ını yaratır (`tenant_id` random GUID)
- Paralel test güvenli: tenant_id namespacing
- Teardown: test sonu `TestTenantFactory.PurgeAsync(tenant_id)` — tüm DB'lerde bulunan koleksiyonlardan `delete_many({tenant_id})`
- Reference data (countries, currencies) sadece bir kere seed (collection fixture)
- Background job test: in-memory job runner, scope simulation
- Event test: in-memory bus, sync dispatch

**Flakiness önleme:**
- Async test'lerde polling helper: `await EventuallyAsync(() => ..., timeout: 5s)`
- Time-dependent test'te `IClock` abstraction (fixed time)
- External call mock (HttpClient handler injection)

---

## 16. Domain Entity Standart Alanları

```
Tenant-scoped (zorunlu):
  id, tenant_id (indexed first), created_at, created_by,
  updated_at, updated_by, is_deleted (partial index), version

Domain-specific (gerektiğinde):
  legal_entity_id      # Finance/Procurement/Sales: zorunlu
  business_unit_id     # Finance opsiyonel
  plant_id             # Inventory/Production
  warehouse_id         # Inventory
```

**Sert kural:** `tenant_id` ↔ `legal_entity_id` farklı evrenler. Asla `same_as` veya `is_a`.

---

## 17. Implementation Roadmap (16-18 hafta)

### Sprint 1 — Tenant Context Foundation (2 hafta)
- BuildingBlocks: Core, Domain, Application, Tenancy
- TenantScopedEntity, GlobalEntity, HybridEntity (whitelist enforcement test)
- ITenantRepository, IGlobalRepository Mongo impl
- JWT tenant_id claim enforcement (gateway + middleware)
- TenantPropagationHandler

### Sprint 2 — PlatformService Skeleton (2 hafta)
- PlatformService projeler + diten_platform DB
- Tenant lifecycle state machine
- Tenant provisioning command (idempotent + rollback)
- Tenant suspension hook (gateway fast-reject)
- 4 internal module (Registry, Config, FeatureFlags, Quotas) namespace + endpoint ayrımı + ArchUnit kuralları

### Sprint 3 — Identity Refactor + User Core/Extension (2 hafta)
- IdentityService refactor
- tenant_user_memberships
- platform_user vs tenant_user ayrımı
- Domain user profile reference pattern doc + ADR

### Sprint 4 — Eventing + Outbox (2 hafta)
- BuildingBlocks.Events: envelope, publisher, consumer base
- Outbox pattern (per-service transactional outbox)
- Schema registry (build-time validation)
- Event versioning convention
- Outbox resilience tests

### Sprint 5 — Audit Service + Forensic Audit (2 hafta)
- AuditService skeleton + worker
- Standart audit (async event consumer)
- Forensic audit (sync local + outbox dual-write)
- Reconciliation job
- Retention worker

### Sprint 6 — Authorization + Cache Contract (2 hafta)
- BuildingBlocks.Authorization: HasPermission, ABAC interface, SoD
- Auth cache (Redis) + invalidation event flow
- Fail-closed default + finance enforcement
- Permission catalog code-seed
- Auth cache invalidation latency test

### Sprint 7 — Cross-Service Access Policy + Test Gate (2 hafta)
- BuildingBlocks.Caching (tenant-aware wrapper)
- Cross-service access policy enforcement (ArchUnit + connection analyzer)
- Local projection pattern (örnek: MDM customer → Procurement local view)
- tests/tenancy framework + en az 10 endpoint için cross-tenant 404 test
- tests/architecture ArchUnit kuralları
- CI gate aktivasyonu (tenancy + architecture build-break)

### Sprint 8 — Gateway, Worker Pattern, MDM Migration (2 hafta)
- Gateway tenant resolution priority chain
- Per-tenant rate limit (basic)
- *.Worker host pattern (her servis için template)
- Background job tenant propagation
- MDM mevcut servisi yeni building-block'lara migrate
- diten_mdm ayrı DB

### Sprint 9 (buffer / hardening, 2-4 hafta)
- WorkflowService skeleton (Definition + Runtime)
- DocumentService skeleton
- TestTenantFactory + seeding framework
- Compound index audit tüm collection'larda
- Performance baseline
- Tenant export tool (KVKK)

**Toplam: 18 hafta** (9 sprint × 2 hafta). Buffer dahil.

---

## 18. İlk Açılacak Servisler

**Faz 1 sonu canlı:**
- `Diten.PlatformService` (+ Worker)
- `Diten.IdentityService`
- `Diten.AuditService` (+ Worker)
- `Diten.MdmService` (refactor, mevcut)
- `Diten.ApiGateway`
- `Diten.Web`
- BuildingBlocks v1

**Faz 2 başında:** ilk gerçek domain — Procurement veya Inventory PoC.

**Faz 2 ortasında:** WorkflowService, DocumentService, NotificationService.

**Faz 3:** Sales, Logistics, Finance, Planning sırasıyla.

---

## 19. Final Decision Set

- **Mimari**: Tek MongoDB cluster, per-service database, GUID tenant_id ile shared multi-tenancy
- **Backbone**: PlatformService (4 internal module), IdentityService (auth-core only), AuditService (outbox), WorkflowService (definition+runtime), DocumentService, NotificationService
- **Authorization**: in-process building block, Redis cache, fail-closed default, role-change event ile invalidation
- **Audit**: outbox pattern zorunlu; finance forensic için sync + outbox dual; immutable storage; reconciliation job
- **Eventing**: envelope standart; tenant_id zorunlu; schema registry build-time, runtime Faz 2; backward compat 6 ay
- **Cross-service data**: 5 seviyeli hiyerarşi (DB direct YASAK → API → projection → analytics → bulk export)
- **Workflow/Saga/Job**: 5 soruluk karar ağacı; ayrı orchestrator servisi YOK
- **Hybrid entity**: whitelist (yalnız workflow_definitions, permission_catalog), ArchUnit enforce
- **Gateway tenant resolution**: JWT > Header > Subdomain, JWT authoritative, conflict log'lanır
- **Test**: tenancy + architecture testleri CI build-break; TestTenantFactory paralel-safe
- **User**: Identity user core (auth) ≠ business user profile (domain) — sert boundary, ADR'li
- **Süre**: 16-18 hafta foundation (9 sprint, buffer dahil)
- **Evolution**: ITenantConnectionResolver Faz 1'de soyutlanır, dedicated cluster geçişi 2-3 yıl sonra tier-based
