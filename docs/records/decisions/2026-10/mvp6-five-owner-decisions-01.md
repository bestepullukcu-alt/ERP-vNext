# Owner decisions — five, taken 2026-10-04

- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q368, Q372, Q374, Q378, and Q218 reclassified

## 1. `dotnet-counters` — INSTALL

Installed 2026-10-04, version `10.0.745401`, at `/Users/natig/.dotnet/tools/dotnet-counters`.
Not on the default PATH; `~/.dotnet/tools` must be added.

Reversible with `dotnet tool uninstall --global dotnet-counters`, local only, nothing added
to any service. It unblocks Q372 and with it three of row 13's four signals. An exporter is
the alternative and a bigger decision — it adds a dependency to the service and contradicts
§8.1's own stated reading path. It stays a production-operations question, not a condition of
MOD-0183's closure.

## 2. Frontend behaviour testing — NODE, NARROW SCOPE

Rejected: a browser harness (too large for the problem) and Jint (its time-zone behaviour
rests on .NET's, and time zones are the thing under test).

Chosen: expose the pure date functions and run them under Node at **two** fixed zones —
`TZ=Europe/Istanbul` and `TZ=UTC`. Both are required. A test that only runs at UTC stays
green while the defect is present, which is the Q361 shape: a test that cannot fail for the
reason it exists.

**The second half of this decision costs something and is accepted:** DOM behaviour remains
proven only by live lane runs. Every frontend behaviour proven today was proven by a lane
driving a browser, never by the suite. That has to be written where a reader of "161/1/162"
can see it, or the number keeps implying coverage that is not there (Q378).

## 3. Q374 historical timestamps — ASK, DO NOT WAIT

The fix is in the tree and no new record is written through the broken path. For records
already written, one question goes to whoever owns deployments: has any environment taken
Change Status or Capture POD through this page from a browser outside UTC?

- **No** → closes here.
- **Yes** → not a fix lane. It is retrospective timestamp identification on regulated records
  and probably a correction record, and that is an owner decision, not a CT dispatch.

Do not close it unasked. Do not block other work on the answer.

## 4. Capacity / S&OP routes — 404, NOT 500

Q273's own reasoning decides this: a module that silently refuses everything is worse than
one plainly unreachable. **A 500 is not plainly unreachable either** — it reads as a server
fault, wakes an on-call engineer, and invites a consumer to retry what will never succeed.

404 is correct because it is true: the module is not present in this deployment. The clean
implementation is to not map the controllers of uncomposed modules, so the fact lives in one
place — the composition in `Program.cs` — rather than in a second "which routes are off"
list that will drift from it (K6).

This does not resolve Q273. `IDemandFixtureReader` is still missing. It fixes what the
absence looks like.

## 5. Q218 — NOT A DECISION

The decision was taken 2026-10-02 and adopted: re-pin both packs to 3.1.0, do not revert the
file. It was never executed. MOD-0186 and MOD-0187 still carry `5dfe7c1b` (3.0.0) in nine
places while the contract file in the tree hashes to `6dc1dd48` (3.1.0).

**The lesson is larger than the drift.** The ledger said `DONE (recommendation adopted)` and
CT read that as work completed for two days. An adopted recommendation is not a performed
action, and a ledger state that conflates them will do this again.
