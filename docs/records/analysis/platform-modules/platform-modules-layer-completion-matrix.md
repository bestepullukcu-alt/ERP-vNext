# Platform Modülleri — Katman Bazında Tamamlanma Matrisi

Status: PASS · PARTIAL · FAIL · NOT_IMPLEMENTED (NI) · NOT_APPLICABLE (NA) · UNKNOWN

## MOD-0004 — Metric & Semantic Registry
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | FAIL | Registry'de MOD-0004 satırı yok; yalnız Blueprint | Registry kaydı + pack yok |
| Domain | NI | — | Metric definition/semantic ID/calc contract yok |
| Application | NI | — | — |
| Persistence | NI | — | — |
| API | NI | — | — |
| Gateway | NI | — | — |
| RBAC | NI | — | — |
| Audit | NI | — | — |
| Events/Integration | NI | — | GOV-REG/SEMANTIC bundle yok |
| Frontend | NI | — | Metric & KPI Studio yok |
| Tests | NI | — | — |
| Runtime smoke | NI | — | — |
| Documentation | PARTIAL | Blueprint_Data satırı | Module spec yok |

## MOD-0018 — RBAC / ABAC Authorization
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | PASS | `MOD-0018` + FU10/12/13/14/15/9 registry | — |
| Domain | PASS | Permissions/Roles/entitlement, `ModulePermissionResolver` | ABAC attribute/policy modeli zayıf |
| Application | PASS | Assign/Revoke handlers, `EntitlementPermissionSyncService`, `RoleProvisioningService` | Real data-scope resolver (FU15) planned |
| Persistence | PASS | AuthService persistence, `DataSeeder` | — |
| API | PASS | Permissions/Roles/Users + `AuthorizationProbeController`, `AccessExplainController` | — |
| Gateway | PASS | auth/permission routes | — |
| RBAC (self) | PASS | `HasPermissionAttribute` tüm servislerde + `HasPermissionReflector` | — |
| Audit | PARTIAL | `AuthAuditLog`, `RbacAuditRecorder`, `PlatformEntitlementAuditSink` | — |
| Events/Integration | PARTIAL | `EntitlementSyncConsumer`, tenant activation events | — |
| Frontend | PARTIAL | Tenant security settings; **RBAC Admin UI (FU9) planned** | Role/Permission Builder, Policy Console, Access Review Dashboard yok |
| Tests | PASS | 20+ (Permissions/Roles/Authorization/Security) | ABAC/data-scope testleri yok |
| Runtime smoke | PARTIAL | Login + 403 enforcement runtime çalışıyor (memory: tenant login/RBAC smokes) | Adanmış batch-decision + data-scope smoke yok |
| Documentation | PASS | Çok sayıda pack + audit | — |

## MOD-0019 — Data Masking & Row/Field Security
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | FAIL | Registry/pack yok | — |
| Domain | NI | — | Masking policy/classification/row-field rule yok |
| Application | NI | — | Policy evaluation yok |
| Persistence | NI | — | — |
| API | NI | — | — |
| Gateway | NI | — | — |
| RBAC | NA | — | — |
| Audit | PARTIAL | AuditEvent alan maskeleme (`ActorEmailMasked`) | Data masking değil |
| Events/Integration | NI | — | SEC-DATA-BUNDLE yok |
| Frontend | NI | — | Data Access Matrix / Masking Rules Studio yok |
| Tests | PARTIAL | `PiiMasking` (CRM log redaction) — MOD-0019 değil | Policy test harness yok |
| Runtime smoke | NI | — | — |
| Documentation | PARTIAL | Blueprint | — |

## MOD-0021 — Audit Trail Service
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | PASS | `MOD-0021` + all-phases plan | — |
| Domain | PASS | `AuditEvent` (immutable, ValidateAppend), retention policy, tenant pref | Tamper-evidence hash zinciri yok |
| Application | PASS | Contracts/Audit/*, IAuditableCommand, outbox writer, retention resolution | — |
| Persistence | PASS | AuditEvent/Outbox/Retention repos, retention seed | — |
| API | PASS | `PlatformAuditController`, `PlatformAuditAppendController` | — |
| Gateway | PASS | audit/events, /export, /retention, /redact-actor | — |
| RBAC | PARTIAL | audit query/export permission | Explicit denied-action audit permission testi kısmi |
| Audit (self) | PASS | meta-audit (`IsMetaAudit`), redaction | — |
| Events/Integration | PASS | outbox worker, `PlatformEntitlementAuditSink`, tenant lifecycle consumer | — |
| Frontend | PARTIAL | `Views/Platform/AuditLog`, `AuditRetention` | Explorer/export UI olgunluğu doğrulanmadı |
| Tests | PASS | 11 test (core/outbox/api/rejection/tenant-scope) | Audit-writer failure runtime davranışı yok |
| Runtime smoke | PARTIAL | pipeline behavior wired | Adanmış authenticated smoke closeout yok |
| Documentation | PASS | pack + all-phases plan | — |

## MOD-0023 — Workflow Designer
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | PASS | `MOD-0023` pack ready-for-dev | — |
| Domain | PASS | Template/Version/Instance/TransitionLog + enums + repos | — |
| Application | PASS | Create/Publish/Start/Approve/Reject/RequestInfo/Delegate/Cancel + SLA escalation sweep | BPMN/görsel step model değil, JSON runtime plan |
| Persistence | PASS | `IWorkflowRepositories` | — |
| API | PASS | `WorkflowDefinitionsController` (17 route) | — |
| Gateway | PASS | `/api/v1/workflow`, `/workflows/instances` | — |
| RBAC | PASS | `platform.workflow.*` seed | — |
| Audit | PARTIAL | transition log; MOD-0021 entegrasyon derinliği doğrulanmadı | — |
| Events/Integration | PARTIAL | Hcm `GatewayWorkflowStartClient`, `ConsumeWorkflowDecision` | Cross-module contract tek tüketici (HCM) |
| Frontend | PARTIAL | `Views/Platform/Workflow` + resx | Görsel Workflow Designer / SLA Console olgunluğu düşük |
| Tests | PASS | 9 test (transition/sla/publish/gate/start) | — |
| Runtime smoke | PARTIAL | downstream doc "runtime mevcut" (2026-07-31) | Adanmış approve/reject/request-info golden-flow smoke yok |
| Documentation | PASS | pack + analiz doc | — |

## MOD-0028 — Documentation Management
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | PASS | `MOD-0028` + FU01–FU06 | — |
| Domain | PASS | `Entities/DocumentManagement/*` | — |
| Application | PASS | Templates/Versions/Approval/Retention/Lifecycle features | — |
| Persistence | PARTIAL | Mongo repos | FU06 Corporate unique index `$ne` **çöküyor** |
| API | PASS | 30+ document controller | — |
| Gateway | PASS | `/api/v1/document-management` | — |
| RBAC | PASS | doc-management permission seed (manuel grant tenant 97C5) | Otomatik seed politikası yok |
| Audit | PARTIAL | audit entegrasyonu var | — |
| Events/Integration | PARTIAL | reconciliation/deviation workflow | Evidence linking (MOD-0031) yok |
| Frontend | PASS | `Views/DocumentManagement/*` (10+ ekran) + 7-dil | — |
| Tests | PASS | doc management app tests (memory: 1911 app suite green offline) | — |
| Runtime smoke | PARTIAL | ControlledDocuments authenticated smoke geçti; **FU06 BLOCKED** | Corporate flow runtime çöküyor |
| Documentation | PASS | spec v2.3.0 + audit dizisi | — |

## MOD-0031 — Evidence Linking Service
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | PARTIAL | `MOD-0031` pack | — |
| Domain | NI | — | EvidenceLink/Bundle/Requirement yok |
| Application–Runtime smoke | NI | — | Hiç kod yok |
| Frontend | NI | — | Evidence Panel/register yok |
| Documentation | PASS | detaylı pack | — |

## MOD-0040 — Canonical ID & Correlation Standard
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Governance/Registry | FAIL | Registry MOD-0040'ı MOD-0288'e (yanlış capability) bağlıyor | Canonical ID pack yok |
| Domain | NI | — | Canonical ID definition / external reference model yok |
| Application | PARTIAL | correlation taşıma (event envelope, background job) | ID mapping/dedup yok |
| Persistence | NI | — | — |
| API | NI | — | — |
| Gateway | PARTIAL | `CorrelationPropagationDelegatingHandler` + `X-Correlation-Id` | TraceIdentifier bazlı; canonical şema değil |
| RBAC | NA | — | — |
| Audit | PARTIAL | `AuditEvent.CorrelationId` | — |
| Events/Integration | PARTIAL | `EventEnvelope`/`EventMetadata` correlation | Trace stitching contract yok |
| Frontend | NA | — | — |
| Tests | PARTIAL | correlation propagation testleri kısmi | Servisler-arası tutarlılık contract testi yok |
| Runtime smoke | PARTIAL | correlation header propagate ediyor | Uçtan-uca trace stitching smoke yok |
| Documentation | PARTIAL | Blueprint | Module pack yok |

## MOD-0063 — Data Warehouse / Lakehouse
| Layer | Status | Evidence | Gap |
|---|---|---|---|
| Tüm katmanlar | NI | `lakehouse`/`warehouse` grep = 0 | Dataset catalog/ingestion/storage/ACL/lineage yok |
| Documentation | PARTIAL | Blueprint | Module spec yok |
