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

⚠ **It is an in-process DI seam, not a network seam.** Registration is
`services.AddScoped<IWorkItemProvider, XProvider>()`, so the provider class is compiled
into the Platform application and resolved from its container. Both providers that
exist today live inside Platform and inject repositories.

**A module running as its own service cannot register into that container.** It needs a
provider *in Platform* that calls it — a thin client plus a projection class. There is
no precedent for this yet; the first module to need it is writing the pattern.

⚠ That adds a network hop to every Task Center load. The list page already fans out on
open; a provider that answers slowly slows the whole page, not just its own rows.

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

*Written 2026-08-24 from the two providers that exist (`tasks`, `workflow`), the
canonical DTO, and the defects found while walking a real task end to end. Companion to
`DCP-004` (Work Aggregation / Task Center).*
