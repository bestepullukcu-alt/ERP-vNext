# Q205 — Hash verification

All hashes measured on the Mac with `shasum -a 256`, 2026-10-02 (Europe/Istanbul).

## 1. BASE-STACK v2

Folder: `docs/records/audits/2026-09/mvp6-base-stack-v2/`

| File | sha256 (measured) | `SHA256SUMS` | Result |
|---|---|---|---|
| `BASE-STACK-v2.md` | `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` | same | OK |
| `LAYERS.tsv` | `9ddadd01b577aef0a0d3a089100260848d9ce10e98aee7486eb07607cde2ed43` | same | OK |

`shasum -a 256 -c SHA256SUMS` → 2/2 OK.

## 2. Q131 archive

Folder: `docs/records/audits/2026-09/mvp6-q131a-port-uri-overlay-01/`

`shasum -a 256 -c SHA256SUMS` → 5/5 OK (`overlay-q131a.tar`, `OVERLAY-MANIFEST.tsv`, `CANDIDATE-REVIEW.tsv`,
`ENV-VARS.md`, `FILE-PLAN.tsv`).

| Item | Expected | Measured | Result |
|---|---|---|---|
| `overlay-q131a.tar` | `4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf` (prompt; BASE-STACK v2 §1 row 3) | `4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf` | OK |

## 3. The one extracted member

Only `scripts/test-env/mvp6-test-mongo-env.sh` was extracted, into `/private/tmp/q205-testenv-01/`
(`tar -xf … -C /private/tmp/q205-testenv-01 scripts/test-env/mvp6-test-mongo-env.sh`). Nothing was extracted into the repo.

| Item | Expected (`OVERLAY-MANIFEST.tsv`, `postimage_sha256`) | Measured | Result |
|---|---|---|---|
| member sha256 | `6c936b4a84bb89a27115190c2ea0c87488e690e8ef7dcf1e383ffaeff54d13b1` | `6c936b4a84bb89a27115190c2ea0c87488e690e8ef7dcf1e383ffaeff54d13b1` | OK |
| mode | 0755 | `-rwxr-xr-x` (in archive and after extraction) | OK |
| size | — | 2,569 bytes | — |
| manifest `preimage_layer` | — | `NEW (absent in BASE-STACK v1)` | — |

The hash was verified **before** the script was run.
