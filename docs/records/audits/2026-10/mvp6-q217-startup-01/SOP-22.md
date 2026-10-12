# Q217 — Service startup check in Development · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q217 · `AL-SCM-STARTUP` (INS) · required E3 — **reached: E3 in part** (real process start in two environments + unauthenticated HTTP; no authenticated call) |
| CT-QUEUE row | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:313` (present, READY) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `devops-agent`. **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** Fields as supplied by CT in the prompt. |
| Read first (in order, in full) | `AGENTS.md` · `.antigravity/agents/devops-agent.md` · `.antigravity/rules/dev-runbook.md` · `configuration-safety.md` · `ports.md` · `mongo-indexing.md` · Q208 `PROVISIONING-STEPS.md` · Q211 `REACHABILITY.md` |
| Base Stack (R9) | `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882`; folder `SHA256SUMS` check OK |
| Mongo script | `scripts/test-env/mvp6-test-mongo-env.sh` sha256 `6c936b4a84bb89a27115190c2ea0c87488e690e8ef7dcf1e383ffaeff54d13b1` ✔ (not executed; the lane set was already up) |
| Start / End (Europe/Istanbul) | 2026-10-02 23:00:40 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        MEASURED. Development: FAILS AT STARTUP. Agent PASS ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      start 665 porcelain lines (= stated baseline); 665 after builds and runs;
                      end: see ARTIFACTS.sha256 header. No .git/index.lock. No git diff. No git write. No gh.
Other build in flight: none
Changed files:        this record folder + /private/tmp/q217-startup-01/ + git-ignored bin/ obj/
Build:                0 errors, 0 warnings (incremental, then --no-incremental). BUILD.md
Startup:              Development → exit 1 at Program.cs:64, 18 unresolvable handlers. STARTUP-VERDICT.md
                      Production (supplementary, env var only) → boots, listens on 127.0.0.1:5061
HTTP:                 10 distinct requests, none authenticated. HTTP-EVIDENCE.md
Persistence evidence: none (no request reached a handler). Schema services created 16 empty collections.
Security evidence:    every mapped route refuses an unauthenticated call (401, or 400 before it)
Decisions:            none taken
Blockers:             none for this WP
Out-of-scope changes: none. Program.cs, appsettings, .csproj, ocelot.json, tests, service code, scripts/,
                      port table: untouched. Port 27017 and the homebrew mongod: not contacted.
Refused:              nothing was refused. Not done by instruction: no token was invented.
```

## 1. Answers to the seven tasks

1. **Base stack and build.** Hash verified (above). Build: 0 errors, 0 warnings; a full recompile reproduced
   the binaries that ran byte for byte. → `BUILD.md`
2. **Mongo.** Lane set `rsq208s1` on 127.0.0.1:31994 was healthy (primary) and reused. Script hash verified.
   Nothing pointed at 27017.
3. **Start in Development: FAILS AT STARTUP.** `System.AggregateException` at `Program.cs:64`: 18 MediatR
   handlers cannot be built because `IReturnRepository`, `IClaimRepository`, `ISandopRepository` and
   `ICapacityRepository` are not registered. Port 5061 never opened. → `STARTUP-VERDICT.md`
4. **Calls.** Impossible in Development. A supplementary run with only the environment name changed to
   Production boots, and was called:
   (a) GET `/api/shipment-bundle/shipments` → 401, Shipment contract error, correlation header echoed.
   (b) GET `/api/shipment-bundle/claims` → 401 with the same Shipment body; without a correlation header 400
   with the Shipment message. **The Shipment middleware's rules were applied to Claims (and Returns)
   requests** — the Claims middleware would have answered differently. → `HTTP-EVIDENCE.md`
5. **Auth.** Every call needs a JWT (HS256, issuer, audience, lifetime) and, on the Shipment branch, the claims
   `sub`, `tenant_id`, `legal_entity_id`, `permission` plus matching scope headers. No token was invented, so
   (a) and (b) got as far as the authentication check and no further.
6. **Plain statement.**
   - **MOD-0183 has its first HTTP evidence, and it is thin:** `/health` 200, and the Shipments list refusing
     an anonymous call correctly (401 / 400 with the contract error and correlation). No authenticated
     Shipments request has ever been made over real HTTP; no handler ran. And this evidence exists only
     outside Development — in Development MOD-0183 does not start at all.
   - **The four unwired modules are not inert. They are broken, and they break the host.** Their handlers
     stop the whole service from starting in Development. Outside Development the service starts, their
     routes are mapped and answer 401; Claims and Returns are answered by the wrong module's middleware,
     S&OP and Capacity by no module middleware. Behind authentication their handlers cannot be constructed —
     that last step is derived from the startup exception, not observed over HTTP.
7. **Shutdown.** Everything this WP started is stopped. Left running: homebrew mongod (pid 825) and the Q208
   lane mongod (pid 2363), neither started here. → `CLEANUP.md`

## 2. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| F-Q217-1 | HIGH | The service does not start in Development since Q202a: 18 handlers of MOD-0186/0187/0190/0192 need four unregistered repositories. This takes MOD-0183, Carriers and Loads down with it. Confirms F-Q211-7. | `STARTUP-VERDICT.md` |
| F-Q217-2 | HIGH | The fault is invisible to every existing test: host tests run as `Testing`, where the container is not validated. In Production the service also boots and the fault moves to request time. A green suite and a "0 error" build both coexist with a service that cannot start in the environment the runbook prescribes. | `STARTUP-VERDICT.md` table; Q211 `REACHABILITY.md` |
| F-Q217-3 | HIGH | Claims and Returns requests are answered by the Shipment middleware with the Shipment contract (400 `X-Correlation-Id is required` where Claims' own rule is 401 `UNAUTHENTICATED` + generated correlation). F-Q210-3 is now measured at runtime, not only reasoned. | `HTTP-EVIDENCE.md` (b) |
| F-Q217-4 | MEDIUM | S&OP and Capacity routes (`/api/supply-chain/…`) are mapped and reachable with no module middleware in the pipeline: a bare 401 with no contract body and no correlation header. | `HTTP-EVIDENCE.md` additional requests |
| F-Q217-5 | MEDIUM | The service has no developer configuration: `JwtSettings:*` and `Mongo:*` are absent from `appsettings.json`, and there is no `appsettings.Development.json` or `launchSettings.json`. A plain `dotnet run` stops at `JwtSettings:Secret is required` before reaching F-Q217-1, and would run as Production, not Development. | `WHAT-Q209-NEEDS.md` §1–2 |
| F-Q217-6 | MEDIUM | E3 is only partly reached. No authenticated request exists for any module. The per-request failure of the four modules is derived, not observed. One authenticated call per module would close it; it needs a CT decision on the token. | `HTTP-EVIDENCE.md` last section; `WHAT-Q209-NEEDS.md` §5 |
| F-Q217-7 | LOW | The mandated reads disagree with each other on ports: `devops-agent.md:22-25` still lists MDM on 5050; `ports.md` stops the band at 5064 and has no row for SupplyChain 5061 or CRM 5065, while `AGENTS.md` §3 assigns both. Nothing was changed. | the three files |
| F-Q217-8 | LOW | The prompt asked to verify the BASE-STACK hash but quoted none. The on-disk value and the folder's own `SHA256SUMS` agree; there was nothing external to compare with. | `BUILD.md` |
| F-Q217-9 | LOW | The Production run left database `diten_q217_startup` (16 empty collections) on the Q208 lane set. Not dropped: this lane deletes nothing. | `CLEANUP.md` |

## 3. Notes on this run

- The supplementary Production run was this lane's choice. It changed one environment variable and no file;
  the prompt's stop condition is about needing to edit `appsettings`, `Program.cs` or a `.csproj`, which was
  not needed. Without it there would be no HTTP evidence at all. CT may disregard it; the Development
  verdict does not depend on it.
- The ten requests were sent twice; the first batch's output was lost to a faulty output filter in this
  lane's own command. Status codes of both batches agree in the service log.
- Port 5061 was free and was used as assigned. No port was reserved anywhere.
- Known gap: whether registering the four `Add…Persistence()` methods is sufficient for startup is not
  measured (validation reports one missing parameter per handler).
