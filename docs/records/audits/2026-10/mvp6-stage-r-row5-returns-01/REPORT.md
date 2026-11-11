# Stage R row 5 — MOD-0186 gets its concurrent conflicting transition, and the race turned out to be one-sided

**Lane:** gate 18.0 row 5 (Concurrency) for MOD-0186 Returns · **Date:** 2026-10-12
**Branch:** `feature/sce/mod-0190-0192-bounded-slice` · **Lane mongod:** slot 4, port 34994, rs `rsq202s4`

## What was missing

`GATE-18-0.tsv` row 5 for MOD-0186 read NOT MEASURED with the note: *"create races executed; no concurrent
conflicting TRANSITION was executed in any record found (the acceptance matrix row R07 claims it; its proof
sits in `evidence/returns-ver-01` outside `docs/records`)."*

Measured and confirmed: every race in `ReturnConcurrencyTests` was a **create** race —
`Create_Races_RespectsExactCap` and `SameKey_TwentyConcurrentCalls_OneAggregateReceiptEvent`. No transition
race existed.

## The conflict chosen, and why it is a real one

`ReturnLifecycle.cs:6` allows `Authorized` to go to `InTransit` **or** `Cancelled`. So two actors can each
hold a legal move while only one of them can be the move that happened — a genuine conflict rather than two
attempts at the same thing. The two actors share tenant and legal entity and differ only in `ActorId`, and
they carry distinct idempotency keys, so neither replay nor key reuse can be what settles it.

## The finding: the race has only one winner, every time

The first version asserted "exactly one move takes effect, whichever wins". It passed — and that was not
enough. Across **thirteen** measured races, `InTransit` won **thirteen out of thirteen**, regardless of which
move was issued first:

| version | races | stored rows afterwards |
|---|---|---|
| `[Fact]`, InTransit issued first | 11 | 11 × `InTransit` |
| `[Theory]`, each target issued first in turn | 2 | 2 × `InTransit` |

So the branch where `Cancelled` is the move that happened had **never executed**, and a test that always sees
the same schedule tests one schedule. Making the issue order a parameter did not change the winner either,
which means the winner is not decided by issue order. **CT did not establish why `InTransit` always wins and
has not guessed** — raised as **Q488**.

## What closes the cell

Two tests, four cases, all green, and the pair covers both directions:

1. **`Transition_Races_TwoActorsOneReturn_ExactlyOneMoveTakesEffect`** (Theory, both issue orders) — the
   concurrency proof. Exactly one winner at 200 and exactly one loser at **422 `INVALID_RETURN_TRANSITION`**;
   the stored status is the winner's; exactly one audit row for that move and it carries the **winning
   actor's** id; audit and outbox each grew by exactly one while entitlement and return counts did not move,
   so the loser wrote nothing; and the used quantity matches the single winner.
2. **`Transition_ConflictIsSymmetric_EitherTargetRefusesTheOther`** (Theory, both orders, sequential) — the
   direction the race could not reach. Whichever legal move is applied first, the other is refused with the
   same 422 `INVALID_RETURN_TRANSITION`, the refusal writes nothing, and the entitlement follows the move that
   happened: `10` when `InTransit` landed, **`0` when `Cancelled` landed**. That last assertion is the one the
   race never exercised, and it passed.

Sequential is the honest instrument for the second claim: it settles the state machine's symmetry and claims
nothing about scheduling.

`ReturnConcurrencyTests` as a whole: **16/16 passed**, evidence in
`evidence/returns-concurrency-suite.log`. Ten repeat runs of the original race are in `evidence/run-1.log`
through `run-10.log`, 10/10 green, 1.13-1.35 s each.

## Cell movement

`GATE-18-0.tsv` sits inside its own lane's `ARTIFACTS.sha256`, so it was **not edited** — editing it would
break a dated audit seal. The transition is recorded in `MOVED.tsv` here, which supersedes that cell.

| module | row | from | to |
|---|---|---|---|
| MOD-0186 Returns | 5 Concurrency | NOT MEASURED | MET |

The acceptance claim at row R07 whose proof sat outside `docs/records` is now evidenced inside it, which was
the note's actual complaint.
