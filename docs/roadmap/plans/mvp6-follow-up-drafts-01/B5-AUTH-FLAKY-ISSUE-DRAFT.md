# Draft issue — Auth `auth-app` CI job is red on main with a different test each run (B5)

> Draft for the owner to file on GitHub (no `gh` on the CT machine). Measured 2026-10-20 on main runs up to
> `673995c15`; main is `9d44e8a1f` as of 2026-10-21 (no new commits since). Not re-measured in CI today.

**Title:** `auth-app` CI red on main in 5 of 6 runs — a different Mongo test fails each time (flaky on Linux CI)

**Body:**

The `auth-app` job of the service-tests workflow was red on `main` in 5 of the last 6 runs measured on 2026-10-20.
Each run failed a **different** test, and the failures started before #164 — so this is not the Q358/G7 change:

| test class | file |
|---|---|
| `ProductIdentityEntitlementReconciliationMongoTests` | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/ProductIdentityEntitlementReconciliationMongoTests.cs` |
| `AnonymousDoorLimitTests` | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AnonymousDoorLimitTests.cs` |
| `OperationalSingleInstanceLockTests` | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/OperationalSingleInstanceLockTests.cs` |

- Locally (macOS) `OperationalSingleInstanceLockTests` passed 30 of 30 repeated runs; on the same commits the PR runs
  were green. The pattern (one random Mongo/timing-sensitive test per run, Linux only) points at shared state or
  timing between tests on CI's mongod 7.0.28, not at one broken test.
- Effect: `main` shows a red `auth-app` most of the time, which hides real regressions and makes reviewers ignore
  the job.

**Asked of the Auth owner**
1. Look at the three failing runs' logs together (same fixture? same collection? a lock/TTL or clock dependency?).
2. Decide: fix the shared cause, or register them as known flaky (with a named owner and an expiry) until fixed.
3. Do not retry-until-green in CI: that hides the cause.

**Not measured:** the exact assertion message of each failure today; whether the job is still red after 2026-10-20.

Labels (suggestion): `ci`, `flaky-test`, `auth` · Owner: Auth service owner · Related: plan v10.0 row B5.
