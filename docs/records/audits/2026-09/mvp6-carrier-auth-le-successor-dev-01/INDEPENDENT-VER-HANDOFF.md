# MVP6-CARRIER-AUTH-LE-SUCCESSOR-VER-01 handoff

Use a verifier that did not write this DEV package. Remain repository read-only.

## Inputs

- `applied-source.tar.gz` and `APPLIED-SOURCE-MANIFEST.tsv`
- `evidence.tar.gz` and `ARTIFACTS.sha256`
- controlling candidate package `../mvp6-carrier-auth-le-successor-01/`
- branch `feature/mvp6-logistics`, base HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

## Required verification

1. Verify the archive, manifest, patch and all 22 target hashes.
2. Extract into a new disposable checkout and use `/Users/natig/.dotnet/dotnet` SDK 8.0.417/runtime 8.0.23 without major roll-forward.
3. Use new lane-specific ports and a DB-010 isolated Mongo replica set.
4. Fresh-build Auth, Platform and MDM and bind source to binary to process.
5. Independently reproduce login, refresh/re-resolution, MFA, forced-password, zero/multiple/inactive/revoked scope, refusal and timeout.
6. Verify exact caller/key/audience/scope and tenant/actor/LE tuple failures. Headers alone must never authorize.
7. Confirm exactly one active authorized LE produces `legal_entity_id`; every invalid or ambiguous result omits it.
8. Confirm secrets and bearer tokens are absent from the permanent evidence.
9. Preserve Carrier missing-LE fail-closed behavior as inherited evidence; run a real Carrier E2E only if the verifier's approved scope includes the existing Carrier source.

Do not fix findings. Return SOP §22 PASS/REWORK/BLOCKED with exact path:line findings, raw evidence hashes, no-change proof and cleanup status. Independent PASS is still not CT acceptance or rollout approval.

