# MOD-0191 — etkinleştirme, override ve ekran kapsam önerisi

**Durum:** İnceleme taslağı. MOD-0191 Module Pack §18/§20 kapsamında bu üç iş bugün `ready-for-dev` değildir. Bu belge kod izni, AC kabulü veya mevcut pack sürümünü değiştirmez. CT-APR-01/02 gereği ayrı pack revizyonu, bağımsız inceleme ve Ali onayı gerekir.

## Ölçülen taban (origin/main `9166569ba`, 11 Kasım 2026)

- Kalıcı manuel miktar akışı `Draft → InReview → Approved` durumlarını ve tarih aralığını tutuyor. `ReadEffectiveAsync` bugün `Approved` ve `asOf` aralığına göre okuyor; ayrı `Active` durumu veya etkinleştirme komutu yok. Bu yüzden yeni ürün kararı mevcut okuma semantiğini değiştirecektir.
- Politika revision onayı ve miktar onayı ayrıdır. Onaylı miktar değiştirilemez; yeni miktar oluşturma, gönderme ve onaylama komutları ile işlem-içi audit mevcut. Ayrı override nesnesi ve tenant ekranı yok.
- Geçici şirket sınırı MDM'de aynı tenant'a ait etkin LegalEntity doğrulamasıdır. Kullanıcı-şirket yetkili kümesi merkezi Platform işi olarak açıktır.

## Ali kararları (11 Kasım 2026)

1. **Etkinleştirme onaydan ayrıdır.** Bağımsız onay tek başına MRP'nin okuyacağı miktarı yürürlüğe koymaz. Açık `Activate` geçişi ve ayrı aktör gerekir.
2. **Override yeni değişmez kayıttır.** Önceki onaylı miktar yerinde değişmez. Gerekçeli yeni aday ayrı inceleme ve bağımsız onaydan geçer; etkinleştirme yine ayrı adımdır.

## İncelemeye sunulan dar dilim

| İş | Önerilen davranış | İzin önerisi | Zorunlu kanıt |
|---|---|---|---|
| Miktarı etkinleştir | `Approved → Active`; aynı Tenant × LegalEntity × SKU × Warehouse ve çakışan tarih aralığında tek yürürlükte miktar. Önceki Active kaydı yeni aralığa göre sürümlü olarak kapanır, silinmez. | `safety-stock.quantities.activate` | Ayrı aktör, beklenen sürüm, MDM kapsamı, transaction + audit, yarış ve tekrar isteği testleri. |
| Override taslağı | Kaynak miktar kimliği, eski/yeni miktar ve temel birim, zorunlu gerekçe ile yeni Draft; kaynak değişmez. | `safety-stock.quantities.override` | Kaynak ve yeni kayıt bağı, özgün hazırlayan ve editörlerin bağımsız onaydan dışlanması, audit arızasında sıfır etki. |
| Override incelemesi | Mevcut submit/review izinlerini yeni kayıtta yeniden kullanma **önerisi**; onay yeni kaydı yalnız `Approved` yapar. | `safety-stock.quantities.submit` / `.review` — **karar bekliyor** | Önceki karar izi ve ayrı etkinleştirme; kendi değişikliğini onaylama yasağı. |
| Tenant ekranı | Şirket, SKU, depo ve tarih filtresi; policy/revision, onaylı/etkin miktar, geçerlilik, kaynak, gerekçe ve audit izi; taslak/inceleme/etkinleştirme eylemleri yalnız izinli kullanıcıya. | Okuma ve eylem izinleri yukarıdaki komutlarla hizalanır; **karar bekliyor**. | 7 dil, UAS-001, mobil ve RTL, gerçek veri, Gateway üzerinden erişim. |

## Uygulama öncesi karar ve sahiplik kapıları

- Ali/CT: önerilen yeni izin anahtarları, eski `Approved` kayıtların yeni etkinlik kuralına geçişi ve geçmişe dönük okuma davranışı. Mevcut MRP tüketicisi `Approved` miktarı okuyabiliyor; geçişte sessizce sıfıra düşmesi veya yanlış miktarı tüketmesi yasak. Geçiş planı tüketiciyle birlikte onaylanmalı.
- Ali/CT: tarih aralığı çakışmasında eski Active kaydın ileri döneminin kesilip kesilmeyeceği ve etkinleştirmenin geri alınması/geri dönüş kuralı. Kararsız durumda kod açılmaz.
- Integration/Web sahipleri: 5069 Gateway rotası, tenant menü ve sayfa sınırı; ekranda izinler ve MDM şirket listesi. Üretim kullanıcı-şirket yetkisi merkezi ekip borcu olarak görünür kalır.
- Audit: merkezi BL-501 ana dalda olduğundan yeni komutun merkezi iletim yolu ve iş/audit başarısızlığında sıfır etki, mevcut c-yolu kayıtları korunarak kanıtlanmalı. Gelişigüzel yeni yerel audit koleksiyonu kurulmaz.
- Pack revizyonu CT-APR-01/02 ile onaylanmadan ve bu kararlar netleşmeden uygulama/API/UI kodu yazılmaz.

## Doğrulama planı

Gerçek geçici Mongo'da durum geçişi, idempotency, çakışan etkin aralık, görev ayrılığı, MDM/izin/kapsam kaybı, audit/transaction geri alma; MRP haftalık `asOf` tüketici uyumu. Ekranda yedi dil, UAS-001, şirket filtresi ve `ar` RTL; kritik guard için kasıtlı kırmızı ve geri alınmış yeşil test.
