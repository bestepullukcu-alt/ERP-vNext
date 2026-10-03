# MVP6-SANDOP-CAPACITY-R2-INDEPENDENT-VER-01 — SOP §22

**Independent technical verdict: PASS, E1/E2 candidate only.** The exact R2 two-file patch applies to the pinned frozen baseline and produces byte-identical YAML and annex. This run, unlike either predecessor's result, executed the complete OpenAPI 3.1 document meta-schema and `openapi-spec-validator 0.7.2` on the R2 YAML: zero errors in each. This is not final-version selection, canonical publication, consumer release consent, pack promotion, runtime DEV GO or E4/E5 evidence.

## Authority, scope and baseline

- WP `MVP6-SANDOP-CAPACITY-R2-INDEPENDENT-VER-01`; independent verifier did not author R2. `AGENTS.md`, Antigravity read-only audit and contract/versioning rules, CT SOP, the real 2026-09-22 user decision, R2 package and validator lane were read. The user chose `amendment-01` only as successor working base, authorized amendment-candidate preparation, and approved the exact MOD-0192 executor design hash. The user explicitly withheld publication, consumer consent, Phase 1.5, Program.cs and runtime GO. The older `DECISION.md` still says PROPOSED; the later real user message approves its exact hash for candidate design only.
- Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The repository was already dirty. Work wrote only this owned audit directory and disposable `/private/tmp/mvp6-sandop-r2-ver01-work`; it did not alter candidate, canonical, pack, runtime, guard or Git metadata.
- Candidate manifest `mvp6-sandop-capacity-amendment-r2-01/SHA256SUMS`: **7/7 entries OK** independently. Frozen baseline `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`; DEMAND SHA-256 `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d`; executor `DECISION.md` SHA-256 `cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d`.

| R2 artifact | SHA-256 |
|---|---|
| `publication-candidate.patch` | `0e1aca53609c2cd137c50882615e65cf1870e37ddad212891decad6f2a0e8835` |
| `sandop-capacity.openapi.candidate.yaml` | `5dc5e9750797ea687fce2e959b17d9b3312231b5701238815cab48e7ca3b661c` |
| `sandop-capacity-semantics-v2.0.0-rc.2.md` | `9f7d87fc72934dc204fb6dffba1a6646ef583dea3c24d996974e716a9add9b20` |
| Offline OpenAPI 3.1 schema `schema.json` | `e7cb616a2a10849a166c4e4a93c62c56cfea02cc00eadf287e2fb875e7124098` |
| This run's `raw-evidence.tar.gz` | `ff547a99333e15487543e7941de49989fde4d6a1e60bab5f0b6f41a151b0c71d` |

## Independent checks and raw results

Command: `PYTHONWARNINGS=ignore python3 /private/tmp/mvp6-sandop-r2-ver01-work/verify.py`, exit **0**. The full script, stdout/stderr, operation matrix and machine-readable results are in [`raw-evidence.tar.gz`](raw-evidence.tar.gz). Its SHA-256 manifest is alongside this report. The script loads the previously recorded offline `openapi-spec-validator 0.7.2` installation at `/private/tmp/mvp6-combined-final-release-q16pebui/tooldeps`; no package install or network access occurred. It calls both `openapi_v31_schema_validator.iter_errors(R2)` and `OpenAPIV31SpecValidator(R2).iter_errors()`; **0/0 errors**. Deliberately invalid version and missing-title documents each produced a meta-schema and full-validator error. A broken response `$ref` produced a full-validator failure; the meta-schema alone did not catch that ref, as expected.

In a fresh disposable directory, `git apply --check` and `git apply` both exited 0 against a byte copy of the exact canonical baseline. The resulting SANDOP YAML and new annex each equaled the R2 candidate **byte for byte**. The patch touches only those two contract files; DEMAND was hash-identical and absent from the patch. Paths and all 12 original operation IDs, request bodies, parameters and pre-existing responses were unchanged. Shared schemas, parameters, headers, security schemes, servers and global security were unchanged, including success and event schemas. Local ref traversal made **204 encounters / 52 distinct refs**. Response-example validation made **98 checks / 33 distinct JSON values**; the 98 checks must not be described as 98 unique scenarios.

Five independent policy mutants were rejected by the same R2 assertions: remove evaluation 422; add unknown-commit to GET 503; remove application 401 correlation header; add a 200-character idempotency-key maximum; remove checksum-mismatch example. Three document mutants were rejected as above. These are E2 structural negatives, not HTTP behavior tests.

## Twelve-operation YAML ↔ annex parity

The annex matrix is at `mvp6-sandop-capacity-amendment-r2-01/sandop-capacity-semantics-v2.0.0-rc.2.md:21-34`; direct operation status/ref checks are in the raw operation matrix. `M` means 503 `MutationUnavailable` (`DEPENDENCY_UNAVAILABLE` or `COMMIT_RESULT_UNRESOLVED` after receipt lookup); `R` means 503 `ReadUnavailable` (`DEPENDENCY_UNAVAILABLE` only). All twelve add 400/401/403; existing success, 404, business 409/422 responses remain byte-identical.

| Operation | Additional status beyond 400/401/403/503 | 503 |
|---|---|---|
| createSandopPlan | — | M |
| getSandopPlan | — | R |
| captureSandopSnapshot | — | M |
| listSandopSnapshots | — | R |
| recordSandopSignOff | — | M |
| listSandopSignOffs | — | R |
| createCapacityPlan | — | M |
| getCapacityPlan | — | R |
| createCapacityScenario | 422 fixture constraint | M |
| getCapacityScenario | — | R |
| evaluateCapacityScenario | 422 fixture resourceRef | M |
| getCapacityEvaluation | — | R |

The YAML uses shared `InvalidConstraintReference` on scenario creation and evaluation (`candidate.yaml:506,597,1448`), `InvalidDemandReference` on the three fixture DEMAND operations (`:73,179,389,1518`), with `checksumMismatch` example (`:1535`). All six GET 503s point to `ReadUnavailable` (`:1418`), all six POST 503s to `MutationUnavailable` (`:1430`). The application-generated 401 declares `WWW-Authenticate` and `X-Correlation-Id` (`:1393-1403`); annex `:15` explicitly excludes pre-application parser challenges from that envelope claim. The exact parsed key schema remains `minLength: 1` without trim/max (`:650-656`), while the annex `:7-13` specifies receipt-first replay, valid-payload fingerprinting, current response correlation and original audit/event correlation. Six conflict components include `IDEMPOTENCY_KEY_REUSED`, and sign-off adds state conflict (`:1475-1674`). No success-body or event shape was added.

The executor section of the annex (`:42-54`) cites the exact approved decision hash and keeps attempt increment on successful claim, Mongo-primary UTC server time, 10-second scan/renew, 30-second fenced lease, atomic terminal/slot/audit/Pending-outbox write and post-third-claim Failed policy. This is an annex/decision pointer check, **not executable executor proof**. The candidate has no publisher/worker, no live DEMAND producer validation and no Finite/Infinite optimizer acceptance.

## Findings and next gate

No R2 artifact or semantic parity defect was found in this static review. `2.0.0-rc.2` and `CANDIDATE` are metadata of an unpublished artifact; major-version compatibility, exact new error codes, consumer impact and release consent remain **UNAPPROVED**. The user has not authorized final 2.0.0 publication, canonical application, pack promotion or runtime. Contract owner/release reviewer must separately assess versioning and obtain exact-hash consumer consent before publication; this technical PASS cannot supply those decisions. E4 HTTP/JWT/Mongo, fixture-producer uptake and executor restart/fence behavior were not run.

**Final no-change:** branch/HEAD remained as above; pinned canonical, DEMAND, decision, patch, YAML and annex hashes were rechecked at handoff. Only this report, raw evidence archive and checksum manifest were added under the owned directory. No commit, push or stash.
