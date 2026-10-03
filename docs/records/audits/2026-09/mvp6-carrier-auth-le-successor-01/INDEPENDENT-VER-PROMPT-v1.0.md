# MVP6-CARRIER-AUTH-LE-SUCCESSOR-VER-01 — independent VER prompt

Role: read-only auditor; must not be the successor implementation writer. Strict repository-read-only.

## NE

Independently verify the authorized implementation of the exact Carrier Auth LE successor and determine whether `GAP-CARRIER-AUTH-LE-01/02` close.

## NEDEN

The candidate has build and focused E1/E2 evidence only. Runtime login/refresh/MFA/forced-password, scope variants and cross-service failure behavior remain unproven.

## NASIL

1. Read `AGENTS.md`, security JWT, multi-tenancy, configuration, Auth/Platform/MDM domain rules, CT SOP, the predecessor REWORK reports and this successor package.
2. Verify the real owner decision and exact hashes:
   - patch `e6b2a3d335ee8f76270fe6ace09793a08d69e57174874be2b2db99941b01f673`;
   - manifest `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d`.
3. Require writer-complete and source hashes matching all 22 targets. Extract the archived source into a unique disposable workspace and fresh-build Auth, Platform and MDM.
4. Use lane-specific ports and DB-010 isolated Mongo. Configure one ephemeral Platform-to-MDM secret without writing it or a bearer token into evidence.
5. Prove the Platform internal key is checked before tenant binding and repository use. Reproduce correct key, wrong key, tenant mismatch and actor mismatch.
6. Prove MDM validates exact algorithm, key id/key, issuer, audience, caller, narrow scope, tenant, actor, LE, lifetime and enabled/key-rotation state before repository use. A tenant header alone must fail.
7. Exercise exactly-one, zero, multiple, inactive and revoked LE scopes. Verify only exactly one active authorized LE yields `legal_entity_id`.
8. Exercise login, refresh, MFA and forced-password issuance. Refresh must re-resolve and must not copy a stale claim.
9. Inject Platform and MDM refusal/timeout/configuration failure. Verify claim omission/fail-closed behavior, correlation and no unauthorized repository access.
10. Use a real Auth-issued token against Carrier to confirm the existing missing-LE 403 remains fail-closed and a valid exact claim works within its tenant/LE. Keep Carrier acceptance separate from the Auth/MDM seam result.
11. Produce source→build→binary→process→HTTP/DB evidence, a row-by-row matrix, no-change proof and process cleanup record.

## YAPMA

Do not fix source, broaden `/api/internal`, authorize headers alone, remove MDM auth, add general MDM read permission, relax Carrier 403, claim Supplier concurrence, archive secrets/tokens, use operational Mongo, mutate git, or declare rollout/full-module/E5/G5 acceptance.

## DOĞRULA

Return SOP §22 `PASS / REWORK / BLOCKED`. Mark every unexecuted path `NOT RUN`. Candidate or writer PASS is not independent acceptance.
