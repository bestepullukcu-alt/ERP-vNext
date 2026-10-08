# Android uyum raporuna yanıt — 2026-10-08

> **Kime:** Android ekibi (iOS bilgi için) · **Kimden:** backend / Control Tower · **Tarih:** 2026-10-08
> **Yanıtlanan:** [ANDROID-REPORT-2026-10-08](ANDROID-REPORT-2026-10-08.md) · sözleşme: [MOBILE-CONTRACT-2026-10-08](MOBILE-CONTRACT-2026-10-08-visit-planning.md)
> Yanıtlar CRM koduna dayanıyor (main, 2026-10-08).

## 0. Önce: değişiklikler main'de
- Sözleşmedeki backend değişiklikleri **2026-10-08'de main'e birleşti.** Raporunuzdaki "test dalında, production'da var sayılmaz" önkoşulu kodda kalktı.
- Test ortamına dağıtım ayrıca bildirilecek. O zamana kadar "gerçek backend doğrulaması" BLOCKED kalabilir.
- Kapsam kararınız (ilk aşamada **yalnız görüntüleme**, planlama akışları DEFERRED) bizim için uygun.

## 1. ⚠ Düzeltmeniz gereken: `resources/me` alanlarının yeri
- `displayName` ve `countryCode` **üst düzeyde değil, `data.items[]` öğesinin içinde**. Raporunuza göre Android ikisini üst düzeyde okuyor; bu durumda bugün ikisi de hep boş gelir.
```json
{ "data": { "items": [ { "resourceId": "c576…", "resourceType": "user", "status": "active",
                         "displayName": "Admin User", "countryCode": "tr" } ] },
  "statusCode": 200, "isSuccessful": true }
```
- `items` hâlâ tek öğe; ilk öğeyi okuyun (06 Ekim yanıtı §2).

## 2. Sorularınız (§7)
1. **`contentSource` geri gönderilsin mi?**
   - **Evet, okunan değeri geri gönderin.**
   - `contentSource` elle seçilen yolculuk / aşamayla ilgili (`manual` + `contentEngagementJourneyId` / `…StageId`). Oyun / kampanya / segment türetmesiyle ilgisi yok.
   - Güncellemede oyun her seferinde doktordan yeniden türetilir; `contentSource` bunu bozmaz.
2. **`weekNumber`:** kullanmıyorsunuz, doğru. Önizleme yoksa gerekmez.
3. **Kapsam:** yalnız görüntüleme; not edildi. Planlama ekranı kararı ürün sahibinde.
4. **`week_reopened` iptallerini göstermek:** uygun. İptal edilmiş ziyaret raporlanamaz.
5. **Örnek yanıtlar:** test kiracısındaki planlar 2026-10-08'de manuel test için temizlendi. Ürün sahibinin test turunda hafta onayı ve yeniden açma yapılınca şu gerçek yanıtları çıkarıp ekleyeceğiz:
   - (i) `planned-visits` liste ve ayrıntı (`contentItems` dolu);
   - (ii) `week_reopened` iptali;
   - (iii) `resources/me` (şekil yukarıda);
   - (v) takvim.
   - **(iv) türler (kesin):**
     - `contentItems[].order`: **int**, listedeki 1 tabanlı sıra; 3C öncesi eski kayıtlarda `0`.
     - `contentItems[].warnings`: **string[]**, her zaman dizi, boş olabilir.
     - `steps[]`: `{ stepId, contentId, contentCode, title, type, minutes }` nesne dizisi.
     - `claims[]`: `{ claimId, claimCode }` dizisi.

## 3. Takvim (`GET /api/crm/visit-report/calendar`) — bugün ne var, ne gelecek
- **Bugün:**
  - öğede ürün listesi **`plannedContent[]`** adıyla geliyor (`contentItems` değil): `productId, productCode, role, journeyId, journeyCode, stageId, stageIndex, stageCode, stageName, steps[{ title, type }]`.
  - **`productName` yok.**
  - Öğede `planStatus` var (`cancelled` görünür), **`cancellationReason` yok**.
  - `reportState`: `none` · `draft` · `submitted` · `amended`.
  - `targetDisplayName` ve `targetInactive` var.
- **Gelecek (backend paketi, ek alan):**
  - takvim öğesine `cancellationReason`;
  - `plannedContent[]`'e `productName`.
- Gelene kadar: takvimde iptal "İptal edildi" (sizin bugünkü davranışınız doğru). Ürün için `productName ?? productCode`; takvimde kod görünür.

## 4. Açık konular (§8)
- **İptal edilmiş ziyaretin formu düzenlenebiliyor.**
  - Bugün backend yalnız **arşivlenmiş** ziyaretin güncellemesini reddediyor (`409 planned_visit_archived`); iptal edilmiş ziyaret güncellemesi **reddedilmiyor**. Bu bir boşluk.
  - Önerimiz: **iptal edilmiş ziyaret salt okunur** olsun, güncelleme `409`. Kural ürün sahibinin onayıyla backend paketine giriyor, kesinleşince bildirilecek.
  - O zamana kadar mobilde iptal edilmiş ziyaretin formunu **salt okunur** göstermeniz uygun olur.
- **`productName` null** (MDM izni yok ya da MDM kapalı) başarıdır; kodu göstermeniz doğru. Saha temsilcisi rolüne ürün okuma izni ürün sahibince verilecek.
- **`IGDIR` → "İgdir":** il kodu ASCII olduğu için baş harf yedeği kesin değil (Web'de de aynı sınır). Kesin çözüm il etiket seti; referans verisi sahiplerinde.
- **Referans etiketleri** (`attributes.label_<dil>`): okuma yolunuz doğru. Etiketler referans verisinin sahiplerince girilecek; şu an setlerde yalnız İngilizce `label` var.
- **SB-3c** (ziyaret yap / tamamla) ve **Faz 6 T1–T3:** uç yok; ayrı notla gelecek.

## 5. Teşekkür / gözlem
- **BiDi yaklaşımınız** (bağlam = etiketin ilk güçlü harfi) Web'deki yalıtımla uyumlu. Web'de ayrıca **sayı oranları** ("0 / 5") RTL'de ters dönüyordu; LRI…PDI ile yalıttık. Mobilde "yapılan / kalan" gibi oranlar gösteriyorsanız aynı sorun olabilir.
- **`ErpContactFiltersFlowTest`** kararsızlığı sizde izlenmeli; backend tarafında ilgili bir değişiklik yok.
