# DCP-004 — Provider Action Dispatch

**A module can show its work in the Task Center today. It cannot be acted on there,
and the reason is not that somebody forgot to wire a button.**

Two developers arrived at the same wall within a week of each other. Both wrote a
provider, both saw their items appear, both pressed a button and watched nothing
happen. The browser routes a real call for exactly one provider code and simulates
the rest, saying so in the console.

This note is what measurement found underneath that, and what has to be decided
before any dispatch is designed. Companion to
`DCP-004-provider-onboarding-note.md`, which covers the read side.

---

## 1. Why the hardcoded provider code was unavoidable

```javascript
// WorkCenterNext/app.js:6328
const isRealTaskItem = (item) =>
    item && item.provenance !== 'fixture' && item.source?.providerCode === 'tasks';
```

It reads as laziness. It is not. **The wire carries no address.**

`WorkItemActionDto` — the whole of what reaches the browser about an action:

```
Code · Label · SemanticType · Enabled · Source · DisabledReasonCode
DisabledReason · RequiresConfirmation · RequiresReason · RequiresEvidence
SupportsBulk · RiskLevel
```

No endpoint. No HTTP method. No permission key. The browser is told an action
exists and whether it is enabled — never where to send it. With nothing to
dispatch on, a hardcoded branch is the only thing that can work.

⚠ `WorkItemSourceDto.DeepLink` is not that address. It is a navigation link and
is null in the MOD-0023 phase.

---

## 2. Four decisions, and none of them is "write the dispatcher"

### D1 — Where does a module's address come from?

**Measured: nowhere.**

- `ModuleManifestDocument` carries no address field. `Service` is a name
  (`"DITENMDMSERVICE"`), not a host.
- `InterfaceRegistry` — the obvious candidate — carries `RoutePath` and no host,
  scheme or port. Its endpoint snapshot has no URL at all.
- The seven inter-service addresses that exist live in `appsettings`, written by
  hand, one key per pair.

⚠ **And the direction is backwards.** During registration the module knows
Platform's address and calls it; Platform never learns the module's. When the
push finishes, Platform holds no way back.

⚠ A manifest is client-supplied. Taking an address from it means a module can
tell Platform "call me here" — a redirect written by the party being called.
There is no precedent for that in this repo, and adding one is a security
decision, not a plumbing one.

### D2 — How is an action described on the wire?

Three descriptions of one action exist today and none of them travels:

| Where | What it holds |
|---|---|
| `TaskWorkItemProvider.BuildActions` | code + label key + `actor.Has(permission)` |
| `RequiredActionPermissions` | the permission list, kept in sync **by hand** |
| `TaskTransitionRoutes:28` | a literal regex of accepted codes |

The provider file says what that costs: *"when `inquire` was added to the provider
and to Platform but not to the proxy's route constraint, the button rendered, the
user pressed it, and the proxy answered 404."*

⚠ And the manifest's action vocabulary is a **different vocabulary**:
`CREATE · UPDATE · ASSIGN · CLAIM · COMPLETE · CANCEL · DELETE · BULK_DELETE`
against the projection's `claim · accept · start · plan · inquire · submitReview
· return · reassign · complete · cancel · release`. No mapping exists between
them, and the Task Center's own manifest declares `Actions: []`.

### D3 — How is aggregation protected? — **CLOSED 2026-08-28**

**Was:**

```csharp
// GetMyWorkItemsHandler.cs:45
foreach (var provider in _providers)
{
    var items = await provider.GetWorkItemsAsync(actor, ct);   // inside the loop
    aggregated.AddRange(items);
}
```

**Sequential. No `try`. No timeout. No partial result.** One provider throwing
propagated out of the handler, so the reader got an error page instead of the rows
the other provider already had in hand; one provider hanging hung the request,
because nothing on the path imposes a deadline (Platform API: no request timeout;
gateway: no `QoSOptions` on any of its 110 routes; the web proxy uses the unnamed
default client). Both providers are in-process Mongo reads, so none of it showed.

**How it was closed — three pieces, all measurable:**

1. **Per-provider isolation.** Each call now runs inside its own `try` and its own
   `CancellationTokenSource.CreateLinkedTokenSource(ct)` with a budget from
   `WorkAggregation:Resilience:ProviderTimeout` (`WorkAggregationResilienceOptions`,
   default 10 s, bound in `Diten.Platform.Infrastructure/DependencyInjection.cs`).
   A failure or timeout in one provider cannot reach another. The caller's OWN
   cancellation still propagates — a reader who navigated away must not produce a
   "the tasks source failed" report about a request nobody is waiting for.

   ⚠ **Still sequential, deliberately.** Providers are registered `Scoped`;
   calling them concurrently would share one DI scope and its Mongo session across
   threads — a separate decision with a separate hazard. The cost is that the worst
   case is N × the budget, recorded as **BL-303** and to be revisited when the
   provider count grows.

2. **The result is honest.** The handler returns
   `Response<WorkItemBoardDto>` where `WorkItemBoardDto = { Items,
   UnavailableSources[] }`, and each entry is `{ providerCode, reasonCode }` with
   `reasonCode ∈ TIMEOUT | ERROR | UNSUPPORTED_VERSION`. Codes only — the sentence
   comes from the 7-language resx on the frontend (the error-code bridge rule).
   `Response<T>` itself was NOT widened: it is shared by every feature in the
   service, and the completeness of one read is a property of that read.

   ⚠ **The version skip is no longer silent.** The bare `continue` on an
   unsupported `ProviderContractVersion` was the small version of this same defect —
   a source leaving the board while the board looked whole. It is still not
   projected (a mis-projected item is worse than a missing one), but it is now
   reported as `UNSUPPORTED_VERSION`.

3. **The screen says it.** `WorkCenterNext/app.js` draws a warning strip above the
   board when `unavailableSources` is non-empty, naming each source and its reason,
   and the tab count badges carry a `+` and are drawn EVEN AT ZERO while a source is
   missing — a count over a partial board is a floor, not a total, and a confident
   zero has been misread on this surface before.

**Guards (tests, not sentences)** — `GetMyWorkItemsHandlerTests`:
one provider throws → the other's items still return, source listed as `ERROR` ·
one provider times out → same, listed as `TIMEOUT` (proved with an already-spent
budget and a token-respecting provider, so the test costs no wall-clock time) ·
unsupported version → listed as `UNSUPPORTED_VERSION`, never silent · both
providers healthy → `UnavailableSources` empty and no strip drawn. Plus the
board-envelope guards in `workcenter-next-work-items-api.test.js`.

⚠ What D3 does NOT close: there is still no timeout at the API, gateway or web-proxy
layer. The budget enforced here is the provider call's, inside the handler.

### D4 — Does the gateway allow writes?

```
/api/v1/work-items/{everything} → ["GET", "OPTIONS"]
```

A command endpoint added to Platform would answer 404 at the gateway. And
`WorkItemsController` has two GETs and says why in its own header: *"No command
endpoint lives here — approve/reject/delegate stay on MOD-0023's existing
endpoints."*

⚠ That comment is a design position, not an oversight. Whoever changes it is
reversing a decision, and should say so.

---

## 3. What tenancy does and does not give you

The catalogue is split, and the split does not mean what its types suggest:

| Entity | Base | TenantId |
|---|---|---|
| `ModuleCatalogItem` | `GlobalEntity` | none — the base says `// No TenantId here` |
| `ModulePageDescriptor` | `TenantScopedEntity` | required |
| `ModulePageActionDescriptor` | `TenantScopedEntity` | required |

But every registration writes the tenant-scoped rows under `Guid.Empty`
(`InternalModuleRegistrationController:84`), because `/api/internal` bypasses
tenant resolution. Tenant-scoped by type, global in practice.

⚠ **Dispatch cannot be derived per tenant from today's catalogue.** Provider
registration is static DI with no tenant condition, every provider is called for
every tenant, and entitlement is not consulted on this path at all — isolation
comes from each provider's own tenant-scoped repository, one level down.

---

## 4. The order these have to happen in

D3 is not last. It is the one that fails silently, on someone else's screen,
months after the change that caused it.

```
1. D3  protect aggregation      — DONE (2026-08-28)
2. D1  address                  — a security decision, not plumbing
3. D2  action on the wire       — endpoint + method + permission
4. D4  gateway                  — reverses a written position; say so
```

⚠ Doing 2 and 3 without 1 buys a working button and an unexplainable page.

---

## 5. What a module can do while this is open

Nothing here blocks the read side. A module can write its provider, project its
items, and see them in the list today. What it cannot do is act on them there —
so its own screens remain the place where work is done, and the Task Center
remains where work is found.

⚠ Tell that to the module's author explicitly. Both developers who hit this wall
had already written the provider before discovering the buttons were inert.

---

*Written 2026-08-27 from measurement of the aggregation handler, the gateway
route table, the projection DTOs, the catalogue entities and the browser
dispatcher. Every "no" above was checked rather than assumed; where something
could not be measured it is not claimed.*
