WORK PACKAGE

WP ID:            WP-TEP-INS-0001
Prompt ID:        P-TEP-INS-0001
Prompt Version:   v1.0
Task Class:       inspection (read-only) — R4 precondition
Golden-Flow Profile: C
Risk Class:       MEDIUM (gates the entire R4 TEP completion wave)
State:            READY

Capability:       R4 TEP Completion precondition — verify R2 TEP MVP exists before any R4 pack/dev
Modules to verify (R2 TEP MVP, status report rows 22-36):
  MOD-0321 TEP Shell · 0322 Industry Candidate Identity · 0323 Association/Member Registry · 0324 Verified HR Access ·
  0325 Consent/Visibility/Access Policy · 0326 TEP Governance/Review Board · 0327 Industry Exit Reference Registry ·
  0328 Reference Exchange Marketplace · 0329 Rehire Recommendation · 0330 Trust Level & Multi-Signature · 0331 Candidate Response/Dispute
Sequence:         Batch 6 precondition
Build Lane:       TEP-r2-mvp-verify
Agent Lane ID:    AL-TEP-INS-MVP
Agent Lane Type:  INS
Target Agent / Entry Point: read-only-auditor / /read-only-audit

Authority:
- Priority src:  status report HR & TEP rows 22-36 (R2), 63-82 (R4 depends on R2)
- Blueprint:     verify canonical identity of 0321-0331 (likely candidate, like HCM)
- Repo-wide:     AGENTS.md (locate TEP domain/service)
- Related:       PROGRAM-2026-09-03 build plan (R4 = Batch 6, BLOCKED until this passes)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Dependencies:
- Depends on: none (read-only)
- Gate state: SATISFIED for inspection
- Parallel-safe with: all pack/INS WPs
- Integration order: BLOCKS all R4 (MOD-0332-0351) pack authoring

Scope:
- Allowed paths (READ ONLY): whole repo — locate TEP domain (execution/domains/*talent* or similar), services, registry, Blueprint
- Protected: ALL (no writes)

Objective:
Establish whether an Industry Talent Ecosystem Platform (TEP) domain/service exists in ERP-vNext, and the status
of each R2 MVP module 0321-0331 (registry/canonical? pack? code? runtime?). R4 completion modules (0332-0351)
depend entirely on this MVP; without it, R4 pack authoring is premature. Report per-module NONE/PACK-ONLY/CODE/RUNTIME
+ evidence, plus a single verdict: is R4 pack authoring unblocked? YES/NO + what must exist first.

Acceptance Criteria:
- TEP domain located (path) or confirmed ABSENT.
- 11-row status table (0321-0331) with path/port evidence.
- Verdict on R4 readiness + ordered precondition list.

Failure Protocol: no writes; no inferred status without evidence (K10).
Output Contract: §22 report + §12.2 table + R4-readiness verdict. Evidence E1 (+E3 if runtime). PASS ≠ ACCEPTED.

---

## Agent Prompt (paste-ready)

/read-only-audit
WP: WP-TEP-INS-0001 · Prompt P-TEP-INS-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Denetim modu: worktree-read-only (strict). Yazma YOK. Her bulgu için path:line veya port kanıtı. Düzeltme YOK.

Önce oku: AGENTS.md (domain/servis haritası), execution/domains/ (TEP/talent domain'i bul), execution/registries/module-id-registry.md.

NE:      ERP-vNext'te bir Industry Talent Ecosystem Platform (TEP) domain/servisi var mı ve R2 MVP modülleri
         0321-0331'in her birinin durumu ne (registry/canonical? pack? kod? runtime?) ölç. Durum = NONE/PACK-ONLY/
         CODE/RUNTIME + kanıt.
NEDEN:   R4 completion (0332-0351) tamamen bu MVP'ye bağlı; MVP yoksa R4 pack authoring erken.
NASIL:   Profile C. find/grep ile domain+servis; lsof ile runtime. Tahmin yok (K10).
YAPMA:   Hiçbir yazma; scope genişletme.
DOĞRULA: TEP domain yolu (veya ABSENT) + 11 satırlık durum tablosu + "R4 pack authoring unblocked mı? YES/NO + önce ne olmalı" verdict.

Durma koşulları: ambiguous domain ownership. Dur ve raporla.
Rapor: §22 + §12.2 tablosu + R4-readiness verdict. Senin PASS'in kapanış değildir (K13).
