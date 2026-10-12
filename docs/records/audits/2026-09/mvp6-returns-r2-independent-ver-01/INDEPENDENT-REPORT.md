# Independent Returns R2 verification
Verdict: REWORK — executable semantic model/evidence defect. No runtime defect or deployment judgement asserted.

## Fresh executed evidence
- `python3 verify_r2.py`: exit0,293 checks,174examples,279refs. stdout/stderr retained in returns-r2/fresh-*.
- `python3 independent_checks.py`: final exit0;6Root schema cases agree;8 deliberately broken disposable model variants rejected with exit1. Each variant has mutation.stdout/stderr. First probe attempt failed KeyError due allOf schema traversal; fixed verifier only, candidate unchanged; no false PASS inherited.
- Actual Store.mutate tested against canonical-extracted64 source×target pairs:64/64,7edges. Independent-lifecycle64.json. InTransit→Cancelled422.
- root dependency schema dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb; semantics fceac091e75a4407210c80b81ca6b6757cc7199d4e09546dda39f3f8271c269b.

## F01 — UUID root equivalence lost on committed receipt/transition
Sources: returns-r2/returns_model.py:44 and :51 use raw string inequality; root_status:34 uses UUID semantic equality. semantics-candidate.md:16 allows hexadecimal case; :30 promises same-root replay; :31 reserves mismatch for different root.
Reproducer: create quantity1, Delivered source root `aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa` =>201; same scope/key/body, uppercase equivalent incoming UUID =>409 CORRELATION_ROOT_MISMATCH. New Authorized transition with uppercase equivalent root likewise409. root-case-witness.json records exact observed results.
Expected: same-root201 REPLAY with no extra writes; fresh authorized transition200. Uppercase representation does not make a different UUID.293 suite fails to cover this precision boundary.
Closure: separate candidate-author rework only; compare parsed UUID identity consistently for stored/receipt/current root; preserve raw response trace independently. Add lower/upper combinations for fresh create, create replay, transition, transition replay, including changed-payload precedence (same UUID changed payload =>IDEMPOTENCY_KEY_REUSED) and genuinely different UUID =>CORRELATION_ROOT_MISMATCH; replay write count stable. Regenerate new byte hashes, independent VER required; no approval transfer.

## Bounded positive findings and limits
Canonical lifecycle, numeric quantity/instant fingerprint, positive quantity, exact UoM, rejection release-once, closed retention, cap4+6/6+6 serialized scenarios, fault rollback and unknown-commit receipt recovery have fresh model checks. Manual Received is a permission-controlled model command, not Warehouse proof. Normative annex explicitly requires opaque Inventory/Warehouse references and zero HTTP, audit retention and Pending-only outbox. Model imports no HTTP client; zero-call counter is model evidence only, not runtime instrumentation. Stored model records do not establish full opaque/audit retention. Source snapshot drift/required-field classification, full auth/header/JWT oracle, event causation, number collisions and per-write failure injection remain normative unmodeled requirements. Model concurrency is serial; model rollback not Mongo transaction proof. No DB accessed.

## Scope/no-change
All execution and intentional mutants restricted to this unique disposable directory. Source repository, archive and candidate source were not edited. Extracted candidate verifier writes its disposable tests.json/lifecycle-fixtures.json/apply-check only, as inspected before execution. Parent verifier owns archive integrity/baseline/no-change inventory and permanent SOP22 manifest. No runtime/publication/guard/pack/git work performed; no consumer uptake or DEV GO.
