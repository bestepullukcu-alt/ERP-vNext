# Owner decision text — Supply Chain module self-registration (NOT APPROVED)

**Status: DRAFT DECISION TEXT — NOT APPROVED.** This text grants nothing until the owner states it, or a named alternative, in their own words. An agent quoting it is not approval.

## Choices (recommended marked)

| ID | Question | Options |
|---|---|---|
| D1 | Where does the shared foundation belong? | **A (recommended):** a follow-up section in DCP-009 + a "Self-registration" section in each module pack (0183–0187) · B: foundation inside the first shipping module's pack |
| D2 | How does SupplyChain authenticate to Platform registration? | **A (recommended now):** the existing `X-Internal-Api-Key` path (no Platform change; same as DevEnablement and the Carrier candidate) · B: a per-service credential like MDM (Platform change by the Platform owner) |
| D3 | Identity strings | **A (recommended):** Domain `SupplyChainExecution`, Service `DitenSupplyChainService`, ModuleCodes `shipment-tracking-pod`, `carrier-management`, `routing-load-planning`, `reverse-logistics`, `claims-management` · B: other names given by the owner |
| D4 | When does a provider ship? | **A (recommended):** with that module's UI in the integrated target, together with its nav keys; never ahead of its UI · B: all at once when the foundation lands |
| D5 | Returns target permission keys are not constants | **A (recommended):** the completeness test reflects `ReturnPermissions.ForTarget` · B: add six `public const` fields to `ReturnPermissions.cs` (a Returns-owned backend change) |

## Decision text to record

> I approve the Supply Chain module self-registration **design** in `docs/roadmap/plans/mvp6-self-registration-prep-01/` for pack and DCP preparation only, with these choices: **D1 [A|B]**, **D2 [A|B]**, **D3 [A|B]**, **D4 [A|B]**, **D5 [A|B]**.
>
> - The shared foundation (manifest interface, registration hosted service, options, project reference, `Program.cs` registration lines, `PlatformRegistration` settings, hosted-service tests) is one capability-level item. Each module ships its own manifest provider and tests mirroring its real frontend routes and buttons, with the permission keys that already exist; no permission key or ID is created.
> - The manifests cover MOD-0183 Shipment, MOD-0184 Carrier, MOD-0185 Loads (only after its UI is approved and built), MOD-0186 Returns and MOD-0187 Claims as listed in MANIFESTS.md. S&OP (MOD-0190) and Capacity (MOD-0192) are excluded until they have a UI scope.
> - The eleven tenant navigation keys in NAV-L10N-KEYS.tsv are required in all seven languages and ship with the module they belong to.
> - Completeness tests in both directions, uniqueness, scope and a reconcile-state test (TEST-PLAN.md) are required before any module counts as closed.
> - The recorded gaps stand as gaps: the single-key action model cannot express the `.transition` and `supplychain.shipments.read` conjunctions; the Shipment and Carrier UIs and the Returns/Claims permission classes are not yet in the common checkout; the Carrier UI source is not archived in the repository.
>
> This decision authorizes preparing the DCP-009 follow-up section and the five pack sections as patches for my later sign-off. It does **not** authorize code, `Program.cs`, `.csproj`, appsettings, `SharedResource` or Platform changes, which remain with the single CT-appointed integration owner after an integrated target (Q14/Q15) exists; nor a new permission key or ID; nor gateway changes; nor commit, push or stash.

## Effort reference

D2=A total 38/70/127 person-hours (EFFORT.md); D2=B adds 4/8/14; D5=B adds 1/2/4.
