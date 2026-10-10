# SOP §22 — MVP6-PACK-ALIGNMENT-01 / MOD-0190 S&OP Workflow & Sign-offs

**Verdict: PREPARED FOR OWNER REVIEW — NOT APPROVED.** The shared pack stays `draft` and unchanged until the decision in [PROMOTION-DECISION.md](PROMOTION-DECISION.md) is recorded. No business decision is reopened.

## Problem (CT audit F-03)

`execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` (SHA-256 `637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877`, identical to HEAD) still says `status: draft` / "Runtime code is not authorized". But the owner promoted an isolated copy to `ready-for-dev` on 2026-09-22, and CT accepted the bounded work package built from it.

## Why a new delta, not the existing one

`mvp6-mod0190-pack-phase15-close-01/proposed-pack.patch` (SHA-256 `ea22af82…6a`, 10/10 files verify) is the **pre-approval** draft delta. It targets `04f2e36f…` with `status: draft`. The owner approved that target and then promoted it to `6a57769c…`. That delta therefore does not describe the approved or accepted state, and it is superseded in purpose, not in record. It is left unchanged.

## Lineage (all hashes rechecked 2026-09-25)

| Step | Identity | Source |
|---|---|---|
| Shared pack today = HEAD | `637690f3…6877` | common checkout |
| Approved draft target | `04f2e36f89cae0a21300217b63756b0cd3104c33af145d985b9fc101a83b38a2` | `mvp6-mod0190-pack-phase15-close-01/proposed-pack-target.md` |
| Owner-promoted isolated pack (`ready-for-dev`) | `6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983` | extracted from `mvp6-mod0190-core-dev-01/source-archive.tar.gz` (SHA-256 `369c857c…a4`); promotion recorded in `mvp6-mod0190-mod0192-isolated-dispatch-01/README.md` (SHA-256 `cab82fa6…9c28`) |
| CT acceptance | `mvp6-mod0190-401-disposition-ct-02/SOP-22.md` `91de3760…91cb` (SHA256SUMS 3/3 OK) | ACCEPTED, approved isolated WP only |
| Accepted source | 379-entry manifest `ae7ef59e…9bef`, archive `8fa00d40…b745` | `mvp6-mod0190-test-oracle-rework-01/` |
| Proposed shared target | `8403d8f46059c99ef34d3fe08b3cacdbbbefb321f30f921b620eceda9180ea40` | produced by [proposed-pack.patch](proposed-pack.patch) |

## What the patch does

`proposed-pack.patch` turns the shared pack into the owner-promoted isolated pack **byte for byte**, then makes exactly two further changes:

1. `status_note` points to the accepted work package and its open boundaries. `status` remains the owner-approved `ready-for-dev`, not `done`.
2. A new §22 "Accepted bounded scope binding" records the CT acceptance, source and composition hashes, the owned-path list, the preserved NON_PASS rows (T03 271/276, T04 Loads 0/1) and the open gates.

Every other line equals the owner-promoted pack. No acceptance criterion, lifecycle, error, replay or scope rule is edited.

## Phase 1.5 delta

| Item | Shared pack today | After the patch |
|---|---|---|
| Phase 1.5 | Unchecked; "promote only after the shipment lane is verified" | Owner-approved on 2026-09-22 for the isolated 38-path core (promoted pack §18). The accepted evidence now covers the E4 rows that were open at approval: HTTP/JWT, failure paths F01–F04, restart and Pending outbox (CT-02 matrix A01–A09) |
| Contract | Specification draft | SANDOP-CAPACITY 2.0.0 YAML `9543e3f2…` / annex `eb1df138…`, as accepted |
| Still outside | — | Common checkout, gateway, shared permissions, live DEMAND/Workflow/Event Bus, UI, E5/G5, rollout |

## Open item named, not decided

The canonical SANDOP-CAPACITY is now **3.0.0** (`5213b535…adab`, annex `f9d9d555…ee64`). The owner-approved publication patch (`mvp6-capacity-duplicate-final-release-prep-01/publication-proposed.patch`) changes only `info.version`, the annex pointer and the Capacity `createCapacityScenario` 409 in the YAML. I checked its YAML hunks: no S&OP operation is edited. The accepted MOD-0190 evidence remains bound to 2.0.0. Re-pinning MOD-0190 to 3.0.0 is a separate CT/owner disposition. This package records the drift in §22 and neither performs nor assumes the re-pin.

## Checks (this lane)

| Check | Result |
|---|---|
| Repo / branch / HEAD | `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| DCP-002 `verify_module_id.py --check-id MOD-0190` | `OK`, exit 0 |
| Patch against a copy of the current pack | `git apply --check` OK; `patch --fuzz=0` OK; result byte-equal to target `8403d8f4…ea40` |
| Promoted → target difference | `status_note` plus §22 only |
| Owned paths | 38/38 found in the accepted manifest; 0/38 present in the common checkout; 0 overlap with MOD-0192 |
| Input records | CT-02 SHA256SUMS 3/3 OK; phase15-close SHA256SUMS 10/10 OK |
| Runtime / tests | None run; none needed for a document delta |

## Changed files

Only this package directory. The pack, contracts, product code, `.antigravity`, gateway and existing records were not changed. No commit, push or stash.
