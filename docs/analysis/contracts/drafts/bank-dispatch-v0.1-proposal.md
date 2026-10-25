# BANK-DISPATCH 0.1.0 — MOD-0269 arayüz sözleşmesi (TASLAK / öneri)

    contract       : BANK-DISPATCH
    version        : 0.1.0
    x-status       : DRAFT (rev 3, gap listeleri 7 ve 8 uygulandı) — öneri; CT incelemesi ve sahip onayından önce dondurulmaz
    x-owner        : MOD-0269 Banks/SWIFT/Payments Gateway [External Provider] (Blueprint W-1, External Systems Register)
    x-consumers    : MOD-0126 Payments & Approvals (serbest bırakma) · MOD-0124 Cash Positioning (durum / ekstre — bu sürümde KAPALI)
    x-decision     : sahip kararı D5 = karar 0.1 seçenek A — "önce sözleşme, dağıtım kapalı"
                     (docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md); D17 (kaynak hesap talimat
                     düzeyinde; beklenmeyen sonuç kaydedilir, parti kilitlenir — treasury-intake-owner-decisions-02.md)
    x-queue        : treasury-intake TRM-06
    x-review       : docs/records/audits/2026-10/treasury-intake-handover-review-01/GAP-LIST-07-BANK-DISPATCH.md
    x-source-input : producer paketi MOD-0269 v1.0 (manifest sha256 55b503fe403c3ece24c3e3ef8316ddaac9abfe4b45b7560c8979a4e9d5a9a2c7),
                     level-1 kaydı docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0269-v1.0.md
    x-identity     : verify_module_id.py --check-id MOD-0269 --name "Banks/SWIFT/Payments Gateway [External Provider]" → exit 0 (2026-10-25)

Bu sözleşme bir **süreç içi port** sözleşmesidir (`IBankDispatchPort`, `Diten.FinanceTreasuryService` içinde), HTTP ucu
değildir; Gateway rotası yoktur. Sağlayıcıdan bağımsızdır: hangi banka, SWIFT servis bürosu ya da toplayıcı kullanılacağı
ayrı bir karardır (karar 0.1 seçenek C) ve bu sürüm onu varsaymaz.

## 1. Kapsam

| Konu | Bu sürümde |
|---|---|
| Ödeme gönderimi (dispatch) | **sözleşme var, gönderim KAPALI** — tek bağdaştırıcı `NotConfiguredBankDispatchAdapter`, her geçerli çağrı `NotConfigured` döner, ağ çağrısı yok |
| Gönderim durumu sorgusu | sözleşme var; tek bağdaştırıcı `NotConfigured` döner |
| Banka kimlikli geri çağrı / olay | **yok** (kaynak kapalı akış 2) |
| Güvenilir non-HTTP çalışan | **yok** (kaynak kapalı akış 3) |
| Banka ekstresi / MOD-0121'e yayım | **yok** (kaynak kapalı akış 4) — MOD-0124 banka beslemesi karşılanmamış kalır |
| Dosya ile değişim (pain.001 vb.) | **yok** — karar 0.1 seçenek B, ayrı karar |

Kaynakta kapalı olan dört akış (paket §L 2–5; `treasury-mod0269-transfer-readiness-disposition-20261018`) burada da
kapalıdır ve **varsayılmaz**.

## 2. Port

```text
interface IBankDispatchPort
    Task<BankDispatchResult>  DispatchAsync(BankDispatchRequest request, CancellationToken ct)
    Task<BankDispatchStatus>  GetDispatchStatusAsync(BankDispatchStatusQuery query, CancellationToken ct)
```

- Çağıran yalnız MOD-0126 `ReleasePaymentBatchCommand`'dır (bu dilimde **tek işlem**, MOD-0126 pack Runtime Constraints).
- Port uzak çağrı yapmadığı sürece (bu sürüm) sonuç belirsiz olamaz; `Unknown` yalnız gerçek bağdaştırıcı için tanımlıdır.
- **Dönüş biçimi (gap 7/P2-a):** iş sonucu (`NotConfigured`, ileride `Accepted` / `Rejected` / `Unknown`) her zaman
  **tipli sonuçtur** (`BankDispatchResult`); port iş sonucu için istisna atmaz. Sözleşme ihlali (§5'teki iki kod) tek
  istisna tipiyle bildirilir: `BankDispatchContractException { Code }`. İptal (`ct`) `OperationCanceledException`'dır.
  Başka istisna tipi sözleşme dışıdır ve tüketicide sözleşme ihlali gibi ele alınır.

## 3. Şemalar ve doğrulama

**Tipler (gap 7/iyileştirme 1):** port süreç içidir; alanlar .NET tipleridir — para `decimal`, kimlik `Guid`, tarih
`DateOnly`, zaman `DateTimeOffset` (UTC). 0.1'de kablo / JSON serileştirmesi **yoktur**. Bir sonraki sürümde süreç dışı
bir bağdaştırıcı ya da serileştirilmiş anlık görüntü gelirse kural: para JSON'da string, değişmez kültür, üs yok
(`^-?\d+(\.\d+)?$`), float yasak; Mongo'da `Decimal128`. **Banka verisi yoktur:** IBAN, hesap numarası, BIC/SWIFT,
banka adı, sağlayıcı sırrı hiçbir alanda taşınmaz; banka ve alacaklı hesapları yalnız **opak referanstır** (çözümleme
MOD-0289'a aittir ve gerçek gönderimde yapılır — §6).

**Genel kural:** her `Guid` alanı (zorunlu olsun ya da verilmiş isteğe bağlı olsun) `Guid.Empty` olamaz.

### 3.1 `BankDispatchRequest`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| contractVersion | string | evet | tam olarak `"bank-dispatch/0.1"` |
| tenantId | Guid | evet | sunucuda çözülür; istemciden alınmaz; ≠ Empty |
| legalEntityId | Guid | evet | ödeyen şirket; Active + Approved (D10/1) MOD-0126 tarafından önceden kontrol edilmiş; ≠ Empty |
| paymentBatchId | Guid | evet | MOD-0126 parti kimliği; ≠ Empty |
| releaseSequence | int | evet | **1..999 999 999** (dokuz hane; `int` aralığında, taşma yok); MOD-0126 `PaymentBatch.ReleaseSequence` (§4.2); aralık dışı → `BANK_DISPATCH_REQUEST_INVALID` |
| dispatchKey | string | evet | **tam olarak** `mod0126:{paymentBatchId:N}:release:{releaseSequence}` (desen `^mod0126:[0-9a-f]{32}:release:[1-9][0-9]{0,8}$`); istekteki `paymentBatchId` ve `releaseSequence`'ten yeniden üretilen değere eşit olmalı |
| paymentBatchCode | string | evet | RCS-001 kayıt kodu; boş değil, ≤ 64 |
| currency | string | evet | `^[A-Z]{3}$`; partinin tek para birimi |
| currencyMinorUnits | int | evet | 0..4; MOD-0126 MOD-0048'den doldurur (port hassasiyet kaynağına gitmez) |
| totalAmount | decimal | evet | > 0; ölçeği ≤ `currencyMinorUnits`; **talimat tutarlarının tam toplamı** (yuvarlama yok, `decimal` eşitliği) |
| requestedValueDate | DateOnly | evet | — |
| rail | enum? | hayır | `Swift` \| `Sepa` \| `Ach` \| `Rtgs` \| `Wire` \| `Local` (kaynak `PaymentsGatewayEnums`); boşsa sağlayıcı seçer (BD-1 açık) |
| instructions | `BankDispatchInstruction[]` | evet | 1..500; `instructionId`'ler istekte tekil |
| requestedBy | Guid | evet | Release'i yapan kullanıcı (token `sub`); ≠ Empty |
| correlationId | string | evet | sunucunun korelasyon değeri (`CorrelationIdMiddleware.CorrelationItemKey`); **opak** — UUID olması gerekmez (imzalı korelasyon UUID değildir, `services/Diten.Platform.Common/src/Diten.Platform.Common/Observability/CorrelationIdMiddleware.cs:74@975356da`); boş değil, ≤ 128 (`ObservabilityOptions.cs:66@975356da`) |

Parti düzeyinde kaynak hesap alanı **yoktur** (D17/1): kaynak hesap talimat düzeyindedir.

### 3.2 `BankDispatchInstruction`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| instructionId | Guid | evet | MOD-0126 talimat kimliği; ≠ Empty; istekte tekil |
| sourceBankAccountRef | Guid | evet | **opak** — talimatın MOD-0126 `PaymentInstruction.SourceBankAccountRef` değeri, **olduğu gibi** (D17/1); ≠ Empty |
| amount | decimal | evet | > 0; ölçeği ≤ `currencyMinorUnits` |
| currency | string | evet | `request.currency`'ye eşit |
| payeeType | enum | evet | `LegalEntityCounterparty` \| `Supplier` \| `Employee` \| `Other` |
| payeeRef | Guid? | koşullu | `payeeType ≠ Other` ise zorunlu; verilmişse ≠ Empty; **opak** |
| destinationRef | Guid? | hayır | **opak** — MOD-0289 hedef hesap referansı; verilmişse ≠ Empty |
| remittanceReference | string? | hayır | ≤ 140; banka verisi içeremez (alan adı/desen taraması: IBAN, BIC deseni) |

**Kaynak hesap kuralı (gap 7/P1-a, D17/1):** port ve bağdaştırıcı talimatları kaynak hesaba göre **birleştirmez,
gruplamaz, yeniden sıralamaz** ve parti düzeyinde tek hesaba **indirgemez**; her talimat kendi kaynak hesabıyla taşınır.
MOD-0126 istek kurarken `DefaultSourceBankAccountRef`'i talimatın değerinin yerine koymaz (talimat değeri zorunludur,
MOD-0126 Validation Rules). Çok hesaplı bir partinin gerçek bir sağlayıcıda nasıl bölüneceği gerçek bağdaştırıcının
kararıdır (§6/2) ve talimat → hesap eşleşmesini değiştiremez.

### 3.3 `BankDispatchResult`

| Alan | Tip | Kural |
|---|---|---|
| contractVersion | string | `"bank-dispatch/0.1"` |
| dispatchKey | string | istekteki anahtar |
| outcome | enum | `NotConfigured` \| `Accepted` \| `Rejected` \| `Unknown` — **0.1'de üretilen tek değer `NotConfigured`** |
| outcomeCode | string | `BANK_DISPATCH_NOT_CONFIGURED` (0.1) |
| adapterCode | string | `not-configured` (0.1) |
| providerReference | string? | 0.1'de her zaman `null` |
| decidedAt | DateTimeOffset | UTC; **idempotency güvencesinin parçası değildir** (§4.1) |

### 3.4 `BankDispatchStatusQuery` / `BankDispatchStatus`

- Sorgu: `contractVersion`, `tenantId`, `legalEntityId`, `dispatchKey` — §3.1'deki kurallarla. Hata eşlemesi gönderimle
  **aynıdır** (§5): `contractVersion` ≠ `bank-dispatch/0.1` → `BANK_DISPATCH_CONTRACT_VERSION_UNSUPPORTED`; diğer ihlaller
  (Guid = Empty, anahtar deseni) → `BANK_DISPATCH_REQUEST_INVALID`.
- Yanıt: `dispatchKey`, `outcome` (§3.3 sözlüğü), `outcomeCode`, `providerReference?`, `asOf` (UTC). 0.1'de her zaman
  `NotConfigured`. Bu sürümde sorguyu çağıran bir MOD-0126 akışı yoktur (Release tek işlemdir); sözleşme yeri ayrılır.

## 4. Davranış kuralları

1. **Idempotency — kimin neyi garanti ettiği (gap 7/P1-c):**
   - **Bağdaştırıcı (0.1):** durumsuzdur. Garantisi dardır: §3'e uyan **her** istek için `outcome = NotConfigured`,
     `outcomeCode = BANK_DISPATCH_NOT_CONFIGURED`, `adapterCode = not-configured`, `providerReference = null` ve istekteki
     `dispatchKey` döner. `decidedAt` her çağrıda değişebilir; "aynı anahtar → bayt bayt aynı yanıt" garantisi **yoktur**.
     Bağdaştırıcı anahtar geçmişi tutmaz ve aynı anahtar / farklı yük çatışmasını **algılamaz**.
   - **MOD-0126 (tüketici):** tam yanıtın tekrarı (replay) MOD-0126'nındır: aynı istemci anahtarıyla gelen Release
     `FinanceTreasuryCommandReceipt`'teki kayıtlı yanıtı döner, portu **yeniden çağırmaz**.
   - **Anahtar / yük çatışması:** 0.1'de sahibi MOD-0126'dır ve yapısal olarak engellenir — `dispatchKey` parti +
     `releaseSequence`'ten üretilir, her sıra yalnız bir `PaymentDispatchAttempt`'e bağlanır (tekil indeks, MOD-0126 pack)
     ve `Approved` partinin talimatları değişmez. Gerçek bağdaştırıcıda sahibi bağdaştırıcıdır: aynı anahtarla farklı yük
     (kanonik yük özeti farklı) `BANK_DISPATCH_KEY_PAYLOAD_CONFLICT` ile reddedilir — §6 kapı maddesi.
2. **`releaseSequence` ve anahtar üretimi (gap 7/P1-b, BD-2 kapandı):**
   - `PaymentBatch.ReleaseSequence` 0'dan başlar. **Yeni** bir Release denemesi (yeni makbuz) sırayı **aynı Mongo
     işleminde** +1 yapar; artış parti sürümüne koşullu (CAS) yazılır, iki eşzamanlı Release aynı sırayı alamaz
     (kaybeden 409 `PAYMENT_BATCH_VERSION_CONFLICT`, port çağrısı yok).
   - Aynı makbuzun tekrarı (aynı istemci anahtarı, makbuz `Completed`) yeni sıra almaz, portu çağırmaz, kayıtlı yanıtı döner.
   - **Commit edilmiş** makbuz yanıtını korur: aynı istemci anahtarıyla tekrar kayıtlı yanıtı (ve o denemenin
     `dispatchKey`'ini) döner (gap 8/P2-a).
   - **Geri alınan** deneme (çökme, iptal, sözleşme ihlali) hiçbir sıra ya da anahtar **rezerve etmez**: sıra artışı da
     geri alınır, ama arada başka bir Release commit ederse aynı istemci anahtarıyla yeniden deneme bir sonraki sırayı
     ve dolayısıyla **farklı** bir `dispatchKey`'i alır. Geri alınan denemenin porta etkisi olmadığı için (0.1, ağ yok)
     bu davranış bu sürüm için yeterlidir; gerçek bağdaştırıcıda anahtar uzak çağrıdan önce kalıcı niyete yazılır (§6/3).
   - **Üst sınır (gap 8/P2-b):** `ReleaseSequence` 999 999 999'a ulaşmışsa MOD-0126 yeni Release'i artırma ve port
     çağrısından **önce** reddeder: 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED`, kayıt yok; taşma (overflow) oluşmaz.
   - Kesin ret sonrası yeniden serbest bırakma kavramı 0.1'de yoktur (sonuç hep `NotConfigured`); gerçek bağdaştırıcıda
     her yeni deneme yine yeni sıra alır, anahtar kuralı değişmez (K16).
3. **Ağ yok:** 0.1 bağdaştırıcısı hiçbir dış adrese çağrı yapmaz; testte ağ çağrısı olmadığı ölçülür (MOD-0126 AC-19).
4. **Sır yok:** port yapılandırması kimlik bilgisi, sertifika ya da uç adresi taşımaz; kaynaktaki `ProviderAdapter` /
   `ConnectivityContract` / kimlik bilgisi referansı varlıkları (paket §B, §E) bu sürüme alınmaz.
5. **Denetim:** port kendi denetim kaydını yazmaz; MOD-0126 Release'in tek `…release-attempted` olayı sonucu taşır
   (CentralAudit, yol b).
6. **Kiracı / şirket:** port isteği kiracı ve şirket kimliğiyle gelir; bağdaştırıcı başka kiracı/şirket verisine erişmez.
7. **İptal (gap 7/P2-a):** `ct` çağrıdan önce ya da çağrı sırasında iptal edilirse port `OperationCanceledException`
   atar ve kısmi sonuç dönmez. 0.1'de uzak etki olmadığı için güvenlidir: MOD-0126 işlemi geri alır, hiçbir kayıt
   yazılmaz, aynı anahtarla yeniden deneme baştan çalışır. Gerçek bağdaştırıcıda gönderimden sonra iptal `Unknown`
   sayılır (§6/3).
8. **0.1 tüketicisi beklenmeyen sonuç alırsa (gap 7/iyileştirme 2, D17/2):** `NotConfigured` dışındaki her `outcome`
   (`Accepted`, `Rejected`, `Unknown`, tanımsız değer) **başarılı Release sayılmaz**. MOD-0126: denemeyi alınan sonuçla
   (`Outcome`, `OutcomeCode`, `ProviderReference`) kaydeder, parti `Approved` kalır, `ReleaseBlockedForReview = true` olur,
   tek `…release-attempted` olayı sonucu taşır, Release 502 `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED` döner (makbuz bu
   yanıtla `Completed`). O partide sonraki Release 409 `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW` döner, port çağrılmaz. Kilidin
   elle incelemeyle açılması bu sürümde yoktur (MOD-0126 Follow-up). Gerekçe: para çıkmış olabilir; geri alma izi siler.

## 5. Hata sözlüğü ve tüketici eşlemesi

| Kod | Biçim | Ne zaman | MOD-0126 Release'teki eşleme |
|---|---|---|---|
| `BANK_DISPATCH_NOT_CONFIGURED` | tipli sonuç (`outcomeCode`) | her geçerli gönderim ve durum sorgusu (0.1) | sonuç, hata değil: **200**, `dispatchOutcome = NotConfigured`, deneme kaydı + makbuz `Completed` + olay aynı işlemde |
| `BANK_DISPATCH_CONTRACT_VERSION_UNSUPPORTED` | `BankDispatchContractException` | gönderimde **ve** durum sorgusunda `contractVersion` ≠ `bank-dispatch/0.1` (tek eşleme) | programlama hatası: işlem **geri alınır** (deneme, sıra artışı, makbuz, olay yok), **500** `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION`, yapılandırılmış hata günlüğü |
| `BANK_DISPATCH_REQUEST_INVALID` | `BankDispatchContractException` | §3 kurallarına uymayan istek | MOD-0126 doğrulaması önce yakalar (iş hatası 400/409/422 orada); port ikinci savunma hattıdır — porta ulaşan geçersiz istek programlama hatasıdır: yukarıdaki gibi geri alma + **500** `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION` |
| (beklenmeyen `outcome`) | tipli sonuç | `NotConfigured` dışı sonuç (§4.8) | **502** `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`, deneme kaydedilir, parti kilitlenir (D17/2) |
| (iptal) | `OperationCanceledException` | `ct` iptal | geri alma; yanıt yok (bağlantı kapanmış); kayıt yok |
| (başka istisna) | — | sözleşme dışı | sözleşme ihlali gibi: geri alma + 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION` |

Geri alma durumlarında makbuz da işlemle birlikte geri alındığı için aynı istemci anahtarıyla tekrar baştan çalışır ve
aynı deterministik hatayı üretir; 500 yanıtı makbuzda saklanmaz.

### 5.1 Sözleşme negatif testleri (gap 7/P2-b; MOD-0126 AC-37; beklenmeyen sonuç AC-38)

Bağdaştırıcı (doğrudan) ve Release (uçtan uca) için her biri ayrı vaka: `contractVersion` farklı; her `Guid` alanında
`Guid.Empty` (tenantId, legalEntityId, paymentBatchId, requestedBy, instructionId, sourceBankAccountRef, verilmiş payeeRef
ve destinationRef); `dispatchKey` desen dışı ya da parti/sıradan üretilenle farklı; `releaseSequence` 0 ve 1 000 000 000; boş ve 501
talimat; yinelenen `instructionId`; `totalAmount` ≠ Σ `amount` (bir en küçük birim farkla); `amount` ölçeği >
`currencyMinorUnits`; `amount` 0 ya da negatif; talimat para birimi farklı; `payeeType ≠ Other` ve `payeeRef` yok;
`remittanceReference` 141 karakter ya da IBAN deseni; `correlationId` boş ya da 129 karakter; durum sorgusunda boş Guid,
desen dışı anahtar ve farklı `contractVersion` (→ `…_VERSION_UNSUPPORTED`). Release'te `ReleaseSequence = 999 999 999`
olan parti → 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED`, port çağrısı ve kayıt yok. Beklenen: her vaka `BankDispatchContractException(BANK_DISPATCH_REQUEST_INVALID | …_VERSION_UNSUPPORTED)`;
Release'te 500 ve hiçbir kalıcı kayıt. Ayrıca: iki kaynak hesaplı partide istekteki her talimat kendi hesabını taşır
(birleştirme yok); `decidedAt` farklı iki çağrının diğer alanları aynıdır. Sabotaj: `Guid.Empty` ya da toplam kontrolü
kaldırıldığında ilgili vaka kırmızıya döner.

## 6. Gerçek gönderim kapısı (bu sözleşmenin 0.1'den sonraki sürümü için)

Gerçek bir bağdaştırıcı `IBankDispatchPort`'a ancak şunlar kapandığında bağlanabilir (MOD-0126 pack, Runtime Constraints
"Gerçek dağıtım açılmadan önce zorunlu kapı"):

1. Sağlayıcı ve arayüz seçimi (karar 0.1 seçenek B ya da C) ve **bankacılık sahibinin** adlandırılması.
2. Hesap sahipliği, Tenant/LE uygunluğu ve alacaklı–hesap eşleşmesi için sözleşmeli kontrol (MOD-0289, MOD-0140); çok
   kaynak hesaplı partinin sağlayıcıda bölünme kuralı (talimat → hesap eşleşmesi değişmeden).
3. `Unknown` sonucu için Release'in iki aşamalı hâle gelmesi (`PaymentReleaseIntent`, Reconcile, sonuç olayı); gönderim
   sonrası iptal `Unknown`'dır.
4. Anahtar / yük çatışması bağdaştırıcıda: anahtar başına kanonik yük özeti saklanır, farklı yük
   `BANK_DISPATCH_KEY_PAYLOAD_CONFLICT`; aynı yük aynı sağlayıcı sonucunu döner.
5. Banka kimlikli geri çağrı / güvenilir çalışan / ekstre akışlarının kendi sözleşmeleri (kaynak kapalı akışlar 2–4).
6. Kimlik bilgisi ve sertifika yönetimi (gizli bilgi deposu; değer asla sözleşmede ya da kayıtta).
7. Kilitli partinin (§4.8) elle inceleme ve açma komutu.
8. Bu sözleşmenin yeni sürümü: `outcome` sözlüğüne yalnız ekleme; `dispatchKey` kuralı değişmez (K16); serileştirme
   kuralı §3'teki gibi.

## 7. Açık konular

| # | Konu | Sahibi | Durum |
|---|---|---|---|
| BD-1 | `rail` alanının 0.1'de tutulması (kaynakta var, kullanılmıyor) ya da çıkarılması | sahip (K1) | açık |
| BD-2 | `releaseSequence` tanımı | Treasury CT | **kapandı** — §4.2 (gap 7/P1-b) |
| BD-3 | Bankacılık sahibi | sahip (K1) | açık |
| BD-4 | Sözleşmenin `drafts/`'tan `docs/analysis/contracts/bank-dispatch.md` (FROZEN 0.1.0) olarak yayımlanması | CT incelemesi + sahip | açık |

## 8. Değişiklik geçmişi

- 0.1.0 rev 3 (2026-10-25) — gap listesi 8: geri alınan deneme anahtar rezerve etmez (yalnız commit edilmiş makbuz
  yanıtı korur); `releaseSequence` 1..999 999 999 ve tükenmede kontrollü 409; durum sorgusunda sürüm hatası tek eşleme.
- 0.1.0 rev 2 (2026-10-25) — gap listesi 7: kaynak hesap talimat düzeyinde (D17/1); `releaseSequence` + `dispatchKey`
  üretimi (BD-2 kapandı); idempotency güvencesi daraltıldı, tam tekrar ve çatışma sahibi MOD-0126; tipli sonuç /
  istisna / iptal / HTTP eşlemesi; `Guid.Empty`, korelasyon, tekil talimat, hassasiyet (`currencyMinorUnits`), durum
  sorgusu doğrulaması ve negatif testler; süreç içi tipler ve ileride serileştirme kuralı; beklenmeyen sonuçta kaydet +
  kilitle (D17/2).
- 0.1.0 (2026-10-25) — ilk taslak; dağıtım kapalı (D5).
