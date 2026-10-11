# HELD DEV prompt — MVP6-MOD0192-DEV-01 / v1.0

**State: HELD. Do not dispatch until Phase 1.5 and all C192 decisions governing the selected slice are recorded.** Target agent/entry point: `@orchestrator` + `/add-module` after pack is owner-promoted. Agent Lane: `AL-MVP6-MOD0192-DEV-01`; Build Lane: planning/capacity; risk HIGH; profile A for stateful backend and E4. This text is a draft, not runtime authorization.

> WP: MVP6-MOD0192-DEV-01 · Prompt v1.0 · HELD  
> Module: MOD-0192 Capacity Planning; domain: supply-chain-execution; service: Diten.SupplyChainService:5061; pack: `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` (**currently draft**); shell: none; golden_reference: none; form_field_count: 0; DataTable: no; entity_base: EntityBase, tenant-owned.  
> Target branch: `feature/mvp6-logistics`; observed HEAD at drafting: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; **replace with fresh measured branch/HEAD/worktree/dirty manifest before dispatch**.  
> Depends on: CT shipment sequence disposition, owner-approved pack promotion, C192-01..08 as applicable, exact frozen hashes and single integration-owner handoff.  
> Parallel-safe with MOD-0190 feature lane only when paths are disjoint; shared seams are single-writer, serialized.

NE: Implement only the explicitly approved CapacityPlans slice of the six frozen operations. Preserve DEMAND facts as reference/provenance and the frozen SANDOP-CAPACITY wire shape.

NEDEN: Make the accepted Capacity plan/scenario/evaluation behavior traceable to immutable input references, scoped state, audit and outbox without inventing an optimizer, scheduler or DEMAND producer operation.

NASIL: Read AGENTS.md, domain config, promoted pack, CT SOP, exact owner decisions, frozen contracts and this spec. Establish fresh dirty-input/source manifest and Phase 1.5. Use only approved mock or producer adapter; label fixture proof. Implement tenant+LE scoped 5-layer/CQRS feature under exact owned paths. Apply selected execution/result/concurrency rules exactly; persist accepted/terminal evaluation and receipt/audit/outbox atomically. Obtain integration-owner handoff for any shared DI/Program.cs/permission/gateway wiring; do not edit those paths in this lane. Correlation/causation and event types follow frozen examples.

ALLOWED: `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/CapacityPlans/**`; corresponding Application, Domain, Persistence and Infrastructure `Features/CapacityPlans/**`; `services/Diten.SupplyChainService/tests/**/CapacityPlans/**`; lane-specific evidence only. PROTECTED: `Features/SandopPlans/**`, DEMAND/shared YAML, Program.cs, project/solution/shared DI/permission registry, gateway, other module paths and `.antigravity/**`.

YAPMA: No runtime algorithm/worker assumption beyond owner decision; no uncontracted endpoint, source SoR copy, cross-scope lookup, pack promotion, canonical/guard edit, commit/push/stash or E5/G5 claim.

DOĞRULA: A192 matrix for the chosen bounded slice; contract parse/ref/example, relevant service build, DB-010 Mongo with fixed test DB and tenant/LE isolation, signed HTTP/JWT/RBAC, replay/races, persistence/restart and audit/outbox fault paths. Keep mock behavior distinct from live DEMAND/constraint uptake. Source→binary→process→evidence hashes required.

OUTPUT: SOP §22 report, exact source/patch/manifest, process/raw HTTP/DB/probe/launch/exit transcripts, row-by-row PASS/PARTIAL/FAIL, remaining GAPs, and independent VER handoff. Failure protocol: stop at authority/contract mismatch, report exact baseline/expected/actual, leave affected gate BLOCKED; never resolve a new business rule locally.
