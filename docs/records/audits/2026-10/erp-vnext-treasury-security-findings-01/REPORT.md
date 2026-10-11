# Security findings in ERP-vNext's Treasury service

- date: 2026-10-11
- by: Control Tower, from inspection lane `AL-FIN-TREASURY-INVENTORY`, spot-verified by CT
- subject repository: `~/Projects/ERP-vNext` — **not this repository**
- raised: separately from the Treasury intake, by owner decision 2026-10-11

These were found while inventorying `services/DitenErpTreasuryService` for a possible intake into this
portfolio. They are recorded on their own because **they hold whether or not the intake ever happens**: the
code is live in the other project today. Nothing here was changed — that repository is outside this session's
scope and the fix is its owner's.

No secret value is reproduced in this record.

## F-1 — a JWT signing secret is committed in plaintext

`src/Diten.ErpTreasuryService.Api/appsettings.json:9` carries a literal `JwtSettings:Secret`.

What makes it worse than the usual case: that file is the service's **only** settings file. There is no
`appsettings.Development.json`, no `UserSecretsId` and no `launchSettings` override of it, so the committed
value is what every environment would run with. It is also in git history, so removing the line does not
retire the secret.

**What it needs:** rotation of the key, then externalisation (environment or a secret store), then a history
decision. Rotating without rotating the issued tokens leaves anything already signed valid until expiry.

## F-2 — a permission wildcard that grants everything

`src/Diten.ErpTreasuryService.Application/CashForecasting/Security/CashForecastSecurity.cs:81-83`:
`EnsurePermission` succeeds when the principal carries a literal `"*"` claim.

Any identity that can obtain a `"*"` permission claim passes every MOD-0125 authorization check. The
inspection lane reports this construct is unique across both repositories — nothing else accepts a wildcard —
so it is not a house pattern, and this repository should not acquire it.

**What it needs:** remove the wildcard arm, and check separately whether anything issues a `"*"` claim today.
The second half matters more than the first: if no issuer exists the exposure is latent, and if one does it
is live.

## Two more, lower, recorded for completeness

**F-3 — nine committed credential slots, all currently empty.** Static `BearerToken` properties on nine
option/contract types across MOD-0120, 0124 and 0126. No values are set in `appsettings.json` — CT's lane
verified that. Two of them (the audit dispatch clients) support only a static token and no inbound
forwarding, so they *require* a provisioned credential to work at all. This is a secret-management decision
waiting to happen in nine places at once, not a defect today.

**F-4 — MOD-0119 writes tenant financial data with no audit record.** Its audit sink is a no-op registered
unconditionally; nine write operations proceed silently. In this portfolio that would violate `AGENTS.md` §6
("every command that changes tenant or platform data is audited"). Recorded here because it is a control
weakness in the subject repository, not only an intake obstacle. Its sibling — MOD-0120 and MOD-0125 refusing
every write because their audit sink is fail-closed with no implementation — is the same gap failing the
safe way, and belongs to the intake plan rather than to this record.

## What CT did and did not verify

CT re-read `appsettings.json:9` and `CashForecastSecurity.cs:81-83` directly and confirms both. CT did **not**
run the service, did not attempt to obtain a `"*"` claim, and did not search for an issuer of one. F-3 and
F-4 are reported from the lane's reading with their locators; CT did not re-verify every one of the nine
slots.
