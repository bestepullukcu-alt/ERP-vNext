# WP-MOD0188-SNAPSHOT-READ

Status: CT assigned; technical and manual acceptance pending. Module: MOD-0188
Demand Planning. Writer lane: existing `feature/sce/mod-0188-demand-planning`
checkout only.

## Goal

Complete the internal, revision-pinned snapshot/status read core for the newly
published manual revisions. This is a bounded AC-D23/D24 producer slice; it is
not public Demand v2, MOD-0189 consumption or final acceptance.

## Scope

- Reuse the immutable Published manifest/parts and checksum. Read the manifest
  and all expected parts from one consistent Mongo snapshot before returning
  any page. A missing, duplicated, corrupted or scope-mismatched part/row,
  count or checksum fails the whole request, never a partial success.
- Define an internal page cursor pinned to Tenant, LegalEntity, revision,
  checksum/manifest version, stable SKU x Warehouse x week order and next
  position. Reject changed revision, scope, order, cursor tampering, expiry or
  invalid state; no merging pages from different revisions. Document the
  technical cursor scheme in MOD-0188-owned evidence, not central contracts.
- Use explicit authoritative fixture authorization for each read. Production
  authority remains fail-closed. Normal planning reads allow Published only.
  Superseded bound-run evidence has no approved contract yet, so reject those
  reads for now; do not invent MRP binding evidence or a bypass. Invalidated
  content is not returned through planning reads.
- Preserve actual zero versus empty demand, missing and unknown semantics in
  the internal DTO. Do not turn absent rows into zero or claim a full plan
  when a selected series has incomplete rows. Read-only operations must not
  mutate manifest, rows, audit, outbox or state.

## Boundaries

No public API, Gateway route, event delivery, MOD-0189 code, frontend, live
cross-service call, invalidation implementation or Demand v2 freeze. Do not
touch AGENTS.md, domain config, DCP, registry, frozen contracts, `.antigravity`
or other services. Demand v2 owned draft may be annotated but stays DRAFT.
Published core currently covers manual rows; method-generated provenance is
not accepted by this WP. AC-D23/D24 remain open for live authority, central
contract review and complete end-to-end producer tests.

Only the existing worktree/branch. Preserve all staged/unstaged and central
user changes. No new branch/worktree, push or PR. No broad staging or commit.

## Verification and gates

Use an owned temporary loopback Mongo replica set on a measured free permitted
port, never 27017. Focused tests must cover page continuity, stable ordering,
zero semantics, scope/revision/cursor tampering, fail-closed Superseded reads,
Invalidated rejection and each integrity failure. Run API build and full
MOD-0188 suite with zero skips, plus one red-then-green test-sensitivity check.
Report source scope, totals, limitations and owned PID/port/folder cleanup.
CT K13 and Ali E4 remain separate. Stop for frozen-contract break, data-loss
risk or a protected/shared central edit; do not fabricate binding authority.

## CT review finding

The writer reports 236/236 with a temporary Mongo; CT has not independently
rerun that suite. Source inspection found an authorization-order defect in
`PublishedSnapshotPageReader.ReadAsync`: snapshot integrity/state outcomes are
returned before actor scope is verified, and an existing out-of-scope record
returns `ScopeDenied` rather than the pack's 404/empty result. This can expose
record existence/state/corruption through differing outcomes. Fix the boundary
for status, manifest and page reads before treating this slice as K13-ready.
An authority outage must still fail closed, and a real missing Demand read
permission must remain distinct from a scope denial. Do not claim that simply
moving the state check after the current full snapshot read resolves the
integrity-outcome leak; verify ordering and scope in focused negative tests.

The writer subsequently reports 238/238 with a temporary Mongo after the
correction. CT source review confirms revision-bound authority is now checked
before Mongo access and same-tenant out-of-scope reads return `NotFound` on all
three surfaces. CT has not independently rerun the real-Mongo suite; live
revision-scope authority and full K13/AC acceptance remain open.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, Module Pack,
domain config, AGENTS.md, add-module workflow and relevant backend, data,
security, integration, testing and code-quality role/rule files. Reading role
files in one chat is not running subagents; report the distinction honestly.
