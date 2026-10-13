# Q236 — CT independent verification (2026-10-03)

CT applied Q236's proposed bytes and verified the result itself rather than accepting the lane's
record (SOP §32 K2).

## Bytes

| check | result |
|---|---|
| `diff` against `mvp6-q236-unbreak-v2-01/proposed/DependencyInjection.cs.txt` | **identical** |
| sha256 of the tree file | `f05c1fcd6a4e49950f04a6e0c952487c94c575a76811c9473d434c3f95ce7fcf` — matches Q236's run A and run C |
| build | `Build succeeded. 0 Warning(s) 0 Error(s)` |

The write went through the `Edit` tool, so it was governed by the `ask` rule the owner installed
today, not by a `cp` that would have bypassed it.

## Startup, measured by CT

Environment: `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://localhost:5061`,
`JwtSettings__Secret` **generated fresh by CT** (`openssl rand -base64 48`, never printed, never
written to a file) — deliberately NOT the value Q235 found exposed. `JwtSettings__Issuer`,
`JwtSettings__Audience`, `Mongo__ConnectionString=mongodb://localhost:27017`,
`Mongo__DatabaseName=diten_supplychain_ctverify`. mongod on 27017 was already running.

| observation | result |
|---|---|
| service up | **t = 3 s**, listening on 5061, still up 4 s later, stopped by SIGTERM |
| `GET /health` | **HTTP 200** |
| `GET /api/shipment-bundle/shipments` | **HTTP 400** — the route is mapped and the middleware runs; consistent with Q231 row 13 ("missing/malformed correlation → 400") |
| `GET /` | HTTP 404, expected (no root route) |
| unresolved service registrations | **0** (run B's signature was 18) |
| unhandled exceptions | none |
| CT's generated secret present in the log | **0 occurrences** |

Log: `boot2.log`. A first attempt (`boot.log`) died at `Program.cs:25` with
`JwtSettings:Secret is required` — a missing-configuration failure that occurs BEFORE the container
validation at line 64, and therefore not the Q236 defect. It already showed 0 unresolved
registrations.

## Verdict

**Q236 CT ACCEPTED.** The SupplyChain service starts in Development for the first time. This
unblocks the nine runtime measurements in
`mvp6-q231-mod0183-18-0-gap-01/RUNTIME-REQUIRED.tsv` (R-01…R-09), which no static analysis could
settle.

Not committed. `OD-Q03a` still says no commit.
