# HELD — MVP6-MOD0183-SHIPMENT-POD-UI-DEV-v2.0

**HELD — not executable until OWNER-DECISION-PACK A–D, immutable target creation and preimage verification are complete.**

## NE
Implement the approved MOD-0183 tenant UI in exactly the 28 paths bound by `UI-PREIMAGE-MANIFEST.tsv` `9bbad7bb3d01f8500711ae75c30cf1fbcb238b9af77c009fdacfb4b29efd3dfe`. Preserve list/create/detail/transition/POD, Compact and 13 user inputs.

## NEDEN
The backend is bounded-accepted, while the UI, transfer and shared integration are not yet materialized on one authorized source set.

## NASIL
1. Record branch/HEAD/worktree/dirty state and verify pack target, the 40-row transfer manifest `0cd90929fb7d2816443db05234a60e1895a5c057f271249a49ec14c7b737ae05`, shared patch `6d9cad8e59cdab9559a3c822a1b0135672b1a4672d7988235586983cb45ba1bd` and all target hashes.
2. Use `_LayoutTenantShell`, GoldenReferenceCompact, DataTables v2, same-origin MVC adapters and Gateway 5000 only.
3. Implement exact frozen fields, lifecycle, POD references, stable per-intent idempotency, trace/root separation, UAS-001 and independent RBAC gates.
4. Keep all source changes inside the 28 UI-owned paths. Shared files are integration-owner outputs and read-only to this writer.
5. Run frontend build, focused controller/form/JS/resource tests, DataTable quality gate and isolated Gateway-backed runtime/browser checks.
6. Produce SOP §22, exact source manifest, build→process→browser/HTTP evidence and writer-complete.

## YAPMA
No shared/gateway/Program/Auth/contract/guard/Carrier changes; no direct 5061 browser call; no edit/delete/bulk/assign/reconcile/history/upload; no permission or route invention; no commit/push/stash.

## DOĞRULA
Map UI183-A01–A16 to exact tests/evidence, including 390/768/desktop, RTL, keyboard focus, tenant/LE isolation, replay/conflict/failure/restart and the recorded port-5061 environment disposition. Static candidate PASS is not runtime acceptance.
