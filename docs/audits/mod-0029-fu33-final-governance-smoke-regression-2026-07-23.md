# MOD-0029-FU33 — Final Governance Smoke & Regression Audit

- **Tarih:** 2026-07-23
- **Branch:** `feature/crm-integration`
- **Tip:** Additive / non-destructive audit & verification (kod değişikliği yok, commit yok)
- **Kapsam:** MOD-0029 FU06–FU32 Document Control backend governance zinciri

---

## 1. Final Verdict

**PASS_WITH_GAPS** — Backend UI çalışmasına hazır.

FU33 kaynaklı hiçbir yeni hata üretilmedi. Çekirdek testler ve build'ler yeşil. Kalan boşlukların
tamamı FU29/FU31/FU32'de bilinçli olarak deferred edilmiş, raporlanmış maddeler veya MOD-0029 dışı
stream'lerden (CRM/HCM) gelen pre-existing failure'lar.

---

## 2. Executive Summary

| Alan | Sonuç |
|---|---|
| Platform Application testleri | **1901 / 1901 PASS** (0 fail, 0 skip) |
| Platform Eventing testleri | 56 PASS, 3 skipped, 0 fail |
| Platform BackgroundJobs testleri | 18 PASS, **2 FAIL (pre-existing, MOD-0029 dışı)** |
| Platform API build | **0 hata** (9 uyarı) |
| AuthService Application testleri | **452 / 452 PASS** |
| AuthService API build | **0 hata** (1 uyarı) |
| Gateway build | **0 hata** |
| Gateway testleri | 44 PASS, **1 FAIL (CRM/HCM port, MOD-0029 dışı)** |
| FU30 route coverage testleri | **7 / 7 PASS** |
| Live smoke (gateway 5000) | 14 endpoint → **hepsi 401, hiç 404 yok** |
| Guardrail ihlali | **Yok** |
| Unseeded runtime permission key | **Yok** |
| FU33'ün değiştirdiği runtime dosya | **Yok** (yalnız bu rapor dosyası) |

---

## 3. Working Tree / Diff Classification

Working tree'de üç ayrı stream'in değişiklikleri iç içe duruyor. En kritik bulgu: **AuthService
`DataSeeder.cs` ve Platform `DependencyInjection.cs` dosyaları hem MOD-0029 hem CRM içeriyor**,
yani stream'ler dosya bazında ayrılamıyor (bkz. §15).

### 3.1 MOD-0029 FU06–FU32 scope (yeni / untracked)

| Katman | Adet | Örnek |
|---|---|---|
| API Controllers | 20 | `DocumentManagementGovernanceSweepController.cs`, `...GovernancePolicyPackController.cs` |
| API Request Models | 17 | `Models/DocumentManagement/RetentionApiRequests.cs` |
| Application Features | 19 klasör | `Features/DocumentManagementGovernanceSweep/` |
| Domain Entities | 47 | `Entities/DocumentManagement/DocumentGovernanceSweepRun.cs` |
| Domain Enums | 19 | `Enums/DocumentManagement/GovernanceSweepEnums.cs` |
| Domain Repository interfaces | 19 | `Repositories/IDocumentManagementRetentionRepositories.cs` |
| Infrastructure Repositories | 19 | `Persistence/Repositories/DocumentManagementGovernanceSweepRepositories.cs` |
| Application testleri | 21 dosya / **812 test** | `DocumentGovernanceSweepTests.cs` |
| AuthService FU29 testi | 1 | `Mod0029Fu29PermissionSeedHardeningTests.cs` |
| Platform FU29A testi | 1 | `Mod0029Fu29aEndpointAttributionTests.cs` |
| Gateway FU30 testi | 1 | `Mod0029Fu30DocumentManagementRouteCoverageTests.cs` |
| Audit raporları | 7 | `docs/audits/mod-0029-fu2*.md`, `...fu3*.md` |

### 3.2 MOD-0029 scope (modified, mevcut dosya)

| Dosya | + satır | MOD-0029 payı |
|---|---|---|
| `Diten.Platform.Infrastructure/.../MongoDbIndexConfigurations.cs` | +728 | 309 satır document-management, CRM payı 0 |
| `Diten.Platform.Infrastructure/DependencyInjection.cs` | +87 | 63 satır document-management, CRM payı 0 |
| `Diten.Platform.Application/DependencyInjection.cs` | +86 | 66 satır document-management, 2 satır CRM |
| `AuthService/.../Seed/DataSeeder.cs` | +200 | **69 satır document-management (FU29) + 33 satır CRM (MOD-0149)** — karışık |
| `AuthService.../DocumentManagementPermissionSeedTests.cs` | +16 | Tamamı FU29 |

### 3.3 Diğer stream'ler (FU33 scope dışı)

| Grup | Dosyalar |
|---|---|
| CRM / MOD-0149 / MOD-0150 | `services/Diten.CrmService/` (yeni), `Features/Crm/`, `frontend/Diten.Web/{Controllers,Models,Views,Resources/Views,wwwroot/assets/js}/CRM/`, `execution/domains/commercial-suite/`, 30+ `docs/audits/mod-0149*/mod-0150*` |
| CRM (auth) | `DefaultRolePermissionTemplate.cs` (`crm-account`, `crm-contact` eklenmiş — **MOD-0149, FU33 değil**), `UserLookupValidationSeedTests.cs` |
| CRM (gateway) | `ocelot.json` +80 satır → **tamamı `/api/crm/*` → port 5061**; document-management route'a dokunulmamış |
| CRM (frontend) | `_LayoutTenantShell.cshtml` (13 CRM satırı), 7 dil `SharedResource.*.resx` |
| Registry / plan | `master-development-plan.md`, `module-id-registry.md`, `module-implementation-status.md` |
| Ortam / araç | `.claude/settings.local.json`, `AGENTS.md`, `watch-diten.ps1`, `watch-diten-bg.ps1`, `fleet-detached.log` |
| Şüpheli / temizlenmeli | `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/bin-verify/` (build artefaktı), `fleet-detached.log` |

---

## 4. Controller & Endpoint Coverage

31 `DocumentManagement*Controller` dosyası mevcut (11'i MOD-0028/FU01–FU05 baseline, 20'si FU06–FU32).
Tüm route prefix'leri `api/v1/document-management` altında; kaçak prefix yok.

Beklenen FU06–FU32 endpoint gruplarının **tamamı mevcut**:

| Grup | Durum | Örnek endpoint |
|---|---|---|
| master-register (FU06) | ✅ | `GET/POST/PUT document-master-register` |
| identifiers (FU07) | ✅ | `POST document-identifiers/reserve`, `.../allocate-uid` |
| lifecycle (FU08/FU08A) | ✅ | `POST .../lifecycle/transition` |
| approval-routes (FU09) | ✅ | `POST .../approval-route/resolve`, `.../approval-evidence` |
| release-gates (FU10) | ✅ | `POST .../release-gates/evaluate` |
| training (FU11) | ✅ | `POST .../training-matrix/resolve`, `.../training-readiness` |
| periodic-reviews (FU12) | ✅ | `POST .../periodic-review/initiate`, `.../extension/approve` |
| suspensions / retirements (FU13) | ✅ | `POST .../suspension-cases`, `.../retirement-cases/{id}/execute` |
| repository-assessments (FU16) | ✅ | `POST repository-assessments/{id}/evaluate` |
| controlled-copies (FU17) | ✅ | `POST .../controlled-copies/{id}/withdraw`, `.../reconcile` |
| external-documents (FU14) | ✅ | `POST external-documents/{id}/impact-assessments` |
| retention / legal-holds / disposition (FU15) | ✅ | `POST retention/evaluate`, `legal-holds/{id}/release`, `disposition-requests/{id}/approve` |
| template-variants localization (FU18) | ✅ | `PUT {id}/localization-profile`, `POST {id}/bilingual-review/complete` |
| repository-downtime-events (FU20) | ✅ | `POST {id}/temporary-issues/{id}/reconcile` |
| gdocp-corrections + policies (FU21) | ✅ | `POST gdocp-corrections/{id}/review` |
| quality-events / deviations / capa-actions (FU22) | ✅ | `POST quality-events/from-source`, `capa-actions/{id}/effectiveness` |
| signature-policies / requests / signatures (FU23) | ✅ | `POST signatures/sign`, `signatures/{id}/verify` |
| governance-policy-pack (FU31A) | ✅ | `GET default/preview`, `POST default/apply`, `GET applications`, `GET applications/{id}` |
| governance-sweeps (FU32) | ✅ | `POST run-all`, `POST preview`, 8 grup `*/run`, `GET runs`, `GET runs/{id}` |

### Destructive verb taraması

DELETE/PUT/PATCH toplam 16 endpoint. **Hiçbiri FU06–FU32 governance zincirinde değil**:

- `DELETE` (5): `access-policies/bulk`, `access-policies/{id}`, `controlled-documents/{id}`,
  `template-masters/bulk`, `template-masters/{id}`, `qms-baselines/{id}/definitions/{canonicalId}`
  → hepsi MOD-0028/FU01–FU05 baseline, hepsi soft-delete semantiği.
- `PUT`/`PATCH` (11): gerçek metadata update (master-register, external-documents,
  repository-assessments, retention-policies, localization-profile, qms-baseline definition move).
  Hard delete yok.
- **Governance policy pack ve governance sweep controller'larında hiç DELETE verb'ü yok.**

---

## 5. Permission / RBAC Audit

### 5.1 Seed ↔ runtime attribute eşleşmesi

- Kodda tanımlı `platform.document-management.*` key sayısı: **120**
- AuthService `DataSeeder` içinde seed edilen key sayısı: **119**
- **Seed edilmemiş 1 key:** `platform.document-management.collection-instances.create`
  → yalnızca `DocumentManagementInstantiationModels.cs:12`'de bir `const` olarak duruyor,
  **hiçbir `[HasPermission]` attribute'unda kullanılmıyor**. Runtime authorization boşluğu **yok**;
  ölü sabit. Temizlik veya seed'e ekleme ileri bir FU'ya bırakılabilir.
- **Ters yönde boşluk yok:** seed edilip kodda karşılığı olmayan key yok → spelling drift yok.

### 5.2 FU29A runtime attribution

`DocumentManagement*Controller` dosyalarında 214 `[HasPermission]` kullanımı, 118 farklı key.
Generic `controlled-documents.view/create` key'leri **yalnızca kendi controller'larında** kullanılıyor
(`DocumentManagementControlledDocumentsController`, `DocumentManagementFolderDocumentsController`).
FU06–FU32 governance endpoint'lerinin hiçbiri generic key'e düşmüyor → FU29A temiz.

### 5.3 Dedicated key boşluğu (bilinen, raporlanmış)

FU29 `governance-policy-pack` ve `governance-sweeps` için dedicated key seed etmedi. İki controller da
**en yakın domain key'ine fallback** yapıyor ve bunu XML doc'unda açıkça belgeliyor:

| Endpoint | Kullanılan key | Fallback tipi |
|---|---|---|
| `governance-policy-pack/default/preview`, `applications*` | `retention.view` | Domain fallback |
| `governance-policy-pack/default/apply` | `retention.manage` | Domain fallback |
| `governance-sweeps/run-all` | `retention.manage` | Domain fallback |
| `governance-sweeps/preview`, `runs`, `runs/{id}` | `retention.view` | Domain fallback |
| `governance-sweeps/periodic-reviews/run` | `master-register.periodic-review.manage` | Kendi domain'i |
| `governance-sweeps/external-documents/run` | `external-documents.manage` | Kendi domain'i |
| `governance-sweeps/temporary-instructions/run` | `master-register.suspension.manage` | Kendi domain'i |
| `governance-sweeps/downtime-temporary-issues/run` | `downtime.manage` | Kendi domain'i |
| `governance-sweeps/quality-capa/run` | `capa.view` (report-only → dar key) | Kendi domain'i |
| `governance-sweeps/signature-requests/run` | `signatures.view` (report-only → dar key) | Kendi domain'i |
| `governance-sweeps/retention-eligibility/run` | `retention.view` (report-only → dar key) | Kendi domain'i |
| `governance-sweeps/legal-hold-scope/run` | `legal-hold.view` (report-only → dar key) | Kendi domain'i |

Önerilen ileri key'ler (FU29 dokümantasyonuyla uyumlu):
`platform.document-management.governance-policy-pack.view/.apply/.manage`,
`platform.document-management.governance-sweeps.view/.run/.manage`.

### 5.4 Default rol davranışı

- `DefaultRolePermissionTemplate.AdminModules` diff'i **CRM (MOD-0149)** kaynaklı (`crm-account`,
  `crm-contact`). MOD-0029 tarafından değiştirilmemiş; `document-management` zaten bu curated
  listede değil, davranış değişmedi.
- SuperAdmin full-catalog davranışı korunuyor — `DataSeeder` diff'i saf additive (0 silinen satır),
  yeni key'ler otomatik olarak full catalog'a giriyor.
- AuthService 452 testinin tamamı yeşil (`Mod0029Fu29PermissionSeedHardeningTests` dahil).

---

## 6. Gateway Route Audit

- `ocelot.json` satır 942–978: iki route mevcut ve **commit'li** (working tree diff'inde yok):
  - `/api/v1/document-management` → `localhost:5057`
  - `/api/v1/document-management/{everything}` → `localhost:5057` (catch-all)
  - Metodlar: GET, POST, PUT, PATCH, DELETE, OPTIONS
- FU31A `governance-policy-pack/*` ve FU32 `governance-sweeps/*` catch-all tarafından **kapsanıyor**
  (live smoke ile doğrulandı, §9).
- `ocelot.json` working-tree diff'i **%100 CRM** (+80 satır, `/api/crm/accounts|contacts` → 5061).
  **FU33 scope dışı.**
- **FU33 gateway'e dokunmadı.**

### KnownDownstreamPorts pre-existing failure

`OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` **FAIL**. Sebep:

```
/api/v1/hcm/employees[/{everything}]        → tanınmayan port 5060  (HCM stream)
/api/v1/hcm/employees/drafts[/{everything}] → tanınmayan port 5060  (HCM stream)
/api/crm/accounts[/{everything}]            → tanınmayan port 5061  (CRM stream)
/api/crm/contacts[/{everything}]            → tanınmayan port 5061  (CRM stream)
```

Hiçbiri document-management değil. Testin `KnownDownstreamPorts` listesine 5060/5061 eklenmesi
HCM ve CRM stream'lerinin işi; **FU33 bilinçli olarak dokunmadı**.

---

## 7. Policy Pack Audit

`DocumentGovernancePolicyPackManifest.cs` sayımı manuel doğrulandı:

| Aile | Manifest | Beklenen |
|---|---|---|
| Retention | **20** | 20 ✅ |
| GDocP correction | **10** | 10 ✅ |
| Signature | **12** | 12 ✅ |
| **Toplam** | **42** | 42 ✅ |

Test assertion'larıyla da tutarlı (`DocumentGovernancePolicyPackTests`: `CreatedCount == 42`,
tekrar apply'da `SkippedExistingCount == 42`, preview `Items.Count == 42`).

- **Idempotent:** ✅ Seeder mevcut key'i `PolicyPackItemStatus.SkippedExisting` ile atlıyor
  ("Already exists; skipped."). Update/Replace/Overwrite çağrısı yok.
- **Apply overwrite yapmıyor:** ✅ Yalnızca eksik policy'ler create ediliyor.
- **FU31A endpoint'leri:** ✅ 4'ü de mevcut (`GET default/preview`, `POST default/apply`,
  `GET applications`, `GET applications/{id}`).
- **Preview write yapmıyor:** ✅ Saf hesaplama; policy de history de yazmıyor.
- **Apply history yazıyor:** ✅ Append-only `DocumentGovernancePolicyPackApplication` satırı;
  tekrar apply 0 created ile yeni bir satır ekliyor, eskisini güncellemiyor.
- **Transaction yok:** ⚠️ Apply çok-belgeli yazımı transaction/session olmadan yapıyor (bilinen gap).
- **`AppliedByRole` her zaman null:** ⚠️ Entity alanı ve API modeli var, hiçbir yerde set edilmiyor.

---

## 8. Governance Sweep Audit

- **Endpoint'ler:** ✅ `POST run-all`, `POST preview`, 8 grup `run` endpoint'i, `GET runs`, `GET runs/{id}`.
- **Tenant zorunlu:** ✅ `if (!tenantContext.IsResolved) → 400 TenantRequired`; ardından
  `TenantGuard.RequireTenant(tenantContext)`. `TenantId` hiçbir controller'da client payload'ından
  veya `X-Tenant-Id` header'ından okunmuyor (`FromHeader` kullanımı 0).
- **dryRun hiçbir şey yazmıyor:** ✅ Kod yorumu ve akış doğrulandı — dry run'da item outcome
  `DryRun` olarak işaretleniyor ve **history satırı bile yazılmıyor** (`Guid.Empty` run id ile
  response dönüyor). Ayrıca `AsOfDate` yalnızca `!DryRun` yolunda etkili.
- **Run history append-only:** ✅ Her run yeni `DocumentGovernanceSweepRun` insert'ü; update yok.
- **Grup izolasyonu:** ✅ Bir grup patlarsa `PartialFailure` warning'i ile diğerleri devam ediyor.
- **Scheduler registration deferred:** ✅ Sweep feature'ında `RecurringJob`, `AddHostedService`,
  `IHostedService`, `BackgroundService`, `Cron` referansı **yok**. `TriggerType` sabit `Manual`.
- **Recurring job eklenmedi:** ✅ `PlatformRecurringJobRegistrar.cs` ve `BackgroundJobs.Tests`
  git'te **hiç değişmemiş** (`git status --short` boş).
- **Auto-* davranışı yok:** ✅ Sweep servisinde auto-delete / auto-close / auto-approve /
  auto-effective / auto-disposition / auto-sign / auto-retire çağrısı yok. Yazan tek yol,
  FU12/FU13/FU20'nin önceden var olan idempotent escalation evaluator'ları.
- **Report-only gruplar:** quality-capa, signature-requests, retention-eligibility, legal-hold-scope
  → sadece rapor üretiyor, subject state'e dokunmuyor (controller XML doc'unda da beyan edilmiş).

---

## 9. Guardrail Verification

| Guardrail | Sonuç | Kanıt |
|---|---|---|
| Hard delete / purge | ✅ Yok | `DeleteOneAsync\|DeleteManyAsync\|DropCollection\|HardDelete` → tüm DM Application feature'ları ve DM repository'lerinde 0 eşleşme |
| Raw bytes Mongo'ya yazma | ✅ Yok | DM governance feature'larında byte/stream persist yok |
| Frontend'de direct 5057 | ✅ Yok | Yalnızca `appsettings*.json` `PlatformServiceUrl` (server-side MVC proxy config, pre-existing). Client JS'te 5057 yok |
| Client TenantId / `X-Tenant-Id` | ✅ Yok | DM controller'larında `X-Tenant-Id`/`FromHeader` 0 eşleşme |
| Certificate validation / X509 | ✅ Yok | ElectronicSignature feature'ında `X509\|CertificateValidation` 0 eşleşme |
| External QMS API / yeni HttpClient | ✅ Yok | DM feature'larında `HttpClient` 0 eşleşme |
| MOD-0023 workflow runtime | ✅ Yok | Governance feature'larında `Workflow` 0 eşleşme |
| Auto close/approve/effective/sign/disposition | ✅ Yok | §8 |
| Public file URL / wwwroot upload | ✅ Yok | Governance zincirinde dosya yükleme yolu yok |
| Unseeded permission key (runtime) | ✅ Yok | §5.1 |
| Route duplicate / conflict | ✅ Yok | 31 controller, prefix çakışması yok, live smoke'ta 404 yok |
| Beklenmeyen scheduler registration | ✅ Yok | §8 |
| MOD-0028 baseline mutasyonu | ✅ Yok | `qms-baselines`, `collection-definitions`, `instantiations` endpoint'leri ve permission'ları değişmemiş |

### Live smoke (gateway `localhost:5000`, unauthenticated)

14 endpoint denendi, **hepsi `401`, hiçbiri `404` değil** → route'lar canlıda erişilebilir,
authorization fail-closed:

```
GET  /api/v1/document-management/governance-policy-pack/default/preview  -> 401
GET  /api/v1/document-management/governance-policy-pack/applications     -> 401
GET  /api/v1/document-management/governance-sweeps/runs                  -> 401
POST /api/v1/document-management/governance-sweeps/preview               -> 401
POST /api/v1/document-management/governance-sweeps/run-all               -> 401
GET  .../document-master-register | signature-policies | quality-events
     | legal-holds | retention-policies | gdocp-corrections
     | external-documents | repository-assessments
     | repository-downtime-events                                        -> 401 (hepsi)
```

Token alınmadı; yazan hiçbir çağrı yapılmadı. Tenant verisi değiştirilmedi.

---

## 10. Build & Test Results

> **Not:** Yerel fleet (Diten.Platform.API pid 2008, Diten.AuthService.Api pid 26128) çalıştığı için
> `bin/Debug` kilitliydi. Görev talimatındaki fallback uygulandı: tüm build ve testler
> `-o ./.tmp/verify-fu33-*` izole çıktı dizinlerine yönlendirildi. Fleet durdurulmadı, hiçbir
> çalışan servise müdahale edilmedi.

| Komut | Sonuç |
|---|---|
| `dotnet build Diten.Platform.API -o .tmp/verify-fu33-platform-build` | **0 hata**, 9 uyarı, 36.4 s |
| `dotnet test Diten.Platform.Application.Tests -o .tmp/verify-fu33-app-tests` | **Başarılı: 1901, Başarısız: 0, Atlanan: 0** |
| `dotnet test Diten.Platform.Eventing.Tests -o .tmp/verify-fu33-ev-tests` | Başarılı: 56, Başarısız: 0, Atlanan: 3 |
| `dotnet test Diten.Platform.BackgroundJobs.Tests -o .tmp/verify-fu33-bg-tests` | **Başarısız: 2**, Başarılı: 18 (§11) |
| `dotnet build Diten.AuthService.Api -o .tmp/verify-fu33-auth-build` | **0 hata**, 1 uyarı |
| `dotnet test Diten.AuthService.Application.Tests -o .tmp/verify-fu33-auth-tests` | **Başarılı: 452, Başarısız: 0** |
| `dotnet build Diten.ApiGateway -o .tmp/verify-fu33-gateway-build` | **0 hata**, 2.4 s |
| `dotnet test Diten.ApiGateway.Tests -o .tmp/verify-fu33-gw-tests` | **Başarısız: 1**, Başarılı: 44 (§11) |

Hedefli test sınıfları — hepsi 1901'lik Application Tests içinde ve **yeşil**
(`DocumentManagement/` altında 21 dosya, toplam **812 test**):

`DocumentGovernanceSweepTests`, `DocumentGovernancePolicyPackApplicationTests`,
`DocumentGovernancePolicyPackTests`, `DocumentElectronicSignatureTests`,
`DocumentQualityEventCAPATests`, `DocumentGDocPCorrectionTrailTests`,
`DocumentRetentionLitigationHoldTests`, `DocumentDowntimeTemporaryIssueTests`,
`DocumentPeriodicReviewTests`, `ExternalDocumentRegisterTests`,
`DocumentSuspensionRetirementTests`, `DocumentControlledCopyTests`,
`DocumentRepositoryAssessmentTests`, `DocumentTrainingMatrixTests`,
`DocumentReleaseGateTests`, `DocumentApprovalRouteTests`, `DocumentLifecycleStatusTests`,
`DocumentIdentifierAllocationTests`, `DocumentMasterRegisterTests`,
`TemplateVariantLocalizationTests`, `Mod0029Fu29aEndpointAttributionTests`.

`Mod0029Fu30DocumentManagementRouteCoverageTests`: **7 / 7 PASS** (gateway suite içinde).
`Mod0029Fu29PermissionSeedHardeningTests`: PASS (AuthService suite içinde).

Not: istenen isimlerden ikisi repoda farklı adla var —
`DocumentLifecycleTests` → `DocumentLifecycleStatusTests`,
`DocumentRepositoryDowntimeTests` → `DocumentDowntimeTemporaryIssueTests`. İçerik karşılığı mevcut.

---

## 11. Pre-existing / Out-of-scope Failures

Toplam 3 başarısız test. **Hiçbiri MOD-0029 kaynaklı değil, hiçbiri FU33 tarafından üretilmedi.**

### 11.1 `Diten.Platform.BackgroundJobs.Tests` — 2 FAIL (pre-existing)

- `BackgroundJobContractsTests.Platform_registrar_returns_standard_descriptors_disabled_by_default`
- `BackgroundJobContractsTests.Platform_registrar_enables_descriptor_from_configuration`
- Hata: `Assert.Equal() Failure — Expected: 8, Actual: 9`
- **Kanıt:** `PlatformRecurringJobRegistrar.cs` ve `BackgroundJobs.Tests/` klasörü working tree'de
  **hiç değişmemiş** (`git status --short` bu yollar için boş). 9. descriptor MOD-0029'dan önce
  eklenmiş; FU32 hiçbir recurring job eklemedi.
- **Sınıflandırma:** pre-existing, MOD-0029 dışı. FU32 kapsam dışı olarak zaten raporlamıştı.
  FU33 de bilinçli olarak **düzeltmedi** (test assertion'ı değiştirmek 9. descriptor'ın sahibinin
  kararı; sahibi belirlenmeden değiştirmek yanlış olur).

### 11.2 `Diten.ApiGateway.Tests` — 1 FAIL (diğer stream)

- `OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet`
- Sebep: HCM port 5060 (4 route) + CRM port 5061 (4 route) `KnownDownstreamPorts`'ta yok.
- **Sınıflandırma:** HCM/CRM stream sorumluluğu. document-management route'ları (5057) tanınıyor
  ve testi geçiyor.

---

## 12. Remaining Gaps

| # | Gap | Kaynak FU | Durum |
|---|---|---|---|
| 1 | `governance-policy-pack` için dedicated permission key yok; `retention.view/.manage` fallback | FU29 / FU31A | Bilinçli deferred, belgelenmiş |
| 2 | `governance-sweeps` için dedicated permission key yok; domain fallback | FU29 / FU32 | Bilinçli deferred, belgelenmiş |
| 3 | Scheduler registration yok (güvenli tenant enumeration pattern'i olmadığı için) | FU32 | Bilinçli deferred |
| 4 | quality-capa / signature-requests / retention-eligibility / legal-hold-scope sweep'leri report-only | FU32 | Tasarım kararı |
| 5 | Policy pack apply'da transaction/session yok (kısmi apply riski) | FU31A | Açık gap |
| 6 | `AppliedByRole` hiçbir zaman set edilmiyor (her zaman null) | FU31A | Açık gap |
| 7 | `IAuditableCommand` governance feature'larında wire edilmemiş | FU31A/FU32 | Açık gap |
| 8 | `platform.document-management.collection-instances.create` seed edilmemiş ölü sabit | FU29 | Zararsız; temizlik |
| 9 | UI yok — tüm FU06–FU32 yüzeyi yalnız API | — | Sıradaki iş |
| 10 | `BackgroundJobs` descriptor count testi (8 vs 9) | Pre-existing | MOD-0029 dışı |
| 11 | Gateway `KnownDownstreamPorts` 5060/5061 eksik | HCM/CRM | MOD-0029 dışı |

---

## 13. Risk Matrix

### Release-blocking
**Yok.** Çekirdek build ve test yeşil; destructive davranış yok; unseeded runtime key yok;
route'lar canlıda erişilebilir ve fail-closed.

### UI-blocking
**Yok.** UI'ın ihtiyaç duyduğu her endpoint mevcut, gateway catch-all ile erişilebilir ve
permission attribute'ları seed edilmiş key'lere bağlı.

| Risk | Şiddet | Not |
|---|---|---|
| Policy pack / sweep ekranı `retention.*` permission'ı olmayan role görünmez | Orta | UI'da yetki gating'i `retention.view`/`retention.manage` üzerinden kurgulanmalı; ileride dedicated key'e geçince UI tarafı da güncellenecek |

### Operational
| Risk | Şiddet | Etki |
|---|---|---|
| Sweep yalnızca manuel tetikleniyor; kimse çağırmazsa overdue/expired escalation'lar oluşmaz | **Yüksek** | Scheduler gelene kadar operasyonel bir prosedür (manuel tetikleme) gerekir |
| Policy pack apply transaction'sız | Orta | Ortada patlarsa kısmen apply edilmiş kalır; ancak idempotent olduğu için tekrar apply düzeltir |
| `AppliedByRole` null + `IAuditableCommand` yok | Orta | GxP denetim izi eksik kalır (kim/hangi rol uyguladı) |
| Report-only sweep'ler aksiyon üretmiyor | Düşük | Bilinçli; aksiyon insan kararına bırakılmış |

### Deferred
Scheduler registration, dedicated permission key'ler, e-signature provider, certificate validation,
external QMS entegrasyonu, MOD-0023 workflow runtime — hepsi bilinçli olarak kapsam dışı.

### Pre-existing / unrelated
BackgroundJobs descriptor count (8 vs 9), gateway `KnownDownstreamPorts` (5060/5061),
CRM/HCM stream'lerinin `ocelot.json` / `DataSeeder` / `DefaultRolePermissionTemplate` diff'leri.

---

## 14. UI Readiness Decision

**BACKEND READY FOR UI — evet.**

Gerekçe:
1. FU06–FU32 endpoint grupları eksiksiz, hepsi `/api/v1/document-management/*` altında.
2. Gateway catch-all route'u commit'li ve canlıda çalışıyor (14/14 endpoint 401, 0 adet 404).
3. Runtime attribute'larındaki tüm permission key'leri seed edilmiş; fail-closed authorization.
4. Platform Application 1901/1901, AuthService 452/452, FU30 route coverage 7/7 yeşil.
5. Kalan boşlukların hiçbiri UI'ı bloke etmiyor.

UI ekibine iletilecek iki not:
- Policy pack ve sweep ekranlarının yetki gating'i **`retention.view` / `retention.manage`**
  üzerinden yapılmalı (dedicated key gelene kadar).
- Sweep'ler zamanlanmıyor; UI "Run now" / "Preview" aksiyonlarını kullanıcıya açık bir şekilde
  manuel operasyon olarak sunmalı, "otomatik çalışıyor" izlenimi vermemeli.

---

## 15. Commit Readiness / Suggested Commit Groups

> **Commit atılmadı, push yapılmadı. Tüm değişiklikler working tree'de duruyor.**

⚠️ **Kritik uyarı:** Aşağıdaki Grup 5 (`DataSeeder.cs`) ve Grup 1 (`Application/DependencyInjection.cs`)
**hem MOD-0029 hem CRM içeriyor**. Bu iki dosya olduğu gibi commit edilirse MOD-0029 commit'i CRM
değişikliğini de taşır. Temiz ayrım isteniyorsa `git add -p` ile hunk seçimi gerekir.

| # | Grup | Dosya | Risk | Bağımlılık |
|---|---|---|---|---|
| 1 | MOD-0029 backend governance core | 20 controller + 17 API model + 17 feature klasörü + 47 entity + 19 enum + 19 repo interface + 19 repo impl + `Infrastructure/DependencyInjection.cs` + `MongoDbIndexConfigurations.cs` + `Application/DependencyInjection.cs` | Düşük — %99 yeni dosya | Grup 5'ten (permission seed) sonra deploy edilmeli |
| 2 | MOD-0029 policy pack | `Features/DocumentManagementGovernancePolicyPack/` (6 dosya), `DocumentGovernancePolicyPackApplication.cs`, `GovernancePolicyPackEnums.cs`, `IDocumentManagementGovernancePolicyPackRepositories.cs`, `DocumentManagementGovernancePolicyPackRepositories.cs`, `DocumentManagementGovernancePolicyPackController.cs` | Düşük | Grup 1 (retention entity/repo) |
| 3 | MOD-0029 sweep | `Features/DocumentManagementGovernanceSweep/` (3 dosya), `DocumentGovernanceSweepRun.cs`, `GovernanceSweepEnums.cs`, sweep repo interface + impl, `DocumentManagementGovernanceSweepController.cs` | Düşük | Grup 1 (FU12/FU13/FU20 evaluator'ları) |
| 4 | MOD-0029 testleri | `tests/.../DocumentManagement/` 21 dosya (812 test) + `Authorization/Mod0029Fu29aEndpointAttributionTests.cs` | Yok | Grup 1–3 |
| 5 | AuthService permission seed | `DataSeeder.cs` (**yalnız 69 doc-management satırı**), `DocumentManagementPermissionSeedTests.cs` | **Orta — CRM ile karışık dosya** | Grup 1'den **önce** deploy |
| 6 | Gateway route coverage testi | `gateway/Diten.ApiGateway.Tests/Mod0029Fu30DocumentManagementRouteCoverageTests.cs` | Yok | `ocelot.json` route'u zaten commit'li |
| 7 | MOD-0029 audit dokümanları | `docs/audits/mod-0029-fu23*.md`, `fu29*.md`, `fu29a*.md`, `fu30*.md`, `fu31*.md`, `fu31a*.md`, `fu32*.md`, `fu33*.md` (bu rapor), `gmg-qms-sop-0001-*.md` | Yok | — |
| 8 | Pre-existing / diğer stream | `services/Diten.CrmService/`, `Features/Crm/`, `frontend/**/CRM/`, `ocelot.json`, `DefaultRolePermissionTemplate.cs`, `UserLookupValidationSeedTests.cs`, `_LayoutTenantShell.cshtml`, 7 `SharedResource.*.resx`, `execution/domains/commercial-suite/`, 30+ `mod-0149*/mod-0150*` audit | Orta | **Ayrı commit — CRM stream sahibi** |
| 9 | Şüpheli / commit edilmemeli | `services/Diten.Building.Blocks/.../bin-verify/` (build artefaktı), `fleet-detached.log`, `.claude/settings.local.json`, `watch-diten*.ps1`, `AGENTS.md` | Düşük | `.gitignore` adayı |

Registry/plan dosyaları (`master-development-plan.md`, `module-id-registry.md`,
`module-implementation-status.md`) hem MOD-0029 hem CRM satırları içerebilir — commit öncesi
hunk düzeyinde bakılmalı.

---

## 16. Files Changed By FU33

| Dosya | Tip |
|---|---|
| `docs/audits/mod-0029-fu33-final-governance-smoke-regression-2026-07-23.md` | **Yeni** — bu rapor |

**Başka hiçbir dosya değiştirilmedi.** Runtime kodu, test kodu, seed, gateway config, frontend —
hepsi FU33 öncesindeki haliyle. Build/test çıktıları `.tmp/verify-fu33-*` altına yazıldı;
`.tmp/` git tarafından ignore ediliyor (`git status --short` bu yollar için boş).

---

## 17. Confirmations

| Onay | Durum |
|---|---|
| FU33 AuthService seed'ini değiştirmedi | ✅ Onaylandı — `DataSeeder.cs` ve `DefaultRolePermissionTemplate.cs` FU33 öncesi hâliyle |
| FU33 Gateway'i değiştirmedi | ✅ Onaylandı — `ocelot.json` diff'i %100 CRM stream |
| FU33 frontend'i değiştirmedi | ✅ Onaylandı |
| MOD-0028 baseline mutasyonu yok | ✅ Onaylandı — qms-baselines / collection-definitions / instantiations yüzeyi değişmemiş |
| Destructive mutation / delete yok | ✅ Onaylandı — governance controller'larında DELETE verb'ü yok; DM feature/repo'larında hard delete çağrısı yok |
| Direct 5057 / client TenantId yok | ✅ Onaylandı — client JS'te 5057 yok; DM controller'larında `X-Tenant-Id`/`FromHeader` yok |
| Mevcut tenant verisi değiştirilmedi | ✅ Onaylandı — live smoke yalnız unauthenticated GET/POST, hepsi 401'de durdu; DB'ye yazan hiçbir çağrı yapılmadı |
| DB cleanup / hard delete yapılmadı | ✅ Onaylandı |
| Scheduler registration eklenmedi | ✅ Onaylandı |
| Business logic / state machine rewrite yok | ✅ Onaylandı |
| Test assertion düzeltmesi yapılmadı | ✅ Onaylandı — BackgroundJobs 8 vs 9 assertion'ı **bilinçli olarak dokunulmadan** bırakıldı |
| Commit / push yapılmadı | ✅ Onaylandı |
| **Final verdict** | **PASS_WITH_GAPS** |

---

## 18. Next Recommended Step

1. **Commit ayrımı** — MOD-0029 gruplarını (1–7) CRM stream'inden (8) ayırarak commit'le.
   `DataSeeder.cs` ve `Application/DependencyInjection.cs` için `git add -p` şart.
   Grup 9'daki artefaktları (`bin-verify/`, `fleet-detached.log`) commit etme, `.gitignore`'a ekle.
2. **UI fazına geç** — MOD-0029 Document Control admin/tenant ekranları. Module pack kapısı:
   UI çalışması için `approved`/`ready-for-dev` statüsünde bir module pack gerekiyor
   (orchestrator Kural 2). Yetki gating'i `retention.view`/`retention.manage` üzerinden.
3. **FU34 adayı (ayrı FU)** — dedicated governance permission key'leri:
   `governance-policy-pack.view/.apply/.manage` + `governance-sweeps.view/.run/.manage`.
   AuthService seed + controller attribute güncellemesi + alias map geçişi birlikte.
4. **FU35 adayı (ayrı FU)** — scheduler registration; ön koşul güvenli tenant enumeration pattern'i.
   Bu çözülmeden recurring job eklenmemeli.
5. **Ayrı stream işleri** — gateway `KnownDownstreamPorts`'a 5060/5061 eklenmesi (HCM/CRM sahibi),
   `BackgroundJobs` descriptor count testinin 9'a güncellenmesi (9. descriptor'ın sahibi).
