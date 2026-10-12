# MOD-0184 DEV-01 prompt v1.0 — HELD / NOT READY

Prepared by MVP6-MOD0184-PREP-01 on 2026-09-16. This is a reviewable future dispatch,
not permission to start. Reissue v1.1 or later after all open decisions and fresh HEAD are bound.
The current measured pack status is **draft**, not approved/ready-for-dev.

```text
@orchestrator /add-module
Work Package ID: MVP6-MOD0184-DEV-01
Prompt ID: MVP6-MOD0184-DEV-01-PROMPT
Prompt Version: 1.0
Build Lane: MVP6-CARRIER-BACKEND
Agent Lane ID: MVP6-MOD0184-DEV-01
Agent Lane Type: DEV
Target Agent / Entry Point: @orchestrator + /add-module
State: PLANNED; Dispatch HELD; DoR NOT READY
Golden-Flow Profile: B — Backend / contract
Repository: /Users/natig/Projects/ERP-vNext-recovery
Branch: feature/mvp6-logistics
Expected Base HEAD: 4a8d4d4b339528a88e6220fb8402e5a2c771136c
Worktree: /Users/natig/Projects/ERP-vNext-recovery
Dirty-worktree baseline: existing untracked docs/roadmap/plans/mvp6-logistics-continuation-2026-09-16.md
and docs/records/audits/2026-09/mvp6-continuation-review-2026-09-16/;
PREP adds/modifies exactly the four paths in pack §27. Re-enumerate and preserve all.
Capability Block: DCP-009 / MVP-6 Logistics
Module: MOD-0184 Carrier Management
Build Sequence: 0183 → 0184 → {0185,0186,0187} → {0190,0192} → {0147,0148}
Risk Class: HIGH
Authority Sources / önce oku:
1. AGENTS.md
2. execution/domains/supply-chain-execution/domain-config.md
3. execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md (including §§21–27)
4. docs/analysis/contracts/shipment-bundle.openapi.yaml (FROZEN 1.0.0 / v1)
5. docs/analysis/contracts/supplier.openapi.yaml (read-only ownership reference)
6. docs/guides/operations/control-tower-sop.md §§8,17,18,20,22,24,28,29,36
7. execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md
8. execution/registries/module-id-registry.md and DCP-002 identity gate
9. docs/roadmap/plans/mvp6-logistics-continuation-2026-09-16.md
10. docs/records/audits/2026-09/mod-0184-prep-01-approval-v1.0.md
Lane 1 dependency evidence: docs/records/audits/2026-09/mvp6-mod0183-ct-review-01-2026-09-16.md;
bounded Shipment E4 ACCEPTED, Carrier PREP prerequisite sufficient, runtime CLOSED.
This new external-lane report appeared during preparation; preserve its bytes.
Related inspection: MVP6-MOD0184-PREP-01; Phase 1.5 pack §25.
Protected Paths: frozen contracts; .antigravity/**; all MOD-0183 source/evidence;
other features/services; frontend; gateway; central registries/DCP/governance.
Program.cs remains protected unless a later explicit owner dispatch authorizes that exact exception.
Parallel-Safe With: read-only inspection only; no parallel shared composition writer.
Integration Order: central prerequisite disposition → approved pack/Phase 1.5 → DEV → independent VER → CT;
gateway/catalog/E5 require separate INT authorization.

Depends On: Lane 1 bounded acceptance; GAP-184-02…08 owner decisions; explicit pack/Phase 1.5
and shared composition approval; runtime dispatch release (not granted by Lane 1).
Target-agent inputs: module pack execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md; current status draft (BLOCKER);
domain supply-chain-execution; service Diten.SupplyChainService, existing five-layer service;
shell none; golden_reference none (backend-only exception); form_field_count 0; DataTable no.
Expected sequential specialist chain after authorized release: business-analyst → orchestrator Phase 1.5
approval → data-agent/backend-architect → security-agent → testing-agent → documentation-writer.
No frontend/l10n/integration-agent dispatch in this lane. This prompt itself dispatches no agents.

NE: Implement only GET/POST /api/shipment-bundle/carriers and POST
/api/shipment-bundle/carriers/{carrierId}/status, matching pack §21 and approved decisions.
NEDEN: Carrier is the next sequenced module; draft currently prevents runtime development.
NASIL: Validate first, use CQRS and internal Response<T>/CustomBaseController with frozen wire mapping;
apply three permissions supplychain.carriers.read, supplychain.carriers.create,
supplychain.carriers.status.change to tenant_user JWT callers. Use pack §23 exact owned paths and
only explicitly approved Program.cs composition hunk. Persistence L3: scoped Mongo transaction
for entity+replay+audit, unique indexes and current-state CAS; no new client version field.
Ownership & boundaries: Carrier operational identity/modes/status; Supplier remains MOD-0140.
Allowed Paths: exact pack §23 prospective allowlist, effective only after signed/versioned owner release;
new DEV evidence docs/records/audits/2026-09/mod-0184-dev-01/ only. Central tracker updated by CT, not DEV.
Preconditions: GAP-184-01…08 disposed; central prerequisite report attached; pack explicitly promoted;
Phase 1.5 approved; shared writer assigned; fresh branch/HEAD and baseline incorporated into new prompt version.
Consistency expectation: unique code per scope, serialized valid lifecycle, durable exact replay,
no partial persistence and no lost updates; error/replay/correlation policy exactly approved.
Pattern: N/A backend-only; contract flow caller→schema/auth→handler→transaction→wire result.
YAPMA: No runtime while HELD/draft. No invented Carrier event, by-ID/update/delete endpoint,
Supplier master, gateway/permission catalog change, Warehouse ingress, Inventory write, hidden
contract tightening, commit/push/stash, or MOD-0183 source edit outside an explicitly approved exception.
DOĞRULA: Pack C01–C12 with unit/schema/real HTTP and persisted evidence, permission and 2×2 scope
matrix, barrier concurrency, fault injection, restart and rollback compatibility. E2+E3+E4 required.
Run DCP-002; dotnet build services/Diten.SupplyChainService/Diten.SupplyChainService.sln;
dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj;
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests.
Run new carriers probes and existing MOD-0183 regression tests without changing historical evidence.
Record fresh architecture results versus historical 15/3, never label known failures PASS.
Validation plan: exact request/response capture + scoped DB before/after + restart evidence + hashes.
Output contract: SOP §22 report, AC→artifact map, exact diff/commands/exits, test counts, hashes,
process/binary provenance, audit/replay counts, risks and unresolved gates. No secrets in artifacts.
Failure protocol: stop on remaining GAP, missing approval, branch/HEAD drift, protected path need,
contract ambiguity, undeclared migration or unplanned shared writer; report, do not expand scope.
Agent PASS is not VER PASS or CT ACCEPTED. E5/G5 and integration stay HELD.
```
