# MVP6-CONSUMER-ISOLATION-MAP-01 — SOP §22

Verdict: **PARTIAL for dispatch readiness; exact available-input isolation map complete.** No checkout or source transfer was performed. Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Read-only Git queries only; no Git mutation.

## Inputs and ownership

`returns-inputs.tsv` and `claims-inputs.tsv` provide exact path, tracked/untracked status, current SHA256, HEAD SHA256, HEAD-only loss and role. They include conservative full SupplyChain source/test closure, recursively referenced Building.Blocks projects and scoped evidence/governance inputs. Evidence files are read-only references, never runtime build inputs. Ignored bin/obj/cache and credentials are not transferable source evidence; no operational credential or DB access is required. Build/restore must be fresh. `project-closure.txt` lists exact project dependencies.

Returns: {'HEAD_IDENTICAL': 182, 'HEAD_ABSENT': 168, 'HEAD_DIFFERS': 8}. Claims: {'HEAD_ABSENT': 193, 'HEAD_IDENTICAL': 182, 'HEAD_DIFFERS': 8}. `head-only-missing-or-different.tsv` enumerates exact omissions/differences; do not rely on HEAD-only checkout or `git diff` alone (untracked files would be lost).

Claims final delta exists but says proposed/HELD; actual pack remains draft. Returns final delta directory is absent at this inspection. Available Returns PREP02 has 47 prospective paths, including ReturnOutboxWorker.cs conflicting with actual approved no-worker design; that row is expressly excluded, not a runtime permission. Claims has 47 proposed paths with no worker. Intersection is 0. This is a measured provisional map, not invented approval for either final delta. User's earlier D186/D187 candidate-design consent remains valid and is not requested again. No accessible separate pack/Phase1.5/runtime grant is inferred from it.

## Baseline transfer plan — not executed

1. CT pins a named source snapshot to this HEAD plus the exact working bytes in source-build rows. Recheck all hashes immediately before capture; if drift occurs, classify only those paths and revise the snapshot manifest. Keep current dirty/index/untracked files untouched. No stash/reset/clean.
2. A later authorized baseline owner creates two isolated execution directories from the SAME source snapshot. HEAD base alone is insufficient: copy exact modified tracked and untracked source files enumerated in the TSVs, retaining their repository-relative paths; verify post-transfer SHA256 against this manifest. Preserve deletions if a refreshed inventory records them. Do not transplant .git, ignored outputs, local credentials or runtime databases. This task creates neither directories nor checkout.
3. Attach each lane's exact reference/evidence inputs separately as read-only material. Scope/pack delta is a proposal until separately approved/applied; never patch real packs as part of baseline copy. Newly released Returns delta replaces the provisional Returns scope with a narrow hash/overlap check, not a new general PREP cycle.
4. Publication handoff supplies actual published YAML/annex paths, version, final hashes, publication/guard result and authorization references. Future published hash is **UNBOUND — DEPENDENCY ON HANDOFF**. Existing final proposal bytes in inventory are reference evidence only and must not be forecast as published bytes. Attach verified handoff to each isolated baseline without writing publication-owned canonical files from consumer lanes.
5. Before build, freeze per-lane source manifest and composition version; after fresh restore/build bind source→binary→process→requests. Actual Returns/Claims runtime uptake is an output of DEV/VER acceptance, NOT a precondition requiring already implemented consumers. Contract-faithful mocks support isolated implementation but cannot close real producer/consumer integration acceptance.

## Producer and published target are separate

`docs/records/audits/2026-09/mvp6-root-r2-producer-ct-accept-01/README.md` accepts bounded isolated11-path Producer evidence against `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb`; it explicitly excludes publication/consumer uptake/rollout/E5. Current local source hashes are inventoried, not declared identical to that accepted source simply because files exist. Final published-target compatibility/binding must be established from publication handoff and exact producer source evidence. Neither this isolated acceptance nor a schema fixture proves final-target consumer runtime uptake.

## Single integration-owner order

Publication owner completes its authorized publication independently. CT records released packs/Phase1.5/isolated runtime scope. Returns and Claims writers then work only in their disjoint released feature paths and finish with exact patches/manifests. One integration owner leases Program.cs, reads its then-current hash, integrates Returns composition first and records an intermediate hash/check, then Claims composition and a final hash/check. This is integration serialization, not a Claims business dependency on Returns acceptance. Preserve existing Carrier/Loads/Shipment routing, JWT and workers; no Returns/Claims worker. The existing generic Shipment branch at Program.cs:68 excludes only Carrier/Loads today; exact Returns/Claims branch exclusions and module registration must be separately authorized and verified. Preserve /returnsXYZ and /claimsXYZ non-matches. No concurrent Program.cs edits, no replacement of shared outbox or new MongoClient inferred.

Independent consumer VER uses that exact composed source/binary. It measures real HTTP/JWT, tenant/LE, receipt/replay, transaction/concurrency/restart and producer seam uptake. Missing producer final binding or live integration stays an explicit acceptance gap, not proof of failure to start bounded consumer DEV. Rollout/data provenance remains separate.

## Evidence limits and remaining handoffs

Need: accessible Returns final delta/owned manifest; actual released pack/Phase1.5/runtime dispositions; publication's final exact target handoff; future assigned isolated directories and single integration lease. No new business approval is produced. Existing Claims prompt wording implying uptake as a start gate is superseded for this map by the current user instruction: uptake is DEV/VER output. Historical documents remain unchanged.

Tests: static path/hash/HEAD comparison and recursive project reference inspection only; no build/runtime/model reruns. Persistence/security evidence: inventory/reference only. Migration/rollback: none performed; no data transfer. Changed files: only this owned audit directory. No publication, pack, runtime, guard or other lane edits. Input hash stability is reported in no-change.txt; concurrent unrelated changes are not attributed to this lane.
