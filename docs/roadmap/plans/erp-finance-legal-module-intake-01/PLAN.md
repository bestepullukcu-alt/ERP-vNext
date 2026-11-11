# Intake plan — the ten "Business / Treasury" modules

    created   : 2026-10-11
    status    : PROPOSAL / UNAPPROVED — forward-looking plan, not a record and not a decision
    author    : agent lane, from measurements taken 2026-10-11 on
                feature/mvp6-logistics @ 2acce233b and the Blueprint master 8.1
    asks for  : CT/owner decisions at Phase 1, 3 and 4. No code is proposed anywhere in this plan.
    supersedes: nothing

## The finding that reshapes the request

The ten modules are **not one block**. Measured against `Blueprint_Data`, they span **four
suites, five capability groups and three waves**:

| capability group | modules | wave | note |
|---|---|---|---|
| Financial Management | MOD-0119 AP, MOD-0120 AR | W-3 | ERP Suite |
| Treasury & Cash | MOD-0124, MOD-0125, MOD-0126, MOD-0289 | W-3 (0125 is W-4) | ERP Suite |
| Legal Governance | MOD-0217 CLM, MOD-0220 Corp. Secretarial | W-3, W-4 | Legal & Contracts |
| Procure-to-Pay | MOD-0140 | W-1 | **already ours** — procurement domain, ready-for-dev |
| External Provider | MOD-0269 | W-1 | **External Systems Register** — see Phase 2 |

So "one treasury capability pack" is the wrong shape. A single DCP spanning four suites would
breach the one-capability-boundary principle CAP-001 exists to enforce.

**MOD-0269 is not a module to build.** Its Blueprint suite is *External Systems Register* and its
capability group is *External Provider*, with no dependency gate. That is a register entry for a
third-party gateway. It also explains the source table's "real bank dispatch remains unavailable" —
that is a provider contract question, not an implementation backlog item. Treating it as an
internal build is a category error and this plan does not propose one.

## Identity: all ten are mintable, eight of the supplied names are not

The DCP-002 gate was run on every ID. Six blocked, and **every block was a canonical-name
mismatch — not an unknown capability.** With exact Blueprint names all pass
(`OK … proven against Blueprint/registry`).

| ID | name as supplied | **Blueprint canonical name (use this)** |
|---|---|---|
| MOD-0119 | Accounts Payable | `Accounts Payable (AP)` |
| MOD-0120 | Accounts Receivable | `Accounts Receivable (AR)` |
| MOD-0124 | Cash Positioning | `Cash Positioning` ✓ |
| MOD-0125 | Cash Forecasting | `Cash Forecasting` ✓ |
| MOD-0126 | Payment Proposals / Approvals | `Payments & Approvals` |
| MOD-0140 | Supplier Onboarding | `Supplier Onboarding (KYC/Sanctions/Docs)` |
| MOD-0217 | Contract Lifecycle | `Contract Lifecycle Management (CLM)` |
| MOD-0220 | Legal Entities | `Corporate Secretarial / Entity Management` |
| MOD-0269 | Banks / SWIFT / Payments Gateway | `Banks/SWIFT/Payments Gateway [External Provider]` |
| MOD-0289 | Bank / Account Registry | `Bank Master & Bank Account Registry` |

Only MOD-0124 and MOD-0125 were supplied with their canonical names. The source table is not
usable as a work order until this column is corrected.

## Current state here — measured

| | count |
|---|---|
| in our module ID registry | **2 of 10** — MOD-0140 (procurement), MOD-0220 (master-data-management) |
| with a module pack here | **2 of 10** — the same two |
| finance/treasury domain here | **none** (11 domains, none is one) |
| treasury service here | **none** (17 services; their AP/AR/Cash live in `DitenErpTreasuryService`, no counterpart) |
| next free microservice port | **5066+** (AGENTS.md §3, after the OD-5 collision history) |

## The dependency gates are the real blocker

The Blueprint names a `Dependency Gate (must exist first)` per module. Resolved to module IDs and
checked against our registry:

| gate | module | wave | our registry |
|---|---|---|---|
| Workflow Designer (Approvals/SLAs/Escalations) | MOD-0023 | W-1 | reserved |
| Audit Trail Service | MOD-0021 | W-1 | reserved |
| RBAC / ABAC Authorization | MOD-0018 | W-1 | reserved |
| Data Contract Registry | MOD-0003 | W-2 | reserved |
| Metric & Semantic Registry | MOD-0004 | W-2 | reserved |
| Integration Monitoring & Reconciliation | MOD-0037 | W-2 | reserved |
| Data Warehouse / Lakehouse | MOD-0063 | W-3 | reserved |
| **Policy & Control Library** | MOD-0005 | W-1 | **not reserved here** |
| **Records Management (Retention/Legal Hold)** | MOD-0030 | W-3 | **not reserved here** |
| **Document Management (Templates/Versioning)** | — | — | **does not resolve to a Blueprint module by that name** |

Three problems, in order of weight:

1. **"Document Management" is a named gate for MOD-0140, MOD-0217 and others, and it does not
   resolve to a Blueprint module.** Either the Blueprint names it differently or the gate is
   unowned. This must be settled before MOD-0217 can be scoped at all.
2. **MOD-0005 and MOD-0030 are unreserved here**, and MOD-0030 (Retention / Legal Hold) gates
   MOD-0220 — one of the two modules already in our registry.
3. **Reserved ≠ implemented.** This plan has *not* verified whether any of the seven reserved
   gates is actually built and running here. That verification is Phase 3 and it is cheap; it
   should happen before any capability pack is drafted, because it determines whether these
   modules are buildable at all.

### Ordering inside the set, from the Blueprint's own gates

    MOD-0269 (external provider register)
        └─ gates ─> MOD-0126 Payments & Approvals
                        └─ gates ─> MOD-0289 Bank Master & Bank Account Registry

    MOD-0125 Cash Forecasting  requires MOD-0063 Data Warehouse + MOD-0004 Metric Registry
    MOD-0220 Corporate Secretarial  requires MOD-0030 Records Management
    MOD-0124 Cash Positioning  requires MOD-0003 + MOD-0037

This is why the source table shows MOD-0125 at ~20%: its gates are analytics-platform
capabilities, not finance logic.

## The plan

### Phase 0 — corrections that need no approval and block nothing

Two stale artifacts found while measuring. Both are factual fixes to things that already exist,
not new scope.

- **`CLAUDE.md` port line is wrong.** It states 5060 is the last port of the 5011–5060 band;
  AGENTS.md records the band extended to 5061, then 5065, with 5066+ next. CLAUDE.md is the only
  auto-loaded file, so this is the stale line most likely to mislead an agent.
- **MOD-0147 / MOD-0148 describe the wrong contract surface.** The Q439 lane corrected them to
  cite three operations (`listSuppliers`, `getSupplier`, `validateSuppliers`); the contract is now
  v1.2.0 with **nine**, having advanced in `780f8368a` and arrived via the 2026-10-06 merge. The
  packs now understate what MVP-6 may consume.

### Phase 1 — resolve the forks · BLOCKS EVERYTHING ELSE

Two of the ten already exist here, and both are duplicated in the producer repository.

- **MOD-0140 — confirmed fork.** Evidence is written:
  `docs/records/audits/2026-10/mod-0140-dual-implementation-01/`. Needs a CT/owner ruling on which
  repository is the system of record.
- **MOD-0220 — probable fork, not yet measured.** Ours: `master-data-management`,
  `Diten.MdmService`, ready-for-dev, 47 LegalEntity files, `LegalEntityLookupsController`. Theirs:
  `DitenMdmService/LegalEntitiesController.cs`. Run the same comparison MOD-0140 got.

**Rationale for the ordering:** adding eight modules from a repository that already disagrees with
ours about two module IDs multiplies the problem rather than solving it. One ID with two systems of
record behind one opaque reference is the specific failure this sequencing avoids.

### Phase 2 — identity and scoping proposal · no code, no new domain

One document CT can rule on, containing only what is already measured:

1. The corrected canonical-name column above.
2. The four-group split, with the explicit finding that a single treasury DCP is the wrong shape.
3. **MOD-0269 reclassified** as an External Systems Register / External Provider entry — a
   provider and contract decision, not a build.
4. DCP-002 preflight evidence for each ID (already produced).
5. The dependency-gate map, including the two unreserved gates and the unresolvable
   "Document Management" name.

### Phase 3 — dependency-gate decision · the true feasibility gate

- Verify implementation state of MOD-0023, 0021, 0018, 0003, 0004, 0037, 0063 **here** —
  reserved is not built.
- Reserve MOD-0005 and MOD-0030, or record why not.
- Settle what "Document Management (Templates/Versioning)" refers to.

If the W-1/W-2 gates are not implemented here, **no module in this set is buildable**, and the plan
stops at this phase with that as its finding. That outcome is a success for the plan, not a failure.

### Phase 4 — per-group capability packs · owner approval each

Three candidates, each its own CAP-001 pack with its own domain, service and port decision:

| candidate | modules | depends on |
|---|---|---|
| Financial Management | MOD-0119, MOD-0120 | Phase 3 gates |
| Treasury & Cash | MOD-0124, MOD-0126, MOD-0289, then MOD-0125 | Phase 3 gates + MOD-0269 provider decision |
| Legal Governance | MOD-0217, MOD-0220 | Phase 3 gates + MOD-0030 + Phase 1 fork ruling |

A new domain is AGENTS.md "Yol A" — the explicitly rare path — requiring `domain-config.md`,
README, AGENTS.md §2 and §9, the registry and the master development plan.

### Phase 5 — per-module packs, then code

Each module pack reaches `ready-for-dev` individually. CAP-001 §7 is explicit: DCP approval
authorises nothing by itself, and `runtime_code_allowed` flips per module. Each must then clear the
twelve mandatory runtime decisions in AGENTS.md §6.

**This is where "migration" ends and rewriting begins.** The producer's code targets a different
architecture — different service topology, different naming, and in MOD-0140's case an error
envelope that reports `succeeded=true` on HTTP 4xx. The portable assets are the **contracts, the
test vectors and the decision records**, not the implementations. That was the measured lesson of
MOD-0140, where the producer's implementation was richer on governance and ours was
contract-complete, and neither could be dropped into the other.

## What this plan deliberately does not do

- Propose copying code in either direction.
- Treat any of the ten as MVP-6 scope. Measured separately: only MOD-0140 is an MVP-6 dependency,
  and its gap is DC-02/DC-03 authority, which **neither** codebase provides.
- Promote any pack, reserve any ID, create any domain or request any port. Those are Phase 2–4
  owner decisions.

## Measurement limits

Verified directly: the Blueprint rows (ID, module name, wave, suite, capability group, dependency
gate) for all ten; DCP-002 gate results for all ten; our registry rows and packs; our domain and
service inventory; the port band and the next free port; MOD-0220's pack frontmatter and
LegalEntity file count; the producer's service layout and controller locations.

Not verified: whether any reserved dependency-gate module is implemented here; whether the
producer's readiness percentages in the source table are accurate — they are that team's figures,
reproduced for context only and measured by nobody in this plan; and the MOD-0220 fork, which is
asserted as probable on file-level evidence and still needs the Phase 1 comparison.
