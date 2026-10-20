# MVP-6 — G7 removal (S&OP MOD-0190 + Capacity MOD-0192) and clean-up — lane measurement

- **Decision:** `docs/records/decisions/2026-10/mvp6-0190-0192-g7-role-grants-owner-decision-01.md` — G7 temporary, expires
  2026-11-03. Ali's ruling: "remove at expiry, measure the entitlement path first". XMC-001 consent for AuthService given by
  the owner in chat (2026-10-20). Removed before the expiry date.
- **Branch:** `feature/sce/mod-0190-0192-g7-grant-removal`, on top of `feature/sce/mod-0183-q358-grant-cleanup` (#164, not
  yet merged) — it reuses that runner. Not committed at the time of writing.
- **Lane:** `~/mvp6-env/c13-220606` — lane replica set `rsc13` on 35994 (mongod 8.0.18), RabbitMQ 5672, Platform 5057,
  Auth 5056, SupplyChain 5066 started with a lane-only `DemandFixtures` section (tenant B, made-up LE/plan) so that S&OP and
  Capacity are composed. Never 27017. Raw outputs in `lane/`.

## Where G7 could have granted anything (code read, `origin/main` 1ff11f922)

- `DemandConsumerComposition.SeedFor` returns null when `environment.IsProduction()`, so S&OP and Capacity are never
  composed in production. Their manifests (and so their eight keys) reach the Platform catalog and Auth only on a
  non-production host with `DemandFixtures:Enabled=true`.
- So **no production tenant can hold G7 grants**. Only tenants activated on a composed non-production host while the
  entries stood can.
- Both manifests are `IsTenantAssignable: true` — the same entitlement path as Shipments (C-13).

## Entitlement path and widening — measured before the change

| # | Step | Result | Evidence |
|---|---|---|---|
| M0 | Before composing | Platform catalog 24 modules, no G7 module; Auth 0 G7 keys | `lane/g7-m0-before.txt` |
| M1 | SupplyChain started with the demand fixture | `SOP-WORKFLOW-SIGNOFFS` and `CAPACITY-PLANNING` in the catalog; Auth 8 keys; **no existing tenant received a grant** (only SuperAdmin, full catalog) | `lane/g7-m1-composed.txt` |
| M2 | Tenant E registered while G7 stood | Admin **8/8 System**; Viewer 2 `.read` System (read rule) | `lane/g7-m2-tenant-e.txt` |
| M3 | Entitle B (no prior grants) and E to both modules | B Admin **8/8 Module**, Viewer 2 read Module. E stays **8/8 System** — the Q358 overlap trap again | `lane/g7-m3-entitled.txt` |

M3 meets Ali's precondition: the entitlement path grants the eight keys.

## What was changed

| File | Change |
|---|---|
| `…/Domain/Authorization/DefaultRolePermissionTemplate.cs` | `sop-workflow-signoffs`, `capacity-planning` removed from `AdminModules`; comment points to this record. |
| `…/Application.Tests/Roles/G7SandopCapacityRoleSplitTests.cs` | Admin baseline holds none of the eight; Viewer still exactly the two `.read` keys. |
| `…/Domain/Authorization/BaselineGrantRetraction.cs` (new) | The Q358 rule made definition-driven: a definition lists modules and their keys; **each key is decided by its own module's entitlement**. `G7SandopCapacityGrantRetraction.Definition` (8 keys, `G7Retraction:Mode`). |
| `…/Domain/Authorization/Q358ShipmentGrantRetraction.cs` | Now only the Q358 definition (same keys, `Q358Retraction:Mode`, same audit-id format — pinned by a test). |
| `…/Persistence/Seed/Q358ShipmentGrantRetractionRunner.cs` | Takes a definition. **Same write calls** — the closed writer inventory is unchanged (no new owner correction). File keeps its name because the inventory lists calls by path. |
| `…/Infrastructure/Authorization/Q358ShipmentGrantRetractionHostedService.cs`, `DependencyInjection.cs` (comment) | Runs Q358 and G7, each only when its own `…Retraction:Mode` is `DryRun`/`Apply`. |
| `…/Application.Tests/Authorization/G7SandopCapacityGrantRetractionTests.cs` (new), Q358 tests (API update) | See below. |

## Lane measurement of the clean-up (new Auth build, entries removed)

Tenant F was registered on the old build (8/8 System) and entitled to Capacity only — the mixed case.

| # | Run | Result | Evidence |
|---|---|---|---|
| M4 | Before | E 8 System (entitled to both), F 8 System (Capacity only), B 8 Module | `lane/g7-m4-before-cleanup.txt` |
| M5 | `G7Retraction=DryRun` | 16 planned: E 8 × Convert; F Capacity 4 × Convert, S&OP 4 × SoftDelete; **0 changed, 0 audit rows** | `lane/g7-m5-dryrun.txt`, `lane/g7-m5-after-dryrun.txt` |
| M6 | `Apply` | 12 converted, 4 soft-deleted, 0 not applied; 12 + 4 audit rows; E 8 Module, F 4 Module; Viewers and B unchanged | `lane/g7-m6-apply1.txt` |
| M7 | `Apply` again | 0 planned, audit counts unchanged | `lane/g7-m7-apply2.txt` |
| M8 | Entitle F to S&OP | F 8/8 Module — the soft-deleted S&OP grants revived; no `DuplicateRolePermissionException`; 0 soft-deleted G7 grants left | `lane/g7-m8-reactivation.txt` |
| M9 | New tenant G, no entitlement | Admin **0/8**; Viewer 2 `.read` (read rule) | `lane/g7-m9-new-tenant.txt` |

The `BSONError` lines in `g7-m6`…`g7-m8` come from a broken count query in the measuring script (cursor `.map`), not from
the service. M8 repeats the count with a corrected query.

## Tests and sabotage

Owned mongod required (`DITEN_REQUIRE_OWNED_MONGO=1`). Retraction + G7 role split + writer inventory + DefaultRolePermission
filter: **124 passed, 0 skipped**.

| Sabotage (production code, restored and byte-compared after) | Red test(s) |
|---|---|
| A key decided by ANY module of the definition, not its own | `Each_key_follows_the_entitlement_of_its_own_module`, `Apply_converts_the_entitled_module_…` |
| G7 entries put back in `AdminModules` | `Admin_baseline_holds_none_of_the_eight_keys` |
| Converted grant tagged with the Q358 module code | `Apply_converts_the_entitled_module_…` |
| Audit-id prefix changed | `The_Q358_audit_id_keeps_the_format_a_Q358_run_already_wrote_and_G7_ids_differ` |

## Gate

Local run `~/mvp6-env/q358-gate-111143` (owned mongod required, skips = red; lane stack running on the same machine):

| Suite | Result | Note |
|---|---|---|
| Auth `Diten.AuthService.Application.Tests` | 2924 passed, 1 failed, 0 skipped | The red is `PasswordWorkGateTests.The_production_bcrypt_cost_is_measured` (a wall-clock bcrypt measurement, 10 s under load). Re-run alone: **1/1 passed**. Green in the #164 gate; this change does not touch password hashing. Writer inventory: green, no new row. |
| `TenantArchitecture.ArchitectureTests` | 141 passed, 3 failed | The same 3 as the C-13 / #164 baseline; none names a file of this change. |
| `scripts/tests` | 217 OK | — |

## Not done / open

- **Not committed, not pushed** — owner approval required (GIT-002). Depends on #164 (stacked branch).
- **Decision record** `mvp6-0190-0192-g7-role-grants-owner-decision-01.md` still reads "IN EFFECT". Marking it superseded
  is the owner's edit (or with approval).
- **Q358 production run (owner decision 2026-10-20, in chat):** Ali (`bestepullukcu-alt`) runs it at the first deployment
  after #164 merges — `Q358Retraction__Mode=DryRun`, the log is read, then `Apply`, then the setting is removed.
- **Non-production hosts** with `DemandFixtures` enabled: running `G7Retraction` (DryRun → Apply → remove the setting) is an
  operator step; who and when: owner (TBD). Production needs no run (nothing to clean, see above).
- The Viewer's two `.read` keys stay with every tenant's Viewer through the read rule (unchanged by design — the decision
  record's "Viewer: no change"). An Admin without the entitlement therefore holds no S&OP/Capacity key while its Viewer
  can read, the same shape C-13 left for Shipments.
- Not measured on mongod 7.0.28 (CI version).
