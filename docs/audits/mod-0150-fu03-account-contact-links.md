# MOD-0150-FU03 — Account Contact Links

**Date:** 2026-07-17 · **Verdict:** **PASS** — `AccountContactLink` M:N aggregate + CRUD + Account/Contact existence + contact-role validation + duplicate/primary uniqueness + Account 360 / Contact 360 projections implemented; CrmService tests 45/45; live Gateway smoke end-to-end. No Account↔Account relationship, no Consent, no Zone.

## Preflight
- MOD-0150 status: FU02 frontend done. FU03 scope = Contact↔Account link management.
- FU01/FU02 dependency: PASS. Reference: contact-role 7, contact-type 9, contact-status 4.
- Scope: AccountContactLink only. No AccountRelationship/Hospital-Pharmacy/Consent/import-export/Zone.

## Reference verification
| SetCode | Expected | Actual | Result |
|---|---|---|---|
| contact-role | 7 | 7 | ✅ |
| contact-type | 9 | 9 | ✅ |
| contact-status | 4 | 4 | ✅ |

## Implementation summary
- **Domain:** `AccountContactLink` (AccountId, ContactId, RoleCode, IsPrimary, Status, ValidFrom/To, Notes) + `IAccountContactLinkRepository`. **Status decision:** no `account-contact-link-status` reference set exists yet → Status is a free internal marker (default "active"), **not** reference-validated (no hardcoded fallback); active-link logic uses IsDeleted + validity. A dedicated set is a follow-up.
- **Application/CQRS:** Link/Update/Delete commands + ListContactsForAccount/ListAccountsForContact/GetById queries + handlers + validators + mapper. Account existence (MOD-0149 repo) + Contact existence (MOD-0150 repo) + contact-role validation (MOD-0048 seam). `ValidFrom<=ValidTo`. **Duplicate active (Tenant,Account,Contact,Role)→409; second primary per (Account,Role)→409 (no auto-unset).**
- **Persistence:** `AccountContactLinkRepository` (Mongo, tenant+soft-delete filters); Guid-as-string class map; indexes incl. **unique active natural key** + **unique active primary per (Account,Role)** partial indexes.
- **API:** `AccountContactController` — account-centric `GET/POST/PUT/DELETE /api/crm/accounts/{accountId}/contacts[/{linkId}]` + `/related-contacts`; contact-centric `GET /api/crm/contacts/{contactId}/accounts` + `/linked-accounts`. Read=`crm.account-contact.read`, mutate=`crm.account-contact.manage`. Routes sit under existing Gateway wildcards — **no new Gateway route**.
- **Permissions:** `crm.account-contact.read`/`.manage` added to AuthService seed (module `crm-contact`) + `SeedTenant97c5CrmContactGrantAsync` extended to grant `crm.account-contact.*` to the 97c5 Admin; live JWT carries both.
- **Projections:** Account 360 Related Contacts (`ListContactsForAccount`) + Contact 360 Linked Accounts (wired into `GetContactOverviewHandler` — real data). Contact Details view now renders Linked Accounts (name/code/role/primary); Account Details Related Contacts render = frontend follow-up (backend projection ready via `/accounts/{id}/related-contacts`).
- **Tests:** `AccountContactLinkTests` (13) → **45/45** total.

## AccountContactLink smoke proof (Gateway 5000, 97c5 Admin, live)
| Step | Evidence | Result |
|---|---|---|
| JWT perms | crm.account-contact.read + manage | ✅ |
| Link C1 (decision-maker, primary) | 201 | ✅ |
| GET account contacts | "Link One", isPrimary:true | ✅ |
| GET contact accounts | ACC-2026-000007, decision-maker | ✅ |
| Contact overview linkedAccounts | "Link Smoke Hospital" (projection wired) | ✅ |
| Delete link | 200 → list 0 | ✅ |

## Failure path proof
| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| Duplicate active link | 409 | 409 | ✅ |
| Second primary same (Account,Role) | 409 | 409 | ✅ |
| Invalid role | 400 | 400 | ✅ |
| Missing account/contact | 404 | 404 (unit) | ✅ |
| Soft-deleted account/contact | 404 | 404 (unit) | ✅ |
| Cross-tenant link | 404 | 404 (unit) | ✅ |

## Projection proof
| Projection | Evidence | Result |
|---|---|---|
| Account 360 Related Contacts | GET /accounts/{id}/contacts + /related-contacts → DisplayName/RoleCode/IsPrimary/Phone/Email | ✅ |
| Contact 360 Linked Accounts | GET /contacts/{id}/accounts + /linked-accounts + Contact overview → AccountName/Code/Type/Role/Primary | ✅ |

## Validation commands
| Command | Result |
|---|---|
| build CrmService.Api | ✅ 0 errors |
| test CrmService.Application.Tests | ✅ **45/45** |
| build ApiGateway | ✅ 0 errors (no route change; wildcard covers) |
| build AuthService.Api | ✅ 0 errors (permission seed) |
| build Diten.Web | ✅ 0 errors (Contact Details linked-accounts render) |
| published-values contact-role | ✅ 7 |
| Gateway API smoke | ✅ all pass |

## Boundary / SoR
| Object/Capability | Owner | Touched? | Risk |
|---|---|---|---|
| AccountContactLink | MOD-0150 FU03 | ✅ | none |
| Account master | MOD-0149 | No Contact fields added | none |
| Contact master | MOD-0150 | No Account array embedded | none |
| Account↔Account relationship | FU04 | No | none |
| Consent | MOD-0164 | No | none |
| Zone/Territory/SalesRep | MOD-0151 | No | none |
| Reference values | MOD-0048 | consumed; no local seed | none |
| Direct 5061 browser | — | No | none |

## Out-of-scope guard
| Forbidden Item | Found? | Status |
|---|---|---|
| Contact fields on Account / Account array on Contact | No | ✅ |
| AccountRelationship / Hospital-Pharmacy impl | No (FU04) | ✅ |
| Consent engine | No | ✅ |
| Zone/MicroZone/Territory/SalesRep | No | ✅ |
| crm.contact.360.read | No | ✅ |
| hardcoded contact-role fallback / CRM local seed | No | ✅ |
| direct 5061 in frontend | No | ✅ |

## Open items / blockers
| Item | Severity | Owner | Blocks FU04? | Notes |
|---|---|---|---|---|
| Account Details "Related Contacts" render (MOD-0149 Details.cshtml) | Low | frontend | No | backend projection ready; frontend follow-up |
| `account-contact-link-status` reference set | Low | MOD-0048 | No | Status currently free internal marker |
| Link management UI (add/remove on Contact/Account details) | Low | frontend | No | API ready; UI follow-up |
| Fleet standalone `dotnet run` (fragile) | Low | ops | No | re-run watch |

## Final verdict: PASS
AccountContactLink implemented, tests 45/45, contact-role validation proven (invalid→400), Gateway smoke end-to-end (link → projections → duplicate 409 → primary 409 → invalid 400 → delete → excluded); Contact 360 Linked Accounts wired to real data. Boundary clean. Next: **MOD-0150-FU04 Account-to-Account Relationships**.
