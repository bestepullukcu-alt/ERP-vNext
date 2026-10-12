# SOP §22 — MVP6-CARRIER-AUTH-CHAIN-RECOVERY-01

## Verdict

**PASS — Carrier real-Auth E2E handoff READY.** The bounded recovery independently completes the fresh Auth→Platform→MDM login, refresh and refresh-time Legal Entity re-resolution evidence on the approved final source. F-01 and F-02 remain **CLOSED** from the controlling independent 49/49 verification and were not rerun.

This is a technical verifier verdict for the cross-service authentication chain. It is not Carrier UI acceptance, CT/full-module acceptance, deployment or rollout approval.

## Authority and baseline

- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Controlling handoff: `docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-ver-01/`
- Final source archive SHA-256: `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`
- Final 22-path manifest SHA-256: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`
- Build-source manifest SHA-256: `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911`

The archive was extracted to a unique disposable source directory. Independent checks matched 22/22 final paths and 3333/3333 build inputs. No product source was edited.

## Build and runtime provenance

Native `/Users/natig/.dotnet/dotnet` SDK 8.0.417 and runtime 8.0.23 were used. No major roll-forward was enabled. Fresh Release restore/build completed with exit 0 for Auth, Platform and MDM. Exact binary hashes and process bindings are in `SOURCE-BINARY-PROCESS.tsv`.

The verifier used lane-owned ports 5556/5557/5559 and DB-010 replica set `rsCarrierAuthChainRecovery01` on port 37994 with fixed databases:

- `DitenAuth_CarrierAuthChainRecovery01`
- `DitenPlatform_CarrierAuthChainRecovery01`
- `DitenMdm_CarrierAuthChainRecovery01`
- separate lane-owned Hangfire storage

Port 27017 was not contacted. Effective configuration is redacted in the evidence archive.

## Cross-service acceptance

Real Auth-issued tokens, rather than diagnostic service tokens, produced these fresh results:

- Login: HTTP 200; exact tenant/actor context and authoritative `legal_entity_id` present.
- Refresh: HTTP 200; exact `legal_entity_id` present.
- Refresh re-resolution: after revoking the assignment, refresh returned 200 and omitted `legal_entity_id`; the fixture was restored.
- Zero scope, multiple scopes, inactive LE and revoked assignment: login returned 200 without `legal_entity_id`.
- Platform refusal: login failed closed with 401 and no access or refresh token.
- Platform timeout: the real dependency call reached the configured five-second timeout; login failed closed with 401 and no token.

The row-by-row disposition and evidence pointers are in `ACCEPTANCE.tsv`. MFA, forced-password and broader tenant/actor negative tuples remain explicitly inherited/content-bound from the unchanged successor source; this report does not relabel them as fresh runs.

## F-01 and F-02

F-01 exact claim cardinality and F-02 NumericDate raw JSON typing remain **CLOSED** by the controlling independent 49/49 matrix. The request explicitly prohibited reopening or rerunning those cases. The fresh chain proves that real Auth-issued login and refresh tokens remain compatible with that final validator.

## Platform aggregate health observation

Platform `/health` returned 503 because only `business_reference_data_provider` was Unhealthy. The same response reports `self`, `mongodb` and `hangfire_storage` Healthy. The required internal login-settings and tenant/LE resolver calls and the full Auth→Platform→MDM chain passed. The 503 is retained as a non-blocking environment observation and is not hidden or converted to an aggregate-health PASS.

## Attempts, security and evidence handling

`ATTEMPT-DISPOSITION.md` preserves the stalled restore, sandbox EPERM, initial Platform configuration failure and dependency failure attempts. None is counted as product PASS or product failure without its proper classification.

Evidence contains redacted claim subsets and presence booleans only. No usable bearer, refresh token, signing key, internal key, password or connection secret is archived.

## Cleanup and no-change

All lane-owned processes were stopped. Ports 37994, 5556, 5557 and 5559 were free after cleanup. The operational Mongo instance was untouched. The common checkout already contained unrelated dirty work; this lane wrote only this new audit directory and did not mutate source, UI, Gateway, Shipment, Git index, branch or history.

## Successor handoff

`CARRIER-REAL-AUTH-E2E-HANDOFF.md` marks the bounded Carrier real-Auth E2E verification **READY**. Carrier must continue to reject missing Legal Entity context fail-closed. This handoff supplies no rollout, commit/push, UI change or CT acceptance authority.
