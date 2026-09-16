# WORK PACKAGE — WP-SCMM-10-MOD-C · ChainTemplate Moderator/ForWhom template-level (frontend)

> **CT (SoR).** Faz 1 kapanış. §14 brief: DESIGN-SCMM-10-moderator-forwhom-template-level (D-a..f). Module **MOD-0162** (SCMM-10). Branch `feature/scmm-content-studio`. **Yalnız frontend** (Diten.Web). Backend=WP-A ✓ (8e313911, CT-E2), reference set=WP-B ✓ (956c0e7a, canlı seed).

## Ölçülmüş girdi (CT)
- **Backend sözleşmesi (WP-A sonrası):** `ConceptChainTemplateDto`/Create+Update request artık **template-seviyesinde** `ModeratorRoleType` (string?) + `ForWhomAudienceProfileIds` (List<Guid>) taşır; `ConceptChainStepDto`/StepRequest'te **AllowedRoleRefs/AudienceDimensionRefs YOK** (kaldırıldı).
- **Frontend dosyaları:** `wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js` (Chain Template Compact authoring) + `concept-slim.js` (step builder kalıntıları) · Views `_TemplateForm.cshtml` · `_TemplateFormL10n.cshtml` · `_TemplateDetailsQuickView.cshtml`.
- **Veri kaynakları (mevcut, hazır):** Moderator = `content-moderator-role` published-values → **KnowledgeController proxy zaten var**: `/api/v1/reference-data/sets/{setCode}/published-values?scope_key={JWT tenant}` (KnowledgeController.cs ~387; setCode=`content-moderator-role`). ForWhom = AudienceProfile listesi (AUD-UI/WP-10-refine ForWhom "audience-profiles" endpoint'i — mevcut).
- **Desenler:** AUD-UI reference-driven **small select2** (select-by-name/store-code=ValueCode) · WP-10-refine ForWhom audience-profiles multi-select2 · **checklist compose-then-add** (`.diten-checkitem`, compose-row + Add below — AUD-UI-6).

## Kapsam (frontend)
1. **Identity & Classification** (template-seviyesi) bölümüne EKLE:
   - **Moderator** — `content-moderator-role` published-values ile **tek** small select2 (store ValueCode → `ModeratorRoleType`); boş=belirsiz OK.
   - **ForWhom** — AudienceProfile **multi** select2 (store id[] → `ForWhomAudienceProfileIds`); boş liste OK.
2. **Step satırından KALDIR:** Moderator/ForWhom (AllowedRoleRefs/AudienceDimensionRefs) alanları — backend'de yok; UI'dan da temizle (template-form.js + concept-slim.js kalıntıları + _TemplateForm.cshtml).
3. **Branches = Tasks-checklist compose-then-add** (AUD-UI deseni): `.diten-checkitem` liste + compose-row (Concept Type seç + Min/Max) + **Add** butonu altta; eklenen branch/step satırları checklist item olarak.
4. **Sözleşmeye bağla:** Create/Update template payload'ı `ModeratorRoleType` + `ForWhomAudienceProfileIds` gönderir; step payload'ından refs çıkar. DetailsQuickView template-seviyesi Moderator/ForWhom gösterir.
5. **L10n:** yeni etiketler (`_TemplateFormL10n.cshtml`) — Moderator/ForWhom/Branches label'ları 7 dil (ham key gösterme; mevcut L10n bridge deseni).

## YAPMA
- Backend/DTO/API (WP-A ✓). Reference set seed (WP-B ✓). **Faz 2** ModeratorPositionRef / Organization positions lookup (ModeratorRoleType=position cascade) — Faz 1 yalnız rol-tipi. Content Set/eligibility/resolver. Başka modül. Yeni nav sayfası.

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` baseline-diff yeşil (yeni fail yok). Template create/update Moderator+ForWhom round-trip (payload→DTO); step satırında Moderator/ForWhom yok; Branches checklist compose-then-add çalışır; L10n anahtarları var (ham key yok). NavManifestL10nGuardTests etkilenmez (yeni nav sayfası yok).
- **E4 (CT/kullanıcı):** A2d manuel test — CHAIN-CARN-01, Identity&Classification'da Moderator + ForWhom=Nefrolog (template-seviyesi), Branches checklist; Golden Compact + required-tracker.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SCMM-10-MOD-C · Prompt v1.0  (ChainTemplate Moderator/ForWhom template-seviyesi — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 956c0e7a (WP-A backend 8e313911 + WP-B ref set sonrası) · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-C-frontend-moderator-forwhom.md (bu WP) + DESIGN-SCMM-10-moderator-forwhom-template-level.md (D-a..f)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js + concept-slim.js (mevcut step-level Moderator/ForWhom + builder)
3. Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml + _TemplateFormL10n.cshtml + _TemplateDetailsQuickView.cshtml
4. frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs (~381-390 published-values proxy setCode/scope_key + AudienceProfile options endpoint)
5. AUD-UI desenleri: reference-driven small select2 (store ValueCode) + checklist compose-then-add (.diten-checkitem, AUD-UI-6) + WP-10-refine ForWhom audience-profiles multi-select2

Backend sözleşmesi (WP-A ✓, değiştirme): ConceptChainTemplateDto/Create+Update request template-seviyesinde ModeratorRoleType(string?) + ForWhomAudienceProfileIds(List<Guid>); ConceptChainStepDto/StepRequest'te AllowedRoleRefs/AudienceDimensionRefs YOK.

NE (frontend):
 1) Identity & Classification (template-seviyesi): Moderator = content-moderator-role published-values small select2 (tek, store ValueCode→ModeratorRoleType, boş OK) + ForWhom = AudienceProfile multi-select2 (id[]→ForWhomAudienceProfileIds, boş OK).
 2) Step satırından Moderator/ForWhom (refs) KALDIR (template-form.js + concept-slim.js kalıntı + _TemplateForm.cshtml).
 3) Branches = Tasks-checklist compose-then-add (.diten-checkitem + compose-row Concept Type+Min/Max + Add altta, AUD-UI-6 deseni).
 4) Payload: create/update template ModeratorRoleType+ForWhomAudienceProfileIds gönderir, step'ten refs çıkar; DetailsQuickView template-seviyesi gösterir.
 5) L10n: yeni etiketler _TemplateFormL10n.cshtml (ham key yok).
NASIL: Moderator picker AUD-UI reference-driven small select2 birebir (KnowledgeController published-values proxy, setCode=content-moderator-role, scope_key server-side JWT tenant); ForWhom WP-10-refine audience-profiles multi-select2; Branches AUD-UI-6 checklist. Sözleşmeye bağlan, uydurma alan yok.
YAPMA: backend/DTO/API (WP-A); reference set (WP-B); Faz2 ModeratorPositionRef/position lookup; Content Set/eligibility/resolver; başka modül; yeni nav sayfası.
DOĞRULA (E2): frontend/Diten.Web.Tests baseline-diff yeşil; template Moderator+ForWhom round-trip; step'te refs yok; Branches checklist çalışır; L10n anahtarları var. Ayrı commit. §22 TÜRKÇE. K13 — CT bağımsız doğrular.

Durma koşulları: published-values proxy content-moderator-role'ü döndürmüyorsa (WP-B seed kontrol) · AudienceProfile options endpoint yoksa · backend sözleşmesi beklenenden farklıysa · kapsam frontend dışına taşarsa → DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 67262f31 (CT adına; agent commit etmemişti) · Agent: PASS · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-wpc-verify
```
- ✅ **Scope:** yalnız frontend/Diten.Web — KnowledgeConceptsController + 7 view-resx (`KnowledgeConceptsIndex.*`) + 3 view + 2 JS. Backend/DTO/SharedResource/başka modül sızıntısı YOK.
- ✅ **Sapma (kabul):** proxy KnowledgeController'da (`CRM/Knowledge` route) sayfadan erişilemezdi → agent aynı deseni **KnowledgeConceptsController**'a (`CRM/KnowledgeConcepts`) ekledi. `[HttpGet("api/reference-data/{setCode}/values")]` → `ProxyGetAsync(path, ReadPermission, ct, ReadFallback)`, **scope_key=JWT tenant (client'tan değil)**. Gate sağlam, yeni güvenlik açığı yok.
- ✅ **Logic:** template-form.js template-seviyesi Moderator(content-moderator-role→ModeratorRoleType) + ForWhom(AudienceProfile[]→ForWhomAudienceProfileIds); step `{conceptTypeId,min,max}` refs-free; Branches `.diten-checkitem` compose-then-add (AUD-UI-6, up/down/remove). concept-slim.js quick-view template-seviyesi gösterir.
- ✅ **L10n:** 7 view-resx geçerli XML, **dupe=0**, yeni key'ler (Moderator/ModeratorHint/ForWhomHint/ConceptType) hepsinde mevcut; SharedResource'a dokunulmadı → NavGuard etkilenmez.
- ✅ **Build+test (CT izole, Release):** build 0-err; **Diten.Web.Tests 137/137, 0 fail** (baseline-diff temiz).
- ⏳ **E4:** A2d manuel test (nihai model).

**FAZ 1 (WP-A + WP-B + WP-C) TAMAM — hepsi CT-doğrulandı.**

## Kalan (bu WP dışı)
- **A2d manuel test** (nihai model) → A3 içerik → A4 claim → A5 eligibility → A6 scope → A7 set → A8 evaluate → sync/PR. · **Faz 2:** ModeratorRoleType=position → Organization positions lookup (MOD-0288) → ModeratorPositionRef picker.
