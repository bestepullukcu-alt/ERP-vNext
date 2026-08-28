# MOD-0149 / MOD-0150 — Account 360 Relationship Management UI + Historical Link Lifecycle

**Date:** 2026-07-21 · **Verdict:** **PASS** — Account Details (MOD-0149 Customer 360) now lets a permitted user
**Add / Edit / End** Related Contacts (AccountContactLink) and Related Accounts (AccountRelationship) through the
existing MOD-0150 APIs, Gateway-only, with the historical lifecycle preserved. Ending a link/relationship never deletes
it — it transitions `Status → ended` + sets `ValidTo`, and the record stays visible in the projection for history.
Backend historical-lifecycle remediation applied (repo active-checks, index migration, contact-link Status, validity →
controlled 400). CrmService tests **82/82**, all builds 0 errors, RESX parity **95/95 × 7**, compact verifier PASS, live
97c5 golden flow end-to-end incl. history preservation, failure paths, and MVC round-trips.

> **Decision (historical business facts):** *Contact↔Account and Account↔Account relationships are historical business
> facts. They must be ended with validity/status transitions, not destroyed, so downstream sales, visits, orders,
> forecasts, and route history preserve their original account/contact/relationship context.*

---

## 1. Preflight

| Item | State |
|---|---|
| MOD-0149 status | Review-ready — Customer 360 Details + read-only Related Contacts/Accounts live |
| MOD-0150 status | Closeout PASS / 100% — AccountContactLink + AccountRelationship backend + projections shipped |
| Existing backend API | `POST/PUT/GET/DELETE /api/crm/accounts/{id}/contacts[/{linkId}]`, `.../relationships[/{relId}]`, `/related-contacts`, `/related-accounts`, `GET /api/crm/contacts/search`, `GET /api/crm/accounts?search=` — all present under existing Gateway wildcards |
| Historical lifecycle scope | Add UI over existing APIs; preserve history on change; minimal backend remediation allowed only for lifecycle correctness. No new module/permission/reference-set/seed. |

## 2. Historical Lifecycle Decision

| Rule | Decision | Reason |
|---|---|---|
| UI "Delete" business action | **Removed** — UI never calls DELETE; only **End Link / End Relationship** | Deleting destroys history; downstream context must survive |
| End behavior | PUT with `Status=ended` + `ValidTo=EndDate` (record kept, `IsDeleted=false`) | Preserve the row for reporting; drop only from the active set |
| DELETE endpoint | Left as technical soft-delete; **not used by the UI** | Avoids scope creep; End is the business path |
| Active uniqueness (link/primary/pair) | Excludes ended/inactive records (app + DB partial index) | An ended record must not block a new active one (e.g., doctor returns) |
| Change of affiliation (A→B, X→Y) | Old record ended; new record inserted separately | Different natural key; both kept — the doctor/pharmacy example |
| ValidFrom > ValidTo / EndDate before ValidFrom | Controlled **400** | Invalid validity window |
| Transaction context (future) | link-id / relationship-id + snapshot fields; never re-bind on change | Downstream MOD-0153/0154/0155/0168 preserve point-in-time context (see §D note) |

## 3. Implementation Summary

- **Related Contacts management:** Add / Edit / End full-page child actions under `/CRM/Accounts/{accountId}/Contacts/...`.
  Add = contact select + `contact-role` dropdown + IsPrimary + ValidFrom/ValidTo + Notes. Edit = role/primary/validity/
  notes (no Status — End owns the lifecycle). End = EndDate + reason, confirms with a "will not be deleted" notice.
- **Related Accounts management:** Add / Edit / End under `/CRM/Accounts/{accountId}/Relationships/...`. Add = target
  account select + `account-relationship-type` + `account-relationship-status` dropdowns + validity + notes. Edit = type/
  status/validity/notes. End = EndDate + reason. Edit/End shown only for **source-owned** rows (inverse rows say
  "Managed from the source account" — the backend guards source-only mutation).
- **End behavior:** the End POST loads the current record, then PUTs it back with `Status=ended` + `ValidTo=EndDate`,
  preserving RoleCode/IsPrimary/RelationshipType. No DELETE, no hard delete.
- **Controller/ViewModel:** `AccountsController` +12 actions (Add/Edit/End GET+POST × contacts/relationships) + loaders
  (contact options via `/contacts`, target-account options via `/accounts`, link/relationship by id). 4 frontend VMs
  (`AccountContactLinkEditViewModel`, `AccountContactLinkEndViewModel`, `AccountRelationshipEditViewModel`,
  `AccountRelationshipEndViewModel`) + 4 payloads mirroring the CrmService request DTOs. No Account/Contact-entity change.
- **Reference dropdowns:** `contact-role`, `account-relationship-type`, `account-relationship-status` sourced live from
  MOD-0048 published-values (no hardcoded list, no local fallback). Direction/inverse/selfAllowed stay backend-derived;
  the UI never sends a direction.
- **Permissions:** read `crm.account-contact.read` / `crm.account-relationship.read`; manage `crm.account-contact.manage`
  / `crm.account-relationship.manage`. No new permission. Add/Edit/End are per-action MVC-guarded + view-gated.
- **Localization:** 24 new keys × 7 languages (95 keys total, identical keysets).
- **Backend remediation (historical lifecycle correctness):**
  1. `RelationshipLifecycle` domain helper (`ClosedStatuses = {ended, inactive}`).
  2. Repo active-checks (`ExistsActiveAsync`/`ExistsPrimaryAsync`/`ExistsActivePairAsync`) exclude closed statuses so an
     ended record never blocks a new active one. List projections **keep** closed rows for history.
  3. **Mongo partial unique index migration** — `ux_account_contact_links_active_natural`, `_primary`,
     `ux_account_relationships_active_directional` changed from `IsDeleted=false` to `IsDeleted=false AND Status="active"`,
     with idempotent drop-and-recreate on startup (`DropIndexIfExists`). Without this the DB index still blocked same-key
     re-activation (E11000 → 500).
  4. Contact-link `Update` now accepts `Status` (relationship Update already did) so End sets `Status=ended` via PUT.
  5. `ValidationBehavior` returns a controlled **400** envelope on FluentValidation failure (e.g. ValidFrom>ValidTo)
     instead of throwing → 500; handler-level `ValidateValidity` added as a unit-tested backstop.
  6. +5 unit tests (end-then-relink allowed, ended excluded from primary, validity 400) → **82/82**.

## 4. Changed Files

| File | Change | Why |
|---|---|---|
| `services/.../Domain/Entities/RelationshipLifecycle.cs` | **New** — closed-status policy helper | Single source of truth for "active vs historically closed" |
| `services/.../Persistence/Repositories/AccountContactLinkRepository.cs` | `OpenTenant` excludes closed statuses in active/primary checks | Ended link must not block a new active one |
| `services/.../Persistence/Repositories/AccountRelationshipRepository.cs` | `ExistsActivePairAsync` excludes closed statuses | Ended relationship must not block a new active pair |
| `services/.../Persistence/DependencyInjection.cs` | 3 partial unique indexes → `+ Status="active"`; idempotent drop-and-recreate | Align DB uniqueness with the historical lifecycle (fixes E11000 on re-link) |
| `services/.../Application/Behaviors/ValidationBehavior.cs` | Validation failure → controlled 400 `Response<T>` | ValidFrom>ValidTo returns friendly 400, not 500 |
| `services/.../AccountContact/{Commands,Handlers}` + `Api/Models/CRM/AccountContactRequests.cs` + `AccountContactController.cs` | Update accepts `Status`; Link/Update add validity check | End Link sets Status=ended; controlled validity |
| `services/.../AccountRelationship/Handlers/AccountRelationshipCommandHandlers.cs` | Create/Update add validity check | Controlled validity |
| `services/.../tests/.../AccountContactLinkTests.cs`, `AccountRelationshipTests.cs` | +5 tests; fakes mirror closed-status exclusion | Prove historical lifecycle |
| `frontend/.../Models/CRM/AccountRelationshipManagementViewModels.cs` | **New** — 4 edit/end VMs + 4 payloads + detail read models | Frontend read/write models (no master pollution) |
| `frontend/.../Controllers/CRM/AccountsController.cs` | +12 actions, loaders, manage perms, manage flags | Add/Edit/End over existing APIs, Gateway-only |
| `frontend/.../Views/CRM/Accounts/{ContactLinkForm,ContactLinkEnd,RelationshipForm,RelationshipEnd}.cshtml` | **New** — 4 full-page child views | Compact-standard forms + historical End notice |
| `frontend/.../Views/CRM/Accounts/Details.cshtml` | Add buttons + per-row Edit/End + Status column + notices | Inline section management, ended rows visible |
| `frontend/.../Models/CRM/AccountViewModels.cs` | `RelatedContactsCanManage`/`RelatedAccountsCanManage` + `SourceAccountId` | Gate actions; source-only mutation |
| `frontend/.../Resources/Views/CRM/Accounts/AccountIndex.{en,tr,fr,es,zh,ar,ru}.resx` | +24 keys each (95 total) | 7-language management + historical-notice strings |

## 5. UI Flow Proof (live, 97c5 Admin, Gateway-only)

| Flow | Expected | Observed | Result |
|---|---|---|---|
| Details renders management UI | Add buttons + per-row Edit/End + notices | Add Contact Link / Add Related Account + Edit/End links (×2) + "historical: ending…" notices + ended Status badge | ✅ |
| Contacts/Add form GET | contact + role dropdowns, IsPrimary, antiforgery | Select Contact / RoleCode / ContactId / IsPrimary / Save Link / __RequestVerificationToken | ✅ |
| Contacts/Add POST | create → 302 Details | HTTP **302** → `/CRM/Accounts/Details/{B}`; medical active link created | ✅ |
| End Link form GET | "will not be deleted" notice + EndDate + confirm | rendered ("will not be deleted from history", End Date, Confirm End Link) | ✅ |
| End Link POST | Status=ended, redirect | HTTP **302**; link `status=ended`, `validTo=2026-08-01`, **record still exists** | ✅ |
| Relationships/Add form GET | target + type + status dropdowns | Select Account / RelationshipType / Status / TargetAccountId / Save Relationship | ✅ |
| Inverse label on target | target sees inverse | A related-accounts shows "serves" (inverse) for the B served-by A relationship | ✅ |
| Direct 5061 in rendered HTML | none | 0 | ✅ |

## 6. Historical Preservation Proof (live)

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| End contact link | Status=ended, ValidTo set, not deleted | 200; `status=ended`, `validTo=2026-06-30`; row present | ✅ |
| Ended link still in projection | history visible | B related-contacts shows Dr. Ayse decision-maker **status=ended** alongside a new **active** one | ✅ |
| Re-link same (Account,Contact,Role) after end | allowed (201) | **201** new link created; both rows kept | ✅ |
| End relationship | Status=ended, not deleted | 200; B related-accounts shows A served-by **status=ended** | ✅ |
| Recreate same pair/type after end | allowed (201) | **201** | ✅ |
| Persistence across fleet restart | history survives | post-restart: Dr. Ayse decision-maker (ended+active) + medical (ended); A↔B served-by (ended+active) all present | ✅ |
| Downstream reassignment | none | End/re-link never touched other records; no transaction re-binding | ✅ (no downstream module yet) |

## 7. Failure Path Proof (live, friendly)

| Failure | Expected | Observed | Status |
|---|---|---|---|
| Contact: invalid RoleCode | 400 | 400 | ✅ |
| Contact: duplicate active link | 409 | 409 | ✅ |
| Contact: second active primary same Account+Role | 409 | 409 | ✅ |
| Contact: ValidFrom>ValidTo (EndDate before ValidFrom) | controlled 400 | **400** (was 500 before ValidationBehavior fix) | ✅ |
| Relationship: invalid RelationshipType | 400 | 400 | ✅ |
| Relationship: invalid Status | 400 | 400 | ✅ |
| Relationship: self-link (selfAllowed=false) | 400 | 400 | ✅ |
| Relationship: duplicate active | 409 | 409 | ✅ |
| Relationship: bidirectional reverse duplicate | 409 | 409 (A↔B nearby) | ✅ |
| Relationship: ValidFrom>ValidTo | controlled 400 | **400** | ✅ |

> Note: overlap is enforced as "one active (non-closed) record per natural key" (point-in-time). Full date-range
> interval-overlap detection is a documented follow-up (Low).

## 8. Permission Proof

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| Manage perm present (97c5 Admin) | Add/Edit/End visible + usable | buttons render; POST/End succeed (302) | ✅ live |
| Missing `crm.account-contact.manage` | contact Add/Edit/End hidden; endpoint 403 | view gates on `RelatedContactsCanManage`; actions call `RequirePage(...manage)` → 403 | ✅ code-proof |
| Missing `crm.account-relationship.manage` | relationship Add/Edit/End hidden; endpoint 403 | gated on `RelatedAccountsCanManage`; `RequirePage(...manage)` → 403 | ✅ code-proof |
| Read-only user (read but not manage) | sees sections, no actions | sections render; buttons hidden | ✅ code-proof |
| Live limited-user render | hidden in browser | only a manage-capable Admin credential available | ⏳ Low open item |

## 9. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `dotnet build frontend/Diten.Web` | ✅ **0 Hata** | clean (fleet stopped) |
| `dotnet build CrmService.Api` | ✅ **0 Hata** | clean |
| `dotnet test CrmService.Application.Tests` | ✅ **82/82** | +5 historical-lifecycle tests |
| RESX parity | ✅ **95/95 × 7** | en/tr/fr/es/zh/ar/ru identical |
| compact verifier (CRM/Accounts) | ✅ PASS | DataTable contract intact |
| Browser/API golden flow (97c5) | ✅ end-to-end | §5–§7 |

## 10. Boundary / SoR Check

| Object/Capability | Owner | Touched? | Boundary Risk |
|---|---|---|---|
| AccountContactLink lifecycle | MOD-0150 FU03 | Yes — historical-lifecycle remediation (Status/active-check/index/validity) | none (no new aggregate) |
| AccountRelationship lifecycle | MOD-0150 FU04 | Yes — same remediation | none |
| Account master/entity | MOD-0149 | No field added | none |
| Contact master | MOD-0150 | No Account array added | none |
| Reference values | MOD-0048 | consumed; no local seed | none |
| Permissions | MOD-0018 | reused 4 keys; none added | none |
| Zone/Territory/SalesRep | MOD-0151 | No | none |
| Consent | MOD-0164 | No | none |
| Gateway routes | integration-agent | No new route (existing wildcards) | none |

## 11. Out-of-Scope Guard

| Forbidden Item | Found? | Status |
|---|---|---|
| New permission | No (4 existing keys) | ✅ |
| Direct 5061 in frontend | No (only a "never called directly" comment) | ✅ |
| Hardcoded role/type/status fallback | No (all MOD-0048 live) | ✅ |
| CRM local seed | No (test data created via Gateway API, marked as test data) | ✅ |
| Relationship graph | No | ✅ |
| Consent capture | No | ✅ |
| Zone/MicroZone/Territory/SalesRep | No | ✅ |
| Account entity Contact/Relationship field | No | ✅ |
| Contact entity Account array | No | ✅ |
| Delete action business hard-delete | No — repos use `ReplaceOneAsync` only; UI uses End (Status=ended), never DELETE | ✅ |

## 12. Open Items

| Item | Severity | Owner | Blocks MOD-0151? | Notes |
|---|---|---|---|---|
| Live limited-user (no-manage) browser render proof | Low | QA | No | code-proof stands; needs a second 97c5 user without manage |
| Full date-range interval-overlap validation | Low | backend | No | current rule = one active per natural key (point-in-time) |
| Contact/account picker uses list (≤200), not typeahead search | Low | frontend | No | `/contacts/search` exists; typeahead is a UX follow-up |
| Test data left in 97c5 (marked) | Info | — | No | Dr. Ayse ended+active links + A↔B served-by ended+active = the historical example; safe to delete |

## 13. Registry / Status Update

- **Previous:** MOD-0150 Closeout PASS / 100 (post-closeout enhancement: Account 360 relationship management UI open).
  MOD-0149 Review-ready.
- **New:** MOD-0150 closeout **unchanged** (100). Post-closeout enhancement **Account 360 Relationship Management UI +
  Historical Link Lifecycle = PASS**; historical-lifecycle backend remediation recorded (repo active-checks + partial
  index migration + contact-link Status + validity→400). MOD-0149 gains the management-UI note.
- **Reason:** UI over existing APIs + minimal lifecycle-correctness remediation. No new MOD ID; `module-id-registry.md`
  untouched. No new permission/reference-set/seed.

## 14. Final Verdict

**PASS:** Account Details supports Related Contacts and Related Accounts management (Add/Edit/End), and the historical
link lifecycle is preserved — ending transitions Status→ended + ValidTo without deleting, ended records stay in the
projection, and same-key re-activation is allowed (app-check + migrated partial index). Failure paths return friendly
409/400 (validity now controlled 400). CrmService 82/82, builds 0 errors, RESX 95×7, verifier PASS, live 97c5 golden
flow end-to-end. Boundary clean; no hard delete, no new permission, no direct 5061, no hardcoded fallback.

## 15. Next Recommended Prompt

**MOD-0151 Territory Management Pack Prep.** (Optional follow-ups: live limited-user permission render; date-range
overlap validation; contact/account typeahead search.)

---

### Downstream historical-context architecture note (§D)

`AccountContactLinkId` and `AccountRelationshipId` are stable historical anchors. Because links/relationships are ended
(never destroyed), future transactional modules — **MOD-0153 Opportunity, MOD-0154 Forecasting, MOD-0155 Visit
Planning, MOD-0168 Order Capture** — must capture point-in-time context (snapshot or link-id reference), e.g.
`AccountId`, `ContactId`, `AccountContactLinkId?`, `AccountRelationshipId?`, `RelationshipTypeAtTime`,
`AccountNameAtTime`, `ContactNameAtTime`, and (post-MOD-0151) `TerritoryIdAtTime` / `SalesRepIdAtTime`. Changing a
relationship must never re-bind historical transactions. This task records the rule only; it implements no downstream
module.
