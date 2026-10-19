# WP-MOD0188-INVALIDATION-CORE

Status: CT assigned; technical and manual acceptance pending. Module: MOD-0188
Demand Planning. Writer lane: existing `feature/sce/mod-0188-demand-planning`
checkout only.

## Goal

Add an internal, auditable invalidation core for Published or Superseded
revisions and the associated fail-closed planning-read boundary. This is a
bounded AC-D20-D22 producer slice, not final acceptance or public Demand v2.

## Scope

- Require independent server-side proof of `demand.plans.invalidate`, current
  actor Tenant/LegalEntity/series scope, and material downstream MRP impact.
  A client boolean, arbitrary reason string or forecast error alone is not
  impact proof. Production evidence adapter remains fail-closed until the
  authoritative source is approved; tests use explicit fixtures.
- Record nonblank reason, impact, evidence reference, actor, server time and
  monotonically incremented state version. In one Mongo transaction, update
  lifecycle metadata in the Draft and Published manifests, write mandatory
  audit and one outbox intent. Published content and checksum never change.
  A same-key/same-content retry has one effect; conflicting/stale requests
  fail without partial effects.
- A current Published revision made Invalidated leaves no usable current
  baseline for that period. Never reactivate an older Superseded revision;
  future replacement requires a new Approved revision and the full normal
  publish path. This is the pack's no-automatic-fallback rule, not a rollback.
  Preserve historical content for an authorized audit-only read with an
  explicit unusable-for-planning warning. Normal planning snapshot/status
  and page reads must reject Invalidated content.
- Ensure the existing publish core can subsequently accept a new Approved
  revision for that period without reviving Invalidated content. Protect
  simultaneous invalidation, publication and repeat attempts with tested
  fencing/unique-slot behavior.

## Boundaries

No public API, Gateway, frontend, MOD-0189 consumer, live cross-service call,
event transport or Demand v2 freeze. No direct state rollback, deletion,
automatic Superseded fallback or edit of Published rows. Do not touch
AGENTS.md, domain config, DCP, registry, frozen contracts, `.antigravity` or
other services. Owned v2 draft may be annotated but stays DRAFT. Do not claim
that fixture MRP impact evidence is live evidence. AC-D20-D22 remain open.

Only the existing worktree/branch. Preserve existing staged/unstaged and
central user changes. No new branch/worktree, push or PR; no broad staging.

## Verification and gates

Use an owned temporary loopback Mongo replica set on a measured free allowed
port, never 27017. Test Published and Superseded invalidation, authorization,
material-evidence absence, natural forecast deviation, immutability/checksum,
monotonic state, current-baseline removal, no old fallback, next normal publish,
idempotency, stale versions, concurrent invalidation/publish and mandatory
audit/outbox failure rollback. Include audit-only warning and planning-read
denial. Run focused tests, API build and full MOD-0188 suite with zero skips;
show one red-then-green sensitivity check. Report totals and owned PID/port/
folder cleanup. CT K13 and Ali E4 remain separate.

Stop and report if a frozen-contract break, data-loss risk, protected/shared
central edit, or unresolvable material-impact authority is required. Complete
safe independent work first; do not invent live evidence or business rules.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, Module Pack,
domain config, AGENTS.md, add-module workflow and relevant backend, data,
security, integration, testing and code-quality role/rule files. Reading role
files in one chat is not running subagents; report the distinction honestly.
