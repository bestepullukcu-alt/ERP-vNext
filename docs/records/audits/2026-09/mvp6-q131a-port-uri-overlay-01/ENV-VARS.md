# Q131a — test database variables (lane-configurable, fail-closed)

WP Q131a · LANE 4 (Cowork, Linux VM) · testing-agent (lead) + devops-agent + security-agent · SOP v2.4 §17.1/§17.3/§17.4/§20/§25/§37.
Overlay on BASE-STACK v1 (`docs/records/audits/2026-09/mvp6-base-stack-v1/BASE-STACK-v1.md` sha256 `80e31c156f08747c934097dcd5a26add8581e357b9f20e9300f4a0fac33534fe`:
BASE a8a236de → Q117 83e6322c → Q121 93bf1c07). **Archive only; not built, not run.** Build/test and independent VER are Q154 on the Mac.

## 1. Variables

| Variable | Read by | Default | Rule (all fail closed with a clear message; the URI is never echoed) |
|---|---|---|---|
| `DITEN_PLATFORM_TEST_MONGO_URI` | every Platform test that used 27017: `Persistence/MongoIntegrationHarness.cs` (+ the classes built on it), 4 × `BusinessReferenceData*MongoTests.cs` (incl. the shared BRD harness), `Schema/PlatformSchemaContractMongoTests.cs`, `Workflow/WorkflowTransitionGateMongoRepositoryTests.cs`, `BackgroundJobs.Tests/PlatformContainerValidationTests.cs`, 2 × `Eventing.Tests/*RabbitMq*IntegrationTests.cs` | **none** | set and non-blank; plain `mongodb://`; no credentials; every host loopback (`127.0.0.1`, `localhost`, `::1`); no port in 27017–27021 (an omitted port counts as 27017) |
| `MVP6_MOD0192_MONGO_URI` | the 5 CapacityPlans Mongo test files (Atomicity/Fault, Concurrency, Isolation, Lease, Restart incl. its child worker) | **none** | as above **plus** `replicaSet=` required (transactions, fail points, two processes). `serverSelectionTimeoutMS` defaults to 5000 as in the old literal unless the URI sets it |

Unchanged and not added: the existing SupplyChain suite variables (`MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`,
`MVP6_MOD0190_MONGO_URI`, `RETURNS_MONGO_URI`, `CLAIMS_TEST_MONGO`). The Platform RabbitMQ tests stay opt-in
(`Eventing__RabbitMq__IntegrationTestsEnabled=true`) and keep their existing `Eventing__RabbitMq__Host/Port/VirtualHost/Username/Password` variables. No new
RabbitMQ variable is needed (their Mongo side now uses `DITEN_PLATFORM_TEST_MONGO_URI`). The old `Eventing__MongoDb__ConnectionString` / `MongoDbSettings__ConnectionString` fallbacks are no longer read by these tests.

## 2. How the kit / CI sets them

`scripts/test-env/mvp6-test-mongo-env.sh` (new in this overlay) prints `export` lines for one slot. It starts nothing, connects to nothing, writes nothing:

```bash
# after the lane's own mongod is PRIMARY (kit K05, or the Mac lane's mongod), in the lane shell:
eval "$(scripts/test-env/mvp6-test-mongo-env.sh --slot "$EK_SLOT" --rs "rs${EK_DB_SUFFIX}")"
# optional: also point the existing SupplyChain suite variables at the same lane mongod (as Q103/Q119/Q121c did)
eval "$(scripts/test-env/mvp6-test-mongo-env.sh --slot "$EK_SLOT" --rs "rs${EK_DB_SUFFIX}" --all-supplychain)"
```

- **Port per slot:** `30994 + 1000 × slot` (slots 1–9 → 31994 … 39994), the evidence-kit rule in `scripts/evidence-kit/k03_ports.py` / `lane.env.example`.
  Two sessions on two slots never share a MongoDB (compatible with R4 / Q146 per-session port ranges). `--port` overrides for a Mac lane that already runs its own port.
- **Refused:** slot outside 1–9, missing or malformed `--rs`, ports 27017–27021, ports ≥ 49152 (OS ephemeral range, as k03 refuses; the old 57192 was in it).
- **On error** it prints nothing to stdout and exits non-zero, so `eval` sets no variable and every test fails closed.
- **URI form:** `mongodb://127.0.0.1:<port>/?replicaSet=<rs>&serverSelectionTimeoutMS=5000`. It has no credentials.
- **The mongod** must be a single-member replica set on loopback started with `--setParameter enableTestCommands=1`: the CapacityPlans fault tests use `configureFailPoint` (Q103: 18 failures without it).
- **CI (`.github/workflows/phase1-gates.yml` → `scripts/run_phase1_gates.sh`)** runs only the tenancy, architecture and web tests; it runs no Platform or CapacityPlans Mongo tests. It therefore needs no variable, and nothing there changes.
- **The evidence kit itself is NOT changed.** `scripts/evidence-kit/` is not in BASE-STACK v1: it is an untracked Q24a install and not in BASE-MANIFEST. Its preimage is **NOT-REACHABLE** per rule 4 and editing it stops. Wiring the `eval` line into `k05_mongo.sh`/`run-kit.sh` (and adding `enableTestCommands=1` to K05) is a separate kit change.

## 3. For Q154 (Mac build/test on BASE-STACK v1 + this overlay)

1. Compose per BASE-STACK v1 §3, then copy this overlay last: `cp` only, 20 files (15 modified + 5 new). Check each member against `OVERLAY-MANIFEST.tsv`.
2. Build:
   - `Diten.Platform.sln` (Application.Tests, BackgroundJobs.Tests, Eventing.Tests)
   - `Diten.SupplyChainService.sln`
   - `TenantArchitecture.ArchitectureTests`
3. Negative control: run without either variable. Expected: the Platform Mongo tests and CapacityPlans Mongo tests fail with the "is not set … fail closed" message, and **no connection to 27017** appears in the dev mongod log.
4. Positive run: lane mongod on the slot port with `enableTestCommands=1`, then set the variables through the script. Expected:
   - Architecture 18/18.
   - SupplyChain = Q121c (407/408; the 1 exclusion is by design) + 9 new pure helper cases.
   - Platform = Q121b after-tree + 11 new pure helper cases.
   - No database created on 27017.

## 4. Q131a checks (this lane, static; evidence in the report)

- **Preimage check:** every preimage was verified against its BASE-STACK layer: 10 × BASE (L3 Auth-22), 4 × BASE (L2 A12-360), 1 × Q117 overlay row. The stack was rebuilt in `/tmp` from the repo's layer archives plus `git archive HEAD`, and it matches BASE-MANIFEST 14,566/14,566 with 0 extra.
- **Port grep:** on the stacked tree + overlay, in Platform tests, SupplyChain tests and `scripts/test-env`, `57192` and `rsmod192` have **0** hits. Every remaining `27017` hit is a refusal, a guard or negative-test data (listed in the report); none is a connection target.
- **Syntax:** tree-sitter C# parse gives 0 ERROR/MISSING nodes in the 19 `.cs` postimages. `bash -n` passes on the script.
- **Guard replay:** a Python port of MongoTestDatabaseGuard over the whole test tree finds 0 offenders and no stale exception entries, before (808 files) and after (812 files).
- **DocsPathGuard:** the overlay adds no `docs/…` references.
- **Secret scan of added lines:** 0 credentials, 0 userinfo URIs, 0 password/secret/token literals.
