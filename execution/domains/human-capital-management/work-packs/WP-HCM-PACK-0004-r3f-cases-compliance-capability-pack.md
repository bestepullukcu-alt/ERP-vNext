WORK PACKAGE

WP ID:            WP-HCM-PACK-0004
Prompt ID:        P-HCM-PACK-0004
Prompt Version:   v1.0
Task Class:       capability pack authoring (documentation; no runtime)
Golden-Flow Profile: C
Risk Class:       HIGH (grievance/disciplinary/compliance = sensitive PII + statutory)
State:            READY-pending-D2 (proposal-mode)

Capability:       R3-F Employee Relations & Compliance cluster
Modules (planning ids): MOD-0317 Employee Relations & HR Case Management · MOD-0318 HR Compliance & Statutory Reporting
Sequence:         Batch 5
Build Lane:       HCM-R3F-cases-compliance
Agent Lane ID:    AL-HCM-PACK-R3F
Agent Lane Type:  INS (authoring)
Target Agent / Entry Point: module-pack-author / /prepare-capability-pack

Authority:
- Priority src:  status report HR & TEP rows 54-55
- Blueprint:     ids ABSENT → propose
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Pattern packs: CAND-CAP-0025, CAND-CAP-0027 · governance pattern: CAND-CAP-0008 (HR governance/sensitive access)
- Repo-wide:     AGENTS.md (DCP-002 gate)
- Related:       PROGRAM-2026-09-03 build plan (D-1, D-2)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Dependencies:
- Depends on: MOD-0023 Workflow, MOD-0021 Audit, MOD-0030 Records/Retention/Legal Hold, MOD-0314 HR Governance & Sensitive Access
- Gate state: OPEN (proposal)
- Parallel-safe with: other pack/INS WPs
- Not parallel-safe with: module-id-registry writer (§16.4)

Scope:
- Allowed paths (WRITE — pack docs only): module-packs/CAND-CAP-00XX-*.md
- Propose-only: module-id-registry.md
- Protected: all code/gateway/resx/existing packs/registry

Objective:
Author 2 DRAFT packs, metadata-only readiness slice, with EXPLICIT sensitive-data handling: 0317 case management
involves grievance/disciplinary/investigation records (confidential PII, restricted visibility) — the readiness
slice must persist NO free-text narrative/attachment/PII; only case-state readiness metadata + governance links to
MOD-0314. 0318 statutory reporting consumes audit (0021) + records/retention (0030); readiness metadata only, no
report generation. Data classification per §18.1; retention/legal-hold linkage per §18.3. §20 waiver PROPOSED-pending-EA (K19).

Acceptance Criteria:
- 2 draft packs (status: draft; pending-EA); explicit confidential-data reserved list; governance linkage declared.
- No registry/code write; proposed alias+MOD in report only.

Failure Protocol: stop if governance/sensitive-access contract (0314) is missing/ambiguous (K12); deliver PROPOSAL if D-2 needed.
Output Contract: §22 report + draft-pack paths + proposed identity rows + data-classification notes. Evidence E1. PASS ≠ ACCEPTED.

---

## Agent Prompt (paste-ready)

/prepare-capability-pack
WP: WP-HCM-PACK-0004 · Prompt P-HCM-PACK-0004 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. AGENTS.md (DCP-002 gate)
2. execution/domains/human-capital-management/domain-config.md
3. execution/domains/human-capital-management/module-packs/CAND-CAP-0025-employee-onboarding.md (ŞABLON)
4. execution/domains/human-capital-management/module-packs/CAND-CAP-0008-hr-governance-sensitive-access-controls.md (governance deseni)
5. execution/domains/human-capital-management/work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md (D-1, D-2)

Kapsam — R3-F, 2 modül: Employee Relations & HR Case Management (0317), HR Compliance & Statutory Reporting (0318).

NE:      Her modül için CAND-CAP-0025 yapısında DRAFT pack, metadata-only readiness slice, AÇIK hassas-veri işleme:
         0317 (şikayet/disiplin/soruşturma) — readiness slice hiçbir free-text narrative/attachment/PII persist ETMEZ;
         yalnız case-state readiness metadata + MOD-0314 governance link. 0318 (yasal raporlama) — audit(0021)+
         records/retention(0030) tüketir; readiness metadata, rapor üretimi YOK.
NEDEN:   Kimlik yok → pack-first. Hassas/regulated alan; runtime DCP-002 kapısında.
NASIL:   Data classification §18.1; retention/legal-hold linkage §18.3. §20 waiver PROPOSED-pending-EA + K19.
YAPMA:   Confidential narrative/attachment/PII persist etme; canonical id'yi shared record'a yazma; kod/registry değiştirme.
         0314 governance contract eksikse uydurma (K12) — dur.
DOĞRULA: 2 draft pack (draft, pending-EA) + confidential-data reserved list + governance linkage + önerilen alias+MOD raporda.

Durma koşulları: eksik governance/sensitive-access contract · kimlik politikası gerekli. Dur ve raporla.
Rapor: §22 + draft-pack yolları + önerilen identity satırları + data-classification notları. PASS ≠ ACCEPTED (K13).
