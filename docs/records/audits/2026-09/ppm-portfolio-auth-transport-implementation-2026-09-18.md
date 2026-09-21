# Portfolio Auth transport — scoped implementation and isolated evidence

Date: 2026-09-18. Worktree: `/Users/alitufanoglu/ERP-vNext-codex-current`.
Branch: `codex/ppm-portfolio-first-delivery`.
Baseline and final HEAD: `1f63c2e3f657f484cfbff5217c2f6edf10fe91a5`.

**Result:** The authorized transport and isolated tests are implemented. PPM unit tests pass
446/446; selected real-Auth/supervisor/disposable-Mongo/TLS integration tests pass 64/64, with zero
failures or skips. Architecture remains 16/18 with the two previously recorded HCM/TEP ClockSkew
failures. This is not real runtime, browser or production acceptance. No runtime endpoint has been
provisioned and the transport remains default-off/production-off.

## Authority and exact changed files

The user's explicit implementation message authorized these 14 paths and isolated tests; the earlier
proposed prompt was not authority. No new pack, general audit or additional source-file scope was opened.
The MOD-0117 scoped amendment records that approval without promoting the overall module from review.
Every path below is relative to the exact worktree above; these are all source/document changes.

| # | Exact path | Change |
|---|---|---|
| 1 | `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthorityOptions.cs` | Existing: default-off, bounded timeout, explicit non-production environment and operator-supplied approved origin/owner/approval reference. |
| 2 | `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthorityClient.cs` | Existing: validate context and bind each outbound message; preserve business response mapping. |
| 3 | `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs` | Existing: scoped request context, identity-free pooled handler, startup validation and sensitive header redaction. |
| 4 | `services/Diten.PpmService/src/Diten.PpmService.Api/Program.cs` | Existing: explicitly save the successfully authenticated Bearer token. |
| 5 | `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthRequestContext.cs` | New: successful Bearer ticket and actor/tenant/header/scope binding; no raw Authorization fallback. |
| 6 | `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthTrustedTarget.cs` | New: one approved HTTPS origin and three routes; normal TLS; redirects/proxy/cookies off. |
| 7 | `services/Diten.PpmService/tests/Diten.PpmService.Tests/Portfolios/PortfolioAuthorityClientTests.cs` | Existing: adapt independent business/response tests to explicit authenticated context. |
| 8 | `services/Diten.PpmService/tests/Diten.PpmService.Tests/Portfolios/PortfolioAuthTransportTests.cs` | New: actual factory concurrent-scope reuse, saved-token/poison-header proof, negative context/profile cases and startup validation. |
| 9 | `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthProviderProcessHost.cs` | Existing: credential-free forwarding client over the existing kernel-verified UDS boundary. |
| 10 | `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthProviderIntegrationTests.cs` | Existing: real Auth through TLS, actual provider failure with a valid token, unchanged business/CAS/replay/audit effects and cleanup. |
| 11 | `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthTransportTestHost.cs` | New: run-owned TLS bridge, client-local CA trust, no fixture credential substitution, connection counters and cleanup. |
| 12 | `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioAuthTransportIntegrationTests.cs` | New: invalid TLS zero HTTP/credential, redirect zero destination connections, valid TLS and default-header credential rejection. |
| 13 | `execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md` | Existing: scoped authorization and evidence link only. |
| 14 | `docs/records/audits/2026-09/ppm-portfolio-auth-transport-implementation-2026-09-18.md` | New: this dated evidence record. |

## Delivered security and lifetime behavior

`PortfolioAuthRequestContext` is scoped to the incoming request. It explicitly requests Bearer
authentication, requires a successful Bearer ticket, and consumes only the ticket's saved access token.
It compares the authenticated principal, current trusted PPM actor/tenant, the single tenant header,
and Portfolio actor/tenant/record-tenant scope. Conflicting claims and platform tenant/actor contexts
fail closed. It never treats a raw header as authentication evidence.

Every lookup, account-assertion and display-label message gets its own Authorization and X-Tenant-Id
headers after these checks. Factory handlers retain only connection settings. No credential or tenant
is held in pooled handlers, singletons or DefaultRequestHeaders; contaminated default headers close the
adapter. Existing entitlement, effective permission and record relation gates remain in place, together
with business 403/404, the two label 409 responses, unavailable 503, CAS/replay and transaction reassertion.

The origin is a single operator-supplied approved HTTPS origin, not a URL from the request. The code
requires profile owner/approval provenance and an explicit matching non-production environment;
unknown/production environments cannot enable it. An enabled invalid profile fails startup validation.
Normal runtime TLS hostname/chain/validity/revocation validation is retained without a custom certificate
callback. Redirects, proxies and cookies are disabled. No certificate/SPKI pin mandate was introduced.
Operator provenance fields do not independently prove endpoint ownership: the real environment owner
must validate and supply that information before activation.

The test-only TLS bridge uses a run-owned CA and custom trust only in its own client. It preserves
hostname rejection and validates the chain, expiry and server-auth EKU. Its ephemeral CA has no
revocation service, so only that test client's custom chain uses NoCheck; runtime revocation checking
remains enabled. No machine trust store was altered. The bridge forwards the received token/tenant
through a credential-free client over the existing UDS boundary, whose new connections verify kernel
peer and root/socket identities. No default fixture token can conceal the actual forwarded credential.

## Agents actually executed and review findings

- **orchestrator (root):** verified baseline and exact allowlist, recorded scoped authority, coordinated
  work, reviewed diffs and test evidence, and wrote this report. Identified and corrected a redirect-test
  evidence gap: a sink with an independent CA could have hidden a followed redirect at TLS; the final
  test requires zero accepted TCP connections at the sink. TLS negatives also require an accepted
  connection before asserting zero HTTP/credential, avoiding vacuous no-call success.
- **backend-architect:** implemented the six runtime files, then assisted testing-agent in the two
  approved unit files. Fixed init-only option construction; added positive saved-token-versus-poisoned
  raw-header evidence and negative trusted-context/profile/startup cases. No extra source paths used.
- **security-agent:** independent read-only review of runtime, bridge and final unit tests; no remaining
  blocking finding. Found that an intermediate dead-client adaptation measured missing-token rejection
  rather than provider failure; it was replaced with a valid saved token plus actual bridge 503 and an
  increased HTTP request count, preserving unchanged version/history/audit assertions. Required Auth
  cleanup even if bridge initialization/disposal fails; catch/finally now protects it. Confirmed the
  corrected redirect/TLS evidence, normal runtime TLS, scoped credentials and UDS boundary.
- **testing-agent:** authored/adapted the six approved test files (final unit assistance as above), ran
  all commands below serially, and checked cleanup and secret-output evidence. Security-agent did not
  claim to execute tests; its test-result statements refer to testing-agent's actual runs.

## Executed commands and results

Commands ran in the worktree above. All used `--logger 'console;verbosity=minimal'`.
The first integration command built the changed integration/runtime sources; subsequent full selected
integration used those binaries. The full unit command built its final sources. No additional package
or project-file edits were required.

```sh
dotnet test services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Diten.PpmService.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~PortfolioAuthTransportIntegrationTests' --logger 'console;verbosity=minimal'
dotnet test services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Diten.PpmService.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~PortfolioAuthProviderIntegrationTests|FullyQualifiedName~PortfolioAuthProviderSupervisorTests|FullyQualifiedName~MongoPersistenceIntegrationTests|FullyQualifiedName~PortfolioOwnerAssignmentMongoTests|FullyQualifiedName~PortfolioAuthTransportIntegrationTests' --logger 'console;verbosity=minimal'
dotnet test services/Diten.PpmService/tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj --no-restore --logger 'console;verbosity=minimal'
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --no-restore --logger 'console;verbosity=minimal'
```

| Run | Passed | Failed | Skipped | Test duration | Exit | Local raw log |
|---|---:|---:|---:|---|---:|---|
| Focused real TLS | 5 | 0 | 0 | 2 seconds | 0 | `/private/tmp/ppm-transport-integration-initial.log` |
| Selected integration | 64 | 0 | 0 | 2m 29s | 0 | `/private/tmp/ppm-transport-integration-full.log` |
| Full PPM unit | 446 | 0 | 0 | 9 seconds | 0 | `/private/tmp/ppm-transport-unit-full.log` |
| Architecture | 16 | 2 | 0 | 3 seconds | 1 | `/private/tmp/ppm-transport-architecture.log` |

The 64 integration cases include the existing real Auth provider 6, supervisor 17, Mongo persistence/owner
36 and new TLS 5. The 446 unit cases comprise the previous 409 plus 37 new transport cases. The focused
5 TLS cases are included again in the selected 64; they are not an additional unique acceptance total.

The two architecture failures are `JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew`
and `JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew`, both naming existing
HumanCapitalService and TalentEcosystemService API Program.cs files. Those source files and the guard
are unchanged. They match the prior merge's recorded 16/18 baseline; the overall architecture suite is
not green. Existing Auth/Platform build warnings remain outside scope.

The run-owned Auth/PPM Mongo, UDS supervisor, secret-output and cleanup assertions passed. Logs were
also checked for JWT-prefix and Mongo-URI markers, with none found; that marker scan supplements,
rather than replaces, the fixture's actual secret-output checks. Generated files were ignored bin/obj
outputs and local temporary test artifacts/logs, not additional source/config changes.

## Evidence boundaries and open environment inputs

The merge baseline records Auth focused fixture 65/65, Auth separate-process host 68/68, PPM unit
409/409 and selected PPM integration 59/59. The unchanged standalone Auth fixture/68-test host suites
were not rerun in this delivery. The new selected integration run exercised the existing PPM supervisor
and real Auth provider as listed above. The earlier SIGKILL cleanup timing sensitivity remains open;
a later successful run does not by itself close that historical risk.

The C3 calls use real fixture-login JWTs checked by real Auth. PPM authentication tickets and PPM
entitlement decisions in these tests remain explicit seams. The actual factory and TLS behavior is
measured; the deployed PPM JWT pipeline, real entitlement provider and Platform login-settings integration
are not thereby accepted. No browser was started and no real endpoint was configured.

Before real non-production technical smoke, the environment owner must supply and verify:

1. The exact Auth HTTPS origin/DNS/port and matching certificate name, valid chain/trust roots,
   certificate/revocation reachability, accountable trust-profile owner, approval reference and renewal
   procedure. No arbitrary URL or implicit loopback fallback is acceptable.
2. The explicit non-production environment name and owner-controlled deployment values for
   PortfolioOwnerAuthority. This delivery did not set them or activate the transport.
3. Compatibility of the real login JWT profile with both PPM and the three Auth GET endpoints;
   existing permitted test actors/tenant and `auth.users.lookup` consumption permission.
4. A real PPM entitlement-provider endpoint and protected credential configuration, effective
   Portfolio permissions and valid PPM record-access binding. Grants/classification/provisioning are
   environment-owner work and were not performed here.
5. Authorized real non-production smoke through frontend/Gateway/PPM: successful and rejected JWTs,
   tenant mismatch, real entitlement Allow/Deny/dependency failure, existing record/owner permissions
   and actual Auth account facts. Only after this separate evidence may browser acceptance begin.

## Final scope verification

The initial worktree was clean. Final changes are exactly the 14 paths listed above; no staged changes,
branch switch, commit, push, PR, Auth/Platform/Gateway/frontend source or real config/secret change.
HEAD remains the baseline. Historical control reports were not edited. `git diff --check` passes.
This record reports scoped code/test completion with the architecture baseline failures and real
runtime/browser gates still open; it does not close the entire Portfolio module.
