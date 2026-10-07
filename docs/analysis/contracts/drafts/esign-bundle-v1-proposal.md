# ESIGN-BUNDLE v1 — proposal (MOD-0022 e-Signature Service)

> **Status: PROPOSAL (draft) — not a contract.** Nothing here is frozen. After CT acceptance, E2 writes it as
> `docs/analysis/contracts/esign.openapi.yaml` (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0022`,
> `x-owner-service: Diten.Platform` + `Diten.AuthService` for §4.1–4.3) in ONE single-writer step, and adds the row to
> `docs/analysis/contracts/README.md`. After the freeze only additive changes are allowed (minor version, README "Freeze
> süreci").
>
> **Source:** Blueprint 8.1 `Blueprint_Data` row 23 col L "ESIGN-BUNDLE (signature envelope schema, signer identity
> binding, audit hooks, evidence link)"; `Contract Bundle Dictionary` row 87. Pack:
> `execution/domains/platform-shared-services/module-packs/MOD-0022-e-signature-service.md` (§8 is the behaviour
> authority; this file is the wire shape).
>
> **Place:** `docs/analysis/contracts/drafts/` follows the precedent `trace-v1.1-proposal.md`; see pack FU-0022-10.

## 1. Rules of this proposal

1. **Every call is made with the user's own tenant token** (`actor_type = tenant_user`) through the Gateway. There is no
   service-key signing. The only S2S call is §4.3 (Platform → AuthService), and it carries no user authority.
2. **Scope = Tenant** for every permission. No path or page is under `/Platform`.
3. `TenantId` always comes from the token, never from a body. Another tenant's id → **404**.
4. Envelope: every response is `Response<T>` (`{ data, isSuccess, statusCode, errors[], errorCode, correlationId }`,
   the platform envelope). Error codes below are the `errorCode` values.
5. Times are UTC ISO-8601 (`…Z`). Hashes are lowercase hex SHA-256.
6. Every write takes `idempotencyKey` (string, 8–100). A replay with the same key returns the first result (200)
   without side effects.
7. Every command writes a MOD-0021 audit event (`SourceModule: "MOD-0022"`). The re-auth endpoints (§4.1–4.3) write
   AuthService `authAuditLogs`.
8. **Never in any payload, log or audit:** the password; the plain ticket (except the one response that issues it); image
   bytes in audit metadata.
9. Additive only towards MOD-0023 (§5): no existing Workflow field, path or status code changes.

## 2. Schemas

### 2.1 `SubjectRef`
```json
{ "module": "inventory", "subjectType": "inventory.quarantine-decision", "subjectId": "string" }
```
`module` `^[a-z][a-z0-9-]{1,40}$`; `subjectType` `^[a-z][a-z0-9.-]{1,80}$`; `subjectId` 1–100.

### 2.2 `SignableSubject` (returned by a subject provider, §6)
```json
{ "display": "Karantina kaydı KRN-2026-0412 · Lot PRS-26A014 · 1.200 kutu Serbest Bırak",
  "summary": [ { "label": "Ürün", "value": "Parasetamol 500 mg Tablet" } ],
  "legalEntityId": "guid|null",
  "fields": { "decision": "release", "quantity": "1200", "lotCode": "PRS-26A014" } }
```
`summary` ≤ 20 rows (label ≤ 60, value ≤ 300); `fields` values are strings or null (≤ 100 keys).

### 2.3 `CanonicalSubject` (hash input — §7)
```
{"module":"…","subjectType":"…","subjectId":"…","fields":{<keys sorted ordinal>}}
```

### 2.4 `Appearance` (request)
```json
{ "type": "none | drawn | sample",
  "sampleId": "guid (type=sample)",
  "drawing": { "pngBase64": "…", "strokes": [[[0.12,0.40],[0.13,0.41]]] } }
```
`pngBase64` decodes to ≤ 1 048 576 bytes and must be a PNG. `strokes` is optional (≤ 256 KB, points 0..1).

### 2.5 `SignatureRecord` (response)
```json
{ "id": "guid", "signatureNumber": "ESIG-2026-004813",
  "subject": { "module": "…", "subjectType": "…", "subjectId": "…" },
  "subjectDisplay": "…", "subjectSummary": [ { "label": "…", "value": "…" } ],
  "legalEntityId": "guid|null", "actionCode": "inventory.quarantine.release|null",
  "context": { "workflowInstanceId": "…", "workflowTaskId": "…", "stepCode": "…", "decision": "approve" },
  "meaningCode": "RELEASE", "meaningText": "Serbest bırakma yetkisi", "meaningLanguage": "tr",
  "signer": { "userId": "…", "login": "ayse.demir", "name": "Ayşe Demir" },
  "signedAtUtc": "2026-10-07T09:14:22Z", "note": "…|null",
  "reauth": { "kind": "password | window", "passwordVerifiedAtUtc": "…" },
  "secondFactor": "none",
  "appearance": { "type": "none | drawn | sample", "sampleId": "guid|null", "imageHash": "hex|null", "hasVector": true },
  "subjectHashAlgorithm": "CanonicalJsonSha256", "subjectHash": "hex",
  "signatureDigest": "hex",
  "requestId": "guid|null",
  "policy": { "scope": "tenant | legalEntity", "legalEntityId": "guid|null", "version": 7, "visualMode": "savedAccepted", "reauthEvery": true, "reauthWindowMinutes": 15 },
  "invalidation": { "reason": "…", "by": "…", "byName": "…", "atUtc": "…" } }
```
`invalidation` is null unless invalidated. The record itself never changes.

### 2.6 `VerificationResult`
```json
{ "signatureId": "guid", "status": "valid | changed | invalidated | tampered | subjectUnresolvable",
  "subjectHashAtSign": "hex", "currentSubjectHash": "hex|null",
  "imageIntact": true, "digestIntact": true, "verifiedAtUtc": "…" }
```
Precedence: `invalidated` > `tampered` (image or digest mismatch) > `subjectUnresolvable` > `changed` > `valid`.

### 2.7 `SignatureRequest`
```json
{ "id": "guid", "requestNumber": "SR-2026-1190", "subject": { }, "subjectDisplay": "…", "legalEntityId": "guid|null",
  "actionCode": "…|null", "meaningCode": "APPROVE",
  "requestedBy": { "userId": "…", "name": "Burak Yıldız" }, "requestedAtUtc": "…",
  "signerUserId": "…", "dueAtUtc": "…",
  "status": "waiting | signed | rejected | expired | cancelled",
  "signatureId": "guid|null", "rejectReason": "…|null", "decidedAtUtc": "…|null" }
```

### 2.8 `Policy`
```json
{ "scope": "tenant | legalEntity", "legalEntityId": "guid|null", "version": 7,
  "rows": [ { "actionCode": "inventory.quarantine.release", "module": "inventory", "actionName": "Serbest Bırak",
              "required": true, "meaningCode": "RELEASE",
              "visualMode": "none | optional | savedAccepted | drawRequired",
              "secondFactor": "notAvailable", "differsFromTenant": false } ],
  "reauthEvery": true, "reauthWindowMinutes": 15,
  "sampleApprovalRequired": true }
```
`sampleApprovalRequired` is only meaningful on the tenant scope (pack CT-D15); on legalEntity it is echoed read-only.

### 2.9 `Meaning`
```json
{ "code": "APPROVE", "names": { "en": "I approve", "tr": "Onaylıyorum", "fr": "J'approuve", "es": "Apruebo",
  "zh": "…", "ar": "أوافق", "ru": "Утверждаю" },
  "description": "…", "isActive": true, "isLocked": true, "usageCount": 3 }
```
All 7 names are required (KS-12). `isLocked` = used by ≥ 1 signature (code immutable).

### 2.10 `Sample`
```json
{ "id": "guid", "sampleNumber": "IMZ-2026-0059", "owner": { "userId": "…", "login": "…", "name": "…" },
  "kind": "drawn | uploaded", "format": "png | pngAndVector | vector",
  "status": "pending | active | inactive | rejected | withdrawn",
  "imageHash": "hex", "createdAtUtc": "…", "usedCount": 14,
  "decision": { "by": "…", "byName": "…", "atUtc": "…", "reason": "…|null" } }
```

### 2.11 `ReauthTicket` (AuthService)
```json
{ "ticket": "base64url (32 random bytes)", "expiresAtUtc": "…", "passwordVerifiedAtUtc": "…",
  "kind": "password | window", "windowExpiresAtUtc": "…|null" }
```

### 2.12 `ExportLogEntry`
```json
{ "id": "guid", "exportNumber": "EXP-2026-0032", "format": "pdf | csv", "recordCount": 46,
  "filter": { "fromUtc": "…", "toUtc": "…", "module": "…|null", "signerUserId": "…|null", "meaningCode": "…|null", "legalEntityId": "…|null" },
  "filterText": "01.09.2026–30.09.2026 · Tüm modüller · Tüm şirketler", "by": { "userId": "…", "name": "…" },
  "atUtc": "…", "fileHash": "hex" }
```

## 3. Permissions (namespace `esign.*`, owner MOD-0022 — PKS-001 §4; all Scope = Tenant)

`esign.signatures.read` · `esign.signatures.sign` (API-only) · `esign.signatures.verify` · `esign.signatures.invalidate` ·
`esign.signatures.export` · `esign.requests.read` · `esign.requests.create` (API-only) · `esign.requests.reject` ·
`esign.my-samples.manage` · `esign.samples.approve` · `esign.samples.reject` · `esign.policy.read` ·
`esign.policy.manage` · `esign.meanings.read` · `esign.meanings.manage`.

The re-auth endpoints (§4.1–4.2) need only an authenticated `tenant_user` token (no permission key). Who may sign a
business decision is decided by the consumer.

## 4. Operations

### 4.1 `POST /api/tenant-auth/esign/reauth` — `esignReauth` (AuthService, via the existing gateway route)
- Auth: `[Authorize]`, `actor_type = tenant_user`. User = `sub`, tenant = `tenant_id`.
- Request: `{ "password": "…", "intent": "sign | sample | invalidate", "openWindowMinutes": 1-60 | null }`
- 200 `ReauthTicket` (kind `password`).
- Errors:
  - 400 validation;
  - 401 `ESIGN_REAUTH_FAILED` (`{ remainingAttempts }`, pack CT-D13);
  - 403 `ESIGN_ACTOR_NOT_TENANT_USER`;
  - 423 `ESIGN_ACCOUNT_LOCKED` (`{ lockoutEndUtc }`). The attempt that **starts** the lockout already answers 423 (`remainingAttempts = 0`), not 401 (measured in E1, CT 2026-10-08).
- Lockout: the tenant login settings `MaxFailedLoginAttempts` / `LockoutDurationMinutes`, on the same user counter as
  login.
- Idempotency: none (each call is an authentication attempt).
- Audit (`authAuditLogs`): `tenant_esign_reauth_succeeded` | `_password_failed` | `_blocked_by_lockout` |
  `_lockout_started`. Metadata `{intent, window}` only.
- Side effects: no access token, no refresh token, no session revocation.

### 4.2 `POST /api/tenant-auth/esign/ticket` — `esignWindowTicket` (AuthService)
- Auth as 4.1. Request `{ "intent": "sign" }` (only `sign` is allowed).
- 200 `ReauthTicket` (kind `window`, `passwordVerifiedAtUtc` = window start).
- Errors:
  - 409 `ESIGN_REAUTH_WINDOW_EXPIRED` (no open window, revoked, or older than 60 min);
  - 400 `ESIGN_INTENT_REQUIRES_PASSWORD` (intent `sample` / `invalidate`);
  - 403 `ESIGN_ACTOR_NOT_TENANT_USER`.
- Audit `tenant_esign_window_ticket_issued`.

### 4.3 `POST /internal/esign-tickets/consume` — `esignConsumeTicket` (AuthService, S2S, **not routed by the gateway**)
- Auth: `X-Internal-Api-Key`.
- Request `{ "ticket": "…", "tenantId": "guid", "userId": "…", "intent": "sign | sample | invalidate" }`
- 200 `{ "kind": "password | window", "passwordVerifiedAtUtc": "…", "issuedAtUtc": "…" }`
- Error: 409 `ESIGN_TICKET_INVALID`. One code for unknown / expired / consumed / user / tenant / intent mismatch.
- Atomic single use: `FindOneAndUpdate` with `ConsumedAtUtc == null && ExpiresAtUtc > now` in the filter.
- Audit `tenant_esign_ticket_consumed` / `tenant_esign_ticket_rejected`.

### 4.4 `POST /api/v1/esign/signatures` — `signSubject`
- Key `esign.signatures.sign`.
- Request:
  ```json
  { "ticket": "…", "subject": { "module": "…", "subjectType": "…", "subjectId": "…" },
    "actionCode": "…|null", "meaningCode": "RELEASE", "note": "…|null",
    "appearance": { "type": "sample", "sampleId": "…" },
    "requestId": "guid|null", "context": { "workflowTaskId": "…", "decision": "approve" },
    "idempotencyKey": "…" }
  ```
- 201 `SignatureRecord` (200 on idempotent replay).
- Errors:
  - 400: `ESIGN_TICKET_REQUIRED`, `ESIGN_DRAWING_EMPTY`, validation.
  - 403: `ESIGN_TICKET_INVALID`, `ESIGN_REAUTH_REQUIRED`, `ESIGN_SIGNER_NOT_NOMINATED`.
  - 404: `ESIGN_SUBJECT_NOT_FOUND`.
  - 409: `ESIGN_MEANING_NOT_ALLOWED`, `ESIGN_APPEARANCE_NOT_ALLOWED`, `ESIGN_SAMPLE_NOT_ACTIVE`, `ESIGN_REQUEST_NOT_OPEN`.
  - 413: `ESIGN_IMAGE_TOO_LARGE`.
  - 415: `ESIGN_IMAGE_NOT_PNG`.
  - 501: `ESIGN_SECOND_FACTOR_NOT_AVAILABLE`.
  - 503: `ESIGN_REAUTH_UNAVAILABLE`.
- Server order: pack §8 E2 steps 1–8. The ticket is consumed only after the subject, policy and appearance checks.
- Audit `esign.signature.signed`.

### 4.5 `GET /api/v1/esign/signatures/{id}` — `getSignature`
- Key `esign.signatures.read`. 200 `SignatureRecord`.
- 404 when missing, in another tenant, or the subject is not visible to the caller.

### 4.6 `GET /api/v1/esign/signatures/by-subject?module=&subjectType=&subjectId=` — `listSignaturesBySubject`
- Key `esign.signatures.read`. 200 `SignatureRecord[]` (signedAt ascending).
- 404 `ESIGN_SUBJECT_NOT_FOUND` if the subject is invisible.
- During E6 it may include FU23 records mapped to the same shape with `source: "fu23"` (additive field).

### 4.7 `POST /api/v1/esign/signatures/{id}/verify` — `verifySignature`
- Key `esign.signatures.verify`. Request: none.
- 200 `VerificationResult`. 404.
- No state change. Audit `esign.signature.verified` (status).

### 4.8 `POST /api/v1/esign/signatures/{id}/invalidate` — `invalidateSignature`
- Key `esign.signatures.invalidate`.
- Request `{ "ticket": "…", "reason": "≥10 chars", "idempotencyKey": "…" }`
- 201 `SignatureRecord` (with `invalidation`).
- Errors: 400 `ESIGN_REASON_TOO_SHORT`, 403 `ESIGN_TICKET_INVALID` / `ESIGN_REAUTH_REQUIRED`, 404,
  409 `ESIGN_ALREADY_INVALIDATED`.
- Audit `esign.signature.invalidated` (critical category).

### 4.9 `POST /api/v1/esign/requests` — `createSignatureRequest`
- Key `esign.requests.create` (API-only; called by consumers with the user's token).
- Request `{ "subject": {…}, "actionCode": "…|null", "meaningCode": "…", "signerUserId": "…", "dueAtUtc": "…", "note": "…|null", "idempotencyKey": "…" }`
- 201 `SignatureRequest`.
- Errors: 400 (due in the past), 404 `ESIGN_SUBJECT_NOT_FOUND`, 409 `ESIGN_MEANING_NOT_ALLOWED`,
  404 `ESIGN_SIGNER_NOT_FOUND` (user not in tenant).
- Audit `esign.request.created`.

### 4.10 `GET /api/v1/esign/requests?scope=mine&module=&status=&fromUtc=&toUtc=&page=&pageSize=` — `listSignatureRequests`
- Key `esign.requests.read`. `scope=mine` (default) = `signerUserId == me`.
- 200 paged `SignatureRequest[]` + `{ waitingCount }`.

### 4.11 `GET /api/v1/esign/requests/{id}` — `getSignatureRequest`
- Key `esign.requests.read`. Only the signer or the requester may read it, else 404.

### 4.12 `POST /api/v1/esign/requests/{id}/reject` — `rejectSignatureRequest`
- Key `esign.requests.reject`. Request `{ "reason": "≥10", "idempotencyKey": "…" }`. 200 `SignatureRequest`.
- Errors: 403 `ESIGN_SIGNER_NOT_NOMINATED`, 409 `ESIGN_REQUEST_NOT_OPEN`, 400 `ESIGN_REASON_TOO_SHORT`.
- Audit `esign.request.rejected`. The requester is notified (MOD-0027 event catalog, follow-up).

### 4.13 `POST /api/v1/esign/requests/{id}/cancel` — `cancelSignatureRequest`
- Key `esign.requests.create`. Only the requester. Request `{ "reason": "≥10" }`. 200; 409 `ESIGN_REQUEST_NOT_OPEN`.
- Expiry is system-only (Hangfire sweeper, audit `esign.request.expired`).

### 4.14 `GET /api/v1/esign/policy?legalEntityId=` — `getPolicy`
- Key `esign.policy.read`.
- 200 `{ "tenant": Policy, "override": Policy|null, "effective": Policy, "legalEntities": [ { "id", "name", "hasOverride" } ] }`

### 4.15 `GET /api/v1/esign/policy/effective?actionCode=&module=&subjectType=&subjectId=` — `getEffectivePolicy`
- Key `esign.signatures.sign`. Used by the dialog.
- 200:
  ```json
  { "required": true, "meaningCode": "RELEASE", "visualMode": "savedAccepted", "reauthEvery": true,
    "reauthWindowMinutes": 15, "allowedMeanings": [ Meaning ], "subject": SignableSubject,
    "mySample": Sample|null, "myPendingSample": Sample|null, "signer": { "login": "…", "name": "…" } }
  ```
- 404 `ESIGN_SUBJECT_NOT_FOUND`.

### 4.16 `PUT /api/v1/esign/policy/tenant` · `PUT /api/v1/esign/policy/legal-entities/{legalEntityId}` · `POST /api/v1/esign/policy/legal-entities/{legalEntityId}/revert` — `savePolicy` / `saveLegalEntityPolicy` / `revertLegalEntityPolicy`
- Key `esign.policy.manage`.
- Request (PUT): `{ "rows": [ { "actionCode", "required", "meaningCode", "visualMode" } ], "reauthEvery": bool, "reauthWindowMinutes": 1-60, "sampleApprovalRequired": bool (tenant only), "reason": "≥10", "expectedVersion": int|null }`.
  The first PUT for a legal entity creates the override with `expectedVersion: null`.
- Revert: `{ "reason": "≥10" }`.
- 200 `Policy` (revert: 204).
- Errors:
  - 400 (`ESIGN_REASON_TOO_SHORT`, window range, `ESIGN_SECOND_FACTOR_NOT_AVAILABLE`, unknown action or meaning);
  - 404 (legal entity not in tenant — `ILegalEntityReferenceValidator`);
  - 409 `ESIGN_POLICY_VERSION_CONFLICT`.
- History: `GET /api/v1/esign/policy/history?legalEntityId=` (key `esign.policy.read`) → `[ { atUtc, by, scope, legalEntityId, changes[], reason } ]`.
- Audit `esign.policy.changed` / `esign.policy.override-reverted`.

### 4.17 `POST /api/v1/esign/samples` — `createSample`
- Key `esign.my-samples.manage`.
- Request `{ "ticket": "…", "kind": "drawn | uploaded", "pngBase64": "…", "strokes": [...] | null, "idempotencyKey": "…" }`
- 201 `Sample` (`pending` or `active` depending on the tenant policy).
- Errors: 403 `ESIGN_TICKET_INVALID` / `ESIGN_REAUTH_REQUIRED` (intent `sample`, kind `password`), 413, 415,
  400 `ESIGN_DRAWING_EMPTY`.
- Audit `esign.sample.submitted`.
- Related:
  - `GET /api/v1/esign/samples/mine` (key `esign.my-samples.manage`) → `Sample[]` (history, newest first).
  - `GET /api/v1/esign/samples/{id}/image` → `image/png` (owner, approvers, or anyone allowed to read a signature that
    uses it; else 404).

### 4.18 `POST /api/v1/esign/samples/{id}/deactivate` · `POST /api/v1/esign/samples/{id}/withdraw` — `deactivateSample` / `withdrawSample`
- Key `esign.my-samples.manage`, owner only (else 404).
- Deactivate: Active → Inactive.
- Withdraw: only if unused and not Active, else 409 `ESIGN_SAMPLE_IN_USE`. The sample is never deleted; a `withdrawn`
  event is appended.
- 200 `Sample`. Audit `esign.sample.deactivated` / `esign.sample.withdrawn`.

### 4.19 `GET /api/v1/esign/samples/pending` · `POST /api/v1/esign/samples/{id}/approve` · `POST /api/v1/esign/samples/{id}/reject` — `listPendingSamples` / `approveSample` / `rejectSample`
- Keys:
  - list and approve: `esign.samples.approve`;
  - reject: `esign.samples.reject`, body `{ "reason": "≥10" }`.
- 200 `Sample`. Approve makes the sample active and deactivates the owner's previous active sample (one transaction).
- Errors: 403 `ESIGN_SELF_APPROVAL_FORBIDDEN`, 409 `ESIGN_SAMPLE_NOT_PENDING`.
- Decision log: `GET /api/v1/esign/samples/decisions` → `[ { atUtc, by, owner, sampleNumber, decision, reason } ]`.
- Audit `esign.sample.approved` / `esign.sample.rejected`.

### 4.20 `GET /api/v1/esign/meanings?activeOnly=` — `listMeanings`
- Key `esign.meanings.read` or `esign.signatures.sign`. 200 `Meaning[]`.

### 4.21 `POST /api/v1/esign/meanings` · `PUT /api/v1/esign/meanings/{code}` · `POST /api/v1/esign/meanings/{code}/activate` · `POST /api/v1/esign/meanings/{code}/deactivate`
- Key `esign.meanings.manage`.
- Create `{ "code", "names"{7}, "description" }`. Update `{ "names"{7}, "description" }` (code is in the path and cannot
  change).
- 201 / 200 `Meaning`.
- Errors: 400 `ESIGN_MEANING_LANGUAGE_MISSING` (`{ missing: ["ar", …] }`), 409 `ESIGN_MEANING_CODE_EXISTS`,
  409 `ESIGN_MEANING_IN_USE` (deactivate while used by a policy row).
- Audit `esign.meaning.*`.

### 4.22 `GET /api/v1/esign/actions` — `listSignableActions`
- Key `esign.policy.read`. 200 `[ { actionCode, module, names{7}, defaultRequired, defaultMeaningCode, defaultVisualMode } ]`.

### 4.23 `POST /api/internal/esign/actions/register` — `registerSignableActions` (Platform, S2S, pack CT-D12)
- Auth `X-Internal-Api-Key`. Request `{ "module": "inventory", "actions": [ { "actionCode", "names"{7}, "defaultRequired", "defaultMeaningCode", "defaultVisualMode" } ] }`
- 200 `{ registered, unchanged }`.
- Upsert by `actionCode`; a module may only register its own `module.` prefix (409 `ESIGN_ACTION_PREFIX_MISMATCH`).
- Never deletes; existing policy rows are kept.

### 4.24 `GET /api/v1/esign/signatures?fromUtc=&toUtc=&module=&signerUserId=&meaningCode=&legalEntityId=&page=&pageSize=` — `searchSignatures`
- Key `esign.signatures.export`. Paged rows for the export screen table:
  `{ signature: SignatureRecord, verificationStatus }`.
- Rows outside the caller's LegalEntity data scope are excluded (pack §8 visibility).

### 4.25 `POST /api/v1/esign/exports` — `createExport`
- Key `esign.signatures.export`.
- Request `{ "format": "pdf | csv", "filter": { … as 2.12 … }, "idempotencyKey": "…" }`
- 200 file stream: `application/pdf` or `text/csv; charset=utf-8` (BOM, `;` separator). Header
  `X-Export-Number: EXP-2026-0032`.
- CSV columns: Tarih-saat; İmza kimliği; İmzalayan; Kullanıcı; İmza anlamı; Modül; Kayıt; Şirket; Doğrulama.
- Errors: 400 (from > to), 413 `ESIGN_EXPORT_TOO_LARGE` (> 10 000).
- Every call writes an `ExportLogEntry` and audit `esign.export.created` (KS-6).

### 4.26 `GET /api/v1/esign/exports?page=&pageSize=` — `listExports`
- Key `esign.signatures.export`. 200 paged `ExportLogEntry[]`.

### 4.27 Error code table

| Code | HTTP | Meaning |
|---|---|---|
| `ESIGN_REAUTH_FAILED` | 401 | wrong password (counts toward lockout) |
| `ESIGN_ACCOUNT_LOCKED` | 423 | account locked (login lockout) |
| `ESIGN_ACTOR_NOT_TENANT_USER` | 403 | platform / partner token |
| `ESIGN_REAUTH_WINDOW_EXPIRED` | 409 | no open window |
| `ESIGN_INTENT_REQUIRES_PASSWORD` | 400 | window ticket asked for sample/invalidate |
| `ESIGN_TICKET_REQUIRED` | 400 | ticket missing |
| `ESIGN_TICKET_INVALID` | 403 (409 on §4.3) | ticket unusable |
| `ESIGN_REAUTH_REQUIRED` | 403 | ticket kind/age does not satisfy policy |
| `ESIGN_REAUTH_UNAVAILABLE` | 503 | AuthService unreachable (fail closed) |
| `ESIGN_SUBJECT_NOT_FOUND` | 404 | unknown, other tenant, or not visible |
| `ESIGN_MEANING_NOT_ALLOWED` | 409 | inactive or not the policy meaning |
| `ESIGN_APPEARANCE_NOT_ALLOWED` | 409 | violates visual mode |
| `ESIGN_SAMPLE_NOT_ACTIVE` | 409 | sample not the caller's active sample |
| `ESIGN_DRAWING_EMPTY` | 400 | drawn without strokes |
| `ESIGN_IMAGE_NOT_PNG` | 415 | not PNG bytes / bad IHDR |
| `ESIGN_IMAGE_TOO_LARGE` | 413 | > 1 MB |
| `ESIGN_REQUEST_NOT_OPEN` | 409 | request not waiting |
| `ESIGN_SIGNER_NOT_NOMINATED` | 403 | caller is not the request's signer |
| `ESIGN_SIGNER_NOT_FOUND` | 404 | signer user not in tenant |
| `ESIGN_REASON_TOO_SHORT` | 400 | reason < 10 |
| `ESIGN_ALREADY_INVALIDATED` | 409 | second invalidation |
| `ESIGN_POLICY_VERSION_CONFLICT` | 409 | stale policy edit |
| `ESIGN_SECOND_FACTOR_NOT_AVAILABLE` | 400 / 501 | KS-11 |
| `ESIGN_MEANING_LANGUAGE_MISSING` | 400 | < 7 names |
| `ESIGN_MEANING_CODE_EXISTS` | 409 | duplicate code |
| `ESIGN_MEANING_CODE_LOCKED` | 409 | code change after use |
| `ESIGN_MEANING_IN_USE` | 409 | deactivate while referenced |
| `ESIGN_SELF_APPROVAL_FORBIDDEN` | 403 | owner decides own sample |
| `ESIGN_SAMPLE_NOT_PENDING` | 409 | decision on a non-pending sample |
| `ESIGN_SAMPLE_IN_USE` | 409 | withdraw a used / active sample |
| `ESIGN_ACTION_PREFIX_MISMATCH` | 409 | action registered outside own module |
| `ESIGN_EXPORT_TOO_LARGE` | 413 | > 10 000 rows |
| `ESIGN_SIGNATURE_REQUIRED` | 409 | workflow step needs a signature (§5) |
| `ESIGN_SIGNATURE_MISMATCH` | 409 | signature does not fit the task (§5) |

## 5. Workflow (MOD-0023) addendum — additive only

### 5.1 Template step attribute (`WorkflowTemplateVersion.DefinitionJson`)
```json
{ "stages": [ { "code": "QA", "steps": [ { "code": "RELEASE", "name": "QA serbest bırakma", "type": "approval",
  "assignment": { "candidatePrincipalIds": ["position:…"] },
  "requirements": { "commentRequired": false, "evidenceRequired": false,
                    "signatureRequired": true, "signatureMeaning": "RELEASE" } } ] } ] }
```
- Both new keys are optional. Absent means `false` / `null`. Templates already published are unchanged and immutable.
- Publish validation (additive): when `signatureRequired` is true, `signatureMeaning` must be an active tenant meaning.
  Otherwise 400 `ESIGN_MEANING_NOT_ALLOWED` on publish.

### 5.2 Approve request (both entry points)
- `POST /api/v1/workflow/tasks/{taskId}/approve`, body `ApproveWorkflowTaskRequest`
  `{ actorId, reasonCode, idempotencyKey, comment?, evidenceRef?, `**`signatureId?`**` }`. The new field is trailing,
  optional, default null. Existing callers are unaffected.
- `POST /api/v1/work-items/{itemId}/actions/approve`: payload `WorkItemActionPayloadDto` gains a trailing **`signatureId?`**
  (`[JsonIgnore(WhenWritingNull)]`). The in-process dispatcher passes it to the same command.

### 5.3 Behaviour
- Steps without `signatureRequired`: identical to today. `signatureId`, if sent, is stored on the transition log and not
  validated.
- Steps with `signatureRequired`:
  - Missing → 409 `ESIGN_SIGNATURE_REQUIRED`.
  - Otherwise the signature must have:
    - the same tenant;
    - `signer.userId == actorId`;
    - `meaningCode == step.signatureMeaning`;
    - `context.workflowTaskId == taskId` and `context.decision == "approve"`;
    - no invalidation;
    - a current subject hash equal to `subjectHash`;
    - `signedAtUtc ≥ task.createdAt`;
    - no binding to another task.
  - Else 409 `ESIGN_SIGNATURE_MISMATCH`.
- Check order: idempotent replay → task state → assignment → actor → SoD → **signature** → transition. Existing codes and
  statuses (`SOD_VIOLATION` 409, `WORKFLOW_ACTOR_DENIED` 403, …) are unchanged.
- Response: `WorkflowTaskTransitionResponse` unchanged. History (`GET instances/{id}/history`) gains a trailing
  `signatureId` per transition (additive).
- Work item projection: the workflow approval work item gains `signatureRequired` / `signatureMeaning` (additive) so
  WorkCenter can open the dialog first.
- How a client signs for a task: `POST /api/v1/esign/signatures` with
  `subject = instance object (or workflow-task fallback)` and
  `context = { workflowInstanceId, workflowTaskId, stepCode, decision: "approve" }`, then approve with `signatureId`.

## 6. Signable-subject provider contract (consumer side)

`GET {providerBaseUrl}/esign-subjects/{subjectType}/{subjectId}` → 200 `SignableSubject` · 404 (unknown or not
visible to the caller).
- Called by Platform with the **caller's JWT** and `X-Tenant-Id`.
- `providerBaseUrl` comes from Platform operator config (`ESign:SubjectProviders:{module}:BaseUrl`), never from a
  manifest (WC-D1 Y2 pattern). In-process providers for Platform modules use the same shape.
- The provider owns the field list. A change to it changes the hash of new signatures only, and makes old signatures
  verify as `changed` only if the values changed. So providers must keep field names stable and version their list in
  their own contract.

## 7. Canonical form and digests (normative)

- Canonical JSON (FU23 rules, `DocumentSignableSubjectResolver.cs:399-442`, plus `module`):
  - keys of `fields` sorted by ordinal;
  - values are JSON strings or `null`;
  - escapes `\\ \" \n \r \t`;
  - DateTime as UTC ISO-8601 round-trip "O";
  - bool `true`/`false`;
  - decimal invariant culture;
  - no whitespace.
- `subjectHash = hex(SHA-256(UTF-8(canonicalSubject)))`.
- `signatureDigest = hex(SHA-256(UTF-8(canonical({ signatureNumber, tenantId, module, subjectType, subjectId,
  subjectHash, legalEntityId, actionCode, context, meaningCode, meaningTextAtSign, signerUserId, signerNameAtSign,
  signedAtUtc, reauthKind, passwordVerifiedAtUtc, appearanceType, appearanceSampleId, appearanceImageHash, requestId }))))`.
  Keys are sorted as above and nested `context` keys are sorted.
- `imageHash = hex(SHA-256(png bytes))`.
- The frozen yaml carries golden test vectors (one subject, one signature) produced in E2.

## 8. Non-functional

- Signing p95 ≤ 800 ms excluding the remote subject provider; the ticket consume is one indexed `FindOneAndUpdate`.
- Payload limits: request ≤ 1.5 MB (base64 PNG + strokes).
- Rate limit on `esign/reauth`: no separate limit; lockout applies (same as login).
- Retention: records, invalidations, samples, sample events, exports and policy changes are never deleted by the
  application.

## 9. Check table — nothing existing removed or changed

| Existing surface | Change |
|---|---|
| `api/tenant-auth/login`, `mfa/*`, `change-password/*` | none |
| `api/platform-auth/*` | none |
| `ApproveWorkflowTaskRequest` | + trailing optional `SignatureId` |
| `WorkItemActionPayloadDto` | + trailing optional `SignatureId` (`JsonIgnore` when null) |
| Workflow `DefinitionJson` | + optional `requirements.signatureRequired`, `requirements.signatureMeaning` |
| Workflow transition log / history | + trailing `signatureId` |
| Workflow error codes / statuses | none changed; two new codes only on flagged steps |
| FU23 `api/v1/document-management/signature*` (16 endpoints) | none (E6 later, additive) |
| `ContentStorageScope` | + `ESignatureImages = 4` (enum is additive by design) |
| Gateway | + route `/api/v1/esign/{everything}` only |
