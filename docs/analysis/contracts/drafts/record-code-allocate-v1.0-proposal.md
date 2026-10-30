# RECORD-CODE-ALLOCATE 1.0.0 — ortak kayıt kodu ayırma sözleşmesi (TASLAK / öneri)

    contract       : RECORD-CODE-ALLOCATE
    version        : 1.0.0 (önceki: v0 — yazılı sözleşme belgesi yoktu; KARAR D59/1 metni, Beste Y-8 2026-10-28)
    x-status       : DRAFT (rev 2 — bağımsız inceleme 01 UYGUN DEĞİL bulguları ve sahip kararları K-8r, K-9…K-12 işlendi) —
                     öneri; MOD-0040-FU02 paketi CT-APR-02 / CT-APR-01'den geçmeden ve açık noktalar (§12) kapanmadan
                     dondurulmaz; dondurma ayrı CT adımıdır (hedef 2026-11-03), dondurulmuş dosyanın yeri o adımda belirlenir
    x-owner        : MOD-0040-FU02 Record Code Service (platform-shared-services, Diten.Platform 5057) — sahip Beste (K2)
    x-module-pack  : execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md
    x-consumers    : Diten.FinanceTreasuryService (MOD-0126 PAY, MOD-0119 APD, MOD-0120 ARI, MOD-0124 CPS / CTP);
                     Diten.MdmService (MOD-0289 BNK / BAC, MOD-0217 AGR); sonra Diten.InventoryService (MOD-0178, paket DL-8)
    x-decision     : sahip kararları K-3, K-4, K-5, K-7, K-8r, K-9, K-10, K-11, K-12 (Beste 2026-10-30; paket Bölüm 0.1);
                     KARAR D59/1 (docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:605-610); KARAR D91
    x-rule         : RCS-001 (.antigravity/rules/record-coding-standard.md) §1, §3, §5.1–§5.12
    x-measured-at  : origin/main @ 6d49f5823 (2026-10-30)

**Uyumluluk kuralı (F-5 ile düzeltildi).** v0'a göre **istek gövdesi ve hata kodlarında değişiklik yalnız eklemedir**
(D59/1, K-3): v0 gövdesi (`legalEntityId`, `recordType`, `idempotencyKey`) v1'de aynı anlamı taşır (`prefixScope` yoksa
`LegalEntity`), v0'ın dört hata kodu aynen kalır. **Yanıt biçimi v0'da bağlayıcı değildi**: v0 metni "200 `{code,
sequence}`" yazıyordu ama zarfı tanımlamıyordu. 1.0.0 yanıtı Platform `Response<T>` zarfının `data` alanında dondurulur
(`data` içeriği v0 ile aynı); bu Treasury CT teyidine bağlıdır (§12 N-2, son tarih 2026-11-02). Tablo §9'da.

## 1. Uç ve kimlik

- `POST /api/internal/record-codes/allocate` — Diten.Platform (5057), **servisten servise**, bir kullanıcı adına. Gateway'de
  rotası **yoktur** (ölçüldü: `gateway/Diten.ApiGateway/ocelot.json`'da `/internal` geçen rota 0); tarayıcı çağırmaz.
- Kimlik deseni P2: tüketici kimlik bilgisi + iletilen kullanıcı token'ı (MOD-0018-FU21 deseni; sahip kararı K-12 e).

| Başlık | Değer | Eksik / çift / yanlış |
|---|---|---|
| `X-Record-Code-Credential-Id` | tüketicinin kimlik bilgisi tanımlayıcısı (ortam gizli bilgisi) | eksik / çift → 401; bilinmiyor → 401 |
| `X-Record-Code-Credential` | kimlik bilgisi sırrı (ortam gizli bilgisi; döndürme penceresi destekli) | eksik / çift / yanlış → 401; **iptal edilmiş kimlik bilgisi → 401** |
| `X-Record-Code-Audience` | `TRUSTED_RECORD_CODE_ALLOCATE` | eksik / çift → 401; farklı değer → 403 |
| `Authorization` | `Bearer <işlemi başlatan kiracı kullanıcısının JWT'si>` — çağıran servis gelen isteğin token'ını iletir | yok / geçersiz / süresi dolmuş → 401; `tenant_id` / `sub` / `actor_type = tenant_user` eksik → 403 |
| `X-Tenant-Id` | **gönderilmez** | varsa 403 `RECORD_CODE_FORBIDDEN` |
| `Content-Type` | `application/json` | 400 |

- **Kiracı** yalnız doğrulanmış kullanıcı token'ının `tenant_id` iddiasıdır. Gövde, başlık ya da sorgu dizesindeki kiracı
  değeri hiçbir zaman okunmaz. Kiracı Platform'da **Active** olmalıdır (askıda / sağlanıyor / devre dışı → 403).
- **Çağıran servis** kimlik bilgisinin bağlı olduğu servistir; kapalı listedir:

| Kimlik bilgisi bağlaması | Servis adı (denetimde `SourceService`) | İzinli `recordType` |
|---|---|---|
| `FinanceTreasury` | `Diten.FinanceTreasuryService` | `PAY`, `APD`, `ARI`, `CPS`, `CTP` |
| `Mdm` | `Diten.MdmService` | `BNK`, `BAC`, `AGR` |

  Servis adı gövdeden ya da başlıktan **asla** alınmaz.
- **Kullanıcı izni:** kullanıcı token'ı türün oluşturma iznini taşımalıdır (yoksa 403 `RECORD_CODE_PERMISSION_MISSING`):

| `recordType` | Gerekli izin (MOD-0018-FU27 taslağındaki ad; FU27 onayıyla kesinleşir) |
|---|---|
| `PAY` | `finance-treasury.payment-batches.create` |
| `APD` | `finance-treasury.ap-invoices.create` |
| `ARI` | `finance-treasury.receivable-invoices.create` |
| `CPS` | `finance-treasury.cash-positions.create` |
| `CTP` | `finance-treasury.cash-position-threshold-policies.create` |
| `BNK` | `mdm.banks.create` |
| `BAC` | `mdm.bank-accounts.create` |
| `AGR` | `mdm.contracts.create` |

- LegalEntity kapsamında Platform, kullanıcı token'ını MDM lookup-validation'a iletir; kullanıcı `mdm.legal-entities.read`
  taşımalıdır (yoksa MDM 403 → bu uçta 503).
- Hız sınırı: kimlik bilgisi tanımlayıcısı başına sabit pencere; aşımda 429 (değer §12 N-9).
- Kimlik bilgisi değerleri bu belgede ve depoda **yoktur**; ortam gizli bilgisinden sağlanır.

## 2. İstek gövdesi

JSON nesnesi, ≤ 4 KB (aşarsa 413). **Bilinmeyen üye 400** (ör. `tenantId`, `sourceService`, `code`); tekrarlı üye 400.

| Alan (JSON) | Tip | Zorunlu | Kural |
|---|---|---|---|
| `recordType` | string | evet | `^[A-Z]{3}$`, büyük/küçük harfe duyarlı, normalleştirme yok. Biçimsiz → 400; katalog dışı → 422 `RECORD_TYPE_UNKNOWN` |
| `prefixScope` | string | hayır | `LegalEntity` \| `Group` \| `Platform` (tam yazım); yoksa `LegalEntity`; başka değer → 400. **`Platform` 1.0.0'da tanımlıdır ama DL-4'e kadar her istekte 422 `RECORD_CODE_SCOPE_INVALID` alır** (K-11) |
| `legalEntityId` | string (UUID) \| null | kapsama bağlı | `LegalEntity`: zorunlu, boş olmayan UUID. `Group` / `Platform`: yok ya da `null`. UUID değil → 400; kapsamla uyumsuz → 422 `RECORD_CODE_SCOPE_INVALID` |
| `idempotencyKey` | string | evet | 1–200 karakter, yazdırılabilir ASCII (`!`…`~`, boşluk yok) — §12 N-3. Çağıran, Create'inin kendi idempotency anahtarından türetir |

## 3. Cevap 200

Platform `Response<T>` zarfı (`services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs:7-16`); zarf
kararı §12 N-2:

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
- Yeni ayırma da tekrar da 200'dür.

## 4. Hatalar

Hata gövdesi aynı zarftır: `data = null`, `isSuccessful = false`, `reason_code` aşağıdaki kod, `errors[]` İngilizce
tanılama metni (**tüketici ekranında gösterilmez**; tüketici `reason_code`'u kendi dil dosyasıyla çevirir).

| HTTP | `reason_code` | Ne zaman | Tüketici davranışı |
|---|---|---|---|
| 400 | `RECORD_CODE_REQUEST_INVALID` | gövde ayrıştırılamıyor, bilinmeyen/tekrarlı üye, alan biçimi yanlış, tanımsız `prefixScope` | hata; Create yapılmaz |
| 400 | `IDEMPOTENCY_KEY_REUSED` | aynı anahtar daha önce **farklı** gövdeyle kullanılmış | hata; Create yapılmaz |
| 401 | `RECORD_CODE_UNAUTHENTICATED` | kimlik bilgisi başlıkları eksik/çift; kimlik bilgisi bilinmiyor, sırrı yanlış ya da **iptal edilmiş**; kullanıcı token'ı yok / geçersiz / süresi dolmuş | Create 503 |
| 403 | `RECORD_CODE_FORBIDDEN` | kimlik bilgisi yapılandırması geçersiz; hedef kitle ya da tüketici uyuşmuyor; `X-Tenant-Id` var; token iddiaları eksik; tür bu kimlik bilgisine izinli değil | Create 503 |
| 403 | `RECORD_CODE_PERMISSION_MISSING` | kullanıcı token'ında türün oluşturma izni yok (§1 tablosu) | Create 403 (tüketicinin kendi kodu) |
| 403 | `RECORD_CODE_TENANT_NOT_ACTIVE` | kiracı Platform'da Active değil | Create yapılmaz |
| 404 | `LEGAL_ENTITY_NOT_FOUND` | şirket yok, silinmiş, **başka kiracının**, ya da aktif değil (tek gövde, sızıntısız) | Create 422/404 (tüketicinin kodu) |
| 409 | `LEGAL_ENTITY_SHORT_CODE_MISSING` | şirketin MDM `code`'u `^[A-Z]{2,5}$`'e birebir uymuyor | Create yapılmaz; "şirket kısa kodunu düzeltin" (tüketicinin metni) |
| 409 | `GROUP_PREFIX_MISSING` | `Group` istendi, kiracının grup öneki ayarı yok | Create yapılmaz |
| 409 | `GROUP_PREFIX_MISMATCH` | kiracının grup öneki ayarı belge `OrgPrefix`'inden farklı (iki kaynak ayrışmış) | Create yapılmaz; Platform yöneticisine bildirim |
| 413 | `RECORD_CODE_REQUEST_TOO_LARGE` | gövde 4 KB üstü | hata |
| 422 | `RECORD_TYPE_UNKNOWN` | tür katalogda yok (KALSIN, İKİSİ DE, `CTR`, henüz kaydı olmayan türler) | hata |
| 422 | `RECORD_CODE_SCOPE_INVALID` | tür bu kapsama izin vermiyor; `Group` + dolu `legalEntityId`; `LegalEntity` + boş `legalEntityId`; **`Platform` (DL-4'e kadar her zaman)** | hata |
| 422 | `LEGAL_ENTITY_NOT_ELIGIBLE` | MDM yanıtı `approvalStatus` taşıyor ve `APPROVED` değil | Create yapılmaz |
| 429 | `RECORD_CODE_RATE_LIMITED` | kimlik bilgisi başına hız sınırı aşıldı | geri çekilip **aynı anahtarla** yeniden dener |
| 503 | `LEGAL_ENTITY_SOURCE_UNAVAILABLE` | MDM erişilemez, 5xx, 3 s aşımı, 400/401/403, ya da çelişkili/ayrıştırılamaz yanıt | **Create yapılmaz** (fail-closed); aynı anahtarla yeniden denenebilir |
| 503 | `RECORD_CODE_UNAVAILABLE` | Platform deposu gerçekten erişilemez, işlem yeniden denemeleri tükendi ya da istek bütçesi (5 s) doldu | **Create yapılmaz**; aynı anahtarla yeniden denenebilir |

Tüketici kuralı (D59/1): **200 dışı her cevap, zaman aşımı ya da port bağlı değil → Create başarısız**, kodsuz kayıt
açılmaz, yedek kod ya da kendi sayacı üretilmez (RCS-001 §6).

## 5. Değerlendirme sırası

1. 429 hız sınırı → 2. 413 boyut → 3. 401 / 403 kimlik bilgisi, kullanıcı token'ı, `X-Tenant-Id` → 4. 403 kiracı aktif değil →
5. 400 gövde → 6. 422 `RECORD_TYPE_UNKNOWN` → 7. 403 tür kimlik bilgisine izinsiz → 8. 403 kullanıcı izni yok →
9. idempotency: kayıt varsa parmak izi eşit → **200 tekrar**, değil → 400 `IDEMPOTENCY_KEY_REUSED` →
10. 422 `RECORD_CODE_SCOPE_INVALID` → 11. önek: `Group` → 409 `GROUP_PREFIX_MISSING` / `GROUP_PREFIX_MISMATCH`;
`LegalEntity` → MDM (404 / 422 `LEGAL_ENTITY_NOT_ELIGIBLE` / 409 / 503) → 12. sayaç (503) → 13. defter + denetim (+ grup
öneki kilidi) tek işlemde (503) → 200.

Yetki (7–8) idempotency'den (9) önce gelir: izinsiz kullanıcı tekrarla kod okuyamaz. MDM, kapsam, tür ya da yetki
hatasında sayaç ilerlemez. Sayaç ilerledikten sonra (12–13) düşen istekte numara bir boşluk olarak kalır (§5.8); kod hiçbir
zaman iki kez verilmez.

## 6. Kapsamlar ve tür kataloğu

| `prefixScope` | `legalEntityId` | Önek | Sayaç anahtarı | 1.0.0'da |
|---|---|---|---|---|
| `LegalEntity` (varsayılan) | zorunlu | MDM MOD-0220 `LegalEntity.Code` (lookup-validation; kiracı + aktif; onaylı — §7) | (kiracı, kısa kod, tür) | etkin |
| `Group` | boş | kiracının grup öneki ayarı (bugün `GMG`; belge `OrgPrefix` ile eşit olmalı) | (kiracı, grup öneki, tür) | etkin |
| `Platform` | boş | yok (öneksiz) | DL-4'te belirlenir | **tanımlı, DL-4'te açılır**; o güne kadar 422 |

| `recordType` | Kayıt türü | İzinli kapsam | İzinli çağıran |
|---|---|---|---|
| `PAY` | Ödeme Partisi (MOD-0126) | LegalEntity | FinanceTreasury |
| `APD` | AP Belgesi (MOD-0119) | LegalEntity | FinanceTreasury |
| `ARI` | AR Alacak Faturası (MOD-0120) | LegalEntity | FinanceTreasury |
| `CPS` | Nakit Pozisyonu Anlık Görüntüsü (MOD-0124) | LegalEntity | FinanceTreasury |
| `CTP` | Nakit Eşik Politikası (MOD-0124) | LegalEntity | FinanceTreasury |
| `BNK` | Banka (MOD-0289) | **yalnız Group** | Mdm |
| `BAC` | Banka Hesabı (MOD-0289) | LegalEntity (şirketin kendi hesabı; `legalEntityId` = sahip şirket) **ve** Group (tedarikçi hesabı) — **seçim çağıranda** (MDM bağdaştırıcısı) | Mdm |
| `AGR` | Sözleşme (MOD-0217) | LegalEntity (yöneten şirket) | Mdm |
| `ORG`, `POS` | Organizasyon Birimi, Pozisyon (MOD-0288) | LegalEntity (öneri) | yok (paket DL-2) → 403 |
| `PLN` | Abonelik Planı | Platform | yok (paket DL-4) → 403 |

`CTR` kullanılmaz: Procurement MOD-0144 bugün `CTR-` + 8 hex üretiyor (`CreateContractHandler.cs:130-131`); aynı önek iki
türde olamaz (RCS-001 §5.5) → MOD-0217 `AGR` (K-7). `AGR` harfleri doküman kodunda da geçer (`GMG-QMS-AGR-0001`) ama
biçimler farklıdır (dört parçalı doküman kodu, üç parçalı kayıt kodu); çakışma yoktur (K-12 a). RCS-001 §3'teki `CTR`
satırının `AGR`'ye çevrilmesi Ali (`.antigravity`) + Treasury PR'ıyla ayrı adımdır.

## 7. Biçim, uygunluk ve sayaç kuralları

- `LegalEntity` / `Group`: `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$` (ör. `MYG-PAY-000042`, `GMG-BNK-000001`).
- `Platform` (DL-4'ten itibaren): `^[A-Z]{3}-[0-9]{6,}$` (ör. `PLN-000001`).
- Sayaç en az 6 hane sıfır dolgulu; `999999`'dan sonra `1000000`; sıfıra dönmez, sıfırlanmaz (§1, §5.7). Tüketici sabit
  uzunluk varsaymaz.
- Yalnız ASCII `A–Z`, `0–9`, `-` (§5.10). Önek, MDM değeri olduğu gibi kalıba uyuyorsa kullanılır; normalleştirme yok.
- **Tüzel kişilik uygunluğu:** kod yalnız **aktif VE onaylı** şirkete verilir. Aktiflik her zaman denetlenir. Onay:
  MDM yanıtında `approvalStatus` varsa `APPROVED` olmalıdır; alan yoksa (bugünkü lookup-validation 1.0.0) Platform onayı
  denetleyemez ve **tüketicinin ön kontrolü zorunludur** (lookup-validation v1.1 main'e girene kadar).
- Önek koda **bir kez** yazılır (§5.2). Grup öneki kullanılana kadar düzeltilebilir, kullanıldıktan sonra kilitlidir (§5.4 dili).
- Ardışıklık garanti değildir; tekillik garantidir: kod kiracıda tekildir.
- **Şirket öneki biçimi** RCS-001 §1'e göredir; K1'in 2026-11-02'ye kadar vereceği karar bu bölümü değiştirebilir — gelmezse
  bu biçimle dondurulur (§12 N-7).

## 8. Idempotency ve eşzamanlılık

- Anahtar kapsamı: (kiracı, çağıran servis, `idempotencyKey`). Başka kiracı ya da servis aynı anahtarı bağımsız kullanır.
- Parmak izi: `recordType`, `prefixScope` (varsayılan uygulanmış), `legalEntityId`.
- Ömür sınırsızdır. Tekrar MDM'e gitmez (MDM kapalıyken de 200 döner).
- **Eşzamanlı aynı anahtar:** N eşzamanlı istek tek kod üretir ve hepsi o kodu alır; geçici yazım çakışmaları
  (`TransientTransactionError` / `WriteConflict`) sunucuda yeniden denenir ya da kazanan okunur; 503 yalnız gerçek
  erişilemezlikte. Sayaçta **en çok N−1** numara boşluk olarak kalabilir.
- Çağıranın yeniden deneme kuralı: zaman aşımı, 429, 503 ya da bağlantı hatasından sonra **aynı anahtarla** yeniden dener;
  yeni anahtar yeni kod demektir. Diğer 4xx yeniden denenmez.

## 9. v0'dan farklar

| Konu | v0 (D59/1) | v1.0.0 | Tür |
|---|---|---|---|
| Gövde | `{legalEntityId, recordType, idempotencyKey}` | aynı + isteğe bağlı `prefixScope` (yoksa `LegalEntity`); `Group`'ta `legalEntityId` boş | ekleme |
| 200 | `{code, sequence}`, zarf tanımsız | aynı alanlar `Response<T>` zarfının `data`'sında | **v0'da bağlayıcı değildi; zarfla dondurulur — Treasury CT teyidi (§12 N-2)** |
| 422 | `RECORD_TYPE_UNKNOWN` | aynı + `RECORD_CODE_SCOPE_INVALID`, `LEGAL_ENTITY_NOT_ELIGIBLE` | ekleme |
| 409 | `LEGAL_ENTITY_SHORT_CODE_MISSING` | aynı + `GROUP_PREFIX_MISSING`, `GROUP_PREFIX_MISMATCH` | ekleme |
| 404 | başka kiracı | aynı; `reason_code` `LEGAL_ENTITY_NOT_FOUND` (yok / aktif değil de aynı gövde) | ekleme |
| 503 | servis yok → Create başarısız | aynı; neden kodları `LEGAL_ENTITY_SOURCE_UNAVAILABLE`, `RECORD_CODE_UNAVAILABLE` | ekleme |
| 400 / 401 / 403 / 413 / 429 | — | §4'teki kodlar | ekleme |
| Kimlik | "kiracı güvenilir bağlamdan" | kimlik bilgisi başlıkları + iletilen kullanıcı token'ı; kullanıcı izni; kiracı Active (§1) | ekleme (v0'da tanımsızdı) |

Tüketici port imzası (Treasury `IRecordCodeAllocator.AllocateAsync(legalEntityId, recordType, idempotencyKey)`):
`LegalEntity` türleri için değişmeden çalışır; `BNK` ve `BAC` için `prefixScope` taşıması gerekir (§12 N-4).

## 10. Örnekler

Şirket kodu (`LegalEntity`, MDM `code = MYG`, onaylı):

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

Grup öneki (banka; tedarikçi banka hesabı da aynı biçimde `BAC` ile):

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

{ "recordType": "PLN", "prefixScope": "Platform", "legalEntityId": null, "idempotencyKey": "k-2" }
403 { …, "reason_code": "RECORD_CODE_FORBIDDEN" }        (PLN hiçbir kimlik bilgisine izinli değil)

{ "recordType": "PAY", "prefixScope": "Platform", "legalEntityId": null, "idempotencyKey": "k-3" }
422 { …, "reason_code": "RECORD_CODE_SCOPE_INVALID" }    (Platform DL-4'e kadar kapalı)

{ "recordType": "PAY", "legalEntityId": "<MDM code = GMG-CH olan şirket>", "idempotencyKey": "k-4" }
409 { …, "reason_code": "LEGAL_ENTITY_SHORT_CODE_MISSING" }

{ "recordType": "PAY", "legalEntityId": "<başka kiracının şirketi>", "idempotencyKey": "k-5" }
404 { …, "reason_code": "LEGAL_ENTITY_NOT_FOUND" }
```

## 11. Denetim (F-1 ile yeniden yazıldı; K-8r, K-9, K-10)

- **Başarılı ayırma** Platform merkezi günlüğüne (`audit_events`) **ayırmayla aynı işlemde** yazılır (kayıt yoksa kod yok):
  - **aktör** = isteği yapan kullanıcı: `ActorType = TenantUser`, `ActorId` = iletilen token'ın `sub`'ı (Platform'un işlem
    içi denetim kapısı aktörü doğrulanmış principal'dan çıkarır);
  - **kaynak servis** `SourceService` = §1 kapalı listesindeki servis adı (ör. `Diten.FinanceTreasuryService`) — yalnız
    doğrulanmış kimlik bilgisinden; gövde ya da başlıktan asla;
  - `TenantId` = token kiracısı; varlık `RecordCodeAllocation`; işlem `Create`; sonuç `Succeeded`; metadata `recordType`,
    `prefixScope`, `legalEntityId`, `prefix`, `code`, `sequence`, `idempotencyKey`.
- **Ret kaydı:** ret bir iş işlemi olmadığından kayıt **ayrı kısa bir işlemle** yazılır ve hiçbir zaman kod üretmez:

| Cevap | Kayıt | Outcome |
|---|---|---|
| 429, 413, 401, kiracı çözülmeden verilen 403 (yapılandırma, hedef kitle, `X-Tenant-Id`, eksik token iddiası), 400 `RECORD_CODE_REQUEST_INVALID` | yazılmaz (yalnız Platform logu) | — |
| 403 `RECORD_CODE_TENANT_NOT_ACTIVE`, 403 `RECORD_CODE_FORBIDDEN` (tür izinsiz), 403 `RECORD_CODE_PERMISSION_MISSING`, 404, 422 `RECORD_CODE_SCOPE_INVALID` | yazılır | `Denied` |
| 400 `IDEMPOTENCY_KEY_REUSED`, 409 (üç kod), 422 `RECORD_TYPE_UNKNOWN`, 422 `LEGAL_ENTITY_NOT_ELIGIBLE`, 503 | yazılır (503'te en iyi çaba) | `Failed` |

  Gerekçe: kimliksiz bir istek ne kiracı ne kişi adlandırabilir; onu yazmak herkesin bir kiracının günlüğünü doldurmasına
  izin verir. Kimliği doğrulanmış ve kiracısı bilinen her ret iz bırakır.
- Tekrar (aynı anahtar) yeni kayıt yazmaz.
- Bu kayıtlar BL-501 ortak ileticisiyle değil Platform içinden yazılır. Tüketici kendi Create kaydını kendi yoluyla yazar
  ve kendi kaydına `idempotencyKey` ile `code`'u koyar; iki kayıt bunlarla eşleşir.

## 12. Açık noktalar (dondurmadan önce kapanır)

| # | Nokta | Öneri | Sahip | Son tarih |
|---|---|---|---|---|
| N-1 | İç uç kimlik deseni | **KAPANDI — K-12 (e)**: P2 + azaltıcılar; arka plan çağıranı paket DL-9 | Beste | 2026-10-30 |
| N-2 | `Response<T>` zarfı — v0 "200 {code, sequence}" yazıyordu, zarfı tanımlamıyordu | zarf (Platform uçlarının hepsi gibi); `data` içeriği v0 ile aynı | **Treasury CT** (teyit) | 2026-11-02 |
| N-3 | `idempotencyKey` biçimi (1–200 yazdırılabilir ASCII) | taslaktaki gibi | Beste + Treasury CT | 2026-11-03 |
| N-4 | Tüketici portuna `prefixScope` (BNK, BAC); MDM bağdaştırıcısının `BAC` eşlemesi (şirket hesabı → LegalEntity, tedarikçi hesabı → Group) | port imzasına isteğe bağlı parametre; tüketici AC'si | Treasury CT + MDM sahibi | 2026-11-03 |
| N-5 | `Platform` kapsamı | **KAPANDI — K-11**: tanımlı, DL-4'te açılır, o güne kadar 422 | Beste | 2026-10-30 |
| N-6 | `PLN` katalogda mı | **KAPANDI — K-12 (d)**: katalogda, bağlanması DL-4; HTTP'den 403 | Beste | 2026-10-30 |
| N-7 | Şirket öneki biçimi (K-6) | karar yoksa RCS-001 §1 | K1 (`gmg-10`) | 2026-11-02 |
| N-8 | `CTR` → `AGR` RCS-001 §3 satırı; MOD-0144 / MOD-0217 kapsam çakışması | `AGR` sözleşmede kalır (K-12 a) | Ali (`.antigravity`) + Treasury PR'ı; Cihan + Treasury | 2026-11-03 (§3); TBD (çakışma) |
| N-9 | Hız sınırı değeri ve eski sırrın geçerlilik penceresi | 600 istek/dakika/kimlik bilgisi; pencere en çok 24 saat | Beste | 2026-11-03 |
| N-10 | §1 kullanıcı izni adlarının teyidi (MOD-0018-FU27 taslağı; `APD` alacak dekontu dahil) | tablodaki gibi | Beste + Treasury CT + MDM sahibi | 2026-11-03 |

## 13. Sürüm kuralı

- 1.0.0 istek gövdesi ve hata kodlarında v0'a göre yalnız ekleme; v0 gövdesiyle yazılmış tüketici değişmeden çalışır. Yanıt
  zarfı v0'da bağlayıcı değildi ve 1.0.0'da ilk kez dondurulur (§9).
- Bundan sonraki her değişiklik yalnız ekleme olur (yeni tür, yeni isteğe bağlı alan, yeni neden kodu, `Platform`
  kapsamının DL-4'te açılması). Bir alanın kaldırılması, anlamının değişmesi, kod kalıbının (§7) değişmesi ya da var olan
  bir durum kodunun anlamının değişmesi **büyük sürüm** sayılır.
- Yeni tür eklemek sözleşme değişikliği değildir; katalog RCS-001 §3'e bağlıdır.

## 14. Değişiklik geçmişi

- 1.0.0 rev 2 (2026-10-30) — bağımsız inceleme 01 (UYGUN DEĞİL) ve sahip kararları K-8r, K-9…K-12: denetim aktörü kullanıcı,
  kaynak servis kapalı listeden (§1, §11); ret kaydı tablosu (§11); `Platform` tanımlı ama DL-4'e kadar 422 (§2, §6);
  401/403 eşlemesi MOD-0018-FU21 ile birebir, iptal edilmiş kimlik bilgisi 401 (§1, §4); kullanıcı izni, kiracı Active,
  hız sınırı, `LEGAL_ENTITY_NOT_ELIGIBLE`, `GROUP_PREFIX_MISMATCH` (§1, §4); eşzamanlı aynı anahtar ve N−1 boşluk sınırı
  (§8); yanıt zarfının v0'da bağlayıcı olmadığı ve "yalnız ekleme"nin kapsamı düzeltildi (başlık, §9, §13).
- 1.0.0 rev 1 (2026-10-30) — ilk yazılı sürüm (taslak). Kaynak: sahip kararları K-3…K-8 (Beste 2026-10-30), v0 metni (D59/1, Y-8).
