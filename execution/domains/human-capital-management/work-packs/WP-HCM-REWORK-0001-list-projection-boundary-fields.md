WORK PACKAGE — REWORK (from WP-HCM-VER-0001 gap G1)

WP ID:            WP-HCM-REWORK-0001
Prompt ID:        P-HCM-REWORK-0001
Prompt Version:   v1.0
Task Class:       bounded fix (backend list projection) — CT-verified defect
Golden-Flow Profile: B (backend/contract)
Risk Class:       LOW-MED (additive DTO field surfacing; no breaking change)
State:            READY

Scope modules (4 of 8 — CT-verified list-column drift):
  MOD-0303 Employee Onboarding · MOD-0306 Performance Review · MOD-0307 Competency & Skills · MOD-0308 Development Plan
Build Lane:       HCM-R3-rework
Agent Lane ID:    AL-HCM-REWORK-LISTDTO
Agent Lane Type:  DEV
Target Agent / Entry Point: backend-architect (paste-block leads with @module-pack-author per standing pref)

Authority:
- Module Pack:   CAND-CAP-0025/0027/0028/0029 (metadata-only readiness boundary states are authorized owned objects)
- Repo-wide:     AGENTS.md (Response<T> envelope, HCM 5059)
- Evidence:      WP-HCM-VER-0001 gap G1 — CT-verified this turn (JS list field ∉ ListItemDto though field ∈ entity)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Dependencies:
- Depends on: none
- Parallel-safe with: read-only WPs; NOT with any other writer on these 4 Application features
- Integration order: standalone

Scope:
- Allowed paths (WRITE):
  services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/Features/{EmployeeOnboarding,PerformanceReviews,CompetencySkills,DevelopmentPlans}/**  (Models + Query handler ToListItem projection)
  services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**  (list-projection tests)
- Protected: Domain entities (fields already exist — do NOT modify), Persistence, Api controllers, gateway, frontend, other 4 modules, ALL packs/registry.

CT-verified defect (measured evidence, path:line):
Each JS DataTable requests list columns whose fields the `*ReadinessListItemDto` never projects, though the fields
EXIST on the entity. Confirmed by ListItemDto-record ↔ index.js data-field diff + entity field grep:
  0303 EmployeeOnboarding : `employeeActionBoundaryState`                              (entity: EmployeeActionBoundaryState ✓)
  0306 PerformanceReviews : `managerReviewUxBoundaryState`, `employeeReviewUxBoundaryState`   (entity: both ✓)
  0307 CompetencySkills   : `managerAssessmentUxBoundaryState`, `employeeAssessmentUxBoundaryState` (entity: both ✓)
  0308 DevelopmentPlans   : `goalAssignmentBoundaryState`, `managerActionUxBoundaryState`    (entity: both ✓)
Symptom: localized list column header renders but every row shows N/A (undefined field on the JSON row).

Objective (bounded):
For each of the 4 modules, add the missing boundary-state field(s) to `*ReadinessListItemDto` AND its `ToListItem`
projection in the module's list Query handler, sourced from the entity property that already exists. Additive only —
do NOT remove existing projected fields, do NOT touch the entity, do NOT add manager/employee UX (reserved by pack).
Reconcile with the JS: after the fix the localized column populates; if any JS column has no entity-backed field at
all, STOP and report (do not invent).

Persistence: no schema change (fields already persisted on entity).
Consistency: use ApplicantIntake/OfferManagement/CandidatePipeline/EmploymentChanges (the 4 CLEAN modules) as the reference projection shape.

Acceptance Criteria (measurable):
- Each of the 4 `ListItemDto` records includes the named field(s); `ToListItem` maps them from the entity.
- Build green (all 9 targets unaffected).
- Test (K3 red-proof): extend each module's *Tests.cs with a list-projection assertion that FAILS before the field is
  added and PASSES after (fix-absent→RED). State the red evidence in the report.
- No entity/persistence/gateway/frontend-logic/pack changes; only the 4 Models + handlers + tests.

Failure Protocol: if a JS column references a field absent from BOTH DTO and entity → STOP, report as new gap (do not invent).
Do not widen scope to the other gaps (G2/G3) — those are separate WPs.

Output Contract: §22 report + changed files + per-module before/after field list + red-test evidence. Evidence E2 (+E3 if authed). PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-REWORK-0001 · Prompt P-HCM-REWORK-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku:
1. AGENTS.md (Response<T> envelope, HCM 5059)
2. Referans (TEMİZ modüller): Features/ApplicantIntake + Features/OfferManagement — ListItemDto ↔ ToListItem deseni
3. work-packs/WP-HCM-VER-0001-... (gap G1 kanıtı)

CT-doğrulanmış kusur (metadata-only readiness list projection eksik): her modülde JS DataTable, ListItemDto'nun
project ETMEDİĞİ ama ENTITY'de VAR OLAN boundary-state alan(lar)ını istiyor → her satırda N/A. Alanlar entity'de mevcut:
  0303 EmployeeOnboarding : EmployeeActionBoundaryState
  0306 PerformanceReviews : ManagerReviewUxBoundaryState, EmployeeReviewUxBoundaryState
  0307 CompetencySkills   : ManagerAssessmentUxBoundaryState, EmployeeAssessmentUxBoundaryState
  0308 DevelopmentPlans   : GoalAssignmentBoundaryState, ManagerActionUxBoundaryState

NE:      Bu 4 modülün her biri için, yukarıdaki alan(lar)ı `*ReadinessListItemDto` record'una VE list Query
         handler'ındaki `ToListItem` projeksiyonuna EKLE; kaynağı zaten var olan entity property'si. Sadece ekleme.
NEDEN:   CT (WP-HCM-VER-0001 G1) ölçtü: localized kolon başlığı var ama satırlarda alan undefined → N/A. Alanlar
         readiness metadata; entity'de mevcut, yalnız list projeksiyonundan düşmüş.
NASIL:   TEMİZ modülleri (ApplicantIntake/OfferManagement) referans al. Mevcut project edilen alanları SİLME.
         Entity'ye DOKUNMA. Manager/employee UX EKLEME (pack tarafından reserved, yetkisiz).
YAPMA:   Entity/persistence/gateway/frontend-logic/pack değiştirme. Diğer gap'lere (G2 concurrency, G3 replay) girme.
         Bir JS kolonu hem DTO hem entity'de yoksa: DUR, yeni gap olarak raporla (uydurma).
DOĞRULA: 4 ListItemDto adlandırılan alanları içeriyor; ToListItem entity'den map ediyor; build yeşil; her modülün
         *Tests.cs'ine list-projection assertion ekle — alan eklenmeden ÖNCE RED, sonra GREEN (fix-absent→RED, K3);
         red kanıtını raporda yaz. Yalnız 4 Models + handler + test değişmiş.

Durma koşulları: DTO+entity'de olmayan JS kolonu · scope dışı ihtiyaç. Dur ve raporla.

Rapor: §22 report + değişen dosyalar + per-module before/after alan listesi + red-test kanıtı.
Senin PASS'in kapanış değildir (K13); CT bağımsız doğrular.
