---
record_id: MOD-0124-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0124-cash-positioning.md (status draft, sha256 f55d401f…, bu worktree HEAD @ce6fb0ed3; pack'in son değişikliği 393baf735)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5 — pack satır 38-39)
---

# MOD-0124 DL-1 hazırlığı — anlık görüntü çekirdeği, eşik politikası, makbuz ve denetim

Bu kayıt, MOD-0124 `ready-for-dev` olduğunda DL-1 WP prompt'unun hızla yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

**DL-1 kapsamı (pack satır 1611-1619):** `Features/CashPosition/` içinde Create / Update / Delete / BulkDelete + List / ById
sorguları, `Features/CashPositionThresholdPolicy/` tam seti, Domain / Persistence, makbuz kullanımı, indeks hizmeti, MOD-0220
ve MOD-0048 bağdaştırıcılarının **kullanımı**, RCS kod portu, `cash-position.openapi.yaml`. Bitiş AC'leri: AC-1, 2, 3, 4, 5,
8, 16 (Create/Update/Delete kısmı), 17 (tek aşamalı kısım), 18, 19 (Create), 21, 22, 28, 31, 32 (Create / Update / Delete /
politika kısmı).

**Servis ortak yapı taşları MOD-0124'ün değildir.** `FinanceTreasuryCommandReceipt` (+ `ux_finance_treasury_command_receipt_scope`,
`ix_finance_treasury_command_receipt_pending`), `IFinanceTreasuryDataScopeSource`, `ILegalEntityEligibilityPort`,
`ICurrencyReferencePort`, `IRecordCodePort`, `DataScope` bağdaştırıcısı ve CentralAudit kaydı (`FinanceTreasuryCentralAuditScope`)
**yalnız MOD-0126 DL-1'de** açılır; MOD-0124 DL-1 bunları **yeniden tanımlamaz, kopyalamaz, genişletmez — yalnız kullanır**
(pack satır 139, 193-196, 447-453, 492-494, 540-555, 1440). Ortak tipte değişiklik gerekirse dört paket birlikte değişir
(pack satır 61-62).

## 1. Başlama koşulları (bugün açık olanlar)

DL-1 prompt'u ancak bunlar kapanınca dağıtılabilir (pack "Ready-for-dev Checklist" satır 1431-1450 ve "Teslim dilimleri"
satır 1615-1617):

| Koşul | Bugün | Kimden |
|---|---|---|
| Paket künyesi `ready-for-dev` + o sürüme CT-APR-02 ve CT-APR-01 (D34: onay `ready-for-dev` turunda) | `draft` (pack satır 17) | CT + Beste |
| DCP (`UNASSIGNED-finance-treasury-intake`) `approved` ve numarası atanmış (pack satır 1434) | `UNASSIGNED`, `draft` | Beste |
| **MOD-0126 DL-1 main'de** — servis iskeleti, port 5071, servis ortak yapı taşları, CentralAudit kurulumu, servis defteri (pack satır 1440, 1616) | MOD-0126 pack `draft`; kod yok | MOD-0126 DL-1 WP'si |
| Servis defteri + CT-DECISIONS satırı için CT kararı | **verildi — D35** (yol b, borç / istisna 0) | — |
| Platform `AuditCategory`'ye `Finance` değeri (D35/3: MOD-0126 DL-1'den önce) | yok (pack satır 1158-1159 hâlâ "karar çıkana kadar değer uydurulmaz" diyor) | Platform sahibi |
| TRM-14: MOD-0220 `lookup-validation` yanıtında `ApprovalStatus` + MOD-0220 sözleşme belgesi (pack satır 1441) | onay turu açık (D34/1) | Beste |
| O-9 RCS-001 ortak kod servisi (BL-565); önekler `CPS` / `CTP` kabul (D29/2) (pack satır 1443, 1578) | servis yok; sahibi belirlenmedi | sahip ataması |
| O-8 küsurat hanesi kaynağı — MOD-0048-FU01 §21 `currency-minor-units` (TRM-17) (pack satır 1444, 1577) | önerildi; onay turu açık | Beste |
| O-23 SEC-002 veri kapsamı çözücüsünün FinanceTreasury tüketim yolu (pack satır 1435, 1592) | yok | Beste |
| O-3 MOD-0037 kapısı (pack satır 1437, 1572) | sahip payı verildi (D29/1); platform-shared-services teyidi açık | platform-shared-services |
| MOD-0018 `finance-treasury.cash-positions.*` / `…cash-position-threshold-policies.*` 20 anahtarın kaydı + rol şablonu (pack satır 1119-1140, 1487) | açık | Beste |

**DL-1'i beklemeyen bağımlılıklar:** MOD-0023 §17 WP (Ali) ve nakit pozisyonu onay şablonu (O-20) DL-3'ün kapısıdır
(pack satır 1445-1448, 1623-1625). MOD-0031 ölçümü (O-17) DL-4'ündür. MOD-0289 banka hesabı arama sözleşmesi taslaktır
(`docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md`, pack satır 1489, 1554); DL-1 buna
**bağlı değildir** — `BankAccountRef` yalnız `Guid ≠ Empty` opak referanstır, doğrulama yapılmaz (pack satır 318, 606, O-15
satır 1584).

**Dayanak kararlar (D34/D35 henüz main'de değil):** `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`,
PR #235 ile taşınıyor, yerel commit `a181e933b`. Bu worktree'deki aynı dosyada D34 / D35 satırı **yok** (ölçüldü: `grep`
0 eşleşme) — prompt yazılırken kayıt main'deki sürümden alıntılanır.

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`; `…` = `src/Diten.ErpTreasuryService.<Katman>/`). Özet:
`shasum -a 256` (ilk 16 hane), satır sayısı `wc -l`, ikisi de @da4abb20, bu oturumda `git archive` + `tar -xf` ile ölçüldü.
Karar dili: **al-uyarla** (doğrula, Recovery düzenine göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`), **yalnız
referans** (davranışı okunur, kod alınmaz), **alma** (do-not-carry maddesiyle, pack satır 1500-1527), **sonraki dilim**.

**Önemli kaynak gözlemi:** kaynakta HTTP'den erişilen oluşturma yolu yalnız içe aktarmadır (CP-03,
`CashPositionSnapshotsController.cs:80`). `CashPositionPersistenceService.CreateDraftSnapshotAsync` (`:485`) elle taslak
oluşturur ama **yalnız testlerden çağrılır** (`CashPositionPersistenceServiceTests.cs`, `CashPositionReferenceFailureTests.cs`;
denetleyici ucu yok — ölçüldü). Recovery `CreateCashPositionCommand`'ının en yakın kaynağı budur; pack bu yöntemi adıyla
anmıyor (pack satır 148 "v1 karşılığı elle giriş").

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/…Domain/CashPositioning/CashPositionEntities.cs` | `ae5f900550a61fe8` · 229 | al-uyarla (yalnız `CashPositionSnapshot` :5-25, `CashPositionLine` :27-46, `CashPositionThresholdPolicy` :135-149) | `Domain/CashPositions/` | `TenantEntity` yerine `EntityBase` (pack satır 247-252); `string` aktör → `Guid` (madde 7); `MaskedAccountRef`, `BankBalanceSnapshotId`, `SourceRecordId` alma (madde 10; pack satır 124); politikada `IsDefault` / `ApprovalStatus` alma (madde 12; pack satır 364-365); `LegalEntityBaseCurrencyCode`, `ReportingTimezoneId` alma (pack satır 309-311, O-10). Yeni: `ContentRevision`, `ApprovalStatus`, `BalanceSourceKind`, `BalanceAsOfUtc`, `SourceReference`, `CashPositionThresholdPolicySlot` (pack satır 140, 280-307, 468-470). Diğer 11 sınıf: 3–8 v1'de taşınmaz, 10–12 DL-2/DL-3, 13–14 alma (pack satır 125-136) |
| `S/…Domain/CashPositioning/CashPositionEnums.cs` | `ae0348b29c59ef8c` · 145 | al-uyarla (alt küme) | `Domain/CashPositions/` | `SnapshotStatus` → `CashPositionStatus` Draft 0 / Reconciled 2 / Published 3 / Superseded 4, sayısal değer korunur; `Imported` 1 ve `Archived` 5 taşınmaz (pack satır 203-213); `CashCategoryType` :68-75 aynen (pack satır 319); `ReconciliationStatus` alt kümesi DL-2 |
| `S/…Domain/CashPositioning/CashPositionDomainException.cs` | `2486720735886e73` · 12 | yalnız referans | — | pack'te yok; Recovery hata sözlüğü `CASH_POSITION_*` kodlarıyla |
| `S/…Domain/Common/TenantEntity.cs` | `2ef3028110d99e9e` · 26 | alma | — | `DateTime` + `long RowVersion` + `string CreatedBy` (pack satır 249-251; madde 7) |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…Application/Common/CashPositionErrorCodes.cs` | `483f3e4d2e9fb925` · 57 | al-uyarla (DL-1 alt kümesi) | `Features/CashPosition/` hata sözlüğü | 52 sabit (ölçüldü); `MOD0124_*` öneki alma (madde 15) → `CASH_POSITION_*`, her kodun HTTP değeri açık (pack satır 500-534). DL-1 alt kümesi (çıkarım): `VALIDATION_FAILED`, `SCOPE_INPUT_FORBIDDEN`, `NOT_FOUND`, `VERSION_CONFLICT`, `INVALID_CURRENCY_CODE`, `MIXED_CURRENCY`, `DUPLICATE_LINE_KEY`, `SENSITIVE_TEXT_REJECTED`, `THRESHOLD_POLICY_VALIDATION_FAILED` + yeni `PERMISSION_DENIED`, `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_CONFLICT`, `REQUEST_IN_PROGRESS`, `LEGAL_ENTITY_NOT_ELIGIBLE`, `LEGAL_ENTITY_UNAVAILABLE`, `CURRENCY_PRECISION_UNAVAILABLE`, `REFERENCE_UNAVAILABLE`, `RECORD_CODE_UNAVAILABLE`, `THRESHOLD_POLICY_OVERLAP`, `COMMIT_OUTCOME_UNKNOWN`; serbest İngilizce ileti alma — resx anahtarı (pack satır 534) |
| `…Application/CashPositioning/Services/CashPositionPersistenceService.cs` | `ac1694b3479c1544` · 953 | al-uyarla (bölerek; yalnız `CreateDraftSnapshotAsync` :485 ve doğrulama kuralları) | `CreateCashPositionHandler` + `CreateCashPositionValidator` + domain | `SensitiveDigits = \d{8,}` :13 korunur (pack satır 329); karışık para birimi :115-120 ve yinelenen satır :122-133 korunur ama anahtardan para birimi çıkar (pack satır 332-334); kaynak karşılaştırması `OrdinalIgnoreCase` (:115) — Recovery upper-normalize sonrası karşılaştırır; `ReportingTimezoneMissing` :147-150 alma (O-10); MOD-0289 doğrulayıcı çağrısı :163-172 alma (pack satır 318); `CP-{yyyyMMdd}-{8hex}` :362 ve `CPI-…` :387 alma (madde 9) → `IRecordCodePort` (`CPS`); `ImportAsync` :51 sonraki kapı (CP-03 ertelendi, O-2/O-3); iş kuralı servis sınıfında değil handler + domain'de (pack satır 905-907) |
| `…Application/CashPositioning/Services/CashPositionThresholdPolicyService.cs` | `01e30a1b7ef910dd` · 253 | al-uyarla (bölerek) | `Features/CashPositionThresholdPolicy/` Create/Update/Delete/BulkDelete handler'ları + validator'lar | Critical ≥ Warning, Blocking ≥ Warning, `EffectiveTo ≥ EffectiveFrom` korunur; kullanıcı girişli `PolicyCode` :179-187 alma (madde 9) → RCS `CTP`; `IsDefault` :88 alma (madde 12); `ApprovalStatus = Active` :89 alma (pack satır 365); `CurrencyCode` zorunlu (O-12); Update / Delete / BulkDelete kaynakta yok — yeni (pack satır 177) |
| `…Application/CashPositioning/Commands/CreateCashPositionThresholdPolicyCommand.cs` | `4267696c29205a16` · 25 | al-uyarla | `Commands/CreateCashPositionThresholdPolicyCommand.cs` + `Handlers/CommandHandlers/CreateCashPositionThresholdPolicyHandler.cs` | komut + handler aynı dosyada (:8, :11) alma; `Command`/`Query` soneki yok (pack satır 901-903) |
| `…Application/CashPositioning/Queries/CashPositionQueries.cs` | `63faa680dd25ad4a` · 77 | al-uyarla | `GetCashPositionListQuery.cs`, `GetCashPositionByIdQuery.cs`, `CashPositionListSort.cs` + handler'lar | kaynak `CursorPage` sayfalaması → DataTable sunucu tarafı (frontmatter satır 11-12); süzgeç Tenant + LE + `IFinanceTreasuryDataScopeSource` okuma kapsamı (pack satır 647-659); CP-10 / CP-11 detay içeriği DL-2/DL-3 (pack satır 147, 155-156) |
| `…Application/CashPositioning/Queries/CashPositionThresholdPolicyQueries.cs` | `b379247f7204c978` · 59 | al-uyarla | `GetCashPositionThresholdPolicyListQuery.cs`, `GetCashPositionThresholdPolicyByIdQuery.cs` + handler'lar | `data_mode: client`, ≤ 200 satır (frontmatter satır 13-15); `IsDefault` süzgeci alma |
| `…Application/CashPositioning/Contracts/CashPositionDtos.cs` | `74fed5b8977651b8` · 346 | al-uyarla (DL-1 DTO'ları) | `CashPositionModels.cs`, `CashPositionThresholdPolicyModels.cs` (tek dosya kuralı) | kaynak isteklerde `LegalEntityId` var (`:25`, `:85`, `:234`) — Recovery DTO / isteklerinde `TenantId` ve `LegalEntityId` **yok** (pack satır 191-192); `ExpectedVersion` / `SnapshotVersion` gövde alanları alma (madde 11) |
| `…Application/CashPositioning/Contracts/CashPositionPersistenceContracts.cs` | `715aab67804af52e` · 245 | yalnız referans | Recovery depo arayüzleri | `ICashPositionRepository` (:21) içe aktarma, outbox, kategori yöntemlerini de taşır; DL-1 yalnız taslak kaydet / oku / ara / politika yöntemlerini yeniden tasarlar |
| `…Application/Contracts/RuntimeContracts.cs` | `6029f0d9122d7ae8` · 57 | yalnız referans / alma | MOD-0126 DL-1'in servis ortak portları | `ICurrencyReferenceValidator` (:37) → yerine `ICurrencyReferencePort` (MOD-0126 DL-1; burada **yazılmaz**); `ILegalEntityReportingContextResolver` (:26) → yerine `ILegalEntityEligibilityPort` (Active + Approved, D14/4) — saat dilimi / taban para birimi alanları alma (O-10, O-7), yalnız içe aktarmada LE kontrolü alma (madde 8); `IBankAccountReferenceValidator` (:50) alma (MOD-0289 yok, madde 10); `ICurrentUserContext.UserId` `string` alma (madde 7) |
| `…/Contracts/ICashPositionVerifiedIdentityContext.cs` | `f8fc03d662340b46` · 18 | yalnız referans | Recovery `ICurrentUser` / JWT | servis içi kimlik bağlamı yeniden yazılmaz |
| `…/Commands/ImportCashPositionCommand.cs`, `IngestBankFeedCommand.cs` | `b089daf4997c6a59` · 27; `907b9f0efeba87b3` · 27 | alma (v1) | — | CP-03 ertelendi (pack satır 148, O-2/O-3); `IngestBankFeedCommand` madde 16 |
| `…/Commands/{ReconcileCashPositionCommand,EvidenceAndPublishCommands}.cs`, `Services/{CashPositionReconciliation,CashPositionEvidence,CashPositionApproval,CashPositionWorkflow,CashPositionPublish,CashPositionEvidenceExport}Service.cs`, `Queries/CashPositionCockpitReadQueries.cs`, `Contracts/{CashPositionWorkflow,CashPositionEvidenceExport,CashPositionCockpitRead}Contracts.cs`, `Common/CanonicalHash.cs` | — | sonraki dilim | DL-2 (mutabakat, istisna, kanıt, trace), DL-3 (onay, yayım, `PublishedContentHash`), DL-4 (dışa aktarım) | `DecisionStatus` doğrudan karar alma (madde 4); uzak-önce-yerel alma (madde 3); `outbox-status` CP-12 düşürülür (pack satır 157) |
| `…/Services/{CashPositionAuditEmitter,CashPositionAuditEventBuilder,CashPositionAuditDispatchService,CashPositionOutboxDispatchService}.cs`, `Contracts/{CashPositionAuditDispatch,CashPositionOutboxDispatch}Contracts.cs` | — | alma | CentralAudit yol b (MOD-0126 DL-1 kaydı) | madde 5, 17; her yazma komutu `ICentralAuditDeclaration` taşır (pack satır 908-910) |
| `…/Services/{CashCategoryService,CashCategoryAssignmentRuleService,CashPositionBankFeedIngestionService,CashPositionSourceContractValidationService}.cs`, `Commands/CreateCashCategory*.cs`, `Queries/CashCategory*.cs`, `Contracts/{BankFeedIngestion,SourceContractValidation,IntegrationRunHealth}Contracts.cs` | — | alma (v1) | — | kategori O-11 (CP-13…CP-18 ertelendi); banka beslemesi ve içe aktarma istemcileri madde 16 |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…Persistence/CashPositioning/MongoCashPositionRepository.cs` | `33db0e1d3d8cf736` · 1618 | al-uyarla (DL-1 kısmı: `SaveDraftSnapshotAsync` :27, `GetSnapshotDetailAsync` :170, `SearchSnapshotsAsync` :704, `SaveThresholdPolicyAsync` :759, `GetThresholdPolicyAsync` :816, `SearchThresholdPoliciesAsync` :826) | `Persistence/CashPositions/` depolar | kaynak taslak kaydı içe aktarma / outbox belgelerini aynı işlemde yazar (:27-73) — onlar alma (madde 5); kaynak politika kaydı işlemsiz `InsertOneAsync` (:759-815) → Recovery: işlem + slot CAS + `CentralAuditTransaction.EnlistAsync` (pack satır 369-371, 548-549); soft delete `IsDeleted` + `DeletedAt` (pack satır 680) — kaynakta hiçbir yol `IsDeleted = true` yazmaz (pack satır 250) |
| `…Persistence/CashPositioning/CashPositionMongoIndexes.cs` | `04ec8dd46aacfe18` · 263 | al-uyarla (DL-1 indeksleri) | indeks kataloğu | kaynak 20 ad, 12 tekil (ölçüldü, :8-30; pack satır 472 ile eşit); `mod0124_` önekleri alma (madde 15). DL-1 indeksleri (pack satır 476-479, 488-490): `ux_cash_position_code`, `ix_cash_position_list`, `ux_cash_position_line_key`, `ux_cash_position_threshold_policy_code`, `ux_cash_position_threshold_policy_slot`, `ix_cash_position_threshold_policy_selector`; `ux_cash_position_current_published` yayım yuvası DL-3'te kullanılır (indeksin DL-1'de mi kurulacağı pack'te yok). **DB tekilliği açıkça:** `ux_cash_position_code` = Unique(TenantId, SnapshotCode), **kısmi filtre YOK** — silinmiş anlık görüntünün kodu da dolu sayılır (pack satır 476, RCS-001 §6); kaynakta karşılığı yok (kod rastgeleydi). `ux_cash_position_threshold_policy_code` = Unique(TenantId, PolicyCode), **kısmi filtre yok** (pack satır 488) — kaynağın `ux_mod0124_threshold_policy_code` indeksi `IsDeleted = false` kısmi filtreli (:81-83) **alınmaz**. `ux_cash_position_line_key` = Unique(TenantId, SnapshotId, BankAccountRef, CashCategoryType) (pack satır 479). `ux_cash_position_threshold_policy_slot` = Unique(TenantId, LegalEntityId, CurrencyCode) (pack satır 489). Kaynağın `ux_mod0124_threshold_policy_default` indeksi alma (madde 12). **Kodun RCS'den gelmesi veritabanı tekilliğini kaldırmaz; ikisi birlikte.** Test: aynı kiracıda aynı kodla ikinci kayıt — silinmiş bir kaydın koduyla dahil — reddedilir (anlık görüntü ve politika için ayrı); sabotaj: indekse `IsDeleted=false` filtresi eklenince silinmiş-kod vakası kırmızıya döner. Makbuz indeksleri burada bildirilmez (MOD-0126 DL-1; pack satır 492-494) |
| `…Persistence/CashPositioning/TreasuryMongoDbContext.cs` | `749431fa806a48b8` · 34 | al-uyarla | `Persistence` bağlamı (MOD-0124 koleksiyonları) | `mod0124_*` koleksiyon adları alma (madde 15) → `cash_positions`, `cash_position_lines`, `cash_position_threshold_policies`, `cash_position_threshold_policy_slots` (pack satır 280, 313, 350, 468); kaynağın veritabanı adı yedeği `"DitenErpTreasury"` (:11-13) — Recovery'de servis iskeletinin bağlamı geçerlidir; pack'te yok |
| `…Persistence/CashPositioning/CashPositionIndexHostedService.cs` | `77e049e83c30ddcf` · 29 | al-uyarla | indeks hizmeti | pack satır 498 "servis iskeletinin indeks barındırılan hizmetiyle" der, satır 548 ve 1613 MOD-0124 "indeks hizmeti"nden söz eder — ayrı hizmet mi, iskeletin hizmetine kayıt mı, prompt'ta tek seçenekle yazılır (bkz. §6) |
| `…Persistence/CashPositioning/CashPositionMongoSerialization.cs` | `541dcb345fdbedf1` · 35 | yalnız referans | — | `DateOnly` dize serileştirici (:23); `ValueDate` `DateOnly` (pack satır 275); Recovery'de ortak serileştirici varsa o kullanılır — pack'te yok |
| — | — | **yeni** | `Persistence/CashPositions/` | `CashPositionThresholdPolicySlot` deposu + CAS (pack satır 369-371, 468-470); makbuz deposu **yazılmaz** (MOD-0126 DL-1) |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…Api/Controllers/CashPositionSnapshotsController.cs` | `b83cdaaca2716400` · 272 | al-uyarla (DL-1 uçları: liste :27, detay :62) | `Api/Controllers/CashPositionsController.cs` | `[Authorize]`-only (:14) alma (madde 1) → her aksiyonda `[HasPermission]`; `api/v{version}/cash-positioning/snapshots` ve `…/mod0124/…` takma rotaları alma (madde 15) → `/api/cash-positions` (pack satır 146-147, 170-172); `import` ucu (:80) alma (CP-03); diğer POST uçları DL-2/DL-3 |
| `…Api/Controllers/CashPositionThresholdPoliciesController.cs` | `e5a87f345d297763` · 97 | al-uyarla | `Api/Controllers/CashPositionThresholdPoliciesController.cs` | aynı (madde 1, 15) → `/api/cash-position-threshold-policies` (pack satır 160-162, 177) |
| `…Api/Controllers/CustomBaseController.cs` | `6b96a56af94debc5` · 193 | yalnız referans | Recovery `CustomBaseController` (iskeletten) | sorgu `tenantId` reddi (:191-192) davranışı korunur (pack satır 257-258); eşlenmemiş kodun 400'e düşmesi (:185) alma (madde 6) |
| `…Api/Program.cs`, `appsettings.json`, `…/{Application,Infrastructure,Persistence}/DependencyInjection.cs` | `627389196a0f848e` · 37; `8b266d053745e6a6` · 143; `983d696c8c14e2a2` · 55; `9acf42356e8f8a78` · 640; `b1e45ec96dc2eb28` · 55 | alma | MOD-0126 DL-1 iskeleti | iskelet, port 5071, DI, CentralAudit kaydı MOD-0126 DL-1'indir; MOD-0124 yalnız kendi handler / depo / indeks kayıtlarını ekler (pack satır 540-544) |
| `…Infrastructure/CashPositioning/ClaimsCashPositionVerifiedIdentityContext.cs` | `6b71fa8f4c79d973` · 35 | yalnız referans | — | Recovery JWT bağlamı |
| `…Infrastructure/CashPositioning/{CashPositionWorkflowClientAdapters,CashPositionWorkflowClientOptions,CashPositionEvidenceClientAdapters,CashPositionEvidenceClientOptions}.cs` | — | sonraki dilim | `Infrastructure/{Workflow,Evidence}/` (pack satır 550) | DL-3 / DL-4 |
| `…Infrastructure/CashPositioning/{CashPositionAuditDispatchClient*,CashPositionOutboxDispatcherHostedService,CashPositionOutboxSinks,BankFeedStatementClientAdapters,IntegrationRunHealthClientAdapters,SourceContractValidationClientAdapters}.cs` | — | alma | — | madde 5 (outbox, `LoggingNoOp` / FailClosed alıcılar), madde 16 |
| `…Infrastructure/PaymentProposals/CurrencyPrecisionFallback.cs` | — | alma | — | MOD-0126 sınıflandırmasında ölçüldü; sabit tablo uydurulmaz (pack satır 267-269) |

**Referans veri portları — DL-1 davranışı (portları MOD-0126 DL-1 yazar; MOD-0124 testleri davranışı kendi komutlarında ölçer):**

| Port (sahibi MOD-0126 DL-1) | Ret | Erişilemez | Dayanak |
|---|---|---|---|
| `ILegalEntityEligibilityPort` (MOD-0220 + TRM-14) | `Active` **ve** `Approved` değilse 422 `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`, kayıt değişmez | 503 `CASH_POSITION_LEGAL_ENTITY_UNAVAILABLE`, geçiş yok | pack satır 668-673, AC-2 satır 1227-1232; okumalar etkilenmez |
| `ICurrencyReferencePort` — kod listesi (MOD-0048) | listede değil → **400** `CASH_POSITION_INVALID_CURRENCY_CODE` (pack 422 değil 400 der) | 503 `CASH_POSITION_REFERENCE_UNAVAILABLE` | pack satır 507, 519, 978, 996 |
| `ICurrencyReferencePort` — küsurat hanesi (`currency-minor-units`, O-8) | fazla hane → 400, yuvarlama yok | 503 `CASH_POSITION_CURRENCY_PRECISION_UNAVAILABLE`, sabit tabloya düşülmez | pack satır 267-269, 1091, AC-4 |
| `IRecordCodePort` (RCS-001, `CPS` / `CTP`) | istekte kod → 400 | 503 `CASH_POSITION_RECORD_CODE_UNAVAILABLE`; servis yoksa Create yazılamaz | pack satır 520, 611, 989, AC-1 |
| `IFinanceTreasuryDataScopeSource` (SEC-002) | kapsam dışı → liste boş, detay / yazma 404 | çözücü hatası → boş kapsam (süzgeçsiz sorgu değil) | pack satır 647-667, 631, AC-31, AC-32 |
| MOD-0289 banka hesabı | **port yok** — yalnız `Guid ≠ Empty` | — | pack satır 318, 606 |

Kaynak karşılaştırma (yalnız referans): kaynak, bağımlılık hatasını `ReferenceServiceUnavailable` koduna, geçersiz kodu
`InvalidCurrencyCode`'a ayırır (`CashPositionPersistenceService.cs:97-111`); bu ayrımı ölçen
`CashPositionReferenceFailureTests.cs` aşağıda davranış listesi olarak kullanılır.

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak (`S/tests/Diten.ErpTreasuryService.Tests/`) | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `CashPositionPersistenceServiceTests.cs` | `1dacbeeb6def38b4` · 251 | taslak oluşturma, kiracı yalıtımı, para birimi hatası, karışık para birimi, yinelenen satır (AC-1, 3, 4); saat dilimi testi (:107) **alma** (O-10); maskeli hesap testi (:170) **alma** (madde 10) |
| `CashPositioning/CashPositionReferenceFailureTests.cs` | `74264b16f9dacb1d` · 140 | bağımlılık hatası ≠ geçersiz kod; zaman aşımı / bozuk yanıt → 503, yan etki yok (AC-2, AC-4 503 kısmı) |
| `CashPositionFailurePathHardeningTests.cs` | `99cfde10a25e8cac` · 220 | yinelenen anahtar → kontrollü kod, sürüm çakışması (AC-16 / 409 deseni); içe aktarma partisi testi (:41) **alma** |
| `CashPositionThresholdPolicyHandlerTests.cs` | `a3876c9dbed29052` · 302 | Critical < Warning, Blocking < Warning, geçerlilik aralığı, para birimi biçimi (AC-8); `PolicyCode` yinelenmesi (:132) → RCS + DB tekilliği; `IsDefault` testi (:150) **alma** (madde 12) |
| `CashPositionThresholdPolicyControllerTests.cs` | `71974ccc6886cc4a` · 182 | sorgu `tenantId` reddi → 400 `SCOPE_INPUT_FORBIDDEN`; `mod0124` takma rota testleri **alma** (madde 15) |
| `CashPositionControllerTests.cs` | `8b2206e50a0c4c50` · 189 | aynı (AC-3 / kapsam girdisi); takma rota testleri **alma** |
| `CashPositionSchemaAndIndexTests.cs` | `519e69f775a6f230` · 100 | hassas banka alanı taraması (AC-5); indeks kataloğu testi |
| `CashPositionPaginationTests.cs` | `04c9bfc2a456e1c8` · 88 | kararlı sıralama fikri; imleç sayfası yerine DataTable sunucu tarafı |
| `CashPositioning/CashPositionVerifiedIdentityContextTests.cs` | `b3ff5f39fc1bf937` · 164 | yalnız referans (başlık çakışması 400, doğrulanmamış kimlik) — Recovery JWT |
| `CashPositionLifecycleAuditEmissionTests.cs` | `479441e960b69f3f` · 202 | **alma** — "denetim hatası birincil işlemi bozmaz" davranışı Recovery'nin tersidir (madde 5; AC-17 sabotajı) |
| `CashPositionImportTests.cs` | `06fdd6397041fd2f` · 263 | **alma (v1)** — CP-03 ertelendi |
| Diğer `CashPosition*Tests.cs` (mutabakat, kanıt, onay, yayım, iş akışı, outbox, audit dispatch, banka beslemesi, kaynak sözleşmesi, cockpit) ve `CashCategory*Tests.cs` | — | DL-2…DL-4 ya da alma |

Devralınmış kaynak kanıtı (`MOD-0124 407/407`, pack satır 1464-1465) Recovery hazırlığının ölçüsü değildir.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

- `CreateCashPositionCommand` (HTTP ucu kaynakta yok; `BalanceSourceKind = ManualEntry`), `UpdateCashPositionCommand`,
  `DeleteCashPositionCommand`, `BulkDeleteCashPositionCommand` (yalnız hiç gönderilmemiş `Draft`/`Reconciled`, soft delete;
  pack satır 170-172, AC-22).
- Eşik politikası Update / Delete / BulkDelete + `CashPositionThresholdPolicySlot` CAS koruması (pack satır 177, 369-371;
  AC-8 sabotajı: slot CAS kaldırılınca eşzamanlı yazım testi kırmızıya döner).
- `ContentRevision` (Update'te +1) ve `Version` ayrımı (pack satır 291).
- Her yazma komutunda `Idempotency-Key` ve servis ortak makbuzun **kullanımı**: `Operation = mod0124:{Komut}`, Create /
  BulkDelete için `TargetId = null` (pack satır 455-458; AC-16). Makbuz tipi ve indeksleri yazılmaz.
- `UnknownTransactionCommitResult` → 503 `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN` (AC-19 Create kısmı; madde 2'nin karşılığı).
- Her yazma komutunda `ICentralAuditDeclaration` + `EnlistAsync`; DL-1 olayları `finance-treasury.cash-position.{created,updated,
  deleted,bulk-deleted}` ve `finance-treasury.cash-position-threshold-policy.{created,updated,deleted,bulk-deleted}` (pack satır
  1164-1167, 1177-1180). `Category`: D35/3 ile `Finance` — Platform değeri gelmeden uydurulmaz.
- Servis defterine (`tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md`, MOD-0126 ile ortak) MOD-0124 komut
  satırları; `AuditTrailStandardTests` `PinnedCounts` değişirse aynı değişiklikte, CT kararıyla (pack satır 558-560; AC-18).
- 20 izin anahtarından DL-1 uçlarının kullandıkları (`cash-positions.{read,read-tenant-wide,create,update,delete,bulk-delete}`,
  `cash-position-threshold-policies.{read,create,update,delete,bulk-delete}`; pack satır 1121-1126, 1136-1140); kayıt MOD-0018.
- Okuma / yazma kapsamı ayrımı: `read-tenant-wide` yazma yetkisi vermez (AC-32 sabotajı).
- `docs/analysis/contracts/cash-position.openapi.yaml` v1.0.0 (DL-1 uçları; DL-3'te `current` eklenir — pack satır 561-562,
  625; AC-28).
- `CASH_POSITION_*` hata sözlüğü + resx ileti anahtarları.

## 4. WP prompt iskeleti (doldurulacak alanlar)

Biçim: main'deki örnek `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.3.md`. Hazır olunca
`execution/domains/finance-treasury/work-packs/prompts/WP-0124-DL1-v1.0.md` olarak yazılır (öneri; yol pack'te yok).

| Alan | Değer (taslak) | Açık |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` (+ §17.4 giriş alanları) | — (iskelet MOD-0126 DL-1'de; bu WP `/add-module` koşmaz) |
| WP / Prompt | `WP-0124-DL1` · `TRM-WP-0124-DL1-P v1.0` | kuyruk satırı (TRM kuyruğuna WP satırı) |
| Repository / Branch / Expected HEAD / Worktree | pack dalı `feature/fin/mod-0124-cash-positioning` (pack satır 19) · Expected HEAD = MOD-0126 DL-1 **ve** MOD-0124 `ready-for-dev` onayı main'deyken alınan commit | dal adı WP başına mı pack başına mı — CT kararı; dağıtım günü |
| Risk | HIGH — sinyaller: kiracı, veri kapsamı, denetim, sözleşme, para (decimal) | — |
| Görev sınıfı · Paket | ürün · MOD-0124 pack | — |
| İzinli yollar | pack "Repo Scope" DL-1 satırları: `Features/CashPosition/**`, `Features/CashPositionThresholdPolicy/**`, `Domain/CashPositions/**`, `Persistence/CashPositions/**`, iki denetleyici, defter satırları, `cash-position.openapi.yaml` (pack satır 545-562) | — |
| Korunan yollar | pack "Protected Paths" (satır 581-592) + `Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`, `Infrastructure` LE / para birimi / kod / `DataScope` bağdaştırıcıları (MOD-0126 DL-1'in), `.antigravity/**`, `ocelot.json`, MOD-0126 / 0119 / 0120 paketleri | — |
| Kapsanan dilim | DL-1 | — |
| Önce oku | pack, MOD-0126 pack (ortak tanımlar, sabitlenmiş sürüm), AGENTS.md, domain-config, multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent, code-style, audit-trail-standard, data-scope-enforcement, record-coding-standard, mongo-indexing, handler-design, D1–D35 karar kayıtları, bu hazırlık kaydı | MOD-0126 pack sürüm sabitlemesi (bkz. §6) |
| Sözleşme yüzeyleri | API (`cash-position.openapi.yaml`), İzin (`finance-treasury.*`), Test (AC-1, 2, 3, 4, 5, 8, 16, 17, 18, 19, 21, 22, 28, 31, 32 — pack satır 1618-1619), Sabotaj (AC-8 slot CAS, AC-17 outbox, AC-31 kapsam süzgeci, AC-32 yazma kapsamı, DB kod tekilliği kısmi filtresi) | AC metinleri pack satır numaralarıyla alıntılanır |
| Keşif | `verify_module_id.py --discover "cash positioning"` raporu, yol@commit + sha256 | dağıtım günü koşulur |
| Kod alma izi | §2'deki **al-uyarla** satırları, her biri `path@da4abb20 + sha256` ile WP raporuna | — |
| Uzunluk | ~10 bin token'ı aşarsa işçiye dosya yolu + SHA-256 ile verilir (S-52) | — |

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynak düzeltmesi yok.
- Sınıflandırma okuma ve paket metniyle yapıldı; kaynak testler bu oturumda **çalıştırılmadı**; Recovery'de kod ya da test
  koşulmadı.
- D34 / D35 metni bu worktree'de okunmadı (kayıt PR #235 / `a181e933b`'de); atıflar o kayda dayanır.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

## 6. Pack'te bulunan tutarsızlıklar (pack değişmedi; `ready-for-dev` revizyonunda CT işi)

1. **O-3 durumu:** "Bağımlılık sözleşmeleri" tablosu "MOD-0037 kapı kararı — sahip kararı **yok** (O-3) — DL-1 önkoşulu" diyor
   (pack satır 627); Checklist ve Açık kararlar D29/1 ile sahip payını verilmiş sayıyor (satır 1437, 1572).
2. **D30 sonrası bayat liste:** "Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler" AGENTS §2/§3/§9, `ports.md` 5071 ve
   RCS-001 §3 `CPS`/`CTP` satırlarını hâlâ bekleyen iş gibi sayıyor (satır 574-576); satır 614, 1439, 1443, 1578 bunların
   uygulandığını (D30) yazıyor.
3. **AC-29 dilimi:** CentralAudit satırı "AUD-001 §8 dev kanıtı AC-29" için dilim DL-1 diyor (satır 632); AC-29 DL-1 bitiş
   listesinde yok, DL-4 bitişinde var (satır 1618-1619, 1631).
4. **`Finance` kategorisi D35 ile eskidi:** Audited Events "karar çıkana kadar değer uydurulmaz" diyor (satır 1158-1159);
   D35/3 `Finance`'in MOD-0126 DL-1'den önce Platform'a eklenmesini karara bağladı. Pack D34 / D35'i hiç anmıyor.
5. **İndeks hizmetinin sahibi belirsiz:** satır 498 "servis iskeletinin indeks barındırılan hizmetiyle", satır 548 ve 1613
   MOD-0124'ün "indeks hizmeti" diyor.
6. **MOD-0126 ortak tanımları sürümsüz:** satır 1456 "MOD-0126 pack @bu commit sha256 ZIP manifestine bakın" — somut
   commit / sha256 yok; çelişkide o sürüm bağlayıcı sayıldığından (satır 1457; satır 61-62 ile birlikte) prompt'ta sabitlenmeli.
7. **Kaynak adlandırması:** pack v1 elle girişin kaynak karşılığını adlandırmıyor; kaynakta HTTP'siz `CreateDraftSnapshotAsync`
   (`CashPositionPersistenceService.cs:485`) var (§2 gözlemi) — çelişki değil, eksik iz.
