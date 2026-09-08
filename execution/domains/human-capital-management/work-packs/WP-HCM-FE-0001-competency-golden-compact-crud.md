WORK PACKAGE

WP ID:            WP-HCM-FE-0001
Prompt ID:        P-HCM-FE-0001
Prompt Version:   v1.0
Task Class:       frontend feature — golden-compact CRUD parity (reference implementation)
Golden-Flow Profile: A (UI / state-changing)
Risk Class:       MEDIUM (state-changing UI over existing authorized endpoints; single module)
State:            READY

Module:           MOD-0307 Competency & Skills Assessment (CAND-CAP-0028) — reference implementation for the 12 HCM pages
Build Lane:       HCM-FE-golden-compact
Agent Lane ID:    AL-HCM-FE-COMPETENCY
Agent Lane Type:  DEV
Target Agent / Entry Point: frontend-ui-ux  (paste-block leads with @module-pack-author per standing pref)

CT decision (owner-approved scope): product owner decided the HCM pages must reach the golden-compact reference
with working CRUD. Scope boundary: readiness-metadata CRUD (create/evaluate/delete) is IN-SCOPE — the API already
exposes it and it is admin readiness management, NOT the pack-reserved manager/employee review UX (stays out).

Authority:
- Golden reference (COPY THIS): frontend/Diten.Web/Controllers/GoldenReferenceCompactController.cs +
  Views/DevEnablement/GoldenReferenceCompact/** + wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/index.js
  (compact pattern: list = DataTable; create/edit = full MVC pages, per its own header comment).
- API surface (already built — wire to it, do NOT change backend):
    GET    /api/competency-skills                    (list)      perm hcm.competency-skills.read
    GET    /api/competency-skills/{id}               (detail)    perm hcm.competency-skills.read
    POST   /api/competency-skills                    (create)    perm hcm.competency-skills.manage
    POST   /api/competency-skills/{id}/evaluate      (evaluate)  perm hcm.competency-skills.evaluate
    DELETE /api/competency-skills/{id}               (delete)    perm hcm.competency-skills.manage
    GET    /api/competency-skills/{id}/audit-metadata            perm hcm.competency-skills.audit.read
  (No PUT/update endpoint — lifecycle is create → evaluate → delete; "edit" = re-evaluate, not free edit.)
- Create request contract: services/.../Application/Features/CompetencySkills/CompetencySkillsModels.cs (read the
  create command DTO; build the form from its real fields — do NOT invent fields, K12).
- Repo-wide: AGENTS.md (§3: all API via gateway; browser base window.API.hcm → :5080 this session).

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
- Runtime UP: frontend 5001, gateway 5080, HCM 5059. Tenant login: /account/login?tenantId=00000000-0000-0000-0000-000000000001

Dependencies:
- Depends on: none (endpoints exist; perms seeded this session)
- Parallel-safe with: read-only WPs; NOT with another writer on CompetencySkills frontend
- Integration order: this is the REFERENCE; the other 11 HCM pages follow after CT accepts it

Scope:
- Allowed paths (WRITE — frontend only):
    frontend/Diten.Web/Controllers/CompetencySkillsController.cs
    frontend/Diten.Web/Views/HumanCapital/CompetencySkills/**
    frontend/Diten.Web/wwwroot/assets/js/HumanCapital/CompetencySkills/**
    frontend/Diten.Web/Resources/Views/HumanCapital/CompetencySkills/**  (l10n resx, tr+en)
- Protected: HCM backend/service, gateway/ocelot, other modules, module packs, _LayoutTenantShell (nav already wired).

Objective:
Bring the Competency & Skills page to GoldenReferenceCompact parity with working CRUD:
  - List: DataTable matching the compact reference (toolbar with an "Ekle/New" button, column visibility, filters).
  - Create: full MVC page (compact pattern) posting to POST /api/competency-skills → on success return to list + refresh.
  - Row actions: Details (detail page/panel), Evaluate (POST /{id}/evaluate), Delete (DELETE /{id}, confirm dialog).
  - All calls go through window.API.hcm (gateway 5080) with the bearer token; honor Response<T> envelope.
  - Visual/layout parity with GoldenReferenceCompact (spacing, card, toolbar, states).

Pattern:  Compact (single detail / section + full MVC create page). Justification: readiness records are structured,
          admin-managed, revisable via evaluate — matches the compact large-field reference.

Persistence: L2/L3 via existing endpoints (no new persistence).
Consistency: rely on server behavior; surface 409/validation errors in the UI.

Golden / Contract flow:
actor(admin) → open Competency page → "Ekle" → create page → fill (real DTO fields) → save → POST → 201 → list refreshed
→ row Evaluate → POST /{id}/evaluate → state updates → row Delete → DELETE → row gone. All via gateway 5080, authed.

Acceptance Criteria (measurable):
- "Ekle/New" button present and opens the compact create page; successful create shows the new row.
- Evaluate and Delete row actions call the correct endpoints and reflect results without a full reload error.
- UX states: loading, empty, validation error, permission-denied (403), save-failed — handled (no raw JSON/exception text in UI).
- l10n: all new labels in CompetencySkills resx (tr + en), real Turkish (no English fallback, K5).
- Visual parity with GoldenReferenceCompact (screenshot compare acceptable).
- Runtime E3 through gateway 5080 with a logged-in tenant token: create→list→evaluate→delete demonstrated.
- No backend/gateway/other-module/pack file changed.

Failure Protocol: if a needed field/endpoint is missing → STOP and report (do not invent backend, K12); do not add
manager/employee review UX (pack-reserved); do not touch other modules.

Output Contract: §22 report + changed files + runtime evidence (E3) + screenshots vs golden compact. PASS ≠ CT ACCEPTED (K13).

Rollout (after CT accepts this reference) — OWNER DECISION 2026-09-07: golden-compact CRUD applies to the 8 READINESS
modules ONLY. Remaining 7 after Competency: applicant-intake, candidate-pipeline, offer-management, employee-onboarding,
employment-changes, performance-reviews, development-plans — each wired to its own create/evaluate/delete endpoints.

READ-ONLY BY DESIGN — do NOT add CRUD (owner decision; pack-scoped governance surfaces): employee-projections,
hr-sensitive-access, position-assignments, offboarding-cases. Their write endpoints exist but the packs reserve/defer
the write UX (Position: assignment mutation deferred; Offboarding: workflow/review/handoff + TEP deferred; Sensitive
Access: write gated on MOD-0021 audit integration, fail-closed/deferred). These stay read-only inspection pages.

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-FE-0001 · Prompt P-HCM-FE-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP: frontend http://localhost:5001 · gateway http://localhost:5080 · HCM 5059. Tenant login: /account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!)

Önce oku (sırayla):
1. GOLDEN REFERANS (birebir taklit et): frontend/Diten.Web/Controllers/GoldenReferenceCompactController.cs +
   Views/DevEnablement/GoldenReferenceCompact/** + wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/index.js
2. Mevcut sayfa: frontend/Diten.Web/{Controllers/CompetencySkillsController.cs, Views/HumanCapital/CompetencySkills/**, wwwroot/assets/js/HumanCapital/CompetencySkills/**}
3. Create contract: services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/Features/CompetencySkills/CompetencySkillsModels.cs (create command ALANLARI)
4. AGENTS.md (§3 port şeması; tüm API gateway üzerinden; tarayıcı base window.API.hcm → :5080)

NE:      Competency & Skills sayfasını GoldenReferenceCompact ile eşdeğer hale getir ve CRUD'u ÇALIŞTIR:
         - Liste: compact referanstaki DataTable + toolbar'da "Ekle" butonu.
         - Ekle: compact desen = tam MVC create sayfası → POST /api/competency-skills → başarıda listeye dön + yenile.
         - Satır aksiyonları: Detay (GET {id}), Evaluate (POST {id}/evaluate), Sil (DELETE {id}, onay dialogu).
         - Tüm çağrılar window.API.hcm (gateway 5080) + bearer token; Response<T> zarfına uy.
NEDEN:   Sayfalar şu an salt-okunur; API zaten create/evaluate/delete sunuyor ama UI bağlanmamış. Ürün sahibi
         golden-compact + CRUD istedi. Bu, reserved manager/employee UX DEĞİL — admin readiness yönetimi.
NASIL:   PUT/update YOK — yaşam döngüsü create→evaluate→delete. Create formunu GERÇEK DTO alanlarından kur (uydurma, K12).
         Compact deseni: create/edit tam MVC sayfası. UX state'leri: loading/empty/validation/403/save-failed.
YAPMA:   Backend/gateway/ocelot/diğer modüller/pack/_LayoutTenantShell'e DOKUNMA. Manager/employee review UX EKLEME
         (pack reserved). Eksik alan/uç varsa DUR ve raporla (backend uydurma).
DOĞRULA: "Ekle" butonu + create sayfası çalışır; create sonrası yeni satır görünür; Evaluate/Delete doğru uçları çağırır;
         l10n tr+en gerçek Türkçe (K5); GoldenReferenceCompact ile görsel parite; UI'da ham JSON/exception METNİ YOK.
         Runtime E3 (gateway 5080, tenant token): create→list→evaluate→delete kanıtı + ekran görüntüsü.

Durma koşulları: eksik contract/alan · reserved UX ihtiyacı · scope dışı. Dur ve raporla.

Rapor: §22 structured report + değişen dosyalar + E3 runtime kanıtı + golden-compact karşılaştırma ekran görüntüsü.
Senin PASS'in kapanış değildir (K13); CT bağımsız doğrular, sonra diğer 11 modüle yayarız.
