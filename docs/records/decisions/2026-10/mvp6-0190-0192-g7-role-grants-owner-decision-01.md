---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: approved
commit:
modules: [MOD-0190, MOD-0192]
---

# Wave 6.1 G7 — default role grants for S&OP + Capacity (owner decision)

> **OWNER RULING RECORDED. The grant is not yet in effect** — it takes effect only when the G7 patch is applied
> to `DefaultRolePermissionTemplate.cs`. Widening every tenant Admin's permissions is the owner's action (the
> BL-652 precedent: the harness refuses it from CT as a `[Permission Grant]`). Package and commands:
> `docs/roadmap/plans/mvp6-0190-0192-g7-permissions-01/README.md`.

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
