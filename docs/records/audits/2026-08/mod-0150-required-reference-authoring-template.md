# MOD-0150 — Required Reference Set Authoring Template & Operator Checklist

**Date:** 2026-07-17 · **Task:** MOD-0150-PREREQ (authoring template only — no runtime code, no seed, no CrmService/MOD-0048 change) · **Verdict:** PASS

## Purpose

Prepare the MOD-0048 / PSS-012 **authoring template + operator checklist** so the operator can author/publish the
reference sets MOD-0150 Contact & Relationship Management needs — via the same governance flow MOD-0149 used
(create set → version → values → validate → submit → **approve with SoD (2nd approver)** → publish → published-values
smoke). **No CRM local seed, no hardcoded fallback, no Mongo hand-edit, no fake published-values.** Machine template:
[mod-0150-required-reference-authoring-template.json](./mod-0150-required-reference-authoring-template.json).

Scope: **tenant-scoped**, published per tenant with `scope_key={tenantId}` (e.g. the CRM tenant
`97c59330-…` in the current environment — mask/keep as environment-specific input, never hardcode in code).

## Required sets (block FU01 create/update validation until published)

| SetCode | Values | Blocks | Metadata? |
|---|---|---|---|
| `contact-type` | 9 | Contact create/update | No |
| `contact-status` | 4 | Contact create/update | No |
| `contact-role` | 7 | AccountContactLink create/update | No |
| `account-relationship-type` | 6 | AccountRelationship create/update | **Yes** (direction/inverse/self) |
| `account-relationship-status` | 4 | AccountRelationship create/update | No |

### contact-type (9)
`doctor` · `pharmacist` · `responsible-person` · `department-contact` · `decision-maker` · `procurement` · `medical` · `administrative` · `other`

### contact-status (4)
`draft` · `active` · `inactive` · `archived`

### contact-role (7)
`decision-maker` · `procurement` · `medical` · `administrative` · `billing` · `primary` · `other`

### account-relationship-type (6) — with metadata
| ValueCode | DisplayName.en / .tr | relationshipDirection | inverseLabelCode | selfAllowed |
|---|---|---|---|---|
| `associated-with` | Associated With / İlişkili | bidirectional | associated-with | false |
| `preferred-pharmacy` | Preferred Pharmacy / Tercih Edilen Eczane | directional | preferred-by | false |
| `refers-to` | Refers To / Yönlendirir | directional | referred-by | false |
| `served-by` | Served By / Hizmet Alır | directional | serves | false |
| `same-network` | Same Network / Aynı Ağ | bidirectional | same-network | false |
| `nearby` | Nearby / Yakın | bidirectional | nearby | false |

### account-relationship-status (4)
`pending` · `active` · `inactive` · `ended`

## Optional sets (do NOT block FU01)

| SetCode | Purpose | Blocks FU01? | Notes |
|---|---|---|---|
| `professional-title` | contact title | No | field left blank if unpublished |
| `medical-specialty` | contact specialty (starter; extend via import) | No | — |
| `department-type` | contact department | No | — |
| `communication-preference-type` | interaction preference | No | **MOD-0164 seam** — when MOD-0164 lands, preference becomes an authoritative MOD-0164 reference consumed read-only; do not build a consent engine here |

> Optional-set rule: if an optional set is unpublished, the field is simply left blank — **no local fallback**. Only the
> five required sets gate create/update.

## Metadata guidance (account-relationship-type)

- `relationshipDirection` ∈ {`directional`, `bidirectional`}. Symmetric relationships (`associated-with`, `same-network`,
  `nearby`) are `bidirectional` and store one row; asymmetric ones (`preferred-pharmacy`, `refers-to`, `served-by`) are
  `directional`.
- `inverseLabelCode` = the label rendered on the **target** account's 360 (`served-by` shows `serves` on the provider;
  `refers-to` shows `referred-by`). Symmetric types point to themselves.
- `selfAllowed` (default **false**) — whether `Source == Target` is permitted for the type (D4).
- **Degraded mode:** if metadata is omitted, `account-relationship-type` still works for create/validation, but inverse
  display + self-link policy degrade to defaults (treat as directional, no inverse label, selfAllowed=false). The
  template ships the metadata so implementation can consume it.

## Operator checklist

| # | Step | Expected evidence |
|---|---|---|
| 1 | Choose the CRM tenant (environment-specific, e.g. `97c59330-…`) | tenant id confirmed |
| 2 | Create each set (`set_code` snake-case payload, `scope_type=tenant`, `status=Active`) | 201 per set |
| 3 | Create a version per set | 201, versionId |
| 4 | Add values (snake_case: `code`, `label`, `is_active`, `sort_order`; add `metadata` for account-relationship-type) | 200 |
| 5 | Validate | 200 |
| 6 | Submit (submitter = user A) | 200, GovernanceState=Submitted |
| 7 | **Approve with a DIFFERENT user (SoD: approver ≠ submitter)** | 200, ApprovalState=Approved |
| 8 | Publish | 200, publishedVersionId set |
| 9 | Published-values smoke per set: `GET /api/v1/reference-data/sets/{setCode}/published-values?scope_key={tenantId}` | 200 |
| 10 | Verify counts | contact-type **9** · contact-status **4** · contact-role **7** · account-relationship-type **6** · account-relationship-status **4** |
| 11 | Verify `isDeprecated=false` on all values | true |
| 12 | Verify lowercase-kebab SetCode + ValueCode | true |
| 13 | **Do NOT** create UPPER_SNAKE aliases | — |
| 14 | **Do NOT** local-seed in CRM | — |
| 15 | **Do NOT** hand-edit Mongo | — |

> SoD note (from MOD-0149): the submitter cannot self-approve. The environment already has ≥2 tenant users
> (one submitted, another approved MOD-0149's sets) — reuse the same two-user flow.

## Published-values smoke checklist

| SetCode | Expected count | Endpoint |
|---|---|---|
| contact-type | 9 | `…/sets/contact-type/published-values?scope_key={tenantId}` |
| contact-status | 4 | `…/sets/contact-status/published-values?scope_key={tenantId}` |
| contact-role | 7 | `…/sets/contact-role/published-values?scope_key={tenantId}` |
| account-relationship-type | 6 | `…/sets/account-relationship-type/published-values?scope_key={tenantId}` |
| account-relationship-status | 4 | `…/sets/account-relationship-status/published-values?scope_key={tenantId}` |

## Acceptance criteria

- Required sets unpublished → FU01 create/update validation returns a **controlled 400** (not a crash, not a fake pass).
- Contact cannot be created without `contact-type` / `contact-status`.
- AccountContactLink cannot be created without `contact-role`.
- AccountRelationship cannot be created without `account-relationship-type` / `account-relationship-status`.
- `account-relationship-type` works without metadata, but inverse display + self-link policy degrade (flagged above).
- With metadata present, inverse display + `selfAllowed` validation are consumable.
- All required values are lowercase kebab-case; no duplicates; `isDeprecated=false`.
- **No CRM local seed · no hardcoded fallback · no Mongo hand-edit · no fake published-values.**

## Failure conditions (task fails if)

- Any runtime code / CrmService / frontend / MOD-0048 runtime touched.
- Local seed or hardcoded fallback introduced.
- Invalid SetCode/ValueCode (non-kebab, UPPER_SNAKE), duplicate value, or wrong counts.
- account-relationship-type metadata shape invalid.
- Template conflicts with the MOD-0150 pack (D1–D7).

## Validation run (this task)

JSON parse OK · counts 9/4/7/6/4 OK · lowercase-kebab OK · no duplicates · `isDeprecated=false` OK ·
account-relationship-type metadata shape OK (relationshipDirection/inverseLabelCode/selfAllowed, selfAllowed default false) ·
5 required + 4 optional. **No runtime code / CrmService / frontend / MOD-0048 change.**
