# MVP6 process pilot — lane prompts v1.0 (2026-09-25)

Pilot limit: two product lanes (Lane-1, Lane-2) + one shared-environment lane (Lane-3). Lane-4 is decision-only.
Each lane chat cannot see the CT conversation: paste the full prompt. Every lane follows
`docs/guides/operations/mvp6-development-process-v1.0.md` §§3–6 and records its events in
`docs/roadmap/plans/mvp6-process-pilot-01/MILESTONE-EVENTS.tsv` **only through its final report** (CT writes the ledger).

---

## Agent Lane-1 — A12 runtime independent VER (queue Q04, READY)

```
Role: MVP6 Lane-1, independent A12 runtime VER. You did not write the A12 patch. You have no access to earlier conversations.
Repo: /Users/natig/Projects/ERP-vNext-recovery (expected feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c; STOP if different).
Process: docs/guides/operations/mvp6-development-process-v1.0.md (§5 evidence checklist, §6 verification by impact).
Authority (owner decision, 2026-09-25, CEO Natig Yusubov, in the CT conversation): existing records suffice —
docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/INDEPENDENT-VER-HANDOFF.md lines 8–24 and
mvp6-shipment-a12-safe404-rework-01/INDEPENDENT-VER-HANDOFF.md. Scope: disposable, isolated, evidence-only run of the A12 successor.
Copy this decision verbatim into AUTHORITY.md. NO source repair, common checkout, gateway/.antigravity/contract edits, A10 proxy, commit/push/stash/prune.
Read: AGENTS.md, CLAUDE.md, MOD-0183 pack; docs/roadmap/plans/mvp6-claude-development-handoff-01/{ENVIRONMENT,AUTHORITY-AND-GATES}.md;
both handoffs above; a12-safe404-rework-01/SOP-22.md; a12-safe404-independent-ver-01/SOP-22.md; template run
mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/ (SOP-22.md, raw/runtime-and-input-hashes.txt, COMMANDS.tsv, scripts/);
shared-ui-independent-ver-01/SOP-22-VER.md (HEAD archive + overlay method); docs/roadmap/plans/mvp6-process-pilot-01/EVIDENCE-REUSE.tsv.
Preflight:
 1. Hashes: A12 patch b3c7dcbb…ecefb, archive 7b6a0d1a…314d, manifest 8ffa6c96…0d36; Auth overlay mvp6-carrier-numericdate-exec-01/final-source.tar.gz f50350b8…e2cd and FINAL-22-SOURCE-MANIFEST.tsv b9713185…1731.
 2. New /private/tmp dir: git archive 4a8d4d4 → A12 360 overlay → Auth 22 overlay. Prove zero path overlap, 360/360 and 22/22. No dirty-checkout source.
 3. Generate a disposable gateway route config (the earlier 776cfe9e… file is gone) from HEAD ocelot.json for lane ports only; never edit the repo ocelot.json; record diff and hash.
 4. BUILD-INPUT-MANIFEST.tsv separate from the owned 360 manifest. STOP and report any remaining missing dependency.
Run: native /Users/natig/.dotnet/dotnet (verify SDK 8.0.417 / runtime 8.0.23; .NET 10 not acceptable). Release binaries, fresh isolated ports,
DB-010 isolated Mongo replica set (never 27017; verify resolved config before start). Fresh real-Auth identities: actor-a T1/LE-A, actor-b T1/LE-A,
actor-le-b T1/LE-B; separate browser profiles; no cookie swapping.
Early vertical slice first: one normal authorized detail load end-to-end before the negative matrix.
Test: normal detail (summary/lines/POD/actions); cross-LE, unknown, soft-deleted detail show only localized safe-not-found + support reference;
hidden surfaces hidden/inert and not keyboard-reachable; late async responses do not re-expose stale surfaces; backend 404 SHIPMENT_NOT_FOUND
+ correlation unchanged; DB before/after zero writes; add negative controls.
Impact regression (EVIDENCE-REUSE.tsv): A12 changes load() in details.js, the refresh path of A08/A09 → rerun the A08 stale-transition and
both A09 stale-POD browser flows (targeted, same fixtures pattern). Accessibility and A03/PRES-183-02 stay inherited (no impact).
PNG: check whether a documented, supported screenshot save/export exists in this tooling; if yes, save PNGs with hashes; if not, record the blocker. No base64/CDP/tunnel/unapproved capture.
Forbidden: counting static or inherited evidence as PASS; storing tokens/credentials.
Output: docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-01/ → AUTHORITY.md, SOP-22.md (PASS/FAIL/NOT RUN per criterion,
incl. A08/A09 regression), BUILD-INPUT-MANIFEST.tsv, EVIDENCE-CHECKLIST.md (process §5, each item ticked with pointer), raw/ (redacted;
source→binary→process→browser), CLEANUP.md, ARTIFACTS.sha256. Final report: start and end timestamps (Istanbul), agent run time, waits.
Return the bounded result to CT; CT decides.
```

## Agent Lane-2 — Single Shipment UI acceptance matrix (Lane A, queue Q05, READY)

```
Role: MVP6 Lane-2, Shipment UI acceptance-matrix reconciliation (read-only + own record folder). No access to earlier conversations.
Repo: /Users/natig/Projects/ERP-vNext-recovery @ 4a8d4d4 (feature/mvp6-logistics).
Process: docs/guides/operations/mvp6-development-process-v1.0.md §4 (single acceptance matrix).
Read: AGENTS.md; handoff folder (MODULE-STATUS, AUTHORITY-AND-GATES, LANE-HANDOFFS); under docs/records/audits/2026-09/:
mvp6-shipment-remaining-acceptance-disposition-01/ (DECISION-NEEDS, CURRENT-SUCCESSOR-STATUS, EXECUTION-PLAN, SCOPE-AWARE-VERIFIER-PROPOSAL,
ACCEPTANCE-GAP-MATRIX.tsv), mvp6-shipment-a08-a09-a12-ct-disposition-01/, mvp6-shipment-shared-ui-ct-disposition-01/ (ACCEPTANCE, OPEN-GATES),
mvp6-shipment-line-accessibility-ct-disposition-01/, mvp6-shipment-ui-functional-ct-consolidation-01/, mvp6-shipment-root-r2-ct-disposition-01/;
docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/; docs/roadmap/plans/mvp6-process-pilot-01/{CT-QUEUE,EVIDENCE-REUSE}.tsv.
Task: one matrix for UI183-A01..A16 + PRES rows. Columns: criterion, bounded scope (in/out, with change-record reference), expected HTTP / browser /
DB result, current state (CLOSED_EXACT / ACCEPTED_BOUNDED / STATIC_ONLY / OPEN / UNAUTHORIZED / POLICY_OPEN), evidence pointer + hash, owner,
dependency, READY or BLOCKED, required final-VER runs (positive, negative, security).
Separate generic DataTable expectations from the bounded scope (keep 49 PASS / 35 FAIL as historical; classify 23 out-of-scope / 8 evidence gaps /
4 policy conflicts row by row). No silent waiver: any scope change is a change-record row for the owner.
Do not regenerate existing decision packs (DN-01, DN-02); list only genuinely missing decisions. No new effort credit.
Forbidden: product, pack, board, contract, guard or Git changes; overwriting records.
Output: docs/records/audits/2026-09/mvp6-shipment-acceptance-reconcile-01/ → SOP-22.md, ACCEPTANCE-MATRIX.tsv, SCOPE-CHANGE-RECORD.tsv,
MISSING-DECISIONS.md (delta only), ARTIFACTS.sha256. Final report with start/end timestamps. Report changes only to CT.
```

## Agent Lane-3 — Reusable evidence kit proposal (Lane B, shared-environment lane, queue Q06, READY)

```
Role: MVP6 Lane-3, environment owner for the evidence kit (proposal only). No access to earlier conversations.
Repo: /Users/natig/Projects/ERP-vNext-recovery @ 4a8d4d4 (feature/mvp6-logistics).
Process: docs/guides/operations/mvp6-development-process-v1.0.md §§5, 8.
Read the launch, fresh-Auth fixture, DB-010, port and cleanup material of the successful lanes:
docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/ (scripts/, COMMANDS.tsv, CLEANUP.txt,
raw/runtime-and-input-hashes.txt), mvp6-carrier-real-auth-e2e-exec-01/, mvp6-carrier-auth-chain-recovery-01/, mvp6-shipment-shared-ui-independent-ver-01/,
mvp6-bc-successor-independent-ver-01/; handoff ENVIRONMENT.md; .antigravity/rules/{dev-runbook,mongo-indexing,configuration-safety}.md.
Task: propose ONE reusable evidence kit covering: exact source input (HEAD archive + ordered overlays with manifest checks), native .NET 8
runtime check, effective DB/config verification before start (refuse 27017), port allocation and conflict check, fresh real-Auth identity
creation and login (secrets generated locally, never persisted), redaction, source→binary→process→browser binding, DB before/after snapshot,
cleanup and verification. Reuse existing scripts where they fit; list each reused script with its path and hash.
Treat durable PNG as a separate environment dependency: document which supported save/export mechanisms exist in the current tooling;
do not bypass restrictions.
Do NOT change product code, .antigravity, gateway or repo scripts. Deliver the kit as a reviewable proposal: file list, exact content or diffs,
where each file would live (per docs-organization / repo rules), and the owner decision needed to adopt it.
Output: docs/roadmap/plans/mvp6-evidence-kit-proposal-01/ → README.md, KIT-SPEC.md, REUSED-SCRIPTS.tsv, proposed/ (candidate files, not active),
ADOPTION-DECISION.md (exact decision text, not approved), SHA256SUMS. Final report with start/end timestamps. Return to CT.
```

## Agent Lane-4 — Loads owner decisions (queue Q07–Q09, decision-only)

Unchanged from the CT dispatch of 25 Sep: present decision A first; B and C are HELD until A (then B) is recorded.
Start only when the owner is ready.

---

# Lane prompts v2.0 (2026-10-02) — Mac-only dispatch set

Three changes from v1.0.

**Cowork withdrawn (G2 placement waiver).** `dispatch-wp.md` G2 places T1/T4 on a Cowork LANE (`uname -s` = Linux).
The owner has withdrawn Cowork from this pilot, so these run on Mac Claude Code. Authority is unaffected — OD-CODEX
already names Mac Claude Code an authorized writer — so this is a recorded placement waiver (SOP §16.2), not a silent
bypass. **Lane separation is now by SESSION, not by machine:** open each prompt in its own session.

**Agent contracts are mandatory (G3).** v1.0 prompts carry a `Role:` line but no target agent and no SOP §17.4
mandatory fields; G3 requires both, plus `agent write scope ⊇ Allowed Paths`. Each prompt below names its agents,
fills their §17.4 fields, and instructs the session to `Read` the agent definition — necessary because AGENTS.md §6.1
states `.antigravity/` is never auto-loaded. Where §17.4 has no row for an agent, the prompt says so.

**`read-only-auditor` cannot persist its own report.** Its `tools:` are `Read, Grep, Glob, Bash` — no Write. So every
read-only WP here is a two-agent lane: `read-only-auditor` analyses, `documentation-writer` persists. SOP §17.4's final
paragraph permits sequential specialist agents inside one lane; single-writer (§16.4) is unaffected.

Write scopes were measured and are disjoint, so P1–P4 may run concurrently in the same checkout:
`mvp6-q201-integration-dryrun-01/` · the two ledgers + `mvp6-q203-ledger-repair-01/` ·
`mvp6-q204-governance-audit-01/` · `mvp6-q205-testenv-01/`. Worktrees are not needed until wave 3 (0190 ∥ 0192),
where both trigger builds and would collide on shared `obj/`/`bin/`.

## Common header — paste at the top of every v2.0 prompt

```
Placement: Claude app → </> Code tab → Local → ERP-vNext-recovery. Run `uname -s`; expect Darwin, else STOP.
Open in its OWN session; never two of these in one session. Report: "G2 placement waiver applied — Cowork withdrawn by owner."
Repo: /Users/natig/Projects/ERP-vNext-recovery, branch feature/mvp6-logistics @ 4a8d4d4b3 (verify; STOP if different).
FIRST ACTION — .antigravity/ is NOT auto-loaded (AGENTS.md §6.1): Read AGENTS.md in full, then every agent/rule/workflow
file named in your AGENT CONTRACT below. Skipping this voids the prompt.
Git preflight (G6): export GIT_OPTIONAL_LOCKS=0; use only `git status --porcelain`, never `git diff`; .git/index.lock exists → STOP.
Known baseline (G7, re-measured 2026-10-02, supersedes "29 M + 2 untracked"): 32 modified, 533 untracked, 0 staged = 565.
Known state, not drift — no clean/stash/reset/checkout/commit.
Never run: git add · git commit · git push · git stash · git checkout · gh (any).
Authority order (AGENTS.md §1): Module Pack > Domain Config > AGENTS.md > .antigravity/ > archive.
Protected: .antigravity/** · gateway/** · services/** (unless Allowed Paths says otherwise) · Views/Shared/_Layout.cshtml ·
Controllers/Archive/** · Views/Archive/**. Anything outside your Allowed Paths.
Three sibling sessions may be live on disjoint folders. Needing to write outside your Allowed Paths → STOP and report; do not coordinate yourself.
State your evidence level (E0–E5) explicitly. Agent PASS ≠ CT ACCEPTED. Cannot reach the required level → say so and stop.
Final report: files written · evidence level · findings as F-<wp>-<n> · anything refused · start/end timestamps (Istanbul). Return to CT; CT decides.
```

## P1 — Q201 integration dry-run · lane `AL-SCM-INTEGRATION-DRYRUN` (INT) · E1

Agents: `read-only-auditor` → `documentation-writer`. Depends on Q199 (DONE). Parallel-safe with P2, P3, P4.

Report as **F-Q201-0**: the CT-QUEUE Q201 row names `LANE 3 (integration-agent)`, which is an agent/scope mismatch —
that agent's §17.4 fields are route family, downstream port and ocelot file; none exist here, and gateway is forbidden.
Q203 corrects the row.

```
AGENT CONTRACT — Read first: .antigravity/agents/read-only-auditor.md, .antigravity/agents/documentation-writer.md,
.antigravity/workflows/read-only-audit.md, .antigravity/rules/docs-organization.md.
Phase A, read-only-auditor (no Write tool) — §17.4 fields:
  mode = worktree-read-only · scope = BASE-STACK v2 layers + accepted module overlays vs the feature/mvp6-logistics
  working tree · every finding carries path:line, else mark "insufficient evidence" and do not speculate ·
  NO FIXES: not one conflict is resolved.
Phase B, documentation-writer — persists Phase A's findings to the Allowed Path only.
Scope check: documentation-writer write scope ⊇ Allowed Paths → matches.
Allowed Paths (write): docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/**  — everything else READ-ONLY.

Context, already measured — do not rediscover: the 533 untracked files are the suspected overlay population.
BASE-STACK v2 = docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md. Accepted overlays live under
docs/records/audits/2026-09/** and 2026-10/**.

Task — three lists, nothing modified: ADD (in BASE-STACK/overlays, absent from the tree) · OVERWRITE (in both,
differs) · CONFLICT (in both, differs, and the tree copy is newer or locally modified). For each CONFLICT name the
authoritative side and cite the record that decides it; cite records, do not exercise judgement where one already decided.
Report separately, never folded into the bulk lists — SOP-22 assigns these to a single integration-agent lane:
Api/Program.cs, common DI/pipeline registration, shared permission catalog/seed, gateway ocelot.json, canonical
SANDOP/DEMAND. Describe the patch each would need, then stop.
Forbidden: writing into the working tree · resolving a conflict · copying an overlay · touching Program.cs.
Dry-run means list only; Q202 does the writing under its own authority.
Output: docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/ → SOP-22.md, ADD-OVERWRITE-CONFLICT.tsv,
SHARED-SEAM-PATCH-NEEDS.md, ARTIFACTS.sha256.
```

## P2 — Q203 ledger repair · lane `AL-CT-LEDGER-REPAIR` (INS) · E1 · single writer

Agents: `read-only-auditor` → `documentation-writer`. No dependency. Parallel-safe with P1, P3, P4.
**Not** parallel-safe with Q200 — both write CT-QUEUE.

```
AGENT CONTRACT — Read first: .antigravity/agents/read-only-auditor.md, .antigravity/agents/documentation-writer.md.
Two agents because read-only-auditor has no Write tool (§17.4: "Yazma aracı yoktur; düzeltme talebi bu ajana verilemez")
and this WP must write two TSVs. §17.4's final paragraph permits sequential agents inside one lane.
Phase A, read-only-auditor — §17.4 fields:
  mode = worktree-read-only · scope = CT-QUEUE.tsv + MILESTONE-EVENTS.tsv under docs/roadmap/plans/mvp6-process-pilot-01/ ·
  each ID's authoritative state needs a path:line or record-folder citation, else DECISION-REQUIRED · Phase A fixes nothing.
Phase B, documentation-writer — writes the decision to the two ledgers and the record folder, nowhere else.
Scope check: ✓ documentation-writer write scope ⊇ Allowed Paths.
Allowed Paths (write): docs/roadmap/plans/mvp6-process-pilot-01/{CT-QUEUE.tsv,MILESTONE-EVENTS.tsv} ·
docs/records/audits/2026-10/mvp6-q203-ledger-repair-01/**
G10 — before writing, compute and report SHA256 for both; expect 299 rows (CT-QUEUE) and 222 lines (MILESTONE-EVENTS).
Counts differ → STOP, the ledger moved under you.

Measured defect — verify, do not re-measure: 299 rows carry 222 distinct IDs → 77 surplus rows across 60 IDs with
CONFLICTING states. Confirmed: Q99 = READY and SUPERSEDED by Q138; Q84b ×4 including READY and NOT RUN (CT-4);
Q136 = READY and SUPERSEDED (merged). Highest counts Q84b ×4, Q88b ×4, Q158 ×4, Q159 ×4, Q160 ×4. The README
promises one state per open item, so dispatch gate G1 and DoR §8.1 can both pass against the wrong row.

Task: (1) establish each duplicated ID's authoritative state from MILESTONE-EVENTS and the referenced record folder —
not row order, not last-wins; silent record → DECISION-REQUIRED and list it; never invent a state. (2) Collapse to one
row per ID, preserving every removed row verbatim in the record folder so the change is reversible. (3) Append:
  Q203	Ledger repair: collapse CT-QUEUE to one row per ID	<state>	Mac Claude Code	-	-	2026-10-02	docs/records/audits/2026-10/mvp6-q203-ledger-repair-01
  Q204	Governance findings audit F-2..F-5 + F-8 (read-only)	READY	Mac Claude Code	Q203	-	2026-10-02	docs/records/audits/2026-10/mvp6-q204-governance-audit-01
  Q205	Isolated Mongo provisioning for MOD0183/0184/0185 tests	READY	Mac Claude Code	Q203	-	2026-10-02	docs/records/audits/2026-10/mvp6-q205-testenv-01
(4) Correct the Q201 row owner: "LANE 3 (integration-agent)" is an agent/scope mismatch; set Mac Claude Code
(read-only-auditor + documentation-writer) and record the reason. (5) Add one MILESTONE-EVENTS line for this repair with
before/after row counts. (6) Publish new SHA256 and row counts for both ledgers.
STOP: Q200 in flight or another session holding these files → STOP and report; CT serialises, do not merge its work yourself.
Output: docs/records/audits/2026-10/mvp6-q203-ledger-repair-01/ → SOP-22.md, REMOVED-ROWS.tsv, STATE-DECISIONS.tsv,
LEDGER-HASHES.md, ARTIFACTS.sha256.
```

## P3 — Q204 governance findings audit · lane `AL-CT-GOVERNANCE-AUDIT` (INS) · E1

Agents: `read-only-auditor` → `documentation-writer`. Depends on Q203 for its ledger row only.
Parallel-safe with P1, P2, P4. Must not be the P2 session.

```
AGENT CONTRACT — Read first: .antigravity/agents/read-only-auditor.md, .antigravity/workflows/read-only-audit.md,
.antigravity/agents/documentation-writer.md.
Phase A, read-only-auditor — §17.4 fields:
  mode = strict · scope = .antigravity/workflows/dispatch-wp.md, docs/guides/operations/control-tower-sop.md,
  control-tower-operating-card.md, AGENTS.md §2/§3, the services/ inventory, and SOP §17.4 vs .antigravity/agents/ ·
  every finding carries path:line, else "insufficient evidence" and no speculation · NO FIXES, not one file changed.
Phase B, documentation-writer — persists findings to the Allowed Path only.
Allowed Paths (write): docs/records/audits/2026-10/mvp6-q204-governance-audit-01/**
.antigravity/** is READ-ONLY. Recommend; never remediate.

Confirm or refute each, with path:line evidence and a disposition:
F-2 dispatch-wp.md sits in an authority path while its own banner says "PROPOSAL v1 … not in .antigravity/workflows/
 yet", to be copied only after the independent VER (Q142), the CT verdict and OD-R11. Q141 and Q142 are both still READY.
 Is the banner stale or was the file placed early? Separately: the owner has withdrawn Cowork, which makes G2's
 "T1/T4 → Cowork LANE" rule unexecutable as written — does G2 need an amendment? Relocation touches .antigravity/** =
 owner approval only; recommend, never move.
F-3 G7 declares "29 M + 2 untracked"; measured 32 modified, 533 untracked, 0 staged. The modified count is nearly right;
 the drift is the 533. Determine what they are (overlays, records, build output) and recommend a re-baselined G7 line or a
 cleanup scope. Decide nothing about the overlays — Q201 owns that; do not duplicate its work.
F-4 the operating card cites SOP "v2.4"; the SOP is v2.5 "Agent Lane Model". List every card gate v2.5 changed,
 especially §36.2 T1–T4, which dispatch-wp depends on.
F-5 AGENTS.md §2 lists 5 services and comments MDM/PPM/PVG/MG out as unscaffolded; services/ holds 16; §3 already added
 port 5061, so two sections of one file disagree. State the correct inventory. Also: are Diten.HcmService and
 Diten.HumanCapitalService two live boundaries or one dead one? Give evidence; if undecidable, say so — this is a question,
 not yet a finding.
F-8 (new) .antigravity/agents/ holds 20 agents; SOP §17.4 lists 10 entry points. Uncovered: devops-agent,
 documentation-writer, code-quality-agent, performance-optimizer, data-agent, explorer-agent, business-analyst,
 product-manager, product-owner, user-manual-generator. How can a WP targeting one of these satisfy G3? Recommend either
 completing §17.4 or barring those agents as direct targets.
Also record that v1.0 of this file carries no target agent and no §17.4 fields in any lane prompt — the G3 gap predates
the v2.0 set.
Forbidden: fixing anything. A read-only audit that edits a file is no longer a read-only audit.
Output: docs/records/audits/2026-10/mvp6-q204-governance-audit-01/ → SOP-22.md, FINDINGS.tsv (one row per finding with
path:line and disposition), RECOMMENDATIONS.md, ARTIFACTS.sha256.
```

## P4 — Q205 isolated Mongo provisioning · lane `AL-SCM-TESTENV` (INS) · E2

Agent: `devops-agent` (has Write). Depends on Q203 for its ledger row. Parallel-safe with P1, P2, P3.
Base Stack (R9): `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` — verify and report its SHA256; no record → STOP.

```
AGENT CONTRACT — Read first: .antigravity/agents/devops-agent.md (skills: mongodb-ops, docker-compose),
.antigravity/rules/dev-runbook.md, .antigravity/rules/mongo-indexing.md, .antigravity/rules/configuration-safety.md.
§17.4 GAP (F-8): the §17.4 table has NO devops-agent row, so no codified mandatory fields exist and G3 cannot be
satisfied as written. CT supplies them by analogy; report the line
"G3 satisfied by analogy — §17.4 has no devops-agent row (F-8)."
  target infrastructure = isolated single-node MongoDB replica set (transactions required)
  consuming tests = services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests
  config surface = environment variables ONLY: MOD0183_TEST_MONGO, MOD0184_TEST_MONGO, MOD0185_TEST_MONGO
  do not touch = test code · service code · any .csproj · appsettings · port 27017
  persistence = report whether the setup survives a machine restart
Scope check: ✓ devops-agent has Write; Allowed Paths is one record folder.
Allowed Paths (write): docs/records/audits/2026-10/mvp6-q205-testenv-01/**

Measured state, verified 2026-10-02 — do not re-derive: `dotnet test services/Diten.SupplyChainService/tests/
Diten.SupplyChainService.Tests/` → 139 total, 46 passed, 93 FAILED. Every failure throws
InvalidOperationException: "Isolated Mongo required". Sources: SourceIntakeTests.cs:42 and ShipmentTests.cs:30 read
MOD0183_TEST_MONGO; ShipmentTests.cs:31 states the requirement verbatim, "must point to an isolated replica set".
MOD0184_TEST_MONGO and MOD0185_TEST_MONGO are read too. A standalone mongod is live on 127.0.0.1:27017 — which is
exactly why the guard fires rather than passing.
THE GUARD IS CORRECT. Do not point these variables at 27017, do not relax the check, do not edit the tests. The suite
needs transactions (concurrency and failed-transaction cases); a standalone instance cannot satisfy them and must not
appear to.
Before creating anything: this repo has prior isolated-Mongo setups on non-default ports — a replica set named rsmod192
on 57192 has been used by capacity tests. Search for an existing isolated replica set and reuse it if present; report
what you found either way.
Task: (1) locate or stand up an isolated single-node replica set, initiated, transactions working. (2) Export the three
variables to it. (3) Re-run the suite and report the real pass/fail split. (4) Any failure SURVIVING provisioning is a
genuine defect and new information — list each separately with test name and assertion; never lump them. (5) Record
exact reproducible steps and restart survival.
Cannot reach E2 → say so plainly and report E1. Never claim E2 on a partial run.
Output: docs/records/audits/2026-10/mvp6-q205-testenv-01/ → SOP-22.md, PROVISIONING-STEPS.md, TEST-RESULTS-BEFORE-AFTER.tsv,
GENUINE-DEFECTS.tsv (empty file if none), ARTIFACTS.sha256.
```

## Dispatch order for the v2.0 set

1. **Now, two sessions:** P2 (ledger, single writer) ∥ P1 (Q201 dry-run, read-only).
2. **Once Q203 has added the rows:** P3 ∥ P4 join — four concurrent sessions, disjoint folders.
3. **Then serial:** Q201 CT ACCEPTED → Q202 alone, with no other `services/**` writer.
4. **Wave 3, separate worktrees:** MOD-0190 ∥ MOD-0192 core DEV from the existing HELD prompts in
   `docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/` (38 ∥ 43 owned paths, zero intersection).
   Needs a CT release first; SOP-22's blocking gate ("both packs draft") closed when the owner promoted both to
   `ready-for-dev` on 2026-09-22.

Open before dispatch: Q200 collides with P2 under §16.4 (fold in or run after), and the 0190/0192 CT release decision.
