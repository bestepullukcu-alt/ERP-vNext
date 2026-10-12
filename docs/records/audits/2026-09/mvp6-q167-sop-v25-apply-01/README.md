# Q167 — Apply SOP v2.5 r2 + add .antigravity/workflows/dispatch-wp.md (r2)

**Agent verdict: APPLIED at exact hashes.** Agent PASS ≠ CT ACCEPTED.

## 1. Metadata (SOP §17.1)

| Field | Value |
|---|---|
| Work Package ID / Prompt | Q167 — Apply SOP v2.5 r2 + add `.antigravity/workflows/dispatch-wp.md` (r2) |
| CT-QUEUE row | line 224: `Q167 · Apply SOP v2.5 r2 + dispatch-wp.md at exact hashes (OD-SIGN-v2.5) · READY · LANE 2 (documentation-writer; single SOP writer) · Q166 · 2026-09-28` |
| Agent Lane ID / Type | Cowork LANE 2 (Linux VM, repo via bridge) / DEV; placement gate `uname -s` = Linux |
| Target Agent / Entry Point | documentation-writer + @orchestrator; single writer of the SOP and of the new workflow file |
| Target Branch / Expected Base HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Authority | OD-R8 / OD-R9 / OD-R11 (`mvp6-ct-verdicts-q138-q139-owner-recs-2026-09-27.md`:44–47); OD-Q142 (`mvp6-ct-verdicts-q157-q142-q145-2026-09-28.md`:39); Q163 PASS; **OD-SIGN-v2.5** (`mvp6-ct-verdicts-q161-q164-sop-v25-signoff-2026-09-28.md` sha256 `aa6af3ba5478825e9ddb7547b589251c56165db2c346730776f9d504179b9722`, line 47) |
| Base Stack | n/a (no build) |
| Allowed Paths | `docs/guides/operations/control-tower-sop.md`; `.antigravity/workflows/dispatch-wp.md` (new); this folder (new); /tmp scratch |
| Ledgers (read, not written) | CT-QUEUE `523441de564b4153cf2226efe511d8342b1af979f72739bb1a6ae82f5b6e4db7` · MILESTONE-EVENTS `de07bba81c51020e9c8d4c0d1fad1f2370e2cf770d06230749c31eab0e9cd79b` (same before and after) |

## 2. Preflight (2026-09-28T08:25:50Z) — all gates PASS

| Gate | Result |
|---|---|
| `uname -s` | Linux |
| Q167 row in CT-QUEUE | present, line 224 |
| `.git/index.lock` | absent (also at the end) |
| git | `GIT_OPTIONAL_LOCKS=0`; only `git status --porcelain` (no `git diff`): 29 ` M` + 506 `??` = known baseline (29 M + 2 UC-01 untracked + record folders); `docs/guides/operations/control-tower-sop.md` clean; `.antigravity/rules/docs-organization.md` ` M` is part of the known 29 and was not touched |
| Live SOP before | `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36` (2,073 lines) = OD-SIGN "before" |
| `.antigravity/workflows/dispatch-wp.md` | did not exist |
| Q162 folder | `SHA256SUMS` = `00c03274acf6f8a7c2533b89d7e6d7be6a5218692c68c025e12df59f1d07bfa0`, `sha256sum -c` 4/4 OK; `SOP-v2.5-r2.patch` = `3e8a0dba32dfe620500229b6494106443a19629032f4a558d579120239aea5f6`; `dispatch-wp.md` = `af0a080e85664dff29cb52206beccd8acd9971988f8b95b85f972b9a3cea3e50` |

## 3. Result

| Item | Before | After |
|---|---|---|
| `docs/guides/operations/control-tower-sop.md` | `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36` (v2.4, 2,073 lines) | **`c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032`** (v2.5, 2,327 lines) = OD-SIGN "after" |
| `.antigravity/workflows/dispatch-wp.md` | absent | **`af0a080e85664dff29cb52206beccd8acd9971988f8b95b85f972b9a3cea3e50`** (4,505 bytes; `cmp` byte-identical to the Q162 file) |
| Reverse check | — | `patch -R --dry-run` on a /tmp copy of the new SOP: exit 0; a real reverse on that copy → `e85854bd…` |
| `.orig` / `.rej` | — | none (`--no-backup-if-mismatch`) |
| Q141 r1 patch | — | **not applied** |
| Restore on mismatch | — | not needed (post hash matched) |

`git status --porcelain` after (vs preflight): exactly 2 new entries — ` M docs/guides/operations/control-tower-sop.md` and
`?? .antigravity/workflows/dispatch-wp.md` — plus this folder. Everything else is unchanged. No ledger write, no rm, no git write, no `git diff`.

## 4. Command output (UTC)

```text
## step 0 preimage copy 2026-09-28T08:26:52Z
e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36  /tmp/q167-l2-vpFC/sop-preimage.md
## step 1a dry-run 2026-09-28T08:26:52Z
$ patch --dry-run -p1 < docs/records/audits/2026-09/mvp6-q162-sop-v25-r2-01/SOP-v2.5-r2.patch
checking file docs/guides/operations/control-tower-sop.md
exit 0
## step 1b apply 2026-09-28T08:26:59Z
$ patch -p1 --no-backup-if-mismatch < docs/records/audits/2026-09/mvp6-q162-sop-v25-r2-01/SOP-v2.5-r2.patch
patching file docs/guides/operations/control-tower-sop.md
exit 0
post sha256 c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032
2327
## .orig/.rej check
none
## step 2 copy dispatch-wp.md 2026-09-28T08:27:06Z
$ cp docs/records/audits/2026-09/mvp6-q162-sop-v25-r2-01/dispatch-wp.md .antigravity/workflows/dispatch-wp.md
exit 0
af0a080e85664dff29cb52206beccd8acd9971988f8b95b85f972b9a3cea3e50  .antigravity/workflows/dispatch-wp.md
cmp: byte-identical
## step 3 reverse check 2026-09-28T08:27:06Z
$ patch -R --dry-run -p1 -d <tmp copy> < docs/records/audits/2026-09/mvp6-q162-sop-v25-r2-01/SOP-v2.5-r2.patch
checking file docs/guides/operations/control-tower-sop.md
exit 0
real reverse on the tmp copy → e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36
```

## 5. Deviations

None. Scratch (preimage copy, reverse-check copy, log) is in `/tmp/q167-l2-*` on the Cowork VM only.

Writer hand-off — Q167
