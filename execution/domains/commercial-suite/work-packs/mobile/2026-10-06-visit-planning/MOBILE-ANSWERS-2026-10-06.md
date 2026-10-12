# Mobil sorularına yanıtlar — 2026-10-06

> Kaynak: [MOBILE-NOTE-2026-10-06-visit-planning.md](MOBILE-NOTE-2026-10-06-visit-planning.md) üzerine mobil ekibin üç sorusu. Yanıtlar CT kod okumasıyla (`test/crm-content-visit-e2e`, main ile senkron).

## 1. Düzenlemede strateji / kampanya / segment değerleri geri gönderilsin mi?
**Evet, öneriniz doğru.**
- `PUT` planlanan ziyaret güncellemesi **tam değiştirmedir**. Gönderilmeyen alan `null` olur:
  - kampanya bağı silinir;
  - içerik strateji şablonu olmadan yeniden çözülür;
  - sıklık segment olmadan yeniden hesaplanır (çoğu zaman `unknown`).
- **Kural:**
  - **Oluştururken** bu üç alanı **göndermeyin** (temsilci seçmez).
  - **Düzenlerken** okuduğunuz değeri **değiştirmeden aynen geri gönderin**. Ekranda gösterilmez, seçtirilmez.
- Detay yanıtında okunacak yerler:

| İstek alanı | Detay yanıtında |
|---|---|
| `campaignId` | `data.campaignId` |
| `strategyTemplateId` | `data.content.strategyTemplateId` (yoksa `data.selection.strategyTemplateId`) |
| `segmentId` | `data.selection.segmentId` |
| `contentSource` | `data.content.contentSource` |

- Sunucu bu alanları kendisi türetmeye başlayınca (Faz 2, B-3) istemciden gelen değer yok sayılır; o zaman gönderimi bırakabilirsiniz, haber verilecek.
- Bu yanıt, nottaki **M1** maddesini düzeltir: "gönderilmez" yalnız **oluşturma** içindir.

## 2. `resources/me` birden fazla kaynak döndürebilir mi?
- **Bugün hayır.** Geçici kural "kullanıcı = kaynak": her zaman **tek** öğe (`resourceId = kullanıcı kimliği`, `resourceType = "user"`, `status = "active"`).
- Kimlik çözülemezse 401, kiracı yoksa 400 döner; boş liste dönmez.
- Ürün kararı (2026-10-06): bölge ataması şimdilik **kullanıcı ↔ bölge**. Bu yüzden yakın planda da tek kaynak var.
- Birden çok kaynak ancak ileride pozisyon tabanlı modele geçilirse olabilir.
- **İstemci için:** `items[]` dizi olarak kalır, ilk öğeyi kullanın. Uzunluk > 1 ise basit bir seçim ekranı için hazırlıklı olun.
- **`displayName` alanı ekleniyor** (Faz 2, B-1; sizin R-M4 talebiniz). Tek kaynakta da gelecek.

## 3. Ad için ara çözüm: satır başına işyeri / kişi isteği sorun olur mu?
- **Ara çözüm olarak kabul**, şu koşullarla:
  - aynı kimlik için **tek istek** (çoğu ziyaret aynı işyerinde);
  - oturum boyunca **önbellek**;
  - aynı anda en fazla **4** istek;
  - yalnız **ekranda görünen** satırlar için.
- Uçlar: `GET /api/crm/contacts/{id}` ve `GET /api/crm/accounts/{id}`. Bugün kimlik listesiyle toplu okuma ucu **yok**.
- **Ayrı bir toplu ad ucu planlanmıyor.** Bunun yerine adlar doğrudan planlanan ziyaret liste / detay yanıtına ekleniyor (Faz 2, B-8):
  - taslak alan adları: `targetDisplayName`, `accountDisplayName`, `contactDisplayName` + hedef pasif işareti;
  - alanlar gelince ara isteklere gerek kalmaz.
- Sıra: Web'deki küçük düzeltme paketinden (VP-FIX-1) hemen sonra. Kesin alan adları sözleşme notuyla gelecek.
