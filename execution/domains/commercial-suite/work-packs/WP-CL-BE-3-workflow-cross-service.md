# WORK PACKAGE — WP-CL-BE-3 · MOD-0023 Workflow'u servisler arası kullanıma aç (DisplayContext · tamamlanma olayı · aday düzeltmesi · toplu okuma) (Platform, backend)

> **CT (SoR).** İddialar v2 Faz 1. Kaynaklar:
> - `SCMM-claims-v2-mockup-analysis-plan.md` (CL-BE-3)
> - `SCMM-claims-approval-evidence-roadmap.md` (§A1, §E)
>
> **Amaç:** CRM, iddia onayını MOD-0023 üzerinden başlatabilsin (CL-BE-4). Onay WorkCenterNext'te anlamlı bir başlıkla görünsün, sonuç CRM'e olay olarak dönsün, onaylayıcı adayları doğru çözülsün.
>
> **Kapsam:** Yalnız Platform workflow ve work-aggregation. **CRM, AuthService ve frontend DEĞİŞMEZ.**
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-3`, dal `wp/cl-be-3` (taban `feature/crm-claims-v2` @ `3e262a6d`). Commit bu dala yapılır, push YOK.

## Kanıt (CT kod okuması)
- **Başlatma:**
  - `StartWorkflowInstanceRequest` (`Platform.Application/Features/Workflow/WorkflowModels.cs:146`): TemplateId/TemplateCode, ObjectType/ObjectId/ObjectRef, CandidatePrincipalIds, ReasonCode, IdempotencyKey, CommentRequired, EvidenceRequired, DueAt. **Görüntü bilgisi yok.**
  - `WorkflowInstance` (`Platform.Domain/Entities/Workflow/WorkflowInstance.cs`): Object*, CurrentStage/Step, Status, StartedBy/At, CompletedAt…
- **Terminal durumlar:**
  - `WorkflowTaskTransitionSupport.cs:152` → onay ya da ret ile instance tamamlanır.
  - `RunWorkflowEscalationsHandler.cs:164` → TimedOut.
  - İptal: cancel komutu.
  - **Olay yayınlanmıyor.** Tasks (MOD-0024) sonucu pull ile okuyor (`Tasks/Services/TaskApprovalService.cs`).
- **Outbox:** `Platform.Application/Contracts/Eventing/ITransactionalOutboxEventWriter.cs` + `Services/Eventing/TransactionalIntegrationEventWriter.cs`. Transport dev'de **RabbitMQ** (`appsettings.Development.json` `Eventing.Transport`). AuthService tüketiyor (`AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`).
- **WorkCenterNext:**
  - `WorkAggregation/Providers/WorkflowApprovalWorkItemProvider.cs` onay görevlerini projekte ediyor.
  - Başlık ve link `IApprovalSourceResolver` üzerinden geliyor (`WorkAggregation/Services/IApprovalSourceResolver.cs`). Bugün yalnız `Tasks/Providers/TaskApprovalSourceResolver.cs` var.
  - Başka modül için başlık "Onay: {objectType} {guid}" gibi anlamsız çıkıyor.
- **Aday çözümü** (`Workflow/Handlers/CommandHandlers/WorkflowCandidateResolver.cs`):
  - `position:{id}` adayları için **`GetAllAsync()`** ile TÜM atamaları okuyor. Yalnız tarih aralığına bakıyor.
  - **`IsCancelled` ve pozisyon durumu (Frozen/Closed/Draft) kontrol edilmiyor** → iptal edilmiş atama ya da kapatılmış pozisyon onaylayıcı olabiliyor.
- **SoD:** yalnız "gönderen onaylayamaz" (`WorkflowTaskTransitionSupport.cs:112`). Kullanıcı kararıyla aynı kişinin ardışık adımları onaylaması **serbest kalır**; bu kural değişmez.
- **Gateway:** `/api/v1/workflow/{everything}` ve `/api/v1/work-items/*` route'ları **var**.

## NE
1. **DisplayContext (görüntü bağlamı):**
   - `StartWorkflowInstanceRequest`'e isteğe bağlı `WorkflowDisplayContext? DisplayContext` eklenir: `{ Title (≤200), Subtitle (≤300), SourceModule (≤64, ör. "crm"), DeepLinkUrl (≤500, **yalnız "/" ile başlayan göreli yol**; mutlak URL ya da `javascript:` → 400), Chips (≤5 kısa etiket, ör. "TR", "v1.0") }`.
   - Instance'a snapshot olarak yazılır, sonra değişmez.
   - Yeni `SnapshotApprovalSourceResolver`: DisplayContext'i olan ve objectType'ını sahiplenen modül resolver'ı **olmayan** instance'lar için başlık, alt başlık ve link döner.
   - **Öncelik:** sahip resolver (Tasks) > snapshot > mevcut yedek. Tasks davranışı birebir aynı kalır.
2. **Tamamlanma olayı:**
   - Instance terminal duruma geçtiğinde (Approved / Rejected / Cancelled / TimedOut) **aynı transaction içinde** outbox'a `platform.workflow.instance.completed` (v1) yazılır.
   - Payload: `{ tenantId, workflowInstanceId, templateCode, templateVersionId, objectType, objectId, objectRef, outcome, completedAt, completedBy (userId), finalStageCode, finalStepCode, reasonCode }`. Yorum metni ve PII yok.
   - Olay adı, sürümü ve payload doğrulaması mevcut eventing sözleşmesine göre yazılır (`BuildingBlocks.Eventing` EventName / PayloadContractValidator + Platform olay kaydı neredeyse).
   - Aynı instance için **tek olay** (idempotent: ikinci terminal geçiş olmaz, olursa olay tekrar yazılmaz).
3. **Aday düzeltmesi** (`WorkflowCandidateResolver`):
   - Atamalar **pozisyon id'leriyle sorgulanır** (repository'ye `GetActiveByPositionIdsAsync(ids, at)` ya da eşdeğeri, tenant-scoped).
   - `IsCancelled == true` ve silinmiş atamalar hariç tutulur.
   - **Pozisyonu `Active` olmayan** (Draft/Frozen/Closed ya da arşivlenmiş) atamalar hariç tutulur.
   - `AssignmentType` Primary / Secondary / Acting / Delegated → **hepsi geçerli**.
   - Bu düzeltme start, reassign ve escalation çağrılarının hepsini etkiler.
4. **Toplu durum okuma** (CRM uzlaştırması için):
   - `GET api/v1/workflow/instances/by-objects?objectType={t}&objectIds={id1,id2…}` (en çok 100 id).
   - Her nesne için instance listesi döner, en yenisi önce: `{objectId, instances:[{workflowInstanceId, status, outcome, currentStageCode, currentStepCode, startedAt, completedAt}]}`.
   - İzin: `platform.workflow.instances.view`. Kiracı izolasyonu uygulanır.
   - Mevcut `GET instances` bozulmaz.
5. **Dokümantasyon:**
   - WP'ye §"Tüketici rehberi" eklenir: servisler arası başlatma örneği (user token iletimi → StartedBy = gönderen → SoD çalışır), DisplayContext örneği, olay payload örneği, uzlaştırma çağrısı.
   - `WorkflowModels.cs` yorumları güncellenir.

## KORU / YAPMA
- **MOD-0023 mevcut uçları ve sözleşmeleri geriye uyumlu kalır** (DisplayContext opsiyonel). Tasks (MOD-0024) akışı, `TaskApprovalSourceResolver`, WorkCenterNext dispatcher ve aksiyonlar, SLA/eskalasyon davranışı DEĞİŞMEZ; yalnız aday filtresi sıkılaşır.
- **Paralel onay YOK** (motor sıralı kalır; bu WP'de değil).
- CRM, AuthService, frontend, ocelot DOKUNMA. Yeni izin anahtarı YOK (mevcut `platform.workflow.*`).
- Yeni base entity, repository base, eventing altyapısı YAZMA; mevcutları kullan (`TenantScopedEntity`, `TenantRepository<T>`, transactional outbox).
- **DUR:**
  - Terminal geçiş ile outbox yazımı aynı transaction'a alınamıyorsa (standalone Mongo) → mevcut `SupportsTransactionsAsync` + telafi desenine uy ve raporla.
  - Olay adlandırma sözleşmesi `platform.workflow.instance.completed` biçimine izin vermiyorsa → sözleşmeye uyan en yakın adı kullan ve raporla.
  - Pozisyon repository'si tenant filtresi vermiyorsa → dur, raporla.

## Acceptance
- **E2:**
  - `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo` → **0 kırmızı** (yeni testler dahil).
  - `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests` → 0 kırmızı.
- **Yeni testler:**
  - DisplayContext kalıcı ve resolver'dan dönüyor.
  - Göreli olmayan link → 400.
  - Tasks resolver önceliği korunuyor.
  - Approve / reject / cancel / timeout → **tam 1** outbox olayı, doğru payload.
  - İptal edilmiş atama ve Frozen/Closed pozisyon aday olmuyor; Secondary/Acting aday oluyor.
  - Toplu okuma: 100 sınırı, kiracı izolasyonu, en yeni önce.
  - SoD (gönderen onaylayamaz) aynen çalışıyor.
- **Diff:** yalnız `services/Diten.Platform/**` (+ bu WP dosyasına §Tüketici rehberi). CRM, Auth, Web ve gateway diff'i YOK.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-3 · MOD-0023 Workflow'u servisler arası kullanıma aç (DisplayContext · tamamlanma olayı · aday düzeltmesi · toplu okuma) (Platform, backend)
Repository: C:\tmp\cl-be-3 (worktree) · Branch: wp/cl-be-3 (taban feature/crm-claims-v2) · commit bu dala, push YOK

Amaç: CRM (İddialar v2) onayı MOD-0023 ile başlatabilsin; WorkCenterNext'te anlamlı başlık/link, sonuç CRM'e olayla dönsün, onaylayıcı adayları doğru çözülsün. Yalnız Platform workflow + work-aggregation.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-3-workflow-cross-service.md (kanıt + NE + KORU) · …/SCMM-claims-approval-evidence-roadmap.md §A1/§E · services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/** (WorkflowModels.cs:146, WorkflowCandidateResolver.cs, WorkflowTaskTransitionSupport.cs:112/152, RunWorkflowEscalationsHandler.cs) · Features/WorkAggregation/** (IApprovalSourceResolver, WorkflowApprovalWorkItemProvider) · Features/Tasks/Providers/TaskApprovalSourceResolver.cs · Contracts/Eventing/ITransactionalOutboxEventWriter.cs · services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/** · Domain/Entities/Organization/{Position,PositionAssignment}.cs.

NE:
 1) StartWorkflowInstanceRequest'e opsiyonel DisplayContext {Title≤200, Subtitle≤300, SourceModule≤64, DeepLinkUrl≤500 yalnız "/" ile başlayan göreli yol, Chips≤5}; instance'a snapshot; SnapshotApprovalSourceResolver (öncelik: sahip resolver > snapshot > mevcut yedek; Tasks aynen).
 2) Terminal geçişte (Approved/Rejected/Cancelled/TimedOut) aynı transaction'da outbox olayı platform.workflow.instance.completed v1 {tenantId, workflowInstanceId, templateCode, templateVersionId, objectType, objectId, objectRef, outcome, completedAt, completedBy, finalStageCode, finalStepCode, reasonCode} — PII/yorum yok; instance başına tek olay; mevcut eventing sözleşmesine uy.
 3) WorkflowCandidateResolver: atamaları pozisyon id'leriyle tenant-scoped sorgula (GetAllAsync kaldır); IsCancelled/silinmiş hariç; pozisyonu Active olmayan hariç; Primary/Secondary/Acting/Delegated geçerli.
 4) GET api/v1/workflow/instances/by-objects?objectType=&objectIds= (≤100) → nesne başına instance listesi (en yeni önce; status, outcome, currentStage/Step, startedAt, completedAt); izin platform.workflow.instances.view; kiracı izolasyonu.
 5) WP dosyasına §Tüketici rehberi (servisler arası başlatma: user token → StartedBy → SoD; DisplayContext örneği; olay payload örneği; uzlaştırma çağrısı).
KORU/YAPMA: MOD-0023 uçları/sözleşmeleri geriye uyumlu; Tasks/WorkCenterNext dispatcher/SLA davranışı aynı (yalnız aday filtresi sıkılaşır); paralel onay YOK; SoD kuralı (yalnız gönderen onaylayamaz) değişmez; CRM/Auth/Web/ocelot DOKUNMA; yeni izin anahtarı YOK; yeni base entity/repo/eventing altyapısı YAZMA.
DOĞRULA (E2): cd C:\tmp\cl-be-3; dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo → 0 kırmızı; dotnet test services/Diten.Platform/tests/Diten.Platform.Eventing.Tests -c Release --nologo → 0 kırmızı; yeni testler: DisplayContext kalıcı+resolver, göreli olmayan link 400, Tasks önceliği, 4 terminal geçişte tam 1 olay, cancelled/frozen/closed aday değil + secondary/acting aday, toplu okuma sınır+izolasyon, SoD aynen; git diff yalnız services/Diten.Platform/** + WP dosyası. Commit ("feat(platform): WP-CL-BE-3 — workflow display context, completion event, candidate fix, batch read (MOD-0023)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: terminal geçiş + outbox aynı transaction'a alınamıyorsa mevcut SupportsTransactionsAsync+telafi desenine uy+raporla; olay adı sözleşmeye uymuyorsa en yakın uyumlu ad+raporla; pozisyon repo tenant filtresi vermiyorsa DUR.
```
