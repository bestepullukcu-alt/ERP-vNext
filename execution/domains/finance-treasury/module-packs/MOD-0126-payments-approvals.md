---
id: MOD-0126
name: Payments & Approvals
domain: finance-treasury
service: Diten.FinanceTreasuryService
service_port: 5071
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
owner: depo sahibi (K1) / Treasury Intake Control Tower
branch: feature/fin/mod-0126-payments-approvals
started: 2026-10-24
target: 2026-12-15
form_field_count: 16
slice: "TRM-S1 — ödeme partisi + doğrulama + MOD-0023 onay akışı + SoD + onay belgesi/kanıt + serbest bırakma (banka dağıtımı KAPALI)"
---

# MOD-0126 — Payments & Approvals

> **Durum: `draft` (planlama belgesi).** Bu paket kod, servis iskeleti, port kaydı, gateway rotası ya da kaynak
> projeden kod alma izni vermez. Kod ancak paket `approved` / `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02)
> ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten sonra yazılır; MOD-0126 için kod alma izni **verildi** (KARAR
> D27/5; D1 yöntemi). Engeller: "Ready-for-dev Checklist" ve "Açık kararlar".
>
> **D27 (2026-10-25, sahip) — kapı kararları verildi:** karar 0.3 → **KARAR D27/1** süreli T32 sapması: Treasury
> sözleşmeleri `docs/analysis/contracts/` altında dondurulmuş dosya olarak tutulur ve MOD-0003 gelince oraya kaydedilir;
> **bitiş: MOD-0003 çıkana kadar, en geç Treasury v1 sonu (hangisi önce gelirse)**. Karar 0.5 → **KARAR D27/2** (AR kısıtlı
> yerel MVP, sapma S-2). Karar 0.6 → **KARAR D27/3** (v1'de ERP-FIN-BUNDLE yok; her modül kendi sözleşmesini yayımlar;
> ortak paket GL gelince ayrı karar). Karar 0.7 → **KARAR D27/4** (GL / MOD-0118 kapsam dışı; fiş / yevmiye yok,
> `VoucherReference` yok). D1 kod alma → **KARAR D27/5** (MOD-0126, MOD-0119, MOD-0120, MOD-0124 için kaynak S `da4abb20`
> doğrulanıp uyarlanarak alınabilir; kod yine yalnız pack `ready-for-dev` olunca ve dilim sırasıyla yazılır). Kayıt:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` D27 (bu revizyonla aynı commit'te). Bu revizyon pack'i `ready-for-dev` yapmaz.
>
> **BANK-DISPATCH dondu (D27/7, D27/8):** `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 (`rail` alanı çıkarıldı;
> bankacılık sahibi 0.1 için K1) — dosya çalışma ağacında, bu oturumda okundu (`bank-dispatch.md:5`). Bu paketteki bütün
> MOD-0269 atıfları dondurulmuş dosyaya işaret eder; bölüm numaraları (§5, §5.1, §6) taslak rev 3 ile aynıdır (ölçüldü:
> `bank-dispatch.md:167, 181, 195`).
>
> **Bağlanan kararlar (yeniden karar verilmez):** yerleşim TREASURY-MOD0126-PLACEMENT-01
> (`docs/records/decisions/2026-10/treasury-mod0126-placement-decision-01.md`); D1 (doğrula, sonra uyarlayarak al),
> D2 (servis `Diten.FinanceTreasuryService`), D3 (dal kısa kodu `fin`), D5 (MOD-0269 = önce sözleşme, dağıtım kapalı)
> — `treasury-intake-owner-decisions-01.md`; D9 (başka modüldeki eklemeli değişiklik onayı), D10/1 (ödeyen şirket
> `OperationalStatus = Active` **ve** `ApprovalStatus = Approved`), D10/2 (MOD-0220 eki, TRM-14), D10/3 (port 5071),
> D14/1 (veri kapsamı: kapsam alanı `LegalEntityId`, kiracı geneli ayrı `…read-tenant-wide` izni — SEC-002), D14/2
> (servis genelinde tek `FinanceTreasuryCommandReceipt`; tam tanımı bu pakettedir, MOD-0119/0120/0124 buna başvurur) —
> `treasury-intake-owner-decisions-02.md`. Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0126-v1.2.md` (koşul 1–5 bu pakette karşılanır,
> bkz. "Implementation Notes → Level-1 koşulları"). DCP: `execution/portfolio/delivery-capability-packs/UNASSIGNED-finance-treasury-intake.md`
> (taslak; dilim TRM-S1).
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-24, bu worktree:**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0126 --name "Payments & Approvals"` →
> `OK  MOD-0126: proven against Blueprint/registry.` (çıkış 0). Kayıt dosyasında MOD-0126 satırı henüz yok; satır bu
> paketle yazılmaz (ayrı iş, D8).
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.2 (`mod0126-transfer-packet-v1.2/`,
> manifest sha256 `660ab4c00af74ccfdac298979d6a3cd20d9b9ceeca2d526540072e10d4cabcc8`, bu oturumda yeniden ölçüldü) ve
> kaynak kod `git -C /Users/natig/Projects/ERP-vNext show da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d:<yol>`. Atıflar
> `paket §X` ya da `Dosya.cs:satır@da4abb20` biçimindedir; Recovery atıfları `yol:satır` (bu worktree, HEAD `d43096a6b`).

## Module Summary

Ödeme partisi (payment batch) hazırlama, doğrulama, MOD-0023 iş akışıyla onaylama, onay ayrılığı (SoD), onay belgesi
(attestation) ve serbest bırakma sistem-of-record'u. Blueprint 8.8 W-3, Treasury & Cash.

- **Hedef kullanıcı:** hazine operatörü (partiyi hazırlar ve gönderir), onaylayıcı (1. kademe), yüksek tutar onaylayıcı
  (2. kademe), hazine yöneticisi (SoD muafiyeti, uzlaştırma, serbest bırakma).
- **Çekirdek:** SoD. Partiyi hazırlayan, son güncelleyen ve gönderen kişi o partide karar veremez; eşik üstü parti ikinci
  kademe izni ister; eşik **tek**, **para birimli** ve **politika sürümlü**dür; muafiyet izin + gerekçe + kanıt ister.
- **Kapsam ilkesi:** kaynaktaki 11 HTTP işlemi (PP-01…PP-11, paket §C) Recovery standartlarına uyarlanır; kaynak
  kusurları taşınmaz ("Implementation Notes → Taşınmayacaklar").
- **Bankaya ödeme gitmez.** Serbest bırakma MOD-0269 arayüz sözleşmesinden geçer; bağdaştırıcı "yapılandırılmamış" döner
  ve deneme kaydedilir (D5).

## Ownership and Boundaries

- **In-scope:** `PaymentBatch` + `PaymentInstruction` (elle hazine kaynağı, `MANUAL_TREASURY`); doğrulama; MOD-0023'e
  gönderme (yerel niyet kaydı + idempotency anahtarı + belirsiz sonuç + uzlaştırma); karar (onay, ret, kanıt isteme,
  yükseltme) ve SoD; SoD muafiyeti; kanıt bağlantısı (MOD-0031 referansı); onay belgesi; serbest bırakma denemesi
  (dağıtım kapalı); istisna kaydı ve iz (trace) okuma; kiracı ekranı (liste + oluştur + düzenle + detay).
- **Out-of-scope:** gerçek banka dağıtımı, banka geri çağrıları, ekstre (MOD-0269, D5 — dört kapalı akış varsayılmaz);
  banka ana verisi ve hesap çözümleme (MOD-0289, MDM — bu modül yalnız opak referans taşır); AP kaynaklı parti
  (`ApLinked`, MOD-0119 — kaynak enum'da var, `PaymentProposalEnums.cs:3-7@da4abb20`; bu dilimde reddedilir); nakit
  tahmini besleme (MOD-0124/0125); GL kaydı (MOD-0118 — KARAR D27/4, kapsam dışı); MOD-0146 Payment Run Support (dışlama korunur);
  tedarikçi, müşteri, şirket ana kayıtları (MOD-0140, MOD-0149, MOD-0220); Görev Merkezi köprüsü (WC-D1; follow-up).
- **Sahip olunan sözleşme:** `PAYMENT-BATCH` (yeni, `docs/analysis/contracts/payment-batch.openapi.yaml`, x-owner
  MOD-0126) — kaynakta resmi sözleşme yok (paket §B "ABSENT"; level-1 koşul 5). Tek yazıcı MOD-0126 (K15).
- **Tüketilen sözleşmeler:** MOD-0220 `lookup-validation` (+ TRM-14 `ApprovalStatus`), MOD-0023 iş akışı başlatma ve
  görev geçişleri, MOD-0031 kanıt bağlantısı, MOD-0048 para birimi, MOD-0269 dağıtım arayüzü (taslak, TBD), Platform veri
  kapsamı (`IDataScopeResolver`, SEC-002 — D14/1).

## Owned Objects

### Varlıklar (kaynak 8 sınıf → Recovery karşılığı)

| # | Kaynak sınıf (`PaymentProposalEntities.cs@da4abb20`) | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `PaymentBatch` (:5-31) | **taşınır** — `PaymentBatch : EntityBase` | çekirdek |
| 2 | `PaymentInstruction` (:33-53) | **taşınır** — `PaymentInstruction : EntityBase` | satır |
| 3 | `PaymentApprovalDecision` (:68-97) | **taşınır + `State`** (niyet/sonuç) | karar niyeti uzak çağrıdan önce yazılır |
| 4 | `PaymentException` (:99-114) | **taşınır** | istisna kuyruğu |
| 5 | `PaymentAttestation` (:116-130) | **taşınır**, yalnız `payment.attestation.v2` / manifest v2 | Recovery'de tarihsel v1 kayıt yok (paket §B, v0.8) |
| 6 | `PaymentEvidenceLinkReference` (:132-149) | **taşınır** (değer nesnesi) | MOD-0031 referansı |
| 7 | `PaymentOutboxMessage` (:55-66) | **taşınmaz** | kaynakta yalnız MOD-0021'e denetim aktarımıdır (paket §F "audit relay to MOD-0021 only"); Recovery'de `CentralAudit` outbox'ı (`central_audit_outbox`) yerini alır (AUD-001 K4) |
| 8 | `PaymentOutboxAttemptLog` (:151-163) | **taşınmaz** | aynı neden; teslim denemelerini `CentralAuditDispatcher` tutar |
| + | — | **yeni** `PaymentWorkflowStartIntent` | uzak başlatmadan önce yerel niyet (level-1 koşul 2) |
| + | — | **yeni** `PaymentDispatchAttempt` | serbest bırakma denemesi; MOD-0269 "yapılandırılmamış" sonucu kaydı (D5) |
| + | — | **yeni** `FinanceTreasuryCommandReceipt` | **servis ortak** istemci idempotency makbuzu (D12/2, D14/2); `Pending`/`Completed` yaşam döngüsü — Entity Fields. Tek tanım bu pakettedir; MOD-0119, MOD-0120, MOD-0124 aynı tipi kullanır |

### Kaynak işlemleri (PP-01…PP-11, paket §C) → Recovery

| Kaynak | Kaynak rota (`api/v{v}/mod0126/…`) | Recovery komut / sorgu | Recovery uç (Gateway `/api/…`) | Karar |
|---|---|---|---|---|
| PP-01 | POST payment-batches/manual | `CreatePaymentBatchCommand` | `POST /api/payment-batches` | taşınır |
| PP-02 | GET payment-batches | `GetPaymentBatchListQuery` | `GET /api/payment-batches` | taşınır (DataTable server) |
| PP-03 | GET payment-batches/{id} | `GetPaymentBatchByIdQuery` | `GET /api/payment-batches/{id}` | taşınır |
| PP-04 | POST …/validate | `ValidatePaymentBatchCommand` | `POST /api/payment-batches/{id}/validate` | taşınır |
| PP-05 | POST …/admin-direct-decision | — | — | **düşürüldü — sahip kararı D11/2** (O-1): kaynak kendini "Phase-1 golden-flow proxy only. Not final MOD-0023 SoD" diye tanımlıyor (`PaymentLifecycleService.cs:13-15@da4abb20`), SoD servisini çağırmıyor ve `PassedByAdminProxy` yazıyor (paket §H.2 (4)) |
| PP-06 | POST …/attestation | `GeneratePaymentAttestationCommand` | `POST /api/payment-batches/{id}/attestations` | taşınır |
| PP-07 | GET …/trace | `GetPaymentBatchTraceQuery` | `GET /api/payment-batches/{id}/trace` | taşınır |
| PP-08 | POST …/submit-workflow | `SubmitPaymentBatchCommand` | `POST /api/payment-batches/{id}/submit` | **uyarlanır**: yerel niyet önce, idempotency anahtarı, belirsiz sonuç |
| PP-09 | POST …/workflow-decision | `ApprovePaymentBatchCommand`, `RejectPaymentBatchCommand`, `RequestPaymentBatchEvidenceCommand`, `ApprovePaymentBatchWithSodWaiverCommand` | `POST /api/payment-batches/{id}/{approve\|reject\|request-evidence\|approve-with-sod-waiver}` | **bölünür**: karar türü başına bir komut (her biri kendi izni ve denetim olayıyla); muafiyet ayrı komut. **Escalate v1 dışı (D22/1)**: MOD-0023'te görev düzeyinde karşılığı yok; yükseltme MOD-0023 SLA kurallarıyla olur |
| PP-10 | GET payment-exceptions | `GetPaymentExceptionListQuery` | `GET /api/payment-exceptions` | taşınır |
| PP-11 | POST outbox/dispatch-once | — | — | **düşürülür** (O-2): elle "bir kez gönder" ucu kaynak denetim outbox'ına aittir; Recovery'de iletim `CentralAuditDispatcher`'ındır |

### Recovery ekleri (kaynakta yok)

| Komut | Uç | Neden |
|---|---|---|
| `UpdatePaymentBatchCommand` | `PUT /api/payment-batches/{id}` | Compact `Edit` sayfası; yalnız `Draft`/`Validated`, gönderilmemiş parti (O-3) |
| `DeletePaymentBatchCommand` | `DELETE /api/payment-batches/{id}` | Golden Reference silme seti; yalnız hiç gönderilmemiş parti, soft delete (O-3) |
| `BulkDeletePaymentBatchCommand` | `DELETE /api/payment-batches/bulk` | Golden Reference zorunlu toplu silme; aynı kural (O-3) |
| `ReconcilePaymentBatchWorkflowCommand` | `POST /api/payment-batches/{id}/workflow/reconcile` | belirsiz başlatma / karar sonucunu MOD-0023'ten okuyup kapatır |
| `LinkPaymentBatchEvidenceCommand` | `POST /api/payment-batches/{id}/evidence-links` | kaynakta kanıt bağlama oluşturmanın içinde ve yerel kayıttan sonra, işlem dışında (`PaymentProposalService.cs:235-298@da4abb20`); Recovery'de ayrı, idempotent komut |
| `ReleasePaymentBatchCommand` | `POST /api/payment-batches/{id}/release` | serbest bırakma denemesi → MOD-0269 portu → "yapılandırılmamış" + kayıt (D5) |

- **Sorgular:** `GetPaymentBatchListQuery`, `GetPaymentBatchByIdQuery`, `GetPaymentBatchTraceQuery`,
  `GetPaymentExceptionListQuery`.
- **DTO'lar:** `PaymentBatchModels.cs` tek dosyada (Golden Reference): `PaymentBatchListItemDto`, `PaymentBatchDetailDto`,
  `PaymentInstructionDto`, `PaymentBatchWriteRequest`, `PaymentInstructionWriteRequest`, `PaymentDecisionRequest`,
  `PaymentSodWaiverRequest`, `PaymentWorkflowStateDto`, `PaymentDecisionDto`, `PaymentExceptionDto`,
  `PaymentAttestationDto`, `PaymentDispatchAttemptDto`, `PaymentBatchTraceDto`. DTO ve isteklerde `TenantId` ve
  `LegalEntityId` **yoktur**.
- **Portlar (Application):** `ILegalEntityEligibilityPort` (MOD-0220), `IPaymentWorkflowPort` (MOD-0023),
  `IPaymentEvidencePort` (MOD-0031), `ICurrencyReferencePort` (MOD-0048), `IBankDispatchPort` (MOD-0269),
  `IRecordCodePort` (RCS-001 ortak kod servisi), `IPaymentSodPolicyProvider` (tek eşik),
  `IFinanceTreasuryDataScopeSource` (SEC-002 veri kapsamı, D14/1 — **servis ortak** tek çözüm noktası; MOD-0119/0120/0124
  aynı arayüzü kullanır).
- **Frontend rotası:** `/FinanceTreasury/PaymentBatches` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`).
- **İzinler:** "Authorization Convention" tablosu.

### Durum makinesi (`PaymentBatchStatus`, kaynakla aynı 10 değer — `PaymentProposalEnums.cs:9-21@da4abb20`)

| Durum | Değer | Recovery anlamı | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 0 | taslak | oluşturma, güncelleme, kesin başlatma reddi (önceki durum Draft ise) | Validate, Update, Submit, Delete |
| Validated | 1 | doğrulandı | Validate başarılı | Update (→ Draft), Submit, Delete |
| Submitted | 2 | **yerel başlatma niyeti yazıldı; MOD-0023 sonucu henüz kesin değil** (Pending ya da Unknown) | Submit tx1 | kesin başarı → PendingApproval; kesin ret → önceki durum; Unknown → Reconcile |
| PendingApproval | 8 | MOD-0023 örneği başladı, karar bekleniyor | kesin başlatma başarısı / Reconcile | kararlar |
| PartiallyApproved | 9 | son adım olmayan onay | Approve (son adım değil) | kararlar |
| EvidenceRequested | 6 | kanıt istendi | RequestEvidence | kararlar |
| Escalated | 7 | yükseltildi — **v1'de girişi yok** (D22/1; değer enum'da korunur, kaynakla aynı 10 değer) | — | — |
| Approved | 3 | son onay (terminal karar) | Approve (son adım) | Release denemesi (durum değişmez) |
| Rejected | 4 | ret (terminal) | Reject | — |
| Cancelled | 5 | MOD-0023 örneği iptal / kapatıldı (terminal) | yalnız Reconcile, MOD-0023 örneği iptal ya da terk edilmiş olarak kapandı dediğinde | — |

- Kaynakta `Submitted` hiçbir yerde atanmıyor (`git grep PaymentBatchStatus.Submitted` @da4abb20 → 0 atama; gönderme
  doğrudan `PendingApproval` yazıyor, `PaymentWorkflowService.cs:190@da4abb20`). Recovery bu değeri "niyet yazıldı, sonuç
  bekleniyor" anlamında kullanır; yeni durum eklenmez.
- **Niyet ve karar durumları — tek geçiş matrisi (gap 2/G5):** başlatma niyeti ve karar niyeti aynı sonuç sözlüğünü
  kullanır. Açık durumlar: `Pending` (başlatma) / `Requested` (karar) ve `Unknown`. Kapalı durumlar: `Started` (başlatma) /
  `Recorded` (karar), `Refused`, `Cancelled`.

| Niyet | Açık → | tx2 (kesin sonuç) | belirsiz | Reconcile (yalnız `ReconcilePaymentBatchWorkflowCommand`) | Sonuç olayı |
|---|---|---|---|---|---|
| `PaymentWorkflowStartIntent.Status` | `Pending` | `Started` · `Refused` | `Unknown` | `Unknown`/`Pending` → `Started` · `Refused` · `Cancelled` | `…submitted` · `…workflow-start-refused` · `…workflow-start-cancelled` |
| `PaymentApprovalDecision.State` | `Requested` | `Recorded` · `Refused` | `Unknown` | `Unknown`/`Requested` → `Recorded` · `Refused` · `Cancelled` | `…approved`/`…approved-with-sod-waiver`/`…rejected`/`…evidence-requested` · `…decision-refused` · `…decision-cancelled` |

  `Refused` = MOD-0023 kesin reddetti; `Cancelled` = MOD-0023 örneği ya da görevi iptal edilmiş / kapanmış bulundu
  (yalnız Reconcile atar, yalnız uzak tarafın **kesin** iptal/kapanış kaydıyla — o örnek için MOD-0023 iptal geçiş
  kaydı ya da `CloseAbandonedWorkflowStart` kapanışı; bulunamadı / 404 / zaman aşımı / belirsiz okuma `Cancelled`
  sayılmaz, niyet `Unknown` kalır — Runtime Constraints 3a). Kaynaktaki `RefusedByWorkflow`
  adı kullanılmaz.
- **Servis geneli durum sözlüğü (sahip kararı D14/2, ortak niyet altyapısı):** bu matris `Diten.FinanceTreasuryService`'in
  **tek** niyet durum sözlüğüdür. Niyet tipleri modüle özgü kalır (MOD-0119, MOD-0120, MOD-0124'ün başlatma, karar ve
  dışa aktarma niyetleri) ama durum adları buradakilerdir: açık `Pending` · `Requested` · `Unknown`; kapalı `Started` ·
  `Recorded` · `Refused` · `Cancelled`. Yeni durum adı eklenmez; `Cancelled` yalnız Reconcile'da atanır.
- `PaymentSodResult` (kaynak 10 değer, `PaymentProposalEnums.cs:31-43@da4abb20`) Recovery'de: `Passed`,
  `FailedSelfApproval`, `FailedSubmitterApproval`, `FailedUpdaterApproval` (yeni), `FailedRoleConflict`,
  `FailedAmountTier`, `Waived`. **Taşınmaz:** `NotEvaluatedByWorkflowProxy`, `PassedByAdminProxy` (PP-05 ile birlikte),
  `NotApplicableForManualTreasury`, `DeferredExternalSource` (kaynakta karşılığı olan akış bu dilimde yok).

## Entity Fields

Taban: servisin kendi `EntityBase`'i (`entity-base-template.md`): `Id`, `TenantId`, `IsDeleted`, `DeletedAt`,
`CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş modülü olduğu için `CreatedBy` / `UpdatedBy` (`Guid?`,
token `sub`'ından; DTO'dan değil) **zorunlu**. Kaynak tabanı `TenantEntity` (`D/Common/TenantEntity.cs:3-15@da4abb20`)
`DateTime` + `long RowVersion` + `string CreatedBy = "system"` taşır; Recovery'de `DateTimeOffset` (UTC), `int Version`
(iyimser eşzamanlılık) ve `Guid` aktör kimliği kullanılır. Not: kaynak tabanda `DeletedAt` alanı **vardır** (`:8`) ama
hiçbir yol `IsDeleted = true` yazmıyor ve silme ucu yok (paket §F). Recovery'de soft delete `IsDeleted` + `DeletedAt`
(UTC) birlikte yazılır.

**Kapsam alanları (her varlıkta):** `TenantId` (`ITenantContext`) ve `LegalEntityId` (isteğin `X-Legal-Entity-Id`
başlığı, MOD-0220'den doğrulanmış; Recovery R-2 deseni, örn. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Loads/LoadContextMiddleware.cs:61-81`)
sunucuda yazılır; gövde ya da sorgu parametresiyle gelen `tenantId` / `legalEntityId` 400 ile reddedilir. Alt kayıtlar
(talimat, karar, istisna, niyet, deneme, onay belgesi) partinin `LegalEntityId`'sini taşır ki her sorgu Tenant + LE ile
süzülsün.

**Para (SOP §18.8, domain-config "Para / decimal"):** tüm tutarlar `decimal` (Mongo `Decimal128`); `float`/`double`
yasak. Para birimi **parti başına tek**: ISO 4217, 3 büyük harf; her talimat partinin para birimini taşır ve farklıysa
reddedilir (`PaymentProposalService.cs:77-85@da4abb20`). Hassasiyet: para biriminin küsurat hanesi MOD-0048'den;
fazla haneli tutar **yuvarlanmaz, reddedilir** (`:94-99@da4abb20`). `TotalAmount` ve `PaymentCount` sunucu hesaplar,
istekten alınmaz. Kur çevrimi yok (FX tarihi uygulanmaz). Saat dilimi: zaman damgaları UTC; `RequestedValueDate`
yalnız tarihtir (`DateOnly`), saat dilimi çevrimi yapılmaz.

### PaymentBatch (`payment_batches`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| BatchCode | string | RCS-001 OTOMATİK `{ŞİRKET}-{TÜR}-{SAYAÇ}`; tür öneki önerisi `PAY` (O-9); kullanıcı giremez/değiştiremez; silinen partinin kodu yeniden kullanılmaz | kaynak `BatchNumber` istemciden de alınıyor (`PaymentProposalService.cs:126-128@da4abb20`) ve sayaç `CountAsync()+1` (`:471-475`) — taşınmaz |
| SourceKind | enum `ManualTreasury`=1 | sunucu sabitler; `ApLinked` reddedilir | `:369-377@da4abb20` |
| DefaultSourceBankAccountRef | Guid | zorunlu, boş olmayan; **opak** MOD-0289 referansı | `:384-387@da4abb20` |
| PaymentMethod | string | zorunlu, trim, ≤ 32; değer listesi TBD (O-13) | `:389-398@da4abb20` |
| CurrencyCode | string(3) | zorunlu, ISO 4217, upper | `:400-405@da4abb20` |
| TotalAmount | decimal | sunucu: talimatların toplamı; > 0 | `:130@da4abb20` |
| PaymentCount | int | sunucu: talimat sayısı | — |
| RequestedValueDate | DateOnly | zorunlu; geçmiş tarih kuralı TBD (O-12) | `:407-410@da4abb20` |
| Status | `PaymentBatchStatus` | durum makinesi | — |
| RiskRating | enum Low/Medium/High | varsayılan Low | `:145@da4abb20` |
| EvidenceRequired | bool | true ise gönderme/son onay öncesi en az bir kanıt bağlantısı ister | — |
| Notes | string? | trim, ≤ 2000 (kaynakta sınır yok; öneri) | — |
| SubmittedBy / SubmittedAt | Guid? / DateTimeOffset? | sunucu (tx1) | — |
| UpdatedByBeforeSubmit | Guid? | gönderme anındaki son güncelleyen (SoD R3) | yeni |
| WorkflowInstanceId / WorkflowStatus / WorkflowCorrelationId | Guid? / string? / string? | yalnız kesin başlatma sonucundan | `:185-189@da4abb20` |
| SodPolicyVersion / Tier2ThresholdAmount / Tier2ThresholdCurrency / IsTier2 | string / decimal / string(3) / bool | gönderme anında sabitlenir (pinned); karar aynı değeri kullanır | yeni (koşul 1) |
| EvidenceLinks | `PaymentEvidenceLinkReference[]` | MOD-0031 referansı; ikili veri yok | :24 |
| ReleaseSequence | int | 0'dan başlar; her **yeni** Release denemesi aynı işlemde +1 (sürüme koşullu, CAS); makbuz tekrarı artırmaz; üst sınır 999 999 999 — ulaşılmışsa 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED` (artırma ve port çağrısı yok; gap 8/P2-b); MOD-0269 `dispatchKey = mod0126:{batchId:N}:release:{ReleaseSequence}` (gap 7/P1-b) | yeni |
| ReleaseBlockedForReview | bool | varsayılan false; bağdaştırıcı `NotConfigured` dışı sonuç döndürürse true olur ve sonraki Release'i kapatır (D17/2) | yeni |
| CreatedBy / UpdatedBy | Guid? | token `sub` | — |

### PaymentInstruction (`payment_instructions`)

| Alan | Tip | Kural |
|---|---|---|
| PaymentBatchId, LineNumber | Guid, int | sunucu; 1..500 satır (`PaymentProposalService.cs:15,418-427@da4abb20`) |
| ManualOutflowReference | string | zorunlu, trim, ≤ 64 (öneri; kaynakta yalnız boş olmama, `:437-441`) |
| PayeeType | enum LegalEntityCounterparty=1, Supplier=2, Employee=3, Other=99 | tanımlı değer (`:455-459`) |
| PayeeReferenceId | Guid? | `PayeeType ≠ Other` ise zorunlu (öneri); varlık doğrulaması TBD (O-14) |
| DestinationReferenceId | Guid? | alacaklı banka hesabının **opak** MOD-0289 referansı |
| SourceBankAccountRef | Guid | zorunlu, boş olmayan, opak (`:443-447`) |
| Amount | decimal | > 0; küsurat ≤ para birimi hanesi (`:87-99`) |
| CurrencyCode | string(3) | sunucu partiden yazar; istekte farklıysa ret |
| DocumentDirection | enum Payment | bu dilimde yalnız `Payment` (`:171`) |
| RemittanceReference | string? | trim, ≤ 140 (öneri) |
| SourceDocumentRef | string? | trim, ≤ 128 (öneri) |
| ManualOverride | bool | varsayılan false |
| ValidationStatus / ValidationMessages | enum / string[] | Validate yazar; mesajlar resx anahtarıdır, serbest İngilizce metin değil |

> **`form_field_count: 16` → `golden_reference: compact`.** Oluştur/düzenle formunda kullanıcının doldurduğu alanlar
> (kaynak istek modeli `CreateManualPaymentBatchRequest` / `CreateManualPaymentInstructionRequest`,
> `PaymentProposalContracts.cs:5-34@da4abb20`; ekran indeksi paket §I): **başlık 7** — DefaultSourceBankAccountRef,
> PaymentMethod, CurrencyCode, RequestedValueDate, RiskRating, EvidenceRequired, Notes; **talimat satırı 9** —
> ManualOutflowReference, PayeeType, PayeeReferenceId, DestinationReferenceId, SourceBankAccountRef, Amount,
> RemittanceReference, SourceDocumentRef, ManualOverride. **Sayılmayanlar:** BatchCode (RCS-001 OTOMATİK), SourceKind
> (sunucu sabit), LegalEntityId (oturum şirketi, `X-Legal-Entity-Id`), satır CurrencyCode (partiden türetilir),
> Idempotency-Key (başlık; yalnız `FinanceTreasuryCommandReceipt`'te saklanır) / CorrelationId (başlık), TotalAmount / PaymentCount (sunucu), kaynaktaki `ApSource` / `Gateway` /
> `ForecastFeed` / `EvidenceLinks` (bu dilimde istek modelinde yok). 16 > 8 → Compact (ayrı Create / Edit / Details).

**Banka verisi yasağı:** hiçbir varlık IBAN, hesap numarası, BIC/SWIFT, banka adı ya da sağlayıcı sırrı tutmaz; yalnız
opak Guid referansı (domain-config "Banka verisi bu domain'de saklanmaz"). Çözümleme MOD-0289'dan ve yalnız dağıtım
anında yapılır (dağıtım bu dilimde kapalı).

### PaymentWorkflowStartIntent (`payment_workflow_start_intents`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| PaymentBatchId | Guid | — |
| IdempotencyKey | string | deterministik: `mod0126:{batchId:N}:start:{SubmissionSequence}`; aynı niyet için hiç değişmez |
| SubmissionSequence | int | partinin kaçıncı gönderimi (kesin ret sonrası yeniden gönderme yeni sıra alır) |
| Status | enum Pending/Started/Refused/Cancelled/Unknown | tek geçiş matrisi (`Cancelled` yalnız Reconcile; D14/2 hizalaması) |
| PreviousBatchStatus | `PaymentBatchStatus` | kesin retteki geri dönüş (Draft/Validated) |
| CandidatePrincipalIds / ExcludedPrincipalIds | Guid[] | çözümleyici sonucu (SoD R1–R4) |
| SodPolicyVersion, Tier2ThresholdAmount, Tier2ThresholdCurrency, IsTier2 | — | sabitlenen politika |
| WorkflowInstanceId | Guid? | yalnız kesin sonuçtan |
| LastOutcomeCode / LastAttemptAt / AttemptCount | string? / DateTimeOffset? / int | `PAYMENT_WORKFLOW_START_*` kodları |
| CorrelationId | string | — |

### PaymentApprovalDecision (`payment_approval_decisions`)

Kaynak alanlar (`PaymentProposalEntities.cs:68-97@da4abb20`) korunur: `Decision`, `ReasonCode`, `Comment`,
`EvidenceRefs`, `EvidenceLinks`, `SodResult`, `SodCheckDetail` (resx anahtarı + parametre), `RemoteIdempotencyKey`
(kaynakta `IdempotencyKey`; Recovery'de MOD-0023 görev geçişine giden **uzak** anahtar, deterministik
`mod0126:{decisionId:N}:transition` — istemci anahtarı değildir),
`DecidedBy` (**Guid**, kaynakta string), `DecidedAt`, `RequestFingerprintVersion`, `RequestFingerprint`, `WorkflowInstanceId`,
`WorkflowTaskId`, `WorkflowTransitionId`, `WorkflowIsFinalStep`, `SodPolicyVersion`, `WaiverRequested`,
`WaiverReasonCode`. **Çıkarılan:** `DecisionPath` (tek yol kalır: iş akışı), `AuditOutboxId`, `AuditEventId` (CentralAudit
taşır). **Eklenen:** `State` (Requested/Recorded/Refused/Cancelled/Unknown — tek geçiş matrisi yukarıda),
`IsWaiverCandidate` (bool — onaylayıcı muafiyet adayı olarak atandıysa, D13).

### FinanceTreasuryCommandReceipt (`finance_treasury_command_receipts`) — servis ortak idempotency makbuzu (gap 2/G2–G3, D14/2)

**Tek ortak tip (sahip kararı D14/2):** `Diten.FinanceTreasuryService`'in bütün modülleri (MOD-0126, MOD-0119, MOD-0120,
MOD-0124) **bu tek tipi ve tek koleksiyonu** kullanır; modül önekli makbuz tipi açılmaz. Tip bu paketin DL-1'iyle (servisi
açan dilim) servis ortak yapı taşı olarak gelir; **tek sahibi ve yazarı MOD-0126 DL-1'dir** (Repo Scope, gap 4/G5); diğer
paketler burayı başvuru gösterir, yeniden tanımlamaz ve önce açmaz.
Tipin ya da kurallarının değişmesi dört paketin birlikte değişikliğini ister.

| Alan | Tip | Kural |
|---|---|---|
| TenantId, LegalEntityId, ActorId, Operation, TargetId, Key | Guid, Guid, Guid, string, Guid?, string | **makbuz kapsamı** (D12/2) — altı alan; `TargetId` Create için `null`, diğerlerinde komutun hedef kaydının kimliği (bu modülde parti) |
| Operation | string | **modül nitelikli işlem adı:** `{modül}:{Komut}` — ör. `mod0126:SubmitPaymentBatch`, `mod0119:ApproveApInvoice`, `mod0120:RequestReceivableInvoiceAdjustment`, `mod0124:PublishCashPosition`. Aynı anahtar başka modülün işleminde ayrı makbuzdur; modül öneki `LinkedIntentId`'nin hangi niyet koleksiyonuna ait olduğunu da söyler |
| RequestFingerprint / FingerprintVersion | string / int | sunucuda hesaplanır |
| Status | `Pending` \| `Completed` | tek aşamalı komut: tx1'de doğrudan `Completed`; iki aşamalı komut (bu modülde Submit ve kararlar): tx1'de `Pending`, tx2'de ya da — tx2 hiç çalışmadıysa — Reconcile'da `Completed` |
| ResponseStatusCode / ResponseBody | int? / string? | **yalnız `Completed`'da** dolu; o anda istemciye verilen (ya da verilecek) HTTP yanıtı — kesin sonuç **ya da** 202 belirsiz |
| LinkedIntentId | Guid? | iki aşamalı komutta başlatma ya da karar niyetinin kimliği (niyet tipi modüle özgüdür) |
| CreatedAt / CompletedAt | DateTimeOffset / DateTimeOffset? | — |

**Değişmezlik (gap 3/G2):** `Completed` makbuz **bir daha değişmez**. Belirsiz sonuçta kaydedilen 202 yanıtı (gövdesinde
`intentId` ve durum sorgusu bağlantısı) sonsuza kadar aynı döner; işlemin **gerçek sonucu** makbuzda değil niyette
(`PaymentWorkflowStartIntent` / `PaymentApprovalDecision`) izlenir ve `GET /api/payment-batches/{id}` ile `…/trace`
üzerinden okunur. Aynı anahtarla tekrar (replay) **asla kurtarma yapmaz**; Reconcile yalnız **`Pending`** makbuzu (tx1
sonrası çökme — istemci hiç yanıt almadı) `Completed` yapar; zaten `Completed` olan makbuza dokunmaz. Bu kurallar dört
modülde aynıdır.

**İstemci anahtarı yalnız makbuzdadır** (gap 3/G1): `PaymentBatch`, `PaymentApprovalDecision`, `PaymentAttestation`,
`PaymentDispatchAttempt` istemci anahtarı alanı taşımaz. MOD-0023'e giden anahtarlar ayrı ve **uzak** anahtardır:
`PaymentWorkflowStartIntent.IdempotencyKey` (`mod0126:{batchId:N}:start:{SubmissionSequence}`) ve
`PaymentApprovalDecision.RemoteIdempotencyKey`; deterministiktir, istemciden bağımsızdır.

### PaymentException (`payment_exceptions`), PaymentAttestation (`payment_attestations`), PaymentDispatchAttempt (`payment_dispatch_attempts`)

- `PaymentException`: kaynak alanları (`:99-114@da4abb20`); `OwnerUserId` Guid?. Bir SoD reddi **tek** istisna satırı
  yazar (kaynak iki satır yazıyor, `PaymentWorkflowService.cs:390-412@da4abb20` — taşınmaz).
- `PaymentAttestation`: kaynak alanları (`:116-130@da4abb20`); `SchemaVersion = payment.attestation.v2`,
  `ContentHash` (SHA-256), `SourceSnapshotJson` (yerel anlık görüntü; banka verisi içermez), `GeneratedBy` Guid.
- `PaymentDispatchAttempt` (yeni): `PaymentBatchId`, `ReceiptId` (Release makbuzu), `ReleaseSequence`, `DispatchKey`,
  `RequestedBy` (Guid), `RequestedAt`, `Outcome` (bağdaştırıcının döndürdüğü değer olduğu gibi: `NotConfigured` ya da
  beklenmeyen `Accepted` / `Rejected` / `Unknown` — D17/2), `OutcomeCode`, `ProviderReference?`,
  `ProviderContractVersion` (MOD-0269 sözleşme sürümü). Ekleme-yalnız; güncelleme/silme yok. Talimat başına kaynak
  hesap porta olduğu gibi gider (D17/1); deneme kaydı banka verisi taşımaz.

### Mongo indeksleri (kaynak 18 ad, 4 tekil — `PaymentProposalsMongoIndexes.cs:8-28@da4abb20`)

| Recovery indeksi | Anahtar | Tekil | Kaynak karşılığı |
|---|---|---|---|
| `ux_payment_batch_code` | TenantId + BatchCode | **evet, kısmi filtre YOK** (silinen partinin kodu da dolu sayılır, RCS-001 §6) | `ux_mod0126_payment_batch_number` (kısmi `IsDeleted=false`, `:47-52` — taşınmaz) |
| `ux_finance_treasury_command_receipt_scope` | TenantId + LegalEntityId + ActorId + Operation + TargetId + Key (`finance_treasury_command_receipts`) | evet — **tek istemci idempotency kuralı** (D12/2); **servis ortak, bu paketin DL-1'i sahibidir** (D14/2); MOD-0119/0120/0124 yeniden bildirmez | kaynağın dört `ux_mod0126_*_idempotency` indeksinin (TenantId + IdempotencyKey) **yerine geçer**; o dört indeks taşınmaz |
| `ux_payment_start_intent_remote_key` | TenantId + IdempotencyKey (uzak, deterministik anahtar) | evet — MOD-0023'e aynı anahtarla tek başlatma | yeni |
| `ux_payment_start_intent_open` | TenantId + PaymentBatchId, kısmi `Status ∈ {Pending, Unknown}` | evet — partide aynı anda tek açık başlatma niyeti | yeni |
| `ux_payment_decision_open` | TenantId + PaymentBatchId, kısmi `State ∈ {Requested, Unknown}` | evet — partide aynı anda tek açık karar niyeti | yeni |
| `ix_finance_treasury_command_receipt_pending` | TenantId + Status + CreatedAt, kısmi `Status = Pending` (`finance_treasury_command_receipts`) | — | yeni (Reconcile listesi); servis ortak, sahibi bu paketin DL-1'i (D14/2) |
| `ux_payment_dispatch_attempt_key` | TenantId + DispatchKey (`payment_dispatch_attempts`) | evet — bir `dispatchKey` tek denemeye bağlanır (gap 7/P1-c; anahtar/yük çatışması yapısal olarak engellenir) | yeni |
| `ix_payment_batch_list` | TenantId + LegalEntityId + IsDeleted + Status + CreatedAt(desc) | — | `ix_mod0126_payment_batch_status` / `_created` |
| `ix_payment_batch_workflow_instance` | TenantId + WorkflowInstanceId (kısmi, var olan) | — | aynı |
| `ix_payment_instruction_batch` | TenantId + PaymentBatchId + LineNumber | — | aynı |
| `ix_payment_decision_batch` | TenantId + PaymentBatchId + DecidedAt | — | aynı |
| `ix_payment_exception_status` | TenantId + LegalEntityId + Status + Severity | — | aynı |
| `ix_payment_attestation_batch` | TenantId + PaymentBatchId + GeneratedAt(desc) | — | aynı |

İndeks kurulumu servis iskeletinin indeks barındırılan hizmetiyle yapılır (`mongo-indexing`); taşıma/geri doldurma yok
(kaynak veri taşınmaz — paket §L.13).

### Hata sözlüğü (kaynak 52 `PAYMENT_*`, `PaymentProposalErrorCodes.cs:5-56@da4abb20`; HTTP eşlemesi paket §E)

Taşınan kodlar korunur (`PAYMENT_VALIDATION_FAILED` 400, `PAYMENT_PERMISSION_DENIED` 403, `PAYMENT_IDEMPOTENCY_CONFLICT`
409, `PAYMENT_BATCH_NOT_FOUND` 404, `PAYMENT_BATCH_VERSION_CONFLICT` 409, `PAYMENT_DECISION_REASON_REQUIRED` 400,
`PAYMENT_DECISION_STATUS_INVALID` 409, `PAYMENT_BATCH_ALREADY_TERMINAL` 409, `PAYMENT_BATCH_ALREADY_SUBMITTED` 409,
`PAYMENT_WORKFLOW_TRANSITION_FAILED` 502, `PAYMENT_WORKFLOW_INSTANCE_MISMATCH` 409, `PAYMENT_WORKFLOW_TASK_INVALID` 400,
`PAYMENT_SOD_DENIED` 403, `PAYMENT_SOD_WAIVER_REQUIRED` 422, `PAYMENT_SOD_WAIVER_FORBIDDEN` 403,
`PAYMENT_SOD_WAIVER_REASON_REQUIRED` 400, `PAYMENT_TIER2_APPROVER_REQUIRED` 403,
`PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` 422, `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` 503, `PAYMENT_EVIDENCE_*`).

- **Değişen:** `PAYMENT_TENANT_FORBIDDEN` kullanılmaz — kiracılar/şirketler arası erişim **404** `PAYMENT_BATCH_NOT_FOUND`
  (multi-tenancy). `PAYMENT_WORKFLOW_START_FAILED` (kaynakta her başlatma hatası 502, `CustomBaseController.cs:57@da4abb20`)
  yalnız **kesin ret** için kullanılır.
- **Yeni:** `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` (202 — ret değildir), `PAYMENT_WORKFLOW_START_PENDING` (409 — açık
  niyet varken yeniden gönderme), `PAYMENT_DECISION_OUTCOME_UNKNOWN` (202), `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE` (422 —
  aktif değil ya da onaylı değil), `PAYMENT_LEGAL_ENTITY_UNAVAILABLE` (503 — MOD-0220 yanıt vermedi; geçiş yok),
  `PAYMENT_SOD_POLICY_UNAVAILABLE` (503 — ayar deposu erişilemiyor; ayarın yokluğu hata değildir), `PAYMENT_BANK_DISPATCH_NOT_CONFIGURED`
  (sonuç kodu, serbest bırakma yanıtında), `PAYMENT_RECORD_CODE_UNAVAILABLE` (503), `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`
  (400), `PAYMENT_DECISION_PENDING` (409 — açık karar niyeti), `PAYMENT_SOD_WAIVER_NOT_ALLOWED` (403 — R1–R3 muaf tutulamaz),
  `PAYMENT_REQUEST_IN_PROGRESS` (202 — aynı anahtarlı istek, makbuz `Pending`; kurtarma yapılmaz),
  `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH` (409 — Reconcile'ı çağıran asıl komutun aktörü değil; uzak yazma/başlatma
  çağrısı yapılmaz, niyet `Unknown` kalır), `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION` (500 — porttan
  `BankDispatchContractException` ya da sözleşme dışı istisna; işlem geri alınır), `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`
  (502 — `NotConfigured` dışı sonuç; deneme kaydedilir, parti kilitlenir, D17/2), `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW` (409 —
  kilitli partide Release; port çağrılmaz), `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED` (409 — `ReleaseSequence` üst sınırda;
  artırma ve port çağrısı yok). MOD-0269 eşlemesi: `docs/analysis/contracts/bank-dispatch.md` (FROZEN 0.1.0) §5.
- **Taşınmaz:** `PAYMENT_AUDIT_DISPATCH_*`, `PAYMENT_AUDIT_CONTRACT_UNAVAILABLE`, `PAYMENT_AUDIT_RESPONSE_INVALID`,
  `PAYMENT_OUTBOX_*` (CentralAudit), `PAYMENT_UNSUPPORTED_P0_OPERATION` (istek modelinde o alanlar yok),
  `PAYMENT_APPROVER_DIRECTORY_*` (O-10'a bağlı).
- Kullanıcıya giden ileti resx anahtarıdır; teknik metin (`403`, `Forbidden`) ekrana çıkmaz (UAS-001 §5).

## Repo Scope

Hepsi kod alma izninden ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.FinanceTreasuryService/**` — **yeni servis**, `@orchestrator /add-module` iskeleti (5 katman:
  Api / Application / Domain / Persistence / Infrastructure + `tests/`), port 5071, JWT, `TenantId` izolasyonu, 4 zorunlu
  pipeline davranışı + `AddCentralAudit<…>(RequireEnlistment = true)`, modül manifest kaydı
  (`module-self-registration-standard`). (DL-1)
  - `src/Diten.FinanceTreasuryService.Application/Features/PaymentBatch/**` (Golden Reference Compact düzeni)
  - `src/Diten.FinanceTreasuryService.Domain/PaymentBatches/**`
  - `src/Diten.FinanceTreasuryService.Persistence/PaymentBatches/**` (depolar, indeks hizmeti; her işlem geri çağrısı
    `CentralAuditTransaction.EnlistAsync` çağırır)
  - `src/Diten.FinanceTreasuryService.Infrastructure/{LegalEntities,Workflow,Evidence,Currency,BankDispatch,RecordCodes,DataScope}/**`
    (port bağdaştırıcıları; `DataScope` = SEC-002 tüketim bağdaştırıcısı, servis ortak)
  - **Servis ortak yapı taşları (D14/1, D14/2) — tek sahibi ve tek yazarı bu paketin DL-1'idir (gap 4/G5):**
    servis iskeleti; `src/Diten.FinanceTreasuryService.Application/Common/{CommandReceipts,DataScope}/**`
    (`FinanceTreasuryCommandReceipt`, `IFinanceTreasuryDataScopeSource` — okuma ve yazma kapsamı, `FinanceTreasuryDataScope`)
    ve `src/Diten.FinanceTreasuryService.Persistence/Common/CommandReceipts/**` (koleksiyon + iki makbuz indeksi
    `ux_finance_treasury_command_receipt_scope`, `ix_finance_treasury_command_receipt_pending`); servis ortak portlar ve
    bağdaştırıcıları `ILegalEntityEligibilityPort` (`Infrastructure/LegalEntities`), `ICurrencyReferencePort`
    (`Infrastructure/Currency`), `IRecordCodePort` (`Infrastructure/RecordCodes`), `DataScope` bağdaştırıcısı; CentralAudit
    kaydı (`AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)`, `FinanceTreasuryCentralAuditScope`,
    servis defteri). MOD-0119, MOD-0120 ve MOD-0124 bunlara **bağımlıdır** (MOD-0126 DL-1, onların DL-1'inin önkoşuludur)
    ve bunları **hiçbir zaman yeniden tanımlamaz ya da açmaz** — başka bir paket önce `ready-for-dev` olsa bile MOD-0126
    DL-1'i bekler. Değişiklik dört paketin birlikte onayını ister.
  - `src/Diten.FinanceTreasuryService.Api/Controllers/PaymentBatchesController.cs`, `PaymentExceptionsController.cs`
  - `src/Diten.FinanceTreasuryService.Api/CentralAudit/FinanceTreasuryCentralAuditScope.cs`
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` — yeni defter (`## İzler` satırı
  `finance-treasury-merkezi-iletim | b | işaret | ICentralAuditDeclaration`, borç 0, istisna 0) + aynı değişiklikte
  `tests/architecture/audit-ledger/CT-DECISIONS.md` satırı ve `AuditTrailStandardTests` `AcceptedTrails` /
  `PinnedCounts` (SupplyChain E-2 deseni, CT-DECISIONS 2026-10-21) — **CT kararı ister**. (DL-1)
- `docs/analysis/contracts/payment-batch.openapi.yaml` — yeni üretici sözleşmesi (x-owner MOD-0126). (DL-1)
- `frontend/Diten.Web/Views/FinanceTreasury/PaymentBatches/**`,
  `frontend/Diten.Web/wwwroot/assets/js/FinanceTreasury/PaymentBatches/**`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/PaymentBatches/PaymentBatchesIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/FinanceTreasury/PaymentBatchesController.cs` (same-origin MVC proxy),
  `PageHelpResource.{7 dil}.resx` içinde `Help.PAYMENTBATCHES.*` ve `Services/Help/PageHelpCatalog.cs` satırı,
  menü/nav anahtarları `Nav.Domain.FINANCETREASURY`, `Nav.Module.PAYMENTSAPPROVALS`, `Nav.Page.PAYMENTBATCHES`. (DL-4)

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9; burada yazılmaz):
MOD-0220 TRM-14; MOD-0023 iş akışı şablonu; `ocelot.json` rotaları (integration-agent); AGENTS.md §2/§3/§9 ve
`.antigravity/rules/ports.md` (5071); `record-coding-standard.md` §3 tablosuna `PAY` satırı; Platform `AuditCategory`
eki (O-6); FinanceTreasury servisinden SEC-002 veri kapsamı çözücüsünün tüketim yolu (platform-shared-services, O-19).
Bkz. "Dependencies".

## Protected Paths

- `.antigravity/**` (port bandı, RCS-001 §3 satırı ve kural değişiklikleri ayrı onay)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.Platform/**` (MOD-0023, MOD-0021, MOD-0048, MOD-0031, `IDataScopeResolver` — platform-shared-services), `services/Diten.MdmService/**`
  (MOD-0220, MOD-0289 — master-data-management), `services/Diten.ProcurementService/**` (MOD-0140; tek yazar MVP-2, K15),
  `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**` (CentralAudit yalnız tüketilir) ve diğer domain servisleri
- `frontend/Diten.Web/Views/Archive/**`, `Controllers/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone` (kaynak
  layout'u; Recovery'de kullanılmaz)
- `docs/analysis/contracts/supplier.openapi.yaml` ve diğer üretici sözleşmeleri (tüketilir, yeniden tanımlanmaz)
- `execution/registries/**`, `execution/domains/finance-treasury/domain-config.md`, CT kuyruğu — bu paketle değişmez
- Kaynak depo `/Users/natig/Projects/ERP-vNext` ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz

## Dependencies

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c) | Bu pakete etkisi |
|---|---|---|---|
| MOD-0023 Workflow Designer | başlatma + görev geçişleri + başlatanın onayını reddetme | **var.** `POST /api/v1/workflow/instances` (`WorkflowDefinitionsController.cs:87-93`, izin `platform.workflow.instances.start`, `WorkflowModels.cs:14`); istekte `IdempotencyKey` var (`WorkflowModels.cs:286-299`) ve aynı anahtar yeniden oynatılır (`StartWorkflowInstanceHandler.cs:76-85`); görev onay/ret uçları `tasks/{taskId}/approve|reject` (`WorkflowDefinitionsController.cs:196-243`); başlatanın onayı `SodViolation` ile reddedilir, kimlik karşılaştırması Guid (`WorkflowTaskTransitionSupport.cs:964-977`); uzlaştırma okuması `GET instances/by-objects` (`:121-132`) | v1 başlatma **kullanıcı token'ıyla genel uçtan** (O-7). MOD-0126 iş akışı şablonu (`templateCode` TBD) MOD-0023'te yayımlanmış olmalı — MOD-0023 sahibinin işi (D9) |
| Güvenilir iş akışı tüketicisi (`api/internal/v1/workflow/trusted-consumer/*`) | servis kimliğiyle başlatma, `start-result` | **var ama yalnız `Diten.MDM`'ye kilitli** — `TrustedServiceTokenValidationExtensions.cs:18`, `ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs:79`, `ServiceIdentityTokenAudiencePolicy.cs:5,16` (Paket A taslağı, `docs/roadmap/plans/erp-finance-legal-module-intake-01/PKG-A-MOD-0033-FU02-TREASURY-WORKFLOW-IDENTITY-DRAFT-v0.1.md`) | v1'de kullanılmaz; arka plan uzlaştırma süpürücüsü bu kimlik gelene kadar yok (follow-up) |
| MOD-0021 Audit / CentralAudit (BL-501, K4) | yol `b` | **var** — `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/` (`CentralAuditBehavior.cs`, işlem içi `CentralAuditTransaction.EnlistAsync`, `CentralAuditRecorder.cs:6-31`); kullananlar Inventory, SupplyChain | yeni serviste `AddCentralAudit` + defter + CT kararı (Repo Scope). Servis-başı MDM kopyası **yasak** (AUD-001 §5) |
| MOD-0220 LE `lookup-validation` | ödeyen şirket uygunluğu (D10/1) | **var** — `GET /api/legal-entities/{id}/lookup-validation`, izin `mdm.legal-entities.read` (`LegalEntitiesController.cs:48-54`); kayıtta `OperationalStatus` ve `ApprovalStatus` var (`LegalEntity.cs:49-50`); yanıtta `ApprovalStatus` **henüz yok** | **TRM-14** (D10/2): MOD-0220 pack değişikliği + eklemeli `ApprovalStatus` alanı + MOD-0220 sözleşme belgesi — MOD-0126 kapsamı değildir, eşlik eden değişikliktir; o gelmeden "onaylı" kontrolü yapılamaz → ödeme kapanır (fail-closed) |
| MOD-0269 Banks/SWIFT/Payments Gateway | serbest bırakma portu | **yok** (0 dosya; karar 0.1 = A, D5) — sözleşme **dondu**: `docs/analysis/contracts/bank-dispatch.md` (BANK-DISPATCH FROZEN 0.1.0, `rail` yok — KARAR D27/7; TRM-06 kapandı) | `IBankDispatchPort` dondurulmuş sözleşmeye göre yazılır; bağdaştırıcı yalnız `NotConfigured` döner |
| MOD-0289 Bank Master & Bank Account Registry | banka referansı çözümleme | kod yok; yerleşim MDM (D20/1), TRM-S3; okuma sözleşmesi **taslak**: `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` (BANK-ACCOUNT-LOOKUP 1.0.0 DRAFT, fikstürler F-E1…F-E9, F-503) | bu dilimde yalnız boş olmayan opak Guid; çözümleme dağıtım açılınca, sözleşme dondurulunca (follow-up) |
| MOD-0048 Reference Data (para birimi) | kod listesi + küsurat hanesi | **kısmen** — `GET /api/lookups/reference/currencies` (`TenantReferenceLookupsController.cs:18,37-41`), `LookupOptionDto` küsurat hanesi taşımıyor (`LookupModels.cs:3-9`; liste `CultureInfo`'dan, `PlatformLookupProvider.cs:358`) | küsurat hanesi kaynağı **TBD** (O-11); yoksa oluşturma `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` ile durur — sabit tablo uydurulmaz |
| MOD-0031 Evidence Linking | kanıt bağlantısı | **var** — `api/v1/evidence` (`EvidenceLinksController.cs:19`) | DL-3; bağlama sözleşmesinin idempotency (dedupe) davranışı DL-3 başında ölçülür; ölçülene kadar hiçbir kurtarma yolu MOD-0031'i yeniden çağırmaz (gap 5/G2) |
| MOD-0018 RBAC | `[HasPermission]`, izin tohumu | var | yeni `finance-treasury.*` ad alanı kaydı (O-5); `read-tenant-wide` yalnız açık atama (`ExplicitGrantOnlyPermissions`) |
| **SEC-002 veri kapsamı** (`IDataScopeResolver`, MOD-0018-FU15) | kullanıcının şirket yetkisi (D14/1) | çözücü yalnız Platform'da kayıtlı (`Diten.Platform.Application/DependencyInjection.cs` → `OrgDataScopeResolver`); ortak kütüphanedeki `NoOpDataScopeResolver` boş liste döner (`.antigravity/rules/data-scope-enforcement.md` §6); FinanceTreasury'den tüketim yolu **yok** | **ENGEL (O-19)**: SEC-002 §4 kapısı — kapsam cümlesi + kapsamı soran kod + kanıt testi olmadan `ready-for-dev` yok. Tüketim yolu servis ortaktır (`IFinanceTreasuryDataScopeSource`); dört Treasury paketi için tek karar (MOD-0120 O-14 ile aynı). `NoOp` kaydı yasak (kapsamı kapatır) |
| RCS-001 ortak kod servisi (§5.12, BL-565) | parti kodu | **yok** (Building.Blocks'ta yok; MDM'de yalnız kendi `CodeReservation`'ı, `CodeReservation.cs:5`); şirket kısa kodu (§5.4) LE kaydında ayrı alan değil (`LegalEntity.cs:11` yalnız `Code`) | **engel**: modül kendi sayacını yazamaz (RCS-001 §6). Servis gelene kadar oluşturma yazılamaz (O-9) |
| MOD-0140 Supplier (PayeeType = Supplier) | alacaklı doğrulaması | var — `POST /api/suppliers/validate` (MOD-0140 pack §3) | varlık doğrulaması O-14 (bu dilimde yalnız biçim) |
| Port 5071 | servis portu | sahip kararı D10/3; **AGENTS.md §3 ve `.antigravity/rules/ports.md` güncellenmedi** (ayrı onay, TRM-04) | iskelet port kaydından sonra |
| DCP (UNASSIGNED-finance-treasury-intake) | dilim kapısı | `draft` | DCP `approved` olmadan pack `ready-for-dev` olmaz (DCP §15) |

### Bağımlılık sözleşmeleri — kapanış tablosu (gap P2, 2026-10-24)

Her satır kapanmadan ilgili dilim başlamaz. **Sahte (mock) istemciyle geçen test, canlı entegrasyon kanıtı sayılmaz:**
"sözleşme testi" sütunu sahte istemcili testi, "canlı kanıt" sütunu gerçek servisle dev ortamında koşulmuş kaydı
(`scripts/run_record.py`) ister.

| Bağımlılık | Sözleşme dosyası / sürüm | Hash | Sahibi | Hata davranışı (MOD-0126) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|---|
| PAYMENT-BATCH (bu modül) | `docs/analysis/contracts/payment-batch.openapi.yaml` v1.0.0 — **yazılacak** | TBD (dondurulunca) | finance-treasury | — | sözleşme testi AC-29 · — | DL-1 |
| MOD-0220 LE uygunluğu + `ApprovalStatus` | MOD-0220 sözleşme belgesi — **yazılacak** (TRM-14) | TBD | master-data-management (D10/2) | 422 uygun değil · 503 erişilemez · geçiş yok | sahte LE istemcisi · dev ortamında Active+Approved / Approved-değil iki kayıt | DL-1 |
| MOD-0023 iş akışı başlatma + görev geçişi | Platform `WorkflowModels.cs` (sözleşme dosyası yok) + şablon kodu — **TBD** (O-8); anahtarla idempotentlik ölçüldü (tekil indeksler + yeniden oynatma, Runtime Constraints 3a) | Platform commit SHA'sı DL-2 başında yazılır | platform-shared-services | belirsiz → 202 Unknown · kesin ret → 502 · zaman aşımı yapılandırılır · yinelenen anahtar hatası → Unknown (ret değil) | sahte MOD-0023 (anahtar başına tek örnek / geçiş) · dev ortamında gerçek başlatma + onay + ret + **aynı anahtarlı yarış yanıtı ölçümü** (var olan örneği / geçişi döndürdüğü doğrulanmadan yinelenen anahtar hatası `Unknown`) | DL-2 |
| MOD-0023 saklama + silmeme politikası (örnek ve geçiş kaydı; gap 6/P1-a) | **KARAR D21/1–2** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/1: `WorkflowInstance` ve `WorkflowTransitionLog` hiç silinmez, TTL yok; silinmiş kayıtlar da okunur — `isDeleted` + `includeDeleted=true` (D25/2, D21/2'yi genişletir); silinmiş görülürse yeniden yazma yok | §17 onayında | platform-shared-services (politika: sahip K1, D21) | §17 onaylanıp AC-W1 geçene kadar 404 / boş okumada anahtarlı yeniden yazma **kapalı**; niyet `Unknown` | ölçüm (@ef4f3a8bc): silme yolu ve TTL yok — ölçüm politika değildir · MOD-0023 AC-W1 (mimari test, sabotajlı) + bu pack'in silinmiş kayıt testi | DL-2 |
| MOD-0023 okuma eşleme alanları (gap 6/P1-b) | **KARAR D21/3** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/3: `by-objects` ve `instances/{id}` → `IdempotencyKey`; `history` → `ApprovalTaskId` + `IdempotencyKey` (yalnız ekleme) | §17 onayında | platform-shared-services | alan kodda yoksa okuma niyeti kapatmaz → `Unknown` | MOD-0023 AC-W2/W3 + bu pack'in eşleme testi | DL-2 |
| MOD-0269 banka arayüzü | `docs/analysis/contracts/bank-dispatch.md` — BANK-DISPATCH **FROZEN 0.1.0** (KARAR D27/7; TRM-06 kapandı; D5) | dosya özeti DL-3 başında `shasum` ile yazılır (bu revizyonda ölçülmedi) | bankacılık sahibi 0.1 için K1 (KARAR D27/8); gerçek sağlayıcı seçilince ayrı sahip | her çağrı `NotConfigured`, ağ çağrısı yok | bağdaştırıcı birim testi · ağ çağrısı olmadığını gösteren test | DL-3 |
| MOD-0048 para birimi + küsurat | `GET /api/lookups/reference/currencies` + küsurat alanı — **yok** (O-11) | TBD | platform-shared-services | hane yok → 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` | sahte istemci · dev ortamında TRY/EUR/JPY | DL-1 |
| RCS-001 ortak kod servisi | **yok** (BL-565, O-9) | — | CT + `.antigravity` onayı | servis yoksa Create yazılamaz | — · — | DL-1 önkoşulu |
| SEC-002 veri kapsamı (D14/1) | Platform `IDataScopeResolver` tüketim yolu — **yok** (O-19; dört paket için tek karar) | TBD | platform-shared-services | çözücü hatası → boş kapsam, boş liste, detay / yazma 404 | kapsam testi AC-32 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-1 |
| MOD-0031 kanıt bağlama | `api/v1/evidence` (sözleşme dosyası ölçülecek) | DL-3 başında | platform-shared-services | 503 → bağlama yok, parti değişmez · belirsiz → yeniden çağrı yok (yalnız okuma) | sahte · dev ortamı · **MOD-0031 dedupe ölçümü** (aynı uzak anahtarın tekrarı tek bağlantı mı) — ölçülmeden kurtarmada yeniden çağrı açılmaz | DL-3 |
| CentralAudit | `Diten.BuildingBlocks.CentralAudit` (kod) | Building.Blocks commit SHA'sı DL-1 başında | BL-501 | yazılamazsa komut başarısız (K2) | sabotaj testi AC-17 · AUD-001 §8 dev kanıtı AC-30 | DL-1 |

## Runtime Constraints

- Servis portu **5071** (D10/3; AGENTS §3 kaydı bekliyor). Tarayıcı ve `Diten.Web` yalnız Gateway (5000) üzerinden
  çağırır; tarayıcı JS 5071'e gitmez.
- **Kiracı + şirket izolasyonu her sorguda:** `TenantId` `ITenantContext`'ten, `LegalEntityId` `X-Legal-Entity-Id`
  başlığından (MOD-0220'de doğrulanmış); başka kiracı ya da başka şirket kaydı **404**. Üretimde sabit/varsayılan kiracı
  yok.
- **Veri kapsamı (SEC-002, sahip kararı D14/1) — kapsam cümlesi:** kapsamı taşıyan alan **`LegalEntityId`** (parti ve
  bütün alt kayıtları); çevrilen kapsam türü yalnız **`LegalEntity`** (`OrgUnit`, `Position`, `ManagerChain` çevrilmez →
  daraltır, sızdırmaz; birim→şirket birleştirmesi uydurulmaz); `IsInclude = false` olan `LegalEntity` kapsamı o şirketi
  **kapatır**. Kullanıcı yalnız yetkili olduğu şirketlerin kayıtlarını görür: istek başlığındaki şirket kullanıcının
  kapsamında değilse parti ve istisna listeleri **boş** (200, 0 satır), detay / iz / her yazma **404**
  `PAYMENT_BATCH_NOT_FOUND`; çözücü hatası, kiracı ya da kullanıcı yokluğu → boş kapsam (süzgeçsiz sorgu değil). Kiracıdaki
  tüm şirketleri görmek ayrı izindir: `finance-treasury.payment-batches.read-tenant-wide` (yalnız açık atama —
  `ExplicitGrantOnlyPermissions`; tek üretici `FinanceTreasuryDataScope.TenantWideScope()`). Kapsam servis genelinde
  **tek yerde** (`IFinanceTreasuryDataScopeSource`, MOD-0119/0120/0124 ile ortak; modül yalnız kendi `read-tenant-wide`
  anahtarını verir) çözülür; okumalar (liste, detay, iz, istisna listesi) **okuma kapsamını**, her yazma **yazma
  kapsamını** kullanır (aşağıda); sayfalama ve toplamlar daraltılmış kümeden hesaplanır. Referans uygulama:
  `WorkReportScope` / `WorkReportScopeSource` (SEC-002 §5).
- **Okuma kapsamı ile yazma kapsamı ayrıdır (gap 4/G1; D14/1 ile tutarlı — `…read-tenant-wide` şirketleri *görme*
  iznidir, yazma izni değildir):** `IFinanceTreasuryDataScopeSource` iki ayrı sonuç verir: **okuma kapsamı** = kullanıcının
  kendi şirket yetkileri ya da `read-tenant-wide` varsa kiracıdaki tüm şirketler (`TenantWideScope()`); **yazma kapsamı**
  = yalnız kullanıcının kendi şirket yetkileri — `TenantWideScope()` yazma kapsamına **hiçbir zaman** girmez (tek üretici
  okuma tarafında kalır). Her yazma komutu (Create, Update, Delete, BulkDelete, Validate, Submit, Approve,
  ApproveWithSodWaiver, Reject, RequestEvidence, Reconcile, kanıt bağlama, onay belgesi, Release) hedef kaydın
  `LegalEntityId`'si için **katman 1**'i ister: `X-Legal-Entity-Id` hedef partinin şirketine eşit **ve** kullanıcının o
  şirkette kendi yetkisi var. Yalnız `read-tenant-wide` sayesinde görünen bir partiye yazma → **404**
  `PAYMENT_BATCH_NOT_FOUND` (kapsam dışı kuralıyla aynı; varlık sızdırılmaz, başka adım çalışmaz). Bu ayrım servis ortak
  yapı taşının parçasıdır (MOD-0126 DL-1; dört paket aynı kuralı uygular).
- **Ödeyen şirket uygunluğu güncel durumdur, kayıt anındaki kopya değildir (TP-001 T14):** Create, Submit, her karar ve
  Release'te MOD-0220 `lookup-validation` çağrılır; `OperationalStatus = Active` **ve** `ApprovalStatus = Approved` değilse
  `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE` (422); MOD-0220 yanıt vermezse `PAYMENT_LEGAL_ENTITY_UNAVAILABLE` (503) — geçiş yok.
  Sözleşme: LEGAL-ENTITY-LOOKUP 1.1.0 (`docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md`,
  MOD-0220 pack §22, TRM-14; sınıflandırma tablosu sözleşme §2). Tutarlı 200 + `approvalStatus = APPROVED` → uygun;
  tutarlı 200 + `DRAFT` / `SUBMITTED` / `REJECTED` ya da 404 → 422 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`; geçersiz ya da
  çelişkili 200 (`approvalStatus` yok / `null` / boş / bilinmeyen; `legalEntityId` yok ya da farklı; `referenceable = false`;
  `lifecycleState ≠ ACTIVE`; ayrıştırılamayan gövde), 401/403, 5xx, zaman aşımı → 503 `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`
  (kapalı-güvenli: belirsiz yanıt asla "onaylı" sayılmaz). Test: her satır için ayrı vaka (AC-39).
  **Reconcile bu kuraldan muaftır** (sahip kararı D15/2): yeni işlem başlatmaz, daha önce kontrol edilip başlatılmış
  işlemin uzak sonucunu kaydeder; şirket arada pasifleşse de niyet kapanabilmelidir. **Muafiyetin sınırı (gap 4/G2, D15/2
  metniyle tutarlı — muafiyet "yeni işlem başlatmayan" kayda dayanır):** muafiyet yalnız Reconcile'ın uzakta **var olan**
  bir örneğin / geçişin sonucunu kaydettiği yol içindir. Okuma örnek/geçiş göstermezse ve Reconcile uzak yazmayı aynı
  anahtarla yeniden yapacaksa (yalnız anahtarla idempotent olduğu ölçülmüş MOD-0023 başlatma / geçiş) bu **yeni bir
  uzak işlemdir**: önce şirket uygunluğu (D10/1, D14/4), SoD + izin ve asıl aktör yeniden denetlenir (Runtime
  Constraints 3a).
- Soft delete (`IsDeleted` + `DeletedAt` UTC, filtrede Id + TenantId). Okumalar `IsDeleted = false`.
- **Idempotency (sahip kararı D12/2 — her yazma komutunda zorunlu):** Create, Update, Delete, BulkDelete, Validate,
  Submit, Reconcile, her karar, kanıt bağlama, onay belgesi ve Release `Idempotency-Key` başlığı **ister**; başlık yoksa
  400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`. Sunucu anahtar **üretmez**.
  - **Makbuz kapsamı:** `(TenantId, LegalEntityId, ActorId, Operation, TargetId, Key)` — `TargetId` Create için boş,
    diğerlerinde parti kimliği. Tekil indeks bu altılıdadır; aynı anahtar başka aktör/işlem/hedef için ayrı makbuzdur.
  - **Makbuz (`FinanceTreasuryCommandReceipt`, servis ortak — Entity Fields, D14/2):** istek parmak izi (sunucuda hesaplanır, muafiyet girdilerini içerir —
    `PaymentWorkflowService.cs:744-746@da4abb20`) ve **yaşam döngüsü**: tek aşamalı komutlarda tx1'de `Completed` + yanıt;
    iki aşamalı komutlarda (Submit ve kararlar — Release bu dilimde tek aşamalıdır) tx1'de `Pending` (yanıt yok, `LinkedIntentId` dolu), yanıt **tx2'de**
    (kesin sonuç ya da 202 belirsiz) ya da — makbuz hâlâ `Pending` ise — **Reconcile'da** yazılır ve makbuz `Completed`
    olur. `Completed` makbuz değişmez (Entity Fields, değişmezlik kuralı).
  - **Kontrol sırası (her komutta aynı):** (1) kimlik + `[HasPermission]` → (2) veri kapsamı (SEC-002, D14/1; okumada
    okuma kapsamı, **yazmada yazma kapsamı** — hedef partinin şirketi kullanıcının kendi şirket yetkisinde değilse, parti
    `read-tenant-wide` ile görünse bile → 404 `PAYMENT_BATCH_NOT_FOUND`, başka adım yok) → (3) makbuz araması: aynı kapsam + farklı parmak
    izi → 409 `PAYMENT_IDEMPOTENCY_CONFLICT`; aynı kapsam + aynı parmak izi + `Completed` → **kayıtlı yanıt aynen döner**;
    aynı kapsam + aynı parmak izi + `Pending` → 202 `PAYMENT_REQUEST_IN_PROGRESS` (bağlı niyetin o anki durumuyla). İki
    durumda da **başka hiçbir adım çalışmaz** — uzak çağrı yok, Reconcile yok. → (4) açık niyet kontrolü (Submit ve
    kararlar; aşağıda 2. ve 4. madde) → (5) durum ve `Version` CAS → (6) iş kuralları ve SoD → (7) tx1 (iş verisi + makbuz
    + denetim, tek işlem).
  - **Kurtarma yalnız `ReconcilePaymentBatchWorkflowCommand` ile yapılır** (kendi `Idempotency-Key`'i ve makbuzuyla); aynı
    anahtarla tekrar asla kurtarma tetiklemez (replay ve recovery ayrıdır).
  - **Çökme:** tx1 sonrası çökmede makbuz `Pending` kalır; aynı anahtarla tekrar 202 `PAYMENT_REQUEST_IN_PROGRESS` alır;
    Reconcile niyeti kapattığında bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır (kesin yanıtla); `Completed`
    makbuza dokunulmaz. tx1 commit olmadıysa hiçbir şey
    yazılmamıştır; aynı anahtarla tekrar baştan çalışır.
- **Eşzamanlılık:** Update / kararlar `If-Match` (Version) ve durum CAS; bayat istek 409.
- **Yerel kayıt önce, uzak çağrı sonra (domain-config; TP-001 T16):**
  1. *Submit:* tx1 — parti `Submitted`, `PaymentWorkflowStartIntent(Pending)`, merkezi denetim satırı (işleme yazılır).
     Sonra MOD-0023 başlatma, aynı `IdempotencyKey` ile. Sonra tx2: kesin başarı → niyet `Started`, parti
     `PendingApproval` + `WorkflowInstanceId`; kesin ret (400/403/409 — 404 **hariç**: 404 belirsizdir → `Unknown`, D21/7; MOD-0023'ün "başlatma tamamlanmadı" 409'u, 409 `WORKFLOW_TRANSITION_CONFLICT` ve 503 `WORKFLOW_TRANSITION_CONFLICT` / `WORKFLOW_TRANSACTION_UNAVAILABLE` hariç — bunlar belirsizdir, D21/7, D25) → niyet `Refused`, parti `PreviousBatchStatus`, tek istisna satırı; **belirsiz** (zaman aşımı, bağlantı kopması,
     5xx, boş ya da okunamayan 2xx gövde) → niyet `Unknown`, parti `Submitted` kalır, yanıt **202**
     `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN`. Belirsiz sonuç **ret olarak kaydedilmez** (kaynak kusuru, paket §H.3).
  2. *Açık niyet varken* (`Pending` / `Unknown`): **aynı** anahtarla tekrar → makbuz kuralı (kayıtlı yanıt ya da 202
     `PAYMENT_REQUEST_IN_PROGRESS`); **farklı** anahtarla Submit → 409 `PAYMENT_WORKFLOW_START_PENDING`, uzak çağrı yok. Parti başına en fazla bir açık
     başlatma niyeti: tekil kısmi indeks `(TenantId, BatchId)` where `Status ∈ {Pending, Unknown}`.
  3. *Reconcile:* sonuç `Started` / `Refused` / `Cancelled`'a yalnız uzak tarafın **kesin** durumuyla bağlanır; belirsiz
     kalırsa durum değişmez ve deneme sayılır. Sıra 3a'dadır.
  3a. *Reconcile önce okur; uzak yazmayı yalnız anahtarla idempotent olduğu ölçülmüş yerde yeniden yapar (gap 4/G2,
     gap 5/G2; her iki aşamalı komut — Submit ve beş karar komutu):*
     - **Önce okuma:** Reconcile uzak tarafı **okur** — başlatmada `instances/by-objects` (parti kimliği) ve niyetin
       deterministik uzak anahtarı; kararda örneğin görev geçmişi ve `RemoteIdempotencyKey`. Okuma yazma değildir.
     - **Uzakta niyetin kendisine ait örnek / geçiş var** (eşleme aşağıda, gap 6/P1-b) → kesin durumu kaydedilir (`Started` / `Recorded` / `Refused`; örnek ya da görev
       uzakta açıkça iptal/kapalıysa `Cancelled`), tx2 yazılır, bağlı makbuz yalnız `Pending` ise `Completed` olur, sonuç
       olayı yazılır. Bu yol şirket kuralından **muaftır** (D15/2).
     - **"Kesin boş" okumadan çıkarılmaz (gap 5/G2):** MOD-0023 sözleşmesinde "bulunamadı / boş okuma son durumdur"
       güvencesi **yoktur** (ilk çağrı hâlâ yolda olabilir, yayılma gecikmesi, başka yüzey); bu yüzden okumaya dayalı
       "kesin boş → yeniden başlat" dalı yoktur. Okuma belirsiz ya da "bulunamadı" ise (zaman aşımı, bağlantı kopması, 5xx,
       okunamayan gövde, 404 / boş sonuç) niyet **asla `Cancelled` yapılmaz**; `Unknown` kalır (`Pending` idiyse
       `Unknown`'a geçer), parti ve makbuz değişmez. Zaman aşımı / 5xx / okunamayan gövdede başka adım yoktur (yalnız
       deneme sayılır); 404 / boş sonuçta tek ek adım aşağıdaki anahtarlı yeniden yazmadır.
     - **Yeniden çağrı yalnız uzak yazmanın kendisi anahtarla idempotent ise — bu modülde yalnız MOD-0023 başlatma ve
       MOD-0023 görev geçişleri.** Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c): başlatmada tekil kısmi indeks
       `ux_workflow_instances_tenant_idempotency_key_active` (TenantId + IdempotencyKey, `IsDeleted = false`, anahtar
       string — `PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131`) ve işleyici anahtarı önce arayıp var olan örneği
       döndürür (`StartWorkflowInstanceHandler.cs:76-85`); geçişlerde (approve / reject / request-info / cancel /
       delegate) validator anahtarı zorunlu tutar (ör. `ApproveWorkflowTaskValidator.cs:13`), tekil kısmi indeks
       `ux_workflow_transition_logs_task_action_idempotency_active` (TenantId + ApprovalTaskId + Action + IdempotencyKey —
       aynı dosya `:211-225`) ve işleyiciler `GetByTaskActionIdempotencyKeyAsync` ile yeniden oynatır
       (`RejectWorkflowTaskHandler.cs:79-80`, `WorkflowTerminalTransitionWriter.cs:402-407`). Bu yüzden okuma örnek /
       geçiş göstermediyse Reconcile **aynı** uzak yazmayı **aynı deterministik uzak anahtarla** (başlatmada
       `IdempotencyKey`, kararda `RemoteIdempotencyKey`) **bir kez** yeniden yapabilir; en fazla bir örnek / geçişi
       MOD-0023'ün tekil indeksi garanti eder. Yeniden yazmadan önce (i) şirket uygunluğu (`OperationalStatus = Active` ve
       `ApprovalStatus = Approved`, D10/1, D14/4), (ii) SoD (başlatmada R1–R4 aday çözümleyicisi yeniden; kararda
       niyetteki karar verenle R1–R10), (iii) izin ve (iv) asıl aktör yeniden denetlenir: yeniden yazma MOD-0023'teki
       başlatanı ya da karar vereni **değiştiremez**; Reconcile'ı çağıran, asıl komutun aktörü (`SubmittedBy` /
       `DecidedBy`) değilse uzak yazma çağrısı yapılmaz (409 `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`). Sonuç eşlemesi:
       başarı (yeni ya da yeniden oynatılmış) → niyet sonuçla kapanır (`Started` / `Recorded`, tx2 eşlemesiyle); **kesin
       ret** (tx2 eşlemesi; yinelenen anahtar / tekil indeks çakışması yanıtı ret sayılmaz) → `Refused`; kesin ret olmayan
       her başarısızlık — yinelenen anahtar yarışı yanıtı, 5xx, zaman aşımı, okunamayan gövde → niyet `Unknown` kalır
       (yalnız deneme sayılır).
     - **Güvencenin sınırı — silinen ya da temizlenen uzak kayıt (gap 6/P1-a):** iki tekil indeks de **kısmidir**
       (`IsDeleted = false`); silinmiş ya da temizlenmiş (purge) bir örnek / geçiş kaydı, aynı anahtarla **yenisinin**
       oluşmasına izin verir. Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c):
       `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs`
       içindeki tek `IsDeleted = true` yazımı `SlaEscalationRule` içindir (`:961-974`, `DeactivateRulesForTemplateAsync`);
       `WorkflowInstance` ya da `WorkflowTransitionLog` için soft delete ya da temizleme yolu bulunmadı;
       `PlatformSchemaManifest.WorkflowWorkCenter.cs` (sha256
       `666b44dcbca19df1f1ebf4f176c9fa86bf66111cc1b935286b625bc1d4706f10`) 0 `ExpireAfter` içerir (TTL yok); okuma tarafında
       `instances/by-objects` silinmiş örnekleri süzer (`GetWorkflowInstancesByObjectsHandler.cs:51`, `DeletedAt is null`),
       yani silinmiş kayıt okumada **görünmez**. "Silme yolu yok" bir ölçümdür, politika değildir; bu yüzden anahtarla
       tekillik güvencesi **yalnız örnek ve geçiş kayıtları silinmediği / temizlenmediği sürece** geçerlidir. Kural: okuma
       silinmiş (`IsDeleted = true` / `DeletedAt` dolu) bir örnek / geçiş gösterirse **ya da** silinmemiş kayıt yokken
       niyette ilk uzak deneme kayıtlıysa ve okuma silinmiş durumu ayırt edemiyorsa (tx1'i commit olmuş her niyet — uzak çağrı yalnız tx1'den sonra yapılır),
       Reconcile aynı anahtarla yeniden yazmaz; niyet `Unknown` kalır (202 `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` /
       `PAYMENT_DECISION_OUTCOME_UNKNOWN`), parti ve bağlı makbuz değişmez. Böylece 404 / boş okumadaki anahtarlı yeniden
       yazma, şu iki koşul kapanana kadar **kapalıdır** (Ready-for-dev engeli, DL-2): (1) MOD-0023'ün saklama + silmeme
       politikası — örnek ve geçiş kayıtları en az niyet ömrü boyunca silinmez, temizlenmez, TTL almaz (Dependencies
       kapanış tablosu; sahibi platform-shared-services); (2) okumanın silinmiş durumu ayırt ettiği ölçüldü ya da eklemeli
       okuma alanıyla sağlandı (D9). İkisi kapanınca "silinmemiş kayıt yok" ilk denemenin MOD-0023'e hiç ulaşmadığı
       anlamına gelir ve yukarıdaki anahtarlı yeniden yazma açılır; silinmiş kayıt görüldüğünde yine yeniden yazma yoktur.
     - **Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b):** `instances/by-objects` aynı partinin **bütün** silinmemiş
       örneklerini döndürür, önceki gönderimlerinkiler dahil (`GetWorkflowInstancesByObjectsHandler.cs:50-69`). Bir okuma
       sonucu bir niyete yalnız şu alanların **hepsi** eşleşirse bağlanır: kiracı; nesne (parti) kimliği; başlatmada
       `SubmissionSequence`, kararda karar kimliği (`PaymentApprovalDecision.Id`); niyetin deterministik uzak anahtarı
       (`IdempotencyKey` / `RemoteIdempotencyKey` — sıra ve karar kimliğini içerir); geçişte ek olarak görev kimliği
       (`WorkflowTaskId`) + eylem (approve / reject / request-info; eylem matrisi MOD-0023 pack §17.4a). Daha eski bir denemenin sonucu
       (önceki `SubmissionSequence`'in örneği, başka bir kararın geçişi ya da iptali) daha yeni bir niyeti **asla**
       kapatmaz; yalnız nesne kimliğiyle ya da "en yeni örnek" sırasıyla eşleme yasaktır. Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c):
       `WorkflowInstanceStatusDto` (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs:200-207`)
       uzak anahtarı, geçmiş satırı `WorkflowInstanceHistoryEntryDto` (`:451-465`) uzak anahtarı ve görev kimliğini
       taşımaz. Eşleme alanı yanıtta yoksa okuma o niyeti kapatamaz → niyet `Unknown` kalır. Kapanış yolu: MOD-0023 okuma
       yanıtlarına eklemeli alan (`IdempotencyKey`; geçmişte ayrıca `ApprovalTaskId` — D9, MOD-0023 pack değişikliği
       önerisi; Dependencies kapanış tablosu) ya da, P1-a koşulları kapalıysa, aynı anahtarla yeniden yazmanın yanıtı
       (MOD-0023 aynı anahtarla var olan örneği / geçişi döndürür — `StartWorkflowInstanceHandler.cs:76-85`; yanıt anahtara
       bağlıdır).
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
     - **Anahtarla idempotent olduğu ölçülmemiş uzak yazma yeniden çağrılmaz:** MOD-0031 kanıt bağlamada aynı uzak
       anahtarın tekrarının güvenli olduğu (dedupe) ölçülmedi → Reconcile ve hiçbir kurtarma yolu MOD-0031'e yeniden
       yazma çağrısı yapmaz; yalnız okur, okuma kesin sonuç gösterene kadar sonuç belirsiz kalır. Yeniden çağrıyı açmak için
       MOD-0031 dedupe ölçümü önkoşuldur (Dependencies kapanış tablosu, DL-3 önkoşulu).
     - **`Cancelled` yalnız kesin uzak iptal / kapanış kaydıyla:** o örnek için MOD-0023 iptal geçiş kaydı (ör.
       `CloseAbandonedWorkflowStartHandler.cs`, anahtar `workflow-start-recovery-close:{instanceId:N}`, `:230`) ya da
       görevin iptal kaydı okunduğunda; başka hiçbir yanıt `Cancelled` üretmez.
     - **Uygun değilse** (şirket uygun değil / SoD ya da izin reddi / aktör farklı): uzak yazma/başlatma çağrısı **yapılmaz**; niyet
       `Unknown` kalır ve `LastOutcomeCode` sebep kodunu taşır (`PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, `PAYMENT_SOD_DENIED`,
       `PAYMENT_PERMISSION_DENIED`, `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`); parti durumu ve bağlı `Pending` makbuz
       değişmez (aynı anahtarla tekrar yine 202 `PAYMENT_REQUEST_IN_PROGRESS`), niyet kapanmadığı için sonuç olayı yazılmaz.
       Reconcile'ın kendi makbuzu sebep koduyla (422 / 403 / 409) `Completed` olur. Koşul düzelince (ör. şirket yeniden
       onaylanınca) yeni bir Reconcile tekrar dener. Niyetin kalıcı olarak kapatılması (açık iptal yolu) Follow-up'tadır.
  4. *Kararlar:* SoD ön kontrolü → tx1 `PaymentApprovalDecision(Requested)` + denetim satırı → MOD-0023 görev geçişi,
     karar idempotency anahtarıyla (kaynakta da iletiliyor, `WorkflowClientAdapters.cs:222@da4abb20`) → tx2 `Recorded`
     + parti durumu. Belirsiz → `Unknown`, 202 `PAYMENT_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır.
     **Parti başına tek açık karar niyeti:** tekil kısmi indeks `(TenantId, BatchId)` where `State ∈ {Requested, Unknown}`;
     tx1 bu indeks + parti `Version` CAS ile atomiktir. Açık karar niyeti varken başka anahtarla ya da başka aktörden
     gelen her karar (çelişen ya da aynı yönde) **uzak sisteme gitmeden** 409 `PAYMENT_DECISION_PENDING` alır; niyet
     tx2 ya da Reconcile ile `Recorded` / `Refused` / `Cancelled` olana kadar yeni karar başlamaz. Aynı anahtarla tekrar →
     makbuz kuralı (kayıtlı yanıt ya da 202 `PAYMENT_REQUEST_IN_PROGRESS`).
  4a. *İki aşamalı denetim (tx1 / tx2) ve çökme kurtarma:*
     - tx1 **deneme olayı** yazar (`…workflow-start-requested`, `…decision-requested`); tx2 **sonuç
       olayı** yazar (`…submitted`, `…approved`, `…rejected`, `…workflow-start-refused` vb.; Release tek aşamalıdır, burada yoktur). Her
       iki olay da kendi işleminde, iş verisiyle birlikte (`RequireEnlistment = true`).
     - Kesin sonuç ancak tx2 commit olunca vardır; tx1 deneme olayı tek başına "yapıldı" anlamına gelmez.
     - **Uzak başarı + tx2 başarısız ya da süreç çöker:** niyet `Requested`/`Pending` kalır; Reconcile (elle) MOD-0023
       örneği/görevi `instances/by-objects` ve görev geçmişinden okur (yalnız niyetin kendi eşleme alanlarıyla — 3a, gap
       6/P1-b), uzak çağrıyı **tekrarlamadan** tx2'yi yazar, sonuç
       olayını üretir ve bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır. Aynı anahtarla tekrar istek kurtarma yapmaz; makbuz `Pending`
       iken 202 `PAYMENT_REQUEST_IN_PROGRESS` alır. İkinci uzak geçiş oluşmaz.
     - **K2 kararları (D11/3):** tx1 deneme olayı yazılamazsa uzak çağrı yapılmaz, komut başarısız; tx2 sonuç olayı
       yazılamazsa niyet açık kalır ve karar "kayıtlı" sayılmaz (Release bunu kabul etmez) — Reconcile tamamlar.
  5. HTTP istemcilerinde **açık zaman aşımı** yapılandırılır (kaynakta yok, paket §H.3 / §N.12); değer
     `FinanceTreasury:Workflow:TimeoutSeconds` — başlangıçta yoksa servis açılmaz (öneri 15 sn, O-8 içinde).
- **SoD politikası tek kaynaktır ve kiracı ayarıdır (sahip kararı D11/1):** kiracı başına PolicyVersion,
  Tier2Threshold.Amount, Tier2Threshold.CurrencyCode. Kodda varsayılan tutar yoktur; kiracının ayarı yoksa **her onay
  tier-2 sayılır** (güvenli varsayılan) ve parti para birimi eşik para biriminden farklıysa da tier-2 sayılır.
  Çözümleyici (gönderme) ve karar kontrolü **aynı** ayarı, partiye o anda sabitlenen sürümle kullanır. Ayarın kayıt
  yeri (MOD-0126 içi kiracı ayarı ya da Platform ayarları) ve ayar ekranı Follow-up'tadır.
  - **İki durum ayrıdır:** (a) kiracının ayarı **yok** → bu bir hata değildir; her onay tier-2 sayılır ve partiye
    `SodPolicySource = DefaultAllTier2` olarak sabitlenir; (b) ayar deposu **erişilemiyor** (altyapı arızası) → Submit ve
    kararlar 503 `PAYMENT_SOD_POLICY_UNAVAILABLE`, uzak çağrı yok, kayıt değişmez. Servisin açılışı ayara bağlı değildir.
- **Bankaya dağıtım kapalı (D5):** `IBankDispatchPort`'un tek bağdaştırıcısı `NotConfiguredBankDispatchAdapter`'dır;
  her çağrı `NotConfigured` döner, `PaymentDispatchAttempt` yazılır, parti `Approved` kalır. Gerçek bağdaştırıcı ayrı
  karar ve paket ister.
  - **Release bu dilimde tek işlemdir (gap 3/G3):** bağdaştırıcı süreç içidir ve ağ çağrısı yapmaz; bu yüzden R11
    doğrulaması, bağdaştırıcı çağrısı, `PaymentDispatchAttempt`, makbuz (`Completed`, 200) ve tek `…release-attempted`
    olayı **aynı Mongo işleminde** yazılır. Uzak çağrı olmadığı için niyet, belirsiz sonuç ve Reconcile yoktur; çökme →
    işlem geri alınır, aynı anahtarla tekrar baştan çalışır.
  - **Sözleşme bağı (gap 7–8, `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 — taslak rev 3'ten, D27/7):** aynı işlemde `ReleaseSequence` +1 (sürüme
    koşullu) ve `dispatchKey` üretilir; makbuz tekrarı portu çağırmaz ve sıra artırmaz; commit edilmiş makbuz yanıtını
    (ve `dispatchKey`'ini) korur; geri alınan deneme sıra ya da anahtar rezerve etmez — arada başka bir Release commit
    ederse yeniden deneme sonraki sırayı alır (gap 8/P2-a; dağıtım kapalı sürüm için yeterli). İstek her talimatı
    kendi `SourceBankAccountRef`'iyle taşır; `DefaultSourceBankAccountRef` talimat değerinin yerine konmaz, talimatlar
    birleştirilmez (D17/1). Port iş sonucunu tipli döndürür: `NotConfigured` → 200; `BankDispatchContractException` /
    sözleşme dışı istisna → geri alma + 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION`; iptal → geri alma, kayıt yok;
    `NotConfigured` dışı sonuç → deneme kaydedilir, `ReleaseBlockedForReview = true`, olay sonucu taşır, 502
    `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED` (makbuz bu yanıtla `Completed`) — başarılı Release sayılmaz (D17/2).
  - **Gerçek dağıtım açıldığında** Release iki aşamalı olur ve kendi `PaymentReleaseIntent` yaşam döngüsünü (Pending /
    Dispatched / Refused / Unknown → Reconcile), sonuç olayını ve kurtarmasını o kararın paketinde alır; bu dilim bunu
    varsaymaz (Follow-up).
  - **Gerçek dağıtım açılmadan önce zorunlu kapı (bu dilimde opak Guid kontrolünün sınırı):** (1) kaynak banka hesabının
    sahibi ödeyen şirkettir (MOD-0289, güncel durum); (2) hesap ve alacaklı aynı kiracıda ve ödeyen şirket için uygundur
    (Tenant + LE, MOD-0289/MOD-0220); (3) alacaklı referansı ile hedef hesap eşleşir (MOD-0140 `SupplierCompanyRelationship`
    Active / MOD-0289 hedef). Bu üçü sözleşmeli kontrol ve testle kapanmadan `IBankDispatchPort`'a gerçek bağdaştırıcı
    bağlanamaz.
- `GlobalEntity` kullanılmaz; bütün kayıtlar kiracıya aittir.
- Mongo GUID V3 / subtype 4; `decimal` → `Decimal128`.

## Layout & Shell Contract

- `shell: tenant` → `Views/FinanceTreasury/PaymentBatches/` altındaki **tüm** `.cshtml` dosyalarında
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml` varsayılanı değiştirilmez. Kaynak ekrandaki
  `Layout = "_LayoutBackbone"` (`frontend/Diten.Web/Views/ERP/Treasury/PaymentProposals/Index.cshtml:3@da4abb20`)
  taşınmaz.
- View klasörü: `Views/FinanceTreasury/PaymentBatches/` · Frontend rotası: `/FinanceTreasury/PaymentBatches`.
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `finance-treasury.payment-batches.read` yoksa kabuk içinde
  `_AccessDenied` partial'ı; başlık, kart, boş tablo, buton çizilmez; yönlendirme yok; 401 tam sayfa.

## Backend File Convention

Golden Reference **Compact** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
okundu): `Features/PaymentBatch/` altında

```text
Commands/          CreatePaymentBatchCommand.cs, UpdatePaymentBatchCommand.cs, DeletePaymentBatchCommand.cs,
                   BulkDeletePaymentBatchCommand.cs, ValidatePaymentBatchCommand.cs, SubmitPaymentBatchCommand.cs,
                   ReconcilePaymentBatchWorkflowCommand.cs, ApprovePaymentBatchCommand.cs,
                   ApprovePaymentBatchWithSodWaiverCommand.cs, RejectPaymentBatchCommand.cs,
                   RequestPaymentBatchEvidenceCommand.cs,
                   LinkPaymentBatchEvidenceCommand.cs, GeneratePaymentAttestationCommand.cs, ReleasePaymentBatchCommand.cs
Queries/           GetPaymentBatchListQuery.cs, GetPaymentBatchByIdQuery.cs, GetPaymentBatchTraceQuery.cs,
                   GetPaymentExceptionListQuery.cs, PaymentBatchListSort.cs
Handlers/CommandHandlers/   {Verb}PaymentBatchHandler.cs (her komut için; suffix YOK)
Handlers/QueryHandlers/     Get{…}Handler.cs
Validators/        CreatePaymentBatchValidator.cs, UpdatePaymentBatchValidator.cs, GetPaymentBatchListValidator.cs,
                   PaymentDecisionValidator.cs, PaymentSodWaiverValidator.cs
PaymentBatchModels.cs       (tüm DTO'lar tek dosyada)
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` `{Verb}{Module}Handler` — **Command/Query
  suffix YOK** (kaynak `CreateManualPaymentBatchCommandHandler` aynı dosyada komutla birlikte,
  `CreateManualPaymentBatchCommand.cs:8-25@da4abb20` — taşınmaz). Tek dosyada tek public tip.
- Controller `CustomBaseController`'dan türer, `Response<T>` döner; her aksiyonda `[HasPermission]`.
- İş kuralı servis sınıflarında değil handler + domain'de (`handler-design`); kaynaktaki `PaymentProposalService` /
  `PaymentWorkflowService` gibi büyük servisler (yaklaşık 1000 satır) bölünerek alınır.
- Her yazma komutu `ICentralAuditDeclaration` taşır (`CentralAuditEntityId` açık yazılır); MOD-0023'e yalnız HTTP ile
  dokunup yerelde yazmayan komut yoktur (hepsi yerel niyet yazar), bu yüzden `ICentralAuditOutsideTransaction` kullanılmaz.

## Frontend File Contract

Golden Reference **Compact** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/` okundu):

```text
Views/FinanceTreasury/PaymentBatches/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml             (sayfa kabuğu + _Form)
├── Edit.cshtml               (sayfa kabuğu + _Form; yalnız Draft/Validated)
├── Details.cshtml            (sekme: Talimatlar · İş akışı ve kararlar · İstisnalar · Onay belgeleri · Serbest bırakma denemeleri · İz)
├── _Form.cshtml              (başlık alanları + talimat satırları ızgarası)
├── _Filter.cshtml            (durum, para birimi, değer tarihi aralığı, tutar aralığı)
├── _DataTable.cshtml         (data-dt-standard="v2", data-dt-data-mode="server", iskelet yükleyici)
├── _IndexL10n.cshtml
└── PaymentBatchesIndex.cs    (marker)
wwwroot/assets/js/FinanceTreasury/PaymentBatches/  index.js · index.l10n.js · form.js · details.js
Resources/Views/FinanceTreasury/PaymentBatches/    PaymentBatchesIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- Durum filtresi ve rozetleri `PaymentBatchStatus`'un 10 değerinin tamamını gösterir; kaynak ekrandaki
  `PendingValidation` / `Released` (enum'da yok) taşınmaz (paket §I T22).
- DataTable sütunları: Parti kodu, Durum, Para birimi, Toplam tutar (para birimine göre biçim), Talimat sayısı, Değer
  tarihi, İş akışı durumu, Oluşturan, Oluşturma zamanı, İşlemler.
- Karar düğmeleri (Onayla / Reddet / Kanıt iste / Yükselt / Muafiyetle onayla) yalnız Details'ta ve yalnız sunucunun
  verdiği `allowedActions` listesine göre çizilir; SoD nedeniyle karar veremeyen kullanıcıya düğme gösterilmez, yerine
  yerelleştirilmiş açıklama (ör. "Bu partiyi siz hazırladığınız için karar veremezsiniz") çizilir — sunucu kontrolünün
  yerine geçmez.
- API profili: same-origin MVC proxy (`/FinanceTreasury/PaymentBatches/api/...`); proxy `AuthTokenCookies.GetAccessToken`
  ile token'ı ve oturumun çözümlenmiş `X-Tenant-Id` / `X-Legal-Entity-Id` başlıklarını ekler; çözümlenemezse istek
  oluşturulmaz (multi-tenancy §6). Tarayıcı JS `access_token` / `document.cookie` okumaz.
- Uyarı/onay diyalogları SweetAlert2 premium standardı (MOD-0013); hardcoded metin ve `|| 'Export'` tarzı geri dönüş yok.
- Para birimi seçimi `GET /api/lookups/reference/currencies`'ten; sabit liste yok. Banka referansı alanları MOD-0289
  arama ucu gelene kadar opak referans girişi (O-15).

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| X-Legal-Entity-Id | Evet | Guid; kiracıya ait; Active + Approved; okumada kullanıcının okuma kapsamında (kendi şirket yetkisi ya da `read-tenant-wide`, D14/1); **yazmada** hedef partinin şirketine eşit ve kullanıcının **kendi** şirket yetkisinde (yazma kapsamı — `read-tenant-wide` yazma yetkisi vermez, gap 4/G1); değilse 404 | — | `ILegalEntityEligibilityPort`, `IFinanceTreasuryDataScopeSource` (okuma / yazma kapsamı) |
| Gövde/sorguda tenantId, legalEntityId | — | bulunursa 400 | — | middleware |
| DefaultSourceBankAccountRef | Evet | Guid ≠ Empty | — | — |
| PaymentMethod | Evet | trim, 1..32 | — | değer listesi O-13 |
| CurrencyCode | Evet | `^[A-Z]{3}$` (upper normalize), MOD-0048 listesinde | — | `ICurrencyReferencePort` |
| RequestedValueDate | Evet | `DateOnly`, default değil; geçmiş tarih O-12 | — | — |
| RiskRating | Hayır | enum | — | — |
| EvidenceRequired | Hayır | bool | — | — |
| Notes | Hayır | trim, ≤ 2000 | — | — |
| Instructions | Evet | 1..500 satır | — | — |
| Instruction.ManualOutflowReference | Evet | trim, 1..64 | — | — |
| Instruction.PayeeType | Evet | tanımlı enum | — | — |
| Instruction.PayeeReferenceId | Koşullu | `PayeeType ≠ Other` → Guid ≠ Empty | — | varlık O-14 |
| Instruction.SourceBankAccountRef | Evet | Guid ≠ Empty | — | — |
| Instruction.DestinationReferenceId | Hayır | Guid ≠ Empty ise kabul | — | — |
| Instruction.Amount | Evet | > 0; küsurat ≤ para birimi hanesi | — | `ICurrencyReferencePort` (hane) |
| Instruction.CurrencyCode | — | gönderilirse partininkine eşit | — | — |
| Instruction.RemittanceReference / SourceDocumentRef | Hayır | trim, ≤ 140 / ≤ 128 | — | — |
| BatchCode | — | istekte bulunursa 400 (OTOMATİK) | Unique (TenantId, BatchCode) | `IRecordCodePort` |
| Idempotency-Key | **Evet** (her yazma komutu, D12/2) | 1..128 yazdırılabilir ASCII; yoksa 400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED` | Unique (TenantId, LegalEntityId, ActorId, Operation, TargetId, Key) | makbuz araması (Runtime Constraints sırası) |
| Karar ReasonCode | Reject / RequestEvidence için Evet | trim, ≤ 64 (`PaymentWorkflowService.cs:291-296@da4abb20`) | — | — |
| Karar WorkflowTaskId | Evet | Guid ≠ Empty, partinin örneğine ait | — | MOD-0023 |
| Muafiyet ReasonCode | Evet | trim, 1..64 | — | — |
| Muafiyet EvidenceRefs | Evet | en az 1 MOD-0031 bağlantı kimliği ya da belge sürüm kimliği (`:364-372@da4abb20`) | — | — |

**Durum kuralları:** Update yalnız `Draft`/`Validated` ve `WorkflowInstanceId = null`, açık niyet yok; güncelleme partiyi
`Draft`'a döndürür ve talimat doğrulama durumlarını sıfırlar. Validate kaynaktaki kontrolleri yapar (talimat var, tek para
birimi, pozitif tutar, toplam = satır toplamı, sayı = satır sayısı, değer tarihi, hassasiyet —
`PaymentLifecycleService.cs:91-145@da4abb20`). Submit yalnız `Draft`/`Validated`; `EvidenceRequired` ise en az bir kanıt.
Delete / BulkDelete yalnız hiç gönderilmemiş (`SubmissionSequence = 0`) `Draft`/`Validated` parti.

**SoD kuralları (çekirdek — her karar komutunda, MOD-0023 çağrısından ÖNCE):**

| # | Kural | Nerede | Kaynak karşılığı |
|---|---|---|---|
| R1 | Partiyi oluşturan (`CreatedBy`) partide **hiçbir karar** veremez | gönderme çözümleyicisi (aday dışı) + karar kontrolü | `PaymentSodPolicyService.cs:38-44@da4abb20`; `ConfiguredApproverResolver.cs:117-121` |
| R2 | Gönderen (`SubmittedBy` = MOD-0023 başlatanı) karar veremez | aynı + MOD-0023 `SodViolation` | `:46-56@da4abb20`; Recovery `WorkflowTaskTransitionSupport.cs:964-977` |
| R3 | Gönderme anındaki son güncelleyen (`UpdatedByBeforeSubmit`) karar veremez | aynı | kaynakta yalnız çözümleyici dışlıyor (`ConfiguredApproverResolver.cs:117-121@da4abb20`), SoD servisi kontrol etmiyor — Recovery'de iki yerde tutarlı |
| R4 | `IsTier2` parti için **Approve** `finance-treasury.payment-batches.tier2-approve` ister; gönderme çözümleyicisi bu izni taşıyan adayları seçer. **Muafiyet adayı (sahip kararı D13):** R1–R3 dışlamasından sonra kiracıda **hiç** tier-2 aday kalmıyorsa, `waive-sod` izni taşıyan kişiler `IsWaiverCandidate = true` ile aday eklenir; bu adaylar yalnız `ApprovePaymentBatchWithSodWaiverCommand` ile onaylayabilir (normal Approve 403 `PAYMENT_TIER2_APPROVER_REQUIRED`); tier-2 aday varken muafiyet adayı eklenmez. Normal onay yetkisi (R4) ve muafiyet yetkisi (R9) ayrı değerlendirilir | gönderme çözümleyicisi + karar | `PaymentSodPolicyService.cs:58-67@da4abb20`; `ConfiguredApproverResolver.cs:41-43`; kaynakta `WaiverApprovers` listesi tanımlı ama okunmuyordu (paket §E) |
| R5 | `IsTier2` hesabı: `batch.CurrencyCode == Threshold.CurrencyCode && TotalAmount >= Threshold.Amount`; para birimi farklıysa **tier-2 sayılır** (kur çevrimi yok; önerilen ihtiyatlı kural, O-4) | gönderme (sabitlenir) | kaynakta iki ayrı, para birimsiz eşik: `SodPolicyContracts.cs:10@da4abb20` (50 000) ve `ApproverPolicyContracts.cs:13` (null) — ayrışabilir (paket §H.2 (3)); **taşınmaz** |
| R6 | Eşik, para birimi ve politika sürümü gönderme anında partiye ve niyete yazılır; karar kontrolü yalnız sabitlenmiş değeri kullanır | karar | yeni (level-1 koşul 1) |
| R7 | Aktör karşılaştırması token `sub`'ının **Guid** değeri üzerindendir; `sub` Guid değilse karar reddedilir (`FailedRoleConflict`) | her yer | kaynakta SoD ordinal, çözümleyici büyük/küçük harf duyarsız (paket §H.2 (3)) — **taşınmaz** |
| R8 | İzin karar anında yeniden değerlendirilir (o anki token, `[HasPermission]` + handler içi tier-2/muafiyet izni) | karar | SoD değerlendirmesi Q3 "not established" — burada test edilir |
| R9 | Muafiyet yalnız `ApprovePaymentBatchWithSodWaiverCommand` ile: izin `waive-sod` + `ReasonCode` + en az 1 kanıt. **Yalnız R4'ü (tier-2 izni eksikliği) kaldırabilir** (sahip kararı D12/1). R1, R2, R3 muaf tutulamaz → 403 `PAYMENT_SOD_WAIVER_NOT_ALLOWED`; MOD-0023'ün başlatan ve atama kontrolleri de kaldırılamaz. Aday çözümleyici R1–R3 kişilerini dışlar (muafiyetle de aday olamazlar); muafiyet yalnız atanmış ama tier-2 izni olmayan onaylayıcı için anlamlıdır | karar | `PaymentSodPolicyService.cs:78-112@da4abb20` (izin, gerekçe, kanıt sırası korunur); kaynak R1/R3/R4 kaldırabiliyordu — **daraltıldı** |
| R10 | Onaylayan, MOD-0023 görevinin atanmış aktörü olmalıdır (MOD-0023 `WorkflowActorDenied`) | MOD-0023 | SoD değerlendirmesi Q2 |
| R11 | Release, karar zincirini **aynı muafiyet modeliyle** yeniden doğrular: her `Recorded` onayın aktörü R1–R3'e uyar (muafiyet yok); tier-2 partide en az bir onay ya `tier2-approve` izniyle ya da kayıtlı bir R4 muafiyetiyle verilmiştir; açık (`Requested`/`Unknown`) karar niyeti varsa Release 409 | Release | yeni — Görev Merkezi gibi başka yüzeyden verilmiş karara karşı ikinci kontrol (O-7) |

## Failure Path to Verify

- **Kendi partisini onaylama (oluşturan)** → 403 `PAYMENT_SOD_DENIED`, `FailedSelfApproval`; MOD-0023 çağrılmaz; tek
  istisna satırı; parti durumu değişmez.
- **Gönderenin onayı** → 403 `PAYMENT_SOD_DENIED`, `FailedSubmitterApproval`; MOD-0023 çağrılmaz.
- **Son güncelleyenin onayı** → 403 `PAYMENT_SOD_DENIED`, `FailedUpdaterApproval`.
- **Tier-2 parti, tier-2 izni olmayan onaylayıcı** → 403 `PAYMENT_TIER2_APPROVER_REQUIRED`, `FailedAmountTier`.
- **Muafiyet izni yok** → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; **gerekçe yok** → 400 `PAYMENT_SOD_WAIVER_REASON_REQUIRED`;
  **kanıt yok** → 422 `PAYMENT_SOD_WAIVER_REQUIRED`; **R1/R2/R3 için muafiyet istemi** → 403 `PAYMENT_SOD_WAIVER_NOT_ALLOWED`.
- **Açık karar niyeti varken başka anahtar/aktörden karar** → 409 `PAYMENT_DECISION_PENDING`, MOD-0023 çağrılmaz.
- **Idempotency-Key yok** → 400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`, kayıt yok.
- **Uzak başarı sonrası tx2 hatası / çökme** → niyet açık kalır; Reconcile uzak çağrıyı tekrarlamadan sonucu yazar.
- **Ödeyen şirket aktif değil ya da onaylı değil** → Create/Submit/karar/Release 422 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`,
  kayıt değişmez; MOD-0220 erişilemez → 503 `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`.
- **Başka kiracı / başka şirket partisi** (okuma, karar, silme) → 404 `PAYMENT_BATCH_NOT_FOUND`, varlık sızdırılmaz.
- **Kapsam dışı şirket** (kullanıcının veri kapsamında olmayan şirket, `read-tenant-wide` yok — D14/1) → liste 200 boş,
  detay / iz / yazma 404 `PAYMENT_BATCH_NOT_FOUND`; veri kapsamı çözücüsü hata verirse aynı sonuç (boş kapsam).
- **Yalnız `read-tenant-wide` ile görünen partiye yazma** (kullanıcının o şirkette kendi yetkisi yok — gap 4/G1) →
  Create / Update / Delete / Validate / Submit / her karar / Reconcile / kanıt / onay belgesi / Release 404
  `PAYMENT_BATCH_NOT_FOUND`; liste ve detay okuması 200; MOD-0023'e ve MOD-0220'ye çağrı yok, kayıt ve makbuz yazılmaz.
- **Reconcile, uzakta örnek bulunamadı / okuma belirsiz** (gap 4/G2, gap 5/G2) → niyet `Cancelled` yapılmaz,
  `Unknown` kalır; okuma "kesin boş" kararı vermez. Zaman aşımı / 5xx / okunamayan gövde → uzak yazma/başlatma çağrısı
  yok. 404 / boş okumada yalnız MOD-0023 başlatma / görev geçişi aynı deterministik anahtarla yeniden yazılabilir;
  önce şirket + SoD + izin + aktör denetlenir: şirket uygun değilse uzak yazma/başlatma çağrısı yok, niyet `Unknown` +
  `LastOutcomeCode = PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422; aktör farklıysa 409
  `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`; uygunsa aynı anahtarla tek uzak yazma — başarı (yeni ya da yeniden
  oynatılmış) niyeti kapatır, yinelenen anahtar hatası / 5xx / zaman aşımı → `Unknown` (asla `Refused` değil).
- **Uzak kayıt silinmiş / temizlenmiş** (gap 6/P1-a) → okuma silinmiş kaydı gösterirse ya da silinmemiş kayıt yokken
  niyetin ilk denemesi kayıtlıysa (okuma silinmiş durumu ayırt edemiyorken) (tx1 commit olmuş) aynı anahtarla yeniden yazma **yapılmaz**; niyet `Unknown`, 202
  `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` / `PAYMENT_DECISION_OUTCOME_UNKNOWN`, parti ve makbuz değişmez, MOD-0023'e
  yazma çağrısı yok. MOD-0023 pack §17 (TRM-15) onaylanıp AC-W1 geçene kadar (KARAR D21/1–2) 404 / boş okumada yeniden yazma kapalıdır.
- **Aynı partinin eski örneği / eski kararın geçişi okunuyor** (gap 6/P1-b) → yeni niyete bağlanmaz; eşleme (kiracı,
  parti, `SubmissionSequence` / karar kimliği, uzak anahtar, geçişte görev + eylem) tutmazsa ya da yanıt eşleme alanını
  taşımıyorsa niyet `Unknown` kalır.
- **MOD-0031 kanıt bağlama belirsiz** → yeniden çağrı yok (dedupe ölçülmedi, gap 5/G2); yalnız okuma, kesin sonuç
  okunana kadar belirsiz.
- **İş akışı başlatma zaman aşımı / bağlantı kopması / boş 2xx** → 202 `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN`, niyet
  `Unknown`, parti `Submitted`; ret istisnası yazılmaz; ikinci Submit 409 `PAYMENT_WORKFLOW_START_PENDING`; Reconcile aynı
  anahtarla ilerler ve ikinci uzak örnek oluşmaz.
- **İş akışı başlatma kesin reddi** (ör. 400 doğrulama, 403 yetkisiz başlatma) → 502 `PAYMENT_WORKFLOW_START_FAILED`,
  niyet `Refused`, parti önceki durumuna döner.
- **MOD-0023 başlatmada 404** (ör. şablon yok) → kesin ret **değildir** (D21/7): 202 `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN`,
  niyet `Unknown`, parti `Submitted` kalır (geri alınmaz), sonuç olayı yok; Reconcile okumasıyla kapanır ya da `Unknown`
  kalır (AC-40).
- **Idempotent tekrar, makbuz `Pending`** (iki aşamalı komut sonucu henüz yok / çökme) → 202 `PAYMENT_REQUEST_IN_PROGRESS`,
  kurtarma yapılmaz, uzak çağrı yok.
- **Idempotent tekrar** — aynı anahtar + aynı yük (makbuz `Completed`): kayıtlı yanıt, ikinci kayıt/denetim olayı/uzak örnek yok; aynı anahtar +
  farklı yük: 409 `PAYMENT_IDEMPOTENCY_CONFLICT`.
- **Eksik alan** (PaymentMethod, CurrencyCode, talimat) → 400 + alan düzeyi resx iletisi; kayıt oluşmaz.
- **Hassasiyet aşımı** (ör. JPY'de küsurat) → 400; hane kaynağı yok → 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE`.
- **Eşzamanlılık** (bayat `If-Match`) → 409 `PAYMENT_BATCH_VERSION_CONFLICT`, sessiz üzerine yazma yok.
- **Yetkisiz aktör** (ilgili izin yok) → API 403; UI: kabuk içinde erişim açıklaması, düğme yok (UAS-001).
- **Gönderilmiş partiyi silme/güncelleme** → 409 `PAYMENT_DECISION_STATUS_INVALID`.
- **Denetim kaydı yazılamıyor** (K2 sınıfı komut, outbox yazımı başarısız) → işlem geri alınır, komut başarısız, iş verisi
  yazılmaz (O-6).
- **Kiracının SoD ayarı yok** → hata değil: her onay tier-2 sayılır (D11/1), `SodPolicySource = DefaultAllTier2`.
- **SoD ayar deposu erişilemiyor** → Submit/karar 503 `PAYMENT_SOD_POLICY_UNAVAILABLE`, uzak çağrı yok, kod varsayılanına
  düşülmez.
- **Release** → 200, `dispatchOutcome = NotConfigured`, `PaymentDispatchAttempt` yazılı, parti `Approved` kalır; bankaya
  ağ çağrısı yapılmaz.
- **Port sözleşme ihlali** (sürüm, geçersiz istek, sözleşme dışı istisna) → 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION`,
  işlem geri alınır: deneme, sıra artışı, makbuz ve olay yok.
- **Release sırasında iptal** → işlem geri alınır, kayıt yok; geri alınan deneme anahtar rezerve etmez.
- **`ReleaseSequence` tükendi** (999 999 999) → 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED`, port çağrısı ve kayıt yok.
- **Beklenmeyen bağdaştırıcı sonucu** (`Accepted` / `Rejected` / `Unknown`) → 502 `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`,
  deneme alınan sonuçla kayıtlı, parti `Approved` + `ReleaseBlockedForReview`; sonraki Release 409
  `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW`, port çağrılmaz (D17/2).

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (kaynakta MOD-0126 denetleyicilerinde
  `[HasPermission]` yok, paket §C — level-1 koşul 4).
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça, çok kelimede tire. **Ad alanı önerisi `finance-treasury.*`**
  (procurement'ın `procurement.*` deseni; domain-config "İzin anahtarı öneki (OPEN)") — MOD-0018 kaydı ve sahip kararı
  **O-5**. Kaynak `mod0126.*` anahtarları **kopyalanmaz**; aşağıdaki eşleme yalnız izlenebilirlik içindir (kaynak anahtar
  hiçbir zaman yayımlanmadığından takma ad gerekmez — PKS-001 §3 yalnız yayımlanmış anahtar için).
- Aktör: `tenant_user`; `platform_admin` geçişi standarttır. Arka plan aktörü bu dilimde yok.

| Recovery anahtarı | Katman | Uçlar | Kaynak anahtar (`PermissionContracts.cs:5-22@da4abb20`) |
|---|---|---|---|
| `finance-treasury.payment-batches.read` | T1 | GET liste, GET detay | `mod0126.payment_batch.read` |
| `finance-treasury.payment-batches.read-tenant-wide` | T3 (yalnız açık atama, `ExplicitGrantOnlyPermissions`) | kiracıdaki tüm şirketlerin partileri, izi ve istisnaları (SEC-002 §3.5, D14/1) — **yalnız okuma**; hiçbir yazma komutunun kapsamını genişletmez (gap 4/G1) | — (Recovery eki) |
| `finance-treasury.payment-batches.create` | T1 | POST | `mod0126.payment_batch.propose` |
| `finance-treasury.payment-batches.update` | T1 | PUT | — (Recovery eki) |
| `finance-treasury.payment-batches.delete` | T1 | DELETE | — |
| `finance-treasury.payment-batches.bulk-delete` | T2 | DELETE bulk | — |
| `finance-treasury.payment-batches.validate` | T3 (sözlükte var) | validate | `mod0126.payment_batch.validate` |
| `finance-treasury.payment-batches.submit` | T3 | submit | `mod0126.payment_batch.submit_workflow` |
| `finance-treasury.payment-batches.reconcile` | T3 | workflow/reconcile | — |
| `finance-treasury.payment-batches.approve` | T2 | approve | `mod0126.payment_batch.approve` |
| `finance-treasury.payment-batches.tier2-approve` | T3 | approve (tier-2 parti, handler içi) | `mod0126.payment_batch.tier2_approve` |
| `finance-treasury.payment-batches.waive-sod` | T3 | approve-with-sod-waiver | `mod0126.payment_batch.waive_sod` |
| `finance-treasury.payment-batches.reject` | T2 | reject | `mod0126.payment_batch.reject` |
| `finance-treasury.payment-batches.request-evidence` | T3 | request-evidence | `mod0126.payment_batch.request_evidence` |
| `finance-treasury.payment-batches.release` | T3 | release | — |
| `finance-treasury.payment-trace.read` | T1 | trace | `mod0126.payment_trace.read` |
| `finance-treasury.payment-attestations.create` | T1 | attestations | `mod0126.payment_attestation.generate` |
| `finance-treasury.payment-evidence.link` | T3 (sözlükte var) | evidence-links | `mod0126.payment_evidence.link` |
| `finance-treasury.payment-evidence.read` | T1 | detay sekmesi | `mod0126.payment_evidence.read` |
| `finance-treasury.payment-exceptions.read` | T1 | GET payment-exceptions | (kaynakta `read` altında) |

- **Taşınmayan kaynak anahtarlar:** `mod0126.audit-outbox.dispatch` (PP-11), `mod0126.payment_batch.admin_direct_proxy`
  (PP-05), `mod0126.payment_evidence.unlink` (kaynakta bildirilmiş, kullanılmıyor — paket §E), `mod0126.payment_evidence.evaluate`
  (değerlendirme akışı bu dilimde yok; O-16).
- Kullanıcının MOD-0023 tarafında da `platform.workflow.instances.start` (gönderen) ve `platform.workflow.tasks.approve` /
  `.reject` / `.request-info` (onaylayıcı) izinleri olmalıdır (`WorkflowModels.cs:14-19`); rol şablonu O-5 içinde.
- Tier-3 eylemler (`read-tenant-wide`, `submit`, `reconcile`, `tier2-approve`, `waive-sod`, `request-evidence`,
  `release`; `escalate` v1 dışı — D22/1) bu pakette kayıtlıdır (PKS-001 §5 Tier 3). `read-tenant-wide` hiçbir rol şablonunda varsayılan
  değildir (O-5).

## Audited Events

Yol `b`: `Diten.BuildingBlocks.CentralAudit` ortak ileticisi (AUD-001 §5 K4) — komut `ICentralAuditDeclaration` taşır,
servis `AddCentralAudit<FinanceTreasuryCentralAuditScope>(…, RequireEnlistment = true)` kaydeder, her depo işlemi olayı
**aynı Mongo işlemine** yazar (`CentralAuditTransaction.EnlistAsync`): iş verisi ve merkezi kayıt birlikte commit olur ya
da hiçbiri. Servisin denetim altyapısı **bugün yok** (servis yok): kurulum DL-1'in ilk adımıdır ve defter satırı CT kararı
ister (Dependencies). Kaynak MOD-0021 aktarımı (kapalı, FailClosed — paket §C, §L.2) taşınmaz.

`Category`: Platform `AuditCategory`'de finans değeri yok (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-27`);
**O-6** — önerilen: eklemeli `Finance` değeri (Platform sahibi, D9 yoluyla); karar çıkana kadar değer uydurulmaz.
`SourceModule`: `MOD-0126`. `EntityType`: `PaymentBatch` (aksi yazılmadıkça).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | K2 (fail-closed) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreatePaymentBatchCommand | finance-treasury.payment-batch.created | PaymentBatch | b | AfterState alan adları: BatchCode, Status, CurrencyCode, TotalAmount, PaymentCount, RequestedValueDate | evet (öneri) | — |
| UpdatePaymentBatchCommand | finance-treasury.payment-batch.updated | PaymentBatch | b | değişen alan **adları** (değer değil) | evet (öneri) | — |
| DeletePaymentBatchCommand | finance-treasury.payment-batch.deleted | PaymentBatch | b | BeforeState: BatchCode, Status | evet (öneri) | — |
| BulkDeletePaymentBatchCommand | finance-treasury.payment-batch.bulk-deleted | PaymentBatch | b | silinen kimlik sayısı; her kimlik için ayrı varlık kaydı (CentralAudit tek varlık kimliği taşır — çoklu kimlik biçimi DL-1'de ölçülür) | evet (öneri) | — |
| ValidatePaymentBatchCommand | finance-treasury.payment-batch.validated | PaymentBatch | b | Status Draft→Validated, sorun sayısı | evet (öneri) | — |
| SubmitPaymentBatchCommand | finance-treasury.payment-batch.submitted (sonuç, tx2/Reconcile) | PaymentBatch | b | Status Submitted→PendingApproval, WorkflowInstanceId, SodPolicyVersion, IsTier2 (tx1 deneme olayı `…workflow-start-requested`: Status →Submitted, uzak anahtar) | evet (öneri) | — |
| ReconcilePaymentBatchWorkflowCommand | kapattığı niyetin **sonuç** olayı (geçiş matrisi; ör. `…submitted`, `…decision-cancelled`); niyet değişmediyse olay yok | PaymentBatch | b | niyet/karar durumu önce→sonra; bağlı makbuz yalnız `Pending` ise `Completed` | evet (öneri) | — |
| ApprovePaymentBatchCommand | finance-treasury.payment-batch.approved | PaymentBatch | b | karar kimliği, SodResult, Status | **evet — K2 (D11/3)** | — |
| ApprovePaymentBatchWithSodWaiverCommand | finance-treasury.payment-batch.approved-with-sod-waiver | PaymentBatch | b | karar kimliği, SodResult=Waived, kaldırılan kural kodları, muafiyet gerekçe kodu, kanıt sayısı | **evet — K2 (D11/3)** | — |
| RejectPaymentBatchCommand | finance-treasury.payment-batch.rejected | PaymentBatch | b | karar kimliği, ReasonCode, Status | **evet — K2 (D11/3)** | — |
| RequestPaymentBatchEvidenceCommand | finance-treasury.payment-batch.evidence-requested | PaymentBatch | b | karar kimliği, Status | evet (öneri) | — |
| LinkPaymentBatchEvidenceCommand | finance-treasury.payment-batch.evidence-linked | PaymentBatch | b | bağlantı kimlikleri | evet (öneri) | — |
| GeneratePaymentAttestationCommand | finance-treasury.payment-attestation.generated | PaymentAttestation | b | ContentHash, SchemaVersion | evet (öneri) | — |
| ReleasePaymentBatchCommand | finance-treasury.payment-batch.release-attempted | PaymentBatch | b | DispatchAttempt kimliği, Outcome=NotConfigured | evet (öneri) | — |

- **İki aşamalı komutların deneme olayları (Runtime Constraints 4a):** Submit → `finance-treasury.payment-batch.workflow-start-requested`
  (tx1) + sonuç `…submitted` ya da `…workflow-start-refused` (tx2 / Reconcile); Approve / ApproveWithSodWaiver / Reject /
  RequestEvidence → `finance-treasury.payment-batch.decision-requested` (tx1, karar türü alanda) + yukarıdaki
  sonuç olayı (tx2 / Reconcile). Release bu dilimde tek aşamalıdır (aşağıda) ve yalnız `…release-attempted` yazar. Deneme olayı tek başına
  "yapıldı" anlamına gelmez; tabloda yazılan olay **sonuç** olayıdır. K2 kararlarında deneme olayı yazılamazsa uzak çağrı
  yapılmaz. Kapalı ama olumsuz sonuçların olayları (Owned Objects geçiş matrisi): `…workflow-start-refused`,
  `…workflow-start-cancelled`, `…decision-refused`, `…decision-cancelled`. Reconcile kendi tek olayını
  (`…workflow-reconciled`) değil, kapattığı niyetin **sonuç** olayını yazar; niyet değişmediyse olay yok.
- **Kayda yazılmayan:** banka referans değerleri, alacaklı referansları, tutarların satır ayrıntısı, yorum serbest metni,
  token. Değişen alanların yalnız **adı** yazılır (AUD-001 §3).
- **K2 (sahip kararı D11/3, O-6):** Approve / ApproveWithSodWaiver / Reject — para çıkışına yetki veren kararlar; ödeme onayı
  AUD-001 §4.3 listesinde açıkça sayılmıyor (kimlik, yetki, kiracı durumu, GxP, KVKK-özel). Öneri: K2 sayılsın. Uyulabilir
  yol bugün var: `RequireEnlistment = true` ile kayıt iş işlemiyle aynı Mongo işlemine yazılır; outbox yazımı başarısızsa
  işlem geri alınır ve komut başarısız olur (SupplyChain kabulü, `CT-DECISIONS.md` 2026-10-21 satırı "commit'ten önce aynı
  işleme yazar (K2)"). Diğer yazma komutları da aynı yolla yazıldığı için pratikte hepsi kapalı-başarısızdır.
- **Bilinen boşluk G-AUD-1:** `CentralAuditBehavior` başarısız komut için kayıt bırakmaz (`ICentralAuditDeclaration.cs`
  özet yorumu: "A failed command writes nothing"); AUD-001 §3 reddedilen komutun da (`Outcome = Denied`) kaydını ister.
  SoD reddi, MOD-0023 kesin reddi ve yetkisiz denemeler bu dilimde yalnız modülün kendi izinde görünür
  (`PaymentException`, `PaymentApprovalDecision.State`, `PaymentWorkflowStartIntent`). Merkezi ret kaydı CentralAudit
  sahibinin (BL-501) işidir — follow-up; bu paket kendi iletim kopyasını yazmaz.
- **Okuma denetimi:** gerekmez (§7'deki üç sınıfa girmiyor; liste dışa aktarımı bu dilimde yok).
- **Arka plan işi yok:** uzlaştırma süpürücüsü bu dilimde yazılmaz (servis kimliği engeli), bu yüzden İ2 satırı yok.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/payment-batches` ve `/api/payment-exceptions` yok (yeni
  servis). Gerekli rotalar: `/api/payment-batches`, `/api/payment-batches/{everything}`, `/api/payment-exceptions`,
  `/api/payment-exceptions/{everything}` → `localhost:5071`; `UpstreamHttpMethod` GET, POST, PUT, PATCH, DELETE, **OPTIONS**;
  explicit rotalar catch-all'dan önce (`routes.md`).
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür; önkoşul AGENTS.md §3 / `ports.md` 5071 kaydı.
- Kaynak rotaları (`/api/v1/mod0126/*` → 5058, kaynak `gateway/DitenApiGateway/Diten.ApiGateway/ocelot.json:1142-1174@da4abb20`)
  taşınmaz (paket §A).
- Servisten MOD-0023 (`/api/v1/workflow/*`), MOD-0220 (`/api/legal-entities/*`), MOD-0031 (`/api/v1/evidence/*`) ve
  MOD-0048 (`/api/lookups/reference/*`) çağrıları ve veri kapsamı tüketimi (SEC-002, O-19) Gateway üzerinden ya da ortamın
  servis-servis adresinden; adresler yapılandırmadan, eksikse servis başlamaz. Bu rotaların Gateway'de var olduğu DL-1/DL-2
  başında ölçülür.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir,
pack'te asla izlenmez; bu pack'teki her AC yalnız "tanımlı"dır.

- [ ] AC-1 (zorunlu) — `POST /api/payment-batches` geçerli istekle 201 döner; parti `Draft`, `BatchCode` RCS-001 biçiminde
  ortak servisten (istekte gönderilen kod 400), `TotalAmount` ve `PaymentCount` sunucu hesabı; `TenantId`/`LegalEntityId`
  sunucuda yazılı, yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — Ödeyen şirket `OperationalStatus = Active` ve `ApprovalStatus = Approved` değilse Create, Submit,
  her karar ve Release 422 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE` döner ve kayıt değişmez; kontrol her çağrıda MOD-0220
  `lookup-validation`'dan yapılır (D10/1). MOD-0220 erişilemezse 503 ve geçiş yok.
- [ ] AC-3 (zorunlu) — Başka kiracının ya da başka şirketin partisine okuma, güncelleme, silme, karar ve Release 404 döner.
- [ ] AC-4 (zorunlu) — Partiyi oluşturan kullanıcının Approve / Reject / RequestEvidence çağrısı 403
  `PAYMENT_SOD_DENIED` (`FailedSelfApproval`) döner ve MOD-0023'e hiçbir istek gitmez (sahte istemciyle sayılır).
- [ ] AC-5 (zorunlu) — Gönderen kullanıcının ve gönderme anındaki son güncelleyenin kararı 403 `PAYMENT_SOD_DENIED`
  (`FailedSubmitterApproval` / `FailedUpdaterApproval`).
- [ ] AC-6a (zorunlu) — Muafiyet adayı (D13): R1–R3 dışlamasından sonra tier-2 aday yoksa `waive-sod` izni olanlar
  `IsWaiverCandidate = true` ile aday olur ve yalnız muafiyetli onay verebilir (normal Approve 403); tier-2 aday varken
  muafiyet adayı eklenmez (iki ayrı test).
- [ ] AC-6 (zorunlu) — `IsTier2` partide `tier2-approve` izni olmayan onaylayıcı 403 `PAYMENT_TIER2_APPROVER_REQUIRED`
  alır; izni olan geçer.
- [ ] AC-7 (zorunlu) — Gönderme çözümleyicisi ile karar kontrolü **aynı** eşik nesnesini kullanır: kodda tek eşik tipi
  vardır (mimari testi: `Tier2` eşiği taşıyan ikinci bir yapılandırma tipi/özelliği yok), değer partiye ve niyete
  sabitlenir, politika sürümü her kararda kayıtlıdır. Kiracı ayarı yoksa her onay tier-2 sayılır
  (`SodPolicySource = DefaultAllTier2`, D11/1) ve servis normal açılır; ayar deposu erişilemezse Submit/karar 503
  `PAYMENT_SOD_POLICY_UNAVAILABLE` (iki durum ayrı testtir).
- [ ] AC-8 (zorunlu) — Parti para birimi eşik para biriminden farklıysa parti tier-2 sayılır (O-4 farklı karar verirse bu
  madde o kararla yeniden yazılır, numarası korunur).
- [ ] AC-9 (zorunlu) — Muafiyetli onay: `waive-sod` izni yok → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; gerekçe yok → 400
  `PAYMENT_SOD_WAIVER_REASON_REQUIRED`; kanıt yok → 422 `PAYMENT_SOD_WAIVER_REQUIRED`; üçü varsa yalnız R4 hatası kaldırılır
  ve karar `Waived` + kaldırılan kural kodu (`R4`) ile kaydedilir. R1/R2/R3 hatasında muafiyet istemi 403
  `PAYMENT_SOD_WAIVER_NOT_ALLOWED` (D12/1); Release aynı modeli uygular (R11).
- [ ] AC-10 (zorunlu) — Aktör karşılaştırması Guid üzerindendir: aynı kullanıcının büyük/küçük harf farklı yazılmış `sub`'ı
  aynı kişi sayılır; Guid olmayan `sub` ile karar reddedilir.
- [ ] AC-11 (zorunlu) — Submit, MOD-0023'ü çağırmadan önce `PaymentWorkflowStartIntent(Pending)` ve parti `Submitted`'ı
  commit eder; uzak çağrı niyetteki `IdempotencyKey` ile yapılır.
- [ ] AC-12 (zorunlu) — Başlatma zaman aşımı, bağlantı kopması ve boş/okunamayan 2xx yanıtı 202
  `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` üretir; niyet `Unknown`, parti `Submitted`; ret istisnası yazılmaz.
- [ ] AC-13 (zorunlu) — Açık niyet varken **aynı** anahtarla Submit makbuz kuralına uyar (makbuz `Completed` → kayıtlı yanıt,
  202 belirsiz dahil; `Pending` → 202 `PAYMENT_REQUEST_IN_PROGRESS`, kurtarma yok); **farklı**
  anahtarla Submit 409 `PAYMENT_WORKFLOW_START_PENDING` ve uzak çağrı yok; Reconcile aynı anahtarla ilerler ve MOD-0023'te
  aynı parti için ikinci örnek oluşmaz (sahte MOD-0023 anahtar başına tek örnek sayar).
- [ ] AC-14 (zorunlu) — Kesin başlatma reddi 502 `PAYMENT_WORKFLOW_START_FAILED`, niyet `Refused`, parti önceki durumuna
  (Draft/Validated) döner.
- [ ] AC-15 (zorunlu) — Kararlar yerel `Requested` niyetinden sonra MOD-0023'e karar idempotency anahtarıyla gider;
  belirsiz sonuç 202 `PAYMENT_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır.
- [ ] AC-16 (zorunlu) — Idempotency (D12/2): her yazma komutu anahtarsız 400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`; aynı
  kapsam (kiracı, şirket, aktör, işlem, hedef, anahtar) + aynı yük, makbuz `Completed` ise kayıtlı yanıtı, `Pending` ise
  202 `PAYMENT_REQUEST_IN_PROGRESS` döndürür (replay kurtarma yapmaz; kurtarma yalnız Reconcile komutuyla) ve ikinci kayıt, ikinci uzak çağrı
  ya da ikinci merkezi denetim olayı oluşmaz; aynı kapsam + farklı yük 409; aynı anahtar başka aktörde ayrı makbuzdur.
  **İlk yanıtın kaybolduğu** senaryo (istemci yanıtı almadan aynı istekle tekrar) testte ayrıca koşulur.
- [ ] AC-17 (zorunlu) — Merkezi denetim: tek aşamalı komutlar (Create, Update, Delete, Validate, kanıt, onay belgesi)
  tam **bir** olay bırakır (Release dahil — bu dilimde tek işlem); iki aşamalı komutlar (Submit, kararlar) tx1'de tam bir **deneme** olayı ve sonuç
  kesinleştiğinde tx2'de (ya da Reconcile'da) tam bir **sonuç** olayı bırakır; idempotent tekrar yeni olay üretmez. Olay
  iş verisiyle aynı Mongo işleminde yazılır; outbox yazımı zorla başarısız edildiğinde komut başarısız olur ve iş verisi
  yazılmaz (sabotaj testi); uzak başarı sonrası tx2 zorla başarısız edildiğinde niyet açık kalır ve Reconcile uzak çağrıyı
  tekrarlamadan sonuç olayını yazar (kurtarma testi).
- [ ] AC-18 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard`
  yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş (borç satırı eklenmemiş).
- [ ] AC-19 (zorunlu) — Release `Approved` partide 200 ve `dispatchOutcome = NotConfigured` döner, `PaymentDispatchAttempt`
  yazılır, parti `Approved` kalır, hiçbir dış ağ çağrısı yapılmaz; `Approved` olmayan partide 409. Release, karar zincirini
  R1–R4'e göre yeniden doğrular.
- [ ] AC-20 (zorunlu) — Hiçbir varlık, DTO ya da denetim kaydı IBAN, hesap numarası, BIC/SWIFT ya da banka adı alanı taşımaz
  (alan adı taraması testi); banka referansları yalnız Guid.
- [ ] AC-21 (zorunlu) — Her denetleyici aksiyonunda `[HasPermission]` vardır (mimari testi); izinsiz çağrı 403.
- [ ] AC-22 (zorunlu) — Tutarlar `decimal`; para birimi hanesini aşan tutar 400 (yuvarlama yok); talimat para birimi
  partininkinden farklıysa 400.
- [ ] AC-23 (zorunlu) — Soft delete yalnız hiç gönderilmemiş `Draft`/`Validated` partide; `IsDeleted` + `DeletedAt` (UTC)
  birlikte yazılır, silinen parti listede görünmez, kodu yeniden kullanılmaz.
- [ ] AC-24 (zorunlu) — Tüm `Views/FinanceTreasury/PaymentBatches/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"`
  açıkça yazılı; `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` yok;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area FinanceTreasury --module PaymentBatches --format gaps`
  sapma vermez ve `quality-gate-datatable` geçer; `<table data-dt-data-mode="server">` pack ile eşleşir.
- [ ] AC-25 (zorunlu) — 7 dil (`en, tr, fr, es, zh, ar, ru`) resx parite PASS; İngilizce olmayan dosyada İngilizce yer
  tutucu yok; hardcoded metin yok (`grep` 0).
- [ ] AC-26 (zorunlu) — `read` izni olmayan kullanıcı sayfayı açınca kabuk içinde erişim açıklaması görür; başlık, tablo,
  "Yeni" düğmesi çizilmez, yönlendirme olmaz (UAS-001, canlı denendi); SoD nedeniyle karar veremeyen kullanıcıya karar
  düğmesi çizilmez.
- [ ] AC-27 (zorunlu) — Durum filtresi ve rozetleri `PaymentBatchStatus`'un 10 değerini gösterir; enum'da olmayan değer yok.
- [ ] AC-28 (zorunlu) — Sayfa yardım metni `Help.PAYMENTBATCHES.Purpose`, `.Audience`, `.Step1…Step5` + `.Step{n}Title`
  7 dilde var.
- [ ] AC-29 (zorunlu) — `docs/analysis/contracts/payment-batch.openapi.yaml` yayımlanmış; uçlar, hata kodları ve
  idempotency başlığı sözleşmeyle eşleşir (sözleşme testi).
- [ ] AC-30 (zorunlu) — Bir oluşturma, bir güncelleme ve bir silme dev ortamında çalıştırılır; üç merkezi kayıt AUD-001 §8
  sorgusuyla gösterilir ve on alan işaretlenir (rapor ortamın adını yazar).
- [ ] AC-31 (zorunlu) — Yarış testleri (gerçek replica set, paralel istek): (a) aynı aktörün aynı anahtarla iki eşzamanlı
  Approve'u → tek karar, tek uzak geçiş; (b) iki farklı onaylayıcının farklı anahtarla eşzamanlı Approve ve Reject'i → tek
  açık karar niyeti, diğeri 409 `PAYMENT_DECISION_PENDING`, MOD-0023'e tek çağrı; (c) iki eşzamanlı Submit (farklı
  anahtar) → tek başlatma niyeti, diğeri 409. Tekil kısmi indeksler kaldırıldığında (b) ve (c) kırmızıya döner (sabotaj).
- [ ] AC-32 (zorunlu) — Veri kapsamı (SEC-002 §4 kanıt testi, D14/1): aynı yetkiye (`payment-batches.read` + yazma
  izinleri) sahip iki kullanıcı, iki ayrı şirket kapsamı → birinin parti ve istisna listesinde diğerinin şirketinin kaydı
  çıkmaz, detayı / izi 404, kararı ve Release'i 404; hiçbir şirket kapsamı olmayan üçüncü kullanıcı boş liste görür ve her
  detay 404; `read-tenant-wide` izni olan kullanıcı iki şirketin kayıtlarını görür. Çözücü hata verdiğinde liste boş döner.
  Kapsam daraltması (`IFinanceTreasuryDataScopeSource` süzgeci) kaldırıldığında test kırmızıya döner (sabotaj);
  `read-tenant-wide` otomatik yoldan verilmez (`ExplicitGrantOnlyPermissions` listesinde).
- [ ] AC-33 (zorunlu) — Okuma kapsamı yazma kapsamını genişletmez (gap 4/G1): `payment-batches.read-tenant-wide` + bütün
  yazma izinlerine sahip ama LE-B'de **kendi şirket yetkisi olmayan** kullanıcı LE-B partilerini listede ve detayda görür
  (200), ama LE-B partisinde Create (`X-Legal-Entity-Id = LE-B`), Update, Delete, Submit, Approve, Reject, Reconcile ve
  Release 404 `PAYMENT_BATCH_NOT_FOUND` döner; MOD-0023 / MOD-0220'ye çağrı gitmez, kayıt, makbuz ve denetim olayı
  oluşmaz (sahte istemcilerle sayılır). Aynı kullanıcı LE-A'da (kendi yetkisi) aynı komutları çalıştırabilir. Sabotaj:
  yazma yolunda okuma kapsamı kullanıldığında (`TenantWideScope()` yazma kapsamına sızdırıldığında) test kırmızıya döner.
- [ ] AC-34 (zorunlu) — Reconcile önce okur (gap 4/G2; Submit ve her karar türü için ayrı test): (a) uzakta niyetin kendi
  eşleme alanlarıyla (AC-36) örnek / geçiş **var** → şirket `Inactive` ya da `Approved` değilken bile sonuç kaydedilir (MOD-0220 çağrılmaz — D15/2), bağlı
  `Pending` makbuz `Completed`, tek sonuç olayı; (b) okuma 404 / boş / zaman aşımı / 5xx → niyet **`Cancelled` olmaz**,
  `Unknown` kalır, parti ve makbuz değişmez, zaman aşımı / 5xx'te uzak yazma/başlatma çağrısı yok (okuma çağrısı
  yapılır); (c) okuma örnek / geçiş göstermedi + şirket uygun değil → MOD-0023'e uzak yazma/başlatma çağrısı yok, niyet
  `Unknown`, `LastOutcomeCode = PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422, sonuç olayı yok, makbuz `Pending`;
  (d) okuma örnek / geçiş göstermedi + gap 6/P1-a'nın iki koşulu kapalı (AC-35) + şirket uygun + SoD/izin geçer + aktör
  asıl aktör → başlatma / geçiş **aynı
  deterministik anahtarla** yeniden yazılır ve MOD-0023'te tam **bir** örnek / geçiş vardır (sahte MOD-0023 anahtar
  başına tek örnek / geçiş sayar; ikinci Reconcile ikincisini üretmez; yinelenen anahtar hatası `Unknown` bırakır, asla
  `Refused` değil); aktör farklıysa 409 `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`, uzak yazma/başlatma çağrısı yok;
  (e) MOD-0031 kanıt bağlama Reconcile ya da herhangi bir kurtarma yolu tarafından hiç yeniden çağrılmaz; belirsiz
  okumada sonuç belirsiz kalır (sahte MOD-0031 yazma çağrısı sayısı 1). Sabotaj: okuma adımı atlanıp doğrudan yeniden
  yazıldığında, "bulunamadı" `Cancelled`'a bağlandığında, farklı anahtarla yeniden yazıldığında ya da MOD-0031 yeniden
  çağrıldığında test kırmızıya döner.
- [ ] AC-35 (zorunlu) — Silinmiş uzak kayıttan sonra yeniden yazma güvenli sayılmaz (gap 6/P1-a; Submit ve her karar türü
  için ayrı test): sahte MOD-0023'te niyetin örneği / geçişi oluşturulup sonra soft delete edilir (ya da temizlenir) ve
  niyet `Unknown` bırakılır; Reconcile (a) silinmiş kaydı gösteren okumada ve (b) silinmemiş kayıt dönmeyen ve silinmiş durumu göstermeyen okumada niyetin
  ilk denemesi kayıtlı (tx1 commit olmuş) durumda aynı anahtarla yeniden yazmaz: niyet `Unknown`, 202
  `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` / `PAYMENT_DECISION_OUTCOME_UNKNOWN`, parti ve bağlı makbuz değişmez, sahte
  MOD-0023'te yazma çağrısı 0 ve yeni örnek / geçiş yok. Sabotaj: silinmiş durum ve ilk deneme kontrolü kaldırıldığında
  Reconcile ikinci bir örnek / geçiş oluşturur ve test kırmızıya döner.
- [ ] AC-36 (zorunlu) — Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b): (a) aynı partide önceki `SubmissionSequence`'in
  kapanmış (`Approved` / `Rejected` / iptal) örneği ve yeni gönderimin `Unknown` niyeti varken Reconcile yeni niyeti eski
  örnekle kapatmaz; yeni niyet `Unknown` kalır ya da yalnız kendi uzak anahtarıyla eşleşen örnekle kapanır; (b) aynı
  görevde başka bir kararın geçişi (farklı `RemoteIdempotencyKey`, görev ya da eylem) açık karar niyetini kapatmaz;
  (c) okuma yanıtı eşleme alanını (uzak anahtar; geçişte görev kimliği) taşımıyorsa niyet `Unknown` kalır. Sabotaj:
  eşleme yalnız nesne (parti) kimliğine indirildiğinde test kırmızıya döner.
- [ ] AC-37 (zorunlu) — MOD-0269 sözleşme negatif testleri (gap 7/P2-b): `docs/analysis/contracts/bank-dispatch.md` (FROZEN 0.1.0) §5.1'deki her vaka
  bağdaştırıcıda `BankDispatchContractException` ve Release'te 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION` verir,
  hiçbir deneme / makbuz / olay / sıra artışı kalmaz. Ayrıca: (a) iki kaynak hesaplı partide istekteki her talimat kendi
  `SourceBankAccountRef`'ini taşır, birleştirme yok; (b) aynı istemci anahtarıyla tekrar portu çağırmaz ve
  `ReleaseSequence`'i artırmaz; (c) commit edilmiş makbuzun tekrarı aynı yanıtı ve aynı `dispatchKey`'i döner; geri
  alınan deneme hiçbir sıra / anahtar bırakmaz (geri alma sonrası `ReleaseSequence` önceki değerinde, deneme kaydı yok) ve
  arada başka Release commit ederse yeniden deneme sonraki sırayı alır; (c2) `ReleaseSequence = 999 999 999` partide
  409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED`, port çağrısı yok;
  (d) iki eşzamanlı Release aynı sırayı alamaz (biri 409); (e) Release sırasında iptal hiçbir kayıt bırakmaz.
  Sabotaj: `Guid.Empty` ya da toplam kontrolü kaldırıldığında ya da talimatlar parti hesabına indirildiğinde test
  kırmızıya döner.
- [ ] AC-38 (zorunlu) — Beklenmeyen bağdaştırıcı sonucu başarı sayılmaz (D17/2): sahte bağdaştırıcı `Accepted`,
  `Rejected`, `Unknown` döndürdüğünde her biri için Release 502 `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`, deneme alınan
  sonuçla kayıtlı, parti `Approved` ve `ReleaseBlockedForReview = true`, tek `…release-attempted` olayı sonucu taşır;
  sonraki Release 409 `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW` ve port çağrı sayısı değişmez. Sabotaj: sonuç kontrolü
  kaldırılıp her sonuç 200 sayıldığında test kırmızıya döner.
- [ ] AC-39 (zorunlu) — Şirket uygunluk yanıtının sınıflandırılması (TRM-14, gap 9/2): sahte MOD-0220 ile her vaka ayrı —
  tutarlı 200 + `APPROVED` → işlem sürer; tutarlı 200 + `DRAFT` / `SUBMITTED` / `REJECTED` ve 404 → 422
  `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`; `approvalStatus` yok / `null` / `""` / `"approved"` (küçük harf) / `"UNKNOWN"`,
  `legalEntityId` yok ya da farklı, `referenceable = false`, `lifecycleState = SUSPENDED`, ayrıştırılamayan gövde, 403, 500
  ve zaman aşımı → 503 `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`; **reddedilen (422) ya da geçersiz / erişilemeyen (503)
  yanıt vakalarında** iş kaydı, makbuz `Completed` ve uzak yazma / başlatma çağrısı oluşmaz (MOD-0023 ve diğer uzak
  okumalara izin vardır; Reconcile'ın uzak okuması bu yasağa girmez). Pozitif vaka (`APPROVED`) normal akışla sürer.
  Reconcile: var olan uzak sonucu kaydetme yolu şirket `REJECTED` iken de kapanır; aynı anahtarla yeniden yazma yolu
  aynı yanıtla 422, uzak çağrı yok. Sabotaj: `approvalStatus` kontrolü "alan yoksa onaylı" yapıldığında ya da kimlik
  karşılaştırması kaldırıldığında test kırmızıya döner. DL-1'de kapanır (Reconcile kısmı DL-2).
- [ ] AC-40 (zorunlu) — MOD-0023 404'ü kesin ret değildir (gap 17/P1, D21/7): başlatma ve her karar geçişinde sahte
  MOD-0023 404 döndürdüğünde niyet **`Unknown`** kalır (`Refused` değil), iş kaydının durumu geri alınmaz (önceki duruma
  dönüş yok), yanıt 202 `…_OUTCOME_UNKNOWN`, sonuç olayı yok; ardından Reconcile okumasıyla kapanır ya da `Unknown`
  kalır. Sabotaj: 404 tx2'de kesin ret grubuna geri eklendiğinde test kırmızıya döner.

## Test Expectations

- **Birim:** validatorlar (her alan); SoD politika servisi R1–R11 (oluşturan/gönderen/güncelleyen, tier-2 sınır değeri
  `TotalAmount == eşik`, para birimi farkı, muafiyet üç dalı, Guid karşılaştırması); durum makinesi geçişleri; idempotency
  parmak izi; RCS kod portu sahtesiyle kod biçimi.
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı/şirket izolasyonu (404); veri kapsamı (AC-32, sabotajlı); soft
  delete; idempotent tekrar; CAS 409;
  CentralAudit kaydının iş işlemiyle birlikte commit/rollback'i ve outbox yazım hatasında komutun başarısız olması (sabotaj);
  tekil indeksler (aynı kod, aynı anahtar).
- **İş akışı sahte sunucusu:** zaman aşımı, bağlantı kopması, boş 2xx, 5xx, kesin 403, 404 (belirsiz → `Unknown`, D21/7), MOD-0023 "başlatma tamamlanmadı"
  409; aynı anahtarla tekrar → tek örnek; karar geçişinde anahtar iletimi; silinmiş örnek / geçiş (AC-35) ve aynı partinin
  eski örneği (AC-36).
- **MOD-0220 sahtesi:** Active+Approved / Active+Draft / Inactive+Approved / 404 / erişilemez; TRM-14 gelince gerçek
  sözleşmeye karşı sözleşme testi.
- **Mimari:** her aksiyonda `[HasPermission]`; tek eşik tipi; banka verisi alan adı yasağı; `float`/`double` para alanı yok;
  `AuditTrailStandard` filtresi yeşil.
- **Sözleşme:** `payment-batch.openapi.yaml` ile uç şekli; MOD-0269 sözleşmesi gelince `IBankDispatchPort` uyumu.
- **Frontend:** browser smoke (liste, oluştur, düzenle, detay, karar düğmeleri izin/SoD'ye göre); yetkisiz kullanıcı canlı
  denemesi (UAS-001); DataTable doğrulayıcı PASS; RESX 7 dil parite PASS.
- **Build:** `Diten.FinanceTreasuryService` + `Diten.Web` + Gateway 0 hata; yerel kapıda `scripts/` Python testleri de
  koşulur.
- Kaynak testleri (R1, 510/510) **kanıt değildir**, Recovery'de yeniden yazılır; kaynakta hiç olmayan testler (başlatma
  zaman aşımı, boş yanıt, AdminDirect atlatması, eşik ayrışması — paket §N.11) burada zorunludur.

## Page Help (UXH-001)

- Sayfa kodu `PAYMENTBATCHES` (`Nav.Page.PAYMENTBATCHES`); Create/Edit/Details aynı kodun alt sayfalarıdır.
- Anahtarlar (kiracı 7 dil): `Help.PAYMENTBATCHES.Purpose` (ödeme partisi hazırlama ve onaya gönderme; bankaya ödeme
  gitmediği açıkça yazılır), `Help.PAYMENTBATCHES.Audience` (hazine operatörü, onaylayıcı, hazine yöneticisi),
  `Help.PAYMENTBATCHES.Step1…Step5` + `Step{n}Title` (Parti oluştur · Talimat ekle · Doğrula · Onaya gönder · Karar ve
  onay ayrılığı). `Immediate` yok (sayfada onaysız hemen geçerli işlem yok). İlgili sayfa: yok (Görev Merkezi köprüsü bu
  dilimde yok; `PageHelpCatalog` satırı yalnız adım ikonları için).
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact okundu (backend + frontend + `DEV-0001`)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, entity_base, form_field_count)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile birebir (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; SoD kuralları R1–R11
- [x] Failure Path ≥ 4 senaryo (duplicate/idempotency, missing, unauthorized, concurrency) + kiracılar arası + SoD + belirsiz sonuç
- [x] Authorization: izin listesi + policy + aktör
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (yol b)
- [x] Veri kapsamı cümlesi (SEC-002 §4, D14/1) ve kanıt testi (AC-32)
- [x] Sayfa yardım metni anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir; test expectations build/verifier/RESX/smoke/sözleşme kapsıyor

**ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz):**
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); yüksek risk — paketi yazmamış, `high: evet` onaylayıcı
- [ ] DCP (UNASSIGNED-finance-treasury-intake) `approved` ve numarası atanmış
- [ ] SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (O-19; platform-shared-services) — kapsamı soran kod ve AC-32 bu yola bağlı
- [ ] Port 5071 AGENTS.md §3 ve `.antigravity/rules/ports.md`'ye yazıldı; AGENTS §2 (domain/servis) ve §9 (`fin`) güncellendi (TRM-03/04)
- [x] MOD-0269 arayüz sözleşmesi dondu — `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 (KARAR D27/7, D27/8; TRM-06 kapandı)
- [x] Kapı kararları 0.3 / 0.6 / 0.7 (KARAR D27/1, D27/3, D27/4) — T32 sapması bitişi: MOD-0003 çıkana kadar, en geç Treasury v1 sonu
- [ ] TRM-14: MOD-0220 pack değişikliği onaylı + `lookup-validation` yanıtında `ApprovalStatus` + MOD-0220 sözleşme belgesi
- [x] SoD eşiği modeli sahip kararıyla yazıldı (O-4 → D11/1: kiracı ayarı, para birimli; ayar yoksa tier-2)
- [x] Sahip MOD-0126 için kod alma iznini verdi (KARAR D27/5; kaynak S `da4abb20`)
- [ ] RCS-001 ortak kod servisi ve şirket kısa kodu kaynağı (O-9) — yoksa Create yazılamaz
- [ ] Yeni servis defteri + CT-DECISIONS satırı için Control Tower kararı (yol b izi kabulü)
- [x] İzin ad alanı `finance-treasury.*` onayı (O-5 → D11/4)
- [ ] MOD-0023'te MOD-0126 iş akışı şablonu (sahibi platform-shared-services) — DL-2 için
- [ ] MOD-0023 pack §17 (TRM-15) **onaylandı** (CT-APR-02/01; yarış yanıtı politikası KARAR D21/4, D22/3) — DL-2 için. **Testlerin geçmesi ready-for-dev engeli değildir** (gap 16/P2): §17 kodu MOD-0023 sahibinin WP'siyle gelir (D25/1) ve DL-2 kabulünden önce main'de olmalıdır; AC-W5…W14 DL-2 uygulama kabul kapısındadır; geçene kadar yinelenen anahtar hatası `Unknown` (D21/7)
- [ ] Silmeme politikası (KARAR D21/1) MOD-0023 pack §17'de onaylı; silinmiş kayıtlar `isDeleted` / `includeDeleted=true` ile okunur (D25/2; AC-W13) — DL-2 için; AC-W1 (silmeme mimari testi) DL-2 uygulama kabul kapısındadır (gap 16/P2); kapanmazsa 404 / boş okumada anahtarlı yeniden yazma kapalı kalır (gap 6/P1-a)
- [ ] MOD-0023 okuma eşleme alanları (KARAR D21/3) §17'de onaylı — DL-2 için; kod MOD-0023 sahibinin WP'siyle gelir (D25/1), AC-W2/W3 uygulama kabul kapısındadır (gap 16/P2); yoksa okuma niyeti kapatmaz (gap 6/P1-b)
- [ ] MOD-0031 kanıt bağlama dedupe ölçümü — kurtarmada yeniden çağrıyı açmak için önkoşul (DL-3; ölçülmezse yeniden
  çağrı kapalı kalır, gap 5/G2)
- [ ] Para birimi küsurat hanesi kaynağı (O-11) — DL-1 Create için

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Producer paketi: MOD-0126 v1.2 (TRM-B7-01), `source-batch7-v1.0/…/mod0126-transfer-packet-v1.2/`, `manifest.json`
  sha256 `660ab4c00af74ccfdac298979d6a3cd20d9b9ceeca2d526540072e10d4cabcc8` (bu oturumda `shasum -a 256` ile ölçüldü);
  paket dosyası `MOD-0126-TRANSFER-PACKET-v1.2.md` sha256 `7003ea95…a1f5` (manifest girdisi). v1.1 (`f6fab005…`) yerini aldı.
- Kaynak commit: `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d` (S, yalnız bu makinede; `git bundle` yedeği önerildi —
  inceleme §6.5).
- Derleme/test kanıtı (devralınmış, kaynak kaydı): R1 @ da4abb20, BUILD ok, **MOD-0126 TEST 510/510** (toplam 1604/1604) —
  `treasury-build-evidence-20261023/SUMMARY-v1.1.md@ebdc38b8` (paket §O); handoff `source-build-evidence-v1.0` SHA256SUMS
  `7ec2cd3332a4ff8d8f7c5fbfe1885b49a5f1e552ee4954806090b59726c536eb`. Modül eşlemesi sınıf adı sezgiseldir (paket §N.11);
  Recovery hazırlığının ölçüsü değildir.
- Kod alma (D1, kopya değil): her alınan dosya `path@da4abb20` + sha256 ile iz kaydına yazılır; aşağıdaki liste taşınmaz.
- Paket B taslağı (`PKG-B-MOD-0021-FU01-MOD-0126-AUDIT-INGEST-DRAFT-v0.1.md`, güvenilir denetim niyeti alımı) bu pakette
  **kullanılmaz**: yol `b` CentralAudit ortak ileticisidir (K4).

### Level-1 koşulları (`MOD-0126-v1.2.md`) → bu paket

1. Tek, para birimli eşik + politika sürümü → R4–R6, AC-7/8 (değer O-4).
2. Başlatma idempotency anahtarı + yerel niyet + belirsiz sonuç → Runtime Constraints, AC-11…15.
3. LE uygunluğu (MOD-0220 + `ApprovalStatus`) → AC-2, TRM-14; banka referansları opak → AC-20, MOD-0289 follow-up.
4. `[HasPermission]` → Authorization Convention, AC-21.
5. Sözleşme → `payment-batch.openapi.yaml`, AC-29.

### Taşınmayacaklar (do-not-carry)

1. **FailClosed istemci varsayılanlarını uygulama saymak:** kaynakta iş akışı, kanıt, onaylayıcı dizini ve denetim
   istemcileri varsayılan `FailClosed` ve kayıtlı yapılandırmada iletim kapalı (paket §C; `WorkflowClientOptions.cs:7`,
   `AuditDispatchClientOptions.cs:7`, `appsettings.json:106-135@da4abb20`). Recovery'de her port gerçek bir bağdaştırıcıya
   bağlanır ya da (MOD-0269 gibi) açıkça "yapılandırılmamış" sonucunu kaydeder; FailClosed sahte "yapıldı" sayılmaz.
2. `_LayoutBackbone` ve kaynak ekran (`Views/ERP/Treasury/PaymentProposals/Index.cshtml`, yer tutucu `console.log`
   betiği — paket §I).
3. Hardcoded İngilizce (kaynak ekranda 0 `Localizer` — paket §I) ve serbest İngilizce hata iletileri.
4. UI durum listesi uyumsuzluğu (`PendingValidation`, `Released` var; `Submitted`, `Cancelled`, `EvidenceRequested`,
   `Escalated`, `PartiallyApproved` yok — paket §I T22).
5. Uzak-önce-yerel sırası: başlatma (`PaymentWorkflowService.cs:158` yerel kayıt `:235`'ten önce) ve geçişler (`:474`
   önce, `:609` sonra) @da4abb20; belirsiz sonucun ret olarak kaydı (`:171-183`, paket §H.3).
6. İki ayrı, para birimsiz eşik (`SodPolicyContracts.cs:10`, `ApproverPolicyContracts.cs:13` @da4abb20) ve `50_000m` kod
   varsayılanı.
7. AdminDirect vekil yolu (PP-05) ve `PassedByAdminProxy` sonucu (`PaymentLifecycleService.cs:13-15, 271-276, 436-437@da4abb20`).
8. `[Authorize]`-only denetleyiciler ve servis içi izin kontrolü (paket §C, §G); web sayfasının izinsiz doğrudan URL
   erişimi (paket §G).
9. Kullanıcıdan alınan parti numarası ve `CountAsync()+1` sayaç (`PaymentProposalService.cs:126-128, 471-475@da4abb20`);
   `IsDeleted=false` kısmi tekil parti numarası indeksi.
10. SoD reddinde çift istisna satırı (`PaymentWorkflowService.cs:390-412@da4abb20`).
11. Sıralı (büyük/küçük harf duyarlı) ile duyarsız aktör karşılaştırması karışımı; `string` aktör kimliği, `"system"`
    varsayılan `CreatedBy` (`TenantEntity.cs:10@da4abb20`).
12. Kaynak denetim outbox'ı (`PaymentOutboxMessage`, `PaymentOutboxAttemptLog`), `/api/audit/write` istemcisi ve PP-11.
13. `mod0126.*` izin anahtarları, `/api/v1/mod0126` rotaları, `mod0126_payment_*` koleksiyon adları, 5058 portu.
14. Kanıt bağlamanın oluşturma içinde, yerel kayıttan sonra, işlem dışında yapılması (`PaymentProposalService.cs:235-298@da4abb20`).
15. Zaman aşımı yapılandırılmamış `HttpClient` (`I/DependencyInjection.cs:177-190@da4abb20`, paket §H.3).
16. Bildirilmiş ama kullanılmayan `mod0126.payment_evidence.unlink` ve `WaiverApprovers` (paket §E, §L.11).
17. Kaynak bulgusu (MOD-0217 ile ortak 67 dosya, Platform `Contracts/Audit` + `Building.Blocks.Audit`): MOD-0126 kodu
    bunlara derlenmiyor (paket §D) — alınmaz.

### Tasarım notları

- MOD-0023 başlatanın onayını zaten reddediyor; MOD-0126 bunu yeniden yazmaz, yalnız MOD-0023'te olmayan kuralları
  (oluşturan, son güncelleyen, tier-2, muafiyet, para birimli eşik) ekler ve gönderme çözümleyicisinde aday dışı bırakır
  (domain-config "modül kendi SoD kontrolünü icat etmez" ile tutarlı: başlatan kuralı miras, ödeme kuralları ek).
- Aday dışı bırakma + MOD-0023 atama kontrolü nedeniyle oluşturan, gönderen ve son güncelleyen hiçbir zaman aday olmaz;
  sahip kararı D12/1 ile muafiyet yalnız R4'ü (atanmış onaylayıcının tier-2 izni eksikliği) kaldırır. Aday seçimi, karar
  kaydı ve Release (R11) aynı modeli kullanır.
- `target: 2026-12-15` planlama yer tutucusudur, sahip kararı değildir.

## Follow-up Items

- Güvenilir servis kimliği (MOD-0033-FU02, Paket A) gelince: güvenilir başlatma + `start-result` + arka plan uzlaştırma
  süpürücüsü (İ2 tetikleyici; her değişiklik `ReconcilePaymentBatchWorkflowCommand` ile denetlenir).
- MOD-0289 banka hesabı arama/çözümleme — sözleşme taslağı `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` (BANK-ACCOUNT-LOOKUP 1.0.0 DRAFT); dondurulunca
  banka referansı seçimi, dağıtım anında çözümleme ve F-E1…F-E9 / F-503 fikstürlerinin bu modülün sınıflandırıcısına karşı
  koşulması.
- MOD-0269 gerçek bağdaştırıcı, banka geri çağrısı, ekstre → ayrı karar ve paket (D5); o pakette Release iki aşamalı
  olur (`PaymentReleaseIntent`, belirsiz sonuç, Reconcile, sonuç olayı) ve gerçek dağıtım kapısı (hesap sahipliği,
  Tenant/LE, alacaklı eşleşmesi) kapanır.
- `ReleaseBlockedForReview` kilidinin elle inceleme ve açma komutu (D17/2; izin, K2 ve denetim o kararla) — bu sürümde
  kilit açılmaz.
- CentralAudit reddedilen komut kaydı (`Outcome = Denied`) — BL-501 sahibine (G-AUD-1).
- Yeniden başlatması engellenen (şirket uygun değil, SoD/izin reddi, aktör farklı) ve bu yüzden `Unknown` kalan niyetin
  kalıcı kapatılması için açık iptal yolu (gap 4/G2) — sahip kararıyla ayrı komut; bu sürümde niyet açık kalır.
- Görev Merkezi köprüsü (WC-D1: yeni sınıf değil, yapılandırma satırı) — onay görevlerinin panoda görünmesi.
- Alacaklı varlık doğrulaması (MOD-0140 `validate`, MOD-0220, çalışan kaynağı) (O-14).
- AP kaynaklı parti (`ApLinked`, MOD-0119) — TRM-S2 sonrası.
- Kiracı düzeyinde SoD politikası kaydı ve yönetim ekranı (O-4'e bağlı).
- Liste dışa aktarımı (export) ve okuma denetimi gerekirse.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | PP-05 AdminDirect kalsın mı? | (a) düşür (b) SoD ile koru | **KARAR: (a) düşür** — sahip D11/2 (2026-10-24) | sahip (K1) |
| O-2 | PP-11 dispatch-once kalsın mı? | (a) düşür (b) İ3 komutu olarak tut | **(a) düşür** — iletim CentralAudit'in | sahip (K1) |
| O-3 | Taslak düzenleme ve silme (kaynakta yok) | (a) Update + soft Delete/BulkDelete yalnız gönderilmemiş taslak (b) yalnız iptal | (a) — Golden Reference Compact seti; reddedilirse Edit/Delete çıkarılır, sapma kaydedilir | sahip (K1) |
| O-4 | SoD tier-2 eşiği **değeri**, **para birimi**, politika sürüm adı; farklı para birimi kuralı; ortam mı kiracı mı | — | **KARAR (D11/1): kiracı ayarı, para birimli**; ayar yoksa her onay tier-2; farklı para birimi → tier-2. Kalan: politika sürüm adı ve ayar ekranı (Follow-up) | sahip (K1) |
| O-5 | İzin ad alanı ve rol şablonları | `finance-treasury.*` / `treasury.*` / `fin.*` | **KARAR (D11/4): `finance-treasury.*`**; rol şablonları MOD-0018 sahibiyle | sahip + MOD-0018 sahibi |
| O-6 | Onay/ret/muafiyet AUD-001 K2 sınıfı mı; `AuditCategory` değeri | — | **KARAR (D11/3): evet K2**; kalan: eklemeli `Finance` kategorisi (Platform, D9) | CT + sahip |
| O-7 | Onay yüzeyi ve başlatma kimliği | (a) kararlar yalnız MOD-0126 ucundan, kullanıcı token'ıyla genel MOD-0023 uçları (b) güvenilir tüketici yolu | (a) v1 — (b) MDM'ye kilitli; Görev Merkezi'nden verilen karara karşı R11 | sahip + MOD-0023 sahibi |
| O-8 | MOD-0023 şablon kodu, çok adımlı onay (PartiallyApproved) adım tanımı, HTTP zaman aşımı değeri | — | şablon MOD-0023 sahibince; zaman aşımı 15 sn | MOD-0023 sahibi |
| O-9 | RCS-001: tür öneki ve şirket kısa kodu kaynağı; ortak kod servisi | `PAY` / başka | `PAY` (§3'te çakışma yok, ölçüldü); servis BL-565 | sahip + CT (`.antigravity` onayı) |
| O-10 | Onaylayıcı adayları nereden (kaynakta yapılandırma listesi / dizin) | MOD-0023 atama (pozisyon/rol) / izin dizini | MOD-0023 şablonunun atama kuralı + R1–R4 dışlaması; ayrı dizin istemcisi taşınmaz | sahip + MOD-0023 sahibi |
| O-11 | Para birimi küsurat hanesi kaynağı | MOD-0048 `LookupOptionDto.Metadata["minorUnits"]` eki / başka | MOD-0048'e eklemeli alan (D9) | MOD-0048 sahibi |
| O-12 | Geçmiş değer tarihine izin | — | Create/Submit'te bugünden (UTC) önce ret | sahip |
| O-13 | `PaymentMethod` değer listesi | serbest metin / referans listesi | MOD-0048 referans listesi | sahip |
| O-14 | Alacaklı referansının varlık doğrulaması | v1 biçim / sözleşmeli doğrulama | v1 biçim; follow-up | sahip |
| O-15 | MOD-0289 gelene kadar banka referansı giriş biçimi | opak referans alanı / DL-4'ü beklet | opak referans alanı | sahip |
| O-16 | Kanıt gereksinimi değerlendirmesi (`evaluate`) | bu dilim / sonra | sonra | sahip |
| O-17 | Muafiyetin kapsamı (bkz. Tasarım notları) | yalnız R4 / R3+R4 / kaldır | **KARAR (D12/1): yalnız R4** (tier-2 izni eksikliği); R1–R3 ve MOD-0023 kontrolleri muaf tutulamaz | sahip (K1) |
| O-18 | Idempotency-Key zorunluluğu | her yazma komutu / yalnız kritik | **KARAR (D12/2): her yazma komutunda zorunlu**; makbuz kapsamı kiracı + şirket + aktör + işlem + hedef. Makbuz tipi servis genelinde tek: **KARAR (D14/2)** `FinanceTreasuryCommandReceipt` | sahip (K1) |
| O-19 | SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (kapsamın kendisi **KARAR (D14/1)**: `LegalEntityId`, `read-tenant-wide` ayrı izin) | Platform kapsam ucu / servis içi çözücü kaydı | Platform sahibiyle kararlaştırılır; servis ortak tek karar (MOD-0119, MOD-0120 O-14, MOD-0124 ile aynı); `NoOp` kaydı yasak (kapsamı kapatır) | sahip + platform-shared-services |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | servis iskeleti, parti çekirdeği ve denetim altyapısı | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Backend File Convention, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar |
| DL-2 | iş akışı ve onay ayrılığı (SoD) | Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention |
| DL-3 | kanıt, onay belgesi, serbest bırakma (dağıtım kapalı) ve denetim kanıtı | Audited Events |
| DL-4 | kiracı ekranı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events tablosundaki denetim kaydı DL-1'in ilk komutuyla gelir, bölüm DL-3'te K2 sabotaj testi ve AUD-001 §8
dev kanıtıyla kapanır; Validation Rules'un alan kuralları DL-1'de, SoD kuralları DL-2'de, R11 DL-3'te uygulanır). Ortak
bölümler (Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Follow-up Items) her dilimde aşağıdaki AC
kimlikleriyle ölçülür.

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: servis iskeleti, `Features/PaymentBatch/` içinde Create/Update/Delete/BulkDelete/Validate + dört
  sorgu, Domain/Persistence, MOD-0220 ve MOD-0048 bağdaştırıcıları, RCS kod portu, CentralAudit kaydı, denetim defteri,
  servis ortak yapı taşları (`FinanceTreasuryCommandReceipt` + iki makbuz indeksi, `IFinanceTreasuryDataScopeSource` okuma
  ve yazma kapsamıyla, ortak LE / para birimi / kod portları ve bağdaştırıcıları, CentralAudit kaydı — D14/1, D14/2; tek
  sahibi bu dilim, MOD-0119/0120/0124 DL-1'inin önkoşulu — gap 4/G5), `payment-batch.openapi.yaml`. **Neden denetim DL-1'de:** AUD-001 K4 — ileticisi olmayan serviste yeni yazma komutu
  yazılamaz; ilk komutla birlikte gelir. Önkoşul: Ready-for-dev engelleri (DCP, port, TRM-14, O-9, O-11, O-19, CT defter kararı; kod alma
  D27/5 ile verildi). Bitiş: AC-1, 2, 3, 39 (Create kısmı), 16 (Create kısmı), 17, 18, 20, 21, 22, 23, 29, 32, 33 (Create / Update / Delete /
  Validate kısmı).
- **DL-2** — Yollar: Submit, Reconcile, Approve/Reject/RequestEvidence/ApproveWithSodWaiver, SoD politika
  servisi ve tek eşik yapılandırması, MOD-0023 bağdaştırıcısı (zaman aşımlı). **Neden ayrı:** SoD ve belirsiz sonuç
  modülün çekirdeği ve en riskli kısmıdır; bağımsız inceleme tek başına yapılabilsin. Önkoşul: DL-1, O-4, O-7, O-8, MOD-0023
  şablonu, MOD-0023 pack §17 (TRM-15) onayı — kod MOD-0023 sahibinin WP'siyle gelir (D25/1); AC-W1/W2/W5…W14 geçişi dilimin **kabul kapısıdır, önkoşulu değil** (gap 16/P2) (KARAR D21/1–4, D22/2–3; gap 5/G2, gap 6/P1-a, P1-b). Bitiş: AC-40, AC-4…15, 39 (Reconcile kısmı), 16 (kararlar), 31, 33 (Submit, kararlar, Reconcile kısmı), 34, 35, 36.
- **DL-3** — Yollar: LinkEvidence, Attestation (v2), Release + `NotConfiguredBankDispatchAdapter`, R11 doğrulaması, K2 sabotaj
  testi, dev ortamı denetim kanıtı. **Neden ayrı:** MOD-0269 sözleşmesine ve MOD-0031 davranış ölçümüne bağlı; DL-2'yi
  bekletmesin. Önkoşul: DL-2, O-6 (MOD-0269 sözleşmesi dondu — D27/7, TRM-06 kapandı), MOD-0031 dedupe ölçümü (yeniden çağrı için; ölçülmezse
  kurtarmada yeniden çağrı yok, gap 5/G2). Bitiş: AC-16 (kalan), 17 (sabotaj), 19, 30, 37, 38, 33 (Release,
  kanıt, onay belgesi kısmı).
- **DL-4** — Yollar: `Views/FinanceTreasury/PaymentBatches/**`, JS, resx (7 dil), MVC proxy, sayfa yardımı, nav anahtarları;
  Gateway rotası integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** ekran API sözleşmesine
  bağlıdır, arka uç değişmeden ilerler. Önkoşul: DL-1 (liste/oluşturma), DL-2 (karar düğmeleri), Gateway rotası, O-15.
  Bitiş: AC-24…28.
