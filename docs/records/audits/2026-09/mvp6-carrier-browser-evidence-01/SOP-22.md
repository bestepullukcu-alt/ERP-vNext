# MVP6-CARRIER-BROWSER-EVIDENCE-01 — SOP §22

Date: 2026-09-23  
Role: evidence-only browser verifier  
Source writer: no  
Verdict: **PARTIAL**

## Authority and exact source

- Controlling pack: `MOD-0184-carrier-management.md`, approved UI scope in §31.
- Prior independent VER: `../mvp6-carrier-ui-ver-01/`.
- UI source manifest: `e96578d0e874e8796469ee763aa9b6e723c7f0c9a966b5fe8a46b535279b26cb`;
  fresh check remained 21/21 exact.
- UI patch: `3c05dfdf5fb6005d962c1c2d0b33dc8fccd1332a52a812bcdf56af44ff19ec07`.
- Index hash: `da254157aa3edc3a32434281e71303c1fd7a2bf9bc8863afe4b21884b37ddd0a`.
- No frontend, Auth, Gateway, permission, shared, pack, canonical, guard, or Git source was changed.

## Isolated process chain

The accepted run used Web/Gateway/Auth/Platform/SupplyChain on
`5201/5200/5256/5257/5261` with separate `*_mvp6_carrier_browser_evidence_01_p52`
Mongo databases. Gateway used a runtime-only disposable Ocelot copy that repointed the existing downstream
routes to the isolated ports. The initial canonical-port attempt was stopped and retained under
`raw/discarded-canonical-port-attempt/`; it is not acceptance evidence.

Platform required the runtime configuration correction
`AuthService:BaseUrl=http://127.0.0.1:5256` before the isolated Carrier permissions could synchronize. This was
an environment-only correction. It did not change repository source.

## Browser result

| Surface | Verdict | Fresh observable |
|---|---|---|
| UAS-001 | PASS | No-role login returned the Carrier page with one denial/remediation card, no DataTable, no create/status skeleton, and no console error. |
| Authorized list shell | PASS | Page rendered `dt-carriers`, `data-dt-standard="v2"`, empty state, search, column visibility, filter, Add Carrier, five data/action columns, and Carrier navigation. |
| GoldenReferenceSlim create | PASS | Add Carrier opened an offcanvas with exactly Carrier Code, Display Name, Supported Modes, and optional External Reference. Required markers matched the first three fields. |
| Status surface | PARTIAL | Status offcanvas markup exists and permission-gated status action is wired, but no row can be produced with the current Auth-issued token; live opening remains Auth-dependent. |
| Seven module languages | PASS | `en,tr,fr,es,zh,ar,ru` rendered localized title, description, Add Carrier, headers, and empty state. Arabic rendered `lang=ar`, `dir=rtl`. |
| Tenant shell localization | PARTIAL | The shared `Search [CTRL + K]` label remained English in the Arabic rendering. This is outside the 21 Carrier-owned paths. |
| Console | PARTIAL | No JavaScript errors. Ten distinct L10n warnings were emitted: Active, Actions, Apply, Cancel, Filter, Passive, Reset, Save, Status, Unknown. |
| Same-origin route | PARTIAL | Browser page origin was Web `5201`; Web runtime logs show downstream calls to Gateway `5200`, and runtime config maps SupplyChain to `5261`. The browser tool did not expose a network-request ledger, so this is not a full browser-network capture. |
| Responsive breakpoints | PARTIAL | Desktop render was captured. The browser viewport capability accepted 768/390 requests but `window.innerWidth` remained 1280, so tablet/mobile PASS is not claimed. |
| Screenshot files | PARTIAL | Four screenshots were visibly captured by the browser tool and SHA-256-bound in `SCREENSHOT-INDEX.tsv`. The browser URL policy rejected the only available in-tool export route, so no PNG file is claimed in the repository archive. |

## Auth boundary

The real Auth-issued tenant token still omits `legal_entity_id`. Carrier MVC correctly fails closed with 403
before forwarding a scoped list/create/status request. The browser displayed the list/create surfaces because
the admin has the independent read/create/status permissions, then showed the localized permission-loss toast
when the list request hit the missing-scope boundary.

The following remain **OPEN pending Lane A real Auth handoff**:

- Auth-issued list data and tenant/LE 404 isolation;
- create success, same-key replay, changed-payload response, and duplicate handling;
- status action and transition results;
- browser network trace for those mutations.

The previous diagnostic signed-token flow was not reused or relabeled.

## Findings

1. **BR-01 — shared/runtime L10n warnings:** ten distinct missing-key warnings are observable in every Carrier
   page load even though visible Carrier text localizes. Source rework was not authorized.
2. **BR-02 — Arabic shared shell label:** `Search [CTRL + K]` remains English while Carrier-owned Arabic text and
   RTL layout are correct. This is a shared-shell finding.
3. **BR-03 — responsive evidence gap:** the provided browser viewport override did not alter the IAB viewport;
   no 768/390 browser PASS is asserted.
4. **BR-04 — screenshot retention gap:** screenshots were captured and hashed, but the browser security policy
   blocked data-URL export. No alternate browser/CDP/screenshot mechanism was used.
5. **BR-05 — transient isolated login attempt:** one admin login returned 500 because an Auth Mongo write was
   canceled; the unchanged environment succeeded on retry. The failure remains in raw logs and is not treated
   as a product acceptance PASS or a hidden discarded result.

## Disposition

This lane closes the previously absent real-browser evidence for UAS-001, the authorized UI shell,
GoldenReferenceSlim create rendering, DataTables v2 desktop rendering, seven Carrier languages, and Arabic RTL.
Overall remains **PARTIAL** because Auth-scoped mutation E2E, responsive breakpoints, permanent PNG retention,
full browser-network capture, and the two localization findings remain open.

