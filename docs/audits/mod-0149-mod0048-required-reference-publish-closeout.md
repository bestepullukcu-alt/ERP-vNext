# MOD-0149 — MOD-0048 Required Reference Publish Closeout (account-type / account-status)

**Date:** 2026-07-14 · **Type:** publish-readiness verification + operator handoff (no publish performed; no Mongo hand-edit; no fake) · **Verdict:** PARTIAL (blocker A OPEN — operator governance action)

## Preflight — PASS

- MOD-0149 `ready-for-dev`, `account-foundation-only` ✓ · blocker B (Auth grant) CLOSED in prior closeout ✓
- Governance flow available in `Diten.Platform` `BusinessReferenceDataController` (route `api/v1/reference-data`): `POST /sets`, `POST /sets/{setId}/versions`, `PUT /versions/{versionId}/values`, `POST /versions/{versionId}/validate|submit|approve|publish` — each gated by `[HasPermission("Platform.BusinessReferenceData.*")]` (SuperAdmin has full catalog).
- Live state (definitive): `diten_personalization_dev.business_reference_data_sets` — `account-type` = **0 docs**, `account-status` = **0 docs**.

## Why not performed here (honest)

Publishing must go through the **real PSS-012 governance flow** (author→validate→submit→approve→publish); the task and standards forbid Mongo hand-edit / code seed / fake success. Driving that flow via API needs an **authenticated platform steward token** — only the bcrypt hash of the seeded `admin@diten.com` SuperAdmin exists; the plaintext is unknown, and cracking/guessing it, hand-writing the sets into Mongo, or minting a fake token are all out of bounds. Platform (5057) is also not running. Therefore blocker A remains an **operator/governance-UI action**, handed off with an exact, executable checklist below.

## Published Set Verification (current)

| SetCode | Published? | Expected Values | Actual Values | IsDeprecated Check | SetCode Match | Result |
|---|---|---|---|---|---|---|
| `account-type` | **No (set absent)** | 9 | 0 | n/a | expects lowercase kebab (CRM seam) | **BLOCKER** |
| `account-status` | **No (set absent)** | 5 | 0 | n/a | expects lowercase kebab | **BLOCKER** |

## Operator Checklist (governance UI, or API by a `Platform.BusinessReferenceData.*` steward)

Base path (via Gateway 5000): `/api/v1/reference-data`. All calls need `Authorization: Bearer <steward JWT>` + `X-Tenant-Id: <tenant GUID>`.

**account-type** (`SetCode: account-type`, DisplayName "Account Type", tenant scope):
1. `POST /sets` → `{ setCode: "account-type", name: "Account Type", scopeType: <tenant> }` → capture `setId`.
2. `POST /sets/{setId}/versions` → capture `versionId`.
3. `PUT /versions/{versionId}/values` → values (all `isDeprecated:false`, kebab `valueCode`): organization, hospital, pharmacy, clinic, distributor, wholesaler, corporate-group, branch, other (DisplayName = title-case).
4. `POST /versions/{versionId}/validate` → `submit` → `approve` → `publish` (approve/publish may require a distinct actor per segregation-of-duties).

**account-status** (`SetCode: account-status`, DisplayName "Account Status"):
1–4 as above with values: draft, active, inactive, suspended, archived.

5. 7-language DisplayName (en, fr, es, zh, ar, ru, tr) — if not filled, missing l10n is a **non-blocking** note (en suffices for functional validation).
6. Value payloads / DisplayNames ready in `docs/audits/mod-0149-crm-reference-data-authoring-template.json`.

> **SetCode convention:** use **lowercase kebab** exactly (`account-type`, `account-status`). Existing live sets are UPPER_SNAKE (`COUNTRY_CODES`); do NOT create UPPER_SNAKE variants — the CRM consumer seam queries the exact lowercase codes.

## Post-publish verification commands

```
# live DB (evidence)
mongoexport --db diten_personalization_dev --collection business_reference_data_sets \
  --query '{"SetCode":{"$in":["account-type","account-status"]}}' --fields SetCode,Status,PublishedVersionId
# expect: 2 rows, Status=1 (Published), PublishedVersionId set

# consumer read (through the seam CRM uses)
GET {gateway}/api/v1/reference-data/sets/account-type/published-values    # expect 9 values, isDeprecated=false
GET {gateway}/api/v1/reference-data/sets/account-status/published-values  # expect 5 values
```

## Backend / Gateway Create Smoke — NOT run

Gated on blocker A. Once published: authenticated CRM Admin (grant is live, blocker B closed) → `POST {gateway}/api/crm/accounts` `{AccountName:"Smoke Test Account", AccountType:"organization", Status:"active", AccountCode:null}` → expect 201 + `ACC-{YYYY}-{seq}`; then list/overview; 409 dup / 400 invalid type / 404 cross-tenant. Unit level already 13/13.

## Validation Commands (this task)

| Command | Result |
|---|---|
| build CrmService.Api | ✅ 0 error |
| test CrmService.Application.Tests | ✅ 13 passed |
| build ApiGateway | ✅ 0 error |
| live: account-type set | ❌ 0 docs |
| live: account-status set | ❌ 0 docs |
| guards (fallback/local-seed/Zone/360/5061) | ✅ all 0 |

## Changed Files

| File | Change |
|---|---|
| docs/audits/mod-0149-mod0048-required-reference-publish-closeout.md | Created |
| execution/registries/module-implementation-status.md | Blocker A still open (publish handoff) |

No Mongo hand-edit, no CRM local seed, no fake token/success, no MOD-0048/frontend/AuthService code change.

## Verdict: PARTIAL

`account-type`/`account-status` **not published** (operator governance action, evidence-based; not faked). Auth grant (blocker B) already live. Frontend + live create smoke remain gated on this single publish step.
