# MVP6-MOD0184-UI-VER-01 — v1.0 — HELD

**Role:** independent frontend/integration verifier who did not write the UI  
**State:** HELD until active DEV writer-complete and immutable handoff.

## Preconditions

- verify the real UI owner/runtime authorization, exact applied pack target, contract/annex hashes, successor baseline, UI source manifest, shared integration decisions, and DEV writer-complete;
- use a hash-bound disposable snapshot; do not share the writer's mutable checkout;
- preserve the existing bounded backend CT acceptance without treating it as UI evidence.

## Verification

1. Confirm changed paths are exactly the authorized UI allowlist plus separately approved integration artifacts; protected/shared/non-Carrier paths remain hash-stable.
2. Run build, focused tests, DataTables v2/Golden Slim verification, and seven-language key/RTL parity.
3. Independently exercise A01–A12: 401/403/UAS-001, tenant/LE hiding, empty list, exact create validation, duplicate code, lifecycle/race 422, correlation, same-key replay, changed-payload conflict, 500/503 retry, and restart/reload behavior applicable to the UI.
4. Verify browser traffic is same-origin and all Carrier service egress passes Gateway 5000; prove no request reaches 5061 directly and no unsupported operation/query is emitted.
5. Separate UI/controller/model results from composed Gateway/JWT evidence. A mock or fixture does not prove route, shared permission registration, or live backend uptake.

## Output

Produce SOP §22, exact input/source/binary/process/evidence hashes, acceptance matrix, raw outputs, no-change proof, and PASS/FAIL/PARTIAL with only real remaining gaps. Do not fix source, alter pack status, change contracts/gateway/shared files, or grant E5/G5/full-module acceptance.
