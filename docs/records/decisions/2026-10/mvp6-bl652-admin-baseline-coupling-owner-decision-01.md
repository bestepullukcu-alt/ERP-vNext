# BL-652(a) — Admin baseline widened for the four MVP-6 logistics modules (coupling unblock)

> **OWNER RULING RECORDED. The grant is not yet in effect** — it takes effect only when
> `docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen.patch` is applied
> to `DefaultRolePermissionTemplate.cs`. CT cannot apply it: the harness refuses the apply as a
> `[Permission Grant]`, which is correct — widening every tenant Admin's permissions is the owner's action.
> The owner (or an authorized hand) runs the apply command at the foot of this record.
> Argument and options: `docs/roadmap/plans/mvp6-bl652-coupling-decision-01/DECISION.md`.

- owner: **ny@gmgroup.ch** (product owner) — decision relayed by the Control Tower, 2026-10-18
- status: **ACCEPTED — Option 1** (widen the Admin baseline; Option 2 remains the standing end-state and will
  supersede this when the security owner restores a SupplyChain→MDM service identity)
- expiry: **2026-11-03** (aligned with Q358 so both widenings expire together; re-approved in writing or
  removed at the expiry)
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

Re-verified current at HEAD 2026-10-18: validator still forwards the caller token
(`MdmLegalEntityScopeValidator.cs:44`); `legal-entity` already in `AdminModules` (line 62); the four manifest
ModuleCodes exist; the Option 1 patch applies clean.

## Ruling

**Option 1 accepted** — apply `option1-adminmodules-widen.patch`, temporary, expiring **2026-11-03**.
Option 2 (SupplyChain MDM service identity) is the agreed standing model and supersedes this widening when
the security owner restores the S2S identity; at that point the Q358 + BL-652 widenings are removed together.

## On apply (Option 1) — owner / authorized hand runs this

```
git apply docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen-v2-after-g7.patch
```

> **Re-cut 2026-10-18 (v2).** The G7 grant (`mvp6-0190-0192-g7-role-grants-owner-decision-01`, commit 40eac559c) changed
> the same `AdminModules` line, so the original `option1-adminmodules-widen.patch` no longer applies. `…-v2-after-g7.patch`
> carries the identical BL-652 comment and the same four ModuleCodes, appended after the G7 codes; it applies cleanly at
> HEAD (`git apply --check`). Use v2; the original is kept unchanged as the record of what was ruled.

Then run `Diten.AuthService.Application.Tests` (the `Roles/` suite) in CI to confirm. CT verified by
inspection that no role test breaks (`AdminModules_are_the_new_manifest_codes_only` is `Contains`-based;
`PermissionScopeEquivalence` and the zero-delta guard use a fixed catalog without supplychain modules), but
could not run them because applying the patch is refused to CT as a permission grant. The patch's inline
comment already carries this decision's path, the expiry, and the Q358 rationale.
