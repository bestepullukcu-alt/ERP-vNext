# Depo (WMS) → Mobil (Android) ekibi: sayım (MOD-0181) sorularınızın cevabı

> **Kime:** Android ekibi · **Kimden:** MVP-5 WMS Control Tower (merkez) · **Tarih:** 2026-11-13
> **Cevap verilen:** "Okutma cevaplarınız uygulandı; sayım formu hazır (MOD-0181)" notunuz, §3'teki 6 soru.
> **Kaynak:** donmuş sözleşme `docs/analysis/contracts/wms-count-bundle.openapi.yaml` (WMS-COUNT-BUNDLE **1.1.0 FROZEN**), servis kodu
> (`services/Diten.InventoryService/...`), paketler `MOD-0181-cycle-counting.md` (r8) ve `MOD-0182-barcode-rfid.md` (r7, ready-for-dev).
> Dosya:satır atıfları MVP-1 hat dalındaki (`feature/sce/mod-0173-inventory-ledger-valuation`, CT etiketi `ct/line-ff-2026-11-13f`) hâline göredir.

Uygulamayı bu kadar ilerletmeniz için teşekkürler. Önce bir düzeltme, sonra sorularınız.

## 1. Kısa özet

- **Yazma uçları yazıldı ve CT tarafından doğrulandı.** Ama gördüğünüz yerde yoklar, çünkü **uzak depoya henüz gönderilmediler**. Bu
  sizin hatanız değil; bizim tarafta bekleyen adım (§2).
- Önerdiğiniz iki şekil **doğru**. Tek ek: `Idempotency-Key` başlığı da gidecek ve gövdedeki `idempotencyKey` ile aynı olacak.
  `If-Match` giriş için **satırın**, gönderim için **sayımın** sürümüdür.
- Satırda ad yok. CT önerisi: adları **sözleşmeye eklemek** (yeni ek sürüm). Rol yetkisi genişletmeyi önermiyoruz (§3, soru 2).
- Liste ucunda "bana atanan" süzgeci **yok**. CT önerisi: ek sürümde `assignedTo` süzgeci eklemek (§3, soru 3).
- Bulunan stokta gövde **kimlikle** gider: `skuId`, `itemId`, `skuLevel` ve kayıtlı lotun kimliği. Ham barkod değeri gitmez (§3, soru 4).
- v1'de sayımda okutulan ürünü **kimse karşılaştırmıyor**. Karşılaştırmayı MOD-0182 okutma anında yapacak (§3, soru 5).
- Seri kontrollü satırda v1'de **yalnız miktar** girilir ve tam sayı olmalıdır. Seri okutma yok (§3, soru 6).
- **Dikkat (kör sayım):** gönderimden sonra beklenen miktarı yalnız `cycle-count.variances.read` yetkisi olan kullanıcı görür. Sayan
  rolünde bu yetki yok (§4).

## 2. "Yazma uçları hiçbir dalda yok": ne ölçtük

| Ölçüm | Sonuç |
|---|---|
| Uzak depodaki hat dalının ucu (`origin/feature/sce/mod-0173-inventory-ledger-valuation`; `git ls-remote` ile de aynı) | `a9fd192ba`, 2026-11-09. **Burada haklısınız.** |
| Bizdeki hat dalının ucu | `f4877003c`, 2026-11-13. Uzaktaki uçtan **367 commit** önde. |
| DL-2 kodu (giriş, bulunan stok, gönderim): `967478ad9` (2026-11-10); Gateway rota testi `96e25bf9b` | Yalnız yerel hat dalında ve CT etiketlerinde (`ct/line-ff-2026-11-10` … `-13f`) var. **Uzakta hiçbir dalda yok.** |
| Sözleşme dondurma: 1.0.0 `abaa7b8c7` (2026-11-12), 1.1.0 `087e5974b` (2026-11-13) | Aynı durum: yerelde var, uzakta hiçbir dalda yok. `ct/line-ff-*` etiketleri de uzakta yok. |
| `origin/main` | Yalnız DL-1 uçları var: liste, sayım, satırlar, olaylar, açma, güncelleme, toplu silme, yayımlama, iptal. Yani okuma uçlarına baktığınız yer doğru. |
| Uzaktaki taslak (`drafts/wms-count-bundle-v1-proposal.md`) | Üç işlemin yalnız yol satırı var, gövdesi yok. **Burada da haklısınız.** Gövdeleri bağlayan taslak commit'i (`7e75c22a7`) de uzakta değil. |

**Sonuç:** "Hiçbir dalda yok" cümlesi gördüğünüz yer için doğru. Kod ve donmuş sözleşme var, ama yalnız bizim makinemizde. Uzak depoya
gönderilmeleri bizim tarafta bekleyen bir adım. Gönderildiğinde size ayrıca haber vereceğiz. Main'e **hat PR'ıyla** gelecek. Tarih
vermiyoruz. O zamana kadar aşağıdaki şekiller donmuş sözleşmeden birebir alındı; sahte sunucunuzu bunlara göre kurabilirsiniz.

## 3. Sorularınızın cevapları

### Soru 1: Yazma uçları, gövdeler ve `If-Match`

Taban adres: Gateway üzerinden `/api/warehouse`. Bütün yazmalar şu kurallara uyar:

- `Idempotency-Key` başlığı **zorunludur** ve gövdedeki `idempotencyKey` ile aynı olmalıdır (8–100 karakter). Kaynak: sözleşme kural 5,
  `:28-32`; parametre `:559`.
- Aynı anahtar ve aynı gövde gönderilirse sunucu 200 ile kaydın **bugünkü hâlini** döner. Aynı anahtar başka gövdeyle gelirse 400
  `IDEMPOTENCY_KEY_REUSED` döner. Bu yüzden bir satırı **yeniden saymak** yeni anahtar ister. Cevap gelmeyince aynı anahtarla yeniden
  göndermeniz doğru.
- `If-Match` biçimi `W/"<version>"`'dur. Eksikse 400 `INVALID_REQUEST`, eskiyse 409 `COUNT_CONFLICT` döner. Kaynak: kural 6, `:33-35`;
  `:562`; kod `CycleCountSupport.cs:122-129`. Cevap, yazılan kaydın `ETag`'ini taşır.

| İşlem | Yol | `If-Match` | Gövde | Cevap | Yetki |
|---|---|---|---|---|---|
| `recordCycleCountLine` | `POST /cycle-counts/{countId}/lines/{lineId}/count` | **satırın** sürümü | `{ idempotencyKey, countedQuantity, scanRef? }` | 200 `CycleCountLine`, satırın `ETag`'i | `cycle-count.counts.record` |
| `submitCycleCount` | `POST /cycle-counts/{countId}/submit` | **sayımın** sürümü | `{ idempotencyKey }` | 200 `CycleCount`, sayımın `ETag`'i | `cycle-count.counts.record` |
| `addCycleCountLine` | `POST /cycle-counts/{countId}/lines` | **yok** (yeni satır) | soru 4'te | 201 `CycleCountLine` (tekrar gönderimde 200) | `cycle-count.counts.record` |

Kaynaklar: sözleşme `:304-320`, `:321-339`, `:270-288`; gövde şemaları `RecordCycleCountLineRequest` `:994-1001` ve `KeyRequest`
`:981-986`. Kod: `CycleCountsController.cs:127-164`; kabul edilen alanlar `CycleCountRequestReader.cs:188-191`. Gövdede bilinmeyen bir
alan 400 döner.

**Önerinizle farkı:** şekil aynı. Eksik olan yalnız `Idempotency-Key` başlığı. `countedQuantity` bir **metin**dir (`"12.5"`), sayı değildir.
Desen `^-?\d+(\.\d{1,6})?$`, değer 0 ya da daha büyük olmalı. Sayı olarak gönderirseniz 400 `COUNT_QUANTITY_INVALID` döner.

**Sürüm hakkında bilmeniz gereken iki şey:**

- Satır sürümünü satır listesinden (`version` alanı) ya da önceki girişin cevabından alın.
- **Her giriş sayımın sürümünü de artırır.** Kod: `CycleCountLineCommandHandler.cs:52`. Bulunan satır da artırır. Bu yüzden gönderimden
  hemen önce `GET /cycle-counts/{countId}` ile sayımı yeniden okuyun ve onun `ETag`'ini kullanın. Elinizdeki eski sayım sürümüyle
  gönderirseniz 409 `COUNT_CONFLICT` alırsınız (`SubmitCycleCountHandler.cs:43`).

**Hata kodları:**

| İşlem | Kodlar |
|---|---|
| Giriş | `INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED`, `COUNT_QUANTITY_INVALID`, `COUNT_NOT_FOUND`, `COUNT_LINE_NOT_FOUND`, `COUNT_CONFLICT`, `COUNT_INVALID_STATE` |
| Gönderim | `INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED`, `COUNT_NOT_FOUND`, `COUNT_CONFLICT`, `COUNT_INVALID_STATE`, 422 `COUNT_LINES_UNCOUNTED`, 503 `COUNT_INVENTORY_UNAVAILABLE` |

Kullandığınız 7 kodun hepsi katalogda var (`:1052-1074`). Sürüm çakışması bu sözleşmede **yalnız 409** döner; 412 yoktur.

**Durum kuralları:**

- Giriş için sayım `Released` ya da `Counting`, satır `Open` ya da `Counted` olmalıdır. Değilse 409 `COUNT_INVALID_STATE` döner.
- İlk giriş sayımı `Counting`'e taşır.
- Gönderimde her `Snapshot` ve `Recount` satırı sayılmış olmalıdır. Satır kalmışsa 422 `COUNT_LINES_UNCOUNTED` döner.
- Bütün satırlarda fark yoksa sayım doğrudan `Closed` olur. Fark varsa `Submitted` olur. Kaynak: sözleşme `:91-112`.

**Main'e ne zaman gelir:** hat PR'ıyla gelecek. Önce uzak depoya gönderilmesi gerekiyor (§2).

### Soru 2: Satırda ad (raf kodu, ürün kodu ve adı, lot numarası)

**Ölçüm:**

- `CycleCountLine` (`:672-704`) yalnız kimlik taşır: `locationId`, `item { itemId, skuId, skuLevel }`, `lotId`.
- Raf kodu, ürün kodu, ürün adı ya da lot numarası alanı **yok**.
- Şema kapalıdır (`additionalProperties: false`). Bu yüzden alan eklemek yeni bir sözleşme sürümü ister.

**Web de aynı sorunu yaşıyor:** Web sayım formu adları kullanıcının kendi yetkisiyle üç ana veriden okuyor (`CycleCountProxy.cs:134-159`,
`MasterLabelResolver`). Paketteki Sayan rolü önerisi yalnız `counts.read` ve `counts.record` içeriyor (`MOD-0181-cycle-counting.md:1008`).
Yani sayan kullanıcı Web'de de adları göremez.

**CT önerisi: adları sözleşmeye eklemek.**

- WMS-COUNT-BUNDLE'a eklemeli bir 1.2.0 sürümü açılır.
- `CycleCountLine`'a isteğe bağlı üç alan eklenir: `locationCode`, `skuCode` ve `lotNumber`.
- Alanlar sayım yayımlanırken (bulunan satırda eklenirken) bir kez yazılır, `expectedQuantity` gibi.
- Ürün adı için ayrıca `skuName` öneriyoruz. Ama adın hangi dilde tutulacağını ölçmedik. Bu yüzden bu alan karar sırasında netleşecek.

**Gerekçe:**

- Emsal var. WMS-BUNDLE görevi `lotNumber` ve `warehouseCode` taşıyor (`wms-bundle.openapi.yaml:1313`, `:1324`). Çıkış görünümündeki
  toplama görevi `fromLocationCode` taşıyor (`:1706`).
- Sayan role üç ana veri okuma yetkisini vermek, sayımla ilgisi olmayan bütün raf, ürün ve lot kayıtlarını da açar. En az yetki ilkesine
  aykırı.
- Satır başına üç ek çağrı da gerekmez.

**Durum:** bu bir **karar** gerektiriyor. MOD-0181 paket revizyonu, CT dondurma adımı ve bir geliştirme işi lazım. Henüz açılmadı. Tarih
yok.

**O zamana kadar:**

- Bugünkü "Ürün adı okunamadı" ve "Lot numarası okunamadı" davranışınız doğru. Kimliğe düşmeyin.
- Test ortamında üç okuma yetkisini test kullanıcısına elle vermek yalnız deneme içindir. Rol şablonuna girmez.

**Toplama ve yerleştirme görevleri:**

- `WarehouseTask` `lotNumber` ve `warehouseCode` taşıyor (`:1324`, `:1313`).
- **Raf kodu yok:** `fromLocationId`, `toLocationId` ve `suggestedToLocationId` yalnız kimlik.
- **Ürün kodu ve adı yok:** `item` yalnız kimlik (`:1166-1173`).
- Çıkış görünümündeki toplama görevinde (`OutboundPickTask`) `fromLocationCode` ve `lotNumber` var, ürün adı yok.
- Eksik sizin gördüğünüz gibi. WMS-BUNDLE MOD-0178'in sözleşmesi olduğu için aynı öneri orada **ayrı bir karar** gerektiriyor. Bunu
  kaydettik.

### Soru 3: "Bana atanan" süzgeci

**Ölçüm:** `listCycleCounts` yalnız şu süzgeçleri alıyor: `status` (çoklu), `warehouseId`, `q` (sayım numarası), `page` ve `pageSize`
(en çok 200). Kaynak: sözleşme `:117-135`, kod `CycleCountsController.cs:32-36`. **Atanan süzgeci yok.**

**CT önerisi:**

- Aynı 1.2.0 ek sürümünde `listCycleCounts`'a isteğe bağlı `assignedTo` (uuid) süzgeci eklenir.
- "Atanmamış" grubu için `unassigned=true` eklenir.
- Emsal: WMS-BUNDLE görev listelerindeki `assigneeUserId` süzgeci (`:404`, `:663`).
- Bu da bir karar gerektiriyor. Henüz açılmadı.

**O zamana kadar:**

- `status=Released&status=Counting` ve deponuzun `warehouseId`'si ile süzün.
- 200'den fazla kayıt olursa sayfalayın. Yalnız ilk 200 kayıtla yetinirseniz bazı sayımlar listede görünmez.

**Bilgi:** sunucu girişi atanan kişiyle **sınırlamıyor**. `cycle-count.counts.record` yetkisi olan herkes giriş yapabilir (girişte
`AssignedTo` denetimi yok). "Başkasına atanmış sayımı göstermeme" kuralı yalnız sizin uygulamanızdaki bir tercih.

### Soru 4: Bulunan stok (`addCycleCountLine`)

**Gövde** (`AddCycleCountLineRequest`, `:1002-1013`; kod `CycleCountRequestReader.cs:72-87`):

```
{ idempotencyKey, locationId, item: { itemId, skuId, skuLevel }, lotId?, countedQuantity, scanRef? }
```

- **Ham değer gitmez.** Ürün `skuId`, `itemId` ve `skuLevel` ile gider. `skuLevel` şu değerlerden biridir: `Gsku`, `Lsku`, `FinishedGood`,
  `Item`.
- **Lot:** `lotId`, MOD-0174'te kayıtlı lotun **kimliğidir** (küçük harfli UUID). Lot numarası değildir (`AddCountLineHandler.cs:133-135`,
  `LotPostingRules.cs:129-130`).
  - Lot kontrollü üründe lot yoksa 400 `COUNT_LOT_REQUIRED` döner.
  - Bu ürüne ait kayıtlı bir lot değilse (lot numarası yazılması dahil) 422 `COUNT_LOT_UNKNOWN` döner.
  - Lot kontrolsüz üründe lot gönderilirse 422 `LOT_NOT_ALLOWED` döner.
- **Raf ve ürün kapsamı:**
  - Raf sayımın `scopeBinIds` listesinde olmalı.
  - `scopeSkuIds` doluysa ürün de o listede olmalı.
  - Değilse 422 `COUNT_LINE_OUT_OF_SCOPE` döner (`AddCountLineHandler.cs:59-62`).
- **Aynı anahtar:** aynı raf, ürün ve lot için satır zaten varsa 409 `COUNT_LINE_DUPLICATE` döner.
- **Yeni satır:** stok durumu yalnız `AVAILABLE` olur. `origin` `Found`, beklenen miktar 0, durum `Counted` olur.
- **Diğer kodlar** (`:274`): `UNKNOWN_ITEM`, `SKU_ITEM_MISMATCH`, `SKU_LEVEL_NOT_STOCKABLE`, `PRODUCT_MASTER_UNAVAILABLE`,
  `PRODUCT_MASTER_DATA_INCOMPLETE`, `COUNT_CONFLICT`.

**Pratikte:** okutulan barkoddan bu kimliklere ulaşmak için bugün ana veri okuması gerekir. Bu da soru 2'deki yetki sorununa takılır.
Barkoddan ürün bulma ayrıca MOD-0290-FU07'ye (GTIN) bağlı.

**CT önerisi:** mobilde bulunan stok ekranını MOD-0182 okutma ucu gelene kadar ertelemeniz doğru.

- O uç `Count` bağlamında çözülmüş ürünü ve lotu `foundStock: true` ile döner (`MOD-0182-barcode-rfid.md:654`).
- Sonra siz o kimliklerle ve `scanRef` ile `addCycleCountLine`'ı çağırırsınız.
- O zamana kadar bulunan stok Web sayım formundaki "Bulunan stok ekle" ile girilir.

### Soru 5: Sayımda ürün kontrolü

- **v1'de (bugün) kimse karşılaştırmıyor.** MOD-0181'de `scanRef` opaktır: saklanır, doğrulanmaz (Q-12 = A; paket `:286`, `:469`, `:1290`).
- Sunucu, okutulan ürünün satırın ürünü olup olmadığına bakmaz. Satırı seçen sayan kişidir.
- Bugünkü davranışınız (yalnız saklamak, mobilde karar vermemek) doğru.

**MOD-0182 okutma anında karşılaştıracak.** Paket `§8.4` bağlam kuralları (`MOD-0182-barcode-rfid.md:654`):

- `Count` bağlamında adımlar `Location` → `Product`. Beklenen, sayımın rafları ve satır anahtarlarıdır.
- Raf sayım kapsamında değilse `Rejected` `WRONG_LOCATION` döner. **Aşılamaz.**
- Ürün ya da lot o rafın satırlarında yoksa `Accepted` ve `foundStock: true` döner. Bu durumda bulunan stok yoluna gidersiniz.
- Kör sayımda cevapta beklenen miktar ve raf stoğu **hiç yoktur**.
- Sayım kapanmışsa `Rejected` `CONTEXT_CLOSED` döner (`:669`).

**Okutmanın girişe bağlanması** (`§8.7`, `:727-751`):

- Giriş `scanRef = "scan-session:{sessionId}"` taşırsa sunucu girişle aynı işlemde oturumu denetler.
- Oturumda `Location` ve `Product` adımlarının son olayları kabul edilmiş olmalı.
- Okunan raf, satırın rafıyla aynı olmalı (bulunan satırda isteğin `locationId`'si).
- Değilse 422 `BARCODE_SESSION_INVALID` döner ve giriş yazılmaz.
- Öneksiz `scanRef` bugünkü gibi opak kalır. Okutmanın zorunlu olduğu depolarda ise oturum gerekir.

**Bugünkü durum:**

- MOD-0182'nin kodu **yok** (servislerde `BarcodeCollections` ve `barcode/scans` bulunmadı).
- Seçilen satırla okutulan ürün farklıysa, ürün aynı rafta başka bir satırda olsa bile sonucun ne olacağı pakette açık yazılı değil.
  Bunu ölçmedik. DL-1 sözleşme notunda netleşecek.
- Akışı şimdiden "raf okut → ürün okut → sunucu satırı ya da bulunan stoğu söyler" modeline uygun kurmanızı öneririz.

### Soru 6: Seri kontrollü satır (`serialControlled: true`)

- **v1'de yalnız miktar girilir. Seri okutma yok.**
- Miktar **tam sayı** olmalıdır. Kesirli değer 400 `COUNT_QUANTITY_INVALID` döner (`RecordCountLineHandler.cs:49`, bulunan satırda
  `AddCountLineHandler.cs:74-75`; sözleşme `:307`).
- Paket kararı Q-10 = A (`MOD-0181-cycle-counting.md:792-796`):
  - Fark hesaplanır ama **defter düzeltmesi yapılmaz**.
  - Onayda 422 `COUNT_SERIAL_NOT_POSTABLE` döner. Onaylayıcı yalnız reddedebilir ya da yeniden sayım isteyebilir.
- Seri düzeyinde sayım ayrı bir iştir (**FU-0181-SERIAL**, `:1405`).
- Okutmanın seri adımı MOD-0182'de `Serial` adımı olarak var. Sayıma bağlanması v1'de yok.

## 4. Notunuzda düzeltmemiz gereken bir nokta: kör sayımda gönderimden sonra

Notunuzda şöyle yazıyor: "Gönderimden sonra satırlar yeniden okunuyor; beklenen miktar ancak o zaman görünüyor." Bu **yalnız
`cycle-count.variances.read` yetkisi olan kullanıcı için** doğru.

- Sözleşme kural 12 (`:44-46`): kör sayımda `expectedQuantity`, `movementsSinceSnapshot`, `expectedAtCount`, `varianceQuantity` ve
  `adjustment.quantity` alanları gönderimden önce herkese, gönderimden sonra da bu yetkisi olmayana **gelmez**.
- Kod da yetkiyi kullanıcının jetonundan okuyor (`CycleCountsController.cs:53`, `:180-182`).
- Sayan rolü önerisinde bu yetki yok (`MOD-0181-cycle-counting.md:1008`). Yani sayan kişi gönderimden sonra da beklenen miktarı görmez.
- **Uygulamanız alan yoksa bunu bir hata ya da "okunamadı" gibi göstermemeli.** Alan yalnız gelmez; bu doğru davranıştır.

## 5. Önceki notumuzdaki (2026-11-12) yanlışlar: düzeltme

- "`GET /cycle-counts` (bana atanan)" yazmıştık. **Böyle bir süzgeç yok** (soru 3).
- "`.../lines/{lineId}/count` (bulunan stok satırı da)" yazmıştık. **Yanlış.** Bulunan stok `POST /cycle-counts/{countId}/lines` ile
  girilir (soru 4).
- Sözleşmeyi "1.0.0 FROZEN" diye anmıştık. Bugünkü sürüm **1.1.0 FROZEN**. Sürüm ekleme yaptı (kanıt paketi, yalnız Web); sizin
  kullandığınız uçlar değişmedi.
- Sürüm çakışmasını "412 / 409" diye yazmıştık. Sayım sözleşmesinde **yalnız 409 `COUNT_CONFLICT`** var.

## 6. Çeviriler

Değişikliği not aldık:

- 7 anahtar çıktı (gerekçe adımı).
- `warehouse_pin_wrong` artık yalnız "Yanlış PIN." Yer tutucusu yok.
- 43 yeni anahtar eklendi.
- İki anahtar çoğul biçimli: `warehouse_count_lines` ve `warehouse_count_uncounted_left`. Her dilin çoğul kuralıyla çevrilecek.

**Bir sayı tutmuyor:** 151 − 7 + 43 = **187**. Notunuzda **188** yazıyor. CSV dosyasını görmedik, sayamadık. Lütfen dosyadaki anahtar
sayısını kontrol edip bize yazın.

## 7. Açık kararlar (bizde)

| # | Konu | Öneri | Kim karar verir |
|---|---|---|---|
| K-1 | Uzak depoya gönderme (DL-2, DL-3 kodu ve 1.1.0 sözleşmesi) | gönderilsin, size haber verilsin | merkez (Beste); tarih yok |
| K-2 | Satıra adlar: `locationCode`, `skuCode`, `lotNumber` (ve `skuName`, dil kuralıyla), WMS-COUNT-BUNDLE 1.2.0 | ekle (soru 2) | MOD-0181 sahibi + CT; açılmadı |
| K-3 | `listCycleCounts` için `assignedTo` ve `unassigned` süzgeci (1.2.0) | ekle (soru 3) | MOD-0181 sahibi + CT; açılmadı |
| K-4 | Görevlerde raf kodu ve ürün kodu/adı (WMS-BUNDLE, MOD-0178) | aynı desen | MOD-0178 sahibi + CT; ayrı karar |
| K-5 | MOD-0182 `Count` bağlamı: seçilen satır ile okunan ürün farklıysa ne olacak | DL-1 sözleşme notunda netleşecek | MOD-0182 CT |
