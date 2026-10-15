# Global Product — bring-up inventory (2026-10-01)

Read-only inventory of the takeover branch (`feature/mdm/product-five-takeover` @ 9d108368c): what must exist for the
Global Product journey to run end to end on a developer machine. Nothing was run, no database was read, no secret
store was opened. Produced by an independent read of the code for CONTROL TOWER; it is the input of the Global Product
work packages, not an acceptance.

Journey: a tenant user opens the register → creates a draft (a canonical code is reserved) → submits → an approver
approves in the Task Center → the product becomes active. Also: edit a draft, withdraw, reject.

**Headline.** Draft create, list and edit should work almost as-is. Submit, approve, reject and withdraw will not: the
service-to-service trust chain, the workflow options, the workflow definition and the decision-polling worker are
absent from every Development appsettings file. Several gaps do not fail softly — a misconfigured first submit
QUARANTINES that draft's operation (`ManualReconciliationRequired`), and nothing retries it.

## 1. What exists for Development

| # | Item | Exists? | What is needed |
|---|---|---|---|
| 1 | Web page, proxy, JS, resx (7 languages, 63 keys each) | yes | — |
| 2 | MDM API endpoints (`api/global-products`) | yes | — |
| 3 | Gateway routes | yes | — |
| 4 | MDM JWT, Mongo, module-registration credential | yes | — |
| 5 | Canonical code reservation (`GP-000000000001`, per tenant) | yes | — |
| 6 | Collections and indexes (created on first use) | yes | — |
| 7 | MDM `ProductIdentityWorkflow` options | no | section; default `Enabled=false`; empty `ReasonCode` is invalid |
| 8 | MDM→Auth service identity + MDM→Platform client URL | no | two sections, one secret |
| 9 | Auth `ServiceIdentityTokenIssuer` (RS256) | no | RSA private key (secret) |
| 10 | Platform `TrustedServiceTokenValidation` | no | the matching public key |
| 11 | Platform `TrustedWorkflowStartAuthorization:Entries` | no | one entry for `GlobalProduct` |
| 12 | Service client record + tenant grant in Auth | no — **no production code creates either** | today: a hand insert into two collections |
| 13 | Workflow definition for Global Product | no — nothing seeds it | create + publish, **single step** |
| 14 | MDM `ProductIdentityWorkflowWorker` (decision polling) | no (default off) | `Enabled` + `LeaseOwner` |
| 15 | Permission catalog rows | partly | read/create are seeded; six more arrive by manifest sync at MDM startup |
| 16 | Role grants | partly | Admin gets read/create; update/submit/withdraw go to nobody by default |
| 17 | Tenant entitlement to `product-item-sku-master` | no (`IsBaseline: false`) | a platform admin adds it |
| 18 | Sidebar entry + nav names (7 languages) | yes, once entitled and permitted | — |
| 19 | RabbitMQ for activation | not needed | activation is HTTP polling |
| 20 | Audit delivery to Platform | no | worker + a second service client + a one-time cutover |
| 21 | Legal-entity scope trust | not needed | no rollout-state document = Preparation mode |
| 22 | Notifications | none in code | — |

## 2. Service-to-service trust

| Call | Credential |
|---|---|
| MDM → Auth `POST /api/internal/v1/auth/service-tokens/issue` | shared secret in `X-Service-Client-Id` / `X-Service-Client-Secret`, body `{tenantId, audience}` |
| MDM → Platform `/api/internal/v1/workflow/trusted-consumer/{start, start-result, terminal-decision-evidence, cancel-preflight, cancel}` | RS256 service token as Bearer; on start and cancel also the user's own JWT in `X-Delegated-Authorization` + `Idempotency-Key` |
| MDM → Platform `/api/internal/module-catalog/register-manifest` | per-service credential headers (present in Dev) |
| MDM → Platform `/api/internal/v1/audit/source-intents/accept` | RS256 token, audience `TRUSTED_AUDIT_SOURCE_INGEST` (only with the audit worker on) |

- Auth looks the client up by `ClientCode`, verifies a PBKDF2-SHA256 hash, requires `ServiceName = Diten.MDM`, the
  identity's `AllowedAudience` equal to the requested audience, and an enabled tenant grant. The token is RS256, 300 s.
- There is no JWKS endpoint: Platform validates against a static public key from configuration.
- Platform also requires the delegated user to hold `platform.workflow.instances.start` in the same tenant.
- **Provisioning gap.** `serviceClientIdentities` and `serviceClientTenantGrants` have no production writer; the only
  inserts are in tests. Auth's `--service-client-operational-run` reads and ROTATES an existing identity and its rotate
  is Windows-only; `--run-product-identity-entitlement-reconciliation` is hard-wired to one tenant and Windows-only.
- The token endpoint's path is not in Auth's tenant-resolution bypass list; MDM's header-less call passes on dev only
  because `TenantResolution:DevBypassEnabled` is true.

## 3. Workflow definition and approval

- Which one: whatever `ProductIdentityWorkflow:GlobalProductTemplateId` / `GlobalProductTemplateCode` names; object
  type `GlobalProduct`, object ref = the canonical code.
- Where it comes from: nothing seeds or installs it. No tenant workflow-definition bridge exists on this branch (the
  cross-branch audit places it on another archived branch).
- Candidates: the definition's first step wins if it names any; otherwise MDM's configured `CandidatePrincipalIds`
  (bare user ids).
- **Only the first resolved candidate can decide** — the others see the item and get 403 (MOD-0023's resolved
  principal rule).
- Maker-checker twice: Platform refuses the starter's approval; MDM quarantines if the decider equals the maker.
- **The definition must be single-step**: MDM requires the terminal evidence's task to be the initial task.
- If absent or wrong: 404 → `AwaitingMakerReplay`; 400 / 403 / 409 → quarantined.

## 4. Permissions and roles

| Key | Seeded? | Default recipients |
|---|---|---|
| `mdm.global-products.read`, `.create` | yes | Admin and Viewer of an entitled tenant (by sync) |
| `.update`, `.submit`, `.withdraw`, `.request-correction`, `.request-retirement`, `.retire` | no — manifest sync | none of Admin/Viewer; the dedicated roles below |
| `platform.workflow.instances.start` | yes, Tenant | `ProductDataSteward` |
| `platform.workflow.tasks.approve`, `.reject` | yes, Tenant | `ProductIdentityApprover` |

Dedicated roles created by the entitlement sync (needs RabbitMQ): `ProductDataSteward` (read, create, submit, update,
withdraw, request-correction, start), `ProductIdentityApprover` (read, inbox view, approve, reject),
`ProductIdentityRetirementSteward` (read, withdraw, request-retirement, start). Permissions are baked into the token:
sign in again after a grant.

## 5. Workers

| Worker | Flag | Dev default | If off |
|---|---|---|---|
| MDM `ProductIdentityWorkflowRecoveryWorker` | `ProductIdentityWorkflowWorker:Enabled` | off | an approval is never applied; the product stays `PendingIdentityApproval` |
| MDM `AuditIntentDeliveryWorker` | `AuditIntentDeliveryWorker:Enabled` | off | no central audit record |
| MDM `ModuleRegistrationHostedService` | on | on | no page, no permission rows |

With defaults an approval shows in the register after roughly 15–45 s.

## 6. Gaps visible in the code

1. **Withdraw contract mismatch.** MDM answers `{operationId, globalProductId, checkpoint, …}`; the Web proxy insists
   on `lifecycleStatus` and `version` and accepts only 200. A successful withdraw is expected to come back as 502
   after one replay, with the JS then reading the record back and showing success. No Web test covers the controller.
2. **Configuration errors quarantine.** Almost any trust or template error on the first submit moves the operation to
   `ManualReconciliationRequired`; it blocks that draft at that version.
3. **No recovery endpoint.** The manifest declares `RECOVER_ORPHANED_LIFECYCLE_OPERATION`; MDM has no controller for it.
4. **The page draws before it checks.** `Index()` returns the view to any signed-in tenant user; one without the read
   permission sees title, toolbar and an empty table, then a 403 toast (rule UAS-001: no skeleton for the unauthorised).
5. **The list is hand-written**: `DtDefaults.create` + `new DataTable`, old client-side export buttons — neither the
   shared list shell / factory nor the server-side export standard.
6. **The Task Center title is generic**: no approval source resolver for `GlobalProduct`.
7. **A tenant sees no audit trail**: intents are written into the aggregate; delivery needs the worker, a second
   service client and a cutover; the only audit UI is under `/Platform`.
8. **No notification**: the approver finds the item only by opening the Task Center.
9. **Single acting approver, single-step definition** (section 3).
10. Audit intents are capped at 32 per aggregate; beyond that a write fails.

## 7. Shortest path to a working journey on dev (as the code stands)

1. RSA-2048 key pair (secret).
2. Auth config `ServiceIdentityTokenIssuer` (issuer, key id, private key, lifetime 300).
3. Platform config `TrustedServiceTokenValidation` (same issuer and key id, public key).
4. Auth DB: one service client identity (`ClientCode`, `ServiceName = Diten.MDM`, `AllowedAudience =
   TRUSTED_WORKFLOW_CONSUMER`, PBKDF2 hash) and one tenant grant — a hand insert today.
5. MDM config: `AuthProductIdentityWorkflowServiceIdentityProvider`, `ProductIdentityWorkflowClient`,
   `ProductIdentityWorkflow` (enabled, template code, candidates, reason code), `ProductIdentityWorkflowWorker`
   (enabled, lease owner).
6. Platform config `TrustedWorkflowStartAuthorization:Entries` for `GlobalProduct`.
7. Start Auth → Platform → MDM → Gateway → Web (MDM pushes its manifest).
8. Create and publish the single-step definition.
9. Grants for a maker and a different approver; sign in again.
10. Maker: draft → submit. Approver: Task Center → approve. The register shows the product approved within the poll
    interval.

Do steps 1–9 BEFORE the first submit (gap 2).

## 8. Not determinable from reading

Whether local user-secrets already carry the missing Auth/Platform sections; whether the dev databases already hold a
service client, a published definition, an entitlement or the dedicated roles; the exact BSON a hand-inserted service
client needs; the real status Platform returns with `TrustedServiceTokenValidation` missing; whether editing a draft
frees a quarantined submit; whether the withdraw round trip really is 502-then-reconciled.
