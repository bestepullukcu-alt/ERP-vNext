# CI · Servis testleri (`service-tests`)

> BL-577 ile eklendi (2026-10-09); BL-590 ile Eventing ve kabul sunucusu satırları, MDM için replika kümesi;
> WP-CI-SERVICE-TESTS-03 (2026-10-10) ile takeover satırları (`auth-scr`, `mdm-api`) ve phase1 derleme adımları; FIX1 ile
> bütün iş akışı dosyaları bekçide, eylem → SHA sözlüğü, MDM Release ön koşulu; WP-CI-SERVICE-TESTS-04 ile diğer
> servislerin yeşil test projeleri (BL-604 A sınıfı), adlı "henüz yok" listesi ve yardım defteri tavanı (BL-609). İş dosyası `.github/workflows/service-tests.yml`; bekçisi
> `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs` korur.

## Ne koşar

`phase1-gates.yml` servis çözümlerini yalnız **derler**. `service-tests` onların testlerini **koşar**: Linux'ta,
her proje ayrı bir iş (matris, `fail-fast: false`).

| matris satırı | proje | yerel süre (macOS, 2026-10-09) |
|---|---|---|
| `platform-app` | `Diten.Platform.Application.Tests` | test 14 dk 8 sn (6685 test) |
| `platform-bgjobs` | `Diten.Platform.BackgroundJobs.Tests` | test 11 sn (42 test) |
| `platform-eventing` | `Diten.Platform.Eventing.Tests` | 68 test; yerelde özel RabbitMQ 4.3.2 düğümü; Mongo testin kendi mongod'u |
| `platform-composition-guard` | `Diten.Platform.CompositionGuard.Tests` | main'in Q366 guard'ı + bağlantı kaynağı testleri; Mongo testin kendi mongod'u (`PlatformTestMongo`) |
| `auth-app` | `Diten.AuthService.Application.Tests` | test 6 dk 52 sn (1617 test) |
| `auth-acceptance-host` | `Diten.AuthService.AccountKindAcceptanceHost.Tests` | test 3 dk 36 sn (68 test) |
| `auth-scr` | `Diten.AuthService.ServiceClientRegistry.Tests` | test 4 sn (160 test) |
| `mdm-app` | `Diten.MdmService.Application.Tests` | test 2 dk 30 sn (2503 test) — önce Platform.API ve MDM.Api **Release** derlenir (aşağıda) |
| `mdm-api` | `Diten.MdmService.Api.Tests` | test 7 dk 32 sn (762 test) |
| `bb-secrets` | `Diten.BuildingBlocks.Security.Secrets.Tests` | 5 test, 14 ms |
| `bb-email` | `Diten.BuildingBlocks.Email.Tests` | 109 test, 0,1 sn |
| `devenablement-api` | `Diten.DevEnablementService.Api.Tests` | 92 test, 3 sn — kendi mongod'u (`DITEN_ITEST_MONGOD_BIN_DIR`) |
| `mg-tests` | `Diten.ManagementGovernanceService.Tests` | 87 test, 0,1 sn |
| `procurement-api` | `Diten.ProcurementService.Api.Tests` | 147 test, 0,2 sn |
| `pvg-api` · `pvg-case-processing` · `pvg-meddra-coding` · `pvg-regpv-base` · `pvg-signal-management` | `Diten.PvgService.*.Tests` | 36 · 23 · 40 · 88 · 43 test, her biri < 0,5 sn |
| `gateway` | `gateway/Diten.ApiGateway.Tests` | ana dal birleşimiyle; Mongo / aracı yok |

Son on satır WP-CI-SERVICE-TESTS-04 ile geldi. Temiz bir `git worktree` kopyasında (bin / obj yok), Debug, işin koştuğu gibi
ölçüldü: derleme + test + `check_trx_no_skipped.py --suite`, hepsi yeşil, atlanan 0. Koşu boyunca 27017'ye (ortak dev
Mongo) bağlanan test süreci olmadı.

**`mdm-app` Release ön koşulu (FIX1, ölçüldü):** 38 MDM testi Platform.API'nin ve MDM.Api'nin RELEASE derlemesini
`bin/Release/net8.0`'dan yükler (çapraz servis sözleşmesi, `ApiStartupExecutionMode…`, `PlatformProductIdentityWorkflowClient…`
vb.). İşin test adımı Debug derler. Bu yüzden `mdm-app` satırında, testlerden önce iki proje `-c Release` derlenir. Bu
adım olmadan 38 test `FileNotFoundException` / "Release assembly was not found" ile kırmızıdır. Yerelde de aynı: o iki
proje Release derlenmeden MDM App koşusu bu 38'i kızartır.

Süreler yalnız test koşusudur; CI'da derleme ve mongod indirme eklenir. En uzun satır `platform-app`'tir. İşin
90 dakikalık sınırı ona göre geniş tutuldu: Linux koşucusu bu Mac'ten yavaş olabilir.

**Kendi mongod'unu açan satırlar:** `auth-scr` (`Support/OwnedMongo.cs`), `mdm-api` (`MdmHttpHost`),
`auth-acceptance-host` ve Platform'un geçici kümeleri: ikiliyi `DITEN_TEST_MONGOD` / `DITEN_ITEST_MONGOD_BIN_DIR`'den
alır, rastgele port, geçici veri. Bugün her satır böyle: işe özel replika kümesi ana dal birleşimi FIX1 ile kaldırıldı.

**Henüz işte olmayanlar (`NotInThisJob`, WP-CI-SERVICE-TESTS-04).** Bekçi `services/` altındaki **her** test projesini
(`*Tests.csproj`: `ArchitectureTests`, `IntegrationTests` dahil; Building Blocks dahil) matrisle karşılaştırır. Satırı
olmayan proje bu listede gerekçesiyle durur, yoksa kırmızıdır. Listeden ad çıkarmak satırını eklemek demektir; ikisi
çakışamaz. Bugünkü liste (BL-604 ölçümü):
- **Crm** `Application.Tests`: B, 1 kırmızı (PdfSharp sistem fontu ister).
- **Enterprise Strategy** `Application.Tests` (6 kırmızı, RBAC dahil), `EndToEnd.Tests` (2): B.
- **HCM** `Application.Tests`: C → B, `EmployeeDraftSessionRepositoryMongoTests` ortak `localhost:27017`'yi kullanır.
- **MG** `IntegrationTests`: C, sabit `/opt/homebrew/bin/mongod`; `DITEN_TEST_MONGOD`'dan okumalı.
- **MG** `ArchitectureTests`: B, 2 kırmızı; kardeş test DLL'lerini yükler (MG çözümü derlenmeli).
- **PPM** `Tests`: yeşil, ama **PPM ekibinin onayı bekleniyor** (sahip iletiyor).
- **PPM** `IntegrationTests`: B + C (10 kırmızı, sabit mongod yolu) ve onay.

B / C işleri sahiplerinindir (XMC-001); CT bu turda onlara dokunmaz.

- **Platform CompositionGuard.Tests** artık listede değil (WP-PLATFORM-COMPOSITION-GUARD-OWNED-01): verilmişse kendi
  `PLATFORM_COMPOSITION_GUARD_MONGO_URI`'si (main'in kuralıyla), yoksa `PlatformTestMongo` (`DITEN_TEST_MONGOD`) →
  `platform-composition-guard` satırı.
- **SupplyChainService.Tests** (main, SCE / MVP-6): `MVP6_MOD0192_MONGO_URI` (kendi replika kümesi, `CapacityTestMongo`);
  SCE'nin dosyası.

**Gateway:** `gateway/Diten.ApiGateway.Tests` bekçi kapsamında (`ServiceTestRoots`) ve `gateway` matris satırı. Çözümü
olmadığı için phase1 onu ayrıca derler. Matris satır adları tekil olmalı (`Every_matrix_suite_name_is_unique`).

**Tetik** (Control Tower kararı, Actions dakikası için): `pull_request`, yalnız `main`'e `push`, elle
(`workflow_dispatch`). Dal push'u bu işi koşmaz.

## phase1: çözüm dışındaki test projeleri derlenir

`phase1-gates.yml` her push'ta servis çözümlerini (`*.sln`) derler, servis testi koşmaz. Çözümde OLMAYAN bir test
projesini bu yüzden hiç derlemez: `e3d3a7331` `Platform.Eventing.Tests`'i kırdı, kimse görmedi.

- phase1 bu projeleri `scripts/run_phase1_gates.sh` içinde **yalnız derler** (`dotnet build … -c Debug`). Satırlar
  betikte, iş akışında değil (FIX1): yerel geçit koşusu ile CI aynı şeyi yapar.
  - `Diten.Platform.Eventing.Tests`;
  - `Diten.Platform.BackgroundJobs.Tests`;
  - `Diten.AuthService.AccountKindAcceptanceHost.Tests`.
- Testleri RabbitMQ / sahipli mongod ister, burada koşar.
- Bekçi (`Every_service_test_project_outside_its_solution_is_built_by_phase1`) çözüm dosyasını ve betiği okur, İKİ YÖNLÜ:
  - çözüm dışındaki her proje için betikte derleme satırı;
  - çözümdeki (ya da artık olmayan) bir proje için satır FAZLADIR, o da kırmızı.
  - Bir projeyi çözüme alırsanız satırını silersiniz; `dotnet test` istenmez.
- phase1 da `service-tests.yml` ile aynı kurala uyar (WP-CI-SERVICE-TESTS-03 eki):
  - `permissions: contents: read`;
  - sır okunmaz;
  - her eylem izin verilen üç eylemden biri ve COMMIT SHA'sına sabit (etiket değil).
  - SHA'lar 2026-10-10'da `@v4`'ün çözüldüğü sürümler: `checkout` v4.4.0, `setup-dotnet` v4.3.1. Davranış değişmedi.
  - Bekçi: `Phase1_has_a_read_only_token_and_every_action_pinned_to_a_commit`.

## Bütün iş akışları için kurallar (FIX1)

Bekçi `.github/workflows/` altındaki **her** dosyayı okur:
- **Adlı liste:** bugün yalnız `phase1-gates.yml` ve `service-tests.yml`. Yeni bir iş akışı dosyası bekçiye (`NamedWorkflows`)
  bilinçli eklenir, kurallarıyla; listede olmayan dosya kırmızıdır.
- **Tek salt okur jeton:** dosya başına tam bir üst düzey `permissions: contents: read`. İş ya da adım düzeyinde
  `permissions:` yok (`write-all`, `contents: write` kırmızı).
- **Yetkili tetik yok:** `pull_request_target`, `workflow_run`, `issue_comment` hiçbir dosyada yok. phase1 tetikleri `push`
  + `pull_request` olarak sabit.
- **Sır yok:** yorum dışında `secrets` sözcüğü yok (`secrets.X`, `secrets['X']`, `toJSON(secrets)`, `secrets: inherit`).
- **Eylem → SHA sözlüğü** (`ActionPins`), her eylem için TEK commit, iki dosyada da aynı:
  - `actions/checkout` → `11d5960a…` (v4.4.0);
  - `actions/setup-dotnet` → `67a3573c…` (v4.3.1);
  - `actions/upload-artifact` → `ea165f8d…` (v4.6.2).
  - Etiket, yazım hatası, çatal ağı commit'i, 40 sıfır kırmızıdır.
  - `uses:` değeri bir sonraki satırda yazılmışsa da okunur.
  - **Sabitler elle yükseltilir** (Dependabot yok; sahibin kararı): yeni sürümün etiket ↔ SHA eşleşmesi
    `git ls-remote https://github.com/actions/<eylem> "refs/tags/<etiket>" "refs/tags/<etiket>^{}"` ile ölçülür, sözlük ve
    iş akışı aynı değişiklikte güncellenir.
- **Kırmızı yeşile çevrilmez:**
  - hiçbir dosyada `continue-on-error` yok;
  - `Test`, `Install mongod …` ve `Run Phase 1 Gates` adımlarında adım düzeyinde `if:` yok;
  - iş düzeyinde `if:` yok.
- **mongod değişkenleri:** `DITEN_TEST_MONGOD` ve `DITEN_ITEST_MONGOD_BIN_DIR` yalnız (koşulsuz) kurulum adımında yazılır;
  hiçbir `env:` anahtarı onları ezemez (boş bir `DITEN_TEST_MONGOD`, sahipli mongod testlerini "bulunamadı"ya çevirirdi).
- **Canlı broker:** `CreateUsingRabbitMq` çağıran her test sınıfı `LiveRabbitMqCollection`'a katılır (tarama testi).

### Okuma kuralı: kapalı başarısız (FIX2, CT kararı)

Yeni paket yok (YAML ayrıştırıcı bir indirmedir); düzenli ifade yarışı yerine:
- Yalnız **tam satır** yorumları atılır (`^\s*#`). Satır içi `#` ve sonrası da taranır; yanlış pozitif kırmızıdır, kabul.
- Bütün aramalar büyük / küçük harf duyarsız (`SECRETS`, `Secrets['X']` da kırmızı).
- Anahtarlar her yerde sayılır: blok ya da akış biçimi (`{ runs-on: x, permissions: write-all }`), tırnaklı ya da değil
  (`"permissions":`). Beklenen sayıdan sapma kırmızı: `permissions` 1, `on` 1, `uses` = okunan değer sayısı, `run` =
  okunan blok sayısı, mongod adları anahtar olarak 0, `exclude` 0.
- Üst düzey belirteç bloğu tam `  contents: read`; araya boş satır ya da yorum girse de blok sonraki sütun-0 anahtarına
  kadar okunur (`actions: write` kırmızı).
- Her dosyadaki **bütün** `if:` değerleri beklenen listeyle, sırasıyla eşleşir (`ExpectedIfs`).
- Hiçbir `run:` bloğunda `||`, `; true`, `exit 0`, `set +e` yok; Test komutu ve `run: ./scripts/run_phase1_gates.sh` tam
  metin; betiğin yorum dışı ilk satırı `set -euo pipefail`. `dotnet test`'te `--filter` yok.
- Release ön koşul adımları sabit: `mdm-app` (Platform.API + MDM.Api) ve `platform-bgjobs` (yalnız Platform.API; çocuk
  host testi Release DLL'ini başlatır, temiz ağaçta ölçüldü).
- Canlı broker taraması her bildirim biçimini (`abstract`, `partial`, `static`, iç içe, `record`) ve her açıcıyı
  (`CreateUsingRabbitMq`, `UsingRabbitMq`, `ConnectionFactory`) okur; her çağrı onu kapsayan EN DIŞ sınıfa göre denetlenir.

### Sertleştirme ve bilinen sınırlar (son tur, CT kararı)

Ek kurallar (kapalı başarısız):
- CRLF normalleştirmesinden sonra iş akışlarında ve geçit betiğinde başka satır sonu yok (yalnız CR, NEL, U+2028 / U+2029).
- `||`, `; true`, `exit 0`, `set +…`, `trap` yasağı `run:` değerine değil, `if:` satırları dışındaki **bütün** metne uygulanır.
- `Skipped = 0` adımı da `Test` gibi tam metin (` | cat` olmaz).
- Geçit betiği:
  - 1. satır `#!/usr/bin/env bash`;
  - `||` tam 2 kez (denetim defteri grep'leri);
  - `exit 0`, `trap`, `set +`, `--filter` yok;
  - tenancy / architecture / web `dotnet test` satırları tam ve birer kez.
- `shell:` / `defaults:` anahtarı yok. İş düzeyi `SUITE` / `PROJECT` matris değerleri. `GITHUB_ENV` yazımları sayılı
  (service-tests 9, phase1 0). `GITHUB_PATH` yok. mongod ataması heredoc biçiminde de yakalanır.
- `github['token']`, `toJSON(` yasak.
- phase1 `push.branches: ["**"]` sabit; `timeout-minutes` en az 1.
- YAML düğüm özellikleri (`&`, `*`, `!`), `? anahtar`, onaltılık kaçış yok.
- Broker taramasında `//` yorum satırları öznitelik sayılmaz.

**Bilinen sınırlar** (bekçi metin okur, YAML değil; PR gözden geçirmesine kalır, CT backlog'a yazdı):
- YAML kaçış dizileri (`\x`, `\u` dışındakiler);
- takma adların ve birleştirme anahtarlarının (`<<`) bütün biçimleri;
- dizgi içinde dengesiz süslü parantezli C# sınıf gövdeleri;
- yeniden kullanılabilir iş akışları ve bileşik eylemler (bugün hiçbiri izinli değil).

## Süre sınırları

- Her iş `timeout-minutes: 90`.
- Her `dotnet test` `--blame-hang --blame-hang-timeout 20m` taşır. Asılan test işi bitirmez; adıyla durur ve döküm
  bırakır (8. ders).

## Platform testlerinin Mongo'su (ana dal birleşimi, CT kararı C)

Platform test projeleri (Application, BackgroundJobs, Eventing) Mongo adresini TEK yerden alır:
`services/Diten.Platform/tests/Shared/PlatformTestMongo.cs`, üç projeye bağlı (linked).

Sıra:
1. **`DITEN_PLATFORM_TEST_MONGO_URI` verilmişse** → o URI, main'in Q131a denetimiyle (PR #134), aynen:
   - düz `mongodb://`, kimlik bilgisi yok, her host loopback;
   - 27017–27021 yok; port yazılmamışsa 27017 sayılır;
   - boş değer "verilmemiş" değil, kırmızı.
   MVP-6 şerit betikleri (`scripts/test-env/mvp6-test-mongo-env.sh`) olduğu gibi çalışır.
2. **Yoksa `DITEN_TEST_MONGOD`** (Auth kuralı, BL-570) → test süreci KENDİ mongod'unu açar:
   - tek düğümlü replika kümesi (işlem destekli), boş bir loopback portunda (27022 ve üstü), geçici dizinde;
   - süreç bitince kapanır;
   - boş / göreli / olmayan yol → `OWNED_TEST_MONGO_INVALID`, kırmızı.
3. **İkisi de yoksa** → `OWNED_TEST_MONGO_UNAVAILABLE`, kırmızı. Atlama yok, 27017'ye ASLA düşmez.

Çocuk süreçler (kilit kanıtları) ebeveynin açtığı mongod'un adresini `DITEN_PLATFORM_TEST_MONGO_URI` üzerinden miras alır;
kendi mongod'larını açmazlar.

**CI:** `DITEN_TEST_MONGOD` her satırda kurulum adımında veriliyor; Platform satırları için iş akışına ek mongod
gerekmez. Eventing de artık kendi mongod'unu açar; işe özel replika kümesi FIX1 ile kaldırıldı (aşağıda).

**Yerelde** Platform testleri için değişkenlerden biri ZORUNLU:

```bash
export DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod
```

Neden: değişkensiz koşu eskiden dev'in ortak Mongo'suna (27017) yazıyordu. BL-595 ölçümü: 79 artık test veritabanı;
uykudan uyanınca zaman aşımı. Main'in Q121b ölçümü: 49 veritabanı.

**Bekçi:** `PlatformTestsOwnMongoGuardTests` — `services/Diten.Platform/tests` altında yorum dışı her `27017` /
`mongodb://localhost` adlı bir istisna ister (sayısıyla, gerekçesiyle). Yenisi kırmızı.

## mongod

- **Sürüm sabit:** 7.0.28. Dev ile aynı ana sürüm (`db version v7.0.28`).
- **Koşucu `ubuntu-22.04`:** MongoDB 7.0'ı Ubuntu 24.04 için yayımlamıyor (noble deposu 8.0'dan başlıyor;
  2026-10-09'da ölçüldü).
- **Resmi tarball:** SHA-256 özeti **iş dosyasına yazılı**. `sha256sum --check --strict` uymazsa iş kırmızı. Sunucudan
  inen `.sha256` dosyasına güvenilmez.
- **Ortam değişkenleri:**
  - `DITEN_TEST_MONGOD`: ikilinin tam yolu. Platform'un geçici mongod / replika kümesi bunu kullanır.
  - `DITEN_ITEST_MONGOD_BIN_DIR`: ikilinin dizini. Auth'un kabul sunucusu bunu kullanır.
  - `DITEN_FU02_MONGOD_PATH` (WP-CI-SERVICE-TESTS-05, BL-646 madde 1): aynı ikilinin tam yolu. Platform'un denetim-outbox
    zamansal fikstürü (`AuditOutboxTemporalReplicaSetFixture`, 4 sınıf) kendi tekil + replika mongod'unu bundan açar.
    Değişken yoksa fikstür PATH'e ve sabit kurulum yollarına bakar; koşucuda bulamaz ve PR #144'teki 325 test
    `AUDIT_OUTBOX_TEMPORAL_TEST_MONGOD_REQUIRED` ile kırmızıdır. Yalnız kurulum adımında yazılır; bekçi:
    `The_audit_outbox_mongod_variable_is_set_by_the_install_step_only`.
- **MDM App** (BL-595) kendi mongod'unu açar: tek yardımcı `MdmTestMongo`. İkiliyi `DITEN_TEST_MONGOD`'dan alır (Auth
  kuralı), rastgele port, geçici dizin, tek düğümlü replika kümesi, yalnız `127.0.0.1`. Değişken yoksa adlı kırmızı
  (`OWNED_TEST_MONGO_UNAVAILABLE`); ortak 27017'ye asla düşmez. Bağlantı ortamdan okunmaz (FIX2): `MDM_TEST_MONGO` yok sayılır;
  kendi replika kümesini kuran sınıflar yardımcıyı süreç içinde `MdmTestMongo.UseOwnedRedirect` ile ona yöneltir. **MDM Api** (`MdmHttpHost`) de kendi mongod'unu açar.
- **Eventing testleri** de kendi mongod'unu açar (`PlatformTestMongo`, ana dal birleşimi karar C).
- **İşe özel mongod YOK** (ana dal birleşimi FIX1, CT kararı D). Hiçbir test artık bir adres değişkeni okumuyor (ölçüldü):
  - MDM `MDM_TEST_MONGO`'yu bilerek yok sayar (BL-595 MDM);
  - Eventing `PlatformTestMongo`'ya geçti;
  - Platform harness'ı kendi mongod'unu açar.
  Bu yüzden "Start a job-local mongod replica set" adımı ve üç değişkeni (`MONGO_TEST_URI`, `MDM_TEST_MONGO`,
  `Eventing__MongoDb__ConnectionString`) kaldırıldı.
  - Bekçi: `No_row_starts_a_job_local_mongod_or_hands_the_tests_a_mongo_address`. İş akışında bir test Mongo adresi ya da
    job-local mongod geri gelirse kırmızı.
  - `scripts/mongo_rs_initiate.py` depoda kalır; yerel betikler kullanabilir.
- Ortak / dev Mongo'ya hiçbir zaman bağlanılmaz.

## RabbitMQ (Eventing satırı)

- **Sürüm sabit, özetle:** `rabbitmq:4.3.2@sha256:345dd3c1…`, dev ile aynı sürüm (Homebrew rabbitmq 4.3.2). Çoklu mimari
  dizin özeti Docker Hub'dan okundu (2026-10-09), iş dosyasına yazılı.
- **Servis konteyneri değil, adım:** `services:` bir matris satırı için açılamaz. Adım `docker run --network host`.
- **Hazırlık beklemesi sınırlı (WP-CI-SERVICE-TESTS-05, BL-646 madde D):** PR #144'te aracı yavaş açılmadı, çöktü:
  `Error when reading /var/lib/rabbitmq/.erlang.cookie: eacces`. Eski yoklama `docker run`'ın hemen ardından ROOT olarak
  `docker exec … rabbitmq-diagnostics` çalıştırıyordu; CLI sunucudan önce davranırsa çerezi root sahipli (mod 400) yaratır,
  imajın `rabbitmq` kullanıcısıyla çalışan sunucu onu okuyamaz. Şimdi: (1) önce koşucudan 127.0.0.1:5672'ye TCP bağlantısı —
  bağlanmadıkça hiçbir CLI çalışmaz, çerezi sunucu kendisi yazar; (2) her CLI çağrısı `docker exec -u rabbitmq` (çerezin
  sahibi): `check_running` + `check_port_listener 5672`; (3) konteyner çıktıysa hemen günlüğüyle kırmızı. En çok 60 deneme ×
  2 sn (120 sn). Aşılırsa `RABBITMQ_NOT_READY` ve konteyner günlüğünün sonu. Çerez değeri hiçbir yere yazılmaz (gizli değer
  yok). Bekçi: `RabbitMq_is_pinned_and_waited_for_and_writes_no_credential` (her `docker exec` `-u rabbitmq`, TCP önce,
  `ERLANG_COOKIE` yok). CI'da ölçülecek.
- **Kullanıcı yazılmaz:** varsayılan `guest` yalnız loopback'ten girer; `--network host` ile bağlantı loopback'tir. Sır
  yok; kullanıcı konteynerle birlikte ölür.
- Testlerin okuduğu ayarlar: `Eventing__RabbitMq__IntegrationTestsEnabled=true`, `Eventing__RabbitMq__Host=127.0.0.1`,
  `Eventing__RabbitMq__Port=5672`.

**Sürüm yükseltme:** `RABBITMQ_IMAGE` etiketi ve özeti birlikte değişir.

## Auth bayrağı

Auth satırları (`auth-*`) `DITEN_REQUIRE_OWNED_MONGO=1` taşır. BL-570 FIX3 ile etkinleşir: sahipli mongod bulunamazsa
test atlanmaz, kırmızı olur. O koda kadar etkisiz ve zararsız.

**Sürüm yükseltme:** `MONGODB_VERSION`, `MONGODB_TARBALL`, `MONGODB_SHA256` üçü birlikte değişir. Özet MongoDB'nin
resmi indirme sayfasından okunur ve dosyaya yazılır.

## Atlanan = 0

`dotnet test` atlanan testte 0 ile çıkar. Her iş bir TRX yazar; `scripts/check_trx_no_skipped.py` onu okur.
- Çalışmayan her testi **adıyla** basar ve 1 ile çıkar.
- Dosya yoksa, okunamıyorsa, bozuksa ya da hiç sonuç taşımıyorsa da 1 ile çıkar, büyük harfle `*** FAILED ***` ve
  `NOTHING WAS CHECKED` yazar.
- Kabul edilen tek atlamalar aşağıdaki adlı izin listesindekilerdir (FIX1). Listeye satır eklemek Control Tower kararıdır.
- Adım `if: always()`: test adımı kırmızı olsa da koşar, iki sonuç da görünür.
- TRX dosyaları `test-results-<satır>` adıyla yüklenir.

Yerelde:

```bash
python3 scripts/check_trx_no_skipped.py --suite <satır, ör. auth-app> <sonuç.trx>
```

### Adlı atlama izin listesi (FIX1, CT kararı)

BL-570 FIX4 (`6148288a8`) bazı Auth testlerini bu koşucuda doğru olamayacakları için ADIYLA atlar. Kabul edilen tek
atlamalar `scripts/ci-skip-allowlist.tsv`'dedir. Satır başına `suite<TAB>KOD<TAB>en çok<TAB>gerekçe` (FIX2: kod başına üst
sınır = bugün o kodu taşıyan test sayısı, `6148288a8`'de ölçüldü: her biri 1); bugün yalnız `auth-app`:
- `MACOS_ONLY`: Ubuntu'da her zaman (Linux kendi `/proc/self/fd` testiyle ölçülür);
- `UID_EQUALS_GID`: uid = gid olan imajda;
- `RUNS_AS_ROOT`: kök konteynerde (CAP_DAC_OVERRIDE).

Kurallar:
- Bir atlama, TRX'teki nedeni (`Output/ErrorInfo/Message`, ölçüldü) listedeki bir KODLA BAŞLIYORSA ve iş satırı (`--suite`)
  eşleşiyorsa geçer.
- Nedensiz atlama, bilinmeyen kod ya da başka satırın kodu KIRMIZI.
- Bir kodla üst sınırdan çok atlama KIRMIZI (yeni bir test kodu ödünç alamaz). Yalnız `NotExecuted` sonucu izinli atlama
  olabilir; izinli nedenli başka bir sonuç da kırmızıdır (FIX2).
- `UNIX_ONLY` hiçbir satırda yok: Ubuntu'da görülürse kırmızı.
- Kabul edilen atlamalar koda göre sayılıp iş günlüğüne yazılır
  (`allowed skips for suite auth-app: 3 (MACOS_ONLY x1, …)`).
- Listeye satır eklemek Control Tower kararıdır.
- **Backlog kodu tek koddur (WP-CI-SERVICE-TESTS-05):** neden `BL-534: …` ise kod `BL-534`'tür (`-<sayı>` eki koda dahil),
  `BL` değil. Bir satır yalnız o maddeyi adlandırır; komşu bir madde (`BL-535`) ya da çıplak `BL` kırmızıdır.
- **WP-CI-SERVICE-TESTS-05 satırları:**
  - `platform-app · BL-534 · 2`: `PlatformAdministratorSeedMeasurementTests`'in iki ölçüm testi (BL-534 sahip kararını
    bekliyordu). Karar verildi (WP-AUTH-ADMIN-BOOTSTRAP-01); o dalın birleşimiyle ikisi de koşan kurala döner: satır o
    birleşimle silinir, en geç 2026-11-14.
  - `mdm-api · CONTRACT_LINE_ABSENT · 1`: `ProductMasterContractFixtureTests` fikstürü sözleşme hattının dosyasıyla
    karşılaştırır (SOURCE.txt, yanındaki worktree); koşucuda o hat hiç yoktur. Atlama nedeni bu kodla başlar. Sahip CT,
    gözden geçirme 2026-12-31 (süresiz satır yok); hat dosyası CI'a gelirse satır silinir.
- Python testi listeyi BİREBİR sabitler (`test_the_repository_allowlist_holds_exactly_…`): satır eklemek ya da silmek o testi
  de değiştirmeyi gerektirir. Desenin birim testi: `BL-534: …` → `BL-534`; `BL`, `BL-`, `BL-534x`, `BL-abc`, boş → kabul yok.

Testleri Python'dadır (`scripts/tests/test_check_trx_no_skipped.py`). phase1 her push'ta koşar
(`run_phase1_gates.sh`: `python3 -m unittest discover -s scripts/tests`). Mimari bekçisi hem bu satırı hem iş akışının
`--suite "$SUITE"` geçirdiğini iddia eder.

MDM'de bilinen atlama koşulu yok (BL-595): MDM App testleri `MdmTestMongo` ile kendi mongod'unu açar; açamazsa adlı
kırmızıdır, atlamaz.

## Çapraz veritabanı kapısı: tek tam-yol istisna (FIX1, CT kararı)

`scripts/check_cross_db_enforcement.sh` (phase1) `new MongoClient(` çağrısını yalnız izinli yollarda kabul eder (izinli
desen `/Infrastructure/Persistence/`). `61af28b01` ile gelen
`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/VerifiedMarketOperationalPersistenceServiceCollectionExtensions.cs`
bu desene uymaz (klasör `Diten.Platform.Infrastructure`), kapı o commit'ten beri kırmızıydı.

- Dosyayı taşımak (tercih a) bu turda yapılamadı (taşıma/silme yasak); **tam yol** istisnası eklendi (b). Glob yok.
- İstisna yalnız o dosyada ve yalnız **bir** `new MongoClient(` için geçer; ikincisi kırmızı.
- Yanındaki başka bir dosya, aynı klasörde olsa bile, istisna kapsamında değildir.
- Dosya taşınırsa istisna kendiliğinden biter; satır kaldırılmalıdır.

Testleri: `scripts/tests/test_check_cross_db_enforcement.py` (üretim betiğini geçici ağaca kopyalayıp koşturur; son
durum gerçek depoda koşar).

## Yardım defteri tavanı taban dala karşı (BL-609)

`docs/roadmap/backlog/page-help-ledger.md` (UXH-001) tavanlarını başlıktaki tek satırda taşır:
`**Tavan (yalnız azalır): kiracı 246 · platform 18 · frozen 99**` (örnek; güncel değerler defterin kendi başlığındadır). Web testi satır sayısının tavanın altında kaldığını
ölçer, ama aynı PR'da yükseltilen tavanı göremez.

phase1 (`scripts/check_page_help_ledger_ceiling.py`) bu satırı PR'ın taban dalınınkiyle karşılaştırır:
- Tabandan büyük tavan KIRMIZI, adıyla.
- Kaybolan ya da yeni gelen kategori kırmızı. Bozuk başlık kırmızı.
- Bilinçli artış: defterde, gerekçeli bir CT satırı, kesin sayılarla:
  `- Tavan artışı (CT kararı): kiracı 236 → 240 — <gerekçe>`.
- Taban yoksa (push), defter bu dalda ya da tabanda yoksa adım **SKIPPED** der ve "NOTHING WAS CHECKED" yazar.
  Bugün defter bu dalda yok; UX-COMMON birleşince devreye girer.

Yerelde:

```bash
python3 scripts/check_page_help_ledger_ceiling.py --base origin/main
```

## İlk kırmızıların sınıflanması

İlk koşu sahibin push'unda olur (bu makinede Linux yok). Kırmızı bir satırda sırayla:

1. **Ortam mı?** mongod inmedi / özet uymadı / port dolu → iş dosyası. Test koduna dokunulmaz.
2. **Platforma özgü mü?** Windows'a bağlı API (adlı semafor, `kernel32` / `ntdll`) → BL-570 sınıfı. Bu dalda yok;
   MDM takeover birleşince gelir.
3. **Asılma mı?** `--blame-hang` dökümü adı verir.
4. **Gerçek regresyon:** test adıyla backlog'a, sahibine.

Hiçbir sınıf "atlansın" diye çözülmez: atlanan test de kırmızıdır.

### BL-646 (PR #144, ilk tam koşu) — WP-CI-SERVICE-TESTS-05

| madde | iş | neden | düzeltme | kanıt |
|---|---|---|---|---|
| 1 | platform-app (325) | denetim-outbox fikstürü mongod'u `DITEN_FU02_MONGOD_PATH`'ten arar | kurulum adımı değişkeni yazar | yerelde ölçüldü |
| 2 | platform-app (2) | `TaskMention`/`TaskComment` testleri `Diten.Platform.Api/…` okur; klasör `Diten.Platform.API` (macOS harf duyarsız, Linux değil) | test yolu `API`; tüm yol sabitleri tarandı, başka eşleşme yok | yerelde ölçüldü (yol); Linux harf duyarlılığı CI'da ölçülecek |
| 3 | platform-app (2) | `BL-534` ölçüm testleri atlanıyor | adlı satır `BL-534` | yerelde ölçüldü (betik) |
| A | platform-bgjobs (1) | `appsettings.Development.json` doğrulanmış GSKU pilotunu açıyor; alt sürecin boş veritabanında durum yok → `Verified GSKU operational state is incomplete` (yerelde de kırmızıydı) | testin alt süreç ortamı pilot bölümünü boşaltır; sağlık kuralı aynı | yerelde ölçüldü |
| B | mdm-api (1) | sözleşme hattı karşılaştırması koşucuda atlanır | atlama kodu + adlı satır (2026-12-31) | yerelde ölçüldü (betik) |
| C | auth-acceptance-host (3) | test desteği macOS `sysctl` çağırıyor (`EntryPointNotFound`) | `ProcessTiming` Linux dalı (`/proc/<pid>/stat` alan 22 + `btime`), harness onu kullanır; ayrıştırıcı birim testi | ayrıştırıcı yerelde ölçüldü; Linux koşusu CI'da ölçülecek |
| D | platform-eventing | RabbitMQ çerezi `eacces` (root CLI yarışı) | TCP önce, `docker exec -u rabbitmq`, çıkış anında kırmızı, 120 sn | CI'da ölçülecek |
| E | inventory-tests (16) | Prism fikstürü npx önbelleğini yalnız Windows yolunda arıyordu | npm'in kendi önbellek yolları | yerelde ölçüldü |

**C'nin sınırı:** karşı süreç doğrulaması (`VerifyPeerIsExpectedApiProcess`) yalnız macOS'tadır ve başka işletim
sisteminde kapalı başarısız olur. Linux'ta üç `LoginPeerVerificationTests` testi bu yüzden geçer: parola gönderilmez, ama
PID / başlangıç zamanı karşılaştırması ve yönlendirme reddi o OS reddinin arkasında kalır, yalnız macOS'ta ayrı ayrı
kanıtlanır. Süreç düzeyindeki testler (`SevenStateSupervisor` koleksiyonu: `proc_listpids`, `kinfo_proc` ebeveyn PID'i,
macOS `posix_spawn` bayrakları) Linux'ta hâlâ kırmızı olabilir; PR #144'teki `DescriptorAndEofTimingTests` iptali buna
bağlı görünür. Linux karşılığı (`SO_PEERCRED`, `/proc`) ayrı iştir; karar CT'nin.
