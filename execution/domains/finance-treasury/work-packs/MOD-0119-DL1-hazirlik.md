---
record_id: MOD-0119-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0119-accounts-payable.md (status draft, sha256 701cb9e9…, son değişiklik 393baf735; bu worktree HEAD ce6fb0ed3)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5)
---

> **Güncelleme (2026-10-26) — pack revizyon turu 02'ye bağlandı.** Bu kayıt pack'in `701cb9e9…` sürümüne göre
> yazıldı. Güncel ve incelenmiş sürüm: sha256 `f97cd3fa670f41c49776d02b0866bf282ad4a987fa5887e21d19cf289d78ef31` (dal `docs/ct/treasury-packs-rev-02` @ `c5df75338`,
> CT-APR-02 Codex UYGUN, açık bulgu 0). Bu kayıtta "pack'te tutarsızlık" olarak listelenen maddeler sahip denetimi gap
> listesi 21 ile pack'e işlendi (`docs/records/audits/2026-10/treasury-intake-handover-review-01/GAP-LIST-21-PREP.md`);
> geçerli olan pack metnidir. Buradaki "pack satır N" atıfları eski sürümün satırlarıdır; WP prompt'u alıntıları yeni
> sürümden yeniden alır.

# MOD-0119 DL-1 hazırlığı — AP kaydı çekirdeği ve doğrulama

Bu kayıt, MOD-0119 `ready-for-dev` olduğunda DL-1 WP prompt'unun hızla yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

**DL-1 kapsamı (pack satır 1531-1536):** `Features/ApInvoice/` içinde Create / Update / Delete / BulkDelete / Validate +
liste / detay sorguları, Domain / Persistence, bağdaştırıcılar, RCS kod portu, `ap-invoice.openapi.yaml`; alacak dekontunun
Validate ön kontrolü (kapasite, orijinal kuralı). Bitiş: AC-1…6, 15 (Create kısmı), 17 (tek aşamalı), 18…22, 28, 31, 32,
33 (Create / Update / Validate kısmı), 40 (Validate kısmı).

**Servis ortak yapı taşları MOD-0119'un değildir.** `Diten.FinanceTreasuryService` iskeleti, `FinanceTreasuryCommandReceipt`
+ iki indeksi, `IFinanceTreasuryDataScopeSource`, `ILegalEntityEligibilityPort`, `ICurrencyReferencePort`,
`IRecordCodePort`, bunların bağdaştırıcıları (`LegalEntities`, `Currency`, `RecordCodes`, `DataScope`) ve CentralAudit
kaydı (`AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)`) **yalnız MOD-0126 DL-1'de** açılır
(pack satır 128-133, 441-455, 513, O-21 satır 1513). MOD-0119 DL-1 bunları **yeniden tanımlamaz, açmaz, değiştirmez**;
yalnız kullanır. Değişiklik gerekirse dört Treasury paketinin onayı gerekir (pack satır 133, 486-488).

## 1. Başlama koşulları (bugün açık olanlar)

DL-1 prompt'u ancak bunlar kapanınca dağıtılabilir (pack "Ready-for-dev Checklist" satır 1331-1354 ve "Teslim dilimleri"
satır 1533-1535):

| Koşul | Pack dayanağı | Bugün | Kimden |
|---|---|---|---|
| Paket künyesi `ready-for-dev` + o sürüme CT-APR-02 ve CT-APR-01 (D34) | satır 1335-1336 | `draft` | CT + Beste |
| DCP numarası ve `approved` | satır 1337, 515 | `UNASSIGNED`, `draft` | Beste |
| **MOD-0126 DL-1** — iskelet, servis ortak yapı taşları, CentralAudit kaydı | satır 1338, 513, 1534 | yok; MOD-0126 pack'i de `draft` (hazırlık kaydı `MOD-0126-DL1-hazirlik.md`) | MOD-0126 WP |
| D35 — denetim defteri yol `b`, borç / istisna 0; Platform `AuditCategory`'ye `Finance` (MOD-0126 DL-1'den önce) | pack O-18 satır 1510 ve satır 1049-1050 hâlâ **TBD** — pack D35'i anmıyor | karar verildi (D35); `Finance` değeri yok | Platform sahibi |
| TRM-14: MOD-0220 `lookup-validation` yanıtında `ApprovalStatus` + sözleşme belgesi | satır 1340, 505, 528 | onay turu açık (D34/1) | Beste |
| MOD-0140 `supplier.openapi.yaml` 1.3.0 main'de — "1.3.0 main'e girmeden DL-1 başlamaz" | satır 1341, 506, 529 | main'de 1.2.0 | Cihan (MVP-2) |
| SEC-002 veri kapsamı tüketim yolu (O-22) | satır 1342, 511, 533 | yok | Beste |
| RCS-001 ortak kod servisi (BL-565, O-9); önek `APD` D29/2 ile kabul, §3 satırı D30 ile uygulandı | satır 1346, 512, 532 | servis yok | sahip belirlenmedi |
| O-10 para birimi küsurat hanesi (MOD-0048-FU01 §21 `currency-minor-units`, TRM-17) | satır 1347, 508, 1502 | önerildi; onay turu açık | Beste |
| O-20 AP rolleri + MOD-0018 `finance-treasury.ap-*` izin tohumu | satır 1512, 510, 1393 | sahip payı kabul (D29/1); MOD-0018 sahibi teyidi açık | Beste |
| MOD-0143 `invoice-match.openapi.yaml` 1.1.1 açıklaması + `lineAmount` tutarlılık kontrolü (422) | satır 899, 1352-1353 — pack: **ready-for-dev engeli değil** (D26/3) | main'de 1.1.0 FROZEN | Cihan |
| MOD-0023 §17 WP (TRM-15) | satır 1349-1351, 1540 — **DL-2 önkoşulu, DL-1 değil** | açık | Ali |
| MOD-0048 `payment-method` kümesi | **pack'te yok** — AP DL-1 ödeme yöntemi taşımıyor (MOD-0126 bekleyişi) | — | (Beste, MOD-0126 için) |
| Port 5071 (D30) | satır 1339 | kapandı | — |
| Gateway: bağımlı servis rotalarının varlığı | satır 1111-1112 ("DL-1/DL-2 başında ölçülür"); `/api/ap-invoices` rotası integration-agent task'ı (satır 1100-1104) | ölçülmedi | integration-agent |

**Dayanak kararlar:** `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` — D34 (onay
`ready-for-dev` turunda; DCP numarası önce ve ayrıca istenir) PR #235 ile taşınıyor, yerel commit `a181e933b` (main'de
değil); D35 (denetim defteri yol `b`, borç 0, Platform `AuditCategory` `Finance` MOD-0126 DL-1'den önce). Pack bu iki
kararı henüz anmıyor (pack D27–D30'a kadar günceldir); bu kayıt pack'i değiştirmez.

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet: `shasum -a 256` (ilk 16 hane), satır sayısı
`wc -l`, ikisi de @da4abb20, bu oturumda `git archive` ile ölçüldü. Pack atıfları `@218acaf5` biçimindedir (pack satır
58-59); pack'e göre bağlı AP dosyaları iki commit'te eşittir (satır 1366-1368). Bu oturumda örneklenen atıflar @da4abb20'de
aynı satırlarda bulundu (`AccountsPayableEntities.cs:5/57/71/84`, `AccountsPayableEnums.cs:16-29/42-52/71-76`,
`AccountsPayableController.cs:17-18/215/221`, `AccountsPayableSecurity.cs:269-273/284-294/328-338`,
`AccountsPayableFoundationService.cs:465-481`, `MongoAccountsPayableRepository.cs:73-74`,
`Application/DependencyInjection.cs:46`). Karar dili: **al-uyarla** (doğrula, Recovery düzenine göre yeniden yaz, iz
kaydına `path@da4abb20 + sha256`), **yalnız referans** (davranışı okunur, kod alınmaz), **alma** (do-not-carry maddesiyle,
pack satır 1406-1442), **sonraki dilim**. Pack'in kod alma sınırı: "varlık 1–3, durum korumaları, doğrulayıcının para
birimi/satır/negatif tutar kuralları" (pack satır 1372-1373).

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/AccountsPayable/AccountsPayableEntities.cs` | `a3eda3c6b01cdd83` · 394 | al-uyarla (yalnız `ApInvoiceControlRecord` :5-55 → `ApInvoice`, `ApInvoiceLine` :57-69) | `Domain/ApInvoices/` | `TenantEntity` yerine `EntityBase`, `DateTimeOffset`, `int Version`, Guid aktör (madde 10); `Guid SupplierReferenceId` → `string SupplierId` (pack satır 253); `IdempotencyKey` :47 **alma** (madde 9); banka alanları :50-53 **alma** (madde 13); satırda `PurchaseOrderRef` / `GoodsReceiptRef` taşınmaz (pack satır 110); dış ERP alanları taşınmaz (pack satır 276-283). `ApException` :84-102 → DL-3. Diğer 13 sınıf **alma** (pack satır 112-120; snapshot madde 12) |
| `S/src/…Domain/AccountsPayable/AccountsPayableEnums.cs` | `2c68e99a11b54ea6` · 248 | al-uyarla (alt küme) | `Domain/ApInvoices/` | `ApDocumentType` :3-8, `ApDocumentDirection` :10-14 aynen; `ApInvoiceStatus` 11 değerden 6'sı, sayısal değerler korunur (Received 0, Validated 1, ApprovalPending 5, Approved 6, Rejected 7, OnHold 8 — pack satır 183-198); `ApApprovalStatus` 8 değerden 6'sı (`NotRequired`, `Delegated` alma — pack satır 199-203); `ApHoldStatus` DL-3 (`Released` alma, pack satır 267); istisna enum'ları DL-3; kalanlar alma |
| `S/src/…Domain/AccountsPayable/AccountsPayableStateGuards.cs` | `2f1c9699440bfa8d` · 38 | al-uyarla (daraltarak) | `ApInvoiceStateMachine` (tek domain tablosu, pack satır 813-814) | DL-1 geçişleri: Received → Validated, Validated → Received (Update; **kaynakta yok**, yeni), silme yalnız `SubmissionSequence = 0`. İstisna geçişleri :21-31 → DL-3 |
| `S/src/…Domain/Common/TenantEntity.cs` | `2ef3028110d99e9e` · 26 | alma | servis `EntityBase` (MOD-0126 DL-1 iskeleti) | madde 10 (`string` aktör, `DateTime`); pack satır 224-227 |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/AccountsPayable/Common/AccountsPayableErrorCodes.cs` | `399ff52e8dc1a21e` · 45 | al-uyarla (DL-1 alt kümesi, yeniden adlandırarak) | `Features/ApInvoice/` hata sözlüğü | 40 kod (sayıldı, pack satır 406 ile eşit); tek `AP_` öneki, `MOD0119_*` **alma** (madde 16); taşınan / yeni / taşınmayan listesi pack satır 408-433. İleti resx anahtarı |
| `…/AccountsPayable/Commands/AccountsPayableCommands.cs` | `bb22e46c97187dbc` · 272 | yalnız referans (`RegisterApInvoiceControlRecordCommand` :8-25, `ProcessApInvoiceImportCommand` :27-44); geri kalan alma | `Commands/CreateApInvoiceCommand.cs` + `Handlers/CommandHandlers/CreateApInvoiceHandler.cs` | 14 komut + handler tek dosyada **alma** (pack satır 804-807); AP-09 dış ERP yükü yerine elle kayıt (pack satır 145); `UpsertApExceptionCommand` → DL-3 (upsert değil açma, pack satır 147); ödeme, projeksiyon, kanıt, olay komutları v1 dışı (pack satır 141-151) |
| `…/AccountsPayable/Contracts/AccountsPayableDtos.cs` | `215e61394e33f8c3` · 559 | yalnız referans (`ApInvoiceListItemDto` :197, `ApInvoiceDetailDto` :219, `ApInvoiceLineDto` :225, `ApInvoiceSearchCriteria` :236) | `ApInvoiceModels.cs` (tek dosya, pack satır 171-175) | `ApInvoiceImportRequest` :16-52 **alma** — gövdede `TenantId` / `LegalEntityId` :17-18 (madde 5), istemciden kontrolsüz tutarlar :28-38 (madde 7). DTO'larda `TenantId` / `LegalEntityId` yok |
| `…/AccountsPayable/Contracts/AccountsPayablePersistenceContracts.cs` | `fdcc056b4b9f1e0a` · 210 | yalnız referans (`IAccountsPayableRepository` :59, fatura yöntemleri) | `Persistence/ApInvoices/` depo arayüzü | projeksiyon / mutabakat / içe aktarma ölçütleri alma (O-4) |
| `…/AccountsPayable/Queries/AccountsPayableQueries.cs` | `b3b5a5eef4f7ff2e` · 165 | al-uyarla (`GetApInvoiceControlRecordDetailQuery` :8-25, `SearchApInvoiceControlRecordsQuery` :27-44) | `Queries/GetApInvoiceListQuery.cs`, `GetApInvoiceByIdQuery.cs`, `ApInvoiceListSort.cs` + `Handlers/QueryHandlers/` | sorgu + handler aynı dosyada → bölünür; DataTable sunucu tarafı; `legalEntityId` sorgu parametresi yerine `X-Legal-Entity-Id` (pack satır 139); kapsam süzgeci `IFinanceTreasuryDataScopeSource` (MOD-0126 DL-1'in); AP-03…08 sorguları alma |
| `…/AccountsPayable/Security/AccountsPayableSecurity.cs` | `0ac98fe26ab96f70` · 436 | alma | `[HasPermission]`, `ILegalEntityEligibilityPort` (ortak), SoD DL-2 | NoOp denetim :269-273 (madde 1); `FailClosed…LegalEntityAccessGuard` :284-294 ve `FoundationOnly…SodPolicy` :328-338 (madde 3); break-glass :314-317 (madde 11); `string` aktör :302, 324 (madde 10); `ap.*` anahtarlar :9-38 ve işlem kodları :70-88 (madde 16) |
| `…/AccountsPayable/Services/AccountsPayableFoundationService.cs` | `a7beb5c9f606c40a` · 3333 | yalnız referans | Create / Update / Delete / BulkDelete / Validate handler'ları + domain (`handler-design`) | pack'in kod alma sınırı dışında (satır 1372-1373); içe aktarma :305-527 davranışı okunur; koddaki mükerrer kontrolü :465-481 **alma** (madde 8); `Success` dönen başarısızlık :321-329, :499-507 **alma** (madde 6); önce-yaz-sonra-denetle :1785→1790, :1948→1953 **alma** (madde 1) |
| `…/AccountsPayable/Validation/AccountsPayableFoundationValidator.cs` | `af636f4c2e6936df` · 196 | al-uyarla (yalnız `DueDate ≥ InvoiceDate` :35-40, negatif tutar :42-52, en az 1 satır :70-75, satır para birimi = başlık :77-85) | `Validators/CreateApInvoiceValidator.cs`, `UpdateApInvoiceValidator.cs`, `GetApInvoiceListValidator.cs` | maskeleme :55-68 **alma** (madde 13); `TenantId` / `LegalEntityId` zorunluluğu :22-23 **alma** (madde 5); kaynak satır para birimini `OrdinalIgnoreCase` karşılaştırır (:78) — Recovery önce büyük harfe normalize eder (pack satır 865); yeni kurallar: 1..200 satır, `GrossAmount > 0`, hane sınırı, `InvoiceDate ≤ bugün` (UTC, O-19), sunucu alanı istekte → 400 (pack satır 854-881) |
| `…/Infrastructure/AccountsPayable/ClaimsAccountsPayableSecurityContextAccessor.cs` | `8e118ea5075734d6` · 134 | yalnız referans | Recovery `ICurrentUser` / JWT | pack'te ad geçmiyor; kimlik bağlamı servis içinde yeniden yazılmaz |
| `…/Application/DependencyInjection.cs` (AP satırları :41-49) | `983d696c8c14e2a2` · 55 | alma | yeni DI | NoOp sink :46 (madde 1), `FailClosed` LE guard :47 ve `FoundationOnly` SoD :48 (madde 3) |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/AccountsPayable/MongoAccountsPayableRepository.cs` | `b781355dba943d51` · 1086 | al-uyarla (DL-1 kısmı: fatura + satır yazma / okuma) | `Persistence/ApInvoices/` depolar | `RowVersion == expectedVersion` (:73-74) → `int Version` CAS, bayat yazma 409 `AP_INVOICE_VERSION_CONFLICT`; her işlem geri çağrısı `CentralAuditTransaction.EnlistAsync` (pack satır 448-449); başlık + satırlar + makbuz + denetim aynı işlemde (pack satır 296); soft delete `IsDeleted` + `DeletedAt` **yeni** (kaynakta silme yolu yok, pack satır 226-227) |
| `…/Persistence/AccountsPayable/AccountsPayableMongoIndexes.cs` | `5bdf3339fe07c4c2` · 251 | al-uyarla (yalnız kurulum deseni) | indeks kataloğu | 26 kaynak adı (sayıldı, pack satır 379 ile eşit) **alınmaz** — `mod0119_` adlar; tekil olmayan `ix_mod0119_invoice_business_signature` :70-79 (madde 8); `ux_mod0119_import_idempotency_key` :98-103 (madde 9); `ix_mod0119_active_exception_lookup` (madde 14, DL-3). Aşağıdaki **veritabanı tekilliği** tablosuna bakın |
| `…/Persistence/AccountsPayable/AccountsPayableMongoContext.cs` | `4f63d0a8fa3073a6` · 86 | al-uyarla | `Persistence/ApInvoices/` bağlamı | koleksiyonlar `ap_invoices`, `ap_invoice_lines` (pack satır 246, 285); kaynağın 17 `mod0119_ap_*` koleksiyon adı alma |
| `…/Persistence/AccountsPayable/AccountsPayableIndexHostedService.cs` | `514acce8893aa6cc` · 29 | al-uyarla | indeks hizmeti (`mongo-indexing`) | taşıma / geri doldurma yok (pack satır 402-404, AC-31) |
| — | — | **kullanılır, yazılmaz** | `Persistence/Common/CommandReceipts/**` | `FinanceTreasuryCommandReceipt` + `ux_finance_treasury_command_receipt_scope`, `ix_finance_treasury_command_receipt_pending` MOD-0126 DL-1'in; AP yeniden bildirmez (pack satır 397-400). AP yalnız `Operation = mod0119:{Komut}`, `TargetId` (Create / BulkDelete `null`) değerlerini verir (pack satır 368-369) |

**Veritabanı tekilliği (DL-1 indeksleri, pack satır 381-395; tekillik kod kontrolüyle değil indeksle):**

| Recovery indeksi | Anahtar | Tekil · filtre | Silinmiş satır | DL-1 testi |
|---|---|---|---|---|
| `ux_ap_invoice_code` | TenantId + InvoiceCode | **evet, kısmi filtre YOK** (pack satır 383, RCS-001 §6) | **dahil** — silinmiş faturanın kodu da dolu sayılır (AC-22, pack satır 1183) | Kodun RCS-001'den gelmesi veritabanı tekilliğini kaldırmaz; ikisi birlikte. Test: aynı kiracıda aynı kodla ikinci kayıt — silinmiş bir faturanın koduyla dahil — reddedilir. Sabotaj önerisi (pack'te bu indeks için yazılı değil): indekse `IsDeleted = false` filtresi eklenince silinmiş-kod vakası kırmızıya döner |
| `ux_ap_invoice_business_key` | TenantId + LegalEntityId + SupplierId + DocumentType + InvoiceNumberNormalized | **evet**, kısmi `IsDeleted = false` (pack satır 384; O-6 D28, `Rejected` de engeller — satır 1498) | **hariç** — silinen faturanın numarası yeniden girilebilir (AC-22, pack satır 1184) | AC-6: 409 `AP_INVOICE_DUPLICATE`; eşzamanlı iki Create → biri 201, diğeri 409 (gerçek replica set); tekil indeks kaldırılınca kırmızı (sabotaj, pack satır 1139-1141) |
| `ux_ap_invoice_match_ref` | TenantId + LegalEntityId + MatchInvoiceRef | **evet**, kısmi `MatchInvoiceRef` var **ve** `IsDeleted = false` (pack satır 385) | hariç | AC-5: aynı MOD-0143 faturasına ikinci kayıt 409 `AP_MATCH_REFERENCE_ALREADY_USED` (pack satır 922, 1138) |
| `ix_ap_invoice_list`, `ix_ap_invoice_supplier`, `ix_ap_invoice_line_invoice` | pack satır 390-391, 393 | hayır | — | — |
| `ix_ap_invoice_workflow_instance` | TenantId + WorkflowInstanceId (kısmi) | hayır | — | dilimi pack'te yok; alan DL-2'de dolar — öneri DL-2 |
| `ux_ap_start_intent_*`, `ux_ap_decision_open`, `ix_ap_decision_invoice` / `ux_ap_exception_active`, `ix_ap_exception_invoice` | pack satır 386-389, 394-395 | — | — | **sonraki dilim** (DL-2 / DL-3) |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| — (kaynakta MOD-0140 / MOD-0143 bağdaştırıcısı yok; `grep` 0) | — | **yeni** | `Infrastructure/Suppliers/`, `Infrastructure/InvoiceMatch/` (pack satır 450) | AP'ye özgü portlar; aşağıdaki "Referans veri portları" |
| `…/Infrastructure/DependencyInjection.cs` | `9acf42356e8f8a78` · 640 | alma | yeni DI | MOD-0125 kodu ve başka modül kayıtları :2-15 (madde 18); zaman aşımsız HTTP istemcileri (madde 19); @218acaf5'ten farklı dosya (pack satır 1367-1368) |
| `…/Api/Controllers/AccountsPayableController.cs` | `4882e500a881744e` · 524 | al-uyarla (yalnız AP-01 :34, AP-02 :68, AP-09 :203 davranışı) | `Api/Controllers/ApInvoicesController.cs` | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]`; `[Authorize]`-only :17 ve `EnsureAccess` :215 **alma** (madde 4); gövde kiracısı :221 **alma** (madde 5); rota `api/v{version}/mod0119/accounts-payable` :18 **alma** (madde 16) → `/api/ap-invoices`; diğer 14 uç v1 dışı (pack satır 141-151) |

**Referans veri / bağımlılık portları — DL-1 davranışı (pack'in istediği hata sözleşmesi):**

| Port | Sahibi | Reddedilir (422 / 409 / 400) | Erişilemez (503) | Pack dayanağı |
|---|---|---|---|---|
| `ILegalEntityEligibilityPort` (MOD-0220 + `ApprovalStatus`) | **MOD-0126 DL-1** (AP kullanır) | Active + Approved değil → 422 `AP_LEGAL_ENTITY_NOT_ELIGIBLE`, kayıt değişmez; okuma etkilenmez | 503 `AP_LEGAL_ENTITY_UNAVAILABLE`, geçiş yok | satır 414, 858, 928-929, AC-2 |
| `ICurrencyReferencePort` (MOD-0048 para birimi + küsurat hanesi) | **MOD-0126 DL-1** | hane aşımı → 400 (yuvarlama yok); kod MOD-0048 listesinde değilse hata kodu **pack'te yok** | hane kaynağı yok → 503 `AP_CURRENCY_PRECISION_UNAVAILABLE`; sabit tablo uydurulmaz | satır 419, 508, 865, 871, 979, AC-21 |
| `IRecordCodePort` (RCS-001, önek `APD`) | **MOD-0126 DL-1** | istekte gönderilen kod → 400 | 503 `AP_RECORD_CODE_UNAVAILABLE`; servis yoksa Create yazılamaz | satır 419, 512, 873, AC-1 |
| `IFinanceTreasuryDataScopeSource` (SEC-002) | **MOD-0126 DL-1** | kapsam dışı şirket → liste boş, detay / yazma 404 `AP_INVOICE_NOT_FOUND`; yazmada `read-tenant-wide` kapsam genişletmez | çözücü hatası → boş kapsam, boş liste, detay / yazma 404; `NoOp` kaydı yasak | satır 533, 931-935, AC-32, AC-33 |
| `ISupplierEligibilityPort` (MOD-0140 `POST /api/suppliers/validate`, 1.3.0) | **MOD-0119 DL-1** (yeni) | ilişki yok / Active değil (OnHold / Blocked / Inactive / ilişkisiz) → 422 `AP_SUPPLIER_NOT_ELIGIBLE` | 503 `AP_SUPPLIER_UNAVAILABLE`, geçiş yok | satır 414-415, 506, 529, 926-927, AC-4 |
| `IInvoiceMatchPort` (MOD-0143 `GET /api/invoice-match/invoices/{invoiceId}`, 1.1.0) | **MOD-0119 DL-1** (yeni) | durum `{Matched, MatchedWithinTolerance}` dışı → 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE`; tedarikçi / para birimi / `Σ Net ≠ totalAmount` → 422 `AP_MATCH_REFERENCE_MISMATCH`; ikinci bağ → 409; ref eksik / yasak → 400 | 503 `AP_MATCH_UNAVAILABLE`, geçiş yok | satır 415-417, 507, 530, 888-899, 922-925, AC-5, AC-40 |

Hepsi kullanıcı token'ıyla, yapılandırılmış zaman aşımıyla çağrılır; adresler yapılandırmadan, eksikse servis başlamaz
(pack satır 1109-1111, madde 19). Kullanıcının bağımlı servis okuma izinleri pack satır 1028-1032.

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `S/tests/…/AccountsPayable/AccountsPayableFoundationTests.cs` | `e4a744c40274b367` · 2822 | `RegisterInvoice_*` (kiracı bağlamı, kiracı izolasyonu, idempotent tekrar, mükerrer) → AC-1, 3, 6, 15; `InMemoryAccountsPayableRepository` :2179 ve `InMemoryAccountsPayableControlAuditSink` :2147 **alma** — Recovery gerçek Mongo replika kümesi ister (pack satır 1286); projeksiyon / mutabakat / kanıt / olay testleri alma |
| `…/AccountsPayableTransactionTests.cs` | `b998538815ef936c` · 891 | `Success_and_exact_replay_commit_once`, `Stale_writer_cannot_replace_committed_invoice`, `Reused_key_with_different_payload_is_conflict`, `Index_conflict_is_not_swallowed_by_hosted_startup` → AC-15, CAS 409, indeks kurulumu; geri kalan projeksiyon / kanıt alma |
| `…/AccountsPayableControllerTests.cs` | `5b44763b69fb0f6f` · 722 | uç + izin davranışı (AC-20) — kaynak `[Authorize]` + `EnsureAccess` olduğu için yalnız senaryo listesi |
| `…/AccountsPayableVerifiedContextTests.cs` | `883d06cfe0f257fb` · 106 | başlık kiracısının talebi geçememesi, LE kapsamı yoksa boş → AC-3, AC-32 senaryoları |
| `…/AccountsPayableFailureOwnershipTests.cs` | `7450d64d78a555b7` · 111 | içe aktarma hatası sahipliği alma (O-4); `Business_scope_filter_applies_before_limit` deseni AC-32 için referans |
| `…/AccountsPayableProtectedPathTests.cs` | `c099492e91ea8938` · 78 | göç / şema dosyası yok → AC-31 benzeri repo taraması |
| `…/AccountsPayableControllerRouteTemplateTests.cs` | `626132efa1b9dd36` · 96 | kanıt manifesti rotaları — alma (v1 dışı) |

Yedi test dosyasında 159 `[Fact]` / `[Theory]` özniteliği sayıldı (`grep -c`). Devralınmış kaynak kanıtı (`MOD-0119 TEST
251/251 @ da4abb20`) Recovery hazırlığının ölçüsü değildir (pack satır 1302-1303); kaynakta hiç olmayan testler (mükerrer
yarışı, satır toplamı tutarlılığı) DL-1'de zorunludur.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

- `Features/ApInvoice/` Golden Reference Compact düzeni (pack satır 784-814): `CreateApInvoiceCommand`,
  `UpdateApInvoiceCommand`, `DeleteApInvoiceCommand`, `BulkDeleteApInvoiceCommand`, `ValidateApInvoiceCommand` +
  handler'lar (suffix yok), `GetApInvoiceListQuery`, `GetApInvoiceByIdQuery`, `ApInvoiceListSort`, `ApInvoiceModels.cs`
  (DL-1 DTO'ları). Her yazma komutu `ICentralAuditDeclaration` taşır (pack satır 810-812).
- Sunucu hesapları: `NetAmount` / `TaxAmount` / `GrossAmount` / `OpenAmount` satırlardan, `DocumentDirection` belge
  türünden, `InvoiceNumberNormalized`, `SupplierNameSnapshot` (yalnız görüntü), `MatchOutcomeSnapshot` (yalnız iz)
  (pack satır 250-264). `CreditReservedAmount` / `CreditedAmount` / `CreditStatus` alanları DL-1 varlığında var; yazan
  komutlar DL-2'dir.
- Alacak dekontu Validate ön kontrolü: `CreditedApInvoiceId` kuralı ve kapasite formülü → 422 `AP_CREDITED_INVOICE_INVALID`
  / `AP_CREDIT_NOTE_EXCEEDS_CAPACITY` (pack satır 867, 1535).
- `X-Legal-Entity-Id` bağlamı (R-2 deseni, pack satır 229-233); gövde / sorguda `tenantId` / `legalEntityId` → 400.
- `Infrastructure/Suppliers/` (`ISupplierEligibilityPort`) ve `Infrastructure/InvoiceMatch/` (`IInvoiceMatchPort`)
  bağdaştırıcıları, sahte istemcili sözleşme testleri (pack satır 529-530); canlı kanıt dev ortamında.
- Denetim olayları (yol `b`): `finance-treasury.ap-invoice.created` / `.updated` / `.deleted` / `.bulk-deleted` /
  `.validated` (pack satır 1055-1059); `Category` D35 / O-18 kararına bağlı — değer uydurulmaz (pack satır 1049-1050).
  `AuditTrailStandardTests` sayıları değişirse aynı değişiklikte güncellenir; defter MOD-0126 DL-1'indir, AP yeni iz satırı
  açmaz (pack satır 458-460, AC-18).
- Modül manifestine MOD-0119 girdisi (pack satır 457).
- `docs/analysis/contracts/ap-invoice.openapi.yaml` v1.0.0 (x-owner MOD-0119; D27/1 T32 sapmasıyla dondurulur; AC-28).
- İzinler (DL-1 uçları): `finance-treasury.ap-invoices.read`, `.read-tenant-wide` (yalnız açık atama), `.create`,
  `.update`, `.delete`, `.bulk-delete`, `.validate` (pack satır 1005-1011). Tohum ve rol şablonu MOD-0018 / O-20.
- Mimari testler: banka alanı adı yasağı (AC-19), `float` / `double` para alanı yok (AC-21), her aksiyonda
  `[HasPermission]` (AC-20), ProcurementService koleksiyon adı / Mongo bağlantısı yok (AC-4), MOD-0126 / MOD-0269 / banka
  çağrısı ve veri taşıma yok (AC-31), AP önekli makbuz tipi yok (AC-32).

## 4. WP prompt iskeleti (doldurulacak alanlar)

Biçim: main'deki örnek `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.3.md`. Hazır olunca
`execution/domains/finance-treasury/work-packs/prompts/WP-0119-DL1-v1.0.md` olarak yazılır.

| Alan | Değer (taslak) | Açık |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` (+ §17.4 giriş alanları) | — (iskelet MOD-0126 DL-1'de; bu WP `/add-module` adımı içermez) |
| WP / Prompt | `WP-0119-DL1` · `TRM-WP-0119-DL1-P v1.0` | kuyruk satırı (TRM kuyruğuna WP satırı) |
| Repository / Branch / Expected HEAD / Worktree | `feature/fin/wp-0119-dl1` · Expected HEAD = MOD-0126 DL-1'in **ve** paketin `ready-for-dev` onayının main'deki commit'i | dağıtım günü; pack künyesindeki dal `feature/fin/mod-0119-accounts-payable` (pack satır 13) ile WP dal adı uyumu CT kararı |
| Risk | HIGH — sinyaller: kiracı, denetim, sözleşme, para | — |
| Görev sınıfı · Paket | ürün · MOD-0119 pack | — |
| İzinli yollar | pack "Repo Scope" DL-1 satırları (satır 446-462): `Features/ApInvoice/**`, `Domain/ApInvoices/**`, `Persistence/ApInvoices/**`, `Infrastructure/{Suppliers,InvoiceMatch}/**`, `Api/Controllers/ApInvoicesController.cs`, manifest girdisi, `ap-invoice.openapi.yaml`, ilgili testler | servis-ortak yollar (`Application/Common/**`, `Persistence/Common/**`, `Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`) izinli **değildir** |
| Korunan yollar | pack "Protected Paths" (satır 480-494) + MOD-0126 `PaymentBatch` yolları (XMC-001), `.antigravity/**`, `ocelot.json`, diğer servisler, üretici sözleşmeleri | — |
| Kapsanan dilim | DL-1 | — |
| Önce oku | pack, AGENTS.md, domain-config, multi-tenancy, security-jwt, git-safety, cross-module-change-consent, code-style, audit-trail-standard, data-scope-enforcement, record-coding-standard, mongo-indexing, handler-design, D1–D35 karar kayıtları, MOD-0126 pack ortak tanımları (pack satır 1360-1361), bu hazırlık kaydı | — |
| Sözleşme yüzeyleri | API (`ap-invoice.openapi.yaml`), tüketilen (`supplier.openapi.yaml` 1.3.0, `invoice-match.openapi.yaml` 1.1.0 — sha256 `41fe1f43…` DL-1 başında yeniden ölçülür, pack satır 530; MOD-0220 TRM-14), İzin (`finance-treasury.ap-invoices.*`), Test (AC-1…6, 15, 17, 18…22, 28, 31, 32, 33, 40 — dilim kısımları pack satır 1536), Sabotaj (AC-6 tekil indeks, AC-17 outbox, AC-32 kapsam, AC-33 yazma kapsamı) | AC metinleri pack satır numaralarıyla alıntılanır |
| Keşif | `verify_module_id.py --discover "accounts payable"` raporu, yol@commit + sha256 | dağıtım günü koşulur |
| Kod alma izi | §2'deki **al-uyarla** satırları, her biri `path@da4abb20 + sha256` ile WP raporuna | — |
| Uzunluk | ~10 bin token'ı aşarsa işçiye dosya yolu + SHA-256 ile verilir (S-52) | — |

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynak düzeltmesi yok.
- Sınıflandırma okuma ve paket metniyle yapıldı; kaynak testler bu oturumda **çalıştırılmadı**.
- Bağımlı servislerin (MOD-0140, MOD-0143, MOD-0220, MOD-0048) Recovery'deki güncel durumu bu oturumda **ölçülmedi**; §1'deki
  "Bugün" sütunu pack metnine ve CT bağlamına dayanır.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.
