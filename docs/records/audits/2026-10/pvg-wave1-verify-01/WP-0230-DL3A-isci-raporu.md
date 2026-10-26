# WP-0230-DL3A · PVG-WP-0230-DL3A-P v1.1 — SOP §22 raporu (geliştirici)

**Agent Verdict:** PASS (geliştirici görüşü; kapanış değildir — K13, kabul CT'nindir). Tek açık nokta: DOĞRULA 4'ün mimari kontrolü çıkış 1 veriyor; aynı üç kırmızı HEAD içeriğinde de aynı parmak iziyle var (Blockers).

## Branch / HEAD
`feature/pvg/wp-0230-dl3` @ `95563dae14c441e4f9ed5e86874c46fc0f649e06` (beklenenle aynı). Commit / push / stash / dal değişikliği yok.

## Worktree status
Başlangıç temizdi. Son `git status --porcelain`: yalnız üç İzinli yol `M`. `git diff --check` çıkış 0. TestResults/ git'e görünmüyor.

## Changed files
- `services/Diten.PvgService/src/Diten.PvgService.Api/PvgServiceApiHost.cs` (+108/−?)
- `services/Diten.PvgService/src/Diten.PvgService.Api/Program.cs` (yeniden yazıldı, 22 satır)
- `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs` (yalnız ekleme: +508, silinen satır 0)
- sha256 (son): Program.cs `f0992070…f9a1`, PvgServiceApiHost.cs `79975…3623`, Tests `fd1e59…92ae`

## Golden/Contract flow
- Tek bağımlılık listesi: `PvgServiceApiHost.cs:19` `RequiredDependencies` (beş tip + güvenli kod). Açılış doğrulaması (`:119`) ve `/health/ready` (`:101`) aynı listeyi `MissingRequiredDependencyCodes` (`:138`) üzerinden kullanır.
- Kurulum yöntemi `PvgServiceApiHost.cs:28` `CreatePvgServiceWebHostBuilder(string? environmentName, IConfiguration configuration, string urls, Action<IServiceCollection>? configureServices = null)` → `IWebHostBuilder`. `null` ortam → `Environments.Production` yöntemin içinde (`:41`). Sıra: `AddRouting` → `AddPvgServiceApiHost` → geri çağrı → TEK doğrulama çağrısı (`:49`). Configure: `UseRouting` + `MapPvgServiceHealthEndpoints` + `MapPvgCaseIntakeTriageEndpoints` (`:59`).
- `Program.cs:3` üst düzey kod yalnız `CreateWebHostBuilder(args).Build().Run();`. `Program.cs:7` `public static IWebHostBuilder CreateWebHostBuilder(string[] args)`: ortam değişkeni + komut satırı yapılandırması, `ASPNETCORE_ENVIRONMENT` (yoksa null → yöntemde Production), `ASPNETCORE_URLS` (varsayılan `http://127.0.0.1:5011`), `Program.cs:20` kurulum yöntemi çağrısı. Çalışma davranışı eskisiyle aynı (aynı builder zinciri, aynı varsayılanlar).

## Sub-flows
- `/health/live` değişmedi (`:98`).
- `/health/ready` 200 gövdesi değişmedi (eski `PvgServiceHealthStatus.Ready`); 503 yeni `PvgServiceNotReadyStatus` (`:203`).

## Failure paths
- Üretim benzeri + eksik bağımlılık → `InvalidOperationException` (Build sırasında, ConfigureServices içinde; AggregateException değil). ValidateOnBuild / UseDefaultServiceProvider kullanılmadı.
- `/health/ready` eksik bağımlılıkta her ortamda 503 + yalnız eksik kodlar; boş gövdeli 500 yok.
- Satır 711 / 722'deki iki eski ret ve testleri aynen (testler artık 717 / 728'de, gövdeleri değişmedi; diff'te silinen satır yok).

## Tests
Taban çizgisi (değişiklikten önce, Debug): Api 36/36, RegPvBase 88/88, CaseProcessing 23/23, MeddraCoding 40/40, SignalManagement 43/43; 0 kırmızı.
Son:
- DOĞRULA 1: `pvg-api.trx` total 58 / passed 58 / failed 0; `ASPNETCORE_ENVIRONMENT=Development` ile `pvg-api-dev.trx` 58/58/0. (Sabotajlardan sonra tekrar: 58/58/0.)
- DOĞRULA 2: `pvg-regpv-base.trx` 88/88/0 (taban çizgisiyle aynı).
- DOĞRULA 3: CaseProcessing 23/23, MeddraCoding 40/40, SignalManagement 43/43; 0 kırmızı.
- DOĞRULA 4: `known_failures.py check … --suite pvg-api` çıkış 0; `--suite pvg-regpv-base` çıkış 0; mimari: 146 test, 3 kırmızı, `known_failures check` **çıkış 1** (Blockers).
CT eki testler (satırlar test dosyasında):
1. `Production_like_startup_refuses_when_a_required_dependency_is_missing` :773 — Theory 5 satır
2. `Production_like_authorized_host_build_refuses_without_required_dependencies` :793
3. `Production_like_startup_refusal_message_carries_only_safe_dependency_codes` :809
4. `Health_ready_returns_503_with_safe_codes_when_a_required_dependency_is_missing` :839 — Theory 10 satır
5. `Health_ready_returns_200_when_all_required_dependencies_resolve` :874 — Theory 2 satır
6. `Setup_method_host_maps_health_and_case_intake_routes` :891
7. `Program_create_web_host_builder_refuses_production_like_build_without_required_dependencies` :926
8. `Program_top_level_statements_only_call_create_web_host_builder` :944
36 + 5 + 1 + 1 + 10 + 2 + 1 + 1 + 1 = 58.
Hazırlık ölçüm yolu yardımcıları: `BuildConfiguredSetupMethodHost` :1025 (kurulum yöntemi → Build → host.Services'teki `IStartup` + `IApplicationBuilderFactory` ile hostun kendi Configure'u; sunucu/Start/Run yok) ve `InvokeMappedHealthReadyAsync` :1049 (hostun `EndpointDataSource`'undan GET `/health/ready` RouteEndpoint'i, `RequestDelegate`'i `DefaultHttpContext` ile: Method GET, Path /health/ready, RequestServices = host kapsamı, Body = MemoryStream). Bütün Health_ready_* testleri ve rota testi bunlardan geçer.

Sabotajlar (her biri `scratchpad/sabotage.py` ile uygulandı, Api paketi koşuldu, geri alındı; geri alma sonrası sha256 üç dosyada başlangıçla aynı):
- S1 Store kaydı silindi → 15 kırmızı: Development_host_wires… ; Health_ready_returns_200 ×2 (Development, Staging); Health_ready_returns_503 Store dışı 8 satır. Fazladan: Case_intake_route_metadata…, Case_intake_triage_endpoints_define…, Case_intake_triage_route_templates…, Health_endpoints_are_defined… (WebApplication Development ValidateOnBuild ile).
- S2 doğrulama çağrısı kaldırıldı → 8 kırmızı: refuses_when… 5 satır, host_build_refuses…, refusal_message…, Program_create_web_host_builder_refuses…. Satır 740'taki eski test (şimdi :746) yeşil kaldı (beklenen).
- S3a /health/ready hep 200 → Health_ready_returns_503 10 satırın hepsi kırmızı.
- S3b 503 gövdesine sabit [PVG_DEPENDENCY_INTAKE_STORE] → o bağımlılığa ait olmayan 8 satır kırmızı (Store satırları yeşil, beklenen).
- S4 liste yalnız Store → 15 kırmızı: refuses_when… diğer 4 satır, Health_ready_503 diğer 8 satır, refusal_message…, host_build_refuses…, Program_create_web_host_builder_refuses….
- S5 hazırlık yalnız Development'ta → Health_ready_503 Staging 5 satırı kırmızı.
- S6 Program.CreateWebHostBuilder içinde eski satır içi kurulum → Program_create_web_host_builder_refuses…, Program_top_level_statements… kırmızı.
- S7 üst düzey kod eski satır içi kuruluma döndü (CreateWebHostBuilder duruyor) → Program_top_level_statements… kırmızı.
- S8 /health/ready eşlemesi eski sabit lambda (mantık yöntemi duruyor) → Health_ready_503 10 satır kırmızı.
- S9 MapPvgCaseIntakeTriageEndpoints çağrısı kaldırıldı → Setup_method_host_maps_health_and_case_intake_routes kırmızı.
TRX'ler: `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/TestResults/sabotage-{1,2,3a,3b,4,5,6,7,8,9}.trx`.

## Persistence evidence
N/A — kalıcılık değişmedi; InMemoryPvgIntakeDraftRepository ve DenyAll* adaptörleri değişmedi.

## Security/RBAC/Tenant evidence
İzin anahtarı / kiracı / kimlik doğrulama değişmedi (DL-4/DL-6). Ret mesajı ve 503 gövdesi tip adı, ad alanı, yapılandırma değeri, sır, kişi verisi taşımaz (testler 3 ve 4: "IPvg", "Diten.", "System.", "Exception" yok). `OperationalRuntimeAuthorized=true` yalnız test içi bellek yapılandırmasında ve test içinde `Program.CreateWebHostBuilder`'a verilen argümanda.

## Audit/Evidence
Kanıt düzeyi E2 (testler; Build var, Run / Start / Main / dinleyen host yok). Hiçbir süreç ortam değişkeni değiştirilmedi.

## Observability
Yeni log yok. Hazırlık 503 gövdesi yalnız güvenli kodlar.

## Migration/Rollback
Migration yok. Geri alma: üç dosyanın HEAD sürümüne dönüşü.

## Decisions
- "does not resolve" = kayıtlılık yorumu — CT kararı (2026-10-23), dayanak paket satır 157 "checked during service registration or provider build" (geliştirici kararı değil, yalnız anılır). Açılış: `IServiceCollection`'da anahtarsız (non-keyed) tanımlayıcı var mı; hazırlık: `IServiceProviderIsService.IsService` (aynı kayıtlılık anlamı, çözme yok).
- Güvenli kodlar (tek liste, `PvgServiceApiHost.cs:19`): IPvgFieldSecurityPolicy → `PVG_DEPENDENCY_FIELD_SECURITY`; IPvgWorkflowTransitionGate → `PVG_DEPENDENCY_WORKFLOW_GATE`; IPvgEvidenceLinkPort → `PVG_DEPENDENCY_EVIDENCE_LINK`; IPvgPermissionGate → `PVG_DEPENDENCY_PERMISSION_GATE`; IPvgIntakeDraftStore → `PVG_DEPENDENCY_INTAKE_STORE`.
- Ret mesajı: `"PVG production-like startup refused; required dependencies are not registered: "` + eksik kodlar `", "` ile, liste sırasıyla.
- 503 gövdesi (`PvgServiceNotReadyStatus`, JSON web varsayılanı): `status` ("not-ready"), `mode`, `operationalRuntimeAuthorized`, `nonProductionAdaptersEnabled`, `missingDependencyCodes` — başka alan yok. 200 gövdesi eski `PvgServiceHealthStatus` (kod listesi yok).
- Kurulum yöntemi: `PvgServiceApiHost.CreatePvgServiceWebHostBuilder(string? environmentName, IConfiguration configuration, string urls, Action<IServiceCollection>? configureServices = null)`.
- Doğrulama yalnız üretim benzeri hostta; MOD-0231/0232/0234 kayıtları aynen, denetlenmez. Satır 740 testi değişmedi (CT "extended" kararı).

## Blockers
- DOĞRULA 4 mimari kontrolü: `known_failures.py check … --suite TenantArchitecture.ArchitectureTests` çıkış 1 (KISMEN-DÜZELDİ 1, YENİ-AYRINTI 2): `AuditTrailStandardTests.EveryWriteCommand_IsAudited…` (Diten.Platform RegisterSignableActionsCommand / VerifySignatureCommand yeni öğe), `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders` (msg parmak izi değişmiş), `EveryServiceWithWriteCommands_HasALedger…` (KISMEN-DÜZELDİ). Hiçbiri PVG'ye değinmiyor (mesajlarda "Pvg" 0). Kanıt: üç dosya geçici olarak HEAD içeriğine çevrilip (git status temiz) aynı paket koşuldu → `architecture-head.trx`, `fingerprint` çıktısı benim değişikliğimle koşulan `architecture.trx` ile **birebir aynı**; sonra dosyalar geri kondu (sha256 aynı). Yani regresyon bu WP'den değil; kayıt (tests/known-failures.tsv) güncel değil. Kayıt / Platform düzeltmesi bu WP'nin İzinli yollarında değil — CT kararı.

## Known gaps
- **Kayıtlı ama çözülemeyen bağımlılık yakalanmaz** (CT kayıtlılık yorumu): kayıtlı bir bağımlılığın kendi bağımlılığı eksikse açılış reddi ve /health/ready bunu görmez.
- **Sıfırdan farklı süreç çıkış kodu E2'de ölçülmez** (paket satır 722): süreç başlatılmadı; Build'deki `InvalidOperationException` ile temsil edildi.
- Kısmi gerçekleştirme denetimi: prompt'un saydığı sekiz kısmi biçim sabotaj/testle kapandı. Ek gözlem (kapsam dışı, değiştirilmedi — "çalışma davranışı değişmez"): `ASPNETCORE_ENVIRONMENT` **boş dize** ise `?? Production` devreye girmez, ortam adı "" olur ve host üretim benzeri sayılmaz (`ProductionLike` bayrağı yoksa üretim dışı adaptörlerle açılır). Bu eski davranışla aynıdır; fail-closed için ayrı bir karar/WP önerilir.
- BG-9 → WP-0230-DL3B. BG-10 açık — OD-PVG-6.

## Out-of-scope changes
Yok. Korunan yollara, csproj'lara, PvgCaseIntakeTriageEndpoints.cs'e, appsettings / launch.json'a dokunulmadı.
