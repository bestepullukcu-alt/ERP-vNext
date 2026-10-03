# Isolated native .NET 8 launch and cleanup plan

This is a HELD environment plan. It starts no process and carries no Auth-dependent E2E authority.

## Start gate

1. Require the approved/applied Auth rework successor and its independent runtime PASS handoff. The current
   independent result is REWORK and cannot start Carrier E2E.
2. Rehash that successor's exact source archive/manifest and `FINAL-UI-SOURCE.tsv`; stop on any mismatch.
3. Re-run `lsof` for every port in `PORT-DB-PLAN.tsv`; any listener blocks start. The ports were free during this
   preparation, but that observation is not a reservation.
4. Run the explicit native host prefix recorded in `NATIVE-NET8-EVIDENCE.txt`. Record `--info`, `--list-runtimes`,
   each target `runtimeconfig.json`, and the absence of both roll-forward variables.
5. Use a unique `/private/tmp/mvp6-carrier-real-auth-e2e-ver-*` runtime root. Do not write credentials or usable
   bearer tokens to evidence.

## Process order and provenance

Start the lane-owned Mongo replica set on 37484, prove PRIMARY, then start MDM, Platform, Auth, SupplyChain,
Gateway, and Web. All .NET components must use `/Users/natig/.dotnet/dotnet`; every process receives an explicit
`ASPNETCORE_URLS` value and lane-local configuration. Gateway uses a disposable route copy pointing only to the
reserved ports.

For each process record source-manifest SHA-256, binary SHA-256, executable path/hash, runtimeconfig hash, PID,
start time, command with secrets redacted, port, and database name. Use the fixed service databases in
`PORT-DB-PLAN.tsv` and isolate scenarios with fresh tenant/legal-entity/actor identities. Never connect to Mongo
27017 and never reuse another lane's database.

## Controlled cleanup

Record PIDs at launch. Before stopping a PID, verify its command contains the expected disposable root and binary;
otherwise stop and do not touch the foreign process. Shut down in this order: Web, Gateway, SupplyChain, Auth,
Platform, MDM, Mongo. Send TERM, wait a bounded interval, and use KILL only for the same still-running verified PID.
Do not use broad `pkill`.

After cleanup, prove every planned port has no listener, the Mongo child is gone, and the final 21-path UI manifest
still has 21/21 exact hashes. Archive launch/exit logs and redacted evidence before disposing the lane-local runtime
directory.
