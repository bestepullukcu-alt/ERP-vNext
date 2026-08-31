# DCP-004 — Provider Onboarding Note

**A module's work does not reach the Task Center by being finished. It reaches it by
being projected.**

This note exists because MOD-0023 was connected without doing that mapping first, and
the consequence is still on screen: approvals arrive, and the card meant to show what
is being approved has never rendered, because the provider emits no amount and no line
items. The plumbing worked on the first try; the projection was never written down.

Read this before wiring a new module — PV, Global Product Lifecycle, or anything else.

---

## 1. Where the seam is, and what it is not

```csharp
public interface IWorkItemProvider
{
    string ProviderCode { get; }
    string ProviderContractVersion { get; }
    IReadOnlyCollection<string> RequiredActionPermissions { get; }
    Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct);
}
```

The aggregator takes `IEnumerable<IWorkItemProvider>`, so **the Task Center itself does
not change when a module is added**. That is the whole design intent and it holds.

⚠ **For a module inside Platform it is an in-process DI seam.** Registration is
`services.AddScoped<IWorkItemProvider, XProvider>()`, so the provider class is compiled
into the Platform application and resolved from its container. The two providers that
predate this note (`workflow`, `tasks`) work that way and inject repositories.

**A module running as its own service cannot register into that container** — and it no
longer needs to. **UPDATED 2026-08-28 (WC-D1):** the general bridge exists. You do not
write a provider class in Platform, and you must not: you open ONE endpoint pair, and an
operator adds ONE configuration row. §7 is that contract, in full.

⚠ That adds a network hop to every Task Center load. The list page already fans out on
open; a provider that answers slowly slows the whole page, not just its own rows. What it
can no longer do is take the page down with it — WC-D3's per-provider budget and
`unavailableSources` were built for exactly this provider and are measured against it.

**A provider is READ-ONLY.** It must never write business state. The module keeps its
own write paths and its own screens; the provider only describes.

---

## 2. The mapping that must exist before any code

Every work item must arrive as `WorkItemProjectionDto`. These are its fields:

```
FixtureKind · Id · WorkIntent · AssignmentMode · OwnershipState · AdmissionState
NormalizedStatus · TaskLifecycle · ExecutionState · TimerState · SystemState
ActionDepth · Title · NativeStatus · Source · LifecycleOwner · WorkItemCapabilities
Actions · Concurrency · WaitingContext · Escalation · DueAt · PrimaryActionCode
OverflowActionCodes · Assignee · Requester · Checklist · Subtasks · ParentTaskItemId
Gates · Priority
```

**Write the mapping table first — one row per field — and answer three things for each:
what the module's value is, what it becomes here, and what happens when it is absent.**

The third column is the one that gets skipped and the one that hurts. A field left null
does not disappear from the screen; it renders as a confident zero, an empty card, or a
disabled action with no reason.

### Fields that are not free-form

| Field | Allowed values |
|---|---|
| `NormalizedStatus` | `Pending` · `InProgress` · `Waiting` · `Done` · `Cancelled` — closed set |
| `WorkIntent` | the intent vocabulary; `approval` and `task` exist today |
| `SystemState` | `fresh` when the module has nothing else to say |
| `ActionDepth` | `inline` when actions complete without leaving |
| Any lifecycle axis the module does not have | `notApplicable` — **not** null, and not an invented word |

`notApplicable` exists so a module can say "this axis does not apply to me" without
pretending to a state it has no concept of. Use it rather than picking the closest value.

### `LifecycleOwner` — the module keeps its own lifecycle

Set it to the module's own provider code and the Task Center will show the work while
the module remains the authority over its states. MOD-0024 does this; MOD-0023 points
elsewhere. Both are supported, and a module with its own lifecycle — a product
lifecycle, for instance — should own it rather than flattening into the task vocabulary.

### `WorkItemCapabilities` — declare only what has data behind it

The capability list gates which cards render. Twelve capabilities are declared in the
contract and five are emitted by nothing at all, so five cards can never draw.

**Declare a capability only when the data exists.** A capability with no data behind it
produces a card that renders empty for every item, which reads as "nobody has done this
work" rather than "this system does not track that".

---

## 3. The permission trap, written down because it has already happened

```csharp
IReadOnlyCollection<string> RequiredActionPermissions { get; }
```

The API layer evaluates **only the keys collected here** against the caller's claims and
hands the granted set to the provider. A key the provider checks but does not declare is
never evaluated, so `actor.Has(key)` silently returns false and the action is projected
as `PERMISSION_DENIED` **for a caller who genuinely holds the permission**.

This is not hypothetical. It is what happened when MOD-0024 was added and the key list
still lived hardcoded in the controller.

**Every key the provider consults must appear in this collection.** Compare the two
lists before wiring anything.

---

## 4. Actions — the part that decides whether the page is usable

An action the provider offers must be one the module's own API can actually perform.

Measured precedents worth not repeating:

- **A verb with no server behind it.** The Task Center offered "pause" for weeks; there
  is no pause transition and never was. It lived in the mock, so the showcase
  demonstrated something a real item could never do.
- **A row control whose only outcome is an error.** Ticking an unstarted subtask called
  `complete` directly and the server correctly refused. The message was good; the path
  offered was not.
- **An icon chosen at the call site.** Actions take their glyph from one map. A
  hand-picked glyph gives the same action two icons on two surfaces.

**For every action, state: the module endpoint it calls, the states it is offered in,
the permission it requires, and what the user is told when it is refused.**

---

## 5. What to hand over before the first line of provider code

1. The field mapping table from §2, all three columns
2. The capability list, each one with the data that backs it
3. The action list from §4, all four columns
4. The permission keys, cross-checked against `RequiredActionPermissions`
5. One real item, projected by hand, end to end

Item 5 is the cheapest way to find out that a field has no source. It takes an hour and
it is the step MOD-0023 skipped.

---

## 6. Verification, when it is wired

- Open the Task Center with the module's items present and count: what the provider
  returned, what the tab badge says, what the list shows.
- Open one item's detail and check every card the capabilities declared — a card that
  renders empty means a capability was declared without data.
- Press every action offered, including the ones expected to fail, and read what the
  user is told.
- Reload after each write and confirm the change survived. Five paths in this module
  have written only to the browser; each was found this way and not by testing.

⚠ **Measure the surface you are claiming.** A sort control was reported working on the
strength of an attribute that belongs to the table view while the list it was meant to
sort never reordered. Verify the thing the user sees, not the thing that is easy to read.

---

## 7. A module in its OWN SERVICE — the whole contract

**UPDATED 2026-08-28 (WC-D1). This section is the one PVG and Global SKU need.**

You do not write any C# in Platform. You open **two endpoints** in your own service and
hand an operator **one configuration row**. Platform reaches every remote module through
a single pair of classes (`HttpWorkItemProvider` / `HttpWorkItemActionDispatcher`), and a
guard test refuses a second implementation of either — so there is one timeout, one
fail-closed rule, one tenant propagation and one error dictionary for every module that
will ever connect this way.

⚠ **Do not write a bridge class for your module and do not ask for one.** N teams' bridge
classes mean N error-handling policies; the first slow module then slows the whole board
and nobody can say which one. That is the failure this design exists to prevent.

### 7.1 The configuration row (written by an OPERATOR, not by you)

```jsonc
// Diten.Platform.API/appsettings.*.json
"WorkAggregation": {
  "RemoteProviders": [
    {
      "ProviderCode":       "pvg",                    // your stable code; also source.providerCode
      "ContractVersion":    "1.0",
      "BaseUrl":            "http://localhost:50xx",  // scheme+host+port, no path
      "ProjectionPath":     "api/v1/work-items/projection",                 // default
      "ActionPathTemplate": "api/v1/work-items/{itemId}/actions/{actionCode}", // default
      "Actions": { "approve": "pvg.signals.approve", "reject": "pvg.signals.reject" }
    }
  ]
}
```

⚠ **The address comes from configuration and NOT from your manifest** (decision D1, owner,
2026-08-28). A manifest is client-supplied, so an address inside it would be the party
being called telling Platform where to send a caller's JWT. Your self-registration
manifest is unaffected and still does what it always did; it simply carries no host.

⚠ **`Actions` is the whole permission story.** The key you name per action is published as
your provider's `RequiredActionPermissions` *and* is the key the dispatcher checks — one
list, so the §3 trap cannot happen to you. **An action your projection publishes that is
absent from this map is STRIPPED before the reader sees it**, and a warning is logged. A
button that reaches nothing is the defect this capability removes; a missing button is at
least visibly missing.

⚠ A malformed row **stops Platform at startup**. That is deliberate: a typo that only
showed up as a permanently unavailable source on somebody's board would be reported as an
outage, months later, to the wrong person.

### 7.2 READ — `GET {BaseUrl}/{ProjectionPath}?scope=self|team`

Answer the shared `Response<T>` envelope. `data` is an OBJECT, never a bare array — the
version handshake needs somewhere to live:

```jsonc
{
  "data": {
    "contractVersion": "1.0",
    "items": [ /* WorkItemProjectionDto — every field in §2 */ ]
  },
  "statusCode": 200,
  "isSuccessful": true,
  "errors": [],
  "reason_code": null
}
```

- `scope=team` asks for the caller's subordinates' work. A module with no team concept
  ignores it, exactly as the in-process providers do.
- **`source.providerCode` on every item MUST equal your row's `ProviderCode`.** Items
  claiming another code are dropped and logged: that field is the address the browser
  posts the next click to, so accepting a foreign one would let you route a write at a
  module the operator never configured.
- **`contractVersion` must equal the row's.** A disagreement is reported as an unavailable
  source, never guessed at — a mis-projected item is worse than a missing one.
- Everything in §2 applies unchanged, and the two rules that bite hardest are: **omit an
  optional field rather than sending `null`** (the client checks for `undefined`, and an
  item that fails validation is DROPPED WHOLE, taking its title and buttons with it), and
  **declare a capability only when data backs it**.

### 7.3 WRITE — `POST {BaseUrl}/{ActionPathTemplate}`

The body is exactly what the browser posts to Platform — one wire shape for the whole
chain:

```jsonc
{ "providerCode": "pvg", "payload": { "expectedVersion": 2, "reason": "…", /* … */ } }
```

Answer the same envelope. **Refusals must carry a stable `reason_code`, never a sentence
the reader is expected to understand** — Platform hands the code through unchanged and the
Task Center resolves it in seven languages. A module that answers prose gets that prose
shown to somebody who may not read the language it is in.

### 7.4 Who decides what

| Decision | Who | Note |
|---|---|---|
| Is this caller signed in, and who are they | **your service**, from the bearer token | Platform forwards the CALLER's own JWT, never a service key |
| Which tenant | **`X-Tenant-Id`**, written by Platform from the request scope | your own tenant middleware reads it as usual — and **refuses a contradiction 400**, see below |
| May this caller press this button | **Platform**, from CLAIMS + the row's `Actions` map | an action the caller lacks is disabled with `PERMISSION_DENIED` whatever your `enabled` said |
| May this caller do this to THIS record | **your service**, underneath | being permitted to press `approve` is not being the assigned approver |
| **Another tenant's record was asked for** | **your service** — **404**, never 403 | see below; the bridge forwards your status unchanged, so this answer is yours to give |
| How long is too long | **Platform**, one budget, `WorkAggregation:Resilience:ProviderTimeout` | applied per provider on read and per action on write |

#### Another tenant's work — the two answers (owner decision, 2026-08-29, BL-323)

Until this was written the row above said only *"your own tenant middleware reads it as
usual"*. It said how to READ the tenant and never what to ANSWER for a foreign one, and
the repo already held two different answers to two different questions. Both are now rules:

| What happened | Answer | Why |
|---|---|---|
| **1. `X-Tenant-Id` and the JWT name DIFFERENT tenants** | **400** | A malformed request, not an access decision. The caller wrote both values, so there is nothing to conceal — and refusing here is what makes it safe for your handlers to read either one. |
| **2. They agree, but the RECORD belongs to another tenant** | **404** | It does not exist for you. |

⚠ **NEVER 403 for case 2, and this is the line that gets "simplified" away, so here is its
reason: a 403 confirms the record exists, which is exactly the leak the 404 is chosen to
prevent.** In a multi-tenant system a record you may not see does not exist for you — the
same shape SAP and Oracle worklists use. 403 stays what it already is: the verdict on an
ACTION the caller may not perform on a record that is theirs.

⚠ **The 404 carries your ordinary absent-record `reason_code`** (the reference consumer
sends `REFERENCE_ITEM_NOT_FOUND`). **Do not mint a cross-tenant code**, and no new
user-facing string is added for this anywhere — that is part of the decision, not an
oversight. A distinct code needs a distinct sentence on screen, and that sentence would
announce the record's existence to the one reader who must not learn it. A foreign record
must read exactly like any absent one.

⚠ **The shape that produces this is a tenant-SCOPED lookup, not a cross-tenant branch.**
Scope the query by the caller's tenant and the foreign record is simply absent, so your
ordinary not-found path answers it. If you find yourself writing
`if (record.TenantId != mine) return Forbid()`, you have written case 2 as a permission
check and will leak through it.

**What is guarded and what is not — stated plainly rather than implied.**

- **Guarded, by real assertions over real code:** this service's tenant middleware (case 1)
  and the reference consumer in §7.6 (case 2), in
  `Diten.DevEnablementService.Api.Tests/WorkItemBridge/CrossTenantContractGuardTests.cs`.
  Every case-2 assertion there is differential — the same item id and the same action code
  must answer 200 to its owner and 404 to a stranger — so the guard cannot pass by refusing
  everything.
- **NOT guarded, and cannot be:** what YOUR module answers. Your code is not in this repo,
  and Platform will not rewrite your status into a 404 — `RemoteWorkItemGateway` forwards a
  module's own verdict and status deliberately, because a bridge that rewrote 403 into 404
  would also erase the legitimate 403 in the row above. **For your module this section is
  documentation, and the only thing standing between it and a third invented answer is you
  reading it.** Assert both cases in your own test suite.

### 7.5 What happens when you are down — read this before you ship

- **Read:** your rows are absent, the rest of the board still draws, and your code appears
  in `unavailableSources` with `ERROR` or `TIMEOUT`. The screen shows a warning strip
  naming you.
- **Write:** the action is **REFUSED** — HTTP 504, `WORK_ITEM_REMOTE_UNAVAILABLE` — and
  never reported as success. ⚠ **It may in fact have landed on your side.** That is
  precisely why the caller is told the outcome is unknown rather than shown a green toast,
  and it is why `expectedVersion` matters: a retried click must not become a second write.
  Support an idempotent or version-checked write, or accept that a retry after a timeout
  can double.

### 7.6 The working example you can read

`Diten.DevEnablementService/…/Controllers/ReferenceWorkItemProviderController.cs` is a
complete, running implementation of both endpoints — the exact envelope, the exact field
names, a real state transition and a real refusal code.

⚠ It is **TEMPORARY** (BL-310) and exists only because no real module had opened an
endpoint on the day the bridge was written. Copy its SHAPE; do not copy its in-memory
store, and expect it to be deleted.

⚠ **It reads the RAW `X-Tenant-Id` header rather than the resolved tenant context, and that
is only safe because this service refuses a contradiction first (case 1).** The raw read is
kept deliberately — the `(no tenant header)` echo in the item title is what caught the §7.7
propagation defect — but until 2026-08-29 the middleware here let the JWT win with a
warning, and the two together let a caller holding tenant A's token read and MUTATE tenant
B's item by sending B's header. Measured, not guessed, and now closed at the middleware
(BL-323). If you copy this file, copy the refusal with it.

### 7.7 Verified live, 2026-08-28

Not inferred from green tests. Platform (`:5057`) reaching DevEnablement (`:5058`):

- the remote item appeared on `/api/v1/work-items/mine` beside 84 in-process items, and
  its title showed the tenant id the far service actually received — the tenant header
  travelling, read off the screen;
- `accept` posted to Platform's one write address moved the item `Pending → InProgress`
  and `version 1 → 2` on the far side, and the next read showed the new state;
- a stale `expectedVersion` came back `409 REFERENCE_CONCURRENCY_CONFLICT` — the module's
  own code, intact;
- an action the row does not configure came back `400 WORK_ITEM_ACTION_UNKNOWN` without
  leaving Platform;
- with the far service STOPPED: the board still drew all 84 rows and named
  `{providerCode: "dev-reference", reasonCode: "ERROR"}`, and the write was refused
  `504 WORK_ITEM_REMOTE_UNAVAILABLE`.

⚠ **One defect was found this way and only this way.** The first implementation reused the
shared `TenantPropagationHandler` and sent **no tenant header at all**, while its unit test
passed: `IHttpClientFactory` caches its handler chain in its own scope, so a
`DelegatingHandler` resolving a request-scoped `ITenantContext` never sees a resolved one.
It was visible only because the far service echoed back "(no tenant header)". The header is
now written by the request-scoped gateway. **Closed out 2026-08-29:** the two other clients
were moved off the handler the same way (**BL-311**), and the handler class itself — dead on
all three services, attached only to a named client nobody created — was deleted (**BL-316**).
The surviving rule is `TenantOnTheWire`: the calling class writes `X-Tenant-Id` from its own
request scope, never a `DelegatingHandler`.

---

*Written 2026-08-24 from the two providers that exist (`tasks`, `workflow`), the
canonical DTO, and the defects found while walking a real task end to end. Companion to
`DCP-004` (Work Aggregation / Task Center).*

*§7 added 2026-08-28 (WC-D1) from the general HTTP bridge, verified end to end against a
reference consumer running in a separate service. Companion to `DCP-004-provider-action-dispatch.md`.*
