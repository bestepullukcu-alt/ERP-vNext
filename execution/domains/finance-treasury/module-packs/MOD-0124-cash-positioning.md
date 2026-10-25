---
id: MOD-0124
name: Cash Positioning
domain: finance-treasury
service: Diten.FinanceTreasuryService
service_port: 5071
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: CashPositions
    data_mode: server
  - module: CashPositionThresholdPolicies
    data_mode: client
    data_mode_max_rows: 200
entity_base: EntityBase
status: draft
owner: depo sahibi (K1) / Treasury Intake Control Tower
branch: feature/fin/mod-0124-cash-positioning
started: 2026-10-24
target: 2027-02-15
form_field_count: 13
slice: "TRM-S2 — tüzel kişilik başına nakit pozisyonu: elle girilen bilinen bakiyeler + mutabakat + istisna + kanıt + MOD-0023 onayı + yayım (banka beslemesi ve ekstre KAPALI)"
---

# MOD-0124 — Cash Positioning

> **Durum: `draft` (planlama belgesi).** Bu paket kod, servis iskeleti, port kaydı, gateway rotası ya da kaynak
> projeden kod alma izni vermez. Kod ancak paket `approved` / `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02)
> ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten ve sahip MOD-0124 için kod alma iznini verdikten sonra yazılır
> (sahip kararı D1, `docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md`). Engeller:
> "Ready-for-dev Checklist" ve "Açık kararlar".
>
> **Bağlanan kararlar (yeniden karar verilmez):** D1 (doğrula, sonra uyarlayarak al), D2 (servis
> `Diten.FinanceTreasuryService`), D3 (dal kısa kodu `fin`), D5 (MOD-0269 = önce sözleşme, dağıtım kapalı; dört kapalı
> akış — gerçek dağıtım, banka kimlikli geri çağrı, güvenilir non-HTTP çalışan, MOD-0121'e ekstre yayımı — varsayılmaz)
> — `treasury-intake-owner-decisions-01.md`; D9 (başka modüldeki eklemeli değişiklik onayı; **şirketler arası ilişki
> modeli eklenmez**), D10/1 (şirket `OperationalStatus = Active` **ve** `ApprovalStatus = Approved`), D10/2 (MOD-0220
> eki, TRM-14), D10/3 (port 5071), D11/4 (izin öneki `finance-treasury.*`), D12/2 (her yazma komutunda zorunlu
> `Idempotency-Key`, altı alanlı makbuz kapsamı), D14/1 (veri kapsamı `LegalEntityId`, kiracı geneli ayrı
> `…read-tenant-wide` izni — SEC-002), D14/2 (servis genelinde tek `FinanceTreasuryCommandReceipt`, tanımı MOD-0126
> pack'inde), D14/3 (Cash Positioning yayımı K2 değildir), D14/4 (nakit pozisyonu oluşturan/değiştiren her yazmada şirket
> Active + Approved) — `treasury-intake-owner-decisions-02.md`. Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0124-v1.0.md` (koşul 1–4 bu pakette karşılanır, bkz.
> "Implementation Notes → Level-1 koşulları"). DCP: `execution/portfolio/delivery-capability-packs/UNASSIGNED-finance-treasury-intake.md`
> (taslak; dilim **TRM-S2**, karar 0.3 bekliyor — DCP §8.1).
>
> **Ortak kurallar MOD-0126 ile aynıdır.** `execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md`
> (üç sahip inceleme turundan geçti: `docs/records/audits/2026-10/treasury-intake-handover-review-01/GAP-LIST-01/02/03-MOD-0126.md`)
> idempotency makbuzu, yerel niyet, tek açık niyet, tek geçiş matrisi, iki aşamalı denetim ve Reconcile-yalnız kurtarma
> kurallarının kaynağıdır. Makbuz **servis ortak tek tiptir** (`FinanceTreasuryCommandReceipt`, sahip kararı D14/2; tanım
> MOD-0126 pack Entity Fields — FinanceTreasuryCommandReceipt); niyetler bu paketin **kendi önekli tipleridir** ama MOD-0126
> durum sözlüğünü kullanır (bkz. "Entity Fields → Makbuz"). Çelişkide bu paketin kendi metni geçerlidir; MOD-0126'ya
> yazılmaz — ortak tipin ve kurallarının değişikliği hariç (dört paket birlikte).
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-24, bu worktree:**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0124 --name "Cash Positioning"` → çıkış 0
> (çağıran Control Tower koşturdu). Kayıt dosyasında MOD-0124 satırı henüz yok; satır bu paketle yazılmaz (ayrı iş, D8).
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.0 (`mod0124-transfer-packet-v1.0/`,
> manifest sha256 `b440aa2ab569dce78dfa66daf929c977afd5abf30a0be8ab422b44f449e10bcb`, bu oturumda `shasum -a 256` ile yeniden
> ölçüldü; dört paket dosyasının sha256'sı manifest girdileriyle eşit) ve kaynak kod
> `git -C /Users/natig/Projects/ERP-vNext show da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d:<yol>`. Atıflar `paket §X` ya da
> `Dosya.cs:satır@da4abb20` biçimindedir (yollar `services/DitenErpTreasuryService/src/` altındadır); Recovery atıfları
> `yol:satır` (bu worktree, HEAD `3a1c32b9e`).

## Module Summary

Tüzel kişilik (LE) başına, bir değer tarihi ve tek pozisyon para birimi için **nakit pozisyonu anlık görüntüsü**
(snapshot) sistem-of-record'u: bilinen banka bakiyelerinin kaydı, mutabakat (eşik ve tazelik kontrolü, istisna üretimi),
kanıt paketi, MOD-0023 iş akışıyla onay ve yayım (o gün/para birimi için tek "geçerli" pozisyon). Blueprint 8.8 W-3,
Treasury & Cash; Blueprint bağımlılık kapısı "Data Contract Registry; Integration Monitoring & Reconciliation" (paket §A).

- **Hedef kullanıcı:** hazine operatörü (bakiyeleri girer, mutabakat çalıştırır, kanıt ekler, onaya gönderir), hazine
  onaylayıcısı (onaylar / reddeder), hazine yöneticisi (yayımlar, eşik politikasını yönetir, belirsiz sonucu uzlaştırır).
- **v1 pozisyon kaynağı (banka beslemesi YOK):** kullanıcının elle girdiği **bilinen bakiyeler** (`BalanceSourceKind =
  ManualEntry`) — banka portalından, ekstreden ya da teyit yazısından okunan değer, her satırda "as-of" zamanı ve kaynak
  referansıyla; dayanak belge kanıt paketiyle (MOD-0031) bağlanır. Banka durumu ve ekstre girdileri MOD-0269'a bağlıdır
  ve o akışlar **kapalıdır** (D5) → bu girdiler v1'de **karşılanmamış** sayılır (level-1 koşul 3) ve "Follow-up Items"
  altında ayrı kapıdır. Kaynaktaki yönetişimli içe aktarma (CP-03; MOD-0003 sözleşmesi + MOD-0037 entegrasyon koşusu)
  da v1'de yoktur (O-2, O-3).
- **Kapsam ilkesi:** kaynaktaki 21 HTTP işleminden (CP-01…CP-21, paket §C) v1'e girenler Recovery standartlarına
  uyarlanır; kaynak kusurları taşınmaz ("Implementation Notes → Taşınmayacaklar").
- **Para yetkisi yoktur.** Pozisyon bir ölçümdür; bankaya ya da ödemeye dokunmaz. Kur çevrimi yapılmaz.
- **Kapsam LE başınadır.** Grup (çoklu LE) konsolidasyonu kaynakta yok (paket §E; `CashPositioning` altında
  konsolidasyon kodu 0 — yalnız MOD-0125 `CashForecastSecurity.cs:8` LE listesi taşır) ve Recovery MOD-0220'de LE↔LE ilişki
  modeli yok; sahip şimdilik eklememeyi seçti (D9). Açık karar **O-5**.

## Ownership and Boundaries

- **In-scope:** `CashPosition` anlık görüntüsü + satırları (elle bilinen bakiye); mutabakat (eşik politikası, tazelik,
  bakiye–defter farkı, para birimi uyumu); istisna kaydı (mutabakat çıktısı); kanıt paketi (MOD-0031 referansları);
  MOD-0023'e onaya gönderme (yerel niyet + idempotency + belirsiz sonuç + uzlaştırma); onay / ret ve onay ayrılığı (SoD);
  yayım ve önceki yayımın yerini alma (supersede); yayım sonrası kanıtın MOD-0031'e dışa aktarımı (yerel niyetle);
  geçerli yayımlanmış pozisyonun okunması; eşik politikası yönetimi; iki kiracı ekranı.
- **Out-of-scope:** banka beslemesi, banka durum sinyali, banka ekstresi (MOD-0269, D5 — kapalı); MOD-0121 Bank
  Reconciliation (dış tüketici, kapsam dışı — paket §D, DCP §12); yönetişimli içe aktarma ve entegrasyon koşusu sağlığı
  (MOD-0003, MOD-0037 — O-2, O-3); nakit kategori ana verisi ve otomatik atama kuralları (CP-13…CP-18, O-11); nakit
  tahmini (MOD-0125); ödeme partileri (MOD-0126 — taahhütlü çıkış olarak gösterimi O-6); GL / defter bakiyesi kaynağı
  (MOD-0118, karar 0.7); banka ana verisi ve hesap çözümleme (MOD-0289, MDM — yalnız opak referans); grup
  konsolidasyonu (O-5); yayım olayı ve olay tüketicileri (O-14); kur çevrimi (O-7).
- **Sahip olunan sözleşme:** `CASH-POSITION` (yeni, `docs/analysis/contracts/cash-position.openapi.yaml`, x-owner
  MOD-0124) — kaynakta resmi sözleşme yok (paket §B "ABSENT"). Tek yazıcı MOD-0124 (K15). MOD-0003 yokken sözleşme bu
  dosyada dondurulur (karar 0.3 önerisi T32; O-2).
- **Tüketilen sözleşmeler:** MOD-0220 `lookup-validation` (+ TRM-14 `ApprovalStatus`), MOD-0023 iş akışı başlatma ve
  görev geçişleri, MOD-0031 kanıt bağlantısı, MOD-0048 para birimi (+ küsurat, O-8), Platform veri kapsamı
  (`IDataScopeResolver`, SEC-002 — D14/1).

## Owned Objects

### Varlıklar (kaynak 14 sınıf, `Domain/CashPositioning/CashPositionEntities.cs@da4abb20` → Recovery)

| # | Kaynak sınıf | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `CashPositionSnapshot` (:5-25) | **taşınır** — `CashPositionSnapshot : EntityBase` (+ `ContentRevision`, `ApprovalStatus`, `BalanceSourceKind`, sabitlenen eşik) | çekirdek |
| 2 | `CashPositionLine` (:27-46) | **taşınır, uyarlanır** — `BankAccountId` → opak `BankAccountRef`; `CashCategoryId` → `CashCategoryType` (O-11); `MaskedAccountRef`, `BankBalanceSnapshotId`, `SourceRecordId` taşınmaz; + `BalanceAsOfUtc`, `SourceReference` | elle bilinen bakiye satırı |
| 3 | `BankBalanceSnapshot` (:48-65) | **taşınmaz (v1)** | banka beslemesi hattı; MOD-0269 kapısı (Follow-up) |
| 4 | `CashPositionImportBatch` (:67-79) | **taşınmaz (v1)** | yönetişimli içe aktarma; MOD-0003 + MOD-0037 kapısı (O-2, O-3) |
| 5 | `CashPositionSourceRecord` (:81-98) | **taşınmaz (v1)** | aynı |
| 6 | `CashPositionImportBatchSnapshot` (:100-105) | **taşınmaz (v1)** | aynı |
| 7 | `CashCategory` (:107-118) | **taşınmaz (v1)** | satırda kaynak enum'u `CashCategoryType` kullanılır (O-11) |
| 8 | `CashCategoryAssignmentRule` (:120-133) | **taşınmaz (v1)** | otomatik atama yalnız beslenen satırlar için anlamlı (O-11) |
| 9 | `CashPositionThresholdPolicy` (:135-149) | **taşınır, uyarlanır** — LE + para birimi zorunlu, `IsDefault` ve kiracı geneli politika yok (O-12) | mutabakat eşikleri |
| 10 | `CashPositionException` (:151-167) | **taşınır** (v1 alt kümesi: 3 tür, 2 durum) | mutabakat çıktısı |
| 11 | `CashPositionEvidencePack` (:169-183) | **taşınır** — `SnapshotVersion` → `SnapshotRevision`; `ExportedAtUtc` yerine dışa aktarma niyeti | kanıt; revizyona bağlı (kaynak ek 6, "policy A") |
| 12 | `CashPositionApprovalDecision` (:185-197) | **taşınır + `State`** (niyet/sonuç) | karar niyeti uzak çağrıdan önce yazılır |
| 13 | `CashPositionOutboxMessage` (:199-216) | **taşınmaz** | kaynakta yayım olayı + denetim aktarımı; Recovery'de denetim `CentralAudit` outbox'ıdır (AUD-001 K4); yayım olayı O-14 |
| 14 | `CashPositionOutboxAttemptLog` (:218-229) | **taşınmaz** | teslim denemelerini `CentralAuditDispatcher` tutar |
| + | — | **yeni** `CashPositionApprovalStartIntent` | MOD-0023 başlatmasından önce yerel niyet (level-1 koşul 2) |
| + | — | **yeni** `CashPositionEvidenceExportIntent` | MOD-0031 dışa aktarımından önce yerel niyet (level-1 koşul 2) |
| + | — | `FinanceTreasuryCommandReceipt` (**servis ortak**, yeni tip açılmaz) | istemci idempotency makbuzu (D12/2) — **KARAR (D14/2)**: tek ortak tip, tanım MOD-0126 pack Entity Fields |
| + | — | **yeni** `CashPositionThresholdPolicySlot` | aynı LE + para birimi için çakışan geçerlilik aralığını yarışta engelleyen koruma belgesi |

### Kaynak işlemleri (CP-01…CP-21, paket §C) → Recovery

| Kaynak | Kaynak rota (kısa) | Recovery komut / sorgu | Recovery uç (Gateway `/api/…`) | Karar |
|---|---|---|---|---|
| CP-01 | GET snapshots | `GetCashPositionListQuery` | `GET /api/cash-positions` | taşınır (DataTable server) |
| CP-02 | GET snapshots/{id} | `GetCashPositionByIdQuery` | `GET /api/cash-positions/{id}` | taşınır; CP-10 ve CP-11 içeriği detay yanıtına katılır |
| CP-03 | POST …/import | — | — | **ertelendi**: yönetişimli içe aktarma `SourceContractId` (MOD-0003) ve `IntegrationRunId` (MOD-0037) ister; ikisinin de Recovery'de üreticisi yok (O-2, O-3). v1 karşılığı elle giriş: `CreateCashPositionCommand` |
| CP-04 | POST …/{id}/reconcile | `ReconcileCashPositionCommand` | `POST /api/cash-positions/{id}/reconcile` | **uyarlanır**: `ExpectedVersion` gövdeden değil `If-Match`'ten; istekteki `ThresholdPolicyId` taşınmaz (politika sunucu seçer) |
| CP-05 | POST …/{id}/evidence-packs | `CreateCashPositionEvidencePackCommand` | `POST /api/cash-positions/{id}/evidence-packs` | **uyarlanır**: `SnapshotVersion` / `PackVersion` istemciden değil sunucudan |
| CP-06 | POST …/{id}/approval-decisions | `SubmitCashPositionCommand`, `ApproveCashPositionCommand`, `RejectCashPositionCommand` | `POST /api/cash-positions/{id}/{submit\|approve\|reject}` | **bölünür**: kaynakta istemci `DecisionStatus` gönderiyor ve iş akışı yoksa kararı doğrudan yazıyor (`CashPositionApprovalService.cs:91-108@da4abb20`); Recovery'de gönderme ve her karar türü ayrı komut, karar yalnız MOD-0023 görev geçişiyle |
| CP-07 | POST …/{id}/publish | `PublishCashPositionCommand` | `POST /api/cash-positions/{id}/publish` | **uyarlanır**: tek yerel işlem, uzak çağrı yok; kanıt dışa aktarımı ayrı komuta çıkarıldı |
| CP-08 | GET …/{id}/source-lineage | `GetCashPositionTraceQuery` | `GET /api/cash-positions/{id}/trace` | uyarlanır (satır kaynağı + kanıt + karar + niyet izi) |
| CP-09 | GET …/{id}/exceptions | `GetCashPositionExceptionListQuery` | `GET /api/cash-positions/{id}/exceptions` | taşınır |
| CP-10 | GET …/{id}/evidence | (CP-02 detayı) | — | birleştirildi |
| CP-11 | GET …/{id}/approval | (CP-02 detayı) | — | birleştirildi |
| CP-12 | GET …/{id}/outbox-status | — | — | **düşürülür**: kaynak outbox'ı taşınmaz |
| CP-13…CP-15 | cash-categories | — | — | **ertelendi** (O-11) |
| CP-16…CP-18 | cash-category-assignment-rules | — | — | **ertelendi** (O-11) |
| CP-19 | POST threshold-policies | `CreateCashPositionThresholdPolicyCommand` | `POST /api/cash-position-threshold-policies` | taşınır, uyarlanır (O-12) |
| CP-20 | GET threshold-policies | `GetCashPositionThresholdPolicyListQuery` | `GET /api/cash-position-threshold-policies` | taşınır (DataTable client) |
| CP-21 | GET threshold-policies/{id} | `GetCashPositionThresholdPolicyByIdQuery` | `GET /api/cash-position-threshold-policies/{id}` | taşınır |
| (HTTP dışı) | `IngestBankFeedCommand` | — | — | **taşınmaz**: HTTP ya da çalışan çağıranı yok (paket §C); MOD-0269 kapısı |
| (HTTP dışı) | `CashPositionOutboxDispatcherHostedService` | — | — | **taşınmaz** (yapılandırmada kapalı, paket §C) |

### Recovery ekleri (kaynakta yok)

| Komut / sorgu | Uç | Neden |
|---|---|---|
| `UpdateCashPositionCommand` | `PUT /api/cash-positions/{id}` | Compact `Edit`; yalnız yayımlanmamış ve açık niyeti olmayan anlık görüntü |
| `DeleteCashPositionCommand` | `DELETE /api/cash-positions/{id}` | Golden Reference silme; yalnız hiç gönderilmemiş `Draft`/`Reconciled`, soft delete |
| `BulkDeleteCashPositionCommand` | `DELETE /api/cash-positions/bulk` | Golden Reference zorunlu toplu silme; aynı kural |
| `ReconcileCashPositionWorkflowCommand` | `POST /api/cash-positions/{id}/workflow/reconcile` | belirsiz onaya-gönderme / karar sonucunu MOD-0023'ten okuyup kapatır (kurtarma) |
| `ExportCashPositionEvidenceCommand` | `POST /api/cash-positions/{id}/evidence-export` | kaynakta dışa aktarım yayımın içinde ve yerel kayıttan önce (`CashPositionPublishService.cs:173` → `:226@da4abb20`); Recovery'de yayım sonrası ayrı, iki aşamalı komut |
| `ReconcileCashPositionEvidenceExportCommand` | `POST /api/cash-positions/{id}/evidence-export/reconcile` | belirsiz dışa aktarma sonucunu MOD-0031'den okuyup kapatır (kurtarma) |
| `GetCurrentCashPositionQuery` | `GET /api/cash-positions/current?valueDate=&currencyCode=` | oturum LE'si için geçerli yayımlanmış pozisyon — tüketicilerin (MOD-0125/0126/0127) **okuma** sözleşmesi; olay yerine (O-14) |
| `UpdateCashPositionThresholdPolicyCommand`, `DeleteCashPositionThresholdPolicyCommand`, `BulkDeleteCashPositionThresholdPolicyCommand` | `PUT …/{id}`, `DELETE …/{id}`, `DELETE …/bulk` | Golden Reference Slim seti (kaynakta yalnız oluşturma) |

> **Ad notu.** `ReconcileCashPositionCommand` kaynaktaki **iş** mutabakatıdır (bakiye–eşik–istisna hesabı).
> `ReconcileCashPositionWorkflowCommand` ve `ReconcileCashPositionEvidenceExportCommand` **kurtarma** komutlarıdır
> (belirsiz uzak sonucu kapatır). Üçü ayrı izin ve ayrı denetim olayı taşır; birbirinin yerine kullanılmaz.

- **Sorgular:** `GetCashPositionListQuery`, `GetCashPositionByIdQuery`, `GetCashPositionTraceQuery`,
  `GetCashPositionExceptionListQuery`, `GetCurrentCashPositionQuery`, `GetCashPositionThresholdPolicyListQuery`,
  `GetCashPositionThresholdPolicyByIdQuery`.
- **DTO'lar:** `CashPositionModels.cs` tek dosyada (Golden Reference): `CashPositionListItemDto`, `CashPositionDetailDto`,
  `CashPositionLineDto`, `CashPositionWriteRequest`, `CashPositionLineWriteRequest`, `CashPositionDecisionRequest`,
  `CashPositionExceptionDto`, `CashPositionEvidencePackDto`, `CashPositionEvidencePackWriteRequest`,
  `CashPositionApprovalStateDto`, `CashPositionDecisionDto`, `CashPositionEvidenceExportDto`, `CashPositionTraceDto`,
  `CurrentCashPositionDto`. `CashPositionThresholdPolicyModels.cs`: `CashPositionThresholdPolicyListItemDto`,
  `CashPositionThresholdPolicyDetailDto`, `CashPositionThresholdPolicyWriteRequest`. DTO ve isteklerde `TenantId` ve
  `LegalEntityId` **yoktur**.
- **Portlar (Application):** `ILegalEntityEligibilityPort` (MOD-0220; MOD-0126 ile aynı arayüz, servis içinde tek
  bağdaştırıcı), `ICashPositionWorkflowPort` (MOD-0023), `ICashPositionEvidencePort` (MOD-0031),
  `ICurrencyReferencePort` (MOD-0048; servis içinde ortak), `IRecordCodePort` (RCS-001 ortak kod servisi; servis içinde ortak),
  `IFinanceTreasuryDataScopeSource` (SEC-002 veri kapsamı, D14/1; servis içinde ortak).
- **Frontend rotaları:** `/FinanceTreasury/CashPositions` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`) ·
  `/FinanceTreasury/CashPositionThresholdPolicies`.
- **İzinler:** "Authorization Convention" tablosu.

### Durum makinesi

**`CashPositionStatus`** (kaynak `SnapshotStatus`, `CashPositionEnums.cs:3-11@da4abb20`; sayısal değerler korunur):

| Durum | Değer | Recovery anlamı | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 0 | elle girilmiş, mutabakatı yapılmamış ya da içeriği değişmiş | Create, Update | Update, Reconcile, Delete |
| Reconciled | 2 | mutabakat tamamlandı (istisnalı ya da istisnasız) | Reconcile | Update (→ Draft), Reconcile, kanıt, Submit, Publish (onaylıysa), Delete (hiç gönderilmemişse) |
| Published | 3 | o LE + değer tarihi + para birimi için geçerli pozisyon; **içeriği değişmez** (Validation Rules → Yayım sonrası değişmezlik) | Publish | yalnız Superseded (yeni yayım) |
| Superseded | 4 | yerini yeni yayım aldı; içeriği değişmez | aynı yuvada yeni Publish | — |

- `Imported` (1) ve `Archived` (5) **taşınmaz**: v1'de onları atayan komut yok (içe aktarma ertelendi; arşiv komutu
  kaynakta da yok). Sayısal değerleri ileride aynı anlamla kullanılmak üzere boş bırakılır; yeni değer eklenmez.
- **`ReconciliationStatus`** (kaynak `:13-20`): v1'de `NotStarted` (0), `Completed` (2), `CompletedWithExceptions` (3).
  `InProgress` (1) ve `Failed` (4) kalıcı durum olarak yazılmaz — mutabakat tek işlemdir; başarısız işlem geri alınır.
- **`CashPositionApprovalStatus`** (yeni; kaynakta onay ayrı nesnedir, `SnapshotStatus` değeri değildir — paket §E):
  `None` 0 · `Submitted` 1 (yerel başlatma niyeti yazıldı, MOD-0023 sonucu kesin değil) · `PendingApproval` 2 ·
  `Approved` 3 · `Rejected` 4 · `Cancelled` 5 (yalnız Reconcile, MOD-0023 örneği iptal/terk edilmiş bulunduğunda).
- **İçerik kilidi:** `ApprovalStatus ∈ {Submitted, PendingApproval}` iken ya da herhangi bir niyet açıkken Update,
  Reconcile, kanıt paketi, Delete ve Publish 409 `CASH_POSITION_INTENT_OPEN`. `Approved` iken Update izinlidir ama
  onayı **düşürür** (`ApprovalStatus → None`, geçerli karar `IsCurrent = false`, kanıt paketi `IsCurrent = false`) —
  kaynaktaki "current-decision retirement" (paket §A.1 ek 4) davranışı.

**Niyet ve karar durumları — tek geçiş matrisi** (MOD-0126 ile aynı sözlük; servis geneli durum sözlüğü, D14/2). Açık
durumlar: `Pending` (başlatma, dışa aktarma) / `Requested` (karar) ve `Unknown`. Kapalı durumlar: `Started` (başlatma) /
`Recorded` (karar, dışa aktarma), `Refused`, `Cancelled`. Ayrı bir `Exported` durum adı kullanılmaz (D14/2 hizalaması);
dışa aktarmanın kesin başarısı `Recorded`'dır.

| Niyet | Açık → | tx2 (kesin sonuç) | belirsiz | Reconcile (yalnız adı geçen kurtarma komutu) | Sonuç olayı |
|---|---|---|---|---|---|
| `CashPositionApprovalStartIntent.Status` | `Pending` | `Started` · `Refused` | `Unknown` | `ReconcileCashPositionWorkflowCommand`: `Unknown`/`Pending` → `Started` · `Refused` · `Cancelled` | `…submitted` · `…approval-start-refused` · `…approval-start-cancelled` |
| `CashPositionApprovalDecision.State` | `Requested` | `Recorded` · `Refused` | `Unknown` | `ReconcileCashPositionWorkflowCommand`: `Unknown`/`Requested` → `Recorded` · `Refused` · `Cancelled` | `…approved` / `…rejected` · `…decision-refused` · `…decision-cancelled` |
| `CashPositionEvidenceExportIntent.Status` | `Pending` | `Recorded` · `Refused` | `Unknown` | `ReconcileCashPositionEvidenceExportCommand`: `Unknown`/`Pending` → `Recorded` · `Refused` · `Cancelled` | `…evidence-exported` · `…evidence-export-refused` · `…evidence-export-cancelled` |

- Anlık görüntüye etkisi: başlatma `Started` → `ApprovalStatus = PendingApproval` + `WorkflowInstanceId`; `Refused` →
  `PreviousApprovalStatus`; `Cancelled` → `Cancelled`. Karar `Recorded` + onay (son adım) → `Approved`; `Recorded` + ret →
  `Rejected`; karar `Refused` → `PendingApproval` kalır; `Cancelled` → `Cancelled`. Dışa aktarma niyeti anlık görüntünün
  durumunu değiştirmez (yayım kesindir).
- `Refused` = uzak sistem kesin reddetti; `Cancelled` = uzak örnek/görev iptal ya da kapanmış bulundu (yalnız Reconcile,
  yalnız uzak tarafın **kesin** iptal/kapanış kaydıyla — başlatma ve kararda o örnek için MOD-0023 iptal geçiş kaydı ya
  da `CloseAbandonedWorkflowStart` kapanışı; dışa aktarmada MOD-0031'de o bağlantının kesin iptal kaydı (ölçülmedi;
  ölçülene kadar dışa aktarma niyeti `Cancelled` olmaz); bulunamadı / 404 / zaman aşımı / belirsiz okuma `Cancelled`
  sayılmaz, niyet `Unknown` kalır — Runtime Constraints 5a).

## Entity Fields

Taban: servisin kendi `EntityBase`'i (`entity-base-template.md`): `Id`, `TenantId`, `IsDeleted`, `DeletedAt`,
`CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş modülü olduğu için `CreatedBy` / `UpdatedBy` (`Guid?`,
token `sub`'ından; DTO'dan değil) **zorunlu**. Kaynak taban `TenantEntity` (`Domain/Common/TenantEntity.cs:3,17@da4abb20`)
`DateTime` + `long RowVersion` + `string CreatedBy` taşır ve kaynakta hiçbir yol `IsDeleted = true` yazmaz (paket §F);
Recovery'de `DateTimeOffset` (UTC), `int Version` (iyimser eşzamanlılık), `Guid` aktör kimliği ve `IsDeleted` +
`DeletedAt` birlikte.

**Kapsam alanları (her varlıkta):** `TenantId` (`ITenantContext`) ve `LegalEntityId` (isteğin `X-Legal-Entity-Id`
başlığı, MOD-0220'den doğrulanmış; Recovery R-2 deseni,
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Loads/LoadContextMiddleware.cs:61-81`)
sunucuda yazılır; gövde ya da sorgu parametresiyle gelen `tenantId` / `legalEntityId` 400 ile reddedilir (kaynak da sorgu
`tenantId`'yi reddediyor, `CustomBaseController.cs:191-192@da4abb20`). Alt kayıtlar (satır, istisna, kanıt, karar, niyet,
makbuz) anlık görüntünün `LegalEntityId`'sini taşır ki her sorgu Tenant + LE ile süzülsün.

**Para (SOP §18.8, domain-config "Para / decimal"):**
- Tüm tutarlar `decimal` (Mongo `Decimal128`); `float`/`double` yasak.
- **Para birimi:** anlık görüntü başına **tek pozisyon para birimi** (ISO 4217, 3 büyük harf, MOD-0048 listesinde). Her
  satır pozisyon para birimini taşır; farklı para birimli satır **reddedilir** (kaynak `MixedCurrencyAggregation`,
  `CashPositionPersistenceService.cs:115-120@da4abb20`). Mutabakat yine de para birimi uyumunu savunma olarak denetler
  (kaynak `CashPositionReconciliationService.cs:142-160@da4abb20`).
- **Hassasiyet:** para biriminin küsurat hanesi MOD-0048'den; fazla haneli tutar **yuvarlanmaz, reddedilir**. Hane
  kaynağı **TBD (O-8)**; kaynak yoksa yazma komutları 503 `CASH_POSITION_CURRENCY_PRECISION_UNAVAILABLE` — sabit tablo
  uydurulmaz.
- **Kur çevrimi (FX) yok:** kaynak da çevirmiyor ("MOD-0124 does not perform FX conversion", `CashPositionReconciliationService.cs:156@da4abb20`).
  FX kuru, kur tarihi (as-of) ve kur kaynağı v1'de uygulanmaz; Recovery'de kur kaynağı yok (servislerde `ExchangeRate` /
  `FxRate` geçen dosya 0, ölçüldü). Çok para birimli ya da grup görünümü bu kaynağı ister → **TBD (O-7)**.
- **Toplamlar sunucuda:** `TotalLedgerBalance`, `TotalAvailableBalance`, `TotalRestrictedBalance`, `TotalVariance`,
  `LineCount` istekten alınmaz (kaynak `:267-271`).
- **Zaman:** zaman damgaları UTC (`DateTimeOffset`); `ValueDate` yalnız tarihtir (`DateOnly`). Kaynak LE'nin raporlama saat
  dilimini (`ReportingTimezoneId`) zorunlu tutuyor (`CashPositionPersistenceService.cs:147-150@da4abb20`); Recovery
  `LegalEntity`'de böyle bir alan yok (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:8-61`).
  v1'de saat dilimi çevrimi yapılmaz; tazelik `now(UTC) − BalanceAsOfUtc` ile ölçülür → **TBD (O-10)**.

### CashPositionSnapshot (`cash_positions`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| SnapshotCode | string | RCS-001 OTOMATİK `{ŞİRKET}-{TÜR}-{SAYAÇ}`; tür öneki önerisi `CPS` (O-9); kullanıcı giremez/değiştiremez; silinen kaydın kodu yeniden kullanılmaz | kaynak `CP-{yyyyMMdd}-{8hex}` (`CashPositionPersistenceService.cs:362@da4abb20`) — taşınmaz |
| BalanceSourceKind | enum `ManualEntry`=1 | sunucu sabitler | yeni |
| ValueDate | DateOnly | zorunlu | `:9` |
| PositionCurrencyCode | string(3) | zorunlu, ISO 4217 upper | `:10` |
| Status | `CashPositionStatus` | durum makinesi | `:13` |
| ReconciliationStatus | enum | durum makinesi | `:14` |
| ApprovalStatus | `CashPositionApprovalStatus` | durum makinesi | yeni (kaynakta ayrı nesnede) |
| ContentRevision | int | içerik değiştiğinde (Update, Reconcile) +1; kanıt ve onay **bu değere** bağlanır; `Version`'dan ayrıdır (her yazımda artan eşzamanlılık sayacı) | yeni — kaynak ek 6 "policy A" (paket §A.1) |
| TotalLedgerBalance / TotalAvailableBalance / TotalRestrictedBalance | decimal | sunucu; satır toplamı | `:15-17` (`TotalBankBalance` = defter toplamı, `CashPositionReconciliationService.cs:267`) |
| TotalVariance | decimal? | sunucu; `ExpectedBookBalance` girilmiş satırların farkı | `ReconcileCashPositionResponseDto` (`CashPositionDtos.cs:161`) |
| LineCount | int | sunucu | `SourceCount` (`:18`) karşılığı |
| LatestBalanceAsOfUtc | DateTimeOffset? | sunucu; satırların en yenisi | `:19` |
| SourceHealthStatus | enum `Unknown`=0 / `Healthy`=1 / `Stale`=2 | mutabakat yazar | `:20`; `Partial`/`Failed` MOD-0037 ister — taşınmaz |
| AppliedThresholdPolicyId / AppliedThresholdPolicyVersion | Guid? / int? | mutabakat anında sabitlenir | yeni |
| AppliedWarningAbsoluteAmount / AppliedCriticalAbsoluteAmount / AppliedWarningFreshnessHours / AppliedBlockingFreshnessHours | decimal / decimal / int / int | mutabakat anında sabitlenir (politika sonradan değişse de iz kalır) | yeni |
| SubmissionSequence | int | kaçıncı gönderim; 0 = hiç gönderilmedi | yeni |
| SubmittedBy / SubmittedAt | Guid? / DateTimeOffset? | Submit tx1 | kaynakta kararda (`:190-191`) |
| ContentUpdatedBy | Guid? | son Update ya da Reconcile'ı yapan (SoD R3) | yeni |
| WorkflowInstanceId | Guid? | yalnız kesin başlatma sonucundan; istemci veremez | kaynak istemci girdisini reddediyor (`CashPositionApprovalService.cs:52-53@da4abb20`) — korunur |
| IsCurrentPublished / PublishedAt / PublishedBy | bool / DateTimeOffset? / Guid? | Publish | `:21, :24` |
| PublishedContentHash | string? | Publish tx'inde sunucu yazar: yayımlanan içeriğin (başlık para birimi + değer tarihi, satırlar, toplamlar, `ContentRevision`, geçerli kanıt paketi ve karar kimliği) kanonik SHA-256'sı; yayımdan sonra **hiç değişmez**; dışa aktarma niyeti bu değere bağlanır | yeni (gap 4/G3) |
| SupersededBySnapshotId / SupersededAt | Guid? / DateTimeOffset? | yeni yayım | `:22-23` |
| Notes | string? | trim, ≤ 2000 (öneri) | — |
| CreatedBy / UpdatedBy | Guid? | token `sub` | — |

**Taşınmayan kaynak alanları:** `LegalEntityBaseCurrencyCode` (`:11`; Recovery `lookup-validation` yanıtında taban para
birimi yok — `LegalEntityModels.cs:238-245`; kur çevrimi olmadığı için v1'de gerekmez, O-7), `ReportingTimezoneId` (`:12`,
O-10).

### CashPositionLine (`cash_position_lines`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| SnapshotId, LineNumber | Guid, int | sunucu; 1..200 satır (öneri; kaynakta üst sınır yok, sayfa sınırı 200 — paket §K) | — |
| BankAccountRef | Guid | zorunlu, ≠ Empty; **opak** MOD-0289 referansı; doğrulama yok (MOD-0289 yok) | `BankAccountId` (`:31`); kaynakta MOD-0289 doğrulayıcısı çağrılıyor (`CashPositionPersistenceService.cs:163-172`) |
| CashCategoryType | enum `Operating`=0 / `Restricted`=1 / `Reserve`=2 / `Investment`=3 / `Other`=4 | zorunlu | kaynak enum `CashCategoryType` (`CashPositionEnums.cs:68-75`); `CashCategoryId` yerine (O-11) |
| CurrencyCode | string(3) | sunucu pozisyon para biriminden yazar; istekte farklıysa ret | `AccountCurrencyCode` / `PositionCurrencyCode` (`:34-35`) |
| LedgerBalance | decimal | zorunlu; işaretli (negatif = kredili mevduat / overdraft, öneri O-18) | `:36` |
| AvailableBalance | decimal | zorunlu; işaretli (öneri O-18) | `:37` |
| CollectedBalance | decimal | zorunlu; ≥ 0 (öneri O-18) | `:38` |
| BlockedBalance | decimal | zorunlu; ≥ 0 (öneri O-18) | `:39` |
| RestrictedAmount | decimal | zorunlu; ≥ 0 (öneri O-18) | `:40` |
| ExpectedBookBalance | decimal? | isteğe bağlı; girilirse fark hesaplanır | `:41` |
| VarianceAmount | decimal? | sunucu: `LedgerBalance − ExpectedBookBalance` | `:42`; `CashPositionReconciliationService.cs:202` |
| BalanceAsOfUtc | DateTimeOffset | zorunlu, UTC, ≤ şimdi | `SourceTimestampUtc` (`:43`); kaynak UTC zorunlu tutuyor (`CashPositionPersistenceService.cs:858-861`) |
| SourceReference | string | zorunlu, trim, 1..64; **8+ ardışık rakam içeremez** (hesap numarası sızmasın) | kaynak `SensitiveDigits = \d{8,}` (`CashPositionPersistenceService.cs:13`) — korunur, serbest metin alanlarına uygulanır |
| LineStatus | enum `Active` | v1'de yalnız `Active`; `Excluded` taşınmaz (atayan komut yok) | `:45` |

**Satır tekilliği:** `(SnapshotId, BankAccountRef, CashCategoryType)` tekildir (kaynak `DuplicateLineKey`,
`CashPositionPersistenceService.cs:122-133@da4abb20`; `ux_mod0124_line_snapshot_account_currency_category`). Para birimi
anahtardan çıkarıldı, çünkü satırda tek para birimi var.

> **`form_field_count: 13` → `golden_reference: compact` (ana ekran `CashPositions`).** Oluştur/düzenle formunda
> kullanıcının doldurduğu alanlar: **başlık 3** — ValueDate, PositionCurrencyCode, Notes; **bakiye satırı 10** —
> BankAccountRef, CashCategoryType, LedgerBalance, AvailableBalance, CollectedBalance, BlockedBalance, RestrictedAmount,
> ExpectedBookBalance, BalanceAsOfUtc, SourceReference. **Sayılmayanlar:** SnapshotCode (RCS-001 OTOMATİK),
> BalanceSourceKind (sunucu sabit), LegalEntityId (oturum şirketi, `X-Legal-Entity-Id`), satır CurrencyCode (başlıktan
> türetilir), toplamlar / fark / sağlık (sunucu), Idempotency-Key (başlık; yalnız `FinanceTreasuryCommandReceipt`'te
> saklanır) / CorrelationId (başlık), kaynaktaki `SourceContractId` / `IntegrationRunId` / `SourceSystemCode` /
> `NormalizedBalanceShape` / `SourceBalanceTypeCode` / `ExternalRecordId` (içe aktarma alanları, v1'de yok). 13 > 8 →
> Compact (ayrı Create / Edit / Details).

**Banka verisi yasağı:** hiçbir varlık, DTO ya da denetim kaydı IBAN, hesap numarası, maskeli hesap numarası, BIC/SWIFT,
banka adı ya da sağlayıcı sırrı tutmaz; yalnız opak Guid (domain-config "Banka verisi bu domain'de saklanmaz"). Kaynak
satırda `MaskedAccountRef` saklıyor (`:32`) — taşınmaz; ekranda gösterim MOD-0289 arama sözleşmesi gelince (O-15).

### CashPositionThresholdPolicy (`cash_position_threshold_policies`) — ikinci ekran

| Alan | Tip | Kural | Kaynak (`CashPositionEntities.cs:135-149@da4abb20`) |
|---|---|---|---|
| PolicyCode | string | RCS-001 OTOMATİK; tür öneki önerisi `CTP` (O-9); kullanıcı giremez | `:137` kullanıcı girişi — taşınmaz |
| PolicyName | string | zorunlu, trim, 1..256 (öneri) | `:138` |
| LegalEntityId | Guid | sunucu (oturum LE'si) | `:139` (kaynakta `null` = kiracı geneli) |
| CurrencyCode | string(3) | **zorunlu**, ISO 4217 upper, MOD-0048 | `:140` (kaynakta isteğe bağlı) — tutar eşiği para birimsiz anlamsız (kur çevrimi yok), O-12 |
| WarningAbsoluteAmount | decimal | ≥ 0; küsurat ≤ para birimi hanesi | `:141` |
| CriticalAbsoluteAmount | decimal | ≥ WarningAbsoluteAmount | `:142` |
| WarningFreshnessHours | int | 0..720 (öneri) | `:143` |
| BlockingFreshnessHours | int | ≥ WarningFreshnessHours, ≤ 720 | `:144` |
| EffectiveFrom / EffectiveTo | DateOnly / DateOnly? | `EffectiveTo ≥ EffectiveFrom`; aynı LE + para biriminde çakışan aralık yok | `:147-148` |

- **Taşınmayan:** `IsDefault` (`:145`) ve `ApprovalStatus` (`:146`; kaynak oluştururken doğrudan `Active` yazıyor,
  `CashPositionThresholdPolicyService.cs:89@da4abb20` — Recovery'de politika oluşturulduğunda geçerlidir, ayrı onay yok).
- **Seçim (mutabakat):** `(TenantId, LegalEntityId, CurrencyCode = PositionCurrencyCode)` için `ValueDate`'te geçerli,
  silinmemiş **tek** politika; yoksa mutabakat 422 `CASH_POSITION_THRESHOLD_POLICY_MISSING` (kaynak davranışı,
  `CashPositionReconciliationService.cs:75-79@da4abb20`). Kodda varsayılan eşik yoktur.
- **Çakışma koruması:** Create / Update / Delete, aynı işlemde `CashPositionThresholdPolicySlot` belgesinin `Version`'ını
  CAS ile artırır (`(TenantId, LegalEntityId, CurrencyCode)` tekil); eşzamanlı iki yazım aynı yuvada yazma çakışması alır
  ve biri 409 döner. Çakışan aralık 409 `CASH_POSITION_THRESHOLD_POLICY_OVERLAP`.
- **Silme:** soft delete; mutabakat değerleri anlık görüntüye sabitlendiği için geçmiş etkilenmez.

> **İkinci ekran `CashPositionThresholdPolicies` → Slim.** Form alanları: PolicyName, CurrencyCode,
> WarningAbsoluteAmount, CriticalAbsoluteAmount, WarningFreshnessHours, BlockingFreshnessHours, EffectiveFrom,
> EffectiveTo = **8** (≤ 8 → `slim`). Sayılmayan: PolicyCode (OTOMATİK), LegalEntityId (oturum). Liste tasarımda
> sınırlıdır (LE × para birimi × geçerlilik aralığı) → `data_mode: client`, `data_mode_max_rows: 200`.

### CashPositionException (`cash_position_exceptions`)

Kaynak alanları (`CashPositionEntities.cs:151-167@da4abb20`) şu farklarla: `OwnerUserId` Guid?; `RecommendedAction`
serbest İngilizce metin yerine **resx anahtarı** `RecommendedActionKey` (kaynak metinleri `CashPositionReconciliationService.cs:111,132,156,176,194,218,236`
— taşınmaz); `ResolutionNote` yerine `ResolutionKey`. **Taşınmayan:** `WaiverReasonCode`, `EvidenceArtifactId`, `DueDate`
(istisna muafiyeti / atama komutu kaynakta yok — O-13).

- **v1 `ExceptionType` alt kümesi** (kaynak 15 değer, `CashPositionEnums.cs:122-139`): `StaleBalance` (1), `Variance` (5),
  `CurrencyMismatch` (7). Diğerleri (MissingSource, DuplicateBalance, CategoryAssignmentMissing, ContractMismatch,
  BankAccountInactive, UnauthorizedSource, InvalidPayload …) beslenen kaynaklara bağlıdır; v1'de üreten kural yok —
  taşınmaz (ilgili kapı açılınca eklenir).
- **v1 `ExceptionStatus`:** `Open` (0), `Resolved` (3; yeniden mutabakatta koşul kalkınca). `Assigned`, `InReview`,
  `Waived`, `Closed` taşınmaz (atayan komut yok, O-13).
- **Kurallar (kaynak `:162-241`, aynen):** tazelik > `BlockingFreshnessHours` → `StaleBalance` Critical, engelleyici;
  > `WarningFreshnessHours` → Medium, engelleyici değil; |fark| > `CriticalAbsoluteAmount` → `Variance` Critical,
  engelleyici; > `WarningAbsoluteAmount` → Medium; para birimi uyumsuzluğu → `CurrencyMismatch` Critical, engelleyici.
  Eşik değeri 0 ise o kontrol kapalıdır (kaynak `> 0` koşulu).
- **Tekillik:** satır + tür başına tek açık istisna (kaynak `ux_mod0124_active_exception`); yeniden mutabakat aynı kaydı
  günceller, koşul kalktıysa `Resolved` yapar (kaynak `:249-261`).

### CashPositionEvidencePack (`cash_position_evidence_packs`)

| Alan | Tip | Kural | Kaynak (`:169-183`) |
|---|---|---|---|
| SnapshotId, SnapshotRevision | Guid, int | sunucu; oluşturma anındaki `ContentRevision` | `SnapshotVersion` (`:172`) istemciden geliyordu (`CashPositionDtos.cs:170`) — taşınmaz |
| PackVersion | int | sunucu, anlık görüntü başına artar | `:173` (istemciden) |
| IsCurrent | bool | anlık görüntü başına tek geçerli paket | `:174`; `ux_mod0124_current_evidence_pack` |
| Status | enum `Draft` / `Locked` | Publish kilitler | `:175`; `Exported` taşınmaz — dışa aktarma niyette izlenir |
| EvidenceRefs | `{ Kind, DocumentId, DocumentVersionId? }[]` | en az 1; `Kind ∈ {Source, Reconciliation, Exception, Approval}`; MOD-0031 / belge sürümü kimlikleri; ikili veri yok | dört `string` listesi (`:176-179`) |
| GeneratedAt / LockedAt | DateTimeOffset? | sunucu | `:180-181` |

### CashPositionApprovalStartIntent (`cash_position_approval_start_intents`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| SnapshotId | Guid | — |
| IdempotencyKey | string | **uzak**, deterministik: `mod0124:{snapshotId:N}:start:{SubmissionSequence}`; aynı niyet için hiç değişmez (kaynakta başlatma isteğinde idempotency anahtarı yok, `CashPositionWorkflowService.cs:59-77@da4abb20`) |
| SubmissionSequence | int | kesin ret ya da ret sonrası yeniden gönderme yeni sıra alır |
| Status | enum Pending / Started / Refused / Cancelled / Unknown | geçiş matrisi |
| PreviousApprovalStatus | `CashPositionApprovalStatus` | kesin retteki geri dönüş (None / Rejected / Cancelled) |
| SnapshotRevision | int | gönderme anındaki `ContentRevision` (sabitlenir) |
| WorkflowInstanceId | Guid? | yalnız kesin sonuçtan |
| LastOutcomeCode / LastAttemptAt / AttemptCount | string? / DateTimeOffset? / int | `CASH_POSITION_WORKFLOW_START_*` kodları |
| CorrelationId | string | — |

### CashPositionApprovalDecision (`cash_position_approval_decisions`)

Kaynak alanlar (`:185-197`): `SnapshotId`, `DecisionStatus` (yalnız `Approved` / `Rejected`; kaynaktaki `Pending` karar türü
değildir, `Cancelled` niyet durumudur), `DecidedBy` (**Guid**, kaynakta string), `DecidedAt`, `DecisionNote` (trim, ≤ 1000,
öneri), `WorkflowInstanceId` (Guid), `IsCurrent`, `ApprovedSnapshotRevision` (kaynak `ApprovedSnapshotVersion`, `:187`).
**Çıkarılan:** `SubmittedBy` / `SubmittedAtUtc` (anlık görüntüde ve başlatma niyetinde). **Eklenen:** `State`
(Requested / Recorded / Refused / Cancelled / Unknown — tek geçiş matrisi), `ReasonCode` (Reject için zorunlu),
`WorkflowTaskId`, `WorkflowTransitionId`, `WorkflowIsFinalStep`, `RemoteIdempotencyKey` (MOD-0023 görev geçişine giden
**uzak** anahtar, deterministik `mod0124:{decisionId:N}:transition` — istemci anahtarı değildir), `RequestFingerprint` /
`RequestFingerprintVersion`, `SodResult` (`Passed`, `FailedCreator`, `FailedSubmitter`, `FailedContentUpdater`,
`FailedActorInvalid`).

### CashPositionEvidenceExportIntent (`cash_position_evidence_export_intents`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| SnapshotId, EvidencePackId | Guid, Guid | yalnız `Locked` paket |
| SnapshotContentHash | string | tx1'de anlık görüntünün `PublishedContentHash`'inden kopyalanır; tx2 ve Reconcile anlık görüntünün hash'iyle eşitliği denetler (eşit değilse değişmezlik bozulmuştur → 409 `CASH_POSITION_PUBLISHED_IMMUTABLE`, uzak yazma çağrısı yok). Dışa aktarma anlık görüntüyü **referansla** (kimlik + hash) gösterir, ona yazmaz (gap 4/G3) |
| Items | `{ LinkType, DocumentId, DocumentVersionId?, RemoteKey, Outcome, EvidenceLinkId? }[]` | `RemoteKey` deterministik: `mod0124:{evidencePackId:N}:{linkType}:{documentId}:{documentVersionId}` (kaynak anahtarı `CashPositionEvidenceExportService.cs:205-206@da4abb20`, `mod0124:` önekiyle) |
| Status | enum Pending / Recorded / Refused / Cancelled / Unknown | geçiş matrisi (servis geneli sözlük, D14/2); tüm kalemler bağlandıysa `Recorded`, herhangi biri kesin reddedildiyse `Refused`, herhangi biri belirsizse `Unknown` |
| LastOutcomeCode / LastAttemptAt / AttemptCount | — | — |
| CorrelationId | string | — |

### Makbuz — `FinanceTreasuryCommandReceipt` (servis ortak, D14/2)

**Tip kararı — KARAR (D14/2):** makbuz servis genelinde **tek ortak tiptir**; modül önekli makbuz tipi açılmaz. Tip,
alanlar, altı alanlı kapsam, `Pending`/`Completed` yaşam döngüsü, değişmezlik (`Completed` makbuz bir daha değişmez),
"tekrar asla kurtarma yapmaz" ve "Reconcile yalnız `Pending` makbuzu `Completed` yapar" kuralları **tek yerde**
tanımlıdır: **MOD-0126 pack Entity Fields — FinanceTreasuryCommandReceipt** (koleksiyon
`finance_treasury_command_receipts`). Burada yeniden tanımlanmaz; MOD-0124'e özgü olanlar yalnız şunlardır:

- **`Operation`:** `mod0124:{Komut}` — ör. `mod0124:CreateCashPosition`, `mod0124:SubmitCashPosition`,
  `mod0124:PublishCashPosition`, `mod0124:CreateCashPositionThresholdPolicy`.
- **`TargetId`:** Create için `null`, diğerlerinde anlık görüntü ya da politika kimliği; BulkDelete için `null` (kapsam
  isteğin parmak izindedir).
- **İki aşamalı komutlar** (tx1'de `Pending`): Submit, Approve, Reject, ExportEvidence; `Completed` tx2'de ya da — tx2 hiç
  çalışmadıysa — ilgili kurtarma komutunda (`ReconcileCashPositionWorkflowCommand` /
  `ReconcileCashPositionEvidenceExportCommand`). `LinkedIntentId` başlatma, karar ya da dışa aktarma niyetinin kimliğidir.
- Belirsiz sonuçta kayıtlı 202 yanıtının gösterdiği gerçek sonuç niyette izlenir ve `GET /api/cash-positions/{id}` ile
  `…/trace` üzerinden okunur.
- **İstemci anahtarı yalnız makbuzdadır:** `CashPositionSnapshot`, `CashPositionApprovalDecision`,
  `CashPositionEvidencePack`, niyetler istemci anahtarı taşımaz. Uzak anahtarlar (`…StartIntent.IdempotencyKey`,
  `…Decision.RemoteIdempotencyKey`, `…ExportIntent.Items[].RemoteKey`) ayrı, deterministik ve istemciden bağımsızdır.

### CashPositionThresholdPolicySlot (`cash_position_threshold_policy_slots`, yeni)

`TenantId`, `LegalEntityId`, `CurrencyCode`, `Version` — yalnız çakışma koruması; iş verisi taşımaz, ekrana çıkmaz.

### Mongo indeksleri (kaynak 20 ad, 12 tekil — `CashPositionMongoIndexes.cs:8-30@da4abb20`)

| Recovery indeksi | Anahtar | Tekil | Kaynak karşılığı |
|---|---|---|---|
| `ux_cash_position_code` | TenantId + SnapshotCode | **evet, kısmi filtre YOK** (silinen kaydın kodu da dolu, RCS-001 §6) | — (kaynak kodu rastgele) |
| `ux_cash_position_current_published` | TenantId + LegalEntityId + ValueDate + PositionCurrencyCode, kısmi `Status = Published ∧ IsCurrentPublished ∧ ¬IsDeleted` | evet — yayım yuvası | `ux_mod0124_snapshot_current_published` (`:110-123`) — korunur |
| `ix_cash_position_list` | TenantId + LegalEntityId + IsDeleted + Status + ValueDate(desc) | — | `ix_mod0124_snapshot_search` |
| `ux_cash_position_line_key` | TenantId + SnapshotId + BankAccountRef + CashCategoryType | evet | `ux_mod0124_line_snapshot_account_currency_category` |
| `ux_cash_position_open_exception` | TenantId + SnapshotId + LineId + ExceptionType, kısmi `Status = Open` | evet | `ux_mod0124_active_exception` |
| `ix_cash_position_exception_snapshot` | TenantId + SnapshotId + Status + Severity | — | `ix_mod0124_exception_queue` |
| `ux_cash_position_current_evidence_pack` | TenantId + SnapshotId, kısmi `IsCurrent` | evet | `ux_mod0124_current_evidence_pack` |
| `ux_cash_position_current_decision` | TenantId + SnapshotId, kısmi `IsCurrent ∧ State = Recorded` | evet | `ux_mod0124_current_approval_decision` |
| `ux_cash_position_decision_open` | TenantId + SnapshotId, kısmi `State ∈ {Requested, Unknown}` | evet — anlık görüntüde tek açık karar niyeti | yeni |
| `ux_cash_position_start_intent_remote_key` | TenantId + IdempotencyKey | evet — MOD-0023'e aynı anahtarla tek başlatma | yeni |
| `ux_cash_position_start_intent_open` | TenantId + SnapshotId, kısmi `Status ∈ {Pending, Unknown}` | evet — tek açık başlatma niyeti | yeni |
| `ux_cash_position_export_intent_open` | TenantId + EvidencePackId, kısmi `Status ∈ {Pending, Unknown}` | evet — pakette tek açık dışa aktarma niyeti | yeni |
| `ux_cash_position_threshold_policy_code` | TenantId + PolicyCode | evet, kısmi filtre yok | `ux_mod0124_threshold_policy_code` |
| `ux_cash_position_threshold_policy_slot` | TenantId + LegalEntityId + CurrencyCode (slot koleksiyonu) | evet | yeni (kaynak `ux_mod0124_threshold_policy_default` taşınmaz) |
| `ix_cash_position_threshold_policy_selector` | TenantId + LegalEntityId + CurrencyCode + IsDeleted + EffectiveFrom | — | `ix_mod0124_threshold_policy_search` |

**Makbuz indeksleri burada bildirilmez (D14/2):** `ux_finance_treasury_command_receipt_scope` (altı alanlı tekil kapsam) ve
`ix_finance_treasury_command_receipt_pending` servis ortak `finance_treasury_command_receipts` koleksiyonundadır ve sahibi
MOD-0126 DL-1'dir (MOD-0126 pack Entity Fields → Mongo indeksleri); MOD-0124 yeniden bildirmez. Kaynağın içe aktarma
tekrar indeksleri taşınmaz.

Kaynağın içe aktarma, kaynak kaydı, banka bakiyesi, kategori, kural ve outbox indeksleri (8 ad) taşınmaz. İndeks kurulumu
servis iskeletinin indeks barındırılan hizmetiyle yapılır (`mongo-indexing`); taşıma/geri doldurma yok (paket §L.13).

### Hata sözlüğü (kaynak 52 `MOD0124_*`, `Application/Common/CashPositionErrorCodes.cs@da4abb20`; HTTP eşlemesi paket §E)

Recovery öneki `CASH_POSITION_*` (kaynak öneki modül numarasıdır, taşınmaz). Kaynakta 17 kod eşlenip kalanı varsayılan
**400**'e düşüyor (`CustomBaseController.cs:21-37, :185@da4abb20`); Recovery'de her kodun HTTP değeri açıktır:

- **Kaynaktan uyarlanan:** `CASH_POSITION_VALIDATION_FAILED` 400 · `CASH_POSITION_SCOPE_INPUT_FORBIDDEN` 400 (kaynak
  `TENANT_FILTER_FORBIDDEN`) · `CASH_POSITION_NOT_FOUND` 404 (kaynak `REFERENCE_NOT_FOUND`) · `CASH_POSITION_VERSION_CONFLICT`
  409 · `CASH_POSITION_INVALID_CURRENCY_CODE` 400 · `CASH_POSITION_MIXED_CURRENCY` 400 · `CASH_POSITION_DUPLICATE_LINE_KEY` 400 ·
  `CASH_POSITION_SENSITIVE_TEXT_REJECTED` 400 · `CASH_POSITION_PUBLISHED_IMMUTABLE` 409 · `CASH_POSITION_INVALID_STATUS_TRANSITION`
  409 · `CASH_POSITION_RECONCILIATION_NO_LINES` 422 · `CASH_POSITION_THRESHOLD_POLICY_MISSING` 422 ·
  `CASH_POSITION_THRESHOLD_POLICY_VALIDATION_FAILED` 400 · `CASH_POSITION_PUBLISH_RECONCILIATION_REQUIRED` 409 ·
  `CASH_POSITION_PUBLISH_BLOCKED_OPEN_EXCEPTION` 409 · `CASH_POSITION_EVIDENCE_PACK_REQUIRED` 409 ·
  `CASH_POSITION_EVIDENCE_PACK_NOT_CURRENT` 409 · `CASH_POSITION_EVIDENCE_PACK_LOCKED` 409 ·
  `CASH_POSITION_EVIDENCE_REVISION_CONFLICT` 409 · `CASH_POSITION_APPROVAL_REQUIRED` 409 · `CASH_POSITION_APPROVAL_REJECTED` 409 ·
  `CASH_POSITION_APPROVAL_REVISION_CONFLICT` 409 · `CASH_POSITION_APPROVAL_SEPARATION_DENIED` 403 ·
  `CASH_POSITION_APPROVAL_ACTOR_INVALID` 403 · `CASH_POSITION_WORKFLOW_INPUT_FORBIDDEN` 400.
- **Yeni:** `CASH_POSITION_PERMISSION_DENIED` 403 · `CASH_POSITION_IDEMPOTENCY_KEY_REQUIRED` 400 ·
  `CASH_POSITION_IDEMPOTENCY_CONFLICT` 409 · `CASH_POSITION_REQUEST_IN_PROGRESS` 202 · `CASH_POSITION_INTENT_OPEN` 409 ·
  `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE` 422 · `CASH_POSITION_LEGAL_ENTITY_UNAVAILABLE` 503 ·
  `CASH_POSITION_CURRENCY_PRECISION_UNAVAILABLE` 503 · `CASH_POSITION_REFERENCE_UNAVAILABLE` 503 (MOD-0048) ·
  `CASH_POSITION_RECORD_CODE_UNAVAILABLE` 503 · `CASH_POSITION_THRESHOLD_POLICY_OVERLAP` 409 ·
  `CASH_POSITION_PUBLICATION_SLOT_CONFLICT` 409 (kaynakta `SnapshotVersionConflict` içine gömülü, `MongoCashPositionRepository.cs:655-659`) ·
  `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN` 503 · `CASH_POSITION_WORKFLOW_START_FAILED` 502 (yalnız kesin ret) ·
  `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN` 202 · `CASH_POSITION_WORKFLOW_START_PENDING` 409 ·
  `CASH_POSITION_DECISION_OUTCOME_UNKNOWN` 202 · `CASH_POSITION_DECISION_PENDING` 409 ·
  `CASH_POSITION_WORKFLOW_TRANSITION_FAILED` 502 · `CASH_POSITION_EVIDENCE_EXPORT_FAILED` 502 ·
  `CASH_POSITION_EVIDENCE_EXPORT_OUTCOME_UNKNOWN` 202 · `CASH_POSITION_EVIDENCE_EXPORT_PENDING` 409 ·
  `CASH_POSITION_RECONCILE_RESTART_ACTOR_MISMATCH` 409 (Reconcile'ı çağıran asıl komutun aktörü değil; uzak yazma/başlatma
  çağrısı yok, niyet `Unknown` kalır).
- **Taşınmaz:** `OUTBOX_WRITE_FAILED` (kaynakta 400'e düşüyor, paket §E), `IMPORT_*`, `SOURCE_CONTRACT_REQUIRED`,
  `INTEGRATION_RUN_REQUIRED`, `CATEGORY_ASSIGNMENT_MISSING`, `CASH_CATEGORY_*`, `CASH_CATEGORY_RULE_*`,
  `THRESHOLD_POLICY_DUPLICATE_DEFAULT`, `REPORTING_TIMEZONE_MISSING` (O-10), `SUPERSEDE_CONFLICT` (yayım yuvası koduna
  katıldı), `RECONCILIATION_ALREADY_IN_PROGRESS` / `RECONCILIATION_FAILED` (kalıcı ara durum yok),
  `RECONCILIATION_SOURCE_INCOMPLETE`, `DUPLICATE_*` (tekil indeks kodlarına katıldı), `EVIDENCE_REVISION_AMBIGUOUS`.
- Kullanıcıya giden ileti resx anahtarıdır; teknik metin (`403`, `Forbidden`) ekrana çıkmaz (UAS-001 §5).

## Repo Scope

Hepsi kod alma izninden ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.FinanceTreasuryService/**` — **servis yalnız MOD-0126 DL-1'de açılır** (`@orchestrator /add-module`
  iskeleti, port 5071, JWT, `TenantId` izolasyonu, 4 pipeline davranışı, `AddCentralAudit<…>(RequireEnlistment = true)`,
  modül manifest kaydı, servis defteri ve CT kararı). Tek sahibi ve yazarı MOD-0126 DL-1'dir (gap 4/G5; D14/2 ile
  tutarlı); MOD-0126 DL-1, MOD-0124 DL-1'in önkoşuludur. MOD-0124 iskelete **yalnız ekler**; MOD-0124 önce `ready-for-dev`
  olsa bile iskeleti, defteri ya da ortak yapı taşını açmaz — MOD-0126 DL-1'i bekler. (DL-1)
  - `src/Diten.FinanceTreasuryService.Application/Features/CashPosition/**` ve `Features/CashPositionThresholdPolicy/**`
    (Golden Reference düzeni)
  - `src/Diten.FinanceTreasuryService.Domain/CashPositions/**`
  - `src/Diten.FinanceTreasuryService.Persistence/CashPositions/**` (depolar, indeks hizmeti; her işlem geri çağrısı
    `CentralAuditTransaction.EnlistAsync` çağırır)
  - `src/Diten.FinanceTreasuryService.Infrastructure/{Workflow,Evidence}/**` içinde MOD-0124 bağdaştırıcıları; LE,
    para birimi, kod portu ve veri kapsamı (`DataScope`) bağdaştırıcıları servis ortaktır ve MOD-0126 DL-1'indir (burada
    yazılmaz, yeniden tanımlanmaz)
  - Servis ortak yapı taşları (`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`
    — `FinanceTreasuryCommandReceipt` + iki indeksi, `IFinanceTreasuryDataScopeSource` okuma / yazma kapsamı; D14/1,
    D14/2) ve CentralAudit kaydı MOD-0126 DL-1 ile açılır; burada yalnız kullanılır
  - `src/Diten.FinanceTreasuryService.Api/Controllers/CashPositionsController.cs`,
    `CashPositionThresholdPoliciesController.cs`
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` — servis defteri MOD-0126 ile ortak (yol `b`, tek iz);
  MOD-0124 yeni iz açmaz. `AuditTrailStandardTests` sayaçları (`PinnedCounts`) MOD-0124 komutlarıyla değişirse aynı
  değişiklikte güncellenir — **CT kararı ister**. (DL-1)
- `docs/analysis/contracts/cash-position.openapi.yaml` — yeni üretici sözleşmesi (x-owner MOD-0124). (DL-1; DL-3'te
  `current` okuma ucuyla tamamlanır)
- `frontend/Diten.Web/Views/FinanceTreasury/CashPositions/**`, `frontend/Diten.Web/Views/FinanceTreasury/CashPositionThresholdPolicies/**`,
  `frontend/Diten.Web/wwwroot/assets/js/FinanceTreasury/{CashPositions,CashPositionThresholdPolicies}/**`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/CashPositions/CashPositionsIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/CashPositionThresholdPolicies/CashPositionThresholdPoliciesIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/FinanceTreasury/{CashPositionsController,CashPositionThresholdPoliciesController}.cs`
  (same-origin MVC proxy), `PageHelpResource.{7 dil}.resx` içinde `Help.CASHPOSITIONS.*` ve
  `Help.CASHPOSITIONTHRESHOLDPOLICIES.*`, `Services/Help/PageHelpCatalog.cs` satırları, nav anahtarları
  `Nav.Domain.FINANCETREASURY` (MOD-0126 ile ortak), `Nav.Module.CASHPOSITIONING`, `Nav.Page.CASHPOSITIONS`,
  `Nav.Page.CASHPOSITIONTHRESHOLDPOLICIES`. (DL-5)

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9; burada yazılmaz):
MOD-0220 TRM-14 (MOD-0126 ile aynı ek); MOD-0023 iş akışı şablonu (nakit pozisyonu onayı); `ocelot.json` rotaları
(integration-agent); AGENTS.md §2/§3/§9 ve `.antigravity/rules/ports.md` (5071); `record-coding-standard.md` §3
tablosuna `CPS` ve `CTP` satırları; Platform `AuditCategory` eki (MOD-0126 O-6 ile ortak); FinanceTreasury servisinden
SEC-002 veri kapsamı çözücüsünün tüketim yolu (platform-shared-services, O-23). Bkz. "Dependencies".

## Protected Paths

- `.antigravity/**` (port bandı, RCS-001 §3 satırları ve kural değişiklikleri ayrı onay)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.Platform/**` (MOD-0023, MOD-0021, MOD-0048, MOD-0031, MOD-0037, MOD-0003, `IDataScopeResolver` — platform-shared-services),
  `services/Diten.MdmService/**` (MOD-0220, MOD-0289), `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`
  (CentralAudit yalnız tüketilir) ve diğer domain servisleri
- `execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md` ve aynı klasördeki MOD-0119 / MOD-0120
  paketleri — bu paketle değişmez
- `frontend/Diten.Web/Views/Archive/**`, `Controllers/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone` (kaynak
  layout'u; Recovery'de kullanılmaz)
- Diğer üretici sözleşmeleri (`docs/analysis/contracts/*` — tüketilir, yeniden tanımlanmaz)
- `execution/registries/**`, `execution/domains/finance-treasury/domain-config.md`, DCP, CT kuyruğu — bu paketle değişmez
- Kaynak depo `/Users/natig/Projects/ERP-vNext` ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz

## Dependencies

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c) | Bu pakete etkisi |
|---|---|---|---|
| **MOD-0003 Data Contract Registry** (Blueprint kapısı) | kaynak sözleşmesi doğrulama (içe aktarma) | **yok** — kayıt satırı "planned / missing", "no module pack found" (`execution/registries/module-id-registry.md:61`); master plan "planned 0%" (`execution/portfolio/master-development-plan.md:104`); sıra belgesi "0 files" (`docs/roadmap/plans/erp-finance-legal-module-intake-01/TREASURY-MIGRATION-SEQUENCE-v1.0.md`, kapı tablosu). Kaynakta da yalnız Fixture/FailClosed (paket §D) | **karar 0.3 açık** (DCP TRM-S2 kapısı). Önerilen: süreli sapma (T32) + `cash-position.openapi.yaml`'da dondurulmuş sözleşme (O-2). v1 içe aktarma yapmadığı için kod MOD-0003'ü çağırmaz; kapı **karar** olarak kapanır, çağrı olarak değil |
| **MOD-0037 Integration Monitoring & Reconciliation** (Blueprint kapısı) | entegrasyon koşusu ve kaynak sağlığı | **paket var, uygulama yok** — `execution/domains/platform-shared-services/module-packs/MOD-0037-integration-monitoring.md:26-27` "Current MVP status: deferred / not implemented", arayüzler "future-state only" (`:43-45`); kayıt "review / planned" (`module-id-registry.md:132`); master plan "planned 0%" (`master-development-plan.md:72`); `services/` ve `gateway/` altında `MOD-0037` / `IntegrationRun` geçen dosya **0** (ölçüldü). **Not:** sıra belgesi "73 files present" yazıyor (`TREASURY-MIGRATION-SEQUENCE-v1.0.md:38`); bu sayı bu ölçümle doğrulanamadı — çalışma zamanı kodu yok | v1 MOD-0037'yi **tüketmez**: `IntegrationRunId` ve dış kaynak sağlığı (`Partial`/`Failed`) taşınmaz; `SourceHealthStatus` yalnız yerel tazelikten. Blueprint kapısının v1'de tüketilmemesi sahip kararıdır (**O-3**) |
| MOD-0269 Banks/SWIFT/Payments Gateway | banka durumu, ekstre | **yok** (0 dosya; D5 — dağıtım/ekstre akışları kapalı) | banka durumu ve ekstre girdileri **karşılanmamış** (level-1 koşul 3); v1 kaynağı elle bilinen bakiye; banka beslemesi Follow-up kapısı |
| MOD-0121 Bank Reconciliation | ekstre tüketicisi | **yok**; dış bağımlılık / tüketici (paket §D, sahip kararı 4) | kapsam dışı; MOD-0124 MOD-0121'e veri vermez |
| MOD-0023 Workflow Designer | onaya gönderme + görev geçişleri | **var.** `POST api/v1/workflow/instances` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:87`), istekteki anahtar yeniden oynatılır (`StartWorkflowInstanceHandler.cs:76-85`), `GET instances/by-objects` (`WorkflowDefinitionsController.cs:121`), `tasks/{taskId}/approve\|reject` (`:196`, `:221`); atanmamış aktör 403 `WorkflowActorDenied` (`WorkflowTaskTransitionSupport.cs:120-126`); başlatanın **onayı** 409 `SodViolation` (`:134-141` — yalnız Approve) | v1 kullanıcı token'ıyla genel uçtan (MOD-0126 O-7 ile aynı yol). Nakit pozisyonu onay şablonu (`templateCode` TBD, tek adım) MOD-0023'te yayımlanmış olmalı — MOD-0023 sahibinin işi (D9, O-20) |
| MOD-0021 Audit / CentralAudit (BL-501, K4) | yol `b` | **var** — `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/` (`CentralAuditBehavior.cs`, `CentralAuditRecorder.cs`, `CentralAuditDispatcher.cs`) | servis kurulumu MOD-0126 DL-1'de; MOD-0124 her yazma komutunda `ICentralAuditDeclaration`. Servis-başı kopya **yasak** (AUD-001 §5) |
| MOD-0220 LE `lookup-validation` | LE uygunluğu | **var** — `GET /api/legal-entities/{id}/lookup-validation`, izin `mdm.legal-entities.read` (`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48-54`); aktif değilse 404 (`ValidateLegalEntityReferenceHandler.cs:20-23`); yanıt `Id, Code, LegalName, DisplayName, OperationalStatus, IsReferenceable` (`LegalEntityModels.cs:238-245`) — `ApprovalStatus` **henüz yok** | **TRM-14** (D10/2): yanıta eklemeli `ApprovalStatus`; o gelmeden "onaylı" kontrolü yapılamaz → yazma komutları kapanır (fail-closed). Kuralın MOD-0124'e uygulanması **KARAR (D14/4)** (O-16 kapandı) |
| MOD-0220 LE↔LE ilişki modeli | grup konsolidasyonu | **yok** — `LegalEntity` yalnız tek `ParentLegalEntityId` / `OwnershipPercent` / `ControlTypeCode` alanı taşıyor (`LegalEntity.cs:31-33`); geçerlilik tarihli ilişki ya da konsolidasyon kapsamı yok. Sahip: "Şirketler arası ilişki modeli eklenmez" (D9) | v1 **LE başına**; konsolidasyon **O-5** |
| MOD-0289 Bank Master & Bank Account Registry | banka hesabı referansı | **yok**; yerleşim MDM önerisi (D7), TRM-S3 | yalnız boş olmayan opak Guid; hesap sahipliği ve LE uygunluğu doğrulanamaz (O-15) |
| MOD-0048 Reference Data (para birimi) | kod listesi + küsurat hanesi | **kısmen** — `GET /api/lookups/reference/currencies` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/TenantReferenceLookupsController.cs:37`); küsurat hanesi alanı yok (MOD-0126 Dependencies ölçümü) | küsurat kaynağı **TBD (O-8)**; yoksa yazma 503 |
| MOD-0031 Evidence Linking | kanıt bağlantısı | **var** — `api/v1/evidence` (`EvidenceLinksController.cs:19`), `POST links` (`:32`), `POST links/query` (`:72`) | DL-4; bağlama ucunun idempotency (dedupe) davranışı ve `links/query` ile uzlaştırma DL-4 başında ölçülür (O-17); dedupe ölçülene kadar Reconcile MOD-0031'e yazma çağrısı yapmaz, yalnız `links/query` okur (gap 5/G2) |
| MOD-0018 RBAC | `[HasPermission]`, izin tohumu | var | `finance-treasury.*` ad alanı (D11/4); MOD-0124 anahtarlarının kaydı; `read-tenant-wide` yalnız açık atama (`ExplicitGrantOnlyPermissions`) |
| **SEC-002 veri kapsamı** (`IDataScopeResolver`, MOD-0018-FU15) | kullanıcının şirket yetkisi (D14/1) | çözücü yalnız Platform'da kayıtlı (`Diten.Platform.Application/DependencyInjection.cs` → `OrgDataScopeResolver`); ortak kütüphanedeki `NoOpDataScopeResolver` boş liste döner (`.antigravity/rules/data-scope-enforcement.md` §6); FinanceTreasury'den tüketim yolu **yok** | **ENGEL (O-23)**: SEC-002 §4 kapısı — kapsam cümlesi + kapsamı soran kod + kanıt testi olmadan `ready-for-dev` yok. Tüketim yolu servis ortaktır (`IFinanceTreasuryDataScopeSource`); MOD-0126 O-19, MOD-0119 O-22, MOD-0120 O-14 ile **tek karar**. `NoOp` kaydı yasak (kapsamı kapatır) |
| RCS-001 ortak kod servisi (§5.12, BL-565) | anlık görüntü ve politika kodu | **yok** (MOD-0126 Dependencies ölçümü); şirket kısa kodu (§5.4) LE'de ayrı alan değil (`LegalEntity.cs:11` yalnız `Code`) | **engel**: modül kendi sayacını yazamaz (RCS-001 §6); servis gelene kadar Create yazılamaz (O-9) |
| MOD-0126 Payments & Approvals | servis iskeleti + servis ortak yapı taşları; (isteğe bağlı) taahhütlü çıkış | **pack `draft`** (sahip tasarım PASS kaydı, `3a1c32b9e`); kod yok | **MOD-0126 DL-1, MOD-0124 DL-1'in önkoşuludur** (iskelet, makbuz tipi + indeksleri, veri kapsamı kaynağı, ortak LE / para birimi / kod portları, CentralAudit kaydı — tek sahibi o dilim, gap 4/G5); onaylı partilerin pozisyonda gösterimi **O-6** (v1'de yok) |
| MOD-0125 / MOD-0126 / MOD-0127 | yayım tüketicileri | hiçbiri onay vermedi (paket §P PUBLICATION yuvası) | v1'de olay yok; `GetCurrentCashPositionQuery` okuma sözleşmesi (O-14) |
| Port 5071 | servis portu | sahip kararı D10/3; AGENTS.md §3 ve `ports.md` güncellenmedi (TRM-04) | iskelet port kaydından sonra |
| DCP (UNASSIGNED-finance-treasury-intake) | dilim kapısı | `draft`; TRM-S2 karar 0.3'ü bekliyor (DCP §8, §8.1) | DCP `approved` olmadan pack `ready-for-dev` olmaz (DCP §15) |

### Bağımlılık sözleşmeleri — kapanış tablosu

Her satır kapanmadan ilgili dilim başlamaz. **Sahte (mock) istemciyle geçen test canlı entegrasyon kanıtı sayılmaz:**
"sözleşme testi" sahte istemcili testi, "canlı kanıt" gerçek servisle dev ortamında koşulmuş kaydı (`scripts/run_record.py`)
ister.

| Bağımlılık | Sözleşme dosyası / sürüm | Hash | Sahibi | Hata davranışı (MOD-0124) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|---|
| CASH-POSITION (bu modül) | `docs/analysis/contracts/cash-position.openapi.yaml` v1.0.0 — **yazılacak** | TBD (dondurulunca) | finance-treasury | — | sözleşme testi AC-28 · — | DL-1 (DL-3'te `current`) |
| MOD-0003 kararı (0.3) | T32 sapma kaydı — **yok** | — | sahip (K1) | — (çağrı yok) | karar kaydı · — | DL-1 önkoşulu |
| MOD-0037 kapı kararı | sahip kararı — **yok** (O-3) | — | sahip (K1) + platform-shared-services | — (çağrı yok) | karar kaydı · — | DL-1 önkoşulu |
| MOD-0220 LE uygunluğu + `ApprovalStatus` | MOD-0220 sözleşme belgesi — **yazılacak** (TRM-14) | TBD | master-data-management (D10/2) | 422 uygun değil · 503 erişilemez · geçiş yok | sahte LE istemcisi · dev ortamında Active+Approved / Approved-değil iki kayıt | DL-1 |
| MOD-0048 para birimi + küsurat | `GET /api/lookups/reference/currencies` + küsurat alanı — **yok** (O-8) | TBD | platform-shared-services | hane yok → 503 | sahte · dev ortamında TRY/EUR/JPY | DL-1 |
| RCS-001 ortak kod servisi | **yok** (BL-565, O-9) | — | CT + `.antigravity` onayı | servis yoksa Create yazılamaz | — · — | DL-1 önkoşulu |
| SEC-002 veri kapsamı (D14/1) | Platform `IDataScopeResolver` tüketim yolu — **yok** (O-23; dört paket için tek karar) | TBD | platform-shared-services | çözücü hatası → boş kapsam, boş liste, detay / `current` / yazma 404 | kapsam testi AC-31 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-1 |
| CentralAudit | `Diten.BuildingBlocks.CentralAudit` (kod) | Building.Blocks commit SHA'sı DL-1 başında | BL-501 | yazılamazsa komut başarısız | sabotaj testi AC-17 · AUD-001 §8 dev kanıtı AC-29 | DL-1 |
| MOD-0023 başlatma + görev geçişi | Platform `WorkflowModels.cs` (sözleşme dosyası yok) + şablon kodu — **TBD** (O-20) | Platform commit SHA'sı DL-3 başında | platform-shared-services | belirsiz → 202 · kesin ret → 502 · zaman aşımı yapılandırılır · yinelenen anahtar hatası → Unknown (ret değil) | sahte MOD-0023 (anahtar başına tek örnek / geçiş) · dev ortamında gerçek başlatma + onay + ret + **aynı anahtarlı yarış yanıtı ölçümü** (var olan örneği / geçişi döndürdüğü doğrulanmadan yinelenen anahtar hatası `Unknown`) | DL-3 |
| MOD-0023 saklama + silmeme politikası (örnek ve geçiş kaydı; gap 6/P1-a) | **KARAR D21/1–2** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/1: `WorkflowInstance` ve `WorkflowTransitionLog` hiç silinmez, TTL yok; silinmişi ayıran okuma istenmez (D21/2) | §17 onayında | platform-shared-services (politika: sahip K1, D21) | §17 onaylanıp AC-W1 geçene kadar 404 / boş okumada anahtarlı yeniden yazma **kapalı**; niyet `Unknown` | ölçüm (@ef4f3a8bc): silme yolu ve TTL yok — ölçüm politika değildir · MOD-0023 AC-W1 (mimari test, sabotajlı) + bu pack'in silinmiş kayıt testi | DL-3 |
| MOD-0023 okuma eşleme alanları (gap 6/P1-b) | **KARAR D21/3** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/3: `by-objects` ve `instances/{id}` → `IdempotencyKey`; `history` → `ApprovalTaskId` + `IdempotencyKey` (yalnız ekleme) | §17 onayında | platform-shared-services | alan kodda yoksa okuma niyeti kapatmaz → `Unknown` | MOD-0023 AC-W2/W3 + bu pack'in eşleme testi | DL-3 |
| MOD-0031 kanıt bağlama | `api/v1/evidence` (sözleşme dosyası ölçülecek) | DL-4 başında | platform-shared-services | belirsiz → 202 Unknown · kesin ret → 502 · yayım etkilenmez · Reconcile yeniden çağırmaz (yalnız `links/query`) | sahte (Reconcile'da yazma çağrısı sayısı 0) · dev ortamı · **MOD-0031 / dışa aktarma dedupe ölçümü** (aynı `RemoteKey` tekrarı tek bağlantı mı) — kurtarmada yeniden çağrıyı açmanın önkoşulu; ölçülene kadar kapalı (gap 5/G2) | DL-4 |
| MOD-0269 banka beslemesi / ekstre | — (D5, kapalı) | — | sahip (K1); bankacılık sahibi adlandırılmadı | v1'de çağrı yok | — · — | v1 dışı (Follow-up) |

## Runtime Constraints

- Servis portu **5071** (D10/3; AGENTS §3 kaydı bekliyor). Tarayıcı ve `Diten.Web` yalnız Gateway (5000) üzerinden
  çağırır; tarayıcı JS 5071'e gitmez.
- **Kiracı + şirket izolasyonu her sorguda:** `TenantId` `ITenantContext`'ten, `LegalEntityId` `X-Legal-Entity-Id`
  başlığından (MOD-0220'de doğrulanmış); başka kiracı ya da başka şirket kaydı **404** `CASH_POSITION_NOT_FOUND`. Üretimde
  sabit/varsayılan kiracı yok. Okumalar LE'nin kiracıya ait olduğunu doğrular; uygunluk (Active + Approved) yalnız yazma
  komutlarında aranır (geçmiş pozisyon, LE pasifleşse de okunabilir).
- **Veri kapsamı (SEC-002, sahip kararı D14/1) — kapsam cümlesi:** kapsamı taşıyan alan **`LegalEntityId`** (anlık
  görüntü, bütün alt kayıtları ve eşik politikası); çevrilen kapsam türü yalnız **`LegalEntity`** (`OrgUnit`, `Position`,
  `ManagerChain` çevrilmez → daraltır, sızdırmaz; birim→şirket birleştirmesi uydurulmaz); `IsInclude = false` olan
  `LegalEntity` kapsamı o şirketi **kapatır**. Kullanıcı yalnız yetkili olduğu şirketlerin kayıtlarını görür: istek
  başlığındaki şirket kullanıcının kapsamında değilse anlık görüntü ve politika listeleri **boş** (200, 0 satır), detay /
  iz / istisna listesi / `current` / her yazma **404** `CASH_POSITION_NOT_FOUND`; çözücü hatası, kiracı ya da kullanıcı
  yokluğu → boş kapsam (süzgeçsiz sorgu değil). Kiracıdaki tüm şirketleri görmek ayrı izindir:
  `finance-treasury.cash-positions.read-tenant-wide` (anlık görüntüler ve eşik politikaları; yalnız açık atama —
  `ExplicitGrantOnlyPermissions`; tek üretici `FinanceTreasuryDataScope.TenantWideScope()`). Kapsam servis genelinde **tek
  yerde** (`IFinanceTreasuryDataScopeSource`, MOD-0126/0119/0120 ile ortak; modül yalnız kendi `read-tenant-wide`
  anahtarını verir) çözülür; okumalar (liste, detay, iz, `current`, politika ekranı) **okuma kapsamını**, her yazma **yazma
  kapsamını** kullanır (aşağıda); sayfalama ve toplamlar daraltılmış kümeden hesaplanır. Referans uygulama:
  `WorkReportScope` / `WorkReportScopeSource` (SEC-002 §5). Kapsam LE başınadır; grup görünümü O-5'tir.
- **Okuma kapsamı ile yazma kapsamı ayrıdır (gap 4/G1; D14/1 ile tutarlı — `…read-tenant-wide` *görme* iznidir; tanım
  MOD-0126 pack Runtime Constraints, servis ortak):** okuma kapsamı = kullanıcının kendi şirket yetkileri ya da
  `read-tenant-wide` varsa kiracıdaki tüm şirketler; **yazma kapsamı** = yalnız kendi şirket yetkileri (`TenantWideScope()`
  hiçbir zaman girmez). Her yazma komutu (Create, Update, Delete, BulkDelete, Reconcile (iş), kanıt paketi, Submit, Approve,
  Reject, iki kurtarma komutu, Publish, ExportEvidence, eşik politikası Create/Update/Delete/BulkDelete) hedef kaydın
  `LegalEntityId`'si için katman 1'i ister: `X-Legal-Entity-Id` hedef kaydın şirketine eşit **ve** kullanıcının o şirkette
  kendi yetkisi var. Yalnız `read-tenant-wide` sayesinde görünen kayda yazma → **404** `CASH_POSITION_NOT_FOUND` (kapsam
  dışı kuralıyla aynı; başka adım çalışmaz).
- **LE uygunluğu güncel durumdur (TP-001 T14; kaynakta yalnız içe aktarmada, paket §G; sahip kararı D14/4):** nakit
  pozisyonu oluşturan ya da değiştiren **her yazma komutunda** — Create, Update, Delete, BulkDelete, Reconcile (iş
  mutabakatı), kanıt paketi, Submit, Approve, Reject, Publish, ExportEvidence ve eşik politikası
  Create/Update/Delete/BulkDelete — MOD-0220 `lookup-validation` çağrılır; `OperationalStatus = Active` **ve**
  `ApprovalStatus = Approved` değilse 422 `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`; MOD-0220 yanıt vermezse 503
  `CASH_POSITION_LEGAL_ENTITY_UNAVAILABLE` — geçiş yok. **İki kurtarma komutu hariç**
  (`ReconcileCashPositionWorkflowCommand`, `ReconcileCashPositionEvidenceExportCommand`; D15/2): yeni karar başlatmaz,
  uygunluğu tx1'de denetlenmiş komutun uzak sonucunu kaydeder (engellemek niyeti açık bırakırdı; MOD-0126 ile aynı).
  Muafiyet yalnız uzakta **var olan** örneğin / geçişin / bağlantının sonucunu kaydetme yolu içindir; okuma örnek / geçiş
  göstermezse aynı anahtarla yeniden yazma (yalnız MOD-0023 başlatma / geçiş) yeni uzak işlemdir ve şirket + SoD + izin +
  asıl aktör yeniden denetlenir (Runtime Constraints 5a, gap 4/G2, gap 5/G2); dışa aktarma yeniden çağrılmaz. Okumalar
  etkilenmez.
- Soft delete (`IsDeleted` + `DeletedAt` UTC, filtrede Id + TenantId + LegalEntityId). Okumalar `IsDeleted = false`.
- **Idempotency (D12/2 — her yazma komutunda zorunlu):** bütün yazma komutları (anlık görüntü, eşik politikası, kurtarma
  komutları dahil) `Idempotency-Key` başlığı **ister**; yoksa 400 `CASH_POSITION_IDEMPOTENCY_KEY_REQUIRED`. Sunucu
  anahtar **üretmez**.
  - **Makbuz kapsamı:** `(TenantId, LegalEntityId, ActorId, Operation, TargetId, Key)`; tekil indeks bu altılıdadır.
  - **Kontrol sırası (her komutta aynı):** (1) kimlik + `[HasPermission]` → (2) veri kapsamı (SEC-002, D14/1; okumada
    okuma kapsamı, **yazmada yazma kapsamı** — hedef kaydın şirketi kullanıcının kendi şirket yetkisinde değilse, kayıt
    `read-tenant-wide` ile görünse bile → 404 `CASH_POSITION_NOT_FOUND`, başka adım yok) → (3) makbuz araması: aynı kapsam + farklı parmak
    izi → 409 `CASH_POSITION_IDEMPOTENCY_CONFLICT`; aynı kapsam + aynı parmak izi + `Completed` → **kayıtlı yanıt aynen
    döner**; aynı kapsam + aynı parmak izi + `Pending` → 202 `CASH_POSITION_REQUEST_IN_PROGRESS` (bağlı niyetin o anki
    durumuyla). İki durumda da **başka hiçbir adım çalışmaz** — uzak çağrı yok, kurtarma yok. → (4) açık niyet kontrolü →
    (5) durum ve `Version` CAS (`If-Match`) → (6) LE uygunluğu, iş kuralları ve SoD → (7) tx1 (iş verisi + makbuz + denetim,
    tek işlem).
  - **Kurtarma yalnız `ReconcileCashPositionWorkflowCommand` ve `ReconcileCashPositionEvidenceExportCommand` ile**
    yapılır (her biri kendi `Idempotency-Key`'i ve makbuzuyla); aynı anahtarla tekrar asla kurtarma tetiklemez.
  - **Çökme:** tx1 sonrası çökmede makbuz `Pending` kalır; aynı anahtarla tekrar 202 `CASH_POSITION_REQUEST_IN_PROGRESS`
    alır; Reconcile niyeti kapattığında bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır. tx1 commit olmadıysa
    hiçbir şey yazılmamıştır; aynı anahtarla tekrar baştan çalışır.
- **Komut sınıfları:**
  - *Tek aşamalı (tx1'de `Completed`, uzak yan etki yok):* Create, Update, Delete, BulkDelete, Reconcile (iş),
    CreateEvidencePack, Publish, eşik politikası Create/Update/Delete/BulkDelete. Kurtarma komutları da tek yerel
    işlemdir: uzak taraf önce **okunur** (`instances/by-objects`, görev geçmişi, `links/query`); okuma örnek / geçiş
    göstermezse yalnız MOD-0023 başlatma / geçişi, 5a'daki denetimler geçerse aynı uzak anahtarla yeniden yazılır
    (MOD-0031 dışa aktarma yeniden çağrılmaz); sonuç tek işlemde yazılır.
  - *İki aşamalı (tx1 `Pending` → uzak çağrı → tx2):* Submit, Approve, Reject, ExportEvidence.
- **Mongo işlem sonucu belirsizliği (kaynak kusuru düzeltilir):** kaynak yayım yolu hata 112'yi `UnknownTransactionCommitResult`
  etiketini ayırmadan "sürüm çakışması" sayıyor (`MongoCashPositionRepository.cs:663@da4abb20`; mutabakat `:291` ve onay
  `:459` ayırıyor — paket §H, §N.11). Recovery'de **her işlemde** aynı kural:
  - `UnknownTransactionCommitResult` etiketli hata **çakışma değildir**: commit sürücü kuralına göre yeniden denenir;
    hâlâ belirsizse 503 `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN` döner. Makbuz iş verisiyle aynı işlemde olduğu için istemci
    **aynı anahtarla** tekrarlar: işlem commit olmuşsa kayıtlı yanıt, olmamışsa baştan çalışma (kurtarma değil, makbuz
    kuralı).
  - Etiketsiz 112 (`WriteConflict`) ve `Version` CAS uyuşmazlığı → 409 `CASH_POSITION_VERSION_CONFLICT`.
  - Yayım yuvası tekil indeks ihlali (11000, dört alanlı anahtar) → 409 `CASH_POSITION_PUBLICATION_SLOT_CONFLICT`
    (kaynak ek 7 davranışı, paket §A.1 / §B).
  - Başka her altyapı hatası → 503 (kaynaktaki varsayılan 400 taşınmaz, paket §E).
- **Eşzamanlılık:** Update, Reconcile, kanıt paketi, Submit, kararlar ve Publish `If-Match` (Version) + durum CAS; bayat
  istek 409.
- **Yerel kayıt önce, uzak çağrı sonra (domain-config; TP-001 T16).** Kaynakta iş akışı başlatma yerel kayıttan önce
  (`CashPositionApprovalService.cs:68` → `:113@da4abb20`) ve kanıt dışa aktarımı yayım kaydından önce
  (`CashPositionPublishService.cs:173` → `:226@da4abb20`) çağrılıyor; uzak etki kalıyor, niyet yok (paket §H, §L.6) —
  **taşınmaz**:
  1. *Submit:* ön koşullar (Status `Reconciled`, ReconciliationStatus `Completed`/`CompletedWithExceptions`, açık
     engelleyici istisna yok, geçerli kanıt paketi `SnapshotRevision = ContentRevision`, `ApprovalStatus ∈ {None,
     Rejected, Cancelled}`) → tx1: `ApprovalStatus = Submitted`, `SubmissionSequence + 1`, `SubmittedBy`,
     `CashPositionApprovalStartIntent(Pending)`, makbuz `Pending`, deneme olayı → MOD-0023 başlatma, niyetteki uzak
     anahtarla → tx2: kesin başarı → `Started`, `PendingApproval` + `WorkflowInstanceId`; kesin ret (400/403/404/409 —
     MOD-0023'ün "başlatma tamamlanmadı" 409'u hariç) → `Refused`, `PreviousApprovalStatus`; **belirsiz** (zaman aşımı,
     bağlantı kopması, 5xx, boş/okunamayan 2xx) → `Unknown`, `Submitted` kalır, 202 `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN`.
     Belirsiz sonuç ret olarak kaydedilmez.
  2. *Açık niyet varken* (`Pending`/`Unknown`): aynı anahtarla tekrar → makbuz kuralı; **farklı** anahtarla Submit → 409
     `CASH_POSITION_WORKFLOW_START_PENDING`, uzak çağrı yok. Anlık görüntü başına en fazla bir açık başlatma niyeti
     (tekil kısmi indeks).
  3. *Kararlar (Approve, Reject):* `ApprovalStatus = PendingApproval` → SoD (R1–R5) → tx1 `CashPositionApprovalDecision(Requested)`
     + makbuz `Pending` + deneme olayı (tekil kısmi indeks + `Version` CAS ile atomik) → MOD-0023 görev geçişi, uzak karar
     anahtarıyla → tx2 `Recorded` + `ApprovalStatus` + sonuç olayı. Belirsiz → `Unknown`, 202
     `CASH_POSITION_DECISION_OUTCOME_UNKNOWN`. Açık karar niyeti varken başka anahtardan ya da başka aktörden gelen her
     karar **uzak sisteme gitmeden** 409 `CASH_POSITION_DECISION_PENDING`. v1 şablonu tek adımlıdır (O-20);
     `WorkflowIsFinalStep = false` dönen onay `Recorded` olur ama `ApprovalStatus = PendingApproval` kalır.
  4. *ExportEvidence:* ön koşul `Status = Published`, kanıt paketi `Locked`, pakette açık dışa aktarma niyeti yok → tx1
     `CashPositionEvidenceExportIntent(Pending)` (kalemler + uzak anahtarlar) + makbuz `Pending` + deneme olayı → her kalem
     için MOD-0031 `POST links` (uzak anahtarla) → tx2 kalem sonuçları + niyet durumu + sonuç olayı. Belirsiz → 202
     `CASH_POSITION_EVIDENCE_EXPORT_OUTCOME_UNKNOWN`; açık niyet varken farklı anahtar → 409
     `CASH_POSITION_EVIDENCE_EXPORT_PENDING`. Yayım bu sonuçtan bağımsızdır; kaynaktaki "dışa aktarma zorunluysa yayımı
     engelle" modu (`CashPositionPublishService.cs:182-202@da4abb20`) taşınmaz (O-17).
  5. *İki aşamalı denetim ve çökme kurtarma:* tx1 **deneme** olayı, tx2 **sonuç** olayı; ikisi de kendi işleminde iş
     verisiyle (`RequireEnlistment = true`). Kesin sonuç tx2 commit olunca vardır. Uzak başarı + tx2 başarısız ya da çökme
     → niyet açık kalır; ilgili Reconcile komutu uzak durumu **okur** (yalnız niyetin kendi eşleme alanlarıyla — 5a, gap
     6/P1-b), uzak çağrıyı tekrarlamadan tx2'yi yazar, sonuç
     olayını üretir, bağlı makbuz yalnız `Pending` ise `Completed` olur. İkinci uzak örnek / geçiş / bağlantı oluşmaz.
  5a. *Reconcile önce okur; uzak yazmayı yalnız anahtarla idempotent olduğu ölçülmüş yerde yeniden yapar (gap 4/G2,
     gap 5/G2; Submit, Approve, Reject ve ExportEvidence; kural MOD-0126 pack Runtime Constraints 3a ile aynı):*
     - **Önce okuma:** başlatmada `instances/by-objects` (anlık görüntü kimliği) + niyetin deterministik uzak anahtarı;
       kararda görev geçmişi + `RemoteIdempotencyKey`; dışa aktarmada `links/query` + her kalemin `RemoteKey`'i.
     - **Uzakta niyetin kendisine ait örnek / geçiş / bağlantı var** (eşleme aşağıda, gap 6/P1-b) → kesin durum kaydedilir
       (tx2 + bağlı `Pending` makbuz `Completed` + sonuç olayı); şirket kuralından **muaf** (D15/2).
     - **"Kesin boş" okumadan çıkarılmaz (gap 5/G2):** ne MOD-0023 ne MOD-0031 sözleşmesinde "bulunamadı / boş okuma son
       durumdur" güvencesi vardır (ilk çağrı hâlâ yolda olabilir); okumaya dayalı "kesin boş → yeniden başlat" dalı
       yoktur. Okuma belirsiz ya da "bulunamadı" ise (zaman aşımı, bağlantı kopması, 5xx, okunamayan gövde, 404 / boş
       sonuç) niyet **asla `Cancelled` yapılmaz**, `Unknown` kalır (`Pending` idiyse `Unknown`'a geçer); anlık görüntü ve
       makbuz değişmez. Zaman aşımı / 5xx / okunamayan gövdede başka adım yoktur; 404 / boş sonuçta tek ek adım — yalnız
       Submit ve kararlarda — aşağıdaki anahtarlı yeniden yazmadır.
     - **Yeniden çağrı yalnız MOD-0023 başlatma ve görev geçişlerinde (Submit, Approve, Reject)** — uzak yazmanın kendisi
       anahtarla idempotent (ölçüldü, Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c):
       `ux_workflow_instances_tenant_idempotency_key_active`
       (TenantId + IdempotencyKey, kısmi `IsDeleted = false`) ve `ux_workflow_transition_logs_task_action_idempotency_active`
       (TenantId + ApprovalTaskId + Action + IdempotencyKey) tekil indeksleri
       (`PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131`, `:211-225`); başlatma var olan örneği döndürür
       (`StartWorkflowInstanceHandler.cs:76-85`), geçişler anahtarı zorunlu tutar (`ApproveWorkflowTaskValidator.cs:13`) ve
       `GetByTaskActionIdempotencyKeyAsync` ile yeniden oynatır (`RejectWorkflowTaskHandler.cs:79-80`,
       `WorkflowTerminalTransitionWriter.cs:402-407`). Yeniden yazmadan önce (i) şirket uygunluğu (D10/1, D14/4), (ii) SoD
       (R1–R5, R7 niyetteki aktörle), (iii) izin (`submit` / `approve` / `reject`, o anki token) ve (iv) aktör —
       Reconcile'ı çağıran asıl komutun aktörü (`SubmittedBy` / `DecidedBy`) değilse uzak yazma çağrısı yok (409
       `CASH_POSITION_RECONCILE_RESTART_ACTOR_MISMATCH`). Hepsi geçerse aynı uzak yazma **aynı deterministik uzak anahtarla
       bir kez** yapılır; en fazla bir örnek / geçişi MOD-0023'ün tekil indeksi garanti eder. Başarı (yeni ya da yeniden
       oynatılmış) → niyet sonuçla kapanır (tx2 eşlemesi); kesin ret (tx2 eşlemesi; yinelenen anahtar / tekil indeks
       çakışması ret sayılmaz) → `Refused`; kesin ret olmayan her başarısızlık (yinelenen anahtar yarışı yanıtı, 5xx, zaman
       aşımı, okunamayan gövde) → `Unknown` kalır.
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
       kalır (202 `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN` / `CASH_POSITION_DECISION_OUTCOME_UNKNOWN`), anlık
       görüntü ve makbuz değişmez. 404 / boş okumadaki anahtarlı yeniden yazma, MOD-0023 saklama + silmeme politikası (en
       az niyet ömrü boyunca; sahibi platform-shared-services) ve silinmiş durumu ayırt eden okuma kapanana kadar
       **kapalıdır** (kapanış tablosu, Ready-for-dev engeli). Dışa aktarma zaten yeniden çağrılmaz; silinmiş bir MOD-0031
       bağlantısı okumada görünmezse kalem `Unknown` kalır.
     - **Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b):** `instances/by-objects` aynı anlık görüntünün önceki
       gönderimlerinin örneklerini de döndürür (`GetWorkflowInstancesByObjectsHandler.cs:50-69`). Okuma sonucu niyete
       yalnız **hepsi** eşleşirse bağlanır: kiracı; anlık görüntü kimliği; başlatmada `SubmissionSequence`, kararda karar
       kimliği (`CashPositionApprovalDecision.Id`); deterministik uzak anahtar (`mod0124:{snapshotId:N}:start:{SubmissionSequence}`
       / `RemoteIdempotencyKey`); geçişte ek olarak görev kimliği (`WorkflowTaskId`) + eylem (approve / reject); dışa
       aktarmada kalemin `RemoteKey`'i + belge kimliği / sürümü + kanıt paketi kimliği. Eski bir denemenin sonucu (önceki
       `SubmissionSequence`'in örneği, başka kararın geçişi, başka dışa aktarma niyetinin bağlantısı) yeni niyeti **asla**
       kapatmaz; yalnız anlık görüntü kimliğiyle ya da "en yeni örnek" sırasıyla eşleme yasaktır. Ölçüldü (Recovery
       @4e01e986269065cba6f962e0c13b9b2ae4519f6c): `WorkflowInstanceStatusDto` (`WorkflowModels.cs:200-207`) ve `WorkflowInstanceHistoryEntryDto`
       (`:451-465`) uzak anahtarı taşımaz, geçmiş satırı görev kimliğini de taşımaz; alan yanıtta yoksa okuma niyeti
       kapatamaz → `Unknown`. Kapanış yolu MOD-0126 pack 3a ile aynı (MOD-0023'e eklemeli okuma alanı, D9; ya da P1-a
       koşulları kapalıysa aynı anahtarla yeniden yazmanın yanıtı).
     - **D21 bağı — yeniden yazma kapısının yeni hâli (sahip kararı D21, MOD-0023 pack §17 (TRM-15)):** yukarıdaki kapının iki
       koşulundan saklama politikası **KARAR D21/1** ile verildi (hiç silinmez, TTL yok) ve MOD-0023 AC-W1 (mimari test) geçince
       kapanır; silinmişi ayıran okuma koşulu **D21/2 ile kaldırıldı** — politika + AC-W1 onun yerine geçer. §17
       onaylanıp AC-W1 geçince 404 / boş okumada aynı deterministik anahtarla yeniden yazma açılır; yalnız asıl
       aktör (**KARAR D21/5**) ve şirket / SoD / izin yeniden kontrolüyle. Silinmiş kayıt yine de görülürse yeniden
       yazma yok, niyet `Unknown`. Eşleme alanları **KARAR D21/3** (§17.3/3) kodda olana kadar okuma niyeti kapatmaz.
     - **Aynı anahtarlı yarış yanıtı (KARAR D21/4, §17.3/4; DL-3 başı kanıt maddesi):** politika — eşzamanlı iki
       istekte son yanıtlar aynı kayıttır ve tek satır oluşur; ara 503 "aynı anahtarla tekrar" olabilir (→ `Unknown`,
       tekrar), 409 yalnız örnek Active'den çıktıysa. Kod okuması (@ef4f3a8bc) bu deseni gösteriyor
       (`StartWorkflowInstanceHandler.cs:300-330, 700-716`, `WorkflowTerminalTransitionWriter.cs:357-…`) ama eşzamanlı
       aynı anahtar testi **ölçülmedi**; MOD-0023 AC-W5/W6 geçene kadar yinelenen anahtar hatası **`Unknown`** sayılır,
       **asla `Refused`** değildir. `Refused` yalnız tx2 tablosundaki kesin iş reddi kodlarıdır (**KARAR D21/7**).
     - **Dışa aktarma (MOD-0031) yeniden çağrılmaz:** aynı `RemoteKey` ile `POST links` tekrarının tek bağlantı bıraktığı
       (dedupe) ölçülmedi. `ReconcileCashPositionEvidenceExportCommand` yalnız `links/query` ile **okur**; okuma bir kalemin
       bağlantısını kesin gösterirse kalem sonucu kaydedilir (`SnapshotContentHash = PublishedContentHash` denetimiyle);
       göstermezse kalem ve niyet `Unknown` kalır, MOD-0031'e yazma çağrısı yapılmaz. Yeniden çağrıyı açmak için
       MOD-0031 / dışa aktarma dedupe ölçümü önkoşuldur (Dependencies kapanış tablosu, Ready-for-dev, DL-4 önkoşulu).
     - **`Cancelled` yalnız kesin uzak iptal / kapanış kaydıyla:** başlatma / kararda o örnek için MOD-0023 iptal geçiş
       kaydı (ör. `CloseAbandonedWorkflowStartHandler.cs`, anahtar `workflow-start-recovery-close:{instanceId:N}`, `:230`);
       dışa aktarmada MOD-0031'de o bağlantının kesin iptal kaydı (ölçülmedi; ölçülene kadar `Cancelled` yok).
     - **Uygun değilse:** uzak yazma/başlatma çağrısı yok; niyet `Unknown` kalır, `LastOutcomeCode` sebep kodunu taşır
       (`CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`, `CASH_POSITION_APPROVAL_SEPARATION_DENIED`, `CASH_POSITION_PERMISSION_DENIED`,
       `CASH_POSITION_RECONCILE_RESTART_ACTOR_MISMATCH`); anlık görüntü ve bağlı `Pending` makbuz değişmez; sonuç olayı
       yazılmaz; Reconcile'ın kendi makbuzu sebep koduyla `Completed` olur. Koşul düzelince yeni Reconcile tekrar dener;
       kalıcı kapatma Follow-up.
  6. HTTP istemcilerinde **açık zaman aşımı**: `FinanceTreasury:Workflow:TimeoutSeconds` (MOD-0126 ile ortak) ve
     `FinanceTreasury:Evidence:TimeoutSeconds`; başlangıçta yoksa servis açılmaz (öneri 15 sn; kaynakta istemci başına 10 sn,
     paket §K).
- **Publish tek yerel işlemdir (uzak çağrı yok):** ön koşullar kaynak korumalarıyla aynı (`CashPositionPublishService.cs:52-140@da4abb20`,
  paket §E): yayımlanmamış, `Version` eşit, `Reconciled` + `Completed`/`CompletedWithExceptions`, açık engelleyici istisna
  yok, geçerli kanıt paketi `SnapshotRevision = ContentRevision` ve kilitli değil, geçerli karar `Recorded` + onay +
  `ApprovedSnapshotRevision = ContentRevision`, açık niyet yok, karar zinciri R1–R3'e uyar (R6). Aynı işlemde: aynı
  yuvadaki önceki yayım `Superseded`, kanıt paketi `Locked`, anlık görüntü `Published` + `IsCurrentPublished`, makbuz
  `Completed`, tek `…published` olayı (`SupersededSnapshotId` alanıyla); `PublishedContentHash` bu işlemde hesaplanıp
  yazılır. Kaynaktaki outbox satırı (`mod0124.snapshot.published.v1`) yazılmaz (O-14).
- **Banka verisi ve banka akışı yok:** hiçbir bağdaştırıcı banka ile konuşmaz; MOD-0269 portu tanımlanmaz (v1'de
  tüketici yok).
- `GlobalEntity` kullanılmaz; bütün kayıtlar kiracıya aittir.
- Mongo GUID V3 / subtype 4; `decimal` → `Decimal128`.

## Layout & Shell Contract

- `shell: tenant` → `Views/FinanceTreasury/CashPositions/` ve `Views/FinanceTreasury/CashPositionThresholdPolicies/`
  altındaki **tüm** `.cshtml` dosyalarında `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml`
  varsayılanı değiştirilmez. Kaynak ekrandaki `Layout = "_LayoutBackbone"`
  (`frontend/Diten.Web/Views/ERP/Treasury/CashPositioning/Index.cshtml:3@da4abb20`, paket §I) taşınmaz.
- View klasörleri: `Views/FinanceTreasury/CashPositions/` · `Views/FinanceTreasury/CashPositionThresholdPolicies/`.
  Frontend rotaları: `/FinanceTreasury/CashPositions` · `/FinanceTreasury/CashPositionThresholdPolicies`.
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `finance-treasury.cash-positions.read` (ya da
  `finance-treasury.cash-position-threshold-policies.read`) yoksa kabuk içinde `_AccessDenied` partial'ı; başlık, kart,
  boş tablo, buton çizilmez; yönlendirme yok; 401 tam sayfa. Kaynakta menü girişi erişim politikasıyla sarılı değil
  (`_LayoutBackbone.cshtml:247-252@da4abb20`, paket §G) — taşınmaz.

## Backend File Convention

Golden Reference birebir (okundu: `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
ve `…/GoldenReferenceSlim/`; ikisi de `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`,
`Validators/`, tek `{Module}Models.cs`).

```text
Features/CashPosition/
  Commands/        CreateCashPositionCommand.cs, UpdateCashPositionCommand.cs, DeleteCashPositionCommand.cs,
                   BulkDeleteCashPositionCommand.cs, ReconcileCashPositionCommand.cs,
                   CreateCashPositionEvidencePackCommand.cs, SubmitCashPositionCommand.cs,
                   ApproveCashPositionCommand.cs, RejectCashPositionCommand.cs,
                   ReconcileCashPositionWorkflowCommand.cs, PublishCashPositionCommand.cs,
                   ExportCashPositionEvidenceCommand.cs, ReconcileCashPositionEvidenceExportCommand.cs
  Queries/         GetCashPositionListQuery.cs, GetCashPositionByIdQuery.cs, GetCashPositionTraceQuery.cs,
                   GetCashPositionExceptionListQuery.cs, GetCurrentCashPositionQuery.cs, CashPositionListSort.cs
  Handlers/CommandHandlers/   {Verb}CashPosition…Handler.cs (her komut için; suffix YOK)
  Handlers/QueryHandlers/     Get{…}Handler.cs
  Validators/      CreateCashPositionValidator.cs, UpdateCashPositionValidator.cs, GetCashPositionListValidator.cs,
                   CashPositionDecisionValidator.cs, CreateCashPositionEvidencePackValidator.cs
  CashPositionModels.cs       (tüm DTO'lar tek dosyada)

Features/CashPositionThresholdPolicy/   (Slim seti)
  Commands/        Create/Update/Delete/BulkDeleteCashPositionThresholdPolicyCommand.cs
  Queries/         GetCashPositionThresholdPolicyListQuery.cs, GetCashPositionThresholdPolicyByIdQuery.cs
  Handlers/CommandHandlers/ · Handlers/QueryHandlers/
  Validators/      CreateCashPositionThresholdPolicyValidator.cs, UpdateCashPositionThresholdPolicyValidator.cs
  CashPositionThresholdPolicyModels.cs
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` `{Verb}{Module}Handler` — **Command/Query
  suffix YOK** (kaynak `ImportCashPositionCommandHandler` komutla aynı dosyada, `ImportCashPositionCommand.cs:8-27@da4abb20`;
  `EvidenceAndPublishCommands.cs` üç komutu tek dosyada topluyor — taşınmaz). Tek dosyada tek public tip.
- Controller `CustomBaseController`'dan türer, `Response<T>` döner; her aksiyonda `[HasPermission]`.
- İş kuralı servis sınıflarında değil handler + domain'de (`handler-design`); kaynaktaki `CashPositionPersistenceService`,
  `CashPositionReconciliationService`, `CashPositionPublishService` gibi servisler bölünerek alınır. Mutabakat kuralları
  (tazelik, fark, para birimi) saf bir domain hesaplayıcısıdır (`CashPositionReconciler`) — birim testlenir.
- Her yazma komutu `ICentralAuditDeclaration` taşır (`CentralAuditEntityId` açık yazılır); yerel kayıt yazmadan uzak
  tarafa dokunan komut yoktur (iki aşamalılar önce yerel niyet yazar), bu yüzden `ICentralAuditOutsideTransaction`
  kullanılmaz.

## Frontend File Contract

**Ekran 1 — `CashPositions`, Golden Reference Compact** (okundu: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/`,
`wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/{index.js,index.l10n.js,form.js}`):

```text
Views/FinanceTreasury/CashPositions/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml             (sayfa kabuğu + _Form)
├── Edit.cshtml               (sayfa kabuğu + _Form; yalnız yayımlanmamış, açık niyetsiz)
├── Details.cshtml            (sekme: Bakiyeler · Mutabakat ve istisnalar · Kanıt · Onay · Yayım ve dışa aktarma · İz)
├── _Form.cshtml              (başlık alanları + bakiye satırları ızgarası)
├── _Filter.cshtml            (değer tarihi aralığı, para birimi, durum, mutabakat durumu, onay durumu)
├── _DataTable.cshtml         (data-dt-standard="v2", data-dt-data-mode="server", iskelet yükleyici)
├── _IndexL10n.cshtml
└── CashPositionsIndex.cs     (marker)
wwwroot/assets/js/FinanceTreasury/CashPositions/   index.js · index.l10n.js · form.js · details.js
Resources/Views/FinanceTreasury/CashPositions/     CashPositionsIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- DataTable sütunları: Kod, Değer tarihi, Para birimi, Toplam defter bakiyesi, Kullanılabilir, Kısıtlı (para birimine göre
  biçim), Durum, Mutabakat, Onay durumu, Açık istisna, Tazelik (sağlık), Oluşturan, İşlemler. Kaynak sütunlarındaki
  `RowVersion` ve maskeli hesap (paket §I) taşınmaz.
- Durum, mutabakat ve onay rozetleri v1 enum değerlerinin tamamını gösterir; enum'da olmayan değer yok.
- Karar düğmeleri (Onaya gönder / Onayla / Reddet / Yayımla / Kanıtı dışa aktar / Uzlaştır) yalnız Details'ta ve yalnız
  sunucunun verdiği `allowedActions` listesine göre çizilir; SoD nedeniyle karar veremeyen kullanıcıya düğme gösterilmez,
  yerine yerelleştirilmiş açıklama çizilir — sunucu kontrolünün yerine geçmez.
- Kaynaktaki ham JSON içe aktarma penceresi (`Index.cshtml:218-233@da4abb20`) ve bağımlılık çipleri (`:20-47`) taşınmaz.

**Ekran 2 — `CashPositionThresholdPolicies`, Golden Reference Slim** (okundu: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`,
`wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/{index.js,index.l10n.js}`):

```text
Views/FinanceTreasury/CashPositionThresholdPolicies/
├── Index.cshtml                    (Layout = "_LayoutTenantShell")
├── _Filter.cshtml                  (para birimi, geçerlilik tarihi)
├── _DataTable.cshtml               (data-dt-standard="v2", data-dt-data-mode="client" + skeleton)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml     (Slim-özel)
├── _DetailsQuickView.cshtml        (Slim-özel)
└── CashPositionThresholdPoliciesIndex.cs
wwwroot/assets/js/FinanceTreasury/CashPositionThresholdPolicies/  index.js · index.l10n.js
Resources/Views/FinanceTreasury/CashPositionThresholdPolicies/    CashPositionThresholdPoliciesIndex.{7 dil}.resx
```

- İstemci modunda JS'te sabit `pageSize=` yok; servis `total` döner, ekran `total > gelen` durumunu gösterir.

**Ortak:**
- API profili: same-origin MVC proxy (`/FinanceTreasury/CashPositions/api/...`, `/FinanceTreasury/CashPositionThresholdPolicies/api/...`);
  proxy `AuthTokenCookies.GetAccessToken` ile token'ı, oturumun çözümlenmiş `X-Tenant-Id` / `X-Legal-Entity-Id`
  başlıklarını ve istemcinin `Idempotency-Key`'ini iletir; çözümlenemezse istek oluşturulmaz (multi-tenancy §6). Tarayıcı
  JS `access_token` / `document.cookie` okumaz. Kaynak ekran doğrudan `/api/v1/mod0124`'e gidiyordu (`index.js:7, :82@da4abb20`)
  — taşınmaz.
- Uyarı/onay diyalogları SweetAlert2 premium standardı (MOD-0013); hardcoded metin yok (kaynakta 0 `Localizer`, paket §I).
- Para birimi seçimi `GET /api/lookups/reference/currencies`'ten; sabit liste yok. Banka referansı alanı MOD-0289 arama
  ucu gelene kadar opak referans girişi (O-15).

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| X-Legal-Entity-Id | Evet | Guid; kiracıya ait; yazmada Active + Approved (iki kurtarma komutunun kayıt yolu hariç — D14/4, D15/2); okumada okuma kapsamında (kendi şirket yetkisi ya da `read-tenant-wide`, D14/1); **yazmada** hedef kaydın şirketine eşit ve kullanıcının **kendi** şirket yetkisinde (yazma kapsamı — `read-tenant-wide` yazma yetkisi vermez, gap 4/G1); değilse 404 | — | `ILegalEntityEligibilityPort`, `IFinanceTreasuryDataScopeSource` (okuma / yazma kapsamı) |
| Gövde/sorguda tenantId, legalEntityId | — | bulunursa 400 `CASH_POSITION_SCOPE_INPUT_FORBIDDEN` | — | middleware |
| Idempotency-Key | **Evet** (her yazma komutu, D12/2) | 1..128 yazdırılabilir ASCII; yoksa 400 | Unique (6 alanlı kapsam) | makbuz araması |
| ValueDate | Evet | `DateOnly`, default değil; gelecek tarih kuralı TBD (O-10) | — | — |
| PositionCurrencyCode | Evet | `^[A-Z]{3}$` (upper normalize), MOD-0048 listesinde | — | `ICurrencyReferencePort` |
| Notes | Hayır | trim, ≤ 2000; 8+ ardışık rakam yok | — | — |
| Lines | Evet | 1..200 satır (öneri) | — | — |
| Line.BankAccountRef | Evet | Guid ≠ Empty | Unique (SnapshotId, BankAccountRef, CashCategoryType) | — |
| Line.CashCategoryType | Evet | tanımlı enum | aynı | — |
| Line.CurrencyCode | — | gönderilirse pozisyon para birimine eşit, değilse 400 `CASH_POSITION_MIXED_CURRENCY` | — | — |
| Line.LedgerBalance / AvailableBalance | Evet | decimal; işaretli (O-18); küsurat ≤ para birimi hanesi | — | `ICurrencyReferencePort` (hane) |
| Line.CollectedBalance / BlockedBalance / RestrictedAmount | Evet | decimal ≥ 0 (O-18); küsurat ≤ hane | — | aynı |
| Line.ExpectedBookBalance | Hayır | decimal; küsurat ≤ hane | — | aynı |
| Line.BalanceAsOfUtc | Evet | UTC (`Offset = 0`), default değil, ≤ şimdi (UTC) | — | — |
| Line.SourceReference | Evet | trim, 1..64; `\d{8,}` içeremez → 400 `CASH_POSITION_SENSITIVE_TEXT_REJECTED` | — | — |
| SnapshotCode | — | istekte bulunursa 400 (OTOMATİK) | Unique (TenantId, SnapshotCode) | `IRecordCodePort` |
| Toplamlar, fark, durumlar, WorkflowInstanceId | — | istekte bulunursa 400 (sunucu alanı; kaynak `WorkflowInstanceId` girdisini zaten reddediyor) | — | — |
| Kanıt EvidenceRefs | Evet | 1..50 kalem (öneri); `Kind` tanımlı; `DocumentId` Guid ≠ Empty; `DocumentVersionId` Guid? | — | — |
| Karar ReasonCode | Reject için Evet | trim, 1..64 | — | — |
| Karar DecisionNote | Hayır | trim, ≤ 1000; 8+ ardışık rakam yok | — | — |
| Karar WorkflowTaskId | Evet | Guid ≠ Empty, anlık görüntünün örneğine ait | — | MOD-0023 |
| Politika PolicyName | Evet | trim, 1..256 | — | — |
| Politika CurrencyCode | Evet | `^[A-Z]{3}$`, MOD-0048 | Unique slot (TenantId, LE, CurrencyCode) | `ICurrencyReferencePort` |
| Politika Warning/CriticalAbsoluteAmount | Evet | ≥ 0; Critical ≥ Warning; küsurat ≤ hane | — | — |
| Politika Warning/BlockingFreshnessHours | Evet | 0..720; Blocking ≥ Warning | — | — |
| Politika EffectiveFrom / EffectiveTo | Evet / Hayır | `EffectiveTo ≥ EffectiveFrom`; aynı LE + para biriminde çakışma yok → 409 | slot CAS | — |

**Durum kuralları:** Update yalnız `Status ∈ {Draft, Reconciled}`, `ApprovalStatus ∈ {None, Rejected, Cancelled,
Approved}` ve açık niyet yokken; güncelleme anlık görüntüyü `Draft`'a döndürür, `ReconciliationStatus = NotStarted`,
`ContentRevision + 1`, kanıt paketi ve onayı geçersiz kılar; istisna kayıtlarına dokunmaz — bir sonraki mutabakat onları
yeniden değerlendirir (kalan koşul açık kalır, kalkan `Resolved` olur). Reconcile yalnız `Status ∈ {Draft, Reconciled}`, en az bir satır, açık niyet yok, `ApprovalStatus ∉
{Submitted, PendingApproval}`; `Approved` iken Reconcile de onayı düşürür. Delete / BulkDelete yalnız `SubmissionSequence = 0`
ve `Status ∈ {Draft, Reconciled}`.

**Yayım sonrası değişmezlik (gap 4/G3; kaynak `:74-77` korumasının Recovery tanımı):** `Published` / `Superseded` anlık
görüntünün **içeriği** — başlık (değer tarihi, pozisyon para birimi), satırlar ve bakiyeler, toplamlar ve fark,
`ContentRevision`, mutabakat sonucu ve sabitlenen eşikler, kilitli kanıt paketi, geçerli karar ve `PublishedContentHash` —
**değişmez**; bunlara dokunan her komut (Update, Delete, BulkDelete, Reconcile (iş), kanıt paketi, Submit, Approve, Reject,
yeniden Publish) 409 `CASH_POSITION_PUBLISHED_IMMUTABLE`. İçerik dışındaki iki yazım **tanımlı istisnadır**: (1) aynı
yuvadaki yeni Publish, önceki yayımın yalnız yaşam döngüsü alanlarını (`Status → Superseded`, `IsCurrentPublished = false`,
`SupersededBySnapshotId`, `SupersededAt`) aynı işlemde yazar; (2) **ExportEvidence anlık görüntüyü hiç değiştirmez** —
yalnız ayrı kayıtlar yazar: `CashPositionEvidenceExportIntent` (anlık görüntü kimliği + `SnapshotContentHash` ile
referans), makbuz ve kanıt bağlantı sonuçları (`Items[].EvidenceLinkId`); anlık görüntünün `Version`'ı da artmaz.
**Kurtarma istisnası:** `ReconcileCashPositionEvidenceExportCommand` yalnız dışa aktarma niyeti, makbuz ve olay kayıtlarını
yazar; anlık görüntüye ve kanıt paketine yazmaz.

**Onay ayrılığı (SoD) — her karar komutunda, MOD-0023 çağrısından ÖNCE; muafiyet YOK, ikinci kademe YOK (para yetkisi
olmadığı için):**

| # | Kural | Nerede | Kaynak karşılığı |
|---|---|---|---|
| R1 | Anlık görüntüyü oluşturan (`CreatedBy`) onaylayamaz ve reddedemez | karar + Publish | `CashPositionApprovalService.cs:56-58@da4abb20` (karar), `CashPositionPublishService.cs:139-140` (yayım) |
| R2 | Gönderen (`SubmittedBy` = MOD-0023 başlatanı) onaylayamaz ve reddedemez | karar + MOD-0023 | MOD-0023 yalnız **onayı** engelliyor (`WorkflowTaskTransitionSupport.cs:134-141`); Recovery'de ret de yerelde engellenir |
| R3 | Son içerik değiştiren (`ContentUpdatedBy` — son Update ya da Reconcile) onaylayamaz ve reddedemez | karar + Publish | yeni — kaynakta yalnız oluşturan kontrol ediliyor |
| R4 | Aktör karşılaştırması token `sub`'ının **Guid** değeri üzerindendir; `sub` Guid değilse karar reddedilir (`FailedActorInvalid`, 403 `CASH_POSITION_APPROVAL_ACTOR_INVALID`) | her yer | kaynak Guid değilse sıralı dize karşılaştırmasına düşüyor (`CashPositionApprovalService.cs:142-145@da4abb20`) — **taşınmaz** |
| R5 | İzin karar anında yeniden değerlendirilir (o anki token, `[HasPermission]`) | karar | — |
| R6 | Publish karar zincirini yeniden doğrular: geçerli `Recorded` onayın aktörü R1–R3'e uyar, `ApprovedSnapshotRevision = ContentRevision`, açık karar niyeti yok (varsa 409) | Publish | kaynak yayım korumaları `:115-140` |
| R7 | Karar veren, MOD-0023 görevinin atanmış aktörü olmalıdır (MOD-0023 `WorkflowActorDenied`, 403) | MOD-0023 | `WorkflowTaskTransitionSupport.cs:120-126` |

Muafiyet ve ikinci kademe eşik MOD-0126'ya özgüdür (D11/1, D12/1, D13) ve burada **uygulanmaz**. Yayımlayana ek SoD
yoktur (O-21).

## Failure Path to Verify

- **Kendi anlık görüntüsünü onaylama / reddetme (oluşturan)** → 403 `CASH_POSITION_APPROVAL_SEPARATION_DENIED`
  (`FailedCreator`); MOD-0023 çağrılmaz; durum değişmez.
- **Gönderenin kararı** → 403 (`FailedSubmitter`), ret dahil; MOD-0023 çağrılmaz.
- **Son içerik değiştirenin kararı** → 403 (`FailedContentUpdater`).
- **Guid olmayan `sub`** → 403 `CASH_POSITION_APPROVAL_ACTOR_INVALID`.
- **Açık karar niyeti varken başka anahtar/aktörden karar** → 409 `CASH_POSITION_DECISION_PENDING`, MOD-0023 çağrılmaz.
- **Idempotency-Key yok** → 400 `CASH_POSITION_IDEMPOTENCY_KEY_REQUIRED`, kayıt yok.
- **Idempotent tekrar** — aynı anahtar + aynı yük + `Completed`: kayıtlı yanıt, ikinci kayıt / olay / uzak çağrı yok;
  `Pending`: 202 `CASH_POSITION_REQUEST_IN_PROGRESS`, kurtarma yok; aynı anahtar + farklı yük: 409
  `CASH_POSITION_IDEMPOTENCY_CONFLICT`.
- **Uzak başarı sonrası tx2 hatası / çökme** (Submit, karar, dışa aktarma) → niyet açık kalır; ilgili Reconcile komutu uzak
  çağrıyı tekrarlamadan sonucu yazar.
- **İş akışı başlatma zaman aşımı / bağlantı kopması / boş 2xx** → 202 `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN`,
  niyet `Unknown`, `ApprovalStatus = Submitted`; ikinci Submit (farklı anahtar) 409 `CASH_POSITION_WORKFLOW_START_PENDING`;
  Reconcile aynı uzak anahtarla ilerler, ikinci örnek oluşmaz.
- **İş akışı başlatma kesin reddi** (403 yetkisiz, 404 şablon yok) → 502 `CASH_POSITION_WORKFLOW_START_FAILED`, niyet
  `Refused`, `ApprovalStatus` önceki değerine döner.
- **Mongo commit sonucu belirsiz** (`UnknownTransactionCommitResult`) → çakışma kodu verilmez; 503
  `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN`; aynı anahtarla tekrar commit olduysa kayıtlı yanıtı, olmadıysa yeni çalışmayı
  verir; ikinci yayım / ikinci olay oluşmaz.
- **Aynı yuvada eşzamanlı iki yayım** → biri 200, diğeri 409 `CASH_POSITION_PUBLICATION_SLOT_CONFLICT`; yuvada tek
  `IsCurrentPublished`.
- **LE aktif değil ya da onaylı değil** → her yazma komutu (eşik politikası dahil; iki kurtarma komutu hariç — D14/4) 422
  `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`, kayıt değişmez; MOD-0220 erişilemez → 503
  `CASH_POSITION_LEGAL_ENTITY_UNAVAILABLE`; okumalar ve `current` etkilenmez.
- **Başka kiracı / başka şirket kaydı** (okuma, güncelleme, silme, karar, yayım, `current`) → 404 `CASH_POSITION_NOT_FOUND`,
  varlık sızdırılmaz.
- **Kapsam dışı şirket** (kullanıcının veri kapsamında olmayan şirket, `read-tenant-wide` yok — D14/1) → anlık görüntü ve
  politika listeleri 200 boş; detay / iz / `current` / yazma 404 `CASH_POSITION_NOT_FOUND`; veri kapsamı çözücüsü hata
  verirse aynı sonuç.
- **Yalnız `read-tenant-wide` ile görünen kayda yazma** (kullanıcının o şirkette kendi yetkisi yok — gap 4/G1) → her yazma
  komutu (Create, Update, Delete, Reconcile, kanıt, Submit, Approve, Reject, Publish, ExportEvidence, kurtarma, eşik
  politikası) 404 `CASH_POSITION_NOT_FOUND`; liste, detay ve `current` okuması 200; uzak çağrı, kayıt ve makbuz yok.
- **Uzak kayıt silinmiş / temizlenmiş** (gap 6/P1-a) → okuma silinmiş kaydı gösterirse ya da silinmemiş kayıt yokken
  niyetin ilk denemesi kayıtlıysa (okuma silinmiş durumu ayırt edemiyorken) aynı anahtarla yeniden yazma **yapılmaz**; niyet `Unknown`, 202
  `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN` / `CASH_POSITION_DECISION_OUTCOME_UNKNOWN`; anlık görüntü ve makbuz
  değişmez.
- **Aynı anlık görüntünün eski örneği / eski kararın geçişi / başka niyetin bağlantısı okunuyor** (gap 6/P1-b) → yeni
  niyete bağlanmaz; eşleme tutmazsa ya da yanıt eşleme alanını taşımıyorsa niyet `Unknown` kalır.
- **Reconcile, uzakta örnek / bağlantı bulunamadı ya da okuma belirsiz** (gap 4/G2, gap 5/G2) → niyet `Cancelled`
  yapılmaz, `Unknown` kalır; okuma "kesin boş" kararı vermez. Zaman aşımı / 5xx / okunamayan gövde → uzak
  yazma/başlatma çağrısı yok. 404 / boş okumada yalnız MOD-0023 başlatma / görev geçişi (Submit, kararlar) aynı
  deterministik anahtarla yeniden yazılabilir; önce şirket + SoD + izin + aktör denetlenir: şirket uygun değilse uzak
  yazma/başlatma çağrısı yok, niyet `Unknown` + `LastOutcomeCode = CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile
  422; aktör farklıysa 409 `CASH_POSITION_RECONCILE_RESTART_ACTOR_MISMATCH`; uygunsa aynı anahtarla tek uzak yazma —
  başarı (yeni ya da yeniden oynatılmış) niyeti kapatır, yinelenen anahtar hatası / 5xx / zaman aşımı → `Unknown` (asla
  `Refused` değil). Dışa aktarmada MOD-0031'e yazma çağrısı hiç yapılmaz; bağlantı okunamadıkça niyet `Unknown`.
- **Eksik alan** (ValueDate, PositionCurrencyCode, satır) → 400 + alan düzeyi resx iletisi; kayıt yok.
- **Karışık para birimi satırı** → 400 `CASH_POSITION_MIXED_CURRENCY`; **yinelenen satır anahtarı** → 400
  `CASH_POSITION_DUPLICATE_LINE_KEY`.
- **Hassasiyet aşımı** (ör. JPY'de küsurat) → 400; hane kaynağı yok → 503 `CASH_POSITION_CURRENCY_PRECISION_UNAVAILABLE`.
- **Kaynak referansında 8+ ardışık rakam** → 400 `CASH_POSITION_SENSITIVE_TEXT_REJECTED`; değer kayda ve loga yazılmaz.
- **Politika yok** → Reconcile 422 `CASH_POSITION_THRESHOLD_POLICY_MISSING`; kodda varsayılan eşik yok.
- **Çakışan politika aralığı / eşzamanlı iki politika yazımı** → 409 `CASH_POSITION_THRESHOLD_POLICY_OVERLAP` /
  `CASH_POSITION_VERSION_CONFLICT`; yuvada çakışma oluşmaz.
- **Açık engelleyici istisna varken Submit ya da Publish** → 409 `CASH_POSITION_PUBLISH_BLOCKED_OPEN_EXCEPTION`.
- **Kanıt paketi yok / eski revizyona bağlı** → Submit ve Publish 409 `CASH_POSITION_EVIDENCE_PACK_REQUIRED` /
  `CASH_POSITION_EVIDENCE_REVISION_CONFLICT`.
- **Onay eski revizyona bağlı** (onaydan sonra Update/Reconcile) → onay düşer; Publish 409 `CASH_POSITION_APPROVAL_REQUIRED`.
- **Eşzamanlılık** (bayat `If-Match`) → 409 `CASH_POSITION_VERSION_CONFLICT`, sessiz üzerine yazma yok.
- **Yayımlanmış kaydın içeriğine yazma** (Update, Delete, Reconcile, kanıt paketi, karar, yeniden yayım) → 409
  `CASH_POSITION_PUBLISHED_IMMUTABLE`; ExportEvidence ve onun Reconcile'ı bu kurala takılmaz çünkü anlık görüntüye yazmaz
  (yalnız niyet / makbuz / bağlantı / olay); niyetteki `SnapshotContentHash` anlık görüntününkiyle eşleşmezse dışa aktarma
  uzak çağrısız 409 `CASH_POSITION_PUBLISHED_IMMUTABLE`.
- **Gönderilmiş kaydı silme** → 409 `CASH_POSITION_INVALID_STATUS_TRANSITION`.
- **Yetkisiz aktör** (ilgili izin yok) → API 403; UI: kabuk içinde erişim açıklaması, düğme yok (UAS-001).
- **Denetim kaydı yazılamıyor** (outbox yazımı başarısız) → işlem geri alınır, komut başarısız, iş verisi yazılmaz.
- **Dışa aktarma belirsiz / kesin ret** → 202 `CASH_POSITION_EVIDENCE_EXPORT_OUTCOME_UNKNOWN` / 502
  `CASH_POSITION_EVIDENCE_EXPORT_FAILED`; yayım ve kanıt kilidi değişmez.

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (kaynakta dört denetleyicide de
  `[HasPermission]` yok, `mod0124` izin sabiti yok — paket §E, §G; level-1 koşul 2).
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça, çok kelimede tire; ad alanı `finance-treasury.*` (D11/4). Kaynakta
  izin anahtarı olmadığı için eşleme ve takma ad yoktur.
- Aktör: `tenant_user`; `platform_admin` geçişi standarttır. Arka plan aktörü bu dilimde yok.

| Recovery anahtarı | Katman | Uçlar |
|---|---|---|
| `finance-treasury.cash-positions.read` | T1 | GET liste, detay, trace, exceptions, current |
| `finance-treasury.cash-positions.read-tenant-wide` | T3 (yalnız açık atama, `ExplicitGrantOnlyPermissions`) | kiracıdaki tüm şirketlerin anlık görüntüleri, `current`'ı ve eşik politikaları (SEC-002 §3.5, D14/1) — **yalnız okuma**; hiçbir yazma komutunun kapsamını genişletmez (gap 4/G1) |
| `finance-treasury.cash-positions.create` | T1 | POST |
| `finance-treasury.cash-positions.update` | T1 | PUT |
| `finance-treasury.cash-positions.delete` | T1 | DELETE |
| `finance-treasury.cash-positions.bulk-delete` | T2 | DELETE bulk |
| `finance-treasury.cash-positions.reconcile` | T3 | reconcile (iş mutabakatı) |
| `finance-treasury.cash-positions.attach-evidence` | T3 | evidence-packs |
| `finance-treasury.cash-positions.submit` | T3 | submit |
| `finance-treasury.cash-positions.approve` | T2 | approve |
| `finance-treasury.cash-positions.reject` | T2 | reject |
| `finance-treasury.cash-positions.workflow-reconcile` | T3 | workflow/reconcile (kurtarma) |
| `finance-treasury.cash-positions.publish` | T3 | publish |
| `finance-treasury.cash-positions.export-evidence` | T3 | evidence-export |
| `finance-treasury.cash-positions.export-reconcile` | T3 | evidence-export/reconcile (kurtarma) |
| `finance-treasury.cash-position-threshold-policies.read` | T1 | GET liste, detay |
| `finance-treasury.cash-position-threshold-policies.create` | T1 | POST |
| `finance-treasury.cash-position-threshold-policies.update` | T1 | PUT |
| `finance-treasury.cash-position-threshold-policies.delete` | T1 | DELETE |
| `finance-treasury.cash-position-threshold-policies.bulk-delete` | T2 | DELETE bulk |

- Kullanıcının MOD-0023 tarafında da `platform.workflow.instances.start` (gönderen) ve `platform.workflow.tasks.approve`
  / `.reject` (onaylayıcı) izinleri olmalıdır; rol şablonu MOD-0018 sahibiyle (MOD-0126 O-5 ile ortak iş).
- Tier-3 eylemler (`read-tenant-wide`, `reconcile`, `attach-evidence`, `submit`, `workflow-reconcile`, `publish`,
  `export-evidence`, `export-reconcile`) bu pakette kayıtlıdır (PKS-001 §5 Tier 3). `read-tenant-wide` hiçbir rol
  şablonunda varsayılan değildir.

## Audited Events

Yol `b`: `Diten.BuildingBlocks.CentralAudit` ortak ileticisi (AUD-001 §5 K4) — komut `ICentralAuditDeclaration` taşır,
servis `AddCentralAudit<FinanceTreasuryCentralAuditScope>(…, RequireEnlistment = true)` kaydeder (MOD-0126 DL-1), her
depo işlemi olayı **aynı Mongo işlemine** yazar (`CentralAuditTransaction.EnlistAsync`). **Servisin denetim altyapısı
bugün yok** (servis yok; `tests/architecture/audit-ledger/` altında `Diten.FinanceTreasuryService.md` yok — ölçüldü):
kurulum MOD-0126 DL-1'in ilk adımıdır, defter satırı CT kararı ister; o gelmeden MOD-0124 yazma komutu yazılamaz
(Dependencies). Kaynaktaki yerel kayıttan **sonra**, en iyi çaba, hatayı yutan yayım (`CashPositionAuditEmitter.cs:9, :187-225@da4abb20`;
`AuditDispatch.Enabled=false`, istemci FailClosed — paket §H, §L.8) taşınmaz.

`Category`: Platform `AuditCategory`'de finans değeri yok (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-27`);
MOD-0126 O-6 ile ortak eklemeli `Finance` önerisi — karar çıkana kadar değer uydurulmaz. `SourceModule`: `MOD-0124`.
`EntityType`: `CashPositionSnapshot` (aksi yazılmadıkça).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | K2 (fail-closed) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreateCashPositionCommand | finance-treasury.cash-position.created | CashPositionSnapshot | b | AfterState alan adları: SnapshotCode, ValueDate, PositionCurrencyCode, LineCount, Status | hayır (D14/3) | — |
| UpdateCashPositionCommand | finance-treasury.cash-position.updated | CashPositionSnapshot | b | değişen alan **adları**; ContentRevision önce→sonra; onay düştüyse ApprovalStatus önce→sonra | hayır | — |
| DeleteCashPositionCommand | finance-treasury.cash-position.deleted | CashPositionSnapshot | b | BeforeState: SnapshotCode, Status | hayır | — |
| BulkDeleteCashPositionCommand | finance-treasury.cash-position.bulk-deleted | CashPositionSnapshot | b | silinen kimlik sayısı; her kimlik için ayrı varlık kaydı (çoklu kimlik biçimi DL-1'de ölçülür, MOD-0126 ile aynı) | hayır | — |
| ReconcileCashPositionCommand | finance-treasury.cash-position.reconciled | CashPositionSnapshot | b | Status, ReconciliationStatus önce→sonra; açık / engelleyici istisna sayısı; AppliedThresholdPolicyId + sürüm | hayır | — |
| CreateCashPositionEvidencePackCommand | finance-treasury.cash-position.evidence-pack-created | CashPositionEvidencePack | b | PackVersion, SnapshotRevision, kalem sayısı | hayır | — |
| SubmitCashPositionCommand | finance-treasury.cash-position.submitted (sonuç, tx2/Reconcile) | CashPositionSnapshot | b | ApprovalStatus Submitted→PendingApproval, WorkflowInstanceId, SnapshotRevision (tx1 deneme olayı `…approval-start-requested`: ApprovalStatus →Submitted, uzak anahtar) | hayır | — |
| ApproveCashPositionCommand | finance-treasury.cash-position.approved | CashPositionSnapshot | b | karar kimliği, SodResult, ApprovalStatus, ApprovedSnapshotRevision | hayır (D14/3) | — |
| RejectCashPositionCommand | finance-treasury.cash-position.rejected | CashPositionSnapshot | b | karar kimliği, ReasonCode, ApprovalStatus | hayır (D14/3) | — |
| ReconcileCashPositionWorkflowCommand | kapattığı niyetin **sonuç** olayı (geçiş matrisi; ör. `…submitted`, `…approval-start-cancelled`, `…decision-cancelled`); niyet değişmediyse olay yok | CashPositionSnapshot | b | niyet / karar durumu önce→sonra; bağlı makbuz yalnız `Pending` ise `Completed` | hayır | — |
| PublishCashPositionCommand | finance-treasury.cash-position.published | CashPositionSnapshot | b | Status Reconciled→Published, SupersededSnapshotId, kilitlenen kanıt paketi kimliği, geçerli karar kimliği | hayır (D14/3) | — |
| ExportCashPositionEvidenceCommand | finance-treasury.cash-position.evidence-exported (sonuç, tx2/Reconcile) | CashPositionEvidencePack | b | kalem sayısı, bağlanan / reddedilen sayısı (tx1 deneme olayı `…evidence-export-requested`) | hayır | — |
| ReconcileCashPositionEvidenceExportCommand | kapattığı dışa aktarma niyetinin **sonuç** olayı (`…evidence-exported` / `…evidence-export-refused` / `…evidence-export-cancelled`); değişmediyse olay yok | CashPositionEvidencePack | b | niyet durumu önce→sonra | hayır | — |
| CreateCashPositionThresholdPolicyCommand | finance-treasury.cash-position-threshold-policy.created | CashPositionThresholdPolicy | b | PolicyCode, CurrencyCode, dört eşik değeri, geçerlilik | hayır | — |
| UpdateCashPositionThresholdPolicyCommand | finance-treasury.cash-position-threshold-policy.updated | CashPositionThresholdPolicy | b | değişen alan adları + eşik değerleri önce→sonra (yapılandırma değeridir, kişisel veri değil) | hayır | — |
| DeleteCashPositionThresholdPolicyCommand | finance-treasury.cash-position-threshold-policy.deleted | CashPositionThresholdPolicy | b | BeforeState: PolicyCode, CurrencyCode | hayır | — |
| BulkDeleteCashPositionThresholdPolicyCommand | finance-treasury.cash-position-threshold-policy.bulk-deleted | CashPositionThresholdPolicy | b | silinen kimlik sayısı; kimlik başına kayıt | hayır | — |

- **İki aşamalı komutların deneme olayları:** Submit → `finance-treasury.cash-position.approval-start-requested` (tx1) +
  sonuç `…submitted` / `…approval-start-refused` (tx2 / Reconcile); Approve / Reject →
  `finance-treasury.cash-position.decision-requested` (tx1, karar türü alanda) + sonuç `…approved` / `…rejected` /
  `…decision-refused`; ExportEvidence → `finance-treasury.cash-position.evidence-export-requested` (tx1) + sonuç
  `…evidence-exported` / `…evidence-export-refused`. Yalnız Reconcile'ın yazdığı olumsuz sonuçlar:
  `…approval-start-cancelled`, `…decision-cancelled`, `…evidence-export-cancelled`. Deneme olayı tek başına "yapıldı"
  anlamına gelmez; tabloda yazılan olay **sonuç** olayıdır. Reconcile kendi tek olayını değil kapattığı niyetin sonuç
  olayını yazar.
- **K2 — KARAR (sahip kararı D14/3):** Cash Positioning yayımı K2 **değildir** (para yetkisi değil; pozisyon bir
  ölçümdür). K2 yalnız MOD-0126'nın para yetkisi veren kararları (D11/3) ile AP ve AR'nin parayı etkileyen kararlarıdır
  (D14/3); MOD-0124'ün hiçbir komutu K2 sayılmaz (onay / ret yayımı besler, para yetkisi vermez). AUD-001 §4.3 listesi de
  nakit pozisyonunu kapsamıyor. Davranış yine de kapalı-başarısızdır: `RequireEnlistment = true` ile kayıt iş işlemiyle
  **aynı işlemde** yazılır; outbox yazımı başarısızsa işlem geri alınır ve komut başarısız olur (sabotaj testi AC-17).
- **Kayda yazılmayan:** banka referans değerleri, `SourceReference` metni, satır tutar ayrıntısı, not ve karar notu
  serbest metni, token. Değişen alanların yalnız **adı** yazılır (AUD-001 §3); eşik politikası değerleri istisnadır
  (yapılandırma değeridir, eski/yeni değer denetim için gerekir).
- **Bilinen boşluk G-AUD-1 (MOD-0126 ile ortak):** `CentralAuditBehavior` başarısız komut için kayıt bırakmaz; SoD reddi,
  MOD-0023 kesin reddi ve yetkisiz denemeler bu dilimde yalnız modülün kendi izinde (karar `State`, niyet `Status`)
  görünür. Merkezi ret kaydı BL-501 sahibinin işidir (follow-up).
- **Okuma denetimi:** gerekmez (dışa aktarma/export ekranı yok). **Arka plan işi yok:** uzlaştırma süpürücüsü yazılmaz
  (servis kimliği engeli, MOD-0126 ile aynı), bu yüzden İ2 satırı yok. İstisna sınıfı (İ1…İ5) kullanan komut yok.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `gateway/Diten.ApiGateway/ocelot.json`'da `cash` / `treasury` / `5071` geçen
  rota yok (ölçüldü). Gerekli rotalar: `/api/cash-positions`, `/api/cash-positions/{everything}`,
  `/api/cash-position-threshold-policies`, `/api/cash-position-threshold-policies/{everything}` → `localhost:5071`;
  `UpstreamHttpMethod` GET, POST, PUT, PATCH, DELETE, **OPTIONS**; explicit rotalar catch-all'dan önce (`routes.md`);
  `/api/cash-positions/current` ve `/bulk` gibi sabit alt yollar `{id}` rotasından önce.
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür; önkoşul AGENTS.md §3 / `ports.md` 5071 kaydı.
  MOD-0126 rotalarıyla aynı task'ta açılabilir.
- Kaynak rotaları (`/api/v1/mod0124`, `/api/v1/mod0124/{everything}` → 127.0.0.1:5058, kaynak `ocelot.json:1094-1126@da4abb20`,
  paket §A; `cash-positioning/*` takma adları gateway'de yönlendirilmiyor, paket §C) taşınmaz.
- Servisten MOD-0023 (`/api/v1/workflow/*`), MOD-0220 (`/api/legal-entities/*`), MOD-0031 (`/api/v1/evidence/*`) ve
  MOD-0048 (`/api/lookups/reference/*`) çağrıları ve veri kapsamı tüketimi (SEC-002, O-23) Gateway üzerinden ya da ortamın
  servis-servis adresinden; adresler yapılandırmadan, eksikse servis başlamaz.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir,
pack'te asla izlenmez; bu pack'teki her AC yalnız "tanımlı"dır.

- [ ] AC-1 (zorunlu) — `POST /api/cash-positions` geçerli istekle 201 döner; anlık görüntü `Draft`, `BalanceSourceKind =
  ManualEntry`, `SnapshotCode` RCS-001 biçiminde ortak servisten (istekte gönderilen kod 400), toplamlar ve `LineCount`
  sunucu hesabı; `TenantId` / `LegalEntityId` sunucuda yazılı, yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — LE `OperationalStatus = Active` ve `ApprovalStatus = Approved` değilse nakit pozisyonu oluşturan /
  değiştiren her yazma komutu (Create, Update, Delete, BulkDelete, Reconcile, kanıt paketi, Submit, Approve, Reject, Publish,
  ExportEvidence, eşik politikası Create/Update/Delete/BulkDelete) 422 `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE` döner ve
  kayıt değişmez (komut başına ayrı test); kontrol her çağrıda MOD-0220 `lookup-validation`'dan yapılır. MOD-0220
  erişilemezse 503 ve geçiş yok. Aynı LE'nin liste, detay ve `current` okuması 200 döner (okuma etkilenmez) ve açık niyetin
  kurtarma komutu tamamlanır (D14/4).
- [ ] AC-3 (zorunlu) — Başka kiracının ya da başka şirketin anlık görüntüsüne ve politikasına okuma, `current`,
  güncelleme, silme, mutabakat, karar ve yayım 404 döner.
- [ ] AC-4 (zorunlu) — Satır para birimi pozisyon para biriminden farklıysa 400 `CASH_POSITION_MIXED_CURRENCY`; aynı
  `(BankAccountRef, CashCategoryType)` iki satırda 400 `CASH_POSITION_DUPLICATE_LINE_KEY`; tutarlar `decimal`, para birimi
  hanesini aşan tutar 400 (yuvarlama yok); kod tabanında para alanı için `float`/`double` yok (mimari testi).
- [ ] AC-5 (zorunlu) — `SourceReference`, `Notes` ve `DecisionNote` içinde 8+ ardışık rakam 400
  `CASH_POSITION_SENSITIVE_TEXT_REJECTED`; hiçbir varlık, DTO ya da denetim kaydı IBAN, hesap numarası, maskeli hesap, BIC/SWIFT
  ya da banka adı alanı taşımaz (alan adı taraması testi); banka referansı yalnız Guid.
- [ ] AC-6 (zorunlu) — Reconcile, oturum LE'si + pozisyon para birimi için `ValueDate`'te geçerli tek politikayı seçer ve
  değerlerini anlık görüntüye sabitler; politika yoksa 422 `CASH_POSITION_THRESHOLD_POLICY_MISSING`; tazelik ve fark
  kuralları kaynakla aynı sınıflandırmayı üretir (Critical/engelleyici, Medium/uyarı; eşik 0 = kapalı) — tablo güdümlü
  birim testi kaynak `CashPositionReconciliationService.cs:162-241@da4abb20` davranışını ölçer.
- [ ] AC-7 (zorunlu) — Yeniden mutabakat aynı satır + tür için ikinci açık istisna açmaz; koşul kalkan istisna `Resolved`
  olur; `ContentRevision` artar ve önceki kanıt paketi ile onay geçersiz olur.
- [ ] AC-8 (zorunlu) — Eşik politikası: aynı LE + para biriminde çakışan geçerlilik aralığı 409
  `CASH_POSITION_THRESHOLD_POLICY_OVERLAP`; eşzamanlı iki çakışan yazımdan biri 409 alır (gerçek replica set, slot CAS
  kaldırıldığında test kırmızıya döner — sabotaj); `CurrencyCode` zorunlu; `Critical < Warning` 400.
- [ ] AC-9 (zorunlu) — Kanıt paketi sunucunun `ContentRevision`'ına bağlanır (istemciden revizyon alınmaz); anlık görüntü
  başına tek geçerli paket; içerik değiştikten sonra eski paketle Submit/Publish 409 `CASH_POSITION_EVIDENCE_REVISION_CONFLICT`.
- [ ] AC-10 (zorunlu) — Submit, MOD-0023'ü çağırmadan önce `CashPositionApprovalStartIntent(Pending)` ve
  `ApprovalStatus = Submitted`'ı commit eder; uzak çağrı niyetteki deterministik anahtarla yapılır; açık engelleyici
  istisna, eksik kanıt ya da `Reconciled` olmayan durumda uzak çağrı olmadan 409.
- [ ] AC-11 (zorunlu) — Başlatma zaman aşımı, bağlantı kopması ve boş/okunamayan 2xx yanıtı 202
  `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN` üretir; niyet `Unknown`, `ApprovalStatus = Submitted`; kesin ret 502
  `CASH_POSITION_WORKFLOW_START_FAILED`, niyet `Refused`, `ApprovalStatus` önceki değerine döner.
- [ ] AC-12 (zorunlu) — Açık başlatma niyeti varken **farklı** anahtarla Submit 409 `CASH_POSITION_WORKFLOW_START_PENDING`
  ve uzak çağrı yok; `ReconcileCashPositionWorkflowCommand` aynı uzak anahtarla ilerler ve MOD-0023'te aynı anlık görüntü
  için ikinci örnek oluşmaz (sahte MOD-0023 anahtar başına tek örnek sayar).
- [ ] AC-13 (zorunlu) — SoD: oluşturan, gönderen ve son içerik değiştiren kullanıcının Approve **ve** Reject çağrısı 403
  döner (`FailedCreator` / `FailedSubmitter` / `FailedContentUpdater`) ve MOD-0023'e hiçbir istek gitmez (sahte istemciyle
  sayılır); büyük/küçük harf farklı yazılmış aynı Guid `sub` aynı kişi sayılır; Guid olmayan `sub` 403.
- [ ] AC-14 (zorunlu) — Kararlar yerel `Requested` niyetinden sonra MOD-0023'e uzak karar anahtarıyla gider; belirsiz sonuç
  202 `CASH_POSITION_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır; açık karar niyeti varken başka anahtar/aktörden karar 409
  `CASH_POSITION_DECISION_PENDING`, uzak çağrı yok.
- [ ] AC-15 (zorunlu) — Publish uzak çağrı yapmaz (sahte MOD-0031 / MOD-0023 çağrı sayısı 0); onaylı, kanıtlı, mutabakatlı,
  engelleyici istisnasız anlık görüntüde 200; aynı yuvadaki önceki yayım aynı işlemde `Superseded` olur; yuvada her zaman
  tek `IsCurrentPublished`; eşzamanlı iki yayımdan biri 409 `CASH_POSITION_PUBLICATION_SLOT_CONFLICT`.
- [ ] AC-16 (zorunlu) — Idempotency (D12/2): her yazma komutu anahtarsız 400; aynı kapsam + aynı yük, makbuz `Completed`
  ise kayıtlı yanıtı, `Pending` ise 202 `CASH_POSITION_REQUEST_IN_PROGRESS` döndürür (tekrar kurtarma yapmaz) ve ikinci
  kayıt, ikinci uzak çağrı ya da ikinci merkezi olay oluşmaz; aynı kapsam + farklı yük 409; aynı anahtar başka aktörde
  ayrı makbuzdur; **ilk yanıtın kaybolduğu** senaryo ayrıca koşulur.
- [ ] AC-17 (zorunlu) — Merkezi denetim: tek aşamalı komutlar tam **bir** olay bırakır; iki aşamalı komutlar (Submit,
  Approve, Reject, ExportEvidence) tx1'de tam bir deneme, sonuç kesinleşince tx2'de (ya da Reconcile'da) tam bir sonuç olayı
  bırakır; idempotent tekrar yeni olay üretmez. Outbox yazımı zorla başarısız edildiğinde komut başarısız olur ve iş verisi
  yazılmaz (sabotaj testi); uzak başarı sonrası tx2 zorla başarısız edildiğinde niyet açık kalır ve Reconcile uzak çağrıyı
  tekrarlamadan sonuç olayını yazar (kurtarma testi).
- [ ] AC-18 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard`
  yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş.
- [ ] AC-19 (zorunlu) — Mongo commit sonucu belirsizliği: `UnknownTransactionCommitResult` etiketli hata enjekte edildiğinde
  yanıt 503 `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN` olur, `VERSION_CONFLICT` ya da `PUBLICATION_SLOT_CONFLICT` değil; aynı
  anahtarla tekrar commit olmuş işlemde kayıtlı yanıtı verir, ikinci yayım/olay oluşmaz (yayım, mutabakat ve karar yolları
  için ayrı test).
- [ ] AC-20 (zorunlu) — ExportEvidence yalnız yayımlanmış anlık görüntünün kilitli paketinde çalışır; MOD-0031'i çağırmadan
  önce `CashPositionEvidenceExportIntent(Pending)` commit edilir; belirsiz sonuç 202, kesin ret 502; açık niyet varken
  farklı anahtar 409 `CASH_POSITION_EVIDENCE_EXPORT_PENDING`; `ReconcileCashPositionEvidenceExportCommand` yalnız
  `links/query` okumasıyla kapatır, MOD-0031'e yazma çağrısı yapmaz (ikinci bağlantı oluşmaz); okuma bağlantıyı
  göstermezse niyet `Unknown` kalır (gap 5/G2); hiçbir durumda yayım ya da kilit değişmez.
- [ ] AC-21 (zorunlu) — Her denetleyici aksiyonunda `[HasPermission]` vardır (mimari testi); izinsiz çağrı 403.
- [ ] AC-22 (zorunlu) — Soft delete yalnız hiç gönderilmemiş `Draft`/`Reconciled` anlık görüntüde; `IsDeleted` + `DeletedAt`
  (UTC) birlikte yazılır, silinen kayıt listede görünmez, kodu yeniden kullanılmaz; yayımlanmış kaydın içeriğine her yazma
  409 (dışa aktarma istisnası AC-34).
- [ ] AC-23 (zorunlu) — `GET /api/cash-positions/current?valueDate=&currencyCode=` oturum LE'sinin geçerli yayımını döner;
  yoksa 404; `Superseded` kayıt dönmez; yanıt şekli `cash-position.openapi.yaml` ile eşleşir.
- [ ] AC-24 (zorunlu) — `Views/FinanceTreasury/CashPositions/*.cshtml` ve `Views/FinanceTreasury/CashPositionThresholdPolicies/*.cshtml`
  dosyalarının tamamında `Layout = "_LayoutTenantShell"` açıkça yazılı; `CashPositions` altında `_CreateEditOffcanvas.cshtml`
  / `_DetailsQuickView.cshtml` yok, `CashPositionThresholdPolicies` altında ikisi de var;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area FinanceTreasury --module CashPositions --format gaps` ve
  `… --module CashPositionThresholdPolicies --format gaps` sapma vermez, `quality-gate-datatable` geçer;
  `<table data-dt-data-mode="server">` / `"client"` pack `screens` kararıyla eşleşir.
- [ ] AC-25 (zorunlu) — İki ekranın resx dosyaları 7 dilde (`en, tr, fr, es, zh, ar, ru`) parite PASS; İngilizce olmayan
  dosyada İngilizce yer tutucu yok; hardcoded metin yok (`grep` 0); istisna önerileri (`RecommendedActionKey`) resx
  anahtarıdır.
- [ ] AC-26 (zorunlu) — İlgili `read` izni olmayan kullanıcı sayfayı açınca kabuk içinde erişim açıklaması görür; başlık,
  tablo, "Yeni" düğmesi çizilmez, yönlendirme olmaz (UAS-001, canlı denendi); SoD nedeniyle karar veremeyen kullanıcıya
  karar düğmesi çizilmez.
- [ ] AC-27 (zorunlu) — Sayfa yardım metni `Help.CASHPOSITIONS.Purpose`, `.Audience`, `.Step1…Step5` + `.Step{n}Title` ve
  `Help.CASHPOSITIONTHRESHOLDPOLICIES.Purpose`, `.Audience`, `.Step1…Step3` + `.Step{n}Title` 7 dilde var.
- [ ] AC-28 (zorunlu) — `docs/analysis/contracts/cash-position.openapi.yaml` yayımlanmış; uçlar, hata kodları ve
  `Idempotency-Key` başlığı sözleşmeyle eşleşir (sözleşme testi).
- [ ] AC-29 (zorunlu) — Bir oluşturma, bir güncelleme ve bir silme dev ortamında çalıştırılır; üç merkezi kayıt AUD-001
  §8 sorgusuyla gösterilir ve on alan işaretlenir (rapor ortamın adını yazar).
- [ ] AC-30 (zorunlu) — Yarış testleri (gerçek replica set, paralel istek): (a) aynı aktörün aynı anahtarla iki eşzamanlı
  Approve'u → tek karar, tek uzak geçiş; (b) iki farklı onaylayıcının farklı anahtarla eşzamanlı Approve ve Reject'i → tek
  açık karar niyeti, diğeri 409, MOD-0023'e tek çağrı; (c) iki eşzamanlı Submit (farklı anahtar) → tek başlatma niyeti;
  (d) iki eşzamanlı ExportEvidence (farklı anahtar) → tek dışa aktarma niyeti. Tekil kısmi indeksler kaldırıldığında
  (b), (c), (d) kırmızıya döner (sabotaj).
- [ ] AC-31 (zorunlu) — Veri kapsamı (SEC-002 §4 kanıt testi, D14/1): aynı yetkiye (`cash-positions.read`,
  `cash-position-threshold-policies.read` + yazma izinleri) sahip iki kullanıcı, iki ayrı şirket kapsamı → birinin anlık
  görüntü ve politika listesinde diğerinin şirketinin kaydı çıkmaz; detay, iz, `current` ve her yazma 404; hiçbir şirket
  kapsamı olmayan üçüncü kullanıcı boş liste görür ve her detay 404; `read-tenant-wide` izni olan kullanıcı iki şirketin
  kayıtlarını görür. Çözücü hata verdiğinde liste boş döner. Kapsam daraltması (`IFinanceTreasuryDataScopeSource`
  süzgeci) kaldırıldığında test kırmızıya döner (sabotaj); `read-tenant-wide` otomatik yoldan verilmez
  (`ExplicitGrantOnlyPermissions` listesinde). Makbuz tipi servis ortak `FinanceTreasuryCommandReceipt`'tir: MOD-0124
  kodunda modül önekli makbuz tipi ya da makbuz koleksiyonu yoktur (mimari testi, D14/2).
- [ ] AC-32 (zorunlu) — Okuma kapsamı yazma kapsamını genişletmez (gap 4/G1): `cash-positions.read-tenant-wide` + bütün yazma
  izinlerine sahip ama LE-B'de **kendi şirket yetkisi olmayan** kullanıcı LE-B anlık görüntülerini, politikalarını ve
  `current`'ını görür (200), ama LE-B'de Create (`X-Legal-Entity-Id = LE-B`), Update, Reconcile, Submit, Approve, Reject,
  Publish, ExportEvidence ve eşik politikası Create 404 `CASH_POSITION_NOT_FOUND` döner; MOD-0023 / MOD-0031 / MOD-0220'ye
  çağrı gitmez, kayıt, makbuz ve denetim olayı oluşmaz (sahte istemcilerle sayılır); aynı kullanıcı LE-A'da (kendi yetkisi)
  aynı komutları çalıştırabilir. Sabotaj: yazma yolunda okuma kapsamı kullanıldığında test kırmızıya döner.
- [ ] AC-33 (zorunlu) — Reconcile önce okur (gap 4/G2; Submit, Approve, Reject ve ExportEvidence için ayrı test): (a) uzakta
  niyetin kendi eşleme alanlarıyla (AC-36) örnek / geçiş / bağlantı **var** → şirket uygun değilken bile sonuç kaydedilir (MOD-0220 çağrılmaz — D15/2), bağlı
  `Pending` makbuz `Completed`, tek sonuç olayı; (b) okuma 404 / boş / zaman aşımı / 5xx → niyet **`Cancelled` olmaz**,
  `Unknown` kalır, anlık görüntü ve makbuz değişmez, zaman aşımı / 5xx'te uzak yazma/başlatma çağrısı yok (okuma çağrısı
  yapılır); (c) Submit/kararda okuma örnek / geçiş göstermedi + şirket uygun değil → MOD-0023'e uzak yazma/başlatma
  çağrısı yok, niyet `Unknown`, `LastOutcomeCode = CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422, sonuç olayı
  yok; (d) Submit/kararda okuma örnek / geçiş göstermedi + gap 6/P1-a'nın iki koşulu kapalı (AC-35) + şirket uygun +
  SoD/izin geçer + asıl aktör → başlatma / geçiş
  aynı deterministik anahtarla yeniden yazılır ve MOD-0023'te tam **bir** örnek / geçiş vardır (ikinci Reconcile
  ikincisini üretmez; yinelenen anahtar hatası `Unknown` bırakır, asla `Refused` değil); aktör farklıysa 409
  `CASH_POSITION_RECONCILE_RESTART_ACTOR_MISMATCH`, uzak yazma/başlatma çağrısı yok; (e) ExportEvidence'ta
  `ReconcileCashPositionEvidenceExportCommand` MOD-0031'i hiç yeniden çağırmaz (sahte MOD-0031 `POST links` sayısı
  Reconcile'da 0); belirsiz ya da boş `links/query` okumasında niyet `Unknown` kalır. Sabotaj: okuma atlanıp doğrudan
  yeniden yazıldığında, "bulunamadı" `Cancelled`'a bağlandığında, farklı anahtarla yeniden yazıldığında ya da dışa
  aktarma Reconcile'ı MOD-0031'e yazdığında test kırmızıya döner.
- [ ] AC-34 (zorunlu) — Yayım sonrası değişmezlik ve dışa aktarma (gap 4/G3): Publish `PublishedContentHash`'i yazar;
  ExportEvidence ve `ReconcileCashPositionEvidenceExportCommand` (başarı, ret, belirsiz ve kurtarma yollarında) sonrasında
  anlık görüntünün belgesi bayt bayt aynıdır (`Version`, içerik ve hash değişmez; test belgeyi önce/sonra karşılaştırır),
  kanıt paketi `Locked` kalır; yazılan kayıtlar yalnız dışa aktarma niyeti (anlık görüntü kimliği + `SnapshotContentHash`),
  makbuz, kanıt bağlantı sonuçları ve olaylardır. Aynı yayımlanmış kayda Update / Reconcile / kanıt paketi / karar 409
  `CASH_POSITION_PUBLISHED_IMMUTABLE`; yeni yayım önceki kaydın yalnız yaşam döngüsü alanlarını (`Superseded`) değiştirir,
  içeriğini ve hash'ini değiştirmez. Sabotaj: dışa aktarma yolu anlık görüntüye yazdığında test kırmızıya döner.
- [ ] AC-35 (zorunlu) — Silinmiş uzak kayıttan sonra yeniden yazma güvenli sayılmaz (gap 6/P1-a; Submit, Approve, Reject için
  ayrı test): sahte MOD-0023'te niyetin örneği / geçişi oluşturulup sonra soft delete edilir (ya da temizlenir) ve niyet
  `Unknown` bırakılır; Reconcile (a) silinmiş kaydı gösteren okumada ve (b) silinmemiş kayıt dönmeyen ve silinmiş durumu
  göstermeyen okumada niyetin ilk denemesi kayıtlı (tx1 commit olmuş) durumda aynı anahtarla yeniden yazmaz: niyet `Unknown`, 202
  `CASH_POSITION_WORKFLOW_START_OUTCOME_UNKNOWN` / `CASH_POSITION_DECISION_OUTCOME_UNKNOWN`, anlık görüntü ve bağlı makbuz
  değişmez, sahte MOD-0023'te yazma çağrısı 0. Sabotaj: silinmiş durum ve ilk deneme kontrolü kaldırıldığında Reconcile
  ikinci bir örnek / geçiş oluşturur ve test kırmızıya döner.
- [ ] AC-36 (zorunlu) — Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b): (a) aynı anlık görüntüde önceki
  `SubmissionSequence`'in kapanmış (reddedilmiş / iptal) örneği ve yeni gönderimin `Unknown` niyeti varken Reconcile yeni
  niyeti eski örnekle kapatmaz; yeni niyet `Unknown` kalır ya da yalnız kendi uzak anahtarıyla eşleşen örnekle kapanır;
  (b) aynı görevde başka bir kararın geçişi (farklı `RemoteIdempotencyKey`, görev ya da eylem) açık karar niyetini
  kapatmaz; (c) dışa aktarmada başka bir niyetin (farklı `RemoteKey`) bağlantısı kalemi kapatmaz; (d) okuma yanıtı
  eşleme alanını taşımıyorsa niyet `Unknown` kalır. Sabotaj: eşleme yalnız anlık görüntü kimliğine indirildiğinde test
  kırmızıya döner.

## Test Expectations

- **Birim:** validatorlar (her alan, iki ekran); `CashPositionReconciler` (tazelik sınırı `== BlockingFreshnessHours`, fark
  sınırı `== CriticalAbsoluteAmount`, eşik 0, para birimi uyumsuzluğu, beklenen bakiye yok); SoD R1–R7 (Guid
  karşılaştırması dahil); durum makinesi ve geçiş matrisi; içerik revizyonu ve onay düşürme; idempotency parmak izi;
  RCS kod portu sahtesiyle kod biçimi; 8+ rakam koruması.
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı/şirket izolasyonu (404); veri kapsamı (AC-31, sabotajlı, iki
  ekran); soft delete; idempotent tekrar; CAS 409; CentralAudit kaydının iş işlemiyle commit/rollback'i ve outbox yazım hatasında komutun başarısız olması (sabotaj);
  tekil indeksler (kod, satır anahtarı, açık istisna, geçerli kanıt, yayım yuvası, açık niyetler, makbuz kapsamı, politika
  yuvası); `UnknownTransactionCommitResult` enjeksiyonu (AC-19).
- **İş akışı sahte sunucusu:** zaman aşımı, bağlantı kopması, boş 2xx, 5xx, kesin 403/404, MOD-0023 "başlatma
  tamamlanmadı" 409; aynı anahtarla tekrar → tek örnek; karar geçişinde anahtar iletimi; `WorkflowIsFinalStep = false`.
- **Kanıt sahte sunucusu:** kalem başına bağlantı, kısmi ret, belirsiz, aynı uzak anahtarla tekrar → tek bağlantı;
  `links/query` ile uzlaştırma.
- **MOD-0220 sahtesi:** Active+Approved / Active+Draft / Inactive+Approved / 404 / erişilemez; TRM-14 gelince gerçek
  sözleşmeye karşı sözleşme testi.
- **Mimari:** her aksiyonda `[HasPermission]`; banka verisi alan adı yasağı; `float`/`double` para alanı yok; komut/handler
  adlarında suffix yok; `AuditTrailStandard` filtresi yeşil.
- **Sözleşme:** `cash-position.openapi.yaml` ile uç şekli (`current` dahil).
- **Frontend:** browser smoke (liste, oluştur, düzenle, detay sekmeleri, karar düğmeleri izin/SoD'ye göre; politika
  ekranında oluştur/düzenle/QuickView/sil); yetkisiz kullanıcı canlı denemesi (UAS-001); DataTable doğrulayıcı iki ekran
  PASS; RESX 7 dil parite PASS.
- **Build:** `Diten.FinanceTreasuryService` + `Diten.Web` + Gateway 0 hata; yerel kapıda `scripts/` Python testleri de
  koşulur.
- Kaynak testleri (kaynak kaydı: MOD-0124 407/407, DCP §9; paket §N.3 eklerin test sayıları devralınmış) **kanıt
  değildir**, Recovery'de yeniden yazılır; kaynakta olmayan testler (başlatma/dışa aktarma zaman aşımı, belirsiz sonuç,
  yayım yolunda `UnknownTransactionCommitResult`, izin kontrolü) burada zorunludur.

## Page Help (UXH-001)

- Sayfa kodu `CASHPOSITIONS` (`Nav.Page.CASHPOSITIONS`); Create/Edit/Details aynı kodun alt sayfalarıdır. Anahtarlar
  (kiracı 7 dil): `Help.CASHPOSITIONS.Purpose` (şirket ve gün bazında bilinen banka bakiyelerinden nakit pozisyonu
  hazırlama, onaylatma ve yayımlama; **bakiyelerin bankadan otomatik gelmediği** açıkça yazılır),
  `Help.CASHPOSITIONS.Audience` (hazine operatörü, onaylayıcı, hazine yöneticisi), `Help.CASHPOSITIONS.Step1…Step5` +
  `Step{n}Title` (Bakiyeleri gir · Mutabakat çalıştır · Kanıt ekle · Onaya gönder · Yayımla). `Immediate` yok.
- Sayfa kodu `CASHPOSITIONTHRESHOLDPOLICIES` (`Nav.Page.CASHPOSITIONTHRESHOLDPOLICIES`): `Purpose` (mutabakatta kullanılan
  fark ve tazelik eşikleri), `Audience` (hazine yöneticisi), `Step1…Step3` + `Step{n}Title` (Para birimini seç · Eşikleri
  gir · Geçerlilik aralığını belirle). `Immediate` yok.
- İlgili sayfa: iki ekran birbirine bağlanır (`PageHelpCatalog` satırı). **Tur:** gerekmez — beş akıştan biri değil →
  "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact ve Slim okundu (backend + frontend; iki ekran ayrı karar)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, `screens`, entity_base, form_field_count)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık (iki ekran)
- [x] Backend / Frontend file convention Golden Reference ile birebir (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; SoD kuralları R1–R7
- [x] Failure Path ≥ 4 senaryo (idempotency, missing, unauthorized, concurrency) + kiracılar arası + SoD + belirsiz sonuç + commit belirsizliği
- [x] Authorization: izin listesi + policy + aktör
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (yol b)
- [x] Veri kapsamı cümlesi (SEC-002 §4, D14/1) ve kanıt testi (AC-31)
- [x] Sayfa yardım metni anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir; test expectations build/verifier/RESX/smoke/sözleşme kapsıyor

**ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz):**
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`)
- [ ] DCP (UNASSIGNED-finance-treasury-intake) `approved` ve numarası atanmış
- [ ] SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (O-23; platform-shared-services) — kapsamı soran kod ve AC-31 bu yola bağlı
- [ ] **Karar 0.3 (MOD-0003)** verildi — önerilen süreli sapma (T32) kaydı (O-2); TRM-S2 bu karara bağlı
- [ ] **MOD-0037 kapısı** için sahip kararı (O-3): v1'in MOD-0037'yi tüketmeden başlaması
- [ ] **Onay yolu** kararı (O-4): MOD-0023 (öneri) ya da yerel maker-checker
- [ ] Port 5071 AGENTS.md §3 ve `.antigravity/rules/ports.md`'ye yazıldı; AGENTS §2 ve §9 güncellendi (TRM-03/04)
- [ ] Servis iskeleti + servis ortak yapı taşları + CentralAudit kurulumu + defter için CT kararı — **yalnız MOD-0126 DL-1** (tek sahibi o dilim; MOD-0124 DL-1'in önkoşulu — gap 4/G5)
- [ ] TRM-14: MOD-0220 `lookup-validation` yanıtında `ApprovalStatus` + MOD-0220 sözleşme belgesi (kuralın MOD-0124'e uygulanması D14/4 ile kararlaştırıldı)
- [ ] Sahip MOD-0124 için kod alma iznini verdi (D1)
- [ ] RCS-001 ortak kod servisi, şirket kısa kodu kaynağı ve `CPS` / `CTP` önekleri (O-9) — yoksa Create yazılamaz
- [ ] Para birimi küsurat hanesi kaynağı (O-8) — DL-1 için
- [ ] MOD-0023'te nakit pozisyonu onay şablonu (tek adım; O-20) — DL-3 için
- [ ] MOD-0023 aynı anahtarlı yarış yanıtı (politika KARAR D21/4) MOD-0023 pack §17 (TRM-15) AC-W5/W6 ile **geçti** — DL-3 için; geçene kadar yinelenen anahtar hatası `Unknown` (D21/7)
- [ ] MOD-0023 pack §17 (TRM-15) onaylandı ve AC-W1 (silmeme mimari testi) **geçti** — politika KARAR D21/1, silinmişi ayıran
  okuma gerekmez (D21/2); DL-3 için; kapanmazsa 404 / boş okumada anahtarlı yeniden yazma kapalı kalır (gap 6/P1-a)
- [ ] MOD-0023 okuma eşleme alanları (KARAR D21/3; §17 AC-W2/W3) kodda ve **geçti** — DL-3 için; yoksa okuma niyeti kapatmaz (gap 6/P1-b)
- [ ] MOD-0031 bağlama ucunun idempotency / sorgu davranışı ölçüldü (O-17) — DL-4 için; `links/query` davranışı DL-4'ün
  önkoşulu, dedupe ölçümü kurtarmada yeniden çağrıyı açmanın önkoşulu (ölçülmezse Reconcile yalnız okur, gap 5/G2)

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Ortak tanımlar: MOD-0126 pack @bu commit sha256 ZIP manifestine bakın. (Makbuz, niyet durum sözlüğü, Reconcile 3a
  kuralları ve servis ortak portlar orada tanımlıdır; bu pack ile çelişirse o sürüm bağlayıcıdır — gap 6/P2-c.)
- Producer paketi: MOD-0124 v1.0 (TRM-B5-01), `source-batch5-v1.0/…/mod0124-transfer-packet-v1.0/`, `manifest.json` sha256
  `b440aa2ab569dce78dfa66daf929c977afd5abf30a0be8ab422b44f449e10bcb`; paket dosyası `MOD-0124-TRANSFER-PACKET-v1.0.md`
  sha256 `2167b8c5f9f492a7891741efda9df36f0862c9680cd948bae45fc0e0d22401b3`, `CONTRACT-STATE.json` `0cf68b7f…8f07`,
  `SOURCE-BINDINGS.json` `f81e0763…1797`, `AUTHOR-REPORT.md` `64c4f1f8…c6f1` (bu oturumda `shasum -a 256` ile ölçüldü,
  manifest girdileriyle eşit). Paket E0 (yerel, yayımlanmamış), "CT decision: karar yok" (paket başlığı).
- Kaynak commit: `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d` (S). Bu paket kaynak kodu yalnız `git show` ile okudu.
- Derleme/test kanıtı (devralınmış): MOD-0124 **407/407** (DCP §9; handoff `source-build-evidence-v1.0`, SHA256SUMS
  `7ec2cd3332a4ff8d8f7c5fbfe1885b49a5f1e552ee4954806090b59726c536eb`). Recovery hazırlığının ölçüsü değildir.
- **Taslak/inceleme durumları:** kaynak ekler 6 (revizyon bağlama, "policy A") ve 7 (yayım yuvası çakışması → 409) `review`
  durumundadır ve kaynakta terfi ettirilmedi (paket §A.1). Bu paket iki davranışı **kendi kararı olarak** benimser
  (`ContentRevision` bağlama; `CASH_POSITION_PUBLICATION_SLOT_CONFLICT`); kaynak eklerin onayı varsayılmaz. Taban paket ile
  kaynak kodu arasındaki uyuşmazlıklar (paket §A.1 T22) uzlaştırılmadı; Recovery ölçüsü kaynak koddur.
- Kod alma (D1, kopya değil): her alınan dosya `path@da4abb20` + sha256 ile iz kaydına yazılır; aşağıdaki liste taşınmaz.

### Level-1 koşulları (`MOD-0124-v1.0.md`) → bu paket

1. Recovery değerleri (MOD-0037, MOD-0003) §M'de kullanılır → "Dependencies" ilk iki satır. **Ölçüm farkı:** sıra belgesi
   MOD-0037 için "73 files present" yazıyor; bu oturumun ölçümü paket var / uygulama yok / çalışma zamanı kodu 0. Fark
   kayıt altına alındı, O-3 bu ölçüme dayanır.
2. İzin anahtarı yok → Authorization Convention, AC-21. Yayım yolunda hata 112 / `UnknownTransactionCommitResult` →
   Runtime Constraints "Mongo işlem sonucu belirsizliği", AC-19. Dışa aktarma ve iş akışı yerel kayıttan önce → yerel niyet
   (Runtime Constraints 1–5), AC-10…12, AC-14, AC-20.
3. Banka durumu / ekstre MOD-0269 nedeniyle karşılanmamış → v1 kaynağı elle bilinen bakiye; Follow-up kapısı.
4. MOD-0003 kapısı (0.3) açık → Ready-for-dev engeli, O-2.

### Tüketici onayları (paket §P → §U)

| Yuva | Recovery'de durum | Bu paketin isteği | Durum |
|---|---|---|---|
| AUTH (MOD-0018) | JWT + `[HasPermission]`; `finance-treasury.*` | 20 anahtarın kaydı (`read-tenant-wide` açık atama dahil) ve rol şablonu | açık |
| LE (MOD-0220) | `lookup-validation` var; `ApprovalStatus` yok | TRM-14 + sözleşme belgesi | açık |
| BANK-ACCOUNT (MOD-0289) | yok | opak Guid; arama/doğrulama sözleşmesi (O-15) | açık — ertelendi |
| BANK-GATEWAY (MOD-0269) | yok, D5 kapalı | v1'de istek yok | v1 dışı |
| BANK-RECON (MOD-0121) | yok, dış | v1'de istek yok | kapsam dışı |
| PUBLICATION (MOD-0125, MOD-0126, MOD-0127) | tüketici yok | `current` okuma sözleşmesi; olay O-14 | açık |
| AUDIT (MOD-0021) | CentralAudit yol b | servis defteri + CT kararı | açık |
| WORKFLOW (MOD-0023) | var | onay şablonu (O-20) | açık |
| EVIDENCE (MOD-0028/MOD-0031) | MOD-0031 var; MOD-0028 tüketilmez | bağlama + sorgu davranışı (O-17) | açık |

Borçlu taraf her yuvada sahip (K1), tüketicinin sahibi adlandırılana kadar (paket §P). Level 3'ten önce hepsi kapanır
(level-1 kaydı).

### Taşınmayacaklar (do-not-carry)

1. `[Authorize]`-only denetleyiciler ve izin anahtarı yokluğu (paket §E, §G); her kimliği doğrulanmış kullanıcının içe
   aktarma, onay ve yayım yapabilmesi.
2. Yayım yolundaki hata 112 yakalayıcısının `UnknownTransactionCommitResult` etiketini ayırmaması
   (`MongoCashPositionRepository.cs:663@da4abb20`; karşılaştırma `:291`, `:459`).
3. Uzak-önce-yerel sırası: kanıt dışa aktarımı yayım kaydından önce (`CashPositionPublishService.cs:173` → `:226`), iş
   akışı başlatma karar kaydından önce (`CashPositionApprovalService.cs:68` → `:113`); niyet, makbuz ve uzlaştırma
   yokluğu (paket §H, §L.6–7); idempotency anahtarsız iş akışı başlatma (`CashPositionWorkflowService.cs:59-77`).
4. İstemcinin `DecisionStatus` göndererek iş akışı olmadan doğrudan "Approved" yazabilmesi (`CashPositionApprovalService.cs:91-108`).
5. Yerel kayıttan sonra, hatayı yutan en iyi çaba denetim (`CashPositionAuditEmitter.cs:187-225`); kaynak outbox'ı,
   dağıtıcı, deneme günlüğü ve `LoggingNoOp` / FailClosed alıcılar (`CashPositionOutboxSinks.cs:8-30`); FailClosed
   istemci varsayılanlarını uygulama saymak (paket §C, T6).
6. Eşlenmemiş her hatanın HTTP 400'e düşmesi, gerçek outbox hatasının 400 olması (`CustomBaseController.cs:185`).
7. Guid olmayan aktörlerde sıralı dize karşılaştırması (`CashPositionApprovalService.cs:142-145`); `string` aktör kimliği.
8. Yalnız içe aktarma anında LE geçerliliği; onay ve yayımda güncel uygunluk yokluğu (paket §G).
9. `CP-{yyyyMMdd}-{8hex}` anlık görüntü kodu ve `CPI-…` içe aktarma kodu (`CashPositionPersistenceService.cs:362, :387`);
   kullanıcı girişli `PolicyCode` / `CategoryCode`.
10. Maskeli hesap numarasının (`MaskedAccountRef`) saklanması ve gösterimi (`CashPositionEntities.cs:32`).
11. İstemciden gelen `SnapshotVersion` / `PackVersion` / `ThresholdPolicyId` / `ExpectedVersion` gövde alanları.
12. Para birimsiz eşik politikası, kiracı geneli `IsDefault` politika (`CashPositionEntities.cs:139-145`).
13. Serbest İngilizce istisna önerileri (`CashPositionReconciliationService.cs:111` vb.) ve hardcoded İngilizce ekran
    (0 `Localizer`, paket §I).
14. `_LayoutBackbone`, ham JSON içe aktarma penceresi, erişim politikasız menü girişi (paket §G, §I).
15. `/api/v1/mod0124` rotaları, 5058 portu, `mod0124_*` koleksiyon adları, `MOD0124_*` hata kodları, `mod0124.*` olay adları.
16. HTTP/çalışan çağıranı olmayan `IngestBankFeedCommand` ve Gateway uygulaması olmayan kaynak sözleşmesi / banka
    beslemesi / entegrasyon koşusu istemcileri (paket §C).
17. Yapılandırılmamış `mod0124.snapshot.published.v1` outbox olayı (tüketici sözleşmesi yok, O-14).

### Tasarım notları

- **v1 pozisyon kaynağı neden elle bilinen bakiye:** banka beslemesi ve ekstre MOD-0269'a bağlı ve D5 bu akışları
  kapattı; yönetişimli içe aktarma MOD-0003 + MOD-0037 ister ve ikisinin de Recovery'de üreticisi yok. Elle giriş bu üç
  kapıdan bağımsızdır; dayanak belge kanıt paketine bağlanır, onay ayrılığı ve yayım kuralları kaynakla aynıdır. Kapılar
  açılınca yeni `BalanceSourceKind` değerleri ve kendi komutları eklenir; elle giriş kalır.
- **Onay MOD-0023 üzerinden (O-4 önerisi):** domain-config "SoD kontrolü MOD-0023'ten miras alınır; modül kendi SoD
  kontrolünü icat etmez" der. MOD-0023 yalnız başlatanın onayını engeller (`WorkflowTaskTransitionSupport.cs:134-141`);
  MOD-0124 oluşturan ve son içerik değiştiren kuralını ve retteki gönderen kuralını ekler (kaynakta oluşturan kuralı vardı).
  Yerel maker-checker (kaynak davranışı, iş akışı kapalıyken) daha az bağımlılık ister ama domain kuralına aykırıdır.
- **Yayımın uzak çağrısız olması:** kaynaktaki tek riskli sıralama (yayım içinde dışa aktarma) dışa aktarmayı ayrı komuta
  ayırarak ortadan kalkar; yayım tek yerel işlem olur ve belirsiz sonucu yalnız Mongo commit belirsizliğidir — o da makbuzla
  çözülür.
- `target: 2027-02-15` planlama yer tutucusudur, sahip kararı değildir.

## Follow-up Items

- **Banka beslemesi kapısı (MOD-0269):** banka durumu ve ekstre girdileri — `BalanceSourceKind = BankStatement`, kaynak
  `IngestBankFeedCommand` / `BankBalanceSnapshot` uyarlaması, MOD-0269 sözleşmesi (D5'teki ekstre akışı açılınca, ayrı karar
  ve paket). O zamana kadar bu girdiler **karşılanmamış**.
- **Yönetişimli içe aktarma kapısı (MOD-0003 + MOD-0037):** CP-03 uyarlaması (`ImportCashPositionCommand`,
  `CashPositionImportBatch`, `CashPositionSourceRecord`), kaynak sözleşmesi doğrulama, entegrasyon koşusu sağlığı
  (`SourceHealthStatus = Partial/Failed`), içe aktarma tekrar indeksleri.
- Nakit kategori ana verisi ve atama kuralları (CP-13…CP-18) — içe aktarma ile birlikte (O-11).
- MOD-0289 banka hesabı arama/doğrulama → hesap seçimi, hesap sahipliği (LE) kontrolü, maskeli gösterim (O-15).
- Grup (çoklu LE) konsolidasyonu — LE↔LE ilişki modeli ve FX kaynağıyla (O-5, O-7).
- Yayım olayı ve tüketici sözleşmesi (MOD-0125/0126/0127) — tüketici onayı gelince (O-14).
- İstisna atama / inceleme / muafiyet akışı (O-13).
- MOD-0126 onaylı partilerin taahhütlü çıkış olarak gösterimi (O-6).
- Güvenilir servis kimliği (MOD-0033-FU02) gelince arka plan uzlaştırma süpürücüsü (İ2 tetikleyici; her değişiklik
  kurtarma komutlarıyla denetlenir).
- CentralAudit reddedilen komut kaydı (`Outcome = Denied`) — BL-501 sahibine (G-AUD-1).
- Yeniden yazması engellendiği (onay) ya da yeniden çağrısı kapalı olduğu (dışa aktarma, gap 5/G2) için `Unknown` kalan
  niyetin kalıcı kapatılması için açık iptal yolu (gap 4/G2) — sahip kararıyla ayrı komut.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | v1 pozisyon kaynağı | (a) elle bilinen bakiye (b) kapılar açılana kadar bekle | **(a)** — üç kapıdan bağımsız, kanıtla desteklenir | sahip (K1) |
| O-2 | MOD-0003 (karar 0.3) | tam modül / süreli sapma (T32) + dondurulmuş sözleşme | süreli sapma (T32), `cash-position.openapi.yaml` (domain-config önerisi) | sahip (K1) |
| O-3 | MOD-0037 Blueprint kapısı v1'de tüketilmeden başlanabilir mi | (a) evet, içe aktarmayla birlikte tüketilir (b) MOD-0037 uygulanana kadar bekle | **(a)** — v1 dış kaynak sağlığı üretmez; ölçüm: paket var, uygulama yok | sahip (K1) + platform-shared-services |
| O-4 | Onay yolu | (a) MOD-0023 iş akışı (b) yerel maker-checker | **(a)** — domain-config SoD kuralı | sahip (K1) |
| O-5 | Grup (çoklu LE) konsolidasyonu | v1 LE başına / LE↔LE modeli ile grup görünümü | **v1 LE başına**; model D9 gereği eklenmedi, kaynakta da yok | sahip (K1) + MDM |
| O-6 | MOD-0126 onaylı partileri taahhütlü çıkış olarak gösterme | v1 / sonra | **sonra** — partiler bankaya gitmiyor (D5), bakiyeyi değiştirmez; PAYMENT-BATCH sözleşmesi dondurulunca | sahip (K1) |
| O-7 | FX kaynağı, kur tarihi (as-of) ve çok para birimli görünüm | — | v1 kur çevrimi yok; kaynak **TBD** (Recovery'de kur kaynağı yok) | sahip + MOD-0048 sahibi |
| O-8 | Para birimi küsurat hanesi kaynağı | MOD-0048 eklemeli alan / başka | MOD-0048'e eklemeli alan (D9) — MOD-0126 O-11 ile ortak, **TBD** | MOD-0048 sahibi |
| O-9 | RCS-001: `CPS` (anlık görüntü) ve `CTP` (eşik politikası) önekleri, şirket kısa kodu, ortak servis | — | `CPS`, `CTP` (§3 tablosunda çakışma yok, ölçüldü; aynı dalda yazılan MOD-0119/0120 önerileriyle çakışma CT'de kontrol edilir); servis BL-565 | sahip + CT (`.antigravity` onayı) |
| O-10 | Raporlama saat dilimi ve değer tarihi kuralı (gelecek tarih) | LE alanı (MOD-0220 eki) / kiracı ayarı / yok | v1 çevrim yok; gelecek tarihli `ValueDate` reddi; alan gerekiyorsa MOD-0220'ye eklemeli öneri | sahip + MDM |
| O-11 | Nakit kategori ana verisi + atama kuralları (CP-13…CP-18) | v1 / içe aktarmayla | **içe aktarmayla**; v1 kaynak enum `CashCategoryType` | sahip (K1) |
| O-12 | Eşik politikası kapsamı | kaynak (kiracı geneli, para birimsiz, `IsDefault`) / LE + para birimi zorunlu | **LE + para birimi zorunlu**, varsayılan politika yok | sahip (K1) |
| O-13 | Engelleyici istisnanın atanması / muafiyeti | v1 / sonra | **sonra** — kaynakta da komut yok; v1'de düzeltip yeniden mutabakat | sahip (K1) |
| O-14 | Yayım olayı (`…published.v1`) | olay + tüketici sözleşmesi / okuma ucu | **v1 okuma ucu** (`current`); olay tüketici onayıyla | sahip + tüketiciler |
| O-15 | MOD-0289 gelene kadar banka referansı girişi | opak referans / DL-5'i beklet | opak referans alanı (MOD-0126 O-15 ile aynı) | sahip (K1) |
| O-16 | D10/1 (Active + Approved) MOD-0124'e uygulanır mı | evet / yalnız Active | **KARAR (D14/4): evet, yazma işlemlerinde** — nakit pozisyonu oluşturan/değiştiren her yazma komutu (iki kurtarma komutu hariç); okumalar etkilenmez | sahip (K1) |
| O-17 | Kanıt dışa aktarımı v1'de mi; yayımı engellesin mi | DL-4 ayrı komut / yok / yayımı engelleyen zorunlu mod | **DL-4 ayrı komut, yayımı engellemez**; MOD-0031 idempotency ölçümüne bağlı | sahip + platform-shared-services |
| O-18 | Bakiye işaret kuralları | — | Ledger/Available işaretli; Collected/Blocked/Restricted ≥ 0 | sahip (K1) |
| O-19 | İdempotency makbuz tipi | modül önekli / servis geneli ortak | **KARAR (D14/2): servis geneli tek ortak tip** `FinanceTreasuryCommandReceipt` (tanım MOD-0126 pack Entity Fields); niyetler modül önekli, durum sözlüğü ortak | sahip + CT |
| O-20 | MOD-0023 şablon kodu, adım sayısı, zaman aşımı | — | tek adımlı şablon; zaman aşımı 15 sn | MOD-0023 sahibi |
| O-21 | Yayımlayana ek SoD | yok / oluşturan yayımlayamaz | **yok** — ayrılık onayda | sahip (K1) |
| O-22 | Approve / Reject / Publish AUD-001 K2 mı | evet / hayır | **KARAR (D14/3): hayır** — Cash Positioning yayımı K2 değildir (para yetkisi değil); onay/ret de K2 sayılmaz; kayıt yine iş verisiyle aynı işlemde yazılır (`RequireEnlistment`) | CT + sahip |
| O-23 | SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (kapsamın kendisi **KARAR (D14/1)**: `LegalEntityId`, `read-tenant-wide` ayrı izin) | Platform kapsam ucu / servis içi çözücü kaydı | Platform sahibiyle kararlaştırılır; servis ortak tek karar (MOD-0126 O-19, MOD-0119 O-22, MOD-0120 O-14 ile aynı); `NoOp` kaydı yasak (kapsamı kapatır) | sahip + platform-shared-services |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | anlık görüntü çekirdeği, eşik politikası, makbuz ve denetim | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Backend File Convention, Ready-for-dev Checklist, Implementation Notes, Açık kararlar |
| DL-2 | mutabakat, istisna, kanıt paketi | Validation Rules |
| DL-3 | MOD-0023 onayı, SoD, yayım ve `current` okuma | Runtime Constraints, Failure Path to Verify, Authorization Convention |
| DL-4 | kanıt dışa aktarımı (MOD-0031) ve denetim kanıtı | Audited Events |
| DL-5 | iki kiracı ekranı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Follow-up Items |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events tablosunun ilk satırları DL-1'in ilk komutuyla gelir, bölüm DL-4'te son iki aşamalı komut, sabotaj
testi ve AUD-001 §8 dev kanıtıyla kapanır; Validation Rules'un alan kuralları DL-1'de, mutabakat ve kanıt kuralları DL-2'de,
SoD kuralları DL-3'te uygulanır; Acceptance Criteria ve Test Expectations her dilimde aşağıdaki AC kimlikleriyle ölçülür).

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: `Features/CashPosition/` içinde Create/Update/Delete/BulkDelete + List/ById sorguları,
  `Features/CashPositionThresholdPolicy/` tam seti, Domain/Persistence, makbuz kullanımı (`FinanceTreasuryCommandReceipt`,
  servis ortak — MOD-0126 DL-1), indeks hizmeti, MOD-0220 ve MOD-0048 bağdaştırıcılarının kullanımı (servis ortak,
  MOD-0126 DL-1'in), RCS kod portu, `cash-position.openapi.yaml`. **Neden denetim DL-1'de:** AUD-001
  K4 — ileticisi olmayan serviste yazma komutu yazılamaz. Önkoşul: Ready-for-dev engelleri (DCP, 0.3, O-3, port,
  **MOD-0126 DL-1** — iskelet ve servis ortak yapı taşları, tek sahibi o dilim, kod alma, TRM-14, O-9, O-8, O-23).
  Bitiş: AC-1, 2, 3, 4, 5, 8, 16 (Create/Update/Delete kısmı), 17 (tek aşamalı kısım), 18, 19 (Create), 21, 22, 28, 31,
  32 (Create / Update / Delete / politika kısmı).
- **DL-2** — Yollar: `ReconcileCashPositionCommand` + `CashPositionReconciler`, istisna sorgusu,
  `CreateCashPositionEvidencePackCommand`, trace sorgusu. **Neden ayrı:** kaynakla davranış eşitliği tablo güdümlü testle
  tek başına incelenebilsin. Önkoşul: DL-1. Bitiş: AC-6, 7, 9, 19 (mutabakat), 32 (Reconcile, kanıt paketi kısmı).
- **DL-3** — Yollar: Submit, Approve, Reject, `ReconcileCashPositionWorkflowCommand`, Publish, `GetCurrentCashPositionQuery`,
  MOD-0023 bağdaştırıcısı (zaman aşımlı). **Neden ayrı:** SoD ve belirsiz sonuç modülün en riskli kısmıdır. Önkoşul: DL-2,
  O-4, O-20, MOD-0023 şablonu, MOD-0023 pack §17 (TRM-15) onayı ve AC-W1/W2/W5/W6 geçişi (KARAR D21/1–4; gap 5/G2, gap 6/P1-a, P1-b). `PublishedContentHash` bu dilimde Publish ile gelir. Bitiş:
  AC-10…15, 16 (kararlar), 19 (yayım, karar), 23, 30 (a–c), 32 (Submit, kararlar, Publish kısmı), 33 (Submit ve kararlar
  kısmı), 35, 36 (Submit ve kararlar kısmı).
- **DL-4** — Yollar: `ExportCashPositionEvidenceCommand`, `ReconcileCashPositionEvidenceExportCommand`, MOD-0031
  bağdaştırıcısı, K2-olmayan sabotaj testi, dev ortamı denetim kanıtı. **Neden ayrı:** MOD-0031 davranış ölçümüne bağlı;
  DL-3'ü bekletmesin. Önkoşul: DL-3, O-17 (`links/query` ölçümü; dedupe ölçülmezse kurtarmada yeniden çağrı kapalı
  kalır, gap 5/G2). Bitiş: AC-16 (kalan), 17 (iki aşamalı + sabotaj), 20, 29, 30 (d),
  32 (ExportEvidence kısmı), 33 (ExportEvidence kısmı), 34, 36 (ExportEvidence kısmı).
- **DL-5** — Yollar: `Views/FinanceTreasury/{CashPositions,CashPositionThresholdPolicies}/**`, JS, resx (7 dil), MVC proxy,
  sayfa yardımı, nav anahtarları; Gateway rotası integration-agent task'ı olarak açılır (bu dilim yazmaz). Önkoşul: DL-1
  (liste/oluşturma), DL-3 (karar düğmeleri), Gateway rotası, O-15. Bitiş: AC-24…27.
