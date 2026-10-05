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

## 3. Sequence — Phase 4 is CLOSED

All five modules are built and proven end to end. Five providers registered; the permission
guard's exception list is down to two (S&OP and Capacity only); every module carries views,
a controller and seven languages. Frontend 390/0/390. MOD-0183 has **no open gate row**:
Q372-R2 closed row 13 under the Q388 ruling.

### What is left is not module work

| | | who |
|---|---|---|
| **Q418** four modules have **no §18.0 gate matrix at all** | so "ready" has no denominator for them; MOD-0183's matrix is the template | pack work |
| **Q424** nothing in any code sets `Shipment.CarrierId` | so Claims' carrier path cannot run on real data; R-4c proved it with a lane DB fixture | owner |
| **Q414** `Loads:ReferenceBaseUrl` configured nowhere | Loads cannot create in any environment built from the tracked config | owner |
| **Q389 / Q390** warehouse intake is dead code; the outbox has no transport, 93 rows Pending | both DECLARED at /health; they decide what MVP-6 means for this module | owner |
| **Q399 / Q416** dated to **2026-11-03** | Q357's expiry breaks Returns create AND Loads' shipment picking | owner |
| **Q404** two acceptance rows | reject and the packs contradict themselves | owner |
| **Q384** 2296 vitest cases, CI runs none | the decision CT asked for rested on a false premise | owner |
| **Q423** a test pins the defect Q420 fixed | three lanes hit it; the fix encodes a contract decision | small lane |
| Q391, Q360, Q405, Q395, Q341, Q293 | carried, each small and independent | lanes |

### Phase 5 — MOD-0190 / MOD-0192, and it is not a lane

Q282 ruled it 2026-10-03: **impossible as framed, not expensive.** DEMAND v1 cannot answer
the question both modules ask — no plan-id lookup, no version, no checksum. The Capacity
"reader" is a single hardcoded tuple; S&OP's takes a collection that is empty in a composed
host. Q381 made their absence honest (404, not 500) without resolving it.

Phase 5 opens on a **capability decision about DEMAND**, which is MVP-4 scope. Writing UI
against it now would repeat what this programme has now measured four times: the September
drafts for Returns, Carriers and Claims were each declared complete — one of them CT
ACCEPTED — and each shipped with between five and six defects, 15 to 18 files changed.
Unbuildable UI is the most expensive work in this repository.

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
