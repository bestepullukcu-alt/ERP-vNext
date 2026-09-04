---
id: MOD-0155-FU03
name: Visit Route Optimization
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — ready-for-dev) · MOD-0155-FU02 (Visit Report) · MOD-0155-FU04 (Visit Content Sequence Execution) · MOD-0155-FU05 (MicroTarget / packing engine) · MOD-0155-FU06 (Cycle Capacity — SHIPPED) · MOD-0155-FU06B (Activity Time Budget — ready-for-dev) · MOD-0155-FU07 (Cycle Capacity Monthly — SHIPPED)
domain: commercial-suite
service: Diten.CrmService
frontend: none (no UI surface — the deliverable is a backend in-process seam + one JSON dry-run preview endpoint; the Day/Week route-visualisation UI belongs to FU05)
shell: tenant
golden_reference: n/a (in-process optimizer seam + one JSON preview endpoint — NOT a Golden DataTable CRUD surface; mirrors the IVisitFrequencyPolicyResolver / IConsentPreferenceEvaluator seams, not a Compact page)
entity_base: n/a (no persisted aggregate — the optimizer is a pure function over a supplied visit set; output is transient and never persisted by FU03)
status: ready-for-dev
runtime_code_allowed: true
flip_approved_by: "user via Control Tower — 2026-08-29 (all 6 D-questions LOCKED: D-ENDPOINT=yes dry-run preview endpoint crm.visit-route.preview, D-WORKINGHOURS=config placeholder, D-TIEBREAK=addedTravel→earliestWindow→visitId, D-DAYSEED=optional startLocation+centroid, D-AVAIL-DAY=weekday, D-SPEED=config-with-default; user ordered FU03 before FU04)"
owner: module-pack-author
branch: feature/crm/mod-0155-fu03-visit-route-optimization
started: 2026-08-29
target: 2026-08-29 (flipped for build)
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = "route plan")
  - MOD-0155-FU01 (PlannedVisit — provides the visit set shape: lat/long, PlannedDurationMinutes, availability snapshot, Slot.SequenceOrder/SlotStartTime/SlotEndTime that THIS FU's output fills. FU03 does NOT write PlannedVisit — it returns a result the caller applies)
  - MOD-0155-FU04 (Visit Content Sequence Execution — UPSTREAM producer of durationMinutes via the FU06B ActivityTimeBudgetCalculator; FU03 treats durationMinutes as GIVEN input, never computes it)
  - MOD-0155-FU06B (Activity Time Budget — source of betweenVisitMinutes = CycleCapacity.BetweenVisitTimeMinutes; read as input)
  - MOD-0155-FU05 (MicroTarget / packing engine — DOWNSTREAM consumer/orchestrator; FU05 selects the visit set + calls THIS optimizer + owns the Day/Week setup UI. FU03 is the scheduler, NOT the selector)
  - MOD-0150 (read-only — ContactAvailability supplies the per-contact availabilityWindows that FU03 honors as a HARD constraint. Mutate NONE)
  - MOD-0151 (read-only — Territory / MicroZone geo context; consumed, never defined)
  - MOD-0288 (boundary — Person/Position master; the additive future source of per-rep working hours. NO HR integration in v1, §D-WORKINGHOURS)
  - MOD-0018 (RBAC — consumes ONE new key `crm.visit-route.preview` for the dry-run endpoint; the key is NOT seeded by this pack → F-RBAC)
---

# MOD-0155-FU03 — Visit Route Optimization

> **✅ READY FOR DEV — code authority granted.**
> Flipped 2026-08-29 (user via Control Tower): `status: ready-for-dev` + `runtime_code_allowed: true`. All 6
> D-questions are LOCKED (see §19/§20). `@orchestrator` may build this pack.
>
> FU01 laid the plan foundation (WHO/WHEN, and the null-born `Slot.SequenceOrder`/`SlotStartTime`/`SlotEndTime` that
> a scheduler will fill). FU06/FU06B/FU07 set the minute budget (how long ONE visit takes, the between-visit buffer,
> the daily work minutes). This FU binds the missing question those foundations deliberately deferred:
> *"Given a **fixed set** of visits with locations, durations and availability windows, in **what order** and in
> **which time-slots across the period** should the rep do them — and which ones **won't fit**?"*
>
> This is the **route + time-window scheduler for a GIVEN visit set**. It is **NOT** the selector/orchestrator
> (that is FU05) and it is **NOT** a plan generator. It is a clean **`IRouteOptimizer` seam** (mirroring the existing
> in-process `IVisitFrequencyPolicyResolver` / `IConsentPreferenceEvaluator` seams — DI-wired, **no HTTP self-call**),
> whose **v1 implementation is an in-process greedy time-window insertion heuristic**. Routing with time windows
> (VRPTW) is NP-hard; the heuristic ships fast and unblocks FU05, and a real solver (OR-Tools / VROOM) can later swap
> behind the **same seam** with zero contract change.
>
> Authority order: **Blueprint Excel** > this pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Identity Gate and Home Decision

### 0.1 DCP-002 — PASS (2026-08-29)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU03 --name "Visit Route Optimization" --parent MOD-0155
OK  MOD-0155-FU03: proven against Blueprint/registry.
REAL_EXIT=0
```

The gate proves **identity** (parent `MOD-0155 | Field Sales / Visit Planning` is canonical in the registry, and the
`FU03` id does not collide), not the descriptive **name**. Parent's canonical name is **"Field Sales / Visit
Planning"** and does not change; the frontmatter `name` is a repo-side descriptor. **The registry row is NOT written
by this pack** (MOD-0155-FU01 / MOD-0165-FU01 precedent) → §20 / F-REG. `FU03` was already reserved for "Route
Planning" across the sibling packs (FU01 §20/F-FU03, FU06B §2.3); this pack claims exactly that slot — no id change
was needed.

### 0.2 D-HOME — Home is **MOD-0155**

`crm-sor-boundary.md` reads *"Visit Plan / MicroTarget / Visit / Visit Report / **route plan** → MOD-0155"*. The route
optimizer is the "route plan" clause of that line and lives in `Diten.CrmService`. It reads Territory/MicroZone geo
(MOD-0151) and ContactAvailability (MOD-0150) **read-only**; it defines no master of its own.

---

## 1. Module Summary

### 1.1 What it does

Introduces one **in-process seam** — **`IRouteOptimizer`** — and its **v1 implementation**, a **greedy time-window
insertion heuristic**. The seam takes a **fully-supplied visit set** (the caller already selected WHICH visits, and
FU04 already produced each visit's `durationMinutes`) plus the rep's working hours, the period and travel parameters,
and returns a concrete **schedule**: for each visit an `assignedDate`, `startTime`, `endTime`, `travelToNextMinutes`
and `sequenceOrder` — plus an **`unscheduled`** list of visits that could not fit, each with a reason.

The optimizer assigns **both order AND time-slots across the whole period** (cross-day continuity: each new day starts
geographically near where the previous day ended). It honors **per-contact availability windows as a HARD
constraint**. Travel time is computed **in-house** (haversine distance × road factor, v1 = 1.3) — **no external
routing API, no cloud call** (pharma HCP-location privacy + cost).

### 1.2 Target consumer

The consumers are **machines**, not an operator screen. **MOD-0155-FU05** (the packing / MicroTarget orchestrator)
selects the visit set, calls `IRouteOptimizer.Optimize(...)` in-process, and applies the result to `PlannedVisit`
rows (writing `Slot.SequenceOrder`/`SlotStartTime`/`SlotEndTime`). A planner interacts with the **result** through
FU05's Day/Week setup UI, not through FU03. FU03 owns the algorithm + contract; it does not own the selection or the
UI. FU03 **also ships one thin dry-run preview endpoint** (`POST /api/crm/route-optimization/preview`, §11) so the
heuristic can be tested with real data **before FU05 exists** — a pure calculator that persists nothing.

### 1.3 Capacity summary

**One seam** (`IRouteOptimizer` interface) · **one v1 implementation** (`GreedyTimeWindowRouteOptimizer`) delegating
to a **pure** `TimeWindowInsertionEngine` (no I/O, no repository, no `DateTime.UtcNow`, no `HttpClient`) · one input
DTO + one output DTO (§4) · a small in-house `HaversineTravelModel` (distance × road factor) · DI registration ·
**one JSON dry-run preview endpoint** (`POST /api/crm/route-optimization/preview`) with **one new permission key**
(`crm.visit-route.preview`) and **one new Ocelot route pair** (declared §15, added by the integration-agent at build
time) · boundary + algorithm unit tests. **No persisted aggregate, no Mongo collection, no index.** Output is
**transient** — FU03 persists nothing, the preview endpoint included (dry-run only).

### 1.4 This FU is the SCHEDULER, not the SELECTOR and not a generator of demand

It does **not** decide **which** targets to visit, does **not** extend frequency into a visit count, does **not**
create `PlannedVisit` rows, does **not** compute durations (reads them as input), does **not** own the Day/Week setup
UI, and does **not** call any external map/routing service. Given a set, it returns a schedule + an unscheduled list.
Selection, frequency-extend, demand generation and the setup UI are **FU05**; duration production is **FU04** (via the
FU06B calculator). The `unscheduled` list is the **supply-vs-demand warning materialised** — a WARNING surfaced to
the planner, **never a hard block**; the planner drops, reschedules or overrides.

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Scope | Decision |
|---|---|
| **In-scope** | `IRouteOptimizer` seam + input/output contract (§4) · v1 `GreedyTimeWindowRouteOptimizer` + pure `TimeWindowInsertionEngine` (greedy time-window insertion, cross-day continuity, availability-honoring, `unscheduled`=warning) · in-house `HaversineTravelModel` (distance × roadFactor 1.3) · DI wiring · **one thin `POST /api/crm/route-optimization/preview` dry-run endpoint over a supplied set** (D-ENDPOINT=YES) + its **new `crm.visit-route.preview` permission key** + **new Ocelot route pair declared** (integration-agent adds it at build time) · algorithm + boundary tests |
| **Out-of-scope (EXPLICITLY DEFERRED)** | Visit **selection** / which targets to visit (**FU05**) · frequency→visit-count **extend** (**FU05**, reads MOD-0165) · **demand generation** / auto-creating PlannedVisit rows (**FU05**) · **writing** the schedule back onto `PlannedVisit.Slot.*` (**FU05** applies the FU03 result) · **duration computation** (**FU04** via FU06B `ActivityTimeBudgetCalculator`) · Day/Week **setup UI** (**FU05**) · visit **execution** / check-in / GPS / actuals (**FU02**) · any **external routing/map API**, real-time traffic, geocoding · a persisted route/plan aggregate · multi-rep / multi-vehicle fleet split · a production solver (OR-Tools/VROOM) — **swappable later behind the SAME seam** (F-SOLVER) |

### 2.2 SoR boundary — owned vs. consumed read-only

| Object | Owner | In this FU |
|---|---|---|
| `IRouteOptimizer` + v1 heuristic + input/output contract | **MOD-0155** | **OPENED** — the only thing this FU owns |
| `PlannedVisit` + `Slot.SequenceOrder`/`SlotStartTime`/`SlotEndTime` | MOD-0155 (FU01) | **READ as input shape / WRITE by FU05** — FU03 returns the values; FU05 applies them. FU03 does NOT open or mutate PlannedVisit |
| `durationMinutes` per visit | MOD-0155 (FU04 via FU06B calc) | **GIVEN input** — never computed here (D-DURATION) |
| `betweenVisitMinutes` | MOD-0155 (FU06B `CycleCapacity.BetweenVisitTimeMinutes`) | **GIVEN input** — read from capacity by the caller; FU03 receives a scalar |
| rep working hours (start/end/lunch) | MOD-0288 / config (future) | **GIVEN input** — v1 placeholder default (§D-WORKINGHOURS); no HR integration |
| `availabilityWindows` (per-contact) | MOD-0150 `ContactAvailability` | **GIVEN input, HARD constraint** — read-only; FU03 honors, never mutates |
| `Account`/`Contact` lat/long (HCP location) | MOD-0149/0150 | **GIVEN input** — coordinates passed in; FU03 does not geocode |
| Territory / MicroZone geo | MOD-0151 | **read-only context** — consumed, not defined |
| Visit **selection** / frequency-extend / demand | MOD-0155 (FU05) | **NOT OPENED** — FU03 is not the selector |
| `Visit` / actuals / GPS | MOD-0155 (FU02) | **NOT OPENED** |

### 2.3 One-sentence boundary with neighbouring measures

> **CycleCapacity (FU06/07)** says how many visits *fit* in the period (coarse) · **ActivityTimeBudgetCalculator
> (FU06B)** says how long *one* visit takes (fine) · **Route optimizer (FU03, this FU)** says in *what order* and *at
> what time on which day* a GIVEN set of visits happens, and *which don't fit* · **Packing/MicroTarget engine
> (FU05)** *selects* the set, *calls* FU03, and *applies* the result. Four distinct measures; this FU opens only the
> third.

### 2.4 Permanent prohibitions (this pack records them)

```text
external routing/map/traffic/geocoding API call          ❌  in-house haversine×roadFactor ONLY (privacy + cost, D-TRAVEL)
IRouteOptimizer writes PlannedVisit / any Mongo doc      ❌  output is transient; FU05 applies it
IRouteOptimizer computes durationMinutes                 ❌  duration is GIVEN input (FU04/FU06B), D-DURATION
IRouteOptimizer selects which targets to visit           ❌  selection is FU05
IRouteOptimizer extends frequency into a visit count     ❌  FU05 reads MOD-0165 and expands demand
availability treated as a soft preference                ❌  availabilityWindows are a HARD constraint (with override), D-AVAIL
unscheduled treated as a hard failure / 500              ❌  unscheduled is a WARNING the planner resolves, D-UNSCHEDULED
DateTime.UtcNow / HttpClient / repository inside engine   ❌  the insertion engine is PURE (mirrors VisitFrequencyResolveEngine)
```

---

## 3. Owned Objects

| Layer | Object |
|---|---|
| **Seam (interface)** | `IRouteOptimizer` (`Features/RouteOptimization/IRouteOptimizer.cs`) — mirrors `IVisitFrequencyPolicyResolver`: single method, read-only, no writes |
| **v1 implementation** | `GreedyTimeWindowRouteOptimizer : IRouteOptimizer` — thin adapter; delegates to the pure engine |
| **Pure engine** | `TimeWindowInsertionEngine` (static/sealed, **no I/O**) — the greedy time-window insertion heuristic (mirrors `VisitFrequencyResolveEngine`) |
| **Travel model** | `HaversineTravelModel` (pure) — great-circle distance × `roadFactor`; `ITravelModel` seam so a real distance matrix can swap later |
| **Contract DTOs** | `RouteOptimizationInput` · `RouteVisitInput` · `AvailabilityWindow` · `RepWorkingHours` · `OptimizationPeriod` · `RouteOptimizationOutput` · `ScheduledVisit` · `UnscheduledVisit` (§4) |
| **Reason codes** | `RouteUnscheduledReasonCodes` (static class — the `unscheduled[].reason` vocabulary, §4.7) |
| **DI** | registration in the CrmService Application/Infrastructure composition root (§10) |
| **Endpoint** | `RouteOptimizationController` + `POST /api/crm/route-optimization/preview` — dry-run over a supplied set, persists nothing (§11/§15) |
| **Permission** | `crm.visit-route.preview` (new key; catalog/grant NOT seeded by this pack → F-RBAC) |
| **Gateway route** | `/api/crm/route-optimization/preview` + OPTIONS pair — **declared** here (§15); the integration-agent writes `ocelot.json` at build time (F-GW) |
| **NOT owned** | any `PlannedVisit` write · any persisted route/plan aggregate · selection · frequency-extend · Day/Week UI · external routing |

---

## 4. Contract (`IRouteOptimizer`) — the normative surface

> This is the load-bearing section. The seam is a **pure function over a supplied set**; there is **no aggregate**
> and **no persistence**. All ids/coords/durations arrive as **input**; the output is **returned, never written**.

### 4.1 Seam signature (mirrors the resolver seams — DI, in-process, no HTTP self-call)

```csharp
/// MOD-0155 FU03 route + time-window scheduler seam. SINGLE source of truth for how a GIVEN visit set is ordered
/// and slotted across a period. v1 = in-process greedy time-window insertion heuristic; a real solver (OR-Tools /
/// VROOM) can swap behind THIS interface with no contract change. Consumers — the FU05 packing engine in-process and
/// the FU03 dry-run preview endpoint's handler — call THIS; no consumer re-implements the heuristic, and there is no
/// HTTP self-call back through the Gateway. The optimizer performs NO writes and reads NO repository.
public interface IRouteOptimizer
{
    RouteOptimizationOutput Optimize(RouteOptimizationInput input);
}
```

> Signature is **synchronous** and takes a **fully-materialised input** — unlike the resolver, the optimizer needs
> **no repository/tenant load**; the caller supplies everything (this keeps the engine pure and trivially testable,
> the `VisitFrequencyResolveEngine.Resolve(...)` precedent). If a future solver needs async I/O, the seam can become
> `Task<...> OptimizeAsync(...)` — flagged for F-SOLVER, not assumed.

### 4.2 Input — `RouteOptimizationInput`

| Field | Type | Required | Rule / Note |
|---|---|---|---|
| `visits` | `RouteVisitInput[]` | Yes | The GIVEN set to schedule; already selected by the caller (FU05). Empty → empty output, not an error |
| `repWorkingHours` | `RepWorkingHours` | Yes | Per-day working window + lunch + optional `startLocation` (§4.4). Config default 09:00–18:00 / lunch 13:00–14:00 (D-WORKINGHOURS-SOURCE = config) |
| `period` | `OptimizationPeriod` | Yes | `{ dateFrom, dateTo }` inclusive; days available for assignment |
| `betweenVisitMinutes` | `int` | Yes | Buffer inserted BETWEEN consecutive visits (from `CycleCapacity.BetweenVisitTimeMinutes`, FU06B). `0 ≤ x ≤ 240` |
| `travelModel` | `TravelModelSpec` | Yes | v1 = `{ kind: "haversine", roadFactor: 1.3 }`. In-house only; no external API (D-TRAVEL) |

`RouteVisitInput`:

| Field | Type | Required | Rule / Note |
|---|---|---|---|
| `visitId` | `Guid` | Yes | Correlates output back to the caller's PlannedVisit; opaque to the engine |
| `lat` / `long` | `double` | Yes | HCP location, **supplied** (FU03 does not geocode). Missing/invalid → visit goes to `unscheduled` (`missing_location`), never a crash |
| `durationMinutes` | `int` | Yes | **GIVEN** (FU04 via FU06B calc). `> 0`. FU03 never computes it (D-DURATION) |
| `availabilityWindows` | `AvailabilityWindow[]` | No | Per-contact HARD constraint (D-AVAIL). Empty ⇒ no per-contact restriction (only working hours bound it) |
| `targetId` | `Guid?` | No | Passed through for the caller; the engine does not resolve it |

`AvailabilityWindow`: `{ day, start, end }` — `start`/`end` are `"HH:mm"` local wall-time (no `DateTimeOffset` —
avoids the CRM parallel-arrays trap, FU01 §4.8). **`day` is a WEEKDAY** (`monday`…`sunday`), matching MOD-0150
`ContactAvailability`'s per-weekday model; the engine maps each weekday onto every concrete date in `period`
(**D-AVAIL-DAY = weekday, LOCKED**). It is **not** a specific date.

### 4.3 Output — `RouteOptimizationOutput`

| Field | Type | Note |
|---|---|---|
| `scheduled` | `ScheduledVisit[]` | The placed visits, in assignment order |
| `unscheduled` | `UnscheduledVisit[]` | The supply-vs-demand **warning**, materialised (D-UNSCHEDULED) |

`ScheduledVisit`:

| Field | Type | Note |
|---|---|---|
| `visitId` | `Guid` | From input |
| `assignedDate` | `DateOnly` | The day within `period` the visit was placed on |
| `startTime` / `endTime` | `string "HH:mm"` | Local wall-time slot; `endTime = startTime + durationMinutes`. Honors working hours, lunch, availability |
| `travelToNextMinutes` | `int` | Haversine×roadFactor travel from this visit to the next in sequence (0 for the last of a day) |
| `sequenceOrder` | `int` | Position within its day (fills FU01 `Slot.SequenceOrder` when the caller applies it) |

`UnscheduledVisit`: `{ visitId, reason }` where `reason ∈ RouteUnscheduledReasonCodes` (§4.7).

### 4.4 `RepWorkingHours` — per-rep config placeholder (D-WORKINGHOURS-SOURCE = config, LOCKED)

```
RepWorkingHours {
  perDay:        { start, end, lunchStart, lunchEnd },   // all "HH:mm" local wall-time
  startLocation: { lat, long }?                          // OPTIONAL day-1 geographic seed (D-DAYSEED, LOCKED)
}
config default: start=09:00 end=18:00 lunchStart=13:00 lunchEnd=14:00 ; startLocation absent
```

- **Source = a per-rep config default** (via a defaults provider / `IConfiguration`, the FU06B
  `ICycleCapacityDefaultsProvider` precedent — the Application layer never carries `IConfiguration` directly). It is
  **NOT** derived from `CycleCapacity`: `CycleCapacity.DailyWorkMinutes` is a minutes-per-day figure with **no**
  start/end/lunch structure, so it cannot supply the slotting window. **D-WORKINGHOURS-SOURCE is settled: config
  placeholder, not CycleCapacity.**
- v1 is a **single per-day window** applied to every day of the period. A per-weekday or per-rep table is an
  **additive** future shape (**MOD-0288 / HR seam**) — **NOT built here** (no HR integration).
- **`startLocation` (D-DAYSEED, LOCKED).** Optional `{lat,long}` seed for day 1. When **present**, day 1 is seeded
  from it; when **absent**, the engine seeds day 1 from the visit **nearest the visit-set centroid**. Subsequent days
  always seed from the previous day's last-scheduled location (cross-day continuity, §4.5).

### 4.5 v1 heuristic — greedy time-window insertion (normative description)

> VRPTW is NP-hard. v1 is a **greedy constructive heuristic** — fast, deterministic, good-enough to unblock FU05.
> Optimality is **not** claimed; the `unscheduled` list makes any shortfall honest and visible.

1. **Order candidates** by a greedy nearest-neighbour rule with time-window awareness. Start each day from a
   geographic seed and repeatedly pick the "cheapest feasible next visit" (added travel + wait, subject to windows).
   **Tie-break (D-TIEBREAK, LOCKED):** among equally-cheap feasible candidates choose **(1) lowest added travel →
   (2) earliest available window start → (3) lowest `visitId`**. The `visitId` final key makes the result fully
   deterministic.
2. **Cross-day continuity.** Day 1 is seeded from `repWorkingHours.startLocation` if present, else from the visit
   **nearest the visit-set centroid** (D-DAYSEED, §4.4). The optimizer fills day 1 from that seed until no more visits
   fit that day's working hours; the **next day's seed is the last-scheduled visit's location** of the previous day,
   so each new day starts geographically near where the previous ended. Days advance through `period` until visits or
   days run out.
3. **Slot placement.** For a candidate, compute arrival = previous `endTime` + `betweenVisitMinutes` + travel; if
   arrival falls in lunch or before an availability-window start, **wait** to the window/lunch-end; `startTime` =
   feasible arrival, `endTime` = `startTime + durationMinutes`. Feasible only if `endTime ≤ working end` AND
   `endTime ≤ availability-window end` AND it lies within an availability window for that day.
4. **Availability = HARD.** A visit is only placed inside one of its `availabilityWindows` (when any are supplied).
   If no window on any remaining day fits, the visit is **not forced** — it goes to `unscheduled`. A **manual-override
   path** lets a planner force a slot (D-AVAIL / applied by FU05's setup UI); the engine itself never violates a
   window on its own.
5. **`unscheduled` = supply-vs-demand warning.** Any visit that cannot be feasibly placed anywhere in `period` is
   emitted to `unscheduled` with a reason (`period_exhausted`, `no_feasible_availability_window`,
   `duration_exceeds_working_day`, `missing_location`). This is a **WARNING**, never a thrown error and never a 500;
   the planner drops / reschedules / extends the period / overrides.
6. **Determinism.** Given identical input the output is identical (pure engine, fixed tie-break — D-TIEBREAK above,
   ending on `visitId`).

### 4.6 Travel model — in-house only (D-TRAVEL)

```
travelMinutes(a, b) = haversineKm(a, b) × roadFactor / assumedSpeedKmPerMin
config defaults: roadFactor = 1.3   (great-circle → road-distance correction)
                 assumedSpeedKmPerMin — sensible default constant, overridable via config (D-SPEED, LOCKED)
```

`HaversineTravelModel` is **pure** and behind an `ITravelModel` seam so a real distance matrix or solver-provided
matrix can swap later. **No external routing/map/geocoding API and no cloud call** — pharma HCP locations must not
leave the system, and cost/rate-limits are avoided. **`assumedSpeedKmPerMin` (and `roadFactor`) are config values
with sensible default constants, overridable via config** (**D-SPEED = config-with-default, LOCKED**) — supplied
through the same defaults provider as the working hours (§4.4), never hardcoded as a magic number in the engine.

### 4.7 `unscheduled[].reason` vocabulary (in-domain, fail-closed)

```text
RouteUnscheduledReasonCodes : period_exhausted · no_feasible_availability_window ·
                              duration_exceeds_working_day · missing_location · invalid_input
```

A static class in the RouteOptimization feature (the FU01/FU06B in-domain-vocab precedent). No MOD-0048 publish is a
runtime precondition — these are engine-internal codes, not an operator-published reference set.

---

## 5. Repo Scope

**Backend — `services/Diten.CrmService/`:**

```text
src/Diten.CrmService.Application/Features/RouteOptimization/
├── IRouteOptimizer.cs                         (NEW — seam interface + GreedyTimeWindowRouteOptimizer impl)
├── TimeWindowInsertionEngine.cs               (NEW — pure greedy time-window insertion heuristic; NO I/O)
├── ITravelModel.cs + HaversineTravelModel.cs  (NEW — pure travel model behind a seam)
├── RouteOptimizationModels.cs                 (NEW — input/output DTOs + RepWorkingHours + startLocation + reason codes)
├── Queries/PreviewRouteOptimizationQuery.cs   (NEW — dry-run preview query = the input DTO)
└── Handlers/PreviewRouteOptimizationHandler.cs (NEW — calls IRouteOptimizer, returns output DTO; NO writes)
src/Diten.CrmService.Application/DependencyInjection.cs   (CHANGES — register IRouteOptimizer + ITravelModel + defaults provider)
src/Diten.CrmService.Api/Controllers/CRM/RouteOptimizationController.cs   (NEW — POST preview, dry-run)
src/Diten.CrmService.Api/Models/CRM/RouteOptimizationRequests.cs         (NEW — request binding for the input DTO)
tests/Diten.CrmService.Application.Tests/RouteOptimization/RouteOptimizationEngineTests.cs   (NEW)
```

> **The defaults provider** (working hours 09:00–18:00 / lunch 13:00–14:00, `roadFactor`, `assumedSpeedKmPerMin`) is a
> small config-backed service — the FU06B `ICycleCapacityDefaultsProvider` / `ConfigurationCycleCapacityDefaultsProvider`
> precedent (Infrastructure layer holds `IConfiguration`, Application consumes the interface).

**This pack (the only write surface valid today):**

```text
execution/domains/commercial-suite/module-packs/MOD-0155-FU03-visit-route-optimization.md
```

---

## 6. Protected Paths

| Path | Reason |
|---|---|
| `.antigravity/**` | Global engineering system |
| `services/Diten.CrmService/**/Features/PlannedVisit/**` | FU01 aggregate; FU03 reads its shape but **writes nothing** (FU05 applies the schedule) |
| `services/Diten.CrmService/**/Features/CycleCapacity/**` | FU06/FU06B/FU07; `BetweenVisitTimeMinutes`/`DailyWorkMinutes` are **read as input**, not modified |
| `services/Diten.CrmService/**/Features/{VisitFrequencyPolicy,ConsentPreference,Campaign,Segmentation,StrategyTemplate,Territory,Account,Contact}/**` | Neighbouring surfaces; consumed read-only, git diff ∅ |
| `services/Diten.Platform/**`, other domain services (`Diten.MdmService/**`, `Diten.HcmService/**`, …) | Out of domain |
| `gateway/**/ocelot.json` | integration-agent owned; the `/api/crm/route-optimization/preview` route pair is **declared** (§15) but written by the integration-agent at build time → F-GW |
| RBAC catalog / seed / `rolePermissions` | **F-RBAC** — the new `crm.visit-route.preview` key is declared (§14) but its catalog row + grant are **not seeded by this pack** |
| `execution/registries/**` | **F-REG** — registry writes are outside pack authority |
| Mongo hand-edit | Forbidden (GUID subtype trap breaks all logins) — moot here (no persistence) |

---

## 7. Dependencies

| Dependency | Direction | Status | Note |
|---|---|---|---|
| **MOD-0155-FU01** PlannedVisit (visit-set shape + `Slot.*`) | input shape / output applied by FU05 | ready-for-dev | FU03 returns `sequenceOrder`/slot times; FU05 writes them onto PlannedVisit |
| **MOD-0155-FU04** content-sequence → `durationMinutes` | UPSTREAM producer | not built | duration is GIVEN input; FU03 never computes it (D-DURATION) |
| **MOD-0155-FU06B** `CycleCapacity.BetweenVisitTimeMinutes` | input scalar | ready-for-dev | caller reads it; FU03 receives `betweenVisitMinutes` |
| **MOD-0155-FU05** packing / MicroTarget engine | DOWNSTREAM consumer/orchestrator | not built | selects the set, calls `IRouteOptimizer`, applies result, owns Day/Week UI |
| **MOD-0150** ContactAvailability | read-only input | shipped | supplies `availabilityWindows` (HARD constraint) |
| **MOD-0151** Territory / MicroZone geo | read-only context | shipped | geo context; not defined here |
| **MOD-0288** Person/Position master | additive future working-hours source | reserved/planned | v1 uses a **config** default (§4.4); **no HR integration** (D-WORKINGHOURS-SOURCE = config) |
| **MOD-0018** RBAC | consumption (dry-run endpoint) | partial | new key `crm.visit-route.preview`; catalog/grant not seeded by this pack → F-RBAC |

---

## 8. Runtime Constraints

- **Pure engine.** `TimeWindowInsertionEngine` and `HaversineTravelModel` have **no I/O**: no `HttpClient`, no
  repository, no `DateTime.UtcNow`, no `ITenantContext`. All inputs arrive on the DTO; `now`/period come from input.
  This mirrors `VisitFrequencyResolveEngine` and makes the heuristic trivially unit-testable and deterministic.
- **In-process, no HTTP self-call.** FU05 and the preview endpoint's handler both call `IRouteOptimizer` via **DI**.
  No consumer re-implements the heuristic; no call goes back out through the Gateway (the resolver-seam rule).
- **No persistence.** No aggregate, no Mongo collection, no index. The `$ne` partial-index crash, DateTimeOffset
  parallel-arrays 500, and GUID-subtype class-map traps are **all N/A** — there is nothing to store. Time is
  `"HH:mm"` string + `DateOnly` (never `DateTimeOffset`), keeping the CRM date traps out even from the DTO shape.
- **Swappable.** A production solver (OR-Tools / VROOM) implements the **same** `IRouteOptimizer` and can inject an
  `ITravelModel` distance matrix — no contract change, no consumer change (F-SOLVER).
- **No external calls.** Travel is in-house haversine only (D-TRAVEL); coordinates are supplied, never geocoded.
- **Overflow / bad-input safety.** Negative/invalid durations, missing coordinates, and empty sets are handled by
  emitting `unscheduled`/empty output — never a thrown exception or 500.

---

## 9. Layout & Shell Contract

**N/A** — FU03 ships a backend seam with **no UI surface**; there is no DataTable, no `golden_reference` page, no
`_Form`/`Details`. The only surface is a single `POST /api/crm/route-optimization/preview` JSON endpoint (no HTML
view, no Razor `Layout`). The Day/Week **setup UI** that visualises a route belongs to **FU05**, not here.

---

## 10. Backend File Convention

New feature folder `Features/RouteOptimization/` mirrors the resolver-seam layout:
`IRouteOptimizer.cs` (interface + sealed impl) ‖ `TimeWindowInsertionEngine.cs` (pure, static/sealed, the
`VisitFrequencyResolveEngine` precedent) ‖ `ITravelModel.cs` + `HaversineTravelModel.cs` ‖ `RouteOptimizationModels.cs`
(DTOs + reason codes). DI registration added to the CrmService composition root next to the existing seam
registrations. No Command/Query suffix on the engine (it is not a MediatR handler). The **preview endpoint** follows
standard CQRS handler naming (`PreviewRouteOptimizationQuery` / `PreviewRouteOptimizationHandler`) — a read-only,
side-effect-free query, the `PreviewCycleCapacityCalculation` precedent.

---

## 11. Endpoint Contract — dry-run preview (D-ENDPOINT = YES, LOCKED)

FU03 ships **one** JSON endpoint: a **dry-run preview** over a supplied visit set. Its purpose is to let the
heuristic be **tested with real data BEFORE FU05 exists**, and later to back an FU05 "preview route" button. There is
**no HTML/Razor view** — it is a pure calculator, the `PreviewCycleCapacityCalculation` precedent.

```
POST /api/crm/route-optimization/preview
auth:  crm.visit-route.preview                 (new key, §14)
body:  RouteOptimizationInput  (§4.2 — the IRouteOptimizer input DTO verbatim)
200:   RouteOptimizationOutput  (§4.3 — scheduled[] + unscheduled[], the output DTO verbatim)
400:   invalid input (malformed DTO / out-of-range betweenVisitMinutes)
```

- **Dry-run only — persists NOTHING.** The handler calls `IRouteOptimizer.Optimize(input)` and returns the result;
  no `PlannedVisit` write, no Mongo write, no side effect. (Applying the schedule onto `PlannedVisit.Slot.*` is
  **FU05**, not this endpoint.)
- **Request = the input DTO, response = the output DTO** — the same contract the in-process seam uses, so a caller
  can validate the heuristic end-to-end over the wire and in-process identically.
- Empty/over-supply/unfittable inputs return **200** with `unscheduled[]` populated (the warning is data, not an
  HTTP error); only a malformed DTO is a **400**.
- The **new Ocelot route pair** (`POST` + `OPTIONS`) is **declared** in §15; the integration-agent writes
  `ocelot.json` at build time (F-GW). The **new permission key** `crm.visit-route.preview` is declared in §14; its
  catalog row + grant are a separate operator step (F-RBAC).

---

## 12. Lifecycle / State

**None.** The optimizer is stateless and owns no entity, therefore no state machine, no `draft/active/closed`, no
archive. The result is transient.

---

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Visit with `durationMinutes` > full working day | `unscheduled` with `duration_exceeds_working_day`; no crash |
| Visit whose availability windows never fit any day in `period` | `unscheduled` with `no_feasible_availability_window`; other visits still scheduled |
| More visit-minutes than the period can hold (supply < demand) | Overflow visits in `unscheduled` with `period_exhausted`; **not** an error — this IS the warning (D-UNSCHEDULED) |
| Visit with missing/invalid lat/long | `unscheduled` with `missing_location`; never a thrown exception |
| Empty `visits` | Empty `scheduled` + empty `unscheduled`; not an error |
| Availability window present | Placed strictly INSIDE a window (HARD); engine never self-violates it (D-AVAIL) |
| Cross-day | Day N+1's first visit is geographically near day N's last visit (continuity) |
| Determinism | Same input twice → byte-identical output (fixed tie-break, D-TIEBREAK) |
| No external call | Network trace shows zero outbound routing/map calls (D-TRAVEL) |

---

## 14. Authorization Convention

The **seam itself has no RBAC** — it is called in-process by FU05, which applies its own authorization. The **preview
endpoint** (§11) is `[Authorize]` under the tenant shell with a **new** permission key **`crm.visit-route.preview`**
(follows the `crm.*` convention; a distinct read-only preview key, not reused from another surface). The catalog row
+ grant are **not seeded by this pack** (F-RBAC) — a separate operator step. Actor: `tenant_user`.

> **Key name.** `crm.visit-route.preview` was chosen over `crm.route-optimization.preview` for a shorter, resource-
> style segment (`visit-route`) consistent with `crm.cycle-capacity.*` / `crm.planned-visit.*`. If Control Tower
> prefers the longer form, only this string changes — no contract impact.

---

## 15. Gateway / API Routing Decision

**One new route pair is required** for the preview endpoint and is **declared here** for the integration-agent:

```
POST    /api/crm/route-optimization/preview   →  Diten.CrmService  (RouteOptimizationController.Preview)
OPTIONS /api/crm/route-optimization/preview   →  (CORS/preflight pair)
```

`ocelot.json` is **integration-agent owned**; this pack **does not** write it (F-GW). Until the route is added the
endpoint returns the known 404 + `{}` missing-route signature. No catch-all covers `/api/crm/route-optimization/*`,
so the pair must be added explicitly.

---

## 16. Acceptance Criteria

> Each item maps to a §17 test. No vague wording.

**AC-SEAM — the seam and its purity**

- [ ] **AC-SEAM-1** `IRouteOptimizer.Optimize(input)` returns `RouteOptimizationOutput`; the impl performs **no**
      repository/HTTP/`DateTime.UtcNow` access (reflection/structure test — mirrors the resolver-engine purity).
- [ ] **AC-SEAM-2** `TimeWindowInsertionEngine` and `HaversineTravelModel` are pure: identical input → identical
      output; no mutation of the input DTO.
- [ ] **AC-SEAM-3** No PlannedVisit / Mongo write occurs during `Optimize` (no repository is even injected into the
      engine); output is transient.

**AC-TRAVEL — in-house only (D-TRAVEL)**

- [ ] **AC-TRAVEL-1** Travel uses `haversineKm × roadFactor(1.3)`; a known coordinate pair yields the expected
      minutes within tolerance.
- [ ] **AC-TRAVEL-2** No outbound routing/map/geocoding call is made (no `HttpClient` reference in the feature).

**AC-SCHEDULE — order + slot + cross-day**

- [ ] **AC-SCHEDULE-1** For a feasible set every visit gets `assignedDate`/`startTime`/`endTime`/`sequenceOrder`,
      `endTime = startTime + durationMinutes`, and slots do not overlap within a day.
- [ ] **AC-SCHEDULE-2** `betweenVisitMinutes` + `travelToNextMinutes` separate consecutive visits.
- [ ] **AC-SCHEDULE-3** Lunch (13:00–14:00 default) is never inside a visit slot; a visit that would straddle lunch
      is pushed after it.
- [ ] **AC-SCHEDULE-4** Cross-day continuity: day N+1's first visit is nearer day N's last location than a random
      alternative (continuity assertion).
- [ ] **AC-SCHEDULE-5 (D-DAYSEED)** With `startLocation` supplied, day 1's first visit is the nearest feasible to it;
      with `startLocation` absent, day 1 seeds from the visit nearest the visit-set centroid.
- [ ] **AC-SCHEDULE-6 (D-TIEBREAK)** Two candidates with equal added travel resolve by earliest available-window
      start, then by lowest `visitId`; output is byte-identical across runs (deterministic).

**AC-AVAIL — hard constraint (D-AVAIL)**

- [ ] **AC-AVAIL-1** A visit with an availability window is placed **inside** it; the engine never emits a slot
      outside the window on its own.
- [ ] **AC-AVAIL-2** A visit whose windows fit nowhere in `period` goes to `unscheduled` with
      `no_feasible_availability_window` — it is **not** forced into an invalid slot.

**AC-UNSCHEDULED — warning, not block (D-UNSCHEDULED)**

- [ ] **AC-UNSCHEDULED-1** Over-supply produces `unscheduled` entries with reasons and **no thrown exception / no
      500**; the schedulable remainder is still returned in `scheduled`.
- [ ] **AC-UNSCHEDULED-2** Every `unscheduled[].reason` is a `RouteUnscheduledReasonCodes` value.

**AC-BOUNDARY — scheduler only**

- [ ] **AC-BOUNDARY-1** No symbol that computes duration, selects targets, or expands frequency exists in the feature
      (`ComputeDuration`/`SelectTargets`/`ExpandFrequency`/`GeneratePlans` absent); `durationMinutes` is read from
      input only.
- [ ] **AC-BOUNDARY-2** `Features/{PlannedVisit,CycleCapacity,VisitFrequencyPolicy,ConsentPreference}/**` → git
      diff ∅ (FU03 reads shapes, mutates nothing).
- [ ] **AC-BOUNDARY-3** The seam is swappable: a stub second `IRouteOptimizer` can be registered and consumed with no
      change to the contract or callers.

**AC-ENDPOINT — dry-run preview (D-ENDPOINT)**

- [ ] **AC-ENDPOINT-1** `POST /api/crm/route-optimization/preview` with a valid `RouteOptimizationInput` returns
      **200** with `RouteOptimizationOutput` (`scheduled[]` + `unscheduled[]`) matching what the in-process seam
      returns for the same input.
- [ ] **AC-ENDPOINT-2** The endpoint **persists nothing** — no PlannedVisit/Mongo write occurs (the handler injects
      no repository); it is a pure dry-run.
- [ ] **AC-ENDPOINT-3** An over-supply / unfittable input returns **200** with `unscheduled[]` populated (warning is
      data, not an HTTP error); a malformed DTO / out-of-range `betweenVisitMinutes` returns **400**.
- [ ] **AC-ENDPOINT-4** The endpoint requires `crm.visit-route.preview`; before the Ocelot route is added it returns
      the 404 + `{}` missing-route signature (route declared §15, written by integration-agent).

---

## 17. Test Expectations

**17.1 Backend unit + endpoint tests (`tests/Diten.CrmService.Application.Tests/RouteOptimization/`) — target ≥ 24 tests**

| Cluster | Coverage |
|---|---|
| 1. Travel model | Haversine correctness + roadFactor scaling; symmetry; no HttpClient |
| 2. Single-day slotting | order, non-overlap, between-visit + travel spacing, lunch avoidance |
| 3. Cross-day continuity | seed rollover; day N+1 near day N's last location; period exhaustion |
| 4. Availability HARD | placed-inside-window; unfittable → unscheduled; manual-override slot honored |
| 5. Unscheduled = warning | over-supply, missing location, duration > working day; never throws; reason codes valid |
| 6. Purity / determinism | identical output twice; input DTO unmutated; no repository injected; tie-break ends on `visitId` |
| 7. Day-seed | `startLocation` present → seeds day 1; absent → centroid-nearest seed |
| 8. Boundary | no duration/selection/frequency symbol; swappable second impl |
| 9. Endpoint | preview returns 200 output DTO = seam output; persists nothing; over-supply → 200 with unscheduled; malformed → 400 |

**17.2 Quality gates**

| Gate | Expectation |
|---|---|
| Build | `Diten.CrmService` PASS (the preview endpoint lives in the CrmService Api project; no `Diten.Web` change) |
| Boundary diff | `Features/{PlannedVisit,CycleCapacity,VisitFrequencyPolicy,ConsentPreference,Territory,Account,Contact}/**` → git diff ∅ |
| `verify_module_id --check-id MOD-0155-FU03` | exit 0 |
| Gateway | `/api/crm/route-optimization/preview` 200 after the integration-agent adds the route; 404 + `{}` before |
| RBAC | `crm.visit-route.preview` present in the auth request; grant is an operator step (F-RBAC) |

> **Orchestrator self-report is not trusted** — test counts are read from an actual run (MOD-0162-FU04 lesson).

---

## 18. Ready-for-dev Checklist

- [x] DCP-002 identity gate **PASS** (exit 0, 2026-08-29) — command + output in §0.1
- [x] Module registry checked: `MOD-0155` canonical, not a deprecated alias
- [x] `IRouteOptimizer` contract specified precisely (§4): input/output DTOs, working hours, travel model, reason codes
- [x] v1 heuristic described normatively (§4.5): greedy time-window insertion, cross-day continuity, availability HARD, unscheduled=warning
- [x] Seam/DI wiring plan mirrors `IVisitFrequencyPolicyResolver` (in-process, no HTTP self-call, pure engine) (§3/§8/§10)
- [x] Vocabulary defined in-domain, fail-closed (§4.7)
- [x] Decision log cites the locked decisions (§19)
- [x] Acceptance criteria testable; each maps to a §17 test
- [x] Boundary discipline recorded: scheduler only, no selection/duration/frequency/persistence (§2.4)
- [x] **All 6 D-questions SETTLED (2026-08-29, user + Control Tower)** and locked into §19/§20: D-ENDPOINT=YES ·
      D-WORKINGHOURS-SOURCE=config · D-TIEBREAK=travel→window→visitId · D-DAYSEED=startLocation|centroid ·
      D-AVAIL-DAY=weekday · D-SPEED=config-with-default
- [x] Dry-run preview endpoint in scope (§11): request=input DTO, response=output DTO, persists nothing, new key
      `crm.visit-route.preview` (§14), new Ocelot route pair declared (§15)
- [ ] **SEPARATE flip decision:** `status: ready-for-dev` + `runtime_code_allowed: true` — NOT taken by this pack;
      Control Tower performs it after reviewing this update

---

## 19. Implementation Notes / Decision Log

> **All six open D-questions were settled by the user + Control Tower on 2026-08-29 and are LOCKED below**
> (D-ENDPOINT, D-WORKINGHOURS-SOURCE, D-TIEBREAK, D-DAYSEED, D-AVAIL-DAY, D-SPEED). §20 no longer carries open
> questions — only deferred follow-ups.

| # | Decision | Rationale / rejected alternative |
|---|---|---|
| **D-ENDPOINT** ✅ LOCKED | **YES — ship one thin `POST /api/crm/route-optimization/preview` dry-run endpoint** over a supplied set: request = the `IRouteOptimizer` input DTO, response = the output DTO (`scheduled[]`+`unscheduled[]`), **persists NOTHING**; a **new** `crm.visit-route.preview` key + a **new** Ocelot route pair (declared §15, integration-agent writes it). In-scope, not optional. | Lets the heuristic be tested with real data **before FU05 exists** and later backs an FU05 "preview route" button. Rejected: seam-only (would leave the heuristic untestable over the wire until FU05). The `PreviewCycleCapacityCalculation` precedent proves a pure dry-run calculator endpoint. |
| **D-WORKINGHOURS-SOURCE** ✅ LOCKED | **Config placeholder.** `RepWorkingHours` come from a per-rep **config** default (09:00–18:00, lunch 13:00–14:00) via a defaults provider — **NOT** from `CycleCapacity`. HR/MOD-0288 is the additive future source; not built now. | `CycleCapacity.DailyWorkMinutes` is a minutes/day figure with **no** start/end/lunch structure, so it cannot supply the slotting window. Config default is enough for v1 and additively replaceable (the FU06B `ICycleCapacityDefaultsProvider` precedent). Rejected: deriving from CycleCapacity (structurally impossible) and hardcoding in the engine (breaks purity/config discipline). |
| **D-TIEBREAK** ✅ LOCKED | **Lowest added travel → earliest available window start → lowest `visitId`.** | Deterministic (the `visitId` final key guarantees byte-identical output). Travel-first keeps the greedy geographically sensible; window-second respects availability pressure. |
| **D-DAYSEED** ✅ LOCKED | **Optional `startLocation{lat,long}` on `RepWorkingHours`;** when absent, day 1 seeds from the visit **nearest the visit-set centroid**. Later days seed from the previous day's last location. | The contract had no rep home-base; an optional seed keeps the common case simple while allowing a real office/home origin. Centroid-nearest is a stable, deterministic fallback. |
| **D-AVAIL-DAY** ✅ LOCKED | **`AvailabilityWindow.day` = WEEKDAY** (`monday`…`sunday`), mapped onto each concrete date in `period`. | Matches MOD-0150 `ContactAvailability`'s per-weekday model, so the input maps 1:1 from the availability master. Rejected: specific-date windows (would diverge from MOD-0150 and force per-date expansion at the source). |
| **D-SPEED** ✅ LOCKED | **`assumedSpeedKmPerMin` (and `roadFactor`) = config with a sensible default constant, overridable via config** — supplied through the same defaults provider as working hours; never a magic number in the engine. | Field conditions vary by geography; config lets ops tune without a code change, while the default keeps v1 turnkey. |
| **D-SEAM** | **`IRouteOptimizer` in-process seam, mirroring the resolver seams.** v1 impl = greedy time-window insertion; a real solver swaps behind the same interface. | Keeps FU05 decoupled from the algorithm and lets a production solver (OR-Tools/VROOM) drop in with no contract change (F-SOLVER). Rejected: baking routing directly into FU05 (would fossilise the heuristic and forbid a solver swap). |
| **D-TRAVEL** | **In-house haversine × roadFactor(1.3); NO external routing/map/geocoding API, no cloud call.** Behind an `ITravelModel` seam. | Pharma HCP locations must not leave the system (privacy); external routing adds cost + rate-limits. roadFactor corrects great-circle to road distance. A real distance matrix can swap behind `ITravelModel` later. |
| **D-DURATION** | **`durationMinutes` is GIVEN input, never computed here.** | Duration = f(content × minute-budget) is FU04's job via the FU06B `ActivityTimeBudgetCalculator`. Computing it here would breach the no-engine boundary and duplicate FU06B. |
| **D-CROSSDAY** | **Optimizer assigns BOTH order AND time-slots across the whole period; each new day seeds from the previous day's last location.** | The rep's schedule is continuous; a per-day-in-isolation slotter would ignore geography across the day boundary. |
| **D-AVAIL** | **Per-contact `availabilityWindows` = HARD constraint, with a manual-override path.** | Locked, user-approved. A doctor's availability is real; the engine never self-violates a window. A planner may force a slot (applied via FU05's setup UI) — override is explicit, not the engine guessing. |
| **D-UNSCHEDULED** | **`unscheduled` IS the supply-vs-demand warning, materialised — a WARNING, never a hard block or 500.** | The planner drops/reschedules/extends/overrides. A hard failure would hide the shortfall instead of surfacing it. |
| **D-WORKINGHOURS** | **NO HR integration in v1.** MOD-0288 is the additive future source; v1 sources working hours from config (see D-WORKINGHOURS-SOURCE above). | Building an HR integration now would create a dead dependency on a reserved/planned module. |
| **D-NO-PERSIST** | **No aggregate, no collection, no index; output is transient.** | The result belongs to whoever applies it (FU05 → PlannedVisit `Slot.*`). Persisting a route here would create a second source of truth for the schedule. |
| **D-NO-ENGINE-CREEP** | **Scheduler only — no selection, no frequency-extend, no demand generation, no Day/Week UI.** | Those are FU05. Foundation-vs-engine discipline (FU01 D8 lineage): a scheduler that also selects would blur the FU03/FU05 boundary irreversibly. |

---

## 20. Follow-up Items (D-questions all resolved)

**All six D-questions are SETTLED (2026-08-29, user + Control Tower)** and locked in §19. Summary:

| ID | Resolution |
|---|---|
| ✅ **D-ENDPOINT** | **YES** — dry-run preview endpoint in scope (request=input DTO, response=output DTO, persists nothing, new `crm.visit-route.preview` key, new Ocelot pair). §11/§14/§15 |
| ✅ **D-WORKINGHOURS-SOURCE** | **Config** placeholder default (09:00–18:00 / 13:00–14:00), not CycleCapacity; HR/MOD-0288 additive later. §4.4 |
| ✅ **D-TIEBREAK** | **Lowest added travel → earliest available window → lowest `visitId`** (deterministic). §4.5 |
| ✅ **D-DAYSEED** | Optional **`startLocation`** on `RepWorkingHours`; absent ⇒ **centroid-nearest** seed. §4.4 |
| ✅ **D-AVAIL-DAY** | **Weekday** (matches MOD-0150 ContactAvailability), mapped onto each date in `period`. §4.2 |
| ✅ **D-SPEED** | **Config with a sensible default constant**, overridable. §4.6 |

**Deferred follow-ups:**

| ID | Item | Why deferred |
|---|---|---|
| **F-SOLVER** | Production solver (OR-Tools / VROOM) behind `IRouteOptimizer` + a real distance matrix behind `ITravelModel` | v1 heuristic unblocks FU05; solver is a like-for-like swap, no contract change |
| **F-FU05-APPLY** | FU05 applies the FU03 result onto `PlannedVisit.Slot.SequenceOrder/SlotStartTime/SlotEndTime` + owns the Day/Week setup UI + selection + frequency-extend | FU05 scope; FU03 only returns the schedule |
| **F-FU04-DURATION** | FU04 produces `durationMinutes` via the FU06B `ActivityTimeBudgetCalculator` | FU04 scope; FU03 reads it as input |
| **F-GW** | `ocelot.json` route pair `/api/crm/route-optimization/preview` (POST + OPTIONS) — **declared** §15 | integration-agent task; not pack authority |
| **F-RBAC** | `crm.visit-route.preview` catalog row + grant | new key **declared** §14; not seeded by this pack |
| **F-REG** | `module-id-registry.md` row for `MOD-0155-FU03` | registry writes outside pack authority (FU01 precedent) |
| **F-MULTIREP** | Multi-rep / multi-vehicle fleet split (true VRP) | v1 is single-rep; multi-rep is a later, larger problem |

---

## 21. Legacy Reference (frozen — no code migrated)

Legacy DitenCRM carried the executable form of this FU: a **best-route geo optimizer** (`bestRoutes` over
`lat/long`), a packed-slot atom (`Order` + `StartTime` + `TravellingTime`), and per-visit constraints
(`AvailableTime`/`PreferredTime`). FU01 already **froze the shape** of these (`Slot.SequenceOrder`/`SlotStartTime`/
`SlotEndTime`, `PlannedVisitAvailabilitySnapshot`) as **storage**; this FU adds the **computation** that fills them —
**re-written**, not migrated. Legacy `Order = bestRoutes.IndexOf+1`, its string time/coord traps, and any `OldSystem`
coupling are **not** carried over ("HH:mm" string + `DateOnly`, no `DateTimeOffset` co-sort). The **Daywork /
VisitMix** day-filling concepts remain FU05's (selection/demand), not FU03's. Related:
[[legacy-visit-planning-analysis]], [[mod0155-visit-route-planning-program]], [[mod0155-fu06-cycle-capacity-pack]].

---

## Handoff

Module pack still **`draft`**, now with **all six D-questions resolved and locked** (2026-08-29, user + Control
Tower): D-ENDPOINT=YES (dry-run preview endpoint in scope) · D-WORKINGHOURS-SOURCE=config · D-TIEBREAK=travel→window→
visitId · D-DAYSEED=startLocation|centroid · D-AVAIL-DAY=weekday · D-SPEED=config-with-default. Scope + contract were
adjusted accordingly (§1–§4, §11, §14, §15, §16–§17). No open questions remain — only deferred cross-FU follow-ups
(§20). For development the status must become `ready-for-dev` **and** `runtime_code_allowed: true` must be set — a
**separate** Control-Tower step, performed after reviewing this update. This pack does not take it.
