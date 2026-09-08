WORK PACKAGE

WP ID:            WP-HCM-INS-0003
Prompt ID:        P-HCM-INS-0003
Prompt Version:   v1.0
Task Class:       inspection (read-only) — external dependency status
Golden-Flow Profile: C
Risk Class:       MEDIUM (gates HR KPI façade + R4-D analytics)
State:            READY

Capability:       R3-G Analytics backbone (external domains) — dependency verification for HR façade
Modules:          MOD-0004 Metric & Semantic Registry · MOD-0059 KPI Catalog · MOD-0060 Metric Definitions &
                  Ownership · MOD-0061 Scorecards/Dashboards · MOD-0062 Baseline & Experiment Measurement ·
                  MOD-0063 Data Warehouse/Lakehouse · MOD-0064 ETL/ELT Pipelines
Sequence:         Batch 3 (parallel with pack authoring)
Build Lane:       analytics-backbone-verify
Agent Lane ID:    AL-ANALYTICS-INS-STATUS
Agent Lane Type:  INS
Target Agent / Entry Point: read-only-auditor / /read-only-audit

Authority:
- Blueprint:     these 7 ARE canonical (Blueprint_Data: domains "Enterprise Control Points" / "Data, Knowledge & Intelligence", Waves W-2/W-3/W-4)
- Priority src:  status report HR & TEP rows 56-62 (Type: Analytics backbone; consumed by HR KPI façade 0312)
- Repo-wide:     AGENTS.md (service/domain map)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Dependencies:
- Depends on: none (read-only)
- Gate state: SATISFIED for inspection
- Parallel-safe with: all pack-authoring WPs (no writes)
- Integration order: BLOCKS WP for MOD-0312 (HR KPI façade) and R4-D analytics packs

Scope:
- Allowed paths (READ ONLY): whole repo (locate data-plane services/domains), AGENTS.md, execution/domains/**, execution/registries/**
- Protected: ALL (no writes)

Objective:
Determine, for each of the 7 analytics-backbone modules, whether it exists in ERP-vNext as (a) a registry/canonical
row, (b) a module pack, (c) a service/code surface, (d) a running runtime. HR's MOD-0312 façade cannot declare a
data contract against modules that are not built. Report per-module: NONE / PACK-ONLY / CODE / RUNTIME + evidence
(path or port). State explicitly whether these live in a separate domain (data/analytics) outside HCM ownership.

Acceptance Criteria:
- Per-module status table (7 rows) with path:line / port evidence.
- Explicit verdict: is the analytics data contract available for HR façade 0312 to consume? YES/NO/PARTIAL.
- Owning domain identified for each (HCM must NOT build these — façade only).

Failure Protocol: no writes; report gaps; do not infer build status without file/port evidence (K10).
Output Contract: §22 report + §12.2-style status table. Evidence E1 (+E3 if runtime found). PASS ≠ ACCEPTED.

---

## Agent Prompt (paste-ready)

/read-only-audit
WP: WP-HCM-INS-0003 · Prompt P-HCM-INS-0003 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Denetim modu: worktree-read-only (strict). Yazma YOK. Her bulgu için path:line veya port kanıtı. Düzeltme YOK.

Önce oku: AGENTS.md (servis/domain haritası), execution/registries/module-id-registry.md, execution/domains/ (data/analytics domain'i bul).

NE:      Şu 7 analytics-backbone modülünün her biri ERP-vNext'te var mı ölç: MOD-0004, 0059, 0060, 0061, 0062,
         0063, 0064. Her biri için: registry/canonical satır? module pack? servis/kod? çalışan runtime? Durum =
         NONE / PACK-ONLY / CODE / RUNTIME + kanıt. Sahip domain'i belirle (bunlar HCM'e ait DEĞİL; HR yalnız façade yapar).
NEDEN:   HR KPI façade MOD-0312 bu modüllere karşı data contract kuracak; kurulmamışsa façade pack'i blocked.
NASIL:   Profile C. grep/find ile servis ve domain arama; lsof ile runtime. Tahmin yok (K10) — kanıtsız "var" yazma.
YAPMA:   Hiçbir yazma. Scope genişletme.
DOĞRULA: 7 satırlık durum tablosu + "0312 façade için analytics data contract mevcut mu? YES/NO/PARTIAL" ifadesi.

Durma koşulları: ambiguous domain ownership. Dur ve raporla.
Rapor: §22 + §12.2 tablosu. Senin PASS'in kapanış değildir (K13).
