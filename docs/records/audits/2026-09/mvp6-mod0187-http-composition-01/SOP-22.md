# MVP6-MOD0187-HTTP-COMPOSITION-01 — SOP §22

## Verdict

**REVIEWABLE COMPOSITION DIFF — NOT APPLIED; exact integration authority is missing.**

## Source and baseline

Registered worktree: `/Users/natig/.codex/worktrees/claims-dev-start-01/ERP-vNext-recovery`.
Branch `codex/mod-0187-claims-dev-start-01`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Claims DEV is complete for its 47 owned paths; the 428-input baseline is evidence context, not an owned source set.

Program.cs baseline SHA256: `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`.
Proposed full integration handoff patch SHA256: `2ff9625201dae4c2a6f8f1fd75e00bc76bea95b7d2ade008bbf20c5346ddd1f7`.

## Exact composition scope

Only `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is shared. The proposed diff:

- registers Claims API/Application/Persistence/Infrastructure namespaces;
- registers `AddClaimPersistence()`;
- registers a five-second `IClaimReferenceReader` HttpClient;
- adds Claims invalid-model response handling;
- adds Claims middleware after authentication;
- excludes Claims paths from generic Shipment middleware.

It adds no endpoint, permission, JWT behavior, gateway route, serializer, worker, or shared refactor. Claims business files remain the 47 owned DEV paths.

Disposable target Program.cs SHA256 after applying only the Program.cs hunk:
`a28cb1ab5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a`.

## Validation

- `git apply --check` against the registered Claims worktree: exit 0.
- Disposable composition apply: successful.
- Disposable SupplyChain API build after Program.cs composition: **0 warnings, 0 errors**, exit 0.
- Build output: `/private/tmp/mvp6-mod0187-composition-build-01/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll`.

## Authority disposition

The recorded owner authority in `evidence/claims-dev/owner-authority.md` explicitly states that the prior delta approval did **not** approve shared composition; it requires a separately approved exact composition diff. No such approval is present in the supplied inputs. Therefore the diff was not applied to the Claims worktree or common checkout.

## Required single decision

Approve the exact Program.cs composition diff identified by source SHA `7fdb5ef0…f204d8`, proposed patch SHA `2ff96252…dd1f7`, and target SHA `a28cb1ab…5ab34a`, with no additional shared files or behavior. This approval must be separate from Claims business/runtime acceptance and must not authorize gateway, JWT relaxation, rollout, commit or push.

## Handoff after approval

After exact approval, the single integration owner may apply only the Program.cs hunk, rebuild the API, and hand Claims DEV the composed binary/source hashes for real HTTP/JWT, producer uptake and restart verification. Independent VER and CT acceptance remain required.
