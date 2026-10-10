# MVP-6 development plan v4.0

Review date: 2026-09-17. WP MVP6-CT-REPLAN-04. Supersedes v3.0 planning statements,
not historical reports, frozen contracts or module-pack authority. User requested CT rules review,
Antigravity orchestration and an updated continuation plan. This turn is planning, not new runtime scope.

## 1. Measured baseline and evidence

Repository /Users/natig/Projects/ERP-vNext-recovery; branch feature/mvp6-logistics;
HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c. Staging empty. Expected dirty tree contains
approved publication, Carrier pack/Program.cs changes, Carrier implementation/tests and prior records.
Do not stash, reset, overwrite, commit or push these inputs as part of this plan.

Fresh composed DEV02+DEV03 manifest check: **78 current non-self hashes match**, no mismatch.
DEV03 supersedes the recorder entry in DEV02; do not falsely report that both historical recorder
hashes match current bytes. Canonical YAML and annex retain approved R1 SHA256 respectively:
ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f and
87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee.

Historical independently verified runtime: build0/0,99 service tests,Carrier33,Shipment33,
VER03 repaired outage9 requests/0 mismatch and GREEN→RED→GREEN. Tests were NOT rerun for this
planning review. Last architecture measurement14 PASS/4 FAIL remains a historical result, not a new run.
Evidence: ../../records/audits/2026-09/mod-0184-ct-continuation-2026-09-17/README.md.

## 2. Rules that determine the plan

- Authority: Module Pack > domain-config > AGENTS.md > global engineering rules. CT SOP v2.4
  and operating card define dispatch/evidence/replan; their adoption status is not changed here.
- Orchestrator coordinates approved packs; module-pack-author prepares draft packs. A planning brief
  or user request to continue does not silently supply missing contract semantics or pack approval.
- SOP §§17/20: measured branch/HEAD/dirty baseline, target agent, owned/protected paths, dependencies,
  risk/profile, NE/NEDEN/NASIL/YAPMA/DOĞRULA and testable exit criteria precede dispatch.
- K13 and §29: agent/VER PASS differs from CT acceptance and full-module/capability completion.
- K14/K15: separate worktrees and disjoint ownership for parallel writers; one composition writer.
  Read-only reviews may share frozen inputs. A worktree created from HEAD alone misses current
  uncommitted Carrier code: first transport the complete approved snapshot and verify its manifest.
- K16: new scope requires a new bounded WP. Rework cannot silently change contracts or shared guards.
- docs-organization K4/K8: historical records are immutable; relocation requires reference inventory,
  approved migration and integrity verification. Encoding/splitting a path just to evade a regex is not a fix.
- Explicit prior publication/security/consumer approvals and external-consumer attestation remain valid.
  Do not repeat those approval loops. Platform/HCM/Talent fixes remain user-deferred.

## 3. Current module readiness

| Module | Current state | Next action / gate |
|---|---|---|
| 0183 Shipment/POD | Bounded E4 centrally accepted; pack still ready-for-dev | Keep ingress GAP01–04 and full E5/G5 separate; reconcile status without marking full module done |
| 0184 Carrier | Pack ready-for-dev; bounded implementation independently verified; CT acceptance pending | Disposition DocsPathGuard, then exact bounded CT decision and pack/board reconciliation |
| 0185 Routing/Load | Draft; depends on Active Carrier and eligible Shipment | First downstream pack preparation; exact reference/client/composition/lifecycle decisions |
| 0186 Reverse | Draft | Prepare alongside0185; distinguish outbound contract from required inbound-return seam |
| 0187 Claims | Draft | Prepare alongside0185; evidence/reference/error/transaction scope and upstream dependencies |
| 0190 S&OP | Draft | Later runtime wave; logistics0183–0187 frozen/verified prerequisite, DEMAND mock and snapshot/signoff semantics |
| 0192 Capacity | Draft | Same later wave; logistics prerequisite and DEMAND mock; shared seams explicit |
| 0147 Performance | Draft | Later wave; Supplier contract identity and domain ownership reconciliation |
| 0148 Portal | Draft | Later wave; ownership plus authenticated supplier-actor mapping; base identity alone is insufficient |

Intended runtime sequence: accepted0183 → bounded0184 acceptance → {0185,0186,0187} →
{0190,0192} → {0147,0148}. Braces indicate potential independent module work, not permission to
write concurrently into the current service checkout. Every module gets its own DoR, Phase1.5,
approved pack, versioned DEV prompt, DEV→independent VER→CT decision.

## 4. Immediate two-track work

| WP | Target / profile | Status | Exit |
|---|---|---|---|
| MVP6-DOCS-PATH-DISPOSITION-01 | read-only-auditor, INS/C, HIGH | READY for inspection only | Exact17-file provenance, guard-vs-canonical conflict, smallest evidence-preserving owner decision and concrete repair scope |
| MVP6-LOGISTICS-NEXT-PREP-01 | module-pack-author, spec-only/B, HIGH | READY sequentially in this checkout | Revised draft0185/0186/0187 packs, canonical consumed identities, operation parity, owned files and explicit unresolved decisions |
| MVP6-DOCS-PATH-REPAIR-01 | documentation-writer; shared authority owner if necessary | HELD pending exact disposition | Authorized repair plus unchanged guard and affected test; preserve historical hash lineage |
| MVP6-MOD0184-CT-ACCEPT-01 | CT with independent evidence review | Pending gate disposition | Explicit bounded acceptance/rework decision; full-module and repository status separate |
| MVP6-MOD0185-DEV-01 | orchestrator + add-module; backend/data/security/testing roles | HELD | Approved0185 pack, exact contract/Phase1.5 and versioned prompt; bounded E4 then independent VER |

The second track need not wait for DocsPathGuard repair or full repository green to prepare specs.
In this checkout serialize writers; a read-only reviewer may run alongside disjoint pack preparation.
Future parallel runtime requires actual isolated worktrees and measured shared-seam safety, not merely
different feature folders. Program.cs, permissions, contracts and registration have one integrator.

## 5. Concrete preparation corrections

0185/0186/0187 still refer to missing WAREHOUSE-SHIPMENT-TRIGGER. The brief now names existing frozen
WAREHOUSE-OUTBOUND. Compare operations and payloads, replace stale identity claims only where covered;
do not conclude automatic intake/correlation or reverse inbound disposition is thereby implemented.

0147/0148 still describe SUPPLIER-BASE as absent, although SUPPLIER is published. Reconcile identity
and supported operations; supplier actor mapping and domain ownership remain separate open decisions.
The brief's old G3/en-son wording is historical relative to the user's contract-first/mock-first lane
authorization. Do not wait for real upstream services when frozen mocks cover the bounded use case.

For each next pack measure schema-required behavior, error/header/replay matrix, legal entity scope,
reference eligibility, lifecycle/events, atomic outbox, failure recovery and exact test criteria.
Record SHIPMENT-BUNDLE info.version1.1.0 separately from wire contractVersion:v1; the Carrier release
does not change non-Carrier operations. For0185 include duplicate-assignment atomicity;0186 net-return
quantity concurrency;0187 decimal/approval bounds and operational settlement without finance posting.
For0190/0192 explicitly resolve Workflow/Event Bus and supply-constraint ownership; for0147 metric/risk
building blocks, and0148 trusted supplier-actor mapping. Source master data remains reference-only.
Reuse existing contracts unchanged; missing semantics are explicit owner decisions, not invented defaults.
Backend-only shell none/form0/golden none applies only when the scoped slice remains backend-only.
Gateway/UI/E5 are separate integration scope; preserve Inventory GET-only/no shadow stock.

## 6. Immediate paste-ready inspection prompt

```text
WP MVP6-DOCS-PATH-DISPOSITION-01; prompt v1.0; lane AL-MVP6-DOCS-INS01; INS/C; HIGH.
Target read-only-auditor / read-only-audit; strict repository-read-only; E1/E2 inspection only.
Repository/worktree /Users/natig/Projects/ERP-vNext-recovery
Branch feature/mvp6-logistics; expected HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c.
Capture fresh status and hashes; existing dirty Carrier/publication/records are expected protected inputs.
Read AGENTS.md, docs-organization, CT SOP, DocsPathGuardTests.cs, canonical contract locations,
mod-0184-dev-01/architecture-baseline-proof.md and CT continuation reports under September audit records.
Depends on completed VER03; parallel-safe only with writers outside inspected inputs.
Allowed reads: exact guard,17 offending artifacts,their manifests/references and canonical authority.
Protected: all repository files and git metadata. Output report in response; no repository writes.
NE: Produce one exact evidence-preserving disposition for the fourth architecture failure.
NEDEN: Approved canonical legacy paths and immutable historical records conflict with current guard policy.
NASIL: Separate active consumers from historical path data; verify all17 artifacts and two current-turn
uptake outputs. Compare narrowly scoped migration/owner exception options and their required authority.
YAPMA: No code/guard edits, records rewrite, path obfuscation, contract relocation or waiver.
DOĞRULA: Every claim has path:line evidence, exact proposed affected paths, references/hash lineage,
retained regression requirements and no-change proof. Name actual authority needed, not generic reapproval.
Unknown fact: inspect canonical sources; report ASSUMPTION only for a documented default.
Stop affected work for hash/branch conflict, ownership conflict or missing migration authority.
SOP §22 output; findings are not a repair or CT acceptance.
```

## 7. Next pack preparation prompt

```text
WP MVP6-LOGISTICS-NEXT-PREP-01; prompt v1.0; lane AL-MVP6-NEXT-PREP01; INS/B; HIGH; E1/E2.
Target module-pack-author / prepare-module-pack; spec-only, no runtime persistence or migration.
Repository/worktree /Users/natig/Projects/ERP-vNext-recovery
Branch feature/mvp6-logistics; expected HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c.
Capture fresh dirty baseline. Single pack writer; no concurrent writer to these packs. No other repo writer
unless separately proven disjoint. Existing uncommitted implementation/records must be preserved.
Read AGENTS.md, module-pack-author mandatory context, module-pack-standard, supply-chain domain config,
DCP009, registry/Blueprint, WP-MVP6, decision report, approved contracts,0183 acceptance and0184VER chain.
Owned existing files only: execution/domains/supply-chain-execution/module-packs/
MOD-0185-routing-load-planning.md, MOD-0186-reverse-logistics.md, MOD-0187-claims-management.md.
Protected: every other existing file including contracts, Program.cs, services, registry,DCP,.antigravity.
NE: Prepare the next three backend-only draft packs,0185 first,with exact contract and development gates.
NEDEN: Shipment is bounded accepted and Carrier evidence is ready; stale consumed identities must not
create artificial waiting or authorize missing integration behavior.
NASIL: Run DCP002 using each pack's canonical name. Reconcile Warehouse outbound identity against actual
operations. Specify each module's exact owned files, lifecycle/reference/error/replay/event/transaction
semantics, frozen mocks, Phase1.5 proposal and testable acceptance. Separate covered and uncovered seams.
YAPMA: No runtime, new schema,error invention,pack promotion,contract edits,shared composition change,
gateway,stock mutation,commit/push/stash or acceptance claim. Keep all three status draft.
DOĞRULA: Contract operation parity, identity checks, draft frontmatter, owned/protected disjointness,
no fabricated defaults, and fresh preservation. Output exact remaining decisions and proposed bounded
DEV scope in SOP §22. Readiness can advance only on actual authority and completed preconditions.
No routine questions: use documented defaults and ASSUMPTION; report concrete unresolved contract/security
decisions rather than guessing. Return report in response; CT persists new records separately.
```

## 8. Closure and integration

Do not mark full module done from the bounded tests. CT decision and applicable pack/seam/board/backlog
updates follow evidence review within each record's ownership; central DCP/registry changes stay central.
Final MVP6 G5 still requires shipment→carrier/load→POD→return/claim, source reconciliation,
S&OP/capacity reproducible snapshots and supplier feedback with actual integrated E5 evidence.
This plan neither repeats completed Carrier development nor advances downstream runtime prematurely.
