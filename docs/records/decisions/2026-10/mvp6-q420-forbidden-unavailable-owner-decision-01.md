# Owner decision — `Forbidden → Unavailable` keeps surfacing as 503

- date: 2026-10-12
- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q420, Q470, Q476, Q477, Q482
- closes: `MOD-0183:430`, the fifth and last leg of its cross-tenant/permission line

## Decision

**`MdmLegalEntityScopeValidator`'s mapping of MDM's `Forbidden` to `Unavailable` stays, and the
caller sees 503 `DEPENDENCY_UNAVAILABLE`.** No fourth `LegalEntityScopeOutcome` value is added
and no caller-forbidden outcome is introduced in MVP-6.

## Why

The mapping is a reasoned choice between two poor options, and the code says so at
`MdmLegalEntityScopeValidator.cs:55-58`: calling a permission gap `NotReferenceable` would report
an authorization problem as a missing record, which is the distinction Q420 exists to protect.

**And the exposure is narrower than the acceptance line implied.** Measured, twice:

- `ShipmentContextMiddleware` runs at `Program.cs:145`, one line before `app.UseAuthorization()` at
  `:146`, so `[HasPermission]` never runs for a caller the legal-entity check rejects first.
- But a caller provisioned from the default template never reaches that path. `DefaultRolePermissionTemplate.cs:41`
  lists `legal-entity` in `AdminModules`; `ClassifyScope` returns `Tenant` for it because it is absent
  from `PlatformAdminModules`; so the AdminRole breadth clause grants the whole `mdm.legal-entities.*`
  family, and ViewerRole grants every Tenant-scoped read outside two exclusion lists, which this key is
  in neither of. Ledger **Q477** had already said this twice before CT re-measured it.
- The VER lane produced the 503 with a hand-built zero-permission identity, which **no provisioning
  path creates**.

The real exposure is the one Q477 stated as a caveat: a tenant whose **custom** roles exclude the
legal-entity module would see 503 where 403 would be truer. That is recorded and accepted, not fixed.

## Why not the alternative

Adding a distinct caller-forbidden outcome would touch **five production consumers of
`lookup-validation` in four services** — `MdmCyclePeriodLegalEntityValidator` (CRM),
`MdmLegalEntityReferenceValidator` and `WorkingCalendarLegalEntityValidator` (Platform),
`GatewayReferenceValidationClient` (HCM) and `MdmLegalEntityScopeValidator` (SupplyChain, MVP-6) —
and each one's status mapping would have to be re-measured. That is a work package of its own, not a
line item, and MVP-6 does not need it.

## Scope

MVP-6 only. If MOD-0220's relationship-aware eligibility later changes what `lookup-validation`
answers, this mapping is re-opened with it — the same five consumers are the blast radius in both
directions.
