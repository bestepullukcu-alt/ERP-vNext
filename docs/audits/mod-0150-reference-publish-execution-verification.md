# MOD-0150 — Reference Publish Execution / Verification — MOD-0048 Required Sets

**Date:** 2026-07-17 · **Verdict:** **PARTIAL** — all 5 required sets created, valued (with metadata) and **submitted**; approve + publish are blocked by the MOD-0048 **SoD rule** (submitter ≠ approver) and no second-approver credential is available. No Mongo hand-edit, no fake publish, no runtime code touched.

## Scope

Publish the 5 MOD-0150 required reference sets via MOD-0048/PSS-012 governance under the CRM tenant, then verify with
published-values smoke. Docs-only allowed changes (this audit + registry). Credentials as runtime input only, masked,
never persisted.

## Tenant / credential (masked)

- Tenant: `97c59330-…-cc93` (CRM tenant).
- Submitter: `bestepullukcu@…` / `********` (tenant_user, 97c5) — the only available tenant credential.
- No `STEWARD_*` / `APPROVER_*` / second-approver env credential present.
- Health: Auth 5056 / Platform 5057 / Gateway 5000 / CRM 5061 = 200.

## Governance execution (per set: create → version → values → validate → submit)

| SetCode | Existing state | Action | values | validate | submit | GovState / Approval | Result |
|---|---|---|---|---|---|---|---|
| `contact-type` | absent (500) | created + submitted | 200 (9 vals) | 200 | 200 | Submitted / Pending | ⏳ pending approve |
| `contact-status` | absent | created + submitted | 200 (4) | 200 | 200 | Submitted / Pending | ⏳ |
| `contact-role` | absent | created + submitted | 200 (7) | 200 | 200 | Submitted / Pending | ⏳ |
| `account-relationship-type` | absent | created + submitted (+metadata) | 200 (6) | 200 | 200 | Submitted / Pending | ⏳ |
| `account-relationship-status` | absent | created + submitted | 200 (4) | 200 | 200 | Submitted / Pending | ⏳ |

All `set_code` / value codes are **lowercase-kebab**, `is_active=true` (isDeprecated=false), scope_type=tenant. No
duplicate sets created (all were absent). No UPPER_SNAKE alias.

### SoD block (expected)
`POST …/versions/{id}/approve` as the submitter → **`sod_submitter_cannot_approve`** (HTTP 400). This is correct
governance: the submitter cannot self-approve. A **second 97c5 approver** must approve (the same two-user flow used for
MOD-0149's sets — e.g. user `d27fa4a6…` submitted MOD-0149 type/status while `bestepullukcu` approved/published).

## Handoff — approve + publish (needs a second 97c5 approver)

For each version below: **approve** as a 97c5 user ≠ submitter, then **publish** (publish is not SoD-gated; `bestepullukcu`
can publish once approved). Then re-run the published-values smoke.

| SetCode | setId | versionId (Submitted/Pending) |
|---|---|---|
| contact-type | `253268a2-4f9f-49fa-822f-11b8d5e3b22e` | `f6aa1b89-925e-416d-8485-068a32dafdbc` |
| contact-status | `d1b3ab48-8f32-4358-9289-f342deeacd1e` | `26b42e7b-3a5a-4a43-8402-ae95d2b086ec` |
| contact-role | `a77c3a1a-cce1-4123-a006-7b6a22d45adb` | `620b3b92-137e-4e82-a27f-53116cea5547` |
| account-relationship-type | `51c9a245-13d4-44a0-a4fb-6f729e1596eb` | `796689be-c5fa-4eab-84e1-944458f40ab4` |
| account-relationship-status | `44a923bc-8f80-4883-8785-c4df6a792400` | `d0abfd82-d746-4d01-80bb-c24fa6e0d52f` |

## Published-values smoke (current — pre-publish)

| SetCode | Expected | Actual | scope_key | Result |
|---|---|---|---|---|
| contact-type | 200 / 9 | **HTTP 400** (no published version) | 97c5 | ⏳ pending publish |
| contact-status | 200 / 4 | HTTP 400 | 97c5 | ⏳ |
| contact-role | 200 / 7 | HTTP 400 | 97c5 | ⏳ |
| account-relationship-type | 200 / 6 + metadata | HTTP 400 | 97c5 | ⏳ |
| account-relationship-status | 200 / 4 | HTTP 400 | 97c5 | ⏳ |

## Metadata (account-relationship-type — stored on submitted version values)

| ValueCode | direction | inverseLabelCode | selfAllowed | Result |
|---|---|---|---|---|
| associated-with | bidirectional | associated-with | false | ✅ stored (attributes) |
| preferred-pharmacy | directional | preferred-by | false | ✅ |
| refers-to | directional | referred-by | false | ✅ |
| served-by | directional | serves | false | ✅ |
| same-network | bidirectional | same-network | false | ✅ |
| nearby | bidirectional | nearby | false | ✅ |

> Metadata stored in the value `attributes` map (keys `direction` / `inverseLabelCode` / `selfAllowed`, per this task's
> spec). Note: the PREREQ template JSON used the key `relationshipDirection`; this execution used `direction` as the task
> mandated — **align the template key to `direction` in a follow-up** so consumer + template match.

## Validation checks

| Check | Expected | Result |
|---|---|---|
| Auth / Platform / Gateway / CRM health | 200 | ✅ |
| Correct-tenant login (97c5) | 200 | ✅ (masked) |
| Set existence after create | present | ✅ 5/5 |
| Version status | Submitted / Pending | ✅ 5/5 |
| published-values smoke | 200 / counts | ⏳ blocked (unpublished) |
| JSON template counts 9/4/7/6/4 | match | ✅ (submitted value counts) |
| Duplicate ValueCode | none | ✅ |
| lowercase-kebab SetCode + ValueCode | all | ✅ |
| account-relationship-type metadata | direction/inverse/self | ✅ stored |
| isDeprecated=false | all | ✅ |
| No Mongo hand-edit | — | ✅ |
| No runtime code / CrmService / frontend / MOD-0048 runtime change | — | ✅ |
| No plaintext credential in changed files | — | ✅ (grep clean) |

## Verdict: PARTIAL

The five required sets are authored, valued (with metadata) and submitted — the entire single-user portion is done.
**Publish is blocked only by SoD:** a second 97c5 approver must approve the five versions, after which publish + the
published-values smoke (9/4/7/6/4) complete. Either supply a second-approver credential (runtime input) or approve the
five versions in the governance UI as the second user (as was done for MOD-0149's sets), then publish.
