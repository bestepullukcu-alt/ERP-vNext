@[.antigravity/agents/backend-architect.md]
WP: WF-S17-DL2 · Prompt PSS-WF-S17-DL2-P v0.1 (TASLAK — dağıtım izni değil; paket onayından ve DL-1 main'e girdikten sonra yeniden üretilir)
Repository: /Users/alitufanoglu/ERP-vNext · Branch: <<DOLDUR: feature/pss/wf-s17-dl2-r1>> · Expected HEAD: <<DOLDUR: DL-1 birleştikten sonra CT dalının ucu>> · Worktree: <<DOLDUR: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-s17-dl2-r1>>
Risk: HIGH · §9.1 sinyalleri: yazma yolu, eşzamanlılık, sözleşme (yanıt kodları), tüketici uyumu
Görev sınıfı: ürün · Paket: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTerminalTransitionWriter.cs (yalnız SamePayload paylaşılırsa), services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/RejectWorkflowTaskHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs, ince eylem işleyicileri (Approve/Delegate/RequestInfo/CancelWorkflowTaskHandler.cs — yalnız gerekirse), services/Diten.Platform/tests/** — <<DOLDUR: onaylı §17.9 madde 14 ile birebir karşılaştır>>
Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/WorkflowInstanceStartCoordinator.cs ve TrustedWorkflowCancellationCoordinator.cs (§17.9/12: kapsam dışı), services/Diten.Platform.Common/**, Features/TimeEntry/**, Features/Tasks/**, Features/WorkAggregation/** (tüketici — XMC-001), diğer servisler, gateway/**, frontend/**, .antigravity/**, AGENTS.md, docs/records/**, execution/**
Kapsanan dilim: DL-2

Önce oku (sırayla):
1. execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md — §17 (17.1–17.9) ve Teslim dilimleri
2. AGENTS.md
3. .antigravity/rules/git-safety.md
4. .antigravity/rules/cross-module-change-consent.md
5. docs/records/audits/2026-10/mod0023-s17-inspection-01/INSPECTION.md
6. docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md (D25, D32)
7. <<DOLDUR: onaylı paketin son bağımsız inceleme kaydı>>

Sözleşme yüzeyleri:
- API: <<DOLDUR: «idempotentReplay» ve 409 kodu (paket:satır)>>
- İzin: N/A — izin, aktör bağlama ve SoD değişmez (§17.6)
- Test: <<DOLDUR: AC-W5…W12, W14, W17, W18 satırları (§17.9'daki haliyle)>>
- Sabotaj: «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md Çekirdek 10)

<<DOLDUR: Keşif satırı ve kararları>>

NE: MOD-0023 §17 DL-2 — (1) bütün başlatmanın tekrarında olguların ve başlatanın karşılaştırılması: aynı anahtar + farklı olgu → 409 (bugün 200, StartWorkflowInstanceHandler.cs:385-391), yanıta idempotentReplay (AC-W11). (2) Geçiş tekrarında gövde karşılaştırması: aynı anahtar + farklı gövde → 409; farklı aktör 403 WORKFLOW_ACTOR_DENIED kalır (AC-W14; §17.9/7). (3) İptal tekrarı aktöre bağlanır (AC-W18; bugün :622-630 bağsız). (4) Yarış ve tekrar durumları §17.9 madde 9–10'daki zorlanan durumlarla ölçülür (AC-W5…W8). (5) Tüketici uyumu AC-W17: Inventory, CRM, MDM, Web, Treasury ve Platform içi çağıranlar (Tasks, Timesheet — bugün 200 bekliyor, WorkCenter dağıtıcısı): ya kendi testi geçer ya da tüketici sahibi "bu durumu hiç göndermez" + kod satırı kaydeder.
NEDEN: D25/1 ve D25/3; §17.7/5, §17.8/5, §17.9. Fingerprint düzeltmesi yazma yolu değişikliğidir, yalnız bu dilimde sahibin onayıyla (D25/1). Yazar ≠ birleştiren (D32/1).
NASIL:
- Makine yükü kuralı (DL-1 ile aynı). Replika kümesi gerektiren testler: AuditOutboxTemporalReplicaSetFixture (DITEN_FU02_MONGOD_PATH ya da DITEN_FU02_REPLICA_MONGO) — <<DOLDUR: CT ortam ölçümü>>.
- Mevcut bariyer kalıpları: WorkflowCandidateDecisionMongoTests* TwoCallerBarrier, WorkflowStartRecoveryMongoTests AllReservedBarrier; AC-W5 ayrıca bariyersiz bağımsız istekler (§17.9/11).
- Var olan SamePayload kuralı (WorkflowTerminalTransitionWriter.cs:399-438) sıralı denetimde yeniden kullanılır; kopyalanmaz.
- Commit ve teslim (S-63): push ve PR yok; commit sonrası dur ve bildir.
YAPMA:
- Güvenilir koordinatörler, tüketici modüllerinin kodu (Tasks, TimeEntry, WorkAggregation, diğer servisler) — tüketici etkisi raporlanır, düzeltilmez (XMC-001).
- 409 için WORKFLOW_TRANSITION_CONFLICT'i yeniden kullanma (yeniden denenebilir sayılıyor, §17.7/6) — <<DOLDUR: onaylı paketteki kod adı>>.
- İzin, aktör bağlama, SoD değişikliği; push, PR, merge, dal değiştirme.
DOĞRULA: <<DOLDUR: tekil csproj komutları, replika kümesi koşusu, tüketici test projeleri (Inventory, CRM, Web), known_failures.py check>>
Sabotajlar: <<DOLDUR: fingerprint karşılaştırmasını kaldır → AC-W11 kırmızı; gövde karşılaştırmasını kaldır → AC-W14 kırmızı; iptal aktör bağını kaldır → AC-W18 kırmızı>>
Durma koşulları:
- Platform içi bir tüketici (Timesheet) yeni 409 ile kırılıyorsa → düzeltme yapmadan DUR ve adıyla raporla (XMC-001; tüketici sahibine gider).
- İş İzinli yolların dışında değişiklik gerektiriyorsa → DUR.
- Expected HEAD / dal farklı, kirli başlangıç, taban çizgisinde kırmızı → DUR.
Rapor formatı: SOP §22 yapılandırılmış rapor; dal, HEAD sha, test sayıları, trx yolları, tüketici başına AC-W17 sonucu, sabotaj başına kırmızı test adı.
