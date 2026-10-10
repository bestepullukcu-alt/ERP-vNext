# UNAPPROVED — exact token findings successor decision

I approve the `MVP6-CARRIER-AUTH-TOKEN-FINDINGS-CLOSE-01` successor for isolated implementation and independent verification.

Exact artifacts:

- successor patch SHA-256: `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`;
- two-file delta manifest SHA-256: `5f7369c8b09ab28f37a8b21e958e2a14258dba60e1307d474d8110cca5f80153`;
- final 22-path source manifest SHA-256: `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2`;
- immutable build-source archive SHA-256: `47c39cdbeaa61a59b9b4891439bc758589911d535f7b84736e8ce3a4a88688e6`;
- build-source manifest SHA-256: `1b3eaf5101cd6a9ee2919c14e71fd51b50c6c433a08ef4dcdbf4492cc09d914e`.

I authorize only the two MDM-owned paths in `DELTA-MANIFEST.tsv`.

The validator shall accept exactly one expected audience and exactly one value for `sub`, `scope`, `tenant_id`, `actor_id`, `legal_entity_id`, `jti`, `iat`, `nbf` and `exp`. A missing value, duplicate value, conflicting duplicate or additional audience shall fail closed before mediator/repository access.

The service-token temporal policy is: `iat`, `nbf` and `exp` are single NumericDate values; `iat` equals `nbf`; issuance may not be later than current server time plus the configured `ClockSkewSeconds`; the existing JWT lifetime validation remains active; expiry inside the configured skew is accepted and expiry outside it is rejected; `exp` is later than `iat`; and `exp - iat` does not exceed `MaximumTokenLifetimeSeconds`.

The existing issuer, HS256, key ID/key, caller, scope, tenant/actor/LE tuple, enabled/key-rotation behavior and endpoint-local trust boundary remain unchanged. JWT validation may not be weakened, duplicate ambiguity may not be resolved by choosing the first value, and time checks may not be bypassed.

After application, a different verifier may run native .NET 8 Phase 2 against the immutable final source. This decision does not authorize Carrier E2E, UI, gateway, Supplier work, broad MDM access, global tenant bypass, rollout, commit, push or stash.

Status: **UNAPPROVED until the owner sends this exact hash-bound decision or an equivalent decision covering the same bytes and temporal policy.**

