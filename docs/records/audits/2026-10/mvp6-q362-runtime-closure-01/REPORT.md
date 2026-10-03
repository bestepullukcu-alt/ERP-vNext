# Q362 — MOD-0183 runtime closure: what a real user sees

- Lane: Q362. Roles: integration-agent (stack); frontend-ui-ux (judgement on what renders). Exclusive on the app ports
  for the run. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 17:33:32 UTC 2026` · `feature/mvp6-logistics` · HEAD `bf1ef28f9` · dirty 6 · staged 0. Ports
  5000, 5001, 5056, 5057, 5061 and 5672 were free.
  27017 was held by another process (`mongod`, pid 766). It was not touched; this lane used its own mongod.
- Step 0 read: `AGENTS.md`, `git-safety.md`, `integration-agent.md`, the pack (§11, §12; the pack has no §18.0 —
  SOP §18.0 is meant), the Q264 `UX-STATES.tsv` and the Q358 REPORT, all before the run.
  `frontend-ui-ux.md` (`90247ddc…`), `CLOSURE-PLAN.tsv` (`108b3636…`) and `GOLDEN-FLOW.md` (`5a58d861…`) were read
  **after** the measurement, before this report. Stated plainly; nothing here was re-ruled because of that order.
- **Nothing was fixed in the repository. Nothing staged or committed.** The AdminModules entry was not touched. No
  token, password or key appears in this record: the scan is at the end, and antiforgery values in saved HTML are
  replaced with `REDACTED`.
- **Agent verdict ≠ CT ACCEPTED.**

## The headline: the cause changed, and it is not the permission

Q358's widening works. The tenant Admin's token carries all five `supplychain.shipments.*` keys (measured,
`evidence/fixture.json`). **That Admin still cannot use the Shipment screens as the tree stands.**

The Web adapter refuses every Shipment API call before the gateway. It does this when the token lacks one
`legal_entity_id` claim (`SupplyChainShipmentsController.cs:156-158`), and no tenant user's token gets one at HEAD.

The legal-entity scope chain is uncomposed in three services:

| service | file | missing at HEAD (present in Q185's lane tree `~/mvp6-env/q185/treeB`) | effect |
|---|---|---|---|
| Auth | `Diten.AuthService.Infrastructure/DependencyInjection.cs` | `AddHttpClient<ITenantLegalEntityScopeClient, PlatformTenantLegalEntityScopeClient>(…)` (+6 lines) | `LoginCommandHandler.cs:184-186` gets a null client and never asks for a legal entity |
| Platform | `Diten.Platform.Infrastructure/DependencyInjection.cs` | `using …Application.Common;` · `Configure<MdmServiceIdentityOptions>` · `AddScoped<IInternalScopeResolutionContext, InternalScopeResolutionContext>` · `AddSingleton<IMdmServiceIdentityTokenProvider, MdmServiceIdentityTokenProvider>` (+4) | `GET /api/internal/tenants/{t}/users/{u}/legal-entity-scope` fails at activation. Measured: 400 "Unable to resolve service for type 'IInternalScopeResolutionContext'" |
| MDM | `Diten.MdmService.Api/Program.cs` | `using …Api.Security;` · `Configure<PlatformServiceIdentityOptions>` · `AddSingleton<IPlatformServiceTokenValidator, PlatformServiceTokenValidator>` (+4) | MDM cannot validate Platform's service token |

Q185's three files are strict supersets of HEAD's. The diffs contain only lines HEAD lacks
(`evidence/counterfactual/*.diff`: +6, +4, +4, −0).

In addition, the paired service identity (`MdmServiceIdentity__Enabled`, `__KeyId`, `__Secret` on Platform;
`PlatformServiceIdentity__*` on MDM) appears in **no** config file, and `Enabled` defaults to false. Q185 supplied it
by environment, and so did this lane.

**So every one of the 15 Q264 rows, R-09 and the R-01 actions has a new single cause: no `legal_entity_id` claim,
because 14 composition lines never reached the committed tree.**

## How it was run

- Environment `~/mvp6-env/q362-20261003-2037/`. A scratch copy of Auth, Platform, SupplyChain, MDM, Gateway, Web and
  their shared projects: 7,684 + 335 files, 0 mismatches against the repository. Everything was built there; nothing
  was built inside the repository.
- One mongod on **57362** (`rsq362`); one database per service.
- Ports 5000 (gateway), 5001 (web), 5056 (auth), 5057 (platform), 5061 (supplychain), and **5059 (MDM)**. MDM was
  added because the legal-entity check reaches it. Helper on 5199.
- One throwaway JWT secret, and one throwaway service-identity secret, both mode 600 and never printed.
- **Users.** Created through Auth's own invite endpoint (`/internal/events/tenant-admin-invited`, the call Platform
  makes). The forced first-login change was done through the gateway, and the new passwords are mode-600 files only.
  The browser fetched each password from the helper inside the page, so no value passed through the transcript.
  - `q362-admin` — tenant Admin, all five keys.
  - `q362-nokey` — its Admin role removed in the lane database, 0 keys.
  - `q362-fix` — org-setup role, inserted the Q185 way.
- **Org fixture.** Q185 part B method, through the gateway with the fixture admin's real token: legal entity in MDM
  (created and activated), OU, positions, assignments for admin and nokey.
- **Two stacks, in sequence:**
  - **AS-IS** — HEAD plus the uncommitted Q358 widening. The verdicts come from here.
  - **COUNTERFACTUAL** — the same stack with Auth, Platform and MDM rebuilt from a `cp` of the copy, and only Q185's
    three files placed over it. The question it answers: does anything **else** block the flow? It is evidence about
    the rest of the system, not a verdict on the product.
- Stopped at the end by recorded pid: web, gateway, supplychain, mdm, platform, auth, helper, then mongod. Ports
  5000, 5001, 5056, 5057, 5059, 5061, 5199 and 57362 were free afterwards.
- Freezing the service: the loading states were held by `SIGSTOP`/`SIGCONT` on **this lane's own** SupplyChain
  process. The save-failed states stopped and restarted that same process.

## R-03 — the denied screen, live (AS-IS) — **MET**

User `q362-nokey` (0 keys), in the tenant shell:

| page | HTTP | redirect | shell | tables | form inputs | action buttons | skeleton | shipment XHR | card |
|---|---|---|---|---|---|---|---|---|---|
| `/SupplyChain/Shipments` | 200 | no | yes | 0 | 0 | 0 | 0 | 0 | "You don't have access to Shipments" |
| `/SupplyChain/Shipments/Create` | 200 | no | yes | 0 | 0 | 0 | 0 | 0 | "You don't have access to Create Shipment" |
| `/SupplyChain/Shipments/Details/{id}` | 200 | no | yes | 0 | 0 | 0 (0 offcanvas) | — | 0 | "You don't have access to Shipment Details" |

UAS-001's rules hold live. The gap Q264 found remains: the page answers **200** where UAS-001 §3 labels this case
403. Evidence: `shots/R03-*`, `html/R03-*`, `evidence/R03-*.json`.

## R-01 — Save, Change Status, Capture POD, list load, through the gateway as it stands (AS-IS)

User `q362-admin`, holding all five keys:

- **List load.** The page gate passes (Create Shipment button, `index.js` loaded). `GET /SupplyChain/Shipments/api`
  → **403** from the Web adapter. Gateway `shipment-bundle` lines: **0**. `index.js` then draws **"You don't have
  access to Shipments"**: a correct UAS rendering, but a false statement to a user who holds the permission.
  `shots/asis-admin-index-denied-after-403.jpg`.
- **Save.** `POST /SupplyChain/Shipments/api` → **403**, body
  `{"error":{"code":"INVALID_REQUEST","message":"Required authorization context or permission is missing."…}}`. The
  user reads **"Check the entered values. Support reference: …"**. `shots/R01-asis-create-save-403-shown-as-validation.jpg`,
  `evidence/asis-create-save-403.json`.
- **Change Status, Capture POD.** **NOT MEASURED as-is.** Neither exists until a shipment exists, and no shipment can
  be created as-is. In the counterfactual both work (R-09).

## R-09 — the golden flow (COUNTERFACTUAL stack) — **runs end to end**

The five elements, as observed:

| element | observed |
|---|---|
| Actor | tenant user `q362-admin` of tenant `…0001`, role Admin, five Shipment keys, legal entity `e5f15d4b…` (one position in an OU bound to that LE) |
| Trigger | the user opens Shipments → Create Shipment |
| Interaction sequence | list → Create (11 required fields) → Save → Details → Change Status (Planned) → reload → Change Status (Dispatched) → reload → Capture POD → reload → Change Status (Closed) → reload |
| Expected response | each command answered 200/201 with the new status; a confirmation dialog before each command; toast "Action completed."; the Details page re-reads the shipment |
| Success result | shipment `SHP-65cc49a9…` in **Closed** after reload; POD shown (recipient, received at, evidence, note); no actions left |

| step | request → status | after reload |
|---|---|---|
| list | GET 200 (empty) | — |
| create | POST 201 `status: Draft` (correlation `12dc94e4…`) → redirect to Details | Draft |
| Planned | POST …/transition 200 | Planned |
| Dispatched | POST …/transition 200 | Dispatched; Capture POD appears |
| POD | POST …/pod 201 | Delivered; POD card filled |
| Closed | POST …/transition 200 | Closed; 0 action buttons |

The database agrees (`evidence/R09-db-check.txt`): `Status: 'Closed'`, `Version: 5`, `LegalEntityId: e5f15d4b…`, POD
stored, 5 audit rows (one per mutation), 6 outbox events. Screenshots: `shots/R09-*`.

## R-05 — the 15 rows (`UX-STATES.tsv`)

**As-is, all 15 remain unreachable, for the new cause above.** In the counterfactual:

| | loading | empty | validation error | save-failed | conflict |
|---|---|---|---|---|---|
| Index | MEASURED | MEASURED | N/A (no write) | N/A — load-failed measured | N/A (no write) |
| Create | N/A (no load; 0 XHR) | MEASURED | MEASURED (en + ar) | **DEFECT** | not reachable by design |
| Details | **DEFECT** (cards drawn before data) | MEASURED (404) | MEASURED (C-07) | **DEFECT** | MEASURED (422) |

RTL (ar) was exercised on the list and on the Create form. `dir=rtl`, Arabic labels and Arabic validation text, no
horizontal scroll (827/827).

## R-07 — one correlation ID in three logs — **NOT MET**

The Create action, correlation `12dc94e4-1e20-4e1d-94cb-19db20dbed3e`:

| log | lines |
|---|---:|
| Web | **0** |
| Gateway | 1 for the create (7 for the whole flow) |
| SupplyChain | 8 for the create (48 for the whole flow) |

```
web:         (none)
gateway:     {"Timestamp":"2026-10-03T20:55:03.1758210+03:00",…"RenderedMessage":"HTTP \"POST\" \"/api/shipment-bundle/shipments\" responded 201 in 217.9366 ms",…}
supplychain: [20:55:02 INF] Request starting HTTP/1.1 POST http://localhost:5061/api/shipment-bundle/shipments … CorrelationId=12dc94e4-1e20-4e1d-94cb-19db20dbed3e
```

Gateway and service carry the ID; the service half of O-1 stays fixed (Q270). **Web still logs none of it.**

Also observed: the Details page sends the shipment's **lifecycle root** (`12dc94e4…`) as `X-Correlation-Id` for every
command (transition and POD), so one ID spans four user actions. "One ID per single user action" therefore holds only
for the create. Files: `evidence/logs/R07-*`.

## Known defects — confirmed or falsified

| id | verdict | evidence |
|---|---|---|
| D-1 (403 from list → toast + empty table) | **FALSIFIED.** A 403 renders the denied card via `showDenied` | AS-IS `shots/asis-admin-index-denied-after-403.jpg` |
| D-2 (failed list load → empty table + toast) | **FALSIFIED.** 503 renders the load-failure alert with the table hidden and no toast. The user waits **90 s** first (adapter on the default 100 s `HttpClient`; the gateway answered 503 at 90 s) | `shots/D2-index-load-failed-503-after-90s.jpg` |
| C-07 (empty date throws) | **FALSIFIED.** No exception; the field is marked and focused; no request sent. POD the same | `shots/R05-details-validation-error-C07-empty-date.jpg` |
| C-08 (2000 vs 1000) | **The mismatch is FALSIFIED.** The note field is `maxlength=1000` and 1996 typed characters leave 1000. **What the user is told: nothing.** The text is cut silently, with no counter or message | `UX-STATES.tsv`; `R05-details-validation-error-*` session |
| status names English | **CONFIRMED** in the running ar and zh pages: the 8 filter options and the row badges are English, everything around them localized | `shots/R05-index-ar-rtl-status-names-english.jpg`, `shots/L10N-index-zh-status-names-english.jpg` |

## SOP §18.0 — what this closes and what it leaves open

| row | after Q362 | why |
|---|---|---|
| 1 Golden flow | **OPEN** | the UI journey is now written from observation (above), but it runs only on the counterfactual stack. As the tree stands it cannot be performed |
| 2 No-shell | **OPEN — new blocker** | the route, the registration and the keys reach the user; the legal-entity claim does not (14 lines + 2 config sections) |
| 7 Validation | client half **measured live** (C-07 falsified, note limit 1000); not closable as-is because Save itself fails | — |
| 8 RBAC / Tenant (+UAS-001) | **R-03 live: MET** (HTTP 200 vs §3's 403 stays as Q264 F-2) | the only row this lane closes, and only its live-UAS requirement |
| 12 UX states | **OPEN** | as-is: unreachable. Counterfactual: 10 measured, 3 of them defects (save-failed shown as validation ×2, Details loading); 4 N/A; 1 unreachable by design |
| 13 Observability | **OPEN** | R-07: the Web log carries no correlation |
| L10n | **OPEN** | the status names are English in ar and zh |

## Findings

- **F-Q362-1 (blocker).** The legal-entity scope chain is uncomposed at HEAD in Auth, Platform and MDM: 14 lines,
  present in Q185's lane tree and never committed. No tenant user can use any Shipment screen.
  `evidence/counterfactual/*.diff`; Platform 400 "Unable to resolve service for type 'IInternalScopeResolutionContext'".
- **F-Q362-2.** `MdmServiceIdentity` (Platform) and `PlatformServiceIdentity` (MDM) are in no config file and default
  to disabled. Deployment must supply them as a pair: `Enabled`, `KeyId` and a ≥32-byte `Secret`, in the secret store.
- **F-Q362-3.** A user who holds the keys but lacks the scope claim is told "You don't have access to Shipments" on
  the list and "Check the entered values." on Save. Neither names the real cause.
- **F-Q362-4.** A 502 from the gateway (service down) is shown as **"Check the entered values."** on Create save,
  Details save and Details load. The adapter passes the empty-bodied 502 through and `create.js`/`details.js` fall
  back to the validation message.
- **F-Q362-5.** A failed list load takes **90 s** to surface. There is no client or adapter timeout below the 100 s
  default.
- **F-Q362-6.** The Details loading state draws every card with empty values under a "Loading…" heading (Q231
  confirmed; `frontend-ui-ux.md` mandates the skeleton loader).
- **F-Q362-7.** The note is truncated silently at 1000: no counter, no message.
- **F-Q362-8.** A second create with the same source document is accepted (two shipments, `SO-Q362-GOLDEN`). This is
  recorded, not ruled: the pack's duplicate rule is source-identity based for Warehouse intake.
- **F-Q362-9.** Details sends the lifecycle root as the correlation ID for every command, so R-07's
  "one action, one ID" holds only for create.
- **F-Q362-10.** The Details action bar (Change Status, Capture POD) sits at the bottom of the page, not in the
  header. Pack §22 open gap 3 already records the placement question.
- **F-Q362-11.** The Change Status `occurredAt` field was prefilled 17:55 while local time was 20:55. That looks like
  UTC rendered in a `datetime-local` field. Observed once, not traced.
- **F-Q362-12.** The shell's search placeholder "Search [CTRL + K]" is English in ar and zh. It is outside the module;
  noted only.

## Not measured

- Change Status and Capture POD **as-is**: they cannot be reached as-is.
- Create conflict: not reachable by design.
- RabbitMQ: not started. Auth and Platform `/health` were 503 throughout, as in Q264. Every flow above worked
  without it.

## Hygiene

- Every record file was scanned for the JWT secret, the service-identity secret, the three test passwords, both
  internal keys and the MDM registration key: **0 hits**.
- Saved HTML embeds the test user's email (`window.CurrentUser`), as in Q264 F-7. These are test identities created
  in this run.
- The built-in browser was signed out at the end.

Nothing committed, nothing pushed, nothing staged.
