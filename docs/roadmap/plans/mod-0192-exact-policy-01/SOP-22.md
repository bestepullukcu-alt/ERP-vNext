# SOP §22 — MVP6-MOD0192-EXACT-POLICY-01

**Agent Verdict:** SPEC CANDIDATE COMPLETE; all C192-02…07 choices UNAPPROVED. No DEV/VER GO.

**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` at preflight. **Worktree status:** existing `mod-0192-spec-01/` untracked input preserved; this task added only the three files in `mod-0192-exact-policy-01/`. No staging, commit, push or stash.

**Changed files:** `DECISION-PACK.md`, `FIXTURE-ORACLE.json`, this report. **Golden/Contract flow:** Published `SANDOP-CAPACITY` v1 SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`; published DEMAND v1 SHA-256 `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d`; draft MOD-0192 pack SHA-256 `edd550b84451af082b934b392cd34f7dc24dd6e462f7e1d9ee0e03c21b4469f7`. These inputs were not changed. The separate 0190 lane remains the shared DEMAND decision owner.

**Sub-flows:** Fixture-only constraint resolution and literal Finite/Infinite oracle; proposed durable Accepted executor with restart recovery; proposed active uniqueness and receipt replay; terminal event/outbox policy; independent publisher boundary. Each has a contract pointer and exact acceptance in `DECISION-PACK.md`.

**Failure paths:** Unknown/stale/cross-scope fixture and mismatched resource/UoM have no scenario write, but their wire response needs amendment. Same-key changed payload needs an amended 409 response. A different key against active evaluation uses frozen `EVALUATION_ALREADY_ACTIVE`. Stale worker writes lose CAS. Pending publisher cannot advance Accepted. No unlisted plan/scenario transition or hidden endpoint was invented.

**Tests:** Static JSON parse/oracle assertions PASS. SHA-256 inputs recorded above; proposal SHA-256 at creation: `DECISION-PACK.md` `acb1b8f0ec1756aa72782c02b0a8af8eb067289a6770a9ca6bef5ccde46c7f6b`; `FIXTURE-ORACLE.json` `d4af417d50ff038fa0db7cc3a8bbaa80143ddf4ce550edd2781fba96c00fed9a`. No runtime, Mongo, HTTP, fixture-schema or algorithm behavior test was run or claimed.

**Persistence evidence:** None; all transaction/CAS/lease semantics are proposed acceptance. **Security/RBAC/Tenant evidence:** Only contract/pack inspection; no JWT or scope runtime proof. **Audit/Evidence:** Proposal and hashes above. **Observability:** Executor/lease/attempt and outbox status should be separately observable in a future authorized DEV/VER; no probe added. **Migration/Rollback:** None; no persisted changes.

**Decisions:** Exact owner approval text and necessary selections are in `DECISION-PACK.md`. **Blockers:** Owner approval of fixture/oracle, executor and bounds, state/replay/event policy; versioned contract disposition for missing error cases; shared DEMAND decision; separate integration ownership; draft pack and sequence gate. **Known gaps:** No live constraint producer or evaluation algorithm, no live DEMAND exact-version proof, no publisher/runtime acceptance. **Out-of-scope changes:** none.
