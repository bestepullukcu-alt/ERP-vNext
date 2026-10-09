# BACKEND REQUIREMENTS — CRM account list: active-contact information and field-user filter options

Audience: the ERP-vNext backend developer. **Read-only analysis**: no file, branch, worktree or commit was created
or changed in ERP-vNext.

- Code examined: `origin/main` = `b994c813a` (2026-10-06, Merge #135), read only.
- Paths below are relative to `services/Diten.CrmService/src/` unless stated otherwise.
- Requested by: mobile product owner, from a field test of the iOS target picker ("İş yerinde bir doktor seçin").

---

## 1. Background — what the user sees

The mobile target picker is a two-step flow:

1. The user picks a **workplace** from a paged list.
2. The app then shows the workplace's **active contacts** (doctors).

Many workplaces have **no active contact**. The user only finds this out after opening each one ("Bu iş yerinin
aktif kişisi yok."). In a tenant with many pharmacies and workplaces, this means opening workplaces one by one to find
a doctor.

The product request is:

- **(a)** Split the list: workplaces **with** active contacts on top, workplaces **without** any in a separate
  "no contact" section below.
- **(b)** Let the user filter the list: only workplaces with contacts, by workplace type, by territory, and sorting.

## 2. Verified current behaviour

| # | Finding | Evidence |
|---|---|---|
| F1 | `GET /api/crm/accounts` (`crm.account.read`) accepts `search, page, pageSize, sortBy, sortDir, status, accountType, territoryNodeId, countryScope`. It has **no contact-related parameter**. | `Diten.CrmService.Api/Controllers/CRM/AccountController.cs:23-39` |
| F2 | The list item `AccountListItemDto` has **no contact or link count** and no `hasContacts` field. A repo-wide search for `ContactCount`, `ActiveContactCount`, `HasContacts` and `LinkCount` finds nothing relevant. | `Diten.CrmService.Application/Features/Account/AccountModels.cs:7-24` |
| F3 | Contacts are available **per account only**: `GET /api/crm/accounts/{id}/contacts` (`crm.account-contact.read`). It takes no parameters and returns links of **every** status (active and closed), so history stays visible. | `Diten.CrmService.Api/Controllers/CRM/AccountContactController.cs:28-31`; `Diten.CrmService.Persistence/Repositories/AccountContactLinkRepository.cs:56-60` |
| F4 | There is **no bulk JSON read of account–contact links**. The only bulk source is `GET /api/crm/accounts/contact-links/export`, which returns a CSV with no `Status` column and publishes an audit event on every call. It is unsuitable for a picker. | `Diten.CrmService.Api/Controllers/CRM/ImportExportController.cs:164-167`; `…/ImportExport/Handlers/AccountContactImportExportHandlers.cs:193-197` |
| F5 | The backend's own definition of an active link: not deleted, and status not `ended` / `inactive` (trimmed, case-insensitive). `ValidFrom` / `ValidTo` are **not** checked anywhere. | `Diten.CrmService.Domain/Entities/RelationshipLifecycle.cs:20-25` |
| F6 | Territory options need `crm.territory.node.read` (`GET /api/crm/territory-models/{id}/nodes`, `…/nodes/by-ids`). The seed grants it only to the tenant-97c5 **Admin** role. Field users hold `crm.territory.read`, which these routes do not accept. | `Diten.CrmService.Api/Controllers/CRM/TerritoryModelsController.cs:89-99`; `Diten.CrmService.Application/Features/Territory/TerritoryPermissions.cs:11-14`; `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs:1316-1340` |
| F7 | Account-type **labels** cannot be read by tenant users. This is already reported as MOD-0048 (P1–P4, R1). | `BACKEND-MOD-0048-CRM-REFERENCE-SETS-REQUIREMENTS.md` (same folder) |

## 3. Why mobile cannot do this properly today

- **The split needs one request per workplace.** Without a count or filter on the list, mobile has to call
  `/accounts/{id}/contacts` for every row: 25 extra requests per page, and N+1 load on the server as the user scrolls.
- **The split is only correct for the pages already loaded.** A workplace with contacts on page 40 cannot appear in
  the "with contacts" section until the user scrolls through pages 1–39. "Only workplaces with contacts" cannot be
  answered for the whole tenant.
- **Totals are wrong.** The paged `total` counts all workplaces, so "N workplaces with contacts" cannot be shown.
- **Territory filter options are not readable by field users** (F6), and **type labels are not readable** (F7).
  Mobile can only offer the values it has already seen in loaded rows.

**Interim mobile behaviour (until this is delivered).** Mobile will probe `/accounts/{id}/contacts` for the rows on
screen, with limited concurrency, and split only the loaded rows. This is marked as a stopgap. It is removed when R1
and R2 below are available.

## 4. Required backend work

### R1 — active-contact count on the list item (required)

Add to `AccountListItemDto`:

```csharp
int ActiveContactCount   // links with !IsDeleted && !RelationshipLifecycle.IsClosed(Status), contact not soft-deleted
```

- Count with the **same rule** as F5 and as the `/contacts` projection (links whose contact was soft-deleted are
  skipped there, so skip them here too). The count must equal what `/accounts/{id}/contacts` would show as active.
- Compute it for the **returned page only**, in one aggregation over `AccountContactLink` with
  `AccountId IN (page ids)` and `TenantId = <server tenant>`. Never one query per account.
- Suggested index: `{ TenantId: 1, AccountId: 1, IsDeleted: 1 }` on the links collection, if not already present.

### R2 — `hasActiveContacts` filter on the list (required)

Add a query parameter to `GET /api/crm/accounts`:

```
hasActiveContacts = true | false     (absent = no filter, today's behaviour)
```

- `true`: only accounts with `ActiveContactCount ≥ 1`. `false`: only accounts with `ActiveContactCount = 0`.
- It combines with every existing filter (`search`, `status`, `accountType`, `territoryNodeId`, `countryScope`) as
  **AND**. `Total` reflects the filter; `UnfilteredTotal` stays tenant-wide.
- Paging and sorting work exactly as today.
- With R2, mobile builds the split with **two paged queries**: first `hasActiveContacts=true` (the top section),
  then `hasActiveContacts=false` (the "no contact" section, loaded after the first is exhausted or on demand).
  This makes the split correct for the whole tenant, not only for loaded pages.
- Implementation hint: resolve the account-id set from the links collection (distinct `AccountId` of active links in
  the tenant) and apply it as `Id IN` / `Id NIN` on the account query, as `territoryNodeId` already does with its
  coverage set (`Diten.CrmService.Persistence/Repositories/AccountRepository.cs`).

### R3 — territory filter options readable by field users (required for the territory filter)

Field users need to see the territory options that can actually narrow **their** account list, without
`crm.territory.node.read`. The backend developer chooses one of these:

- **R3-a (preferred):** `GET /api/crm/accounts/filter-options` under `crm.account.read`. It returns the distinct
  territory nodes (id, code, name) and account types (code) present on the caller's tenant accounts, optionally
  narrowed by `search`. Read-only, tenant-scoped, no hierarchy write data.
- **R3-b:** accept `crm.territory.read` as a read fallback on `GET /api/crm/territory-models/nodes/by-ids` and
  `…/{id}/nodes`, as several other features already do (`ReadFallback = "crm.territory.read"`, for example
  `…/Features/VisitReport/VisitReportPermissions.cs:24`).

### R4 — account-type labels (dependency, already reported)

Covered by **MOD-0048 R1** (tenant-scoped `account-type` published values). If R3-a returns codes only, mobile
labels them through the MOD-0048 route once it is open. Until then mobile shows the raw code.

## 5. Contract mobile expects

```
GET /api/crm/accounts?search=ecz&page=1&pageSize=25&hasActiveContacts=true&accountType=pharmacy&territoryNodeId=<guid>&sortBy=accountname&sortDir=asc
```

```json
{ "data": { "items": [ { "id": "…", "accountName": "0505 ФАРИН ФАРМ", "accountCode": "0505", "accountType": "pharmacy",
                         "accountCategory": null, "status": "active", "parentAccountId": null,
                         "territoryNodeId": "…", "territoryNodeCode": "…", "territoryNodeName": "…",
                         "territoryCountryScope": "…", "activeContactCount": 3 } ],
            "total": 412, "page": 1, "pageSize": 25, "unfilteredTotal": 5310 },
  "errors": null, "statusCode": 200, "isSuccessful": true }
```

- **Errors.** An invalid `hasActiveContacts` value (anything but `true`/`false`, case-insensitive) → 400 with a
  validation error, **or** it is ignored like other unknown values today. Pick one and document it; mobile only
  sends `true` or `false`.
- **Backward compatibility.** `activeContactCount` is additive. Older clients ignore it. Without the new
  parameter, the response is unchanged.

## 6. Security and tenant isolation

- The tenant is always the server-resolved tenant (JWT). No tenant, account or link id from the client widens the
  scope.
- The count reveals only the **number** of active links on an account the caller can already list. No contact data
  is added to the list.
- **Open question for the backend developer:** should `activeContactCount` require `crm.account-contact.read` as
  well? Recommendation: return it under `crm.account.read`, because it is a count, not contact data. If not, return
  `null` when the caller lacks `crm.account-contact.read`, and ignore `hasActiveContacts` in that case. Mobile hides
  the split when the value is `null`.

## 7. Acceptance criteria

1. Every list item carries `activeContactCount`. It equals the number of rows `GET /accounts/{id}/contacts` returns
   whose status is not `ended`/`inactive` (case-insensitive), for the same tenant and moment.
2. `hasActiveContacts=true` returns only accounts with a count ≥ 1. `false` returns only accounts with 0. Absent
   returns today's result. All three combine with `search`, `accountType`, `status`, `territoryNodeId`,
   `countryScope`, `sortBy`, `sortDir` and paging.
3. `total` reflects the new filter.
4. One page costs a bounded number of database round trips, independent of `pageSize` (no N+1).
5. A field user (role without `crm.territory.node.read`) can obtain territory filter options (R3).
6. No cross-tenant data: an account of tenant B never appears, and links of tenant B never change a count in
   tenant A.

## 8. Backend test scenarios

- An account with 2 active links, 1 `ended` and 1 `Inactive` (mixed case) → count 2.
- An account whose only active link points to a soft-deleted contact → count 0. It appears under
  `hasActiveContacts=false`.
- A soft-deleted link → not counted.
- `hasActiveContacts=true` + `search` + `accountType` + `territoryNodeId` → intersection only, with the correct `total`.
- Paging over `hasActiveContacts=true` with 60 matches and `pageSize=25` → pages of 25/25/10, no duplicates, stable
  order.
- Same account id in another tenant with active links → not counted, not returned.
- R3: a field-user token gets the territory options. An unauthenticated call gets 401.

---

## BACKEND REQUIREMENT STATUS

- Repository: ERP-vNext — READ-ONLY
- Code Changes: NONE
- Branch: NOT CREATED
- Commit: NOT CREATED
- Mobile status: interim client-side split (per-row probe, loaded pages only) — STOPGAP until R1 + R2.
  Territory filter options: from loaded rows only, until R3. Type labels: raw codes, until MOD-0048 R1.
- Requested items: R1 (required), R2 (required), R3 (required for the territory filter), R4 = MOD-0048 R1 (dependency)
