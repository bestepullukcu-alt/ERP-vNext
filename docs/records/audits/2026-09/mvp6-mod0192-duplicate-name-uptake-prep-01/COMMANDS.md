# Command/result transcript

All commands ran in the disposable source root unless stated otherwise.

| Purpose | Command summary | Exit/result |
|---|---|---|
| Baseline integrity | SHA-256 verification of 379-source base and 43-source overlay manifests | 422 entries, zero mismatch, zero overlap |
| Initial targeted RED | `dotnet test ... --filter Scenario_name_conflict...|Proven_name_index...` | exit 1, 0/2; exact message RED plus rejected experimental one-read oracle |
| Targeted GREEN | `dotnet test ... --filter Scenario_name_conflict...|Proven_name_index...|Duplicate_policy... --logger trx` | exit 0, 3/3 |
| Broad attempt, discarded | `dotnet test ... --filter FullyQualifiedName~CapacityPlans --logger trx` against Mongo without test commands | exit 1, 27/34; seven failpoint setup failures |
| Mongo correction | restart isolated `rsmod192`/57192 with `enableTestCommands=1` | ready; no operational 27017 access |
| Final Capacity regression | same CapacityPlans filter and TRX logger | exit 0, 34/34 in 1m53s |
| Patch dry-run | `patch -p1 --dry-run < PROPOSED-UPTAKE.patch` against exact 43-source preimage | exit 0, three paths |
| Patch reproduction | apply patch to second disposable preimage and SHA-256 all targets | exit 0; all `SOURCE-MANIFEST.tsv` targets exact |

The attempted later extraction of a one-test RED run hit a sandbox-local MSBuild named-pipe
permission error and was cancelled. The production mapper file was restored immediately to
target hash `c55ad161...`; this attempt created no usable evidence and is not counted.
