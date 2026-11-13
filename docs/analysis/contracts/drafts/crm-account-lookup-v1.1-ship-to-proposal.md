# CRM-ACCOUNT-LOOKUP 1.1.0 — U-4 sevkiyat adresi okuma (TASLAK, ek)

    contract       : CRM-ACCOUNT-LOOKUP
    version        : 1.1.0 (taslak)
    x-status       : DRAFT (2026-11-13, CT commercial-suite) — 1.0.0'a yalnız ekleme: yeni uç U-4 ve yeni izin anahtarı.
                     U-1, U-2, U-3, §4–§9'daki 1.0.0 kuralları DEĞİŞMEZ. Donma: pack eki onayı + DL-3 CT K13 kabulü.
    x-base         : docs/analysis/contracts/crm-account-lookup.md (FROZEN 1.0.0, main 3870486d6)
    x-owner        : MOD-0149 Customer 360 / Account Hierarchy (commercial-suite, Diten.CrmService, port 5061)
    x-consumers    : Adaylar (istek, bağlayıcı değil): MOD-0178 — WMS ekibinin "çıkış sevkiyatı formu v2" isteği
                     (2026-11-13; MOD-0178 paketi bugün bu okumayı içermez, hizalanınca tüketici olur — sahip SCE);
                     MOD-0183 Shipment Tracking / MOD-0186 Reverse Logistics (MVP-6; bugün sözleşmesiz
                     `GET /api/crm/accounts/{id}` + `crm.account.read`, §0)
    x-record       : docs/records/decisions/2026-11/mod0149-ship-to-owner-decisions-01.md (K-12…K-19, WMS isteği / K-14)
    x-decisions    : K-12 tasarım (1.1 içinde ayrı uç), K-13 izin anahtarı `crm.account-ship-to.read` — commercial-suite
                     sahibi Beste, 2026-11-13. K-18 çoklu teslimat adresi yapılacak (Beste, 2026-11-13); gövde bugünden
                     listedir (§2.2). K-9 (MVP-6 adres yüzeyi) bu ekle kapanır (donmada).
                     Q-DEPOT-SOR kapandı (Beste, 2026-11-13): ecza depoları CRM Account (`wholesaler`), kendi depolarımız
                     (warehouse) CRM'de değil.
    x-measured-at  : origin/main 5a099cadc (2026-11-13)

## 0. Neden

- **İhtiyaç:** sevkiyatı hazırlayan kullanıcı alıcıyı CRM müşteri kaydından seçer; sevkiyat hesabın kimliğini
  (`customerRef` = `Account.Id`, SHIPMENT-BUNDLE 3.5.0) ve adresinin o anki kopyasını tutar. Hesap yoksa elle giriş sürer
  (tüketicinin kuralı).
- **1.0 neden yetmez:** U-1/U-3 adres taşımaz (1.0 §2.2, §2a.2). Onları koruyan `crm.account.lookup-validation` Hazine'nin
  AR rollerinde (#14, #16) var; adresi oraya eklemek AR'a adres açar ve K-1'i genişletir.
- **Bugünkü sözleşmesiz yol (ölçüldü):** MVP-6 Web adaptörü `GET /api/crm/accounts/{id}` gövdesinden ad, kod ve dört adres
  alanını okuyor (`frontend/Diten.Web/Models/SupplyChain/Shipments/ShipmentViewModels.cs:62-98`, `ShipmentCustomer.ReadDetail`;
  hedef metni `Destination()`, CT kararı 2026-11-03 SB-23). O uç tüm kaydı döner (sorumlu kişi ad / telefon / e-posta,
  notlar, dış referanslar, öznitelikler) ve `crm.account.read` ister.
- **Neden ayrı uç, aynı sözleşme (K-12):** kiracı, 404, K-11, 503 kuralları 1.0 ile aynı kalsın, ikinci kez yazılmasın;
  adres yalnız adres izni olana açılsın.

## 1. Ölçülen kaynaklar (main 5a099cadc)

- Hesabın adres alanları: `Account.CountryRef`, `CityRef`, `DistrictRef`, `AddressLine`
  (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Account.cs:27-30`). **Posta kodu yok** (yalnız
  `Contact.PostalCode`, `Contact.cs:46`). Hesapta **tek adres** var; teslimat adresi listesi yok.
- Kod kümeleri: Web formu Reference Data kümelerini kullanır — `country`, `city`, `district`
  (`frontend/Diten.Web/Controllers/CRM/AccountsController.cs:56-58`). CrmService yazmada bu alanları bir kümeye karşı
  **doğrulamaz** (`Application/Features/Account/AccountReferenceValidation.cs` yalnız durum kümesine bakar; ölçüm
  2026-11-13). Sonuç: dönen değer formun yazdığı koddur; eski ya da elle yüklenmiş kayıtta kümede olmayan değer olabilir.
- U-1/U-3 uygulaması (DL-1): `AccountController.cs` (`lookup`, `{accountId:guid}/lookup-validation`),
  `Api/Filters/RequireTokenTenantAttribute.cs` (K-11), `Application/Features/Account/AccountLookupContract.cs`,
  `GetAccountLookupValidationHandler.cs`, `SearchAccountLookupHandler.cs`; testler
  `tests/Diten.CrmService.Application.Tests/AccountLookup/*`.
- Gateway: `/api/crm/accounts/{everything}` (1.0 §8) `{id}/ship-to`'yu da kapsar → **yeni rota yok**, `ocelot.json` değişmez
  (teyit integration-agent'ın, yalnız not).

## 2. U-4 — `ship-to`

| # | Uç | Amaç |
|---|---|---|
| U-4 | `GET /api/crm/accounts/{accountId:guid}/ship-to` | hesabın kimliği, kodu, adı, durumu ve teslimat adresleri listesi (sevkiyat alıcısı kopyası için) |

### 2.1 Ortak kurallar (1.0'dan, değişmeden)

- Kiracı token'dan; `X-Tenant-Id` ↔ token `tenant_id` uyuşmazlığı ve kiracısız çağrı **400** (1.0 §1).
- `tenant_id` taşımayan token **400**, veri yok (K-11). U-4 aynı `RequireTokenTenant` filtresini taşır; CRM'in ortak kiracı
  ara katmanı değişmez.
- **404:** hesap yok / silinmiş / başka kiracıda — üçü aynı durum ve aynı gövde (1.0 §2.1).
- **Durum 404'e katılmaz:** `inactive`, `suspended`, `archived` vb. hesap 200 döner; `status` alanında görünür. Sevkiyata
  izin verip vermemek tüketicinin kuralıdır (K-14).
- 503: CRM deposunu okuyamazsa 503; varsayılan değer uydurmaz (1.0 §7). Tüketici her 5xx'i 503 sayar.
- Yalnız okuma; kayıt yazmaz, denetim olayı üretmez.

### 2.2 200 gövdesi (`data`)

| Alan (JSON) | Tip | Değer |
|---|---|---|
| `accountId` | string (UUID) | istenen kimlik |
| `code` | string, boş değil | `Account.AccountCode` |
| `name` | string, boş değil | `Account.AccountName` |
| `status` | string, boş değil | `Account.Status` (MOD-0048 `account-status` değer kodu, 1.0 §4) |
| `defaultAddressId` | string \| null | listede varsayılan teslimat adresinin `addressId`'si; liste boşsa null |
| `addresses` | dizi, her zaman var (boş olabilir) | teslimat adresleri; öğe alanları aşağıda. Sıra: varsayılan önce, sonra `label`, sonra `addressId` |
| `addresses[].addressId` | string, boş değil | adresin kalıcı kimliği. Hesabın ana adresi için sabit `"primary"`; ek teslimat adresleri (K-18) için UUID |
| `addresses[].kind` | string | `primary` (hesabın ana adresi) \| `delivery` (ek teslimat adresi, K-18) |
| `addresses[].label` | string \| null | kullanıcının verdiği ad (ör. "İstanbul şube"); `primary` için null |
| `addresses[].isDefault` | bool | tam bir öğe `true` (liste boş değilse) |
| `addresses[].countryRef` | string \| null | kırpılmış; boşsa null. Küme: Reference Data `country` |
| `addresses[].cityRef` | string \| null | kırpılmış; boşsa null. Küme: `city` |
| `addresses[].districtRef` | string \| null | kırpılmış; boşsa null. Küme: `district` |
| `addresses[].postalCode` | string \| null | kırpılmış; boşsa null. `primary`: `Account.PostalCode` (K-16 ile eklenecek alan; eklenene kadar null). `delivery`: adresin posta kodu |
| `addresses[].addressLine` | string \| null | kırpılmış; boşsa null; kesilmez |
| `contractVersion` | string | `crm-account-lookup/1.1` |

- **`primary` öğesi:** `Account.CountryRef`, `CityRef`, `DistrictRef`, `AddressLine` (K-16 sonrası `PostalCode`)'dan
  kurulur. Bu alanların hepsi boşsa `primary` öğesi **listeye girmez**.
- **İlk dilim (DL-3):** hesapta yalnız ana adres olduğundan liste en çok bir öğe (`primary`, `isDefault = true`) taşır.
  Ek teslimat adresleri K-18 ile geldiğinde liste büyür; gövde biçimi değişmez (yeni sürüm gerekmez).
- **Silinmiş / pasif ek adres** listede yer almaz.

Gövde **taşımaz:** sorumlu kişi (ad / telefon / e-posta), notlar, üst hesap, dış referanslar, öznitelikler, logo,
konum (enlem/boylam), `isBillable`. Görünen ad (ülke / şehir / ilçe adı) dönmez (K-15).

Liste boş olabilir (adres girilmemiş hesap): 200 döner, tüketici kendi kuralını uygular (MVP-6'da
`CUSTOMER_ADDRESS_MISSING`, `ShipmentViewModels.cs:68`).

Örnek (200, K-18 sonrası; ilk dilimde yalnız `primary` öğesi olur):

```json
{ "data": { "accountId": "3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c", "code": "ACC-2026-000042",
            "name": "Example Ecza Deposu", "status": "active", "defaultAddressId": "primary",
            "addresses": [
              { "addressId": "primary", "kind": "primary", "label": null, "isDefault": true,
                "countryRef": "tr", "cityRef": "ist", "districtRef": "kadikoy", "postalCode": null,
                "addressLine": "Örnek Mah. Örnek Cad. No: 1" },
              { "addressId": "9b1e4c7a-2d3f-4a5b-8c6d-7e8f9a0b1c2d", "kind": "delivery", "label": "Ankara şube",
                "isDefault": false, "countryRef": "tr", "cityRef": "ank", "districtRef": "cankaya",
                "postalCode": "06000", "addressLine": "Örnek Sok. No: 5" } ],
            "contractVersion": "crm-account-lookup/1.1" },
  "errors": null, "statusCode": 200, "isSuccessful": true }
```

(Kod değerleri örnektir; gerçek kodlar Reference Data kümelerindedir.)

### 2.3 Durum kodları

200 · 400 (kiracı uyuşmazlığı / kiracısız / `tenant_id`'siz token) · 401 · 403 (anahtar yok) · 404 · 503. Rota kısıtı
`:guid` uymazsa yönlendirme 404'tür (1.0 ile aynı).

## 3. İzin — K-13

- **Anahtar: `crm.account-ship-to.read`** (PKS-001: 3 segment, kaynak `account-ship-to`, Tier-1 eylem `read`).
- `crm.account.read` U-4'e **yetmez** (o anahtar tüm kaydı açan uçlar içindir); `crm.account.lookup-validation` da **yetmez**
  (AR rollerine adres açılmasın). Tek başına bu iki anahtardan biri 403 alır.
- Seçici için tüketici U-3'ü kullanır (`crm.account.lookup-validation`). Sevkiyat rolleri iki anahtarı birlikte alır:
  U-3 (arama) + U-4 (adres). U-3'ün izni değişmez.
- Katalog: anahtar CRM manifest'ine `ApiPermissions` olarak eklenir (DL-1-FU01 emsali, `CrmManifestProvider.cs`, scope
  `Tenant`). Rollere atama tüketicinin işidir (Auth ile; geçici: Roller → İzinler).
- Veri kapsamı: kiracı geneli (1.0 §6 ile aynı). Bölge / temsilci kapsamı `crm.account.read`'e uygulanırsa bu anahtara
  uygulanması ayrı karardır.

## 4. Tüketici kuralları (öneri; tüketicinin paketinde bağlayıcı olur)

| CRM cevabı | Tüketici |
|---|---|
| 200, `accountId` istenen, `contractVersion` `crm-account-lookup/1.` ile başlar, `name` dolu | kullanıcı listeden adres seçer (varsayılan önceden seçili); kopyayı al: `customerRef` = `accountId`, `addressId`, ad, kod, adres alanları (kod) + kendi çözdüğü görünen adlar |
| 200, `addresses` boş | tüketicinin "adres eksik" kuralı (elle giriş ya da hata) |
| 200, birden çok `isDefault = true` ya da `defaultAddressId` listede yok | geçersiz gövde → "alınamadı" |
| 404 | "müşteri bulunamadı" (tüketicinin kodu) |
| 400, 401, 403, 5xx, zaman aşımı, geçersiz gövde | "müşteri bilgisi alınamadı" — tüketicinin 503 karşılığı; kopya yazılmaz |

- Kopya **o anın** değeridir: hesap sonradan değişirse sevkiyat değişmez. Tüketici adresi önbellekte tutup yeni
  sevkiyata taşımaz; her seçimde U-4'ü yeniden çağırır.
- Görünen adlar (K-15): tüketici kodu Reference Data'dan (`country`, `city`, `district`) kullanıcının dilinde çözer; kod
  kümede yoksa kodun kendisini gösterir. Kopyaya hem kod hem ad yazılması önerilir (belge yazdırma için).
- Çağrı yolu: tüketicinin sunucusu, Gateway (5000) üzerinden, ekrandaki kullanıcının token'ıyla (1.0 §5a ile aynı).
  Kullanıcısız (servis) çağrı tanımlı değildir.

## 5. Sürüm uyumu (1.0 §10)

- Yeni uç + yeni anahtar = ekleme → 1.1. U-1/U-3 gövdeleri değişmez; `contractVersion` değerleri `crm-account-lookup/1.0`
  olarak kalır (tüketiciler `1.` önekine bakar).
- **1.x eklemesi neyi kapsar (temel §10'a açıklama):** temel §10'daki "yeni alan, yeni isteğe bağlı parametre" listesine
  **yeni uç** ve **yeni izin anahtarı** da girer — mevcut uçların yolu, gövdesi, durum kodları ve izni değişmediği sürece.
- **`contractVersion` anlamı:** her uç, gövdesinin tanımlandığı küçük sürümü döner (U-1 / U-3 `crm-account-lookup/1.0`,
  U-4 `crm-account-lookup/1.1`). Tüketici yalnız `crm-account-lookup/1.` önekine bakar (temel §5); bir uç kendi gövdesi
  değişmedikçe sürüm değerini değiştirmez.
- **Donmada temel dosyaya işleniş:** 1.1 donunca bu taslak `docs/analysis/contracts/crm-account-lookup.md`'ye işlenir ve
  o dosya `1.1.0` olur: künye (version, x-status, x-consumers, x-implemented); §1 uç tablosu (U-4), "tek dar okuma
  anahtarı" cümlesi (artık iki anahtar) ve Gateway cümlesi ("üç uç da" → dört uç); **§2b (yeni bölüm**, §2a'dan sonra:
  U-4 gövdesi = bu taslağın §2); §5a (tüketici satırları); **§5b (yeni bölüm**, §5a'dan sonra: U-4 tüketici kuralları =
  bu taslağın §4); §6 (K-13 anahtarı); §7 durum kodları (U-4); §8 Gateway (`{everything}` jokerinin kapsadığı yollara
  `{id}/ship-to` eklenir; yeni rota yok — bu taslağın §1); §9 (S- fikstürleri); §10 (yukarıdaki iki madde); §11 (K-9
  kapandı, K-12…K-19); §12 değişiklik geçmişi. 1.0.0 metninin öteki bölümleri (§2, §2a, §3, §4, §5) değişmez.
- Çoklu adres (K-18) ve ek adreslerde posta kodu bu biçimle **aynı sürümde** gelir: yalnız listeye öğe eklenir.
  Tüketici ilk günden listeyle çalışır; tek öğeli liste özel durum değildir.
- İleride 1.x ekleme olabilir: görünen adlar, adres başına ek alanlar (ör. teslim saatleri, alıcı adı — kişisel veri
  değerlendirmesiyle).
- Büyük sürüm sayılır: `addresses` ya da öğe alanlarını kaldırmak / yeniden adlandırmak, `"primary"` kimliğinin anlamını
  değiştirmek, 404 kararına durumu katmak.

## 6. Uyum fikstürleri (DL-3; S-MULTI DL-4)

| Fikstür | Durum | Beklenen |
|---|---|---|
| S-OK | aktif hesap, dört alan dolu; ek teslimat adresi ve `Account.PostalCode` verisi yok (DL-4 öncesi) | 200, tek öğe `primary`, `isDefault`, `defaultAddressId = "primary"`, alanlar kırpılmış, `postalCode` null, `contractVersion` 1.1 |
| S-EMPTY | adres alanları boş / yalnız boşluk | 200, `addresses = []`, `defaultAddressId = null` |
| S-PART | yalnız `CityRef` dolu | 200, tek öğe `primary`, öteki alanlar null |
| S-MULTI (K-18, **DL-4** — DL-3'te koşmaz) | ana adres + iki ek adres, biri varsayılan, biri silinmiş | 200, iki öğe (silinen yok), tam bir `isDefault`, sıra kuralı | 
| S-ST-x | `status ∈ {active, inactive, draft, suspended, archived}` ve küme dışı bir kod (ör. `closed`) | 200, `status` saklandığı gibi, adres yine döner |
| S-404 | yok / silinmiş / başka kiracı | 404, üç gövde bayt bayt aynı |
| S-K11 | `tenant_id`'siz token | 400, `data` yok |
| S-403a | yalnız `crm.account.read` | 403 |
| S-403b | yalnız `crm.account.lookup-validation` | 403 |
| S-PII | kişi ad / telefon / e-posta, not, dış referans, öznitelik dolu hesap | gövdede bu alanlardan hiçbiri yok (anahtar listesi tam eşleşme) |
| S-503 | depo istisnası | 503, `data` yok |

## 7. Uygulama dilimi DL-3 (bilgi; bağlayıcı metin pack ekinde)

- `AccountController`: `GET {accountId:guid}/ship-to`, `[HasPermission("crm.account-ship-to.read")]`, `[RequireTokenTenant]`.
- Application: `GetAccountShipToQuery` + işleyici, `AccountLookupContract`'a U-4 sabitleri; mevcut `GetByIdAsync`
  (kiracı + `!IsDeleted`) kullanılır, yeni depo sorgusu / indeks gerekmez.
- Manifest: `CrmManifestProvider.ApiPermissions`'a anahtar (Platform tarafı; DL-1-FU01 emsali, ayrı küçük iş).
- Testler: §6 fikstürleri (S-MULTI hariç — DL-4) gerçek işleyici + API düzeyinde; filtre ve izin özniteliği yansımayla (DL-1 emsali); sabotaj
  kanıtı.
- Gateway / `ocelot.json` / Infrastructure / ortak kiracı ara katmanı: değişmez.
- Ön koşul: MOD-0149 pack eki (DL-3, AC'ler) onayı. Kod yalnız onaylı pack ile yazılır.

## 8. Açık kararlar

| # | Konu | Sahip | Durum |
|---|---|---|---|
| K-12 | Tasarım: 1.1 içinde ayrı uç | commercial-suite (Beste) | **KARAR 2026-11-13** |
| K-13 | Anahtar `crm.account-ship-to.read` | commercial-suite (Beste) | **KARAR 2026-11-13** |
| K-14 | Aktif olmayan hesaba sevkiyat | tüketici (MOD-0178 / MOD-0183) | **TÜKETİCİ KARARI (WMS, 2026-11-13):** yeni sevkiyatta yalnız `active` hesap seçilebilir; seçimden sonra pasifleşen hesapta mevcut sevkiyat sürer; WMS kayıt ve "sevke hazır" adımında U-4'ü yeniden okur, durum `active` değilse uyarır. CRM'den istenen yalnız `status` alanı (§2.2) — sözleşmeye ek kural gelmez |
| K-15 | Görünen adı kim çözer | commercial-suite | ÖNERİ: tüketici, Reference Data'dan (dile bağlı, kaynak Reference Data) |
| K-16 | Ana adreste posta kodu | commercial-suite (Beste) | **KARAR 2026-11-13: eklenecek.** `Account.PostalCode` MOD-0149 DL-4 içinde (veri + hesap formu + 7 dil); U-4 `primary.postalCode` o zamana kadar null, sonra dolu — biçim değişmez |
| K-17 | Kod kümesi doğrulaması (yazmada doğrulama yok) | commercial-suite | AÇIK → DL-4 — U-4 saklananı döner; yeni ve değişen adreslerde yazma doğrulaması MOD-0149 DL-4'te (pack §32.10) |
| K-9 | MVP-6 adres yüzeyi | commercial-suite | Bu ekle kapanır (donmada); MVP-6 `GET /api/crm/accounts/{id}`'den U-4'e geçer |
| K-18 | Çoklu teslimat adresi | commercial-suite (Beste) | **KARAR 2026-11-13: yapılacak.** Hesaba ek teslimat adresleri (ad, ülke / şehir / ilçe, posta kodu, adres satırı, varsayılan, pasif) — MOD-0149 DL-4 (pack §32.10; veri + hesap ekranı + 7 dil + yazmada Reference Data doğrulaması, K-17'yi yeni adreslerde kapatır). Ekran: mockup yok; hesap detayının mevcut sekme yapısında (Work Center sekme kartı) standart liste + form (Beste 2026-11-13). Sözleşme biçimi §2.2 bunu bugünden taşır |
| K-19 | Yeni anahtarı alan CRM rolleri | commercial-suite (Beste); CT önerisi | **KARAR 2026-11-13** (Beste; CT önerisi, inceleme 0a9fd4ef F-2): CRM Admin alır; öteki CRM rolleri almaz (pack §16; pack §32.8 "CT-APR-01 onayıyla kesinleşir") |

Kayıt: docs/records/decisions/2026-11/mod0149-ship-to-owner-decisions-01.md (taslak — sahip okuması pack onayıyla kayda geçer).

## 9. Değişiklik geçmişi

- 1.1.0 taslak (2026-11-13) — U-4 `ship-to`, anahtar `crm.account-ship-to.read`; WMS (MOD-0178) isteği ve K-9. CT
  commercial-suite.
- 1.1.0 taslak r2 (2026-11-13) — K-18 (çoklu teslimat adresi, Beste): gövde tek `address` yerine `addresses[]` +
  `defaultAddressId`; ana adres `addressId = "primary"`; `postalCode` öğe alanı (ana adreste null).
- 1.1.0 taslak r3 (2026-11-13) — K-16 kararı: ana adrese `Account.PostalCode` eklenecek (K-18 FU'su içinde);
  K-18 ekranı mockup'sız, mevcut sekme yapısında standart liste + form.
- 1.1.0 taslak r4 (2026-11-13) — K-14 WMS kararı kaydedildi (yalnız `active` seçilir; kayıt ve "sevke hazır"da yeniden
  okuma + uyarı); sözleşme biçimi değişmedi.
- 1.1.0 taslak r5 (2026-11-13) — bağımsız inceleme 2026-11-13-0a9fd4ef (KOŞULLU, 0 ENGEL) bulguları: tüketiciler "aday
  (istek)" ve karar kaydı yolu (F-3); S-OK koşulu (F-6); S-MULTI DL-4, S-ST-x tam küme, "FU" → "DL-4" (F-7); §5'e 1.x
  ekleme kapsamı, uç başına `contractVersion` ve donmada temel dosyaya işleniş (F-8); K-19 (F-2).
- 1.1.0 taslak r6 (2026-11-13) — dar yeniden inceleme 2026-11-13-dadab82e (KOŞULLU, 3 düşük): donmada işleniş listesi
  temel dosyanın bölümleriyle eşlendi — §2b ve §5b yeni bölüm, §1 Gateway cümlesi ve §8 eklendi (F-3); K-19 "ÖNERİ (CT)"
  olarak etiketlendi (F-2); K-17 "DL-4" (not). Sözleşme biçimi değişmedi.
- 1.1.0 taslak r7 (2026-11-13) — dar yeniden inceleme 2 (2026-11-13-8749bc2a, KOŞULLU, 2 düşük): §5 Gateway atfı
  §6 → §1 (F-2); K-19 Beste kararı (2026-11-13). Sözleşme biçimi değişmedi.
