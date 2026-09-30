# WORK PACKAGE — WP-KP-2 · Bilgi Yolu revizyonu + MLR onayı (tek kanal, yorumlu karar, iğneli notlar, SoD kişi bazında)

> **CT (SoR).**
> - **Tasarım:** `DESIGN-KP-STUDIO-knowledge-path-studio.md` §2.3, §3.2, §3.3, §4 (D-KP-4/8).
> - **Mockup:** `mockups/kp-studio/` v2 (MLR sekmesi + inceleyici görünümü); analiz kalanı #6 (SoD kişi bazında).
> - **Bağımlılık:** KP-1 birleşti (`95c2444d`).
> - **Paralel:** KP-UI-1 (Web). Bu paket **yalnız CrmService**.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-2`, dal `wp/kp-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
>
> **Sonra (ayrı küçük paket, KP-2-CFG):** canlıda 6 şablon (`KP-MLR-{TR|BY|UZ|TM|GE|AZ}`) + grant script. CL-CFG-1 deseni; bu paket şablon **oluşturmaz**.

## Amaç
Bilgi Yolu, onaya gönderildiğinde **dondurulmuş revizyon** üretsin ve MOD-0023 sıralı MLR akışını başlatsın.
- **Karar tek kanaldan (K1):** yol içindeki inceleyici görünümünden ya da Görev Merkezi'nden verilen karar aynı iş akışına **yorumla** yazılır.
- **Notlar:** sayfa / blok üstüne iğnelenir, çözülür, sonraki revizyona taşınır.
- **Onaysız yol doğrudan yayınlanamaz.**

## Kanıt (CT) — kopyalanacak desen: İddialar v2 (BE-3 / BE-4 / BE-3a)
- **Başlatma:** `Features/ContentComposition/Claims/ClaimReviewHandlers.cs` + `ClaimReviewCore.cs` (`ClaimReviewRules`: ObjectRef, IdempotencyKey, DisplayContext, `ClaimSourceLink`).
  - İstemci: `Infrastructure/Workflow/GatewayClaimWorkflowClient.cs` (kullanıcı token'ı ile `StartAsync`; boş aday listesi = şablon adayları).
- **Sonuç:**
  - `Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs` (MassTransit + `ICrmEventInboxRepository` inbox) → `ClaimReviewOutcomeApplier` / `ApplyReviewOutcome`;
  - `ClaimQueryHandlers`'ta 120 sn sonra okumada uzlaşma;
  - `platform.workflow.instance.completed.v1` olayı aynı işlemde.
- **Geçmiş:** Platform `GET api/v1/workflow/instances/{id}/history` (yorumlarla). Tek örnek için `by-objects`.
- **Görev Merkezi eylemleri:** Platform `WorkflowApprovalWorkItemActionDispatcher` → `ApproveWorkflowTaskRequest(actorId, reasonCode, idempotencyKey, comment, evidenceRef)` / Reject. Aynı Platform uçları CRM'den kullanıcı token'ıyla çağrılacak.
- **Yol:**
  - `KnowledgePath` (KP-1: `ChainTemplate`, `CountryCode`, `LanguageCode`, `Steps[].Arrangement`, `Claims[]`, `IsLegacyUnapproved()`);
  - `KnowledgePathStudio*` (KP-1 kuralları, uyum özeti, iddia okuması);
  - yayın ucu `PublishKnowledgePathHandler`.
- **Yetki anahtarları:** `crm.knowledge.path.read | manage | publish` (Web `KnowledgePathsController`, manifest `KNOWLEDGE_PATHS`).

## NE
1. **`KnowledgePathRevision` (yeni aggregate)** — CRM class-map, string-Guid.
   - **Kimlik:** `PathId`, `PathVersion`, `RevisionNumber` (yol içinde artar), `CreatedAt / By` (= gönderen).
   - **Dondurulmuş anlık görüntü:**
     - zincir ref;
     - kimlik ve bağlam (ülke, dil, ürün, kitle);
     - adımlar: içerik id + sürüm + yerleşim + ayarlar;
     - iddialar: id, kod, sürüm, ülke sürümü id + sürüm, yerleşim;
     - uyum özeti;
     - `Pages` (boş; KP-UI-3 doldurur).
   - `ReviewRound {WorkflowInstanceId, TemplateCode, SubmittedAt, SubmittedBy, Outcome, ClosedAt, ReasonCode}`.
   - `Notes[] {NoteId, PageRef?, BlockRef?, X?, Y?, StepRef?, Text, Author, CreatedAt, ResolvedAt?, ResolvedBy?, CarriedFromRevision?}`.
   - `ChangeSummary` (önceki revizyonla fark; Öneri 6).
   - **KP-3 alanları için yer ayrılmış ama bu pakette yazılmaz:** `RenderedArtifacts`, `ReleaseState`.
2. **Onaya gönder** — `POST paths/{id}/submit-review` (`crm.knowledge.path.manage`).
   - **Ön koşullar** (hepsi 409 + kod):
     - `chain_template_required`: eski yol gönderilemez;
     - taslak durumda;
     - açık tur yok (`review_round_open`);
     - uyum özetinde `under` / `over` yok (`chain_conformance_failed`);
     - içerikler yayında (`component_not_published`);
     - tek dil (`component_language_mismatch`);
     - iddiaların yolun ülkesinde sürümü var (`claim_no_country_version`).
   - **İddianın onay durumu ENGEL DEĞİL.** MLR incelemesi sırasında iddia onaylanabilir; yayında (KP-3) kesin kapı var.
   - **Gönderim adımları:**
     - revizyon yazılır;
     - `KP-MLR-{CountryCode}` şablonuyla MOD-0023 örneği başlatılır (**kullanıcı token'ı**, idempotency `crm:knowledge-path:{pathId}:r{n}`);
     - DisplayContext: başlık `Bilgi yolu onayı · {PathCode} v{PathVersion} · Rev {n}`; açıklama yol adı; bağlantı `/CRM/KnowledgePaths/{pathId}/Review/{revisionId}` (inceleyici görünümü, KP-UI-2 ekranı; o gelene kadar mevcut `Details`); etiketler ülke / dil / sürüm.
   - **Yol durumu `review` olur, ama taslak düzenlenebilir kalır.** İçerik ve iddia eklemek serbest; revizyon dondurulmuştur.
   - **Değişiklik özeti:** son revizyonla fark (adım / iddia ekle / çıkar / taşı, içerik / iddia sürüm değişimi) `ChangeSummary`'ye yazılır.
   - Şablon yoksa 409 `approval_template_missing`. Platform erişilemezse 503 `workflow_unavailable` (İddialar'daki kodlar).
3. **Geri çek** — `POST paths/{id}/withdraw-review`: yalnız gönderen ya da manage; açık tur iptal (Platform cancel); yol durumu `draft`.
4. **Karar — tek kanal (K1)** — `POST paths/{id}/revisions/{revId}/decision` `{decision: approve | reject, comment}`.
   - **Yorum:** onayda isteğe bağlı; retde **zorunlu** (400 `comment_required`).
   - **Çağrı:** CRM, kullanıcının token'ıyla o örnekteki **kullanıcıya atanabilir açık görevi** bulur ve Platform'un approve / reject ucunu **yorumla** çağırır. Aday değilse Platform'un 403'ü aynen döner.
   - **SoD kişi bazında (D-KP-8, mockup kalanı #6):** kararı veren = revizyonu gönderen → 403 `sod_submitter_cannot_decide`. CRM ön kontrolü (kişi kimliği, rol değil) + Platform SoD.
   - **Görev Merkezi:** Oradan verilen karar da aynı görev üzerinden yazılır. Sonuç her iki yolda da tüketici / uzlaşmayla uygulanır.
5. **Sonuç uygulama:**
   - mevcut outcome tüketicisi **ObjectType'a göre yönlendirir**: `crm-claim` / `crm-claim-country-version` → iddia uygulayıcısı; **yeni** `crm-knowledge-path-revision` → yol uygulayıcısı;
   - aynı inbox kullanılır;
   - okumada 120 sn uzlaşma.
   - **Onay:** revizyon `approved`, yol durumu `approved` (yayın KP-3).
   - **Ret:** revizyon `rejected`, yol durumu `draft`.
   - Açık notlar sonraki revizyona `CarriedFromRevision` ile taşınır (Öneri 3).
6. **Notlar:**
   - `POST paths/{id}/revisions/{revId}/notes` `{pageRef?, blockRef?, stepRef?, x?, y?, text}`;
   - `…/notes/{noteId}/resolve`;
   - yazar ya da manage.
7. **Okuma:**
   - `GET paths/{id}/revisions` (liste);
   - `GET paths/{id}/revisions/{revId}` (anlık görüntü + tur + notlar + değişiklik özeti);
   - `GET …/review-history`: Platform geçmişi, **adım adı + yorum + kişi + tarih** (REQ-WCN-01 W-1 / W-2'nin CRM tarafı).
8. **Doğrudan yayın kapısı:**
   - mevcut `POST paths/{id}/publish` **zincirli yolda** 409 `approval_via_workflow_only` döner (yayın KP-3'te onaylı revizyondan);
   - **zincirsiz eski yolda bugünkü davranış kalır** (KP-3 / KP-4 kapatır).
   - `PathStatus` üzerinden `approved` / `published` yazan her Update yolu kapalı (mevcut V-P12 deseni).

## KORU / YAPMA
- Web / Platform / Auth kodu DOKUNMA (Platform uçları olduğu gibi tüketilir).
- **İddia onay akışı DEĞİŞMEZ.** Tüketici yalnız yönlendirme kazanır; iddia testleri yeşil kalır.
- **Çıktı / render / yayın / geri çekme YOK** (KP-3). İçerik Seti / SB-2 kaldırma YOK (KP-4).
- Şablon **oluşturma** YOK (KP-2-CFG). Testlerde sahte iş akışı istemcisi.
- Ham repository yazması yok. Yeni aggregate class-map'e. İşlem yoksa telafi (memory `crm-standalone-mongo-transaction-fallback`).
- **DUR:**
  - Platform'da "belirli örnekte kullanıcıya atanabilir açık görevi bul" için kullanıcı token'ıyla çağrılabilir bir uç yoksa → **uydurma**; mevcut uçları raporla.
  - Platform approve / reject ucu yorum kabul etmiyorsa → raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban 2143/0/5; bilinen sıra flake'i hariç). Build 0 hata.
  - **Yeni testler:**
    - gönder ön koşulları (her kod);
    - revizyon anlık görüntüsü + numara artışı;
    - açık turda ikinci gönderim 409;
    - başlatma isteği (şablon kodu, idempotency, DisplayContext, kullanıcı token'ı);
    - geri çek;
    - karar: onay / ret, ret yorumu zorunlu, SoD kişi bazında 403, görev bulunamadı / aday değil;
    - sonuç yönlendirmesi (iddia ↔ yol);
    - uygulama (approved / rejected, not taşıma);
    - uzlaşma;
    - notlar;
    - değişiklik özeti;
    - doğrudan yayın 409 (zincirli) + eski yol davranışı korunur;
    - class-map round-trip;
    - iddia onay testleri yeşil.
  - **Sabotaj:** SoD kişi kontrolü, ret yorumu zorunluluğu ve sonuç yönlendirmesi testleri kırmızıya dönmeli.
- **E4:** KP-2-CFG (şablonlar + grant) ve KP-UI-2 (inceleyici ekranı) sonrası CT. Arada API ile kontrol edilebilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-2 · Bilgi Yolu revizyonu + MLR onayı (tek kanal, yorumlu karar, iğneli notlar, SoD kişi bazında)
Repository: C:\tmp\kp-2 (worktree) · Branch: wp/kp-2 · commit bu dala, push YOK · PARALEL: KP-UI-1 (Web) — sen yalnız CrmService

Amaç: DESIGN-KP-STUDIO §2.3/§3.2/§3.3: onaya gönderince dondurulmuş KnowledgePathRevision + MOD-0023 KP-MLR-{ülke} akışı (Medikal→Hukuk→Ruhsat); karar tek kanal (CRM karar ucu da Görev Merkezi de aynı Platform görevine yorumla yazar); iğneli notlar; SoD kişi bazında; zincirli yol doğrudan yayınlanamaz. Şablon oluşturma YOK (KP-2-CFG ayrı).

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-2-path-revision-mlr.md · …/DESIGN-KP-STUDIO-knowledge-path-studio.md · …/WP-CL-BE-3-workflow-cross-service.md + WP-CL-BE-4-claims-workflow-approval.md + WP-CL-BE-3a-* (desen) · …/REQ-WCN-01-claims-approval-workcenter-ux.md · services/Diten.CrmService/src/**/ContentComposition/Claims/{ClaimReviewHandlers, ClaimReviewCore, ClaimQueryHandlers}.cs · Infrastructure/Workflow/GatewayClaimWorkflowClient.cs · Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs · Domain/Repositories/ICrmEventInboxRepository.cs · Features/Knowledge/Path/** (KP-1: KnowledgePathStudio*, publish handler) · Domain/Entities/KnowledgePath.cs · Persistence/DependencyInjection.cs · services/Diten.Platform/src/**/WorkAggregation/Providers/WorkflowApprovalWorkItemActionDispatcher.cs + Workflow API controller'ları (görev bul / approve / reject / cancel / history uçları) · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements, crm-standalone-mongo-transaction-fallback.

NE:
 1) KnowledgePathRevision (yeni aggregate, class-map string-Guid): PathId, PathVersion, RevisionNumber, CreatedBy(=gönderen); dondurulmuş anlık görüntü (zincir ref, kimlik+bağlam, adımlar içerik id+sürüm+yerleşim+ayar, iddialar id/kod/sürüm/ülke sürümü id+sürüm/yerleşim, uyum özeti, Pages boş); ReviewRound{WorkflowInstanceId, TemplateCode, SubmittedAt/By, Outcome, ClosedAt, ReasonCode}; Notes[]{NoteId, PageRef?, BlockRef?, StepRef?, X?, Y?, Text, Author, CreatedAt, ResolvedAt/By?, CarriedFromRevision?}; ChangeSummary; RenderedArtifacts/ReleaseState yer ayrılmış (yazılmaz).
 2) POST paths/{id}/submit-review (manage): ön koşullar 409 — chain_template_required, taslak, review_round_open, chain_conformance_failed (under/over), component_not_published, component_language_mismatch, claim_no_country_version (iddia onay durumu ENGEL DEĞİL). Revizyon yaz → MOD-0023 KP-MLR-{CountryCode}, kullanıcı token'ı, idempotency crm:knowledge-path:{pathId}:r{n}, DisplayContext (başlık "Bilgi yolu onayı · {PathCode} v{PathVersion} · Rev {n}", bağlantı /CRM/KnowledgePaths/{pathId}/Review/{revisionId}, etiketler ülke/dil/sürüm); yol durumu review ama düzenleme serbest; ChangeSummary son revizyona göre; approval_template_missing 409 / workflow_unavailable 503.
 3) POST paths/{id}/withdraw-review (gönderen ya da manage): Platform cancel, yol draft.
 4) POST paths/{id}/revisions/{revId}/decision {approve|reject, comment}: ret yorumu zorunlu (400 comment_required); CRM kullanıcı token'ıyla örnekteki kullanıcıya atanabilir açık görevi bulur ve Platform approve/reject'i YORUMLA çağırır (aday değilse Platform 403 aynen); SoD kişi bazında: karar veren = gönderen → 403 sod_submitter_cannot_decide (rol değil kişi).
 5) Sonuç: mevcut outcome tüketicisi ObjectType'a göre yönlendirir (claim/claim-country-version → iddia; yeni crm-knowledge-path-revision → yol uygulayıcısı), aynı inbox; okumada 120 sn uzlaşma; onay → revizyon approved + yol approved; ret → rejected + yol draft; açık notlar sonraki revizyona CarriedFromRevision ile taşınır.
 6) Notlar: POST …/revisions/{revId}/notes, …/notes/{noteId}/resolve (yazar ya da manage).
 7) Okuma: GET paths/{id}/revisions, …/revisions/{revId} (anlık görüntü + tur + notlar + özet), …/review-history (Platform geçmişi: adım adı + yorum + kişi + tarih).
 8) Doğrudan yayın: POST paths/{id}/publish zincirli yolda 409 approval_via_workflow_only; zincirsiz eski yolda bugünkü davranış; Update ile approved/published yazma kapalı.
KORU/YAPMA: Web/Platform/Auth kodu DOKUNMA (Platform uçları olduğu gibi); iddia onay akışı DEĞİŞMEZ (yalnız tüketici yönlendirmesi; iddia testleri yeşil); çıktı/render/yayın/geri çekme YOK (KP-3); set/SB-2 kaldırma YOK (KP-4); şablon oluşturma YOK; testlerde sahte iş akışı istemcisi; ham repository yazması yok; işlem yoksa telafi.
DOĞRULA (E2): cd C:\tmp\kp-2; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2143/0/5; bilinen sıra flake'i hariç); build 0 hata. Yeni testler: gönder ön koşulları (her kod), anlık görüntü + numara, açık turda 409, başlatma isteği (şablon/idempotency/DisplayContext/token), geri çek, karar onay/ret + ret yorumu + SoD kişi 403 + görev yok/aday değil, sonuç yönlendirmesi, uygulama + not taşıma, uzlaşma, notlar, değişiklik özeti, doğrudan yayın 409 + eski yol korunur, class-map round-trip, iddia onay testleri yeşil. Sabotaj: SoD kişi + ret yorumu + yönlendirme testleri kırmızıya dönmeli. Commit ("feat(crm): WP-KP-2 — knowledge path revision + MLR approval (single channel, comments, notes, person SoD)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: Platform'da kullanıcı token'ıyla "örnekte bana atanabilir açık görev" bulunacak uç yoksa ya da approve/reject yorum kabul etmiyorsa DUR + mevcut uçları raporla (uydurma).
```
