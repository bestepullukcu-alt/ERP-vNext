# SOP §22 DEV handoff — MVP6-CARRIER-AUTH-LE-SUCCESSOR-DEV-01

## Verdict

**DEV PASS — writer complete.** The exact authorized 22-path successor was applied only in an isolated checkout and its real Auth→Platform→MDM→JWT chain was exercised. Independent VER and CT acceptance remain open.

## Execution identity

- Repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch: `feature/mvp6-logistics`
- Base HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Isolated checkout: `/private/tmp/mvp6-carrier-auth-le-successor-dev-01/repo`
- Candidate patch: `e6b2a3d335ee8f76270fe6ace09793a08d69e57174874be2b2db99941b01f673`
- Candidate source manifest: `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d`
- Candidate source archive: `1c497022407788a5b38f5c554c867d446cc08541cf00c0d3390704c466301846`
- Authority: the user's exact hash-bound `MVP6-CARRIER-AUTH-LE-SUCCESSOR-DEV-01` dispatch, recorded in `OWNER-AUTHORIZATION.md`.

## Applied scope

Only the 22 paths listed in `APPLIED-SOURCE-MANIFEST.tsv` were applied. The endpoint-local Platform context binding precedes tenant-filtered resolution. Platform signs a narrow MDM service identity for exact caller, audience, scope and tenant/actor/LE tuple. MDM validates that identity at the endpoint boundary. Auth emits `legal_entity_id` only for one active authorized LE and re-resolves on refresh.

No production source was written in the shared repository. The permanent repository delta produced by this task is this audit directory only.

## Build and test evidence

- Native SDK/runtime: 8.0.417 / 8.0.23; no major roll-forward.
- Fresh Auth, Platform and MDM builds: PASS, zero errors and zero warnings.
- Platform successor tests: 8/8 PASS.
- MDM service-token validator tests: 7/7 PASS.
- The attempted broad Auth application regression produced neither output nor TRX and is **NOT ESTABLISHED**. It is retained rather than reported as PASS.

## Runtime evidence

The isolated environment used Mongo replica set `rsCarrierAuthLeDev01` on port 37484 and service ports 5456/5457/5459. The DB names were lane-specific. Secrets and bearer tokens were excluded from permanent evidence.

The evidence establishes:

- real login and refresh emit the exact LE claim for one active authorized scope;
- refresh performs a new resolution and drops a stale claim after scope revocation;
- zero, multiple, inactive and soft-deleted/revoked assignments emit no LE claim;
- dependency refusal and timeout fail closed by omitting the claim;
- MFA challenge emits no token, while successful verification emits the correct claim;
- forced-password issuance preserves the same authoritative scope before and after password change;
- wrong key, caller, audience, over-broad scope and wrong tenant/actor/LE are rejected;
- headers without a valid signed service identity are rejected;
- restart against the same database and binary preserves correct resolution.

Platform `/health` remained 503 because the unrelated business-reference-data provider is disabled in the isolated environment. The successor internal endpoint and full Auth→Platform→MDM chain succeeded; no repository-wide health PASS is claimed.

## Source → binary → process

| Service | DLL SHA-256 | Runtime process |
|---|---|---|
| Auth | `93eef40f1afc9758a0503944af4f9f0d52448f2c0ec3b0d0022a5435ec229f86` | `/Users/natig/.dotnet/dotnet .../Diten.AuthService.Api.dll`, port 5456 |
| Platform | `844ffbf8335b2caf7661efd34fae1c51d28046a2ba477fb7e22e6c8fa582dd0a` | `/Users/natig/.dotnet/dotnet .../Diten.Platform.API.dll`, port 5457 |
| MDM | `9510f5ceb5b9d0d3b0e408211a4f50bcac39780f9c2fabd74523c41734b8221a` | `/Users/natig/.dotnet/dotnet .../Diten.MdmService.Api.dll`, port 5459 |

The fresh closing build reproduced the same three DLL hashes that were used by the runtime processes.

## Remaining gates

1. A different agent must run the independent VER handoff.
2. Carrier UI E2E with this Auth-issued token remains a successor consumer verification step.
3. CT acceptance and operational rollout are not granted.
4. Full Auth application regression is not established by this handoff.
5. The unrelated Platform health dependency remains outside this bounded scope.

## Cleanup and Git safety

All lane-owned API, SMTP and Mongo processes were stopped after evidence capture. No commit, push, stash, branch switch, rollout or operational database access occurred. Pre-existing dirty work was preserved.

