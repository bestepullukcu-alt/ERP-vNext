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
| 409 with a Platform code (`WORKFLOW_TRUSTED_CANCEL_CONFLICT`, `WORKFLOW_TRUSTED_CANCEL_NOT_FOUND`, …) on a **withdrawal** | the approval was **decided or closed** before the withdrawal reached it — and only that: when Platform answers "conflict" while the approval is still open (it merely moved on since MDM read its versions), MDM asks again once, with fresh versions, inside the same request (see the next row for what the user sees if that conflicts too). The code is Platform's own when it is a code (anything longer than 128 characters or with control characters is replaced by `PRODUCT_IDENTITY_WITHDRAWAL_APPROVAL_CLOSED`); it is kept in the withdrawal's own field, so a repeat of the same withdrawal gets the same answer however often the worker has polled the decision since | nothing to withdraw — the decision is (or will shortly be) on the register | follows the decision |
| 409 `PRODUCT_IDENTITY_WORKFLOW_APPROVAL_CHANGING` on a **withdrawal** | the approval is still open and changed twice while it was being withdrawn (somebody is working on it right now); one request asks again once, not more | try again in a moment — the withdrawal was forgotten, so the repeat is a fresh one | waiting for the decision, unchanged |
| 409 `PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED` | the operation was quarantined: the answer was about the record (a contract violation, an idempotency conflict, evidence that contradicts what MDM stored) | no retry — see below | **blocked at that version** |

"Submit again" always means the same request again: the operation is resumed, never duplicated. A start and a
cancellation carry the user's own token, so the worker can never repeat them — that is why these answers ask for a
resubmit instead of saying "queued".

**What the worker does with an approval that is already pending.** It keeps the operation at `AwaitingDecision`
(disposition `Retryable`) and tries again with a back-off that doubles from `RetryDelaySeconds` up to
`ServiceFaultMaxRetryDelaySeconds` (default 900). Nothing is quarantined. The operation carries
`ServiceFaultCount` — the WORKER's consecutive failed attempts; a user's clicks during an outage neither count nor
move the worker's next attempt — and `ServiceFaultSinceUtcTicksV1`, the first fault of the streak of CONSECUTIVE
faults the operation is in. Both are cleared by the first real answer the chain gives for that operation — a start
accepted, a decision read, "not decided yet", "nothing arrived" (whoever asked: the worker's lookup or the user's
own submit), a refusal that is the user's own (session, permission, set-up), an approval found closed. Faults
further apart than two ceilings are not one streak.

Three things follow, and the alert has to know them:

- **The two readers of "still open" differ.** When the WORKER reads an unfinished withdrawal's approval and finds
  it still open, that is an answer and ends the streak. When a USER's withdrawal reads the versions (the same
  call), the streak is left alone: the count is the worker's, and what the request met is said by the cancellation
  that follows. So a user's withdrawal that fails right after a quiet period can be logged with the worker's
  earlier "failing since" — the one case in which a single fault after a quiet period shows an old streak.
- **Rarely visited operations stay at Warning.** A streak needs two faults at most two ceilings apart. With the
  worker switched off, during a deployment pause, or behind a tenant fault with many operations queued (the cycle
  stops at the first failing operation of a tenant), an operation is visited more rarely than that: every visit
  starts a new streak and logs a Warning. In that situation alert on the NUMBER of waiting operations, not on the
  level.
- **A broken start path alone never reaches Error.** A start that gets no answer is followed by the lookup, and
  "nothing arrived" is an answer. The operations wait for their makers at Warning; the alert there is the number
  of operations waiting for a resubmit (`AwaitingMakerReplay`).

**A withdrawal during an outage.** The approval still exists at the engine and may be decided at any moment, so the
operation never waits where the worker does not read. If the cancellation is refused before it is processed, the
withdrawal is forgotten, the operation awaits the decision again and the requester is told to ask again. If the
cancellation got no answer, it may still land — so the worker leaves the operation alone for a **settle window**
(`RetryDelaySeconds`, never less than 30 seconds and — the setting's own limit — never more than one hour; the same
window covers a withdrawal that was cut off between its steps) and only then reads the approval: still open → the withdrawal did not happen (forgotten, decision awaited
again); decided → the decision is applied; closed with no readable decision → the withdrawal landed, and the
operation stays at `WithdrawalOutcomeUnknown` until its requester repeats the same withdrawal, which completes it
(the worker re-reads it every `ServiceFaultMaxRetryDelaySeconds`). A withdrawal never brings the worker's next
attempt forward: an operation the worker had scheduled twelve minutes ahead is still read twelve minutes ahead.
The window is only for a cancellation that may have landed: a withdrawal that was refused before it was processed
(the user's session ran out, the service identity was refused) or found the approval closed puts the worker's
schedule back to what it was — a user clicking through refusals does not keep the decision poll waiting.
**A repeat that is refused does not undo an earlier attempt.** If a cancellation went unanswered (or was cut off)
and the user's repeat is then refused before it is processed, the operation stays a withdrawal whose outcome is
unknown: the earlier cancellation may have landed, and only the worker's read of the approval can tell.
A cancellation that lands only after the settle window has passed and the worker has read "still open" is not
covered: the decision poll then meets a cancelled approval and the operation is quarantined.

**A success MDM cannot accept** (`PRODUCT_WORKFLOW_RESPONSE_INVALID`: Platform answered 200/201 with a field MDM does
not know, or contradicting itself) is a mismatch of versions between the two services, not a fault of the record.
The operation waits, and the line is an **Error on every attempt** (one per worker attempt, at the back-off's
pace) — waiting will not mend it; deploy matching versions.

| MDM log line | Level | Means |
|---|---|---|
| `… met a service fault at {Call}: {ResultCode}; worker attempt N, failing since …; the worker retries` | Warning; **Error** from worker attempt `ServiceFaultErrorAfterAttempts` (default 5), or once the streak of consecutive faults — from its first fault to this one, with no answer in between — spans `ServiceFaultMaxRetryDelaySeconds`, or on every attempt for `…RESPONSE_INVALID` | one operation is waiting; the worker will retry it. **Alert on the Error level of this line**: it means an outage that is going on now for that operation, not a single fault after a quiet period (an answer in between starts the count and the "failing since" again) — with the exceptions listed under "Three things follow" above |
| `…; nothing was started; the maker resubmits` · `…; the maker was told to resubmit; the worker's schedule is unchanged` | same rule (the attempt number shown is the worker's and does not rise) | a user met the fault; the answer was 503 `…SERVICE_UNAVAILABLE_RESUBMIT` |
| `…; the withdrawal was dropped and its requester repeats it; the decision is awaited again` · `…; the worker reads the approval to learn whether the withdrawal landed` · `…; an earlier cancellation may have landed; the worker reads the approval to learn whether it did` | same rule | a withdrawal met the fault (see above) |
| `… the approval is closed and holds no decision (…); the withdrawal appears to have landed and waits for its requester` | Warning | only the requester's repeat can finish that withdrawal |
| `… the approval is closed and its decision could not be accepted (…); whether the withdrawal landed is not known` | Warning | the approval is closed, and what Platform says about its decision does not pass MDM's checks; the requester's repeat settles it either way |
| `… the approval moved on while it was being withdrawn (…) and is still open; the withdrawal is asked again with fresh versions` | Information | Platform's "conflict" was about versions, not about a decision; the same request goes on |
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

A wrong `ServiceFault*` value, a `LeaseSeconds` outside 10–900 or a `RetryDelaySeconds` outside 1–3600 (or above
the ceiling) keeps MDM from **starting** (`PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`) — also when the
worker is switched off, because submit and withdraw read the same values; it never turns requests into 500s.

**Still open.** A quarantined operation has no way out (BL-498): no operator action exists and the draft stays
blocked at that version. An unreadable answer, an unknown 403 or `…RESPONSE_INVALID` has no end state: the worker
keeps trying at the ceiling for ever (the last one with an Error line each time). The seven sibling processors (correction, retirement, GSKU, LSKU) still quarantine on a service
fault and have no pre-flight.

### 8. GSKU — the three flows on the same chain (WP-MDM-GSKU-ACCEPT-01)

The GSKU identity approval, the GSKU correction and the GSKU retirement request use the same client, token and worker
rules as the Global Product. What differs is only their names:

| Flow | MDM section (switch, approvers) | MDM worker section | Platform `ObjectType` | Tracked template code |
|---|---|---|---|---|
| GSKU identity | `FirstGskuIdentityWorkflow` | `FirstGskuIdentityWorkflowWorker` | `gsku` | `GSKU-IDENTITY` |
| GSKU correction | `GskuCorrectionWorkflow` | `GskuCorrectionWorkflowWorker` | `GskuCorrection` | `GSKU-CORRECTION` |
| GSKU retirement request | `GskuRetirementRequestWorkflow` | `GskuRetirementRequestWorkflowWorker` | `GskuRetirementRequest` | `GSKU-RETIREMENT-REQUEST` |

- **Tracked** (not secret): the three template codes and the identity reason code in MDM's
  `appsettings.Development.json`, and Platform's three `TrustedWorkflowStartAuthorization` entries for the default
  client. **Written by the bootstrap** into MDM's local file: `Enabled` and `CandidatePrincipalIds` of the three flows,
  `Enabled` of the three workers (a non-default `--client-code` also gets the three Platform entries).
- **One approval definition per flow and tenant** (step 4, with the template code from the table). Until it exists and
  is published, a submit or a request answers 409 `PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY`, writes nothing and
  the screen says so ("the approval template for GSKUs is not set up yet") — publish it and submit again.
- **Who decides.** A decision taken by the maker (or the requester) of the same item is **not applied** (FIX2): the
  engine has closed the approval with it and no other decision will come. The GSKU identity goes back to draft with
  the reason (`FIRST_GSKU_IDENTITY_SELF_APPROVAL_IGNORED`, shown on the screen); a correction or retirement request
  ends unapplied and the GSKU is free again. Logged as a warning.
- **Approved but not applicable.** When the parent Global Product is no longer approved, or a referenced unit is no
  longer valid, by the time the decision arrives, the GSKU goes back to draft with the reason; the maker sees it in the
  detail view in their language and submits again.

### 9. Behaviour that changed with WP-MDM-GSKU-ACCEPT-01 FIX1 (Global Product included)

- **Legal-entity scope and the workers.** In an `Enforced` tenant the workers used to stop altogether; a decision taken
  in the Task Center never reached MDM there. Now every product-identity worker (Global Product, GSKU identity, GSKU
  correction, GSKU retirement request) carries on the operations whose Global Product has a scope policy in force now
  and skips the others; `Preparation` and no rollout run everything; `FailClosedSuspended` or an unreadable rollout
  runs nothing. Live: nothing to switch on beyond the workers' own `Enabled`; an operation that stays pending in an
  Enforced tenant means its product has no policy in force.
- **A busy or silent store.** A claim the database does not answer within half the lease is given up and answered 503
  `PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE`; the request may simply be repeated.
- **A withdrawal under another command.** A second withdrawal of the same approval with a different command id is
  answered 409 and told the command the first one stands under; the Web repeats it once with that id.
- **A withdrawal whose requester never came back.** When an interrupted withdrawal can only be finished by its
  requester and 72 hours pass, the operation is handed to manual reconciliation and logged as an error
  (`…_WITHDRAWAL_REQUESTER_NEVER_RETURNED`) — alert on that line.
- **Direct GSKU retirement** (`POST /api/gskus/{id}/retire`, `mdm.gskus.retire`) is **closed for every caller**
  (FIX2): human or not, the answer is 403 `GSKU_DIRECT_RETIREMENT_SYSTEM_ONLY`, and the key cannot be granted to a
  human role. People retire a GSKU through the retirement request. It is opened only when a system actor is defined
  (an inbound service identity in MDM), for that identity alone, with its own test.

### 10. A GSKU draft and its submit — every state has a way out (WP-MDM-GSKU-ACCEPT-01 FIX2)

| Where the latest submit stands | On the screen | Edit | Submit | Way out |
|---|---|---|---|---|
| on its way to the engine | "being submitted" | no | no | automatic — the worker learns what the engine holds |
| never received by the engine | "did not reach the engine" | yes (sets the submit aside) | yes (the same submit) | the maker |
| the engine may hold it (manual reconciliation) | "manual reconciliation — the submitter must submit again" | no | yes | **the submitter's** repeat of the same submit: it reads the engine — the start is there → it goes on; nothing there → the submit ends |
| ended before the engine started anything | "edit the draft, then submit again" | yes | no (the same submit cannot be repeated) | the maker edits and submits |
| the engine approved, MDM could not apply it | the reason (parent not approved, unit no longer selectable, decided by the maker) | yes | yes | the maker fixes and submits |

Not covered yet (backlog, after D2): if the submitter has left, nobody else can settle a manual reconciliation — a
separate reconciliation endpoint with its own permission (`mdm.gskus.reconcile-identity`) is planned.

A correction or a retirement request gives the GSKU back (its lock) only when the engine is known to hold nothing for
it — refused before processing, no definition (`PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY`), closed without a
decision, decided by its maker, or its decision no longer applicable. An answer that is lost or cannot be read keeps
the lock and the start is looked up again (an approval may exist in the Task Center).

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
| `FirstGskuIdentityWorkflow`, `GskuCorrectionWorkflow`, `GskuRetirementRequestWorkflow` — `:Enabled`, `:TemplateCode`, `:CandidatePrincipalIds[]` (identity also `:ReasonCode`) | the three GSKU flows (Development step 8); each needs its own published definition and its Platform entry (§4) |
| `FirstGskuIdentityWorkflowWorker:Enabled`, `:LeaseOwner`; `GskuCorrectionWorkflowWorker:Enabled`; `GskuRetirementRequestWorkflowWorker:Enabled` | the three GSKU workers — without them a decision is never applied |
| `GskuCorrectionWorkflowWorker:QuarantineAfterFailedAttempts` (30), `:MaxRetryDelaySeconds` (900), `:RetryDelaySeconds`, `:StartSettleSeconds` (120); the same under `GskuRetirementRequestWorkflowWorker` | each retry waits twice as long, never longer than the ceiling (a ceiling below `RetryDelaySeconds` is raised to it and logged once as a Warning). After this many worker attempts in a row (0 = never) an operation is marked `*_RETRY_LIMIT_REACHED` with one Error line: if the cause is transient (engine / Platform unreachable, timeout, 5xx) it is **never** quarantined — it keeps retrying at the ceiling and heals by itself when the engine is back; any other cause goes to manual reconciliation, whose way out is the requester's same request (the Web offers it as *Repeat request*). Only the worker's attempts count — the requester's repeats neither count nor jump the wait. `StartSettleSeconds`: how long after a start whose answer was lost the worker trusts "the engine has no such instance" and ends the request |

Restart MDM last. Then create and publish the definition (Development step 4) and assign the roles (step 5).

### 5a. What to watch (WP-MDM-GSKU-ACCEPT-01 FIX3 B13, FIX4)

- Every recovery cycle of the GSKU flows writes ONE line: `{Flow} recovery cycle: tenants …, operations …, claimed …,
  advanced …, quarantined …, waiting …, failed …, in manual reconciliation …` (Information; Warning when something got
  stuck or failed; **Debug** when the cycle found nothing to do and nothing is stuck). Flows: `FIRST_GSKU_IDENTITY`,
  `GSKU_CORRECTION`, `GSKU_RETIREMENT_REQUEST`, and `GSKU_PAIR_RETIREMENT` (the pair retirement has no worker of its
  own — it runs inside the retirement request's apply step, and its count is reported by that cycle). Counts only: no
  tenant data, no secret.
- **Stuck** = the operation holds the GSKU (manual reconciliation with the lock held) or is past
  `QuarantineAfterFailedAttempts` (`*_RETRY_LIMIT_REACHED`). Only stuck operations are logged on their own (Error, with
  tenant, operation id and reason code) and counted in "in manual reconciliation" and the gauge.
- **Ended** = the request is over and the GSKU is free (`*_SELF_APPROVAL_IGNORED`, `*_DEFINITION_NOT_READY`,
  `*_START_NOT_RECEIVED`, `*_NOT_APPLICABLE`, `*_CLOSED_WITHOUT_DECISION`, `*_EVIDENCE_CONFLICT`, …). Logged as
  Information, not counted — nothing to do.
- The gauge `mdm.product_identity.recovery.manual_reconciliation` (meter `Diten.MdmService.ProductIdentityRecovery`,
  tag `flow`) carries the same "in manual reconciliation" number. MDM has no metrics exporter of its own today: read
  it with `dotnet-counters` or wire an OpenTelemetry exporter. Alert when it rises; a `*_RETRY_LIMIT_REACHED` that
  falls back to 0 by itself was an outage that healed.
- A correction or retirement request stuck in manual reconciliation is settled by its requester repeating the same
  request (same GSKU version, same facts): a decision on record is applied, an approval closed without a decision or
  never started ends the request and frees the GSKU, an open approval keeps waiting. Another user, or another version,
  cannot (409). If the engine cannot be reached (no answer, a 5xx, an unreadable or unexpected answer, a refused
  service identity) the repeat answers 503: a request whose approval is on record goes back to waiting for its decision
  (AwaitingDecision); one with nothing on record stays as it is — repeat later. If the engine answers something about
  the record itself (`WORKFLOW_START_RECOVERY_CONFLICT`, a tenant mismatch), the repeat answers 409
  `*_MANUAL_RECONCILIATION_NEEDED`: a person has to reconcile it (BL-530).
- What the engine's terminal evidence means (FIX5, Platform BL-547): `WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION`
  (cancelled in the Task Center, timed out) — the request ENDS and the GSKU is free (an identity submit goes back to
  draft); `WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT` — the engine's facts contradict each other: STUCK, the lock held;
  `WORKFLOW_DECISION_NOT_TERMINAL` — still open, an SLA-escalated approval included: it WAITS.
- A refused GSKU write: the audit capacity, the store or a lease held elsewhere WAIT (never quarantined); a concurrent
  write is tried again (counted); anything else (an idempotency or contract conflict, a lock that is not the request's,
  a GSKU that is gone) is STUCK at once.
- A start whose answer was lost keeps the GSKU's lock until the engine says what it holds: a refused LOOKUP (an
  identity not granted, a rotated key, a 403) never gives the lock back — only the START call's own "nothing was
  processed" refusal does (FIX6). Otherwise a lost start could leave an approval open in the Task Center that nobody
  would ever apply.
- `mdm.product_identity.recovery.needing_a_person` (same meter, tag `flow`) counts the stuck requests their requester's
  repeat cannot settle — a write refused for a reason that does not pass (an idempotency or contract conflict, a lock
  that is not the request's, a GSKU that is gone or drifted, a refused admission, a quarantined pair retirement). Only a
  person can release those (BL-530): alert on any value above 0. The cycle line says it as "needing a person".
- The "is stuck" Error line is written ONCE, when a request becomes stuck, with the cause the attempt before it met
  (the record then holds only `*_RETRY_LIMIT_REACHED`); while it keeps retrying at the ceiling the next cycles say
  "still stuck" as Information.

### 6. Not covered here — open before a live bring-up

- **Auth's tenant resolution on the token endpoint.** Outside Development a request to the token endpoint without a
  resolved tenant is refused before it reaches the endpoint; MDM's request carries none. Not solved by this work package.
- **Rotation and revocation.** Not done from configuration. Rotation is the operational command of pack Section E,
  which cannot run against a real database today (pack §F, "why this section exists").
- **Definition and approver set-up per tenant** is manual (Development steps 4–5).
