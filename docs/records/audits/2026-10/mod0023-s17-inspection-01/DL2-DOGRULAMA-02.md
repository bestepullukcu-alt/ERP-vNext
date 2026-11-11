# WF-S17-DL2 — doğrulama 02: guard testleri (2026-11-02)

- Teslim: `07caa63c18c1f176c488dc66adc811ca7889a847` (ana commit `1e5615697` üstünde) · dal `feature/pss/wf-s17-dl2-r2`. Push yok.
- Konu: `DL2-DOGRULAMA-01.md` B-1, B-2, B-3 açıkları. CT kararı: aynı iş kapsamı, izinli test yolu, üretim kodu değişmez.
- Fark: 3 test dosyası, +61 satır, hepsi `services/Diten.Platform/tests/**`; üretim kodu farkı boş (CT okudu).

| Açık | Test | İşçinin sabotajı → kırmızı |
|---|---|---|
| B-1 | `WorkflowSameKeyHttpMongoTests.AC_W14_signature_id_is_part_of_the_body`: düz dizge `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT` ve ham gövdede `"reason_code":"…"` | Sabit değeri `WORKFLOW_TRANSITION_CONFLICT` → kırmızı |
| B-2 | `WorkflowCandidateDecisionMongoTests.AC_W14_another_tenant_sending_the_task_id_and_key_of_a_recorded_transition_gets_404` (8 satır: dört eylem × aynı/başka aktör; boş olmama kontrolü, satır sayısı değişmez) | `ExecutionFilter` kaldırıldı → 8 satır kırmızı (409 / 403) |
| B-3 | `WorkflowStartRecoveryMongoTests.AC_W11_idempotent_replay_flag_is_true_when_a_start_told_unavailable_is_found_whole` | `idempotentReplay: false` → kırmızı |

- İşçinin son koşusu: `~Workflow` + `TimesheetApprovalHttpMongoTests` 1046/1046, parmak izi boş, `check` YENİ=0; mimari 216/210/6, parmak izi ve `check` tabanla aynı.
- CT ölçmedi: sabotajları yeniden koşmadı; test metnini ve farkı okudu. Sonuç: B-1, B-2, B-3 kapandı. B-4…B-6 açık kalır (B-4 AC-W17 sahip kaydına yazılır).
