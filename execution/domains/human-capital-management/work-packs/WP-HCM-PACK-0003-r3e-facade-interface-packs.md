WORK PACKAGE

WP ID:            WP-HCM-PACK-0003
Prompt ID:        P-HCM-PACK-0003
Prompt Version:   v1.0
Task Class:       module pack authoring ×5 (documentation; no runtime)
Golden-Flow Profile: C
Risk Class:       MEDIUM
State:            READY-pending-D2 (proposal-mode; 0312 pack BLOCKED on WP-HCM-INS-0003)

Capability:       R3-E HCM Facade / Interface / Channel cluster
Modules (planning ids):
  MOD-0312 HR KPI & Analytics Facade      (dep: analytics backbone 0004/0059-0064 — verify WP-HCM-INS-0003)
  MOD-0313 HR Documentation & Evidence Workspace (dep: MOD-0028 Documentation&Evidence, MOD-0031 Evidence Linking)
  MOD-0315 Time, Attendance & Leave Interface     (dep: external MOD-0280 Time & Attendance)
  MOD-0316 Compensation & Benefits Interface      (dep: external MOD-0279 Payroll, MOD-0281 Payroll Integration)
  MOD-0319 Employee / Manager Self-Service Channel (dep: broad — 0298/0299/0307-0320; BUILD LAST)
Sequence:         Batch 4
Build Lane:       HCM-R3E-facade-interface
Agent Lane ID:    AL-HCM-PACK-R3E
Agent Lane Type:  INS (authoring)
Target Agent / Entry Point: module-pack-author / /prepare-module-pack (5 distinct packs — heterogeneous SoRs)

Authority:
- Priority src:  status report HR & TEP rows 49-53
- Blueprint:     ids ABSENT → propose
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Pattern packs: CAND-CAP-0025, CAND-CAP-0027
- Repo-wide:     AGENTS.md (DCP-002 gate; §3 port schema — interfaces are CONSUMERS of external sources via gateway, never direct)
- Related:       PROGRAM-2026-09-03 build plan (D-1, D-2); WP-HCM-INS-0003 (analytics readiness gate for 0312)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Dependencies:
- Depends on: WP-HCM-INS-0003 for MOD-0312 data contract; R0/R1 backbone (0028/0031/0280/0279/0281) declared as consumed
- Gate state: OPEN (proposal); 0312 pack = CONTRACT-BLOCKED until INS-0003 confirms analytics availability (K12)
- Parallel-safe with: PACK-0001/0002, INS-0003 (doc/read)
- Not parallel-safe with: module-id-registry writer (§16.4)

Scope:
- Allowed paths (WRITE — pack docs only): execution/domains/human-capital-management/module-packs/CAND-CAP-00XX-*.md
- Propose-only: module-id-registry.md
- Protected: all code/gateway/resx/existing packs/registry

Objective:
Author 5 DRAFT packs, metadata-only readiness first slice. These are FACADE/INTERFACE modules — they CONSUME
external SoRs (analytics, T&A, payroll) via gateway; they do NOT persist source-of-truth payroll/attendance/PII.
Each pack must: declare the consumed contract + owner; state that raw source payloads are NOT persisted (readiness
metadata only); for 0312, mark the analytics data contract as a precondition gated on WP-HCM-INS-0003; for 0319,
declare it integrates all prior R1-R3 surfaces and is sequenced LAST. §20 waiver PROPOSED-pending-EA (K19 fields).

Acceptance Criteria:
- 5 draft packs (status: draft; pending-EA), each with consumed-contract declaration + reserved list.
- 0312 pack explicitly CONTRACT-BLOCKED pending analytics availability (INS-0003).
- No registry/code/gateway/resx write; proposed alias+MOD mapping in report only.

Failure Protocol: stop if a consumed contract is missing/ambiguous (K12 — do not invent it); deliver PROPOSAL if D-2 needed.
Output Contract: §22 report + 5 draft-pack paths + proposed identity rows + per-module dependency/contract status. Evidence E1. PASS ≠ ACCEPTED.

---

## Agent Prompt (paste-ready)

/prepare-module-pack
WP: WP-HCM-PACK-0003 · Prompt P-HCM-PACK-0003 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. AGENTS.md (DCP-002 gate; §3 port şeması — interface'ler dış kaynağı gateway üzerinden TÜKETİR, direkt değil)
2. execution/domains/human-capital-management/domain-config.md
3. execution/domains/human-capital-management/module-packs/CAND-CAP-0025-employee-onboarding.md (ŞABLON)
4. execution/domains/human-capital-management/work-packs/PROGRAM-2026-09-03-hr-tep-r3r4-buildplan.md (D-1, D-2)
5. execution/domains/human-capital-management/work-packs/WP-HCM-INS-0003-analytics-backbone-status-audit.md (0312 kapısı)

Kapsam — R3-E, 5 modül (façade/interface/channel), status report satır 49-53:
- HR KPI & Analytics Facade (0312) · HR Documentation & Evidence Workspace (0313) · Time/Attendance/Leave Interface (0315)
- Compensation & Benefits Interface (0316) · Employee/Manager Self-Service Channel (0319, EN SON)

NE:      Her modül için CAND-CAP-0025 yapısında DRAFT pack, metadata-only readiness slice. Bunlar FAÇADE/INTERFACE:
         dış SoR'u (analytics, T&A, payroll) gateway üzerinden TÜKETİR; kaynak payroll/attendance/PII payload'unu
         PERSIST ETMEZ (yalnız readiness metadata). Her pack tüketilen contract + owner'ı beyan etsin.
NEDEN:   Kimlik yok → pack-first. Interface'ler mevcut kaynaklara bağlı; contract eksikse blocked (K12).
NASIL:   0312 için analytics data contract'ı WP-HCM-INS-0003'e bağlı ön koşul olarak işaretle (CONTRACT-BLOCKED).
         0315→0280, 0316→0279/0281, 0313→0028/0031 tüketilen contract'larını yaz. 0319'u EN SON sırala, tüm R1-R3
         yüzeylerini entegre ettiğini belirt. Para alanı decimal. §20 waiver PROPOSED-pending-EA + K19.
YAPMA:   Kaynak payload persist etme; canonical id'yi shared record'a yazma; registry/kod/gateway/resx değiştirme.
         Eksik tüketilen contract'ı uydurma (K12) — dur ve raporla.
DOĞRULA: 5 draft pack (draft, pending-EA) + her birinde consumed-contract beyanı + reserved list; 0312 CONTRACT-BLOCKED;
         önerilen alias+MOD raporda; hiçbir kod/registry değişmemiş.

Durma koşulları: eksik/ambiguous consumed contract · kimlik politikası gerekli. Dur ve raporla.
Rapor: §22 + 5 draft-pack yolu + önerilen identity satırları + per-module contract durumu. PASS ≠ ACCEPTED (K13).
