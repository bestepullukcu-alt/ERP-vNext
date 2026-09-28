# WORK PACKAGE — WP-CL-BE-3a · Workflow: şablon adaylarıyla başlatma (boş aday listesi) + instance geçmişi (yorumlarla) (Platform, backend, küçük)

> **CT (SoR).** İddialar v2 Faz 2 düzeltmesi. **Kaynak:** WP-CL-BE-4 raporu ve CT teyidi.
>
> **Sorun 1:** CRM, onaylayıcıları **şablondan** aldığı için `CandidatePrincipalIds` boş gönderiyor. Ancak Platform doğrulayıcısı boş listeyi reddediyor. Sonuç: **canlıda hiçbir iddia onaya gönderilemez.**
>
> **Sorun 2:** İddianın "Onay" sekmesi (mockup senaryo 7: turlar, kim, ne zaman, **yorum/ret gerekçesi**) için Platform yorumları dışarı vermiyor. Yorumlar `WorkflowTransitionLog.Comment` alanında saklanıyor ama hiçbir uç bu alanı dönmüyor.
>
> **Kapsam:** Yalnız Platform workflow. CRM, Auth, Web DEĞİŞMEZ.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-3a`, dal `wp/cl-be-3a`. Commit bu dala, push YOK.

## Kanıt (CT)
- **Doğrulayıcı:** `Platform.Application/Features/Workflow/Validators/StartWorkflowInstanceValidator.cs:26-28` → `RuleFor(CandidatePrincipalIds).NotEmpty()`.
- **Handler şablon adaylarını zaten önceliklendiriyor:**
  - `StartWorkflowInstanceHandler.cs:114-123` → `firstStep.CandidatePrincipalIds.Count > 0 ? firstStep… : request…`.
  - Çözümlenen aday kalmazsa handler zaten **400 `WorkflowAssignmentCandidatesRequired`** dönüyor (satır 129-134).
  - Yani doğrulayıcıdaki kural gereksiz ve şablon adaylı kullanımı engelliyor.
- **Yorum:** `Domain/Entities/Workflow/WorkflowTransitionLog.cs:23` `Comment` var.
- **DTO eksik:** `WorkflowModels.cs` → `WorkflowTaskDto(… ActionedBy, ActionReasonCode)` **yorum içermiyor**. Instance detayı geçiş kaydını dönmüyor.

## NE
1. **Boş aday listesi:**
   - `StartWorkflowInstanceValidator`'dan `CandidatePrincipalIds.NotEmpty()` kuralı kaldırılır. Öğe başı kurallar (`NotEmpty` + `MaximumLength(256)`) kalır.
   - "Şablon adaysız + istek adaysız" durumu handler'daki mevcut **400 `WorkflowAssignmentCandidatesRequired`** ile reddedilmeye devam eder.
   - Davranış değişmez: istekte aday varsa ve şablonda yoksa istek adayları kullanılır.
2. **Instance geçmişi:**
   - `GET api/v1/workflow/instances/{id}/history` (izin `platform.workflow.instances.view`, kiracı izolasyonu, başka tenant → 404).
   - Dönen liste `WorkflowTransitionLog` kayıtlarıdır, `SequenceNo` sırasıyla.
   - Her kayıt: `{sequenceNo, action (start / approve / reject / delegate / request-info / cancel / escalate / timeout…), actorId, actorDisplay?, fromStageCode, fromStepCode, toStageCode, toStepCode, stepName?, comment, reasonCode, occurredAt}`.
   - `stepName` şablon sürümünden çözülür (varsa).
   - `actorDisplay` yalnız zaten kullanılan bir kullanıcı-ad çözümleyici varsa doldurulur; yoksa boş kalır, yeni bağımlılık eklenmez.
   - Yorum metni **yalnız bu uçtan** döner. **Olaylara ve loglara girmez** (BE-3 kuralı).
3. **WP-CL-BE-3 §Tüketici rehberine** "geçmiş ucu" ve "boş aday = şablon adayları" notu eklenir.

## KORU / YAPMA
- **Diğer doğrulama kuralları, SoD, aday çözümü (BE-3 düzeltmesi), tamamlanma olayı, DisplayContext DEĞİŞMEZ.**
- Mevcut DTO'lar yalnız **ekleme** yoluyla genişler (sonda, opsiyonel alan). Tasks ve WorkCenterNext davranışı aynı kalır.
- CRM, Auth, Web, gateway DOKUNMA. Mevcut `/api/v1/workflow/{everything}` route'u yeni ucu kapsar.
- Yeni izin anahtarı YOK.
- **DUR:** Tasks (MOD-0024) boş aday listesine güvenen bir yolla ("boşsa varsayılan şablon") başlatıyorsa ve kaldırılan kural o yolu etkiliyorsa → dur ve raporla.

## Acceptance
- **E2:**
  - `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo` → **yeni kırmızı yok**. Taban kırmızı küme = 173 ortam testi (TRX karşılaştırması).
  - Eventing testleri 65/0/3.
  - Platform API build 0 hata.
- **Yeni testler:**
  - Şablonda pozisyon adayı var + istek boş → başlatılıyor.
  - Şablon adaysız + istek boş → 400 `WorkflowAssignmentCandidatesRequired`.
  - İstekte aday var + şablonda yok → istek adayları kullanılıyor (eski davranış).
  - Geçmiş: başlat → onay (yorumlu) → ret (gerekçeli) sırası; yorumlar dönüyor; başka tenant → 404; izin yok → 403.
  - **Sabotaj kanıtı** en az 2 kural için.
- **Diff:** yalnız `services/Diten.Platform/**` + WP-CL-BE-3 rehber notu.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-3a · Workflow: şablon adaylarıyla başlatma (boş aday listesi) + instance geçmişi (yorumlarla) (Platform, backend, küçük)
Repository: C:\tmp\cl-be-3a (worktree) · Branch: wp/cl-be-3a · commit bu dala, push YOK

Amaç: (1) CRM onaylayıcıları şablondan aldığı için CandidatePrincipalIds boş gönderiyor ama StartWorkflowInstanceValidator boş listeyi reddediyor → canlıda iddia onaya gönderilemiyor. Handler şablon adaylarını zaten önceliklendiriyor ve aday çıkmazsa zaten 400 WorkflowAssignmentCandidatesRequired dönüyor → doğrulayıcıdaki NotEmpty kaldırılacak. (2) İddia "Onay" sekmesi için yorum/ret gerekçesi dahil instance geçmişi ucu.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-3a-workflow-template-candidates-history.md · …/WP-CL-BE-3-workflow-cross-service.md (§Tüketici rehberi) · services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/{Validators/StartWorkflowInstanceValidator.cs:26-28, Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs:114-134, WorkflowModels.cs (WorkflowTaskDto/WorkflowInstanceDto)} · Domain/Entities/Workflow/WorkflowTransitionLog.cs · Tasks'ın workflow başlatma yolu (TaskApprovalService).

NE:
 1) StartWorkflowInstanceValidator'dan CandidatePrincipalIds.NotEmpty() kaldır (öğe başı NotEmpty+MaximumLength(256) kalsın); şablon adaysız + istek adaysız → handler'daki mevcut 400 WorkflowAssignmentCandidatesRequired.
 2) GET api/v1/workflow/instances/{id}/history (platform.workflow.instances.view, kiracı izolasyonu, başka tenant 404) → WorkflowTransitionLog SequenceNo sırasıyla {sequenceNo, action, actorId, actorDisplay?, fromStageCode, fromStepCode, toStageCode, toStepCode, stepName?, comment, reasonCode, occurredAt}; stepName şablon sürümünden; actorDisplay yalnız mevcut çözümleyici varsa; yorum yalnız bu uçta (olay/log'a girmez).
 3) WP-CL-BE-3 §Tüketici rehberine geçmiş ucu + "boş aday = şablon adayları" notu.
KORU/YAPMA: diğer doğrulama, SoD, aday çözümü, tamamlanma olayı, DisplayContext DEĞİŞMEZ; DTO'lar yalnız ekleme; Tasks/WorkCenterNext aynı; CRM/Auth/Web/gateway DOKUNMA; yeni izin anahtarı YOK.
DOĞRULA (E2): cd C:\tmp\cl-be-3a; dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo → yeni kırmızı yok (taban 173 ortam kırmızısı, TRX ile karşılaştır); dotnet test services/Diten.Platform/tests/Diten.Platform.Eventing.Tests -c Release --nologo → 65/0/3; Platform API build 0 hata; yeni testler: şablon adaylı+istek boş başlıyor, ikisi de boş 400, istek adaylı eski davranış, geçmiş sırası+yorumlar+404+403; ≥2 sabotaj kanıtı; git diff yalnız services/Diten.Platform/** + BE-3 rehber notu. Commit ("fix(platform): WP-CL-BE-3a — start with template candidates (empty request list) + instance history with comments (MOD-0023)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: Tasks boş aday listesine güvenen bir yolla başlatıyorsa ve kaldırılan kural onu etkiliyorsa DUR+raporla.
```
