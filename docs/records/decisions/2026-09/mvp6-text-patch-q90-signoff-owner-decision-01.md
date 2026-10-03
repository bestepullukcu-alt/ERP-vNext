# Owner decision — Q90 text patches R1…R3 (Q92) — 2026-09-26

Recorded by the Q93 v2 lane (Claude Code on the Mac; single ledger and pack writer) at 2026-09-26T21:15+03:00 on CT instruction (CT writes no files).
Decisions given by the owner through the question tool, 2026-09-26 ~20:20 +03:00 (CT conversation), on the options in
`docs/roadmap/plans/mvp6-text-patch-q90-01/SIGN-OFF.md` (SHA-256 `e54d976635952f90d80d3fad89cc8722e4fd869ef592e5058627483a8a30f377`; package `SHA256SUMS` `47ef82cc33ba75a712d7c895dbb7570c239cc67bc297b89f5e4f2c44e4f00bbe`, 5/5 OK at 21:14).

This is the fixed record path named in SIGN-OFF.md.

| Decision | Patch | Option | Target | Preimage SHA-256 | Patch SHA-256 | Postimage SHA-256 (patch alone) |
|---|---|---|---|---|---|---|
| 1 — R1: MOD-0192 §23.4 IDs in the Details address | `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/01-R1-MOD-0192-s23.4-ids-in-address.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` | `13be62ce0bef2c717de8e1b2d8c3ab1ec342545147df181f743f718397e142d6` | `ca52818ee1d2ddbf70582eb0026a4f0d5def656e0c72cc12b71566688f029280` |
| 2 — R2: MOD-0190 §24 stale DCP-009 note | `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/02-R2-MOD-0190-s24-stale-dcp-note.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913` | `5c65d9ed71e7783e79eb705a475a7bcdeb3a269fd330ccd70ecf6c0a9ed06859` | `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa` |
| 3 — R3: MOD-0192 §24 stale DCP-009 note | `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/03-R3-MOD-0192-s24-stale-dcp-note.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` | `5fe167125148e15545f3336f26eacf3548ef27beef180c6de63c7348ceadfb88` | `6c927cd7352341da59b7d2c4e99d898e0e0fbbcf617653939eaee32da9f5498b` |

**Combined MOD-0192 postimage after R1 and R3 (either order):** `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b`.

Approved text (option A, per decision): as written in SIGN-OFF.md under "Exact text (option A)" for Decisions 1–3 — apply the named patch only if the target's SHA-256 before it is the preimage above and after it is the postimage above (MOD-0192: the combined postimage, since both R1 and R3 are approved).

## Scope

Text only (pack wording). **Not approved:** code (the Capacity draft-overlay revision is Q88c), contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies (Q93 v2); an independent VER follows (Q95), started only after the Q93 hand-off line exists in MILESTONE-EVENTS.
