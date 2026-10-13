# Owner decision — merge `origin/main` now, then re-baseline

- date: 2026-10-05
- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q432, Q433, Q434

## Decision

**Merge now. Stop producing MVP-6 evidence against the pre-2026-09-15 base.**

Rationale, in the owner's words: further verification against the old base produces evidence
that must be rerun after integration. Its marginal value is low; it creates duplicated
verification.

**`origin/main` merges into the MVP-6 branch. No rebase, no history rewrite.** The reason is
not conflict handling — it is that rewriting 18 commits re-identifies them and costs audit
traceability for nothing. Upstream arrives as one integration event.

## Measured geometry

| | |
|---|---|
| diverged | 2026-09-15, `bc109afa4` (PR #111) |
| main since | 845 commits, 2297 files, 19 days (~44/day) |
| us since | 18 commits, 8330 files (mostly the evidence layer) |
| **overlapping** | **28 files** |
| main's changes to `services/Diten.SupplyChainService` | **0** |
| main's changes to `Views/SupplyChain`, `Controllers/SupplyChain` | **0, 0** |

**Stated correctly, because CT first stated it wrongly:** the five modules have a *very low
direct source-conflict risk*. They do **not** have low integration risk. Platform (578 files),
Auth (133) and the gateway (3) changed, and SupplyChain source being unchanged is not
SupplyChain runtime being unchanged. The five golden flows are mandatory after the merge.

## What must survive the merge

| | |
|---|---|
| **Q363** | `IInternalScopeResolutionContext` is registered **0 times on origin/main** — the defect is live there and unfixed. Our 14 lines are new. Keeping the diff is not enough: upstream's DI composition moved 845 commits, so the registration must be **runtime-tested**, not merely present. |
| **Q302** | Our delta in HumanCapital and TalentEcosystem `Program.cs` is the eager JWT validation that made `8f60dc6d3` safe. Main changed 2+/1− lines in each. If the merge takes main's side there, removing the base-config secret becomes unsafe. |
| **gateway** | the `shipment-bundle` route pair, alongside main's 3 gateway changes |
| **today's standards** | `AGENTS.md`, `control-tower-sop.md` (K22, K23), `.antigravity/rules/docs-organization.md` (K5, K6), `.antigravity/agents/frontend-ui-ux.md` |
| **secret removals** | our three security commits must not be undone |

## Secret gate — binding

> **The merge must not complete with known secret material in the resulting tree.**

`origin/main` carries **14** tracked `appsettings*.json` with a `JwtSettings.Secret`. Two
classes, two decisions:

| class | decision |
|---|---|
| **A** — the 2 base configs we already sanitised (`d8b34f90`: HumanCapital, TalentEcosystem) | our sanitised version is preserved |
| **B** — 12 upstream Development configs carrying `ff4555d1`, a digest we have never seen | sanitised in the merge result, **with upstream provenance recorded**. We do not claim ownership of the original defect, and "it came from upstream" does not justify leaving it. |
| **C** — anything credential-like we cannot classify | quarantine, BLOCKED pending security classification |

**Repository sanitation is not rotation.** If those 12 are live credentials, removing them
from the tree does not revoke them. That is a separate operation and a separate record.

## Conflict policy — three buckets, not one

1. **Upstream-authoritative shared surface** — take upstream, reapply only an explicitly
   owned local delta.
2. **Local corrective delta must survive** — Q363 and Q302. Acceptance is upstream behaviour
   retained **AND** our fix retained **AND** runtime-tested.
3. **Composite configuration** — `ocelot.json`, the seven `SharedResource.*.resx`,
   `dt-defaults.js`. Semantic merge, **never ours/theirs wholesale**. For `ocelot.json`:
   upstream routes kept, shipment-bundle kept, and duplicate routes, ordering and auth policy
   checked, then a gateway runtime smoke.

## Evidence lifecycle — nothing is deleted, authority is withdrawn

| evidence | base | status |
|---|---|---|
| Q372-R2 row 13 | pre-merge HEAD | `VALID_PRE_MERGE / INTEGRATION_STALE` |
| Q425 suite baseline | pre-merge HEAD | `VALID_PRE_MERGE / INTEGRATION_STALE` |
| five module golden flows | pre-merge HEAD | `VALID_PRE_MERGE / INTEGRATION_STALE` |
| post-merge reruns | integrated HEAD | acceptance candidate |

They are not wrong and they are not discarded. They are diagnostic and reference only.
**No integrated PASS may be claimed until post-merge re-verification completes** (K18).

## Completion gate

```
merge conflicts resolved
zero known secret material in the resulting tree
Platform / Auth targeted regression passes
gateway validation passes
frontend baseline passes
five SupplyChain golden flows rerun
required MVP-6 gates rerun
```

This merge is therefore not housekeeping. **It is the MVP-6 integrated acceptance gate.**
