# Q218 — Integration Gate: recommendation (nothing applied)

## The question

SOP §27.1 Integration Gate asks that "contract versions eşleşiyor" (`docs/guides/operations/control-tower-sop.md:1389`).
MOD-0186 and MOD-0187 bind SHIPMENT-BUNDLE 3.0.0 `5dfe7c1b…`. The canonical file is 3.1.0 `6dc1dd48…`.

## Answer

**Re-pin both packs to 3.1.0. Do not revert the file.** The difference is immaterial for Returns and Claims — that is
measured, and it is the reason the re-pin is safe. It is not, by itself, a way to pass the gate.

| Option | Verdict | Evidence |
|---|---|---|
| **A. Re-pin MOD-0186 and MOD-0187 to 3.1.0 `6dc1dd48…96aa2`** | **Recommended** | Every operation the two modules own or consume is equal in both versions after parsing, with everything it reaches (`VERSION-DELTA.md`). The Returns, Claims and root annexes are unchanged and equal their pins (`HASHES.md`). So a re-pin changes one hash and one version number in each pack and no rule. |
| B. Revert the canonical file to 3.0.0 | **Rejected** | 3.1.0 is the owner-authorized publication (decisions A and B, 2026-09-25), CT-accepted (`CT-QUEUE.tsv:26`, `:33`). The guard authority binds `6dc1dd48…` (`docs-path-authority.json:11`). The Loads uptake evidence is pinned to `6dc1dd48…` (`docs/records/audits/2026-09/mvp6-loads-uptake-dev-01/evidence/source-contract-parity.json:3`). A revert would undo an owner decision, break that guard binding and remove the Loads root field. |
| C. Declare the difference immaterial and leave the pins | **Not sufficient alone** | True in substance. But both packs state "Published contracts at acceptance **and today**" for `5dfe7c1b…` (MOD-0186 `:584`, MOD-0187 `:533`). That sentence has been false since 2026-09-26. A gate that reads "versions match" cannot pass on a pack that names a different hash. K6 applies: one fact living in two places has drifted. |

## What the re-pin would touch (described, not done)

| Pack | Lines that carry the effective pin |
|---|---|
| MOD-0186 | `:10` (status note "3.0.0"), `:584` ("at acceptance and today"), `:619` ("3.0.0 … only"), `:780` (gap entry that would close) |
| MOD-0187 | `:10`, `:533`, `:569`, `:729` |

The acceptance value should stay visible: both modules were accepted on `5dfe7c1b…`. Only "today" changes. The annex
hashes do not change.

## Decision owner

| Decision | Owner | Basis |
|---|---|---|
| Re-pin, revert, or accept as is | **Repository owner (Natig Yusubov)** | The owner gave decisions A and B for this contract. Decision B says it "does not authorize … pack promotion". The packs' own §31 bindings were approved by owner decisions (MOD-0187 `:521`). A pin is part of that binding. |
| Integration Gate ruling for Q209 | **Control Tower** | SOP §27.1; pack owner field `supply-chain-execution / control-tower` (MOD-0187 `:11`) |
| The pack edit itself, if decided | a CT-dispatched pack lane, one writer per pack | AGENTS.md authority order: the Module Pack is the highest authority, so it is not edited as a side effect |

## Gate status today

- **Literal reading: NOT MET.** Two packs name a hash that is not the canonical file.
- **Material risk for Returns and Claims: none found.** Same operations, same fields, same error codes in both versions.
- **Before the gate is called met**, two things remain that this lane cannot give: the owner decision above, and
  run-time evidence. Neither module has ever called the real Shipment endpoint in this tree (Q210 F-Q210-4, Q211 §2 point 3).

## Related points for CT (not part of the re-pin)

1. **MOD-0185 is further behind than the two packs in question.** Its effective pin is 2.0.0 `93c696e2…` (`:20`, `:478-479`).
   It is the module 3.1.0 was published for, and its pack mentions neither 3.0.0 nor 3.1.0. See `PIN-INVENTORY.tsv`.
2. **The 3.0.0 bytes exist in one untracked folder only.** Git holds 1.0.0. If that folder is lost, the `5dfe7c1b…` pin
   cannot be checked again by anyone.
3. **Gaps between consumer and contract that exist in both versions** (F-Q218-7): undeclared 500 `SHIPMENT_ROOT_INVALID`
   on `getShipment`; undeclared 401/403/5xx on `getShipment`; scope headers sent that the operation does not declare;
   A re-pin fixes none of them. K12 is the
   relevant control: the producer and both consumers agree on a status the contract does not contain.
