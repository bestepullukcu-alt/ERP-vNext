@[.antigravity/agents/backend-architect.md]
WP: WF-S17-DL1 · Prompt PSS-WF-S17-DL1-P v1.0
Repository: /Users/alitufanoglu/ERP-vNext · Branch: feature/pss/wf-s17-dl1-r1 · Expected HEAD: 48cf76bda7719c2db62332fc8a0ac1789fe560b1 · Worktree: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-s17-dl1-r1
Risk: HIGH · §9.1 sinyalleri: silme, kiracı, sözleşme
Görev sınıfı: ürün · Paket: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstancesByObjectsHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstanceHistoryHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetWorkflowInstancesByObjectsQuery.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs, services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs, services/Diten.Platform/tests/**, tests/architecture/TenantArchitecture.ArchitectureTests/**
Korunan yollar: services/Diten.Platform.Common/**, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetWorkflowInstanceHistoryQuery.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/**, services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/**, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/**, services/Diten.CrmService/**, services/Diten.InventoryService/**, services/Diten.MdmService/**, frontend/**, gateway/**, .antigravity/**, AGENTS.md, docs/records/**, execution/**
Kapsanan dilim: DL-1

Önce oku (sırayla):
1. execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md — §17 ve Teslim dilimleri; §17.9 madde 1–6, 15, 16 öncelikli
2. AGENTS.md
3. execution/domains/platform-shared-services/domain-config.md
4. .antigravity/rules/multi-tenancy.md
5. .antigravity/rules/security-jwt.md
6. .antigravity/rules/git-safety.md
7. .antigravity/rules/cross-module-change-consent.md
8. docs/records/audits/2026-10/mod0023-s17-inspection-01/INSPECTION.md
9. docs/records/decisions/2026-10/mod0023-s17-owner-decisions-01.md
10. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowCrossServiceTests.cs

Sözleşme yüzeyleri:
- API: «`by-objects` and `history` return `isDeleted`; `by-objects` hides deleted rows» (execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md:666)
- İzin: «no new permission; the existing `instances.view`» (execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md:731)
- Test: «AC-W15 — **One deletion marker; inherited delete closed (DL-1):**» (execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md:751)
- Sabotaj: «Sabotage: dropping the tenant condition on the `includeDeleted` branch» (execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md:1364)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/mod0023-s17-inspection-01/DISCOVERY-MOD-0023.txt@48cf76bda · sha256 75cbd40bc1c1c3be02b41cde5bac7f6591b9c371b9040ea126823bdd39b94f7c
Keşif kararı 1: child — MOD-0023-FU01 (global nesne geçiş kapısı) aynı modülün önceki dilimidir; bu işin yolları ona dokunmaz
Keşif kararı 2: child — MOD-0023-FU02 (güvenilir başlatma ve karar kanıtı) aynı modülün önceki dilimidir; güvenilir koordinatörler bu işin kapsamı dışındadır (§17.9/12)
Keşif kararı 3: child — MOD-0023-FU03 (iş öğesi izdüşümü) aynı modülün önceki dilimidir; bu iş ona dokunmaz
Keşif kararı 4: child — MOD-0023-FU04 (kiracı hedefli tanım yönetimi) aynı modülün önceki dilimidir; bu iş ona dokunmaz
Keşif kararı 5: parent — MOD-0023 paketi bu işin yetki paketidir (§17 revizyon 3, sha256 ec942baa…, main'de onaylı); yeni modül açılmaz
Keşif kararı 6: parent — MOD-0023-workflow-designer.md eski paket adıdır (künyede previous, yerini bu paket aldı); yeni iş açmaz
Keşif kararı 7: parent — PVG-M1-09 PVG kuyruğunda bu modüle bağımlılık takibidir; PVG yazmaz, bu işle çakışmaz
Keşif kararı 8: parent — TRM-15 Treasury kuyruğunda bu işin tüketici tarafı takibidir; kodu bu iş yazar (D25/1)
Keşif kararı 9: parent — WF-S17-DL1 bu işin kendi satırıdır
Keşif kararı 10: child — WF-S17-DL2 aynı paketin DL-2 dilimidir; yazma yolu dosyaları ondadır, bu işin yolları ayrıktır
Keşif kararı 11: child — WF-TPL-01 aynı kuyrukta şablon kurulumu; bu işle yol paylaşmaz
Keşif kararı 12: yeni — HCM-EMP-05 MOD-0251 kuyruğunda iş akışı anılması; bu iş ona dokunmaz
Keşif kararı 13: yeni — HCM-LEAVE-06 izin kuyruğunda MOD-0023 tüketicisi; bu işin okuma alanları ona eklemeli, onu değiştirmez

Hedef ajan giriş alanları (§17.4, backend-architect):
- Hedef servis ve proje yolları: Diten.Platform · yukarıdaki İzinli yollar · test projesi services/Diten.Platform/tests/Diten.Platform.Application.Tests · kaynak-metin kuralı tests/architecture/TenantArchitecture.ArchitectureTests
- Aggregate / alanlar: entity değişmez; WorkflowInstanceStatusDto ve WorkflowInstanceHistoryEntryDto'ya sona eklemeli isteğe bağlı üyeler
- Komut / sorgu listesi: GetWorkflowInstancesByObjectsQuery'ye sona isteğe bağlı IncludeDeleted (varsayılan false); komut yok
- Neden kodları: yeni kod yok; 404 NotFoundNonLeakage aynen
- Doğrulayıcı kuralları: yok
- İzin anahtarları: yeni anahtar yok; instances.view aynen
- Response<T> ve CustomBaseController: mevcut kalıp aynen

NE: MOD-0023 §17 DL-1 — (1) Silmeme: WorkflowInstanceRepository, ApprovalTaskRepository, WorkflowTransitionLogRepository içinde devralınan DeleteAsync override edilir, yazmadan reddeder (AC-W1, AC-W15; arayüzlere silme üyesi yok; Platform.Common değişmez). (2) Eklemeli okuma alanları: by-objects → idempotencyKey, isDeleted; history → approvalTaskId, idempotencyKey, isDeleted (AC-W2, §17.7/4). history'de isDeleted dönen satırlarda hep false'tur, çünkü silinmiş satır süzülür. (3) by-objects?includeDeleted=true: IWorkflowInstanceRepository'ye tek yeni okuma üyesi (ListByObjectIdsAsync ikizi; kiracı koşulu kalır, yalnız IsDeleted == false düşer); varsayılan davranış değişmez (AC-W13). Varsayılan arayüz gövdesi NotSupportedException atar, yalnız WorkflowInstanceRepository uygular (§17.9/2). (4) Kiracı ve izin sınırı (AC-W16, §17.9/15). (5) AC-W2 kiracı cümlesi §17.9/16'daki haliyle. (6) Etkisiz handler süzgeci `.Where(x => x.DeletedAt is null)` kaldırılabilir (§17.9/1a).
NEDEN: TRM-15 — Treasury bilinmeyen sonuçtan sonra kendi örneğini anahtarla bulmalı ve silinmiş satırı görüp yeniden başlatmamalı. Paket §17.1, §17.7, §17.8, §17.9; sahip kararı O-S17-2 = (b): history'ye includeDeleted yok. Kodu MOD-0023 sahibi tarafı yazar, başka kişi merge eder (D32/1).
NASIL:
- Makine yükü: her dotnet build / test komutundan önce uptime koş; 1 dakikalık yük 12'den büyükse 60 saniye bekle ve yeniden ölç; 12'nin altına inmeden derleme başlatma. Testleri proje proje koş; çözüm genelinde dotnet test yok.
- Önce taban çizgisi: DOĞRULA 1–2 değişiklik yapmadan; geçen / kalan sayıları yaz.
- AC-W13, AC-W15, AC-W16 kanıtı gerçek depoda Mongo ile (§17.9/1c; sahte depo kanıt değildir). services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow altındaki *MongoTests kalıbını kullan; deleted fixture IsDeleted = true, DeletedAt = null.
- AC-W15 davranış testi: silmeyi türetilmiş depo ve RepositoryBase<T> / TenantRepository<T> başvurusu üzerinden çağırır; ikisi de reddeder; satır değişmez (IsDeleted false, UpdatedAt aynı).
- AC-W1 kaynak-metin kuralı tests/architecture/TenantArchitecture.ArchitectureTests altına: üç depoda silme / IsDeleted yazımı / ExpireAfter yok, üç arayüzde silme üyesi yok, şema manifesti PlatformSchemaManifest.WorkflowWorkCenter.cs'te ExpireAfter yok.
- Yeni alanlar kayıtların sonuna isteğe bağlı üye olarak; AC-W4 için `git grep -n "WorkflowInstanceStatusDto\|WorkflowInstanceDto\|WorkflowInstanceHistoryEntryDto"` ile kurulum yerlerini tara ve raporda yaz.
- Kişi alanları (§17.9/4): yalnız ölç ve raporda yaz (RuntimeAssignmentSnapshot.TieBreakExplanation, CreatedBy / UpdatedBy dolu mu); kod değişikliği yok; O-S17-1 açık.
- Commit ve teslim (S-63): kendi dalında yerel commit; push ve PR yok; commit sonrası dur ve CT'ye dal adı, HEAD sha'sı ve test sonucunu bildir.
YAPMA:
- CommandHandlers/**, Services/** (DL-2), GetWorkflowInstanceHistoryQuery.cs ve history eylemine includeDeleted (O-S17-2 = b), Platform.Common, entity dosyaları, gateway, frontend, başka servis (XMC-001).
- Yeni izin, yeni rota, indeks ya da şema değişikliği; arayüze silme üyesi; TTL.
- Tüketici servislerin kodunu ya da testini değiştirme; kırılırsa DUR ve adıyla raporla.
- Push, PR, merge, dal değiştirme, stash, reset, rebase.
DOĞRULA: (komutlar worktree kökünde; her dotnet komutundan önce uptime)
1. dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug --filter "FullyQualifiedName~Workflow" --logger "trx;LogFileName=wf-dl1.trx" → 0 failed; sayı = taban çizgisi + yeni testler; WorkflowCrossServiceTests yeni alanlarla geçer (AC-W3 Platform satırları).
2. dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj -c Debug --logger "trx;LogFileName=wf-dl1-arch.trx" → 0 failed.
3. python3 scripts/known_failures.py check services/Diten.Platform/tests/Diten.Platform.Application.Tests/TestResults/wf-dl1.trx --suite service-tests-platform-app → çıkış 0.
4. dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug → 0 hata (AC-W4).
5. AC-W3 tüketici koşusu (değişiklik yok, yalnız koş): dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj --filter "FullyQualifiedName~ClaimsWorkflowApprovalTests|FullyQualifiedName~KnowledgePathReviewTests" ve dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Diten.InventoryService.Tests.csproj --filter "FullyQualifiedName~QuarantineWorkflowTests" → 0 failed; MDM ve Web satırları bu işin dışında (sahipleri ayrıca kanıtlar; raporda "koşmadı" yaz).
Sabotajlar (her biri geçici; ilgili testi koş, adı verilen testin kırmızısını gör, geri al, yeşil; kırmızı test adlarını raporla; derleme hatası kırmızı sayılmaz):
6. S1 — üç depodan birinin DeleteAsync override'ını kaldır → AC-W15 davranış testi ve AC-W1 kuralı kırmızı.
7. S2 — includeDeleted dalını ExecutionFilter üzerinden oku (bayrak yok sayılır) → AC-W13 testi kırmızı; varsayılan dalı ExecutionFilter'sız oku → varsayılan gizleme testi kırmızı.
8. S3 — yeni okuma üyesinde kiracı koşulunu düşür → AC-W16 testi kırmızı.
9. S4 — IApprovalTaskRepository'ye bir Delete üyesi ekle → AC-W1 kuralı kırmızı.
10. S5 — şema manifestine ExpireAfter ekle → AC-W1 kuralı kırmızı (Korunan yol: yalnız sabotaj için geçici değil; bu sabotajı kaynak-metin kuralının test verisiyle yap, manifest dosyasına yazma).
11. git diff --check temiz; git status --porcelain yalnız İzinli yolları gösterir; git diff --stat -- services/Diten.Platform.Common boş.
Durma koşulları:
- İş İzinli yolların dışında değişiklik gerektiriyorsa → DUR ve adıyla raporla (XMC-001).
- Worktree HEAD Expected HEAD'den farklı, dal farklı ya da başlangıçta kirli dosya → DUR.
- Taban çizgisinde bilinen borç dışında kırmızı → değişiklik yapmadan DUR.
- Paket ile kod arasında §17.9'da çözülmeyen çelişki → DUR.
- Bir tüketici testi yeni alanlarla kırılırsa → düzeltme yapmadan DUR ve testi adıyla raporla.
- 1 dakikalık yük 15 dakika boyunca 12'nin altına inmiyorsa → DUR ve uptime çıktılarıyla raporla.
- Commit dışında push, PR, merge gerekirse → DUR.
Rapor formatı: SOP §22 yapılandırılmış rapor (Agent Verdict · Branch / HEAD · Worktree status · Changed files · Contract flow · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Sabotaj başına kırmızı test adı · Kişi alanı ölçümü · AC başına durum).
