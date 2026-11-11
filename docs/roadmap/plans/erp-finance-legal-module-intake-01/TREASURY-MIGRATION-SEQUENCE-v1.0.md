# Treasury migration — the sequence

    version : 1.0
    created : 2026-10-11
    status  : PROPOSAL / UNAPPROVED
    aim     : migrate the Treasury part of the source project into ERP-vNext-recovery
    basis   : Blueprint `Dependency Gate (must exist first)` for each module, intersected
              with the DESTINATION's measured implementation state. Not a wish list — the
              order below is forced by the gate graph, not chosen.

## 1 · The set

Taken from the source's own `DitenErpTreasuryService` plus the bank/payments modules that serve it:

| module | canonical name (DCP-002 verified) | Blueprint group | wave | source readiness* |
|---|---|---|---|---|
| MOD-0119 | Accounts Payable (AP) | Financial Management | W-3 | ~95% |
| MOD-0120 | Accounts Receivable (AR) | Financial Management | W-3 | ~90% |
| MOD-0124 | Cash Positioning | Treasury & Cash | W-3 | ~80% |
| MOD-0125 | Cash Forecasting | Treasury & Cash | W-4 | ~20% |
| MOD-0126 | Payments & Approvals | Treasury & Cash | W-3 | ~85% |
| MOD-0289 | Bank Master & Bank Account Registry | Treasury & Cash | W-3 | ~85% |
| MOD-0269 | Banks/SWIFT/Payments Gateway [External Provider] | **External Provider** | W-1 | ~60% |
| MOD-0146 | Payment Run Support | Procure-to-Pay | W-3 | ~0–10% |

\* source's own figures, reproduced for context. Measured by nobody here (TP-001 T7), and the source
itself states ±10 points and that it reran no builds or live workflows.

## 2 · The gate graph — measured, both sides

Destination implementation state of every gate the set needs:

| gate | destination | note |
|---|---|---|
| MOD-0023 Workflow Designer | **199 files** | present |
| MOD-0018 RBAC / ABAC | **267 files** | present |
| MOD-0021 Audit Trail Service | **122 files** | present |
| MOD-0037 Integration Monitoring & Reconciliation | **73 files** | present |
| MOD-0031 Evidence Linking | **71 files** | present (soft-page dependency, not a hard gate) |
| MOD-0169 Billing & Invoicing | **23 files** | partial — the likely referent of MOD-0120's gate, see §6 |
| SoD / approver | **12 files** | **thin, and approvals are MOD-0126's core** |
| **MOD-0003 Data Contract Registry** | **0 files** | **absent — and absent in the source too** |
| **MOD-0004 Metric & Semantic Registry** | **0 files** | absent |
| **MOD-0063 Data Warehouse / Lakehouse** | **0 files** | absent |
| **MOD-0269 bank gateway** | **0 files** | external provider — a decision, not a build |

The source's own platform readiness supports this: it lists MOD-0023 at ~55%, MOD-0021 at ~70%,
MOD-0018 at ~65% — and **does not list MOD-0003, MOD-0004 or MOD-0063 at all.** Three gates that
four of the eight modules depend on are absent from both repositories and from the source's own
inventory of what it has built.

### The forced order

```
MOD-0269  external provider        no gates — the root of the chain
   |
   +--> MOD-0126  Payments & Approvals        + MOD-0023 OK  + MOD-0021 OK
          |
          +--> MOD-0146  Payment Run Support  + MOD-0119
          |
          +--> MOD-0289  Bank Master          + MOD-0003 X  + MOD-0018 OK + MOD-0021 OK
                                              + MOD-0023 OK + MOD-0269
                                              + MOD-0220 FORK + MOD-0140 FORK

MOD-0003  Data Contract Registry    ABSENT BOTH SIDES
   +--> MOD-0119  Accounts Payable            + MOD-0023 OK + MOD-0021 OK
   +--> MOD-0120  Accounts Receivable         + Billing surface (MOD-0169, partial)
   +--> MOD-0124  Cash Positioning            + MOD-0037 OK
   +--> MOD-0289  Bank Master                 (see above)

MOD-0004 + MOD-0063                 ABSENT
   +--> MOD-0125  Cash Forecasting
```

**MOD-0289 is the hardest module in the set, not an easy one.** Its ~85% source readiness is about
its own code; its Blueprint gate list is eight items long and includes MOD-0003 (absent), MOD-0269
(a provider decision), **MOD-0220 and MOD-0140 — both of which exist here as forks of the source's
own versions.** A MOD-0289 migrated from the source is built against the source's supplier and
legal-entity contracts, which measurably differ from ours. That is a contract-mismatch risk, not a
missing-dependency risk, and it is invisible in a readiness percentage.

## 3 · The sequence

### Wave 0 — decisions, no code · all four are independent and can run in parallel

| # | decision | owner | unblocks |
|---|---|---|---|
| 0.1 | **MOD-0269 provider / interface** — which provider, which interface, what "controlled dispatch" means here. Blueprint classes it *External Provider*; the source reports real bank dispatch unavailable | owner + whoever owns banking | MOD-0126, MOD-0289 |
| 0.2 | **MOD-0126 placement** — domain and service. A conformant pack cannot be written without it; see `MOD-0126-PLACEMENT-DECISION-PACK.md` | owner | the whole of Wave 1 |
| 0.3 | **MOD-0003 Data Contract Registry — fund or defer** | owner | MOD-0119, MOD-0120, MOD-0124, MOD-0289 — four of eight |
| 0.4 | **MOD-0146** — confirm the registry's deliberate exclusion, or reverse it | owner | whether MOD-0146 is in scope at all |

0.3 is the highest-leverage decision in the entire migration. It is the single gate that converts
this from a one-module intake into a five-module one.

### Wave 1 — MOD-0126 Payments & Approvals · the only module buildable today

Gates: MOD-0023 ✓ · MOD-0021 ✓ · MOD-0269 (needs 0.1).

    1.1  producer packet for MOD-0126                    source lane — IN PROGRESS
    1.2  verify the SoD/approver depth here (12 files)    destination — approvals are the
                                                          module's core; do not assume
    1.3  destination module pack, status `draft`          after 0.2 and 1.1
    1.4  MOD-0269 integration verified, not fixture       after 0.1
    1.5  owner promotes pack to `ready-for-dev`           only then may code be written
    1.6  implement: contract + field model + test vectors
         kept, handlers re-expressed against our pipeline
    1.7  destination supplement §M, §Q, §R, §S, §T, §U    TP-001; §U gates release

### Wave 2 — unlocked only by MOD-0003 · three modules, two can run in parallel

    2.1  MOD-0119 Accounts Payable        gates 0003 + 0023 OK + 0021 OK
    2.2  MOD-0120 Accounts Receivable     gates 0003 + Billing surface (see §6)
    2.3  MOD-0124 Cash Positioning        gates 0003 + 0037 OK

2.1 and 2.2 are independent of each other. 2.3 is independent of both. Each repeats the Wave 1
shape: producer packet → placement already settled by then → pack → `ready-for-dev` → implement.

### Wave 3 — MOD-0289 Bank Master · needs Wave 1, Wave 2 and both fork resolutions

    3.1  MOD-0140 fork resolution — which repo is the SoR
         evidence: docs/records/audits/2026-10/mod-0140-dual-implementation-01/
    3.2  MOD-0220 fork resolution — comparison NOT YET DONE
    3.3  then MOD-0289, whose gate list needs 0003, 0018, 0021, 0023, 0126, 0220, 0140, 0269

**3.1 and 3.2 can start now** — they are independent of every other item in this sequence and of
each other. If the aim is to shorten the critical path, they are the work to start in parallel with
Wave 0.

### Wave 4 — MOD-0125 Cash Forecasting · needs two further absent platform capabilities

    4.1  MOD-0004 Metric & Semantic Registry     absent
    4.2  MOD-0063 Data Warehouse / Lakehouse     absent
    4.3  then MOD-0125

The source's ~20% figure is consistent with this: the gap is not forecasting logic, it is two
platform capabilities neither repository has.

### Out of scope unless 0.4 reverses it

    MOD-0146 Payment Run Support — registry-excluded as "Not SoR; Finance/Treasury; out of
    scope"; source reports ~0-10% operationally ready; gates on MOD-0126 and MOD-0119, so it
    is last in the graph regardless.

## 4 · Critical path

    0.1 MOD-0269 decision  ->  1.x MOD-0126  ->  3.3 MOD-0289
    0.3 MOD-0003 decision  ->  2.x (three modules)  ->  3.3 MOD-0289
    3.1 + 3.2 fork resolutions  ->  3.3 MOD-0289

Three strands converge on MOD-0289, so it is last whatever happens. **Everything except MOD-0126
is gated behind a decision or an absent platform capability**, which means the migration's pace is
set by Wave 0, not by engineering capacity.

## 5 · What can start this week, without any further decision

| | work | why it is unblocked |
|---|---|---|
| a | MOD-0126 producer packet | source lane, already in progress |
| b | **MOD-0220 fork comparison** | needs nothing; mirrors the MOD-0140 lane; a Wave 3 prerequisite |
| c | **verify SoD/approver depth here** (12 files) | destination measurement, no approval needed |
| d | **verify MOD-0169 Billing & Invoicing** (23 files) | resolves MOD-0120's unresolved gate |
| e | correction record for the 66 affected manifests | prescribed K4 remedy, still unwritten |

b, c and d are all destination measurements that could change the sequence, and none costs an
approval cycle. The pattern is established: verifying MOD-0003 took two commands and collapsed a
five-member proposal to one.

## 6 · Two unresolved gate names

**"Billing integration surface"** (MOD-0120's gate) resolves to no module of that name. The
likeliest referent is **MOD-0169 Billing & Invoicing** (W-2, precedes MOD-0120's W-3), which has
**2 registry rows and 23 source files here** — partial. Candidates also include MOD-0129
E-Invoicing/E-Archive (W-3) and MOD-0143 Invoice Capture & 3-Way Match (W-2). **This needs an
Enterprise Architect ruling before MOD-0120 can be scoped**, and it is the same class of defect as
"Document Management", which likewise resolves to nothing.

**MOD-0269's gate list is empty**, which is correct for an External Systems Register entry and is
why it is the root of the chain rather than a dependency of it.

## 7 · Measurement limits

Measured here: Blueprint ID, name, wave, capability group and full dependency-gate string for all
eight modules; DCP-002 gate results; destination implementation file counts for every named gate;
the registry's MOD-0146 exclusion; MOD-0140 and MOD-0220 presence here; MOD-0169's registry rows and
file count.

Not measured: every source readiness percentage in §1 — those are the source's figures, carry its
own stated ±10 points, and were produced without rerunning builds or live workflows; whether the
source's modules reach their own workflow/audit/evidence/approver dependencies through real
implementations or fail-closed clients, which the source lane has flagged and which belongs in its
packet §D; and the MOD-0220 fork, still unmeasured.

---

# AMENDMENT 1 — the cycle dissolves, and four identities are corrected

    amended : 2026-10-11, after the source lane's sequence and identity corrections
    effect  : no change to the wave order. The reported cycle does not exist in the
              Blueprint; one identity error of mine is corrected; one identity defect is
              found in the source's own pack filenames.

## 1 · There is no cycle — measured in both directions

The source reports *"the source MOD-0126 pack depends on MOD-0289, while the Blueprint makes
MOD-0289 depend on MOD-0126"* and proposes an approval-only slice to break it. Measured:

    MOD-0126 gates : Workflow Designer (Approvals/SLAs/Escalations); Audit Trail Service;
                     Banks/SWIFT/Payments Gateway [External Provider]
                     -> contains NO reference to MOD-0289 or Bank Master

    MOD-0289 gates : Data Contract Registry; RBAC / ABAC Authorization; Audit Trail Service;
                     Workflow Designer; **Payments & Approvals**; Corporate Secretarial /
                     Entity Management; Supplier Onboarding; Banks/SWIFT/Payments Gateway
                     -> contains MOD-0126

**One direction only: MOD-0126 → MOD-0289.** The reverse edge exists solely in the *source's own
pack*, at lines 31, 51 and 64 of `MOD-0126-payment-proposals-approvals.md`.

A source pack is not authoritative here. The destination's authority order (AGENTS.md §1) ranks
destination module packs, destination domain configs, then AGENTS.md; DCP-002 makes the Blueprint
canonical for module identity and its gate column. **A source pack sits outside that order
entirely** — it is evidence about the source's implementation, not a constraint on the destination's
sequence. So the wave order in §3 stands unchanged.

### And the source's own pack shows why no slice is needed

Line 70 of that same pack reads: *"MOD-0126 must not store raw bank account numbers or provider
secrets."* So their MOD-0126 already holds a **bank reference, not bank data** — it delegates
storage to MOD-0289 and resolves through it.

An opaque reference needs MOD-0289 **at resolve time, not at build time.** MOD-0126 can therefore
be implemented here with the bank reference as an opaque identifier, and MOD-0289 wired in when it
lands — which is the contract-first posture this repository already runs on, and the same pattern
MVP-6 uses where only an opaque `supplierId` crosses the boundary.

That is cleaner than a bounded approval-only slice: no slice boundary to define, no partial module
to re-open later, and it is grounded in the source's own stated constraint rather than in a
negotiated scope cut. **The source's cycle finding is correct about their implementation and does
not require a Blueprint-level resolution.**

## 2 · Identity corrections — all four confirmed against the Blueprint

| ID | asserted earlier | **Blueprint canonical** | group / wave |
|---|---|---|---|
| MOD-0043 | "Tenant Architecture Foundation" | **SLO/SLA Monitoring** | Observability · W-3 |
| MOD-0044 | "Tenant Manager" | **Backup & Restore** | Resilience · W-3 |
| MOD-0121 | — | **Bank Reconciliation** | Financial Management · W-4 |
| MOD-0122 | "Bank Reconciliation" | **Period Close & Consolidation** | Financial Management · W-4 |
| MOD-0127 | FX Exposure Management | **FX Exposure Management** ✓ | Treasury & Cash · W-4 |

**Provenance, stated plainly.** MOD-0043 and MOD-0044 were never asserted in any document in this
folder — they appear only in the source's platform readiness list, and the correction is the
source's own. MOD-0122 as "Bank Reconciliation" **was repeated by me**, in
`TREASURY-CAPABILITY-PACK-PROPOSAL.md` at lines 168, 224 and 246. It was attributed to the source
lane each time and marked "not independently re-measured", but it was repeated without running the
gate — and running the gate is exactly what T1 exists to require. Corrected here rather than by
editing that document, since it carries its own amendment history.

Neither correction changes the sequence: MOD-0121, MOD-0122 and MOD-0127 are all **W-4 Financial
Management or Treasury & Cash** modules that remain outside the eight-module set, and both
exclusions still hold on their stated grounds.

## 3 · A DCP-002 identity defect in the source's pack filenames

The source's pack is named `MOD-0122-bank-reconciliation.md`. Per the Blueprint, **MOD-0122 is
Period Close & Consolidation**, and Bank Reconciliation is **MOD-0121**.

So that pack claims an ID whose canonical name is a different capability. Under TP-001 T1 the gate
would refuse it, and under DCP-002 the registry would record a collision. The source presumably
needs either a rename to `MOD-0121-bank-reconciliation.md` or an Enterprise Architect ruling — and
should check whether `MOD-0122` is also reserved elsewhere in its registry for Period Close.

Reported, not corrected: that is the source's repository and its own identity gate to run.

## 4 · On the source's proposed order

Their ten-step order and the five waves in §3 agree on every dependency, and their step ordering is
compatible with the wave graph. Two differences worth naming:

- They place **MOD-0289 at step 6, before AP/AR at step 7.** The gate graph permits this —
  MOD-0289 and AP/AR are independent of each other — but MOD-0289 carries eight gates including
  both forks, while AP and AR carry three and two. Doing the hardest module before two easier ones
  that share its MOD-0003 gate front-loads the risk. **A sequencing preference, not a conflict.**
- Their step 3 pairs *"close Interface Registry prerequisites, then implement missing MOD-0003"* —
  which correctly identifies MOD-0003 as something to **implement**, not transfer. It exists in
  neither repository, so there is nothing to migrate; it is new build either way.

## 5 · Measurement limits for this amendment

Measured: the full Blueprint gate strings for MOD-0126 and MOD-0289 in both directions; Blueprint
canonical names, waves and groups for MOD-0043, MOD-0044, MOD-0121, MOD-0122, MOD-0127; the source
pack's MOD-0289 references and its line-70 constraint; the source's pack filename against the
Blueprint; and my own propagation of MOD-0122 in this folder.

Not measured: the source's ten-step plan at `/private/tmp/treasury-transfer-sequence-2026-10-11.md`,
which is outside both repositories and was not read; and whether the source's registry reserves
MOD-0122 for Period Close.

---

# AMENDMENT 2 — MOD-0120's billing gate, investigated with contract evidence

    amended : 2026-10-11
    trigger : "Don't assign the billing surface to MOD-0169 without contract and ownership
              evidence." The caution was correct — MOD-0169 is the wrong answer.
    effect  : MOD-0120 moves from "blocked on MOD-0003" to blocked on MOD-0003 PLUS an
              unowned contract, and two further absent prerequisites surface.

## 1 · The gate names a contract, not a module — and the contract has no producer

The Blueprint's contract columns answer this directly, so no inference was needed:

    MOD-0120  Min Integration Contract Codes : ERP-FIN-BUNDLE;AR-BILLING-BUNDLE
              notes : "billing contracts, customer ID binding, dispute reference IDs,
                       audit hooks"

Searching every Blueprint row for declarers of each code:

| code | declared by | verdict |
|---|---|---|
| **AR-BILLING-BUNDLE** | **MOD-0120 only** | **no producer exists.** The gate's consumer is its sole declarer |
| ERP-FIN-BUNDLE | MOD-0118 GL · MOD-0119 AP · MOD-0120 AR | shared bundle, three co-declarers, no single producer |

**MOD-0169 was the wrong answer and would have been a silent misassignment.** MOD-0169 Billing &
Invoicing declares `BILLING-BUNDLE` — a *different code*, SoR "billing runs". The other candidates
are equally wrong by code: MOD-0129 declares `ERP-EINV-BUNDLE`, MOD-0143 `MATCH-BUNDLE`, MOD-0036
`EDI-BUNDLE`. Name resemblance pointed at MOD-0169; the contract codes point at nobody.

So "Billing integration surface" is the **same defect class as "Document Management"** — a named
gate with no owning module — but sharper, because the code is explicit and its only declarer is the
module that needs it.

**Neither contract artifact exists.** Zero matches for `ERP-FIN-BUNDLE`, `AR-BILLING-BUNDLE`,
`fin-bundle` or `billing-bundle` in the destination's `docs/analysis/contracts/` or the source's
`contracts/`.

## 2 · The source never implemented a billing integration

Code evidence from `AccountsReceivableController.cs` and the AR persistence layer:

- AR serves `GET invoices` from **its own repository** — `_repository.ListInvoicesAsync`,
  `ReceivableInvoiceListFilter`, `InvoiceLifecycleStatus`; 173 invoice references in the
  persistence layer
- **zero files matching "billing"** anywhere under their treasury service's `src/`

So their AR **owns its invoices locally and consumes no billing producer.** That is internally
consistent with their own note — *"Restricted MVP; HAR capture or waiver signoff remains"* — and
with ~90%: it is 90% of an AR that holds its own invoices.

The consequence for migration is specific. Transferring their AR yields an AR with locally-held
invoices and **no billing integration** — which may be an acceptable restricted MVP here, but it
does not satisfy the Blueprint gate, and **the gate has no producer to satisfy it with.** That is an
owner decision about scope, not an engineering gap to close.

## 3 · Two further absent prerequisites, surfaced by ERP-FIN-BUNDLE

Because AP and AR share `ERP-FIN-BUNDLE` with a third module:

**MOD-0118 General Ledger (GL)** — W-3, Financial Management. SoR: accounts, ledgers, journals,
postings, fiscal calendars.

    in our registry : 0 rows
    in our code     : 0 files
    in the source's readiness list : absent entirely
    its own gates   : API Gateway; Data Contract Registry; SoR & Ownership Registry

GL co-declares the financial bundle that both AP and AR consume, and **nobody in this migration has
mentioned it.** Whether AP and AR are coherent without a General Ledger is a question for the owner;
it was invisible until the contract codes were read.

**MOD-0001 System-of-Record & Ownership Registry** — **W-1**, Authoritative Registries. GL's third
gate.

    in our registry : 0 rows
    in our code     : 0 files

A **W-1 foundational registry**, absent. For contrast, GL's other two gates are met: MOD-0032 API
Gateway is reserved with 12 implementation files, and MOD-0003 is the already-known absence.

## 4 · Revised position for MOD-0120 and MOD-0119

| module | position before Amendment 2 | position now |
|---|---|---|
| MOD-0120 AR | Wave 2, blocked on MOD-0003 | **blocked on MOD-0003 + an unowned contract (AR-BILLING-BUNDLE) + a shared bundle whose co-declarer GL is absent.** Transferable only as an explicitly scoped restricted MVP with no billing integration |
| MOD-0119 AP | Wave 2, blocked on MOD-0003 | unchanged in its own gates, but it **co-declares ERP-FIN-BUNDLE with GL and AR**, so the same bundle-ownership question applies to it |

Wave order is unchanged — both were already behind MOD-0003. What changed is that MOD-0003 is no
longer the *only* thing in their way, and **AP/AR before MOD-0289 is now a weaker preference**: AP
and AR have acquired a bundle-ownership question that MOD-0289 does not share.

## 5 · What this adds to the decision list

| # | decision | owner |
|---|---|---|
| 0.5 | **`AR-BILLING-BUNDLE` has no producer.** Assign an owner, or accept MOD-0120 as a restricted MVP with no billing integration and record that as a deviation under TP-001 T32 with an owner and an expiry | owner + EA |
| 0.6 | **`ERP-FIN-BUNDLE` has three co-declarers and no producer.** Decide who owns it before AP or AR is scoped | EA |
| 0.7 | **MOD-0118 General Ledger** — in or out of scope? If in, it brings MOD-0001 (W-1, absent) with it | owner |

0.5 is the one that can be settled quickly, because the source has already answered it in practice:
they built AR without billing integration. Ratifying that as the destination's scope is a smaller
decision than finding a producer for a contract nobody owns.

## 6 · Next in the agreed order

The agreed priority was billing, then SoD/approver depth, then the MOD-0220 fork. Billing is done
and it did expose further prerequisites, as predicted. **SoD/approver depth is next** — 12 files
measured here, against a module whose entire purpose is approvals.

## 7 · Measurement limits

Measured: MOD-0120's `Minimum Integration Contracts` and `Min Integration Contract Codes` columns;
every Blueprint row declaring either code; contract codes and SoR for MOD-0169, MOD-0129, MOD-0143
and MOD-0036; MOD-0118's row and gates; MOD-0001's identity and wave; registry rows and
implementation file counts here for MOD-0118, MOD-0001 and MOD-0032; contract-artifact absence in
both repositories; and the source AR controller and persistence layer for billing consumption.

Not measured: whether the source's 11 outbound HTTP clients under its treasury `src/` serve any
AR-adjacent purpose — none matched "billing", but their individual targets were not enumerated;
and whether the source's registry reserves any of MOD-0118, MOD-0001 or the two bundle codes,
since the source has no `execution/registries/module-id-registry.md`.

---

# AMENDMENT 3 — correcting Amendment 2's inference, not its data

    amended : 2026-10-11
    trigger : "'No producer' is stronger than the evidence supports."
    effect  : the finding weakens from ABSENT to UNRESOLVED. MOD-0169 returns as a live
              candidate. GL stops being an automatic prerequisite. Wave order unchanged.

## 1 · The inference was wrong, and the correction matters

Amendment 2 reasoned: *`AR-BILLING-BUNDLE` is declared only by MOD-0120, therefore no producer
exists.* That does not follow. **The `Min Integration Contract Codes` column records which modules
declare a bundle as their minimum integration contract — it does not encode producer versus consumer
roles.** A consumer declaring a bundle says nothing about whether a producer exists; it says the
column is silent on the question.

So the supported conclusion is: **producer ownership and the integration mapping are unresolved.**
Not absent. Amendment 2's "no producer exists" is withdrawn.

I also overcorrected in the opposite direction by calling MOD-0169 "the wrong answer". A different
bundle code does not disqualify a counterpart.

## 2 · MOD-0169 is a live candidate, and its own notes say why

Measured — the two bundles read as complementary halves of one interface:

    MOD-0120  AR-BILLING-BUNDLE  billing contracts · customer ID binding ·
                                 dispute reference IDs · audit hooks
    MOD-0169  BILLING-BUNDLE     billing schema · **AR integration contract** ·
                                 discrepancy exception model · audit hooks

MOD-0169's bundle **names an AR integration contract explicitly**, both carry "audit hooks", and
the pairing of *dispute reference IDs* with a *discrepancy exception model* reads as two sides of
the same exception flow.

A further signal from the `Integration Contracts` column: **MOD-0169 is `BRIDGE-BASE`**, while
MOD-0118 GL and MOD-0120 AR are both `DOMAIN-APP-BASE`. A bridge is what sits between two domain
apps, which is consistent with MOD-0169 being the billing counterpart rather than a third unrelated
domain.

**This is suggestive, not conclusive.** It is textual evidence of intent in the Blueprint, not a
producer assignment. The mapping still needs an EA ruling — but MOD-0169 should go into that ruling
as the leading candidate, not be excluded from it.

## 3 · ERP-FIN-BUNDLE is a basket, and most of it is already here

Its own notes decompose it, and measuring each named concern in the destination:

    ERP-FIN-BUNDLE (API Gateway, OAuth2/JWT, GL data contracts, Canonical IDs,
                    AuditEvent v1, Evidence links/export, OTel/Correlation-ID, error model)

| named concern | destination |
|---|---|
| API Gateway | Ocelot, **6 files** |
| OAuth2 / JWT | **35 files** |
| Canonical IDs / correlation | **967 files** |
| AuditEvent v1 | **96 files** |
| Evidence links / export | **63 files** |
| OTel / Correlation-ID | **52 files** |
| error model | `ProblemDetails` / `Response<T>`, **2705 files** |
| **GL data contracts** | **the only GL-specific element** |

So `ERP-FIN-BUNDLE` is **seven platform concerns we already have, plus GL data contracts.** It is
not a single produced interface, and it does not need one producer.

**This supports the third qualification directly: sharing the bundle does not make GL a prerequisite
for AP or AR.** What AP and AR need from the bundle is the platform baseline, which is present.
What they would need from GL specifically is "GL data contracts" — and whether either module
actually requires them is a scope question, not a dependency the bundle imposes.

MOD-0118's own gates remain as measured (API Gateway ✓, Data Contract Registry ✗, MOD-0001 SoR &
Ownership Registry ✗ — a W-1 absence), but that now bears on **GL's** buildability, not on AP's or
AR's.

## 4 · And my absence claim about the source was also too strong

Amendment 2 said the source has "**zero** files matching billing" under its treasury `src/`. That
measurement stands, and the restricted-MVP reading is supported by it — but **zero matches on one
keyword across one tree does not prove absence across every possible adapter.** The source lane
reports the same result from its own C#/JSON search, which strengthens the interpretation without
establishing it. 11 outbound HTTP clients exist under that tree whose individual targets I did not
enumerate.

Stated correctly: **the source's AR owns its invoices locally and no billing integration was found.
That is evidence for the restricted-MVP interpretation, not proof of it.**

## 5 · The three decisions, as qualified

| # | decision, restated | owner |
|---|---|---|
| 0.5 | **Define the AR billing boundary**, or approve a restricted-MVP deviation under TP-001 T32 with owner and expiry. MOD-0169 enters this as the leading candidate counterpart | owner + EA |
| 0.6 | **Assign ownership per interface within `ERP-FIN-BUNDLE`** — it is seven platform concerns plus GL data contracts, so ownership is per interface, not per bundle | EA |
| 0.7 | **Decide GL scope explicitly.** Sharing the bundle does not make it an AP/AR prerequisite. If GL is in scope it brings MOD-0001 (W-1, absent) with it; if out, AP and AR are unaffected by its absence | owner |

## 6 · Effect on ordering

**MOD-0289 versus AP/AR stays provisional** until 0.5–0.7 resolve, as agreed. Amendment 2 argued
AP/AR had "acquired a bundle-ownership question that MOD-0289 does not share"; with ERP-FIN-BUNDLE
decomposed and mostly satisfied, that argument is weaker than stated. Both remain behind MOD-0003,
and the ordering preference between them should not be settled on this evidence.

Wave order is unchanged by this amendment.

## 7 · What Amendment 3 changed

**Withdrawn:** "no producer exists" for `AR-BILLING-BUNDLE`; "MOD-0169 was the wrong answer";
"zero billing files proves the source implemented no billing integration"; and the claim that GL's
bundle-sharing makes it an AP/AR prerequisite.

**Retained, all measured:** the contract codes and their declarers; that neither bundle exists as an
artifact in either repository; that the source's AR serves invoices from its own repository with no
billing client found; MOD-0118's and MOD-0001's absence here; and the decomposition in §3.

**Added:** MOD-0169's AR-integration note, the BRIDGE-BASE signal, and the measured decomposition
showing ERP-FIN-BUNDLE is mostly already satisfied here.

## 8 · Next

SoD/approver depth, as agreed — 12 files measured here against a module whose entire purpose is
approvals.
