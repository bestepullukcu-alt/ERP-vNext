---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# BL-652(a) — the legal-entity validation coupling: the 26th permission, or a service identity

    item      : BL-652 part (a) · owner: SCE/MVP-6 + security (part (b) already done) · author: CT (gmg-10)
    measured  : 2026-10-18 at HEAD
    status    : OWNER/SECURITY DECISION — OPEN. Two options, measured below. CT recommends but does not
                decide: both touch the authorization model (a cross-domain grant, or the reversal of a
                PR #134 security decision). No patch is applied; Option 1's exact edit is given so it is
                mechanical on approval.

## The coupling, measured

`MdmLegalEntityScopeValidator.cs` validates a legal entity by calling MDM's
`GET api/legal-entities/{id}/lookup-validation`, which carries `[HasPermission("mdm.legal-entities.read")]`,
and it **forwards the caller's `Authorization`** (`:44`) rather than a service identity. Its own comment
(`:12-13`) states why: *"SupplyChain holds no MDM service identity — Platform's MdmServiceIdentity pair is
Platform-to-MDM only. The consequence is a real permission [coupling]."*

So a caller holding all 25 `supplychain.*` keys but **not** `mdm.legal-entities.read` gets **503
`DEPENDENCY_UNAVAILABLE`** on every create. Measured two-way (backlog): 25 keys → 503; `+ mdm.legal-entities.read`
with the same body → 201. This corrects Q496's premise: the modules need **25 + 1**, not 25.

Part **(b)** — the misleading 503 message — is already fixed in the tree (`CarrierContractError.cs`,
commit `07f7e2b48`, 11-test guard); this decision is only part (a), the coupling itself.

### The nuance that narrows it

`DefaultRolePermissionTemplate.AdminModules` already grants the tenant **Admin** role the full `legal-entity`
module — so an Admin actor already holds `mdm.legal-entities.read`. And Q358 (TEMPORARY, expires 2026-11-03,
owner decision `mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md`) already added
`shipment-tracking-pod` (MOD-0183) to that list, giving every tenant Admin the five `supplychain.shipments.*`
keys. So the coupling bites exactly **a role that holds supplychain keys for MOD-0184/0185/0186/0187 without
`legal-entity`** — the four modules Q358 did not cover.

## Option 1 — grant the coupling through the Admin baseline (follows the Q358 precedent)

Add the four modules' manifest ModuleCodes to `DefaultRolePermissionTemplate.AdminModules`, exactly as Q358
added `shipment-tracking-pod`:

```
"carrier-management", "routing-load-planning", "reverse-logistics", "claims-management"
```

Because `legal-entity` is **already** in `AdminModules`, every tenant Admin then holds all four modules'
`supplychain.*` keys **and** `mdm.legal-entities.read` — the coupling is resolved for Admin actors, create
answers 201. This is a one-line edit mirroring Q358.

- **Cost:** low. **Scope:** Admin-role actors only — a *non-Admin* role granted supplychain keys would still
  need `legal-entity` added to its own grant.
- **What it is, honestly:** a cross-domain breadth grant to every tenant's Admin, and a second suspension of
  the curated-list decision the template records as the owner's (*"the Admin baseline stays a curated module
  list"*). So it needs the **same ceremony as Q358**: an owner decision record and an expiry (or explicit
  permanence), not a silent template edit. CT can prepare the patch the moment the owner approves the breadth
  grant and sets the expiry; CT will not pre-apply a permission grant.

## Option 2 — give SupplyChain an MDM service identity (reverse R-2b)

Make the validator call MDM on its **own** authority (a SupplyChain→MDM service identity) instead of
forwarding the caller's token; then **no caller needs `mdm.legal-entities.read`** and the coupling disappears
at the source. This reverses **R-2b** (`Platform.Infrastructure/DependencyInjection.cs:191`, PR #134: *"the
internal legal-entity path is retired outright"*), which deliberately removed that infrastructure.

- **Cost:** high. It restores retired infra (the service-identity token provider, its options, the secret,
  MDM's acceptance of it) and is a security-architecture change — a work package, not a patch.
- **What it is:** the cleaner long-term model (service-to-service calls act on their own authority, callers
  do not carry cross-domain read permissions), but it undoes a shipped decision and needs the security owner.

## Recommendation

**Option 1 now, Option 2 as the standing model** — and they are not exclusive. Option 1 unblocks MVP-6 Admin
actors immediately on the exact precedent the owner already set with Q358 (temporary, expiry-bounded, decision
record), and keeps the blast radius to the tenant Admin role. Option 2 is the right end state and can supersede
Option 1 when the security owner is ready to restore an S2S identity; at that point the Q358-style widenings
(shipments + these four) can be removed together.

If the owner wants only the cheapest unblock and accepts the breadth grant + an expiry, CT will prepare the
`AdminModules` patch (the four ModuleCodes above) with a matching owner-decision-record stub, exactly as Q358
was done. Neither option is CT's to decide.

## Files

- `DECISION.md` — this. Part (b) is already in the tree (`07f7e2b48`); this package is part (a) only.
