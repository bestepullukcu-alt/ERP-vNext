# WP-MVP1-AUDIT1 (v2) — BL-501 ortak merkezi denetim ileticisi · kanıt

- **Durum:** ajan sonucu, **CT bekliyor**. İlk tur: madde 1–5 ve 7 teslim, madde 6 BLOCKED. **CT HOLD sonrası** (§11): MVP-1 ucu birleştirildi, Abandoned / Unconfirmed testli, madde 6 karar A uygulandı.
- **Dal:** `feature/platform/bl-501-central-audit`, taban **9729409e0** (doğrulandı), yalıtılmış worktree
  `C:\Users\Ali\erp-vnext-wt-bl501`. Push yok, main'e commit yok; ana checkout'a ve MVP-1 dalına yazılmadı.
- **Filo:** 5000–5070 / 4010 / 27017'ye dokunulmadı. Sonda kendi portlarında: mongod 27191, Prism 4191, Platform 5191,
  Inventory 5192; tüm filo adresleri kapalı porta (127.0.0.1:9) yönlendirildi. Sırlar her çalıştırmada üretildi, dosyaya /
  loga yazılmadı (dev-secret kullanılmadı). `AccountKindAcceptanceHost.Tests` koşulmadı.

## 1. Commit'ler

| commit | konu |
|---|---|
| `ec8a33977` | ortak paket `Diten.BuildingBlocks.CentralAudit` (+ testler) |
| `01e27d2aa` | Platform alıcısı: `EventId` ile olay başına idempotency (yalnız ekleme) + gerçek host testi |
| `60ff50089` | işaret bağımlılıksız `…CentralAudit.Abstractions`'a; depolar için tek satırlık `CentralAuditTransaction` |
| `528cd415d` | ortak paket: başarısız komutun satırı `Abandoned`, `RequireEnlistment`, GUID'ler string |
| `3f08b287c` | InventoryService bağlama (58 komut) + mimari K4 öğretimi + defter |
| `b95974c85` | bağlanma kılavuzu + MVP-2 / MVP-3 / MVP-6 prompt taslakları |
| `686f8f4ed` | düzeltme: Platform testi `528cd415d` ile kalkan `Outcome` üyesine başvuruyordu (derleme) |
| `8703455c6` | düzeltme: Inventory kapsam bağdaştırıcısı adı iki talep yazımından da okur (sonda bulgusu) |
| `b5f703ed4` | test: teslim testi paylaşılan test DB'sinde yalıtıldı (tam suite bulgusu) |
| `ed3666bca` | düzeltme: `EnsureIndexesAsync` → `CreateIndexesAsync` (DB-010 korumasının yanlış pozitifi) |
| (bu commit) | kanıt + MOD-0173 §19 notu |

Diff-stat (`9729409e0..ed3666bca`): **64 dosya, +3679 / −80**.

## 2. Tasarım ve ölçümler (dosya:satır)

| madde | nerede | ne |
|---|---|---|
| 1a işaret | `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit.Abstractions/ICentralAuditDeclaration.cs:13,30` | `ICentralAuditDeclaration { CentralAudit; CentralAuditEntityId(object?) }` + `CentralAuditMetadata(Category, Operation, EntityType, SourceModule)` — 82ebedeb4 ile birebir ad/üye. Bağımlılıksız ayrı proje: Application katmanı Mongo/HTTP almadan işaretler (gerekçe). `:38` `ICentralAuditOutsideTransaction`. |
| 1b davranış | `…CentralAudit/CentralAuditBehavior.cs:29,69` | İşaretli + başarılı (istisna yok ve varsa `IsSuccessful == true`) → 1 olay; başarısız → 0. Gövde kopyalanmaz (yalnız kimlikler). |
| 1c kalıcı outbox | `…CentralAudit/CentralAuditRecorder.cs:26,123,143,180`, `CentralAuditOutboxStore.cs:58` | Servisin kendi DB'sinde `central_audit_outbox`. İşlem içindeyse `CentralAuditTransaction.EnlistAsync(s)` ile **aynı işlemde** (`AwaitingCommand`) yazılır, komut bitince onaylanır; işlem geri alınırsa satır da yok. İşlemi olmayan komut: komuttan hemen sonra (⚠ `:173` "THE GAP": komut ile outbox yazımı arasında çökme = kayıp; log hatası). Satır asla silinmez. |
| 1d gönderici | `…CentralAudit/CentralAuditDispatcher.cs:46,69,90,100`, `PlatformCentralAuditClient.cs:34,87` | `POST /api/internal/audit/append` + `X-Internal-Api-Key`; 2xx → Delivered; 400 → Rejected (kalır, hata logu: kiracı + eventId); diğer → Pending + üstel geri çekilme (uyarı logu: kiracı + eventId). Adres/anahtar yoksa: komutlar çalışır, outbox birikir, dakikada bir `NOT DELIVERING … N event(s)` uyarısı + `/health/central-audit` Degraded. Sahipsiz `AwaitingCommand` satırı 10 dk sonra serbest bırakılır (silinmez). |
| 1e Platform idempotency | `services/Diten.Platform/src/…/Contracts/Audit/AuditAppendRequest.cs:36`, `…/Features/Audit/AuditService.cs:68`, `…/AuditIdempotencyKeyBuilder.cs:54` | **Ölçüm:** alıcıda bileşik anahtarlı idempotency vardı (korelasyon+tür+varlık+işlem+sıra, `ux_audit_outbox_idempotency_key`), `eventId` yoktu; aynı korelasyonla gelen iki farklı olayı tek kayda indiriyordu (Inventory çağıranın `X-Correlation-Id`'sini kabul eder → kayıp). **Ekleme:** `EventId` varsa anahtar `audit:event:<id>`; yoksa eski davranış. |
| 1f DI | `…CentralAudit/CentralAuditServiceCollectionExtensions.cs:19`; `services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs:61,158` | Tek satır `AddCentralAudit<TScope>(configuration, sourceService[, options])`. Anahtarlar MDM ile aynı: `PlatformRegistration:BaseUrl` / `PlatformRegistration:InternalApiKey` (Platform'da `AuthService:InternalApiKey`); ince ayar `CentralAudit:*`. |
| 2 mimari | `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs:373,756,801`, `AuditTrailStandard/AuditTrailMeasurement.cs` (`SourceText`, `RegistrationEntryPoints`) | Tanıma ölçüldü: işaret + davranış Building.Blocks'ta zaten tanınıyordu; **kayıt** yalnız servis tiplerinde davranış adı aranıyordu (top-level `Program.cs` görünmüyordu, `AddCentralAudit` tanınmıyordu). Öğretildi: Building.Blocks davranışı için servisin tüm kaynağında davranış adı **ya da** onu kaydeden Building.Blocks `Add…` üyesi. Yeni koruma: Building.Blocks / Platform dışında `*AuditForward*` tipi ya da `/api/internal/audit/append` istemcisi → kırmızı; MDM (4) ve Auth (3) kopyaları adıyla istisna (FU-BL501-MIGRATE), bayat istisna da kırmızı. |

Tasarımdan ölçümle ayrılan üç nokta (CT kararına aykırı değil; daha sıkı):

1. **Başarısız ama işlemi commit olmuş komut** (işlem içinde "bulunamadı" dönüyor ya da çok işlemli komutun sonraki adımı
   düşüyor): satır silinmez, `Abandoned` olur ve gönderilmez — "başarısız komut → 0 merkezi kayıt" korunur.
2. **Idempotent tekrar** veri yazmadan başarı döner; Inventory `RequireEnlistment = true` ile çalışır: işleme kaydolmamış
   başarı = yazılmadı → kayıt yok (yoksa her tekrar ikinci bir "Create" kaydı). İşlemi olmayan tek komut açıkça işaretli.
   Unutulmuş işlem yolunu `CentralAuditDeclarationTests.Every_transaction_a_command_can_run_in_enlists…` yakalar (9 geri çağrı).
3. **GUID'ler string:** sürücü 2.x V2 modu `Standard` UUID'yi CSUUID yazıp filtrede eşleşmiyordu (onaylar satırı bulamadı).

## 3. Inventory komut envanteri (üretim kodundan çıkarıldı — 58)

Mimari ölçüm (`Inventory_PrintsTheTable…`): taban `Diten.InventoryService | 58 | a 0 | b 0 | c 0 | … | borç 58`,
sonra `58 | 0 | 58 | 0 | 0 | 0 | …`. Özellik dağılımı: Expiry 9 · Inventory 3 · Locations 4 · Quarantine 14 · Recall 22 ·
Traceability 6. Kategori hepsinde `MasterData` (Platform'da envanter kategorisi yok — açık konu 5). Kaynak modül = manifest kodu.

| # | özellik | komut | kaynak modül | işlem | varlık türü | varlık id | merkezi kaydın yazıldığı işlem |
|---:|---|---|---|---|---|---|---|
| 1 | Expiry | `CloseExpiryExceptionCommand` | EXPIRY-FEFO-MANAGEMENT | LifecycleTransition | ExpiryException | `ExceptionId` | ExpiryRepository.InTransactionAsync |
| 2 | Expiry | `CommentExpiryExceptionCommand` | EXPIRY-FEFO-MANAGEMENT | Update | ExpiryException | `ExceptionId` | ExpiryRepository.InTransactionAsync |
| 3 | Expiry | `CreateDateOverrideCommand` | EXPIRY-FEFO-MANAGEMENT | Create | LotDateOverride | `Id(Data<ExpiryDateOverrideDto>(response)?.OverrideId)` | ExpiryRepository.InTransactionAsync |
| 4 | Expiry | `CreateExpiryEvidenceExportCommand` | EXPIRY-FEFO-MANAGEMENT | Export | ExpiryEvidenceExport | `Id(Data<ExpiryEvidenceExportDto>(response)?.ExportId)` | ExpiryRepository.InTransactionAsync |
| 5 | Expiry | `OpenExpiryExceptionCommand` | EXPIRY-FEFO-MANAGEMENT | Create | ExpiryException | `Id(Data<ExpiryExceptionDetailDto>(response)?.ExceptionId)` | ExpiryRepository.InTransactionAsync |
| 6 | Expiry | `PrepareExpiryResolutionCommand` | EXPIRY-FEFO-MANAGEMENT | Update | ExpiryException | `ExceptionId` | ExpiryRepository.InTransactionAsync |
| 7 | Expiry | `ResolveExpiryExceptionCommand` | EXPIRY-FEFO-MANAGEMENT | LifecycleTransition | ExpiryException | `ExceptionId` | ExpiryRepository.InTransactionAsync |
| 8 | Expiry | `TakeExpiryExceptionCommand` | EXPIRY-FEFO-MANAGEMENT | Assign | ExpiryException | `ExceptionId` | ExpiryRepository.InTransactionAsync |
| 9 | Expiry | `UpdateExpiryPolicyCommand` | EXPIRY-FEFO-MANAGEMENT | Update | ExpiryPolicy | `Id(Data<ExpiryPolicyDto>(response)?.LegalEntityId)` | ExpiryRepository.InTransactionAsync |
| 10 | Inventory | `CreateReservationCommand` | INVENTORY-LEDGER | Create | InventoryReservation | `Id(Data<ReservationResponseDto>(response)?.ReservationId)` | InventoryReservationRepository.CreateAsync |
| 11 | Inventory | `PostMovementCommand` | INVENTORY-LEDGER | Create | InventoryTransaction | `Id(Data<MovementResponseDto>(response)?.TransactionId)` | InventoryLedgerRepository.PostAsync |
| 12 | Inventory | `ReleaseReservationCommand` | INVENTORY-LEDGER | LifecycleTransition | InventoryReservation | `Id(ReservationId)` | InventoryReservationRepository.ReleaseAsync |
| 13 | Locations | `BulkCreateBinsCommand` | INVENTORY-LEDGER | Create | Location | `Id(LocationId)` | LocationRepository.InTransactionAsync |
| 14 | Locations | `ChangeLocationStatusCommand` | INVENTORY-LEDGER | LifecycleTransition | Location | `Id(LocationId)` | LocationRepository.InTransactionAsync |
| 15 | Locations | `CreateLocationCommand` | INVENTORY-LEDGER | Create | Location | `Id(Data<LocationDetailDto>(response)?.LocationId)` | LocationRepository.InTransactionAsync |
| 16 | Locations | `UpdateLocationCommand` | INVENTORY-LEDGER | Update | Location | `Id(LocationId)` | LocationRepository.InTransactionAsync |
| 17 | Quarantine | `BlockStockCommand` | QUARANTINE-BLOCKED-STOCK | Create | BlockedStock | `Id(…FirstOrDefault()?.BlockedId)` | QuarantineRepository.InTransactionAsync (+ defter) |
| 18 | Quarantine | `CompleteBlockedReviewCommand` | QUARANTINE-BLOCKED-STOCK | LifecycleTransition | BlockedStock | `BlockedId` | QuarantineRepository.InTransactionAsync |
| 19 | Quarantine | `CreateBlockReasonCommand` | QUARANTINE-BLOCKED-STOCK | Create | BlockReason | `null` (kod, GUID değil) | QuarantineRepository.InTransactionAsync |
| 20 | Quarantine | `CreateDecisionCommand` | QUARANTINE-BLOCKED-STOCK | Create | QuarantineDecision | `Id(Data<DecisionDto>(response)?.DecisionId)` | QuarantineRepository.InTransactionAsync |
| 21 | Quarantine | `PrepareWorkflowTemplateCommand` | QUARANTINE-BLOCKED-STOCK | Create | WorkflowTemplate | `Id(…TemplateId)` | **yok** — MOD-0023'e HTTP; `ICentralAuditOutsideTransaction`, komut sonrası |
| 22 | Quarantine | `QuarantineStockCommand` | QUARANTINE-BLOCKED-STOCK | Create | QuarantineHold | `Id(…FirstOrDefault()?.HoldId)` | QuarantineRepository.InTransactionAsync (+ defter) |
| 23 | Quarantine | `ReapplyDecisionCommand` | QUARANTINE-BLOCKED-STOCK | Execute | QuarantineDecision | `DecisionId` | QuarantineRepository / defter / lot |
| 24 | Quarantine | `RefreshDecisionCommand` | QUARANTINE-BLOCKED-STOCK | Execute | QuarantineDecision | `DecisionId` | QuarantineRepository / defter / lot |
| 25 | Quarantine | `SaveSettingsCommand` | QUARANTINE-BLOCKED-STOCK | Update | QuarantineSettings | `Id(Data<SettingsDto>(response)?.LegalEntityId)` | QuarantineRepository.InTransactionAsync |
| 26 | Quarantine | `StartReviewCommand` | QUARANTINE-BLOCKED-STOCK | LifecycleTransition | QuarantineHold | `HoldId` | QuarantineRepository.InTransactionAsync |
| 27 | Quarantine | `SubmitDecisionCommand` | QUARANTINE-BLOCKED-STOCK | LifecycleTransition | QuarantineDecision | `DecisionId` | QuarantineRepository.InTransactionAsync |
| 28 | Quarantine | `UpdateBlockReasonCommand` | QUARANTINE-BLOCKED-STOCK | Update | BlockReason | `null` (kod) | QuarantineRepository.InTransactionAsync |
| 29 | Quarantine | `UpdateHoldCommand` | QUARANTINE-BLOCKED-STOCK | Update | QuarantineHold | `HoldId` | QuarantineRepository.InTransactionAsync |
| 30 | Quarantine | `WithdrawDecisionCommand` | QUARANTINE-BLOCKED-STOCK | LifecycleTransition | QuarantineDecision | `DecisionId` | QuarantineRepository.InTransactionAsync |
| 31 | Recall | `AddRecallScopeItemsCommand` | RECALL-READINESS | Update | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 32 | Recall | `ApproveRecallScopeCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 33 | Recall | `CancelRecallCaseCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 34 | Recall | `CloseRecallCaseCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 35 | Recall | `CompleteRecallEffectivenessCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 36 | Recall | `ConfirmRecallSegregationCommand` | RECALL-READINESS | Create | RecallSegregation | `Id(…SegregationId)` | RecallRepository.InTransactionAsync |
| 37 | Recall | `DeclareRecallConsumptionCommand` | RECALL-READINESS | Create | RecallConsumptionDeclaration | `Id(…DeclarationId)` | RecallRepository.InTransactionAsync |
| 38 | Recall | `DiffRecallScopeCommand` | RECALL-READINESS | Execute | RecallCase | `Id(CaseId)` | RecallRepository.InTransactionAsync |
| 39 | Recall | `ExportRecallTraceCommand` | RECALL-READINESS | Export | Lot | `Id(RootLotId)` | RecallRepository.InTransactionAsync |
| 40 | Recall | `LinkRecallReturnCommand` | RECALL-READINESS | Create | RecallReturnLink | `Id(…LinkId)` | RecallRepository.InTransactionAsync |
| 41 | Recall | `MarkRecallScopeItemsCommand` | RECALL-READINESS | Update | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 42 | Recall | `OpenRecallCaseCommand` | RECALL-READINESS | Create | RecallCase | `Id(…CaseId)` | RecallRepository.InTransactionAsync |
| 43 | Recall | `PrepareRecallCaseActionCommand` | RECALL-READINESS | Update | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 44 | Recall | `RecordRecallNotificationCommand` | RECALL-READINESS | Create | RecallNotification | `Id(…NotificationId)` | RecallRepository.InTransactionAsync |
| 45 | Recall | `RequestRecallQuarantineCommand` | RECALL-READINESS | Create | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 46 | Recall | `RetryRecallQuarantineCommand` | RECALL-READINESS | Execute | RecallQuarantineRequest | `RequestId` | RecallRepository.InTransactionAsync |
| 47 | Recall | `ReturnRecallScopeCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 48 | Recall | `ReviseRecallScopeCommand` | RECALL-READINESS | Update | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 49 | Recall | `RunRecallTraceCommand` | RECALL-READINESS | Execute | Lot | `Id(RootLotId)` | RecallRepository.InTransactionAsync |
| 50 | Recall | `StartRecallEffectivenessCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 51 | Recall | `SubmitRecallScopeCommand` | RECALL-READINESS | LifecycleTransition | RecallCase | `CaseId` | RecallRepository.InTransactionAsync |
| 52 | Recall | `UpdateRecallSettingsCommand` | RECALL-READINESS | Update | RecallSettings | `Id(…LegalEntityId)` | RecallRepository.InTransactionAsync |
| 53 | Traceability | `ApplyApprovedLotStatusChangeCommand` | LOT-SERIAL-TRACKING | LifecycleTransition | Lot | `LotId` | LotRepository.InTransactionAsync |
| 54 | Traceability | `ChangeLotStatusCommand` | LOT-SERIAL-TRACKING | LifecycleTransition | Lot | `Id(LotId)` | LotRepository.InTransactionAsync |
| 55 | Traceability | `CommitManualLotMigrationCommand` | LOT-SERIAL-TRACKING | Execute | ManualLotMigration | `Id(…MigrationId)` | ManualLotMigrationRepository.CommitAsync |
| 56 | Traceability | `CreateLotCommand` | LOT-SERIAL-TRACKING | Create | Lot | `Id(…LotId)` | LotRepository.InTransactionAsync |
| 57 | Traceability | `LinkGenealogyCommand` | LOT-SERIAL-TRACKING | Create | GenealogyLink | `Id(…LinkId)` | LotRepository.InTransactionAsync |
| 58 | Traceability | `UpdateLotCommand` | LOT-SERIAL-TRACKING | Update | Lot | `Id(LotId)` | LotRepository.InTransactionAsync |

**İşlem kayıt noktaları (9):** `Persistence/Expiry/ExpiryRepository.cs:202`, `Quarantine/QuarantineRepository.cs:229`,
`Recall/RecallRepository.cs:263`, `Repositories/InventoryLedgerRepository.cs:181`, `Repositories/InventoryReservationRepository.cs:50,89`,
`Repositories/LocationRepository.cs:159`, `Traceability/LotRepository.cs:204`, `Traceability/ManualLotMigrationRepository.cs:118`.
Arka plan süpürücüleri (RetestHold, ReservationExpiry, SkuPositionRebuilder, LotExpiry) komut değildir — ölçüm dışı (AUD-001 §9), adıyla.

**R4 komutları:** taban `9729409e0`'da R4 dalı (`feature/sce/mod-0177-r4`) bu tabanda, yeni komut yok → **0**. R4 yeni komut
eklerse birleştirmede işaretsiz gelir ve mimari (`EveryWriteCommand…`) + `CentralAuditDeclarationTests` (58 sabit) kırmızı olur:
o komutlara işaret + (işlemi varsa) zaten kayıtlı depo yolu yeter.

Ürün içi geçmişler **aynen**: `inventory_audit`, karantina geçmişi, recall zaman çizelgesi, expiry olayları, lot / lokasyon
geçmişi — hiçbir satırı kaldırılmadı (`CentralAuditInventoryTests.A_posted_movement…` defter denetim satırının hâlâ yazıldığını doğrular).

## 4. Defter

`tests/architecture/audit-ledger/Diten.InventoryService.md` (README biçimi): iz `inventory-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`; istisna 0, dolaylı 0, borç 0, K2 borcu 0, yazan sorgu 0. `PinnedCounts["Diten.InventoryService"] = (0, 0)`,
`AcceptedTrails` satırı WP (CT) yönergesine dayanır ("komutlar yol b ile denetleniyor sayılmalı").
`git diff 9729409e0..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` → **boş** (deftere satır eklenmedi).
CT-DECISIONS.md'ye yazılmadı.

K2: 57 komutun merkezi kaydı iş verisiyle aynı Mongo işleminde (kapalı-başarısız: kayıt yazılamazsa işlem geri alınır, veri de
yazılmaz). İşlemi olmayan tek komut Inventory verisi değiştirmez → K2 borcu yok; CT kararı gerektiren sayı artışı **yok**.

## 5. Madde 6 — BLOCKED

**BLOCKED — ölçüm WP'nin varsayımıyla çelişiyor; kod yazılmadı.**

| WP | ölçüm (taban 9729409e0, `EveryWriteCommand…` tam listesi) |
|---|---|
| "Platform 3 → yol a" | Platform'da denetimsiz-yeni **18** komut, hepsi `Features/ESignature/ESignatureRequests.cs` (MOD-0022). Ayrıca 5 aynı adlı çift (`VerifySignatureCommand`, `InvalidateSignatureCommand`, `CreateSignatureRequestCommand`, `RejectSignatureRequestCommand`, `CancelSignatureRequestCommand`; `ESignature/` ve `DocumentManagementElectronicSignature/`) — defter adı ayırt edemiyor. |
| Platform yol a | Bu komutlar **zaten işlem içinde merkezi niyet yazıyor**: `ESignatureAuditIntent.EnqueueAsync` (`Features/ESignature/Services/ESignatureServices.cs`, `ITransactionalAuditOutboxWriter`, iş işlemiyle aynı). Yol a (`AuditBehavior`) eklemek ikinci, **en iyi çaba** kayıt üretir (çift kayıt) ve e-imza GxP/K2 sınıfı olduğundan yeni **K2 borcu** demektir. AUD-001 §4.4: "K1 kapanana kadar yeni K2 sınıfı Platform komutu = engelli, CT'ye yaz"; "Yol a ile yazıp 'sonra düzeltiriz' demek yeni K2 borcu üretir". |
| "Auth 3 → mevcut b ileticisi" | 3 komut ölçüldü: `ConsumeESignTicketCommand`, `IssueESignWindowTicketCommand`, `ReauthenticateForESignCommand` (`Features/ESignReauth`). Kimlik + e-imza → K2 sınıfı. Mevcut b ileticisi (`IUserAuditRecorder.RecordAsync`) hatayı yutar (en iyi çaba) → K2 borcu = deftere satır = CT kararı. AUD-001 §4.4: önce-yaz kalıbı (`IAuthAuditService.WriteOnceAsync`) **bu dalda yok** → "yeni K2 sınıfı Auth komutu = engelli, CT'ye yaz". |

Seçenekler (CT):

- **A (önerilen).** Platform 18: `ESignatureAuditIntent.EnqueueAsync` için `## İzler`'e işlem-içi bir **yazıcı** izi
  (`ESignatureAuditIntent.EnqueueAsync`, yol `a`) — CT kabulü + `AcceptedTrails`; önce bu yolun teslim ettiğini (DeadLetter değil) dev
  kanıtıyla göstermek (K1'deki 7/7 DeadLetter `RegisterModuleManifestCommand` içindi; e-imza yükü `BuildPayload` anahtarlarını taşıyor —
  sondada `esign.actions.registered` 3/3 `audit_events`'e düştü). 5 aynı-ad çift ayrı iş (ad değişikliği). Auth 3: `WriteOnceAsync`
  önce-yaz kalıbı gelene kadar CT'nin yazılı K2-borcu kararı (3 satır + `PinnedCounts.K2Debt` 8 → 11) ile mevcut b ileticisi.
- **B.** Madde 6 MVP-1 kapsamından çıkar: 18 + 3 komut ayrı WP (MOD-0022 sahibi), FU-0022-AUD-01 açık kalır.

## 6. Sabotaj tablosu

Her biri: özgün baytlar kopyalandı → bozuldu → hedef testler **kırmızı** → özgün baytlar geri yazıldı (SHA eşit) → dosya `touch`
(artımlı derleme; ilk turda geri yazılan dosya eski zaman damgasını taşıdığı için sabotajlı DLL kalmıştı — betik düzeltildi, tur baştan
koşuldu) → aynı testler **yeşil**. Derleme hatası 0 (kırmızı gerçek davranıştan).

| # | sabotaj | kırmızı | geri | SHA |
|---|---|---|---|---|
| s1 | davranış başarısız komutta da yazsın (`CentralAuditBehavior.cs`) | `A_failed_marked_command_leaves_nothing(AnswersFailure)` — 13/14 | 14/14 | f8b4f8349f99 = |
| s2 | işlem içi yazım işlem dışına (`InsertInTransactionAsync` → `InsertAsync`) | Inventory `An_aborted_ledger_transaction_leaves_no_event` — 10/11 | 11/11 | 79b9a441b399 = |
| s3 | göndericide başarısızlıkta satırı sil (`MarkRetryAsync` → `DeleteOne`) | Inventory `Platform_failing_keeps_the_row_and_a_later_success_marks_it_delivered` — 10/11 | 11/11 | 8fe81dfc299f = |
| s4a | Platform `EventId` dalı kapalı (`AuditService.cs`) | `Two_events_that_share_correlation_type_entity_and_operation_are_two_records` — 3/4 | 4/4 | 6e336c323a09 = |
| s4b | Platform yinelenme yakalaması kaldırıldı (`AuditOutboxRepository.TryInsertAsync`) | `The_same_event_id_sent_twice_is_one_record_and_both_sends_are_accepted` (502) — 3/4 | 4/4 | 64418cc0495f = |
| s5 | `StartReviewCommand`'dan işaret kaldırıldı | mimari `EveryWriteCommand…` 65 → **66** (`StartReviewCommand — YENİ KOMUT DENETİMSİZ GELDİ`) + Inventory `CentralAuditDeclarationTests` 2/4 | 65 (taban kırmızısı) / 4/4 | ba44e9af57a1 = |
| s6 | Inventory'de iletici kopyası (`InventoryAuditForwarder` + `/api/internal/audit/append`) | `CentralAuditForwarder_LivesOnlyInBuildingBlocks…` (tip + dosya) | yeşil | 06c57a088257 = |
| s7 | olayda kiracı başka kimlikten (`TenantId = CompanyId`) | Inventory 5/11 (`A_posted_movement…`, `Each_tenants_event…`, …); Building.Blocks 3/14 (`The_event_is_filed_under_the_callers_tenant…`) | 11/11, 14/14 | 79b9a441b399 = |

## 7. Sayılar (taban `9729409e0` kendi ölçümüm → sonra `ed3666bca`)

| suite | taban | sonra | not |
|---|---|---|---|
| Inventory | 1143 ✓ / 16 ✗ / 4 atlanan; Prism tek başına 16/16 → **1159 + 4** | 1158 ✓ / 16 ✗ / 4; Prism tek başına 16/16 → **1174 + 4** | 16 kırmızının 16'sı `PrismConsumedContractSmokeTests` (zaman aşımı); +15 yeni test |
| Platform Application | 5804 ✓ / 260 ✗ (6064) | 5808 ✓ / 260 ✗ (6068) | kırmızı küme **ad ad aynı** (yeni 0, giden 0); +4 yeni (`CentralAuditAppendMongoTests`) |
| Building.Blocks CentralAudit | — | 28/28 | yeni |
| Building.Blocks Secrets | 5/5 | 5/5 | |
| Mimari | 69/77 (8 ✗) | **71/79 (8 ✗)** | kırmızı küme ad ad aynı (aşağıda); +2 yeni test yeşil |
| Auth Application | 1152/1154 | 1152/1154 | 2 kırmızı ad ad aynı (`CompositionRootGuardTests` ×2) |
| Gateway | 164/165 | 164/165 | aynı (`EveryRoute_DownstreamPortIsInKnownServiceSet`) |
| Web | 2272/2272 | 2272/2272 | |
| Derleme | Inventory sln 0/0, Platform API 36 uyarı / 0 | Inventory sln 0/0, Platform API 36 / 0, CentralAudit 0/0, testleri 0/0 | Platform 36 uyarı tabanda da 36 |

Mimari 8 kırmızı (taban = sonra): `AuditTrailStandardTests.EveryWriteCommand…` (denetimsiz-yeni **123 → 65**, −58 Inventory),
`AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger…` (**7 → 6**: Inventory defteri geldi; kalan SupplyChain + 5 Platform
çift adı), `DocsPathGuardTests` ×5, `MongoTestDatabaseGuardTests.PerRunDatabaseExceptionListStaysHonest`. Ara turda görülen
`NoTestBuildsThePlatformSchema` (benim test deposumun `EnsureIndexesAsync` adı) `ed3666bca` ile giderildi; istisna listesine satır eklenmedi.

## 8. Uçtan uca sonda (kendi portlar, kendi mongod)

`scratchpad/e2e/run-e2e.ps1` + `probe/` (yerel). Platform ortam adı `Bl501Probe` (yalnız `appsettings.json` + ortam değişkenleri);
Inventory `Mock`. Ölçüm: Platform `BackgroundJobs:Enabled=false` iken açılmıyor (`EmailDispatchSweepJob` kayıtlı, `IBackgroundJobScheduler`
değil; ValidateOnBuild) — taban kodun davranışı; sonda kendi depolamasıyla `Enabled=true` verdi.

```
replica set primary ready
OK   stock movement posted -> 201
OK   Inventory outbox row exists, written in the ledger transaction
OK   Inventory outbox row Delivered
OK   Platform audit read endpoint returns the record
  kim:      actorId=bf54f9ba-8b52-4198-9dbd-c70da456ff20 actorType=TenantUser ad(maskeli)=A***i
  ne:       MasterData/Create InventoryTransaction 96ee9694-0b52-4cdf-a57e-0e992830f40f (PostMovementCommand) outcome=Succeeded kaynak=Diten.InventoryService/INVENTORY-LEDGER
  ne zaman: 2026-10-14T15:43:30.706+00:00
  kiracı:   074b474e-afc1-4667-8f43-f757ae173840  korelasyon: 55b92959-b068-45a0-b2c5-939e0d993d33
OK   record: tenant = caller's tenant
OK   record: actor = caller
OK   record: entity = posted transaction
OK   tenant user cannot read the central log (401/403)
8/8 checks passed
health /health/central-audit: başlangıçta Healthy, sonda Healthy · Platform /health/live 200
```

Okuma: `GET /api/platform/audit/events?entityType=InventoryTransaction&entityId=…` (platform yöneticisi). Aynı uç kiracı kullanıcısına 403.
Platform `audit_events`: 4 × `CreateLocationCommand` + 1 × `PostMovementCommand` (Inventory) + 3 × `esign.actions.registered`.
Platform'un kendi `SeedDefaultSubscriptionPlansCommand` / `RegisterModuleManifestCommand` satırları DeadLetter (`Field=TenantId … Required
guid missing`) — AUD-001 K1'de kayıtlı bilinen sorun, bu işle ilgisiz.

İlk sonda turu bir hata buldu ve düzeltildi (`8703455c6`): ad `s***m` ("system" maskesi) düşüyordu — Inventory JWT işleyicisi
`given_name`/`family_name`'i `ClaimTypes.*`'a eşliyor.

## 9. Kılavuz ve prompt taslakları

`docs/guides/operations/central-audit-connect.md` (docs-organization §1 soru 4). Servis notları ölçümle:

- **MVP-3:** 82ebedeb4'teki bildirimler **bu dalda yok** — `730a7fa1f` ("defer BL-501 audit connection after MVPs") geri almış; WP'nin
  "bu dalda var" ifadesi ölçümle uyuşmuyor (bloklamıyor: biçim 82ebedeb4'ten birebir alındı). 4 komut; tek işlem `BomHistoryJournal.CommitAsync`.
- **MVP-2 Procurement:** 35 komut (hepsi borç), **0 Mongo işlemi** → `RequireEnlistment` yok, komut-sonrası yol (kayıp riski yazılı).
- **MVP-6 SupplyChain:** 17 komut, **defter yok**; 15 `WithTransactionAsync`; zarflarda `IsSuccessful` **yok** (4xx başarılı sayılır —
  önce eklenmeli); özellik başına `*RequestContext`.

Üç "bağlan" prompt taslağı kılavuzun sonunda.

## 10. Açık konular / riskler

1. Madde 6 BLOCKED (§5).
2. İşlemi olmayan servislerde (Procurement) komut ile outbox arası çökme = kayıp (log hatası); Inventory'de yalnız `PrepareWorkflowTemplateCommand`.
3. Bir komut içinde **paralel** iki işlem aynı olayı aynı anda kaydederse ikinci commit tekil anahtar çakışmasıyla iş işlemini düşürür;
   Inventory'de paralel işlem ölçülmedi (hepsi sıralı). Kılavuza not.
4. MDM (4 tip) ve Auth (3 tip) iletici kopyaları adıyla istisna — **FU-BL501-MIGRATE**. Auth/MDM'nin "anahtar yoksa sessiz" (K8) davranışı
   taşınana kadar sürer; ortak paket K8'i kendi kullanıcıları için kapatır.
5. Platform `AuditCategory`'de envanter / GxP-operasyon değeri yok → Inventory `MasterData`. Ayrı kategori = Platform enum + Web 2 dil
   etiketi + CT kararı.
6. Okuma tarafı yalnız platform yöneticisi (AUD-001 K6 açık).

## 11. CT HOLD sonrası (2026-10-14) — üç düzeltme + madde 6 kararı A

### 11.1 Commit'ler

| commit | madde |
|---|---|
| `b9c113b54` | 1 — MVP-1 ucu `394c05550` merge (`--no-ff`, rebase yok; R4 + FIX-QUI1). Çakışma yok. |
| `29c7941ab` | 1 — R4 `PrepareRecallSettingsChangeCommand` işaretlendi (`ForRecall("Create", "RecallSettingsChange")`, varlık = yanıttaki değişiklik kimliği). Yazması `RecallRepository.InTransactionAsync` içinde (zaten kaydolan geri çağrı); yeni işlem noktası yok (13 = 9 komut + 4 süpürücü). Envanter 58 → **59**; Inventory defter notu ve `CentralAuditDeclarationTests` sabiti 59. |
| `6e2e9c127` | 2 + 3 — Abandoned gerçek Mongo testi; sahipsiz satır `Unconfirmed` |
| `c2f1a487a` | 4 — CT-DECISIONS (metin aynen), Platform izi, Auth K2 borcu 8 → 11, Auth bağlantısı |
| `28b357fe0` | 4 — Auth olay etiketleri (Web en/tr) + MOD-0022 §20 FU kaydı |
| (bu commit) | kanıt eki + MOD-0173 §19 satırı |

Diff-stat `394c05550..28b357fe0` (MVP-1 ucuna göre bu dalın katkısı): **76 dosya, +4126 / −87**.

### 11.2 Madde 2 — başarısız komut kuralı artık testli (gerçek Mongo)

`CentralAuditInventoryDeliveryTests.A_failed_command_whose_transaction_committed_leaves_an_abandoned_row_that_is_never_sent`:
var olmayan bir rezervasyonu (geçerli UUID, `If-Match: W/"1"`) serbest bırakmak `InventoryReservationRepository.ReleaseAsync`
işlemini açar (olay kaydolur), "bulunamadı"yı işlem içinde döner ve işlem **commit olur**; komut 404. Satır `Abandoned`;
`NextAttemptAtUtc` en başa çekilse de `GetDueAsync` (dispatcher'ın gönderdiği tek kaynak) onu döndürmez; betikli Platform'a 0 gövde.
**s8** (`AbandonAsync` → `Pending`) bu testi kırmızı yapar, geri alınca yeşil.

### 11.3 Madde 3 — sahipsiz satır "doğrulanmamış" gider

Ölçüm: `AuditAppendRequest.Outcome` (`AuditOutcome`: Unknown 0, Succeeded 1, Failed 2, Denied 3) var; "doğrulanmamış" karşılığı **yok**.
Eklemeli: `AuditOutcome.Unconfirmed = 4` (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs`). Eski çağıranlar
değişmez (varsayılan Succeeded). Platform lookup'ı enum'dan üretildiği için değer listede kendiliğinden çıkar; Web ham adı gösterir.
Paket: `CentralAuditOutboxRecord.ConfirmedByCommand` (varsayılan true); `ReleaseOrphansAsync` false yapar; istemci `Outcome = 4`
ve `metadata.confirmedByCommand = false` gönderir. Testler:

- Inventory (gerçek Mongo) `An_orphan_row_is_released_and_delivered_as_unconfirmed_never_as_succeeded` → gövde `"outcome":4`, `1` yok;
  onaylı satır (teslim testi) `"outcome":1`, `"confirmedByCommand":true`.
- Platform gerçek host `An_unconfirmed_row_is_filed_as_unconfirmed_and_a_confirmed_row_as_succeeded` → `audit_events.Outcome`
  `Unconfirmed` / `Succeeded`.
- **s9** (`ReleaseOrphansAsync`'ten `ConfirmedByCommand=false` kaldırıldı) → Inventory testi kırmızı, geri yeşil.

### 11.4 Madde 4 — CT kararı A

- `tests/architecture/audit-ledger/CT-DECISIONS.md`: CT metni aynen (komut adları yerine konarak), tabloya satır.
- Platform: `## İzler` → `esign-islem-ici-denetim | a | yazıcı | ESignatureAuditIntent.EnqueueAsync`; `AcceptedTrails` satırı.
  Yol a işareti eklenmedi. **Ölçüm: 18 komutun 16'sı izden kredi alıyor** (Platform yol a 280 → 296).
  ⚠ **CT'ye soru:** `RegisterSignableActionsCommand` ve `VerifySignatureCommand` merkezi kaydı işlem **dışında**
  `IAuditService.AppendAsync` ile yazıyor (`Features/ESignature/Handlers/CatalogHandlers.cs` ~:310, `SignatureHandlers.cs` ~:590).
  Karar metni "işlem içinde yazdığı" diyor; bu ikisi girmiyor. Defter satırı / iz uydurulmadı, ikisi taban kırmızısında kalıyor.
  Seçenek A: ikisini `ESignatureAuditIntent.EnqueueAsync` ile işlem içine taşımak (MOD-0022 sahibi). Seçenek B: `IAuditService.AppendAsync`
  için dar bir yazıcı izi, CT kararıyla (geniş belirteç: her Platform handler'ına kredi verme riski).
- Auth: 3 komut mevcut `IUserAuditRecorder.RecordAsync` (yol b) ile merkezi günlüğe; ayrı olay adları `user_esign_reauthenticated`,
  `user_esign_window_ticket_issued`, `user_esign_ticket_consumed` (`UserAuditEvents`, işlem Execute). Mevcut `tenant_esign_*`
  satırları ve onları sayan testler aynen. Parola / bilet olaya girmez (yalnız intent, kind, consumedBy).
  Defter `## K2 borcu` + 3 satır, `PinnedCounts.K2Debt` 8 → 11 (aynı commit). Tam suite `UserAuditEventLabelGuardTests`'i kırmızı
  buldu (ekran etiketi yok) → `_IndexL10n.cshtml` + `AuditLogIndex.en/tr.resx` (Platform ekranı 2 dil) eklendi.
- MOD-0022 §20: FU-0022-AUD-01 **kapandı**; FU-0022-AUD-02 **açık** (önce-yaz kalıbı).
- Defter büyümesi: `git diff 394c05550..HEAD -- tests/architecture/audit-ledger/ | grep '^\+- '` → 3 satır
  (`ConsumeESignTicketCommand`, `IssueESignWindowTicketCommand`, `ReauthenticateForESignCommand`). Üçü de `CT-DECISIONS.md`'de
  (`grep -wF`, CI kapısının kuralı).
- AuditTrail denetimsiz-yeni: **65 → 46** (Platform −16, Auth −3). Envanter: Auth `29 | b 11 | borç 18 | K2 11`, Inventory `59 | b 59`,
  Platform `482 | a 296 | c 15 | istisna 5 | borç 166 | K2 192`.

### 11.5 Sabotaj s1–s9 (birleştirme sonrası; betik: geri yaz → SHA → `touch` → yeşil tekrar)

| # | sabotaj | kırmızı | geri | SHA |
|---|---|---|---|---|
| s1 | davranış başarısız komutta da yazar | BB `A_failed_marked_command_leaves_nothing(AnswersFailure)` 13/14 | 14/14 | f8b4f8349f99 = |
| s2 | işlem içi yazım işlem dışına | Inventory `An_aborted_ledger_transaction_leaves_no_event` 16/17 | 17/17 | 79b9a441b399 = |
| s3 | başarısızlıkta satır silinir | Inventory `Platform_failing_keeps_the_row…` 16/17 | 17/17 | b55fb08ab605 = |
| s4a | Platform `EventId` dalı kapalı | Platform `Two_events_that_share…` 4/5 | 5/5 | 6e336c323a09 = |
| s4b | Platform yinelenme yakalaması kaldırıldı | Platform `The_same_event_id_sent_twice…` 4/5 | 5/5 | 64418cc0495f = |
| s5 | `StartReviewCommand` işaretsiz | mimari 46 → **47** (`StartReviewCommand — YENİ KOMUT DENETİMSİZ GELDİ`); Inventory bildirim 2/4 | 46 / 4/4 | ba44e9af57a1 = |
| s6 | Inventory'de iletici kopyası | `CentralAuditForwarder_LivesOnlyInBuildingBlocks…` | yeşil | a598ad63d6aa = |
| s7 | olayda kiracı başka kimlikten | Inventory 10/17 (7 test) | 17/17 | 79b9a441b399 = |
| s8 | `AbandonAsync` → `Pending` | Inventory `A_failed_command_whose_transaction_committed…` 16/17 | 17/17 | b55fb08ab605 = |
| s9 | serbest bırakmada `ConfirmedByCommand=false` yok | Inventory `An_orphan_row_is_released…unconfirmed…` 16/17 | 17/17 | b55fb08ab605 = |

Derleme hatası her sabotajda 0 (kırmızı davranıştan).

### 11.6 Sayılar — taban = MVP-1 ucu `394c05550` (kendi ölçümüm), sonra = HEAD

| suite | taban | sonra | not |
|---|---|---|---|
| Inventory | **1182 + 4** | **1199 + 4** | +17 (CentralAudit 17); Prism bu koşuda iki tarafta yeşil |
| Platform Application | 5805 ✓ / 260 ✗ (6065) | 5810 ✓ / 260 ✗ (6070) | kırmızı küme **ad ad aynı**; +5 (`CentralAuditAppendMongoTests`) |
| Building.Blocks CentralAudit | — | 28/28 | |
| Building.Blocks Secrets | 5/5 | 5/5 | |
| Mimari | 69/77 (8 ✗) | **71/79 (8 ✗)** | kırmızı küme ad ad aynı |
| Auth Application | 1152/1154 | 1152/1154 | aynı 2 (`CompositionRootGuardTests` ×2, `AUTH_COMPOSITION_GUARD_MONGO_URI` yok). Kendi mongod'umla (27192) **2/2 yeşil**: kurucu DI değişikliği doğrulandı |
| Gateway | 164/165 | 164/165 | aynı |
| Web | 2280/2280 | 2280/2280 | |
| Derleme | Inventory sln 0/0 · Platform API 36/0 · Auth API 1/0 · Web 18/0 | aynı · CentralAudit + testleri 0/0 | yeni uyarı yok |

### 11.7 Açık

1. `RegisterSignableActionsCommand` / `VerifySignatureCommand` (§11.4) — CT kararı.
2. `AuditOutcome.Unconfirmed` için Web'de ayrı rozet / etiket yok (lookup enum adını verir) — gerekirse FU.
3. Önceki §10 maddeleri 2–6 aynen (FU-BL501-MIGRATE, FU-BL501-CATEGORY, K6).
