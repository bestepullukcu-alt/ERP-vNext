# CI · Servis testleri (`service-tests`)

> BL-577 ile eklendi (2026-10-09). İş dosyası `.github/workflows/service-tests.yml`; bekçisi
> `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs` korur.

## Ne koşar

`phase1-gates.yml` servis çözümlerini yalnız **derler**. `service-tests` onların testlerini **koşar**: Linux'ta,
her proje ayrı bir iş (matris, `fail-fast: false`).

| matris satırı | proje | yerel süre (macOS, 2026-10-09) |
|---|---|---|
| `platform-app` | `Diten.Platform.Application.Tests` | test 14 dk 8 sn (6685 test) |
| `platform-bgjobs` | `Diten.Platform.BackgroundJobs.Tests` | test 11 sn (42 test) |
| `auth-app` | `Diten.AuthService.Application.Tests` | test 6 dk 52 sn (1617 test) |
| `mdm-app` | `Diten.MdmService.Application.Tests` | test 20 sn (463 test; ürün işleri takeover'da) |

Süreler yalnız test koşusudur; CI'da derleme ve mongod indirme eklenir. En uzun satır `platform-app`'tir. İşin
90 dakikalık sınırı ona göre geniş tutuldu: Linux koşucusu bu Mac'ten yavaş olabilir.

İşte olmayan servis test projeleri, bekçide gerekçesiyle adlıdır (`NotInThisJob`):
- `Diten.Platform.Eventing.Tests`: RabbitMQ entegrasyonu; iş aracı başlatmaz.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests`: BL-577 listesinde yok.

Yeni bir servis test projesi (ör. takeover'dan gelen MDM Api, Auth ServiceClientRegistry) bekçide **kırmızıdır**. Ya
satırını eklersiniz ya da Control Tower kararıyla `NotInThisJob`'a adlandırırsınız.

**Tetik** (Control Tower kararı, Actions dakikası için): `pull_request`, yalnız `main`'e `push`, elle
(`workflow_dispatch`). Dal push'u bu işi koşmaz.

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
- **MDM testleri** kendi mongod'larını açmaz, `MONGO_TEST_URI` / `MDM_TEST_MONGO`'ya bağlanır (varsayılan
  `localhost:27017`). MDM satırında iş, aynı ikiliyle `127.0.0.1:27017`'de **işe özel** tek düğümlü bir mongod
  başlatır.
- Ortak / dev Mongo'ya hiçbir zaman bağlanılmaz.

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

Bilinen atlama koşulu: `LegalEntityMongoRoundTripTests` (MDM) `Skip.IfNot(IsMongoReachable())`, `MONGO_TEST_URI`
yanıt vermezse atlar. CI'da işe özel mongod bunu koşturur.

## İlk kırmızıların sınıflanması

İlk koşu sahibin push'unda olur (bu makinede Linux yok). Kırmızı bir satırda sırayla:

1. **Ortam mı?** mongod inmedi / özet uymadı / port dolu → iş dosyası. Test koduna dokunulmaz.
2. **Platforma özgü mü?** Windows'a bağlı API (adlı semafor, `kernel32` / `ntdll`) → BL-570 sınıfı. Bu dalda yok;
   MDM takeover birleşince gelir.
3. **Asılma mı?** `--blame-hang` dökümü adı verir.
4. **Gerçek regresyon:** test adıyla backlog'a, sahibine.

Hiçbir sınıf "atlansın" diye çözülmez: atlanan test de kırmızıdır.
