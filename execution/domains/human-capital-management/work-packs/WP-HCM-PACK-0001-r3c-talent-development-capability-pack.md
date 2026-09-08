WORK PACKAGE

WP ID:            WP-HCM-PACK-0001
Prompt ID:        P-HCM-PACK-0001
Prompt Version:   v1.0
Task Class:       capability/module pack authoring (documentation; no runtime)
Golden-Flow Profile: C (no runtime writes; produces draft packs)
Risk Class:       MEDIUM (identity proposal + scope boundary authoring)
State:            READY-pending-D2   (dispatchable as PROPOSAL; finalization needs registry ratification)

Capability:       R3-C HCM Talent Development cluster
Modules (planning ids): MOD-0307 Competency & Skills Assessment · MOD-0308 Development Plan Management ·
                        MOD-0309 Learning / Training Records · MOD-0320 Succession & High-Potential Tracking
Sequence:         Batch 1 (after in-flight R3-A/B/C close)
Build Lane:       HCM-R3C-talent-development
Agent Lane ID:    AL-HCM-PACK-R3C
Agent Lane Type:  INS (authoring; no runtime writer)
Target Agent / Entry Point: module-pack-author  /  /prepare-capability-pack

Authority:
- Identity:      execution/registries/module-id-registry.md (rows must be PROPOSED, not invented — §4.1 row 1)
- Priority src:  ERPSource/Project ongoing status report 15 June 2026 (1).xlsx → HR & TEP rows 43-46
- Blueprint:     ERPSource/System Capability & Implementation Blueprint - master 5.xlsx (canonical id source; these ids ABSENT → propose)
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Pattern packs: execution/domains/human-capital-management/module-packs/CAND-CAP-0025-employee-onboarding.md,
                 CAND-CAP-0027-performance-review-management.md  (structure + metadata-only readiness template)
- Repo-wide:     AGENTS.md (§ HCM note: runtime pending DCP-002; port 5059; §3 port schema)
- Related:       work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md (Decisions D-1, D-2)

Repository:
- Path:          /Users/cihan/Desktop/ERP-vNext
- Branch:        hr-future
- Expected HEAD: c2e54336
- Worktree:      /Users/cihan/Desktop/ERP-vNext (single worktree)
- Dirty baseline: in-flight HCM untracked set (do not touch)

Dependencies:
- Depends on:    D-2 (identity/alias policy) for FINAL pack status; may proceed as PROPOSAL now
- Gate state:    OPEN (proposal-mode allowed; approved-status blocked on D-2)
- Parallel-safe with: WP-HCM-PACK-0002 (R3-D), analytics-backbone INS (all doc/read work)
- Not parallel-safe with: any writer touching module-id-registry.md (single-writer identity seam §16.4)
- Integration order: precedes R3-C implementation WPs

Scope:
- Allowed paths (WRITE — pack docs only):
  execution/domains/human-capital-management/module-packs/CAND-CAP-00XX-*.md  (new draft packs)
- Propose-only (NO direct write): execution/registries/module-id-registry.md  (registry gate ratifies)
- Protected paths: ALL source code, gateway, resx, existing packs, registry (propose rows in report, do not commit)
- Owned objects: draft pack documents

Objective:
Author 4 DRAFT module packs (one per module, cross-referenced as the R3-C capability) following the exact
structure of CAND-CAP-0025/0027, each scoped to a **metadata-only readiness first slice** (no runtime workflow,
no PII/attachment/free-text/credential persistence, no candidate/employee-facing UX) consistent with the
established 0300-series pattern and the AGENTS.md DCP-002 gate. Propose CAND-CAP aliases + reserved-MOD mapping
for registry ratification; do NOT finalize canonical identity or write registry rows.

Each pack must contain (per template): Module Summary · Ownership & Boundaries · Owned Objects · Entity Fields
(metadata-only, decimal-not-float for any measure) · Repo Scope · Protected Paths · Dependencies (explicit:
0307→0308/0320, 0309→0308/0320; foundation MOD-0288/0299/0298) · Runtime Constraints · Layout & Shell Contract ·
Backend File Convention · Frontend File Contract · API Surface Contract (metadata-only endpoints) · Data Boundary ·
§20 Notes/Waivers (state waiver as PROPOSED-pending-EA, with K19 fields: owner, compensating control, expiry, exit).

Persistence:      no runtime writes (documents only)
Consistency:      N/A

Acceptance Criteria (measurable):
- 4 draft packs created, `status: draft`, `canonicalization_status: candidate / pending-EA`.
- Each declares metadata-only readiness scope + explicit reserved list (what is NOT authorized).
- Dependency graph across the 4 modules declared and acyclic.
- Proposed CAND-CAP alias + MOD mapping listed in the report for registry ratification (NOT written to registry).
- No source/gateway/resx/registry file modified.

Failure Protocol:
- stop if identity policy (D-2) is required to finalize — deliver as PROPOSAL and report
- do not invent canonical MOD ids into any shared record; do not author runtime scope
- do not touch existing packs or code

Output Contract: §22 structured report + list of created draft-pack paths + proposed identity rows. Evidence: E1.
Your PASS ≠ CT ACCEPTED (K13); packs are DRAFT until CT + registry gate ratify.

---

## Agent Prompt (paste-ready)

/prepare-capability-pack
WP: WP-HCM-PACK-0001 · Prompt P-HCM-PACK-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. AGENTS.md  (HCM notu: runtime DCP-002 gate, port 5059, §3 port şeması)
2. execution/domains/human-capital-management/domain-config.md
3. execution/domains/human-capital-management/module-packs/CAND-CAP-0025-employee-onboarding.md  (YAPI ŞABLONU)
4. execution/domains/human-capital-management/module-packs/CAND-CAP-0027-performance-review-management.md
5. execution/domains/human-capital-management/work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md  (D-1, D-2)

Kapsam — R3-C Talent Development capability, 4 modül (status report HR&TEP satır 43-46):
- Competency & Skills Assessment   (planning id MOD-0307)
- Development Plan Management       (planning id MOD-0308)
- Learning / Training Records       (planning id MOD-0309)
- Succession & High-Potential Tracking (planning id MOD-0320)

NE:      Her modül için CAND-CAP-0025/0027 yapısında bir DRAFT module pack yaz. Her pack "metadata-only readiness
         first slice" ile sınırlı olsun (runtime workflow YOK; PII/attachment/free-text/credential/rating narrative
         persistence YOK; employee/manager-facing UX YOK). Dört pack'i tek R3-C capability olarak çapraz referansla.
NEDEN:   Bu 4 modülün ne Blueprint ne registry kimliği var; dev'e geçmeden önce pack-first pipeline (0300-serisi
         deseni) gerekiyor. Runtime AGENTS.md DCP-002 kapısında; pack draft, EA sonra onaylar.
NASIL:   Şablonun tüm bölümleri. Entity Fields metadata-only (ölçüm alanı varsa decimal, float değil). Bağımlılıkları
         AÇIKÇA yaz: 0307→0308/0320, 0309→0308/0320; foundation MOD-0288/0299/0298. §20'de waiver'ı PROPOSED-pending-EA
         olarak, K19 alanlarıyla (owner, compensating control, expiry/review, exit) yaz.
YAPMA:   Canonical MOD id'yi HİÇBİR shared record'a yazma. module-id-registry.md'yi DEĞİŞTİRME — alias+MOD eşlemesini
         yalnızca RAPORDA öner (registry gate onaylar). Kaynak kod / gateway / resx / mevcut pack'lere DOKUNMA.
         Runtime scope yazma. Kimlik politikası (D-2) gerekiyorsa dur, PROPOSAL olarak teslim et.
DOĞRULA: 4 draft pack (status: draft, canonicalization_status: candidate / pending-EA); her biri metadata-only scope
         + "not authorized" reserved liste; acyclic dependency grafiği; önerilen CAND-CAP alias+MOD eşlemesi raporda.
         Hiçbir kod/gateway/resx/registry dosyası değişmemiş.

Durma koşulları: kimlik politikası kararı gerekli · shared record'a canonical id yazma ihtiyacı · scope belirsizliği.
Kapsamı kendin genişletme; dur ve raporla.

Rapor formatı: §22 structured report + oluşturulan draft-pack yolları + önerilen identity satırları.
Senin PASS'in kapanış değildir (K13); pack'ler CT + registry gate onaylayana dek DRAFT'tır.
