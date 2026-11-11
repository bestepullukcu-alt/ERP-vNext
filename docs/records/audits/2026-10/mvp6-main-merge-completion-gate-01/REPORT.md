# The main-merge completion gate — six of seven items, and what the seventh needs

- Measured: **2026-10-16 / 2026-10-17** (Europe/Istanbul), branch `feature/mvp6-logistics`.
- Merges under test: **`681bb44d3`** (lane branch `feature/sce/mod-0190-0192-bounded-slice`) and
  **`1b3408c2b`** (`origin/main` `3912d568b`, PR #146). Both run by the repository owner in the
  primary checkout; `git merge` was refused to CT by a permission rule throughout.
- Governing decision: `docs/records/decisions/2026-10/mvp6-merge-origin-main-owner-decision-01.md`
  (2026-10-05, ACCEPTED) — *"This merge is therefore not housekeeping. It is the MVP-6 integrated
  acceptance gate."* Its seven items are reproduced verbatim in `GATE.tsv`.
- Verdicts: `GATE.tsv`. Measurements: `evidence/`.

## Result

| item | verdict |
|---|---|
| 1 merge conflicts resolved | **PASS** — 0 conflicts across both merges |
| 2 zero known secret material | **PASS** — nothing new entered the tree |
| 3 Platform / Auth regression | **PARTIAL** — Auth 2817/2817; Platform 8645/3/2 of 8650, none merge-attributable |
| 4 gateway validation | **PASS** — 244/244 |
| 5 frontend baseline | **FAIL** — 24 red of 5195; exactly one was the merge's, and it is closed |
| 6 five SupplyChain golden flows | **NOT RUN** — owner decision in session, deferred on disk |
| 7 required MVP-6 gates | **PASS** — SupplyChain 529/1/530, identical to pre-merge |

**No integrated PASS is claimed.** K18 forbids it until re-verification completes, and item 6 has not
run. This record exists so that what is open is named rather than implied.

## What the merge actually changed, and what it did not

`origin/main` touched **zero** files under `services/Diten.SupplyChainService`. Its 32 frontend files
are **all** MOD-0188 Demand Planning plus the seven `SharedResource.*.resx`. The SupplyChain suites
came out **529/1/530 on the merged tree, identical to the pre-merge run** — same count, same single
by-design failure.

So the integration risk the owner's decision warned about — *"SupplyChain source being unchanged is
not SupplyChain runtime being unchanged"* — materialised in exactly one place, and it was not in
SupplyChain's code: MOD-0188's new tenant page met our shared page-help guard.

## The one failure this merge caused

`page-help-ledger` went red on `DEMANDPLANNING`. Measured, both halves:

- the page did **not** exist at the pre-merge HEAD `681bb44d3`
- the guard was **byte-identical** at that HEAD

so the verdict can only have changed because the page arrived. `Help.DEMANDPLANNING.Purpose` is
absent in **all seven** tenant languages — checked per language, 0 everywhere.

Closed by `21d835707` (WP-LANE-MAIN-SYNC-05): the page is named in `MAIN_SYNC_4_PAGES` with its owner
(MOD-0188 / `Diten.PlanningService`) and the fourth sync's reason, keys pinned, list only shrinks,
with its own pin test. Not a `page-help-ledger.md` row, because that ledger's ceiling says in its own
words that every new row after WP-LANE-UX-GUARDS-01 is red again — a row there is a CT ceiling
decision. Not the help text, because the wording is MOD-0188's to choose (XMC-001). **BL-646** carries
it with a 30-day date. Frontend went **25 → 24** red, nothing newly failing.

## The failures this merge did not cause — and how that was decided

Every "pre-existing" below rests on the same two measurements, not on judgement: **the merge changed
neither the test nor the inputs it reads**, so the verdict cannot have changed.

| red | classification | basis |
|---|---|---|
| `wcn-dead-code-and-timer` / `openNew` | pre-existing | CRM's `visit-evidence-settings.js:373` and the guard are byte-identical at `681bb44d3`; the file is identical on `origin/main`. **BL-647** |
| Platform `LoginOnlyEndpointGuardTests` ×2 (E-signature) | pre-existing | merge changed 0 of the controllers and 0 of the guard; both controllers present pre-merge and on main. **BL-648**, risk red |
| Platform `A_hanging_name_source_costs_the_search_at_most_the_bound` | load-sensitive, not a regression | failed at `took 00:00:04.84` against a 4-second wall-clock bound in the full suite; **passed 3 of 3 alone** on the same binaries. **BL-649** |
| 4 frontend files (campaign-targeting, consent-preference, objectives-edit-hydration, pvg-case-intake) | pre-existing | not tree-wide scanners, and none of their inputs is among the 32 files the merge changed |
| 7 frontend files (global-confirm-text, planning-cycles ×2, strategy-apis, strategy-periods ×2, wcn-calendar-view) | **UNATTRIBUTED** | these are tree-wide scanners, so the 32 new files could in principle reach them. Settling it needs a full-checkout baseline at `681bb44d3`; free disk was 3.9–6.2 GiB and a full worktree is ~4.2 GB. Not done, and not guessed. |

## A retraction, because the method was wrong before the answer was

CT first attempted the attribution by checking out `origin/main` into a **sparse** worktree
containing only `frontend/`, running vitest there, and reporting *"main has 108 failures across 33
files; 23 of ours are inherited and 85 that are red on main are green here."*

**Those numbers are void.** Several of those tests read files under `services/Diten.Platform/`, which
the sparse checkout excluded, so they failed at **collection** rather than on their merits — proved by
running two of them in that worktree and getting `Tests  no tests`. The contaminated run was deleted
and the attribution above was redone from commit-level facts instead, which need no baseline run at
all.

## The four hand-checks the decision names

- **`ocelot.json` / `OcelotConfigurationTests`** — `5068` added for Planning, `5066` intact,
  shipment-bundle routes intact, **0** duplicate upstream templates. The decision's bucket 3
  ("semantic merge, never ours/theirs wholesale") is satisfied by inspection.
- **`AGENTS.md`** — main's change is the pack filename convention (`{ID}` → `{ID}-{slug}`) and the
  Planning 5068 row. Our packs already use `{ID}-{slug}`.
- **Q302** — eager JWT validation present in both `HumanCapitalService` and `TalentEcosystemService`
  `Program.cs`. Present; the decision also wants it runtime-tested, which item 3 covers for Auth and
  Platform but not for those two services specifically.
- **Q363** — `IInternalScopeResolutionContext` is at **zero** references. It was **not** lost in the
  merge: **R-2b (`2acce233b`, 2026-10-10)** removed the whole internal legal-entity service-identity
  path under an owner decision whose condition Q481 had measured (no production consumer). Absent at
  the pre-merge HEAD, absent on `origin/main`. The 2026-10-05 clause that Q363's delta "must survive"
  is **superseded by that later decision**, not violated — and R-2b records that all eight removed
  files were added by this branch, so main's behaviour did not change.

## Item 6, deferred rather than attempted

`evidence/06-golden-flows-not-run.txt` lists exactly what it needs: the six `launch.json` services
(SupplyChain is not one of them and must be started outside), a loopback credential helper, a mongod
per data-owning service, a **commercial-plan** tenant (the default answers
`QUOTA_CONFIGURATION_MISSING`), a Platform-admin token minted by Q358's method, a tenant Admin actor,
and a browser.

**A correction CT owes this record:** earlier in the session CT said item 6 would probably hit the
Q496 authorization. It does not. Q496 is about the Stage R lane **account** being role `Viewer` with
0 of 25 `supplychain.*` keys; the golden flows never used that account — `mvp6-r4a-carriers-ui-01`
registered its own STARTER tenant through Platform's admin API and signed in as `r4a-admin`. Two
different walls, and only the disk one is in the way.

Free disk during this gate run was **3.9–5.3 GiB**. `mvp6-stage-r-readiness-01` warns in its own words
that starting the stack at 2.1 GiB *"would most likely reproduce"* the ENOSPC that silently killed a
lane mongod (Q490, FTDC id 9399800, `"s":"F"`). The volume has filled **three times** in this
programme. A stack started without headroom produces a false red, not evidence — so it was not
started.
