# Yerelde hızlı test koşusu (TEST-HIZ-01)

Modül yazarken bir test projesinin hızlı kısmını yerelde koşmak için. Mongo'ya giden ve tek başına yavaş olan testler
dışarıda kalır. **CI (`service-tests.yml`) her testi filtresiz koşmaya devam eder**: bu komut CI'ın yerini almaz,
yalnız geliştirme sırasındaki bekleme süresini kısaltır.

## Komut

```bash
scripts/test-env/fast-local-tests.sh services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj
```

Yalnız bir bölümü koşmak için ikinci argümana ek bir filtre verilir. Bu filtre `Category!=Mongo&Category!=Slow` ile
birlikte uygulanır:

```bash
scripts/test-env/fast-local-tests.sh services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj "FullyQualifiedName~.Tasks."
```

Ondan sonraki argümanlar olduğu gibi `dotnet test`'e geçer (ör. `--no-build`). Mongo testlerini de koşmak gerekiyorsa
eski komut değişmedi: `dotnet test <proje>`, `DITEN_TEST_MONGOD` ile.

## Ne dışarıda kalır

| Etiket | Ne zaman konur |
|---|---|
| `[Trait("Category", "Mongo")]` (sınıfa) | Sınıfın en az bir testi gerçek bir mongod başlatıyor ya da ona bağlanıyor |
| `[Trait("Category", "Slow")]` (teste) | Mongo'suz ama tek başına ≥ 5 sn süren test (ör. bütün repoyu tarayan kaynak guard'ı) |

## Etiketi unutulan Mongo testi nasıl yakalanır

Komut, testlerin mongod aradığı bütün yolları (`DITEN_TEST_MONGOD`, `DITEN_FU02_MONGOD_PATH`,
`DITEN_ITEST_MONGOD_BIN_DIR`) `scripts/test-env/refuse-mongod/mongod`'a yönlendirir. Bu dosya hiçbir veritabanı
başlatmaz ve 97 koduyla çıkar. Komut ayrıca verilmiş bir lane adresini (`DITEN_PLATFORM_TEST_MONGO_URI`) kaldırır.

Etiketsiz bir Mongo testi bu yüzden sessizce mongod başlatmaz; kırmızı olur ve hatası `… mongod exited with code 97`
olur. Çözüm, o testin sınıfına `[Trait("Category", "Mongo")]` eklemektir.

## Hangi projelerde çalışır

Pilot yalnız `Diten.Platform.Application.Tests`. Komut başka bir proje verildiğinde koşmaz; etiketlenmemiş Mongo
testleri yanlış nedenle kırmızı olurdu. Proje etiketlendikçe betiğin `TAGGED_PROJECTS` listesine eklenir.

## Ölçüm (2026-10-23, geliştirici makinesi, Platform.Application.Tests)

| | Test | Süre |
|---|---:|---:|
| CI, filtresiz (service-tests 39921122915, "Test" adımı derleme dahil) | 8.714 | 9 dk 58 sn |
| Yerel hızlı koşu, `--no-build` | 6.898 | 30 sn |
| Yerel hızlı koşu, artımlı derlemeyle (günlük komut) | 6.898 | 49 sn |

- Dışarıda kalanlar 1.816 test (CI sayımıyla 8.714 − 6.898): 155 sınıftaki Mongo testleri ve 2 `Slow` test.
- Hızlı koşu sırasında yeni hiçbir mongod süreci başlamadı.
- Bir sınıfın Mongo etiketi kaldırıldığında komut kırmızıya döndü (`exited with code 97`); etiket geri konunca yeşile
  döndü.
- Kaynak: `docs/records/analysis/test-speed/`.
