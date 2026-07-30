# MOD-0117 Phase 2A Implementation Audit — 2026-07-29

## 1. Audit status

This document records the repository state produced under the scoped
[MOD-0117 module pack](../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md).
It is an implementation audit and API summary with scoped runtime evidence,
not production authorization or production-readiness acceptance.

- Phase 2A evidence promotes the module pack to `review`, not `done`.
- The implemented scope is Phase 2A Context & Referenceability Core only.
- The temporary browser-to-database chain is evidenced; production deployment,
  real AuthService PPM grant provisioning, and production-environment
  replica-set rollback readiness are not claimed.
- Phase 2B, the typed ExternalContextReference provider, MOD-0354/DWS runtime integration, WorkCenter integration, and all four protected WorkCenter hazards remain excluded.

## 2. Implemented repository surfaces

### Backend service

The new [Diten.PpmService solution](../../services/Diten.PpmService/Diten.PpmService.sln)
contains the required API, Application, Domain, Persistence, and Infrastructure
projects plus a test project. The implementation includes:

- Portfolio, Initiative, Program, and Project domain entities;
- CQRS commands, queries, handlers, validators, and application services;
- MongoDB repositories, tenant-first indexes, optimistic concurrency, soft
  deletion, and a transaction health check;
- a producer-local `ppm_audit_intents` collection written in the same MongoDB
  transaction as each mutation;
- authenticated API controllers under `/api/v1/ppm/{resource}`;
- a fail-closed effective-permission adapter.

The service is configured for local port `5061`. That port is a backend
process boundary; it is not a browser-facing endpoint.

### Frontend

The implementation adds:

- tenant hub `/ppm`;
- `/ppm/portfolios`;
- `/ppm/initiatives`;
- `/ppm/programs`;
- `/ppm/projects`.

The controller is [PpmController](../../frontend/Diten.Web/Controllers/PPM/PpmController.cs).
It accepts only the four closed resource names and forwards API calls to
`GatewayUrl/api/v1/ppm/{resource}`. The frontend therefore targets Gateway
port `5000`; it contains no direct browser call to service port `5061`.

The pages use `_LayoutTenantShell`, DataTable v2 markup, Slim-style Index
create/edit offcanvas surfaces, details quick views, lifecycle badges, and
localized resources for the seven required languages. These files are
code-reality evidence only. They are not proof that the UI can complete a live
request while the gateway route and permission integration remain absent.

## 3. Entity and lifecycle summary

All four aggregates use a PPM-local entity base with `Guid` identity and
tenant identity, scalar UTC timestamps, soft-delete fields, actor audit fields,
and integer optimistic-concurrency versioning. `Code` and `Name` are normalized
with trim plus NFC before persistence. `VisibilityPolicyKey` is rejected while
its authoritative MOD-0018 validation path is unavailable.

| Entity | Parent rule | Initial state | Allowed forward transitions | New-reference states |
|---|---|---|---|---|
| Portfolio | None | `Draft` | `Draft → Active`; `Draft/Active → Archived` | `Draft`, `Active` |
| Initiative | Optional Portfolio | `Proposed` | `Proposed → Active/Cancelled`; `Active → OnHold/Completed/Cancelled`; `OnHold → Active/Completed/Cancelled` | `Proposed`, `Active`, `OnHold` |
| Program | Optional Portfolio | `Draft` | `Draft → Active/Cancelled`; `Active → OnHold/Completed/Cancelled`; `OnHold → Active/Completed/Cancelled` | `Draft`, `Active`, `OnHold` |
| Project | Exactly one Initiative XOR Program, represented by `ParentType + ParentId` | `Draft` | `Draft → Planned/Cancelled`; `Planned → Active/OnHold/Cancelled`; `Active → OnHold/Completed/Cancelled`; `OnHold → Active/Completed/Cancelled` | `Draft`, `Planned`, `Active`, `OnHold` |

Referenceability is derived from tenant visibility, soft-delete state, and the
closed lifecycle state. It is not persisted as a mutable authorization flag.
Terminal parent records cannot receive new references. Cross-tenant and
non-visible lookups are deliberately indistinguishable from absence.

## 4. API contract summary

Each of `portfolios`, `initiatives`, `programs`, and `projects` exposes the
same Phase 2A route shape:

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/ppm/{resource}` | Tenant-scoped active list |
| `GET` | `/api/v1/ppm/{resource}/{id}` | Tenant-scoped detail |
| `POST` | `/api/v1/ppm/{resource}` | Create |
| `PUT` | `/api/v1/ppm/{resource}/{id}` | Metadata/parent update with expected version |
| `POST` | `/api/v1/ppm/{resource}/{id}/lifecycle` | Closed lifecycle transition with expected version |
| `DELETE` | `/api/v1/ppm/{resource}/{id}?expectedVersion={version}` | Soft delete |

The API uses the repository `Response<T>` envelope and string enum values;
integer enum values are rejected. The implemented error boundary is:

- `400` — malformed input or validation failure;
- `401` — absent or invalid authentication;
- `403` — authenticated command lacks the required MOD-0018 permission result;
- `404` — missing, soft-deleted, cross-tenant, or otherwise invisible entity/reference;
- `409` — duplicate tenant-scoped code, invalid lifecycle transition, or stale concurrency version;
- `503` — required Mongo transaction capability is unavailable.

No ExternalContextReference validation endpoint, provider interface, route,
transport, or cache has been implemented.

## 5. Persistence and audit behavior

The implementation defines `ppm_portfolios`, `ppm_initiatives`,
`ppm_programs`, `ppm_projects`, and `ppm_audit_intents`. Active codes use
tenant-first partial unique indexes. Reads and mutations are constrained by
tenant and soft-delete state; mutation replacement additionally checks the
expected version.

Every mutation and its producer-local technical audit intent are executed
inside one Mongo session transaction using snapshot read concern, majority
write concern, and primary read preference. Failure to create or commit the
transaction aborts the operation and is mapped to `503` where the failure means
replica-set transaction support is unavailable.

This local record is not the MOD-0021 business audit system of record.
Versioned delivery, durable at-least-once publication, idempotent consumption,
retry/dead-letter/alarm handling, and authorized replay remain blocked on the
MOD-0021 cross-service contract. No exactly-once claim is made.

## 6. Authorization posture

The application declares closed `ppm.{resource}.{action}` permission names and
routes all reads and mutations through `IEffectivePermissionEvaluator`. A
shared signed-JWT evaluator and the PPM adapter now implement the reusable
in-process enforcement path; their focused suites pass **21/21** and **17/17**
respectively.

This does not establish production grant readiness. The real AuthService
tenant-administrator token used during verification contained **zero** PPM
permissions. The successful end-to-end smoke used an ephemeral,
environment-only signed JWT bootstrap. It did not alter repository seed data,
AuthService data, roles, grants, or tenant configuration. Actual PPM permission
grant provisioning therefore remains open and must not be inferred from the
smoke result.

The service does not calculate roles, grants, or effective permissions and
does not repurpose `IEntitlementChecker` as a permission engine. Missing tenant
or actor context also fails closed.

## 7. Test and validation evidence

Repository test sources cover:

- lifecycle and terminal-state rules;
- Project parent identity requirements;
- referenceability;
- enum string serialization and numeric-enum rejection;
- fail-closed authorization;
- mutation/audit transaction coupling;
- frontend route allowlisting, Gateway-only targeting, tenant shell, and
  DataTable v2 markup.

Final automated results after remediation were:

- shared signed-JWT evaluator (`Diten.Platform.Common`): **21/21 PASS**;
- PPM service and adapter tests: **17/17 PASS**;
- targeted Gateway PPM route test: **1/1 PASS**;
- isolated Mongo replica-set integration tests: **9/9 PASS**;
- `Diten.Web.Tests`: **22/22 PASS**;
- delegated jsdom interaction check: **PASS**.

The full Gateway suite still contains a pre-existing HCM failure unrelated to
MOD-0117. The focused PPM route result is therefore reported separately and is
not presented as a clean full-suite result.

A real authenticated browser smoke exercised this temporary chain:

`UI :5191 → Gateway :5200 → Diten.PpmService :5061 → Mongo replica set :27018`

It created Portfolio, Initiative, and Program records; created Projects with
both Initiative and Program parents; opened quick view; edited a record;
transitioned a Project from `Draft` to `Planned`; and soft-deleted a record.
The browser console reported **0 errors and 0 warnings**. The temporary gateway
and Mongo ports are verification-only; the governed production-facing browser
boundary remains Gateway `5000`, and service port `5061` remains non-browser
facing.

The successful smoke used the ephemeral signed JWT bootstrap described in
§6. It is real UI-to-database CRUD evidence, but it is not evidence that
AuthService currently provisions the required PPM grants.

DataTable verification now reports **60 PASS / 4 known policy/profile false
positives per resource surface**. The four remaining findings are recorded as
verifier-policy/profile incompatibilities rather than silently treated as
passes.

## 8. Remaining blockers

| Blocker | Current result |
|---|---|
| AuthService PPM grant provisioning | **OPEN.** Shared JWT evaluation and the PPM adapter are evidenced, but the real tenant-admin token has zero PPM permissions; the smoke bootstrap was ephemeral and changed no Auth data. |
| Gateway `/api/v1/ppm/**` route | **EVIDENCED FOR PHASE 2A.** The targeted route test passes 1/1 and the temporary real chain reached PPM through Gateway; the unrelated pre-existing HCM full-suite failure remains outside MOD-0117. |
| Mongo replica-set runtime proof | **EVIDENCED FOR PHASE 2A.** Isolated integration tests pass 9/9 and the real CRUD chain reached Mongo replica set `27018`. |
| MOD-0021 semantic audit contract and delivery | **BLOCKED.** Only producer-local transactional audit intent exists. |
| DataTable verifier | **PARTIAL.** Each resource surface reports 60 PASS and 4 known policy/profile false positives. |
| End-to-end UI/API acceptance | **EVIDENCED WITH AUTH CAVEAT.** Real browser CRUD passed through the full temporary chain with zero console errors/warnings, using an ephemeral signed JWT bootstrap rather than a real AuthService PPM grant. |
| ExternalContextReference provider and DWS integration | **EXCLUDED.** Requires a later explicit implementation authorization. |
| Phase 2B and MOD-0136/MOD-0138/MOD-0072 runtime links | **EXCLUDED.** No implementation authority in this slice. |
| WorkCenter projection or protected task/approval behavior | **EXCLUDED.** Any protected hazard requires Gate 2 before production changes. |

## 9. Closure statement

Phase 2A now has automated, isolated replica-set, and real browser-to-database
evidence sufficient to recommend `review`. The pack may move from `approved`
to `review`, but not `done`: AuthService PPM grant provisioning is still open,
the DataTable verifier retains four known policy/profile false positives per
surface, and MOD-0021 cross-service delivery remains blocked. Phase 2B, the
ExternalContextReference provider, DWS runtime integration, and WorkCenter
scope remain outside this review.
