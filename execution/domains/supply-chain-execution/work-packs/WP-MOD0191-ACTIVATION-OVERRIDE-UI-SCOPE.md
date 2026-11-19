Dispatch edilebilir WP değildir; MOD-0191 pack revizyon önerisidir.

# MOD-0191 — etkinleştirme, override ve ekran kapsam önerisi

**Durum:** İnceleme taslağı. MOD-0191 Module Pack §18/§20 kapsamında bu üç iş bugün `ready-for-dev` değildir. Bu belge kod izni, AC kabulü veya mevcut pack sürümünü değiştirmez. CT-APR-01/02 gereği ayrı pack revizyonu, bağımsız inceleme ve Ali onayı gerekir. Ürün kararları henüz kaynak karara bağlanmadığı için aşağıdaki davranışlar öneridir.

## Ölçülen taban (origin/main `9166569ba`, 11 Kasım 2026)

- Kalıcı manuel miktar akışı `Draft → InReview → Approved` durumlarını ve tarih aralığını tutuyor. `ReadEffectiveAsync` bugün `Approved` ve `asOf` aralığına göre okuyor; ayrı `Active` durumu veya etkinleştirme komutu yok. Bu yüzden yeni ürün kararı mevcut okuma semantiğini değiştirecektir.
- Politika revision onayı ve miktar onayı ayrıdır. Onaylı miktar değiştirilemez; yeni miktar oluşturma, gönderme ve onaylama komutları ile işlem-içi audit mevcut. Ayrı override nesnesi ve tenant ekranı yok.
- Geçici şirket sınırı MDM'de aynı tenant'a ait etkin LegalEntity doğrulamasıdır. Kullanıcı-şirket yetkili kümesi merkezi Platform işi olarak açıktır.

## Ali onayına sunulan öneriler

1. **Öneri — etkinleştirmeyi onaydan ayırma:** `Approved` miktarın MRP tarafından okunması için ayrıca `Activate` geçişi değerlendirilsin. Bugünkü `ReadEffectiveAsync` yalnız `Approved` ve tarih aralığına bakar; ayrı `Active` durumu yoktur. Geçiş ve eski kayıtların okunması Ali kararı bekler.
2. **Öneri — gerekçeli override için yeni kayıt:** Önceki onaylı miktarı yerinde değiştirmeden yeni aday ve bağımsız inceleme değerlendirilsin. Override nesnesi, görev ayrılığı ve etkinleştirme ilişkisi Ali kararı bekler.

Main'de bu iki öneriyi onaylayan MOD-0191 karar kaydı yoktur; pack §8/§17 etkinleştirme ve override'ı kararsız bırakır. Ali teyit ederse karar kaydı merkez CT tarafından açılacak; bu belge karar yerine geçmez.

## İncelemeye sunulan dar dilim

| İş | Önerilen davranış | İzin önerisi | Zorunlu kanıt |
|---|---|---|---|
| Miktarı etkinleştir | Öneri: `Approved → Active`. Çakışan etkin tarih aralığında **A:** bugünkü gibi reddet; **B:** eski Active kaydın ileri aralığını sürümlü kapat. A/B ve geçmiş `Approved` kayıtların okuma geçişi Ali kararı bekler; mevcut taban örtüşen aralığı reddeder (pack §3, `ManualQuantityMongoStore.cs:112`). | Yeni anahtar adayı aşağıdaki tabloda; onay bekliyor. | Etkinleştirenin hazırlayan, önemli editör ve onaylayandan ayrı olması **öneri**, kesin görev ayrılığı Ali kararı bekler. Beklenen sürüm, MDM kapsamı, işlem + audit, yarış ve tekrar isteği testleri. |
| Override taslağı | Öneri: kaynak miktar kimliği, eski/yeni miktar ve temel birim, zorunlu gerekçe ile yeni Draft; kaynak değişmez. | Yeni anahtar adayı aşağıdaki tabloda; onay bekliyor. | Kaynak ve yeni kayıt bağı; hazırlayan/önemli editörden bağımsız onay, audit arızasında sıfır etki. |
| Override incelemesi | Öneri: yeni kayıtta mevcut `safety-stock.policies.submit` ve `safety-stock.policies.review` izinlerini kullan; onay yeni kaydı yalnız `Approved` yapar. | Mevcut anahtarlar; yeni `quantities` segmenti ayrı seçenek ve pack kararı gerektirir. | Önceki karar izi, hazırlayan/önemli editörden ayrı inceleyici; etkinleştirme ayrıca kararlaştırılır. |
| Tenant ekranı | Şirket, SKU, depo ve tarih filtresi; policy/revision, onaylı/etkin miktar, geçerlilik, kaynak, gerekçe ve audit izi; taslak/inceleme/etkinleştirme eylemleri yalnız izinli kullanıcıya. | Okuma için mevcut `safety-stock.policies.read`; eylem anahtarları karar bekler. | 7 dil, UAS-001, mobil ve RTL, gerçek veri; Gateway 5000 → SafetyStockService 5069 (§3 port kaydı bekliyor), rota sahibi integration-agent. |

### İzin anahtarı karşılaştırması — karar bekliyor

| İş | Bugün kod/manifest | Pack §3 ve §14 adayı | Bu öneride açık seçenek |
|---|---|---|---|
| Manuel miktar oluştur | `safety-stock.policies.create` | §3 mevcut; §14 `safety-stock.policy.create` adayı | Mevcut çoğul anahtar korunur; yeni `safety-stock.quantities.create` ancak ayrı pack/izin kararıyla. |
| Policy revision güncelle | `safety-stock.policies.update` | §3 mevcut; §14 `safety-stock.policy.update` adayı | Mevcut çoğul anahtar korunur; bu öneri revision yazma iznini değiştirmez. |
| İncelemeye gönder | `safety-stock.policies.submit` | §3 mevcut; §14 `safety-stock.policy.submit` adayı | Mevcut çoğul anahtar kullanımı önerilir; `safety-stock.quantities.submit` §3 ile çelişir ve onay bekler. |
| İncele/onayla | `safety-stock.policies.review` | §3 mevcut; §14 `safety-stock.policy.approve` adayı | Mevcut çoğul anahtar kullanımı önerilir; `safety-stock.quantities.review` §3 ile çelişir ve onay bekler. |
| Miktarı oku | `safety-stock.policies.read` | §3 mevcut; §14 `safety-stock.policy.read` adayı | Ekranda mevcut anahtar; yeni `quantities.read` açılmadı. |
| Etkinleştir | Yok | §14 `safety-stock.policy.activate` adayı | `safety-stock.quantities.activate` ayrı kaynak/izin modeli önerisidir; ikisinden biri Ali/CT ile seçilir. |
| Override oluştur | Yok | §14 `safety-stock.override.create` adayı | `safety-stock.quantities.override` ayrı seçenektir; §4 `SafetyStockOverride / ApprovalDecision` ad alanı ve PKS-001 Tier-3 `override` eylem kaydı kararı olmadan izin açılmaz. |

Pack §14'teki `safety-stock.evaluation.read/run` adayları bu dilimde evaluation yazımı veya yeni ekran yetkisi açmaz.

## Uygulama öncesi karar ve sahiplik kapıları

| Açık karar / iş | Sahip | Hedef | Kabul sınırı |
|---|---|---|---|
| Ayrı etkinleştirme, eski `Approved` kayıtların geçişi ve geçmişe dönük MRP okuması; çakışan aralıkta A reddet / B eski Active ileri aralığını sürümlü kapat; geri alma kuralı | Ali (ürün) | TBD — ürün teyidi ve eski veri geçişi henüz kararlaştırılmadı | Mevcut MRP `Approved` miktarı okur; geçişte sessiz sıfıra düşme veya yanlış miktar yasak. Karar gelmeden komut açılmaz. |
| Override nesnesi, gerekçe, hazırlayan/önemli editör/onaylayandan ayrı etkinleştirici, izin adları; `safety-stock.quantities.*` seçeneği ve PKS-001 Tier-3 `override` kaydı | Ali (ürün ve izin kararı) | TBD — yeni iş/izin modeli kararı yok | Pack §3/§14 ve §4 ad alanı uzlaştırılmadan yeni anahtar ya da komut açılmaz. |
| MOD-0191 pack revizyonu, CT-APR-01/02 bağımsız incelemesi ve sabotaj/kanıt planı | Beste (MVP-4 sahibi) | TBD — Ali ürün kararı ve bağımsız inceleme bekleniyor | Bu öneri `ready-for-dev` yetkisi vermez; pack onayı ayrı kaydedilir. |
| MRP tüketicisiyle `Approved` → `Active` geçiş ve haftalık `asOf` uyumu | Beste (MVP-4 sahibi; MOD-0189 koordinasyonu) | TBD — etkinlik semantiği onaylanmadı | İki taraflı sözleşme ve regresyon kanıtı olmadan tüketici davranışı değişmez. |
| Gateway 5000 → SafetyStockService 5069 (§3 port kaydı bekliyor) rota eşlemesi | integration-agent | TBD — merkezî port kaydı ve rota görevi bekleniyor | Tarayıcı 5069'a doğrudan çağrı yapmaz; `ocelot.json` bu önerinin kapsamı dışındadır. |
| Tenant ekranı, menü, yedi dil ve MDM aktif şirket seçimi | Beste (MVP-4 sahibi; Web koordinasyonu) | TBD — ürün yüzeyi ve Gateway kararı bekleniyor | UAS-001 gereği izinsiz kullanıcıya iskelet yok; kullanıcı-şirket yetkili kümesi merkezi iş olarak görünür kalır. |
| BL-501 SafetyStock bağlantısı ve audit teslim kanıtı | Beste (MVP-4 sahibi; merkez audit ekibiyle koordinasyon) | TBD — servis bağlantısı ve kabul tarihi belirlenmedi | BL-501 main'dedir, SafetyStock yazma yoluna bağlandığı kanıtlanmamıştır. Mevcut işlem-içi geçici `c` izi korunur; yeni audit koleksiyonu veya merkezi teslim varmış iddiası yok. |

Kapsam ve izolasyon için pack §8 ve AC-S02; ekranın yedi dil, UAS-001 ve yalnız Gateway davranışı için AC-S09 esas alınır. Görev ayrılığı, gerekçe ve audit ayrıca pack §12/§17 karar ve test kapılarında ele alınır. Şirket kimliği MDM `lookup-validation` ile fail-closed doğrulanır; bu kullanıcı-şirket yetkisi değildir. Yeni ekran ve komutlar için ilgili açık güvenlik borcu kapatılmadan canlı AC kabulü ilan edilmez.

## Doğrulama planı

Pack revizyonu onaylanırsa gerçek geçici Mongo'da durum geçişi, idempotency, çakışan etkin aralık için seçilen A/B kuralı, hazırlayan/önemli editör/onaylayandan ayrı aktör, MDM/izin/kapsam kaybı, audit/transaction geri alma; MRP haftalık `asOf` tüketici uyumu ölçülür. Ekranda yedi dil, UAS-001, şirket filtresi ve `ar` RTL; kritik koruma kasıtlı bozulunca kırmızı, geri alınınca yeşil test istenir. Pack AC-S02/AC-S09 ve §8 kiracı kurallarıyla karşılaştırılır; bu belge testin çalıştırıldığı veya AC'nin kapandığı iddiası değildir.
