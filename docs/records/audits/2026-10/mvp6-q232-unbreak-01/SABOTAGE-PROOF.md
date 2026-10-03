# Q232 — Sabotage proof (K3)

**Result: the proof does NOT hold in the form the WP expects.** With the four lines removed the service fails at
startup — as expected. With the four lines present it **also fails at startup**, with a different and shorter
error list. So the four lines are necessary and not sufficient.

All runs: scratch copy, Debug build, `ASPNETCORE_ENVIRONMENT=Development`, same variables (`STARTUP-AND-HTTP.md`).

| Run | `Program.cs` sha256 (in the copy) | Build | Start (+03) | Outcome | Inner errors | Fails at | Log |
|---|---|---|---|---|---:|---|---|
| A. lines present | `b417e769…` | 0 errors / 0 warnings | 23:17:30 | exit 134 within ~1 s; port 5061 never opened | **13** | `Program.cs:72` (`builder.Build()`) | `evidence/service-fixed.log` |
| B. lines removed (sabotage) | `7fdb5ef0…` (= repo bytes) | 0 errors / 0 warnings | 23:18:05 | exit 134 within ~1 s | **18** | `Program.cs:64` (`builder.Build()`) | `evidence/service-sabotage.log` |
| C. lines restored | `b417e769…` | 0 errors / 0 warnings | 23:18:10 | exit 134 within ~1 s | **13** | `Program.cs:72` | `evidence/service-fixed2.log` |

Run B reproduces Q217 exactly: the same 18 handlers, the same 4 repository interfaces.
Runs A and C are identical to each other.

## What the four lines change

| Unresolved type | Lines removed (B) | Lines present (A, C) |
|---|---:|---:|
| `Domain.Features.Returns.IReturnRepository` | 3 | 0 |
| `Domain.Features.Claims.IClaimRepository` | 3 | 0 |
| `Domain.Features.SandopPlans.ISandopRepository` | 6 | 0 |
| `Domain.Features.CapacityPlans.ICapacityRepository` | 6 | 0 |
| `Domain.Features.SandopPlans.IDemandFixtureReader` | hidden | 3 |
| `Application.Features.Returns.IReturnReferenceReader` | hidden | 1 |
| `Application.Features.Claims.IClaimReferenceReader` | hidden | 2 |
| `Domain.Features.CapacityPlans.IDemandFixtureReader` | hidden | 7 |
| **Total** | **18** | **13** |

"Hidden": container validation reports only the first constructor parameter it cannot resolve, so these were
masked by the repository errors. Q217 predicted this second round (`WHAT-Q209-NEEDS.md` §1, option A).

So the lines are doing real work — all 18 repository errors disappear — but what the sabotage distinguishes is
"18 errors" from "13 errors", not "fails" from "starts".

## Scratch state at the end

The copy holds the proposed bytes (`b417e769…`). The repo file was never touched (`7fdb5ef0…` at start and end).
