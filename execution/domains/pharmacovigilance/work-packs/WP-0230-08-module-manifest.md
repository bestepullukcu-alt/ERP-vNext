---
id: WP-0230-08
title: Module manifest and catalog registration
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-07]
gate: BLOCKED - governance decision required
status: blocked
estimate: 0.5 d
---

# WP-0230-08 - Module manifest and catalog registration ⛔ BLOCKED

## Do not start this pack

It is documented so the conflict below is visible and gets a decision, not so it gets implemented. Every other
slice-1 pack completes without it.

## The conflict

Two rules in this repository disagree about MOD-0230, and both are currently authoritative.

**`.antigravity/rules/module-self-registration-standard.md`** opens with:

> **MANDATORY for every tenant-assignable module.** A module is not "done" until it self-registers.
> The catalog is populated FROM CODE, never by hand.

MOD-0230 is a tenant-assignable module with a tenant UI. By that rule it must ship a `ModuleManifestProvider`
declaring its pages and actions, reconciled into `platform_module_catalog` at startup.

**The MOD-0230 module pack and the PVG domain-config** both state that **menu entry and module-catalog work
remain blocked** in slice 1.

So MOD-0230 cannot simultaneously be "done" and compliant. One of the two has to give.

## Why this is not a trivial "just register it"

The manifest standard requires the declaration to mirror the **real UI**, and forbids inventing entries:

> Do NOT invent pages/actions/permissions: every `RequiredPermission`/`PermissionKey` must be a real
> `*Permissions` constant the controller enforces.

MOD-0230 slice 1 has one action that is **enforced but never satisfiable**: `pvg.case-intake-triage.route`
denies unconditionally, because the MOD-0023 queue registry does not exist. Registering it publishes a
capability into the tenant catalog that no tenant can ever exercise, and that a tenant admin could grant to a
role in good faith. That is a governance problem, not a code problem, and it is exactly the "false progress"
the DCP-004 no-shell gate exists to prevent.

There is also a scope question. Registering MOD-0230 in the catalog makes it **tenant-assignable** - a product
a tenant can subscribe to. Doing that while the operational runtime gate is closed, MOD-0019 masking does not
exist, and PHI handling is backed by a deny-by-default port would advertise a regulated safety module as
available. That decision belongs to you, not to an implementing agent.

## The three options

| Option | What it means | Cost |
|---|---|---|
| **A. Defer (recommended)** | MOD-0230 ships slice 1 as un-catalogued. It is reachable by direct route for development and demo only. Registration happens when the operational runtime gate opens. | Amend the module-self-registration standard to carve out an explicit "pre-operational module" state, or record a scoped exception in the MOD-0230 pack. `.antigravity/**` edits need your approval. |
| **B. Register with `IsTenantAssignable: false`** | Manifest ships and the catalog reflects reality, but no tenant can subscribe. Pages declared, `IsNavigationVisible: false`. | Still publishes the unsatisfiable `route` action. Needs a decision on whether declaring-but-not-assigning is acceptable, and whether `route` is omitted from the manifest until MOD-0023 lands. |
| **C. Register fully** | Standard satisfied literally. | Advertises a regulated PV module as tenant-assignable while its masking, workflow, and evidence dependencies do not exist. **Not recommended.** |

Option A keeps the slice honest. Option B is defensible if you want zero standards drift. Option C should not
happen before the operational runtime gate opens.

## What is needed before this pack can move to `ready`

- [ ] You choose A, B, or C.
- [ ] If **A**: the carve-out is written into either `.antigravity/rules/module-self-registration-standard.md` (protected - needs your approval) or as a scoped, time-boxed exception recorded in the MOD-0230 pack and DCP-004 §20.
- [ ] If **B**: decide whether `pvg.case-intake-triage.route` is declared or withheld until MOD-0023 ships.
- [ ] If **C**: the operational runtime gate must open first, which requires MOD-0019, MOD-0023, MOD-0031, and a retention / legal-hold owner. That is not a slice-1 decision.

## If it later becomes `ready` (option B or C)

File manifest would be:

```text
services/Diten.PvgService/src/Diten.Pvg.API/ModuleRegistration/
├── PvgCaseIntakeTriageManifestProvider.cs
└── (reuse ModuleRegistrationHostedService + PlatformRegistrationOptions patterns
     from Diten.DevEnablementService)
```

`ModuleCode`: `case-intake-triage`. `Service`: `pvg`. `Domain`: `pharmacovigilance`.
Pages mirror the **frontend** routes, not the API - Index, Create, Edit, Details - with
`IsNavigationVisible: true` on Index only. Actions mirror the buttons the UI actually shows: Create (Toolbar),
View / Edit / Triage / Route (RowAction). Reference: `GoldenCompactManifestProvider` in DevEnablement.

## Escalation

This is one of the two open items that need you rather than an agent. The other is approval to register port
5011 in the protected `.antigravity/rules/ports.md`.
