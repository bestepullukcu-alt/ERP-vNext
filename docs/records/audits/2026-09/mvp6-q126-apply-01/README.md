# Q126 — apply of Q114 P1 (MOD-0187 §32.2 layout rule) at exact hash

WP-MVP6-PACK-126 · Prompt Q126 v1 · DEV lane (single pack writer) · chat lane on the Linux VM bridge · agent `module-pack-author`.
Start 2026-09-27 14:11:44 +03 · apply 14:13:19 +03 · end 2026-09-27T14:14+03:00 (Istanbul). Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.

## Owner decision

`docs/records/decisions/2026-09/mvp6-q114-claims-pack-signoff-owner-decision-01.md` — SHA-256 `72aeee378c69ae07bbc120218e7e366e4187a1ddeab708a294ab9d837e5c47ff` (new).
P1 = A (approve), P2 = B (defer), owner via question tool ~14:00 +03:00. P2 (`supplychain.carriers.read`, F-CU10) is not in the pack.

## Hashes

| Item | Path | SHA-256 |
|---|---|---|
| Pack preimage | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` |
| Combined patch (not applied) | `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/01-MOD-0187-layout-and-carriers-read.patch` | `9708198c82fdb415e63a27217b576bd87b6b04c3a509c4f9237a14b85e4cd476` |
| P1-only patch (new; header + §32.2 hunk `@@ -559,7 +559,8 @@` of the combined patch, 0 P2 lines) | `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/02-MOD-0187-P1-only.patch` | `8e7ac4ac2944d28ffe1052ec147399fcb6f9bbdde2c33943beb871aa3393ee10` |
| Pack postimage (applied) = SIGN-OFF reference "P1 alone" | same pack path | `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` |
| Q114 SIGN-OFF.md / SHA256SUMS (unchanged, 3/3 OK) | `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/` | `2eac338c9a4659c2050b5d61dc0ccdcad090a24bdbd5599f8c4ee134613bd13a` / `2d09982d36cd4abe7e241e11d6b581935a7d91b56272444d2b8551240cf049c0` |

## Steps and checks

1. Preflight: `GIT_OPTIONAL_LOCKS=0`; 19 diff paths; no `.git/index.lock`; pack = preimage.
2. P1-only cut in `/tmp/q126`: `git apply --check` on a /tmp copy of the preimage (`GIT_CEILING_DIRECTORIES=/tmp`) exit 0 → `3d1a00e2…4cae` exactly. Reverse → `96c9a0ae…`. `--numstat` 2/1.
3. Before the apply: pack = `96c9a0ae…`; patch = `8e7ac4ac…`; /tmp preimage copy byte-identical; `verify_module_id.py . --check-id MOD-0187 --name "Claims Management"` → `OK`, exit 0.
4. `git apply` (working tree only, no `--index`) → exit 0; pack = `3d1a00e2…4cae` (match). `verify_module_id.py` after → exit 0. Change: +2/−1 (§32.2 first bullet only).
5. After: 0 `.orig`/`.rej`; no untracked file in `module-packs/` or `delivery-capability-packs/`; mode 100644 unchanged; the other 25 of 26 pack/DCP files unchanged; `git diff --name-only HEAD` = the same 19 paths (MOD-0187 was already in the set); no `.git/index.lock`; HEAD unchanged.

## ASSUMPTIONs / observations

- **A1:** The new P1-only patch is not added to the Q114 folder's `SHA256SUMS` (existing files there are not edited). It is bound by hash in the decision record and here.
- **O1:** `git apply` printed "unable to unlink … Operation not permitted" (the VM bridge blocks deletes). It then wrote the file in place. The resulting bytes equal the postimage, and no stray file remains.

## Not done (by rule)

No ledger edit (LANE 1), no rm, no git add/commit/push/stash, no other file changed.

Writer hand-off — Q126: P1 applied at exact hash (MOD-0187 96c9a0ae → 3d1a00e2, +2/−1); P2 deferred; record 72aeee37; P1-only patch 8e7ac4ac; 19 paths unchanged; no .orig/.rej; uncommitted; Q128 independent VER (LANE 4) may start.
