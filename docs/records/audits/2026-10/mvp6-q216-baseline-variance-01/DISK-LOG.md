# Q216 — Disk log

Volume `/` (`/dev/disk3s3s1`, 460 GiB). Free space from `shutil.disk_usage("/")` in the driver (per run) and
`df -k /` (before / after). STOP threshold: 2 GiB. **Never approached.**

| Point | Time (+03) | Free |
|---|---|---:|
| Dispatch figure in the prompt | — | 9.1 GiB |
| **Before** — preflight, before the build | 21:00 | 8.2 GiB |
| After the build, before run 1 | 21:01 | 8.19 GiB |
| After run 1 | 21:05 | 8.22 GiB |
| After run 2 | 21:09 | 8.10 GiB |
| After run 3 | 21:13 | 8.11 GiB |
| After run 4 | 21:18 | 8.11 GiB |
| **Midway** — after run 5 | 21:22 | **8.00 GiB** |
| After run 6 | 21:26 | 8.10 GiB |
| After run 7 | 21:31 | 7.93 GiB |
| After run 8 | 21:35 | 7.86 GiB |
| After run 9 | 21:39 | 8.01 GiB |
| **After** — run 10 | 21:43 | **8.02 GiB** |
| After writing this record | — | 8.00 GiB |

- This WP's own footprint: **20 MB** in `/private/tmp/q216-baseline-variance-01/` (ten `.trx` files of about 2 MB,
  logs, scripts). No `TestResults` folder was written into the repo: every run used `--results-directory` in the
  scratch folder.
- The lane mongod's data (`/private/tmp/q208-suite-baseline-01/db`) was 18 MB before the sequence. The tests use fixed
  database names and a new tenant per test, so data grows a little with every run and is never dropped.
- The free figure moves by ±0.15 GiB between runs in both directions. That is other activity on the machine, not
  this WP.
- 0.9 GiB less was free at preflight than the dispatch figure. Not caused by this WP; not investigated.
