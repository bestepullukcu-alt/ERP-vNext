# MOD-0190 / MOD-0192 — owner override: build the full working UI (opens the composition path)

> **OWNER DECISION that SUPERSEDES** `docs/records/decisions/2026-10/mvp6-0190-0192-fixture-bound-owner-decision-01.md`
> (2026-10-05). That decision kept 0190/0192 fixture-bound for all of MVP-6 and **did not authorise building
> their UI**. This record **reverses that** for the UI and composition, on the owner's explicit choice. The
> prior record is not edited (K4) — it stands, superseded by this one from 2026-10-18 forward.

- owner: **ny@gmgroup.ch** — decision relayed by the Control Tower, 2026-10-18
- choice: **Full working UI** for MOD-0190 S&OP and MOD-0192 Capacity — i.e. authorise building the UI AND
  opening the composition path it depends on, accepting that this is a **multi-gate program effort that
  expands beyond the original MVP-6 bounded scope**.

## What this authorises

1. Composing MOD-0190 and MOD-0192 in `Diten.SupplyChainService` production (reversing the deliberate
   non-composition in `Program.cs` and Q484's test-host-only env-gate for Capacity), **once their gates
   close** — see the sequenced plan.
2. Building their tenant UI from the **already-approved** §23 UI scope and §24 manifests
   (`mvp6-sop-capacity-ui-scope-owner-decision-01`, `mvp6-sop-capacity-ui-pack-signoff-owner-decision-01`) —
   no longer barred.
3. Pulling the upstream producers they need **into scope**: the **SUPPLY-CONSTRAINTS** contract (MOD-0192
   precondition) and **DEMAND v2** uptake (real demand instead of the test `DemandFixtureReader`).

## What it does NOT do (still gated — see the plan)

This authorisation is not a green light to compose or publish today. The composition still requires the
**Q292 §22 gate** to open, the **SUPPLY-CONSTRAINTS** contract to exist and be frozen, and the **DEMAND v2**
consumer re-model (the conditions from `mvp6-demand-v2-consumer-acceptance-01`) — the last of which depends
on the **MOD-0188 owner** finalising the v2 draft. Until those close, the reserved gateway routes stay 404.

## Current build state (measured 2026-10-18, so the plan is honest)

- **Backend coded**, all layers, for both modules (`.../Features/{SandopPlans,CapacityPlans}` across
  Application/Domain/Persistence/Api/Infrastructure) — but **fixture-bound** (`DemandFixtureReader`) and
  **not composed** in `Program.cs`; every `/api/supply-chain/...` route **404s** in production.
- **UI designed** (§23 scope + §24 manifests approved; UI-revision patches exist at
  `mvp6-ui-pack-patch-190-192-01/patches/`) — but **not built** (no views/JS/resx under
  `frontend/.../SupplyChain/{Sandop,Capacity}`).
- `sop-workflow-signoffs` (0190) and `capacity-planning` (0192) ModuleCodes are already in the manifest set
  (DCP-009 §24); the DEMAND consume permission is new.

## Sequenced plan

`docs/roadmap/plans/mvp6-0190-0192-ui-wave-01/PLAN.md` — the gate chain, owners, dependencies and the
critical path, with the pieces CT can prepare now marked.
