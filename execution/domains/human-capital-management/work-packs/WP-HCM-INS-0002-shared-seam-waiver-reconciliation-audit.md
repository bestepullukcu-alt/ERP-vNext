WORK PACKAGE

WP ID:            WP-HCM-INS-0002
Prompt ID:        P-HCM-INS-0002
Prompt Version:   v1.0
Task Class:       inspection / reconciliation (read-only)
Golden-Flow Profile: C
Risk Class:       HIGH (shared seams: gateway/identity/l10n/DI/shell + waiver governance)
State:            READY

Capability:       CAND-CAP-0024 / 0025 / 0026 / 0027 (HCM shared seams + waiver provenance)
Module:           human-capital-management
Sequence:         takeover-2
Build Lane:       HCM-reconciliation
Agent Lane ID:    AL-HCM-INS-SEAM
Agent Lane Type:  INS
Target Agent / Entry Point: read-only-auditor  /  /read-only-audit

Authority:
- Identity:      execution/registries/module-id-registry.md
- Repo-wide:     AGENTS.md (§3 port schema line 81, 85; git rules)
- Domain Config: execution/domains/human-capital-management/domain-config.md
- Module Pack:   CAND-CAP-0024/0025/0026/0027 §20 (Notes, Waivers, Follow-Up)
- Related:       work-packs/TAKEOVER-2026-09-03-hcm-drift-and-decision.md (Decision CT-HCM-D-2026-09-03-01)

Repository:
- Path:          /Users/cihan/Desktop/ERP-vNext
- Branch:        hr-future
- Expected HEAD: c2e54336
- Worktree:      /Users/cihan/Desktop/ERP-vNext  (single worktree)
- Dirty baseline: shared seams modified (ocelot +80, DI +2, _LayoutTenantShell +32, SharedResource +3/+3, registry +3)

Dependencies:
- Depends on:    none (read-only; feeds Decision D-1 + parallel-safety gate)
- Gate state:    SATISFIED for inspection
- Parallel-safe with: WP-HCM-INS-0001 (both read-only)
- Not parallel-safe with: any future HCM writer lane on these seams
- Integration order: precedes opening any parallel HCM writer lane

Scope:
- Allowed paths (READ ONLY):
  gateway/Diten.ApiGateway/ocelot.json, services/.../Persistence/DependencyInjection.cs,
  frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml, frontend/Diten.Web/Resources/SharedResource.*.resx,
  execution/registries/module-id-registry.md, module-packs/CAND-CAP-002[4-7]*.md §20, AGENTS.md
- Protected paths: ALL (no writes)
- Owned objects: none

Objective:
Two reconciliation outputs:
  (A) SHARED-SEAM integrity: enumerate every seam write by the in-flight HR work; confirm §16.4 single-writer
      integrity, gateway upstream/downstream + tenant-header (X-Tenant-Id / Authorization) passing matches
      AGENTS.md §3 port schema (all traffic via 5000→5059, never direct), l10n keys land in correct resx,
      DI registrations match declared repositories, registry rows have no duplicate MOD ownership.
  (B) WAIVER provenance: extract each pack §20 waiver; check K19 completeness (owner/approver, compensating
      control, expiry/review, exit condition); confirm whether a canonical waiver register / decision record
      exists anywhere; tabulate the registry(`pending-EA`,"not authorized") vs pack(`done`,`EA-waived`) contradiction.

Preconditions:
- Waiver text in a module pack is a CLAIM; the §5.1 SoR for an exception is the waiver register (K11/K19).

Inspection Flow:
list seam diffs → cross-check each vs authority (ocelot↔AGENTS port schema; resx↔module vs SharedResource;
DI↔Domain repo interfaces; registry↔duplicate ownership) → extract pack §20 waivers → K19 completeness table
→ confirm waiver-register presence/absence → gap list with path:line.

Persistence:      no writes
Consistency:      N/A

Security / Privacy:
- Confirm gateway routes carry auth/tenant headers; flag any HCM route missing tenant isolation.

Conditional Gates:
- Deprecation/migration: flag any registry row that reassigns an already-owned MOD id.

Acceptance Criteria (measurable):
- Seam table: per seam (ocelot, DI, shell, resx-en, resx-tr, registry) — writer, authority match Y/N, path:line.
- Gateway: every HCM upstream route maps to 5059 via 5000, with tenant/auth header — PASS/FAIL per route.
- Waiver table: per capability — waiver text, K19 fields present/absent, register location or "NONE".
- Explicit statement: does a canonical waiver/decision register exist? (yes+path / no).
- Output: §12.2-style tables + gap list. No remediation.

Validation (commands the auditor runs):
- git diff gateway/Diten.ApiGateway/ocelot.json ; grep -nE 'X-Tenant-Id|Authorization|Port|Upstream|Downstream' (added lines)
- git diff services/.../Persistence/DependencyInjection.cs
- git diff frontend/Diten.Web/Resources/SharedResource.en.resx SharedResource.tr.resx
- grep -nE 'MOD-030[2-6]' execution/registries/module-id-registry.md   # duplicate ownership check
- find execution docs -iname '*waiver*' -o -iname '*decision*'          # register presence

Failure Protocol:
- stop and report on ambiguous authority; no writes; any fix = separate REWORK WP.

Output Contract: §22 structured report + reconciliation tables. Evidence level required: E1. Your PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

/read-only-audit
WP: WP-HCM-INS-0002 · Prompt P-HCM-INS-0002 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext

Önce oku (sırayla):
1. AGENTS.md  (§3 port şeması: satır 81, 85 — 5000→5059, direct çağrı yasak; git kuralları)
2. execution/registries/module-id-registry.md  (CAND-CAP-0024..0027 satırları, MOD-0302/0303/0304/0306)
3. module-packs/CAND-CAP-0024/0025/0026/0027 — yalnız §20 (Notes, Waivers, Future Follow-Up)
4. execution/domains/human-capital-management/work-packs/TAKEOVER-2026-09-03-hcm-drift-and-decision.md

Denetim modu: worktree-read-only (strict). Yazma YOK. Her bulgu için `path:line` kanıtı. Düzeltme YOK.

NE:      (A) In-flight HR işinin dokunduğu her shared seam'i (ocelot, DI, _LayoutTenantShell, SharedResource
         en/tr, module-id-registry) listele ve authority ile karşılaştır: gateway route'ları AGENTS.md port
         şemasına (5000→5059, tenant/auth header) uyuyor mu; l10n key doğru resx'e mi; DI kayıtları Domain repo
         arayüzleriyle eşleşiyor mu; registry'de duplicate MOD ownership var mı.
         (B) Her pack §20 waiver'ını çıkar; K19 (owner/approver, compensating control, expiry/review, exit)
         tam mı; canonical waiver/decision register VAR MI (yoksa "NONE"); registry(pending-EA,"not authorized")
         vs pack(done,EA-waived) çelişkisini tablola.
NEDEN:   §16.4 single-writer + §4.2 fail-closed + Decision D-1 için seam integrity ve waiver provenance kanıtı
         gerekiyor. Pack içindeki waiver metni iddiadır; §5.1 SoR waiver register'dır (K11/K19).
NASIL:   Profile C. git diff + grep ile seam-by-seam. Waiver'lar için K19 completeness tablosu. Register aramasi.
YAPMA:   Yazma yok. Scope genişletme yok. Waiver'ı "geçerli" varsayma; alan eksikse eksik yaz.
DOĞRULA: Seam tablosu (writer, authority-match Y/N, path:line) + gateway PASS/FAIL per route + waiver tablosu +
         "canonical register: yes+path / no" ifadesi. Required evidence: E1.

Durma koşulları: ambiguous authority · protected-path yazma ihtiyacı · scope belirsizliği. Dur ve raporla.

Rapor formatı: §22 structured report + reconciliation tabloları.
Senin PASS'in kapanış değildir (K13). CT bağımsız doğrular.
