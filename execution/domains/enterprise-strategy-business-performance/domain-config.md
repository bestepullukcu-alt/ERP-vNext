# Enterprise Strategy & Business Performance — Domain Config

> Bu dosya ESBP'ye özgü sınırları ve kararları tanımlar. Engineering uygulama standartları
> [.antigravity/rules/](../../../.antigravity/rules/)'dan devralınır; burada tekrar edilmez. Üst seviye
> DCP-005 historical governance kaynağıdır; aktif 1.3/1.4/1.6 orchestration
> [DCP-006](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)'dır.

## Purpose

Enterprise Strategy & Business Performance (ESBP), strateji ve performans kayıtlarını sahiplenir. Demand
yalnız typed transition/reference sınırında tüketilir.

Bu governance scaffold production kodu veya module pack oluşturmaz ve tek başına implementation yetkisi
vermez.

## Domain Identity

- Ad: `Enterprise Strategy & Business Performance`
- Klasör: `enterprise-strategy-business-performance`
- Kısa kod: `esbp`
- Mevcut servis: `services/Diten.EnterpriseStrategyService`
- Tenant shell: `_LayoutTenantShell`
- Canonical local API port: `5102`
- Frontend erişimi: yalnız Gateway `5000` üzerinden
- Gateway route değişikliği: bu scaffold kapsamı dışında; yalnız `integration-agent`

## In-Scope Capabilities

- `MOD-0352` — Enterprise Strategy Management (historical approved alias: `CAND-CAP-0007` / “Enterprise Strategy & Performance Management”); subdomain 1.1 and outside the active DCP-006 1.3/1.4/1.6 implementation scope
- `CAND-CAP-0007-FU01` — Enterprise Strategy Security, Tenancy & Data Migration Foundation
- `MOD-0117` Demand — yalnız consumer/transition ve strategy-alignment sınırı

`MOD-0354` ve `MOD-0355`, `management-governance` domain'ine taşınmıştır.

Candidate kimlikler governance-only'dir; runtime literal olamaz. Gelecekteki canonical `MOD-xxxx`
allocation'ı DCP-002 ve Enterprise Architect kararıyla yapılır.

## Out-of-Scope

- MOD-0117'e ait Demand/idea, portfolio, project, benefit ve capacity lifecycle
- MOD-0024'e ait generic task/checklist lifecycle
- MOD-0023'e ait workflow run, approval decision, eligibility/delegation, SLA ve escalation
- DCP-004 / CAND-CAP-0006'ya ait WorkCenter aggregation/projection
- MOD-0288'e ait person/position/organization master records
- Shared document/evidence modüllerine ait payload ve binary ownership
- DWS Wave 1 node execution lifecycle, task assignment/actions ve local approval authority
- BPM içinde workflow-run veya approval motoru
- Gateway route implementation ve protected platform service değişiklikleri

## Domain-Level Repo Scope

Governance scaffold scope:

- `execution/domains/enterprise-strategy-business-performance/**`
- `AGENTS.md` §2'deki minimal domain-list güncellemesi
- `execution/README.md` içindeki minimal domain bağlantısı

Gelecekte yalnız ilgili onaylı module pack'in açıkça yetkilendirebileceği implementation adayları:

- `services/Diten.EnterpriseStrategyService/**`
- `frontend/Diten.Web/**` içindeki ESBP tenant surfaces
- repo-standard ESBP test ve migration/audit evidence yolları

Mevcut `Diten.EnterpriseStrategyService` DWS kodu yalnız legacy/hazard/migration reference'tır; production
baseline veya yeni domain service'i değildir.

## Existing frontend/prototype evidence

`frontend/Diten.Web` içindeki `ManagementGovernance`, `DeliveryExecutionManagement` ve ilişkili ESBP/DWS
yüzeyleri önceden yazılmış mock/prototype/legacy code-reality evidence'tır. Production baseline,
tamamlanmış capability, module status veya implementation authority değildir. Registry `Active` /
`Monitor` etiketleri gerçek lifecycle durumu sayılmaz; görüntülenen 1.1/1.2/1.5/1.7/1.8/1.9/1.10
yüzeyleri DCP-006 aktif delivery scope'una girmez.

Approve/assign/escalate kontrolleri ve hard-coded permission sonuçları `QUARANTINE` hazard evidence'tır.
DWS FS + due date/owner/overdue/status bileşimi structural Wave 1 değildir ve `QUARANTINE` kalır. Gate 2
alınmadan bu yüzeyler kaldırılamaz, değiştirilemez veya etkinleştirilemez. Pure
hierarchy/order/structural dependency yalnız reference olarak korunabilir; BPM placeholder'ları
implementation kanıtı değildir.

Global `_ViewStart` üzerinden seçilen FROZEN `_Layout` production temeli değildir. Gelecekteki tenant
module pack'leri `_LayoutTenantShell` kullanır ve `_Layout.cshtml` değiştirilmez.

## Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- Diğer domain'lerin `services/**` ve `execution/domains/**` yolları
- DCP-005 tarafından ayrıca yetkilendirilmemiş portfolio/registry dosyaları

## Ownership Boundaries

- **Strategy:** MOD-0352, goals/objectives/cascade, planning periods ve strategy
  performance context sahibidir.
- **Demand:** Canonical SoR MOD-0117'dir. ESBP yalnız strategy alignment ve kontrollü transition adapter'ı
  sahibi olabilir; ikinci Demand lifecycle oluşturamaz.
- **DWS/BPM:** `management-governance` domain'ine aittir. ES içindeki mevcut DWS kodu yalnız legacy,
  hazard ve migration reference'tır.
- **Generic task/checklist:** MOD-0024. DWS node veya ES `TaskAggregate` ikinci task SoR olamaz.
- **Workflow/approval:** MOD-0023 workflow run, approval task/decision, eligibility/delegation, SLA ve
  escalation sahibidir.
- **WorkCenter:** DCP-004 / CAND-CAP-0006 yalnız aggregation/projection sahibidir; business lifecycle
  gerçeğini taşımaz.
- **Person/position/org:** MOD-0288 canonical master sahibidir; ESBP typed reference tüketir.
- **Document/evidence:** İlgili shared modüller payload ve provenance sahibidir; ESBP typed link tüketir.

## Control Tower Gate 2

Aşağıdaki alanlarda herhangi bir production code, DTO/API, UI, projection, migration, deletion veya
deprecation değişikliğinden önce Claude WorkCenter Control Tower Gate 2 gerekir:

1. ES `TaskAggregate` değişikliği, migrasyonu, silinmesi veya deprecation.
2. DWS task-benzeri node alanlarında projection, UI, migrasyon veya davranış.
3. `ApprovedAt` / `ApprovedBy` üzerinden yerel approval.
4. Serbest metin Demand kimliğinden WorkCenter projection.

Salt-okunur envanter ve Gate 2 kanıt hazırlığı production değişikliği sayılmaz. WC-5/cross-service bridge ve
ES içinde platform-internal `IWorkItemProvider` ayrı DCP-004 onay kapısındadır; Gate 2 bu yetkiyi vermez.

## Runtime Decisions

- Persistence MongoDB'dir; tenant-owned operational records için `TenantId` zorunludur.
- Tenant server-side claim/context üzerinden çözülür; request body tenant seçemez.
- Cross-tenant read/write fail-closed olur ve kontrollü not-found/denial döner.
- Soft delete, audit, optimistic concurrency ve idempotency ilgili onaylı module pack'te zorunlu kontratlardır.
- Mevcut tenant'ı deterministik belirlenemeyen kayıtlar default tenant'a atanmaz; audit edilebilir
  karantinaya alınır.
- Migration tasarımı forward, retry, partial failure, verification, quarantine ve rollback davranışını
  collection bazında açıklar.
- `Diten.Platform.Common.Persistence.BaseEntity`, BL-030 BSON `DateTimeOffset` riski çözülmeden ES'ye
  körlemesine taşınamaz. Scalar UTC/BSON representation ve gerçek Mongo guard testi foundation pack'te
  karara bağlanır.
- Authentication ve authorization fail-closed JWT + RBAC/permission enforcement kullanır.
- Tenant UI `_LayoutTenantShell` ve yedi dil standardını kullanır.
- Frontend hiçbir zaman `5102` servis portuna doğrudan çağrı yapmaz; Gateway `5000` kullanır.
- Her delivery slice mevcut regression'ı kapatmadan sonraki slice'a geçemez.

Engineering detay kaynakları:

- [multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md)
- [security-jwt.md](../../../.antigravity/rules/security-jwt.md)
- [entity-base-template.md](../../../.antigravity/rules/entity-base-template.md)
- [mongo-indexing.md](../../../.antigravity/rules/mongo-indexing.md)
- [ports.md](../../../.antigravity/rules/ports.md)
- [routes.md](../../../.antigravity/rules/routes.md)
- [views-organization.md](../../../.antigravity/rules/views-organization.md)
- [localization-standard.md](../../../.antigravity/rules/localization-standard.md)

## Delivery Gates

- DCP-005 `approved` / `ready-for-execution` olmadan production implementation başlamaz.
- Sıradaki capability/module için ayrıca `approved` / `ready-for-dev` module pack gerekir.
- Gateway route işi ayrı integration-agent scope ve onayı gerektirir.
- Gate 2 tehlikelerinde yazılı Gate 2 PASS gerekir.
- Her slice gerçek persistence, security/tenant, audit/concurrency/idempotency, failure-path ve regression
  kanıtıyla kapanır.
- Candidate kimlikler runtime'da bulunursa teslimat bloklanır.

## Open Decisions

- Foundation base entity/BSON strategy ve BL-030 çözümü
- Collection-by-collection tenant mapping, quarantine retention ve data steward
- MOD-0117 Demand FU identity ve transition contract
- ES `5102` gateway route implementation
- CTD/CST-125 pilot dataset ve success thresholds
- Candidate kimliklerin gelecekteki canonical `MOD-xxxx` allocation'ları
