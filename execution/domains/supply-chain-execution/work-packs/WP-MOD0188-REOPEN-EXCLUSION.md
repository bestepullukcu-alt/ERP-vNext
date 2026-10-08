# WP-MOD0188-REOPEN-EXCLUSION

Status: CT technical K13 verified; Ali E4 and AC-D15/D16 remain open. Module: MOD-0188 Demand Planning. Owner lane: existing
`feature/sce/mod-0188-demand-planning` checkout only.

## Goal

Expose the existing audited InReview/Approved -> Draft reopen transition and add
reasoned SKU x Warehouse series exclusion before review completes. This is a
bounded AC-D15/D16 core slice, not completion of either acceptance criterion.

## Scope

- PlanningService DemandPlanning domain, application, persistence, API and tests.
- Owned Demand v2 OpenAPI draft may change, but remains DRAFT.
- Reopen requires `demand.drafts.update`, verified actor and Tenant/LegalEntity/
  data scope, reason, idempotency key and expected content/state versions.
- Exclusion requires the same planner permission and scope checks. Record the
  excluded series, actor, reason, time, previous/new state and audit atomically.
  Reject exclusion after review completes; any allowed significant edit must
  invalidate prior review and return to Draft through an audited transition.
- A selected series must not silently disappear. Preserve its history and make
  inclusion/exclusion explicit in Draft read and the future manifest contract.
- Reuse the current Mongo transaction, candidate-slot and audit conventions.
  Failed, stale, conflicting or repeated requests must not create partial effects.

## Boundaries

Do not change AGENTS.md, domain config, DCP, registry, frozen contracts,
`.antigravity`, frontend, Gateway, other services or MOD-0189. Do not add live
cross-service calls. Existing Platform/MDM/LOCATION authorities remain
fail-closed; fixture evidence is not live acceptance. Do not implement publish,
invalidation or final manifest. Do not mark AC-D15/D16 complete.

Only one existing worktree/branch. Preserve all existing staged and unstaged
changes. Local commit is permitted only for individually verified MOD-0188
scope; never include central/user changes. No push or PR.

## Verification and gates

Test actor/permission/scope loss, reason, InReview and Approved reopen, stale
versions, repeated/conflicting keys, candidate-slot release, audit failure
rollback, excluded-series readback and 52-week completeness. Use only an
isolated loopback test replica set on a free permitted port and a test-named DB;
clean only the owned PID and temporary folder. Run full MOD-0188 tests with
zero skips and API build. Report test totals, source hash/checkpoint, changed
files and remaining AC-D15/D16 gaps. CT performs independent K13 review; Ali's
manual acceptance is separate.

Stop and report if this slice requires a frozen-contract break, data-loss risk,
or edit to a protected/shared central file. Complete unaffected independent
checks first.

## CT independent verification

- API/test project rebuilt from current source: 0 warnings, 0 errors.
- Full MOD-0188 suite on a CT-owned, loopback single-member replica set:
  188 passed, 0 failed, 0 skipped. This includes the audit-failure rollback
  test; no source mutation was performed by CT.
- The replica set used port 31994 and a CT-owned temporary folder outside the
  repository. Its owned PID 14832 stopped; the folder no longer exists and the
  port has 0 listeners. Operational Mongo ports were not used.
- Source inspection found the audited reopen, exclusion readback, version and
  idempotency boundaries in place. This verifies this technical slice only,
  not live Platform/MDM/LOCATION authority, a user screen, final manifest,
  all AC-D15/D16 edit sources, or Ali's manual acceptance.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, the MOD-0188 Module
Pack, domain config, AGENTS.md, add-module workflow and relevant backend,
security, data, integration and testing agent/rule files. Reading role files in
one chat is not running subagents; report the distinction honestly.
