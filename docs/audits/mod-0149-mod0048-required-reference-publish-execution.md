# MOD-0149 — MOD-0048 Required Reference Publish, Authenticated Steward Execution (Attempt)

**Date:** 2026-07-14 · **Type:** governance publish execution attempt · **Verdict:** PARTIAL (not executed — steward credential not present in task input; not faked)

## Outcome (honest)

The publish of `account-type` / `account-status` via the PSS-012 governance API was **not executed** because an authenticated steward token could not be obtained:

- The task references user-provided login credentials, but **no password value is present in the task input** — only `TenantId 97c5…cc93` and usernames `bestepullukcu@gmail.com` / `admin@diten.com` (credentials **masked**; only the bcrypt hash of `admin@diten.com` is seeded, plaintext unknown).
- The **entire fleet is down** (ports 5000/5056/5057/5061 all closed) — no Auth to log in against, no Platform to call.
- Per policy I did **not**: guess/crack the password, hand-write the sets into Mongo, mint or fake a token, or fabricate a success. No CRM local seed / fallback created.

## Confirmed ready for execution (once a real steward token is available)

- Governance endpoints verified present in `BusinessReferenceDataController` (`api/v1/reference-data`): `POST /sets`, `POST /sets/{id}/versions`, `PUT /versions/{id}/values`, `POST /versions/{id}/validate|submit|approve|publish`, and the consumer **`GET /sets/{setCode}/published-values`** (line 337 — matches the CRM seam path exactly).
- Auth requirement: `Platform.BusinessReferenceData.*` (SuperAdmin full catalog).
- Value payloads (kebab `valueCode`, `isDeprecated:false`) + exact API sequence: see `docs/audits/mod-0149-mod0048-required-reference-publish-closeout.md` and `mod-0149-crm-reference-data-authoring-template.json`.
- Blocker B (Auth grant) already live: `crm.account.*` = 9/9 catalog + 9/9 role grant.

## Live state (evidence)

`diten_personalization_dev.business_reference_data_sets` — `account-type` = **0**, `account-status` = **0** (still absent).

## Execution recipe (for an operator with a steward session)

1. Start fleet (or use running env): Auth 5056, Platform 5057, Gateway 5000.
2. `POST {gateway}/api/auth/login` with the steward credential (masked) + `X-Tenant-Id` → capture Bearer token (do **not** persist/commit/log it).
3. Confirm token carries `Platform.BusinessReferenceData.Create/Version.*/Publish`.
4. For `account-type` then `account-status`: create set → create version → `PUT values` → validate → submit → approve → publish (approve/publish may need a second actor per segregation-of-duties).
5. Verify: `GET {gateway}/api/v1/reference-data/sets/account-type/published-values` → 9 values; `.../account-status/published-values` → 5 values; DB `Status=1`, `PublishedVersionId` set, `IsDeprecated=false`.
6. Then CRM create smoke: `POST {gateway}/api/crm/accounts {AccountName:"Smoke Test Account", AccountType:"organization", Status:"active", AccountCode:null}` → 201 + `ACC-{YYYY}-{seq}`.

## Security / secret handling

No plaintext password, token, or JWT in any changed file or this report (only a `<steward JWT>` placeholder). No token persisted/committed. No Mongo hand-edit.

## Verdict: PARTIAL

Publish not performed (credential absent + fleet down); not faked. Single remaining hard blocker for MOD-0149 frontend + live create smoke.
