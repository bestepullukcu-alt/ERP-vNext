---
id: WP-0230-08
title: Module manifest and catalog registration
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-DL4B, WP-0230-DL6, WP-0230-DL7A, WP-0230-DL7B-R1, WP-0230-DL7C, WP-0230-DL4D, WP-0230-DL8, WP-0230-DL10-R1]  # queue state: WP-0230-DL10-R1 INTEGRATED; the others INTEGRATED or ACCEPTED
gate: pack revision 15 approved on main
status: blocked
estimate: 0.5 d
---

# WP-0230-08 - Module manifest and catalog registration ⛔ BLOCKED

## Decision (2026-10-31)

Owner "Evet, sürsün" 2026-10-31 (`docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-03.md`; sequencing `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-04.md`): `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-01.md` (KK-1 ...
KK-6) holds, corrected by `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-02.md`. Option B applies, narrowed: the module enters the catalog with `IsTenantAssignable: false`, no page is visible in
the menu, and it is not offered for tenant subscription. `Diten.PvgService` registers it, when the key and the base URL are both set, the URL is a valid absolute http/https address, and the host is not production-like. Platform and Auth
code do not change, no seed is written, and the operational runtime gate stays closed. An authorized user then sees the
page; the list returns no records and saving is refused (`-02` Düzeltme 1; pack gap G-23, OD-PVG-14).

**Option A is no longer recommended.** It assumed the screen opens by direct address. After DL-7's permission check, a
user without the key gets 403 at that address, and no role can hold the key until it exists in AuthService (record,
"Durum").

The open question under option B is answered as a CT reading: `pvg.case-intake-triage.route` is declared, not withheld. The
owner sees that reading when approving pack revision 15 (record, "CT okumaları" 1).

**Gate:** pack revision 15 approved on main. It is not approved yet, so this pack stays `blocked` and nothing here may be
started. The kept-for-history sections below are not the contract. The contract is the pack's "Closed Catalog
Registration" section and slice DL-11 (AC-36, AC-37, AC-38), whose prerequisites are in `depends_on` above (WP-0230-DL10-R1 `INTEGRATED` first; KK-7, KK-12, `docs/records/decisions/2026-11/pvg-mod0230-kapali-kayit-05.md`).

## Do not start this pack

It is documented so the conflict below stays visible as history, not so it gets implemented before the gate above opens.

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
| **A. Defer** | MOD-0230 ships slice 1 as un-catalogued. It is reachable by direct route for development and demo only. Registration happens when the operational runtime gate opens. | Amend the module-self-registration standard to carve out an explicit "pre-operational module" state, or record a scoped exception in the MOD-0230 pack. `.antigravity/**` edits need your approval. |
| **B. Register with `IsTenantAssignable: false`** | Manifest ships and the catalog reflects reality, but no tenant can subscribe. Pages declared, `IsNavigationVisible: false`. | Still publishes the unsatisfiable `route` action. Needs a decision on whether declaring-but-not-assigning is acceptable, and whether `route` is omitted from the manifest until MOD-0023 lands. |
| **C. Register fully** | Standard satisfied literally. | Advertises a regulated PV module as tenant-assignable while its masking, workflow, and evidence dependencies do not exist. **Not recommended.** |

Option C should not happen before the operational runtime gate opens. Option B, narrowed, is the 2026-10-31 decision;
see "Decision (2026-10-31)" for why option A no longer holds.

## What is needed before this pack can move to `ready`

Since 2026-10-31 the conditions are the gate above and `depends_on`: WP-0230-DL10-R1 `INTEGRATED` (WP-0230-DL10 went to `REWORK`
on 2026-11-01), each other WP `INTEGRATED` or `ACCEPTED`. The list below is kept as history.

- [x] You choose A, B, or C. Done 2026-10-31: B, narrowed ("Evet, sürsün").
- [ ] If **A**: the carve-out is written into either `.antigravity/rules/module-self-registration-standard.md` (protected - needs your approval) or as a scoped, time-boxed exception recorded in the MOD-0230 pack and DCP-004 §20.
- [ ] If **B**: decide whether `pvg.case-intake-triage.route` is declared or withheld until MOD-0023 ships. Declared in the draft (CT reading, open to the owner at pack revision 15's approval).
- [ ] If **C**: the operational runtime gate must open first, which requires MOD-0019, MOD-0023, MOD-0031, and a retention / legal-hold owner. That is not a slice-1 decision.

## If it later becomes `ready` (option B or C)

Superseded by the pack's "Closed Catalog Registration" section. The text below is kept as history and is wrong in three
places measured on 2026-10-31: the project folder is `src/Diten.PvgService.Api`; the top-level page carries
`IsNavigationVisible: false` under this decision; and Triage and Route are forms on the Details page, not row actions.

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

This was one of the two open items that needed you rather than an agent; its state is in "Decision (2026-10-31)" above.
The other was approval to register port 5011 in the protected `.antigravity/rules/ports.md`; for that, see OD-PVG-7 in the
MOD-0230 pack.
