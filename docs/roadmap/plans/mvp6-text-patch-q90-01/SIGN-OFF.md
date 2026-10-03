# Q92 — owner sign-off: Q90 text patches R1…R3

**STATUS: NOT DECIDED.** Queue item Q92 (DECISION-REQUIRED). This text grants nothing until the owner states it, or a named alternative, in their own words. Prepared 2026-09-26 (+03:00) by the Q90 chat lane; the patches are **not applied**.

**Record path to use (fixed):** `docs/records/decisions/2026-09/mvp6-text-patch-q90-signoff-owner-decision-01.md`. No patch adds an `Approved:` line; the record binds each patch by the hashes below.

Each decision is independent. Options: **A — approve applying the patch at the exact hashes (recommended)**; **B — defer** (the file stays at its preimage; the patch stays valid while the preimage is unchanged).

Preimages are the Q86 postimages, bound in `docs/records/decisions/2026-09/mvp6-text-patch-q83-signoff-owner-decision-01.md`: MOD-0190 `003aba70…9913`, MOD-0192 `f6b4d0f3…e445`. Q93 should apply only after the Q87 VER of those postimages has passed.

## Decision 1 — R1: MOD-0192 §23.4 IDs in the Details address (F-Q79-05 = A)

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/01-R1-MOD-0192-s23.4-ids-in-address.patch` | `13be62ce0bef2c717de8e1b2d8c3ab1ec342545147df181f743f718397e142d6` |
| Target (expected postimage, this patch alone) | same path | `ca52818ee1d2ddbf70582eb0026a4f0d5def656e0c72cc12b71566688f029280` |

Change (+11/−2 lines): §23.4 plan-workspace row gains the optional query `?scenarioId={uuid}&evaluationId={uuid}`; a new "Details address" paragraph (written with `history.replaceState`; UUID-validated; an invalid value is ignored with a localized message and no request; safe-not-found per §23.8; no new endpoint, field, permission, route segment or browser storage; manifest RoutePath unchanged); §23.11 rows CP-21 (reload reopens), CP-22 (shared link), CP-23 (invalid ID, no backend call); §23.13 "Session memory" marked resolved. Effort numbers unchanged.

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/01-R1-MOD-0192-s23.4-ids-in-address.patch` (SHA-256 `13be62ce0bef2c717de8e1b2d8c3ab1ec342545147df181f743f718397e142d6`) to `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`, only if the file's SHA-256 before it is `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` and after it is `ca52818ee1d2ddbf70582eb0026a4f0d5def656e0c72cc12b71566688f029280` (or, when Decision 3 is also applied, `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b` after both R1 and R3). This is a text correction only; it does not authorize code (the Capacity draft-overlay revision is Q88c), contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q93 may apply the patch; file becomes `ca52818e…` |
| B — Defer | File stays `f6b4d0f3…`; patch waits |

## Decision 2 — R2: MOD-0190 §24 stale DCP-009 note (Q89)

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/02-R2-MOD-0190-s24-stale-dcp-note.patch` | `5c65d9ed71e7783e79eb705a475a7bcdeb3a269fd330ccd70ecf6c0a9ed06859` |
| Target (expected postimage, this patch alone) | same path | `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa` |

Change (+1/−1 lines): open-gaps item 2 "DCP-009 §21.1 still lists MOD-0190 as excluded…" → "Closed: …" pointing to the applied DCP-009 text `ab728d67…` and the Q85 record `d27458e1…`.

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/02-R2-MOD-0190-s24-stale-dcp-note.patch` (SHA-256 `5c65d9ed71e7783e79eb705a475a7bcdeb3a269fd330ccd70ecf6c0a9ed06859`) to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, only if the file's SHA-256 before it is `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913` and after it is `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa`. This is a text correction only; it does not authorize code (the Capacity draft-overlay revision is Q88c), contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q93 may apply the patch; file becomes `ae1be969…` |
| B — Defer | File stays `003aba70…`; patch waits |

## Decision 3 — R3: MOD-0192 §24 stale DCP-009 note (Q89)

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/03-R3-MOD-0192-s24-stale-dcp-note.patch` | `5fe167125148e15545f3336f26eacf3548ef27beef180c6de63c7348ceadfb88` |
| Target (expected postimage, this patch alone) | same path | `6c927cd7352341da59b7d2c4e99d898e0e0fbbcf617653939eaee32da9f5498b` |

Change (+1/−1 lines): the same one-line correction for MOD-0192.

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q90-01/patches/03-R3-MOD-0192-s24-stale-dcp-note.patch` (SHA-256 `5fe167125148e15545f3336f26eacf3548ef27beef180c6de63c7348ceadfb88`) to `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`, only if the file's SHA-256 before it is `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` and after it is `6c927cd7352341da59b7d2c4e99d898e0e0fbbcf617653939eaee32da9f5498b` (or, when Decision 1 is also applied, `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b` after both R1 and R3). This is a text correction only; it does not authorize code (the Capacity draft-overlay revision is Q88c), contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q93 may apply the patch; file becomes `6c927cd7…` |
| B — Defer | File stays `f6b4d0f3…`; patch waits |

## Apply procedure for Q93 (only after the Q92 record exists and Q87 has passed)

1. `export GIT_OPTIONAL_LOCKS=0`; confirm the record exists at the fixed path and names the option per decision.
2. `sha256sum` each target = its preimage and each patch = its bound hash; stop on any mismatch (that file only).
3. `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"` and MOD-0192 (`Capacity Planning`); a non-zero exit stops.
4. Keep a /tmp copy of each preimage; `git apply --check <patch>`, then `git apply <patch>` (working tree only). R1 and R3 both touch MOD-0192, touch different lines and commute (checked both orders).
5. `sha256sum` each target = its postimage (MOD-0192 = `a1df34c1…` when both R1 and R3 are approved; `ca52818e…` R1 only; `6c927cd7…` R3 only; MOD-0190 = `ae1be969…`); on a mismatch restore the preimage bytes and stop.
6. Independent VER in a separate lane, started only after the Q93 writer hand-off line exists in MILESTONE-EVENTS.
