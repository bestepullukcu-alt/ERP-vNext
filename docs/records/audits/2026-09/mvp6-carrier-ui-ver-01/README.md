# MVP6-CARRIER-UI-VER-01 — independent verification

Date: 2026-09-23  
Role: `testing-agent`, independent of UI/integration writers  
v2 immutable snapshot: `/private/tmp/mvp6-carrier-ui-ver-01/snapshot-v2`

## Verdict

**PARTIAL.** The v2 owned rework fixes the authorized-page HTTP 500: exact v2 source renders the Carrier page
with HTTP 200 and the DataTable/create/status surfaces. Fresh build, focused tests, registration, real login,
JWT enforcement, and authenticated UAS-001 pass. A real Auth-issued tenant JWT cannot complete the Carrier
adapter flow because Auth `TokenService` does not emit the mandatory `legal_entity_id` claim for any tenant
identity. The adapter correctly fails closed with 403 and was not weakened.

A diagnostic-only locally signed scoped token confirmed the downstream implementation: empty list 200, create
201, same-key replay 201 with `idempotentReplay=true`, and status change 200/Suspended through
Web MVC → Gateway 5000 → SupplyChain 5061. This token was not Auth-issued, so these checks do not make the
real Gateway/JWT/RBAC flow PASS.

## Exact bindings

- v2 UI manifest: `e96578d0e874e8796469ee763aa9b6e723c7f0c9a966b5fe8a46b535279b26cb`; 21/21 exact.
- v2 UI patch: `3c05dfdf5fb6005d962c1c2d0b33dc8fccd1332a52a812bcdf56af44ff19ec07`.
- v2 Index: `da254157aa3edc3a32434281e71303c1fd7a2bf9bc8863afe4b21884b37ddd0a`.
- Pack target: `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f`.
- BC successor: `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`; 422/422 layered.
- Shared patch: `3fdc9188f635a2428e3ffc5c4ef07c9c4b747b2f74a4196d8e6207cd85ccf1d5`; 18/18.

## Fresh verification

- v2 Web build: PASS, 0 errors, 16 offline/baseline warnings.
- Focused Carrier UI: PASS, 12/12 (writer rework handoff).
- Navigation L10n: PASS, 7/7.
- Gateway Carrier route: PASS, 1/1.
- Manifest/registration unit checks: PASS, 3/3.
- DataTable verifier: PARTIAL, 82 PASS / 9 FAIL; seven pack-forbidden bulk/delete checks, one proxy-profile
  false positive, and one unchanged protected personalization-client guard.
- Seven language key sets: PASS static; Arabic RTL remains without screenshot.

## Live runtime

The exact v2 snapshot ran isolated Platform 5057, Auth 5056, SupplyChain 5061, Gateway 5000, and Web 5001
against disposable `*_mvp6_carrier_ui_ver_01` Mongo databases. Registration synchronized all three Carrier
permissions. Admin and no-role login both returned 200.

- Authorized Carrier page: 200; `dt-carriers`, create offcanvas, save button, and permission payload present.
- UAS-001: 200; access-denied explanation present; DataTable/create surface absent; adapter 403.
- Unauthenticated Gateway and SupplyChain Carrier endpoints: 401.
- Real admin JWT: Carrier read/create/status permissions present; `legal_entity_id` absent.
- Real admin adapter: 403 fail closed.
- Diagnostic scoped path: empty list 200, create 201, replay 201/true, status 200/Suspended.

All five processes were stopped and ports 5000/5001/5056/5057/5061 were confirmed released.

## Browser/screenshot boundary

No browser surface was exposed to this subagent (`listBrowsers()=[]`). Native Chrome was rejected because MCP
elicitation is restricted to the root thread. No screenshots are claimed. Root can capture UAS and authorized
page screenshots, but a real scoped list/create/status browser proof still requires an Auth-supported
`legal_entity_id` claim path.

## Source preservation

The verifier changed no source. The v2 snapshot stayed 21/21 exact. Only this audit directory and
`/private/tmp/mvp6-carrier-ui-ver-01` evidence were written. No commit, push, stash, rollout, E5, or G5 occurred.
