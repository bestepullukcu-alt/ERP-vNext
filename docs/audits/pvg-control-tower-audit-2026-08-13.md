# PVG Control Tower Audit - 2026-08-13

> Audit date uses the local workspace date/time context. This audit uses the supplied Control Tower SOP and
> Operating Card from `/Users/natig/Downloads/` and the repository copies under `docs/control-tower/`.

## Executive Summary

PVG is no longer governance-only. The current repository has tracked `Diten.PvgService` class-library source and
tests for:

- MOD-0230 REG-PV-BASE intake guardrail contracts.
- MOD-0231 Case Processing signal-minimum contracts.
- MOD-0232 MedDRA Coding non-operational contracts.
- MOD-0234 Signal Management no-shell contract model.

The work is still **not operational runtime**. There is no PVG API host, no `Program.cs`, no appsettings, no Mongo
persistence, no Gateway route, and no PVG tenant UI. This matches the DCP/member-pack gate split: build/test is
open, operational runtime remains closed.

Control Tower verdict:

| Concern | Verdict |
|---|---|
| DCP authority | PASS - `DCP-004` is `approved` |
| Member pack authority | PASS - MOD-0230/MOD-0231/MOD-0232/MOD-0234 are `ready-for-dev` for build/test only |
| Runtime authorization | FAIL-CLOSED - operational runtime remains closed for every PVG member |
| Current code evidence | PASS for class-library contract tests; no API/UI/runtime evidence yet |
| Next planning action | Continue with a bounded MOD-0230 runtime-slice plan, not full PVG runtime |

## Sources Reviewed

- `/Users/natig/Downloads/control-tower-sop (2).md`
- `/Users/natig/Downloads/control-tower-operating-card (2).md`
- `docs/control-tower/control-tower-sop.md`
- `docs/control-tower/control-tower-operating-card.md`
- `AGENTS.md`
- `.antigravity/agents/orchestrator.md`
- `execution/domains/pharmacovigilance/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0232-meddra-coding.md`
- `execution/domains/pharmacovigilance/module-packs/MOD-0234-signal-management.md`
- `execution/domains/pharmacovigilance/work-packs/`
- `docs/plans/pvg-fast-track-execution-plan-2026-08-09.md`
- `docs/specs/pvg-reg-pv-base-port-contracts-v1.md`
- `services/Diten.PvgService/**`
- `gateway/Diten.ApiGateway/ocelot.json`
- `frontend/Diten.Web/Views/**` and PVG-specific frontend path search
- `execution/registries/module-id-registry.md`

## Current Repository Reality

| Area | Code reality | Audit judgment |
|---|---|---|
| PVG domain governance | Exists and authorizes only the DCP-004 build/test gate | PASS |
| DCP-004 | `approved`; operational runtime explicitly `NO-GO` | PASS |
| MOD-0230 pack | `ready-for-dev`; build gate open; runtime gate closed | PASS |
| MOD-0231 pack | `ready-for-dev`; non-operational class-library contracts/tests only | PASS |
| MOD-0232 pack | `ready-for-dev`; non-operational class-library contracts/tests only; no MedDRA data | PASS |
| MOD-0234 pack | `ready-for-dev`; no-shell contract/tests only | PASS |
| `Diten.PvgService` source | Tracked Domain/Application/Infrastructure class libraries and tests exist | PASS for build/test gate |
| PVG API host | No `Program.cs`, no API project, no appsettings | Correctly absent for current gate |
| PVG persistence | No Mongo repository/collection/index implementation found | Correctly absent for current gate |
| PVG Gateway route | No implemented route family found in `ocelot.json` | Correctly absent until API host exists |
| PVG tenant UI | No `Views/Pharmacovigilance/**`, JS, or resources found | Correctly absent until WP-07 |
| Control Tower docs | Repo copies exist; supplied docs align with them | Usable operating method |
| Worktree | Existing untracked PVG docs/work-packs are present | Treat as user/worktree state; do not overwrite casually |

## Evidence Collected

Identity gates:

| Module | Command result |
|---|---|
| MOD-0230 Case Intake & Triage | `OK MOD-0230: proven against Blueprint/registry.` |
| MOD-0231 Case Processing | `OK MOD-0231: proven against Blueprint/registry.` |
| MOD-0232 MedDRA Coding | `OK MOD-0232: proven against Blueprint/registry.` |
| MOD-0234 Signal Management | `OK MOD-0234: proven against Blueprint/registry.` |

Test evidence:

| Suite | Result |
|---|---|
| `Diten.PvgService.RegPvBase.Tests` | Passed: 31 |
| `Diten.PvgService.CaseProcessing.Tests` | Passed: 21 |
| `Diten.PvgService.MeddraCoding.Tests` | Passed: 27 |
| `Diten.PvgService.SignalManagement.Tests` | Passed: 33 |

Note: an initial parallel test run hit shared build-output file locks. Serial reruns passed. Future Control Tower
verification should run these PVG suites serially or with isolated output paths.

## Findings

### F1 - The prior 2026-08-09 replanning audit is stale

The older audit says DCP-004 and member packs are draft and that no tracked PVG source exists. Current files show
DCP-004 approved, all four member packs ready for build/test, and tracked `Diten.PvgService` class-library code.

Impact: planning must now start from measured class-library progress, not from a governance-only baseline.

### F2 - Current PVG code is class-library evidence, not runtime acceptance

`Diten.PvgService` currently contains Domain/Application/Infrastructure class libraries and tests. It does not
contain an API host, runtime listener, appsettings, persistence adapter, Gateway route, or UI.

Impact: current evidence is E2-level for contracts/tests. It is not E3/E4 runtime evidence.

### F3 - MOD-0230 is the only member eligible for the next runtime slice

MOD-0230 authorizes backend/tests/gateway/tenant UI under the build/test gate. MOD-0231, MOD-0232, and MOD-0234
are limited to non-operational class-library contracts/tests only.

Impact: the next implementation plan must focus on MOD-0230 API/persistence/UI and keep downstream modules as
contract-only consumers.

### F4 - Operational runtime remains closed by design

The blockers are still real: real MOD-0019, MOD-0023, MOD-0031, retention/legal-hold ownership, and downstream
licensing/data-product/metric gates are not closed.

Impact: even after a local MOD-0230 vertical slice runs, it must not be represented as production/validation-ready.

### F5 - Work-pack plan and current code have drift

The work-pack README still describes WP-01 through WP-08 as "Ready" or blocked, but the repository already has
tracked class-library code and tests corresponding to WP-01 and parts of downstream class-library work. WP-02 in
the old work-pack shape expects an API project that does not exist yet.

Impact: the continuation plan should re-baseline work packages rather than dispatching the old WP sequence blindly.

### F6 - No PVG frontend or Gateway route exists

Searches for PVG-specific view/resource/JS paths found no `frontend/Diten.Web/Views/Pharmacovigilance/**`,
`wwwroot/assets/js/Pharmacovigilance/**`, or `Resources/Views/Pharmacovigilance/**`. `ocelot.json` has no
implemented MOD-0230 route family.

Impact: frontend and Gateway work remain future steps, dependent on a concrete API host and controller.

### F7 - The local worktree has untracked planning artifacts

Untracked files/directories include PVG audits/plans/specs/work-packs and an OD decision-record artifact.

Impact: any continuation work must preserve these files and either stage them deliberately later or keep them as
local planning state. They should not be deleted or rewritten as cleanup.

## Control Tower Gate Assessment

| Gate | Status | Notes |
|---|---|---|
| DoR identity + owner | Partial | IDs pass; MOD-0231/MOD-0232/MOD-0234 owners remain TBD in packs |
| Pack authority | PASS | DCP/member build-test gates are open |
| Contract | PASS for class-library contracts; partial for MOD-0230 runtime | API/persistence/UI contract still needs re-baselined WP |
| Dependencies | Partial | Build/test ports pass; operational dependencies remain blockers |
| Scope | PASS if MOD-0230 only | Other members must stay class-library-only |
| Git isolation | Partial | Worktree has unrelated and untracked planning files |
| Execution profile | Profile B now; Profile A later | Current evidence is backend/contract; UI/state-changing waits for API |
| Acceptance evidence | E2 achieved for class-library tests | E3/E4 not achieved |

## Recommended Next Move

Create a new Control Tower development plan that:

1. Freezes current class-library evidence as the new baseline.
2. Defines MOD-0230 API + persistence as the next single-writer backend lane.
3. Keeps Gateway and UI behind API readiness.
4. Keeps MOD-0231/MOD-0232/MOD-0234 as contract-only verification lanes.
5. Requires serial PVG test execution or isolated build outputs.
6. Records that operational runtime remains closed after local/dev/CI build-test success.
