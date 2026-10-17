# BL-652(a) — Admin baseline widened for the four MVP-6 logistics modules (coupling unblock)

> **DRAFT FOR OWNER RULING — no permission is granted by this file.** The grant happens only when
> `patches/option1-adminmodules-widen.patch` is applied to `DefaultRolePermissionTemplate.cs`. CT cannot
> apply it — the harness refuses it as `[Permission Grant]`, which is correct: widening every tenant
> Admin's permissions is the owner's action. To enact: set `owner`, `status`, the **expiry**, then apply
> the patch and move this file to
> `docs/records/decisions/2026-10/mvp6-bl652-admin-baseline-coupling-owner-decision-01.md`.
> Argument and options: `docs/roadmap/plans/mvp6-bl652-coupling-decision-01/DECISION.md`.

- owner: __________ (product owner) — decision relayed by the Control Tower, 2026-10-__
- status: __________ (ACCEPTED Option 1 / chose Option 2 / DEFERRED)
- expiry: __________ (CT proposes **2026-11-03**, aligned with Q358 so the widenings expire together; or set permanent)
- module: MOD-0184/0185/0186/0187 · service `Diten.SupplyChainService` (5066) · auth template
  `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- precedent: Q358 (`mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md`) — the same
  curated-list suspension for `shipment-tracking-pod`

## What is being granted

`MdmLegalEntityScopeValidator` forwards the caller's token to MDM's `lookup-validation`
(`[HasPermission("mdm.legal-entities.read")]`), so a caller with the 25 `supplychain.*` keys but not that
MDM read gets 503 on every create. The patch adds `carrier-management`, `routing-load-planning`,
`reverse-logistics`, `claims-management` to `AdminModules`. Because `legal-entity` is already there, every
tenant **Admin** then holds those four modules' keys **and** `mdm.legal-entities.read` — create answers 201.

Scope: this fixes **Admin-role** actors only. A non-Admin role granted supplychain keys would still need
`legal-entity` on its own grant. Option 2 (a SupplyChain→MDM service identity, reversing R-2b) removes the
coupling at the source and would supersede this; this widening is the cheap, Q358-shaped unblock.

## Ruling

> _Owner: state Option 1 (apply the patch) with the expiry, or Option 2 (service identity, a WP), or defer._

________________________________________________________________

## On apply (Option 1)

```
git apply docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen.patch
```
Then run `Diten.AuthService.Application.Tests` (the `Roles/` suite) in CI to confirm — CT verified by
inspection that no role test breaks (`AdminModules_are_the_new_manifest_codes_only` is `Contains`-based;
`PermissionScopeEquivalence` and the zero-delta guard use a fixed catalog without supplychain modules), but
could not run them because applying the patch is refused to CT as a permission grant.
