---
record_id: MOD-0120-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0120-accounts-receivable.md (status draft, sha256 9055152a…, dal docs/ct/treasury-mod0126-dl1-prep @ce6fb0ed3)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5)
---

> **Güncelleme (2026-10-26) — pack revizyon turu 02'ye bağlandı.** Bu kayıt pack'in `9055152a…` sürümüne göre
> yazıldı. Güncel ve incelenmiş sürüm: sha256 `da4ca5f8a17dc631fbeb7c023d951836af5990dc905d6a74c7475be63cd48551` (dal `docs/ct/treasury-packs-rev-02` @ `c5df75338`,
> CT-APR-02 Codex UYGUN, açık bulgu 0). Bu kayıtta "pack'te tutarsızlık" olarak listelenen maddeler sahip denetimi gap
> listesi 21 ile pack'e işlendi (`docs/records/audits/2026-10/treasury-intake-handover-review-01/GAP-LIST-21-PREP.md`);
> geçerli olan pack metnidir. Buradaki "pack satır N" atıfları eski sürümün satırlarıdır; WP prompt'u alıntıları yeni
> sürümden yeniden alır.

# MOD-0120 DL-1 hazırlığı — fatura çekirdeği, müşteri/şirket doğrulaması, veri kapsamı ve denetim

Bu kayıt, MOD-0120 `ready-for-dev` olduğunda DL-1 WP prompt'unun hızla yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

**Servis ortak yapı taşları bu dilimde yazılmaz.** Servis iskeleti, `FinanceTreasuryCommandReceipt` (+ iki indeksi),
`IFinanceTreasuryDataScopeSource`, `ILegalEntityEligibilityPort`, `ICurrencyReferencePort`, `IRecordCodePort`, `DataScope`
bağdaştırıcısı ve CentralAudit kaydı (`FinanceTreasuryCentralAuditScope`) **yalnız MOD-0126 DL-1'de** açılır; MOD-0120 bunları
yeniden tanımlamaz, önce açmaz, yalnız kullanır (pack satır 188-194, 498-512, 572). MOD-0126 DL-1 bu dilimin önkoşuludur
(pack satır 501, 1584-1586).

## 1. Başlama koşulları (bugün açık olanlar)

DL-1 prompt'u ancak bunlar kapanınca dağıtılabilir (pack "Ready-for-dev Checklist" satır 1346-1369 ve "Teslim dilimleri"
satır 1582-1586):

| Koşul | Bugün | Kimden |
|---|---|---|
| Paket künyesi `ready-for-dev` + o sürüme CT-APR-02 ve CT-APR-01 (S-49, D34/2) | `draft` (pack satır 11) | CT + Beste |
| DCP (UNASSIGNED-finance-treasury-intake) numarası ve `approved` (pack satır 1354); D34/2: numara önce ve ayrıca istenir | `UNASSIGNED`, `draft` | Beste |
| **MOD-0126 DL-1 bitti:** iskelet, servis ortak yapı taşları, port 5071 (pack satır 1356) | MOD-0126 de `draft`; DL-1 hazırlığı ayrı kayıtta | MOD-0126 WP |
| Platform `AuditCategory`'ye `Finance` değeri — D35/3: MOD-0126 DL-1 bu değer main'e girmeden başlamaz; MOD-0120 DL-1 dolaylı olarak bekler | yok | Platform sahibi |
| S-2 bitiş / gözden geçirme koşulunun sahip tarafından yazılması (pack satır 1349-1350) | açık | sahip (K1) |
| O-3: MOD-0149'a eklemeli `lookup-validation` + dış referans çözümleme ucu + sözleşme belgesi (pack satır 1357, 1551); kayıt satırı `reserved / planned`, pack `ready-for-dev` çelişkisi (TRM-09, pack satır 560) | yok; sahip payı D29/1 verildi, üretici teyidi açık | Beste (pack: commercial-suite sahibi) |
| O-4: faturalanabilir müşteri durum kodları (MOD-0048 `account-status`, pack satır 1552) | açık | Beste (pack: commercial-suite sahibi) |
| TRM-14: MOD-0220 §22 `ApprovalStatus` (pack satır 1358) | D34/1 onay turunda | Beste |
| O-14: SEC-002 çözücüsünün FinanceTreasury tüketim yolu (pack satır 1359, 1563) | yok | Beste + platform-shared-services |
| O-6: RCS-001 ortak kod servisi BL-565 (önek `ARI` D29/2, §3 satırı D30 ile uygulandı; pack satır 1360) | servis yok | sahibi belirlenmedi |
| Küsurat hanesi kaynağı — MOD-0048 `currency-minor-units` (MOD-0126 O-11 ile ortak; pack satır 1361) | önerildi (TRM-17, MOD-0048-FU01 §21), D34/1 onay turunda | Beste |
| O-15: MOD-0018 `finance-treasury.receivable-*` izin tohumu ve rol şablonları (pack satır 1564) | sahip payı D29/1; MOD-0018 teyidi açık | Beste |
| Kaynağın sınırlı denetim / çoklu yazma devrinin taşınmadığının sahip onayı (Level-1 koşul 1, pack satır 1369) | açık | sahip (K1) |

**DL-1'i beklemeyenler (bilgi için):** MOD-0023 §17 WP (Ali) ve AR düzeltme şablonu yalnız DL-3'ün önkoşuludur (pack satır
1362-1363, 1592-1594); O-7/O-8 DL-3'tedir. O-8 kapandı: **D31/1 — muafiyet yok, tier-2 aday yoksa 422** (pack satır 1556).

**Dayanak kararlar:** `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` — D34 ve D35 yerel commit
`a181e933b`'de okundu (PR #235 ile taşınır, main'de değil). Bu çalışma ağacının (`ce6fb0ed3`) aynı dosyası yalnız D31'e kadar
başlık taşıyor (D32–D35 başlığı 0 — ölçüldü); prompt yazılırken kayıt main'deki sürümden alıntılanır.
- **D31/1:** AR O-8 muafiyet yok, 422.
- **D34:** onay satırı yalnız künyesi `ready-for-dev` olan sürüme yazılır; MOD-0120 dahil taslak paketler `ready-for-dev`
  olunca tek seferde onaya gelir; DCP numarası önce istenir.
- **D35:** iz `finance-treasury-merkezi-iletim | b`, `RequireEnlistment = true`; borç / istisna / `PinnedCounts` sıfır; `Finance`
  kategorisi MOD-0126 DL-1'den önce; defter + `CT-DECISIONS.md` satırı MOD-0126 DL-1 değişikliğinde yazılır. MOD-0120 için
  sonuç: aynı servis, aynı iz — yeni defter satırı yok (pack satır 514-516); `AuditTrailStandardTests` sayaçları değişirse
  yalnız ayrı CT kararıyla (D35/2). AC-19 (pack satır 1183-1184) bu kuralın testidir.

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet: `shasum -a 256` (ilk 16 hane), satır sayısı
`wc -l`, ikisi de @da4abb20, bu oturumda `git archive` ile ölçüldü. Karar dili: **al-uyarla** (doğrula, Recovery düzenine
göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`), **yalnız referans** (davranışı okunur, kod alınmaz), **alma**
(do-not-carry maddesiyle — pack "Taşınmayacaklar" satır 1480-1509), **sonraki dilim**.

**İki commit eşitliği (AC-34, pack satır 1221-1222 "ölçülmedi" diyor):** `git diff --name-only 218acaf5 da4abb20 --
services/DitenErpTreasuryService` bu oturumda koşuldu: `AccountsReceivable` içeren yol **0**; servis genelinde değişen
paylaşılan dosyalar `Api/Controllers/CustomBaseController.cs` ve `Infrastructure/DependencyInjection.cs` (pack satır 1386 ile
aynı). Yani pack'in `@218acaf5` satır atıfları AR dosyalarında `@da4abb20` için de geçerlidir; ölçüm burada, kanıt WP'nin
kod alma iz kaydında yinelenir.

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/AccountsReceivable/AccountsReceivableEntities.cs` | `c93d26919388244f` · 613 | al-uyarla (yalnız `ReceivableInvoice` :9-57, `ReceivableLine` :59-82, `ReceivableSchedule` :84-104) | `Domain/ReceivableInvoices/` — `ReceivableInvoice : EntityBase`, gömülü `ReceivableInvoiceLine`, `ReceivableInstallment` | taban alanları :50-56 (`DateTime`, `CreatedBy = "system"`, `long RecordVersion`, `CorrelationId`) **alma** (madde 8); ayrı koleksiyonda satır/taksit **alma** (madde 4); GL alanları **alma** (madde 9); içe aktarma alanları (`SourceSystem`, `IdempotencyKey`, `BatchId`…) **alma** (madde 8, 12; pack satır 321-324); diğer 21 sınıf **alma** (madde 10, 11, 12; `ReceivableException` ve `ReceivableSettlementReceipt` pack satır 129, 134) |
| `S/src/…Domain/AccountsReceivable/AccountsReceivableEnums.cs` | `c00173f897d2497c` · 282 | al-uyarla (`InvoiceLifecycleStatus` :7-17 → `ReceivableInvoiceStatus` 5 değer; `CollectionState` :28-39 → `ReceivableCollectionStage` 7 değer) | `Domain/ReceivableInvoices/` | `ISSUED`+`OPEN` → `Open`; `CANCELLED`, `REVERSED` **alma** (pack satır 211-213). Aşama enum'u DL-1'de varlık alanı için gerekir, geçişleri DL-2. `PostingStatus` :19 **alma** (madde 9); projeksiyon / kanıt / içe aktarma / kredi notu enum'ları **alma** (madde 10-12); `FxHandling` :239-243 yalnız referans (pack satır 292) |
| `S/src/…Domain/AccountsReceivable/AccountsReceivableStateGuards.cs` | `607b3a8389dca36c` · 166 | yalnız referans (:9-45) / sonraki dilim (:48-106) | `ReceivableInvoiceStateRules` (pack satır 852) | DL-1 yalnız "Update / Delete / BulkDelete yalnız `Draft`" kuralını yazar (pack satır 934); aşama geçişleri DL-2, WriteOff girişleri :63-98 DL-3 |
| `S/src/…Domain/Common/TenantEntity.cs` | `2ef3028110d99e9e` · 26 | alma | — | madde 8; Recovery `EntityBase` (pack satır 269) |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/AccountsReceivable/Common/AccountsReceivableErrorCodes.cs` | `42e9ab7dcafce150` · 171 | yalnız referans | `Features/ReceivableInvoice/` hata sözlüğü (`RECEIVABLE_*`) | 134 `MOD0120_*` (ölçüldü, pack satır 465 ile aynı) **kopyalanmaz**; anlamı korunanlar Recovery adıyla (pack satır 467-486). İleti resx anahtarı (pack satır 492) |
| `…/Contracts/AccountsReceivableDtos.cs` | `3cf9a87cb4dc5bc4` · 101 | yalnız referans (`ReceivableInvoiceFoundationRequest` :10, `…ResponseDto` :33) | `ReceivableInvoiceModels.cs` (tek dosya, pack satır 182-187) | DTO'larda `TenantId` / `LegalEntityId` yok; projeksiyon / istisna / tahsilat-olayı / içe aktarma DTO'ları **alma** (madde 10, 12) |
| `…/Contracts/AccountsReceivableReadContracts.cs` | `6c37d98143f2b7e6` · 199 | yalnız referans (`ArListQueryContract` :5, `ArReceivableSummaryDtoV1` :58, `AccountsReceivableQueryContract` :129) | `GetReceivableInvoiceListQuery`, `ReceivableInvoiceListSort`, `GetReceivableInvoiceListValidator` | DataTable sunucu tarafı (pack satır 155); sayfa boyu üst sınırı, bilinmeyen sıralama / filtre reddi davranış olarak okunur; projeksiyon / kanıt / istisna sözleşmeleri **alma** (madde 10-12) |
| `…/Contracts/AccountsReceivableEventNames.cs` | `d073847d2b3a226c` · 95 | alma | — | Recovery olay adları `finance-treasury.receivable-invoice.{created,updated,deleted,bulk-deleted}` (pack satır 1075-1078); do-not-carry listesinde madde numarası yok |
| `…/Audit/IAccountsReceivableControlAuditSink.cs`, `…/Audit/AccountsReceivableAuditMetadataFactory.cs` | `276a924d978ee262` · 35, `8dea3883b254a0cc` · 47 | alma | `ICentralAuditDeclaration` (komut başına) | madde 1; AC-34 bu adların iz kaydında olmamasını ister (pack satır 1223) |
| `…/Validation/AccountsReceivableFoundationValidator.cs` | `d87e584acff01852` · 161 | al-uyarla (`ValidateInvoiceFoundationRequest` :25-71, `ValidateScheduleDueDates` :72-103) | `Validators/CreateReceivableInvoiceValidator.cs`, `UpdateReceivableInvoiceValidator.cs` | kurallar pack "Validation Rules" satır 900-918'e göre yeniden yazılır (yuvarlama yok, hane aşımı 400, taksit toplamı = brüt); :104-125 içe aktarma ve :126-161 tahsilat olayı **alma** (madde 12; MOD-0269) |
| `…/Services/AccountsReceivableFoundationService.cs` | `77212d1e9acc241f` · 327 | yalnız referans (`ProcessInvoiceFoundationAsync` :63-162) | Create / Update / Delete / BulkDelete handler'ları + domain | toplu servis sınıfı taşınmaz, kural handler + domain'e (pack satır 845-847); projeksiyon :163, tahsilat olayı :251, içe aktarma :285 **alma** (madde 10, 12) |
| `…/Persistence/IAccountsReceivableRepository.cs` | `c99fe33281ab2651` · 313 | yalnız referans (`GetInvoiceByIdAsync` + `authorizedLegalEntityIds`, `ListInvoicesAsync`) | `Persistence/ReceivableInvoices/` depo arayüzü | kapsam süzgeci kaynağı `IFinanceTreasuryDataScopeSource` (servis ortak); `GetInvoiceByIdempotencyKeyAsync`, `GetInvoiceBySourceIdentityAsync`, `UpsertInvoiceAsync` **alma** (madde 8, 12); `SaveLinesAsync` vb. **alma** (madde 4) |
| `…/Persistence/AccountsReceivablePersistenceContracts.cs` | `7066c51f497c7215` · 45 | yalnız referans (`ReceivableInvoiceListFilter` :14) | liste sorgusu | — |
| `…/References/AccountsReceivableReferenceValidators.cs` | `9b57722605bfd10f` · 208 | yalnız referans (`ICustomerReferenceValidator` :30, `ILegalEntityReferenceValidator` :35, `ICurrencyReferenceValidator` :44) | `ICustomerReferencePort` (MOD-0120'nin kendi portu, yeni); LE ve para birimi → servis ortak `ILegalEntityEligibilityPort`, `ICurrencyReferencePort` (MOD-0126 DL-1; **ikinci kopya yazılmaz**, pack satır 191-194) | üretim derlemesindeki `Foundation…TestDouble` sınıfları :68-208 **alma** — do-not-carry'de madde yok (öneri: Recovery test ikizi `tests/` altında); `ISourceSystemCodeValidator`, `ISettlementEventSourceValidator` **alma** (madde 12; MOD-0269) |
| `…/Security/AccountsReceivableSecurity.cs` | `80bfc18261e8d601` · 286 | alma | `[HasPermission]` + `finance-treasury.receivable-invoices.*` (pack satır 1030-1046) | madde 7 (anahtarlar, takma ad, 7 rol sabiti) |
| `…/Controls/AccountsReceivableControlsPolicies.cs` | `d7c607721f7db7ea` · 266 | alma / sonraki dilim | — | `AccountsReceivableControlsPolicy` :23 **alma** (madde 6); `AccountsReceivableSodPolicy` :104 DL-3'te yalnız referans (A1–A7 yeniden yazılır, pack satır 943-951); `ExceptionTransitionPolicy` :179 **alma** (pack satır 129); `AuditCommitPolicy` :246 **alma** (madde 1) |
| `…/Commands/AccountsReceivableV041Commands.cs`, `…/Services/AccountsReceivableV041Services.cs` | `3e69d754bd8481af` · 142, `517820651556face` · 897 | alma (yalnız `UpdateReceivableInvoiceConcurrencyCommand` :86-98 ve `AccountsReceivableConcurrencyService` :594-641 yalnız referans) | Update `If-Match` + `Version` CAS | geçersiz kılma ve projeksiyon politikası **alma** (madde 10; pack satır 160); kanıt dışa aktarma **alma** (madde 11); `long RecordVersion` → `int Version` (madde 8) |
| `…/Commands/AccountsReceivable{Evidence,Import,Projection,Reconciliation}Commands.cs`, `…/Services/AccountsReceivable{EvidenceLifecycle,Import,ProjectionHandoff,Reconciliation}Service.cs` + arayüzleri, `…/Services/AccountsReceivableProjectionInputRegistrationClient.cs`, `…/Validation/AccountsReceivable{Import,Reconciliation}Validator.cs` | — | alma | — | madde 1, 10, 11, 12 |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/AccountsReceivable/MongoAccountsReceivableRepository.cs` | `e4dec20044e50b81` · 1143 | al-uyarla (yalnız fatura okuma / yazma kısmı) | `Persistence/ReceivableInvoices/` depoları | kaynakta `StartSession` 0 (ölçüldü; pack satır 1412) → Recovery'de Mongo işlemi, her işlem geri çağrısı `CentralAuditTransaction.EnlistAsync` (pack satır 505-506); tek belge (fatura + satır + taksit), soft delete `IsDeleted` + `DeletedAt`; içe aktarma / projeksiyon / kanıt yolları **alma** (madde 1, 10-12) |
| `…/AccountsReceivableMongoIndexes.cs` | `888540018ceadb13` · 571 | **alma** (yalnız referans) | indeks kataloğu (pack satır 441-455) | pack: "kaynak indekslerinin hiçbiri olduğu gibi taşınmaz" (satır 439); `idx_mod0120_` adları **alma** (madde 7). Kaynağın `idx_mod0120_invoices_tenantId_idempotencyKey` (:116) ve `…_legalEntityId_invoiceNumber_sourceSystem` (:129) tekilleri **alınmaz** (madde 8, 12). **DB tekilliği — DL-1:** (1) `ux_receivable_invoice_code` = **Unique(TenantId, InvoiceCode), kısmi filtre YOK** — silinmiş faturanın kodu da dolu sayılır (pack satır 443, RCS-001 §6; satır 917). Kodun RCS'den gelmesi veritabanı tekilliğini kaldırmaz; ikisi birlikte. Test: aynı kiracıda aynı kodla ikinci kayıt — silinmiş bir faturanın koduyla dahil — reddedilir (AC-23 "kodu yeniden kullanılmaz", satır 1192-1193); sabotaj: indekse `IsDeleted=false` filtresi eklenince silinmiş-kod vakası kırmızıya döner. (2) `ux_receivable_invoice_external_number` = Unique(TenantId, LegalEntityId, ExternalInvoiceNumberKey), kısmi `{ExternalInvoiceNumberKey: {$type: "string"}}` (pack satır 444); soft delete'te anahtar `null` yapılır (satır 303) → silinen taslağın numarası yeniden kullanılabilir (AC-24, satır 1194-1195); Void kısmı DL-3. (3) `ix_receivable_invoice_list`, `ix_receivable_invoice_customer` (satır 449-450); `ix_receivable_invoice_stage` (satır 451) için dilim pack'te yazılı değil. Düzeltme / niyet / tahsilat indeksleri DL-3. Makbuz indeksleri MOD-0126 DL-1'in (satır 457-459) |
| `…/AccountsReceivableMongoContext.cs` | `afd41486c017cfed` · 90 | yalnız referans | `Persistence` bağlamı | kaynak koleksiyon adları **alma** (madde 7); Recovery `receivable_invoices` (pack satır 295) |
| `…/AccountsReceivableMongoSerialization.cs` | `ab7870aa455d9714` · 246 | yalnız referans | servis serileştirme deseni | `decimal` → `Decimal128`, GUID V3 / subtype 4 (pack satır 804) |
| `…/AccountsReceivableIndexHostedService.cs` | `d744df15523e2dcd` · 43 | al-uyarla | indeks hizmeti | servis iskeletinin indeks barındırılan hizmeti varsa ona eklenir (`mongo-indexing`, pack satır 462-463); kısmi filtre biçimi gerçek replika kümesinde ölçülür (T-5, satır 1456) |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Infrastructure/AccountsReceivable/FailClosedAccountsReceivableReferenceValidators.cs` | `8acd83223c3209b2` · 65 | **alma** | — | madde 2 (saplamaları uygulama saymak); `FailClosedArControlAuditSink` :59-65 madde 1 |
| `…/GatewayBackedAccountsReceivableReferenceValidators.cs` | `250f9e364dfea47f` · 415 | yalnız referans | `Infrastructure/Customers/**` (yeni MOD-0149 bağdaştırıcısı); LE / para birimi bağdaştırıcıları servis ortak (MOD-0126 DL-1) | kaynakta **müşteri** için gateway doğrulayıcısı yok (yalnız FailClosed — pack satır 1410); LE doğrulayıcısı :13-123 yalnız Active bakar, `ApprovalStatus` yok (→ TRM-14); para birimi :124-266 yayımlanmış haneyi okur; HTTP durum eşlemesi :366-415 503 / 422 ayrımı için okunur. `ISourceSystemCodeValidator` :267 **alma** (madde 12) |
| `…/AccountsReceivableReferenceValidationOptions.cs` | `9ab43e98d68f69bd` · 103 | alma | — | madde 2 (bağlanmış ama okunmayan seçenekler) |
| `…/ClaimsAccountsReceivableSecurityContextAccessor.cs` | `d553840336166ae7` · 144 | alma | Recovery `ITenantContext` + JWT | madde 5 (`ar_legal_entity_scope`), madde 6 |
| `…/GatewayAccountsReceivableProjectionInputRegistrationClient.cs`, `…/AccountsReceivableProjectionPropagationOptions.cs` | `b6635ccbd4f81fc4` · 194, `8187dc2266e9692d` · 10 | alma | — | madde 10 |
| `…/Infrastructure/DependencyInjection.cs` | `9acf42356e8f8a78` · 640 | alma | servis ortak DI (MOD-0126 DL-1) + MOD-0120 kaydı | FailClosed varsayılanları :51-52 (madde 1, 2); bu dosya 218acaf5 ile da4abb20 arasında farklı (ölçüldü) |
| `…/Api/Controllers/AccountsReceivableController.cs` | `ef24cfe6cd8d79d9` · 1689 | al-uyarla (yalnız AR-01 `ListInvoices` :52-84, AR-02 `GetInvoice` :85-99) | `Api/Controllers/ReceivableInvoicesController.cs` | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]`; rota `/api/receivable-invoices` (madde 7: `/api/v1/mod0120` alma); `EnsureAccess` :943-990 **alma** (madde 6), ama gövde / sorguda `tenantId` reddi davranışı korunur → 400 `RECEIVABLE_VALIDATION_FAILED` (pack satır 278, 487); diğer 33 işlem **alma** (pack satır 157-161) |
| `…/Api/Program.cs` | `627389196a0f848e` · 37 | alma | MOD-0126 DL-1 iskeleti | port 5058 (madde 7); iskelet tek (K15, pack satır 501-502) |

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `S/tests/…/AccountsReceivable/AccountsReceivableFoundationTests.cs` | `14b4912cf714a05f` · 576 | para alanında `float`/`double` yok (AC-20), kiracı uyuşmazlığı, referans doğrulayıcı sonuçları (AC-2, AC-3) |
| `…/AccountsReceivableRepositoryTests.cs` | `6b678cc50a545ba4` · 735 | `ScopeFiltering_PrecedesPagination_AndEmptyMeansNothing` (AC-6), `Decimal128` gidiş-dönüş (AC-20); override / export CAS testleri **alma** |
| `…/AccountsReceivableControllerTests.cs` | `0c6b49c466246c30` · 867 | liste: yasak `tenantId` sorgusu, izinsiz çağrı (AC-22); içe aktarma / projeksiyon / kanıt rotaları **alma** |
| `…/AccountsReceivableReferenceWiringTests.cs` | `90cc833b83fd6d33` · 323 | HTTP hatası / bozuk gövde → kapalı sonuç (503 davranışı); "yapılandırma yoksa hepsi FailClosed" testi **alma** (madde 2) |
| `…/AccountsReceivableSchemaAndIndexTests.cs` | `b2086ce9b207e74e` · 544 | `Decimal128` her para alanında, tekil indeks bayrağı (AC-20, AC-23, AC-24) |
| `…/AccountsReceivableV041ContractTests.cs` | `98cb95120a7ceebd` · 949 | `QueryContract_*` liste doğrulayıcı; `Concurrency_StaleRecordVersionReturnsControlledError` (409 CAS); içe aktarma / override **alma** |
| `…/InMemoryAccountsReceivableRepository.cs` | `b46fe45c222d4983` · 886 | **alma** — Recovery gerçek Mongo replica set ister (lane kuralı) |
| `…/AccountsReceivableAuditCommitPolicyTests.cs`, `…ControllerRouteTemplateTests.cs` | `855c9a74e8d7c2fc` · 51, `e2bea2cd1c9253bd` · 79 | **alma** (madde 1; madde 7 rota) |
| `…/AccountsReceivableSodPolicyTests.cs` | `aeac1a56498379e9` · 82 | DL-3 |
| İçe aktarma, projeksiyon, kanıt, mutabakat, istisna, korunan yol, kısıtlı API testleri | — | **alma** (madde 10-12) |

Devralınmış kaynak kanıtı (317/317 @da4abb20) Recovery hazırlığının ölçüsü değildir (pack satır 1316-1318; madde 15).

### 2.6 Referans veri portları ve hata davranışı (DL-1 komutlarında)

| Port | Sahibi | Davranış (pack) |
|---|---|---|
| `ICustomerReferencePort` (MOD-0149) — **MOD-0120'nin kendi portu, DL-1'de yeni** | MOD-0120 | Create / Update (Issue DL-2): yok / başka kiracı → 422 `RECEIVABLE_CUSTOMER_NOT_FOUND`; faturalanabilir değil → 422 `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE`; erişilemez → 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE`, geçiş yok (satır 630-634, 975-976). MOD-0149 koleksiyonu okunmaz (AC-3 mimari testi, satır 1138-1141). O-3 gelmeden fail-closed (satır 560) |
| `ILegalEntityEligibilityPort` (MOD-0220 + `ApprovalStatus`) | servis ortak — MOD-0126 DL-1 | Create / Update / Delete / BulkDelete: Active + Approved değil → 422 `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`; erişilemez → 503 `RECEIVABLE_LEGAL_ENTITY_UNAVAILABLE` (satır 620-623) |
| `ICurrencyReferencePort` (MOD-0048 liste + `currency-minor-units`) | servis ortak — MOD-0126 DL-1 | hane yok → 503 `RECEIVABLE_CURRENCY_PRECISION_UNAVAILABLE`, sabit tablo yok (satır 287, 993); hane aşımı → 400. **Listede olmayan para birimi için Recovery hata kodu pack'te yok** (Validation satır 909 kuralı koyar, hata sözlüğü satır 465-492 kod vermez; kaynakta `MOD0120_REFERENCE_CURRENCY_INVALID`) |
| `IRecordCodePort` (RCS-001, önek `ARI`) | servis ortak — MOD-0126 DL-1 | servis yanıt vermiyor → 503 `RECEIVABLE_RECORD_CODE_UNAVAILABLE`, fatura yazılmaz (satır 1018); istekte kod → 400 (satır 917) |
| `IFinanceTreasuryDataScopeSource` (SEC-002) | servis ortak — MOD-0126 DL-1 | okuma kapsamı / yazma kapsamı ayrı; kapsam dışı liste 200 boş, detay / yazma 404; çözücü hatası → boş kapsam (satır 602-619) |

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

- `Features/ReceivableInvoice/`: `CreateReceivableInvoiceCommand`, `UpdateReceivableInvoiceCommand`,
  `DeleteReceivableInvoiceCommand`, `BulkDeleteReceivableInvoiceCommand` + handler'ları; `GetReceivableInvoiceListQuery`,
  `GetReceivableInvoiceByIdQuery`, `GetReceivableInvoiceTraceQuery` + `ReceivableInvoiceListSort`; validatorlar;
  `ReceivableInvoiceModels.cs` (pack satır 824-842, 1582). İz sorgusu DL-1'de yalnız oluşturma / güncelleme izini taşır
  (düzeltme, karar, tahsilat bölümleri DL-2 / DL-3 — öneri; pack dilim ayrımını iz için yazmıyor).
- Domain: `ReceivableInvoice : EntityBase` (gömülü satır / taksit, sunucu toplamları, tek varsayılan taksit — AC-1),
  `ReceivableInvoiceStateRules` (DL-1 kısmı).
- Persistence: `receivable_invoices` deposu, §2.3'teki DL-1 indeksleri, `CentralAuditTransaction.EnlistAsync`.
- `Infrastructure/Customers/**`: MOD-0149 bağdaştırıcısı (O-3 ucu; zaman aşımlı `HttpClient` — öneri, MOD-0126 deseni).
- Api: `ReceivableInvoicesController` (AR-01, AR-02 + Create / Update / Delete / BulkDelete), her aksiyonda `[HasPermission]`.
- Her yazma komutunda `Idempotency-Key` zorunlu, servis ortak makbuz (`Operation = mod0120:{Komut}`, pack satır 425-430);
  tek aşamalı → tx1'de `Completed` (AC-17 Create kısmı, AC-18 tek aşamalı).
- `ICentralAuditDeclaration` dört komutta; olaylar `…created / updated / deleted / bulk-deleted` (pack satır 1075-1078);
  `Category` — pack "değer uydurulmaz" diyor (satır 1070), D35/3 sonrası `Finance` (Platform eki main'e girdikten sonra).
- `docs/analysis/contracts/accounts-receivable.openapi.yaml` v1.0.0 (AC-30; pack satır 517, 584).
- Testler (gerçek replika kümesi): AC-1, 2, 3, 5, 6, 17 (Create), 18 (tek aşamalı + sabotaj), 19, 20, 21, 22, 23, 24, 30,
  31, 34, 35 (Create / Update / Delete) — pack satır 1587-1588.

## 4. WP prompt iskeleti (doldurulacak alanlar)

Biçim: main'deki örnek `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.3.md`. Hazır olunca
`execution/domains/finance-treasury/work-packs/prompts/WP-0120-DL1-v1.0.md` olarak yazılır.

| Alan | Değer (taslak) | Açık |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` (+ §17.4 giriş alanları) | — (iskelet MOD-0126 DL-1'de; bu WP `/add-module` koşmaz) |
| WP / Prompt | `WP-0120-DL1` · `TRM-WP-0120-DL1-P v1.0` | kuyruk satırı (TRM kuyruğuna WP satırı) |
| Repository / Branch / Expected HEAD / Worktree | `feature/fin/wp-0120-dl1` · Expected HEAD = MOD-0126 DL-1 merge'ünden sonra paketin `ready-for-dev` onayının main'deki commit'i | dağıtım günü |
| Risk | HIGH — sinyaller: kiracı, denetim, sözleşme, para | — |
| Görev sınıfı · Paket | ürün · MOD-0120 pack | — |
| İzinli yollar | pack "Repo Scope" DL-1 satırları: `Features/ReceivableInvoice/**`, `Domain/ReceivableInvoices/**`, `Persistence/ReceivableInvoices/**`, `Infrastructure/Customers/**`, `Api/Controllers/ReceivableInvoicesController.cs`, `docs/analysis/contracts/accounts-receivable.openapi.yaml` (satır 503-517) | test proje yolu pack'te yok |
| Korunan yollar | pack "Protected Paths" (satır 534-546) + servis ortak yollar (`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`, LE / para birimi / kod / `DataScope` bağdaştırıcıları — yalnız kullanılır), `tests/architecture/audit-ledger/**` (D35/2: satır eklemek CT kararı) | — |
| Kapsanan dilim | DL-1 | — |
| Önce oku | pack, MOD-0126 pack (ortak tanımlar bağlayıcı, pack satır 1375-1376), AGENTS.md, domain-config, multi-tenancy, security-jwt, git-safety, cross-module-change-consent, code-style, audit-trail-standard, data-scope-enforcement, record-coding-standard, D9–D35 karar kayıtları, MOD-0126 DL-1 ve bu hazırlık kaydı | — |
| Sözleşme yüzeyleri | API (`accounts-receivable.openapi.yaml`), İzin (`finance-treasury.receivable-invoices.*`), Test (§3 AC listesi), Sabotaj (AC-6 kapsam, AC-18 denetim, AC-35 yazma kapsamı, kod indeksi silinmiş-kod vakası) | AC metinleri pack satır numaralarıyla alıntılanır |
| Keşif | `verify_module_id.py --discover "accounts receivable"` raporu, yol@commit + sha256 | dağıtım günü koşulur |
| Kod alma izi | §2'deki **al-uyarla** satırları, her biri `path@da4abb20 + sha256` ile WP raporuna | — |
| Uzunluk | ~10 bin token'ı aşarsa işçiye dosya yolu + SHA-256 ile verilir (S-52) | — |

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`, `git diff --name-only`); kaynak düzeltmesi yok.
- Sınıflandırma okuma ve paket metniyle yapıldı; kaynak testler bu oturumda **çalıştırılmadı**, Recovery'de hiçbir şey
  derlenmedi.
- MOD-0149, MOD-0220, MOD-0048 uçları çağrılmadı; "ölçüldü" yazmayan her üretici durumu pack'ten alıntıdır.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.
