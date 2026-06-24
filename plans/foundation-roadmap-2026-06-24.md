# Foundation Forward Plan — 2026-06-24

> Owner-approved sequencing for the platform-foundation work that must land before vertical modules (HR/CRM, MOD-0297+).
> Companion to the code-truth status tracker: [`execution/registries/module-implementation-status.md`](../execution/registries/module-implementation-status.md).
> Operating model: Claude = CONTROL TOWER (writes prompts, read-only verifies live; does not author product code). Execution = Antigravity agents.

## Where we are (this milestone — committed on `chore/blueprint-v7-reconciliation`)
- **Blueprint v7 reconciliation**: 15 files repointed master 5 → master 7 (`verify_module_id.py` works); new code-truth status tracker + a maintenance rule (module-pack-standard) that keeps it updated.
- **MOD-0288 Organization/Person/Position** — tenant-side, FULL e2e live-proven (LE→OrgUnit→Position→reports-to→manager-chain→assignment). Closed the audit drift ("registry done / no UI"). Org endpoints opened to `tenant_user` (gateway + Platform.Common middleware); permissions seeded+granted (auth DataSeeder).
- **MOD-0220 Legal Entity — minimal tenant-side slice** (create/activate/list) to unblock the OrgUnit FK. **MDM read-back bug fixed** (missing `clientSettings.GuidRepresentation = Standard`) + real-Mongo round-trip regression test.
- **3 hidden bugs caught by live verification** that build/test/docs all missed: permissions never registered; org backend rejected tenant_user; MDM reads fully broken. (Validates the "verify from code/live, not from docs" discipline.)

## Sequencing (owner-approved)

### NOW — parallel, not "build"
- **0a. Commit + merge this milestone** (done as of this file). Natural, tested, e2e-green stopping point.
- **0b. Get MOD-0023 PUSHED to the repo.** Another developer holds MOD-0023 (~80% per their analysis) but **currently cannot push** (blocker: repo write access? branch conflicts? clarify git-push vs deploy). Resolve in parallel — it gates the workflow/task/WorkCenter chain. Zero build cost to us.

### Build order
1. **MOD-0285 Navigation menu loader (runtime slice)** — FIRST. Deps ready (MOD-0288 ✅, entitlement ✅, permission ✅); small, low-risk, high-leverage: the org/legal screens + ALL future HR/CRM/MOD-0220 screens auto-appear in the tenant menu → ends manual `_LayoutTenantShell` editing forever. Decisions taken earlier: augment (keep hardcoded sections, add a dynamic descriptor-driven section); platform-template descriptor ownership; governed-publish/versioning deferred (needs MOD-0023).
2. **MOD-0220 full — Phase 1 (rich Legal Entity register)** — manager-requested (prototype drawn). Extend the existing minimal entity → rich (Legal Form, Organization Role, Country, Base Currency, Parent Entity, registered address, 3 status fields). Reference data: Country/Currency reuse existing Platform lookups; **Legal Form + Organization Role must be created** (operator-managed lookups, like Domain/Service Management). Keep `mdm_legal_entities` collection + OrgUnit FK working (`IsReferenceable` → `OperationalStatus == ACTIVE`). **Defer** evidence-gated lifecycle (IN_REVIEW→APPROVED, evidence COMPLETE) and relationships/corporate-actions/filings to Phase 2 (depend on MOD-0023/MOD-0031).

### Blocked on MOD-0023 push — PARK until 0b resolves
3. **MOD-0023 Workflow integration** — once pushed: permission seed/grant (the named gap, same class that bit MOD-0288), recurring-escalation job, per-user task query; verify. Opens the approval-gate for all modules.
4. **MOD-0024 Task & Checklist Engine** — the task/inbox engine; sits on MOD-0023.
5. **WorkCenter wiring** — connect the existing frontend prototype (currently mock) to MOD-0023 + MOD-0024. Owner's personal module; the payoff. Inbox = ApprovalTask (MOD-0023) + Task/Checklist (MOD-0024).

### If MOD-0023 stays blocked — pull HR-greenfield forward (all independent)
6. **MOD-0031 Evidence** (also unblocks MOD-0220 Phase 2 evidence gating) → **MOD-0019 Data Masking / Row-Field Security** (HR salary/PII) → **MOD-0028 Document Management**. HR prerequisites; greenfield (~0% today).
7. **Partial-finishers + quality debt** (interleave): MOD-0017-FU01 tenant security self-service UI · MOD-0033 quota override UI · MOD-0018-FU13 cache invalidation · DevEnablement/Gateway tests · EnterpriseStrategy l10n (91 views) + MOD-ID assignment.

→ Then **HR/CRM verticals** once the foundation is complete.

## Dependency notes
- **WorkCenter** = MOD-0023 (approvals) + MOD-0024 (tasks). MOD-0023 alone does NOT finish the task side.
- **MOD-0023 candidate routing** consumes MOD-0288 position data (now built) — tenant `position_assignments` must be populated or "candidate required".
- **MOD-0220 full lifecycle** + **HR sensitive-access** + **HR doc/evidence** all converge on MOD-0031 Evidence + MOD-0023 Workflow.
- **MDM dev quirk**: no `launchSettings` → run with `ASPNETCORE_ENVIRONMENT=Development` (else its dev JWT secret isn't read). DB = `DitenERP_Dev`.

## One flex point
If the manager needs MOD-0220 urgently, swap 1↔2 (MOD-0220 first, manual menu for now, MOD-0285 after). Efficiency favors MOD-0285 first.
