# WORK PACKAGE — WP-CL-BE-4 · İddia ↔ MOD-0023 onay akışı (onaya gönder / geri çek · sonuç olayı · turlar · okumada uzlaştırma · doğrudan onayın kaldırılması) (CRM, backend)

> **CT (SoR).** İddialar v2 Faz 2. Kaynaklar:
> - `SCMM-claims-v2-mockup-analysis-plan.md` (CL-BE-4, K10/K11, D2 = sıralı)
> - `SCMM-claims-approval-evidence-roadmap.md` §A3/§E
> - Üzerine kurulduğu paketler: **WP-CL-BE-1** (model) + **WP-CL-BE-3** (workflow servisler arası; §Tüketici rehberi)
>
> **Amaç:** Çekirdek/yerel iddia ve ülke sürümü **yalnız MOD-0023 workflow sonucuyla** onaylansın.
> - CRM akışı kullanıcının kendi token'ıyla başlatır (SoD çalışır).
> - Onay WorkCenterNext'te anlamlı başlıkla görünür.
> - Sonuç `platform.workflow.instance.completed.v1` olayıyla CRM'e döner.
> - Kaçan olay, okumada uzlaştırma ile yakalanır.
> - Tur geçmişi tutulur.
> - **Geçici doğrudan onay uçları kaldırılır.**
>
> **Kapsam:** Yalnız CRM. Platform, Auth ve Web DEĞİŞMEZ. Kanıt kuralı (≥1 kanıt) **bu WP'de YOK** (BE-5).
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-4`, dal `wp/cl-be-4` (taban `test/crm-content-visit-e2e` @ `1e2f05c7`). Commit bu dala, push YOK. BE-6 paralel çalışır; ortak dosyalarda küçük bölgeler değiştir.

## Kanıt (CT kod okuması)
- **BE-1 modeli:**
  - `Domain/Entities/ClaimCountryVersion.cs` → `ReviewRounds: List<ClaimReviewRound{WorkflowInstanceId, SubmittedAt, SubmittedBy, Outcome, ClosedAt}>` (şu an boş).
  - `Claim.cs` → `Kind`, `Status` (draft / in-review / approved / review-required / inactive / archived). **Claim'de ReviewRounds YOK** → eklenecek.
  - Geçici onay: `ClaimsController.cs:156` + `ClaimCommands.cs:114` + `ClaimV2CommandHandlers.cs:727` (`TEMPORARY: replaced by WP-CL-BE-4`).
  - Çekirdek onay yayılımı şimdilik doğrudan approve'da. Eski çekirdek → inactive, ülke sürümleri → review-required.
- **BE-3 Platform sözleşmesi** (`WP-CL-BE-3-workflow-cross-service.md` §Tüketici rehberi):
  - `POST /api/v1/workflow/instances` + opsiyonel `DisplayContext {Title, Subtitle, SourceModule, DeepLinkUrl (göreli), Chips≤5}`.
  - Olay `platform.workflow.instance.completed.v1` `{tenantId, workflowInstanceId, templateCode, templateVersionId, objectType, objectId, objectRef, outcome, completedAt, completedBy, finalStageCode, finalStepCode, reasonCode}`.
  - `GET /api/v1/workflow/instances/by-objects?objectType=&objectIds=` (≤100).
  - Görev iptali `POST /api/v1/workflow/tasks/{taskId}/cancel`. Instance detayı `GET /api/v1/workflow/instances/{id}`.
  - **SoD:** yalnız gönderen onaylayamaz, gönderen = token sahibi.
- **CRM'den Gateway çağrısı deseni:** `Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs:393` `ForwardContextHeaders` **Authorization + X-Tenant-Id iletiyor**. Aynı desen kullanılacak.
- **CRM'de olay tüketici ve MassTransit altyapısı YOK.** Emsal: AuthService.
  - `Infrastructure/DependencyInjection.cs:115-150` → `AddMassTransit` + `UsingRabbitMq` + `UseMessageRetry`, ayar `AuthServiceEventingOptions` (`UseRabbitMq`).
  - `Eventing/EntitlementSyncConsumer.cs` → `IConsumer<EventTransportMessage>`, `EventName` switch, **inbox ile idempotency** (`TryInsertAsync(EventId…)`).
  - Transport dev'de RabbitMQ.
- **CRM'de BackgroundService/IHostedService yok.** Periyodik süpürge yerine **okumada uzlaştırma** (kullanıcı token'ıyla).

## Kararlar
- **Akış şablonları konfigürasyondan gelir:**
  - Çekirdek: `Crm:Claims:Workflow:CoreTemplateCode` = `CLAIM-CORE-MLR`.
  - Ülke sürümü: `LocalTemplateCodeFormat` = `CLAIM-LOCAL-MLR-{0}` (`{0}` = ülke kodu).
  - Yalnız yerel iddia: kendi ülkesinin şablonu.
  - Şablonun kendisi Faz 4'te oluşturulur (CL-CFG-1). Şablon yoksa Platform hata verir → CRM **409 `approval_template_missing`**. Sessiz geri düşüş yok.
- **Adaylar şablondan gelir** (pozisyonlar, WP-ORG-02). CRM `CandidatePrincipalIds` göndermez (boş).
- **ObjectType / ObjectRef:**
  - `crm.claim` / `crm/claim/{claimId}`.
  - `crm.claim-country-version` / `crm/claim-country-version/{versionId}`.
- **DisplayContext:**
  - Çekirdek: Title `İddia onayı · {ClaimCode} v{ClaimVersion}`, Subtitle `{ClaimName}`, SourceModule `crm`, DeepLinkUrl `/CRM/Claims/Details/{claimId}`, Chips `[Kind, "v{ver}"]`.
  - Ülke sürümü: Title `İddia ülke onayı · {ClaimCode} · {CountryCode} v{Version}`, DeepLinkUrl `/CRM/Claims/Details/{claimId}?country={CountryCode}`, Chips `[CountryCode, "v{ver}"]`.
  - Metinler Türkçe sabit (arayüz dili yok; WorkCenterNext başlığı).
- **Idempotency key:** `crm:{objectType}:{objectId}:r{roundNo}`.

## NE
1. **Claim'e `ReviewRounds`** (ClaimCountryVersion'daki `ClaimReviewRound` yeniden kullanılır):
   - Alanlar genişler: `RoundNo`, `TemplateCode`, `CompletedBy`, `ReasonCode`.
   - Class-map'te `WorkflowInstanceId` stringGuid (Guid tuzağı).
2. **Onaya gönder:**
   - Uçlar: `POST claims/{id}/submit-review` · `POST claims/country-versions/{id}/submit-review` (`crm.claim.manage`).
   - Durum `draft` olmalı; değilse 409 `invalid_status`.
   - **Çekirdek/yerel:** metin ve ürün dolu.
   - **Ülke sürümü:** `core` ise bağlı çekirdek onaylı (`core_not_approved`); ülkenin **tüm dillerinde** metin (BE-1 geçici onaydaki kural buraya taşınır; `languages_incomplete`); hücre kapalı değil.
   - Platform'a `POST /api/v1/workflow/instances` (token + tenant iletilir; şablon kodu, ObjectType/Id/Ref, IdempotencyKey, DisplayContext).
   - Başarılı: yeni tur eklenir (`RoundNo` = önceki + 1), durum **in-review**.
   - Platform 4xx (şablon yok) → `approval_template_missing`. 403 → `approval_forbidden`. 5xx ya da ağ hatası → 503 `workflow_unavailable`.
   - **Hata halinde hiçbir şey yazılmaz.**
3. **Geri çek:**
   - Uçlar: `POST claims/{id}/withdraw-review` · `POST claims/country-versions/{id}/withdraw-review`.
   - Açık turun aktif görevi instance detayından bulunur → Platform `tasks/{taskId}/cancel`.
   - Tur `Outcome = cancelled`, durum **draft**. Terminal olay gelirse idempotent işlenir.
4. **Sonuç olayı tüketicisi:**
   - CRM'e MassTransit + RabbitMQ, **AuthService desenini** izleyerek: `CrmEventingOptions`, `UseRabbitMq`, dev `appsettings.Development.json`'da Platform ile aynı broker.
   - `ClaimWorkflowOutcomeConsumer : IConsumer<EventTransportMessage>`:
     - Yalnız `platform.workflow.instance.completed.v1` ve objectType `crm.claim` / `crm.claim-country-version` işlenir. Diğer olaylar yok sayılır.
     - **Inbox idempotency:** `crm_event_inbox` koleksiyonu, EventId tekil.
     - Tenant olaydan alınır, repository tenant-scoped çalışır.
     - Instance, nesnenin **açık turu** değilse yok sayılır ve loglanır.
   - **Uygulama `ApplyReviewOutcome`** (tek yerde; tüketici ve uzlaştırma aynı yolu kullanır):
     - `approved` → nesne **approved**, tur kapanır. Çekirdekte BE-1 yayılımı buraya **taşınır** (eski çekirdek inactive, o ClaimCode'un onaylı ülke sürümleri review-required). Ülke sürümünde önceki onaylı sürüm inactive.
     - `rejected` → **draft** + tur `rejected` + reasonCode.
     - `cancelled` / `timed-out` → **draft** + tur outcome.
5. **Okumada uzlaştırma:**
   - Liste, detay ve coverage okumalarında açık turu olan ve `SubmittedAt` üzerinden `Crm:Claims:Workflow:ReconcileAfterSeconds` (varsayılan 120 sn) geçmiş kayıtlar toplanır.
   - Çağıranın token'ıyla `by-objects` (≤100/çağrı) sorulur. Terminal olanlara `ApplyReviewOutcome` uygulanır.
   - Platform'a erişilemezse okuma **bozulmaz** (log + devam).
6. **Tur geçmişi okuması:**
   - Uçlar: `GET claims/{id}/review-history` · `GET claims/country-versions/{id}/review-history`.
   - Turlar (en yeni önce) ve her tur için Platform instance detayından adım satırları: adım adı, işlem yapan, işlem, zaman, yorum.
   - Platform detayı yorum veya adım bilgisi vermiyorsa yalnız turlar döner ve raporlanır.
7. **Doğrudan onayın kaldırılması:**
   - `POST claims/{id}/approve` ve `POST claims/country-versions/{id}/approve` → **409 `approval_via_workflow_only`**.
   - Komutlar ve handler'lar silinir, `TEMPORARY` işaretleri kalkar.
   - Onay mantığı yalnız `ApplyReviewOutcome`'da kalır.
8. **Kilit:** `in-review` durumunda güncelleme, yeni sürüm açma ve arşivleme → 409 `in_review_locked`.
9. **Audit:** `claim_review_submitted / withdrawn / outcome_applied` (id, tur, outcome; metin yok).

## KORU / YAPMA
- **Platform, Auth, Web, gateway DOKUNMA.** Platform sözleşmesi BE-3'teki gibi kullanılır.
- **Kanıt (≥1) kuralı YOK** (BE-5). Workflow şablonu ya da pozisyon oluşturma YOK (CL-CFG-1). İzin grant YOK (Faz 4).
- **Servis token'ıyla workflow başlatma YOK.** Yalnız kullanıcı token'ı ile; aksi halde SoD sessizce devre dışı kalır.
- BE-1 kuralları ve hata kodları korunur. ContentSetClaim DEĞİŞMEZ.
- Yeni base, repo ya da eventing altyapısı icat etme. AuthService desenini kopyala (inbox dahil).
- **DUR:**
  - `EventTransportMessage` ve imza/güven doğrulaması (Platform yayını) CRM'e taşınamıyor ya da BuildingBlocks'ta yoksa → dur ve raporla.
  - Instance detayı aktif görev id'sini vermiyorsa → geri çekmeyi yapma, dur ve raporla.
  - RabbitMQ ayarı CRM'e eklenince CRM açılışı brokersız ortamda kırılıyorsa → `UseRabbitMq=false` iken hiç kaydolmayacak şekilde yap ve raporla.

## Acceptance
- **E2:**
  - `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo` → **0 kırmızı** (taban 1974/0/5 + yeni).
  - `dotnet test frontend/Diten.Web.Tests -c Release --nologo` → 229/0.
  - CRM API build 0 hata.
- **Yeni testler** (Platform HTTP ve olay sahte):
  - Gönderim: token ve tenant iletiliyor, DisplayContext ve IdempotencyKey doğru, tur ekleniyor, durum in-review.
  - Şablon yok → `approval_template_missing`; 5xx → `workflow_unavailable`; hata halinde yazma yok.
  - Ülke sürümü: dil eksik → `languages_incomplete`; çekirdek onaysız → `core_not_approved`.
  - Tüketici: approved / rejected / cancelled / timed-out uygulanıyor; yabancı olay yok sayılıyor; tekrar olay (inbox) etkisiz; açık tur olmayan instance yok sayılıyor.
  - Çekirdek approved → yayılım (eski inactive + ülke sürümleri review-required).
  - Uzlaştırma: süresi geçmiş açık tur by-objects ile kapanıyor; Platform erişilemezken okuma bozulmuyor.
  - Geri çek: cancel çağrılıyor → draft.
  - Doğrudan approve → `approval_via_workflow_only`.
  - in-review'da güncelleme → `in_review_locked`.
  - Kiracı izolasyonu.
  - **Sabotaj kanıtı** en az 3 kural için.
- **Diff:** yalnız `services/Diten.CrmService/**`.
- **E4:** birleştirme + CL-CFG-1 (şablonlar) + Faz 4 grant sonrası canlı. **Bu WP'de değil.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-4 · İddia ↔ MOD-0023 onay akışı (onaya gönder/geri çek · sonuç olayı · turlar · okumada uzlaştırma · doğrudan onayın kaldırılması) (CRM, backend)
Repository: C:\tmp\cl-be-4 (worktree) · Branch: wp/cl-be-4 (taban test/crm-content-visit-e2e @1e2f05c7) · commit bu dala, push YOK · BE-6 paralel — ortak dosyalarda küçük bölgeler değiştir

Amaç: Çekirdek/yerel iddia ve ülke sürümü YALNIZ MOD-0023 workflow sonucuyla onaylansın — kullanıcının token'ıyla başlat (SoD), WorkCenterNext başlığı (DisplayContext), sonuç platform.workflow.instance.completed.v1 olayıyla CRM'e dönsün, kaçan olay okumada uzlaştırılsın, tur geçmişi tutulsun, geçici doğrudan onay uçları kaldırılsın. Kanıt (≥1) kuralı YOK (BE-5).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-4-claims-workflow-approval.md (Kararlar + NE 1–9 + hata kodları) · …/WP-CL-BE-3-workflow-cross-service.md §Tüketici rehberi · …/WP-CL-BE-1-claims-model-v2.md · services/Diten.CrmService/src/** (Claim.cs, ClaimCountryVersion.cs [ClaimReviewRound], Claims/ClaimV2CommandHandlers.cs:727, ClaimCommands.cs:114, ClaimsController.cs:156, Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs:393 ForwardContextHeaders, Persistence/DependencyInjection.cs class-map) · services/Diten.AuthService/src/Diten.AuthService.Infrastructure/{DependencyInjection.cs:115-150, Eventing/EntitlementSyncConsumer.cs, Eventing/AuthServiceEventingOptions.cs} (MassTransit+inbox deseni) · services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/** · memory crm-new-aggregate-classmap-guid.

NE:
 1) Claim'e ReviewRounds (ClaimReviewRound yeniden kullan; +RoundNo, TemplateCode, CompletedBy, ReasonCode; WorkflowInstanceId stringGuid).
 2) POST claims/{id}/submit-review + claims/country-versions/{id}/submit-review (crm.claim.manage): draft değilse 409 invalid_status; çekirdek/yerel metin+ürün; ülke sürümü: core onaylı (core_not_approved), ülkenin tüm dilleri (languages_incomplete — BE-1 geçici onaydaki kural buraya), hücre kapalı değil; Platform POST /api/v1/workflow/instances (Authorization+X-Tenant-Id ilet; şablon Crm:Claims:Workflow:CoreTemplateCode=CLAIM-CORE-MLR / LocalTemplateCodeFormat=CLAIM-LOCAL-MLR-{0}; ObjectType crm.claim|crm.claim-country-version; ObjectRef crm/claim/{id}|crm/claim-country-version/{id}; IdempotencyKey crm:{type}:{id}:r{n}; DisplayContext WP'deki gibi; CandidatePrincipalIds boş) → tur ekle, in-review; şablon yok 409 approval_template_missing, 403 approval_forbidden, 5xx/ağ 503 workflow_unavailable; hata halinde yazma yok.
 3) withdraw-review (iki uç): instance detayından aktif görev → Platform tasks/{taskId}/cancel → tur cancelled, draft.
 4) CRM'e MassTransit+RabbitMQ (AuthService deseni, CrmEventingOptions/UseRabbitMq, dev ayar Platform ile aynı broker) + ClaimWorkflowOutcomeConsumer : IConsumer<EventTransportMessage> (yalnız platform.workflow.instance.completed.v1 + objectType crm.claim/crm.claim-country-version; inbox crm_event_inbox EventId tekil; tenant olaydan; açık tur değilse yok say+log) → ApplyReviewOutcome (tek yer): approved → approved (+ çekirdekte BE-1 yayılımı buraya TAŞI; ülke sürümünde önceki onaylı inactive) · rejected → draft + reasonCode · cancelled/timed-out → draft.
 5) Okumada uzlaştırma: liste/detay/coverage'da açık turu SubmittedAt+Crm:Claims:Workflow:ReconcileAfterSeconds(120) geçmiş kayıtlar → çağıranın token'ıyla by-objects (≤100) → terminalse ApplyReviewOutcome; Platform yoksa okuma bozulmaz.
 6) GET claims/{id}/review-history + claims/country-versions/{id}/review-history → turlar + Platform instance detayından adım satırları (adım, kim, işlem, zaman, yorum); detay yetersizse yalnız turlar + raporla.
 7) Doğrudan approve uçları (claims/{id}/approve, claims/country-versions/{id}/approve) → 409 approval_via_workflow_only; komut/handler sil, TEMPORARY işaretleri kalksın.
 8) in-review'da güncelleme/yeni sürüm/arşiv → 409 in_review_locked.
 9) Audit claim_review_submitted/withdrawn/outcome_applied (metinsiz).
KORU/YAPMA: Platform/Auth/Web/gateway DOKUNMA; kanıt kuralı YOK; şablon/pozisyon oluşturma YOK; grant YOK; servis token'ıyla workflow başlatma YOK; BE-1 kural/kodları korunur; ContentSetClaim DEĞİŞMEZ; yeni altyapı icat etme, AuthService desenini kopyala.
DOĞRULA (E2): cd C:\tmp\cl-be-4; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 1974/0/5 + yeni); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 229/0; CRM API build 0 hata; yeni testler WP Acceptance listesindeki gibi + ≥3 sabotaj kanıtı; git diff yalnız services/Diten.CrmService/**. Commit ("feat(crm): WP-CL-BE-4 — claims approval via MOD-0023 workflow (submit/withdraw, outcome consumer, rounds, reconcile-on-read)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: EventTransportMessage + imza/güven doğrulaması CRM'e taşınamıyorsa DUR+raporla; instance detayı aktif görev id'si vermiyorsa geri çekmeyi yapma, DUR+raporla; RabbitMQ ayarı brokersız açılışı kırıyorsa UseRabbitMq=false'ta kayıt yapma+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (E2)** — canlı öncesi gerekli: **WP-CL-BE-3a** + CL-CFG-1 + grant
```
Commit: c3a62ef4 · Agent: PASS (CRM 2016/0/5, Web 229/0, 4 sabotaj/6 kırmızı) · CT: worktree C:\tmp\cl-be-4 → CRM 2016/0/5; BE-6 ile birleşik 2040/0/5, Web 229/0, API build 0 hata
```
- ✅ **Kapsam:** 26 dosya, yalnız `services/Diten.CrmService/**`. `TEMPORARY` işareti kalmadı. MassTransit.RabbitMQ 8.3.6 eklendi (AuthService ile aynı sürüm).
- ✅ **Tasarım:**
  - Onay yalnız `ApplyReviewOutcome` (tek yer) ile işliyor. Tüketici ve okumada uzlaştırma aynı yolu kullanıyor.
  - Inbox (EventId `_id`) ile tekrar etkisiz.
  - Olay, CRM'deki açık tur + nesne + tenant ile birebir eşleşmezse yok sayılıyor. İmza doğrulaması bugün yok, bu kontrol telafi ediyor.
  - Doğrudan approve → 409 `approval_via_workflow_only`. in-review kilidi var.
- ⛔ **Canlı engel (CT teyit):** Platform `StartWorkflowInstanceValidator.cs:26-28` boş `CandidatePrincipalIds`'i reddediyor. Oysa handler (`StartWorkflowInstanceHandler.cs:114-134`) şablon adaylarını zaten önceliklendiriyor. → **WP-CL-BE-3a** düzeltecek.
- ⚠ **Eski İddialar UI "Onayla" düğmesi 409 alır.** CL-FE işi.
- ⚠ **Geri çekme:** eskalasyona düşmüş adım geri çekilemez → 409 `withdraw_not_possible`.
- ⚠ Dev'de RabbitMQ açık. Broker kapalıyken CRM açılışı denenmedi; canlı E4'te bakılacak.
- ℹ **Önerilen Platform işleri:**
  - (1) Boş aday → **BE-3a**.
  - (2) Olay imzalama ve doğrulama → ayrı güvenlik işi.
  - (3) Yorumlu geçmiş → **BE-3a**.
