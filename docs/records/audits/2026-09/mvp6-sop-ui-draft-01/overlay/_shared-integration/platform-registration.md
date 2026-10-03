# MOD-0190 S&OP — platform registration checklist (integration owner; pack §23.10, §24)

Nothing below is done by the module lane.

1. **Backend uptake** — the accepted S&OP backend (38 owned paths + approved composition, pack §22; isolated source
   `8fa00d40…b745`) enters the integrated target. `SandopPermissions.cs` exists only there today (pack §24 open gap 1).
2. **Gateway** — merge `gateway-sandop-routes.ocelot-fragment.json` (explicit routes, OPTIONS, no catch-all).
3. **Manifest provider** — `SopWorkflowSignoffsManifestProvider` + the `AddSingleton` line (`supplychain-Program.cs.patch.txt`),
   in the same change as the nav keys (W-03). Pages `SANDOP_PLANS` (nav, List) and `SANDOP_PLAN_DETAILS` (Detail, parent
   `SANDOP_PLANS`); actions `CREATE`, `CAPTURE_SNAPSHOT`, `RECORD_SIGN_OFF` (all Toolbar, none dangerous).
4. **Permissions** — the four existing keys only: `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`,
   `.sign-off.record`. Role seeding for the four test identities of SU-VS1 (read-only, create, capture, sign-off) is a
   platform/environment step, not a new key.
5. **Navigation and Ctrl+K** — `Nav.Module.SOPWORKFLOWSIGNOFFS` and `Nav.Page.SANDOP_PLANS` in `SharedResource.{7}.resx`
   (fragments in `sharedresource-nav-keys/`, values reviewed by the l10n agent); registry-reconciled codes.
6. **DCP-009 §21.1** — the "Excluded: MOD-0190 S&OP … no UI scope, no manifest" row must change through a separate DCP patch
   by its owner (pack §24 open gap 2).
7. **Icon map** — `icon-map.proposal.md` (G-ICONMAP).
8. **Guards** — W-01…W-04 and R-01…R-04 (DCP-009 §21.3) green for this module.
