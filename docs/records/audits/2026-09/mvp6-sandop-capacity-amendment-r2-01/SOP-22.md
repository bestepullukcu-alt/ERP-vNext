# MVP6-SANDOP-CAPACITY-AMENDMENT-R2-01 — SOP §22

**Verdict: versioned successor candidate prepared; canonical publication NO-GO.** The two-file `mvp6-sandop-capacity-amendment-01` package is the only working base. It and the earlier one-file candidate remain immutable. The 2026-09-22 user messages approve D190-04/05, C192-02…07 and the exact `MOD-0192` executor policy **for candidate preparation**; the latest message selects the two-file base and authorizes the narrow evaluation/checksum/GET-503/mutation-unknown-commit/application-401 corrections. This is not release consent, final 2.0.0 selection, pack promotion, Phase 1.5, Program.cs or runtime DEV GO.

## Input pins and exact successor output

| Input | SHA-256 |
|---|---|
| Canonical frozen `docs/analysis/contracts/sandop-capacity.openapi.yaml` | `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c` |
| Frozen `docs/analysis/contracts/demand.openapi.yaml` | `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d` |
| Selected predecessor YAML | `88070dc3fa27aff9ba0d4b40ba1a43641906f77d369e4ce1a84be75ae267816d` |
| Selected predecessor annex | `c3fb876cf19d8f6943781a6ea55d0c9f07d6c4137505b6b27b1718739aaa644c` |
| Selected predecessor publication patch | `20b295e96ca9d934578fd9e5155e32a09675bb1f0815f0e3e697cc138f861648` |
| Independent reconciliation `mvp6-sandop-capacity-candidate-reconcile-01/SOP-22.md` | `67ff4dc13ff5104e2ef6ad4d5a8bec2c94c37573264d2a114f4d7cb5f093a8ca` |
| `docs/roadmap/plans/mod-0192-executor-exact-decisions-01/DECISION.md` | `cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d` |

R2 candidate YAML SHA-256 `5dc5e9750797ea687fce2e959b17d9b3312231b5701238815cab48e7ca3b661c`; proposed annex SHA-256 `9f7d87fc72934dc204fb6dffba1a6646ef583dea3c24d996974e716a9add9b20`; exact canonical-baseline two-file `publication-candidate.patch` SHA-256 `0e1aca53609c2cd137c50882615e65cf1870e37ddad212891decad6f2a0e8835`. `successor-delta.diff` SHA-256 `93d4c0942fa3044954f9f63f15e901dd6f8980514d607a760f7501b49bc44f99` is an inspection-only comparison with the selected predecessor; it is **not** an instruction to overwrite that historical package.

The proposed metadata is `info.version: 2.0.0-rc.2`, `x-status: CANDIDATE`, wire `contractVersion: v1`, with an exact R2 annex pointer. Neither a final 2.0.0 release nor wire-v1 consumer compatibility is approved. The canonical patch targets **only** existing SANDOP-CAPACITY YAML and a new module-specific annex; DEMAND and all other contracts are absent from the patch.

## Authorized delta, no silent policy expansion

1. `evaluateCapacityScenario` now proposes 422 `INVALID_CONSTRAINT_REFERENCE` for a test-only resourceRef absent from or incompatible with the exact scoped fixture, with zero evaluation/receipt/audit/outbox writes. `createCapacityScenario` retains its earlier fixture 422. Checksum mismatch remains proposed 422 `INVALID_DEMAND_REFERENCE` only on 0190 snapshot capture and 0192 plan creation, against exact scoped **fixtures**, never as a live DEMAND version/checksum claim; the YAML now includes an explicit mismatch example.
2. All six GET 503 responses reference **ReadUnavailable** with only `DEPENDENCY_UNAVAILABLE`. All six POST 503 responses reference **MutationUnavailable**: known dependency/storage failure → `DEPENDENCY_UNAVAILABLE`; unresolved commit **after scoped durable receipt lookup** → `COMMIT_RESULT_UNRESOLVED`, same exact key recovery and no zero-write assertion. These are distinct operation response components and annex rules.
3. `Unauthenticated` retains `WWW-Authenticate: Bearer` and now describes `X-Correlation-Id` plus matching Error body **when the application generates 401**. Parser-level challenges before application context may lack that header/envelope and are not claimed as application-format evidence. Existing current-request response correlation, original persisted audit/event correlation and receipt-first replay remain.
4. Existing six 409 `x-error-codes` lists gain concrete candidate examples for `IDEMPOTENCY_KEY_REUSED`; sign-off also gains `SANDOP_SIGN_OFF_STATE_CONFLICT`. The original named business conflict example remains. This is example alignment, not a new policy beyond the selected candidate. Exact parsed `Idempotency-Key` and name schemas retain `minLength:1`, with **no trim or max-200**.
5. The R2 annex binds the approved `DECISION.md` hash and records successful-claim attempt increment, Mongo-primary server UTC lease time, 10-second scan/heartbeat, 30-second fenced lease, renewal loss/stale terminal rejection, atomic terminal+active-slot release+audit+Pending outbox, unknown-terminal-commit read-before-retry and expiry after claim 3 → Failed without claim 4. It preserves the literal fixture oracle boundary and states that computation can repeat while durable terminal effect is unique. Event Bus publisher/worker remains absent.

## Verification, compatibility and gates

`PYTHONWARNINGS=ignore python3 .../verify_candidate.py` exited 0; raw output `validation-results.json`. It measured 12 operations/six mutations, 274 local reference encounters, 98 response example validations, preservation of old request/success/event/shared schemas, five deliberate negative mutants rejected, and disposable `git apply --check` + apply with byte-identical YAML and annex. Exact patch scope via `git apply --numstat`: 276 additions/45 deletions in SANDOP YAML and one 61-line new annex. **Full OpenAPI 3.1 document meta-schema validation remains NOT RUN** because a trusted validator was unavailable; parse/ref/example checks must not be represented as that full check. No HTTP/JWT/Mongo, consumer compatibility or executor runtime test occurred. This is E1/E2 candidate evidence only.

`docs/analysis/contracts/README.md:45-50` prohibits post-freeze breaking change and permits additive minor changes. New 400/401/403/409/422/503 behavior and stricter state/fixture rejection may affect exhaustive-code clients; 2.0.0-rc.2 is a **candidate compatibility posture**, not an approved major release. Contract owner must approve exact wire codes, versioning and publication after independent R2 VER and full OAS document validation; MOD-0190 and MOD-0192 consumer release consent must bind the eventual exact hashes. Their runtime uptake, pack/Phase 1.5, shared composition and E5/G5 remain distinct. No previous consent automatically transfers to these new hashes.

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; checkout was dirty before this work. Only this new audit directory was written. Canonical YAML, predecessor candidates, packs, guard, Program.cs, runtime and historical evidence were not changed. No commit, push or stash.
