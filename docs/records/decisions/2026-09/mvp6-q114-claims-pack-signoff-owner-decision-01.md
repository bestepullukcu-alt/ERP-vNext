# Owner decision — Q114 MOD-0187 pack text patch (P1 layout rule, P2 carriers.read prerequisite) — 2026-09-27

Recorded by the Q126 chat lane (WP-MVP6-PACK-126, single pack writer) at 2026-09-27T14:13+03:00 on CT instruction (CT writes no files).
Decisions given by the owner through the question tool, 2026-09-27 ~14:00 +03:00 (CT conversation), on the options in
`docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/SIGN-OFF.md` (SHA-256 `2eac338c9a4659c2050b5d61dc0ccdcad090a24bdbd5599f8c4ee134613bd13a`; package `SHA256SUMS` `2d09982d36cd4abe7e241e11d6b581935a7d91b56272444d2b8551240cf049c0`, 3/3 OK at recording).

This is the fixed record path named in SIGN-OFF.md.

| Decision | Option | Scope |
|---|---|---|
| 1 — P1: §32.2 layout rule (page views only; partials set no `Layout`) | **A — approve** | pack text, §32.2 first bullet |
| 2 — P2: `supplychain.carriers.read` role prerequisite (F-CU10), §32.4 + §32.11 CU-09…CU-13 row | **B — defer** | not applied |

## Bound hashes

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` |
| Combined patch P1 + P2 (not applied) | `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/01-MOD-0187-layout-and-carriers-read.patch` | `9708198c82fdb415e63a27217b576bd87b6b04c3a509c4f9237a14b85e4cd476` |
| P1-only patch (cut from the combined patch; §32.2 hunk only, +2/−1) | `docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/02-MOD-0187-P1-only.patch` | `8e7ac4ac2944d28ffe1052ec147399fcb6f9bbdde2c33943beb871aa3393ee10` |
| Target (postimage, P1 alone — the reference postimage in SIGN-OFF.md) | same target path | `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` |

Approved text (Decision 1, option A): as written in SIGN-OFF.md under Decision 1 "Exact text (option A)" — the P1 part applied alone through a re-cut patch, only if the target's SHA-256 before it is `96c9a0ae…71ae2` and after it is `3d1a00e2…4cae`. The combined patch file is not applied (SIGN-OFF.md: applied only when both decisions are A).

## Deferred (Decision 2, option B)

P2 — the `supplychain.carriers.read` prerequisite for a carrier-linked create (F-CU10) — is deferred and **not** in the pack. The pack keeps its §32.4 and §32.11 text for it. CU-10 remains "PASS with lane grant, prerequisite undocumented". The combined patch and the P2-alone reference postimage `b49c81bb646d675b43c25d11ef64e3fabaf2018dbf082f94e1149e304bb4fd42` stay on file; after P1 is applied, that reference no longer applies to the current pack.

## Scope

Text only (pack §32.2 wording). **Not approved:** UI draft or code, contract, gateway, permission, role-seed, navigation, localization, registry or status changes, `done` status, commit, push or stash. One named writer applies (Q126); an independent VER follows (Q128, LANE 4), started only after the Q126 writer hand-off.
