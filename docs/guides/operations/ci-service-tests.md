# CI · Servis testleri (`service-tests`)

> BL-577 ile eklendi (2026-10-09); BL-590 ile Eventing ve kabul sunucusu satırları, MDM için replika kümesi;
> WP-CI-SERVICE-TESTS-03 (2026-10-10) ile takeover satırları (`auth-scr`, `mdm-api`) ve phase1 derleme adımları; FIX1 ile
> bütün iş akışı dosyaları bekçide, eylem → SHA sözlüğü, MDM Release ön koşulu. İş dosyası `.github/workflows/service-tests.yml`; bekçisi
> `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs` korur.

## Ne koşar

`phase1-gates.yml` servis çözümlerini yalnız **derler**. `service-tests` onların testlerini **koşar**: Linux'ta,
her proje ayrı bir iş (matris, `fail-fast: false`).

| matris satırı | proje | yerel süre (macOS, 2026-10-09) |
|---|---|---|
| `platform-app` | `Diten.Platform.Application.Tests` | test 14 dk 8 sn (6685 test) |
| `platform-bgjobs` | `Diten.Platform.BackgroundJobs.Tests` | test 11 sn (42 test) |
| `platform-eventing` | `Diten.Platform.Eventing.Tests` | test 6 sn (68 test; yerelde özel RabbitMQ 4.3.2 düğümü + replika kümesi) |
| `auth-app` | `Diten.AuthService.Application.Tests` | test 6 dk 52 sn (1617 test) |
| `auth-acceptance-host` | `Diten.AuthService.AccountKindAcceptanceHost.Tests` | test 3 dk 36 sn (68 test) |
| `auth-scr` | `Diten.AuthService.ServiceClientRegistry.Tests` | test 4 sn (160 test) |
| `mdm-app` | `Diten.MdmService.Application.Tests` | test 2 dk 30 sn (2503 test) — önce Platform.API ve MDM.Api **Release** derlenir (aşağıda) |
| `mdm-api` | `Diten.MdmService.Api.Tests` | test 7 dk 32 sn (762 test) |

**`mdm-app` Release ön koşulu (FIX1, ölçüldü):** 38 MDM testi Platform.API'nin ve MDM.Api'nin RELEASE derlemesini
`bin/Release/net8.0`'dan yükler (çapraz servis sözleşmesi, `ApiStartupExecutionMode…`, `PlatformProductIdentityWorkflowClient…`
vb.). İşin test adımı Debug derler. Bu yüzden `mdm-app` satırında, testlerden önce iki proje `-c Release` derlenir. Bu
adım olmadan 38 test `FileNotFoundException` / "Release assembly was not found" ile kırmızıdır. Yerelde de aynı: o iki
proje Release derlenmeden MDM App koşusu bu 38'i kızartır.

Süreler yalnız test koşusudur; CI'da derleme ve mongod indirme eklenir. En uzun satır `platform-app`'tir. İşin
90 dakikalık sınırı ona göre geniş tutuldu: Linux koşucusu bu Mac'ten yavaş olabilir.

**Kendi mongod'unu açan satırlar:** `auth-scr` (`Support/OwnedMongo.cs`), `mdm-api` (`MdmHttpHost`),
`auth-acceptance-host` ve Platform'un geçici kümeleri: ikiliyi `DITEN_TEST_MONGOD` / `DITEN_ITEST_MONGOD_BIN_DIR`'den
alır, rastgele port, geçici veri. `mdm-api` adı işe özel replika kümesini de başlatır (`mdm-*`); test onu kullanmaz,
zararsız.

**Platform / Auth / MDM** için işte olmayan servis test projesi bugün **yok**. Bekçinin `NotInThisJob` listesi BL-590'dan
beri boş; liste, ileride bir Control Tower kararının yeri olarak durur. Kural ona dayanmaz: diskteki projeleri matrisle
karşılaştırır. Bu üç servise yeni bir test projesi gelirse bekçide **kırmızıdır**: ya satırını eklersiniz ya da Control
Tower kararıyla `NotInThisJob`'a adlandırırsınız.

**Diğer servislerin testleri CI'da hiç yok (BL-604).** Ne derleniyor ne koşuluyor; phase1 yalnız gateway, Auth, Platform
ve MDM çözümlerini derler. Ölçüm (2026-10-10, `*.Tests.csproj`):
- Crm (1), Hcm (1), DevEnablement (1), ManagementGovernance (1), Ppm (1), Pvg (5), Procurement (1), EnterpriseStrategy (2);
- Building Blocks (2): `Email.Tests` `Diten.Platform.sln`'de, derleniyor ama koşulmuyor; `Security.Secrets.Tests` hiçbiri.
- Bekçi bugün yalnız Platform / Auth / MDM `tests/` dizinlerini tarar.

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

## Süre sınırları

- Her iş `timeout-minutes: 90`.
- Her `dotnet test` `--blame-hang --blame-hang-timeout 20m` taşır. Asılan test işi bitirmez; adıyla durur ve döküm
  bırakır (8. ders).

## mongod

- **Sürüm sabit:** 7.0.28. Dev ile aynı ana sürüm (`db version v7.0.28`).
- **Koşucu `ubuntu-22.04`:** MongoDB 7.0'ı Ubuntu 24.04 için yayımlamıyor (noble deposu 8.0'dan başlıyor;
  2026-10-09'da ölçüldü).
- **Resmi tarball:** SHA-256 özeti **iş dosyasına yazılı**. `sha256sum --check --strict` uymazsa iş kırmızı. Sunucudan
  inen `.sha256` dosyasına güvenilmez.
- **Ortam değişkenleri:**
  - `DITEN_TEST_MONGOD`: ikilinin tam yolu. Platform'un geçici mongod / replika kümesi bunu kullanır.
  - `DITEN_ITEST_MONGOD_BIN_DIR`: ikilinin dizini. Auth'un kabul sunucusu bunu kullanır.
- **MDM App ve Eventing testleri** kendi mongod'larını açmaz, bir URI'ye bağlanır: `MONGO_TEST_URI` / `MDM_TEST_MONGO` /
  `Eventing__MongoDb__ConnectionString` (varsayılan `localhost:27017`). **MDM Api** (`MdmHttpHost`) ise kendi mongod'unu açar.
- **İşe özel mongod (MDM, Eventing satırları):** aynı ikiliyle `127.0.0.1:27017`'de **tek düğümlü bir replika kümesi**
  (BL-590).
  - Neden: MDM işlem (transaction) kullanır; işlem replika kümesi ister. Bağımsız (standalone) mongod'da işlem testleri
    kırmızıdır.
  - `--replSet rs0` ile başlar.
  - `scripts/mongo_rs_initiate.py` kümeyi başlatır ve birincil olana kadar bekler. En çok 60 sn; aşılırsa adlı hata
    (`REPLICA_SET_NOT_PRIMARY`, `REPLICA_SET_NOT_ENABLED`, `MONGOD_NOT_REACHABLE`).
  - Betik yalnız python3 standart kütüphanesidir: 7.0 tarball'ında kabuk (`mongosh`) yok, ayrı indirme ve özet gerekmez.
  - Bağlantı dizesi `mongodb://127.0.0.1:27017/?directConnection=true`.
- Ortak / dev Mongo'ya hiçbir zaman bağlanılmaz.

Yerelde aynı adım (dev Mongo 27017'de çalıştığı için başka port):

```bash
python3 scripts/mongo_rs_initiate.py --port 27117 --set rs0 --timeout 60
```

## RabbitMQ (Eventing satırı)

- **Sürüm sabit, özetle:** `rabbitmq:4.3.2@sha256:345dd3c1…`, dev ile aynı sürüm (Homebrew rabbitmq 4.3.2). Çoklu mimari
  dizin özeti Docker Hub'dan okundu (2026-10-09), iş dosyasına yazılı.
- **Servis konteyneri değil, adım:** `services:` bir matris satırı için açılamaz. Adım `docker run --network host`.
- **Hazırlık beklemesi sınırlı:** `rabbitmq-diagnostics check_port_connectivity`, en çok 60 deneme × 2 sn. Aşılırsa
  `RABBITMQ_NOT_READY` ve konteyner günlüğünün sonu.
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
- İzinli atlama listesi **yok**. Bir atlama Control Tower kararıdır.
- Adım `if: always()`: test adımı kırmızı olsa da koşar, iki sonuç da görünür.
- TRX dosyaları `test-results-<satır>` adıyla yüklenir.

Yerelde:

```bash
python3 scripts/check_trx_no_skipped.py <sonuç.trx>
```

### Adlı atlama izin listesi (FIX1, CT kararı)

BL-570 FIX4 (`6148288a8`) bazı Auth testlerini bu koşucuda doğru olamayacakları için ADIYLA atlar. Kabul edilen tek
atlamalar `scripts/ci-skip-allowlist.tsv`'dedir. Satır başına `suite<TAB>KOD<TAB>gerekçe`; bugün yalnız `auth-app`:
- `MACOS_ONLY`: Ubuntu'da her zaman (Linux kendi `/proc/self/fd` testiyle ölçülür);
- `UID_EQUALS_GID`: uid = gid olan imajda;
- `RUNS_AS_ROOT`: kök konteynerde (CAP_DAC_OVERRIDE).

Kurallar:
- Bir atlama, TRX'teki nedeni (`Output/ErrorInfo/Message`, ölçüldü) listedeki bir KODLA BAŞLIYORSA ve iş satırı (`--suite`)
  eşleşiyorsa geçer.
- Nedensiz atlama, bilinmeyen kod ya da başka satırın kodu KIRMIZI.
- `UNIX_ONLY` hiçbir satırda yok: Ubuntu'da görülürse kırmızı.
- Kabul edilen atlamalar koda göre sayılıp iş günlüğüne yazılır
  (`allowed skips for suite auth-app: 3 (MACOS_ONLY x1, …)`).
- Listeye satır eklemek Control Tower kararıdır.

Testleri Python'dadır (`scripts/tests/test_check_trx_no_skipped.py`). phase1 her push'ta koşar
(`run_phase1_gates.sh`: `python3 -m unittest discover -s scripts/tests`). Mimari bekçisi hem bu satırı hem iş akışının
`--suite "$SUITE"` geçirdiğini iddia eder.

Bilinen atlama koşulu: `LegalEntityMongoRoundTripTests` (MDM) `Skip.IfNot(IsMongoReachable())`, `MONGO_TEST_URI`
yanıt vermezse atlar. CI'da işe özel mongod bunu koşturur.

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

## İlk kırmızıların sınıflanması

İlk koşu sahibin push'unda olur (bu makinede Linux yok). Kırmızı bir satırda sırayla:

1. **Ortam mı?** mongod inmedi / özet uymadı / port dolu → iş dosyası. Test koduna dokunulmaz.
2. **Platforma özgü mü?** Windows'a bağlı API (adlı semafor, `kernel32` / `ntdll`) → BL-570 sınıfı. Bu dalda yok;
   MDM takeover birleşince gelir.
3. **Asılma mı?** `--blame-hang` dökümü adı verir.
4. **Gerçek regresyon:** test adıyla backlog'a, sahibine.

Hiçbir sınıf "atlansın" diye çözülmez: atlanan test de kırmızıdır.
