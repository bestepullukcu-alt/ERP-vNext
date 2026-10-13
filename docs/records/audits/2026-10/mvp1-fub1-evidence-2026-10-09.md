# WP-MVP1-FUB1 — kanıt (2026-10-09)

> **İstem:** CT-MVP1-FUB1 v1, taban `9697fda76`, dal `feature/mvp1-fub1` (yerel, push yok).
> **Kapsam:**
> - MOD-0022 **FU-0022-VIS-02** (doğrulamada şirket kapsamı).
> - MOD-0173 FU01 **O-PM1-6** (stok defterinde seviye etiketi, ürün seçicisinde ürün adı).
>
> **Durum:** tamamlandı; ikisi de kapandı. Sözleşme, şema, imza işleyicisi ve InventoryService değişmedi. Lots ve Quarantine Web
> dosyalarına (ITEM) dokunulmadı. Yeni izin anahtarı yok.
>
> **Betikler:** `mvp1-fub1-scripts-2026-10-09.md`.

## 1. Doğrulama tüketicileri ölçümü (NE madde 1)

`verifySignature`'ı (`POST /api/v1/esign/signatures/{id}/verify`) HTTP üzerinden çağıran tek tüketici e-İmza Doğrulama ekranı
(`frontend/Diten.Web/wwwroot/assets/js/ESignature/verification.js:66`). Ekran zaten "bulunamadı" yanıtını gösteriyor, yani yeni
404 onu kırmıyor.

| tüketici | nasıl doğruluyor | kanıt (dosya:satır) | FUB1'den etkilenir mi |
|---|---|---|---|
| E4 Workflow onay kapısı | uygulama içi `IESignatureVerifier.VerifyAsync` (handler'ı değil) | `services/Diten.Platform/src/Diten.Platform.Application/Features/ESignature/Services/ESignatureGate.cs:124` | hayır |
| MOD-0175 Q3 karar veren imzası | HTTP `GET /api/v1/esign/signatures/{id}` (+ `GET policy/effective`); doğrulama ucu yok | `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/ESign/GatewayESignClient.cs:31,51`; `.../Quarantine/Services/QuarantineDeciderSignature.cs:17` | hayır (getir değişmedi) |
| Dışa aktarma ve arama | uygulama içi doğrulayıcı (durum sütunu) | `ExportHandlers.cs:154`, `SignatureHandlers.cs:775` | hayır |
| Doküman yönetimi (FU23) | ayrı uç `/api/v1/document-management/signatures/{id}/verify` | `DocumentManagementSignaturesController.cs:169` | hayır (MOD-0022 kaydı değil) |

Hiçbir modül HTTP doğrulamasına bağlı değil → BLOCKED koşulu yok.

## 2. Yapılanlar

**Platform** (`6df0be02b`):
- `VerifySignatureCommandHandler`: kontrol `HidesTenantLevel(record)` → `!scope.CanSee(record)` oldu, yani arama ve dışa
  aktarmayla aynı tek kural (`ESignatureVisibilityScope`):
  - imzalayan kendi kaydını görür;
  - şirketli kayıt yalnız kapsamdaysa görünür, değilse **404** (kayıt yokmuş gibi);
  - şirketsiz kayıt yalnız read sahibine görünür (VIS1 aynen).
- Getir değişmedi. `HidesTenantLevel` korunuyor; şirketli kayıtta kapısı hâlâ konu sağlayıcısı. Bunun nedeni Q3'ün getir ucunu
  kullanması ve istemin yalnız doğrulamayı istemesi. Açık nokta §7'de.
- `ESignatureVisibilityScope.HidesTenantLevel` açıklaması güncellendi: yalnız getir kullanıyor.

**Web** (`02905f3ea`):
- **Stok defteri listesi:**
  - `SkuLevelItem` anahtarı `StockLedgerIndex.{7}.resx`'e eklendi (değerler hareketler sayfasının mevcut etiketleri:
    "Malzeme kalemi", "Material item", …).
  - `_IndexL10n.cshtml` ve `index.l10n.js` gerekli anahtar listesine eklendi.
  - "Item" artık ham görünmüyor. Gsku / Lsku / FinishedGood zaten etiketliydi. Hareket listesinde dört anahtar da vardı.
- **Ürün seçicisi** (`ProductPickerSource.SearchProductsAsync`):
  - Global Product satırı (seviye ≠ Item) ürünün kendi adını `GET /api/product-master/products/{itemId}` ile okuyor
    (getProduct). Bu, stok defterinin etiket çözücüsünün zaten kullandığı sözleşme okuması
    (`MasterLabelResolver.cs:59`). Sayfadaki her ürün için en çok bir çağrı yapılıyor.
  - Metin = "kod · ürün adı"; SKU adı **ikinci satırda** (`PickerOption.Sub`).
  - Malzeme kalemi değişmedi. Okunamayan ürün bugünkü metni koruyor (seçici bozulmuyor).
  - SKU seçicisi değişmedi (yalnız `stockable-items`).
- **Sunum:**
  - `StockMovementsController.Lookups` `sub` alanını taşıyor.
  - Üç seçici (defter filtresi, hareket listesi filtresi, hareket formu) Select2 `templateResult` ile iki satır çiziyor; metin
    jQuery `text()` ile kaçırılıyor.
- **PM1 koruma testi:** `Product_and_sku_pickers_call_only_the_contract_search_for_Active_records` genişletildi.
  - İzin verilen istekler: `stockable-items` araması ile ürün seçicisinin tek ürün okuması (`products/{itemId}`).
  - Malzeme kalemi için okuma yapılmadığı da kontrol ediliyor.
  - `status=Active` kontrolü yalnız arama isteklerine uygulanıyor.
  - Bu değişiklik PM1'in kabullü korumasını bilerek genişletiyor; CT'nin görmesi için burada belirtildi.

## 3. Test sayıları

| paket | önce (`9697fda76`) | sonra (`02905f3ea`) |
|---|---|---|
| Platform Application.Tests, filtre `~ESign\|~Workflow\|~WorkAggregation\|~ModuleRegistration\|~ModulePages`, `DITEN_TEST_MONGOD` | **613 / 613** | **615 / 615** (+2) |
| Diten.Web.Tests (tam) | **685 / 685** | **687 / 687** (+2) |
| tests/architecture | **18 / 18** | **18 / 18** |
| InventoryService, filtre `~Quarantine` (Q3 dahil), **kendi** mongod'umda (`INVENTORY_TEST_MONGO`, 27017 değil) | — | **141 / 141** (kod değişmedi; koruma koşusu) |

**Derleme:**
- 0 hata.
- Değişen dosyalarda uyarı yok (`--no-incremental`, değişen dosya adlarıyla süzüldü).
- Release (AuthService, Platform, Gateway, Web): 0 hata.

**Resx eşitliği:**
- `StockLedgerIndex.{7}`: +1 anahtar her dilde.
- Yeni test `Every_sku_level_is_labelled_in_the_ledger_and_movement_lists_in_seven_languages` her seviyeyi 7 dilde iki
  liste için kontrol ediyor: boş değil, ham değer değil, İngilizce kopyası değil (Gsku / Lsku hariç).
- Mevcut `InventoryResxParityTests` yeşil.

## 4. Senaryolar (önce kırmızı dahil)

| # | senaryo | FUB1 öncesi | sonrası |
|---|---|---|---|
| S1 | Kapsam dışı şirketin kaydını doğrulama (read + verify, kapsam = şirket 1, kayıt şirket 2) | **200** — `Verify_of_a_company_record_outside_the_callers_scope_is_not_found` **KIRMIZI** | **404** |
| S2 | Kapsam içi şirket kaydını doğrulama | 200 | 200 |
| S3 | İmzalayan kendi şirket kaydını (kapsamı yok) doğrular | 200 | 200 (`The_signer_verifies_their_own_company_record_even_outside_their_scope`) |
| S4 | Şirketsiz kayıt (VIS1'in 6 testi) | yeşil | yeşil (değişmedi) |
| S5 | E4 onay kapısı (`WorkflowESignatureGateTests`, filtre içinde) ve Q3 (`~Quarantine` 141) | yeşil | yeşil (değişmedi) |
| S6 | Stok defteri listesinde `Item` satırı etiketli: eski defter etiket dosyalarıyla (`9697fda76`) | **KIRMIZI** (`Every_sku_level_is_labelled…`) | yeşil |
| S7 | Seçicide ürün adı: eski seçici davranışıyla (ürün okuması yok, SKU adı metin) | **KIRMIZI** (`The_product_picker_names_a_Global_Product…`) | yeşil |

S1 testleri koddan önce koşuldu. S6 ve S7 önce-kırmızısı, commit'ten sonra eski dosyaları / eski davranışı geçici olarak geri
getirerek ölçüldü (`sabotage.ps1` BEFORE-1/2) ve geri alındı.

## 5. Sabotajlar (K3) — `sabotage.ps1`, her biri `git checkout` ile geri alındı

| # | bozma | kırmızı test |
|---|---|---|
| (a) | Doğrulamadaki yeni kapsam kontrolü kaldırıldı (`!CanSee` → VIS1'in `HidesTenantLevel`'ı) | `Verify_of_a_company_record_outside_the_callers_scope_is_not_found` (1/8) |
| (b) | Etiket eşlemesi kaldırıldı (defter listesi ham seviyeyi yazıyor: `skuLevelText = (level) => level \|\| ''`) | `Every_sku_level_is_labelled_in_the_ledger_and_movement_lists_in_seven_languages` (1/23) |

Geri alındıktan sonra Web 23/23 ve Platform 8/8 yeşil; `git status --porcelain` boş.

## 6. Çalışma zamanı probu (kendi yığın) — `probe.js`, 6 / 6 PASS

**Yığın:**
- İzole mongod: rastgele port, asla 27017.
- AuthService 15056, Platform 15057, gateway 15191, Web 15001.
- **Kendi MDM taklidim 15059** (`mdm-stub.js`; filonun 5059'u değil). Yalnız
  `GET /api/legal-entities/{id}/lookup-validation`'a iki şirket için yanıt veriyor.
- Token'lar kendi AuthService girişinden. İmza bileti kendi `esign/reauth`'undan, gerçek (test) parolayla.

| # | adım | beklenen | gerçek |
|---|---|---|---|
| F1 | Yönetici, şirket 1 (LE1) için org birimi + pozisyon oluşturur; A'yı atar (gerçek Platform org API) | 2xx ×3 | 201 / 201 / 201 |
| F2 | B bir workflow görevini imzalar (kiracı düzeyi); harness bu kaydı LE1 ve LE2 kaydı olarak kopyalar | sign 201, 2 kopya | 201 (`legalEntityId` null); ESIG-FUB1-63B968D3 (LE1), ESIG-FUB1-83D277B4 (LE2) |
| F3 | **A (kapsam = yalnız şirket 1) doğrular:** şirket 1 / şirket 2 / kiracı düzeyi | 200 / **404** / 200 | 200 / **404** / 200 |
| F4 | A araması aynı kuralı izler | şirket 1 + kiracı düzeyi; şirket 2 yok | yalnız LE1 kopyaları ve kiracı düzeyi kayıtlar (önceki denemelerin LE1 kopyaları dahil), LE2 yok |
| F5 | B (imzalayan) şirket 2 kaydını doğrular | 200 | 200 |
| F6 | Kapsam gerçek zincirden geliyor: Platform `OrgDataScopeResolver` → `MdmLegalEntityReferenceValidator` → kendi taklidim | LE1 lookup-validation çağrısı | 6 çağrı (taklit günlüğü) |

**Neden kopya:** Yığında yalnız Platform var. Şirket alanını dolduran tek sağlayıcı InventoryService'in karantina konusu;
workflow-task ve doküman konuları şirketsiz. Bu yüzden şirketli kayıtlar, gerçek bir imzanın yığının kendi DB'sinde
`LegalEntityId` değiştirilerek kopyalanmasıyla üretildi (testteki `CopyAsync` yöntemi). Kopyanın özeti değiştiği için durumu
`tampered` / `changed` olabilir. Ölçülen şey görünürlük: 200 ya da 404.

**Probun kendi sorunları** (düzeltildi, kanıt için yazıldı):
1. Harness adaptasyon betiğim `split().join(() => b)` ile DB adlarına fonksiyon metni yazdı. Ben düzeltilene kadar
   AuthService seed edemedi; harness kendi süreçlerini kapattı.
2. İlk MDM taklidi Node'un varsayılan başlık sınırıyla (16 KB) büyük yönetici token'ına **431** döndü. Taklit kendi PID'iyle
   kapatılıp `--max-http-header-size=262144` ile yeniden açıldı (PM1 Prism'deki aynı ayar).
3. Org API'si `data` alanında doğrudan id döndürüyor; prob `data.id` bekliyordu.

## 7. Süreçler / filo

**Sahibin filosu öncesi ve sonrası aynı** (istek gönderilmedi; 4010 ve 5059 dahil):

```
4010:19992 5000:17980 5001:10452 5056:16912 5057:16548 5058:3300 5059:9532 5060:36016 5061:32468 5066:12644 27017:4904
```

**Kendi süreçlerim** (hepsi yalnız kendi PID'leriyle kapatıldı; sonrasında hiçbiri yok, 15001 / 15056 / 15057 / 15059 /
15191 boş):
- test mongod 15088 (Q3 koşusu).
- İlk yığın denemesi: auth 18816.
- Yığın: 32748 (auth), 35116 (platform), 23668 (gateway), 35508 (web) ve EphemeralMongo.
- MDM taklidi 35276 ve 42184.

## 8. Açık noktalar (CT)

- **O-FUB1-1 — getir (`getSignature`) şirketli kayıtta veri kapsamı uygulamıyor.**
  - Kapısı VIS1 öncesindeki gibi konu sağlayıcısı.
  - İstemin "arama / getir ile aynı" ifadesi getir için ölçümle uyuşmuyor; getirin şirket kuralı yok.
  - Getir değiştirilirse MOD-0175 Q3'ün `GET signatures/{id}` okuması etkilenir (kapsam dışı karar verici imzası 404 olur).
  - Bu WP yalnız doğrulamayı değiştirdi. Getir için karar CT'nin.
- **O-FUB1-2 — PM1 koruma testinin genişlemesi** (§2): ürün seçicisi artık getProduct okuması da yapıyor. CT kabul ederse
  W-UI-01 kapanış notuna eklenmeli.
- **Push öncesi:** MVP-1 / main senkronu gerekiyor. Bu WP push yapmadı.
