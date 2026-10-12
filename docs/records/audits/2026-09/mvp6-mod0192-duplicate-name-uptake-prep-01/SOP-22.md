# MVP6-MOD0192-DUPLICATE-NAME-UPTAKE-PREP-01 — SOP §22

Date: 2026-09-23  
Role: Capacity module owner / disposable candidate preparation  
Repository: `/Users/natig/Projects/ERP-vNext-recovery`  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

## Verdict

**PREPARED / HELD.** The smallest successor uptake candidate is one production mapping
line plus two Capacity-owned test files. No repository production source was changed.
The exact 43-source runtime baseline was reconstructed only in
`/private/tmp/mvp6-mod0192-duplicate-name-uptake-prep-01.vi9M4v`; the proposed patch was
also applied to a second disposable preimage and reproduced all three target hashes.

The repository logic already satisfies the deterministic duplicate and proven unique-name
race result. The missing production behavior is narrower: the API error mapper emits the
fallback `Invalid request` message for the successor code. The candidate adds the exact v3
message. It deliberately leaves `CapacityRepository.cs`, `CapacitySchema.cs`, transactions,
receipts, X01/X07, executor behavior, and `Program.cs` byte-unchanged.

Application remains **HELD** because the final 3.0.0 artifacts are proposed rather than
canonical in the inspected baseline, and no real user decision authorizes this production
source patch. The existing final-artifact owner record grants design-consumer consent and
conditional publication only; the hosted decision explicitly excludes additional
production persistence and duplicate-name policy. `OWNER-DECISION-REQUEST.md` contains the
single narrow, hash-bound conditional authority text needed after exact publication.

## Exact inputs

| Input | SHA-256 / result |
|---|---|
| Final MOD-0192 43-source manifest | `36114191e1576b8b5654b9910f3347432d5d3bf36aac87339a4280cc85a852f8` |
| 43-source transfer archive | `753843251ea261473e308e5fb100de834b1e5ab88fa7576ff50ad22bf332274e` |
| Published v2 canonical YAML baseline | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |
| Published v2 annex baseline | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| Final-proposed v3 YAML | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| Final-proposed v3 annex | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Final-proposed publication patch | `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23` |
| Proposed uptake patch | `f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f` |

All 422 reconstructed input entries (379 immutable composition baseline plus 43 Capacity
overlay) matched their manifests; the two lists had zero overlap. The candidate changed
only the three paths in `SOURCE-MANIFEST.tsv`.

## Existing behavior and minimum delta

| Concern | Existing exact behavior | Candidate disposition |
|---|---|---|
| Deterministic duplicate | Repository lines 118–125 resolve scoped parent, receipt, Draft state, fixture, then exact name with `Collation.Simple`; returns 409 name conflict | Preserve; no repository edit |
| Unique-index race | Existing precommit transient/duplicate retry at lines 136–137 reruns the transaction; the next exact-name read observes the winner and returns the defined 409 | Preserve and test; no new classifier |
| Unique index | Active-only simple-collation unique index on TenantId, LegalEntityId, CapacityPlanId, Name | Preserve; no schema edit |
| Same-key replay | Receipt lookup precedes lifecycle, fixture, and name checks | Preserve and test |
| Changed payload | Receipt fingerprint conflict remains 409 `IDEMPOTENCY_KEY_REUSED` | Preserve and test |
| Arbitrary duplicate/transient/unknown | No evidence converts these to name conflict; unknown commit remains receipt-resolved or 503 | Preserve and add negatives |
| Wire message | Mapper falls through to `Invalid request` for the successor code | Add exact v3 message in `CapacityContractError.cs` |

The race probe used a Mongo command event to pause precisely after the loser's first empty
`capacity_scenarios` find, then committed a winner through another repository instance.
The loser performed at least two scenario-name reads, returned 409, and left one scenario;
the only receipt/audit/outbox rows were the parent-plan and winning-scenario write sets.

## Verification

| Check | Fresh result | Boundary |
|---|---:|---|
| Patch dry-run against exact archived baseline | PASS | Three paths only |
| Patch apply and target-hash comparison | PASS | All three target hashes exact |
| Exact successor + policy/race tests | 3/3 PASS | Repository/model with real isolated Mongo |
| CapacityPlans regression | 34/34 PASS | Includes X01/X07 test suite; no hosted HTTP claim |

Mongo ran as disposable replica set `rsmod192` on port 57192 with database
`DitenSupplyChain_Mod0192_Test`. The final regression used `enableTestCommands=1` because
the existing X01/X07 tests require Mongo failpoints. One earlier 27/34 run without this
setting is discarded as environment setup error and is not counted. The RED archive also
contains a rejected experimental one-read race oracle; `RED-GREEN.md` states its boundary.

The green evidence proves repository/test behavior, not final canonical publication,
composed HTTP/JWT response headers, consumer uptake, rollout, or E5/G5. Those remain for
the post-publication writer and independent VER.

## Authority matrix

| Action | Current authority | Status |
|---|---|---|
| Prepare disposable patch/tests | Current user request | Complete |
| Preserve accepted X01/X07/hosted behavior | Existing acceptance chain | Complete; not reopened |
| Publish v3 YAML/annex | Conditional publication record, but prior final VER reported guard RED | Outside this task; not assumed complete |
| Apply production uptake patch | No matching real user authority found | HELD; use exact decision request |
| Independent post-apply verification | Not yet dispatched | HELD prompt supplied |
| Program.cs/canonical/guard/rollout/git | Explicitly excluded | Not authorized |

## Repository effect

The shared checkout already contained 236 dirty/untracked porcelain entries when measured
at close. Capacity product/test paths from the 43-source manifest are absent from that
shared checkout and were not created there. The only new repository content from this task
is this audit directory. No contract, guard, pack, runtime source, `Program.cs`, index,
commit, push, stash, checkout, or git metadata was changed.
