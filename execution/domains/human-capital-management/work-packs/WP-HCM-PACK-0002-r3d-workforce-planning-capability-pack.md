WORK PACKAGE

WP ID:            WP-HCM-PACK-0002
Prompt ID:        P-HCM-PACK-0002
Prompt Version:   v1.0
Task Class:       capability pack authoring (documentation; no runtime)
Golden-Flow Profile: C
Risk Class:       MEDIUM
State:            READY-pending-D2 (proposal-mode dispatchable)

Capability:       R3-D Workforce Planning cluster
Modules (planning ids): MOD-0310 Workforce Planning · MOD-0311 Headcount & Position Budget Planning
Sequence:         Batch 2
Build Lane:       HCM-R3D-workforce-planning
Agent Lane ID:    AL-HCM-PACK-R3D
Agent Lane Type:  INS (authoring)
Target Agent / Entry Point: module-pack-author / /prepare-capability-pack

Authority:
- Priority src:  ERPSource status report → HR & TEP rows 47-48
- Blueprint:     ids ABSENT → propose (do not invent canonical rows)
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Pattern packs: CAND-CAP-0025, CAND-CAP-0027
- Repo-wide:     AGENTS.md (DCP-002 gate; port 5059)
- Related:       work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md (D-1, D-2)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Dependencies:
- Depends on:    R3-C competency (0307) for skills-supply input; position/org foundation MOD-0288/0299
- Gate state:    OPEN (proposal-mode)
- Parallel-safe with: WP-HCM-PACK-0001, WP-HCM-INS-0003 (all doc/read)
- Not parallel-safe with: any module-id-registry writer (§16.4)
- Integration order: after R3-C packs

Scope:
- Allowed paths (WRITE — pack docs only): execution/domains/human-capital-management/module-packs/CAND-CAP-00XX-*.md
- Propose-only: module-id-registry.md
- Protected: all code/gateway/resx/existing packs/registry

Objective:
Author 2 DRAFT packs (0310, 0311) in the CAND-CAP-0025/0027 structure, metadata-only readiness first slice.
Declare deps explicitly: 0310→(0307 competency, 0299 position, 0311 headcount); 0311→(0299/0288 position, external
finance/budget source). Money fields decimal-not-float (§18.8). §20 waiver PROPOSED-pending-EA with K19 fields.

Acceptance Criteria:
- 2 draft packs (status: draft; candidacy pending-EA), metadata-only scope + reserved list, acyclic deps.
- Proposed alias+MOD mapping in report only; no registry/code write.

Failure Protocol: stop if D-2 needed to finalize (deliver PROPOSAL); no canonical id into shared records; no runtime scope.
Output Contract: §22 report + draft-pack paths + proposed identity rows. Evidence E1. PASS ≠ ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

/prepare-capability-pack
WP: WP-HCM-PACK-0002 · Prompt P-HCM-PACK-0002 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. AGENTS.md (HCM DCP-002 gate, port 5059)
2. execution/domains/human-capital-management/domain-config.md
3. execution/domains/human-capital-management/module-packs/CAND-CAP-0025-employee-onboarding.md (ŞABLON)
4. execution/domains/human-capital-management/work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md (D-1, D-2)

Kapsam — R3-D Workforce Planning, 2 modül: Workforce Planning (MOD-0310), Headcount & Position Budget (MOD-0311).

NE:      Her modül için CAND-CAP-0025 yapısında DRAFT pack, metadata-only readiness slice (runtime planning
         hesaplama/optimizasyon YOK; sadece readiness metadata + reserved list).
NEDEN:   Blueprint/registry kimliği yok; pack-first. Runtime DCP-002 kapısında.
NASIL:   Bağımlılıklar açık: 0310→(0307,0299,0311), 0311→(0299/0288, dış finans/bütçe kaynağı). Para alanları decimal.
         §20 waiver PROPOSED-pending-EA, K19 alanlarıyla.
YAPMA:   Canonical id'yi shared record'a yazma; registry/kod/gateway/resx değiştirme; runtime scope yazma.
DOĞRULA: 2 draft pack (draft, pending-EA), metadata-only + reserved list, acyclic dep, önerilen alias+MOD raporda.

Durma koşulları: kimlik politikası gerekli · shared record'a canonical id ihtiyacı. Dur ve raporla.
Rapor: §22 + draft-pack yolları + önerilen identity satırları. Senin PASS'in kapanış değildir (K13).
