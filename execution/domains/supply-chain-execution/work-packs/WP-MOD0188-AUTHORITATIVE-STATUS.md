# WP-MOD0188-AUTHORITATIVE-STATUS

Status: CT assigned; technical and manual acceptance pending. Module: MOD-0188
Demand Planning. Writer lane: existing `feature/sce/mod-0188-demand-planning`
checkout only.

## Goal

Provide a separate, authoritative internal revision-status read for authorized
consumers. It must reveal Published, Superseded or Invalidated lifecycle state
and monotonic stateVersion without granting snapshot content. This is a bounded
AC-D23/D27 and producer-status slice, not final Demand v2 or MOD-0189 code.

## Scope

- Apply independent actor permission and revision-bound Tenant/LegalEntity/
  selected-series scope proof before any database existence, corruption or
  state outcome. Same-tenant out-of-scope record returns empty NotFound;
  missing `demand.plans.consume` permission and unavailable authority remain
  separate fail-closed outcomes. Production authority stays unconfigured.
- Read lifecycle metadata consistently from the persisted Published manifest,
  manual revision state and durable transition audit/status evidence. Verify
  monotonic version and revision/scope identity. Do not infer status from a
  cursor, event, client flag or prior cached result. If the authoritative
  state cannot be reconciled, return closed error, never a guessed Published.
- For authorized callers, return Invalidated and Superseded status metadata
  even though normal content reads reject those states. Invalidation must be
  observable when the underlying content is corrupt; do not require a valid
  full part checksum merely to report an already durable Invalidated state.
  This is status only, not historical content/MRP binding authorization.
- Keep `PublishedSnapshotPageReader` normal content behavior unchanged.
  If its current `ReadStatusAsync` name conflicts with the new meaning,
  separate interfaces clearly and update internal callers/tests without
  silently changing public v2 semantics.

## Boundaries

No public API, Gateway, MOD-0189 consumer, frontend, event delivery, live
cross-service call or Demand v2 freeze. Do not alter Published content,
checksum or lifecycle state through the read. No edits to AGENTS.md, domain
config, DCP, registry, frozen contracts, `.antigravity` or other services.
Owned v2 draft may be annotated but stays DRAFT. AC-D23/D27 and live status
acceptance remain open.

Use only the existing worktree and branch. Preserve existing staged/unstaged
and central user changes. No new branch/worktree, staging, commit, push or PR.

## Verification and gates

Use an owned temporary loopback Mongo replica set on a free permitted port,
never 27017. Test authorized Published, Superseded and Invalidated status;
invalidated-with-corrupt-content; missing/mismatched audit or stateVersion;
out-of-scope healthy/invalidated/corrupt indistinguishability; permission and
authority outages; concurrent transition observation; and no content leak.
Run focused tests, API build and full MOD-0188 suite with zero skips. Show one
red-then-green sensitivity check. Report totals, remaining live authority
gap and owned PID/port/folder cleanup. CT K13 and Ali E4 remain separate.

Stop on frozen-contract break, data-loss risk, required protected/shared
central edit or an unresolved authoritative-status invariant. Complete safe
independent work first; do not fabricate MRP binding evidence.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, Module Pack,
domain config, AGENTS.md, add-module workflow and relevant backend, data,
security, integration, testing and code-quality role/rule files. Reading role
files in one chat is not running subagents; report the distinction honestly.
