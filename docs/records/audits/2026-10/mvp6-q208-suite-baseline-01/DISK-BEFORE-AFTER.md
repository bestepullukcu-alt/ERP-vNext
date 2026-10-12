# Q208 — Disk before / after

Volume `/dev/disk3s1` (460 GiB), measured with `df` on `~`.

| Moment (+03) | Free |
|---|---:|
| Dispatch (prompt) | 13 GiB |
| Start 19:39:19 | 14 GiB |
| After SupplyChain build | 14 GiB |
| After the full suite run 19:44:33 | 10 GiB |
| After the three Platform test builds | 10.1 GiB |
| End (after the Loads reruns) 19:47:58 | 9.1 GiB |

The 2 GiB stop line was never approached.

## What this WP itself used

| Item | Size |
|---|---:|
| Scratch `/private/tmp/q208-suite-baseline-01` (dbpath 27 MB + logs + TRX) | 27–35 MB |
| `services/Diten.SupplyChainService` in total, incl. all `bin/` `obj/` | 99 MB |
| `services/Diten.Platform` in total, incl. all `bin/` `obj/` | 395 MB |
| This record folder | < 10 MB |

**The drop of about 5 GiB is not explained by this WP** (its whole footprint is under 0.6 GiB, most of which
existed before). What consumed the rest was not investigated; Q198 saw the same kind of unexplained drop
(its REPORT, "Disk"). Worth a look by the owner before the next large build.
