# TEST-HIZ-01 · aralıklı kırmızılar ve Auth hızlı koşu: kapanış (2026-10-26)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, bu oturumda beş işin hepsini istedi:
> - (1) QUIESCENCE_DRIFT reddini koruyan test;
> - (2) acceptance-host'un aralıklı testleri;
> - (3) Auth için yerel hızlı komut;
> - (4) MDM'nin aralıklı testleri;
> - (5) Auth'ta gereksiz el sıkışma reddinin üretimde düzeltilmesi.
>
> İş aynı dalda birikir; push öncesi sorulur.
> **Önceki kayıt:** `2026-10-26-test-hiz-01-auth-flaky.md`. O kayıttaki §3 (yeşil mutant) ve §4 (üretim notu) burada kapanıyor.

## 1. QUIESCENCE_DRIFT: commit reddini koruyan testler (`6e4077ec7`)

| Sabotaj (`EntitlementReconciliationOperationStore.CommitAsync`) | Önce | Sonra |
|---|---:|---:|
| işlemden önceki drift kontrolü kapatıldı | 0 kırmızı | 1 kırmızı (`…AfterThePlan…`) |
| işlemin içindeki drift kontrolü kapatıldı | 0 kırmızı | 1 kırmızı (`…DuringTheCommit…`) |

- Her sabotaj yalnız kendi testini kırmızıya çeviriyor; öteki test yeşil kalıyor.
- Sınıf 56/56 yeşil.

## 2. acceptance-host (`a0d7e509b`)

- `SigkillAfterReady_BackupGroupCleansUpApiAndMongod`: SIGTERM'den sonra sabit 1 sn bekliyordu. Artık API ve mongod
  kapanana kadar bekliyor (en çok 15 sn).
- `SupervisorClosesChannelDuringSeed_HostReportsSupervisorLostExit6`: host'a 20 sn veriyordu. Host kapalı kanalı ancak
  seed'den sonra fark ediyor; artık 90 sn.
- `ExitCodeAlignmentTests`: yerelde 30 başlatmada tekrar üretilemedi. Host'un yanıtı test çıktısına yazılıyor; bir
  sonraki kırmızı kodunu söyleyecek.
- Hiçbir assert değişmedi. İki sabotajın ikisi de önce ve sonra 1 kırmızı. Proje 71/71 yeşil.

## 3. Auth yerel hızlı komut (`cf98b4803`)

- 54 sınıf `[Trait("Category","Mongo")]` aldı. Ölçüm Platform'daki yöntemle yapıldı: bütün Mongo yolları reddedildi ve
  yeni mongod süreçleri ile 27017 bağlantıları izlendi.
- Hızlı koşu: **1.950/1.950 yeşil**, 0 mongod, 0 bağlantı.
  - `--no-build` ile 14 sn; artımlı derlemeyle 19 sn.
  - CI'da filtresiz koşu: 2.929 test, 11 dk 21 sn.
- Etiketin gerçekten iş gördüğünün kanıtı: bir sınıfın etiketi kaldırılınca hızlı koşuda o sınıf 54/54 kırmızı.

## 4. MDM (`93b9f6137`, `2432630f6`)

- **`FirstGskuConcurrencyMongoTests`: üretim hatası.**
  - Allocator belgesi henüz yokken eşzamanlı ilk komutlar upsert yarışına giriyor. Kaybeden komut duplicate key alıyor
    ve kendi tahsisi yazılmıyor. Kod bunu çakışma sanıp 409 dönüyordu.
  - Düzeltme: güncelleme bir kez yeniden deneniyor.
  - Yeni deterministik test (failCommand fail point): eski kodda her koşuda kırmızı, düzeltmede yeşil. Sınıf 10/10.
- **`MdmOwnedMongoGuardTests.BL595`:** `Process.Start`, çocuk süreç `sleep`'i exec etmeden önce dönüyor. Test artık
  çocuk `sleep` olana kadar bekliyor.
- **`GskuBoundedClaimTests`:** yarım-kira bütçesi paralel yük altında ölçülüyordu (CI'da 932 ms). Sınıf artık
  tek başına koşuyor.
- Assert'ler değişmedi. Sabotajlar:

  | Test | Sabotaj | Önce / sonra |
  |---|---|---|
  | BL595 | yol kontrolü kapatıldı | 1 / 1 kırmızı |
  | GskuBoundedClaim | sınır tam kiraya çekildi | 3 / 3 kırmızı |

## 5. Auth üretim düzeltmesi: el sıkışması süren bağlantı (`46d6def97`)

**Değişiklik:**
- `ObserveQuiescenceAsync` istemci bilgisi olmayan bir bağlantı görürse en çok 10 × 25 ms bekleyip yeniden okuyor.
- Adını hâlâ söylemeyen bağlantı gözlemde kalıyor, yani ret fail-closed.
- Testteki `WaitForSettledConnectionsAsync` bekleyişi kaldırıldı; düzeltme artık üretimde.

**Yeni test:** `Commit_WhenAConnectionStaysUnnamedAfterThePlan_IsStillRefusedAsQuiescenceDrift`. Ham bir TCP bağlantısı
hiç el sıkışmıyor; commit `QUIESCENCE_DRIFT` ile reddediliyor ve hiçbir şey yazılmıyor.

| Ölçüm | Sonuç |
|---|---|
| Eski üretim kodu, test bekleyişi yok · drift teorisi × 40 | **2 kırmızı** (hata üretildi) |
| Düzeltilmiş üretim kodu, test bekleyişi yok · drift teorisi × 60 | **0 kırmızı** |
| Sınıfın tamamı | 57/57 yeşil, 25 sn |
| S5 sabotajı: adsız bağlantılar yok sayıldı (`\|\| IsMidHandshake(row)`) | yeni test kırmızı (fail-closed korunuyor) |

- Dosya her adımdan sonra düzeltilmiş kopyayla bayt bayt karşılaştırıldı (`cmp`): aynı.
- Bu sınıfın yerel süresi 26 sn'den 25 sn'ye indi; üstüne 1 yeni test eklendi.

## 6. Önce / sonra özeti (TEST-HIZ-01 bütünü)

| Ölçü | Önce | Sonra | Kaynak |
|---|---:|---:|---|
| CI platform-app "Test" adımı | 9 dk 58 sn | 7 dk 21 sn | #219 CI |
| Ortak Mongo'ya geçen 16 Platform sınıfı (CI, TRX) | 369 sn | 67 sn | parti 1–2 kayıtları |
| Platform yerel hızlı koşu | tam proje gerekiyordu | 6.898 test 30 sn (derlemeyle 49 sn) | `local-fast-tests.md` |
| Auth yerel hızlı koşu | tam proje gerekiyordu (CI 2.929 test, 11 dk 21 sn) | 1.950 test 14 sn (derlemeyle 19 sn) | §3 |
| auth-app sıralı koleksiyon (yerel, 38 sınıf, 856 test) | 13 dk 55 sn | 6 dk 13 sn | `…auth-app.md` |

- auth-app'in CI'daki yeni süresi henüz ölçülmedi; dal push edilmedi.
- Aralıklı kırmızı veren 8 test sınıfından 7'sinin kök nedeni bulundu ve düzeltildi: AnonymousDoorLimit, QUIESCENCE_DRIFT,
  SevenState, WireLevel, FirstGskuConcurrency, BL595 ve GskuBoundedClaim. İkisi üretim hatasıydı (MDM upsert yarışı,
  Auth el sıkışma reddi). `ExitCodeAlignmentTests` tekrar üretilemedi; ona tanı satırı eklendi.

## 7. Açık kalan

- **auth-app'in en uzun yolu:** `ExportGrantBackfillScenarioTests` (yerelde 237 sn). Bölünmesi ayrı onay ister.
- **auth-app'in CI süresi:** push ve PR'dan sonra ölçülecek.
