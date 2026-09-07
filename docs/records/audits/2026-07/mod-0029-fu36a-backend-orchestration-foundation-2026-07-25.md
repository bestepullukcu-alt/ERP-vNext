# MOD-0029-FU36A Backend Orchestration Foundation

- Date: 2026-07-25
- Parent pack: `MOD-0029-FU36 Controlled Document Registration Orchestration`
- Pack status: `ready-for-dev`
- Delivery slice: backend foundation
- Frontend changed: **No**
- Ocelot changed: **No**
- Commit/push: **No**

## Outcome

FU36A backend foundation was implemented as a durable, tenant-scoped orchestration over the existing Controlled
Document, immutable version, Master Register and content-storage boundaries.

Delivered:

- `ControlledDocumentRegistrationOperation` and the approved eight-state enum;
- tenant-scoped repository and four MongoDB indexes;
- metadata-only retry snapshot and storage descriptor (never raw bytes);
- idempotent create and retry orchestration;
- Draft Master Register + ControlledDocument + first immutable version + link before `Completed`;
- safe content cleanup and soft-archive/reconciliation behavior for partial metadata;
- operation and reverse Master Register queries;
- thin MediatR controller with four approved endpoints;
- three dedicated AuthService catalog permissions;
- targeted Platform, Auth and Gateway tests;
- FU36A static verifier.

## Authorization

Create requires all four permissions:

1. `platform.document-management.master-register.registration.create`
2. `platform.document-management.master-register.manage`
3. `platform.document-management.master-register.link`
4. `platform.document-management.controlled-documents.create`

Operation view, retry/reconciliation and reverse lookup use their approved dedicated/downstream keys. Multiple
`HasPermission` attributes are explicitly enabled and therefore evaluate as an AND gate.

## Guardrails

- Request DTO has no `TenantId`, UID, document code, effective date, lifecycle, approval, release-gate or signature
  control.
- UID/document-code allocation is not invoked.
- Lifecycle/register status remains Draft.
- No approval/effective/signature automation exists.
- Operation entity stores no raw bytes or public URL.
- No DELETE endpoint or registration hard-delete repository method exists.
- Existing MOD-0028 mutation paths were not changed.
- Frontend runtime files and Ocelot configuration were not changed.

## Verification

- Platform API Release build: **PASS**, 0 warnings / 0 errors on final build.
- Platform Application full suite: **PASS**, 1907/1907.
- FU36A Platform targeted tests: **PASS**, 6/6.
- AuthService full suite: **PASS**, 454/454.
- FU36A Auth targeted tests: **PASS**, 2/2.
- FU36A Gateway route tests: **PASS**, 4/4.
- Gateway Release build: **PASS**, 0 warnings / 0 errors.
- FU36A verifier: **PASS**.
- Scoped `git diff --check`: **PASS** (enforced by verifier).

## Non-blocking environment gaps

- Requested Debug builds encounter file locks from already-running local API processes. AuthService explicitly
  reports PID 908 holding its Debug output. Release builds and complete test suites are green; running services
  were not terminated.
- Repository-wide `git diff --check` still reports the pre-existing, scope-out `watch-diten.ps1:6` trailing
  whitespace. Task-scoped diff check passes.
- Runtime smoke was intentionally excluded by the FU36A task.

## Next step

Prepare and run the FU36 frontend unified-create implementation prompt after reviewing this backend contract.
