---
id: MOD-0155-FU02
name: Visit Report
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — ready-for-dev) · MOD-0155-FU03 (Route Optimization — BUILT) · MOD-0155-FU04 (Visit Content Sequence — BUILT) · MOD-0155-FU05 (MicroTarget Planning Engine — ready-for-dev) · MOD-0155-FU06 (Cycle Capacity — SHIPPED) · MOD-0155-FU06B (Activity Time Budget — ready-for-dev) · MOD-0155-FU07 (Cycle Capacity Monthly — SHIPPED)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: n/a (D-CALENDAR-UI = A, LOCKED — a bespoke Day/Week EXECUTION calendar like FU05's setup console, NOT a Golden DataTable CRUD surface; `verify_datatable_page` is N/A)
entity_base: EntityBase (D-REPORT-PERSISTENCE = A, LOCKED — a new immutable `VisitReport` aggregate linked to the FU01 `PlannedVisit` by `PlannedVisitId`, NOT execution fields mutated onto the plan atom)
status: ready-for-dev
runtime_code_allowed: true
flip_approved_by: "user via Control Tower — 2026-08-29 (all 8 D-questions LOCKED §19/§20: REPORT-PERSISTENCE=new immutable VisitReport aggregate, EXECUTION-STATUS=report-side outcome+minimal reflection via FU01 command, STAGE-ADVANCE=B actual-on-report + FU05-read-switch F-STAGE-READ, CALENDAR-UI=bespoke Day/Week calendar, REPORT-CONTENT=actuals+outcome+feedback+samples+follow-up, EDIT-WINDOW=short-window-then-append-only-amendments, split crm.visit-report.* RBAC; the LAST FU — closes MOD-0155)"
owner: module-pack-author
started: 2026-08-29
target: 2026-08-29 (flipped for build)
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = "Visit / Visit Report")
  - MOD-0155-FU01 (PlannedVisit atom — the planned visit FU02 EXECUTES + REPORTS. Read its status machine draft/planned/confirmed/cancelled/archived + Confirm/Cancel/Archive commands + PlannedVisitContentRef.StageIndex. FU02 ADDS the execution outcome + the report; it does NOT duplicate FU01's lifecycle. ready-for-dev)
  - MOD-0155-FU04 (VisitContentSequenceResolver — BUILT; FU04 resolves the PLANNED next content via PriorStageIndex. FU02 records the ACTUAL content presented; that actual StageIndex is what FU04/FU05 read next cycle. Read-only, signature unchanged)
  - MOD-0155-FU05 (MicroTarget Planning Engine — ready-for-dev; FU05 produces the PlannedVisit rows FU02 displays/executes on the calendar. Respect the FU05 SETUP vs FU02 EXECUTION boundary. Read-only)
  - MOD-0164-FU02 (IConsentPreferenceEvaluator — SHIPPED; contactability at execution time (compliance). Read-only, FilterApplied honoured)
  - MOD-0150 (Contact + ContactAvailability — read-only; who was actually seen)
  - MOD-0149 (Account / WorkPlace — read-only; where the visit happened)
  - MOD-0151 (Territory — read-only; FU02 is the FU that finally populates the FU09A/B readiness LastVisitDate/DueStatus placeholders FU01 §8.5 left as null/unknown — but see D-STAGE-ADVANCE / F-READINESS for whether that lands in this FU)
  - MOD-0048 (reference data — outcome codes / sample-material types; NOT a runtime precondition → F-RD)
  - MOD-0018 (RBAC — new `crm.visit-report.*` keys declared, split read/record; seed/grant NOT in this pack → F-RBAC)
  - MOD-0288 (boundary — Person/Position master; the reporting rep is a string ResourceId (FU01 D4 shape), no Guid FK)
  - DEV-0001 (Golden Reference Compact — template reference for any list surface)
---

# MOD-0155-FU02 — Visit Report

> **✅ READY FOR DEV — code authority granted.**
> The **LAST FU in the MOD-0155 program** — it closes **Field Sales / Visit Planning**. Flipped 2026-08-29 (user via
> Control Tower): `status: ready-for-dev` + `runtime_code_allowed: true`. All 8 D-questions are LOCKED (§19/§20);
> `entity_base: EntityBase` + `golden_reference: n/a` are resolved. `@orchestrator` may build this pack.
>
> **DCP-002 identity gate — PASS (2026-08-29):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU02 --name "Visit Report" --parent MOD-0155`
> → `OK  MOD-0155-FU02: proven against Blueprint/registry.` (**exit 0**, first try — no id/name change needed).
> Parent `MOD-0155 | Field Sales / Visit Planning` is canonical in the registry; `FU02` was already reserved for
> **"Visit Report"** across every sibling pack (FU01/FU03/FU04/FU05 frontmatter). **The registry row is NOT written by
> this pack** (FU01 / FU05 precedent) → §20 / F-REG.
>
> **What closes here.** FU01 built the plan **atom**. FU03 built the route seam. FU04 built the content-sequence
> resolver. FU05 built the **setup/orchestration** that *generates* the plan. **FU02 is the EXECUTION counterpart of
> FU05's setup** — the calendar where a rep views the planned visits FU05 produced, marks each **done / missed /
> rescheduled**, and **records the Visit Report** (what was actually presented + the outcome). It is where a *planned*
> visit becomes an *executed, reported* visit — and it **closes the content loop**: the actual presented content it
> records is what FU04/FU05 read next cycle to advance the doctor to the next topic (§4.4).
>
> Authority order: **Blueprint Excel** > this pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Identity Gate and Home Decision

### 0.1 DCP-002 — PASS (2026-08-29)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU02 --name "Visit Report" --parent MOD-0155
OK  MOD-0155-FU02: proven against Blueprint/registry.
REAL_EXIT=0
```

The gate proves **identity** (parent `MOD-0155 | Field Sales / Visit Planning` is canonical, and the `FU02` id does not
collide), not the descriptive **name**. Parent's canonical name is **"Field Sales / Visit Planning"** and does not
change. `FU02` was already reserved for **"Visit Report"** across every sibling's frontmatter; this pack claims exactly
that slot — **no id or name change was needed** (first-try exit 0). **The registry row is NOT written by this pack**
(FU01 / FU05 precedent) → §20 / F-REG.

### 0.2 D-HOME — Home is **MOD-0155**

`crm-sor-boundary.md` reads *"Visit Plan / MicroTarget / **Visit / Visit Report** / route plan → MOD-0155"*. The Visit
Report is the "Visit / Visit Report" clause of that line and lives in `Diten.CrmService`. It reads the FU01
`PlannedVisit` atom and the FU04 resolver, records execution + report data, and defines at most **one new aggregate**
(the Visit Report itself, if D-REPORT-PERSISTENCE = A); every master (account, contact, journey, consent) is read
through an **existing seam**.

---

## 1. Module Summary

### 1.1 What it does

FU02 is the **execution / calendar page** of the field-sales workflow — the counterpart to FU05's Day/Week **setup**
page. Its user is the **field rep** (and the field manager reviewing outcomes). On it the rep:

1. Opens their **calendar** (day / week view) of the planned visits FU05 generated as FU01 `PlannedVisit` atoms.
2. For each visit, records the **execution outcome**: **completed / missed / rescheduled** (and can still use FU01's
   existing `cancelled`).
3. For a **completed** visit, records the **Visit Report** — *what was actually presented* (the actual content /
   topic, which may differ from the plan), the **outcome**, doctor feedback, **samples / materials given**, and a
   **follow-up** flag.

FU02 is where a *planned* visit becomes an *executed, reported* visit. It is the FU that finally makes the doctor's
sequence progress **meaningful** — see §4.4.

### 1.2 Critical loop-closing role

FU04's *"the next visit's content auto-advances to the next topic"* is driven by `PriorStageIndex` — which FU04/FU05
read from **the doctor's last `PlannedVisit.PlannedVisitContentRef.StageIndex`** (FU04 `ResolveAsync`: `nextIndex =
prior + 1`). That prior StageIndex is only **trustworthy once a visit has actually been COMPLETED and its presented
content recorded** — i.e. **FU02's Visit Report is what makes a StageIndex "the last stage actually presented"**,
and therefore what advances the sequence on the next cycle. FU01 §8.5 already recorded the other half of this: the
MOD-0151 readiness projection returns `LastVisitDate = null` / `DueStatus = unknown` **on purpose** because *"the
executed visit belongs to FU02"*. **FU02 is where both of those become real.** §4.4 makes the write→read contract
explicit.

### 1.3 What it is NOT

FU02 is **not** the setup / generation surface (that is **FU05**), **not** the plan atom's shape or lifecycle owner
(that is **FU01** — FU02 extends the lifecycle at the execution end, it does not redefine it), **not** the content
resolver or the "next stage" arithmetic (that is **FU04** — FU02 records *actuals*, it does not compute the next
stage), and **not** a master of accounts / contacts / journeys / consent (all read-only through existing seams). It
performs **no route optimisation, no plan generation, no capacity maths** (D8 no-engine, inherited across the program).

### 1.4 Target consumer

The field rep, through the **Day/Week EXECUTION calendar** (§9), and the field manager reviewing recorded outcomes.
Downstream, FU04/FU05 read the actual StageIndex FU02 recorded (next cycle), and MOD-0151 readiness can finally show a
real `LastVisitDate` (F-READINESS — see D-STAGE-ADVANCE for whether that write lands in this FU or stays a downstream
read).

### 1.5 Legacy lineage

Legacy pharma had a **Visit / ActivityReport** step distinct from the MicroTarget plan (the executed visit with the
presented detail + samples + feedback). vNext splits the concern cleanly: the **plan atom → FU01**, the
**generation → FU05**, the **executed visit + report → FU02 (this FU)**. Code is **not migrated** — the rules are
re-expressed over the shipped FU01/FU04 seams (§21).

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Scope | Decision |
|---|---|
| **In-scope** | The **execution / calendar page** (day/week view of the FU01 planned visits; mark **done / missed / rescheduled** inline; open + record a report) · the **Visit Report** itself (outcome + actual content presented + doctor feedback + samples/materials given + follow-up flag + timestamp, linked to the `PlannedVisit`) · the execution/report **endpoints** + `crm.visit-report.*` RBAC keys + Ocelot route(s) · the write→read contract that makes the **last completed visit's actual StageIndex** available to FU04/FU05 next cycle (§4.4) |
| **Out-of-scope (EXPLICITLY DEFERRED)** | Plan **generation** / selection / route packing = **FU05** · the `PlannedVisit` **aggregate shape + core lifecycle** = **FU01** (FU02 extends the execution end via FU01's command path with a report-side status, D-EXECUTION-STATUS = A) · the "next stage" **arithmetic** = **FU04** (FU02 records actuals only) · FU05's `PriorStageIndex` read switch to the report = **F-STAGE-READ** (§20.1, a FU05-side edit) · content / journey / segment / consent **masters** (read-only) · GPS/geo-fenced check-in, live location tracking, digital-detailing content rendering, e-signature capture, expense/time-entry (MOD-0280 SoR) · the MOD-0151 readiness **projection** itself (FU02 supplies the executed-visit fact; the FU09A/B read of it is **F-READINESS**) |

### 2.2 SoR boundary — owned vs. consumed read-only

| Object | Owner | In this FU |
|---|---|---|
| The **execution / calendar page** (view plan → mark done/missed/rescheduled → open report) | **MOD-0155** | **OPENED** — FU02's core execution surface (distinct from FU05's setup surface) |
| **`VisitReport`** (outcome + actual content + feedback + samples + follow-up + timestamp, linked to `PlannedVisit`) | **MOD-0155** | **OPENED** (D-REPORT-PERSISTENCE = A, LOCKED) — a new immutable report aggregate. `entity_base: EntityBase`. See §4.3 |
| `PlannedVisit` + its status machine (`draft/planned/confirmed/cancelled/archived`) + Confirm/Cancel/Archive commands | MOD-0155 (FU01) | **EXTENDED at the execution end** — FU02 records the execution outcome (completed/missed/rescheduled) against the plan. It does **NOT** redefine FU01's lifecycle or add execution fields onto the atom (FU01 §2.3 forbids `PlannedVisit.ActualStartTime`). The outcome is report-side + a minimal "executed" reflection via FU01's command path, no new terminal states (**D-EXECUTION-STATUS = A**, LOCKED) |
| `PlannedVisitContentRef.StageIndex` (the ordinal FU04 reads as `PriorStageIndex`) | MOD-0155 (FU01 shape) | **READ + (the actual value) RECORDED** — FU02 records the ACTUAL content presented; the resulting "last completed" StageIndex is recorded on the `VisitReport` and read by FU04/FU05 next cycle. FU02 writes NO advanced cursor onto the plan atom (**D-STAGE-ADVANCE = B**, LOCKED; FU05-side read switch = F-STAGE-READ §20.1) |
| `VisitContentSequenceResolver` (PLANNED next content) | MOD-0155 (FU04) | **CONSUMED read-only (optional)** — shows the rep the *planned* content to compare against what was actually presented. FU02 never computes the next stage. Signature unchanged |
| The plan rows generated by the engine | MOD-0155 (FU05) | **READ / DISPLAYED** — FU02 is the execution view of FU05's output; FU02 does not generate plans |
| `IConsentPreferenceEvaluator` (contactability) | MOD-0164 (FU02) | **READ-ONLY provider call** — compliance at execution time; `FilterApplied` honoured |
| `ContactAvailability` / `Contact` | MOD-0150 | **READ-ONLY** — who was actually seen |
| `Account` / `WorkPlace` | MOD-0149 | **READ-ONLY** — where the visit happened |
| `TerritoryNode` / FU09A/B readiness projection | MOD-0151 | **READ-ONLY** (projection consumed); FU02 supplies the executed-visit fact the projection has been missing (F-READINESS) |
| Person / Position master | MOD-0288 | **untouched** — the reporting rep is a string `ResourceId` (FU01 D4 shape), no Guid FK |

### 2.3 Permanent prohibitions (this pack records them)

```text
Execution fields mutated onto the PlannedVisit atom      ❌  FU01 §2.3 already bans PlannedVisit.ActualStartTime; plan
                                                             and execution are not one document (D-REPORT-PERSISTENCE)
FU02 redefines FU01's PlanStatus machine                 ❌  FU01 owns draft/planned/confirmed/cancelled/archived;
                                                             FU02 ADDS the execution outcome, does not rewrite the machine
FU02 computes the next content stage                     ❌  that arithmetic is FU04 (nextIndex = prior + 1); FU02 records ACTUALS
FU02 generates / re-plans a schedule                     ❌  generation + re-plan is FU05; FU02 executes what exists
FU02 mutates any master (account/contact/journey/consent) ❌  every master is read-only through an existing seam
A submitted report is silently edited in place            ❌  pharma compliance — D-EDIT-WINDOW (LOCKED): immutable
                                                             after a short window, then append-only amendment
FU02 owns GPS check-in / e-signature / expense            ❌  deferred / other SoR (MOD-0280); not opened here
Gateway/RBAC/registry/Mongo hand-edit by this pack        ❌  declared only; integration-agent / operator / F-* steps
```

---

## 3. Owned Objects (all 8 D-questions LOCKED — shape is concrete)

> The object set is now settled by the locked D-questions: **D-REPORT-PERSISTENCE = A** (a new `VisitReport`
> aggregate), **D-EXECUTION-STATUS = A** (report-side outcome + a minimal atom reflection via FU01's command path),
> **D-CALENDAR-UI = A** (bespoke calendar). The table is final.

| Layer | Object | Note |
|---|---|---|
| **Aggregate** | **`VisitReport`** (aggregate root, `entity_base: EntityBase`) + embedded `VisitReportContentActuals` · `VisitReportSample[]` · `VisitReportFeedback` · `VisitReportAmendment[]` + `IVisitReportRepository` (+ Mongo impl, class-map **MANDATORY** — the CRM new-aggregate GUID-subtype trap) | **OPENED** (D-REPORT-PERSISTENCE = A). Immutable-after-submit (D-EDIT-WINDOW). Linked to the `PlannedVisit` by `PlannedVisitId`. Fields §4.3 |
| **Commands** | `RecordVisitOutcomeCommand` (completed/missed/rescheduled) · `SubmitVisitReportCommand` · `AmendVisitReportCommand` (append-only) | Execution-end write path; distinct from FU01's Create/Update/Confirm/Cancel/Archive. `RecordVisitOutcome` reflects the "executed" marker onto the plan through FU01's command path (D-EXECUTION-STATUS = A) |
| **Queries** | `GetVisitCalendarQuery` (day/week window of PlannedVisits + their report state) · `GetVisitReportByIdQuery` · `ListVisitReportsQuery` · `GetVisitReportContractQuery` (vocab/reference-data for dropdowns) | Calendar read joins FU01 atoms with their FU02 report state |
| **Vocabulary (in-domain)** | `VisitExecutionOutcome` (`completed` · `missed` · `rescheduled`; `cancelled` stays FU01) · `VisitReportStatus` (`draft` · `submitted` · `amended`) · `VisitReportReasonCodes` · sample/material + outcome reference-data keys (D-REPORT-CONTENT / F-RD) | In-domain fail-closed (FU01 precedent); out-of-set → 400; dropdowns fed from the contract endpoint, no hardcoded fallback |
| **Endpoints** | `VisitReportController` — record-outcome · submit-report · amend · calendar read · report read/list · contract | D-ENDPOINTS (§15) |
| **Permissions** | `crm.visit-report.read` · `crm.visit-report.record` · `crm.visit-report.amend` — **DEFINITION ONLY**; `record` also requires FU01 `crm.planned-visit.manage`; seed/grant = F-RBAC | D-RBAC (§14) |
| **Gateway route(s)** | `/api/crm/visit-report/{everything}` (+ OPTIONS) — **declared** (§15); integration-agent writes `ocelot.json` | F-GW |
| **Frontend** | Bespoke Day/Week EXECUTION calendar (`Views/CRM/VisitExecution/**` + JS) + same-origin proxy (D-CALENDAR-UI = A) | NOT a Golden CRUD surface; `verify_datatable_page` N/A |
| **Cross-FU follow-up** | **`F-STAGE-READ`** — FU05's `PriorStageIndex` resolution must switch to reading the last COMPLETED `VisitReport`'s StageIndex (§20.1) | A FU05-side edit; FU02 provides the report + query, does not modify FU05 |
| **NOT owned** | plan generation (FU05) · PlannedVisit shape + core lifecycle (FU01) · next-stage arithmetic (FU04) · any master |

---

## 4. Execution Flow — the normative surface (all 8 D-questions LOCKED)

> This is the load-bearing section. FU02 is an **execution recorder**: it holds **no engine** (D8) — it reads FU01
> atoms + the FU04-planned content, and it records what actually happened. The flow below is the contract; the
> D-questions that shape it are LOCKED (§19/§20) and their choices are stamped inline.

### 4.1 The end-to-end flow (normative)

```text
① OPEN THE CALENDAR
   Rep opens the Day/Week EXECUTION calendar (D-CALENDAR-UI = A — bespoke tenant-shell, NOT Golden CRUD).
   GetVisitCalendarQuery returns the FU01 PlannedVisit atoms in the window (their Slot.SlotStartTime / SequenceOrder
   from FU05) joined with each visit's FU02 report state (none / draft / submitted). Read-only display of the plan.

② PICK A VISIT → SEE THE PLANNED CONTENT
   For a selected visit, optionally call FU04 VisitContentSequenceResolver.ResolveAsync to SHOW the planned next
   content (JourneyId/StageId/StageIndex) so the rep can compare it against what they actually present. FU02 never
   computes the next stage — it only DISPLAYS the plan and RECORDS the actual.

③ RECORD THE EXECUTION OUTCOME
   RecordVisitOutcomeCommand: completed | missed | rescheduled (cancelled stays FU01's existing command).
   Relation to FU01's PlanStatus machine (D-EXECUTION-STATUS = A): a report-side VisitExecutionOutcome + a MINIMAL
   "executed" marker reflected onto the PlannedVisit through FU01's own command path — no new fields on the atom, no
   new terminal states on FU01's machine, FU01 §2.3 preserved.
     - missed      → reason code; no report content required; feeds re-plan (FU05) as "doctor missed"
     - rescheduled → captures the new intent; the actual re-plan write is FU05's job, not FU02's
     - completed   → proceeds to ④

④ RECORD THE VISIT REPORT  (completed only)
   SubmitVisitReportCommand writes the VisitReport (D-REPORT-PERSISTENCE = A — a NEW immutable aggregate linked by
   PlannedVisitId, NOT fields on the plan atom). It captures (D-REPORT-CONTENT):
     - ActualContent  : the actual content / topic / stage PRESENTED (may differ from the FU04-planned StageId),
                        incl. the ACTUAL StageIndex + a MatchedPlan flag — the value that closes the loop (§4.4)
     - Outcome        : the outcome code (reference-data-driven; F-RD)
     - Feedback       : doctor feedback / notes (free text)
     - Samples        : samples / materials given (typed, reference-data-driven; F-RD)
     - FollowUpFlag   : whether a follow-up is required (+ notes)
     - Timestamp / reporting rep (ResourceId, string; FU01 D4 shape)

⑤ SUBMIT → IMMUTABLE  (pharma compliance)
   On submit, a short correction window applies; after it the report is IMMUTABLE and corrections are append-only
   AmendVisitReport — never a silent in-place edit (D-EDIT-WINDOW). The report is now the audit record of what was
   presented.

⑥ LOOP CLOSES NEXT CYCLE  (§4.4)
   The actual StageIndex recorded in ④ becomes the doctor's "last completed"
   StageIndex. FU02 writes NO advanced cursor onto the plan atom (D-STAGE-ADVANCE = B — record actuals only). NEXT
   cycle, FU05 (via FU04 PriorStageIndex) reads the last COMPLETED VisitReport's StageIndex to advance to the next
   topic — the FU05-side F-STAGE-READ follow-up (§20.1).
```

### 4.2 D8 — FU02 holds no engine

FU02 computes no schedule, no route, no capacity, and **no next content stage**. The next-stage arithmetic stays in
FU04 (`nextIndex = prior + 1`); FU02 only *displays* that plan and *records* what actually happened. Its own logic is
**outcome capture + report persistence + calendar read** — nothing that scores, routes, or advances a sequence.

### 4.3 `VisitReport` — the report aggregate (D-REPORT-PERSISTENCE = A, LOCKED)

> `entity_base: EntityBase` (tenant-owned; `TenantId` server-resolved from the JWT claim, never in the payload;
> soft-delete `IsDeleted`/`DeletedAt`; `Version` = concurrency token). A **separate** aggregate linked to the plan
> atom by `PlannedVisitId` — the plan stays a plan, the report is the immutable record of execution (FU01 §2.3:
> *plan and execution are not one document*).

| # | Field | Type | Note |
|---|---|---|---|
| 1 | `Id` (VisitReportId) | Guid | `EntityBase` |
| 2 | `TenantId` | Guid | Server-resolved; cross-tenant access → 404 |
| 3 | `PlannedVisitId` | Guid | The FU01 atom this report executes (1:1 — one report per visit; corrections are append-only amendments, not a second report) |
| 4 | `ExecutionOutcome` | string | `VisitExecutionOutcome`: `completed` · `missed` · `rescheduled` (`cancelled` stays FU01) |
| 5 | `ReportStatus` | string | `VisitReportStatus`: `draft` · `submitted` · `amended` (§12; no reverse) |
| 6 | `ContentActuals` | `VisitReportContentActuals` (embedded) | The ACTUAL content presented — `JourneyId?`, `StageId?`, **`StageIndex?`** (the loop-closing value, §4.4), `StageCode?`, `MatchedPlan` (bool — did it match the FU04-planned stage?), display snapshots |
| 7 | `Samples` | `VisitReportSample[]` (embedded) | Samples / materials given: `{ ItemType (ref-data), ItemId?, Quantity, Notes? }` |
| 8 | `Feedback` | `VisitReportFeedback` (embedded) | `{ DoctorFeedback?, OutcomeCode (ref-data), FollowUpRequired (bool), FollowUpNotes? }` |
| 9 | `ReportedByResourceId` | string | The reporting rep (FU01 `Resource.ResourceId` shape — string, no fake FK; MOD-0288 owns the master) |
| 10 | `ExecutedAt` | DateTimeOffset | When the visit actually happened (the execution instant; single DateTimeOffset — never co-sorted with a second one, CRM parallel-arrays 500) |
| 11 | `SubmittedAt` / `AmendedAt` | DateTimeOffset? | Compliance timestamps (D-EDIT-WINDOW) |
| 12 | `Amendments` | `VisitReportAmendment[]` (embedded) | Append-only corrections after the edit window (D-EDIT-WINDOW = B): `{ At, ByResourceId, Reason, ChangedFields }` |
| 13 | `CreatedBy`/`UpdatedBy`/`CreatedAt`/`UpdatedAt`/`IsDeleted`/`DeletedAt`/`Version` | — | `EntityBase` audit |

> **MongoDB indexes:** `TenantId + PlannedVisitId` (the report-for-a-visit lookup) ·
> `TenantId + ReportStatus` · `TenantId + ContentActuals.StageIndex` scoped by target (for "last completed stage per
> doctor" — the §4.4 read). **No `$ne` partial index** (use `Filter.Type`/`$lt`). Embedded types stay
> class-map-registered (CRM new-aggregate GUID-subtype trap); **never** co-sort two `DateTimeOffset` fields
> (parallel-arrays 500 — `ExecutedAt` stands alone, and any date pairing uses a `DateOnly` like FU01's `PlannedDate`).

### 4.4 The loop-closing contract — what FU02 records → what FU04/FU05 read next cycle (NORMATIVE)

This is the reason FU02 closes the program. The content sequence only advances because a completed visit's presented
stage was recorded.

```text
WRITE (FU02, this FU)                          READ (next cycle, downstream)
──────────────────────────────────────────     ─────────────────────────────────────────────────
VisitReport.ContentActuals.StageIndex          FU05 builds the next plan → for each doctor calls
  = the stage ACTUALLY presented at the         FU04 ResolveAsync with:
  completed visit (④)                             PriorStageIndex = the doctor's LAST COMPLETED
                                                   VisitReport.ContentActuals.StageIndex
                                                 FU04: nextIndex = PriorStageIndex + 1  → next topic
```

- **Today (pre-FU02)** FU04/FU05 read `PriorStageIndex` from the last `PlannedVisit.PlannedVisitContentRef.StageIndex`
  — i.e. the *planned* stage, which is only correct if every planned visit was actually completed as planned. FU02
  makes it correct by recording the **actual** presented stage of a **completed** visit.
- **D-STAGE-ADVANCE = B (LOCKED)** fixes the mechanics: FU02 records the actual StageIndex on the `VisitReport` and
  FU04/FU05 read *that* (the last completed report) next cycle — **FU02 writes no advanced cursor onto the plan
  atom** (keeps FU01 §2.3 + D8). This creates a FU05-side follow-up, **`F-STAGE-READ`** (§20.1): FU05's
  `PriorStageIndex` resolution must switch from the last `PlannedVisit.StageIndex` to the last COMPLETED
  `VisitReport.ContentActuals.StageIndex`. That change is out of FU02's write scope — FU02 supplies the report + the
  query; FU05 is edited separately.
- FU01 §8.5 (`LastVisitDate = null` / `DueStatus = unknown` "belongs to FU02") is the **same** loop from the
  readiness angle: the executed-visit fact FU02 records is what a future readiness projection reads (F-READINESS).

---

## 5. Repo Scope (final shape — all 8 D-questions LOCKED: D-REPORT-PERSISTENCE = A, D-CALENDAR-UI = A)

**Backend — `services/Diten.CrmService/`:**

```text
src/Diten.CrmService.Domain/Entities/VisitReport.cs                         (NEW — aggregate + embedded types + vocab, §4.3)
src/Diten.CrmService.Domain/Repositories/IVisitReportRepository.cs          (NEW)
src/Diten.CrmService.Application/Features/VisitReport/
├── Commands/{RecordVisitOutcome,SubmitVisitReport,AmendVisitReport}Command.cs   (NEW)
├── Queries/{GetVisitCalendar,GetVisitReportById,ListVisitReports,GetVisitReportContract}Query.cs   (NEW)
├── Handlers/{CommandHandlers,QueryHandlers}/**                                    (NEW)
├── Validators/**                                                                  (NEW)
├── VisitReportPermissions.cs                                                      (NEW — crm.visit-report.* constants; DEFINITION ONLY)
├── VisitReportValidation.cs                                                       (NEW — shared guards, ONE place)
└── VisitReportModels.cs                                                           (NEW — all DTO/VM in one file)
src/Diten.CrmService.Infrastructure/Persistence/VisitReportRepository.cs    (NEW)
src/Diten.CrmService.Infrastructure/Persistence/DependencyInjection.cs      (CHANGES — VisitReport class-map + indexes §4.3)
src/Diten.CrmService.Api/Controllers/CRM/VisitReportController.cs           (NEW — record/submit/amend + calendar + report read + contract)
src/Diten.CrmService.Api/Models/CRM/VisitReportRequests.cs                  (NEW)
tests/Diten.CrmService.Application.Tests/VisitReport/**                     (NEW)

── Frontend: frontend/Diten.Web/ ── (D-CALENDAR-UI = A, bespoke tenant-shell calendar)
Controllers/CrmVisitExecutionController.cs     (NEW — same-origin proxy)
Views/CRM/VisitExecution/**                    (NEW — the Day/Week EXECUTION calendar; NOT a Golden CRUD surface)
wwwroot/assets/js/CRM/VisitExecution/**        (NEW — calendar + mark done/missed/rescheduled + report offcanvas)
Resources/Views/CRM/VisitExecution/*.{ar,en,es,fr,ru,tr,zh}.resx
Views/Shared/_LayoutTenantShell.cshtml         (permission-guarded <li> for the execution calendar)

── Gateway (DECLARED — integration-agent wires it, pack does not write) ──
gateway/**/ocelot.json                         (DECLARE: /api/crm/visit-report/{everything} — §15)

── This pack (the only write surface valid today) ──
execution/domains/commercial-suite/module-packs/MOD-0155-FU02-visit-report.md
```

---

## 6. Protected Paths

| Path | Reason |
|---|---|
| `.antigravity/**` | Global engineering system |
| `services/Diten.CrmService/**/Domain/Entities/PlannedVisit.cs` (+ `Features/PlannedVisit/**`) | FU01 aggregate + lifecycle — FU02 records the execution outcome via FU01's command path (D-EXECUTION-STATUS); it does not change the shape or the PlanStatus machine |
| `services/Diten.CrmService/**/Features/VisitContentSequence/**` | FU04 resolver — CONSUMED read-only (planned content display); not modified |
| `services/Diten.CrmService/**/Features/VisitPlanning/**` | FU05 engine — its output (PlannedVisit rows) is READ; not modified |
| `services/Diten.CrmService/**/Features/{ConsentPreference,ContactAvailability,Account,Contact,Territory}/**` | Consumed read-only via existing seams; git diff ∅ |
| `services/Diten.Platform/**`, other domain services | Out of domain |
| `gateway/**/ocelot.json` | integration-agent owned; `/api/crm/visit-report/*` **declared** (§15) → F-GW |
| RBAC catalog / seed / `rolePermissions` | **F-RBAC** — `crm.visit-report.*` keys declared (§14), not seeded here |
| `execution/registries/**` | **F-REG** — registry writes outside pack authority |
| `frontend/Diten.Web/Views/Shared/_Layout*.cshtml`, `Archive/**` | FROZEN |
| Mongo hand-edit | Forbidden (GUID subtype trap breaks all logins) |

---

## 7. Dependencies

| Dependency | Direction | Status | Note |
|---|---|---|---|
| **MOD-0155-FU01** PlannedVisit atom + lifecycle | FU02 reads + records the outcome against it | ready-for-dev | draft/planned/confirmed/cancelled/archived + Confirm/Cancel/Archive already exist; FU02 ADDS execution outcome (D-EXECUTION-STATUS), does NOT redefine the machine |
| **MOD-0155-FU04** `VisitContentSequenceResolver` | in-process CONSUME (optional, display) | **BUILT** | shows the PLANNED next content; `PriorStageIndex` read is what FU02's actuals feed next cycle (§4.4) |
| **MOD-0155-FU05** planning engine output | read/display | ready-for-dev | FU02 is the execution view of FU05's generated PlannedVisit rows |
| **MOD-0164-FU02** `IConsentPreferenceEvaluator` | read-only provider | **SHIPPED** | contactability at execution time; FilterApplied honoured |
| **MOD-0150** Contact + ContactAvailability | read-only | shipped | who was seen |
| **MOD-0149** Account / WorkPlace | read-only | shipped | where the visit happened |
| **MOD-0151** readiness projection (FU09A/B) | read-only (F-READINESS) | shipped | FU02 supplies the executed-visit fact the projection has been missing (FU01 §8.5) |
| **MOD-0048** reference data (outcome / sample-material types) | loose | — | NOT a runtime precondition; set publish is a separate operator step → F-RD |
| **MOD-0018** RBAC | consumption | partial | new `crm.visit-report.*` keys; catalog/grant not seeded here → F-RBAC |
| **MOD-0288** Person/Position master | boundary | reserved/planned | reporting rep = string `ResourceId`; no Guid FK (FU01 D4) |
| **DEV-0001** Golden Compact | template | shipped | reference for any list surface |

---

## 8. Runtime Constraints

- **Recorder, not engine (D8).** FU02 reads FU01/FU04 and records execution; it computes no schedule, route,
  capacity, or next stage.
- **Plan and execution are separate documents.** Execution data is a `VisitReport` (D-REPORT-PERSISTENCE = A),
  not fields on the `PlannedVisit` atom (FU01 §2.3).
- **In-process, no HTTP self-call.** FU04/MOD-0164 are called via DI, never back through the Gateway.
- **Tenant.** Every read is tenant-scoped via `ITenantContext`; FU02 adds no cross-tenant path; cross-tenant → 404.
- **Immutability (pharma compliance).** A submitted report is immutable after a short window; corrections are
  append-only amendments, never silent in-place edits (D-EDIT-WINDOW). This is the compliance-critical rule of the FU.
- **Concurrency.** `EntityBase.Version` optimistic token; a stale write → 409, no silent overwrite.
- **Transactions.** D-EXECUTION-STATUS = A reflects a minimal "executed" marker onto the `PlannedVisit` (via FU01's
  command path) alongside the `VisitReport` write, so the outcome write spans two aggregates. That multi-doc write
  MUST guard `StartTransaction` with `SupportsTransactionsAsync` + compensation (CRM standalone Mongo trap), else
  dev standalone 500s — all-or-nothing, no orphaned marker. The pure report submit/amend (single `VisitReport`
  aggregate) needs no transaction.
- **Date/time traps.** `ExecutedAt` is a lone `DateTimeOffset`; any date pairing uses `DateOnly` — never co-sort two
  `DateTimeOffset` fields (CRM parallel-arrays 500, inherited from FU01).

---

## 9. Layout & Shell Contract — the Day/Week EXECUTION calendar (D-CALENDAR-UI = A, LOCKED)

FU02 owns the **execution** surface (`shell: tenant` ⇒ `Layout = "_LayoutTenantShell"`, written explicitly, never
trusting `_ViewStart`). It is **NOT** FU05's setup surface. **LOCKED: a bespoke tenant-shell calendar**
(`golden_reference: n/a`), not a Golden DataTable CRUD page — the surface is a day/week calendar with inline
mark-done + a report offcanvas, not row CRUD, so `verify_datatable_page` is **N/A**. Panels:

```text
┌ Day / Week calendar ───────────────────────────────────────────────────────┐
│  the rep's PlannedVisit atoms in the window (FU05 slot order + time),        │
│  each showing report state (none / draft / submitted)                       │
├ Inline outcome ────────────────────────────────────────────────────────────┤
│  mark a visit done / missed / rescheduled directly on the calendar cell     │
├ Visit Report offcanvas (completed) ────────────────────────────────────────┤
│  planned content (FU04, read-only) vs ACTUAL content presented;             │
│  outcome code · doctor feedback · samples/materials given · follow-up flag  │
├ Submit / Amend ────────────────────────────────────────────────────────────┤
│  submit → immutable; amend → append-only correction (D-EDIT-WINDOW)         │
└─────────────────────────────────────────────────────────────────────────────┘
```

> The rejected alternative (D-CALENDAR-UI = B, a Golden DataTable Compact list of visits-to-report) would have set
> `golden_reference: compact` and pulled the Compact offcanvas/quickview partials + `verify_datatable_page` in-scope.
> A was chosen: a calendar matches the rep's day/week execution mental model and mirrors FU05's bespoke console.

---

## 10. Backend File Convention

New feature folder `Features/VisitReport/`, following the FU01 Golden layout: `Commands`/`Queries`/`Handlers`
(with separate `CommandHandlers` + `QueryHandlers` subfolders) / `Validators`, one `VisitReportModels.cs` for all
DTO/VMs, one `VisitReportValidation.cs` for shared guards (two copies forbidden). Command = `{Verb}VisitReportCommand`
(record); Query = `{Get|List}VisitReport{Qualifier}Query` (record); Handler = `{Verb}VisitReportHandler` (class, no
suffix); Validator = `{Verb}VisitReportValidator` (no suffix). The `VisitReport` aggregate + repository follow the FU01
Golden layout — **class-map registration MANDATORY** (the CRM new-aggregate GUID-subtype trap). If the calendar read
joins FU01 + FU02 state, that join lives in the `GetVisitCalendar` query handler, read-only.

---

## 11. Frontend File Contract (D-CALENDAR-UI = A, LOCKED)

Bespoke execution calendar: `Views/CRM/VisitExecution/**` + `wwwroot/assets/js/CRM/VisitExecution/**` + same-origin
proxy `Controllers/CrmVisitExecutionController.cs` + 7-language RESX (`ar,en,es,fr,ru,tr,zh`) + one permission-guarded
`<li>` in `_LayoutTenantShell`. The Compact offcanvas/quickview partials are **N/A** (not a Golden CRUD surface); the
report is a bespoke offcanvas on the calendar. RESX edits require a full fleet restart.

---

## 12. Lifecycle / State (D-REPORT-PERSISTENCE = A + D-EXECUTION-STATUS = A, LOCKED)

The `VisitReport` carries a **`VisitReportStatus`** state machine, distinct from FU01's `PlanStatus`:

```text
draft ──(submit)──► submitted ──(amend, append-only)──► amended
```

- **No reverse transitions**; a submitted report is immutable (D-EDIT-WINDOW).
- **`draft`** — the rep is filling the report (optional; some flows submit directly).
- **`submitted`** — the compliance record of what was presented (immutable in place).
- **`amended`** — one or more append-only corrections after the edit window; the original stays intact.
- **Relation to FU01 (D-EXECUTION-STATUS = A, LOCKED):** the `PlannedVisit.PlanStatus` machine (`draft/planned/
  confirmed/cancelled/archived`) is FU01's; FU02 records `completed/missed/rescheduled` as the **execution outcome**
  report-side (`VisitExecutionOutcome`) plus a minimal "executed" reflection onto the plan through FU01's command
  path. It does **not** add new terminal states to FU01's machine (the rejected option B) — FU01's enum is
  untouched.

---

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Report a visit that has no `PlannedVisit` | 400/404 — a report must link to an existing plan atom (no orphan reports) |
| Mark `missed` | reason code captured; no report content required; the row is available to FU05 re-plan |
| Mark `rescheduled` | new intent captured; the actual re-plan write stays FU05's job (FU02 does not generate) |
| Actual content differs from the FU04-planned stage | `ContentActuals` records the actual `StageIndex` + `MatchedPlan=false`; the loop reads the actual next cycle (§4.4) |
| Submit then attempt an in-place edit | rejected; only an append-only amendment after the window (D-EDIT-WINDOW) |
| Two reports for one completed visit | rejected (1:1 by `PlannedVisitId`); a correction is an append-only amendment, not a second report |
| Outcome+report spans two aggregates | all-or-nothing via transaction + `SupportsTransactionsAsync` fallback + compensation; no half-write; dev standalone works |
| Stale `Version` on amend | 409, no silent overwrite |
| Cross-tenant report id | 404 (no existence leak) |

---

## 14. Authorization Convention (D-RBAC = split, LOCKED)

The endpoints (§15) are `[Authorize]` under the tenant shell with a **split of read vs record**:

| Key | Guards |
|---|---|
| `crm.visit-report.read` | open the execution calendar, read a report, list reports |
| `crm.visit-report.record` | mark done/missed/rescheduled + submit a report |
| `crm.visit-report.amend` | file an append-only amendment after the edit window (D-EDIT-WINDOW) |

Splitting `record` from `read` lets a manager review outcomes without recording them. Because D-EXECUTION-STATUS = A
reflects the "executed" marker onto the `PlannedVisit`, `record` **also** requires FU01 `crm.planned-visit.manage`
(FU05 precedent for a cross-FU write). Catalog rows + grants are **not seeded by this pack** (F-RBAC). Actor:
`tenant_user`.

---

## 15. Gateway / API Routing Decision (D-ENDPOINTS, LOCKED)

**New route(s) required**, **declared here** for the integration-agent. One wildcard pair covers the execution surface:

```text
POST/GET/PUT  /api/crm/visit-report/{everything}   →  Diten.CrmService  (VisitReportController)
OPTIONS       /api/crm/visit-report/{everything}   →  (CORS/preflight pair)

concrete endpoints under the wildcard:
  GET  /api/crm/visit-report/calendar            (day/week window of PlannedVisits + report state)  [crm.visit-report.read]
  POST /api/crm/visit-report/outcome             (mark done/missed/rescheduled)                     [crm.visit-report.record + crm.planned-visit.manage]
  POST /api/crm/visit-report                      (submit a report)                                  [crm.visit-report.record]
  POST /api/crm/visit-report/{id}/amend           (append-only amendment)                            [crm.visit-report.amend]
  GET  /api/crm/visit-report[/{id}]               (list / read reports)                              [crm.visit-report.read]
  GET  /api/crm/visit-report/contract             (vocab / reference-data for dropdowns)             [crm.visit-report.read]
```

`ocelot.json` is **integration-agent owned**; this pack **does not** write it (F-GW). No catch-all covers
`/api/crm/visit-report/*`, so the pair must be added explicitly. Until then the endpoints return the 404 + `{}`
missing-route signature. Any bodiless 204 responses must use the `IsBodilessStatus` proxy guard.

---

## 16. Acceptance Criteria (all 8 D-questions LOCKED — finalised)

**AC-EXEC — the execution outcome**

- [ ] **AC-EXEC-1** A visit can be marked `completed` / `missed` / `rescheduled` on the calendar; `cancelled` still
      routes to FU01's existing command; the "executed" marker reflects onto the plan via FU01's command path without
      new terminal states on FU01's machine (D-EXECUTION-STATUS = A).
- [ ] **AC-EXEC-2** FU02 adds NO execution fields onto the `PlannedVisit` atom (FU01 §2.3 preserved); execution data
      lives on the `VisitReport` (D-REPORT-PERSISTENCE = A) — verified structurally.
- [ ] **AC-EXEC-3** FU02 contains no next-stage arithmetic / no route / no plan generation (`nextIndex`, `Haversine`,
      `Generate` absent from `Features/VisitReport/**` — delegated to FU04/FU03/FU05).

**AC-REPORT — the report content (D-REPORT-CONTENT)**

- [ ] **AC-REPORT-1** A completed visit's report captures actual content presented (incl. actual `StageIndex`),
      outcome code, doctor feedback, samples/materials given, and a follow-up flag.
- [ ] **AC-REPORT-2** Outcome codes + sample/material types are reference-data-driven (contract endpoint feeds the
      dropdowns; no hardcoded fallback list; out-of-set → 400).

**AC-LOOP — the loop closes (§4.4)**

- [ ] **AC-LOOP-1** The actual `StageIndex` recorded on a completed visit's report is the value a next-cycle
      `PriorStageIndex` read returns; FU02 writes NO advanced cursor onto the plan atom (D-STAGE-ADVANCE = B). The
      FU05-side switch to reading the report is tracked as F-STAGE-READ (§20.1), out of FU02's write scope.

**AC-IMMUTABLE — pharma compliance (D-EDIT-WINDOW)**

- [ ] **AC-IMMUTABLE-1** A submitted report cannot be edited in place; a correction is an append-only amendment after
      the window, preserving the original.

**AC-BOUNDARY / AC-UI**

- [ ] **AC-BOUNDARY-1** `Features/{VisitContentSequence,VisitPlanning,PlannedVisit,ConsentPreference}/**` → git diff ∅
      (consumed/read; any PlannedVisit reflection goes through FU01's own command path).
- [ ] **AC-UI-1** The execution calendar is a bespoke tenant-shell surface (`Layout = "_LayoutTenantShell"`);
      `verify_datatable_page` is N/A (D-CALENDAR-UI = A).

---

## 17. Test Expectations

- **Backend tests** (`tests/…/VisitReport/`): outcome recording (completed/missed/rescheduled); report submit +
  immutability + append-only amendment; the §4.4 loop (actual StageIndex is what a next-cycle `PriorStageIndex` read
  returns, via test-doubles for FU04/FU05); orphan-report rejection; 1:1 per `PlannedVisitId`.
- **Persistence tests:** `VisitReport` round-trip + class-map (GUID subtype) + status-machine guards (no reverse) +
  tenant isolation; no co-sorted `DateTimeOffset`.
- **Boundary tests:** no engine/next-stage/route/generation symbol in the feature; consumed features git diff ∅.
- **Build:** `Diten.CrmService` + `frontend/Diten.Web` → 0 errors.
- **Verifier:** `verify_module_id --check-id MOD-0155-FU02` exit 0; `verify_datatable_page` N/A (D-CALENDAR-UI = A —
  bespoke calendar, not a Golden CRUD surface).
- **Smoke (user):** open the execution calendar → see FU05-generated visits → mark one completed → record a report
  (actual content + samples + feedback + follow-up) → submit (immutable) → next cycle the doctor advances a stage.

> **Orchestrator self-report is not trusted** — test counts are read from an actual run (MOD-0162-FU04 lesson).

---

## 18. Ready-for-dev Checklist (NOT satisfied — this is a DRAFT)

- [x] DCP-002 identity gate **PASS** (exit 0, 2026-08-29, first try) — command + output in §0.1
- [x] Module registry checked: `MOD-0155` canonical, `FU02` reserved for "Visit Report" across siblings
- [x] Execution/report flow specified normatively (§4) + the loop-closing write→read contract (§4.4)
- [x] Grounded against the BUILT FU01 `PlannedVisit` (status machine + `PlannedVisitContentRef.StageIndex`) + FU04
      `VisitContentSequenceResolver` (`PriorStageIndex` / `nextIndex = prior + 1`)
- [x] **All 8 D-questions SETTLED and LOCKED** (2026-08-29, user + Control Tower) — §19.b/§20 are now resolution tables
- [x] **D-REPORT-PERSISTENCE = A** → `entity_base: EntityBase`; new immutable `VisitReport` aggregate (fields §4.3)
- [x] **D-CALENDAR-UI = A** → `golden_reference: n/a`; bespoke tenant-shell calendar; `verify_datatable_page` N/A
- [x] **D-EXECUTION-STATUS / D-STAGE-ADVANCE / D-REPORT-CONTENT / D-EDIT-WINDOW / D-ENDPOINTS / D-RBAC** all LOCKED (§20)
- [x] **F-STAGE-READ** cross-FU follow-up recorded (§20.1) — FU05 reads the last completed report's StageIndex; out of
      FU02's write scope
- [ ] **SEPARATE flip decision:** `status: ready-for-dev` + `runtime_code_allowed: true` + `runtime_code_scope` —
      NOT taken by this pack; Control Tower performs it after reviewing this update

---

## 19. Implementation Notes / Decision Log

> **19.a Program context** — encoded, not reopened (from the MOD-0155 roadmap [[mod0155-visit-route-planning-program]]
> and the shipped siblings).

| # | Decision | Rationale |
|---|---|---|
| **D-HOME** | Home = MOD-0155 (`Diten.CrmService`) | "Visit / Visit Report" clause of the SoR line (§0.2) |
| **D-CLOSES** | FU02 is the LAST FU — it closes the MOD-0155 program | plan(FU01) → route(FU03) → content(FU04) → engine(FU05) → **execution+report(FU02)** |
| **D-SETUP-VS-EXEC** | FU05 owns the SETUP page; FU02 owns the EXECUTION calendar | two surfaces, one boundary (FU05 §1.2 / D-NOT-FU02 already declared this split) |
| **D8-NO-ENGINE** | FU02 records; it computes no schedule/route/next-stage | inherited across the program |
| **D-NO-ATOM-MUTATION** | Execution data is NOT fields on the plan atom | FU01 §2.3 already bans `PlannedVisit.ActualStartTime` |

> **19.b The FU02 D-questions — ALL LOCKED (2026-08-29, user + Control Tower).** Every recommended default was
> accepted. These are now part of the design and are not reopened. Full text (question + rejected options) in §20.

| # | Locked decision | Concrete effect |
|---|---|---|
| **D-REPORT-PERSISTENCE = A** | New immutable `VisitReport` aggregate linked by `PlannedVisitId`; NOT execution fields on the FU01 plan atom (honors FU01 §2.3) | `entity_base: EntityBase`; §4.3 field set; plan atom untouched |
| **D-EXECUTION-STATUS = A** | Report-side `VisitExecutionOutcome` + a minimal "executed" reflection onto the plan via FU01's existing command path; FU01's `PlanStatus` machine UNCHANGED | §12; no new terminal states on the atom; §14 cross-FU permission |
| **D-STAGE-ADVANCE = B** | FU02 records the ACTUAL presented `StageIndex` on the `VisitReport`; NO advanced cursor written onto the plan atom | §4.4 loop reads the last completed report's `StageIndex`; **F-STAGE-READ** follow-up (FU05 must read the report, not the atom) |
| **D-CALENDAR-UI = A** | Bespoke tenant-shell Day/Week EXECUTION calendar (mirrors FU05's console) | `golden_reference: n/a`; `verify_datatable_page` N/A; §9 panels |
| **D-REPORT-CONTENT (locked)** | Actual content/stage (+ actual `StageIndex` + `MatchedPlan`) · outcome code · doctor feedback · samples/materials · follow-up flag; outcome + sample types **reference-data-driven** | §4.3 embedded blocks; F-RD for the sets |
| **D-EDIT-WINDOW (locked)** | Short correction window after submit, then immutable; corrections are append-only amendments (never a silent in-place edit) | §12 `submitted → amended`; pharma compliance; `Amendments[]` |
| **D-ENDPOINTS (locked)** | calendar-read + outcome + submit + amend + report-read + contract; one declared Ocelot wildcard pair | §15; F-GW |
| **D-RBAC (locked)** | Split `crm.visit-report.read` / `.record` / `.amend`; `record` ALSO requires FU01 `crm.planned-visit.manage` | §14; F-RBAC |

---

## 20. D-Question Resolutions — ALL LOCKED (2026-08-29, user + Control Tower)

> **All 8 D-questions are SETTLED.** Every recommended default was accepted. The table records each with its rejected
> alternatives; the design (§3/§4/§9/§12/§14/§15/§16) is stamped accordingly. Nothing here is still open.

| ID | ✅ Resolution | Rejected alternatives |
|---|---|---|
| **D-REPORT-PERSISTENCE** | **A — a NEW immutable `VisitReport` aggregate** (`EntityBase`), linked to the `PlannedVisit` by `PlannedVisitId`. FU01 §2.3 already bans execution fields on the plan atom (`PlannedVisit.ActualStartTime ❌ … plan and execution are not one document`); a report is an immutable compliance record with a different lifecycle (submit → amend), a different RBAC key, and a 1:many amendment history — all of which argue for a separate aggregate. Keeps the plan atom small and lets a completed report be queried independently for the §4.4 loop + readiness. | B) embed the report fields on the `PlannedVisit` atom (violates FU01 §2.3, bloats the atom, mixes two lifecycles/RBAC scopes, makes immutability-after-submit awkward on a mutable row) |
| **D-EXECUTION-STATUS** | **A — a report-side `VisitExecutionOutcome` (`completed`/`missed`/`rescheduled`) on the `VisitReport`, plus a MINIMAL "executed" reflection** onto the `PlannedVisit` through FU01's existing command path — WITHOUT adding new terminal states to FU01's `PlanStatus` machine. `cancelled` stays FU01's existing command. Keeps FU01 the single owner of the plan machine (no cross-FU enum rewrite) while the execution outcome lives where the report lives. | B) add terminal states (`completed`/`missed`) to FU01's `PlannedVisitStatus` (edits FU01's owned vocabulary from FU02, couples the two enums, re-opens the state machine) · C) outcome ONLY on the report, no reflection (calendar/readiness would have to join to the report to know a plan was executed) |
| **D-STAGE-ADVANCE** | **B — FU02 records the ACTUAL presented `StageIndex` on the `VisitReport`; FU04/FU05 read the last COMPLETED report's StageIndex as `PriorStageIndex` next cycle.** FU02 writes NO advanced cursor onto the plan atom (keeps FU01 §2.3 + D8). The "advance" stays FU04's arithmetic (`nextIndex = prior + 1`); FU02 only supplies the truthful "last actually presented" input. **Cross-FU consequence — `F-STAGE-READ` (§20.1):** FU05 today resolves FU04's `PriorStageIndex` from the doctor's last `PlannedVisit.StageIndex`; under B the correct source is the doctor's last COMPLETED `VisitReport`'s actual StageIndex → FU05 needs a small follow-up change to read the report instead of the plan atom. | A) FU02 also stamps the completed visit's `PlannedVisitContentRef.StageIndex` via FU01's command path (zero downstream change, but re-mutates the plan atom's content position at execution time — rejected in favour of a clean report-sourced read) · C) FU02 computes+writes the NEXT stage itself (that is FU04's arithmetic — D8 violation) |
| **D-CALENDAR-UI** | **A — a bespoke tenant-shell Day/Week EXECUTION calendar** (`golden_reference: n/a`; `verify_datatable_page` N/A), mirroring FU05's bespoke setup console. The rep's mental model is a day/week calendar with inline mark-done + a report offcanvas — a generation/execution workflow, not row CRUD. Symmetry with FU05 (setup) makes the pair coherent. | B) a Golden DataTable Compact list of "visits to report" (`golden_reference: compact`; `verify_datatable_page` in-scope) — cheaper + verifier-covered, but a flat list loses the day/week calendar affordance the field workflow expects |
| **D-REPORT-CONTENT** | **Capture: actual content/topic/stage presented (incl. actual `StageIndex` + a `MatchedPlan` flag), outcome code, doctor feedback (free text), samples/materials given (typed, quantity), follow-up flag (+ notes).** Outcome codes and sample/material types are **reference-data-driven** (MOD-0048 sets, F-RD); the rest are structural/free-text. The minimum that closes the loop (actual stage) + satisfies pharma reporting (samples, outcome, feedback) + drives follow-up. | Deferred behind F-* until needed: digital-detailing content telemetry, e-signature, objection tracking, competitor mentions, expense lines (MOD-0280 SoR) — keeps FU02 focused on closing the program, not a full detailing suite |
| **D-EDIT-WINDOW** | **A short correction window after submit, then the report is immutable and corrections are append-only `Amendments[]` (never a silent in-place edit).** Pharma compliance — a visit report is an audit record of what was presented to an HCP; regulators expect an unalterable record with a visible correction trail. The short window absorbs honest typos without a full amendment. | Freely editable while `draft`, locked only after an explicit "finalise" (weaker audit posture) · fully immutable on submit with NO amendments (real corrections then need a whole new report, messy for the 1:1 link) |
| **D-ENDPOINTS** | **calendar-read + record-outcome + submit-report + amend + report read/list + contract**, under one declared `/api/crm/visit-report/{everything}` Ocelot wildcard pair (+ OPTIONS). | A single overloaded endpoint (unclear verbs + RBAC) — same reasoning as FU05 D-ENDPOINTS |
| **D-RBAC** | **Split `crm.visit-report.read` / `.record` / `.amend`; `record` ALSO requires FU01 `crm.planned-visit.manage`** (it reflects the executed status onto the atom via FU01's command). Seed/grant = F-RBAC. | A single `crm.visit-report.manage` (no review-without-record) · reusing `crm.planned-visit.*` (conflates the plan surface with the execution surface) |

### 20.1 F-STAGE-READ — the cross-FU follow-up D-STAGE-ADVANCE = B creates (DO NOT LOSE)

> **This is a consequence of locking D-STAGE-ADVANCE = B and must be carried into FU05's build.** It is a FU05-side
> change, declared here so the decision that creates it is recorded next to the decision.

- **Today:** FU05's orchestration resolves FU04's `PriorStageIndex` from the doctor's **last `PlannedVisit`
  `PlannedVisitContentRef.StageIndex`** (FU05 §4.1 ④: *"PriorStageIndex ← the doctor's last PlannedVisit
  PlannedVisitContentRef.StageIndex"*). That is the *planned* stage — correct only if every planned visit was
  actually completed as planned.
- **Under D-STAGE-ADVANCE = B:** the authoritative "last stage actually presented" now lives on the doctor's **last
  COMPLETED `VisitReport`'s `ContentActuals.StageIndex`** (§4.3 field 6, §4.4). FU02 deliberately writes **no**
  advanced cursor onto the plan atom.
- **Follow-up (`F-STAGE-READ`):** FU05's `PriorStageIndex` resolution needs a **small change to read the last
  completed `VisitReport` instead of the last `PlannedVisit`**. This is a FU05 edit (or a shared read helper FU02
  exposes), **out of FU02's write scope** — FU02 only *provides* the report + the query; it does not modify FU05.
  Recorded so the coupling is not silently dropped when FU05 is next revised. Until F-STAGE-READ lands, FU04/FU05
  keep reading the plan-atom StageIndex (functionally correct while plans are completed as planned; drifts only when
  actuals diverge from the plan — exactly the case FU02 now records).

---

## 21. Legacy Reference (frozen — no code migrated)

Legacy pharma had a **Visit / ActivityReport** step distinct from the MicroTarget plan: the executed visit carrying the
detail actually presented, samples handed over, and doctor feedback — the record that made the next visit's content
advance and fed coverage/adherence stats. vNext splits the concern: the plan atom → **FU01**, generation → **FU05**,
route → **FU03**, content order → **FU04**, and the **executed visit + report → FU02 (this FU)**. FU02 **re-expresses**
that reporting step over the shipped FU01/FU04 seams — **no code, column, or `OldSystem` coupling is migrated**.
Related: [[mod0155-visit-route-planning-program]], [[legacy-visit-planning-analysis]], [[mod0155-fu06-cycle-capacity-pack]].

---

## Handoff

Module pack **`status: draft`** — **NO `runtime_code_allowed`, NO flip stamp** (deliberate; the flip is a separate
Control-Tower step after reviewing this update). **DCP-002 identity gate PASS** (exit 0, first try — no id/name change).
**All 8 D-questions are now SETTLED and LOCKED** (2026-08-29, user + Control Tower — §19.b/§20); every recommended
default was accepted. Frontmatter resolved: **`entity_base: EntityBase`** (D-REPORT-PERSISTENCE = A) and
**`golden_reference: n/a`** (D-CALENDAR-UI = A). The choices are propagated through scope (§2/§3), the execution flow
(§4 + the §4.3 `VisitReport` field set + the §4.4 loop contract), repo scope (§5), lifecycle (§12), authorization
(§14), gateway (§15), and the acceptance criteria + tests (§16/§17).

FU02 is the **execution/calendar counterpart to FU05's setup page**: it displays the FU01 `PlannedVisit` atoms FU05
generated, records **completed/missed/rescheduled**, and captures the immutable **Visit Report** (actual content +
outcome + samples + feedback + follow-up). Its **loop-closing role** (§4.4) is explicit: the actual `StageIndex` FU02
records on a completed visit is what FU04/FU05 read as `PriorStageIndex` next cycle — and it is the FU that finally
makes FU01 §8.5's deferred `LastVisitDate`/`DueStatus` real (F-READINESS).

**Grounded against the BUILT pieces:** FU01 already ships the `draft/planned/confirmed/cancelled/archived` machine +
Create/Update/Confirm/Cancel/Archive and explicitly bans execution fields on the atom (FU01 §2.3); FU04 already ships
`VisitContentSequenceResolver` with `nextIndex = prior + 1`. FU02 **extends** the execution end without duplicating
FU01's lifecycle and **records actuals** without duplicating FU04's arithmetic.

**Newly concrete for Control-Tower review before flip:**
- **`VisitReport` field set (§4.3):** aggregate (`EntityBase`) with `PlannedVisitId`, `ExecutionOutcome`
  (`completed/missed/rescheduled`), `ReportStatus` (`draft/submitted/amended`), embedded `ContentActuals` (incl. the
  actual `StageIndex` + `MatchedPlan`), `Samples[]`, `Feedback`, `Amendments[]`, `ReportedByResourceId` (string),
  `ExecutedAt`. Indexes `TenantId+PlannedVisitId` · `TenantId+ReportStatus` · target-scoped
  `ContentActuals.StageIndex` (no `$ne`).
- **Endpoint list (§15):** `GET …/calendar` · `POST …/outcome` · `POST …/` (submit) · `POST …/{id}/amend` ·
  `GET …/[{id}]` · `GET …/contract`, under one declared `/api/crm/visit-report/{everything}` Ocelot wildcard (+ OPTIONS).
- **RBAC keys (§14):** `crm.visit-report.read` / `.record` / `.amend`; **`record` additionally requires FU01
  `crm.planned-visit.manage`** (it reflects the executed status onto the atom). Definition only — seed/grant is F-RBAC.
- **`F-STAGE-READ` follow-up (§20.1):** D-STAGE-ADVANCE = B means FU05's `PriorStageIndex` resolution must switch from
  the last `PlannedVisit.StageIndex` to the last COMPLETED `VisitReport.ContentActuals.StageIndex`. That is a FU05-side
  edit, **out of FU02's write scope** — FU02 supplies the report + query; recorded so the coupling is not lost.

For development the status must become `ready-for-dev` **and** `runtime_code_allowed: true` (+ `runtime_code_scope`) —
a **separate Control-Tower step** after this update is reviewed. This pack does not take it, and does not commit or push.
