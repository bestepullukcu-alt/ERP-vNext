# WP-0230-DL2A · PVG-WP-0230-DL2A-P v1.1 · SOP §22 rapor (geliştirici)

**Agent Verdict:** PASS (geliştirici beyanı; K13 — kapanış değildir, kabul CT'nindir). Tek açık: DOĞRULA 5'teki mimari `known_failures` çıkışı 1 — temiz HEAD'de birebir aynı, bu WP'den değil (Blockers).

**Branch / HEAD:** `feature/pvg/wp-0230-dl2` @ `95563dae14c441e4f9ed5e86874c46fc0f649e06` (= Expected HEAD; başlangıçta `git status --porcelain` boş). Commit/push/stash/reset yapılmadı.

**Worktree status:** `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-wp0230-dl2` — 11 dosya `M`, hepsi İzinli yollarda; `git diff --check` rc=0.

## Changed files (numstat)
| + | − | Dosya |
|---|---|---|
| 22 | 2 | Application/RegPvBase/IPvgIntakeDraftStore.cs |
| 1 | 0 | Application/RegPvBase/PvgApplicationReasonCodes.cs |
| 29 | 8 | Application/RegPvBase/PvgIntakeDraftApplicationService.cs |
| 2 | 1 | Application/RegPvBase/PvgIntakeDraftMutationResult.cs |
| 2 | 1 | Application/RegPvBase/PvgIntakeDraftSummary.cs |
| 2 | 1 | Application/RegPvBase/{Update,Triage,Route}IntakeDraftCommand.cs (her biri) |
| 23 | 8 | Infrastructure/RegPvBase/InMemoryPvgIntakeDraftRepository.cs |
| 235 | 9 | tests/…RegPvBase.Tests/PvgIntakeDraftApplicationServiceTests.cs |
| 77 | 1 | tests/…RegPvBase.Tests/PvgIntakeDraftPersistenceBoundaryTests.cs |

## Golden/Contract flow
- Depo sözleşmesi: `ReplaceAsync(scope, id, long? expectedVersion, intake)` → `PvgIntakeDraftReplaceResult` (IPvgIntakeDraftStore.cs:12-17); sonuç tipi + `PvgIntakeDraftReplaceOutcome { Replaced, NotFound, VersionConflict }` aynı dosyada (:42-58); `PvgPersistedIntakeDraft.Version` (:40).
- Bellek içi depo: `AddAsync` ilk sürümü yazar (InMemoryPvgIntakeDraftRepository.cs:9, :22); `ReplaceAsync`: yok → NotFound (:42), null ya da eşleşmeyen sürüm → VersionConflict, yazmadan (:47), eşleşirse `current.Version + 1` (:52) ile `ConcurrentDictionary.TryUpdate(key, replacement, current)` (:54) — karşılaştır-ve-yaz tek atomik adım; TryUpdate kaybederse VersionConflict.
- Uygulama: komutlara sona `long? ExpectedVersion = null` (Update/Triage/RouteIntakeDraftCommand.cs:9); `IntakeDraftVersionConflict = "PVG_INTAKE_DRAFT_VERSION_CONFLICT"` (PvgApplicationReasonCodes.cs:6). Üç çağrı yeri üç dal: update :157/:163/:168, triage :246/:252/:257, route :335/:341/:346 (PvgIntakeDraftApplicationService.cs) — NotFound → IntakeDraftNotFound, Replaced değil → IntakeDraftVersionConflict (fail-closed), başarılı → sonuçta yeni sürüm (:177, :266, :355). Kontrol sırası değişmedi.
- Okuma: `PvgIntakeDraftSummary(..., long Version)`; GetDraftByIdAsync :403, ListDraftsAsync :444 (yalnız özet kurulumu; sayfalama/sayım/ListAsync imzası/PvgIntakeDraftQueryResult dokunulmadı).
- Api: kaynak değişmedi; `dotnet build …Api.csproj` 0 hata 0 uyarı.

## Sub-flows / Failure paths
- Eski sürüm → `PVG_INTAKE_DRAFT_VERSION_CONFLICT`, AuditIntent/IntakeDraftId/Version null, depo değişmez.
- Sürümsüz (null) → aynı kod (CT kuralı), depo değişmez.
- Başka kiracı → `ReplaceAsync` NotFound (çakışma değil; varlık sızmaz) — Repository_mutations_are_tenant_scoped; servis katmanında kiracı dışı kayıt zaten okuma adımında IntakeDraftNotFound.

## Tests
- Taban (değişiklik öncesi): RegPvBase 88/88, Api 36/36, CaseProcessing 23/23, MeddraCoding 40/40, SignalManagement 43/43.
- Son: RegPvBase **97/97** (88 + 9 yeni satır), Api **36/36** (aynı sayı), CaseProcessing 23/23, MeddraCoding 40/40, SignalManagement 43/43. `known_failures` pvg-regpv-base exit 0, pvg-api exit 0 (ÖZET hepsi 0).
- Paket BG-12 (adlar birebir, hepsi Passed): `ReplaceAsync_with_stale_version_returns_conflict_not_not_found` (PersistenceBoundaryTests.cs:63), `Update_with_stale_version_returns_version_conflict_and_leaves_store_unchanged` (ApplicationServiceTests.cs:1156), `Triage_and_route_with_stale_version_return_version_conflict` (:1188, Recording* allow-stub yalnız testte).
- CT ekleri (Passed): `Get_by_id_returns_current_version_after_update` (:1234), `List_summary_returns_current_version` (:1259), `Mutation_without_expected_version_is_version_conflict_and_leaves_store_unchanged` (:1288, Theory: Update/Triage/Route), `Concurrent_replace_with_same_expected_version_allows_exactly_one_success` (PersistenceBoundaryTests.cs:94; 100 tur × 8 LongRunning görev, `Barrier(8)` başlama bariyeri; turda tam 1 Replaced, 7 VersionConflict, depo sürümü = kazananınki).
- Yalnız sürüm göndermek için güncellenen mevcut testler (iddia değişmedi): `Successful_mutations_emit_safe_audit_intent_metadata_only` (:214, `MutateAsync` yardımcısı okunan sürümü gönderir), `Successful_operations_preserve_correlation_for_guard_evaluation_and_emit_safe_observability_metadata` (:377; izin sırası iddiası korunsun diye sürüm servis değil depo üzerinden okunur, triage `detail.Items[0].Version`, route `triaged.Version`), `Create_and_update_without_evidence_references_do_not_invoke_evidence_link` (:865), `Triage_allowed_by_field_security_calls_workflow_evidence_and_mutates_safely` (:990), `Repository_mutations_are_tenant_scoped` (PersistenceBoundaryTests.cs:42; yeni imza, tenant A'nın gerçek sürümüyle; `Assert.False` → `Assert.Equal(NotFound, …Outcome)`). Başka kırılan test yok.
- Sabotajlar (her biri geri alındı, `cmp` ile temiz kopyaya eşit doğrulandı):
  - A1 depo karşılaştırması kapalı (`if (false)`): 7 kırmızı — ReplaceAsync_with_stale…, Update_with_stale…, Triage_and_route_with_stale…, Mutation_without_expected_version ×3, Concurrent_replace….
  - A2 çakışma dalı → IntakeDraftNotFound: update → Update_with_stale… + Mutation_without…(Update); triage → Triage_and_route_with_stale… + Mutation_without…(Triage); route → Triage_and_route_with_stale… + Mutation_without…(Route).
  - B GetById sabit `1L` → Get_by_id_returns_current_version_after_update (+ Successful_operations_preserve_correlation…, triage o sürümü kullandığı için); List sabit `1L` → List_summary_returns_current_version.
  - C null'da karşılaştırma atlanır → Mutation_without_expected_version… ×3 kırmızı.
  - D kilitsiz oku-karşılaştır-`Thread.Yield()`-yaz: 3/3 koşu kırmızı (`Expected: 1, Actual: 8`); geri alındıktan sonra 3/3 yeşil.
- trx: services/Diten.PvgService/tests/*/TestResults/{pvg-regpv-base,pvg-api}.trx; sabotaj trx'leri scratchpad `sab/` altında.

## Persistence evidence
L1 bellek içi (InMemoryPvgIntakeDraftRepository). BG-13: `git grep -n -i mongo -- services/Diten.PvgService/src` → 0 satır; `…/tests` → 8 satır, hepsi mevcut "Mongo yok" iddiaları (yalnız ApplicationServiceTests satırı 1162→1339 kaydı); PvgService *.csproj'larında MongoDB yok; `DenyAllAdapters_are_stateless_and_do_not_reference_persistence_or_network_libraries` Passed. Migration/koleksiyon/indeks yok; PvgIntakeDraftIndexCatalog.cs dokunulmadı.

## Security/RBAC/Tenant evidence
İzin sabitleri (:7-11) ve kontrol sırası değişmedi (izin sırası iddiaları yeşil: Triage_and_route_with_stale… callLog'u permission→field→workflow→evidence sonra çakışma). Kiracı: depo anahtarı (TenantId, IntakeDraftId); başka kiracı = NotFound.

## Audit/Evidence · Observability
Denetim çağrısı eklenmedi (DL-5); PvgAuditIntent değişmedi; çakışmada AuditIntent null. Yeni çıktı yalnız güvenli neden kodu + sayısal sürüm; ham PHI/PII yok (AssertSafe tüm yeni sonuçlarda).

## Migration/Rollback
Yok. Geri alma = bu 11 dosyanın çalışma ağacı değişikliğini atmak.

## Decisions
- Sürüm türü `long` (C# `long`, JSON sayı); alan adı `Version` (depoda `PvgPersistedIntakeDraft.Version`, özette `PvgIntakeDraftSummary.Version`, sonuçta `PvgIntakeDraftMutationResult.Version`; komutta `ExpectedVersion`); başlangıç değeri `1` (`InitialVersion`), her başarılı replace'te +1.

## Blockers
- DOĞRULA 5 mimari: 146 testte 3 kırmızı, `known_failures` exit 1 (KISMEN-DÜZELDİ 1, YENİ-AYRINTI 2: Diten.Platform `RegisterSignableActionsCommand`/`VerifySignatureCommand` denetimsiz; DocsPathGuard msg `cdad4e1fb1af48fc` — scripts/ ve Inventory/SupplyChain/Manufacturing yolları). **Temiz HEAD `git archive 95563dae1` dışa aktarımında birebir aynı** (aynı 3 test, aynı ayrıntılar). PvgService ile ilgisi yok; kayıt güncellemesi bu WP'nin kapsamı dışı.

## Known gaps
1. Create sonucu `Version` null (AddAsync imzası değişmedi; istemci ilk sürümü okumayla alır). DL-4 create yanıtında sürüm isterse AddAsync dönüşü genişletilmeli.
2. Api kaynağı değişmedi ama `PvgCaseIntakeApiResponse.Items` artık `PvgIntakeDraftSummary.Version`'ı JSON'da taşır (GET/list) — DL-4'ün resmileştireceği örtük yüzey.
3. Api komutlara sürüm geçirmediği için HTTP üzerinden PUT/triage/route depoya ulaştığında 409 `PVG_INTAKE_DRAFT_VERSION_CONFLICT` alır. Bugün deny-all adaptörler (alan güvenliği/iş akışı) daha önce engellediğinden canlı davranış değişmez; izin veren adaptör DL-4'ten önce devreye girmemeli.
4. Kanıt düzeyi E2; canlı host koşulmadı.

## Out-of-scope changes
Yok. (Repo dışı: scratchpad'de HEAD dışa aktarımı `head-export-95563dae1/` ve sabotaj trx'leri.)
