# DCP-004 + MOD-0028-FU06 Approval / Ready-for-dev Audit — 2026-07-25

## 1. Summary

DCP-004 architecture and delivery sequence are approved. MOD-0028-FU06 is promoted from `draft` to
`ready-for-dev`; runtime implementation is `not-started`. MOD-0029-FU37 remains `draft`.

## 2. Decisions Approved

- Corporate Collection Instance is tenant-owned and company-independent.
- Dummy CompanyId and nullable-only CompanyId quick fixes are prohibited.
- Typed `ScopeType + ScopeOwnerId` is required.
- Company behavior remains backward-compatible.
- Corporate access is deny-by-default with explicit governed grants.
- Corporate provisioning is tenant-scoped, idempotent, retry-safe, and audit-visible.
- One active Corporate instance is allowed per tenant, CorporateOwnerId, and baseline.
- FU06 performs no bulk migration.
- `DocumentScope` registration behavior belongs to downstream MOD-0029-FU37.

## 3. Why FU36C/FU36D Remain Paused

FU36C requires the completed FU37 scope/link contract; FU36D is the final runtime smoke after FU36C. FU06 approval
alone does not provide registration amendments or reverse-navigation scope checks.

## 4. DCP-004 Status

`approved`. This is a governance status, not runtime implementation completion.

## 5. MOD-0028-FU06 Status

`ready-for-dev`; runtime implementation `not-started`.

## 6. MOD-0029-FU37 Status

`draft`. It depends on FU06 implementation, verification, and reconciliation and was not promoted by this task.

## 7. Storage Partition Decision

- Company: `tenant/{tenantId}/company/{companyId}/folder/{folderId}`;
- Corporate: `tenant/{tenantId}/corporate/{corporateOwnerId}/folder/{folderId}`.

One scope-aware contract produces both. Corporate has no CompanyId fallback.

## 8. Authorization / Folder Access Decision

Corporate read/write/admin access is deny-by-default and requires explicit governed role/group/policy grants.
Company membership alone grants no Corporate access. The future evaluator is scope-aware.

## 9. Provisioning Decision

A published Corporate baseline provisions a real Corporate Collection Instance through an idempotent,
tenant-scoped, retry-safe, audit-visible operation. Duplicate active instances are prohibited per tenant,
CorporateOwnerId, and baseline.

## 10. Migration Decision

No bulk migration is performed. Existing Company Collection Instances and their behavior remain unchanged.
Corporate instances are created through the new provisioning operation. Any future migration requires a separate
reconciliation/migration pack.

## 11. Registry Changes

The existing MOD-0028-FU06 registry row is updated to `ready-for-dev`. FU36 remains `ready-for-dev`; FU37 remains
`draft`. No duplicate row is added. The code-truth implementation registry is unchanged because FU06 runtime work
has not started.

## 12. Verification Results

- MOD-0028-FU06 module-ID preflight: PASS (`exit 0`).
- Delivery Capability Pack verifier: script not present; no PASS claimed.
- Module pack verifier: script not present; no PASS claimed.
- Repository-wide `git diff --check`: fails only on pre-existing `watch-diten.ps1:6` trailing whitespace.
- Touched tracked-file `git diff --check`: PASS.
- All touched governance files trailing-whitespace scan: PASS.
- Build/test: not required for governance-only changes.

## 13. Runtime Code Changed: No

This approval pass modifies governance/documentation files only. Pre-existing runtime working-tree changes are not
claimed or modified.

## 14. Next Step

Start a separately authorized MOD-0028-FU06 backend implementation task. After implementation, tests, and
reconciliation pass, review MOD-0029-FU37 for promotion to `ready-for-dev`.
