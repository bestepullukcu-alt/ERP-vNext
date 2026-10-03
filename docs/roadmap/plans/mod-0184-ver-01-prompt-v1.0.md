# MOD-0184 VER-01 prompt v1.0 — HELD / implementation not yet delivered

Independent verification proposal. No DEV execution/evidence exists for this slice. Before dispatch,
CT must bind a new version to the actual delivered commit/diff, DEV evidence manifest and approved
pack revision. The baseline below is measured preparation HEAD, **not a future verified implementation**.

```text
@testing-agent
Work Package ID: MVP6-MOD0184-VER-01
Prompt ID: MVP6-MOD0184-VER-01-PROMPT
Prompt Version: 1.0
Build Lane: MVP6-CARRIER-INDEPENDENT-VERIFICATION
Agent Lane ID: MVP6-MOD0184-VER-01
Agent Lane Type: VER
Target Agent / Entry Point: testing-agent; independent verifier, not the DEV author
State: PLANNED; Dispatch HELD
Golden-Flow Profile: B — Backend / contract verification (isolated test DB writes permitted)
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

Depends On: released DEV prompt, approved pack decisions, DEV delivered source/diff and immutable
manifest, isolated test DB, independent verifier assignment and refreshed Expected Base HEAD.
Allowed Paths: new evidence only docs/records/audits/2026-09/mod-0184-ver-01/;
isolated DB-010 test data and local build outputs. Source/config/contracts/governance read-only.
Do not call this strict Profile C: HTTP tests create test records and build outputs.
Target-agent inputs: validate CreateCarrier/ChangeCarrierStatus/GetCarrierList handlers and
exact Carrier HTTP surface; expected behavior C01–C12; isolation+soft-delete fixture requirements
pack §24; test project services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj.

NE: Independently prove or reject the delivered bounded Carrier implementation against approved
pack, frozen wire contract, and exact authorized diff. No fixes in VER lane.
NEDEN: DEV self-report cannot close HIGH-risk persistence/auth/concurrency delivery.
NASIL: Read original source/contracts and run commands independently. Verify implementation HEAD,
binary/startup provenance, live process port, required indexes and transaction-capable isolated Mongo.
Use L3 persistence with fresh caller→auth/schema→command→persist/audit/replay→reload/error evidence.
Check all C01–C12, including soft-deleted fixtures, overlapping codes across 2 tenants × 2 LEs,
missing permissions/claims, schema nullable vs required, replay after later lifecycle/restart,
parallel duplicates/transitions and failure before/after commit. Inspect actual DB state after each.
Ownership & boundaries: audit only Carrier; Supplier/Shipment/protected source preserved.
Consistency expectation: serializable business outcome, no lost update, no duplicate success audit,
no successful partial writes; no invented Carrier event. All error codes must trace to owner decisions.
Preconditions: actual DEV commit/evidence and all DoR release decisions bound to new prompt version;
stop immediately if current draft/HELD state persists.
YAPMA: No source repairs, permission bypass, production DB writes, central acceptance, pack promotion,
commit/push/stash, overwritten historical evidence, or treating stale process/tests as current proof.
DOĞRULA: DCP-002; service build; full SupplyChain test project; architecture project; Carrier HTTP/restart
probes and Shipment regressions. Exact commands are pack §24. Check contract refs/examples and
negative endpoint surface; compare protected hashes and approved Program.cs hunk. Capture actual
sent bytes/responses and persisted records with secrets redacted. E2+E3+E4 required; no E5 claim.
Validation plan: independently rerun, report skips/failures honestly; historical 15/3 architecture
baseline is comparison only. Unreachable replica-set or absent failure probe is missing evidence, not PASS.
Output contract: SOP §37 VERIFICATION REPORT with Agent Verdict / Verification Verdict /
CT Status separately; each AC PASS/FAIL/BLOCKED with path:line or immutable evidence pointer;
manifest and reproduction commands; measured source HEAD, counts, process/restart proof,
failed criteria, exact rework request and next gate. CT status remains pending.
Failure protocol: scope/contract/evidence mismatch → FAIL with bounded rework; unavailable prerequisite
→ BLOCKED. Do not silently waive or rerun against modified source. A rework needs versioned DEV then VER.
```
