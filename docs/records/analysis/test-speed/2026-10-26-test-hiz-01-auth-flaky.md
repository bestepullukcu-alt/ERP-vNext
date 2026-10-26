# TEST-HIZ-01 · auth-app'in iki aralıklı testi: kök neden ve düzeltme (2026-10-26)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, bu oturumda: iki aralıklı Auth testinin kök nedenini bul ve düzelt; MDM'ye dokunma; iş aynı dalda
> birikir, push öncesi sorulur.
> **Bağlam:** #219 merge edildikten sonra main'deki iki service-tests çalışması (40109872818, 40109966643) yalnız
> auth-app'teki bu iki test yüzünden kırmızıydı.

## 1. `AnonymousDoorLimitTests.The_wrong_attempt_refusal_says_the_time_left_and_lets_the_client_in_when_the_window_ends`

**Hata (CI 40109966643):** `Assert.InRange` başarısız; sınır 1–800 ms, ölçülen değer **800,0698 ms**.

**Kök neden:**
- `PasswordDoorRateLimiter` pencereyi duvar saatiyle ölçüyor (`DateTime.UtcNow`; pencere ilk yanlış denemede açılıyor).
- Test ise monoton zamanlayıcıyla bekliyor (`Task.Delay(1200)`).
- İki saat arasındaki fark CI'da 0,07 ms çıktı: sınırlayıcının saatine göre 1.199,93 ms geçmişti. Test sınırı tam
  800 ms olduğu için bu kadarı kırmızıya yetti.
- Üretim davranışında hata yok; test iki farklı saati aynı saat sayıyordu.

**Düzeltme (yalnız test):** Son yanlış denemeden sonra, 1.200 ms dolana kadar sınırlayıcının kendi saatine
(`DateTime.UtcNow`) göre de bekleniyor. Pencere ilk denemede açıldığı için bu süre pencerenin başından da en az 1.200 ms.
**Assert değişmedi** (1–800 ms).

**Kanıt:**
- **D1 sabotajı** (`PasswordDoorRateLimiter.FailuresLeftFor` kalan süre yerine tam pencereyi döndürüyor): önce 1 kırmızı,
  sonra 1 kırmızı; ikisinde de aynı test.
- Düzeltilmiş test arka arkaya 30 kez koşuldu: 30/30 yeşil. Hata yerelde tekrar üretilemedi; dayanak CI'daki ölçüm.

## 2. `ProductIdentityEntitlementReconciliationMongoTests.Commit_WhenWriterAppearsAfterSnapshot_CannotFinalizeSuccess`

**Hata (CI 40109872818, ve 2026-10-21…24 arasında farklı satırlarda):** `InvalidOperationException: QUIESCENCE_DRIFT`.

**Mekanizma:** `EntitlementReconciliationOperationStore.ObserveQuiescenceAsync`, sunucuda işletimsel istemcininki dışındaki
bütün bağlantıların (boştakiler dahil, `$currentOp`) bir parmak izini çıkarıyor. Commit iki yerde (işlemden önce ve işlemin
içinde) bu parmak izini planınkiyle karşılaştırıyor.

**Ölçüm (kök neden):**
- Geçici bir kayıt satırıyla (commit edilmedi, sonra bayt bayt geri yüklendi) her gözlem bir dosyaya yazıldı.
- Teori kırmızı çıkana kadar koşuldu: 20. denemede `token-after-revoke` satırı düştü.
- O anda gözlem listesine **istemci bilgisi olmayan** tek bir bağlantı girdi (`conn36`) ve 51 ms sonra çıktı. 20 denemenin
  bütün gözlemlerinde bu biçimde yalnız bu bir satır vardı.
- Bir bağlantının istemci bilgisi el sıkışma (handshake) tamamlanınca belli oluyor. O ana kadar bağlantı adsız ve üretim
  kodu onu yabancı istemci sayıyor. Süreçteki bütün istemciler işletimsel uygulama adını taşıdığı için el sıkışma
  bitince satır yine filtreleniyor.
- `SeedPlanAsync`, planı okumadan hemen önce yeni bir `MongoClient` oluşturuyor (`mongo.NewClient(probe)`). Sürücü bu
  istemcinin izleme bağlantılarını arka planda açıyor; el sıkışmaları plan ile commit arasına denk gelebiliyor.

**Düzeltme (yalnız test):** `WaitForSettledConnectionsAsync`. Store'un istemcisi oluşturulduktan sonra ve plan okunmadan
önce, sunucuda el sıkışması süren bağlantı kalmayana kadar, 300 ms boyunca kararlı biçimde bekleniyor (en çok 10 sn,
sonra açık bir hata). Store'un reddi ve **hiçbir assert değişmedi**.

**Kanıt:**
- **Önce:** teori 20 denemede 1 kez kırmızı.
- **Sonra:** 60 denemede 0 kırmızı. Düzeltme olmasaydı 60'ının birden yeşil çıkma olasılığı yaklaşık %5.
- Sınıfın tamamı (54 test) yeşil.
- **Maliyet:** sınıf yerelde 14 sn'den 26 sn'ye çıktı (her senaryo yaklaşık 300 ms bekliyor).

## 3. Yeşil mutant: commit'teki drift reddini hiçbir test korumuyor

| # | Sabotaj (`EntitlementReconciliationOperationStore.cs`) | Bu sınıfta kırmızı (önce / sonra) |
|---|---|---:|
| Q1 | işlemden önceki `if (quiet != plan.QuiescenceFingerprint) throw …QUIESCENCE_DRIFT` kapatıldı | 0 / 0 |
| Q2 | işlemin içindeki `if (await ObserveQuiescenceAsync(ct) != quiet) throw …QUIESCENCE_DRIFT` kapatıldı | 0 / 0 |
| Q1+Q2 | ikisi birden (yalnız önce koşuldu) | 0 |

- Aralıklı kırmızıya sebep olan kontrolü, bu sınıfta "gerekli" diye koruyan bir test yok.
- `Read_WhenUnattributedIdleClientExists_RecordsResidualFingerprintWithoutInventingAuthAttribution` yalnız parmak izinin
  okumada değiştiğine bakıyor; commit'in reddettiğine bakmıyor.
- Başka bir Auth sınıfının bunu yakalayıp yakalamadığı ölçülmedi.
- **Öneri (ayrı karar):** yabancı bir istemci plan ile commit arasında görünürse commit'in `QUIESCENCE_DRIFT` ile reddettiğini
  ölçen bir test.

## 4. Üretim tarafı için not (değiştirilmedi)

Aynı yarış üretimde de yaşanabilir: işletimsel araç da kendi istemcisini oluşturup hemen planı okuyor. Sonuç güvenli yönde
(commit reddedilir, yeniden denenebilir), ama gereksiz bir ret. Çözüm seçenekleri (el sıkışması süren bağlantıyı yok saymak
ya da araçta "yerleşmeyi beklemek") Auth üretim kodu değişikliği ister ve sahibin ayrı onayına bağlı.
