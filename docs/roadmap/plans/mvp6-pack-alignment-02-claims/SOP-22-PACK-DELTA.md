# SOP §22 — MVP6-PACK-ALIGNMENT-02-CLAIMS / MOD-0187 Claims Management (CT queue Q29)

**Verdict: PREPARED FOR OWNER REVIEW — NOT APPROVED.** The shared pack stays `draft` and unchanged until the decision in [PROMOTION-DECISION.md](PROMOTION-DECISION.md) is recorded. This is the successor to `docs/roadmap/plans/mod-0187-final-pack-delta-01/`, which stays unchanged. No business decision is reopened.

## Problem

The shared pack `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` (SHA-256 `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f`, verified) still says `status: draft`, "Phase 1.5 BLOCKED; DEV/VER HELD". Meanwhile:

- on 2026-09-20 the owner approved the exact delta `afe36ff9…0187` and conditional promotion;
- an isolated copy was promoted to `ready-for-dev` (`d035d420…`);
- on 2026-09-22 CT accepted the bounded work package built under it.

The old delta (20 Sep) predates publication and acceptance, and it still pins SHIPMENT-BUNDLE `93c696e2…`.

## Lineage (all hashes rechecked 2026-09-26)

| Step | Identity | Source |
|---|---|---|
| Shared pack today | `a342054c…1c1f` (differs from HEAD `f8d864e8…`; the PREP-02 working-tree edit) | common checkout |
| Owner-approved delta | `afe36ff9123088593ec920191657176e5343270655b7b0a2be4106bc05820187` | `mod-0187-final-pack-delta-01/proposed-pack.patch` (package 13/13 OK) |
| Owner authority | `owner-authority.md` `8a89e5d0…12c6` | `mod-0187-runtime-dispatch-01/` (SHA256SUMS 12/12 OK) |
| Activation patch (approved delta + status + §30) | `9b5327e2ff93ee6498459f496f855e80513c784ef2d95b72930680f0e5988a16` | same package |
| Owner-promoted isolated pack (`ready-for-dev`) | `d035d42059141f17edb90bb97183892a3c98c171a16770795ce2557f834ac9b0` | **reconstructed** here by applying the activation patch to `a342054c…` with `--fuzz=0`. The hash equals `pack-hashes.txt` and the pack entry in `mvp6-mod0187-r22-r25-evidence-02/raw/manifests/target-source-sha256.txt` |
| CT acceptance | `mvp6-mod0187-ct-accept-01/SOP-22.md` `30c9233e…7942` (SHA256SUMS 2/2 OK) | ACCEPTED, bounded isolated WP |
| Accepted source | 341-entry manifest `92879d20…0f80`, archive `edb759a0…5a21` | `mvp6-mod0187-normal-baseline-integration-01/` (8/8 OK) |
| Proposed shared target | `f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf` | produced by [proposed-pack.patch](proposed-pack.patch) |

## What the patch does

`proposed-pack.patch` turns the shared pack into the owner-promoted isolated pack **byte for byte**; it contains the approved delta plus the §30 activation. It then makes exactly three further changes:

1. `status_note` points to the accepted work package and its open boundaries. `status` stays the owner-approved `ready-for-dev`, never `done`.
2. §6 Protected Paths gains one line naming the published SHIPMENT-BUNDLE YAML and Claims/root annexes as read-only.
3. A new §31 "Accepted bounded scope binding" records the owner authority, CT acceptance, R01–R30 matrix, source and `Program.cs` hashes, current contract hashes, owned paths and open gates. It also carries a precedence note: the §29 "canonical remains 2.0.0 / `93c696e2…`" sentence, the §5 "does not apply this pack change" note and §30's "UNEXECUTED at activation" are historical.

No acceptance row, lifecycle, permission, error, replay, amount or scope rule is edited.

## Current canonical contracts (no drift for Claims)

| File | Now | Pinned by CT acceptance |
|---|---|---|
| `shipment-bundle.openapi.yaml` | `5dfe7c1b…d21c` | same |
| `claims-semantics-v3.0.0.md` | `16e65c26…4eb63` | same |
| `shipment-root-semantics-v3.0.0.md` | `7d1327a1…f8af` | same |

## Phase 1.5 delta

| Item | Shared pack today | After the patch |
|---|---|---|
| Status | `draft`, "Phase 1.5 BLOCKED" | `ready-for-dev` as the owner conditionally approved (condition: publication done, Phase 1.5 checked; both recorded in `mod-0187-runtime-dispatch-01`) |
| Phase 1.5 | Proposed only (old §27/§28) | Nine-row review recorded: rows 1–6, 8, 9 design PASS; row 7 UI N/A. REPO-001 repository, entity-versioning (internal CAS) and response-envelope exceptions are limited to this Claims slice |
| Scope | 48-path PREP-02 proposal | 47 exact paths, worker removed |
| Runtime evidence | none | R01–R30 accepted at their stated evidence class (§31) |
| Still outside | — | Common checkout, gateway, shared permissions, publisher, finance, UI, migration, E5/G5, rollout |

## Remaining gaps (honest list)

1. **Record typo, not edited:** `mvp6-mod0187-r14-apply-01/AUTHORITY.md` shows the R14 `Program.cs` baseline as `a72a05a5b7dc235c…ab34a`. The baseline artifact, that package's `MANIFEST.tsv` and `PATCH-ORDER.md` all give `a72a05a5e185e04d…0254c`. Patch and target hashes agree. CT may want a correction successor; this lane cannot edit records.
2. **Composition hop not re-traced:** the `Program.cs` hop `7fdb5ef0…` → `a72a05a5e185…` is taken from the combined Returns/Claims source records. This lane did not re-trace its authority. The CT acceptance treats the normal composition as separately approved.
3. **Count difference:** CT acceptance (22 Sep) reported 44 Claims paths absent from the common checkout. Today 47/47 owned paths are absent. The difference was not investigated; either way, uptake is Q14/Q15.
4. **Forward drift risk:** Loads 3.1.0 publication (Q25, HELD) would change `shipment-bundle.openapi.yaml` away from `5dfe7c1b…`. The Claims pin would then need a re-pin disposition like MOD-0190's.
5. **Stale body text kept:** §§5, 29 and 30 retain historical wording; §31 states precedence rather than rewriting owner-promoted text. Option B in the decision file rewrites it instead.

## Checks (this lane)

| Check | Result |
|---|---|
| Repo / branch / HEAD | `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| DCP-002 `verify_module_id.py --check-id MOD-0187 --name "Claims Management"` | `OK`, exit 0 |
| Patch against a copy of the current pack | `git apply --check` OK; `patch --fuzz=0` OK; result byte-equal to `f0e4d3bd…baf` |
| Promoted → target difference | `status_note`, one §6 line and §31 only |
| Owned paths | 47/47 in the accepted manifest; 0/47 in the common checkout; no worker, no `Program.cs` |
| Runtime / tests | None run; none needed for a document delta |

## Changed files

Only this package directory. The pack, contracts, product code, `.antigravity`, gateway, existing records and the old delta were not changed. No commit, push or stash.
