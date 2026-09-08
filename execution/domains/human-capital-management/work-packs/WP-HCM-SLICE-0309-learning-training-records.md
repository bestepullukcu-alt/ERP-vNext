WORK PACKAGE — full end-to-end module slice (pack + backend + API + frontend CRUD + test)

WP ID:            WP-HCM-SLICE-0309
Prompt ID:        P-HCM-SLICE-0309
Prompt Version:   v1.0
Task Class:       full module slice (greenfield)
Golden-Flow Profile: A (state-changing CRUD)
Risk Class:       MEDIUM
State:            READY
Target Agent / Entry Point: @orchestrator / module-pack-author + backend-architect + frontend-ui-ux (paste-block leads @module-pack-author)

Module: Learning / Training Records · planning id MOD-0309 · proposed alias CAND-CAP-0030 · domain human-capital-management · service Diten.HumanCapitalService (5059)
Reference (established pattern, copy/adapt): CompetencySkills (0307) end-to-end — pack CAND-CAP-0028, backend Features/CompetencySkills, Api/Hcm/CompetencySkillsController, frontend HumanCapital/CompetencySkills golden-compact CRUD.
Sequence: R3-C next after 0307/0308. Next after this: MOD-0320 Succession & High-Potential.

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001, gateway 5080, HCM 5059. Login: /account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). USE localhost.

Scope:
- Allowed (WRITE): new CAND-CAP-0030 pack; HCM backend (Domain/Application/Persistence/Api) for LearningTraining;
  gateway ocelot.json (learning-training routes → 5059); auth DataSeeder (new hcm.learning-training.* permissions);
  frontend HumanCapital/LearningTraining (Controller/Views/js/resx) + _LayoutTenantShell nav entry; HCM tests.
- Protected: other modules, the 4 read-only governance modules, dt-defaults.js/auth (already fixed), other services, existing packs/registry rows.

Objective: build MOD-0309 as a metadata-only readiness slice, end-to-end, identical in shape to CompetencySkills (0307).
Do not repeat the earlier list-column drift (JS column ↔ ListItemDto field must match). Remember the permission-seed step
(missing it = nav hidden / 403). Golden-compact CRUD (create/evaluate/delete) wired to the new endpoints.

Acceptance (E3/E4): build 9/9 green; login → "Learning / Training" nav visible; page opens with no reload loop; +New →
create (real DTO fields) → list; evaluate/delete work; Mongo DitenHumanCapital collection gets the doc + soft-delete;
l10n tr/en real Turkish; console clean. Per §22 report + evidence.

Failure Protocol: missing contract/field → STOP (K12). Reserved manager/employee UX → don't add. Don't touch governance
modules/other modules/auth. PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-SLICE-0309 · Prompt P-HCM-SLICE-0309 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001 · gateway 5080 · HCM 5059. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). localhost KULLAN.

Modül: Learning / Training Records · planning id MOD-0309 · önerilen alias CAND-CAP-0030 · domain human-capital-management · servis Diten.HumanCapitalService (5059).
REFERANS (birebir desen — kopyala/uyarla): CompetencySkills (0307) uçtan uca — pack CAND-CAP-0028, backend Features/CompetencySkills, Api/Hcm/CompetencySkillsController, frontend Views+js/HumanCapital/CompetencySkills (golden-compact CRUD).

Önce oku:
1. execution/domains/human-capital-management/module-packs/CAND-CAP-0028-competency-skills-assessment.md (pack ŞABLONU)
2. Backend referans: services/.../Application/Features/CompetencySkills/** + Domain/Entities/CompetencySkillsReadinessMetadata.cs + Api/Controllers/Hcm/CompetencySkillsController.cs
3. Frontend referans: frontend/Diten.Web/{Controllers/CompetencySkillsController.cs, Views/HumanCapital/CompetencySkills/**, wwwroot/assets/js/HumanCapital/CompetencySkills/**}
4. AGENTS.md (§3 port; tüm API gateway 5080; DCP-002 gate) · services/Diten.AuthService/.../Seed/DataSeeder.cs (izin seed)

NE: MOD-0309'u 0307 deseniyle UÇTAN UCA, metadata-only readiness slice olarak kur:
    a) PACK: CAND-CAP-0030-learning-training-records.md (draft, CAND-CAP-0028 yapısı, §20 waiver K19-tam).
    b) BACKEND: LearningTrainingReadinessMetadata (+ ReadinessState enum, repo interface, Mongo repo, DI kaydı),
       Application/Features/LearningTraining (Commands/Queries/Handlers/Guard/Models), Api/Hcm/LearningTrainingController
       (read/manage/evaluate/audit.read).
    c) API: gateway ocelot.json'a /api/learning-training rotaları → 5059 (Offer/Competency rotalarını desen al);
       izinleri DataSeeder'a EKLE (hcm.learning-training.read/manage/evaluate/audit.read) — yoksa nav gizli/403 kalır.
    d) FRONTEND: golden-compact CRUD (Controller + Views Index/Create/Details/_Form/_DataTable + js) Competency'yi
       desen al; _LayoutTenantShell nav'a "Learning / Training" (Perms.Has("hcm.learning-training.read")); l10n tr/en gerçek Türkçe.
    e) TEST: Application.Tests/LearningTrainingTests.cs (handler davranışı, fix-absent→RED, K3).
NEDEN: R3-C lane sıradaki modül (0307/0308 bitti). Kimlik Blueprint/registry'de yok → pack-first; runtime EA-waived slice deseni.
NASIL: List DTO'da JS kolonu ↔ DTO alanı eşleşsin (0303-0308'deki drift'i TEKRARLAMA). Para alanı decimal. Response<T> zarfı.
       Auth restart sonrası yeni izinlerin SuperAdmin'e düştüğünü doğrula.
YAPMA: Backend contract uydurma (K12); reserved manager/employee UX; governance modüllerine/dt-defaults/auth/diğer modüllere dokunma.
DOĞRULA: Build 9/9 yeşil; login → nav'da Learning/Training görünür; sayfa reload döngüsüz açılır; +Yeni Ekle → create → POST →
    liste; evaluate/delete çalışır; Mongo DitenHumanCapital'da kayıt; l10n gerçek Türkçe; konsol temiz. E3/E4 kanıtı + ekran görüntüsü.

Durma koşulları: eksik contract/alan · reserved UX · scope dışı. Dur ve raporla.
Rapor: §22 + oluşturulan/değişen dosyalar + izin seed diff + E3/E4 kanıtı + golden-compact ekran görüntüsü.
Senin PASS'in kapanış değildir (K13); CT bağımsız doğrular, sonra sıradaki modül (MOD-0320 Succession).
