# MOD-0192 Capacity Planning — platform registration checklist (integration owner; pack §23.10, §24). NOT APPLIED.

Nothing below is done by the module lane.

1. **Backend uptake** — the accepted Capacity backend (43 paths + approved composition, including the executor, pack §22;
   isolated source BC-SOURCE `ebd5d80c…7064`) enters the integrated target. `CapacityPermissions.cs` exists only there today
   (pack §24 open gap 1).
2. **Gateway** — merge `gateway-capacity-routes.ocelot-fragment.json` (six explicit routes, OPTIONS, no catch-all, no collection
   GET). Confirm Ocelot placeholder matching keeps `/{capacityPlanId}` from swallowing deeper paths on the target (README F5).
3. **Manifest provider** — `CapacityPlanningManifestProvider` + the `AddSingleton` line (`supplychain-Program.cs.patch.txt`),
   in the same change as the nav keys (W-03). Pages `CAPACITY_PLANS` (nav, List) and `CAPACITY_PLAN_DETAILS` (Detail, parent
   `CAPACITY_PLANS`); actions `CREATE`, `CREATE_SCENARIO`, `EVALUATE` (all Toolbar, none dangerous).
4. **Permissions** — the four existing keys only: `supplychain.capacity-plans.read`, `.create`, `.scenario.create`,
   `.evaluate`. Role seeding for the test identities of CP-VS1 / §23.12 (read-only, create, scenario, evaluate) is a
   platform/environment step, not a new key.
5. **Navigation and Ctrl+K** — `Nav.Module.CAPACITYPLANNING` and `Nav.Page.CAPACITY_PLANS` in `SharedResource.{7}.resx`
   (fragments in `sharedresource-nav-keys/`, values reviewed by the l10n agent); registry-reconciled codes.
6. **DCP-009 §21.1** — the "Excluded: MOD-0192 … no UI scope" row must change through a separate DCP patch by its owner
   (pack §24 open gap 2).
7. **Icon map** — `icon-map.proposal.md` (G-ICONMAP); module icon `bx-bar-chart-alt-2`, SortOrder 450.
8. **Fixtures** — DEMAND fixture and `CAPACITY-EVAL-FIXTURE-192-01@1` seeded on the target for CP-VS1 (pack §23.11).
9. **Guards** — W-01…W-04 and R-01…R-04 (DCP-009 §21.3) green for this module.
