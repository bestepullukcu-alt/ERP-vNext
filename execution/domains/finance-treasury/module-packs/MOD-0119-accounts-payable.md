---
id: MOD-0119
name: Accounts Payable (AP)
domain: finance-treasury
service: Diten.FinanceTreasuryService
service_port: 5071
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
owner: depo sahibi (K1) / Treasury Intake Control Tower
branch: feature/fin/mod-0119-accounts-payable
started: 2026-10-24
target: 2027-01-29
form_field_count: 12
slice: "TRM-S2 — satıcı borcu (AP faturası) kaydı + doğrulama + MOD-0023 onay akışı + SoD + bekletme + istisna + kanıt (içe aktarma, ödeme ve projeksiyon KAPALI)"
---

# MOD-0119 — Accounts Payable (AP)

> **Durum: `draft` (planlama belgesi).** Bu paket kod, servis iskeleti, port kaydı, gateway rotası ya da kaynak
> projeden kod alma izni vermez. Kod ancak paket `approved` / `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02)
> ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten ve sahip MOD-0119 için kod alma iznini verdikten sonra yazılır
> (sahip kararı D1, `docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md`). Engeller:
> "Ready-for-dev Checklist" ve "Açık kararlar".
>
> **Bağlanan kararlar (yeniden karar verilmez):** D1 (doğrula, sonra uyarlayarak al; kod alma modül modül), D2 (servis
> `Diten.FinanceTreasuryService`), D3 (dal kısa kodu `fin`), D5 (MOD-0269 = önce sözleşme, dağıtım kapalı) —
> `treasury-intake-owner-decisions-01.md`; D9 (başka modüldeki eklemeli değişiklik onayı, tek yazar K15), D10/1 (şirket
> `OperationalStatus = Active` **ve** `ApprovalStatus = Approved`), D10/2 (MOD-0220 eki, TRM-14), D10/3 (port 5071),
> D11/4 (izin öneki `finance-treasury.*`), D12/2 (her yazma komutunda zorunlu idempotency anahtarı), D14/1 (veri kapsamı
> `LegalEntityId`, kiracı geneli ayrı `…read-tenant-wide` izni — SEC-002), D14/2 (servis genelinde tek
> `FinanceTreasuryCommandReceipt`, tanımı MOD-0126 pack'inde), D14/3 (AP'de borcu etkileyen kararlar K2), D14/4 (borç
> oluşturan/değiştiren her yazmada şirket Active + Approved) — `treasury-intake-owner-decisions-02.md`. D11/1, D11/2,
> D12/1 ve D13 MOD-0126 içindir; bu pakette **emsal** olarak anılır, AP'ye uygulanması ayrıca "Açık kararlar"da sahibe
> sorulur (D11/3'ün AP karşılığı D14/3 ile kararlaştırıldı). Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0119-v1.1.md` (koşul 1–4 bu pakette karşılanır, bkz.
> "Implementation Notes → Level-1 koşulları"). DCP: `execution/portfolio/delivery-capability-packs/UNASSIGNED-finance-treasury-intake.md`
> (taslak; dilim **TRM-S2**, DCP §8 adım 5: "MOD-0003 kararından sonra").
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-24, bu worktree:**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0119 --name "Accounts Payable (AP)"` → çıkış 0
> (Treasury Intake CT koşturdu). Kayıt dosyasında MOD-0119 satırı henüz yok; satır bu paketle yazılmaz (ayrı iş, D8).
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.1 (`mod0119-transfer-packet-v1.1/`,
> manifest sha256 `f075aeafbdb17ffa3015e6dea248f0bd407bc0db0064ce94df10c37f581c1d31`, bu oturumda `shasum -a 256` ile
> yeniden ölçüldü; paket dosyası `MOD-0119-TRANSFER-PACKET-v1.1.md` sha256 `733db513…88b4`, manifest girdisiyle eşit) ve
> kaynak kod `git -C /Users/natig/Projects/ERP-vNext show 218acaf5f3d78476bd16b9f4b1e5767d6b41d11f:<yol>`. Atıflar
> `paket §X` ya da `Dosya.cs:satır@218acaf5` biçimindedir; Recovery atıfları `yol:satır` (bu worktree, HEAD `3a1c32b9e`).
> **Derleme uyarısı (paket §O):** `218acaf5` kendi başına derlenmeyebilir ve derlenmedi; testler (251/251)
> `da4abb20`'de koştu, bağlı 34 kod dosyasının 32'si eşit, farklı iki dosyada AP satırı değişmemiş — bkz. O-12.

## Module Summary

Satıcı borcunun (AP faturası) sistem-of-record'u: tedarikçi faturası ve alacak dekontunun (credit note) borç kaydı olarak
girilmesi, doğrulanması, MOD-0023 iş akışıyla onaylanması, onay ayrılığı (SoD), bekletme (hold), istisna kaydı ve kanıt
bağlantısı. Blueprint 8.8 W-3, Financial Management (paket §A).

- **Hedef kullanıcı:** AP operatörü (kaydı girer, doğrular, onaya gönderir, istisna açar), AP onaylayıcısı (karar verir),
  AP yöneticisi (bekletme, uzlaştırma).
- **Çekirdek:** borç kaydı tek ve doğru olmalı — mükerrer fatura tekil indeksle engellenir, tutarlar sunucuda satırlardan
  hesaplanır, PO'lu fatura yalnız MOD-0143'ün eşleşme sonucu uygunsa ilerler; kaydı giren, gönderen ve son güncelleyen
  kişi o faturada karar veremez.
- **Kapsam ilkesi:** kaynaktaki 17 HTTP işlemi (AP-01…AP-17, paket §C) tek tek karara bağlanır ("Owned Objects →
  Kaynak işlemleri"); kaynak bir **dış ERP içe aktarma ve kontrol** modülüdür, Recovery v1 ise **elle kayıt + MOD-0143
  referansı** ile başlar (O-4). Kaynak kusurları taşınmaz ("Implementation Notes → Taşınmayacaklar").
- **Ödeme yapılmaz, ödeme partisi beslenmez.** AP → MOD-0126 dikişi (`ApLinked`, MOD-0126 v1'de reddedilir) follow-up'tır.

## Ownership and Boundaries

- **In-scope:** `ApInvoice` + `ApInvoiceLine` (belge türleri: PO'lu fatura, PO'suz fatura, alacak dekontu); doğrulama
  (şirket, tedarikçi ilişkisi, MOD-0143 eşleşme sonucu, tutar ve para birimi kuralları); MOD-0023'e gönderme (yerel niyet
  kaydı + idempotency anahtarı + belirsiz sonuç + uzlaştırma); karar (onay, ret, bilgi isteme) ve SoD; bekletme ve
  bekletmeyi kaldırma; istisna kaydı (aç, çöz, yeniden aç); kanıt bağlantısı (MOD-0031 referansı, yalnız okuma
  doğrulaması); iz (trace) okuma; kiracı ekranı (liste + oluştur + düzenle + detay).
- **Out-of-scope:** fatura yakalama ve 3-yönlü eşleştirme (MOD-0143, procurement — AP sonucu **tüketir**, eşleştirme
  yapmaz); dış ERP içe aktarma, içe aktarma hataları ve kaynak kontrol toplamı mutabakatı (AP-03…06, AP-09, AP-10 — O-4);
  ödeme önerisi adayı ve MOD-0126'ya parti beslemesi (`ApLinked` — follow-up); ödeme/tahsilat durumu olayları (AP-16
  MOD-0126, AP-17 MOD-0269 — üreticileri v1'de yok, D5); nakit çıkışı projeksiyonu (AP-07, AP-12 — MOD-0125 adayı,
  MOD-0124 dikişi follow-up); kanıt paketi manifesti, kilitleme ve dışa aktarma (AP-08, AP-13…15 — follow-up); GL
  kaydı ve fiş referansı (MOD-0118, karar 0.7); tedarikçi, şirket, banka ana kayıtları (MOD-0140, MOD-0220, MOD-0289);
  banka hesabı referansları (MOD-0289 yok — v1'de AP banka referansı **taşımaz**); MOD-0146 Payment Run Support (dışlama
  korunur); vergi motoru (O-15).
- **Sahip olunan sözleşme:** `AP-INVOICE` (yeni, `docs/analysis/contracts/ap-invoice.openapi.yaml`, x-owner MOD-0119) —
  kaynakta resmi sözleşme yok (paket §B "ABSENT"; level-1 koşul 4). Tek yazıcı MOD-0119 (K15). ERP-FIN-BUNDLE ile ilişkisi
  karar 0.6'ya bağlı (O-2).
- **Tüketilen sözleşmeler:** MOD-0220 `lookup-validation` (+ TRM-14 `ApprovalStatus`), Platform veri kapsamı
  (`IDataScopeResolver`, SEC-002 — D14/1), `SUPPLIER`
  (`supplier.openapi.yaml`, 1.3.0 hedefi — `validateSuppliers` şirket ilişkisi durumu), `INVOICE-MATCH`
  (`invoice-match.openapi.yaml` 1.1.0 FROZEN — `getInvoice`), MOD-0023 iş akışı başlatma ve görev geçişleri, MOD-0031 kanıt
  bağlantısı okuma, MOD-0048 para birimi. Başka servisin koleksiyonu doğrudan okunmaz (domain-config "Contract-first").

## Owned Objects

### Varlıklar (kaynak 17 sınıf, `AccountsPayableEntities.cs@218acaf5` → Recovery karşılığı)

| # | Kaynak sınıf | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `ApInvoiceControlRecord` (:5-55) | **uyarlanır** — `ApInvoice : EntityBase` | çekirdek; dış ERP alanları (`SourceSystem`, `SourceVersionHash`, `IntakeFingerprint` …) v1'de yok (O-4) |
| 2 | `ApInvoiceLine` (:57-69) | **uyarlanır** — `ApInvoiceLine : EntityBase` | `PurchaseOrderRef` / `GoodsReceiptRef` taşınmaz: PO/GRN bağı MOD-0143'ündür |
| 3 | `ApException` (:84-102) | **uyarlanır** — `ApInvoiceException : EntityBase` | istisna kuyruğu; `OwnerUserId` Guid; tek aktif kuralı (O-13) |
| 4 | `ApSupplierFinancialProfileSnapshot` (:71-82) | **taşınmaz** | uygunluk kayıt anındaki kopya değil güncel durumdur (TP-001 T14); banka hedef referansı taşır (MOD-0289 yok) |
| 5 | `ApPaymentProposalCandidate` (:104-124), `ApPaymentProposalLine` (:126-142) | **taşınmaz** | MOD-0126 dikişi (follow-up) |
| 6 | `ApImportEvent` (:144-164), `ApImportFailure` (:166-187) | **taşınmaz** | dış ERP içe aktarma v1 dışı (O-4) |
| 7 | `ApExternalEventReceipt` (:189-206) | **taşınmaz** | AP-16/AP-17 üreticileri yok |
| 8 | `ApSourceReconciliationRun` (:208-233), `ApSourceReconciliationLine` (:235-246) | **taşınmaz** | içe aktarma mutabakatı (O-4) |
| 9 | `ApVoucherReference` (:248-258) | **taşınmaz** | GL kapsam dışı (karar 0.7, O-3) |
| 10 | `ApCashOutflowProjectionLine` (:260-297), `ApCashOutflowProjectionBatchRecord` (:369-387), `ApProjectionSourceEpoch` (:389-394) | **taşınmaz** | MOD-0125 yayını yok (paket §F, OI-08) |
| 11 | `ApEvidencePackManifest` (:299-353) | **taşınmaz** | dışa aktarma kaynakta kapalı (`RequestExport` → `BlockedPendingBackendContract`, :340-346); v1'de MOD-0031 bağlantı referansı |
| 12 | `ApPolicyConfig` (:355-367) | **taşınmaz** | politika ayarı v1'de yok (onay kademesi O-7) |
| + | — | **yeni** `ApWorkflowStartIntent` | uzak başlatmadan önce yerel niyet (MOD-0126 deseni) |
| + | — | **yeni** `ApApprovalDecision` | karar niyeti/sonucu; kaynakta `ApApprovalStatus` var ama karar kaydı ve HTTP işlemi yok (paket §C "not HTTP-exposed") |
| + | — | `FinanceTreasuryCommandReceipt` (**servis ortak**, yeni tip açılmaz) | istemci idempotency makbuzu (D12/2, D14/2); tanım MOD-0126 pack Entity Fields |

**Ortak tip kararı — KARAR (D14/2):** makbuz servis genelinde **tek ortak tiptir** (`FinanceTreasuryCommandReceipt`,
koleksiyon `finance_treasury_command_receipts`; tanım MOD-0126 pack Entity Fields — FinanceTreasuryCommandReceipt); AP
önekli makbuz tipi açılmaz. Niyet tipleri AP'ye özgü kalır (`ApWorkflowStartIntent`, `ApApprovalDecision`; koleksiyonlar
`ap_*`) ama MOD-0126'nın tek geçiş matrisindeki durum adlarını kullanır. Şu **servis-ortak portlar** da paylaşılır:
`ILegalEntityEligibilityPort` (MOD-0220), `ICurrencyReferencePort` (MOD-0048), `IRecordCodePort` (RCS-001),
`IFinanceTreasuryDataScopeSource` (SEC-002, D14/1) — MOD-0126 paketindeki adlarla aynı. **Tek sahibi ve yazarı MOD-0126
DL-1'dir** (gap 4/G5; D14/2 ile tutarlı): makbuz tipi + indeksleri, veri kapsamı kaynağı, ortak LE / para birimi / kod
portları ve bağdaştırıcıları ve CentralAudit kaydı orada açılır; MOD-0119 bunları **yeniden tanımlamaz ve açmaz**, AP
DL-1 MOD-0126 DL-1'i bekler. Ortak tipte ya da portta değişiklik dört Treasury paketinin onayını ister.

### Kaynak işlemleri (AP-01…AP-17, paket §C; rota tabanı `api/v{v}/mod0119/accounts-payable`, `AccountsPayableController.cs:18@218acaf5`) → Recovery

| Kaynak | Kaynak rota | Recovery komut / sorgu | Recovery uç (Gateway `/api/…`) | Karar |
|---|---|---|---|---|
| AP-01 | GET invoices | `GetApInvoiceListQuery` | `GET /api/ap-invoices` | **taşınır** (DataTable server); `legalEntityId` sorgu parametresi yerine `X-Legal-Entity-Id` başlığı |
| AP-02 | GET invoices/{id} | `GetApInvoiceByIdQuery` | `GET /api/ap-invoices/{id}` | taşınır |
| AP-03 / AP-04 | GET import-failures[/{id}] | — | — | **v1 dışı** (O-4) |
| AP-05 / AP-06 | GET import-failures/operational[/{id}] | — | — | **v1 dışı** (O-4) |
| AP-07 | GET projections | — | — | **v1 dışı** (MOD-0125/0124 dikişi, follow-up) |
| AP-08 | GET evidence-manifests/{objectId}/versions/{v} | — | — | **v1 dışı** (manifest follow-up) |
| AP-09 | POST invoices/import | `CreateApInvoiceCommand` | `POST /api/ap-invoices` | **uyarlanır**: dış ERP yükü yerine elle kayıt; `TenantId`/`LegalEntityId` gövdeden alınmaz; kaynak "200 + `ValidationRejected`" yanıtı taşınmaz |
| AP-10 | POST reconciliation/source-control-totals | — | — | **v1 dışı** (O-4) |
| AP-11 | POST exceptions | `RaiseApInvoiceExceptionCommand` | `POST /api/ap-invoices/{id}/exceptions` | **uyarlanır**: upsert değil, açma; tür başına tek aktif istisna (O-13) |
| AP-12 | POST projections/build | — | — | **v1 dışı** |
| AP-13 / AP-14 / AP-15 | POST evidence-manifests[/…/lock, /export-requests] | — | — | **v1 dışı**; yerine `LinkApInvoiceEvidenceCommand` |
| AP-16 | POST events/planned-payment-updated | — | — | **v1 dışı** — MOD-0126 v1 `ApLinked` partiyi reddeder |
| AP-17 | POST events/settlement-status-changed | — | — | **v1 dışı** — MOD-0269 dağıtımı kapalı (D5) |

### Recovery ekleri (kaynakta HTTP işlemi yok)

| Komut | Uç | Neden |
|---|---|---|
| `UpdateApInvoiceCommand` | `PUT /api/ap-invoices/{id}` | Compact `Edit`; yalnız `Received`/`Validated`, hiç gönderilmemiş ya da kesin ret/iptal sonrası, açık niyet yok |
| `DeleteApInvoiceCommand` | `DELETE /api/ap-invoices/{id}` | Golden Reference silme seti; yalnız hiç gönderilmemiş kayıt (`SubmissionSequence = 0`), soft delete |
| `BulkDeleteApInvoiceCommand` | `DELETE /api/ap-invoices/bulk` | Golden Reference zorunlu toplu silme; aynı kural |
| `ValidateApInvoiceCommand` | `POST /api/ap-invoices/{id}/validate` | şirket, tedarikçi ilişkisi, MOD-0143 sonucu, tutar kuralları |
| `SubmitApInvoiceCommand` | `POST /api/ap-invoices/{id}/submit` | yerel niyet önce, idempotency anahtarı, belirsiz sonuç |
| `ReconcileApInvoiceWorkflowCommand` | `POST /api/ap-invoices/{id}/workflow/reconcile` | belirsiz başlatma / karar sonucunu MOD-0023'ten okuyup kapatır |
| `ApproveApInvoiceCommand`, `RejectApInvoiceCommand`, `RequestApInvoiceInfoCommand` | `POST /api/ap-invoices/{id}/{approve\|reject\|request-info}` | karar türü başına bir komut; kaynakta yalnız izin sabitleri var (`ap.invoice.approve` / `reject` / `request_evidence`, `AccountsPayableSecurity.cs:36-38@218acaf5`) |
| `PlaceApInvoiceOnHoldCommand`, `ReleaseApInvoiceHoldCommand` | `POST /api/ap-invoices/{id}/hold`, `…/release-hold` | kaynakta `ApHoldStatus` ve `OnHold` durumu var (`AccountsPayableEnums.cs:71-76`, `AccountsPayableStateGuards.cs:15`), komutu yok |
| `ResolveApInvoiceExceptionCommand`, `ReopenApInvoiceExceptionCommand` | `POST /api/ap-invoices/{id}/exceptions/{exceptionId}/{resolve\|reopen}` | kaynak istisna geçişleri (`AccountsPayableStateGuards.cs:21-31`) daraltılarak |
| `LinkApInvoiceEvidenceCommand` | `POST /api/ap-invoices/{id}/evidence-links` | MOD-0031'de var olan bağlantının referansı; uzak yazma yok |

- **Sorgular:** `GetApInvoiceListQuery`, `GetApInvoiceByIdQuery`, `GetApInvoiceTraceQuery`
  (`GET /api/ap-invoices/{id}/trace`), `GetApInvoiceExceptionListQuery` (`GET /api/ap-invoices/{id}/exceptions`).
  Faturalar arası istisna kuyruğu ekranı v1'de yok (Follow-up).
- **DTO'lar:** `ApInvoiceModels.cs` tek dosyada (Golden Reference): `ApInvoiceListItemDto`, `ApInvoiceDetailDto`,
  `ApInvoiceLineDto`, `ApInvoiceWriteRequest`, `ApInvoiceLineWriteRequest`, `ApDecisionRequest`, `ApHoldRequest`,
  `ApInvoiceExceptionDto`, `ApInvoiceExceptionWriteRequest`, `ApWorkflowStateDto`, `ApDecisionDto`, `ApEvidenceLinkDto`,
  `ApInvoiceTraceDto`. DTO ve isteklerde `TenantId` ve `LegalEntityId` **yoktur** (kaynak istek modelinde vardı,
  `AccountsPayableDtos.cs:16-18@218acaf5`).
- **Portlar (Application):** servis-ortak `ILegalEntityEligibilityPort`, `ICurrencyReferencePort`, `IRecordCodePort`,
  `IFinanceTreasuryDataScopeSource` (SEC-002 veri kapsamı, D14/1);
  AP'ye özgü `ISupplierEligibilityPort` (MOD-0140), `IInvoiceMatchPort` (MOD-0143), `IApWorkflowPort` (MOD-0023),
  `IApEvidencePort` (MOD-0031).
- **Frontend rotası:** `/FinanceTreasury/ApInvoices` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`).
- **İzinler:** "Authorization Convention" tablosu.

### Durum makinesi (`ApInvoiceStatus`, kaynak 11 değerden 6'sı — `AccountsPayableEnums.cs:16-29@218acaf5`)

Sayısal değerler kaynakla aynı tutulur (izlenebilirlik); Recovery'ye kaynak veri taşınmadığı için uyumluluk gerekçesi yoktur.

| Durum | Değer | Recovery anlamı | Giriş | Çıkış |
|---|---|---|---|---|
| Received | 0 | kaydedildi, düzenlenebilir | Create, Update | Validate, Update, Delete |
| Validated | 1 | doğrulandı | Validate başarılı; kesin başlatma reddi; Reconcile "iptal" | Update (→ Received), Submit, Hold, Delete (hiç gönderilmemişse) |
| ApprovalPending | 5 | onay akışında (alt durum `ApprovalStatus`'ta) | Submit tx1 | Approve (son adım) → Approved · Reject → Rejected · kesin başlatma reddi / iptal → Validated |
| Approved | 6 | son onay; borç ödemeye uygun (ödeme dikişi follow-up) | Approve (son adım) | Hold |
| Rejected | 7 | ret (terminal) | Reject | — |
| OnHold | 8 | bekletmede | Hold (`Validated` ya da `Approved`'dan) | ReleaseHold → bekletme öncesi durum |

- **Taşınmayan değerler:** `MatchPending` (2), `Matched` (3) — eşleştirme durumu MOD-0143'ündür, AP yalnız sonucu
  okur; `Exception` (4) — istisna ayrı kayıttır, engelleyici açık istisna geçişi durdurur; `PaymentProposed` (9),
  `Closed` (10) — ödeme dikişi follow-up. UI yalnız taşınan 6 değeri gösterir.
- **`ApprovalStatus` (kaynak 8 değer, `AccountsPayableEnums.cs:42-52`) Recovery'de:** `DraftRequest` (gönderilmedi),
  `Submitted` (**yerel başlatma niyeti yazıldı; MOD-0023 sonucu henüz kesin değil**), `PendingDecision` (örnek başladı,
  karar bekleniyor; çok adımlı akışta son adıma kadar), `InfoRequested` (bilgi istendi), `Approved`, `Rejected`.
  **Taşınmaz:** `NotRequired` (v1'de her belge türü onay ister, O-14), `Delegated` (devretme MOD-0023'ündür, AP durumu
  değildir).
- `ApprovalPending` sırasında `Hold` 409 (iş akışı açıkken bekletme v1'de yok); `Approved` kayıt bekletmeye alınabilir.
- **Niyet ve karar durumları — tek geçiş matrisi (MOD-0126 ile aynı sözlük; servis geneli durum sözlüğü, D14/2):** açık durumlar `Pending` (başlatma) /
  `Requested` (karar) ve `Unknown`; kapalı durumlar `Started` (başlatma) / `Recorded` (karar), `Refused`, `Cancelled`.

| Niyet | Açık → | tx2 (kesin sonuç) | belirsiz | Reconcile (yalnız `ReconcileApInvoiceWorkflowCommand`) | Sonuç olayı |
|---|---|---|---|---|---|
| `ApWorkflowStartIntent.Status` | `Pending` | `Started` · `Refused` | `Unknown` | `Unknown`/`Pending` → `Started` · `Refused` · `Cancelled` | `…submitted` · `…workflow-start-refused` · `…workflow-start-cancelled` |
| `ApApprovalDecision.State` | `Requested` | `Recorded` · `Refused` | `Unknown` | `Unknown`/`Requested` → `Recorded` · `Refused` · `Cancelled` | `…approved`/`…rejected`/`…info-requested` · `…decision-refused` · `…decision-cancelled` |

  `Refused` = MOD-0023 kesin reddetti; `Cancelled` = MOD-0023 örneği ya da görevi iptal edilmiş / kapanmış bulundu
  (yalnız Reconcile atar, yalnız uzak tarafın **kesin** iptal/kapanış kaydıyla — o örnek için MOD-0023 iptal geçiş
  kaydı ya da `CloseAbandonedWorkflowStart` kapanışı; bulunamadı / 404 / zaman aşımı / belirsiz okuma `Cancelled`
  sayılmaz, niyet `Unknown` kalır — Runtime Constraints 3a). Başlatma `Cancelled` → fatura `Validated`,
  `ApprovalStatus = DraftRequest`; yeniden gönderme yeni `SubmissionSequence` alır. MOD-0126'dan farkı: AP faturası iş akışı
  iptalinde terminal olmaz.

## Entity Fields

Taban: servisin kendi `EntityBase`'i (`entity-base-template.md`): `Id`, `TenantId`, `IsDeleted`, `DeletedAt`,
`CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş modülü olduğu için `CreatedBy` / `UpdatedBy` (`Guid?`, token
`sub`'ından; DTO'dan değil) **zorunlu**. Kaynak tabanı `TenantEntity` `DateTime` + `long RowVersion` + `string
CreatedBy` taşır (paket §H; `MongoAccountsPayableRepository.cs:73-74@218acaf5` `RowVersion == expectedVersion`);
Recovery'de `DateTimeOffset` (UTC), `int Version` ve `Guid` aktör kimliği. Kaynakta silme yok: `IsDeleted = true` yazan
yol ve `HttpDelete` yok (paket §F); Recovery'de soft delete `IsDeleted` + `DeletedAt` (UTC) birlikte yazılır.

**Kapsam alanları (her varlıkta):** `TenantId` (`ITenantContext`) ve `LegalEntityId` (isteğin `X-Legal-Entity-Id`
başlığı, MOD-0220'den doğrulanmış; Recovery R-2 deseni, örn. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Loads/LoadContextMiddleware.cs:61-81`)
sunucuda yazılır; gövde ya da sorgu parametresiyle gelen `tenantId` / `legalEntityId` 400 ile reddedilir (kaynakta gövde
kiracısı bağlamla karşılaştırılıyordu, `AccountsPayableController.cs:221@218acaf5` — taşınmaz). Alt kayıtlar (satır,
istisna, niyet, karar, makbuz) faturanın `LegalEntityId`'sini taşır.

**Para (SOP §18.8, domain-config "Para / decimal"):** tüm tutarlar `decimal` (Mongo `Decimal128`); `float`/`double` yasak.
Para birimi **fatura başına tek**: ISO 4217, 3 büyük harf; her satır faturanın para birimini taşır ve farklıysa reddedilir
(kaynak kuralı, `AccountsPayableFoundationValidator.cs:77-85@218acaf5`). Hassasiyet: para biriminin küsurat hanesi
MOD-0048'den (kaynak **TBD**, O-10); fazla haneli tutar **yuvarlanmaz, reddedilir**. Tutarlar **negatif olmaz**; alacak
dekontunun yönü belge türünden gelir (kaynak kuralı, `AccountsPayableFoundationValidator.cs:42-52`; işaretli tutar
`DocumentDirection`'dan hesaplanır, `AccountsPayableEntities.cs:293-296`). `NetAmount`, `TaxAmount`, `GrossAmount`,
`OpenAmount` sunucu hesaplar (kaynakta istemciden ayrı ayrı alınıyor ve birbirine karşı kontrol edilmiyordu,
`AccountsPayableDtos.cs:28-38` / `AccountsPayableFoundationValidator.cs:18-89` — taşınmaz). Kur çevrimi yok (FX tarihi
uygulanmaz). Saat dilimi: zaman damgaları UTC; `InvoiceDate` ve `DueDate` yalnız tarihtir (`DateOnly`), saat dilimi
çevrimi yapılmaz; "bugün" UTC tarihidir (O-19).

### ApInvoice (`ap_invoices`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| InvoiceCode | string | RCS-001 OTOMATİK `{ŞİRKET}-{TÜR}-{SAYAÇ}`; tür öneki önerisi `APD` (O-9); kullanıcı giremez/değiştiremez; silinen kaydın kodu yeniden kullanılmaz | kaynakta iç kod yok |
| DocumentType | enum `PoBackedInvoice`=0, `NonPoInvoice`=1, `CreditNote`=2 | zorunlu | `AccountsPayableEnums.cs:3-8@218acaf5` |
| DocumentDirection | enum `PayableOutflow`=0, `CreditAdjustment`=1 | sunucu: `CreditNote` → `CreditAdjustment`, diğerleri `PayableOutflow`; istekten alınmaz | `:10-14` (kaynakta istekten alınıyordu) |
| SupplierId | string | zorunlu; MOD-0140 `supplierId` (sözleşmede `type: string`, örn. `SUP-2001` — `supplier.openapi.yaml` `Supplier.supplierId`); çağıran şirketle **Active** ilişki | kaynak `Guid SupplierReferenceId` (`AccountsPayableEntities.cs:14`) — tip sözleşmeye uyarlanır |
| SupplierNameSnapshot | string | **yalnız görüntü** için; sunucu MOD-0140 yanıtından yazar; uygunluk kararında kullanılmaz | `SupplierDisplayNameSnapshot` (:15; kaynakta istemciden) |
| InvoiceNumber | string | zorunlu, trim, 1..64 (öneri; kaynakta sınır yok) — tedarikçinin fatura numarası | :18 |
| InvoiceNumberNormalized | string | sunucu: trim + büyük harf + iç boşluklar tekleştirilmiş; mükerrer anahtarı (O-6) | yeni |
| InvoiceDate / DueDate | DateOnly / DateOnly | zorunlu; `DueDate ≥ InvoiceDate` (`AccountsPayableFoundationValidator.cs:35-40`); ileri tarih kuralı O-19 | :19-20 |
| CurrencyCode | string(3) | zorunlu, ISO 4217, upper, MOD-0048 listesinde | :22 |
| NetAmount / TaxAmount / GrossAmount | decimal | sunucu: satır toplamları; `Gross = Net + Tax`; ≥ 0 | :23-25 |
| OpenAmount | decimal | sunucu: v1'de `GrossAmount − CreditedAmount` (ödeme dikişi yok); işaret `DocumentDirection`'dan; bağlı alacak dekontu **yalnız onaylandığında** (Approve son adım tx2 ya da onu kaydeden Reconcile) azaltır, oluşturma ya da gönderimde azaltmaz (Runtime Constraints → Alacak dekontu kapasitesi) | :26 |
| CreditReservedAmount / CreditedAmount | decimal / decimal | yalnız orijinal (`PoBackedInvoice` / `NonPoInvoice`) faturada; sunucu: bekleyen (onay akışındaki) ve onaylı bağlı alacak dekontlarının `GrossAmount` toplamı; `CreditedAmount + CreditReservedAmount ≤ GrossAmount` her commit'te değişmezdir; yalnız orijinal faturanın `Version` CAS'ıyla değişir | yeni |
| CreditStatus | enum `None` / `PartiallyCredited` / `FullyCredited` | sunucu: `CreditedAmount = 0` → None; `0 < CreditedAmount < GrossAmount` → PartiallyCredited; `CreditedAmount = GrossAmount` → FullyCredited (`OpenAmount = 0`); `InvoiceStatus` değişmez (`Approved` kalır) | yeni |
| MatchInvoiceRef | string? | `PoBackedInvoice` ise **zorunlu**, diğerlerinde yasak; MOD-0143 `invoiceId`; kiracı + şirkette tekil (bir MOD-0143 faturası tek AP kaydına bağlanır) | yeni (kaynakta PO ref satırdaydı, :62-63) |
| MatchOutcomeSnapshot | { Status, TotalAmount, CurrencyCode, ReadAt } ? | sunucu, Validate/Submit/Approve'da MOD-0143'ten okunan değer — **iz için**; uygunluk her seferinde yeniden okunur | yeni |
| CreditedApInvoiceId | Guid? | `CreditNote` ise zorunlu (öneri, O-14); aynı kiracı + şirket + tedarikçi + para birimi, durumu `Approved`, kendisi alacak dekontu değil; dekontun `GrossAmount`'u orijinalin kalan kapasitesini aşamaz (Runtime Constraints → Alacak dekontu kapasitesi) | yeni |
| InvoiceStatus / ApprovalStatus | enum / enum | durum makinesi | :34, :37 |
| HoldStatus / HoldReasonCode / StatusBeforeHold | enum `None`/`OnHold` / string? / `ApInvoiceStatus`? | Hold yazar, ReleaseHold temizler; `Released` değeri taşınmaz (bekletme kaldırılınca `None`) | :39 (`ApHoldStatus` :71-76) |
| EvidenceLinks | `ApEvidenceLinkReference[]` | MOD-0031 bağlantı kimliği + nesne referansı; ikili veri yok | yeni |
| Notes | string? | trim, ≤ 2000 (öneri) | — |
| SubmissionSequence | int | kaçıncı gönderim; 0 = hiç gönderilmedi | yeni |
| SubmittedBy / SubmittedAt | Guid? / DateTimeOffset? | sunucu (tx1) | — |
| UpdatedByBeforeSubmit | Guid? | gönderme anındaki son güncelleyen (SoD R3) | yeni |
| WorkflowInstanceId / WorkflowStatus / WorkflowCorrelationId | Guid? / string? / string? | yalnız kesin başlatma sonucundan | yeni |
| CreatedBy / UpdatedBy | Guid? | token `sub` | — |

**Taşınmayan kaynak alanları:** `IntakeFingerprint*`, `IntakeCompleted`, `ExpectedIntakeChildCount`, `IntakeImportEventId`,
`SourceSystem`, `SourceInstance`, `ExternalInvoiceRef`, `SourceVersionHash`, `SourcePayloadHash`, `SourceFreshness`,
`ReconciliationStatus` (içe aktarma, O-4); `IdempotencyKey` (`:47` — istemci anahtarı yalnız makbuzda); `CausationId`
(`CorrelationId` başlıktan, denetimde); `PaidAmount`, `RemainingAmount`, `CashPayableAmount`, `PaymentStatus`,
`PlannedPaymentDate` (ödeme dikişi); `DiscountAmount`, `WithholdingAmount`, `RetentionAmount`, `FeeAmount` (O-16);
`MatchStatus` (MOD-0143'ündür; yerine `MatchOutcomeSnapshot`); `EvidenceStatus` (yerine bağlantı listesi); `PayerBankAccountReferenceId`,
`MaskedPayerBankAccountRef`, `PayeeBankAccountReferenceId`, `MaskedPayeeBankAccountRef` (MOD-0289 yok; banka verisi bu
domain'de saklanmaz); `VoucherReference` (O-3); `SchemaVersion` sabitleri (`mod0119.*.v1` — sözleşme sürümü yerine geçer).

### ApInvoiceLine (`ap_invoice_lines`)

| Alan | Tip | Kural |
|---|---|---|
| ApInvoiceId, LineNumber | Guid, int | sunucu; 1..200 satır (öneri; kaynakta üst sınır yok, en az 1 kuralı `AccountsPayableFoundationValidator.cs:70-75@218acaf5`) |
| Description | string | zorunlu, trim, 1..256 (öneri) |
| NetAmount | decimal | ≥ 0; küsurat ≤ para birimi hanesi |
| TaxAmount | decimal | ≥ 0; küsurat ≤ para birimi hanesi; vergi kodu/oranı yok (O-15) |
| GrossAmount | decimal | sunucu: `Net + Tax` |
| CurrencyCode | string(3) | sunucu faturadan yazar; istekte farklıysa ret |

Güncellemede satırlar bütün olarak değiştirilir (aynı Mongo işleminde başlık + satırlar + makbuz + denetim).

> **`form_field_count: 12` → `golden_reference: compact`.** Oluştur/düzenle formunda kullanıcının doldurduğu alanlar:
> **başlık 9** — DocumentType, SupplierId, InvoiceNumber, InvoiceDate, DueDate, CurrencyCode, MatchInvoiceRef (koşullu),
> CreditedApInvoiceId (koşullu), Notes; **satır 3** — Description, NetAmount, TaxAmount. **Sayılmayanlar:** InvoiceCode
> (RCS-001 OTOMATİK), DocumentDirection (sunucu), SupplierNameSnapshot / MatchOutcomeSnapshot (sunucu), LegalEntityId
> (oturum şirketi, `X-Legal-Entity-Id`), satır LineNumber / CurrencyCode / GrossAmount (sunucu), başlık tutarları (sunucu),
> Idempotency-Key (başlık; yalnız `FinanceTreasuryCommandReceipt`'te saklanır) / CorrelationId (başlık), durum alanları. Kaynak istek
> modelindeki 32 alan (`ApInvoiceImportRequest`, `AccountsPayableDtos.cs:16-52@218acaf5`) bir dış ERP yüküdür, form değildir;
> kaynak ekran (`Views/ERP/Finance/AccountsPayable/Index.cshtml@218acaf5`) form içermiyor (paket §I). 12 > 8 → Compact
> (ayrı Create / Edit / Details).

**Banka verisi yasağı:** hiçbir varlık IBAN, hesap numarası, BIC/SWIFT, banka adı, maskelenmiş hesap ya da banka hesabı
referansı tutmaz (v1). Ödeme dikişi açıldığında banka referansı yalnız opak Guid olarak ve MOD-0289 sözleşmesiyle gelir
(Follow-up).

### ApInvoiceException (`ap_invoice_exceptions`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| ApInvoiceId | Guid | — | `RelatedObjectId` / `RelatedObjectType` (:87-88) — v1'de yalnız AP faturası |
| ExceptionType | enum | v1 türleri: `MissingSupplierProfile`, `KycHold`, `PoMismatch`, `GrnMismatch`, `DuplicateInvoice`, `MissingEvidence`, `ApprovalOverdue` (elle açılır) | `AccountsPayableEnums.cs:94-112` |
| Severity | enum Low/Medium/High/Critical | varsayılan Medium | :114-120 |
| Status | enum `Open`=0, `Resolved`=4, `Reopened`=6 | durum makinesi aşağıda | :122-131 |
| IsBlocking | bool | true ise açık iken Submit / Approve / ReleaseHold 409 `AP_BLOCKING_EXCEPTION_OPEN` | :94 |
| OwnerUserId | Guid? | kaynakta `string?` (:95) | — |
| DueDate | DateOnly? | — | :96 |
| ReasonCode / ResolutionCode | string / string? | resx anahtarı ya da kod; serbest metin yorum ≤ 2000 ayrı alan | `RecommendedAction` / `ResolutionNote` (:97-98) yerine |
| RaisedBy / ResolvedBy / ResolvedAt | Guid / Guid? / DateTimeOffset? | sunucu | yeni |

- **Taşınmayan istisna türleri:** `InvalidBankBinding`, `SupplierBankRecentlyChanged` (MOD-0289 yok), `FundingGap`
  (MOD-0124/0125), `SourceReconciliationMismatch`, `StaleErpSource`, `ImportFailure`, `ContractFailure` (içe aktarma, O-4),
  `PaymentExecutionFailure`, `PartialSettlementReview` (ödeme dikişi). `Amount` / `CurrencyCode` (:92-93) istisnada
  tutulmaz (faturanın tutarı tek kaynaktır). `EvidenceManifestRef` (:99) taşınmaz.
- **İstisna durum makinesi (kaynak 7 durumdan 3'ü, `AccountsPayableStateGuards.cs:21-31@218acaf5`):** `Open` → Resolve →
  `Resolved` → Reopen → `Reopened` → Resolve → `Resolved`. `Triaged`, `Assigned`, `RemediationPending`, `Closed`
  taşınmaz (atama ve kapanış iş akışı v1'de yok; Follow-up).
- **Tek aktif kuralı (O-13, OI-09):** `(TenantId, ApInvoiceId, ExceptionType)` için `Status ∈ {Open, Reopened}` olan en
  fazla bir kayıt — tekil kısmi indeks; ikinci açma 409 `AP_EXCEPTION_ALREADY_ACTIVE`. Kaynakta bu kural yoktu
  (etkin istisna indeksi tekil değil, `AccountsPayableMongoIndexes.cs:142-150`; kaynak sahip kararı S-OD-2 "nonunique
  korunur" — kaynak tarafı içindir, Recovery'yi bağlamaz).

### ApWorkflowStartIntent (`ap_workflow_start_intents`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| ApInvoiceId | Guid | — |
| IdempotencyKey | string | **uzak** (MOD-0023) anahtar, deterministik: `mod0119:{invoiceId:N}:start:{SubmissionSequence}`; aynı niyet için hiç değişmez |
| SubmissionSequence | int | faturanın kaçıncı gönderimi (kesin ret ya da iptal sonrası yeniden gönderme yeni sıra alır) |
| Status | enum Pending/Started/Refused/Cancelled/Unknown | tek geçiş matrisi |
| PreviousInvoiceStatus | `ApInvoiceStatus` | kesin ret / iptal sonrası dönüş (`Validated`) |
| CandidatePrincipalIds / ExcludedPrincipalIds | Guid[] | çözümleyici sonucu (SoD R1–R3 dışlaması) |
| WorkflowInstanceId | Guid? | yalnız kesin sonuçtan |
| LastOutcomeCode / LastAttemptAt / AttemptCount | string? / DateTimeOffset? / int | `AP_WORKFLOW_START_*` kodları |
| CorrelationId | string | — |

### ApApprovalDecision (`ap_approval_decisions`, yeni)

`Decision` (Approve/Reject/RequestInfo), `ReasonCode`, `Comment` (≤ 2000), `DecidedBy` (**Guid**), `DecidedAt`,
`RemoteIdempotencyKey` (MOD-0023 görev geçişine giden **uzak** anahtar, deterministik `mod0119:{decisionId:N}:transition` —
istemci anahtarı değildir), `RequestFingerprintVersion`, `RequestFingerprint`, `WorkflowInstanceId`, `WorkflowTaskId`,
`WorkflowTransitionId`, `WorkflowIsFinalStep`, `SodResult` (`Passed`, `FailedSelfApproval`, `FailedSubmitterApproval`,
`FailedUpdaterApproval`, `FailedRoleConflict`), `SodCheckDetail` (resx anahtarı + parametre), `State`
(Requested/Recorded/Refused/Cancelled/Unknown — tek geçiş matrisi). Muafiyet alanı **yoktur** (R7).

### Makbuz — `FinanceTreasuryCommandReceipt` (servis ortak, D14/2)

Tip, alanlar, altı alanlı kapsam, `Pending`/`Completed` yaşam döngüsü, değişmezlik (`Completed` makbuz bir daha
değişmez), "tekrar asla kurtarma yapmaz" ve "Reconcile yalnız `Pending` makbuzu `Completed` yapar" kuralları **tek
yerde** tanımlıdır: **MOD-0126 pack Entity Fields — FinanceTreasuryCommandReceipt** (koleksiyon
`finance_treasury_command_receipts`). Burada yeniden tanımlanmaz; AP'ye özgü olanlar yalnız şunlardır:

- **`Operation`:** `mod0119:{Komut}` — ör. `mod0119:CreateApInvoice`, `mod0119:SubmitApInvoice`, `mod0119:ApproveApInvoice`.
- **`TargetId`:** Create ve BulkDelete için `null`, diğerlerinde fatura kimliği.
- **İki aşamalı komutlar** (tx1'de `Pending`): Submit ve kararlar (Approve, Reject, RequestInfo); `LinkedIntentId`
  `ApWorkflowStartIntent` ya da `ApApprovalDecision` kimliğidir. Diğer bütün yazma komutları tek aşamalıdır.
- Belirsiz sonuçta kayıtlı 202 yanıtının gösterdiği gerçek sonuç niyette (`ApWorkflowStartIntent` / `ApApprovalDecision`)
  izlenir ve `GET /api/ap-invoices/{id}` ile `…/trace` üzerinden okunur.

**İstemci anahtarı yalnız makbuzdadır:** `ApInvoice`, `ApInvoiceException`, `ApApprovalDecision` istemci anahtarı alanı
taşımaz. MOD-0023'e giden anahtarlar ayrı ve **uzak** anahtardır (`ApWorkflowStartIntent.IdempotencyKey`,
`ApApprovalDecision.RemoteIdempotencyKey`); deterministiktir, istemciden bağımsızdır.

### Mongo indeksleri (kaynak 26 ad, `AccountsPayableMongoIndexes.cs:10-35@218acaf5`)

| Recovery indeksi | Anahtar | Tekil | Kaynak karşılığı |
|---|---|---|---|
| `ux_ap_invoice_code` | TenantId + InvoiceCode | **evet, kısmi filtre YOK** (silinen kaydın kodu da dolu sayılır, RCS-001 §6) | yok |
| `ux_ap_invoice_business_key` | TenantId + LegalEntityId + SupplierId + DocumentType + InvoiceNumberNormalized, kısmi `IsDeleted = false` | **evet** — mükerrer fatura (O-6) | `ix_mod0119_invoice_business_signature` (:70-79) **tekil değil**, mükerrer kontrolü kodda (`AccountsPayableFoundationService.cs:465-481`) — yarışa açık, taşınmaz |
| `ux_ap_invoice_match_ref` | TenantId + LegalEntityId + MatchInvoiceRef, kısmi `MatchInvoiceRef` var **ve** `IsDeleted = false` | evet — bir MOD-0143 faturası tek AP kaydı | yeni |
| `ux_ap_start_intent_remote_key` | TenantId + IdempotencyKey (uzak anahtar) | evet — MOD-0023'e aynı anahtarla tek başlatma | yeni |
| `ux_ap_start_intent_open` | TenantId + ApInvoiceId, kısmi `Status ∈ {Pending, Unknown}` | evet — faturada tek açık başlatma niyeti | yeni |
| `ux_ap_decision_open` | TenantId + ApInvoiceId, kısmi `State ∈ {Requested, Unknown}` | evet — faturada tek açık karar niyeti | yeni |
| `ux_ap_exception_active` | TenantId + ApInvoiceId + ExceptionType, kısmi `Status ∈ {Open, Reopened}` | evet — tek aktif istisna (O-13) | `ix_mod0119_active_exception_lookup` (tekil değil) |
| `ix_ap_invoice_list` | TenantId + LegalEntityId + IsDeleted + InvoiceStatus + DueDate | — | `ix_mod0119_invoice_le_due` / `_status` |
| `ix_ap_invoice_supplier` | TenantId + LegalEntityId + SupplierId | — | `ix_mod0119_invoice_supplier` |
| `ix_ap_invoice_workflow_instance` | TenantId + WorkflowInstanceId (kısmi, var olan) | — | yeni |
| `ix_ap_invoice_line_invoice` | TenantId + ApInvoiceId + LineNumber | — | yeni |
| `ix_ap_decision_invoice` | TenantId + ApInvoiceId + DecidedAt | — | yeni |
| `ix_ap_exception_invoice` | TenantId + LegalEntityId + ApInvoiceId + Status | — | yeni |

**Makbuz indeksleri burada bildirilmez (D14/2):** `ux_finance_treasury_command_receipt_scope` (altı alanlı tekil kapsam;
kaynağın `ux_mod0119_*_idempotency*` indekslerinin yerine geçer, o indeksler taşınmaz) ve
`ix_finance_treasury_command_receipt_pending` servis ortak `finance_treasury_command_receipts` koleksiyonundadır ve sahibi
MOD-0126 DL-1'dir (MOD-0126 pack Entity Fields → Mongo indeksleri); MOD-0119 yeniden bildirmez.

İndeks kurulumu servisin indeks barındırılan hizmetiyle yapılır (`mongo-indexing`); **taşıma/geri doldurma yok**: kaynak
veri taşınmaz (level-1 koşul 1; paket §J.1 — "production-live" raporları yerel geliştirme yığınında, gerçek kullanıcı ve
veri yok; GO-NOGO :114 "No schema or data was added or altered by MOD-0119 commits").

### Hata sözlüğü (kaynak 40 kod, `AccountsPayableErrorCodes.cs:5-44@218acaf5`; `MOD0119_*` ve `AP_*` birlikte — Recovery tek `AP_` öneki)

- **Taşınan (yeniden adlandırılarak):** `MOD0119_VALIDATION_FAILED` → `AP_VALIDATION_FAILED` 400;
  `MOD0119_IDEMPOTENCY_CONFLICT` → `AP_IDEMPOTENCY_CONFLICT` 409; `MOD0119_DUPLICATE_SOURCE_INVOICE_CONFLICT` /
  `AP_IMPORT_DUPLICATE_CONFLICT` → `AP_INVOICE_DUPLICATE` 409; `MOD0119_NOT_FOUND` → `AP_INVOICE_NOT_FOUND` 404;
  `MOD0119_INVALID_STATE_TRANSITION` → `AP_INVOICE_STATUS_INVALID` 409; `AP_SOD_BLOCKED` → `AP_SOD_DENIED` 403;
  `AP_RBAC_PERMISSION_MISSING` / `AP_PERMISSION_OR_SCOPE_DENIED` → `AP_PERMISSION_DENIED` 403.
- **Yeni:** `AP_INVOICE_VERSION_CONFLICT` 409, `AP_IDEMPOTENCY_KEY_REQUIRED` 400, `AP_REQUEST_IN_PROGRESS` 202,
  `AP_LEGAL_ENTITY_NOT_ELIGIBLE` 422, `AP_LEGAL_ENTITY_UNAVAILABLE` 503, `AP_SUPPLIER_NOT_ELIGIBLE` 422 (ilişki yok ya da
  Active değil), `AP_SUPPLIER_UNAVAILABLE` 503, `AP_MATCH_REFERENCE_REQUIRED` 400, `AP_MATCH_REFERENCE_NOT_ALLOWED` 400,
  `AP_MATCH_OUTCOME_NOT_ELIGIBLE` 422, `AP_MATCH_REFERENCE_MISMATCH` 422 (tedarikçi / para birimi / tutar farkı),
  `AP_MATCH_REFERENCE_ALREADY_USED` 409, `AP_MATCH_UNAVAILABLE` 503 (`AP_MATCH_AMOUNT_BASIS_UNCONFIRMED` D26 ile kaldırıldı —
  taban net), `AP_CREDITED_INVOICE_INVALID` 422,
  `AP_CURRENCY_PRECISION_UNAVAILABLE` 503, `AP_RECORD_CODE_UNAVAILABLE` 503, `AP_WORKFLOW_START_FAILED` 502 (yalnız kesin
  ret), `AP_WORKFLOW_START_OUTCOME_UNKNOWN` 202, `AP_WORKFLOW_START_PENDING` 409, `AP_DECISION_OUTCOME_UNKNOWN` 202,
  `AP_DECISION_PENDING` 409, `AP_DECISION_REASON_REQUIRED` 400, `AP_WORKFLOW_TASK_INVALID` 400,
  `AP_WORKFLOW_TRANSITION_FAILED` 502, `AP_INVOICE_ON_HOLD` 409, `AP_BLOCKING_EXCEPTION_OPEN` 409,
  `AP_EXCEPTION_ALREADY_ACTIVE` 409, `AP_EXCEPTION_STATUS_INVALID` 409, `AP_EVIDENCE_LINK_INVALID` 422,
  `AP_EVIDENCE_UNAVAILABLE` 503, `AP_EVIDENCE_REQUIRED` 422 (O-17), `AP_CREDIT_NOTE_EXCEEDS_CAPACITY` 422 (bağlı alacak
  dekontu orijinalin kalan kapasitesini aşıyor; tam alacaklanmış orijinal dahil), `AP_RECONCILE_RESTART_ACTOR_MISMATCH` 409
  (Reconcile'ı çağıran asıl komutun aktörü değil; uzak yazma/başlatma çağrısı yok, niyet `Unknown` kalır).
- **Taşınmaz:** `MOD0119_TENANT_CONTEXT_MISSING`, `MOD0119_TENANT_MISMATCH` (gövdede kiracı yok; bağlam yoksa standart
  401/400), `MOD0119_LEGAL_ENTITY_ACCESS_DENIED` / `AP_ABAC_LEGAL_ENTITY_DENIED` (başka şirket → 404), `MOD0119_LEGAL_ENTITY_ABAC_NOT_WIRED`,
  `MOD0119_SOD_RUNTIME_PENDING` (kaynak yer tutucuları, `AccountsPayableSecurity.cs:284-294, 328-338`),
  `MOD0119_EVIDENCE_MANIFEST_LOCKED`, `MOD0119_EXTERNAL_CONTRACT_PENDING`, `MOD0119_PERSISTENCE_FAILED`, `AP_IMPORT_*`,
  `AP_RECON_*`, `AP_PROJECTION_*`, `AP_PLANNED_PAYMENT_*`, `AP_SETTLEMENT_*`, `AP_EVIDENCE_MANIFEST_*`,
  `AP_EVIDENCE_READINESS_CONFLICT`, `AP_BREAK_GLASS_*`, `AP_MASKING_POLICY_VIOLATION` (banka alanı yok),
  `AP_SERVICE_PRINCIPAL_SCOPE_DENIED`. Kaynaktaki "HTTP 200 + `Succeeded=false`" dört tekrar kodu (paket §E) taşınmaz.
- Kiracılar/şirketler arası erişim **404** `AP_INVOICE_NOT_FOUND` (multi-tenancy). Kullanıcıya giden ileti resx
  anahtarıdır; teknik metin (`403`, `Forbidden`) ekrana çıkmaz (UAS-001 §5).

## Repo Scope

Hepsi kod alma izninden ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.FinanceTreasuryService/**` — **servis bu paketle açılmaz.** İskelet (5 katman, port 5071, JWT,
  `TenantId` izolasyonu, 4 pipeline davranışı, `AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment =
  true)`, modül manifest kaydı) ve servis ortak yapı taşları **yalnız** MOD-0126 paketinin DL-1'inde açılır (tek sahibi ve
  yazarı; O-21 kapandı — gap 4/G5). MOD-0119 bu paket önce `ready-for-dev` olsa bile iskeleti ya da ortak yapı taşını
  açmaz; MOD-0126 DL-1'i bekler. MOD-0119 o servise **feature** olarak eklenir:
  - `src/Diten.FinanceTreasuryService.Application/Features/ApInvoice/**` (Golden Reference Compact düzeni) (DL-1…DL-3)
  - `src/Diten.FinanceTreasuryService.Domain/ApInvoices/**` (DL-1)
  - `src/Diten.FinanceTreasuryService.Persistence/ApInvoices/**` (depolar, indeksler; her işlem geri çağrısı
    `CentralAuditTransaction.EnlistAsync` çağırır) (DL-1)
  - `src/Diten.FinanceTreasuryService.Infrastructure/{Suppliers,InvoiceMatch,ApWorkflow,ApEvidence}/**` (AP'ye özgü port
    bağdaştırıcıları); servis-ortak `LegalEntities`, `Currency`, `RecordCodes`, `DataScope` bağdaştırıcıları MOD-0126
    DL-1'indir, AP yalnız kullanır (DL-1)
  - Servis ortak yapı taşları (`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`
    — `FinanceTreasuryCommandReceipt` + iki indeksi, `IFinanceTreasuryDataScopeSource` okuma / yazma kapsamı; D14/1,
    D14/2) ve CentralAudit kaydı MOD-0126 DL-1 ile açılır; AP yalnız kullanır, yeniden tanımlamaz (DL-1)
  - `src/Diten.FinanceTreasuryService.Api/Controllers/ApInvoicesController.cs` (DL-1…DL-3)
  - Modül manifestine MOD-0119 girdisi (`module-self-registration-standard`) (DL-1)
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` — servisin defteri MOD-0126 DL-1 ile açılır; AP aynı
  `b` izini kullanır, yeni iz satırı açmaz; `AuditTrailStandardTests` sayıları değişirse aynı değişiklikte güncellenir ve
  CT kararı ister (DL-1).
- `docs/analysis/contracts/ap-invoice.openapi.yaml` — yeni üretici sözleşmesi (x-owner MOD-0119); karar 0.3 önerisi
  kabul edilirse T32 sapması altında dondurulur (DL-1).
- `frontend/Diten.Web/Views/FinanceTreasury/ApInvoices/**`,
  `frontend/Diten.Web/wwwroot/assets/js/FinanceTreasury/ApInvoices/**`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/ApInvoices/ApInvoicesIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/FinanceTreasury/ApInvoicesController.cs` (same-origin MVC proxy),
  `PageHelpResource.{7 dil}.resx` içinde `Help.APINVOICES.*` ve `Services/Help/PageHelpCatalog.cs` satırı,
  menü/nav anahtarları `Nav.Domain.FINANCETREASURY` (MOD-0126 ile ortak), `Nav.Module.ACCOUNTSPAYABLE`,
  `Nav.Page.APINVOICES` (DL-4).

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9; burada yazılmaz):
MOD-0220 TRM-14 (MOD-0126 ile ortak); MOD-0140 `supplier.openapi.yaml` 1.3.0 (MVP-2 tek yazar — Treasury yazmaz, K15);
MOD-0023 AP iş akışı şablonu; `ocelot.json` rotaları (integration-agent); AGENTS.md §2/§3/§9 ve
`.antigravity/rules/ports.md` (5071); `record-coding-standard.md` §3 tablosuna `APD` satırı; Platform `AuditCategory` eki
(O-18); FinanceTreasury servisinden SEC-002 veri kapsamı çözücüsünün tüketim yolu (platform-shared-services, O-22). Bkz.
"Dependencies".

## Protected Paths

- `.antigravity/**` (port bandı, RCS-001 §3 satırı ve kural değişiklikleri ayrı onay)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.Platform/**` (MOD-0023, MOD-0021, MOD-0048, MOD-0031, `IDataScopeResolver` — platform-shared-services), `services/Diten.MdmService/**`
  (MOD-0220 — master-data-management), `services/Diten.ProcurementService/**` (MOD-0140 tek yazar MVP-2, MOD-0143 — K15),
  `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**` (CentralAudit yalnız tüketilir) ve diğer domain servisleri
- MOD-0126'nın `Features/PaymentBatch/**`, `Domain/PaymentBatches/**`, `Persistence/PaymentBatches/**` ve
  `Infrastructure/BankDispatch/**` yolları (aynı servis, başka modül — XMC-001); servis-ortak portlarda ve ortak yapı
  taşlarında (`FinanceTreasuryCommandReceipt`, `IFinanceTreasuryDataScopeSource`) değişiklik dört Treasury paketinin
  onayını ister
- `frontend/Diten.Web/Views/Archive/**`, `Controllers/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone` (kaynak
  layout'u; Recovery'de kullanılmaz)
- `docs/analysis/contracts/supplier.openapi.yaml`, `invoice-match.openapi.yaml` ve diğer üretici sözleşmeleri (tüketilir,
  yeniden tanımlanmaz)
- `execution/registries/**`, `execution/domains/finance-treasury/domain-config.md`, DCP, CT kuyruğu — bu paketle değişmez
- Kaynak depo `/Users/natig/Projects/ERP-vNext` ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz

## Dependencies

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c) | Bu pakete etkisi |
|---|---|---|---|
| **MOD-0003 Data Contract Registry** (Blueprint kapısı) | sözleşme kaydı | **yok** (0 dosya; kaynakta da yok, paket §D) | **ENGEL — karar 0.3 AÇIK.** Öneri: süreli T32 sapması + `docs/analysis/contracts/`'ta dondurulmuş sözleşmeler (O-1). Karar verilmeden pack `ready-for-dev` olmaz |
| **ERP-FIN-BUNDLE** (Min Integration Contract, GL + AP + AR ortak) | finans paket sözleşmesi | **yok**; üreticisi yok (`TREASURY-MIGRATION-SEQUENCE-v1.0.md` Amendment 2 §1, §5) | **ENGEL — karar 0.6 AÇIK (EA).** AP'nin kendi `AP-INVOICE` sözleşmesinin bundle'a nasıl bağlanacağı bu karara bağlı (O-2) |
| MOD-0118 GL | fiş/yevmiye | yok | karar 0.7 önerisi "kapsam dışı" (O-3); AP v1 fiş üretmez ve `VoucherReference` taşımaz |
| MOD-0023 Workflow Designer | başlatma + görev geçişleri | **var.** `POST /api/v1/workflow/instances` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:87`); istekte `IdempotencyKey` ve aynı anahtar yeniden oynatılır (`…/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs:76-85`); görev uçları `tasks/{taskId}/approve` (:196), `reject` (:221), `request-info` (:261); başlatanın onayı `SodViolation` ile reddedilir (`WorkflowTaskTransitionSupport.cs:133-140`, Guid karşılaştırması `:964-977`); uzlaştırma okuması `GET instances/by-objects` (:121); izinler `WorkflowModels.cs:16-20` | v1 kullanıcı token'ıyla genel uçlardan (MOD-0126 O-7 deseni). MOD-0119 iş akışı şablonu (`templateCode` TBD, O-11) MOD-0023 sahibinin işi (D9). Not: MOD-0023 başlatanın **reddine** izin verir (`WorkflowTaskTransitionSupport.cs:445`); AP R2 daha sıkıdır ve yerelde uygulanır |
| MOD-0021 Audit / CentralAudit (BL-501, K4) | yol `b` | **var** — `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/`; kullananlar Inventory, SupplyChain | servis kaydı MOD-0126 DL-1'de; kaynak NoOp kaydedici taşınmaz (level-1 koşul 2) |
| MOD-0220 LE `lookup-validation` | borçlu şirket uygunluğu (D10/1 kuralı) | **var** — `GET /api/legal-entities/{id}/lookup-validation` (`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48`); kayıtta `OperationalStatus` ve `ApprovalStatus` var (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:49-50`); yanıtta `ApprovalStatus` **henüz yok** | **TRM-14** (D10/2) MOD-0126 ile ortak; gelmeden "onaylı" kontrolü yapılamaz → AP yazma komutları kapanır (fail-closed) |
| MOD-0140 Supplier (`SUPPLIER`) | tedarikçi kimliği + şirket ilişkisi durumu | **main'de 1.2.0 FROZEN**, `supplierId: string`, `POST /api/suppliers/validate` (`services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/SuppliersController.cs:112`); **tedarikçi kiracıda tek + LE bazlı `SupplierCompanyRelationship` değişikliği `origin/feature/mvp2/supplier-tenant-01` dalında** (MOD-0140 pack `ready-for-dev`, 1.3.0 dosyası "bu pack'in onayından sonra, DL-2 ile yazılır"; `validate` yanıtındaki `status` = çağıran şirketin ilişki durumu; Blocked/OnHold yeni işleme kapalı, K-7; banka bilgisi MOD-0140'ta yok, K-8) | AP yalnız `supplierId` + çağıran şirketle **Active** ilişkiyi sözleşmeden tüketir; koleksiyon okuması yok. **1.3.0 main'e girmeden DL-1 başlamaz.** Treasury MOD-0140'a yazmaz (K15, D9/4) |
| MOD-0143 Invoice Capture & 3-Way Match (`INVOICE-MATCH`) | eşleşme sonucu | **var** — `invoice-match.openapi.yaml` 1.1.0 FROZEN, `GET /api/invoice-match/invoices/{invoiceId}` (`services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/InvoiceMatchController.cs:52-53`, izin `procurement.invoice-match.read`); durumlar `Captured / Matched / MatchedWithinTolerance / Exception / Rejected / ClearedForPayment` (`…/Domain/Entities/Invoice.cs:16`; `ClearedForPayment` "yalnız işaret", `ResolveMatchExceptionHandler.cs:70`); `Invoice` şeması `supplierId`, `currency`, `totalAmount` (Decimal string) taşır | AP sonucu **okur**, eşleştirme yapmaz; uygun durum kümesi `{Matched, MatchedWithinTolerance}` ve tutar kuralı `Σ Net = totalAmount` (KARAR D26). MOD-0143 değişikliği gerekmez; sözleşme 1.1.1 açıklaması + kayıt tutarlılık kontrolü procurement tarafında onaylandı (D26/3) |
| MOD-0048 Reference Data (para birimi) | kod listesi + küsurat hanesi | **kısmen** — `GET /api/lookups/reference/currencies` var, küsurat hanesi yok (MOD-0126 pack Dependencies ölçümü) | küsurat kaynağı **TBD** (O-10, MOD-0126 O-11 ile ortak); yoksa Create 503 `AP_CURRENCY_PRECISION_UNAVAILABLE` — sabit tablo uydurulmaz |
| MOD-0031 Evidence Linking | kanıt bağlantısı okuma | **var** — `GET /api/v1/evidence/links/{id}` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/EvidenceLinksController.cs:83-84`); bağlantı nesne referansı `EvidenceObjectRefDto(Module, ObjectType, ObjectId, ObjectVersion)` (`…/Features/EvidenceLinking/EvidenceLinkingModels.cs:67`) | AP bağlantıyı **oluşturmaz**, var olanın nesnesinin `(MOD-0119, ApInvoice, {id})` olduğunu doğrular (DL-3) |
| MOD-0018 RBAC | `[HasPermission]`, izin tohumu | var | `finance-treasury.*` ad alanı (D11/4); rol şablonları O-20; `read-tenant-wide` yalnız açık atama (`ExplicitGrantOnlyPermissions`) |
| **SEC-002 veri kapsamı** (`IDataScopeResolver`, MOD-0018-FU15) | kullanıcının şirket yetkisi (D14/1) | çözücü yalnız Platform'da kayıtlı (`Diten.Platform.Application/DependencyInjection.cs` → `OrgDataScopeResolver`); ortak kütüphanedeki `NoOpDataScopeResolver` boş liste döner (`.antigravity/rules/data-scope-enforcement.md` §6); FinanceTreasury'den tüketim yolu **yok** | **ENGEL (O-22)**: SEC-002 §4 kapısı — kapsam cümlesi + kapsamı soran kod + kanıt testi olmadan `ready-for-dev` yok. Tüketim yolu servis ortaktır (`IFinanceTreasuryDataScopeSource`); MOD-0126 O-19, MOD-0120 O-14, MOD-0124 ile **tek karar**. `NoOp` kaydı yasak (kapsamı kapatır) |
| RCS-001 ortak kod servisi (§5.12, BL-565) | fatura kodu | **yok** (MOD-0126 pack ölçümü); şirket kısa kodu (§5.4) LE kaydında ayrı alan değil (`LegalEntity.cs:11` yalnız `Code`) | **ENGEL**: modül kendi sayacını yazamaz (RCS-001 §6). Servis gelene kadar Create yazılamaz (O-9) |
| `Diten.FinanceTreasuryService` iskeleti + servis ortak yapı taşları | servis | **yok** | **yalnız** MOD-0126 DL-1 açar (makbuz tipi + indeksleri, veri kapsamı kaynağı, ortak LE / para birimi / kod portları, CentralAudit kaydı — O-21 kapandı, gap 4/G5); MOD-0126 DL-1, AP DL-1'in önkoşuludur; o olmadan AP DL-1 başlamaz |
| Port 5071 | servis portu | sahip kararı D10/3; **AGENTS.md §3 ve `.antigravity/rules/ports.md` güncellenmedi** (TRM-04) | iskelet port kaydından sonra |
| DCP (UNASSIGNED-finance-treasury-intake) | dilim kapısı | `draft`; TRM-S2 "karar 0.3 bekliyor" (DCP §8.1) | DCP `approved` olmadan pack `ready-for-dev` olmaz (DCP §15) |

### Bağımlılık sözleşmeleri — kapanış tablosu

Her satır kapanmadan ilgili dilim başlamaz. **Sahte (mock) istemciyle geçen test, canlı entegrasyon kanıtı sayılmaz:**
"sözleşme testi" sütunu sahte istemcili testi, "canlı kanıt" sütunu gerçek servisle dev ortamında koşulmuş kaydı
(`scripts/run_record.py`) ister.

| Bağımlılık | Sözleşme dosyası / sürüm | Hash | Sahibi | Hata davranışı (MOD-0119) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|---|
| AP-INVOICE (bu modül) | `docs/analysis/contracts/ap-invoice.openapi.yaml` v1.0.0 — **yazılacak** | TBD (dondurulunca) | finance-treasury | — | sözleşme testi AC-28 · — | DL-1 |
| MOD-0003 kararı | karar kaydı (0.3) — **yok** | — | sahip (K1) | — | karar kaydı | DL-1 önkoşulu |
| ERP-FIN-BUNDLE kararı | karar kaydı (0.6) — **yok** | — | EA | — | karar kaydı | DL-1 önkoşulu |
| MOD-0220 LE uygunluğu + `ApprovalStatus` | MOD-0220 sözleşme belgesi — **yazılacak** (TRM-14) | TBD | master-data-management (D10/2) | 422 uygun değil · 503 erişilemez · geçiş yok | sahte LE istemcisi · dev ortamında Active+Approved / Approved-değil iki kayıt | DL-1 |
| MOD-0140 tedarikçi ilişkisi | `supplier.openapi.yaml` **1.3.0** — MVP-2 DL-2'de yazılacak (bugün main'de 1.2.0) | TBD (main'e girince) | procurement (MVP-2) | ilişki yok / Active değil → 422 · erişilemez → 503 · geçiş yok | sahte istemci · dev ortamında Active / OnHold / Blocked / ilişkisiz dört tedarikçi | DL-1 |
| MOD-0143 eşleşme sonucu | `invoice-match.openapi.yaml` 1.1.0 FROZEN (1.1.1 yalnız açıklama, D26/3); `totalAmount` **net** (üretici ölçümü, D26/1; vergi taşınmaz) | `41fe1f43bc74638c6d409c85b2613d36d00ddbfaeff880dc3c8e929cec513d11` (DL-1 başında yeniden `shasum`) | procurement (MOD-0143 sahibi) | uygun değil → 422 · fark → 422 · erişilemez → 503 · `ClearedForPayment` / `Captured` / `Exception` / `Rejected` → 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE` | sahte istemci · dev ortamında Matched / MatchedWithinTolerance / Exception / ClearedForPayment faturaları | DL-1 |
| MOD-0048 para birimi + küsurat | `GET /api/lookups/reference/currencies` + küsurat alanı — **yok** (O-10) | TBD | platform-shared-services | hane yok → 503 | sahte istemci · dev ortamında TRY/EUR/JPY | DL-1 |
| RCS-001 ortak kod servisi | **yok** (BL-565, O-9) | — | CT + `.antigravity` onayı | servis yoksa Create yazılamaz | — · — | DL-1 önkoşulu |
| SEC-002 veri kapsamı (D14/1) | Platform `IDataScopeResolver` tüketim yolu — **yok** (O-22; dört paket için tek karar) | TBD | platform-shared-services | çözücü hatası → boş kapsam, boş liste, detay / yazma 404 | kapsam testi AC-32 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-1 |
| MOD-0023 iş akışı başlatma + görev geçişi | Platform `WorkflowModels.cs` (sözleşme dosyası yok) + AP şablon kodu — **TBD** (O-11) | Platform commit SHA'sı DL-2 başında yazılır | platform-shared-services | belirsiz → 202 Unknown · kesin ret → 502 · zaman aşımı yapılandırılır · yinelenen anahtar hatası → Unknown (ret değil) | sahte MOD-0023 (anahtar başına tek örnek / geçiş) · dev ortamında gerçek başlatma + onay + ret + bilgi isteme + **aynı anahtarlı yarış yanıtı ölçümü** (var olan örneği / geçişi döndürdüğü doğrulanmadan yinelenen anahtar hatası `Unknown`) | DL-2 |
| MOD-0023 saklama + silmeme politikası (örnek ve geçiş kaydı; gap 6/P1-a) | **KARAR D21/1–2** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/1: `WorkflowInstance` ve `WorkflowTransitionLog` hiç silinmez, TTL yok; silinmiş kayıtlar da okunur — `isDeleted` + `includeDeleted=true` (D25/2, D21/2'yi genişletir); silinmiş görülürse yeniden yazma yok | §17 onayında | platform-shared-services (politika: sahip K1, D21) | §17 onaylanıp AC-W1 geçene kadar 404 / boş okumada anahtarlı yeniden yazma **kapalı**; niyet `Unknown` | ölçüm (@ef4f3a8bc): silme yolu ve TTL yok — ölçüm politika değildir · MOD-0023 AC-W1 (mimari test, sabotajlı) + bu pack'in silinmiş kayıt testi | DL-2 |
| MOD-0023 okuma eşleme alanları (gap 6/P1-b) | **KARAR D21/3** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/3: `by-objects` ve `instances/{id}` → `IdempotencyKey`; `history` → `ApprovalTaskId` + `IdempotencyKey` (yalnız ekleme) | §17 onayında | platform-shared-services | alan kodda yoksa okuma niyeti kapatmaz → `Unknown` | MOD-0023 AC-W2/W3 + bu pack'in eşleme testi | DL-2 |
| MOD-0031 kanıt okuma | `api/v1/evidence` (sözleşme dosyası ölçülecek) | DL-3 başında | platform-shared-services | 503 → bağlama yok, fatura değişmez · AP MOD-0031'e yazmaz, kurtarmada yeniden çağrı yok | sahte · dev ortamı · **MOD-0031 dedupe ölçümü** — AP'ye MOD-0031 yazması ya da kurtarmada yeniden çağrı eklenmeden önce zorunlu (gap 5/G2) | DL-3 |
| CentralAudit | `Diten.BuildingBlocks.CentralAudit` (kod) | Building.Blocks commit SHA'sı DL-1 başında | BL-501 | yazılamazsa komut başarısız | sabotaj testi AC-17 · AUD-001 §8 dev kanıtı AC-29 | DL-1 |

## Runtime Constraints

- Servis portu **5071** (D10/3; AGENTS §3 kaydı bekliyor). Tarayıcı ve `Diten.Web` yalnız Gateway (5000) üzerinden
  çağırır; tarayıcı JS 5071'e gitmez.
- **Kiracı + şirket izolasyonu her sorguda:** `TenantId` `ITenantContext`'ten, `LegalEntityId` `X-Legal-Entity-Id`
  başlığından (MOD-0220'de doğrulanmış); başka kiracı ya da başka şirket kaydı **404**. Üretimde sabit/varsayılan kiracı
  yok. Kaynağın talep (claim) kapsam kümeleri (`ap_legal_entity_scope`, `ap_supplier_scope`, `ap_bank_account_scope` —
  paket §G) taşınmaz; şirkete erişim R-2 deseni ve aşağıdaki SEC-002 veri kapsamıyla çözülür.
- **Veri kapsamı (SEC-002, sahip kararı D14/1) — kapsam cümlesi:** kapsamı taşıyan alan **`LegalEntityId`** (fatura ve
  bütün alt kayıtları); çevrilen kapsam türü yalnız **`LegalEntity`** (`OrgUnit`, `Position`, `ManagerChain` çevrilmez →
  daraltır, sızdırmaz; birim→şirket birleştirmesi uydurulmaz); `IsInclude = false` olan `LegalEntity` kapsamı o şirketi
  **kapatır**. Kullanıcı yalnız yetkili olduğu şirketlerin kayıtlarını görür: istek başlığındaki şirket kullanıcının
  kapsamında değilse liste **boş** (200, 0 satır), detay / iz / istisna listesi / her yazma **404** `AP_INVOICE_NOT_FOUND`;
  çözücü hatası, kiracı ya da kullanıcı yokluğu → boş kapsam (süzgeçsiz sorgu değil). Kiracıdaki tüm şirketleri görmek
  ayrı izindir: `finance-treasury.ap-invoices.read-tenant-wide` (yalnız açık atama — `ExplicitGrantOnlyPermissions`; tek
  üretici `FinanceTreasuryDataScope.TenantWideScope()`; `MOD-0119-0120-TENANT-LE-EVIDENCE-v1.0.md` §4'teki "modüle özel
  anahtar" budur). Kapsam servis genelinde **tek yerde** (`IFinanceTreasuryDataScopeSource`, MOD-0126/0120/0124 ile ortak;
  modül yalnız kendi `read-tenant-wide` anahtarını verir) çözülür; okumalar (liste, detay, iz, istisna listesi) **okuma
  kapsamını**, her yazma **yazma kapsamını** kullanır (aşağıda); sayfalama ve toplamlar daraltılmış kümeden hesaplanır.
  Referans uygulama: `WorkReportScope` / `WorkReportScopeSource` (SEC-002 §5). Tek ekranda şirketler arası birleşik AP
  listesi v1'de yok (Follow-up).
- **Okuma kapsamı ile yazma kapsamı ayrıdır (gap 4/G1; D14/1 ile tutarlı — `…read-tenant-wide` *görme* iznidir;
  tanım MOD-0126 pack Runtime Constraints, servis ortak):** okuma kapsamı = kullanıcının kendi şirket yetkileri ya da
  `read-tenant-wide` varsa kiracıdaki tüm şirketler; **yazma kapsamı** = yalnız kendi şirket yetkileri (`TenantWideScope()`
  hiçbir zaman girmez). Her yazma komutu (Create, Update, Delete, BulkDelete, Validate, Submit, Reconcile, Approve,
  Reject, RequestInfo, Hold, ReleaseHold, istisna açma / çözme / yeniden açma, LinkEvidence) hedef faturanın
  `LegalEntityId`'si için katman 1'i ister: `X-Legal-Entity-Id` hedef faturanın şirketine eşit **ve** kullanıcının o
  şirkette kendi yetkisi var. Yalnız `read-tenant-wide` sayesinde görünen faturaya yazma → **404** `AP_INVOICE_NOT_FOUND`
  (kapsam dışı kuralıyla aynı; başka adım çalışmaz).
- **Uygunluk güncel durumdur, kayıt anındaki kopya değildir (TP-001 T14):** (a) MOD-0220 `lookup-validation` — borç
  oluşturan ya da değiştiren **her yazma komutunda** (sahip kararı D14/4: Create, Update, Delete, BulkDelete, Validate,
  Submit, Approve, Reject, RequestInfo, Hold, ReleaseHold, istisna açma / çözme / yeniden açma, LinkEvidence) —
  `OperationalStatus = Active` **ve** `ApprovalStatus = Approved` değilse `AP_LEGAL_ENTITY_NOT_ELIGIBLE` (422), yanıt yoksa
  `AP_LEGAL_ENTITY_UNAVAILABLE` (503). **Reconcile hariç (D15/2):** yeni karar başlatmaz, uygunluğu tx1'de denetlenmiş
  komutun uzak sonucunu kaydeder (engellemek niyeti açık bırakırdı; MOD-0126 ile aynı). Muafiyet yalnız uzakta **var
  olan** örneğin / geçişin sonucunu kaydetme yolu içindir; okuma örnek / geçiş göstermezse aynı anahtarla yeniden yazma
  (yalnız MOD-0023 başlatma / geçiş) yeni uzak işlemdir ve şirket + SoD + izin + asıl aktör yeniden denetlenir (Runtime
  Constraints 3a, gap 4/G2, gap 5/G2). Okumalar uygunluk istemez (pasifleşmiş şirketin
  geçmiş borcu okunabilir; kapsam kuralı yine geçerli). (b) MOD-0140 — Create, Update, Validate, Submit, Approve ve
  ReleaseHold'da çağıran şirketle ilişki `Active` değilse `AP_SUPPLIER_NOT_ELIGIBLE` (422), yanıt yoksa
  `AP_SUPPLIER_UNAVAILABLE` (503); (c) PO'lu faturada
  Validate, Submit ve Approve'da MOD-0143 — durum uygun küme dışında ise `AP_MATCH_OUTCOME_NOT_ELIGIBLE` (422), tedarikçi /
  para birimi / tutar farkı `AP_MATCH_REFERENCE_MISMATCH` (422), yanıt yoksa `AP_MATCH_UNAVAILABLE` (503); tutar kuralı
  `Σ Net = totalAmount` (KARAR D26 — Validation Rules → MOD-0143). Hiçbirinde
  geçiş yapılmaz. Bu **okumalar** uzak yan etki değildir ve tx1'den önce yapılır.
- Soft delete (`IsDeleted` + `DeletedAt` UTC, filtrede Id + TenantId + LegalEntityId). Okumalar `IsDeleted = false`.
- **Idempotency (D12/2 kuralı — her yazma komutunda zorunlu):** Create, Update, Delete, BulkDelete, Validate, Submit,
  Reconcile, her karar, Hold, ReleaseHold, istisna açma/çözme/yeniden açma ve kanıt bağlama `Idempotency-Key` başlığı
  **ister**; başlık yoksa 400 `AP_IDEMPOTENCY_KEY_REQUIRED`. Sunucu anahtar **üretmez**.
  - **Makbuz kapsamı:** `(TenantId, LegalEntityId, ActorId, Operation, TargetId, Key)`; tekil indeks bu altılıdadır; aynı
    anahtar başka aktör/işlem/hedef için ayrı makbuzdur.
  - **Makbuz (`FinanceTreasuryCommandReceipt`, servis ortak — MOD-0126 pack Entity Fields, D14/2):** istek parmak izi sunucuda hesaplanır; tek aşamalı komutlarda tx1'de `Completed` +
    yanıt; iki aşamalı komutlarda (Submit ve kararlar) tx1'de `Pending` (yanıt yok, `LinkedIntentId` dolu), yanıt
    **tx2'de** (kesin sonuç ya da 202 belirsiz) ya da — makbuz hâlâ `Pending` ise — **Reconcile'da** yazılır ve makbuz
    `Completed` olur. `Completed` makbuz değişmez.
  - **Kontrol sırası (her komutta aynı):** (1) kimlik + `[HasPermission]` → (2) veri kapsamı (SEC-002, D14/1; okumada
    okuma kapsamı, **yazmada yazma kapsamı** — hedef faturanın şirketi kullanıcının kendi şirket yetkisinde değilse, fatura
    `read-tenant-wide` ile görünse bile → 404 `AP_INVOICE_NOT_FOUND`, başka adım yok) → (3) makbuz araması: aynı kapsam + farklı
    parmak izi → 409 `AP_IDEMPOTENCY_CONFLICT`; aynı kapsam + aynı parmak izi + `Completed` → **kayıtlı yanıt aynen
    döner**; aynı kapsam + aynı parmak izi + `Pending` → 202 `AP_REQUEST_IN_PROGRESS` (bağlı niyetin o anki durumuyla).
    İki durumda da **başka hiçbir adım çalışmaz** — uzak çağrı yok, Reconcile yok. → (4) açık niyet kontrolü (Submit ve
    kararlar) → (5) durum ve `Version` CAS → (6) uygunluk okumaları, iş kuralları ve SoD → (7) tx1 (iş verisi + makbuz +
    denetim, tek işlem).
  - **Kurtarma yalnız `ReconcileApInvoiceWorkflowCommand` ile yapılır** (kendi `Idempotency-Key`'i ve makbuzuyla); aynı
    anahtarla tekrar asla kurtarma tetiklemez (replay ve recovery ayrıdır).
  - **Çökme:** tx1 sonrası çökmede makbuz `Pending` kalır; aynı anahtarla tekrar 202 `AP_REQUEST_IN_PROGRESS` alır;
    Reconcile niyeti kapattığında bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır; `Completed` makbuza dokunulmaz.
    tx1 commit olmadıysa hiçbir şey yazılmamıştır; aynı anahtarla tekrar baştan çalışır.
- **Eşzamanlılık:** Update / kararlar / Hold `If-Match` (Version) ve durum CAS; bayat istek 409 `AP_INVOICE_VERSION_CONFLICT`.
- **Yerel kayıt önce, uzak çağrı sonra (domain-config; TP-001 T16):**
  1. *Submit:* tx1 — fatura `ApprovalPending` + `ApprovalStatus = Submitted`, `ApWorkflowStartIntent(Pending)`, merkezi
     denetim deneme olayı (işleme yazılır). Sonra MOD-0023 başlatma, niyetteki uzak anahtarla. Sonra tx2: kesin başarı →
     niyet `Started`, `ApprovalStatus = PendingDecision` + `WorkflowInstanceId`; kesin ret (400/403/409 — 404 **hariç**: 404 belirsizdir → `Unknown`, D21/7; MOD-0023'ün
     "başlatma tamamlanmadı" 409'u, 409 `WORKFLOW_TRANSITION_CONFLICT` ve 503 `WORKFLOW_TRANSITION_CONFLICT` / `WORKFLOW_TRANSACTION_UNAVAILABLE` hariç — bunlar belirsizdir, D21/7, D25) → niyet `Refused`, fatura `Validated`, `ApprovalStatus = DraftRequest`;
     **belirsiz** (zaman aşımı, bağlantı kopması, 5xx, boş ya da okunamayan 2xx gövde) → niyet `Unknown`, fatura
     `ApprovalPending`/`Submitted` kalır, yanıt **202** `AP_WORKFLOW_START_OUTCOME_UNKNOWN`. Belirsiz sonuç ret olarak
     kaydedilmez.
  2. *Açık niyet varken* (`Pending` / `Unknown`): **aynı** anahtarla tekrar → makbuz kuralı; **farklı** anahtarla Submit →
     409 `AP_WORKFLOW_START_PENDING`, uzak çağrı yok. Fatura başına en fazla bir açık başlatma niyeti (`ux_ap_start_intent_open`).
  3. *Reconcile:* sonuç `Started` / `Refused` / `Cancelled`'a yalnız uzak tarafın **kesin** durumuyla bağlanır; belirsiz
     kalırsa durum değişmez ve deneme sayılır. Sıra 3a'dadır.
  3a. *Reconcile önce okur; uzak yazmayı yalnız anahtarla idempotent olduğu ölçülmüş yerde yeniden yapar (gap 4/G2,
     gap 5/G2; Submit ve üç karar komutu; kural MOD-0126 pack Runtime Constraints 3a ile aynı):*
     - **Önce okuma:** başlatmada `instances/by-objects` (fatura kimliği) + niyetin deterministik uzak anahtarı; kararda
       görev geçmişi + `RemoteIdempotencyKey`.
     - **Uzakta niyetin kendisine ait örnek / geçiş var** (eşleme aşağıda, gap 6/P1-b) → kesin durum kaydedilir (tx2 +
       bağlı `Pending` makbuz `Completed` + sonuç olayı);
       şirket kuralından **muaf** (D15/2).
     - **"Kesin boş" okumadan çıkarılmaz (gap 5/G2):** MOD-0023 sözleşmesinde "bulunamadı / boş okuma son durumdur"
       güvencesi yoktur (ilk çağrı hâlâ yolda olabilir); okumaya dayalı "kesin boş → yeniden başlat" dalı yoktur. Okuma
       belirsiz ya da "bulunamadı" ise (zaman aşımı, bağlantı kopması, 5xx, okunamayan gövde, 404 / boş sonuç) niyet
       **asla `Cancelled` yapılmaz**, `Unknown` kalır (`Pending` idiyse `Unknown`'a geçer); fatura ve makbuz değişmez.
       Zaman aşımı / 5xx / okunamayan gövdede başka adım yoktur; 404 / boş sonuçta tek ek adım aşağıdaki anahtarlı
       yeniden yazmadır.
     - **Yeniden çağrı yalnız MOD-0023 başlatma ve görev geçişlerinde** — uzak yazmanın kendisi anahtarla idempotent
       (ölçüldü, Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c): `ux_workflow_instances_tenant_idempotency_key_active` (TenantId +
       IdempotencyKey, kısmi `IsDeleted = false`) ve `ux_workflow_transition_logs_task_action_idempotency_active`
       (TenantId + ApprovalTaskId + Action + IdempotencyKey) tekil indeksleri
       (`PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131`, `:211-225`); başlatma var olan örneği döndürür
       (`StartWorkflowInstanceHandler.cs:76-85`), geçişler anahtarı zorunlu tutar (`ApproveWorkflowTaskValidator.cs:13`) ve
       `GetByTaskActionIdempotencyKeyAsync` ile yeniden oynatır (`RejectWorkflowTaskHandler.cs:79-80`,
       `WorkflowTerminalTransitionWriter.cs:402-407`). Yeniden yazmadan önce (i) şirket uygunluğu (D10/1, D14/4), (ii) SoD
       (başlatmada R1–R3 aday dışlaması yeniden; kararda niyetteki karar verenle R1–R6, R8, R9), (iii) izin ve (iv) aktör:
       Reconcile'ı çağıran asıl komutun aktörü (`SubmittedBy` / `DecidedBy`) değilse uzak yazma çağrısı yok (409
       `AP_RECONCILE_RESTART_ACTOR_MISMATCH`) — MOD-0023'teki başlatan / karar veren değişmez. Hepsi geçerse aynı uzak
       yazma **aynı deterministik uzak anahtarla bir kez** yapılır; en fazla bir örnek / geçişi MOD-0023'ün tekil indeksi
       garanti eder. Başarı (yeni ya da yeniden oynatılmış) → niyet sonuçla kapanır (tx2 eşlemesi); kesin ret (tx2
       eşlemesi; yinelenen anahtar / tekil indeks çakışması ret sayılmaz) → `Refused`; kesin ret olmayan her başarısızlık
       (yinelenen anahtar yarışı yanıtı, 5xx, zaman aşımı, okunamayan gövde) → `Unknown` kalır.
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
       kalır (202 `AP_WORKFLOW_START_OUTCOME_UNKNOWN` / `AP_DECISION_OUTCOME_UNKNOWN`), fatura, alacak dekontu
       rezervasyonu ve makbuz değişmez. 404 / boş okumadaki anahtarlı yeniden yazma, MOD-0023 saklama + silmeme politikası
       (en az niyet ömrü boyunca; sahibi platform-shared-services) ve silinmiş durumu ayırt eden okuma kapanana kadar
       **kapalıdır** (kapanış tablosu, Ready-for-dev engeli).
     - **Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b):** `instances/by-objects` aynı faturanın önceki gönderimlerinin
       örneklerini de döndürür (`GetWorkflowInstancesByObjectsHandler.cs:50-69`). Okuma sonucu niyete yalnız **hepsi**
       eşleşirse bağlanır: kiracı; fatura kimliği; başlatmada `SubmissionSequence`, kararda karar kimliği
       (`ApApprovalDecision.Id`); deterministik uzak anahtar (`ApWorkflowStartIntent.IdempotencyKey` /
       `ApApprovalDecision.RemoteIdempotencyKey`); geçişte ek olarak görev kimliği (`WorkflowTaskId`) + eylem (approve /
       reject / request-info). Eski bir denemenin sonucu (önceki `SubmissionSequence`'in örneği, başka kararın geçişi ya da
       iptali) yeni niyeti **asla** kapatmaz; yalnız fatura kimliğiyle ya da "en yeni örnek" sırasıyla eşleme yasaktır.
       Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c): `WorkflowInstanceStatusDto` (`WorkflowModels.cs:200-207`) ve
       `WorkflowInstanceHistoryEntryDto` (`:451-465`) uzak anahtarı taşımaz, geçmiş satırı görev kimliğini de taşımaz; alan
       yanıtta yoksa okuma niyeti kapatamaz → `Unknown`. Kapanış yolu MOD-0126 pack 3a ile aynı (MOD-0023'e eklemeli okuma
       alanı, D9; ya da P1-a koşulları kapalıysa aynı anahtarla yeniden yazmanın yanıtı). Alacak dekontunun alacaklaması
       ve rezervasyon bırakma yalnız dekontun kendi niyetine bağlanmış sonuçla yazılır.
     - **D21 bağı — yeniden yazma kapısının yeni hâli (sahip kararı D21, MOD-0023 pack §17 (TRM-15)):** yukarıdaki kapının iki
       koşulundan saklama politikası **KARAR D21/1** ile verildi (hiç silinmez, TTL yok) ve MOD-0023 AC-W1 (mimari test) geçince
       kapanır; silinmiş kayıtlar ayrıca okunur (**D25/2**: `isDeleted`, `includeDeleted=true`) — politika + AC-W1 korunur; okumada
       silinmiş kayıt görülürse yeniden yazma yok. §17
       onaylanıp AC-W1 geçince (ve AC-W5…W14 geçmişse, D22/2) 404 / boş okumada aynı deterministik anahtarla yeniden yazma açılır; yalnız asıl
       aktör (**KARAR D21/5**) ve şirket / SoD / izin yeniden kontrolüyle. Silinmiş kayıt yine de görülürse yeniden
       yazma yok, niyet `Unknown`. Eşleme alanları **KARAR D21/3** (§17.3/3) kodda olana kadar okuma niyeti kapatmaz.
     - **Aynı anahtarlı yarış yanıtı (KARAR D21/4, §17.3/4; DL-2 başı kanıt maddesi):** politika — eşzamanlı iki
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
     - **MOD-0031 yeniden çağrılmaz:** AP MOD-0031'e yazmaz (yalnız bağlantıyı okur, LinkEvidence tek aşamalıdır); hiçbir
       kurtarma yolu MOD-0031'e yazma çağrısı yapmaz. İleride bir yazma eklenirse önce MOD-0031 dedupe ölçümü gerekir
       (Dependencies kapanış tablosu).
     - **`Cancelled` yalnız kesin uzak iptal / kapanış kaydıyla** (o örnek için MOD-0023 iptal geçiş kaydı ya da
       `CloseAbandonedWorkflowStartHandler.cs` kapanışı, anahtar `workflow-start-recovery-close:{instanceId:N}`, `:230`).
     - **Uygun değilse:** uzak yazma/başlatma çağrısı yok; niyet `Unknown` kalır, `LastOutcomeCode` sebep kodunu taşır
       (`AP_LEGAL_ENTITY_NOT_ELIGIBLE`, `AP_SOD_DENIED`, `AP_PERMISSION_DENIED`, `AP_RECONCILE_RESTART_ACTOR_MISMATCH`);
       fatura, alacak dekontu rezervasyonu ve bağlı `Pending` makbuz değişmez; sonuç olayı yazılmaz; Reconcile'ın kendi
       makbuzu sebep koduyla `Completed` olur. Koşul düzelince yeni Reconcile tekrar dener; kalıcı kapatma Follow-up.
  4. *Kararlar (Approve / Reject / RequestInfo):* SoD ön kontrolü + uygunluk okumaları + engelleyici istisna ve bekletme
     kontrolü → tx1 `ApApprovalDecision(Requested)` + deneme olayı → MOD-0023 görev geçişi, uzak karar anahtarıyla → tx2
     `Recorded` + fatura durumu (Approve son adım → `Approved`; son adım değil → `PendingDecision` kalır; Reject →
     `Rejected`; RequestInfo → `InfoRequested`). Belirsiz → `Unknown`, 202 `AP_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır.
     **Fatura başına tek açık karar niyeti** (`ux_ap_decision_open`); tx1 bu indeks + fatura `Version` CAS ile atomiktir.
     Açık karar niyeti varken başka anahtarla ya da başka aktörden gelen her karar **uzak sisteme gitmeden** 409
     `AP_DECISION_PENDING` alır.
  4a. *İki aşamalı denetim ve çökme kurtarma:* tx1 **deneme olayı** (`…workflow-start-requested`, `…decision-requested`),
     tx2 **sonuç olayı** yazar; ikisi de kendi işleminde, iş verisiyle birlikte (`RequireEnlistment = true`). Kesin sonuç
     ancak tx2 commit olunca vardır. **Uzak başarı + tx2 başarısız ya da süreç çöker:** niyet açık kalır; Reconcile (elle)
     MOD-0023 örneği/görev geçmişinden sonucu okur (yalnız niyetin kendi eşleme alanlarıyla — 3a, gap 6/P1-b), uzak çağrıyı **tekrarlamadan** tx2'yi yazar, sonuç olayını üretir ve
     bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır. **K2 kararlarında (Approve, Reject — sahip kararı D14/3):** tx1 deneme
     olayı yazılamazsa uzak çağrı yapılmaz, komut başarısız; tx2 sonuç olayı yazılamazsa niyet açık kalır ve karar
     "kayıtlı" sayılmaz — Reconcile tamamlar.
  5. HTTP istemcilerinde **açık zaman aşımı** (kaynakta yok, paket §L); değer `FinanceTreasury:Workflow:TimeoutSeconds`
     (MOD-0126 ile aynı ayar) — başlangıçta yoksa servis açılmaz (öneri 15 sn, O-11). MOD-0140, MOD-0143, MOD-0220,
     MOD-0031 okumaları için `FinanceTreasury:Reads:TimeoutSeconds` (öneri 5 sn); zaman aşımı 503 sayılır.
- **Alacak dekontu kapasitesi (orijinal faturaya bağlı `CreditNote`; O-14 önerisinin parçası):**
  - **Kural:** onaylı + bekleyen (onay akışındaki) dekontların toplamı orijinalin **brüt tutarını** aşamaz. Bu dekontun
    kendi rezervasyonu iki kez sayılmasın diye formül **adım adım ayrı** yazılır (gap 6/P1-c; `thisAmount` = bu dekontun
    `GrossAmount`'u, `thisReservation` = bu dekontun Submit'te orijinale yazdığı rezervasyon):
    - **Validate (ön kontrol, rezerv etmez) ve Submit tx1 (rezervasyon):** `CreditedAmount + CreditReservedAmount +
      thisAmount ≤ GrossAmount` → Submit'te orijinalde `CreditReservedAmount += thisAmount`.
    - **Approve (son adım tx2 ya da onu kaydeden Reconcile):** `CreditedAmount + (CreditReservedAmount − thisReservation)
      + thisAmount ≤ GrossAmount` — eşdeğeri: bu dekontun rezervasyonu zaten sayılmışken `CreditedAmount +
      CreditReservedAmount ≤ GrossAmount` → `thisAmount` `CreditReservedAmount`'tan `CreditedAmount`'a taşınır
      (`CreditReservedAmount −= thisReservation`, `CreditedAmount += thisAmount`; v1'de `thisReservation = thisAmount`),
      `OpenAmount = GrossAmount − CreditedAmount`. Submit formülü onayda **kullanılmaz** (bu dekontu iki kez sayardı ve
      kalan kapasitenin tamamını kullanan dekontu yanlışlıkla reddederdi).
    **Neden brüt tutar (OpenAmount değil):** brüt tutar orijinal onaylandıktan sonra değişmez; tavan sabit ve işlem
    sırasından bağımsızdır. OpenAmount v1'de zaten `Gross − CreditedAmount`'tır; "onaylı + bekleyen" toplamını OpenAmount'a
    karşı ölçmek onaylı dekontları iki kez sayardı, yalnız bekleyenleri OpenAmount'a karşı ölçmek ise aynı sonucu verir —
    tek, değişmeyen tavan daha basit ve test edilebilir. Ödeme dikişi açılınca (OpenAmount ödemeyle de azalır) kural o
    paketle yeniden yazılır (Follow-up).
  - **Bekleyen = onay akışında:** rezervasyon **Submit tx1**'de yapılır (dekont `ApprovalPending`); Validate yalnız ön
    kontroldür, rezerv etmez (`Received` / `Validated` dekont kapasite tutmaz). Submit tx1 aynı Mongo işleminde orijinal
    faturanın `CreditReservedAmount += dekont GrossAmount` alanını orijinalin `Version` CAS'ıyla yazar; kapasite aşılırsa
    422 `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`, uzak çağrı yok, hiçbir şey yazılmaz.
  - **Onay tek işlemde:** dekontun son adım onayının tx2'si (ya da onu kaydeden Reconcile) **aynı işlemde** kapasiteyi
    yukarıdaki **Approve formülüyle** yeniden denetler (rezervasyon tutulduğu için aşamaz; aşıyorsa değişmez bozulmuştur → tx2 yazılmaz, niyet açık kalır),
    dekontu `Approved` yapar ve orijinalde `CreditReservedAmount −= tutar`, `CreditedAmount += tutar`, `OpenAmount = Gross −
    CreditedAmount`, `CreditStatus` (`PartiallyCredited` / `FullyCredited`) yazar — hepsi orijinalin `Version` CAS'ıyla.
    OpenAmount oluşturmada ya da gönderimde **değil**, yalnız onayda azalır.
  - **Rezervasyonun bırakılması:** dekontun Reject'i (tx2), başlatmanın kesin reddi (`Refused`, tx2) ya da kesin iptali
    (`Cancelled`, Reconcile) — aynı işlemde, orijinalin `Version` CAS'ıyla `CreditReservedAmount −= tutar`.
  - **Belirsizlik:** niyet açıkken (`Pending` / `Unknown` başlatma, `Requested` / `Unknown` karar) rezervasyon sayılmaya
    devam eder, OpenAmount değişmez; Reconcile G2 kuralıyla yeniden başlatamadığında da (niyet `Unknown`) rezervasyon kalır.
  - **CAS çakışması = yeniden oku ve yeniden hesapla, kör tekrar değil:** orijinalin `Version`'ı değiştiyse handler
    orijinali yeniden okur, kapasiteyi ve sayaçları **yeni değerlerden** yeniden hesaplar ve yeniden CAS dener (sınırlı:
    öneri 3 deneme); eski okunmuş değer hiçbir zaman yazılmaz. Yeniden hesapta kapasite aşılıyorsa 422
    `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`; deneme biterse 409 `AP_INVOICE_VERSION_CONFLICT` (tx1'de: hiçbir şey yazılmadı;
    tx2'de: niyet açık kalır, Reconcile tamamlar). Böylece aynı orijinale iki dekont aynı anda fazla alacak yaratamaz.
  - Tam alacaklanmış (`FullyCredited`, kapasite 0) orijinale yeni dekont Validate / Submit'te 422
    `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`. Orijinale bekletme (Hold) kapasiteyi değiştirmez.
- **Tek aşamalı yazma komutları** (Create, Update, Delete, BulkDelete, Validate, Hold, ReleaseHold, istisna komutları,
  LinkEvidence): uzak **yazma** yoktur; uygunluk okumaları tx1'den önce, iş verisi + makbuz + tek denetim olayı tek Mongo
  işleminde. Okuma tx1'den sonra eskiyse (yarış) bir sonraki geçişte yeniden okunur; uygunluk kararı commit anına
  sabitlenmez (Implementation Notes → Tasarım notları).
- **Ödeme yok:** AP v1 MOD-0126'ya parti beslemez, MOD-0269'a dokunmaz, ödeme durumu tutmaz. `Approved` borç ödeme
  dikişi açılana kadar yalnız kayıttır (Follow-up).
- `GlobalEntity` kullanılmaz; bütün kayıtlar kiracıya ve şirkete aittir.
- Mongo GUID V3 / subtype 4; `decimal` → `Decimal128`.

## Layout & Shell Contract

- `shell: tenant` → `Views/FinanceTreasury/ApInvoices/` altındaki **tüm** `.cshtml` dosyalarında
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml` varsayılanı değiştirilmez. Kaynak ekrandaki
  `Layout = "_LayoutBackbone"` (`frontend/Diten.Web/Views/ERP/Finance/AccountsPayable/Index.cshtml:3@218acaf5`, paket §I)
  taşınmaz.
- View klasörü: `Views/FinanceTreasury/ApInvoices/` · Frontend rotası: `/FinanceTreasury/ApInvoices`.
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `finance-treasury.ap-invoices.read` yoksa kabuk içinde
  `_AccessDenied` partial'ı; başlık, kart, boş tablo, buton çizilmez; yönlendirme yok; 401 tam sayfa.

## Backend File Convention

Golden Reference **Compact** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
okundu): `Features/ApInvoice/` altında

```text
Commands/          CreateApInvoiceCommand.cs, UpdateApInvoiceCommand.cs, DeleteApInvoiceCommand.cs,
                   BulkDeleteApInvoiceCommand.cs, ValidateApInvoiceCommand.cs, SubmitApInvoiceCommand.cs,
                   ReconcileApInvoiceWorkflowCommand.cs, ApproveApInvoiceCommand.cs, RejectApInvoiceCommand.cs,
                   RequestApInvoiceInfoCommand.cs, PlaceApInvoiceOnHoldCommand.cs, ReleaseApInvoiceHoldCommand.cs,
                   RaiseApInvoiceExceptionCommand.cs, ResolveApInvoiceExceptionCommand.cs,
                   ReopenApInvoiceExceptionCommand.cs, LinkApInvoiceEvidenceCommand.cs
Queries/           GetApInvoiceListQuery.cs, GetApInvoiceByIdQuery.cs, GetApInvoiceTraceQuery.cs,
                   GetApInvoiceExceptionListQuery.cs, ApInvoiceListSort.cs
Handlers/CommandHandlers/   {Verb}ApInvoiceHandler.cs, ReconcileApInvoiceWorkflowHandler.cs,
                            {Verb}ApInvoiceExceptionHandler.cs, … (her komut için; suffix YOK)
Handlers/QueryHandlers/     Get{…}Handler.cs
Validators/        CreateApInvoiceValidator.cs, UpdateApInvoiceValidator.cs, GetApInvoiceListValidator.cs,
                   ApDecisionValidator.cs, ApHoldValidator.cs, ApInvoiceExceptionValidator.cs
ApInvoiceModels.cs          (tüm DTO'lar tek dosyada)
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` `{Verb}{Module}Handler` — **Command/Query
  suffix YOK**. Tek dosyada tek public tip. Kaynakta komutlar tek dosyada toplu (`Application/AccountsPayable/Commands/AccountsPayableCommands.cs@218acaf5`)
  ve iş kuralları tek büyük serviste (`AccountsPayableFoundationService.cs`, 3000+ satır, örn. içe aktarma :305-527) —
  taşınmaz; handler + domain'e bölünerek alınır (`handler-design`).
- Controller `CustomBaseController`'dan türer, `Response<T>` döner; her aksiyonda `[HasPermission]` (kaynakta yalnız
  `[Authorize]`, `AccountsPayableController.cs:17` ve elle `EnsureAccess` — taşınmaz).
- Her yazma komutu `ICentralAuditDeclaration` taşır (`CentralAuditEntityId` açık yazılır); MOD-0023'e yalnız HTTP ile
  dokunup yerelde yazmayan komut yoktur (iki aşamalı komutlar yerel niyet yazar), bu yüzden `ICentralAuditOutsideTransaction`
  kullanılmaz.
- Durum geçişleri tek domain tablosundan (`ApInvoiceStateMachine`) okunur; kaynaktaki `AccountsPayableStateGuards`
  sözlüğü (`AccountsPayableStateGuards.cs:5-19`) v1 durum kümesine daraltılarak alınır.

## Frontend File Contract

Golden Reference **Compact** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/` okundu):

```text
Views/FinanceTreasury/ApInvoices/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml             (sayfa kabuğu + _Form)
├── Edit.cshtml               (sayfa kabuğu + _Form; yalnız Received/Validated, açık niyet yok)
├── Details.cshtml            (sekme: Satırlar · Onay ve kararlar · İstisnalar · Kanıt · İz)
├── _Form.cshtml              (başlık alanları + satır ızgarası; belge türüne göre koşullu alanlar)
├── _Filter.cshtml            (durum, onay durumu, belge türü, tedarikçi, vade tarihi aralığı, para birimi)
├── _DataTable.cshtml         (data-dt-standard="v2", data-dt-data-mode="server", iskelet yükleyici)
├── _IndexL10n.cshtml
└── ApInvoicesIndex.cs        (marker)
wwwroot/assets/js/FinanceTreasury/ApInvoices/  index.js · index.l10n.js · form.js · details.js
Resources/Views/FinanceTreasury/ApInvoices/    ApInvoicesIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- Durum filtresi ve rozetleri `ApInvoiceStatus`'un taşınan 6 değerini ve `ApprovalStatus`'un taşınan 6 değerini gösterir;
  taşınmayan değerler (MatchPending, Matched, Exception, PaymentProposed, Closed, NotRequired, Delegated) görünmez.
- DataTable sütunları: Kayıt kodu, Tedarikçi, Fatura no, Belge türü, Fatura tarihi, Vade tarihi, Para birimi, Brüt tutar
  (para birimine göre biçim; alacak dekontu işaretli), Durum, Onay durumu, Bekletme, İşlemler.
- Karar düğmeleri (Onayla / Reddet / Bilgi iste) ve Bekletme / Bekletmeyi kaldır yalnız Details'ta ve yalnız sunucunun
  verdiği `allowedActions` listesine göre çizilir; SoD nedeniyle karar veremeyen kullanıcıya düğme gösterilmez, yerine
  yerelleştirilmiş açıklama çizilir — sunucu kontrolünün yerine geçmez.
- **Seçim kaynakları (sabit liste yok):** tedarikçi `GET /api/suppliers` (çağıran şirketle ilişkili, sözleşme 1.3.0);
  MOD-0143 faturası `GET /api/invoice-match/invoices?supplierId=…` (yalnız uygun durumlar gösterilir; son karar sunucuda);
  para birimi `GET /api/lookups/reference/currencies`; alacak dekontu için orijinal fatura `GET /api/ap-invoices`
  (aynı tedarikçi, `Approved`). Hepsi same-origin MVC proxy üzerinden.
- API profili: same-origin MVC proxy (`/FinanceTreasury/ApInvoices/api/...`); proxy `AuthTokenCookies.GetAccessToken` ile
  token'ı ve oturumun çözümlenmiş `X-Tenant-Id` / `X-Legal-Entity-Id` başlıklarını ekler; çözümlenemezse istek
  oluşturulmaz (multi-tenancy §6). Tarayıcı JS `access_token` / `document.cookie` okumaz. Proxy her yazma isteğine
  istemcinin ürettiği `Idempotency-Key`'i aynen iletir (anahtarı tarayıcı formu açılışta üretir; sunucu üretmez).
- Uyarı/onay diyalogları SweetAlert2 premium standardı (MOD-0013); hardcoded metin ve `|| 'Export'` tarzı geri dönüş yok.
  Kaynak ekrandaki sabit İngilizce metin (0 `Localizer`, paket §I) taşınmaz.

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| X-Legal-Entity-Id | Evet | Guid; kiracıya ait; Active + Approved (her yazmada, Reconcile'ın kayıt yolu hariç — D14/4, D15/2); okumada okuma kapsamında (kendi şirket yetkisi ya da `read-tenant-wide`, D14/1); **yazmada** hedef faturanın şirketine eşit ve kullanıcının **kendi** şirket yetkisinde (yazma kapsamı — `read-tenant-wide` yazma yetkisi vermez, gap 4/G1); değilse 404 | — | `ILegalEntityEligibilityPort`, `IFinanceTreasuryDataScopeSource` (okuma / yazma kapsamı) |
| Gövde/sorguda tenantId, legalEntityId | — | bulunursa 400 | — | middleware |
| DocumentType | Evet | tanımlı enum | — | — |
| SupplierId | Evet | trim, 1..64, MOD-0140 sözleşme biçimi; çağıran şirketle Active ilişki | — | `ISupplierEligibilityPort` |
| InvoiceNumber | Evet | trim, 1..64 | Unique (TenantId, LegalEntityId, SupplierId, DocumentType, InvoiceNumberNormalized), silinmemiş | — |
| InvoiceDate | Evet | `DateOnly`, default değil; ileri tarih kuralı O-19 | — | — |
| DueDate | Evet | `DateOnly`; `≥ InvoiceDate` | — | — |
| CurrencyCode | Evet | `^[A-Z]{3}$` (upper normalize), MOD-0048 listesinde | — | `ICurrencyReferencePort` |
| MatchInvoiceRef | Koşullu | `PoBackedInvoice` → zorunlu (400 `AP_MATCH_REFERENCE_REQUIRED`); diğer türlerde gönderilirse 400 `AP_MATCH_REFERENCE_NOT_ALLOWED` | Unique (TenantId, LegalEntityId, MatchInvoiceRef), silinmemiş | `IInvoiceMatchPort` (Validate/Submit/Approve) |
| CreditedApInvoiceId | Koşullu | `CreditNote` → zorunlu (öneri, O-14); aynı tedarikçi + para birimi, `Approved`, kendisi alacak dekontu değil (aksi 422 `AP_CREDITED_INVOICE_INVALID`); Validate / Submit: dekont `GrossAmount ≤ orijinal Gross − CreditedAmount − CreditReservedAmount`; Approve: aynı formül bu dekontun kendi rezervasyonu `CreditReservedAmount`'tan düşülerek (aksi 422 `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`; Validate ön kontrol, Submit rezervasyon, Approve kesin — Runtime Constraints → Alacak dekontu kapasitesi, gap 6/P1-c) | orijinal faturanın `Version` CAS'ı | yerel okuma |
| Notes | Hayır | trim, ≤ 2000 | — | — |
| Lines | Evet | 1..200 satır | — | — |
| Line.Description | Evet | trim, 1..256 | — | — |
| Line.NetAmount / Line.TaxAmount | Evet | ≥ 0; küsurat ≤ para birimi hanesi; ikisi birden 0 olamaz | — | `ICurrencyReferencePort` (hane) |
| Line.CurrencyCode | — | gönderilirse faturanınkine eşit | — | — |
| Başlık tutarları, DocumentDirection, InvoiceCode, SupplierNameSnapshot | — | istekte bulunursa 400 (sunucu alanı) | Unique (TenantId, InvoiceCode) | `IRecordCodePort` |
| Toplam | — | `GrossAmount > 0` (sıfır tutarlı belge reddedilir) | — | — |
| Idempotency-Key | **Evet** (her yazma komutu) | 1..128 yazdırılabilir ASCII; yoksa 400 `AP_IDEMPOTENCY_KEY_REQUIRED` | Unique (TenantId, LegalEntityId, ActorId, Operation, TargetId, Key) | makbuz araması |
| Karar ReasonCode | Reject / RequestInfo için Evet | trim, ≤ 64 | — | — |
| Karar WorkflowTaskId | Evet | Guid ≠ Empty, faturanın örneğine ait | — | MOD-0023 |
| Hold ReasonCode | Evet | trim, 1..64 | — | — |
| İstisna ExceptionType / Severity / ReasonCode | Evet / Hayır / Evet | v1 türleri; enum; trim 1..64 | `ux_ap_exception_active` | — |
| Evidence link id | Evet | Guid ≠ Empty; MOD-0031'de var, nesnesi `(MOD-0119, ApInvoice, {id})` | — | `IApEvidencePort` |

**Durum kuralları:** Update yalnız `Received`/`Validated`, `WorkflowInstanceId = null` ya da son niyet `Refused`/`Cancelled`,
açık niyet yok; güncelleme kaydı `Received`'a döndürür. Validate: uygunluk okumaları (LE, tedarikçi, PO'lu ise MOD-0143),
tutar kuralları, mükerrer kontrol (indeks), alacak dekontu kuralı; başarılıysa `Validated`. Submit yalnız `Validated`,
`HoldStatus = None`, engelleyici açık istisna yok, kanıt kuralı (O-17). Delete / BulkDelete yalnız `SubmissionSequence = 0`
ve `Received`/`Validated`. Hold yalnız `Validated`/`Approved`; ReleaseHold yalnız `OnHold` ve engelleyici açık istisna yok.

**MOD-0143 eşleşme kuralları (PO'lu fatura; KARAR D26, O-5):** MOD-0143 faturası aynı kiracı + şirkette bulunur; `status ∈
{Matched, MatchedWithinTolerance}` (D26/2 — `ClearedForPayment` tolerance-override ile gelmiş olabilir, `Captured` /
`Exception` / `Rejected` doğrulanmamış tutar taşıyabilir; hepsi 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE`); `supplierId` ve
`currency` AP kaydıyla eşit; tutar kuralı **`Σ Line.NetAmount = totalAmount`** (D26/1; MOD-0143 `Decimal` string'i
`decimal`'a çevrilerek, yuvarlamasız), fark 422 `AP_MATCH_REFERENCE_MISMATCH`. AP eşleştirme yapmaz, tolerans uygulamaz.
**Taban (üretici cevabı, `PRODUCER-ANSWER-MOD0143-01.md`, ölçüm origin/main @05f311784):** `totalAmount = Σ lineAmount`
sunucuda hesaplanır (`CaptureInvoiceHandler.cs:138`, `InvoiceMatchValidationRules.cs:48`); `lineAmount` yoksa
`quantity × unitPrice` (`:116-118`); 3'lü eşleştirme satırı fatura miktarı × PO birim fiyatıyla karşılaştırır ve PO'da vergi
yoktur (`RunThreeWayMatchHandler.cs:223-224`) — yani `Matched` / `MatchedWithinTolerance` faturada `totalAmount` nettir
(ikincisinde PO'ya göre tolerans kadar fark olabilir; faturanın kendi satır toplamı yine `totalAmount`'tır). Vergi
INVOICE-MATCH'te taşınmaz; AP `TaxAmount`'u kendi tutar. Sözleşme 1.1.1 açıklaması ve kayıtta `lineAmount` tutarlılık
kontrolü procurement tarafında onaylandı (D26/3) — gelene kadar kural yalnız iki durumla sınırlı olduğu için güvenlidir.

**SoD kuralları (her karar komutunda, MOD-0023 çağrısından ÖNCE):**

| # | Kural | Nerede | Kaynak karşılığı |
|---|---|---|---|
| R1 | Kaydı oluşturan (`CreatedBy`) faturada **hiçbir karar** veremez | gönderme çözümleyicisi (aday dışı) + karar kontrolü | kaynakta SoD yalnız yer tutucu: `FoundationOnlyAccountsPayableSodPolicy` her çağrıda red (`AccountsPayableSecurity.cs:328-338@218acaf5`) |
| R2 | Gönderen (`SubmittedBy` = MOD-0023 başlatanı) karar veremez (onay **ve** ret) | aynı + MOD-0023 onay kapısı | Recovery MOD-0023 yalnız onayı reddeder (`WorkflowTaskTransitionSupport.cs:133-140`); AP reddi de yerelde engeller |
| R3 | Gönderme anındaki son güncelleyen (`UpdatedByBeforeSubmit`) karar veremez | aynı | yeni |
| R4 | Karar veren, MOD-0023 görevinin atanmış aktörü olmalıdır (MOD-0023 `WorkflowActorDenied`) | MOD-0023 | — |
| R5 | Aktör karşılaştırması token `sub`'ının **Guid** değeri üzerindendir; `sub` Guid değilse karar reddedilir (`FailedRoleConflict`) | her yer | kaynakta `string ActorUserId` (`AccountsPayableSecurity.cs:302, 324`) — taşınmaz |
| R6 | İzin karar anında yeniden değerlendirilir (o anki token, `[HasPermission]`) | karar | — |
| R7 | **Muafiyet yoktur.** R1–R4 hiçbir izinle kaldırılamaz; `approve-with-sod-waiver` komutu ve kaynaktaki break-glass isteği (`AccountsPayableBreakGlassRequest`, `AccountsPayableSecurity.cs:314-317`) taşınmaz. Tutar kademesi v1'de yok (O-7) — kaldırılacak R4-benzeri kural da yok | karar | MOD-0126 D12/1 ile tutarlı (muafiyet ayrılık kurallarını kaldıramaz) |
| R8 | Approve, uygunluğu (LE, tedarikçi ilişkisi, PO'lu ise MOD-0143) **o anda** yeniden okur; uygun değilse karar uzak sisteme gitmez | karar | TP-001 T14 |
| R9 | Engelleyici açık istisna ya da `OnHold` varken Approve 409 (`AP_BLOCKING_EXCEPTION_OPEN` / `AP_INVOICE_ON_HOLD`); Reject ve RequestInfo serbest | karar | yeni |

## Failure Path to Verify

- **Kaydı oluşturanın kararı** → 403 `AP_SOD_DENIED`, `FailedSelfApproval`; MOD-0023 çağrılmaz; fatura durumu değişmez.
- **Gönderenin kararı (onay ya da ret)** → 403 `AP_SOD_DENIED`, `FailedSubmitterApproval`; MOD-0023 çağrılmaz.
- **Son güncelleyenin kararı** → 403 `AP_SOD_DENIED`, `FailedUpdaterApproval`.
- **Mükerrer fatura** (aynı şirket + tedarikçi + belge türü + normalize numara) → 409 `AP_INVOICE_DUPLICATE`, kayıt yok;
  eşzamanlı iki Create → biri 201, diğeri 409 (tekil indeks).
- **Aynı MOD-0143 faturasına ikinci AP kaydı** → 409 `AP_MATCH_REFERENCE_ALREADY_USED`.
- **PO'lu faturada MOD-0143 sonucu uygun değil** (`Captured`, `Exception`, `Rejected`) → Validate/Submit/Approve 422
  `AP_MATCH_OUTCOME_NOT_ELIGIBLE`; tutar / tedarikçi / para birimi farkı → 422 `AP_MATCH_REFERENCE_MISMATCH`; MOD-0143
  erişilemez → 503 `AP_MATCH_UNAVAILABLE`, geçiş yok.
- **Tedarikçi ilişkisi Active değil** (OnHold / Blocked / Inactive / ilişkisiz) → 422 `AP_SUPPLIER_NOT_ELIGIBLE`; MOD-0140
  erişilemez → 503 `AP_SUPPLIER_UNAVAILABLE`.
- **Şirket aktif değil ya da onaylı değil** → her yazma komutu (Reconcile hariç; D14/4) 422 `AP_LEGAL_ENTITY_NOT_ELIGIBLE`,
  kayıt değişmez; MOD-0220 erişilemez → 503; okumalar etkilenmez.
- **Başka kiracı / başka şirket faturası** (okuma, güncelleme, silme, karar, bekletme, istisna) → 404 `AP_INVOICE_NOT_FOUND`.
- **Kapsam dışı şirket** (kullanıcının veri kapsamında olmayan şirket, `read-tenant-wide` yok — D14/1) → liste 200 boş,
  detay / iz / istisna listesi / yazma 404 `AP_INVOICE_NOT_FOUND`; veri kapsamı çözücüsü hata verirse aynı sonuç.
- **Yalnız `read-tenant-wide` ile görünen faturaya yazma** (kullanıcının o şirkette kendi yetkisi yok — gap 4/G1) → her
  yazma komutu (Create, Update, Delete, Validate, Submit, Approve, Reject, RequestInfo, Hold, istisna, kanıt, Reconcile)
  404 `AP_INVOICE_NOT_FOUND`; liste ve detay okuması 200; uzak çağrı, kayıt ve makbuz yok.
- **Reconcile, uzakta örnek bulunamadı / okuma belirsiz** (gap 4/G2, gap 5/G2) → niyet `Cancelled` yapılmaz, `Unknown`
  kalır; okuma "kesin boş" kararı vermez. Zaman aşımı / 5xx / okunamayan gövde → uzak yazma/başlatma çağrısı yok. 404 /
  boş okumada yalnız MOD-0023 başlatma / görev geçişi aynı deterministik anahtarla yeniden yazılabilir; önce şirket + SoD +
  izin + aktör denetlenir: şirket uygun değilse uzak yazma/başlatma çağrısı yok, niyet `Unknown` + `LastOutcomeCode =
  AP_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422; aktör farklıysa 409 `AP_RECONCILE_RESTART_ACTOR_MISMATCH`; uygunsa aynı
  anahtarla tek uzak yazma — başarı (yeni ya da yeniden oynatılmış) niyeti kapatır, yinelenen anahtar hatası / 5xx /
  zaman aşımı → `Unknown` (asla `Refused` değil). MOD-0031'e hiçbir kurtarma yolu yazmaz.
- **Uzak kayıt silinmiş / temizlenmiş** (gap 6/P1-a) → okuma silinmiş kaydı gösterirse ya da silinmemiş kayıt yokken
  niyetin ilk denemesi kayıtlıysa (okuma silinmiş durumu ayırt edemiyorken) aynı anahtarla yeniden yazma **yapılmaz**; niyet `Unknown`, 202
  `AP_WORKFLOW_START_OUTCOME_UNKNOWN` / `AP_DECISION_OUTCOME_UNKNOWN`, fatura, rezervasyon ve makbuz değişmez.
- **Aynı faturanın eski örneği / eski kararın geçişi okunuyor** (gap 6/P1-b) → yeni niyete bağlanmaz; eşleme (kiracı,
  fatura, `SubmissionSequence` / karar kimliği, uzak anahtar, geçişte görev + eylem) tutmazsa ya da yanıt eşleme alanını
  taşımıyorsa niyet `Unknown` kalır; alacaklama ya da rezervasyon bırakma yazılmaz.
- **Alacak dekontu orijinalin kalan kapasitesini aşıyor** (onaylı + bekleyen + bu > orijinal brüt; tam alacaklanmış
  orijinal dahil) → Validate / Submit 422 `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`, uzak çağrı yok, rezervasyon yok.
- **Kalan kapasitenin tamamını kullanan dekontun onayı** (gap 6/P1-c) → Approve formülü bu dekontun rezervasyonunu düşerek
  hesaplar; onay geçer (`OpenAmount = 0`), çift sayımla 422 dönmez.
- **MOD-0143 faturası `ClearedForPayment` / `Captured` / `Exception` / `Rejected`** (D26/2) → PO'lu faturada Validate / Submit /
  Approve 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE`, uzak çağrı yok, onay yok.
- **Vergili PO'lu fatura** (D26/1) → `Σ Net = totalAmount` ise geçer (vergi AP'de `TaxAmount`); `Gross = totalAmount` ama
  `Σ Net ≠ totalAmount` ise 422 `AP_MATCH_REFERENCE_MISMATCH`.
- **Orijinal fatura uygun değil** (onaylı değil, başka tedarikçi / para birimi, kendisi alacak dekontu) → 422
  `AP_CREDITED_INVOICE_INVALID`.
- **Aynı orijinale eşzamanlı iki dekont** → orijinalin `Version` CAS'ı biri için çakışır; o istek orijinali yeniden okuyup
  kapasiteyi yeniden hesaplar: sığıyorsa geçer, sığmıyorsa 422 `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`; denemeler tükenirse 409
  `AP_INVOICE_VERSION_CONFLICT`. Fazla alacak oluşmaz.
- **Dekont onayı belirsiz** (`AP_DECISION_OUTCOME_UNKNOWN`) → rezervasyon kalır, orijinalin OpenAmount'u değişmez;
  Reconcile onayı kaydedince aynı işlemde OpenAmount azalır.
- **Açık karar niyeti varken başka anahtar/aktörden karar** → 409 `AP_DECISION_PENDING`, MOD-0023 çağrılmaz.
- **İş akışı başlatma zaman aşımı / bağlantı kopması / boş 2xx** → 202 `AP_WORKFLOW_START_OUTCOME_UNKNOWN`, niyet `Unknown`,
  fatura `ApprovalPending`/`Submitted`; ikinci Submit (farklı anahtar) 409 `AP_WORKFLOW_START_PENDING`; Reconcile aynı uzak
  anahtarla ilerler ve ikinci uzak örnek oluşmaz.
- **İş akışı başlatma kesin reddi** (ör. 404 şablon yok) → 502 `AP_WORKFLOW_START_FAILED`, niyet `Refused`, fatura `Validated`.
- **MOD-0023 örneği başka yüzeyden iptal edildi** → Reconcile niyeti `Cancelled` yapar, fatura `Validated`; yeniden
  gönderme yeni sıra alır.
- **Uzak başarı sonrası tx2 hatası / çökme** → niyet açık kalır; Reconcile uzak çağrıyı tekrarlamadan sonucu yazar.
- **Idempotency-Key yok** → 400 `AP_IDEMPOTENCY_KEY_REQUIRED`, kayıt yok.
- **Idempotent tekrar** — makbuz `Completed`: kayıtlı yanıt, ikinci kayıt/denetim olayı/uzak örnek yok; makbuz `Pending`:
  202 `AP_REQUEST_IN_PROGRESS`, kurtarma yok; aynı anahtar + farklı yük: 409 `AP_IDEMPOTENCY_CONFLICT`.
- **Eksik alan** (SupplierId, InvoiceNumber, satır) → 400 + alan düzeyi resx iletisi; kayıt oluşmaz.
- **Hassasiyet aşımı** (ör. JPY'de küsurat) → 400; hane kaynağı yok → 503 `AP_CURRENCY_PRECISION_UNAVAILABLE`.
- **Satır para birimi farklı / negatif tutar / sıfır toplam / `DueDate < InvoiceDate`** → 400 `AP_VALIDATION_FAILED`.
- **İstemcinin gönderdiği başlık tutarı ya da kod** → 400 (sunucu alanı).
- **Eşzamanlılık** (bayat `If-Match`) → 409 `AP_INVOICE_VERSION_CONFLICT`, sessiz üzerine yazma yok.
- **Gönderilmiş faturayı silme / onay akışındaki faturayı güncelleme ya da bekletme** → 409 `AP_INVOICE_STATUS_INVALID`.
- **Engelleyici açık istisna varken Submit / Approve / ReleaseHold** → 409 `AP_BLOCKING_EXCEPTION_OPEN`; aynı türde ikinci
  aktif istisna → 409 `AP_EXCEPTION_ALREADY_ACTIVE`.
- **Başka nesneye ait kanıt bağlantısı** → 422 `AP_EVIDENCE_LINK_INVALID`; MOD-0031 erişilemez → 503, fatura değişmez.
- **Yetkisiz aktör** (ilgili izin yok) → API 403 `AP_PERMISSION_DENIED`; UI: kabuk içinde erişim açıklaması, düğme yok (UAS-001).
- **Denetim kaydı yazılamıyor** (outbox yazımı başarısız) → işlem geri alınır, komut başarısız, iş verisi yazılmaz.
- **Ödeme beklentisi:** `Approved` fatura MOD-0126'da partiye dönüşmez; `ApLinked` partisi MOD-0126 v1'de reddedilir —
  AP v1'de bu yol yoktur (test: AP'de MOD-0126'ya giden çağrı yok).

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (kaynakta `[Authorize]` + elle
  `EnsureAccess`, `AccountsPayableController.cs:17, 215@218acaf5`).
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça, çok kelimede tire; ad alanı `finance-treasury.*` (D11/4). Kaynak `ap.*`
  anahtarları (25 değer, `AccountsPayableSecurity.cs:9-38@218acaf5`; paket §E) **kopyalanmaz** (domain-config "kaynaktaki
  `ap.*` … olduğu gibi alınmaz"); eşleme yalnız izlenebilirlik içindir (kaynak anahtar hiçbir zaman Recovery'de
  yayımlanmadığından takma ad gerekmez — PKS-001 §3).
- Aktör: `tenant_user`; `platform_admin` geçişi standarttır. Arka plan aktörü ve servis kimliği bu dilimde yok (kaynaktaki
  AP-16/AP-17 servis-principal yolu taşınmaz).

| Recovery anahtarı | Katman | Uçlar | Kaynak anahtar |
|---|---|---|---|
| `finance-treasury.ap-invoices.read` | T1 | GET liste, GET detay, GET istisnalar | `ap.invoice.read`, `ap.invoice.detail.read` |
| `finance-treasury.ap-invoices.read-tenant-wide` | T3 (yalnız açık atama, `ExplicitGrantOnlyPermissions`) | kiracıdaki tüm şirketlerin faturaları, izi ve istisnaları (SEC-002 §3.5, D14/1) — **yalnız okuma**; hiçbir yazma komutunun kapsamını genişletmez (gap 4/G1) | — (kaynakta `ap_legal_entity_scope` talebi; taşınmaz) |
| `finance-treasury.ap-invoices.create` | T1 | POST | `ap.invoice.import` (anlam değişti: elle kayıt) |
| `finance-treasury.ap-invoices.update` | T1 | PUT | — |
| `finance-treasury.ap-invoices.delete` | T1 | DELETE | — |
| `finance-treasury.ap-invoices.bulk-delete` | T2 | DELETE bulk | — |
| `finance-treasury.ap-invoices.validate` | T3 (sözlükte var) | validate | — |
| `finance-treasury.ap-invoices.submit` | T3 | submit | `ap.payment_candidate.submit` değil — yeni (fatura onayı) |
| `finance-treasury.ap-invoices.reconcile` | T3 | workflow/reconcile | — |
| `finance-treasury.ap-invoices.approve` | T2 | approve | `ap.invoice.approve` |
| `finance-treasury.ap-invoices.reject` | T2 | reject | `ap.invoice.reject` |
| `finance-treasury.ap-invoices.request-info` | T3 | request-info | `ap.invoice.request_evidence` |
| `finance-treasury.ap-invoices.hold` | T3 | hold | — |
| `finance-treasury.ap-invoices.release-hold` | T3 | release-hold | — |
| `finance-treasury.ap-invoice-trace.read` | T1 | trace | — |
| `finance-treasury.ap-invoice-exceptions.create` | T1 | POST exceptions | `ap.exception.create` |
| `finance-treasury.ap-invoice-exceptions.resolve` | T3 | resolve | `ap.exception.update` |
| `finance-treasury.ap-invoice-exceptions.reopen` | T3 | reopen | `ap.exception.update` |
| `finance-treasury.ap-invoice-evidence.link` | T3 (sözlükte var) | evidence-links | `ap.evidence_manifest.create` değil — yeni |

- **Taşınmayan kaynak anahtarlar:** `ap.reconciliation.*`, `ap.projection.*`, `ap.evidence_manifest.*`,
  `ap.payment_candidate.*`, `ap.policy.*`, `ap.import-failures.*`, `ap.external_event.record` (v1 dışı akışlar); eski
  `ap.import_failure.read` (kaynakta bilinçli kırılma, paket §B) zaten yok.
- Kullanıcının bağımlı servislerde de okuma izni olmalıdır, çünkü çağrılar kullanıcı token'ıyla yapılır:
  `mdm.legal-entities.read` (MOD-0220), MOD-0140 tedarikçi okuma izni (1.3.0'da adı ölçülür), `procurement.invoice-match.read`
  (MOD-0143, `InvoiceMatchController.cs:53`), MOD-0031 kanıt okuma; MOD-0023'te `platform.workflow.instances.start`
  (gönderen) ve `platform.workflow.tasks.approve` / `.reject` / `.request-info` (onaylayıcı) (`WorkflowModels.cs:16-19`).
  Rol şablonu O-20.
- Tier-3 eylemler (`read-tenant-wide`, `validate`, `submit`, `reconcile`, `request-info`, `hold`, `release-hold`,
  `resolve`, `reopen`, `link`) bu pakette kayıtlıdır (PKS-001 §5 Tier 3). `read-tenant-wide` hiçbir rol şablonunda
  varsayılan değildir (O-20).

## Audited Events

Yol `b`: `Diten.BuildingBlocks.CentralAudit` ortak ileticisi (AUD-001 §5 K4) — komut `ICentralAuditDeclaration` taşır,
servis `AddCentralAudit<FinanceTreasuryCentralAuditScope>(…, RequireEnlistment = true)` kaydeder (MOD-0126 DL-1), her
depo işlemi olayı **aynı Mongo işlemine** yazar (`CentralAuditTransaction.EnlistAsync`): iş verisi ve merkezi kayıt
birlikte commit olur ya da hiçbiri. **Servisin denetim altyapısı bugün yok** (servis yok): `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md`
dosyası yok; kurulum MOD-0126 DL-1'in ilk adımıdır ve AP DL-1'in önkoşuludur (Dependencies). Kaynak NoOp kaydedici
(`NoOpAccountsPayableControlAuditSink`, `AccountsPayableSecurity.cs:269-273@218acaf5`; DI `Application/DependencyInjection.cs:46`)
ve önce-yaz-sonra-denetle sırası (`AccountsPayableFoundationService.cs:1785→1790, 1948→1953`) **taşınmaz**; kaynağın
2026-11-30'a kadar süreli sınırlaması (sahip kararı B2-1, paket §L.3) yalnız kaynak hazırlığı içindir ve Recovery'ye
taşınmaz (level-1 koşul 2).

`Category`: Platform `AuditCategory`'de finans değeri yok (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-26`);
**O-18** (MOD-0126 O-6 ile ortak) — önerilen: eklemeli `Finance` değeri; karar çıkana kadar değer uydurulmaz.
`SourceModule`: `MOD-0119`. `EntityType`: `ApInvoice` (istisna olaylarında `ApInvoiceException`).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | K2 (fail-closed) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreateApInvoiceCommand | finance-treasury.ap-invoice.created | ApInvoice | b | AfterState alan adları: InvoiceCode, DocumentType, Status, CurrencyCode, GrossAmount, satır sayısı | evet (öneri) | — |
| UpdateApInvoiceCommand | finance-treasury.ap-invoice.updated | ApInvoice | b | değişen alan **adları** (değer değil); Status → Received | evet (öneri) | — |
| DeleteApInvoiceCommand | finance-treasury.ap-invoice.deleted | ApInvoice | b | BeforeState: InvoiceCode, Status | evet (öneri) | — |
| BulkDeleteApInvoiceCommand | finance-treasury.ap-invoice.bulk-deleted | ApInvoice | b | silinen kimlik sayısı; her kimlik için ayrı varlık kaydı (CentralAudit tek varlık kimliği taşır — çoklu kimlik biçimi DL-1'de ölçülür) | evet (öneri) | — |
| ValidateApInvoiceCommand | finance-treasury.ap-invoice.validated | ApInvoice | b | Status Received→Validated; okunan MOD-0143 durumu (PO'lu ise) | evet (öneri) | — |
| SubmitApInvoiceCommand | finance-treasury.ap-invoice.submitted (sonuç, tx2/Reconcile) | ApInvoice | b | ApprovalStatus Submitted→PendingDecision, WorkflowInstanceId (tx1 deneme olayı `…workflow-start-requested`: Status Validated→ApprovalPending, uzak anahtar) | evet (öneri) | — |
| ReconcileApInvoiceWorkflowCommand | kapattığı niyetin **sonuç** olayı (geçiş matrisi; ör. `…submitted`, `…workflow-start-cancelled`, `…decision-cancelled`); niyet değişmediyse olay yok | ApInvoice | b | niyet/karar durumu önce→sonra; bağlı makbuz yalnız `Pending` ise `Completed` | evet (öneri) | — |
| ApproveApInvoiceCommand | finance-treasury.ap-invoice.approved | ApInvoice | b | karar kimliği, SodResult, WorkflowIsFinalStep, Status (son adımda →Approved) | **evet — K2 (D14/3)** | — |
| RejectApInvoiceCommand | finance-treasury.ap-invoice.rejected | ApInvoice | b | karar kimliği, ReasonCode, Status →Rejected | **evet — K2 (D14/3)** | — |
| RequestApInvoiceInfoCommand | finance-treasury.ap-invoice.info-requested | ApInvoice | b | karar kimliği, ReasonCode, ApprovalStatus →InfoRequested | evet (öneri) | — |
| PlaceApInvoiceOnHoldCommand | finance-treasury.ap-invoice.placed-on-hold | ApInvoice | b | Status önce→OnHold, HoldReasonCode | evet (öneri) | — |
| ReleaseApInvoiceHoldCommand | finance-treasury.ap-invoice.hold-released | ApInvoice | b | Status OnHold→önceki | evet (öneri) | — |
| RaiseApInvoiceExceptionCommand | finance-treasury.ap-invoice-exception.raised | ApInvoiceException | b | ExceptionType, Severity, IsBlocking, fatura kimliği | evet (öneri) | — |
| ResolveApInvoiceExceptionCommand | finance-treasury.ap-invoice-exception.resolved | ApInvoiceException | b | Status önce→Resolved, ResolutionCode | evet (öneri) | — |
| ReopenApInvoiceExceptionCommand | finance-treasury.ap-invoice-exception.reopened | ApInvoiceException | b | Status Resolved→Reopened | evet (öneri) | — |
| LinkApInvoiceEvidenceCommand | finance-treasury.ap-invoice.evidence-linked | ApInvoice | b | bağlantı kimlikleri | evet (öneri) | — |

- **İki aşamalı komutların deneme olayları:** Submit → `finance-treasury.ap-invoice.workflow-start-requested` (tx1) + sonuç
  `…submitted` ya da `…workflow-start-refused` (tx2 / Reconcile); Approve / Reject / RequestInfo →
  `finance-treasury.ap-invoice.decision-requested` (tx1, karar türü alanda) + yukarıdaki sonuç olayı (tx2 / Reconcile).
  Deneme olayı tek başına "yapıldı" anlamına gelmez; tabloda yazılan olay **sonuç** olayıdır. Kapalı ama olumsuz
  sonuçların olayları: `…workflow-start-refused`, `…workflow-start-cancelled`, `…decision-refused`, `…decision-cancelled`.
  Reconcile kendi olayını değil, kapattığı niyetin **sonuç** olayını yazar.
- **Alacak dekontu etkisi orijinal faturada:** dekontun Submit (rezervasyon), Approve (alacaklama) ve Reject / başlatma
  reddi / iptal (rezervasyon bırakma) olayları, orijinal fatura kimliğini ve orijinalde değişen alan **adlarını**
  (`CreditReservedAmount`, `CreditedAmount`, `OpenAmount`, `CreditStatus`) aynı olayda taşır; orijinal için ayrı komut ve
  ayrı olay yoktur (değişiklik dekont komutunun işlemindedir).
- **Kayda yazılmayan:** tedarikçi adı, fatura numarası değeri, satır açıklaması, yorum serbest metni, tutarların satır
  ayrıntısı, token. Değişen alanların yalnız **adı** yazılır (AUD-001 §3).
- **K2 — KARAR (sahip kararı D14/3):** Approve / Reject — borcun ödemeye uygunluğuna karar veren, parayı etkileyen
  işlemler K2'dir (fail-closed; denetim kaydı iş verisiyle aynı işlemde). AP v1'de iptal/silme kararı ve elle ödeme kaydı
  komutu yoktur (silme yalnız hiç gönderilmemiş taslağın soft delete'idir); bu komutlar açılırsa aynı karar onları da
  bağlar. Uyulabilir yol bugün var: `RequireEnlistment = true` ile kayıt iş
  işlemiyle aynı Mongo işlemine yazılır, outbox yazımı başarısızsa işlem geri alınır ve komut başarısız olur. Diğer yazma
  komutları da aynı yolla yazıldığı için pratikte hepsi kapalı-başarısızdır.
- **Bilinen boşluk G-AUD-1 (MOD-0126 ile ortak):** `CentralAuditBehavior` başarısız komut için kayıt bırakmaz; AUD-001 §3
  reddedilen komutun da (`Outcome = Denied`) kaydını ister. SoD reddi, MOD-0023 kesin reddi ve yetkisiz denemeler bu dilimde
  yalnız modülün kendi izinde görünür (`ApApprovalDecision.State`, `ApWorkflowStartIntent`). Merkezi ret kaydı
  CentralAudit sahibinin (BL-501) işidir — follow-up; bu paket kendi iletim kopyasını yazmaz.
- **Okuma denetimi:** gerekmez (§7'deki üç sınıfa girmiyor; liste dışa aktarımı bu dilimde yok).
- **Arka plan işi yok:** uzlaştırma süpürücüsü bu dilimde yazılmaz (servis kimliği engeli, MOD-0126 ile aynı), bu yüzden
  İ2 satırı yok.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/ap-invoices` yok (yeni servis). Gerekli rotalar:
  `/api/ap-invoices`, `/api/ap-invoices/{everything}` → `localhost:5071`; `UpstreamHttpMethod` GET, POST, PUT, PATCH,
  DELETE, **OPTIONS**; explicit rotalar catch-all'dan önce (`routes.md`).
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür; önkoşul AGENTS.md §3 / `ports.md` 5071 kaydı ve
  MOD-0126'nın `/api/payment-*` rotaları ile aynı task'ta açılabilir.
- Kaynak rotaları (`/api/v1/mod0119`, `/api/v1/mod0119/{everything}` → 127.0.0.1:5058, kaynak `ocelot.json:1189-1236@218acaf5`)
  taşınmaz. Kaynakta bu rotalar temel pack'in izni olmadan eklenmişti ve denetleyicide olmayan PUT/PATCH/DELETE'i de
  iletiyordu (paket §A T22-2, §C, OI-12) — Recovery'de rota ancak bu paket `ready-for-dev` olunca integration-agent ile
  açılır.
- Servisten MOD-0140 (`/api/suppliers/*`), MOD-0143 (`/api/invoice-match/*`), MOD-0220 (`/api/legal-entities/*`), MOD-0023
  (`/api/v1/workflow/*`), MOD-0031 (`/api/v1/evidence/*`) ve MOD-0048 (`/api/lookups/reference/*`) çağrıları ve veri
  kapsamı tüketimi (SEC-002, O-22) Gateway üzerinden ya da ortamın servis-servis adresinden; adresler yapılandırmadan, eksikse servis başlamaz. Bu rotaların
  Gateway'de var olduğu DL-1/DL-2 başında ölçülür.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir,
pack'te asla izlenmez; bu pack'teki her AC yalnız "tanımlı"dır.

- [ ] AC-1 (zorunlu) — `POST /api/ap-invoices` geçerli istekle 201 döner; kayıt `Received`, `InvoiceCode` RCS-001 biçiminde
  ortak servisten (istekte gönderilen kod 400), `NetAmount` / `TaxAmount` / `GrossAmount` / `OpenAmount` sunucu hesabı
  (`Gross = Net + Tax`, satır toplamları), `DocumentDirection` belge türünden; `TenantId`/`LegalEntityId` sunucuda yazılı,
  yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — Şirket `OperationalStatus = Active` ve `ApprovalStatus = Approved` değilse borç oluşturan /
  değiştiren her yazma komutu (Create, Update, Delete, BulkDelete, Validate, Submit, Approve, Reject, RequestInfo, Hold,
  ReleaseHold, istisna açma / çözme / yeniden açma, LinkEvidence) 422 `AP_LEGAL_ENTITY_NOT_ELIGIBLE` döner ve kayıt
  değişmez (komut başına ayrı test); kontrol her çağrıda MOD-0220 `lookup-validation`'dan yapılır. MOD-0220 erişilemezse 503
  ve geçiş yok. Aynı şirketin faturasının liste ve detay okuması 200 döner (okuma etkilenmez) ve açık niyetin Reconcile'ı
  tamamlanır (D14/4).
- [ ] AC-3 (zorunlu) — Başka kiracının ya da başka şirketin faturasına okuma, güncelleme, silme, karar, bekletme, istisna ve
  kanıt işlemi 404 döner.
- [ ] AC-4 (zorunlu) — Tedarikçinin çağıran şirketle ilişkisi `Active` değilse (OnHold / Blocked / Inactive / ilişkisiz)
  Create, Update, Validate, Submit ve Approve 422 `AP_SUPPLIER_NOT_ELIGIBLE` döner; dört durum ayrı test edilir; durum
  MOD-0140 sözleşmesinden okunur, AP'de tedarikçi koleksiyonu okuması yoktur (mimari testi: ProcurementService
  koleksiyon adı ya da Mongo bağlantısı AP kodunda geçmez).
- [ ] AC-5 (zorunlu) — PO'lu faturada `MatchInvoiceRef` yoksa 400; MOD-0143 durumu uygun küme dışındaysa 422
  `AP_MATCH_OUTCOME_NOT_ELIGIBLE`; tedarikçi, para birimi ya da net tutar farkı
  (`Σ Net ≠ totalAmount`, KARAR D26) varsa 422 `AP_MATCH_REFERENCE_MISMATCH`; `ClearedForPayment` 422
  `AP_MATCH_OUTCOME_NOT_ELIGIBLE` (AC-40); aynı MOD-0143 faturasına ikinci kayıt 409.
- [ ] AC-6 (zorunlu) — Mükerrer fatura (aynı şirket + tedarikçi + belge türü + normalize numara) 409 `AP_INVOICE_DUPLICATE`;
  eşzamanlı iki Create'te biri 201, diğeri 409 (gerçek replica set); tekil indeks kaldırıldığında test kırmızıya döner
  (sabotaj).
- [ ] AC-7 (zorunlu) — Kaydı oluşturanın Approve / Reject / RequestInfo çağrısı 403 `AP_SOD_DENIED` (`FailedSelfApproval`)
  döner ve MOD-0023'e hiçbir istek gitmez (sahte istemciyle sayılır).
- [ ] AC-8 (zorunlu) — Gönderenin ve gönderme anındaki son güncelleyenin kararı (onay **ve** ret) 403 `AP_SOD_DENIED`
  (`FailedSubmitterApproval` / `FailedUpdaterApproval`); gönderme çözümleyicisi bu üç kişiyi aday dışı bırakır.
- [ ] AC-9 (zorunlu) — Aktör karşılaştırması Guid üzerindendir: aynı kullanıcının büyük/küçük harf farklı yazılmış `sub`'ı
  aynı kişi sayılır; Guid olmayan `sub` ile karar reddedilir. Kodda muafiyet / break-glass komutu ya da alanı yoktur
  (mimari testi).
- [ ] AC-10 (zorunlu) — Submit, MOD-0023'ü çağırmadan önce `ApWorkflowStartIntent(Pending)` ve fatura
  `ApprovalPending`/`Submitted`'ı commit eder; uzak çağrı niyetteki uzak anahtarla yapılır.
- [ ] AC-11 (zorunlu) — Başlatma zaman aşımı, bağlantı kopması ve boş/okunamayan 2xx yanıtı 202
  `AP_WORKFLOW_START_OUTCOME_UNKNOWN` üretir; niyet `Unknown`, fatura `ApprovalPending`; ret olarak kaydedilmez.
- [ ] AC-12 (zorunlu) — Açık niyet varken **aynı** anahtarla Submit makbuz kuralına uyar; **farklı** anahtarla Submit 409
  `AP_WORKFLOW_START_PENDING` ve uzak çağrı yok; Reconcile aynı uzak anahtarla ilerler ve MOD-0023'te aynı fatura için
  ikinci örnek oluşmaz (sahte MOD-0023 anahtar başına tek örnek sayar).
- [ ] AC-13 (zorunlu) — Kesin başlatma reddi 502 `AP_WORKFLOW_START_FAILED`, niyet `Refused`, fatura `Validated`; MOD-0023
  örneği iptal bulunursa Reconcile niyeti `Cancelled`, faturayı `Validated` yapar ve yeniden gönderme `SubmissionSequence + 1`
  ile yeni uzak anahtar alır.
- [ ] AC-14 (zorunlu) — Kararlar yerel `Requested` niyetinden sonra MOD-0023'e uzak karar anahtarıyla gider; belirsiz sonuç
  202 `AP_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır; çok adımlı akışta son adım olmayan onay faturayı
  `ApprovalPending`/`PendingDecision`'da bırakır, son adım `Approved` yapar.
- [ ] AC-15 (zorunlu) — Idempotency: her yazma komutu anahtarsız 400 `AP_IDEMPOTENCY_KEY_REQUIRED`; aynı kapsam + aynı yük,
  makbuz `Completed` ise kayıtlı yanıtı, `Pending` ise 202 `AP_REQUEST_IN_PROGRESS` döndürür (replay kurtarma yapmaz) ve
  ikinci kayıt, ikinci uzak çağrı ya da ikinci merkezi denetim olayı oluşmaz; aynı kapsam + farklı yük 409; aynı anahtar
  başka aktörde ayrı makbuzdur. **İlk yanıtın kaybolduğu** senaryo ayrıca koşulur.
- [ ] AC-16 (zorunlu) — Engelleyici açık istisna ya da `OnHold` varken Submit / Approve 409; aynı türde ikinci aktif istisna
  409 `AP_EXCEPTION_ALREADY_ACTIVE` (tekil kısmi indeks; kaldırıldığında eşzamanlı iki açma testi kırmızıya döner);
  `ApprovalPending` faturada Hold 409.
- [ ] AC-17 (zorunlu) — Merkezi denetim: tek aşamalı komutlar tam **bir** olay bırakır; iki aşamalı komutlar (Submit,
  kararlar) tx1'de tam bir **deneme** olayı ve sonuç kesinleştiğinde tx2'de (ya da Reconcile'da) tam bir **sonuç** olayı
  bırakır; idempotent tekrar yeni olay üretmez. Olay iş verisiyle aynı Mongo işleminde yazılır; outbox yazımı zorla
  başarısız edildiğinde komut başarısız olur ve iş verisi yazılmaz (sabotaj testi); uzak başarı sonrası tx2 zorla başarısız
  edildiğinde niyet açık kalır ve Reconcile uzak çağrıyı tekrarlamadan sonuç olayını yazar (kurtarma testi).
- [ ] AC-18 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard`
  yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş (borç satırı eklenmemiş).
- [ ] AC-19 (zorunlu) — Hiçbir varlık, DTO ya da denetim kaydı IBAN, hesap numarası, BIC/SWIFT, banka adı, maskelenmiş hesap
  ya da banka hesabı referansı alanı taşımaz (alan adı taraması testi).
- [ ] AC-20 (zorunlu) — Her denetleyici aksiyonunda `[HasPermission]` vardır (mimari testi); izinsiz çağrı 403.
- [ ] AC-21 (zorunlu) — Tutarlar `decimal`; para birimi hanesini aşan tutar 400 (yuvarlama yok); satır para birimi
  faturanınkinden farklıysa 400; negatif tutar, sıfır toplam ve `DueDate < InvoiceDate` 400; `float`/`double` para alanı
  yok (mimari testi).
- [ ] AC-22 (zorunlu) — Soft delete yalnız hiç gönderilmemiş `Received`/`Validated` faturada; `IsDeleted` + `DeletedAt`
  (UTC) birlikte yazılır, silinen kayıt listede görünmez, kodu yeniden kullanılmaz; silinen faturanın numarası yeniden
  girilebilir.
- [ ] AC-23 (zorunlu) — Tüm `Views/FinanceTreasury/ApInvoices/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` açıkça
  yazılı; `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` yok;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area FinanceTreasury --module ApInvoices --format gaps`
  sapma vermez ve `quality-gate-datatable` geçer; `<table data-dt-data-mode="server">` pack ile eşleşir.
- [ ] AC-24 (zorunlu) — 7 dil (`en, tr, fr, es, zh, ar, ru`) resx parite PASS; İngilizce olmayan dosyada İngilizce yer
  tutucu yok; hardcoded metin yok (`grep` 0).
- [ ] AC-25 (zorunlu) — `read` izni olmayan kullanıcı sayfayı açınca kabuk içinde erişim açıklaması görür; başlık, tablo,
  "Yeni" düğmesi çizilmez, yönlendirme olmaz (UAS-001, canlı denendi); SoD nedeniyle karar veremeyen kullanıcıya karar
  düğmesi çizilmez.
- [ ] AC-26 (zorunlu) — Durum filtresi ve rozetleri `ApInvoiceStatus` ve `ApprovalStatus`'un taşınan değerlerini gösterir;
  taşınmayan değer yok.
- [ ] AC-27 (zorunlu) — Sayfa yardım metni `Help.APINVOICES.Purpose`, `.Audience`, `.Step1…Step5` + `.Step{n}Title` 7 dilde var.
- [ ] AC-28 (zorunlu) — `docs/analysis/contracts/ap-invoice.openapi.yaml` yayımlanmış; uçlar, hata kodları ve idempotency
  başlığı sözleşmeyle eşleşir (sözleşme testi).
- [ ] AC-29 (zorunlu) — Bir oluşturma, bir güncelleme ve bir silme dev ortamında çalıştırılır; üç merkezi kayıt AUD-001 §8
  sorgusuyla gösterilir ve on alan işaretlenir (rapor ortamın adını yazar).
- [ ] AC-30 (zorunlu) — Yarış testleri (gerçek replica set, paralel istek): (a) aynı aktörün aynı anahtarla iki eşzamanlı
  Approve'u → tek karar, tek uzak geçiş; (b) iki farklı onaylayıcının farklı anahtarla eşzamanlı Approve ve Reject'i → tek
  açık karar niyeti, diğeri 409 `AP_DECISION_PENDING`, MOD-0023'e tek çağrı; (c) iki eşzamanlı Submit (farklı anahtar) →
  tek başlatma niyeti, diğeri 409. Tekil kısmi indeksler kaldırıldığında (b) ve (c) kırmızıya döner (sabotaj).
- [ ] AC-31 (zorunlu) — AP kodunda MOD-0126, MOD-0269 ya da banka bağdaştırıcısına çağrı yoktur ve kaynak veri taşıma /
  geri doldurma işi yoktur (mimari testi + repo taraması).
- [ ] AC-32 (zorunlu) — Veri kapsamı (SEC-002 §4 kanıt testi, D14/1): aynı yetkiye (`ap-invoices.read` + yazma izinleri)
  sahip iki kullanıcı, iki ayrı şirket kapsamı → birinin listesinde diğerinin şirketinin faturası çıkmaz; detay, iz,
  istisna listesi ve her yazma 404; hiçbir şirket kapsamı olmayan üçüncü kullanıcı boş liste görür ve her detay 404;
  `read-tenant-wide` izni olan kullanıcı iki şirketin faturalarını görür. Çözücü hata verdiğinde liste boş döner. Kapsam
  daraltması (`IFinanceTreasuryDataScopeSource` süzgeci) kaldırıldığında test kırmızıya döner (sabotaj);
  `read-tenant-wide` otomatik yoldan verilmez (`ExplicitGrantOnlyPermissions` listesinde). Makbuz tipi servis ortak
  `FinanceTreasuryCommandReceipt`'tir: AP kodunda AP önekli makbuz tipi ya da makbuz koleksiyonu yoktur (mimari
  testi, D14/2).
- [ ] AC-33 (zorunlu) — Okuma kapsamı yazma kapsamını genişletmez (gap 4/G1): `ap-invoices.read-tenant-wide` + bütün yazma
  izinlerine sahip ama LE-B'de **kendi şirket yetkisi olmayan** kullanıcı LE-B faturalarını listede ve detayda görür (200),
  ama LE-B'de Create (`X-Legal-Entity-Id = LE-B`), Update, Validate, Submit, Approve, Reject, Hold ve istisna açma 404
  `AP_INVOICE_NOT_FOUND` döner; MOD-0023 / MOD-0220 / MOD-0140 / MOD-0143'e çağrı gitmez, kayıt, makbuz ve denetim olayı
  oluşmaz (sahte istemcilerle sayılır); aynı kullanıcı LE-A'da (kendi yetkisi) aynı komutları çalıştırabilir. Sabotaj:
  yazma yolunda okuma kapsamı kullanıldığında test kırmızıya döner.
- [ ] AC-34 (zorunlu) — Reconcile önce okur (gap 4/G2; Submit ve her karar türü için ayrı test): (a) uzakta niyetin kendi
  eşleme alanlarıyla (AC-38) örnek / geçiş **var** → şirket uygun değilken bile sonuç kaydedilir (MOD-0220 çağrılmaz — D15/2), bağlı `Pending` makbuz `Completed`,
  tek sonuç olayı; (b) okuma 404 / boş / zaman aşımı / 5xx → niyet **`Cancelled` olmaz**, `Unknown` kalır, fatura ve
  makbuz değişmez, zaman aşımı / 5xx'te uzak yazma/başlatma çağrısı yok (okuma çağrısı yapılır); (c) okuma örnek / geçiş
  göstermedi + şirket uygun değil → MOD-0023'e uzak yazma/başlatma çağrısı yok, niyet `Unknown`, `LastOutcomeCode =
  AP_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422, sonuç olayı yok; (d) okuma örnek / geçiş göstermedi + gap 6/P1-a'nın
  iki koşulu kapalı (AC-37) + şirket uygun + SoD/izin geçer + asıl aktör → başlatma / geçiş aynı deterministik anahtarla yeniden yazılır ve MOD-0023'te tam **bir**
  örnek / geçiş vardır (ikinci Reconcile ikincisini üretmez; yinelenen anahtar hatası `Unknown` bırakır, asla `Refused`
  değil); aktör farklıysa 409 `AP_RECONCILE_RESTART_ACTOR_MISMATCH`, uzak yazma/başlatma çağrısı yok; (e) MOD-0031'e
  Reconcile ya da herhangi bir kurtarma yolu hiç yazma çağrısı yapmaz; belirsiz okumada niyet `Unknown` kalır (sahte
  MOD-0031 yazma çağrısı sayısı 0). Sabotaj: okuma atlanıp doğrudan yeniden yazıldığında, "bulunamadı" `Cancelled`'a
  bağlandığında, farklı anahtarla yeniden yazıldığında ya da MOD-0031'e yazıldığında test kırmızıya döner.
- [ ] AC-35 (zorunlu) — Alacak dekontu kapasitesi: brüt 1 000'lik onaylı orijinale 600'lük dekont gönderilince orijinal
  `CreditReservedAmount = 600`, `OpenAmount = 1 000` (değişmez); 500'lük ikinci dekontun Validate'i ve Submit'i 422
  `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`; 600'lük dekont onaylanınca **aynı işlemde** dekont `Approved`, orijinal
  `CreditReservedAmount = 0`, `CreditedAmount = 600`, `OpenAmount = 400`, `CreditStatus = PartiallyCredited`; 400'lük
  dekont onaylanınca `OpenAmount = 0`, `CreditStatus = FullyCredited`, `InvoiceStatus = Approved` kalır; sonra her yeni
  dekont 422. Dekont oluşturma ve gönderme OpenAmount'u azaltmaz; reddedilen dekontun rezervasyonu aynı işlemde bırakılır.
  Onaylı olmayan / başka tedarikçinin / alacak dekontu olan orijinal 422 `AP_CREDITED_INVOICE_INVALID`.
- [ ] AC-36 (zorunlu) — Alacak dekontu yarış ve belirsizlik testleri (gerçek replica set, paralel istek): (a) aynı
  orijinale tek başına sığan ama birlikte aşan iki dekontun eşzamanlı Submit'i → biri geçer, diğeri 422
  `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`; `CreditedAmount + CreditReservedAmount ≤ GrossAmount` her an korunur; (b) birlikte
  sığan iki dekontun eşzamanlı Submit / Approve'u → CAS çakışan istek orijinali yeniden okuyup yeniden hesaplar ve geçer;
  sayaçlar dekont tutarlarının tam toplamına eşittir (kör tekrar ya da eski değerin üzerine yazılması yok); (c) dekont
  Submit'i ya da Approve'u belirsiz kaldığında (`Unknown`) rezervasyon sayılmaya devam eder, OpenAmount değişmez ve kalan
  kapasiteyi aşan üçüncü dekont 422 alır; (d) belirsiz onayı Reconcile kaydedince alacaklama + OpenAmount + CreditStatus
  sonucu kaydeden işlemle **aynı işlemde** yazılır; başlatma `Refused` / `Cancelled` ya da Reject rezervasyonu aynı işlemde
  bırakır; tx2 zorla başarısız edildiğinde ne dekont ne orijinal kısmen yazılır. Sabotaj: orijinalin `Version` CAS'ı
  kaldırıldığında (a) fazla alacakla, (b) kayıp güncellemeyle kırmızıya döner.
- [ ] AC-37 (zorunlu) — Silinmiş uzak kayıttan sonra yeniden yazma güvenli sayılmaz (gap 6/P1-a; Submit ve her karar türü
  için ayrı test): sahte MOD-0023'te niyetin örneği / geçişi oluşturulup sonra soft delete edilir (ya da temizlenir) ve
  niyet `Unknown` bırakılır; Reconcile (a) silinmiş kaydı gösteren okumada ve (b) silinmemiş kayıt dönmeyen ve silinmiş durumu göstermeyen okumada niyetin
  ilk denemesi kayıtlı (tx1 commit olmuş) durumda aynı anahtarla yeniden yazmaz: niyet `Unknown`, 202
  `AP_WORKFLOW_START_OUTCOME_UNKNOWN` / `AP_DECISION_OUTCOME_UNKNOWN`, fatura, alacak dekontu rezervasyonu ve bağlı makbuz
  değişmez, sahte MOD-0023'te yazma çağrısı 0. Sabotaj: silinmiş durum ve ilk deneme kontrolü kaldırıldığında Reconcile
  ikinci bir örnek / geçiş oluşturur ve test kırmızıya döner.
- [ ] AC-38 (zorunlu) — Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b): (a) aynı faturada önceki `SubmissionSequence`'in
  kapanmış (reddedilmiş / iptal) örneği ve yeni gönderimin `Unknown` niyeti varken Reconcile yeni niyeti eski örnekle
  kapatmaz; yeni niyet `Unknown` kalır ya da yalnız kendi uzak anahtarıyla eşleşen örnekle kapanır; (b) aynı görevde başka
  bir kararın geçişi (farklı `RemoteIdempotencyKey`, görev ya da eylem) açık karar niyetini kapatmaz ve alacak dekontu
  sayaçlarını değiştirmez; (c) okuma yanıtı eşleme alanını taşımıyorsa niyet `Unknown` kalır. Sabotaj: eşleme yalnız
  fatura kimliğine indirildiğinde test kırmızıya döner.
- [ ] AC-39 (zorunlu) — Alacak dekontu formülü onayda çift saymaz (gap 6/P1-c): brüt 1 000'lik onaylı orijinalde
  `CreditedAmount = 600` iken 400'lük dekont (kalan kapasitenin **tamamı**) Submit'te geçer (`CreditReservedAmount =
  400`) ve onayı **başarılı** olur: `CreditReservedAmount = 0`, `CreditedAmount = 1 000`, `OpenAmount = 0`, `CreditStatus =
  FullyCredited`; aynı orijinale 1 birim fazlası olan dekont Validate / Submit'te 422 `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`
  (kapasite aşımı testi korunur). Sabotaj: onayda Submit formülü (`CreditedAmount + CreditReservedAmount + thisAmount ≤
  GrossAmount`) kullanıldığında tam kapasite onayı 422 alır ve test kırmızıya döner.
- [ ] AC-40 (zorunlu) — Vergili PO'lu fatura ve durum kümesi (KARAR D26): `TaxAmount > 0` olan PO'lu fatura için (a)
  `Σ Net = totalAmount` (MOD-0143 `Matched`) → Validate / Submit / Approve geçer, `GrossAmount = Net + Tax`; (b)
  `Gross = totalAmount` ama `Σ Net ≠ totalAmount` → 422 `AP_MATCH_REFERENCE_MISMATCH`; (c) MOD-0143 `MatchedWithinTolerance`
  → kural aynı; (d) `ClearedForPayment`, `Captured`, `Exception`, `Rejected` → 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE`, MOD-0023'e
  çağrı yok, onay yok. Sabotaj: kural brüte çevrildiğinde (a)/(b), durum kümesine `ClearedForPayment` eklendiğinde (d)
  kırmızıya döner.
- [ ] AC-41 (zorunlu) — MOD-0023 404'ü kesin ret değildir (gap 17/P1, D21/7): başlatma ve her karar geçişinde sahte
  MOD-0023 404 döndürdüğünde niyet **`Unknown`** kalır (`Refused` değil), iş kaydının durumu geri alınmaz (önceki duruma
  dönüş yok), yanıt 202 `…_OUTCOME_UNKNOWN`, sonuç olayı yok; ardından Reconcile okumasıyla kapanır ya da `Unknown`
  kalır. Sabotaj: 404 tx2'de kesin ret grubuna geri eklendiğinde test kırmızıya döner.

## Test Expectations

- **Birim:** validatorlar (her alan, koşullu alanlar belge türüne göre); tutar hesabı (`Gross = Net + Tax`, satır
  toplamları, hane sınırı); SoD R1–R9 (oluşturan / gönderen / güncelleyen, Guid karşılaştırması, ret de engelli); durum
  makinesi geçişleri (taşınan 6 durum + bekletme dönüşü); mükerrer anahtar normalizasyonu; idempotency parmak izi; RCS
  kod portu sahtesiyle kod biçimi.
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı/şirket izolasyonu (404); veri kapsamı (AC-32, sabotajlı); soft
  delete; idempotent tekrar; CAS 409; tekil indeksler (kod, iş anahtarı, MOD-0143 referansı, açık niyet, aktif istisna); CentralAudit kaydının iş işlemiyle
  birlikte commit/rollback'i ve outbox yazım hatasında komutun başarısız olması (sabotaj).
- **İş akışı sahte sunucusu:** zaman aşımı, bağlantı kopması, boş 2xx, 5xx, kesin 403, 404 (belirsiz → `Unknown`, D21/7), MOD-0023 "başlatma tamamlanmadı"
  409; aynı anahtarla tekrar → tek örnek; karar geçişinde anahtar iletimi; çok adımlı onay; iptal edilmiş örnek; silinmiş
  örnek / geçiş (AC-37) ve aynı faturanın eski örneği (AC-38).
- **MOD-0220 / MOD-0140 / MOD-0143 / MOD-0031 sahteleri:** her biri için uygun / uygun değil / bulunamadı / erişilemez;
  MOD-0140 için Active / OnHold / Blocked / Inactive / ilişkisiz; MOD-0143 için altı durum ve vergili fatura (AC-40); gerçek sözleşmeler geldiğinde
  sözleşme testi (1.3.0, 1.1.0, TRM-14).
- **Mimari:** her aksiyonda `[HasPermission]`; banka verisi alan adı yasağı; `float`/`double` para alanı yok; muafiyet /
  break-glass tipi yok; başka servisin koleksiyon adı yok; `AuditTrailStandard` filtresi yeşil.
- **Sözleşme:** `ap-invoice.openapi.yaml` ile uç şekli.
- **Frontend:** browser smoke (liste, oluştur — üç belge türü, düzenle, detay, karar ve bekletme düğmeleri izin/SoD'ye
  göre); yetkisiz kullanıcı canlı denemesi (UAS-001); DataTable doğrulayıcı PASS; RESX 7 dil parite PASS.
- **Build:** `Diten.FinanceTreasuryService` + `Diten.Web` + Gateway 0 hata; yerel kapıda `scripts/` Python testleri de
  koşulur.
- Kaynak testleri (R1, 251/251 @ `da4abb20`, paket §O) **kanıt değildir**, Recovery'de yeniden yazılır; kaynakta hiç olmayan
  testler (mükerrer yarışı, satır toplamı tutarlılığı, SoD ret yolu, belirsiz başlatma) burada zorunludur.

## Page Help (UXH-001)

- Sayfa kodu `APINVOICES` (`Nav.Page.APINVOICES`); Create/Edit/Details aynı kodun alt sayfalarıdır.
- Anahtarlar (kiracı 7 dil): `Help.APINVOICES.Purpose` (satıcı borcu kaydı, doğrulama ve onay; ödeme yapılmadığı açıkça
  yazılır), `Help.APINVOICES.Audience` (AP operatörü, AP onaylayıcısı, AP yöneticisi), `Help.APINVOICES.Step1…Step5` +
  `Step{n}Title` (Fatura kaydı · Satırlar ve tutarlar · Doğrula · Onaya gönder · Karar, bekletme ve istisna). `Immediate`
  yok (onaysız hemen geçerli işlem yok). İlgili sayfa: MOD-0143 fatura eşleştirme ekranı (varsa `PageHelpCatalog`
  satırında bağlantı; yoksa "yok").
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact okundu (backend + frontend dosya seti listelendi)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, entity_base, form_field_count)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile birebir (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; SoD kuralları R1–R9; MOD-0143 eşleşme kuralı; alacak dekontu kapasitesi
- [x] Failure Path ≥ 4 senaryo (duplicate/idempotency, missing, unauthorized, concurrency) + kiracılar arası + SoD + belirsiz sonuç
- [x] Authorization: izin listesi + policy + aktör + bağımlı servis izinleri
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (yol b)
- [x] Veri kapsamı cümlesi (SEC-002 §4, D14/1) ve kanıt testi (AC-32)
- [x] Sayfa yardım metni anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir; test expectations build/verifier/RESX/smoke/sözleşme kapsıyor

**ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz):**
- [ ] **Karar 0.3 — MOD-0003 Data Contract Registry** (O-1): fon ya da süreli T32 sapması kayıtlı
- [ ] **Karar 0.6 — ERP-FIN-BUNDLE sahipliği** (O-2, EA) kayıtlı
- [ ] Karar 0.7 — GL kapsamı (O-3) kayıtlı (öneri "kapsam dışı" kabul edilirse pack değişmez)
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); yüksek risk — paketi yazmamış, `high: evet` onaylayıcı
- [ ] DCP (UNASSIGNED-finance-treasury-intake) `approved` ve numarası atanmış
- [ ] `Diten.FinanceTreasuryService` iskeleti, servis ortak yapı taşları ve CentralAudit kaydı MOD-0126 DL-1 ile açıldı (tek sahibi o dilim; O-21 kapandı)
- [ ] Port 5071 AGENTS.md §3 ve `.antigravity/rules/ports.md`'ye yazıldı; AGENTS §2 ve §9 (`fin`) güncellendi
- [ ] TRM-14: MOD-0220 `lookup-validation` yanıtında `ApprovalStatus` + MOD-0220 sözleşme belgesi
- [ ] MOD-0140 `supplier.openapi.yaml` 1.3.0 main'de (MVP-2)
- [ ] SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (O-22; platform-shared-services) — kapsamı soran kod ve AC-32 bu yola bağlı
- [ ] O-4 (v1 kaynak kapsamı), O-5 (MOD-0143 — KARAR D26, kapandı), O-6 (mükerrer anahtar), O-7 (onay kademesi),
  O-13 (tek aktif istisna), O-14 (alacak dekontu) sahip kararıyla yazıldı (O-8 K2 → D14/3 ile kapandı)
- [ ] O-12 kod alma kaynağı commit'i kararlaştırıldı ve sahip MOD-0119 için kod alma iznini verdi (D1)
- [ ] RCS-001 ortak kod servisi ve şirket kısa kodu kaynağı; `APD` öneki §3'e yazıldı (O-9) — yoksa Create yazılamaz
- [ ] Para birimi küsurat hanesi kaynağı (O-10) — DL-1 Create için
- [ ] MOD-0023'te MOD-0119 iş akışı şablonu (sahibi platform-shared-services, O-11) — DL-2 için
- [ ] MOD-0023 pack §17 (TRM-15) **onaylandı** (CT-APR-02/01; yarış yanıtı politikası KARAR D21/4, D22/3) — DL-2 için. **Testlerin geçmesi ready-for-dev engeli değildir** (gap 16/P2): §17 kodu MOD-0023 sahibinin WP'siyle gelir (D25/1) ve DL-2 kabulünden önce main'de olmalıdır; AC-W5…W14 DL-2 uygulama kabul kapısındadır; geçene kadar yinelenen anahtar hatası `Unknown` (D21/7)
- [ ] Silmeme politikası (KARAR D21/1) MOD-0023 pack §17'de onaylı; silinmiş kayıtlar `isDeleted` / `includeDeleted=true` ile okunur (D25/2; AC-W13) — DL-2 için; AC-W1 (silmeme mimari testi) DL-2 uygulama kabul kapısındadır (gap 16/P2); kapanmazsa 404 / boş okumada anahtarlı yeniden yazma kapalı kalır (gap 6/P1-a)
- [ ] MOD-0023 okuma eşleme alanları (KARAR D21/3) §17'de onaylı — DL-2 için; kod MOD-0023 sahibinin WP'siyle gelir (D25/1), AC-W2/W3 uygulama kabul kapısındadır (gap 16/P2); yoksa okuma niyeti kapatmaz (gap 6/P1-b)
- [x] MOD-0143 `totalAmount` tabanı — üretici ölçümü net (`PRODUCER-ANSWER-MOD0143-01.md`), KARAR D26/1; kabul durumları D26/2.
  Sözleşme 1.1.1 açıklaması + kayıt tutarlılık kontrolü procurement tarafında (D26/3) — ready-for-dev engeli değildir
- [ ] Tüketici onayı slotları (paket §P → §U) "Implementation Notes"taki tabloya göre kapatıldı — level 3'ten önce hepsi

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Ortak tanımlar: MOD-0126 pack @bu commit sha256 ZIP manifestine bakın. (Makbuz, niyet durum sözlüğü, Reconcile 3a
  kuralları ve servis ortak portlar orada tanımlıdır; bu pack ile çelişirse o sürüm bağlayıcıdır — gap 6/P2-c.)
- Producer paketi: MOD-0119 v1.1 (TRM-B7-02), `source-batch7-v1.0/…/mod0119-transfer-packet-v1.1/`, `manifest.json`
  sha256 `f075aeafbdb17ffa3015e6dea248f0bd407bc0db0064ce94df10c37f581c1d31` (bu oturumda `shasum -a 256` ile ölçüldü);
  paket dosyası `MOD-0119-TRANSFER-PACKET-v1.1.md` sha256 `733db5135b67c4c0f05318b274d724c72b687592f322646d8c24fc6a64bb88b4`
  (manifest girdisiyle eşit). v1.0 (`2d4435d2…`) yerini aldı.
- Kaynak commit: `218acaf5f3d78476bd16b9f4b1e5767d6b41d11f` (bağlı, derlenmedi); fark commit'i `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d`
  (3 commit sonrası; bağlı 34 kod dosyasının 32'si eşit; farklı iki dosya `Api/Controllers/CustomBaseController.cs` +9 AP
  dışı satır, `Infrastructure/DependencyInjection.cs` +2 AP dışı kayıt — paket §O). Kaynak commit'ler yalnız bu makinede;
  `git bundle` yedeği önerildi.
- Derleme/test kanıtı (devralınmış): R1 @ `da4abb20`, BUILD ok, **MOD-0119 TEST 251/251** (`SUMMARY-v1.1.md@ebdc38b8`,
  paket §O; DCP §9). Bağlı baytların testi değildir (paket başındaki uyarı).
- Kod alma (D1, kopya değil): her alınan dosya `path@commit` + sha256 ile iz kaydına yazılır; alınacak kod v1 kapsamıyla
  sınırlıdır (varlık 1–3, durum korumaları, doğrulayıcının para birimi/satır/negatif tutar kuralları); aşağıdaki liste taşınmaz.
- Kaynak paketin 18 "sağlamadıkları" maddesinden (paket §L) bu paket şunları **Recovery'de sağlar**: §L.1 sözleşme
  (AP-INVOICE), §L.2–3 denetim (yol b, aynı işlem), §L.4 LE uygunluğu (MOD-0220), §L.5 SoD/iş akışı (MOD-0023), §L.8
  silme (soft delete), §L.11 tek aktif istisna (O-13 kabul edilirse), §L.17 rota izni (integration-agent). Kalanlar v1
  dışıdır (Follow-up).

### Level-1 koşulları (`MOD-0119-v1.1.md`) → bu paket

1. "Production-live" raporları yerel geliştirme yığınıdır; gerçek kullanıcı ve taşınacak veri yok (paket §J.1) → **veri
   taşıma yok** (Entity Fields → indeksler, AC-31); kaynak etiketi Recovery hazırlığı sayılmaz (OI-13 Recovery tarafında
   bu yolla kapanır; kaynak tarafındaki gerilim sahibin kaynak kararıdır).
2. NoOp denetim ve önce-yaz-sonra-denetle → AUD-001 yol b, aynı işlem (Audited Events, AC-17, AC-18, AC-29); 2026-11-30
   kaynak sınırlaması taşınmaz.
3. MOD-0003 (karar 0.3) ve ERP-FIN-BUNDLE (karar 0.6) açık → Dependencies'te ENGEL, Ready-for-dev'de engel, O-1/O-2.
4. Sözleşme ABSENT → `ap-invoice.openapi.yaml` (AC-28).

### Tüketici onayı slotları (paket §P, 12 slot, borçlu Natig (K1)) → v1 ilgisi

| Slot | v1 ilgisi | Kapanış yolu |
|---|---|---|
| AUTH (MOD-0018) | **gerekli** — `finance-treasury.ap-*` anahtarları (`read-tenant-wide` açık atama dahil), rol şablonu | O-20; kaynağın `ap_*_scope` talepleri taşınmaz, yerine SEC-002 veri kapsamı (D14/1, O-22) |
| LE (MOD-0220) | **gerekli** | TRM-14 sözleşme belgesi |
| SUPPLIER (MOD-0140) | **gerekli** — `supplierId` anlamı, ilişki durumu; görüntü kopyası | 1.3.0 + MOD-0140 sahibi onayı |
| BANK (MOD-0289) | v1'de yok (banka referansı taşınmaz) | Follow-up |
| PAYMENTS (MOD-0126) | v1'de yok | Follow-up (`ApLinked` dikişi) |
| SETTLEMENT (MOD-0269) | v1'de yok | Follow-up |
| FORECAST (MOD-0125) | v1'de yok | Follow-up |
| WORKFLOW (MOD-0023) | **gerekli** | şablon (O-11) + MOD-0023 sahibi |
| AUDIT (MOD-0021) | **gerekli** — CentralAudit; "canlıyken NoOp mu" sorusu Recovery'de veri taşınmadığı için kaynak tarafında kalır | MOD-0126 DL-1 + AC-17/29 |
| EVIDENCE (MOD-0028/0031) | **gerekli** (yalnız okuma) | DL-3 başında ölçüm |
| DATA-CONTRACT (MOD-0003) | **gerekli** | karar 0.3 (O-1) |
| FIN-BUNDLE | **gerekli** | karar 0.6 (O-2) |

### Taşınmayacaklar (do-not-carry)

1. NoOp denetim kaydedici ve iş işlemi dışında, iş verisinden sonra denetim (`AccountsPayableSecurity.cs:269-273`,
   `AccountsPayableFoundationService.cs:1785→1790, 1948→1953@218acaf5`; paket §H, §L.2–3); kaynağın 2026-11-30 süreli
   sınırlaması.
2. "Production-live" / "GO WITH WAIVER" / "CLOSED" kaynak etiketleri (paket §J.1) — hazırlık ya da canlı kullanım kanıtı
   değildir; veri taşıma, geri doldurma, cutover yok.
3. Yer tutucu güvenlik: `FailClosedAccountsPayableLegalEntityAccessGuard` (`AccountsPayableSecurity.cs:284-294`),
   `FoundationOnlyAccountsPayableSodPolicy` (:328-338) — Recovery'de gerçek MOD-0220 ve MOD-0023 bağdaştırıcısı; yer tutucu
   "yapıldı" sayılmaz.
4. `[Authorize]`-only denetleyici ve elle `EnsureAccess` (`AccountsPayableController.cs:17, 215`).
5. Gövdede `TenantId` / `LegalEntityId` ve gövde kiracısı karşılaştırması (`AccountsPayableDtos.cs:17-18`,
   `AccountsPayableController.cs:221`); talep kapsam kümeleri `ap_*_scope` (paket §G).
6. Başarısız sonucu `Success` olarak dönmek (`ApInvoiceImportRuntimeStatuses.ValidationRejected` /
   `FoundationDependencyBlocked`, `AccountsPayableFoundationService.cs:321-329, 499-507`) ve "HTTP 200 + `Succeeded=false`"
   tekrar kodları (paket §E).
7. İstemciden alınan ve birbirine karşı kontrol edilmeyen tutarlar (`AccountsPayableDtos.cs:28-38`) — Recovery'de sunucu
   hesabı.
8. Tekil olmayan iş imzası indeksi ve koddaki mükerrer kontrolü (`AccountsPayableMongoIndexes.cs:70-79`,
   `AccountsPayableFoundationService.cs:465-481`) — yarışa açık.
9. İstemci idempotency anahtarının iş varlığında tutulması (`ApInvoiceControlRecord.IdempotencyKey`, `AccountsPayableEntities.cs:47`)
   ve kiracı + anahtar tekilliği (`ux_mod0119_import_idempotency_key`, `AccountsPayableMongoIndexes.cs:98-103`).
10. `string` aktör kimliği (`AccountsPayableSecurity.cs:302, 324`), `string OwnerUserId` (`AccountsPayableEntities.cs:95`),
    `DateTime` zaman damgaları.
11. Break-glass isteği (`AccountsPayableBreakGlassRequest`, `AccountsPayableSecurity.cs:314-317`) ve her türlü SoD muafiyeti.
12. Uygunluk için kayıt anı kopyası (`ApSupplierFinancialProfileSnapshot`, `AccountsPayableEntities.cs:71-82`).
13. Banka hesabı referansları ve maskelenmiş değerler (`AccountsPayableEntities.cs:50-53`), maskeleme politikası
    (`AccountsPayableFoundationValidator.cs:55-68`) — v1'de alan yok, politika gereksiz.
14. Tek aktif kuralı olmayan istisna kaydı (`ix_mod0119_active_exception_lookup`; kaynak S-OD-2) — O-13 kabul edilirse.
15. `_LayoutBackbone` ve kaynak ekran (`Views/ERP/Finance/AccountsPayable/Index.cshtml`, `_Filter.cshtml`,
    `wwwroot/assets/js/ERP/Finance/AccountsPayable/{api,index,runtime}.js`); 0 `Localizer` sabit metin (paket §I).
16. `ap.*` izin anahtarları, `MOD0119_AP_*` işlem kodları (`AccountsPayableSecurity.cs:70-88`), `/api/v1/mod0119` rotaları,
    5058 portu, `mod0119.*.v1` şema sabitleri, `MOD0119_*` hata önekleri.
17. Temel pack izni olmadan eklenmiş gateway rotaları ve denetleyicide olmayan yöntemleri ileten rota (`ocelot.json:1189-1236@218acaf5`,
    commit `4f26fc40`; paket §A T22-2, OI-12).
18. Aynı serviste paketi olmayan MOD-0125 kodu ve başka modül kayıtları (`Infrastructure/DependencyInjection.cs:2-15`, paket §D).
19. Zaman aşımı yapılandırılmamış HTTP istemcileri (paket §L.9 sınırları; Recovery'de açık zaman aşımı).

### Tasarım notları

- **Neden elle kayıt + MOD-0143 referansı (O-4 önerisi):** kaynak AP, bir dış ERP'den gelen faturaları içe aktaran ve
  kontrol eden bir modüldür (AP-09, AP-10, import-failures). Recovery'de PO'lu fatura yakalama ve eşleştirmenin SoR'u
  MOD-0143'tür (`invoice-match.openapi.yaml` "AP/ödeme YÜRÜTMEZ"), dış ERP kaynağı adlandırılmamıştır ve MOD-0037 /
  MOD-0003 kararları yoktur. Bu yüzden v1, borcun kendisini (AP kaydı) Recovery'nin kendi kaynaklarından kurar; içe
  aktarma ayrı bir kaynak sistem kararı ve sözleşmesiyle açılır.
- **MOD-0143 ile çift kayıt:** PO'lu faturada satırlar AP'de yeniden girilir (tutar MOD-0143 toplamına, O-5'te doğrulanan
  net / brüt tabanında eşitlenir; taban doğrulanana kadar PO'lu fatura onaya ilerlemez). Bu
  bilinçli bir v1 sadeleştirmesidir; MOD-0143 satırlarının AP'ye otomatik kopyası O-5'in alt seçeneğidir.
- **Uygunluk okuması ile commit arası yarış:** okuma tx1'den önce yapılır; tedarikçi ilişkisi okuma ile commit arasında
  değişirse kayıt o anki geçişte eski okumayla commit olabilir. Sonraki her geçiş (Validate, Submit, Approve) yeniden
  okuduğu için yanlış uygunluk onaya ulaşamaz; R8 son kapıdır.
- MOD-0023 başlatanın onayını zaten reddediyor; AP bunu yeniden yazmaz, yalnız MOD-0023'te olmayan kuralları (oluşturan,
  son güncelleyen, gönderenin reddi) ekler ve gönderme çözümleyicisinde aday dışı bırakır (domain-config "modül kendi SoD
  kontrolünü icat etmez" ile tutarlı: başlatan kuralı miras, AP kuralları ek).
- `target: 2027-01-29` planlama yer tutucusudur, sahip kararı değildir; TRM-S2 karar 0.3'e bağlıdır.

## Follow-up Items

- **AP → MOD-0126 ödeme dikişi:** `Approved` faturadan ödeme önerisi (kaynak `ApPaymentProposalCandidate`), MOD-0126'da
  `ApLinked` kaynak türünün açılması, `PaymentProposed` / `Closed` durumları, `OpenAmount` azaltımı. Her iki paketin
  değişikliği ve MOD-0289 banka referansı sözleşmesi gerekir; bu paket kapsamı değildir.
- Ödeme ve tahsilat durumu olayları (kaynak AP-16 MOD-0126, AP-17 MOD-0269) — üreticiler açılınca, sözleşmeli ve
  idempotent alım.
- Dış ERP içe aktarma, içe aktarma hataları ve kaynak kontrol toplamı mutabakatı (AP-03…06, AP-09, AP-10) — kaynak sistem,
  MOD-0037 ve MOD-0003 kararıyla (O-4).
- Nakit çıkışı projeksiyonu (AP-07, AP-12) — MOD-0124 Cash Positioning / MOD-0125 Cash Forecasting tüketicisi adlandırılınca.
- Kanıt paketi manifesti, kilitleme, dışa aktarma ve saklama sınıfı (AP-08, AP-13…15; kaynak `FINANCE_AP_7Y` yalnız üst veri).
- GL fiş referansı ve yevmiye (MOD-0118, karar 0.7).
- Kesinti alanları (iskonto, stopaj/tevkifat, teminat, ücret) ve vergi kodu/oranı (O-15, O-16).
- Onay tutar kademesi ve kiracı politika ayarı (O-7; MOD-0126 D11/1 modeli aday).
- Faturalar arası istisna kuyruğu ekranı, istisna atama ve kapanış (`Assigned`, `Closed` vb.).
- Tek ekranda şirketler arası birleşik AP listesi (izni `finance-treasury.ap-invoices.read-tenant-wide` — D14/1 ile bu
  pakette; birleşik görünüm ve şirket filtresi ekranı sonra).
- Güvenilir servis kimliği (MOD-0033-FU02) gelince arka plan uzlaştırma süpürücüsü (İ2).
- CentralAudit reddedilen komut kaydı (`Outcome = Denied`) — BL-501 sahibine (G-AUD-1).
- Yeniden başlatması engellendiği için `Unknown` kalan niyetin (ve tuttuğu alacak dekontu rezervasyonunun) kalıcı
  kapatılması için açık iptal yolu (gap 4/G2) — sahip kararıyla ayrı komut.
- Ödeme dikişi açılınca alacak dekontu kapasite kuralının ödenmiş tutarla birlikte yeniden yazılması (brüt tavan →
  kalan borç).
- Görev Merkezi köprüsü — onay görevlerinin panoda görünmesi.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | **MOD-0003 Data Contract Registry (karar 0.3)** — ENGEL | fonla / süreli T32 sapması / ertele | süreli T32 sapması (sahip + bitiş tarihi) + `docs/analysis/contracts/`'ta dondurulmuş sözleşmeler (domain-config önerisi); **karar verilmedi** | sahip (K1) |
| O-2 | **ERP-FIN-BUNDLE sahipliği (karar 0.6)** — ENGEL | GL / AP / AR / ayrı sahip | AP kendi `AP-INVOICE` sözleşmesini sahiplenir; bundle kararı gelince eşlenir; **karar verilmedi** | EA |
| O-3 | GL (MOD-0118) kapsamı (karar 0.7) | içeri (MOD-0001 ile) / dışarı | dışarı (karar 0.7 önerisi); AP v1 fiş üretmez; **karar verilmedi** | sahip (K1) |
| O-4 | v1 kaynak kapsamı: dış ERP içe aktarma, içe aktarma hataları, mutabakat (AP-03…06, AP-09, AP-10) | v1'de / sonra | sonra; v1 elle kayıt + MOD-0143 referansı (Tasarım notları) | sahip (K1) |
| O-5 | MOD-0143 uygun durumları ve tutar kuralı | durum kümesi; `Σ Net = totalAmount` / `Gross = totalAmount` / satır kopyası | **KARAR D26 (2026-10-25):** `{Matched, MatchedWithinTolerance}`; `Σ Net = totalAmount` (taban net — üretici ölçümü); tedarikçi + para birimi eşit; satırlar elle; `ClearedForPayment` sözleşme çözüm türünü gösterene kadar kapalı | sahip + MOD-0143 (procurement) sahibi |
| O-6 | Mükerrer fatura anahtarı | (şirket, tedarikçi, belge türü, normalize no) / + fatura tarihi / kaynak imzası (tarih + tutar) | (TenantId, LE, SupplierId, DocumentType, InvoiceNumberNormalized), silinmemiş kayıtlarda tekil; `Rejected` de engeller | sahip (K1) |
| O-7 | Onay tutar kademesi | v1 tek kademe / MOD-0126 D11/1 modeli | v1 tek kademe; kademe Follow-up | sahip (K1) |
| O-8 | AP Approve / Reject K2 mi | evet / hayır | **KARAR (D14/3): evet** — AP'de parayı etkileyen kararlar (onay, ret; açılırsa iptal/silme ve elle ödeme kaydı) K2 | CT + sahip |
| O-9 | RCS-001 tür öneki ve ortak kod servisi | `APD` / başka | `APD` (§3 tablosunda ve repoda çakışma yok, ölçüldü 2026-10-24; `INV` Yatırım Dosyası'nda); servis BL-565 | sahip + CT (`.antigravity` onayı) |
| O-10 | Para birimi küsurat hanesi kaynağı (MOD-0126 O-11 ile ortak) | MOD-0048 eklemeli alan / başka | MOD-0048'e eklemeli alan (D9); **TBD** | MOD-0048 sahibi |
| O-11 | MOD-0023 AP şablon kodu, çok adımlı onay adımları, HTTP zaman aşımı | — | şablon MOD-0023 sahibince; zaman aşımı 15 sn (MOD-0126 ile aynı ayar); **TBD** | MOD-0023 sahibi |
| O-12 | Kod alma kaynağı commit'i | `218acaf5` derlenip test edilir / `da4abb20` (AP satırları eşit, 251/251) | `da4abb20` — D1 "bağlanan commit'te derlendiği ve testleri geçtiği" şartı için ya bağlama `da4abb20`'ye taşınır ya da `218acaf5` derlenir | sahip (K1) |
| O-13 | İstisna tek aktif kuralı (paket OI-09; kaynak S-OD-2) | tek aktif / kaynak gibi çoklu | fatura + tür başına tek aktif (tekil kısmi indeks) | sahip (K1) |
| O-14 | Alacak dekontu ve onay kapsamı | orijinal faturaya bağ zorunlu / serbest; onay her belgede / `NotRequired`; kapasite tavanı orijinal brüt / onay anındaki OpenAmount | bağ zorunlu (aynı tedarikçi + para birimi, `Approved`); her belge türü onay ister; onaylı + bekleyen dekontlar orijinalin **brüt** tutarını aşamaz (gerekçe Runtime Constraints → Alacak dekontu kapasitesi), rezervasyon Submit'te, OpenAmount azaltımı onayda, orijinalin `Version` CAS'ıyla sıralanır | sahip (K1) |
| O-15 | Vergi | satır vergi tutarı elle / vergi kodu + oran kaynağı | v1 elle tutar; vergi motoru GL / e-fatura kararıyla | sahip (K1) |
| O-16 | Kesinti alanları (iskonto, stopaj/tevkifat, teminat, ücret) | v1'de / sonra | sonra | sahip (K1) |
| O-17 | Gönderme öncesi kanıt zorunluluğu | her belge / yalnız PO'suz / yok | her belge türünde en az 1 kanıt (fatura görüntüsü), yoksa 422 `AP_EVIDENCE_REQUIRED` | sahip (K1) |
| O-18 | `AuditCategory` değeri (MOD-0126 O-6 ile ortak) | eklemeli `Finance` / başka | eklemeli `Finance` (Platform, D9); **TBD** | CT + Platform sahibi |
| O-19 | Fatura tarihi kuralı | ileri tarih serbest / yasak | `InvoiceDate ≤ bugün` (UTC); `DueDate ≥ InvoiceDate` (kaynak kuralı) | sahip (K1) |
| O-20 | AP rolleri ve rol şablonları | — | AP operatörü / onaylayıcı / yönetici; bağımlı servis okuma izinleri şablona dahil; **TBD** | sahip + MOD-0018 sahibi |
| O-21 | Servis iskeletini ve ortak yapı taşlarını hangi paket açar | MOD-0126 DL-1 / AP DL-1 | **KAPANDI (D14/2 ile tutarlı; gap 4/G5):** tek sahibi ve yazarı MOD-0126 DL-1 — iskelet, `FinanceTreasuryCommandReceipt` + indeksleri, `IFinanceTreasuryDataScopeSource`, ortak LE / para birimi / kod portları, CentralAudit kaydı; AP DL-1 onu bekler ve bunları yeniden tanımlamaz. "Hangi paket önce kodlanırsa" kuralı yoktur | sahip (K1) |
| O-22 | SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (kapsamın kendisi **KARAR (D14/1)**: `LegalEntityId`, `read-tenant-wide` ayrı izin) | Platform kapsam ucu / servis içi çözücü kaydı | Platform sahibiyle kararlaştırılır; servis ortak tek karar (MOD-0126 O-19, MOD-0120 O-14, MOD-0124 ile aynı); `NoOp` kaydı yasak (kapsamı kapatır) | sahip + platform-shared-services |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | AP kaydı çekirdeği ve doğrulama | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Backend File Convention, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar |
| DL-2 | onay akışı ve onay ayrılığı (SoD) | Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention |
| DL-3 | bekletme, istisna, kanıt ve denetim kanıtı | Audited Events |
| DL-4 | kiracı ekranı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events tablosundaki denetim kaydı DL-1'in ilk komutuyla gelir, bölüm DL-3'te K2 sabotaj testi ve AUD-001 §8
dev kanıtıyla kapanır; Validation Rules'un alan ve MOD-0143 kuralları DL-1'de, SoD kuralları DL-2'de uygulanır).

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: `Features/ApInvoice/` içinde Create/Update/Delete/BulkDelete/Validate + liste/detay sorguları,
  Domain/Persistence, MOD-0220 / MOD-0140 / MOD-0143 / MOD-0048 bağdaştırıcıları, RCS kod portu, `ap-invoice.openapi.yaml`.
  **Neden ilk:** borç kaydı ve mükerrer koruması modülün temelidir; onay akışı ona dayanır. Önkoşul: Ready-for-dev
  engelleri (0.3, 0.6, DCP, **MOD-0126 DL-1** — iskelet ve servis ortak yapı taşları, tek sahibi o dilim, port, kod alma,
  TRM-14, MOD-0140 1.3.0, O-9, O-10, O-22). Alacak dekontunun Validate ön kontrolü (kapasite, orijinal kuralı) bu dilimdedir.
  Bitiş: AC-1…6, 15 (Create kısmı), 17 (tek aşamalı), 18…22, 28, 31, 32, 33 (Create / Update / Validate kısmı), 40.
- **DL-2** — Yollar: Submit, Reconcile, Approve / Reject / RequestInfo, SoD politika servisi, MOD-0023 bağdaştırıcısı
  (zaman aşımlı), trace sorgusu. **Neden ayrı:** SoD ve belirsiz sonuç en riskli kısımdır; bağımsız inceleme tek başına
  yapılabilsin. Alacak dekontu rezervasyonu (Submit), alacaklama (Approve) ve bırakma (Reject / ret / iptal) bu dilimdedir.
  Önkoşul: DL-1, O-7, O-11, O-14, MOD-0023 şablonu (K2 sınıfı D14/3 ile kararlaştırıldı), MOD-0023 pack §17 (TRM-15) onayı — kod MOD-0023 sahibinin WP'siyle gelir (D25/1); AC-W1/W2/W5…W14 geçişi dilimin **kabul kapısıdır, önkoşulu değil** (gap 16/P2) (KARAR D21/1–4, D22/2–3; gap 5/G2, gap 6/P1-a, P1-b). Bitiş:
  AC-41, AC-7…15, 17 (iki aşamalı), 30, 33 (Submit, kararlar, Reconcile kısmı), 34, 35, 36, 37, 38, 39.
- **DL-3** — Yollar: Hold / ReleaseHold, istisna komutları ve sorgusu, LinkEvidence + MOD-0031 bağdaştırıcısı, Submit'e
  kanıt kuralı (O-17 "evet" ise bu dilimde eklenir; DL-2'de Submit kanıtsız çalışır ve bu fark DL-2 kaydına yazılır), Submit / Approve'daki
  bekletme ve engelleyici istisna kapısı (R9 — DL-2'de bu kayıtlar henüz yoktur), K2 sabotaj testi, dev ortamı denetim
  kanıtı. **Neden ayrı:** MOD-0031 davranış ölçümüne ve O-13/O-17'ye bağlı; DL-2'yi bekletmesin.
  Önkoşul: DL-2, O-13, O-17, O-18. Bitiş: AC-16, 17 (sabotaj), 29, 33 (Hold, istisna, kanıt kısmı).
- **DL-4** — Yollar: `Views/FinanceTreasury/ApInvoices/**`, JS, resx (7 dil), MVC proxy, sayfa yardımı, nav anahtarları;
  Gateway rotası integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** ekran API sözleşmesine
  bağlıdır, arka uç değişmeden ilerler. Önkoşul: DL-1 (liste/oluşturma), DL-2 (karar düğmeleri), DL-3 (bekletme, istisna),
  Gateway rotası. Bitiş: AC-23…27.
