# Q484 — the Capacity context middleware is wired, and the re-pin must not happen

**Lane:** MOD-0192 :199 / :200 · **Date:** 2026-10-12 · **Branch:** `feature/sce/mod-0190-0192-bounded-slice`
**Authorization:** owner released the integration-owner surface for "the Capacity branch + re-pinning the two
hashes" in session on 2026-10-12.

**Status: the branch is in and evidenced. The re-pin is REFUSED by this lane, and the owner needs to read §5
before the branch is kept.** Nothing in this lane is committed.

## 1. The defect, measured rather than inherited

`Program.cs` wired five context middlewares — Carrier (:132), Load (:133), Return (:141), Claim (:142) and a
Shipment catch-all (:143-145) — and the whole of `src` held exactly one reference to
`CapacityContextMiddleware`: its own declaration. A Capacity request was therefore MAPPED but not CONTEXTED.

Measured on lane slot 4 (`mongodb://127.0.0.1:34994/?replicaSet=rsq202s4`) with the pre-change tree
(`Program.cs` sha256 `c846af73…`):

```
POST /api/supply-chain/capacity-plans  (valid token)
  Route matched with {action = "Create", controller = "CapacityPlans"}
  Handling CreateCapacityPlanCommand CorrelationId=00000000-… TenantId=00000000-… LegalEntityId=00000000-…
  Operation failed: InvalidOperationException
  → 500
POST /api/supply-chain/sandop-plans   (valid token) → 201
```

So the handler ran on an unpopulated `CapacityRequestContext` — no tenant scope at all — beside a sibling
module that answered 201. `SandopCapacityHostCompositionTests` had recorded that 500 on purpose, as a guard
that the known gap could not outlive its reason, and it fired the moment the branch went in.

**A correction belongs here.** Reading `Program.cs:152-157` (the Q381 controller filter, whose comment quotes
a *pre-filter* measurement of this same 500) and `Program.cs:82-85` ("Capacity is NOT composed"), CT began to
withdraw the defect report as a stale comment. That was wrong, and measuring settled it: the controller is
kept by the filter and the 500 was live. The lesson is the cheap one — two comments disagreed, and the run
was the only thing that could decide between them.

## 2. The change

`Program.cs`, +12/-1: one `using`, the branch, and Capacity added to the catch-all's exclusion list.

| | sha256 |
|---|---|
| before | `c846af732fb833fa4a3c4b4a15bcd7f70b4a2f5bec277598f62c4957e5fdd18a` |
| after | `0d66f096247b644c6192abb8ec50974539318beb60f1047679adb653142a3dd4` |

## 3. The guard was rewritten, and it cannot write

`SandopCapacityHostCompositionTests` now asserts the contract instead of the defect. It proves the branch is
in the pipeline **without creating a plan**, which is forced rather than stylistic:

`CapacityRepository.cs:82-83` checks duplicates on *(scope, horizon start, horizon end, demand plan id, demand
plan version)* and `demand.IsExact` pins all four to the fixture's values, so **exactly one capacity plan can
exist per scope for this fixture**, whatever its scenario name. CT measured the consequence the hard way: a
creating probe in this test consumed the single slot and `CapacityCorrelationTests` answered **409
CAPACITY_PLAN_ALREADY_EXISTS** in the shared set while passing when run alone. That is also why the original
guard could assert 500 — asserting a refusal costs no slot.

Two wrong diagnoses were made and discarded on measurement before the real one: first the unique *name* index
(wrong — the duplicate key is the horizon/demand tuple, not the name), then a collision with
`CapacityIsolationTests` (wrong — it uses database `DitenSupplyChain_Mod0192_Test` while the HTTP host uses
`DitenSupplyChain_Mod0190_0192_Http_Test`; they never meet). The comment asserting the second diagnosis was
reverted rather than softened, so the correlation test's diff is now the un-skip and nothing else.

The guard instead sends an **unauthenticated** request and asserts the contract envelope.

## 4. Sabotage proof

Both lines of the fix were disabled in place (`Program.cs` → `dcdcedc03d239171`), rebuilt, measured, then
restored from the pre-sabotage copy back to `0d66f096247b644c`, verified by digest.

| | with branch | branch disabled |
|---|---|---|
| `Capacity_controller_is_mapped_and_its_context_middleware_is_composed` | pass | **fail** — 401 with no code and no body |
| `Correlation_survives_request_response_audit_and_event` | pass | **fail** — 500, expected 201 |
| `Malformed_correlation_is_rejected_before_any_write` | pass | **fail** — 500, expected 400 |
| `Sandop_controller_is_mapped_and_serves_a_request` | pass | pass |

The Sandop case staying green is what makes this a targeted sabotage rather than a broad break.

The sabotage also corrected the guard's own reasoning. CT had written that the catch-all would answer
"Authentication required."; measurement showed a **bare 401 with no body at all** — `ShipmentContextMiddleware`
returns early on an endpoint carrying no Shipment permission attribute, and the authentication middleware
refuses it bare. The status is 401 either way, so only the code discriminates. The assertion was rewritten
onto the code and the comment onto the measurement.

## 5. The re-pin is refused, and the gate needs the owner

The owner also released "re-pinning the two hashes". **This lane will not do it, because an owner decision
already says the opposite.**

`MOD-0192:273` and `MOD-0190:261` carry `Accepted composition (Program.cs, integration-owner surface)` —
`50c48a2b8958…` and `a2a216be15d0…`. These were never live guards on the file: Q213 already measured
`Program.cs sha256 7fdb5ef0… ≠ accepted composition 50c48a2b…`, and the pre-change tree was `c846af73…`,
matching neither. They record a **target shape**, and the owner decided their fate on 2026-10-03:

> **CT-QUEUE Q292, owner approved, decision 5 of 7:** "MOD-0190 section 22 and MOD-0192 section 22 keep their
> accepted composition — it is correct as a TARGET … but it is GATED: uptake is blocked until MOD-0188 exists
> and DEMAND v1.1 carries version and checksum, plus a SUPPLY-CONSTRAINTS contract for 0192."

and Q298 recorded the execution as "the gate is written into both packs **without withdrawing the accepted
composition or altering its hash**". So re-pinning would overwrite an owner decision with a newer one taken
without that decision on the table. CT put the authorization request to the owner without surfacing the gate;
that omission is CT's.

**What the owner now has to weigh.** `REACHABILITY.md:89-91` names the accepted composition as five things in
`Program.cs`: `AddCapacityPersistence()`; the two fixture readers; `AddHostedService<CapacityEvaluationExecutor>()`;
**the Capacity middleware branch**; the Capacity model-error adapter. This lane took up **item four of five
and nothing else.**

Measured, in the module's favour:

- Items 1-3 are the harmful ones. Q283: `CapacityEvaluationExecutor` is referenced nowhere, so if Capacity
  were composed, evaluations would return 202 and **never complete**, with no startup error. None of the three
  was touched.
- In the production composition the branch is **inert**: Capacity persistence is not registered, so the Q381
  filter drops `CapacityPlansController` and no capacity endpoint is mapped;
  `CapacityContextMiddleware.InvokeAsync` returns `next(http)` when the endpoint carries no
  `CapacityPermissionAttribute`. Its only live effect is in the test host, which composes the module itself.
- Item 5 is still absent, so a schema-invalid Capacity request still answers with the Shipment envelope.

Against it: the gate's words are "uptake is blocked", and the branch is one of the five named items. Whether
one inert item counts as uptake is the owner's reading, not CT's — and CT is the party that benefits from the
permissive one. F-Q298-1 is also on the record: `Program.cs` went `a2a216be… → 50c48a2b…` *through* the
Capacity patch, so MOD-0192's target contains MOD-0190's and a full uptake would compose S&OP too. A single
branch does not.

**Three ways forward, for the owner:**

1. Keep the branch as an exception to Q292, hash unchanged, recorded as such. :199/:200 close.
2. Move the fix into the test host (`SandopCapacityHttpHost`, e.g. an `IStartupFilter`) so the gated
   production composition is untouched. :199/:200 close on a test-only seam.
3. Revert; :199/:200 stay open until the gate opens.

**Correction to the three options above, found after they were written.** `SandopCapacityHttpHost`'s own
header records a reading CT had not carried into this report: *"The §22 composition uptake gate (Q292) binds
the production host and stays shut; **the owner read it on 2026-10-11 as not binding a test host**, which is
what this is."* That changes the balance between the options rather than the refusal of the re-pin:

- Option 2 is not a novel idea needing a fresh ruling — it is the shape the owner has **already** read as
  outside the gate, and the host it would live in exists and is in use.
- Option 1 is correspondingly starker than this report first made it sound. The branch CT added is in
  `Program.cs`, which is exactly the production host that the same reading says the gate **does** bind. So
  keeping it is not a small continuation of an existing reading; it is a new exception to a shut gate.

CT presented option 2 as an alternative of equal standing when it in fact carries a precedent, and option 1
as lighter than the owner's own reading makes it. The recommendation, stated plainly so the owner can reject
it: **option 2.** The defect, the sabotage proof and the rewritten guard in this lane all transfer to it
unchanged; only the two lines move out of `Program.cs`, and the hash the owner decided to keep then stays
untouched by construction rather than by exception.

## 6. What is not closed

`:201` needs the g5 tool, which lives on the walker branch. `:203` needs MOD-0178. Neither moved.

## 7. Environment

Lane mongod started by CT on slot 4, port 34994 (rule `30994 + 1000*slot`), single-member replica set
`rsq202s4`, loopback, `enableTestCommands=1`, cache capped at 0.25 GB. The operational band 27017-27021 was
not used; only 27017 was listening and it is forbidden. Free disk moved between 1.8 and 6.4 GiB during the
lane, which is worth watching — the Returns suite needs ~500 MB for `createIndexes`.

## The production container, measured against the branch (added 2026-10-13)

`CompositionRootGuardTests` builds the **production** container from `Program.cs` and asserts that every
controller constructor parameter resolves, that every optional `Diten.*` dependency is registered, and that
the legal-entity validator is the real MDM one. With the capacity branch in place: **3 of 3 passed**
(`evidence/composition-root-guard.log`). So the branch does not alter what the production host composes,
which is the substance of the Q292 gate's concern, independently of how the gate's wording is read.

Finding on the way, and it is not this lane's to fix: the three guards do not run in an ordinary lane. They
fail closed with *"SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI is not set. Point it at a disposable mongod (never
27017); this guard must not be skipped."* — and `scripts/test-env/mvp6-test-mongo-env.sh`, the script the
suites' own error messages tell you to use, does not print that variable, not even under
`--all-supplychain`. So every full-suite run from the documented setup shows these three red, which is the
state in which a guard stops being read. Raised as Q492. CT could not add the line: writing to
`scripts/test-env/` is refused by a deny rule.
