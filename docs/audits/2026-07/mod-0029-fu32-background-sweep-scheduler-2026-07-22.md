# MOD-0029-FU32 — Background Sweep / Scheduler Jobs

**Tarih:** 2026-07-22
**Servis:** `Diten.Platform`
**Kapsam:** Document Control (MOD-0029) için non-destructive, idempotent, tenant-aware background governance sweep foundation
**Verdict:** PASS_WITH_GAPS (scheduler registration bilinçli olarak deferred)

---

## 1. Initial Audit Summary

| # | İnceleme | Bulgu |
|---|---|---|
| 1 | Background job / scheduler altyapısı | Hangfire tabanlı; `IRecurringJobRegistrar` + `HangfireRecurringJobRegistrationHostedService` mevcut |
| 2 | Platform job registration pattern | `PlatformRecurringJobRegistrar` — 9 kayıt, hepsi `BackgroundJobSchedulerOptions.EnabledJobs` feature-flag'i ile gated, default disabled |
| 3 | Manual maintenance/sweep command pattern | FU31A `DocumentGovernancePolicyPackApplicationService` (service + MediatR + append-only history) — FU32 bu pattern'in birebir kardeşi olarak kuruldu |
| 4 | FU12 PeriodicReview | `DocumentPeriodicReviewService.EvaluateOverdueAsync(registerEntryId)` mevcut ve **zaten idempotent** (`RaiseEscalationAsync` açık escalation'ı suppress ediyor) |
| 5 | FU14 ExternalDocument | `NextCheckDueDate` + `ImpactAssessmentDueDate` alanları var; `GetOverdueImpactAssessmentsAsync` status'ü **persist ediyor** → sweep bu metodu çağırmıyor, kendi read-only raporunu üretiyor |
| 6 | FU13 TemporaryInstructionControl | `EvaluateExpiryAsync` mevcut; expired + action yok ise `OpenInternalAsync` ile **idempotent** suspension CASE açıyor (suspension execute etmiyor) |
| 7 | FU20 Downtime | `DocumentTemporaryIssueService.EvaluateOverdueAsync(downtimeEventId, issueId)` mevcut; `EnsureEscalationAsync` duplicate-suppressed |
| 8 | FU22 CAPA | Overdue evaluator **yok** → report-only |
| 9 | FU23 SignatureRequest | `SignatureRequestStatus.Expired` enum'da var ama **expiry transition command yok** → report-only |
| 10 | FU15 Retention / LegalHold | `DocumentRetentionEvaluator.EvaluateAsync` subject state yazıyor → sweep çağırmıyor; mevcut subject'leri okuyup raporluyor |
| 11 | Idempotency key pattern | Sidecar'larda "açık/aktif kayıt varsa skip" pattern'i standart → FU32 aynısını kullanıyor |
| 12 | Audit / correlation / tenant context | `TenantGuard.RequireTenant`, `ICorrelationContext`, `ICurrentUserContext` — hepsi kullanıldı |
| 13 | Permission attribution | FU29A kuralı: en yakın seeded key, unseeded key icat edilmez |
| 14 | FU29 seeded sweep key | **Dedicated `governance-sweeps.*` key YOK** (121 seeded doc-management key tarandı) |
| 15 | Fallback key kararı | Her endpoint kendi domain'inin seeded key'ini kullanıyor (bkz. §7) |
| 16 | Test baseline | `Diten.Platform.Application.Tests`: **1860 passed / 0 failed** |

---

## 2. Existing Scheduler / Background Job Pattern

```
Diten.BuildingBlocks.BackgroundJobs.IRecurringJobRegistrar
  └── PlatformRecurringJobRegistrar (Application/BackgroundJobs/)
        └── RecurringJobRegistration(BackgroundJobDescriptor, jobType, argsType, args, context)
              └── HangfireRecurringJobRegistrationHostedService (Infrastructure)
```

- Her job `IsEnabled = RegisterStandardJobs && EnabledJobs[id] == true` → **default disabled**.
- Cadence örnekleri: daily (`0 2 * * *`), hourly (`0 * * * *`), 5-min (`*/5 * * * *`).
- **Tenant enumeration pattern yok**: mevcut sweep job'ları (`WorkflowEscalationSweepJob`) tenant registry üzerinden değil, kendi içlerinde çözüyor.

---

## 3. Sweep Run Model

### Entity — `DocumentGovernanceSweepRun` (`TenantScopedEntity`)
Collection: `document_management_governance_sweep_runs`

| Alan | Not |
|---|---|
| `SweepKey` / `SweepName` / `SweepVersion` | `SweepVersion = "1.0.0"`, semantics değişince bump |
| `TriggerType` | `Manual` / `Scheduled` / `System` |
| `Status` | `Completed` / `CompletedWithWarnings` / `Failed` |
| `StartedAt` / `CompletedAt` / `AsOfDate` | |
| `TriggeredByUserId` / `CorrelationId` / `CreatedBy` | |
| `ItemsScanned` / `ItemsAffected` | |
| `FindingsCreated` / `EscalationsCreated` | |
| `ExistingFindingsSkipped` / `ExistingEscalationsSkipped` | idempotency kanıtı |
| `Warnings` / `ErrorMessage` | |
| `SweepKeysExecuted` | run-all'da çalışan grup listesi |
| `ResultItems` | `DocumentGovernanceSweepResultItem[]` |
| `DryRun` | persisted satırda daima `false` |

`DocumentGovernanceSweepResultItem`: `SubjectType`, `SubjectId`, `Action`, `Outcome`, `Message`, `RelatedFindingId`, `RelatedEscalationId`.

### Enums (`GovernanceSweepEnums.cs`)
- `DocumentGovernanceSweepStatus` — Completed / CompletedWithWarnings / Failed
- `DocumentGovernanceSweepTriggerType` — Manual / Scheduled / System
- `DocumentGovernanceSweepItemOutcome` — NoActionRequired / Reported / EscalationCreated / SkippedExisting / Warning / DryRun

### Repository
`IDocumentGovernanceSweepRunRepository` — `CreateAsync`, `GetByIdAsync`, `GetAllForTenantAsync`, `GetLatestBySweepKeyAsync`, `UpdateAsync`.
**Delete metodu yok.** `UpdateAsync` sadece start'ta açılmış bir run'ı kapatmak için; completed run revize edilmiyor.

Mongo index'leri: `(TenantId, SweepKey, StartedAt desc)`, `(TenantId, Status)`, `(TenantId, StartedAt desc)` — **SweepKey üzerinde unique index yok** (tekrar çalışma normal).

---

## 4. Sweep Service / Orchestrator

`DocumentGovernanceSweepService` (Application/Features/DocumentManagementGovernanceSweep/)

| Method | Not |
|---|---|
| `PreviewAllAsync` | zorlanmış dry-run |
| `RunAllAsync` | tüm gruplar veya `SweepKeys` alt kümesi |
| `RunPeriodicReviewsAsync` | |
| `RunExternalDocumentsAsync` | |
| `RunTemporaryInstructionsAsync` | |
| `RunDowntimeTemporaryIssuesAsync` | |
| `RunCapaAsync` | |
| `RunSignatureRequestsAsync` | |
| `RunRetentionEligibilityAsync` | |
| `RunLegalHoldScopeAsync` | |
| `ListRunsAsync` / `GetRunAsync` | tenant-scoped history |

Kurallar: tenant-scoped (`TenantGuard`), idempotent, mevcut kayıt overwrite edilmez, duplicate finding/escalation üretilmez, hard delete yok, subject destructive mutation yok.

---

## 5. Implemented Sweep Groups

| # | Grup | Mod | Davranış |
|---|---|---|---|
| 1 | Periodic Review | **Evaluator-backed** | Due date geçmiş, in-force dokümanlar → FU12 `EvaluateOverdueAsync`. Escalation created/skipped delta ile sayılıyor. Auto-suspend / lifecycle transition **yok**, review auto-initiate **yok** |
| 2 | External Documents | **Report-only** | Monitoring overdue + due-soon (14 gün) + impact assessment overdue. External API çağrısı yok, source status/lifecycle dokunulmuyor, `AssessmentStatus` flip edilmiyor |
| 3 | Temporary Instructions | **Evaluator-backed** | Expired control → FU13 `EvaluateExpiryAsync`. Expiry action yoksa idempotent suspension **CASE** açılıyor (`Opened`, `ApprovedAt`/`ExecutedAt` null). Suspension execute edilmiyor |
| 4 | Downtime Temp Issues | **Evaluator-backed** | Reconciliation due geçmiş issue → FU20 `EvaluateOverdueAsync` (ReconciliationOverdue + MissingReconciliation, duplicate-suppressed). Copy withdraw yok, issue close yok |
| 5 | Quality / CAPA | **Report-only** | CAPA due overdue + effectiveness due overdue. CAPA/deviation/quality event close/cancel/effective **yok**, bridge auto-trigger **yok** |
| 6 | Signature Requests | **Report-only** | Draft/Pending + due geçmiş request'ler raporlanıyor. Sign/verify/invalidate/Expired transition **yok** (FU23'te böyle bir komut yok — icat edilmedi) |
| 7 | Retention Eligibility | **Report-only** | Eligible / blocked-by-hold / missing-policy / not-evaluated (coverage gap) / permanent. **Hiçbir silme, purge, disposition yok**; disposition request bile açılmıyor |
| 8 | Legal Hold Scope | **Report-only** | Active hold'un `EffectiveUntil` geçmişse ve boş scope varsa rapor. Release/cancel/re-scope **yok** |

---

## 6. Deferred Sweep Groups

Hiçbir grup tamamen deferred edilmedi; ancak **davranış seviyesinde** şunlar bilinçli olarak deferred:

| Deferred davranış | Gerekçe |
|---|---|
| Signature request → `Expired` status transition | FU23'te böyle bir komut/kural yok; sweep state machine icat etmez |
| Retention subject re-evaluation | `DocumentRetentionEvaluator.EvaluateAsync` subject state yazıyor → sweep bunu tetiklemiyor, coverage gap'i warning olarak raporluyor |
| External impact `AssessmentStatus` → `Overdue` persist | FU14 `GetOverdueImpactAssessmentsAsync` bunu yapıyor ama sweep'in yazma yetkisi dışında; report-only tutuldu |
| CAPA overdue escalation/finding kaydı | FU22'de escalation sidecar'ı yok; finding aggregate'i olmadan uydurma kayıt üretilmedi |
| Periodic review auto-initiate | Review başlatma sahibi olan insani bir eylem |

---

## 7. API / Command Changes & Permission Attribution

Controller: `DocumentManagementGovernanceSweepController`
Base route: `/api/v1/document-management/governance-sweeps`

| Endpoint | Verb | Permission (hepsi FU29 **seeded**) |
|---|---|---|
| `/run-all` | POST | `platform.document-management.retention.manage` |
| `/preview` | POST | `platform.document-management.retention.view` |
| `/periodic-reviews/run` | POST | `platform.document-management.master-register.periodic-review.manage` |
| `/external-documents/run` | POST | `platform.document-management.external-documents.manage` |
| `/temporary-instructions/run` | POST | `platform.document-management.master-register.suspension.manage` |
| `/downtime-temporary-issues/run` | POST | `platform.document-management.downtime.manage` |
| `/quality-capa/run` | POST | `platform.document-management.capa.view` (report-only → dar key) |
| `/signature-requests/run` | POST | `platform.document-management.signatures.view` (report-only → dar key) |
| `/retention-eligibility/run` | POST | `platform.document-management.retention.view` (report-only → dar key) |
| `/legal-hold-scope/run` | POST | `platform.document-management.legal-hold.view` |
| `/runs` | GET | `platform.document-management.retention.view` |
| `/runs/{id}` | GET | `platform.document-management.retention.view` |

- **DELETE / PUT / PATCH verb'ü yok** (test ile doğrulandı).
- Request body `TenantId` taşımıyor; tenant server-side çözülüyor.
- Body opsiyonel — gönderilmezse default'larla çalışır.

### Önerilen future key'ler (FU29 seed işi, bu task'ta seed **yapılmadı**)
- `platform.document-management.governance-sweeps.view`
- `platform.document-management.governance-sweeps.run`
- `platform.document-management.governance-sweeps.manage`

MediatR: 9 run command + 1 preview query + 2 history query, hepsi thin handler.

---

## 8. Scheduler Registration Decision — **DEFERRED**

Mevcut Hangfire pattern güvenli ve feature-flag'li olmasına rağmen recurring job **eklenmedi**. Gerekçe:

1. `PlatformRecurringJobRegistrar`'daki job'lar tenant context'siz çalışıyor. FU32 sweep'i **tenant context olmadan çalışamaz** (`TenantGuard.RequireTenant`).
2. Repo'da güvenli bir **tenant enumeration** pattern'i yok — bir sweep job'ın hangi tenant'lar için döneceğini belirleyecek onaylı bir kaynak bulunmuyor.
3. Task hard boundary'si: *"Tenant enumeration gerekiyorsa mevcut tenant registry pattern yoksa scheduler deferred bırak. Tenant context olmadan sweep çalıştırma."*

**Sonuç:** Bu FU sadece manual trigger API + service teslim ediyor. Scheduler registration için önerilen (gelecek FU) cadence:

| Sweep | Önerilen cadence |
|---|---|
| periodic review | daily `0 2 * * *` |
| external monitoring | daily `0 2 * * *` |
| temporary instruction expiry | daily `0 3 * * *` |
| downtime temporary issue | daily `0 3 * * *` |
| CAPA | daily `0 4 * * *` |
| signature expiry | daily `0 4 * * *` |
| retention eligibility | weekly `0 5 * * 0` |

Hepsi `RegisterStandardJobs` + per-job `EnabledJobs` flag'i altında, **default disabled**. Hourly'den sık job önerilmiyor.

---

## 9. Idempotency Behavior

- Aynı subject + aynı koşul için duplicate escalation/finding **oluşmaz** — mevcut FU12/FU13/FU20 evaluator'ları açık escalation'ı suppress ediyor.
- Sweep, evaluator çağrısının **öncesi/sonrası escalation sayısını** karşılaştırıp `EscalationsCreated` vs `ExistingEscalationsSkipped` olarak kaydediyor → idempotency run history'de görünür kanıt.
- **Closed/resolved finding sonrası yeni finding:** mevcut pattern `Open or Acknowledged` durumundakileri skip ediyor; kapanmış bir escalation koşul devam ediyorsa yenisinin açılmasına izin veriyor. FU32 bu davranışı **değiştirmedi**, mevcut evaluator semantiğine bıraktı.
- Her manual run **ayrı** append-only history satırı yazar.
- Dry run **hiç** satır yazmaz.

---

## 10. Dry-Run Behavior — karar

**dryRun=true hiçbir şey yazmaz — history satırı dahil.** Response `RunId = Guid.Empty`, `DryRun = true` döner ve tüm bulgular response gövdesinde raporlanır. `PreviewOnly=true` satır yazma alternatifi seçilmedi; "dry run izsizdir" kuralı daha kolay ispatlanabilir ve testlerle doğrulandı.

`asOfDate`: server-validated, candidate selection'ı yönetir. Evaluator-backed gruplarda evaluator'lar kendi yazımlarını `UtcNow` ile damgaladığı için grup bir **warning** ekler — çağıran farkı tahmin etmek zorunda kalmaz.

`maxItems`: grup başına tarama sınırı; kesme olursa warning.

---

## 11. Failure Model — karar

**Group-level isolation.** Bir grup exception atarsa `GOVERNANCE_SWEEP_PARTIAL_FAILURE` warning'i kaydedilir, diğer gruplar çalışmaya devam eder ve run `CompletedWithWarnings` olarak biter. Fail-fast seçilmedi — bir grubun çökmesi diğerlerinin bulgularını gizlememelidir. Sadece run'ın kendisinin kurulamaması `Failed`/500'dür (history best-effort).

---

## 12. Reason Codes

`GovernanceSweepReasonCodes`: `GOVERNANCE_SWEEP_RUN_NOT_FOUND`, `GOVERNANCE_SWEEP_TENANT_REQUIRED`, `GOVERNANCE_SWEEP_FAILED`, `GOVERNANCE_SWEEP_UNSUPPORTED`, `GOVERNANCE_SWEEP_DRY_RUN`, `GOVERNANCE_SWEEP_PARTIAL_FAILURE`.

---

## 13. Audit (IAuditableCommand)

`IAuditableCommand` pattern'i Platform'da **command bazlı** uygulanıyor. FU32'de ayrı bir audit interface'i implement edilmedi; bunun yerine her run zaten `DocumentGovernanceSweepRun` olarak **append-only governance evidence** yazıyor (`CorrelationId`, `TriggeredByUserId`, `CreatedBy`, `StartedAt`/`CompletedAt`, tüm sayaçlar ve per-subject result item'lar). Bu, FU31A'nın policy-pack application history yaklaşımıyla birebir tutarlı. **Gap olarak raporlanıyor** (bkz. §17).

---

## 14. Tests

Yeni: `tests/Diten.Platform.Application.Tests/DocumentManagement/DocumentGovernanceSweepTests.cs` — **41 test**.
Testler **gerçek** FU08/FU12/FU13/FU20 servisleriyle çalışır (mock'lanmadı), böylece destructive-değil iddiaları gerçek davranışa karşı doğrulanır. Her fake repo `UpdateCalls`/`DeleteCalls` sayar.

Kapsanan senaryolar: run-all history / tenant scoping / tenant-required / no-hard-delete / no-auto-close-approve-effective-disposition-sign-retire / periodic review escalation created+skipped / no-auto-suspend / external monitoring due+overdue report / impact overdue without mutation / temporary instruction idempotent suspension case / suspension not executed / downtime overdue + duplicate skip / no copy withdrawal / CAPA due+effectiveness report without close / signature report without sign-invalidate / retention eligible-blocked-missing-permanent without delete / legal hold not released / dry-run no writes / dry-run no subject change / cross-tenant run detail blocked / unknown run 404 / run list tenant scoping / group failure isolation / unknown sweep key / maxItems cap / asOfDate narrowing / preview writes nothing / controller permission attribution (run-all manage, history view, 8 grup endpoint'i) / no unseeded permission key / no destructive HTTP verb.

Güncellenen: `DocumentSuspensionRetirementTests.cs` — `FakeTemporaryControlRepo`'ya yeni `GetAllForTenantAsync` eklendi (additive).

---

## 15. Build / Test Results

```
dotnet build services/Diten.Platform/src/Diten.Platform.API/... -o ./.tmp/verify-fu32-platform-build
  → 0 error (bin kilidi nedeniyle alternatif output kullanıldı — çalışan fleet)

Diten.Platform.Application.Tests   → 1901 passed / 0 failed  (baseline 1860 + 41 yeni)
Diten.Platform.Eventing.Tests      →   56 passed / 0 failed / 3 skipped (RabbitMQ integration)
Diten.Platform.BackgroundJobs.Tests→   18 passed / 2 failed  ← PRE-EXISTING, FU32 ile ilgisiz
```

**BackgroundJobs 2 failure — pre-existing:** `BackgroundJobContractsTests` 8 descriptor bekliyor, `PlatformRecurringJobRegistrar` 9 tane döndürüyor. Registrar `286d019e` commit'inde 9'a çıkmış, test assertion'ı güncellenmemiş. FU32 **hiçbir recurring job eklemedi** (scheduler deferred) ve bu dosyalarda uncommitted değişiklik yok (`git status` ile doğrulandı).

---

## 16. Guardrail Verification

| Guardrail | Sonuç |
|---|---|
| AuthService seed değişikliği | ❌ yok — FU32 hiçbir AuthService dosyasına dokunmadı |
| Gateway değişikliği | ❌ yok |
| Frontend değişikliği | ❌ yok |
| MOD-0028 baseline lifecycle mutation | ❌ yok |
| raw bytes Mongo'ya | ❌ yok — sadece governance metadata |
| hard delete / purge | ❌ yok — grep temiz, repository'de delete metodu bile yok |
| auto close / approve / effective / disposition / sign / retire | ❌ yok — grep + 6 ayrı test |
| direct 5057 / client TenantId / X-Tenant-Id | ❌ yok — grep temiz |
| workflow runtime / e-sign provider / certificate validation | ❌ yok — `HttpClient`/`X509` grep temiz |
| compliance claim | ❌ yok |
| external QMS API | ❌ yok |
| existing policy overwrite | ❌ yok |
| destructive subject state mutation | ❌ yok |
| scheduler registration | ❌ yok — bilinçli deferred |
| commit / push | ❌ yapılmadı — tüm değişiklikler working tree'de |

---

## 17. Remaining Gaps

1. **Scheduler registration deferred** — tenant enumeration pattern'i olmadığı için. Ayrı bir FU gerekiyor (tenant registry + per-tenant job context).
2. **Dedicated FU29 permission seed yok** — `governance-sweeps.view/run/manage` key'leri önerildi, seed edilmedi (AuthService boundary).
3. **`IAuditableCommand` implement edilmedi** — run history append-only evidence olarak yeterli görüldü; merkezi audit trail'e bağlanması ayrı iş.
4. **CAPA / signature / retention / legal hold grupları report-only** — bu domainlerde escalation/finding sidecar'ı olmadığı için. Sidecar aggregate'ler eklenirse bu gruplar escalation üretecek şekilde yükseltilebilir.
5. **`asOfDate` evaluator-backed gruplarda tam uygulanamıyor** — mevcut evaluator'lar `UtcNow` ile damgalıyor. Warning ile açıkça raporlanıyor; clock abstraction ayrı refactor.
6. **UI yok** — task kapsamı dışı.
7. **Pre-existing BackgroundJobs test failure** — `BackgroundJobContractsTests` descriptor count assertion'ı (8 vs 9) güncellenmeli; FU32 dışı.

---

## 18. Final Verdict

**PASS_WITH_GAPS**

Sweep foundation (model + orchestrator + 8 grup + manual trigger API + append-only history + idempotency + dry-run + 41 test) tam olarak teslim edildi. `PASS` yerine `PASS_WITH_GAPS` çünkü scheduler registration bilinçli olarak deferred edildi ve dedicated permission key'leri henüz seed edilmedi — ikisi de task'ın hard boundary'lerinin gerektirdiği kararlar.
