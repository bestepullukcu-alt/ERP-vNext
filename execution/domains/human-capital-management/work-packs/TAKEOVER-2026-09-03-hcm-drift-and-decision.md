# CONTROL TOWER — HCM Takeover Record & Drift Matrix

- **Date:** 2026-09-03
- **Role:** CONTROL TOWER (SOP v2.4 + Operating Card v1.0)
- **Scope:** domain `human-capital-management` — in-flight CAND-CAP-0024/0025/0026/0027
- **Turn type:** §33 Day-1 takeover · Profile C (read-only inspection) · **no implementation, no product-code writes**
- **Repository:** `/Users/cihan/Desktop/ERP-vNext` · Branch `hr-future` · HEAD `c2e54336` · single worktree
- **System-of-record note (K11):** this file is SoR for the takeover; chat is not.

---

## §12.1 Inspection budget (operated under)

```text
Max files:        ~40 read-only (packs headers, registry, AGENTS.md, ocelot/DI/shell diffs, code inventory)
Max time:         1 CT turn
Early-stop blocker: authority/identity conflict on runtime authorization (HIT — see D-1)
No-write constraint: YES — measurement only; the only writes this turn are CT records under work-packs/
```

## §11 CLASSIFY

```text
Task class:           capability takeover / reconciliation (cross-module, read-only this turn)
Owner candidate:      hcm-domain-owner + enterprise-architect (EA) for DCP-002 gate
Required pack:        CAND-CAP-0024/0025/0026 (shipped), CAND-CAP-0027 (draft)
Prompt profile:       C (inspection) for the two dispatched WPs
Risk:                 HIGH (authority/authorization gate, shared contract seams, tenant/PII surface)
Inspection required:  yes
Entry point (§6):     read-only audit — /read-only-audit
Target agent/command: read-only-auditor
```

## Evidence (reproducible commands — K7)

```bash
# identity vs pack-status contradiction
grep -nE '^status|canonicalization_status' execution/domains/human-capital-management/module-packs/CAND-CAP-002[4-7]*.md
grep -nE 'CAND-CAP-002[4-7]' execution/registries/module-id-registry.md         # all = candidate/pending-EA, "does not authorize runtime implementation"
# AGENTS.md higher authority (repo-wide execution, §4.1)
grep -nE 'HCM|5059|production service yok|DCP-002' AGENTS.md                     # line 33/61-67/81: no production runtime; pending DCP-002
# code reality (all untracked)
git status --porcelain=v1 | grep '??' | grep -iE 'Onboarding|EmploymentChange|Offer'
# shared seams touched
git diff --stat gateway/Diten.ApiGateway/ocelot.json .../Persistence/DependencyInjection.cs \
  frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml frontend/Diten.Web/Resources/SharedResource.*.resx
# runtime
lsof -nP -iTCP:5059 -sTCP:LISTEN            # HCM live; freshness (process-start vs binary) UNMEASURED → WP-HCM-INS-0001
```

---

## Pack ↔ Code ↔ Runtime drift matrix

| Capability | Pack front-matter | Identity registry (§4.1 auth) | AGENTS.md (repo-wide auth) | Code reality | Runtime (5059) | Verdict |
|---|---|---|---|---|---|---|
| **0024 Offer Mgmt** | `status: done` · `EA-waived-for-runtime-first-slice` | `candidate / pending-EA`, MOD-0302 reserved, **"does not authorize runtime implementation"** | native HCM runtime **not authorized**, pending DCP-002 | Backend + FE controller + views/js shipped (4 files untracked); **no ocelot route in diff** | route wiring **unknown** | **DRIFT_FOUND + DECISION_REQUIRED** |
| **0025 Employee Onboarding** | `status: done` · `EA-waived-for-runtime-first-slice` | `candidate / pending-EA`, MOD-0303, **"does not authorize…"** | same | Full slice shipped (11 files: ctrl, App Features, Domain `ReadinessMetadata`+`ReadinessState`, Mongo repo, tests, FE); ocelot route → 5059; DI registered | live; **freshness unmeasured** | **DRIFT_FOUND + DECISION_REQUIRED** |
| **0026 Employment Change/Transfer/Promotion** | `status: done` · `pending-EA` (§20 asserts runtime waiver) | `candidate / pending-EA`, MOD-0304, **"does not authorize…"** | same | Full slice shipped (11 files, mirror of 0025); ocelot route → 5059; DI registered | live; **freshness unmeasured** | **DRIFT_FOUND + DECISION_REQUIRED** |
| **0027 Performance Review Mgmt** | `status: draft` · `pending-EA` | `candidate / pending-EA`, MOD-0306 | same | **no code** | n/a | **ALIGNED** (pack ahead of code, as expected) |

### Shared-seam single-writer picture (§16.4)
All written by one in-flight HR effort; **single worktree → no concurrent writer today** (contrast K14 historical 19):
`ocelot.json` (+80, routes→5059) · `DependencyInjection.cs` (+2 repo regs) · `_LayoutTenantShell.cshtml` (+32 nav) · `SharedResource.en/tr.resx` (+3/+3) · `module-id-registry.md` (+3 rows).
**All shipped HR work is UNTRACKED/uncommitted (K17): it lives in no branch; a stray checkout/stash destroys it.**

---

## §14 DECISION REQUIRED — D-1 (owner: EA / hcm-domain-owner)

```text
Decision ID:        CT-HCM-D-2026-09-03-01
Context:            0024/0025/0026 shipped native HCM runtime (controllers, persistence, gateway, DI, shell, l10n).
Measured evidence:  Packs self-declare status:done + "EA-waived-for-runtime-first-slice".
                    Registry says candidate/pending-EA + "does not authorize runtime implementation".
                    AGENTS.md (higher §4.1 authority) says HCM production service yok; pending DCP-002 canonicalization.
                    No canonical waiver register exists (docs/decisions absent; §5.1 exception SoR missing).
Authority concern:  §4.1 — a Module Pack (row 2) cannot self-grant a runtime waiver overriding the DCP-002 /
                    identity gate held by AGENTS.md + registry (rows 1,3,4). Pack "done" is not authority.
Options:            (A) EA ratifies runtime-first-slice into a canonical decision + waiver register (K19: owner,
                        compensating control, expiry/review, exit) and updates registry status; work continues.
                    (B) EA rejects; shipped 0024/0025/0026 runtime is reworked/reverted to authorized scope.
                    (C) EA scopes a narrower "metadata-only readiness" ratification; audit confirms code stays inside it.
Recommendation:     Do NOT dispatch further HCM implementation (incl. 0027 build or any 0024-0026 extension) until
                    D-1 is resolved (§4.2 fail-closed). Meanwhile dispatch the two READ-ONLY inspection WPs below to
                    produce the E1/E3 evidence the decision needs.
Selected option:    <pending owner>
Required record changes: registry status rows · new waiver register / decision log · pack front-matter status reconcile
Effective date:     <pending>
```

**Gate consequence:** all *implementation* HCM WPs are `BLOCKED` on D-1. The two WPs issued this turn are `INS`/read-only and are **not** blocked (§11: ambiguity → inspection; §4.2 blocks implementation, not evidence-gathering).

---

## §30.1 Closure & replan (this turn)

```text
Turn output:        drift matrix + D-1 decision brief + 2 INS WP files (register created)
CT status:          takeover measured; DECISION_REQUIRED open (D-1)
What changed:       created execution/domains/human-capital-management/work-packs/ (SoR register was missing)
Decisions:          none self-made; D-1 escalated to EA/owner (K9 — CT challenges by measurement, does not bypass authority)
Intentionally not done: no product-code/pack/registry edits; no implementation dispatch (fail-closed on D-1)
Known gaps:         (g1) HCM 5059 runtime freshness unmeasured; (g2) offer gateway wiring unknown; (g3) waiver register absent;
                    (g4) SOP/Operating-Card at repo root not docs/ (§2.4/§40 companion-link gate fails); (g5) all HR work uncommitted
Dependency impact:  0027 implementation depends on D-1; 0024-0026 extension depends on D-1
Next work:          WP-HCM-INS-0001, WP-HCM-INS-0002 (parallel-safe, read-only) → feed D-1
```
