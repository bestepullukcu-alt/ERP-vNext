# WP-MVP1-SYNC1 — MVP-1 dalı ← origin/main senkronu: kanıt (2026-10-12)

> Prompt: CT-MVP1-SYNC1 v1 → v1.1 (port eki) → v1.2 (CT kararı, BLOCKED çözümü). Ajan sonucu, **CT kabulü bekliyor**.
> Önceki tur: `mvp1-sync1-blocked-2026-10-12.md` (BLOCKED; sahip kararı B ile çözüldü).
> Betikler: `mvp1-sync1-scripts-2026-10-12.md`. Ekran görüntüleri: `mvp1-sync1-shots/`.

## 0. Sonuç

**PASS (CT kabulü bekliyor).** MVP-1 dalı `origin/main` ile senkron: `git rev-list --count HEAD..origin/main` = **0**
(ölçüm 2026-10-12, main = `a814bad0f`, değişmedi). Merge'den kaynaklanan kırmızı test yok. Merge öncesi ve sonrası iki tarafta
da aynı olan kırmızılar §6.2'de "main'de de kırmızı" olarak ayrıldı.

| | |
|---|---|
| Çalışma dalı | `feature/mvp1-sync1` (worktree `C:\Users\Ali\erp-vnext-wt-mvp1-sync1`) |
| Taban | `98da00832` + MVP-1 belge commit'i `630970e20` (`247187de7` ile alındı) |
| main | `a814bad0f` — 395 commit; merge-base `90fde0846` |
| Commit'ler | `2d935c67c` merge · `2fdedc988` OD-P1 port · `c7314b856` test yardımcısı Windows koruması · `ffa92ec8d` K3 (e) testi · bu belge |
| Kabulde | MVP-1 dalına fast-forward (CT). Push / PR yok. |

## 1. CT kararlarının uygulanması (v1.2)

| # | Karar | Uygulama | Kanıt |
|---|---|---|---|
| 1 | Sahip kararı **B** — eski kayıtta main'in davranışı | Bizim `IsStarterAsync` (Start-log yolu) silindi; tek uygulama main'in `IsStarter`'ı. Testimiz `Fx_an_instance_started_before_the_fix_still_blocks_its_starter_through_the_start_log` silindi. **Sahip kararıyla FX kabul maddesi daraltıldı: eski açık görevlerde başlatan kendi onayını verebilir (bilinen açık, sahip kabul etti).** MOD-0175 §19 SYNC1 notu yazıldı. | main testi `An_instance_from_before_the_field_keeps_the_old_comparison` 200 ✅; yeni kayıt SoD: `Fx_a_tenant_user_who_started…` 409 ✅, `Whoever_started_an_instance_cannot_approve_it…` 409 ✅ |
| 2 | Tek aktör bağlama: bizim `WorkflowActorBinding` | main'in `TryBindActor`'ı silindi; `WorkflowActorMismatch` tek tanım (`WorkflowModels.cs:73`). Yeni testler: main'in kabul ettiği her girdi (boş, null, boşluk, D / N / B / P / büyük harf / boşluklu GUID) beş aksiyonda kabul edilir ve çağırana bağlanır; reddettikleri (başka GUID, `user:` önekli, metin, başkasının e-postası) 403. Tek fark: çağıranın kendi e-postası (bizde kabul, main'de ret) — main'in hiçbir testi bunu istemiyor. | `WorkflowActorBindingTests` 29/29 (yeni `Sync1_…` 13 vaka); main B4: `Naming_someone_else_as_the_actor_is_403…` 5/5, `The_assigned_person_acts_with_no_actor…` 2/2 — **değişmeden** yeşil |
| 3 | `StartedByUserId` = main'in `Guid?`; toleranslı okuma | `WorkflowInstanceBsonMap` (Infrastructure/Persistence) + `LenientNullableGuidSerializer`: BSON string / Guid / null okunur, Standard Guid yazılır; `PlatformBsonConventions.Register` içinden kaydedilir (üretim ve test aynı yol). Toplu göç yok. | `Sync1_an_instance_whose_starter_id_was_written_as_text…` (metin okunur, başlatan 409), `Sync1_another_approver_decides…` (kayıt Guid olarak geri yazılır), `Sync1_a_starter_id_text_that_is_not_a_guid…` (geçersiz metin → null → eski karşılaştırma) |
| 4 | `SchemaProfile` 12 çakışması — önce ölç | **Ölçüm: yalnız kod içi seçim.** `SchemaProfile` yalnız `PlatformSchemaManifest.For()` filtresinde, `KnownProfiles` sıralamasında ve hata metninde kullanılıyor; hiçbir belgeye, indeks adına ya da şema kayıt koleksiyonuna yazılmıyor (`SchemaCollection.Profile` bellek içi). → main'in `TimeEntry = 12`, bizim `ESignature = 13`. | `SchemaProfile.cs:50`; manifest testleri yeşil |
| 5 | Taban güncellemesi | `git merge feature/sce/mod-0173-inventory-ledger-valuation` (`630970e20`, belge) → `git fetch origin main` → `git merge origin/main --no-ff` | §0 |
| 6 | Port eki OD-P1 | Inventory 5066 → **5067**: ocelot'ta bizim 8 rota (`/api/inventory`, `/api/locations`, `/api/trace`, `/api/quarantine` + `{everything}`); main'in `/api/shipment-bundle` (2 rota) 5066'da kaldı. Gateway testleri, Inventory `launchSettings` / `appsettings`, runtime-probe betiği, 5 çalıştırma betiği, Web guard testi ve yorumları, `domain-config.md`, DCP-009. | gateway: 5067 bilinen port listesinde, rota testleri 5067 ✅; runtime: yığının gateway'i `/api/locations` / `/api/quarantine` → kendi Inventory'ye ulaştı (§8) |
| 7 | Sabotaj (d) yeni kayıt için | §7 (d) | — |

**Port ekinde dokunulmayanlar (listele):** `AGENTS.md` §3 port tablosu (satır 88: "Inventory Service 5066", satır 93 notu) ve `CLAUDE.md`
"Portlar" bloğu (5066'yı listelemiyor; 5067 eklenmeli) — CT günceller. MDM R-12 doğrudan çağrısı (5067) — CT bildirir. Pack geçmiş
notlarındaki 5066 anmaları (MOD-0173 / 0174 / 0175 / FU01 / FU02) tarih kaydıdır, değiştirilmedi. `DCP-009` satır 98 (OD-5 karar metni) tarihtir.

## 2. Çakışma tablosu (26 dosya, her parça elle)

main'in değişikliği `git log 90fde0846..origin/main -- <dosya>` ile ölçüldü.

| Dosya | Bizim değişikliğimiz | main'in değişikliği | Çözüm | Koruyan testler |
|---|---|---|---|---|
| `WorkflowDefinitionsController.cs` (5 parça) | FX `WorkflowActorBinding.Bind` her task aksiyonunda | `59761261c` (APPROVAL-STATUS-01 v2 B4) `TryBindActor` | **tek uygulama: bizimki**; `TryBindActor` ve çift `using` silindi | `WorkflowActorBindingTests` 29, main B4 7 |
| `WorkflowModels.cs` (otomatik merge) | `WorkflowActorMismatch` | aynı sabit | çift tanım (derleme hatası) → main'in belgeli tanımı kaldı | derleme |
| `Approve/Reject/Cancel/Delegate/RequestInfoWorkflowTaskHandler.cs` | `ActorId ?? string.Empty` | `59761261c` `ActorId!` | bizimki (null güvenli; controller her zaman doldurur) | Workflow testleri |
| `StartWorkflowInstanceHandler.cs` | `StartedByUserId = UserId.ToString()` | `94731f870` `= UserId` (Guid) | main'inki (tip birliği) | FX + B2 |
| `WorkflowInstance.cs` | `string? StartedByUserId` (FX) | `94731f870` `Guid? StartedByUserId` | **main'in `Guid?`'ı** + toleranslı okuma (karar 3) | Sync1 3 test |
| `WorkflowTaskTransitionSupport.cs` | FX `IsStarterAsync` (Start-log fallback); E4 imza kapısı | `94731f870` / `59761261c` `IsStarter`; `092861e6e` başlatana devretme reddi | **tek uygulama: main'in `IsStarter`'ı** (karar 1 / B); `IsStarterAsync` silindi; E4 kapısı yerinde | E4 27, FX 5, `Delegate_to_the_starter…` |
| `WorkItemProjectionService.cs` | E4/E5W2 `WithSignature` sarmalı | `67b2e0b5c` / `3ee1442e8` / `07bc157ca` / `092861e6e` adım bağlamı, `DelegationExcludedTargets`, `BuildApprove(task, instance, …)` | birleşim: main'in imzası + `WithSignature(BuildApproveCore(task, instance, …))` | `ApprovalStepContext` 35, E4 |
| `WorkAggregationModels.cs` | `SignatureRequired / SignatureMeaning / SignatureActionCode` | `AcceptsNote`, `RequiresTargetPerson`, `ExcludedTargetPrincipalIds` | birleşim (hepsi sonda, varsayılanlı, null'da yazılmaz) | WorkCenter / WorkAggregation |
| `WorkItemActionDispatchModels.cs` | `SignatureId` | `13dd4321f` `PlannedStartAt`, `PlannedDurationMinutes` | birleşim | WorkAggregation |
| `PlatformSchemaManifest.cs` | `ESignatureCollections` | `145059fbc` `TimeEntryCollections` | birleşim | manifest testleri |
| `PlatformSchemaMigrations.cs` | E5B eski tekil indeks düşürme | `2125732dc` T1a indeks düşürme | birleşim (ikisi de manifestten önce) | E5B index migration, TimeEntry |
| `SchemaProfile.cs` | `ESignature = 12` | `145059fbc` `TimeEntry = 12` | `TimeEntry = 12`, `ESignature = 13` (karar 4) | manifest |
| `PlatformRecurringJobRegistrar.cs` | e-imza talep süresi taraması | `145059fbc` / `c1d05f9a6` / `3c2ffe7dd` / `e70c226e9` 3 timesheet işi | birleşim | BackgroundJobs |
| `DefaultRolePermissionTemplate.cs` | CT-D10 `"esign"` + `DefaultSignerPermissions` | `18c5ab23a` Q358 `"shipment-tracking-pod"` (geçici, 2026-11-03) | birleşim; main'in Q358 yorumu korundu | Auth 1154/1154 |
| `WorkCenterNext/app.js` (2 parça) | E5W1/E5W2 `signatureForApproval` + `body.signatureId` | `c8f85abd3` timer bloğu; `07bc157ca` / `092861e6e` BL-491 `targetPrincipalId` | birleşim (iki blok yan yana; gövdeye iki satır); `node --check` OK | Web 1429/1429; runtime §8 |
| `SharedResource.{ar,en,es,fr,ru,tr,zh}.resx` | e-İmza + karantina nav, DisplayContext anahtarları (22/dil) | MVP-6 / CRM / TimeEntry anahtarları (42–43/dil) | anahtar birleşimi; yinelenen anahtar yok (ar/es/fr/ru/zh 546, en/tr 579). **Aynı anahtar farklı metin: 1** — `fr` `Nav.Domain.SUPPLYCHAINEXECUTION`: main "Exécution de la chaîne d’approvisionnement" kaldı; bizimki "Exécution de la chaîne logistique" | Web |
| `DCP-009-supply-chain-inventory.md` | Inventory servis satırı | `eb1306590` runtime_code_allowed notu | iki satırın birleşimi | — |

Sözleşme dosyalarında (`docs/analysis/contracts/**`) çakışma yok.

## 3. Korunan davranışlar — merge sonrası yerleri (HEAD)

**Bizim (MVP-1, CT-ACCEPTED):**

| Davranış | Dosya:satır |
|---|---|
| FX aktör = oturumdaki kullanıcı | `WorkflowActorBinding.cs:26` · `WorkflowDefinitionsController.cs:205, 223, 241, 259, 277` |
| FX SoD kullanıcı kimliğiyle (yeni kayıt) | `StartWorkflowInstanceHandler.cs:165` → `WorkflowTaskTransitionSupport.cs:124` (`IsStarter`, `:822`) |
| FX 7 runtime anahtarı Tenant | `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/TenantRouteScopeCorrections.cs:88-94` |
| E4 imza gerekli adımda `signatureId` yoksa 409 | `WorkflowTaskTransitionSupport.cs:136-143`; bağlama `:149` (`IESignatureGate`, `ESignatureGate.cs:33`) |
| E4 transition log'da `SignatureId` | `WorkflowTaskTransitionSupport.cs:247, 289` |
| E4/E5W2 iş öğesinde `signatureRequired / signatureMeaning / signatureActionCode` | `WorkItemProjectionService.cs:258, 264-271`; `WorkAggregationModels.cs` (DTO sonu) |
| `workflow.task.approve` aksiyon kaydı | `WorkflowSignableActions.cs:15` |
| WorkCenter dispatch `SignatureId` | `WorkItemActionDispatchModels.cs:147` |
| E5B eski tekil indeksi düşürme | `PlatformSchemaMigrations.cs:118`; `SchemaProfile.cs:50` (`ESignature = 13`); `PlatformSchemaManifest.cs:50` |
| E5W1/E5W2 WorkCenter imza penceresi | `WorkCenterNext/app.js:8096` (`signatureForApproval`), `:8108` (`actionCode`), `:8213` (`body.signatureId`) |
| e-İmza nav anahtarları | `SharedResource.tr.resx:1136-1143` (+ 6 dil) |
| CT-D10 + CT-Q15 imzacı varsayılanları | `DefaultRolePermissionTemplate.cs:43` (`"esign"`), `:54` (`DefaultSignerPermissions`) |
| ROLE1 şablon yalnız rol oluşturulurken | `DefaultRoleProvisioningRule.cs:20` |
| MOD-0175 karantina nav anahtarları | `SharedResource.tr.resx:1131-1135` (+ 6 dil) |
| MOD-0175 WorkCenter DisplayContext etiketleri | `QuarantineWorkflow.cs:119-126`, `QuarantineWorkflowPort.cs:26` (runtime: başlık / özet / etiketler §8) |

**main'in (ölçülüp aynı ciddiyetle korunan):**

| Davranış | Yer | Test (merge sonrası) |
|---|---|---|
| APPROVAL-STATUS-01 B1: onaylanmış örnek onaylı sayılır | `WorkflowInstanceOutcome` | `After_a_REAL_approval…`, `Only_Approved_or_Completed…` ✅ |
| B2: başlatan onaylayamaz (kullanıcı kimliğiyle) + eski kayıt eski karşılaştırma | `WorkflowTaskTransitionSupport.cs:822` | `Whoever_started…`, `A_task_approval_the_creator_routed_to_themselves…`, `An_instance_from_before_the_field…` ✅ |
| B3: genel iptal yükseltilmiş onayı geri çekmez | `CancelAsync(AllowEscalated)` | `The_public_cancel_endpoint_refuses_an_escalated_approval_task` ✅ |
| B4: aktör imzalı kullanıcı | `WorkflowActorBinding` (tek uygulama) | `Naming_someone_else…` 5/5, `The_assigned_person_acts…` 2/2 ✅ |
| B2 takip: başlatan WorkCenter'da kapalı onay görür | `WorkItemProjectionService.cs` (`SelfApprovalNotAllowed`) | `The_starter_sees_approve_disabled…` ✅; runtime W1 |
| WCN-APPROVAL-UX-01: adım / aday pozisyonlar / isteğe bağlı not | projeksiyon `stepName`, `AcceptsNote` | `ApprovalStepContext` 35/35 ✅; runtime §8 |
| WCN-DELEGATE-LOGTIME-01: başlatana devretme reddi; pencere uygun kişileri sunar | `WorkflowTaskTransitionSupport.cs:424`; `DelegationExcludedTargets` | `Delegate_to_the_starter_is_refused…` ✅; runtime: `excludedTargetPrincipalIds` = [B, A] |
| MOD-0280 time-entry onayı | TimeEntry | TimeEntry 326/326, Timesheet 130/130 ✅ |
| MVP-6 lojistik | SupplyChainService (5066) | 494/499, kırmızı 5 = main'de de (§6.2) |
| Q358 geçici Admin genişlemesi | `DefaultRolePermissionTemplate.cs:38-43` | Auth ✅ |

## 4. Derleme

Release, `--no-incremental`, her proje ayrı. **Merge sonrası 24 proje: 0 hata.**

| Proje | Önce `98da00832` uyarı | **Sonra (HEAD) uyarı** | Hata (sonra) |
|---|---|---|---|
| Diten.Platform.API | 35 | 36 | 0 |
| Diten.Platform.Application.Tests | 172 | 178 | 0 |
| Diten.Web | 17 | 18 | 0 |
| Diten.Web.Tests | 21 | 24 | 0 |
| Diten.CrmService.Application.Tests | 19 | 20 | 0 |
| Diten.AuthService.Api / .Application.Tests | 1 / 6 | 1 / 6 | 0 |
| Diten.ApiGateway / .Tests | 0 / 7 | 0 / 7 | 0 |
| Diten.InventoryService.Api / .Tests | 0 / 0 | 0 / 0 | 0 |
| Diten.CrmService.Api | 2 | 2 | 0 |
| Diten.MdmService.Api | 5 | 5 | 0 |
| DevEnablement / HumanCapital / Procurement / TalentEcosystem .Api | 1 / 1 / 1 / 1 | 1 / 1 / 1 / 1 | 0 |
| Hcm / ManagementGovernance / Ppm / Pvg .Api | 0 | 0 | 0 |
| Diten.SupplyChainService.Api / .Tests (yalnız main'de) | — | 0 / 0 | 0 |
| TenantArchitecture.ArchitectureTests | 0 | 0 | 0 |

Artışlar main'den: dosya bazında yeni uyarılar `RegulatoryTextsControllerBase.cs` (CS0108) ve `TimeEntryT2bRouteTests.cs` (xUnit2031) gibi
main dosyalarında. Bu WP'nin dokunduğu dosyalarda (Workflow çekirdeği, `WorkflowInstanceBsonMap.cs`, `PlatformBsonConventions.cs`,
`DisposableStandaloneMongo.cs`, test dosyaları, Web port yorumları) **uyarı yok** (derleme günlüklerinde aranarak ölçüldü).

## 5. `git diff --stat 98da00832..HEAD`

- Tümü: 9964 dosya, +1 282 039 / −17 878 (çoğu main'in kayıt belgeleri: `docs/records/audits/2026-09/` %57,5, `2026-10/` %13,1).
- Kod (`services frontend gateway tests`): 1824 dosya, +164 187 / −17 633.
- main'e göre bizim kod farkımız (`a814bad0f..HEAD`, aynı klasörler): 919 dosya, +119 916 / −222.

## 6. Testler

### 6.1 Önce / main / sonra

Ortam: Platform filtresi `ESign | Workflow | WorkAggregation | WorkCenter | ModuleRegistration | ModulePages | TimeEntry | Timesheet`.
"Sonra" ve "main" koşularında main'in yeni test ortamı şartları sağlandı (§6.3).

| Paket | Önce `98da00832` | main `a814bad0f` | **Sonra (HEAD)** | Not |
|---|---|---|---|---|
| Platform filtreli | 617/617 | 820/820 | **1025/1025** | E4 27, FX 5, aktör 29, Sync1 16, main APPROVAL-STATUS 23, ApprovalStepContext 35, TimeEntry 326, Timesheet 130, WorkflowCrossService 37 |
| InventoryService (kendi replica set'i) | 839/839 | — | **839/839** | karantina Workflow testleri dahil |
| Web | 699/699 | 1063/1063 | **1429/1429** | |
| Gateway | 107/108 | 86/87 | **110/111** | tek kırmızı aynı: FU-INT1-01 |
| AuthService.Application | 1072/1072 | 1101/1101 | **1154/1154** | |
| CrmService (Workflow tüketicisi, claim) | 2108/2114 (1 kırmızı, 5 atlanan) | 2457/2462 (5 atlanan) | **2457/2462** (5 atlanan) | öncedeki kırmızı: §6.2 |
| tests/architecture | 18/18 | 69/77 | **69/77** | aynı 8 kırmızı (§6.2) |
| SupplyChainService (MVP-6) | yok | 494/499 | **494/499** | aynı 5 kırmızı |

Workflow tüketicisi ölçümü (`api/v1/workflow | IWorkflowGateway | workflow/instances` araması): CrmService, InventoryService,
Platform içi (Tasks, TimeEntry). SupplyChainService Workflow kullanmıyor; yine de koşuldu.

### 6.2 Main'de de kırmızı (merge'den bağımsız)

| Test | Neden | Önce | main | Sonra |
|---|---|---|---|---|
| Gateway `EveryRoute_DownstreamPortIsInKnownServiceSet` | FU-INT1-01: 5063 / 5064 / 5065 bilinen port listesinde değil. 5067 listede (OD-P1). | kırmızı | kırmızı | kırmızı (aynı portlar) |
| Mimari `DocsPathGuardTests` ×5, `MongoTestDatabaseGuardTests.PerRunDatabaseExceptionListStaysHonest`, `AuditTrailStandardTests` ×2 | main'in kendi kırmızıları | — | 8 kırmızı | aynı 8 |
| SupplyChain `CompositionRootGuardTests` ×3, `CapacityRestartTests.Separate_processes…`, `ClaimReplayTests.Receipt_TwoIndependentTestProcesses…` | açık mod / ayrı süreç ister ("Explicit write/read restart mode required; exclude from ordinary suite", alt süreç derlemesi) | — | 5 | aynı 5 |
| CRM `ContactLocationPiiHardeningTests.PiiMasking_Redacts…` | önce koşusunda kırmızı, main ve sonra yeşil: rastgele GUID'deki rakam dizisi telefon gibi maskelenmiş (`account=***f-0333-…`) — veri bağımlı, kararsız | kırmızı | yeşil | yeşil |

⚠ **`AuditTrailStandardTests.EveryWriteCommand_IsAudited…` kapsamı büyüdü:** main'de 44 denetimsiz komut, merge sonrası **92**.
Fark tamamen bizim komutlarımız — main'in yeni kuralı (denetim defteri) MVP-1 dalında yoktu: InventoryService 27, AuthService 3
(`ConsumeESignTicketCommand`, `IssueESignWindowTicketCommand`, `ReauthenticateForESignCommand`), Platform 3
(`CancelSignatureRequestCommand`, `CreateExportCommand`, `CreateMeaningCommand`). Defter testi ayrıca: InventoryService defteri yok;
Platform e-imzada 5 aynı adlı komut (`VerifySignatureCommand`, `InvalidateSignatureCommand`, `CreateSignatureRequestCommand`,
`RejectSignatureRequestCommand`, `CancelSignatureRequestCommand` — `ESignatureRequests.cs` ve ikinci bir dosyada). Davranış çelişkisi
değil; "yeni özellik yok" sınırı nedeniyle burada düzeltilmedi → **FU-SYNC1-AUDIT** (§9).

### 6.3 Test ortamı (main'in yeni şartları)

main bu makinede (Windows) ek ortam istiyor; ikisi de "sonra" ve "main" koşularında aynı biçimde sağlandı:

1. **`DisposableStandaloneMongo`** (main) mongod'a koşulsuz `--nounixsocket` veriyordu; Windows mongod 9.0 bunu reddeder ("unrecognised
   option", çıkış 2). Ölçüm: düz `origin/main`'de filtreli Platform 286/820 kırmızı, hepsi "Standalone mongod exited with 2". Düzeltme
   `c7314b856`: bizim `DisposableMongoReplicaSet`'teki (MOD-0022 E2) korumanın aynısı — seçenek yalnız Windows dışında. Main ölçümü için
   aynı yama yalnız ölçüm worktree'sine uygulandı (commit yok). İlk denemede seçeneği süzen bir ara katman (`mongod-shim`) denendi;
   mongod dosyaları kapanmadan veri klasörü silinmeye çalışıldığı için 288 temizlik hatası verdi, bırakıldı (betik belgede).
2. **Hat mongod'u**: `DITEN_PLATFORM_TEST_MONGO_URI`, `AUTH_COMPOSITION_GUARD_MONGO_URI`, `MVP6_MOD0192_MONGO_URI` ve SupplyChain
   değişkenleri `scripts/test-env/mvp6-test-mongo-env.sh` şeklinde: kendi tek üyeli replica set'im, slot 1 → **31994**, `rssync1`,
   `enableTestCommands=1`, yalnız loopback. Bunlar olmadan main'de Platform 9, Auth 2, SupplyChain 204 ortam kırmızısı ölçüldü.
3. `DITEN_TEST_MONGOD` / `DITEN_ITEST_MONGOD_BIN_DIR` = `C:\Program Files\MongoDB\Server\9.0\bin`; Inventory'de `INVENTORY_TEST_MONGO` boş
   (paket kendi replica set'ini açar; asla 27017).

`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmadı. Platform tam paketi koşulmadı.

## 7. Sabotaj (K3) — birleşik kod

Her sabotaj tek düzenleme; ilgili filtre koşuldu; dosya bayt bayt geri yazıldı ve `git status --porcelain` boş (merge commit'li ağaçta).

| # | Sabotaj | Sonuç | Kırmızı testler (örnek) | Geri alınınca |
|---|---|---|---|---|
| (a) | Onay yolunda imza kapısı çağrısı kaldırıldı (`if (false && … task.SignatureRequired)`) | **14 kırmızı** / 27 | `Api_approve_of_a_signature_required_step_without_a_signature_is_409_required`, `WorkCenter_approve_of_a_signature_required_step_without_a_signature_is_409_required`, `Another_users_signature_is_refused_on_both_paths`, E5B `…re_signed…` ×3 | temiz, yeşil |
| (b) | Aktör bağlama kaldırıldı — gövdedeki `ActorId`'ye güven | **22 kırmızı** / 34 | FX `A_body_actor_that_is_not_the_caller_is_refused…` ×5, **main** `Naming_someone_else_as_the_actor_is_403_and_changes_nothing` ×5, `Sync1_…` | temiz |
| (c) | Başlatana devretme reddi kaldırıldı | **1 kırmızı** / 1 | **main** `Delegate_to_the_starter_is_refused_by_user_id_whatever_the_spelling_of_the_id` | temiz |
| (d) | SoD (yeni kayıt) kullanıcı kimliği yerine e-posta (`StartedBy`) karşılaştırır | **3 kırmızı** / 3 | FX `Fx_a_tenant_user_who_started_the_workflow_cannot_approve_it`, **main** `Whoever_started_an_instance_cannot_approve_it_even_as_its_only_candidate`, `Sync1_an_instance_whose_starter_id_was_written_as_text…` | temiz |
| (e) ek | Toleranslı `StartedByUserId` eşlemesi kaldırıldı | 1. koşu: **kırmızı değil** (0/15) → ölçüm: sürücünün `GuidSerializer`'ı geçerli GUID metnini zaten okuyor. Eşlemenin kendi değeri geçersiz metin; test eklendi (`ffa92ec8d`) → **1 kırmızı** / 16 | `Sync1_a_starter_id_text_that_is_not_a_guid_reads_as_no_id_and_the_instance_keeps_the_old_comparison` | temiz, 36/36 |

Eski kayıt sabotajı yok (sahip kararı B).

## 8. Runtime probe — kendi yığını

Yığın (`stack/Program.cs`, Q5 → FUB2 → SYNC1): kendi mongod (replica set, rastgele port 63596; asla 27017), AuthService 15056, Platform
15057, Gateway 15191 (`ocelot.json`'ın GEÇİCİ kopyası: **5067** → kendi Inventory 15176; 5057 / 5056 / 5059 → kendi servisler / LE taklidi;
main'in 5066'sı dahil diğer her şey → ölü port 9), InventoryService 15176, kendi PRODUCT-MASTER taklidi `pm-stub.js` 14036, LE taklidi 14037,
Web 15001. Sırlar bellekte; test şifresi yalnız `<çıktı>/pw.txt`'de, kapanışta silindi. main'in AuthService'i örnek kullanıcıları artık
isteğe bağlı tohumluyor (`DevSeeds:MockUsers`); yığında açıldı.

Kullanıcılar: A = john.doe (karar veren), B = jane.smith (onaylayan); QA rolü 31/31 anahtar.

| # | Adım | Beklenen | Sonuç |
|---|---|---|---|
| H0 | Yığının iki karantina kaydı | 2 kayıt | ✅ KRN-2026-0001 / 0002 |
| D1 | Aday = B; A karar taslağı → imza → gönder | 200, 201, 201, 202 | ✅ QKR-2026-0001, ESIG-2026-000001, 202 PendingApproval |
| I1b | B'nin WorkCenter öğesi | onay açık, imza gerekli, anlam, `workflow.task.approve`; not alır | ✅ `signatureRequired true`, `APPROVE`, `workflow.task.approve`, `acceptsNote true`, `stepName` "QA onayı"; devret `requiresTargetPerson`, `excludedTargetPrincipalIds` = [B, A] |
| NOSIG1b | B, WorkCenter dispatch'ten **imzasız** onay | 409 `ESIGN_SIGNATURE_REQUIRED` | ✅ 409 |
| UI | B, **WorkCenter ekranından**: Onayla → main'in onay penceresi (adım, isteğe bağlı not) → bizim e-imza penceresi (adım QA / QA_APPROVAL, "Onaylıyorum", görünüm "Yalnız ad soyad") → İmzala → Tamam | 200 | ✅ `POST /WorkCenterNext/api/work-items/…/actions/approve → 200 OK`; imza ESIG-2026-000002; "Onayla işlemi tamamlandı"; ekran listeye döndü |
| S1 | Karar durumu | Applied | ✅ Applied, kayıt Decided, `approver` = B, `approverSignatureId` dolu |
| D2 (ilk) | Aday = yalnız A (karar veren) | — | MOD-0175 409 `QUARANTINE_APPROVERS_NOT_CONFIGURED` — bizim "karar veren ≠ onaylayan" kuralımız Workflow'dan önce durdurur (beklenen davranış) |
| D2 | Aday = [A, B] | 202 | ✅ QKR-2026-0003; görev A'ya atanmadı (karar veren adaylardan çıkarılır) |
| W0 | A, yayımlanmış şablonla Workflow örneği başlatır, tek aday A | 200 | ✅ |
| W1 | A'nın WorkCenter öğesi | onay kapalı, `SELF_APPROVAL_NOT_ALLOWED` | ✅ (imza bayrağı da duruyor) |
| W2 | A, **imzalı** WorkCenter onayı (başlatanın kendi onayı) | 409 `SOD_VIOLATION` | ✅ |
| W3 | A, doğrudan task API, imzalı | 409 `SOD_VIOLATION` | ✅ |
| W4 | A, gövdede B'yi aktör yazar | 403 `WORKFLOW_ACTOR_MISMATCH` | ✅ |
| W5 | Örnek | Active (dokunulmadı) | ✅ |

Probe dosyasındaki FAIL satırları (açıkça): ilk `I1b` ve iki `I2a` probe betiğinin öğe eşleştirme hatası (öğe `source.objectId` = karar kimliği
ile eşlenmeliydi; düzeltildi, `I1b` yeniden PASS); `D3` var olmayan üçüncü kaydı denedi (betik hatası); ilk `D2` yukarıda açıklandı.

Ekranlar (`mvp1-sync1-shots/`): `01` WorkCenter listesi (B, "QA onayı" etiketi) · `02` onay öğesi detayı (adım, DisplayContext özeti ve
etiketleri, aksiyonlar) · `03` main'in onay penceresi "Not (isteğe bağlı)" · `04` bizim e-imza penceresi · `05` "İmza kaydedildi" · `06` 200 ve
listeye dönüş. Aday pozisyonlar bu probede görünmez: şablon adayları kişi (`user:`), pozisyon değil — `ApprovalStepContext` testleri kapsıyor.

## 9. Bulgular ve takip maddeleri

| Kod | Konu | Sahip |
|---|---|---|
| **FU-SYNC1-AUDIT** | main'in `AuditTrailStandard` kuralı MVP-1 komutlarını kapsamıyor: Inventory 27, Auth 3, Platform 3 denetimsiz/listesiz; Inventory defteri yok; Platform e-imzada 5 çift ad (§6.2). | MVP-1 CT + main mimari sahibi |
| FU-SYNC1-PORTS | `AGENTS.md` §3 ve `CLAUDE.md` port tabloları 5067; MDM R-12 doğrudan çağrı 5067. | CT |
| FU-SYNC1-DOMCFG | `domain-config.md:63` SupplyChainService'i hâlâ **5061** gösteriyor; main'de servis 5066'da (main'in kaydı, değiştirilmedi). | MVP-6 / EA |
| FU-INT1-01 (açık) | Gateway bilinen port listesi 5063/5064/5065. main'de de kırmızı. | gateway sahibi |
| FU-SYNC1-TESTENV | main'in yeni Mongo testleri bu makinede ek ortam ister (§6.3); `c7314b856` main'e de gerekli (Windows). | main test altyapısı |
| Bilgi | Sahip kararı B: eski açık görevlerde başlatan kendi onayını verebilir (bilinen açık). MOD-0175 akışında karar veren hiç aday olmadığı için karantina kararlarında bu açık yok. | — |

## 10. Süreçler (PID) — yalnız kendi listem

| Süreç | PID | Kapanış |
|---|---|---|
| Yığın deneme 1 (tohum kullanıcısı yok → durduruldu) | mongod 56184 (port) · auth 28240 · platform 23476 · gateway 38252 · pm-stub 36624 · le-stub 41012 · inventory 11412 · web 18324 | stop dosyası; "7 processes stopped, stack mongod disposed, pw.txt deleted" |
| Yığın (probe) | mongod 63596 (port) · auth 10900 · platform 23036 · gateway 3292 · pm-stub 8156 · le-stub 11152 · inventory 23264 · web 21076 | aynı; portlar kapalı doğrulandı |
| Hat mongod'u | lane 11832 · mongod 36140 (31994) | stop dosyası; 31994 kapalı doğrulandı |
| Test / derleme | `dotnet test` / `dotnet build` süreçleri (kendi worktree'lerim: `…-sync1`, `…-sync1-before`, `…-sync1-main`) | kendiliğinden bitti |

Sahibin 27017 mongod'u (PID 4904) açıktı; hiçbir istek gönderilmedi, dokunulmadı. Sahibin filosu kapalıydı (5000–5061 dinlemiyordu).
Ana checkout'ta dosya düzenlenmedi; push yok; main'e ve MVP-1 dalına yazılmadı.
