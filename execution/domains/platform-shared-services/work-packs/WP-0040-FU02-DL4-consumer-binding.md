# WP-0040-FU02-DL4 — Kayıt Kodu Servisi DL-4: Platform kapsamı ve İKİSİ DE tüketicilerinin bağlanması (TTY + PLN)

Tarih: 2026-11-14 · Veren: CT (merkez, Beste adına) · Paket: `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md`
**r18**, LF sha256 `1dbd718093a2e098bc09a1db502b5ba71edd0103b27e5219505ffa34f5c12273` · Onay: `execution/approvals/approvals.tsv` satır 100
(bu paket yolunun son satırı; Ali «onayla» 2026-11-14, yazan `bestepullukcu-alt`, onaylayan `GMG-ERP`; commit `87d5617ca`) · İnceleme:
`docs/records/reviews/MOD-0040-FU02/2026-11-14-1dbd7180.md` (UYGUN; `execution/approvals/reviews.tsv` satır 391) · Önceki dilim: DL-3
VERIFIED (`07ad98464`; `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md` "MOD-0040-FU02 DL-3")

> **DURUM: BAŞLATILABİLİR.** Paket §18.1'deki DL-4 başlama kapılarının hepsi kapalı: DL-3 tabanda (`07ad98464`); r18 CT-APR-02
> incelemesi UYGUN (açık ENGEL / ORTA yok); r18 sha256'sına CT-APR-01 onay satırı (satır 100); Q-17…Q-26 ve r17 inceleme kararları
> (F-1 c, F-2, A-22) işlendi; XMC-001 Q-26 onayı ve `UpdateSubscriptionPlanCommandValidator.cs` ek onayı verildi (§2). Paketin künyesi
> `approval_status: pending` yazar; onaylı sürümü belirleyen approvals.tsv'deki son satırdır (paket üst uyarısı). Kod bu WP, paket r18 ve
> aşağıdaki CT şartlarıyla yazılır; commit / merge kuralları §4.

## 0. Başlamadan

- **Dal:** tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (yeni dal yok). CT etiketi `ct/line-ff-2026-11-14g`
  (CT oluşturur; bu WP'nin commit'ini içerir — etiket yoksa dur ve CT'ye yaz). Etiket dalının atası ise
  (`git merge-base --is-ancestor ct/line-ff-2026-11-14g HEAD`) devam; değilse `git merge --ff-only ct/line-ff-2026-11-14g`; olmazsa
  `git merge --no-ff ct/line-ff-2026-11-14g`; çakışmada `git merge --abort` ve CT'ye yaz (çözme).
- **DL-3 tabanda:** `git merge-base --is-ancestor 07ad98464 HEAD` başarısızsa dur.
- **Paket sha256 (fail-closed):** paketin LF sha256'sını ölç (ör. `git show HEAD:execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md | tr -d '\r' | sha256sum`);
  `1dbd7180…2273` değilse **dur** — kod yok, CT'ye yaz.
- **Onay satırı:** `execution/approvals/approvals.tsv`'de bu paket yolu için yazılmış **son** satırın 2. sütunu `1dbd7180…2273`, 3. sütunu
  `approve` olmalı (bugün satır 100). Değilse ya da sonradan aynı yol için başka satır yazılmışsa dur.
- **Paketi oku (r18 numaralarıyla):** üst uyarı ve r16 / r17 / r18 revizyon blokları; 2.6; 3; 4.5; 4.6; 5 "DL-4"; 6; 7 (DL-4 / DL-11
  satırları); 8.17; 8.18; 9; 11; 12; 13; 14; 16 (AC-25, AC-76…AC-91); 17 (DL-4 satırları, S-117…S-138); 18.1 (DL-4 kapıları); 19.8; 19.10;
  20 (FU-RCS-03, -04, -22, -23, -24, -27, -28); 21.6; 22; 23; 24.3. Bu WP'nin §1'i bölümlerin bir kısmını birebir taşır; çelişkide paket kazanır,
  §3'teki CT şartları paketi **ekler** (çelişmez).
- **Dilim başı taban koşusu** (§5) koşulmadan kod yazılmaz; dilim başı kırmızıları adıyla rapora.

## 1. Kapsam (paketten birebir, r18 @ `1dbd7180`)

### 1.1 Dilim satırı — paket §23 DL-4

| dilim | ad | kapsadığı bölümler | kabul ölçütleri ve kapsam notu |
|---|---|---|---|
| DL-4 | Platform kapsamı ve İKİSİ DE tüketicilerinin bağlanması — Görev Türü (TTY, MOD-0024) ve Abonelik Planı (PLN, öneksiz kod, MOD-0361); PPM DL-11'de, PPM sahibi ekibin (Q-17 kararı) | Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Layout & Shell Contract, Backend File Convention, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes, Follow-up Items | **r16:** AC-25 (K-11'den taşındı; AC-86 / AC-87 ile ölçülür) ve AC-76…AC-91; kiracısız taban ayrı `GlobalEntity` koleksiyonları (Q-18 A, Bölüm 4.6), platform-global denetim mevcut platform denetim kiracısıyla (Q-19 A), oturumlu port (Q-20 A, Bölüm 8.18); sözleşme 1.0.0 değişmez. **r17:** Q-17…Q-26 karar (Q-24 B: PLN güncellemesi kodu değiştirebilir, normalleştirici aynen, değişiklik motorla; Q-25 B: grup öneki yoksa form kodu zorunlu tutar); XMC-001 sahip onaylı (Bölüm 5 "DL-4"); **başlama kapısı** DL-3 tabanda (sağlandı), r17 inceleme + r17 sha256'sına onay satırı; **kabul kapısı** canlı veride desene uyan eski kod sayımı, sürüm notu ve yönetici bildirimi, A-21 (yalnız yeni belirteç açılırsa); canlıya alma kapısı yok (Q-25 B). **r18:** r17 incelemesi KOŞULLU; F-1 (c) PLN eski kodu değişiklik anında deftere alınır (`Legacy`, emekli), F-2 boş kodlu güncelleme 400, A-22 PPM ekibi; AC-82, AC-88, AC-90 hizalandı, S-136…S-138; **başlama kapısı** r18 inceleme + r18 sha256'sına onay satırı + `UpdateSubscriptionPlanCommandValidator.cs` XMC-001 ek onayı; kod izni r18 onayına kadar yok |

PPM (MOD-0117) bu dilimde **yoktur** — DL-11, PPM sahibi ekibin (Q-17, A-22). Paket §2.6'nın dışarıda bıraktıkları:

**DL-4 dışında:** PPM `PFL` / `PRG` / `PRJ` / `INI` / `INV` (MOD-0117, Diten.PpmService, süreç dışı) → **DL-11, PPM sahibi ekibin**
(Q-17 kararı): PPM kodunu ekip kendi servisinde yazar; PPM'in kullanacağı Platform tarafı uçlarını (sözleşme eki + Platform kodu) da PPM
ekibi yazar (A-22 kararı, r18; XMC-001) — bu paketin payı DL-11 revizyonu, inceleme ve kabul ölçümüdür (Bölüm 8.19); eski kodların
sahiplik defterine **genel** alınması → DL-6 (Q-22 A kararı; PLN'de yalnız kod değişikliği anındaki alım DL-4'tedir — F-1 c); kodu kayda
bağlama / kullanılmayan kod raporu (FU-RCS-04) → DL-10 (Q-23 A kararı); PLN kullanım kilidi (FU-RCS-27); tohumların motora bağlanması
(FU-RCS-23); arka plan çağıranları → DL-9; yönetici ekranları → DL-10.

### 1.2 Repo Scope — paket §5 "DL-4 (r16) — tam dosya yolları"

N = yeni, M = yalnız belirtilen değişiklik; **XMC** = başka modülün dosyası — **sahip onaylı (karar kaydı, Q-26, 2026-11-14)**, yalnız
bu listeyle sınırlı; liste dışı dokunuş yine sorulur (XMC-001). Kompozisyon dosyalarının SHA-256'sı sabitlenmez (module-pack-standard §13).
Liste sahip kararlarıyla yazıldı (Q-17, Q-18 A, Q-20 A, Q-21 C, Q-22 A, Q-24 B, Q-25 B). Yollar `30c313eb4`'te ölçüldü; **r17:** Q-24 B ve
Q-25 B aynı dosyalarda kalır — XMC listesi değişmedi; tek ek bu paketin yeni test dosyası `TaskTypeRecordCodeFormTests.cs` (başka modülün
mevcut dosyası değil). **r18:** F-1 (c) bu paketin dosyalarına iki eklemeli değişiklik getirir (`RecordCodeClaimOrigin.cs`,
`IInProcessRecordCodeAssignments.cs`) ve çekirdeğe eski kod kolunu ekler; F-2 kod kuralını güncelleme doğrulayıcısında tutar — kuralın
taşındığı `UpdateSubscriptionPlanCommandValidator.cs` Q-26 listesinde **yoktu** → XMC-001 ek onayı açık (Bölüm 18.1 DL-4 başlama kapısı).

**Bu paketin dosyaları (MOD-0040-FU02).**

Domain (`services/Diten.Platform/src/Diten.Platform.Domain/`):

- M `Features/RecordCodes/RecordCodeTypeCatalog.cs` — yalnız `RecordCodeTypeDefinition`'a eklemeli, varsayılanı `null` son üye
  `RecordCodePlatformBinding? PlatformBinding` (`Caller`, `RequiredPermission`) ve `PLN` satırına bağlama; `PLN`'in `AllowedCaller` /
  `RequiredPermission` alanları `null` kalır; başka satır değişmez (Bölüm 4.4 r16 notu).
- M `Features/RecordCodes/RecordCodeExceptions.cs` — yalnız `RecordCodePlatformScopeViolationException`.
- N `Entities/RecordCodes/RecordCodePlatformCounter.cs`, `Entities/RecordCodes/RecordCodePlatformClaim.cs`
- M `Repositories/IRecordCodeRepositories.cs` — yalnız `IRecordCodePlatformCounterRepository`, `IRecordCodePlatformClaimRepository`;
  mevcut beş arayüz değişmez.
- M (r18, F-1 c) `Enums/RecordCodes/RecordCodeClaimOrigin.cs` — yalnız eklemeli `Legacy = 3`; `System` / `Manual` değişmez.

Application (`services/Diten.Platform/src/Diten.Platform.Application/`):

- M `Contracts/RecordCodes/IInProcessRecordCodeAssignments.cs` — yalnız eklemeli `ClaimWithinAsync`, `ChangeWithinAsync` ve iki istek
  kaydının eklemeli son üyesi `ExternalCodeTaken` (Bölüm 8.18 madde 1); **r18 (F-1 c):** değişiklik isteğine ondan sonra eklemeli son üye
  `LegacyAdoption` ve yeni değer `RecordCodeLegacyAdoption(DateTime RecordCreatedAtUtc)`.
- N `Features/RecordCodes/RecordCodeAssignmentWriter.cs` — sahiplenme / değişikliğin işlem içi çekirdeği (kiracı ve platform kolu);
  **r18:** platform kolunda eski kod alımı (`Legacy` satırı + DL-3 emekliliği, Bölüm 8.18 madde 5).
- M `Features/RecordCodes/InProcessRecordCodeAssignments.cs` — oturumlu yöntemler ve platform kolu komut kurulumu.
- M `Features/RecordCodes/RecordCodeInProcessGuard.cs` — yalnız `DefendPlatform` ve platform izin hükmü; kiracı kolu değişmez (DL-3
  AC-73 eşlik testi değişmeden yeşil).
- M `Features/RecordCodes/Handlers/CommandHandlers/ClaimRecordCodeHandler.cs`, `ChangeRecordCodeHandler.cs` — işlem gövdesi çekirdeğe
  taşınır, platform kolu ve dış yoklama eklenir; kiracı kolunun davranışı değişmez (DL-3 AC-62…AC-75 testleri değişmeden yeşil).
- M `Features/RecordCodes/Handlers/QueryHandlers/PreviewRecordCodeHandler.cs`, `GetRecordCodeHistoryHandler.cs` — yalnız platform kolu.
- M `Features/RecordCodes/RecordCodeAuditIntent.cs` — yalnız DL-3'ün dört yönteminin kayıt kiracısı ve hedef alan eklemeli aşırı
  yüklemeleri (yöntem adları aynı → defter değişmez; Bölüm 21.6).
- M `DependencyInjection.cs` — yalnız `RecordCodeAssignmentWriter` kaydı.
- **Değişmez:** `LockRecordCodeHandler.cs`, `AllocateRecordCodeHandler.cs`, DL-2 dosyaları (`InProcessRecordCodeAllocator.cs`,
  `IInProcessRecordCodeAllocator.cs`).

Infrastructure (`services/Diten.Platform/src/Diten.Platform.Infrastructure/`):

- M `Persistence/Repositories/RecordCodeRepositories.cs` — yalnız iki platform deposu.
- M `Persistence/Schema/PlatformSchemaManifest.RecordCodes.cs` — yalnız iki koleksiyon ve dört index'i (Bölüm 4.6).
- M `Persistence/Schema/PlatformCollections.cs` — yalnız iki koleksiyon adı.
- M `DependencyInjection.cs` — yalnız iki depo kaydı.

Testler:

- N `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/TaskTypeRecordCodeHttpTests.cs`,
  `SubscriptionPlanRecordCodeHttpTests.cs`, `PlatformRecordCodeMongoTests.cs`
- M `…/RecordCodes/RecordCodeTypeCatalogTests.cs` — yalnız yeni AC-76 / AC-85 testleri; mevcut testler değişmez.
- M `…/RecordCodes/InternalRecordCodesHttpTests.cs` — yalnız AC-76 testi.
- M `…/RecordCodes/InProcessRecordCodeAssignmentsTests.cs` — yalnız AC-89 / AC-91 testleri; **r18:** AC-82 kiracı kolu `LegacyAdoption`
  reddi testi.
- N `frontend/Diten.Web/tests/task-type-record-code-form.test.js`, `frontend/Diten.Web/tests/subscription-plan-record-code-form.test.js`
- N (r17, Q-25 B) `frontend/Diten.Web.Tests/Controllers/TaskTypeRecordCodeFormTests.cs` — yalnız AC-83 form adımı (yansıma; MOD-0024'ün
  mevcut Web testleri değişmez)

**XMC — MOD-0024 Görev Türü (Q-26; sahip onaylı — karar kaydı).**

- M `services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs` — yalnız üç eylem: `GET task-types/code-preview`,
  `GET task-types/{id:guid}/code-history`, `PUT task-types/{id:guid}/code`; üçü de `[HasPermission(TaskPermissions.TaskTypesManage)]`.
- M `services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Commands/TaskItemCommands.cs` — yalnız `ChangeTaskTypeCodeCommand`.
- M `…/Features/Tasks/Queries/TaskItemQueries.cs` — yalnız `GetTaskTypeCodePreviewQuery`, `GetTaskTypeCodeHistoryQuery`.
- M `…/Features/Tasks/Handlers/CommandHandlers/TaskTypeHandlers.cs` — `CreateTaskTypeHandler` kodu motordan alır (oturumlu, Bölüm 8.18
  madde 3); yeni `ChangeTaskTypeCodeHandler`; `UpdateTaskTypeHandler` ve `SetTaskTypeActiveHandler` değişmez.
- M `…/Features/Tasks/Handlers/QueryHandlers/TaskTypeQueryHandlers.cs` — yalnız iki sorgu işleyicisi.
- M `…/Features/Tasks/Services/TaskTypeRules.cs` — yalnız `ValidateShape`'te oluşturma kodunun zorunluluğu kalkar; `ValidateCodeUnchanged`
  değişmez.
- M `…/Features/Tasks/TaskModels.cs` — `CreateTaskTypeRequest.Code` isteğe bağlı (`string?`) ve eklemeli `CodeReason`;
  `CreateTaskTypeResultDto`'ya eklemeli `Code`; yeni `ChangeTaskTypeCodeRequest(ExpectedCurrentCode, NewCode, Reason, ExpectedVersion)`.
- M `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/ITaskRepositories.cs` — yalnız iki dar arayüz
  (`ITaskTypeTransactionalWriter`, `ITaskTypeUsageReader`; Bölüm 3); `ITaskTypeRepository` değişmez.
- M `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/TaskRepositories.cs` — yalnız iki dar arayüzün
  uygulanması.
- M Web: `frontend/Diten.Web/Controllers/TaskTypesController.cs`, `frontend/Diten.Web/Models/TaskTypes/TaskTypeEditViewModel.cs`,
  `frontend/Diten.Web/Views/Tasks/TaskTypes/_Form.cshtml`, `frontend/Diten.Web/Views/Tasks/TaskTypes/Edit.cshtml`,
  `frontend/Diten.Web/Views/Tasks/TaskTypes/Details.cshtml`,
  `frontend/Diten.Web/Resources/Views/Tasks/TaskTypes/TaskTypesIndex.{en,tr,fr,es,zh,ar,ru}.resx`. **r17 (Q-25 B):** denetleyici
  önizleme cevabını form durumuna çevirir, görünüm modeli `CodeRequired` taşır, `_Form.cshtml` zorunlu kod dalını çizer, dil dosyalarına
  `RecordCodeManualRequired` (Bölüm 11).
- M testler — yalnız kurucu bağımlılığı ve istek gövdesi (sınama amacı değişmez): `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Tasks/TaskTypeConcurrencyTests.cs`,
  `TaskTypeDeliverableRequirementTests.cs`, `TaskTypeEffectivenessGateHttpTests.cs`, `TaskTypeEffectivenessGateMongoTests.cs`,
  `TaskTypeReviewMeetingRequirementTests.cs` (ölçüldü: beşi `CreateTaskTypeHandler` / `UpdateTaskTypeHandler` kurar ya da
  `CreateTaskTypeCommand` gönderir); gerekirse `…/Tasks/TaskTestDoubles.cs` (yalnız iki dar arayüzün sahtesi). Web testleri
  (`frontend/Diten.Web.Tests/Controllers/TaskTypeConcurrencyFormTests.cs`, `TaskTypeClosureOutcomeEditorTests.cs`,
  `TaskTypeEffectivenessMessagesFormTests.cs`, `TaskTypeReviewMeetingRequirementFormTests.cs`, `frontend/Diten.Web/tests/task-types-active-gate.test.js`,
  `task-type-closure-outcome-editor.test.js`) değişmeden yeşil kalmalıdır; biri değişirse adıyla kabul kaydına yazılır (XMC).
- **Değişmez (korunan, Bölüm 6):** `CreateTaskItemHandler.cs` (Q-21 C kararı; A seçilseydi XMC listesine o dosya ve onu kuran 10 test
  dosyası girerdi — Bölüm 19.8), `TaskSupportingEntities.cs`, `ITaskTypeRepository` gövdesi, görev türü liste ekranı dosyaları.

**XMC — MOD-0361 / CAND-CAP-0002-FU02 Abonelik Planı (Q-26; sahip onaylı — karar kaydı).**

- M `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/SubscriptionPlansController.cs` — yalnız iki eylem:
  `GET code-preview` (`[HasPermission("platform.subscription-plans.create")]`), `GET {id:guid}/code-history` (`…read`).
- M `services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/CreateSubscriptionPlanCommandHandler.cs`
  — kod motordan, koordinatör gövdesinde (Bölüm 8.18 madde 6).
- M `…/Features/SubscriptionPlans/Handlers/CommandHandlers/UpdateSubscriptionPlanCommandHandler.cs` — kod değişikliği kolu (r17, Q-24 B): normalleştirilmiş farklı kod koordinatör işleminde motorun `ChangeWithinAsync`'iyle (Bölüm 8.18 madde 6); bugünkü `ExistsByCodeAsync` tekillik denetimi motorun adım 13'üne geçer; **r18 (F-1 c):** istek her zaman `LegacyAdoption = new(plan.CreatedAt.UtcDateTime)` taşır — eski planda (aktif sahiplik yok) motor eski kodu deftere alır, 409 `RECORD_CODE_IMMUTABLE` kolu yoktur.
- M `…/Features/SubscriptionPlans/SubscriptionPlanContracts.cs` — `CreateSubscriptionPlanRequest.Code` isteğe bağlı ve eklemeli `CodeReason`;
  `UpdateSubscriptionPlanRequest`'e eklemeli düz `CodeReason` (r17, Q-24 B; `CodeChange` üyesi yok).
- M `…/Features/SubscriptionPlans/Validators/SubscriptionPlanRequestValidator.cs` — yalnız kod kuralı (`:19-23`) bu ortak dosyadan kalkar →
  oluşturmada boş kod sistem kodu alır, elle kod retleri işleyicide verilir ki denetime yazılsın (DL-2 inceleme 04 F-6 emsali).
- M (r18, F-2; **liste dışı — XMC-001 ek onayı açık**, Bölüm 18.1) `…/Features/SubscriptionPlans/Validators/UpdateSubscriptionPlanCommandValidator.cs` —
  yalnız ortak dosyadan kalkan kod kuralı (`NotEmpty` "Code is required." + normalleştirilmiş 2–50) güncelleme doğrulayıcısına **aynen**
  taşınır: güncellemede boş kod bugünkü gibi 400 (`ValidationProblemDetails`); `CreateSubscriptionPlanCommandValidator.cs` değişmez.
- N `…/Features/SubscriptionPlans/Queries/GetSubscriptionPlanCodePreviewQuery.cs`, `GetSubscriptionPlanCodeHistoryQuery.cs`;
  N `…/Features/SubscriptionPlans/Handlers/QueryHandlers/GetSubscriptionPlanCodePreviewHandler.cs`, `GetSubscriptionPlanCodeHistoryHandler.cs`
  (Golden Reference adı — ek yok; modülün mevcut `…CommandHandler` adları yeniden adlandırılmaz, Bölüm 10).
- M Web: `frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs` (görünüm modeli bu dosyada),
  `frontend/Diten.Web/Views/Platform/SubscriptionPlans/_Form.cshtml`, `frontend/Diten.Web/Views/Platform/SubscriptionPlans/Edit.cshtml`,
  `frontend/Diten.Web/Resources/Views/Platform/SubscriptionPlans/SubscriptionPlansIndex.{en,tr}.resx`.
- M testler — gerekirse, yalnız istek gövdesi: `services/Diten.Platform/tests/Diten.Platform.Application.Tests/GlobalApplicability/GlobalApplicabilityAuditChangeHandlerTests.cs`,
  `…/Persistence/TransactionOwnedAuditDeliveryMongoTests.cs`, `…/Tenants/PartnerAdminScopeHttpMongoTests.cs` (ölçüldü: plan
  oluşturma / güncelleme gönderenler).
- **Değişmez (korunan, Bölüm 6):** `SeedDefaultSubscriptionPlansCommandHandler.cs` (FU-RCS-23), `SubscriptionPlanCodeNormalizer.cs`
  (değişmez; r17, Q-24 B: oluşturma ve güncelleme yolunda motordan **önce** aynen çağrılır, tohum da kullanmaya devam eder), `GlobalApplicabilityTransactionCoordinator.cs`,
  `ISubscriptionPlanRepository.cs`, `GlobalApplicabilityTransactionalArchitectureTests.cs`, `TransactionOwnedAuditCommands.cs` (yeni plan
  komutu açılmaz — Q-24).

**Değişmez (paylaşılan; AC-91 `git diff` ile ölçer):** `docs/analysis/contracts/record-code-allocate.md`, `gateway/Diten.ApiGateway/ocelot.json`,
`TrustedRecordCodeCredentialAuthenticator.cs`, `CanonicalTransactionalAuditOutboxWriter.cs`, `AuditOutboxWriteRequest.cs`,
`TransactionOwnedAuditSourceServices.cs`, `services/Diten.PpmService/**` (MOD-0117 — DL-11, PPM sahibi ekibin; bu paket dokunmaz).

**DL-4'ün dışında, CT / sahip adımı (geliştirici yazmaz):** DL-4 kodu yeni bir `RecordCodeAuditIntent` belirteci açarsa defter satırı
(A-21 CT kararı: açılmaz — aşırı yükleme; açılırsa A-20 emsali); RCS-001 §3 `TTY` ve "Abonelik Planı" satırlarının "uyumsuz" notu (FU-RCS-24, Ali — `.antigravity`).

> **CT notu (XMC ek onayı):** yukarıdaki `UpdateSubscriptionPlanCommandValidator.cs` satırındaki "liste dışı — XMC-001 ek onayı açık" ifadesi
> **kapandı**: Beste ek onayı verdi (karar kaydı "Ek — MOD-0040-FU02 r18 XMC ek onayı ve inceleme F-1", satır 157-158; `7e58c40e6`) — dosya Q-26
> listesine eklendi, yalnız kod kuralının aynen taşınması için. Paket metni sonraki revizyonda hizalanır.

### 1.3 Çalışma kuralları — paket §8.18

Bu bölümün seçimleri Bölüm 24.3'teki sahip / CT kararlarıdır (Beste 2026-11-14; karar kaydı
`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:121-139`). Q-24 ve Q-25'te seçim r16 önerisinden farklıydı (B);
madde 3, 6 ve 8 r17'de buna göre yazıldı. Diğer maddelerin içeriği r16'daki gibidir, yalnız "öneri" dili "karar" oldu. **r18:** r17
incelemesinin sahip kararları (`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:141-153`) madde 1, 5, 6 ve 8'e
işlendi — F-1 (c) PLN eski kodunun değişiklik anında deftere alınması, F-2 boş kodlu güncellemede 400, F-3 izdüşüm atfı.

**1. Oturumlu port (Q-20 A kararı).** `IInProcessRecordCodeAssignments`'a **eklemeli** iki yöntem:

```csharp
Task<Response<RecordCodeClaimResult>> ClaimWithinAsync(
    IPlatformTransactionSession session, InProcessRecordCodeClaimRequest request, CancellationToken ct);
Task<Response<RecordCodeChangeResult>> ChangeWithinAsync(
    IPlatformTransactionSession session, InProcessRecordCodeChangeRequest request, CancellationToken ct);
```

İki istek kaydı eklemeli, varsayılanı `null` bir son üye alır: `Func<string, CancellationToken, Task<bool>>? ExternalCodeTaken` (tüketicinin
defter dışı eski kod yoklaması, madde 5). **r18 (F-1 c):** `InProcessRecordCodeChangeRequest` ondan sonra eklemeli, varsayılanı `null` bir
son üye daha alır: `RecordCodeLegacyAdoption? LegacyAdoption` (`RecordCodeLegacyAdoption(DateTime RecordCreatedAtUtc)`) — aktif sahiplik
satırı olmayan kaydın saklı kodunu değişiklik anında deftere alma izni (madde 5); yalnız platform kolunda geçerlidir. DL-3'ün beş yöntemi ve
davranışı **değişmez**. Oturumlu yöntemler değerlendirme sırasını (Bölüm
8.17) aynen uygular; farklar: (a) adım 15'teki yazımlar (sahiplik satırı, emeklilik, Tablo E / F) tüketicinin verdiği oturumla, tüketicinin
işleminde yapılır — tüketici kendi kaydını aynı işlemde yazar, hepsi birlikte commit olur ya da hiçbiri olmaz; (b) sistem kodunda DL-1
ayırması (`AllocateRecordCodeCommand`, `ISender`) bugünkü gibi **kendi** işlemindedir (Mongo'da iç içe işlem yoktur) — tüketici işlemi
düşerse ayrılan kod bir boşluk olarak kalır (§5.8), yeni denemenin yeni kayıt kimliği yeni numara alır; (c) ret satırları ret yazıcısıyla
ayrı kısa işlemde yazılır (DL-3 gibi) ve motor ret sonucunu döner — tüketici işlem gövdesini **hiçbir şey yazmadan** bitirir; (d) sahiplik
eklemesindeki E11000 tüketicinin işlemini iptal ettirir: motor `RecordCodeClaimConflictException`'ı tüketici işleyicisinin yakaladığı bir
sonuca çevirir (`ux_…_code` → 409 `RECORD_CODE_TAKEN`, `ux_…_record_active` → 409 `RECORD_CODE_ALREADY_ASSIGNED`); oturumlu yolda **tekrar
kolu yoktur** çünkü kayıt kimliği yenidir (oluşturma) ya da değişiklik kendi aktif satırını emekliye ayırır.

**2. Platform kolu (Q-18 A, Q-19 A kararları).** Katalog satırında `PlatformBinding` varsa (bugün yalnız `PLN`) motor platform koluna girer:

- **Savunma** `RecordCodeInProcessGuard.DefendPlatform`: `ITenantContext.IsPlatformContext == true` (ara katman `/api/platform/*`
  isteğinde kurar — `services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs:304-307`, `:129`),
  `ActorType == platform_admin` (`OrdinalIgnoreCase`), `UserId ≠ Guid.Empty`; değilse hiçbir şey göndermeden 403 `RECORD_CODE_FORBIDDEN`,
  ret satırı yok, uyarı logu. Kiracı kolunun savunması değişmez ve `PLN`'i reddeder (kiracı kolu `AllowedCaller`'a bakar, `null` → adım 3,
  403 `RECORD_CODE_FORBIDDEN`; DL-3 AC-73 değişmez). Platform kolu `TTY` gibi kiracı türlerini kabul etmez (`PlatformBinding` yok → 403).
- **Adımlar:** 1 (kiracı aktif) yoktur; 2, 3 (`PlatformBinding.Caller`), 4 (platform aktöründe anahtar ayırt edici değildir — Bölüm 14;
  koruma savunmadır), 5–13 DL-3 ile aynı, depo platform deposu; 14 önek yok — kod `PLN-{n, en az 6 hane}`, biçim
  `RecordCodeFormat.IsValidPlatformCode`; sayaç platform sayacı, `$inc` **tüketicinin işleminde** (iptal sayacı da geri alır); 15
  tüketicinin işlemi.
- **Denetim (Q-19 A kararı):** kayıt kiracısı `AuditTenantIds.PlatformSystemTenantId` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Audit/AuditTenantIds.cs:5`),
  `Intent.TargetTenantId = null`. Yazıcı bunu bugün kabul eder: kutunun boş kiracı reddi yalnız `Guid.Empty` içindir
  (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/AuditOutboxWriteRequest.cs:28-30`), yazıcının hedef reddi de
  (`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs:119`; `null` hedef
  geçer). Emsal: küresel uygulanabilirlik koordinatörü aynı iki alanla işlem içi yazar
  (`services/Diten.Platform/src/Diten.Platform.Application/Features/GlobalApplicability/GlobalApplicabilityTransactionCoordinator.cs:154`, `:165`)
  ve abonelik planı komutları bugün böyle denetlenir. Yazıcı, kaynak servis kümesi ve `TransactionOwnedAuditCommands` **değişmez**. Aktör
  `platform_admin` → `PlatformAdministrator` (kapı çözer, K-8r). Retler aynı kayıt kiracısıyla ret yazıcısından.
- **Yeni belirteç yok (A-21, CT kararı):** `RecordCodeAuditIntent`'in DL-3 yöntemleri kayıt kiracısını ve hedefi parametre alan eklemeli
  aşırı yüklemelerle platform kolunu taşır; yöntem adları aynı kaldığından defter (`tests/architecture/audit-ledger/Diten.Platform.md`)
  değişmez. Kod buna rağmen yeni bir ad açarsa A-21'in kapısı doğar (A-20 emsali, Bölüm 18.1).

**3. TTY bağlanması (MOD-0024; XMC-001 sahip onaylı, Q-26).**

- **Oluşturma** (`CreateTaskTypeHandler`): bugünkü biçim / sınıf / toplantı / kapanış sonucu kuralları (kod hariç) ve etkinlik kapısı
  (`TaskTypeEffectivenessGate.CheckAsync` — dış servis çağrısı işlemden **önce**) → `IPlatformTransactionExecutor.ExecuteAsync` gövdesi:
  (a) `ClaimWithinAsync(session, new("TTY", type.Id, request.Code, request.CodeReason, ExternalCodeTaken: tüketici yoklaması))`; ret →
  gövde hiçbir şey yazmadan ret döner; (b) başarıda `type.Code = sonuç.Code`; (c) `ITaskTypeTransactionalWriter.CreateAsync(session, type)`.
  Görev türü kimliği kayıttan önce bellidir (`BaseEntity.Id` başlangıç değeri `Guid.NewGuid()`,
  `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:5`). Bugün yazım oturumsuzdur
  (`services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/TaskTypeHandlers.cs:103`).
- **Kod kuralı:** `TaskTypeRules.ValidateShape` oluşturma kodunu artık zorunlu tutmaz (boş = sistem kodu); biçim ve uzunluk motordadır
  (`RecordCodeManualCode`, 2–40 — bugünkü 40 üst sınırıyla aynı); `TaskTypeRules.NormalizeCode` oluşturma yolunda kullanılmaz; güncelleme
  kuralı `ValidateCodeUnchanged` değişmez (400 `TASK_TYPE_CODE_IMMUTABLE`).
- **Grup öneki tanımlı değilse (Q-25 B kararı, r17).** Sunucu kuralı **değişmez**: kodsuz oluşturma ve önizleme 409 `GROUP_PREFIX_MISSING`
  (ayar etkin `OrgPrefix`'ten farklıysa 409 `GROUP_PREFIX_MISMATCH`; DL-1 adım 11 / DL-3 adım 14), görev türü yazılmaz, sistem kodu
  verilmez. Tüketici ekranı bu durumda kodu **zorunlu** tutar: Web denetleyicisi `Create` GET'inde önizlemeyi okur ve cevabı özel statik bir
  yardımcıyla form durumuna çevirir — 409 `GROUP_PREFIX_MISSING` → `CodeRequired = true`: form kod ve gerekçe girişlerini zorunlu işaretler
  (`required`, `*`), sistem kodu yer tutucusu göstermez, yerelleştirilmiş `RecordCodeManualRequired` uyarısını gösterir; kullanıcının yazdığı
  kod DL-3 elle kod kurallarından (normalleştirme, 2–40, desen yasağı — Q-11 A, Q-12 A) ve gerekçe kuralından (10–500 — Q-9 A, Q-10 A)
  geçer, motorun tekillik denetimi (defter + dış yoklama) aynen. 409 `GROUP_PREFIX_MISMATCH` aynı form davranışını alır (**çıkarım:** iki
  durumda da sistem kodu verilemez; karar metni yalnız "tanımlı değilse" der — Bölüm 24.3 Q-25 notu; **CT kabul**, karar kaydı satır
  152-153, r18). Önizleme başka bir nedenle
  başarısızsa (ör. 503) kod isteğe bağlı kalır, yer tutucu gösterilmez ve kayıtta sunucunun cevabı gösterilir. Kodsuz POST yine 409
  alır (form denetimini atlayan istemciye karşı sunucu sınırı). **Canlıya alma kapısı değildir:** hizalanmamış kiracıda görev türü elle
  kodla açılmaya devam eder.
- **Gerekçeli değişiklik** (`ChangeTaskTypeCodeHandler`, `PUT …/task-types/{id}/code`): tür kiracı deposundan okunur (yoksa 404
  `TASK_NOT_FOUND`); türün aktif sahiplik satırı yoksa → 409 `RECORD_CODE_IMMUTABLE` (eski kod, madde 5); kilit denetimi (madde 4);
  işlem gövdesi: `ChangeWithinAsync(session, …)` + `ITaskTypeTransactionalWriter.UpdateCodeAsync(session, id, yeniKod, expectedVersion)` —
  sürüm eşleşmezse işlem iptal, 409 `TASK_CONCURRENCY_CONFLICT`; Tablo F aynı işlemde.
- **Öneri / geçmiş uçları** motorun `PreviewAsync` / `GetHistoryAsync`'ini çağıran sorgulardır; denetlenmez.

**4. TTY kullanım kilidi (Q-21 C kararı).** Görevler türü kimlikle anar (`TaskItem.TaskTypeId`,
`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskItem.cs:313`) ve kodu taşımaz; kod değişikliği görevlerin gösterilen
tür kodunu değiştirir — MOD-0024'ün bugünkü değişmezlik gerekçesi (`TaskSupportingEntities.cs:586-589`). Karar: kod değişikliği ucu,
değişiklikten önce `ITaskTypeUsageReader.HasTasksAsync(typeId)` (silinmiş görevler dahil) okur; görev varsa `LockAsync("TTY", typeId)`
(Tablo G) ve 409 `RECORD_CODE_LOCKED`. `CreateTaskItemHandler` değişmez. **Kabul edilen sınır:** ilk görev kod değişikliğiyle aynı anda
açılırsa (okumadan sonra, commit'ten önce) değişiklik geçebilir; o görev türün yeni kodunu gösterir, veri bütünlüğü kimlikle korunur.
Reddedilen A seçeneği (görev açılınca kilit) bu sınırı kapatırdı ama `CreateTaskItemHandler`'a kurucu bağımlılığı ekler; onu kuran **10**
test dosyası değişirdi (ölçüldü, Bölüm 19.8).

**5. Eski kodlar ve çakışma savunması (Q-22 A kararı; FU-RCS-22; r18 F-1 c kararı).** Sahiplik satırı olmayan (bugün açılmış) kayıtların
kodu **deftere alınmaz** (RCS-001 §2); TTY'de **değişmez** de (409 `RECORD_CODE_IMMUTABLE`, madde 3). PLN'de kod değişikliği anında
deftere alınır (aşağıda, F-1 c). Tüketici, motora `ExternalCodeTaken` yoklamasını verir: TTY'de "kiracıda bu kodu taşıyan görev
türü var mı, emekliler dahil" (bugünkü tekillik sorgusu, `TaskTypeHandlers.cs:64-70`); PLN'de oturumlu `ExistsByCodeAsync(session, code)`
(`services/Diten.Platform/src/Diten.Platform.Domain/Repositories/ISubscriptionPlanRepository.cs:24`). Motor: **elle kodda** adım 13 = defter
`CodeExistsAsync` **veya** yoklama → 409 `RECORD_CODE_TAKEN`; **sistem kodunda** ayrılan kod yoklamada doluysa ayırma
`{tür küçük harf}:{RecordId:N}:2`, sonra `:3` anahtarıyla tekrarlanır (DL-2 Bölüm 8.15 deseni; her deneme ayrı DL-1 satırı, boşluk),
üçüncü denemeden sonra 409 `RECORD_CODE_COLLISION`; PLN sistem kodunda platform sayacı aynı işlemde bir daha artırılır (en çok 3).
Yoklama verilmezse (`null`) motor yalnız defteri arar (DL-3 davranışı). **Canlı sayım:** kabul kaydı kiracı başına
`^[A-Z]{2,5}-TTY-[0-9]{6,}$` desenine uyan görev türü kodu ve `^PLN-[0-9]{6,}$` desenine uyan plan kodu sayısını yazar (Bölüm 18.1).

**5a. PLN eski kodunun değişiklik anında deftere alınması (F-1 c kararı, r18; karar kaydı satır 145-148).** DL-4'ten önce açılmış (tohum
dahil) bir planın kodu güncellemede değiştirilirse değişiklik gerekçeyle deftere alınır; değiştirilmeyen eski kodlar deftere alınmaz
(Q-22 A korunur). Motor tarafı `ChangeWithinAsync`'in **platform kolundadır** ve DL-3 değişiklik sırasını (Bölüm 8.17) şu farkla uygular:

- **Adım 6 (istek):** `LegacyAdoption` **kiracı kolunda** doluysa → 400 `RECORD_CODE_REQUEST_INVALID`, hiçbir şey yazılmaz, Tablo H satırı
  (TTY'nin Q-22 A'sı korunur; defter alımının genel hali DL-6'dadır — FU-RCS-22). Platform kolunda `RecordCreatedAtUtc` şimdiki andan küçük
  değilse → aynı 400 (geçmiş sırası için, Bölüm 4.6).
- **Adım 7 (aktif satır, oturumla okunur):** aktif satır **varsa** DL-3 kolu aynen koşar ve `LegacyAdoption` etkisizdir (tüketici planın
  defter durumunu bilmeden her istekte gönderir — sessiz yok sayma değil, tanımlı davranış). Aktif satır **yoksa**: `LegacyAdoption`
  `null` → 404 `RECORD_CODE_NOT_ASSIGNED` (DL-3); dolu → **eski kod alımı**: eski kod = `ExpectedCurrentCode` (tüketicinin koordinatör
  işleminde okuduğu saklı kod; boşsa 400 `RECORD_CODE_REQUEST_INVALID`). Eski kod defterde (tür başına, emekliler dahil) zaten varsa → 409
  `RECORD_CODE_CONFLICT` (defter ile kayıt tutarsız; hiçbir şey yazılmaz — DL-6 / operasyon konusudur). Adım 8 (beklenen kod) ve adım 9
  (kilit) bu kolda yoktur (satır yok; PLN'de kilit yok — FU-RCS-27).
- **Adım 10–13 aynen:** gerekçe 10–500 (400 `RECORD_CODE_REASON_REQUIRED`); yeni kodun biçimi ve desen yasağı (400
  `RECORD_CODE_MANUAL_FORMAT_INVALID` / `RECORD_CODE_PATTERN_RESERVED`); yeni kod eski koddan farklı (400 `RECORD_CODE_UNCHANGED`); yeni kod
  defterde ya da dış yoklamada doluysa 409 `RECORD_CODE_TAKEN`. Eski kodun kendisi biçim denetiminden geçmez (motordan önce verilmiştir).
- **Adım 15 (tüketicinin işlemi, tek işlem):** (i) `Legacy` satırı `Active` eklenir (`Code` = eski kod, `Reason = null`, `ClaimedAtUtc` =
  `RecordCreatedAtUtc`, `ClaimedBy` = kullanıcı — Bölüm 4.6); (ii) DL-3 değişiklik çekirdeği aynen: `RetireIfActiveAsync` (beklenen kod =
  eski kod) satırı `Retired` yapar (`RetiredAtUtc` = şimdi, `RetiredBy` = kullanıcı, `ReplacedByCode` = yeni kod); (iii) yeni satır `Active`
  / `Manual` + `Reason`; (iv) Tablo F-P (önce = eski kod, sonra = yeni kod, gerekçe; `RetiredClaimId` = `Legacy` satırının kimliği). Ayrı
  bir sahiplenme satırı (Tablo E-P) yazılmaz — deftere alım değişikliğin parçasıdır, olay `platform.record-code.changed`'dir. Sayaç
  değişmez.
- **Çakışmalar:** `Legacy` satırı eklemesinde E11000 (herhangi index) → işlem iptal, 409 `RECORD_CODE_CONFLICT`; emeklilik eşleşmezse
  DL-3 kuralı (409 `RECORD_CODE_CONFLICT`); yeni satır eklemesinde `ux_…_type_code` → 409 `RECORD_CODE_TAKEN` (DL-3 kuralı,
  `ChangeRecordCodeHandler.cs:166-170`). Aynı eski planın eşzamanlı iki ilk değişikliğinde Mongo yazma çakışması (WriteConflict 112 /
  `TransientTransactionError`) birinin gövdesini iptal eder ve yürütücü gövdeyi baştan koşturur
  (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/PlatformTransactionExecutor.cs:50-56`, `:109-114`); yeniden koşuda
  plan ve defter yeniden okunur — kazananın satırı artık aktiftir, ikinci istek DL-3 koluna girer (beklenen kod = kazananın kodu) ya da kod
  aynıysa değişiklik yoktur. Bu, bugünkü plan güncellemesinin "son yazan kazanır" davranışıdır (planda sürüm denetimi yok —
  `UpdateSubscriptionPlanCommandHandler.cs:34-69`); defterde hiçbir kodun iki aktif satırı ve hiçbir eski kodun iki satırı olmaz (index'ler).
- **Neden yalnız değişiklik anında:** değişmeyen eski kod plan koleksiyonunda durdukça dış yoklama onu korur (madde 5); kod değişince plan
  koleksiyonu eski kodu artık taşımaz — defter satırı olmazsa eski kod başka bir plana verilebilirdi (§5.8 ihlali). Alım bu boşluğu kapatır;
  genel alım (değişmeyen eski kodlar, TTY) DL-6'da kalır.

**6. PLN bağlanması (MOD-0361; XMC-001 sahip onaylı, Q-26; Q-24 B kararı, r17; F-1 c, F-2 kararları, r18).** Sahip kararı: PLN'de bugünkü davranış korunur —
güncelleme kodu değiştirebilir, bugünkü normalleştirici aynen uygulanır, ayrı `CodeChange` üyesi ve boşluk / alt çizgi reddi yoktur.
RCS-001'in İKİSİ DE kuralları (§2 gerekçe zorunlu, denetimde eski / yeni kod + gerekçe; §5.8 kod yeniden kullanılmaz; §5.9 desen yasağı;
§5.10 ASCII; §6 tekillik) bu yolda da motorla sağlanır: kod değişikliği sayaç, sahiplik defteri ve denetimle tutarlıdır.

- **Normalleştirme (tek sıra):** tüketici önce bugünkü `SubscriptionPlanCodeNormalizer.Normalize`'ı uygular — kırp, boşluk / alt çizgi →
  `-`, ardışık `-` teke, baştaki / sondaki `-` atılır, `ToUpperInvariant`
  (`services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/SubscriptionPlanCodeNormalizer.cs:15-29`; dosya
  değişmez) — ve sonucu motora elle kod olarak verir; motorun `RecordCodeManualCode` kuralı ondan **sonra** koşar (Türkçe harf → ASCII,
  `^[A-Z0-9]+(?:-[A-Z0-9]+)*\z`, 2–40, desen yasağı — Bölüm 8.17). Normalleştiricinin çıktısında boşluk ve alt çizgi kalmadığından motorun
  bu iki reddi PLN'de tetiklenmez (sahip kararı). Normalleştiricinin dokunmadığı işaretler (nokta, `/` vb.) RCS-001 §5.10 gereği 400
  `RECORD_CODE_MANUAL_FORMAT_INVALID`; `PLN 000123` ve `PLN_000123` normalleştirmeden sonra `PLN-000123` olduğu için 400
  `RECORD_CODE_PATTERN_RESERVED`. **Motora tür başına normalleştirme eklenmez:** r16'daki B seçeneği metni "motor tür başına normalleştirme
  alır" diyordu; aynı sonuç tüketicinin bugünkü dosyasını motordan önce çağırmakla alınır ve motor değişmez (**r17 yorumu (4)**, Bölüm 24.3
  Q-24 notu). Bugünkü üst sınır 50 (`SubscriptionPlanRequestValidator.cs:22`) motorun 2–40 sınırına (Q-12 A) iner: yeni elle kod ve
  değişiklikte 41–50 karakter 400 `RECORD_CODE_MANUAL_FORMAT_INVALID`; değişmeyen eski kodlar yeniden doğrulanmaz (kod aynı geldiğinde motor
  çağrılmaz) ve eski kod değişikliğinde yalnız **yeni** kod bu kurala uyar (madde 5a) — sürüm notu (madde 8). Daralmalar (üst sınır 40,
  nokta reddi) r17 yorumu (3)'tür — **CT kabul (karar kaydı satır 152-153, r18)**.
- **Oluşturma:** koordinatör gövdesinde (`CreateSubscriptionPlanCommandHandler.cs:49`) bugünkü kod tekillik denetiminin (`:53`) yerine
  `ClaimWithinAsync(session, new("PLN", plan.Id, normalleştirilmiş kod ya da null, request.CodeReason, ExternalCodeTaken))`; boş kod →
  sistem kodu `PLN-…`; başarıda `plan.Code = sonuç.Code`; ret → `GlobalApplicabilityMutation(…, false)` ile hiçbir şey yazılmaz.
- **Güncelleme (Q-24 B; F-2 kararı, r18):** boş kod bugünkü gibi **400 "Code is required."** — kod kuralı (`NotEmpty` + normalleştirilmiş
  2–50, `SubscriptionPlanRequestValidator.cs:19-23`) güncelleme doğrulayıcısında aynen kalır (aşağıda "Doğrulayıcı"); cevap bugünkü
  `ValidationProblemDetails`, işleyici çağrılmaz, denetim satırı bugünkü gibi yazılmaz (karar kaydı satır 149: "bugünkü davranış korunur").
  r17'deki "boş kod saklı kodu korur" yorumu (Q-24 (5)) kaldırıldı. Dolu `request.Code` aynı normalleştiriciden geçer. Koordinatör gövdesi
  planı oturumla okur (`UpdateSubscriptionPlanCommandHandler.cs:34`), sonra:
  - normalleştirilmiş kod saklı koda `Ordinal` eşit → kod değişikliği yok, motor çağrılmaz; dolu `CodeReason` → 400
    `RECORD_CODE_REQUEST_INVALID`, hiçbir şey yazılmaz (sessiz yok sayma yok — DL-3 adım 6 kuralı); diğer alanlar bugünkü gibi;
  - farklı → aynı koordinatör işleminde `ChangeWithinAsync(session, new("PLN", plan.Id, ExpectedCurrentCode: işlemde okunan saklı kod,
    NewCode: normalleştirilmiş kod, Reason: request.CodeReason, ExternalCodeTaken, LegacyAdoption: new(plan.CreatedAt.UtcDateTime)))`.
    Planın **aktif platform sahiplik satırı yoksa** (eski plan: tohum `FREE` … ve DL-4 öncesi açılmış her plan) motor eski kodu aynı
    işlemde deftere alır (`Legacy` satırı, emekli) ve değişikliği yazar — madde 5a, F-1 c; PLN'de 409 `RECORD_CODE_IMMUTABLE` kolu **yoktur**
    (r17'deki kol kalktı). Aktif satır varsa DL-3 kolu. İki durumda da: gerekçe 10–500 (yoksa 400
    `RECORD_CODE_REASON_REQUIRED`); biçim / desen (adım 11); tekillik — defterde aynı türde emekliler dahil her kod ve dış yoklamada eski
    plan kodları (adım 13, 409 `RECORD_CODE_TAKEN`; bugünkü `ExistsByCodeAsync(session, normalizedCode, plan.Id)` denetimi, `:36`, bunun yerine
    geçer); başarıda eski sahiplik (DL-3 satırı ya da yeni `Legacy` satırı) `Retired` (`ReplacedByCode` = yeni kod), yeni sahiplik `Active` /
    `Manual` + `Reason`; `plan.Code = sonuç.Code`; plan, izdüşüm (`_state.UpsertSubscriptionPlanAsync`; izdüşüm kodu taşır —
    `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/GlobalApplicabilityStateRepository.cs:33`, F-3),
    koordinatörün `UpdateSubscriptionPlanCommand` denetim satırı (önce / sonra kod dahil — `GlobalApplicabilityAuditChange.cs:46`) ve Tablo
    F-P **tek işlemde** commit olur ya da hiçbiri olmaz; motor reddi → `GlobalApplicabilityMutation(…, false)`, hiçbir şey yazılmaz (eski
    planda `Legacy` satırı da yazılmaz).
  - **Sayaç:** elle kod ve kod değişikliği platform sayacına dokunmaz (DL-3 kuralı); sayaç yalnız kodsuz oluşturmada ilerler.
  - **Eşzamanlılık:** beklenen kod koordinatör işleminde okunan saklı koddur; aynı işlem denemesinde araya giren bir değişiklik
    `RetireIfActiveAsync` filtresini tutturmaz → 409 `RECORD_CODE_CONFLICT` (DL-3 değişiklik kuralı), plan değişmez; Mongo yazma çakışmasında
    yürütücü gövdeyi yeniden koşturur ve değerlendirme yeniden okunan durumla yapılır (madde 5a).
- **Yeni komut ve ayrı üye yok:** değişiklik bugünkü `UpdateSubscriptionPlanCommand`'ın `Code` alanıyla gelir; istek kaydına yalnız
  eklemeli düz `CodeReason` (oluşturmadaki adla aynı) eklenir. Plan komutları `ITransactionOwnedAuditCommand`'dır ve korunan
  `TransactionOwnedAuditCommands.Authorized` listesinde (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditCommands.cs:43-47`)
  ve `GlobalApplicabilityTransactionalArchitectureTests.ExactCommands`'tadır — değişmez.
- **Gerekçe alanı neden var (r17 yorumu (1) — CT kabul, karar kaydı satır 152-153, r18):** sahip kararı ayrı `CodeChange` üyesini kaldırır;
  RCS-001 §2 İKİSİ DE değişikliğinde gerekçeyi zorunlu tutar ve karar metni bunu kaldırmaz. Düz `CodeReason` iki kuralı birlikte karşılar
  (Bölüm 24.3 Q-24 notu). F-1 (c) karar metni de eski kodun değişikliğini "gerekçeyle (RCS-001 §2, 10–500)" diye yazar.
- **Doğrulayıcı (r18, F-2):** kod kuralı (`SubscriptionPlanRequestValidator.cs:19-23`) ortak doğrulayıcıdan kalkar ve
  `UpdateSubscriptionPlanCommandValidator.cs`'e **aynen** taşınır (XMC-001 ek onayı açık — Bölüm 5, 18.1): oluşturmada boş kod sistem kodu alır
  ve elle kod retleri işleyicide verilir ki denetime yazılsın; güncellemede boş kod ve normalleştirilmiş 2–50 dışı kod bugünkü gibi 400
  (`ValidationProblemDetails`). Normalleştirilmiş 41–50 karakterli **farklı** kod doğrulayıcıdan geçer, motorda 400
  `RECORD_CODE_MANUAL_FORMAT_INVALID`; aynı kalan 41–50 karakterli eski kod motora gitmez (kabul).
- **Tohum** (`SeedDefaultSubscriptionPlansCommandHandler.cs:36`, `FREE` …) değişmez; tohum kodları eski kod sayılır (FU-RCS-23) — kod
  güncellemede değişince madde 5a ile deftere alınır (F-1 c; karar metni "tohumlar dahil").
- **Kullanım kilidi yok:** kiracıya bağlanmış planın kodu da gerekçeyle değişebilir (bugünkü davranış; FU-RCS-27).

**7.** Hız sınırı yok (DL-2 / DL-3 gerekçesi: kimlik bilgisi yok, her çağrı kullanıcı başlatır). Arka plan çağıranı yok (DL-9).

**8. Sürüm notu (DL-4; Bölüm 8.16 deseni, kabul kaydına yazılır).** Görev türü: boş kod sistem kodu alır; grup öneki tanımlı olmayan
kiracıda form kodu ve gerekçeyi zorunlu tutar (Q-25 B); elle kod gerekçe ister; karakter kuralı daraldı (boşluk, alt çizgi, nokta
reddedilir; Türkçe harf ASCII'ye çevrilir); kod, görev açılmamışsa gerekçeyle değişebilir. Abonelik planı: boş kod `PLN-…` alır; elle kod
gerekçe ister; güncelleme kodu bugünkü gibi değiştirebilir ve boşluk / alt çizgi bugünkü gibi `-`'ye çevrilir (Q-24 B), ama kod
değişikliği gerekçe ister ve kod geçmişine yazılır; eski planların (tohum dahil) kodu da gerekçeyle değişebilir — ilk değişiklikte eski
kod kod geçmişine "eski kod" olarak yazılır ve bir daha hiçbir plana verilemez (F-1 c); boş kodlu güncelleme bugünkü gibi 400 "Code is
required." (F-2); üst sınır 50'den 40'a iner (yalnız yeni ya da değişen kodda); Türkçe harf ASCII'ye çevrilir, diğer işaretler (nokta vb.) reddedilir; `PLN-` + 6 hane desenine uyan elle kod
reddedilir.

### 1.4 Kabul ölçütleri — paket §16 (AC-25 ve AC-76…AC-91)

AC-25 (DL-1'den taşındı; AC-86 ve AC-87 ile ölçülür):

- [ ] AC-25 (zorunlu) — **(DL-4, K-11)** `PLN` bağlanmasıyla Platform kapsamı açıldığında: yalnız izinli kapsamı `Platform` olan tür için, kiracısız sayaç/defter ve platform-global denetim kaydıyla öneksiz `PLN-000001` biçiminde kod verilir; kiracı kapsamlı okumalar kiracısız satırı görmez. DL-1'de bu madde uygulanmaz ve DL-1 kabulünde ölçülmez. **r16:** madde metni değişmez; DL-4'te AC-86 (kiracı okumaları kiracısız satırı görmez) ve AC-87 (öneksiz `PLN-000001`, kiracısız sayaç / sahiplik, platform-global denetim kaydı) ile ölçülür; aşağıdaki sabotajın somut biçimi S-127, denetim kolu S-128.
  Sabotaj: (DL-4) kiracı kapsamlı sorguya kiracısız satırı katan filtre konunca kiracı testi kırmızı.

**DL-4 — TTY ve PLN tüketicileri, Platform kapsamı (r16).** Numaralar AC-75'ten devam eder; AC-25 (DL-1'den taşındı) metni değişmez,
AC-86 ve AC-87 onu ölçer. Her madde **üretim koduyla** ölçülür: üretim kataloğu, üretim işleyicileri (tüketici uçları gerçek HTTP'den),
üretim depoları gerçek Mongo replica set'te, üretim `CanonicalTransactionalAuditOutboxWriter` ve kutu işleyicisi; sahte yalnız principal /
izin bağlamında ve depoyu **saran** kaydedici / hata enjekte eden sarmalayıcıda (DL-3 deseni — bariyer yok, sıralı). Maddeler Bölüm
24.3'teki **sahip / CT kararlarıyla** yazıldı (r17; "Q-n" etiketi kararı anar). Q-24 ve Q-25'te seçim r16 önerisinden farklıydı (B):
AC-83, AC-84, AC-88 ve AC-90 r17'de hizalandı. **r18:** r17 incelemesinin sahip kararlarıyla (F-1 c, F-2) AC-82, AC-88 ve AC-90
hizalandı; yeni AC yok. Testler Bölüm 17'deki sınıflardadır; her maddenin sabotajı maddenin içinde ve Bölüm 17 sabotaj listesinde
(S-117…S-132; r17: S-134, S-135; r18: S-136, S-137, S-138).

- [ ] AC-76 (zorunlu, DL-4; sabotaj S-117; Q-18 / Q-19 A kararı) — **Katalog — `PLN` ve değişmeyen HTTP yüzeyi:** `RecordCodeTypeCatalog`'da `PLN` satırı `Class = "İKİSİ DE"`, `AllowedScopes = [Platform]`, `AllowedCaller = null`, `RequiredPermission = null` (DL-1 `AC09_…` değişmeden yeşil) ve `PlatformBinding = ("Diten.Platform", "platform.subscription-plans.create")`; bu anahtar `SubscriptionPlansController.Create` eylemindeki `[HasPermission]` değeriyle birebirdir (yansıma, sabit metin değil); başka hiçbir satırda `PlatformBinding` yoktur. `TrustedRecordCodeCredentialAuthenticator.Consumers` `PLN` içermez: iç uca her iki kimlik bilgisiyle `PLN` → 403 `RECORD_CODE_FORBIDDEN`; izinli bir türle (`PAY`, `BNK`) `prefixScope = Platform` → 422 `RECORD_CODE_SCOPE_INVALID` (DL-1 AC-8 değişmeden); sayaç, defter ve platform koleksiyonlarında satır yoktur. Testler `RecordCodeTypeCatalogTests.AC76_pln_is_a_platform_type_bound_to_the_in_process_platform_caller`, `RecordCodeTypeCatalogTests.AC76_the_pln_permission_is_the_plan_controller_key`, `InternalRecordCodesHttpTests.AC76_platform_scope_stays_closed_over_http`.
  Sabotaj: `PLN` FinanceTreasury bağlamasına eklenince HTTP testi (403 yerine 422 / 200); `PLN` kapsamına `Group` eklenince kapsam adımı; `PlatformBinding.RequiredPermission` `platform.subscription-plans.read` yapılınca yansıma adımı kırmızı (S-117).
- [ ] AC-77 (zorunlu, DL-4; sabotaj S-118; Q-20 A kararı) — **TTY oluşturma — sistem kodu:** grup öneki `GMG`'ye hizalı kiracıda `platform.tasks.task-types.manage` taşıyan kiracı kullanıcısı kodsuz `POST /api/v1/tasks/task-types` → 201, `CreateTaskTypeResultDto.Code = "GMG-TTY-000001"`; görev türü kaydının `Code`'u aynı; `record_code_claims`'te `RecordId` = görev türü kimliği, `Active` / `System`, `Reason = null`; `audit_events`'te Tablo A (DL-1 ayırması, kendi işleminde) ve Tablo E (görev türü yazımıyla **aynı** işlemde). İkinci kodsuz tür `GMG-TTY-000002`. **İptal:** `IPlatformTransactionFaultProbe.BeforeCommitAsync` görev türü işlemini düşürünce görev türü, sahiplik satırı ve Tablo E yoktur; DL-1 ayırma satırı kalır (boşluk, §5.8); cevap 503 `RECORD_CODE_UNAVAILABLE`. Testler `TaskTypeRecordCodeHttpTests.AC77_*`.
  Sabotaj: Tablo E görev türü işleminin commit'inden sonra ayrı çağrıyla yazılınca iptal adımı Tablo E satırını görür — kırmızı (S-118).
- [ ] AC-78 (zorunlu, DL-4; sabotaj S-119; Q-9 A, Q-22 A kararı) — **TTY oluşturma — elle kod ve gerekçe:** `Code = "denetim"`, `CodeReason = "Ekip kısaltması kullanılıyor"` → 201, kayıtta `DENETIM`; sahiplik `Active` / `Manual`, `Reason` kırpılmış metin; sayaç ve DL-1 defteri dokunulmaz; tek Tablo E. Retler (görev türü yazılmaz, sahiplik yok, motorun Tablo H satırı adım numarasıyla): gerekçesiz → 400 `RECORD_CODE_REASON_REQUIRED`; `"A_B"` → 400 `RECORD_CODE_MANUAL_FORMAT_INVALID`; 41 karakter → 400 `RECORD_CODE_MANUAL_FORMAT_INVALID` (bugünkü 40 sınırıyla aynı sonuç, kural motorda); `"GMG-TTY-000050"` → 400 `RECORD_CODE_PATTERN_RESERVED`; sahiplik satırı olmayan **eski** bir görev türü `DENETIM`'i taşıyorsa (emekli tür dahil) → 409 `RECORD_CODE_TAKEN` (dış yoklama, `EvaluationStep = 13`); başka bir türün emekli sahiplik kodu → 409 `RECORD_CODE_TAKEN`; kodsuz istekte dolu `CodeReason` → 400 `RECORD_CODE_REQUEST_INVALID`. Testler `TaskTypeRecordCodeHttpTests.AC78_*`.
  Sabotaj: tüketici `ExternalCodeTaken` yoklamasını bağlamayınca (`null`) eski kod adımı motor ön denetimini geçer, görev türü index'inde E11000 alır ve 409 `RECORD_CODE_TAKEN` yerine 500 / 503 döner — kırmızı (S-119).
- [ ] AC-79 (zorunlu, DL-4; sabotaj S-120; Q-20 A kararı) — **TTY atomikliği ve yeniden deneme:** (a) elle kodlu oluşturmada sarmalayıcı görev türü eklemesinde bir kez `MongoException` türevi atar → 503; görev türü, sahiplik ve Tablo E yok; aynı `DENETIM` ile tekrar → 201 (kod yanmadı); (b) sistem kodlu oluşturmada aynı hata → 503; DL-1 ayırma satırı `GMG-TTY-000001` kalır, yeni deneme yeni görev türü kimliğiyle `GMG-TTY-000002` alır; hiçbir kodun iki sahiplik satırı yoktur; (c) işlem içi kapı `false` → 503, (a) ile aynı sonuç. Testler `TaskTypeRecordCodeHttpTests.AC79_*`.
  Sabotaj: elle kodlu sahiplenme oturumsuz `ClaimAsync` ile (tüketici işleminin dışında) yazılınca (a)'nın tekrar adımı 201 yerine 409 `RECORD_CODE_TAKEN` alır — kırmızı (S-120).
- [ ] AC-80 (zorunlu, DL-4; sabotaj S-121; Q-20 A kararı) — **TTY gerekçeli kod değişikliği; güncelleme kodu değiştirmez:** `PUT /api/v1/tasks/task-types/{id}` farklı `Code` ile → 400 `TASK_TYPE_CODE_IMMUTABLE` (bugünkü kural, değişmez). `PUT /api/v1/tasks/task-types/{id}/code` `{ expectedCurrentCode: "GMG-TTY-000001", newCode: "denetim", reason: "Ekip kısaltmasına geçildi", expectedVersion }` → 200; görev türünün `Code`'u `DENETIM`, `Version` bir artar; eski sahiplik `Retired`, yeni `Active` / `Manual`; tek Tablo F (eski kod, yeni kod, gerekçe, kişi, zaman); `GET …/{id}/code-history` iki satır. Görev türü sürümü eşleşmezse (araya başka düzenleme girdi) → 409 `TASK_CONCURRENCY_CONFLICT`; emeklilik geri alınır, Tablo F yok. Eskimiş `expectedCurrentCode` → 409 `RECORD_CODE_CONFLICT`; gerekçesiz → 400 `RECORD_CODE_REASON_REQUIRED`; desenli yeni kod → 400 `RECORD_CODE_PATTERN_RESERVED`. Testler `TaskTypeRecordCodeHttpTests.AC80_*`.
  Sabotaj: görev türü kod güncellemesi değişiklik işleminin dışında yazılınca sürüm farkı adımında sahiplik `DENETIM` olur, kayıt eski kodda kalır — kırmızı (S-121).
- [ ] AC-81 (zorunlu, DL-4; sabotaj S-122; Q-21 C kararı) — **TTY kullanım kilidi:** sistem kodlu türle bir görev açıldıktan sonra (`POST /api/v1/tasks`, `taskTypeId`) kod değişikliği → kod değişikliği ucu türün görevi olduğunu (silinmişler dahil) okur, sahipliği kilitler (`LockedAtUtc` dolu, tek Tablo G) ve 409 `RECORD_CODE_LOCKED` döner; görev türü ve sahiplik kodu değişmez; ikinci deneme yine 409, yeni Tablo G yok (`LockedNow = false`). Görevi olmayan türün kodu değişir (AC-80). Görev oluşturma bugünkü gibi çalışır, `CreateTaskItemHandler` değişmez (`git diff`). Testler `TaskTypeRecordCodeHttpTests.AC81_*`.
  Sabotaj: kod değişikliği ucundaki görev varlığı denetimi kaldırılınca görevli türün kodu değişir ve adım 409 yerine 200 alır — kırmızı (S-122).
- [ ] AC-82 (zorunlu, DL-4; sabotaj S-123, S-137; Q-22 A kararı; RCS-001 §2; r18 F-1 c sınırı) — **TTY eski kodlar ve çakışma savunması:** sahiplik satırı olmayan eski tür (`CAPA`) için kod değişikliği → 409 `RECORD_CODE_IMMUTABLE`, kayıt ve defter değişmez, `Failed` ret satırı; geçmiş ucu → 404 `RECORD_CODE_NOT_ASSIGNED`; eski türün güncellemesi kodu değiştirmeden bugünkü gibi 200. **Kiracı kolu eski kod almaz (r18):** motorun kiracı kolunda `ChangeWithinAsync` `LegacyAdoption` doluyken (`TTY`, aktif satırı olmayan kayıt) → 400 `RECORD_CODE_REQUEST_INVALID` (`EvaluationStep = 6`), kiracı sahiplik deposuna ekleme ve emeklilik çağrısı yok (depoyu saran kaydedici), Tablo H ret satırı; F-1 (c) yalnız PLN'nin platform kolundadır. **Çakışma:** kiracıda sahiplik satırı olmayan `GMG-TTY-000001` kodlu eski tür varken kodsuz oluşturma — ayrılan `000001` dış yoklamada dolu, ayırma `tty:{id:N}:2` anahtarıyla tekrarlanır, kayıt `GMG-TTY-000002` alır; DL-1 defterinde iki satır (ilk numara boşluk). Eski `000001`, `000002`, `000003` doluysa üç denemeden sonra 409 `RECORD_CODE_COLLISION`, görev türü yazılmaz. Testler `TaskTypeRecordCodeHttpTests.AC82_*`, `InProcessRecordCodeAssignmentsTests.AC82_the_tenant_branch_refuses_a_legacy_adoption`.
  Sabotaj: çakışma atlaması aynı ayırma anahtarını yeniden kullanınca (DL-2 S-92 emsali) "`000002`" adımı aynı `000001`'i geri alır, görev türü index'inde E11000 → kırmızı (S-123); kiracı kolu `LegacyAdoption`'ı kabul edip eski kod alımını koşturunca kiracı kolu adımı 400 yerine 200 alır ve kaydedici `Legacy` satırı eklemesini görür — kırmızı (S-137).
- [ ] AC-83 (zorunlu, DL-4; sabotaj S-124, S-135; Q-25 B kararı, r17) — **Grup öneki önkoşulu, önizleme ucu ve elle kod zorunluluğu:** hizalanmamış kiracıda kodsuz oluşturma → 409 `GROUP_PREFIX_MISSING` (görev türü yok; sunucu kuralı değişmez); `GET /api/v1/tasks/task-types/code-preview` → aynı 409; elle kod + gerekçeyle oluşturma → 201, kayıtta normalleştirilmiş kod, sahiplik `Active` / `Manual`, tek Tablo E, sayaç ve DL-1 defteri dokunulmaz (AC-78 retleri aynen: gerekçesiz 400 `RECORD_CODE_REASON_REQUIRED`, `"A_B"` 400 `RECORD_CODE_MANUAL_FORMAT_INVALID`, `"GMG-TTY-000050"` 400 `RECORD_CODE_PATTERN_RESERVED`). `OrgPrefix` `GMX`, ayar `GMG` iken → 409 `GROUP_PREFIX_MISMATCH` (iki ana makine, DL-1 AC-35 deseni). Hizalı kiracıda önizleme iki kez → ikisi de `{ suggestedCode: "GMG-TTY-000001", isReserved: false }`; sayaç, defter, sahiplik ve `audit_outbox` satırı yok (DL-3 AC-65 ile aynı). **Form (Q-25 B):** Web denetleyicisinin önizleme cevabını form durumuna çeviren özel statik yardımcısı — önizleme 409 `GROUP_PREFIX_MISSING` ya da `GROUP_PREFIX_MISMATCH` → `CodeRequired = true`, yer tutucu yok, uyarı anahtarı `RecordCodeManualRequired`; önizleme 200 → `CodeRequired = false`, yer tutucu önerilen kod; önizleme 503 ya da başka ret → `CodeRequired = false`, yer tutucu yok. Bu bir canlıya alma kapısı değildir (Bölüm 18.1). Testler `TaskTypeRecordCodeHttpTests.AC83_*`, `TaskTypeRecordCodeFormTests.AC83_*` (yansıma — `TaskTypeConcurrencyFormTests` deseni).
  Sabotaj: önizleme ucu sistem sahiplenmesi ya da `NextAsync` çağırınca "satır yok" adımı; ayar yokken yedek önek kullanılınca 409 adımı kırmızı (S-124); form yardımcısı önizlemenin 409 `GROUP_PREFIX_MISSING`'inde `CodeRequired = false` döndürünce (kod isteğe bağlı kalır, kullanıcı yalnız sunucu reddiyle karşılaşır) form adımı kırmızı (S-135).
- [ ] AC-84 (zorunlu, DL-4; sabotaj S-125; Bölüm 11) — **TTY ekranı (7 dil):** `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `Index.cshtml` `Layout = "_LayoutTenantShell"` satırını değişmeden taşır; oluşturma formunda kod girişi hizalı kiracıda isteğe bağlıdır (`required`, `data-required` ve `*` yalnız `CodeRequired` dalında — koşulsuz yok), yer tutucusu önizlemenin önerdiği koddur; `CodeRequired` dalında kod ve gerekçe girişleri `required` ve `*` taşır ve `RecordCodeManualRequired` uyarısı görünür (Q-25 B, AC-83); kod yazılınca gerekçe alanı gönderilir; düzenleme ekranında kod salt okunur, ayrı "Kodu değiştir" bölümü `Edit/{id}/Code`'a gönderir; ayrıntı ekranı kod geçmişini gösterir, eski kodlu türde eski kod notu. Bölüm 11'deki anahtarlar `TaskTypesIndex.{en,tr,fr,es,zh,ar,ru}.resx`'in yedisinde de vardır ve `en` dışındaki her dilde değer `en`'den farklıdır; cevaptaki `reason_code` eşlenmiş bir anahtarsa yerelleştirilmiş metin gösterilir, İngilizce `errors[]` gösterilmez. Liste ekranı dosyaları ve `wwwroot/assets/js/Tasks/TaskTypes/index.js` değişmez. Test `task-type-record-code-form.test.js` (Web node; görünüm ve kaynak dosyası okuması).
  Sabotaj: bir dil dosyasından bir anahtar silinince ya da `fr` değeri `en` ile aynı yapılınca kaynak adımı; kod girişine koşulsuz `required` konunca (`CodeRequired` dalı dışında) form adımı kırmızı (S-125).
- [ ] AC-85 (zorunlu, DL-4; sabotaj S-126) — **TTY yetki ve kiracı izolasyonu:** üç yeni eylem `[HasPermission(TaskPermissions.TaskTypesManage)]` taşır (yansıma); anahtarı olmayan kiracı kullanıcısı → 403 `PERM_DENIED` (çerçeve), motor çağrılmaz; yeni izin anahtarı yoktur (PKS-001 kataloğu, rol köprüsü ve izin etiketi dil dosyaları `git diff`'te değişmez). A kiracısının kullanıcısı B'nin görev türü kimliğiyle kod değişikliği ya da geçmiş ister → 404 tek gövde (`TASK_NOT_FOUND`), B'nin kaydı ve sahiplik satırları değişmez (motorun kiracı yüklemi DL-3 AC-72 ile ölçülü). Testler `TaskTypeRecordCodeHttpTests.AC85_*`, `RecordCodeTypeCatalogTests.AC85_the_task_type_code_actions_carry_the_task_type_key`.
  Sabotaj: kod değişikliği eyleminden `[HasPermission]` kaldırılınca yansıma adımı ve 403 adımı kırmızı (S-126).
- [ ] AC-86 (zorunlu, DL-4; sabotaj S-127; Q-18 A kararı; AC-25'in "kiracı okumaları kiracısız satırı görmez" ölçümü) — **Kiracısız taban:** `record_code_platform_counters` ve `record_code_platform_claims` `GlobalEntity`, `SchemaProfile.RecordCodes`, Bölüm 4.6'daki adlı index'lerle kayıtlı; iki koleksiyonun belgelerinde `TenantId` alanı yok. Platform satırı `PLN-000001` varken kiracı sahiplik deposunun doğrudan `CodeExistsAsync(A, "PLN", "PLN-000001")` çağrısı `false`, `ListHistoryAsync(A, "PLN", planId)` boş; kiracı A'nın `PreviewAsync("TTY")` sonucu platform sayacından etkilenmez. Platform depolarının her yöntemi kiracı bağlamında (`IsPlatformContext == false`) `RecordCodePlatformScopeViolationException` atar ve hiçbir şey okumaz / yazmaz (testler istisnayı değil koleksiyonun satır sayısını ölçer). DL-1 AC-24 / AC-30 / AC-31 ve DL-3 AC-71 / AC-72 testleri değişmeden yeşil. Testler `PlatformRecordCodeMongoTests.AC86_*`.
  Sabotaj (AC-25'in sabotajının somut biçimi): kiracı sahiplik deposunun `CodeExistsAsync`'ine platform koleksiyonunu katan bir `$unionWith` eklenince kiracı adımı `true` görür; platform deposunun bağlam savunması kaldırılınca kiracı bağlamı adımı satır yazar — kırmızı (S-127).
- [ ] AC-87 (zorunlu, DL-4; sabotaj S-128; Q-19 A kararı; AC-25'in ana ölçümü) — **PLN oluşturma — öneksiz kod ve platform-global denetim:** `platform_admin` kodsuz `POST /api/platform/subscription-plans` → 201; plan kaydının `Code`'u `PLN-000001`, ikinci plan `PLN-000002`; `record_code_platform_claims`'te `RecordId` = plan kimliği, `Active` / `System`; `record_code_platform_counters` `LastNumber = 2`; aynı işlemden `audit_events`'e iki satır: küresel uygulanabilirlik koordinatörünün `CreateSubscriptionPlanCommand` satırı (bugünkü) ve Tablo E-P (`RequestType = ClaimRecordCodeCommand`, kayıt kiracısı `AuditTenantIds.PlatformSystemTenantId`, `TargetTenantId = null`, `ActorType = PlatformAdministrator`, `SourceService = Diten.Platform`, `Metadata.RecordType = PLN`, `Outcome = Succeeded`). Hiçbir kiracının `record_code_*` koleksiyonlarında satır yok. **İptal** (`BeforeCommitAsync`): plan, sahiplik, sayaç artışı ve iki denetim satırı birlikte yok — sayaç işlem içinde olduğundan boşluk da yok. Kod `RecordCodeFormat.IsValidPlatformCode`'a uyar. Testler `SubscriptionPlanRecordCodeHttpTests.AC87_*`.
  Sabotaj: Tablo E-P'nin kayıt kiracısı `Guid.Empty` yapılınca kapı reddeder ve 201 adımı 503 alır; `TargetTenantId` token kiracısıyla doldurulunca alan eşitliği adımı; platform sayacı işlem dışında artırılınca iptal adımı `LastNumber` artışını görür — kırmızı (S-128).
- [ ] AC-88 (zorunlu, DL-4; sabotaj S-129, S-134, S-136, S-138; Q-22 A, Q-24 B kararı, r17; F-1 c, F-2 kararı, r18) — **PLN elle kod, normalleştirme ve güncellemede gerekçeli kod değişikliği (eski planlar dahil):** oluşturma: `"Kurumsal Plus"` + gerekçe → 201 `KURUMSAL-PLUS` (bugünkü normalleştirici boşluğu `-`'ye çevirir — Q-24 B), tek Tablo E-P, sahiplik `Active` / `Manual`, platform sayacı değişmez; `"kurumsal_plus"` (başka planda `KURUMSAL-PLUS` varken) → 409 `RECORD_CODE_TAKEN`; `"Öğrenci"` + gerekçe → 201 `OGRENCI`; `"PLN 000123"` ve `"PLN_000123"` → 400 `RECORD_CODE_PATTERN_RESERVED` (normalleştirmeden sonra `PLN-000123`); `"Plan.Pro"` → 400 `RECORD_CODE_MANUAL_FORMAT_INVALID` (RCS-001 §5.10); normalleştirilmiş 41 karakter → 400 `RECORD_CODE_MANUAL_FORMAT_INVALID`; gerekçesiz elle kod → 400 `RECORD_CODE_REASON_REQUIRED`; başka planın emekli kodu → 409 `RECORD_CODE_TAKEN`; oluşturmada kod boş istek doğrulayıcıdan geçer (kod kuralı oluşturmadan kalktı). Güncelleme (`PUT /api/platform/subscription-plans/{id}`; başarı kodu bugünkü 204, `services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/UpdateSubscriptionPlanCommandHandler.cs:71`): aynı kodla yalnız ad değişikliği → 204, kod korunur, motor çağrılmaz, defterde yeni satır yok; **boş kod (F-2, r18):** `code` `null` ya da yalnız boşluk → 400, `ValidationProblemDetails` `errors` içinde "Code is required." (bugünkü cevap, `SubscriptionPlanRequestValidator.cs:21`'in metni), işleyici çağrılmaz, plan değişmez; `code = "___"` (normalleştirilmiş boş) → bugünkü 400 "Code must be between 2 and 50 characters after canonical normalization."; aynı kod + dolu `codeReason` → 400 `RECORD_CODE_REQUEST_INVALID`, plan değişmez; sistem kodlu (`PLN-000001`) planda `code = "kurumsal plus"` + `codeReason` → 204: plan kodu `KURUMSAL-PLUS`, küresel uygulanabilirlik izdüşümünün `Code`'u aynı, eski sahiplik `Retired` (`ReplacedByCode = KURUMSAL-PLUS`), yeni `Active` / `Manual` + `Reason`, tek Tablo F-P (eski kod, yeni kod, gerekçe, `PlatformAdministrator`) ve koordinatörün `UpdateSubscriptionPlanCommand` satırı aynı işlemde, platform sayacı değişmez, `GET …/{id}/code-history` iki satır; aynı değişiklik `codeReason`'sız → 400 `RECORD_CODE_REASON_REQUIRED`, plan (diğer alanlar dahil) değişmez; yeni kod sahiplik satırı olmayan eski bir planın koduysa → 409 `RECORD_CODE_TAKEN` (dış yoklama); **iptal** (`BeforeCommitAsync`) → plan, izdüşüm, sahiplik satırları ve iki denetim satırı eski hâlinde. **Eski plan (F-1 c, r18):** aktif sahiplik satırı olmayan tohum planı `FREE`'de yalnız ad değişikliği (kod aynı) → 204, platform defterinde plan için satır yok (Q-22 A); `code = "temel"` + `codeReason` → 204: plan kodu `TEMEL`, izdüşümün `Code`'u aynı; `record_code_platform_claims`'te plan için tam iki satır — `FREE` / `Legacy` / `Retired` (`Reason = null`, `ClaimedAtUtc` = planın `CreatedAt`'i (UTC), `RetiredBy` = kullanıcı, `ReplacedByCode = TEMEL`) ve `TEMEL` / `Manual` / `Active` + `Reason`; aynı işlemden tek Tablo F-P (önce `FREE`, sonra `TEMEL`, gerekçe, `PlatformAdministrator`, `Metadata.RetiredClaimId` = `Legacy` satırının kimliği) ve koordinatörün `UpdateSubscriptionPlanCommand` satırı; Tablo E-P yok; platform sayacı değişmez; `GET …/{id}/code-history` iki satır, sıra `FREE` → `TEMEL`, kökenler `Legacy`, `Manual`. Ardından yeni plan `code = "free"` + gerekçe → 409 `RECORD_CODE_TAKEN` (eski kod defterde dolu, §5.8). `FREE`'nin değişikliği `codeReason`'sız → 400 `RECORD_CODE_REASON_REQUIRED`, plan ve defter değişmez (`Legacy` satırı yok); eski planın değişikliği `BeforeCommitAsync` ile düşünce plan, izdüşüm, `Legacy` satırı, yeni satır ve iki denetim satırı yok; eski kod defterde zaten varken (test düzeni: platform defterine aynı türde başka kayıt için `PROFESSIONAL` satırı) `PROFESSIONAL` planının kod değişikliği → 409 `RECORD_CODE_CONFLICT`, plan ve defter değişmez. Bu akışın hiçbir adımı 409 `RECORD_CODE_IMMUTABLE` döndürmez. Testler `SubscriptionPlanRecordCodeHttpTests.AC88_*`.
  Sabotaj: güncelleme işleyicisi farklı kodu motoru atlayıp plan koduna doğrudan yazınca (bugünkü atama korunur) değişiklik adımı defterde emeklilik ve Tablo F-P görmez; değişiklik koordinatör işleminin dışında yazılınca iptal adımı yeni sahiplik satırını ya da izdüşümde eski kodu görür — kırmızı (S-129); normalleştirici motorun desen yasağından sonra uygulanınca ya da hiç uygulanmayınca `"PLN_000123"` adımı 201 ya da 400 `RECORD_CODE_MANUAL_FORMAT_INVALID` alır — kırmızı (S-134); eski kod alımı atlanınca (yalnız yeni `Manual` satırı eklenir, `Legacy` satırı yazılmaz) iki satır adımı tek satır görür ve `"free"` ile yeni plan 409 yerine 201 alır — kırmızı (S-136); kod kuralı güncelleme doğrulayıcısına taşınmayınca (oluşturmayla birlikte kalkınca) boş kod adımı "Code is required." yerine 400 `RECORD_CODE_REASON_REQUIRED` / `RECORD_CODE_MANUAL_FORMAT_INVALID` ya da 204 alır — kırmızı (S-138).
- [ ] AC-89 (zorunlu, DL-4; sabotaj S-130; Q-19 A kararı) — **PLN aktör ve bağlam:** platform kolunun savunması `ActorType = platform_admin` (`OrdinalIgnoreCase`), dolu `sub` ve `ITenantContext.IsPlatformContext == true` ister; değilse hiçbir şey göndermeden 403 `RECORD_CODE_FORBIDDEN` (sahte `ISender` çağrı sayısı 0, ret satırı yok). `partner_admin` token'ıyla plan oluşturma → 403 (bugünkü politika), motor çağrılmaz; kiracı kullanıcısı token'ı → 403 (ara katman). Kiracı kolu platform türünü kabul etmez: `tenant_user` ile `PLN` → 403 `RECORD_CODE_FORBIDDEN` (adım 3; DL-3 AC-73 değişmez); platform kolu kiracı türünü kabul etmez: `platform_admin` ile `TTY` → 403 `RECORD_CODE_FORBIDDEN`. Testler `InProcessRecordCodeAssignmentsTests.AC89_*`, `SubscriptionPlanRecordCodeHttpTests.AC89_*`.
  Sabotaj: platform savunmasından `IsPlatformContext` kolu kaldırılınca kiracı bağlamlı `platform_admin` adımı; `PLN` kiracı savunmasından geçirilince `tenant_user` adımı kırmızı (S-130).
- [ ] AC-90 (zorunlu, DL-4; sabotaj S-131; Bölüm 11; r18 F-1 c, F-2, F-4) — **PLN ekranı (2 dil):** `Create.cshtml` ve `Edit.cshtml` `Layout = "_LayoutPlatformAdmin"` satırını değişmeden taşır; oluşturmada kod isteğe bağlı (yer tutucu önizleme `PLN-…`; oluşturma dalında `required` yok), kod yazılınca gerekçe; düzenlemede kod girişi bugünkü gibi düzenlenebilir ve **zorunludur** (`required` kalır — F-2: boş kod 400) ve yanında gerekçe girişi (`CodeReason`) vardır — ayrı "Kodu değiştir" bölümü ve `codeChange` yok (Q-24 B, r17); **eski kodlu planda (aktif sahiplik yok) kod girişi salt okunur değildir** — kullanıcı kodu gerekçeyle değiştirebilir ve sunucu değişikliği kabul eder (F-1 c; r17'deki "değişirse 409" davranışı yok, F-4 kapandı); bu planda eski kod notu (`RecordCodeLegacy`, PLN metni: eski kod değişince kod geçmişine yazılır ve bir daha kullanılamaz) görünür ve geçmiş tablosu yerine not gösterilir; ilk değişiklikten sonra geçmiş tablosu düzenleme ekranında iki satırla, `Legacy` köken hücresi `RecordCodeOriginLegacy` ile; Bölüm 11'deki anahtarlar `SubscriptionPlansIndex.{en,tr}.resx`'in ikisinde de var, `tr` değeri `en`'den farklı; `reason_code` eşlemesi TTY ile aynı kural. Test `subscription-plan-record-code-form.test.js`.
  Sabotaj: `SubscriptionPlansIndex.tr.resx`'ten bir anahtar (ör. `RecordCodeOriginLegacy`) silinince; oluşturma dalında plan kod girişine `required` geri konunca ya da düzenleme dalından `required` kaldırılınca; eski kodlu planda kod girişi `readonly` yapılınca kırmızı (S-131).
- [ ] AC-91 (zorunlu, DL-4; sabotaj S-132) — **Takım ve değişmeyenler:** `dotnet build` Diten.Platform ve Diten.Web PASS; Platform'un tam test takımı filtresiz, Web node takımı ve `Diten.Web.Tests`; DL-1, DL-2, DL-3 kayıt kodu testleri değişmeden yeşil (DL-4 hiçbirini değiştirmez — `PLN` satırının mevcut alanları ve `AllowedCaller` kuralı korunur); Bölüm 5 "DL-4" **Değişmez** listesindeki dosyalar (sözleşme, `ocelot.json`, kimlik bilgisi eşlemesi, denetim kapısı, `TransactionOwnedAuditCommands.cs`, `AllocateRecordCodeHandler.cs`, `LockRecordCodeHandler.cs`, DL-2 dosyaları, `CreateTaskItemHandler.cs`, plan tohumu, `services/Diten.PpmService/**`) `git diff`'te değişmez; XMC dosyaları yalnız Bölüm 5 "DL-4" listesindekiler; atlanan test yok; bilinen 19 Platform Mongo ortam kırmızısının adları `ctv-0040dl2` tabanıyla aynı (yeni 0, kaybolan 0 — farklıysa adıyla, CT aynı gün main ile karşılaştırır); `Diten.Platform.Application` derlemesinde `ChangeTaskTypeCodeCommand` ve dört yeni sorgu için `IValidator<>` uygulaması yok (yansıma — AC-45 / AC-75 deseni). Testler `InProcessRecordCodeAssignmentsTests.AC91_the_dl4_requests_have_no_validator`; kabul kaydı.
  Sabotaj: yeni bir testin `Skip` ile işaretlenmesi kabul kaydında; `ChangeTaskTypeCodeCommand`'a FluentValidation doğrulayıcısı eklenince yansıma adımı kırmızı (S-132).

> **CT ek adımı (AC-88, bu WP §3 CT-1):** AC-88'e §3'teki "deftere alınmış PLN-desenli eski kod atlanır" adımı eklenir; sabotajı S-139-WP.

### 1.5 Test sınıfları ve sabotajlar — paket §17 (DL-4 satırları, S-117…S-138)

| Test sınıfı | Tür | Kapsadığı kabul ölçütleri |
|---|---|---|
| `RecordCodeTypeCatalogTests` (M, **DL-4**) | birim + yansıma (`SubscriptionPlansController.Create` ve `TasksController`'ın yeni eylemlerinin `[HasPermission]` değeri) | AC-76, AC-85 (yansıma) |
| `InternalRecordCodesHttpTests` (M, **DL-4**) | gerçek HTTP | AC-76 (`PLN` 403, `prefixScope = Platform` 422) |
| `TaskTypeRecordCodeHttpTests` (N, **DL-4**) | gerçek HTTP (`TasksController` üretim yolu) + üretim `TenantResolutionMiddleware`, `AuditBehavior`, `CanonicalTransactionalAuditOutboxWriter` ve kutu işleyicisi + gerçek Mongo replica set + görev türü ve sahiplik deposunu saran kaydedici / hata enjekte eden sarmalayıcı + `IPlatformTransactionFaultProbe` (iptal) + iki ana makine (`OrgPrefix` `GMG` / `GMX`) | AC-77…AC-83, AC-85 |
| `PlatformRecordCodeMongoTests` (N, **DL-4**) | gerçek Mongo + üretim platform ve kiracı depoları | AC-86 (AC-25) |
| `SubscriptionPlanRecordCodeHttpTests` (N, **DL-4**) | gerçek HTTP (`SubscriptionPlansController`, `PlatformActor`) + üretim küresel uygulanabilirlik koordinatörü, yazıcı ve kutu işleyicisi + üretim doğrulama hattı (`ValidationBehavior`, güncelleme doğrulayıcısı — r18 F-2) + gerçek Mongo (tohum planları üretim tohumuyla — r18 F-1 c) + `IPlatformTransactionFaultProbe` | AC-87, AC-88, AC-89 (HTTP kolu) (AC-25) |
| `InProcessRecordCodeAssignmentsTests` (M, **DL-4**) | birim (sahte `ISender` çağrı sayımı, sahte bağlamlar) + yansıma | AC-82 (r18: kiracı kolu `LegacyAdoption` reddi), AC-89 (platform savunması), AC-91 (doğrulayıcı yok) |
| `task-type-record-code-form.test.js`, `subscription-plan-record-code-form.test.js` (N, **DL-4**) | Web node (görünüm + kaynak dosyası okuması) | AC-84, AC-90 |
| `TaskTypeRecordCodeFormTests` (N, **DL-4**, r17) | yansıma — Web denetleyicisinin önizleme → form durumu yardımcısı (`TaskTypeConcurrencyFormTests` deseni) | AC-83 (form, Q-25 B) |
| Tam takım (Platform + Web node + `Diten.Web.Tests`) (**DL-4**) | CI | AC-91 |

| # | Sabotaj | Kırmızıya dönmesi gereken |
|---|---|---|
| S-117 | (r16, DL-4) `PLN` bir HTTP kimlik bilgisi bağlamasına eklenir / `PLN` kapsamına `Group` eklenir / `PlatformBinding.RequiredPermission` `platform.subscription-plans.read` yapılır | `InternalRecordCodesHttpTests.AC76_platform_scope_stays_closed_over_http`; `RecordCodeTypeCatalogTests.AC76_*` |
| S-118 | (r16, DL-4) Tablo E görev türü işleminin commit'inden sonra ayrı çağrıyla yazılır | `TaskTypeRecordCodeHttpTests`: AC-77 iptal adımı Tablo E satırını görür |
| S-119 | (r16, DL-4) Tüketici `ExternalCodeTaken` yoklamasını bağlamaz (`null`) | `TaskTypeRecordCodeHttpTests`: AC-78 eski görev türü kodu adımı 409 yerine 500 / 503 |
| S-120 | (r16, DL-4) Elle kodlu sahiplenme oturumsuz `ClaimAsync` ile, tüketici işleminin dışında yazılır | `TaskTypeRecordCodeHttpTests`: AC-79 (a) tekrar adımı 201 yerine 409 `RECORD_CODE_TAKEN` |
| S-121 | (r16, DL-4) Görev türü kod güncellemesi değişiklik işleminin dışında yazılır | `TaskTypeRecordCodeHttpTests`: AC-80 sürüm farkı adımı — sahiplik `DENETIM`, kayıt eski kodda |
| S-122 | (r16, DL-4; Q-21 C kararı) Kod değişikliği ucundaki görev varlığı denetimi kaldırılır | `TaskTypeRecordCodeHttpTests`: AC-81 görevli tür adımı 409 yerine 200 |
| S-123 | (r16, DL-4) Çakışma atlaması aynı ayırma anahtarını yeniden kullanır (DL-2 S-92 emsali) | `TaskTypeRecordCodeHttpTests`: AC-82 "`000002`" adımı |
| S-124 | (r16, DL-4) Önizleme ucu sistem sahiplenmesi / `NextAsync` çağırır / ayar yokken yedek önek kullanır | `TaskTypeRecordCodeHttpTests`: AC-83 "satır yok" adımı / 409 adımı |
| S-125 | (r16, DL-4; r17 Q-25 B) Bir dil dosyasından anahtar silinir / `fr` değeri `en` ile aynı yapılır / kod girişine koşulsuz `required` konur (`CodeRequired` dalı dışında) | `task-type-record-code-form.test.js` |
| S-126 | (r16, DL-4) Kod değişikliği eyleminden `[HasPermission]` kaldırılır | `RecordCodeTypeCatalogTests.AC85_the_task_type_code_actions_carry_the_task_type_key`; `TaskTypeRecordCodeHttpTests`: AC-85 403 adımı |
| S-127 | (r16, DL-4; AC-25 sabotajı) Kiracı sahiplik deposunun `CodeExistsAsync`'ine platform koleksiyonunu katan `$unionWith` eklenir / platform deposunun bağlam savunması kaldırılır | `PlatformRecordCodeMongoTests`: AC-86 kiracı adımı `true` görür / kiracı bağlamı adımı satır yazar |
| S-128 | (r16, DL-4) Tablo E-P kayıt kiracısı `Guid.Empty` yapılır / `TargetTenantId` token kiracısıyla doldurulur / platform sayacı işlem dışında artırılır | `SubscriptionPlanRecordCodeHttpTests`: AC-87 201 adımı 503 / alan eşitliği adımı / iptal adımı `LastNumber` |
| S-129 | (r16, DL-4; r17 Q-24 B; r18 F-1 c) Plan güncellemesinde farklı kod motoru atlayıp plan koduna doğrudan yazılır / kod değişikliği koordinatör işleminin dışında yazılır | `SubscriptionPlanRecordCodeHttpTests`: AC-88 değişiklik adımı (emeklilik ve Tablo F-P yok; eski planda `Legacy` satırı da yok) / iptal adımı (yeni sahiplik, `Legacy` satırı ya da izdüşümde eski kod) |
| S-130 | (r16, DL-4) Platform savunmasından `IsPlatformContext` kolu kaldırılır / `PLN` kiracı savunmasından geçirilir | `InProcessRecordCodeAssignmentsTests.AC89_*` |
| S-131 | (r16, DL-4; r18 F-1 c, F-2) `SubscriptionPlansIndex.tr.resx`'ten anahtar (ör. `RecordCodeOriginLegacy`) silinir / oluşturma dalında plan kod girişine `required` geri konur ya da düzenleme dalından kaldırılır / eski kodlu planda kod girişi `readonly` yapılır | `subscription-plan-record-code-form.test.js` |
| S-132 | (r16, DL-4) Yeni bir test `Skip` ile işaretlenir / `ChangeTaskTypeCodeCommand`'a FluentValidation doğrulayıcısı eklenir | kabul kaydı (AC-91); `InProcessRecordCodeAssignmentsTests.AC91_the_dl4_requests_have_no_validator` |
| S-133 | (r16, DL-3 hizalaması; CT kararı, r15 inceleme F-1@97058ee5) Kilit filtresinden `Code ==` (ön okunan kod) kaldırılır | `RecordCodeAssignmentMongoTests.AC70_a_code_changed_after_the_lock_s_read_is_a_conflict_and_nothing_is_locked` — yeni kod kilitlenir, 409 yerine 200 |
| S-134 | (r17, DL-4; Q-24 B) PLN'de bugünkü normalleştirici motorun desen yasağından sonra uygulanır ya da hiç uygulanmaz | `SubscriptionPlanRecordCodeHttpTests`: AC-88 `"PLN_000123"` adımı 201 ya da 400 `RECORD_CODE_MANUAL_FORMAT_INVALID` |
| S-135 | (r17, DL-4; Q-25 B) TTY form yardımcısı önizlemenin 409 `GROUP_PREFIX_MISSING`'inde `CodeRequired = false` döndürür | `TaskTypeRecordCodeFormTests.AC83_*` |
| S-136 | (r18, DL-4; F-1 c) Platform kolunun eski kod alımı atlanır: aktif satırı olmayan planın değişikliğinde yalnız yeni `Manual` satırı eklenir, `Legacy` satırı yazılmaz (emeklilik adımı da atlanır) | `SubscriptionPlanRecordCodeHttpTests`: AC-88 eski plan adımı defterde iki yerine tek satır görür; `"free"` ile yeni plan 409 `RECORD_CODE_TAKEN` yerine 201 alır |
| S-137 | (r18, DL-4; F-1 c sınırı, Q-22 A) Motorun kiracı kolu `LegacyAdoption`'ı reddetmez, eski kod alımını koşturur | `InProcessRecordCodeAssignmentsTests.AC82_the_tenant_branch_refuses_a_legacy_adoption`: 400 yerine 200 ve kaydedicide `Legacy` satırı eklemesi |
| S-138 | (r18, DL-4; F-2) Plan kod kuralı güncelleme doğrulayıcısına taşınmaz (oluşturmayla birlikte kalkar) | `SubscriptionPlanRecordCodeHttpTests`: AC-88 boş kod adımı "Code is required." yerine 400 `RECORD_CODE_REASON_REQUIRED` / `RECORD_CODE_MANUAL_FORMAT_INVALID` ya da 204 |
| S-139-WP | (bu WP, CT şartı CT-1; r18 inceleme F-1) Platform kolunda sistem kodu çakışma atlamasının koşulundan oturumlu defter `CodeExistsAsync` sorgusu kaldırılır (yalnız dış yoklama kalır) | `SubscriptionPlanRecordCodeHttpTests`: AC-88 CT adımı — `PLN-000002` atlanmaz, kodsuz oluşturma 201 `PLN-000003` yerine 409 `RECORD_CODE_TAKEN` alır |

S-133 (r16, DL-3 hizalaması; AC-70) DL-3 kabulünde koşuldu; DL-4 `RecordCodeRepositories.cs`'i ve DL-3 işleyicilerinin gövdesini
değiştirdiği için bu dilimde **yeniden koşulur**.

### 1.6 Kapılar — paket §18.1 (DL-4 satırları)

| Kapı | Koşul | Ne zaman | Sahip |
|---|---|---|---|
| DL-4 **başlama kapısı** (r14 bilgi; r16) | DL-3 kodu CT tarafından doğrulanmış ve DL-4'ün yazılacağı tabanda (Bölüm 23 dilim sırası: DL-4 DL-3'ü bekler) | **sağlandı** — DL-3 VERIFIED (`07ad98464`, 2026-11-14; `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`); bu ağaçta `git merge-base --is-ancestor 07ad98464 HEAD` başarılı | CT |
| DL-4 **başlama kapısı** (r16; r17; r18) | **r18'in** CT-APR-02 incelemesi (açık ENGEL / ORTA yok) ve r18 sha256'sına yazılmış CT-APR-01 onay satırı; künye `ready-for-dev` (r18'de sağlandı). r16 incelenmedi, r17 incelendi (KOŞULLU) — ikisi de onaylanmadı, r18 onların yerine geçer; onay satırı Q-17…Q-26 kararlarını, r17 inceleme kararlarını (F-1 c, F-2, A-22) ve Bölüm 24.3'teki r17 / r18 yorumlarını adıyla anar | DL-4 kodundan önce | CT + Beste |
| DL-4 **başlama kapısı** (r16; r17) | Q-17…Q-26 sahip / CT seçimleri (Bölüm 24.3) | **kapandı** — Beste 2026-11-14 (`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:121-139`); Q-24 B ve Q-25 B öneriden farklıydı → r17'de işlendi | Beste + CT |
| DL-4 **başlama kapısı** (r18) | r17 bağımsız incelemesinin sahip kararları (F-1, F-2, A-22; r17 yorumları) | **kapandı** — Beste 2026-11-14 (`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:141-153`, `3f57d2f60`); F-1 (c), F-2 (400 korunur), A-22 (PPM ekibi), Q-24 (1) / (3) ve Q-25 MISMATCH yorumları CT kabul → r18'de işlendi (Bölüm 19.10) | Beste + CT |
| DL-4 **başlama kapısı** (r16; r17) | XMC-001 (Q-26): Bölüm 5 "DL-4" XMC satırlarındaki MOD-0024 ve MOD-0361 dosyalarına dokunma onayı; bu dosyalarda açık bir dalda iş olup olmadığı (sıra) | **onay kapandı** — sahip onaylı (karar kaydı satır 137-138), yalnız Bölüm 5 "DL-4" listesi, liste dışı dokunuş yine sorulur; **sıra** DL-4 WP'si güncellenirken CT ölçer | Beste (onay, kapandı); CT (sıra) |
| DL-4 **başlama kapısı** (r18, F-2) | XMC-001 **ek onayı**: `services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Validators/UpdateSubscriptionPlanCommandValidator.cs` (MOD-0361) — yalnız ortak doğrulayıcıdan kalkan kod kuralının (`SubscriptionPlanRequestValidator.cs:19-23`) aynen taşınması (Bölüm 5). Q-26 onayı r16 listesiyle sınırlıydı; F-2 kararı ("400 korunur") bu dosyaya dokunmayı gerektirir ama dosyayı adlandırmaz | **açık** — DL-4 kodundan önce | Beste |
| DL-4 **kabul kapısı** (r16) | Canlı veride kiracı başına `^[A-Z]{2,5}-TTY-[0-9]{6,}$` ve `^PLN-[0-9]{6,}$` desenine uyan eski kod sayımı (Q-22) kabul kaydında; bir (kiracı, önek, tür) için sayım 3'ü aşarsa devreye almadan önce CT'ye bildirim (DL-6); sürüm notu (Bölüm 8.18 madde 8) kabul kaydında ve kiracı / Platform yöneticilerine yazılı bildirimi (r17: Q-25 B'den sonra kaldırılan canlıya alma kapısından buraya taşındı) | DL-4 kabul kaydı | CT |
| DL-4 **kabul kapısı** (r16; r17) | A-21 — **CT kararı: yeni belirteç açılmaz** (DL-3 yöntemlerinin aşırı yüklemesi, Bölüm 8.18 madde 2); kapı yalnız DL-4 kodu buna rağmen yeni bir `RecordCodeAuditIntent` belirteci açarsa doğar: defter satırı CT kabul koşusundan önce (A-20 emsali) | CT kabul koşusundan önce (kapı doğarsa) | CT |
| ~~DL-4 **canlıya alma kapısı** (r16)~~ | **kaldırıldı (r17, Q-25 B kararı):** grup öneki hizalanmamış kiracıda form kodu ve gerekçeyi zorunlu tutar (elle kod; AC-83); hizalama canlıya almayı bekletmez. Yönetici bildirimi DL-4 kabul kapısına taşındı | — | — |

> **CT notu:** "XMC-001 ek onayı … **açık**" satırı karar kaydı satır 157-158 ile kapandı (§2). "Sıra" ölçümü §2'de.

## 2. Sahip ve CT kararları (bağlayıcı)

- **DL-4 kararları:** Q-17…Q-26 (Beste 2026-11-14; `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:121-139`,
  `0a2518a53`) ve r17 inceleme kararları F-1 (c), F-2, A-22 + r17 yorumları (aynı dosya `:141-153`, `3f57d2f60`). A-21 (CT): yeni
  `RecordCodeAuditIntent` belirteci **açılmaz** (DL-3'ün dört yöntemi eklemeli aşırı yüklemeyle). Kod buna rağmen yeni belirteç gerektirirse
  dur ve CT'ye yaz (A-20 emsali kapı doğar).
- **Onay satırında adıyla anılan yorumlar (bağlayıcı uygulama seçimi):** Q-24 (1) düz `CodeReason`; Q-24 (3) PLN daralmaları (üst sınır 40,
  nokta reddi); Q-24 (4) normalleştirici motordan önce, motor değişmez; Q-25 `GROUP_PREFIX_MISMATCH` genişletmesi; `Legacy` satırının
  `ClaimedAtUtc`'si = plan `CreatedAt` (UTC).
- **XMC-001 (sahip onaylı):**
  - **Q-26** (karar kaydı satır 137-138): MOD-0024 (Görev Türü) ve MOD-0361 / CAND-CAP-0002 (Abonelik Planı) dosyalarına yalnız paket
    §5 "DL-4" listesiyle (§1.2) sınırlı dokunma.
  - **Ek onay** (karar kaydı "Ek — MOD-0040-FU02 r18 XMC ek onayı ve inceleme F-1", satır 155-158): `services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Validators/UpdateSubscriptionPlanCommandValidator.cs`
    — yalnız ortak `SubscriptionPlanRequestValidator`'dan kalkan kod kuralının (`NotEmpty` "Code is required." + normalleştirilmiş 2–50)
    **aynen** taşınması; davranış değişmez. `CreateSubscriptionPlanCommandValidator.cs` değişmez.
  - **Liste dışı her dokunuş** (başka MOD-0024 / MOD-0361 dosyası, MOD-0021 denetim bileşeni, Building.Blocks, PPM) önce CT'ye sorulur.
    `services/Diten.PpmService/**` (MOD-0117) **dokunulmaz** — DL-11.
- **Sıra (§18.1 "sıra DL-4 WP'si güncellenirken CT ölçer") — ölçüldü 2026-11-14, `87d5617ca`:** 527 ref'te `HEAD..<ref>` aralığında §1.2'deki
  TTY / PLN / RecordCodes / Web dosyalarına dokunan commit'ler yalnız `origin/main` birleştirme commit'leri ve eski / yedek dallardır
  (son birleştirme dışı commit 2026-10-19); bu dosyalarda açık, beklenen bir iş yok. Çakışma çıkarsa dur ve CT'ye yaz.
- **DL-3 sapmaları** (tarih alanları `DateTime` UTC, AC-68 örneği, kilit filtresi kodu taşır — S-133) r16'da pakete işlendi; DL-1 / DL-2 /
  DL-3 testleri **değişmeden** yeşil kalır (AC-91).
- **A-18:** TTY (Group) ve PLN (Platform) MDM kısa kodunu kullanmaz → DL-4'ü etkilemez (paket §2.6).
- **Dil kuralı:** TTY ekranları kiracı kabuğunda (`_LayoutTenantShell`) → **7 dil** (`TaskTypesIndex.{en,tr,fr,es,zh,ar,ru}.resx`; yeni anahtar
  `RecordCodeManualRequired` ve AC-84'ün anahtarları yedi dosyada, `fr`… değeri `en` ile aynı bırakılmaz). PLN ekranları Platform yönetici
  kabuğunda (`_LayoutPlatformAdmin`) → **2 dil** (`SubscriptionPlansIndex.{en,tr}.resx`). Bir dil eksikse iş bitmemiştir. Yeni izin anahtarı
  yok (paket §14) → rol köprüsü / PKS kataloğu değişmez.

## 3. CT şartları (bu WP'ye özgü; paket metni sonraki revizyonda hizalanır)

- **CT-1 — platform kolunda atlama defteri de sorar (r18 inceleme F-1, DÜŞÜK; CT kararı, karar kaydı satır 159-161):** §8.18 madde 5'teki
  sistem kodu çakışma atlaması **platform kolunda** "dış yoklama dolu **veya** oturumlu platform defteri `CodeExistsAsync` (tür başına,
  emekliler dahil) dolu" koşuluyla çalışır. Gerekçe: F-1 (c) ile deftere alınan desene uyan eski plan kodu (ör. `PLN-000007`) plan
  koleksiyonunda artık durmaz; yalnız dış yoklamaya bakan atlama onu görmez, sayaç o numaraya gelince ekleme E11000 → 409
  `RECORD_CODE_TAKEN` alır, işlem iptali sayaç artışını da geri aldığı için sonraki her kodsuz oluşturma aynı numarada kalıcı olarak takılır.
  Kiracı kolunun (TTY) davranışı **değişmez** (DL-3 AC'leri; TTY eski kodları deftere alınmaz).
  - **AC-88 adımı** (`SubscriptionPlanRecordCodeHttpTests.AC88_*`): test düzeni — platform sayacı boş; plan koleksiyonunda sahiplik satırı
    olmayan eski plan, kodu `PLN-000002`. (1) Bu planın kodu `code = "kurumsal ek"` + `codeReason` ile değiştirilir → 204, platform defterinde
    `PLN-000002` / `Legacy` / `Retired` satırı; (2) kodsuz oluşturma → 201 `PLN-000001`; (3) kodsuz oluşturma → 201 **`PLN-000003`**
    (`PLN-000002` defterde dolu olduğu için atlanır; aynı işlemde sayaç bir daha artar), 409 değil; defterde `PLN-000002` için tek satır kalır.
  - **Sabotaj S-139-WP:** atlama koşulundan defter sorgusu kaldırılır → (3) 409 `RECORD_CODE_TAKEN` alır → kırmızı; geri al → yeşil.
- **CT-2 — kabulde canlı sayım:** §18.1 DL-4 kabul kapısı gereği kabul kaydı canlı veride kiracı başına `^[A-Z]{2,5}-TTY-[0-9]{6,}$` desenine
  uyan görev türü kodu ve `^PLN-[0-9]{6,}$` desenine uyan plan kodu sayısını yazar (r18 inceleme F-1 kapanışında ayrıca istenir); ek olarak
  `CreatedAt` alanı olmayan plan belgesi sayısı (§4 not 1). Geliştiricinin canlı veriye erişimi yoksa raporda "ölçülmedi — CT ölçer" yazar;
  sayımı uydurmaz. `^PLN-[0-9]{6,}$` sayısı 0 değilse CT kabulden önce CT-1 adımının canlı karşılığını değerlendirir.
- **CT-3 — sürüm notu:** §8.18 madde 8'in metni kabul kaydına girer; kiracı / Platform yöneticilerine yazılı bildirim CT'nindir (kabul kapısı).

## 4. Uygulama notları (r18 inceleme; bulgu değil, uygulama ve test için bağlayıcı)

1. **`CreatedAt` alanı olmayan eski plan belgesi.** `BaseEntity.CreatedAt` `DateTimeOffset`, `init`, varsayılanı `UtcNow`; alanı olmayan eski
   bir plan belgesi okunduğunda değer **okuma anı** olur. Bu durumda `Legacy` satırının `ClaimedAtUtc`'si (= `RecordCreatedAtUtc`) `Manual`
   satırıyla aynı milisaniyeye düşebilir ve geçmiş sırası (`ClaimedAtUtc`, sonra `Id`) rastgele Guid'e kalır; adım 6'nın
   "`RecordCreatedAtUtc` < şimdi" denetimi de sınırda kalabilir. Paket davranışını değiştirme; test belgeleri `CreatedAt` taşır. Canlı veride
   `CreatedAt`'siz plan sayısı kabul kaydında ölçülür (CT-2); 0 değilse CT'ye yaz.
2. **`ClaimedAtUtc` milisaniye karşılaştırması.** `CreatedAt` `DateTimeOffset` olarak tick hassasiyetinde, `ClaimedAtUtc` BSON tarih olarak
   milisaniye hassasiyetinde saklanır. AC-88'in "`ClaimedAtUtc` = planın `CreatedAt`'i (UTC)" adımı her iki değeri **milisaniyeye kırparak**
   karşılaştırır (ör. `new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc)`); tick eşitliği aranmaz.
3. **Kod kuralı taşıması:** `SubscriptionPlanRequestValidator.cs:19-23` kuralı `UpdateSubscriptionPlanCommandValidator.cs`'e metin ve
   mesajlarıyla aynen geçer (S-138); güncelleme cevabı bugünkü `ValidationProblemDetails`.

## 5. Kurallar, test ve kanıt

- **Kiracı:** kiracı kapsamlı her sorgu `RecordCodeTenantPredicate`; başka kiracı 404 tek gövde. Platform (kiracısız) satırları yalnız ayrı
  `GlobalEntity` koleksiyonlarında ve platform depolarıyla (Q-18 A); kiracı okumaları onları görmez (AC-25 / AC-86). Soft delete dışı silme yok.
- **Denetim:** işlem içi (DL-1 / DL-3 deseni); platform kolunda kayıt kiracısı `AuditTenantIds.PlatformSystemTenantId`, `TargetTenantId = null`
  (Q-19 A); retler kısa ayrı işlemde, Outcome kuralı §21.2. Yazıcı, kaynak servis kümesi, `TransactionOwnedAuditCommands` değişmez.
- **Korunan yollar:** `.antigravity/**`, `gateway/Diten.ApiGateway/ocelot.json` (yalnız integration-agent), `_Layout.cshtml`, Archive,
  Building.Blocks — dokunma. Paket §5 "Değişmez" listesi AC-91'de `git diff` ile ölçülür.
- **Ortam:** `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`; 27017 **yok** (kullanılmaz). Main checkout'ta
  `dotnet watch` kilidi olabilir — derleme ve sabotaj koşuları ayrık worktree'de.
- **Tam koşu (dilim başı ve sonu):** `services/Diten.Platform/tests/Diten.Platform.Application.Tests` **filtresiz**; dokunulan takımlar
  filtresiz: `frontend/Diten.Web.Tests`, `tests/architecture/TenantArchitecture.ArchitectureTests`, `tests/tenancy/TenantArchitecture.TenancyTests`.
  `gateway/Diten.ApiGateway.Tests` yalnız `gateway/` altında dosya değişirse koşulur — DL-4 oraya dokunmaz (AC-91); değişirse dur.
- **Taban:** bilinen **19** Platform Mongo ortam kırmızısı; adları `ctv-0040dl2` tabanıyla birebir aynı olmalı
  (`docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md:160`). Yeni kırmızı 0, kaybolan 0; farklıysa adıyla yaz (CT aynı gün
  main ile karşılaştırır).
- **Atlanan test 0.** `Skip` işaretli test kabulü durdurur (S-132).
- **Web node (vitest):** yalnız repodaki `C:\Users\Ali\erp-vnext\frontend\Diten.Web\node_modules` kullanılır; ayrık worktree'de
  `frontend/Diten.Web/node_modules` bu dizine **junction** ile bağlanır. `npm install` / yeni paket yok. Web node takımı tam koşulur (AC-91).
- **Sabotaj:** her satır tek tek — uygula → hedef vaka kırmızı → geri al → yeşil: S-117…S-132, S-133 (yeniden), S-134…S-138, S-139-WP.
  Sabotaj tarifi kodla uyuşmazsa eşdeğeriyle yapılır ve raporda gerekçesiyle yazılır (DL-3 kabul emsali).
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.
- **Git:** yalnız açık dosya yoluyla stage (`git add -A` / `git add .` yok); hook atlanmaz (`--no-verify` yok); `main`'e commit yok;
  **push / PR yok**. Dal `feature/sce/mod-0173-inventory-ledger-valuation`.

## 6. Rapor

Commit sha'ları; AC-25 ve AC-76…AC-91 her biri için test adı ve sonuç; CT-1 adımının test adı ve sonucu; sabotaj tablosu (S-117…S-139-WP,
her satırda kırmızıya dönen test adı; eşdeğer tarifler gerekçeli); tam koşu sayıları (dilim başı / sonu: geçen / kalan / kırmızı / atlanan)
ve 19 kırmızının adlarının `ctv-0040dl2` ile karşılaştırması; `Diten.Web.Tests`, Web node ve iki TenantArchitecture takımının sayıları;
AC-91 `git diff --name-only` çıktısı ile §1.2 listesinin karşılaştırması (XMC dosyaları yalnız listedekiler + ek onaylı doğrulayıcı;
"Değişmez" listesi boş fark); yeni `RecordCodeAuditIntent` belirteci açılmadığının kanıtı (A-21); 7 dil / 2 dil kaynak dosyası anahtar
listesi; canlı sayımlar (CT-2) ya da "ölçülmedi — CT ölçer"; sürüm notu metni (§8.18 madde 8); açık noktalar.

## Değişiklik geçmişi

- 2026-11-14 (`30c313eb4`): ilk iskelet — r15'e göre, **BEKLEMEDE** (r15 DL-4 kod izni vermiyordu); D-1…D-11 girdileri ve XMC listesi.
- 2026-11-14 (`7e58c40e6`): "Ek — r18 onay yolunda" — r18 inceleme UYGUN, XMC ek onayı, inceleme F-1 CT ek şartı.
- 2026-11-14: **r18'e göre yeniden üretildi** (paket `1dbd7180`, onay approvals.tsv satır 100, `87d5617ca`) — BEKLEMEDE kutusu kaldırıldı,
  durum BAŞLATILABİLİR; etiket `ct/line-ff-2026-11-14g`; kapsam paket §23 DL-4, §5 "DL-4", §8.18, §16 (AC-25, AC-76…AC-91), §17 (S-117…S-138)
  ve §18.1 DL-4 kapılarından birebir; r15 numaralı D-1…D-11 tablosu r18'in AC'leriyle karşılandığı için çıkarıldı; "Ek — r18 onay yolunda"
  bölümündeki CT ek şartı §3 CT-1 / CT-2'ye taşındı (S-139-WP); r18 inceleme uygulama notları §4'e yazıldı.
