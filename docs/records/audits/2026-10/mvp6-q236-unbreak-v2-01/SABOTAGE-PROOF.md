# Q236 — Sabotage proof (SOP §32 K3)

**The proof holds.** With the change the service starts in Development and stays up; with the change reverted the
same service fails at `builder.Build()`; with the change restored it starts again.

All three runs: the scratch copy, Debug build, `ASPNETCORE_ENVIRONMENT=Development`, identical environment
variables (`STARTUP-AND-HTTP.md`), started by `evidence/run.sh`. The only thing that differs between runs is
`DependencyInjection.cs`.

| Run | `DependencyInjection.cs` sha256 | `Application.dll` sha256 | Build | Start (+03) | Outcome | Unresolved registrations | Log |
|---|---|---|---|---|---|---:|---|
| A. change present | `f05c1fcd…7fcf` | `54fc7149…abd4` | 0 errors / 0 warnings | 00:16:07 | **UP** at 00:16:09, listening on 5061, still up at 00:16:39, stopped by SIGTERM, exit 0 | **0** | `evidence/service-A-fixed.log` |
| B. change reverted (sabotage) | `b1779280…c3b2` (= repository bytes) | `427c4298…e19a` | 0 errors / 0 warnings | 00:16:56 | **exit 134 within 1 s**; port 5061 never opened; fails at `Program.cs:line 64` | **18** | `evidence/service-B-sabotage.log` |
| C. change restored | `f05c1fcd…7fcf` | `54fc7149…abd4` | 0 errors / 0 warnings | 00:17:01 | **UP** at 00:17:02, still up at 00:17:33, stopped by SIGTERM, exit 0 | **0** | `evidence/service-C-restored.log` |

Runs A and C produce the same `Application.dll` hash and the same HTTP bodies (compared with UUIDs normalised).

## Run B in detail

It reproduces Q217 exactly: 18 distinct handler registrations, four repository interfaces, no validator.

| Unresolved type | Handlers |
|---|---:|
| `Domain.Features.Returns.IReturnRepository` | 3 |
| `Domain.Features.Claims.IClaimRepository` | 3 |
| `Domain.Features.SandopPlans.ISandopRepository` | 6 |
| `Domain.Features.CapacityPlans.ICapacityRepository` | 6 |
| **Total** | **18** |

(Each message appears three times in the log text — once in the aggregate summary and twice in the nested trace —
so a plain `grep -c` returns 54.)

## What the sabotage distinguishes

Unlike Q232, the two states are "starts" and "does not start", not "18 errors" and "13 errors".
Both logs A and C contain `Hosting environment: Development` and `Application started`.

## Where this was measured

In the scratch copy, because the repository edit was refused. The repository file is `b1779280…c3b2` — the bytes
of run B. So the repository today is in the failing state, and this proof says what applying the proposed bytes
would change.
