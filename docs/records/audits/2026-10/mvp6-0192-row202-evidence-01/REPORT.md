<!-- verify from: this folder -->
# MOD-0192:202 — the run output REP-001 asked for, captured

    lane      : mvp6-0192-row202-evidence-01
    measured  : 2026-10-12
    tree      : 3c69ba7a2
    why       : CT closed :202 on 2026-10-12 citing "6/6, three runs, 45-58 ms" and a sabotage proof,
                and committed no artefact. REP-001 §3 calls that E0. This lane supplies the artefacts.

## What was missing

The discipline self-audit's finding B2: CT reported every measurement from stdout and committed no
`.trx`, `.log` or `.json` beside any of them. REP-001 §3 is explicit — *"Kanıt bir test koşusuysa:
koşunun çıktısı (trx/log/json) kaydın içinde durur; 'testler yeşildi' tek başına E0'dır."*

`SandopCapacitySeparationTests` needs no MongoDB — it uses only `System.Reflection` and
`System.Text.RegularExpressions`, and the `MongoClient`/`GetCollection` strings in it are literals it
**searches source for**, never a connection. So it is the one piece of today's evidence debt that could
be closed while no lane replica set is running.

## Artefacts, with digests

All five in `evidence/` beside this report, measured on tree `3c69ba7a2`, captured with
`--logger 'console;verbosity=detailed'` so each log names every test and carries the assertion
message of every failure:

| file | sha256 (16) | result |
|---|---|---|
| `clean-run-1.log` | `952df9325489a019` | 6/6 passed, 0.9033 s |
| `clean-run-2.log` | `031af037f8202ebc` | 6/6 passed, 0.8006 s |
| `clean-run-3.log` | `25bd4330d24a1f43` | 6/6 passed, 0.7775 s |
| `sabotage-run.log` | `c049563fb56ccba6` | **2 failed, 4 passed**, 0.7726 s |
| `restored-run.log` | `d614d3f49a6ae191` | 6/6 passed, 0.7683 s |
| `outcomes.tsv` | `916f3b18dac0e9f9` | 30 rows: five runs x six tests |

Cite as `docs/records/audits/2026-10/mvp6-0192-row202-evidence-01/evidence/<file>@3c69ba7a2`.

### Why these are `.log` and not `.trx`

The first capture of this lane wrote `.trx`. None of it could be committed:

```
git check-ignore -v evidence/clean-run-1.trx
  .gitignore:26:*.trx   docs/records/.../evidence/clean-run-1.trx
```

`.gitignore:26` excludes `*.trx` repository-wide. Three ways out were open — negate the pattern for
`docs/records/**/evidence/*.trx`, force the add with `git add -f`, or capture a format the repository
already keeps. The third was taken. The first two change, or quietly step around, a repository-wide
decision for one lane's convenience; and the repository's own evidence convention already answers the
question — Q214's `evidence/` folder holds fourteen sealed artefacts, nine `.log`, four `.tsv` and one
`.log.gz`, and not one `.trx`. So this lane follows the house pattern rather than amending the rule
that would have blocked it.

The console logs are not a reduction of the `.trx`: at detailed verbosity they carry the per-test
outcome, the duration and the full assertion text, which is everything the `.trx` held that this
record relies on. The `.trx` files of the first capture stay on disk in the working tree, ignored and
uncommitted; `outcomes.tsv` is derived from the five logs, not from them.

## The sabotage, and why it is targeted rather than broad

A single line — `// sabotage probe: HttpClient` — was appended to
`Infrastructure/Features/CapacityPlans/ConstraintFixtureReader.cs`, taking it from `02be87604a2b` to
`1d2cbfef0195`. Exactly two cases turned red, and they are the two the guard is made of:

```
Failed  SandopCapacitySeparationTests.Every_fixture_reader_answers_from_memory
Failed  SandopCapacitySeparationTests.Neither_module_reaches_a_live_producer(feature: "CapacityPlans")
```

`Neither_module_reaches_a_live_producer(feature: "SandopPlans")` **passed**, which is the point: the
sabotage was in a CapacityPlans file and the Sandop case correctly did not notice. A guard that fired
on both would have been detecting something other than what it claims.

The file was restored from the copy taken before the edit and verified back to `02be87604a2b` with
`git diff --name-only` returning nothing. `restored-run.log` is the proof that the green returns.

## What this closes, and what it does not

**Closes:** the artefact gap behind MOD-0192:202. The tick's claim — six cases green, two of them the
"frozen mocks" clause turned from an inspection into a guard, with a sabotage proof — now has run output
in the record at a named tree.

**Does not close:** the rest of finding B2. These remain reported from stdout with no artefact:

| claim | where | needs |
|---|---|---|
| ten cold runs, 48/48 ×10 | MOD-0192:197, :198 | a lane replica set in the 30994+1000×slot band |
| 19 s → 0.3–0.9 s | MOD-0190 Sandop atomicity | same |
| 9/10 → 10/10, 19 s → 1 s | MOD-0185 Loads atomicity | same |
| 60–86 s → 0.2–0.3 s | MOD-0186 Returns atomicity | same, plus a baseline tree for the before figures |

**Why they could not be captured here.** No mongod is listening in the allowed band. The only two
running are 27017 and 27019, both inside the operational band 27017–27021 that the test fixtures
themselves refuse — `ReturnTestFixture` throws *"Only isolated local Returns Mongo allowed"* on port
27017. CT's own cold replica set on 38994 was killed on 2026-10-12 at or after 12:32 with no shutdown
record in its log, and the five lane mongods on 33994–37994 are gone as well. The machine also carries
911 MiB free disk and 5.99 GiB of 7.00 GiB swap in use, and `log show` returns nothing for CT, so the
cause cannot be confirmed from here.

Starting a replica set into that state would reproduce the failure Q214 already documented: a mongod
that dies mid-run produces a different red set each time, which is the signature CT misread on
2026-10-11 as suite non-determinism and used to retract four ticks.

## Compliance

One product file was edited and restored within this lane, verified byte-identical to the committed
object. No pack line ticked. No `Program.cs`, contract, gateway or hash-pinned file touched.
