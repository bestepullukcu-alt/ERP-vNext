# WF-S17-DL2 — işçi raporu 01 (2026-11-02)

- Kaynak: işçi oturumu "İş akışı › Aynı anahtar yanıtı (işçi)" notu, olduğu gibi kopyalandı. İddiadır; doğrulama ayrı kayıttadır.
- Teslim: `1e5615697984b98d85be4873991f9322f2442217` · dal `feature/pss/wf-s17-dl2-r2` · taban `8698dbddc` · prompt v1.8 (sha256 `08ebd287…`). Push yok.

---

# WF-S17-DL2 — işçi raporu (prompt v1.8, 2026-11-02)

**Agent Verdict:** tamam, açık kayıtlarla (en altta). Model: istenen claude-sonnet-5-5 · high; kullanılan claude-sonnet-5-5 (bekçi ve başlatma testlerini yazan fork'lar aynı model; tüketici tablosu taslağı alt ajanı sonnet).
Prompt v1.6 (`23b82d5f…`) ve v1.8 (`08ebd287…`) sha256 ölçüldü, eşleşti. Geçici dal wip `1a43e453e` `git cherry-pick -n` ile geri alındı, geçici dal silinmedi.

## Branch / HEAD / Worktree
Dal `feature/pss/wf-s17-dl2-r2`, Expected HEAD `8698dbddc552b5a6e2aa7adafb4f88746f6983fd`; tek commit (sha raporun üstündeki mesajda). Push, PR, merge yok.

## Changed files (13)
Üretim (5): StartWorkflowInstanceHandler.cs, WorkflowTaskTransitionSupport.cs, WorkflowTerminalTransitionWriter.cs, WorkflowModels.cs, WorkflowTransitionLog.cs (yalnız `TargetPrincipalId` ekler).
Test (8): WorkflowCandidateDecisionMongoTests.cs (yardımcılar), WorkflowCandidateDecisionMongoTests.S17.cs (yeni), WorkflowStartRecoveryMongoTests.cs (yalnız `partial`), WorkflowStartRecoveryMongoTests.S17.cs (yeni), WorkflowSameKeyHttpMongoTests.cs (yeni), WorkflowInstanceStartTests.cs (istisna 1), TimesheetApprovalHttpMongoTests.cs (istisna 2), tests/architecture/.../WorkflowRetentionSourceGuardTests.cs (yeni).
Ince eylem işleyicilerine, ocelot.json'a, Platform.Common'a, tüketici koduna dokunulmadı. D2.cs ve D2Fix1.cs değişmedi.

## Contract flow / Failure paths (özet)
- Başlatma: her anahtar tekrarında `SameStartFactsAsync` (şablon, nesne, başlatan, parmak izi); farklı → 409 WORKFLOW_INSTANCE_START_CONFLICT; aynı → bugünkü 200; yanıtta `idempotentReplay` (ilk yazma false, geri kalan true).
- Geçiş: Approve/Reject/RequestInfo/Cancel sıralı tekrarında aktör (403 WORKFLOW_ACTOR_DENIED) sonra gövde (409 WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT); Delegate değişmedi. O-S17-3 (i): `TransitionAsync`'te stale-sürüm / karar-verilemez reddinden önce anahtar bir kez daha okunur. RequestInfo yinelenen anahtar reddi (`ux_workflow_transition_logs_task_action_idempotency_active`) aktöre ve gövdeye bağlı tekrara eşlenir. `WorkflowTransitionLog.TargetPrincipalId` (RequestInfo: kırpılmış hedef ya da `""`; null = alan öncesi) üç yolda ayrıca karşılaştırılır, null eski satırda yalnız Comment üzerinden.
- İzin, aktör bağlama, SoD, rota, indeks, şema değişmedi. Kiracı: bütün okumalar mevcut depo süzgeçleriyle (TenantId); yeni sorgu yok.

## Tabanlar ve parmak izleri
Platform taban (kod değişmeden, v1.3 koşusu): 3874 toplam / 3874 geçti / 0 kaldı / 0 NotExecuted; `fingerprint --suite platform-app` çıktısı BOŞ. TRX `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TestResults/wf-dl2-platform-taban.trx`.
Platform son (v1.8, sabotajsız): 3967 toplam (= 3874 + 93 yeni) / 3967 geçti / 0 kaldı / 0 NotExecuted; parmak izi çıktısı BOŞ (1. ve 3. sütun tabanla aynı); `check` ÖZET BİLİNEN=0 KISMEN-DÜZELDİ=0 YENİ=0 YENİ-AYRINTI=0. TRX `…/TestResults/wf-dl2-platform.trx`. İlk son koşu (istisnalardan önce, v1.6): 3964 geçti / 3 kırmızı (iki istisna testi + Meetings yük zamanlaması), `scratchpad/wf-dl2-platform-run1-red3.trx`.
Mimari taban (v1.6'da yeniden ölçüldü): 176 toplam / 170 geçti / 6 kaldı, `tests/architecture/TenantArchitecture.ArchitectureTests/TestResults/wf-dl2-arch-taban.trx`. Mimari son: 216 (= 176 + 40 yeni) / 210 geçti / 6 kaldı / NotExecuted 0, `…/wf-dl2-arch.trx`. Meetings testi son koşuda geçti (yeniden koşu gerekmedi; önceki koşuda yükten bir kez kırıldı, 4,69 sn, tek başına geçti).

Arch parmak izi (taban ve son aynı; üç sütun):
```
items:5:d9773291e8efe9fa	Failed	TenantArchitecture.ArchitectureTests.AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous
items:55:6ee8c6dc31c3a246	Failed	TenantArchitecture.ArchitectureTests.AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt
msg:48791d1df5b9bfe3	Failed	TenantArchitecture.ArchitectureTests.CiServiceTestsGuardTests.Every_action_in_every_workflow_is_at_its_one_recorded_commit
msg:48791d1df5b9bfe3	Failed	TenantArchitecture.ArchitectureTests.CiServiceTestsGuardTests.No_workflow_uses_a_privileged_trigger_or_reads_a_secret
msg:6cde8f07cde75f9a	Failed	TenantArchitecture.ArchitectureTests.CiServiceTestsGuardTests.Nothing_can_turn_a_red_test_or_build_green
msg:13385a5e6ed357ef	Failed	TenantArchitecture.ArchitectureTests.DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders
```
Platform parmak izi: taban ve son boş (kırmızı yok). Arch `check` (taban; son ile YENİ kümesi aynı; çıkış 1, tek başına durma nedeni değil):
```
KISMEN-DÜZELDİ	TenantArchitecture.ArchitectureTests.AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous (2 kayıtlı öğe giderildi, yeni öğe yok)
YENİ	TenantArchitecture.ArchitectureTests.CiServiceTestsGuardTests.Every_action_in_every_workflow_is_at_its_one_recorded_commit
YENİ	TenantArchitecture.ArchitectureTests.CiServiceTestsGuardTests.No_workflow_uses_a_privileged_trigger_or_reads_a_secret
YENİ	TenantArchitecture.ArchitectureTests.CiServiceTestsGuardTests.Nothing_can_turn_a_red_test_or_build_green
YENİ-AYRINTI	TenantArchitecture.ArchitectureTests.AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt (2 yeni öğe: Diten.Platform: RegisterSignableActionsCommand — YENİ KOMUT DENETİMSİZ GELDİ (services/Dit; Diten.Platform: VerifySignatureCommand — YENİ KOMUT DENETİMSİZ GELDİ (services/Diten.Platf)
YENİ-AYRINTI	TenantArchitecture.ArchitectureTests.DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders (kayıt msg:439e2be27b6b5cdc, şimdi msg:13385a5e6ed357ef — incele)
ÖZET BİLİNEN=0 KISMEN-DÜZELDİ=1 YENİ=3 YENİ-AYRINTI=2 DÜZELDİ=0 KOŞMADI=0 BİLİNMİYOR=0
DUR: kayıtta olmayan kırmızı ya da bilinen kırmızıda yeni ayrıntı var — regresyon olarak bildirin.
```

## İstisna iki test (v1.8)
1. `WorkflowInstanceStartTests.Duplicate_idempotency_key_returns_existing_start_without_duplicates`. Eski hazırlık: `Start()` içinde `DueAt: UtcNow.AddDays(2)`, iki çağrıda farklı; kırmızı: `Assert.True() Failure … WorkflowInstanceStartTests.cs:204`. Yeni hazırlık: `Start(..., dueAt:)` isteğe bağlı parametre, testte tek `var due = UtcNow.AddDays(2)` iki çağrıya verilir; Assert ve ad aynı. Yeşil: son koşuda sınıf 15/15.
2. `TimesheetApprovalHttpMongoTests.Starting_the_same_submission_again_returns_the_same_MOD_0023_instance`. Eski: ikinci `StartAsync` kimliksiz kapsamda; kırmızı: `Assert.Equal() Failure: Expected f633a678-… Actual: null (TimesheetApprovalHttpMongoTests.cs:69)`. **409 sebebi ölçüldü** (geçici sınama testi, silindi): kimliksiz kapsamda `instance= reason=WORKFLOW_INSTANCE_START_CONFLICT`, kayıtlı `StartedByUserId` = `Person`; ilk başlatmayı yapan kullanıcı bağlamında aynı örnek (`same=True`). Sebep başlatan farkı. Yeni: ikinci çağrıdan önce kapsamın `IHttpContextAccessor.HttpContext`'ine `Person` sub talebiyle `DefaultHttpContext` verilir; Assert, ad ve dosyadaki başka test aynı. Yeşil: sınıf 42/42.
`git diff --stat` dokunulan mevcut test dosyaları: WorkflowInstanceStartTests.cs (+8 −4), TimesheetApprovalHttpMongoTests.cs (+11 −0). Üretim kodu ve TimeEntry üretim kodu değişmedi.

## AC başına durum (hepsi gerçek Mongo / HTTP; sahte depo kanıt değil)
- AC-W5: tamam. Zorlanmış çakışma 3 turda varışla bırakıldı ((a)=(b)=6, tek Created, 5 false, zaman aşımı yok); bağımsız 8 paralel HTTP × 5 tur tek örnek. S1–S3 kırmızı.
- AC-W6 / W7: tamam (a, b, c; son ve ara adım; RequestInfo a, b, c). S4–S6b, S19a–c.
- AC-W8: tamam (b iki adım, yeniden oynatma, c ara adım approve-vs-reject, sıfır satır 503). S7, S7b.
- AC-W9, W10, W11, W12: tamam. S8a/b, S9, S9b, S10…S12b.
- AC-W14: tamam (diğer aktör a/b/c, gövde a/b, SignatureId, hedef çarpışması 3 yol × 2 yön, eski satır 6 satır). S13a–d, S14–S16b.
- AC-W17: kod tarafı tamam (tüketici koşuları yeşil); tablo aşağıda ve `WF-S17-DL2-consumer-table-draft.md`; sahip kaydı **bekliyor**.
- AC-W18: tamam (üç test, S18); sahip kaydı **bekliyor**.
- O-S17-3 = (i): uygulandı (S4, S5 kırmızı).
- AC-W15 dördüncü depo: S17-8 ile açık; yeni bekçi dosyası 40 test, G1–G8 kırmızı.

## Ölçümler (test içinden yazılan, ham: scratchpad/measure.txt)
```
AC_W6_c_barrier(False)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W6_c_barrier(True)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W7_c_reject_barrier	409 WORKFLOW_TRANSITION_CONFLICT
duplicate-key exception	MongoDB.Driver.MongoWriteException / DuplicateKey (wrapped in PlatformTransactionUnavailableException)
AC_W7_requestinfo_c_barrier	409 WORKFLOW_TRANSITION_CONFLICT; executor reported unavailable: no
AC_W14_c_overlap	409 WORKFLOW_TRANSITION_CONFLICT
AC_W6_c_barrier(False)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W6_c_barrier(True)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W7_c_reject_barrier	409 WORKFLOW_TRANSITION_CONFLICT
duplicate-key exception	MongoDB.Driver.MongoWriteException / DuplicateKey (wrapped in PlatformTransactionUnavailableException)
AC_W7_requestinfo_c_barrier	409 WORKFLOW_TRANSITION_CONFLICT; executor reported unavailable: no
AC_W14_c_overlap	409 WORKFLOW_TRANSITION_CONFLICT
timesheet-probe	stored StartedByUserId=5da23096-9dbe-4d55-b1cc-3cff66f38e6b Person=5da23096-9dbe-4d55-b1cc-3cff66f38e6b | no-user: instance= reason=WORKFLOW_INSTANCE_START_CONFLICT | same-user: instance=79fd268c-3d32-490f-a4ba-05fd447bdf45 reason= same=True
AC_W6_c_barrier(False)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W6_c_barrier(True)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W7_c_reject_barrier	409 WORKFLOW_TRANSITION_CONFLICT
duplicate-key exception	MongoDB.Driver.MongoWriteException / DuplicateKey (wrapped in PlatformTransactionUnavailableException)
AC_W7_requestinfo_c_barrier	409 WORKFLOW_TRANSITION_CONFLICT; executor reported unavailable: no
AC_W14_c_overlap	409 WORKFLOW_TRANSITION_CONFLICT
duplicate-key exception	MongoDB.Driver.MongoWriteException / DuplicateKey (wrapped in PlatformTransactionUnavailableException)
duplicate-key exception	MongoDB.Driver.MongoWriteException / DuplicateKey (wrapped in PlatformTransactionUnavailableException)
AC_W6_c_barrier(False)	200 Replayed
AC_W6_c_barrier(False)	200 Replayed
AC_W6_c_barrier(True)	409 WORKFLOW_TRANSITION_CONFLICT
AC_W7_c_reject_barrier	409 WORKFLOW_TRANSITION_CONFLICT
AC_W7_requestinfo_c_barrier	409 WORKFLOW_TRANSITION_CONFLICT; executor reported unavailable: no
AC_W14_c_overlap	409 WORKFLOW_TRANSITION_CONFLICT
```
Sürücünün yinelenen anahtar istisna tipi: `MongoDB.Driver.MongoWriteException`, `WriteError.Category = DuplicateKey`, mesajda `ux_workflow_transition_logs_task_action_idempotency_active`; `PlatformTransactionUnavailableException`'ın iç istisnası olarak gelir (kodda böyle yakalanıyor). AC-W7 RequestInfo (c) barrier: kaybeden 409 `WORKFLOW_TRANSITION_CONFLICT`; yürütücü "unavailable" bildirmedi, yani sürüm korumalı güncelleme yolundan (TerminalConflict) geçti, yinelenen anahtar yolundan değil. Approve ve Reject barrier kaybedeni de her seferinde 409 (200 Replayed değil).

## Sabotaj sonuçları (geçici; sha önce/sonra aynı; geri alma sonrası aynı filtre yeşil)
Çalıştırıcı `scratchpad/sab/run_sab.py` (süzgeç = beklenen test adları; derleme hatası kırmızı sayılmadı). Son sütun kırmızı olan test adları.
| id | sonuç | sha | geri alma sonrası | kırmızı testler |
|---|---|---|---|---|
| S1 | RED-OK | sha-same | green:1p/0f | AC_W5_forced_overlap_every_caller_missed_the_key_and_one_reservation_wins |
| S2 | RED-OK | sha-same | green:1p/0f | AC_W5_forced_overlap_every_caller_missed_the_key_and_one_reservation_wins |
| S3 | RED-OK | sha-same | green:1p/0f | AC_W5_forced_overlap_every_caller_missed_the_key_and_one_reservation_wins |
| S8a | RED-OK | sha-same | green:4p/0f | AC_W9_replay_after_each_terminal_state_answers_200_same_instance(terminal: Cancelled) | AC_W9_replay_after_each_terminal_state_answers_200_same_instance(terminal: Completed) | AC_W9_replay_after_each_terminal_state_answers_200_same_instance(terminal: Rejected) | AC_W9_replay_after_each_terminal_state_answers_200_same_instance(terminal: TimedOut) |
| S8b | RED-OK | sha-same | green:1p/0f | AC_W9_recovery_refuses_a_whole_start_and_writes_nothing |
| S10 | RED-OK | sha-same | green:8p/0f | AC_W11_same_key_another_starter_is_409 | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "candidates") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "due-at") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "object-id") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "object-ref") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "reason-code") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "template") | AC_W11_trusted_whole_start_replayed_through_the_g |
| S10-fingerprint | RED-OK | sha-same | green:7p/0f | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "candidates") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "due-at") | AC_W11_same_key_different_facts_is_409_and_nothing_changes(different: "reason-code") | AC_W11_trusted_whole_start_replayed_through_the_generic_handler |
| S10-starter | RED-OK | sha-same | green:1p/0f | AC_W11_same_key_another_starter_is_409 |
| S10b | RED-OK | sha-same | green:1p/0f | AC_W11_legacy_start_without_fingerprint_is_held_to_its_identity |
| S11-false | RED-OK | sha-same | green:3p/0f | AC_W11_idempotent_replay_flag_false_first_true_after(mode: "completing") | AC_W11_idempotent_replay_flag_false_first_true_after(mode: "whole") | AC_W11_idempotent_replay_on_the_wire |
| S11-true | RED-OK | sha-same | green:3p/0f | AC_W11_idempotent_replay_flag_false_first_true_after(mode: "whole") | AC_W11_idempotent_replay_on_the_wire |
| S11-complete | RED-OK | sha-same | green:2p/0f | AC_W11_idempotent_replay_flag_false_first_true_after(mode: "completing") |
| S12 | RED-OK | sha-same | green:1p/0f | AC_W12_never_whole_closed_start_replay_is_409_and_close_record_is_readable |
| S12b | RED-OK | sha-same | green:1p/0f | AC_W12_never_whole_closed_start_replay_is_409_and_close_record_is_readable |
| S4 | RED-OK | sha-same | green:13p/0f | AC_W14_b_different_body_late_is_409(action: "approve") | AC_W14_b_different_body_late_is_409(action: "reject") | AC_W14_b_other_actor_late_is_403(action: "approve", staleVersion: False) | AC_W14_b_other_actor_late_is_403(action: "approve", staleVersion: True) | AC_W14_b_other_actor_late_is_403(action: "reject", staleVersion: False) | AC_W14_b_other_actor_late_is_403(action: "reject", staleVersion: True) | AC_W6_b_late_twin_is_200(intermediate: False, staleVersion: False) | AC_W6_b_late_twin_is_200(intermediate: False, staleVersion: True) | AC_W6_b_late_twin_is_200(intermediate: True, staleVers |
| S5 | RED-OK | sha-same | green:3p/0f | AC_W6_a_replay_on_mongo(intermediate: False) | AC_W6_a_replay_on_mongo(intermediate: True) | AC_W7_a_replay_on_mongo |
| S5-only-lookup | RED-OK | sha-same | green:3p/0f |  |
| S5-only-reread | RED-OK | sha-same | green:3p/0f |  |
| S6 | RED-OK | sha-same | green:13p/0f | AC_W14_legacy_row_identical_retry(path: "duplicate-key", row: "no-target") | AC_W14_legacy_row_identical_retry(path: "duplicate-key", row: "target") | AC_W14_requestinfo_b_other_actor(staleVersion: False) | AC_W14_target_collision_duplicate_key(direction: "AB", identical: False) | AC_W14_target_collision_duplicate_key(direction: "BA", identical: False) | AC_W7_requestinfo_b_duplicate_key_is_replay |
| S6b | RED-OK | sha-same | green:1p/0f | AC_W7_requestinfo_b_stale_version_is_conflict_then_replay_200 |
| S7 | RED-OK | sha-same | green:4p/0f | AC_W8_b_late_loser_is_invalid_state_or_conflict(intermediate: False) | AC_W8_b_late_loser_is_invalid_state_or_conflict(intermediate: True) | AC_W8_b_loser_replay_gets_the_same_answer(intermediate: False) | AC_W8_b_loser_replay_gets_the_same_answer(intermediate: True) |
| S7b | RED-OK | sha-same | green:6p/0f | AC_W14_c_other_actor_overlap_is_409_or_503 | AC_W6_c_barrier_identical_overlap_writes_one_row(intermediate: True) | AC_W7_c_reject_barrier_identical_overlap | AC_W7_requestinfo_c_barrier_identical_overlap | AC_W8_c_approve_vs_reject_intermediate_step |
| S9 | RED-OK | sha-same | green:4p/0f | AC_W10_replay_after_the_task_moved_on(kind: "approve-final") | AC_W10_replay_after_the_task_moved_on(kind: "approve-intermediate") | AC_W10_replay_after_the_task_moved_on(kind: "reject") |
| S9b | RED-OK | sha-same | green:4p/0f | AC_W10_replay_after_the_task_moved_on(kind: "request-info") |
| S13a | RED-OK | sha-same | green:4p/0f | AC_W14_a_other_actor_is_403(action: "approve") | AC_W14_a_other_actor_is_403(action: "reject") | AC_W14_a_other_actor_is_403(action: "requestInfo") | AC_W18_cancel_replay_other_actor_is_403 |
| S13b | RED-OK | sha-same | green:9p/0f | AC_W14_a_different_body_is_409(action: "approve", field: "comment") | AC_W14_a_different_body_is_409(action: "approve", field: "evidence") | AC_W14_a_different_body_is_409(action: "approve", field: "reason") | AC_W14_a_different_body_is_409(action: "reject", field: "comment") | AC_W14_a_different_body_is_409(action: "reject", field: "evidence") | AC_W14_a_different_body_is_409(action: "reject", field: "reason") | AC_W14_a_different_body_is_409(action: "requestInfo", field: "comment") | AC_W14_a_different_body_is_409(action: "requestInfo", field: "evidence") | AC_W14_a_different_body_is_409(act |
| S13c | RED-OK | sha-same | green:9p/0f | AC_W14_a_different_body_is_409(action: "approve", field: "comment") | AC_W14_a_different_body_is_409(action: "approve", field: "evidence") | AC_W14_a_different_body_is_409(action: "approve", field: "reason") | AC_W14_a_different_body_is_409(action: "reject", field: "comment") | AC_W14_a_different_body_is_409(action: "reject", field: "evidence") | AC_W14_a_different_body_is_409(action: "reject", field: "reason") | AC_W14_a_different_body_is_409(action: "requestInfo", field: "comment") | AC_W14_a_different_body_is_409(action: "requestInfo", field: "evidence") | AC_W14_a_different_body_is_409(act |
| S14 | RED-OK | sha-same | green:10p/0f | AC_W14_target_collision_duplicate_key(direction: "AB", identical: False) | AC_W14_target_collision_duplicate_key(direction: "BA", identical: False) | AC_W14_target_collision_sequential(direction: "AB") | AC_W14_target_collision_sequential(direction: "BA") | AC_W14_target_collision_unavailable_reread(direction: "AB", versionMoves: False) | AC_W14_target_collision_unavailable_reread(direction: "AB", versionMoves: True) | AC_W14_target_collision_unavailable_reread(direction: "BA", versionMoves: False) | AC_W14_target_collision_unavailable_reread(direction: "BA", versionMoves: True) |
| S16 | RED-OK | sha-same | green:6p/0f | AC_W14_legacy_row_identical_retry(path: "duplicate-key", row: "no-target") | AC_W14_legacy_row_identical_retry(path: "duplicate-key", row: "target") | AC_W14_legacy_row_identical_retry(path: "sequential", row: "no-target") | AC_W14_legacy_row_identical_retry(path: "sequential", row: "target") | AC_W14_legacy_row_identical_retry(path: "unavailable-reread", row: "no-target") | AC_W14_legacy_row_identical_retry(path: "unavailable-reread", row: "target") |
| S15 | RED-OK | sha-same | green:11p/0f | AC_W14_target_collision_duplicate_key(direction: "BA", identical: False) | AC_W14_target_collision_duplicate_key(direction: "BA", identical: True) | AC_W14_target_collision_sequential(direction: "BA") | AC_W14_target_collision_unavailable_reread(direction: "BA", versionMoves: False) | AC_W14_target_collision_unavailable_reread(direction: "BA", versionMoves: True) | AC_W7_requestinfo_c_injected_identical_twin_is_replayed |
| S16b | RED-OK | sha-same | green:1p/0f | AC_W7_requestinfo_c_injected_identical_twin_is_replayed |
| S17 | RED-OK | sha-same | green:1p/0f | Delegate_replay_with_a_different_body_stays_idempotent |
| S18 | RED-OK | sha-same | green:2p/0f | AC_W18_cancel_replay_different_body_is_409 | AC_W18_cancel_replay_other_actor_is_403 |
| S19a | RED-OK | sha-same | green:3p/0f | AC_W6_c_zero_row_503_then_first_write(intermediate: False) |
| S19b | RED-OK | sha-same | green:3p/0f | AC_W6_c_zero_row_503_then_first_write(intermediate: True) |
| S19c | RED-OK | sha-same | green:3p/0f | AC_W7_requestinfo_c_zero_row_503_then_first_write |
| S13d | RED-OK | sha-same | green:1p/0f | AC_W14_signature_id_is_part_of_the_body |

Notlar: S5-only-lookup ve S5-only-reread kontrolleri bilerek kırmızısız (biri diğerini karşılar). S7b: AC_W6_c_barrier(intermediate: False) yeşil kaldı, diğer üç özdeş-çakışma testi kırmızı (kaybeden 409 dalındaydı); prompt'a göre durma nedeni değil. S8b ve S12b CloseAbandonedWorkflowStartHandler.cs'e yalnız geçici yazıldı, commit'e girmedi. S2/S3 ReserveHold'u (test sınıfı) geçici yazar, iki parça birlikte.
Bekçi sabotajları (korunan dosyaya yazmadan, bekçinin kaynak-metin girdisine geçici ekleme; sha aynı; geri alma sonrası yeşil): G1, G2 → `AC_W15_the_fourth_store_deletes_nothing_and_writes_no_deletion_marker`; G3, G4, G5, G6, G8 → `AC_W15_no_file_in_the_scan_set_deletes_a_row_or_writes_a_deletion_marker`; G7 → `AC_W15_exactly_the_four_expected_classes_take_a_retained_type_as_a_generic_base_argument` (ilk deneme tarifi derleme hatası verdi, kırmızı sayılmadı; tarif düzeltilip yeniden koşuldu). Kalıcı [Theory] satırları (G1–G8) ve pozitif kontroller yeşil.

## Yeniden kullanılan mevcut testler (yeniden yazılmadı)
AC-W8 (c): `WorkflowCandidateDecisionMongoTests.Two_candidates_approving_the_FINAL_step_at_once_write_one_decision_and_one_completion_event`, `…Two_candidates_approving_an_INTERMEDIATE_step_at_once_create_exactly_one_next_task`, Fix1.cs:356-376 (approve vs reject, son adım), Fix1.cs:141-172 (unavailable). Aynı anahtar farklı gövde 409: D2.cs:265-276, D2Fix1.cs:368-378 (değişmedi, yeşil). Özdeş ikiz Replayed: D2Fix1.cs:235-259, :380-387. AC-W10 için Mongo'da mevcut test bulunmadı.

## Bekçi dosyası başlığında kalan kaçış yolları
using takma adı; yansıma ya da genel yardımcıyla silme; üç ad için başka dosyada çıplak `GetCollection<BsonDocument>("workflow_instances")`; `Builders<T>` değişkende tutulup tip süzgecini atlatma; Features/Workflow dışındaki Application yazımı (Tasks, TimeEntry, WorkAggregation taranmaz).

## DOĞRULA 5–8
5: `dotnet build …Platform.API.csproj` 0 hata. 6: S17 sınıfları ve WorkflowSameKeyHttpMongoTests TRX'te Passed, atlanan 0: 118 yeni AC testi geçti (WorkflowCandidateDecisionMongoTests 72, WorkflowStartRecoveryMongoTests 17 AC, WorkflowSameKeyHttpMongoTests 4, + mevcut AC adlı testler). 7: aşağıda. 8: `git diff --check` temiz; `git diff --stat -- services/Diten.Platform.Common` boş; arch'ta D/M/R boş (yalnız yeni WorkflowRetentionSourceGuardTests.cs); WorkflowTransitionLog.cs yalnız `TargetPrincipalId` ekler; D2.cs ve D2Fix1.cs değişmedi.

## Tüketici sınıfları (kod değişmedi; yeni yanıtla gövde kanıtı değil: sahte ağ geçidi) — sahip kaydı sütunu: bekliyor
Platform içi (DOĞRULA 3 koşusu, hepsi Passed): TaskApprovalGateTests 14/14, TaskWorkflowLinkGateTests 83/83, TaskReviewGateTests 15/15, TaskReviewHandoffTests 12/12, TaskUpwardRequestTests 10/10, TimesheetApprovalHttpMongoTests 42/42 (istisna 2 dahil), TimesheetHardeningHttpMongoTests 13/13, WorkItemActionDispatchTests 48/48, ApprovalSourceContextTests 19/19, WorkflowCrossServiceTests 45/45.
Servis dışı (DOĞRULA 7, ayrı ayrı, hepsi 0 failed): CRM (ClaimsWorkflowApprovalTests, KnowledgePathReviewTests) 71/71; Inventory (QuarantineWorkflowTests) 22/22; MDM (WorkflowApprovalCoreTests) 18/18; HCM (GatewayWorkflowStartClientTests) 1/1; Web (ClaimsV2ProxyTests) 24/24. MDM `GatewayWorkflowClient` tüketici testi ve Web geçmiş gövdesi testi yoktur: "sahibinde, koşmadı". Treasury ve İzin (MOD-0280-FU02): "kod yok, koşmadı".
Satır satır tablo (dosya:satır, gördüğü yanıt, kodun yaptığı, anahtar kurulumu, "bu durumu gönderebilir mi" ölçümü, test adları): `WF-S17-DL2-consumer-table-draft.md` (bu klasör; taslak adı kaldı, içerik son; alt ajan okuyarak çıkardı, çıkarımlar işaretli). Öne çıkanlar: (1) Inventory karantina yeniden gönderimi: aynı anahtar, değişebilir aday ve son tarih → olası 400 WorkflowRejected; (2) Task Center anahtarı aktör-, metin-, imzasız: kayıp yanıt sonrası yeni gerekçe 409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`, ön yüzde eşleme yok; (3) görev onayı, incelemesi ve yukarı isteği son tarih satırı (TaskApprovalService.cs:249, TaskReviewService.cs:319, TaskUpwardRequestService.cs:122): görevin son tarihi değişirse aynı anahtarla tekrar artık 409; onay ve inceleme supersede + bir yeniden başlatmayla karşılıyor, yukarı istek oluşturmada bir kez gider; (4) CRM/MDM başlatma: başka kişi 400; (5) Web konsolunda anahtar düzenlenebilir, Cancel 403 "izin yok" görünür.
Timesheet `TimesheetFinalizer.cs:239`: sahibin kendi kararında `CancelAsync` dönüşünü okumaz; 403 ya da 409 alsa da hafta Draft'a döner (okunarak doğrulandı; tablo satır 6d). `:219` `OutcomeKey` MOD-0023 çağrısı değil, sonlandırma anahtarıdır.
HCM `GatewayWorkflowStartClient.cs:13`: paket listesinde yok; ölçüldü: DI'de kayıtsız, çağıranı yok, gövdesi `StartWorkflowInstanceRequest` ile uyuşmuyor, yanıtta `definitionKey` bekliyor; uç uca çalışmıyor ("uyumlu" yazılmaz); HCM sahibine not.

## Açık kayıtlar
- AC-W15 dördüncü depo için S17-8 ile açık.
- Genel uçtan güvenilir örnek tekrarı 200→409 (paket yazmıyor; `AC_W11_trusted_whole_start_replayed_through_the_generic_handler` ile sabit).
- Cancel geç gelen ikizi değişmedi (InvalidState).
- AC-W17 ve AC-W18 sahip kayıtları bekliyor (MDM testi D32/5).
- TRM-15 bu işle kapanmaz. O-S17-1 açık.
- HCM istemcisi uç uca çalışmıyor (HCM sahibine not).
- Karar isteyen tüketici bulguları: yukarıdaki (1)–(5) ve taslağın "Findings" bölümü.
- İki mevcut test v1.8 istisnasıyla güncellendi (yukarıda).
- Mongo: kendi mongod'um (PID 30125, başlangıç Mon Nov 2 02:22:12 2026) PID + saat doğrulanarak kapatıldı, 37994 boş.
