# TEST-HIZ-01 · Test süresi ölçüm raporu (2026-10-23)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru → `records/analysis/test-speed/`). Bu dosya
> düzeltilmez; ölçüm yenilenirse yeni tarihli bir rapor yazılır.
> **Kapsam:** salt okunur. Hiçbir test silinmedi, değiştirilmedi ya da yerelde tam takım koşulmadı.

## 1. Kısa sonuç

- **Sorun test sayısı değil, testlerin küçük ve yavaş bir kısmı.** 19.714 test sonucunun 16.682'si (%85) 0,1 saniyenin
  altında bitiyor ve toplam sürenin yalnız %5'ini tutuyor. Sürenin %62'si 1 saniyeden uzun süren 598 testte.
- **Süreyi Mongo kullanan testler belirliyor.** Gerçek Mongo'ya giden 6.483 test (sonuçların %33'ü) toplam test
  süresinin %86'sını (45 dk 36 sn'nin 39 dk 08 sn'si) tutuyor.
- **CI'ın duvar saati süresi 15 dk 43 sn** ve bunu tek bir iş belirliyor: auth-app (2.929 test, test adımı 15 dk 08 sn;
  bunun 10 dk 40 sn'si 1.118 Mongo testi). İkinci sırada platform-app (8.714 test, 9 dk 58 sn; 2.114 Mongo testi
  10 dk 45 sn).
- **1. adımın sonucu:** PR #205'ten sonra main'de platform-app, auth-acceptance-host ve inventory-tests yeşil.
  Çalışma yine kırmızı, çünkü auth-app'te bilinen aralıklı bir test düştü (§7).
- **Gözden kaçan büyük bir boşluk var:** 11 test projesi service-tests'te yok. Bunların üçü (mimari, tenancy, Web)
  phase1'de koşmalı, ama phase1 2026-10-21'den beri menü anahtarı kontrolünde durduğu için **hiç koşmuyor** (§8).
  CRM'in 2.382 testi ise hiçbir CI işinde yok.

## 2. Kaynak ve yöntem

| Ne | Değer |
|---|---|
| Commit | main `9f53d4e1e` (PR #205 merge) |
| service-tests çalışması | 39921122915 (28 iş, 28 TRX dosyası, artifact `test-results-*`) |
| phase1-gates çalışması | 39921122875 |
| Karşılaştırma (1. adım öncesi) | service-tests 39890889696 (main `0123bda72`) |
| Test başına süre | TRX `UnitTestResult/@duration` |
| İş ve adım süresi | GitHub Actions jobs API (`started_at` / `completed_at`); "Test" adımı derlemeyi de içerir |
| Mongo / HTTP host / dosya okuma işareti | Test dosyasının kaynak metni taranarak (aşağıda); test, sınıfının dosyasının işaretini alır |

**İşaretler nasıl kondu (sezgisel, dosya düzeyinde):**

- **Mongo:** dosyada `MongoDB.Driver`, `IMongoDatabase`, `MongoClient`, `mongod`, `DITEN_TEST_MONGOD`, sahip olunan
  ya da tek kullanımlık Mongo yardımcılarının adları (`PlatformTestMongo`, `OwnedMongo`, `DisposableStandaloneMongo`,
  `DisposableMongoReplicaSet`, `EphemeralMongo`, `TestMongo` …) geçiyor.
- **HTTP host:** `WebApplicationFactory` ya da aynı projede ondan türeyen bir sınıf (kalıtımla, 31 tür bulundu),
  `TestServer`, `UseTestServer`, `WebApplication.CreateBuilder`, `Host.CreateDefaultBuilder`.
- **Dosya okuma:** `File.ReadAllText/ReadAllLines/ReadAllBytes/ReadLines/OpenText/OpenRead` ya da `new StreamReader(`
  **ve** aynı dosyada bir kaynak uzantısı (`.cshtml`, `.resx`, `.cs`, `.js`, `.json`, `.yml`, `.md` …) ya da repo
  kökü arama (`RepoRoot`, `FindRepo`, `"services"`, `".github"` …).

**Sınırlar (önemli):**

- İşaret dosya düzeyinde: aynı dosyada Mongo'ya gitmeyen saf testler de "Mongo" sayılabilir. 4. adımdaki her karar
  test bazında yeniden okunarak verilecek.
- TRX süresi yalnız testin kendisini ölçer. Sınıf ya da koleksiyon fixture'ının kurulumu (ör. paylaşılan bir mongod'un
  açılması) hiçbir teste yazılmaz; bu yüzden bazı işlerde test adımı, testlerin toplam süresinden uzun.
- xUnit koleksiyonları paralel koşturur; testlerin toplam süresi, iş süresinden uzun olabilir (platform-app,
  inventory-tests).
- Tek bir CI çalışması ölçüldü. Runner hızı çalışmadan çalışmaya oynuyor: aynı işler 39890889696'da platform-app
  9 dk 32 sn, auth-app 13 dk 17 sn sürdü.
- [Fact]/[Theory] sayımı (18.175) özel `*Fact` / `*Theory` niteliklerini de içerir (ör. `PerformanceFact`); bu yüzden
  2026-10-23 sabahki 17.901'den biraz yüksek.

## 3. Suite (CI işi) başına süre

Sıralama iş süresine göre. "Mongo testi", "HTTP host testi", "dosya okuyan test" sütunları: o işaretli test sonucu
sayısı ve toplam süresi. Bir test birden çok işaret taşıyabilir.

| Suite | Test sayısı | İş süresi | Test adımı | Testlerin toplam süresi | Mongo testi (süre) | HTTP host testi (süre) | Dosya okuyan test (süre) |
|---|---:|---:|---:|---:|---:|---:|---:|
| auth-app | 2929 | 15 dk 43 sn | 15 dk 08 sn | 11 dk 46 sn | 1118 (10 dk 40 sn) | 334 (3 dk 08 sn) | 546 (25 sn) |
| platform-app | 8714 | 10 dk 24 sn | 9 dk 58 sn | 12 dk 12 sn | 2114 (10 dk 45 sn) | 485 (5 dk 01 sn) | 1206 (42 sn) |
| mdm-api | 1221 | 7 dk 52 sn | 7 dk 23 sn | 6 dk 31 sn | 1172 (6 dk 29 sn) | 138 (6 sn) | 19 (2 sn) |
| auth-acceptance-host | 71 | 7 dk 11 sn | 6 dk 21 sn | 5 dk 01 sn | 36 (4 dk 04 sn) | 7 (1 dk 10 sn) | 7 (1 dk 16 sn) |
| supplychain-tests | 606 | 3 dk 46 sn | 3 dk 09 sn | 2 dk 42 sn | 291 (2 dk 39 sn) | 102 (14 sn) | 38 (4 sn) |
| mdm-app | 2921 | 2 dk 58 sn | 1 dk 36 sn | 1 dk 37 sn | 1077 (1 dk 12 sn) | 144 (6 sn) | 470 (13 sn) |
| platform-bgjobs | 93 | 2 dk 30 sn | 55 sn | 56 sn | 56 (55 sn) | 53 (36 sn) | 29 (17 sn) |
| inventory-tests | 1341 | 2 dk 22 sn | 1 dk 37 sn | 3 dk 38 sn | 266 (1 dk 38 sn) | 886 (3 dk 27 sn) | 202 (7 sn) |
| auth-scr | 160 | 2 dk 15 sn | 1 dk 44 sn | 13 sn | 73 (6 sn) | 36 (2 sn) | 73 (7 sn) |
| platform-eventing | 68 | 2 dk 07 sn | 1 dk 27 sn | 7 sn | 3 (7 sn) | 0 (0 sn) | 0 (0 sn) |
| platform-composition-guard | 12 | 1 dk 59 sn | 1 dk 24 sn | 0 sn | 12 (0 sn) | 2 (0 sn) | 10 (0 sn) |
| planning-tests | 384 | 1 dk 48 sn | 1 dk 02 sn | 26 sn | 213 (25 sn) | 92 (17 sn) | 89 (17 sn) |
| devenablement-api | 92 | 1 dk 13 sn | 40 sn | 3 sn | 0 (0 sn) | 0 (0 sn) | 15 (0 sn) |
| procurement-api | 147 | 1 dk 00 sn | 27 sn | 1 sn | 0 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| pvg-api | 36 | 1 dk 00 sn | 19 sn | 0 sn | 0 (0 sn) | 36 (0 sn) | 0 (0 sn) |
| safety-stock-read | 46 | 59 sn | 31 sn | 10 sn | 12 (8 sn) | 13 (2 sn) | 0 (0 sn) |
| manufacturing-tests | 82 | 55 sn | 24 sn | 0 sn | 35 (0 sn) | 35 (0 sn) | 8 (0 sn) |
| bb-central-audit | 31 | 51 sn | 21 sn | 1 sn | 0 (0 sn) | 0 (0 sn) | 10 (0 sn) |
| mg-tests | 87 | 50 sn | 17 sn | 1 sn | 0 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| safety-stock-calculation | 34 | 50 sn | 18 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| pvg-meddra-coding | 40 | 49 sn | 20 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 20 (0 sn) |
| bb-email | 109 | 48 sn | 15 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 59 (0 sn) |
| pvg-regpv-base | 88 | 48 sn | 18 sn | 0 sn | 5 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| bb-secrets | 5 | 47 sn | 17 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| gateway | 313 | 47 sn | 20 sn | 9 sn | 0 (0 sn) | 14 (5 sn) | 259 (5 sn) |
| pvg-case-processing | 23 | 38 sn | 12 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| pvg-signal-management | 43 | 33 sn | 7 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 21 (0 sn) |
| safety-stock-policy-state | 18 | 33 sn | 8 sn | 0 sn | 0 (0 sn) | 0 (0 sn) | 0 (0 sn) |
| **Toplam** | **19714** | | | **45 dk 36 sn** | **6483 (39 dk 08 sn)** | **2377 (14 dk 15 sn)** | **3081 (3 dk 36 sn)** |

**Okuma:**

- İlk dört iş (auth-app, platform-app, mdm-api, auth-acceptance-host) toplam test süresinin %78'ini tutuyor.
- Küçük işlerde (≤ 1 dk) sürenin neredeyse tamamı kurulum ve derleme: test adımı 7–40 sn, testlerin toplamı ≤ 10 sn.
  Bu işlerde test silmek süreyi değiştirmez.

### 3.1 Kaynak taraması · 39 test projesi (CI'da koşmayanlar dahil)

Sayılar test niteliği sayısıdır (TRX'teki sonuç sayısı değil; bir [Theory] birden çok sonuç üretir). "Mongo dosyalarındaki test": Mongo işaretli dosyalardaki test niteliği sayısı.

| Proje | [Fact]/[Theory] | [InlineData] | Mongo dosyalarındaki test | HTTP host dosyalarındaki test | Dosya okuyan dosyalardaki test |
|---|---:|---:|---:|---:|---:|
| Diten.Platform.Application.Tests | 6511 | 2007 | 1576 (187 dosya) | 307 (29 dosya) | 581 (67 dosya) |
| Diten.CrmService.Application.Tests | 2382 | 391 | 89 (4 dosya) | 0 (0 dosya) | 32 (5 dosya) |
| Diten.Web.Tests | 1858 | 1068 | 0 (0 dosya) | 487 (61 dosya) | 1098 (124 dosya) |
| Diten.AuthService.Application.Tests | 1652 | 1254 | 679 (66 dosya) | 218 (10 dosya) | 177 (29 dosya) |
| Diten.MdmService.Application.Tests | 1585 | 1394 | 636 (61 dosya) | 69 (10 dosya) | 185 (16 dosya) |
| Diten.InventoryService.Tests | 975 | 417 | 221 (45 dosya) | 696 (113 dosya) | 155 (26 dosya) |
| Diten.MdmService.Api.Tests | 856 | 432 | 831 (56 dosya) | 45 (2 dosya) | 14 (5 dosya) |
| Diten.SupplyChainService.Tests | 362 | 152 | 195 (40 dosya) | 79 (15 dosya) | 26 (7 dosya) |
| Diten.PlanningService.Cycles.Tests | 356 | 44 | 209 (26 dosya) | 6 (2 dosya) | 2 (1 dosya) |
| Diten.PpmService.Tests | 238 | 158 | 0 (0 dosya) | 0 (0 dosya) | 24 (5 dosya) |
| Diten.ProcurementService.Api.Tests | 147 | 0 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.ApiGateway.Tests | 122 | 182 | 0 (0 dosya) | 8 (3 dosya) | 74 (16 dosya) |
| Diten.AuthService.ServiceClientRegistry.Tests | 93 | 91 | 48 (7 dosya) | 18 (2 dosya) | 32 (3 dosya) |
| TenantArchitecture.ArchitectureTests | 79 | 73 | 44 (5 dosya) | 0 (0 dosya) | 79 (12 dosya) |
| Diten.ManagementGovernanceService.IntegrationTests | 74 | 26 | 39 (13 dosya) | 5 (1 dosya) | 0 (0 dosya) |
| Diten.AuthService.AccountKindAcceptanceHost.Tests | 70 | 2 | 35 (11 dosya) | 7 (2 dosya) | 7 (2 dosya) |
| Diten.PpmService.IntegrationTests | 70 | 29 | 42 (10 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.PvgService.RegPvBase.Tests | 67 | 27 | 5 (1 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.ManufacturingService.Tests | 64 | 21 | 35 (5 dosya) | 35 (5 dosya) | 8 (1 dosya) |
| Diten.Platform.BackgroundJobs.Tests | 60 | 31 | 33 (4 dosya) | 32 (3 dosya) | 19 (2 dosya) |
| Diten.ManagementGovernanceService.Tests | 58 | 8 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.ManagementGovernanceService.ArchitectureTests | 57 | 0 | 6 (1 dosya) | 0 (0 dosya) | 43 (13 dosya) |
| Diten.DevEnablementService.Api.Tests | 56 | 46 | 0 (1 dosya) | 0 (1 dosya) | 9 (1 dosya) |
| Diten.HcmService.Application.Tests | 52 | 12 | 1 (1 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.Platform.Eventing.Tests | 41 | 3 | 3 (2 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.BuildingBlocks.Email.Tests | 40 | 62 | 0 (0 dosya) | 0 (0 dosya) | 14 (1 dosya) |
| Diten.Application.Tests | 31 | 9 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.SafetyStockService.Read.Tests | 30 | 22 | 6 (2 dosya) | 7 (3 dosya) | 0 (0 dosya) |
| Diten.PvgService.SignalManagement.Tests | 29 | 3 | 0 (0 dosya) | 0 (0 dosya) | 14 (1 dosya) |
| Diten.SafetyStockService.Calculation.Tests | 28 | 9 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.BuildingBlocks.CentralAudit.Tests | 27 | 6 | 0 (1 dosya) | 0 (0 dosya) | 7 (1 dosya) |
| Diten.PvgService.MeddraCoding.Tests | 27 | 3 | 0 (0 dosya) | 0 (0 dosya) | 13 (1 dosya) |
| Diten.PvgService.Api.Tests | 21 | 18 | 0 (0 dosya) | 21 (1 dosya) | 0 (0 dosya) |
| Diten.PvgService.CaseProcessing.Tests | 20 | 4 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.SafetyStockService.PolicyState.Tests | 18 | 0 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.Platform.CompositionGuard.Tests | 7 | 6 | 7 (2 dosya) | 2 (1 dosya) | 5 (1 dosya) |
| Diten.BuildingBlocks.Security.Secrets.Tests | 5 | 0 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| Diten.EnterpriseStrategy.EndToEnd.Tests | 4 | 0 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |
| TenantArchitecture.TenancyTests | 3 | 0 | 0 (0 dosya) | 0 (0 dosya) | 0 (0 dosya) |

## 4. Test süresi dağılımı

| Test süresi | Test sayısı | Toplam süre | Toplam sürenin payı |
|---|---:|---:|---:|
| < 0,1 sn | 16682 | 2 dk 26 sn | %5 |
| 0,1–1 sn | 2434 | 14 dk 58 sn | %33 |
| 1–5 sn | 539 | 15 dk 48 sn | %35 |
| 5–30 sn | 55 | 8 dk 42 sn | %19 |
| ≥ 30 sn | 4 | 3 dk 42 sn | %8 |

## 5. En yavaş 50 test

Süre TRX'ten. Test adları 110 karakterde kısaltıldı.

| # | Süre | Suite | Test | İşaret |
|---:|---:|---|---|---|
| 1 | 89.9 sn | supplychain-tests | `Diten.SupplyChainService.Tests.CapacityPlans.CapacityFaultTests.X07_Unknown_terminal_commit_ack_loss_keeps_on…` | Mongo |
| 2 | 57.1 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPI_Nothing_arrived_inside_the_window_holds_the_draft_and_a…` | Mongo |
| 3 | 39.7 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.PoisonedEnvironmentTests.ApiChildEnvironment_IsExactlyTheAl…` | Mongo, HTTP host, dosya |
| 4 | 35.2 sn | supplychain-tests | `Diten.SupplyChainService.Tests.CapacityPlans.CapacityRestartTests.Separate_processes_claim_once_and_restart_r…` | Mongo |
| 5 | 22.1 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.SevenStateAcceptanceTests.Cancel_NoConfirmationWithin5s_Fal…` | Mongo |
| 6 | 18.3 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.SevenStateAcceptanceTests.SigkillAfterReady_BackupGroupClea…` | Mongo |
| 7 | 15.9 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.SevenStateAcceptanceTests.NormalShutdown_HostApiMongodAllEx…` | Mongo |
| 8 | 15.5 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.SeedReadyShapeTests.SeedReady_HasExactActorAndSubjectShape_…` | — |
| 9 | 15.3 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.PoisonedEnvironmentTests.RealHost_PoisonedEnvironment_Still…` | Mongo, HTTP host, dosya |
| 10 | 15.0 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.ReadyMessageLabelTests.Ready_carries_the_stub_loginSettings…` | Mongo |
| 11 | 15.0 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.WireLevelErrorMappingTests.SupervisorClosesChannelDuringSee…` | Mongo |
| 12 | 14.6 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.PoisonedEnvironmentTests.RealApiChild_NeverOpensAnyFileDesc…` | Mongo, HTTP host, dosya |
| 13 | 14.4 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.StrictMessageSchemaTests.RealHost_ShutdownWithUnknownField_…` | — |
| 14 | 14.3 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.NetworkIsolationTests.ApiChildConnections_OnlyMongodAndLogi…` | Mongo |
| 15 | 12.3 sn | auth-app | `Diten.AuthService.Application.Tests.Users.AdminBootstrapGuardTests.No_document_carries_the_dev_password` | dosya |
| 16 | 11.8 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.DescriptorAndEofTimingTests.ApiAndMongod_NeverHoldControlSo…` | Mongo |
| 17 | 10.8 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.ExitCodeAlignmentTests.FramingViolationDuringShutdownWait_R…` | — |
| 18 | 10.7 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.F11_A_correction_repeat_after_its_decision_was_written_comp…` | Mongo |
| 19 | 10.6 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.ExitCodeAlignmentTests.MalformedJsonDuringShutdownWait_Repo…` | — |
| 20 | 10.6 sn | platform-bgjobs | `Diten.Platform.BackgroundJobs.Tests.WorkflowTemplatesStartupCompositionTests.The_normal_start_composition_wri…` | Mongo, HTTP host, dosya |
| 21 | 10.3 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.SevenStateAcceptanceTests.BadDotnetPath_ApiStartFailedWithE…` | Mongo |
| 22 | 10.3 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.SevenStateAcceptanceTests.HelloAckWithheld_HostSelfTimesOut…` | Mongo |
| 23 | 9.9 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.ExceptionTextLeakTests.HappyPathRealRun_HostStdoutIsExactly…` | Mongo |
| 24 | 9.7 sn | auth-app | `Diten.AuthService.Application.Tests.Users.AdminResetInvalidatesEndpointTests.Successful_sign_ins_do_not_use_u…` | Mongo, HTTP host |
| 25 | 9.7 sn | platform-bgjobs | `Diten.Platform.BackgroundJobs.Tests.PlatformAdministratorSeedCompositionTests.The_start_up_writes_the_develop…` | Mongo |
| 26 | 9.6 sn | auth-app | `Diten.AuthService.Application.Tests.Users.AdminResetInvalidatesEndpointTests.A_deactivated_accounts_answers_d…` | Mongo, HTTP host |
| 27 | 9.3 sn | auth-app | `Diten.AuthService.Application.Tests.Users.AdminResetInvalidatesEndpointTests.A_client_out_of_wrong_attempts_i…` | Mongo, HTTP host |
| 28 | 8.9 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.F5_A_stuck_retirement_request_whose_start_never_arrived_is_…` | Mongo |
| 29 | 8.5 sn | platform-bgjobs | `Diten.Platform.BackgroundJobs.Tests.PlatformContainerValidationTests.The_client_that_carries_the_internal_key…` | Mongo, HTTP host |
| 30 | 8.3 sn | auth-acceptance-host | `Diten.AuthService.AccountKindAcceptanceHost.Tests.ExceptionTextLeakTests.BadDotnetPathCanary_NeverLeaksIntoHo…` | Mongo |
| 31 | 8.0 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.B13_A_retirement_request_past_the_limit_on_a_transient_caus…` | Mongo |
| 32 | 7.8 sn | supplychain-tests | `Diten.SupplyChainService.Tests.Loads.LoadLifecycleTests.EverySourceTargetPairMatchesFrozenLifecycleAndRejecte…` | Mongo |
| 33 | 7.8 sn | auth-app | `Diten.AuthService.Application.Tests.Testing.AccountKindAcceptanceGuardTests.T1_A_then_B_start_dispose_A_then_…` | Mongo |
| 34 | 7.8 sn | auth-app | `Diten.AuthService.Application.Tests.Testing.AccountKindAcceptanceGuardTests.T5_dispose_still_stops_the_runner…` | Mongo |
| 35 | 7.8 sn | auth-app | `Diten.AuthService.Application.Tests.Testing.AccountKindAcceptanceGuardTests.T2_A_then_B_start_dispose_B_then_…` | Mongo |
| 36 | 7.8 sn | auth-app | `Diten.AuthService.Application.Tests.Testing.AccountKindAcceptanceGuardTests.Two_sequential_start_dispose_cycl…` | Mongo |
| 37 | 7.4 sn | platform-bgjobs | `Diten.Platform.BackgroundJobs.Tests.PlatformContainerValidationTests.Platform_container_builds_under_the_vali…` | Mongo, HTTP host |
| 38 | 7.3 sn | mdm-api | `Diten.MdmService.Api.Tests.FgFix1HttpTests.Fix2_Item01_A_commit_with_an_unknown_result_is_ambiguous_and_burns…` | Mongo |
| 39 | 7.3 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPI_A_stuck_submit_holds_the_draft_against_an_edit_and_a_ne…` | Mongo |
| 40 | 7.3 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPIF_The_reconciling_repeat_inside_the_settle_window_says_5…` | Mongo |
| 41 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPIF_A_submit_stuck_at_an_earlier_version_still_counts_on_b…` | Mongo |
| 42 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.ItemS1HttpTests.Fix1_An_unknown_write_outcome_is_202_and_burns_nothing(fault: "com…` | Mongo |
| 43 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPIF_A_reconciling_repeat_answered_incomplete_says_503` | Mongo |
| 44 | 7.2 sn | auth-app | `Diten.AuthService.Application.Tests.Auth.PlatformAccountAtTenantDoorTests.A_lookup_limit_of_twenty_returns_tw…` | Mongo |
| 45 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPI_The_runner_counts_a_stuck_submit_on_both_gauges` | Mongo |
| 46 | 7.2 sn | auth-app | `Diten.AuthService.Application.Tests.Auth.PlatformAccountAtTenantDoorTests.A_lookup_limit_of_twenty_returns_tw…` | Mongo |
| 47 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPI_The_submitters_repeat_settles_a_stuck_submit_nothing_ar…` | Mongo |
| 48 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPI_A_submit_stuck_at_an_earlier_version_does_not_hold_the_…` | Mongo |
| 49 | 7.2 sn | mdm-api | `Diten.MdmService.Api.Tests.GskuLifecycleHttpTests.GPC1_Over_http_a_request_stuck_on_a_record_answer_is_a_pers…` | Mongo |
| 50 | 6.8 sn | platform-app | `Diten.Platform.Application.Tests.Persistence.SubscriptionParticipantFaultMatrixMongoTests.ExactFortyFourHandl…` | Mongo |

**Okuma:**

- 50 testin toplamı 11 dk 31 sn (toplam test süresinin %25'i).
- 50'nin 18'i auth-acceptance-host'ta: gerçek süreç, gerçek mongod ve gerçek API başlatan kabul testleri (macOS işi).
- mdm-api'de `GskuLifecycleHttpTests` 12 kez listede. Bu testler 7–57 sn arası; sürenin büyük kısmı beklemelerden
  ("settle window", "stuck submit") geliyor.
- En uzun test (89,9 sn) supplychain-tests'te bir hata enjeksiyonu testi (`CapacityFaultTests.X07…`).

## 6. En yavaş 25 test sınıfı (sınıftaki testlerin toplam süresi)

| # | Toplam süre | Test | Suite | Sınıf | İşaret |
|---:|---:|---:|---|---|---|
| 1 | 4 dk 23 sn | 356 | mdm-api | `GskuLifecycleHttpTests` | Mongo |
| 2 | 3 dk 50 sn | 150 | auth-app | `ExportGrantBackfillScenarioTests` | Mongo |
| 3 | 3 dk 04 sn | 184 | auth-app | `AdminResetInvalidatesEndpointTests` | Mongo, HTTP host |
| 4 | 1 dk 32 sn | 25 | supplychain-tests | `CapacityFaultTests` | Mongo |
| 5 | 1 dk 22 sn | 7 | auth-acceptance-host | `SevenStateAcceptanceTests` | Mongo |
| 6 | 1 dk 16 sn | 103 | auth-app | `PlatformAccountAtTenantDoorTests` | Mongo |
| 7 | 1 dk 10 sn | 4 | auth-acceptance-host | `PoisonedEnvironmentTests` | Mongo, HTTP host, dosya |
| 8 | 1 dk 08 sn | 47 | platform-app | `PartnerAdminScopeHttpMongoTests` | Mongo, HTTP host |
| 9 | 59 sn | 46 | platform-app | `TenantSubscriptionEndpointsHttpMongoTests` | Mongo, HTTP host |
| 10 | 52 sn | 20 | auth-app | `AccountKindAcceptanceGuardTests` | Mongo |
| 11 | 51 sn | 45 | platform-app | `TenantModulesScreenHttpMongoTests` | Mongo, HTTP host |
| 12 | 35 sn | 2 | supplychain-tests | `CapacityRestartTests` | Mongo |
| 13 | 33 sn | 28 | platform-app | `DecisionMakerSearchHttpMongoTests` | Mongo, HTTP host |
| 14 | 26 sn | 35 | mdm-api | `LskuFix2HttpTests` | Mongo |
| 15 | 24 sn | 16 | platform-app | `MeetingAttendeeSearchHttpMongoTests` | Mongo, HTTP host |
| 16 | 24 sn | 34 | auth-app | `EntitlementAttestationConsumerTests` | — |
| 17 | 21 sn | 4 | auth-acceptance-host | `ExceptionTextLeakTests` | Mongo |
| 18 | 21 sn | 2 | auth-acceptance-host | `ExitCodeAlignmentTests` | — |
| 19 | 21 sn | 4 | auth-acceptance-host | `WireLevelErrorMappingTests` | Mongo |
| 20 | 21 sn | 72 | mdm-api | `FgFix1HttpTests` | Mongo |
| 21 | 21 sn | 26 | platform-app | `TransactionOwnedAuditDeliveryMongoTests` | Mongo |
| 22 | 19 sn | 3 | platform-bgjobs | `PlatformAdministratorSeedCompositionTests` | Mongo |
| 23 | 19 sn | 22 | platform-app | `PlatformAdministratorSafetyMongoTests` | Mongo |
| 24 | 18 sn | 24 | platform-bgjobs | `PlatformContainerValidationTests` | Mongo, HTTP host |
| 25 | 18 sn | 60 | mdm-api | `LskuFix3HttpTests` | Mongo |

## 7. Bu çalışmada düşen test ve bilinen aralıklı kırmızılar

- **Bu çalışmada:** auth-app ·
  `ProductIdentityEntitlementReconciliationMongoTests.Commit_WhenWriterAppearsAfterSnapshot_CannotFinalizeSuccess(drift: "new-token")`
  · `QUIESCENCE_DRIFT`.
- **Son 36 çalışmada (2026-10-21…23) aralıklı kırmızılar:**
  - auth-app 8 kez (`ProductIdentityEntitlementReconciliationMongoTests`, farklı satırlar);
  - mdm-app 5 kez (`FirstGskuConcurrencyMongoTests`, `MdmTestMongoTests.BL595_The_stop_needs_pid_start_and_executable_path`);
  - auth-acceptance-host'ta PR #205 öncesinde 5/38 (2'si PR #205 ile düzeltilen yarış; kalan 3'ü `ExitCodeAlignmentTests`,
    `SevenStateAcceptanceTests.SigkillAfterReady…`, `WireLevelErrorMappingTests.SupervisorClosesChannelDuringSeed…`).
- Bunlar zamana bağlı Mongo ve süreç testleri. Kök nedenleri bu raporun kapsamında araştırılmadı.

## 8. CI'da koşmayan test projeleri

39 test projesinin 28'i service-tests'te. Kalan 11:

| Proje | [Fact]/[Theory] | Nerede koşmalı | Bugün |
|---|---:|---|---|
| frontend/Diten.Web.Tests | 1.858 | phase1 (`run_phase1_gates.sh:246`) | **koşmuyor** — phase1 156. satırda duruyor |
| tests/architecture/TenantArchitecture.ArchitectureTests | 79 | phase1 (`:237`) | **koşmuyor** — aynı neden |
| tests/tenancy/TenantArchitecture.TenancyTests | 3 | phase1 (`:234`) | **koşmuyor** — aynı neden |
| Diten.CrmService.Application.Tests | 2.382 | — | hiçbir işte yok |
| Diten.PpmService.Tests | 238 | — | hiçbir işte yok |
| Diten.PpmService.IntegrationTests | 70 | — | hiçbir işte yok (sabit `/opt/homebrew/bin/mongod` yolu) |
| Diten.ManagementGovernanceService.IntegrationTests | 74 | — | hiçbir işte yok (sabit Homebrew yolu, 3 dosya) |
| Diten.ManagementGovernanceService.ArchitectureTests | 57 | — | hiçbir işte yok |
| Diten.HcmService.Application.Tests | 52 | — | hiçbir işte yok |
| Diten.Application.Tests (Enterprise Strategy) | 31 | — | hiçbir işte yok |
| Diten.EnterpriseStrategy.EndToEnd.Tests | 4 | — | hiçbir işte yok |

service-tests guard'ı (`CiServiceTestsGuardTests`) "NotInThisJob" listesi tutuyor. Bu raporda listenin gerekçeleri
yeniden incelenmedi.

## 9. phase1-gates kırmızısı (dokunulmadı, yalnız kayıt)

- **Ne:** `scripts/check_pack_nav_keys.py` iki yeni menü anahtarı buluyor:
  - `execution/domains/supply-chain-execution/module-packs/MOD-0176-expiry-fefo-management.md:645` ·
    `Nav.Module.EXPIRY-FEFO-MANAGEMENT`; çalışma zamanı `Nav.Module.EXPIRYFEFOMANAGEMENT` okuyor.
  - `execution/domains/supply-chain-execution/module-packs/MOD-0177-recall-readiness.md:915` ·
    `Nav.Module.RECALL-READINESS`; çalışma zamanı `Nav.Module.RECALLREADINESS` okuyor.
  - Özet satırı: "14 key(s) off form, 12 known, 2 new".
- **Ne zamandan beri:** her iki paket main'e PR #183 ile girdi (2026-10-21, SCE · MOD-0173). O günden önce phase1
  daha ileride, `test architecture` adımında kırmızıydı.
- **Etkisi:** `run_phase1_gates.sh` `set -euo pipefail` ile koşuyor; menü anahtarı kontrolü (156. satır) betiği durduruyor.
  Mimari, tenancy ve Web testleri (234–246. satırlar) 2026-10-21'den beri main'de hiç koşmadı. Bu üç projenin bugünkü
  durumu bilinmiyor.
- **Sahip:** Supply Chain (SCE) paketleri. XMC-001: bu oturum dokunmadı.

## 10. 3. ve 4. adım için ölçümden çıkan öneri (karar bekliyor)

**3. adım · yavaş testleri yerelde dışarıda bırakmak.**

- Etiket ölçütü önerisi:
  - `Category=Mongo`: §3'teki Mongo işaretli sınıflar (dosya düzeyinde 551 dosya).
  - `Category=Slow`: tek başına ≥ 5 sn süren 59 test ve §6'daki sınıflar.
- Yerel varsayılan komut `--filter "Category!=Mongo&Category!=Slow"` olur; CI filtresiz koşmaya devam eder (workflow
  değişmez).
- Beklenen yerel kazanç (CI süreleriyle tahmin, ölçülmedi):
  - platform-app'te test süresinin ~%88'i;
  - auth-app'te ~%90'ı Mongo testlerinde;
  - geriye kalan testler bu projelerde birkaç dakikadan kısa sürmeli.
- Etiketleme çok sayıda dosyaya dokunur. Etiketler başka modüllerin test dosyalarına (Platform, Auth, MDM, Inventory,
  SupplyChain, Planning) eklenecek, bu yüzden her modül için XMC-001 onayı gerekir.

**4. adım · "gereksiz" adayları.**

- Süre açısından kazanç küçük testlerde değil. 16.682 hızlı testi silmek CI süresini en çok ~2,5 dk azaltır; bu
  sürenin büyük kısmı da paralel çalışmada zaten gizleniyor.
- Aday listesi yavaş sınıflardan başlamalı: §6'daki ilk 10 sınıf, toplam test süresinin %43'ü.
- Her aday için tekrar ölçütü değişmez: korunan üretim satırı sabote edilir; başka bir test kırmızıya dönmüyorsa test
  gereksiz değildir.

## 11. Yeniden üretme

Ölçüm üç küçük betikle yapıldı. Betikler bu kayda eklenmedi; 3. adımda istenirse `scripts/` altına alınır.

1. `gh run download <run> --dir <boş klasör>` ile TRX'ler indirilir.
2. Test dosyaları taranır (işaretler, §2).
3. TRX süreleri sınıf işaretleriyle birleştirilir ve tablolar üretilir.
