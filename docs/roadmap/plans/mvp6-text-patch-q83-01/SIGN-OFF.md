# Q85 — owner sign-off: Q83 text-correction patches P1…P5

**STATUS: NOT DECIDED.** Queue item Q85 (DECISION-REQUIRED). This text grants nothing until the owner states it, or a named alternative, in their own words. Prepared 2026-09-26 (+03:00) by the Q83 chat lane; the patches are **not applied**.

**Record path to use (fixed):** `docs/records/decisions/2026-09/mvp6-text-patch-q83-signoff-owner-decision-01.md`. No patch adds an `Approved:` line; the record binds each patch by the hashes below.

Each decision is independent. Options for every decision: **A — approve applying the patch at the exact hashes (recommended)**; **B — defer** (the file stays at its preimage; the patch stays valid while the preimage is unchanged).

## Decision 1 — P1: MOD-0190 §22 wording

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/01-P1-MOD-0190-s22-wording.patch` | `e475c74a4c51343dc94c0034abe1851c460f5b992f8e57a11a6ad65fcfa6fd8b` |
| Target (expected postimage, this patch alone) | same path | `44a377c2c9c903b8647a9fc479122ffcbd997f95eb514b804a2ec3bf8f639ac3` |

Change (+1/−1 lines): replaces "It is a proposal until the owner decision in `…/mod-0190-sop/PROMOTION-DECISION.md` is recorded." with "The owner decision prepared in `…/PROMOTION-DECISION.md` is recorded in the `Approved:` record above." (CT F-Q79-02).

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/01-P1-MOD-0190-s22-wording.patch` (SHA-256 `e475c74a4c51343dc94c0034abe1851c460f5b992f8e57a11a6ad65fcfa6fd8b`) to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, only if the file's SHA-256 before it is `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` and after it is `44a377c2c9c903b8647a9fc479122ffcbd997f95eb514b804a2ec3bf8f639ac3` (or, when Decision 4 is also applied, `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913` after both P1 and P4). This is a text correction only; it does not authorize code, contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q86 may apply the patch; file becomes `44a377c2…` |
| B — Defer | File stays `2bdd533f…`; patch waits |

## Decision 2 — P2: MOD-0192 §22 wording

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/02-P2-MOD-0192-s22-wording.patch` | `2f0d2d0f8d7ba893cc30a7f22a44094fdff8a16a6a74bfa05c960cd08747d4ec` |
| Target (expected postimage, this patch alone) | same path | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` |

Change (+1/−1 lines): the same one-sentence correction for `…/mod-0192-capacity/PROMOTION-DECISION.md` (CT F-Q79-02).

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/02-P2-MOD-0192-s22-wording.patch` (SHA-256 `2f0d2d0f8d7ba893cc30a7f22a44094fdff8a16a6a74bfa05c960cd08747d4ec`) to `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`, only if the file's SHA-256 before it is `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f` and after it is `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445`. This is a text correction only; it does not authorize code, contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q86 may apply the patch; file becomes `f6b4d0f3…` |
| B — Defer | File stays `7c3678bc…`; patch waits |

## Decision 3 — P3: DCP-009 §21.1 exclusion

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md` | `6b12ce685114248e89b78729546f43e86836d52f337599cb399de2993920046b` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/03-P3-DCP-009-s21.1-exclusion.patch` | `7bda0123d8190237b2ce69397b0b974dd904ee5516654f34cd89e78a7b4c34c0` |
| Target (expected postimage, this patch alone) | same path | `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf` |

Change (+2/−2 lines): adds `sop-workflow-signoffs` (MOD-0190) and `capacity-planning` (MOD-0192) to the ModuleCodes row and replaces the "Excluded" row with "None…", citing the Q80 record `mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md` (`37f3ff0d08f52391e2f8e605917874c41146b1eba91e928f4b65b2fb18f80bea`); `MANIFESTS.md` is not changed (CT F-Q79-03, F-Q83-1).

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/03-P3-DCP-009-s21.1-exclusion.patch` (SHA-256 `7bda0123d8190237b2ce69397b0b974dd904ee5516654f34cd89e78a7b4c34c0`) to `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md`, only if the file's SHA-256 before it is `6b12ce685114248e89b78729546f43e86836d52f337599cb399de2993920046b` and after it is `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf`. This is a text correction only; it does not authorize code, contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q86 may apply the patch; file becomes `ab728d67…` |
| B — Defer | File stays `6b12ce68…`; patch waits |

## Decision 4 — P4: MOD-0190 contract pin 2.0.0 → 3.0.0

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/04-P4-MOD-0190-contract-pin-3.0.0.patch` | `f0c4662d68cfc7df4616a9b2b3d637ffb80cace60071e986aaf283a64b1b2eb3` |
| Target (expected postimage, this patch alone) | same path | `0f0649112899a9f1fa4f58d523bcecf1bc2fd9df3cdbef6d3fd77359120b2766` |

Change (+8/−8 lines): re-pins §3 API row, §6 protected annex path, §7 dependency row, §16 acceptance line, §18 checklist line, §23.3 sentence and §23.13 F190-PIN bullet to SANDOP-CAPACITY 3.0.0 (YAML `5213b535…adab`, annex `f9d9d555…ee64`); §21/§22 acceptance evidence stays bound to 2.0.0; grounded in `COMPARE-0190-PIN.md` (verdict IDENTICAL) (CT F-Q79-04, F190-PIN).

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/04-P4-MOD-0190-contract-pin-3.0.0.patch` (SHA-256 `f0c4662d68cfc7df4616a9b2b3d637ffb80cace60071e986aaf283a64b1b2eb3`) to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, only if the file's SHA-256 before it is `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` and after it is `0f0649112899a9f1fa4f58d523bcecf1bc2fd9df3cdbef6d3fd77359120b2766` (or, when Decision 1 is also applied, `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913` after both P1 and P4). This is a text correction only; it does not authorize code, contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q86 may apply the patch; file becomes `0f064911…` |
| B — Defer | File stays `2bdd533f…`; patch waits |

## Decision 5 — P5: MOD-0187 §32 intro + §32.14 wording

| Item | Path | SHA-256 |
|---|---|---|
| Target (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | `8ed42fad66b7739b8c56778a57e0ead84cc8ce3678ea4868306263ac501a94a2` |
| Patch | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/05-P5-MOD-0187-s32.14-wording.patch` | `c82deca593d9cfd0b9410b19d87607a42ebc5dda0a6474742e51fb0fd8853bc5` |
| Target (expected postimage, this patch alone) | same path | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` |

Change (+10/−3 lines): replaces "UI code is **not** authorized by this section alone (§32.14)" and the "until an integrated target exists and a versioned UI dispatch is released" condition with the authorized UI work under the owner decisions PH15-UI-187 (`95a5c4f4…`), modules-first (`63e8601e…`) and draft overlays (`05033624…`); effort numbers unchanged (Q64a F2).

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/05-P5-MOD-0187-s32.14-wording.patch` (SHA-256 `c82deca593d9cfd0b9410b19d87607a42ebc5dda0a6474742e51fb0fd8853bc5`) to `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md`, only if the file's SHA-256 before it is `8ed42fad66b7739b8c56778a57e0ead84cc8ce3678ea4868306263ac501a94a2` and after it is `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2`. This is a text correction only; it does not authorize code, contract, gateway, permission, navigation, localization, registry or status-tracker changes, `done` status, commit, push or stash. One named writer applies it after rechecking every hash; any mismatch stops the application; an independent VER follows.

| Option | Effect |
|---|---|
| **A — Approve (recommended)** | Q86 may apply the patch; file becomes `96c9a0ae…` |
| B — Defer | File stays `8ed42fad…`; patch waits |

## Apply procedure for Q86 (only after the Q85 record exists)

1. `export GIT_OPTIONAL_LOCKS=0`; confirm the record exists at the fixed path and names the option per decision.
2. `sha256sum` each target = its preimage and each patch = its bound hash; stop on any mismatch (that file only).
3. `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"`, then the same for MOD-0190 (`S&OP Workflow & Sign-offs`) and MOD-0192 (`Capacity Planning`); a non-zero exit stops.
4. Keep a /tmp copy of each preimage; `git apply --check <patch>`, then `git apply <patch>` (working tree only; no `--index`, no commit/push/stash). P1 and P4 both touch MOD-0190, touch different lines and commute (checked both orders).
5. `sha256sum` each target = its postimage (MOD-0190 = `003aba70…9913` when both P1 and P4 are approved; `44a377c2…` P1 only; `0f064911…` P4 only); on a mismatch restore the preimage bytes and stop.
6. Independent VER in a separate lane, started only after the Q86 writer hand-off line exists in MILESTONE-EVENTS (CT rule from Q82 N2).
