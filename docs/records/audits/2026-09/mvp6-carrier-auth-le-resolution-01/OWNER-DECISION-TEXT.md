# Exact owner decision — UNAPPROVED

I approve the narrow Carrier Auth Legal Entity issuance candidate with these exact artifacts:

- Candidate patch SHA-256: `6559c94835814ab65dc50a05cca32db905e31e637f9a15aeeeb033e9850c83e6`
- Source manifest SHA-256: `84ce27833951bfed92edf46c02a0ee7c8934d1c6eff006e665d3ddaf09bf8b23`
- Scope: the ten exact Auth/Platform paths in `SOURCE-MANIFEST.tsv` only.

The Platform internal endpoint shall use the existing `IDataScopeResolver` and return a Legal Entity only when the actor has exactly one distinct, active and revalidated Legal Entity scope. Auth shall emit `legal_entity_id` only from that server-authoritative result on login, MFA verification, refresh and forced-password token issuance. Zero scopes, multiple scopes, invalid data, timeout or dependency failure shall omit the claim. The client may not select or supply the authoritative Legal Entity. Carrier's current fail-closed 403 and cross-scope 404 behavior shall remain unchanged.

This approval authorizes isolated implementation, focused security/runtime tests and separate independent verification of the exact candidate. It does not authorize Program.cs, gateway, permission, Carrier UI, contract, migration, rollout, commit or push changes, and it is not CT acceptance or E5/G5 evidence.

Status: **UNAPPROVED draft**. This text is a decision request, not an owner decision.
