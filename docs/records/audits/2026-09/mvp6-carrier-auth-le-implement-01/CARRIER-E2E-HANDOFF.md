# Carrier E2E handoff

Status: **HELD — Auth/Platform candidate requires rework.**

Do not use this candidate to claim a real Auth-issued Carrier E2E pass. The actual login and refresh tokens still omit `legal_entity_id` because the new Platform internal endpoint returns HTTP 400 before resolver evaluation. Carrier's existing missing-claim 403 remains the controlling fail-closed behavior.

Carrier E2E may resume only after:

1. F1 tenant-context handling is corrected and independently verified;
2. F2 server-to-server MDM authorization is explicitly designed, authorized, implemented and independently verified;
3. a real Auth-issued token contains the server-authoritative `legal_entity_id` for exactly-one scope and omits it for zero/multiple/invalid/unavailable scope;
4. the same token passes Carrier tenant/LE/RBAC checks without changing Carrier UI or middleware.

This handoff grants no new source authority.
