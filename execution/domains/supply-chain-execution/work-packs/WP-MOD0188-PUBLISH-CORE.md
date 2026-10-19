# WP-MOD0188-PUBLISH-CORE

Status: CT assigned; technical and manual acceptance pending. Module: MOD-0188
Demand Planning. Writer lane: existing `feature/sce/mod-0188-demand-planning`
checkout only.

## Goal

Build the internal Approved-to-Published core and atomic replacement of the
same official planning-period baseline. This is a bounded AC-D17-D19 producer
slice, not completion of those criteria or release of Demand v2.

## Scope

- Reuse the current Draft/review Mongo transaction and audit patterns. A
  publish candidate must still be Approved at the expected content/state
  versions. Check `demand.plans.publish`, server-verified Tenant/LegalEntity/
  data scope, actor identity and separation of duties: creator or any material
  editor and an integration actor cannot publish; an independent approver may.
- Verify every selected, non-excluded SKU x Warehouse series and its 52 weeks;
  reject missing, duplicate, corrupted or scope-mismatched parts and rows.
  Compute a canonical, reproducible integrity checksum, expected part/row
  counts and a revision-bound immutable manifest. Document the canonical
  serialization, algorithm and ordering in MOD-0188-owned evidence; do not
  claim central v2 freeze.
- In one real Mongo transaction, create the immutable Published snapshot,
  transition the prior current Published revision of the same Tenant x
  LegalEntity x first-local-week planning period to Superseded, advance state
  versions, record mandatory audit and one outbox intent. Protect concurrent
  publishes from different cycles/as-of/calendar versions. Distinct first
  local weeks remain distinct periods even when 52-week horizons overlap.
- A same-key/same-content retry has one effect; changed-content and stale
  version requests conflict. No partial Published, Superseded or outbox effect
  on any failure. Expose only an internal service/store contract and tests in
  this WP; defer public API, Gateway, event delivery and MRP consumption.

## Boundaries

No edits to AGENTS.md, domain config, DCP, registry, frozen contracts,
`.antigravity`, frontend, Gateway, other services or MOD-0189. Owned Demand v2
draft may be annotated only if necessary and must stay DRAFT. Do not invent
Platform/MDM/LOCATION authority or claim live acceptance. Do not add rollback,
invalidation, historic consumer read or automatic deletion. AC-D17-D26 stay
open pending CT K13, v2 contract and Ali E4.

Use only the existing worktree and branch. Preserve all prior staged/unstaged
changes. No new branch/worktree, push or PR. No commit or staging unless the
MOD-0188 scope can be individually isolated without touching prior state.

## Verification and gates

Run focused tests, API build and the full MOD-0188 suite on an owned temporary
loopback Mongo replica set at a measured free permitted port, never 27017.
Test permissions/SoD, 52-week and part corruption, deterministic checksum,
same-period competing cycles, distinct-period overlap, prior snapshot
immutability, audit/outbox failure rollback, idempotency and stale versions.
Show one targeted red-then-green sensitivity check without leaving weakened
source. Clean only the owned PID/port/folder; report totals and skips.

If a frozen contract break, data-loss risk or protected/shared central edit is
required, stop that path and report it. Do not guess a new business rule.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, the Module Pack,
domain config, AGENTS.md, add-module workflow and relevant backend, data,
security, integration, testing and code-quality role/rule files. Reading role
files in one chat is not running subagents; report the distinction honestly.
