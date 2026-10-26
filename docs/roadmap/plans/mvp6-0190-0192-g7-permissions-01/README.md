---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
modules: [MOD-0190, MOD-0192]
---

# G7 — S&OP + Capacity default role grants — apply package

    prepared : 2026-10-18 by CT (gmg-10) · decision: `docs/records/decisions/2026-10/mvp6-0190-0192-g7-role-grants-owner-decision-01.md`
    applies  : the owner (permission grant; CT does not apply it — BL-652 precedent)

## Patches

| file | use |
|---|---|
| `patches/g7-adminmodules-standalone.patch` | apply on the current tree (BL-652 **not** applied) |
| `patches/g7-adminmodules-after-bl652.patch` | apply **instead**, if BL-652's `option1-adminmodules-widen.patch` is applied first (both edit the same `AdminModules` line) |
| `patches/g7-role-split-test.patch` | new `G7SandopCapacityRoleSplitTests` (Admin = 8, Viewer = the 2 reads) — apply with either |

Verified by CT: the standalone patch and the test patch apply cleanly with `git apply --check`. The after-BL-652 variant
applies cleanly on top of BL-652 in a throwaway repo. On a disposable copy of HEAD with the standalone and test patches
applied, `Diten.AuthService.Application.Tests` (filter Roles/DefaultRolePermission/PermissionSeed) ran **848 passed, 0 failed,
7 skipped**, including both new G7 facts. Sabotage in the copy (drop `capacity-planning` from `AdminModules`) turns
`Admin_holds_all_eight_keys` red.

## Apply (from the repo root)

```
git apply docs/roadmap/plans/mvp6-0190-0192-g7-permissions-01/patches/g7-adminmodules-standalone.patch docs/roadmap/plans/mvp6-0190-0192-g7-permissions-01/patches/g7-role-split-test.patch
```

If BL-652 goes in first:

```
git apply docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen.patch
git apply docs/roadmap/plans/mvp6-0190-0192-g7-permissions-01/patches/g7-adminmodules-after-bl652.patch docs/roadmap/plans/mvp6-0190-0192-g7-permissions-01/patches/g7-role-split-test.patch
```

Then: `dotnet test services/Diten.AuthService/tests/Diten.AuthService.Application.Tests --filter "FullyQualifiedName~Roles"`.
Already-provisioned tenants get the new default-role keys only through the role reconcile path, the same as for
Q358 and BL-652.
