# MOD-0150-FU04 — Account-to-Account Relationships

**Date:** 2026-07-17 · **Verdict:** **PASS** — `AccountRelationship` directional aggregate + CRUD + account existence + account-relationship-type/status validation + **metadata-driven direction/inverse-display/self-link** + duplicate (bidirectional-aware) + Account 360 Related Accounts projection implemented; CrmService tests 62/62; live Gateway smoke incl. inverse display proven. No Consent, no Zone, no relationship graph.

## Preflight
- MOD-0150 status: FU03 links done. FU04 scope = Account↔Account relationships.
- FU01/FU02/FU03 dependency: PASS.
- Scope: AccountRelationship only. No Contact/link change, no graph, no Consent/import-export/Zone.

## Reference verification
| SetCode | Expected | Actual | Metadata | Result |
|---|---|---|---|---|
| account-relationship-type | 6 | **6** | ✅ direction/inverseLabelCode/selfAllowed present | ✅ (operator published) |
| account-relationship-status | 4 | **4** | — | ✅ |

## Implementation summary
- **Domain:** `AccountRelationship` (Source→Target, RelationshipType, Direction, Status, validity, Notes) + `IAccountRelationshipRepository`.
- **Metadata seam:** added `IReferenceMetadataReader.GetValueAttributesAsync` (implemented by `GatewayReferenceDataValidator`) to read a published value's `attributes` — separate interface so existing validators/tests are untouched. `RelationshipTypeMetadata.Parse` → direction/inverseLabelCode/selfAllowed (degraded mode when absent: directional, no inverse, self-forbidden).
- **Application/CQRS:** Create/Update/Delete + ListRelationshipsForAccount/GetById + handlers + validators + mapper. Source+Target existence (MOD-0149) + type/status validation (MOD-0048). **Direction derived from metadata** (never blind from request). **Self-link forbidden unless `selfAllowed=true`** (D4). **Duplicate unique active (Tenant,Source,Target,Type); bidirectional types also match the reverse pair** (D5).
- **Persistence:** `AccountRelationshipRepository` (Mongo, tenant+soft-delete); Guid-as-string class map; indexes incl. unique active directional pair; **bidirectional reverse duplicate enforced at repository level** (checks both directions).
- **API:** `AccountRelationshipController` — `GET/POST/PUT/DELETE /api/crm/accounts/{accountId}/relationships[/{relationshipId}]` + `/related-accounts`. Read=`crm.account-relationship.read`, mutate=`crm.account-relationship.manage`. Under existing Gateway wildcard — **no new route**.
- **Permissions:** `crm.account-relationship.read`/`.manage` seeded (module `crm-contact`) + 97c5 grant extended; live JWT carries both.
- **Projection / inverse display:** Account 360 Related Accounts — from the source's view shows the direct type; from the target's view shows the **inverseLabelCode** (D3). Bidirectional shows the same label both sides. Account Details frontend render = follow-up (backend projection ready).
- **Tests:** `AccountRelationshipTests` (17) → **62/62** total.

## AccountRelationship smoke proof (Gateway 5000, 97c5 Admin, live)
| Step | Evidence | Result |
|---|---|---|
| Create A served-by B | 201, direction=outbound | ✅ |
| A related-accounts | Pharmacy, displayDirection=direct, effectiveLabelCode=served-by | ✅ |
| **B related-accounts (inverse)** | Hospital, displayDirection=inverse, **effectiveLabelCode=serves** | ✅ |
| Delete relationship | 200 → list reflects removal | ✅ |

## Failure path proof
| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| Duplicate (directional) | 409 | 409 | ✅ |
| Bidirectional reverse duplicate (same-network B→A) | 409 | 409 | ✅ |
| Self-link (default) | 400 | 400 | ✅ |
| Invalid relationship type | 400 | 400 | ✅ |
| Invalid status | 400 | 400 | ✅ |
| Missing source/target account | 404 | 404 (unit) | ✅ |
| Soft-deleted source/target | 404 | 404 (unit) | ✅ |
| Missing required set | controlled 400 | 400 (unit) | ✅ |
| Cross-tenant | 404 | 404 (unit) | ✅ |

## Projection / inverse display proof
| Scenario | Expected | Observed | Result |
|---|---|---|---|
| Source view (A, served-by) | direct → "served-by" | direct / served-by | ✅ |
| Target view (B) | inverse → "serves" | inverse / serves | ✅ |
| Bidirectional (same-network) | same label both sides | bidirectional | ✅ |

## Metadata proof
| RelationshipType | direction | inverseLabelCode | selfAllowed | Consumed? | Result |
|---|---|---|---|---|---|
| associated-with | bidirectional | associated-with | false | ✅ (reverse-dup + label) | ✅ |
| preferred-pharmacy | directional | preferred-by | false | ✅ | ✅ |
| refers-to | directional | referred-by | false | ✅ | ✅ |
| served-by | directional | serves | false | ✅ (inverse display live) | ✅ |
| same-network | bidirectional | same-network | false | ✅ (reverse-dup live) | ✅ |
| nearby | bidirectional | nearby | false | ✅ | ✅ |

## Validation commands
| Command | Result |
|---|---|
| build CrmService.Api | ✅ 0 errors |
| test CrmService.Application.Tests | ✅ **62/62** |
| build ApiGateway | ✅ 0 (no route change; wildcard covers) |
| build AuthService.Api | ✅ 0 (permission seed) |
| published-values account-relationship-type/status | ✅ 6+metadata / 4 |
| Gateway API smoke | ✅ all pass |

## Boundary / SoR
| Object/Capability | Owner | Touched? | Risk |
|---|---|---|---|
| AccountRelationship | MOD-0150 FU04 | ✅ | none |
| Account master | MOD-0149 | No relationship array embedded | none |
| Contact / AccountContactLink | MOD-0150 FU01-03 | No | none |
| Consent | MOD-0164 | No | none |
| Zone/Territory/SalesRep | MOD-0151 | No | none |
| Reference values + metadata | MOD-0048 | consumed; no local seed | none |
| Direct 5061 browser | — | No | none |

## Out-of-scope guard
| Forbidden Item | Found? | Status |
|---|---|---|
| relationship array on Account / field on Contact | No | ✅ |
| AccountContactLink changed outside FU03 | No | ✅ |
| Consent engine | No | ✅ |
| Zone/MicroZone/Territory/SalesRep | No | ✅ |
| crm.contact.360.read / crm.relationship.360.read | No | ✅ |
| hardcoded relationship-type/status fallback / CRM local seed | No | ✅ |
| relationship graph visualization | No | ✅ |
| direct 5061 in frontend | No | ✅ |

## Open items / blockers
| Item | Severity | Owner | Blocks FU05/FU06? | Notes |
|---|---|---|---|---|
| Account Details "Related Accounts" + "Related Contacts" render (MOD-0149 Details.cshtml) | Low | frontend | No | backend projections ready; frontend follow-up |
| Relationship management UI (add/edit on Account details) | Low | frontend | No | API ready |
| Fleet standalone `dotnet run` (fragile) | Low | ops | No | re-run watch |

## Final verdict: PASS
AccountRelationship implemented, tests 62/62, account-relationship-type/status + metadata validation proven (invalid→400), Gateway smoke end-to-end incl. **live inverse display (target sees "serves")** and **bidirectional reverse duplicate 409**, self-link 400, duplicate 409. Direction derived from metadata (not request). Boundary clean; no graph, no Consent, no local seed. Next: **MOD-0150-FU05 Consent / Preference Seam** (or FU06 Import/Export/Audit Hardening).
