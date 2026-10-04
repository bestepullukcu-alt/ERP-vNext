# MVP-6 replan — 2026-10-04

> **This file exists because K21 was never applied.** The SOP requires every CT turn to
> close with a replan and a next-work calculation. In 483 ledger rows, zero carry one.
> CT answered "what is next" in chat all day, which is also K11: chat is not the
> system of record. This file is the record. It is rewritten, not appended, each turn.

## 1. Measured state, 2026-10-04

| module | service | gateway | UI | gate |
|---|---|---|---|---|
| **MOD-0183 Shipments** | composed, reachable | 2 routes → 5061 | 10 views · 4 js · controller · **7/7 languages** | rows 1, 8, 10(waiver), 12, L10n-status closed; **row 13 open** |
| MOD-0184 Carriers | composed, reachable | **none** | **none** | — |
| MOD-0185 Loads | composed, reachable | **none** | **none** | — |
| MOD-0186 Returns | composed, reachable | **none** | **none** | — |
| MOD-0187 Claims | composed, reachable | **none** | **none** | — |
| MOD-0190 S&OP · MOD-0192 Capacity | MediatR-excluded | none (404 since Q381) | none | behind `IDemandFixtureReader` |

Of the gateway's 271 routes, exactly **two** serve this programme.

## 2. The critical path, and it is not what the plan said

Step 4 was written as "the four modules' **UI revisions**". That was wrong, and CT wrote
it. There is nothing to revise. Each of the four needs, from zero, the same six things
MOD-0183 received today:

1. a gateway route pair
2. a Web controller (the adapter, with its 5xx-envelope mapping — Q371)
3. views
4. view scripts
5. a `.resx` set in **seven** languages
6. a manifest provider **and** its `AddSingleton`, which `MOD-0184:473` forbids shipping
   ahead of the UI — so for Carrier the UI is the precondition of its own permissions

**The optimisation is that this sequence is now measured, not guessed.** MOD-0183 paid the
discovery cost: the 403-as-validation-error, the idempotency key per page, the skeleton,
the UTC round trip, the seven-language resx, the adapter envelope. Doing it four more
times by discovery would pay it four more times.

**So the next unit of work is not a module. It is the recipe.** One lane extracts, from
MOD-0183's committed files and the Q362/Q371/Q374 records, a written sequence with the
defects already named — then one module is built against it as the proof the recipe is
real, and only then do the remaining three run in parallel.

## 3. Sequence

Corrected by R-1 (Q386): CT's previous order said "the gateway file is shared, so route
additions are sequenced." **There are six shared surfaces, not one**, and R-4's three
parallel modules would have collided on the other five. The recipe measured them from
what MOD-0183's commits touched outside its own folders.

### Phase 0 — finish MOD-0183 (in flight)
| lane | closes |
|---|---|
| **Q372-R2** | §18.0 row 13. PAUSED mid-run: the controller edit is in the working tree and a suite mongod is still up on 57373. O-1 is already measured (Web 1 / Gateway 1 / SupplyChain 8, sabotage 0/1/8), O-2's p95 measured for the first time. |
| Q361 | the zero-margin p95 assertion that makes every suite comparison unreliable |
| Q360 | the expiry guard; without it the Q357 retraction passes unnoticed on 2026-11-03 |
| **new, from R-1** | **F-R1-2**: Q371's idempotency fix is half-done. `create.js:76` mints a new correlation id per Save; `ShipmentRepository.cs:70` answers 400 to a known key with a different id. Q371 measured the edited-payload path (409, correct) and never measured the unchanged retry — the recovery case the fix exists for. K4. |

### Phase 1 — cross-cutting, ONCE, before any module
`frontend/Diten.Web/Program.cs` carries two defects every module would otherwise inherit:
- **2.4** the JSON-401 marker has no consumer (`:72-78` has no `OnRedirectToLogin`), which
  is the real cause of the "pre-existing" frontend test failure (F-R1-1)
- **2.5** the adapter inherits `HttpClient`'s 100 s default; a failed list load took 90 s

Fix both here and no module touches this file again. Doing it after R-2 means doing it
four more times.

### Phase 2 — R-2: ONE module, end to end, against the recipe
**Returns (MOD-0186).** It depends only on what is already shipped: its service is composed
and reachable, its contract pin is current (Q380), and `supplychain.shipments.read` — which
Q279 found Returns needs — already exists.

### Phase 3 — R-3: amend the recipe from what R-2 actually cost
Only the lines R-2 paid for that R-1 did not predict. A recipe that is never corrected by
contact with a second module is a guess with a table around it.

### Phase 4 — R-4: Carriers, Loads, Claims
Module-scoped work runs in parallel: controller, views, scripts, module resx ×7, view
models, provider and its tests.

**Sequenced, one writer at a time:**

| shared surface | why every module touches it |
|---|---|
| `gateway/.../ocelot.json` | one route pair per module |
| SupplyChain `Program.cs` | one `AddSingleton` per provider; already carries the MediatR exclusion and the Q381 filter |
| `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` | nav keys per module — **seven separate conflict points** |
| `_DataTableL10n.cshtml` + `dt-defaults.js` | any module needing a new DataTable key |
| `DefaultRolePermissionTemplate.cs:41` | only on the `AdminModules` path; the entitlement route touches no shared file |

**Order constraint that is not a file collision:** Carrier's provider must ship before
Claims' carrier path.

### Phase 5 — MOD-0190 / MOD-0192
Behind a production `IDemandFixtureReader` (Q273). Q381 made their absence honest — 404
rather than 500 — but did not resolve it.

### Gating the sequence, not inside it
| item | blocks | who |
|---|---|---|
| **Q384** 2296 vitest cases exist, CI runs none | Q376, and the honesty of every frontend suite number | Owner |
| **Q388** row 13 ruling: wiring vs demonstration | whether row 13 closes with 4 of 8 instruments unexercisable | Owner may overrule CT |
| **Q389** warehouse intake is dead code · **Q390** outbox has no transport, 93 rows Pending | what MVP-6 actually means for this module | Owner |
| **F-R1-3** Carrier nav keys shipped in 7 languages with no Carrier UI; `NavManifestL10nGuardTests` forces it, `MOD-0184:473` forbids it | Phase 4's Carrier work | Owner |
| **Q379** historical timestamps · **Q357** retraction, expires 2026-11-03 | — | Owner |

## 4. What CT changes about itself

Twelve ledger rows name a CT failure today. They share one shape — **a measurement that
could not have shown the opposite**:

- absence concluded from a search that never looked for presence (Q333, Q383)
- a probe that renders "absent" and "empty" identically (Q345)
- one code path read and attributed to another (Q375, Q383)
- a path or line number written without opening it (Q340, Q345, Q383)

Three controls, each falsifiable:

1. **An absence claim names the search that would have found it.** "Not registered" is
   written as "`grep -rn AddScoped.*IFoo services/` returns 0 lines", not as a conclusion.
2. **A dispatch premise cites file:line and is pasted, not paraphrased.** Q344 and Q383
   both began as a paraphrase of something CT had not opened.
3. **This file is rewritten at the end of every CT turn.** That is K21, and it is what
   makes "what is next" answerable without asking CT.
