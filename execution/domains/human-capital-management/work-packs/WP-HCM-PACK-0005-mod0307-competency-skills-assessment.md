WORK PACKAGE

WP ID:            WP-HCM-PACK-0005
Prompt ID:        P-HCM-PACK-0005
Prompt Version:   v1.0
Task Class:       module pack authoring (single module; documentation, no runtime)
Golden-Flow Profile: C
Risk Class:       MEDIUM
State:            READY

Capability:       R3-C Talent Development — module 1 of 4 (per-module cadence, matches 0022-0027 pattern)
Module (planning id): MOD-0307 Competency & Skills Assessment  → proposed alias CAND-CAP-0028
Sequence:         next after CAND-CAP-0027 (Performance Review, slice shipped)
Build Lane:       HCM-R3C-competency
Agent Lane ID:    AL-HCM-PACK-0307
Agent Lane Type:  INS (authoring)
Target Agent / Entry Point: module-pack-author / /prepare-module-pack

Authority:
- Priority src:  ERPSource status report → HR & TEP row 43 (R3-C, MOD-0307)
- Blueprint:     id ABSENT → propose (no invented canonical row)
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Pattern pack:  execution/domains/human-capital-management/module-packs/CAND-CAP-0027-performance-review-management.md (FRESHEST template)
- Impl template: the shipped PerformanceReviews slice (Domain/Application/Persistence/Api/frontend/tests)
- Repo-wide:     AGENTS.md (HCM DCP-002 gate; port 5059; §3 port schema)
- Related:       work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md (Batch 1)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
- Dirty baseline: in-flight untracked HCM slices (Offer/Onboarding/EmploymentChange/PerformanceReview) — DO NOT TOUCH

Dependencies:
- Depends on:    HR foundation (MOD-0288 directory, MOD-0298 employee projection, MOD-0299 position, MOD-0314 governance); MOD-0057 Taxonomy (skills/roles) as skill vocabulary source
- Gate state:    OPEN (proposal-mode; final approved status pending D-2 identity ratification)
- Parallel-safe with: other pack-authoring WPs
- Not parallel-safe with: module-id-registry.md writer (§16.4 single-writer identity seam)
- Integration order: pack → then dev slice (@orchestrator /add-module) → feeds 0308 Dev Plan + 0320 Succession

Scope:
- Allowed paths (WRITE — pack doc only): execution/domains/human-capital-management/module-packs/CAND-CAP-0028-competency-skills-assessment.md
- Propose-only: module-id-registry.md (row proposed in report; NOT written)
- Protected: ALL code/gateway/resx/existing packs/registry

Objective:
Author ONE draft module pack for Competency & Skills Assessment, structurally identical to CAND-CAP-0027, scoped
to a metadata-only readiness first slice consistent with the shipped 0024-0027 pattern. Owned readiness objects:
CompetencyAssessmentReadinessMetadata (+ ReadinessState enum, repository interface, Mongo repository). Metadata-only:
NO rating/scoring narrative, NO free-text appraisal, NO PII-heavy or attachment persistence, NO employee/manager-facing
assessment UX. Skill vocabulary referenced via MOD-0057 Taxonomy (do not duplicate). Declare downstream consumers
(0308 Development Plan, 0320 Succession). §20 waiver written PROPOSED-pending-EA with FULL K19 fields
(owner/approver, compensating control, expiry/review, exit condition) — closing the K19 gap seen in 0024-0027.

Acceptance Criteria (measurable):
- CAND-CAP-0028 draft pack created: status: draft; canonicalization_status: candidate / pending-EA.
- Sections mirror CAND-CAP-0027 (Summary, Ownership, Owned Objects, Entity Fields [metadata-only], Repo Scope,
  Protected Paths, Dependencies, Runtime Constraints, Layout/Shell, Backend/Frontend/API conventions, Data Boundary, §20 waivers).
- Explicit reserved list (what is NOT authorized in the slice).
- §20 waiver carries all K19 fields.
- Proposed registry row (CAND-CAP-0028 → reserved MOD-0307) in the report ONLY.
- No code/gateway/resx/registry/existing-pack modified.

Failure Protocol: stop if D-2 identity policy is required to finalize (deliver as PROPOSAL); do not invent canonical
MOD id into any shared record; do not author runtime scope; do not touch the shipped slices.

Output Contract: §22 report + created pack path + proposed identity row + declared dependency/consumer graph. Evidence E1. PASS ≠ CT ACCEPTED (K13).

Next WP (queued): WP-HCM-DEV-0307 — @orchestrator /add-module, builds the metadata-only slice from this pack once drafted (mirrors PerformanceReviews footprint: Domain entity/enum/repo, Persistence Mongo repo + DI, Application Features, Api controller, gateway ocelot route→5059, frontend controller/views/js, SharedResource l10n, tests).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-PACK-0005 · Prompt P-HCM-PACK-0005 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. execution/domains/human-capital-management/module-packs/CAND-CAP-0027-performance-review-management.md  ← BİREBİR YAPI ŞABLONU
2. AGENTS.md  (HCM notu: runtime DCP-002 gate, port 5059, §3 port şeması)
3. execution/domains/human-capital-management/domain-config.md
4. execution/domains/human-capital-management/work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md  (D-1, D-2, Batch 1)

Modül: Competency & Skills Assessment · planning id MOD-0307 · önerilen alias CAND-CAP-0028 · domain human-capital-management · servis Diten.HumanCapitalService (port 5059).

NE:      CAND-CAP-0027 ile yapısal olarak AYNI tek bir DRAFT module pack yaz:
         execution/domains/human-capital-management/module-packs/CAND-CAP-0028-competency-skills-assessment.md
         Kapsam = metadata-only readiness first slice (shipped 0024-0027 deseniyle birebir tutarlı).
         Owned objects: CompetencyAssessmentReadinessMetadata (+ ReadinessState enum, repository interface, Mongo repository).
NEDEN:   R3-C sıradaki modül; Performance Review (CAND-CAP-0027) slice tamamlandı, kaldığımız yer burası. Kimlik
         Blueprint/registry'de yok → pack-first. Runtime AGENTS.md DCP-002 kapısında; pack draft, EA sonra onaylar.
NASIL:   Bölümler CAND-CAP-0027'yi izlesin (Summary, Ownership & Boundaries, Owned Objects, Entity Fields [metadata-only],
         Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Layout & Shell, Backend/Frontend/API convention,
         Data Boundary, §20 Notes/Waivers). Bağımlılıklar açık: foundation MOD-0288/0298/0299/0314; skill sözlüğü
         MOD-0057 Taxonomy'den (KOPYALAMA, referans ver). Downstream tüketici: 0308 Development Plan, 0320 Succession.
         §20 waiver'ı PROPOSED-pending-EA olarak TAM K19 alanlarıyla yaz (owner/approver, compensating control,
         expiry/review, exit condition) — 0024-0027'deki eksik K19'u burada kapat.
YAPMA:   rating/scoring narrative, free-text appraisal, PII-heavy veya attachment persistence, employee/manager-facing
         assessment UX YOK. Canonical MOD id'yi HİÇBİR shared record'a yazma. module-id-registry.md'yi DEĞİŞTİRME —
         satırı yalnız raporda öner. Kaynak kod / gateway / resx / mevcut pack / shipped slice'lara DOKUNMA.
DOĞRULA: CAND-CAP-0028 draft pack (status: draft, canonicalization_status: candidate / pending-EA); metadata-only scope +
         reserved list; §20 waiver K19-tam; önerilen registry satırı (CAND-CAP-0028 → MOD-0307) raporda; hiçbir kod/registry
         değişmemiş; dependency/consumer grafiği yazılı.

Durma koşulları: kimlik politikası (D-2) finalize için gerekli · shared record'a canonical id yazma ihtiyacı · scope belirsizliği.
Kapsamı kendin genişletme; dur ve raporla.

Rapor formatı: §22 structured report + oluşturulan pack yolu + önerilen identity satırı + dependency/consumer grafiği.
Senin PASS'in kapanış değildir (K13); pack CT + registry gate onaylayana dek DRAFT'tır.
