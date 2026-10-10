# MVP6-G5-LOGISTICS-GOLDEN-FLOW-01

Lojistik golden flow ilk kez uçtan uca koşturuldu; MOD-0183'ün kabul listesinden altı
madde kanıtlandı, iki bulgu çıktı ve ikisi de sahip kararıyla kapatıldı.

- tarih: 2026-10-10 / 2026-10-11
- dal: `feature/sce/mvp6-g5-logistics-golden-flow`, `origin/main` `a814bad0f`'ten
- yığın: Auth 5056 · Platform 5057 · MDM 5059 · SupplyChain 5066 · Gateway 5000, dört lane mongod
- araç: `scripts/g5/g5_golden_flow.py` (bu turda yazıldı, tekrar koşulabilir)

Ham log, JSON dökümü ve ekran görüntüsü **bu kayda konmadı** — kural gereği yalnız bu özet.

## Kabul listesi

| madde | sonuç |
|---|---|
| `:425` bir create = bir scoped kayıt + bir lifecycle/outbox girdisi | **✓** beş koleksiyonun her birinde 1 |
| `:426` aynı idempotency anahtarı ikinci yazma üretmez | **✓** 201 / 200, aynı id, `idempotentReplay=true`, veritabanında 1 kayıt |
| `:427` geçiş matrisi; geçersizler kısmi durum bırakmaz | **✓** 200/422/200/422/200 · 4 geçiş = 4 lifecycle girdisi, reddedilenler hiçbir şey yazmadı |
| `:428` POD atomik, olaylar bir kez | **✓** `ShipmentDelivered` ve `PodCaptured` ayrı olaylar, her biri bir kez; altı durum değişikliği = altı outbox olayı |
| `:429` korelasyon zincir boyunca korunur | **✓** tek korelasyon, beş modülde **36 belge** |
| `:433` yükler dondurulmuş OpenAPI ile eşleşir | **✓** 19 yanıt, **0 şema ihlali** (hata yolları dahil) |
| `:434` zincir shipment→carrier/load→POD→return/claim | **✓** 10 adım, hepsi geçti |

Ölçülemeyen: `:432` depo alımı — üreticisi MOD-0178 yok. Kalan: `:424`, `:430`, `:431`
(`:431`'in statik yarısı ölçüldü: yerel stok/bakiye sınıfı ve koleksiyonu **yok**).

## Bulgu 1 — taşıyıcı bağı (D187-01 ile kapatıldı)

Hiçbir kod `Shipment.CarrierId`/`LoadId` alanlarına yazmıyor; `MOD-0183:95` *"no new
assignment API"* ve `:499` atama yüzeyini tutuyor. `ClaimLifecycle` ise talebin
taşıyıcısını bu boş alanla karşılaştırıyordu, dolayısıyla **sevkiyatı gerçekten taşıyan
taşıyıcıyı adlandıran hasar talebi imkânsızdı** — ölçüldü: taşıyıcıyla 422, taşıyıcısız 201.

Sahip kararı (2026-10-11) D187-01'i kapattı: **yok olan sevkiyat taşıyıcısı uyuşmazlık
değildir.** Talebin taşıyıcısı yine güvene alınmıyor — `ClaimReferenceReader` onu kapsamdaki
taşıyıcı listesinde arıyor ve yoksa 404 veriyor. **Mevcut ve farklı** bir sevkiyat taşıyıcısı
hâlâ 422; böylece atama yüzeyi geldiği gün koruma anlamını koruyor.

Sonuç ölçüldü: aynı istek 422'den **201**'e döndü, zincir 10/10 oldu.

## Bulgu 2 — ilan edilmemiş durum kodu (sözleşme 3.2.1)

`GET /shipments` ve `POST /shipments`, yabancı legal entity'de **404** dönüyordu ama
sözleşme bunu **1.0.0'dan beri hiç** ilan etmemişti. R-2'nin getirdiği bir şey değil:
R-2 öncesi middleware de kiracı uyuşmazlığında 404 veriyordu (`f57f1c6b7`,
`ShipmentContextMiddleware:29`); R-2 yalnız tetikleyiciyi genişletti.

`SHIPMENT-BUNDLE` **3.2.1**: iki işleme, diğer on dördünün zaten kullandığı
`$ref: '#/components/responses/NotFound'` eklendi. Tamamen eklemeli; hiçbir tüketici
etkilenmiyor. `GET /claims` kasten dokunulmadı — o işlem yabancı LE'de **403** dönüyor ve
403 zaten ilan edilmiş (R-2'nin Claims için seçtiği kural).

## Tekrar keşfedilmemesi gereken çalıştırma bilgileri

1. **Servis sırası:** Platform **önce**, SupplyChain **sonra**. Tersi olursa beş modül
   manifesti kaydolmaz, izinler Auth'a senkronlanmaz ve her çağrı 403 döner.
2. **Auth'un login'i Platform'a bağımlı** — `/api/internal/tenants/{id}/login-settings`.
   Platform yokken login 401 "Login is temporarily unavailable".
3. **Sevkiyat yaşam döngüsü tek korelasyon köküne bağlı**; farklı korelasyonla gönderilen
   geçiş 400 alır. Kabul listesinde yazmıyor, davranış kasıtlı.
4. **Yük yalnız sevkiyat `Draft` ya da `Planned` iken** bağlanabilir
   (`LoadReferenceReader.cs:69`), yani sıra shipment → load → dispatch → POD.
5. **Korelasyon alan adı modüle göre değişir:** Shipments `CorrelationId`,
   Loads/Returns/Claims `CorrelationRoot`, outbox zarflarında `correlationId`.
6. `SHIPMENT_ALREADY_ASSIGNED` guard'ı **Loads'un kendi `assignments` koleksiyonundan**
   çalışıyor (`LoadRepository.cs:54`), sevkiyat üzerinden okuyan kopyası ise ölü.

## Açık kalan

- Atama yüzeyi (`MOD-0183:499`) hâlâ tutuluyor; `Shipment.carrierId` doldurulmuyor ve
  `MOD-0187:621`'in taşıyıcı kutucuğu bu yüzden etkinleşmiyor. D187-01 bunu çözmüyor,
  yalnız hasar talebini açıyor.
- `:432` depo alımı MOD-0178 olmadan ölçülemez.
