# MVP6-MOD0183-UI-VER-01 — v1.0 — HELD

**HELD:** Run only after authorized DEV writer-complete on a frozen final source manifest. The verifier must not be the writer.

Work Package ID: MVP6-MOD0183-UI-VER-01  
Prompt Version: 1.0  
Module: MOD-0183 Shipment Tracking & POD  
Agent Lane Type: VER  
Target Agent / Entry Point: `testing-agent` / independent read-only verification  
Risk Class: HIGH  

## NE

Independently verify the exact final UI/shared source against `ACCEPTANCE.md`; do not invent a broader VER scope.

## NASIL

- Freeze final manifests and use a separate disposable checkout, ports, DB-010 database and evidence directory.
- Fresh build the exact source; bind source→binary→process→MVC→Gateway 5000→SupplyChain 5061.
- Verify five operations, exact query/body fields, list/detail/create/transition/POD, server paging boundaries and no invented calls.
- Use real JWT/RBAC for independent read/create/dispatch/cancel/POD permission negatives, UAS-001 and tenant/LE safe 404.
- Verify stable-key replay, changed-payload conflict, response-loss retry, exact errors and trace/root separation.
- Run DataTables quality gate, seven-language parity/RTL, accessibility and measured 390/768/desktop browser checks.
- Check console/network logs: no direct 5061 request, token exposure, unknown route or unhandled error.
- Preserve failed attempts and report any missing screenshot-export capability as OPEN rather than fabricating evidence.

## YAPMA

No source/pack/gateway/Auth/canonical/guard fix; no diagnostic token acceptance; no unit/model PASS promoted to E4/browser
PASS; no full-module, rollout, E5/G5, commit/push/stash claim.

## Output

SOP §22 independent verdict, source/hash manifest, raw commands and exit codes, permission/tenant/replay/browser matrix,
screenshot index when supported, no-change record and exact remaining blockers.

