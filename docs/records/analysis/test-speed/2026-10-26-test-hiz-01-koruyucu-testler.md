# TEST-HIZ-01 · bağımsız incelemenin iki test boşluğu ve Platform yarışı (2026-10-26)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, CT-Setup üzerinden (2026-10-26):
> - önce `TenantModulesScreenHttpMongoTests` kırmızısının kök nedeni;
> - sonra bağımsız incelemenin #225 için bulduğu iki 🟡 test boşluğu.
>
> Yerel koşular yük kuralıyla yapıldı (1 dk yükü 12'nin üstündeyse bekle; tek proje, `--filter`).

## 1. Auth: bekleme döngüsünü doğrudan koruyan test

**Boşluk:** `ObserveQuiescenceAsync`'in el sıkışma bekleme döngüsü kapatılınca sınıf 57/57 yeşil kalıyordu.

**Test:** `Commit_WhenAConnectionNamesItselfAfterTheFirstObservation_WaitsForItAndCommits`
- Plandan sonra ham bir TCP bağlantısı açılıyor.
- Commit'in ilk `$currentOp` okuması döndükten hemen sonra (store istemcisinin komut olayında) bu bağlantı işletimsel uygulama adıyla hello gönderiyor.
- Yani ilk gözlem bağlantıyı adsız görüyor. Döngü varsa sonraki okuma bağlantıyı adlı görür ve commit başarılı olur; döngü yoksa ilk gözlemde `QUIESCENCE_DRIFT`.
- Sıra komut olayıyla kuruluyor, zamanlayıcıya bağlı değil.

| Koşu | Yük | Sonuç |
|---|---:|---|
| Gerçek kod, sınıf | 4,47 | 58/58 yeşil |
| S0: döngü kapalı (`settle < 0`) | 8,12 | 1 kırmızı: yalnız yeni test |
| Gerçek kod, sınıf, sonra | 10,93 | 58/58 yeşil |
| Yalnız yeni test × 10 | 7,8–11,7 | 10/10 yeşil (TRX: 166–320 ms) |

Üretim dosyası sabotajdan sonra bayt bayt geri yüklendi (`cmp`).

## 2. MDM: yeniden denemede aynı komut koruması

**Boşluk:** `AllocateForFirstGskuAsync`'in tek yeniden denemesinin güvenliği filtredeki
`Allocations.CreationCommandId ≠ komut` koşuluna dayanıyor. Bu koşulu yalnız yeniden denemede düşüren sabotajı mevcut
testler yakalamıyordu.

**Test:** `A_command_sent_again_after_a_lost_upsert_keeps_its_one_allocation` (depo düzeyinde, failCommand fail point ile).
- Tahsisi zaten yazılmış bir komut yeniden gönderiliyor. İlk findAndModify duplicate key alıyor ve kod yeniden deniyor.
  Komut aynı revizyonu, GSKU'yu ve ordinal'i almalı.
- Aynı komut iki kez eşzamanlı gönderiliyor; ilk findAndModify yine duplicate key alıyor. İki çağrı aynı cevabı almalı.
- Allocator belgesinde iki komutun her biri için tek tahsis olmalı, `LastOrdinal` = 2.

| Koşu | Yük | Sonuç |
|---|---:|---|
| Gerçek kod, sınıf | 6,13 | 3/3 yeşil |
| S2: yeniden deneme koşulsuz `_id` filtresiyle | 6,92 | 1 kırmızı: yalnız yeni test |
| Gerçek kod, sınıf, sonra | 9,10 | 3/3 yeşil |

## 3. Platform: `TenantModulesScreenHttpMongoTests` kırmızısı (ürün kodu değiştirilmedi)

**Hata:** `Two_screens_suspending_the_same_plan_module_at_once…` satır 754. Bu assert iki eşzamanlı askıya almanın
yalnız birinin 204 almasını bekliyor; ölçülen değer 2.

**CI geçmişi:**
- 10-14…10-26 arasında 142 kırmızı platform-app işi tarandı.
- #216'dan (2026-10-23 13:02) önce bu test 0 kez kırmızı.
- Sonra 2 kez kırmızı: main run 40299055331 ve PVG #226 run 40312447713. İkisi de satır 754.

**Paylaşılan host:**
- `ResetAsync` her koleksiyonu `DeleteMany` ile boşaltıyor; indeksler korunuyor.
- Host'ta arka plan servisi yok.
- Mock'lar yalnız kurulumda ayarlanıyor.

**Yerel:** sınıf 20/20 yeşil (45 test, yük 3–8).

**Kök neden: ürün kodunda TOCTOU.** Deterministik yoklamayla ölçüldü (commit edilmedi, geri yüklendi):
- B isteğinin kapı okuması (`RefuseProjectionDisableAsync` → `GetByTenantAndModuleAsync`) döndükten sonra A isteği baştan sona koşturuldu.
- Sonuç: `first=204 second=204 rows=1 audit=2`.
- Mekanizma:
  - B'nin `SuspendPlanLineAsync` içindeki override araması A'nın yeni override'ını buluyor.
  - Süresi dolmuş override'ı devralmak için yazılmış `SuspendThroughExistingOverrideAsync`, override'ın süresinin dolup dolmadığını kontrol etmiyor ve okuduğu sürümle güncelliyor.
  - Sonuç olarak 204 dönüyor; ayrıca çift denetim kaydı, çift olay ve çift sürüm artışı yazılıyor.

**Neden #216'dan sonra:** ölçülmedi. Tahmin: tek sıcak host'ta iki istek gerçekten paralel koşuyor; önceden her test soğuk
bir host'la başlıyordu.

**Açık karar:** Platform ürün düzeltmesi XMC-001 onayı istiyor. Öneri:
- devralma yalnız süresi dolmuş override için yapılsın, aksi halde `ACTION_NOT_OFFERED` dönsün;
- yoklama deterministik test olarak eklensin;
- test gevşetilmesin.
