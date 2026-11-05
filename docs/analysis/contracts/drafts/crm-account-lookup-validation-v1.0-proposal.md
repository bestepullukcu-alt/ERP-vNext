# CRM-ACCOUNT-LOOKUP 1.0.0 — MOD-0149 müşteri doğrulama, seçici araması ve dış referans çözümleme sözleşmesi (TASLAK / öneri)

    contract       : CRM-ACCOUNT-LOOKUP
    version        : 1.0.0
    x-status       : DRAFT — öneri; MOD-0149 pack "Treasury AR tüketimi (MOD-0120)" eki CT-APR-02 / CT-APR-01'den geçmeden
                     ve pack §31 DL-1 (U-1, U-3) kabul edilmeden dondurulmaz. U-2 bölümü (§3) DL-2 ile (MOD-0120 O-10)
                     1.x içinde ayrıca dondurulur; o güne kadar bağlayıcı taslaktır
    x-owner        : MOD-0149 Customer 360 / Account Hierarchy (commercial-suite, Diten.CrmService, port 5061)
    x-consumers    : MOD-0120 Accounts Receivable (`ICustomerReferencePort`). Adaylar (istek, bağlayıcı değil): MOD-0177
                     Recall Readiness; MOD-0183 Shipment Tracking / MOD-0186 Reverse Logistics — §5a
    x-decisions    : Y-4/4b (uçlar + sözleşme eki kabul), Y-4/4c (`account-status` kümesi, faturalanabilir yalnız `active`)
                     — docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:35;
                     MOD-0120 O-3 (KARAR D29/1, seçenek (a)), O-4; K-1, K-2, K-3 (commercial-suite sahibi Beste, 2026-10-30);
                     K-8 (U-3, Beste 2026-11-04); K-11 (tenant_id'siz token reddi, Beste 2026-11-05) — K-1…K-6, K-8, K-11
                     kaydı docs/records/decisions/2026-11/mod0149-ar-lookup-owner-decisions-01.md (taslak);
                     K-7 = D112/2 (docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md)
    x-pattern      : LEGAL-ENTITY-LOOKUP 1.1.0 ve BANK-ACCOUNT-LOOKUP 1.0.0
                     (docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md,
                     docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md)
    x-source       : MOD-0149 pack § "Treasury AR tüketimi (MOD-0120)"
                     (execution/domains/commercial-suite/module-packs/MOD-0149-customer-360-account-hierarchy.md);
                     çelişkide pack kazanır, bu dosya pack ile hizalanır
    x-measured-at  : origin/main 4dbcd67a8 (2026-11-04; ilk ölçüm bf3b0cd57, 2026-10-30)

Kod yoktur; üç uç henüz yazılmadı. Bu dosya pack ekinin sözleşme hâlidir.

Ölçülen kaynaklar (satır numaraları numaralı okumadan, @4dbcd67a8; aşağıdaki dosyalar bf3b0cd57'den beri yalnız
`Persistence/DependencyInjection.cs`'te değişti):

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
  `IsDeleted = false` filtreli, harmanlama (collation) yok (`.../Persistence/DependencyInjection.cs:1442-1451`). İndeks
  kurulumu açılışta "best-effort"tür: `MongoException` yutulur (`:2163`); `Mongo:EnsureIndexesOnStartup=false` ile
  kurulum hiç koşmaz (`:289-295`, yalnız test bağlantısı için).
- Arama (U-3 dayanağı): `AccountRepository.ListAsync` kiracı + `IsDeleted = false` filtresiyle ad ya da kodda
  "içerir" araması yapar (`.../Persistence/Repositories/AccountRepository.cs:78-145`); terim
  `TurkishInsensitivePattern` ile `Regex.Escape` edilir ve Türkçe i harflerine duyarsızdır
  (`.../Application/Common/TurkishInsensitivePattern.cs:6-15`); indeks `ix_accounts_tenant_name`
  (`DependencyInjection.cs:1435-1437`). Bugünkü liste ucu `GET /api/crm/accounts` `crm.account.read` ister (`AccountController.cs:23-24`).
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
| U-3 | `GET /api/crm/accounts/lookup?search={q}&page={p}&pageSize={n}&billableOnly={b}` | müşteri seçici araması: ad ya da koda göre sayfalı liste, öğe başına U-1'in doğrulama alanları |
| U-2 | `GET /api/crm/accounts/external-references/resolve?sourceSystem={s}&externalId={e}` | dış referanstan hesabı bul + U-1'in doğrulama alanları (DL-2, ertelendi) |

- Kiracı istemciden seçilmez: Gateway kiracıyı kullanıcının token'ından çözüp `X-Tenant-Id` başlığına yazar; CrmService kiracıyı bu başlıktan kurar ve token'daki `tenant_id` ile karşılaştırır (`services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs:21-59`;
  Gateway `Middleware/TenantResolutionMiddleware.cs:203-239`). Başlık ile token farklıysa **400**; kiracısız çağrı (başlık
  yok) **400** ve veri dönmez (`GetAccountByIdHandler.cs:30-33` emsali). İstek parametresi ya da gövdeyle kiracı verilmez.
- Bu uçlar yalnız `tenant_id` taşıyan kullanıcı token'ıyla çalışır (K-11): token'da `tenant_id` yoksa `X-Tenant-Id`
  başlığı olsa bile **400**, veri dönmez. Gateway token'da kiracı yoksa istemcinin başlığını, alt alan adını ya da
  Development'ta yedek kiracıyı kullanabilir (`gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:204-211`,
  `:229-235`, `:298`); CRM ise yalnız iki değer de doluysa karşılaştırır (`TenantResolutionMiddleware.cs:37`). K-11 bu
  yolların bu sözleşmenin uçlarına kiracı seçtirmesini kapatır.
- İzin: tek dar okuma anahtarı (§6); `crm.account.read` **gerekmez ve yetmez**.
- Yalnız okuma: hiçbir uç kayıt yazmaz, denetim olayı üretmez (AUD-001 okuma; MOD-0149 §23 okuma olayı tanımlamaz).
- Gateway: üç uç da mevcut `/api/crm/accounts/{everything}` rotasıyla taşınır (§8); `ocelot.json` bu sözleşmeyle değişmez.

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

## 2a. U-3 — `lookup` (müşteri seçici araması; karar K-8)

### 2a.1 Girdi

| Parametre | Kural |
|---|---|
| `search` | zorunlu; `Trim()` sonrası 2–100 karakter, aksi **400**. Düz metindir (regex değil): CRM terimi kaçışlar; ad ya da kodda "içerir", büyük/küçük harfe ve Türkçe i harflerine (`i ı I İ`) duyarsız |
| `page` | isteğe bağlı, varsayılan 1; `< 1` → **400** |
| `pageSize` | isteğe bağlı, varsayılan 20; `< 1` ya da `> 50` → **400** |
| `billableOnly` | isteğe bağlı, varsayılan `false`; `true` → yalnız `status = "active"` hesaplar |

Kiracı §1'deki gibi; silinmiş hesaplar dönmez. Sıralama ad artan, eşit adlarda `accountId` artan (kararlı sayfalama:
ardışık sayfalar çakışmaz, kayıt atlanmaz). Eşleşme yoksa **200, boş liste** (404 yok).

### 2a.2 200 gövdesi (`data`)

| Alan | Tip | Değer |
|---|---|---|
| `items` | dizi | her öğe §2.2'nin `accountId`, `code`, `name`, `status`, `isBillable` alanları (aynı tanım, aynı `isBillable` kuralı) |
| `page`, `pageSize` | int | uygulanan değerler |
| `total` | int | filtreli eşleşme sayısı |
| `contractVersion` | string | `crm-account-lookup/1.0` |

Öğeler §2.2'de sayılan alanların dışında hiçbir şey taşımaz (adres, sorumlu kişi, dış referans, öznitelik yok).
**U-3 sonucu uygunluk kararı değildir:** tüketici seçilen `accountId`'yi kaydetmeden önce U-1 ile doğrular (§5).

Örnek (200):

```json
{ "data": { "items": [ { "accountId": "3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c", "code": "ACC-2026-000042",
                         "name": "Example Hastanesi", "status": "active", "isBillable": true } ],
            "page": 1, "pageSize": 20, "total": 1, "contractVersion": "crm-account-lookup/1.0" },
  "errors": null, "statusCode": 200, "isSuccessful": true }
```

## 3. U-2 — `external-references/resolve`

> **ERTELENDİ (K-7, MOD-0120 sahibi 2026-10-30):** bu sürümde tüketicisi yok; MOD-0120 O-10 (içe aktarma / eski kayıt
> taşıma) ile etkinleşir (MOD-0149 pack §31 DL-2). Tanım burada kalır ve 1.x içinde değişmeden uygulanır;
> `sourceSystem` değerleri O-10'da belirlenir, büyük/küçük harf kuralı (§3.1) bağlayıcıdır.

### 3.1 Girdi ve normalizasyon (koddan ölçüldü, yazma yoluyla aynı)

- `sourceSystem` ve `externalId` zorunlu; ikisi de yalnız `Trim()` edilir. Büyük/küçük harf **korunur**; eşleşme
  sıradan (ordinal), büyük/küçük harfe duyarlı tam eşitliktir — benzersiz indeksle (harmanlama yok) aynı anlam. `OldCRM`
  ile `oldcrm` iki ayrı kaynaktır.
- Boş ya da yalnız boşluk `sourceSystem` / `externalId` → **400**. Çözümleme `"default"` kaynağını **varsaymaz**
  (yazma yolunun varsayılanı tüketiciye taşınmaz; `"default"` aranacaksa açıkça gönderilir).
- Uzunluk üst sınırı (bağlayıcı, DL-2): `Trim()` sonrası her biri en çok 256 karakter; aşan → **400** (pack AC-AR-6).
  Yazma yolunda sınır ölçülmedi; 256'dan uzun kayıtlı bir değer bu uçla aranamaz.

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
| `archived` | `false` | **K-2** (Beste, 2026-10-30) — referans verisi şablonunda var (`docs/records/audits/2026-08/mod-0149-crm-reference-data-authoring-template.json`, `account-status` 5. değer) |

- **Karar K-2 (`archived`):** beş değerin hepsi yayımlanır, faturalanabilir yalnız `active`. Gerekçe: yazma yolu
  MOD-0048'de yayımlanmış her kodu kabul eder (`AccountReferenceValidation.cs:25`); şablon yayımlandıysa `archived`
  hesap vardır ve tüketici sözleşmede adı geçmeyen bir kod görmemelidir.
- Kümede olmayan bir kod (ileride MOD-0048'e eklenen değer, eski veri) da `isBillable = false` döner; CRM 200'ü
  bozmaz. Yeni bir kodun **faturalanabilir** sayılması bu sözleşmenin değişikliğidir (§10).
- Tüketici `status`'u yalnız gösterim / gerekçe için kullanır; karar `isBillable` iledir.

## 5. Tüketici sınıflandırması — MOD-0120 eşlemesi (sözleşmenin parçası)

MOD-0120'nin beklentisi (pack kapanış tablosu, MOD-0149 satırı: "bulunamadı 422 · uygun değil 422 · erişilemez 503 ·
geçiş yok"; Runtime Constraints "Müşteri uygunluğu güncel durumdur"):

| CRM yanıtı | Sınıf | MOD-0120 sonucu |
|---|---|---|
| 200; `accountId` istenen (U-2'de: dolu, UUID) ; `contractVersion` `crm-account-lookup/1.` ile başlar; `isBillable = true`; `status = "active"` | uygun | işlem sürer |
| 200, tutarlı; `isBillable = false` ve `status ≠ "active"` | tanımlı, uygun değil | 422 `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE` |
| 404 (U-1 ya da U-2) | bulunamadı (yok / silinmiş / başka kiracı / eşleşmeyen dış referans) | 422 `RECEIVABLE_CUSTOMER_NOT_FOUND` |
| 409 (U-2, çoklu eşleşme) | belirsiz — veri bozulması, hesap belirlenemez | **Üretici kuralı K-3:** tahmin yok, hesap seçilmez, işlem durur; kalıcı hata (tekrar denemek çözmez) → öneri 422 ayrı kod (ör. `RECEIVABLE_CUSTOMER_AMBIGUOUS`), 503 değil; kod adı MOD-0120 sahibinin |
| 200 ama `accountId` yok / farklı; `isBillable` yok / `null`; `isBillable = true` iken `status ≠ "active"`; `isBillable = false` iken `status = "active"`; `status` / `code` / `name` yok ya da boş; `contractVersion` yok ya da `1.` değil; gövde ayrıştırılamıyor / `data` yok | geçersiz / çelişkili | 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE` |
| 400, 401 / 403 (izin ataması eksik dahil), 5xx (CRM 503 dahil), zaman aşımı, ulaşılamıyor | erişilemiyor | 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE` |

Kural: **geçersiz ya da çelişkili yanıt 503**, **tanımlı ama uygun olmayan durum 422**; hiçbir belirsiz yanıt "uygun"
sayılmaz ve hiçbirinde AR kaydı yazılmaz (geçiş yok). Karşılaştırmalar büyük/küçük harfe duyarlıdır. Tüketici
`code` / `name`'i yalnız anlık görüntü alanlarına yazar (MOD-0120 `CustomerCodeSnapshot` / `CustomerNameSnapshot`);
anlık görüntü uygunluk yerine geçmez. U-3 (seçici) bu tabloya girmez: seçicideki satır uygunluk değildir, seçilen kimlik
U-1 ile bu tabloya göre sınıflandırılır; U-3'ün 400 / 403 / 5xx yanıtı seçicide "arama yapılamadı" olarak gösterilir.

**K-3 (üretici kuralı, Beste 2026-10-30):** 409'da tüketici adaylardan birini seçmez; "ilk satır" ya da "en yeni" gibi
bir kural uygulamaz. 503 "geçici, sonra tekrar dene" anlamına geldiği için kullanılmaz: hata kalıcıdır ve kullanıcıya
"bu dış numara birden çok müşteriye bağlı; CRM'de düzeltilmeli" türünden bir gerekçe gösterilir. Düzeltme CRM'de
mükerrer `AccountExternalReference` satırının temizlenmesidir.

## 5a. Tüketiciler ve çağrı yolu

Pack §30 bu ekin tek tüketicisi olarak MOD-0120'yi sayar. Öteki satırlar **aday (istek)**: sözleşmeyi bağlamaz; tüketici
paketi bu sözleşmeye atıfla hizalanınca tüketici olur (sahip SCE) ve tablo 1.x içinde güncellenir.

| Tüketici | Uçlar | Amaç | Durum |
|---|---|---|---|
| MOD-0120 Accounts Receivable (Treasury) | U-1, U-3; U-2 MOD-0120 O-10 ile (DL-2) | müşteri seçimi (U-3), kayıt öncesi uygunluk (U-1, §5) | ilk tüketici; Y-4/4b |
| MOD-0177 Recall Readiness (MVP-1, Diten.InventoryService) | U-1 | geri çağırma ekranlarında müşteri adı ve kodu (`customerRef` = `Account.Id`, SHIPMENT-BUNDLE 3.5.0) | **aday (istek)** — MVP-1 isteği 2026-11-03 (`customerRef` SHIPMENT-BUNDLE 3.5.0). MOD-0177 paketi henüz hizalı değil: MVP-6 dalındaki sürüm (`origin/docs/sce/mvp6-dilim-b-packs-01`, :611-612) `CustomerRef`'i `shipToReference` / `DOC:` olarak tanımlıyor. Hizalanınca: 404'te kimlik tutulur, ad "bulunamadı" gösterilir |
| MOD-0183 Shipment Tracking / MOD-0186 Reverse Logistics (MVP-6) | U-1, U-3 | alıcı seçimi ve doğrulaması, iadede müşteri adı | **aday (istek)** — paketleri bugün sözleşmesiz iç uçları (`GET /api/crm/accounts`, `/{id}`, `crm.account.read`; MOD-0183:841-846, MOD-0186:1086) kullanıyor; U-1 / U-3'e geçiş paket revizyonuyla; adres okuması U-3'te yok — seçici + adres sözleşmesi K-9 |

- **Çağrı yolu:** tüketici CRM'i **sunucu tarafında, Gateway (5000) üzerinden, oturumdaki kullanıcının token'ıyla** çağırır
  (emsal: MOD-0177'nin SHIPMENT okuması). Servis kimliğiyle (servis-servis token) çağrı bu sözleşmede tanımlı değildir;
  kiracı kullanıcının token'ındaki `tenant_id`'den gelir (Gateway başlığa yazar, CRM karşılaştırır; `tenant_id`'siz
  token reddedilir, K-11; §1). Tarayıcı CRM'i doğrudan çağırmaz.
- **İzin ataması:** `crm.account.lookup-validation` her tüketicinin ilgili rollerine verilir (AR: MOD-0018-FU27; geri
  çağırma, sevkiyat ve iade rolleri: kendi sahipleri). Anahtar atanmamış kullanıcı 403 alır (§5: 503).
- Yeni bir tüketici bu tabloya eklenerek sözleşmeye girer (1.x içinde ekleme).

## 6. İzin — KARAR K-1 (commercial-suite sahibi, Beste, 2026-10-30)

- Amaç: AR kullanıcısına `crm.account.read` **vermemek** (MOD-0120 O-3 (b) reddedildi: CRM okuma yetkisi + sözleşmesiz).
  Üç uç ayrı, dar bir okuma anahtarıyla korunur; anahtar yalnız doğrulama alanlarını açar, hesap kaydını açmaz.
- **Anahtar: `crm.account.lookup-validation`** (K-1). PKS-001 (`.antigravity/rules/permission-key-standard.md`):
  3 segment, `^[a-z][a-z0-9-]*$` (§1); `lookup-validation` Tier-3 eylem sözlüğünde var; emsal
  `auth.users.lookup-validation` (§8.3). Değerlendirilip seçilmeyen aday: `crm.account.lookup` (sözlükte yok).
- `crm.account.read` bu uçlara yetmez; tek başına `crm.account.read` sahibi 403 alır.
- Ayrı işler (bu sözleşmenin dışında): Auth katalog kaydı (MOD-0018), AR
  rollerine atanması (MOD-0018-FU27 Treasury Auth eki ya da sahibinin seçtiği yer). CRM rol eşlemesi MOD-0149 §16'ya
  eklendi (#360, 445eb32b1). Anahtar atanmamış kullanıcıyla çağrı 403 → tüketici 503 (§5).
- Veri kapsamı: MVP kiracı kapsamı (MOD-0149 §16). Bölge / temsilci kapsamı (MOD-0018-FU15) ileride `crm.account.read`'e
  uygulanırsa bu anahtara uygulanıp uygulanmayacağı ayrı karardır; uygulanmazsa AR kiracı genelinde doğrular.

## 7. Hata modeli, 503, korelasyon

- **Zarf:** CRM'in mevcut `Response<T>` zarfı (`data`, `errors`, `statusCode`, `isSuccessful`); emsallerdeki gibi
  `data` yalnız 200'de dolu. CRM zarfında makine-okur hata kodu alanı yoktur; tüketici **yalnız HTTP durum koduna**
  göre karar verir, `errors[]` metni sözleşmenin parçası değildir (yerelleştirilmez, değişebilir).
- **Durum kodları:** 200 · 400 (U-3 ve U-2 girdisi; her uçta kiracı uyuşmazlığı ya da kiracısız çağrı, §1; U-1'de rota kısıtı `:guid` uymazsa yönlendirme 404'tür) · 401 · 403 ·
  404 (U-1, U-2; U-3'te yok) · 409 (yalnız U-2) · 503.
- **503 (kaynak okunamıyor):** CRM kendi deposunu okuyamazsa (Mongo istisnası, zaman aşımı) **503** döner; 500 ya da
  varsayılan değer dönmez, "uygun" ya da "bulunamadı" uydurmaz. Gövdede `data` yok. Bugün yakalanmamış Mongo istisnası
  genel hata yoluna düşer — 503 eşlemesi bu ekin uygulama işidir. Tüketici her 5xx'i 503 sayar (§5).
- **Korelasyon:** tüketici `X-Correlation-Id` başlığını gönderir (emsal tüketici davranışı, CrmService'in kendi
  istemcileri de iletir). CRM başlığı yanıt gövdesine taşımaz; başlığın yokluğu hata değildir. Gelen başlığı günlük
  kapsamına koyan ara katman CrmService Api'de ölçülmedi/bulunmadı → sözleşme 1.0 korelasyonu **garanti etmez**;
  tüketici CRM günlüğünde başlığa güvenmez. DL-1 uygulayıcısı paylaşılan bir ara katman bulursa bağlar ve raporlar
  (pack §30.9); bulamazsa madde K-10 olarak açık kalır (sahip commercial-suite / K2). Yeni bir mekanizma bu sözleşmeyle
  tanımlanmaz.
- **Önbellek:** yanıtlar güncel durumdur; tüketici uygunluk kararını önbelleğe almaz (MOD-0120 "Müşteri uygunluğu güncel
  durumdur").

## 8. Gateway

Ölçüm (@4dbcd67a8, bf3b0cd57'den beri değişmedi): `ocelot.json:2970-2989` `/api/crm/accounts/{everything}` → `http://localhost:5061/api/crm/accounts/{everything}`,
`UpstreamHttpMethod` GET dahil; `/api/crm/accounts` altında daha özel bir Upstream şablonu yok. `{everything}`
`{id}/lookup-validation`'ı, `lookup`'ı ve `external-references/resolve`'u kapsar; sorgu dizesi Ocelot'ta varsayılan olarak
iletilir. Emsal: `AccountContactController.cs:13` ve `AccountRelationshipController.cs:13` aynı joker rotayla, yeni rota
açmadan yayımlandı. Sonuç: **yeni Gateway rotası gerekmez**; `ocelot.json`'a dokunulmaz. Teyit integration-agent'ındır
(yalnız not). Servis tarafında `lookup` ve `external-references/resolve` sabit yolları `{id:guid}` kısıtlı rotalarla çakışmaz.

## 9. Uyum fikstürleri

Her biri öteki alanlar uygunken yalnız bir şeyi bozar. Fikstürler **tüketicinin** testidir: MOD-0120 onları kendi üretim
`ICustomerReferencePort` sınıflandırıcısına karşı koşar (MOD-0120 AC-3; DL-2 satırları O-10 ile). CRM'de sınıflandırıcı
kopyası yazılmaz; üreticinin uyumu gerçek işleyicilerle ölçülür (pack AC-AR-9a / AC-AR-9b). Dilim sütunu, fikstürün
dayandığı üretici dilimidir.

| Fikstür | Durum | Beklenen sınıf | Dilim |
|---|---|---|---|
| F-OK | `status = active`, `isBillable = true` | uygun | DL-1 |
| F-NE-x | `status ∈ {inactive, draft, suspended, archived}`, `isBillable = false` (dört fikstür) | 422 NOT_ELIGIBLE | DL-1 |
| F-UNK | `status = closed` (kümede yok), `isBillable = false` | 422 NOT_ELIGIBLE | DL-1 |
| F-404 | yok / silinmiş / başka kiracı (üç fikstür, gövdeleri bayt bayt aynı) | 422 NOT_FOUND | DL-1 |
| F-R404 | U-2 eşleşme yok; `oldcrm` ile `OldCRM` kaydı (harf farkı) | 422 NOT_FOUND | DL-2 |
| F-R409 | U-2 iki etkin aday satır | 422 belirsiz (K-3; kod adı MOD-0120'nin) | DL-2 |
| F-C1 | `isBillable = true` + `status = suspended` | 503 | DL-1 |
| F-C4 | `isBillable = false` + `status = active` | 503 | DL-1 |
| F-C2 | `accountId` farklı | 503 | DL-1 |
| F-C3 | `isBillable` yok | 503 | DL-1 |
| F-V | `contractVersion = crm-account-lookup/2.0` | 503 | DL-1 |
| F-503 | CRM 503 | 503 | DL-1 |
| F-403 | anahtar atanmamış kullanıcı (403) | 503 | DL-1 |

## 10. Sürüm kuralı

1.x yalnız ekleme (yeni alan, yeni isteğe bağlı parametre). Şunlar **büyük sürümdür**: 404 kararını genişletmek ya da
daraltmak (ör. faturalanamaz hesabı 404 yapmak), `isBillable` kuralını `active` dışına açmak ya da bir alanı kaldırmak /
yeniden adlandırmak. `account-status` kümesine faturalanamaz bir kod eklemek 1.x içindedir (tüketici `isBillable`'a
bakar). MOD-0003 Data Contract Registry yoktur (D27/1): dondurulunca bu dosya `docs/analysis/contracts/crm-account-lookup.md`
olur ve MOD-0003 gelince oraya kaydedilir. JSON özellik sırası sözleşmenin parçası değildir.

## 11. Açık kararlar (sözleşme dondurulmadan kapanmalı)

| # | Karar | Sahip | CT önerisi |
|---|---|---|---|
| K-1 | İzin anahtarı | commercial-suite (Beste) | **KAPANDI 2026-10-30:** `crm.account.lookup-validation`; Auth katalog + AR rol ataması ayrı iş |
| K-2 | `archived` sözleşmede yayımlanır mı | commercial-suite (Beste) | **KAPANDI 2026-10-30:** evet, `isBillable = false` |
| K-3 | U-2 409 karşısında tüketici davranışı | kural: commercial-suite (Beste); kod adı: MOD-0120 sahibi | **ÜRETİCİ KURALI 2026-10-30:** tahmin yok, işlem durur, kalıcı hata → öneri 422 ayrı kod (503 değil) |
| K-4 | Gateway: mevcut joker rota yeterli mi (§8) | integration-agent | **KAPANDI (sahip) 2026-10-30:** yeterli; integration-agent teyidi yalnız not |
| K-8 | MOD-0120 seçicisinin veri kaynağı | commercial-suite (Beste) | **KAPANDI 2026-11-04:** U-3 `lookup` aynı anahtarla, DL-1'de; MOD-0120:1146-1147 ve :1349-1350 hizalanır (Finans) |
| K-9 | MVP-6 seçici + adres için sözleşmeli yüzey | commercial-suite (Beste) | **AÇIK:** U-3 adres taşımaz; adres okuması bugün sözleşmesiz `GET /api/crm/accounts/{id}` (`crm.account.read`). Öneri: DL-1 sonrası 1.x eki |
| K-10 | Gelen `X-Correlation-Id`'nin CRM günlük kapsamına bağlanması (§7) | commercial-suite (K2) | **AÇIK:** 1.0 garanti etmez; DL-1 uygulayıcısı paylaşılan ara katmanı ölçer, varsa bağlar; yoksa K2 sonraki ek ya da platform işi olarak karar verir. Dondurmayı engellemez |
| K-11 | `tenant_id` taşımayan token'la çağrı | commercial-suite (Beste) | **KAPANDI 2026-11-05:** reddedilir — başlık olsa bile 400, veri yok (§1; pack AC-AR-15) |

Kayıt: K-1…K-6, K-8 ve K-11 `docs/records/decisions/2026-11/mod0149-ar-lookup-owner-decisions-01.md` (taslak; sahip okuması CT-APR-01 ile); K-7 = D112/2
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md`).
| K-7 | U-2'nin MOD-0120'deki kullanım yeri | MOD-0120 sahibi | **KAPANDI** (MOD-0120 sahibi, 2026-10-30): bu sürümde U-2'yi çağıran akış yok — `CustomerId` müşteri seçiciden `Account.Id` olarak gelir ve U-1 ile doğrulanır; U-2'yi kullanacak içe aktarma ve eski kayıt taşıma MOD-0120 O-10 ile bu sürümün dışında, banka eşleştirme pakette yok. **DL-2 O-10'a kadar ertelendi;** DL-1 ve sözleşme belgesi bu sürümün önkoşulu. `sourceSystem` listesi O-10'da belirlenir; büyük/küçük harf kuralı sözleşmede (§3.1) bağlayıcı. K-3'teki tüketici hata kodu O-10 kapsamında MOD-0120'ye girer. |

## 12. Değişiklik geçmişi

- 1.0.0 (2026-10-30) — ilk yazılı sürüm; MOD-0120 O-3 / O-4 ve Y-4/4b, Y-4/4c'den; MOD-0149 pack eki ile birlikte.
- 1.0.0 (2026-10-30, aynı gün, taslak düzeltmesi) — sahip kararları: K-1 anahtar, K-2 `archived`, K-3 üretici kuralı
  (409 → tahmin yok, kalıcı hata); K-7 MOD-0120 sahibine soru.
- 1.0.0 (2026-10-30, MOD-0120 sahibi yanıtı) — K-7 kapandı: U-2 MOD-0120 O-10'a kadar ertelendi; U-1 ve bu belge bu
  sürümün önkoşulu.
- 1.0.0 (2026-11-04, bağımsız inceleme `docs/records/reviews/MOD-0149/2026-11-04-6df3626c.md` sonrası) — F-4: dondurma
  DL-1'e (U-1, U-3) bağlandı, U-2 DL-2 ile ayrıca donar; F-7: ölçümler 4dbcd67a8'de yenilendi, §6 CRM rol eşlemesi
  güncellendi; K-8: U-3 `lookup` (§2a); §5a tüketiciler (MOD-0177, MOD-0183 / MOD-0186) ve çağrı yolu; §9 fikstürlerine
  dilim sütunu; K-9 açık karar.
- 1.0.0 (2026-11-04, eski sürümün (paket 4e6c020d, sözleşme ec4aaa16) ayrı bir bağımsız incelemesinden kalan bulgular) —
  üretici uyumu gerçek işleyicilerle ölçülür, §9 fikstürleri tüketicinin testidir; §5 `isBillable = false` + `active`
  çelişkisi 503 (F-C4); §3.1 256 karakter sınırı bağlayıcı (DL-2); §4 sürüm atfı §10; §7 korelasyon K-10 olarak
  sahiplendi; K kararları kayda bağlandı.
- 1.0.0 (2026-11-05, dar yeniden inceleme `docs/records/reviews/MOD-0149/2026-11-05-a7cdea72.md` sonrası) — F-1: U-3
  kararlı sıralama (ad + `accountId`); F-2: §5a'da MOD-0177 / MOD-0183 / MOD-0186 aday (istek); F-3: §1 kiracı
  mekanizması kodla hizalandı (Gateway token → `X-Tenant-Id`, CRM karşılaştırır; uyuşmazlık ve kiracısız çağrı 400).
- 1.0.0 (2026-11-05, dar yeniden inceleme 2 `docs/records/reviews/MOD-0149/2026-11-05-999c7e29.md` sonrası) — §1 K-11:
  `tenant_id`'siz token reddedilir; künyede adaylar ayrıldı; §11'e karar kaydı atfı. U-3 sıralamasının yalnız U-3
  yolunda olduğu ve ek indeks pack §30.2'de.
