---
record_id: MOD-0124-DL2-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0124-cash-positioning.md (status draft, sha256 2f5344868f465524b24e3ce80d5d1b04da6aec6572f5814e621d72cc302379f5 (pack sahip denetimi 61466e504 KOŞULLU revizyonu, 2026-10-28 — MOD-0126 ortak tanımları yeniden sabitlendi; önceki sürüm `ea31ff55…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `55b6d936…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `2f079bfa…` (D58/D59 revizyonu); D57 sürümü `869c0419…`; D56 sürümü `f7debb3e…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `bb9e388a…` içindir), dal chore/ct/treasury-intake-ct-05 @ec063fee9, "Revizyon (2026-10-27, D54 — MOD-0126 yeniden sabitleme)"; MOD-0126 ortak tanımları 05410477…e42e (D58/D59; önceki 5f69139c…8a5d D57, 1baf8728…0916 D56, 5101b1e8…c88a); CT-APR-02 UYGUN, 0 ENGEL — execution/approvals/reviews.tsv:98; CT-APR-01 satırı yok)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5; DL-1 kaydıyla aynı commit)
---

> **Güncel durum (2026-10-27).** Pack `status: draft`; bu sürüme CT-APR-02 kaydı var (`reviews.tsv:98`, UYGUN, açık ENGEL 0 —
> dalda, main'de değil); CT-APR-01 yok. `services/Diten.FinanceTreasuryService/` yok — MOD-0126 DL-1 de MOD-0124 DL-1 de
> **başlamadı**. MOD-0124 DL-2 yalnız MOD-0124 DL-1 kabul edildikten sonra başlar. Satır atıfları `yol:satır@ec063fee9`; pack
> `ready-for-dev` revizyonunda değişirse o sürümde yeniden doğrulanır (S-53). `MOD-0124-DL1-hazirlik.md` ile birlikte okunur.
>
> **MOD-0124'te DL-2 iş akışı dilimi değildir.** Pack "Teslim dilimleri" (`:1708-1713`): DL-2 = **mutabakat, istisna, kanıt paketi**.
> MOD-0023 onayı, SoD, yayım ve `current` okuması **DL-3**; MOD-0031 kanıt dışa aktarımı **DL-4**; ekranlar **DL-5**. Bu kayıt DL-2'yi
> buna göre yazar; MOD-0023 şablonu, §17 kodu ve SoD DL-2'nin konusu değildir.

# MOD-0124 DL-2 hazırlığı — iş mutabakatı, istisna ve kanıt paketi

Bu kayıt, MOD-0124 DL-2 WP prompt'unun DL-1 kabul edildiği gün yazılabilmesi için **kaynaktan ne alınacağını**, **neyin
uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi, kuyruğu ve onay kayıtlarını değiştirmez.

**DL-2 kapsamı** (`MOD-0124-cash-positioning.md:1737-1739@ec063fee9`): `ReconcileCashPositionCommand` + saf domain hesaplayıcısı
`CashPositionReconciler` (tazelik, fark, para birimi), `GetCashPositionExceptionListQuery` (`GET /api/cash-positions/{id}/exceptions`),
`CreateCashPositionEvidencePackCommand` (`POST /api/cash-positions/{id}/evidence-packs`), `GetCashPositionTraceQuery`
(`GET /api/cash-positions/{id}/trace`). Neden ayrı: kaynakla davranış eşitliği tablo güdümlü testle tek başına incelenebilsin.
DL-2 tek bölümü kapatır: **Validation Rules** (alan kuralları DL-1'de, mutabakat ve kanıt kuralları DL-2'de, SoD R1–R7 DL-3'te — §5
bulgu 3). Ad notu (pack `:252`): `ReconcileCashPositionCommand` **iş** mutabakatıdır; iş akışı kurtarması
`ReconcileCashPositionWorkflowCommand` (DL-3) ve dışa aktarma kurtarması (DL-4) ayrı komutlardır.

## 1. Başlama koşulları (ölçüm: çalışma ağacı @ec063fee9, 2026-10-27)

Kaynak: DL-2 Önkoşul metni "Önkoşul: DL-1" (`:1739`); pack'te **"— DL-2 için" işaretli dilim kapısı yok** — dilim kapıları "— DL-3
için" (O-4, O-14, MOD-0023 şablonu, §17) ve "— DL-4 için" (O-17, MOD-0031 ölçümü) (`:1548-1557`). "DL-2'ye etkisi": **bekletir** /
**kabul kapısı** / **DL-3+** — MOD-0126 DL-2 kaydıyla aynı anlam.

| Koşul | Sahibi | Bugün | DL-2'ye etkisi |
|---|---|---|---|
| **MOD-0124 DL-1 kabul edildi** — DL-1 WP'si main'de, Bitiş AC'leri kabul kaydında (AC-8 eşik politikası dahil), Auth kayıt kodu main'de ve DL-1 testleri gerçek izinle (D53/2) | MOD-0124 DL-1 WP + CT | **yok** — servis yok; kuyrukta `WP-0124-DL1` PLANNED, `WP-0126-DL1`'e bağlı (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:23`) | bekletir |
| ↳ MOD-0126 DL-1 main'de (iskelet, servis ortak yapı taşları, indeks barındırılan hizmeti, CentralAudit) — MOD-0124 DL-1 üzerinden | MOD-0126 DL-1 WP | yok (`WP-0126-DL1` PLANNED) | bekletir (dolaylı) |
| ↳ Paket kapısı (DL-1 üzerinden): `ready-for-dev`, CT-APR-02/01, DCP-011, Auth paket metni (D53/1), `Finance = 18`, TRM-14, `read-tenant-wide` Auth tanımı, BL-565 (`CPS` / `CTP`) — paket kapısı değil, DL-1 kabul kapısı (D59/1), küsurat seed'i (O-8) | Beste / Ali / CT | CT-APR-02 **var** (`reviews.tsv:98`, dalda); CT-APR-01 yok; DCP-011 `status: draft`; `LegalEntityLookupDto` `ApprovalStatus` taşımıyor (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106`); `AuditEnums.cs`'te `Finance` 0; `appsettings.json`'da `currency-minor-units` 0; BL-565 `DURUM: AÇIK` | bekletir (dolaylı) |
| MOD-0220 `ApprovalStatus` (TRM-14) — AC-2 Reconcile ve kanıt paketi için (her yazmada Active + Approved, D14/4) | Ali | yok (yukarıda) | DL-1 üzerinden |
| Eşik politikası (`CashPositionThresholdPolicy`, LE + para birimi zorunlu, O-12) — Reconcile'ın girdisi | MOD-0124 DL-1 | DL-1'de yazılır (AC-8) | DL-1 üzerinden; politika yoksa 422 `CASH_POSITION_THRESHOLD_POLICY_MISSING` (AC-6) |
| O-13 engelleyici istisnanın atanması / muafiyeti | — | **kapandı** — D28: sonra; v1'de düzeltip yeniden mutabakat (`:1693`) | — |
| O-18 bakiye işaret kuralları | — | **kapandı** — D28 (`:1698`) | — |
| MOD-0031 / MOD-0262 (kanıt paketi kalemleri `EvidenceRefs`) | — | DL-2 MOD-0031'i ve belge deposunu **çağırmaz**: kalem yalnız biçimle doğrulanır (`DocumentId` Guid ≠ Empty, `DocumentVersionId` Guid?, `Kind` tanımlı; Validation Rules `:1072`); pack'te MOD-0262 bağımlılığı yok (grep 0) | — (bkz. §5 bulgu 5) |
| Auth kayıt kodu `finance-treasury.cash-positions.reconcile` / `.attach-evidence` / `.read` | Ali | yok (Auth'ta `finance-treasury` 0; MOD-0018 Treasury paketi yok) | DL-1 kabul kapısı (D53/2) üzerinden; DL-2 testleri gerçek izinle |
| MOD-0023 nakit pozisyonu şablonu (O-20), §17 kodu, O-4 / O-14 | Ali / — | **2026-10-28 (Y-12): kod `FT-CASH-POSITION-APPROVAL` geldi** (tek aşama / tek onay adımı / atamasız; adaylar `CandidatePrincipalIds`); şablonu kuran kod main'de değil, WP / tarih yok. Ölçüm anında: şablon **karar var, kod yok** — Y-6: nakit pozisyonu **tek adım** (`SOURCE-ANSWERS-2026-10-27.md:66-68`); §17 kodu yok (`WorkflowModels.cs:406-423`, `:451-465`) | **DL-3** — DL-2'yi bekletmez |
| MOD-0031 `links/query` ve dedupe ölçümü (O-17) | Treasury CT | ölçülmedi | **DL-4** |

**DL-2'yi ilk bekleten:** MOD-0124 DL-1'in kabul edilmemiş olması — zincir MOD-0126 DL-1 → paket kapısı (Auth paket metni,
`Finance = 18`, TRM-14). DL-2'nin **kendine özgü engeli yoktur**; DL-1 kabul edildiği gün başlayabilir. Ama bazı DL-2 AC'leri DL-3
komutları olmadan tam test edilemez (§5 bulgu 2).
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-2)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet `shasum -a 256` (ilk 16 hane) · satır `wc -l`, @da4abb20;
2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext archive da4abb20 services/DitenErpTreasuryService` ile (salt okunur) ölçüldü.
DL-1 kaydıyla ortak dosyalar (`ae5f9005…`, `ae0348b2…`, `483f3e4d…`, `74fed5b8…`, `715aab67…`, `63faa680…`, `33db0e1d…`, `04ec8dd4…`,
`b83cdaac…`) DL-1 ölçümüyle **aynı**; DL-2'ye özgü dosyalar ilk kez ölçüldü. Karar dili DL-1 kaydıyla aynı; "madde N" = pack
"Taşınmayacaklar" (`:1611-1640`).

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| mutabakat kuralları (tazelik, fark, para birimi; eşik 0 = kapalı) ve istisna güncelle / `Resolved` mantığı; `CashPositionException`, `CashPositionEvidencePack` tipleri; `ExceptionType` 3 değer, `ExceptionStatus` 2 değer; istisna ve kanıt paketi indekslerinin deseni; depo mutabakat / kanıt yazma kısmı; commit belirsizliği ayrımı (hata 112 etiketi) | iş mutabakatı servisi akışı, kanıt paketi servisi, komut sınıfları, kaynak iz (`source-lineage`) sorgusu, kaynak testleri | gövdeden `ExpectedVersion` / `ThresholdPolicyId` / `SnapshotVersion` / `PackVersion` / `MarkCurrent`, serbest İngilizce `RecommendedAction` / `ResolutionNote`, kayıttan sonra isteğe bağlı denetim yayıcısı, her istisnayı 400'e düşüren hata eşlemesi, 12 kaynak istisna türünün kalanı, `Assigned` / `InReview` / `Waived` / `Closed` durumları, `mod0124` rotaları ve adları |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/CashPositioning/CashPositionEntities.cs` | `ae5f900550a61fe8` · 229 | al-uyarla (yalnız `CashPositionException` :151-167, `CashPositionEvidencePack` :169-183) | `Domain/CashPositions/` | `TenantEntity` → `EntityBase`; `OwnerUserId` Guid?; `RecommendedAction` → `RecommendedActionKey` (resx), `ResolutionNote` → `ResolutionKey`; `WaiverReasonCode`, `EvidenceArtifactId`, `DueDate` **alma** (O-13); pakette `SnapshotVersion` → sunucu `SnapshotRevision` (= `ContentRevision`), `PackVersion` sunucu, `Exported` durumu **alma** (pack Entity Fields `:452-484`) |
| `S/src/…Domain/CashPositioning/CashPositionEnums.cs` | `ae0348b29c59ef8c` · 145 | al-uyarla (alt küme) | `Domain/CashPositions/` | `ExceptionType` :122-139 → `StaleBalance` (1), `Variance` (5), `CurrencyMismatch` (7), sayısal değer korunur; diğer 12 tür **alma**; `ExceptionStatus` → `Open` (0), `Resolved` (3) |
| `S/src/…Domain/CashPositioning/CashPositionDomainException.cs` | `2486720735886e73` · 12 | yalnız referans | — | Recovery hata sözlüğü `CASH_POSITION_*` |
| — | — | **yeni** | `CashPositionReconciler` (saf domain, birim testli — pack `:988`) | kaynakta kurallar servisin içinde (aşağıda); Recovery'de ayrı, yan etkisiz hesaplayıcı |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/CashPositioning/Services/CashPositionReconciliationService.cs` | `bbff068a7a7171e9` · 549 | al-uyarla (kurallar :162-241, istisna güncelle / `Resolved` :245-262) / yalnız referans (akış) | `CashPositionReconciler` + `ReconcileCashPositionHandler` | AC-6 tablo güdümlü testi bu satırların davranışını ölçer; `request.ExpectedVersion` :64 ve `request.ThresholdPolicyId` :75 **alma** (madde 11 — `If-Match` ve sunucu seçimi: oturum LE + para birimi + `ValueDate`'te geçerli tek politika); serbest İngilizce öneriler :111, :132, … ve `"Resolved by deterministic reconciliation rerun."` :252 **alma** (madde 13); kayıt :286 sonra isteğe bağlı (`null` olabilen) yayıcıyla denetim :320-330 **alma** (madde 5 — Recovery'de olay aynı işlemde, `RequireEnlistment`); genel `catch` → `ReconciliationFailed` :298-309 **alma** (commit belirsizliği ayrı 503, AC-19) |
| `…/Commands/ReconcileCashPositionCommand.cs` | `b3f29403332a9b78` · 27 | yalnız referans | `Commands/ReconcileCashPositionCommand.cs` + `Handlers/CommandHandlers/ReconcileCashPositionHandler.cs` | komut + handler aynı dosyada **alma** |
| `…/Services/CashPositionEvidenceService.cs` | `dd9ba2562538dfe8` · 136 | yalnız referans | `CreateCashPositionEvidencePackHandler` + `CreateCashPositionEvidencePackValidator` | istemciden `SnapshotVersion` (:47) ve `PackVersion` (:69) **alma** (madde 11, AC-9); `MarkCurrent` istekten (:86) **alma** — anlık görüntü başına tek geçerli paket sunucu kuralıdır (`ux_cash_position_current_evidence_pack`) |
| `…/Commands/EvidenceAndPublishCommands.cs` | `1613410a2e272af8` · 57 | yalnız referans (`CreateEvidencePackCommandHandler` :11-27) / DL-3 (`RecordApprovalDecision…` :28-44, `Publish…` :45-57) | — | — |
| `…/Contracts/CashPositionDtos.cs` | `74fed5b8977651b8` · 346 | al-uyarla (DL-2 DTO'ları) | `CashPositionModels.cs` → `CashPositionExceptionDto`, `CashPositionEvidencePackDto`, `CashPositionEvidencePackWriteRequest`, `CashPositionTraceDto` | istekte `SnapshotVersion` (:170) **alma**; `TenantId` / `LegalEntityId` yok |
| `…/Common/CashPositionErrorCodes.cs` | `483f3e4d2e9fb925` · 57 | al-uyarla (DL-2 alt kümesi) | `Features/CashPosition/` hata sözlüğü | `CASH_POSITION_THRESHOLD_POLICY_MISSING` (422), `CASH_POSITION_EVIDENCE_REVISION_CONFLICT` (409), `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN` (503), `CASH_POSITION_VERSION_CONFLICT` (409), `CASH_POSITION_PUBLISHED_IMMUTABLE` (409), `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE` (422); `MOD0124_*` öneki **alma** (madde 15) |
| `…/Queries/CashPositionCockpitReadQueries.cs`, `…/Contracts/CashPositionCockpitReadContracts.cs` | `a7665b82fe122a7f` · 332, `e88ea537b9c2cfde` · 143 | yalnız referans (`GetCashPositionSourceLineageQuery` :18-…) | `GetCashPositionTraceQuery` (CP-08 → `GET /api/cash-positions/{id}/trace`, pack `:226`) | Recovery izi: satır kaynağı + mutabakat + istisna + kanıt paketi (DL-2); karar ve niyet izi DL-3'te eklenir |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/CashPositioning/MongoCashPositionRepository.cs` | `33db0e1d3d8cf736` · 1618 | al-uyarla (DL-2 kısmı: `LoadReconciliationAggregateAsync` :188, `SaveReconciliationAsync` :240, `SaveEvidencePackAsync` :320, `GetCurrentEvidencePackAsync` :387, `GetExceptionsAsync` :541; iz için `GetSourceLineageAsync` :492 yalnız referans) | `Persistence/CashPositions/` | tek işlem: anlık görüntü (`Status → Reconciled`, `ReconciliationStatus`, `ContentRevision + 1`, `ContentUpdatedBy`, `AppliedThresholdPolicyId` + sürüm), satırlar, istisnalar, makbuz, olay; hata 112'de `UnknownTransactionCommitResult` etiketi ayrılır (kaynak mutabakat yolu `:291`, `:459` ayırıyor — korunur; yayım yolu `:663` ayırmıyor — madde 2, DL-3) → 503 `CASH_POSITION_COMMIT_OUTCOME_UNKNOWN` (AC-19 mutabakat kısmı) |
| `…/CashPositionMongoIndexes.cs` | `04ec8dd46aacfe18` · 263 | al-uyarla (desen) | indeks kataloğu | `ux_mod0124_active_exception` :207 → `ux_cash_position_open_exception` (kısmi `Status = Open`); `ix_mod0124_exception_queue` :220 → `ix_cash_position_exception_snapshot`; `ux_mod0124_current_evidence_pack` :231 → `ux_cash_position_current_evidence_pack` (kısmi `IsCurrent`) — pack `:553-555`; adlar `mod0124_` **alma** (madde 15) |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Api/Controllers/CashPositionSnapshotsController.cs` | `b83cdaaca2716400` · 272 | yalnız referans (`reconcile` :99, `evidence-packs` :119, `exceptions` :201-202, `source-lineage` :183-184) | `CashPositionsController.cs`: `POST /api/cash-positions/{id}/reconcile`, `POST …/evidence-packs`, `GET …/exceptions`, `GET …/trace` | `[Authorize]`-only (:14) **alma** — her aksiyonda `[HasPermission]`; `mod0124` / `cash-positioning/snapshots` rotaları **alma**; `evidence` :219 ve `approval` :237 GET'leri detaya katılır (CP-10 / CP-11) |
| — | — | — | — | DL-2'de **yeni bağdaştırıcı yok**: MOD-0023 (DL-3) ve MOD-0031 (DL-4) bağdaştırıcıları bu dilimde yazılmaz; LE / para birimi / veri kapsamı servis ortak (MOD-0126 DL-1) |

### 2.5 Testler (yalnız referans)

| Kaynak | Özet · satır | DL-2'de karşılığı |
|---|---|---|
| `S/tests/…/CashPositionReconciliationTests.cs` | `205f85b3f0647f2a` · 472 | AC-6 tablo güdümlü vakalar (tazelik sınırı `== BlockingFreshnessHours`, fark sınırı, para birimi uyumsuzluğu, eşik 0); AC-7 yeniden mutabakat |
| `…/CashPositioning/CashPositionReconciliationPersistenceTests.cs` | `adf1427e319040df` · 140 | istisna tekilliği, `Resolved` geçişi (AC-7) |
| `…/CashPositioning/CashPositionReconciliationTransactionTests.cs` | `d039626efa655e8e` · 118 | tek işlem + commit belirsizliği (AC-19 mutabakat) |
| `…/CashPositioning/CashPositionEvidenceRevisionTests.cs` | `e2fe08a72a1e2b99` · 146 | pakete revizyon bağı (AC-9) |
| `…/CashPositioning/CashPositionEvidenceRevisionTransactionTests.cs` | `4c7729b415fd9065` · 163 | tek geçerli paket yarışı (AC-9) |
| `…/CashPositionCockpitReadsTests.cs` | `f7e337092c9d410c` · 484 | iz / kaynak soyu okuması (trace) |
| `…/CashPositionLifecycleAuditEmissionTests.cs` | — (DL-1 kaydı) | **alma** — "denetim hatası birincil işlemi bozmaz" Recovery'nin tersidir |

### 2.6 Yetki ve roller — DL-2 (D46, D47, D49, D50)

Pack "Authorization Convention" (`:1197-1233`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-338`; D46 tablosu tarihçedir — D49/5).

- **DL-2 uçlarının anahtarları:** `finance-treasury.cash-positions.reconcile` (T3, iş mutabakatı), `.attach-evidence` (T3, kanıt
  paketi), `.read` (T1 — trace ve exceptions dahil; ayrı trace anahtarı yok), `.read-tenant-wide` (T3, yalnız açık atama, yalnız
  okuma). Bağımlı: `mdm.legal-entities.read` (AC-2). DL-2'de MOD-0023 izni gerekmez.
- **Test kişileri matristen** (`treasury-intake-owner-decisions-05.md:189-190`, `:268-275`): Reconcile ve kanıt paketi → yalnız
  **Nakit pozisyonu hazırlayıcı**; okuma / iz / istisna listesi → **Nakit pozisyonu görüntüleyici** (+ miras: hazırlayıcı, onaylayıcı,
  **Hazine yöneticisi**), Hazine konsolide görüntüleyici. Negatif testler: **Nakit pozisyonu onaylayıcı** ve **Hazine yöneticisi**
  `.reconcile` / `.attach-evidence` taşımaz → 403 (Hazine yöneticisi yalnız `workflow-reconcile` / `export-reconcile` kurtarma
  anahtarlarını taşır — iş mutabakatı değil).
- **SoD girdisi:** DL-2'de SoD kuralı yok (R1–R7 DL-3). Ama Reconcile **`ContentUpdatedBy`**'yi yazar (R3: son Update ya da
  Reconcile'ı yapan onaylayamaz ve reddedemez — pack `:374`, Validation Rules R3). Reconcile `ContentRevision`'ı da artırır; kanıt
  paketi ve sonraki onay bu revizyona bağlanır. Guid aktör (`sub`), sunucu yazar.
- **D50** MOD-0124'ü değiştirmedi; **D48** MOD-0289 içindir. **D47/4:** nakit pozisyonu rollerine banka okuması verilmez; `BankAccountRef`
  yalnız opak Guid (AC-5).
- **Denetim:** Reconcile ve kanıt paketi K2 değil (O-22, D14/3) — ama olay yine iş verisiyle aynı işlemde (`RequireEnlistment`);
  olaylar `finance-treasury.cash-position.reconciled`, `.evidence-pack-created` (pack Audited Events `:1261-1262`).

## 3. Kaynakta olmayan, DL-2'de yeni yazılacaklar

- `CashPositionReconciler` (saf, yan etkisiz) + politika seçimi (oturum LE + pozisyon para birimi + `ValueDate`'te geçerli tek politika;
  değerler anlık görüntüye sabitlenir: `AppliedThresholdPolicyId` + sürüm).
- Reconcile durum kuralları (Validation Rules "Durum kuralları" `:1082-1088`): yalnız `Status ∈ {Draft, Reconciled}`, en az bir satır,
  açık niyet yok, `ApprovalStatus ∉ {Submitted, PendingApproval}`; `Approved` iken Reconcile onayı düşürür (DL-3 alanı — §5 bulgu 2);
  `Published` / `Superseded` içerik değişmezliği → 409 `CASH_POSITION_PUBLISHED_IMMUTABLE` (`:1089-…`; yayım DL-3'te doğar).
- Yeniden mutabakat: satır + tür başına tek açık istisna (`ux_cash_position_open_exception`), koşul kalkınca `Resolved`, `ContentRevision`
  artar, önceki kanıt paketi geçersiz (AC-7).
- Kanıt paketi: sunucunun `ContentRevision`'ına bağlı (`SnapshotRevision`), `PackVersion` sunucuda artar, anlık görüntü başına tek
  geçerli paket (`IsCurrent`), kalemler 1..50, `Kind ∈ {Source, Reconciliation, Exception, Approval}`, ikili veri yok (AC-9).
- DL-1'in Update komutunun genişlemesi: Update `ContentRevision + 1`, `ReconciliationStatus = NotStarted`, **kanıt paketini geçersiz
  kılar** (pack Durum kuralları) — kanıt paketi DL-2'de doğduğu için bu dal DL-2'de bağlanır (Update kodu DL-1'indir; aynı feature
  klasöründe değişiklik).
- `GetCashPositionTraceQuery`, `GetCashPositionExceptionListQuery`; `cash-position.openapi.yaml` DL-2 uçlarıyla (sürüm artışı CT belirler).
- AC-19 mutabakat kısmı: `UnknownTransactionCommitResult` etiketli hata → 503, `VERSION_CONFLICT` değil; aynı anahtarla tekrar commit
  olmuş işlemde kayıtlı yanıt.

## 4. DL-2 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir. Hedef yol (öneri): `execution/domains/finance-treasury/work-packs/prompts/WP-0124-DL2-v1.0.md`.
Kuyrukta `WP-0124-DL2` satırı **yok** — bu kayıt kuyruğa yazmaz.

**Başlık satırları**

| Alan | Değer (taslak) | DL-2 başladığı gün doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — `/add-module` yok | — |
| WP / Prompt | `WP-0124-DL2` · `TRM-WP-0124-DL2-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0124-dl2` — **öneri — D51/3 / D52/1 deseni, sahip teyidi gerekir** | Expected HEAD = MOD-0124 DL-1 merge'ü ve kabul kaydı main'deyken alınan commit (S-53); worktree yolu |
| Risk | HIGH — kiracı, yetki, denetim, Mongo commit belirsizliği, kaynak davranış eşitliği | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı; MOD-0126 ortak tanımları sha ile sabit | sha |
| Kapsanan dilim | **yalnız DL-2** (mutabakat, istisna, kanıt paketi) — başlama koşulu yalnız DL-1 kabulü; pack'te "— DL-2 için" dilim kapısı yok; MOD-0023 / SoD / yayım DL-3, MOD-0031 dışa aktarma DL-4 — bu WP'ye girmez | — |

**DL-2 Bitiş AC'leri** (`:1739`): AC-6, 7, 9, **19 (mutabakat)**, **31 (iz, Reconcile ve kanıt paketi uçları kısmı)**, **32 (Reconcile,
kanıt paketi kısmı)**. AC metinleri pack'ten tırnakla alıntılanır. Kısmi AC'lerin kalanı: AC-19 yayım / karar, AC-31 / 32 Submit,
kararlar, Publish → DL-3; AC-31 ExportEvidence → DL-4 (tam kapanış). AC-2, AC-3, AC-16, AC-17'nin DL-2 komutlarına düşen kısmı pack'te
DL-2 listesinde yok (§5 bulgu 1); prompt bu davranışları Reconcile ve kanıt paketi için yine de test ettirmelidir (D14/4, D12/2,
AUD-001). AC-7 ve AC-9'un DL-3 komutlarına bağlı dalları için §5 bulgu 2.

**İzinli yollar** (pack Repo Scope `:618-640`): `services/Diten.FinanceTreasuryService/src/…Application/Features/CashPosition/**`,
`…Domain/CashPositions/**`, `…Persistence/CashPositions/**`, `…Api/Controllers/CashPositionsController.cs`,
`services/Diten.FinanceTreasuryService/tests/**` altında MOD-0124 klasörü; `docs/analysis/contracts/cash-position.openapi.yaml`.
`Features/CashPositionThresholdPolicy/**` DL-1'indir — DL-2 yalnız okur. `Infrastructure/{Workflow,Evidence}/**` DL-3 / DL-4'tür.

**Korunan yollar:** `.antigravity/**`, `AGENTS.md`, `gateway/**`, `services/Diten.Platform/**`, `services/Diten.AuthService/**`,
`services/Diten.MdmService/**`, `services/Diten.Building.Blocks/**`; servis ortak yollar (`Application/Common/{CommandReceipts,DataScope}/**`,
`Persistence/Common/CommandReceipts/**`, `Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`, indeks barındırılan hizmeti);
MOD-0126 / 0119 / 0120 feature yolları; `tests/architecture/audit-ledger/**` (yeni iz yok; `PinnedCounts` değişirse CT kararı, AC-18);
`frontend/**` (DL-5); `execution/**`; kaynak depo.

**Önce oku:** pack (`ready-for-dev` sürümü), MOD-0126 pack (ortak tanımlar), MOD-0124 DL-1 WP raporu ve kabul kaydı, AGENTS.md,
domain-config, `.antigravity/rules/` multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent,
code-style (S-56), audit-trail-standard, data-scope-enforcement, mongo-indexing, handler-design; karar kayıtları
`treasury-intake-owner-decisions-0{1..5}.md` (D14, D28 O-12 / O-13 / O-18, D47, D49), bu kayıt.

**Sözleşme yüzeyleri:** API `cash-position.openapi.yaml` (reconcile, evidence-packs, exceptions, trace; `Idempotency-Key`, `If-Match`);
tüketilen MOD-0220 TRM-14. İzin §2.6.

**Gerekli testler:** birim (`CashPositionReconciler` tablo güdümlü — kaynak `CashPositionReconciliationService.cs:162-241@da4abb20`
davranışı: tazelik `> Blocking` Critical / engelleyici, `> Warning` Medium, `|fark| > Critical` Critical, `> Warning` Medium, para birimi
uyumsuzluğu Critical, eşik 0 = kapalı; sınır değerleri `==`); **gerçek Mongo replica set** entegrasyonu (tek işlem, istisna tekilliği +
yeniden mutabakatta `Resolved`, tek geçerli paket yarışı, CAS 409, `UnknownTransactionCommitResult` enjeksiyonu → 503, kiracı / şirket
404, AC-32 yazma kapsamı, makbuz tekrarı, CentralAudit commit / rollback ve outbox sabotajı) — her takım kendi URI değişkenini okur,
varsayılan porta düşmez, replica set `enableTestCommands` ile; sahte MOD-0220; mimari (her aksiyonda `[HasPermission]`, banka alanı
yasağı, `float` / `double` yok, modül önekli makbuz tipi yok — AC-31); sözleşme testi; build 0 hata; yerel kapıda `scripts/` Python testleri.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş**: hedef servis + proje yolu; aggregate / entity + alanlar (`CashPositionException`,
  `CashPositionEvidencePack`; `CashPositionSnapshot` mutabakat alanları `ReconciliationStatus`, `ContentRevision`, `ContentUpdatedBy`,
  `AppliedThresholdPolicyId` / `…Version`); komut / sorgu listesi (Reconcile, CreateEvidencePack; exceptions, trace) ve
  `CashPositionReconciler`; validator kuralları (pack Validation Rules kanıt satırı ve Durum kuralları); izin anahtarları §2.6;
  `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 (S-52); `prompt_gate.py review-check` (S-54); HIGH iş — kabul kanıtı betiğini CT yazar (S-83); Expected HEAD DL-2
başladığı gün doldurulur (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22.

**Kod alma izi:** §2'deki **al-uyarla** satırları; her biri WP raporunda `path@da4abb20 + sha256`.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive` → kazı dizini); kaynak ve Recovery testleri **çalıştırılmadı**; Recovery'de
  yalnız dosya okuma ve `grep` (@ec063fee9).
- Auth kaydının, TRM-14'ün ve DL-1'in tarihi sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi):**

1. **Çok komutlu AC'ler DL-1'de "tam" kapanıyor.** AC-2 (Active + Approved; komut listesinde Reconcile ve kanıt paketi de var, `:1320-1325`)
   ve AC-3 (404 — "mutabakat, karar ve yayım" dahil, `:1326-1327`) DL-1 Bitiş'te kısmisiz sayılıyor (`:1734`) ama DL-2 / DL-3 / DL-4
   komutlarını anıyor; AC-16 (idempotency) DL-1'de "(Create/Update/Delete kısmı)", DL-3'te "(kararlar)", DL-4'te "(kalan)" — Reconcile ve
   kanıt paketi hiçbir dilimde adıyla yok; AC-17 (tek aşamalı denetim olayı) DL-1'de "(tek aşamalı kısım)" ve DL-4'te — DL-2'nin iki tek
   aşamalı komutu adıyla yok. Öneri: DL-2 Bitiş'e "2, 3, 16, 17 (Reconcile, kanıt paketi kısmı)"; DL-1'de AC-2 / AC-3 "(DL-1 komutları
   kısmı)".
2. **İki DL-2 AC'si DL-3 komutlarını gerektiriyor.** AC-9: "içerik değiştikten sonra eski paketle **Submit/Publish** 409
   `CASH_POSITION_EVIDENCE_REVISION_CONFLICT`" (`:1343-1345`) — Submit ve Publish DL-3'tedir. AC-7: "önceki kanıt paketi **ile onay**
   geçersiz olur" (`:1338-1340`) — onay DL-3'te doğar. Durum kuralları da "`Approved` iken Reconcile onayı düşürür" ve "yayım sonrası
   değişmezlik" diyor; DL-2'de `Approved` / `Published` kayıt oluşamaz. Öneri: AC-9 ve AC-7'yi "(paket / revizyon kısmı)" DL-2,
   "(Submit / Publish / onay kısmı)" DL-3 diye bölmek; prompt bu dalları DL-3'e bırakmalı, sahte durumla "geçti" saymamalı.
3. **Validation Rules DL-2'de "kapanıyor" ama SoD R1–R7 DL-3'te uygulanıyor** (`:1710`, `:1715-1718`; MOD-0120 ile aynı desen). Kabul
   kaydında Validation Rules DL-2'de kapandı sayılırsa R1–R7 açık kalır. Öneri: DL-2 "mutabakat ve kanıt kuralları kısmı" ya da bölümü DL-3'e
   taşımak.
4. **İstisna listesi ucu kapsam AC'lerinde adıyla yok.** AC-31 DL-2 kısmı "iz, Reconcile ve kanıt paketi uçları" (`:1739`); AC-3 ve AC-31
   metni "detay, iz, `current` ve her yazma" sayıyor — `GET …/exceptions` hiçbirinde yok. Kaynakta istisna kuyruğu `ix_mod0124_exception_queue`
   şirket süzgeciyle okunuyordu; Recovery ucunun kapsam dışı şirkette 404 vermesi açıkça test edilmeli (prompt'a eklenir).
5. **Kanıt paketi kalemlerinin kaynağı doğrulanmıyor.** `EvidenceRefs` "MOD-0031 / belge sürümü kimlikleri" (`:480`) ama Validation Rules
   yalnız biçim denetliyor (`:1072`); DL-2'de MOD-0031 / belge deposu çağrısı yok, DL-4 dışa aktarımı ise bu kimlikleri MOD-0031'e bağlar.
   Var olmayan belge kimliğiyle kilitli paket DL-4'e kadar fark edilmez. v1 için bilinçli ise pack'te "DL-2'de yalnız biçim, varlık DL-4'te"
   diye yazılmalı; değilse DL-2'ye MOD-0031 okuma bağımlılığı (ve dilim kapısı) eklenmeli.
