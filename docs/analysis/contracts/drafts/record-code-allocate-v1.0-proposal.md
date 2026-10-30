# RECORD-CODE-ALLOCATE 1.0.0 — ortak kayıt kodu ayırma sözleşmesi (TASLAK / öneri)

    contract       : RECORD-CODE-ALLOCATE
    version        : 1.0.0 (önceki: v0 — yazılı sözleşme belgesi yoktu; KARAR D59/1 metni, Beste Y-8 2026-10-28)
    x-status       : DRAFT (rev 1) — öneri; MOD-0040-FU02 paketi CT-APR-02 / CT-APR-01'den geçmeden ve açık noktalar (§12)
                     kapanmadan dondurulmaz; dondurma ayrı CT adımıdır (hedef 2026-11-03), dondurulmuş dosyanın yeri o adımda
                     belirlenir
    x-owner        : MOD-0040-FU02 Record Code Service (platform-shared-services, Diten.Platform 5057) — sahip Beste (K2)
    x-module-pack  : execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md
    x-consumers    : Diten.FinanceTreasuryService (MOD-0126 PAY, MOD-0119 APD, MOD-0120 ARI, MOD-0124 CPS / CTP);
                     Diten.MdmService (MOD-0289 BNK / BAC, MOD-0217 AGR); sonra Diten.InventoryService (MOD-0178, paket DL-8)
    x-decision     : sahip kararları K-3, K-4, K-5, K-7, K-8 (Beste 2026-10-30; paket Bölüm 0.1); KARAR D59/1
                     (docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:605-610); KARAR D91 (kısa kod kaynağı)
    x-rule         : RCS-001 (.antigravity/rules/record-coding-standard.md) §1, §3, §5.1–§5.12
    x-measured-at  : origin/main @ 6d49f5823 (2026-10-30)

Kural: **v0'a göre değişiklik yalnız eklemedir** (D59/1, K-3). v0 gövdesiyle (`legalEntityId`, `recordType`,
`idempotencyKey`) gelen istek v1'de aynı anlamı taşır (`prefixScope` yoksa `LegalEntity`). Tablo §9'da.

## 1. Uç ve kimlik

- `POST /api/internal/record-codes/allocate` — Diten.Platform (5057), **servisten servise**. Gateway'de rotası **yoktur**
  (ölçüldü: `gateway/Diten.ApiGateway/ocelot.json`'da `/internal` geçen rota 0); tarayıcı çağırmaz.
- Başlıklar (her biri **tam bir kez**; paket Bölüm 8.2, CT önerisi P2 — açık nokta §12 N-1):

| Başlık | Değer | Eksik / çift / yanlış |
|---|---|---|
| `X-Record-Code-Credential-Id` | tüketicinin kimlik bilgisi tanımlayıcısı (ortam gizli bilgisi) | 401 |
| `X-Record-Code-Credential` | kimlik bilgisi sırrı (ortam gizli bilgisi; döndürme destekli) | 401 |
| `X-Record-Code-Audience` | `TRUSTED_RECORD_CODE_ALLOCATE` | 401 ya da 403 |
| `Authorization` | `Bearer <işlemi başlatan kiracı kullanıcısının JWT'si>` — çağıran servis gelen isteğin token'ını iletir | 401 / 403 |
| `X-Tenant-Id` | **gönderilmez** | varsa 403 `RECORD_CODE_FORBIDDEN` |
| `Content-Type` | `application/json` | 400 |

- **Kiracı** yalnız doğrulanmış kullanıcı token'ının `tenant_id` iddiasıdır (Guid, boş değil); token ayrıca tam bir `sub`
  (Guid) ve `actor_type = tenant_user` taşır. Gövde, başlık ya da sorgu dizesindeki kiracı değeri hiçbir zaman okunmaz.
- **Çağıran servis** kimlik bilgisinin bağlı olduğu servistir (ör. `DITENFINANCETREASURYSERVICE`); her bağlamanın izinli
  tür listesi vardır (§6). Kullanıcıdan ayrıca Platform izni istenmez.
- Kimlik bilgisi değerleri bu belgede ve depoda **yoktur** (K-8); ortam gizli bilgisinden sağlanır.

## 2. İstek gövdesi

JSON nesnesi, ≤ 4 KB (aşarsa 413). **Bilinmeyen üye 400** (ör. `tenantId`, `code`); tekrarlı üye 400.

| Alan (JSON) | Tip | Zorunlu | Kural |
|---|---|---|---|
| `recordType` | string | evet | `^[A-Z]{3}$`, büyük/küçük harfe duyarlı, normalleştirme yok. Biçimsiz → 400; biçimli ama katalog dışı → 422 `RECORD_TYPE_UNKNOWN` |
| `prefixScope` | string | hayır | `LegalEntity` \| `Group` \| `Platform` (tam yazım); yoksa `LegalEntity`; başka değer → 400 |
| `legalEntityId` | string (UUID) \| null | kapsama bağlı | `LegalEntity`: zorunlu, boş olmayan UUID. `Group` / `Platform`: yok ya da `null`. UUID değil → 400; kapsamla uyumsuz → 422 `RECORD_CODE_SCOPE_INVALID` |
| `idempotencyKey` | string | evet | 1–200 karakter, yazdırılabilir ASCII (`!`…`~`, boşluk yok) — §12 N-3. Çağıran, Create'inin kendi idempotency anahtarından türetir |

## 3. Cevap 200

Platform `Response<T>` zarfı (`services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs:7-16`; zarf §12 N-2):

```json
{ "data": { "code": "MYG-PAY-000042", "sequence": 42 },
  "statusCode": 200, "isSuccessful": true, "errors": [],
  "reason_code": null, "correlation_id": "0HN7…" }
```

| Alan | Tip | Anlam |
|---|---|---|
| `data.code` | string | verilen kod; §7 kalıbı; ASCII |
| `data.sequence` | integer (64 bit) | sayaçtan alınan numara; `code`'un son parçasının sayısal değeri |

- Aynı `idempotencyKey` + aynı gövde (kiracı ve çağıran aynı) → **aynı** `code` ve `sequence`, 200; sayaç ilerlemez.
- Yeni ayırma da tekrar da 200'dür (ayrım çağıran için anlamsızdır; ikisinde de kod aynıdır).

## 4. Hatalar

Hata gövdesi aynı zarftır: `data = null`, `isSuccessful = false`, `reason_code` aşağıdaki kod, `errors[]` İngilizce
tanılama metni (**tüketici ekranında gösterilmez**; tüketici `reason_code`'u kendi dil dosyasıyla çevirir).

| HTTP | `reason_code` | Ne zaman | Tüketici davranışı |
|---|---|---|---|
| 400 | `RECORD_CODE_REQUEST_INVALID` | gövde ayrıştırılamıyor, bilinmeyen/tekrarlı üye, alan biçimi yanlış, tanımsız `prefixScope` | hata (çağıran kodu yanlış); Create yapılmaz |
| 400 | `IDEMPOTENCY_KEY_REUSED` | aynı anahtar daha önce **farklı** gövdeyle (tür, kapsam ya da şirket) kullanılmış | hata; Create yapılmaz |
| 401 | `RECORD_CODE_UNAUTHENTICATED` | kimlik bilgisi başlıkları eksik/çift, kimlik bilgisi doğrulanmıyor, kullanıcı token'ı yok / geçersiz / süresi dolmuş | Create 503 (kodsuz kayıt yok) |
| 403 | `RECORD_CODE_FORBIDDEN` | kimlik bilgisi iptal / hedef kitle yanlış; token iddiaları eksik (`tenant_id`, `sub`, `actor_type`); `X-Tenant-Id` var; tür bu çağırana izinli değil | Create 503 |
| 404 | `LEGAL_ENTITY_NOT_FOUND` | şirket yok, silinmiş, **başka kiracının**, ya da aktif değil (MDM 404; tek gövde, sızıntısız) | Create 422/404 (tüketicinin kendi kodu) |
| 409 | `LEGAL_ENTITY_SHORT_CODE_MISSING` | şirketin MDM `code`'u `^[A-Z]{2,5}$`'e birebir uymuyor (boş, küçük harf, `GMG-CH`, 1 ya da 6+ harf, ASCII dışı, boşluklu) | Create yapılmaz; kullanıcıya "şirket kısa kodunu düzeltin" (tüketicinin metni) |
| 409 | `GROUP_PREFIX_MISSING` | `Group` kapsamı istendi, kiracının grup öneki kaydı yok | Create yapılmaz |
| 413 | `RECORD_CODE_REQUEST_TOO_LARGE` | gövde 4 KB üstü | hata |
| 422 | `RECORD_TYPE_UNKNOWN` | tür katalogda yok (KALSIN türleri, İKİSİ DE türleri, `CTR`, henüz kaydı olmayan türler dahil) | hata |
| 422 | `RECORD_CODE_SCOPE_INVALID` | tür bu kapsama izin vermiyor; `Group`/`Platform` + dolu `legalEntityId`; `LegalEntity` + boş `legalEntityId`; HTTP'den `Platform` (v1, §12 N-5) | hata |
| 503 | `LEGAL_ENTITY_SOURCE_UNAVAILABLE` | MDM erişilemez, 5xx, 3 s aşımı, 400/401/403, ya da çelişkili/ayrıştırılamaz yanıt | **Create yapılmaz** (fail-closed); aynı anahtarla yeniden denenebilir |
| 503 | `RECORD_CODE_UNAVAILABLE` | Platform deposu / işlem / denetim kutusu yazılamadı, istek bütçesi (5 s) doldu | **Create yapılmaz**; aynı anahtarla yeniden denenebilir |

Tüketici kuralı (D59/1): **200 dışı her cevap, zaman aşımı ya da port bağlı değil → Create başarısız**, kodsuz kayıt
açılmaz, yedek kod ya da kendi sayacı üretilmez (RCS-001 §6).

## 5. Değerlendirme sırası

Birden çok hata aynı anda varsa ilk tutan döner:

1. 413 boyut → 2. 401 / 403 başlık, kimlik bilgisi, kullanıcı token'ı, `X-Tenant-Id` → 3. 400 gövde →
4. idempotency: kayıt varsa parmak izi eşit → **200 tekrar**, değil → 400 `IDEMPOTENCY_KEY_REUSED` →
5. 422 `RECORD_TYPE_UNKNOWN` → 6. 403 tür çağırana izinsiz → 7. 422 `RECORD_CODE_SCOPE_INVALID` →
8. önek: `Group` → 409 `GROUP_PREFIX_MISSING`; `LegalEntity` → MDM (404 / 409 / 503) →
9. sayaç (503) → 10. defter + denetim tek işlemde (503) → 200.

Sonuç: MDM, kapsam ya da tür hatasında **sayaç ilerlemez**. Sayaç ilerledikten sonra (adım 9–10) düşen istekte numara bir
boşluk olarak kalır (RCS-001 §5.8 kabul eder); kod hiçbir zaman iki kez verilmez.

## 6. Kapsamlar ve tür kataloğu (v1)

| `prefixScope` | `legalEntityId` | Önek | Sayaç anahtarı |
|---|---|---|---|
| `LegalEntity` (varsayılan) | zorunlu | MDM MOD-0220 `LegalEntity.Code` (lookup-validation; kiracı + aktif) | (kiracı, kısa kod, tür) |
| `Group` | boş | kiracının grup öneki kaydı (bugün `GMG`; RCS-001 §5.3) | (kiracı, grup öneki, tür) |
| `Platform` | boş | yok (öneksiz) | (Platform, "", tür) — kiracısız; v1'de HTTP'den kapalı |

| `recordType` | Kayıt türü | İzinli kapsam | İzinli çağıran (v1) |
|---|---|---|---|
| `PAY` | Ödeme Partisi (MOD-0126) | LegalEntity | FinanceTreasury |
| `APD` | AP Belgesi (MOD-0119) | LegalEntity | FinanceTreasury |
| `ARI` | AR Alacak Faturası (MOD-0120) | LegalEntity | FinanceTreasury |
| `CPS` | Nakit Pozisyonu Anlık Görüntüsü (MOD-0124) | LegalEntity | FinanceTreasury |
| `CTP` | Nakit Eşik Politikası (MOD-0124) | LegalEntity | FinanceTreasury |
| `BNK` | Banka (MOD-0289) | **yalnız Group** | Mdm |
| `BAC` | Banka Hesabı (MOD-0289) | LegalEntity (şirketin kendi hesabı; `legalEntityId` = sahip şirket) **ve** Group (tedarikçi hesabı) | Mdm |
| `AGR` | Sözleşme (MOD-0217) | LegalEntity (yöneten şirket) | Mdm |
| `ORG` | Organizasyon Birimi (MOD-0288) | LegalEntity (öneri) | yalnız Platform süreç içi (paket DL-2) |
| `POS` | Pozisyon (MOD-0288) | LegalEntity (öneri) | yalnız Platform süreç içi (paket DL-2) |
| `PLN` | Abonelik Planı | Platform | yalnız Platform süreç içi — v1 kataloğunda olup olmadığı açık nokta (§12 N-6) |

`CTR` kullanılmaz: Procurement MOD-0144 bugün `CTR-` + 8 hex üretiyor (`CreateContractHandler.cs:130-131`); aynı önek iki
türde olamaz (RCS-001 §5.5) → MOD-0217 `AGR` (K-7). RCS-001 §3'teki `CTR` satırının `AGR`'ye çevrilmesi ayrı CT adımıdır.

## 7. Biçim ve sayaç kuralları

- `LegalEntity` / `Group`: `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$` (ör. `MYG-PAY-000042`, `GMG-BNK-000001`).
- `Platform`: `^[A-Z]{3}-[0-9]{6,}$` (ör. `PLN-000001`).
- Sayaç en az 6 hane sıfır dolgulu; `999999`'dan sonra `1000000` (7 hane); sıfıra dönmez, yıl/dönem/şirketle sıfırlanmaz
  (RCS-001 §1, §5.7). Tüketici sabit uzunluk varsaymaz.
- Yalnız ASCII `A–Z`, `0–9`, `-` (§5.10). Önek, MDM değeri olduğu gibi kalıba uyuyorsa kullanılır; servis büyütmez, kırpmaz,
  harf çevirmez.
- Önek koda **bir kez** yazılır; şirketin kısa kodu sonra değişse de verilmiş kod değişmez (§5.2). Yeni ayırma o andaki
  kısa kodu kullanır.
- Ardışıklık garanti değildir (boşluk olabilir); tekillik garantidir: kod kiracıda tekildir.
- **Şirket öneki biçimi** RCS-001 §1'e göredir; K1'in 2026-11-02'ye kadar vereceği karar (tek grup öneki / öneksiz)
  bu bölümü değiştirebilir — gelmezse bu biçimle dondurulur (§12 N-7).

## 8. Idempotency

- Anahtar kapsamı: (kiracı, çağıran servis, `idempotencyKey`). Başka kiracı ya da başka servis aynı anahtarı bağımsız kullanır.
- Parmak izi: `recordType`, `prefixScope` (varsayılan uygulanmış hâli) ve `legalEntityId`. v0 gövdesi ile aynı anlamdaki v1
  gövdesi (`prefixScope: "LegalEntity"` açıkça yazılmış) aynı parmak izidir.
- Ömür sınırsızdır: anahtar ne zaman tekrar edilirse aynı kod döner. Tekrar MDM'e gitmez (MDM kapalıyken de 200 döner).
- Aynı anahtarla eşzamanlı istekler tek kod üretir.
- Çağıranın yeniden deneme kuralı: zaman aşımı, 503 ya da bağlantı hatasından sonra **aynı anahtarla** yeniden dener;
  yeni anahtar yeni kod demektir. 4xx yeniden denenmez.

## 9. v0'dan farklar (yalnız ekleme)

| Konu | v0 (D59/1) | v1.0.0 |
|---|---|---|
| Gövde | `{legalEntityId, recordType, idempotencyKey}` | aynı + isteğe bağlı `prefixScope` (yoksa `LegalEntity`); `Group`/`Platform`'da `legalEntityId` boş |
| 200 | `{code, sequence}`; aynı anahtar aynı kod | aynı, `Response<T>` zarfının `data`'sında (§12 N-2) |
| 422 | `RECORD_TYPE_UNKNOWN` | aynı + `RECORD_CODE_SCOPE_INVALID` |
| 409 | `LEGAL_ENTITY_SHORT_CODE_MISSING` | aynı + `GROUP_PREFIX_MISSING` |
| 404 | başka kiracı | aynı; `reason_code` `LEGAL_ENTITY_NOT_FOUND` (yok / aktif değil de aynı gövde) |
| 503 | servis yok → Create başarısız | aynı; iki neden kodu `LEGAL_ENTITY_SOURCE_UNAVAILABLE`, `RECORD_CODE_UNAVAILABLE` |
| 400 | — | `RECORD_CODE_REQUEST_INVALID`, `IDEMPOTENCY_KEY_REUSED` (K-3) |
| 401 / 403 / 413 | — | `RECORD_CODE_UNAUTHENTICATED`, `RECORD_CODE_FORBIDDEN`, `RECORD_CODE_REQUEST_TOO_LARGE` |
| Kimlik | "kiracı güvenilir bağlamdan" | kimlik bilgisi başlıkları + iletilen kullanıcı token'ı (§1) — §12 N-1 |

Tüketici port imzası (Treasury `IRecordCodeAllocator.AllocateAsync(legalEntityId, recordType, idempotencyKey)`):
`LegalEntity` türleri için değişmeden çalışır; `BNK` ve tedarikçi `BAC` için `prefixScope` taşıması gerekir (§12 N-4).

## 10. Örnekler

Şirket kodu (`LegalEntity`, MDM `code = MYG`):

```json
POST /api/internal/record-codes/allocate
{ "recordType": "PAY", "prefixScope": "LegalEntity",
  "legalEntityId": "6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7",
  "idempotencyKey": "payment-batch:create:9b2f0c4e-1d7a-4a55-9a3e-0c6b8f2d7e11" }

200 { "data": { "code": "MYG-PAY-000042", "sequence": 42 }, "statusCode": 200, "isSuccessful": true,
      "errors": [], "reason_code": null, "correlation_id": "…" }
```

v0 gövdesi (aynı sonuç):

```json
{ "recordType": "PAY", "legalEntityId": "6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7",
  "idempotencyKey": "payment-batch:create:9b2f0c4e-1d7a-4a55-9a3e-0c6b8f2d7e11" }
```

Grup öneki (banka):

```json
{ "recordType": "BNK", "prefixScope": "Group", "legalEntityId": null,
  "idempotencyKey": "bank:create:2a6d1f3c-7b8e-4c90-8d12-5e4f3a2b1c0d" }

200 { "data": { "code": "GMG-BNK-000001", "sequence": 1 }, … }
```

Hatalar:

```json
{ "recordType": "BNK", "prefixScope": "LegalEntity", "legalEntityId": "6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7",
  "idempotencyKey": "k-1" }
422 { "data": null, "statusCode": 422, "isSuccessful": false, "errors": ["…"],
      "reason_code": "RECORD_CODE_SCOPE_INVALID", "correlation_id": "…" }

{ "recordType": "PAY", "legalEntityId": "<MDM code = GMG-CH olan şirket>", "idempotencyKey": "k-2" }
409 { …, "reason_code": "LEGAL_ENTITY_SHORT_CODE_MISSING" }

{ "recordType": "PAY", "legalEntityId": "<başka kiracının şirketi>", "idempotencyKey": "k-3" }
404 { …, "reason_code": "LEGAL_ENTITY_NOT_FOUND" }
```

## 11. Denetim

- Her yeni ayırma Platform merkezi günlüğüne (`audit_events`) **ayırmayla aynı işlemde** yazılır (kayıt yoksa kod yok):
  aktör türü `Service`, `SourceService` = çağıran servis, `TenantId` = token kiracısı, varlık `RecordCodeAllocation`,
  işlem `Create`; metadata `recordType`, `prefixScope`, `legalEntityId`, `prefix`, `code`, `sequence`, `idempotencyKey`,
  `onBehalfOfUserId` (token `sub`).
- Kiracısı bilinen retler en iyi çaba bir ret kaydı yazar (paket AC-19; açık karar A-14).
- Tekrar (aynı anahtar) yeni kayıt yazmaz.
- Bu kayıt BL-501 ortak ileticisiyle değil Platform içinden yazılır. Tüketici kendi Create kaydını kendi yoluyla yazar;
  iki kayıt `idempotencyKey` + `code` ile eşleştirilir.

## 12. Açık noktalar (dondurmadan önce kapanır)

| # | Nokta | Öneri | Sahip | Son tarih |
|---|---|---|---|---|
| N-1 | İç uç kimlik deseni (paket A-13): kimlik bilgisi + iletilen kullanıcı token'ı (P2). Sınır: kullanıcı token'ı olmayan arka plan işi çağıramaz (401) | P2; arka plan çağıranı paket DL-9 | Beste | 2026-11-03 |
| N-2 | `Response<T>` zarfı — v0 "200 {code, sequence}" yazıyordu | zarf (Platform uçlarının hepsi gibi); `data` içeriği v0 ile aynı | Beste + Treasury CT | 2026-11-03 |
| N-3 | `idempotencyKey` biçimi (1–200 yazdırılabilir ASCII) | taslaktaki gibi | Beste + Treasury CT | 2026-11-03 |
| N-4 | Tüketici portuna `prefixScope` eklenmesi (BNK, tedarikçi BAC) | port imzasına isteğe bağlı parametre | Treasury CT + MDM sahibi | 2026-11-03 |
| N-5 | `Platform` kapsamı HTTP'den kapalı (paket A-6) | kapalı | Beste | 2026-11-03 |
| N-6 | `PLN` v1 kataloğunda mı (paket A-7) | v1'de, yalnız süreç içi | Beste | 2026-11-03 |
| N-7 | Şirket öneki biçimi (K-6; paket A-1) | karar yoksa RCS-001 §1 | K1 (`gmg-10`) | 2026-11-02 |
| N-8 | `CTR` → `AGR` için RCS-001 §3 satırı (paket A-3); MOD-0144 / MOD-0217 kapsam çakışması (paket A-2) | `AGR` sözleşmede kalır | CT; Cihan + Treasury | 2026-11-03 (§3); TBD (çakışma) |

## 13. Sürüm kuralı

- 1.0.0 v0'a göre yalnız ekleme; v0 gövdesiyle yazılmış tüketici değişmeden çalışır (§9).
- Bundan sonraki her değişiklik de yalnız ekleme olur (yeni tür, yeni isteğe bağlı alan, yeni neden kodu). Bir alanın
  kaldırılması, anlamının değişmesi, kod kalıbının (§7) değişmesi ya da var olan bir durum kodunun anlamının değişmesi
  **büyük sürüm** sayılır.
- Yeni tür eklemek sözleşme değişikliği değildir; katalog RCS-001 §3'e bağlıdır (tür önce §3'e, sonra kataloğa girer).

## 14. Değişiklik geçmişi

- 1.0.0 rev 1 (2026-10-30) — ilk yazılı sürüm (taslak). Kaynak: sahip kararları K-3…K-8 (Beste 2026-10-30), v0 metni
  (D59/1, Y-8). v0'a eklenenler §9'da.
