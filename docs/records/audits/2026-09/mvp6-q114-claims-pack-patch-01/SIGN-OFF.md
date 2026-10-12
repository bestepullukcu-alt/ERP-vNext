# Q114 — owner sign-off: MOD-0187 pack text patch (P1 layout rule, P2 carriers.read prerequisite)

**STATUS: NOT DECIDED.** This text grants nothing until the owner states it, or a named alternative, in their own words. Prepared 2026-09-27 (+03:00) by the Q114 chat lane (WP-MVP6-PACK-114); the patch is **not applied**.

**Record path to use (fixed):** `docs/records/decisions/2026-09/mvp6-q114-claims-pack-signoff-owner-decision-01.md`. The patch adds no `Approved:` line; the record binds it by the hashes below.

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` |
| Patch (P1 + P2, one file) | `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/01-MOD-0187-layout-and-carriers-read.patch` | `9708198c82fdb415e63a27217b576bd87b6b04c3a509c4f9237a14b85e4cd476` |
| Target (postimage, P1 + P2) | same path | `b2fba5f34495a38b59286ce1749e3af77b5a2ecd512221c416269eded1ba7d7c` |
| Reference only: postimage if P1 alone | same path | `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` |
| Reference only: postimage if P2 alone | same path | `b49c81bb646d675b43c25d11ef64e3fabaf2018dbf082f94e1149e304bb4fd42` |

The patch file carries both parts. It is applied only when **both** decisions are A. If one decision is B, this file is not applied; a new single-part patch is cut against the same preimage, and its result must equal the reference postimage above.

Each option: **A — approve applying at the exact hashes (recommended)**; **B — defer** (the pack stays at its preimage; the patch stays valid while the preimage is unchanged).

## Decision 1 — P1: §32.2 layout rule (page views only)

Change (+2/−1 lines, §32.2 first bullet): "every Claims `.cshtml` states `Layout = "_LayoutTenantShell";`" becomes "the Claims page view (`Index.cshtml`) states `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02)". Source: Q64b D-02 / F-PACK-32.2; Q64d confirms one shell after the fix.

> **Exact text (option A):** I approve the P1 part of `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/01-MOD-0187-layout-and-carriers-read.patch` (SHA-256 `9708198c82fdb415e63a27217b576bd87b6b04c3a509c4f9237a14b85e4cd476`) for `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md`, only if the file's SHA-256 before it is `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` and after it is `b2fba5f34495a38b59286ce1749e3af77b5a2ecd512221c416269eded1ba7d7c` together with Decision 2, or `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` alone through a re-cut patch. This is a pack text correction only; it does not authorize code, UI draft, contract, gateway, permission, role-seed, navigation, localization, registry or status changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | §32.2 scopes the explicit layout to page views; partials set none |
| B — Defer | §32.2 keeps "every Claims `.cshtml`" (the wording that produced D-02) |

## Decision 2 — P2: `supplychain.carriers.read` role prerequisite (F-CU10)

Change (+4/−1 lines):
- §32.4 gains 3 lines after the G-SHIPREAD sentence: a create that links a carrier (`carrierId` sent) also needs `supplychain.carriers.read` on the actor, because the backend forwards the user's token to the carrier list, and a 403 there returns 503 `CLAIM_REFERENCE_UNAVAILABLE` with zero write. Transitions do not need it. It is a role prerequisite only: no new key, no code change, and the UI still makes no carrier lookup (§32.3).
- The §32.11 row CU-09…CU-13 gains "carrier link (CU-10 precondition: the actor also holds `supplychain.carriers.read`, §32.4)".

Source: Q64b F-CU10, reproduced in Q64d (503 without the grant, 201 with it). The code path is cited in README.md.

> **Exact text (option A):** I approve the P2 part of `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/01-MOD-0187-layout-and-carriers-read.patch` (SHA-256 `9708198c82fdb415e63a27217b576bd87b6b04c3a509c4f9237a14b85e4cd476`) for `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md`, only if the file's SHA-256 before it is `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` and after it is `b2fba5f34495a38b59286ce1749e3af77b5a2ecd512221c416269eded1ba7d7c` together with Decision 1, or `b49c81bb646d675b43c25d11ef64e3fabaf2018dbf082f94e1149e304bb4fd42` alone through a re-cut patch. This records `supplychain.carriers.read` as a role prerequisite for carrier-linked creates only; it creates no permission key and authorizes no code, UI draft, contract, gateway, role-seed, navigation, localization, registry or status change, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | §32.4 and CU-10 state the carriers.read prerequisite for carrier-linked creates |
| B — Defer | The prerequisite stays unrecorded; CU-10 positive branch needs a lane-only grant (as Q97/Q64d) |

## Apply procedure (after the record exists)

1. `export GIT_OPTIONAL_LOCKS=0`; confirm the record at the fixed path names an option per decision.
2. `sha256sum` target = `96c9a0ae…71ae2` and patch = `9708198c…d476`; stop on mismatch.
3. `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"`; non-zero exit stops.
4. Both A: `git apply --check`, then `git apply` (working tree only). One A: do not apply this file; cut the single-part patch and recheck against the reference postimage.
5. `sha256sum` target = the postimage; on mismatch restore the preimage bytes and stop. Independent VER follows the writer hand-off line.
