# A01–A12 consolidation

All PASS statements below are historical CT dispositions, not fresh test executions. CT01 and CT03 are durable current records. Named code files are corroborating source pointers, not replacement runtime proof. Raw-path availability: missing-evidence.tsv.

|AC|Preserved decision|Exact evidence mapping|
|---|---|---|
|A01|CT01§A01 PASS|runtime.json; LoadContractTests.cs / full132TRX|
|A02|CT01§A02 PASS|LoadContractTests.cs; independent ver02-full-net8.trx132/132|
|A03|CT01§A03 PASS|LoadIsolationTests.cs; full132TRX RBAC/isolation|
|A04|CT03 ACCEPTED closure; CT01/02 deficiencies superseded|A04 REWORK02→VER02 failure-paths.json e540570e0e7cd2067927a2e9f97e99500ec927e5193f05657561bb1fa7e9315e; independent-query.json809c477858ea90401ffffe07a971afaa68b7817cc9c97c42543fffd7b0874e5c|
|A05|CT01§A05 PASS|LoadLifecycleTests.cs; full132TRX|
|A06|CT01§A06 PASS|LoadReplayTests.cs; runtime/restart.json; no dependency reread|
|A07|CT01§A07 PASS; CT03 inherited closure preserved|DEV03Loads33/33; enableTestCommands, standalone/unavailable/createIndexes independent logs|
|A08|CT01§A08 PASS|LoadContractTests/LoadReferenceTests/LoadConcurrencyTests; full132TRX|
|A09|CT01§A09 PASS|VER02 restart.json two process IDs durable state/Pending outbox|
|A10|CT01§A10 PASS bounded mocks|GET-only reference boundary; live sourceDB excluded|
|A11|CT01§A11 service regression PASS|independent132/132; architecture50/3 historical external failures not waived|
|A12|CT01§A12 PASS; CT03 inherited closure preserved|DEV02/03 transport probes; independent294-byte create/replay,0-byte GET,18verifierchecks,negative records|
