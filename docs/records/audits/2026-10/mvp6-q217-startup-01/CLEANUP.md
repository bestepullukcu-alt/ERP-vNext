# Q217 — Cleanup

## Started by this WP, and stopped

| What | Pid | Stopped how | State now |
|---|---|---|---|
| Service, Development run | (exited by itself, code 1) | — | gone |
| Service, Production run on 127.0.0.1:5061 | 15026 | `kill -INT`; log shows `Application is shutting down...`; exit code 0 | gone; 0 listeners on 5061 |
| MSBuild worker nodes from the two builds | 14595, 14596, 14597, 14599, 14608, 14609 (+ nodes of build 2) | `dotnet build-server shutdown`, then `kill` / `pkill` (SIGTERM) on the idle nodes | gone; 0 `dotnet` processes |

## Not started by this WP, left running

| What | Pid | Note |
|---|---|---|
| Homebrew mongod, 127.0.0.1:27017 and [::1]:27017 | 825 | Not contacted. Same pid and listeners before and after. No variable pointed at 27017; neither service log contains "27017". |
| Lane mongod `rsq208s1`, 127.0.0.1:31994 | 2363 | Found healthy (primary) and reused, as the prompt allowed. Left running as Q208 left it. Stop: `mongosh --quiet --port 31994 --eval 'db.adminCommand({shutdown:1})'`. |

## Left behind (nothing was deleted)

- Database `diten_q217_startup` on the lane set (31994). The Production run's schema services created 16
  empty collections with their indexes: `sce_shipments`, `sce_shipment_audit`, `sce_shipment_history`,
  `sce_shipment_outbox`, `sce_shipment_receipts`, `sce_shipment_source_evidence`,
  `sce_shipment_source_intents`, `sce_shipment_source_links`, `loads`, `load_assignments`, `loads_audit`,
  `loads_outbox`, `loads_receipts`, `carriers`, `carrier_audit`, `carrier_idempotency`. Data size 0.
  It disappears with the lane's `/private/tmp` dbpath at the next restart. No Returns, Claims, S&OP or
  Capacity collection was created — their schema services are not registered.
- Scratch folder `/private/tmp/q217-startup-01/` (19 files, about 100 KB): `build.log`, `build-full.log`,
  `service.log`, `service-run2-production.log`, `unresolved.txt`, `dll-ran.sha256`, `mongo-health.txt`,
  two start-time notes and ten `http-*.txt`.
- `bin/` and `obj/` of eight projects, rewritten by the builds (git-ignored).
- `~/.aspnet/DataProtection-Keys` was read by the Production run ("User profile is available"); whether a
  key file was added there was not checked.

## Repository

- `git status --porcelain`: 665 lines at start, 665 after the builds and runs, 665 after this record
  folder was written (it sits inside the already-untracked `docs/records/audits/2026-10/`, so it adds no line).
- No `git add`, commit, push, stash, checkout, diff or `gh`. No `.git/index.lock` seen.
- Not edited: `Program.cs`, `appsettings.json`, any `.csproj`, `ocelot.json`, test code, service code,
  `scripts/`, the port table.
- Disk: 7.8 GiB free at start, 7.5 GiB at end (above the 2 GiB stop line throughout).
