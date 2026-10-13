# Fresh command/exit summary

| Action | Result |
|---|---|
| `shasum -a 256 -c` against writer `SHA256SUMS` | all 54 entries OK (`input-sha256-check.txt`) |
| `tar -tzf` writer `evidence.tar.gz` | exit 0, 54 members (`input-archive-members.txt`) |
| Copy normal/evidence source to separate snapshot; compare non-generated SHA256 inventory | 14,556 files each, exactly two authorized differences |
| `git apply --check` and `git apply` against copied normal files | exit 0; both target hashes reproduced (`patch-reproduction.txt`) |
| `dotnet build ...Api.csproj -c Debug --no-restore --no-incremental -m:1 /nr:false` | exit 0, 0 warnings/errors (`raw/logs/build.log`) |
| `mongod --replSet claims_r22r25_indver --port 27925 ... --setParameter enableTestCommands=1` | isolated replica set started; sandbox-only socket EPERM attempt retained in log, successful retry used approved escalation |
| Copied mechanics with fresh API 51925 / Mongo 27925 / DB `diten_claims_r22r25_indver` | exit 1 because authored combined oracle assumes immediate 201; R22 and six stages passed (`raw/process-boundary.json`) |
| Separate authored committed/not-committed restart mechanics | exit 1 for first-201 committed receipt recovery and immediate not-committed 503 (`raw/unknown-commit-rerun.json`) |
| Verifier's `independent_timeline.py` | exit 0; failpoint on/off, first HTTP/DB, restart, open transaction, 60-second lifetime, later single-write 201 |
| `python3 verify-oracle.py` over raw captures | exit 0, `INDEPENDENT_ORACLE_PASS` (`oracle-result.txt`) |
| `failCommand` off and isolated replica shutdown | final failpoint off; API 51925 and Mongo 27925 have no listener |

The two author-derived mechanics scripts are preserved so their FAIL labels remain visible. The independent oracle does not inherit those labels or silently change their expected values; it tests the published 503-pending-recovery and committed-replay requirements against the raw observations.
