WORK PACKAGE

WP ID:            WP-HCM-INS-0001
Prompt ID:        P-HCM-INS-0001
Prompt Version:   v1.0
Task Class:       inspection / audit (read-only)
Golden-Flow Profile: C
Risk Class:       HIGH (authorization gate + PII/persistence surface)
State:            READY

Capability:       CAND-CAP-0024 / 0025 / 0026 (HCM runtime-first slices)
Module:           human-capital-management (MOD-0302/0303/0304 reserved, pending-EA)
Sequence:         takeover-1
Build Lane:       HCM-reconciliation
Agent Lane ID:    AL-HCM-INS-BOUNDARY
Agent Lane Type:  INS
Target Agent / Entry Point: read-only-auditor  /  /read-only-audit

Authority:
- Identity:      execution/registries/module-id-registry.md (rows CAND-CAP-0024/0025/0026)
- Repo-wide:     AGENTS.md (§ HCM note lines 33,61-67,81 — port 5059; DCP-002 gate)
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Module Pack:   execution/domains/human-capital-management/module-packs/CAND-CAP-0024-*.md, -0025-*.md, -0026-*.md
- Related:       work-packs/TAKEOVER-2026-09-03-hcm-drift-and-decision.md (Decision CT-HCM-D-2026-09-03-01)

Repository:
- Path:          /Users/cihan/Desktop/ERP-vNext
- Branch:        hr-future
- Expected HEAD: c2e54336
- Worktree:      /Users/cihan/Desktop/ERP-vNext  (single worktree; HR work is UNTRACKED)
- Dirty baseline: 8 tracked-modified + large untracked HCM set (see takeover record)

Dependencies:
- Depends on:    none (read-only; feeds Decision D-1)
- Gate state:    SATISFIED for inspection
- Parallel-safe with: WP-HCM-INS-0002 (both read-only, no writes)
- Not parallel-safe with: any future HCM writer lane
- Integration order: precedes any 0024-0026 rework / 0027 build

Scope:
- Allowed paths (READ ONLY):
  services/Diten.HumanCapitalService/**, gateway/Diten.ApiGateway/ocelot.json,
  frontend/Diten.Web/{Controllers,Views,Resources,wwwroot/assets/js}/**/{Offer,Onboarding,EmploymentChange}*,
  execution/domains/human-capital-management/module-packs/CAND-CAP-002[4-6]*.md
- Protected paths: ALL (no writes — auditor has no write tools)
- Owned objects:  none (inspection)
- Consumed:       pack §3 owned-objects, §4 entity fields, §12 API surface, §13 data boundary

Objective:
Produce E1 (static) + E3 (runtime-freshness) evidence answering, per capability:
  (1) Does shipped code stay INSIDE the pack-authorized "metadata-only readiness" boundary, or does it
      touch the registry-reserved list (offer-letter/document gen, payroll/tax/banking, PII-heavy,
      attachment/free-text/credential persistence, candidate-facing UX, notification/doc integration)?
  (2) Is the 5059 runtime FRESH (process start > HCM Api binary timestamp, §25.1)?
  (3) Do gateway routes match each pack's §12 declared API surface (and is Offer wired at all)?

Preconditions:
- Read the three packs' §3/§4/§12/§13 and §20 waiver notes first.
- Treat pack "done" and pack self-waiver as CLAIMS, not authorization (K2/K13).

Golden / Inspection Flow:
inspect authorized boundary (pack §4/§12/§13) → grep shipped Domain/App/Persistence for reserved-list fields
→ diff gateway routes vs pack §12 → measure §25.1 freshness on 5059 → tabulate gaps with path:line evidence.

Persistence:      no writes
Consistency:      N/A

Security / Privacy:
- Flag ANY persisted field that is PII/payroll/credential/free-text/attachment (path:line).
- Do not print secrets/tokens; report presence only.

Observability:    n/a (read-only)

Conditional Gates:
- Migration: report if any Mongo collection/index created outside metadata-only scope.
- Finance precision: flag any money field not `decimal`.

Acceptance Criteria (measurable):
- Per capability: IN-BOUNDARY / OUT-OF-BOUNDARY verdict with path:line evidence for every OUT finding.
- §25.1 freshness result for pid on 5059 (process-start vs binary mtime) stated explicitly.
- Offer (0024) gateway wiring: PRESENT / ABSENT with evidence.
- Output: §12.2 inspection table + gap list. No remediation performed.

Validation (commands the auditor runs):
- ps -o lstart= -p $(lsof -nP -iTCP:5059 -sTCP:LISTEN -t | head -1)
- find services/Diten.HumanCapitalService -iname '*.Api.dll' -exec ls -l {} \;
- grep -rniE 'decimal|float|Attachment|Ssn|Iban|Salary|Token|Password|FreeText|Narrative' services/Diten.HumanCapitalService/src/.../Domain/Entities
- git diff gateway/Diten.ApiGateway/ocelot.json | grep -iE 'offer|onboard|employment'

Failure Protocol:
- stop and report on missing/ambiguous contract; do not infer intended scope
- no writes; any fix need = separate REWORK WP (not this lane)

Output Contract: §22 structured report + §12.2 table. Evidence level required: E1 (+E3 freshness). Your PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

/read-only-audit
WP: WP-HCM-INS-0001 · Prompt P-HCM-INS-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. AGENTS.md  (HCM notu: satır 33, 61-67, 81 — port 5059, DCP-002 gate)
2. execution/domains/human-capital-management/module-packs/CAND-CAP-0024-offer-management.md  (§3,§4,§12,§13,§20)
3. .../CAND-CAP-0025-employee-onboarding.md  (§3,§4,§12,§13,§20)
4. .../CAND-CAP-0026-employment-change-transfer-promotion.md  (§3,§4,§12,§13,§20)
5. execution/domains/human-capital-management/work-packs/TAKEOVER-2026-09-03-hcm-drift-and-decision.md

Denetim modu: worktree-read-only (strict). Yazma YOK. Her bulgu için `path:line` kanıtı zorunlu. Düzeltme YOK.

NE:      0024/0025/0026 shipped kodunun pack-authorized "metadata-only readiness" sınırının İÇİNDE mi
         yoksa registry-reserved listede mi olduğunu; 5059 runtime freshness'ını; ve gateway route ↔ pack §12
         uyumunu (Offer wired mı?) ölç. Kesin verdict + kanıt üret.
NEDEN:   Registry + AGENTS.md "runtime not authorized / pending DCP-002" derken kod shipped. Decision D-1'in
         E1/E3 kanıtı gerekiyor. Pack "done" ve self-waiver KANIT değil, iddiadır (K2/K13).
NASIL:   Profile C. §25.1 freshness komutu (ps lstart vs .Api.dll mtime). Domain/Application/Persistence içinde
         reserved-list alan taraması (PII/payroll/attachment/free-text/credential/money-as-float). ocelot diff.
YAPMA:   Hiçbir dosyaya yazma. Scope'u genişletme. Intended scope'u tahmin etme; eksikse dur ve raporla.
DOĞRULA: Her capability için IN/OUT-OF-BOUNDARY verdict + OUT bulguları path:line; 5059 freshness sonucu;
         Offer wiring PRESENT/ABSENT. §12.2 tablosu. Required evidence: E1 (+E3). "works" kanıt değildir.

Durma koşulları: missing/ambiguous contract · protected-path yazma ihtiyacı · scope belirsizliği.
Kapsamı kendin genişletme; dur ve raporla.

Rapor formatı: §22 structured report + §12.2 inspection table.
Senin PASS'in kapanış değildir (K13). CT bağımsız doğrular.
