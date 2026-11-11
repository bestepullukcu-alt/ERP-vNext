---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: approved
commit:
modules: [MOD-0190, MOD-0192]
---

# Wave 6.1 G7 — default role grants for S&OP + Capacity (owner decision)

> **SUPERSEDED (2026-10-20) — no longer in effect.** PR #167 (merge `d6c7b9e05`, 2026-10-20) removed both codes
> (`sop-workflow-signoffs`, `capacity-planning`) from `AdminModules`, and the definition-driven retraction runner covers
> existing tenants' G7 grants (non-prod `DemandFixtures` hosts only; production never composed S&OP/Capacity). Evidence:
> `docs/records/audits/2026-10/mvp6-g7-grant-removal-01/REPORT.md`. The 2026-11-03 expiry below no longer applies. The
> text below is kept unchanged as the record of what was ruled on 2026-10-18.

> **IN EFFECT (2026-10-18).** The owner applied `g7-adminmodules-standalone.patch` + `g7-role-split-test.patch`
> (`git apply`, owner's terminal); CT verified the tree (`AdminModules` line 68 carries both codes; the patch reverse-checks
> as applied) and ran `Diten.AuthService.Application.Tests` (Roles/DefaultRolePermission/PermissionSeed): **848 passed,
> 0 failed, 7 skipped**, incl. both G7 facts. Package: `docs/roadmap/plans/mvp6-0190-0192-g7-permissions-01/README.md`.
> If BL-652 is applied later, it now conflicts on the same line — re-cut BL-652 on top of this (CT can prepare it).

- owner: **ny@gmgroup.ch** — "prep G7 with that split", 2026-10-18
- split: **tenant Admin = all 8 keys; default Viewer = the 2 `.read` keys**
- expiry: **2026-11-03**, aligned with Q358 and BL-652 (all three curated-list suspensions expire together;
  re-approved in writing or removed; the entitlement path supersedes them)

## What is granted

| role | keys | mechanism |
|---|---|---|
| Admin | `supplychain.sandop-plans.{read,create,snapshot.capture,sign-off.record}`, `supplychain.capacity-plans.{read,create,scenario.create,evaluate}` | `sop-workflow-signoffs` and `capacity-planning` join `AdminModules` (the patch) |
| Viewer | `supplychain.sandop-plans.read`, `supplychain.capacity-plans.read` | **no change** — the existing Viewer rule already grants every tenant-scoped `read` not on `EntitlementOnlyViewerPermissions` |

Both roles also need `mdm.legal-entities.read`, because since the R-2 alignment each S&OP/Capacity request is validated by
MDM with the caller's token. Admin already holds it (`legal-entity` is in `AdminModules`), and Viewer holds it through
the read rule (it is not entitlement-only). So no further grant is needed.

**Where it bites:** the keys enter the permission catalog only from the module manifests. Those are registered only on
hosts where S&OP/Capacity are composed (the non-production demand seed, G4). So in production this grants nothing until
the modules are composed there (DEMAND v2, G3).

## Not granted

Any other role, and any key beyond these eight.
