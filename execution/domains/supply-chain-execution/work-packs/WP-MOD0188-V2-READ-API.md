# WP-MOD0188-V2-READ-API

Status: CT assigned; technical and manual acceptance pending. Module: MOD-0188
Demand Planning. Writer lane: existing `feature/sce/mod-0188-demand-planning`
checkout only.

## Goal

Wire the existing producer-side status, manifest, revision-pinned pages and
audit-only Invalidated history readers to thin Demand v2 DRAFT API endpoints.
This is a bounded AC-D22-D24/D27 delivery slice, not contract freeze, Gateway
release, live MRP integration or full acceptance.

## Scope

- Compare each endpoint, response and error mapping against the owned
  `contracts/drafts/demand-v2.openapi.yaml`. Update only that owned DRAFT if
  needed, identify any unresolved central decision and never self-freeze.
- Status: authorized `demand.plans.consume` reader can observe Published,
  Superseded and Invalidated state/version without forecast rows. Unknown or
  inconsistent state must not be reported as Published.
- Manifest/pages: use complete snapshot integrity checks and signed,
  revision/scope-bound cursor. Normal content read is Published only.
  Superseded content remains closed until an approved bound-MRP proof exists;
  Invalidated content is always closed to planning reads.
- Separate audit-only Invalidated history endpoint requires `demand.audit.read`
  and revision-bound scope, with a prominent localized/unambiguous warning;
  it must never be routable or reusable as planning consumption. Audit rows
  should be bounded and revision-pinned, not an unbounded response.
- Thin controllers, CQRS/Response<T>/CustomBaseController, JWT/RBAC and
  server-side permission/scope checks. No payload/header/cursor is authority.
  Same-tenant out-of-scope record returns 404/empty, missing operation
  permission 403, unavailable authority or integrity source fails closed.
  Preserve distinct 409/422/503 semantics as the DRAFT specifies.

## Boundaries

No Gateway route, frontend, MOD-0189 code, live cross-service adapter, event
delivery, publish/invalidate command API or frozen Demand v1 change. Do not
touch AGENTS.md, domain config, DCP, registry, frozen contracts,
`.antigravity` or other services. The service may run directly only in isolated
tests; no claim of public availability while authority adapters remain closed.
AC-D22-D24/D27 remain open for live authority, central v2 review and E4.

Only the existing worktree/branch. Preserve prior staged/unstaged and central
user changes. No new branch/worktree, staging, commit, push or PR.

## Verification and gates

Use fixture authority in API-level tests, plus an owned temporary loopback
Mongo replica set on a measured free permitted port, never 27017. Test all
three statuses, normal content and Invalidated denial, Superseded denial,
audit-only separation/warning, corrupt and missing snapshot, signed cursor,
permission/scope revocation between pages, source outage, cross-tenant and
same-tenant out-of-scope responses. Check OpenAPI syntax, internal references,
unique operation IDs and implementation-to-DRAFT route/shape agreement. Run
API build and full MOD-0188 suite with zero skips; include one red-then-green
negative check. Report totals and owned PID/port/folder cleanup. CT K13 and
Ali E4 remain separate.

## CT review after first writer report

The writer reports six focused and 263 total tests with an owned temporary
Mongo, but the API tests instantiate the controller directly; they do not
exercise real HTTP routing, JWT authorization policy, tenant middleware or
response serialization. Full HTTP/JWT behavior therefore remains unverified.
The new `PublishedRevisionManifest.PreparedAt` is required by current readers
but was not present in previously persisted development manifests. It is not
part of the existing canonical checksum bytes, so the current change can
reject old intact snapshots without a checksum-scheme change. Before K13,
prove legacy BSON compatibility with a real-Mongo fixture or establish that
no durable earlier records exist. Do not backfill, delete, rehash or silently
weaken integrity checks. Keep the v2 contract DRAFT and ACs open.

The writer later reports 267/267 real-Mongo tests and 0-warning API build.
CT source review found a legacy BSON test that unsets `PreparedAt`, preserves
the old checksum and resolves the time only through validated Draft creation
audit; a corrupt creation audit fails closed. The additional tests use a
loopback Kestrel host with signed JWTs and the actual authentication,
permission, tenant middleware and routing pipeline, though not production
Platform/MDM adapters or Gateway. Full independent CT Mongo rerun remains
open. The audit warning is still a bilingual backend string, not seven-language
user-facing localization; retain this as an E4/frontend gap.

Stop for frozen-contract break, data-loss risk, protected/shared central edit,
or an unresolved response/security rule. Complete independent safe work first.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, Module Pack,
domain config, AGENTS.md, add-module workflow and relevant backend, data,
security, integration, testing and code-quality role/rule files. Reading role
files in one chat is not running subagents; report the distinction honestly.
