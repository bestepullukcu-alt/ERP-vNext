@[.antigravity/agents/backend-architect.md]
WP: WF-S17-DL1 · Prompt PSS-WF-S17-DL1-P v0.1 (TASLAK — dağıtım izni değil; paket onayından sonra yeniden üretilir)
Repository: /Users/alitufanoglu/ERP-vNext · Branch: <<DOLDUR: feature/pss/wf-s17-dl1-r1>> · Expected HEAD: <<DOLDUR: paket onay satırı main'e girdikten sonra CT dalının ucu>> · Worktree: <<DOLDUR: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-s17-dl1-r1>>
Risk: HIGH · §9.1 sinyalleri: veri saklama (silmeme politikası), kiracı sınırı, sözleşme (okuma alanları)
Görev sınıfı: ürün · Paket: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs, services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstancesByObjectsHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstanceHistoryHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetWorkflowInstancesByObjectsQuery.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs, services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs, services/Diten.Platform/tests/**, tests/architecture/** (yalnız yeni kural) — <<DOLDUR: onaylı §17.9 madde 2'nin son metniyle birebir karşılaştır>>
Korunan yollar: services/Diten.Platform.Common/**, .antigravity/**, AGENTS.md, gateway/**, frontend/**, docs/records/**, execution/**, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/** (DL-2'nin), services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetWorkflowInstanceHistoryQuery.cs (O-S17-2 = b), diğer servisler
Kapsanan dilim: DL-1

Önce oku (sırayla):
1. execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md — §17 (17.1–17.9) ve Teslim dilimleri
2. AGENTS.md
3. execution/domains/platform-shared-services/domain-config.md
4. .antigravity/rules/multi-tenancy.md
5. .antigravity/rules/git-safety.md
6. .antigravity/rules/cross-module-change-consent.md
7. docs/records/audits/2026-10/mod0023-s17-inspection-01/INSPECTION.md
8. docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md (D21, D22, D25, D32)
9. <<DOLDUR: onaylı paketin son bağımsız inceleme kaydı docs/records/reviews/MOD-0023/…>>

Sözleşme yüzeyleri:
- API: <<DOLDUR: «by-objects includeDeleted …» (paket:satır, onaylı sürüm)>>
- İzin: «no new permission; the existing `instances.view`» (paket §17.8/7) — <<DOLDUR: satır>>
- Test: <<DOLDUR: AC-W1, W2, W3, W4, W13, W15, W16 satırları (§17.9'daki haliyle)>>
- Sabotaj: «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md Çekirdek 10); AC-W15/W16 sabotajları (paket:satır)

<<DOLDUR: Keşif: <sonuç> · rapor <yol>@<commit> · sha256 <hex>>>
<<DOLDUR: her BULGU için: Keşif kararı <n>: parent — MOD-0023 §17 dilimi …>>

NE: MOD-0023 §17 DL-1 — (1) silmeme politikası: üç depo (WorkflowInstance, ApprovalTask, WorkflowTransitionLog) devralınan RepositoryBase<T>.DeleteAsync'i override ederek reddeder (yazmadan atar); arayüzlere silme üyesi eklenmez; Platform.Common değişmez (AC-W1, AC-W15). (2) Eklemeli okuma alanları: by-objects → idempotencyKey, isDeleted; history → approvalTaskId, idempotencyKey, isDeleted (AC-W2). (3) by-objects?includeDeleted=true: IWorkflowInstanceRepository'ye tek yeni okuma üyesi (kiracı koşulu korunur, yalnız IsDeleted == false düşer); varsayılan davranış değişmez (AC-W13). (4) Kiracı ve izin sınırı (AC-W16, §17.9 madde 15'teki haliyle: by-objects başka kiracı nesnesi → boş liste 200; history / instances/{id} → 404). (5) Tüketici uyumu AC-W3 (Inventory, CRM, MDM, Web, Platform'un kendi testleri).
NEDEN: TRM-15 — Treasury bilinmeyen sonuçtan sonra kendi örneğini anahtarla bulmalı ve silinmiş satırı görüp yeniden başlatmamalı (paket §17.1, §17.7, §17.8, §17.9). Kodu MOD-0023 sahibi yazar, başka kişi merge eder (D32/1).
NASIL:
- Makine yükü: her dotnet build / test öncesi uptime; 1 dakikalık yük 12'nin üstündeyse 60 sn bekle, yeniden ölç. Testler proje proje; çözüm genelinde dotnet test yok.
- Önce taban çizgisi: DOĞRULA 1–3 değişiklik yapmadan; sayıları yaz.
- AC-W15 davranış testi gerçek Mongo ile (sahte depo kanıt değil, §17.9/1); mevcut *MongoTests kalıbı (services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/). Kaynak-metin kuralı tests/architecture/TenantArchitecture.ArchitectureTests altında.
- Kişi alanları: §17.8/4 + §17.9 listesi; RuntimeAssignmentSnapshot'ı ölç ve raporda yaz (kod değişikliği yok, O-S17-1 açık).
- Ortam sayımı (§17.8/2): her ortamda üç tipte isDeleted: true sayısı — CT/sahip ölçer, işçi yazmaz.
- Commit ve teslim (S-63): push ve PR yok; commit sonrası dur ve bildir.
YAPMA:
- CommandHandlers/** (DL-2), GetWorkflowInstanceHistoryQuery.cs ve history eylemine includeDeleted (O-S17-2 = b), Platform.Common, gateway, frontend, başka servis (XMC-001).
- Yeni izin, yeni rota, indeks ya da şema değişikliği; silme üyesi; TTL.
- Push, PR, merge, dal değiştirme, stash, reset, rebase.
DOĞRULA: <<DOLDUR: tekil csproj komutları (Diten.Platform.Application.Tests, TenantArchitecture.ArchitectureTests, tüketici test projeleri), known_failures.py check, AC başına beklenen test adları>>
Sabotajlar: <<DOLDUR: AC-W15 — override kaldır; filtreyi DeletedAt'e geri al; arayüze silme üyesi ekle; AC-W16 — includeDeleted dalında kiracı koşulunu düşür → adı verilen test kırmızı>>
Durma koşulları:
- İş İzinli yolların dışında değişiklik gerektiriyorsa → DUR ve raporla (XMC-001).
- Expected HEAD / dal farklı ya da başlangıçta kirli dosya → DUR.
- Taban çizgisinde kırmızı → değişiklik yapmadan DUR.
- Paket ile kod arasında §17.9'da çözülmeyen çelişki → DUR.
Rapor formatı: SOP §22 yapılandırılmış rapor; dal, HEAD sha, test sayıları ve trx yolları; sabotaj başına kırmızı test adı.
