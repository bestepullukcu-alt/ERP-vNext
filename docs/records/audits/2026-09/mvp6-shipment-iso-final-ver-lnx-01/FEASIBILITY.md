# Step 0 — feasibility result: **FAIL (NOT RUNNABLE ON LINUX CHAT LANE)**

| Check | Required | Observed | Result |
|---|---|---|---|
| a. OS/arch | Linux VM | Ubuntu 22.04.5 LTS, aarch64, 4 CPU, 3.8 GiB RAM | recorded |
| a. Free disk in `/tmp` | ≥ 6 GB | 4.2 GB | **FAIL** |
| a. Branch/HEAD | `4a8d4d4b…` | `4a8d4d4b…` / `feature/mvp6-logistics` | PASS |
| b. .NET SDK 8.0.417 (linux-arm64) | official dotnet-install script or Microsoft feed | `dot.net`, `builds.dotnet.microsoft.com`, `dotnetcli.azureedge.net` all refused by the egress proxy (HTTP 403 on CONNECT) | **FAIL** |
| c. MongoDB 8.x server (aarch64) | official tarball/package | `fastdl.mongodb.org` refused (403) | **FAIL** |
| d. Playwright + Chromium | pip/npm package + browser download | package indexes reachable (pypi 200, npm 200), browser hosts `cdn.playwright.dev` and `playwright.azureedge.net` refused (403) → no Chromium | **FAIL** |

Nothing was installed or downloaded; `/tmp` was not used. Per prompt §0e no workaround was attempted (no mirrors, unofficial binaries, alternative proxies or tunnels).

What worked: shell access, repository read/write through the bridge, HEAD/branch check, curl/python3/node/npm/pip present, PyPI and npm registries reachable.

Additional constraint even if the network were opened: RAM 3.8 GiB for Mongo + Auth + Platform + MDM + SupplyChain + Gateway + Web + headless Chromium is tight and may itself be a blocker.

## Recommendation (for CT/owner, not decided here)

Either (1) run Q61 from a local Mac session as the environment rule already requires, or (2) have the account's egress allowlist extended to the official hosts (`dot.net`/`builds.dotnet.microsoft.com`, `fastdl.mongodb.org`, `cdn.playwright.dev`/`playwright.azureedge.net`) and give the VM ≥ 6 GB free in `/tmp`, then re-dispatch; any Linux result would still be PROVISIONAL.
