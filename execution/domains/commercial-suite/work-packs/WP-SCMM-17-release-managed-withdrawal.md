# WORK PACKAGE — WP-SCMM-17 · Release yetkisi + managed withdrawal (rendered ContentSetRevision) (backend, CrmService)

> **CT (SoR).** CAND-CAP-0011 / **SCMM-17** (work-plan §4 satır 99: *"Release yetki + managed withdrawal"*, dep 15,16, Gate **G3**; AT05 *output+release — manifest-bound*, AT07 *restriction+withdrawal*). Ön-koşul **SCMM-16B KOMPLE** (`0dd1653a`: render → PDF → FU01 → `RenderedArtifact` bind). Bu WP = **rendered revizyonu yayınla (release) + yönetilen geri çekme (withdrawal)** — CrmService iç yaşam döngüsü. **CrmService (Domain+Application+Api).** Platform/FU01 **DEĞİŞMEZ**. Render (16B) + snapshot + review **DEĞİŞMEZ** (yalnız additive release state). **Kalıcı silme YOK** (AD-6: withdrawal bir durumdur, artifact byte'ları silinmez). **SCMM'i kapatır.**

## Kanıt
- **SCMM-17 kapsamı** (`SCMM-content-studio-work-plan.md:99`): release authorization + managed withdrawal; dep 15,16; Gate G3. AT05 **manifest-bound** (release belirli artifact'e bağlı), AT07 **restriction+withdrawal**.
- **ContentSetRevision** (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ContentSetRevision.cs`): `SubmittedBy`(author) · `Decision`(ContentSetReviewDecision, `ReviewerId`=onaylayan ActorName) · `ReviewStatus` (submitted/in-review/**approved**/rejected) · **16B'den** `RenderedArtifact`(ContentSetRenderedArtifact? {ContentId,Checksum,MediaType,ByteSize,FileName,RenderedAtUtc,RenderedBy}) + `IsRendered()`. **Release/withdrawal alanı YOK** → 17 additive ekler.
- **SoD deseni mevcut** (SCMM-15, aynı dosya sat.206-209): reviewer `ActorName` ≠ `SubmittedBy` → değilse **403** (`ActorMatchesSubmitter`). `IActorContext` yalnız `ActorName` taşır (kimlik = ActorName). Duplicate-identical→mevcut döndür; conflicting→409; stale→409 desenleri mevcut.
- **Reason code deseni** (`ContentSetRevisionReasonCodes`): Submitted/Approved/Rejected/**Rendered**/NotApproved mevcut → Released/Withdrawn ekle.
- **Audit:** `IContentCompositionAuditPublisher` (16B'de `content_set_revision_rendered` fail-soft) mevcut → release/withdraw event'leri ekle.
- **Frontend:** revizyon/review/render/release konsolu **YOK** (SCMM-15/16/17 backend-only) → işletim UI'ı ayrı WP; E4 = authenticated smoke.

## NE (CrmService; Platform/FU01/render/snapshot/review DEĞİŞMEZ)
1. **Domain (additive):** `ContentSetRevision`'a nullable `ContentSetReleaseState` VO: `{ string ReleaseStatus (released|withdrawn), Guid ReleasedArtifactContentId, string ReleasedArtifactChecksum, DateTimeOffset ReleasedAtUtc, string ReleasedBy, DateTimeOffset? WithdrawnAtUtc, string? WithdrawnBy, string? WithdrawalReason }` + `IsReleased()`/`IsWithdrawn()` helper. **manifest-bound:** release anında `RenderedArtifact.ContentId+Checksum` **release state'e pin'lenir** (yayınlanan çıktı değişmez şekilde tanımlı). Snapshot/review/render alanları **mutasyonsuz**. Persistence class-map (yeni VO; ReleasedArtifactContentId GUID subtype — [[crm-new-aggregate-classmap-guid]] tuzağı).
2. **Application — `ReleaseContentSetRevisionCommand(revisionId)` + handler:**
   - Tenant-scoped yükle (404).
   - **Idempotent:** zaten `released` ise → mevcut release DTO (200). Zaten `withdrawn` ise → **409** (geri çekilen yeniden yayınlanamaz; yeni revizyon gerekir).
   - **Precondition:** `RenderedArtifact == null` → **409** (`content_set_revision_not_rendered`; yayınlamadan önce render şart). (approved zaten render'ın ön-koşulu.)
   - **SoD:** releaser `ActorName` == `Decision.ReviewerId` → **403** (`content_set_revision_release_sod`; onaylayan kendi çıktısını yayınlayamaz — author→reviewer→releaser üç ayrı görev). ReviewerId null ise (beklenmez, approved⇒decision var) → 409 controlled.
   - `ReleaseState` set (status=released, artifact pin, ReleasedAt/By=ActorName), optimistic persist, audit `content_set_revision_released` (fail-soft).
3. **Application — `WithdrawContentSetRevisionCommand(revisionId, reason)` + handler:**
   - Tenant-scoped yükle (404). **Precondition:** `IsReleased()` değilse → **409** (yalnız yayınlanan geri çekilir). **Reason zorunlu** (boş→400).
   - **Idempotent:** zaten `withdrawn` ise → mevcut (200).
   - status=withdrawn + WithdrawnAt/By/Reason set; **byte SİLİNMEZ** (managed — AD-6); optimistic persist; audit `content_set_revision_withdrawn` (fail-soft). Withdrawn = terminal (yeniden release yok).
4. **Application query:** release state revizyon DTO'suna additive alan (mevcut `GetById`/list DTO'ya `ReleaseState` ekle; render/review alanları korunur).
5. **Api — `ContentSetRevisionsController`:** `POST {id}/release` (RBAC **yeni `crm.content-set.release`**) → ReleaseStateDto; `POST {id}/withdraw` (RBAC **yeni `crm.content-set.withdraw`**, body `{reason}`) → ReleaseStateDto. Her ikisi izin + seed notu (SCMM-16A grant deseni).

## KORU / YAPMA
- **Platform/FU01 DEĞİŞMEZ** (tüketilmez bile — release/withdraw saf CrmService durum geçişi; artifact zaten 16B'de depolandı). **ContentSetRevision snapshot + review (SCMM-15) + RenderedArtifact/render (SCMM-16B) DEĞİŞMEZ** — yalnız additive `ContentSetReleaseState`. `ContentSetRevisionTests` + `ContentSetRevisionRenderTests` mevcut davranış korunur. **Frontend YOK** (ayrı UI WP). **Kalıcı silme/purge YOK** (withdrawal = durum; FU01 compensate/purge ÇAĞIRMA). TenantId JWT'den; SoD ActorName üstünden. Diğer servisler DOKUNMA.
- **DUR:** (a) approved⇒Decision.ReviewerId dolu değilse (SoD imkansız) → controlled 409, DUR gerekmez ama beklenmeyen model ise raporla; (b) managed withdrawal'ın "byte silme yok" ilkesi bir retention/DCP gereğiyle çelişiyorsa → DUR+raporla (purge = MOD-0262-FU05, bu WP'de YOK); (c) release-state persist optimistic concurrency ile çakışıyorsa → DUR+raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/... -c Release` → yeşil (baseline 1853/0 + yeni release/withdraw testleri); CrmService.Api derleme 0 hata. git diff: CrmService Domain (ReleaseState + class-map) + Application (2 command/handler/dtos/permissions/query) + Api (2 endpoint). **Platform/FU01/snapshot/review/render davranış diff YOK.** Testler: release not-rendered→409 · release SoD (releaser=reviewer)→403 · release başarı→state pinned (contentId+checksum) · release idempotent→mevcut · withdrawn→re-release 409 · withdraw not-released→409 · withdraw reason boş→400 · withdraw başarı→terminal · withdraw idempotent · **byte silinmez** (FU01 compensate çağrılmaz — seam çağrı sayısı 0).
- **E4 (authenticated smoke, 16A/16B deseni):** rendered revizyon → `POST release` (farklı actor, reviewer değil) → released+pinned · reviewer ile release → 403 · not-rendered revizyon release → 409 · `POST withdraw {reason}` → withdrawn · withdraw sonrası release → 409 · withdraw reason'sız → 400 · başka tenant → 404.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-17 · Release yetkisi + managed withdrawal (rendered ContentSetRevision) (Diten.CrmService, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Worktree: ana checkout

Amaç: SCMM-17 — SCMM-16B'de render edilip FU01'e depolanan (RenderedArtifact bound) revizyonu YAYINLA (release, manifest-bound: contentId+checksum pin) + YÖNETİLEN GERİ ÇEKME (managed withdrawal, byte SİLİNMEZ — AD-6). Saf CrmService durum geçişi. Platform/FU01/render/snapshot/review DEĞİŞMEZ. SCMM'i kapatır.

Önce oku: execution/domains/commercial-suite/work-packs/WP-SCMM-17-release-managed-withdrawal.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ContentSetRevision.cs (SubmittedBy/Decision.ReviewerId/ReviewStatus/RenderedArtifact/IsRendered) · .../Application/Features/ContentComposition/ContentSetRevisions/ContentSetRevisionCommandHandlers.cs (SCMM-15 SoD deseni sat.206 ActorMatchesSubmitter; 16B render handler idempotent/409 deseni) · ContentSetRevisionPermissions.cs · ContentSetRevisionQueryHandlers.cs + Dtos + Persistence class-map + IContentCompositionAuditPublisher · memory crm-new-aggregate-classmap-guid.

NE (CrmService; Platform/FU01/render/snapshot/review DEĞİŞMEZ):
 1) Domain: ContentSetRevision'a nullable ContentSetReleaseState VO {ReleaseStatus(released|withdrawn), ReleasedArtifactContentId(Guid), ReleasedArtifactChecksum, ReleasedAtUtc, ReleasedBy, WithdrawnAtUtc?, WithdrawnBy?, WithdrawalReason?} + IsReleased()/IsWithdrawn(); additive; snapshot/review/render mutasyon YOK; class-map GUID subtype. Release anında RenderedArtifact.ContentId+Checksum state'e PIN'lenir (manifest-bound).
 2) Application ReleaseContentSetRevisionCommand+handler: 404; zaten released→mevcut(200); zaten withdrawn→409; RenderedArtifact yok→409 (content_set_revision_not_rendered); SoD releaser ActorName==Decision.ReviewerId→403 (content_set_revision_release_sod); yoksa release state set+pin+optimistic persist+audit content_set_revision_released (fail-soft).
 3) Application WithdrawContentSetRevisionCommand+handler: 404; IsReleased değil→409; reason boş→400; zaten withdrawn→mevcut(200); yoksa withdrawn set (WithdrawnAt/By/Reason)+optimistic persist+audit content_set_revision_withdrawn (fail-soft); BYTE SİLME YOK (FU01 compensate/purge ÇAĞIRMA); terminal (re-release yok).
 4) Query: release state revizyon DTO'suna additive (render/review korunur).
 5) Api: POST {id}/release (RBAC yeni crm.content-set.release) → ReleaseStateDto; POST {id}/withdraw (RBAC yeni crm.content-set.withdraw, body {reason}) → ReleaseStateDto. İzinler tanımla+seed notu.
KORU/YAPMA: Platform/FU01 DEĞİŞMEZ (çağrılmaz bile); snapshot+review(SCMM-15)+render(SCMM-16B) DEĞİŞMEZ (yalnız additive ReleaseState); ContentSetRevisionTests+ContentSetRevisionRenderTests korunur; frontend YOK; kalıcı silme/purge YOK (withdrawal=durum); TenantId JWT'den; SoD ActorName; diğer servisler DOKUNMA.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo → yeşil (baseline 1853+yeni); CrmService.Api derleme 0 hata; git diff yalnız CrmService (Domain/Application/Api/Persistence); Platform/FU01/snapshot/review/render diff yok. Testler: release not-rendered→409, release SoD→403, release başarı→pinned, release idempotent, withdrawn→re-release 409, withdraw not-released→409, withdraw reason-boş→400, withdraw başarı+idempotent, byte silinmez (FU01 compensate çağrı=0). Ayrı commit ("feat(scmm): WP-SCMM-17 — release authorization + managed withdrawal for rendered ContentSetRevision (CAND-CAP-0011 SCMM-17)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: approved⇒Decision.ReviewerId dolu değilse (beklenmeyen model); managed-withdrawal "byte silme yok" bir retention/DCP gereğiyle çelişiyorsa (purge=MOD-0262-FU05, bu WP'de YOK); release-state optimistic concurrency çakışıyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-23) → **ACCEPTED (E2)**
```
Commit: fdbf2942 · Agent: PASS (1864/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-scmm17 @fdbf2942 → 1864/0/5 (birebir)
```
- ✅ **Kapsam (10 dosya, +587/−4):** yalnız CrmService (Api/Application/Domain/Persistence + test). **Platform/FU01/render/store/frontend diff = 0** (KORU temiz — release/withdraw saf durum geçişi, FU01 çağrılmıyor bile). ContentSetRevision.cs + CommandHandlers.cs **saf additive** (SCMM-15/16B kodu değişmedi); 4 silme yalnız Dtos/Mapper/Permissions additive edit.
- ✅ **Release handler (kod okundu):** idempotent (IsReleased→200) · withdrawn→**409** (terminal, re-release yok) · not-rendered (RenderedArtifact null)→**409** · **SoD:** reviewerId null→controlled 409 (bypass değil), releaser ActorName==Decision.ReviewerId→**403** · **manifest-bound pin** (ReleasedArtifactContentId+Checksum = RenderedArtifact'ten).
- ✅ **Withdraw handler:** idempotent (IsWithdrawn→200, retry reason'sız güvenli) · not-released→**409** · blank-reason→**400** · Withdrawn+WithdrawnAt/By/Reason set · **byte SİLİNMEZ**.
- ✅ **AD-6 yapısal kanıt:** `ReleaseContentSetRevisionHandler` + `WithdrawContentSetRevisionHandler` ctor'larında `IContentArtifactStore`/renderer **YOK** → silme/compensate/purge yolu handler'lardan **erişilemez** (test: FU01 compensate çağrı=0 + yapısal no-delete-path).
- ✅ **RBAC:** yeni `crm.content-set.release` + `crm.content-set.withdraw` (Permissions.New'e eklendi).
- ✅ **Build+test (CT izole):** Application.Tests **1864/0/5** (baseline 1853 + 11 yeni: release not-rendered 409 · SoD 403 · success-pin · idempotent · no-reviewer 409 · cross-tenant 404 · withdraw not-released 409 · blank-reason 400 · terminal re-release 409 · idempotent · AD-6 no-delete-path). Api derleme 0 hata (2 uyarı pre-existing Territory).

**WP-SCMM-17 KOMPLE (E2). SCMM backend zinciri TAMAM: 15 freeze → 16A scope → 16B render → 17 release/withdraw.** E4 (canlı) + `crm.content-set.release|withdraw` seed/grant + işletim UI (ayrı frontend WP) kullanıcıda.

