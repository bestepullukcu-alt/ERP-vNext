# Owner decision — MOD-0190 S&OP and MOD-0192 Capacity UI pack revisions + Phase 1.5 tables (Q80) — 2026-09-26

Recorded by the Q81 chat lane (single ledger and pack writer) at 2026-09-26T17:54+03:00 on CT instruction (CT writes no files).
Decision given by the owner through the question tool, 2026-09-26 ~17:50 +03:00 (CT conversation), on the options in
`docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/SIGN-OFF.md` (SHA-256 `8fcdf780caa9aac93ffe105293abeaed18da35d12294e87a98038f1758976438`; package `SHA256SUMS`
`d8170212b71fd2166b3f2c1a011a09693b459fb3cd43cc41765bb704c5686398`, 4/4 OK at 17:52).

This is the fixed record path that the `Approved:` lines in §23 and §24 of both patches point to.

## Decision 1 — MOD-0190 S&OP: option A

| Item | Path | SHA-256 |
|---|---|---|
| Patch | `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/01-MOD-0190-ui-revision.patch` | `eea740986b0058b06a43bb198b9a446f9a1cc7dde41f445661b7bd8f49db8225` |
| Pack preimage | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `56fb8e7d19daa36370747f391ac92c05eca1b51c84d643fbdf7ace687b82ac41` |
| Pack expected postimage | same path | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` |
| Phase 1.5 table | `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-190.md` | `fb6ad9d8f63381822e0287a0251fb69ddfe7354ff753442c0198a4a8a9945cf8` |

Approved text (option A, verbatim from SIGN-OFF.md):

> I approve applying `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/01-MOD-0190-ui-revision.patch` (SHA-256 `eea740986b0058b06a43bb198b9a446f9a1cc7dde41f445661b7bd8f49db8225`) to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, only if the pack's SHA-256 before it is `56fb8e7d19daa36370747f391ac92c05eca1b51c84d643fbdf7ace687b82ac41` and after it is `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f`. The patch sets `shell: tenant`, `golden_reference: slim` and `form_field_count: 5`, keeps `status: ready-for-dev`, and adds §23 (tenant UI scope, GoldenReferenceSlim: entry page with "Create plan" and "Open plan by ID" and no plan list, plan workspace with snapshots and sign-offs tables, "Capture snapshot" and "Record sign-off", the six published operations and four existing permission keys only, 32 owned UI paths, acceptance rows SU-VS1…SU-20 and OUT rows SU-SCR-01…09, effort 52/86/148 h) and §24 (self-registration `sop-workflow-signoffs`, pages `SANDOP_PLANS` / `SANDOP_PLAN_DETAILS`, SR-D4 ship rule). I also approve the Phase 1.5 table `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-190.md` (SHA-256 `fb6ad9d8f63381822e0287a0251fb69ddfe7354ff753442c0198a4a8a9945cf8`) with F190-LIST, F190-DEMAND, F190-PIN and F190-ENT kept as open findings. One named writer applies the patch after rechecking every hash; any mismatch stops the application and nothing is partially applied; an independent VER follows. This decision does **not** authorize: UI code beyond my separate decisions of 2026-09-26 ("modules first", draft overlays) and a versioned UI dispatch; any gateway, permission, navigation, localization, icon-map, DCP, `Program.cs` or other shared edit except by the single CT-appointed integration owner; a contract or backend change (including a plan list operation); the contract re-pin; `done` status; a registry or status-tracker update; rollout, E5/G5; commit, push or stash.

## Decision 2 — MOD-0192 Capacity: option A

| Item | Path | SHA-256 |
|---|---|---|
| Patch | `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/02-MOD-0192-ui-revision.patch` | `a9199cf1a9d7f6d2960171fd1c300fa7b523fcc134d40ae1861356bd83306bfb` |
| Pack preimage | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `9b8b90f1def56be975d63c456a6fb61ba574fec8e2060457d53c72c839fdf813` |
| Pack expected postimage | same path | `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f` |
| Phase 1.5 table | `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-192.md` | `5dee65d1d07b81974de600991ae8ed110ae2c0a1a6edf2eb093cc2dd55af4116` |

Approved text (option A, verbatim from SIGN-OFF.md):

> I approve applying `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/02-MOD-0192-ui-revision.patch` (SHA-256 `a9199cf1a9d7f6d2960171fd1c300fa7b523fcc134d40ae1861356bd83306bfb`) to `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`, only if the pack's SHA-256 before it is `9b8b90f1def56be975d63c456a6fb61ba574fec8e2060457d53c72c839fdf813` and after it is `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f`. The patch sets `shell: tenant`, `golden_reference: slim` and `form_field_count: 7`, keeps `status: ready-for-dev`, and adds §23 (tenant UI scope, GoldenReferenceSlim: entry page with "Create plan" and "Open plan by ID" and no lists, plan workspace with "Create scenario", "Open scenario by ID", "Evaluate" and an evaluation panel with the bottleneck table and a manual Refresh, decimal values as strings, the six published SANDOP-CAPACITY 3.0.0 operations and four existing permission keys only, 32 owned UI paths, acceptance rows CP-VS1…CP-20 and OUT rows CP-SCR-01…10, effort 58/98/168 h) and §24 (self-registration `capacity-planning`, pages `CAPACITY_PLANS` / `CAPACITY_PLAN_DETAILS`, SR-D4 ship rule). I also approve the Phase 1.5 table `docs/roadmap/plans/mvp6-ui-scope-190-192-01/PH15-UI-192.md` (SHA-256 `5dee65d1d07b81974de600991ae8ed110ae2c0a1a6edf2eb093cc2dd55af4116`) with F192-LIST, F192-DEMAND and F192-POLL kept as open findings. One named writer applies the patch after rechecking every hash; any mismatch stops the application and nothing is partially applied; an independent VER follows. This decision does **not** authorize: UI code beyond my separate decisions of 2026-09-26 ("modules first", draft overlays) and a versioned UI dispatch; automatic polling; any gateway, permission, navigation, localization, icon-map, DCP, `Program.cs` or other shared edit except by the single CT-appointed integration owner; a contract or backend change (including list operations); `done` status; a registry or status-tracker update; rollout, E5/G5; commit, push or stash.

## Scope

Pack text and the two Phase 1.5 tables only. **Not approved:** UI code (beyond the separate 2026-09-26 decisions "modules first"
and draft overlays, and a versioned UI dispatch), any shared edit outside the single integration owner, contract or backend
changes, `done` status, registry/status-tracker updates, rollout, commit, push or stash.

Note: the Q81 dispatch abbreviated the MOD-0192 patch hash as `a9199cf1…b8225`; the full hash above is taken from the package
`SHA256SUMS` and SIGN-OFF.md, as the dispatch instructs.
