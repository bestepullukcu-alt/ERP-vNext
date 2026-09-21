# PPM entitlement credential transport hardening — scoped implementation evidence

Date: 2026-09-18.
Worktree: `/Users/alitufanoglu/ERP-vNext-codex-current`.
Branch: `codex/ppm-portfolio-first-delivery`.
Initial and final HEAD: `1f63c2e3f657f484cfbff5217c2f6edf10fe91a5`.

## Result and authority

The current user instruction authorized exactly six files and isolated tests. The existing entitlement
consumer is hardened; no new entitlement provider or Auth API was implemented. Final PPM unit tests
passed **474/474** and selected integration tests **78/78**, with no failures or skips.
Architecture remains **16 passed / 2 failed** at the previously recorded HCM/TalentEcosystem ClockSkew
guards. This delivery is source/isolated-test evidence, not a live Platform entitlement, deployed PPM
authentication, VM setup, browser or production acceptance.

The earlier 14-file Portfolio Auth transport work was already dirty at entry. It is preserved and is
not attributed to this delivery. Initial staging was empty; initial branch and HEAD matched the request.
The initial status, tracked diff and all 14 file hashes were captured before edits. No commit, push,
PR, branch switch, shared database access, grant/provisioning or real config/secret changes occurred.

## Exact delta for this delivery

All paths below are relative to the worktree above. Only these six source/document paths changed
relative to this turn's baseline.

| Path | Baseline state | This turn's delta |
|---|---|---|
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Entitlements/PpmEntitlementDecisionClient.cs` | Clean | Strict configured HTTPS origin and original-path validation; nonempty tenant; per-message key/correlation; default sensitive-header pollution rejection; normal TLS handler with redirect/proxy/cookies off. +47/-3 lines. |
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs` | Previously dirty | Replace only the entitlement client's registration with the dedicated handler and header redaction. Preserve the entire existing Portfolio Auth registration. |
| `services/Diten.PpmService/tests/Diten.PpmService.Tests/PpmEntitlementAuthorizationTests.cs` | Clean | Adapt fixture origin to HTTPS; add 28 cases for target/key/header/tenant rejection and shared authorization decisions. +102/-4 lines. |
| `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Entitlements/PpmEntitlementTransportIntegrationTests.cs` | Absent | New 14-case actual-factory TLS/redirect/cookie/log/scope transport evidence with nested disposable test support. |
| `execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md` | Previously dirty | Append scoped authorization and this evidence link; preserve all preceding content and overall review status. |
| `docs/records/audits/2026-09/ppm-entitlement-transport-hardening-2026-09-18.md` | Absent | This new dated evidence record. |

The final combined dirty set is 18 paths: the original 14 plus four previously clean/new paths.
The six-path delivery overlaps the earlier work only in DI and MOD-0117. Prior twelve other files are
byte-identical. Removing only the new entitlement DI registration restores the exact baseline DI hash;
removing only the appended scoped section restores the exact baseline pack hash.

## Transport and contract behavior

The existing `PpmEntitlementDecision:BaseUrl` remains the configuration input; no new configuration
contract was invented. It must be a single HTTPS origin supplied by the trusted environment owner.
The validator rejects userinfo (including empty userinfo delimiters), query/fragment (including empty
delimiters), non-root paths, normalized dot-path tricks, escapes, backslashes, whitespace/control
characters and invalid/zero ports. An optional root slash is accepted. HTTPS syntax does not prove
ownership or operator approval; that remains an external configuration trust requirement.

Only GET `/api/internal/ppm/tenants/{tenantId:D}/entitlement-decision` is constructed. The existing
authorizer still derives tenant/actor from trusted context before calling this consumer. Empty tenant
is rejected. The service credential and canonical correlation are attached to the new message only;
incoming Authorization/tenant/cookie/key headers are not copied. Polluted default sensitive headers
fail before send. Credentials remain opaque (internal spaces preserved); empty/whitespace-only and
control-character credentials are rejected.

The factory uses a plain identity-free HttpClientHandler with AllowAutoRedirect=false, UseProxy=false,
UseCookies=false and revocation checking enabled. Runtime has no certificate-validation callback and
retains normal hostname/chain/validity checks. The dedicated service key and other sensitive headers
are redacted by the actual HttpClientFactory logging pipeline.

Provider contract `platform.ppm-entitlement-decision.v1`, ModuleCode PPM, exact response shape and size
bound, authoritative Allow/Deny, indeterminate/dependency failure, timeout/caller cancellation and
correlation are preserved. No permission, entitlement-provider, CAS/replay, record-access or business
mutation code changed. Existing Portfolio default-off/production-off behavior and configuration remain
untouched.

## Actual agents and independent review

- **backend-architect** (`entitlement_backend`): implemented the two runtime files and preserved the
  earlier Portfolio DI work. Did not run tests.
- **testing-agent** (`entitlement_testing`): changed only the two authorized test files and executed
  all recorded tests serially.
- **security-agent role:** attempts to start/resume a separate security session were rejected by the
  collaboration tool's agent-thread limit. The testing agent explicitly read the security-agent role
  and independently reviewed the backend-authored client, DI and unchanged access authorizer in the
  same agent session. It did not author those runtime files. This is not a claim that a third,
  separately spawned security agent ran.
- **orchestrator/root:** captured baseline, independently reviewed runtime and test evidence, applied
  the two narrow review corrections in the authorized client, maintained pack/report and verified scope.

Security review found that `https://@platform.internal` normalized to empty Uri.UserInfo and could
reach the handler. The focused origin test first failed (11 passed / 1 failed); raw `@` rejection
closed it. Final unit and actual-factory no-network cases cover the correction.
Root also identified an unnecessary restriction on opaque keys containing internal spaces; review
confirmed no provider key grammar required that restriction. The final code preserves internal spaces
while rejecting control characters, and a positive test covers this compatibility boundary.
Final review found no remaining blocking issue in this scoped transport. Independent review of the
test code by root checked that log-redaction and redirect/TLS evidence were not vacuous.

## Executed final tests

All commands ran in the exact worktree above. Final integration rebuilt after the last source/test
changes; the initial successful runs below are not substituted for final-source validation.

```sh
dotnet test services/Diten.PpmService/tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj --no-restore --logger 'console;verbosity=minimal'
dotnet test services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Diten.PpmService.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~PortfolioAuthProviderIntegrationTests|FullyQualifiedName~PortfolioAuthProviderSupervisorTests|FullyQualifiedName~MongoPersistenceIntegrationTests|FullyQualifiedName~PortfolioOwnerAssignmentMongoTests|FullyQualifiedName~PortfolioAuthTransportIntegrationTests|FullyQualifiedName~PpmEntitlementTransportIntegrationTests' --logger 'console;verbosity=minimal'
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --no-restore --logger 'console;verbosity=minimal'
```

| Final run | Passed | Failed | Skipped | Duration | Exit | Raw local log |
|---|---:|---:|---:|---|---:|---|
| Full PPM unit | 474 | 0 | 0 | 6 s | 0 | `/private/tmp/ppm-entitlement-unit-final.log` |
| Selected integration | 78 | 0 | 0 | 1m 36s | 0 | `/private/tmp/ppm-entitlement-integration-final.log` |
| Architecture | 16 | 2 | 0 | 2 s | 1 | `/private/tmp/ppm-entitlement-architecture.log` |

474 = baseline 446 + 28 new unit cases. 78 = existing selected 64 + 14 new entitlement transport cases.
The shared-consumer cases cover Portfolio, Initiative, Program, Project, InvestmentCase and
BenefitCommitment permission decisions; allow, deny and dependency-unavailable ordering is preserved.
Existing real Auth/verified UDS/disposable Mongo, Portfolio ownership/CAS/replay/reassertion and
zero-mutation/audit rejection regressions remain included.

Architecture failures are the unchanged
`JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew` and
`JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew`, naming HumanCapitalService
and TalentEcosystemService Program.cs. Those files and the architecture guard were not changed;
the architecture suite is not green.

Earlier executed runs: focused TLS 13/13 (4 s), initial unit 472/472 (8 s), initial selected
integration 77/77 (1m 41s), then empty-userinfo red group 11/12 (exit 1).
The corresponding logs are `/private/tmp/ppm-entitlement-transport-initial.log`,
`/private/tmp/ppm-entitlement-unit-full.log`,
`/private/tmp/ppm-entitlement-integration-full.log`, and
`/private/tmp/ppm-entitlement-empty-userinfo-red.log`. They are historical steps within this
delivery, not additional unique passing test totals. The final runs supersede their acceptance counts.

## Security, cleanup and evidence limits

The new integration suite exercises the actual AddInfrastructure/HttpClientFactory composition,
without replacing the production primary handler. Before adding test trust, it asserts redirect,
proxy and cookies are disabled, runtime revocation is enabled and the runtime callback is absent.

The test CA is generated in memory and trusted only by the test client's CustomRootTrust chain.
Hostname mismatch/unavailable certificate, chain validity, expiry and server-auth EKU remain checked.
Only this ephemeral test CA's chain uses NoCheck because it has no revocation service; runtime
revocation remains enabled. No trust-all, host trust-store or proxy-environment modification occurred.

- Invalid root/hostname/expiry: a TCP connection is observed but no HTTP request/credential arrives.
- Redirect 302/307/308: the original approved test provider receives its request, but the destination
  sink accepts zero TCP connections. A different sink CA cannot hide a followed redirect.
- Invalid origin including empty userinfo: zero network connections.
- Two concurrent request scopes use one factory handler without mixing tenant/correlation.
- Incoming credentials/tenant/cookies are absent; response cookies are not replayed.
- Actual Trace output contains the service-key header name and masking marker, but not the random
  test credential. This is not a test that merely disabled logging.

TLS listeners/connections and certificates are disposed; existing Auth/PPM fixture cleanup and
secret-output assertions pass. Supplemental log scans found no JWT-prefix/Mongo-URI or fixed
synthetic poison markers. Generated artifacts are ignored build outputs and temporary test logs,
not additional source/config changes. A successful run does not close the previously recorded
Auth cleanup timing sensitivity.

The new TLS endpoint is an isolated transport fixture, not a deployed Platform entitlement provider.
Existing real Auth tests retain their documented PPM authentication/entitlement seams. No real
environment, tenant, actor, grant, credential, certificate profile or browser acceptance was created.

## Remaining environment prerequisites

The environment owner must still supply approved Auth and Platform HTTPS origins with normal TLS
trust/renewal ownership, protected matching entitlement credentials, compatible real JWT configuration
and an explicitly isolated non-production runtime. PSS must authorize disposable bootstrap actors,
permissions/Human classification and real entitlement data through existing paths. Separate authority
is still needed for environment startup, real PPM API smoke, then browser acceptance and cleanup.
This delivery does not repeat or extend the prior environment inventory/setup scope.

## Baseline preservation record

These hashes describe the fourteen pre-existing dirty files at entry, not work produced by this delivery.

| Baseline path | Initial SHA-256 |
|---|---|
| `docs/records/audits/2026-09/ppm-portfolio-auth-transport-implementation-2026-09-18.md` | `f8031d98f0d3db008244bc72aadc685120507406dcdca8423baf40dde34ee3d1` |
| `execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md` | `385a570f91cd07ea9f2dc8a30e9af937ce78ac4c0b47602d0628f4a3a06f6143` |
| `services/Diten.PpmService/src/Diten.PpmService.Api/Program.cs` | `2b49e728b58717a149c18e75b6fd05b30b3033a94ce0968a4e76c33c2a0ec1c3` |
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs` | `7ebd658df4bd69039e05c96c171cb641113d5a7ff8b174744ba279cd4032718d` |
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthRequestContext.cs` | `113538d4c7c2b3f6807c188b027d974ff25a05939183b3f67b2069a4b61658f5` |
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthTrustedTarget.cs` | `58c1deac857aa8d9ab7594dd453543998ecf1c10c8d31e26369293472c0f2d18` |
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthorityClient.cs` | `60e3dca05c4136e9844b9ee9adbb7b375a801683e5269de9953477dd5d39934f` |
| `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthorityOptions.cs` | `e0eaf1adafb0efb328796798fa99a4426f5324d6ae968c6012196a5c0bd72986` |
| `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthProviderIntegrationTests.cs` | `394cfe15c40131f540aaed2484ee2154fd9a5e396c19bc80c686a669d11d76d4` |
| `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthProviderProcessHost.cs` | `35d1b04b87be24b260bc793ff4db0e99eb6a55f9fd83859a770e80a17a6a6fc8` |
| `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthTransportIntegrationTests.cs` | `5e4cc1467e66de4ff227c123ea1f742be1b087c50dfe45f83d1ac481eda21017` |
| `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthTransportTestHost.cs` | `96352d2f3f45b5e0890dcef3df8e15b7287b7fe6f674cdd3e855012440ee70d9` |
| `services/Diten.PpmService/tests/Diten.PpmService.Tests/Portfolios/PortfolioAuthTransportTests.cs` | `9ac604fa5d550e3fb235d43261f80f406aabb1442ab1f958f4a371d7a3a8ed3a` |
| `services/Diten.PpmService/tests/Diten.PpmService.Tests/Portfolios/PortfolioAuthorityClientTests.cs` | `230f4a33721c42fe4d63dfe857c556b839666d65ca17224d312abc7ee5e98c28` |

Final checks: exact six-path incremental scope; combined eighteen-path dirty set; twelve non-overlapping
baseline files unchanged; prior DI and pack content recover their recorded hashes after removing only
this delivery's additions. Branch/HEAD unchanged, staging empty, git diff --check clean. No historical
Portfolio implementation report was edited.
