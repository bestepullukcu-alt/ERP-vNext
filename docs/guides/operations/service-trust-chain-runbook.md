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

Nothing seeds it at start-up. It must have **one** approval step: MDM requires the deciding task to be the task the start
created.

**Now with a command (WP-PLT-WORKFLOW-TEMPLATES-01):** the seven product-identity definitions (`GLOBAL-PRODUCT-IDENTITY`,
`GSKU-IDENTITY`, `GSKU-CORRECTION`, `GSKU-RETIREMENT-REQUEST`, `LSKU-IDENTITY`, `LSKU-RETIREMENT-REQUEST`, `ITEM-ACTIVATION`)
are installed from Platform's catalog `Seed/workflow-templates/product-identity-templates.json` with
`scripts/dev/ensure-workflow-templates.sh --tenant <guid> [--apply]` (or `.ps1 -Tenant <guid> [-Apply]`; live: the host's
`--ensure-workflow-templates --tenant <guid> [--apply]`). Without `--apply` it only lists what it would do. A definition that
already exists is never overwritten: published and equal → `already-published`; published and different → `drift` (exit 2);
designed by the tenant → `needs-owner` (exit 2). `MDM-COMP-PROPOSAL` is not in the catalog (tenant-specific). The manual
calls below remain the alternative.
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
| LSKU identity | `LskuIdentityWorkflow` | `LskuIdentityWorkflowWorker` | `lsku` | `LSKU-IDENTITY` |
| LSKU retirement request | `LskuRetirementRequestWorkflow` | `LskuRetirementRequestWorkflowWorker` | `LskuRetirementRequest` | `LSKU-RETIREMENT-REQUEST` |

*LSKU rows (WP-MDM-LSKU-ACCEPT-01 FIX1 B4, FIX2 item 12):* the same rules. Tracked: the two template codes, the identity
reason code `LSKU_IDENTITY_APPROVAL` and the identity worker's `LeaseOwner` in MDM's `appsettings.Development.json`;
Platform's two `TrustedWorkflowStartAuthorization` entries (`lsku` / `LSKU-IDENTITY`, `LskuRetirementRequest` /
`LSKU-RETIREMENT-REQUEST`) for the default client — Platform matches the client, service, audience, object type and
template code exactly. Written by the bootstrap: `Enabled` and `CandidatePrincipalIds` of both flows and `Enabled` of
both workers (a non-default `--client-code` also gets both Platform entries). A local file written before FIX1 lacks the
LSKU sections and is reported as "not a complete setup": move it aside and run the bootstrap again. One published
approval definition per LSKU flow and tenant (step 4). The stuck child-creation admission gauge
(`GSKU_CHILD_ADMISSION`, every child kind) runs on its own worker (`GskuChildAdmissionGauge:Enabled`, default on,
`:IntervalSeconds` 300) — it does not need any LSKU worker to be on. It counts admissions on GSKUs only: the Global
Product's own `ChildCreationAdmissions` (GSKU creation under a GP) are not counted by it.

*LSKU gauges (FIX3 items 2, 12, 14, 15):* the same worker also records the `LSKU_IDENTITY` flow, so it is counted even
while the identity worker is off; `mdm.product_identity.recovery.count_failed{flow}` = 1 says the last count failed.
Each count has its own switch (FIX4 item 12): `GskuChildAdmissionGauge:Enabled` (the admission count) and
`GskuChildAdmissionGauge:LskuIdentityEnabled` (the `LSKU_IDENTITY` gauges), both default on; the worker runs while
either is on, so turning the admission count off no longer silences the LSKU identity gauges. A wait at its limit is
logged as an Error ONCE, by the recovery runner when the operation newly becomes stuck (FIX4 item 6); a repeat by the
maker keeps the limit and records the current cause in `RetryLimitCauseCode`. *Legacy submits (FIX5, information):* an identity operation written
before the start-sent mark existed counts its 2-minute settle window from its last update read before the claim; every
refused attempt on it writes a new update, so the window starts again after each refusal — such an operation is only
closed (draft cancel, reconciliation) once it has been left alone for the window (measured: 0 such documents in Dev).

| gauge (`flow=LSKU_IDENTITY`) | counts | what to do |
|---|---|---|
| `manual_reconciliation` | stuck (manual reconciliation), parked for the maker (`AwaitingMakerReplay`), and a wait at its limit (Retryable with `LSKU_IDENTITY_WORKFLOW_RETRY_LIMIT_REACHED`: a provider or store that keeps refusing — e.g. a wrong service identity, 401/403 — keeps the approval waiting, counted, logged once) | remove the cause; the next worker attempt succeeds and the count resets |
| `needing_a_person` | stuck operations their requester's repeat cannot settle, and a withdrawal whose cancel was left unanswered for more than an hour (`WithdrawalOutcomeUnknown` / parked for its maker) | ask the requester to repeat the same withdrawal; otherwise escalate |

Operator repair of leaked admissions (`POST /api/lskus/operator/child-admission-repairs`, recover key): an admission is
never released while a live row or a live code can still become the child. Past the cutoff the repair first BURNS a
code that can no longer become one — consumed for an identity whose row never came, bound to a cancelled row, or
reserved and never consumed — and only then releases the admission, with an audit intent on the GSKU in the same
write. A refused burn keeps the admission and is reported (`LSKU_ADMISSION_REPAIR_BURN_REFUSED:<code>`).

**Legal-entity scope (WP-MDM-SCOPE-INT-01 FIX1 / FIX2).** The repair is a non-human actor and follows the background
workers' scope rule (the same endpoint serves `POST /api/finished-goods/operator/child-admission-repairs` for FG):

| tenant | what the repair does | answer |
|---|---|---|
| no rollout / Preparation | every orphan, as before | `found`, `released`, `items` |
| Enforced | only orphans whose GSKU's product is in scope (a policy in force); the others are skipped and only counted — their codes and admissions are untouched, their ids never returned | `outOfScope` = skipped orphans over every page walked |
| FailClosedSuspended / a fence up / a rollout that cannot be read | refused | `409 PRODUCT_SCOPE_FAIL_CLOSED_SUSPENDED` / `409 PRODUCT_SCOPE_WRITE_FENCE_ACTIVE` / coded `503` — from the write fence, before the repair runs; `409 LSKU_ADMISSION_REPAIR_SCOPE_CLOSED` from the repair itself only on a direct call or a race with a suspension |

The repair walks the orphans page by page (GSKU id order) until it has `limit` in-scope ones or reaches the end, so
out-of-scope orphans in front never hide the ones behind them. The walk is bounded (50 pages of 100 GSKUs); when it
stops at that bound `truncated` is `true` — run it again. A run that ended in an error keeps its writer lease until it
expires (~120 s); during that time an activation / suspension fence waits, then goes on.

**A burn needs a replica set (FIX4 item 11).** Burning a code is one Mongo transaction (it reads "no live row bound" and
burns in the same transaction). On a standalone `mongod` every burn is refused with
`CODE_RESERVATION_BURN_TRANSACTION_UNAVAILABLE` (503): an LSKU draft cancel deletes the draft and answers 503 (its code
stays consumed, never burned), and a creation's repeat or this repair cannot burn either — the admission stays, counted.
This is the GSKU S5 stance (no transaction, no irreversible step); run MDM on a replica set (a single-node one is
enough). Only the LSKU kind can be burned today: an FG / GSKU / GP code burn is refused with
`CODE_RESERVATION_BURN_KIND_NOT_TRANSACTIONAL` until that insert writes its reservation inside its own transaction.

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

### 11. Item — the activation approval (MOD-0290-FU04 S2, flow `ITEM_ACTIVATION`)

The item's activation (Draft → Active) and reactivation (Deactivated → Active) go through one approval on the same chain:
the same client, token and Platform endpoints as the flows above. What differs:

| | Value |
|---|---|
| MDM section (switch, approver, template) | `ItemActivationWorkflow` — `:Enabled`, `:TemplateCode` (tracked: `ITEM-ACTIVATION`), `:ApproverPositionId`, `:ReasonCode` (tracked: `ITEM_ACTIVATION_APPROVAL`), optional `:DueAfterSeconds` |
| MDM worker section | `ItemActivationWorkflowWorker` — `:Enabled`, `:LeaseOwner` (tracked: `mdm-dev-local-item`), `:StartSettleSeconds` (120; 1–600) |
| Platform `ObjectType` / template code | `Item` / `ITEM-ACTIVATION` — Platform's `TrustedWorkflowStartAuthorization` lists exactly `{Diten.MDM, TRUSTED_WORKFLOW_CONSUMER, Item, ITEM-ACTIVATION}` for the default client (tracked in `appsettings.Development.json`) |
| Approver | the **QA Manager position** (OD-7). MDM sends the candidate `position:{ApproverPositionId}`; the engine resolves the position's current holders and the first one decides (GP-1b). The id is configuration — never code. A vacant position: Platform answers the start 503 "candidates required" (`WorkflowInstanceStartCoordinator`, an empty seat is transient); the operation waits for the maker's replay (`AwaitingMakerReplay`) and **the submit fence is held** — the item stays Draft / Deactivated and refuses edits (`ITEM_ACTIVATION_SUBMIT_IN_FLIGHT`) until the maker submits again; `manual_reconciliation` counts it. Fill the seat, then the maker submits again (the LSKU behaviour). The id is one value for the whole deployment: a tenant whose QA Manager position has another id fails closed (known limit). |
| Maker ≠ approver | the maker's own decision is never applied: the item goes back (Draft, or Deactivated for a reactivation) with `ITEM_ACTIVATION_SELF_APPROVAL_IGNORED`. The engine also drops the starter from the candidates. |

**Not written by the bootstrap.** `setup-service-trust.*` writes the switches and approvers of the GP / GSKU / LSKU flows
only; the item flow's `Enabled`, `ApproverPositionId` and the worker's `Enabled` go to the git-ignored
`appsettings.Development.local.json` by hand:

```json
{
  "ItemActivationWorkflow": { "Enabled": true, "ApproverPositionId": "<the QA Manager position's id>" },
  "ItemActivationWorkflowWorker": { "Enabled": true }
}
```

A non-default `--client-code` gets no Platform `Item` entry from the bootstrap (named gap, `DevTrustBootstrapCommand.cs`
`GskuStarts` / `LskuStarts`): add the entry by hand for that client.

**Dev set-up, in order (the owner enters shared dev data; CT measures):**
1. Organisation → Positions: create (or pick) the **QA Manager** position in the tenant, Active; assign at least one person
   who is NOT the item's maker (two people are proposed, OD-7). Copy the position's id.
2. Platform → Workflow definitions: create `ITEM-ACTIVATION` and publish it with one approval step (step 4 above). The
   step **must NOT define candidates** (`assignment.candidatePrincipalIds` empty): a step's candidates win over the
   `position:<ApproverPositionId>` MDM sends, and MDM does not check that the person who decides holds the QA Manager
   position — a definition with its own candidates silently changes who may approve.
3. MDM local file: the JSON above; restart MDM.
4. Item keys: `mdm.items.submit` (manifest 1.1.0, S5) and `mdm.items.withdraw` (manifest 1.2.0, ITEMS `WITHDRAW_APPROVAL`,
   S2 lane merge FIX1) are declared by MDM's module manifest; once MDM has registered it, grant them to roles in the
   Role screens. The Items page offers "Withdraw Approval Request" only to the person who submitted, holding
   `mdm.items.withdraw`.

**The worker reads the item lists with MDM's service identity** (S2 FIX1): no user is present when the approval is
applied, so the reference check runs with the trusted service token for the tenant (audience
`TRUSTED_REFERENCE_DATA_CONSUMER`, the GSKU workflow resolver's identity provider `AuthWorkflowVerifiedMarketServiceIdentityProvider`
and the `VerifiedGskuResolver` credential). Platform's item-universal endpoint accepts it. If Platform or Auth refuses it
(401 after one refresh, 403, or no identity issued), the approval waits with `ITEM_REFERENCE_PROVIDER_CONFIGURATION_ERROR`
(an error line, `needing_a_person`) and the item stays PendingActivation; it is asked again every hour, uncounted, so it
heals by itself once the identity / credential configuration is fixed (no one has to submit again). This wait is the
worker's only: a refusal of the maker's own token on their repeat answers the maker 503 and the operation stays on its
ordinary retry. An outage (5xx,
timeout) only waits. An instance that runs the item approval worker checks this identity's configuration at start
(`ITEM_ACTIVATION_SERVICE_IDENTITY_CONFIGURATION_INVALID` stops the start). A user's request (the maker's repeat) reads
the lists with the user's own token — the service identity is the worker's only.

**Approval on, worker off** (S2 FIX1): not refused at start (in a multi-instance deployment one instance carries the
worker). Each instance with the approval on and its worker off says so once at start (Warning). The gauge
`ITEM_ACTIVATION_UNPROCESSED` (flow tag on the recovery gauges) counts approvals the worker should be carrying with no real
move (a checkpoint write — a claim alone is not progress) for `ItemActivationGauge:UnprocessedAfterMinutes` (default 90,
20–1440, and above the longest planned wait — the 1-hour configuration retry — + the worker's poll interval + lease;
checked at start, an invalid value stops the start).
An open approval is asked again at half its age, from the retry delay up to every 15 minutes (never counted toward the
retry limit). `mdm.product_identity.recovery.oldest_open_approval_age` (unit `s`) is the age of the oldest approval still waiting
for its decision (a number, no tenant).

**The activation gate** (contract draft.11 "Etkinleştirme kuralı", R-04): at the submit and again when the approval is
applied, the item needs `lotControlled` filled, a base unit, at least one storage condition unless it is packaging (O-25),
and conversions that pass the chain rules (≤ 4 edges from the base unit). `serialControlled` is always false for an
item. Missing → 422 `ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION` with the missing field names (at the submit), or the
approval is not applied and the item goes back with that code (at the decision).

**Every state has a way out:**

| Where the request stands | Item | Way out |
|---|---|---|
| on its way to the engine | Draft / Deactivated, edits refused (`ITEM_ACTIVATION_SUBMIT_IN_FLIGHT`) | automatic — the worker learns what the engine holds |
| never received by the engine | as before | the maker submits again |
| the engine may hold it (manual reconciliation) | **held** — PendingActivation, not editable | the **maker's same submit** re-reads the engine (start there → goes on; nothing there after the settle window → ends); otherwise a person (`needing_a_person`) |
| pending, the engine open | PendingActivation | the approver decides, or the maker withdraws (`POST /api/mdm/items/{id}/withdraw`) |
| rejected / closed without a decision / decided by the maker / gate no longer met | back: Draft, or **Deactivated** for a reactivation | the maker fixes and submits again |

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
| `GskuCorrectionWorkflowWorker:QuarantineAfterFailedAttempts` (30), `:MaxRetryDelaySeconds` (900), `:RetryDelaySeconds`, `:StartSettleSeconds` (120; 1–600, larger stops the host at start); the same under `GskuRetirementRequestWorkflowWorker` | each retry waits twice as long, never longer than the ceiling (a ceiling below `RetryDelaySeconds` is raised to it and logged once as a Warning). After this many worker attempts in a row (0 = never) an operation is marked `*_RETRY_LIMIT_REACHED` with one Error line: if the cause is transient (engine / Platform unreachable, timeout, 5xx) it is **never** quarantined — it keeps retrying at the ceiling and heals by itself when the engine is back; any other cause goes to manual reconciliation, whose way out is the requester's same request (the Web offers it as *Repeat request*). Only the worker's attempts count — the requester's repeats neither count nor jump the wait. `StartSettleSeconds`: how long after a start whose answer was lost the worker trusts "the engine has no such instance" and ends the request |

Restart MDM last. Then create and publish the definition (Development step 4) and assign the roles (step 5).

**Item activation (MOD-0290-FU04 S2) — live deployment steps:** Platform `TrustedWorkflowStartAuthorization:Entries[]`
gets `{ClientId, Diten.MDM, TRUSTED_WORKFLOW_CONSUMER, Item, ITEM-ACTIVATION}` (the live base `appsettings.json` lists no
entry — every environment configures its own); MDM gets `ItemActivationWorkflow:Enabled`, `:TemplateCode`,
`:ApproverPositionId` (the live QA Manager position's id), `:ReasonCode`, and `ItemActivationWorkflowWorker:Enabled`,
`:LeaseOwner`; the `ITEM-ACTIVATION` definition is published in the live tenant. Without the worker a decision is never
applied (production runs with background jobs off — decide it with the owner).

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
  A request that reaches the retry limit on an answer about the record says so (`*_STUCK_ON_RECORD_ANSWER`, or
  `*_STUCK_ON_RELEASE_CONFLICT` for a lock release that kept conflicting) and is counted there; one that reaches it on a
  service fault stays `*_RETRY_LIMIT_REACHED`, keeps retrying and is not a person's (FIX7).
- A switched-off worker (identity, correction, retirement request) still records both gauges — counts only, nothing is
  claimed or written — every `StuckGaugeIntervalSeconds` (15–3600, default 60), not at the poll interval (BL-552 flow 3
  FIX3). It reads only stuck requests, through partial indexes that hold nothing else
  (`ix_mdm_gp_identity_workflow_stuck`; `ix_mdm_gp_correction_stuck_manual` + `…_stuck_at_limit`;
  `ix_gp_retirement_stuck_manual` + `…_stuck_at_limit`), so the scan does not grow with finished history. The
  correction and retirement indexes are created by the service on first use (the first stuck scan or reservation);
  the identity index with the store's other indexes.
- The MDM Mongo user needs `createIndex` on these collections (the stores create their indexes at runtime). For a
  least-privilege user, either grant it on the MDM database or create the indexes above once before the deploy; with
  neither, the stuck scan fails every interval with an Error line and the gauges are not recorded. The identity store
  creates its indexes synchronously when it is built, i.e. once per poll for each tenant with stuck work; the sparse
  gauge interval keeps that cost to one round per interval while the worker is off.
- No request is left in manual reconciliation while it is waiting: if the requester's repeat would end a request but
  the lock release has to wait (the cap, the store, a concurrent write), the request goes back to where the worker looks
  (its decision, or the start's outcome) and the worker releases it. The store refuses "manual reconciliation + retryable".
- The "is stuck" Error line is written ONCE, when a request becomes stuck, with the cause the attempt before it met
  (the record then holds only `*_RETRY_LIMIT_REACHED`); while it keeps retrying at the ceiling the next cycles say
  "still stuck" as Information.
- **Global Product correction (BL-552, flow `GLOBAL_PRODUCT_CORRECTION`)** follows every rule above, with the Global
  Product as the locked record:
  - **Cycle line and gauges:** the same cycle line and both gauges.
  - **Settings:** `GlobalProductCorrectionWorkflowWorker` takes `QuarantineAfterFailedAttempts` (30),
    `MaxRetryDelaySeconds` (900) and `StartSettleSeconds` (120), as the GSKU workers do.
  - **Lock release:** the product's lock is given back by a conditional write — this request's binding, at the
    version the product is at, in its tenant only. The write carries the request's own ENDED audit operation
    (`GlobalProductCorrectionEnded`, flow 2 FIX1 item 12) with the reason it ended in its facts, so central audit
    tells "ended, nothing held" from "stuck". A stuck request writes no audit of its own. Platform maps the new
    operations (`GlobalProductCorrectionEnded`, `GlobalProductRetirementEnded`,
    `GlobalProductIdentityDecisionNotApplicable`) as lifecycle transitions. **Deploy Platform first.** Platform answers
    an operation it does not map with 409 `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED`. For every operation the DEPLOYED
    Platform does not map yet — in this first release all produced product operations (no product module is live; the
    map is not on `origin/main`), the never-produced 43 / 47 excepted; base and rule in `CentralMappingRollout` —
    MDM's audit delivery then waits instead of dead-lettering: the intent stays retryable with that reason, for at most
    2880 attempts (about a day at the default 30 s retry delay), and is delivered once Platform maps it. After that
    bound — or for any other operation — the intent is dead-lettered, never reaches the central log, and there is no
    tool to send a dead-lettered intent again (backlog BL-566). Watch: the delivery worker writes one Warning per
    cycle while any wait — "Audit delivery: N intent(s) of <operations> in M tenant(s) wait for Platform to map the
    operation" — and the intents' last delivery error is `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED`. The attempt at the
    bound is dead-lettered and no longer counted as waiting.
  - **Refused writes:** the product's audit cap (`AUDIT_INTENT_CAPACITY_EXCEEDED`) waits.
    `GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT` is a counted retry.
  - **An approved name another product took in the meantime:** the request ENDS unapplied and the product is given
    back (`GLOBAL_PRODUCT_CORRECTION_NAME_CONFLICT`, the reason on record). The name stays unique; the maker opens a
    new request with another name. Nothing waits for a person (BL-552 FIX1, CT decision — the GSKU "no longer
    selectable" rule).
  - **Different from the Global Product correction before BL-552 (not from GSKU, which does the same):**
    - a correction its own maker decided ends unapplied and frees the product
      (`GLOBAL_PRODUCT_CORRECTION_SELF_APPROVAL_IGNORED`); before, it was stuck for ever;
    - an approved name that was taken ended stuck with the lock held; now it ends and frees the product.
  - **Interrupted requests and audit delivery:** a repeat of a request cut off after it took the lock is recognised
    even after the audit delivery worker compacted its intents into receipts; the product held by that very request
    is its own lock, and the request goes on.
  - **Status codes:** a refusal that passes by itself (the audit cap, the store) answers 503 and ENDS NOTHING: the
    request waits, and the maker's same key goes through once it has passed (flow 2 FIX1 item 8). A start the engine
    refused for the maker keeps the maker's code (session expired 401, no start permission 403); one refused for how
    the engine treats this service (its trust, its grant — e.g. `WORKFLOW_TRUSTED_START_FORBIDDEN`) ends the request
    with `PRODUCT_IDENTITY_WORKFLOW_SERVICE_UNAVAILABLE_RESUBMIT` (503), as the identity approval says it.
  - **A request that never took the product** (cut off before its lock) holds nothing; the worker leaves it waiting
    for its maker's same key (`*_AWAITING_MAKER`, looked at again at the retry ceiling) for at most 7 days from its
    creation. After that it ENDS with `*_MAKER_NEVER_RETURNED` (nothing to give back) and the maker's repeat is told
    so (409); the maker sends a new request.
  - **Rows of the previous version (124e30acc and before):** a request it left stuck on a name conflict, with the
    product one version past its base + 1 and the lock still held, is ENDED by its requester's same command: the
    product is given back at its current version. Dev count at 2026-10-06: 0 such rows.
  - **Scope:** the worker follows the legal-entity scope rule of every product-identity worker (an out-of-scope or
    suspended product is not touched).
- **Global Product retirement request (BL-552, flow `GLOBAL_PRODUCT_RETIREMENT_REQUEST`)** follows the same rules as
  the correction above, with the same settings on `GlobalProductRetirementRequestWorkflowWorker`:
  - **Approval closed without a decision, or decided by its own maker:** the request ENDS and the product is given back
    (it stays approved).
  - **An approved retirement that meets a dependant** (an approved or draft child found after the request,
    `DEPENDENT_IDENTITIES_EXIST` / `DRAFT_CHILD_CANCELLATION_REQUIRED`): the request ENDS "not applicable" and the
    product is given back — the GSKU retirement request's rule. Before BL-552 it was stuck with the lock held.
  - **Everything else** (lookup apart from start, settle window, write refusals, the limit's cause, the gauges, a
    request cut off after the lock, compacted intents, scope, status codes, the ended audit, the previous version's
    rows) as for the correction. Its store refuses every move into manual reconciliation from a finished request.
- **Global Product identity approval (BL-552 flow 3, flow `GLOBAL_PRODUCT_IDENTITY`)** follows the same pattern:
  - **The engine's three answers:** an approval closed without a decision (or decided by its own maker) sends the
    product back to draft (`GlobalProductIdentityDecisionNotApplicable`); one still open waits; contradicting evidence
    is stuck and the submitter's same submit reads the engine again.
  - **A submit stuck in manual reconciliation HOLDS THE DRAFT (CT decision S2, as GSKU):** an edit is refused
    (`GLOBAL_PRODUCT_IDENTITY_RECONCILIATION_REQUIRED`) and so is a new submit of the same version. The screen says
    "a submission of this product is stuck — the person who submitted it resolves it by pressing Submit again; if that
    does not resolve it, an administrator has to" (`ErrorSubmitStuck`, seven languages). Ways out: the submitter's
    same submit (settles it), or a person (BL-530) when the record's answer will not change; a repeat that cannot
    settle it hands it to a person (`*_STUCK_ON_RECORD_ANSWER`). Both gauges count it (`needing_a_person` alert) —
    on every cycle, also while the worker is switched off.
  - **Settings:** `ProductIdentityWorkflowWorker` takes `QuarantineAfterFailedAttempts` (30; 0–1000, 0 = never)
    and `StartSettleSeconds` (120; 1–600). A value outside those bounds STOPS THE HOST AT START, whether the worker
    is switched on or not (the interactive submit reads both); before, it was quietly clamped. The Global Product
    correction and retirement, GSKU correction / retirement request and LSKU identity / retirement request workers
    take the same `StartSettleSeconds` bound (1–600; `StartSettleWindow`).
  - **Platform süpürme yaşı (WorkflowStartRecovery AbandonAfterMinutes) MDM yerleşme penceresinden en az 5 dk uzun
    olmalı.** Platform closes a half-started workflow that has no task after that age (at least 15 min, default 30);
    a longer MDM settle window would keep MDM waiting on a start Platform has already closed. Operator rule: Platform
    `AbandonAfterMinutes` × 60 ≥ MDM `StartSettleSeconds` + 300 — the 600 s cap (15-min floor − 5 min) keeps it for any
    Platform setting (BL-552 flow 3 FIX3 EK, XMC-001; SETTLE-CAP FIX1).
- **Rolling back past BL-552:** stop the new version first (no instance may write while the fields are removed).
  The old version does not wait for Platform's map: intents of operations 79 / 80 / 81 still waiting when it takes
  over are dead-lettered by it (BL-566, no resend tool). The request and operation documents gained fields (`FailedAttemptCount`,
  `LastStartSentAtUtcTicksV1`). This binary reads documents with fields it does not know, and the service does not
  start if that guard is missing (`GlobalProductLifecycleClassMaps`). Older binaries do not read them — **when
  rolling back, remove the new fields first** with
  `$unset: { FailedAttemptCount: "", LastStartSentAtUtcTicksV1: "" }` on:
  - back to 75c286f7d or 124e30acc (no guard at all): `mdm_global_product_correction_operations`,
    `mdm_global_product_retirement_request_operations` and `mdm_global_product_identity_workflow_operations`;
  - back to 1c7cb196b (guard for correction and retirement only, flow 2): `mdm_global_product_identity_workflow_operations`
    — its identity map has no guard and the identity fields came in flow 3 (23fb2d85f).

### 5c. Item approval — what to watch (MOD-0290-FU04 S2)

- Gauges (meter `Diten.MdmService.ProductIdentityRecovery`, tag `flow`), counted every 5 minutes by their own worker
  (`ItemActivationGauge:Enabled`, on by default — independent of the recovery worker):
  - `flow=ITEM_ACTIVATION` `…manual_reconciliation` — stuck item approvals: manual reconciliation, waiting for the maker's
    replay, or a wait at its retry limit (`ITEM_ACTIVATION_WORKFLOW_RETRY_LIMIT_REACHED`). Each holds its item.
  - `flow=ITEM_ACTIVATION` `…needing_a_person` — of those, the ones the maker's repeat cannot settle (BL-530), and
    withdrawals whose cancel has been unanswered for over an hour. **Alert on it.**
  - `flow=ITEM_ORPHAN_CODE` `…manual_reconciliation` — `IT-` codes held for more than 15 minutes with no item row. Swept by
    `POST /api/mdm/items/operator/orphan-code-repairs?limit=…&minimumAgeMinutes=…` (key
    `mdm.product-identity.lifecycle-operations.recover`): a consumed code is burned (never reused); a reserved-but-never-
    consumed code is counted, not burned. `minimumAgeMinutes` is 15–525600 (the gauge's own age is the floor: a younger
    code may still be consumed by its create's repeat); outside → 400 `ITEM_ORPHAN_CODE_REPAIR_INVALID`.
  - `flow=ITEM_ACTIVATION_UNPROCESSED` `…manual_reconciliation` — approvals the worker should be carrying with no real move
    for `ItemActivationGauge:UnprocessedAfterMinutes` (default 90; above the 1-hour configuration retry + poll + lease,
    checked at start): the approval is on and no worker runs it.
  - `mdm.product_identity.recovery.oldest_open_approval_age` (`flow=ITEM_ACTIVATION`, unit `s`) — the age of the oldest
    approval still waiting for its decision (0: none). Its own gauge, never on `manual_reconciliation`; a failed read sets
    `count_failed` for `flow=ITEM_ACTIVATION_OLDEST_OPEN_APPROVAL_SECONDS`. Alert on an age past what the QA Manager seat
    should take (e.g. several days).
  - `…count_failed` = 1 for a flow: its last count failed, the numbers above are stale.
- Newly stuck operations are logged once (Error) by the recovery cycle: `ITEM_ACTIVATION operation … is stuck (…)`.

### 5b. Finished Good sales-box GTIN index (WP-MDM-ITEM-MASTER S3)

The sales-box GTIN is unique among a tenant's non-retired Finished Goods: a unique partial index
`ux_finished_goods_tenant_gtin_live_v1` on `mdm_finished_goods` (`TenantId`, `Gtin`; partial filter `Gtin` exists and is a
string, `LifecycleStatus` `$in` Draft / PendingIdentityApproval / IdentityApproved). MDM builds it at start-up
(`ProductStockBehaviourIndexInitializer`): the start never fails on it, waits at most 10 s, and retries every minute.

- **MongoDB version:** the `$in` partial filter is measured on MongoDB **7.0** (7.0.28) — run MDM on 7.0 or later. An older
  server is not verified.
- **Watch:** `mdm.stock_behaviour.gtin_index_missing` = 1 means the index is not built; uniqueness then rests on the pre-write
  check alone (two simultaneous assignments of one GTIN could both land). `mdm.stock_behaviour.gtin_index_build_failed{error}`
  counts the failed builds by error type (also logged as a warning).
- **If the build keeps failing on a duplicate** (an `E11000` in the log): two live Finished Goods already hold one GTIN.
  Find them read-only (never correct by a direct write):

```js
db.mdm_finished_goods.aggregate([
  { $match: { Gtin: { $type: "string" }, LifecycleStatus: { $in: [1, 2, 3] } } },
  { $group: { _id: { tenant: "$TenantId", gtin: "$Gtin" }, ids: { $push: "$_id" }, n: { $sum: 1 } } },
  { $match: { n: { $gt: 1 } } }
])
```

  The way out depends on the holder: **a Draft** — cancel that draft (the cancel gives the GTIN back, below); **an
  identity-approved Finished Good** — its GTIN is locked (O-8) and there is NO correction step yet (the MOD-0023
  critical-field correction is a later slice): the owner / Control Tower decide, MDM offers no exit today.
- A cancelled Finished Good draft gives its GTIN back in the cancel itself (S3 FIX1) and keeps it as `ReleasedGtin`
  (S3 FIX2, traceability); it no longer blocks a new FG.

### 6. Not covered here — open before a live bring-up

- **Auth's tenant resolution on the token endpoint.** Outside Development a request to the token endpoint without a
  resolved tenant is refused before it reaches the endpoint; MDM's request carries none. Not solved by this work package.
- **Rotation and revocation.** Not done from configuration. Rotation is the operational command of pack Section E,
  which cannot run against a real database today (pack §F, "why this section exists").
- **Definition and approver set-up per tenant** is manual (Development steps 4–5).

---

<!-- Merge WP-CT-LANE-BL521-MERGE-01 — the lane and BL-521 each created this file. The lane's MDM → Auth → Platform runbook
     stays as written above; BL-521's chain (its own sections, in Turkish, as delivered) follows below. -->

> **Servis güven zinciri — BL-521.** Bu belge servisler arası güvenin dağıtımda nasıl
> korunacağını anlatır: bir servisin kararını başka bir servisin cevabına dayandırdığı yerlerde
> hangi sırayla çıkılır, neyin geri alınamayacağı. Her bölüm bir zinciri anlatır; yeni bir zincir
> doğduğunda yeni bölüm eklenir.

## 1 · Platform yönetici kaydı → Auth belirteç basımı (BL-521)

**Zincir.** Auth, bir platform aktörüne belirteç bastığı her kapıda — giriş
(`POST /api/platform-auth/login`), zorunlu parola değişimi
(`POST /api/platform-auth/change-password/forced`), yenileme (`POST /api/auth/refresh-token`) —
belirtecin `actor_type`'ını (`platform_admin` / `partner_admin`) Platform'daki yönetici
**kaydından** alır: `GET /api/internal/platform-administrators/status` cevabındaki
`actorType` alanı. Auth'un kullanıcı üzerindeki kendi kopyası (`User.PlatformActorType`) tür
kararında kullanılmaz.

**Sıra: önce Platform (bütün kopyalar), sonra Auth.**

| adım | ne | neden |
|---|---|---|
| 1 | Platform'un **bütün kopyaları** yeni sürüme çıkar | Durum cevabındaki `actorType` alanını yalnız yeni Platform gönderir. |
| 2 | Platform'un her kopyasının durum ucuna `actorType` döndürdüğü doğrulanır | Yük dengeleyici arkasında tek bir eski kopya kalırsa Auth o kopyaya düşen her istekte türü okuyamaz. |
| 3 | Auth yeni sürüme çıkar | Yeni Auth, **gönderilmiş ama bilinmeyen** bir türü **kesin "hayır"** sayar (giriş ve yenileme reddedilir, yenilemede yöneticinin bütün oturumları iptal edilir). Türü **hiç gönderilmemiş** bir cevap ise (eski Platform) FIX3'ten beri **cevap yok** sayılır: 503, oturum iptali yok. |

**Ters sıra ne yapar.** Auth önce çıkarsa, eski Platform'un `actorType` taşımayan her durum
cevabı **cevap yok** sayılır (BL-521 FIX3 madde 5): hiçbir platform yöneticisi giriş ya da
yenileme yapamaz, Platform çıkana kadar her deneme 503 alır — bir **kesinti**dir, oturum silme
değil; Platform çıktığında kendiliğinden düzelir. (FIX2'de bu durum "bilinmeyen tür" sayılıyor
ve yenileme yapan her yöneticinin oturumlarını iptal ediyordu.)

**Geri alma.** Auth çıktıktan sonra Platform geri alınırsa, geri alınan Platform `actorType`
göndermez ve yukarıdaki kesinti yaşanır (oturumlar silinmez). Geri alma gerekiyorsa sıra yine:
önce Auth, sonra Platform.

**E-posta değiştirilemez (FIX4 madde 1; FIX3'ün e-posta taşıması kalktı).** Bir platform
yöneticisinin e-postası değiştirilemez: Platform 409 `PLATFORM_ADMINISTRATOR_EMAIL_IMMUTABLE` verir;
yeni adres yeni yönetici olarak davet edilir, eski kayıt kaldırılır. Senkron
(`POST /api/platform-auth/platform-admins/sync`) e-postayı yalnız hesabı **bulmak** için kullanır;
`previousEmail` gönderilmez, gönderilse de yeni Auth okumaz, hiçbir hesap başka adrese taşınmaz.
Bu madde için dağıtım sırası önemsizdir (eski Auth da hesabı aynı adresle arar).

**Durum ve roller (FIX4 madde 2 ve 5).** Senkron gövdesinde kaydın durumu (`status`) gider: yeni
Auth hesabı yalnız kayıt `Active` ise etkinleştirir, başka durumda (ya da `status` yoksa) hesabın
durumuna dokunmaz ve adresi hiçbir zaman onaylamaz (onay yalnız davet kabulünde). Eski Auth
`status`'u tanımaz, eskisi gibi her senkronda etkinleştirir — eski davranış, kilitlenme yok.
Durum cevabında kaydın rolleri (`roles`) gider: yeni Auth her giriş, yenileme ve zorunlu parola
değişiminde Auth'taki dört platform rolünü kayda yakınsatır ve belirteci onlardan basar; `roles`
göndermeyen eski Platform'da yakınsatma yapılmaz (eski davranış). Sıra yine: önce Platform, sonra Auth.

**Yakınsatma bekliyor işareti (FIX4 madde 2).** Platform güncellemede önce Auth'u eşitler, sonra
kaydı yazar. Kayıt yazımı reddedilir, hata verir ya da sonucu bilinmez kalırsa kayıt **yeniden
okunur** ve Auth kaydın **şimdiki** haline (roller, durum) yakınsatılır; ön görüntü geri
yüklenmez. Bu yakınsatma da başarısız olursa kayıtta `AuthSyncPendingSinceUtc` dolar ve Error
günlüğü yazılır (yalnız yönetici kimliği). Bekleyenleri bulmak (salt okunur):
`db.platform_administrators.find({AuthSyncPendingSinceUtc: {$ne: null}}, {_id: 1, AuthSyncPendingSinceUtc: 1})`.
Kaydın bir sonraki başarılı güncellemesi işareti temizler; giriş ve yenileme rolleri zaten kayıttan alır.

**Son tur (FIX5) — işletim etkisi.**

- **Yönlendirme izlenmez (madde 1).** Auth'un Platform durum istemcisi 3xx cevabını **izlemez**
  (`AllowAutoRedirect = false`): iç anahtar özel bir başlıkta gider ve yönlendirmede başka hedefe
  taşınırdı. 3xx = **cevap yok** (503, oturum iptali yok). Zaman aşımı `PlatformService:TimeoutSeconds`
  (1–30 sn, varsayılan 5). Platform ← Auth yönündeki çağrının (provision / sync) yönlendirmesiz
  istemciye taşınması e-posta dalındadır; o dal birleşirken CT uzlaştırır, bu dalda o satıra dokunulmadı.
- **Cevap istenen adresin cevabı olmalı (madde 2).** Durum cevabındaki `email` istenenle (normalize)
  aynı değilse ya da yoksa Auth cevabı **cevap yok** sayar (503). Yeni Platform `email`'i zaten
  gönderir; karışık sürümde `email` göndermeyen eski bir Platform da 503 alır (kesinti, oturum silme değil).
- **Girişte boş rol listesi (madde 3).** Durum cevabında `roles` **boş ya da yalnız tanınmayan adlardan**
  oluşuyorsa Auth giriş / yenileme / zorunlu değişimde hiçbir platform rolü vermez (ve hesabın
  platform rollerini geri alır); giriş eskisi gibi 403 olur. Davet / senkron (provision / sync)
  yolunun "rol yoksa `ReadOnly`" kuralı değişmedi.
- **Provision gövdesinde `status` (madde 11).** Yeni Auth var olan hesabı yalnız `status = Active`
  ise etkinleştirir ve var olan hesabın e-postasını **onaylamaz**. `status` göndermeyen eski Platform
  ile yeni Auth: var olan hesap etkinleştirilmez (güvenli yön). Eski Auth `status`'u tanımaz ve
  eskisi gibi etkinleştirip onaylar. Sıra önemsiz; yine önce Platform, sonra Auth önerilir.
- **Auth cevap vermiyorsa ikinci çağrı yok (madde 10).** Güncellemede ilk senkron zaman aşımı /
  bağlantı hatası / 5xx ile düşerse Platform aynı Auth'a ikinci (yakınsatma) çağrısı yapmaz;
  kayıtta `AuthSyncPendingSinceUtc` doldurulur ve ilk hata yüzeye çıkar. **Açık:** Platform → Auth
  çağrısının kendi zaman aşımı bu dalda ayarlanmadı (istemci satırı e-posta dalında).
- **Günlük (madde 6).** Platform'un Auth çağrısı hatasında günlüğe yalnız yönetici kimliği, durum
  kodu ve neden kodu yazılır; Auth'un cevap gövdesi ve e-posta yazılmaz.
- **İşaret korunur (madde 8, 9).** `AuthSyncPendingSinceUtc`'yi yalnız güncelleme ve başarılı
  yakınsatma temizler; öteki kayıt yazımları saklı işareti korur. Auth'un giriş bildirimi
  (`accept-login`) artık yalnız son giriş zamanını ve davet durumunu yazar: sürüm, `UpdatedBy` ve işaret değişmez.

**Kiracı kapısı ve platform hesapları (FIX6 madde 13; FIX5'in "bütün kiracı" yasağının yerine).** Platform sistem
kiracısı (`00000000-0000-0000-0000-000000000001`) aynı zamanda varsayılan **iş kiracısıdır**: sıradan kullanıcıları orada
yaşar. Bu yüzden kural kiracıya değil **hesaba** bağlıdır: Auth kaydında `PlatformActorType` taşıyan hesap, "platform hesabı".
- **Kiracı belirteci basılan her yerde platform hesabı reddedilir** (kiracı girişi, MFA doğrulaması, kiracı yenilemesi, kiracı
  zorunlu parola değişimi): kapının kendi genel cevabıyla (yanlış parola / yanlış kod / geçersiz yenileme belirteci), parola
  bakılmadan, belirteç, yenileme satırı ve denetim satırı yazılmadan. Sıradan kullanıcılar eskisi gibi girer. Platform kapısı
  (`/api/platform-auth/login`, yenileme) etkilenmez. Kayıt (register) yalnız YARATTIĞI hesaba belirteç basar; platform
  hesabının adresi her zamanki "kullanımda" 409'u alır.
- **Yalnız platform kapısı aktörü (`actor_type` = `platform_admin` / `partner_admin`) platform hesabını görür; kimliği
  doğrulanmış her öteki istek (kiracı aktörü `tenant_user`, ama aynı zamanda actor_type'ı olmayan, boş ya da bilinmeyen bir
  belirteç) görmez ve dokunamaz (FIX8 madde 2; FIX6 yalnız `tenant_user`'ı gizliyordu, ötekiler her hesabı görüyordu):** liste /
  arama / arama kutusu / dışa aktarım ve üst sayaçlar onu içermez; kullanıcı yönetimi uçları (oku, doğrula, onay, etiket, tür,
  güncelle, sil, askıya al, etkinleştir, parola sıfırla, davet yeniden gönder, rol ata / kaldır) ona **404** verir, olmayan bir
  kimliğin cevabıyla aynı. Kural tek yerde (`HttpPlatformAccountVisibility` → `PlatformAccountScopedUserRepository` +
  `UserListReader`). Kimliği doğrulanmamış istek (anonim kapılar: giriş, MFA doğrulama, yenileme, platform kapısı; iç anahtarla
  gelen iç servis çağrıları) ve istek dışı çalışma (arka plan) gizlenmez. Auth yalnız `tenant_user` ve platform kapısı türlerinde
  belirteç basar; başka bir türde belirteç gelirse platform hesabı ona görünmez (kapalı yönde).
- **"Platform hesabı" her yerde aynı tanımdır (FIX8 madde 3).** `PlatformActorType` en az bir boşluk-dışı karakter taşıyorsa
  hesap platform hesabıdır (`User.IsPlatformAccount`); boş, boşluklu ya da eksik değer platform hesabı **değildir** ve kiracı
  aktörü onu sıradan kullanıcı gibi görür, rol izin kaldırmada oturumu sonlanır, kiracı kapısından girer. Mongo süzgeçleri bu
  kuralın tek paylaşılan tanımını kullanır (`PlatformAccountFilter`, boşluk kümesi `char.IsWhiteSpace`'ten türetilir; liste, arama,
  sayaç ve rol sahipleri aynı süzgeçtir) ve bir testle bellek kuralıyla gerçek belgeler üzerinde karşılaştırılır.
- **Dev etkisi:** `admin@diten.com` (tohum, `PlatformActorType=platform_admin`) artık kiracı kapısından (`/account/login`)
  giremez; aynı kiracının öteki kullanıcıları girer.
- **İşaret platform kapısında onarılır (FIX7 madde 1).** Hesabı "platform hesabı" yapan şey Auth'un kullanıcı üzerindeki
  kopyasıdır (`PlatformActorType`); platform kapısı ise yalnız Platform'un yönetici kaydına güvenir. Kopyası eksik ya da farklı
  bir hesap (eski bir sağlama, kaybolmuş bir eşitleme) platform kapısında yönetici, kiracı kapısında sıradan kullanıcı
  görünürdü. Artık **platform belirtecinin basıldığı her yerde** (platform girişi, platform yenilemesi, platform zorunlu parola
  değişimi) kayıttaki tür (`platform_admin` / `partner_admin`) hesabın üstüne, eksik ya da farklıysa, yazılır: yalnız o alanı
  değiştiren hedefli yazım, yalnız platform sistem kiracısındaki hesap için. Yazım başarısız olursa o basım da başarısız olur
  (500, yeniden dene) — kiracı kapısının kabul edeceği bir hesap bırakılmaz. Sonuç: bir hesap platform kapısından **bir kez**
  geçtikten sonra kiracı kapısı ve kiracı aktörleri için platform hesabıdır; hiç geçmemiş bir hesap kaydı olsa bile işaretsiz
  kalır.
- **Rol düzeyi (FIX7 madde 3).** Kiracı aktörünün rol işlemleri platform hesabına uzanmaz: bir rolden izin kaldırırken o rolü
  tutan platform hesaplarının oturumları sonlandırılmaz; rol listesindeki kişi sayısı platform hesaplarını saymaz. Platform
  kapısı aktörü ve iç çağrılar hepsini görür.
- **Günlük (FIX7 madde 5).** Kiracı kapısının bir platform hesabını reddi tek bir uyarı satırı bırakır:
  `AUTH_TENANT_DOOR_PLATFORM_ACCOUNT_REFUSED`, kapı (`sign-in` / `mfa-verify` / `refresh` / `forced-change`) ve kiracı kimliği;
  e-posta ve kullanıcı kimliği yazılmaz, cevap değişmez. (Kiracı zorunlu parola değişimi HTTP'de bir kiracı belirteciyle çağrılır:
  hesabı daha önce depo gizlediği için 401'i oradan alır ve bu satırı yazmaz; satır, hiçbir şeyin hesabı gizlemediği çağrıda
  yazılır.) Platform hesabının işareti bir basımda düzeltilirse ayrıca `AUTH_PLATFORM_ACCOUNT_DISCRIMINATOR_HEALED` bilgi satırı
  yazılır.
- **Bilinen sınırlar (açıkça).**
  (a) **Bir platform hesabının adresiyle kullanıcı oluşturmak yine 409 "kullanımda" verir.** Adres benzersiz olduğu sürece bu
  yoldan bir hesabın VAR OLDUĞU yoklanabilir; kaçınılmazdır, gizlenmedi.
  (b) **Bu kural gelmeden bir platform hesabına basılmış kiracı belirteci iptal edilmedi.** `SessionTimeoutMinutes` (kiracı
  ayarı, varsayılan 60 dk) dolana kadar o belirteç **içine gömülü izinleriyle** sıradan kullanıcılar ve roller üzerinde işlem
  yapabilir ve öteki servisler tarafından kabul edilir. Bu bir **penceredir**; "hiçbir şeyi yönetemez" değildir. Kapanması için
  belirtecin süresinin dolması ya da o hesabın oturumlarının elle iptali gerekir.
  (c) **Anonim `POST api/tenant-auth/register` (ve `api/auth/register`) PLATFORM kiracısında artık her adrese aynı 403'ü verir**
  (WP-USERS-ROLES-CLOSE-FIX1 FIX3 madde 5: `RegisterCommandHandler` platform kiracısında adrese bakmadan reddeder; kendi kendine
  platform hesabı açılmaz, yönetici adresi yoklanamaz). Kayıt ucu istemci başına / kiracı başına sayılır, gövde 8 KB. **Kalan:**
  başka bir kiracıda var olan bir adres hâlâ 409 "kullanımda" alır — o kiracının hesapları için belirteçsiz bir yoklama; tekleştirilmesi
  karar bekliyor (BL-621).
  (d) **İşaretsiz bir hesabın kiracı oturumu yenilemede reddedilmez.** Kiracı yenilemesinin platform hesabını reddetmesi (A')
  işarete bağlıdır; işareti eksik hesabın kiracı oturumu, hesap bir platform basımından geçip işaretlenene kadar yenilenmeye devam
  eder ve böylece `SessionTimeoutMinutes`'ı aşar (yenileme ömrü kadar). İşaretin yalnız platform basımında onarılması (FIX7) ile
  birlikte okunmalıdır: hiç platform basımından geçmemiş hesap işaretsiz kalır (tek seferlik doldurma yapılırsa kapanır).
  (e) **Kiracı zorunlu parola değişimi kapısının ret satırı üretim yolunda yazılmaz.** Bu kapı HTTP'de bir kiracı belirteciyle
  çağrılır ve depo hesabı önce gizlediği için "kullanıcı yok" 401'ini verir; satır yalnız hiçbir şeyin hesabı gizlemediği çağrıda
  (handler düzeyinde) yazılır.
  (f) **İşaret yazımı fail-closed'dır (FIX8 madde 1):** yazım hata verirse ya da hiçbir hesapla eşleşmezse (hesap okunduktan sonra
  silinmiş ya da taşınmış) platform girişi / yenilemesi / zorunlu değişimi belirteç basmaz (500); yeniden denenir.

**İç anahtar reddi (FIX3 madde 7).** Platform durum çağrısına 401 / 403 dönerse (iç anahtar iki
serviste aynı değil) Auth bunu **Error** düzeyinde günlükler (anahtar ya da değeri yazılmaz) ve
her platform girişine 503 verir. Düzeltme: Auth'taki `PlatformService:InternalApiKey` ile
Platform'daki `AuthService:InternalApiKey` aynı değeri taşımalı (değerler bu belgeye yazılmaz).

**Geçici hata ile kesin "hayır" ayrımı (FIX2 madde 3).** Dağıtım sırasında Platform'un
verdiği 5xx, ağ hatası, zaman aşımı ya da okunamayan cevap **"hayır" değildir**: Auth o isteği
503 ile reddeder ve **hiçbir oturumu iptal etmez**; kullanıcı yeniden dener. Oturum iptal
eden yalnız Platform'un kesin cevabıdır (kendi 404 zarfı, etkin olmayan kayıt, bilinmeyen
tür). Platform tarafında da yönetici kaydı okunamazsa istek 503 alır, güvenlik olayı
yazılmaz.

**Doğrulama (dağıtımdan sonra).**

1. Bir platform yöneticisiyle giriş → belirteçte `actor_type = platform_admin`.
2. Bir iş ortağı yöneticisiyle giriş → `actor_type = partner_admin`; aynı hesapla zorunlu
   parola değişimi → yine `partner_admin`.
3. Auth günlüğünde `Platform administrator status answered 404 without its envelope` ya da
   `status could not be read` satırları dağıtım penceresi dışında görünmemeli.
