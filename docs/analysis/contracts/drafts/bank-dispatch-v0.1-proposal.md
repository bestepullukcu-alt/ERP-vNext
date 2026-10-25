# BANK-DISPATCH 0.1.0 — MOD-0269 arayüz sözleşmesi (TASLAK / öneri)

    contract       : BANK-DISPATCH
    version        : 0.1.0
    x-status       : DRAFT — öneri; CT incelemesi ve sahip onayından önce dondurulmaz
    x-owner        : MOD-0269 Banks/SWIFT/Payments Gateway [External Provider] (Blueprint W-1, External Systems Register)
    x-consumers    : MOD-0126 Payments & Approvals (serbest bırakma) · MOD-0124 Cash Positioning (durum / ekstre — bu sürümde KAPALI)
    x-decision     : sahip kararı D5 = karar 0.1 seçenek A — "önce sözleşme, dağıtım kapalı"
                     (docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md)
    x-queue        : treasury-intake TRM-06
    x-source-input : producer paketi MOD-0269 v1.0 (manifest sha256 55b503fe403c3ece24c3e3ef8316ddaac9abfe4b45b7560c8979a4e9d5a9a2c7),
                     level-1 kaydı docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0269-v1.0.md
    x-identity     : verify_module_id.py --check-id MOD-0269 --name "Banks/SWIFT/Payments Gateway [External Provider]" → exit 0 (2026-10-25)

Bu sözleşme bir **süreç içi port** sözleşmesidir (`IBankDispatchPort`, `Diten.FinanceTreasuryService` içinde), HTTP ucu
değildir; Gateway rotası yoktur. Sağlayıcıdan bağımsızdır: hangi banka, SWIFT servis bürosu ya da toplayıcı kullanılacağı
ayrı bir karardır (karar 0.1 seçenek C) ve bu sürüm onu varsaymaz.

## 1. Kapsam

| Konu | Bu sürümde |
|---|---|
| Ödeme gönderimi (dispatch) | **sözleşme var, gönderim KAPALI** — tek bağdaştırıcı `NotConfiguredBankDispatchAdapter`, her çağrı `NotConfigured` döner, ağ çağrısı yok |
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

## 3. Şemalar

Parasal alanlar `decimal` (kablo üzerinde string, `^-?\d+(\.\d+)?$`), float yasak. Kimlikler Guid. **Banka verisi yoktur:**
IBAN, hesap numarası, BIC/SWIFT, banka adı, sağlayıcı sırrı hiçbir alanda taşınmaz; banka ve alacaklı hesapları yalnız
**opak referanstır** (çözümleme MOD-0289'a aittir ve gerçek gönderimde yapılır — §6).

### 3.1 `BankDispatchRequest`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| contractVersion | string | evet | `"bank-dispatch/0.1"` |
| tenantId | Guid | evet | sunucuda çözülür; istemciden alınmaz |
| legalEntityId | Guid | evet | ödeyen şirket; Active + Approved (D10/1) MOD-0126 tarafından önceden kontrol edilmiş |
| dispatchKey | string (1..128) | evet | **deterministik**: `mod0126:{paymentBatchId:N}:release:{releaseSequence}`; aynı anahtar = aynı gönderim |
| paymentBatchId | Guid | evet | MOD-0126 parti kimliği |
| paymentBatchCode | string | evet | RCS-001 kayıt kodu |
| currency | string | evet | `^[A-Z]{3}$`; partinin tek para birimi |
| totalAmount | decimal | evet | > 0; talimat toplamına eşit |
| requestedValueDate | date | evet | ISO 8601 tarih |
| rail | enum | hayır | `Swift` \| `Sepa` \| `Ach` \| `Rtgs` \| `Wire` \| `Local` (kaynak `PaymentsGatewayEnums`); boşsa sağlayıcı seçer |
| sourceBankAccountRef | Guid | evet | **opak** — MOD-0289 hesap referansı |
| instructions | `BankDispatchInstruction[]` | evet | 1..500 |
| requestedBy | Guid | evet | Release'i yapan kullanıcı |
| correlationId | string | evet | izleme |

### 3.2 `BankDispatchInstruction`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| instructionId | Guid | evet | MOD-0126 talimat kimliği |
| amount | decimal | evet | > 0; para birimi küsuratını aşmaz |
| currency | string | evet | partininkine eşit |
| payeeType | enum | evet | `LegalEntityCounterparty` \| `Supplier` \| `Employee` \| `Other` |
| payeeRef | Guid | koşullu | `payeeType ≠ Other` ise; **opak** |
| destinationRef | Guid | hayır | **opak** — MOD-0289 hedef hesap referansı |
| remittanceReference | string (≤140) | hayır | serbest metin; banka verisi içeremez (doğrulama kuralı alan adı/desen taraması) |

### 3.3 `BankDispatchResult`

| Alan | Tip | Kural |
|---|---|---|
| contractVersion | string | `"bank-dispatch/0.1"` |
| dispatchKey | string | istekteki anahtar |
| outcome | enum | `NotConfigured` \| `Accepted` \| `Rejected` \| `Unknown` — **0.1'de üretilen tek değer `NotConfigured`** |
| outcomeCode | string | `BANK_DISPATCH_NOT_CONFIGURED` (0.1) |
| adapterCode | string | `not-configured` (0.1) |
| providerReference | string? | 0.1'de her zaman `null` |
| decidedAt | date-time | UTC |

### 3.4 `BankDispatchStatusQuery` / `BankDispatchStatus`

- Sorgu: `tenantId`, `legalEntityId`, `dispatchKey`.
- Yanıt: `dispatchKey`, `outcome` (§3.3 sözlüğü), `outcomeCode`, `providerReference?`, `asOf` (UTC). 0.1'de her zaman
  `NotConfigured`.

## 4. Davranış kuralları

1. **Idempotency:** aynı `dispatchKey` için port her zaman aynı sonucu döndürür. `NotConfiguredBankDispatchAdapter`
   durumsuz ve deterministiktir (aynı girdi → aynı çıktı); kayıt MOD-0126 `PaymentDispatchAttempt` tarafından tutulur.
2. **Ağ yok:** 0.1 bağdaştırıcısı hiçbir dış adrese çağrı yapmaz; testte ağ çağrısı olmadığı ölçülür (MOD-0126 AC-19).
3. **Sır yok:** port yapılandırması kimlik bilgisi, sertifika ya da uç adresi taşımaz; kaynaktaki `ProviderAdapter` /
   `ConnectivityContract` / kimlik bilgisi referansı varlıkları (paket §B, §E) bu sürüme alınmaz.
4. **Denetim:** port kendi denetim kaydını yazmaz; MOD-0126 Release'in tek `…release-attempted` olayı sonucu taşır
   (CentralAudit, yol b).
5. **Kiracı / şirket:** port isteği kiracı ve şirket kimliğiyle gelir; bağdaştırıcı başka kiracı/şirket verisine erişmez.

## 5. Hata sözlüğü

| Kod | Ne zaman | Not |
|---|---|---|
| `BANK_DISPATCH_NOT_CONFIGURED` | her gönderim ve durum sorgusu (0.1) | sonuç, hata değil; Release 200 döner |
| `BANK_DISPATCH_CONTRACT_VERSION_UNSUPPORTED` | `contractVersion` ≠ `bank-dispatch/0.1` | programlama hatası; Release'teki HTTP eşlemesi MOD-0126 pack'inde yazılacak (TBD) |
| `BANK_DISPATCH_REQUEST_INVALID` | §3 kurallarına uymayan istek (ör. banka verisi deseni) | MOD-0126 doğrulaması önce yakalar; port ikinci savunma hattıdır |

## 6. Gerçek gönderim kapısı (bu sözleşmenin 0.1'den sonraki sürümü için)

Gerçek bir bağdaştırıcı `IBankDispatchPort`'a ancak şunlar kapandığında bağlanabilir (MOD-0126 pack, Runtime Constraints
"Gerçek dağıtım açılmadan önce zorunlu kapı"):

1. Sağlayıcı ve arayüz seçimi (karar 0.1 seçenek B ya da C) ve **bankacılık sahibinin** adlandırılması.
2. Hesap sahipliği, Tenant/LE uygunluğu ve alacaklı–hesap eşleşmesi için sözleşmeli kontrol (MOD-0289, MOD-0140).
3. `Unknown` sonucu için Release'in iki aşamalı hâle gelmesi (`PaymentReleaseIntent`, Reconcile, sonuç olayı).
4. Banka kimlikli geri çağrı / güvenilir çalışan / ekstre akışlarının kendi sözleşmeleri (kaynak kapalı akışlar 2–4).
5. Kimlik bilgisi ve sertifika yönetimi (gizli bilgi deposu; değer asla sözleşmede ya da kayıtta).
6. Bu sözleşmenin yeni sürümü: `outcome` sözlüğüne yalnız ekleme; `dispatchKey` kuralı değişmez (K16).

## 7. Açık konular

| # | Konu | Sahibi |
|---|---|---|
| BD-1 | `rail` alanının 0.1'de tutulması (kaynakta var, kullanılmıyor) ya da çıkarılması | sahip (K1) |
| BD-2 | `releaseSequence` tanımı (kesin ret sonrası yeniden serbest bırakma yeni sıra mı?) — MOD-0126 pack ile hizalanır | Treasury CT |
| BD-3 | Bankacılık sahibi | sahip (K1) |
| BD-4 | Sözleşmenin `drafts/`'tan `docs/analysis/contracts/bank-dispatch.md` (FROZEN 0.1.0) olarak yayımlanması | CT incelemesi + sahip |

## 8. Değişiklik geçmişi

- 0.1.0 (2026-10-25) — ilk taslak; dağıtım kapalı (D5).
