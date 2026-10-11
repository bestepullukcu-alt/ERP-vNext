# Phase 1 findings for the successor writer

The immutable candidate passed package integrity, but is not ready for independent
runtime verification.

1. In `PlatformServiceTokenValidator.cs:45-70,83-84`, replace first-value matching
   with exact-one cardinality checks for `sub`, `scope`, `tenant_id`, `actor_id`,
   `legal_entity_id`, `jti`, `iat` and `exp`. Require exactly one expected audience.
   Add correctly signed duplicate/conflict negatives and prove zero repository reads.
2. Bind token acceptance to an explicit `iat`/`nbf`/current-time/`exp` policy. Add
   future `iat`, future `nbf`, expired inside/outside skew and excessive effective
   lifetime negatives. Do not treat `exp-iat` alone as sufficient.
3. Preserve the current ordering: internal key before tenant/actor bind; signed
   route/header tuple before MDM mediator/repository.
4. Keep the trust boundary explicit: the Platform resolver authorizes actor→LE;
   MDM authenticates and binds the service tuple. A valid key/token by itself is not
   actor entitlement evidence.
5. Publish an immutable full build-source archive, final manifest, writer-complete
   and no-active-writer record. The 22-file overlay alone cannot start Phase 2.

Login, refresh, MFA, forced-password, zero/multiple/inactive/revoked and dependency
failure rows remain runtime work. No Phase 1 declaration closes them.
