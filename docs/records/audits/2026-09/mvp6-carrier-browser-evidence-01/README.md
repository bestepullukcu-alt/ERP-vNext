# MVP6-CARRIER-BROWSER-EVIDENCE-01

Final browser-evidence verdict: **PARTIAL**.

The controlling report is [SOP-22.md](SOP-22.md). Supporting matrices are
[BROWSER-ACCEPTANCE.tsv](BROWSER-ACCEPTANCE.tsv) and [SCREENSHOT-INDEX.tsv](SCREENSHOT-INDEX.tsv).

---

## Environment readiness record

Status: **READY for root-thread browser capture; Auth-dependent E2E OPEN**  
Source: immutable v2 snapshot `/private/tmp/mvp6-carrier-ui-ver-01/snapshot-v2`

## Exact source binding

- UI manifest: `e96578d0e874e8796469ee763aa9b6e723c7f0c9a966b5fe8a46b535279b26cb`; 21/21.
- Index: `da254157aa3edc3a32434281e71303c1fd7a2bf9bc8863afe4b21884b37ddd0a`.
- Web binary: `33496b3a05bf03678238897d4cfb924f8b8657ea883b51cbe9255d5fff0cb172`.
- Existing independent VER: `../mvp6-carrier-ui-ver-01/README.md`.
- Governing pack: MOD-0184 Carrier Management §31; UAS-001, DataTables v2, tenant seven-language and
  same-origin MVC→Gateway rules remain controlling.

## Isolated runtime

| Component | URL/port | Disposable DB |
|---|---|---|
| Web | `http://127.0.0.1:5201` | `DitenWeb_mvp6_carrier_browser_evidence_01_p52` |
| Gateway | `http://127.0.0.1:5200` | n/a |
| Auth | `http://127.0.0.1:5256` | `DitenAuth_mvp6_carrier_browser_evidence_01_p52` |
| Platform | `http://127.0.0.1:5257` | `DitenPlatform_mvp6_carrier_browser_evidence_01_p52` |
| SupplyChain | `http://127.0.0.1:5261` | `DitenSupplyChain_mvp6_carrier_browser_evidence_01_p52` |

Gateway uses a disposable runtime-only Ocelot copy under
`/private/tmp/mvp6-carrier-browser-evidence-01/gateway-runtime`. All downstream routes originally targeting
5056/5057/5061 are mapped to 5256/5257/5261. Repository source is unchanged.

The earlier canonical-port attempt is retained under `raw/discarded-canonical-port-attempt/` and is not PASS
evidence.

## Browser entry

Open:

`http://127.0.0.1:5201/account/login?tenantId=00000000-0000-0000-0000-000000000001`

Use the repository development runbook credentials communicated to the root task. Credentials and cookies are
not stored in this audit.

- Authorized seed user: `admin@diten.com`.
- UAS no-role user: `john.doe.def@diten.com`.
- Carrier page: `http://127.0.0.1:5201/SupplyChain/Carriers`.

## Auth-independent browser captures

The root browser can capture:

1. authorized page 200 with tenant shell, `dt-carriers`, four create fields, create/status permission surfaces;
2. UAS-001 page for the no-role identity: one denial/remediation message and no table/create/status skeleton;
3. seven language rendering and Arabic `dir=rtl`;
4. same-origin browser requests to Web 5201 and absence of direct browser calls to 5261;
5. DataTables v2 markup/controls and SweetAlert2-backed UI wiring visible before a successful backend mutation.

## Open boundary

Auth `TokenService` does not emit `legal_entity_id` in tenant JWTs. Carrier MVC correctly requires that signed
claim and returns 403 before forwarding. Therefore Auth-issued list/create/status/replay, tenant/LE 404 hiding,
and full browser mutation evidence remain OPEN. No adapter weakening or synthetic browser identity is allowed in
this lane.

Platform `/health` returns 503 because optional dependency readiness is degraded; internal registration,
branding/login settings, and the verified login route operate. Auth/Gateway/SupplyChain health are 200.

## UAS credential revalidation

The isolated Auth DB contains an active default-tenant `john.doe.def@diten.com` identity with zero role
assignments. Live Web→Gateway→Auth login returned 200; `/SupplyChain/Carriers` returned 200 with one UAS denial,
no DataTable, and no create surface. Credentials are communicated out of band and not archived.

Platform was restarted with `AuthService:BaseUrl=http://127.0.0.1:5256`, then SupplyChain re-registered its
manifest. All three Carrier permissions synchronized to isolated Auth. Admin login/page preflight now returns
200 with DataTable, create, and status surfaces and no access-denied card.

## Cleanup

Browser capture completed and the isolated stack was stopped. Ports 5200, 5201, 5256, 5257, and 5261 have no
listeners. The v2 21-path source manifest was rechecked after shutdown with zero mismatches.

Raw evidence retains the initial separate-port callback drift, the runtime-only Platform Auth callback correction
to port 5256, Carrier permission resynchronization, the transient admin login HTTP 500 caused by a cancelled
Mongo audit write, subsequent successful admin/no-role preflights, final listener cleanup, and source no-change.
