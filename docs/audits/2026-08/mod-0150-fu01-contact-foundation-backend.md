# MOD-0150-FU01 — Contact Foundation Backend

**Date:** 2026-07-17 · **Verdict:** **PASS** — Contact aggregate + CRUD + external references + MOD-0048 reference validation + MOD-0018 permission enforcement implemented; tests 32/32; Gateway smoke proven end-to-end. No AccountContactLink / AccountRelationship / Consent / frontend / Zone-Territory.

## Preflight

- MOD-0150 status: ready-for-dev, `runtime_code_scope=FU01-contact-foundation-backend-only`.
- Reference publish: **contact-type 200/9, contact-status 200/4, contact-role 200/7** published (FU01 needs type+status → PASS). `account-relationship-type/status` still Submitted/Pending (FU03/FU04 — not FU01 blockers).
- Scope: Contact foundation only. No link/relationship/consent/frontend/import-export.

## Reference verification

| SetCode | Expected | Actual | Result |
|---|---|---|---|
| contact-type | ≥1 (required) | **9** | ✅ |
| contact-status | ≥1 (required) | **4** | ✅ |
| contact-role | (FU03) | 7 | ✅ preflight-visible |
| account-relationship-type / status | (FU04) | 400 (pending publish) | ⏳ not FU01 blocker |

## Implementation summary

- **Domain:** `Contact` (aggregate; First/Last/DisplayName, ContactType, Status, title/specialty/department/phone/email/notes) + `ContactExternalReference` (SourceSystem+ExternalId, unique per tenant). Extends `EntityBase` (soft delete, Version). No Account/Zone/Territory/Role fields.
- **Application/CQRS:** Create/Update/Delete commands; GetById/Overview/List/Search queries + handlers. `ContactReferenceValidation` (contact-type/status via MOD-0048 seam). DisplayName auto-derived from First+Last when blank. Duplicate external ref → 409. `Response<T>` pattern; tenant required; soft-delete filtered.
- **Persistence:** `ContactRepository` + `ContactExternalReferenceRepository` (Mongo, tenant+soft-delete filters); Guid-as-string class maps; indexes (tenant+display, tenant+type, tenant+deleted+status, unique tenant+source+external, tenant+contact).
- **API:** `ContactController` `/api/crm/contacts` — GET list/search/{id}/{id}/overview, POST, PUT, DELETE. Per-action `[HasPermission("crm.contact.*")]`. import/export declared-not-implemented (FU06).
- **Gateway:** added `/api/crm/contacts` + `/api/crm/contacts/{everything}` routes → 5061.
- **Permissions:** 7 `crm.contact.*` keys added to AuthService DataSeeder (`crm-contact` module) + `crm-contact` in `AdminModules` + `SeedTenant97c5CrmContactGrantAsync` (idempotent grant to the 97c5 Admin role; no Mongo hand-edit). Backend enforces `[HasPermission]`; live JWT carries all 7.
- **Audit:** `IContactAuditPublisher` seam + `LoggingContactAuditPublisher` (MOD-0021 HTTP wiring = FU06).
- **Tests:** `ContactFoundationTests` (13 new) → **32/32** total green.

## Contact smoke proof (Gateway 5000, 97c5 CRM Admin, live)

| Step | Evidence | Result |
|---|---|---|
| Create, DisplayName blank | 201, id `e56900fe-…` | ✅ |
| DisplayName auto-derived | `"Ahmet Yilmaz"` | ✅ |
| List (search) | 200 | ✅ |
| GetById | 200 + detail | ✅ |
| Overview (Contact 360) | 200, LinkedAccounts empty (FU03) | ✅ |
| Delete | 200 | ✅ |
| Reload deleted | 404 | ✅ |

## Failure path proof

| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| Invalid contact-type | 400 | 400 (published-value validation) | ✅ |
| Invalid contact-status | 400 | 400 (unit-tested) | ✅ |
| Missing required set | controlled 400 | SetMissing → 400 (unit-tested) | ✅ |
| Deprecated reference value | 400 | InvalidValue → 400 (unit-tested) | ✅ |
| Duplicate external reference | 409 | 409 "already exists." | ✅ |
| Cross-tenant get | 404 | 404 (unit-tested) | ✅ |

## Validation commands

| Command | Result |
|---|---|
| build CrmService.Api | ✅ 0 errors |
| test CrmService.Application.Tests | ✅ **32/32** |
| build ApiGateway (route added) | ✅ 0 errors |
| build AuthService.Api (permission seed added) | ✅ 0 errors |
| published-values contact-type/status | ✅ 9 / 4 |
| Gateway API smoke | ✅ all pass |

## Boundary / SoR proof

| Object/Capability | Owner | Touched? | Risk |
|---|---|---|---|
| Contact master | MOD-0150 | ✅ implemented | none |
| Account master | MOD-0149 | No (no Contact fields on Account) | none |
| AccountContactLink | MOD-0150 FU03 | No | none |
| AccountRelationship | MOD-0150 FU04 | No | none |
| Consent engine | MOD-0164 | No | none |
| Zone/MicroZone/Territory/SalesRep | MOD-0151 | No (absent from Contact) | none |
| Reference values | MOD-0048 | consumed via seam; no local seed | none |
| Permission engine | MOD-0018 | consumed; `crm.contact.*` seeded | none |

## Out-of-scope guard

| Forbidden Item | Found? | Status |
|---|---|---|
| Contact fields on Account entity | No | ✅ |
| ZoneId/MicroZoneId/TerritoryId/SalesRepId | No | ✅ |
| crm.contact.360.read | No (only "not used" comment) | ✅ |
| hardcoded contact-type/status fallback | No | ✅ |
| CRM local reference seed | No | ✅ |
| AccountContactLink / AccountRelationship impl | No (FU03/FU04) | ✅ |
| Consent engine impl | No (MOD-0164) | ✅ |

## Open items / blockers

| Item | Severity | Owner | Blocks FU02? | Notes |
|---|---|---|---|---|
| Fleet: CrmService/Gateway/Auth/Web/Platform now standalone `dotnet run` | Low | ops | No | re-run watch to restore hot reload |
| import/export endpoints | Low | FU06 | No | declared-not-implemented |
| MOD-0021 audit HTTP wiring | Low | FU06 | No | logging seam today |
| account-relationship-type/status publish | Low | operator | No (FU04) | pending 2nd approver |

## Final verdict: PASS

Contact Foundation Backend implemented, tests 32/32 green, MOD-0048 reference validation proven (invalid → 400), Gateway smoke passed end-to-end (create → auto DisplayName → detail/overview → invalid 400 → duplicate 409 → soft-delete → 404). Boundary clean; no fake readiness, no local seed, no hardcoded fallback, tenant isolation preserved. Next: **MOD-0150-FU02 Contact Frontend Compact Vertical**.
