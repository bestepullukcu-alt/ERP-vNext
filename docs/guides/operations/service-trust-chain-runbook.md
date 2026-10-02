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
refused. A refused token request no longer quarantines anything (WP-GP-1B-A-RESILIENCE-01): a submit answers 503
`PRODUCT_IDENTITY_WORKFLOW_SERVICE_UNAVAILABLE_RESUBMIT`, pending approvals stay pending, and MDM's worker stops its
cycle at the first refusal and logs it (step 7) — nothing moves until the files match. If the files are lost, bootstrap with a new
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
explicitly. A definition that was created but not published makes the first submit answer 409
`PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY`; publish it and submit again (step 7).

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

The rule (WP-GP-1B-A-RESILIENCE-01): **a fault of the service chain is not a fault of the record.** A credential,
token, setting or reachability fault leaves the operation where it is; it is retried or resubmitted, and it is logged.
Manual reconciliation is only for an answer that is about the record itself.

**What the submitter (or the withdrawing requester) sees.** These codes are fixed; the screen sentences are a later
package.

| You see | Meaning | What to do | The draft |
|---|---|---|---|
| 503 `PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID` | MDM has no usable `ProductIdentityWorkflowClient:PlatformBaseUrl` | fix the setting, submit again | untouched — nothing was written |
| 503 `PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID` | MDM has no usable `AuthProductIdentityWorkflowServiceIdentityProvider` (not bootstrapped) | bootstrap, submit again | untouched — nothing was written |
| 503 `PRODUCT_IDENTITY_WORKFLOW_CONFIGURATION_INVALID` | the transport is configured but `ProductIdentityWorkflow:Enabled` is not on | fix the setting, submit again | untouched |
| 503 `PRODUCT_IDENTITY_WORKFLOW_SERVICE_UNAVAILABLE_RESUBMIT` | Auth or Platform refused MDM itself (wrong secret, missing tenant grant, key mismatch, missing authorization entry), or did not answer | fix the chain (the MDM log names the code), then **submit again** — same draft, nothing is lost | waiting for the resubmit |
| 401 `PRODUCT_IDENTITY_WORKFLOW_SESSION_EXPIRED` | the approval engine did not accept the submitter's session | sign in again, submit again | waiting for the resubmit |
| 403 `PRODUCT_IDENTITY_WORKFLOW_START_PERMISSION_REQUIRED` | the submitter lacks `platform.workflow.instances.start` | grant it, sign in again, submit again | waiting for the resubmit |
| 409 `PRODUCT_IDENTITY_WORKFLOW_APPROVER_NOT_ASSIGNED` | nobody is assigned to the approval step | assign an approver, submit again | waiting for the resubmit |
| 409 `PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY` | the approval definition is missing or not published | create / publish it, submit again | waiting for the resubmit |
| 409 `PRODUCT_IDENTITY_WORKFLOW_BUSY` | somebody else holds the operation at this very moment — the worker's poll, or another request for the same draft. An attempt that is interrupted (the browser went away, a store error) gives its hold back at once, so this does not outlast the other caller's step | try again in a few seconds | unchanged |
| 409 `PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT` | the operation moved under this request (another caller advanced it first) | reload, look at the state, repeat if still needed | unchanged by this request |
| 409 `PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT` | the request does not fit where the operation is — e.g. a submit repeated while a withdrawal of the same draft is in flight; no call is made | reload | unchanged |
| 409 `PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED` | the start never reached the approval engine (found out by the worker) and only the submitter can repeat it | submit again | waiting for the resubmit |
| 503 `PRODUCT_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED` | one request ran out of steps — not expected; report it with the MDM log | try again | wherever the last step left it |
| 409 with a Platform code (`WORKFLOW_TRUSTED_CANCEL_CONFLICT`, `WORKFLOW_TRUSTED_CANCEL_NOT_FOUND`, …) on a **withdrawal** | the approval was decided or closed before the withdrawal reached it; the code is Platform's own, passed through | nothing to withdraw — the decision is (or will shortly be) on the register | follows the decision |
| 409 `PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED` | the operation was quarantined: the answer was about the record (a contract violation, an idempotency conflict, evidence that contradicts what MDM stored) | no retry — see below | **blocked at that version** |

"Submit again" always means the same request again: the operation is resumed, never duplicated. A start and a
cancellation carry the user's own token, so the worker can never repeat them — that is why these answers ask for a
resubmit instead of saying "queued".

**What the worker does with an approval that is already pending.** It keeps the operation at `AwaitingDecision`
(disposition `Retryable`) and tries again with a back-off that doubles from `RetryDelaySeconds` up to
`ServiceFaultMaxRetryDelaySeconds` (default 900). Nothing is quarantined. The operation carries
`ServiceFaultCount` — the WORKER's consecutive failed attempts; a user's clicks during an outage neither count nor
move the worker's next attempt — and `ServiceFaultSinceUtcTicksV1`, the moment the fault was first met by anybody.
Both are cleared only when the chain really answers: a start is accepted, or a decision is read.

**A withdrawal during an outage.** The approval still exists at the engine and may be decided at any moment, so the
operation never waits where the worker does not read. If the cancellation is refused before it is processed, the
withdrawal is forgotten, the operation awaits the decision again and the requester is told to ask again. If the
cancellation got no answer, the worker reads the approval at its next cycle: still open → the withdrawal did not
happen (forgotten, decision awaited again); decided → the decision is applied; closed with no readable decision →
the withdrawal landed, and the operation stays at `WithdrawalOutcomeUnknown` until its requester repeats it (the
worker re-reads it every `ServiceFaultMaxRetryDelaySeconds`).

**A success MDM cannot accept** (`PRODUCT_WORKFLOW_RESPONSE_INVALID`: Platform answered 200/201 with a field MDM does
not know, or contradicting itself) is a mismatch of versions between the two services, not a fault of the record.
The operation waits, and the line is an **Error the first time** — waiting will not mend it; deploy matching versions.

| MDM log line | Level | Means |
|---|---|---|
| `… met a service fault at {Call}: {ResultCode}; worker attempt N, failing since …; the worker retries` | Warning; **Error** from worker attempt `ServiceFaultErrorAfterAttempts` (default 5), or once the fault has lasted longer than `ServiceFaultMaxRetryDelaySeconds`, or at once for `…RESPONSE_INVALID` | one operation is waiting; the worker will retry it |
| `…; nothing was started; the maker resubmits` · `…; the maker was told to resubmit; the worker's schedule is unchanged` | same rule (the attempt number shown is the worker's and does not rise) | a user met the fault; the answer was 503 `…SERVICE_UNAVAILABLE_RESUBMIT` |
| `…; the withdrawal was dropped and its requester repeats it; the decision is awaited again` · `…; the worker reads the approval to learn whether the withdrawal landed` | same rule | a withdrawal met the fault (see above) |
| `… the approval is closed and no decision can be read (…); the withdrawal appears to have landed and waits for its requester` | Warning | only the requester's repeat can finish that withdrawal |
| `recovery cycle stopped: the service identity was refused (…)` | Error | the credential or the token is refused for everybody: nothing else was tried this cycle |
| `recovery skipped the rest of tenant …` | Warning | Auth answered 403 for that tenant — it holds no grant for it, or the audience is not allowed for this client (Auth does not say which); other tenants were processed |
| `recovery cycle skipped: the transport is not configured (…)` | Error | MDM's own settings are missing; no operation was touched |
| `… was refused to the maker on {Call}: {ResultCode} -> {MakerCode}` | Warning | one of the user-facing rows above |
| `… was quarantined at {Checkpoint}: {ResultCode}` | Error | manual reconciliation is required |
| `recovery of operation … failed with {ExceptionType}` · `recovery cycle N failed with {ExceptionType}` | Error | an unexpected failure (the type only — never the message or the stack) |
| `… the lease could not be given back ({ExceptionType}); it expires by itself` | Warning | an interrupted attempt could not release its hold; `BUSY` until `LeaseSeconds` pass |

None of these lines carries a secret, a token, a header value or a user.

| `{ResultCode}` in a waiting line | Look at |
|---|---|
| `PRODUCT_WORKFLOW_IDENTITY_REJECTED` | the secret MDM holds is not the one whose hash Auth stored (second bootstrap, rotation, revoked client) |
| `PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED` | Auth answered 403: its `ServiceClientRegistry:Clients[].Tenants[]` does not list that tenant, or the client's audience is not the one MDM asks for |
| `PRODUCT_WORKFLOW_RESPONSE_INVALID` | MDM and Platform are on versions whose workflow contract differs |
| `PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID` | MDM's `ExpectedIssuer` does not match Auth's issuer, or Auth answered something else |
| `WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED` | Platform cannot validate Auth's token (public key / key id / issuer) |
| `WORKFLOW_TRUSTED_START_FORBIDDEN` | Platform's `TrustedWorkflowStartAuthorization:Entries[]` has no entry for this client, object type and definition |
| `PRODUCT_WORKFLOW_RESPONSE_UNREADABLE` | something between MDM and Platform answered instead of Platform (a proxy page, a crash page) |
| `PRODUCT_WORKFLOW_IDENTITY_UNAVAILABLE`, `PRODUCT_WORKFLOW_UNAVAILABLE`, `…_TIMEOUT` | Auth or Platform is down or slow |

A wrong `ServiceFault*` value (or a `RetryDelaySeconds` above the ceiling) keeps MDM from **starting**
(`PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`); it never turns requests into 500s.

**Still open.** A quarantined operation has no way out (BL-498): no operator action exists and the draft stays
blocked at that version. An unreadable answer or an unknown 403 has no end state: the worker keeps trying at the
ceiling for ever. The seven sibling processors (correction, retirement, GSKU, LSKU) still quarantine on a service
fault and have no pre-flight.

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
| `ProductIdentityWorkflowWorker:ServiceFaultMaxRetryDelaySeconds` (900), `:ServiceFaultErrorAfterAttempts` (5) | how far a waiting operation backs off, and from which attempt its waiting is logged as an error — alert on that line |

Restart MDM last. Then create and publish the definition (Development step 4) and assign the roles (step 5).

### 6. Not covered here — open before a live bring-up

- **Auth's tenant resolution on the token endpoint.** Outside Development a request to the token endpoint without a
  resolved tenant is refused before it reaches the endpoint; MDM's request carries none. Not solved by this work package.
- **Rotation and revocation.** Not done from configuration. Rotation is the operational command of pack Section E,
  which cannot run against a real database today (pack §F, "why this section exists").
- **Definition and approver set-up per tenant** is manual (Development steps 4–5).
