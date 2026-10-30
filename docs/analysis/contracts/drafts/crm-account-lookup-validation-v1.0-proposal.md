# CRM-ACCOUNT-LOOKUP 1.0.0 — MOD-0149 müşteri doğrulama ve dış referans çözümleme sözleşmesi (TASLAK / öneri)

    contract       : CRM-ACCOUNT-LOOKUP
    version        : 1.0.0
    x-status       : DRAFT — öneri; MOD-0149 pack "Treasury AR tüketimi (MOD-0120)" eki CT-APR-02 / CT-APR-01'den geçmeden
                     ve eki teslim satırı (AR-EK-1) kabul edilmeden dondurulmaz
    x-owner        : MOD-0149 Customer 360 / Account Hierarchy (commercial-suite, Diten.CrmService, port 5061)
    x-consumers    : MOD-0120 Accounts Receivable (`ICustomerReferencePort`); ileride başka Treasury / O2C tüketicileri
    x-decisions    : Y-4/4b (uçlar + sözleşme eki kabul), Y-4/4c (`account-status` kümesi, faturalanabilir yalnız `active`)
                     — docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:35;
                     MOD-0120 O-3 (KARAR D29/1, seçenek (a)), O-4
    x-pattern      : LEGAL-ENTITY-LOOKUP 1.1.0 ve BANK-ACCOUNT-LOOKUP 1.0.0
                     (docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md,
                     docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md)
    x-source       : MOD-0149 pack § "Treasury AR tüketimi (MOD-0120)"
                     (execution/domains/commercial-suite/module-packs/MOD-0149-customer-360-account-hierarchy.md);
                     çelişkide pack kazanır, bu dosya pack ile hizalanır
    x-measured-at  : origin/main bf3b0cd57 (2026-10-30)

Kod yoktur; iki uç henüz yazılmadı. Bu dosya pack ekinin sözleşme hâlidir.

Ölçülen kaynaklar (satır numaraları numaralı okumadan, @bf3b0cd57):

- Bugünkü okuma: `GET /api/crm/accounts/{id:guid}` izin `crm.account.read`
  (`services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/AccountController.cs:65-68`); denetleyici
  `[Authorize]` + `[Route("api/crm/accounts")]` (`:12-13`). Bu uç tam hesap kaydını (adres, sorumlu kişi, dış referanslar,
  öznitelikler) döner; Treasury'ye açılmaz (MOD-0120 O-3 (b) reddedildi).
- Kiracı ve silme: `AccountRepository.ActiveTenant` = `TenantId == tenantId && !IsDeleted`
  (`.../Persistence/Repositories/AccountRepository.cs:30-31`); `GetByIdAsync` bu filtreyi kullanır (`:33-37`); işleyici
  kayıt yoksa 404 `"Account not found."` (`.../Account/Handlers/QueryHandlers/GetAccountByIdHandler.cs:35-39`).
- Durum: `Account.Status` = MOD-0048 `account-status` değer kodu (`.../Domain/Entities/Account.cs:24-25`); yazmada MOD-0048
  yayımlanmış kümeye karşı doğrulanır (`.../Account/AccountReferenceValidation.cs:12, 25`). Hesap kiracı genelidir
  (şirket / LegalEntity alanı yok).
- Dış referans: `AccountExternalReference` (`.../Domain/Entities/AccountExternalReference.cs:3-22`); depo yalnız
  `ExistsBySourceExternalAsync` / `ListByAccountAsync` / `InsertAsync` (`.../Domain/Repositories/IAccountExternalReferenceRepository.cs:5-13`)
  — çözümleme sorgusu yok.
- Tekillik: benzersiz kısmi indeks `ux_account_external_refs_tenant_source_external` = `{TenantId, SourceSystem, ExternalId}`,
  `IsDeleted = false` filtreli, harmanlama (collation) yok (`.../Persistence/DependencyInjection.cs:1436-1446`). İndeks
  kurulumu açılışta "best-effort"tür: `MongoException` yutulur (`:2157-2160`).
- `SourceSystem` normalizasyonu (yazma yolu): yalnız `Trim()`; boşsa sabit `"default"`
  (`.../Account/Handlers/CommandHandlers/CreateAccountHandler.cs:14, 62-73`); `ExternalId` yalnız `Trim()` (`:70, 129`).
  Varlık kontrolü sıradan eşitlikle (`AccountExternalReferenceRepository.cs:19-20`). Büyük/küçük harf dönüşümü **yok**.
- Zarf: `Response<T>` = `data`, `errors` (string listesi), `statusCode`, `isSuccessful`
  (`.../Application/Common/Models/Response.cs:3-20`); durum eşlemesi `CustomBaseController.cs:10-23`. Makine-okur hata
  kodu alanı yoktur.
- Korelasyon: CrmService dış çağrılarında gelen `X-Correlation-Id`'yi iletir (ör.
  `.../Infrastructure/CyclePeriod/MdmCyclePeriodLegalEntityValidator.cs:147-155`); Api projesinde gelen başlığı günlük
  kapsamına koyan bir ara katman bu ölçümde **bulunmadı** (`Controllers/` ve `Program.cs` tarandı).
- Gateway: `/api/crm/accounts` ve `/api/crm/accounts/{everything}` → `localhost:5061`, GET dahil
  (`gateway/Diten.ApiGateway/ocelot.json:2950-2989`); `/api/crm/accounts` altında başka Upstream şablonu yok.

## 1. Uçlar

| # | Uç | Amaç |
|---|---|---|
| U-1 | `GET /api/crm/accounts/{accountId:guid}/lookup-validation` | hesap var mı, çağıranın kiracısında mı, durumu ne, faturalanabilir mi |
| U-2 | `GET /api/crm/accounts/external-references/resolve?sourceSystem={s}&externalId={e}` | dış referanstan hesabı bul + U-1'in doğrulama alanları |

- Kiracı yalnız token'dan (sunucu tarafı `ITenantContext`); istek parametresi, gövde ya da başlıkla kiracı verilmez.
- İzin: tek dar okuma anahtarı (§6); `crm.account.read` **gerekmez ve yetmez**.
- Yalnız okuma: hiçbir uç kayıt yazmaz, denetim olayı üretmez (AUD-001 okuma; MOD-0149 §23 okuma olayı tanımlamaz).
- Gateway: iki uç da mevcut `/api/crm/accounts/{everything}` rotasıyla taşınır (§8); `ocelot.json` bu sözleşmeyle değişmez.

## 2. U-1 — `lookup-validation`

### 2.1 404 (gövde aynı, varlık sızdırmaz)

Hesap yok; silinmiş (`IsDeleted = true`); başka kiracıda. Üçü de aynı durum kodu ve aynı gövdeyle döner; yanıt hesabın
başka bir kiracıda var olduğunu ele vermez. **Durum 404 kararına katılmaz:** faturalanamaz hesap 200 döner (`isBillable =
false`) — tüketici "bulunamadı" ile "uygun değil"i ayırabilmelidir (MOD-0120 iki ayrı 422 kodu).

### 2.2 200 gövdesi (`data`)

| Alan (JSON) | Tip | Değer |
|---|---|---|
| `accountId` | string (UUID) | istenen kimlik |
| `code` | string, boş değil | `Account.AccountCode` (kayıt sonrası her zaman dolu — pack §10.1a) |
| `name` | string, boş değil | `Account.AccountName` |
| `status` | string, boş değil | `Account.Status`, saklandığı gibi (MOD-0048 `account-status` değer kodu; §4) |
| `isBillable` | bool | `status == "active"` (sıradan, büyük/küçük harfe duyarlı karşılaştırma) — başka hiçbir girdiye bakmaz |
| `contractVersion` | string | `crm-account-lookup/1.0` |

Gövde **adres, coğrafya, sorumlu kişi (ad / telefon / e-posta), notlar, üst hesap, dış referanslar, öznitelikler, logo**
taşımaz. Değişmez kural: `isBillable = true` ⇔ `status = "active"`; ikisi çelişirse yanıt geçersizdir (§5).

Örnek (200):

```json
{ "data": { "accountId": "3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c", "code": "ACC-2026-000042",
            "name": "Example Hastanesi", "status": "active", "isBillable": true,
            "contractVersion": "crm-account-lookup/1.0" },
  "errors": null, "statusCode": 200, "isSuccessful": true }
```

## 3. U-2 — `external-references/resolve`

### 3.1 Girdi ve normalizasyon (koddan ölçüldü, yazma yoluyla aynı)

- `sourceSystem` ve `externalId` zorunlu; ikisi de yalnız `Trim()` edilir. Büyük/küçük harf **korunur**; eşleşme
  sıradan (ordinal), büyük/küçük harfe duyarlı tam eşitliktir — benzersiz indeksle (harmanlama yok) aynı anlam. `OldCRM`
  ile `oldcrm` iki ayrı kaynaktır.
- Boş ya da yalnız boşluk `sourceSystem` / `externalId` → **400**. Çözümleme `"default"` kaynağını **varsaymaz**
  (yazma yolunun varsayılanı tüketiciye taşınmaz; `"default"` aranacaksa açıkça gönderilir).
- Uzunluk üst sınırı: her biri 256 karakter (öneri; aşan → 400). Yazma yolunda sınır ölçülmedi.

### 3.2 Eşleşme kuralı

Aday satırlar: çağıranın kiracısında, `IsDeleted = false`, `SourceSystem` ve `ExternalId` tam eşit dış referanslar.
Eşleşen satırın `AccountId`'si U-1'in 404 kuralından geçmelidir (hesap var, silinmemiş, aynı kiracı).

| Durum | Yanıt |
|---|---|
| Tam bir aday satır, hesabı geçerli | **200** — gövde §2.2 alanları + `sourceSystem`, `externalId` (normalize edilmiş, eşleşen değerler) |
| Aday yok; ya da tek aday ama hesabı silinmiş / yok | **404** (gövde U-1'in 404'üyle aynı biçimde; başka kiracıdaki eşleşme sızdırılmaz) |
| İki ya da daha çok aday satır | **409** — hesap seçilmez, ilk satır döndürülmez |

**409 savunma amaçlıdır, veri bozulmasını gösterir.** Benzersiz kısmi indeks `{TenantId, SourceSystem, ExternalId}`
kodda tanımlıdır (`DependencyInjection.cs:1436-1446`); indeks bir veritabanında varsa iki etkin aday oluşamaz. Ancak
indeks kurulumu açılışta hata yutar (`:2157-2160`): önceden mükerrer veri varsa indeks sessizce kurulmamış olabilir.
Canlı veritabanlarında indeksin varlığı bu sözleşmede **ölçülmedi** (Mongo bağlantısı kapsam dışı). 409 bu nedenle
sözleşmede kalır; aynı hesaba giden iki satır da 409'dur (veri bozulması gizlenmez).

Örnek (200):

```json
{ "data": { "accountId": "3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c", "code": "ACC-2026-000042",
            "name": "Example Hastanesi", "status": "active", "isBillable": true,
            "sourceSystem": "OldCRM", "externalId": "WP-118204",
            "contractVersion": "crm-account-lookup/1.0" },
  "errors": null, "statusCode": 200, "isSuccessful": true }
```

## 4. `account-status` kümesi ve faturalanabilirlik (sözleşmede yayımlanır)

Küme MOD-0048 `account-status` setidir (kiracı kapsamlı); CRM değeri saklandığı gibi döner. Faturalanabilirlik kuralı
**CRM'dedir** ve tek alanla (`isBillable`) verilir; **AR tarafında sabit durum tablosu olmaz** (Y-4/4c).

| `status` | `isBillable` | Kaynak |
|---|---|---|
| `active` | `true` | Y-4/4c |
| `inactive` | `false` | Y-4/4c |
| `draft` | `false` | Y-4/4c |
| `suspended` | `false` | Y-4/4c |
| `archived` | `false` | **AÇIK KARAR (Beste)** — referans verisi şablonunda var (`docs/records/audits/2026-08/mod-0149-crm-reference-data-authoring-template.json`, `account-status` 5. değer), Y-4/4c'de yok |

- **CT önerisi (`archived` için):** beş değerin hepsi yayımlanır, faturalanabilir yalnız `active`. Gerekçe: yazma yolu
  MOD-0048'de yayımlanmış her kodu kabul eder (`AccountReferenceValidation.cs:25`); şablon yayımlandıysa `archived`
  hesap vardır ve tüketici sözleşmede adı geçmeyen bir kod görmemelidir.
- Kümede olmayan bir kod (ileride MOD-0048'e eklenen değer, eski veri) da `isBillable = false` döner; CRM 200'ü
  bozmaz. Yeni bir kodun **faturalanabilir** sayılması bu sözleşmenin değişikliğidir (§9).
- Tüketici `status`'u yalnız gösterim / gerekçe için kullanır; karar `isBillable` iledir.

## 5. Tüketici sınıflandırması — MOD-0120 eşlemesi (sözleşmenin parçası)

MOD-0120'nin beklentisi (pack kapanış tablosu, MOD-0149 satırı: "bulunamadı 422 · uygun değil 422 · erişilemez 503 ·
geçiş yok"; Runtime Constraints "Müşteri uygunluğu güncel durumdur"):

| CRM yanıtı | Sınıf | MOD-0120 sonucu |
|---|---|---|
| 200; `accountId` istenen (U-2'de: dolu, UUID) ; `contractVersion` `crm-account-lookup/1.` ile başlar; `isBillable = true`; `status = "active"` | uygun | işlem sürer |
| 200, tutarlı; `isBillable = false` (herhangi bir `status`) | tanımlı, uygun değil | 422 `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE` |
| 404 (U-1 ya da U-2) | bulunamadı (yok / silinmiş / başka kiracı / eşleşmeyen dış referans) | 422 `RECEIVABLE_CUSTOMER_NOT_FOUND` |
| 409 (U-2, çoklu eşleşme) | veri bozulması — hesap belirlenemez | 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE` (CT önerisi; tahmin yapılmaz) |
| 200 ama `accountId` yok / farklı; `isBillable` yok / `null`; `isBillable = true` iken `status ≠ "active"`; `status` / `code` / `name` yok ya da boş; `contractVersion` yok ya da `1.` değil; gövde ayrıştırılamıyor / `data` yok | geçersiz / çelişkili | 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE` |
| 400, 401 / 403 (izin ataması eksik dahil), 5xx (CRM 503 dahil), zaman aşımı, ulaşılamıyor | erişilemiyor | 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE` |

Kural: **geçersiz ya da çelişkili yanıt 503**, **tanımlı ama uygun olmayan durum 422**; hiçbir belirsiz yanıt "uygun"
sayılmaz ve hiçbirinde AR kaydı yazılmaz (geçiş yok). Karşılaştırmalar büyük/küçük harfe duyarlıdır. Tüketici
`code` / `name`'i yalnız anlık görüntü alanlarına yazar (MOD-0120 `CustomerCodeSnapshot` / `CustomerNameSnapshot`);
anlık görüntü uygunluk yerine geçmez.

## 6. İzin — AÇIK KARAR (commercial-suite sahibi, Beste)

- Amaç: AR kullanıcısına `crm.account.read` **vermemek** (MOD-0120 O-3 (b) reddedildi: CRM okuma yetkisi + sözleşmesiz).
  İki uç ayrı, dar bir okuma anahtarıyla korunur; anahtar yalnız bu doğrulama yanıtını açar, hesap kaydını açmaz.
- **Aday:** `crm.account.lookup`. PKS-001 (`.antigravity/rules/permission-key-standard.md`) ölçümü: biçim geçerli
  (3 segment, `^[a-z][a-z0-9-]*$`, §1), ama son segment `lookup` §5 eylem sözlüğünde yok (Tier-1/Tier-2 değil, Tier-3
  örneklerinde yok) → kullanılırsa MOD-0149 pack'inde Tier-3 eylem olarak kaydedilmelidir (§5 Tier-3 kuralı).
- **Alternatif (sözlükte var):** `crm.account.lookup-validation` — Tier-3 örneklerinde `lookup-validation` var; emsal
  anahtar `auth.users.lookup-validation` (§8.3).
- Anahtar bu sözleşmede **kesinleşmez**. Ayrı işler (bu sözleşmenin dışında): Auth katalog kaydı (MOD-0018), AR
  rollerine atanması (MOD-0018-FU27 Treasury Auth eki ya da sahibinin seçtiği yer), CRM rol eşlemesi (MOD-0149 §16
  tablosu). Anahtar atanmamış kullanıcıyla çağrı 403 → tüketici 503 (§5).
- Veri kapsamı: MVP kiracı kapsamı (MOD-0149 §16). Bölge / temsilci kapsamı (MOD-0018-FU15) ileride `crm.account.read`'e
  uygulanırsa bu anahtara uygulanıp uygulanmayacağı ayrı karardır; uygulanmazsa AR kiracı genelinde doğrular.

## 7. Hata modeli, 503, korelasyon

- **Zarf:** CRM'in mevcut `Response<T>` zarfı (`data`, `errors`, `statusCode`, `isSuccessful`); emsallerdeki gibi
  `data` yalnız 200'de dolu. CRM zarfında makine-okur hata kodu alanı yoktur; tüketici **yalnız HTTP durum koduna**
  göre karar verir, `errors[]` metni sözleşmenin parçası değildir (yerelleştirilmez, değişebilir).
- **Durum kodları:** 200 · 400 (U-2 girdisi; U-1'de rota kısıtı `:guid` uymazsa yönlendirme 404'tür) · 401 · 403 · 404 ·
  409 (yalnız U-2) · 503.
- **503 (kaynak okunamıyor):** CRM kendi deposunu okuyamazsa (Mongo istisnası, zaman aşımı) **503** döner; 500 ya da
  varsayılan değer dönmez, "uygun" ya da "bulunamadı" uydurmaz. Gövdede `data` yok. Bugün yakalanmamış Mongo istisnası
  genel hata yoluna düşer — 503 eşlemesi bu ekin uygulama işidir. Tüketici her 5xx'i 503 sayar (§5).
- **Korelasyon:** tüketici `X-Correlation-Id` başlığını gönderir (emsal tüketici davranışı, CrmService'in kendi
  istemcileri de iletir). CRM başlığı yanıt gövdesine taşımaz; başlığın yokluğu hata değildir. Gelen başlığı günlük
  kapsamına koyan ara katman CrmService Api'de ölçülmedi/bulunmadı → ekin uygulama işinde ya mevcut paylaşılan bir
  ara katman bağlanır ya da bu madde açık kalır (yeni bir mekanizma bu sözleşmeyle tanımlanmaz).
- **Önbellek:** yanıtlar güncel durumdur; tüketici uygunluk kararını önbelleğe almaz (MOD-0120 "Müşteri uygunluğu güncel
  durumdur").

## 8. Gateway

Ölçüm (@bf3b0cd57): `ocelot.json:2970-2989` `/api/crm/accounts/{everything}` → `http://localhost:5061/api/crm/accounts/{everything}`,
`UpstreamHttpMethod` GET dahil; `/api/crm/accounts` altında daha özel bir Upstream şablonu yok. `{everything}` hem
`{id}/lookup-validation`'ı hem `external-references/resolve`'u kapsar; sorgu dizesi Ocelot'ta varsayılan olarak
iletilir. Emsal: `AccountContactController.cs:13` ve `AccountRelationshipController.cs:13` aynı joker rotayla, yeni rota
açmadan yayımlandı. Sonuç: **yeni Gateway rotası gerekmez**; `ocelot.json`'a dokunulmaz. Teyit integration-agent'ındır
(yalnız not). Servis tarafında `external-references/resolve` sabit yolu `{id:guid}` kısıtlı rotalarla çakışmaz.

## 9. Uyum fikstürleri

Her biri öteki alanlar uygunken yalnız bir şeyi bozar; CrmService testlerinde bir başvuru sınıflandırıcısı koşar, MOD-0120
aynı fikstürleri kendi `ICustomerReferencePort` sınıflandırıcısına karşı koşar (MOD-0120 AC kapsamı, o paketin işi).

| Fikstür | Durum | Beklenen sınıf |
|---|---|---|
| F-OK | `status = active`, `isBillable = true` | uygun |
| F-NE-x | `status ∈ {inactive, draft, suspended, archived}`, `isBillable = false` (dört fikstür) | 422 NOT_ELIGIBLE |
| F-UNK | `status = closed` (kümede yok), `isBillable = false` | 422 NOT_ELIGIBLE |
| F-404 | yok / silinmiş / başka kiracı (üç fikstür, gövdeleri bayt bayt aynı) | 422 NOT_FOUND |
| F-R404 | U-2 eşleşme yok; `oldcrm` ile `OldCRM` kaydı (harf farkı) | 422 NOT_FOUND |
| F-R409 | U-2 iki etkin aday satır | 503 |
| F-C1 | `isBillable = true` + `status = suspended` | 503 |
| F-C2 | `accountId` farklı | 503 |
| F-C3 | `isBillable` yok | 503 |
| F-V | `contractVersion = crm-account-lookup/2.0` | 503 |
| F-503 | CRM 503 | 503 |
| F-403 | anahtar atanmamış kullanıcı (403) | 503 |

## 10. Sürüm kuralı

1.x yalnız ekleme (yeni alan, yeni isteğe bağlı parametre). Şunlar **büyük sürümdür**: 404 kararını genişletmek ya da
daraltmak (ör. faturalanamaz hesabı 404 yapmak), `isBillable` kuralını `active` dışına açmak ya da bir alanı kaldırmak /
yeniden adlandırmak. `account-status` kümesine faturalanamaz bir kod eklemek 1.x içindedir (tüketici `isBillable`'a
bakar). MOD-0003 Data Contract Registry yoktur (D27/1): dondurulunca bu dosya `docs/analysis/contracts/crm-account-lookup.md`
olur ve MOD-0003 gelince oraya kaydedilir. JSON özellik sırası sözleşmenin parçası değildir.

## 11. Açık kararlar (sözleşme dondurulmadan kapanmalı)

| # | Karar | Sahip | CT önerisi |
|---|---|---|---|
| K-1 | İzin anahtarı: `crm.account.lookup` (Tier-3 kaydı ister) ya da `crm.account.lookup-validation` (sözlükte var) | commercial-suite (Beste) | sahibin seçimi; anahtar seçilince Auth katalog + AR rol ataması ayrı iş |
| K-2 | `archived` sözleşmede yayımlanır mı | commercial-suite (Beste) | evet, `isBillable = false` |
| K-3 | U-2 409'un tüketici eşlemesi (503) | MOD-0120 sahibi | 503 (tahmin yok) |
| K-4 | Gateway: mevcut joker rota yeterli mi (§8) | integration-agent | yeterli; yalnız teyit |

## 12. Değişiklik geçmişi

- 1.0.0 (2026-10-30) — ilk yazılı sürüm; MOD-0120 O-3 / O-4 ve Y-4/4b, Y-4/4c'den; MOD-0149 pack eki ile birlikte.
