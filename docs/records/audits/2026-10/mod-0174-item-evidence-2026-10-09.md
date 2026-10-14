# MOD-0174 / MOD-0175 ITEM — `Item` seviyesi TRACE ve QUARANTINE'de, stoklanamaz SKU, `updateHold`, kişi adları (WP-0174-ITEM, CT-MVP1-0174-ITEM v1)

| alan | değer |
|---|---|
| Modül | MOD-0174 Lot/Serial + MOD-0175 Quarantine, dilim ITEM (O-PM1-1, O-PM1-3, ASSUMPTION-0173-PM1-03, FU-0175-Q5-01, FU-0175-Q5-02) |
| Dal / taban | `feature/sce/mod-0174-item`, taban `9697fda76` (temiz). Yalıtılmış worktree `C:\Users\Ali\erp-vnext-wt-0174-item`. Push, PR, merge yok |
| Commit'ler | 1) `074654e8b` sözleşmeler (TRACE 1.3.0, QUARANTINE 1.1.0, INVENTORY 1.2.1) + README · 2) `367a30326` kod + testler + Web · 3) bu kanıt + betikler belgesi + 12 ekran görüntüsü + MOD-0174 / MOD-0175 pack §19 ITEM notları |
| Durum | **ajan sonucu — CT bekliyor** |
| BLOCKED | Yok. İki nokta sözleşmenin izin listesini aşıyordu. İkisi de sahibe soruldu ve karar alındı (§1): `updateHold` için yalnız denetim satırı yazılır; TRACE `createLot` listesine `SKU_LEVEL_NOT_STOCKABLE` eklenir. `qd-v1` alan listesi değişmedi; kişi adları için yeni uç ya da sözleşme değişikliği gerekmedi; FUB1 dosyalarına (`ProductPickerSource`, stok defteri listesi) dokunulmadı |

## 1. Sahip kararları (bu WP içinde)

**K-1 — `updateHold` geçmiş satırı.**
- Çelişki: NE-4 ve sözleşme kuralı 7 "denetim + geçmiş satırı" istiyor. Ama QUARANTINE'in `HistoryAction` enum'u kapalı (16 değer) ve "not güncellendi" anlamına gelen bir değeri yok. Yeni değer eklemek `Item` dışında bir şema değişikliği olurdu.
- Karar: **sözleşme değişmez, yalnız denetim satırı yazılır.**
- Uygulama: `inventory_audit` koleksiyonuna `QUARANTINE_HOLD_UPDATED` satırı yazılıyor. Satırda önceki/yeni not ve ekler, önceki sürüm, kullanıcı ve correlation id var. Geçmiş satırı yazılmıyor. Bu durum açık nokta **O-ITEM-1** olarak kaydedildi.

**K-2 — Stoklanamaz SKU'ya lot.**
- Karar: TRACE 1.3.0'da `createLot` hata listesine `SKU_LEVEL_NOT_STOCKABLE` (422) eklenir. INVENTORY'deki kodun aynısı.
- Değişen yalnız `x-error-codes` listesi. Şema ve durum kodu değişmedi.

## 2. Ne yapıldı

**Sözleşmeler.** Üçü de FROZEN kaldı. Her birine changelog satırı eklendi, README güncellendi.

- **TRACE 1.3.0:**
  - `SkuLevel` enum'unun sonuna `Item` eklendi. Açıklama metni değişmedi.
  - `createLot` `x-error-codes` listesine `PRODUCT_MASTER_DATA_INCOMPLETE` ve `SKU_LEVEL_NOT_STOCKABLE` eklendi.
- **QUARANTINE 1.1.0:** yalnız `SkuLevel` enum'una `Item` eklendi.
- **INVENTORY 1.2.1:** yalnız `postMovement` açıklamasına "`stockable: false` SKU → aynı 422" eklendi.

Bu sınırlar testle ölçülüyor. 1.2.0 / 1.0.0 / 1.2.0 dosyaları SHA ile sabitlenmiş fikstürler olarak repoda. Testler, eski dosyanın her öğesinin yeni dosyada aynen bulunduğunu ve eklemelerin **tam olarak** yukarıdakiler olduğunu doğruluyor.

**TRACE `Item` (NE-1).**
- `createLot`, lot okuma uçları ve GS1 kontrolü `Item` seviyesini kabul ediyor:
  - `WireNames.TryParseLegacySkuLevel` kaldırıldı;
  - K-G `LotIdentityPolicy.AllowedSkuLevels` listesine `Item` eklendi;
  - doğrulama `getSku(level=Item)` ile yapılıyor.
- `Item` seviyesinde seri takibi yok: `trackingMode = Lot`.
- Lot bayrağı bilinmiyorsa **422 `PRODUCT_MASTER_DATA_INCOMPLETE`** dönüyor (önceden 503). ASSUMPTION-0173-PM1-03 kapandı.
- `getProduct` yalnız GSKU'da çağrılıyor. Bu, PM1'deki defter kuralıyla aynı.
- `Item` seviyesinde GTIN null olduğu için GS1 kontrolünde GTIN adımı `Skipped` sonucunu alıyor (mevcut null kuralı).

**QUARANTINE `Item` (NE-2).**
- İstek okuyucusu `Item`'ı kabul ediyor. Yanıtta `ItemRef.skuLevel` olarak `Item` dönüyor.
- `qd-v1` alan listesi değişmedi. `Item` için değerler doğru doluyor: `itemId == skuId`, lot, satır miktarı ve hedef durum. Testle doğrulandı.

**Stoklanamaz SKU (NE-3, O-PM1-3).**
- `getSku` yanıtındaki `stockable: false` iki şeyi reddediyor:
  - her hareketi: 422 `SKU_LEVEL_NOT_STOCKABLE`, hiçbir şey yazılmaz;
  - lot oluşturmayı.
- Kural tek noktada: `ProductMasterStockRules.IsStockable`.
- REVERSAL bu kurala hiç ulaşmaz (O-PM1-2 CT kararı).
- **ASSUMPTION-0174-ITEM-01:** `stockable` alanı yoksa davranış bugünkü gibi; ret yalnız açıkça `false` olduğunda.

**`updateHold` (NE-4, FU-0175-Q5-01).** `PATCH /api/quarantine/holds/{id}`:
- Anahtar `quarantine.holds.review`. If-Match zorunlu.
- Gövde: `analysisNote` (en fazla 2000, boş dize notu temizler), `attachments` (en fazla 20, her birinde `name` + `dmsRef`), `idempotencyKey`.
- Ret durumları:
  - kayıt yok → 404;
  - karar verilmiş kayıt → 409 `QUARANTINE_HOLD_CLOSED`;
  - eski sürüm → 409 `QUARANTINE_CONFLICT`. Hem handler kontrol ediyor hem sürüm koşullu replace.
- Aynı anahtar + aynı gövde → güncel kayıt döner; aynı anahtar + farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`.
- Hiçbir şey değişmiyorsa yazma yapılmaz.
- Yanıt `HoldDetail`, yeni ETag ile.
- Tek transaction: kayıt + denetim satırı (K-1).
- Mimari testi `QuarantineArchitectureTests` daraltıldı: karantina kodu yalnız `InventoryCollections.Audit`'e erişebilir, yalnız `Find` ve `InsertOneAsync` çağırabilir. Ledger koleksiyonları hâlâ yasak.
- Ekran: "Notu düzenle" düğmesi Razor'da yalnız `quarantine.holds.review` sahibine çiziliyor (UAS-001). JS'te mevcut `STARTREVIEW` eylem kodu ile gösteriliyor; bu kod aynı anahtarı taşıyor, bu yüzden manifest değişmedi. Kayıt sayfanın ETag'iyle PATCH gönderiyor.

**Kişi adları (NE-5, FU-0175-Q5-02)** — ölçüm §6'da. Web, sayfadaki her `{ userId }` nesnesinin `name` alanını dolduruyor. `UserRef.name` sözleşmede zaten vardı.
- Doldurulan alanlar: kayıt (`createdBy`, `reviewStartedBy`), geçmiş (`by`), karar (`decider`, `approver`).
- Kaynak: AuthService'in mevcut `GET /api/users/{id}/display-label` ucu, çağıranın token'ıyla.
- Ayarlar'daki aday kişiler (`user:<id>`) mevcut `lookups/{kind}` ucunun yeni `users` türüyle çözülüyor.
- Çözülemeyen kimlik → kısaltılmış kimlik + "bilinmeyen kullanıcı" (`QuarantineUi.person`). Sayfa hata vermez.

**Web (NE-6).**
- Lots formu `Item:<id>` değerini kabul ediyor (`LotFormRules.TryParseSku`). Seçici zaten Item SKU'larını getiriyordu.
- Seviye etiketleri: Lots detayında `Level.*` (4 anahtar), karantina detayında `Quarantine.Level.*` (4 anahtar). Çeviriler PM1'de hareket formu için yapılmış olanlarla aynı.
- Yeni anahtarlar: `Quarantine.Common.UnknownUser`, `Quarantine.Details.Quality.EditNote`, `Quarantine.Details.Quality.NoteSaved`.
- 7 dilde gerçek çeviri. Mevcut resx/anahtar eşitlik testleri yeşil.

## 3. Testler — önce / sonra

Inventory testleri kendi geçici replica set'inde koştu (`TestSettings`, EphemeralMongo); 27017'ye gidilmedi.

| süit | taban (9697fda76) | sonra (367a30326) | uyarı (`--no-incremental`) |
|---|---|---|---|
| Inventory | 785/800 — 15 kırmızının hepsi yük altında Prism'in 90 sn'de açılamaması; sözleşme filtresi tek başına 50/50 → **800** | **829/829** (+29) | 0 → 0 |
| Web | 685/685 | **694/694** (+9) | 21 → 21 |
| Gateway | 107/108 | 107/108 | 7 → 7 |
| Mimari | 18/18 | 18/18 | — |

- Gateway'deki tek kırmızı bilinen 5065 kırmızısı (`EveryRoute_DownstreamPortIsInKnownServiceSet`).
- `AccountKindAcceptanceHost.Tests` koşulmadı.
- **Sözleşme doğrulayıcıları:**
  - TRACE 1.3.0 / QUARANTINE 1.1.0 / INVENTORY 1.2.1 sürüm ve fark testleri yeşil;
  - her API testinin yanıtı kendi sözleşmesine karşı doğrulanıyor (`ContractValidatingHandler`), `Item` hold'ları dahil;
  - Prism tüketici testleri yeşil.

## 4. Önce-kırmızı

Yöntem: yeni testler, tabanı (9697fda76) içeren **ayrı** bir geçici worktree'ye kopyalandı. Oraya yalnız iki `Stockable` kayıt alanı derleme dolgusu olarak eklendi; hiçbir kural bu alanları okumuyor. Testler orada koşuldu. Worktree işten sonra kaldırıldı.

Sonuç: **21 kırmızı.**

| senaryo | taban kodda | ITEM'de |
|---|---|---|
| Lot takipli `Item` kaleme lot oluşturma | **400** "skuLevel must be Gsku, Lsku or FinishedGood" | 201, `trackingMode=Lot` + lotlu giriş 201 |
| Lot bayrağı bilinmeyen kaleme lot (Lsku) | **503** `PRODUCT_MASTER_UNAVAILABLE` | **422** `PRODUCT_MASTER_DATA_INCOMPLETE` |
| Lot bayrağı bilinmeyen kaleme lot (Item) | 400 | 422 aynı kod |
| `Item` stoku karantinaya (lot takipsiz giriş) | hold açılıyor ama **GET hold şema ihlali**: `/item/skuLevel: enum` | 1.1.0 gövdesi, liste ve detay geçerli |
| `Item` lotu karantinaya → karar → `qd-v1` → onay → uygulandı | 400 (lot oluşturulamıyor) | Applied, 40 AVAILABLE |
| `Item` stoku elle karantina / bloke | 400 "skuLevel must be …" | 201 / 201 |
| `stockable: false` SKU'ya giriş (Item ×2, Lsku) | **201** | 422 `SKU_LEVEL_NOT_STOCKABLE`, hiçbir şey yazılmaz |
| `stockable: false` SKU'ya lot (Item, Lsku) | 400 / **201** | 422 `SKU_LEVEL_NOT_STOCKABLE` |
| Sonradan stoklanamaz olan SKU'dan çıkış | 201 | 422; ters kayıt 201 |
| `updateHold` (6 durum) | **405** (uç yok) | 200 + denetim satırı; eski sürüm 409; kapalı kayıt 409; biçim / If-Match / anahtar 400 / 403; aynı anahtar 200, farklı gövde 400; boş not temizler |
| GS1 kontrolü `Item` seviyesinde | 400 | GTIN `Skipped`, `PRODUCT_MASTER_GTIN_NULL` |

**Web önce-kırmızı.** Yeni Web testleri tabanda **derlenmiyor**: `QuarantinePeople` sınıfı yok ve `Lookups`'ta `ids` parametresi yok. Bu bir derleme kırmızısı.

Taban davranış ekrandan da görülüyor:
- tabanda `details.js` kişileri `h.by?.name || h.by?.userId` olarak gösteriyordu. Servis `name` doldurmadığı için ekranda ham kimlik vardı (Q5 O-Q5-2);
- `LotFormRules.TryParseSku("Item:…")` false dönüyordu.

## 5. Sabotajlar (K3)

Her sabotaj: tek metin değişikliği → derle → ilgili testler → geri al. Geri almadan sonra 30/30 yeşil ve `git status` aynı.

| # | sabotaj | kırmızı | testler |
|---|---|---|---|
| (a) | TRACE `createLot` `Item`'ı yine reddediyor | **7** | `A_lot_controlled_Item_gets_a_lot_and_its_receipt_posts_with_that_lot`, GS1, stockable / unknown-flag (Item), iki karantina akışı |
| (b) | QUARANTINE yanıtı `Item` hold'u eski değerle (`Lsku`) döndürüyor | **3** | `An_Item_receipt_into_quarantine_…`, `A_non_lot_Item_receipt_…_1_1_0_bodies`, `Item_level_stock_is_quarantined_and_blocked_manually` |
| (c) | `IsStockable` hep `true` | **6** | `StockableSkuTests` ×4, `A_stockable_false_sku_gets_no_lot` ×2 |
| (d) | `updateHold`'da sürüm kontrolü tamamen kaldırıldı (handler + koşullu replace) | **1** | `A_stale_version_is_409_and_nothing_changes` |
| (e) | Web kişi adı çözücüsü hiçbir şey çözmüyor | **3** | `QuarantinePeopleTests` — label okuma, sayfa verisinde adlar, `lookups/users` |

Not (b): ilk koşu derlenmedi (sabotaj satırında `SkuLevel` için using yoktu). O koşu (a)'nın ikilisiyle çalıştığı için **geçersiz sayıldı**. Tam nitelikli adla yeniden koşuldu: 3 kırmızı.

Not (d): handler kontrolünü tek başına kaldırmak testi kırmızı yapmaz. Sürüm koşullu replace ikinci katman olarak 409'u yine verir. Bu yüzden sabotaj iki katmanı birlikte kaldırıyor.

## 6. Kişi adı çözücüsü — ölçüm (madde 5)

Web'de **tek bir ortak kullanıcı etiketi çözücüsü yok**. Bulunan desenler:

| desen | nerede | uç / anahtar | bilinmeyen kimlik |
|---|---|---|---|
| A — sayfa başına sunucu tarafında, kimlik başına okuma | `ConsentPreferencesController.ResolveUserNamesAsync`; `KnowledgeConceptsController.UserDisplayName` | `/api/users/{id}` (`auth.users.read`) → ad soyad ya da e-posta | ham GUID / satır gizli |
| B — tüm kullanıcı listesini yükle, istemcide eşle | Workflow (`/Platform/Workflow/lookup/users`), WorkReport, DocumentManagement, PositionAssignments, CRM | `/api/users` (`auth.users.read`). Parametresiz çağrı yalnız ilk 20 kullanıcıyı getiriyor | kimlik |
| C — arama | `/Users/api/lookup` | `/api/users/lookup` (`auth.users.lookup`) | — |
| D — kimlik başına etiket | Web'de **kullanan yok**. AuthService `GET /api/users/{id}/display-label`; gateway `/api/users/{everything}` ile yönlendiriyor | `auth.users.lookup`. Başka kiracı ve olmayan kullanıcı için aynı 404. Yalnız etiket, e-posta yok | — |
| E — adı servis veriyor | Lots geçmişi, Meetings, Tasks, e-imza denetimi | — | `UnknownUser` |

Seçim:
- **Desen:** A. Sayfa çağrısı başına, sunucu tarafında, çağıranın token'ıyla çözülüyor. Karantina controller'ı ürün ve lokasyon adlarını zaten böyle dolduruyor (`MasterLabelResolver` → `Decorate`).
- **Kaynak:** D (`display-label`). Bu uç amaca yönelik ve yalnız etiket döndürüyor; A'nın kullandığı `/api/users/{id}` ise e-posta ve profil döndürüyor.
- **Neden B değil:** B'nin 20 kullanıcı sınırı var ve `auth.users.read` istiyor.
- Yeni uç yok. Sözleşme değişikliği yok.
- **Yetki:** `auth.users.lookup` varsayılan rollerden yalnız kiracı Admin'inde var (`DefaultRolePermissionTemplate.TenantSelfServicePermissions`). Bu anahtarı taşımayan karantina kullanıcısı adları göremez, "bilinmeyen kullanıcı" görür. Yoklamada QA rolüne bu anahtar verildi. Hangi rollerin bu anahtarı alacağı açık nokta: **O-ITEM-2**.

## 7. Çalışma zamanı yoklaması

**Yığın.** Hepsi kendi süreçlerim, kendi portlarımda:
- AuthService 15056, Platform 15057 (Workflow + MOD-0022), Gateway 15191 (geçici ocelot kopyası), Inventory 15176, Web 15001;
- ürün ana verisi taklidi 14036 (`pm-stub.js`, PM1 betikleri). Filonun 4010'una istek gönderilmedi;
- şirket taklidi 14037;
- yalıtılmış mongod.

Kullanıcılar: A (John Doe, karar veren), B (Jane Smith, onaylayan), QA rolüyle. Token'lar yalnız kendi AuthService girişlerinden alındı. Şifre taraması: 7 log, transkript ve repo içinde **0** eşleşme.

**API kontrolleri** (`api-checks.json`, 9/9 OK):

| kontrol | sonuç |
|---|---|
| `createLot`, Item seviyesi (lot takipli hammadde RM-…0002) | 201, `trackingMode=Lot` |
| Bu lotla QUARANTINE'e giriş (GOODS_RECEIPT_PO, 30 KGM) | 201 |
| Hold listesi `item.skuLevel = Item` (1.1.0) | 200, KRN-2026-0001 |
| `updateHold` güncel ETag ile | 200, ETag W/"1" |
| `updateHold` eski ETag ile | 409 `QUARANTINE_CONFLICT` |
| Lot bayrağı bilinmeyen kaleme `createLot` | 422 `PRODUCT_MASTER_DATA_INCOMPLETE` |
| `stockable: false` SKU'ya `createLot` | 422 `SKU_LEVEL_NOT_STOCKABLE` |
| `stockable: false` SKU'ya giriş | 422 `SKU_LEVEL_NOT_STOCKABLE` |
| AuthService `display-label` (A'nın token'ı ile B) | 200 "Jane Smith" |

**Ekran akışı** — yerleşik tarayıcı, `mod-0174-item-shots/`:

| # | adım |
|---|---|
| 01 | Lots → Yeni Lot. Ürün "RM-59B1730001 · Laktoz monohidrat", SKU seçicisinde Item seviyesi SKU |
| 02 | Lot **LKT-26A001** oluştu. SKU satırında "RM-59B1730001 · **Malzeme kalemi**", takip "Lot" |
| 03 | Hareket formu: açılış bakiyesi, Hedef stok durumu **Karantina**, lot LKT-26A001, 40 KGM → INV-2026-0000002. Seçicide stoklanamaz RM-…0004 yok |
| 04 | Karantina kuyruğu: iki Item kaydı (KRN-2026-0001 API, KRN-2026-0002 ekran) |
| 05 | Kayıt Detayı: "RM-59B1730001 · **Malzeme kalemi** · KRN-2026-0002", Kaydı oluşturan **John Doe**, geçmişte **John Doe**, "Notu düzenle" |
| 06 | Analiz notu kaydedildi: "Nem %4,2; mikrobiyoloji uygun. Spesifikasyon içinde." |
| 07 | Karar Ver → Serbest Bırak 40 → e-imza penceresi: `qd-v1` konusu (karar, kayıt, lot, depo, lokasyon, durum, satır, CoA, gerekçe) |
| 08 | Karar verenin imzası: ESIG-2026-000001, John Doe, "Serbest bırakma yetkisi", çizim |
| 09 | Onay bekliyor. İş akışı kutusunda Karar veren **John Doe** |
| 10 | B (Jane Smith) WorkCenter'da: "Karantina kararı KRN-2026-0002 · Serbest bırak" → Onayla → imza ESIG-2026-000002, "Onaylıyorum" |
| 11 | Uygulandı: Karar Verildi; QKR-2026-0001 Uygulandı; lot durumu Serbest; geçmiş "Stok hareketi uygulandı — **Jane Smith**", "Onaylandı — **Jane Smith**", "Karar onaya gönderildi — **John Doe**" |
| 12 | Karantina Ayarları: onaylayıcı aday "Kullanıcı: **Jane Smith**" (`lookups/users`) |

Ekran notları:
- Lot detayındaki **Değişiklik geçmişi** ham kullanıcı kimliği gösteriyor. Bu Lots ekranı. MOD-0174 CT kararı (FU-LOC2-04 + FU-0174-P5) "merkezi dizine kadar kimlik" diyor; bu WP'nin kapsamı karantina ekranları.
- Hareket makbuzunda lot numarası yerine lot kimliği görünüyor. Mevcut davranış, bu WP'de değişmedi.

## 8. Süreçler (PID)

**Sahibin filosu** — istek gönderilmedi, hiçbir sürece dokunulmadı.

| port | önce (17:25) | yoklama öncesi (18:12) | sonra (18:27) |
|---|---|---|---|
| 4010 | 19992 | 19992 | 19992 |
| 5000 | 17980 | 17980 | 17980 |
| 5001 | 10452 | 10452 | 10452 |
| 5056 | 16912 | 16912 | 16912 |
| 5057 | 16548 | 16548 | 16548 |
| 5058 | 3300 | 3300 | 3300 |
| 5059 | 9532 | 9532 | 9532 |
| 5060 | 36016 | 36016 | 36016 |
| 5061 | 32468 | 32468 | 32468 |
| 5066 | 12644 | 12644 | 12644 |
| 27017 | 4904 | 4904 | 4904 |

**Kendi süreçlerim:**
- 1. yığın: 7 süreç. Ayar yazımında şirket taklidi 431 döndü; node'un başlık boyutu bayrağı eksikti. Yığın kendi `stop` dosyasıyla kapatıldı.
- 2. yığın: 31044, 42268, 40680, 33344, 9360, 20132, 41360. `stop` sonrası **0 canlı**. Yığının mongod'u kapatıldı, `pw.txt` silindi.
- Test süitleri kendi EphemeralMongo replica set'lerini açıp kapattı.
- Önce-kırmızı worktree'si `git worktree remove` ile kaldırıldı.

## 9. Açık noktalar ve varsayımlar

| kimlik | konu |
|---|---|
| **O-ITEM-1** | `updateHold` geçmiş satırı yazmıyor (sahip kararı K-1). Not değişikliğinin "Karar geçmişi"nde görünmesi için QUARANTINE'in bir sonraki eklemeli sürümünde `HistoryAction` + `NoteUpdated` gerekir. Denetim satırı `inventory_audit`'te önceki/yeni değerlerle duruyor |
| **O-ITEM-2** | Kişi adları `auth.users.lookup` anahtarına bağlı. Varsayılan şablonda bu anahtar yalnız kiracı Admin'inde var. QA / karantina rollerine verilmezse ekranda "bilinmeyen kullanıcı" görünür (güvenli yön). Rol şablonu kararı → AuthService / rol sahibi |
| O-ITEM-3 | Hareket bağlantılarında `byUserName` boş kalıyor. MovementLink'te kullanıcı kimliği yok, servis doldurmuyor. Sözleşmede alan var; ileride InventoryService doldurmalı |
| O-ITEM-4 | `updateHold` tekrarı (aynı anahtar + aynı gövde) "ilk sonucu" değil kaydın **güncel** halini döndürüyor. Arada başka bir düzenleme olduysa fark görünür. Anahtar ve parmak izi denetim satırında |
| O-ITEM-5 | "Notu düzenle" kararı bekleyen kayıtta da açık. Sözleşme "kayıt açıkken" diyor; kayıt `Decided` olunca kapanıyor (ekran 11) |
| O-ITEM-6 | Gateway 5065 kırmızısı (bilinen, satın alma) |
| ASSUMPTION-0174-ITEM-01 | `stockable` alanı yok → PM1 kuralları. Ret yalnız açıkça `false` olduğunda |
| ASSUMPTION-0174-ITEM-02 | Kişi adı kaynağı AuthService `display-label`, desen A (§6). Yeni uç yok; Web'in mevcut `lookups/{kind}` ucuna `users` türü eklendi |
| ASSUMPTION-0174-ITEM-03 | Not düzenleme düğmesi yeni eylem kodu almadı. Aynı anahtarı (`quarantine.holds.review`) taşıyan `STARTREVIEW` kullanılıyor; manifest değişmedi |

**Kapananlar:** O-PM1-1 (TRACE 1.3.0 + QUARANTINE 1.1.0 `Item`), O-PM1-3 (`stockable: false`), ASSUMPTION-0173-PM1-03 (createLot 503 → 422), **FU-0175-Q5-01** (`updateHold`), **FU-0175-Q5-02** (kişi adları).

**Push öncesi:** `git fetch origin main` → `git merge origin/main --no-ff` → derleme + süitler → push yalnız `git rev-list --count HEAD..origin/main` = 0 iken.
