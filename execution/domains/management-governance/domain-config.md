# Management & Governance — Domain Config

> Domain'e özgü kapsam ve sahiplik sözleşmesidir. Engineering uygulama kuralları
> [.antigravity/rules/](../../../.antigravity/rules/)'dan devralınır; burada tekrarlanmaz. Aktif
> orchestration kaynağı [DCP-006](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)'dır.

## Domain identity

- Name: `Management & Governance`
- Slug: `management-governance`
- Branch short code: `mg`
- Canonical capabilities: `MOD-0354`, `MOD-0355` (historical aliases `CAND-CAP-0008/0009`)
- Planned service: `services/Diten.ManagementGovernanceService` — mevcut değil
- Planned internal modules: `Dws`, `ProcessModeling`
- Port ve gateway: `TBD`; bu scaffold karar vermez

Candidate kimlikler governance-only'dir ve hiçbir runtime literal'da kullanılamaz.

## In scope

### MOD-0354 — DWS structural core

- Structure definition/template/instance/node
- Hierarchy and ordering
- Pure structural dependency
- Validation
- Immutable baseline, version and compare

DWS dışında: task/execution dependency ve lifecycle, assignment, progress, due date, task action ve local
approval.

### MOD-0355 — Business Process Architecture & Modeling core

- Process architecture, domain and family
- Process model and version
- Activity and control point
- Typed role and KPI binding

BPM dışında: workflow run, approval decision/eligibility/delegation, operational task, SLA ve escalation.

## Existing frontend/prototype evidence

`frontend/Diten.Web` içindeki mevcut `ManagementGovernance`, `DeliveryExecutionManagement` ve ilişkili
ESBP/DWS yüzeyleri mock/prototype/legacy code-reality evidence'tır; production baseline, tamamlanmış
capability, module status veya implementation authority değildir. Registry `Active` / `Monitor`
etiketleri lifecycle status sayılmaz.

`/management-governance` 1.1/1.2/1.5/1.7/1.8/1.9/1.10 alanlarını da gösterir; bunlar DCP-006 aktif
delivery scope'u değildir. Approve/assign/escalate kontrolleri ve hard-coded permission sonuçları
`QUARANTINE` hazard evidence'tır ve Gate 2 olmadan kaldırılamaz, değiştirilemez veya etkinleştirilemez.
DWS FS + due date/owner/overdue/status bileşimi structural Wave 1 değildir ve `QUARANTINE` kalır; pure
hierarchy/order/structural dependency yalnız reference olarak korunabilir. BPM placeholder'ları
implementation kanıtı değildir.

Global `_ViewStart` ile seçilen FROZEN `_Layout` production temeli değildir. Gelecekteki tenant module
pack'leri `_LayoutTenantShell` kullanır; `_Layout.cshtml` değiştirilmez.

## Ownership boundaries

- PPM/initiative/program/project/portfolio: `MOD-0117`
- Strategy: ESBP / `MOD-0352`; typed Demand transition: `MOD-0117`. Demand implementation bu domain dışında
- WorkCenter aggregation/projection: DCP-004 / `CAND-CAP-0006`
- Generic task/checklist: `MOD-0024`
- Workflow/approval: `MOD-0023`
- Evidence: `MOD-0031`
- Effective permission: `MOD-0018`
- Canonical reference data: `MOD-0048`
- Person/position/organization: `MOD-0288`

## Planned service isolation contract

`Dws` ve `ProcessModeling`:

- birbirlerinin domain tiplerine referans veremez;
- repository veya Mongo collection paylaşamaz;
- ayrı permission aileleri kullanır;
- task/approval/workflow helper paylaşamaz;
- ortak business aggregate oluşturamaz;
- yalnız typed ID, query contract ve versioned event ile haberleşir.

Bu sınırlar module-pack architecture testleriyle mekanik olarak korunamıyorsa
`Diten.ManagementGovernanceService` scaffold'u fail-closed bloklanır ve DWS/BPM ayrı servis kararı
uygulanır.

## Permission enforcement integration

- AuthService is the system of record for permission grants and signed-JWT issuance.
- Tenant services enforce the signed JWT permission claim locally and fail closed through a PSS-approved
  reusable in-process handler/policy/evaluator.
- Platform/AuthService service-specific filters or evaluators cannot be copied into DWS. A synchronous
  AuthService or remote decision call is neither required nor designed for Wave 1.
- `IEntitlementChecker` is limited to module/feature entitlement and cannot be used for permission enforcement.
  JWT freshness and revocation follow MOD-0018-FU13.
- DWS consumes the MOD-0018 enforcement result and does not calculate roles, grants, RBAC/ABAC or effective
  permission. Its command-permission denial is `403`.
- MOD-0117 validation owns context referenceability only. An absent, soft-deleted, cross-tenant or actor-invisible
  context returns `404`; the validator never recalculates DWS permission.
- Until the reusable shared integration is allocated and implemented, the MOD-0018 subset is `PARTIAL`, DWS
  runtime start is blocked and DCP-006 OD-04 remains open. No follow-up identity is allocated here.

## Greenfield service foundation decisions

New Management Governance collections use a local base contract with `Guid Id`, required server-resolved
`Guid TenantId`, scalar BSON UTC `DateTime CreatedAtUtc`, nullable scalar BSON UTC `DateTime UpdatedAtUtc`,
`bool IsDeleted`, nullable scalar BSON UTC `DateTime DeletedAtUtc` and technical `int Version`.
Local/`Unspecified` timestamps fail closed unless explicitly normalized from a server-produced value to UTC.
Neither `Diten.Platform.Common.Persistence.BaseEntity` nor the existing ES base class is inherited or copied.

The persistence model is multi-document Mongo replica-set transactions. A single-document tree aggregate is
rejected because unbounded structural growth creates an unacceptable 16 MB document-limit risk. Standalone
Mongo fallback, snapshot/compensating rollback and partial commit are forbidden. Missing transaction support
makes readiness fail closed and mutations return `503`.

DWS mutation data and its idempotency receipt persist in the same required transaction boundary. Wave 1
receipts have no TTL or automatic expiry; cleanup that weakens replay guarantees is forbidden. Any future
retention policy requires a separate versioned pack decision. Every required audited mutation persists its
producer-local technical audit intent/outbox in that same transaction; if the intent cannot be persisted, the
mutation rolls back. The local intent is not the MOD-0021 business audit SoR. After commit, publication is
asynchronous and a broker/consumer/Platform failure does not roll back the mutation; it requires retry,
dead-letter, alarm and authorized replay.

`AuditIntentPersisted` and `AuditEventAcceptedByMOD0021` are technical observability states only. They cannot
become a DWS aggregate/revision business `Status`, task lifecycle, workflow or approval state. DWS cannot
write directly to Platform `audit_outbox` or `audit_events`, and the existing shared-key
`/api/internal/audit/append` endpoint is not an authoritative Wave 1 baseline. The MOD-0021 dependency
remains `PARTIAL` until the versioned semantic contract and its implementation, compatibility, transaction,
security, payload-limit and delivery evidence exist.

## Repo scope

Bu governance görevinin scope'u:

- `execution/domains/management-governance/**`

Future implementation scope ancak ilgili approved/ready-for-dev module pack ile açıkça yetkilendirilebilir.
Bu scaffold service, frontend, gateway, port veya migration yolu açmaz.

## Protected paths

- `.antigravity/**`
- `services/**`
- `frontend/**`
- `gateway/**`
- diğer domain'lerin `execution/domains/**`
- registry ve portfolio dosyaları, açık governance reconciliation görevi dışında
- Office belgeleri

## Control Tower Gate 2 reference

DCP-006 §15'teki dört tehlike korunur:

1. ES `TaskAggregate` değişikliği/migrasyonu/silinmesi/deprecation;
2. DWS task/execution dependency veya task-benzeri status/date/progress/assignment/lifecycle davranışı;
3. local approval (`ApprovedAt`/`ApprovedBy`, approve service/UI/route/command);
4. free-text Demand identity'den WorkCenter projection.

Pure structural dependency Gate 2 tehlikesi değildir. Salt-okunur legacy inceleme production değişikliği
sayılmaz.

## Delivery gates

- DCP-006 `approved`.
- OD-02 ve OD-08 `CLOSED`.
- DCP-005 OD-03 `CLOSED`; cold-start, real-Mongo scalar UTC representation and sort/index proof remain
  implementation acceptance evidence.
- DWS Wave 1 shared-contract blockers are limited to MOD-0117 typed context validation, the `PARTIAL`
  MOD-0018 reusable in-process signed-JWT permission enforcement integration and a versioned MOD-0021 audit
  append/event contract.
- DWS ve BPM için ayrı module pack.
- Pack `approved` / `ready-for-dev` ve açık kullanıcı onayı olmadan code/service scaffold yok.
- Candidate runtime-literal taraması temiz olmalı.
