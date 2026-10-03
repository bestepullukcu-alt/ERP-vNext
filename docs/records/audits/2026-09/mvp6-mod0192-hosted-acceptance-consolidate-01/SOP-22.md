# MVP6-MOD0192-HOSTED-ACCEPTANCE-CONSOLIDATE-01 — SOP §22

Date: 2026-09-23  
Role: independent Control Tower evidence reviewer  
Repository: `/Users/natig/Projects/ERP-vNext-recovery`  
Branch / HEAD observed: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

## CT disposition

**NARROW CLOSE / PASS.** The two hosted observations left PARTIAL by
`mvp6-mod0192-http-process-independent-ver-01`—a process killed while its lease was
live followed by expiry/recovery and stale-fence behavior, and committed HTTP response
loss followed by receipt replay—are closed for the exact authorized 422-source
composition and isolated E4-style evidence boundary.

The closure has a precise evidence level: the writer executed the native hosted
scenarios, while the later independent verifier checked the immutable source and
authority bindings, rebuilt the API, reconstructed `Program.cs`, independently reran
only eight focused repository checks, and validated the hosted claims from the sealed
raw HTTP/Mongo/process/log archive. The verifier did **not** independently rerun the
hosted HTTP/process scenarios. This is sufficient because the controlling executor
acceptance requires isolated Mongo, real separate process/restart, request traces, and
persisted evaluation/receipt/slot/audit/outbox observations; it does not require every
native hosted scenario to be executed twice.

This is not full-module acceptance. Duplicate-name behavior remains a separate
unpublished contract/release gate. Gateway, live DEMAND/constraint integration,
publisher delivery, rollout, E5/G5, and exactly-once execution remain outside this
disposition.

## Authority and source binding

| Binding | Exact value | Disposition |
|---|---|---|
| Owner decision | `OWNER-DECISION.md` SHA-256 `b47d667be009ee1cadbfe725c7b6789abbab237cb337c34c24457c824a7f2cb1` | Authorizes the exact 43-source transfer, exact `Program.cs` patch, isolated HTTP/JWT and hosted process/restart evidence, Capacity test-path evidence, and independent VER. It excludes additional production persistence, duplicate-name policy, canonical/guard, gateway, live producer, rollout and E5/G5. |
| MOD-0190 baseline + Capacity overlay | 379 + 43 paths; overlap 0; combined 422; combined manifest SHA-256 `edfb66423a9763634b895eba19b1a1dd063cc2e4cc9b1212fdbd79f4426400ee` | Independently recalculated by VER. MOD-0190 bytes remain the baseline except the separately authorized `Program.cs` target. |
| Product `Program.cs` | baseline `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` + patch `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e` → target `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` | VER independently applied the patch with exit 0 and reproduced the target. This is the normal product composition used by the hosted writer processes. |
| Test-only source delta | `CapacityAtomicityTests.cs` `91999330faf2a50f5ff2cda3595506ecded50d5b87ffb7bf393dd655f71e51db` → `2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43`; patch `b6744cddfd498536abeb4b350d8b8b2bd1c30c0824875757522026ed5f01df81` | Adds the five later-read fail-closed cases and count/scope assertions. It adds no product behavior and no event/audit `_id` oracle. |
| Previous PARTIAL snapshot | 422-entry manifest SHA-256 `59d21906b77c82431df3746ad7725e305ed7286029266378fe7288c6a8a10151` | Product bytes and `Program.cs` are unchanged. The hosted snapshot differs only in the test-only `CapacityAtomicityTests.cs` byte variant. Historical evidence is not relabeled as freshly executed. |
| Hosted timing/response-loss control | disposable `hosted_probe.py` SHA-256 `b94eb4af44c4fa1f99eb295c2971c985ce913eeb7a199e855b90489577b51f90` | There is **no production or deployable test-seam source diff** for timing or response loss. The harness uses an app-name-scoped Mongo `failCommand` block and a client socket close. Normal production behavior remains the exact 43 sources plus `Program.cs` target above. |
| Writer seal / binary | writer seal `f1667f45fea1c26355431be441717d8534ec16a6ce16e80904a8da1bc9404deb`; API DLL `f43af77c5c7ffeb5ea438daac4801b61cdf89e0e39f6d5f9dfb40576e3e8ee8f` | All five native writer processes bind to this DLL. |
| Independent build / focused rerun | verifier DLL `f494174e83bafed8ea4191dac4a930331afa7e73b7ad7f2ddd12b41430c9c077`; 8/8 TRX `9218b8ce3848f8d9c8ca3a26a36ceb49febdb433a1e4662a3dad64ee3e950b6a` | Independent build and repository-level rerun. It is not a hosted HTTP/process rerun. |

## Single acceptance matrix

Evidence labels used below:

- **W-executed:** the hosted evidence writer executed the scenario.
- **V-rerun:** the independent verifier executed the check again.
- **V-raw:** the independent verifier checked sealed writer raw records without rerunning the native scenario.

| Acceptance item | Controlling requirement | W-executed | V-rerun | V-raw | CT result and exact boundary |
|---|---|---|---|---|---|
| X01 definite precommit / unknown / committed result classification | `DECISION.md` X01/X07: known precommit failure is not unknown; uncertain result is read-resolved; committed result replays original | Included in writer 36/36 | **Yes, 3/3 focused X01 checks** | Writer TRX parsed | **CLOSED/PRESERVED.** No new contrary evidence and no product rework. Hosted work does not reopen X01. |
| X07 five later reconciliation reads | Broad event, exact event, broad audit, exact audit and active-slot reads must each fail closed with one targeted injection and unchanged state | **Yes, five cases inside writer 36/36** | **Yes, 5/5 inside verifier 8/8** | TRX and stdout parsed | **CLOSED.** Each case recorded one failpoint entry and one injected failure. Initial evaluation-read evidence was not substituted. Physical Mongo `_id` equality is not required. |
| Six operations, JWT/RBAC, tenant/LE/soft-delete, replay/conflict | Published Capacity contract/annex and promoted pack | **Yes, native HTTP** | Not in later 8/8; the earlier independent PARTIAL run executed these against its immutable 422 snapshot | **Yes**, 21 HTTP records parsed | **CLOSED for isolated composed API.** Later product source is unchanged; only a test file differs. No gateway claim. |
| Committed HTTP response loss | X02/X07: client can lose the acknowledgement; durable receipt exists; same key returns original result without duplicate work | **Yes.** Client socket closed without reading; receipt observed before retry; same key returned the same CapacityPlan ID | **No native rerun** | **Yes.** HTTP and Mongo records independently parsed | **CLOSED at W-executed + V-raw level.** This proves committed response loss only; it does not turn every network error into committed success. |
| Kill while lease is live | X03: Running attempt/fence/lease survive process death; no reclaim while lease is valid | **Yes.** Main process was killed with exit `-9`; restart after 3s still saw Running, attempt/fence 1 | No native rerun | **Yes.** Process, DB and log records parsed | **CLOSED at W-executed + V-raw level.** |
| Expiry and recovery | X03: after server-time expiry one later claim advances attempt/fence and completes | **Yes.** Completion occurred at attempt/fence 2; final effect event=1, audit=1, slot=0 | No native rerun | **Yes** | **CLOSED.** The 30s value remains a reclaim boundary, not a recovery SLA. |
| Stale worker / renewal-loss outcome | X05 and stale terminal rule: old fence creates no terminal effect after a newer fence wins | **Yes.** Worker A was held past expiry; worker B completed on the new fence; A logged lease loss; final effect 1/1/0 | No native hosted rerun; lease/fence mechanics were covered by the earlier independent 36/36 repository/process run | **Yes** | **CLOSED narrowly for stale-fence zero-effect and recovery.** This does not assert successful local cancellation or exactly-once fixture execution. |
| Lease race / single durable terminal effect | X04: competing execution may repeat calculation, but only a current fence may commit one terminal effect | **Yes, old-fence A versus new-fence B native race** | Later 8/8 did not rerun it; earlier independent evidence includes the two-host single-effect observation and the separate-process expiry/claim test | **Yes** | **CLOSED for single durable effect and fenced terminal ownership.** The current raw probe alone is not described as two fresh workers simultaneously winning the same expired claim. |
| Terminal atomiklik | Terminal evaluation + slot release + audit + Pending event are all-or-none and scoped | **Yes.** Kill/recovery and stale race each end event=1, audit=1, slot=0 | Repository atomicity covered in independent runs; not a native hosted rerun in 8/8 | **Yes** | **CLOSED within the exact fixture executor.** Content/count/scope define identity; physical event/audit `_id` equality is not added. |
| No publisher / Pending-only | Evaluation progresses without publisher; event remains Pending | **Yes.** Five process logs show no transport; final snapshot contains 10 Pending Capacity outbox rows | No native rerun | **Yes** | **CLOSED for the no-publisher boundary.** No delivery claim. |
| Writer roll-forward suite | Repository/test regression on exact writer snapshot | **36/36** after a separate zero-test aborted launch | **Not rerun as 36/36 by the later verifier**; verifier reran only 8/8 | **Yes**, writer TRX parsed | **PASS as writer execution plus archive verification.** Counts are not added and the aborted launch is retained. |
| Duplicate scenario name | Separate unpublished successor contract/release work | Not changed | Not in scope | Boundary verified | **OPEN / HELD separately.** No `CAPACITY_SCENARIO_NAME_CONFLICT` publication, consumer consent, cutover, production uptake, or behavior acceptance is inferred. |

## Why the former PARTIAL can close

The former independent report correctly left two rows PARTIAL because its own run had no
bounded way to hold a live lease or lose a transport acknowledgement. The successor used
external, disposable controls rather than changing production behavior:

- raw socket close after a valid request, followed by direct observation of the durable
  receipt before same-key recovery;
- Mongo `failCommand` scoped by application name to hold a worker after claim, allowing
  real kill/restart, server-time expiry, a newer fence, and stale terminal rejection.

The resulting writer chain contains a fixed binary, five real processes, request records,
process exits, DB timelines and final scoped effects. The later verifier recalculated the
source/authority bindings and parsed those raw records directly. The controlling acceptance
does not prescribe duplicate native execution by the verifier, so another VER dispatch
would repeat evidence without closing a stated requirement. **No further hosted VER is
recommended for these rows.**

## Preserved limits and remaining gate

- X01 and the five X07 later-read faults remain closed.
- Event/audit physical Mongo `_id` equality is not an acceptance condition.
- Fixture DEMAND/constraint data, literal Finite/Infinite oracle, Pending-only outbox and
  no-publisher boundaries remain in force.
- This closure does not establish exactly-once execution, a recovery deadline, live
  producer uptake, publisher delivery, gateway acceptance, rollout, E5/G5 or full MOD-0192
  completion.
- **Only the duplicate-name successor contract/release path remains open from this review:**
  final byte/hash, independent release verification, exact-hash consumer consent,
  publication/cutover and production uptake remain outside the hosted evidence closure.

## Input preservation and repository effect

The pre-write working tree was already dirty/untracked (`1978` porcelain entries; captured
read-only snapshot SHA-256 `7cf31f8b31867dc05ca4dbfa986c38b7b79711b8ad0abb357076d6b24e7ad0d2`).
This review did not alter product source, tests, `Program.cs`, contract, pack, guard, gateway,
or git state. Its only repository output is this new audit directory.

