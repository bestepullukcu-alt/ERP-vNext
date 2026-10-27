---
record_id: MOD-0126-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
refreshed_at_local: 2026-10-27
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (status draft, sha256 91090dd64f9103f3b559c0ecb321eb696a8be1404122b00d383428d3494713cc, main @904dfd5c2)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5)
---

> **Güncel durum (2026-10-27).** Bu kayıt pack'in main @904dfd5c2 sürümüne bağlıdır (sha256 `91090dd6…13cc`, `status: draft`;
> bu sürüme CT-APR-02 ve CT-APR-01 satırı yok). 2026-10-26 sürümlerine (`684e89d6…`, `94d3ad93…`) yapılan "pack satır N" atıfları
> geçersizdir; bu kayıt pack'e bölüm adıyla ya da `yol:satır@904dfd5c2` ile atıf yapar. Pack `ready-for-dev` revizyonunda yeniden
> değişirse atıflar o sürümde yeniden doğrulanır (S-53).

# MOD-0126 DL-1 hazırlığı — servis iskeleti, parti çekirdeği ve denetim altyapısı

Bu kayıt, MOD-0126 `ready-for-dev` olduğunda DL-1 WP prompt'unun aynı gün yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

## 1. Başlama koşulları (ölçüm: main @904dfd5c2, 2026-10-27)

Kaynak: pack "Teslim dilimleri" DL-1 önkoşulu (`MOD-0126-payments-approvals.md:1557-1559@904dfd5c2`) ve "Ready-for-dev
Checklist" işaretsiz maddeleri (`:1398-1420@904dfd5c2`). "DL-1'e etkisi" sütunu: **bekletir** = DL-1 kodu bu kapanmadan
başlamaz; **DL-2/DL-3** = pack'e göre sonraki dilimin önkoşulu.

| Koşul | Sahibi | Bugün | DL-1'e etkisi |
|---|---|---|---|
| Pack `ready-for-dev` + o sürüme CT-APR-02 (bağımsız inceleme) | Treasury CT + bağımsız inceleyici | `status: draft`; güncel sha `91090dd6…` için `execution/approvals/reviews.tsv`'de satır yok (son MOD-0126 incelemesi `8eab5e3c…`, `reviews.tsv:82`) | bekletir |
| CT-APR-01 sürüme bağlı onay satırı (`high: evet`, paketi yazmamış onaylayıcı; D34/2: taslak paketler tek turda) | Beste (onaylayıcı) | `execution/approvals/approvals.tsv`'de MOD-0126 satırı yok | bekletir |
| DCP-011 `approved` | Beste | dosya `execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md` var, `status: draft` (`:7`); numara atandı (Y-4/2), onay `ready-for-dev` turunda | bekletir |
| TRM-14 (a) MOD-0220 §22 pack onayı | Beste (onay verildi) | **kapandı** — `approvals.tsv:17` (MOD-0220 `91248a47…`) | — |
| TRM-14 (b) `lookup-validation` yanıtında `ApprovalStatus` | Ali (kod MOD-0023 kulesinin sırasında, Y-5/3) | **yok** — `LegalEntityLookupDto` alanları `LegalEntityId, Code, LegalName, DisplayName, LifecycleState, Referenceable` (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106@904dfd5c2`); işleyici `Active` değilse 404 (`…/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-23@904dfd5c2`) | bekletir (AC-2, AC-39) |
| TRM-14 (c) MOD-0220 sözleşme belgesi | Ali | taslak var: `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` (LEGAL-ENTITY-LOOKUP 1.1.0); dondurulmadı | bekletir (AC-39 sözleşme testi bu sürüme) |
| O-9 RCS-001 ortak kod servisi (BL-565) + çağrı sözleşmesi | Beste (D44) | **yok** — `services/Diten.Building.Blocks/src/` altında kod servisi yok, `services/**`'te `IRecordCodePort` / kod servisi istemcisi 0; BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9473-9477@904dfd5c2`); çağrı sözleşmesi gelmedi (Y-4). Not: backlog satırında sahip hâlâ "CT (Platform + MDM …)" yazıyor, D44 (Beste) ile uyumsuz | bekletir (Create yazılamaz) |
| `PAY` öneki RCS-001 §3'te | — | **var** — `.antigravity/rules/record-coding-standard.md:63@904dfd5c2` (D29/2, D30) | — |
| Platform `AuditCategory`'ye `Finance = 18` (D35/3, Y-4/7) | Beste | **yok** — enum `Unknown = 0 … ESignature = 17` (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-27@904dfd5c2`); kopya `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs` son değer `ESignature = 17` (`:29`), `Finance` yok | bekletir (pack: "DL-1 bu değer main'e girmeden başlamaz") |
| O-11 MOD-0048 `currency-minor-units` seed + izin listesi | Beste (Y-4/3: başlangıç listesi kabul) | **yok** — `services/Diten.Platform/**`'te `currency-minor-units` geçmiyor; `BusinessReferenceData:ConsumableSets` listesinde yok (`services/Diten.Platform/src/Diten.Platform.API/appsettings.json:104-…@904dfd5c2`) | bekletir (Create; yoksa 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE`) |
| O-13 MOD-0048 `payment-method` kümesi seed + izin listesi | Beste (Y-4/3) | **yok** — aynı ölçüm, `payment-method` 0 | bekletir (Create; yoksa 503 `PAYMENT_METHOD_SOURCE_UNAVAILABLE`) |
| O-19 SEC-002 tüketim yolu | Beste (yol), Ali (Auth) | yol **belli** (Y-4/6: servis içi `IFinanceTreasuryDataScopeSource` + Platform `IDataScopeResolver` bağdaştırıcısı, fail-closed). Açık: `read-tenant-wide`'ın Auth'ta açık atamalı tanımı — `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`'te `finance-treasury.*` anahtarı yok (grep 0). "Kapsamı soran kod + kanıt testi" DL-1'in kendi işidir (AC-32) | bekletir (pack checklist maddesi); bkz. not 2 |
| MOD-0018 Auth: rol şablonları (D46 + D47 + D49 + D50 matrisi) ve açık atamalı anahtarlar | Ali (Y-6: önce MOD-0018 paketi, sonra kod; numara ve tarih yok) | Treasury rolleri için MOD-0018 paketi depoda yok (`execution/domains/*/module-packs/MOD-0018-*` yalnız FU9…FU22); Auth'ta anahtar yok | DL-1 kodunu doğrudan bekletmez (izin anahtarları servis manifestinden kaydolur; testler izni token'a koyar — **çıkarım**, pack'te yazılı değil); `read-tenant-wide` maddesi O-19 satırındadır |
| MOD-0023 şablon kodu (O-8) | Ali | Y-6: ödeme partisi şablonu **tek adım**; kod, iş paketi numarası ve tarih yok | DL-2 |
| MOD-0023 §17 rev 2 onayı | Beste (onay verildi) | **kapandı** — `approvals.tsv:16` (`803fbaa5…`); AC-W1…W17 DL-2 kabul kapısında | DL-2 |
| MOD-0031 dedupe ölçümü | Treasury CT (ölçüm) | ölçülmedi | DL-3 |
| Verilmiş olanlar | — | port 5071 (`AGENTS.md:84`, `.antigravity/rules/ports.md:29`; D30); denetim defteri CT kararı D35; kod alma D27/5; ad alanı `finance-treasury.*` D11/4; servis `services/Diten.FinanceTreasuryService/` bugün yok (beklenen) | — |

**Not 1 — ENGELLER başlığının kapsamı.** Pack "ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz)" listesi DL-2 / DL-3 maddelerini de
içeriyor (MOD-0023 şablonu, §17 rev 2 maddeleri, MOD-0031 dedupe). Pack metnine göre `ready-for-dev` bunları da bekler; MOD-0289'daki
"iki kapı" ayrımı (tasarım kapısı ↔ dilim kabul kapısı) MOD-0126'da yok. DL-1'in DL-2 maddelerini beklemesi isteniyor mu — CT /
sahip kararı; bu kayıt karar vermez.

**Not 2 — O-19 maddesinde kilitlenme.** Checklist maddesi "kapsamı soran kod + kanıt testi" ister; kod `ready-for-dev`'den önce
yazılamaz. MOD-0289 gap 16/2'deki gibi bu kısım dilim kabul kapısına taşınmadıkça madde kapanamaz. Pack revizyonu CT işidir
(öneri: Auth tanımı ready-for-dev maddesi, kod + AC-32 DL-1 kabul kapısı).

**DCP numarası:** DCP-011 atandı ve dosya adı değişti (Y-4/2, 2026-10-27). D32/8'in numarasız onay istisnası D34/2 ile kullanılmaz;
pack checklist satırı ("DCP `approved` ve numarası atanmış") olduğu gibi kalır — açık kalan yalnız `approved`.

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet: `shasum -a 256` (ilk 16 hane), satır sayısı `wc -l`,
ikisi de @da4abb20; 2026-10-27'de `git show da4abb20:<yol>` ile **yeniden ölçüldü, 25 dosyanın hepsi 2026-10-26 ölçümüyle aynı**.
Karar dili: **al-uyarla** = kaynaktan alınacak (doğrula, Recovery düzenine göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`),
**yalnız referans** = uyarlanacak davranış (davranış okunur, kod alınmaz, Recovery'de yeniden yazılır), **alma** = alınmayacak
(pack "Taşınmayacaklar" maddesiyle), **sonraki dilim**.

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `PaymentBatch`, `PaymentInstruction`, `PaymentEvidenceLinkReference` tipleri; `PaymentBatchStatus` 10 değer; DL-1 hata kodları; Create / Validate komutları; sözleşme DTO'ları; liste / detay sorguları; `PaymentProposalService` kuralları (bölünerek); Mongo deposu (DL-1 kısmı), indeks kataloğu, bağlam, indeks hizmeti; denetleyicinin DL-1 uçları | para birimi hanesi arayüzü (→ `ICurrencyReferencePort`), kimlik bağlamı (→ JWT `sub`), kaynak testleri (davranış listesi) | `TenantEntity` (`"system"` aktör), `PassedByAdminProxy`, outbox sınıfları, `PermissionContracts` + talep değerlendirici, `CurrencyPrecisionFallback` sabit tablosu, kaynak DI (zaman aşımsız `HttpClient`), `Program.cs` / port 5058, `/api/v1/mod0126` rotaları, `mod0126_*` adlar, `ux_mod0126_payment_batch_number` kısmi indeksi, kullanıcıdan parti no + `CountAsync()+1`, `InMemoryPaymentBatchRepository` |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/PaymentProposals/PaymentProposalEntities.cs` | `568fe3cf50abdcb3` · 163 | al-uyarla (yalnız `PaymentBatch` :5-31, `PaymentInstruction` :33-53, `PaymentEvidenceLinkReference` :132-149) | `Domain/PaymentBatches/` | `TenantEntity` yerine `EntityBase`; `string` aktör → Guid (madde 11); outbox sınıfları (:55-66, :151-163) **alma** (madde 12); karar / istisna / onay belgesi DL-2/DL-3; banka referansları yalnız opak Guid (AC-20; pack "Entity Fields") |
| `S/src/…Domain/PaymentProposals/PaymentProposalEnums.cs` | `72e767bd46390e85` · 132 | al-uyarla | `Domain/PaymentBatches/` | `PaymentBatchStatus` 10 değer aynen (:9-21; pack "Durum makinesi"); `PassedByAdminProxy` **alma** (madde 7); `ApLinked` kaynak türü bu dilimde reddedilir (pack "Ownership and Boundaries") |
| `S/src/…Domain/Common/TenantEntity.cs` | — | alma | — | madde 11 (`"system"` varsayılan `CreatedBy`) |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Common/PaymentProposalErrorCodes.cs` | `ce3cc9e42e035587` · 57 | al-uyarla (DL-1'in kullandığı kodlar) | `Features/PaymentBatch/` hata sözlüğü | 52 kodun DL-1 alt kümesi + pack "Hata sözlüğü"nün DL-1'e düşen **yeni** kodları: `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`, `PAYMENT_REQUEST_IN_PROGRESS`, `PAYMENT_RECORD_CODE_UNAVAILABLE`, `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`, `PAYMENT_CURRENCY_SOURCE_UNAVAILABLE`, `PAYMENT_CURRENCY_CODE_INVALID`, `PAYMENT_METHOD_NOT_ALLOWED`, `PAYMENT_METHOD_SOURCE_UNAVAILABLE`; `PAYMENT_TENANT_FORBIDDEN` kullanılmaz (404); serbest İngilizce ileti **alma** (madde 3) — ileti resx anahtarı |
| `…/Commands/CreateManualPaymentBatchCommand.cs` | `ce2481db5037955c` · 25 | al-uyarla | `Commands/CreatePaymentBatchCommand.cs` + `Handlers/CommandHandlers/CreatePaymentBatchHandler.cs` | komut + handler aynı dosyada **alma** (:8-25); kullanıcıdan parti no ve `CountAsync()+1` **alma** (madde 9) → `IRecordCodePort` (`PAY`, RCS-001 §3) |
| `…/Commands/ValidatePaymentBatchCommand.cs` | `7481d77d5ecd98fe` · 25 | al-uyarla | `Commands/ValidatePaymentBatchCommand.cs` + handler | idempotency makbuzu ile (AC-16) |
| `…/Contracts/PaymentProposalContracts.cs` | `1e659de1d2867029` · 197 | al-uyarla | `PaymentBatchModels.cs` (tek dosya) | DTO'larda `TenantId` / `LegalEntityId` yok (AC-1); banka alanı adı yok (AC-20) |
| `…/Contracts/CurrencyPrecisionContracts.cs` | `69adb336a829e8c5` · 8 | yalnız referans | `ICurrencyReferencePort` (servis ortak) | kaynak arayüz tenant + kod; Recovery portu MOD-0048 `currency-minor-units` kümesini okur |
| `…/Contracts/IPaymentVerifiedIdentityContext.cs` | `5510bddb215ca7c4` · 8 | yalnız referans | Recovery `ICurrentUser` / JWT | servis içi kimlik bağlamı yeniden yazılmaz |
| `…/Queries/PaymentBatchQueries.cs` | `470bd7db2ca4e7f0` · 58 | al-uyarla | `Queries/GetPaymentBatchListQuery.cs`, `GetPaymentBatchByIdQuery.cs`, `PaymentBatchListSort.cs` + handler'lar | DataTable sunucu tarafı; kapsam süzgeci `IFinanceTreasuryDataScopeSource` (AC-32); `read-tenant-wide` yalnız okuma, yazma kapsamını genişletmez (AC-33) |
| `…/Services/PaymentProposalService.cs` | `f1dc4aa93bc0b636` · 489 | al-uyarla (bölerek) | Create / Update / Delete / BulkDelete / Validate handler'ları + domain | kuralları handler + domain'e böl (`handler-design`); kanıt bağlama :235-298 **alma** (madde 14, DL-3 ayrı komut); sayaç :126-128, :471-475 **alma** (madde 9) |
| `…/Contracts/PermissionContracts.cs`, `Infrastructure/…/ClaimsPaymentPermissionEvaluator.cs` | — | alma | `[HasPermission]` | madde 8, 13; DL-1 anahtarları aşağıda §2.6 |
| `…/Contracts/{SodPolicy,ApproverPolicy,WorkflowClient,PaymentLifecycle,PaymentEvidence,AuditDispatch}Contracts.cs`, `Services/{PaymentWorkflow,PaymentSodPolicy,PaymentLifecycle,PaymentEvidence,PaymentAuditDispatch}Service.cs`, `Commands/{SubmitWorkflow,AdminDirectDecision,GeneratePaymentAttestation,DispatchOutboxOnce}Command.cs`, `Queries/PaymentLifecycleQueries.cs` | — | sonraki dilim / alma | DL-2 (iş akışı, SoD, muafiyet — AC-9, AC-42, AC-43), DL-3 (kanıt, onay belgesi, Release); AdminDirect ve DispatchOutboxOnce **alma** (madde 7, 12; O-1, O-2) | |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/PaymentProposals/MongoPaymentBatchRepository.cs` | `2dd642c8c777e253` · 718 | al-uyarla (DL-1 kısmı) | `Persistence/PaymentBatches/` depolar | her işlem geri çağrısı `CentralAuditTransaction.EnlistAsync`; soft delete (`IsDeleted`, `DeletedAt`) |
| `…/PaymentProposalsMongoIndexes.cs` | `2bcb2622103f0e39` · 198 | al-uyarla | indeks kataloğu | 18 ad (4 tekil); `mod0126_` önekleri **alma** (madde 13). Kaynağın `ux_mod0126_payment_batch_number` indeksi (kısmi `IsDeleted=false`, `:47-52`) **alınmaz** (madde 9); yerine pack'in `ux_payment_batch_code` = **Unique(TenantId, BatchCode), kısmi filtre YOK** (`MOD-0126-payments-approvals.md:409@904dfd5c2`, RCS-001 §6). Kodun RCS'den gelmesi DB tekilliğini kaldırmaz. Test `PaymentBatchCodeUniquenessTests` (gerçek replica set) + sabotaj: AC-23 (`:1241-1247@904dfd5c2`) |
| `…/PaymentProposalsMongoContext.cs` | `977aa0d20bbbb5f1` · 26 | al-uyarla | `Persistence` bağlamı | koleksiyon adları `payment_batches`, `payment_instructions` |
| `…/PaymentProposalsIndexHostedService.cs` | `43c53eaad05a9717` · 29 | al-uyarla | indeks hizmeti | Recovery şema manifest deseni varsa ona uyulur (`mongo-indexing`) |
| — | — | **yeni** | `Persistence/Common/CommandReceipts/**` | `FinanceTreasuryCommandReceipt` + `ux_finance_treasury_command_receipt_scope`, `ix_finance_treasury_command_receipt_pending` (servis ortak; kaynakta yok; pack "Mongo indeksleri") |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Infrastructure/PaymentProposals/CurrencyPrecisionFallback.cs` | `e00db36958d3b402` · 60 | **alma** | — | sabit ISO tablosu (USD/EUR/GBP/JPY/CHF/TRY) "test only" fallback'i; pack: sabit tablo uydurulmaz, hane yoksa 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` |
| `…/ClaimsPaymentVerifiedIdentityContext.cs` | `a8a89f1e80e0152e` · 35 | yalnız referans | — | Recovery JWT bağlamı |
| `…/Infrastructure/DependencyInjection.cs` | — | alma | yeni DI | zaman aşımsız `HttpClient` (madde 15); FailClosed varsayılanları uygulama sayılmaz (madde 1) |
| `…/Api/Controllers/PaymentBatchesController.cs` | `205cd802bd8801db` · 346 | al-uyarla (DL-1 uçları) | `Api/Controllers/PaymentBatchesController.cs` (+ pack Repo Scope `PaymentExceptionsController.cs` — DL-1 okuma ucu olarak, bkz. §3) | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]` (AC-21); rota `/api/payment-batches` (madde 13: `/api/v1/mod0126` alma) |
| `…/Api/Program.cs`, `appsettings.json` | — | alma | `@orchestrator /add-module` iskeleti | port 5071 (5058 alma, madde 13); `AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)`; `Category = Finance (18)` main'e girince (D35/3) |

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `S/tests/…/PaymentProposals/PaymentProposalServiceTests.cs` | `f903ba73b30245c1` · 313 | Create / Validate kuralları (AC-1, 2, 3, 18, 20) |
| `…/PaymentDraftRepositoryTests.cs` | `fb4efcf0dde92419` · 243 | taslak deposu, kiracı süzgeci (AC-3, 32) |
| `…/PaymentDraftTransactionTests.cs` | `be4b3a1186dcb409` · 171 | işlem + denetim kaydı birlikte (AC-17) |
| `…/PaymentValidationRepositoryTests.cs` | `6e47ab7fc3664fac` · 260 | Validate deposu |
| `…/PaymentValidationTransactionTests.cs` | `845bc61fae32d989` · 156 | Validate işlemi |
| `…/PaymentBatchesControllerTests.cs` | `74571d371cb37634` · 321 | uç + izin (AC-21, 22) |
| `…/InMemoryPaymentBatchRepository.cs` | `3dd093f30a0de809` · 570 | **alma** — Recovery gerçek Mongo replica set ister |
| `…/FixtureCurrencyPrecisionResolver.cs` | `46e912f8ccf373b5` · 25 | test fikstürü deseni; sabit tablo üretim koduna girmez |

Devralınmış kaynak kanıtı (`MOD-0126 TEST 510/510 @ da4abb20`) Recovery hazırlığının ölçüsü değildir (pack "Kaynak ve kanıt").

### 2.6 Yetki ve roller — pack değişikliklerinin DL-1'e etkisi (D46, D47, D49, D50)

Pack "Authorization Convention" (`:1024-1088@904dfd5c2`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-405@904dfd5c2`; D46 tablosu tarihçedir, uygulanmaz).

- **DL-1 uçlarının anahtarları:** `finance-treasury.payment-batches.read`, `.read-tenant-wide` (T3, yalnız açık atama, yalnız okuma),
  `.create`, `.update`, `.delete`, `.bulk-delete` (T2), `.validate` (T3); DL-1'in "dört sorgu"su (pack "Backend File Convention":
  liste, detay, iz, istisna listesi — `:837-838@904dfd5c2`) için ayrıca `finance-treasury.payment-trace.read` ve
  `finance-treasury.payment-exceptions.read`. Kaynak `mod0126.*` anahtarları **alınmaz**.
- **Rol şablonları kod değildir:** Auth kaydı Ali'nin MOD-0018 paketindedir (Y-6). DL-1 yalnız manifestte anahtarları kaydeder; hiçbir
  tohum / varsayılan rol yazmaz. Test kişileri matristen alınır: Ödeme hazırlayıcı (create / update / delete / validate), toplu silme
  için **Hazine yöneticisi** (`bulk-delete` yalnız onda — D47/1; hazırlayıcıda yok), okuma için Ödeme görüntüleyici (D49/1).
- **`waive-sod` DL-1'de yok:** muafiyet kuralları (D47/1, D49/4; AC-9, AC-42, AC-43) DL-2'dedir. DL-1'in tek etkisi: manifest kaydı
  `waive-sod` ve `read-tenant-wide`'ı hiçbir rol şablonuna varsayılan bağlamaz (açık atama listesi Auth'ta, D47/1).
- **D50** yalnız AP / AR rollerini değiştirir; MOD-0126 DL-1'e etkisi yok. **D47/4:** finans rollerine `mdm.banks.read` /
  `mdm.bank-accounts.read` verilmez; DL-1 banka verisi okumaz (opak Guid).
- **Denetim kategorisi:** MOD-0126 `Finance = 18` bekler (D35/3); `MasterData` ara çözümü seçilmedi. D48/1 yalnız MOD-0289'u
  `MasterData`'ya bağlar, MOD-0126'yı değiştirmez.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

Yollar pack "Repo Scope" (`:461-512@904dfd5c2`) ve "Teslim dilimleri" DL-1 satırı (`:1551-1560@904dfd5c2`).

- Servis iskeleti `Diten.FinanceTreasuryService` (5 katman + `tests/`), port 5071, JWT, kiracı izolasyonu, 4 pipeline
  davranışı, modül manifest kaydı (`module-self-registration-standard`).
- Servis ortak yapı taşları (tek sahibi MOD-0126 DL-1; MOD-0119/0120/0124 bunları yeniden tanımlamaz):
  `FinanceTreasuryCommandReceipt`, `IFinanceTreasuryDataScopeSource` (okuma + yazma kapsamı; Y-4/6 yolu — Platform
  `IDataScopeResolver` bağdaştırıcısı, fail-closed, MDM `ITrustedLegalEntityScopeProvider` kullanılmaz), `ILegalEntityEligibilityPort`
  (MOD-0220 + `ApprovalStatus`), `ICurrencyReferencePort` (MOD-0048 `currency-minor-units`), `IRecordCodePort` (RCS-001, `PAY`),
  `DataScope` bağdaştırıcısı (`NoOp` kaydı yasak), CentralAudit kaydı + `FinanceTreasuryCentralAuditScope`.
- **Ödeme yöntemi kaynağı — artık pack'te:** `IPaymentMethodReferencePort` (`Features/PaymentBatch/**`) + MOD-0048 `payment-method`
  bağdaştırıcısı `Infrastructure/Currency/**` (zaman aşımlı `HttpClient`; uç `GET /api/lookups/reference-data/consumable-sets/payment-method/published-values`).
  MOD-0126'ya özgüdür, servis ortak değildir (pack "Repo Scope"). DL-1 kabulü AC-41 (a), (b), (c), (e) Create / Update için;
  (d) DL-2 / DL-3. Karşılaştırma `StringComparison.Ordinal` (S-56). Sabotaj: küme kontrolü kaldırılınca (b), harfe duyarsız
  karşılaştırma konunca (e) kırmızı.
- `UpdatePaymentBatchCommand`, `DeletePaymentBatchCommand`, `BulkDeletePaymentBatchCommand` (O-3; yalnız gönderilmemiş taslak,
  soft delete; AC-23).
- Dört sorgu: `GetPaymentBatchListQuery`, `GetPaymentBatchByIdQuery`, `GetPaymentBatchTraceQuery`, `GetPaymentExceptionListQuery`
  + `PaymentBatchListSort` (pack "Backend File Convention"); istisna okuma ucu `PaymentExceptionsController` (Repo Scope). İstisna
  kayıtları DL-2'de (SoD) oluşur; DL-1'de liste boş da olsa kapsam süzgeci AC-32 ile test edilir.
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` + `CT-DECISIONS.md` satırı ("kararı veren": Natig,
  2026-10-26 — D35) + `AuditTrailStandardTests` `AcceptedTrails` / `PinnedCounts` — üçü aynı değişiklikte (D35/4); borç / istisna 0.
- `docs/analysis/contracts/payment-batch.openapi.yaml` v1.0.0 (AC-29).
- **MOD-0220 §22 kodu DL-1 WP'sinde değil:** pack "Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler" listesinde; kod Ali'nin
  MOD-0023 kulesinin sırasındadır (Y-5/3). DL-1 yalnız tüketir (önceki sürümdeki "aynı dalga, ayrı yazar kapsamı" önerisi geçersiz).

## 4. DL-1 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir; prompt'un içermesi gerekenlerin listesidir. Biçim örneği main'de
`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri):
`execution/domains/finance-treasury/work-packs/prompts/WP-0126-DL1-v1.0.md`. Kuyruk satırı `WP-0126-DL1` (CT-QUEUE `PLANNED`,
bu dalda 242ef03df).

**Başlık satırları**

| Alan | Değer (taslak) | `ready-for-dev` günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` | yeni servis iskeleti için `@orchestrator /add-module` adımı aynı WP'de mi ayrı mı — CT kararı |
| WP / Prompt | `WP-0126-DL1` · `TRM-WP-0126-DL1-P v1.0` | — |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0126-dl1` (pack künyesinde `feature/fin/mod-0126-payments-approvals` — hangisi, CT kararı) | Expected HEAD = pack `ready-for-dev` onayının main'deki commit'i (S-53); worktree yolu |
| Risk | HIGH — §9.1 sinyalleri: kiracı, yetki, denetim, sözleşme, para | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kapsanan dilim | **yalnız DL-1** | — |

**İzinli yollar** (pack Repo Scope, DL-1 satırları): `services/Diten.FinanceTreasuryService/**` (Api / Application / Domain /
Persistence / Infrastructure + `tests/`; `Features/PaymentBatch/**`, `Domain/PaymentBatches/**`, `Persistence/PaymentBatches/**`,
`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`,
`Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`, `Api/Controllers/{PaymentBatches,PaymentExceptions}Controller.cs`,
`Api/CentralAudit/FinanceTreasuryCentralAuditScope.cs`); `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md`,
`tests/architecture/audit-ledger/CT-DECISIONS.md` (tek satır), `AuditTrailStandardTests` sabitleri; `docs/analysis/contracts/payment-batch.openapi.yaml`.
`Infrastructure/{Workflow,Evidence,BankDispatch}/**` DL-2 / DL-3'tür — DL-1 prompt'una girmez.

**Korunan yollar** (pack Protected Paths): `.antigravity/**`, `AGENTS.md`, `gateway/**` (ocelot yalnız integration-agent; DL-4),
`services/Diten.Platform/**`, `services/Diten.MdmService/**` (MOD-0220 kodu dahil), `services/Diten.ProcurementService/**`,
`services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`, diğer servisler, `frontend/**` (DL-4),
`docs/analysis/contracts/` altındaki diğer üretici sözleşmeleri, `execution/**` (registries, domain-config, kuyruk, approvals),
kaynak depo ve handoff klasörü.

**Önce oku:** pack (`ready-for-dev` sürümü), AGENTS.md, `execution/domains/finance-treasury/domain-config.md`, `.antigravity/rules/`
multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent, code-style (S-56), audit-trail-standard,
data-scope-enforcement, record-coding-standard, mongo-indexing, handler-design, module-self-registration-standard, ports; karar
kayıtları `treasury-intake-owner-decisions-0{1..5}.md` (D1–D50; rol matrisi -05), bu hazırlık kaydı.

**Sözleşme yüzeyleri:**
- API: `payment-batch.openapi.yaml` v1.0.0 (AC-29) — uçlar ve hata kodları pack "Hata sözlüğü"nden alıntıyla.
- İzin: §2.6'daki DL-1 anahtarları; her aksiyonda `[HasPermission]` (AC-21).
- Test — **DL-1 Bitiş AC'leri** (`:1559-1560@904dfd5c2`): AC-1, 2, 3, 39 (Create kısmı), 41 (a–c, e; Create / Update), 16 (Create
  kısmı), 17, 18, 20, 21, 22, 23, 29, 32, 33 (Create / Update / Delete / Validate kısmı). AC metinleri prompt'a pack'ten tırnakla
  alıntılanır. Not: AC-17 DL-3 bitişinde de "17 (sabotaj)" olarak geçiyor; DL-1'de tek aşamalı komutların tek olayı + outbox
  sabotajı (Test Expectations), K2 sabotajı DL-3 — prompt bu ayrımı açık yazmalı.
- Sabotaj: AC-17 (outbox yazımı zorla başarısız → iş verisi yok), AC-23 (`IsDeleted=false` kısmi filtresi), AC-32 (kapsam süzgeci
  kaldırılınca), AC-33 (`TenantWideScope()` yazmaya sızınca), AC-39 (`approvalStatus` yoksa onaylı sayılınca / kimlik karşılaştırması
  kaldırılınca), AC-41 (b), (e).

**Gerekli testler:** birim (validatorlar, idempotency parmak izi, RCS port sahtesiyle kod biçimi); **gerçek Mongo replica set**
entegrasyonu (kiracı / şirket 404, AC-32, soft delete, idempotent tekrar ve ilk yanıtın kaybolduğu senaryo, CAS 409, CentralAudit
commit / rollback, `PaymentBatchCodeUniquenessTests`) — her test takımı kendi URI değişkenini okur, varsayılan porta düşmez,
replica set `enableTestCommands` ile; sahte MOD-0220 (AC-39 vaka listesi), sahte MOD-0048 (AC-41, para birimi hanesi);
mimari (`[HasPermission]`, banka alan adı yasağı, `float`/`double` para alanı yok, `AuditTrailStandard` — AC-18); sözleşme testi
(AC-29); build `Diten.FinanceTreasuryService` 0 hata; yerel kapıda `scripts/` Python testleri. Sahte istemciyle geçen test canlı
kanıt sayılmaz (pack kapanış tablosu).

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md` (+ `/add-module` kullanılıyorsa `.antigravity/agents/orchestrator.md`
  ve ilgili workflow), yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş** olarak: hedef servis + proje yolu (`services/Diten.FinanceTreasuryService/src/…`);
  aggregate / entity + alanlar (`PaymentBatch`, `PaymentInstruction`, `FinanceTreasuryCommandReceipt` — pack "Entity Fields");
  komut / sorgu listesi (Create, Update, Delete, BulkDelete, Validate; liste, detay, iz, istisna listesi); validator kuralları
  (`CreatePaymentBatchValidator`, `UpdatePaymentBatchValidator`, `GetPaymentBatchListValidator` — pack "Validation Rules"un alan kuralları); izin anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- `/add-module` alanları (iskelet aynı WP'deyse): pack yolu + status, domain `finance-treasury`, servis `Diten.FinanceTreasuryService`,
  shell `tenant`, `golden_reference: compact`, `form_field_count: 16`, dal, alt ajan zinciri.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa işçiye yol + SHA-256 ile verilir (S-52); inceleme kaydı commit'ten önce `prompt_gate.py review-check` ile denenir (S-54);
HIGH iş — kabul kanıtı betiğini CT yazar, kırmızı tabanı kaydeder, prompt'a yol + SHA-256 ile verir (S-83); Expected HEAD
`ready-for-dev` günü doldurulur, pack onayı değişirse prompt yeniden tabanlanır ve incelenir (S-53); dalga sonunda tek PR (S-76,
S-77). Rapor biçimi SOP §22.

**Keşif:** `python3 .antigravity/scripts/verify_module_id.py . --discover "payments approvals"` raporu, yol@commit + sha256, keşif
kararları (parent / child / yeni) — dağıtım günü koşulur.

**Kod alma izi:** §2'deki **al-uyarla** satırları; her biri WP raporunda `path@da4abb20 + sha256`.

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git show`); kaynak düzeltmesi yok. Kaynak testler **çalıştırılmadı**.
- Recovery'de hiçbir test ya da build koşulmadı; ölçümler dosya okuma ve `grep` ile @904dfd5c2.
- Rol şablonlarının Auth'taki karşılığı, MOD-0048 seed'lerinin zamanı, BL-565 çağrı sözleşmesi ve `Finance = 18` tarihi
  sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.
