# MVP6-BC-SUCCESSOR-INDEPENDENT-VER-01 — SOP §22

## Independent verdict

**PASS — the exact bounded BC successor source set is independently verified.**

This is an independent technical verdict for the approved three-file successor
integration. It is not Control Tower acceptance, full-module acceptance,
common-checkout integration, rollout, E5 or G5.

## Mode and baseline

- Role: writer-independent verifier.
- Repository mode: source and writer-checkout read-only; verifier outputs were
  written only to this owned audit directory and the disposable `/private/tmp`
  workspace.
- Repository branch/HEAD: `feature/mvp6-logistics` /
  `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Writer source archive:
  `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064`.
- Controlling 422-row manifest:
  `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`.
- Writer package manifest:
  `d7e39b9e815367d5e99a948b134d622fb7224fa1fdab252a4b9aaf23f066fec5`.
- Writer raw evidence archive:
  `e2c57251d0723eb41369645112fcd2b273cdba1e5df110c77a18fd9379fe76b9`.

## Source and scope verification

The archive was extracted into a new disposable directory. All **422/422**
manifest entries existed and matched; missing and mismatched counts were zero.
The three authorized target hashes matched:

1. `CapacityContractError.cs` — `c55ad161…`;
2. `CapacityContractTests.cs` — `4d57bf51…`;
3. `CapacityConcurrencyTests.cs` — `45329002…`.

The protected hashes also matched:

- `Program.cs` — `50c48a2b…`;
- hosted/read-fault `CapacityAtomicityTests.cs` — `2c133ac1…`;
- accepted S&OP `SandopAtomicityTests.cs` — `eba2da6f…`.

The source evidence is in `SOURCE-VERIFICATION.json`,
`VERIFIED-SOURCE-MANIFEST.tsv` and `CHANGED-PRESERVED.tsv`.

## Fresh build and test identity reconciliation

- SDK: native .NET `8.0.417`.
- Build: **PASS**, zero warnings and zero errors.
- API DLL: `fb6a73d7f3783facef11f638e220c887c3620ed49f87fd6f320513fee106146b`.
- Test DLL: `559eff471815055158ff0edad12125da20ee46369bcd1aea52bf0811a88746da`.
- CapacityPlans TRX: `027db6cb73222588daecdb0695135ff46f3be81a35ab74522f0cd6826fba9608`.
- Result: **39 passed, 0 failed, 0 skipped**.

The 39 TRX identities include three X01 cases, 21 X07 cases, the hosted
atomicity case, deterministic duplicate precedence, the proven unique-index
race, the exact message contract case and the separate-process restart cases.
Historical B `36` and C `34` counts were not summed and were not relabeled as
successor evidence.

## Independent runtime evidence

A verifier-owned MongoDB 8.0.18 replica set was initialized as PRIMARY on
`57292` with test commands enabled. The API used `57032` and a fresh database
`DitenSupplyChain_BC_Successor_VER01`. Neither writer process nor writer data was
reused.

Both API processes executed the verifier-built DLL `fb6a73d7…`. Authenticated
HTTP independently proved:

- plan and scenario creation;
- same-key/same-payload replay of the original scenario;
- same-key/changed-payload `409 IDEMPOTENCY_KEY_REUSED` before name-conflict
  evaluation;
- new-key/exact-name `409 CAPACITY_SCENARIO_NAME_CONFLICT` with exact message
  `Capacity scenario name already exists in this plan`;
- request/header/body correlation equality for all eight calls;
- restart with the same binary, database and persisted fixtures;
- post-restart plan read, receipt replay and repeated duplicate-name response.

Successful scoped Mongo queries measured `0 → 1` plan, `0 → 1` scenario,
`0 → 2` receipts, audit and Pending outbox records. Counts stayed unchanged
after replay, conflict and restart; evaluations and active slots remained zero.
Every query exited zero. The raw HTTP/DB record is
`RAW-EVIDENCE.tar.gz::raw/http-restart-evidence.json` with SHA-256
`7a5ce6478e9218ad31bc76d041270db23d835234cce59570b06c2b39c27ba9d2`.

## Failed attempts retained

Two verifier-harness failures are preserved and excluded from controlling
results:

1. the initial `--no-restore` build lacked `project.assets.json`; restore was
   then performed and the fresh native build passed;
2. the first persistence query appended the database after URI query
   parameters, so mongosh selected `test`. Its zero counts triggered the
   fail-closed assertion. The URI was corrected without changing product or
   test source, and the complete probe passed.

No failed attempt was hidden or counted as product failure/pass.

## Evidence boundaries

`ACCEPTANCE.tsv` is controlling. The independent run verifies the bounded
successor integration, exact duplicate response, replay/error precedence,
race, X01, X07, hosted/read-fault behavior and restart persistence. It does not
promote inherited B/C evidence into a broader acceptance claim.

The verifier did not edit the source archive, writer checkout, repository
source, `Program.cs`, contract, guard, gateway, UI or shared permission files.
No commit, push, stash or branch operation occurred.

## Cleanup and no-change

- Both API processes terminated.
- Verifier databases were dropped.
- TCP ports `57032`, `57192` and `57292` had no listeners after cleanup.
- Repository branch and HEAD remained unchanged.
- Existing dirty work was preserved. Only
  `docs/records/audits/2026-09/mvp6-bc-successor-independent-ver-01/`
  was created by this verifier.

## Permanent evidence

- `RAW-EVIDENCE.tar.gz` —
  `638267416f3ac9b7da8232875c1e7ed6f64e4d383d7ab179191ec9fdab290f8c`.
- `VERIFIED-SOURCE-MANIFEST.tsv` —
  `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`.
- `BINARY-MANIFEST.tsv` —
  `6d39b76e1211887e07ae97c885352cccc504e7e2dcfeb5bb6e52c3841bc9894e`.

**Recommendation to CT:** the exact bounded BC successor may proceed to its
separate CT disposition. Do not infer full-module, E5/G5 or rollout approval.
