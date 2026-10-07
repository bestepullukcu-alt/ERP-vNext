---
id: MOD-0290-FU04
name: Item / Material Master
parent: MOD-0290
parent_name: Product / Item / SKU Master
domain: master-data-management
service: Diten.MdmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "YOK (S0 belge dilimi). Kod S1'den itibaren, her dilim ayrı CT prompt'u ve bu paketin `ready-for-dev` onayıyla açılır."
owner: module-pack-author
branch: docs/mdm/item-master-s0
started: 2026-10-06
revised: 2026-10-07
target: S1 sahibin/CT'nin `ready-for-dev` onayıyla
form_field_count: 11
data_classification: "Kiracı içinde herkese açık ana veri (kiracı geneli, şirket kapsamı yok); kişisel veri yok."
audit_class: "Ana veri (Karar 3a) — yol b, merkezi günlük; günlük yazılamazsa iş durmaz, uyarı kalır."
dependencies:
  - MOD-0290 (Product / Item / SKU Master — ebeveyn; kod defteri, GSKU / LSKU / Bitmiş Ürün kümeleri, durum akışı, onay bağı, denetim yolu b)
  - MOD-0023 (ortak onay motoru — etkinleştirme ve kritik alan düzeltmesi onayı; onaycı = kalite pozisyonu)
  - MOD-0288 (organizasyon, kişi ve pozisyon dizini — onaycı pozisyonla çözülür)
  - MOD-0048 (Referans Veri Yönetimi — üç yeni evrensel liste `item-uom`, `storage-condition`, `material-type`; tanım MOD-0048-FU01'de; mevcut `uom`, `pack-applicability`, `market` listelerine dokunulmaz)
  - MOD-0018 (RBAC / katalog → Auth izin eşitlemesi — yalnız tüketim)
  - MOD-0021 (merkezi denetim günlüğü — yol b iletimi)
  - MOD-0173 / MOD-0174 (Stok Defteri ve Değerleme / Lot-Parti-Seri İzleme — okuma sözleşmesinin ilk tüketicisi; emekliye ayırmada uygunluk sorgusunun sahibi; taahhütleri §7.2)
  - MOD-0175 / MOD-0176 / MOD-0178 (Karantina-Bloke Stok / Son Kullanma-FEFO / Yerleştirme-Toplama-Paketleme — sonraki alanların ve ambalaj hiyerarşisinin sahipleri)
  - MOD-0193 (Ürün Ağacı ve Rotalar, BOM — parti reçetesinin sahibi; MDM kimliğine referansla bağlanır)
  - MOD-0141 / MOD-0142 / MOD-0143 / MOD-0145 (Satın Alma — okuma sözleşmesinin tüketicileri; doğrulayıcı değişikliği onların dosyası)
  - DEV-0001 (Golden Reference Compact — ekran şablonu)
---

# MOD-0290-FU04 — Item / Material Master (kalem / malzeme ana kaydı)

> **DRAFT · S0 belge dilimi · KOD YOK.** İlk taslak 2026-10-06 (G0); bu revizyon 2026-10-07 (S0) stok ekibiyle
> varılan uzlaşmayı işler ([ürün ana verisi tamamlama planı §4a](../../../../docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md)).
>
> Şirketin etkin madde, yardımcı madde, hammadde, ambalaj malzemesi ve ara ürünü kaydedeceği yer bugün sistemde yok.
> Satın Alma, Stok ve Üretim bu kayda bağlanmak zorunda; kayıt gelmezse her biri kendi kalem tablosunu yazar ve aynı
> malzeme üç yerde üç kimlikle yaşar. Bu paket P1 (kalem kaydı) tasarımını
> ([mdm-item-master-p1-design-2026-10-03.md](../../../../docs/roadmap/plans/mdm-item-master-p1-design-2026-10-03.md),
> öneriler A2 + C2) ve stok paketini (plan §4a, dilimler S0–S7) yetkili bir Module Pack taslağına çevirir.
>
> **DCP-002 kimlik kapısı (modül kimliği kanonikleştirme) — PASS (2026-10-06):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU04 --name "Item / Material Master" --parent MOD-0290`
> → `OK MOD-0290-FU04: proven against Blueprint/registry.`
> **Tüm dallarda ölçüldü:** `git log --all -S "MOD-0290-FU0{4..10}"` = 0 commit; her dalda `git grep` = 0.
> **Neden FU03 değil:** `MOD-0290-FU03` başka dallarda *Product Legal Entity Scope Assignment* paketidir (MOD-0018-FU21 /
> FU22 onu tüketici olarak anar). Bu dalın registry'sinde satırı olmadığı için doğrulayıcı FU03'e de "OK" der.
> Doğrulayıcı yalnız bu dalın registry'sini okur; numara tüm dallarda ayrıca ölçülmelidir (§25 F8).

## 0. Kararlar

### 0.1 Sahibin devrettiği, CT'nin verdiği kararlar (2026-10-06)

| # | Soru | Karar | Bu pakete etkisi |
|---|---|---|---|
| 1 | Bitmiş Ürün bir kalem türü mü? | **(a)** Bitmiş Ürün (FG) ayrı kalır | Ayrı `Item` kümesi (A2). FG ayrı kayıt olarak kalır; S3 ona yalnız stok davranışı alanları ekler (§4.2). Okuma sözleşmesi ikisini tek biçimde döner (`itemKind`) |
| 2 | Stok ekibi: `skuLevel = Item`, kalem kimliği = SKU kimliği? | **KAPANDI — stok ekibi kabul etti (2026-10-07)** | §0.2. Sözleşme v1.1'in dondurulması artık yalnız CT kararını bekler |
| 3 | GxP sınıfı | **(a) ana veri** | Merkezi günlük (yol b); kayıt yazılamazsa iş durmaz, uyarı kalır (§21) |
| 4 | Onay ne zaman? | **(a)** yalnız etkinleştirme + kritik alan düzeltmesi | Onaycı = **kalite pozisyonu**, MOD-0023 üzerinden pozisyonla (§2.6) |
| 5 | Kalem kodu | **(c)** sistem kodu kalıcı, SOP kodu ayrı alan | v1'de yalnız sistem kodu `IT-{12 hane}` — ürün kimliği ailesi (RCS-001 §4, §22). Şirket kodlama SOP'si GMG-SCM-SOP-0001 v0.3 sahipten gelince SOP kodu ayrı alan olarak eklenir (P10 — eski kodlar ve veri taşıma paketi) |
| 6 | Görünürlük | **(a) kiracı geneli** | **"Kalem, kiracı içinde herkese açık ana veridir; şirket (tüzel kişi) kapsamı yoktur."** Stok ekibi de onayladı (soru S6) |

### 0.2 Stok ekibiyle uzlaşma (2026-10-07) — plan §4a

Kaynak: plan §4a ve stok ekibinin 2026-10-07 cevabı (sahip sohbete yapıştırdı). Ekip önerilerimizin hepsini kabul etti.
**Numara notu:** bu tablodaki S1–S8 stok ekibine sorulan **soruların** numarasıdır; bu pakette "soru S5" ya da
"cevap S5" diye geçer. Yalın "S0–S7" ise **dilimlerdir** (§20).

| Soru | Konu | Uzlaşma | Bu pakette |
|---|---|---|---|
| S1 | Kimlik | Dışarıya **tek** okuma sözleşmesi. Mevcut beş v1 yolu kalemi de cevaplar; yeni yol eklenebilir, eskilerin yerine geçmez. v1.1 (yalnız ekleme): `ItemKind` (`GlobalProduct` \| `Item`), `SkuLevel`'e `Item`, kalemde `materialType`. Kalemde kalem kimliği = SKU kimliği; SKU → kalem çözümlemesi (D-SKU-LINK) kalem için kendisini döndürür | §16 |
| S2 | Bileşim | Madde kaydı + ruhsat / etiket bileşimi MDM'de (Ürün Tanımı Sürümüne bağlı, P5 — madde + bileşim paketi); parti reçetesi MOD-0193 BOM'da (MVP-3 üretim), MDM kimliğine referansla | §2.4 |
| S3 | Alanlar | Kalem / GSKU / Bitmiş Ürün alan tablosu | §4.1, §4.2 |
| S4 | Sözleşme ekleri | Stoklanabilir kalem / SKU araması (sayfalı, `q`); toplu SKU okuma; olay MVP-1'de yok; MDM 64 KB istek başlığı kabul eder (belirteç ~35 KB) | §16, §8.5 |
| S5 | Yaşam döngüsü | Taslak hareket yok · Etkin evet · Kullanım dışı yeni giriş yok, mevcut stok çıkabilir, hurda / iade · Emekli hareket yok · her durumda ters kayıt · stoğu sıfır olmayan kalem emekliye ayrılamaz (stok uygunluk sorgusu) · sözleşmeye `Deactivated` ve `Retired` eklemeyle girer | §8 |
| S6 | Görünürlük | Kiracı geneli | §2.5 |
| S7 | Zaman / geçici kod | Stok hemen başlıyor (dondurulmuş v1'in Prism taklidine karşı); geçici kalem tablosu yok; taşınacak veri yok | §7.2 |
| S8 | Ambalaj hiyerarşisi | MVP-1'de gerekmez; kutu → koli → palet MOD-0178'de (P9 — ambalaj hiyerarşisi paketi). MVP-1 için: satış kutusu GTIN'i + temel birim ↔ kutu ↔ koli çevrimleri (`getSkuUom`) | §4.2, §16 |
| D-1 | GTIN | Bitmiş Ürün satış kutusu düzeyinde gerekli (İTS karekodundaki (01)); stoklanan SKU'dan okunur | §4.2 |
| D-2 | Bitmiş Ürünün üst kaydı | GSKU (bugünkü kod `FinishedGood.GskuId`) | §4.2 |

### 0.3 G0'dan (2026-10-06) değişenler

| G0 taslağı | Şimdi | Neden |
|---|---|---|
| Kalem türü `ItemType`: `RAW_MATERIAL`, `PACKAGING_MATERIAL`, `SEMI_FINISHED`, `CONSUMABLE` | `MaterialType`, altı değer; değer eşlemesi §16.2 | soru S3 |
| "Etken / yardımcı ayrımı tür değil, bileşimdeki roldür" | `ACTIVE_INGREDIENT` ve `EXCIPIENT` malzeme türüdür; bileşimdeki rol (P5) ayrıca kalır | soru S3 |
| İç durum `Inactive` | `Deactivated` | Plan §4a'nın durum adı; iç ve dış ad aynı |
| Sözleşmede `PendingActivation` | Sözleşmede yok; iç durumdur, `Draft` ya da `Deactivated` olarak yayımlanır (§16.3) | Plan §4a yalnız `Deactivated` + `Retired` ekler |
| Kalemde `SerialTracked` | Yok; sözleşme kalem için `serialControlled: false` döner | soru S3 "Kalem: gereksiz" |
| İç adlar `LotTracked` / `SerialTracked` | `LotControlled` / `SerialControlled` (sözleşmedeki adla aynı) | Tek ad |
| Tek saklama koşulu | Çoklu `StorageConditionCodes` | soru S3 |
| `Stockable` yok | `Stockable` (bool) | soru S3 |
| "FG'ye dokunulmaz" | S3 GSKU / LSKU / FG'ye stok davranışı alanları ekler | soru S3 |
| GTIN kapsam dışı (P6 — tanımlayıcılar paketi) | Bitmiş Ürün satış kutusu GTIN'i kapsamda; kalem GTIN'i sonra | D-1 |
| `/items/{itemId}` + `MaterialItemRef` asıl okuma | Asıl okuma mevcut beş v1 yolu; `/items/{itemId}` kısayol olarak kalır, şeması `ProductRef` | soru S1 |
| Emekliye ayırma serbest | Stok uygunluk denetimi; ulaşılamazsa ret (§8.3) | soru S5 |
| Birim çevrimi son dilim (G5) | Alan S1'de, doğrulamalar S6'da; GSKU ve Bitmiş Ürün de çevrim taşır | soru S3 |
| Dilimler G0–G5 | S0–S7 (§20) | Plan §4a |

## 1. Module Summary

`Item`, MOD-0290'ın Blueprint 8.1'de sahip olduğu **kalem kayıtları ve kalem yaşam döngüsünü** taşıyan yeni kümedir
(nesne anahtarı "ProductID + ItemID + SKUID" — kalem kimliği ürün kimliğinden ayrı). Paketin üç parçası var:

1. **Kalem kaydı** (S1, S2) — stoklanan malzemenin ana kaydı, onaylı etkinleştirme, yaşam döngüsü.
2. **Stok davranışı alanları** (S3) — GSKU / LSKU / Bitmiş Ürün'e temel birim, çevrim, raf ömrü, saklama, lot, seri,
   stoklanabilir ve Bitmiş Ürün satış kutusu GTIN'i.
3. **Okuma sözleşmesi** `product-master-bundle` v1.1 (S4) — kalem ve ürün SKU'ları tek sözleşmeden.

Malzeme türleri (`MaterialType`, soru S3; değerler sözleşme ve MOD-0048 `material-type` listesiyle birebir):

| Kod | Anlam | SAP karşılığı |
|---|---|---|
| `ACTIVE_INGREDIENT` | Etkin madde (API) | ROH (hammadde) |
| `EXCIPIENT` | Yardımcı madde | ROH |
| `RAW_MATERIAL` | Diğer hammadde (çözücü, işlem yardımcısı …) | ROH |
| `PACKAGING_PRIMARY` | Birincil ambalaj — ürünle temas eden (blister folyo, şişe, kapak) | VERP (ambalaj) |
| `PACKAGING_SECONDARY` | İkincil ambalaj — karton kutu, prospektüs, etiket | VERP |
| `INTERMEDIATE` | Ara ürün / yarı mamul (bulk, granül) | HALB (yarı mamul) |

Mamul (SAP FERT) kalem türü **değildir**: mamul = Global Ürün zinciri (`itemKind: GlobalProduct`); stoklanan mamul
MOD-0290'ın GSKU / LSKU / Bitmiş Ürün kaydıdır (Karar 1).

## 2. Ownership and Boundaries

### 2.1 SoR kararı

- Kalem kaydının ve stok davranışı alanlarının tek kaynağı (SoR) MOD-0290 / `Diten.MdmService`'tir. Satın Alma, Stok
  ve Üretim **yalnız kimlik saklar ve okuma sözleşmesiyle okur**; yerel kalem tablosu açılmaz, ad / kod / alan
  kopyalanmaz (ekranda göstermek için tutulan kopya, ana kayıt değişince eski sayılır).
- CRM'in "Product" kaydı (Model B) kalem değildir ve stok tarafından tüketilmez (DEC-INV-17 — stok ekibinin "Model A
  kanonik" kararı).
- Yazma tarafı ayrıdır (A2: `Item` ≠ `FinishedGood` ≠ `GlobalProduct`); okuma tarafı SAP ve Oracle gibi **tek
  sözleşme** gösterir (§16).
- Lot, seri, bakiye ve hareket stoktadır (MOD-0173 / MOD-0174); kalemin tanımı MDM'dedir.

### 2.2 In-scope (S1–S7)

- `Item` kümesi: taslak oluştur / düzenle, onaya gönder, geri çek, etkinleştirme kararını uygula, kullanım dışı
  bırak, yeniden etkinleştir, emekliye ayır (stok uygunluk denetimiyle); sonraki dilimde onaylı kritik alan düzeltmesi.
- Kod defterinde yeni tür `IT` (ayır → tüket → bağla; kod yeniden kullanılmaz; §22).
- Stok davranışı alanları: kalemde (S1) ve GSKU / LSKU / Bitmiş Ürün'de (S3); Bitmiş Ürün satış kutusu GTIN'i (S3).
- Birim çevrimleri: kalem, GSKU, Bitmiş Ürün (alan S1 / S3, doğrulama S6).
- Okuma sözleşmesi `product-master-bundle` v1.1 (S4).
- MOD-0048 üç yeni evrensel liste: `item-uom`, `storage-condition`, `material-type` (tanım MOD-0048-FU01 §4, tohum S1).
- **Kalemler** sayfası + GSKU / Bitmiş Ürün sayfalarında "Stok davranışı" bölümü (S5).
- Dev verisi adımları ve stok ekibine "hazır" bildirimi (S7).

### 2.3 Out-of-scope / yetkisiz

- Bitmiş Ürünü kaleme taşımak (Karar 1b reddedildi).
- Şirket (tüzel kişi) kapsamı (Karar 6).
- Madde kaydı ve ruhsat / etiket bileşimi (P5 — MDM'de ama ayrı paket); parti reçetesi (MOD-0193 BOM).
- Kalem GTIN'i (sonra); LSKU GTIN'i (v1.1'de null — CT kararı O-6); koli / palet GTIN'i ve ambalaj hiyerarşisi (MOD-0178, P9 — ambalaj
  hiyerarşisi paketi).
- Tehlike sınıfı, "CoA zorunlu mu", girişte varsayılan karantina — sonra (MOD-0175).
- GSKU seri takibi — sonra.
- Lot, seri, bakiye, hareket, rezervasyon (stok); tedarikçi ve onaylı tedarikçi listesi (Satın Alma); spesifikasyon
  (kalite); değerleme sınıfı (stok).
- Olay (event) yayını — MVP-1'de gerekmez (stok her harekette canlı okur).
- Elektronik imza (BL-519 — ortak elektronik imza işi; etkinleştirme onayı + denetim izi yeterli).
- SOP-kontrollü malzeme kodu üretimi: ebeveyn paketin **GMG-SCM-SOP-0001 delivery guard** kuralı aynen geçerlidir
  (DCP-005 — yönetişim çekirdeği onayı + yetkili sahip paketi + açık kod başlatma izni). Sistem kodu (`CanonicalCode`)
  aşırı yüklenmez.
- Toplu eski veri aktarımı (P10 — eski kodlar ve veri taşıma); genel `manage` izni; `delete` / `bulk-delete`.

### 2.4 Bileşim ayrımı (soru S2)

| Ne | Nerede | Bağ |
|---|---|---|
| Madde kaydı (ör. paracetamol) | MDM, P5 (madde + bileşim paketi) | — |
| Ruhsat / etiket bileşimi (birim başına etkin madde, güç) | MDM, P5; Ürün Tanımı Sürümüne bağlı | madde kimliği |
| Parti reçetesi (parti başına miktarlar) | MOD-0193 BOM (MVP-3 üretim) | MDM kalem / madde **kimliğine** referans; ad / kod kopyalanmaz |
| Kalem (stoklanan malzeme, ör. "Amoksisilin trihidrat") | bu paket | — |

Kalem (stoklanan, satın alınan, lotu izlenen malzeme) ile madde (bileşimdeki kavramsal madde) ayrı kayıtlardır; kalem →
madde bağı P5'te açılır, bu pakette alan yoktur. BOM'un sahibi kişi / dal MVP-3'ten sorulacak (O-19).

### 2.5 Görünürlük

**Kalem, kiracı içinde herkese açık ana veridir; şirket (tüzel kişi) kapsamı yoktur.** Stok bakiyesi ve hareketi zaten
şirket bazında tutulur (DEC-INV-18 — stok bakiyesi tüzel kişi başına). Ürün Tüzel Kişi Kapsamı (MOD-0290-FU03) kaleme
uygulanmaz. GSKU / LSKU / Bitmiş Ürün'e eklenen stok alanları satırın bugünkü kapsamına uyar; S3 kapsamı değiştirmez.

### 2.6 Onay bağlantısı (MOD-0023)

| Konu | Kural |
|---|---|
| Ne onaylanır | Kalemde etkinleştirme (Taslak → Etkin), **yeniden etkinleştirme** (Kullanım dışı → Etkin) ve etkin kalemde **kritik alan düzeltmesi** (sonraki dilim). Etkin kalemde yalnız ad ve açıklama onaysız, denetim iziyle düzenlenir; diğer bütün alanlar kritiktir (tasarım §C). Boş alanın ilk doldurulması onaysız, denetimle; onaylı GSKU / Bitmiş Ürün'de ve etkin kalemde dolu değerin değiştirilmesi MOD-0023 onayıyla (kritik alan düzeltmesi) — CT kararı O-8 |
| Onaycı | **Kalite pozisyonu** — MOD-0023 adayı pozisyonla verir, motor pozisyonu kişilere çözer, ilk karar veren karar verir (GP-1b kalıbı — Global Ürün onaycısının pozisyonla çözülmesi) |
| Yapan ≠ onaylayan | Kendi kaydını onaylayan kişinin kararı uygulanmaz; `platform_admin` atlatması bu alan kuralını geçemez |
| Bağlantı noktası | GP / GSKU kalıbı (`ProductIdentityWorkflowBinding`, güvenli başlatma, iz arama); S2'de LSKU kopyası yerine **paylaşılan** bağlantı noktası |
| Kalıcı kanıt | Karar kanıtı (aktör, zaman, görev, geçiş sırası) kalemle birlikte saklanır ve denetime iletilir |

## 3. Owned Objects

| Kök | Sorumluluk | Depolama |
|---|---|---|
| `Item` | Kalem kimliği, malzeme türü, stok davranışı alanları, birim çevrimleri, yaşam döngüsü, onay bağı, denetim niyetleri | `mdm_items` (kiracı + `IsDeleted` filtresi) |
| `ItemWorkflowOperation` | Etkinleştirme / düzeltme onay isteğinin dayanıklı işlemi (GSKU / GP kalıbı) | `mdm_item_workflow_operations` |
| `ItemUomConversion` | Kaleme özel yönlü çevrim `{FromUom, ToUom, Numerator, Denominator}` (SAP MARM gibi) | `Item` içinde gömülü liste |
| `CodeReservation` (tür `IT`) | Kod ayırma / tüketme / bağlama | mevcut kod defteri |
| GSKU / LSKU / Bitmiş Ürün stok davranışı (S3) | Ebeveyn MOD-0290 kümelerine eklenen alanlar; küme sahipliği ebeveynde kalır | mevcut koleksiyonlar, gömülü `StockBehaviour` nesnesi (öneri; S3'te ölçülür) |

**Yazma komutları** (hepsi §21'de): kalem — `CreateItemCommand`, `UpdateItemCommand`, `SubmitItemCommand`,
`WithdrawItemApprovalCommand`, `ApplyItemActivationDecisionCommand` (işçi), `DeactivateItemCommand`,
`RequestItemReactivationCommand`, `RetireItemCommand`, `AddItemUomConversionCommand` (S6, etkin kalem),
`RequestItemCriticalCorrectionCommand` ve `ApplyItemCriticalCorrectionDecisionCommand` (işçi) (sonraki dilim);
S3 — `SetGskuStockBehaviourCommand`, `SetLskuStockBehaviourCommand`, `SetFinishedGoodStockBehaviourCommand`,
`AssignFinishedGoodGtinCommand`.

**Sorgular:** ekran — `GetItemListQuery`, `GetItemByIdQuery`; sözleşme (S4) — `GetProductMasterProductQuery`,
`GetProductMasterSkuQuery`, `ResolveSkuProductQuery`, `GetSkuUomQuery`, `ValidateProductMasterRefsQuery`,
`SearchStockableItemsQuery`, `BulkReadSkusQuery`.

**Uçlar:** ekran `/api/mdm/items/*`; sözleşme `/api/product-master/*`; servisler arası
`/api/internal/v1/product-master/validate`. **Sayfa:** `ITEMS` ("Kalemler", `/MasterDataManagement/Items`).
**İzinler:** §14.

## 4. Entity Fields

### 4.1 Kalem (`Item : EntityBase`, kiracı kapsamlı)

`form_field_count = 11` ("Formda" sütunu). Etkin kalemde yalnız `Name` ve `Description` onaysız düzenlenir; "kilitli
(kritik)" alanlar yalnız onaylı kritik alan düzeltmesiyle değişir (tasarım §C — raf ömrü, yeniden test ve saklama
koşulu GxP açısından stok davranışını değiştirir). Boş kalmış kilitli alanın **ilk doldurulması** onaysızdır ve denetim
kaydı bırakır; dolu değerin değiştirilmesi MOD-0023 onaylı kritik alan düzeltmesidir ve o dilim gelene kadar
reddedilir (CT kararı O-8, 2026-10-07).

| Alan | Tür | Formda | Etkinleşince | Not |
|---|---|---|---|---|
| `CanonicalCode` | string `IT-000000000003` | hayır (sistem) | değişmez | §22 |
| `Name` | string 1–200 | evet | denetimle düzenlenir | Kiracı + malzeme türü içinde, emekli olmayanlar arasında tekil (normalize anahtar) |
| `NameNormalized` | string | hayır | — | Tekillik anahtarı (GP ad kuralı) |
| `Description` | string 0–2000 | evet | denetimle düzenlenir | |
| `MaterialType` | enum, altı değer (§1) | evet | **kilitli** | Stok ve MRP davranışı buna göre dallanır |
| `BaseUomCode` + `BaseUomSelection` | MOD-0048 `item-uom` + seçim kanıtı (`ReferenceCatalogSelection`) | evet | **kilitli** | Stok her zaman temel birimde tutulur; SAP'de de stok oluştuktan sonra temel birim değişmez |
| `UomConversions` | liste `{FromUom, ToUom, Numerator, Denominator}`; pay / payda ondalık metin | evet | eklenir; değiştirilmez, silinmez | §4.4 (ör. kg ↔ g, L ↔ mL türetilir; çapraz boyut elle) |
| `StorageConditionCodes` + seçim kanıtları | MOD-0048 `storage-condition`, **çoklu** | evet | **kilitli** | En çok bir sıcaklık koşulu (O-2) |
| `LotControlled` | bool | evet | **kilitli** | Stok kuralı `LOT_REQUIRED` |
| `Stockable` | bool | evet | **kilitli** | false → aramada görünmez ve stok yeni hareket kabul etmez, ters kayıt hariç (O-26); zorunlu stok alanları eksikken true olamaz (O-8); ambalaj dışı kalemde boş `StorageConditionCodes` ile true olamaz (O-25) |
| `ShelfLifeDays` | int? 1–3650 | evet | **kilitli** | SKT ve FEFO (MOD-0176) |
| `RetestDays` | int? 1–3650 | evet | **kilitli** | Yeniden test kuyruğu (MOD-0175); hammadde çoğunlukla yeniden test tarihiyle izlenir |
| `MinRemainingShelfLifeDays` | int? 0–3650, ≤ `ShelfLifeDays` | evet | **kilitli** | Uzlaşma tablosunda yok; isteğe bağlı kalır, stok ekibine soruldu (CT kararı O-7) |
| `LifecycleStatus` | `Draft` · `PendingActivation` · `Active` · `Deactivated` · `Retired` | hayır | — | §8 |
| `WorkflowBinding` | onay bağı (`ActivationKind = Activation \| Reactivation`) | hayır | — | GP kalıbı |
| `AuditIntents` / `AuditIntentReceipts` | denetim niyetleri ve makbuzları | hayır | — | Yol b |
| `Version` | int | hayır | — | İyimser kilit (CAS) |

**Sözleşmede kalem için sabit:** `serialControlled = false` = uygulanmaz (seri takibi kalemde yok — soru S3; O-27), `gtin = null`
(sonra). **Taslak / eksik kalem (O-25):** zorunlu stok alanı dolmamışsa `getSku`'da alan null, `getProduct`'ta alan yok döner
ve `stockable` false'tur; `ShelfLifeDays` / `RetestDays` dolu ya da null olabilir (O-9). **Yetkili okuma (O-27):** stok
davranışı için `getSku(level=Item)`; `getProduct`'taki kalem alanları aynı değerleri taşır.
**v1'de yok:** şirket kapsamı, madde bağı (P5), tedarikçi, GTIN, SOP kodu, değerleme sınıfı, tehlike sınıfı,
"CoA zorunlu mu", girişte varsayılan karantina (MOD-0175, sonra).

**Eski belge kuralı (GP / GSKU dersi):** yeni alan ilk günden vardır; sonradan eklenecek her alan için koşul
`Eq(false)` / `Eq(null)` / `Eq(0)` eksik alanı da eşlemelidir ve test alansız bir belgeyle yapılır. S3'te GSKU / LSKU /
Bitmiş Ürün'ün **mevcut** belgeleri bu alanları taşımaz — aynı kural orada da geçerlidir.

### 4.2 GSKU / LSKU / Bitmiş Ürün stok davranışı (S3)

Kaynak: plan §4a, soru S3 tablosu. "gerekli" = alan modelde var ve `Stockable = true` olabilmesi için dolu olmalı (O-8).
Eksik zorunlu alan sözleşmede (`getSku`, yetkili okuma — O-27) null döner ve kayıt `stockable: false` yayımlanır (O-25);
`stockable: false` kayıtta stok yeni hareket kabul etmez, ters kayıt hariç (O-26).

| Alan | GSKU | LSKU | Bitmiş Ürün | Kural |
|---|---|---|---|---|
| `BaseUomCode` (`item-uom`) | gerekli | GSKU'dan (O-3) | gerekli | Bitmiş Ürünün temel birimi üst GSKU'nunkiyle aynı (CT kararı O-14) |
| Birim çevrimleri | gerekli: kutu ↔ adet | GSKU'dan (O-3) | gerekli: koli ↔ kutu | §4.4. GSKU'da kutu ↔ adet mevcut `PackQuantity` + `PackUomCode`'dan türetilir, yeniden girilmez (CT kararı O-14) |
| `ShelfLifeDays` | gerekli (ruhsatlı raf ömrü) | GSKU'dan (O-3) | gerekli; boşsa üst GSKU'dan okunur | Bitmiş Ürün boş bırakırsa değer okuma anında üst GSKU'dan gelir (D-2), kopyalanmaz; sözleşme `shelfLifeDaysSource = Gsku` döner. Kendi değeri GSKU'nunkini aşamaz; doğrulamayla zorlanır (CT kararı O-15) |
| `StorageConditionCodes` (çoklu) | gerekli | GSKU'dan (O-3) | gerekli | En çok bir sıcaklık koşulu (O-2) |
| `LotControlled` | gerekli | GSKU'dan (O-3) | gerekli | Ürün / kalem düzeyinde yeterli (stok ekibi) |
| `SerialControlled` | **sonra** (v1.1'de yok) | gerekli | gerekli | Pazara bağlı (İTS) → SKU düzeyinde; v1.0'da ürün düzeyindeydi, v1.1 SKU düzeyinde ek alan açar |
| `Stockable` | gerekli | GSKU'dan (O-3) | gerekli | |
| `Gtin` | — (gereksiz) | null (O-6) | **gerekli** | Satış kutusu GTIN-14 (İTS karekodundaki (01)); GS1 kontrol hanesi doğru; kiracıda emekli olmayan Bitmiş Ürünler arasında tekil |
| Sonra | koli / palet GTIN'i, ambalaj hiyerarşisi (MOD-0178, P9) | | | |

**Ebeveyn paket revize edildi (O-16, 2026-10-07):** MOD-0290 paketinin §4 GSKU ve Bitmiş Ürün tablolarında GTIN'i
"Deferred/prohibited" (paket-yerel BL-022) sayan satırlar silinmedi; "superseded 2026-10-07" işaretlendi ve bu pakete
(sözleşme v1.1) yönlendirildi. Revizyon yalnız ekleme; ebeveyn paketin başka hiçbir kuralı değişmedi (CT kararı;
XMC-001 — başka modülde düzeltme onayı).

**Düzenleme yolu:** stok davranışı kendi komutu ve kendi izniyle düzenlenir (ürün kimliği formuna eklenmez — O-12).
Onaylı (`IdentityApproved`) kayıtlarda alanlar bugün boştur. Boş alanın ilk doldurulması onaysızdır ve denetim kaydı
bırakır; dolu değerin değiştirilmesi MOD-0023 onaylı kritik alan düzeltmesidir, o dilim gelene kadar reddedilir
(409 `STOCK_BEHAVIOUR_FIELD_LOCKED`) — CT kararı O-8. LSKU yalnız `SerialControlled`'ı kendisi taşır; diğer alanları
okuma anında üst GSKU'dan gelir (CT kararı O-3).

### 4.3 MOD-0048 listeleri

Liste tanımları (değerler, öznitelikler, yaşam döngüsü) liste sahibinin paketindedir:
[MOD-0048-FU01 §4 "Universal Item Reference Lists for MOD-0290-FU04"](../../platform-shared-services/module-packs/MOD-0048-FU01-enterprise-business-reference-data-provider.md).
Neden orası: MOD-0048 kod setlerinin ve yaşam döngüsünün sahibidir (Master 8.1 `SoR_Map`); GSKU'nun `uom` /
`pack-applicability` listeleri ve LSKU'nun `market` listesi de aynı pakette, aynı "evrensel, kod sahipli" kalıpla
tanımlı. Yeni dosya açılmadı.

| Liste | Seçim | v1 değerleri | Tüketen |
|---|---|---|---|
| `item-uom` | tek | Uzlaşılan en az küme: `C62` adet, `MGM` mg, `GRM` g, `KGM` kg, `MLT` mL, `LTR` L + kutu `XBX`, koli `XCS`. S1 ölçümü (UN/ECE Rec 20 Rev 11e, 2026-10-07): `MC` µg, `MTR` m, `MTK` m² geçerli; rulo = `XRO`. Uluslararası ünite için tek birim kodu **yok** (yalnız `HIU` = 100 IU) → stok + kalite birimine soru (CT iletti): `HIU` mı, liste dışı yerel kod mu (O-21) | Kalem, GSKU, LSKU, Bitmiş Ürün temel birimi ve çevrimleri |
| `storage-condition` | **çoklu** | `AMBIENT_15_25` (15–25°C), `COOL_2_8` (2–8°C), `FROZEN_BELOW_MINUS_20` (≤ −20°C), `PROTECT_FROM_LIGHT`, `PROTECT_FROM_MOISTURE` | Kalem, GSKU, LSKU, Bitmiş Ürün |
| `material-type` | tek | Altı değer (§1) | Kalem |

- Mevcut `uom` listesine (GSKU `PackUomCode`; beş değer), `pack-applicability` ve `market` listelerine dokunulmaz.
  `item-uom`, `uom`'un beş değerini **aynı boyut ve hassasiyetle** içerir; üçüncü bir birim sözlüğü açılmaz.
- G0 "kalem türü MOD-0048 listesi değildir" diyordu; uzlaşma (plan §4a S0) `material-type`'ı MOD-0048'e koyar.
  Kod kümesi yine kapalıdır: değerler sözleşmedeki `MaterialType` ile birebir aynıdır, yeni değer sözleşme sürümü ister.
- Ekranda yedi dilde etiket: MOD-0048 görünen adı tek dillidir; etiketin kaynağı S5'te ölçülür (O-22).

### 4.4 Birim çevrimi kuralları (alan S1 / S3, doğrulama S6)

1. **Yön:** 1 `FromUom` = `Numerator` / `Denominator` `ToUom`. Pay ve payda ondalık metindir (`"0.001"`), sıfırdan
   büyüktür, MDM yuvarlamaz ve girildiği gibi saklar (üs gösterimi ve baştaki `+` reddedilir).
2. **Zincir:** kayıtta kullanılan her birim temel birime **en çok 4 adımda** ulaşır (stok 4 adıma kadar zincirler);
   ulaşmayan çevrim kaydedilmez.
3. **Tek kayıt:** bir birim çifti bir kez yayımlanır; ters yön saklanmaz, tüketici kesin kesirle (payda / pay) türetir.
4. **Döngü yok:** `FromUom ≠ ToUom`; döngü oluşturan çevrim reddedilir.
5. **Aynı boyut:** KGM ↔ GRM, LTR ↔ MLT gibi çevrimler MOD-0048 `item-uom` çarpanından türetilir, elle girilmez,
   `getSkuUom`'da açıkça döner; çarpanla çelişen elle girilmiş çevrim reddedilir.
6. **Çapraz boyut / ambalaj:** `XBX` ↔ `C62`, `XCS` ↔ `XBX` gibi çevrimler kayda özeldir ve elle girilir (GSKU'da
   kutu ↔ adet için O-14).
7. **Bitmiş Ürün:** `getSkuUom` Bitmiş Ürünün kendi çevrimlerini (koli ↔ kutu) ve üst GSKU'nun kutu ↔ adet çevrimini
   birlikte döner; temel birim ↔ kutu ↔ koli tek çağrıda okunur (soru S8).
8. **Etkin kayıtta:** çevrim eklenir; değiştirme / silme onaylı düzeltmedir (sonraki dilim).
9. **Hassasiyet:** `item-uom` `MaximumDecimalPrecision` stoktaki miktarları sınırlar, çevrim çarpanlarını değil.

## 5. Repo Scope (S1–S7; S0'da yalnız belgeler)

| Dilim | Yollar |
|---|---|
| S0 | bu paket · `docs/analysis/contracts/product-master-bundle.openapi.yaml` (v1.1, yalnız ekleme) · `execution/domains/platform-shared-services/module-packs/MOD-0048-FU01-enterprise-business-reference-data-provider.md` (yalnız yeni planlama bölümü) · `execution/domains/master-data-management/domain-config.md` (bağlantı cümlesi) |
| S1 | `services/Diten.MdmService/src/**/Features/ItemMaster/**` (CT kararı 5: kapsam envanteri dışı ad alanı), `Domain/Entities/Item.cs`, `Domain/Enums/CodeBearingEntityType.cs` (+`Item`), `Persistence/Repositories/CodeReservationRepository.cs` (önek `switch`ine `IT` — ortak kod), kalem deposu + dizinler, stok uygunluk istemcisi (`Infrastructure`), testler (`MdmHttpHost` gerçek Mongo barındırıcısı; BL-527 — MDM testlerinin ortak test veritabanını paylaşması sorunu, geçici mongod ile); Platform: MOD-0048-FU01'in adlı adımındaki üç liste (FU01'de ayrı yetki) |
| S2 | onay işlemcisi + paylaşılan bağlantı noktası, Platform onay şablonu / güven kaydı (dev) |
| S3 | `services/Diten.MdmService/src/**/Features/ProductItemSkuMaster/**` içinde GSKU / LSKU / Bitmiş Ürün stok davranışı komutları, alanlar ve testler (kesin dosyalar S3 prompt'unda ölçülür); ebeveyn paket revize edildi (O-16, 2026-10-07) |
| S4 | `/api/product-master/*` uçları ve sözleşme sorguları, `/api/internal/v1/product-master/validate`; ağ geçidi rotası **entegrasyon işi** (korumalı dosya); Satın Alma doğrulayıcısı **Satın Alma ekibinin dosyası** |
| S5 | `frontend/Diten.Web/Views/MasterDataManagement/Items/**`, `wwwroot/assets/js/MasterDataManagement/Items/**`; `Views/MasterDataManagement/Gskus/**` ve `FinishedGoods/**` içinde "Stok davranışı" parçaları; resx (7 dil); kendini kaydetme |
| S6 | çevrim doğrulayıcısı (alan hizmeti) + testler (§4.4) |
| S7 | dev verisi adımları (R-07 — stok ekibinin dev verisi isteği) `docs/guides/operations/` altında (nasıl yapılır belgesi — docs-organization 4. soru); stok ekibine bildirim |

## 6. Protected Paths

- `.antigravity/**` — kurallar (RCS-001 §3 tablosu dahil) yalnız CT tarafından değiştirilir (O-18).
- Stok / Satın Alma / Üretim servisleri ve sözleşme dosyaları (`docs/analysis/contracts/inventory-bundle.openapi.yaml`,
  `trace.openapi.yaml`, `bom.openapi.yaml`, Satın Alma sözleşmeleri) — sahipleri o ekipler; `skuLevel: Item` eklemesini
  stok ekibi yapar (§7.2).
- `gateway/**/ocelot*.json` — rota eklemesi entegrasyon işidir (integration-agent).
- `frontend/Diten.Web/Views/Shared/**` — paylaşılan yerleşim yalnız ortak liste bileşeni üzerinden tüketilir.
- MOD-0048 mevcut `uom`, `pack-applicability`, `market` listeleri ve `VerifiedGskuUniversalCatalog` davranışı — dokunulmaz.
- GP / GSKU / LSKU / Bitmiş Ürün kümeleri: S3 yalnız **ekleme** yapar (stok alanları + yeni komutlar); mevcut
  komutlar, işlemciler ve testleri değişmez ve yeşil kalır. Paylaşılan onay bağlantı noktası S2'de yalnız çıkarılır.
- MDM ve ağ geçidi `Program.cs` içindeki 64 KB başlık sınırı — düşürülemez (sözleşmenin parçası, §8.5).
- Platform: yalnız ekleme (yeni denetim işlemleri ve referans listeleri); silme / değiştirme yok.

## 7. Dependencies / Boundaries

### 7.1 Bağımlılıklar

| Bağımlılık | Durum | Engel mi |
|---|---|---|
| Denetim yolu b (`mdm-merkezi-iletim`) | **Var** (`tests/architecture/audit-ledger/Diten.MdmService.md`) | Hayır |
| MDM HTTP test barındırıcısı (`MdmHttpHost`, gerçek Mongo) | G0 ölçümü (2026-10-06): bu dalda yok; `feature/mdm/product-five-takeover` ve `feature/mdm/product-fg-accept` dallarında var. S1 prompt'unda yeniden ölçülür | **S1 için evet** |
| MOD-0023 onay motoru + pozisyonla aday | Var (GP-1b) | S2 için dev'de "kalite pozisyonu" ve şablon kurulumu gerekir |
| MOD-0048 üç yeni liste | Tanım S0'da (FU01 §4); kod yok | S1'in ilk işi; Platform kodu için FU01 adlı adım yetkisi gerekir |
| P0 kabulü — GSKU, LSKU, Bitmiş Ürün (plan §4 P0, yazılmış parçaların kabulü) | Sürüyor | **S3 için evet** (kabul edilmemiş kümenin üstüne alan eklenmez) |
| Ebeveyn MOD-0290 paket revizyonu (O-16) | **Yapıldı** (2026-10-07; ebeveyn §4 GSKU / FG notları) | Hayır |
| Stok uygunluk sorgusu, kiracı geneli (O-11) | INVENTORY-BUNDLE v1 `/availability` tüzel kişi kapsamlı | S1 emekliye ayırmayı engellemez: sorgu yokken emekliye ayırma kapalı-başarısız reddedilir (§8.3) |
| 64 KB istek başlığı | **Var** — MDM ve ağ geçidi `Program.cs` (`MaxRequestHeadersTotalSize = 64 * 1024`, commit 9dfe859f2; ölçüldü 2026-10-07) | Hayır; S4 testle ölçer |
| Kalite birimine GxP sınıfı teyidi (Karar 3 notu) | Sorulacak | Hayır (karar a ile ilerlenir) |
| GMG-SCM-SOP-0001 v0.3 | Sahipte | Yalnız SOP kodu alanı için |

### 7.2 Sınırlar ve stok ekibinin taahhütleri

| Taraf | Taahhüt / sınır | Kaynak | MDM'ye etkisi |
|---|---|---|---|
| Stok (MOD-0173 / MOD-0174) | INVENTORY-BUNDLE (stok hareket / bakiye sözleşmesi) ve TRACE (lot / seri izleme sözleşmesi) sözleşmelerine `skuLevel: Item` ekler (yalnız ekleme) | cevap S1, 2026-10-07 | Kalemin stok satırı `(itemId, skuId, skuLevel) = (X, X, Item)`; S4 uçtan uca testi buna bağlı |
| Stok | Lot kimlik politikası `Item` düzeyini kabul eder | cevap S1 | Kalemde `lotControlled` stok kuralını sürer |
| Stok | DEC-INV-17'yi günceller (Model A + `ItemKind: Item`) | cevap S1 | Model B (CRM Product) yine tüketilmez |
| Stok | Hareket kuralını durumlara göre **stok uygular**; MDM yalnız durumu yayımlar | cevap S5 | §8.2; hareket türü eşlemesi O-20 |
| Stok | Uygunluk sorgusunu sağlar; MDM emekliye ayırmada çağırır | cevap S5 | §8.3; kiracı geneli cevap O-11 |
| Stok | Geçici kalem tablosu yok, taşınacak veri yok; dallar `feature/sce/mod-0173-inventory-ledger-valuation`, `feature/sce/mod-0174-lot-serial-tracking` | cevap S7 | Göç işi yok |
| Stok | v1.1 hazır olunca Prism köprüsünü kaldırır, gerçek MDM'ye karşı kendi G1 kapısının (stok ekibinin kimlik kapısı) maddelerini kapatır | plan §4a R-07 | S7 "hazır" bildirimi |
| Stok | Geçici "Model A seçici" rotası (W-UI-01) `/stockable-items` ile kalkar | plan §4a, soru S4 | S4 araması |
| Stok | Dev verisi isteği (R-07): birkaç Global Ürün + GSKU / LSKU + 1 lot takipli + 1 seri takipli + 1 hammadde kalemi | plan §4a | S7: paylaşılan dev verisini sahip girer, CT adımları verir |
| Stok | İstek listeleri (R-01 … R-09) bizim git'te yok (itilmemiş) | cevap ek | O-23 |
| Üretim (MOD-0193 BOM, MVP-3) | Parti reçetesi BOM'da, MDM kimliğine referansla | cevap S2 | Bu pakette alan yok; BOM sahibi O-19 |
| MDM P5 (madde + bileşim) | Madde kaydı + ruhsat / etiket bileşimi MDM'de, Ürün Tanımı Sürümüne bağlı | cevap S2 | Ayrı paket |
| Depo (MOD-0178) | Koli / palet GTIN'i, ambalaj hiyerarşisi | cevap S8 | P9 |
| Karantina (MOD-0175) | Tehlike sınıfı, CoA zorunlu mu, girişte varsayılan karantina; yeniden test kuyruğu | plan §4a, soru S3 | Sonra; `RetestDays` bugün yayımlanır |
| FEFO (MOD-0176) | Raf ömrü tüketicisi | plan §4a, soru S3 | `shelfLifeDays` |
| Satın Alma (MOD-0141/0142/0143/0145) | Kalem kimliği + birim; doğrulayıcı değişimi onların dosyası | tasarım §E | F7 |
| MOD-0048 (Platform) | Üç yeni liste; mevcut listeler değişmez | bu paket §4.3 | FU01 adlı adımı |
| MOD-0290 ebeveyn | GSKU / LSKU / Bitmiş Ürün alan tabloları | O-16 | revize edildi 2026-10-07 |

## 8. Runtime Constraints

### 8.1 Yaşam döngüsü (C2) ve geçişler

```text
Draft ──Submit──▶ PendingActivation ──onay (MOD-0023)──▶ Active ──Deactivate──▶ Deactivated ──Retire*──▶ Retired
  ▲                    │   │                                ▲                        │
  └──── Withdraw ──────┘   └── red / kapandı-kararsız ──▶ Draft│                        │
                                                               └─ onay (MOD-0023) ◀── RequestReactivation
                (yeniden etkinleştirme de PendingActivation'dan geçer; red / geri çekme → Deactivated)
                (* stok uygunluk denetimi, §8.3)                         (Retired geri dönmez)
```

| Geçiş | İzin | Onay | Not |
|---|---|---|---|
| oluştur / düzenle (Draft) | `create` / `update` | yok | Serbest düzenleme |
| Draft → PendingActivation | `submit` | başlatır | Taslak kilitlenir. **S1'de iskelet:** gönder ve yeniden etkinleştirme isteği 503 `ITEM_ACTIVATION_APPROVAL_UNAVAILABLE`, yazım yok; onay ve `withdraw` S2 |
| PendingActivation → Draft | `withdraw` | geri çekme | Motorda iptal |
| PendingActivation → Active | — (işçi) | **onaylandı** | Yapan ≠ onaylayan |
| PendingActivation → Draft | — (işçi) | reddedildi / kapandı-kararsız / kendi onayı | Taslak açılır |
| Active → Deactivated | `deactivate` | yok | Yeni girişte seçilemez; mevcut stok çıkabilir |
| Deactivated → PendingActivation → Active | `submit` (istek) · `withdraw` (geri çekme) | **onaylı** — ilk etkinleştirmeyle aynı yol | CT kararı (2026-10-06): yeniden etkinleştirme bir etkinleştirmedir (Karar 4). İşlem `ActivationKind = Reactivation` taşır; red / kapandı-kararsız / kendi onayı / geri çekme kalemi **Deactivated**'a döndürür |
| Deactivated → Retired | `retire` | yok | **Stok uygunluk denetimi geçmeli (§8.3).** Geri dönmez; kod yeniden kullanılmaz. Etkin kalem önce kullanım dışı bırakılır |
| Active → Retired | — | — | **Yok** (409; önce kullanım dışı) |
| Kritik alan düzeltmesi (Active) | `update` | **onaylı** | Sonraki dilim |

### 8.2 Stok hareket kuralı (stok uygular, MDM yayımlar)

| İç durum | Sözleşmedeki durum | Yeni giriş | Mevcut stoktan çıkış, hurda, iade | Ters kayıt (REVERSAL) |
|---|---|---|---|---|
| `Draft` | `Draft` | hayır | hayır | evet |
| `PendingActivation` (ilk etkinleştirme) | `Draft` | hayır | hayır | evet |
| `Active` | `Active` | evet | evet | evet |
| `Deactivated` | `Deactivated` | hayır | evet | evet |
| `PendingActivation` (yeniden etkinleştirme) | `Deactivated` | hayır | evet | evet |
| `Retired` | `Retired` | hayır | hayır | evet |

- Kural MDM'de **uygulanmaz**; MDM durumu sözleşmede yayımlar, stok her harekette canlı okur ve uygular. Hangi stok
  hareket türünün "giriş", "çıkış", "iade" sayılacağı (ör. müşteri iadesi, depolar arası transfer, sayım fazlası) stok
  ekibinin DEC-INV-17 güncellemesindedir (O-20).
- Durumdan bağımsız: `stockable: false` kayıtta stok yeni hareket kabul etmez, ters kayıt hariç (CT kararı O-26). Bu kural
  kalem ve ürün SKU'larının hepsine uygulanır; sözleşmede `LifecycleStatus` açıklamasında ve `x-notes`'ta yazılı.
- Yanıt enum'ları açıktır: stok bilinmeyen bir durum değeri görürse "yeni hareket yok" sayar, ters kaydı yine kabul eder.
- Emekli kalemde ters kayıtla geri gelen stok: O-24.
- Ürün satırlarının (GP / GSKU / LSKU / Bitmiş Ürün) iç durum eşlemesi: §16.3 (O-4).

### 8.3 Emekliye ayırmada stok uygunluk denetimi (soru S5; dilim S1'de yapılır)

1. `RetireItemCommand` yalnız `Deactivated` kalemde çalışır. Durum değişmeden önce MDM stok ekibinin uygunluk
   sorgusunu `(skuId = itemId, skuLevel = Item)` için çağırır.
2. **Sıfır olmayan stok:** herhangi bir tüzel kişide, herhangi bir stok durumunda (AVAILABLE, QUALITY_INSPECTION,
   QUARANTINE, BLOCKED, IN_TRANSIT) sıfırdan farklı miktar — eksi bakiye de sıfır değildir.
3. Sonuç sıfır değilse → **409 `ITEM_RETIRE_STOCK_NOT_ZERO`**; kalem `Deactivated` kalır.
4. Stok servisine ulaşılamıyorsa, bütçe dolarsa, 5xx ya da çözülemeyen cevap gelirse, cevap kiracının bütün tüzel
   kişilerini kapsamıyorsa → **503 `ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE`**; kalem `Deactivated` kalır
   (kapalı-başarısız; "sessizce geçer" yoktur).
5. Çağrı MDM'nin servis kimliğiyle yapılır, kullanıcının belirteciyle değil; zaman bütçesi S1'de sabitlenir (öneri:
   MOD-0048 sağlayıcısıyla aynı 2 sn).
6. Reddedilen emekliye ayırma da denetim kaydı bırakır (`Outcome = Failed`, neden kodu ile — AUD-001 §3; §21).
7. Bugünkü engel: INVENTORY-BUNDLE v1 `/availability` "Tenant + LegalEntity scoped" — kiracı genelinde tek cevap
   vermiyor (O-11). Stok ekibinin kiracı geneli sorgusu gelene kadar madde 4 geçerlidir: emekliye ayırma reddedilir.

Aynı korumanın GSKU / LSKU / Bitmiş Ürün emekliye ayırmasında da olması önerilir; o komutlar ebeveyn paketindedir (O-11).

### 8.4 Kabul edilmiş GSKU kalıbı (WP-MDM-GSKU-ACCEPT-01; BL-552 — GP onay akışının motor cevaplarını karantinaya çevirmesi) — S2'nin tasarım kuralı

- **(A)** Kalem ancak motor bu istek için hiçbir şey tutmadığı biliniyorsa taslağa döner / kilidi bırakır: başlatma
  araması ↔ başlatma cevabı ayrıdır; "hiçbir şey gelmedi" yalnız bekleme penceresinden sonra güvenilir; motorun üç
  cevabı (açık → bekle; kapandı-kararsız → taslağa dön; çelişkili kanıt → takılı + uzlaştırma). **Sahipsiz onay olmaz.**
- **(B)** Hiçbir durum sonsuza dek, görünmeden, adlı bir çıkışı olmadan takılı kalmaz: her takılı durumun çıkışı ya
  yapanın aynı komutu ya kişidir (BL-530 — takılı onayın adlı çıkışı); iki gösterge sayar (`manual_reconciliation`,
  `needing_a_person`); depo "manuel uzlaştırma + yeniden denenebilir" çiftini reddeder; yazım sınıfları
  (kendiliğinden geçen → bekle, eşzamanlı → sayılı tekrar, diğer → takılı); makbuz bilen tekrar; yeni alanlar
  `IgnoreExtraElements` ile ve açılış denetimiyle.
- Takılı onay isteği taslağı **tutar** (GP onay akışı 3'teki karar ile aynı).

### 8.5 Okuma yükü, başlık ve kapsam

- Stok her harekette sözleşmeyi canlı okur; MDM'ye ulaşamazsa hareketi reddeder. MDM olay yayımlamaz (MVP-1).
- MDM 64 KB'a kadar istek başlığını kabul eder (gelen belirteç ~35 KB). Ölçüldü 2026-10-07: MDM ve ağ geçidi
  `Program.cs` içinde `MaxRequestHeadersTotalSize = 64 * 1024`. Bu sınır sözleşmenin parçasıdır; düşürülmesi yasak.
- Kalem satırları kiracı genelinde, ürün satırları sunucunun çözdüğü tüzel kişi kapsamıyla süzülür. Başka kiracının
  kimliği "yok" ile aynı cevabı alır (404 / `notFound`).

## 9. Layout & Shell Contract

- Tüm `Views/MasterDataManagement/Items/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **açıkça** (kiracı
  kabuğu; MDM'nin diğer ekranlarıyla aynı).
- GSKU ve Bitmiş Ürün sayfaları bugün slim (offcanvas + hızlı görünüm). "Stok davranışı" hızlı görünümde ayrı bir bölüm
  ve kendi düzenleme offcanvas'ı olarak eklenir (CT kararı O-12): kimlik formu büyümez, her form 8 alanın altında kalır,
  slim → compact geçişi gerekmez. LSKU'da kendi düzenlenen tek alan seri takibidir (O-3); yeri S5'te belirlenir
  (öneri: aynı offcanvas'ın yalnız seri alanlı biçimi).
- Yetkisiz kullanıcıya iskelet çizilmez ve yönlendirme yapılmaz (UAS-001, `.antigravity/rules/unauthorized-surface-standard.md`);
  "Stok davranışı" bölümü izni olmayan kullanıcıda hiç çizilmez.

## 10. Backend File Convention

```text
services/Diten.MdmService/src/Diten.MdmService.Application/Features/Items/
├── Commands/   CreateItemCommand · UpdateItemCommand · SubmitItemCommand · WithdrawItemApprovalCommand ·
│               ApplyItemActivationDecisionCommand · DeactivateItemCommand · RequestItemReactivationCommand ·
│               RetireItemCommand · AddItemUomConversionCommand (S6) ·
│               RequestItemCriticalCorrectionCommand · ApplyItemCriticalCorrectionDecisionCommand (sonraki dilim)
├── Queries/    GetItemListQuery · GetItemByIdQuery
├── Handlers/
│   ├── CommandHandlers/  CreateItemHandler · UpdateItemHandler · SubmitItemHandler · WithdrawItemApprovalHandler ·
│   │                     ApplyItemActivationDecisionHandler · DeactivateItemHandler · RequestItemReactivationHandler ·
│   │                     RetireItemHandler · AddItemUomConversionHandler ·
│   │                     RequestItemCriticalCorrectionHandler · ApplyItemCriticalCorrectionDecisionHandler
│   └── QueryHandlers/    GetItemListHandler · GetItemByIdHandler
├── Validators/ CreateItemValidator · UpdateItemValidator · UomConversionChainValidator (S6)
├── Workflow/   ItemWorkflowProcessor (S2; paylaşılan bağlantı noktasıyla)
└── ItemModels.cs

services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/   (S3, yalnız ekleme)
├── Commands/   SetGskuStockBehaviourCommand · SetLskuStockBehaviourCommand ·
│               SetFinishedGoodStockBehaviourCommand · AssignFinishedGoodGtinCommand
└── Handlers/CommandHandlers/  (aynı adlar, `Handler` sonekiyle) · Validators/ (aynı adlar, `Validator` sonekiyle)

services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMasterContract/   (S4)
├── Queries/    GetProductMasterProductQuery · GetProductMasterSkuQuery · ResolveSkuProductQuery · GetSkuUomQuery ·
│               ValidateProductMasterRefsQuery · SearchStockableItemsQuery · BulkReadSkusQuery
└── Handlers/QueryHandlers/  (aynı adlar, `Handler` sonekiyle)

services/Diten.MdmService/src/Diten.MdmService.Infrastructure/   (S1)
└── IInventoryStockAvailabilityClient (+ HTTP uygulaması; servis kimliği, bütçe, kapalı-başarısız)
```

Handler / Validator adlarında `Command` / `Query` soneki yok. `Delete` / `BulkDelete` **yok** (ebeveyn paket kuralı:
iş yaşam döngüsü `retire` ile biter). Kesin klasör adları S1 / S3 / S4 prompt'larında ölçülür.

## 11. Frontend File Contract

```text
Views/MasterDataManagement/Items/                       (Compact)
├── Index.cshtml       (Layout açıkça; liste = ortak liste bileşeni `_ListShell` + `createList`, altın liste)
├── Create.cshtml · Edit.cshtml · Details.cshtml
├── _Form.cshtml       (iki bölüm: Kimlik · Stok davranışı; çevrim listesi satır ekleme ile)
├── _Filter.cshtml · _DataTable.cshtml · _IndexL10n.cshtml
└── ItemsIndex.cs
wwwroot/assets/js/MasterDataManagement/Items/ index.js · index.l10n.js

Views/MasterDataManagement/Gskus/ ve FinishedGoods/     (S5, yalnız ekleme)
├── _StockBehaviourSection.cshtml     (hızlı görünümde okuma bölümü)
└── _StockBehaviourOffcanvas.cshtml   (düzenleme; FG'de GTIN dahil)
```

`_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` Items'ta **yok** (compact). Satır içi CSS yok (FG-003 —
sınıflar `backbone-custom.css`'te). Liste, ilk MDM ürün ekranı olarak ortak liste bileşenini kullanır (BL-440 — altın
liste geçişi dokunma protokolü). Ortak bir "Stok davranışı" parçası çıkarılırsa yeri S5'te `views-organization`
kuralıyla belirlenir.

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| Name | Evet | Trim, 1–200, kontrol karakteri yok | Tekil dizin (kiracı, malzeme türü, normalize ad, emekli değil) | `NameExistsAsync` |
| Description | Hayır | Trim, ≤ 2000 | — | — |
| MaterialType | Evet | Altı değerden biri (MOD-0048 `material-type` etkin değeri); Active'de değişmez | — | referans çözümleyici |
| BaseUomCode | Evet | MOD-0048 `item-uom` etkin değeri + seçim kanıtı; Active'de değişmez | — | referans çözümleyici |
| UomConversions | Hayır (liste) | §4.4 kuralları 1–6; birimler `item-uom` etkin değerleri | — | çevrim doğrulayıcısı (S6) |
| StorageConditionCodes | Hayır (liste) | Her kod `storage-condition` etkin değeri + seçim kanıtı; tekrar yok; en çok bir sıcaklık koşulu (O-2) | — | referans çözümleyici |
| LotControlled / Stockable | Evet | bool; Active'de değişmez | — | — |
| ShelfLifeDays | Hayır | 1–3650 | — | — |
| RetestDays | Hayır | 1–3650 | — | — |
| MinRemainingShelfLifeDays | Hayır | 0–3650; `ShelfLifeDays` varsa ≤ ona | — | — |
| ExpectedVersion | Evet (yazımlarda) | Mevcut sürüm | CAS | — |
| Retire | — | Yalnız `Deactivated`; stok denetimi sıfır (§8.3) | — | `IInventoryStockAvailabilityClient` |
| GSKU / LSKU / FG stok alanları (S3) | §4.2 | Aynı birim, saklama ve çevrim kuralları; FG `ShelfLifeDays` ≤ GSKU (CT kararı O-15) | — | — |
| FG `Gtin` (S3) | FG'de `Stockable = true` için evet | 14 hane, GS1 kontrol hanesi doğru | Tekil dizin (kiracı, GTIN, emekli olmayan FG) | `GtinExistsAsync` |

İlişkili kurallar: `Stockable = true` olabilmesi için kaydın seviyesindeki "gerekli" alanlar dolu olmalıdır (O-8).
ACTIVE_INGREDIENT, EXCIPIENT, RAW_MATERIAL ve INTERMEDIATE türünde `Stockable = true` iken `ShelfLifeDays` ya da
`RetestDays`'ten en az biri zorunludur; ambalajda isteğe bağlı (CT kararı O-9). Ambalaj dışı kalemde (`PACKAGING_PRIMARY` /
`PACKAGING_SECONDARY` dışı) boş `StorageConditionCodes` listesiyle `Stockable = true` olamaz (CT kararı O-25).

## 13. Failure Path to Verify (S1 / S3 / S4 HTTP testleri, gerçek Mongo)

| Durum | Beklenen |
|---|---|
| Kimliksiz | 401 (belirteç yok, tenant başlığı var). Tenant başlığı da yoksa servis geneli `TenantResolutionMiddleware` yetkilendirmeden önce 400 "Missing Tenant" döner (bütün MDM uçları; S1 ölçümü) |
| İzinsiz | 403 (ekranda yetkisiz yüz, UAS-001) |
| Başka kiracının kalemi / SKU'su | 404 (ifşa yok); toplu okumada `notFound` |
| Geçerli oluşturma | 201 + `IT-` kodu |
| Aynı ad + malzeme türü | 409 `ITEM_NAME_CONFLICT` |
| Geçersiz birim / saklama koşulu / malzeme türü | 422 |
| İki sıcaklık koşulu birden (O-2) | 422 `ITEM_STORAGE_CONDITION_CONFLICT` |
| Zincir 4 adımı aşıyor / döngü / sıfır ya da eksi pay-payda / üs gösterimi | 422 `UOM_CONVERSION_INVALID` (neden ayrıntıda) |
| Eşzamanlı yazım (eski sürüm) | 409 `ITEM_CONCURRENCY_CONFLICT` |
| Aynı Idempotency-Key ile tekrar | ilk cevap, ikinci yazım yok |
| Active'de ad / açıklama dışı **dolu** alanı onaysız değiştirme | 409 `ITEM_CRITICAL_FIELD_LOCKED` (boş alanın ilk doldurulması: 200 + denetim — O-8) |
| Active kalemi doğrudan emekliye ayırma | 409 (önce kullanım dışı) |
| Deactivated kalemde stok sıfır değil | 409 `ITEM_RETIRE_STOCK_NOT_ZERO`; kalem Deactivated kalır; ret denetimde |
| Stok servisine ulaşılamıyor / bütçe doldu / kısmi kapsam | 503 `ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE`; kalem Deactivated kalır; ret denetimde |
| Kendi kaydını onaylama | uygulanmaz, taslağa döner |
| Denetim günlüğü yazılamıyor | iş tamamlanır, uyarı günlüğü + gösterge (Karar 3a) |
| Retired kalemi geri alma | 409 |
| FG GTIN kontrol hanesi yanlış / başka FG'de var | 422 `FG_GTIN_INVALID` / 409 `FG_GTIN_CONFLICT` |
| Zorunlu stok alanı eksikken `Stockable = true` | 422 `STOCK_BEHAVIOUR_INCOMPLETE` (O-8) |
| Onaylı GSKU / FG'de dolu stok alanını onaysız değiştirme | 409 `STOCK_BEHAVIOUR_FIELD_LOCKED` (O-8; boş alanın ilk doldurulması 200 + denetim) |
| FG raf ömrü üst GSKU'nunkinden uzun | 422 `FG_SHELF_LIFE_EXCEEDS_GSKU` (O-15) |
| Raf ömrü ve yeniden test ikisi de boşken etkin madde / yardımcı madde / hammadde / ara ürün kaleminde `Stockable = true` | 422 `STOCK_BEHAVIOUR_INCOMPLETE` (O-9) |
| Ambalaj dışı kalemde boş saklama koşulu listesiyle `Stockable = true` | 422 `STOCK_BEHAVIOUR_INCOMPLETE` (O-25) |
| Toplu okuma 0 ya da 200'den fazla kimlik | 400 `BATCH_LIMIT_EXCEEDED` |
| Toplu okumada geçersiz UUID | 400 `INVALID_UUID` |
| Aramada `pageSize` > 100, `q` > 200 karakter ya da bilinmeyen enum değeri | 400 `INVALID_PARAMETER` |
| `/validate`'te geçersiz satır biçimi ya da 200'den uzun `refs` | 400 `INVALID_REF` / `BATCH_LIMIT_EXCEEDED` |
| `getSkuUom` bilinmeyen kimlik | 404 `UNKNOWN_SKU` |
| ~35 KB belirteçle sözleşme çağrısı | 200 (431 değil) |

## 14. Authorization Convention

Kiracı servisi biçimi `mdm.<kaynak>.<eylem>` (PKS-001 — izin anahtarı standardı); genel `manage` **yok**; `delete` **yok**.

| Anahtar | İnsana verilir | Açıklama |
|---|---|---|
| `mdm.items.read` | evet | Liste ve detay |
| `mdm.items.create` | evet | Taslak oluştur |
| `mdm.items.update` | evet | Taslak düzenle; etkin kalemde serbest alanlar; çevrim ekleme (S6); kritik düzeltme isteği |
| `mdm.items.submit` | evet | Etkinleştirme ve yeniden etkinleştirme (REACTIVATE) onayına gönder |
| `mdm.items.withdraw` | evet | Onay isteğini geri çek |
| `mdm.items.deactivate` | evet | Kullanım dışı bırak (DEACTIVATE) |
| `mdm.items.retire` | evet | Emekliye ayır (stok denetimiyle) |
| `mdm.gskus.edit-stock-behaviour` · `mdm.lskus.edit-stock-behaviour` · `mdm.finished-goods.edit-stock-behaviour` | evet | S3 stok davranışı ve FG GTIN düzenleme (CT kararı O-12) |
| `mdm.product-master.read` | evet | Sözleşme okumaları, kullanıcı oturumu: beş v1 yolu + `/items` + arama + toplu okuma (CT kararı O-13; G0'daki `mdm.stockable-items.read` kullanılmaz) |

**İnsana verilemeyenler (rol ataması yapılmaz):**

| Yetki | Kim | Neden |
|---|---|---|
| Etkinleştirme kararını uygulama | MDM işçisi (servis kimliği) | Karar motordan gelir; onay Görev Merkezi'nde kalite pozisyonundaki kişi tarafından verilir |
| Servisler arası `/api/internal/v1/product-master/validate` ve sözleşme okumaları | Stok / Satın Alma servis kimlikleri (güvenilir tüketici) | Sunucudan sunucuya; anahtar adı S4'te servis güven zinciriyle kesinleşir |
| Stok uygunluk sorgusu (MDM → stok) | MDM servis kimliği | Emekliye ayırma denetimi (§8.3); stok tarafının kabul etmesi gerekir (O-11) |
| Kod ayırma tüketimi / süresi dolma | sistem | Ebeveyn paket kuralı |

Yapan / onaylayan ayrımı izinle değil, alan geçişinde insan kimliği karşılaştırılarak uygulanır.

## 15. Gateway / API Routing Decision

Gerekli. Ekran uçları `/api/mdm/items/*` ve sözleşme uçları `/api/product-master/*` ağ geçidinden kullanıcı oturumuyla;
servisler arası `/api/internal/v1/product-master/validate` ağ geçidi dışından servis kimliğiyle. Rota eklemesi korumalı
dosyadır → integration-agent görevi (S4 / S5 ile). Başlık sınırı (64 KB) ağ geçidinde zaten var (ölçüldü 2026-10-07).

## 16. Okuma sözleşmesi — `product-master-bundle` v1.1 (yalnız ekleme)

Dosya: [`docs/analysis/contracts/product-master-bundle.openapi.yaml`](../../../../docs/analysis/contracts/product-master-bundle.openapi.yaml).
Sürüm `1.1.0-draft.4` (S0-FIX1, 2026-10-07). v1.0'ın değişmemiş kopyası
[`product-master-bundle.v1.0.openapi.yaml`](../../../../docs/analysis/contracts/product-master-bundle.v1.0.openapi.yaml)
dosyasındadır (git blob 4f9c63309^ ile aynı); v1.0 Prism taklidi onu kullanır. Ana dosyada v1.0 satırları yalnız
bilinçli istisnalarla değişti, hepsi changelog'un draft.4 satırında sayılı: `example` → adlı `examples` (ilk örnek v1.0
örneği, değerleri aynı — ölçüldü), ProductRef stok alanlarına `deprecated` + açıklama (O-1), getSkuUom 404, `/validate`
400, `refs` en çok 200, `UomInfo` / `ValidateRequest` / `ValidateResponse`'a eklenen açıklama, iki enum'a değer
eklenmesi. Dondurulmuş `UomConversion` şemasına dokunulmadı; pay / payda > 0 kuralı v1.1 açıklamasıdır (CT kararı). v1.1 eklemeleri CT dondurana kadar DRAFT.

### 16.1 Ekler

| Yer | v1.1 |
|---|---|
| `SkuLevel` | `+ Item` (`Gsku`, `Lsku`, `FinishedGood` aynen) |
| `ItemKind` (yeni) | `GlobalProduct` \| `Item` — `ProductRef`, `SkuRef`, `SkuProductLink`, `StockableItemRef`, `validateRefs` sonucunda |
| `MaterialType` (yeni) | Altı değer; yalnız `itemKind: Item` kayıtlarında dolu |
| `LifecycleStatus` | `+ Deactivated`, `Retired` (v1.0 değerleri aynen); hareket kuralı enum açıklamasında |
| `GET /products/{itemId}` | Kalemi de cevaplar (`itemKind`, `materialType`, `storageConditions`, `stockable`). Global Ürün türünde v1.0 ve v1.1 stok alanları dönmez, `getSku`'dan okunur (CT kararı O-1 — **davranış daralması**; v1.0 stok alanları `deprecated` işaretli, "alan yok = bilinmiyor, false / 0 varsayılmaz"; stok ekibinin yazılı onayı bekleniyor) |
| `GET /skus/{skuId}?level=Item` | Stok davranışı için **yetkili okuma** (O-27). `skuId == itemId`; ürün SKU'larında S3 alanları (`baseUomId`, `shelfLifeDays` + `shelfLifeDaysSource`, `retestDays`, `storageConditions`, `lotControlled`, SKU düzeyi `serialControlled`, `stockable`, `name`, `lifecycleStatus`); Bitmiş Ürün `gtin` = satış kutusu GTIN'i |
| `GET /skus/{skuId}/product?level=Item` | Kendisini döndürür (D-SKU-LINK); Bitmiş Ürün → GSKU (D-2) → Global Ürün; cevaba şema (`SkuProductLink`) |
| `GET /skus/{skuId}/uom` | Kalem için `skuId == itemId`; çevrim girildiği yönde, tüketici iki yönde kesin kesirle yürür; pay / payda > 0 (v1.1 açıklaması; dondurulmuş `UomConversion` şeması değişmedi); yalnız kaydın kullandığı çiftler; zincir ≤ 4 adım (adım = kenar); aynı boyut çevrimleri açıkça; Lsku'da üst GSKU'nun çevrimleri (O-3); FG'de GSKU zinciri dahil; 404 `UNKNOWN_SKU` |
| `POST /validate` | Geçerli satır biçimleri: yalnız `itemId` · `skuId` + `skuLevel` · üçü birden (bağ da denetlenir); seviye ya da bağ uyuşmazlığı `exists: false`; başka biçim 400; `refs` en çok 200; `skuLevel: Item` kabul eder; `itemId` her iki türü çözer; sonuçta `itemKind`, `lifecycleStatus`; `exists` = varlık, kullanılabilirlik `lifecycleStatus`'tan |
| `GET /items/{itemId}` | Kısayol; şema `ProductRef`; Global Ürün kimliği 404 |
| `GET /stockable-items` | Sayfalı arama: `q` (kod ön eki YA DA ad içinde; ordinal, büyük / küçük harf duyarsız, aksan katlaması yok; boş serbest), `skuLevel`, `materialType` (verilirse yalnız Item satırları), `status` (verilmezse Retired dışındaki bütün durumlar), `page`, `pageSize` (≤ 100; aşarsa 400); yalnız `stockable: true`; sıra `canonicalCode`; son sayfanın ötesi 200 + boş liste |
| `POST /skus/bulk-read` (yeni) | ≤ 200 kimlik → kimlik + seviye + kod + ad + `lifecycleStatus`, istek sırasında; Draft / Deactivated / Retired da bulunur; Global Ürün kimliği ve bulunamayanlar `notFound`; `name` yalnız gösterim (ayrıştırılmaz, anahtar değil); 400 (`BATCH_LIMIT_EXCEEDED`, `INVALID_UUID`) |
| `info.x-notes` | Açık enum kuralı (yanıtta bilinmeyen değer hata değil; `LifecycleStatus`'ta "yeni hareket yok"; istekte bilinmeyen değer 400), yetkili okuma `getSku`, O-1 davranış daralması, `itemKind` yoksa GlobalProduct, `canonicalCode` opak, `contractVersion` ile dallanılmaz, birim kodları veridir (sabit kodlanmaz; O-21), ondalıklar tam ayrıştırılır, taslak / eksik kayıt görünümü (O-25), `stockable: false` hareket kuralı (O-26), 64 KB başlık, Prism başlık notu (doğrulanmadı), 3.1 / `nullable` sözdizimi notu, olay yok, sonraki alanlar |
| `info.x-changelog` | 2026-10-06 (G0), 2026-10-07 (S0, CT kararları, S0-FIX1 — draft.4: "davranış daralması (O-1) — stok ekibinin yazılı onayı bekleniyor") |
| Örnekler | Her işlemde adlı `examples`; ilk örnek v1.0 örneği ("v1.0-mock-only", Prism varsayılanı değişmez); v1.1 örnekleri: `item` (`/products/{itemId}` dahil), `global-product`, `gsku`, `lsku`, `fg`, `item-self-link`, `item-uom` (KGM / GRM), `fg-uom` (koli / kutu + GSKU zinciri), arama ve toplu okuma örnekleri; hepsi şemaya karşı doğrulandı |
| Şema biçimi | `materialType` yanıt alanları `MaterialType`'a bağlı (`anyOf` + null); `StorageConditionCode` beş kodla enum; yalnız yeni v1.1 şemalarında `required` (StockableItemRef, StockableItemPage, BulkSkuReadItem, BulkSkuReadResponse); v1.1 alanlarında 3.1 sözdizimi, v1.0 `nullable` olduğu gibi |

**Anlam notu:** v1.0'da `itemId` "stok satırının bağlandığı kimlik" = Global Ürün kimliğidir. v1.1'de malzeme kalemleri
için aynı alan kalemin kendi kimliğidir; `itemKind` (ve `skuLevel`) hangisi olduğunu söyler. Kimlikler UUID'dir, çakışmaz.
**Kapsam notu:** v1.0 "Tenant + LegalEntity scoped" der; kalemler kiracı geneli olduğu için (Karar 6) kalem satırlarına
tüzel kişi süzmesi uygulanmaz — ürün satırlarının kapsamı aynen kalır.

### 16.2 G0 terimlerinin eşlemesi

| G0 (2026-10-06) | S0 (2026-10-07) | Tür |
|---|---|---|
| şema `ItemType` | şema `MaterialType` | yeniden adlandırma + değer eşlemesi |
| alan / sorgu parametresi `itemType` | `materialType` | yeniden adlandırma |
| `RAW_MATERIAL` (etkin + yardımcı madde dahil) | `ACTIVE_INGREDIENT` · `EXCIPIENT` · `RAW_MATERIAL` (artık yalnız diğer hammadde) | bölme |
| `PACKAGING_MATERIAL` | `PACKAGING_PRIMARY` · `PACKAGING_SECONDARY` | bölme |
| `SEMI_FINISHED` | `INTERMEDIATE` | yeniden adlandırma |
| `CONSUMABLE` | — (uzlaşma listesinde yok) | çıkarıldı (O-10) |
| `LifecycleStatus.Inactive` | `Deactivated` | yeniden adlandırma |
| `LifecycleStatus.PendingActivation` | sözleşmede yok; iç durum, `Draft` / `Deactivated` olarak yayımlanır | eşleme (§16.3) |
| `MaterialItemRef` | `ProductRef` v1.1 alanları (`/items/{itemId}` `ProductRef` döner) | birleştirme |
| `MaterialItemRef.storageCondition` (tek) | `ProductRef.storageCondition` (v1.0, tek) + `storageConditions` (v1.1, çoklu) | eşleme (O-2) |
| `StockableItemRef.itemType` | `materialType` | yeniden adlandırma |
| iç `LotTracked` / `SerialTracked` | `LotControlled` / (kalemde yok) `SerialControlled` | yeniden adlandırma |
| iç `Inactive` | `Deactivated` | yeniden adlandırma |
| `storage-condition` önerisi (AMBIENT_15_25, BELOW_25, BELOW_30, COOL_2_8, FROZEN_BELOW_MINUS_18, CONTROLLED_ROOM_20_25) | AMBIENT_15_25, COOL_2_8, FROZEN_BELOW_MINUS_20, PROTECT_FROM_LIGHT, PROTECT_FROM_MOISTURE | uzlaşma listesi |
| `item-uom` kutu `BX`, rulo `RO` | kutu `XBX`, koli `XCS`, rulo `XRO` (S1 ölçümü, O-21) | UN/ECE Rec 20 ambalaj kodları |
| izin `mdm.stockable-items.read` | `mdm.product-master.read` | yerine geçme (CT kararı O-13) |
| dilimler G0–G5 | S0–S7 (§20) | yeniden numaralandırma |

### 16.3 Durum eşlemesi (iç → sözleşme)

| Kayıt | İç durum | Sözleşme |
|---|---|---|
| Kalem | `Draft`, `PendingActivation` (ilk) | `Draft` |
| Kalem | `Active` | `Active` |
| Kalem | `Deactivated`, `PendingActivation` (yeniden) | `Deactivated` |
| Kalem | `Retired` | `Retired` |
| GP / GSKU / LSKU / Bitmiş Ürün (CT kararı **O-4**) | `Draft`, `PendingIdentityApproval` → `Draft`; `IdentityApproved` → `Active`; `Retired` → `Retired` | v1.0 `Archived` hiç yayımlanmaz |

### 16.4 Hata kuralı

Tüketiciler 503 aldığında "sessizce geçer" demez (kapalı-başarısız). Sözleşmede bilinmeyen kimlik v1.0 cevabını alır
(404 `UNKNOWN_ITEM` / `UNKNOWN_SKU`). Satın Alma'nın kendi çağıranına döndüğü 422 `UNKNOWN_ITEM` Satın Alma'nın kuralıdır.

## 17. Acceptance Criteria (dilim başına)

- **S1:** §13 tablosunun kalem satırları `MdmHttpHost` üzerinde gerçek Mongo ile birer HTTP testiyle ölçülür; kod `IT-`
  ile ve tekrarsız, ailenin tek sayacından; ad tekilliği emekli olmayanlar arasında; alansız eski belge testi; yol b
  iletimi her yazma komutu için (§21) testle; emekliye ayırmada stok denetimi: sıfır → Retired, sıfır değil → 409,
  ulaşılamıyor / bütçe / kısmi kapsam → 503, üçünde de denetim kaydı; MOD-0048 üç liste tohumlu ve `uom` /
  `pack-applicability` / `market` değişmemiş (mevcut testleri yeşil); `IgnoreExtraElements` + açılış denetimi.
- **S2:** motorun üç cevabı, bekleme penceresi, yazım sınıfları, iki gösterge, depo değişmezi, yapan ≠ onaylayan,
  takılı onayın taslağı tutması, yeniden etkinleştirme reddinde `Deactivated`'a dönüş — her biri adlı test ve
  sabotajla kırmızı.
- **S3:** §4.2 alanları GSKU / LSKU / Bitmiş Ürün'de; FG raf ömrü mirası (`Own` / `Gsku`); FG GTIN kontrol hanesi ve
  tekilliği; eksik zorunlu alanla `Stockable = true` reddi ve dolu alanı onaysız değiştirme reddi (O-8); FG raf ömrü ≤ GSKU (O-15);
  LSKU alanlarının GSKU'dan mirası (O-3); mevcut GSKU / LSKU / FG testleri
  değişmeden yeşil; mevcut belgeler (alan yok) okununca stok alanları boş ve `stockable` false.
- **S4:** beş v1 yolu kalemi cevaplar (`itemKind`, `skuId == itemId`, D-SKU-LINK kendisi); `skuLevel=Item` ile
  `/validate`; arama sayfalı ve kararlı sıralı; toplu okuma ≤ 200, `notFound`, 400; v1.0 örnekleriyle geriye uyum
  (v1.0 alanları aynı adla ve türle döner); 503'te kapalı-başarısız; ~35 KB belirteçle 200; sözleşme dosyasına karşı
  şema testi (cevaplar v1.1 şemasını doğrular); açık enum kuralı (bilinmeyen değerle tüketici testi stok tarafında);
  `product-master-bundle.v1.0.openapi.yaml` Prism taklidinde v1.0 davranışı değişmeden.
- **S5:** Tüm `Views/MasterDataManagement/Items/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` açıkça;
  `verify_datatable_page.py` + `quality-gate-datatable`; yedi dil anahtar eşitliği; yetkisiz yüz (iskelet yok,
  yönlendirme yok); dar ekran; Ctrl+K'da "Kalemler"; GSKU / FG "Stok davranışı" bölümü izinsiz kullanıcıda çizilmez;
  canlı sayfa kontrolü.
- **S6:** §4.4 kuralları 1–9 her biri adlı test; aynı boyut çevrimi çarpandan türetilir; etkin kayıtta çevrim eklenir,
  değiştirilmez, silinmez; zincir 5. adımda reddedilir.
- **S7:** R-07 adımları yazılı; sahip dev verisini girdi; CT ölçtü (her kayıt sözleşmeden okunuyor); stok ekibine
  "hazır" bildirimi gitti.

## 18. Test Expectations

Yeni testler MDM HTTP barındırıcısında gerçek (geçici) mongod ile (BL-527); kural kendi kopyasıyla değil üretim koduyla
ölçülür; her koruma için sabotaj kanıtı (çapa tam bir kez, kırmızı = adlı test); Web tarafında hata kodu → cümle
köprüsü (7 dil) ve kod taraması (`GskuRefusalBridgeTests` kalıbı) — yeni kodlar: `ITEM_RETIRE_STOCK_NOT_ZERO`,
`ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE`, `UOM_CONVERSION_INVALID`, `ITEM_STORAGE_CONDITION_CONFLICT`,
`STOCK_BEHAVIOUR_INCOMPLETE`, `STOCK_BEHAVIOUR_FIELD_LOCKED`, `FG_SHELF_LIFE_EXCEEDS_GSKU`, `FG_GTIN_INVALID`,
`FG_GTIN_CONFLICT`. Stok uygunluk istemcisi test çiftiyle değil
HTTP taklidiyle (bütçe, 5xx, kısmi kapsam) ölçülür. Sözleşme testi cevapları `product-master-bundle.openapi.yaml`
şemasına karşı doğrular.

## 19. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (FU04, tüm dallarda ölçüldü)
- [x] Altı karar işlendi (§0.1)
- [x] Stok ekibinin cevabı (Karar 2) — kabul, 2026-10-07 (§0.2)
- [x] Denetim sınıfı ve yol (§21); kayıt kodu (§22)
- [x] Okuma sözleşmesi v1.1 taslağı uzlaşmaya göre (§16)
- [x] MOD-0048 yeni liste tanımları (MOD-0048-FU01 §4)
- [ ] CT: sözleşme v1.1'i dondurur (FROZEN v1.1)
- [x] CT kararları (2026-10-07, §25.1): O-1 … O-6, O-8, O-9, O-12 … O-15 kabul; O-7, O-10, O-11, O-16 … O-24 karara bağlandı
- [x] O-21 (birim kodları) — S1'de ölçüldü (XBX / XCS / XRO geçerli; IU için tek kod yok → stok + kaliteye soru)
- [ ] **MOD-0048-FU01 adlı adımı (üç listenin Platform sağlayıcısı)** — S1'in canlıda kullanılabilmesinin ön koşulu. Bugün sağlayıcı yok; kalem oluşturma 503 `ITEM_REFERENCE_LIST_UNAVAILABLE` (kapalı başarısızlık). CT'nin ayrı Platform işi; girdi: S1 raporu §5
- [ ] S5 ön koşulu: Auth `EntitlementOnlyViewerPermissions` + `mdm.items.read` (§23 #7)
- [ ] **FG kimlik işlemcisi dilimi önkoşulu (S3 incelemesi M1):** FG kimlik rezervasyonu FG'ye aynı işlemde dokunmalı (GSKU deseni, `FirstGskuIdentityWorkflowOperationRepository` rezervasyon dokunuşu); yoksa stok düzenlemesi açık FG kimlik işleminin sabitlediği sürümü kaydırabilir
- [ ] **S6 / düzeltme dilimi (S3 incelemesi M7):** GSKU düzeltmesi ya da taslak düzenlemesi `PackQuantity` / `PackUomCode`'u değiştirince türetilen kutu ↔ paket kenarı ve saklanan çevrimler yeniden doğrulanır
- [ ] **BL-574 benimseyicisi:** S3'ün dört stok komutu S1'in `IAuditDetailsProvider` mekanizmasını takeover'a girince benimser (önce / sonra, ilk doldurma)
- [ ] Stok ekibine sorular gönderildi (§25.1 sonu); O-11 cevabı gelene kadar emekliye ayırma reddedilir
- [ ] Registry'de FU03'ün (Ürün Tüzel Kişi Kapsamı) bu dala yansıması — CT yönetişim işi
- [ ] Kalite pozisyonunun dev'de kurulması — S2'den önce
- [ ] `storage-condition` ve `item-uom` değer listelerinin kalite / stok birimiyle teyidi — S1'den önce
- [x] Ebeveyn MOD-0290 paket revizyonu (O-16) — 2026-10-07
- [ ] P0 kabulü (GSKU, LSKU, Bitmiş Ürün) — S3'ten önce
- [ ] Paket `ready-for-dev` (CT)

## 20. Implementation Notes — dilimler (plan §4a, yaklaşık 8–10 prompt)

| Sıra | Dilim | İçerik | Risk |
|---|---|---|---|
| S0 | Belge | Sözleşme v1.1 (yalnız ekleme) + MOD-0048 yeni listeler + bu paket + denetlenen olaylar; dondurma (CT) | 🟢 |
| S1 | Kalem sunucu çekirdeği | Taslak / düzenle / kod (`IT`, ürün kimliği ailesi) / denetim; yaşam döngüsü durumları; emekliye ayırmada stok uygunluk denetimi; MOD-0048 üç listenin Platform tohumu | 🟡 ortak kod `switch`i; `MdmHttpHost` bağımlılığı; stok sorgusu (O-11) |
| S2 | Etkinleştirme onayı | MOD-0023, paylaşılan bağlantı noktası, (A)/(B) | 🟡 |
| S3 | Stok davranışı alanları | GSKU / LSKU / Bitmiş Ürün: temel birim, çevrimler, raf ömrü, saklama, lot, seri (SKU düzeyi), stoklanabilir, Bitmiş Ürün GTIN'i | 🟡 kabul sürecindeki kümelere ekleme (P0 kabulünden sonra) |
| S4 | Okuma sözleşmesi | Mevcut 5 yolun kalemi cevaplaması, D-SKU-LINK, arama, toplu okuma, 64 KB başlık testi | 🟡 tüketiciler Prism'den gerçeğe geçer |
| S5 | Ekranlar | **Kalemler** sayfası + GSKU / Bitmiş Ürün "Stok davranışı" bölümü; 7 dil, menü, Ctrl+K, yetkisiz yüz | 🟢 |
| S6 | Birim çevrimi doğrulamaları | Zincir, yönlü çevrim, ondalık hassasiyet (P2 — ölçü birimi eşlemesi — paketinin stoğa dönük kısmı) | 🟢 |
| S7 | Dev verisi + "hazır" | R-07 adımları (sahip girer) + stok ekibine bildirim | 🟢 |

G0'daki dilimlerin karşılığı: G0 → S0, G1 → S1, G2 → S2, G3 → S4, G4 → S5, G5 → S6; S3 ve S7 yeni. S1 ile S3 farklı
kümelere dokunur ve paralel yürüyebilir; S4 S1 ve S3'ten sonra. Her dilim düzeltme turlarıyla birden çok prompt
sürebilir; yeni dilim CT kararıdır.

## 21. Audited Events (AUD-001)

Servisin denetim altyapısı **var**: `mdm-merkezi-iletim` (yol b). Defterde yeni borç açılmaz: mevcut GP / GSKU / LSKU /
FG komutları defterin "bilinen borç" listesindedir; S3'ün yeni komutları o kümelerde de **ilk günden yol b ile** doğar.
Sınıf: ana veri (Karar 3a) — iletim hatası işi durdurmaz; uyarı günlüğü ve gösterge kalır. Reddedilen komut da
kaydedilir (`Outcome = Failed` / `Denied`, AUD-001 §3). **S1 ölçümü:** yol b kaydı ret **nedenini** taşımaz (`AuditForwardRequest`'te alan yok); neden cevapta (`errors`) ve uyarı günlüğündedir, kayıtta yalnız `Outcome = Failed`. Yapısal 400'ler (doğrulayıcı, `ValidationBehavior` en dışta) yol b'ye gitmez — servis geneli davranış. Okuma sorguları (ekran ve sözleşme) denetlenmez: kişisel veri
yok, ana veri okuması (AUD-001 §7 kapsamı dışı).

| Komut | Olay adı | Nesne | Yol | Önceki / sonraki ya da değişen alanlar | İstisna |
|---|---|---|---|---|---|
| CreateItemCommand | item.created | Item | b | Sonraki: kod, ad, malzeme türü, temel birim, çevrimler, saklama koşulları, lot, stoklanabilir, raf ömrü / yeniden test / en az kalan raf ömrü | — |
| UpdateItemCommand | item.updated | Item | b | Önce / sonra: değişen alanlar (çevrim listesi dahil) | — |
| SubmitItemCommand | item.submitted | Item | b | Durum Draft → PendingActivation, onay isteği kimliği | — |
| WithdrawItemApprovalCommand | item.approval-withdrawn | Item | b | Durum → Draft (ya da yeniden etkinleştirmede → Deactivated), iptal kanıtı | — |
| ApplyItemActivationDecisionCommand (işçi) | item.activated / item.reactivated / item.activation-rejected / item.activation-not-applicable | Item | b | Durum (ilk: → Active / Draft; yeniden: → Active / Deactivated), karar kanıtı (aktör, zaman, görev, geçiş sırası) | — |
| DeactivateItemCommand | item.deactivated | Item | b | Durum Active → Deactivated, gerekçe | — |
| RequestItemReactivationCommand | item.reactivation-submitted | Item | b | Durum Deactivated → PendingActivation (`ActivationKind = Reactivation`), gerekçe, onay isteği kimliği | — |
| RetireItemCommand | item.retired · ret: item.retire-refused (`Outcome = Failed`) | Item | b | Durum Deactivated → Retired, gerekçe, stok denetimi sonucu (sıfır / sıfır değil / ulaşılamadı) ve neden kodu | — |
| AddItemUomConversionCommand (S6) | item.uom-conversion-added | Item | b | Sonraki: from, to, pay, payda | — |
| RequestItemCriticalCorrectionCommand (sonraki dilim) | item.critical-correction-requested | Item | b | İstenen: kritik alan önce / sonra, onay isteği kimliği | — |
| ApplyItemCriticalCorrectionDecisionCommand (işçi, sonraki dilim) | item.critical-correction-applied / item.critical-correction-rejected | Item | b | Önce / sonra: kritik alan, karar kanıtı | — |
| SetGskuStockBehaviourCommand (S3) | gsku.stock-behaviour-updated | Gsku | b | Önce / sonra: temel birim, çevrimler, raf ömrü, saklama koşulları, lot, stoklanabilir | — |
| SetLskuStockBehaviourCommand (S3) | lsku.stock-behaviour-updated | Lsku | b | Önce / sonra: seri takibi (diğer alanlar GSKU'dan miras — O-3) | — |
| SetFinishedGoodStockBehaviourCommand (S3) | finished-good.stock-behaviour-updated | FinishedGood | b | Önce / sonra: temel birim, çevrimler, raf ömrü (kaynak dahil), saklama koşulları, lot, seri, stoklanabilir | — |
| AssignFinishedGoodGtinCommand (S3) | finished-good.gtin-assigned | FinishedGood | b | Önce / sonra: GTIN | — |

Kayda yazılmayan alan yok (kişisel veri, sır ya da belirteç taşımıyor). Platform denetim haritasına yeni işlemler
**yalnız ekleme** ile girer; `ProductAuditOperation` değerleri için 87'den önce CT'ye sorulur (2026-10-06 CT notu).

## 22. Record Codes (RCS-001)

| Kayıt türü | Sınıf | Biçim | Gerekçe |
|---|---|---|---|
| Kalem / Malzeme (`Item`) | **KALSIN ailesi — ürün kimliği ailesi** (RCS-001 §4); davranışı OTOMATİK: sistem üretir, kullanıcı değiştiremez | `IT-{12 hane}`, ör. `IT-000000000003` | RCS-001 §4 yeni ürün kimliği türünü (adıyla "Kalem / Malzeme, MOD-0290-FU04") bu aileye katar, RCS-001 `{ŞİRKET}-{TÜR}-{SAYAÇ}` biçimine değil. Kalem kiracı genelidir (Karar 6) → şirket öneki yoktur |

- **Sayaç:** ailenin tek sayacı — kiracı başına `mdm_canonical_code_counters` (GP / GS / LS / FG ile paylaşılır).
  Bu yüzden `IT` numaraları ardışık değildir; boşluk doğaldır (RCS-001 §5.8). Sayaç hiç sıfırlanmaz.
- **Defter:** ortak kod defteri, ayır → tüket → bağla; yeni `CodeBearingEntityType.Item` ve önek `IT`
  (`CodeReservationRepository.BuildCode` `switch`ine bir satır — S1). Kendi sayacı yazılmaz.
- **Benzersizlik:** `TenantId + ReservedCode`, türler arası tek ad alanı; emekli, iptal, süresi dolmuş ve yumuşak
  silinmiş kayıtların kodu da dolu sayılır ve **asla yeniden kullanılmaz**.
- **Değişmezlik:** kod oluşturulduktan sonra değiştirilemez; API de reddeder (güncelleme isteğinde kod alanı yoktur,
  gelirse 400) — yalnız ekran değil.
- **Biçim:** yalnız ASCII `A–Z`, `0–9`, `-`; kod uzunluğu **sabit 15** (2 harf + `-` + 12 hane). 10^12 tükenmesi kapsam dışı (CT kararı 4, 2026-10-07; S1 ölçüm notu).
- **Önek ölçümü (2026-10-07, bu dal):** MDM önek `switch`inde yalnız `GP`, `GS`, `LS`, `FG` var; depoda `"IT"` yalnız
  ülke kodu (İtalya, Platform sağlama listesi) ve bir iş birimi etiketi olarak geçiyor — kayıt kodu öneki olarak yok.
  Çakışma yok.
- **SOP kodu** (Karar 5c): ayrı, isteğe bağlı alan; sonra (P10), GMG-SCM-SOP-0001 koruma kuralıyla.
- **GSKU / LSKU / Bitmiş Ürün:** S3 kod üretmez; mevcut kodlar değişmez. GTIN bir kayıt kodu değildir (GS1
  tanımlayıcısı; girilir, doğrulanır, tekildir).
- **RCS-001 §6 kontrol listesi:** sınıf + gerekçe ✔ (bu bölüm); önek ölçüldü ✔; ortak kod servisi — ürün ailesi MDM
  defterini kullanır (§4 istisnası); İKİSİ DE değil; testler (S1): eşzamanlı iki oluşturma farklı kod, emekli kod
  yeniden kullanılmaz, kod ASCII, kod değiştirme reddi. **RCS-001 §3 tablosuna satır** — kural dosyası CT'nin (O-18).

## 23. Modül platform bağlantıları (10 satır)

| # | Bağlantı | Bu pakette ne olur | Dilim |
|---|---|---|---|
| 1 | Menü | Ana Veri altında "Kalemler" (`ITEMS`), yalnız `mdm.items.read` olana | S5 |
| 2 | Ctrl+K | `Nav.Page.ITEMS` kararlı kodla otomatik; yedi dilde aranır | S5 |
| 3 | l10n (7 dil) | en, tr, fr, es, zh, ar, ru — sayfa, form, "Stok davranışı" bölümü, liste etiketleri (O-22), hata kodu → cümle köprüsü; bir dil eksikse iş bitmemiştir | S1 / S3 (hata kodları) + S5 |
| 4 | Yetkisiz yüz (UAS-001) | İzinsiz kullanıcıya iskelet çizilmez, yönlendirme yapılmaz; GSKU / FG "Stok davranışı" bölümü izinsizde yok; ölçülür | S5 |
| 5 | Altın liste | `_ListShell` + `createList` (ilk MDM ürün ekranı); dokunma protokolü | S5 |
| 6 | Denetim | Yol b, §21; defterde yeni borç yok | S1–S6 |
| 7 | Manifest (kendini kaydetme) | `product-item-sku-master` modülüne `ITEMS` sayfası + eylemleri (ADD_NEW, VIEW_DETAILS, EDIT, SUBMIT, WITHDRAW_APPROVAL, DEACTIVATE, REACTIVATE, RETIRE); GSKU / FG sayfalarına `EDIT_STOCK_BEHAVIOUR` (O-12); modül sürümü 1.0.0 → 1.1.0; yeni modül yok, alan `MASTER-DATA-MANAGEMENT`. **S5 ön koşulu (S1 ölçümü):** Auth `EntitlementOnlyViewerPermissions` listesine `mdm.items.read` eklenir; eklenmezse Viewer şablonu tenant kapsamlı `*.read` ile yetkisiz Viewer'a verir → S5'te DUR | S5 |
| 8 | Plan / yetki | Katalog → Auth izin eşitlemesi; modül hakkı olan kiracının Admin'i izinleri plan eşitlemesiyle alır; kalite pozisyonu için rol önerisi; stok / satın alma servis kimlikleri (S4) | S1 / S4 / S5 |
| 9 | Gösterge | Onay işçisi: `manual_reconciliation` + `needing_a_person` (akış adı `ITEM_ACTIVATION`); denetim iletim hatası sayacı; stok uygunluk sorgusu ret / ulaşılamadı sayacı | S1 / S2 |
| 10 | Runbook | `docs/guides/operations/service-trust-chain-runbook.md`'ye kalem onay akışı (durum ⇔ çıkış tablosu, ayarlar, geri alma); dev'de onay zinciri; R-07 dev verisi adımları | S2 / S7 |

KVKK: kişisel veri yok; yalnız işlemi yapanın kullanıcı kimliği. Bildirim: onay görevi motordan ve Görev Merkezi'nden;
kalemin kendi bildirimi yok; olay yayını yok (MVP-1).

## 24. SAP / Oracle karşılaştırması

| Konu | SAP S/4HANA (malzeme ana verisi) | Oracle Fusion (Product Hub / Item Master) | Bu paket |
|---|---|---|---|
| Model | Tek malzeme kaydı; **malzeme türü** (ROH, VERP, HALB, FERT …) görünümleri, numara aralığını ve stok / değerleme davranışını belirler | Tek kalem ana kaydı; **kalem sınıfı** / kullanıcı kalem türü öznitelikleri belirler | Yazmada ayrı `Item` (altı malzeme türü) + ürün zinciri ayrı (Karar 1); okumada tek sözleşme, `itemKind` ile |
| Kapsam | Genel veri (istemci) + tesis / depo görünümleri | Ana organizasyonda tanımlanır, organizasyon atamasıyla açılır | Kiracı geneli (Karar 6); şirkete özgü stok verisi stokta (SAP tesis görünümünün karşılığı) |
| Numara | İç / dış numara aralığı, malzeme türüne bağlı | Kalem numarası, otomatik ya da elle | Sistem kodu `IT-` kalıcı, ürün ailesiyle tek sayaç; SOP kodu ayrı alan, sonra |
| Temel birim + çevrim | Stok oluştuktan sonra temel birim değişmez; alternatif birimler MARM'da pay / payda (`UMREZ` / `UMREN`) | Birincil birim + standart, sınıf içi ve kaleme özel çevrimler | Etkinleşince kilitli; yönlü pay / payda ondalık metin (MARM ile aynı fikir); aynı boyut listeden türetilir |
| Durum | Çapraz-tesis malzeme durumu (`MSTAE`) satın almayı engelleyip çıkışa izin verebilir | Kalem durumu (Active, Inactive, Obsolete …) "stoklanabilir / işlem görebilir / satın alınabilir" özniteliklerini sürer | Draft → Active → Deactivated (yeni giriş yok, çıkış var) → Retired; kuralı stok uygular |
| Silme / emeklilik | Silme işareti ve arşivleme stok varken yapılamaz | Durum "Obsolete" / silme eldeki stok varken engellenir | Stoğu sıfır olmayan kalem emekliye ayrılamaz; stok sorgusu ulaşılamazsa ret |
| Saklama | Ayrı iki alan: saklama koşulu (`RAUBE`) ve sıcaklık koşulu (`TEMPB`) | Kalem öznitelikleri / öznitelik grupları | Çoklu liste; en çok bir sıcaklık koşulu (CT kararı O-2) SAP'nin ayrımıyla uyumlu |
| Lot / raf ömrü | Parti yönetimi bayrağı; toplam raf ömrü (`MHDHB`), en az kalan raf ömrü (`MHDRZ`) | Lot / seri kontrolü, raf ömrü günleri, yeniden test aralığı | `LotControlled`, SKU düzeyi `SerialControlled`, `ShelfLifeDays`, `RetestDays`, `MinRemainingShelfLifeDays` (O-7) |
| GTIN | EAN / UPC birim başına (MEAN) | GTIN çapraz referansı birim başına | Bitmiş Ürün satış kutusu GTIN'i; koli / palet sonra (P9) |
| Onay | Değişiklik belgesi her değişikliği izler; etkinleştirme onayı kuruma göre (MDG değişiklik talebi) | Yeni kalem talebi / değişiklik siparişi onay akışı | Etkinleştirme + kritik alan düzeltmesi onaylı (Karar 4a); her değişiklik denetimde |

Fark ve gerekçe: SAP ve Oracle bitmiş ürünü de aynı kayıtta tutar; burada ürün zinciri zaten yazılmış ve kabul
sürecinde olduğu için ayrı kalır (A1 🔴 regresyon). Okuma tarafında ikisi gibi tek sözleşme verildiğinden tüketici farkı
`itemKind` alanından okur.

## 25. Follow-up Items ve açık maddeler

### 25.1 Açık maddeler ve CT kararları (2026-10-07)

Sahip ürün kararlarını CT'ye devretti; CT kararlarını 2026-10-07'de verdi. "Önerimiz" sütunu S0 taslağının önerisidir;
bağlayıcı olan "CT kararı" sütunudur.

| # | Konu | Önerimiz | CT kararı (2026-10-07) | Sonraki adım / sahip | Regresyon riski |
|---|---|---|---|---|---|
| O-1 | `getProduct` Global Ürün için v1.0 stok alanlarını (`baseUomId`, `shelfLifeDays`, `storageCondition`, `lotControlled`, `serialControlled` …) ne döndürecek? Uzlaşma bu alanları SKU düzeyine koyuyor | Global Ürün türünde bu alanlar dönmez (`ProductRef`'te `required` yok); stok davranışı `getSku`'dan; v1.0 şeması değişmez | **Kabul** — davranış daralması olarak işaretli: ProductRef'in v1.0 stok alanları `deprecated: true` + "alan yok = bilinmiyor, false / 0 varsayılmaz"; v1.0 örneği "v1.0-mock-only"; changelog draft.4 (S0-FIX1) | S4 uygular; **stok ekibinin yazılı onayı bekleniyor** (aşağıdaki soru 1) | 🟡 Prism'deki v1.0 örneği Global Ürünü stok alanlarıyla gösteriyor; o örneğe bağlanan tüketici `getSku`'ya geçmeli |
| O-2 | Saklama koşulunda sıcaklık kodu sayısı ve v1.0 tek değerli `storageCondition` | En çok bir sıcaklık kodu + istenen kadar koruma kodu; v1.0 alanı o sıcaklık kodunu taşır, yoksa null (SAP `RAUBE` / `TEMPB` ayrımı) | **Kabul** | S1 / S3 doğrulaması (422 `ITEM_STORAGE_CONDITION_CONFLICT`) | 🟢 |
| O-3 | LSKU'nun seri takibi dışındaki stok alanları | Okuma anında üst GSKU'dan miras; LSKU yalnız `SerialControlled` taşır | **Kabul** | S3 / S4; stok ekibine bildirilir (aşağıdaki liste) | 🟡 LSKU'da stok tutan tüketici GSKU değerlerini görür |
| O-4 | Ürün satırlarının iç durum eşlemesi ve v1.0 `Archived` | `Draft` / `PendingIdentityApproval` → `Draft`; `IdentityApproved` → `Active`; `Retired` → `Retired`; `Archived` hiç yayımlanmaz, görülürse `Retired` gibi | **Kabul** | S4 | 🟢 |
| O-5 | Ürün SKU'larının görünen adı (GSKU / FG'de ad alanı yok) | Okuma anında türetilir, saklanmaz: Global Ürün adı · paket miktarı + birimi (LSKU'da + pazar kodu) | **Kabul** | S4 | 🟢 |
| O-6 | LSKU `gtin` (v1.0 alanı) | v1.1'de null; GTIN Bitmiş Ürün satış kutusunda; LSKU tanımlayıcıları P6'da | **Kabul** | S4 | 🟢 |
| O-7 | Kalemde `MinRemainingShelfLifeDays` (uzlaşma tablosunda yok) | İsteğe bağlı alan; stok ekibine sorulsun | **İsteğe bağlı kalır; stok ekibine soru** | stok ekibi | 🟢 |
| O-8 | Eksik stok alanları; onaylı kayıtlarda ilk doldurma ve değişiklik | Zorunlu alanlar dolmadan `Stockable = true` olamaz; boş alanın ilk doldurulması onaysız; dolu değerin değiştirilmesi onaylı | **Kabul, ayrıntıyla:** boş alanın ilk doldurulması onaysız, denetimle; onaylı GSKU / Bitmiş Ürün'de ve etkin kalemde dolu değerin değiştirilmesi MOD-0023 onayıyla (kritik alan düzeltmesi). Düzeltme dilimi gelene kadar dolu değer değiştirilemez (409) | S1 / S3; kritik alan düzeltmesi sonraki dilim | 🟡 bugün onaylı kayıtların hepsi boş |
| O-9 | Malzeme türüne göre zorunlu değer | ACTIVE_INGREDIENT, EXCIPIENT, RAW_MATERIAL, INTERMEDIATE için `Stockable = true` iken `ShelfLifeDays` ya da `RetestDays`'ten en az biri; ambalajda isteğe bağlı | **Kabul** | S1 doğrulaması | 🟢 |
| O-10 | G0'daki `CONSUMABLE` (sarf malzemesi) | MVP-1'de yok | **MVP-1'de yok**; gerekirse sözleşme v1.2'de eklemeyle | — | 🟢 |
| O-11 | Emekliye ayırma için kiracı geneli stok sorgusu (INVENTORY-BUNDLE v1 `/availability` tüzel kişi kapsamlı) | Stok ekibi yalnız ekleme bir iç sorgu açar: kiracının bütün tüzel kişileri ve stok durumları, MDM servis kimliğiyle | **Emekliye ayırma reddedilir** (503 `ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE`) — stok ekibi kiracı geneli, MDM servis kimliğiyle çağrılabilen uygunluk cevabını verene kadar; stok ekibine soru. Aynı korumanın GSKU / LSKU / FG emekliye ayırmasına uygulanması bu kararın dışında, öneri olarak kalır (ebeveyn paket) | stok ekibi | 🟡 cevap gelmezse kalem emekliye ayrılamaz (güvenli taraf) |
| O-12 | GSKU / LSKU / FG stok davranışını kim, hangi izinle düzenler | Ayrı komut + ayrı izin (`mdm.<kaynak>.edit-stock-behaviour`) + ayrı offcanvas (SAP görünüm yetkisi, Oracle öznitelik grubu yetkisi) | **Kabul** | S3 / S5 | 🟢 |
| O-13 | Sözleşme okumalarının kullanıcı oturumu izni | Tek anahtar `mdm.product-master.read`; G0'daki `mdm.stockable-items.read` yerine | **Kabul** | S4 | 🟢 |
| O-14 | GSKU kutu ↔ adet çevrimi ve FG temel birimi | Kutu ↔ adet `PackQuantity` + `PackUomCode`'dan türetilir (C62 ise); FG temel birimi = üst GSKU'nunki | **Kabul** | S3 | 🟡 iki kaynaktan girilen çevrim çelişir |
| O-15 | FG kendi raf ömrü GSKU'nun ruhsatlı raf ömrünü aşabilir mi? | Hayır | **Kabul:** FG ≤ GSKU, doğrulamayla zorlanır (422 `FG_SHELF_LIFE_EXCEEDS_GSKU`) | S3 | 🟢 |
| O-16 | Ebeveyn MOD-0290 paketi GSKU / FG'de GTIN'i "deferred/prohibited" (paket-yerel BL-022) sayıyor, stok alanı taşımıyor | S3'ten önce ebeveyn paket revizyonu | **Yapıldı (2026-10-07):** ebeveyn §4 GSKU ve Bitmiş Ürün satırları yalnız ekleme ile revize edildi; eski metin silinmedi, "superseded 2026-10-07" işaretli | — | 🟢 |
| O-17 | Ağ geçidinde 64 KB başlık | Ölçüldü: ağ geçidi `Program.cs` 64 KB taşıyor (commit 9dfe859f2) | **Kapandı** | — | — |
| O-18 | RCS-001 §3 tablosunda kalem satırı yok | Kurala satır: "Kalem / Malzeme (MDM ürün kimliği ailesi) · KALSIN · `IT-` + 12 hane" | **CT satırı kendisi ekler**; bu paket kural dosyasını değiştirmez | CT | 🟢 |
| O-19 | BOM'un (MOD-0193) sahibi kişi / dal | MVP-3'e sorulur | **Sahip** | sahip → MVP-3 | 🟢 |
| O-20 | Kullanım dışı durumda hangi stok hareket türü giriş / çıkış / iade sayılır | Stok ekibinin DEC-INV-17 güncellemesinde | **Stok tarafı** (DEC-INV-17 güncellemesi) | stok ekibi | 🟢 |
| O-21 | `item-uom` ayrıntıları: kutu / koli için güncel UN/ECE Rec 20 kodu (`XBX` / `XCS`), uluslararası ünite, rulo | S1 tohumundan önce ölçülür | **S1'de tohumdan önce doğrulanır** | S1 | 🟢 |
| O-22 | Evrensel listelerin yedi dilde ekran etiketi | Etiket MDM resx'inden kararlı kodla | **S5'te ölçülür** | S5 | 🟢 |
| O-23 | Stok ekibinin istek listeleri (R-01 … R-09) bizim git'te yok | Stok ekibinden itmesi istenir | **Sahip** | sahip → stok ekibi | 🟢 |
| O-24 | Emekli kalemde ters kayıtla (REVERSAL) geri gelen stok | Stok yalnız hurda / silme ile kapatır (istisna kaydıyla); MDM tarafı değişmez | **Stok tarafı** (DEC-INV-17 güncellemesi) | stok ekibi | 🟡 kapatılamayan stok |
| O-25 | Taslak ve eksik kayıtlar sözleşmede nasıl görünür (S0-FIX1 incelemesi) | — | **CT kararı:** eksik zorunlu stok alanı SkuRef'te null, ProductRef'te alan yok; kayıt `stockable: false`. Kalemde `shelfLifeDays` / `retestDays` dolu ya da null (O-9). Ambalaj dışı kalemde boş `storageConditions` listesi `stockable: true`'yu engeller | S1 / S3 doğrulaması; S4 | 🟢 |
| O-26 | `stockable: false` kayıtta stok hareketi (S0-FIX1 incelemesi) | — | **CT kararı:** `stockable: false` kayıtta stok yeni hareket kabul etmez (ters kayıt hariç); durumdan bağımsız, hareket kuralına eklendi | stok uygular; sözleşmede yazılı | 🟢 |
| O-27 | Stok davranışı için hangi okuma yetkilidir (S0-FIX1 incelemesi) | — | **CT kararı:** `getSku` (kalem için `getSku(level=Item)`); `getProduct`'taki kalem alanları aynı değeri taşır. Kalemde `serialControlled: false` = uygulanmaz | S4 | 🟢 |

#### Stok ekibine gidecek sorular

0. **O-1 — yazılı onay isteği (davranış daralması)** — v1.1 sunucusu Global Ürün (`itemKind: GlobalProduct`) için
   `getProduct` yanıtında v1.0 stok alanlarını (`baseUomId`, `shelfLifeDays`, `minRemainingShelfLifeDays`,
   `storageCondition`, `retestDays`, `lotControlled`, `serialControlled`) artık döndürmez; bu alanlar `getSku`'dan
   (yetkili okuma) okunur. Alan yok = bilinmiyor; false / 0 varsayılmaz. Prism'deki v1.0 örneği bu alanları gösterdiği
   için ona bağlandıysanız `getSku`'ya geçmeniz gerekir. Bunu kabul ettiğinizi yazılı olarak teyit eder misiniz?
1. **O-7** — Kalemde "mal kabulde en az kalan raf ömrü" (`MinRemainingShelfLifeDays`) sizin için gerekli mi? Bizde
   isteğe bağlı alan olarak duruyor; gerekliyse hangi kalemlerde zorunlu olmalı?
2. **O-11** — Emekliye ayırma denetimi için kiracının **bütün tüzel kişilerini** ve bütün stok durumlarını kapsayan,
   **MDM servis kimliğiyle** çağrılabilen "bu SKU'da sıfırdan farklı stok var mı" cevabını INVENTORY-BUNDLE'a yalnız
   ekleme olarak verebilir misiniz? Bugünkü `/availability` tek tüzel kişi kapsamlı. Cevap gelene kadar MDM kalemi
   emekliye ayırmayı reddeder.
3. **O-20** — Kullanım dışı (`Deactivated`) kalemde hangi hareket türleri kabul: müşteri iadesi (`CUSTOMER_RETURN`),
   depolar arası / yer / durum transferi, sayım fazlası (`COUNT_GAIN`)? Lütfen DEC-INV-17 güncellemesine yazın.
4. **O-24** — Emekli kalemde ters kayıtla (`REVERSAL`) geri gelen stoğu nasıl kapatacaksınız? Önerimiz: yalnız hurda /
   silme, istisna kaydıyla; DEC-INV-17'de.
5. **O-3 (bilgi)** — LSKU'nun stok alanları (temel birim, çevrimler, raf ömrü, saklama koşulları, lot, stoklanabilir)
   okuma anında üst GSKU'dan gelir; LSKU yalnız seri takibini kendisi taşır. `getSku(level=Lsku)` bu alanlarda
   GSKU'nun değerlerini döner.
6. **O-23** — İstek listeleriniz (R-01 … R-09, `mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md`)
   bizim git'te yok; dalınıza itebilir misiniz?

### 25.2 Follow-up

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F1 | ~~Stok ekibinin Karar 2 cevabı~~ — **kapandı** (kabul, 2026-10-07); kalan: CT'nin v1.1'i dondurması | CT | 🟡 dondurulmazsa tüketiciler Prism'de kalır |
| F2 | GMG-SCM-SOP-0001 v0.3 → SOP kodu alanı (P10), ebeveyn SOP koruma kuralıyla | sahip / CT | 🟢 sistem kodu kalıcı, sonradan eklenir |
| F3 | Kalite birimine GxP sınıfı teyidi (b gelirse ortak iletici işi önce) | sahip | 🟡 |
| F4 | `product-master-bundle` dosyasını beş klasörden birine taşıma (`docs/reference/architecture/contracts/`), taşıma protokolüyle | CT | 🟢 |
| F5 | Registry'de MOD-0290-FU03 (Ürün Tüzel Kişi Kapsamı) satırı bu dalda yok; FU numaraları tüm dallarda tek kaynaktan ölçülmeli | CT yönetişim | 🔴 yeni çakışma riski |
| F6 | Bileşim ve madde (P5), LSKU / kalem GTIN'i (P6), ambalaj hiyerarşisi (P9), değerleme, tehlike sınıfı | ebeveyn iş listesi | 🟢 |
| F7 | Satın Alma'nın izin verici doğrulayıcısının gerçek istemciyle değişimi | Satın Alma ekibi | 🟡 bugün her kimlik geçiyor |
| F8 | `verify_module_id.py` yalnız bulunduğu dalın registry'sini okuyor. Öneri: `--check-id` aday kimliği tüm dallarda `git log --all -S "<ID>"` ile de arasın; başka paket adıyla iz varsa BLOCKED dönsün | CT (DCP-002 sahibi) | 🔴 yeni çakışma riski sürüyor |

### Orchestrator handoff

Module pack `draft` olarak hazır (S0 revizyonu). Geliştirme için status `ready-for-dev` olmalıdır (§19); sonra S1 için
ayrı CT prompt'u. Golden Reference **compact** şablon alındı — sapma yok.
