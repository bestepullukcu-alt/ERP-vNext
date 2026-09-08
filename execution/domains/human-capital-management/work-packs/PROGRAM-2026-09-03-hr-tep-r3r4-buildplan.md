# CONTROL TOWER — HR & TEP R3/R4 Build Program: Reconciliation, Ordered Plan & Gates

- **Date:** 2026-09-03 · **Role:** CONTROL TOWER (SOP v2.4)
- **Source (priority/portfolio, §4.1 "product priority" row):** `ERPSource /Project ongoing status report 15 June 2026 (1).xlsx` → sheet `HR & TEP`
- **Source (canonical identity, §4.1 row 1):** `ERPSource /System Capability & Implementation Blueprint - master 5.xlsx` → `Blueprint_Data`, `Release_Backlog`
- **Scope requested:** develop backend/frontend/API for 40 MODs (R3-C/D/E/F/G + all R4)
- **Turn type:** Profile C measurement + planning · **no implementation, no dev dispatch** (gates fail)

## Measurement evidence (K2/K7/K10)
```bash
# identity authority: Blueprint has ONLY analytics backbone of the 40
python3 -c "openpyxl Blueprint_Data" → matched: MOD-0004,0059,0060,0061,0062,0063,0064 ; ALL others ABSENT
# registry: zero rows for all 40
for m in 0307..0351 ...; do grep "MOD-$m" execution/registries/module-id-registry.md; done  → NO REGISTRY ROW ×40
# packs: only CAND-CAP-0006..0027 exist; none for the 40
# AGENTS.md: HCM native runtime "production service yok", pending DCP-002 (lines 33,61-67)
```

## Gate status — applies to the requested dev work
| Gate | Result | Consequence |
|---|---|---|
| **Canonical identity (§4.1 row 1)** | **FAIL** — 0307-0320/0332-0351 not Blueprint-backed, no registry row; 0004/0059-0064 belong to Enterprise/Data domains, no HCM mapping | cannot write MOD ids into runtime literals |
| **Module Pack (§6, §8.1)** | **FAIL** — no pack for any of the 40 | entry point = `/prepare-module-pack` / `/prepare-capability-pack`, NOT dev |
| **Authorization (D-1 / DCP-002)** | **FAIL** — AGENTS.md: HCM native runtime not authorized | fail-closed on runtime implementation (§4.2) |
| **Dependency (§16.1)** | **UNMEASURED** — deps for non-canonical ids undefined in Blueprint `Dependencies` | packs must declare deps before sequencing hardens |
| **DoR (§8.1)** | **FAIL** | **no dev WP can reach READY this turn** |

**Therefore:** the executable next work for all 40 is the **pack/identity pipeline stage**, dispatched in dependency order. Direct `@orchestrator /add-module` dev prompts become DoR-valid only per-module AFTER: (a) identity ratified (D-2), (b) pack approved, (c) D-1 authorization resolved.

---

## §14 DECISION REQUIRED — D-2 (owner: EA / registry gate)
```text
Decision ID: CT-HCM-D-2026-09-03-02
Context:     40 requested MODs are planning-only ids (status report) with no Blueprint/registry identity.
Options:     (A) Follow the established 0300-series pattern: allocate CAND-CAP aliases (CAND-CAP-0028+) +
                 reserved MOD mapping in module-id-registry, metadata-only readiness first-slice, pack-first.
             (B) Canonicalize into Blueprint master first (adds Blueprint_Data/Release_Backlog rows), then packs.
             (C) Reject/defer part of the scope (e.g., R4 TEP until R2 TEP MVP 0321-0331 exists).
Recommendation: (A) for R3 completion (consistent with 0300-0306 in-flight pattern); explicit Blueprint
                canonicalization (B) before any id is written to a shared contract/gateway route.
Selected:    <pending owner>
```
Blocks: all pack-authoring that must cite a MOD/alias in a shared record. Does NOT block pack-authoring that *proposes* aliases for ratification (the WP below does exactly this).

---

## Ordered build plan (dependency-aware) — the "sırasıyla" sequence

Legend: **[FLIGHT]** in-flight (CAND-CAP-0022-0027) · **[EXT]** external domain (Data/Enterprise) · **[NEW]** greenfield, needs pack. Entry point for every **[NEW]** = pack-authoring.

### Prerequisite (blocks the whole wave)
- P0. Resolve **D-1** (authorize HCM runtime-first slice) + **D-2** (identity/alias policy). CT+EA. **No dispatch until P0.**
- P1. Close in-flight R3-A/B/C **[FLIGHT]**: 0300/0301/0302/0303/0304 + 0306 (perf-review draft) — see prior takeover record; verify via WP-HCM-INS-0001/0002.

### Batch 1 — R3-C Talent Development cluster **[NEW]**  → capability pack
Order: **MOD-0307 Competency & Skills Assessment → MOD-0308 Development Plan → MOD-0309 Learning/Training Records → MOD-0320 Succession & High-Potential**. (0307 skills feed 0308 plans + 0320 succession; 0309 records feed 0308/0320.)
→ **WP-HCM-PACK-0001** (this turn) — `/prepare-capability-pack`.

### Batch 2 — R3-D Planning cluster **[NEW]**
Order: **MOD-0310 Workforce Planning → MOD-0311 Headcount & Position Budget**. Depends on: position/org foundation (MOD-0288/0299), competency (0307). → `/prepare-capability-pack`.

### Batch 3 — R3-G Analytics backbone **[EXT]** — verify, don't build here
**MOD-0004, 0059, 0060, 0061, 0062, 0063, 0064** are Enterprise/Data-plane modules (Blueprint W-2/W-3/W-4). HR does not own them. → **INS WP**: confirm their build/runtime status in the data-plane services before HR façade 0312 can consume them. Blocks 0312.

### Batch 4 — R3-E Facade/Interface cluster **[NEW]**
Order: **MOD-0312 HR KPI & Analytics Facade** (dep: Batch 3 analytics) · **MOD-0313 HR Documentation & Evidence Workspace** (dep: MOD-0028/0031 evidence backbone) · **MOD-0315 Time/Attendance/Leave Interface** (dep: ext MOD-0280) · **MOD-0316 Compensation & Benefits Interface** (dep: ext MOD-0279) · **MOD-0319 Employee/Manager Self-Service Channel** (dep: most of R1-R3, build LAST). → `/prepare-module-pack` per module (distinct SoRs/consumers).

### Batch 5 — R3-F Cases & Compliance **[NEW]**
**MOD-0317 Employee Relations & HR Case Management** (dep: workflow MOD-0023, audit MOD-0021) · **MOD-0318 HR Compliance & Statutory Reporting** (dep: records/retention MOD-0030, audit). → `/prepare-capability-pack`.

### Batch 6+ — R4 TEP Completion **[NEW]** (gated on R2 TEP MVP 0321-0331 existing)
Order by dependency: **0336 Talent Data Foundation** → **0332/0333/0334** (risk/early-warning/integrity) → **0335** reputation → **0337-0343** (pool, passports, dev network, succession pool, cert registry, mentorship) → **0344-0349** (benchmarking, workforce analytics, trends, forecasting, heatmap, mobility) → **0350/0351** (association ops, knowledge network).
→ Each cluster `/prepare-capability-pack`. **R4 is BLOCKED until R2 TEP MVP modules (0321-0331) exist** — verify first (none seen in HCM domain; TEP likely separate domain — INS needed).

---

## §30.1 Closure & replan (this turn)
```text
CT status:        program measured; D-1 + D-2 OPEN; dev dispatch fail-closed (identity+pack+authorization)
What changed:     wrote this plan + WP-HCM-PACK-0001 to work-packs register (SoR); no product code
Decisions:        none self-made; D-2 escalated (K9); recommendation recorded
Intentionally not done: did NOT emit dev prompts for the 40 (DoR FAIL — would inject non-canonical ids, K12/§4.1)
Known gaps:       analytics backbone (0004/0059-0064) build status unknown; R2 TEP MVP existence unknown;
                  per-module dependencies undefined until packs declare them
Next work (order): (1) owner resolves D-1/D-2 → (2) dispatch WP-HCM-PACK-0001 (R3-C) →
                  (3) WP-HCM-PACK-0002 (R3-D) + INS analytics-backbone status (parallel-safe) → Batches 4-6
```

## Dispatch decision — 2026-09-03 (CT judgment; owner gave "no preference")
Chosen path: **emit R3 pack-authoring prompts now in dependency order (proposal-mode); gate R4 behind TEP-MVP
verification.** Rejected the 0300-series runtime-first path (max identity/authorization debt) — that posture is the
owner's to accept, not CT's to assume. D-1 + D-2 remain OPEN before any pack goes draft→approved or any dev starts.

Register (all Profile C / read-or-doc, mutually parallel-safe; none touch code/registry — single-writer identity
seam untouched, §16.4):

| WP | Batch | Entry point | Produces | Dispatch state |
|---|---|---|---|---|
| WP-HCM-PACK-0001 | 1 · R3-C | /prepare-capability-pack | 4 draft packs (0307/0308/0309/0320) | READY (proposal) |
| WP-HCM-PACK-0002 | 2 · R3-D | /prepare-capability-pack | 2 draft packs (0310/0311) | READY (proposal) |
| WP-HCM-INS-0003 | 3 · R3-G | /read-only-audit | analytics backbone status (0004/0059-0064) | READY — gates 0312 |
| WP-HCM-PACK-0003 | 4 · R3-E | /prepare-module-pack | 5 draft packs (0312/0313/0315/0316/0319) | READY; 0312 CONTRACT-BLOCKED on INS-0003 |
| WP-HCM-PACK-0004 | 5 · R3-F | /prepare-capability-pack | 2 draft packs (0317/0318) | READY (proposal) |
| WP-TEP-INS-0001 | 6 pre | /read-only-audit | R2 TEP MVP existence (0321-0331) | READY — gates ALL R4 |

R4 (0332-0351) pack authoring intentionally NOT emitted — blocked on WP-TEP-INS-0001 verdict (K10: no prompt
against unverified precondition). Batch 6+ authored once TEP MVP confirmed.
```text
```
