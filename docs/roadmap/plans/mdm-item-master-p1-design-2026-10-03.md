# Kalem / malzeme kaydı (P1) — ölçüm ve tasarım

> **Modül:** MOD-0290 (Ürün, Kalem ve SKU Ana Verisi) · **Plan:** [Ürün ana verisi tamamlama planı](mdm-product-master-completion-plan-2026-10-02.md), P1 + P2 · **İş listesi:** BL-518 (MOD-0290'ın eksik parçaları)
> **Tarih:** 2026-10-03 · **Durum:** tasarım; kod yok · **Hazırlayan:** Control Tower (salt okunur ölçüm, ikinci ölçüm alt ajan)
> **Ölçülen yer:** MDM kodu `feature/mdm/product-five-takeover` @ `96cf7d888`; plan ve iş listesi `chore/ct-round-2` @ `34bc0cc5c`; stok yapılandırması `origin/main`.
> **Sıra:** Sahip kararıyla (2026-10-03) önce yazılmış yedi parçanın kabulü biter (Global Ürün → GSKU → LSKU → ABB → Şirket Kapsamı → Marka → Bitmiş Ürün). Bu belge o sırada kalem işinin **tasarımını** hazır tutar; kod yazımı sahibin altı kararından sonra başlar.

## Bu belge ne için

Şirketin hammadde, ambalaj malzemesi, yarı mamul ve sarf malzemesini kaydedeceği yer bugün sistemde **yok**. Satın Alma, Stok ve Üretim bu kayda bağlanmak zorunda; kayıt gelmezse her biri kendi kalem tablosunu yazar ve aynı malzeme üç yerde üç farklı kimlikle yaşar. Bu belge kaydın nasıl kurulacağını, diğer modüllerin onu nasıl okuyacağını ve sahibin hangi kararları vermesi gerektiğini yazar.

## Önce bilinmesi gereken altı bulgu

1. **Kalem için dondurulmuş bir okuma sözleşmesi zaten var, ama hammaddeyi tanımıyor.** `docs/analysis/contracts/product-master-bundle.openapi.yaml` "FROZEN, sahibi MOD-0290" diyor (:10-11); stok analizi sırasında yazılmış, MDM'de hiç uygulanmamış (`/api/product-master` ne kodda ne ağ geçidinde var). SKU seviyesi yalnız `Gsku / Lsku / FinishedGood` (:135). Gerçek MDM ile dört yerde çelişiyor: durum adları (`Draft/Active/Archived` ↔ MDM'nin `Draft/PendingIdentityApproval/IdentityApproved/Retired`), kod biçimi (`GP-000123` ↔ gerçek `GP-000000000001`), adres yok, stok alanları Global Ürüne konmuş (plan Global Ürüne dokunmayı yasaklıyor). **Sonuç:** yeni sözleşme icat edilmez; bu sözleşme yalnız ekleme yapılarak v1.1'e genişletilir.
2. **Satın Alma kalem kimliğini bekliyor, ama bugün her kimliği geçerli sayıyor.** Talep, sipariş ve mal kabul satırları `ItemId` ve `UomId` tutuyor (`Requisition.cs:25-34`, `PurchaseOrder.cs:29-38`, `GoodsReceipt.cs:47-63`); doğrulayıcı boş liste döndürüyor, rastgele bir kimlik bile geçiyor (`IProductReferenceValidator.cs:22-26`). Mal kabulün SKU seviyesi listesinde hammadde yok.
3. **Ölçü birimi listesi hammaddeye yetmez; MDM'de zaten iki ayrı birim sözlüğü var.** MOD-0048 `uom` listesi sabit beş değer (C62 adet, GRM, KGM, MLT, LTR); GSKU doğrulayıcısı aynı beşini kodda da tutuyor; CRM Ürünleri bambaşka bir liste kullanıyor (`mg, g, ml, l, iu, piece, box…`). Hammadde için mg, µg, IU, m, m², rulo, kutu gerekir. Üçüncü bir sözlük açılmaz.
4. **Denetim izi kuralı (AUD-001) yeni kalem komutlarının eski yoldan yazılmasını yasaklıyor.** Mevcut ürün kimliği komutları defterde "bilinen borç" listesinde ve o liste yalnız küçülür. Kalem komutları ilk günden merkezi günlüğe yazan yolla (yol b) doğar. Kayıt "GxP kaydı" sayılırsa kayıt yazılamadığında işlem durmak zorunda; MDM'de bu yol bugün yok (Karar 3).
5. **Ürün zincirindeki onay entegrasyonu pahalı.** MOD-0023 (ortak onay motoru) entegrasyonu 43 dosya, 7.445 satır; varlık başına yaklaşık 1.200 satırlık bir işlemci. Platform'da her nesne türü için ayrı güven kaydı ve şablon gerekiyor; dev yapılandırmasında yalnız Global Ürün kayıtlı.
6. **Bitmiş Ürün bu dalda yalnız taslak olarak var ve stok özelliği taşımıyor.** Denetleyicide yalnız okuma, GSKU seçici ve taslak oluşturma var; kayıt dosyası "Gönder" ve "Emekliye ayır" eylemlerini ilan ediyor ama uçları yok. Okuma sözleşmesi bugün hiçbir Bitmiş Ürünü "stoklanabilir" gösteremez (Bitmiş Ürün kabulünde ele alınır, ürün sırasının 7. adımı).

## Ölçüm özeti

| Konu | Bulgu |
|---|---|
| Yeniden kullanılacak kalıplar | Ortak taban (`EntityBase`), tek kiracı kod defteri (`CodeReservation`: ayır → tüket → bağla, kod yeniden kullanılmaz; önek `{GP,GS,LS,FG}-{12 hane}`), durum akışı, onay bağı (`ProductIdentityWorkflowBinding`), MOD-0048 seçim kanıtı (`ReferenceCatalogSelection`) |
| Depoda kalem / malzeme varlığı | **Hiçbir dalda yok** (`git log --all`, ad ve mesaj araması). CRM'in "Product" kaydı (Model B) stok tarafından tüketilmez (DEC-INV-17) |
| Satın Alma (MOD-0141/0142/0143/0145) | Satır: `ItemId` zorunlu, "MOD-0290'dan alınır, kapalı-başarısız"; mal kabul ek olarak SKU seviyesi istiyor; bilinmeyen kalem → 422 `UNKNOWN_ITEM` |
| Stok ekibi | Kodu yok (stok dalları `origin/main`'in 0 commit önünde). Analizlerinde istedikleri: temel birim + çevrim, GTIN, raf ömrü, en az kalan raf ömrü, saklama koşulu, yeniden test süresi, lot / seri izlenir mi, malzeme türü. Kendi belgeleri: "Hammadde ayrı modül değildir; MOD-0290 kalemidir" |
| Blueprint 8.1 | MOD-0290'ın sahip olduğu nesneler arasında **kalem kayıtları** ve kalem yaşam döngüsü var; nesne anahtarı "ProductID + ItemID + SKUID" (kalem kimliği ürün kimliğinden ayrı) |
| MOD-0290 paketi | Bitmiş Ürün "korunan kimlik kaydı", stok / lot / malzeme anlamı yok; bileşim için yer tutucu alan yasak (`PACK:116`); genel `manage` izni yasak (`PACK:700`); şirket kodlama SOP'si GMG-SCM-SOP-0001 (v0.3 taslak) açıkça kapsam dışı, ayrı sözleşme istiyor |
| MDM HTTP test barındırıcısı | **Yok**; "ApiContract" testleri sahte aracı kullanıyor. GSKU düzeltme turu ilk barındırıcıyı kuracak, kalem işi onu kullanır |

Ölçülemeyenler: SOP v0.3'ün içeriği (belge depoda yok); stok ekibinin gönderilmemiş yerel işi; madde kaydını başka bir geliştiricinin yazıp yazmadığı; şirketin bugün hammadde kaydını nerede ve kaç kalemle tuttuğu.

## Tasarım

### A. Model — önerilen: A2

| Seçenek | Ne | Gelecek regresyon riski |
|---|---|---|
| A1 | Tek `Item` kümesi; Bitmiş Ürün de bir tür olur | 🔴 Bitmiş Ürün kayıtları, alt kabulü, kod öneki, onay ve denetim teslimi taşınır |
| **A2 (öneri)** | Ayrı `Item` kümesi (hammadde, ambalaj malzemesi, yarı mamul, sarf). Bitmiş Ürün olduğu gibi kalır. Tek "stoklanabilir kalem" okuma sözleşmesi ikisini aynı biçimde döner | 🟡 Sözleşmeye yeni bir SKU seviyesi değeri eklenir |
| A3 | CRM "Product"a tür eklemek | 🔴 DEC-INV-17'ye aykırı; ikinci ana veri doğar |
| A4 | Global Ürüne malzeme türü eklemek | 🔴 "Global Ürün olduğu gibi kalır" kararıyla çelişir |

Etken / yardımcı madde ayrımı kalem türü değildir; bileşim satırındaki roldür (P5). Karşılaştırma: Blueprint kalem kimliğini ürün kimliğinden ayrı sayar; SAP tek malzeme kaydında ROH / VERP / HALB / FERT türleri kullanır; Oracle tek kalem ana kaydı ve kalem sınıfları kullanır. A2 yazma tarafında ayrı kalır, okuma tarafında SAP ve Oracle gibi tek liste gösterir.

### B. İlk sürüm alanları

| Alan | Neden |
|---|---|
| Sistem kodu `IT-000000000001` (kod defteri, yeni tür, değişmez) | Diğer kodlarla aynı ad alanında çakışmaz |
| Ad (zorunlu; kiracı + tür içinde, emekli olmayanlar arasında tekil) + açıklama | Ana veride en büyük risk mükerrer malzemedir |
| Kalem türü: hammadde / ambalaj malzemesi / yarı mamul / sarf (etkinleşince kilitli) | Stok ve MRP davranışı ona göre dallanır |
| Temel ölçü birimi + MOD-0048 seçim kanıtı (etkinleşince kilitli) | SAP'de de stok oluştuktan sonra temel birim değişmez |
| Lot izlenir mi, seri izlenir mi | Stok kuralları `LOT_REQUIRED`, `SERIAL_COUNT_MISMATCH` |
| Raf ömrü (gün), yeniden test süresi, en az kalan raf ömrü | Hammadde çoğunlukla son kullanma yerine yeniden test tarihiyle izlenir |
| Saklama koşulu (yeni MOD-0048 listesi) | Dondurulmuş sözleşmede var |
| **İlk sürümde yok:** şirket kapsamı (Karar 6), madde bağı (P5), tedarikçi (Satın Alma'nın), GTIN (P6), eski kod (P10), değerleme sınıfı (stok), tehlikeli madde | — |

### C. Yaşam döngüsü ve onay — önerilen: C2

Taslak (serbest düzenlenir) → Onaya gönder → **Etkin** (MOD-0023 onayı; yapan ile onaylayan farklı) → Kullanım dışı (izinle, denetimli) → Emekli (geri dönmez). Etkin kalemde kritik alanlar kilitli; ad ve açıklama denetim iziyle düzenlenir; kritik alan düzeltmesi sonraki dilimde onaylı. Seçenekler: C1 her değişiklik onaylı (iş yaklaşık iki katı), C3 onaysız (GxP beklentisine uymaz). Onaylı spesifikasyon ve onaylı tedarikçi listesi kalemde değil, kalite modüllerinde ve Satın Alma'da durur; bu yüzden kalem için "etkinleştirme onayı + denetim izi" yeterli, elektronik imza (BL-519) gerekmez. Karşılaştırma: SAP malzeme değişikliğini değişiklik belgesiyle izler, etkinleştirme onayı kuruma göre yapılandırılır; Oracle Product Hub'da yeni kalem talebi onayı vardır.

### D. Birim çevrimi (P2)

- MOD-0048'de yeni, global, kod sahipli `item-uom` listesi: UN/ECE Rec 20 kodları (GSKU'nun beş değeri bunun alt kümesi), boyut, ondalık hassasiyet, boyutun temel birimine çarpan. Mevcut `uom` listesine dokunulmaz (GSKU'ya özel; yayın kontrolü birebir karşılaştırıyor). Risk 🟢.
- Aynı boyutta çevrim (g ↔ kg) listedeki çarpandan türetilir; kaleme özel çevrim `{alternatif birim, pay, payda}` (SAP'deki MARM gibi). Etkin kalemde çevrim eklenebilir, değiştirilemez ve silinemez; düzeltme onaylıdır.

### E. Diğer servisler için okuma sözleşmesi

`product-master-bundle` v1.1, yalnız ekleme: SKU seviyesine `Item` (hammaddede kalem kimliği = SKU kimliği, stok satırının üçlü kimliği değişmeden çalışır); `GET /items/{id}`; `/validate` `Item` kabul eder; "stoklanabilir kalemler" arama ucu (kod / ad, tür, durum, sayfalama; kalem ve Bitmiş Ürün aynı biçimde). İki yüzey: ekranlar için ağ geçidinden kullanıcı oturumuyla (`mdm.stockable-items.read`), sunucular için servisler arası `/api/internal/v1/product-master/validate` — 503 gelirse "sessizce geçer" denmez. Tüketiciler yalnız kimlik saklar. Satın Alma'nın izin verici doğrulayıcısının gerçek istemciyle değiştirilmesi Satın Alma ekibinin dosyasıdır.

### F. Ekranlar

Sayfa `ITEMS` ("Kalemler", `/MasterDataManagement/Items`); liste ortak liste bileşeniyle (`_ListShell` + `createList`; ilk MDM ürün ekranı olur); form "compact" altın referans, iki bölüm (Kimlik · Stok davranışı); yetkisiz kullanıcıya iskelet çizilmez (UAS-001); yedi dil; izinler `mdm.items.read/create/update/submit/withdraw/deactivate/retire` + `mdm.stockable-items.read` (genel `manage` yok); denetim yol b (`EntityType "Item"`); kendini kaydetme aynı modüle yeni sayfa; ağ geçidi rotası korumalı dosya, entegrasyon işi.

### G. Dilimler (yaklaşık 10 prompt)

| # | Dilim | Risk |
|---|---|---|
| G0 | Belge: kalem paketi (MOD-0290-FU03; FU01 iki ayrı pakette kullanılmış) + sözleşme v1.1 + MOD-0048 yeni listeler + veri kapsamı cümlesi + denetlenen olaylar tablosu | 🟢 |
| G1 | Sunucu çekirdeği: taslak oluştur / düzenle, kod, denetim; GSKU turunun kurduğu HTTP test barındırıcısıyla 401 / 403 / başka kiracı 404 / 201 / mükerrer 409 / hatalı birim 422 / tekrar | 🟡 ortak kod `switch`i |
| G2 | Etkinleştirme onayı (MOD-0023); LSKU kopyası yerine paylaşılan bağlantı noktası | 🟡 |
| G3 | Okuma sözleşmesi + Satın Alma'nın gerçek doğrulayıcısı (G2 ile paralel) | 🟡 |
| G4 | Ekranlar, yedi dil, menü, Ctrl+K, canlı sayfa kontrolü | 🟢 |
| G5 | Birim çevrimi (P2) | 🟢 |

### H. Modül platform bağlantıları — P1 neyi değiştirir

| # | Satır | Değişiklik |
|---|---|---|
| 1 | Katalog | Yeni modül yok; `product-item-sku-master`'a `ITEMS` sayfası |
| 2 | Alan | `MASTER-DATA-MANAGEMENT`, değişmez |
| 3 | Sayfa eylemleri | ADD_NEW, VIEW_DETAILS, EDIT, SUBMIT, WITHDRAW_APPROVAL, DEACTIVATE, RETIRE |
| 4 | İzin anahtarları | `mdm.items.*` (yedi) + `mdm.stockable-items.read`; katalog → Auth eşitlemesi |
| 5 | Denetim | Yol b; defterde yeni borç yok; K2 sınıfı Karar 3'e bağlı |
| 6 | Bildirim | Onay görevi motordan ve Görev Merkezi'nden; kalemin kendi bildirimi yok |
| 7 | Menü | Ana Veri altında görünür; `Nav.Page.ITEMS` yedi dilde; Ctrl+K otomatik |
| 8 | KVKK | Kişisel veri yok; yalnız işlemi yapanın kullanıcı kimliği |
| 9 | Kurulum | Modül hakkı olan kiracı izinleri plan eşitlemesiyle alır; dev'de onay zinciri kurulumu |
| 10 | Kendini kaydetme | Kayıt dosyasına `ITEMS`; modül sürümü 1.0.0 → 1.1.0 |

## Sahibin kararları

| # | Soru | Seçenekler | Öneri | Seçilmezse |
|---|---|---|---|---|
| 1 | Bitmiş Ürün bir kalem türü olsun mu? | (a) ayrı kalır, ortak listede "bitmiş ürün" görünür · (b) kaleme taşınır | (a) — (b) yazılmış Bitmiş Ürün kaydını yeniden yapar 🔴 | (a) ile ilerlenir; (b) ileride yine mümkün |
| 2 | **Stok ekibine:** hammaddede `skuLevel = Item` ve kalem kimliği = SKU kimliği kabul mü? Bitmiş ürünün lot / raf ömrü şimdilik "tanımsız" dönerse girişi durduracaklar mı? Geçici bir kalem tablosu yazdılar mı? | kabul · farklı öneri | kabul (yalnız ekleme; onların henüz kodu yok) | G1 başlar ama G3 dondurulamaz; beklerlerse iki ana veri doğar |
| 3 | Kalem kaydı "GxP kaydı" mı? | (a) ana veri: merkezi günlük, kayıt düşerse iş durmaz ama uyarı kalır · (b) GxP: kayıt yazılamazsa işlem durur (önce ortak iletici işi) | (a), kalite birimine sorarak | Paket yazılamaz (kural sınıfın pakette yazılmasını istiyor) |
| 4 | Onay ne zaman gerekir? | (a) yalnız etkinleştirme + kritik alan düzeltmesi · (b) her değişiklik · (c) onaysız | (a); onaycı = kalite pozisyonu | (a); onaycı pozisyon bilinmeden dev'de onay zinciri kurulamaz |
| 5 | Kalem kodu | (a) sistem kodu · (b) şirket SOP'sindeki kod · (c) ikisi: sistem kodu kalıcı, SOP kodu ayrı alan (P10) | (c); ilk sürümde yalnız (a). **SOP v0.3 belgesi paylaşılmalı** | Sistem koduyla başlanır; SOP kodu sonradan yeniden iş çıkarmadan eklenir |
| 6 | Kalem kimlere görünür? | (a) grup geneli · (b) şirket kapsamlı | (a) — stok zaten şirket bazında; (b) kapsam altyapısını genişletir | Pakete "kiracı içinde herkese açık ana veri" cümlesi yazılmadan liste yazılamaz |

## Yan bulgular (kapsam dışı)

- Bitmiş Ürün kayıt dosyası "Gönder" ve "Emekliye ayır" eylemlerini ilan ediyor ama uçları yok (`ProductItemSkuMasterManifestProvider.cs:102-103` ↔ `FinishedGoodsController.cs:20-41`); teslim denetiminde Bitmiş Ürün kod ayırma dalı yok (`AuditIntentDeliveryRepository.cs:1522-1527`). İkisi Bitmiş Ürün kabulünde (ürün sırasının 7. adımı) ele alınır.
- MOD-0290 paketine özel BL-015…BL-027 numaraları genel iş listesi numaralarıyla çakışıyor (ör. genel BL-015 = Görev Merkezi alternatif görünümler); `MOD-0290-FU01` iki ayrı pakette kullanılmış.
- `product-master-bundle` MDM'nin uygulaması ya da gözden geçirmesi olmadan "FROZEN" ilan edilmiş; MDM paketi bu sözleşmeye atıf yapmıyor.
- `origin/main`'de CRM'in derleme çıktıları izleniyor: 324 dosya, 301 MB (BL-522).

## Sonraki adım

Sahibin altı kararı + SOP v0.3 belgesi + 2. sorunun stok ekibine iletilmesi. Sonra G0 (belge dilimi) — stok ekibinin cevabı beklenmeden yazılabilir; G1 Karar 3 cevaplanınca başlar. Kod yazımı, yazılmış yedi parçanın kabulünden sonra.
