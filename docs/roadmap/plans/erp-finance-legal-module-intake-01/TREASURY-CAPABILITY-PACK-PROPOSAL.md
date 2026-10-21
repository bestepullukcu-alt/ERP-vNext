# Treasury intake — capability pack proposal

    created  : 2026-10-11
    status   : PROPOSAL / UNAPPROVED. Not a DCP yet — a DCP number is assigned on approval.
               Next free: DCP-011. Minting it is an owner/CT act, not done here.
    decision : the owner has decided to migrate the Treasury modules. This document is the
               executable form of that decision, not a re-opening of it.
    measured : 2026-10-11, consumer HEAD 2acce233b; producer /Users/natig/Projects/ERP-vNext
    companion: PLAN.md in this folder (phasing and the wider ten-module picture)

## Scope — defined empirically by the producer's own service

Rather than assume what "Treasury" means, the set is taken from what
`services/DitenErpTreasuryService` actually contains (279 tracked files), by MOD reference count
in its source:

| module | canonical name (DCP-002 verified) | producer refs | producer controllers |
|---|---|---|---|
| MOD-0124 | `Cash Positioning` | 99 | `CashPositionSnapshots`, `CashPositionThresholdPolicies`, `CashCategories`, `CashCategoryAssignmentRules` |
| MOD-0120 | `Accounts Receivable (AR)` | 99 | `AccountsReceivable` |
| MOD-0126 | `Payments & Approvals` | 55 | `PaymentBatches` |
| MOD-0125 | `Cash Forecasting` | 50 | `CashForecastingProjectionInputs` |
| MOD-0119 | `Accounts Payable (AP)` | 29 | `AccountsPayable` |
| MOD-0146 | `Payment Run Support` | — | `Mod0146PaymentSourceSnapshots` — **see Blocker 2** |
| MOD-0289 | `Bank Master & Bank Account Registry` | — | **not in the treasury service** — lives in their `Diten.Platform` |

**All seven pass the DCP-002 gate** with these exact names (`verify_module_id.py … → OK, proven
against Blueprint/registry`). Identity is not a blocker. None of the seven is currently in our
registry.

### The code is not in one place

"Migrate the treasury service" is not available as a single lift. The modules are spread across
**three** producer services:

    DitenErpTreasuryService   MOD-0119, 0120, 0124, 0125, 0126, 0146
    Diten.Platform            MOD-0289 (BankPaymentsGatewayController, Mod0289ContractBootstrapTests)
                              MOD-0269 (same controller — external provider)
    DitenMdmService           MOD-0217 CLM, MOD-0220 (the Legal Governance pair, see PLAN.md)

Their treasury service also **consumes** MOD-0023, 0269, 0031, 0021, 0028, 0003, 0048, 0018, 0037.

## Feasibility — measured against our platform, not assumed

Each module's Blueprint `Dependency Gate (must exist first)`, resolved to module IDs, counted as
non-test source files present in our `services/`:

| gate | module | our source files | state |
|---|---|---:|---|
| Workflow Designer | MOD-0023 | **199** | present |
| RBAC / ABAC Authorization | MOD-0018 | **285** | present |
| Audit Trail Service | MOD-0021 | **113** | present |
| Integration Monitoring & Reconciliation | MOD-0037 | **73** | present |
| Data Contract Registry | MOD-0003 | **3** | **thin — verify before relying on it** |
| Metric & Semantic Registry | MOD-0004 | **0** | **absent** |
| Data Warehouse / Lakehouse | MOD-0063 | **0** | **absent** |

Per module:

| module | gates | verdict |
|---|---|---|
| MOD-0119 AP | Workflow · Data Contract Registry · Audit | **buildable**, subject to MOD-0003 verification |
| MOD-0120 AR | Data Contract Registry · Billing integration surface | **buildable**, subject to MOD-0003; billing surface undefined here |
| MOD-0124 Cash Positioning | Data Contract Registry · Integration Monitoring | **buildable**, subject to MOD-0003 |
| MOD-0126 Payments & Approvals | Workflow · Audit · **MOD-0269 external provider** | **buildable** once the provider decision exists |
| MOD-0289 Bank Master | Data Contract Registry · RBAC · Audit · Workflow · **MOD-0126** | **buildable after MOD-0126** |
| MOD-0125 Cash Forecasting | **MOD-0063 Data Warehouse · MOD-0004 Metric Registry** | **NOT BUILDABLE HERE** — both gates measure 0 source files |
| MOD-0146 Payment Run Support | (P2P group) | **governance collision** — see Blocker 2 |

## Three blockers, all factual

**1 · MOD-0125 Cash Forecasting cannot be migrated.** Its two Blueprint gates — Data
Warehouse/Lakehouse and Metric & Semantic Registry — have **zero source files** in this
repository. This independently explains the ~20% readiness in the source table: the gap is not
forecasting logic, it is two absent platform capabilities. Migrating its code would produce a
module that cannot run. Recommend: exclude from this intake, reserve the ID, and make it a
successor pack once MOD-0004 and MOD-0063 exist.

**2 · MOD-0146 collides with a deliberate exclusion in our own registry.** The registry states,
verbatim: *"Not in this reservation (deliberate): MOD-0146 Payment Run Support (Not SoR;
Finance/Treasury; out of scope)"*. Their treasury service nonetheless ships
`Mod0146PaymentSourceSnapshotsController`. A wholesale lift therefore imports a module this
repository has already ruled out of scope. This needs an explicit owner reversal or an explicit
exclusion — it cannot be resolved silently by copying.

**3 · MOD-0269 is a provider, not a build.** Blueprint suite *External Systems Register*, group
*External Provider*, no dependency gate. It gates MOD-0126. What is required is a provider and
interface decision, not an implementation. Their `BankPaymentsGatewayController` is the control
wrapper, not the bank.

Also relevant and non-blocking: **their treasury service is not routed.** Their gateway's
`ocelot.json` declares ports 5004, 5050, 5056, 5057, 5058 — none for `DitenErpTreasuryService`.
Their readiness figures describe code, not a deployed, reachable service.

## Proposed capability boundary

| field | proposal |
|---|---|
| members | MOD-0119, MOD-0120, MOD-0124, MOD-0126, MOD-0289 — **five** |
| deferred | MOD-0125 (Blocker 1), MOD-0146 (Blocker 2) |
| external prerequisite | MOD-0269 provider/interface decision |
| Blueprint groups | Financial Management (0119, 0120) + Treasury & Cash (0124, 0126, 0289) |
| domain | **new** — `execution/domains/finance-treasury/` (AGENTS.md "Yol A") |
| service | **new** — `Diten.FinanceTreasuryService`, 5-layer per §6 |
| port | **5066** — next free; AGENTS.md §3 records 5066+ as next after the OD-5 collision history |
| `runtime_code_allowed` | **false** at pack level. Flips per member at its own ready-for-dev gate (CAP-001 §7) |

Two Blueprint groups in one pack is a deliberate narrowing of PLAN.md's three-pack proposal: AP
and AR are the direct upstream of Cash Positioning and Payments & Approvals in the producer's own
service, and splitting them would put one dependency chain across two packs. **Legal Governance
(MOD-0217, MOD-0220) stays out** — different suite, and MOD-0220 is an unresolved fork.

## Delivery sequence, from the Blueprint's gates

    1  MOD-0269 provider/interface decision        (owner; no code)
    2  MOD-0003 Data Contract Registry verified    (3 source files — confirm it is real)
    3  MOD-0119 AP  +  MOD-0120 AR                 (independent of each other)
    4  MOD-0124 Cash Positioning                   (needs 0003 + 0037)
    5  MOD-0126 Payments & Approvals               (needs Workflow + Audit + 0269 decision)
    6  MOD-0289 Bank Master & Bank Account Registry (gated on 0126 by the Blueprint)
    ·  MOD-0125 Cash Forecasting                   deferred until 0004 + 0063 exist

## What the owner must decide

1. Approve the five-member boundary, or amend it.
2. **MOD-0146** — reverse the registry exclusion, or confirm the exclusion and leave it behind.
3. **MOD-0125** — accept deferral, or fund MOD-0004 and MOD-0063 first.
4. New domain `finance-treasury` and new service `Diten.FinanceTreasuryService`.
5. **Port 5066.**
6. Assign DCP-011 (or a chosen number) and promote this proposal to a CAP-001 pack.
7. Each member then needs its own module pack reaching `ready-for-dev` before any code.

## On the word "migrate"

The owner's decision to bring these modules in is recorded and this proposal implements it. One
mechanical point stands, and it is measurement rather than objection: the producer's code targets a
different service topology, different service naming, and — measured in MOD-0140 — an error
envelope that returns `succeeded=true` on HTTP 4xx. Each member will clear the twelve mandatory
runtime decisions in AGENTS.md §6 on arrival, so the realistic unit of transfer is the
**contract, the test vectors, the entity/field model and the decision records**, with the handlers
re-expressed against our pipeline. That is what the per-member packs in step 7 should scope.

## Measurement limits

Verified here: the producer's treasury service file count, controller inventory and MOD reference
counts; the producer's gateway port list; module locations across their three services; Blueprint
ID, name, wave, group and dependency gate for every module named; DCP-002 gate results for all
seven; our registry absence for all seven; our gate source-file counts; the MOD-0146 registry
exclusion text; our port band and next free port.

Not verified: whether the producer's treasury code builds or its tests pass; the readiness
percentages in the source table, which are that team's figures and were measured by nobody here;
whether MOD-0003's three source files constitute a usable Data Contract Registry — that is
sequence step 2 and it may demote MOD-0119, MOD-0120 and MOD-0124 to not-buildable.

---

# AMENDMENT 1 — the three open questions are answered, and the boundary collapses

    amended : 2026-10-11, after producer-lane answers and independent verification
    effect  : proposed membership drops from FIVE to ONE
    status  : still PROPOSAL / UNAPPROVED

## The answers, and what verification found

| question | producer answer | verified here |
|---|---|---|
| MOD-0122 Bank Reconciliation | exclude — pack `blocked`, governance only, runtime not started | accepted; not independently re-measured |
| MOD-0127 FX Exposure | exclude — pack `draft`, placeholder outside Cash Positioning Wave 1 | accepted; not independently re-measured |
| MOD-0003 Data Contract Registry | **verify, then exclude** — no registry runtime; treasury holds fail-closed and fixture clients, not the producer | **confirmed, and stronger than stated** |

### MOD-0003 is absent from BOTH repositories

**Producer side**, their own code says so in plain text.
`SourceContractValidationClientAdapters.cs` contains exactly two implementations —
`FailClosedSourceContractValidationClient` and `FixtureSourceContractValidationClient` — and the
fail-closed one throws with this message:

> *"MOD-0003 source-contract validation client is fail-closed. Configure
> CashPositioning:SourceContractClient:Mode to Fixture (Development/Test) once MOD-0003 ships a
> stable data-contract registry route, or to GatewayClient when an adapter is implemented."*

No MOD-0003 producer exists anywhere in their repository; `DitenErpTreasuryService` is the only
service referencing it, as a consumer.

**Consumer side — and this corrects an earlier figure in this folder.** PLAN.md and the proposal
above recorded MOD-0003 as "3 source files — thin". That was a **false positive**. All three are
`Diten.HcmService` employee-master files that merely contain the substring `DataContract`:
`EmployeeMasterValidationContracts.cs`, `EmployeeReferenceDataContracts.cs` and
`EmployeeMasterContractValidators.cs` — the last being five FluentValidation `AbstractValidator`
classes for employee records. None is a Data Contract Registry. Searching for a MOD-0003 producer
or any `*ContractRegistry*` type returns nothing.

**MOD-0003 is therefore not thin. It is absent on both sides.**

## Consequence — four of the five proposed members lose their gate

Measured against the Blueprint `Dependency Gate (must exist first)` column:

| member | gate includes Data Contract Registry? | status |
|---|---|---|
| MOD-0119 Accounts Payable (AP) | **yes** | **gate unmet** |
| MOD-0120 Accounts Receivable (AR) | **yes** | **gate unmet** |
| MOD-0124 Cash Positioning | **yes** | **gate unmet** |
| MOD-0289 Bank Master & Bank Account Registry | **yes** | **gate unmet** |
| MOD-0126 Payments & Approvals | no — Workflow, Audit, MOD-0269 | **gates met**, pending the MOD-0269 provider decision |

This is the outcome PLAN.md Phase 3 was written to produce, and it arrived before any approval
cycle was spent: *"If the W-1/W-2 gates are not implemented here, no module in this set is
buildable, and the plan stops at this phase with that as its finding. That outcome is a success
for the plan, not a failure."*

It also reconciles the source table's own wording. MOD-0124 was listed at ~80% with *"live
dependency activation remains"* — that is precisely this: Cash Positioning runs today against a
**fixture** client because the registry it depends on does not exist.

## Revised boundary

| field | amended proposal |
|---|---|
| members | **MOD-0126 Payments & Approvals — one** |
| blocked on MOD-0003 | MOD-0119, MOD-0120, MOD-0124, MOD-0289 — reserve the IDs, do not scope the work |
| blocked on MOD-0004 + MOD-0063 | MOD-0125 Cash Forecasting (Amendment 0 / Blocker 1, unchanged) |
| excluded by producer answer | MOD-0122, MOD-0127 |
| excluded by registry ruling | MOD-0146 (Blocker 2, unchanged — still needs an explicit owner reversal or confirmation) |
| external prerequisite | MOD-0269 provider and interface decision — now the **only** thing between us and the one remaining member |

A one-member capability pack is not worth a new domain, a new service and a port. **The honest
recommendation is therefore to not open DCP-011 yet.**

Two coherent routes, and the choice is the owner's:

1. **Fund MOD-0003 Data Contract Registry first.** It gates four of the five directly, and it is
   a W-2 platform capability absent from both repositories. Closing it converts this proposal from
   one member to five in a single move. Nothing else in this intake unlocks as much.
2. **Take MOD-0126 alone**, into an existing domain rather than a new one, once the MOD-0269
   provider decision exists. Smallest possible step; leaves the other four reserved and idle.

## What changed in this amendment, and what did not

Changed: the MOD-0003 figure (from "thin, 3 files" to "absent, 0 files") and the resulting
membership (five to one). Unchanged: every identity finding, the canonical-name corrections, the
four-group split, the MOD-0269 reclassification, the MOD-0146 collision, the MOD-0125 deferral, and
the measured conformance audit of the producer's treasury code.

Not verified in this amendment: the MOD-0122 and MOD-0127 pack statuses, taken from the producer
lane's answer rather than re-measured here; and whether a MOD-0003 implementation exists in any
third repository.
