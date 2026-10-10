# SOP §22 — MVP6-PACK-ALIGNMENT-01 / MOD-0192 Capacity Planning

**Verdict: PREPARED FOR OWNER REVIEW — NOT APPROVED.** The shared pack stays `draft` and unchanged until the decision in [PROMOTION-DECISION.md](PROMOTION-DECISION.md) is recorded. No business decision is reopened.

## Problem (CT audit F-03)

`execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` (SHA-256 `edd550b84451af082b934b392cd34f7dc24dd6e462f7e1d9ee0e03c21b4469f7`, identical to HEAD) still says `status: draft` / "Runtime code is not authorized". But the owner promoted an isolated copy to `ready-for-dev` on 2026-09-22, and CT accepted the resulting bounded source (BC successor `dec28b6a…`, 23 Sep).

## Why a new delta, not the existing one

`mvp6-mod0192-pack-phase15-close-01/proposed-pack.patch` (SHA-256 `ea7cec16…0032`, 7/7 files verify) is the **pre-approval** draft delta. It targets `b5b948ee…` with `status: draft` and pins SANDOP-CAPACITY 2.0.0. The owner approved and promoted it to `d01bf7a0…`, and the contract has since moved to 3.0.0 through an owner decision. That delta does not describe the approved or accepted state. It is left unchanged.

## Lineage (all hashes rechecked 2026-09-25)

| Step | Identity | Source |
|---|---|---|
| Shared pack today = HEAD | `edd550b8…69f7` | common checkout |
| Approved draft target | `b5b948ee0803c535f91c9a3cc66e6098e7e29635c6b7f2aeaf6873b64eed74fc` | `mvp6-mod0192-pack-phase15-close-01/MOD-0192-PROPOSED-DRAFT.md` |
| Owner-promoted isolated pack (`ready-for-dev`) | `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0` | extracted from `mvp6-mod0192-core-dev-01/DEV-SOURCE.tar.gz` (SHA-256 `cab87ad5…602c`); promotion recorded in `mvp6-mod0190-mod0192-isolated-dispatch-01/README.md` (SHA-256 `cab82fa6…9c28`) |
| X01/X07 production rework, independent VER | `mvp6-mod0192-x01-x07-independent-ver-01/SOP-22-VER.md` `ae1b2f6b…01ad` | PASS at repository scope |
| Hosted HTTP/process narrow close | `mvp6-mod0192-hosted-acceptance-consolidate-01/SOP-22.md` `08536c9b…538a` | CT NARROW CLOSE / PASS |
| Duplicate-name contract 3.0.0 | `mvp6-capacity-duplicate-publication-exec-01/SOP-22.md` `47a47bad…6ebe`; YAML `5213b535…adab`, annex `f9d9d555…ee64` | PUBLISHED / PASS; owner decision `mvp6-capacity-duplicate-final-owner-decision-01.md` |
| CT acceptance (controlling) | `mvp6-bc-successor-ct-handoff-01/README.md` `883efdae…2eac`; MANIFEST 6/6 OK | BOUNDED ACCEPTED |
| Accepted source | 422-entry manifest `dec28b6a…4634`, archive `ebd5d80c…7064`; VER 39/39 | `mvp6-bc-successor-exec-02/`, `mvp6-bc-successor-independent-ver-01/` |
| Proposed shared target | `de81a0e289fff2732ca333357654fcd0ea7254fd9496391d2b06933d1891946c` | produced by [proposed-pack.patch](proposed-pack.patch) |

## What the patch does

`proposed-pack.patch` turns the shared pack into the owner-promoted isolated pack **byte for byte**, then makes exactly three further changes:

1. `status_note` points to the accepted evidence and published 3.0.0. `status` remains the owner-approved `ready-for-dev`, not `done`.
2. §6 Protected Paths gains one line protecting both SANDOP-CAPACITY annexes (v2 and v3).
3. A new §22 "Accepted bounded scope binding" records the acceptance chain, source and composition hashes, owned paths, open gates, and a **contract-precedence clause**. The published 3.0.0 contract supersedes the 2.0.0 pins in §§3, 7, 16, 18 and 21 only for its published change: `createCapacityScenario` 409 `CAPACITY_SCENARIO_NAME_CONFLICT` and the annex pointer. The accepted source implements that change. The owner already decided it through the publication and BC successor decisions; this package only records it.

The 2.0.0 lines in the body are deliberately **not** rewritten. Rewriting them would edit acceptance text beyond the binding. If the owner prefers in-body re-pinning, that is option B in the decision file.

## Phase 1.5 delta

| Item | Shared pack today | After the patch |
|---|---|---|
| Phase 1.5 | Unchecked | Owner-approved on 2026-09-22 for the isolated 43-path core/executor (promoted pack §18). The accepted evidence now covers X01/X07 at repository scope, hosted HTTP/JWT/process/restart (narrow close) and the 3.0.0 duplicate-name uptake (39/39) |
| Contract | Specification draft, 2.0.0 | Accepted surface = 3.0.0 / wire v1 (precedence clause in §22) |
| Still outside | — | Common checkout, gateway, shared permissions, live DEMAND/constraint producers, publisher delivery, UI, exactly-once claims, E5/G5, rollout |

## Checks (this lane)

| Check | Result |
|---|---|
| Repo / branch / HEAD | `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| DCP-002 `verify_module_id.py --check-id MOD-0192` | `OK`, exit 0 |
| Patch against a copy of the current pack | `git apply --check` OK; result byte-equal to target `de81a0e2…946c`; applies together with the MOD-0190 patch |
| Promoted → target difference | `status_note`, one §6 line and §22 only |
| Owned paths | 43/43 found in the accepted 422 manifest; 0/43 present in the common checkout; 0 overlap with MOD-0190 |
| 3.0.0 scope | YAML hunks of the owner-approved publication patch touch only `info.version`, the annex pointer and `createCapacityScenario` 409 |
| Runtime / tests | None run; none needed for a document delta |

## Changed files

Only this package directory. The pack, contracts, product code, `.antigravity`, gateway and existing records were not changed. No commit, push or stash.
