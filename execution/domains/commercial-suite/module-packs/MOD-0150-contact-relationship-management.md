---
id: MOD-0150
name: Contact & Relationship Management
domain: commercial-suite
service: Diten.CrmService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
owner: module-pack-author
branch: feature/crm/mod-0150-contact-relationship-management
started: 2026-07-17
target: 2026-09-30
form_field_count: 14
runtime_code_allowed: true
runtime_code_scope: "FU01-contact-foundation-backend-only (Contact aggregate + CRUD + reference validation + permissions + tests; NO AccountContactLink/AccountRelationship/frontend/import-export until later FUs); FU-contact-availability-visit-preference (AccountContactLink-scoped ContactAvailability + VisitPreference + date-specific availability exceptions + read/write APIs + minimal UI + tests; NO route/visit planning, NO frequency/call-cycle engine, NO campaign engine, NO territory assignment, NO GPS/check-in, NO hard delete)"
ready_for_dev_by: MOD-0150-PREREQ authoring template complete 2026-07-17 (D1–D7 approved 2026-07-17). Operator MOD-0048 publish of the 5 required sets is a runtime create-smoke prerequisite (not a code blocker) — MOD-0149 parity.
dependencies:
  - MOD-0018
  - MOD-0048
  - MOD-0021
  - MOD-0285
  - MOD-0149
  - MOD-0164 (soft — consent/preference read-only seam; engine NOT built here)
  - commercial-suite-domain-foundation
---

# MOD-0150 — Contact & Relationship Management

> **APPROVED-PENDING-PREREQ (Pack Review / Approval Gate 2026-07-17).** The pack and decisions **D1–D7 are APPROVED**;
> boundary, permission (PKS-001) and reference gates PASS. Status is **not yet ready-for-dev** for one parity reason:
> the **MOD-0048 required reference sets** (`contact-type`, `contact-status`, `contact-role`,
> `account-relationship-type`, `account-relationship-status`) need an **authoring template first** — exactly as MOD-0149
> gated ready-for-dev on its reference readiness. `runtime_code_allowed` stays **false** until **MOD-0150-PREREQ**
> (authoring template) is complete; it then flips to `ready-for-dev` + `runtime_code_allowed: true` scoped to
> **FU01-contact-foundation-backend-only**. No runtime code / controller / entity / frontend / migration / seed /
> gateway route / permission seed was produced in the pack or this gate.
> Authority: Module Pack > Domain Config ([../domain-config.md](../domain-config.md)) > AGENTS.md > .antigravity/rules.
> Builds on **MOD-0149** (review-ready PASS) Account foundation. **Consent is NOT owned here → MOD-0164** (no pack yet →
> D7 read-only seam / SetMissing-tolerated confirmed; no hard dependency).

> ### Decision Gate outcome (D1–D7 — all APPROVED)
> | ID | Decision | Result |
> |---|---|---|
> | D1 | Contact↔Account = M:N `AccountContactLink` | ✅ approve |
> | D2 | Primary uniqueness per `(Account, RoleCode)` | ✅ approve |
> | D3 | Relationship directional + inverse-label reference | ✅ approve |
> | D4 | Self-link forbidden unless type self-allowed | ✅ approve |
> | D5 | Duplicate relationship unique active → 409 | ✅ approve |
> | D6 | Reuse `Diten.CrmService` (no new service) | ✅ approve |
> | D7 | Consent seam read-only / no-op when MOD-0164 absent | ✅ approve (MOD-0164 has no pack yet) |
>
> **MOD-0150-PREREQ complete (2026-07-17):** the MOD-0048 required-set authoring template + operator checklist are ready
> ([template md](../../../docs/audits/mod-0150-required-reference-authoring-template.md) ·
> [template json](../../../docs/audits/mod-0150-required-reference-authoring-template.json)) — counts 9/4/7/6/4,
> lowercase-kebab, no duplicates, account-relationship-type metadata (direction/inverse/self). Status is now
> **ready-for-dev**, `runtime_code_allowed: true` scoped **FU01-only**. **Operator must publish the 5 required sets in
> MOD-0048 before the FU01 create smoke** (create/update validation returns controlled 400 until then) — a runtime
> validation prerequisite, **not** a code blocker (MOD-0149 parity).

> ### Scope update — `FU-contact-availability-visit-preference` (2026-08-01)
> **Additive authorization.** MOD-0150 now also owns **`ContactAvailability` / `VisitPreference` master data, scoped to
> `AccountContactLink`** (§20). The requirement was recorded during **MOD-0151 FU09A** pack authorization
> ([evidence](../../../../docs/audits/mod-0151-fu09a-visit-route-readiness-boundaries-pack-authorization-2026-08-01.md)):
> route readiness cannot answer *"can this doctor be visited here, on this day, in this window?"* unless availability is
> mastered somewhere — and it **cannot be a flat field on `Contact`**, because the same doctor works at several
> hospitals / clinics / pharmacies with **different** days and hours per location. `AccountContactLink` (D1, M:N) is
> therefore the correct key.
> **Ownership chain:** **MOD-0150 = master** · **MOD-0151 (FU09A) = read-only consumer** for route readiness ·
> **MOD-0155 = consumer** that produces visit / route plans. No availability data is copied into MOD-0151, and no
> `ContactAvailability` aggregate may be opened there.
> **This is not a general runtime grant.** Route optimization, daily route building, visit planning/execution,
> check-in/check-out, GPS validation, visit reports, digital detailing, survey, campaign engine, frequency /
> call-cycle engine, territory assignment, `ContactTerritoryAssignment`, Account master mutation, patient data,
> workflow approval / ChangeRequest / MOD-0023 and hard delete remain **unauthorized** (§20 exclusions).
> Existing FU01–FU06 scopes are **preserved unchanged**.

## 1. Module Summary

MOD-0150 owns **Contacts** (people who work at / relate to Accounts — doctor, pharmacist, responsible person,
department/decision-maker/procurement/medical/administrative contact) and **relationship links**: Contact↔Account and
**Account↔Account** (Hospital↔Pharmacy, Clinic↔Pharmacy, preferred/associated pharmacy, nearby, same-network,
referral, served-by). It consumes MOD-0149 Account master (never re-owns it), MOD-0048 reference data, MOD-0018
permissions, MOD-0021 audit, and a read-only MOD-0164 consent/preference seam.

## 2. Business Context

The legacy pharma CRM modelled institutions and the people/relationships around them (a hospital's decision-maker
doctor; a hospital's associated pharmacy; a clinic that refers to a hospital). MOD-0149 gave us the Account master +
hierarchy + 360. MOD-0150 adds the **people layer** (Contacts) and the **peer-relationship layer** (Account↔Account),
so Account 360 can show *Related Contacts* and *Related Accounts* — the missing halves of "Customer 360".

## 3. Ownership / SoR Boundary

**Owns:** Contact master · Contact-to-Account assignment (`AccountContactLink`) · Account contact roles · Primary-contact
flag · Contact communication fields · Contact status · Contact relationship links · **Account-to-Account relationship
links** (`AccountRelationship`) · relationship type/status/validity · consent-reference seam · interaction-preference
reference seam · **`AccountContactLink`-scoped contact availability / working schedule, visit preference and
date-specific availability exceptions** (§20).

**Does NOT own (consume-only):** Account master / AccountCode generation / Account hierarchy parent-child →
**MOD-0149** · Consent policy engine / consent capture legal workflow / preference definitions → **MOD-0164** ·
Territory / Zone / MicroZone / SalesRep assignment → **MOD-0151** · Visit / Route planning → later CRM module ·
Campaign / Segment → Marketing · Lead / Opportunity → CRM sales · Product / brand / SKU · Employee master · country/city/
district reference values → **MOD-0048** (no CRM local seed).

### 3.1 Account↔Account relationship ownership (kesin karar)
Account-to-Account relationships are **NOT** a MOD-0149 Account master field. MOD-0149 may render a **read-only
"Related Accounts"** section in Account 360 (a projection). **Creation/editing of relationships is owned by MOD-0150**
(FU04). This mirrors the MOD-0151 coverage rule (MOD-0149 shows a read-only placeholder; the owning module writes it).

## 4. Owned Objects

| Object | Kind | Notes |
|---|---|---|
| `Contact` | aggregate root | standalone CRM master; listable on its own; links to N Accounts |
| `AccountContactLink` | aggregate | M:N Contact↔Account with RoleCode + IsPrimary + validity |
| `AccountRelationship` | aggregate | directional Account↔Account link with type/status/validity |
| `ContactExternalReference` | child | SourceSystem + ExternalId (mirrors MOD-0149 AccountExternalReference) |
| `ContactAvailability` | aggregate | **§20** — weekly availability window **per `AccountContactLink`** (location-scoped), effective-dated, status-managed. **Never a flat field on `Contact`** |
| `ContactAvailabilityException` | aggregate | **§20** — date-specific override (leave, congress, surgery day, temporary location change). **Stronger than the weekly pattern** |
| `VisitPreference` | value object | **§20** — preferred/avoid window, appointment requirement + lead time, average duration, preferred contact method. Embedded in `ContactAvailability`; optional contact-level default |

## 5. Out-of-Scope

Account master data · AccountCode generation · Account hierarchy parent-child ownership · Territory/Zone/MicroZone/
SalesRep · Visit/Route · Consent policy engine · Consent capture legal workflow · Campaign/segmentation · Lead/
opportunity · Product/brand/SKU · Employee master · CRM local country/city/district seed.

**§20 clarification:** owning contact **availability** does **not** make MOD-0150 a planner. Visit plans, daily routes,
route optimization, visit execution, check-in/check-out, GPS validation, visit reports, **visit frequency /
call-cycle policy** and campaign targeting stay out of scope (MOD-0155 · MOD-0165 / MOD-0167). MOD-0150 answers
*"when can this person be visited at this location?"* — never *"who should be visited today, in what order?"*.

## 6. Dependencies

| MOD | Contract | Required for | Blocking? |
|---|---|---|---|
| MOD-0149 | Account existence validation, Account lookup/search, 360 related-contacts + related-accounts projections | link validation, Account 360 sections | **Yes** (hard) |
| MOD-0048 | published-values consumer (contact-type/status/role, account-relationship-type/status) | create/update validation | **Yes** (required sets block create) |
| MOD-0018 | permission engine (`crm.contact.*`, `crm.account-contact.*`, `crm.account-relationship.*`) | authz | **Yes** |
| MOD-0021 | audit hooks (contact + relationship create/update/delete) | audit | No (seam; HTTP wiring is FU06) |
| MOD-0285 | catalog / page descriptor / nav | menu | No (static menu interim, like MOD-0149) |
| MOD-0164 | consent-reference read + preference-reference read | consent/preference seam | **No** (read-only seam; engine deferred to MOD-0164) |

### 6.1 MOD-0048 reference readiness
Required sets (`contact-type`, `contact-status`, `contact-role`, `account-relationship-type`,
`account-relationship-status`) must be **published by the operator** before implementation validation — same governance
flow and consumer pattern as MOD-0149 (`published-values?scope_key={tenant}`; missing required set → controlled 400).
No CRM local seed, no hardcoded fallback.

## 7. Repo Scope (implementation, after approval)

Reuse **`Diten.CrmService`** (same 5-layer service as MOD-0149, port 5061) — new `Contact` / `AccountContactLink` /
`AccountRelationship` aggregates under `Features/Contact` and `Features/Relationship`. **No separate service.** This
keeps Account↔Contact↔Relationship in one bounded context (all CRM Core), reusing the Gateway route prefix `/api/crm/*`,
the Guid-as-string class-map convention, and the reference-validator seam.

## 8. Protected Paths / Runtime Constraints

- No Account entity pollution (no Contact fields on `Account`). No Zone/MicroZone/Territory/SalesRep anywhere.
- Reference values via MOD-0048 consumer only. No CRM local reference seed. No hardcoded fallback.
- Frontend calls Gateway (5000); never CrmService (5061) directly. Golden Reference **Compact** pattern.
- Consent/preference are **read-only reference seams** until MOD-0164 exists; MOD-0150 builds no consent engine.

## 9. Proposed Domain Model

### 9.1 Contact (aggregate root)
`ContactId, TenantId, FirstName, LastName, DisplayName, ContactType, ProfessionalTitle?, Specialty?, Department?,
Phone?, Email?, Status, ExternalReferences[], Notes?, CreatedAt, UpdatedAt, IsDeleted/DeletedAt, Version`.
- `ContactType` / `Status` validated against MOD-0048 (`contact-type` / `contact-status`).
- `DisplayName` auto-derived from First+Last when blank (mirrors AccountName/AccountCode pattern).
- `ExternalReferences` = `ContactExternalReference(SourceSystem, ExternalId, …)`, unique `(Tenant, SourceSystem, ExternalId)`.

### 9.2 AccountContactLink (M:N)
`AccountContactLinkId, TenantId, AccountId, ContactId, RoleCode, IsPrimary, Status, ValidFrom?, ValidTo?, Notes?` + audit base.
- `AccountId` validated to exist (MOD-0149), not soft-deleted. `RoleCode` validated against `contact-role`.
- Unique active `(Tenant, AccountId, ContactId, RoleCode)` — same contact can hold different roles at one account.
- **Primary rule (decision D2):** at most one `IsPrimary=true` per `(Account, RoleCode)`.

### 9.3 AccountRelationship (directional)
`AccountRelationshipId, TenantId, SourceAccountId, TargetAccountId, RelationshipType, Direction, Status, ValidFrom?, ValidTo?, Notes?` + audit base.
- Both accounts validated to exist (MOD-0149), not soft-deleted.
- `RelationshipType` / `Status` validated against `account-relationship-type` / `account-relationship-status`.
- **Direction (decision D3):** stored **directional** (Source→Target). `Direction ∈ {outbound, inbound, bidirectional}`.
  Inverse display uses an **inverse-label on the `account-relationship-type` reference** (e.g. `served-by` ⇄ `serves`,
  `refers-to` ⇄ `referred-by`); symmetric types (`same-network`, `nearby`, `associated`) stored once as `bidirectional`.
- **Self-link (decision D4):** forbidden unless the relationship-type is flagged self-allowed (default: forbid → 400).
- **Duplicate (decision D5):** unique active `(Tenant, Source, Target, RelationshipType)`; re-add → 409.

Directionality examples: `Hospital —associated-with→ Pharmacy` (+ inverse view on Pharmacy 360);
`Clinic —refers-to→ Hospital` (inverse `referred-by` on Hospital 360); `Hospital —served-by→ Pharmacy`.

### 9.4 ContactAvailability (aggregate — `AccountContactLink`-scoped, §20)

`AvailabilityId, TenantId, AccountContactLinkId, ContactId, AccountId, Weekday, StartTime, EndTime,
PreferredStartTime?, PreferredEndTime?, AvoidStartTime?, AvoidEndTime?, AppointmentRequired,
AverageVisitDurationMinutes?, AvailabilityType, EffectiveFrom?, EffectiveTo?, Notes?, Source, Status` + audit base
(`CreatedAt/CreatedBy/UpdatedAt/UpdatedBy`, soft-delete, `Version`).

- **`AccountContactLinkId` is the owning key** (decision **D8**). `ContactId` / `AccountId` are **denormalized
  navigation copies** of the link's own values — they are *derived from the link, never independently supplied*, so a
  row can never claim a contact/account pair the link does not have.
- `AvailabilityType` validated against MOD-0048 `contact-availability-type`
  (`working-hours` · `visiting-hours` · `preferred-window` · `restricted-window` · `appointment-only` ·
  `temporary-exception`). `Status` validated against `contact-availability-status`
  (`active` · `inactive` · `archived`). **No hardcoded fallback** — missing required set → controlled 400.
- `Weekday` is a stable, machine-readable value (ISO-8601 day: `monday` … `sunday`), not a localized label.
- Times are **local wall-clock times of the account location**, stored without a timezone offset; MOD-0150 does not
  own timezone master data. Consumers must not reinterpret them as instants.

### 9.5 VisitPreference (value object, §20)

`PreferredVisitDurationMinutes?, PreferredVisitStartTime?, PreferredVisitEndTime?, AvoidVisitStartTime?,
AvoidVisitEndTime?, AppointmentRequired, AppointmentLeadTimeDays?, PreferredContactMethod?, Notes?`.

- **Decision D10:** preference is read **in the `AccountContactLink` context**. Location-varying preference is stored
  **inside `ContactAvailability`**; a *general* contact-level default preference is **optional** and, when present, is
  a fallback only — the link-scoped value always wins.
- `PreferredContactMethod` reuses the existing `communication-preference-type` reference (§10) — no new value set.

### 9.6 ContactAvailabilityException (aggregate — date-specific, §20)

`AvailabilityExceptionId, TenantId, AccountContactLinkId, Date, IsAvailable, StartTime?, EndTime?, Reason?, Notes?,
Source, Status` + audit base.

- **Decision D12:** a date-specific exception **overrides the weekly pattern** for that date — it is strictly stronger.
- `IsAvailable=false` = not visitable that day (leave, congress, surgery day, temporary location change);
  `IsAvailable=true` + `StartTime`/`EndTime` = an ad-hoc window that need not exist in the weekly pattern.
- Example: *2026-09-12 — Dr. Ayşe is not at Medicana (congress)* → `IsAvailable=false`, `Reason=congress`.

## 10. Reference Data Requirements

| SetCode | Required? | Example values | Owner | Blocks create? |
|---|---|---|---|---|
| `contact-type` | **Required** | doctor, pharmacist, responsible-person, department-contact, decision-maker, procurement, medical, administrative, other | MOD-0048 | **Yes** |
| `contact-status` | **Required** | active, inactive, draft, archived | MOD-0048 | **Yes** |
| `contact-role` | **Required** | decision-maker, procurement, medical, administrative, billing, primary, other | MOD-0048 | **Yes** (for links) |
| `account-relationship-type` | **Required** | associated-with, preferred-pharmacy, refers-to, served-by, same-network, nearby | MOD-0048 | **Yes** (for relationships) |
| `account-relationship-status` | **Required** | active, inactive, pending, ended | MOD-0048 | **Yes** (for relationships) |
| `professional-title` | Optional | dr, prof, assoc-prof, pharm, nurse, other | MOD-0048 | No |
| `medical-specialty` | Optional | cardiology, oncology, pediatrics, … | MOD-0048 | No |
| `department-type` | Optional | purchasing, pharmacy, admin, clinical, … | MOD-0048 | No |
| `communication-preference-type` | Optional | phone, email, sms, none | MOD-0048 / MOD-0164 seam | No |
| `contact-availability-type` | **Required** (§20 only) | working-hours, visiting-hours, preferred-window, restricted-window, appointment-only, temporary-exception | MOD-0048 | **Yes** (for availability) |
| `contact-availability-status` | **Required** (§20 only) | active, inactive, archived | MOD-0048 | **Yes** (for availability) |
| `availability-exception-reason` | Optional (§20) | leave, congress, surgery, training, temporary-relocation, other | MOD-0048 | No |

> **§20 note:** these three sets are **proposals for the MOD-0048 authoring template**, exactly like the original five.
> This pack **creates and publishes nothing**; until the operator publishes them, availability create/update returns a
> controlled 400 (fail-closed) — a runtime prerequisite, not a code blocker. They do **not** block FU01–FU06.

## 11. Permission Model (PKS-001, MOD-0018)

| Permission | Purpose | UI/API usage |
|---|---|---|
| `crm.contact.read` | list/detail contact | Contacts list, Details, lookups (JSON) |
| `crm.contact.create` | create contact | Create page/POST |
| `crm.contact.update` | update contact | Edit page/POST |
| `crm.contact.delete` | soft-delete contact | delete action |
| `crm.contact.import` | bulk import | FU06 |
| `crm.contact.export` | export (PII) | FU06 |
| `crm.account-contact.read` | read account-contact links | Account 360 Related Contacts, Contact's Accounts tab |
| `crm.account-contact.manage` | link/unlink, set role/primary | link editor |
| `crm.account-relationship.read` | read relationships | Account 360 Related Accounts |
| `crm.account-relationship.manage` | create/edit/delete relationship | relationship editor |
| `crm.contact.overview.read` | Contact 360 read model | Contact Details/overview |
| `crm.relationship.overview.read` | relationship overview read model | relationship overview |
| `crm.contact.consent.read` | read consent references (seam) | Contact Details consent seam (read-only) |
| `crm.contact.preference.read` | read preference references (seam) | Contact Details preference seam (read-only) |
| `crm.contact.availability.read` | **§20** read availability / visit preference / exceptions | Availability tab, link panel, MOD-0151 FU09A readiness lookup, MOD-0155 consumption |
| `crm.contact.availability.manage` | **§20** create/update/deactivate/archive availability + exceptions | Availability editor. **No delete key** — hard delete is forbidden |

> All keys are PKS-001 valid (lowercase-dotted, ≥3 segments, `^[a-z][a-z0-9-]*$` per segment). **No `crm.contact.360.read`**
> (digit-leading segment invalid) — use `crm.contact.overview.read`. `account-contact` / `account-relationship` are valid
> kebab segments. No new RBAC engine; consume MOD-0018.

> **§20 permission decision:** `crm.contact.availability.read` / `crm.contact.availability.manage` are the **canonical
> targets** (both PKS-001 valid). If the permission catalog / grants are not ready, the §20 implementation **must not
> seed or grant anything**; it falls back temporarily to the existing `crm.contact.read` (read) and
> `crm.contact.update` (manage) keys, scoped **only** to the availability endpoints. The fallback **does not widen
> authority** — every §20 validation guard still runs — and it introduces no new permission literal. Follow-up:
> **`MOD-0150-FU-RBAC — Contact Availability Permission Catalog Alignment`** (§19), mirroring the MOD-0151
> FU04A-RBAC / FU05-RBAC / FU08-RBAC pattern.

## 12. Integration Contracts

| Contract | Provider | Consumer | Required for | Blocking? |
|---|---|---|---|---|
| Account existence + lookup/search | MOD-0149 | MOD-0150 | link/relationship validation | **Yes** |
| Account 360 related-contacts projection | MOD-0150 | MOD-0149 (renders) | Account 360 Related Contacts | No (additive) |
| Account 360 related-accounts projection | MOD-0150 | MOD-0149 (renders) | Account 360 Related Accounts | No (additive) |
| CRM-CORE-BUNDLE (contact objects) | MOD-0150 | O2C / downstream | commercial bundle | No |
| CONSENT-BINDING (consent + preference reference read) | MOD-0164 | MOD-0150 | consent/preference seam | **No** (read-only; engine in MOD-0164) |
| Reference published-values | MOD-0048 | MOD-0150 | contact/relationship validation | **Yes** |
| Audit hooks (contact + relationship CUD) | MOD-0021 | MOD-0150 | audit | No (seam; FU06 HTTP wiring) |
| **Contact availability / visit preference read** (§20) | **MOD-0150** | **MOD-0151 FU09A** (route readiness) | `AvailabilityStatus` / `PreferredVisitWindow` on route candidate readiness | No — MOD-0151 returns `unknown` + reason code when absent |
| **Contact availability / exception read** (§20) | **MOD-0150** | **MOD-0155** (visit / route planning) | visit candidate eligibility, time windows, appointment requirement, average duration | No (MOD-0155 not started) |

## 13. UI / Navigation Proposal

Golden Reference **Compact** throughout (full Create/Edit/Details pages; DataTable v2; no offcanvas/quickview).
1. **Contacts list** (`/CRM/Contacts`) — DataTable v2, filters contact-type/status.
2. **Contact Create / Edit / Details** — compact full pages; Details = Contact 360 (linked accounts + consent/preference seam read-only).
3. **Contact → Accounts tab** — the accounts this contact is linked to (roles, primary).
4. **Account Details → Related Contacts** section (read model, MOD-0149 renders MOD-0150 projection).
5. **Account Details → Related Accounts** section (read model; relationships).
6. **Account relationship create/edit** — owned by MOD-0150 (`/CRM/Accounts/{id}/relationships` or a `/CRM/Relationships` surface).
7. First phase = **list/table** only (no relationship graph).
8. **Contact Details → Availability tab** (**§20**) — availability rows grouped by linked account/location, plus
   date-specific exceptions.
9. **AccountContactLink detail / relationship section → Availability panel** (**§20**) — the location-scoped editor;
   this is where availability is actually created and edited.
10. **Account 360 → Contact Availability panel** (**§20**, optional) — **read-only** projection, same rule as Related
    Contacts (MOD-0149 renders, MOD-0150 owns).

**§20 UI must show:** account / location · weekday · start–end · preferred window · avoid window · appointment
required (+ lead time) · average duration · effective dates · status · source · notes · date-specific exceptions.
**§20 UI must NOT contain:** build route · create visit plan · GPS / check-in / check-out · campaign or frequency
configuration · territory assignment editing · workflow approval actions · any hard-delete action.

Menu/catalog: static tenant-shell `<li>` gated by `crm.contact.read` (interim, like MOD-0149); page descriptor
registered via a Platform-side manifest provider with `IsNavigationVisible=false` until the MOD-0285 nav migration.

## 14. Implementation Sequencing

| FU | Name | Scope | Dependencies | Acceptance (key) |
|---|---|---|---|---|
| FU01 | Contact Foundation Backend | Contact aggregate + CRUD + reference validation + permissions + tests | MOD-0149, MOD-0048, MOD-0018 | invalid contact-type/status → 400; tests green |
| FU02 | Contact Frontend Compact Vertical | Contacts list/create/edit/details + menu/catalog/page descriptor | FU01 | golden flow PASS; compact verifier PASS |
| FU03 | Account Contact Links | `AccountContactLink` aggregate + Account 360 Related Contacts read model + primary rule | FU01, MOD-0149 | link to missing Account → 400; 1 primary per (Account,Role) |
| FU04 | Account-to-Account Relationships | `AccountRelationship` aggregate + type/direction + Account 360 Related Accounts | FU01, MOD-0149 | no self-link (unless allowed); duplicate → 409; inverse display |
| FU05 | Consent / Preference Seam | MOD-0164 read-only reference reads on Contact 360 | MOD-0164 (soft) | seam read-only; no consent engine |
| FU06 | Import / Export / Audit Hardening | contact + relationship import/export; MOD-0021 audit HTTP wiring | FU01–FU04, MOD-0021 | import validates references; audit events emitted |
| **FU07** | **Contact Availability & Visit Preference** (`FU-contact-availability-visit-preference`, §20) | `ContactAvailability` + `VisitPreference` VO + `ContactAvailabilityException` aggregates (**`AccountContactLink`-scoped**); validation + overlap conflict policy; read APIs (contact / link / account / date lookup); write APIs (create/update/deactivate/archive, **no hard delete**); Availability tab + link panel (Compact, 7-lang RESX); contract flags; tests + Gateway-only smoke | **FU03** (`AccountContactLink`), MOD-0048 (3 new sets), MOD-0018 | availability on an inactive link → 400; overlapping same-link+weekday window → controlled 409; `EffectiveTo < EffectiveFrom` → 400; exception overrides weekly pattern; duplicate identical row is idempotent; delete attempt → deactivate/archive only; **no route/visit/frequency surface exists** |

## 15. Acceptance Criteria

- Contact cannot be created with invalid `contact-type` / `contact-status` (→ 400).
- Contact cannot be linked to a non-existing / soft-deleted Account (→ 400).
- Same Contact may link to multiple Accounts; one Account may have multiple Contacts (M:N via `AccountContactLink`).
- **Primary-contact uniqueness:** at most one `IsPrimary=true` per `(Account, RoleCode)` (decision D2).
- `AccountRelationship` cannot link a deleted/non-existing Account (→ 400).
- `AccountRelationship` cannot self-link unless the relationship-type is explicitly self-allowed (default → 400).
- Duplicate relationship (`Tenant, Source, Target, RelationshipType` active) → 409.
- Relationship direction + inverse display defined (directional storage + inverse-label reference).
- Consent/preference references are a **read-only seam** unless MOD-0164 exists; no engine here.
- MOD-0149 Account entity is **not** polluted with Contact fields.
- No Zone/MicroZone/Territory/SalesRep fields introduced.
- No CRM local reference seed; no hardcoded fallback.

**§20 (Contact Availability & Visit Preference):**

- Availability is **always** created against an `AccountContactLink`; there is **no** availability write path that
  takes only a `ContactId` (→ 400), and **no** availability field is added to the `Contact` aggregate.
- The same contact linked to two accounts can hold **two independent** weekly schedules; reading one location's
  availability never returns the other's rows.
- `StartTime >= EndTime` → 400; a preferred window not contained in the available window → 400;
  `EffectiveTo < EffectiveFrom` → 400.
- Availability against an inactive / ended `AccountContactLink` cannot be created as `active` → 400.
- Overlapping active windows for the same `(link, weekday)` → controlled **409** (conflict policy §20).
- Re-posting an identical availability row is **idempotent** (no duplicate).
- A date-specific exception **wins** over the weekly pattern for that date.
- Missing availability is **not** "unavailable": consumers receive *no rows*, and MOD-0151 FU09A reports `unknown`
  (never `contact_not_available_on_day`).
- Hard delete does not exist — only `inactive` / `archived`.
- Cross-tenant contact / account / link references → 404 (never a silent empty write).

## 16. Risks / Decisions Needed

| ID | Decision | Options | Recommended | Impact |
|---|---|---|---|---|
| D1 | Contact↔Account cardinality | embed-in-account · **M:N link** | **M:N `AccountContactLink`** | contact reuse across accounts |
| D2 | Primary contact uniqueness scope | per-Account · **per-(Account,Role)** | **per-(Account,Role)** | validation + index |
| D3 | Relationship directionality | store both rows · **directional + inverse display** | **directional + inverse-label reference** | 1 row, inverse rendered |
| D4 | Self-link | forbid · allow-by-type | **forbid unless type self-allowed** | validation |
| D5 | Duplicate relationship | allow · **unique active** | **unique active → 409** | index |
| D6 | Service path | new service · **reuse Diten.CrmService** | **reuse** | one CRM Core bounded context |
| D7 | Consent seam if MOD-0164 absent | block · **read-only seam no-op** | **read-only seam (SetMissing tolerated)** | no hard dependency on MOD-0164 |
| **D8** | Availability key (§20) | flat field on `Contact` · **`AccountContactLink`-scoped** | **`AccountContactLink`-scoped** | same doctor, different hours per hospital/clinic/pharmacy |
| **D9** | One contact, many accounts (§20) | single schedule · **schedule per link** | **schedule per link** | every link may carry its own availability |
| **D10** | Visit preference placement (§20) | contact-level only · **link-scoped (+ optional contact default)** | **link-scoped, contact-level optional fallback** | route planning always reads it in link context |
| **D11** | Preferred window mandatory? (§20) | required · **optional** | **optional** — absent ⇒ use the available window | avoids fabricated preferences |
| **D12** | Date exception vs weekly pattern (§20) | weekly wins · **exception wins** | **exception wins** | leave / congress / surgery day must override |
| **D13** | Avoid window semantics (§20) | inverse of preferred · **stronger constraint inside the available window** | **stronger constraint** | "do not visit between 12:00–13:00" is a hard signal, not a preference |
| **D14** | `AppointmentRequired` effect (§20) | drops the candidate · **warning / reason only** | **warning / reason only** | MOD-0155 may later bind it to an appointment flow |
| **D15** | Missing availability (§20) | treat as unavailable · **`unknown`** | **`unknown`** | absence of data must never silently shrink field coverage (MOD-0151 R11) |

## 17. Out-of-Scope Guard

| Forbidden item | Status |
|---|---|
| Runtime code / controller / entity / frontend / migration / seed / gateway route / permission seed in this task | Not produced (pack prep only) |
| Consent policy engine / capture workflow | Deferred to MOD-0164 |
| Territory / Zone / MicroZone / SalesRep / Visit / Route | Out of scope (MOD-0151 / later) |
| Campaign / Segment / Lead / Opportunity / Product | Out of scope |
| Account master pollution with Contact fields | Forbidden |
| CRM local reference seed / hardcoded fallback | Forbidden |
| `crm.contact.360.read` (digit-leading segment) | Forbidden → use `crm.contact.overview.read` |
| **Availability as a flat field on `Contact`** (§20) | **Forbidden** → `AccountContactLink`-scoped only (D8) |
| **Route optimization / daily route / visit plan / visit execution / check-in-out / GPS / visit report** (§20) | **Forbidden** → MOD-0155 |
| **Visit frequency / call-cycle policy or engine** (§20) | **Forbidden** → produced by MOD-0165 / MOD-0167, consumed by MOD-0155 |
| **`ContactTerritoryAssignment` / territory assignment writes** (§20) | **Forbidden** → MOD-0151 owns coverage; contact coverage stays derived |
| **Hard delete of availability / exceptions** (§20) | **Forbidden** → `inactive` / `archived` only |
| **Patient / clinical data** (§20) | **Forbidden** — out of CRM scope |

## 18. Ready-for-dev Checklist (Pack Review / Approval Gate)

- [ ] Decisions D1–D7 confirmed by reviewer.
- [ ] MOD-0164 consent/preference read contract shape agreed (or seam-no-op accepted for now).
- [ ] MOD-0048 required sets (contact-type/status/role, account-relationship-type/status) authoring template prepared (separate prereq task, like MOD-0149).
- [ ] Permission keys accepted (catalog→auth sync verified at seed time — implementation task).
- [ ] `form_field_count` (Contact ≈ 14) → Golden Reference Compact confirmed.
- [ ] Status flips `content-ready` → `ready-for-dev` only via the approval gate.

## 19. Follow-up Items

- Relationship **graph** visualization (phase 2; list/table first).
- Contact merge/dedup (later).
- Territory-scoped contact visibility (aligns with MOD-0149 FU15 territory scoping).
- Full consent binding once MOD-0164 lands (FU05 upgrade).
- **`MOD-0150-FU-RBAC — Contact Availability Permission Catalog Alignment`** (§11 / §20) — add
  `crm.contact.availability.read` / `.manage` to the permission catalog + grants. Until then the §20 implementation
  uses the documented temporary fallback and seeds nothing.
- **MOD-0048 authoring template extension** — `contact-availability-type`, `contact-availability-status`,
  `availability-exception-reason` (§10). Operator publish is a runtime prerequisite for §20 create validation, not a
  code blocker.
- **`VisitFrequencyPolicy` / `CallCyclePolicy` ownership** is **not** MOD-0150's — produced by MOD-0165 / MOD-0167,
  consumed by MOD-0155 (recorded in MOD-0151 §22.6 / F21). Listed here only so it is not accidentally pulled into
  the contact master.
- **Timezone / multi-country wall-clock handling** for availability windows (§9.4) — deferred; today times are local
  to the account location and MOD-0150 owns no timezone master.
- **Frontmatter scope-string drift (governance):** `runtime_code_scope` still carries the original FU01-only
  parenthetical although FU01–FU06 shipped (Closeout PASS 2026-07-20). This authorization deliberately **appended**
  the new scope instead of rewriting history; a separate reconciliation task should refresh the FU01 clause and the
  `APPROVED-PENDING-PREREQ` banner.

---

## 20. FU — Contact Availability & Visit Preference (authorized scope, 2026-08-01)

**Why this exists.** MOD-0151 FU09A established that route readiness cannot answer *"can this doctor be visited at
this location, on this day, in this window?"* without a real master for availability — and that the master cannot be
a flat field on `Contact`, because one contact works at several accounts with **different** days and hours. The
correct key is `AccountContactLink` (D1 M:N → D8). MOD-0151 consumes this **read-only**; MOD-0155 consumes it to
build plans. This FU authorizes the **master data**, nothing downstream of it.

**Core architectural rule:** *availability answers "when is this person visitable **here**" — it never answers "who
should be visited today, in what order".* The moment a scoring, sequencing or cadence rule appears in MOD-0150, the
boundary is broken.

### 20.1 Allowed scope

1. **`ContactAvailability`** (§9.4) — `AccountContactLink`-scoped weekly windows, effective-dated, status-managed.
2. **`VisitPreference`** (§9.5) — preferred / avoid window, appointment requirement + lead time, average visit
   duration, preferred contact method; link-scoped, with an optional contact-level default.
3. **`ContactAvailabilityException`** (§9.6) — date-specific overrides (leave, congress, surgery, temporary
   relocation).
4. **Read APIs** (§20.4) — by contact, by link, by account, and a date/weekday lookup for readiness consumers.
5. **Write APIs** (§20.5) — create / update / deactivate / archive for availability and exceptions. **No hard delete.**
6. **Minimal Compact UI** (§13 items 8–10) with 7-language RESX parity.
7. Reference validation against the three new MOD-0048 sets (§10), contract flags (§20.8), backend/frontend tests,
   Gateway-only authenticated smoke and an implementation evidence report.

### 20.2 Data model policy

| Decision | Result |
|---|---|
| Owning key | **`AccountContactLinkId`** (D8). `ContactId` / `AccountId` are derived copies, never independently supplied |
| Flat field on `Contact`? | **Never** (D8) — it would collapse multi-location doctors into one wrong schedule |
| One contact, many accounts | **Independent schedule per link** (D9); reads are link-isolated |
| Preference placement | Link-scoped inside availability; contact-level default **optional fallback** (D10) |
| Preferred window mandatory? | **No** (D11) — absent ⇒ the available window is used; no fabricated preference |
| Avoid window meaning | A **stronger constraint** inside the available window, not the inverse of preferred (D13) |
| Weekly vs date-specific | **Exception wins** for its date (D12) |
| Deletion | **Hard delete forbidden** — `inactive` / `archived` only |
| Time semantics | Local wall-clock at the account location; no timezone master here (§9.4) |
| Values | MOD-0048-driven (`contact-availability-type`, `contact-availability-status`, `availability-exception-reason`); **no hardcoded fallback** |

### 20.3 Validation policy

| Rule | Behaviour |
|---|---|
| `StartTime` < `EndTime` | Violation → **400** |
| Preferred window ⊆ available window | Violation → **400** |
| Avoid window | May overlap the available window (that is its purpose); interpreted as the stronger constraint |
| `EffectiveFrom` / `EffectiveTo` | `EffectiveTo < EffectiveFrom` → **400** |
| Link state | Availability cannot be created `active` against an inactive / ended `AccountContactLink` → **400** |
| Tenant isolation | Cross-tenant `Contact` / `Account` / `AccountContactLink` → **404**; tenant comes from the JWT claim, never from the payload |
| Overlap conflict | Overlapping **active** windows for the same `(AccountContactLinkId, Weekday)` → controlled **409** with both row identities reported; no silent merge, no silent overwrite |
| Idempotency | An identical row (same link + weekday + window + type + effective range) is a **no-op**, not a duplicate |
| Exception uniqueness | One active exception per `(link, Date)`; a second one → **409** (update the existing row instead) |
| Reference sets unpublished | **Fail-closed** controlled 400 (MOD-0149 / MOD-0150 parity) |
| Delete attempt | No delete endpoint exists; a delete-shaped request → controlled `unsupported_operation` |

### 20.4 Read API surface (proposal — routes are `integration-agent` territory)

| Endpoint | Purpose | Permission |
|---|---|---|
| `GET /api/crm/contacts/{contactId}/availability` | All availability across the contact's links, grouped by account | `crm.contact.availability.read` |
| `GET /api/crm/account-contact-links/{linkId}/availability` | The location-scoped schedule + preference + exceptions | `crm.contact.availability.read` |
| `GET /api/crm/accounts/{accountId}/contact-availability` | Every contact's availability at one account/location | `crm.contact.availability.read` |
| `GET /api/crm/contact-availability/lookup?date=…&accountId=…&contactId=…` | Readiness lookup: effective window for a concrete date (weekly pattern **with** exceptions applied) | `crm.contact.availability.read` |

The lookup endpoint is the **MOD-0151 FU09A / MOD-0155 consumption seam**. It returns *rows or nothing* — it never
returns a verdict, a score, an ordering or a route.

### 20.5 Write API surface (proposal)

`POST` / `PUT` availability · `POST` deactivate · `POST` archive · `POST` / `PUT` exception · `POST` archive
exception — all under `crm.contact.availability.manage`.

**Guards:** no hard delete · `Contact` master is **not** mutated (availability lives in its own aggregate) ·
`Account` master is **not** mutated · no territory assignment is written · no route/visit plan is written ·
tenant from claim only.

### 20.6 MOD-0151 integration boundary

```
MOD-0150 = ContactAvailability master
MOD-0151 = read-only consumer inside route readiness (FU09A)
MOD-0155 = consumer that builds visit / route plans
```

- MOD-0151 **must not** copy availability data into its own store and **must not** open a `ContactAvailability`
  aggregate (MOD-0151 §22.6 boundary).
- When availability is absent, MOD-0151 returns `AvailabilityStatus=unknown` — **not** `contact_not_available_on_day`
  (D15; MOD-0151 R11).
- `AppointmentRequired` produces a reason/warning on the candidate row; it does **not** drop the candidate (D14).
- Contact territory coverage stays **derived** (`Contact → AccountContactLink → Account → current coverage`);
  this FU adds no territory field and no `ContactTerritoryAssignment`.

### 20.7 MOD-0155 integration boundary

MOD-0155 will consume availability for: visit candidate eligibility · available time window · preferred window ·
avoid window · appointment requirement (+ lead time) · average visit duration · date-specific exceptions.
**MOD-0150 produces none of the plan:** no sequencing, no travel time, no daily plan, no cadence compliance, no visit
record. Frequency / call-cycle policy remains MOD-0165 / MOD-0167 → MOD-0155.

### 20.8 Contract flags

```json
{
  "supportsContactAvailability": true,
  "supportsAccountContactLinkAvailability": true,
  "supportsVisitPreference": true,
  "supportsAvailabilityExceptions": true
}
```

These flags mean **"availability/preference master data is supported"**. They do **not** imply visit planning, route
planning or frequency support, and no `supportsVisitPlanning` / `supportsRoutePlanning` / `supportsVisitFrequency`
flag is introduced here.

### 20.9 Test expectations

- **Unit:** link-scoped creation (contact-only write path returns 400 / does not exist); two links → two isolated
  schedules; `StartTime>=EndTime`; preferred ⊄ available; `EffectiveTo<EffectiveFrom`; inactive link → 400;
  same-link+weekday overlap → 409; identical row idempotent; exception overrides weekly pattern; second active
  exception for the same date → 409; unpublished reference set → fail-closed 400; cross-tenant → 404;
  deactivate/archive path works and **no delete path compiles**.
- **Guard:** `Contact` aggregate carries **no** availability field; no route / visit / plan / frequency / cadence /
  GPS type exists in MOD-0150; no territory or `ContactTerritoryAssignment` write; `Account` master untouched;
  no permission seed/grant; no `TenantId` in request payloads; frontend never calls 5061 directly.
- **Frontend:** Availability tab + link panel render, Compact verifier, DataTable v2 contract, 7-language RESX parity,
  no route/visit/GPS action anywhere on the surface.
- **Authenticated Gateway-only smoke:** link A gets Mon 09:00–13:00 + Wed 14:00–17:00 with preferred 10:00–12:00 and
  `AppointmentRequired=true` → link B for the same contact gets a different schedule → both read back isolated →
  overlapping window on link A → 409 → date exception (`2026-09-12`, `IsAvailable=false`, congress) → lookup for that
  date returns "not available", lookup for the following Monday returns the weekly window → archive → row leaves the
  active list but stays readable → `Contact` and `Account` masters unchanged.

### 20.10 Explicit exclusions

Route optimization algorithm · daily route building · visit plan creation · visit execution · check-in / check-out ·
GPS validation · visit report · digital detailing · survey · campaign engine · frequency / call-cycle engine ·
territory assignment · `ContactTerritoryAssignment` · Account master mutation · territory model mutation · workflow
approval · ChangeRequest · MOD-0023 integration · evidence pack · new import/export scope · Brand/Product master ·
patient data · hard delete · Mongo hand-edit · RBAC seed/grant (unless separately authorized) · MOD-0048 publish
(unless separately authorized) · `TenantId` in request payloads · direct port 5061 business API calls.
