# Service trust chain (MDM → Auth → Platform) — runbook

How to bring up the chain that lets MDM start an approval in Platform on behalf of a user: MDM asks Auth for a
short-lived service token, Platform validates that token and starts the workflow. Global Product identity approval is
the first consumer; the other product modules use the same chain.

Contract: `execution/domains/platform-shared-services/module-packs/MOD-0033-FU02-service-identity-token-issuance-foundation.md`,
Section F. What was missing before this existed: `docs/records/audits/2026-10/global-product-bring-up-inventory-2026-10-01.md`.

**No value appears in this document** — only setting names and where they live.

## The three things that have to agree

| # | What | Who holds which half |
|---|---|---|
| 1 | An RSA key pair (2048 bit or more) that signs service tokens | Auth holds the **private** key; Platform holds the **public** key |
| 2 | A service-client credential pair for MDM | Auth holds the **hash**; MDM holds the **raw secret** |
| 3 | Which client may start which workflow | Platform lists the client's **id**, object type and template code |

Auth never receives the raw secret except inside a token request. Platform never receives a private key.

A declared client is created by Auth **at startup, if it is missing** — and is never changed afterwards. A different
hash in configuration does not replace the stored one; Auth logs a warning naming the client and the differing field
names and carries on. That is deliberate: a restart must not be able to swap a credential.

---

## Development

### 1. Before you start

- `mongod` is running **before** any service starts (a service that started first hangs 30 s on every query).
- You know the tenant id you test with, and the user id (the token's `sub`) of the person who will **approve**.
  The approver must be a different user from the one who submits.

### 2. Run the bootstrap — once per Auth database

```bash
scripts/dev/setup-service-trust.sh --tenant-id <tenant id> --approver-user-id <approver user id>
```

Windows: `scripts/dev/setup-service-trust.ps1 -TenantId <tenant id> -ApproverUserId <approver user id>`.

It writes one git-ignored `appsettings.Development.local.json` next to `appsettings.Development.json` in each service
(owner-only), prints the three paths and the client id, and prints nothing secret. Each service loads that file in
Development only.

| File in | Sections it holds |
|---|---|
| `services/Diten.AuthService/src/Diten.AuthService.Api` | `ServiceIdentityTokenIssuer` (private key), `ServiceClientRegistry` (hash, tenants) |
| `services/Diten.Platform/src/Diten.Platform.API` | `TrustedServiceTokenValidation` (public key) |
| `services/Diten.MdmService/src/Diten.MdmService.Api` | `AuthProductIdentityWorkflowServiceIdentityProvider` (raw secret), `ProductIdentityWorkflow` (`Enabled`, `CandidatePrincipalIds`), `ProductIdentityWorkflowWorker` (`Enabled`) |

Running it again changes nothing and says `already set up`. It never overwrites a file: if only some of the three
exist it stops and tells you which. The local file sits directly after `appsettings.Development.json`: it outranks that
file (and `appsettings.json` before it), while user secrets, environment variables and command-line arguments still win
over it. It is never copied to build or publish output. `DevelopmentLocalSettings__Enabled=false` (environment variable
or argument) makes a service ignore the file — test hosts that run a real service under Development set it.

Options: `--tenant-id` and `--approver-user-id` may be repeated; `--auth-dir`, `--platform-dir`, `--mdm-dir` write
somewhere else; `--client-code`, `--issuer`, `--auth-base-url` replace the defaults. `--auth-base-url` must be this
machine (`localhost`, `127.0.0.1` or `[::1]`, no user info, query or fragment): MDM sends the raw secret there.

What is **tracked** (not secret, already in `appsettings.Development.json`): Platform's
`TrustedWorkflowStartAuthorization` entry for the default client and `GlobalProduct`; MDM's
`ProductIdentityWorkflowClient:PlatformBaseUrl`, `ProductIdentityWorkflow:GlobalProductTemplateCode` and `:ReasonCode`,
`ProductIdentityWorkflowWorker:LeaseOwner`. The switches are **not** tracked: a checkout that was never bootstrapped
behaves as it did before (a submit answers 503 and writes nothing).

⚠ **One credential set per Auth database.** The local files are the only copy of the secret that matches the hash Auth
stored on its first start. If you work in a second checkout against the same database, **copy the three files** from
the first one — do not bootstrap again. A second bootstrap generates a new secret that Auth will not adopt (it warns
`already exists and differs from configuration in CredentialHash,CredentialVersion`), MDM's token requests are then
refused, and a refused token request **quarantines** the submit — and, with the worker enabled, every approval that
is pending in that MDM database within one poll (the worker and the withdraw path have no pre-flight yet). If the files are lost, bootstrap with a new
`--client-code`: a new client is created, and the bootstrap also writes Platform's authorization entry for it into
Platform's local file.

### 3. Start the services, in this order

`Auth → Platform → MDM → Gateway → Web`

On Auth's first start after the bootstrap, its log has one line per create:
`ServiceClientRegistry: created service client …` and `ServiceClientRegistry: granted service client … for tenant …`.
Later starts log nothing for an unchanged client. MDM pushes its module manifest to Platform when it starts (pages and
permission rows come from that).

If Auth does not start and the error begins `Configuration error: ServiceClientRegistry:Clients[n] <Field> …`, the named
field of entry `n` is blank or malformed. Nothing was written.

### 4. Create and publish the approval definition — once per tenant

Nothing seeds it. It must have **one** approval step: MDM requires the deciding task to be the task the start created.
Both calls go through the gateway and must resolve to the tenant you test with.

**Create** — `POST /api/v1/workflow/definitions` · permission `platform.workflow.definitions.manage`

| Field | Value |
|---|---|
| `templateCode` | the tracked `ProductIdentityWorkflow:GlobalProductTemplateCode` value, exactly (max 64, letters, digits, `.` `_` `-`) |
| `name` | free text, max 200 |
| `description` | optional, max 1000 |

Answers 201 with the definition `id`. A second create with the same code answers 409.

**Publish** — `POST /api/v1/workflow/definitions/{id}/publish` · permission `platform.workflow.definitions.publish`

| Field | Value |
|---|---|
| `definitionJson` | a JSON **string**: one stage with one step of `type` `approval` — `stages[0].code`, `stages[0].steps[0].code`, `.name`, `.type`. Leave the step's `assignment` out, so the candidates MDM sends are used. |
| `schemaVersion` | required text, max 32 |
| `expressionVersion` | required text, max 32 |
| `expectedTemplateVersion`, `expectedRowVersion`, `publishReason` | optional |

Both `definitions.*` permissions are platform-administrator scope: use a platform administrator, or grant them
explicitly. A definition that was created but not published makes the first submit fail with a conflict — and that
quarantines it (step 7).

### 5. Permissions

| Who | Needs | Usual source |
|---|---|---|
| Tenant | entitlement to `product-item-sku-master` | a platform administrator adds it |
| Submitter | `mdm.global-products.read`, `.create`, `.update`, `.submit`, and `platform.workflow.instances.start` | role `ProductDataSteward` |
| Approver | `mdm.global-products.read`, `platform.workflow.tasks.approve`, `platform.workflow.tasks.reject`, the Task Center inbox | role `ProductIdentityApprover` |

Permissions are baked into the token: sign in again after a grant. With several candidates, only the first one in
id order can decide; the others see the item and are refused.

### 6. What should happen

1. Submitter creates a draft and submits it → the request answers 202 and the product shows as pending approval.
2. Approver opens the Task Center and approves (or rejects).
3. The register shows the decision after MDM's worker next polls: 15 s by default, so expect 15–45 s.

### 7. When it does not

| You see | Meaning | The draft |
|---|---|---|
| 503 `PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID` | MDM has no usable `ProductIdentityWorkflowClient:PlatformBaseUrl` | untouched — fix and submit again |
| 503 `PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID` | MDM has no usable `AuthProductIdentityWorkflowServiceIdentityProvider` (not bootstrapped) | untouched — fix and submit again |
| 503 `PRODUCT_IDENTITY_WORKFLOW_CONFIGURATION_INVALID` | the transport is configured but `ProductIdentityWorkflow:Enabled` is not on (settings edited by hand) | untouched |
| 409 `PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED` | the operation was quarantined: Auth or Platform refused (wrong secret, missing grant, missing or unpublished definition, missing permission, missing authorization entry) | **blocked at that version** — there is no retry |

The three 503 rows are refused before anything is written; on a checkout that was never bootstrapped you get the
second one. Everything Auth or Platform refuses lands in the last row — do steps 2–5 before the first submit.

---

## Live — for IT

Names and places only. Every value below is a deployment secret or a deployment decision and belongs in the
environment's secret store / configuration, never in a tracked file. Configuration keys follow the usual
`Section:Key` form (`Section__Key` as an environment variable; list entries are `Section__List__0__Key`).

### 1. Signing key pair

Generate an RSA key pair of at least 2048 bits (PEM). Custody and rotation of this key are a separate decision
(pack §20.5).

| Service | Setting | Holds |
|---|---|---|
| Auth | `ServiceIdentityTokenIssuer:Issuer` | issuer name |
| Auth | `ServiceIdentityTokenIssuer:ActiveKeyId` | key id |
| Auth | `ServiceIdentityTokenIssuer:ActivePrivateKeyPem` | **private key** |
| Auth | `ServiceIdentityTokenIssuer:TokenLifetimeSeconds` | must be 300 |
| Platform | `TrustedServiceTokenValidation:Issuer` | the same issuer name |
| Platform | `TrustedServiceTokenValidation:CurrentKeyId` | the same key id |
| Platform | `TrustedServiceTokenValidation:CurrentPublicKeyPem` | **public key** |

### 2. Client credential — the generator

```bash
dotnet run --project services/Diten.AuthService/tools/Diten.AuthService.ServiceClientCredentialGenerator -- \
  --secret-file <new file> --client-code <client code>
```

- It generates the secret itself; it accepts none.
- The **raw secret** is written once to the file you name — refused if the file exists, readable by you only. Move it
  into MDM's secret store and remove the file.
- It prints the **hash**, the **version**, and the **client id** for that client code. It needs no database and no network.

### 3. Auth — declare the client

`ServiceClientRegistry:Clients[]`, one entry per client:

| Field | Holds |
|---|---|
| `ClientCode` | the code MDM presents |
| `ServiceName` | the calling service's name (the issuer's closed list) |
| `AllowedAudience` | the one audience this client may ask for |
| `CredentialHash` | the generator's hash |
| `CredentialVersion` | the generator's version |
| `Tenants[]` | tenant ids to grant |

Restart Auth. A missing client and missing grants are created and recorded in the Auth audit log. Onboarding another
tenant = add its id and restart Auth. Removing an entry or a tenant changes nothing; a grant somebody disabled stays
disabled; a different hash does **not** replace the stored one.

### 4. Platform — authorize the client

`TrustedWorkflowStartAuthorization:Entries[]`: `ClientId` (the id the generator printed), `ServiceName`, `Audience`,
`ObjectType`, and exactly one of `TemplateCode` / `TemplateId`. Restart Platform.

### 5. MDM

| Setting | Holds |
|---|---|
| `AuthProductIdentityWorkflowServiceIdentityProvider:AuthBaseUrl`, `:ExpectedIssuer`, `:ClientId` | Auth's address, the issuer name, the client code |
| `AuthProductIdentityWorkflowServiceIdentityProvider:ActiveClientSecret` | **the raw secret** |
| `ProductIdentityWorkflowClient:PlatformBaseUrl` | Platform's address |
| `ProductIdentityWorkflow:Enabled`, `:GlobalProductTemplateCode`, `:CandidatePrincipalIds[]`, `:ReasonCode` | the switch, the definition's code, the approvers, the reason code |
| `ProductIdentityWorkflowWorker:Enabled`, `:LeaseOwner` | the decision-polling worker; a lease owner name unique to the instance |

Restart MDM last. Then create and publish the definition (Development step 4) and assign the roles (step 5).

### 6. Not covered here — open before a live bring-up

- **Auth's tenant resolution on the token endpoint.** Outside Development a request to the token endpoint without a
  resolved tenant is refused before it reaches the endpoint; MDM's request carries none. Not solved by this work package.
- **Rotation and revocation.** Not done from configuration. Rotation is the operational command of pack Section E,
  which cannot run against a real database today (pack §F, "why this section exists").
- **Definition and approver set-up per tenant** is manual (Development steps 4–5).
