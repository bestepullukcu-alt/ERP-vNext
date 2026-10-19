# WP-MOD0188-V2-COMMAND-API

Status: CT assigned; technical and manual acceptance pending. Module: MOD-0188
Demand Planning. Writer lane: existing `feature/sce/mod-0188-demand-planning`
checkout only.

## Goal

Expose thin Demand v2 DRAFT publish and invalidate command endpoints over the
existing internal Mongo cores. This is a bounded producer API slice, not live
permission integration, Demand v2 freeze or AC-D17-D22 completion.

## Scope

- Match the owned `contracts/drafts/demand-v2.openapi.yaml` paths, request
  fields, idempotency header, state/content version preconditions and outcome
  codes. Reconcile DRAFT success/error envelopes with existing `Response<T>`
  and `CustomBaseController`; do not silently diverge from the draft.
- Thin controller + separate CQRS commands, validators and handlers. Actor
  and Tenant come from verified server context; selected LegalEntity is an
  untrusted hint rechecked by the independent authority. Do not accept
  client-supplied actor, scope, verified-impact or approval flags.
- Publish requires `demand.plans.publish`, a still-valid Approved revision,
  separation of duties, full manual-row integrity and atomic store behavior.
  Method-generated weeks stay blocked until verified provenance exists.
  Invalidate requires `demand.plans.invalidate`, revision-bound scope and
  independently verified material MRP-impact evidence. Natural forecast
  deviation alone never qualifies. Keep idempotent replay and conflict clear.
- Preserve the existing production hard-deny publish policy and unconfigured
  authority adapters. Successful API behavior can be tested only by explicit
  test-only policy/authority fixtures. Do not weaken default runtime security
  to make a test pass. Neither endpoint may emit a delivered event directly;
  only the existing transactional outbox intent is written.

## Boundaries

No Gateway, frontend, MOD-0189, live cross-service calls, event transport,
Demand v1 changes or central contract freeze. No edits to AGENTS.md, domain
config, DCP, registry, frozen contracts, `.antigravity` or other services.
Owned v2 OpenAPI may change but remains DRAFT. Do not mark any AC complete.

Only the existing worktree and branch. Preserve prior staged/unstaged and
central user changes. No new branch/worktree, staging, commit, push or PR.

## Verification and gates

Use an owned temporary loopback Mongo replica set on a measured free permitted
port, never 27017, and a loopback HTTP host with real JWT/routing/middleware.
Test default production fail-closed policy, fixture-authorized publish and
invalidate success, permission/scope loss, missing/invalid idempotency key,
stale versions, same/different replay, creator/editor/integration actor,
missing material evidence, audit/outbox rollback and resulting status/read
behavior. Verify OpenAPI parse/internal refs/unique operation IDs and request,
response and error-code agreement. Run focused tests, API build and full
MOD-0188 suite with zero skips; show one red-then-green sensitivity check.
Report totals, remaining live gates and owned PID/port/folder cleanup. CT K13
and Ali E4 remain separate.

Stop for frozen-contract break, data-loss risk, protected/shared central edit
or unresolved business/security rule. Finish safe independent work first.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, Module Pack,
domain config, AGENTS.md, add-module workflow and relevant backend, data,
security, integration, testing and code-quality role/rule files. Reading role
files in one chat is not running subagents; report the distinction honestly.
