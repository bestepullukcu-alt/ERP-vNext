---
id: MOD-0155-FU05
name: MicroTarget Visit Planning Engine
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — ready-for-dev) · MOD-0155-FU02 (Visit Report — calendar/execution page) · MOD-0155-FU03 (Route Optimization — BUILT/ready-for-dev) · MOD-0155-FU04 (Visit Content Sequence — BUILT/ready-for-dev) · MOD-0155-FU06 (Cycle Capacity — SHIPPED) · MOD-0155-FU06B (Activity Time Budget — ready-for-dev) · MOD-0155-FU07 (Cycle Capacity Monthly — SHIPPED)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: n/a (D-UI = B, LOCKED — the Day/Week SETUP screen is a bespoke tenant-shell selection+generation console, NOT a Golden DataTable CRUD surface; `verify_datatable_page` is N/A)
entity_base: EntityBase (D-PERSISTENCE = C, LOCKED — the thin `PlanningSession` staging aggregate is the persisted aggregate; the real plan atoms stay in FU01 `PlannedVisit`)
status: ready-for-dev
runtime_code_allowed: true
flip_approved_by: "user via Control Tower — 2026-08-29 (all 15 D-questions LOCKED §19/§20: PERSISTENCE=thin PlanningSession staging, UI=bespoke tenant-shell console, SELECTION=preview→apply, FREQUENCY-EXTEND=per-target+weekly-route-rerun, REPLAN=in-place, PERIOD/WEEK=MOD-0165 CyclePeriod, TERRITORY=warn-not-filter, CONTENT-ADVANCE=PriorStageIndex from last PlannedVisit, APPLY=transaction+standalone-fallback, SUPPLY-DEMAND=transient, RBAC=split crm.visit-plan.read/generate/apply, CAPACITY=CyclePeriod-pinned, MULTI-REP=single-rep v1)"
owner: module-pack-author
started: 2026-08-29
target: 2026-08-29 (flipped for build)
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = "Visit Plan / MicroTarget")
  - MOD-0155-FU01 (PlannedVisit atom — the plan rows FU05 WRITES: TargetType incl. pharmacy, PlannedVisitContentRef, PlannedVisitScheduleSlot, PlannedVisitAvailabilitySnapshot, PlannedVisitSelectionProvenance, Frequency/Consent provenance. FU05 is the producer FU01 anticipated. ready-for-dev)
  - MOD-0155-FU03 (IRouteOptimizer — BUILT; FU05 builds the RouteOptimizationInput from the selected visit set + calls Optimize(...). The unscheduled[] IS the supply-vs-demand WARNING materialised)
  - MOD-0155-FU04 (VisitContentSequenceResolver — BUILT; FU05 calls ResolveAsync(...) per doctor to get next-content + PromoItemCount/NonPromoItemCount + VisitDurationMinutes, which feeds FU03's durationMinutes + FU01's PlannedVisitContentRef)
  - MOD-0155-FU06B (ActivityTimeBudgetCalculator + CycleCapacity.BetweenVisitTimeMinutes / TotalVisitNumber — ready-for-dev; supply = capacity, betweenVisitMinutes buffer feeds FU03. Read-only)
  - MOD-0165-FU03 (IVisitFrequencyPolicyResolver — SHIPPED; for frequency-extend of weeks 2..n. Read-only, signature unchanged)
  - MOD-0164-FU02 (IConsentPreferenceEvaluator — SHIPPED; contactability gate on selection. Read-only, FilterApplied honoured)
  - MOD-0150 (ContactAvailability — read-only; per-contact HARD availability windows honoured by the engine + passed to FU03)
  - MOD-0149 (Account + AccountRelationship — read-only; clinic↔pharmacy bidirectional link, "pharmacies of a clinic" query)
  - MOD-0167 (Segment eligible-universe filter (ISegmentMembershipReader) + StrategyTemplate play — read-only; segment FILTERS, selection is MANUAL)
  - MOD-0151 (Territory / coverage / MicroZone geo — read-only; territory gate on account selection)
  - MOD-0288 (boundary — Person/Position master; rep working hours are a config placeholder now, HR seam additive later)
  - MOD-0018 (RBAC — new key(s) decision D-RBAC; seed/grant NOT in this pack → F-RBAC)
  - DEV-0001 (Golden Reference Compact — template reference for any list surface)
---

# MOD-0155-FU05 — MicroTarget Visit Planning Engine

> **✅ READY FOR DEV — code authority granted.**
> The **largest FU in the MOD-0155 program** — the orchestration engine that turns *selection* into a *scheduled plan*.
> Flipped 2026-08-29 (user via Control Tower): `status: ready-for-dev` + `runtime_code_allowed: true`. All 15
> D-questions are LOCKED (§19/§20); the design is concrete (`entity_base: EntityBase`, `golden_reference: n/a`, the
> `PlanningSession` field set, the endpoint list, the RBAC keys — all resolved below). `@orchestrator` may build this pack.
>
> **DCP-002 identity gate — PASS (2026-08-29):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU05 --name "MicroTarget Visit Planning Engine" --parent MOD-0155`
> → `OK  MOD-0155-FU05: proven against Blueprint/registry.` (**exit 0**, first try — no id/name change needed).
>
> FU01 built the plan **atom** (`PlannedVisit` + the null-born `PlannedVisitScheduleSlot`, `PlannedVisitContentRef`,
> `PlannedVisitAvailabilitySnapshot`, `PlannedVisitSelectionProvenance` a scheduler/selector fills). FU03 built the
> **`IRouteOptimizer`** seam. FU04 built the **`VisitContentSequenceResolver`**. FU06B built the minute-budget /
> capacity. **FU05 is the ORCHESTRATION that binds them all**: it *selects* the visit set (accounts + doctors,
> segment-filtered, manual), *resolves* content+duration per doctor (FU04), *calls* the route optimizer (FU03),
> *applies* frequency-extend across weeks (MOD-0165), *computes* supply-vs-demand (FU06B), supports *re-plan*, and
> owns the **Day/Week SETUP UI** (selection + generation). It is the vNext materialisation of the legacy `MicroTarget`
> row (the Daywork unified plan atom), split in vNext into **FU01 (the atom it writes)** + **FU05 (this engine)**.
>
> **FU05 does NOT own the execution/calendar page** — that is **FU02 (Visit Report)** — nor does it redefine any of
> the pieces it consumes. It owns the *setup* surface and the *orchestration*, and it defines **no new master**: every
> master (account, contact, availability, segment, strategy, journey, capacity, territory, frequency, consent) is read
> through an **existing seam**.
>
> Authority order: **Blueprint Excel** > this pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Identity Gate and Home Decision

### 0.1 DCP-002 — PASS (2026-08-29)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU05 --name "MicroTarget Visit Planning Engine" --parent MOD-0155
OK  MOD-0155-FU05: proven against Blueprint/registry.
REAL_EXIT=0
```

The gate proves **identity** (parent `MOD-0155 | Field Sales / Visit Planning` is canonical in the registry, and the
`FU05` id does not collide), not the descriptive **name**. Parent's canonical name is **"Field Sales / Visit
Planning"** and does not change; the frontmatter `name` is a repo-side descriptor. `FU05` was already reserved for
**"MicroTarget"** across every sibling pack (FU01/FU03/FU04/FU06B frontmatter); this pack claims exactly that slot —
**no id or name change was needed** (first-try exit 0). **The registry row is NOT written by this pack** (FU01 / FU03
/ FU06B precedent) → §20 / F-REG.

### 0.2 D-HOME — Home is **MOD-0155**

`crm-sor-boundary.md` reads *"Visit Plan / **MicroTarget** / Visit / Visit Report / route plan → MOD-0155"*. The
MicroTarget engine is the "MicroTarget" clause of that line and lives in `Diten.CrmService`. It writes only FU01's
`PlannedVisit` (MOD-0155's own atom) and reads everything else **read-only** through existing seams; it defines no
master of its own.

---

## 1. Module Summary

### 1.1 What it does

FU05 is the **orchestration engine** that turns a rep's *selection* into a *scheduled plan*. It runs the real
field-sales workflow (user's own words, authoritative — see §19 locked context) end to end:

1. Rep opens a **period** (monthly, split into weeks).
2. Picks **accounts** (hospitals/clinics + their related **pharmacies**) considering **working time + territory**.
3. Picks **contacts (doctors)** from those accounts — **filtered by campaign SEGMENT, selected MANUALLY** (segment
   filters the eligible universe; the pick is a human's).
4. Per doctor the engine resolves **content** (FU04) — the **next visit's content auto-advances** — and therefore the
   **visit duration = f(content)** (FU04 → FU06B).
5. Visits are **packed** into working hours (09:00–18:00, lunch 13:00–14:00) back-to-back = duration + travel +
   between-visit buffer.
6. **Route optimized** (FU03) to minimise travel, geographically continuous **across days**, honouring per-contact
   availability as a HARD constraint.
7. First week set → other weeks **auto-extend by frequency** (MOD-0165); the rep **re-plans** manually (doctor missed
   / "I can go day X").

The engine **selects the visit set, resolves content+duration, calls the route optimizer, applies frequency-extend,
computes supply-vs-demand, supports re-plan, and drives the Day/Week setup UI** — and then **applies** the result as
FU01 `PlannedVisit` atoms (writing `PlannedVisitScheduleSlot`, `PlannedVisitContentRef`, availability + selection
provenance).

### 1.2 What it is NOT

FU05 is **not** the algorithm (that is FU03), **not** the content resolver (FU04), **not** the minute-budget /
capacity (FU06B), **not** a master of accounts / contacts / segments / strategy / journeys / territory (all read
through existing seams), and **not** the **execution/calendar page** — marking a visit done/missed and doing the
presentation (Visit Report) is **FU02**. FU05 owns the *setup* surface and the *orchestration only*.

### 1.3 Target consumer

The rep / field manager, through the **Day/Week SETUP screen** (§9). Behind that screen the engine calls the
in-process seams via DI (no HTTP self-call) and writes FU01 atoms. Downstream, FU02's execution page reads the
`PlannedVisit` rows FU05 produced.

### 1.4 Legacy lineage

Legacy `MicroTarget` (the Daywork microservice unified plan atom: `Employee + Week + Client + Order + Date +
TravellingTime + Category + Criteria`) is **split in vNext** into the **FU01 atom** (storage) + **FU05 engine**
(this FU, the orchestration). Legacy `GetMicroTargetByFilterHandler` built rows *from* `AIAPIs.BestRouteMicroTarget()`
(`Order = bestRoutes.IndexOf+1`) — the route-UPSTREAM sequence FU03→FU05 mirrors exactly. Legacy `TempClient`
staging (a setup draft state) is the direct precedent for **D-PERSISTENCE** (§20). Code is **not migrated** — rules
are re-expressed over the shipped seams (§21).

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Scope | Decision |
|---|---|
| **In-scope** | The **orchestration engine** (select → resolve content+duration → optimize route → frequency-extend → supply-vs-demand → apply → re-plan) · the **Day/Week SETUP UI** (period picker, account+contact selection with segment filter, content preview, generate/preview, supply-vs-demand warning, re-plan) · building the `RouteOptimizationInput` from the selected set + calling FU03 `IRouteOptimizer.Optimize` · calling FU04 `VisitContentSequenceResolver.ResolveAsync` per doctor · calling MOD-0165 `IVisitFrequencyPolicyResolver` for frequency-extend · calling MOD-0164 consent evaluator as a contactability gate · reading MOD-0149 `AccountRelationship` for pharmacies-of-a-clinic · reading MOD-0167 segment membership as the eligible-universe filter · reading MOD-0151 territory as a selection gate · **applying** the generated schedule as FU01 `PlannedVisit` atoms · a **thin `PlanningSession` staging aggregate** (D-PERSISTENCE = C, LOCKED — draft + selected set + generation-state + provenance + `draft`/`generated`/`committed` lifecycle) + its session CRUD + preview/generate/apply/re-plan endpoints + `crm.visit-plan.*` RBAC keys + Ocelot route(s) |
| **Out-of-scope (EXPLICITLY DEFERRED)** | The route **algorithm** (**FU03** `IRouteOptimizer`) · **content resolution** / next-stage / duration arithmetic (**FU04** + **FU06B**) · the `PlannedVisit` **aggregate shape** (**FU01** owns it; FU05 writes instances) · the **execution/calendar page**, mark done/missed, check-in, GPS, actuals, digital detailing, the presentation itself = **Visit Report (FU02)** · any **master CRUD** (account/contact/availability/segment/strategy/journey/territory/capacity/frequency/consent — all read-only) · any **external routing/map API** (FU03 is in-house haversine) · a **production solver** (F-SOLVER, behind FU03's seam) · multi-rep / fleet split |

### 2.2 SoR boundary — owned vs. consumed read-only

| Object | Owner | In this FU |
|---|---|---|
| The **orchestration engine** (selection → generation → apply → re-plan) + the **Day/Week setup UI** | **MOD-0155** | **OPENED** — FU05's core, the only orchestration in the program |
| `PlanningSession` staging aggregate (period + selected set + generation state + provenance + lifecycle) | **MOD-0155** | **OPENED** (D-PERSISTENCE = C, LOCKED) — a THIN staging record; NOT a second schedule source-of-truth (the atoms stay in FU01). `entity_base: EntityBase` |
| `PlannedVisit` + `PlannedVisitScheduleSlot` / `PlannedVisitContentRef` / `PlannedVisitAvailabilitySnapshot` / `PlannedVisitSelectionProvenance` | MOD-0155 (FU01) | **WRITTEN** — FU05 is the producer FU01 anticipated; it creates/updates instances, does NOT change the aggregate shape |
| `IRouteOptimizer` + input/output contract | MOD-0155 (FU03) | **CONSUMED in-process** — FU05 builds `RouteOptimizationInput`, calls `Optimize`, maps `ScheduledVisit`→slot, `unscheduled`→warning. Contract unchanged |
| `VisitContentSequenceResolver` + result | MOD-0155 (FU04) | **CONSUMED in-process** — FU05 calls `ResolveAsync` per doctor; `PromoItemCount`/`NonPromoItemCount`/`VisitDurationMinutes`/`JourneyId`/`StageId` feed FU03 `durationMinutes` + FU01 `PlannedVisitContentRef`. Signature unchanged |
| `CycleCapacity` (`TotalVisitNumber`, `BetweenVisitTimeMinutes`) + `ActivityTimeBudgetCalculator` | MOD-0155 (FU06B/FU06/FU07) | **READ-ONLY** — supply = `TotalVisitNumber`; `BetweenVisitTimeMinutes` → FU03 input. Not mutated |
| `IVisitFrequencyPolicyResolver` + resolve result | MOD-0165 (FU03) | **READ-ONLY provider call** — frequency drives weeks 2..n extend; signature unchanged |
| `IConsentPreferenceEvaluator` + evaluate result | MOD-0164 (FU02) | **READ-ONLY provider call** — contactability gate; `FilterApplied` honoured |
| `ContactAvailability` (per-contact windows) | MOD-0150 | **READ-ONLY** — HARD constraint; passed into FU03 `availabilityWindows` + snapshot onto FU01 |
| `Account` / `AccountRelationship` (clinic↔pharmacy, bidirectional) | MOD-0149 | **READ-ONLY** — "pharmacies of a clinic" query; the link is context, not mutated |
| `Segment` / membership + `StrategyTemplate` play | MOD-0167 | **READ-ONLY** — segment FILTERS the eligible doctor universe; selection is MANUAL; strategy read via FU04, never mutated |
| `TerritoryNode` / coverage / MicroZone geo | MOD-0151 | **READ-ONLY** — territory gate on account selection; geo context |
| `Visit` / VisitReport / check-in / GPS / actuals | MOD-0155 (FU02) | **NOT OPENED** — execution is FU02 |

### 2.3 One-sentence boundary with neighbouring measures

> **FU03** orders + slots a GIVEN set (the algorithm) · **FU04** says which content is next + how long that makes the
> visit · **FU06B** says how many visits fit (capacity) + the between-visit buffer · **FU01** stores one plan atom ·
> **FU02** executes + reports the visit · **FU05 (this FU)** *selects* the set, *calls* FU04 + FU03, *extends* by
> frequency, *warns* on supply-vs-demand, *applies* the result as FU01 atoms, and *drives the setup UI*. Seven
> distinct responsibilities; FU05 opens only the orchestration + setup surface.

### 2.4 Permanent prohibitions (this pack records them)

```text
FU05 re-implements the route heuristic                    ❌  it CALLS IRouteOptimizer (FU03); no copy, no HTTP self-call
FU05 computes visit duration / next content stage          ❌  it CALLS VisitContentSequenceResolver (FU04) + FU06B calc
FU05 changes the PlannedVisit aggregate shape              ❌  it WRITES instances; FU01 owns the shape
FU05 mutates any master (account/contact/segment/…)        ❌  every master is read-only through an existing seam
FU05 owns the execution/calendar page / marks done/missed  ❌  that is FU02 (Visit Report)
supply-vs-demand blocks the planner                        ❌  it is a WARNING (unscheduled[] + capacity delta); planner proceeds
availability is a soft preference in generation            ❌  HARD constraint (honoured by FU03) + explicit manual override
segment auto-selects doctors                               ❌  segment FILTERS the universe; the pick is MANUAL
external routing/map/geocoding API                          ❌  FU03 is in-house haversine only (inherited)
Gateway/RBAC/registry/Mongo hand-edit by this pack          ❌  declared only; integration-agent / operator / F-* steps
```

---

## 3. Owned Objects (all 15 D-questions LOCKED — shape is concrete)

| Layer | Object | Note |
|---|---|---|
| **Orchestration service** | `VisitPlanningEngine` (`Features/VisitPlanning/VisitPlanningEngine.cs`) — the coordinator: builds the selection, calls FU04 per doctor, builds `RouteOptimizationInput`, calls FU03, maps the result, applies frequency-extend, computes supply-vs-demand, writes FU01 atoms | **OPENED** — FU05's core. NOT pure (it orchestrates I/O), but it delegates every calculation to the shipped seams (D8: no new algorithm) |
| **Staging aggregate** | **`PlanningSession`** (aggregate root, `entity_base: EntityBase`) + embedded `PlanningSessionSelection` (chosen accounts/doctors) + `PlanningSessionGenerationState` + `PlanningSessionProvenance` + `IPlanningSessionRepository` (+ Mongo impl, class-map **MANDATORY** — CRM new-aggregate GUID-subtype trap) | **OPENED** (D-PERSISTENCE = C, LOCKED). THIN staging record; `draft`/`generated`/`committed` lifecycle (§12). Legacy `TempClient` precedent. Fields §4.3 |
| **Generation DTOs** | `VisitPlanGenerationRequest` (CyclePeriodId + selected accounts/doctors + options) · `VisitPlanPreview` (proposed scheduled + unscheduled + `SupplyDemandSummary` + per-doctor content/duration) · `VisitPlanApplyResult` (written PlannedVisit ids) · `SupplyDemandSummary` (transient, D-SUPPLY-DEMAND-SHAPE = A) | Preview is transient (dry-run); apply persists FU01 atoms + flips the session to `committed` |
| **Commands / Queries** | `CreatePlanningSessionCommand` · `UpdatePlanningSessionSelectionCommand` · `GeneratePlanPreviewQuery` (dry-run) · `ApplyPlanningSessionCommand` (writes FU01 atoms) · `ReplanPlanningSessionCommand` (subset) · `ListPlanningSessionsQuery` · `GetPlanningSessionByIdQuery` | D-ENDPOINTS = B + session CRUD (D-PERSISTENCE) |
| **Selection helpers** | `EligibleContactSelector` (segment-filter + consent gate + availability read) · `PharmacyExpander` (AccountRelationship → pharmacies-of-a-clinic) · `TerritoryGate` (MOD-0151, WARN not filter — D-TERRITORY-GATE = B) | Read-only assembly over seams; no new master |
| **Frequency-extend** | `FrequencyExtendPlanner` — calls `IVisitFrequencyPolicyResolver` per target, re-runs the route per week (D-FREQUENCY-EXTEND = B) | Read-only resolve; writes only FU01 atoms |
| **Endpoints** | `VisitPlanningController` — `POST …/preview` · `POST …/apply` · `POST …/re-plan` + session CRUD (`GET/POST/PUT …/sessions[/{id}]`) | Preview persists nothing; apply writes FU01 atoms + commits the session |
| **Frontend** | Bespoke Day/Week SETUP console (`Views/CRM/VisitPlanning/**` + JS) + same-origin proxy (D-UI = B) | NOT a Golden CRUD surface; `verify_datatable_page` N/A |
| **Permissions** | `crm.visit-plan.read` · `crm.visit-plan.generate` · `crm.visit-plan.apply` (apply ALSO requires FU01 `crm.planned-visit.manage`) — D-RBAC = B | DEFINITION ONLY; seed/grant NOT in this pack → F-RBAC |
| **Gateway route(s)** | `/api/crm/visit-plan/{everything}` (+ OPTIONS) — **declared** (§15); integration-agent writes `ocelot.json` | F-GW |
| **NOT owned** | route heuristic (FU03) · content/duration (FU04/FU06B) · PlannedVisit shape (FU01) · execution page (FU02) · any master |

---

## 4. Orchestration Flow — the normative surface

> This is the load-bearing section. FU05 is a **coordinator**: it holds **no new algorithm** (D8) — every
> calculation is delegated to a shipped seam. The flow below is the contract; all 15 D-questions that shape it are
> LOCKED (§19/§20) and their choices are stamped inline.

### 4.1 The end-to-end flow (normative)

```text
① PERIOD + WEEKS
   Rep opens a MOD-0165 CyclePeriod (D-PERIOD-MODEL = A, LOCKED — no new period entity); weeks are DERIVED from the
   CyclePeriod calendar via the working-days math (D-WEEK-MODEL = A, LOCKED — no new week entity).

② ACCOUNT SELECTION  (manual, gated)
   Rep picks accounts (hospital/clinic + pharmacy). Gates:
     - Territory gate     → MOD-0151 WARNS on out-of-territory (D-TERRITORY-GATE = B, LOCKED — never a hard filter)
     - Working-time / capacity context → the CyclePeriod-pinned CycleCapacity.TotalVisitNumber (FU06B) — advisory
                                         (D-CAPACITY-SCOPE = A, LOCKED)
   PharmacyExpander: for a chosen clinic, offer its related pharmacies via MOD-0149 AccountRelationship
     (bidirectional; "pharmacies of a clinic"). Selection stays MANUAL — relationship OFFERS, does not auto-add.

③ CONTACT (DOCTOR) SELECTION  (segment-filtered, MANUAL)
   EligibleContactSelector for the chosen accounts:
     - Segment FILTER  → MOD-0167 ISegmentMembershipReader narrows the eligible doctor universe (unknown ≠ member)
     - Consent gate    → MOD-0164 IConsentPreferenceEvaluator (contactability; FilterApplied honoured, blocked=excluded-not-dropped)
     - Availability     → MOD-0150 ContactAvailability read (per-contact windows)
   The rep SELECTS doctors manually from the filtered set. (D-SEGMENT-FILTER — segment filters, pick is manual)

④ CONTENT + DURATION  (per doctor → FU04)
   For each selected doctor, call VisitContentSequenceResolver.ResolveAsync(request):
     request = { SubjectType, SubjectId=contactId, SegmentId?, StrategyTemplateId?, CyclePeriodId?, PriorStageIndex?, EffectiveAt }
     PriorStageIndex ← the doctor's last PlannedVisit PlannedVisitContentRef.StageIndex (content auto-advances).
                       (D-CONTENT-ADVANCE = A, LOCKED — no new per-doctor cursor)
   Result → { JourneyId, StageId, StageIndex, PromoItemCount, NonPromoItemCount, VisitDurationMinutes, Status }.
   VisitDurationMinutes (FU04 via FU06B) becomes the visit's durationMinutes.

⑤ PACK + ROUTE  (whole selected set → FU03)
   Build RouteOptimizationInput:
     visits[]         = { visitId(new), lat, long (HCP coords, MOD-0149/0150), durationMinutes(④), availabilityWindows(③), targetId }
     repWorkingHours  = config placeholder { 09:00–18:00, lunch 13:00–14:00 } (+ optional startLocation)   (HR seam later, MOD-0288)
     period           = { dateFrom, dateTo } of week-1 (then per week for extend)
     betweenVisitMinutes = CycleCapacity.BetweenVisitTimeMinutes (FU06B)
     travelModel      = haversine × roadFactor (FU03 default)
   Call IRouteOptimizer.Optimize(input):
     scheduled[]   → { visitId, assignedDate, startTime, endTime, travelToNextMinutes, sequenceOrder }  (cross-day continuous)
     unscheduled[] → the SUPPLY-vs-DEMAND WARNING, materialised (couldn't fit)

⑥ SUPPLY-vs-DEMAND  (WARNING, not block)
   SupplyDemandSummary (TRANSIENT — recomputed, never persisted; D-SUPPLY-DEMAND-SHAPE = A, LOCKED):
     supply = the CyclePeriod-pinned CycleCapacity.TotalVisitNumber (visits the rep CAN do; D-CAPACITY-SCOPE = A)
     demand = frequency × targets (visits PLANNED)  — plus unscheduled[] from ⑤
   Over-plan surfaces a WARNING; the planner MAY proceed (drop / reschedule / extend period / override). NEVER a hard block.

⑦ FREQUENCY-EXTEND  (weeks 2..n)  (D-FREQUENCY-EXTEND = B, LOCKED)
   FrequencyExtendPlanner: for week-1's set, call IVisitFrequencyPolicyResolver per target → resolved cadence →
   place the visit into weeks 2..n at that cadence, RE-RUNNING ⑤ (the route) PER WEEK so each week stays
   route-continuous. (A one-pass whole-month optimisation is deferred behind FU03's F-SOLVER.)

⑧ PREVIEW → APPLY  (D-SELECTION-FLOW = B, LOCKED — dry-run preview, then apply)
   PREVIEW = ①–⑦ as a dry-run: VisitPlanPreview, persists NOTHING (session stays `draft`/`generated`).
   APPLY   = write FU01 PlannedVisit atoms: one per scheduled visit, filling
             PlannedVisitScheduleSlot (SequenceOrder/SlotStartTime/SlotEndTime from ⑤),
             PlannedVisitContentRef (JourneyId/StageId/StageIndex/PromoItemCount… from ④, ContentSource=strategy),
             PlannedVisitAvailabilitySnapshot (③), PlannedVisitSelectionProvenance (segment/campaign/strategy),
             PlannedDurationMinutes (④). Source = route-plan/campaign.
             ATOMIC: a transaction guarded by SupportsTransactionsAsync + compensation (D-APPLY-ATOMICITY = C, LOCKED —
             the CRM standalone-Mongo pattern; no half-applied plan; works on dev standalone).
             The PlanningSession flips `generated` → `committed` (D-PERSISTENCE = C, LOCKED).

⑨ RE-PLAN  (manual)  (D-REPLAN = A, LOCKED — in-place update)
   Doctor missed / rep says "I can go day X": re-run ⑤ (+ ⑦) for the affected subset, updating ONLY the affected
   PlannedVisit atoms IN PLACE (no new revision). The rest are untouched.
```

### 4.2 D8 — FU05 holds no new algorithm

Every number FU05 shows or writes comes from a **shipped seam**: order/slots from **FU03**, content/duration from
**FU04**/**FU06B**, cadence from **MOD-0165**, capacity from **FU06B**, contactability from **MOD-0164**,
availability from **MOD-0150**, pharmacies from **MOD-0149**, eligibility from **MOD-0167**, territory from
**MOD-0151**. FU05's own logic is **assembly + selection state + apply** — not scoring, not routing, not duration
arithmetic. This keeps the FU03/FU04/FU05 boundaries clean and lets each seam's implementation swap without touching
the engine.

### 4.3 `PlanningSession` — the thin staging aggregate (D-PERSISTENCE = C, LOCKED)

> `entity_base: EntityBase` (tenant-owned; `TenantId` server-resolved from the JWT claim, never in the payload;
> soft-delete `IsDeleted`/`DeletedAt`; `Version` = concurrency token). This is a **THIN** staging record — it holds
> the *selection + generation state + provenance*, **NOT the schedule itself** (the schedule lives as FU01
> `PlannedVisit` atoms after apply). No second source-of-truth for the plan. Legacy `TempClient` precedent.

| # | Field | Type | Note |
|---|---|---|---|
| 1 | `Id` (PlanningSessionId) | Guid | `EntityBase` |
| 2 | `TenantId` | Guid | Server-resolved; cross-tenant access → 404 |
| 3 | `CyclePeriodId` | Guid | The MOD-0165 CyclePeriod this session plans (D-PERIOD-MODEL = A). Weeks are derived, not stored (D-WEEK-MODEL = A) |
| 4 | `ResourceId` | string | The rep this plan is for (FU01 `Resource.ResourceId` shape — string, sahte-FK yasağı; MOD-0288 owns the master). Single-rep (D-MULTI-REP = A) |
| 5 | `Status` | string | `PlanningSessionStatus`: `draft` · `generated` · `committed` · `archived` (§12; no reverse) |
| 6 | `Selection` | `PlanningSessionSelection` (embedded) | The manual selection (§4.3a) |
| 7 | `GenerationState` | `PlanningSessionGenerationState` (embedded) | Last generation metadata (§4.3b) — NOT the scheduled slots |
| 8 | `Provenance` | `PlanningSessionProvenance` (embedded) | Segment/campaign/strategy origin snapshot (never authored FKs; consent `MatchedConsentId` precedent) |
| 9 | `CommittedPlannedVisitIds` | Guid[] | The FU01 atom ids written at apply — the link to the real schedule (provenance only; the atoms are the truth) |
| 10 | `CreatedBy`/`UpdatedBy`/`CreatedAt`/`UpdatedAt`/`IsDeleted`/`DeletedAt`/`Version` | — | `EntityBase` audit |

**4.3a `PlanningSessionSelection` (embedded):** `SelectedAccountIds` (Guid[]) · `SelectedPharmacyIds` (Guid[]) ·
`SelectedContacts` (`{ ContactId, AccountId?, AccountContactLinkId? }[]` — the manual doctor picks, segment-filtered) ·
`SegmentId` (Guid? — the filter applied) · `CampaignId` (Guid?). **Selection is MANUAL; segment only filtered the
universe.**

**4.3b `PlanningSessionGenerationState` (embedded):** `LastGeneratedAt` (DateTimeOffset?) · `ScheduledCount` (int) ·
`UnscheduledCount` (int) · `SupplyDemandStatus` (string — a coarse `ok`/`over-planned` flag; the full
`SupplyDemandSummary` is **transient**, D-SUPPLY-DEMAND-SHAPE = A, recomputed on preview, never persisted here).

> **MongoDB indexes:** `TenantId + CyclePeriodId + ResourceId` (the session-for-rep-in-period lookup) ·
> `TenantId + Status`. **No `$ne` partial-index** (use `Filter.Type`/`$lt`). Embedded types stay class-map-registered
> (CRM new-aggregate GUID-subtype trap) and carry no `DateTimeOffset` co-sort (parallel-arrays 500).

---

## 5. Repo Scope (final shape — all D-questions LOCKED)

**Backend — `services/Diten.CrmService/`:**

```text
src/Diten.CrmService.Application/Features/VisitPlanning/
├── VisitPlanningEngine.cs                    (NEW — the orchestration coordinator)
├── VisitPlanningModels.cs                    (NEW — generation/preview/apply DTOs + SupplyDemandSummary)
├── EligibleContactSelector.cs                (NEW — segment-filter + consent gate + availability read)
├── PharmacyExpander.cs                       (NEW — MOD-0149 AccountRelationship → pharmacies-of-a-clinic)
├── TerritoryGate.cs                          (NEW — MOD-0151 territory gate)
├── FrequencyExtendPlanner.cs                 (NEW — MOD-0165 resolve → weeks 2..n)
├── VisitPlanningPermissions.cs               (NEW — crm.visit-plan.read/.generate/.apply constants; DEFINITION ONLY)
├── Commands/{CreatePlanningSession,UpdatePlanningSessionSelection,ApplyPlanningSession,ReplanPlanningSession}Command.cs (NEW)
├── Queries/{GeneratePlanPreview,ListPlanningSessions,GetPlanningSessionById}Query.cs                                    (NEW)
└── Handlers/{CommandHandlers,QueryHandlers}/**   (NEW — preview(dry-run) / apply(writes FU01 atoms) / re-plan / session CRUD)
src/Diten.CrmService.Domain/Entities/PlanningSession.cs   (NEW — aggregate + embedded Selection/GenerationState/Provenance + PlanningSessionStatus, §4.3)
src/Diten.CrmService.Domain/Repositories/IPlanningSessionRepository.cs   (NEW)
src/Diten.CrmService.Infrastructure/Persistence/PlanningSessionRepository.cs   (NEW)
src/Diten.CrmService.Infrastructure/Persistence/DependencyInjection.cs   (CHANGES — PlanningSession class-map + indexes §4.3)
src/Diten.CrmService.Application/DependencyInjection.cs   (CHANGES — register the engine + helpers)
src/Diten.CrmService.Api/Controllers/CRM/VisitPlanningController.cs   (NEW — preview / apply / re-plan + session CRUD)
src/Diten.CrmService.Api/Models/CRM/VisitPlanningRequests.cs         (NEW — request binding)
tests/Diten.CrmService.Application.Tests/VisitPlanning/**            (NEW)

── Frontend: frontend/Diten.Web/ ──  (D-UI = B, bespoke tenant-shell console)
Controllers/CrmVisitPlanningController.cs      (NEW — same-origin proxy)
Views/CRM/VisitPlanning/**                     (NEW — the bespoke Day/Week SETUP screen; NOT a Golden CRUD surface)
wwwroot/assets/js/CRM/VisitPlanning/**         (NEW — selection + preview/generate + re-plan)
Resources/Views/CRM/VisitPlanning/*.{ar,en,es,fr,ru,tr,zh}.resx
Views/Shared/_LayoutTenantShell.cshtml         (permission-guarded <li> for the setup page)

── Gateway (DECLARED — integration-agent wires it, pack does not write) ──
gateway/**/ocelot.json                         (DECLARE: /api/crm/visit-plan/{everything} — §15)

── This pack (the only write surface valid today) ──
execution/domains/commercial-suite/module-packs/MOD-0155-FU05-microtarget-visit-planning-engine.md
```

---

## 6. Protected Paths

| Path | Reason |
|---|---|
| `.antigravity/**` | Global engineering system |
| `services/Diten.CrmService/**/Features/RouteOptimization/**` | FU03 seam — CONSUMED, not modified |
| `services/Diten.CrmService/**/Features/VisitContentSequence/**` | FU04 resolver — CONSUMED, not modified |
| `services/Diten.CrmService/**/Features/CycleCapacity/**` | FU06/FU06B/FU07 — capacity/buffer READ as input, not modified |
| `services/Diten.CrmService/**/Domain/Entities/PlannedVisit.cs` (+ `Features/PlannedVisit/**`) | FU01 aggregate — FU05 WRITES instances via FU01's own commands; the shape is not changed |
| `services/Diten.CrmService/**/Features/{VisitFrequencyPolicy,ConsentPreference,Segmentation,StrategyTemplate,Territory,Account,Contact}/**` | Consumed read-only via existing seams; git diff ∅ |
| `services/Diten.Platform/**`, other domain services (`Diten.MdmService/**`, `Diten.HcmService/**`, …) | Out of domain |
| `gateway/**/ocelot.json` | integration-agent owned; `/api/crm/visit-plan/*` **declared** (§15) → F-GW |
| RBAC catalog / seed / `rolePermissions` | **F-RBAC** — `crm.visit-plan.*` keys declared (§14), not seeded here |
| `execution/registries/**` | **F-REG** — registry writes outside pack authority |
| `frontend/Diten.Web/Views/Shared/_Layout*.cshtml`, `Archive/**` | FROZEN |
| Mongo hand-edit | Forbidden (GUID subtype trap breaks all logins) |

---

## 7. Dependencies

| Dependency | Direction | Status | Note |
|---|---|---|---|
| **MOD-0155-FU01** PlannedVisit atom | FU05 WRITES instances | ready-for-dev | fills Slot/ContentRef/Availability/Selection provenance; shape unchanged |
| **MOD-0155-FU03** `IRouteOptimizer` | in-process CONSUME | **BUILT** | `Optimize(RouteOptimizationInput)`; unscheduled = supply-vs-demand warning |
| **MOD-0155-FU04** `VisitContentSequenceResolver` | in-process CONSUME | **BUILT** | `ResolveAsync`; content + duration per doctor |
| **MOD-0155-FU06B** capacity/buffer + calculator | read-only | ready-for-dev | supply = `TotalVisitNumber`; `BetweenVisitTimeMinutes` → FU03 |
| **MOD-0165-FU03** `IVisitFrequencyPolicyResolver` | read-only provider | **SHIPPED** | frequency-extend weeks 2..n; signature unchanged |
| **MOD-0164-FU02** `IConsentPreferenceEvaluator` | read-only provider | **SHIPPED** | contactability gate; FilterApplied honoured |
| **MOD-0150** ContactAvailability | read-only | shipped | HARD availability windows |
| **MOD-0149** Account + AccountRelationship | read-only | shipped | pharmacies-of-a-clinic (bidirectional) |
| **MOD-0167** Segment membership + StrategyTemplate | read-only | shipped | segment filters eligible universe; strategy via FU04 |
| **MOD-0151** Territory / coverage | read-only | shipped | territory gate on account selection |
| **MOD-0288** Person/Position master | additive future working-hours source | reserved/planned | v1 config placeholder; NO HR integration (D-WORKINGHOURS) |
| **MOD-0018** RBAC | consumption | partial | new `crm.visit-plan.*` keys; catalog/grant not seeded here → F-RBAC |

---

## 8. Runtime Constraints

- **Coordinator, not pure.** `VisitPlanningEngine` performs I/O (it reads seams, calls FU03/FU04, writes FU01), but
  it delegates **every calculation** to a shipped seam — no new heuristic, no duration arithmetic, no scoring (D8).
- **In-process, no HTTP self-call.** FU03/FU04/FU06B/MOD-0165/0164 are all called via **DI**, never back through the
  Gateway (the resolver-seam rule).
- **Preview persists nothing.** The dry-run preview (§4.1 ⑧) is transient; only APPLY writes FU01 atoms and flips the
  `PlanningSession` to `committed` (D-PERSISTENCE = C).
- **Tenant.** Every seam reads tenant-scoped via `ITenantContext`; FU05 adds no cross-tenant path.
- **Availability HARD, override explicit.** The engine passes availability windows to FU03 as HARD constraints; a
  planner override is an explicit action, never the engine silently violating a window.
- **Supply-vs-demand is a warning.** Over-plan never throws / never 500s; it is data the planner resolves.
- **Atomic apply.** Writing many `PlannedVisit` atoms is all-or-nothing via a transaction (D-APPLY-ATOMICITY = C);
  the CRM standalone-Mongo transaction-fallback rule applies (guard `StartTransaction` with `SupportsTransactionsAsync`
  + compensation), else dev standalone 500s. No half-applied plan.
- **Date/time traps.** Slots are `"HH:mm"` string + `DateOnly` (inherited from FU01/FU03) — never `DateTimeOffset`
  co-sorted (CRM parallel-arrays 500).

---

## 9. Layout & Shell Contract — the Day/Week SETUP screen (D-UI = B, LOCKED)

FU05 owns the **selection + generation** surface (`shell: tenant` ⇒ `Layout = "_LayoutTenantShell"`). It is **NOT**
the execution/calendar page (FU02). **LOCKED: a bespoke tenant-shell console** (`golden_reference: n/a`), not a Golden
DataTable CRUD page — the surface is a generation workflow, not row CRUD, so `verify_datatable_page` is **N/A**. Panels:

```text
┌ Period picker ─────────────────────────────────────────────────────────────┐
│  MOD-0165 CyclePeriod + weeks derived from its calendar (no new entity)     │
├ Account selection ─────────────────────────────────────────────────────────┤
│  clinic/hospital list (territory-gated) + "related pharmacies" offer        │
│  (MOD-0149 AccountRelationship); manual multi-select                        │
├ Contact (doctor) selection ────────────────────────────────────────────────┤
│  segment filter (eligible universe) + consent badge + availability hint;    │
│  MANUAL multi-select                                                        │
├ Content preview (per doctor) ──────────────────────────────────────────────┤
│  next stage (FU04) + resolved visit duration; read-only preview            │
├ Generate / Preview ────────────────────────────────────────────────────────┤
│  run ⑤–⑦ as dry-run → Day/Week grid of proposed slots (route-ordered)       │
├ Supply-vs-demand warning ──────────────────────────────────────────────────┤
│  capacity vs demand + unscheduled[] (couldn't fit) — WARNING, proceed OK    │
├ Apply / Re-plan ───────────────────────────────────────────────────────────┤
│  commit → write FU01 atoms; re-plan a subset (doctor missed / "day X")      │
└─────────────────────────────────────────────────────────────────────────────┘
```

The saved `PlanningSession` rows (D-PERSISTENCE = C) are listed inside this bespoke console (a simple session picker /
"my draft plans" list) — **not** a separate Golden Compact page. The whole surface stays bespoke; `verify_datatable_page`
is N/A.

---

## 10. Backend File Convention

New feature folder `Features/VisitPlanning/`. `VisitPlanningEngine` is a sealed coordinator (no Command/Query suffix —
it is a service, the resolver/engine precedent). Preview/apply/re-plan + session CRUD follow standard CQRS handler
naming (`Queries`/`Commands` + `Handlers`). The `PlanningSession` aggregate + repository follow the FU01 Golden layout
(class-map registration MANDATORY — the CRM new-aggregate GUID-subtype trap). DI registration added next to the
existing seam registrations.

---

## 11. Frontend File Contract (D-UI = B, LOCKED)

Bespoke console: a new `Views/CRM/VisitPlanning/**` screen + `wwwroot/assets/js/CRM/VisitPlanning/**`
+ same-origin proxy `Controllers/CrmVisitPlanningController.cs` + 7-language RESX + one permission-guarded `<li>` in
`_LayoutTenantShell`. The **Compact offcanvas/quickview partials are N/A** (not a Golden CRUD surface). The saved
`PlanningSession` list lives inside this console (a session picker), not a separate Golden page.

---

## 12. Lifecycle / State (D-PERSISTENCE = C, LOCKED)

The `PlanningSession` staging aggregate carries a **`PlanningSessionStatus`** state machine:

```text
draft ──(first generation)──► generated ──(apply)──► committed ──(archive)──► archived
```

- **No reverse transitions.** `committed` never returns to `draft`; re-plan updates the FU01 atoms in place (§4.1 ⑨,
  D-REPLAN = A) without reopening the session.
- **`draft`** — selection is being edited; no generation yet.
- **`generated`** — a preview has run (still persists no atoms; the preview `SupplyDemandSummary` is transient).
- **`committed`** — apply wrote the FU01 `PlannedVisit` atoms (ids captured in `CommittedPlannedVisitIds`).
- **`archived`** — session retired; frees nothing on FU01 (the atoms live on).
- The real schedule's lifecycle is FU01's `PlannedVisit.PlanStatus` — the session status is only the *staging* state.

---

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Over-plan (demand > capacity, or visits couldn't fit) | `SupplyDemandSummary` + `unscheduled[]` populated; **WARNING**, never a 500; planner may still apply |
| Doctor with no resolvable content (FU04 `no-strategy`/`end-of-journey`) | Visit still schedulable with duration = report-only; FU04 status surfaced, never a crash |
| Doctor unavailable on every candidate day | FU03 returns `no_feasible_availability_window` in `unscheduled[]`; other visits still scheduled |
| Consent-blocked doctor selected | Excluded-not-dropped with a reason (MOD-0164 `FilterApplied`); planner sees why |
| Pharmacy with no clinic relationship | Still selectable directly (relationship OFFERS, is not a precondition — FU01 D9) |
| Preview then no apply | Nothing persisted (dry-run) |
| Apply writes many atoms, one fails | All-or-nothing (transaction + compensation); no half-applied plan; session NOT flipped to `committed` (D-APPLY-ATOMICITY = C) |
| Frequency-extend across weeks | Weeks 2..n replicate at resolved cadence; cross-day continuity preserved per week |
| Re-plan a subset | Only the affected atoms change; the rest untouched (D-REPLAN) |
| Dev standalone Mongo | `StartTransaction` guarded by `SupportsTransactionsAsync` + compensation (no 500) |

---

## 14. Authorization Convention

The **engine has no RBAC of its own** — it is called in-process by the setup screen's handlers, which authorize. The
**endpoints** (§15) are `[Authorize]` under the tenant shell with a **new split of three keys (D-RBAC = B, LOCKED):**

| Key | Guards |
|---|---|
| `crm.visit-plan.read` | open the setup console, list sessions, read a session |
| `crm.visit-plan.generate` | run a preview (dry-run generation) |
| `crm.visit-plan.apply` | commit → write FU01 atoms + flip session to `committed`. **ALSO requires FU01 `crm.planned-visit.manage`** (the engine calls FU01's command path) |

Splitting `apply` from `read`/`generate` lets a manager preview without commit rights. Catalog rows + grants are
**not seeded by this pack** (F-RBAC). Actor: `tenant_user`.

---

## 15. Gateway / API Routing Decision

**New route(s) required (D-ENDPOINTS = B, LOCKED)** for the setup endpoints, **declared here** for the
integration-agent. One wildcard pair covers preview + apply + re-plan + session CRUD:

```text
POST/GET/PUT  /api/crm/visit-plan/{everything}   →  Diten.CrmService  (VisitPlanningController)
OPTIONS       /api/crm/visit-plan/{everything}   →  (CORS/preflight pair)

concrete endpoints under the wildcard:
  POST /api/crm/visit-plan/preview              (dry-run; persists nothing)          [crm.visit-plan.generate]
  POST /api/crm/visit-plan/apply                (writes FU01 atoms; commits session) [crm.visit-plan.apply + crm.planned-visit.manage]
  POST /api/crm/visit-plan/re-plan              (in-place subset update)             [crm.visit-plan.apply + crm.planned-visit.manage]
  GET  /api/crm/visit-plan/sessions[/{id}]      (list / read staging sessions)       [crm.visit-plan.read]
  POST/PUT /api/crm/visit-plan/sessions[/{id}]  (create / edit selection)            [crm.visit-plan.generate]
```

`ocelot.json` is **integration-agent owned**; this pack **does not** write it (F-GW). No catch-all covers
`/api/crm/visit-plan/*`, so the pair must be added explicitly. Until then the endpoints return the 404 + `{}`
missing-route signature. Any bodiless 204 responses must use the `IsBodilessStatus` proxy guard.

---

## 16. Acceptance Criteria (all 15 D-questions LOCKED — finalised)

**AC-FLOW — the orchestration binds the seams**

- [ ] **AC-FLOW-1** Given a selected set, the engine calls FU04 `ResolveAsync` per doctor and FU03 `Optimize` once,
      and maps `ScheduledVisit`→slot + `unscheduled`→warning (structural: the seams are injected + called, not
      re-implemented).
- [ ] **AC-FLOW-2** No route heuristic / duration arithmetic / next-stage logic exists in `Features/VisitPlanning/**`
      (`Haversine`/`ComputeDuration`/`NextStage` absent — delegated to FU03/FU04/FU06B).
- [ ] **AC-FLOW-3** `PriorStageIndex` fed to FU04 comes from the doctor's last `PlannedVisit.PlannedVisitContentRef.StageIndex`
      (content auto-advances) (D-CONTENT-ADVANCE).

**AC-SELECT — segment filters, selection is manual**

- [ ] **AC-SELECT-1** Segment membership NARROWS the eligible doctor universe; a non-member is not offered; the pick
      is an explicit selection (D-SEGMENT-FILTER).
- [ ] **AC-SELECT-2** A consent-blocked doctor is excluded-not-dropped with a reason (MOD-0164 FilterApplied).
- [ ] **AC-SELECT-3** For a chosen clinic, its related pharmacies (MOD-0149 AccountRelationship, bidirectional) are
      OFFERED; a pharmacy with no relationship is still directly selectable.

**AC-WARN — supply-vs-demand is a warning**

- [ ] **AC-WARN-1** Over-plan produces `SupplyDemandSummary` + `unscheduled[]` and **no 500**; apply is still allowed.

**AC-APPLY — preview vs apply**

- [ ] **AC-APPLY-1** Preview persists nothing (no PlannedVisit / staging write).
- [ ] **AC-APPLY-2** Apply writes one FU01 `PlannedVisit` per scheduled visit with Slot/ContentRef/Availability/Selection
      provenance filled; Source = route-plan/campaign.
- [ ] **AC-APPLY-3** Apply is all-or-nothing via a transaction guarded by `SupportsTransactionsAsync` + compensation;
      a mid-apply failure leaves no half-plan and the session is NOT flipped to `committed`; works on dev standalone
      Mongo (D-APPLY-ATOMICITY = C).

**AC-EXTEND / AC-REPLAN**

- [ ] **AC-EXTEND-1** Week-1 set extends to weeks 2..n at the MOD-0165 per-target resolved cadence, re-running the
      route per week (D-FREQUENCY-EXTEND = B).
- [ ] **AC-REPLAN-1** Re-plan of a subset updates only the affected atoms IN PLACE; no new revision; the session is
      not reopened (D-REPLAN = A).

**AC-SESSION — the PlanningSession staging aggregate (D-PERSISTENCE = C)**

- [ ] **AC-SESSION-1** A `PlanningSession` round-trips (write→read) with `Selection`/`GenerationState`/`Provenance`
      preserved; class-map registered (no GUID-subtype/empty-query trap).
- [ ] **AC-SESSION-2** The status machine is `draft → generated → committed → archived` with NO reverse transition;
      `SupplyDemandSummary` is NOT stored on the session (transient, D-SUPPLY-DEMAND-SHAPE = A).
- [ ] **AC-SESSION-3** `CommittedPlannedVisitIds` links to the FU01 atoms; the session holds no schedule of its own
      (no second source-of-truth).

**AC-BOUNDARY**

- [ ] **AC-BOUNDARY-1** `Features/{RouteOptimization,VisitContentSequence,CycleCapacity,PlannedVisit,VisitFrequencyPolicy,
      ConsentPreference}/**` → git diff ∅ (consumed, not modified; FU01 written via its own command path).
- [ ] **AC-BOUNDARY-2** No master (account/contact/segment/strategy/journey/territory) is mutated.

**AC-ENDPOINT / AC-UI (D-ENDPOINTS = B, D-UI = B, D-RBAC = B)**

- [ ] **AC-ENDPOINT-1** `preview` returns a `VisitPlanPreview` and persists nothing; `apply` writes atoms + commits
      the session; `re-plan` updates a subset; session CRUD reads/edits the staging record.
- [ ] **AC-ENDPOINT-2** `apply`/`re-plan` require BOTH `crm.visit-plan.apply` AND FU01 `crm.planned-visit.manage`;
      `preview` requires `crm.visit-plan.generate`; reads require `crm.visit-plan.read`; before the Ocelot route is
      added each returns the 404 + `{}` missing-route signature.
- [ ] **AC-UI-1** The Day/Week setup page is a bespoke tenant-shell console (`Layout = "_LayoutTenantShell"`);
      `verify_datatable_page` is N/A (not a Golden CRUD surface).

---

## 17. Test Expectations

- **Backend orchestration tests** (`tests/…/VisitPlanning/`): the engine calls FU04/FU03 with the right inputs
  (test-doubles for the seams), maps results correctly, computes supply-vs-demand, frequency-extends, and applies FU01
  atoms; preview persists nothing; apply atomicity; re-plan subset isolation.
- **Boundary tests:** no new heuristic/duration/next-stage symbol in the feature; consumed features git diff ∅.
- **Persistence tests (D-PERSISTENCE = C):** `PlanningSession` round-trip + class-map (GUID subtype) + status-machine
  guards (no reverse transition; `SupplyDemandSummary` not persisted) + tenant isolation.
- **Build:** `Diten.CrmService` + `frontend/Diten.Web` → 0 errors.
- **Verifier:** `verify_module_id --check-id MOD-0155-FU05` exit 0; `verify_datatable_page` **N/A** (bespoke console,
  D-UI = B — not a Golden CRUD surface).
- **Smoke (user):** open the setup screen → pick a period, accounts, doctors → preview a Day/Week grid → see
  supply-vs-demand → apply → PlannedVisit rows appear (FU02 execution page reads them) → re-plan one doctor.

> **Orchestrator self-report is not trusted** — test counts are read from an actual run (MOD-0162-FU04 lesson).

---

## 18. Ready-for-dev Checklist (NOT satisfied — this is a DRAFT)

- [x] DCP-002 identity gate **PASS** (exit 0, 2026-08-29) — command + output in §0.1
- [x] Module registry checked: `MOD-0155` canonical, `FU05` reserved for MicroTarget across siblings
- [x] Orchestration flow specified normatively (§4): select → content/duration (FU04) → route (FU03) → supply/demand →
      frequency-extend (MOD-0165) → preview/apply (FU01) → re-plan
- [x] Seam-consumption plan grounded against the BUILT seams (FU03 `Optimize`, FU04 `ResolveAsync`, MOD-0165 resolver)
- [x] Locked context encoded in the decision log (§19)
- [x] **All 15 D-questions SETTLED and LOCKED** (2026-08-29, user + Control Tower) — §20 is now a resolution table
- [x] **D-PERSISTENCE = C** → `entity_base: EntityBase`; thin `PlanningSession` staging aggregate (fields §4.3)
- [x] **D-UI = B** → `golden_reference: n/a`; bespoke tenant-shell console; `verify_datatable_page` N/A
- [x] **D-SELECTION-FLOW / D-FREQUENCY-EXTEND / D-REPLAN / D-TERRITORY-GATE / D-CONTENT-ADVANCE / D-APPLY-ATOMICITY /
      D-PERIOD-MODEL / D-WEEK-MODEL / D-CAPACITY-SCOPE / D-SUPPLY-DEMAND-SHAPE / D-MULTI-REP** all LOCKED (§20)
- [x] **D-RBAC = B** (`crm.visit-plan.read/.generate/.apply` + FU01 `crm.planned-visit.manage` on apply) ·
      **D-ENDPOINTS = B** (preview/apply/re-plan + session CRUD; Ocelot route declared §15) — keys/routes concrete
- [ ] **SEPARATE flip decision:** `status: ready-for-dev` + `runtime_code_allowed: true` + `runtime_code_scope` —
      NOT taken by this pack; Control Tower performs it after reviewing this update

---

## 19. Implementation Notes / Decision Log (LOCKED — do not reopen)

> **19.a Program context** — the user-approved decisions from the MOD-0155 roadmap
> ([[mod0155-visit-route-planning-program]]), encoded not reopened.

| # | Decision | Rationale |
|---|---|---|
| **D-HOME** | Home = MOD-0155 (`Diten.CrmService`) | MicroTarget is the "MicroTarget" clause of the SoR line (§0.2) |
| **D-SPLIT** | Legacy `MicroTarget` splits into FU01 (atom) + FU05 (engine) | vNext separation of storage vs orchestration (§1.4) |
| **D-ROUTE-UPSTREAM** | FU03 is UPSTREAM; FU05 consumes its order/slots | CRM2-proven (`Order = bestRoutes.IndexOf+1`); route is standalone, MicroTarget consumes it |
| **D-AVAILABILITY** | Per-contact HARD constraint + explicit manual override; enforced by the engine, honoured by FU03 | user-approved; a doctor's availability is real; engine never self-violates |
| **D-SUPPLY-DEMAND** | Supply-vs-demand = WARNING, planner may proceed; `unscheduled[]` IS the warning materialised | over-plan surfaced, never hidden; hard-block would hide the shortfall |
| **D-SEGMENT-MANUAL** | Segment FILTERS the eligible universe; selection is MANUAL | user-approved; the pick is a human's |
| **D-WORKINGHOURS** | Rep working hours = config placeholder (09:00–18:00, lunch 13:00–14:00); HR/MOD-0288 additive later | no dead HR dependency now; FU03 already takes a config placeholder |
| **D-CONTENT-DEFAULT** | Content journey/stage = strategy-derived default + manual override (FU04 resolves; FU01 stores) | derive-default + override family (same as availability + duration) |
| **D-PHARMACY** | Clinic↔pharmacy = MOD-0149 AccountRelationship (bidirectional); relationship OFFERS, does not auto-add; not a precondition | verified P0.3; pharmacy is a first-class target (FU01 D9) |
| **D-DURATION** | Duration = f(content) via FU04 → FU06B; FU05 never computes it | FU04/FU06B own the arithmetic; D8 no-engine |
| **D-NO-NEW-MASTER** | FU05 defines no new master; every master is read via an existing seam | assembly over shipped modules |
| **D-NOT-FU02** | FU05 owns the SETUP surface only; execution/calendar + mark done/missed + Visit Report = FU02 | two separate UI surfaces (setup vs execution) |
| **D-NO-EXTERNAL-ROUTING** | No external routing/map/geocoding API (inherited from FU03 in-house haversine) | pharma HCP-location privacy + cost |

> **19.b The 15 FU05 D-questions — ALL LOCKED (2026-08-29, user + Control Tower).** Every recommended default was
> accepted. These are now part of the design and are not reopened. Full text (question + rejected options) in §20.

| # | Locked decision | Concrete effect |
|---|---|---|
| **D-PERSISTENCE = C** | Thin `PlanningSession` staging aggregate (draft + selected set + generation state + provenance + `draft/generated/committed`); atoms stay in FU01 | `entity_base: EntityBase`; §4.3 field set; §12 status machine |
| **D-UI = B** | Bespoke tenant-shell selection+generation console | `golden_reference: n/a`; `verify_datatable_page` N/A; §9 panels |
| **D-SELECTION-FLOW = B** | Preview (dry-run) → apply | preview persists nothing; §4.1 ⑧ |
| **D-FREQUENCY-EXTEND = B** | Per-target cadence via MOD-0165, re-run route per week | `FrequencyExtendPlanner`; §4.1 ⑦ |
| **D-REPLAN = A** | In-place update of affected atoms; no revision | §4.1 ⑨; AC-REPLAN-1 |
| **D-PERIOD-MODEL = A** | Reuse MOD-0165 CyclePeriod; no new period entity | `PlanningSession.CyclePeriodId`; §4.1 ① |
| **D-WEEK-MODEL = A** | Derive weeks from the CyclePeriod calendar; no new week entity | working-days math; no week rows |
| **D-TERRITORY-GATE = B** | Warn (not hard-filter) on out-of-territory | `TerritoryGate`; §4.1 ② |
| **D-CONTENT-ADVANCE = A** | `PriorStageIndex` from the doctor's last PlannedVisit `StageIndex` | §4.1 ④; no new cursor |
| **D-APPLY-ATOMICITY = C** | Transaction + `SupportsTransactionsAsync` fallback + compensation | §8; AC-APPLY-3 |
| **D-SUPPLY-DEMAND-SHAPE = A** | Transient summary, recomputed, never persisted | `SupplyDemandSummary` transient; §4.3b `SupplyDemandStatus` coarse flag only |
| **D-RBAC = B** | Split `crm.visit-plan.read/.generate/.apply`; apply also needs FU01 `crm.planned-visit.manage` | §14; F-RBAC seed |
| **D-ENDPOINTS = B** | preview + apply + re-plan + session CRUD; Ocelot route declared | §15; §3 |
| **D-CAPACITY-SCOPE = A** | CyclePeriod-pinned CycleCapacity for supply | §4.1 ②/⑥ |
| **D-MULTI-REP = A** | Single-rep v1; multi-vehicle VRP behind FU03 F-SOLVER | `PlanningSession.ResourceId` single rep |

---

## 20. D-Question Resolutions — ALL LOCKED (2026-08-29, user + Control Tower)

> **All 15 D-questions are SETTLED.** Every recommended default was accepted. The table records each with its
> rejected alternatives; the design (§3/§4/§9/§12/§14/§15/§16) is stamped accordingly. Nothing here is still open.

| ID | ✅ Resolution | Rejected alternatives |
|---|---|---|
| **D-PERSISTENCE** | **C — lean staging.** Thin `PlanningSession` aggregate (draft + selected set + generation state + provenance + `draft/generated/committed`); the real plan atoms stay in FU01 `PlannedVisit`. `entity_base: EntityBase`. Legacy `TempClient` precedent — NOT a second schedule source-of-truth. | A) transient-only (loses the cross-session draft the setup UI needs) · B) full aggregate holding the schedule (a second source of truth for the plan) |
| **D-UI** | **B — bespoke tenant-shell console** (period picker, account/contact selection w/ segment filter, content preview, generate/preview, supply-vs-demand warning, re-plan). `golden_reference: n/a`; `verify_datatable_page` N/A. Saved sessions listed inside the console. | A) Golden Compact CRUD (it is a generation workflow, not row CRUD) · C) bespoke + a separate Golden sessions list (unneeded second surface) |
| **D-SELECTION-FLOW** | **B — preview (dry-run) → apply.** Preview persists nothing; apply writes FU01 atoms. FU03/FU04 preview precedent. | A) one-shot generate+apply (no chance to review the Day/Week grid + warnings) |
| **D-FREQUENCY-EXTEND** | **B — per-target cadence, per-week route.** `IVisitFrequencyPolicyResolver` per target → place into weeks 2..n → re-run FU03 per week for continuity. | A) verbatim week-1 replica (ignores per-target cadence) · C) one-pass whole-month optimise (larger VRP — F-SOLVER) |
| **D-REPLAN** | **A — in-place update** of the affected `PlannedVisit` atoms; session not reopened. | B) new revision / supersede (heavier audit model) · C) both/config (premature) |
| **D-PERIOD-MODEL** | **A — reuse MOD-0165 CyclePeriod;** no new period entity. Keeps supply/demand + frequency coherent. | B) rep-chosen month (diverges from capacity/frequency scope) · C) either (ambiguous) |
| **D-WEEK-MODEL** | **A — derive weeks from the CyclePeriod calendar** (working-days math); no new week entity. | B) explicit week rows (redundant entity) · C) ISO weeks (ignores the working calendar) |
| **D-TERRITORY-GATE** | **B — warn**, not hard-filter, on out-of-territory account selection. Matches the excluded-not-dropped pattern. | A) hard filter (hides valid targets) · C) config (premature) |
| **D-CONTENT-ADVANCE** | **A — `PriorStageIndex` from the doctor's last PlannedVisit `PlannedVisitContentRef.StageIndex`,** fed to FU04. No new cursor. | B) per-doctor cursor on the session (duplicate state) · C) explicit rep input (manual, error-prone) |
| **D-APPLY-ATOMICITY** | **C — transaction with standalone fallback.** `StartTransaction` guarded by `SupportsTransactionsAsync` + compensation. All-or-nothing; dev standalone works. | A) single transaction (500s on dev standalone) · B) idempotent-per-visit (resumable but no atomic guarantee) |
| **D-SUPPLY-DEMAND-SHAPE** | **A — transient summary,** recomputed on preview, never persisted (only a coarse `SupplyDemandStatus` flag on the session). | B) stored on the session (a second derived truth that goes stale) |
| **D-RBAC** | **B — split `crm.visit-plan.read` / `.generate` / `.apply`;** apply ALSO requires FU01 `crm.planned-visit.manage`. Seed/grant = F-RBAC. | A) one `crm.visit-plan.manage` (no preview-without-commit) · C) reuse `crm.planned-visit.*` (conflates surfaces) |
| **D-ENDPOINTS** | **B — preview + apply + re-plan + session CRUD;** one Ocelot wildcard pair declared (§15). | A) 1 combined endpoint (overloaded verbs, unclear RBAC) |
| **D-CAPACITY-SCOPE** | **A — CyclePeriod-pinned CycleCapacity** for supply (`TotalVisitNumber` + `BetweenVisitTimeMinutes`). | B) per-rep capacity (HR-seam refinement, defer) · C) per-BU (wrong granularity) |
| **D-MULTI-REP** | **A — single-rep v1.** Multi-vehicle VRP is deferred behind FU03's F-SOLVER seam. | B) multi-rep fleet split (a much larger problem now) |

---

## 21. Legacy Reference (frozen — no code migrated)

Legacy `MicroTarget` (Daywork microservice) was the executable form of this FU: a unified plan atom
(`Employee + Week + Client + Order + Date + TravellingTime + Category + Criteria`) built by
`GetMicroTargetByFilterHandler` *from* `AIAPIs.BestRouteMicroTarget()` (the standalone geo-optimizer,
`Order = bestRoutes.IndexOf+1`). Legacy nuances captured for this FU: the **"VisitMix first" ordering gate**, the
**Recommended(AI) vs Targeted(manual) split** (vNext: segment-filter + manual selection; `PlannedVisitSelectionMode`
reserves `recommended`/`targeted`), **A/B/C coverage stats** ("3/12"), **TempClient staging** (the setup draft state —
realised in vNext as the thin `PlanningSession` staging aggregate, D-PERSISTENCE = C), and **toggle-style plan
create**. vNext splits the concerns: the atom → **FU01**, the route →
**FU03**, content order → **FU04** (over MOD-0162/0167), the minute-budget → **FU06/FU06B**, cadence → **MOD-0165**.
FU05 **re-expresses the orchestration** over those shipped seams — **no code, column, or `OldSystem` coupling is
migrated**. Related: [[mod0155-visit-route-planning-program]], [[legacy-visit-planning-analysis]],
[[legacy-crmv2-ucln-subjectlist-forwhom-analysis]], [[mod0155-fu06-cycle-capacity-pack]].

---

## Handoff

Module pack **`status: draft`** — **NO `runtime_code_allowed`, NO flip stamp** (deliberate). DCP-002 identity gate
PASS (exit 0, first try — no id/name change). **All 15 D-questions are now SETTLED and LOCKED** (2026-08-29, user +
Control Tower — §19.b/§20); every recommended default was accepted. Frontmatter resolved: **`entity_base:
EntityBase`** (D-PERSISTENCE = C) and **`golden_reference: n/a`** (D-UI = B). The choices are propagated through
scope (§2/§3), the orchestration flow (§4 + the new §4.3 `PlanningSession` field set), repo scope (§5), lifecycle
(§12), authorization (§14), gateway (§15), and the acceptance criteria + tests (§16/§17). The flow/seam plan remains
grounded against the **BUILT** FU03 `IRouteOptimizer` + FU04 `VisitContentSequenceResolver` +
MOD-0165/0164/0150/0149/0167/0151/FU06B seams.

**Newly concrete for Control-Tower review before flip:**
- **`PlanningSession` field set (§4.3):** aggregate (`EntityBase`) with `CyclePeriodId`, `ResourceId` (string,
  single-rep), `Status` (`draft/generated/committed/archived`), embedded `Selection` / `GenerationState` /
  `Provenance`, and `CommittedPlannedVisitIds` (link to FU01 atoms). Thin staging — NOT the schedule. Indexes
  `TenantId+CyclePeriodId+ResourceId` and `TenantId+Status` (no `$ne`).
- **Endpoint list (§15):** `POST …/preview` · `POST …/apply` · `POST …/re-plan` · `GET/POST/PUT …/sessions[/{id}]`,
  under one declared `/api/crm/visit-plan/{everything}` Ocelot wildcard (+ OPTIONS).
- **RBAC keys (§14):** `crm.visit-plan.read` / `.generate` / `.apply`; **apply + re-plan additionally require FU01
  `crm.planned-visit.manage`**. Definition only — seed/grant is F-RBAC.

For development the status must become `ready-for-dev` **and** `runtime_code_allowed: true` (+ `runtime_code_scope`) —
a **separate Control-Tower step** after this update is reviewed. This pack does not take it, and does not commit or push.
