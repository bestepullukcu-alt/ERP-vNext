# RECORD-CODE-ALLOCATE 1.0.0 — ortak kayıt kodu ayırma sözleşmesi (TASLAK / öneri)

    contract       : RECORD-CODE-ALLOCATE
    version        : 1.0.0 (önceki: v0 — yazılı sözleşme belgesi yoktu; KARAR D59/1 metni, Beste Y-8 2026-10-28)
    x-status       : DRAFT (rev 9 — paket r10, son bakış 01 tur 4 (özet 0e25482b) §12 ayrımı; rev 8 — paket r9, inceleme 03 tur 6 (özet f9c26991) ve son bakış 01 tur 3 (özet ec0346bb) sözleşme payı; rev 7 — paket r8, son bakış 01 tur 2 (özet 8a63c4e5) ve inceleme 03 tur 5 (özet e6dd4cd8) sözleşme payı; rev 6 — paket r7, bağımsız son bakış 01 (özet a52313ba) ve inceleme 03 tur 4 sözleşme payı işlendi; rev 5 — paket r5, inceleme 03 tur 2 F-3 işlendi; rev 4 — paket r4, bağımsız inceleme 03 F-2 / F-3 / F-9 sözleşme payı işlendi; rev 3 — bağımsız inceleme 02 KOŞULLU bulguları işlendi; rev 2 — inceleme 01 ve sahip kararları K-8r, K-9…K-12) —
                     öneri; MOD-0040-FU02 paketi CT-APR-02 / CT-APR-01'den geçmeden ve **dondurmayı bekleten** açık noktalar (§12, "Bekletir")
                     kapanmadan dondurulmaz; bekletmeyen noktalar dondurmadan sonra da açık kalabilir (rev 9); dondurma ayrı CT adımıdır
                     (hedef 2026-11-03), dondurulmuş dosyanın yeri o adımda belirlenir
    x-owner        : MOD-0040-FU02 Record Code Service (platform-shared-services, Diten.Platform 5057) — sahip Beste (K2)
    x-module-pack  : execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md
    x-consumers    : Diten.FinanceTreasuryService (MOD-0126 PAY, MOD-0119 APD, MOD-0120 ARI, MOD-0124 CPS / CTP);
                     Diten.MdmService (MOD-0289 BNK / BAC, MOD-0217 AGR); sonra Diten.InventoryService (MOD-0178, paket DL-8)
    x-decision     : sahip kararları K-3, K-4, K-5, K-7, K-8r, K-9, K-10, K-11, K-12 (Beste 2026-10-30; paket Bölüm 0.1);
                     KARAR D59/1 (docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:605-610); KARAR D91
    x-rule         : RCS-001 (.antigravity/rules/record-coding-standard.md) §1, §3, §5.1–§5.12
    x-measured-at  : 81864bd98 (rev 6 ölçümleri 703cbaec7'de; atıf yapılan servis, Gateway, kural ve defter dosyaları iki commit
                     arasında değişmedi, diff --stat ile ölçüldü) (2026-11-02; ilk ölçüm 6d49f5823, 2026-10-30 — atıf yapılan servis, Gateway ve kural dosyaları
                     iki commit arasında ve 8e954eb2b'ye kadar değişmedi, `git diff --stat`)

**Uyumluluk kuralı.** v0'a göre **istek gövdesi ve hata kodlarında değişiklik yalnız eklemedir** (D59/1, K-3): v0 gövdesi
(`legalEntityId`, `recordType`, `idempotencyKey`) v1'de aynı anlamı taşır (`prefixScope` yoksa `LegalEntity`), v0'ın dört
hata kodu aynen kalır. **Yanıt biçimi — ÖNERİ, karar değil (inceleme 02 F-10):** D59/1 ve Y-8 değişikliğin "yalnız ekleme"
olduğunu yanıt dahil söyler; v0 metni "200 `{code, sequence}`" yazıyordu ve zarfı tanımlamıyordu. Bu taslağın **önerisi**,
v0 yanıt biçiminin bağlayıcı olmadığını kabul edip 1.0.0 yanıtını Platform `Response<T>` zarfının `data` alanında
dondurmaktır (`data` içeriği v0 ile aynı). Öneri **Treasury CT teyidi** gelmeden karar sayılmaz. **Varsayılan (rev 7, son
bakış 01 tur 2 F-7 — §12 N-2 ile aynı):** teyit sözleşme dondurma adımına (2026-11-03) kadar gelmezse 1.0.0 zarfla donar;
Treasury sonradan en üst düzey biçimi isterse bu büyük sürümdür (§13). Tablo §9'da.

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
| `Content-Type` | `application/json` | başka / yok → 400 `RECORD_CODE_REQUEST_INVALID` (415 kullanılmaz — rev 6) |

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
  taşımalıdır (yoksa MDM 403 → bu uçta **403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`**, kalıcı — rev 6).
- Hız sınırı: kimlik bilgisi ve token doğrulandıktan sonra (tüketici servis, kiracı) bölümlü sabit pencere; aşımda 429
  `RECORD_CODE_RATE_LIMITED` (bu uçun kendi kodu; Platform'un genel `RATE_LIMITED`'i değil); değer §12 N-9.
- Varsayılan / tohum kiracı `00000000-0000-0000-0000-000000000001` normal bir iş kiracısıdır ve kod alır.
- Kimlik bilgisi değerleri bu belgede ve depoda **yoktur**; ortam gizli bilgisinden sağlanır.

## 2. İstek gövdesi

JSON nesnesi, ≤ 4 KB (aşarsa 413). **Bilinmeyen üye 400** (ör. `tenantId`, `sourceService`, `code`); tekrarlı üye 400.

| Alan (JSON) | Tip | Zorunlu | Kural |
|---|---|---|---|
| `recordType` | string | evet | `^[A-Z]{3}$`, büyük/küçük harfe duyarlı, normalleştirme yok. Biçimsiz → 400; katalog dışı → 422 `RECORD_TYPE_UNKNOWN` |
| `prefixScope` | string | hayır | `LegalEntity` \| `Group` \| `Platform` (tam yazım); yoksa `LegalEntity`; başka değer → 400. **`Platform` 1.0.0'da tanımlıdır ama DL-4'e kadar, tür izni adımını geçmiş her istekte 422 `RECORD_CODE_SCOPE_INVALID` alır** (K-11); izinli çağıranı olmayan tür (`PLN`) önce 403 alır (§5) |
| `legalEntityId` | string (UUID) \| null | kapsama bağlı | `LegalEntity`: zorunlu, boş olmayan UUID. `Group` / `Platform`: yok ya da `null`. UUID değil → 400; kapsamla uyumsuz → 422 `RECORD_CODE_SCOPE_INVALID` |
| `idempotencyKey` | string | evet | 1–200 karakter, yazdırılabilir ASCII (`!`…`~`, boşluk yok) — §12 N-3. Çağıran, Create'inin kendi idempotency anahtarından türetir |

## 3. Cevap 200

Platform `Response<T>` zarfı (`services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs:7-16`) — **öneri**,
Treasury CT teyidi bekleniyor (§12 N-2):

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

Hata gövdesi aynı zarftır — **iki adlı istisna** `AUDIT_RECORD_UNAVAILABLE` ve `AUDIT_INTENT_INVALID` (`ProblemDetails`, `reason_code` uzantıda; rev 7): `data = null`, `isSuccessful = false`, `reason_code` aşağıdaki kod, `errors[]` İngilizce
tanılama metni (**tüketici ekranında gösterilmez**; tüketici `reason_code`'u kendi dil dosyasıyla çevirir).

| HTTP | `reason_code` | Ne zaman | Tüketici davranışı |
|---|---|---|---|
| 400 | `RECORD_CODE_REQUEST_INVALID` | gövde ayrıştırılamıyor, `Content-Type` `application/json` değil, bilinmeyen/tekrarlı üye, alan biçimi yanlış, tanımsız `prefixScope` (cevap her zaman bu zarftadır, `ProblemDetails` değil) | hata; Create yapılmaz |
| 400 | `IDEMPOTENCY_KEY_REUSED` | aynı anahtar daha önce **farklı** gövdeyle kullanılmış | hata; Create yapılmaz |
| 401 | `RECORD_CODE_UNAUTHENTICATED` | kimlik bilgisi başlıkları eksik/çift; kimlik bilgisi bilinmiyor, sırrı yanlış ya da **iptal edilmiş**; kullanıcı token'ı yok / geçersiz / süresi dolmuş | Create 503 |
| 403 | `RECORD_CODE_FORBIDDEN` | kimlik bilgisi yapılandırması geçersiz; hedef kitle ya da tüketici uyuşmuyor; `X-Tenant-Id` var; token iddiaları eksik; tür bu kimlik bilgisine izinli değil | Create 503 |
| 403 | `RECORD_CODE_PERMISSION_MISSING` | kullanıcı token'ında türün oluşturma izni yok (§1 tablosu) | Create 403 (tüketicinin kendi kodu) |
| 403 | `RECORD_CODE_TENANT_NOT_ACTIVE` | kiracı Platform'da Active değil | Create yapılmaz |
| 404 | `LEGAL_ENTITY_NOT_FOUND` | şirket yok, silinmiş, **başka kiracının**, ya da aktif değil (tek gövde, sızıntısız) | Create 422/404 (tüketicinin kodu) |
| 409 | `LEGAL_ENTITY_SHORT_CODE_MISSING` | şirketin MDM `code`'u `^[A-Z]{2,5}$`'e birebir uymuyor | Create yapılmaz; "şirket kısa kodunu düzeltin" (tüketicinin metni) |
| 409 | `GROUP_PREFIX_MISSING` | `Group` istendi, kiracının grup öneki ayarı yok | Create yapılmaz |
| 409 | `GROUP_PREFIX_MISMATCH` | kiracının grup öneki ayarı — **kilitli ya da değil** — etkin belge `OrgPrefix`'inden farklı (belge önekinin yapılandırması hizalamadan sonra değişmiş; çok örnekli dağıtımda yeniden başlatma bitene kadar geçici de olabilir) — rev 6 | Create yapılmaz; Platform yöneticisine bildirim (kurtarma: `OrgPrefix` kilitli değere geri alınır) |
| 409 | `LEGAL_ENTITY_SHORT_CODE_CHANGED` | şirketin MDM kısa kodu, kodu verilmiş bir önekten sonra değişmiş ya da bu kısa kod daha önce **başka** bir şirketin kodlarında kullanılmış (RCS-001 §5.4 ara dönem kilidi) | Create yapılmaz; "şirket kısa kodu değişmiş, MDM yöneticisine başvurun" (tüketicinin metni) |
| 413 | `RECORD_CODE_REQUEST_TOO_LARGE` | gövde 4 KB üstü | hata |
| 422 | `RECORD_TYPE_UNKNOWN` | tür katalogda yok (KALSIN, İKİSİ DE, `CTR`, henüz kaydı olmayan türler) | hata |
| 422 | `RECORD_CODE_SCOPE_INVALID` | tür bu kapsama izin vermiyor; `Group` + dolu `legalEntityId`; `LegalEntity` + boş `legalEntityId`; **`Platform` (DL-4'e kadar, tür izni geçtiyse)** | hata |
| 422 | `LEGAL_ENTITY_NOT_ELIGIBLE` | MDM yanıtı `approvalStatus` taşıyor ve `APPROVED` değil (lookup-validation v1.1'den itibaren; bugün 1.0.0'da alan yok — §7) | Create yapılmaz |
| 429 | `RECORD_CODE_RATE_LIMITED` | (tüketici servis, kiracı) başına hız sınırı aşıldı | geri çekilip **aynı anahtarla** yeniden dener |
| 403 | `LEGAL_ENTITY_READ_PERMISSION_MISSING` | MDM 403: kullanıcı token'ında `mdm.legal-entities.read` yok (rev 6, ekleme) | Create 403; **yeniden denenmez** (kalıcı) |
| 503 | `LEGAL_ENTITY_SOURCE_UNAVAILABLE` | MDM erişilemez, 5xx, 3 s aşımı, 400/401, ya da çelişkili/ayrıştırılamaz yanıt | **Create yapılmaz** (fail-closed); aynı anahtarla, sınırlı sayıda ve geri çekilerek yeniden denenebilir (§8) |
| 503 | `RECORD_CODE_UNAVAILABLE` | Platform deposu gerçekten erişilemez, işlem yeniden denemeleri tükendi ya da istek bütçesi (5 s) doldu | **Create yapılmaz**; aynı anahtarla yeniden denenebilir |
| 503 | `AUDIT_RECORD_UNAVAILABLE` | **`ProblemDetails` gövdesi, zarf değil** (Platform ortak kuralı): başarılı ayırmanın denetim kaydı yazılamadı (aktör adlandırılamadı); ayırma yapılmadı (rev 7) | **Create yapılmaz**; aynı anahtarla, sınırlı olarak yeniden denenebilir; `reason_code` `ProblemDetails` uzantısındadır |
| 500 | `AUDIT_INTENT_INVALID` | **`ProblemDetails` gövdesi, zarf değil**: Platform'da programlama hatası (geçersiz denetim niyeti); ayırma yapılmadı (rev 7) | **Create yapılmaz**; yeniden denenmez |

Tüketici kuralı (D59/1): **200 dışı her cevap, zaman aşımı ya da port bağlı değil → Create başarısız**, kodsuz kayıt
açılmaz, yedek kod ya da kendi sayacı üretilmez (RCS-001 §6).

## 5. Değerlendirme sırası

1. 413 boyut → 2. 401 / 403 kimlik bilgisi, kullanıcı token'ı, `X-Tenant-Id` → 3. 429 hız sınırı → 4. 403 kiracı aktif değil →
5. 400 gövde → 6. 422 `RECORD_TYPE_UNKNOWN` → 7. 403 tür kimlik bilgisine izinsiz → 8. 403 kullanıcı izni yok →
9. idempotency: kayıt varsa üç alan ayrı ayrı eşit (§8) → **200 tekrar**, değil → 400 `IDEMPOTENCY_KEY_REUSED` →
10. 422 `RECORD_CODE_SCOPE_INVALID` → 11. önek: `Group` → 409 `GROUP_PREFIX_MISSING` / `GROUP_PREFIX_MISMATCH`;
`LegalEntity` → MDM (404 / 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` / 422 `LEGAL_ENTITY_NOT_ELIGIBLE` / 409 / 503), ardından kısa kod kullanım denetimi (409
`LEGAL_ENTITY_SHORT_CODE_CHANGED`) → 12. sayaç (503) → 13. defter + denetim (+ grup
öneki kilidi) tek işlemde: kilit eşleşmezse 409 `GROUP_PREFIX_MISMATCH` (grup öneki ayarı bu istekten sonra başka bir
önekle yeniden hizalandı), işlem / bütçe tükenirse 503 → 200 (rev 8).

Yetki (7–8) idempotency'den (9) önce gelir: izinsiz kullanıcı tekrarla kod okuyamaz. Tür izni (7) kapsamdan (10) önce
gelir: `PLN` + `Platform` → 403, `PAY` + `Platform` → 422. MDM, kapsam, tür ya da yetki
hatasında sayaç ilerlemez. Sayaç ilerledikten sonra (12–13) düşen istekte numara bir boşluk olarak kalır (§5.8); kod hiçbir
zaman iki kez verilmez.

## 6. Kapsamlar ve tür kataloğu

| `prefixScope` | `legalEntityId` | Önek | Sayaç anahtarı | 1.0.0'da |
|---|---|---|---|---|
| `LegalEntity` (varsayılan) | zorunlu | MDM MOD-0220 `LegalEntity.Code` (lookup-validation; kiracı + aktif; onaylı — §7) | (kiracı, kısa kod, tür) | etkin |
| `Group` | boş | kiracının grup öneki ayarı — değerin **tek kaynağı belge `OrgPrefix`'i** (bugün `GMG`); ayar Platform yöneticisinin hizalamasıyla alınır, ilk kullanımda kilitlenir | (kiracı, grup öneki, tür) | etkin |
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
| `ORG`, `POS` | Organizasyon Birimi, Pozisyon (MOD-0288) | 1.0.0'da boş küme (karar A-8, paket DL-2; öneri LegalEntity) — rev 6 | yok (paket DL-2) → 403 |
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
- **Tüzel kişilik uygunluğu (inceleme 02 F-8):** hedef kural kodun yalnız **aktif VE onaylı** şirkete verilmesidir. MDM
  main'deki lookup-validation **1.0.0**'da `approvalStatus` alanı yoktur; v1.1 taslaktır. **v1.1 yayınlanana kadar** servis
  yalnız **aktifliği** denetler ve onay denetimi **tüketicinin zorunlu ön kontrolüdür**; yanıtta alan görünürse
  `APPROVED` dışı 422, tanımsız değer 503. **v1.1 yayınlandığında** alan yoksa yanıt geçersizdir → 503
  (`legal-entity-lookup-validation-v1.1-proposal.md:69`); bu Platform'da bir kod değişikliğidir ve MOD-0220 sahibinin v1.1
  yayınına açık bağımlılıktır (§12 N-11).
- Önek koda **bir kez** yazılır (§5.2). Grup öneki: değer belge `OrgPrefix`'inden hizalanır; hiç kullanılmadıysa
  `OrgPrefix` değiştirilip yeniden hizalanabilir, kullanıldıktan sonra kilitlidir (§5.4 dili).
- **Kısa kod kullanım kilidi — ara dönem (RCS-001 §5.4):** MDM kısa kodu bugün değiştirilebilir; bir şirketin kodları
  verildikten sonra kısa kodu değişirse ya da serbest kalan kısa kod başka bir şirkete verilirse yeni ayırma 409
  `LEGAL_ENTITY_SHORT_CODE_CHANGED` alır (kod verilmez). MDM tarafındaki kalıcı kilit §12 N-12.
- Ardışıklık garanti değildir; tekillik garantidir: kod kiracıda tekildir.
- **Şirket öneki biçimi** RCS-001 §1'e göredir; K1'in 2026-11-02'ye kadar vereceği karar bu bölümü değiştirebilir — gelmezse
  bu biçimle dondurulur (§12 N-7).

## 8. Idempotency ve eşzamanlılık

- Anahtar kapsamı: (kiracı, çağıran servis, `idempotencyKey`). Başka kiracı ya da servis aynı anahtarı bağımsız kullanır.
- "Aynı gövde" = üç alanın **ayrı ayrı** eşitliği (birleştirilmiş metin ya da özet yok): `recordType` (birebir),
  `prefixScope` (**varsayılan uygulandıktan sonra** — gönderilmeyen `prefixScope` ile `"LegalEntity"` aynıdır),
  `legalEntityId` (UUID değeri olarak; büyük/küçük harf ya da yazım biçimi farkı aynı değerdir). Örnek: önce v0 gövdesi
  (`prefixScope` yok), sonra aynı anahtarla `"prefixScope": "LegalEntity"` → 200 ve aynı `code`; aynı anahtarla
  `"prefixScope": "Group"` → 400 `IDEMPOTENCY_KEY_REUSED`.
- Ömür sınırsızdır. Tekrar MDM'e gitmez (MDM kapalıyken de 200 döner).
- **Eşzamanlı aynı anahtar:** N eşzamanlı istek tek kod üretir; **200 alan her istek o kodu alır**. Geçici yazım
  çakışmaları (`TransientTransactionError` / `WriteConflict`) sunucuda yeniden denenir ve yeniden denemede kazanan okunur.
  **Sınır (rev 5, inceleme 03 tur 2 F-3):** Platform yeniden denemeleri beklemeden ve sınırlı sayıda yapar; kazananın
  commit'i bu denemelerden uzun sürerse aynı anahtarlı bir istek **503 `RECORD_CODE_UNAVAILABLE`** alabilir. Bu bir hata
  değil, tanımlı sınırdır: istek kod almamıştır ve yeni kod da üretilmemiştir. Sayaçta **en çok N−1** numara boşluk
  olarak kalabilir.
- **Kural — 503'ten sonra aynı anahtarla yeniden dene:** tüketici 503'ü (bu sınır dahil) Create başarısızlığı sayar ve
  Create'i **aynı** `idempotencyKey` ile yeniden dener; kazanan commit olduysa cevap 200 ve **kazananın kodu**dur (yeni kod
  değil). Yeni anahtar kullanmak yeni ve ayrı bir kod demektir — 503'ten sonra yapılmamalıdır. Commit sonucu bilinmeyen
  503'te de kural aynıdır (§11).
- Çağıranın yeniden deneme kuralı: zaman aşımı, 429, 503 ya da bağlantı hatasından sonra **aynı anahtarla** yeniden dener;
  yeni anahtar yeni kod demektir. Diğer 4xx yeniden denenmez. Yeniden deneme **sınırlı ve geri çekilerek** yapılır (öneri en çok 3
  deneme, üstel bekleme; sonra Create başarısız) — sonsuz yeniden deneme yoktur (rev 6).

## 9. v0'dan farklar

| Konu | v0 (D59/1) | v1.0.0 | Tür |
|---|---|---|---|
| Gövde | `{legalEntityId, recordType, idempotencyKey}` | aynı + isteğe bağlı `prefixScope` (yoksa `LegalEntity`); `Group`'ta `legalEntityId` boş | ekleme |
| 200 | `{code, sequence}`, zarf tanımsız | aynı alanlar `Response<T>` zarfının `data`'sında | **öneri:** v0'da bağlayıcı değildi kabulüyle zarfla dondurmak — Treasury CT teyidi bekleniyor (§12 N-2) |
| 422 | `RECORD_TYPE_UNKNOWN` | aynı + `RECORD_CODE_SCOPE_INVALID`, `LEGAL_ENTITY_NOT_ELIGIBLE` | ekleme |
| 409 | `LEGAL_ENTITY_SHORT_CODE_MISSING` | aynı + `GROUP_PREFIX_MISSING`, `GROUP_PREFIX_MISMATCH`, `LEGAL_ENTITY_SHORT_CODE_CHANGED` | ekleme |
| 404 | başka kiracı | aynı; `reason_code` `LEGAL_ENTITY_NOT_FOUND` (yok / aktif değil de aynı gövde) | ekleme |
| 503 | servis yok → Create başarısız | aynı; neden kodları `LEGAL_ENTITY_SOURCE_UNAVAILABLE`, `RECORD_CODE_UNAVAILABLE` | ekleme |
| 400 / 401 / 403 / 413 / 429 | — | §4'teki kodlar (rev 6: 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` eklendi) | ekleme |
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
  - `TenantId` = `TargetTenantId` = token kiracısı; `RequestType` `AllocateRecordCodeCommand`; varlık `RecordCodeAllocation`
    (kimlik = ayırma defteri kaydı); işlem `Create`; kategori `PlatformConfiguration`; `SourceModule` `MOD-0040-FU02`;
    sonuç `Succeeded`;
  - **yükün tek tanımı paket Bölüm 21.3 Tablo A'dır (rev 4, inceleme 03 F-2); alanlar ve yerleri birebir:**
    `BeforeState` yok; `AfterState` = `Code`, `Sequence`, `RecordType`, `PrefixScope` (varsayılan uygulanmış), `Prefix`,
    `LegalEntityId` (`Group`'ta `null`); `Metadata` = `EventName` (`platform.record-code.allocated`), `IdempotencyKey`,
    `GroupPrefixLockedNow` (bu ayırma kiracının grup önekini kilitlediyse `true`); Platform'un denetim kapısı istemci
    korelasyon başlığı varsa `ClientCorrelation` ekler. Anahtar yazımı PascalCase'dir;
  - **denetim kutusu anahtarı istemci anahtarı değildir (inceleme 02 F-2):** başarılı ayırmada ayırma defteri kaydının
    kimliğinden türetilir (`record-code:allocated:{allocationId}`), ret kaydında her ret için yeni bir Guid'dir. Böylece iki
    kiracının aynı istemci anahtarı ya da önce reddedilip sonra başarılı olan aynı anahtar çakışmaz.
  - **Commit sonucu bilinmiyorsa (rev 4, inceleme 03 F-3):** Platform commit'i kendisi yeniler; yenilemeler tükenirse 503
    `RECORD_CODE_UNAVAILABLE` döner ve ayırma commit olmuş ya da olmamış olabilir. Tüketici **aynı** `idempotencyKey` ile
    yeniden dener: commit olmuşsa 200 ve aynı kod, olmamışsa yeni bir ayırma (ilk yazım) ve yeni kod. (rev 3'teki "kutu satırı
    zaten yazılmışsa 200" cümlesi kaldırıldı: Platform'un işlem yürütücüsüyle o durum oluşmaz.)
- **Ret kaydı:** ret bir iş işlemi olmadığından kayıt **ayrı kısa bir işlemle** yazılır ve hiçbir zaman kod üretmez:

| Cevap | Kayıt | Outcome |
|---|---|---|
| 413, 401, kiracı çözülmeden verilen 403 (yapılandırma, hedef kitle, `X-Tenant-Id`, eksik token iddiası) | yazılmaz (yalnız Platform logu) | — |
| 429 `RECORD_CODE_RATE_LIMITED` | **yazılmaz — AUD-001 §3'ten adlı sapma** (sahip kararı Beste 2026-11-02): uyarı logu + sayaç | — |
| 403 `RECORD_CODE_FORBIDDEN` — platform yöneticisi / ortak yönetici token'ı | yazılır (platform denetim kaydı, hedef kiracı token'dan) — rev 6 | `Denied` |
| 403 `RECORD_CODE_TENANT_NOT_ACTIVE`, 403 `RECORD_CODE_FORBIDDEN` (tür izinsiz), 403 `RECORD_CODE_PERMISSION_MISSING`, 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`, 404 | yazılır | `Denied` |
| 400 `RECORD_CODE_REQUEST_INVALID` (rev 6), 400 `IDEMPOTENCY_KEY_REUSED`, 409 (dört kod), 422 `RECORD_CODE_SCOPE_INVALID` (rev 6: `Failed`), 422 `RECORD_TYPE_UNKNOWN`, 422 `LEGAL_ENTITY_NOT_ELIGIBLE`, 503 | yazılır (503'te en iyi çaba) | `Failed` |

  **Tek `Outcome` kuralı** (paket Bölüm 21.2'deki cümlenin aynısı; rev 8):
  > **`Denied`** = (a) istekçi (kimlik bilgisi, kullanıcı ya da kiracı) bu işi yapmaya **yetkili değil**, ya da (b) istenen kayıt
  > istekçiye **tek gövdeli bir "yok" cevabıyla gizlenir** (yok / başka kiracının / aktif değil ayırt edilmez). **`Failed`** =
  > diğer her ret (biçim, iş kuralı, yapılandırma / ana veri, kaynak erişilemezliği).

  Gerekçe: kimliksiz bir istek ne kiracı ne kişi adlandırabilir; onu yazmak herkesin bir kiracının günlüğünü doldurmasına
  izin verir. Kimliği doğrulanmış ve kiracısı bilinen her ret iz bırakır.

  Ret kaydının yükü paket Bölüm 21.3 Tablo B'dir; bu paragraf onunla birebirdir (rev 7, son bakış 01 tur 2 F-6). Ortak:
  `RequestType` `AllocateRecordCodeCommand`, varlık türü `RecordCodeAllocation`, kategori `PlatformConfiguration`, kaynak
  servis kapalı listedeki ad; varlık kimliği yok; `BeforeState` / `AfterState` yok. `Metadata`: `EventName`
  (`platform.record-code.allocation-refused`), `ReasonCode`, `EvaluationStep` (§5 numarası) her retde; `IdempotencyKey`,
  `RecordType`, `PrefixScope`, `LegalEntityId` yalnız gövde ayrıştırıldıktan sonraki retlerde (§5 adım 6–13) — **adım 4
  (`RECORD_CODE_TENANT_NOT_ACTIVE`) ve adım 5 (400 `RECORD_CODE_REQUEST_INVALID`) retlerinde bu anahtarlar yoktur**; `Prefix`
  yalnız önek çözüldükten sonraki retlerde (`LEGAL_ENTITY_SHORT_CODE_CHANGED`, `GROUP_PREFIX_MISMATCH`, adım 12–13'teki 503);
  `code` / `sequence` hiçbir retde. Kiracı ve aktör: adım 4–13 retlerinde kayıt **token kiracısının**, aktör kullanıcıdır
  (`TenantUser`) — adım 4'te kiracı kaydı yok ya da silinmişse kayıt **platform denetim kiracısında**, hedef kiracı token
  kiracısı (rev 8). **Platform aktörü reddi** (adım 2): kayıt **platform denetim kiracısında**, hedef kiracı token'daki
  `tenant_id` (yoksa, Guid değilse ya da boş Guid ise boş), aktör platform / ortak yönetici, `Outcome = Denied`, `Metadata` yalnız `EventName`, `ReasonCode`
  (`RECORD_CODE_FORBIDDEN`), `EvaluationStep = 2`; yazımı hız sınırlayıcıdan geçer (sınırı aşan retler yazılmaz).
- Tekrar (aynı anahtar) yeni kayıt yazmaz.
- Bu kayıtlar BL-501 ortak ileticisiyle değil Platform içinden yazılır. Tüketici kendi Create kaydını kendi yoluyla yazar
  ve kendi kaydına `idempotencyKey` ile `code`'u koyar; iki kayıt bunlarla eşleşir.

## 12. Açık noktalar

**Dondurma kuralı (rev 9, son bakış 01 tur 4 F-1):** "Bekletir" sütunu **evet** olan nokta (N-2, N-3, N-4, N-7, N-9, N-10)
kapanmadan 1.0.0 dondurulmaz; **hayır** olan nokta (N-8, N-11, N-12) dondurmayı bekletmez — 1.0.0 metnini değiştirmez ya da
sonradan yalnız eklemeli değişiklikle gelir (§13). KAPANDI satırları (N-1, N-5, N-6) bilgi içindir.

| # | Nokta | Öneri | Sahip | Son tarih | Bekletir |
|---|---|---|---|---|---|
| N-1 | İç uç kimlik deseni | **KAPANDI — K-12 (e)**: P2 + azaltıcılar; arka plan çağıranı paket DL-9 | Beste | 2026-10-30 | — (kapandı) |
| N-2 | `Response<T>` zarfı — v0 "200 {code, sequence}" yazıyordu, zarfı tanımlamıyordu; D59/1 ve Y-8 yanıt dahil "yalnız ekleme" der | **öneri:** zarf (Platform uçlarının hepsi gibi), `data` içeriği v0 ile aynı; "v0 yanıt biçimi bağlayıcı değildi" bir öneridir. **Varsayılan (rev 6):** teyit dondurma adımına kadar gelmezse 1.0.0 zarfla donar; sonradan en üst düzey biçim istenirse büyük sürüm (§13) | **Treasury CT** (teyit); varsayılanı CT dondurmada uygular | 2026-11-03 (dondurma) | **evet** (yanıt biçimi; teyit yoksa varsayılanla kapanır) |
| N-3 | `idempotencyKey` biçimi (1–200 yazdırılabilir ASCII) | taslaktaki gibi | Beste + Treasury CT | 2026-11-03 | **evet** |
| N-4 | Tüketici portuna `prefixScope` (BNK, BAC); MDM bağdaştırıcısının `BAC` eşlemesi (şirket hesabı → LegalEntity, tedarikçi hesabı → Group) | port imzasına isteğe bağlı parametre; tüketici AC'si | Treasury CT + MDM sahibi | 2026-11-03 | **evet** |
| N-5 | `Platform` kapsamı | **KAPANDI — K-11**: tanımlı, DL-4'te açılır, o güne kadar 422 | Beste | 2026-10-30 | — (kapandı) |
| N-6 | `PLN` katalogda mı | **KAPANDI — K-12 (d)**: katalogda, bağlanması DL-4; HTTP'den 403 | Beste | 2026-10-30 | — (kapandı) |
| N-7 | Şirket öneki biçimi (K-6) | karar yoksa RCS-001 §1 | K1 (`gmg-10`) | 2026-11-02 | **evet** (gelmezse RCS-001 §1 ile kapanır) |
| N-8 | `CTR` → `AGR` RCS-001 §3 satırı; MOD-0144 / MOD-0217 kapsam çakışması | `AGR` sözleşmede kalır (K-12 a) | Ali (`.antigravity`) + Treasury PR'ı; Cihan + Treasury | 2026-11-03 (§3); TBD (çakışma) | **hayır** — §3 kolu DL-7, katalog testinde adlı istisna; MOD-0144 / MOD-0217 çakışma kolu TBD; ikisi de 1.0.0 metnini değiştirmez (`AGR` kalır) |
| N-4 notu | **MDM tarafı teyit edildi — Ali (MDM sahibi), 2026-11-04:** BNK → Group (legalEntityId boş); BAC şirket hesabı → LegalEntity (hesabın sahibi şirket); BAC tedarikçi hesabı → Group (legalEntityId boş); AGR → LegalEntity (yöneten şirket). MOD-0289 / MOD-0217 D93/1, D65/2, D94/1, D95/1 ve Treasury D125/3 ile aynı. Treasury CT teyidi bekleniyor | Ali | 2026-11-04 | evet (Treasury teyidine kadar) |
| N-7 notu | **KAPANDI — 2026-11-03 (MVP-1 CT):** K1'den 2026-11-02'ye kadar karar gelmedi; RCS-001 §1 ile kapandı (paket K-6) | — | — | 2026-11-03 | — (kapandı) |
| N-9 notu | **KAPANDI — sahip kararı (Beste, 2026-11-03, paket A-16):** 600 istek / dakika / bölüm; eski sırrın geçerlilik penceresi en çok 24 saat | — | — | 2026-11-03 | — (kapandı) |
| N-9 | Hız sınırı değeri (bölüm: tüketici servis + kiracı) ve eski sırrın geçerlilik penceresi | 600 istek/dakika/bölüm; pencere en çok 24 saat | Beste | 2026-11-03 | **evet** |
| N-10 | §1 kullanıcı izni adlarının teyidi (MOD-0018-FU27 taslağı; `APD` alacak dekontu dahil) | tablodaki gibi | Beste + Treasury CT + MDM sahibi | 2026-11-03 | **evet** |
| N-11 | MDM lookup-validation v1.1 (`approvalStatus`) yayını; o sürümde Platform okuyucusunun "alan yok → 503" kuralına geçmesi (kod değişikliği) | v1.1'e kadar tüketici onay ön kontrolü zorunlu | MOD-0220 sahibi + Beste | TBD | **hayır** — v1.1 yayını gelecekte; o güne kadar tüketici ön kontrolü, sözleşme metni değişmez |
| N-12 | RCS-001 §5.4 kısa kod kalıcı kullanım kilidi (MDM tarafı); o güne kadar `LEGAL_ENTITY_SHORT_CODE_CHANGED` ara dönem kilidi | MDM'de kilit | MDM sahibi (Ali) + Beste | 2026-11-06 | **hayır** — DL-5 kararı (2026-11-06); ara dönem kilidi 1.0.0'da tanımlı |

## 13. Sürüm kuralı

- 1.0.0 istek gövdesi ve hata kodlarında v0'a göre yalnız ekleme; v0 gövdesiyle yazılmış tüketici değişmeden çalışır. Yanıt
  zarfı için **öneri**: v0'da bağlayıcı değildi kabulüyle 1.0.0'da ilk kez zarfla dondurulur — Treasury CT teyidi
  bekleniyor (§9, §12 N-2).
- Bundan sonraki her değişiklik yalnız ekleme olur (yeni tür, yeni isteğe bağlı alan, yeni neden kodu, `Platform`
  kapsamının DL-4'te açılması). Bir alanın kaldırılması, anlamının değişmesi, kod kalıbının (§7) değişmesi ya da var olan
  bir durum kodunun anlamının değişmesi **büyük sürüm** sayılır.
- Yeni tür eklemek sözleşme değişikliği değildir; katalog RCS-001 §3'e bağlıdır.

## 14. Değişiklik geçmişi

- 1.0.0 rev 9 (2026-11-02) — paket r10, son bakış 01 tur 4 F-1: §12'ye "Bekletir" sütunu ve dondurma kuralı (N-2, N-3, N-4, N-7,
  N-9, N-10 bekletir; N-8, N-11, N-12 bekletmez); başlık cümlesi buna göre. İstek, cevap ve hata kodları değişmedi.

- 1.0.0 rev 8 (2026-11-02) — paket r9 (künye ready-for-dev), inceleme 03 tur 6 ve son bakış 01 tur 3 düşük bulguları: §5 adım 13'e
  kilit eşleşmezliği 409; §11 tek `Outcome` cümlesi paketteki ile aynı, yinelenen yük cümlesi kaldırıldı; adım 4 reddinde
  olmayan kiracının kayıt kiracısı; boş Guid `tenant_id` → hedef kiracı boş. İstek gövdesi, durum kodları ve neden kodları
  değişmedi.

- 1.0.0 rev 7 (2026-11-02) — paket r8, son bakış 01 tur 2 ve inceleme 03 tur 5: §11 ret yükü paket Bölüm 21.3 Tablo B ile
  birebir (adım 5 reddi, platform aktörü reddinin kiracısı ve `EvaluationStep = 2`, sınırlayıcı); §4 iki `ProblemDetails`
  satırı (`AUDIT_RECORD_UNAVAILABLE` 503, `AUDIT_INTENT_INVALID` 500) ve zarf kuralının adlı istisnası; başlıktaki A-17
  metni §12 N-2 varsayılanıyla aynı. İstek gövdesi değişmedi; hata kodları yalnız eklendi.

- 1.0.0 rev 6 (2026-11-02) — paket r7, bağımsız son bakış 01 (özet a52313ba) ve inceleme 03 tur 4 (özet f4049860): paketle beş
  uyuşmazlık giderildi (§5 adım 9 alan alan eşitlik, §6 `ORG` / `POS` boş küme, §4 `GROUP_PREFIX_MISMATCH` kilitli ya da değil,
  §1 `Content-Type` 400, künye); MDM 403 için kalıcı 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` (ekleme) ve sınırlı yeniden
  deneme (§1, §4, §8, §9); ret kaydı tablosu: 400 ve platform aktörü 403 yazılır, 429 adlı sapma, tek `Outcome` kuralı ve
  422 `RECORD_CODE_SCOPE_INVALID` `Failed` (§11); N-2 için varsayılan (§12). İstek gövdesi değişmedi; hata kodları yalnız eklendi.

- 1.0.0 rev 5 (2026-11-02) — paket r5, bağımsız inceleme 03 tur 2 (KOŞULLU): aynı anahtar örtüşmesinde 503 sınırı ve
  "503'ten sonra aynı anahtarla yeniden dene" kuralı yazıldı, "hepsi aynı kodu alır / 503 yalnız gerçek erişilemezlikte"
  ifadesi daraltıldı (§8, F-3); `x-measured-at` 81864bd98 (başlık, F-3). İstek gövdesi, durum kodları ve neden kodları
  değişmedi.
- 1.0.0 rev 4 (2026-11-02) — paket r4, bağımsız inceleme 03 (KOŞULLU): başarı ve ret denetim yükü paket Bölüm 21.3'teki
  tek tanıma bağlandı, alan adları ve yerleri (`AfterState` / `Metadata`) birebir, ret alanlarının elde olduğu adım yazıldı
  (§11, F-2); commit sonucu bilinmeyen deneme için 503 + aynı anahtarla tekrar kuralı, "kutu satırı zaten yazılmışsa 200"
  cümlesi kaldırıldı (§11, F-3); "aynı gövde" alan alan ve varsayılan uygulanmış kapsamla, v0 → açık `LegalEntity`
  örneği (§8, F-9). İstek gövdesi, durum kodları ve neden kodları değişmedi.
- 1.0.0 rev 3 (2026-10-30) — bağımsız inceleme 02 (KOŞULLU): denetim kutusu anahtarı ayırma kimliğinden / ret başına yeni
  Guid, aynı ayırmanın tekrarında 200 (§11); `approvalStatus` için v1.1 bağımlılığı doğru yazıldı (§7, N-11); kısa kod
  ara dönem kilidi ve `LEGAL_ENTITY_SHORT_CODE_CHANGED` (§4, §7, N-12); grup önekinin tek kaynağı belge `OrgPrefix`'i (§6);
  hız sınırı bölümü (tüketici servis, kiracı) ve sırası (§1, §4, §5); `Platform` için "tür izni geçtiyse 422" (§2, §4, §5);
  v0 yanıt biçiminin bağlayıcı olmadığı **öneri** olarak yazıldı, Treasury CT teyidi bekleniyor (başlık, §3, §9, N-2);
  varsayılan / tohum kiracı normal iş kiracısıdır (§1).
- 1.0.0 rev 2 (2026-10-30) — bağımsız inceleme 01 (UYGUN DEĞİL) ve sahip kararları K-8r, K-9…K-12: denetim aktörü kullanıcı,
  kaynak servis kapalı listeden (§1, §11); ret kaydı tablosu (§11); `Platform` tanımlı ama DL-4'e kadar 422 (§2, §6);
  401/403 eşlemesi MOD-0018-FU21 ile birebir, iptal edilmiş kimlik bilgisi 401 (§1, §4); kullanıcı izni, kiracı Active,
  hız sınırı, `LEGAL_ENTITY_NOT_ELIGIBLE`, `GROUP_PREFIX_MISMATCH` (§1, §4); eşzamanlı aynı anahtar ve N−1 boşluk sınırı
  (§8); yanıt zarfının v0'da bağlayıcı olmadığı ve "yalnız ekleme"nin kapsamı düzeltildi (başlık, §9, §13).
- 1.0.0 rev 1 (2026-10-30) — ilk yazılı sürüm (taslak). Kaynak: sahip kararları K-3…K-8 (Beste 2026-10-30), v0 metni (D59/1, Y-8).
