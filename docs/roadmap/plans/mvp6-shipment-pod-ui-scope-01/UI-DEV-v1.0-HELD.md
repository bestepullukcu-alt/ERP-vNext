# MVP6-MOD0183-UI-DEV-01 — v1.0 — HELD

**HELD:** Bu belge UI DEV yetkisi degildir. Owner exact pack delta/target'i, UI Phase 1.5'i, durable source transferini
ve shared integration patch'ini onaylayana kadar uygulanmaz.

Work Package ID: MVP6-MOD0183-UI-DEV-01  
Prompt Version: 1.0  
Module: MOD-0183 Shipment Tracking & POD  
Agent Lane Type: DEV + INT  
Target Agent / Entry Point: `@orchestrator`; `frontend-ui-ux` only for 28 UI-owned paths; one `integration-agent` for shared files  
Risk Class: HIGH  
Target Branch: feature/mvp6-logistics  
Expected Base HEAD: 4a8d4d4b339528a88e6220fb8402e5a2c771136c  
Module Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` with owner-approved UI delta  
Shell / Golden / Fields: tenant / compact / 13  
Pattern: mixed — DataTables list, separate Compact create/detail, bounded transition/POD offcanvas actions  

## Preconditions

1. `PROPOSED-PACK.patch` exact bytes and resulting pack hash are owner-approved and applied in an isolated registered checkout.
2. `PHASE15-ACCEPTANCE.tsv` gaps are closed with evidence.
3. An immutable source/transfer manifest names the accepted Shipment source and the target checkout.
4. The single shared integration patch has exact preimage/patch/target hashes and separate authority.
5. No other writer owns the same gateway, module registration, navigation resources or target checkout.

## NE

Implement only the five-operation first tenant UI defined by `SCREEN-ROUTE-PERMISSION.tsv` and `ACCEPTANCE.md`.

## NASIL

- Write only `OWNED-PATHS.txt`; integration owner alone writes separately approved shared paths.
- Use same-origin MVC adapters and Gateway 5000. Do not call 5061 from browser code or mint/read bearer tokens in JS.
- Implement DataTables v2 list, Compact create with repeatable lines, read-only detail, transition action and POD reference action.
- Enforce UAS-001 and independent read/create/dispatch/cancel/POD UI gates; backend remains authoritative.
- Preserve exact contract fields, lifecycle, errors, idempotency and correlation/root distinction.
- Ship real en/tr/fr/es/zh/ar/ru translations, Arabic RTL, 390/768/desktop responsiveness and SweetAlert2 wrappers.
- Run build, exact frontend tests, `verify_datatable_page.py`, route/permission negatives and composed Gateway/browser evidence.
- Archive source→build→process→browser evidence and declare writer-complete.

## YAPMA

No backend/canonical/guard/Auth/Carrier writes; no edit/delete/bulk/assign/reconcile/history/upload/lookup endpoint; no
global layout/JS changes; no direct service browser traffic; no commit/push/stash; no E5/G5/full-module claim.

## Failure protocol

Any preimage mismatch, shared conflict, unsupported contract need or missing authority stops the affected part and produces
an exact rework/authority finding. Do not widen scope or overwrite another lane.

## Output

SOP §22 handoff, exact source/test/shared manifests, build/process/browser raw evidence, screenshots when the approved tool
supports artifact export, acceptance matrix, protected-path hashes and writer-complete. Independent VER starts afterward.

