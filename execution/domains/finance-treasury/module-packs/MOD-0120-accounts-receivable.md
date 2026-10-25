---
id: MOD-0120
name: Accounts Receivable (AR)
domain: finance-treasury
service: Diten.FinanceTreasuryService
service_port: 5071
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
owner: depo sahibi (K1) / Treasury Intake Control Tower
branch: feature/fin/mod-0120-accounts-receivable
started: 2026-10-24
target: 2027-02-15
form_field_count: 13
slice: "TRM-S2 — kısıtlı MVP: yerel alacak faturası + tahsilat aşaması + MOD-0023 onaylı düzeltme (iptal / silme) + elle tahsilat ve onaylı tahsilat geri alma (D16); faturalama entegrasyonu YOK, GL YOK, MOD-0125 beslemesi YOK"
---

# MOD-0120 — Accounts Receivable (AR)

> **Durum: `draft` (planlama belgesi).** Bu paket kod, servis iskeleti, port kaydı, gateway rotası ya da kaynak
> projeden kod alma izni vermez. Kod ancak paket `approved` / `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02)
> ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten ve sahip **MOD-0120 için** kod alma iznini verdikten sonra
> yazılır (sahip kararı D1, `docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md`: "Kod alma izni modül
> modül verilir"). Engeller: "Ready-for-dev Checklist" ve "Açık kararlar".
>
> **Bu paket dört açık kapı kararına dayanır ve hiçbirini vermez:** MOD-0003 (karar 0.3), AR-BILLING-BUNDLE (karar 0.5),
> ERP-FIN-BUNDLE (karar 0.6, EA), GL kapsamı (karar 0.7) — `domain-config.md` "Key Decisions" OPEN; DCP
> `UNASSIGNED-finance-treasury-intake.md` §8 adım 5 ("TRM-S2: MOD-0003 kararından sonra"), §18; CT kuyruğu TRM-12. Paket
> önerilen seçeneklerle **taslak** olarak yazıldı; her biri "Açık kararlar" tablosunda **karar değil, öneri** olarak durur.
> Önerilen seçenekler reddedilirse ilgili bölümler o kararla yeniden yazılır.
>
> **Bağlanan kararlar (yeniden karar verilmez):** D1 (doğrula, sonra uyarlayarak al; MOD-0120 kod alma izni ayrıca),
> D2 (servis `Diten.FinanceTreasuryService`), D3 (dal kısa kodu `fin`), D5 (MOD-0269 = önce sözleşme, dağıtım kapalı) —
> `treasury-intake-owner-decisions-01.md`; D9 (başka modüldeki eklemeli değişiklik onayı ve şartları), D10/1 ve D10/2
> (şirket `OperationalStatus = Active` **ve** `ApprovalStatus = Approved`; MOD-0220 eki TRM-14), D10/3 (port 5071),
> D11/4 (izin öneki `finance-treasury.*`), D12/2 (her yazma komutunda zorunlu Idempotency-Key), D14/1 (veri kapsamı
> `LegalEntityId`, kiracı geneli ayrı `…read-tenant-wide` izni), D14/2 (servis genelinde tek `FinanceTreasuryCommandReceipt`,
> tanımı MOD-0126 pack'inde), D14/3 (AR'de alacağı etkileyen kararlar K2), D14/4 (alacak oluşturan/değiştiren her yazmada
> şirket Active + Approved), D15/1 (elle tahsilat DL-3'te), D15/2 (Reconcile şirket kuralından muaf), D16 (elle tahsilat
> v1'de: ödeme referansı iş tekilliği, mükerrer tahsilat kontrolü, onaylı geri alma) —
> `treasury-intake-owner-decisions-02.md`. D11/1, D12/1, D13 MOD-0126'ya özgüdür; bu pakette
> **desen** olarak önerilir, MOD-0120 için ayrıca sahip teyidi ister (O-7, O-8 muafiyet kısmı). Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0120-v1.0.md` (koşul 1–5 → "Implementation Notes →
> Level-1 koşulları"). DCP: `UNASSIGNED-finance-treasury-intake.md` (taslak; dilim TRM-S2).
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-24, bu worktree (HEAD `3a1c32b9e`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0120 --name "Accounts Receivable (AR)"` →
> `OK  MOD-0120: proven against Blueprint/registry.` (çıkış 0). Kayıt dosyasında MOD-0120 satırı henüz yok; satır bu
> paketle yazılmaz (ayrı iş, D8). Kaynak paketteki ad `Accounts Receivable` (kanonik değil, paket §A T22) kullanılmaz.
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.0
> (`/Users/natig/Projects/treasury-transfer-handoff/source-batch3-v1.0/docs/roadmap/plans/treasury-source-transfer-preparation/mod0120-transfer-packet-v1.0/`,
> `manifest.json` sha256 `f92e068a69fc281ddc89cd834758c12703cca3de5b1f18f21aaa2aa8d68f0df9`, bu oturumda `shasum -a 256`
> ile yeniden ölçüldü); §D / §L5 / §J **düzeltme kaydıyla** okundu
> (`source-batch3-v1.0/docs/records/decisions/2026-10/treasury-b3-owner-inputs-correction-20261022/`: AR-BILLING-BUNDLE
> üreticisi "yok" değil **belirsiz**, MOD-0169 Billing & Invoicing önde gelen aday, EA kararı bekliyor). Kaynak kod
> yalnız `git -C /Users/natig/Projects/ERP-vNext show 218acaf5f3d78476bd16b9f4b1e5767d6b41d11f:<yol>` ile okundu.
> Atıflar `paket §X` ya da `Dosya.cs:satır@218acaf5` biçimindedir; Recovery atıfları `yol:satır` (bu worktree).
> Kardeş paket MOD-0149 integration v1.0 (manifest `332775d3f394d346c292e279e0aaa20f6c047f7efb0948fc716ecd709e0c3cf0`,
> yeniden ölçüldü) müşteri tarafı için okundu.

## Module Summary

Müşteri alacağının sistem-of-record'u: alacak faturası (receivable invoice), taksit planı, kalan tutar, tahsilat
aşaması ve alacağı azaltan onaylı düzeltmeler. Blueprint 8.8 W-3, Financial Management; Blueprint SoR "invoices (AR),
receivables, collections states, credit memos" (paket §A).

- **Hedef kullanıcı:** alacak operatörü (faturayı kaydeder ve açar, tahsilat aşamasını yürütür), alacak yöneticisi
  (iptal / silme talebini onaylar ya da reddeder), finans kontrolörü (yüksek tutarlı düzeltme onayı), denetçi (okuma).
- **Kısıtlı MVP (karar 0.5 önerisi, sapma S-2):** AR kendi alacak faturasını **yerel** olarak tutar; faturalama
  sisteminden (MOD-0169 / AR-BILLING-BUNDLE) fatura almaz, müşteriye fatura göndermez, yasal/e-fatura numarası üretmez.
  Kaynak da AR'yi faturalama entegrasyonu olmadan kurmuştur (paket §D; sıra belgesi Amendment 3 §4 "evidence for the
  restricted-MVP interpretation, not proof of it").
- **Çekirdek:** alacağı **azaltan** her işlem (iptal = Void, alacak silme = WriteOff) MOD-0023 onayından ve onay
  ayrılığından (SoD) geçer; faturayı oluşturan, açan ve talebi gönderen kişi o düzeltmeye karar veremez.
- **Kapsam ilkesi:** kaynağın 35 HTTP işleminden (AR-01…AR-35, paket §C) yalnız fatura okuma ikisi (AR-01, AR-02)
  taşınır; içe aktarma, MOD-0125 projeksiyonu, tarih geçersiz kılma ve kanıt dışa aktarma alt sistemleri bu sürümde
  **taşınmaz** (gerekçe "Owned Objects → Kaynak işlemleri"). Kaynakta olmayan elle oluşturma, açma, tahsilat aşaması ve
  onaylı düzeltme Recovery ekleridir. Kaynak kusurları taşınmaz ("Implementation Notes → Taşınmayacaklar").
- **Para hareketi yok:** AR banka işlemi yapmaz, ödeme almaz; tahsilat yalnız (DL-3, sahip kararı D16) elle **kayıt**
  edilir ve yalnız MOD-0023 onaylı **karşı kayıtla** geri alınır.
  MOD-0269 mutabakat/ödeme tüketimi kaynakta da ertelenmiştir (paket §L.13).

## Ownership and Boundaries

- **In-scope:** `ReceivableInvoice` (satırlar ve taksitler gömülü); taslak oluşturma/düzenleme/silme; açma (Issue);
  tahsilat aşaması (InCollection, Dunning, PromiseToPay, Disputed) ve gün hesabıyla türetilen yaşlandırma (NotDue / Due /
  Overdue); MOD-0023 onaylı düzeltme talebi (Void, WriteOff) — yerel niyet + idempotency + belirsiz sonuç + uzlaştırma;
  elle tahsilat kaydı (ödeme referansı iş tekilliği + mükerrer tahsilat kontrolü) ve onaylı tahsilat geri alma (karşı
  kayıt) — D16; iz (trace) okuma; kiracı ekranı (liste + oluştur + düzenle + detay).
- **Out-of-scope:** faturalama, yasal fatura numarası, e-fatura, fatura PDF'i ve müşteriye gönderim (MOD-0169 /
  AR-BILLING-BUNDLE, karar 0.5); GL kaydı, `PostingStatus`, GL hesap/masraf yeri (MOD-0118, karar 0.7); MOD-0125 nakit
  tahmini beslemesi ve projeksiyon/politika/geçersiz kılma alt sistemi (MOD-0125 aday, paketi yok); toplu içe aktarma ve
  kaynak kontrol toplamı mutabakatı (O-10, servis kimliği engeli); kredi notu (credit memo; Follow-up); kısmi silme,
  fazla/eşleşmemiş tahsilat, tek ödeme referansının birden çok faturaya bölünmesi (Follow-up); banka mutabakatı ve MOD-0269 mutabakat olayı (MOD-0121,
  MOD-0269 — D5); kanıt dosyası saklama ve dışa aktarma (MOD-0031 sahibi); müşteri, şirket, banka hesabı ana kayıtları
  (MOD-0149, MOD-0220, MOD-0289).
- **Sahip olunan sözleşme:** `ACCOUNTS-RECEIVABLE` (yeni, `docs/analysis/contracts/accounts-receivable.openapi.yaml`,
  x-owner MOD-0120) — kaynakta resmi sözleşme yok (paket §B "ABSENT"). Bu dosya aynı zamanda MOD-0003 sapmasının (S-1)
  "dondurulmuş sözleşme" kanıtıdır. Tek yazıcı MOD-0120 (K15).
- **Tüketilen sözleşmeler:** MOD-0149 müşteri doğrulaması + dış referans çözümleme (**yazılacak**, D9 eki, O-3);
  MOD-0220 `lookup-validation` (+ TRM-14 `ApprovalStatus`); MOD-0023 iş akışı başlatma ve görev geçişleri; MOD-0048
  para birimi; MOD-0031 kanıt bağlantı kimlikleri (yalnız referans); Platform veri kapsamı (`IDataScopeResolver`, SEC-002).
- **Müşteri SoR'u Recovery MOD-0149 Customer 360'tır** (commercial-suite, `Diten.CrmService`, port 5061). AR müşteri
  kaydı açmaz, değiştirmez, birleştirmez; `CustomerId` = MOD-0149 `Account.Id`, yalnız sözleşmeyle doğrulanır. MOD-0149
  koleksiyonu (`accounts`, `account_external_references`) doğrudan okunmaz (domain-config "Contract-first").

## Owned Objects

### Varlıklar (kaynak 24 sınıf → Recovery karşılığı)

Kaynak sınıflar `services/DitenErpTreasuryService/src/Diten.ErpTreasuryService.Domain/AccountsReceivable/AccountsReceivableEntities.cs@218acaf5`
(satır 9–613; paket §E "24 entities").

| # | Kaynak sınıf | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `ReceivableInvoice` (:9-57, 39 alan) | **taşınır, daraltılarak** — `ReceivableInvoice : EntityBase` | çekirdek; içe aktarma, GL ve kaynak-sistem alanları taşınmaz (Entity Fields) |
| 2 | `ReceivableLine` (:59-82) | **taşınır, gömülü** `ReceivableInvoiceLine` (değer nesnesi) | faturayla tek belgede: çoklu yazma yok (Taşınmayacaklar 4) |
| 3 | `ReceivableSchedule` (:84-104) | **taşınır, gömülü** `ReceivableInstallment` | aynı neden |
| 4 | `ReceivableException` (:106-133) | **taşınmaz** | kaynakta içe aktarma / referans çözümleme istisna kuyruğu; içe aktarma bu sürümde yok |
| 5–10 | `ReceivableProjectionLine`, `…ProjectionRun`, `…ProjectionCurrencyTotal`, `…ProjectionHandoffRecord`, `…ProjectionInvalidationRecord`, `ProjectionPolicy` | **taşınmaz** | MOD-0125 tüketicisi Recovery'de yok (aday, paketi yok); paket §P FORECAST yuvası |
| 11 | `ReceivableOverride` (:362-405) | **taşınmaz** | projeksiyon kovası/tarih geçersiz kılma; MOD-0125'e bağlı. Beklenen tahsilat tarihi faturada düz alan olarak kalır |
| 12–16 | `ReceivableEvidenceManifest`, `…EvidenceExportRequest`, `…DocumentReference`, `…EvidenceReference`, `EvidenceExportRecord` | **taşınmaz** | kanıt SoR'u MOD-0031; AR yalnız bağlantı kimliği taşır (düzeltme talebinde `EvidenceRefs`) |
| 17–21 | `ReceivableRejectionReport`, `…ImportLineResult`, `ImportBatchLock`, `ReceivableImportBatch`, `ReceivableImportFailure` | **taşınmaz** | içe aktarma O-10 |
| 22 | `ReceivableSettlementReceipt` (:562-576) | **taşınmaz** | MOD-0269 mutabakat tüketimi kaynakta ertelenmiş (paket §L.13); elle tahsilat ayrı Recovery varlığıdır |
| 23–24 | `ReceivableReconciliationRecord`, `…VarianceRecord` | **taşınmaz** | içe aktarma O-10 |
| + | — | **yeni** `ReceivableAdjustmentRequest` | iptal / silme talebi; MOD-0023 onayı |
| + | — | **yeni** `ReceivableAdjustmentStartIntent` | uzak başlatmadan önce yerel niyet (domain-config "Yerel kayıt önce") |
| + | — | **yeni** `ReceivableAdjustmentDecision` | karar niyeti/sonucu (onay, ret) |
| + | — | **yeni** `ReceivablePayment` (D16) | elle tahsilat kaydı ve onaylı geri almanın karşı kaydı, ekleme-yalnız |
| + | — | `FinanceTreasuryCommandReceipt` (**servis ortak**, yeni tip açılmaz) | istemci idempotency makbuzu (D12/2, D14/2) — tanım MOD-0126 pack Entity Fields |

**Makbuz ve niyet tipi kararı — KARAR (D14/2):** makbuz servis genelinde **tek ortak tiptir**
(`FinanceTreasuryCommandReceipt`, koleksiyon `finance_treasury_command_receipts`; tanım MOD-0126 pack Entity Fields —
FinanceTreasuryCommandReceipt); AR önekli makbuz tipi açılmaz. Niyet tipleri AR'ye özgü kalır
(`ReceivableAdjustmentStartIntent`, `ReceivableAdjustmentDecision`) ama MOD-0126'nın tek geçiş matrisindeki durum
adlarını kullanır. Ortak tipin ya da kurallarının değişmesi dört Treasury paketinin birlikte değişikliğini ister.

### Kaynak işlemleri (AR-01…AR-35, paket §C, `CONTRACT-STATE.json.operations`) → Recovery

Kaynak taban rota `api/v{version:apiVersion}/mod0120/accounts-receivable` (`AccountsReceivableController.cs:17@218acaf5`),
yalnız `[Authorize]` (:16) ve denetleyici içi izin kontrolü (`EnsureAccess`, :943-990).

| Kaynak | Kaynak rota | İzin (kaynak) | Recovery | Karar |
|---|---|---|---|---|
| AR-01 | GET invoices | `receivable.view` | `GetReceivableInvoiceListQuery` → `GET /api/receivable-invoices` | **taşınır** (DataTable server) |
| AR-02 | GET invoices/{id} | `receivable.view` | `GetReceivableInvoiceByIdQuery` → `GET /api/receivable-invoices/{id}` | **taşınır** |
| AR-03…06, AR-13, AR-16, AR-17 | import-batches, rejection reports, import-failures, reconciliation-records, invoices/import, reconciliation/source-control-totals | `receivable.view`, `receivable.import`, `receivable.reconcile` (AR-16/17 servis kimliği ister, :419, :450) | — | **bu sürümde taşınmaz** (O-10): Recovery'de FinanceTreasury için güvenilir servis kimliği yok (güvenilir tüketici yalnız `Diten.MDM`'ye kilitli — MOD-0126 pack Dependencies) ve içe aktarma kaynakta çoklu yazma kısmi kalıcılık taşır (paket §L.3) |
| AR-07 | GET exceptions | `receivable.view` | — | **taşınmaz** — istisna kaynağı içe aktarmaydı |
| AR-08…12, AR-18…21, AR-27 | projections/*, projection-policies/*, handoff dispatch | `receivable.projection.build`, `ar.projection.*` | — | **taşınmaz** — MOD-0125 tüketicisi yok; HTTP istemcisi (`GatewayAccountsReceivableProjectionInputRegistrationClient.cs`) taşınmaz |
| AR-22…26 | overrides, submit/approve/reject/reverse | `ar.schedule.override.*` | — | **taşınmaz** — projeksiyon kovasına bağlı; yerel SoD + kapalı eşik çözücü (paket §L.7) |
| AR-14, 15, 28…35 | evidence-manifests/*, export-requests, evidence-export-records/* | `receivable.evidence.*`, `ar.evidence.export.revoke` | — | **taşınmaz** — kanıt ve dışa aktarma MOD-0031'in; AR yalnız bağlantı kimliği taşır |

### Recovery ekleri (kaynakta yok)

| Komut | Uç (Gateway `/api/…`) | Neden |
|---|---|---|
| `CreateReceivableInvoiceCommand` | `POST /api/receivable-invoices` | kısıtlı MVP: fatura yerelde açılır (S-2) |
| `UpdateReceivableInvoiceCommand` | `PUT /api/receivable-invoices/{id}` | Compact `Edit`; yalnız `Draft` |
| `DeleteReceivableInvoiceCommand` | `DELETE /api/receivable-invoices/{id}` | Golden Reference silme; yalnız `Draft`, soft delete |
| `BulkDeleteReceivableInvoiceCommand` | `DELETE /api/receivable-invoices/bulk` | Golden Reference zorunlu toplu silme; aynı kural |
| `IssueReceivableInvoiceCommand` | `POST /api/receivable-invoices/{id}/issue` | `Draft → Open`; tutarlar ve müşteri anlık görüntüsü kilitlenir |
| `SetReceivableInvoiceCollectionStageCommand` | `POST /api/receivable-invoices/{id}/collection-stage` | tahsilat aşaması geçişi (kaynak geçiş tablosundan uyarlanmış, `AccountsReceivableStateGuards.cs:48-106@218acaf5`) |
| `RequestReceivableInvoiceAdjustmentCommand` | `POST /api/receivable-invoices/{id}/adjustments` | Void / WriteOff talebi + MOD-0023 başlatma (iki aşamalı) |
| `ApproveReceivableInvoiceAdjustmentCommand` | `POST /api/receivable-invoices/{id}/adjustments/{adjustmentId}/approve` | karar (iki aşamalı, K2) |
| `RejectReceivableInvoiceAdjustmentCommand` | `POST /api/receivable-invoices/{id}/adjustments/{adjustmentId}/reject` | karar (iki aşamalı, K2) |
| `ReconcileReceivableInvoiceAdjustmentCommand` | `POST /api/receivable-invoices/{id}/adjustments/{adjustmentId}/workflow/reconcile` | belirsiz başlatma / karar sonucunu MOD-0023'ten okuyup kapatır; **tek kurtarma yolu** |
| `RecordReceivableInvoicePaymentCommand` (D16) | `POST /api/receivable-invoices/{id}/payments` | elle tahsilat kaydı (tek aşamalı, K2 — D14/3); ödeme referansı iş tekilliği (D16) |
| (geri alma — yeni komut yok) `RequestReceivableInvoiceAdjustmentCommand` `AdjustmentType = PaymentReversal` + Approve / Reject / Reconcile | `POST /api/receivable-invoices/{id}/adjustments` (`paymentId` ile) | **onaylı tahsilat geri alma** (D16): Void / WriteOff gibi MOD-0023 onaylı düzeltme; onayda karşı kayıt (Runtime Constraints) |

- **Sorgular:** `GetReceivableInvoiceListQuery`, `GetReceivableInvoiceByIdQuery`, `GetReceivableInvoiceTraceQuery`
  (`GET /api/receivable-invoices/{id}/trace` — düzeltmeler, kararlar, tahsilatlar, aşama geçmişi).
- **DTO'lar:** `ReceivableInvoiceModels.cs` tek dosyada (Golden Reference): `ReceivableInvoiceListItemDto`,
  `ReceivableInvoiceDetailDto`, `ReceivableInvoiceLineDto`, `ReceivableInstallmentDto`, `ReceivableInvoiceWriteRequest`,
  `ReceivableInvoiceLineWriteRequest`, `ReceivableInstallmentWriteRequest`, `ReceivableCollectionStageRequest`,
  `ReceivableAdjustmentRequestDto`, `ReceivableAdjustmentCreateRequest`, `ReceivableAdjustmentDecisionRequest`,
  `ReceivableAdjustmentDecisionDto`, `ReceivablePaymentDto`, `ReceivablePaymentRequest`, `ReceivableInvoiceTraceDto`.
  DTO ve isteklerde `TenantId` ve `LegalEntityId` **yoktur**.
- **Portlar (Application):** `ICustomerReferencePort` (MOD-0149), `ILegalEntityEligibilityPort` (MOD-0220),
  `IReceivableWorkflowPort` (MOD-0023), `ICurrencyReferencePort` (MOD-0048), `IRecordCodePort` (RCS-001 ortak kod
  servisi), `IFinanceTreasuryDataScopeSource` (SEC-002, D14/1 — **servis ortak** tek çözüm noktası, MOD-0126 pack ile
  aynı arayüz), `IReceivableAdjustmentPolicyProvider` (tek eşik, O-7). MOD-0126 ile aynı adı taşıyan portlar (LE, para
  birimi, kod, veri kapsamı) servis içinde **tek** bağdaştırıcıya bağlanır; ikinci kopya yazılmaz. **Tek sahibi ve yazarı
  MOD-0126 DL-1'dir** (gap 4/G5; D14/2 ile tutarlı): ortak portlar, bağdaştırıcıları, makbuz tipi + indeksleri, veri
  kapsamı kaynağı ve CentralAudit kaydı orada açılır; MOD-0120 bunları yeniden tanımlamaz ve önce açmaz.
- **Frontend rotası:** `/FinanceTreasury/ReceivableInvoices` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`).
- **İzinler:** "Authorization Convention" tablosu.

### Fatura durum makinesi (`ReceivableInvoiceStatus`)

Kaynak `InvoiceLifecycleStatus` 8 değer (`AccountsReceivableEnums.cs:7-17@218acaf5`); geçişler
`AccountsReceivableStateGuards.cs:9-45@218acaf5`. Recovery 5 değer kullanır:

| Recovery | Değer | Kaynak karşılığı | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 0 | DRAFT | Create, Update | Issue, Update, Delete |
| Open | 1 | ISSUED + OPEN (birleştirildi) | Issue; onaylı geri alma sonrası `PaidAmount = 0` | tahsilat (→ PartiallyPaid/Paid), onaylı Void |
| PartiallyPaid | 2 | PARTIALLY_PAID | tahsilat (kalan > 0); onaylı geri alma sonrası `0 < PaidAmount` | tahsilat (→ Paid); onaylı geri alma (→ PartiallyPaid/Open) |
| Paid | 3 | PAID | tahsilat (kalan = 0) | yalnız onaylı tahsilat geri alma (→ PartiallyPaid/Open, D16) |
| Void | 4 | VOID | onaylı Void düzeltmesi | — (terminal) |

- **Taşınmayan değerler:** `ISSUED` (müşteriye gönderim faturalamanın durumudur, karar 0.5; burada `Open` ile
  birleşir), `CANCELLED` (taslak iptal edilmez, soft delete edilir; açılmış fatura `Void` olur), `REVERSED` (ödenmiş
  faturanın tersine çevrilmesi kredi notu / mutabakat işidir, Follow-up).
- **Alacak silme (WriteOff) durum değiştirmez:** kaynak modelinde olduğu gibi (`CollectionState.WRITTEN_OFF`,
  `AccountsReceivableEnums.cs:28-39@218acaf5`) silme **tahsilat aşamasıdır**: onaylı WriteOff → `CollectionStage =
  WrittenOff` (terminal), `OutstandingAmount = 0`, `WrittenOffAmount` = silinen tutar; `Status` (Open / PartiallyPaid)
  değişmez. `Status` para hareketini, `CollectionStage` tahsilat sürecini anlatır.

### Tahsilat aşaması (`ReceivableCollectionStage`, saklanır) ve yaşlandırma (`ReceivableAgingState`, türetilir)

Kaynak `CollectionState` 9 değeri (`AccountsReceivableEnums.cs:28-39@218acaf5`) iki ayrı kavrama ayrılır, çünkü
`NOT_DUE` / `DUE` / `OVERDUE` tarihten türer ve saklandığında bir zamanlayıcı ister (Recovery'de bu dilimde arka plan
işi yok):

- **Türetilen yaşlandırma** (okuma anında, saklanmaz): `NotDue` (bugün < `DueDate`), `Due` (bugün = `DueDate`),
  `Overdue` (bugün > `DueDate`) + `DaysOverdue`. "Bugün"ün saat dilimi **O-12** (öneri: UTC takvim günü). Taksitli
  faturada en eski ödenmemiş taksitin vadesi kullanılır.
- **Saklanan aşama** ve izinli geçişler (kaynak tablosundan; `NOT_DUE/DUE/OVERDUE` hedefleri `None`'a karşılık gelir):

| Aşama | Değer | `SetReceivableInvoiceCollectionStageCommand` ile izinli hedef | Diğer giriş |
|---|---|---|---|
| None | 0 | InCollection, PromiseToPay, Disputed | başlangıç; düzeltme talebi reddi/iptali (önceki aşama `None` ise) |
| InCollection | 1 | Dunning, PromiseToPay, Disputed, None | — |
| Dunning | 2 | InCollection, PromiseToPay, Disputed | — |
| PromiseToPay | 3 | None, InCollection, Disputed | — (`PromiseToPayDate` zorunlu) |
| Disputed | 4 | None, InCollection | — (`ReasonCode` zorunlu) |
| WriteOffProposed | 5 | — (elle girilemez, elle çıkılamaz) | yalnız WriteOff talebi tx1 |
| WrittenOff | 6 | — (terminal) | yalnız onaylı WriteOff (tx2 / Reconcile) |

  Aşama yalnız `Open` / `PartiallyPaid` faturada değişir; fatura `Paid` olunca aşama `None`'a döner. Her geçiş gömülü
  `CollectionStageHistory`'ye (ekleme-yalnız) ve merkezi denetime yazılır.

### Düzeltme talebi (`ReceivableAdjustmentRequest.Status`) ve tek geçiş matrisi

| Durum | Değer | Anlamı | Çıkış |
|---|---|---|---|
| Submitted | 0 | yerel talep + başlatma niyeti yazıldı; MOD-0023 sonucu kesin değil | kesin başarı → PendingApproval; kesin ret → StartRefused; belirsiz → Reconcile |
| PendingApproval | 1 | MOD-0023 örneği başladı | Approve → Approved; Reject → Rejected; Reconcile (örnek iptal) → Cancelled |
| Approved | 2 | onaylandı ve **etki faturaya uygulandı** (aynı tx2) | — (terminal) |
| Rejected | 3 | reddedildi; fatura aşaması geri döndü | — (terminal) |
| StartRefused | 4 | MOD-0023 başlatmayı kesin reddetti; fatura aşaması geri döndü | — (terminal; yeni talep açılabilir) |
| Cancelled | 5 | MOD-0023 örneği iptal / terk edilmiş bulundu (yalnız Reconcile) | — (terminal) |

Başlatma niyeti ve karar niyeti MOD-0126 ile **aynı sonuç sözlüğünü** kullanır (MOD-0126 pack, gap 2/G5; servis geneli
durum sözlüğü, D14/2):

| Niyet | Açık → | tx2 (kesin sonuç) | belirsiz | Reconcile (yalnız `ReconcileReceivableInvoiceAdjustmentCommand`) | Sonuç olayı |
|---|---|---|---|---|---|
| `ReceivableAdjustmentStartIntent.Status` | `Pending` | `Started` · `Refused` | `Unknown` | `Unknown`/`Pending` → `Started` · `Refused` · `Cancelled` | `…adjustment-submitted` · `…adjustment-start-refused` · `…adjustment-start-cancelled` |
| `ReceivableAdjustmentDecision.State` | `Requested` | `Recorded` · `Refused` | `Unknown` | `Unknown`/`Requested` → `Recorded` · `Refused` · `Cancelled` | `…adjustment-approved` / `…adjustment-rejected` · `…adjustment-decision-refused` · `…adjustment-decision-cancelled` |

`Refused` = MOD-0023 kesin reddetti; `Cancelled` = MOD-0023 örneği ya da görevi iptal / kapalı bulundu (yalnız Reconcile,
yalnız uzak tarafın **kesin** iptal/kapanış kaydıyla — o örnek için MOD-0023 iptal geçiş kaydı ya da
`CloseAbandonedWorkflowStart` kapanışı; bulunamadı / 404 / zaman aşımı / belirsiz okuma `Cancelled` sayılmaz,
niyet `Unknown` kalır — Runtime Constraints 4a).

## Entity Fields

Taban: servisin `EntityBase`'i (`entity-base-template.md`): `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`,
`UpdatedAt`, `Version` — tekrar tanımlanmaz. İş modülü: `CreatedBy` / `UpdatedBy` (`Guid?`, token `sub`'ından)
**zorunlu**. Kaynak tabanı (`AccountsReceivableEntities.cs:50-56@218acaf5`) `DateTime` + `string CreatedBy = "system"` +
`long RecordVersion` + istemci `CorrelationId` taşır; Recovery'de `DateTimeOffset` (UTC), `int Version` (iyimser
eşzamanlılık), `Guid` aktör. Kaynakta silme yok (`IsDeleted` 0 eşleşme, paket §F); Recovery'de soft delete `IsDeleted`
+ `DeletedAt` (UTC) birlikte ve yalnız `Draft` faturada.

**Kapsam alanları (her varlıkta):** `TenantId` (`ITenantContext`) ve `LegalEntityId` (isteğin `X-Legal-Entity-Id`
başlığı, MOD-0220'den doğrulanmış; R-2 deseni, MOD-0126 pack Entity Fields) sunucuda yazılır; gövde ya da sorguda gelen
`tenantId` / `legalEntityId` 400 ile reddedilir (kaynakta `MOD0120_TENANT_MISMATCH`, `AccountsReceivableController.cs:948-953@218acaf5`
— kod taşınmaz, davranış korunur). Alt kayıtlar faturanın `LegalEntityId`'sini taşır.

**Para (SOP §18.8, domain-config "Para / decimal") — dokuz başlık:**

| Başlık | Kural |
|---|---|
| decimal | tüm tutarlar `decimal` (Mongo `Decimal128`); `float`/`double` yasak (kaynakta da, `AccountsReceivableEntities.cs:4-5@218acaf5`) |
| para birimi kaynağı | fatura başına **tek** ISO 4217 kodu, MOD-0048 `GET /api/lookups/reference/currencies` listesinde; satır, taksit, tahsilat ve düzeltme faturanın para birimini taşır, farklıysa ret |
| hassasiyet | para biriminin küsurat hanesi MOD-0048'den; kaynak **TBD** (MOD-0126 O-11 ile aynı karar — tek karar iki pakete uygulanır); hane yoksa yazma 503 `RECEIVABLE_CURRENCY_PRECISION_UNAVAILABLE`, sabit tablo uydurulmaz |
| yuvarlama | **yuvarlama yapılmaz.** Hane aşan tutar **reddedilir**. Miktar × birim fiyat bu sürümde yoktur (fiyatlama faturalamanındır, karar 0.5); toplamlar tam decimal toplamıdır, yuvarlama gerektirmez. Vergi motoru yok: satır vergisi kullanıcı girdisidir (O-11) |
| saat dilimi | zaman damgaları UTC `DateTimeOffset`; `InvoiceDate`, `DueDate`, taksit vadesi, `PromiseToPayDate`, `PaymentDate` yalnız tarih (`DateOnly`), çevrim yapılmaz; yaşlandırmanın "bugün"ü O-12 |
| hafta sınırı | uygulanmaz — bu sürümde haftalık kova yok (projeksiyon MOD-0125'le birlikte taşınmadı) |
| giriş/çıkış modeli | AR yalnız **giriş** (alacak) tutar; tüm tutarlar ≥ 0; negatif satır / kredi notu yok (Follow-up) |
| kur (FX) | çevrim yok; yalnız belge para birimi (kaynak `FxHandling.DOCUMENT_CURRENCY_ONLY`, `AccountsReceivableEnums.cs:239-243@218acaf5`) |
| değişmez anlık görüntü | `Issue` sonrası satırlar, taksitler, toplamlar, para birimi, müşteri ve müşteri anlık görüntüsü **değişmez**; düzeltme yalnız onaylı Void / WriteOff, tahsilat ya da onaylı tahsilat geri alma (karşı kayıt, D16) ile |

### ReceivableInvoice (`receivable_invoices`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| InvoiceCode | string | RCS-001 OTOMATİK `{ŞİRKET}-{TÜR}-{SAYAÇ}`; tür öneki önerisi `ARI` (O-6; `INV` §3'te Yatırım Dosyası'na ayrılmış — ölçüldü); kullanıcı giremez/değiştiremez; silinen faturanın kodu yeniden kullanılmaz. **İç kayıt kodudur, yasal fatura numarası değildir** (karar 0.5) | kaynak `InvoiceNumber` (:20) içe aktarmadan geliyordu — taşınmaz |
| CustomerId | Guid | zorunlu; MOD-0149 `Account.Id`; `ICustomerReferencePort` ile doğrulanır (Create, Update, Issue) | :14 (kaynakta çağıranın verdiği, ihraççısız Guid — paket MOD-0149 §E) |
| CustomerCodeSnapshot / CustomerNameSnapshot / CustomerSnapshotAt | string / string / DateTimeOffset | **sunucu** MOD-0149 yanıtından yazar (taslakta her yazmada, `Issue`'da son kez); yalnız görüntüdür, uygunluk değildir | :15-19 (kaynakta `CustomerDisplayProvenance` serbest string — taşınmaz) |
| ExternalInvoiceNumber | string? | trim, ≤ 64; dışarıda kesilmiş yasal faturanın numarası (O-5) | kaynak `SourceInvoiceNumber` (:21) |
| ExternalInvoiceNumberKey | string? | sunucu: büyük harf + trim; soft delete ve Void'de `null` yapılır; mükerrer kontrol anahtarı | yeni |
| InvoiceDate | DateOnly | zorunlu | :24 |
| DueDate | DateOnly | zorunlu, ≥ InvoiceDate; taksit varsa son taksitin vadesine eşit | :25 |
| ExpectedCollectionDate | DateOnly? | ≥ InvoiceDate; yalnız `Draft`'ta ve `PromiseToPay` geçişinde değişir | :26 |
| CurrencyCode | string(3) | zorunlu, ISO 4217, upper | :29 |
| CollectionDestinationRef | Guid? | **opak** MOD-0289 tahsilat hesabı referansı; boş değilse Guid ≠ Empty; IBAN/hesap no tutulmaz | kaynak `CollectionDestinationId` (:28) |
| Notes | string? | trim, ≤ 2000 | — |
| Lines | `ReceivableInvoiceLine[]` | 1..200 (öneri) | kaynak ayrı koleksiyon (`ReceivableLine`) — gömülür |
| Installments | `ReceivableInstallment[]` | 0..24 (öneri); boşsa sunucu tek taksit (`DueDate`, `GrossTotal`) yazar | kaynak ayrı koleksiyon — gömülür |
| NetTotal / TaxTotal / GrossTotal | decimal | **sunucu**: satır toplamları; GrossTotal > 0 | :30-32 |
| PaidAmount / WrittenOffAmount / OutstandingAmount | decimal | **sunucu**; `Outstanding = Gross − Paid − WrittenOff`; Void'de `Outstanding = 0` | :33 |
| Status | `ReceivableInvoiceStatus` | durum makinesi | :34 |
| CollectionStage / PromiseToPayDate / CollectionReasonCode | enum / DateOnly? / string? | aşama tablosu | :36-38 (kaynak `CollectionStrategyCode`, `DunningStage` serbest string — taşınmaz) |
| CollectionStageHistory | `ReceivableCollectionStageChange[]` | ekleme-yalnız: From, To, ReasonCode, ActorId (Guid), At (UTC); ≤ 500 | yeni |
| OpenAdjustmentRequestId | Guid? | açık düzeltme talebi varken dolu; tahsilat ve ikinci talep 409 | yeni |
| IssuedBy / IssuedAt | Guid? / DateTimeOffset? | sunucu (Issue) | yeni (SoD A2) |
| CreatedBy / UpdatedBy | Guid? | token `sub` | — |

**Taşınmayan kaynak alanları:** `SourceCustomerReference`, `CustomerDisplayProvenance`, `SourceSystem`, `SourceRecordId`,
`InvoicePayloadHash`, `IdempotencyKey`, `SourceWatermarkAtUtc`, `BatchId` (içe aktarma, O-10); `PostingStatus`,
`GlPostingReferenceId` (GL, karar 0.7); `ExpectedCollectionDateSource` (projeksiyon); `CorrelationId` (başlıktan gelir,
varlıkta tutulmaz; merkezi denetim taşır).

### ReceivableInvoiceLine (gömülü) ve ReceivableInstallment (gömülü)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| Line.LineNumber | int | sunucu, 1'den ardışık | `ReceivableLine` :64 |
| Line.Description | string | zorunlu (öneri; kaynakta `string?`), trim, 1..256 | :65 |
| Line.NetAmount | decimal | > 0; küsurat ≤ hane | :71 |
| Line.TaxAmount | decimal | ≥ 0; küsurat ≤ hane; kullanıcı girdisi (vergi motoru yok, O-11) | :70 |
| Line.GrossAmount | decimal | **sunucu** = Net + Tax | :69 |
| Installment.InstallmentNumber | int | sunucu | `ReceivableSchedule` :89 |
| Installment.DueDate | DateOnly | ≥ InvoiceDate; artan sıra, tekrar yok | :90 |
| Installment.Amount | decimal | > 0; küsurat ≤ hane; toplam = GrossTotal | :93 |
| Installment.OutstandingAmount | decimal | **sunucu**; tahsilat en eski taksitten düşer | :94 |

**Taşınmayan satır alanları:** `Quantity`, `UnitPrice` (fiyatlama faturalamanın, karar 0.5), `GlAccountCode`,
`CostCenterCode` (GL, karar 0.7), satır başına `CurrencyCode` (faturadan türetilir).

> **`form_field_count: 13` → `golden_reference: compact`.** Oluştur/düzenle formunda kullanıcının doldurduğu alanlar:
> **başlık 8** — CustomerId (müşteri seçici), ExternalInvoiceNumber, InvoiceDate, DueDate, ExpectedCollectionDate,
> CurrencyCode, CollectionDestinationRef, Notes; **satır 3** — Description, NetAmount, TaxAmount; **taksit 2** —
> DueDate, Amount. **Sayılmayanlar:** InvoiceCode (RCS-001 OTOMATİK), LegalEntityId (oturum şirketi,
> `X-Legal-Entity-Id`), müşteri anlık görüntüleri (sunucu, MOD-0149), satır GrossAmount ve tüm toplamlar (sunucu),
> Status / CollectionStage (ayrı komutlar), Idempotency-Key (başlık; yalnız `FinanceTreasuryCommandReceipt`'te saklanır) /
> CorrelationId (başlık). Düzeltme talebi ve tahsilat Details sayfasındaki eylem diyaloglarıdır, create/edit formu
> değildir. 13 > 8 → Compact (ayrı Create / Edit / Details).

**Banka verisi yasağı:** hiçbir varlık IBAN, hesap numarası, BIC/SWIFT, banka adı tutmaz; yalnız opak Guid
(domain-config "Banka verisi bu domain'de saklanmaz").

### ReceivableAdjustmentRequest (`receivable_adjustment_requests`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| InvoiceId | Guid | — |
| AdjustmentType | enum `Void`=1, `WriteOff`=2, `PaymentReversal`=3 | Void: `Status = Open` ve `PaidAmount = 0`; WriteOff: `Status ∈ {Open, PartiallyPaid}`, `Outstanding > 0`, ve (yaşlandırma `Overdue` **ya da** aşama ∈ {InCollection, Dunning, Disputed}) — kaynak `WRITE_OFF_PROPOSED` girişleri (`AccountsReceivableStateGuards.cs:63-98@218acaf5`); **PaymentReversal (D16):** `Status ∈ {PartiallyPaid, Paid}`, `CollectionStage ≠ WrittenOff`, `ReversedPaymentId` bu faturanın `EntryType = Receipt` ve `IsReversed = false` tahsilatı |
| ReversedPaymentId / ReversedPaymentRecordedBy | Guid? / Guid? | yalnız `PaymentReversal`'da zorunlu; geri alınan tahsilat ve onu kaydeden aktör talep anında **sabitlenir** (SoD A7); diğer türlerde istekte gelirse 400 |
| Amount / CurrencyCode | decimal / string(3) | **sunucu**: Void / WriteOff'ta talep anındaki `OutstandingAmount` (tam tutar; kısmi silme Follow-up), PaymentReversal'da geri alınan tahsilatın `Amount`'u; faturanın para birimi; sabitlenir |
| ReasonCode | string | zorunlu, trim, 1..64 |
| Comment | string? | trim, ≤ 2000; denetime yazılmaz |
| EvidenceRefs | Guid[] | MOD-0031 bağlantı kimlikleri; WriteOff için en az 1 (öneri, O-13); ikili veri yok |
| Status | `ReceivableAdjustmentStatus` | yukarıdaki tablo |
| PreviousCollectionStage | `ReceivableCollectionStage` | ret / başlatma reddi / iptalde geri dönüş |
| RequestedBy / RequestedAt | Guid / DateTimeOffset | sunucu (= MOD-0023 başlatanı) |
| InvoiceCreatedBy / InvoiceIssuedBy | Guid? / Guid? | talep anında faturadan **sabitlenir** (SoD A2) |
| PolicyVersion / Tier2ThresholdAmount / Tier2ThresholdCurrency / IsTier2 / PolicySource | string / decimal? / string? / bool / enum | talep anında sabitlenir (O-7) |
| CandidatePrincipalIds / ExcludedPrincipalIds | Guid[] | çözümleyici sonucu |
| WorkflowInstanceId / WorkflowCorrelationId | Guid? / string? | yalnız kesin başlatma sonucundan |

### ReceivableAdjustmentStartIntent (`receivable_adjustment_start_intents`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| AdjustmentRequestId, InvoiceId | Guid, Guid | — |
| IdempotencyKey | string | **uzak**, deterministik: `mod0120:{adjustmentId:N}:start`; talep başına tek başlatma (kesin retten sonra yeni talep açılır) |
| Status | enum Pending/Started/Refused/Cancelled/Unknown | geçiş matrisi |
| WorkflowInstanceId | Guid? | yalnız kesin sonuçtan |
| LastOutcomeCode / LastAttemptAt / AttemptCount | string? / DateTimeOffset? / int | `RECEIVABLE_WORKFLOW_START_*` kodları |
| CorrelationId | string | — |

### ReceivableAdjustmentDecision (`receivable_adjustment_decisions`, yeni)

`AdjustmentRequestId`, `InvoiceId`, `Decision` (Approve | Reject), `ReasonCode`, `Comment`, `DecidedBy` (Guid),
`DecidedAt`, `State` (Requested/Recorded/Refused/Cancelled/Unknown), `RemoteIdempotencyKey` (MOD-0023 görev geçişine
giden **uzak** anahtar, deterministik `mod0120:{decisionId:N}:transition` — istemci anahtarı değildir),
`WorkflowInstanceId`, `WorkflowTaskId`, `WorkflowTransitionId`, `WorkflowIsFinalStep`, `SodResult`
(`Passed` | `FailedRequesterDecision` | `FailedInvoiceCreatorDecision` | `FailedInvoiceIssuerDecision` |
`FailedPaymentRecorderDecision` | `FailedAmountTier` | `FailedRoleConflict`), `PolicyVersion`, `RequestFingerprint` / `FingerprintVersion`.
Muafiyet (waiver) **yoktur** (O-8).

### ReceivablePayment (`receivable_payments`, yeni — sahip kararı D16)

`InvoiceId`, `CustomerId` (**sunucu**, faturadan; iş tekilliği anahtarının parçası), `EntryType` (`Receipt`=1 — elle
tahsilat | `Reversal`=2 — onaylı geri almanın karşı kaydı), `Amount` (> 0, Receipt'te ≤ OutstandingAmount, küsurat ≤ hane;
yön `EntryType`'tan), `CurrencyCode` (= fatura), `PaymentDate` (DateOnly, ≥ InvoiceDate, bugünden sonra değil — O-12
"bugün"ü; Reversal'da onay günü), `PaymentReference` (Receipt'te zorunlu, trim, 1..64; banka referansı değeri değil, iç
referans), `PaymentReferenceKey` (**sunucu**: trim + büyük harf + iç boşluklar tekleştirilmiş; mükerrer kontrol anahtarı;
Reversal'da `null`), `InstallmentAllocations` (taksit numarası + düşülen tutar; Receipt'te en eski taksitten yazılır,
Reversal'da aynısı geri yazılır), `ReceiptId` (tahsilat makbuzu), `RecordedBy` (Guid), `RecordedAt`. Geri alma alanları:
Receipt'te `IsReversed` (bool, `false`), `ReversedByPaymentId` / `ReversedAt` (`null`); Reversal'da `ReversesPaymentId` ve
`AdjustmentRequestId` (onaylı `PaymentReversal` talebi). **Ekleme-yalnız**: güncelleme ve silme yok; tutar, para birimi,
tarih, referans ve dağılım **hiç değişmez**. Tek istisna: onaylı geri almanın tx2'sinde orijinal Receipt'e **bir kez**
`IsReversed = true` + `ReversedByPaymentId` + `ReversedAt` yazılır (tek yönlü; geri alma geri alınamaz). Fazla tahsilat
(Outstanding'i aşan) reddedilir — eşleşmemiş nakit Follow-up.

**İş tekilliği ve mükerrer tahsilat (D16, gap 6/P2-a):** `(TenantId, LegalEntityId, CustomerId, PaymentReferenceKey)`
geri alınmamış Receipt'ler arasında tekildir (`ux_receivable_payment_reference_active`). Aynı ödeme farklı idempotency
anahtarıyla ya da başka aktörden (makbuz kapsamı aktör içerdiği için makbuz bunu yakalamaz) ikinci kez gelirse — aynı ya da
aynı müşterinin başka faturasına — 409 `RECEIVABLE_PAYMENT_DUPLICATE`, kayıt yok. **Politika (v1): geri alınan tahsilatın
referansı serbest kalır** — onaylı geri almadan sonra aynı referansla yeni Receipt girilebilir (indeks yalnız `IsReversed =
false` kayıtları kapsar; geri alma çoğunlukla yanlış fatura / tutarla girilmiş tahsilatın düzeltilmesidir). Sahip bunu
değiştirirse indeksin `IsReversed` filtresi kaldırılır ve bu cümle o kararla yeniden yazılır.

### Makbuz — `FinanceTreasuryCommandReceipt` (servis ortak, D14/2)

Tip, alanlar, altı alanlı kapsam, `Pending`/`Completed` yaşam döngüsü, değişmezlik (`Completed` makbuz bir daha
değişmez), "tekrar asla kurtarma yapmaz" ve "Reconcile yalnız `Pending` makbuzu `Completed` yapar" kuralları **tek
yerde** tanımlıdır: **MOD-0126 pack Entity Fields — FinanceTreasuryCommandReceipt** (koleksiyon
`finance_treasury_command_receipts`). Burada yeniden tanımlanmaz; AR'ye özgü olanlar yalnız şunlardır:

- **`Operation`:** `mod0120:{Komut}` — ör. `mod0120:CreateReceivableInvoice`, `mod0120:RequestReceivableInvoiceAdjustment`,
  `mod0120:ApproveReceivableInvoiceAdjustment`, `mod0120:RecordReceivableInvoicePayment`.
- **`TargetId`:** Create ve BulkDelete için `null`, diğerlerinde fatura kimliği (karar ve uzlaştırmada düzeltme kimliği).
- **İki aşamalı komutlar** (tx1'de `Pending`): RequestAdjustment, Approve, Reject; `LinkedIntentId`
  `ReceivableAdjustmentStartIntent` ya da `ReceivableAdjustmentDecision` kimliğidir. Diğer bütün yazma komutları tek
  aşamalıdır (tx1'de `Completed`).
- Belirsiz sonuçta kayıtlı 202 yanıtının gösterdiği gerçek sonuç `GET /api/receivable-invoices/{id}` ve `…/trace`
  üzerinden okunur.

**İstemci anahtarı yalnız makbuzdadır:** `ReceivableInvoice`, `ReceivableAdjustmentRequest`, `ReceivableAdjustmentDecision`,
`ReceivablePayment` istemci anahtarı alanı taşımaz (kaynak `ReceivableInvoice.IdempotencyKey`, :41, taşınmaz).

### Mongo indeksleri (kaynak 59 model, 22 tekil, 1 kısmi — `AccountsReceivableMongoIndexes.cs:110-520@218acaf5`, paket §E)

Kaynak indekslerinin hiçbiri olduğu gibi taşınmaz (varlıkların çoğu taşınmıyor; kalanların anahtarı değişti).

| Recovery indeksi | Anahtar | Tekil | Not |
|---|---|---|---|
| `ux_receivable_invoice_code` | TenantId + InvoiceCode | **evet, kısmi filtre YOK** (silinen faturanın kodu da dolu, RCS-001 §6) | — |
| `ux_receivable_invoice_external_number` | TenantId + LegalEntityId + ExternalInvoiceNumberKey, kısmi `{ExternalInvoiceNumberKey: {$type: "string"}}` | evet | mükerrer dış numara (O-5) |
| `ux_receivable_adjustment_open` | TenantId + InvoiceId, kısmi `Status ∈ {Submitted, PendingApproval}` | evet — faturada tek açık düzeltme | — |
| `ux_receivable_adjustment_start_remote_key` | TenantId + IdempotencyKey (uzak) | evet | MOD-0023'e tek başlatma |
| `ux_receivable_adjustment_start_open` | TenantId + AdjustmentRequestId, kısmi `Status ∈ {Pending, Unknown}` | evet | — |
| `ux_receivable_adjustment_decision_open` | TenantId + AdjustmentRequestId, kısmi `State ∈ {Requested, Unknown}` | evet — talepte tek açık karar niyeti | — |
| `ix_receivable_invoice_list` | TenantId + LegalEntityId + IsDeleted + Status + DueDate | — | liste ve yaşlandırma |
| `ix_receivable_invoice_customer` | TenantId + LegalEntityId + CustomerId + DueDate | — | müşteri filtresi |
| `ix_receivable_invoice_stage` | TenantId + LegalEntityId + CollectionStage | — | tahsilat aşaması filtresi |
| `ix_receivable_adjustment_invoice` | TenantId + InvoiceId + RequestedAt(desc) | — | detay sekmesi |
| `ix_receivable_payment_invoice` | TenantId + InvoiceId + PaymentDate | — | detay sekmesi |
| `ux_receivable_payment_reference_active` | TenantId + LegalEntityId + CustomerId + PaymentReferenceKey, kısmi `{EntryType: "Receipt", IsReversed: false}` | evet — iş tekilliği (D16, gap 6/P2-a) | mükerrer tahsilat; geri alınmış Receipt referansı serbest bırakır (politika, Entity Fields) |
| `ux_receivable_payment_reversal_of` | TenantId + ReversesPaymentId, kısmi `{EntryType: "Reversal"}` | evet — bir tahsilata en fazla bir karşı kayıt | D16 |

**Makbuz indeksleri burada bildirilmez (D14/2):** `ux_finance_treasury_command_receipt_scope` (altı alanlı tekil kapsam) ve
`ix_finance_treasury_command_receipt_pending` servis ortak `finance_treasury_command_receipts` koleksiyonundadır ve sahibi
MOD-0126 DL-1'dir (MOD-0126 pack Entity Fields → Mongo indeksleri); MOD-0120 yeniden bildirmez.

Kısmi indeks ifadeleri `$in` yerine eşitlik/`$type` ile yazılır; Mongo kısmi filtresinin desteklediği biçim DL-1'de
gerçek replika kümesinde ölçülür (kaynak §N.8: indeks semantiği kaynakta da ölçülmemiş). Kurulum servis iskeletinin
indeks barındırılan hizmetiyle (`mongo-indexing`); taşıma/geri doldurma yok.

### Hata sözlüğü (`RECEIVABLE_*`; kaynak 134 `MOD0120_*`, `AccountsReceivableErrorCodes.cs@218acaf5`, paket §E)

Kaynak kodları **kopyalanmaz**; anlamı korunanlar Recovery adıyla yeniden yazılır:

- **Korunan anlam:** `RECEIVABLE_IDEMPOTENCY_KEY_REQUIRED` 400 (kaynak `MOD0120_IDEMPOTENCY_KEY_REQUIRED`),
  `RECEIVABLE_IDEMPOTENCY_CONFLICT` 409 (`MOD0120_IDEMPOTENCY_CONFLICT`), `RECEIVABLE_CUSTOMER_NOT_FOUND` 422
  (`MOD0120_REFERENCE_CUSTOMER_UNRESOLVED`), `RECEIVABLE_CUSTOMER_UNAVAILABLE` 503 (`MOD0120_REFERENCE_CUSTOMER_UNAVAILABLE`),
  `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE` 422, `RECEIVABLE_COLLECTION_TRANSITION_INVALID` 409.
- **Yeni:** `RECEIVABLE_VALIDATION_FAILED` 400, `RECEIVABLE_INVOICE_NOT_FOUND` 404, `RECEIVABLE_INVOICE_VERSION_CONFLICT`
  409, `RECEIVABLE_INVOICE_STATUS_INVALID` 409, `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE` 422 (O-4), `RECEIVABLE_LEGAL_ENTITY_UNAVAILABLE`
  503, `RECEIVABLE_CURRENCY_PRECISION_UNAVAILABLE` 503, `RECEIVABLE_RECORD_CODE_UNAVAILABLE` 503,
  `RECEIVABLE_EXTERNAL_NUMBER_DUPLICATE` 409, `RECEIVABLE_REQUEST_IN_PROGRESS` 202 (makbuz `Pending`; kurtarma yok),
  `RECEIVABLE_ADJUSTMENT_NOT_FOUND` 404, `RECEIVABLE_ADJUSTMENT_PENDING` 409 (açık düzeltme), `RECEIVABLE_ADJUSTMENT_NOT_ALLOWED`
  409 (Void/WriteOff ön koşulu yok), `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN` 202, `RECEIVABLE_WORKFLOW_START_FAILED` 502
  (yalnız kesin ret), `RECEIVABLE_WORKFLOW_START_PENDING` 409, `RECEIVABLE_DECISION_OUTCOME_UNKNOWN` 202,
  `RECEIVABLE_DECISION_PENDING` 409, `RECEIVABLE_DECISION_STATUS_INVALID` 409, `RECEIVABLE_WORKFLOW_TASK_INVALID` 400,
  `RECEIVABLE_SOD_DENIED` 403, `RECEIVABLE_TIER2_APPROVER_REQUIRED` 403, `RECEIVABLE_ADJUSTMENT_POLICY_UNAVAILABLE` 503,
  `RECEIVABLE_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` 422, `RECEIVABLE_PAYMENT_EXCEEDS_OUTSTANDING` 422,
  `RECEIVABLE_PAYMENT_DUPLICATE` 409 (aynı kiracı + şirket + müşteri + ödeme referansıyla geri alınmamış tahsilat var — D16;
  anahtar ya da aktör farklı olsa da), `RECEIVABLE_PAYMENT_NOT_FOUND` 404 (geri alınacak tahsilat bu faturada yok),
  `RECEIVABLE_RECONCILE_RESTART_ACTOR_MISMATCH` 409 (Reconcile'ı çağıran asıl komutun aktörü değil; uzak yazma/başlatma
  çağrısı yok, niyet `Unknown` kalır).
- **Taşınmaz:** `MOD0120_TENANT_MISMATCH` (→ 400 `RECEIVABLE_VALIDATION_FAILED`), `MOD0120_LEGAL_ENTITY_ACCESS_DENIED`
  (kaynakta boş LE kapsamında 400, `AccountsReceivableController.cs:969-973@218acaf5` → Recovery'de **boş sonuç**, SEC-002
  §3.3), `MOD0120_ABAC_LEGAL_ENTITY_DENIED` (kapsam dışı yazma → Recovery'de **404**, multi-tenancy), `MOD0120_AUDIT_UNAVAILABLE`
  (CentralAudit; K2 hatası komutu başarısız kılar), içe aktarma / projeksiyon / geçersiz kılma / kanıt dışa aktarma kodları
  (alt sistemler taşınmadı).
- Kullanıcıya giden ileti resx anahtarıdır; teknik metin ekrana çıkmaz (UAS-001 §5).

## Repo Scope

Hepsi kod alma izninden (D1, MOD-0120) ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.FinanceTreasuryService/**` — servis **bu paketle açılmaz**: iskelet (5 katman, port 5071, JWT,
  `TenantId` izolasyonu, pipeline davranışları, `AddCentralAudit<…>(RequireEnlistment = true)`, modül manifest kaydı) ve
  servis ortak yapı taşları **yalnız MOD-0126 DL-1'inde** açılır (tek sahibi ve yazarı — gap 4/G5, D14/2 ile tutarlı).
  MOD-0126 DL-1, MOD-0120 DL-1'in önkoşuludur; MOD-0120 önce `ready-for-dev` olsa bile iskeleti açmaz, bekler (tek
  iskelet, K15). MOD-0120'nin kendi yolları: (DL-1…DL-3)
  - `src/Diten.FinanceTreasuryService.Application/Features/ReceivableInvoice/**` (Golden Reference Compact düzeni)
  - `src/Diten.FinanceTreasuryService.Domain/ReceivableInvoices/**`
  - `src/Diten.FinanceTreasuryService.Persistence/ReceivableInvoices/**` (depolar, indeks hizmeti; her işlem geri çağrısı
    `CentralAuditTransaction.EnlistAsync` çağırır)
  - `src/Diten.FinanceTreasuryService.Infrastructure/Customers/**` (yeni port bağdaştırıcısı); LE, para birimi, kod ve
    veri kapsamı (`DataScope`) bağdaştırıcıları servis ortaktır ve MOD-0126 DL-1'indir (tek kopya); iş akışı
    bağdaştırıcısı MOD-0126'nın MOD-0023 bağdaştırıcısıyla ortaktır
  - Servis ortak yapı taşları (`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`
    — `FinanceTreasuryCommandReceipt` + iki indeksi, `IFinanceTreasuryDataScopeSource` okuma / yazma kapsamı; D14/1,
    D14/2) ve CentralAudit kaydı MOD-0126 DL-1 ile açılır; burada yalnız kullanılır, yeniden tanımlanmaz
  - `src/Diten.FinanceTreasuryService.Api/Controllers/ReceivableInvoicesController.cs`
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` — aynı servis, aynı iz
  (`finance-treasury-merkezi-iletim | b`); yeni satır gerekmez. `AuditTrailStandardTests` sayaçları (`PinnedCounts`)
  yeni bildirimlerle değişirse aynı değişiklikte `CT-DECISIONS.md` satırı — **CT kararı ister** (DL-1'de ölçülür).
- `docs/analysis/contracts/accounts-receivable.openapi.yaml` — yeni üretici sözleşmesi (x-owner MOD-0120). (DL-1)
- `frontend/Diten.Web/Views/FinanceTreasury/ReceivableInvoices/**`,
  `frontend/Diten.Web/wwwroot/assets/js/FinanceTreasury/ReceivableInvoices/**`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/ReceivableInvoices/ReceivableInvoicesIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/FinanceTreasury/ReceivableInvoicesController.cs` (same-origin MVC proxy),
  `PageHelpResource.{7 dil}.resx` içinde `Help.RECEIVABLEINVOICES.*` ve `Services/Help/PageHelpCatalog.cs` satırı,
  nav anahtarları `Nav.Domain.FINANCETREASURY` (MOD-0126 ile ortak), `Nav.Module.ACCOUNTSRECEIVABLE`,
  `Nav.Page.RECEIVABLEINVOICES`. (DL-4)

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9; burada yazılmaz):
MOD-0149 müşteri doğrulama + dış referans çözümleme eki (commercial-suite, O-3); MOD-0220 TRM-14 (MOD-0126 ile ortak);
MOD-0023 AR düzeltme iş akışı şablonu; `ocelot.json` rotaları (integration-agent); AGENTS.md §2/§3/§9 ve
`.antigravity/rules/ports.md` (5071; TRM-03/04); `record-coding-standard.md` §3 tablosuna `ARI` satırı; Platform
`AuditCategory` eki (MOD-0126 O-6 ile ortak); FinanceTreasury servisinde veri kapsamı çözücüsü (SEC-002, O-14).

## Protected Paths

- `.antigravity/**` (port bandı, RCS-001 §3 satırı ve kural değişiklikleri ayrı onay)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.CrmService/**` (MOD-0149 — commercial-suite), `services/Diten.Platform/**` (MOD-0023, MOD-0021,
  MOD-0048, MOD-0031, MOD-0018, `IDataScopeResolver` — platform-shared-services), `services/Diten.MdmService/**` (MOD-0220,
  MOD-0289 — master-data-management), `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**` (CentralAudit
  yalnız tüketilir) ve diğer domain servisleri
- `services/Diten.FinanceTreasuryService/**/Features/PaymentBatch/**` ve MOD-0126'nın diğer yolları (tek yazar MOD-0126);
  aynı klasördeki MOD-0119 / MOD-0124 paketleri ve yolları
- `frontend/Diten.Web/Views/Archive/**`, `Controllers/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone` (kaynak
  layout'u; Recovery'de kullanılmaz)
- Diğer üretici sözleşmeleri (`docs/analysis/contracts/*.openapi.yaml`) — tüketilir, yeniden tanımlanmaz
- `execution/registries/**`, `execution/domains/finance-treasury/domain-config.md`, DCP, CT kuyruğu — bu paketle değişmez
- Kaynak depo `/Users/natig/Projects/ERP-vNext` ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz

## Dependencies

Ölçüm Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c (`services/ frontend/ gateway/` MOD-0126 ölçüm tabanı `d43096a6b`'den
beri değişmedi — `git diff --stat d43096a6b 4e01e986269065cba6f962e0c13b9b2ae4519f6c -- services/ frontend/ gateway/` boş). MOD-0126 ile ortak satırlar o pack'in ölçümüne dayanır ve
burada yeniden yazılmaz, yalnız AR'ye etkisi yazılır.

| Kapı / bağımlılık | Rol | Recovery durumu | Bu pakete etkisi |
|---|---|---|---|
| **MOD-0003 Data Contract Registry** (Blueprint kapısı) | sözleşme kaydı | **yok** — kayıt satırı `planned / missing`, pack yok (`execution/registries/module-id-registry.md:61`); kaynakta da 0 eşleşme (paket §D) | **karar 0.3 açık.** Öneri: süreli T32 sapması S-1 (dondurulmuş `accounts-receivable.openapi.yaml`); karar gelmeden DCP §8 gereği TRM-S2 başlamaz |
| **AR-BILLING-BUNDLE** ("Billing integration surface") | faturalama sınırı | **üretici belirsiz** (düzeltme kaydı; sıra belgesi Amendment 3); MOD-0169 Billing & Invoicing `reserved / planned`, commercial-suite, "SoR boundary with Finance = EA-TBD" (`module-id-registry.md:289`); MOD-0169 pack **yok** (ölçüldü); sözleşme artefaktı iki depoda da yok | **karar 0.5 açık.** Öneri: kısıtlı MVP sapması S-2 (AR faturayı yerelde tutar, faturalama entegrasyonu yok); MOD-0169 sonra bağlanır (Follow-up) |
| **ERP-FIN-BUNDLE** | ortak arayüz sepeti | yedi platform kaygısı Recovery'de var (Gateway, JWT, kimlikler, denetim, kanıt, korelasyon, hata modeli — sıra belgesi Amendment 3 §3, devralınmış), yalnız GL veri sözleşmeleri yok | **karar 0.6 (EA) açık**; AR bu sürümde GL veri sözleşmesi istemez (karar 0.7 önerisi "kapsam dışı") |
| MOD-0118 General Ledger | GL kaydı | kayıt satırı yok (ölçüldü) | **karar 0.7 açık**; öneri kapsam dışı → `PostingStatus`/GL alanları taşınmaz |
| **MOD-0149 Customer 360** (müşteri SoR) | müşteri doğrulama + seçici + dış referans | **var, sözleşmesiz.** `GET /api/crm/accounts/{id}` izin `crm.account.read` (`services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/AccountController.cs:65-68`), liste `GET /api/crm/accounts` (:23-24); Gateway rotası `/api/crm/accounts` ve `/{everything}` (`ocelot.json:2951-2979`); `Account` kiracı geneli (LE alanı yok), `Status` MOD-0048 değer kodu (`Account.cs:24-25`); `AccountExternalReference` tekil (TenantId, SourceSystem, ExternalId) (`AccountExternalReference.cs:3-8`) ama **çözümleme ucu yok** — depo yalnız `ExistsBySourceExternalAsync` / `ListByAccountAsync` (`IAccountExternalReferenceRepository.cs:5-13`); `docs/analysis/contracts/` altında müşteri sözleşmesi **yok**; kayıt satırı `reserved / planned` iken pack `ready-for-dev` (TRM-09) | **O-3**: MOD-0149'a eklemeli `lookup-validation` + dış referans çözümleme ucu ve sözleşme belgesi (D9; commercial-suite sahibine sorulur, D9/3). O gelmeden müşteri doğrulaması yapılamaz → Create/Issue kapanır (fail-closed) |
| MOD-0220 LE `lookup-validation` | şirket uygunluğu (D10/1 deseni) | var; yanıtta `ApprovalStatus` yok (MOD-0126 pack Dependencies; `LegalEntitiesController.cs:48`, `LegalEntity.cs:49-50` yeniden ölçüldü) | **TRM-14** (MOD-0126 ile ortak). AR için "Active + Approved" kuralı D10/1'in uzantısıdır — **KARAR (D14/4)**: alacak oluşturan/değiştiren her yazmada; o gelmeden "onaylı" kontrolü yapılamaz → AR yazma komutları kapanır (fail-closed) |
| MOD-0023 Workflow Designer | düzeltme onayı | var (başlatma idempotency, görev onay/ret, başlatanın onayı `SodViolation` — MOD-0126 pack Dependencies) | AR düzeltme **şablonu** (`templateCode` TBD) MOD-0023 sahibinin işi (D9); v1 kullanıcı token'ıyla genel uç (MOD-0126 O-7 ile aynı) |
| MOD-0021 / CentralAudit (K4) | yol `b` | var (`Diten.BuildingBlocks.CentralAudit`, SupplyChain/Inventory kullanıyor) | servis iskeletiyle gelir; AR yalnız bildirim ekler |
| MOD-0048 para birimi | kod listesi + küsurat | kısmen — liste var, küsurat yok (MOD-0126 pack) | MOD-0126 **O-11** ile tek karar; yoksa yazma 503 |
| RCS-001 ortak kod servisi (§5.12, BL-565) | fatura kodu | **yok** (MOD-0126 pack) | **engel**: modül kendi sayacını yazamaz; servis gelene kadar Create yazılamaz (O-6) |
| **SEC-002 veri kapsamı** (`IDataScopeResolver`) | kullanıcının şirket yetkisi (katman 1; D14/1) | çözücü yalnız Platform'da kayıtlı; ortak kütüphanedeki `NoOpDataScopeResolver` boş liste döner (`.antigravity/rules/data-scope-enforcement.md` §6); FinanceTreasury'den tüketim yolu **yok** | **engel (O-14)**: SEC-002 §4 kapısı — kapsam cümlesi + kapsamı soran kod + test olmadan `ready-for-dev` yok. Tüketim yolu servis ortaktır (`IFinanceTreasuryDataScopeSource`); MOD-0126 O-19, MOD-0119 ve MOD-0124 ile **tek karar** |
| MOD-0031 Evidence Linking | düzeltme kanıt kimliği | var (`api/v1/evidence`, MOD-0126 pack) | yalnız bağlantı kimliği doğrulanır (DL-3 başında davranış ölçülür) |
| MOD-0289 Bank Account Registry | tahsilat hesabı referansı | yok (D7, TRM-S3) | yalnız boş olmayan opak Guid; çözümleme Follow-up |
| MOD-0269 / MOD-0121 | mutabakat, banka ekstresi | yok (D5) | kullanılmaz; tahsilat yalnız elle (D16) |
| MOD-0125 Cash Forecasting | projeksiyon tüketicisi | aday, paketi yok (domain-config) | kullanılmaz; FORECAST tüketici yuvası açık (§U) |
| MOD-0018 RBAC | `[HasPermission]`, izin tohumu | var | `finance-treasury.*` (D11/4) altında yeni anahtarlar; `read-tenant-wide` yalnız açık atama (`ExplicitGrantOnlyPermissions`) |
| Servis iskeleti + servis ortak yapı taşları | `Diten.FinanceTreasuryService` | **yok** | **yalnız** MOD-0126 DL-1 açar (makbuz tipi + indeksleri, veri kapsamı kaynağı, ortak LE / para birimi / kod portları, CentralAudit kaydı — gap 4/G5); MOD-0120 DL-1'in önkoşulu |
| Port 5071 | servis portu | D10/3; AGENTS §3 / `ports.md` güncellenmedi (TRM-04) | iskelet port kaydından sonra |
| DCP | dilim kapısı | `draft`; TRM-S2 "karar 0.3 bekliyor" (DCP §8.1) | DCP `approved` olmadan `ready-for-dev` yok |

### Bağımlılık sözleşmeleri — kapanış tablosu

Her satır kapanmadan ilgili dilim başlamaz. **Sahte (mock) istemciyle geçen test canlı entegrasyon kanıtı sayılmaz:**
"sözleşme testi" sahte istemcili testi, "canlı kanıt" gerçek servisle dev ortamında koşulmuş kaydı
(`scripts/run_record.py`) ister.

| Bağımlılık | Sözleşme dosyası / sürüm | Hash | Sahibi | Hata davranışı (MOD-0120) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|---|
| ACCOUNTS-RECEIVABLE (bu modül) | `docs/analysis/contracts/accounts-receivable.openapi.yaml` v1.0.0 — **yazılacak** | TBD (dondurulunca) | finance-treasury | — | sözleşme testi AC-30 · — | DL-1 |
| MOD-0149 müşteri doğrulama + dış referans çözümleme | MOD-0149 sözleşme belgesi — **yazılacak** (O-3) | TBD | commercial-suite | bulunamadı 422 · uygun değil 422 · erişilemez 503 · geçiş yok | sahte müşteri istemcisi · dev ortamında uygun / uygun değil / başka kiracı üç hesap | DL-1 |
| MOD-0220 LE uygunluğu + `ApprovalStatus` | MOD-0220 sözleşme belgesi — **yazılacak** (TRM-14) | TBD | master-data-management | 422 · 503 · geçiş yok | sahte LE · dev ortamında Active+Approved / Approved-değil | DL-1 |
| MOD-0048 para birimi + küsurat | `GET /api/lookups/reference/currencies` + küsurat — **yok** (MOD-0126 O-11) | TBD | platform-shared-services | 503 `RECEIVABLE_CURRENCY_PRECISION_UNAVAILABLE` | sahte · dev ortamında TRY/EUR/JPY | DL-1 |
| RCS-001 ortak kod servisi | **yok** (BL-565, O-6) | — | CT + `.antigravity` onayı | servis yoksa Create yazılamaz | — · — | DL-1 önkoşulu |
| SEC-002 veri kapsamı (D14/1) | Platform `IDataScopeResolver` tüketim yolu — **yok** (O-14; dört paket için tek karar) | TBD | platform-shared-services | çözücü hatası → boş kapsam, boş liste, detay / yazma 404 | kapsam testi AC-6 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-1 |
| MOD-0023 başlatma + görev geçişi | Platform `WorkflowModels.cs` (sözleşme dosyası yok) + AR şablon kodu — **TBD** | Platform commit SHA'sı DL-3 başında | platform-shared-services | belirsiz → 202 Unknown · kesin ret → 502 · zaman aşımı yapılandırılır · yinelenen anahtar hatası → Unknown (ret değil) | sahte MOD-0023 (anahtar başına tek örnek / geçiş) · dev ortamında gerçek başlatma + onay + ret + **aynı anahtarlı yarış yanıtı ölçümü** (var olan örneği / geçişi döndürdüğü doğrulanmadan yinelenen anahtar hatası `Unknown`) | DL-3 |
| MOD-0023 saklama + silmeme politikası (örnek ve geçiş kaydı; gap 6/P1-a) | **KARAR D21/1–2** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/1: `WorkflowInstance` ve `WorkflowTransitionLog` hiç silinmez, TTL yok; silinmişi ayıran okuma istenmez (D21/2) | §17 onayında | platform-shared-services (politika: sahip K1, D21) | §17 onaylanıp AC-W1 geçene kadar 404 / boş okumada anahtarlı yeniden yazma **kapalı**; niyet `Unknown` | ölçüm (@ef4f3a8bc): silme yolu ve TTL yok — ölçüm politika değildir · MOD-0023 AC-W1 (mimari test, sabotajlı) + bu pack'in silinmiş kayıt testi | DL-3 |
| MOD-0023 okuma eşleme alanları (gap 6/P1-b) | **KARAR D21/3** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/3: `by-objects` ve `instances/{id}` → `IdempotencyKey`; `history` → `ApprovalTaskId` + `IdempotencyKey` (yalnız ekleme) | §17 onayında | platform-shared-services | alan kodda yoksa okuma niyeti kapatmaz → `Unknown` | MOD-0023 AC-W2/W3 + bu pack'in eşleme testi | DL-3 |
| MOD-0031 kanıt bağlantısı | `api/v1/evidence` (sözleşme dosyası ölçülecek) | DL-3 başında | platform-shared-services | 503 → talep oluşmaz · AR MOD-0031'e yazmaz, kurtarmada yeniden çağrı yok | sahte · dev ortamı · **MOD-0031 dedupe ölçümü** — AR'ye MOD-0031 yazması ya da kurtarmada yeniden çağrı eklenmeden önce zorunlu (gap 5/G2) | DL-3 |
| CentralAudit | `Diten.BuildingBlocks.CentralAudit` (kod) | Building.Blocks commit SHA'sı DL-1 başında | BL-501 | yazılamazsa komut başarısız (K2) | sabotaj testi AC-18 · AUD-001 §8 dev kanıtı AC-31 | DL-1 |

## Runtime Constraints

- Servis portu **5071** (D10/3; AGENTS §3 kaydı bekliyor). Tarayıcı ve `Diten.Web` yalnız Gateway (5000) üzerinden
  çağırır; tarayıcı JS 5071'e ve 5061'e (CRM) gitmez.
- **Kiracı + şirket izolasyonu her sorguda:** `TenantId` `ITenantContext`'ten, `LegalEntityId` `X-Legal-Entity-Id`
  başlığından (MOD-0220'de doğrulanmış); başka kiracı ya da başka şirket kaydı **404**. Üretimde sabit/varsayılan kiracı yok.
- **Veri kapsamı (SEC-002, sahip kararı D14/1) — kapsam cümlesi:** kapsamı taşıyan alan **`LegalEntityId`**; çevrilen
  kapsam türü yalnız **`LegalEntity`** (`OrgUnit`, `Position`, `ManagerChain` çevrilmez → daraltır, sızdırmaz; birim→şirket
  birleştirmesi uydurulmaz); `IsInclude = false` olan `LegalEntity` kapsamı o şirketi **kapatır**. Kullanıcı yalnız yetkili
  olduğu şirketlerin kayıtlarını görür: istek başlığındaki şirket kullanıcının kapsamında değilse liste **boş** (200, 0
  satır) ve detay / iz / her yazma **404** `RECEIVABLE_INVOICE_NOT_FOUND` döner; çözücü hatası, kiracı ya da kullanıcı
  yokluğu → boş kapsam. Kiracıdaki tüm şirketleri görmek ayrı izindir: `finance-treasury.receivable-invoices.read-tenant-wide`
  (yalnız açık atama — `ExplicitGrantOnlyPermissions`; tek üretici `FinanceTreasuryDataScope.TenantWideScope()`). Kapsam
  servis genelinde **tek yerde** (`IFinanceTreasuryDataScopeSource`, MOD-0126/0119/0124 ile ortak; modül yalnız kendi
  `read-tenant-wide` anahtarını verir) çözülür; okumalar (liste, detay, iz) **okuma kapsamını**, her yazma **yazma
  kapsamını** kullanır (aşağıda). Referans uygulama: `WorkReportScope` / `WorkReportScopeSource` (SEC-002 §5). Kaynaktaki
  `ar_legal_entity_scope` talep (claim) kümesi ve boş kümede 400 davranışı taşınmaz.
- **Okuma kapsamı ile yazma kapsamı ayrıdır (gap 4/G1; D14/1 ile tutarlı — `…read-tenant-wide` *görme* iznidir; tanım
  MOD-0126 pack Runtime Constraints, servis ortak):** okuma kapsamı = kullanıcının kendi şirket yetkileri ya da
  `read-tenant-wide` varsa kiracıdaki tüm şirketler; **yazma kapsamı** = yalnız kendi şirket yetkileri (`TenantWideScope()`
  hiçbir zaman girmez). Her yazma komutu (Create, Update, Delete, BulkDelete, Issue, SetCollectionStage, RequestAdjustment,
  Approve, Reject, Reconcile, RecordPayment) hedef faturanın `LegalEntityId`'si için katman 1'i ister: `X-Legal-Entity-Id`
  hedef faturanın şirketine eşit **ve** kullanıcının o şirkette kendi yetkisi var. Yalnız `read-tenant-wide` sayesinde
  görünen faturaya yazma → **404** `RECEIVABLE_INVOICE_NOT_FOUND` (kapsam dışı kuralıyla aynı; başka adım çalışmaz).
- **Şirket uygunluğu güncel durumdur (TP-001 T14; sahip kararı D14/4):** alacak oluşturan ya da değiştiren **her yazma
  komutunda** — Create, Update, Delete, BulkDelete, Issue, SetCollectionStage, RequestAdjustment, Approve, Reject ve
  RecordPayment — MOD-0220 `lookup-validation` çağrılır; `OperationalStatus = Active` **ve** `ApprovalStatus = Approved`
  değilse 422 `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`; erişilemezse 503 `RECEIVABLE_LEGAL_ENTITY_UNAVAILABLE` — geçiş yok.
  **Reconcile hariç (D15/2):** yeni bir karar ya da alacak değişikliği başlatmaz, uygunluğu tx1'de denetlenmiş komutun uzak
  sonucunu kaydeder (engellemek niyeti açık bırakırdı; MOD-0126 ile aynı). Muafiyet yalnız uzakta **var olan** örneğin /
  geçişin sonucunu kaydetme yolu içindir; okuma örnek / geçiş göstermezse aynı anahtarla yeniden yazma (yalnız MOD-0023
  başlatma / geçiş) yeni uzak işlemdir ve şirket + SoD + izin + asıl aktör yeniden denetlenir (Runtime Constraints 4a,
  gap 4/G2, gap 5/G2). Okumalar uygunluk istemez (pasifleşmiş şirketin
  geçmiş alacağı okunabilir; kapsam kuralı yine geçerli).
- **Müşteri uygunluğu güncel durumdur:** Create, Update ve Issue'da `ICustomerReferencePort` MOD-0149 sözleşmesini çağırır:
  yok / başka kiracı → 422 `RECEIVABLE_CUSTOMER_NOT_FOUND`; durumu faturalanabilir değil → 422 `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE`
  (faturalanabilir durum kodları O-4); erişilemez → 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE`, geçiş yok. Açılmış faturanın
  tahsilatı ve düzeltmesi müşteri durumundan bağımsızdır (alacak, müşteri pasifleşince ortadan kalkmaz). Anlık görüntü
  uygunluk yerine geçmez (paket MOD-0149 §G).
- Soft delete (`IsDeleted` + `DeletedAt` UTC, filtrede Id + TenantId); yalnız `Draft`. Okumalar `IsDeleted = false`.
- **Idempotency (D12/2 — her yazma komutunda zorunlu; MOD-0126 ile aynı kural):** Create, Update, Delete, BulkDelete,
  Issue, SetCollectionStage, RequestAdjustment, Approve, Reject, Reconcile ve RecordPayment `Idempotency-Key` başlığı
  **ister**; yoksa 400 `RECEIVABLE_IDEMPOTENCY_KEY_REQUIRED`. Sunucu anahtar **üretmez**.
  - **Makbuz kapsamı:** `(TenantId, LegalEntityId, ActorId, Operation, TargetId, Key)`; tekil indeks bu altılıdadır.
  - **Yaşam döngüsü:** tek aşamalı komutlarda tx1'de `Completed` + yanıt; iki aşamalı komutlarda (RequestAdjustment,
    Approve, Reject) tx1'de `Pending` (`LinkedIntentId` dolu), yanıt tx2'de ya da — makbuz hâlâ `Pending` ise —
    Reconcile'da yazılır. `Completed` makbuz değişmez.
  - **Kontrol sırası (her komutta aynı):** (1) kimlik + `[HasPermission]` → (2) veri kapsamı (SEC-002; okumada okuma
    kapsamı, **yazmada yazma kapsamı** — hedef faturanın şirketi kullanıcının kendi şirket yetkisinde değilse, fatura
    `read-tenant-wide` ile görünse bile → 404 `RECEIVABLE_INVOICE_NOT_FOUND`)
    → (3) makbuz araması: aynı kapsam + farklı parmak izi → 409 `RECEIVABLE_IDEMPOTENCY_CONFLICT`; aynı + `Completed` →
    **kayıtlı yanıt aynen döner**; aynı + `Pending` → 202 `RECEIVABLE_REQUEST_IN_PROGRESS`. İki durumda da **başka hiçbir
    adım çalışmaz** — uzak çağrı yok, Reconcile yok. → (4) açık talep / açık niyet kontrolü (RecordPayment'ta ayrıca iş
    anahtarı mükerrer kontrolü — D16; `Version`'dan önce, böylece mükerrer tahsilat bayat `If-Match` ile gelse de 409
    `RECEIVABLE_PAYMENT_DUPLICATE` alır) → (5) durum ve `Version` CAS → (6) iş kuralları, LE + müşteri uygunluğu, SoD →
    (7) tx1 (iş verisi + makbuz + denetim, tek işlem).
  - **Kurtarma yalnız `ReconcileReceivableInvoiceAdjustmentCommand` ile** (kendi anahtarı ve makbuzuyla); aynı anahtarla
    tekrar asla kurtarma tetiklemez.
  - **Çökme:** tx1 sonrası çökmede makbuz `Pending` kalır; aynı anahtarla tekrar 202; Reconcile niyeti kapattığında bağlı
    makbuz **yalnız `Pending` ise** `Completed` yapılır. tx1 commit olmadıysa hiçbir şey yazılmamıştır; aynı anahtarla
    tekrar baştan çalışır.
- **Eşzamanlılık:** Update, Issue, SetCollectionStage, RecordPayment ve kararlar `If-Match` (Version) + durum CAS; bayat
  istek 409 `RECEIVABLE_INVOICE_VERSION_CONFLICT`.
- **Tek belge yazımı:** fatura, satırları, taksitleri ve aşama geçmişi tek Mongo belgesidir; Create / Update / Issue /
  SetCollectionStage / RecordPayment iş verisi tek belge + makbuz + denetim satırını **tek işlemde** yazar. Kaynaktaki
  "çoklu yazma kısmi kalıcılık" sınıfı (paket §L.3) bu komutlarda yapısal olarak oluşmaz.
- **Yerel kayıt önce, uzak çağrı sonra (domain-config; TP-001 T16) — düzeltme akışı:**
  1. *RequestAdjustment:* ön kontroller (tür ön koşulu, açık talep yok, politika okunur) → tx1 — `ReceivableAdjustmentRequest(Submitted)`,
     `ReceivableAdjustmentStartIntent(Pending)`, fatura `OpenAdjustmentRequestId` + (WriteOff'ta) `CollectionStage =
     WriteOffProposed` + `PreviousCollectionStage`, makbuz `Pending`, deneme olayı. Sonra MOD-0023 başlatma, niyetteki uzak
     anahtarla. Sonra tx2: kesin başarı → niyet `Started`, talep `PendingApproval`; kesin ret (400/403/404/409 — MOD-0023'ün
     "başlatma tamamlanmadı" 409'u, 409 `WORKFLOW_TRANSITION_CONFLICT` ve 503 `WORKFLOW_TRANSITION_CONFLICT` / `WORKFLOW_TRANSACTION_UNAVAILABLE` hariç — bunlar belirsizdir, D21/7, D25) → niyet `Refused`, talep `StartRefused`, fatura aşaması geri, `OpenAdjustmentRequestId
     = null`, 502; **belirsiz** (zaman aşımı, bağlantı kopması, 5xx, boş ya da okunamayan 2xx) → niyet `Unknown`, talep
     `Submitted` kalır, 202 `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN`. Belirsiz sonuç ret olarak kaydedilmez.
  2. *Açık talep varken* aynı faturaya yeni talep → 409 `RECEIVABLE_ADJUSTMENT_PENDING`, uzak çağrı yok; tahsilat da 409.
     Tekil kısmi indeks `ux_receivable_adjustment_open` + fatura `Version` CAS ile atomik.
  3. *Kararlar (Approve / Reject):* SoD ön kontrolü (A1–A7) → tx1 `ReceivableAdjustmentDecision(Requested)` + deneme olayı
     → MOD-0023 görev geçişi, `RemoteIdempotencyKey` ile → tx2: `Recorded` + talep `Approved` / `Rejected` + **etki aynı
     işlemde**: Approve-Void → fatura `Status = Void`, `OutstandingAmount = 0`, taksit kalanları 0, `ExternalInvoiceNumberKey
     = null`; Approve-WriteOff → `CollectionStage = WrittenOff`, `WrittenOffAmount = Amount`, `OutstandingAmount = 0`;
     **Approve-PaymentReversal (D16)** → yeni `ReceivablePayment(EntryType = Reversal, Amount = geri alınan tutar,
     ReversesPaymentId, AdjustmentRequestId)` **eklenir** (karşı kayıt, ekleme-yalnız), orijinal Receipt'e bir kez
     `IsReversed = true` + `ReversedByPaymentId` + `ReversedAt`, faturada `PaidAmount −= Amount`, `OutstandingAmount +=
     Amount` (kalan tutar geri gelir), taksit kalanları orijinalin `InstallmentAllocations`'ından aynen geri yazılır,
     `Status` yeniden hesaplanır (`PaidAmount = 0` → `Open`, aksi `PartiallyPaid`), referans serbest kalır (politika,
     Entity Fields); Reject → aşama `PreviousCollectionStage`'e döner (PaymentReversal'da aşama değişmemişti, tahsilat
     değişmez); her durumda `OpenAdjustmentRequestId = null`. Etki uygulanırken
     faturanın talep anındaki `Version`'ı ile CAS yapılır (talep açıkken fatura kilitlidir). Belirsiz → `Unknown`, 202
     `RECEIVABLE_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır. **Talep başına tek açık karar niyeti**
     (`ux_receivable_adjustment_decision_open`); açıkken başka anahtar/aktörden karar uzak çağrısız 409 `RECEIVABLE_DECISION_PENDING`.
  4. *İki aşamalı denetim ve çökme kurtarma (MOD-0126 Runtime 4a ile aynı):* tx1 **deneme olayı**, tx2 **sonuç olayı**,
     ikisi de iş verisiyle aynı işlemde (`RequireEnlistment = true`). Uzak başarı + tx2 başarısız ya da çökme → niyet açık
     kalır; Reconcile MOD-0023 örneği/görevi `instances/by-objects` ve görev geçmişinden okur (yalnız niyetin kendi eşleme
     alanlarıyla — 4a, gap 6/P1-b), uzak çağrıyı **tekrarlamadan** tx2'yi (etki dahil) yazar, sonuç olayını üretir ve bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır. K2
     kararlarında deneme olayı yazılamazsa uzak çağrı yapılmaz; sonuç olayı yazılamazsa etki uygulanmaz, niyet açık kalır.
  4a. *Reconcile önce okur; uzak yazmayı yalnız anahtarla idempotent olduğu ölçülmüş yerde yeniden yapar (gap 4/G2,
     gap 5/G2; RequestAdjustment, Approve, Reject; kural MOD-0126 pack Runtime Constraints 3a ile aynı):*
     - **Önce okuma:** başlatmada `instances/by-objects` (düzeltme talebi kimliği) + niyetin deterministik uzak anahtarı
       (`mod0120:{adjustmentId:N}:start`); kararda görev geçmişi + `RemoteIdempotencyKey`.
     - **Uzakta niyetin kendisine ait örnek / geçiş var** (eşleme aşağıda, gap 6/P1-b) → kesin durum kaydedilir (tx2, etki
       dahil + bağlı `Pending` makbuz `Completed` + sonuç olayı); şirket kuralından **muaf** (D15/2).
     - **"Kesin boş" okumadan çıkarılmaz (gap 5/G2):** MOD-0023 sözleşmesinde "bulunamadı / boş okuma son durumdur"
       güvencesi yoktur (ilk çağrı hâlâ yolda olabilir); okumaya dayalı "kesin boş → yeniden başlat" dalı yoktur. Okuma
       belirsiz ya da "bulunamadı" ise (zaman aşımı, bağlantı kopması, 5xx, okunamayan gövde, 404 / boş sonuç) niyet
       **asla `Cancelled` yapılmaz**, `Unknown` kalır (`Pending` idiyse `Unknown`'a geçer); talep, fatura kilidi ve makbuz
       değişmez. Zaman aşımı / 5xx / okunamayan gövdede başka adım yoktur; 404 / boş sonuçta tek ek adım aşağıdaki
       anahtarlı yeniden yazmadır.
     - **Yeniden çağrı yalnız MOD-0023 başlatma ve görev geçişlerinde** — uzak yazmanın kendisi anahtarla idempotent
       (ölçüldü, Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c): `ux_workflow_instances_tenant_idempotency_key_active` (TenantId +
       IdempotencyKey, kısmi `IsDeleted = false`) ve `ux_workflow_transition_logs_task_action_idempotency_active`
       (TenantId + ApprovalTaskId + Action + IdempotencyKey) tekil indeksleri
       (`PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131`, `:211-225`); başlatma var olan örneği döndürür
       (`StartWorkflowInstanceHandler.cs:76-85`), geçişler anahtarı zorunlu tutar (`ApproveWorkflowTaskValidator.cs:13`) ve
       `GetByTaskActionIdempotencyKeyAsync` ile yeniden oynatır (`RejectWorkflowTaskHandler.cs:79-80`,
       `WorkflowTerminalTransitionWriter.cs:402-407`). Yeniden yazmadan önce (i) şirket uygunluğu (D10/1, D14/4), (ii) SoD
       (başlatmada A1–A4 ve A7 aday çözümleyicisi yeniden; kararda niyetteki karar verenle A1–A7), (iii) izin ve (iv) aktör:
       Reconcile'ı çağıran asıl komutun aktörü (`RequestedBy` / `DecidedBy`) değilse uzak yazma çağrısı yok (409
       `RECEIVABLE_RECONCILE_RESTART_ACTOR_MISMATCH`). Hepsi geçerse aynı uzak yazma **aynı deterministik uzak anahtarla
       bir kez** yapılır; en fazla bir örnek / geçişi MOD-0023'ün tekil indeksi garanti eder. Başarı (yeni ya da yeniden
       oynatılmış) → niyet sonuçla kapanır (tx2 eşlemesi, etki dahil); kesin ret (tx2 eşlemesi; yinelenen anahtar / tekil
       indeks çakışması ret sayılmaz) → `Refused`; kesin ret olmayan her başarısızlık (yinelenen anahtar yarışı yanıtı, 5xx,
       zaman aşımı, okunamayan gövde) → `Unknown` kalır.
     - **Güvencenin sınırı — silinen ya da temizlenen uzak kayıt (gap 6/P1-a; kural ve ölçüm MOD-0126 pack Runtime
       Constraints 3a ile aynı):** iki tekil indeks de kısmidir (`IsDeleted = false`); silinmiş ya da temizlenmiş bir
       örnek / geçiş aynı anahtarla yenisinin oluşmasına izin verir. Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c):
       `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs` içindeki
       tek `IsDeleted = true` yazımı `SlaEscalationRule` içindir (`:961-974`, `DeactivateRulesForTemplateAsync`);
       `WorkflowInstance` / `WorkflowTransitionLog` için soft delete ya da temizleme yolu bulunmadı;
       `PlatformSchemaManifest.WorkflowWorkCenter.cs` (sha256
       `666b44dcbca19df1f1ebf4f176c9fa86bf66111cc1b935286b625bc1d4706f10`) 0 `ExpireAfter` (TTL yok); `instances/by-objects`
       silinmişleri süzer (`GetWorkflowInstancesByObjectsHandler.cs:51`). Güvence **yalnız örnek ve geçiş kayıtları
       silinmediği sürece** geçerlidir. Okuma silinmiş kayıt gösterirse **ya da** silinmemiş kayıt yokken niyetin ilk
       uzak denemesi kayıtlıysa ve okuma silinmiş durumu ayırt edemiyorsa (tx1'i commit olmuş her niyet), Reconcile aynı anahtarla yeniden yazmaz; niyet `Unknown`
       kalır (202 `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN` / `RECEIVABLE_DECISION_OUTCOME_UNKNOWN`), talep, fatura
       kilidi, tahsilatlar ve makbuz değişmez. 404 / boş okumadaki anahtarlı yeniden yazma, MOD-0023 saklama + silmeme
       politikası (en az niyet ömrü boyunca; sahibi platform-shared-services) ve silinmiş durumu ayırt eden okuma kapanana
       kadar **kapalıdır** (kapanış tablosu, Ready-for-dev engeli).
     - **Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b):** `instances/by-objects` bir nesnenin bütün silinmemiş
       örneklerini döndürür (`GetWorkflowInstancesByObjectsHandler.cs:50-69`). Okuma sonucu niyete yalnız **hepsi**
       eşleşirse bağlanır: kiracı; nesne (düzeltme talebi) kimliği — AR'de her talep kendi nesnesidir ve tek başlatma
       taşır, gönderim sırası talep kimliğindedir; kararda karar kimliği (`ReceivableAdjustmentDecision.Id`); deterministik
       uzak anahtar (`mod0120:{adjustmentId:N}:start` / `RemoteIdempotencyKey`); geçişte ek olarak görev kimliği
       (`WorkflowTaskId`) + eylem (approve / reject). Başka bir talebin ya da kararın sonucu (aynı faturanın önceki talebi,
       aynı görevde başka kararın geçişi ya da iptali) bu niyeti **asla** kapatmaz; yalnız fatura ya da talep kimliğiyle
       veya "en yeni örnek" sırasıyla eşleme yasaktır. Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c): `WorkflowInstanceStatusDto`
       (`WorkflowModels.cs:200-207`) ve `WorkflowInstanceHistoryEntryDto` (`:451-465`) uzak anahtarı taşımaz, geçmiş satırı
       görev kimliğini de taşımaz; alan yanıtta yoksa okuma niyeti kapatamaz → `Unknown`. Kapanış yolu MOD-0126 pack 3a ile
       aynı (MOD-0023'e eklemeli okuma alanı, D9; ya da P1-a koşulları kapalıysa aynı anahtarla yeniden yazmanın yanıtı).
       Düzeltmenin etkisi (Void, WriteOff, tahsilat geri alma) yalnız o talebin kendi niyetine bağlanmış sonuçla yazılır.
     - **D21 bağı — yeniden yazma kapısının yeni hâli (sahip kararı D21, MOD-0023 pack §17 (TRM-15)):** yukarıdaki kapının iki
       koşulundan saklama politikası **KARAR D21/1** ile verildi (hiç silinmez, TTL yok) ve MOD-0023 AC-W1 (mimari test) geçince
       kapanır; silinmişi ayıran okuma koşulu **D21/2 ile kaldırıldı** — politika + AC-W1 onun yerine geçer. §17
       onaylanıp AC-W1 geçince (ve AC-W5…W14 geçmişse, D22/2) 404 / boş okumada aynı deterministik anahtarla yeniden yazma açılır; yalnız asıl
       aktör (**KARAR D21/5**) ve şirket / SoD / izin yeniden kontrolüyle. Silinmiş kayıt yine de görülürse yeniden
       yazma yok, niyet `Unknown`. Eşleme alanları **KARAR D21/3** (§17.3/3) kodda olana kadar okuma niyeti kapatmaz.
     - **Aynı anahtarlı yarış yanıtı (KARAR D21/4, §17.3/4; DL-3 başı kanıt maddesi):** politika — eşzamanlı iki
       istekte son yanıtlar aynı kayıttır ve tek satır oluşur; ara 503 "aynı anahtarla tekrar" olabilir (→ `Unknown`,
       tekrar), 409 yalnız örnek Active'den çıktıysa. Kod okuması (@ef4f3a8bc) bu deseni gösteriyor
       (`StartWorkflowInstanceHandler.cs:300-330, 700-716`, `WorkflowTerminalTransitionWriter.cs:357-…`) ama eşzamanlı
       aynı anahtar testi **ölçülmedi**; MOD-0023 AC-W5…W14 geçene kadar yinelenen anahtar hatası **`Unknown`** sayılır,
       **asla `Refused`** değildir. `Refused` yalnız tx2 tablosundaki kesin iş reddi kodlarıdır (**KARAR D21/7**).
       Hedef yanıtlar MOD-0023 pack §17.3/4 tablosundadır (D22/3): bütünleşmiş başlatmanın ya da kaydedilmiş geçişin
       tekrarı, örnek sonradan kapansa da **200** ve aynı kayıt — yeni ret sayılmaz; hiç bütünleşmemiş ve kurtarmanın
       kapattığı başlatma 409 `WORKFLOW_INSTANCE_START_CONFLICT` → yalnız kurtarmanın kapanış kaydıyla `Cancelled`,
       yoksa `Unknown`; aynı göreve farklı karar çatışmasında kaybeden 409 `WORKFLOW_TASK_INVALID_STATE` → kaybeden
       niyet tx2 eşlemesiyle `Refused` (kendi kararı kaydedilmedi). Kullanılan uzak eylemler: §17.4a matrisi. AC-W5…W14
       bugünkü kodla geçmezse §17 bloke kalır ve yeniden yazma kapısı kapalı kalır (D22/2).
       **MOD-0023 üretici cevabı ve D25 (§17.7):** eşzamanlı aynı anahtarlı geçişte kaybeden 409
       `WORKFLOW_TRANSITION_CONFLICT` alabilir → `Unknown` (kesin ret değil), aynı anahtarla tekrar ya da okuma;
       bütünleşmiş başlatmada farklı olgu bugün 200 döner (üretici düzeltecek) — Treasury anahtarı niyet başına
       deterministiktir, farklı niyette asla tekrar kullanılmaz. Reconcile `includeDeleted=true` ile okur (D25/2);
       §17 kodu MOD-0023 sahibinin WP'siyle gelir (D25/1), bu WP main'e girmeden MOD-0023 kullanan dilim kabul edilmez.
     - **MOD-0031 yeniden çağrılmaz:** AR MOD-0031'e yazmaz (yalnız bağlantı kimliğini doğrular); hiçbir kurtarma yolu
       MOD-0031'e yazma çağrısı yapmaz. İleride bir yazma eklenirse önce MOD-0031 dedupe ölçümü gerekir (Dependencies
       kapanış tablosu).
     - **`Cancelled` yalnız kesin uzak iptal / kapanış kaydıyla** (o örnek için MOD-0023 iptal geçiş kaydı ya da
       `CloseAbandonedWorkflowStartHandler.cs` kapanışı, anahtar `workflow-start-recovery-close:{instanceId:N}`, `:230`).
     - **Uygun değilse:** uzak yazma/başlatma çağrısı yok; niyet `Unknown` kalır, `LastOutcomeCode` sebep kodunu taşır
       (`RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`, `RECEIVABLE_SOD_DENIED`, izin reddi, `RECEIVABLE_RECONCILE_RESTART_ACTOR_MISMATCH`);
       talep, fatura (kilit ve aşama) ve bağlı `Pending` makbuz değişmez; sonuç olayı yazılmaz; Reconcile'ın kendi makbuzu
       sebep koduyla `Completed` olur. Koşul düzelince yeni Reconcile tekrar dener; kalıcı kapatma Follow-up.
  5. HTTP istemcilerinde **açık zaman aşımı**; değer `FinanceTreasury:Workflow:TimeoutSeconds` (MOD-0126 ile ortak ayar).
- **Düzeltme politikası (O-7 önerisi — MOD-0126 D11/1 deseni):** kiracı ayarı, para birimli eşik + politika sürümü; ayar
  yoksa her düzeltme tier-2 sayılır (`PolicySource = DefaultAllTier2`) — hata değildir; talep para birimi eşik para
  biriminden farklıysa tier-2; ayar deposu erişilemiyorsa talep ve kararlar 503 `RECEIVABLE_ADJUSTMENT_POLICY_UNAVAILABLE`,
  uzak çağrı yok. Çözümleyici ve karar kontrolü **aynı**, talebe sabitlenmiş değeri kullanır. Kodda varsayılan tutar yok.
- **Tahsilat (sahip kararı D16 — v1'de, DL-3) tek aşamalıdır:** uzak çağrı yoktur; doğrulama, iş anahtarı mükerrer
  kontrolü, `ReceivablePayment(EntryType = Receipt)`, fatura (Paid/PartiallyPaid, taksit kalanları en eskiden,
  `InstallmentAllocations` yazılır), makbuz (`Completed`, 201) ve tek `…payment-recorded` olayı **aynı işlemde**. Niyet,
  belirsiz sonuç ve Reconcile yoktur; çökme → geri alınır, aynı anahtarla tekrar baştan çalışır.
  - **Mükerrer tahsilat (D16, gap 6/P2-a):** iş anahtarı `(TenantId, LegalEntityId, CustomerId, PaymentReferenceKey)`
    geri alınmamış Receipt'ler arasında tekildir (`ux_receivable_payment_reference_active`). Makbuz kapsamı aktörü ve
    anahtarı içerdiği için iki kullanıcının (ya da aynı kullanıcının iki anahtarla) aynı ödemeyi girmesini makbuz
    yakalamaz; iş anahtarı yakalar: ön kontrol (kontrol sırası 4) var olan kaydı görürse 409 `RECEIVABLE_PAYMENT_DUPLICATE`;
    eşzamanlı iki istekte tekil indeks ikinciyi engeller ve handler yazma çakışmasında (tekil indeks ya da fatura `Version`
    CAS) iş anahtarını **yeniden okur** — aynı anahtarlı geri alınmamış tahsilat varsa 409 `RECEIVABLE_PAYMENT_DUPLICATE`
    döner (sürüm çakışması ya da 500 değil); yoksa 409 `RECEIVABLE_INVOICE_VERSION_CONFLICT`. Hiçbir durumda ikinci
    tahsilat, ikinci denetim olayı ya da ikinci `Completed` makbuz başarı yanıtı oluşmaz.
- **Onaylı tahsilat geri alma (D16, gap 6/P2-b) — `AdjustmentType = PaymentReversal`:** yeni komut yoktur; Void /
  WriteOff ile **aynı** iki aşamalı akış kullanılır (RequestAdjustment → MOD-0023 başlatma → Approve / Reject → gerekirse
  Reconcile), aynı yerel niyet, idempotency, belirsiz sonuç ve kurtarma kuralları, aynı SoD (A1–A7) ve K2 (D14/3:
  tahsilatı geri almak parayı etkileyen karardır — talep, onay ve ret fail-closed, denetim iş verisiyle aynı işlemde).
  Talep tx1'i faturayı kilitler (`OpenAdjustmentRequestId`; açıkken yeni tahsilat, başka düzeltme ve ikinci geri alma 409
  `RECEIVABLE_ADJUSTMENT_PENDING`) ama tutarları **değiştirmez**; etki yalnız onayın tx2'sinde (ya da onu kaydeden
  Reconcile'da) karar sonucuyla aynı işlemde yazılır: karşı kayıt (`EntryType = Reversal`) eklenir, orijinal tahsilatın
  tutarı değişmez, faturanın kalan tutarı geri gelir (Runtime Constraints → düzeltme akışı 3). Kısmi geri alma yoktur
  (tam tutar). Geri alma geri alınamaz; yanlış geri alma yeni bir Receipt ile düzeltilir.
- `GlobalEntity` kullanılmaz; bütün kayıtlar kiracıya aittir. Mongo GUID V3 / subtype 4; `decimal` → `Decimal128`.
- Arka plan işi yok (yaşlandırma okuma anında türetilir; uzlaştırma süpürücüsü servis kimliği gelene kadar yok).

## Layout & Shell Contract

- `shell: tenant` → `Views/FinanceTreasury/ReceivableInvoices/` altındaki **tüm** `.cshtml` dosyalarında
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml` varsayılanı değiştirilmez. Kaynak ekrandaki
  `Layout = "_LayoutBackbone"` (`frontend/Diten.Web/Views/ERP/Finance/AccountsReceivable/Index.cshtml:3@218acaf5`, paket
  §I) taşınmaz.
- View klasörü: `Views/FinanceTreasury/ReceivableInvoices/` · Frontend rotası: `/FinanceTreasury/ReceivableInvoices`.
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `finance-treasury.receivable-invoices.read` yoksa kabuk içinde
  `_AccessDenied` partial'ı; başlık, kart, boş tablo, buton çizilmez; yönlendirme yok; 401 tam sayfa. Kaynak ekrandaki
  "simulated role selector" (8 rol, `Index.cshtml:51-60@218acaf5`) ve "Mock action / pending backend" diyaloğu taşınmaz.

## Backend File Convention

Golden Reference **Compact** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
okundu: `Commands/`, `Queries/` + `…ListSort.cs`, `Handlers/{CommandHandlers,QueryHandlers}/`, `Validators/`,
`…Models.cs`): `Features/ReceivableInvoice/` altında

```text
Commands/          CreateReceivableInvoiceCommand.cs, UpdateReceivableInvoiceCommand.cs,
                   DeleteReceivableInvoiceCommand.cs, BulkDeleteReceivableInvoiceCommand.cs,
                   IssueReceivableInvoiceCommand.cs, SetReceivableInvoiceCollectionStageCommand.cs,
                   RequestReceivableInvoiceAdjustmentCommand.cs, ApproveReceivableInvoiceAdjustmentCommand.cs,
                   RejectReceivableInvoiceAdjustmentCommand.cs, ReconcileReceivableInvoiceAdjustmentCommand.cs,
                   RecordReceivableInvoicePaymentCommand.cs (D16; geri alma ayrı komut değil — PaymentReversal düzeltme türü)
Queries/           GetReceivableInvoiceListQuery.cs, GetReceivableInvoiceByIdQuery.cs,
                   GetReceivableInvoiceTraceQuery.cs, ReceivableInvoiceListSort.cs
Handlers/CommandHandlers/   {Verb}ReceivableInvoice{…}Handler.cs (her komut için; suffix YOK) —
                            ör. CreateReceivableInvoiceHandler.cs, RequestReceivableInvoiceAdjustmentHandler.cs
Handlers/QueryHandlers/     GetReceivableInvoiceListHandler.cs, GetReceivableInvoiceByIdHandler.cs,
                            GetReceivableInvoiceTraceHandler.cs
Validators/        CreateReceivableInvoiceValidator.cs, UpdateReceivableInvoiceValidator.cs,
                   GetReceivableInvoiceListValidator.cs, SetReceivableInvoiceCollectionStageValidator.cs,
                   RequestReceivableInvoiceAdjustmentValidator.cs, ReceivableAdjustmentDecisionValidator.cs,
                   RecordReceivableInvoicePaymentValidator.cs
ReceivableInvoiceModels.cs  (tüm DTO'lar tek dosyada)
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` `{Verb}{Module}Handler` — **Command/Query
  suffix YOK**. Tek dosyada tek public tip. Kaynaktaki toplu servis sınıfları (`AccountsReceivableV041Services.cs`,
  `AccountsReceivableImportService.cs`, `AccountsReceivableFoundationService.cs`) taşınmaz; iş kuralı handler + domain'de
  (`handler-design`).
- Controller `CustomBaseController`'dan türer, `Response<T>` döner; **her aksiyonda `[HasPermission]`** (kaynakta yok —
  `[Authorize]` + `LocalAccountsReceivableAuthorizationGuard`, `AccountsReceivableController.cs:16, 963@218acaf5`).
- Her yazma komutu `ICentralAuditDeclaration` taşır (`CentralAuditEntityId` açık yazılır). MOD-0023'e dokunan komutlar
  önce yerel niyet yazdığı için `ICentralAuditOutsideTransaction` kullanılmaz.
- Domain: `ReceivableInvoiceStateRules` (durum + aşama geçiş tabloları tek yerde), `ReceivableAdjustmentSodRules` (A1–A7).

## Frontend File Contract

Golden Reference **Compact** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/` okundu: Index,
Create, Edit, Details, `_Form`, `_Filter`, `_DataTable`, `_IndexL10n`, marker):

```text
Views/FinanceTreasury/ReceivableInvoices/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml             (sayfa kabuğu + _Form)
├── Edit.cshtml               (sayfa kabuğu + _Form; yalnız Draft)
├── Details.cshtml            (sekme: Satırlar · Taksitler · Tahsilat aşaması · Düzeltmeler ve kararlar · Tahsilatlar · İz)
├── _Form.cshtml              (başlık alanları + satır ızgarası + taksit ızgarası)
├── _Filter.cshtml            (durum, tahsilat aşaması, yaşlandırma, müşteri, vade aralığı, para birimi)
├── _DataTable.cshtml         (data-dt-standard="v2", data-dt-data-mode="server", iskelet yükleyici)
├── _IndexL10n.cshtml
└── ReceivableInvoicesIndex.cs (marker)
wwwroot/assets/js/FinanceTreasury/ReceivableInvoices/  index.js · index.l10n.js · form.js · details.js
Resources/Views/FinanceTreasury/ReceivableInvoices/    ReceivableInvoicesIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
  Kaynaktaki sürgü pencereler (Receivable Detail, Evidence/Exception Detail, Advanced Detail drawer — paket §I) ve altı
  ekranlı "workspace" (AR Home, Collections Workbench, Cash Forecast Input, Evidence & Exceptions, Admin/Advanced) taşınmaz;
  bu sürümde tek liste ekranı vardır.
- DataTable sütunları: Fatura kodu, Müşteri, Dış fatura no, Fatura tarihi, Vade, Para birimi, Brüt tutar, Kalan tutar
  (para birimine göre biçim), Durum, Tahsilat aşaması, Gecikme (gün), İşlemler. (Kaynak tablo `dt-mod0120-receivables`
  sütunlarından "Expected Collection", "Risk", "Owner", "Next Action" alınmaz — kaynakta veri karşılığı ölçülmedi, paket §N.9.)
- Durum ve aşama rozetleri `ReceivableInvoiceStatus`'un 5 ve `ReceivableCollectionStage`'in 7 değerinin tamamını gösterir;
  yaşlandırma rozeti 3 değer.
- Eylem düğmeleri (Aç, Tahsilat aşaması, İptal talebi, Silme talebi, Onayla, Reddet, Tahsilat kaydet, Tahsilatı geri al
  — talep, D16) yalnız Details'ta ve
  sunucunun verdiği `allowedActions` listesine göre çizilir; SoD nedeniyle karar veremeyen kullanıcıya düğme yerine
  yerelleştirilmiş açıklama çizilir — sunucu kontrolünün yerine geçmez.
- Müşteri seçici MOD-0149 ucunu **same-origin MVC proxy** üzerinden çağırır (O-3'ün sonucu: lookup ucu ya da
  `GET /api/crm/accounts?search=`); sabit liste yok. Tahsilat hesabı alanı MOD-0289 arama ucu gelene kadar opak referans
  girişi (MOD-0126 O-15 ile aynı).
- API profili: same-origin MVC proxy (`/FinanceTreasury/ReceivableInvoices/api/...`); proxy `AuthTokenCookies.GetAccessToken`
  ile token'ı ve oturumun çözümlenmiş `X-Tenant-Id` / `X-Legal-Entity-Id` başlıklarını ekler, `Idempotency-Key`'i istemci
  JS üretip başlıkta gönderir (sunucu üretmez); çözümlenemezse istek oluşturulmaz. Tarayıcı JS `access_token` /
  `document.cookie` okumaz (kaynak `api.js` cookie'den token okuyor — paket §I; taşınmaz).
- Uyarı/onay diyalogları SweetAlert2 premium standardı (MOD-0013); hardcoded metin yok (kaynak `Index.cshtml`'de 0
  `Localizer`, paket §I).
- Para birimi seçimi `GET /api/lookups/reference/currencies`'ten.

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| X-Legal-Entity-Id | Evet | Guid; kiracıya ait; Active + Approved (her yazmada, Reconcile'ın kayıt yolu hariç — D14/4, D15/2); okumada okuma kapsamında (kendi şirket yetkisi ya da `read-tenant-wide`, D14/1); **yazmada** hedef faturanın şirketine eşit ve kullanıcının **kendi** şirket yetkisinde (yazma kapsamı — `read-tenant-wide` yazma yetkisi vermez, gap 4/G1); değilse 404 | — | `ILegalEntityEligibilityPort`, `IFinanceTreasuryDataScopeSource` (okuma / yazma kapsamı) |
| Gövde/sorguda tenantId, legalEntityId | — | bulunursa 400 | — | middleware |
| CustomerId | Evet | Guid ≠ Empty; MOD-0149'da var, aynı kiracı, faturalanabilir durum (O-4) | — | `ICustomerReferencePort` |
| ExternalInvoiceNumber | Hayır | trim, ≤ 64, ASCII yazdırılabilir; şirket içinde tekil (O-5) | Unique kısmi (TenantId, LegalEntityId, ExternalInvoiceNumberKey) | — |
| InvoiceDate | Evet | `DateOnly`, default değil | — | — |
| DueDate | Evet | ≥ InvoiceDate; taksit varsa son taksit vadesine eşit | — | — |
| ExpectedCollectionDate | Hayır | ≥ InvoiceDate | — | — |
| CurrencyCode | Evet | `^[A-Z]{3}$` (upper normalize), MOD-0048 listesinde | — | `ICurrencyReferencePort` |
| CollectionDestinationRef | Hayır | Guid ≠ Empty ise kabul | — | — |
| Notes | Hayır | trim, ≤ 2000 | — | — |
| Lines | Evet | 1..200 satır | — | — |
| Line.Description | Evet | trim, 1..256 | — | — |
| Line.NetAmount | Evet | > 0; küsurat ≤ hane | — | `ICurrencyReferencePort` (hane) |
| Line.TaxAmount | Evet | ≥ 0; küsurat ≤ hane | — | aynı |
| Installments | Hayır | 0..24; vadeler artan ve tekrarsız; her vade ≥ InvoiceDate; tutar > 0, küsurat ≤ hane; toplam = GrossTotal | — | — |
| InvoiceCode | — | istekte bulunursa 400 (OTOMATİK) | Unique (TenantId, InvoiceCode) | `IRecordCodePort` |
| Sunucu alanları (toplamlar, Status, CollectionStage, anlık görüntüler) | — | istekte bulunursa 400 | — | — |
| Idempotency-Key | **Evet** (her yazma komutu, D12/2) | 1..128 yazdırılabilir ASCII; yoksa 400 `RECEIVABLE_IDEMPOTENCY_KEY_REQUIRED` | Unique altılı kapsam | makbuz araması |
| If-Match | Evet (Update, Issue, SetCollectionStage, RecordPayment, kararlar) | `Version` | — | CAS |
| CollectionStage hedefi | Evet | aşama tablosunda izinli; `PromiseToPay` → `PromiseToPayDate` zorunlu (≥ bugün, O-12); `Disputed` → `ReasonCode` zorunlu | — | — |
| CollectionStage ReasonCode | Koşullu | trim, ≤ 64 | — | — |
| Düzeltme AdjustmentType | Evet | `Void` \| `WriteOff` \| `PaymentReversal` (D16); tür ön koşulu (Entity Fields) | — | — |
| Düzeltme PaymentId (geri alınacak tahsilat) | PaymentReversal için Evet; diğer türlerde yasak (400) | Guid ≠ Empty; bu faturanın `EntryType = Receipt`, `IsReversed = false` tahsilatı (yoksa 404 `RECEIVABLE_PAYMENT_NOT_FOUND`; geri alınmışsa ya da fatura `WrittenOff` / `Void` ise 409 `RECEIVABLE_ADJUSTMENT_NOT_ALLOWED`) | Unique (TenantId, ReversesPaymentId) karşı kayıtta | yerel okuma |
| Düzeltme ReasonCode | Evet | trim, 1..64 | — | — |
| Düzeltme Comment | Hayır | trim, ≤ 2000 | — | — |
| Düzeltme EvidenceRefs | WriteOff için Evet (O-13) | en az 1 MOD-0031 bağlantı kimliği, her biri Guid ≠ Empty, ≤ 20 | — | MOD-0031 (DL-3) |
| Karar WorkflowTaskId | Evet | Guid ≠ Empty, talebin örneğine ait | — | MOD-0023 |
| Karar ReasonCode | Reject için Evet | trim, 1..64 | — | — |
| Tahsilat Amount | Evet | > 0; ≤ OutstandingAmount; küsurat ≤ hane | — | — |
| Tahsilat PaymentDate | Evet | ≥ InvoiceDate; ≤ bugün (O-12) | — | — |
| Tahsilat PaymentReference | Evet | trim, 1..64; normalize anahtar (`PaymentReferenceKey`) kiracı + şirket + müşteri içinde geri alınmamış tahsilatlar arasında tekil (D16); aksi 409 `RECEIVABLE_PAYMENT_DUPLICATE` | Unique kısmi (TenantId, LegalEntityId, CustomerId, PaymentReferenceKey) `{EntryType: Receipt, IsReversed: false}` | iş anahtarı ön kontrolü (kontrol sırası 4) |

**Durum kuralları:** Update yalnız `Draft`; Delete / BulkDelete yalnız `Draft` (BulkDelete'te bir kimlik uygun değilse
hiçbiri silinmez — tek işlem); Issue yalnız `Draft` ve müşteri + LE uygun; SetCollectionStage yalnız `Open` /
`PartiallyPaid` ve açık düzeltme yokken; RequestAdjustment tür ön koşulu ve açık talep yokken (PaymentReversal: fatura
`PartiallyPaid` / `Paid`, `CollectionStage ≠ WrittenOff`, tahsilat geri alınmamış); RecordPayment yalnız `Open` /
`PartiallyPaid`, `CollectionStage ≠ WrittenOff`, açık düzeltme yokken ve iş anahtarı serbestken (D16).

**SoD kuralları (düzeltme kararlarında, MOD-0023 çağrısından ÖNCE; domain-config "başlatan kuralı miras, ek kurallar
modülde"):**

| # | Kural | Nerede | Kaynak karşılığı |
|---|---|---|---|
| A1 | Talebi gönderen (`RequestedBy` = MOD-0023 başlatanı) karar veremez | çözümleyici (aday dışı) + karar kontrolü + MOD-0023 `SodViolation` | Recovery `WorkflowTaskTransitionSupport.cs:964-977` (MOD-0126 pack) |
| A2 | Faturayı oluşturan (`InvoiceCreatedBy`) ve açan (`InvoiceIssuedBy`) o faturanın düzeltmesine karar veremez | aynı | kaynakta düzeltme akışı yok; geçersiz kılmada yerel SoD vardı (paket §L.7) — taşınmaz, kural yeniden yazılır |
| A3 | `IsTier2` talepte **Approve** `finance-treasury.receivable-adjustments.tier2-approve` ister; çözümleyici bu izni taşıyan adayları seçer; tier-2 aday kalmazsa 422 `RECEIVABLE_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (muafiyet yok, O-8) | çözümleyici + karar | O-7 |
| A4 | `IsTier2` = `Amount ≥ eşik` (aynı para birimi) ya da para birimi farklı ya da ayar yok; talep anında sabitlenir | talep | MOD-0126 R5/R6 deseni |
| A5 | Aktör karşılaştırması token `sub`'ının **Guid** değeri üzerindendir; `sub` Guid değilse karar reddedilir (`FailedRoleConflict`) | her yer | kaynak aktör `string` (`AccountsReceivableEntities.cs:388-395@218acaf5`) — taşınmaz |
| A6 | İzin karar anında yeniden değerlendirilir; onaylayan MOD-0023 görevinin atanmış aktörüdür (`WorkflowActorDenied`) | karar + MOD-0023 | — |
| A7 | **PaymentReversal (D16):** geri alınan tahsilatı kaydeden (`ReversedPaymentRecordedBy`) o geri almaya karar veremez (`FailedPaymentRecorderDecision`); A1–A6 aynen uygulanır (talebi gönderen, faturayı oluşturan / açan karar veremez; tier-2 eşiği geri alınan tutara uygulanır) | çözümleyici (aday dışı) + karar kontrolü | yeni |

Reject de A1–A2'ye uyar (talebi gönderen kendi talebini reddederek geri çekemez; geri çekme Follow-up).

## Failure Path to Verify

- **Kendi talebine karar (gönderen)** → 403 `RECEIVABLE_SOD_DENIED`, `FailedRequesterDecision`; MOD-0023 çağrılmaz.
- **Faturayı oluşturan / açan kişinin kararı** → 403 `RECEIVABLE_SOD_DENIED` (`FailedInvoiceCreatorDecision` /
  `FailedInvoiceIssuerDecision`); MOD-0023 çağrılmaz.
- **Tier-2 talep, tier-2 izni olmayan onaylayıcı** → 403 `RECEIVABLE_TIER2_APPROVER_REQUIRED`, `FailedAmountTier`.
- **Açık talep varken ikinci talep ya da tahsilat** → 409 `RECEIVABLE_ADJUSTMENT_PENDING`, uzak çağrı yok.
- **Açık karar niyeti varken başka anahtar/aktörden karar** → 409 `RECEIVABLE_DECISION_PENDING`, MOD-0023 çağrılmaz.
- **Ödemesi olan faturada Void talebi** → 409 `RECEIVABLE_ADJUSTMENT_NOT_ALLOWED`; **vadesi gelmemiş ve aşaması None/PromiseToPay
  olan faturada WriteOff** → 409 aynı kod.
- **İş akışı başlatma zaman aşımı / bağlantı kopması / boş 2xx** → 202 `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN`, niyet
  `Unknown`, talep `Submitted`; ret yazılmaz; Reconcile aynı uzak anahtarla ilerler, ikinci örnek oluşmaz.
- **Başlatma kesin reddi** → 502 `RECEIVABLE_WORKFLOW_START_FAILED`, niyet `Refused`, talep `StartRefused`, fatura aşaması geri.
- **Uzak onay başarılı + tx2 hatası / çökme** → niyet açık, fatura değişmemiş; Reconcile uzak çağrıyı tekrarlamadan etkiyi
  uygular ve sonuç olayını yazar.
- **Idempotency-Key yok** → 400 `RECEIVABLE_IDEMPOTENCY_KEY_REQUIRED`, kayıt yok. **Aynı anahtar + aynı yük** (`Completed`)
  → kayıtlı yanıt, ikinci kayıt/olay/uzak çağrı yok; **`Pending`** → 202 `RECEIVABLE_REQUEST_IN_PROGRESS`, kurtarma yok;
  **aynı anahtar + farklı yük** → 409 `RECEIVABLE_IDEMPOTENCY_CONFLICT`.
- **Mükerrer dış fatura numarası** (aynı şirket) → 409 `RECEIVABLE_EXTERNAL_NUMBER_DUPLICATE`; silinen taslağın ya da Void
  faturanın numarası yeniden kullanılabilir.
- **Müşteri yok / başka kiracının** → 422 `RECEIVABLE_CUSTOMER_NOT_FOUND`; **faturalanabilir değil** → 422
  `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE`; **MOD-0149 erişilemez** → 503 `RECEIVABLE_CUSTOMER_UNAVAILABLE`, kayıt değişmez.
- **Şirket aktif değil ya da onaylı değil** → her yazma komutu (Create, Update, Delete, BulkDelete, Issue, SetCollectionStage,
  RequestAdjustment, Approve, Reject, RecordPayment) 422 `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`, kayıt değişmez; MOD-0220
  erişilemez → 503; okuma ve Reconcile etkilenmez (D14/4).
- **Başka kiracı / başka şirket / kapsam dışı şirket faturası** (okuma, yazma, karar) → 404 `RECEIVABLE_INVOICE_NOT_FOUND`;
  kapsam dışı şirketle liste → 200 boş.
- **Yalnız `read-tenant-wide` ile görünen faturaya yazma** (kullanıcının o şirkette kendi yetkisi yok — gap 4/G1) → her
  yazma komutu (Create, Update, Delete, Issue, SetCollectionStage, RequestAdjustment, Approve, Reject, Reconcile,
  RecordPayment) 404 `RECEIVABLE_INVOICE_NOT_FOUND`; liste ve detay okuması 200; uzak çağrı, kayıt ve makbuz yok.
- **Reconcile, uzakta örnek bulunamadı / okuma belirsiz** (gap 4/G2, gap 5/G2) → niyet `Cancelled` yapılmaz, `Unknown`
  kalır; okuma "kesin boş" kararı vermez. Zaman aşımı / 5xx / okunamayan gövde → uzak yazma/başlatma çağrısı yok. 404 /
  boş okumada yalnız MOD-0023 başlatma / görev geçişi aynı deterministik anahtarla yeniden yazılabilir; önce şirket + SoD +
  izin + aktör denetlenir: şirket uygun değilse uzak yazma/başlatma çağrısı yok, niyet `Unknown` + `LastOutcomeCode =
  RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422; aktör farklıysa 409 `RECEIVABLE_RECONCILE_RESTART_ACTOR_MISMATCH`;
  uygunsa aynı anahtarla tek uzak yazma — başarı (yeni ya da yeniden oynatılmış) niyeti kapatır, yinelenen anahtar hatası
  / 5xx / zaman aşımı → `Unknown` (asla `Refused` değil). MOD-0031'e hiçbir kurtarma yolu yazmaz.
- **Eksik alan** (müşteri, para birimi, satır) → 400 + alan düzeyi resx iletisi; kayıt oluşmaz.
- **Hassasiyet aşımı** (JPY'de küsurat) → 400; hane kaynağı yok → 503 `RECEIVABLE_CURRENCY_PRECISION_UNAVAILABLE`.
- **Taksit toplamı ≠ brüt toplam** → 400.
- **Eşzamanlılık** (bayat `If-Match`) → 409 `RECEIVABLE_INVOICE_VERSION_CONFLICT`, sessiz üzerine yazma yok.
- **Açılmış faturayı güncelleme / silme** → 409 `RECEIVABLE_INVOICE_STATUS_INVALID`.
- **İzinsiz geçiş** (aşama tablosu dışı) → 409 `RECEIVABLE_COLLECTION_TRANSITION_INVALID`.
- **Kalandan büyük tahsilat** → 422 `RECEIVABLE_PAYMENT_EXCEEDS_OUTSTANDING`.
- **Mükerrer tahsilat** (D16, gap 6/P2-a) — aynı kiracı + şirket + müşteri + ödeme referansıyla geri alınmamış tahsilat
  varken, farklı idempotency anahtarıyla ya da başka kullanıcıdan, aynı ya da aynı müşterinin başka faturasına → 409
  `RECEIVABLE_PAYMENT_DUPLICATE`; ikinci kayıt, fatura değişikliği ve denetim olayı yok. Eşzamanlı iki istekte biri 201,
  diğeri 409 `RECEIVABLE_PAYMENT_DUPLICATE` (sürüm çakışması ya da 500 değil).
- **Tahsilat geri alma ön koşulu yok** (D16) — tahsilat bu faturada değil → 404 `RECEIVABLE_PAYMENT_NOT_FOUND`; tahsilat
  zaten geri alınmış, fatura `WrittenOff` ya da `Void`, karşı kayıt için geri alma istenmiş → 409
  `RECEIVABLE_ADJUSTMENT_NOT_ALLOWED`; açık talep varken → 409 `RECEIVABLE_ADJUSTMENT_PENDING`. Uzak çağrı yok.
- **Tahsilatı kaydeden kişinin geri alma kararı** (A7) → 403 `RECEIVABLE_SOD_DENIED` (`FailedPaymentRecorderDecision`);
  MOD-0023 çağrılmaz.
- **Geri alma onayı belirsiz / tx2 hatası** → fatura ve tahsilatlar değişmez, karşı kayıt yazılmaz, niyet açık; Reconcile
  kendi niyetine bağlanmış sonuçla etkiyi bir kez yazar (tek karşı kayıt — `ux_receivable_payment_reversal_of`).
- **Uzak kayıt silinmiş / temizlenmiş** (gap 6/P1-a) → okuma silinmiş kaydı gösterirse ya da silinmemiş kayıt yokken
  niyetin ilk denemesi kayıtlıysa (okuma silinmiş durumu ayırt edemiyorken) aynı anahtarla yeniden yazma **yapılmaz**; niyet `Unknown`, 202
  `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN` / `RECEIVABLE_DECISION_OUTCOME_UNKNOWN`; talep, fatura ve makbuz değişmez.
- **Başka talebin ya da kararın uzak sonucu okunuyor** (gap 6/P1-b) → bu niyete bağlanmaz; eşleme (kiracı, talep, karar
  kimliği, uzak anahtar, geçişte görev + eylem) tutmazsa ya da yanıt eşleme alanını taşımıyorsa niyet `Unknown` kalır;
  etki yazılmaz.
- **Yetkisiz aktör** → API 403; UI: kabuk içinde erişim açıklaması, düğme yok (UAS-001).
- **Denetim kaydı yazılamıyor** (outbox yazımı başarısız) → işlem geri alınır, komut başarısız, iş verisi yazılmaz.
- **Ortak kod servisi yanıt vermiyor** → 503 `RECEIVABLE_RECORD_CODE_UNAVAILABLE`, fatura yazılmaz.

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (kaynakta yok — paket §C, §G).
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça, çok kelimede tire. Ad alanı `finance-treasury.*` (**D11/4**). Kaynak
  `receivable.*` / `ar.*` anahtarları (`AccountsReceivableSecurity.cs:26-55@218acaf5`, 30 anahtar + `receivable.edit` takma
  adı :58-70) **kopyalanmaz**; aşağıdaki eşleme yalnız izlenebilirlik içindir (kaynak anahtar hiç yayımlanmadığından takma ad
  gerekmez, PKS-001 §3). Kaynak 7 rol sabiti (`treasury_viewer` … `service_principal`, :110-116) taşınmaz; rol şablonları
  MOD-0018 sahibiyle (O-15).
- Aktör: `tenant_user`; `platform_admin` geçişi standarttır. Arka plan aktörü bu sürümde yok.

| Recovery anahtarı | Katman | Uçlar | Kaynak anahtar |
|---|---|---|---|
| `finance-treasury.receivable-invoices.read` | T1 | GET liste, GET detay | `receivable.view` |
| `finance-treasury.receivable-invoices.read-tenant-wide` | T3 (yalnız açık atama) | kiracıdaki tüm şirketler (SEC-002 §3.5) — **yalnız okuma**; hiçbir yazma komutunun kapsamını genişletmez (gap 4/G1) | — (kaynakta `ar_legal_entity_scope` talebi) |
| `finance-treasury.receivable-invoices.create` | T1 | POST | — |
| `finance-treasury.receivable-invoices.update` | T1 | PUT | — (`receivable.edit` takma adı taşınmaz) |
| `finance-treasury.receivable-invoices.delete` | T1 | DELETE | — |
| `finance-treasury.receivable-invoices.bulk-delete` | T2 | DELETE bulk | — |
| `finance-treasury.receivable-invoices.issue` | T2 | issue | — |
| `finance-treasury.receivable-invoices.collection-manage` | T2 | collection-stage | — |
| `finance-treasury.receivable-adjustments.request` | T3 | adjustments (POST) | `receivable.writeoff.propose` |
| `finance-treasury.receivable-adjustments.approve` | T3 | approve | `receivable.writeoff.approve` |
| `finance-treasury.receivable-adjustments.tier2-approve` | T3 | approve (tier-2, handler içi) | — |
| `finance-treasury.receivable-adjustments.reject` | T3 | reject | — |
| `finance-treasury.receivable-adjustments.reconcile` | T3 | workflow/reconcile | — |
| `finance-treasury.receivable-payments.record` | T3 | payments (D16) | — (`receivable.settlement.consume` MOD-0269 tüketimidir, eşdeğer değil) |
| `finance-treasury.receivable-trace.read` | T1 | trace | `receivable.audit.view` |

- **Taşınmayan kaynak anahtarlar:** `receivable.import`, `receivable.reconcile` (içe aktarma, O-10); `receivable.exception.*`;
  `receivable.creditmemo.*` (kredi notu Follow-up); `receivable.projection.build`, `ar.projection.*`, `ar.schedule.override.*`
  (MOD-0125); `receivable.evidence.*`, `ar.evidence.export.revoke`, `receivable.export.request` (MOD-0031 / dışa aktarma);
  `receivable.settlement.consume` (MOD-0269).
- Kullanıcının MOD-0023 tarafında da `platform.workflow.instances.start` (talep eden) ve `platform.workflow.tasks.approve`
  / `.reject` (onaylayıcı) izinleri olmalıdır (MOD-0126 pack Authorization); müşteri seçici için O-3'ün sonucuna göre
  `crm.account.read` ya da MOD-0149 lookup izni.
- Tier-3 eylemler (`read-tenant-wide`, `adjustments.*`, `payments.record`) bu pakette kayıtlıdır (PKS-001 §5 Tier 3).
- **Tahsilat geri alma (D16; izin KARAR D21/6)** ayrı uç ve ayrı izin açmaz: `PaymentReversal` bir düzeltme türüdür; talep
  `receivable-adjustments.request`, karar `…approve` / `…tier2-approve` / `…reject`, kurtarma `…reconcile` ister. Tahsilatı
  kaydedenin kararı A7 ile engellenir (izin bunu kaldırmaz).

## Audited Events

Yol `b`: `Diten.BuildingBlocks.CentralAudit` ortak ileticisi (AUD-001 §5 K4) — komut `ICentralAuditDeclaration` taşır,
servis `AddCentralAudit<FinanceTreasuryCentralAuditScope>(…, RequireEnlistment = true)` kaydeder (servis iskeletiyle,
MOD-0126 pack), her depo işlemi olayı **aynı Mongo işlemine** yazar. **Servisin denetim altyapısı bugün yok** (servis yok;
`tests/architecture/audit-ledger/` altında `Diten.FinanceTreasuryService.md` yok — ölçüldü): kurulum servis iskeletinin
ilk adımıdır (Dependencies). Kaynak denetim yolu (`IAccountsReceivableControlAuditSink` → varsayılan
`FailClosedArControlAuditSink` her zaman `false`, `FailClosedAccountsReceivableReferenceValidators.cs:59-65@218acaf5`;
paket §H, §L.2) **taşınmaz**.

`Category`: Platform `AuditCategory`'de finans değeri yok — MOD-0126 **O-6** ile ortak; karar çıkana kadar değer uydurulmaz.
`SourceModule`: `MOD-0120`. `EntityType`: `ReceivableInvoice` (aksi yazılmadıkça).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | K2 (fail-closed) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreateReceivableInvoiceCommand | finance-treasury.receivable-invoice.created | ReceivableInvoice | b | AfterState alan adları: InvoiceCode, Status, CurrencyCode, GrossTotal, LineCount, DueDate | evet (öneri) | — |
| UpdateReceivableInvoiceCommand | finance-treasury.receivable-invoice.updated | ReceivableInvoice | b | değişen alan **adları** (değer değil) | evet (öneri) | — |
| DeleteReceivableInvoiceCommand | finance-treasury.receivable-invoice.deleted | ReceivableInvoice | b | BeforeState: InvoiceCode, Status | evet (öneri) | — |
| BulkDeleteReceivableInvoiceCommand | finance-treasury.receivable-invoice.bulk-deleted | ReceivableInvoice | b | silinen kimlik sayısı; her kimlik için ayrı varlık kaydı (çoklu kimlik biçimi DL-1'de ölçülür, MOD-0126 ile aynı) | evet (öneri) | — |
| IssueReceivableInvoiceCommand | finance-treasury.receivable-invoice.issued | ReceivableInvoice | b | Status Draft→Open, GrossTotal, CurrencyCode, CustomerId | evet (öneri) | — |
| SetReceivableInvoiceCollectionStageCommand | finance-treasury.receivable-invoice.collection-stage-changed | ReceivableInvoice | b | CollectionStage önce→sonra, ReasonCode | evet (öneri) | — |
| RequestReceivableInvoiceAdjustmentCommand | finance-treasury.receivable-invoice.adjustment-submitted (sonuç, tx2/Reconcile) | ReceivableAdjustmentRequest | b | AdjustmentType, Amount, CurrencyCode, IsTier2, PolicyVersion, Status Submitted→PendingApproval (tx1 deneme olayı `…adjustment-start-requested`) | **evet — K2 (D14/3)** | — |
| ApproveReceivableInvoiceAdjustmentCommand | finance-treasury.receivable-invoice.adjustment-approved | ReceivableAdjustmentRequest | b | karar kimliği, SodResult, AdjustmentType, fatura Status / CollectionStage önce→sonra, OutstandingAmount önce→sonra; PaymentReversal'da ayrıca geri alınan tahsilat kimliği, karşı kayıt kimliği, PaidAmount önce→sonra (D16) | **evet — K2 (D14/3)** | — |
| RejectReceivableInvoiceAdjustmentCommand | finance-treasury.receivable-invoice.adjustment-rejected | ReceivableAdjustmentRequest | b | karar kimliği, ReasonCode, CollectionStage geri dönüşü | **evet — K2 (D14/3)** | — |
| ReconcileReceivableInvoiceAdjustmentCommand | kapattığı niyetin **sonuç** olayı (geçiş matrisi); niyet değişmediyse olay yok | ReceivableAdjustmentRequest | b | niyet/karar durumu önce→sonra; bağlı makbuz yalnız `Pending` ise `Completed` | evet (öneri) | — |
| RecordReceivableInvoicePaymentCommand (D16) | finance-treasury.receivable-invoice.payment-recorded | ReceivableInvoice | b | Payment kimliği, Amount, Status önce→sonra, OutstandingAmount önce→sonra | **evet — K2 (D14/3)** | — |

- **İki aşamalı komutların deneme olayları:** RequestAdjustment → `finance-treasury.receivable-invoice.adjustment-start-requested`
  (tx1) + sonuç `…adjustment-submitted` / `…adjustment-start-refused` / `…adjustment-start-cancelled`; Approve / Reject →
  `finance-treasury.receivable-invoice.adjustment-decision-requested` (tx1, karar türü alanda) + sonuç
  `…adjustment-approved` / `…adjustment-rejected` / `…adjustment-decision-refused` / `…adjustment-decision-cancelled`
  (tx2 / Reconcile). Deneme olayı tek başına "yapıldı" anlamına gelmez; tabloda yazılan **sonuç** olayıdır. Reconcile kendi
  olayını değil kapattığı niyetin sonuç olayını yazar.
- **Kayda yazılmayan:** müşteri anlık görüntü değerleri, dış fatura numarası değeri, satır açıklamaları, yorum serbest
  metni, tahsilat referans değeri, tahsilat hesabı referansı, token. Değişen alanların yalnız **adı** yazılır (AUD-001 §3).
- **Tahsilat geri alma (D16) yeni olay adı açmaz:** `PaymentReversal` talebi, kararı ve Reconcile'ı yukarıdaki düzeltme
  olaylarını (`…adjustment-start-requested`, `…adjustment-submitted`, `…adjustment-decision-requested`,
  `…adjustment-approved` / `…adjustment-rejected` …) `AdjustmentType = PaymentReversal` alanıyla yazar; karşı kayıt onay
  olayıyla aynı işlemdedir. Mükerrer tahsilat reddi (409) iş verisi yazmaz, olay da yazmaz (G-AUD-1).
- **K2 — KARAR (sahip kararı D14/3):** alacağı etkileyen kararlar — iptal / silme talebi (Void / WriteOff), tahsilat geri
  alma talebi (PaymentReversal, D16), onayı ve reddi — ve elle tahsilat kaydı K2'dir (fail-closed; denetim kaydı iş verisiyle aynı işlemde). AUD-001 §4.3 listesinde
  açıkça sayılmıyordu; sahip MOD-0126 D11/3 desenini AR'ye uyguladı. Uyulabilir yol bugün var
  (`RequireEnlistment = true`; SupplyChain kabulü, `CT-DECISIONS.md` 2026-10-21). Diğer komutlar aynı yolla yazıldığı için
  pratikte hepsi kapalı-başarısızdır.
- **Bilinen boşluk G-AUD-1 (MOD-0126 ile ortak):** `CentralAuditBehavior` başarısız komut için kayıt bırakmaz; SoD reddi ve
  yetkisiz denemeler bu sürümde yalnız modülün kendi izinde (`ReceivableAdjustmentDecision.State`, talep durumu) görünür.
  Merkezi `Denied` kaydı BL-501 sahibinin işidir — Follow-up; bu paket kendi iletim kopyasını yazmaz.
- **Okuma denetimi:** gerekmez (§7'deki üç sınıfa girmiyor; dışa aktarma yok).
- **Arka plan işi yok:** İ2 satırı yok.
- **Kaynağın sınırlı denetim / çoklu yazma devri taşınmaz** (Implementation Notes → Taşınmayacaklar 1): bu tablo her yazma
  komutunu iş verisiyle aynı işlemde denetler; kaynağın "denetim sonradan / yok sayılan geç denetim / CAS'tan önce başarı"
  sırası hiçbir komutta yoktur.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/receivable-invoices` yok (yeni servis). Gerekli rotalar:
  `/api/receivable-invoices`, `/api/receivable-invoices/{everything}` → `localhost:5071`; `UpstreamHttpMethod` GET, POST,
  PUT, PATCH, DELETE, **OPTIONS**; explicit rotalar catch-all'dan önce (`routes.md`).
- `ocelot.json` korunur: **integration-agent task'ı**; önkoşul AGENTS.md §3 / `ports.md` 5071 kaydı (MOD-0126 rotalarıyla
  aynı task'ta açılabilir).
- Kaynak rotaları (`/api/v1/mod0120` ve `/{everything}` → 127.0.0.1:5058, kaynak `ocelot.json:1237-1284@218acaf5`, paket §C)
  taşınmaz.
- Servisten MOD-0149 (`/api/crm/accounts/*` ya da O-3 ucu), MOD-0220, MOD-0023, MOD-0031, MOD-0048 ve veri kapsamı
  (O-14) çağrıları Gateway ya da ortamın servis-servis adresi üzerinden; adresler yapılandırmadan, eksikse servis başlamaz.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir,
pack'te asla izlenmez; bu pack'teki her AC yalnız "tanımlı"dır.

- [ ] AC-1 (zorunlu) — `POST /api/receivable-invoices` geçerli istekle 201 döner; fatura `Draft`, `InvoiceCode` RCS-001
  biçiminde ortak servisten (istekte gönderilen kod 400), toplamlar ve tek varsayılan taksit sunucu hesabı;
  `TenantId`/`LegalEntityId` sunucuda yazılı, yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — Şirket `OperationalStatus = Active` ve `ApprovalStatus = Approved` değilse alacak oluşturan /
  değiştiren her yazma komutu (Create, Update, Delete, BulkDelete, Issue, SetCollectionStage, RequestAdjustment, Approve,
  Reject, RecordPayment) 422 `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE` döner ve kayıt değişmez (komut başına ayrı test); kontrol
  her çağrıda MOD-0220 `lookup-validation`'dan; MOD-0220 erişilemezse 503 ve geçiş yok. Aynı şirketin faturasının liste ve
  detay okuması 200 döner (okuma etkilenmez) ve açık niyetin Reconcile'ı tamamlanır (D14/4).
- [ ] AC-3 (zorunlu) — Müşteri MOD-0149 sözleşmesinden doğrulanır: olmayan / başka kiracının müşterisi 422
  `RECEIVABLE_CUSTOMER_NOT_FOUND`, faturalanabilir olmayan durum 422 `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE`, MOD-0149 erişilemez
  503; MOD-0149 koleksiyonunu doğrudan okuyan kod yok (mimari testi: FinanceTreasury'de `accounts` /
  `account_external_references` koleksiyon adı 0).
- [ ] AC-4 (zorunlu) — `Issue` sonrası satır, taksit, toplam, para birimi, müşteri ve anlık görüntüyü değiştiren her istek
  409 döner; Update / Delete yalnız `Draft`'ta çalışır.
- [ ] AC-5 (zorunlu) — Başka kiracının ya da başka şirketin faturasına okuma, yazma, karar ve tahsilat 404 döner.
- [ ] AC-6 (zorunlu) — Veri kapsamı (SEC-002 §4 kanıt testi): aynı yetkili iki kullanıcı, iki ayrı şirket kapsamı → birinin
  listesinde diğerinin şirketinin faturası çıkmaz ve detayı 404; kapsamı olmayan üçüncü kullanıcı boş liste görür;
  `read-tenant-wide` izni olan görür. Kapsam daraltması (`IFinanceTreasuryDataScopeSource` süzgeci) kaldırıldığında test
  kırmızıya döner (sabotaj); çözücü hata verdiğinde liste boş döner (D14/1).
- [ ] AC-7 (zorunlu) — Tahsilat aşaması geçişleri tablodaki izinlerle birebir; tablo dışı geçiş 409
  `RECEIVABLE_COLLECTION_TRANSITION_INVALID`; `WriteOffProposed` / `WrittenOff` elle girilemez; her geçiş
  `CollectionStageHistory`'ye ve merkezi denetime yazılır.
- [ ] AC-8 (zorunlu) — Yaşlandırma okuma anında türetilir: `DueDate` dün olan fatura `Overdue`, `DaysOverdue = 1`; bugün
  olan `Due`; yarın olan `NotDue` (O-12'nin saat dilimiyle); saklanan alan yoktur.
- [ ] AC-9 (zorunlu) — RequestAdjustment MOD-0023'ü çağırmadan önce talep (`Submitted`), başlatma niyeti (`Pending`), fatura
  kilidi ve (WriteOff'ta) `WriteOffProposed` aşamasını commit eder; uzak çağrı niyetteki uzak anahtarla yapılır.
- [ ] AC-10 (zorunlu) — Başlatma zaman aşımı, bağlantı kopması ve boş/okunamayan 2xx 202
  `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN` üretir; niyet `Unknown`; ret yazılmaz; Reconcile aynı anahtarla ilerler ve
  MOD-0023'te ikinci örnek oluşmaz (sahte MOD-0023 anahtar başına tek örnek sayar).
- [ ] AC-11 (zorunlu) — Kesin başlatma reddi 502 `RECEIVABLE_WORKFLOW_START_FAILED`, talep `StartRefused`, fatura aşaması ve
  kilidi geri döner.
- [ ] AC-12 (zorunlu) — Talebi gönderenin, faturayı oluşturanın ve açanın Approve / Reject çağrısı 403
  `RECEIVABLE_SOD_DENIED` döner ve MOD-0023'e istek gitmez (sahte istemciyle sayılır).
- [ ] AC-13 (zorunlu) — `IsTier2` talepte `tier2-approve` izni olmayan onaylayıcı 403 `RECEIVABLE_TIER2_APPROVER_REQUIRED`
  alır; izni olan geçer. Kiracı ayarı yoksa her talep tier-2 (`DefaultAllTier2`), ayar deposu erişilemezse 503 (iki ayrı
  test). Kodda tek eşik tipi vardır (mimari testi). (O-7 farklı karar verirse bu madde o kararla yeniden yazılır, numarası
  korunur.)
- [ ] AC-14 (zorunlu) — Onaylı Void: fatura `Void`, `OutstandingAmount = 0`, dış numara anahtarı boşalır; onaylı WriteOff:
  `CollectionStage = WrittenOff`, `WrittenOffAmount` = talep tutarı, `OutstandingAmount = 0`, `Status` değişmez; Reject:
  aşama `PreviousCollectionStage`'e döner. Etki karar sonucu (`Recorded`) ile **aynı işlemde** yazılır.
- [ ] AC-15 (zorunlu) — Açık düzeltme varken aynı faturaya ikinci talep ve tahsilat 409 `RECEIVABLE_ADJUSTMENT_PENDING`;
  açık karar niyeti varken başka anahtarla karar 409 `RECEIVABLE_DECISION_PENDING`, MOD-0023'e çağrı yok.
- [ ] AC-16 (zorunlu) — Void yalnız `PaidAmount = 0` olan `Open` faturada; WriteOff yalnız ön koşul (Overdue ya da
  InCollection/Dunning/Disputed) varken; aksi 409 `RECEIVABLE_ADJUSTMENT_NOT_ALLOWED`.
- [ ] AC-17 (zorunlu) — Idempotency (D12/2): her yazma komutu anahtarsız 400; aynı kapsam + aynı yük, makbuz `Completed` ise
  kayıtlı yanıtı (202 belirsiz dahil), `Pending` ise 202 `RECEIVABLE_REQUEST_IN_PROGRESS` döndürür (replay kurtarma yapmaz)
  ve ikinci kayıt, ikinci uzak çağrı ya da ikinci denetim olayı oluşmaz; aynı kapsam + farklı yük 409; aynı anahtar başka
  aktörde ayrı makbuzdur. İlk yanıtın kaybolduğu senaryo ayrıca koşulur.
- [ ] AC-18 (zorunlu) — Merkezi denetim: tek aşamalı komutlar tam **bir** olay bırakır; iki aşamalı komutlar tx1'de bir
  **deneme**, tx2'de (ya da Reconcile'da) bir **sonuç** olayı; idempotent tekrar yeni olay üretmez. Olay iş verisiyle aynı
  Mongo işleminde; outbox yazımı zorla başarısız edildiğinde komut başarısız ve iş verisi yazılmaz (sabotaj testi); uzak onay
  sonrası tx2 zorla başarısız edildiğinde fatura değişmez, niyet açık kalır ve Reconcile uzak çağrıyı tekrarlamadan etkiyi
  ve sonuç olayını yazar (kurtarma testi).
- [ ] AC-19 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard`
  yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş (borç satırı eklenmemiş).
- [ ] AC-20 (zorunlu) — Tutarlar `decimal`; hane aşan tutar 400 (yuvarlama yok); satır / taksit / tahsilat para birimi
  faturanınkinden farklıysa 400; taksit toplamı brüt toplama eşit değilse 400; mimari testi: para alanlarında
  `float`/`double` 0.
- [ ] AC-21 (zorunlu) — Hiçbir varlık, DTO ya da denetim kaydı IBAN, hesap numarası, BIC/SWIFT, banka adı alanı taşımaz
  (alan adı taraması); tahsilat hesabı yalnız Guid.
- [ ] AC-22 (zorunlu) — Her denetleyici aksiyonunda `[HasPermission]` vardır (mimari testi); izinsiz çağrı 403;
  `read-tenant-wide` otomatik yoldan verilmez (`ExplicitGrantOnlyPermissions` listesinde).
- [ ] AC-23 (zorunlu) — Soft delete yalnız `Draft`; `IsDeleted` + `DeletedAt` (UTC) birlikte; silinen fatura listede
  görünmez, kodu yeniden kullanılmaz; BulkDelete'te uygun olmayan bir kimlik varsa hiçbiri silinmez.
- [ ] AC-24 (zorunlu) — Mükerrer dış fatura numarası aynı şirkette 409; farklı şirkette kabul; silinen taslak ve Void
  faturanın numarası yeniden kullanılabilir.
- [ ] AC-25 (zorunlu) — Tüm `Views/FinanceTreasury/ReceivableInvoices/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"`
  açıkça yazılı; `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` yok;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area FinanceTreasury --module ReceivableInvoices --format gaps`
  sapma vermez ve `quality-gate-datatable` geçer; `<table data-dt-data-mode="server">` pack ile eşleşir.
- [ ] AC-26 (zorunlu) — 7 dil (`en, tr, fr, es, zh, ar, ru`) resx parite PASS; İngilizce olmayan dosyada İngilizce yer
  tutucu yok; hardcoded metin yok (`grep` 0).
- [ ] AC-27 (zorunlu) — `read` izni olmayan kullanıcı sayfayı açınca kabuk içinde erişim açıklaması görür; başlık, tablo,
  "Yeni" düğmesi çizilmez, yönlendirme olmaz (UAS-001, canlı denendi); SoD nedeniyle karar veremeyen kullanıcıya karar
  düğmesi çizilmez.
- [ ] AC-28 (zorunlu) — Durum (5), aşama (7) ve yaşlandırma (3) rozetleri ve filtreleri enum'larla birebir; enum'da olmayan
  değer yok.
- [ ] AC-29 (zorunlu) — Sayfa yardım metni `Help.RECEIVABLEINVOICES.Purpose`, `.Audience`, `.Step1…Step5` + `.Step{n}Title`
  7 dilde var.
- [ ] AC-30 (zorunlu) — `docs/analysis/contracts/accounts-receivable.openapi.yaml` yayımlanmış; uçlar, hata kodları,
  `Idempotency-Key` ve `If-Match` başlıkları sözleşmeyle eşleşir (sözleşme testi).
- [ ] AC-31 (zorunlu) — Bir oluşturma, bir açma ve bir silme dev ortamında çalıştırılır; üç merkezi kayıt AUD-001 §8
  sorgusuyla gösterilir ve on alan işaretlenir (rapor ortamın adını yazar).
- [ ] AC-32 (zorunlu) — Yarış testleri (gerçek replika kümesi, paralel istek): (a) aynı faturaya farklı anahtarla iki
  eşzamanlı düzeltme talebi → tek talep, diğeri 409; (b) iki onaylayıcının eşzamanlı Approve ve Reject'i → tek açık karar
  niyeti, MOD-0023'e tek çağrı; (c) düzeltme talebi ile eşzamanlı tahsilat → biri 409; (d) aynı fatura için iki eşzamanlı
  Create aynı dış numarayla → biri 409. Tekil kısmi indeksler kaldırıldığında (a), (b), (d) kırmızıya döner (sabotaj).
- [ ] AC-33 (zorunlu — sahip kararı D16, DL-3) — Tahsilat: kalan 100'lük faturada 40 → `PartiallyPaid`, kalan 60, en eski
  taksit düşer ve `InstallmentAllocations` yazılır; 60 → `Paid`, aşama `None`; 61 → 422
  `RECEIVABLE_PAYMENT_EXCEEDS_OUTSTANDING`; tahsilat kaydı güncellenemez/silinemez (güncelleme / silme ucu yok, depoda
  `Replace` / `Delete` yolu yok — mimari testi).
- [ ] AC-34 (zorunlu) — Kaynağın sınırlı denetim / çoklu yazma devri taşınmadı: kod alma iz kaydında (`path@218acaf5` +
  sha256) `IAccountsReceivableControlAuditSink`, `FailClosedArControlAuditSink`, içe aktarma / projeksiyon / kanıt servisleri
  yok; §S sapma tablosunda bu devir için satır yok (taşınırsa satır + sahip + bitiş zorunlu ve etkilenen akış level-2'ye
  geçmez).
- [ ] AC-35 (zorunlu) — Okuma kapsamı yazma kapsamını genişletmez (gap 4/G1): `receivable-invoices.read-tenant-wide` + bütün
  yazma izinlerine sahip ama LE-B'de **kendi şirket yetkisi olmayan** kullanıcı LE-B faturalarını listede ve detayda görür
  (200), ama LE-B'de Create (`X-Legal-Entity-Id = LE-B`), Update, Issue, SetCollectionStage, RequestAdjustment, Approve ve
  Reject, RecordPayment ve tahsilat geri alma talebi (D16) 404 `RECEIVABLE_INVOICE_NOT_FOUND` döner; MOD-0023 / MOD-0220 / MOD-0149'a çağrı
  gitmez, kayıt, makbuz ve denetim olayı oluşmaz (sahte istemcilerle sayılır); aynı kullanıcı LE-A'da (kendi yetkisi) aynı
  komutları çalıştırabilir. Sabotaj: yazma yolunda okuma kapsamı kullanıldığında test kırmızıya döner.
- [ ] AC-36 (zorunlu) — Reconcile önce okur (gap 4/G2; RequestAdjustment, Approve, Reject için ayrı test): (a) uzakta niyetin
  kendi eşleme alanlarıyla (AC-38) örnek / geçiş **var** → şirket uygun değilken bile sonuç ve etki kaydedilir (MOD-0220 çağrılmaz — D15/2), bağlı `Pending` makbuz
  `Completed`, tek sonuç olayı; (b) okuma 404 / boş / zaman aşımı / 5xx → niyet **`Cancelled` olmaz**, `Unknown` kalır,
  talep, fatura ve makbuz değişmez, zaman aşımı / 5xx'te uzak yazma/başlatma çağrısı yok (okuma çağrısı yapılır); (c)
  okuma örnek / geçiş göstermedi + şirket uygun değil → MOD-0023'e uzak yazma/başlatma çağrısı yok, niyet `Unknown`,
  `LastOutcomeCode = RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422, sonuç olayı yok; (d) okuma örnek / geçiş
  göstermedi + gap 6/P1-a'nın iki koşulu kapalı (AC-37) + şirket uygun + SoD/izin geçer + asıl aktör → başlatma / geçiş aynı deterministik anahtarla yeniden yazılır
  ve MOD-0023'te tam **bir** örnek / geçiş vardır (ikinci Reconcile ikincisini üretmez; yinelenen anahtar hatası `Unknown`
  bırakır, asla `Refused` değil); aktör farklıysa 409 `RECEIVABLE_RECONCILE_RESTART_ACTOR_MISMATCH`, uzak yazma/başlatma
  çağrısı yok; (e) MOD-0031'e Reconcile ya da herhangi bir kurtarma yolu hiç yazma çağrısı yapmaz; belirsiz okumada niyet
  `Unknown` kalır (sahte MOD-0031 yazma çağrısı sayısı 0). Sabotaj: okuma atlanıp doğrudan yeniden yazıldığında,
  "bulunamadı" `Cancelled`'a bağlandığında, farklı anahtarla yeniden yazıldığında ya da MOD-0031'e yazıldığında test
  kırmızıya döner.
- [ ] AC-37 (zorunlu) — Silinmiş uzak kayıttan sonra yeniden yazma güvenli sayılmaz (gap 6/P1-a; RequestAdjustment, Approve,
  Reject için ayrı test): sahte MOD-0023'te niyetin örneği / geçişi oluşturulup sonra soft delete edilir (ya da temizlenir)
  ve niyet `Unknown` bırakılır; Reconcile (a) silinmiş kaydı gösteren okumada ve (b) silinmemiş kayıt dönmeyen ve silinmiş durumu göstermeyen okumada niyetin
  ilk denemesi kayıtlı (tx1 commit olmuş) durumda aynı anahtarla yeniden yazmaz: niyet `Unknown`, 202
  `RECEIVABLE_WORKFLOW_START_OUTCOME_UNKNOWN` / `RECEIVABLE_DECISION_OUTCOME_UNKNOWN`, talep, fatura (kilit, aşama,
  tutarlar), tahsilatlar ve bağlı makbuz değişmez, sahte MOD-0023'te yazma çağrısı 0. Sabotaj: silinmiş durum ve ilk deneme
  kontrolü kaldırıldığında Reconcile ikinci bir örnek / geçiş oluşturur ve test kırmızıya döner.
- [ ] AC-38 (zorunlu) — Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b): (a) aynı faturanın önceki (reddedilmiş /
  başlatması reddedilmiş) düzeltme talebinin örneği ve yeni talebin `Unknown` niyeti varken Reconcile yeni niyeti eski
  örnekle kapatmaz; yeni niyet `Unknown` kalır ya da yalnız kendi uzak anahtarıyla eşleşen örnekle kapanır; (b) aynı görevde
  başka bir kararın geçişi (farklı `RemoteIdempotencyKey`, görev ya da eylem) açık karar niyetini kapatmaz ve etki (Void,
  WriteOff, karşı kayıt) yazılmaz; (c) okuma yanıtı eşleme alanını taşımıyorsa niyet `Unknown` kalır. Sabotaj: eşleme
  yalnız fatura ya da talep kimliğine indirildiğinde test kırmızıya döner.
- [ ] AC-39 (zorunlu) — Mükerrer tahsilat (D16, gap 6/P2-a; gerçek replika kümesi, paralel istek): (a) iki farklı kullanıcı
  farklı `Idempotency-Key` ile aynı faturaya aynı müşteri + aynı `PaymentReference` (büyük/küçük harf ve boşluk farkıyla)
  tahsilatını **eşzamanlı** gönderir → biri 201, diğeri 409 `RECEIVABLE_PAYMENT_DUPLICATE`; fatura tek tahsilat kadar
  azalır, tek `…payment-recorded` olayı; (b) aynı istek sırayla ve bayat `If-Match` ile gelse de 409
  `RECEIVABLE_PAYMENT_DUPLICATE` (sürüm çakışması kodu değil); (c) aynı müşterinin başka faturasına aynı referans → 409
  aynı kod; başka müşteride ya da başka şirkette aynı referans → 201. Sabotaj: `ux_receivable_payment_reference_active`
  indeksi ve ön kontrol kaldırıldığında (a) iki tahsilatla kırmızıya döner.
- [ ] AC-40 (zorunlu) — Onaylı tahsilat geri alma (D16, gap 6/P2-b): 100'lük faturada 40'lık tahsilat (`PartiallyPaid`,
  kalan 60) için `PaymentReversal` talebi → talep `Submitted`, fatura kilitli, tutarlar **değişmez**, MOD-0023 aynı uzak
  anahtarla başlatılır; onay sonucu (`Recorded`) ile **aynı işlemde**: `EntryType = Reversal` karşı kaydı eklenir
  (orijinal tahsilatın tutarı, tarihi ve referansı değişmez; yalnız `IsReversed = true` + `ReversedByPaymentId` bir kez),
  `PaidAmount = 0`, `OutstandingAmount = 100`, taksit kalanları orijinal dağılımla geri gelir, `Status = Open`; aynı
  referansla yeni tahsilat artık 201 (politika: referans serbest kalır); Reject → tahsilat ve fatura değişmez, kilit
  kalkar. Talep, onay ve ret K2'dir: outbox yazımı zorla başarısız edilince komut başarısız, karşı kayıt yazılmaz (sabotaj).
  Belirsiz onayı Reconcile kaydedince karşı kayıt tam bir kez yazılır (ikinci Reconcile ikinci karşı kayıt üretmez —
  `ux_receivable_payment_reversal_of`).
- [ ] AC-41 (zorunlu) — Tahsilat geri alma ön koşulları ve SoD (D16): geri alınmış tahsilatın ikinci geri alma talebi,
  `WrittenOff` / `Void` faturada geri alma ve karşı kayıt için geri alma 409 `RECEIVABLE_ADJUSTMENT_NOT_ALLOWED`; başka
  faturanın tahsilatı 404 `RECEIVABLE_PAYMENT_NOT_FOUND`; açık talep varken 409 `RECEIVABLE_ADJUSTMENT_PENDING`; talebi
  gönderen, faturayı oluşturan / açan ve **tahsilatı kaydeden** kişinin kararı 403 `RECEIVABLE_SOD_DENIED`
  (`FailedPaymentRecorderDecision` dahil) ve MOD-0023'e istek gitmez; tier-2 eşiği geri alınan tutara uygulanır. Sabotaj: A7
  kaldırıldığında tahsilatı kaydedenin onayı geçer ve test kırmızıya döner.
- [ ] AC-42 (zorunlu) — Tahsilat geri alma senaryoları (sahip incelemesi 2026-10-25, gerçek replica set): (a) **tamamen
  ödenmiş** fatura (100/100, `Paid`) tek tahsilatın geri alınmasıyla yeniden açılır: `PaidAmount = 0`, `OutstandingAmount =
  100`, `Status = Open`, taksitler geri gelir; (b) **birden fazla tahsilattan biri** (30 + 70 = 100, `Paid`) geri alınınca
  yalnız o tahsilat `IsReversed`, `PaidAmount = 70`, `OutstandingAmount = 30`, `Status = PartiallyPaid`, diğer tahsilat ve
  referansı değişmez; (c) **aynı tahsilata eşzamanlı iki geri alma talebi** (farklı anahtar / aktör) → tek açık talep,
  ikincisi 409 `RECEIVABLE_ADJUSTMENT_PENDING`, MOD-0023'e tek başlatma; onay sonrası tek karşı kayıt
  (`ux_receivable_payment_reversal_of`); (d) **uzak onay sonrası yerel commit hatası** (MOD-0023 onayı başarılı, tx2 zorla
  başarısız) → talep niyeti açık kalır, karşı kayıt yok, fatura tutarları değişmez; Reconcile uzak onayı (anahtar ve
  eşleşme alanları — P1-a/P1-b engelleri kapandıktan sonra) okuyup tek karşı kaydı ve sonuç olayını yazar, ikinci uzak geçiş
  yok. Sabotaj: (b)'de faturanın tüm tahsilatları sıfırlanırsa, (c)'de `ux_receivable_payment_reversal_of` kaldırılırsa, (d)'de
  karşı kayıt tx2 dışında yazılırsa test kırmızıya döner.

## Test Expectations

- **Birim:** validatorlar (her alan); durum ve aşama geçiş tabloları (izinli/izinsiz her çift); yaşlandırma türetme (sınır
  günleri, taksitli fatura); düzeltme SoD kuralları A1–A7 (eşik sınırı `Amount == eşik`, para birimi farkı, ayar yok, Guid
  karşılaştırması); idempotency parmak izi; RCS kod portu sahtesiyle kod biçimi; toplam ve taksit hesapları (hane sınırları).
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı/şirket izolasyonu (404); veri kapsamı (AC-6, sabotajlı); soft
  delete; idempotent tekrar; CAS 409; CentralAudit commit/rollback ve outbox yazım hatasında komutun başarısız olması
  (sabotaj); tekil indeksler (kod, dış numara, açık talep, açık karar, makbuz, tahsilat iş anahtarı, karşı kayıt — AC-39,
  AC-40).
- **İş akışı sahte sunucusu:** zaman aşımı, bağlantı kopması, boş 2xx, 5xx, kesin 403/404, "başlatma tamamlanmadı" 409; aynı
  anahtarla tekrar → tek örnek; karar geçişinde uzak anahtar iletimi; tx2 hatası sonrası Reconcile; silinmiş örnek /
  geçiş (AC-37) ve başka talebin / kararın örneği (AC-38).
- **MOD-0149 sahtesi:** uygun / uygun değil / yok / başka kiracı / erişilemez; O-3 sözleşmesi gelince gerçek sözleşmeye karşı
  sözleşme testi. **MOD-0220 sahtesi:** MOD-0126 ile aynı beş durum.
- **Mimari:** her aksiyonda `[HasPermission]`; tek eşik tipi; banka verisi alan adı yasağı; `float`/`double` para alanı yok;
  MOD-0149 koleksiyon adı yok; `AuditTrailStandard` filtresi yeşil.
- **Sözleşme:** `accounts-receivable.openapi.yaml` ile uç şekli.
- **Frontend:** browser smoke (liste, oluştur, düzenle, aç, detay, aşama, düzeltme, karar düğmeleri izin/SoD'ye göre);
  yetkisiz kullanıcı canlı denemesi (UAS-001); DataTable doğrulayıcı PASS; RESX 7 dil parite PASS.
- **Build:** `Diten.FinanceTreasuryService` + `Diten.Web` + Gateway 0 hata; yerel kapıda `scripts/` Python testleri de koşulur.
- Kaynak testleri (317/317 @da4abb20, bağlanan baytlar değil — `source-build-evidence-v1.2/…/SUMMARY-v1.2.md` satır 8)
  **kanıt değildir**; Recovery'de yeniden yazılır. Kaynakta hiç olmayan testler (müşteri gerçek doğrulaması, LE güncel
  uygunluğu, denetim–iş verisi atomikliği, belirsiz sonuç) burada zorunludur.

## Page Help (UXH-001)

- Sayfa kodu `RECEIVABLEINVOICES` (`Nav.Page.RECEIVABLEINVOICES`); Create/Edit/Details aynı kodun alt sayfalarıdır.
- Anahtarlar (kiracı 7 dil): `Help.RECEIVABLEINVOICES.Purpose` (müşteri alacağını kaydetme ve izleme; bu ekranın yasal
  fatura kesmediği ve müşteriye göndermediği açıkça yazılır), `Help.RECEIVABLEINVOICES.Audience` (alacak operatörü, alacak
  yöneticisi, finans kontrolörü), `Help.RECEIVABLEINVOICES.Step1…Step5` + `Step{n}Title` (Fatura kaydet · Satır ve taksit ekle
  · Aç · Tahsilatı izle · İptal/silme talebi ve onay). `Immediate` yok. İlgili sayfa: yok.
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact okundu (backend + frontend + `DEV-0001`)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, entity_base, form_field_count)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile birebir (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; SoD kuralları A1–A7
- [x] Failure Path ≥ 4 senaryo (duplicate/idempotency, missing, unauthorized, concurrency) + kiracılar arası + SoD + belirsiz sonuç
- [x] Authorization: izin listesi + policy + aktör
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (yol b)
- [x] Veri kapsamı cümlesi (SEC-002 §4) ve kanıt testi (AC-6)
- [x] Para dokuz başlığı (SOP §18.8)
- [x] Sayfa yardım metni anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir

**ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz):**
- [ ] Karar 0.3 (MOD-0003) — sahip; S-1 sapması kabul edilirse sahip + bitiş tarihi yazılı (O-1a)
- [ ] Karar 0.5 (AR-BILLING-BUNDLE) — sahip + EA; S-2 kısıtlı MVP sapması kabul edilirse sahip + bitiş tarihi yazılı (O-1b)
- [ ] Karar 0.6 (ERP-FIN-BUNDLE) — EA; karar 0.7 (GL) — sahip (O-1c, O-1d)
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); yüksek risk — paketi yazmamış, `high: evet` onaylayıcı
- [ ] DCP (UNASSIGNED-finance-treasury-intake) `approved` ve numarası atanmış
- [ ] Sahip **MOD-0120 için** kod alma iznini verdi (D1)
- [ ] Servis iskeleti ve servis ortak yapı taşları MOD-0126 DL-1 ile açıldı (tek sahibi o dilim — gap 4/G5) ve port 5071 kaydı (AGENTS §2/§3/§9, `ports.md`; TRM-03/04)
- [ ] MOD-0149 eki: müşteri doğrulama + dış referans çözümleme + sözleşme belgesi (O-3; commercial-suite sahibi, D9)
- [ ] TRM-14: MOD-0220 `ApprovalStatus` (MOD-0126 ile ortak) — kural D14/4 ile kararlaştırıldı
- [ ] SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (O-14)
- [ ] RCS-001 ortak kod servisi ve `ARI` öneki (O-6)
- [ ] Para birimi küsurat hanesi kaynağı (MOD-0126 O-11 ile ortak)
- [ ] MOD-0023'te AR düzeltme iş akışı şablonu — DL-3 için
- [ ] MOD-0023 pack §17 (TRM-15) **onaylandı** (CT-APR-02/01; yarış yanıtı politikası KARAR D21/4, D22/3) — DL-3 için. **Testlerin geçmesi ready-for-dev engeli değildir** (gap 16/P2): §17 kodu MOD-0023 sahibinin WP'siyle gelir (D25/1) ve DL-3 kabulünden önce main'de olmalıdır; AC-W5…W14 DL-3 uygulama kabul kapısındadır; geçene kadar yinelenen anahtar hatası `Unknown` (D21/7)
- [ ] Düzeltme politikası (O-7) ve muafiyet kararı (O-8 kalan kısmı; K2 kısmı D14/3 ile kapandı) — DL-3 için
- [x] **O-9 — elle tahsilat bu sürümde mi: KARAR (D16)** — v1'de (DL-3, O-9a → D15/1), ödeme referansı iş tekilliği,
  mükerrer tahsilat kontrolü ve onaylı geri alma (karşı kayıt) ile; AC-33, AC-39, AC-40, AC-41
- [ ] Silmeme politikası (KARAR D21/1) MOD-0023 pack §17'de onaylı; silinmişi ayıran okuma gerekmez (D21/2) — DL-3 için; AC-W1 (silmeme mimari testi) DL-3 uygulama kabul kapısındadır (gap 16/P2); kapanmazsa 404 / boş okumada anahtarlı yeniden yazma kapalı kalır (gap 6/P1-a)
- [ ] MOD-0023 okuma eşleme alanları (KARAR D21/3) §17'de onaylı — DL-3 için; kod MOD-0023 sahibinin WP'siyle gelir (D25/1), AC-W2/W3 uygulama kabul kapısındadır (gap 16/P2); yoksa okuma niyeti kapatmaz (gap 6/P1-b)
- [ ] Kaynağın sınırlı denetim/çoklu yazma devri taşınmıyor (onaylandı mı — sahip, Level-1 koşul 1)

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Ortak tanımlar: MOD-0126 pack @bu commit sha256 ZIP manifestine bakın. (Makbuz, niyet durum sözlüğü, Reconcile 3a
  kuralları ve servis ortak portlar orada tanımlıdır; bu pack ile çelişirse o sürüm bağlayıcıdır — gap 6/P2-c.)
- Producer paketi: MOD-0120 v1.0 (TRM-B3-01), manifest sha256 `f92e068a69fc281ddc89cd834758c12703cca3de5b1f18f21aaa2aa8d68f0df9`
  (bu oturumda ölçüldü); paket dosyası `MOD-0120-TRANSFER-PACKET-v1.0.md` sha256 `28102eaf4df558b44794d6e5017ed7e8f7a639a52e2aeefd4fabb14ad5cfa8cd`
  (sahip özet kararı `treasury-b3-digest-MOD-0120-20261022/DECISION.json`, kabul edildi 2026-10-22).
- Taşıma izni: sahip özet kararı `treasury-b3-digest-MOD-0120-20261022` (Level-1 koşul 3) — yalnız paket teslimi; kod
  alma, uygulama, veri taşıma, geçiş izni değildir.
- Kaynak commit: `218acaf5f3d78476bd16b9f4b1e5767d6b41d11f` (S; test edilen AR commit'i `2604202c`, ata). Yalnız bu makinede
  (inceleme REPORT §6.5: `git bundle` önerildi).
- Derleme/test kanıtı (devralınmış): `source-build-evidence-v1.2/docs/records/audits/2026-10/treasury-build-evidence-20261023/SUMMARY-v1.2.md`
  satır 8 — "62/64 (same two files differ) · BUILD ok · TEST pass (at da4abb20, not the exact bound bytes) · 317/317". Farklı
  iki dosya aynı tablonun MOD-0119 satırında adlandırılır: `Api/Controllers/CustomBaseController.cs`,
  `Infrastructure/DependencyInjection.cs`. Recovery hazırlığının ölçüsü değildir.
- Kod alma (D1, kopya değil): her alınan dosya `path@218acaf5` + sha256 ile iz kaydına yazılır; "Taşınmayacaklar" listesi
  alınmaz.

### Level-1 koşulları (`MOD-0120-v1.0.md`) → bu paket

1. Süresiz denetim / çoklu yazma sınırlaması → **taşınmıyor** (Taşınmayacaklar 1, §S'de satır yok, AC-34). Taşınırsa §S
   satırı (sahip + bitiş) zorunlu ve etkilenen yazma akışları level-2'ye geçmez.
2. §D/§L5/§J düzeltme kaydıyla okundu → Dependencies "AR-BILLING-BUNDLE" satırı ("üretici belirsiz", MOD-0169 önde gelen aday).
3. §O taşıma izni → sahip özet kararı (yukarıda).
4. MOD-0149 §O S çalışma ağacı ölçümü → §T madde T-1.
5. Kapılar MOD-0003, AR-BILLING-BUNDLE, ERP-FIN-BUNDLE açık → Dependencies, O-1, Ready-for-dev engelleri.

### Hedef eki (TP-001 v1.5 taslağı §4a — §M, §Q, §S, §T, §U)

**§M — uyum farkı (kaynak → Recovery §6 kuralı):**

| Konu | Kaynak (S@218acaf5) | Recovery kuralı → bu paket |
|---|---|---|
| Yetki | `[Authorize]` + denetleyici içi izin (`AccountsReceivableController.cs:16, 943-990`) | `[HasPermission]` her aksiyonda (AC-22) |
| Kiracı | JWT talebi; ortak `RuntimeContexts.cs:27-32` `X-Tenant-Id` geri düşüşü (AR kullanmıyor, paket §G) | `ITenantContext`; geri düşüş yok |
| Şirket yetkisi | `ar_legal_entity_scope` talebi; boşsa GET 400 | SEC-002 `IDataScopeResolver`; boşsa boş sonuç (AC-6) |
| Şirket uygunluğu | yapılandırmaya bağlı, varsayılan kapalı (paket §G) | MOD-0220 güncel, Active + Approved (AC-2) |
| Müşteri | çağıranın Guid'i; doğrulayıcı koşulsuz kapalı saplama (`DependencyInjection.cs:559`, paket MOD-0149 §B) | MOD-0149 sözleşmesi (AC-3) |
| Denetim | `FailClosedArControlAuditSink` → `false`; geç denetim yok sayılıyor; CAS'tan önce denetim (paket §H, §L.2-3) | CentralAudit yol b, iş verisiyle aynı işlem (AC-18) |
| İşlem | 0 `StartSession` (paket §H) | Mongo işlemi + tek belge |
| Silme | yok | soft delete, yalnız Draft |
| Dil / layout | hardcoded İngilizce, `_LayoutBackbone` | 7 dil, `_LayoutTenantShell` |
| Rota / port | `/api/v1/mod0120`, 5058 | `/api/receivable-invoices`, 5071 |

**§Q — kimlik ve referans eşlemesi** (bu sürümde **kaynak veri taşınmaz**; aşağıdaki eşleme, ileride ayrı kararla veri
taşınırsa ya da içe aktarma O-10 açılırsa kullanılır ve o işin önkoşuludur):

- **CustomerId:** kaynak `ReceivableInvoice.CustomerId` (`AccountsReceivableEntities.cs:14@218acaf5`) çağıranın verdiği,
  ihraççısı olmayan bir Guid'dir; müşteri doğrulayıcısı her çağrıda "çözülmedi" döner (`FailClosedAccountsReceivableReferenceValidators.cs:11-17@218acaf5`).
  Bu Guid Recovery'de **kimlik olarak kullanılmaz**. Eşleme yolu: kaynak kaydın müşteri referansı (`SourceCustomerReference`,
  :17) ve kaynak sistem kodu → Recovery `AccountExternalReference(SourceSystem, ExternalId)` (tekil `(TenantId, SourceSystem,
  ExternalId)`, `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/AccountExternalReference.cs:3-8`) → MOD-0149
  `AccountId`. Çözümleme **yalnız MOD-0149 sözleşmesiyle** (O-3'teki çözümleme ucu); koleksiyon okunmaz. Çözülemeyen satır
  yer tutucu müşteriyle açılmaz, reddedilip istisna listesine yazılır.
- **LegalEntityId:** kaynak Guid → Recovery MOD-0220 kimliği; eşleme tablosu MOD-0220 sahibinden (TBD).
- **Para / tarih:** ISO 4217 aynı; `DateTime` → `DateTimeOffset` UTC; `DateOnly` aynı; küsurat hanesi MOD-0048 (aşan kayıt ret).
- **Durum:** `DRAFT→Draft`, `ISSUED`/`OPEN→Open`, `PARTIALLY_PAID→PartiallyPaid`, `PAID→Paid`, `VOID→Void`; `CANCELLED`,
  `REVERSED` → eşleme yok, kayıt istisna listesine (sahip kararı). `CollectionState` `NOT_DUE/DUE/OVERDUE→None` (yaşlandırma
  türetilir), diğerleri adıyla.
- **Kodlar:** kaynak `InvoiceNumber` → `ExternalInvoiceNumber`; `InvoiceCode` RCS-001'den yeni (RCS-001 §5.11).
- **Uzlaştırma sorgusu:** `(TenantId, LegalEntityId)` başına kaynak fatura sayısı = eşlenen + reddedilen; brüt ve kalan
  toplamları para birimi başına eşit. **Yeniden koşulabilir mi:** evet — eşleme anahtarı `(TenantId, LegalEntityId,
  ExternalInvoiceNumberKey)` tekil, ikinci koşu mükerrer üretmez.

**§S — sapmalar (T32: karar, gerekçe, sahip, bitiş) — hepsi ÖNERİ, karar değil:**

| # | Sapılan karar | Gerekçe | Sahip | Bitiş / gözden geçirme | Durum |
|---|---|---|---|---|---|
| S-1 | Blueprint kapısı MOD-0003 Data Contract Registry | MOD-0003 Recovery'de yok; sözleşme `accounts-receivable.openapi.yaml` olarak dondurulur, MOD-0003 gelince kaydedilir | sahip (K1) | **TBD** (öneri: MOD-0003 kararı ya da en geç 2027-06-30, hangisi önce) | karar 0.3 bekliyor |
| S-2 | Blueprint kapısı AR-BILLING-BUNDLE ("Billing integration surface") | üretici belirsiz; kısıtlı MVP (yerel fatura, faturalama entegrasyonu yok); MOD-0169 sonra bağlanır | sahip (K1) + EA | **TBD** (öneri: MOD-0169 pack'i `ready-for-dev` olduğunda gözden geçirme) | karar 0.5 bekliyor |
| — | Kaynağın 2026-10-12 sınırlı denetim / çoklu yazma devri (paket §L.3: denetim varsayılanı `false`, yok sayılan geç denetim, CAS öncesi başarı, çoklu yazma kısmi kalıcılık; **bitişi yok**) | **taşınmıyor** — sapma satırı açılmaz | — | — | taşınırsa satır + sahip + bitiş zorunlu; etkilenen akış level-2'ye geçmez |

**§T — hedef tarafın doğrulayamadıkları:**

- T-1 (Level-1 koşul 4): kaynak S çalışma ağacı MOD-0120 paketinde okunmadı (paket §N.4); kardeş MOD-0149 paketi §O ölçümü:
  `git status` ölçüm anında S'de **414** kirli girdi; bağlı yollardan `services/DitenErpTreasuryService/src/Diten.ErpTreasuryService.Infrastructure/DependencyInjection.cs`
  çalışma ağacında değiştirilmiş (` M`) — bağlı özet commit blob'udur, commit edilmemiş değişiklik tarif edilmemiştir. MOD-0120
  paketi de aynı dosyayı bağlar (wiring 5, paket §O); kod alınırken commit baytı esas alınır, çalışma ağacı değil.
- T-2: bağlanan baytlar (`218acaf5`) derlenmedi; testler `da4abb20`'de koştu (62/64 dosya eşit). Bu paket kaynak derleme/test
  koşmadı.
- T-3: MOD-0149 uçları bu oturumda çağrılmadı; yalnız kod okundu. MOD-0149 kayıt satırı ile pack durumu çelişkisi (TRM-09)
  burada çözülmedi.
- T-4: `IDataScopeResolver`'ın FinanceTreasury'den nasıl tüketileceği ölçülmedi (O-14).
- T-5: Mongo kısmi indeks ifadelerinin (`$type`, eşitlik) gerçek replika kümesindeki davranışı ölçülmedi (DL-1).
- T-6: MOD-0169 Billing & Invoicing'in kapsamı ve AR integration contract'ı — pack yok, ölçülecek bir şey yok; yalnız kayıt
  satırı ve Blueprint notu (sıra belgesi Amendment 3 §2, devralınmış) okundu.
- T-7: Build, test, derleme, tarayıcı — hiçbiri koşmadı (planlama belgesi).

**§U — tüketici onayı kapanış kaydı** (paket §P, `CONTRACT-STATE.json.consumer_acks`: 13 yuva, hepsi açık, borçlu sahip K1;
Level-1 kaydı "level 3'ten önce hepsi kapanır"):

| Yuva | Tüketici | Recovery tarafında ne değişiyor | Onay / onaylı istisna | Tarih | Onaylayan |
|---|---|---|---|---|---|
| AUTH | MOD-0018 / kimlik | `[HasPermission]`, `finance-treasury.*`, `read-tenant-wide` açık atama | açık | — | sahip (K1) |
| LE | MOD-0220 | `lookup-validation` + `ApprovalStatus` (TRM-14); SEC-002 kapsamı | açık | — | sahip (K1) |
| CUSTOMER | MOD-0149 | doğrulama + dış referans çözümleme eki (O-3) | açık | — | sahip (K1) / commercial-suite |
| COLLECTION-DESTINATION | MOD-0289 | yalnız opak Guid; çözümleme yok | açık | — | sahip (K1) |
| FORECAST | MOD-0125 | besleme **yok** (taşınmadı) | açık — "etkilenmez" kaydı önerilir | — | sahip (K1) |
| AUDIT | MOD-0021 | CentralAudit yol b; kaynak devri taşınmadı | açık | — | sahip (K1) |
| WORKFLOW | MOD-0023 | AR düzeltme şablonu (yeni) | açık | — | sahip (K1) / MOD-0023 sahibi |
| EVIDENCE | MOD-0028 / MOD-0031 | yalnız bağlantı kimliği; manifest/dışa aktarma taşınmadı | açık | — | sahip (K1) |
| REFERENCE-DATA | MOD-0048 | para birimi + küsurat (O-11 ortak) | açık | — | sahip (K1) |
| DATA-CONTRACT | MOD-0003 | S-1 sapması önerisi | açık | — | sahip (K1) |
| BILLING | AR-BILLING-BUNDLE karşılığı (MOD-0169 aday) | S-2 kısıtlı MVP önerisi | açık | — | sahip (K1) + EA |
| FIN-BUNDLE | MOD-0118, MOD-0119 | GL alanı yok (0.7 önerisi); AP ile ortak sözleşme yok | açık | — | sahip (K1) + EA |
| SETTLEMENT | MOD-0269 | tüketim yok; elle tahsilat ve onaylı geri alma (D16) | açık | — | sahip (K1) |

### Taşınmayacaklar (do-not-carry)

1. **2026-10-12 sınırlı denetim / çoklu yazma devri** (paket §L.3, sahip kararı 1 `DECISION.json@200a67ce`; bitiş yok, uzatma
   yok): denetim varsayılanı `false` (`FailClosedAccountsReceivableReferenceValidators.cs:59-65`), içe aktarma son denetim
   sonucunun yok sayılması (`AccountsReceivableImportService.cs:602`), mutabakatta kayıttan önce denetim (`AccountsReceivableReconciliationService.cs:99, 138`
   → `:187`), geçersiz kılma ve dışa aktarma yollarında CAS'tan önce denetim, içe aktarma/projeksiyon/kanıt/devirde çoklu yazma
   kısmi kalıcılık (paket §H, devralınmış S spec §14). **Hiçbiri taşınmaz.** Taşınırsa §S satırı (sahip + bitiş) şart ve
   etkilenen akış level-2'ye geçmez.
2. FailClosed referans doğrulayıcı saplamalarını (müşteri, LE, para birimi, kaynak sistem, tahsilat hesabı, mutabakat olayı —
   `FailClosedAccountsReceivableReferenceValidators.cs:11-57`) uygulama saymak; bağlanmış ama okunmayan doğrulama seçenekleri
   (`AccountsReceivableReferenceValidationOptions.cs`, paket MOD-0149 §B madde 2).
3. Çağıranın verdiği, ihraççısız `CustomerId`; müşteri anlık görüntüsünü uygunluk saymak; serbest string
   `CustomerDisplayProvenance`.
4. Ayrı koleksiyonlarda satır/taksit (`ReceivableLine`, `ReceivableSchedule`) ve işlemsiz çoklu kayıt.
5. `ar_legal_entity_scope` talebini şirket yetkisi saymak; boş kapsamda 400; kapsam dışı yazmada `MOD0120_ABAC_LEGAL_ENTITY_DENIED`.
6. `[Authorize]`-only denetleyici, `LocalAccountsReceivableAuthorizationGuard`, servis kimliği bayrağı.
7. `receivable.*` / `ar.*` anahtarları, `receivable.edit` takma adı, 7 kaynak rol sabiti, `/api/v1/mod0120` rotaları, 5058 portu,
   kaynak koleksiyon adları.
8. `string CreatedBy = "system"`, `DateTime`, `long RecordVersion`, varlıkta istemci `CorrelationId` ve `IdempotencyKey`.
9. GL alanları (`PostingStatus`, `GlPostingReferenceId`, `GlAccountCode`, `CostCenterCode`) — karar 0.7.
10. MOD-0125 projeksiyon alt sistemi (koşu, satır, devir, geçersiz kılma, politika, geçersizleştirme) ve Gateway HTTP istemcisi
    (yapılandırılmış bearer seçeneği dahil, paket §G T20).
11. Kanıt manifesti, yasal tutma, dışa aktarma kaydı ve indirme belirteci alt sistemi (`revoked:{id}` belirteç işareti,
    `AccountsReceivableV041Services.cs:856`).
12. İçe aktarma, ret raporu, kaynak kontrol toplamı mutabakatı (O-10).
13. Kaynak ekran: `_LayoutBackbone`, altı ekranlı mockup, simüle rol seçici, "Mock action / pending backend" diyaloğu, hardcoded
    İngilizce, cookie'den token okuyan `api.js` (paket §I).
14. Kaynak pack durumu ve etiketleri (`status: review`, "frontend-only mockup", kanonik olmayan ad, `owner: ai-orchestrator` —
    paket §A T2/T22).
15. Kaynak test sayılarını (317 AR / 823 Treasury) ve "~90%" etiketini hazırlık ölçüsü saymak (paket §J).

### Tasarım notları

- **Neden MOD-0023 onaylı düzeltme:** alacağı azaltan işlem (iptal, silme) bir hak kaybıdır; domain-config SoD'yi MOD-0023'ten
  miras alır ve "modül kendi SoD kontrolünü icat etmez" der. A1 MOD-0023'ün başlatan kuralıdır; A2 (oluşturan/açan) ve A3–A4
  (eşik) modülün ek kurallarıdır ve çözümleyicide aday dışı bırakılır — MOD-0126 ile aynı yapı.
- **Neden elle tahsilat v1'de (sahip kararı D16):** kaynakta elle tahsilat yok; mutabakat MOD-0269'a bırakılmış. Tahsilat
  olmadan `Paid` hiç oluşmaz ve AR yalnız kayıt defteri olur. Sahip önerilen "sonraki sürüm" yerine "kurallarıyla dahil"i
  seçti: tek aşamalı, K2 elle kayıt + ödeme referansı iş tekilliği + mükerrer kontrol + onaylı geri alma. **Neden geri alma
  ayrı komut değil:** Void / WriteOff'un MOD-0023 onayı, SoD, niyet, belirsiz sonuç ve Reconcile altyapısı aynen gerekir;
  ikinci bir akış aynı riskleri ikinci kez yazardı. **Neden karşı kayıt:** tahsilat kaydı denetim izinin parçasıdır;
  silmek ya da tutarını değiştirmek geçmişi bozar — geri alma yeni bir kayıtla görünür olur.
- **Neden yaşlandırma türetilir:** saklanan `DUE/OVERDUE` bir zamanlayıcı ister ve zamanlayıcı servis kimliği ister (yok).
- `target: 2027-02-15` planlama yer tutucusudur, sahip kararı değildir.

## Follow-up Items

- MOD-0169 Billing & Invoicing bağlantısı (AR-BILLING-BUNDLE karşılığı): faturalamadan gelen fatura sözleşmesi, yasal numara,
  müşteri kimliği bağlama, uyuşmazlık referansları (S-2 bitişi).
- MOD-0003 gelince sözleşme kaydı (S-1 bitişi).
- Kredi notu (Blueprint SoR "credit memos"), kısmi silme, kısmi tahsilat geri alma, tek ödeme referansının birden çok
  faturaya bölünmesi, eşleşmemiş/fazla nakit.
- Toplu içe aktarma + kaynak kontrol toplamı mutabakatı — güvenilir servis kimliği (MOD-0033-FU02) gelince (O-10).
- MOD-0125 projeksiyon girdisi (paketi açılırsa) — FORECAST yuvası.
- MOD-0269 / MOD-0121 mutabakat olayıyla otomatik tahsilat.
- MOD-0289 tahsilat hesabı arama/çözümleme.
- Talep geri çekme (MOD-0023 örnek iptali) ve Görev Merkezi köprüsü (WC-D1).
- CentralAudit reddedilen komut kaydı (G-AUD-1, BL-501).
- Yeniden başlatması engellendiği için `Unknown` kalan düzeltme niyetinin (ve fatura kilidinin) kalıcı kapatılması için
  açık iptal yolu (gap 4/G2) — sahip kararıyla ayrı komut.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır; kayıt satırı (D8) ayrı iş.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1a | Karar 0.3 — MOD-0003 | fonla / ertele (T32 sapması) | **ertele**: S-1, dondurulmuş sözleşme, sahip + bitiş | sahip (K1) |
| O-1b | Karar 0.5 — AR fatura sınırı | üretici ata / kısıtlı MVP (T32) | **kısıtlı MVP**: S-2; MOD-0169 sonra; EA üretici kararı ayrı | sahip (K1) + EA |
| O-1c | Karar 0.6 — ERP-FIN-BUNDLE | arayüz başına sahiplik | EA; AR için yalnız platform kaygıları gerekir | EA |
| O-1d | Karar 0.7 — GL | içeride / dışarıda | **dışarıda** (AR GL alanı taşımaz) | sahip (K1) |
| O-2 | AR'de şirket "Active + Approved" kuralı (D10/1 yalnız ödeyen şirket içindi) | aynı kural / yalnız Active | **KARAR (D14/4): aynı kural, yazma işlemlerinde** — alacak oluşturan/değiştiren her yazma komutu (Reconcile hariç); okumalar etkilenmez | sahip (K1) |
| O-3 | MOD-0149 tüketim yolu | (a) MOD-0149'a eklemeli `GET /api/crm/accounts/{id}/lookup-validation` + `GET …/external-references/resolve` + sözleşme belgesi (D9) (b) mevcut `GET /api/crm/accounts/{id}`, AR kullanıcısına `crm.account.read` | **(a)** — sözleşme-önce; (b) AR kullanıcısına CRM okuma yetkisi verir ve sözleşmesizdir | sahip + commercial-suite sahibi |
| O-4 | Faturalanabilir müşteri durum kodları (MOD-0048 `account-status`) | — | yalnız "aktif" anlamlı kod(lar); liste MOD-0149 sözleşmesinde yayımlanır, AR'de sabit tablo yok | commercial-suite sahibi |
| O-5 | Dış fatura numarası tekilliği ve yasal numara | şirket içinde tekil / serbest | şirket içinde tekil (silinen/Void hariç); yasal numara üretimi faturalamanın | sahip (K1) |
| O-6 | RCS-001 tür öneki ve ortak kod servisi | `ARI` / başka | `ARI` (§3'te çakışma yok, `INV` dolu — ölçüldü); servis BL-565 | sahip + CT (`.antigravity` onayı) |
| O-7 | Düzeltme eşiği | MOD-0126 D11/1 deseni (kiracı ayarı, para birimli, ayar yoksa tier-2) / eşiksiz tek kademe | **D11/1 deseni**, ayrı politika anahtarı | sahip (K1) |
| O-8 | Düzeltme kararları ve tahsilat K2 mı; muafiyet | K2 evet/hayır; muafiyet var/yok | **KARAR (D14/3): K2 evet** — iptal/silme talebi, onay, ret ve elle tahsilat; **muafiyet** (açık): öneri yok (tier-2 aday yoksa 422) | CT + sahip |
| O-9 | Elle tahsilat **bu sürümde mi** | bu sürümde / Follow-up (MOD-0269 / MOD-0121'i bekle) | **KARAR (D16, 2026-10-25): bu sürümde, kurallarıyla** — tek aşamalı, K2 (D14/3), ekleme-yalnız; ödeme referansı iş tekilliği (kiracı + şirket + müşteri + referans), mükerrer tahsilat kontrolü (farklı anahtar / aktör), onaylı geri alma (karşı kayıt, `PaymentReversal`); AC-33, AC-39…41 | sahip (K1) |
| O-9a | Elle tahsilat **hangi dilimde** | ayrı dilim / DL-3 | **KARAR (D15/1): DL-3** — iptal/silme onayıyla birlikte; eski DL-4 DL-3'e katıldı. Bu karar yalnız dilimi belirler; kapsam O-9'da D16 ile kararlaştırıldı | sahip (K1) |
| O-10 | Toplu içe aktarma (AR-16/17) | bu sürüm / servis kimliği gelince | servis kimliği gelince (Follow-up) | sahip (K1) |
| O-11 | Satır vergisi | kullanıcı girdisi / vergi motoru | kullanıcı girdisi (motor faturalamanın) | sahip (K1) |
| O-12 | "Bugün"ün saat dilimi (yaşlandırma, söz tarihi, tahsilat tarihi) | UTC / şirket saat dilimi | UTC (MOD-0126 O-12 ile tutarlı); şirket saat dilimi MOD-0220'de alan olursa yeniden | sahip (K1) |
| O-13 | WriteOff kanıtı zorunlu mu | evet / hayır | evet, en az 1 MOD-0031 bağlantısı | sahip (K1) |
| O-14 | SEC-002 çözücüsünün FinanceTreasury'de tüketim yolu (kapsamın kendisi **KARAR (D14/1)**: `LegalEntityId`, `read-tenant-wide` ayrı izin) | Platform kapsam ucu / servis içi çözücü kaydı | Platform sahibiyle kararlaştırılır; servis ortak tek karar (MOD-0126 O-19, MOD-0119, MOD-0124 ile aynı); `NoOp` kaydı yasak (kapsamı kapatır) | sahip + platform-shared-services |
| O-15 | Rol şablonları | — | MOD-0018 sahibiyle; `read-tenant-wide` hiçbir şablonda varsayılan değil | sahip + MOD-0018 sahibi |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | fatura çekirdeği, müşteri/şirket doğrulaması, veri kapsamı ve denetim | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Backend File Convention, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar |
| DL-2 | açma ve tahsilat aşaması | Validation Rules |
| DL-3 | onaylı düzeltme (iptal / silme / tahsilat geri alma), elle tahsilat (D16), SoD ve belirsiz sonuç | Runtime Constraints, Failure Path to Verify, Authorization Convention, Audited Events |
| DL-4 | kiracı ekranı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events tablosundaki denetim kaydı DL-1'in ilk komutuyla gelir, bölüm DL-3'te K2 sabotaj ve kurtarma testiyle
kapanır; Validation Rules'un fatura alanları DL-1'de, aşama kuralları DL-2'de, SoD kuralları DL-3'te uygulanır). Ortak
bölümler her dilimde aşağıdaki AC kimlikleriyle ölçülür.

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: `Features/ReceivableInvoice/` içinde Create/Update/Delete/BulkDelete + liste/detay/iz sorguları,
  Domain/Persistence, MOD-0149 bağdaştırıcısı (LE, para birimi, kod ve veri kapsamı bağdaştırıcıları servis ortaktır ve
  MOD-0126 DL-1'indir), `accounts-receivable.openapi.yaml`. Önkoşul: Ready-for-dev engelleri (0.3, 0.5, 0.6, 0.7, DCP, kod
  alma, **MOD-0126 DL-1** — iskelet ve servis ortak yapı taşları, tek sahibi o dilim, O-3, TRM-14, O-14, O-6, küsurat).
  Bitiş: AC-1, 2, 3, 5, 6, 17 (Create kısmı), 18 (tek aşamalı), 19, 20, 21, 22, 23, 24, 30, 31, 34, 35 (Create / Update /
  Delete kısmı).
- **DL-2** — Yollar: Issue, SetCollectionStage, yaşlandırma türetme. **Neden ayrı:** fatura kilitlenmesi ve aşama tablosu
  bağımsız incelenebilir; DL-3'ün ön koşulu. Önkoşul: DL-1. Bitiş: AC-4, 7, 8, 28 (arka uç kısmı), 35 (Issue,
  SetCollectionStage kısmı).
- **DL-3** — Yollar: RequestAdjustment, Approve, Reject, Reconcile, düzeltme politikası ve tek eşik, MOD-0023 bağdaştırıcısı
  (servis içinde MOD-0126 ile ortak, zaman aşımlı). **Neden ayrı:** SoD ve belirsiz sonuç modülün en riskli kısmıdır.
  Önkoşul: DL-2, O-7, O-8, O-13, MOD-0023 AR şablonu, MOD-0023 pack §17 (TRM-15) onayı — kod MOD-0023 sahibinin WP'siyle gelir (D25/1); AC-W1/W2/W5…W12 geçişi dilimin **kabul kapısıdır, önkoşulu değil** (gap 16/P2) (KARAR D21/1–4, D22/2–3; gap 5/G2, gap 6/P1-a, P1-b), MOD-0031 davranış ölçümü.
  Bitiş: AC-9…16, 17 (kararlar), 18 (iki aşamalı + sabotaj + kurtarma), 32, 33, 35 (düzeltme, karar, Reconcile,
  tahsilat kısmı), 36, 37, 38, 39, 40, 41, 42. **RecordPayment (elle tahsilat) ve tahsilat geri alma (`PaymentReversal`) da bu
  dilimdedir** (sahip kararları D15/1, 2026-10-24 — ayrı DL-4 hiçbir bölümü kapatmadığı için dilim tablosu okunamıyordu —
  ve D16, 2026-10-25 — elle tahsilat v1'de, kurallarıyla); K2 komutları aynı onay ve denetim altyapısını kullanır.
- **DL-4** — Yollar: `Views/FinanceTreasury/ReceivableInvoices/**`, JS, resx (7 dil), MVC proxy, sayfa yardımı, nav anahtarları;
  Gateway rotası integration-agent task'ı olarak açılır (bu dilim yazmaz). Önkoşul: DL-1 (liste/oluşturma), DL-2/DL-3 (eylem
  düğmeleri), Gateway rotası, O-3 (müşteri seçici). Bitiş: AC-25…29.
