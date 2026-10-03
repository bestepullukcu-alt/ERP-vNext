# Cleanup — MVP6-SHIPMENT-A12-RUNTIME-INDEPENDENT-VER-02

Machine-checked rows: `CLEANUP.tsv` (evidence-kit K11, run with `GIT_OPTIONAL_LOCKS=0`, 2026-09-26 07:48 UTC / 10:48 Istanbul).

| Resource | Result |
|---|---|
| Browsers | The harness closed every Playwright context after each phase. Before cleanup, 0 Chromium processes were using a lane profile (`raw/browser-processes-before-cleanup.txt`). The three per-actor persistent profiles, which held session cookies, were deleted with the workspace |
| Lane supervisor (sole holder of the in-memory secrets) | `shutdown` request: it cleared its secret dictionaries and exited; its control socket was removed. The secrets were never written to a file |
| .NET processes | Auth 16224, Platform 16257, MDM 16319, SupplyChain 16336, Gateway 16357, Web 16378: stopped with SIGTERM after an ownership check (command line contains the lane workspace). No SIGKILL was needed. Five earlier PIDs from the first startup attempt had already exited |
| Mongo | Lane `mongod` 15795 stopped via `shutdownServer`. Its dbpath, holding every lane database (Auth, Platform, MDM, SupplyChain, Web, Hangfire) and all fixture data, was removed with the workspace. Operational `27017` was never used (netcheck ×2) |
| Ports | 5400, 5401, 5456, 5457, 5459, 5461, 5499 (sink) and 34994: no listener (all PASS) |
| Workspace `/private/tmp/mvp6-a12-rtver02.S0rbZc` | Removed and verified: source, head.tar, builds, env files (placeholders only), `secrets/` (placeholders + issuer/audience only), Mongo, gateway-runtime, logs, browser profiles, baseline copy |
| Repository | HEAD `4a8d4d4b…` and branch `feature/mvp6-logistics` unchanged. No git write command was run. New status entries: this folder, plus two CT records written by CT during the run (`mvp6-ct-terminal-lane-security-2026-09-26.md` 10:25, `-02-` 10:27). K11 marks the latter as `repo-status FAIL`; neither was created by this lane. No tracked file changed |
| Secrets in evidence | `SECRET-SCAN.txt` (pattern) PASS, `SECRET-SCAN-EXACT.txt` (exact in-memory values) PASS, `SECRET-RESCAN-AFTER-CLEANUP.txt` (pattern, after cleanup) — see file |

Not removed (by design): this evidence folder.
