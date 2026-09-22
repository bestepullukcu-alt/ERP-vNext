# WORK PACKAGE — WP-SCMM-15 · ContentSetRevision: draft→değişmez Revision freeze + in-domain review kararı (backend)

> **CT (SoR).** CAND-CAP-0011 / SCMM-15 (DEC-SCMM-04 **C3**). Branch `feature/crm-scmm-studio`. **Kullanıcı kararı:** SCMM-15/16/17 sırayla; **review bağı = in-domain state** (MOD-0023 partial/blocked → tam workflow routing ERTELENDİ, F-SCMM-15-WORKFLOW). R1 audit fail-closed ATLANDI → not: release gate (SCMM-17) mevzuat açısından audit-blocked kalır; bu WP freeze+review **mekanizmasını** kurar. **CrmService (Domain+Application+Api+Persistence).** ContentSet aggregate DEĞİŞMEZ.

## Kanıt
- **ContentSet (SCMM-14):** mutable draft — Status draft/inactive/archived, **frozen/approved YOK** (doc açıkça "that is the SCMM-15 Revision"). Alanlar: `Template` (ContentSetTemplateRef), `Scope` (ContentSetScopeRef?), `SelectedComponents` (List<ContentSetComponent> — pinned KnowledgeContent), `SelectedClaims` (List<ContentSetClaim>), `EligibilitySnapshot`, SetCode/Name.
- **DEC-SCMM-04 C3:** "Submit review + consume decision — revision↔decision; correlated; **duplicate/stale-safe**." Kurallar: "submission **manifest'i dondurur**"; stale write→controlled conflict (silent overwrite yok); duplicate submit/decision→**mevcut sonucu döndür**; SoD (Author immutable submitted revision'ı değiştiremez; reviewer≠author).
- **Revision aggregate YOK** (find=0) → sıfırdan kurulacak.
- **Gateway:** `/api/crm/content-composition/{everything}` mevcut → yeni endpoint'ler kapsanır (ocelot değişmez).
- **Desen (aynala):** mevcut aggregate + CQRS + controller + Mongo repo + class-map (StrategyTemplate / ContentSet precedent'i).

## NE (CrmService; ContentSet DEĞİŞMEZ)
1. **Domain — `ContentSetRevision` aggregate (EntityBase):**
   - `RevisionCode` (iş anahtarı), `RevisionNumber` (int, ContentSet lineage başına artan), `ContentSetId` + `ContentSetVersion` (pinned kaynak).
   - **Dondurulmuş snapshot (submit anında, DEĞİŞMEZ):** Template ref + Scope ref + **SelectedComponents** + **SelectedClaims** + **EligibilitySnapshot** — ContentSet'ten byte-for-byte kopya; oluşturulduktan sonra **asla mutate edilmez**.
   - **ReviewStatus** (in-domain): `submitted → in-review → approved | rejected` (+ ileride released/withdrawn = SCMM-17, BURADA YOK). Vocab class.
   - **ReviewDecision:** ReviewerId + Decision + Reason + DecidedAt (null until decided).
   - SubmittedBy/SubmittedAt + CorrelationId + CreatedBy/UpdatedBy + ArchivedAt/By. Silme yok (archive-only).
2. **Application (CQRS):**
   - `SubmitContentSetForReviewCommand(contentSetId, expectedVersion?)` → ContentSet yükle; **shape guard** (en az 1 component; eligibility uygun mu — DEC gereği unresolved external-ref → reddet); snapshot'ı dondur → yeni Revision `submitted`; **idempotent** (aynı set+version için açık revision varsa onu döndür); expectedVersion uyuşmazsa **409**; RevisionNumber = lineage'de max+1.
   - `RecordReviewDecisionCommand(revisionId, decision=approve|reject, reason)` → yalnız `submitted`/`in-review`'dan; **SoD: ReviewerId ≠ SubmittedBy** (author kendi revision'ını onaylayamaz) → ihlal 403; duplicate karar → mevcut sonucu döndür; approved/rejected'a geçir + ReviewDecision yaz.
   - (ops.) `StartReviewCommand` (submitted→in-review) — istenirse; yoksa doğrudan submitted→karar.
   - Queries: `GetContentSetRevisionByIdQuery`, `ListContentSetRevisionsQuery(contentSetId)` (newest-first).
3. **Api:** `POST /api/crm/content-composition/content-set-revisions` (submit) · `POST .../{id}/review-decision` · `GET .../{id}` · `GET .../?contentSetId=` — uygun RBAC (submit=manage; review-decision=**yeni/ayrı review izni**, SoD). Gateway {everything} kapsıyor.
4. **Persistence:** `IContentSetRevisionRepository` + Mongo repo (ListByContentSet/ById/Insert/UpdateStatus) + **RegisterClassMaps** (GUID subtype + DateTimeOffset [ticks,off] CRM konvansiyonu — yeni aggregate class-map trap'ine dikkat).
5. **RBAC:** review-decision için izin anahtarı (ör. `crm.content-set.review`) tanımla/seed (SoD rolleri: Reviewer≠Author). manage=submit için mevcut `crm.content-set.manage` reuse.

## KORU / YAPMA
- **ContentSet aggregate/status DEĞİŞMEZ** (frozen state ContentSet'e EKLENMEZ — ayrı Revision aggregate). Snapshot **immutable** (submit sonrası içerik mutate edilmez; yalnız ReviewStatus/Decision geçişi). **SoD** zorunlu (reviewer≠author). Idempotency + stale (409) + duplicate→mevcut. TenantId sunucudan. **Release/activate/withdraw (SCMM-17) + render/PDF (SCMM-16) BU WP'DE YOK.** MOD-0023 tam workflow routing YOK (in-domain state; F-SCMM-15-WORKFLOW). Diğer aggregate'ler (StrategyTemplate/Knowledge/Concept/Path/Journey/ContentScope/ContentSet mevcut davranış) DOKUNMA. Gateway/ocelot DOKUNMA. Frontend DOKUNMA (ayrı UI WP'si).
- **DUR:** ContentSet snapshot'ının derin kopyası immutable kurulamıyorsa (pinned ref yapısı); RegisterClassMaps yeni aggregate'i tanımıyorsa (GUID binary trap → sessiz boş sorgu) → DUR+raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/... -c Release` yeşil (baseline + yeni testler); Diten.CrmService.Api derleme 0 hata. git diff: yeni Domain(ContentSetRevision + vocab + repo iface) + Application(2 command + 2 query + handlers + models) + Api(controller) + Persistence(repo + class-map) + testler. **ContentSet/diğer aggregate/frontend/gateway diff YOK.** Testler: freeze-immutability · idempotent submit · stale 409 · SoD (reviewer≠author 403) · duplicate decision → mevcut · tenant izolasyon.
- **E4:** submit→Revision `submitted` (snapshot dondu) · başka aktör approve→`approved` · author kendi approve→403 · re-submit→mevcut/next revision.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-15 · ContentSetRevision draft→değişmez Revision freeze + in-domain review (CAND-CAP-0011/SCMM-15, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Worktree: ana checkout

Kullanıcı: SCMM-15/16/17 sırayla; review=in-domain state (MOD-0023 ertelendi); R1 audit atlandı (release gate audit-blocked kalır, mekanizma kurulur). CrmService only; ContentSet DEĞİŞMEZ; render/release (16/17) YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-SCMM-15-content-set-revision-freeze.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ContentSet.cs (Template/Scope/SelectedComponents/SelectedClaims/EligibilitySnapshot + statuses) · docs/decisions/DEC-SCMM-04-integration-delivery-contracts.md (C3 + concurrency/SoD kuralları) · (desen) mevcut bir aggregate+CQRS+controller+Mongo repo+RegisterClassMaps (StrategyTemplate veya ContentSet) · gateway ocelot (content-composition/{everything} — DOKUNMA).

NE (CrmService; ContentSet DEĞİŞMEZ):
 1) Domain ContentSetRevision (EntityBase): RevisionCode + RevisionNumber(lineage max+1) + ContentSetId+ContentSetVersion(pinned) + DONDURULMUŞ snapshot (Template/Scope/SelectedComponents/SelectedClaims/EligibilitySnapshot byte-for-byte, immutable) + ReviewStatus(submitted/in-review/approved/rejected — vocab class; released/withdrawn YOK) + ReviewDecision(ReviewerId/Decision/Reason/DecidedAt) + SubmittedBy/At + CorrelationId. Archive-only.
 2) Application: SubmitContentSetForReviewCommand(contentSetId, expectedVersion?) → ContentSet yükle + shape guard (≥1 component; unresolved external-ref reddet) → snapshot dondur → Revision submitted; idempotent (açık revision varsa döndür); expectedVersion mismatch→409; RevisionNumber=max+1. RecordReviewDecisionCommand(revisionId, approve|reject, reason) → submitted/in-review'dan; SoD ReviewerId≠SubmittedBy→403; duplicate→mevcut; status+decision yaz. Query: GetById + ListByContentSet(newest-first). Tenant context'ten TenantId.
 3) Api: POST /api/crm/content-composition/content-set-revisions (submit, manage izni) · POST .../{id}/review-decision (yeni review izni, SoD) · GET .../{id} · GET .../?contentSetId=. CreateActionResultInstance deseni.
 4) Persistence: IContentSetRevisionRepository + Mongo repo + RegisterClassMaps (GUID subtype + DateTimeOffset [ticks,off]; yeni-aggregate class-map trap).
 5) RBAC: crm.content-set.review anahtarı tanımla (+ seed gerekiyorsa not); submit=crm.content-set.manage reuse.
KORU/YAPMA: ContentSet aggregate/status DEĞİŞMEZ (frozen state ContentSet'e eklenmez); snapshot immutable (yalnız status/decision geçişi); SoD zorunlu; idempotency+409+duplicate→mevcut; TenantId sunucudan; SCMM-16 render + SCMM-17 release/withdraw YOK; MOD-0023 routing YOK (in-domain); diğer aggregate'ler/frontend/gateway DOKUNMA.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo → yeşil (baseline + yeni); Diten.CrmService.Api derleme 0 hata; git diff yalnız yeni Revision dosyaları + Api controller; ContentSet/diğer aggregate/frontend/gateway diff yok. Testler: freeze-immutability/idempotent/stale-409/SoD-403/duplicate/tenant. Ayrı commit ("feat(scmm): WP-SCMM-15 — ContentSetRevision freeze + in-domain review (CAND-CAP-0011)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: immutable snapshot kurulamıyorsa; RegisterClassMaps yeni aggregate'i tanımıyorsa (GUID binary trap) → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 6c991301 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-scmm15-verify @6c991301
```
- ✅ **Kapsam (14 yeni dosya + DI 8 satır, +971):** Domain(ContentSetRevision + IContentSetRevisionRepository) + Application(ContentComposition/ContentSetRevisions: 8 dosya — command/query/handlers/dtos/mapper/permissions) + Persistence(repo + DI class-map) + Api(controller + request models) + test. **ContentSet.cs/StrategyTemplate/Knowledge/ContentScope/frontend/gateway/ocelot TEMİZ** ✓. DI diff **yalnız ekleme** (mevcut kayıt silinmemiş).
- ✅ **KORU=0:** freeze = **deep-clone** (CloneComponent/Claim/Arrangement/Snapshot) → snapshot immutable, sonraki draft düzenlemeleri revision'a sızmaz; ContentSet aggregate/status DEĞİŞMEDİ (frozen state ayrı Revision'da). Idempotent (aynı version açık revision→döndür); shape guard (≥1 component + unresolved eligibility reddi, DEC-SCMM-04); stale ExpectedVersion→409; archived→409; RevisionNumber=lineage max+1; SubmittedBy=actor (SoD kimliği); SoD reviewer≠author→403 (test geçti). render/release (16/17) + MOD-0023 routing YOK.
- ✅ **Build+test (CT izole, Release):** Diten.CrmService.Api **0 hata**; Application.Tests **1845/0/5** (+~14 yeni revision testi: freeze-immutability/idempotent/stale-409/shape-400/SoD-403/duplicate/tenant). ⚠ bir koşuda 1 başarısızlık = **pre-existing PII order-flake** (ContactLocationPiiHardeningTests, SCMM-15 dışı) — re-run 1845/0 doğruladı.
- ⏳ E4: submit→frozen revision · başka aktör approve→approved · author self-approve→403. **RBAC seed açık:** `crm.content-set.review` AuthService kataloğu + 97c5 grant (ops, bu WP dışı).

**WP-SCMM-15 KOMPLE (E2). Sonraki: SCMM-16 (render/PDF).**
