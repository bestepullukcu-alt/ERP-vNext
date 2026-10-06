---
id: MOD-0290-FU04
name: Item / Material Master
parent: MOD-0290
parent_name: Product / Item / SKU Master
domain: master-data-management
service: Diten.MdmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "YOK (G0 belge dilimi). Kod G1'den itibaren, her dilim ayrı CT prompt'u ve bu paketin `ready-for-dev` onayıyla açılır."
owner: module-pack-author
branch: docs/mdm/item-master-g0
started: 2026-10-06
target: G1 sahibin/CT'nin `ready-for-dev` onayıyla
form_field_count: 10
data_classification: "Kiracı içinde herkese açık ana veri (grup geneli, şirket kapsamı yok); kişisel veri yok."
audit_class: "Ana veri (Karar 3a) — yol b, merkezi günlük; günlük yazılamazsa iş durmaz, uyarı kalır."
dependencies:
  - MOD-0290 (Product / Item / SKU Master — ebeveyn; kod defteri, durum akışı, onay bağı, denetim yolu b)
  - MOD-0023 (ortak onay motoru — etkinleştirme ve kritik alan düzeltmesi onayı; onaycı = kalite pozisyonu)
  - MOD-0288 (pozisyon dizini — onaycı pozisyonla çözülür)
  - MOD-0048 (iş referans verisi — yeni `item-uom` ve `storage-condition` listeleri; mevcut `uom` listesine dokunulmaz)
  - MOD-0018 (RBAC / katalog → Auth izin eşitlemesi — yalnız tüketim)
  - MOD-0021 (merkezi denetim günlüğü — yol b iletimi)
  - MOD-0141 / MOD-0142 / MOD-0143 / MOD-0145 (Satın Alma — okuma sözleşmesinin tüketicileri; doğrulayıcı değişikliği onların dosyası)
  - Stok (MOD numarası stok ekibinde) — okuma sözleşmesinin tüketicisi; G3 dondurulması onların cevabını bekler
  - DEV-0001 (Golden Reference Compact)
---

# MOD-0290-FU04 — Item / Material Master (kalem / malzeme ana kaydı)

> **DRAFT (2026-10-06) · G0 belge dilimi · KOD YOK.**
> Şirketin hammadde, ambalaj malzemesi, yarı mamul ve sarf malzemesini kaydedeceği yer bugün sistemde yok. Satın Alma,
> Stok ve Üretim bu kayda bağlanmak zorunda; kayıt gelmezse her biri kendi kalem tablosunu yazar ve aynı malzeme üç
> yerde üç kimlikle yaşar. Bu paket P1 tasarımını
> ([mdm-item-master-p1-design-2026-10-03.md](../../../../docs/roadmap/plans/mdm-item-master-p1-design-2026-10-03.md),
> öneriler A2 + C2) yetkili bir Module Pack taslağına çevirir.
>
> **DCP-002 kimlik kapısı — PASS (2026-10-06):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU04 --name "Item / Material Master" --parent MOD-0290`
> → `OK MOD-0290-FU04: proven against Blueprint/registry.`
> **Tüm dallarda ölçüldü:** `git log --all -S "MOD-0290-FU0{4..10}"` = 0 commit; her dalda `git grep` = 0.
> **Neden FU03 değil:** tasarım belgesi FU03 öneriyordu, ama `MOD-0290-FU03` başka dallarda *Product Legal Entity
> Scope Assignment* paketidir (MOD-0018-FU21/FU22 onu tüketici olarak anar; 590 iz). Bu dalın registry'sinde satırı
> olmadığı için doğrulayıcı FU03'e de "OK" der — Marka'da bulunan çakışmanın aynısı. Doğrulayıcı yalnız bu dalın
> registry'sini okur; numara tüm dallarda ayrıca ölçülmelidir.

## 0. Kararlar (sahip devretti, CT verdi — 2026-10-06)

| # | Soru | Karar | Bu pakete etkisi |
|---|---|---|---|
| 1 | Bitmiş Ürün bir kalem türü mü? | **(a)** FG ayrı kalır | Ayrı `Item` kümesi (A2). FG'ye dokunulmaz; okuma sözleşmesi ikisini tek biçimde döner |
| 2 | Stok ekibi: `skuLevel = Item`, kalem kimliği = SKU kimliği? | **"kabul" ile ilerle** | Sözleşme v1.1 böyle yazıldı. ⚠ **AÇIK SORU — sahibin stok ekibine iletmesi bekleniyor.** G1 başlayabilir; **G3 dondurulması (FROZEN) onların cevabını bekler** |
| 3 | GxP sınıfı | **(a) ana veri** | Merkezi günlük (yol b); kayıt yazılamazsa iş durmaz, uyarı kalır (§21) |
| 4 | Onay ne zaman? | **(a)** yalnız etkinleştirme + kritik alan düzeltmesi | Onaycı = **kalite pozisyonu**, MOD-0023 üzerinden pozisyonla (§2.4) |
| 5 | Kalem kodu | **(c)** sistem kodu kalıcı, SOP kodu ayrı alan | v1'de yalnız sistem kodu `IT-{12 hane}`. SOP kodu (P10) GMG-SCM-SOP-0001 v0.3 sahipten gelince, ebeveyn paketin SOP koruma kuralıyla (§2.3) eklenir |
| 6 | Görünürlük | **(a) grup geneli** | **"Kalem, kiracı içinde herkese açık ana veridir; şirket (tüzel kişi) kapsamı yoktur."** Ürün Tüzel Kişi Kapsamı (MOD-0290-FU03) kaleme uygulanmaz |

Hiçbir karar GP / GSKU / FG kurallarıyla çelişmez: FG ve GP olduğu gibi kalır; şirket kapsamı yalnız ürün tarafında
kalır; SOP kodu ebeveyn paketin koruma kuralına tabi kalır.

## 1. Module Summary

`Item`, MOD-0290'ın Blueprint 8.1'de sahip olduğu **kalem kayıtları ve kalem yaşam döngüsünü** taşıyan yeni kümedir
(nesne anahtarı "ProductID + ItemID + SKUID" — kalem kimliği ürün kimliğinden ayrı). Dört kalem türü:

| Tür kodu | Anlam | SAP karşılığı |
|---|---|---|
| `RAW_MATERIAL` | Hammadde (etken ve yardımcı madde dahil — etken/yardımcı ayrımı tür değil, bileşimdeki roldür, P5) | ROH |
| `PACKAGING_MATERIAL` | Ambalaj malzemesi (kutu, folyo, prospektüs, etiket) | VERP |
| `SEMI_FINISHED` | Yarı mamul (bulk, granül) | HALB |
| `CONSUMABLE` | Sarf malzemesi (filtre, eldiven, laboratuvar sarfı) | HIBE |

Bitmiş ürün (FERT karşılığı) kalem türü **değildir**; MOD-0290'ın Finished Good kaydı olarak kalır (Karar 1).

## 2. Ownership and Boundaries

### 2.1 SoR kararı

- Kalem kaydının tek kaynağı (SoR) MOD-0290 / `Diten.MdmService`'tir. Satın Alma, Stok ve Üretim **yalnız kimlik
  saklar ve okuma sözleşmesiyle okur**; yerel kalem tablosu açılmaz.
- CRM'in "Product" kaydı (Model B) kalem değildir ve stok tarafından tüketilmez (DEC-INV-17).
- Yazma tarafı ayrıdır (A2: `Item` ≠ `FinishedGood` ≠ `GlobalProduct`); okuma tarafı SAP ve Oracle gibi **tek
  "stoklanabilir kalem" listesi** gösterir (§16).

### 2.2 In-scope (P1, G1–G5)

- `Item` kümesi: taslak oluştur / düzenle, onaya gönder, geri çek, etkinleştirme kararını uygula, kullanım dışı
  bırak, yeniden etkinleştir, emekliye ayır; sonraki dilimde onaylı kritik alan düzeltmesi.
- Kod defterinde yeni tür `IT` (ayır → tüket → bağla; kod yeniden kullanılmaz).
- MOD-0048: `item-uom` (UN/ECE Rec 20, boyut, ondalık hassasiyet, boyutun temel birimine çarpan) ve
  `storage-condition` listeleri.
- Okuma sözleşmesi `product-master-bundle` v1.1 (yalnız ekleme, §16).
- `ITEMS` sayfası (compact), yedi dil, menü, Ctrl+K.
- P2 (G5): kaleme özel birim çevrimi.

### 2.3 Out-of-scope / yetkisiz

- Bitmiş Ürünü kaleme taşımak (Karar 1b reddedildi).
- Şirket (tüzel kişi) kapsamı (Karar 6); tedarikçi ve onaylı tedarikçi listesi (Satın Alma); spesifikasyon (kalite);
  değerleme sınıfı (stok); GTIN (P6); bileşim / madde bağı (P5, ebeveyn BL-015); tehlikeli madde sınıflandırması;
  elektronik imza (BL-519 — etkinleştirme onayı + denetim izi yeterli).
- SOP-kontrollü malzeme kodu üretimi: ebeveyn paketin **GMG-SCM-SOP-0001 delivery guard** kuralı aynen geçerlidir
  (DCP-005 onayı + yetkili sahip paketi + açık kod başlatma izni). Karar 5'teki SOP kodu ayrı, kalıcı sistem
  kodundan bağımsız bir alandır; sistem kodu (`CanonicalCode`) aşırı yüklenmez.
- Toplu eski veri aktarımı (ebeveyn BL-023); genel `manage` izni; `delete` / `bulk-delete`.

### 2.4 Onay bağlantısı (MOD-0023)

| Konu | Kural |
|---|---|
| Ne onaylanır | Etkinleştirme (Taslak → Etkin), **yeniden etkinleştirme** (Kullanım dışı → Etkin) ve etkin kalemde **kritik alan düzeltmesi** (sonraki dilim). Etkin kalemde yalnız ad ve açıklama onaysız, denetim iziyle düzenlenir; diğer bütün alanlar kritiktir (tasarım §C) |
| Onaycı | **Kalite pozisyonu** — MOD-0023 adayı pozisyonla verir, motor pozisyonu kişilere çözer, ilk karar veren karar verir (GP-1b kalıbı) |
| Yapan ≠ onaylayan | Kendi kaydını onaylayan kişinin kararı uygulanmaz (GP "self approval ignored" kuralı); `platform_admin` atlatması bu alan kuralını geçemez |
| Bağlantı noktası | GP / GSKU kalıbı (`ProductIdentityWorkflowBinding`, güvenli başlatma, iz arama); G2'de LSKU kopyası yerine **paylaşılan** bağlantı noktası |
| Kalıcı kanıt | Karar kanıtı (aktör, zaman, görev, geçiş sırası) kalemle birlikte saklanır ve denetime iletilir |

## 3. Owned Objects

| Kök | Sorumluluk | Depolama |
|---|---|---|
| `Item` | Kalem kimliği, türü, temel birimi, stok davranışı alanları, yaşam döngüsü, onay bağı, denetim niyetleri | `mdm_items` (kiracı + `IsDeleted` filtresi) |
| `ItemWorkflowOperation` | Etkinleştirme / düzeltme onay isteğinin dayanıklı işlemi (GSKU / GP kalıbı) | `mdm_item_workflow_operations` |
| `ItemUomConversion` (P2, G5) | Kaleme özel alternatif birim `{alternatif birim, pay, payda}` (SAP MARM gibi) | `Item` içinde gömülü liste |
| `CodeReservation` (tür `IT`) | Kod ayırma / tüketme / bağlama | mevcut kod defteri |

Yazma komutları (hepsi §21'de): `CreateItemCommand`, `UpdateItemCommand`, `SubmitItemCommand`,
`WithdrawItemApprovalCommand`, `ApplyItemActivationDecisionCommand` (işçi), `DeactivateItemCommand`,
`RequestItemReactivationCommand`, `RetireItemCommand`, `RequestItemCriticalCorrectionCommand` (sonraki dilim),
`ApplyItemCriticalCorrectionDecisionCommand` (işçi, sonraki dilim), `AddItemUomConversionCommand` (G5).

## 4. Entity Fields (v1)

`Item : EntityBase` (kiracı kapsamlı). `form_field_count = 10` (alttaki tabloda "formda" sütunu). Etkin kalemde yalnız
`Name` ve `Description` onaysız düzenlenir; "kilitli (kritik)" alanlar yalnız onaylı kritik alan düzeltmesiyle değişir
(tasarım §C — raf ömrü, yeniden test ve saklama koşulu GxP açısından stok davranışını değiştirir).

| Alan | Tür | Formda | Etkinleşince | Not |
|---|---|---|---|---|
| `CanonicalCode` | string `IT-000000000001` | hayır (sistem) | değişmez | Kod defteri; yeniden kullanılmaz |
| `Name` | string 1–200 | evet | denetimle düzenlenir | Kiracı + tür içinde, emekli olmayanlar arasında tekil (normalize anahtar) |
| `NameNormalized` | string | hayır | — | Tekillik anahtarı (GP ad kuralı) |
| `Description` | string 0–2000 | evet | denetimle düzenlenir | |
| `ItemType` | enum `RAW_MATERIAL` · `PACKAGING_MATERIAL` · `SEMI_FINISHED` · `CONSUMABLE` | evet | **kilitli** (kritik) | Stok ve MRP davranışı buna göre dallanır |
| `BaseUomCode` + `BaseUomSelection` | MOD-0048 `item-uom` + seçim kanıtı (`ReferenceCatalogSelection`) | evet | **kilitli** (kritik) | SAP'de de stok oluştuktan sonra temel birim değişmez |
| `LotTracked` | bool | evet | **kilitli** (kritik) | Stok kuralı `LOT_REQUIRED` |
| `SerialTracked` | bool | evet | **kilitli** (kritik) | Stok kuralı `SERIAL_COUNT_MISMATCH` |
| `ShelfLifeDays` | int? 1–3650 | evet | **kilitli** (kritik) | |
| `RetestDays` | int? 1–3650 | evet | **kilitli** (kritik) | Hammadde çoğunlukla son kullanma yerine yeniden test tarihiyle izlenir |
| `MinRemainingShelfLifeDays` | int? 0–3650, ≤ `ShelfLifeDays` | evet | **kilitli** (kritik) | Mal kabulde en az kalan raf ömrü |
| `StorageConditionCode` + seçim kanıtı | MOD-0048 `storage-condition` | evet | **kilitli** (kritik) | |
| `LifecycleStatus` | `Draft` · `PendingActivation` · `Active` · `Inactive` · `Retired` | hayır | — | §8 |
| `WorkflowBinding` | onay bağı | hayır | — | GP kalıbı |
| `AuditIntents` / `AuditIntentReceipts` | denetim niyetleri ve makbuzları | hayır | — | Yol b |
| `Version` | int | hayır | — | İyimser kilit (CAS) |

**v1'de yok:** şirket kapsamı, madde bağı, tedarikçi, GTIN, SOP kodu, değerleme sınıfı, tehlikeli madde.

**Eski belge tablosu (GP / GSKU dersi):** yeni alan ilk günden vardır; sonradan eklenecek her alan için koşul
`Eq(false)` / `Eq(null)` / `Eq(0)` eksik alanı da eşlemelidir ve test alansız bir belgeyle yapılır.

### 4.1 Yeni MOD-0048 listeleri (G0'da tanımlanır, G1'de tohumlanır)

Mevcut `uom` listesine dokunulmaz (GSKU'ya özel; yayın kontrolü birebir karşılaştırıyor). Üçüncü bir birim sözlüğü
açılmaz: `item-uom`, GSKU'nun beş değerini alt küme olarak içerir.

| Liste | Kapsam | Öznitelikler | v1 değerleri (öneri) |
|---|---|---|---|
| `item-uom` | global, kod sahipli | `DimensionCode` (COUNT, MASS, VOLUME, LENGTH, AREA), `MaximumDecimalPrecision`, `FactorToDimensionBase` | C62 adet, MGM mg, GRM g, KGM kg, MC µg, MLT ml, LTR l, MTR m, MTK m², `IU` (uluslararası ünite — UN/ECE kodu yok; boyut COUNT değil `ACTIVITY`, çevrimsiz), RO rulo, BX kutu |
| `storage-condition` | global, kod sahipli | `TemperatureMinC`, `TemperatureMaxC`, `ProtectFromLight`, `ProtectFromMoisture` | AMBIENT_15_25, BELOW_25, BELOW_30, COOL_2_8, FROZEN_BELOW_MINUS_18, CONTROLLED_ROOM_20_25 |

Değer listeleri kalite birimiyle teyit edilir (§19); kalem türü (`ItemType`) MOD-0048 listesi **değildir** — davranış
dallandıran sabit bir kümedir (SAP'de malzeme türü de kod yapılandırmasıdır).

## 5. Repo Scope (G1–G5; G0'da yalnız belgeler)

| Dilim | Yollar |
|---|---|
| G0 | bu paket (§4.1 MOD-0048 liste tanımları dahil) · `execution/registries/module-id-registry.md` (FU04 satırı) · `execution/domains/master-data-management/domain-config.md` (bağlantı) · `docs/analysis/contracts/product-master-bundle.openapi.yaml` (v1.1, yalnız ekleme) |
| G1 | `services/Diten.MdmService/src/**/Features/Items/**`, depo + dizinler, kod defteri `IT` türü, Platform `business-reference-data` tohumuna iki yeni liste, testler (`MdmHttpHost`, BL-527) |
| G2 | onay işlemcisi + paylaşılan bağlantı noktası, Platform onay şablonu / güven kaydı (dev) |
| G3 | okuma sözleşmesi uçları (`/api/product-master/*`, `/api/internal/v1/product-master/validate`); Satın Alma doğrulayıcısı **Satın Alma ekibinin dosyası** |
| G4 | `frontend/Diten.Web/Views/MasterDataManagement/Items/**`, `wwwroot/assets/js/MasterDataManagement/Items/**`, resx (7 dil), kendini kaydetme |
| G5 | birim çevrimi |

## 6. Protected Paths

- `gateway/**/ocelot*.json` — rota eklemesi entegrasyon işidir (integration-agent), bu paketin dilimleri yazmaz.
- `frontend/Diten.Web/Views/Shared/**` — paylaşılan yerleşim yalnız ortak liste bileşeni üzerinden tüketilir; değişmez.
- Mevcut MOD-0048 `uom` listesi (GSKU'ya özel; yayın kontrolü birebir karşılaştırıyor) — dokunulmaz.
- GP / GSKU / LSKU / FG kümeleri ve işlemcileri — dokunulmaz; paylaşılan onay bağlantı noktası G2'de yalnız
  çıkarılır, davranışları değişmez (onların testleri yeşil kalır).
- Platform: yalnız ekleme (yeni denetim işlemleri ve referans listeleri); silme / değiştirme yok.

## 7. Dependencies

| Bağımlılık | Durum | Engel mi |
|---|---|---|
| Denetim yolu b (`mdm-merkezi-iletim`) | **Var** (`tests/architecture/audit-ledger/Diten.MdmService.md` — `IAuditableCommand + IAuditMetadataProvider`) | Hayır |
| MDM HTTP test barındırıcısı (`MdmHttpHost`, gerçek Mongo) | **Bu dalda yok.** `feature/mdm/product-five-takeover` (5cd68d609'dan beri; @ 23fb2d85f) ve `feature/mdm/product-fg-accept` dallarında var; `main`'de yok | **G1 için evet:** G1 o dalın üstünden açılır ya da önce birleşmesi beklenir |
| MOD-0023 onay motoru + pozisyonla aday | Var (GP-1b) | G2 için dev'de "kalite pozisyonu" ve şablon kurulumu gerekir |
| MOD-0048 yeni listeler | Yok | G1'in ilk işi |
| Stok ekibinin cevabı (Karar 2) | **Bekleniyor** | G3 dondurulması için evet |
| Kalite birimine GxP sınıfı teyidi (Karar 3 notu) | Sorulacak | Hayır (karar a ile ilerlenir) |
| GMG-SCM-SOP-0001 v0.3 | Sahipte | Yalnız SOP kodu alanı için |

## 8. Runtime Constraints — yaşam döngüsü (C2) ve dayanıklılık ilkeleri

```text
Draft ──Submit──▶ PendingActivation ──onay (MOD-0023)──▶ Active ──Deactivate──▶ Inactive ──Retire──▶ Retired
  ▲                    │   │                                 ▲                      │
  └──── Withdraw ──────┘   └── red / kapandı-kararsız ──▶ Draft │                      │
                                                                └─ onay (MOD-0023) ◀── RequestReactivation
                    (yeniden etkinleştirme de PendingActivation'dan geçer; red / geri çekme → Inactive)
                                                        (Retired geri dönmez)
```

| Geçiş | İzin | Onay | Not |
|---|---|---|---|
| oluştur / düzenle (Draft) | `create` / `update` | yok | Serbest düzenleme |
| Draft → PendingActivation | `submit` | başlatır | Taslak kilitlenir |
| PendingActivation → Draft | `withdraw` | geri çekme | Motorda iptal |
| PendingActivation → Active | — (işçi) | **onaylandı** | Yapan ≠ onaylayan |
| PendingActivation → Draft | — (işçi) | reddedildi / kapandı-kararsız / kendi onayı | Taslak açılır |
| Active → Inactive | `deactivate` | yok | Yeni belgede seçilemez; mevcut stok okunur |
| Inactive → PendingActivation → Active | `submit` (istek) · `withdraw` (geri çekme) | **onaylı** — ilk etkinleştirmeyle AYNI yol (MOD-0023, kalite pozisyonu; yapan ≠ onaylayan) | **Tasarıma ek, CT kararı (2026-10-06):** yeniden etkinleştirme bir etkinleştirmedir (Karar 4). İşlem `ActivationKind = Reactivation` taşır; red / kapandı-kararsız / kendi onayı / geri çekme kalemi **Inactive**'e döndürür (Draft'a değil). SAP: malzeme durum değişikliği onaylı değişiklik talebiyle; Oracle: kalem durumu Etkin'e dönerken onay akışı tetiklenir |
| Inactive → Retired | `retire` | yok | Geri dönmez; kod yeniden kullanılmaz. Etkin kalem önce kullanım dışı bırakılır (tasarım §C sırası) |
| Kritik alan düzeltmesi (Active) | `update` | **onaylı** | Sonraki dilim |

**Kabul edilmiş GSKU kalıbı (WP-MDM-GSKU-ACCEPT-01, GP BL-552) — G2'nin tasarım kuralı:**

- **(A)** Kalem ancak motor bu istek için hiçbir şey tutmadığı biliniyorsa taslağa döner / kilidi bırakır: başlatma
  araması ↔ başlatma cevabı ayrıdır; "hiçbir şey gelmedi" yalnız bekleme penceresinden sonra güvenilir; motorun üç
  cevabı (açık → bekle; kapandı-kararsız → taslağa dön; çelişkili kanıt → takılı + uzlaştırma). **Sahipsiz onay olmaz.**
- **(B)** Hiçbir durum sonsuza dek, görünmeden, adlı bir çıkışı olmadan takılı kalmaz: her takılı durumun çıkışı ya
  yapanın aynı komutu ya kişidir (BL-530); iki gösterge sayar (`manual_reconciliation`, `needing_a_person`); depo
  "manuel uzlaştırma + yeniden denenebilir" çiftini reddeder; yazım sınıfları (kendiliğinden geçen → bekle,
  eşzamanlı → sayılı tekrar, diğer → takılı); makbuz bilen tekrar; yeni alanlar `IgnoreExtraElements` ile ve açılış
  denetimiyle.
- Takılı onay isteği taslağı **tutar** (GP akış 3 S2 kararı ile aynı).

## 9. Layout & Shell Contract

Tüm `Views/MasterDataManagement/Items/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **açıkça** (kiracı kabuğu; MDM'nin
diğer ekranlarıyla aynı). Yetkisiz kullanıcıya iskelet çizilmez ve yönlendirme yapılmaz (UAS-001,
`.antigravity/rules/unauthorized-surface-standard.md`).

## 10. Backend File Convention

```text
services/Diten.MdmService/src/Diten.MdmService.Application/Features/Items/
├── Commands/   CreateItemCommand · UpdateItemCommand · SubmitItemCommand · WithdrawItemApprovalCommand ·
│               ApplyItemActivationDecisionCommand · DeactivateItemCommand · RequestItemReactivationCommand · RetireItemCommand ·
│               RequestItemCriticalCorrectionCommand · ApplyItemCriticalCorrectionDecisionCommand (sonraki dilim) ·
│               AddItemUomConversionCommand (G5)
├── Queries/    GetItemListQuery · GetItemByIdQuery · GetStockableItemListQuery
├── Handlers/
│   ├── CommandHandlers/  CreateItemHandler · UpdateItemHandler · SubmitItemHandler · WithdrawItemApprovalHandler ·
│   │                     ApplyItemActivationDecisionHandler · DeactivateItemHandler · RequestItemReactivationHandler ·
│   │                     RetireItemHandler · RequestItemCriticalCorrectionHandler ·
│   │                     ApplyItemCriticalCorrectionDecisionHandler · AddItemUomConversionHandler
│   └── QueryHandlers/    GetItemListHandler · GetItemByIdHandler · GetStockableItemListHandler
├── Validators/ CreateItemValidator · UpdateItemValidator
├── Workflow/   ItemWorkflowProcessor (G2; paylaşılan bağlantı noktasıyla)
└── ItemModels.cs
```

Handler / Validator adlarında `Command` / `Query` soneki yok. `Delete` / `BulkDelete` **yok** (ebeveyn paket kuralı:
iş yaşam döngüsü `retire` ile biter).

## 11. Frontend File Contract (Compact)

```text
Views/MasterDataManagement/Items/
├── Index.cshtml       (Layout açıkça; liste = ortak liste bileşeni `_ListShell` + `createList`, altın liste)
├── Create.cshtml · Edit.cshtml · Details.cshtml
├── _Form.cshtml       (iki bölüm: Kimlik · Stok davranışı)
├── _Filter.cshtml · _DataTable.cshtml · _IndexL10n.cshtml
└── ItemsIndex.cs
wwwroot/assets/js/MasterDataManagement/Items/ index.js · index.l10n.js
```

`_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **yok** (compact). Satır içi CSS yok (FG-003). Liste,
ilk MDM ürün ekranı olarak ortak liste bileşenini kullanır (BL-440 dokunma protokolü).

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| Name | Evet | Trim, 1–200, kontrol karakteri yok | Tekil dizin (kiracı, tür, normalize ad, emekli değil) | `NameExistsAsync` |
| Description | Hayır | Trim, ≤ 2000 | — | — |
| ItemType | Evet | Dört değerden biri; Active'de değişmez | — | — |
| BaseUomCode | Evet | MOD-0048 `item-uom` etkin değeri + seçim kanıtı; Active'de değişmez | — | referans çözümleyici |
| LotTracked / SerialTracked | Evet | bool; Active'de değişmez | — | — |
| ShelfLifeDays | Hayır | 1–3650 | — | — |
| RetestDays | Hayır | 1–3650 | — | — |
| MinRemainingShelfLifeDays | Hayır | 0–3650; `ShelfLifeDays` varsa ≤ ona | — | — |
| StorageConditionCode | Hayır | MOD-0048 `storage-condition` etkin değeri + seçim kanıtı | — | referans çözümleyici |
| ExpectedVersion | Evet (yazımlarda) | Mevcut sürüm | CAS | — |

İlişkili: `SerialTracked = true` iken `BaseUomCode` sayılabilir (boyut `COUNT`) olmalı.

## 13. Failure Path to Verify (G1'in HTTP testleri, gerçek Mongo)

| Durum | Beklenen |
|---|---|
| Kimliksiz | 401 |
| İzinsiz | 403 (yetkisiz yüz UAS-001) |
| Başka kiracının kalemi | 404 (ifşa yok) |
| Geçerli oluşturma | 201 + `IT-` kodu |
| Aynı ad + tür | 409 `ITEM_NAME_CONFLICT` |
| Geçersiz birim / saklama koşulu | 422 |
| Eşzamanlı yazım (eski sürüm) | 409 `ITEM_CONCURRENCY_CONFLICT` |
| Aynı Idempotency-Key ile tekrar | ilk cevap, ikinci yazım yok |
| Active'de ad / açıklama dışı alan düzenleme | 409 `ITEM_CRITICAL_FIELD_LOCKED` |
| Active kalemi doğrudan emekliye ayırma | 409 (önce kullanım dışı) |
| Kendi kaydını onaylama | uygulanmaz, taslağa döner |
| Denetim günlüğü yazılamıyor | iş tamamlanır, uyarı günlüğü + gösterge (Karar 3a) |
| Retired kalemi geri alma | 409 |

## 14. Authorization Convention

Kiracı servisi biçimi `mdm.<kaynak>.<eylem>`; genel `manage` **yok**; `delete` **yok**.

| Anahtar | İnsana verilir | Açıklama |
|---|---|---|
| `mdm.items.read` | evet | Liste ve detay |
| `mdm.items.create` | evet | Taslak oluştur |
| `mdm.items.update` | evet | Taslak düzenle; etkin kalemde serbest alanlar; kritik düzeltme isteği |
| `mdm.items.submit` | evet | Etkinleştirme ve yeniden etkinleştirme (REACTIVATE) onayına gönder |
| `mdm.items.withdraw` | evet | Onay isteğini geri çek |
| `mdm.items.deactivate` | evet | Kullanım dışı bırak (DEACTIVATE) |
| `mdm.items.retire` | evet | Emekliye ayır |
| `mdm.stockable-items.read` | evet | Ekranlar için stoklanabilir kalem arama (kalem + FG) |

**İnsana verilemeyenler (rol ataması yapılmaz):**

| Yetki | Kim | Neden |
|---|---|---|
| Etkinleştirme kararını uygulama | MDM işçisi (servis kimliği) | Karar motordan gelir; insan "onayla" düğmesi kalemde yok — onay Görev Merkezi'nde, kalite pozisyonundaki kişi tarafından verilir |
| Servisler arası doğrulama `/api/internal/v1/product-master/validate` | Satın Alma / Stok servis kimlikleri (güvenilir tüketici, client credential) | Sunucudan sunucuya; anahtar adı G3'te servis güven zinciriyle kesinleşir |
| Kod ayırma tüketimi / süresi dolma | sistem | Ebeveyn paket kuralı |

Yapan / onaylayan ayrımı izinle değil, alan geçişinde insan kimliği karşılaştırılarak uygulanır.

## 15. Gateway / API Routing Decision

Gerekli. Ekran uçları `/api/mdm/items/*` ve sözleşme uçları `/api/product-master/*` ağ geçidinden kullanıcı oturumuyla;
servisler arası `/api/internal/v1/product-master/validate` ağ geçidi dışından servis kimliğiyle. Rota eklemesi
korumalı dosyadır → integration-agent görevi (G3 / G4 ile).

## 16. Okuma sözleşmesi — `product-master-bundle` v1.1 (yalnız ekleme)

Dosya: [`docs/analysis/contracts/product-master-bundle.openapi.yaml`](../../../../docs/analysis/contracts/product-master-bundle.openapi.yaml).
v1.0 parçaları değişmez ve FROZEN kalır; v1.1 eklemeleri **G3'e kadar DRAFT** (stok ekibinin cevabı, Karar 2).

| Ekleme | Ne |
|---|---|
| `SkuLevel` | `+ Item` (`Gsku`, `Lsku`, `FinishedGood` aynen) |
| `LifecycleStatus` | `+ PendingActivation`, `Inactive`, `Retired` (v1.0 değerleri aynen; tüketiciler bilinmeyen değeri "seçilemez" saymalı) |
| `GET /items/{itemId}` | `MaterialItemRef`: kod, ad, tür, temel birim, lot / seri, raf ömrü, yeniden test, en az kalan raf ömrü, saklama koşulu, durum |
| `GET /stockable-items` | Arama: kod / ad, `skuLevel`, `itemType`, durum, sayfalama; kalem ve Bitmiş Ürün **aynı biçimde** (`StockableItemRef`) |
| `/validate` | `skuLevel: Item` kabul eder; `itemId` hem Global Ürün hem kalem kimliğini çözer |
| `/skus/{skuId}?level=Item` | Kalemde **kalem kimliği = SKU kimliği** (`skuId == itemId`); stok satırının üçlü kimliği değişmeden çalışır |

**Anlam notu:** v1.0'da `itemId` "stok satırının bağlandığı kimlik" = Global Ürün kimliğidir. v1.1'de malzeme
kalemleri için aynı alan kalemin kendi kimliğidir; `skuLevel` hangisi olduğunu söyler. Kimlikler UUID'dir, çakışmaz.
**Kapsam notu:** v1.0 "Tenant + LegalEntity scoped" der; kalemler grup geneli olduğu için (Karar 6) kalem satırlarına
tüzel kişi süzmesi uygulanmaz — ürün satırlarının kapsamı aynen kalır.
**Hata kuralı:** sunucular 503 aldığında "sessizce geçer" demez (kapalı-başarısız); bilinmeyen kalem 422 `UNKNOWN_ITEM`
(Satın Alma kuralı).

## 17. Acceptance Criteria (dilim başına)

- **G1:** §13 tablosunun her satırı `MdmHttpHost` üzerinde gerçek Mongo ile bir HTTP testiyle ölçülür; kod `IT-` ile
  ve tekrarsız; ad tekilliği emekli olmayanlar arasında; alansız eski belge testi; yol b iletimi her yazma komutu için
  (§21) testle; `IgnoreExtraElements` + açılış denetimi.
- **G2:** motorun üç cevabı, bekleme penceresi, yazım sınıfları, iki gösterge, depo değişmezi, yapan ≠ onaylayan,
  takılı onayın taslağı tutması — her biri adlı test ve sabotajla kırmızı.
- **G3:** §16 uçları; `skuLevel=Item` ile `/validate`; kalem + FG tek liste; 503'te kapalı-başarısız.
- **G4:** Tüm `Views/MasterDataManagement/Items/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` açıkça;
  `verify_datatable_page.py` + `quality-gate-datatable`; yedi dil anahtar eşitliği; yetkisiz yüz (iskelet yok,
  yönlendirme yok); dar ekran; Ctrl+K'da "Kalemler"; canlı sayfa kontrolü.
- **G5:** aynı boyutta çevrim listeden türetilir; kaleme özel çevrim eklenir, değiştirilmez, silinmez.

## 18. Test Expectations

Yeni testler MDM HTTP barındırıcısında gerçek (geçici) mongod ile (BL-527); kural kendi kopyasıyla değil üretim koduyla
ölçülür; her koruma için sabotaj kanıtı (çapa tam bir kez, kırmızı = adlı test); Web tarafında hata kodu → cümle
köprüsü (7 dil) ve kod taraması (`GskuRefusalBridgeTests` kalıbı).

## 19. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (FU04, tüm dallarda ölçüldü)
- [x] Altı karar işlendi (§0)
- [x] Denetim sınıfı ve yol (§21)
- [x] Okuma sözleşmesi v1.1 taslağı (§16)
- [ ] Registry'de FU03'ün (Ürün Tüzel Kişi Kapsamı) bu dala yansıması — CT yönetişim işi
- [ ] Stok ekibinin cevabı (Karar 2) — G3 dondurulmasından önce
- [ ] Kalite pozisyonunun dev'de kurulması — G2'den önce
- [ ] `storage-condition` değer listesinin kalite birimiyle teyidi — G1'den önce
- [ ] Paket `ready-for-dev` (CT)

## 20. Implementation Notes — prompt sırası (tasarım §G)

| Sıra | Dilim | İçerik | Risk |
|---|---|---|---|
| G0 | Belge | Bu paket + sözleşme v1.1 + registry / domain-config | 🟢 |
| G1 | Sunucu çekirdeği | Taslak oluştur / düzenle, kod `IT`, §4.1 listelerinin Platform tohumu, denetim yol b, HTTP testleri (§13) | 🟡 ortak kod `switch`i; `MdmHttpHost` bağımlılığı (§7) |
| G2 | Etkinleştirme onayı | MOD-0023, paylaşılan bağlantı noktası, (A)/(B) | 🟡 |
| G3 | Okuma sözleşmesi | §16 uçları + Satın Alma'nın gerçek doğrulayıcısı (Satın Alma ekibi; G2 ile paralel) | 🟡 stok cevabı |
| G4 | Ekranlar | Compact, ortak liste, 7 dil, menü, Ctrl+K, canlı kontrol | 🟢 |
| G5 | Birim çevrimi (P2) | `ItemUomConversion` | 🟢 |

Tasarım belgesi "yaklaşık 10 prompt" der ama altı dilim (G0–G5) listeler; her dilim düzeltme turlarıyla birden çok
prompt sürebilir. G6+ yoktur; yeni dilim CT kararıdır.

## 21. Audited Events (AUD-001)

Servisin denetim altyapısı **var**: `mdm-merkezi-iletim` (yol b). Defterde yeni borç açılmaz. Sınıf: ana veri (Karar 3a)
— iletim hatası işi durdurmaz; uyarı günlüğü ve gösterge kalır.

| Komut | Olay adı | Nesne | Yol | Önceki / sonraki ya da değişen alanlar | İstisna |
|---|---|---|---|---|---|
| CreateItemCommand | item.created | Item | b | Sonraki: kod, ad, tür, temel birim, lot / seri, raf ömrü alanları, saklama | — |
| UpdateItemCommand | item.updated | Item | b | Önce / sonra: değişen alanlar | — |
| SubmitItemCommand | item.submitted | Item | b | Durum Draft → PendingActivation, onay isteği kimliği | — |
| WithdrawItemApprovalCommand | item.approval-withdrawn | Item | b | Durum → Draft, iptal kanıtı | — |
| ApplyItemActivationDecisionCommand (işçi) | item.activated / item.reactivated / item.activation-rejected / item.activation-not-applicable | Item | b | Durum (ilk: → Active / Draft; yeniden: → Active / Inactive), karar kanıtı (aktör, zaman, görev, geçiş sırası) | — |
| DeactivateItemCommand | item.deactivated | Item | b | Durum Active → Inactive, gerekçe | — |
| RequestItemReactivationCommand | item.reactivation-submitted | Item | b | Durum Inactive → PendingActivation (`ActivationKind = Reactivation`), gerekçe, onay isteği kimliği | — |
| RetireItemCommand | item.retired | Item | b | Durum → Retired, gerekçe | — |
| RequestItemCriticalCorrectionCommand (sonraki dilim) | item.critical-correction-requested | Item | b | İstenen: kritik alan önce / sonra, onay isteği kimliği | — |
| ApplyItemCriticalCorrectionDecisionCommand (işçi, sonraki dilim) | item.critical-correction-applied / item.critical-correction-rejected | Item | b | Önce / sonra: kritik alan, karar kanıtı | — |
| AddItemUomConversionCommand (G5) | item.uom-conversion-added | Item | b | Sonraki: alternatif birim, pay, payda | — |

Platform denetim haritasına yeni işlemler **yalnız ekleme** ile girer; `ProductAuditOperation` değerleri için 87'den
önce CT'ye sorulur (2026-10-06 CT notu).

## 22. Modül platform bağlantıları (10 satır)

| # | Bağlantı | P1'de ne olur | Dilim |
|---|---|---|---|
| 1 | Menü | Ana Veri altında "Kalemler" (`ITEMS`), yalnız `mdm.items.read` olana | G4 |
| 2 | Ctrl+K | `Nav.Page.ITEMS` kararlı kodla otomatik; yedi dilde aranır | G4 |
| 3 | l10n (7 dil) | en, tr, fr, es, zh, ar, ru — sayfa, form, hata kodu → cümle köprüsü; bir dil eksikse iş bitmemiştir | G1 (hata kodları) + G4 |
| 4 | Yetkisiz yüz (UAS-001) | İzinsiz kullanıcıya iskelet çizilmez, yönlendirme yapılmaz; ölçülür | G4 |
| 5 | Altın liste | `_ListShell` + `createList` (ilk MDM ürün ekranı); dokunma protokolü | G4 |
| 6 | Denetim | Yol b, §21; defterde yeni borç yok | G1–G5 |
| 7 | Manifest (kendini kaydetme) | `product-item-sku-master` modülüne `ITEMS` sayfası + eylemleri (ADD_NEW, VIEW_DETAILS, EDIT, SUBMIT, WITHDRAW_APPROVAL, DEACTIVATE, REACTIVATE, RETIRE — REACTIVATE tasarıma ek ve onaylı, §8); modül sürümü 1.0.0 → 1.1.0; yeni modül yok, alan `MASTER-DATA-MANAGEMENT` | G4 |
| 8 | Plan / yetki | Katalog → Auth izin eşitlemesi; modül hakkı olan kiracının Admin'i izinleri plan eşitlemesiyle alır; kalite pozisyonu için rol önerisi | G1 / G4 |
| 9 | Gösterge | Onay işçisi: `manual_reconciliation` + `needing_a_person` (akış adı `ITEM_ACTIVATION`); denetim iletim hatası sayacı | G2 |
| 10 | Runbook | `docs/guides/operations/service-trust-chain-runbook.md`'ye kalem onay akışı: durum ⇔ çıkış tablosu, ayarlar, geri alma satırı; dev'de onay zinciri kurulumu | G2 |

KVKK: kişisel veri yok; yalnız işlemi yapanın kullanıcı kimliği. Bildirim: onay görevi motordan ve Görev Merkezi'nden;
kalemin kendi bildirimi yok.

## 23. SAP / Oracle karşılaştırması

| Konu | SAP S/4HANA (MM01, malzeme ana verisi) | Oracle Fusion (Product Hub / Item Master) | Bu paket |
|---|---|---|---|
| Model | Tek malzeme kaydı; **malzeme türü** (ROH, VERP, HALB, HIBE, FERT) görünümleri, numara aralığını ve stok / değerleme davranışını belirler | Tek kalem ana kaydı; **kalem sınıfı** ve kalem şablonu öznitelikleri belirler | Yazmada ayrı `Item` (dört tür) + FG ayrı (Karar 1); okumada tek "stoklanabilir kalem" listesi |
| Kapsam | Genel veri (istemci) + tesis / depo görünümleri (MARC / MARD) | Ana organizasyonda tanımlanır, **organizasyon atamasıyla** kullanım organizasyonlarına açılır | Grup geneli (Karar 6); şirkete özgü stok verisi stok modülünde kalır (SAP'nin tesis görünümü karşılığı) |
| Numara | İç / dış numara aralığı, malzeme türüne bağlı | Kalem numarası, otomatik ya da elle | Sistem kodu `IT-` kalıcı; SOP kodu ayrı alan, sonra (Karar 5c) |
| Temel birim | Stok oluştuktan sonra değiştirilemez; alternatif birimler MARM | Birincil birim + birim çevrimleri (standart, sınıf içi, kaleme özel) | Etkinleşince kilitli; kaleme özel çevrim G5 (MARM gibi) |
| Durum | Malzeme durumu (çapraz-tesis / tesise özgü) kullanımı engeller | Kalem durumu (Active, Inactive, Obsolete…) işlevsel öznitelikleri sürer | Draft → PendingActivation → Active → Inactive → Retired |
| Onay | Değişiklik belgesi her değişikliği izler; etkinleştirme onayı kuruma göre (MDG değişiklik talebi) | Yeni kalem talebi / değişiklik siparişi onay akışı | Etkinleştirme + kritik alan düzeltmesi onaylı (Karar 4a); her değişiklik denetimde |
| Lot / raf ömrü | Parti yönetimi bayrağı; minimum kalan raf ömrü, toplam raf ömrü (MARA) | Lot / seri kontrolü, raf ömrü günleri, yeniden test aralığı | `LotTracked`, `SerialTracked`, `ShelfLifeDays`, `MinRemainingShelfLifeDays`, `RetestDays` |

Fark ve gerekçe: SAP ve Oracle bitmiş ürünü de aynı kayıtta tutar; burada FG zaten yazılmış ve kabul sırasında olduğu
için ayrı kalır (A1 🔴 regresyon). Okuma tarafında ikisi gibi tek liste verildiğinden tüketici farkı görmez.

## 24. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F1 | Stok ekibinin Karar 2 cevabı; G3 dondurulması | sahip → stok ekibi | 🟡 cevap gelmezse iki ana veri doğabilir |
| F2 | GMG-SCM-SOP-0001 v0.3 → SOP kodu alanı (P10), ebeveyn SOP koruma kuralıyla | sahip / CT | 🟢 sistem kodu kalıcı, sonradan eklenir |
| F3 | Kalite birimine GxP sınıfı teyidi (b gelirse ortak iletici işi önce) | sahip | 🟡 |
| F4 | `product-master-bundle` dosyasını beş klasörden birine taşıma (`docs/reference/architecture/contracts/`), taşıma protokolüyle | CT | 🟢 |
| F5 | Registry'de MOD-0290-FU03 (Ürün Tüzel Kişi Kapsamı) satırı bu dalda yok; FU numaraları tüm dallarda tek kaynaktan ölçülmeli | CT yönetişim | 🔴 yeni çakışma riski |
| F6 | Bileşim (P5), GTIN (P6), değerleme, tehlikeli madde | ebeveyn iş listesi | 🟢 |
| F7 | Satın Alma'nın izin verici doğrulayıcısının gerçek istemciyle değişimi | Satın Alma ekibi | 🟡 bugün her kimlik geçiyor |
| F8 | `verify_module_id.py` yalnız bulunduğu dalın registry'sini okuyor (Marka'da ve burada iki kez tuzak). Öneri: `--check-id` aday kimliği tüm dallarda `git log --all -S "<ID>"` ile de arasın; başka bir paket adıyla iz varsa BLOCKED dönsün | CT (DCP-002 sahibi) | 🔴 yeni çakışma riski sürüyor |

### Orchestrator handoff

Module pack `draft` olarak hazır. Geliştirme için status `ready-for-dev` olmalıdır; sonra G1 için ayrı CT prompt'u.
Golden Reference **compact** şablon alındı — sapma yok.
