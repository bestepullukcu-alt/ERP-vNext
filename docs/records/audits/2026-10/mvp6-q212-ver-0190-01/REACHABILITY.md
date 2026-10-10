# Q212 — MOD-0190 S&OP: reachability and shared seams

Static reading only. Nothing was run and no seam was patched. The pack assigns `Program.cs`, common DI, the shared
permission registry and the gateway to integration owners (pack `:99`); their absence here is **not** a module defect.

Short path: `S.` = `services/Diten.SupplyChainService/src/Diten.SupplyChainService.`

## 1. What the 19 green tests execute — measured, not assumed

- **No test starts a host.** In `tests/Diten.SupplyChainService.Tests/SandopPlans/` there are 0 occurrences of
  `WebApplicationFactory`, `TestServer`, `SandopPlansController` or a handler constructor.
- 16 tests call `SandopRepository` directly against Mongo through `SandopTestHost.Open()`
  (`SandopContractTests.cs:4-7`). The host helper also creates the indexes itself (`:6`).
- 2 tests call pure functions (`SandopRequestFingerprint`, `SandopWire.Validate`).
- 1 test calls the request gate function with a `DefaultHttpContext` and a fabricated principal
  (`SandopContractTests.cs:22-28`).
- Not executed by any test: the controller, the six MediatR handlers, the three FluentValidation validators, the
  pipeline behaviors, `[Authorize]` / JwtBearer, `AddSandopPersistence`.

This matches what Q210 and Q211 found for Returns and Claims.

## 2. State of the service today

| Point | State | Evidence |
|---|---|---|
| Controller routes | **Mapped.** `MapControllers()` discovers `SandopPlansController` by convention; its six routes exist on port 5061. | `S.Api/Program.cs:71`; `SandopPlansController.cs:4-5` |
| Controller construction | Needs only `ISender`, which is registered. | `SandopPlansController.cs:5`; `S.Application/DependencyInjection.cs:11` |
| Handlers | Need `ISandopRepository` and `IDemandFixtureReader`. **Neither is registered.** | handlers `:3`; `AddSandopPersistence` has no caller (`SandopPersistenceRegistration.cs:4`) |
| Request gate | Does not depend on `Program.cs`: it is called inside each action. | `SandopContextMiddleware.cs:3`, `:10` |
| What sits in front | Every non-Carrier, non-Load path goes through **`ShipmentContextMiddleware`**. | `Program.cs:68` |
| Indexes | Never created by the service. `SandopSchema.EnsureAsync` is called only by a test. | `SandopSchema.cs:4`; `SandopContractTests.cs:6` |

So the module is **half-wired**: routes exist, dependencies do not. What a request actually returns: **insufficient
evidence** — this WP may not start the service.

### 2.1 The Shipment middleware in front is a contract problem, not only a wiring gap

By reading `S.Api/Middleware/ShipmentContextMiddleware.cs`, an S&OP request would be judged by Shipment rules before
any S&OP code runs:

| Case | Shipment middleware (in front today) | S&OP gate / published contract |
|---|---|---|
| Bad or missing correlation | 400 with a Shipment message (`:12`) | 400 `INVALID_CORRELATION_ID` (`SandopContextMiddleware.cs:15`; pack `:156`) |
| Header scope ≠ token scope | **404 `SHIPMENT_NOT_FOUND`** (`:24`) | 403 `FORBIDDEN` (`SandopContextMiddleware.cs:17`) |
| Idempotency key | 1 to 128 characters, and the stored value is **trimmed** (`:33-34`) | nonempty exact value, no trim, no added maximum (pack `:129`, `:155`) |

The pack's own composition rule is that shared composition is integration-owned (`:99`, `:133`). Whoever composes
`Program.cs` must exclude `/api/supply-chain/sandop-plans` from the Shipment branch. Without that, wiring the
dependencies alone would still produce responses that break the S&OP contract.

## 3. Shared seams needed to make the module real (none patched)

| # | Seam | What is missing | Owner per record |
|---|---|---|---|
| 1 | `S.Api/Program.cs` — DI | `AddSandopPersistence()`; a registration for `IDemandFixtureReader` | single integration owner — Q209 HELD (`CT-QUEUE.tsv:305`); pack `:99` |
| 2 | `S.Api/Program.cs` — pipeline | Exclude the S&OP path family from the Shipment middleware branch at `:68` (§2.1) | same |
| 3 | Index creation at start-up | A call to `SandopSchema.EnsureAsync`. Nothing in the tree makes it outside tests. See F-Q212-4. | integration owner; **CT should assign it explicitly** — neither the pack nor the owned paths name who calls it |
| 4 | DEMAND source | Outside a test environment there is no fixture, so every create returns 422 `INVALID_DEMAND_REFERENCE` (`SandopRepository.cs:49`). By design: "TEST FIXTURE ONLY" (pack `:119`). A live DEMAND reader is a separate producer/owner binding (pack `:240`). | DEMAND owner + CT |
| 5 | `gateway/Diten.ApiGateway/ocelot.json` | 0 S&OP routes. Needed: GET+POST `/api/supply-chain/sandop-plans`, GET `/{sandopPlanId}`, GET+POST `/{sandopPlanId}/snapshots`, GET+POST `/{sandopPlanId}/sign-offs`; pass-through of `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`; `/sandop-plansXYZ` not matched | integration-agent only (`AGENTS.md:108`); pack `:189-191`, `:417-419` |
| 6 | Shared permission catalogue / seed | Four keys exist only in `SandopPermissions.cs:3`: `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record` | platform owner; pack `:184-185` |
| 7 | Token claims | The gate needs exactly one `tenant_id`, `legal_entity_id`, `sub`, and a `permission` claim per key (`SandopContextMiddleware.cs:14`). Whether Auth issues them: insufficient evidence (outside Q212's scope) | security owner |
| 8 | Event transport | Events stay `Pending` in `sandop_outbox` (`SandopRepository.cs:80`). No worker or publisher, by design (pack `:122`) | separate integration WP; pack `:240` |
| 9 | Workflow | None. JWT actor only (pack `:121`) | later, by owner decision |
| 10 | Self-registration | `SopWorkflowSignoffsManifestProvider` + its `AddSingleton` line + tests M-01…M-08; ships with the UI (pack `:545`) | integration owner |
| 11 | Navigation keys | `Nav.Module.SOPWORKFLOWSIGNOFFS`, `Nav.Page.SANDOP_PLANS` × 7 languages (0 present) | integration owner + l10n agent; pack `:519-526` |
| 12 | Tenant UI | 32 owned UI paths; 0 present. The S&OP UI draft v3 is CT ACCEPTED as a draft and is not in the tree (Q202b HELD) | UI writer after an integrated target; pack `:479-481` |

Seams 1, 2, 5, 6 and 12 are the ones the pack's status_note already lists as open. Seams 3 and 4 are the two this
VER adds: they are not named as anyone's task anywhere I read.

## 4. Related work not yet in the tree

- **Q215** fail-point alignment (`CT-QUEUE.tsv:311`): would turn the two weak S&OP tests into evidence.
- **S&OP UI draft v3** (`docs/records/audits/2026-09/mvp6-sop-ui-draft-03/`): carries the hand-off notes for seams
  5, 10 and 11.
