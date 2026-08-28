# MOD-0149 / MOD-0150 — Account 360 UI Completion (Related Contacts & Related Accounts)

**Date:** 2026-07-20 · **Verdict:** **PASS** (upgraded from PARTIAL after the 2026-07-20 remediation-attempt-2 live
golden flow) — Account Details (MOD-0149 Customer 360) renders the MOD-0150 FU03 **Related Contacts** and FU04
**Related Accounts** read-only projections, Gateway-only, permission-gated, with empty/unauthorized/dependency states
and 7-language RESX parity. Frontend build 0 errors, compact verifier PASS, RESX parity 71/71 × 7. The authenticated
97c5 golden flow is now proven live end-to-end (real link + relationship rows render, inverse label "serves" on the
target, empty-state on an unrelated account, no direct 5061). No backend change, no new permission, no hardcoded
fallback. See the **remediation-attempt-2** section at the bottom (attempt-1's "reference sets not published" was a
false negative from an improper tenant session — corrected there).

> This is a **post-closeout enhancement** of MOD-0149 Details.cshtml consuming projections that MOD-0150-FU03/FU04
> already shipped. It is **not** a new backend capability. MOD-0150 closeout status is unchanged (still Closeout PASS /
> 100%).

---

## 1. Preflight

| Item | State |
|---|---|
| MOD-0149 status | Review-ready (95) — Customer 360 / Account Hierarchy Details.cshtml live |
| MOD-0150 status | Closeout PASS / 100% — FU03 AccountContactLink + FU04 AccountRelationship backend projections shipped |
| Related Contacts endpoint | `GET /api/crm/accounts/{accountId}/related-contacts` (alias of `/contacts`) — `crm.account-contact.read` — returns `AccountRelatedContactDto[]` |
| Related Accounts endpoint | `GET /api/crm/accounts/{accountId}/related-accounts` (alias of `/relationships`) — `crm.account-relationship.read` — returns `RelatedAccountDto[]` (inverse label pre-resolved) |
| Scope confirmation | Render the two ready projections in Account Details, read-only, Gateway-only. No backend logic, no new entity/permission/seed, no relationship graph, no consent, no Zone/Territory/SalesRep. |

## 2. Implementation Summary

- **Controller** (`AccountsController.Details`): after loading the `/overview` model, calls
  `PopulateRelatedProjectionsAsync(id, overview)`. Two new private loaders — `LoadRelatedContactsAsync` /
  `LoadRelatedAccountsAsync` — call the FU03/FU04 projection endpoints **through the Gateway (`_gatewayUrl`)** with
  Bearer + `X-Tenant-Id` propagation (existing `AddAuthHeaders()`), returning `null` on non-success/exception
  (controlled dependency) or an empty list when reachable-but-empty. Each section is gated by
  `PermissionClaims.HasPermission(User, …)` (defence-in-depth mirror of the CrmService `[HasPermission]`), so a caller
  without the permission never triggers the call.
- **ViewModel** (`AccountOverviewViewModel`): extended **frontend-only** with `RelatedContacts` /
  `RelatedAccounts` lists + `…Authorized` / `…Unavailable` flags, plus two new read models
  `AccountRelatedContactViewModel` / `AccountRelatedAccountViewModel` mirroring the backend DTOs. No field added to the
  Account master/entity; not part of the `/overview` payload.
- **Related Contacts section** (Details.cshtml): DisplayName (→ Contact Details link), ContactType badge, RoleCode,
  Primary badge, Phone, Email. States: not-authorized → `NotAuthorized`; endpoint down → `DependencyUnavailable`;
  200-empty → `NoRelatedContacts`.
- **Related Accounts section** (Details.cshtml): RelatedAccountName (→ related Account Details link),
  RelatedAccountCode, RelatedAccountType, RelationshipType (renders `EffectiveLabelCode` — direct or inverse from this
  account's perspective), DisplayDirection badge, Status, ValidFrom, ValidTo, Notes. Same three states +
  `NoRelatedAccounts`.
- **Permissions:** reuses catalog keys `crm.account-contact.read` (FU03) and `crm.account-relationship.read` (FU04).
  No new key invented; no `crm.account.360.related.read`.
- **Localization:** 19 new keys added to all 7 `AccountIndex.*.resx` (71 keys each, identical keysets).
- **JS behavior:** **none added** — server-side render is sufficient (mirrors the existing MOD-0150 Contact Details
  Linked-Accounts pattern, which is also server-rendered). No client fetch, no mock data, no loading spinner needed
  because the sections render with the page. Decision recorded here per §E.

## 3. Changed Files

| File | Change | Why |
|---|---|---|
| `frontend/Diten.Web/Models/CRM/AccountViewModels.cs` | +2 read models (`AccountRelatedContactViewModel`, `AccountRelatedAccountViewModel`); `AccountOverviewViewModel` +6 frontend-only fields | Carry the FU03/FU04 projection rows + auth/availability state to the view without polluting the Account master |
| `frontend/Diten.Web/Controllers/CRM/AccountsController.cs` | +2 permission consts; `Details` calls `PopulateRelatedProjectionsAsync`; +3 private methods (populate + 2 Gateway loaders) | Load the projections Gateway-only, permission-gated, fail-soft |
| `frontend/Diten.Web/Views/CRM/Accounts/Details.cshtml` | +2 read-only cards (Related Contacts, Related Accounts) in the main column | Render the projections with empty/unauthorized/dependency states |
| `frontend/Diten.Web/Resources/Views/CRM/Accounts/AccountIndex.{en,tr,fr,es,zh,ar,ru}.resx` | +19 keys each | Localize the two sections in 7 languages |

## 4. UI Proof

| Surface | Expected | Evidence | Result |
|---|---|---|---|
| Related Contacts card | Renders below Child Accounts; table DisplayName/Type/Role/Primary/Phone/Email | Details.cshtml card + compact verifier PASS | ✅ (code) |
| Related Contacts empty | "No related contacts yet." | `NoRelatedContacts` branch | ✅ (code) |
| Related Contacts unauthorized | not-authorized message, page still renders | `!RelatedContactsAuthorized` branch | ✅ (code) |
| Related Accounts card | Renders below Related Contacts; Name/Code/Type/Relationship/Direction/Status/Valid/Notes | Details.cshtml card | ✅ (code) |
| Related Accounts inverse label | Column shows `EffectiveLabelCode` (target sees inverse) | Binds backend-resolved field | ✅ (code) |
| Contact/Account detail links | `/CRM/Contacts/Details/{contactId}`, `/CRM/Accounts/Details/{relatedAccountId}` | anchors in cards | ✅ (code) |
| Live 97c5 browser render (real rows) | link/relationship rows visible, links navigate | **not executed this pass** | ⏳ deferred |

## 5. Projection Proof

| Projection | Endpoint | Evidence | Result |
|---|---|---|---|
| Related Contacts | `GET {gateway}/api/crm/accounts/{id}/related-contacts` | `LoadRelatedContactsAsync` binds `GatewayResponse<List<AccountRelatedContactViewModel>>` (mirrors `AccountRelatedContactDto`) | ✅ contract |
| Related Accounts | `GET {gateway}/api/crm/accounts/{id}/related-accounts` | `LoadRelatedAccountsAsync` binds `GatewayResponse<List<AccountRelatedAccountViewModel>>` (mirrors `RelatedAccountDto`) | ✅ contract |
| Live payload (200 with rows) | same | needs runtime 97c5 token + seeded links | ⏳ deferred (FU03/FU04 audits already proved these endpoints live) |

## 6. Permission Proof

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| Has `crm.account-contact.read` | Related Contacts loads | `RelatedContactsAuthorized=true` → loader called | ✅ (code) |
| Missing `crm.account-contact.read` | section shows NotAuthorized, no call | guard short-circuits before HTTP | ✅ (code) |
| Has `crm.account-relationship.read` | Related Accounts loads | `RelatedAccountsAuthorized=true` → loader called | ✅ (code) |
| Missing `crm.account-relationship.read` | section shows NotAuthorized, no call | guard short-circuits | ✅ (code) |
| Backend denies despite claim (403) | section shows DependencyUnavailable, page intact | non-success → `null` → unavailable | ✅ (code) |
| Live JWT permission matrix | authorized vs unauthorized in browser | **not executed this pass** | ⏳ deferred |

## 7. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `dotnet build frontend/Diten.Web/Diten.Web.csproj` | ✅ **0 Hata** | Built to isolated `bin-verify` (running dev-fleet Diten.Web PID locks the default `bin`; MSB3026/MSB3027 are file-lock, not compile errors) |
| compact verifier (`--area CRM --module Accounts --reference compact`) | ✅ **PASS** | DataTable v2 contract intact; no regression from the two new cards |
| RESX 7-lang parity | ✅ **71/71 identical** | en/tr/fr/es/zh/ar/ru keysets identical |
| CrmService / Gateway / Auth build | ⏭️ not run | no backend/model-contract/route change → not required per §I |
| Browser golden flow | ⏳ deferred | needs running fleet + 97c5 token |

## 8. Boundary / SoR Check

| Object/Capability | Owner | Touched? | Boundary Risk |
|---|---|---|---|
| AccountContactLink logic | MOD-0150 FU03 | No (consumed via projection endpoint) | none |
| AccountRelationship logic | MOD-0150 FU04 | No (consumed via projection endpoint) | none |
| Account master/entity | MOD-0149 | No Contact/Relationship field added (frontend VM only) | none |
| Reference values | MOD-0048 | No | none |
| Permissions catalog | MOD-0018 | Reused existing keys; none added | none |
| Coverage/Zone/Territory | MOD-0151 | No | none |
| Consent | MOD-0164 | No | none |
| Gateway routes | integration-agent | No route change (endpoints under existing wildcard) | none |

## 9. Out-of-Scope Guard

| Forbidden Item | Found? | Status |
|---|---|---|
| Account entity Contact/Relationship field | No | ✅ |
| AccountContactLink backend logic change | No | ✅ |
| AccountRelationship backend logic change | No | ✅ |
| Relationship graph visualization | No | ✅ |
| Consent capture | No | ✅ |
| Zone/MicroZone/Territory/SalesRep | No | ✅ |
| CRM local seed | No | ✅ |
| Hardcoded fallback / mock data | No | ✅ |
| Direct 5061 call in frontend | No (all via `_gatewayUrl`) | ✅ |
| New permission invented | No (reused `crm.account-contact.read` / `crm.account-relationship.read`) | ✅ |
| Offcanvas/quickview added | No (read-only cards) | ✅ |

## 10. Open Items

| Item | Severity | Owner | Blocks MOD-0151? | Notes |
|---|---|---|---|---|
| Authenticated browser golden flow (live 97c5 render of real rows) | Medium | frontend/QA | No | needs running fleet + 97c5 token; endpoints already proven live in FU03/FU04 |
| Link/Relationship management UI (create/edit/delete on Account Details) | Low | frontend | No | optional in this task; not built (read-only only). API ready |
| Manage-Contacts / Manage-Relationships links | Low | frontend | No | optional; not added |

## 11. Registry / Status Update

- **Previous:** MOD-0150 — Closeout PASS (Review-ready) / 100. Open note: *"Account Details Related Contacts/Accounts render … (→ Account 360 UI Completion)."* MOD-0149 — Review-ready / 95.
- **New:** MOD-0150 closeout **unchanged** (still 100); the "Account Details Related Contacts/Accounts render" post-closeout enhancement is marked **frontend delivered (PARTIAL — build/verifier PASS; authenticated browser smoke deferred)**. MOD-0149 gains a post-closeout enhancement note for the Details.cshtml render.
- **Reason:** Frontend-only enhancement consuming already-shipped FU03/FU04 projections; no backend/permission/registry-ID change. No new MOD ID; `module-id-registry.md` untouched.

## 12. Final Verdict

**PARTIAL:** Related Contacts and Related Accounts sections exist, render server-side through the Gateway,
permission-gated with empty/unauthorized/dependency states, 7-language RESX parity, frontend build 0 errors and compact
verifier PASS — but the authenticated browser golden flow proving live 97c5 rows was not executed in this pass. Promote
to PASS after the browser smoke against a running fleet.

## 13. Next Recommended Prompt

Remediation to close PARTIAL → PASS: **run the Account 360 browser golden flow** with a 97c5 CRM Admin session against
a running fleet — open an Account with ≥1 linked contact and ≥1 relationship, confirm Related Contacts (DisplayName /
RoleCode / Primary + Contact link) and Related Accounts (RelatedAccountName / relationship label / inverse display +
Account link) render, confirm empty/unauthorized states, no console errors, no direct 5061. Then **MOD-0151 Territory
Management Pack Prep**.

---

# Browser Golden Flow Remediation — 2026-07-20 (attempt 1)

**Verdict of this pass: still PARTIAL** — a live authenticated session was obtained and the auth / routing / permission
chain was proven **live** (upgraded from code-only), but the **real-row render** of Related Contacts / Related Accounts
could not be produced because the current runtime has **no published MOD-0048 reference sets**, so no
Account/Contact/Link/Relationship can be created through the API, and creating them any other way is out of this task's
scope. This is a runtime **data prerequisite** blocker, not a code defect.

## Runtime preflight

| Service | Check | Result |
|---|---|---|
| Ports 5000/5001/5056/5057/5061 | LISTENING | ✅ all up |
| Diten.Web `/account/login` | GET | ✅ 200 |
| Gateway routing (unauth projection) | `GET /api/crm/accounts/{id}/related-contacts` no token | ✅ 401 (routed+guarded, not 404) |

## Authenticated session (credentials masked)

- Logged in via `POST {gateway}/api/tenant-auth/login` as `bestepullukcu@gmail.com` (password **masked**, passed on
  stdin, never persisted). Token held in an **ephemeral session-scratch file only, deleted at end**; not written to any
  tracked/persistent file; masked in this report.
- **Finding — tenant resolution:** this credential resolves to tenant **`00000000-0000-0000-0000-000000000001` /
  role SuperAdmin** (not 97c5). The JWT carries **312 permissions** including `crm.account.read`,
  `crm.account-contact.read`, `crm.account-relationship.read`.
- **Finding — CrmService tenant scoping:** `TenantResolutionMiddleware` derives the tenant **solely from the
  `X-Tenant-Id` header** (no JWT cross-check), so the API can be pointed at tenant 97c5 with this JWT. The **web app**,
  however, locks the user to their home tenant (`0…01`) — there is no web-UI tenant override — and `0…01` is empty.

## Test data preflight — BLOCKED

| Data | Evidence | Result |
|---|---|---|
| Accounts in 97c5 | `GET /api/crm/accounts` → `total:0` | ✗ none |
| Accounts in `0…01` | `GET /api/crm/accounts` → `total:0` | ✗ none |
| Contacts (both tenants) | `GET /api/crm/contacts` → `total:0` | ✗ none |
| Published `account-type`/`account-status`/`contact-*`/`account-relationship-*` (97c5 + `0…01`) | `published-values?scope_key=…` → `count:0` for all | ✗ none published |
| **Create Account attempt** | `POST /api/crm/accounts {hospital}` → **400** `Required reference set 'account-type' is not published yet (MOD-0048 authoring pending). / …'account-status'…` | ✗ blocked |

Creating the required reference values needs the MOD-0048 governance flow (steward author → submit → approve →
publish) which is **SoD-gated (two approvers)** and is **out of scope** here (forbidden: new reference set, CRM local
seed, manual Mongo insert, hardcoded fallback). Therefore no smoke data was created.

## Live proofs achieved this pass

| Check | Expected | Observed | Status |
|---|---|---|---|
| related-contacts, no token | 401 | 401 | ✅ |
| related-accounts, no token | 401 | 401 | ✅ |
| related-contacts, valid JWT (authorized), missing account | reach handler (not 403) | **404 "Account not found"** | ✅ guard passed live |
| related-accounts, valid JWT (authorized), missing account | reach handler (not 403) | **404 "Account not found"** | ✅ guard passed live |
| Web cookie login | 200 + `access_token` cookie | 200, chunked `access_token`C1–C5 set | ✅ |
| `GET /CRM/Accounts` (authenticated) | 200 + DataTable v2 grid | 200, 63 KB, `data-dt-standard="v2"` present | ✅ deployed surface live |
| `GET /CRM/Accounts/Details/{fake}` (authenticated) | clean redirect, no crash | 200 after 1 redirect (→ Index; missing account) | ✅ modified Details action runs, non-breaking |

The `404 not 403` on the projection endpoints with an authorized JWT proves the permission guard **passes for an
authorized user** at runtime; the `/CRM/Accounts/Details/{fake}` clean redirect proves the **modified Details controller
action is deployed and non-breaking**.

## Not achievable this pass (still open)

| Item | Reason |
|---|---|
| Real-row render of Related Contacts / Related Accounts | No Account/Contact/Link/Relationship exists and none can be created (reference sets unpublished; creation out of scope) |
| Inverse display (served-by ↔ serves) live in browser | Needs a real relationship pair; blocked as above |
| Empty-state render in browser (Details of a real account) | Details redirects when the account does not exist, so even the empty state needs ≥1 real account |
| Limited-user (unauthorized) live permission render | Only a SuperAdmin credential was available; code-proof stands (guard short-circuit) — Low open item |

## Search guards (re-confirmed this pass)

| Guard | Result |
|---|---|
| Direct 5061 in frontend | None (all traffic via Gateway `_gatewayUrl` / `:5000`) |
| Hardcoded mock/fake data | None (no data fabricated; create was attempted through the API and correctly 400'd) |
| New permission invented | None (reused `crm.account-contact.read` / `crm.account-relationship.read`) |
| Account entity Contact/Relationship field | None |
| AccountContactLink / AccountRelationship backend logic change | None (backend untouched) |
| Relationship graph / Consent capture / Zone-Territory-SalesRep | None |

## Remediation to reach PASS

The **only** remaining blocker is runtime data availability. To close:
1. Publish the MOD-0048 reference sets in the target CRM tenant (97c5): `account-type`, `account-status`,
   `contact-type`, `contact-status`, `contact-role`, `account-relationship-type` (+direction/inverse metadata),
   `account-relationship-status` — via the governance flow with a **second approver** (SoD).
2. Then create minimal smoke data via the Gateway API (1 hospital + 1 pharmacy account, 1 contact, 1 primary
   decision-maker link, 1 `served-by` relationship) and re-run the golden flow — Related Contacts row, Related Accounts
   row on the source (direct `served-by`) and target (inverse `serves`), empty-state on an unrelated account.

Because that prerequisite is a governance/ops action outside this frontend task's scope, the enhancement remains
**PARTIAL** with the render proof deferred to a run against a reference-data-provisioned tenant.

---

# Browser Golden Flow Remediation — 2026-07-20 (attempt 2) — **PASS**

**Correction of attempt 1:** attempt-1's conclusion that "no MOD-0048 reference sets are published in 97c5" was a
**false negative**. Root cause: the login was sent to `POST /api/tenant-auth/login` **without an `X-Tenant-Id`
header**, so the AuthService resolved the user (`bestepullukcu@gmail.com`) to the **default tenant `0…01` / SuperAdmin**;
the reference-data lookups and the Account create then validated against `0…01` (which has nothing published), not 97c5.
The web `AuthGateway.LoginTenantAsync` always sends `X-Tenant-Id`, so the browser session was never affected — only my
API probe was mis-scoped.

**Corrected finding — 97c5 reference sets ARE published** (login re-issued with `X-Tenant-Id: 97c5` → JWT `tenant_id`
= 97c5, role **Admin**, 117 perms incl. all four CRM keys):

| Set | Published values (97c5) |
|---|---|
| account-type | 9 (organization, hospital, pharmacy, clinic, distributor, …) |
| account-status | 5 (draft, active, inactive, suspended, archived) |
| contact-type | 9 · contact-status 4 · contact-role 7 |
| account-relationship-type | 6 (associated-with, preferred-pharmacy, refers-to, served-by, same-network, nearby) |
| account-relationship-status | 4 (pending, active, inactive, ended) |

## Test data (created via Gateway API — clearly marked test data)

| Data | Id (test data) | How |
|---|---|---|
| Account A (existing) | `ee678de2…` ACC-2026-000003 "TEST" (pharmacy) | pre-existing |
| Account B (existing) | `88c1b88a…` ACC-2026-000004 "Özel Keşan Hastanesi" (hospital) | pre-existing |
| Contact C1 | `4d7790f8…` Dr. Ayse Yilmaz (doctor) | `POST /api/crm/contacts` |
| Link C1→B | `bae92985…` decision-maker, primary | `POST /api/crm/accounts/{B}/contacts` → 201 |
| Relationship B served-by A | `3ed07332…` served-by, active | `POST /api/crm/accounts/{B}/relationships` → 201 |

No local seed / no Mongo insert — all created through the Gateway with the 97c5 Admin JWT. (Left in place so the owner
can view them in the browser; safe to delete the link, relationship and contact to restore a clean tenant.)

## Projection proof (live, real data)

| Projection | Endpoint | Observed | Result |
|---|---|---|---|
| B Related Contacts | `/accounts/{B}/related-contacts` | Dr. Ayse Yilmaz · doctor · decision-maker · **primary=true** · phone · email | ✅ |
| B Related Accounts | `/accounts/{B}/related-accounts` | TEST · ACC-2026-000003 · pharmacy · **served-by** · direction **direct** | ✅ |
| A Related Accounts (inverse) | `/accounts/{A}/related-accounts` | Özel Keşan Hastanesi · ACC-2026-000004 · direction **inverse** · effective **serves** | ✅ |
| A Related Contacts (empty) | `/accounts/{A}/related-contacts` | count 0 | ✅ |

## Render proof (authenticated web, tenant 97c5, real rendered HTML)

| Surface | Expected | Observed | Result |
|---|---|---|---|
| Web cookie login (tenant 97c5) | 200, JWT tenant 97c5 / Admin | 200, chunked `access_token`, tenant 97c5 | ✅ |
| `GET /CRM/Accounts/Details/{B}` | 200 render | 200, 77 KB | ✅ |
| Related Contacts heading + row | localized heading, C1 row | "Related Contacts" (localized, **not** raw key) + "Dr. Ayse Yilmaz" / "decision-maker" / email / phone "555 111 2233" / **Primary** badge | ✅ |
| Contact Details link | `/CRM/Contacts/Details/{C1}` | present (`/CRM/Contacts/Details/4d7790f8…`) | ✅ |
| Related Accounts heading + row | localized, A row | "Related Accounts" + "Relationship" + "Direction" headers + "ACC-2026-000003" / "served-by" / "direct" | ✅ |
| `GET /CRM/Accounts/Details/{A}` inverse + empty | inverse "serves"; "No related contacts yet." | "serves" + "inverse" rendered; **"No related contacts yet."** empty-state; related account link `/CRM/Accounts/Details/{B}` + ACC-2026-000004 | ✅ |
| Raw RESX key leak | none | none (NotAuthorized/DependencyUnavailable/NoRelated*/ViewContact/ViewAccount absent as raw text) | ✅ |
| Direct 5061 in rendered HTML | none | `grep 5061` → 0 in both Details pages | ✅ |

## Permission proof (live)

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| projection no token | 401 | 401 | ✅ |
| projection authorized JWT, missing account | reach handler (not 403) | 404 "Account not found" | ✅ (guard passes) |
| authorized JWT, real account | 200 + rows | 200 + rows (see projection proof) | ✅ |
| limited-user (unauthorized) live render | section hidden/unauthorized | only Admin credential available → code-proof stands | ⏳ Low open item |

## Navigation check

The tenant-shell CRM menu renders correctly for the 97c5 Admin: the authenticated page HTML contains the **Commercial
Suite** header + `href="/CRM/Accounts"` and `href="/CRM/Contacts"` (gated by `crm.account.read` / `crm.contact.read`,
both present). There is **no separate "Account Relations" nav entry by design** — Related Contacts and Related Accounts
are read-only **sections inside Account Details** (reached via Commercial Suite → Accounts → open an account), not a
standalone page. A dedicated top-level relationships/management screen would be a new surface (new controller/view)
outside this remediation's scope; it belongs in a future management-UI module pack.

## Verdict of attempt 2: **PASS**

Account 360 UI Completion browser golden flow proven live in tenant 97c5: Related Contacts and Related Accounts render
real data, inverse display correct (target sees "serves"), empty-state friendly, headings localized (RESX loaded), no
direct 5061, permission guard live. The only remaining item is the limited-user unauthorized live render (Low; code-proof
stands). Enhancement upgraded **PARTIAL → PASS**.
