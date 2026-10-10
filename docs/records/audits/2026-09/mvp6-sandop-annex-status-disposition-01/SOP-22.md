# MVP6-SANDOP-ANNEX-STATUS-DISPOSITION-01 — SOP §22

**Verdict: CANDIDATE READY FOR OWNER DISPOSITION; canonical NOT CHANGED.** A narrow, annex-only [unified patch](annex-status.patch) and [complete target candidate](candidate-annex.md) reconcile the false publication-status wording with the actual MOD-0190/MOD-0192 consent and prior exact two-file application. The earlier final-pack, VER, publication and historical candidate records remain intact. The new annex hash is **not** covered automatically by earlier consumer consent or publication authority.

## Baseline and authority

- Branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Existing dirty checkout preserved; staged index empty. No branch switch, commit, push or stash.
- Canonical annex baseline SHA-256 `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`; canonical YAML SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`. Both were rehashed before and after candidate construction and remain unchanged.
- The real owner message granted separate MOD-0190 and MOD-0192 consent and canonical publication for those **original** exact bytes. [Publication-exec-01](../mvp6-sandop-capacity-publication-exec-01/SOP-22.md) records that the bytes are present but the annex still says `UNAPPROVED`/noncanonical and production DocsPathGuard is red. This candidate addresses only the annex narrative conflict, not the guard.
- Output is solely this new audit directory: `SOP-22.md`, `candidate-annex.md`, `annex-status.patch`, `VALIDATION.md`, `OWNER-DECISION-UNAPPROVED.md`, `MANIFEST.tsv`, `SHA256SUMS`.

## Exact change and semantic boundary

| Artifact | SHA-256 |
|---|---|
| Baseline canonical annex | `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442` |
| Candidate target annex | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| Exact annex-only patch | `5d66663f0983e0022f5d8f1b1aaaf6e28148c417be29217f54b48edfc82204b0` |
| Unchanged canonical YAML | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |

The patch changes only present-tense release/authority labels at lines 1, 3, 5, 9, 13, 15, 19, 21, 36, 38, 40, 45 and 58. Historical candidate-preparation approval and the historical executor `DECISION.md` label remain identified as such. Twelve operation rows, core transport/receipt text, executor bullets and YAML bytes are identical; [VALIDATION.md](VALIDATION.md) gives the exact comparisons. A reverse application of the 15 listed editorial substitutions reproduces the baseline byte for byte. The changed verbs clarify that rules already in the exact published YAML are published; no rule, status code, lifecycle arrow, replay precedence, fixture condition or executor timing was added.

## Verification and next gate

Disposable `git apply --unsafe-paths --check` and `git apply --unsafe-paths` both exited 0 against an exact annex baseline copy; the result matched `candidate-annex.md` byte for byte. YAML metadata and all 12 operation IDs still bind to the same annex path. No full OAS rerun was necessary because YAML bytes are unchanged; no runtime or guard PASS is claimed. The production DocsPathGuard failure and three external architecture failures in the publication handoff remain open without waiver.

Only the [single copyable owner decision text](OWNER-DECISION-UNAPPROVED.md) can authorize this **new exact annex hash**, both MOD-0190/0192 consumer repins and later annex-only application. Until that actual decision and required guard disposition, do not apply this patch or treat its proposed published-status wording as current canonical truth.

## SOP §22 fields

| Field | Result |
|---|---|
| Agent verdict | Candidate PASS at E1/E2 static editorial/patch scope; release application HELD. |
| Golden/contract flow | Published YAML preserved; annex status replacement proposed at exact old/new hashes. |
| Sub-flows / failure paths | Baseline mismatch, changed patch or ungranted replacement/repin stops application. |
| Tests | 12/12 matrix and executor text byte parity, inverse edit round-trip, disposable patch check/apply, YAML hash/parity PASS. No runtime test. |
| Persistence / security / observability | Not touched or evidenced by this task. |
| Migration / rollback | No DB migration; no canonical edit. A later authorized writer must preserve the exact annex preimage. |
| Decisions | Earlier consent/publication retained for old hashes; new annex replacement and two consumer repins UNAPPROVED. |
| Blockers / known gaps | New exact-hash owner decision; production DocsPathGuard disposition; later consumer pack uptake and Phase 1.5. |
| Out-of-scope changes | None. No canonical, YAML, guard, pack, runtime or historical record change. |
