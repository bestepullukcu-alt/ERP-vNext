---
record_id: MOD-0289-DL2-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/master-data-management/module-packs/MOD-0289-bank-master-bank-account-registry.md (status draft, approval_status not-approved, sha256 ff2c556f4e4ff076cea02a45d3d0f5d88adb995887baf90d031da1f606affd97 (pack ready-for-dev 1. tur revizyonu, 2026-10-28 — Y-12, D62–D65; önceki sürüm `aab4187d…` (sahip denetimi 5cfd67813 düzeltmesi + D65); D58/D59 sürümü `698e135e…`; D57 sürümü `5af910d6…`; D56 sürümü `5137e84f…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `ef905628…` içindir), ölçüm dal chore/ct/treasury-intake-ct-05 @ec063fee9 — çalışma ağacı temiz, dosya HEAD ile aynı; satır atıfları bu sürüme `:N@ec063fee9`)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur — `git ls-tree` / `git archive`; MOD-0289 kod alma izni D45/3 — D27/5 koşullarıyla; kaynak commit DL-1 hazırlık kaydıyla aynı)
predecessor: execution/domains/master-data-management/work-packs/MOD-0289-DL1-hazirlik.md
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve bu pack sürümü dahil; D69 main'de değil ve MOD-0289'a dokunmaz).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU numarası açık — K-1). Taslak main'de ama `draft` ve teyitsiz — pack'teki "Auth paket metni
> main'de" maddesi işaretsiz, paket kapısı açık (D53/1). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-2**'si (MOD-0289: 23 anahtar, 3 rol, 2 açık atama); kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **SEC-002:** ekin **DL-4**'ü (KARAR D56/1; Platform dosyaları için XMC-001 Beste — D63/2): paket metni paket kapısı, kodu
> DL-1 kabul kapısı; modül kodu değeri ekin K-1b'si. 2026-10-27 ölçümü: bugün çalışmaz (iki tarafta `product-item-sku-master` sabit).
> - **MOD-0023 yok** (D23/2; v1'de dört göz) — Y-12 şablon kodları bu pakete dokunmaz.
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **O-14** (grup önekli kayıtlarda — banka `BNK`, tedarikçi hesabı `BAC` — allocator'a gidecek `legalEntityId`; şirket hesabı
> `BAC` için sahip şirketin `LegalEntityId`'si önerisinin teyidi, D65/2) **Beste'de, cevap yok** (2026-10-28): DL-1 kabul kapısı,
> tedarikçi `BAC` kısmı DL-3 kabulünde; değer pakette seçilmez. AC-60 üç dilime bölündü (banka DL-1, şirket hesabı DL-2, tedarikçi
> hesabı DL-3).
> - **DL-2 kabul kapısı:** Treasury tüketici fikstür koşusu aranmaz (KARAR D57/1); `BANK-ACCOUNT-LOOKUP` kendi sözleşme testiyle
> (AC-20, AC-42).
> - **DL-3:** MOD-0140 1.3.0 `GET /api/suppliers/eligible` — Active ilişkili bütün tedarikçiler (KARAR D58); ölçüm: main'de
> `supplier.openapi.yaml` hâlâ `version: 1.2.0`.
> - **MDM domain-config** MOD-0289 cümlesi (D51/5) main'de (`domain-config.md:48`); pack'teki ilgili checklist maddesi henüz işaretsiz.
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol oldu; MOD-0289'un 23 anahtarı ve 3 rolü değişmedi.
>
> **Durum (2026-10-27).** İlk DL-2 hazırlık kaydı. Pack sürümü DL-1 kaydının bağlandığı sürümle aynıdır (`ef905628…`); bu sürüme
> CT-APR-02 satırı **var** (`execution/approvals/reviews.tsv:91` — UYGUN, 0 ENGEL), CT-APR-01 satırı **yok** (`approvals.tsv`'de
> MOD-0289 0 satır). DL-2, DL-1'in **kabulünden** sonra başlar (pack "Teslim dilimleri" DL-2 Önkoşulu, `:1904@ec063fee9`); bu kayıt
> DL-1'in paket kapısını yeniden ölçmez, yalnız DL-2'nin kendi dilim kapısını ("— DL-2 için", D51/1) ve DL-2'ye özgü kodu ölçer.
> Pack `ready-for-dev` revizyonunda değişirse atıflar o sürümde yeniden doğrulanır (S-53).

# MOD-0289 DL-2 hazırlığı — şirket banka hesapları, hassas veri ve okuma sözleşmesi

Bu kayıt, MOD-0289 DL-1 kabul edildiğinde DL-2 WP prompt'unun aynı gün yazılabilmesi için **kaynaktan ne alınacağını**, **neyin
uyarlanacağını**, **neyin alınmayacağını** ve DL-2'nin **dilim kapısını** önceden ölçer. Kod yazdırmaz; paketi, kuyruğu, onay /
inceleme kayıtlarını değiştirmez. Prompt, paketin `ready-for-dev` sürümüne ve DL-1 kabulünden sonraki Expected HEAD'e göre ayrıca
yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt kapısından geçer.

**DL-2 kapsamı** (pack "Teslim dilimleri", `:1901-1908@ec063fee9`): şirket sahipli banka hesabı (`BankAccount`, `OwnerType =
LegalEntity`), uygulama düzeyi AES-256-GCM şifreleme + HMAC tekillik özetleri + anahtar kümesi çiti ve onaylı hedef, maskeleme,
gerekçe kodlu maskesiz okuma (`BankAccountSensitiveRead`), dört göz aktivasyonu (D23/2) ve ayrı "Banka hesabı etkinleştirici"
rolü (D47/2), hesap amaçları (`BankAccountPurpose`, D24/6), MOD-0048 para birimi portu, SEC-002 veri kapsamı, `BANK-ACCOUNT-LOOKUP`
1.0.0 sözleşmesinin dondurulması. **Tedarikçi sahipli hesap DL-3**, ekranlar DL-4.

## 1. Başlama koşulları (ölçüm: çalışma ağacı @ec063fee9, 2026-10-27)

Kaynak: pack "Ready-for-dev Checklist" → "Dilim kapıları" (`:1643-1667@ec063fee9`), DL-2 Önkoşul metni (`:1904-1907`), "Bağımlılık
sözleşmeleri — kapanış tablosu" (`:620-637`). "DL-2'ye etkisi": **bekletir** = DL-2 kodu bu kapanmadan başlamaz; **kabul** = DL-2
başlamasını değil kabulünü bekletir; **—** = kapandı.

| Koşul | Sahibi | Bugün | DL-2'ye etkisi |
|---|---|---|---|
| DL-1 kabul edildi (dilim kabul kapısı, `:1926`) | DL-1 WP + CT | DL-1 başlamadı: MdmService'te `Bank*` varlığı / `Features/BankMaster` yok (`ls`); DL-1'in paket kapısı açık — CT-APR-01 yok, DCP-011 `status: draft` (`execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md:7`), Auth paket metni (MOD-0018 Treasury Auth eki taslağı #281 ile main'de, teyitsiz; ölçüm anında MOD-0018'de `mdm.bank*` 0), RCS-001 BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9477`) — paket kapısı değil, DL-1 kabul kapısı (D59/1), MDM domain-config cümlesi (2026-10-28 ölçümünde main'de; ölçüm anında yalnız bu dalda, `domain-config.md:46-48`), O-14 (Beste; DL-1 kabul kapısı) | bekletir (ilk ve en uzun engel; ayrıntı DL-1 kaydı §1) |
| CT-APR-02 bu pack sürümüne | Treasury CT + bağımsız inceleyici | **var** — `reviews.tsv:91` (`ef905628…`, UYGUN, 0). DL-1 kaydının §1'indeki "satır yok" bugün eskidir | — (paket kapısı; `ready-for-dev` revizyonunda yenilenir) |
| Şifreleme ve özet anahtarı kaynağı + döndürme (O-6) | sahip | **kapandı — D33/2–3** (`:1645-1646`); ürün adı ortam kurulumunda ölçülür | — (ortam ürünü kabul öncesi dev kaydına yazılır) |
| Güvenlik onayı (O-6) | sahip (K1) | **kapandı — D33/1** (`:1647-1648`); gerçek Mongo testleri DL-2 kabul kapısında | — / kabul |
| AC-50 çit yük testi eşiği | sahip | **kapandı — D33/4** (`:1649`, AC-50 `:1500-1505`: kayıp 0, çift 0, eski kümeli commit 0, yeniden deneme ≤ %5, p95 ≤ 500 ms, p99 ≤ 1,5 sn; makine yükü kayda — S-51) | kabul |
| `ApprovalRevision` / `ApprovalRef` kaynağı | sahip | **kapandı — D33/3** (`:1650`) | — |
| Hesap amacı kodu RCS-001 dışında (doğal anahtar) | sahip | **kapandı — D27/10** (`:1644`) | — |
| SEC-002 kapsam sağlayıcısının MOD-0289 anahtarlarıyla çalışması (`:1652`) | MOD-0018 Treasury Auth eki DL-4 (KARAR D56/1): Treasury yazar, MOD-0018 sahibi teyit eder; XMC-001 Beste (Platform, D63/2) + MDM sahibi (istemci) | **ölçüldü 2026-10-27: bugün ÇALIŞMAZ.** MDM istemcisi modül kodunu sabitliyor: `ModuleCode = "product-item-sku-master"` ve başka modül kodunu 400 `LEGAL_ENTITY_SCOPE_REQUEST_INVALID` ile reddediyor (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs:18,60-62,163-171@ec063fee9`). Platform ucu da yalnız aynı modül kodu + sabit `FirstConsumerPermissions` listesi (ürün / GSKU / LSKU / FG anahtarları) + yapılandırılmış `AllowedPairs` (en çok 38) kabul ediyor (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs:15,35-56,97-101,103-114@ec063fee9`). `mdm.bank-accounts.*` hiçbir listede yok. Pack'in "hata → boş kapsam" kuralıyla bu, kiracı geneli izni olmayan her kullanıcının boş liste görmesi demektir; AC-17 geçemez. Platform tarafının çözümleme anlamı (izin → şirket listesi) okunmadı — **kısmi inceleme** | **2026-10-28 (D56/1):** DL-2 dilim kapısı değil — ek DL-4 paket metni paket kapısı, kodu DL-1 kabul kapısı (DL-2 bunu DL-1 üzerinden bekler); modül kodu değeri ekin K-1b'si. Ölçüm anında: **bekletir** — Platform değişikliği (`services/Diten.Platform/**` korunan yol, platform-shared-services; MOD-0018-FU16/FU17 "permission onboarding" deseni) + MDM istemcisinde modül kodu değişikliği (`Infrastructure/Authorization/**` pack Repo Scope'unda yok — XMC-001 / D9 onayı). Modül kodu pack'te tanımsız (§5 bulgu 1) |
| Auth: `ExplicitGrantOnlyPermissions`'a `mdm.bank-accounts.read-unmasked` ve `.read-tenant-wide` (`:1655`) | Treasury yazar (MOD-0018 Treasury Auth eki DL-2), MOD-0018 sahibi teyit eder (D9) | **yok** — dosyada `mdm` 0 (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`, 95 satır, grep 2026-10-27) | bekletir (pack: dilim kapısı); AC-16 DL-2 Bitiş'te → kabul de bekler |
| MOD-0048 para birimi listesinin MDM'den tüketimi (`:1656-1660`) | DL-2 yazarı (port) · Beste (Platform ucu) | **ölçüldü, kapandı:** MdmService'te para birimi listesi okuyan istemci yok (`grep 'reference/currencies' services/Diten.MdmService` → 0); tek okuyucu `GatewayReferenceValueReader` (`…/Infrastructure/ReferenceData/GatewayReferenceValueReader.cs:16,18,30,34-49@ec063fee9` — `Gateway:BaseUrl`, çağıranın `Authorization` + `X-Tenant-Id` başlıkları iletilir, yol `/api/lookups/reference-data/consumable-sets/{set}/published-values`). Yeni port `ICurrencyReferencePort` (`Infrastructure/Currency/**`) aynı desenle `GET /api/lookups/reference/currencies`'e gider | — (port DL-2'de yazılır; erişilemez → 503 `MDM_BANK_CURRENCY_REFERENCE_UNAVAILABLE`, sabit liste yok) |
| MDM `ValidationBehavior` girilen değeri döndürüyor mu (`:1661-1665`) | — | **ölçüldü: döndürmüyor** — 400 gövdesi yalnız `error.ErrorMessage` listesi (`…/Application/Behaviors/ValidationBehavior.cs:25-31,46@ec063fee9`); `Response<T>` dışı yol da yalnız mesaj taşır (`:49`). Kalan koşul: doğrulayıcı mesajlarında `{PropertyValue}` yok (biçimlenmiş mesaj değeri taşırdı) | — / DL-2 kod incelemesi |
| `System.Security.Cryptography.AesGcm` hedef çerçevede (S-2) | Treasury CT (ölçüm) | **kısmen ölçüldü:** MdmService'in beş projesi `net8.0` (`services/Diten.MdmService/src/*/…csproj:3`); `AesGcm` çerçevenin parçası. Çalışma ortamında `AesGcm.IsSupported` ve CI imajındaki sağlayıcı ölçülmedi | kabul (AC-37) — isteğe bağlı S-2 sahip onayı ister |
| Çok anahtarlı kısmi tekil indeks (S-1) | Treasury CT (ölçüm) | ölçülmedi (pack `:500` "DL-2 başında gerçek replica set'te ölçülür") | kabul (AC-12, AC-35) |
| Gerçek replica set test altyapısı (AC-13, AC-35…AC-39, AC-43…AC-46, AC-48…AC-52) | — | **var:** `MdmTestMongo` tek düğümlü replica set + `enableTestCommands=1` başlatır (`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs:13,37,47,156,494@ec063fee9`; ikili `DITEN_TEST_MONGOD`, yönlendirme `MDM_TEST_MONGO`); `failCommand` bu bayrakla açılır | — (DL-2 suite'i kendi değişkenini ister — bellek notu "lane Mongo port traps") |
| Döndürme işinin sistem aktörü niyet yolunda (`:1162-1166`) | Treasury CT (ölçüm) | **kısmi:** `IAuditSystemActor` var (`…/Application/Contracts/Audit/IPlatformAuditForwarder.cs:52@ec063fee9`); niyet yolunda emsal `ApplyItemActivationDecisionCommand` (`…/Features/ItemMaster/Commands/ApplyItemActivationDecisionCommand.cs:20,35`) — `LocalAuditIntent.ActorId`'ye nasıl yazıldığı okunmadı | prompt öncesi CT ölçümü (bekletmez) |
| `BANK-ACCOUNT-LOOKUP` 1.0.0 sözleşme dosyası | CT (taslak) · DL-2 (dondurma) | taslak var: `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` (`x-status: DRAFT`) | kabul (AC-20, AC-42) — dondurma `docs/analysis/contracts/**` yazımıdır (Protected Path; prompt'ta açık izin gerekir, §4) |
| DL-2 kuyruk satırı | Treasury CT | **yok** — `CT-QUEUE.tsv`'de yalnız `WP-0289-DL1` (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:20`) | — (satır DL-1 kabulünden sonra CT turunda açılır; bu kayıt yazmaz) |

**DL-2'yi ilk bekleten:** (1) **DL-1 kabulü** — o da DL-1'in paket kapısındaki dış sahipli, tarihsiz maddeleri bekler (Auth paket metni — MOD-0018 Treasury Auth eki, taslak main'de, MOD-0018 sahibinin teyidi bekleniyor; ekin DL-4 SEC-002 paket
metni — D56/1; DCP-011 onayı — Beste; CT-APR-01; DL-1 kabulünde ayrıca BL-565, ek kodları ve O-14 — Beste). (2) **SEC-002 sağlayıcısı**
artık DL-2 dilim kapısı değil: KARAR D56/1 ile ekin DL-4'ü — paket metni paket kapısı, kodu DL-1 kabul kapısı (iki tarafta kod
değişikliği: Platform izin listesi + MDM istemcisinin modül kodu). (3) DL-2'ye özgü açık dilim kapısı: **Auth açık atama**
(`read-unmasked`, `read-tenant-wide` — ekin DL-2'si; Treasury yazar, MOD-0018 sahibi teyit eder). DL-2 kabulünde Treasury tüketici
fikstür koşusu aranmaz (KARAR D57/1).
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-2)

Kaynak MOD-0289 kodu `services/Diten.Platform/` altındadır (`P/` = `services/Diten.Platform/src/`); MDM'ye taşınmaz, kurallar MDM
düzenine yeniden yazılır. Kaynak commit DL-1 kaydıyla aynıdır (`da4abb20`). 2026-10-27'de `git archive` ile **yeniden ölçüldü**: DL-1
kaydındaki DL-2'ye de giren 9 dosyanın sha256 ilk 16 hanesi ve satır sayısı **aynı**; yeni ölçülen `Mod0289AccountCreateFingerprint.cs`
`999aa47c26f0df37` · 33. Karar dili DL-1 kaydıyla aynı: **al-uyarla** (D45/3; doğrula, MDM düzenine yeniden yaz, iz kaydına
`path@da4abb20 + sha256`), **yalnız referans**, **alma** ("madde N" = pack "Taşınmayacaklar", `:1785-1813@ec063fee9`), **sonraki
dilim**. DL-2'de al-uyarla alanı DL-1'den **dardır**: hesap modelinin büyük kısmı (zarf, özet, çit, maske türetimi, dört göz,
okuma kaydı) kaynakta yoktur ve Recovery'de yenidir.

**Özet:**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `LegalEntityBankAccountDraft`'ın iş alanları (`BankId`, `BranchId`, `LegalEntityId`, `CurrencyCode`, `AccountPurpose`, `PaymentUsage`, `AccountHolderName`, `AccountDisplayName` → `DisplayName`); hesap Create / Get / liste / Activate akışının adımları ve sahip / banka / şube uygunluk kontrolleri (`Mod0289BankService.cs:324-488`); hesap liste sorgusu iskeleti (repo `:306-369`, kaçışsız regex hariç); denetleyicinin şirket hesabı uçlarının şekli | `SensitiveBankDataAccessLog` (okuma kaydı fikri); `RecordDeniedSensitiveAccessAsync` / `GetSensitiveAccessLogsAsync` (`:774-860`) — liste şekli; kaynak testlerin hesap vakaları (davranış listesi) | `TenantId = Guid.Empty` (`:357`, `:384`), istemciden gelen `AccountLast4` / `MaskedAccountDisplay` (`:353-369`), `ProtectedAccountRef` dış referans modeli (madde 13), ayrı sahiplik bağı ve iki tip (madde 6), çok boyutlu durum (madde 15), sahip durumu kopyası (madde 5), token / ödeme referansı aktarma (madde 7), doğrudan değişiklik ucu (madde 8), `MOD0289_*` izin / hata literalleri ve `/api/v1/mod0289` (madde 10), Guid'in BSON string saklanması (madde 12), kanıt / vekâlet / doğrulama koşusu / muafiyet / varsayılan atama / içe aktarma (v1 dışı), F02 / PAIR (madde 3) |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/Diten.Platform.Domain/Entities/Mod0289/BankMasterEntities.cs` | `3ee3c4f67a756ecf` · 721 | **al-uyarla** (yalnız `LegalEntityBankAccountDraft` :41-106 iş alanları) | `Domain/Entities/BankAccount.cs : EntityBase, IAuditIntentAggregate` (pack "Entity Fields" → BankAccount, `:322-366`) | taban `TenantScopedEntity` alma (madde 4); `AccountLast4` / `MaskedAccountDisplay = "MASKED"` (:63-64) istemci alanı → **alma**, sunucu türetir; `ProtectedAccountRef` (:65) alma (madde 13) → `AccountIdentifierEnvelope` + `EncryptionKeyVersion`; `ApprovalStatus`, `PaymentEligibilityStatus`, `OwnerValidation*`, `ValidationState`, `EvidencePackId`, `MandateId`, `SubmitReady`, `SourceSystem`… (:71-103) alma (madde 5, 15; pack "Taşınmayan hesap alanları"); `OwnerType` + `SupplierId` tek belgede (madde 6 — iki tip birleşir; tedarikçi dalı DL-3) |
| aynı dosya, `SensitiveBankDataAccessLog` :544-571 | — | yalnız referans | `Domain/Entities/BankAccountSensitiveRead.cs` (yalnız ekleme; depo arayüzünde Update / Delete / Replace yok, AUD-001 §4.1) | kaynak `TokenRead` / `Decision` / `ConsumerModule` / `PermissionChecked` alanları alınmaz; Recovery alanları `BankAccountId`, `ActorId` (Guid `sub`), `ReasonCode` (1..64), `ReasonComment` (≤ 500, tanımlayıcı içeremez), `OccurredAt`, `Outcome = Revealed` (pack `:404-416`); kaynakta "reddedildi" kaydı vardı (`RecordDeniedSensitiveAccessAsync` :774) — Recovery'de reddedilen deneme kaydı **yok** (Follow-up "Merkezi ret kaydı") |
| aynı dosya, `SupplierPaymentDestinationDraft` :265-307 | — | sonraki dilim (DL-3) | `BankAccount` (`OwnerType = Supplier`) | — |
| aynı dosya, diğer 20 sınıf (içe aktarma, değişiklik talebi, token, kanıt, vekâlet, doğrulama, istisna, varsayılan atama, outbox) | — | alma / v1 dışı | — | madde 1, 7; D23/2 |
| `P/Diten.Platform.Domain/Entities/Mod0289/BankMasterEnums.cs` | `51f1254aed62f22b` · 213 | yalnız referans (`BankMasterLifecycleStatus` DL-1'de alındı; hesap aynı enum'u kullanır) | `BankLifecycleStatus` (DL-1) + yeni `BankAccountOwnerType`, `BankAccountIdentifierType`, `IdentifierCheckLevel` | onay / ödeme uygunluğu / sahip doğrulama boyutları alma (madde 15) |
| `P/Diten.Platform.Domain/Repositories/Mod0289/IMod0289BankRepository.cs` | `5af68246cabf3a41` · 210 | **al-uyarla** (yalnız :119-124 şirket hesabı Create / Get / liste / Activate imzaları) | `Domain/Repositories/I{BankAccount,BankAccountSensitiveRead}Repository.cs` | `GetOwnershipBindingByAccountIdAsync` (:122) alma (madde 6); kaynakta Update / Deactivate / Delete yok — sürüm süzgeçli koşullu güncelleme yeni |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/…Application/Features/Mod0289/Services/Mod0289BankService.cs` | `44617d01fb258eab` · 5353 | **al-uyarla, bölerek** (yalnız :324-488 şirket hesabı Create / Get / Activate / liste) | `Features/BankAccount/{Commands,Queries,Handlers,Validators,Rules}/**`, `BankAccountModels.cs` (pack "Backend File Convention", `:958-1014`) | Kaynak farkları (pack kazanır): para birimi `EnsureActiveReferenceValueAsync("CURRENCY_CODE")` ile referans kümesinden (:331) → Recovery `ICurrencyReferencePort` (MOD-0048 `currencies` ucu, D24/8); `TenantId = Guid.Empty` (:357, :384) alma (AC-26); `AccountLast4` / maske istemciden (:353, :368-369) alma → sunucu türetir (AC-14); Activate'te aktör karşılaştırması **yok** (:437-488) → dört göz yeni (D23/2, AC-18 dört göz kolu); Activate'te onay durumu ve sahiplik bağı (:443-448) alma; işlem sonrası `EmitGovernanceAuditAsync` (:469) alma → niyet aynı işlemde (AC-6); İngilizce serbest iletiler alma (resx anahtarı) |
| aynı dosya, :669-773 (token), :1249-1316 (doğrudan değişiklik), :2531-2900 (doğrulama, kanıt, vekâlet) | — | alma / v1 dışı | — | madde 7, 8; v1 dışı listesi |
| aynı dosya, :774-860 (okuma kaydı) | — | yalnız referans | `GetBankAccountSensitiveReadListQuery` | yalnız liste şekli; yazma yolu Recovery'de "değerden önce, aynı işlemde" (AC-15) |
| `P/…Features/Mod0289/Models/Mod0289BankModels.cs` | `f189b970caed2d0b` · 888 | yalnız referans (hesap modelleri) | `BankAccountModels.cs` (`BankAccountListItemDto`, `BankAccountDetailDto`, `BankAccountRevealDto`, `BankAccountWriteRequest`, lookup DTO) | DTO'larda tam tanımlayıcı alanı yalnız `BankAccountRevealDto`'da (mimari test); `TenantId` yok |
| `P/…Features/Mod0289/Services/Mod0289AccountCreateFingerprint.cs` | `999aa47c26f0df37` · 33 | **alma** | `Rules/BankAccountUniquenessKey.cs` + `Infrastructure/BankData/HmacUniquenessDigester.cs` (yeni) | F02 parmak izi (madde 3); Recovery tekillik anahtarı türe göre normalize + HMAC-SHA256, sürümlü (pack `:684-763`) |
| `P/…Features/Mod0289/Services/Mod0289PaymentReferenceHandoffService.cs` | `8fe015e359f0e688` · 445 | **alma** | — | madde 7 |
| `P/…Features/Mod0289/Mod0289Permissions.cs` | `7731c1e0f1869cbc` · 43 | **alma** | `mdm.bank-accounts.*`, `mdm.bank-account-purposes.*` (§2.6) | madde 10 |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/…Infrastructure/Persistence/Repositories/Mod0289/Mod0289BankRepository.cs` | `9e61cbcd4b3c5a97` · 2228 | **al-uyarla** (yalnız :196-305 Create / Get okuma şekli, :306-369 liste iskeleti, :681 Activate) | `Persistence/Repositories/{BankAccount,BankAccountSensitiveRead}Repository.cs` (+ `BankAccountPurposeRepository.cs`, `Infrastructure/BankData/BankDataKey{Fence,SetTarget}Repository.cs`) | kaynak Create bir oturum açıp hesap + sahiplik bağını birlikte yazar (:230) — bağ alma (madde 6); Recovery: hesap + niyet + kiracı çiti aynı işlemde (`MasterDataAtomicWrite`), `Version` CAS. Liste araması `MaskedAccountDisplay` dahil kaçışsız regex (:326-329) **alma** (DL-1'deki D51/4 kuralıyla aynı gerekçe; hesap listesinde arama kuralı pack'te yok — §4 açık soru). İndeks kurulumu kaynakta yok → pack "Mongo indeksleri" hesap / amaç / çit / okuma kaydı satırları (`:495-506`) yeni |

**DL-2 veritabanı indeksleri (pack `:489-506@ec063fee9`):** `ux_mdm_bank_account_code` (TenantId + Code, **kısmi filtre yok**),
`ux_mdm_bank_account_identifier` (TenantId + `UniquenessKeyDigests` çok anahtarlı, kısmi `IsDeleted = false` — S-1 ölçümü),
`ux_mdm_bank_data_key_fence_tenant`, `ix_mdm_bank_account_key_versions` (kiracısız yönetim sorgusu — yalnız sürüm adları),
`ix_mdm_bank_account_owner_le`, `ux_mdm_bank_account_purpose_code` (kısmi filtre yok), `ix_mdm_bank_account_purpose`,
`ix_mdm_bank_account_sensitive_read`. `ix_mdm_bank_account_owner_supplier` tanım olarak DL-2'de kurulabilir, testi DL-3 (öneri).
Sabotajlar: `ux_mdm_bank_account_identifier` kaldırılınca AC-12 / AC-35 kırmızı; `ux_mdm_bank_account_code`'a `IsDeleted=false`
eklenince silinmiş-kod vakası kırmızı.

### 2.4 Api ve frontend

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/Diten.Platform.API/Controllers/Mod0289Controller.cs` | `7ba4e18ed40c2d45` · 2451 | **al-uyarla** (yalnız :241-444 şirket hesabı Create / liste / detay / şirkete göre liste / activation uçlarının şekli) | `Api/Controllers/BankAccountsController.cs`, `BankAccountPurposesController.cs` | rota `/api/v1/mod0289/legal-entity-accounts` alma → `/api/mdm/bank-accounts`, `…/{id}/activate`, `…/{id}/deactivate`, `…/{id}/unmasked-reads`, `…/lookup-validation`, `…/sensitive-reads` (pack "Kaynak işlemleri" / "Recovery ekleri", `:204-252`); token uçları (:1058-1215) alma (madde 7); `sensitive-access-logs` (:1216) yalnız referans; `direct-mutation` (:2249) alma (madde 8); doğrulama / kanıt / vekâlet uçları (:445-830) v1 dışı; eylem süzgeci kimlik koruması alma (madde 4) |
| `P/Diten.Platform.API/Models/Mod0289/Mod0289Contracts.cs` | `f440007a804b6ad5` · 467 | yalnız referans (hesap istekleri) | `BankAccountWriteRequest` | istekte `AccountLast4`, `MaskedAccountDisplay`, `ProtectedAccountRef` alma; tam tanımlayıcı yalnız Create isteğinde, saklanmaz |
| `frontend/Diten.Web/**` BankMasterRegistry, `mod0289-*-ui.test.js` | — | sonraki dilim (DL-4) | — | madde 14 |

### 2.5 Testler (yalnız referans — davranış listesi; Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-2'de karşılığı |
|---|---|---|
| `services/Diten.Platform/tests/Diten.Platform.API.Tests/Mod0289ContractBootstrapTests.cs` | `324b62a7563857d6` · 4075 | yalnız şirket hesabı Create / Activate / liste vakaları → AC-10, AC-18, AC-19 davranış fikri; kaynakta şifreleme, maske türetimi, dört göz, çit testi **yok** — AC-13…AC-15, AC-33…AC-52 karşılıksız, tamamen yeni |
| F02 / PAIR / ödeme referansı / kimlik / seed testleri | DL-1 kaydı §2.5 | alma — güvence olarak alıntılanmaz |

### 2.6 Yetki, roller ve denetim — DL-2 (D23/2, D23/4, D46–D50)

Pack "Authorization Convention" (`:1156-1200@ec063fee9`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-405@ec063fee9`; D46 tablosu tarihçedir, uygulanmaz — D49/5).

- **DL-2 anahtarları:** `mdm.bank-accounts.read` (T1), `.read-tenant-wide` (T3, yalnız açık atama, yalnız okuma, maskeli), `.create`,
  `.update` (T1), `.activate`, `.deactivate` (T2, + dört göz), `.delete` (T1), `.bulk-delete` (T2), `.read-unmasked` (T3, yalnız
  açık atama, gerekçe kodu, değerden önce denetim), `.sensitive-reads.read` (T1, dört parçalı); `mdm.bank-account-purposes.read`,
  `.create`, `.update`, `.delete` (T1), `.activate`, `.deactivate` (T2). Kaynak `MOD0289_*` anahtarları alınmaz (madde 10).
- **Roller (matris bölüm 3, `:192-198`):**

  | Rol | DL-2 anahtarları | DL-2 test kişisi olarak |
  |---|---|---|
  | Banka verisi görüntüleyici | `mdm.banks.read`, `mdm.bank-accounts.read`, `mdm.bank-account-purposes.read` | maskeli liste / detay / lookup; maskesiz okuma 403 (AC-15) |
  | Banka verisi yöneticisi | görüntüleyici + hesap `create`, `update`, `delete`, `bulk-delete`, `sensitive-reads.read`, amaç `create`, `update`, `delete`, `activate`, `deactivate`. **Yok:** hesap `activate` / `deactivate` (D47/2) | hesap oluşturan / güncelleyen; Activate denemesi **403 izin** (dört göz kodu değil — iki 403 ayrı kodla test edilir) |
  | Banka hesabı etkinleştirici (**D47/2**) | görüntüleyici + hesap `activate`, `deactivate` | hesabı oluşturmamış / son güncellememiş kişi → 200; **aynı kişide iki rol** + oluşturan → 403 `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED` (D23/2, çalışma anı; Guid karşılaştırması, büyük / küçük harf farklı `sub` — Test Expectations `:1554-1557`) |
  | Açık atama (rol yok) | `mdm.bank-accounts.read-unmasked`, `.read-tenant-wide` (matris bölüm 1, `:140-141`) | maskesiz okuma: izin + gerekçe + kapsam → 200 `Cache-Control: no-store` + tek okuma kaydı + tek niyet değerden önce; kiracı geneli: iki şirketi maskeli görür, kapsamsız şirkette yazma 404 (AC-17) |

- **Dört göz yalnız banka hesabında** (matris "örtüşmeler" notu, `:400-403`): amaç oluşturma + etkinleştirme aynı yöneticide; amaç
  testleri dört göz beklememeli.
- **D47/4:** finans rollerine `mdm.banks.read` / `mdm.bank-accounts.read` verilmez (`:340-356`); v1'de `BANK-ACCOUNT-LOOKUP`'ı okuyan
  finans akışı yok. DL-2'nin lookup tüketicisi yalnız sözleşme testidir (bkz. §5 bulgu 3).
- **Kayıt kodu:** hesap `BAC`, şirket hesabında şirket kısa kodu öneki (D24/4; `.antigravity/rules/record-coding-standard.md` §3, D30);
  amaç kodu doğal anahtar (D27/10). RCS servisi DL-1'de bağlanır (BL-565).
- **Denetim:** `AuditCategory.MasterData` (14, D48/1); `AuditAggregateType`'a DL-2'de `BankAccount`, `BankAccountSensitiveRead`, çit /
  hedef ve amaç değerleri eklenir (DL-1 yalnız `Bank`, `BankBranch` — D51/4); hesap yazmaları ve maskesiz okuma **K2** (D24/2, D27/11),
  `mdm-guvenilir-kaynak-niyeti` yolu, CT-DECISIONS satırı DL-1'de açıldıysa DL-2 yalnız defter satırı ekler.

## 3. Kaynakta olmayan, DL-2'de yeni yazılacaklar

Yollar pack "Backend File Convention" (`:958-1014`), "Repo Scope" (`:541-582`) ve DL-2 satırı (`:1901-1908`); hepsi
`services/Diten.MdmService/` altında; yeni servis ve port yok (MDM 5059, D20/1).

- `Domain/Entities/{BankAccount,BankAccountSensitiveRead,BankAccountPurpose,BankDataKeyFence,BankDataKeySetTarget}.cs`,
  `Domain/Enums/{BankAccountOwnerType,BankAccountIdentifierType,IdentifierCheckLevel}.cs`, depo arayüzleri.
- `Features/BankAccount/**`: Create, Update, Activate (dört göz), Deactivate, Delete, BulkDelete, `RevealBankAccountIdentifierCommand`,
  iş-yalnız `RotateBankAccountProtectionCommand`, `AdvanceBankDataKeyFenceCommand`, `ApproveBankDataKeySetTargetCommand`; sorgular
  `GetBankAccountListQuery`, `GetBankAccountByIdQuery`, `GetBankAccountSensitiveReadListQuery`, `ValidateBankAccountReferenceQuery`;
  `Rules/{IbanRules,BicRules,DomesticAccountRules,BankAccountUniquenessKey}.cs` (saf işlevler). **Bu dilimde yalnız şirket sahibi
  dalı**; tedarikçi dalı `OwnerType = Supplier` → 400 / "bu dilimde yok" (prompt'ta açık yazılır).
- `Features/BankAccountPurpose/**` (çekirdek altı kod sabit, kiracı kodları, 7 dil etiket — AC-53…AC-57).
- `Infrastructure/BankData/**`: `AesGcmBankAccountIdentifierProtector` (biçim baytı `0x01` ‖ 12 bayt nonce ‖ şifreli metin ‖ 16 bayt
  etiket; AAD = kiracı + hesap + sürüm), `HmacUniquenessDigester`, `BankDataKeyOptions` (`Mdm:BankData:EncryptionKeys` /
  `Mdm:BankData:HashKeys`, sürümlü), `BankDataKeyStartupValidator` (anahtar yoksa servis açılmaz, AC-38), `BankDataKeySetId`, çit ve
  hedef depoları; `Api/Services/BankData/BankAccountProtectionRotationWorker.cs` (`Mdm:BankData:Rotation:Enabled` varsayılan `false`).
- `Infrastructure/Currency/**`: `ICurrencyReferencePort` — `GatewayReferenceValueReader` deseni (§1).
- `IBankAccountDataScopeSource` → `ITrustedLegalEntityScopeProvider` (§1 SEC-002 — bugün çalışmaz; sağlayıcı ve istemci değişikliği
  bu WP'nin İzinli yollarında değil).
- `Api/Controllers/{BankAccounts,BankAccountPurposes}Controller.cs`; `BankMasterManifestProvider`'a hesap / amaç anahtarları ve sayfaları.
- Denetim izi ekleri (yalnız ekleme): `AuditAggregateType` DL-2 değerleri, `MasterDataAuditOperation` değerleri, teslim deposu koleksiyon
  listesi (`mdm_bank_accounts`, `mdm_bank_account_sensitive_reads`, `mdm_bank_account_purposes`, çit / hedef); defter K2 satırları.
- `BANK-ACCOUNT-LOOKUP` 1.0.0'ın dondurulması (taslaktan `docs/analysis/contracts/` altına — yol prompt'ta ölçülerek yazılır; Protected
  Path istisnası açık izinle).
- Hata sözlüğü DL-2 alt kümesi (pack `:512-539`): `MDM_BANK_ACCOUNT_*`, `MDM_BANK_INACTIVE`, `MDM_BANK_CURRENCY_*`,
  `MDM_BANK_PROTECTION_*`, `MDM_BANK_KEYSET_*`, `MDM_BANK_DOMESTIC_*`, `MDM_BANK_IBAN_COUNTRY_FORMAT_*`, `MDM_BANK_ACCOUNT_PURPOSE_*`;
  tedarikçiye özgü `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE` (MOD-0140) ve `MDM_BANK_ACCOUNT_RELATIONSHIP_REQUIRED` DL-3; ileti resx anahtarı.

## 4. DL-2 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir; prompt'un içermesi gerekenlerin listesidir. Biçim örneği
`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri; pack'te yok):
`execution/domains/master-data-management/work-packs/prompts/WP-0289-DL2-v1.0.md`. Kuyrukta `WP-0289-DL2` satırı **yok** (CT
DL-1 kabulünden sonra açar — bu kayıt kuyruğa yazmaz).

**Başlık satırları**

| Alan | Değer (taslak) | DL-2 dağıtım günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` (+ `security-agent` salt okunur inceleme — öneri, HIGH / hassas veri) | — |
| WP / Prompt | `WP-0289-DL2` · `TRM-WP-0289-DL2-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/mdm/wp-0289-dl2` — **öneri — D51/3 / D52/1 deseni, sahip teyidi gerekir** (D51/3 yalnız DL-1 dalını adlandırdı) | Expected HEAD = DL-1 kabulünün main'deki commit'i (S-53); MdmService commit SHA'sı (kapanış tablosu "DL-2 başında"); worktree yolu |
| Risk | HIGH — §9.1 sinyalleri: hassas finansal veri, şifreleme, kiracı, veri kapsamı, denetim, dört göz, yetki | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kod alma | **al-uyarla** — §2 satırları, D45/3; her dosya `path@da4abb20 + sha256` WP raporunda | — |
| Kapsanan dilim | **yalnız DL-2** (tedarikçi hesabı, MOD-0140, ekranlar, Gateway **yok**) | — |

**DL-2 Bitiş AC'leri** (pack `:1906-1908@ec063fee9`): AC-3 (hesap kısmı), AC-4 (hesap kısmı), AC-5 (hesap kısmı), AC-6 (hesap
kısmı), AC-8 (hesap kısmı), AC-9 (hesap kısmı), AC-10, AC-11, AC-12, AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-19, AC-20,
AC-21, AC-22, **AC-26 (şirket hesabı kısmı)**, AC-33, AC-34, AC-35, AC-36, AC-37, AC-38, AC-39, AC-40, AC-42, AC-43, AC-44,
AC-45, AC-46, AC-47, AC-48, AC-49, AC-50, AC-51, AC-52, AC-53, AC-54, AC-55, AC-56, AC-57. AC-27 (hesap / aktivasyon / maskesiz okuma
kısmı) ve AC-41 DL-3'tedir. AC metinleri pack'ten tırnakla alıntılanır (AC-1…AC-59 `:1288-1551@ec063fee9`).

**Dilim kabul kapısı — DL-2** (`:1927`): Bitiş AC'leri; gerçek replica set AC'leri (AC-12, AC-13, AC-15, AC-35…AC-39, AC-43…AC-46, AC-48…AC-52 — pack D56 revizyonu,
§5 bulgu 4 kapandı)
ve **AC-50 yük testi** D33/4 eşiğine göre (`scripts/run_record.py` kaydı, makine yükü); tüketici kanıtı: `BANK-ACCOUNT-LOOKUP`
sözleşme testi (AC-20, AC-42); "en az bir Treasury tüketicisinin fikstür koşusu" şartı **KARAR D57/1** ile kalktı (tüketici testi ilk
gerçek tüketicinin dilimine girer — §5 bulgu 3 kapandı); build.

**İzinli yollar** (pack Repo Scope + Backend File Convention, DL-2): `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{BankAccount,BankAccountSensitiveRead,BankAccountPurpose,BankDataKeyFence,BankDataKeySetTarget}.cs`,
`Domain/Enums/{BankAccountOwnerType,BankAccountIdentifierType,IdentifierCheckLevel}.cs`, `Domain/Repositories/I{BankAccount,BankAccountSensitiveRead,BankAccountPurpose}Repository.cs`,
`Application/Features/{BankAccount,BankAccountPurpose}/**`, `Persistence/Repositories/{BankAccount,BankAccountSensitiveRead,BankAccountPurpose}Repository.cs`,
`Infrastructure/{BankData,Currency}/**`, `Api/Services/BankData/BankAccountProtectionRotationWorker.cs`,
`Api/Controllers/{BankAccounts,BankAccountPurposes}Controller.cs`, `Api/ModuleRegistration/BankMasterManifestProvider.cs` (ekleme);
denetim izi eklemeleri (`AuditAggregateType.cs`, `MasterDataAuditOperation`, `AuditIntentDeliveryRepository.cs` — yalnız ekleme);
`services/Diten.MdmService/tests/**`; `tests/architecture/audit-ledger/Diten.MdmService.md` (defter satırları); `BANK-ACCOUNT-LOOKUP`
dondurulmuş dosyası (tek dosya, yol prompt'ta). Hangi dosyanın hangi pack satırından geldiği prompt'ta tabloyla verilir (Repo Scope ve
DL-2 "Yollar" farkı — §5 bulgu 2).

**Korunan yollar:** `.antigravity/**`, `AGENTS.md`, `gateway/**` (`ocelot.json` yalnız integration-agent), `services/Diten.Platform/**`
(SEC-002 sağlayıcısı dahil), `services/Diten.AuthService/**` (`ExplicitGrantOnlyPermissions` — MOD-0018 Treasury Auth eki DL-2; Treasury yazar, MOD-0018 sahibi teyit eder), `services/Diten.ProcurementService/**`,
`services/Diten.Building.Blocks/**`, Treasury servisi ve diğer servisler; `services/Diten.MdmService/**` içinde İzinli yollar dışı her şey —
özellikle `Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs` (SEC-002 istemcisi; değişikliği ayrı onay,
XMC-001), MOD-0220 `LegalEntity` (yalnız okunur), MOD-0290 ürün ailesi, DL-1 banka / şube dosyaları (yalnız okunur; hata varsa dur ve
sor); `frontend/**` (DL-4); `docs/analysis/contracts/**` (lookup dosyası dışında); `execution/**`; kaynak depo.

**Önce oku:** pack (`ready-for-dev` sürümü), AGENTS.md, MDM domain-config, `.antigravity/rules/` multi-tenancy, security-jwt,
permission-key-standard, data-scope-enforcement, audit-trail-standard, record-coding-standard, mongo-indexing, handler-design,
code-style, git-safety, cross-module-change-consent, module-self-registration-standard; karar kayıtları D1, D9, D20, D23, D24, D27,
D33, D45–D69 (`treasury-intake-owner-decisions-0{1..5}.md`; özellikle D56/1, D57/1, D59/1, D65/2), MOD-0018 Treasury Auth
eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`, DL-2 ve DL-4); DL-1 hazırlık kaydı ve DL-1 WP raporu; bu kayıt.

**Sözleşme yüzeyleri:** API `/api/mdm/bank-accounts` (+ `{id}`, `activate`, `deactivate`, `bulk`, `unmasked-reads`, `lookup-validation`,
`sensitive-reads`), `/api/mdm/bank-account-purposes`; tüketilen MOD-0048 `GET /api/lookups/reference/currencies` (sahte istemci +
dev ortamında TRY / EUR / JPY), SEC-002 sağlayıcısı (sahte + dev ortamında iki şirket iki kullanıcı); sahip olunan `BANK-ACCOUNT-LOOKUP`
1.0.0 (F-E1…F-E9, F-503).

**Gerekli testler** (pack "Test Expectations", `:1552-1581`): birim — IBAN (MOD 97-10, TR / DE / GB / FR, ülke uzunluğu yanlış vektör),
yurt içi kurallar, tekillik anahtarı, AES-GCM zarfı (10 000 şifrelemede nonce tekilliği, AAD, kurcalama — AC-37), özet çift yazma,
maske / son 4 türetimi, dört göz, lookup sınıflandırıcısı; **gerçek replica set** (kendi URI değişkeni, varsayılan porta düşmez,
`enableTestCommands`, sınıf başına veritabanı — `MdmTestMongo` deseni) — AC-12, AC-13 (ham belge + bütün indekslerde düz metin yok),
AC-15 (okuma kaydı + niyet değerden önce aynı işlemde), AC-17, AC-35…AC-39, AC-43…AC-46, AC-48…AC-52; **AC-50 yük testi** (≥ 8 yazar, 10
dk, çit ilerletmesi); günlük yakalama (tanımlayıcı yokluğu, AC-14); mimari (`[HasPermission]`, DTO'da tam tanımlayıcı yalnız
`BankAccountRevealDto`, `AuditTrailStandard`); build 0 hata; yerel kapıda `scripts/` Python testleri. Sabotajlar: AC-9 (`Version`),
AC-15 (okuma kaydı zorla başarısız → 503, değer yok), AC-17 (kapsam süzgeci / kiracı geneli yazmaya sızma), dört göz kontrolü
kaldırılınca, AC-39 (eski nonce), AC-43…AC-52 (çit / hedef). Bellek içi sahte depo replica set AC'lerini kapatmaz.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş**: hedef servis + proje yolu; aggregate / entity + alanlar (pack "Entity Fields" BankAccount,
  BankAccountSensitiveRead, BankAccountPurpose, çit, hedef); komut / sorgu listesi (§3); validator kuralları (pack "Validation Rules"
  hesap satırları; `{PropertyValue}` yasak); izin anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`; SEC-002 sağlayıcısı ya da
  Auth değişikliği gerekirse dur ve bildir (başka modül — XMC-001).

**Kart kuralları:** **S-63** — "push ve PR yok; commit sonrası dur ve bildir" (dal adı, HEAD sha, test sonucu); S-52 (prompt > ~10 bin
token → yol + SHA-256); S-54 (`prompt_gate.py review-check` commit'ten önce); S-83 (HIGH — kabul kanıtı betiğini CT yazar, kırmızı taban);
S-53 (Expected HEAD DL-1 kabulünden sonra; pack değişirse yeniden tabanlama); S-51 (AC-50 makine yükü); dalga sonunda tek PR (S-76,
S-77). Rapor biçimi SOP §22. Kodu kimin yazacağı (MDM sahibi Ali — Y-2) dağıtımdan önce netleşmeli.

**Açık sorular (prompt'tan önce CT / sahip):** (1) SEC-002 sağlayıcısının değişikliğini kim, hangi pakette yapar ve MOD-0289'un modül kodu
nedir (§5 bulgu 1); (2) hesap listesinde arama kuralı (banka için D51/4 var; hesapta `MaskedDisplay` / `DisplayName` araması — pack'te
kural yok; öneri: aynı `Regex.Escape` + 100 karakter kuralı, tam tanımlayıcı araması yok); (3) DL-2 kabul kapısındaki "Treasury
tüketicisi fikstür koşusu" v1'de okuyan akış yokken nasıl karşılanır (§5 bulgu 3); (4) S-1 / S-2 isteğe bağlı ön doğrulama yapılsın mı.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynak düzeltmesi yok. Kaynak ve Recovery testleri **çalıştırılmadı**;
  Recovery'de yalnız dosya okuma ve `grep` (@ec063fee9).
- SEC-002'nin Platform tarafında izin → şirket çözümleme anlamı okunmadı (yalnız izin listesi) — **kısmi inceleme**.
- Ortamdaki gizli depo ürünü, `AesGcm.IsSupported`, çok anahtarlı kısmi indeks davranışı ölçülmedi (kabul kapısında).
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi; `ready-for-dev` revizyonunda CT işi — düzeltme yapılmadı):**

1. **SEC-002 "ölçüm" maddesi aslında kod değişikliği:** Checklist `:1652` ve Dependencies `:608` sağlayıcıyı "var, MOD-0289
   anahtarlarıyla çalıştığı ölçülmedi" diye yazıyor; kapanış tablosu (`:632`) yalnız AC-17 + dev kaydı istiyor. Ölçüm: MDM istemcisi
   (`PlatformTrustedLegalEntityScopeProviderClient.cs:18,170`) ve Platform doğrulayıcısı (`TrustedLegalEntityScopeCredentialAuthenticator.cs:35-56,97-114`)
   modül kodunu `product-item-sku-master`'a ve sabit bir izin listesine bağlıyor. Gerekenler pack'te yok: (a) Platform değişikliği
   (korunan yol, platform-shared-services; muhtemelen bir MOD-0018 FU "permission onboarding" paketi — FU16 / FU17 deseni), (b) MDM
   istemcisinde modül kodu (Repo Scope'ta `Infrastructure/Authorization/**` yok), (c) MOD-0289'un **modül kodu** (izin atfı / kiracı
   yetkisi / kapsam isteği için) pack'te tanımsız. Aynı bulgu MOD-0217 için de geçerlidir (paket kapısında). **Kapandı — KARAR D56/1** (MOD-0018
   Treasury Auth eki DL-4; modül kodu değeri ekin K-1b'si).
2. **DL-2 "Yollar" listesi Repo Scope'tan dar:** `:1901-1902` yalnız `{BankAccount,BankAccountSensitiveRead}`, `Features/BankAccount/**`,
   `Rules/`, `Infrastructure/{BankData,Currency}/**`, `BankAccountsController` ve lookup dosyasını sayıyor; Repo Scope (`:574-578`) amaç
   arka ucunu (`Features/BankAccountPurpose/**`, `BankAccountPurpose.cs`, depo, denetleyici) "DL-2" diye yazıyor, AC-53…AC-57 DL-2 Bitiş'te;
   `BankAccountProtectionRotationWorker.cs` (`:553`) ve çit / hedef varlıkları (`BankDataKeyFence`, `BankDataKeySetTarget` — "Entity Fields"
   `:418-488`, Backend File Convention `:996-1000`) Repo Scope'un Domain listesinde yok; `IdentifierCheckLevel` enum'u (`:338`) Repo
   Scope enum listesinde (`:546`) yok. **Kapandı** (pack D56 revizyonu).
3. **DL-2 kabul kapısı ile D47/4 arasında gerilim:** `:1927` "en az bir Treasury tüketicisinin fikstür koşusunun kendi sahibince kaydı"
   istiyor; D47/4 ölçümü (`treasury-intake-owner-decisions-05.md:340-356`) v1'de hiçbir finans akışının MOD-0289'u okumadığını, tüketici
   alanlarının opak Guid olduğunu yazıyor. Tüketici fikstür koşusunun hangi pakette, hangi WP'de yapılacağı tanımsız — DL-2 kabulü dış
   sahibe bağlanmış oluyor (sahip kararı gerekir: kapıdan çıkarmak ya da tüketici WP'sini adlandırmak). **Kapandı — KARAR D57/1**
   (şart kapıdan çıktı; tüketici testi ilk gerçek tüketicinin diliminde).
4. **Kabul kapısının gerçek replica set listesi Test Expectations'tan dar:** `:1927` AC-12'yi (aynı IBAN yarışı; Test Expectations
   `:1560` "tekil indeksler ve yarış (AC-2, AC-9, AC-12)") ve AC-15'i ("aynı işlemde" okuma kaydı + niyet) saymıyor. **Kapandı** (pack D56 revizyonu).
5. **Dependencies "Şifreleme altyapısı" satırı eskidi:** `:610` "`AesGcm` hedef çerçevede kullanılabilirliği ölçülmedi" — hedef
   çerçeve bugün ölçüldü (`net8.0`, beş csproj); kalan yalnız çalışma ortamı desteği. **Kapandı** (pack D56
   revizyonu).
6. **Dört göz AC kimliği:** Validation Rules `:1093-1097` ve Failure Path `:1147` dört göz kuralını yazıyor; kuralın sabotajlı testi
   `:1345-1347` (AC-18 bloğunun içinde, D23/2) — DL-2 Bitiş'te AC-18 var, ama dört göz ayrı AC kimliği taşımıyor; prompt'ta AC-18'in
   dört göz kolu açıkça adlandırılmalı. **Kapandı** (pack D56 revizyonu: DL-2 Bitiş'te
   adlandırıldı).
7. **DL-1 kaydı eskidi (pack değil):** `MOD-0289-DL1-hazirlik.md` §1 "CT-APR-02 … satır yok" diyor; bugün `reviews.tsv:91` UYGUN. **Kapandı** (DL-1 kaydı 2026-10-28'de
   tazelendi).
