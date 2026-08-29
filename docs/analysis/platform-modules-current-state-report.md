# Platform Modülleri — Mevcut Durum Raporu (Kanıta Dayalı)

> **Kapsam:** MOD-0004, MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0028, MOD-0031, MOD-0040, MOD-0063
> **Tür:** Salt-okunur analiz. Kod değişikliği / seed / migration / commit yapılmadı.
> **Tarih:** 2026-08-03
> **Kanıt kaynağı:** repository kodu, `execution/registries/module-id-registry.md`, module pack'ler, `docs/audits/*`, gateway `ocelot.json`, testler, `docs/System Capability & Implementation Blueprint - master 7.xlsx :: Blueprint_Data`.

---

## 0. Yönetici Özeti

| Modül | Blueprint adı | Gerçek durum | Ağırlıklı tamamlanma | Runtime kanıtı |
|---|---|---|---|---|
| MOD-0004 | Metric & Semantic Registry | **SPECIFICATION_ONLY** | %5–10 (Low) | Yok |
| MOD-0018 | RBAC / ABAC Authorization | **PARTIALLY_IMPLEMENTED** (RBAC core güçlü; ABAC/data-scope + admin UI eksik) | %62–75 (Med) | Login + permission enforcement runtime çalışıyor |
| MOD-0019 | Data Masking & Row/Field Security | **NOT_IMPLEMENTED** (yalnız log-redaction parçası) | %8–15 (Med) | Yok |
| MOD-0021 | Audit Trail Service | **IMPLEMENTED_BUT_RUNTIME_NOT_PROVEN** | %70–80 (Med) | Pipeline behavior wired; adanmış authenticated smoke closeout yok |
| MOD-0023 | Workflow Designer | **PARTIALLY_IMPLEMENTED** | %60–72 (Med) | Downstream doc "runtime mevcut" diyor; adanmış golden-flow smoke yok |
| MOD-0028 | Documentation Management | **PARTIALLY_IMPLEMENTED** | %68–78 (Med) | ControlledDocuments authenticated smoke geçti (MOD-0029 FU); FU06 Corporate **BLOCKED** |
| MOD-0031 | Evidence Linking Service | **SPECIFICATION_ONLY** (yalnız pack) | %8–12 (High) | Yok |
| MOD-0040 | Canonical ID & Correlation Standard | **FOUNDATION_ONLY** (korelasyon foundation var; canonical ID modeli yok) | %25–35 (Med) | Gateway correlation propagation aktif |
| MOD-0063 | Data Warehouse / Lakehouse | **SPECIFICATION_ONLY** (yalnız blueprint) | %5 (High) | Yok |

**En hazır modül:** MOD-0018 (RBAC core) ve MOD-0021 (Audit) — ikisi de foundation olarak kullanılabilir.
**En kritik eksik:** MOD-0019 (Data Masking) — PV için hasta/raportör PII maskeleme yok (P0).
**En büyük çelişki:** Registry'de `MOD-0040` yanlış capability'ye (Tenant Organization Foundation → MOD-0288) bağlanmış; Blueprint'te `MOD-0040 = Canonical ID & Correlation Standard`.

---

## A. Blueprint Doğrulaması

Blueprint (`Blueprint_Data`) satırları, dokuz modülün canonical adlarını **doğrular**. Repository gerçeğiyle karşılaştırma:

| Module | Blueprint target | Repository evidence | Actual state | Match verdict |
|---|---|---|---|---|
| MOD-0004 | Metric & Semantic Registry; SoR: metric defs/semantic IDs/calc contracts/certification | execution veya services içinde **hiç** pack/entity/controller yok | SPECIFICATION_ONLY | **NO_MATCH** (blueprint build hedefliyor, repo boş) |
| MOD-0018 | RBAC/ABAC; SoR: roles/entitlements/policies/access reviews | `Diten.AuthService` tam RBAC (permission catalog, roles, assignment, `HasPermissionAttribute` tüm servislerde), Platform entitlement/AccessExplain | PARTIALLY_IMPLEMENTED | **PARTIAL_MATCH** (RBAC var; ABAC/masking/data-scope + admin UI eksik) |
| MOD-0019 | Data Masking & Row/Field Security; SoR: masking policies/row-field rules | Yalnız `CrmService/.../Common/PiiMasking.cs` (log redaction) + audit alan maskeleme | NOT_IMPLEMENTED | **NO_MATCH** |
| MOD-0021 | Audit Trail Service; SoR: audit events/retention/export; AUDIT-BUNDLE (AuditEvent v1, tamper-evidence, OTel) | `Diten.Platform` tam audit domain + outbox + retention + query/export controller + gateway route + 11 test dosyası | IMPLEMENTED_BUT_RUNTIME_NOT_PROVEN | **PARTIAL_MATCH** (tamper-evidence kriptografik zincir yok) |
| MOD-0023 | Workflow Designer; SoR: definitions/run history/SLA/escalation; WORKFLOW-BUNDLE | `Diten.Platform` tam workflow (definitions/versions/publish/instances/tasks/transitions/SLA/escalation) + frontend + tests + gateway | PARTIALLY_IMPLEMENTED | **PARTIAL_MATCH** |
| MOD-0028 | Documentation & Evidence Management; SoR: documents/templates/versions | `Diten.Platform` 30+ document controller + frontend + RBAC seed + audit; FU06 runtime blocked | PARTIALLY_IMPLEMENTED | **PARTIAL_MATCH** |
| MOD-0031 | Evidence Linking Service; SoR: evidence objects/links/provenance; EVIDENCE-LINK | Yalnız module pack (`MOD-0031-evidence-linking-service.md`), **hiç** `EvidenceLink` entity/controller yok | SPECIFICATION_ONLY | **NO_MATCH** |
| MOD-0040 | Canonical ID & Correlation Standard; SoR: canonical ID defs/correlation rules; TRACE-BUNDLE | Gateway `CorrelationPropagationDelegatingHandler` + `X-Correlation-Id` + `AuditEvent.CorrelationId` + event envelope; canonical/external ID mapping yok; pack yok | FOUNDATION_ONLY | **PARTIAL_MATCH** (correlation partial; canonical ID absent). Registry `MOD-0040`'ı yanlış capability'ye bağlıyor → **OUTDATED_BLUEPRINT/registry drift** |
| MOD-0063 | Data Warehouse / Lakehouse; SoR: datasets/ACLs/retention; LAKEHOUSE-BUNDLE | Kod/pack/frontend **yok** (`lakehouse`/`warehouse` grep = 0) | SPECIFICATION_ONLY | **NO_MATCH** |

---

## B. Modül Bazında Repository Envanteri (özet kanıt)

### MOD-0004 — Metric & Semantic Registry — SPECIFICATION_ONLY
- Registry kaydı: **Yok** (`module-id-registry.md`'de MOD-0004 satırı yok).
- Module pack / spec: **Yok** (`execution/**`, `docs/**` altında `MOD-0004` dosyası bulunamadı).
- Domain/entity/repo/API/gateway/RBAC/frontend/test: **Yok.** `metric`/`semantic` grep sonuçları alakasız (observability metrics, TokenService metadata vb.).
- **Sonuç:** Yalnızca Blueprint satırı var. Kod tabanında hiçbir implementasyon yok.

### MOD-0018 — RBAC / ABAC Authorization — PARTIALLY_IMPLEMENTED
- Registry: `MOD-0018 | RBAC/ABAC | ready-for-dev`; çok sayıda FU (FU10/FU12/FU13/FU14/FU15/FU9).
- Domain/Persistence: `Diten.AuthService` — permission catalog, roles, role assignment, `ModulePermissionResolver.cs`, `DataSeeder.cs`; `AuthAuditLog`.
- Application: `AssignPermissionCommandHandler`, `RevokePermissionCommandHandler`, `EntitlementPermissionSyncService`, `RoleProvisioningService`.
- API: `PermissionsController`, `RolesController`, `InternalPermissionsController`, `UsersController`; Platform: `AuthorizationProbeController`, `AccessExplainController`, `TenantSecuritySettingsController`.
- Enforcement: `HasPermissionAttribute` **her serviste** (Auth, Platform, Crm, Hcm, Mdm, DevEnablement) + `HasPermissionReflector`.
- Tests: 20+ test (`Permissions/*`, `Roles/*`, `Authorization/*`, `EndpointAuthorizationClassificationTests`).
- **Eksik:** ABAC/data-scope (`MOD-0018-FU15 real-data-scope-resolver` = planned), RBAC Admin UI (`MOD-0018-FU9` = planned), attribute-based policy console.

### MOD-0019 — Data Masking & Row/Field Security — NOT_IMPLEMENTED
- Registry/pack/spec: **Yok.**
- Bulunan tek kanıt: `CrmService/.../Common/PiiMasking.cs` — MOD-0150 kapsamında **log/audit redaction** helper'ı (e-posta/telefon şekillerini maskeler), veri maskeleme policy engine'i **değil**. `AuditEvent` alanları `ActorEmailMasked`/`IpAddressMasked` olarak maskeli tutulur.
- Data classification, masking rules studio, row-level security, policy test harness, query enforcement, response serialization masking: **Yok.**
- Tenant/company izolasyonu (`TenantScopedEntity`, cross-tenant 404) row-level güvenliğe kısmen yaklaşır ama MOD-0019 policy modeli değildir.

### MOD-0021 — Audit Trail Service — IMPLEMENTED_BUT_RUNTIME_NOT_PROVEN
- Registry: `MOD-0021 | Audit Trail Service | ready-for-dev / implemented evidence` + `MOD-0021-PLAN` all-phases plan.
- Domain: `Entities/Audit/AuditEvent.cs` (immutable, `ValidateAppend()` append-only + immutability guard), `AuditEventRetentionPolicy`, `TenantAuditPreference`, `AuditTenantIds`.
- Persistence: `AuditEventRepository`, `AuditOutboxRepository`, `AuditRetentionPolicyRepository`, `AuditOutboxMessage/Status`, `AuditRetentionPolicySeed`.
- Application: `Contracts/Audit/*` (IAuditableCommand, IAuditOutboxWriter, AuditAppendRequest/Result, retention resolution), `Features/Audit/*`, audit pipeline behavior.
- Infra: `AuditOutboxWorker/Processor`, `PlatformEntitlementAuditSink`, `SafeAuditErrorFormatter`.
- API: `PlatformAuditController`, `PlatformAuditAppendController`.
- Gateway: `/api/platform/audit/events`, `/audit/export`, `/audit/retention`, `/audit/redact-actor`, `/api/v1/platform/audit/events`.
- Tests: 11 dosya (`AuditApplicationCoreTests`, `AuditOutboxWorkerTests`, `AuditPhase5ApiSurfaceTests`, `BizCriticalAuditRejectionTests`, `TenantScopeTests`, ...).
- **Eksik/zayıf:** Kriptografik **tamper-evidence zinciri yok** (AuditEvent'te hash/previousHash alanı yok — grep 0; immutability yalnız uygulama seviyesinde). Adanmış authenticated runtime smoke closeout dokümanı bulunamadı.

### MOD-0023 — Workflow Designer — PARTIALLY_IMPLEMENTED
- Registry: `MOD-0023 | Workflow Designer | review / planned`; pack `ready-for-dev` (2026-06-23).
- Domain: `Entities/Workflow/WorkflowTemplate`, `WorkflowTemplateVersion`, `WorkflowInstance`, `WorkflowTransitionLog`; enums (`WorkflowInstanceStatus`, `WorkflowTemplateVersionStatus`, `WorkflowTransitionAction`); `IWorkflowRepositories`.
- Application: Commands (Create/Publish/Start/Approve/Reject/RequestInfo/Delegate/Cancel/RunEscalations), Handlers, `WorkflowCandidateResolver`, `WorkflowDefinitionRuntimePlan`, `WorkflowEscalationSweepJob` (background), `IWorkflowTransitionGate`.
- API: `WorkflowDefinitionsController` (`api/v1/workflow`) — definitions, publish, versions, instances, tasks, tasks/mine, transitions/evaluate, sla-rules, escalations/run, task approve/reject/delegate/request-info/cancel.
- Gateway: `/api/v1/workflow`, `/api/v1/workflow/{everything}`, `/api/v1/platform/workflows/instances`.
- RBAC: `platform.workflow.definitions.view/manage/publish`, `instances.start/view`, `tasks.approve/reject/delegate` (DefaultRolePermissionTemplate seed).
- Frontend: `Controllers/WorkflowController.cs` + `Views/Platform/Workflow` + 7-dil resx.
- Tests: 9 dosya (`WorkflowTaskTransitionTests`, `WorkflowSlaEscalationTests`, `WorkflowTemplateVersionPublishTests`, `WorkflowTransitionGateTests`, `WorkflowInstanceStartTests`, ...).
- **Runtime:** `docs/audits/crm-capability-progress-review-2026-07-31.md:229` "MOD-0023 Workflow ... runtime **mevcut**" der; ancak **adanmış** uçtan-uca approval golden-flow smoke closeout dokümanı yok. Önceki blocker (ocelot route + permission seed) artık giderilmiş görünüyor.

### MOD-0028 — Documentation Management — PARTIALLY_IMPLEMENTED
- Registry: `MOD-0028 | Documentation & Evidence Management | review / planned` + FU01–FU06.
- API: 30+ controller — `DocumentManagementTemplatesController`, `TemplateMastersController`, `TemplateVariantsController`, `ControlledDocumentsController`, `SignaturesController`, `RetentionController`, `LifecycleController`, `MasterRegisterController`, `ApprovalController`, `AccessPoliciesController`, `CorporateCollectionInstancesController`, ...
- Domain: `Entities/DocumentManagement/*`.
- Frontend: `Views/DocumentManagement/*` (ControlledDocuments, MasterRegister, TemplateMasters/Variants, AccessMatrix, QmsBaselines, Instantiations, Reconciliation, RepositoryAssessments).
- Gateway: `/api/v1/document-management`, `/api/v1/document-management/{everything}`.
- RBAC/Audit: doc-management permission seed (memory: manual grants tenant 97C5), audit entegrasyonu var.
- **Runtime:** ControlledDocuments authenticated runtime smoke MOD-0029 FU dizisinde **geçti** (`mod-0029-fu36d-fu37d-authenticated-runtime-smoke-*`); **ancak** FU06 Corporate Collection Instance → `docs/audits/mod-0028-fu06-runtime-smoke-reconciliation-2026-07-25.md` **BLOCKED** (Mongo partial index `$ne` reddi Platform startup'ı çökertiyor).

### MOD-0031 — Evidence Linking Service — SPECIFICATION_ONLY
- Registry: `MOD-0031 | Evidence Linking Service | review / planned`.
- Pack: `MOD-0031-evidence-linking-service.md` (owned objects: EvidenceLink, EvidenceBundle, EvidenceRequirement).
- Kod: `EvidenceLink` entity/controller/repo/handler **yok** (grep sonuçları Enterprise Strategy'nin KPI "evidence link" alanları + BRD evidence fixture'ları — MOD-0031 SoR değil).
- **Sonuç:** Tasarım/pack seviyesinde. Runtime capability yok.

### MOD-0040 — Canonical ID & Correlation Standard — FOUNDATION_ONLY
- Registry: `MOD-0040` **deprecated alias → MOD-0288 (Tenant Organization Foundation)**. Bu, Blueprint canonical adı (Canonical ID & Correlation Standard) ile **çelişir**.
- Var olan foundation: `gateway/.../Observability/CorrelationPropagationDelegatingHandler.cs` + `appsettings*.json` `Correlation.HeaderName = X-Correlation-Id`; `AuditEvent.CorrelationId`; `Diten.BuildingBlocks.Eventing/EventEnvelope.cs` + `EventMetadata` (correlation taşıma); background job context correlation.
- **Eksik:** Canonical ID model, external reference mapping (legacy ObjectId → ExternalReference), trace stitching contract, duplicate/conflict handling, adanmış module/pack. Correlation ID gateway'de `HttpContext.TraceIdentifier`'a bağlı — standartlaştırılmış canonical ID şeması değil.

### MOD-0063 — Data Warehouse / Lakehouse — SPECIFICATION_ONLY
- Registry/pack/spec/kod/frontend: **Yok** (`lakehouse`, `warehouse` grep = 0; `dataset` hitleri BusinessReferenceData reference-data setleridir, analitik dataset değil).
- **Sonuç:** Yalnızca Blueprint satırı.

---

## C. Golden Flow Analizi (özet)

| Modül | Beklenen golden flow | Repo'da çalışıyor mu? | Failure path korunuyor mu? |
|---|---|---|---|
| MOD-0004 | metrik tanımı → calc contract → certify → reload → tüketici erişimi | **Hayır** (hiç yok) | Yok |
| MOD-0018 | permission → role → assign → login → decision → izinli açık/izinsiz 403 | **Evet (RBAC core)** — enforcement + login runtime çalışıyor; ABAC/data-scope kısmı yok | Kısmi: endpoint-level 403 var; `EndpointAuthorizationClassificationTests` açık endpoint denetler. Data-scope leakage guard eksik (FU15 planned) |
| MOD-0019 | classification → masking policy → role bind → maskeli/tam görünüm → API'de aynı kural | **Hayır** | Yok — "UI maskeli, API ham" riski **test edilemez** (policy engine yok) |
| MOD-0021 | kritik işlem → AuditEvent v1 → actor/object/before-after/correlation/tenant/time → explorer → export/retention | **Büyük ölçüde (offline)** — append/outbox/retention/export var, pipeline behavior wired; adanmış authenticated smoke yok | Evet: `ValidateAppend()` immutability/append-only; `BizCriticalAuditRejectionTests`. **Ama** audit-writer failure davranışı (kritik işlem sessizce devam mı) runtime doğrulanmadı; tamper-evidence zinciri yok |
| MOD-0023 | definition → step/SLA → publish → instance → task inbox → approve/reject/request-info → history/audit | **Evet (backend)** — tüm endpointler + `WorkflowTaskTransitionTests`; runtime "mevcut" iddiası var, adanmış smoke yok | Evet (test): `WorkflowTransitionGateTests` invalid transition; unauthorized approval RBAC ile; row-version guard |
| MOD-0028 | document create/upload → metadata+content → version → reopen → new version → permission → audit | **Kısmen** — ControlledDocuments akışı authenticated smoke geçti; Corporate collection (FU06) **BLOCKED** | Kısmi: access policy testleri offline green; FU06 index defect runtime'ı kırıyor |
| MOD-0031 | object ↔ evidence link → provenance → reopen görünür → audit/export pack | **Hayır** | Yok |
| MOD-0040 | request → correlation ID üret/koru → downstream API/log/event/audit aynı ID → canonical mapping → trace stitching | **Kısmen** — correlation propagation var; canonical ID mapping + trace stitching yok | Kısmi: correlation kayıp riski gateway handler'ıyla azaltılmış; "aynı external ID iki canonical objeye" guard yok (canonical model yok) |
| MOD-0063 | dataset → contract binding → ingestion → lakehouse write → ACL → tüketici sorgu → lineage/run history | **Hayır** | Yok |

---

## D–E–F–G–I–J–K

Ayrıntılar ayrı dosyalarda:
- Katman matrisi: `platform-modules-layer-completion-matrix.md`
- Yüzdeler / capability matrisi: `platform-modules-capability-matrix.md`
- PV değerlendirmesi: `platform-modules-pv-readiness-assessment.md`
- Bağımlılık/sıra: `platform-modules-dependency-and-sequence-report.md`
- Gap & remediation + yol haritası: `platform-modules-gap-and-remediation-plan.md`
- Nihai karar: `platform-modules-final-status-report.md`
- CSV'ler: `platform-modules-traceability-matrix.csv`, `platform-modules-gap-matrix.csv`, `platform-modules-pv-readiness-matrix.csv`

---

## H. Çelişki ve Yanlış Sahiplik Bulguları

| Conflict ID | Capability | Current owner | Correct owner | Evidence | Severity | Recommendation |
|---|---|---|---|---|---|---|
| CONF-01 | MOD-0040 kimliği | Registry: MOD-0040 = Tenant Organization Foundation (deprecated → MOD-0288) | Blueprint: MOD-0040 = Canonical ID & Correlation Standard | `module-id-registry.md` MOD-0040 satırı vs `Blueprint_Data` | **Yüksek** | EA reservation ile registry'yi Blueprint'e hizala; Canonical ID & Correlation için ayrı MOD-0040 pack aç |
| CONF-02 | Permission enforcement | Her serviste ayrı `HasPermissionAttribute` (Auth/Platform/Crm/Hcm/Mdm/DevEnablement) | Dağıtık enforcement + AuthService merkezi catalog — **kabul edilebilir** desen | 6 servisde `class HasPermission` | Düşük | Duplication değil; catalog/authority AuthService'te. İzlemede tut |
| CONF-03 | Audit | Bazı servisler local audit publisher (Crm `LoggingAccountAuditPublisher`, Hcm `DraftAuditService`) | MOD-0021 Platform Audit (append API) canonical SoR | Crm/Hcm Infrastructure/Audit local logger'lar | Orta | Local logger'ları MOD-0021 append path'e köprüle (Crm `HttpCrmAuditPublisher`, Hcm `GovernedHcmAuditAppendClient` bunu yapıyor — kısmen uyumlu) |
| CONF-04 | Data masking | UI/log-redaction (`PiiMasking`) | MOD-0019 policy engine (yok) | `CrmService/Common/PiiMasking.cs` | **Yüksek (PV için)** | MOD-0019 gerçek policy engine olarak inşa edilmeli; redaction helper yerine geçmez |
| CONF-05 | Evidence linking | Enterprise Strategy KPI "evidence link" + BRD evidence fixtures = string/embedded referanslar | MOD-0031 Evidence Linking Service (yok) | ES `EnterpriseStrategyDtos`, BRD `ProvisionBusinessReferenceDataEvidenceFixtureCommand` | Orta | MOD-0031 canonical evidence link SoR'u inşa edilene kadar cross-module evidence normal string ID kalır (sızıntı riski) |
| CONF-06 | Correlation ID | Gateway `TraceIdentifier` bazlı | MOD-0040 canonical ID & correlation standardı | `CorrelationPropagationDelegatingHandler.cs` | Orta | Standardı MOD-0040 pack ile canonical hale getir; servisler-arası tutarlılık contract testi yok |

> **Not:** Analytics-için-operational-DB, workflow-hardcoded-in-business-modules ve document-storage-local-disk için repo'da **karşı kanıt** bulundu (workflow tek runtime Platform'da; audit generic; doc storage partition sözleşmesi merkezî). MOD-0063 operasyonel SoR gibi yanlış kullanım riski **yok** çünkü MOD-0063 hiç yok.

---

## M. Raporlama Uyarıları / Sınırlar

- Bu analiz **statik kanıt** (kod, test dosya varlığı, pack, gateway) üzerinedir. `dotnet test` / authenticated runtime smoke bu görevde **çalıştırılmadı**; "test var" ≠ "test yeşil geçti". PASS iddiası hiçbir modül için verilmedi.
- `MOD-0028` için ControlledDocuments runtime smoke kanıtı MOD-0029 FU closeout dokümanlarına dayanır (dolaylı); körü körüne güvenilmedi, FU06 blocker'ı açıkça işaretlendi.
- Yüzdeler kanıt seviyesiyle birlikte aralık olarak verildi (bkz. capability matrisi).
