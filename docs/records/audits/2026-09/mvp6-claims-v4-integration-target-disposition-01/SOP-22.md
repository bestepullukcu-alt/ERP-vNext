# MVP6 Claims v4 Integration Target Disposition 01 — SOP §22

Date: 2026-09-27
Role: CT / integration readiness inspection
Runtime/build/test: not run here. User constrained build/test/runtime to Mac Claude Code.
Scope: Q129 CT decision through Q108/CU-28 dependency for MOD-0187 Claims v4.

## Verdict

Q129 is accepted as verification evidence for the Claims v4 draft input, but it does not make Claims v4 fully accepted. The controlling CT decision explicitly keeps MOD-0187 Claims UI v4 at `INTEGRATION_READY`, not `ACCEPTED` or `DONE`, and carries CU-28 plus O-01 to Q108.

The exact integrated target for Q108 must be composed by a single integration writer on the accepted target stack:

1. accepted base from Q103;
2. Q117 accepted overlay;
3. Q121 accepted overlay;
4. Claims v4 23-file module overlay;
5. only the authorized shared integration files, with final preimage checks and recomputed target hashes.

The mutable common checkout is not the target. Its `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is already modified and must be treated as a shared-writer conflict risk for this task.

## Evidence inspected

- `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`
- `docs/roadmap/plans/mvp6-process-pilot-01/MILESTONE-EVENTS.tsv`
- `docs/records/audits/2026-09/mvp6-ct-verdicts-q129-q101b-q121-2026-09-27.md`
- `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/REPORT.md`
- `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/compose/DERIVED-OVERLAYS.sha256`
- `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/compose/LAYERS-BEFORE-AFTER-v4.tsv`
- `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/compose/claims-v4-module-MANIFEST.tsv`
- `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/compose/claims-v4-env-integration-MANIFEST.tsv`
- `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/compose/COMPOSE-LOG-v4.txt`
- `docs/records/audits/2026-09/mvp6-claims-ui-draft-04/SHA256SUMS`
- `docs/records/audits/2026-09/mvp6-claims-ui-draft-04/README.md`
- `docs/records/audits/2026-09/mvp6-claims-ui-draft-04/CHANGES.md`
- `docs/records/audits/2026-09/mvp6-claims-ui-draft-04/FILE-PLAN.tsv`
- `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md`
- `docs/records/decisions/2026-09/mvp6-q101-fix-decisions-owner-decision-01.md`
- `docs/records/audits/2026-09/mvp6-q103-accepted-base-01/SHA256SUMS`
- `docs/records/audits/2026-09/mvp6-q121-mongo-guard-fixes-01/SHA256SUMS`

## Q129 to Q108/CU-28 dependency

Q129 verified Claims v4 in an env-copy composition. That composition used BASE -> Q117 -> v4 and explicitly did not close CU-28. The later CT verdict says future Mac builds use BASE -> Q117 -> Q121 -> module draft. Therefore Q108 must not inherit Q129's env-copy as the final target. It must rebuild the target stack including Q121 and then perform the real integrated route/regression checks.

CU-28 remains the family routing/regression gate. The accepted Q129 evidence closes the v4 fix scope, including CU-25, but it does not prove that Claims v4 is safe in the final shared checkout with real gateway/shared integration.

## Allowed shared files

The only shared files currently supported by the v4 evidence package are:

- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` — add Claims manifest provider registration only.
- `gateway/Diten.ApiGateway/ocelot.json` — add the exact two Claims routes only.
- `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` — route-count guard patch belongs with the gateway route delta, but needs exact target binding in Q108 because Q129 did not apply it.
- `frontend/Diten.Web/Resources/SharedResource.en.resx`
- `frontend/Diten.Web/Resources/SharedResource.tr.resx`
- `frontend/Diten.Web/Resources/SharedResource.fr.resx`
- `frontend/Diten.Web/Resources/SharedResource.es.resx`
- `frontend/Diten.Web/Resources/SharedResource.zh.resx`
- `frontend/Diten.Web/Resources/SharedResource.ar.resx`
- `frontend/Diten.Web/Resources/SharedResource.ru.resx`

`frontend/Diten.Web/Program.cs` is a no-change note. Icon map and platform registration are proposal/checklist items only; they are not authorized source patches without a separate exact preimage/patch/target decision.

## Protected sources

All other files are protected for this disposition. In particular, do not write backend Claims source, contracts, gateway beyond the exact route delta, shared layout/partials/JS/CSS, permission catalogs, personalization catalogs, module/role catalogs, or any current mutable checkout file while another integration writer may own the same surface.

The current checkout is not a clean integrated target. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is modified in the working tree, so this lane must not write it.

## Concrete delta if new scope is needed

If Q108 intends to close CU-28, the minimum additional integration package is:

1. compose BASE(Q103) -> Q117 -> Q121 -> Claims v4 23-file module overlay;
2. apply the authorized shared integration set with exact preimage checks;
3. include the route-count guard patch with exact baseline/patch/target hashes, or explicitly defer it;
4. produce exact deltas for any icon-map, platform registration, permission, personalization, or Ctrl+K writes before applying them;
5. resolve O-01 with an exact diff or exact acceptance disposition, because it is not part of the v4 overlay;
6. hand off writer-complete to a separate testing-agent for Mac build/test/runtime verification.

This package must not treat `INTEGRATION_READY` as acceptance. It only authorizes the next integration writer/tester sequence after all preimages match.

## Required Mac/Claude Code verification after writer-complete

- SupplyChain, Gateway, and Web build from source produced by the composed target.
- Real gateway route checks for the two Claims routes and negative non-match checks.
- CU-27/CU-28 routing and family regression on the integrated target.
- Claims UI/backend smoke that uses real gateway route behavior.
- No direct service-port browser calls, no secrets in evidence, no operational Mongo 27017.

## Output files

- `SOP-22.md`
- `TARGET-MATRIX.tsv`
- `ARTIFACTS.sha256`
