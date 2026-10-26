---
record_id: MOD-0126-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (status draft, sha256 684e89d6…, main @04c8fb4cf)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5)
---

# MOD-0126 DL-1 hazırlığı — servis iskeleti, parti çekirdeği ve denetim altyapısı

Bu kayıt, MOD-0126 `ready-for-dev` olduğunda DL-1 WP prompt'unun hızla yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

## 1. Başlama koşulları (bugün açık olanlar)

DL-1 prompt'u ancak bunlar kapanınca dağıtılabilir (paket "Ready-for-dev Checklist" ve "Teslim dilimleri"):

| Koşul | Bugün | Kimden |
|---|---|---|
| Paket künyesi `ready-for-dev` + o sürüme CT-APR-02 ve CT-APR-01 (S-49, D34) | `draft` | CT + Beste |
| DCP numarası ve `approved` | `UNASSIGNED`, `draft` | Beste |
| TRM-14: MOD-0220 §22 onayı (`ApprovalStatus`) | onay turu açık (D34/1) | Beste |
| O-9 RCS-001 ortak kod servisi (BL-565) | yok; sahibi belirlenmedi | sahip ataması |
| O-11 küsurat hanesi + O-13 `payment-method` kümesi (TRM-17, MOD-0048-FU01 §21) | önerildi; onay turu açık | Beste |
| O-19 SEC-002 tüketim yolu | yok | Beste |
| Yeni servis defteri + CT-DECISIONS satırı için CT kararı | açık | CT + sahip |
| MOD-0018 `finance-treasury.*` izin tohumu ve rol şablonları | açık | Beste |

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet: `shasum -a 256` (ilk 16 hane), satır sayısı
`wc -l`, ikisi de @da4abb20, bu oturumda `git archive` ile ölçüldü. Karar dili: **al-uyarla** (doğrula, Recovery düzenine
göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`), **yalnız referans** (davranışı okunur, kod alınmaz), **alma**
(do-not-carry maddesiyle), **sonraki dilim**.

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/PaymentProposals/PaymentProposalEntities.cs` | `568fe3cf50abdcb3` · 163 | al-uyarla (yalnız `PaymentBatch` :5-31, `PaymentInstruction` :33-53, `PaymentEvidenceLinkReference` :132-149) | `Domain/PaymentBatches/` | `TenantEntity` yerine `EntityBase`; `string` aktör → Guid (do-not-carry 11); outbox sınıfları (:55-66, :151-163) **alma** (madde 12); karar / istisna / onay belgesi DL-2/DL-3 |
| `S/src/…Domain/PaymentProposals/PaymentProposalEnums.cs` | `72e767bd46390e85` · 132 | al-uyarla | `Domain/PaymentBatches/` | `PaymentBatchStatus` 10 değer aynen (:9-21); `PassedByAdminProxy` **alma** (madde 7) |
| `S/src/…Domain/Common/TenantEntity.cs` | — | alma | — | madde 11 (`"system"` varsayılan `CreatedBy`) |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Common/PaymentProposalErrorCodes.cs` | `ce3cc9e42e035587` · 57 | al-uyarla (DL-1'in kullandığı kodlar) | `Features/PaymentBatch/` hata sözlüğü | 52 kodun DL-1 alt kümesi; serbest İngilizce ileti **alma** (madde 3) — ileti resx anahtarı |
| `…/Commands/CreateManualPaymentBatchCommand.cs` | `ce2481db5037955c` · 25 | al-uyarla | `Commands/CreatePaymentBatchCommand.cs` + `Handlers/CommandHandlers/CreatePaymentBatchHandler.cs` | komut + handler aynı dosyada **alma** (:8-25); kullanıcıdan parti no ve `CountAsync()+1` **alma** (madde 9) → `IRecordCodePort` (`PAY`) |
| `…/Commands/ValidatePaymentBatchCommand.cs` | `7481d77d5ecd98fe` · 25 | al-uyarla | `Commands/ValidatePaymentBatchCommand.cs` + handler | idempotency makbuzu ile |
| `…/Contracts/PaymentProposalContracts.cs` | `1e659de1d2867029` · 197 | al-uyarla | `PaymentBatchModels.cs` (tek dosya) | DTO'larda `TenantId` / `LegalEntityId` yok |
| `…/Contracts/CurrencyPrecisionContracts.cs` | `69adb336a829e8c5` · 8 | yalnız referans | `ICurrencyReferencePort` (servis ortak) | kaynak arayüz tenant + kod; Recovery portu MOD-0048 `currency-minor-units` kümesini okur |
| `…/Contracts/IPaymentVerifiedIdentityContext.cs` | `5510bddb215ca7c4` · 8 | yalnız referans | Recovery `ICurrentUser` / JWT | servis içi kimlik bağlamı yeniden yazılmaz |
| `…/Queries/PaymentBatchQueries.cs` | `470bd7db2ca4e7f0` · 58 | al-uyarla | `Queries/GetPaymentBatchListQuery.cs`, `GetPaymentBatchByIdQuery.cs`, `PaymentBatchListSort.cs` + handler'lar | DataTable sunucu tarafı; kapsam süzgeci `IFinanceTreasuryDataScopeSource` |
| `…/Services/PaymentProposalService.cs` | `f1dc4aa93bc0b636` · 489 | al-uyarla (bölerek) | Create / Update / Delete / BulkDelete / Validate handler'ları + domain | kuralları handler + domain'e böl (`handler-design`); kanıt bağlama :235-298 **alma** (madde 14, DL-3 ayrı komut); sayaç :126-128, :471-475 **alma** (madde 9) |
| `…/Contracts/PermissionContracts.cs`, `Infrastructure/…/ClaimsPaymentPermissionEvaluator.cs` | — | alma | `[HasPermission]` | madde 8 |
| `…/Contracts/{SodPolicy,ApproverPolicy,WorkflowClient,PaymentLifecycle,PaymentEvidence,AuditDispatch}Contracts.cs`, `Services/{PaymentWorkflow,PaymentSodPolicy,PaymentLifecycle,PaymentEvidence,PaymentAuditDispatch}Service.cs`, `Commands/{SubmitWorkflow,AdminDirectDecision,GeneratePaymentAttestation,DispatchOutboxOnce}Command.cs`, `Queries/PaymentLifecycleQueries.cs` | — | sonraki dilim / alma | DL-2 (iş akışı, SoD), DL-3 (kanıt, onay belgesi, Release); AdminDirect ve DispatchOutboxOnce **alma** (madde 7, 12; O-1, O-2) | |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/PaymentProposals/MongoPaymentBatchRepository.cs` | `2dd642c8c777e253` · 718 | al-uyarla (DL-1 kısmı) | `Persistence/PaymentBatches/` depolar | her işlem geri çağrısı `CentralAuditTransaction.EnlistAsync`; soft delete (`IsDeleted`, `DeletedAt`) |
| `…/PaymentProposalsMongoIndexes.cs` | `2bcb2622103f0e39` · 198 | al-uyarla | indeks kataloğu | 18 ad (4 tekil); `mod0126_` önekleri **alma** (madde 13); `IsDeleted=false` kısmi tekil parti no indeksi **alma** (madde 9) — kod RCS'den gelir |
| `…/PaymentProposalsMongoContext.cs` | `977aa0d20bbbb5f1` · 26 | al-uyarla | `Persistence` bağlamı | koleksiyon adları `payment_batches`, `payment_instructions` |
| `…/PaymentProposalsIndexHostedService.cs` | `43c53eaad05a9717` · 29 | al-uyarla | indeks hizmeti | Recovery şema manifest deseni varsa ona uyulur |
| — | — | **yeni** | `Persistence/Common/CommandReceipts/**` | `FinanceTreasuryCommandReceipt` + `ux_finance_treasury_command_receipt_scope`, `ix_finance_treasury_command_receipt_pending` (servis ortak; kaynakta yok) |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Infrastructure/PaymentProposals/CurrencyPrecisionFallback.cs` | `e00db36958d3b402` · 60 | **alma** | — | sabit ISO tablosu (USD/EUR/GBP/JPY/CHF/TRY) "test only" fallback'i; paket: sabit tablo uydurulmaz, hane yoksa 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` |
| `…/ClaimsPaymentVerifiedIdentityContext.cs` | `a8a89f1e80e0152e` · 35 | yalnız referans | — | Recovery JWT bağlamı |
| `…/Infrastructure/DependencyInjection.cs` | — | alma | yeni DI | zaman aşımsız `HttpClient` (madde 15); FailClosed varsayılanları uygulama sayılmaz (madde 1) |
| `…/Api/Controllers/PaymentBatchesController.cs` | `205cd802bd8801db` · 346 | al-uyarla (DL-1 uçları) | `Api/Controllers/PaymentBatchesController.cs` | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]`; rota `/api/payment-batches` (madde 13: `/api/v1/mod0126` alma) |
| `…/Api/Program.cs`, `appsettings.json` | — | alma | `@orchestrator /add-module` iskeleti | port 5071 (5058 alma, madde 13); `AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)` |

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `S/tests/…/PaymentProposals/PaymentProposalServiceTests.cs` | `f903ba73b30245c1` · 313 | Create / Validate kuralları (AC-1, 2, 3, 18, 20) |
| `…/PaymentDraftRepositoryTests.cs` | `fb4efcf0dde92419` · 243 | taslak deposu, kiracı süzgeci (AC-21, 32) |
| `…/PaymentDraftTransactionTests.cs` | `be4b3a1186dcb409` · 171 | işlem + denetim kaydı birlikte (AC-17) |
| `…/PaymentValidationRepositoryTests.cs` | `6e47ab7fc3664fac` · 260 | Validate deposu |
| `…/PaymentValidationTransactionTests.cs` | `845bc61fae32d989` · 156 | Validate işlemi |
| `…/PaymentBatchesControllerTests.cs` | `74571d371cb37634` · 321 | uç + izin (AC-21, 22) |
| `…/InMemoryPaymentBatchRepository.cs` | `3dd093f30a0de809` · 570 | **alma** — Recovery gerçek Mongo replica set ister (lane kuralı) |
| `…/FixtureCurrencyPrecisionResolver.cs` | `46e912f8ccf373b5` · 25 | test fikstürü deseni; sabit tablo üretim koduna girmez |

Devralınmış kaynak kanıtı (`MOD-0126 TEST 510/510 @ da4abb20`) Recovery hazırlığının ölçüsü değildir (paket "Kaynak ve kanıt").

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

- Servis iskeleti `Diten.FinanceTreasuryService` (5 katman + `tests/`), port 5071, JWT, kiracı izolasyonu, 4 pipeline
  davranışı, modül manifest kaydı.
- Servis ortak yapı taşları (tek sahibi MOD-0126 DL-1; MOD-0119/0120/0124 bunları yeniden tanımlamaz):
  `FinanceTreasuryCommandReceipt`, `IFinanceTreasuryDataScopeSource` (okuma + yazma kapsamı), `ILegalEntityEligibilityPort`
  (MOD-0220 + `ApprovalStatus`), `ICurrencyReferencePort` (MOD-0048 `currency-minor-units`), `IRecordCodePort` (RCS-001),
  `DataScope` bağdaştırıcısı (`NoOp` kaydı yasak), CentralAudit kaydı + `FinanceTreasuryCentralAuditScope`.
- `UpdatePaymentBatchCommand`, `DeletePaymentBatchCommand`, `BulkDeletePaymentBatchCommand` (O-3; yalnız gönderilmemiş taslak,
  soft delete).
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` + `CT-DECISIONS.md` satırı + `AuditTrailStandardTests`
  (CT kararı ister).
- `docs/analysis/contracts/payment-batch.openapi.yaml` v1.0.0 (AC-29).
- MOD-0220 §22 kodu (`lookup-validation` yanıtına `ApprovalStatus`) — MDM servisinde, MOD-0220 sahibinin onayıyla (TRM-14,
  `whole_pack=TRM-14`); MOD-0126 DL-1 WP'siyle aynı dalga, ayrı yazar kapsamı (XMC-001 / D9 prompt'ta yeniden yazılır).

## 4. WP prompt iskeleti (doldurulacak alanlar)

Biçim: main'deki örnek `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.3.md`. Hazır olunca
`execution/domains/finance-treasury/work-packs/prompts/WP-0126-DL1-v1.0.md` olarak yazılır.

| Alan | Değer (taslak) | Açık |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` (+ §17.4 giriş alanları) | yeni servis iskeleti için `@orchestrator /add-module` adımı ayrı mı tek mi — CT kararı |
| WP / Prompt | `WP-0126-DL1` · `TRM-WP-0126-DL1-P v1.0` | kuyruk satırı (TRM kuyruğuna WP satırı) |
| Repository / Branch / Expected HEAD / Worktree | `feature/fin/wp-0126-dl1` · Expected HEAD = paketin `ready-for-dev` onayının main'deki commit'i | dağıtım günü |
| Risk | HIGH — sinyaller: kiracı, denetim, sözleşme, para | — |
| Görev sınıfı · Paket | ürün · MOD-0126 pack | — |
| İzinli yollar | paket "Repo Scope" DL-1 satırları (servis, Common/CommandReceipts, Common/DataScope, Infrastructure portları, ledger, sözleşme) | MOD-0220 §22 kodu aynı prompt'ta mı ayrı WP mi |
| Korunan yollar | paket "Protected Paths" + `.antigravity/**`, `ocelot.json`, diğer servisler | — |
| Kapsanan dilim | DL-1 | — |
| Önce oku | pack, AGENTS.md, domain-config, multi-tenancy, security-jwt, git-safety, cross-module-change-consent, code-style (S-56 kültür kuralı), audit-trail-standard, data-scope-enforcement, record-coding-standard, D1–D34 karar kayıtları, bu hazırlık kaydı | — |
| Sözleşme yüzeyleri | API (`payment-batch.openapi.yaml`), İzin (`finance-treasury.*`), Test (AC-1, 2, 3, 16, 17, 18, 20, 21, 22, 23, 29, 32, 33, 39, 41), Sabotaj (AC-17 denetim, AC-32 kapsam) | AC metinleri pack satır numaralarıyla alıntılanır |
| Keşif | `verify_module_id.py --discover "payments approvals"` raporu, yol@commit + sha256 | dağıtım günü koşulur |
| Kod alma izi | §2'deki **al-uyarla** satırları, her biri `path@da4abb20 + sha256` ile WP raporuna | — |
| Uzunluk | ~10 bin token'ı aşarsa işçiye dosya yolu + SHA-256 ile verilir (S-52) | — |

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynak düzeltmesi yok.
- Sınıflandırma okuma ve paket metniyle yapıldı; kaynak testler bu oturumda **çalıştırılmadı**.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.
