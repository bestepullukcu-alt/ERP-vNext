# PVG sekizinci dalga — bağımsız doğrulama: WP-0230-DL10 (AC-13, AC-35)

> Control Tower için bağımsız ölçüm kaydı, 2026-11-01. Kabul ya da entegrasyon kaydı değildir; kabul CT'nindir.
> Karar: **REDDEDİLDİ.** Bir engel, bir ORTA, dokuz DÜŞÜK bulgu açık. Engel depo kodunda değil: zorunlu AC-35 (b)'nin 404 şartı karşılanmıyor ve bu sapma onaysız. Kiracı yalıtımı ölçümle sağlam. Düzeltme dar. DL-10'un gerekli kanıtı E4, kabul ayrıca canlı kapıya bağlı kalır.

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl10` |
| İş commit'i | `990ceb5fb382b7df18079e6fbac654b0c7a39ca0` (`990ceb5fb`); HEAD^1 = Expected HEAD `da3cb0ce9dd45cdebe0a528dc76648bd48bd9abc` (ölçüldü) |
| CT dalına birleştirme | Yok; push yok |
| Paket | `MOD-0230-case-intake-triage.md`, sha256 `61c7902f…850279` (prompt'la aynı, ölçüldü) |
| Prompt | `WP-0230-DL10-v1.1.md`, sha256 `e244e401…c97e` (DoR ile aynı, ölçüldü) |

Ölçümü işi yazmayan bir ölçücü (alt ajan) yaptı. Süitler kendi worktree'mde `990ceb5fb` üzerinde koştu. Sabotajlar ve yoklama testi, `git archive` ile kurulan karalama kopyada koştu (kopyada git komutu çalışmadı). İşçi raporu bana verilmedi; kanıt sayılmadı. Yalnız test yorumundan, işçinin D-1 adlı bir sapma yazdığını biliyorum.

## Bulgular

| Önem | Bulgu | Yol | Sonraki adım |
|---|---|---|---|
| **ENGEL** | AC-35 (b) yazıldığı gibi karşılanmıyor. Paket: başka kiracının GET ve PUT'u "return 404 with the same body as an unknown id". Ölçülen: dört istek de **409**, gövde `PVG_INTAKE_DRAFT_NOT_FOUND`. Testin adı `..._return_404_like_an_unknown_id`, ama 404'ü ölçmüyor: `NotEqual(200)` ve "bilinmeyen id ile aynı" der. Çekirdek kural 2 de kiracılar arası 404 ister. | `PvgMongoApiTests.cs:56-65`; `PvgCaseIntakeTriageEndpoints.cs:233` | Aşağıdaki "Engelin okuması" |
| ORTA | Biçimi bozuk connection string, parolasıyla birlikte iletiye ve stderr'e düşüyor. `FromConnectionString` korumasız. Gerçek ikilide ölçüldü: `mongodb:/fake-user:<parola>@…` → çıkış -6, stderr `The connection string '…<parola>…' is not valid.` Ret kodu da yok. Prompt: connection string "hiçbir iletiye, log'a" yazılmaz. Yalnız yapı/test kapısında ve değer bozuksa oluşur; üretim benzeri host daha önce reddeder. Bunu ölçen test yok. | `PvgMongoContext.cs:20` | İşçi düzeltir: ayrıştırmayı sar, değeri taşımayan güvenli kodla reddet, sahte parolalı test ekle |
| DÜŞÜK | Depo çalışırken durursa iş uçları 5,0 saniye sonra **gövdesiz 500** döner (POST ve GET ölçüldü; adres sızmıyor). Paket bunu "kabul edilemez" sayar (satır 935, 999). Bellek içi depoda bu yol yoktu, DL-10 ile ulaşılır oldu. Düzeltme yeri AC-6 (DL-4). | `PvgCaseIntakeTriageEndpoints.cs` (korunan yol) | DL-4C kapsamına depo kesintisi satırı eklenir |
| DÜŞÜK | Depo değeri paketten gevşek. `mongo` ve `MONGO` Mongo seçer; boş değer host üzerinden bellek içine düşer. Paket: "any other value refuses". Üretim benzeri host büyük/küçük harfe bakmadan reddeder (kod okuması); tarama küçük harfi yakalar (C11). Güvenli yöne düşüyor. | `PvgServiceApiHost.cs:200-209` | Sıkılaştır ya da pakete yaz |
| DÜŞÜK | `AddSecretsProvider` çağrılmıyor. `ValidateRequiredSecrets` ve `configuration[...]` kullanılıyor (Inventory `DependencyInjection.cs` ile aynı). Paket "through the secrets provider", prompt "Inventory `Program.cs` deseni" der. Doğrulama çalışıyor; eksik değer mesajı yalnız anahtar adını taşıyor. | `PvgPersistenceServiceCollectionExtensions.cs:48-52` | CT okuması |
| DÜŞÜK | Replace'in kiracı karşılaştırması test edilmemiş (H6 yeşil). Kod bugün reddediyor (yoklama: `PVG_PERSISTENCE_TENANT_SCOPE_MISMATCH`, sürüm 1 kaldı). Testler bunu yalnız `AddAsync`'te ölçüyor. Satır düşerse `$set` `TenantId` yazdığı için kayıt başka kiracıya geçebilir (kod okuması, koşulmadı). | `PvgMongoIntakeDraftStore.cs:64` | `Store_edge_cases`'e replace satırı |
| DÜŞÜK | Replace'in alan alan yazımı ölçülmüyor (N1 yeşil). T1 yalnız ekleme yolunu ölçer. `$set` dışlama listesine bir alan girerse kayıt sessizce eski değerde kalır. Bugünkü liste doğru. | `PvgIntakeDraftDocumentMapper.cs:14-17` | T1'e replace turu |
| DÜŞÜK | Triyaj ve yönlendirme yazımlarının aktörü ölçülmüyor (J9, J10; Api ve RegPvBase süitlerinde yeşil). Paket (c2) yalnız create/update ister. Kod dört çağrıda da doğru. | `PvgIntakeDraftApplicationService.cs:325,421` | Bilgi; K2 yazmaları açılmadan kapatılmalı |
| DÜŞÜK | Replace'te `UpdatedAt` ve `CreatedAt` ölçülmüyor (J11, J12 yeşil). Kod doğru (yoklama: `CreatedAt` aynı, `UpdatedAt` ilerledi). | `PvgMongoIntakeDraftStore.cs:86` | Test satırı |
| DÜŞÜK | İşlemin `snapshot` okuması ölçülmüyor (N3 yeşil). Paket şartı; kod doğru. `w1` ise dolaylı yakalanıyor (L4: adı geçen test yeşil, başka 12 test kırmızı). | `PvgMongoContext.cs:13-14` | Bilgi |
| DÜŞÜK | Küçük test boşlukları. C9: taramanın iç içe JSON dalı fikstürde yok, ama gerçek taramada çalışıyor (C10 kırmızı). G7: eşitlik kırıcı `IntakeDraftId` düşünce yeşil; süzgeçsiz listede index aynı sırayı verir, durum süzgeçli listede eşit zaman fikstürde yok. | `PvgPersistenceBoundaryScanTests.cs:341`; `PvgMongoIntakeDraftStore.cs:164` | Bilgi |
| Bilgi | D3 (`IsDeleted = false` satırı silinir) yeşil: `bool` varsayılanı zaten false, eşdeğer mutant. Paketin istediği davranış D2 ile ölçüldü. | `PvgIntakeDraftDocumentMapper.cs:32` | — |
| Bilgi | Paket, denetim `ThrowIfUnsafe` arkasına alınınca iki satırın da kırılacağını söyler. Ölçümde yalnız `Production` satırı kırmızı (C2). İkinci satır, denetim daha sonraya alınırsa kırılır. | — | Bilgi |
| Bilgi | Paket iki commit-hatası testinin "aynı" dedicated replica set'i kullandığını söyler; her test kendi mongod'unu başlatıyor. Yalıtım daha güçlü. | `PvgMongoDedicatedTests.cs:98,129` | Bilgi |

### Engelin okuması

Bu benim önem kararım; karar CT'nindir. Gerekçe: AC-35 zorunlu, DL-10'un bitişi ona bağlı ve paket bağlayıcı. Bir işçi onaylı pakette bir şartı kendi başına yeniden yorumlayamaz. Prompt bu durum için açık: "Paket metniyle bu prompt çelişiyorsa … yazma, DUR" (`WP-0230-DL10-v1.1.md:91`). İşçi durmadı, sapmayı test yorumuna yazıp devam etti.

Çelişki gerçek ve işçiden önce var:

- Paket AC-35 (b) 404 ister (satır 1563). Aynı paketin AC-6'sı ise 404'ü "Not applied until that approval" sayar (satır 1521; MOD-0041 / Ops onayı bekliyor) ve G-8 bugünkü 409'u yazar (satır 247).
- Prompt API sözleşmesini değiştirmeyi yasaklar (`:71`), `PvgCaseIntakeTriageEndpoints.cs` korunan yoldur (`:7`).
- Kabul edilmiş DL4B testi aynı durumda 409'u sabitler (`PvgCaseIntakeAuthenticationTests.cs:155`).
- Prompt incelemesi de yanlış okumuş: "kiracılar arası erişim 404 (… mevcut AC-6 davranışı)" (`WP-0230-DL10-v1.0-inceleme-01.md:14`). Böyle bir davranış yok.

Yani 404, bu dilimin izinli yollarıyla yapılamaz. Etki sınırlı: kiracılar arası yanıt bilinmeyen id ile bayt düzeyinde aynı (ölçüldü), kayıt verisi yok, PUT kaydı değiştirmiyor, tenant filtresi sabotajı testi kırmızıya çeviriyor (H2, H3, H5). Sızıntı yok; ihlal edilen şey sözleşme ve onay zinciri.

Çözüm iki yoldan biri, sahip kararı ister:

1. **Paket düzeltmesi (önerim):** AC-35 (b) "bilinmeyen id ile aynı durum ve gövde; 404, AC-6 / DL-4C ile" olur, test adı buna uyar, test bugünkü durumu açıkça sabitler. Onay satırı gerekir. Revizyon 15 taslağıyla birlikte düşünülebilir.
2. **Önce DL-4C:** 404 uygulanır. MOD-0041 / Ops onayını bekler, DL-10'u ve onu bekleyen DL-5'i uzatır.

Birinci yol onaylanırsa bu bulgu test adı düzeltmesine iner. O zaman işe kalan: ORTA bulgusu ve test düzeltmesi. Dar bir tekrar ölçümü yeter.

## AC alt maddeleri

Her ad pakette yazıldığı gibi var (TRX'te ölçüldü). "Kod" sütunu test adından değil kod okumasından.

| Madde | Test | Ölçtüğü | Kod | Sabotaj |
|---|---|---|---|---|
| AC-13 (a) | `Only_the_persistence_project_references_the_Mongo_driver`, `Only_the_Api_and_MOD0230_test_projects_reference_the_persistence_project` | Evet | Evet; `grep MongoDB *.csproj` yalnız Persistence | A1–A6 kırmızı |
| AC-13 (b) | `Mongo_store_creates_exactly_the_catalog_indexes_each_starting_with_TenantId`, `Pvg_database_holds_only_the_named_collections`, `Catalog_declares_the_tenant_received_index_with_received_descending`, `Persistence_indexes_are_tenant_first_and_do_not_create_global_unique_ids` | Evet (ad, alan, sıra, yön, teklik) | Evet | B1–B8 kırmızı |
| AC-13 (c) | `Mongo_store_is_refused_on_a_production_like_host`, `In_memory_store_is_the_default_and_mongo_is_opt_in`, `Unknown_store_value_refuses_start`, `No_committed_configuration_selects_the_mongo_store` | Evet | Evet; değer gevşekliği DÜŞÜK | C1–C8, C10, C11 kırmızı; C9 yeşil |
| AC-13 (d) | `Mongo_store_never_reads_counts_or_replaces_a_soft_deleted_draft`, `Mongo_store_writes_IsDeleted_false_and_no_DeletedAt_on_create` | Evet | Evet | D1, D2, D4–D7 kırmızı; D3 eşdeğer |
| AC-13 (e) | `Store_contract_and_mongo_store_have_no_delete_bulk_delete_archive_void_or_export_member` | Evet | Evet | E1 kırmızı |
| AC-13 (f) | `Pvg_host_registers_only_the_named_hosted_services` | Evet | Evet | F1 kırmızı |
| AC-13 (g) | Mevcut korumalar | Süitlerde yeşil | Deny-all dosyaları değişmedi | Paket sabotaj istemiyor |
| AC-35 (a) | `Mongo_ReplaceAsync_with_stale_version_returns_conflict_not_not_found`, `Mongo_list_orders_by_received_descending_then_draft_id_and_pages`, `Mongo_count_with_and_without_status_matches_the_in_memory_store` | Evet; iki depoda theory | Evet | G1–G6, N2 kırmızı; G7 yeşil |
| AC-35 (b) 1 | `Mongo_store_never_reads_counts_or_replaces_another_tenants_draft` | Evet | Evet | H1, H3–H5 kırmızı |
| AC-35 (b) 2 | `Cross_tenant_get_and_put_on_the_mongo_store_return_404_like_an_unknown_id` | **Hayır: 404 ölçülmüyor** | **Hayır: 409** | H2 kırmızı (`NotEqual(200)`) |
| AC-35 (c) | `Draft_survives_a_host_restart_on_the_mongo_store` | Evet | Evet | I1, I2 kırmızı |
| AC-35 (c2) | `Mongo_store_writes_CreatedBy_and_UpdatedBy_from_the_validated_subject`, `Create_and_update_requests_do_not_accept_CreatedBy_or_UpdatedBy`, `Body_createdBy_and_updatedBy_never_reach_the_store` | Evet | Evet; aktör `Endpoints.cs:555` yalnız doğrulanmış claim | J1–J8, J13 kırmızı; J9–J12 yeşil |
| AC-35 (d) | `Unreachable_mongo_store_refuses_start`, `Ready_returns_503_when_the_selected_mongo_store_stops_answering` | Evet | Evet | K1–K8 kırmızı (K6: 30,2 sn) |
| AC-35 (e) | `Mongo_write_is_lost_when_its_transaction_commit_fails`, `Mongo_replace_is_lost_when_its_transaction_commit_fails`, `Mongo_store_refuses_start_without_transaction_support` | Evet (bir `commitTransaction`, fail point `finally`'de kapanıyor) | Evet | L1–L3, L5 kırmızı; L4 başka testlerde; N3 yeşil |
| AC-35 (f) | `Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod`, `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` | Evet | Evet | M1–M3 kırmızı |
| T1 | `Mongo_store_round_trips_every_SafetyCaseIntake_field` | Evet; yansımayla dolu fikstür, milisaniye altı | Evet; replace turu yok (DÜŞÜK) | T1a–T1e kırmızı; N1 yeşil |
| T2 | `Mongo_concurrent_replace_of_the_same_version_has_one_winner` | Evet; 30 tur | Evet | T2a kırmızı |

## Temiz ölçülenler

- **Kapsam.** 29 dosya, hepsi İzinli yollarda; `Program.cs`, Endpoints, Domain, Gateway, `tests/architecture` yok. `git diff --check` çıkış 0. Mevcut testlerde silinen 22 satır yalnız imza güncellemesi; silinen `Fact`, `Theory`, `InlineData` ya da `Assert` 0, eklenen `Skip` 0. Sır, gerçek connection string ve gerçek kişi verisi yok; değerler sentetik, JWT anahtarı koşuda üretiliyor. Worktree koşulardan sonra temiz.
- **Testler.** Her koşudan önce `uptime`; 1 dakikalık yük 2,70–3,71, bekleme olmadı.

  | Süit | Taban | Geçen | Başarısız | Atlanan |
  |---|---|---|---|---|
  | `Diten.PvgService.Api.Tests` | 298 | 335 | 0 | 0 |
  | `Diten.PvgService.RegPvBase.Tests` | 210 | 211 | 0 | 0 |
  | `CaseProcessing` | 23 | 23 | 0 | 0 |
  | `MeddraCoding` | 40 | 40 | 0 | 0 |
  | `SignalManagement` | 43 | 43 | 0 | 0 |
  | `TenantArchitecture.ArchitectureTests` | 143 + 3 kırmızı | 143 | 3 | 0 |

- **Mimari kırmızılar.** `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders`, `AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`, `AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous`. `origin/main` (`0504c96ea`) kopyasında koşuldu: aynı üç ad (173 geçen, 3 kırmızı). Yeni kırmızı yok; kırmızı çıktılarda Pvg satırı yok; `Diten.PvgService` sayımı `Debt: 13, Exceptions: 0` değişmedi.
- **`known_failures.py check`.** `pvg-api` ve `pvg-regpv-base`: ikisi çıkış 0, `BİLİNEN=0 YENİ=0`.
- **Güvenlik.**
  - Her okuma, sayım ve replace `TenantId` ve `IsDeleted = false` taşır (kod ve sabotaj). Insert `IsDeleted=false`, `DeletedAt=null` yazar (ham belge). Yoklama kiracı değeri uydurmaz; ping koleksiyon okumaz.
  - Üretim benzeri host bağlantıdan önce reddeder. `Production`, boş, boşluklu ve `production` ortam adlarında `PVG_PERSISTENCE_BUILD_TEST_ONLY`. Gerçek ikili 0,1 sn'de çıkış -6, çıktıda değer yok.
  - Kapalı portta gerçek ikili 5,3 sn'de `PVG_PERSISTENCE_STORE_UNREACHABLE`, çıktıda parola ve adres yok. Bilinmeyen değer `PVG_PERSISTENCE_STORE_UNKNOWN`.
  - Bootstrap log'u yalnız istisna türünü yazar. Hazırlık gövdesi yalnız güvenli kod.
  - Varsayılan depo bellek içi. Commit'li yapılandırma Mongo seçmiyor; PvgService'te `appsettings` ve `launchSettings` yok.
  - Dört deny-all bağdaştırıcı değişmedi. Seed, grant, `BackgroundService`, zamanlayıcı, `AddCentralAudit`, MediatR, `EnsureIndexesAsync` yok.
- **CI.** `pvg-api` ve `pvg-regpv-base` satırları `DITEN_TEST_MONGOD` kuran ortak iş içinde (`service-tests.yml`, okundu; CI koşulmadı).
- **Sabotajlar.** 87 sabotaj. Çapa tam bir kez eşleşti (hata 0), derleme hatası 0. Kopyada önce yeşil görüldü (335, 211, mimari 143 + aynı 3 kırmızı). Hepsi sha256 ile geri yazıldı; bitişte PvgService kaynakları worktree ile aynı (`diff -rq`; tek fark C11'den kalan boş `Properties` klasörü). Sabotajlar sırasında en yüksek yük 10,05.

  | Grup | Sayı | Kırmızı | Yeşil |
  |---|---|---|---|
  | Paket ve prompt adlı | 47 | 47 | 0 |
  | Benim seçtiğim | 40 | 29 + L4 (başka testlerde) | 10 |

  Yeşil kalanlar: C9, D3, G7, H6, J9, J10, J11, J12, N1, N3. Nedenleri yukarıdaki bulgularda. Kırmızılar ölçülen iddiayla kırıldı; ilk hata satırları `results.jsonl` ve `logs/` içinde.

  Benim seçtiklerimden öne çıkanlar: tenant filtresi düşürme H3 (replace), H4 (list), H5 (sınıflandırma); sürüm artmaması G4; açılışta koleksiyon düşürme I2; `StartAsync` boş K3 ve arka planda K4; başlıktan aktör J8; test kancasını Api'den çağırma A6; ret iletisinde adres K5; 5 sn zaman aşımı düşmesi K6.

  Geri dönüş sha256 (ilk 12 hane, her sabotajda aynı):

  | Dosya | sha256 |
  |---|---|
  | `PvgMongoIntakeDraftStore.cs` | `3ef6fbc45a34` |
  | `PvgMongoSchemaBootstrap.cs` | `6e703691a5a4` |
  | `PvgMongoContext.cs` | `7b56e8cb91d3` |
  | `PvgMongoStoreReadiness.cs` | `ae7ae785f791` |
  | `PvgIntakeDraftDocument.cs` | `b64772594119` |
  | `PvgIntakeDraftDocumentMapper.cs` | `75f3364a38f9` |
  | `PvgPersistenceServiceCollectionExtensions.cs` | `1c5abd6ae763` |
  | `PvgServiceApiHost.cs` | `14df98d4d869` |
  | `Diten.PvgService.Api.csproj` | `b22955d5af99` |
  | `PvgCaseIntakeTriageEndpoints.cs` | `afc57591c926` |
  | `PvgIntakeDraftApplicationService.cs` | `5a55e34a63f9` |
  | `PvgIntakeDraftIndexCatalog.cs` | `b70d2e820f22` |
  | `PvgMongoApiTests.cs` | `8c558e84590f` |
  | `PvgMongoTestData.cs` | `306745ba812b` |
  | `PvgMongoTestHarness.cs` | `476ec8b94303` |
  | `PvgPersistenceBoundaryScanTests.cs` | `06cc3ed637bd` |

- **Süreç.** Bitişte tek mongod geliştirme süreci (PID 898, 20 Ekim'den beri; dokunulmadı). Geride kalan test mongod'u 0, geçici `diten-pvg-intake-mongo-*` klasörü 0. Benim başlattığım dotnet süreci kalmadı; görülen `VBCSCompiler` (PID 61804) koşularımdan önce başlamıştı, dokunulmadı. 27017 ve 5011'e bağlanılmadı.

## Ölçülmeyenler

- Canlı host ve Gateway (E4), geliştirme mongod'u üzerinde açılış. İşçi 5011'i bağlamadı, ben de bağlamadım. Dört kapı reddettiği için canlı yazma zaten ölçülemez (DoR).
- CI'da gerçek koşu; Linux. Her şey Mac'te mongod 7.0.28 ile koştu.
- Parent `da3cb0ce9`'da süit koşulmadı; taban sayıları görevden alındı. Mimari taban `origin/main`'de ölçüldü, parent'ta değil.
- İşçi raporu, sapma kaydı D-1'in rapordaki metni, işçinin kendi sabotaj kanıtı.
- `Pvg:Runtime:ProductionLike=true` ile `mongo` küçük harf birleşimi koşulmadı (kod okuması).
- Replace'te kiracı karşılaştırması düşerse kaydın başka kiracıya geçmesi koşulmadı (kod okuması).
- Bellek ve büyük veri davranışı: durum süzgeçli listenin bellekte sıralaması (paket kabul ediyor).

## Kanıt yolları

Karalama dizinleri, `/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/0ff520a7-0e9b-4ed4-a37e-f30a6fcdb17e/scratchpad/` altında:

- `dl10-sab01/` sabotaj kopyası; `dl10-main01/` `origin/main` kopyası
- `dl10-sabotage/run.py` tüm sabotajlar ve çapalar; `dl10-sabotage/results.jsonl`; `dl10-sabotage/logs/`
- `dl10-suites/` süit çıktıları ve `uptime.log`
- `dl10-probe/DL10VerifyProbeTests.ran.cs`, `dl10-probe/out.txt`, `dl10-probe/real_binary.py`
