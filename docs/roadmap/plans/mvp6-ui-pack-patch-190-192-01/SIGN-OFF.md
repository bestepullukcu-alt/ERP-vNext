# Q80 — owner sign-off: MOD-0190 S&OP and MOD-0192 Capacity UI pack revisions + Phase 1.5 tables

**STATUS: NOT DECIDED.** Queue item Q80 (DECISION-REQUIRED). This text grants nothing until the owner states it, or a named
alternative, in their own words. An agent quoting it is not approval. Prepared 2026-09-26 (+03:00) by the Q79 chat lane; drafts are
**not applied**.

Scope already decided: `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-scope-owner-decision-01.md` (SHA-256
`1814672b501cda4aa3a65e40b41930c013fcea77545a43e63ffb10b237be1553`) — option A for both modules, scope only. That record says the
Phase 1.5 tables, the pack text change and UI code go to Q80. This sign-off covers the first two; UI code stays outside it.

**Record path to use (fixed):** `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`. Both patches
already carry `Approved:` lines that point to exactly this path (§23 and §24 of each pack). A record at any other path makes those
lines wrong; the patches would then have to be regenerated and re-hashed before Q81.

## Bound inputs

| Item | Path | SHA-256 |
|---|---|---|
| MOD-0190 pack (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `56fb8e7d19daa36370747f391ac92c05eca1b51c84d643fbdf7ace687b82ac41` |
| MOD-0190 patch | `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/01-MOD-0190-ui-revision.patch` | `eea740986b0058b06a43bb198b9a446f9a1cc7dde41f445661b7bd8f49db8225` |
| MOD-0190 pack (expected postimage) | same path | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` |
| PH15-UI-190 table | `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-190.md` | `fb6ad9d8f63381822e0287a0251fb69ddfe7354ff753442c0198a4a8a9945cf8` |
| MOD-0192 pack (preimage) | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `9b8b90f1def56be975d63c456a6fb61ba574fec8e2060457d53c72c839fdf813` |
| MOD-0192 patch | `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/02-MOD-0192-ui-revision.patch` | `a9199cf1a9d7f6d2960171fd1c300fa7b523fcc134d40ae1861356bd83306bfb` |
| MOD-0192 pack (expected postimage) | same path | `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f` |
| PH15-UI-192 table | `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-192.md` | `5dee65d1d07b81974de600991ae8ed110ae2c0a1a6edf2eb093cc2dd55af4116` |

`git apply --check` passed for both patches against byte copies of the preimages in `/tmp` (see `README.md`); applying them in
`/tmp` produced exactly the postimage hashes above, and each reverse check passed. The two patches touch different files and
are independent.

## Decision 1 — MOD-0190 S&OP

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/01-MOD-0190-ui-revision.patch` (SHA-256 `eea740986b0058b06a43bb198b9a446f9a1cc7dde41f445661b7bd8f49db8225`) to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, only if the pack's SHA-256 before it is `56fb8e7d19daa36370747f391ac92c05eca1b51c84d643fbdf7ace687b82ac41` and after it is `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f`. The patch sets `shell: tenant`, `golden_reference: slim` and `form_field_count: 5`, keeps `status: ready-for-dev`, and adds §23 (tenant UI scope, GoldenReferenceSlim: entry page with "Create plan" and "Open plan by ID" and no plan list, plan workspace with snapshots and sign-offs tables, "Capture snapshot" and "Record sign-off", the six published operations and four existing permission keys only, 32 owned UI paths, acceptance rows SU-VS1…SU-20 and OUT rows SU-SCR-01…09, effort 52/86/148 h) and §24 (self-registration `sop-workflow-signoffs`, pages `SANDOP_PLANS` / `SANDOP_PLAN_DETAILS`, SR-D4 ship rule). I also approve the Phase 1.5 table `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-190.md` (SHA-256 `fb6ad9d8f63381822e0287a0251fb69ddfe7354ff753442c0198a4a8a9945cf8`) with F190-LIST, F190-DEMAND, F190-PIN and F190-ENT kept as open findings. One named writer applies the patch after rechecking every hash; any mismatch stops the application and nothing is partially applied; an independent VER follows. This decision does **not** authorize: UI code beyond my separate decisions of 2026-09-26 ("modules first", draft overlays) and a versioned UI dispatch; any gateway, permission, navigation, localization, icon-map, DCP, `Program.cs` or other shared edit except by the single CT-appointed integration owner; a contract or backend change (including a plan list operation); the contract re-pin; `done` status; a registry or status-tracker update; rollout, E5/G5; commit, push or stash.

| Option | Effect |
|---|---|
| **A — Approve the patch and PH15-UI-190 (recommended)** | Q81 may apply the patch at the exact hashes; pack becomes `2bdd533f…017f`; independent VER follows |
| B — Approve PH15-UI-190 only | The pack stays `56fb8e7d…ac41`; the patch waits (valid while the preimage is unchanged) |
| C — Defer both | Nothing changes; the patch stays a draft |

## Decision 2 — MOD-0192 Capacity

> **Exact text (option A):** I approve applying `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/02-MOD-0192-ui-revision.patch` (SHA-256 `a9199cf1a9d7f6d2960171fd1c300fa7b523fcc134d40ae1861356bd83306bfb`) to `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`, only if the pack's SHA-256 before it is `9b8b90f1def56be975d63c456a6fb61ba574fec8e2060457d53c72c839fdf813` and after it is `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f`. The patch sets `shell: tenant`, `golden_reference: slim` and `form_field_count: 7`, keeps `status: ready-for-dev`, and adds §23 (tenant UI scope, GoldenReferenceSlim: entry page with "Create plan" and "Open plan by ID" and no lists, plan workspace with "Create scenario", "Open scenario by ID", "Evaluate" and an evaluation panel with the bottleneck table and a manual Refresh, decimal values as strings, the six published SANDOP-CAPACITY 3.0.0 operations and four existing permission keys only, 32 owned UI paths, acceptance rows CP-VS1…CP-20 and OUT rows CP-SCR-01…10, effort 58/98/168 h) and §24 (self-registration `capacity-planning`, pages `CAPACITY_PLANS` / `CAPACITY_PLAN_DETAILS`, SR-D4 ship rule). I also approve the Phase 1.5 table `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-192.md` (SHA-256 `5dee65d1d07b81974de600991ae8ed110ae2c0a1a6edf2eb093cc2dd55af4116`) with F192-LIST, F192-DEMAND and F192-POLL kept as open findings. One named writer applies the patch after rechecking every hash; any mismatch stops the application and nothing is partially applied; an independent VER follows. This decision does **not** authorize: UI code beyond my separate decisions of 2026-09-26 ("modules first", draft overlays) and a versioned UI dispatch; automatic polling; any gateway, permission, navigation, localization, icon-map, DCP, `Program.cs` or other shared edit except by the single CT-appointed integration owner; a contract or backend change (including list operations); `done` status; a registry or status-tracker update; rollout, E5/G5; commit, push or stash.

| Option | Effect |
|---|---|
| **A — Approve the patch and PH15-UI-192 (recommended)** | Q81 may apply the patch at the exact hashes; pack becomes `7c3678bc…171f`; independent VER follows |
| B — Approve PH15-UI-192 only | The pack stays `9b8b90f1…f813`; the patch waits (valid while the preimage is unchanged) |
| C — Defer both | Nothing changes; the patch stays a draft |

## Apply procedure for Q81 (only after the Q80 record exists)

1. `export GIT_OPTIONAL_LOCKS=0`; confirm the Q80 record exists at the fixed path above and names the chosen option per module.
2. `sha256sum` each target pack = its preimage; `sha256sum` each patch = its bound hash; stop on any mismatch.
3. `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"` and the MOD-0192 equivalent; non-zero exit stops.
4. `git apply --check <patch>`, then `git apply <patch>` (working tree only; no `--index`, no commit/push/stash).
5. `sha256sum` each pack = its postimage; on mismatch restore the preimage bytes and stop.
6. Independent VER in a separate lane (patch-only diff, hashes, no other file changed).
