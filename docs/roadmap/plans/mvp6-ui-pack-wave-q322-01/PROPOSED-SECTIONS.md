# Q322 UI pack wave — proposed §9 and §11 for MOD-0184, MOD-0185, MOD-0186, MOD-0187

**Lane:** Q322 · **Date:** 2026-10-13 · **State:** PROPOSAL, needs owner approval per pack
**Template:** MOD-0183 §9 and §11 as reconciled by Q320 (ACCEPTED 2026-10-03, E2)
**Measurements:** `MEASUREMENTS.tsv` beside this file. Every count here was measured; none is carried over
from another pack and none is inferred from a screenshot.

## Why this lane is open now

Q322's recorded state is **"HELD (starts when Q320 reports)"**. Q320 reported: it is ACCEPTED, E2, and its
reconciled §9/§11 are in `MOD-0183-shipment-tracking-pod.md:257-350`. So Q322's hold has lapsed and its own
row is stale. The owner's 2026-10-03 decision was to sequence rather than parallelise, *"so the four
revisions come out in one shape instead of four"* — which is why all four are proposed together here.

**Nothing in this lane edits a pack.** Module packs are authority documents (AGENTS.md 1) and each of the four
demands a *separately approved* revision in its own §11. This file is the text for that approval, in the
precedent form of `docs/roadmap/plans/mvp6-pack-alignment-01/*/proposed-pack.patch`.

## Upgraded 2026-10-13: the denied-access bound is now observed, not cited

The four §9 sections below each assert that the page renders the shared `_AccessDenied` partial inside the
shell without redirect. When they were written that was a source citation (`Index.cshtml:13`, `:14`, `:16`,
`:16`). It has since been **measured live** against the running stack, with a user holding none of the 25
`supplychain.*` permission keys: all four routes are served, each answers with its own module-specific
message inside the full tenant shell, and the URL does not change — `LIVE-DENIED-ACCESS.tsv` beside this
file. That is the same standard Q320 set for MOD-0183, which cited Q264's live run rather than the view
source.

## What the measurement settled before any text was written

**1. All four are Slim, and that is the measured answer, not a preference.** AGENTS.md:185-186 binds
`8 ve altı` → `GoldenReferenceSlim` (create/edit offcanvas inside Index) and `8'den fazla` →
`GoldenReferenceCompact` (separate `Create`/`Edit`/`Details`/`_Form`). Counted by the MOD-0183 method — every
`input`, `select` and `textarea` in the create/edit view, the repeatable row template counted once per field:

| module | create/edit fields | rule | current shape | verdict |
|---|---:|---|---|---|
| MOD-0184 Carriers | **5** | 8 ve altı | offcanvas on Index | Slim, compliant |
| MOD-0185 Loads | **8** | 8 ve altı | offcanvas on Index | Slim, compliant — **exactly on the boundary** |
| MOD-0186 Returns | **6** | 8 ve altı | offcanvas on Index | Slim, compliant |
| MOD-0187 Claims | **7** | 8 ve altı | offcanvas on Index | Slim, compliant |

MOD-0185 deserves the flag: at exactly 8 it is Slim, but **one more field makes it Compact** and forces a
structural rewrite into four files. That is a constraint on future Loads work, and §11 states it so nobody
adds a ninth field without knowing the cost.

**2. The tree has the UI; three of the four §22 sections still say it does not.** Measured:

| pack | §22 says | the tree says |
|---|---|---|
| MOD-0185 §22 note 1 | *"Loads UI scope is HELD and unbuilt; the RoutePath and the action table are proposals"* | `SupplyChainLoadsController.cs:20` `[Route("SupplyChain/Loads")]`, `:43` `[HttpGet("")]`, `Views/SupplyChain/Loads/` with six files, 62 l10n keys × 7 |
| MOD-0186 §22 page table | RoutePath `/SupplyChain/Returns` **"(approved; not a route yet)"** | `SupplyChainReturnsController.cs:35` and `:75` declare exactly that route |
| MOD-0186/0187 M-04 | *"the frontend view-route set of the … controller (to be built)"* | both controllers exist and were driven live in four recorded lanes (r4a, r4b, r4c, r2) |

This is the Q320 situation repeating in three more packs: §22 was written while the UI was a plan and was
never re-read when the UI landed. The proposed §9/§11 below describe the tree; **the stale qualifiers in §22
are listed in `OPEN-FOR-OWNER.md` rather than edited here**, because §22 carries its own owner authority.

**3. MOD-0185 has no view model at all.** Measured: nothing under `frontend/Diten.Web/Models/` matches
`*Load*`, and `SupplyChainLoadsController.cs` references no view model type. Its offcanvas is driven entirely
from JavaScript against the API, with fields identified by `id`. The other three have one
(`CarrierViewModels.cs`, `ReturnViewModels.cs`, `ClaimViewModels.cs`). MOD-0183's §11 has a "View model"
column; for MOD-0185 the honest value is "none", and that is what it says.

---

# MOD-0184 Carrier Management

## §9 Layout & Shell Contract (proposed)

**Shell: tenant.** Reconciled with §22 on 2026-10-13, in the shape Q320 set for MOD-0183. §22 declares one
tenant page and the tree holds it. Each bound, in this form:

- **Shell.** The page renders inside the tenant shell: `Layout = "_LayoutTenantShell"` at
  `Views/SupplyChain/Carriers/Index.cshtml:8`. Shared shell files stay protected (§6); this module does not
  edit them.
- **Routes.** Exactly the one §22 RoutePath, `/SupplyChain/Carriers`, served by
  `Controllers/SupplyChainCarriersController.cs:15` with the view action at `:43`. The controller's other
  entries are `api` endpoints (`:46`, `:60`, `:78`), not frontend routes. No other frontend route is added
  without a revision of this pack; §22 test M-04 checks the set.
- **Menu.** No hand-written menu or sidebar entry. Navigation comes only from the §22 manifest page
  `CARRIERS` (Nav **true**) and its navigation keys, which ship with the UI and never ahead of it.
- **DataTable.** The list uses the shared v2 contract: `data-dt-standard="v2"` at
  `_DataTable.cshtml:15` (AGENTS.md DataTable rule).
- **Localization surface.** Tenant module, seven languages: `CarriersIndex.{en,tr,fr,es,zh,ar,ru}.resx` holds
  **45 keys in each of the seven** — measured by occurrence, 45 unique, no duplicates. Hard-coded user text is
  not added.
- **Denied access.** The page renders the shared `_AccessDenied` partial when the key it needs is missing
  (`Index.cshtml:13`), inside the shell and without redirect (UAS-001).

The UI is **not** complete. Reconciling the contract makes its open defects closable; it does not close them
(§11, open items).

## §11 Frontend File Contract (proposed)

All paths under `frontend/Diten.Web/`.

**Screens.** One page, from §22's page table, matched to its view under `Views/SupplyChain/Carriers/`:

| §22 PageCode | PageType | View | Partials it composes |
|---|---|---|---|
| `CARRIERS` | List | `Index.cshtml` | `_Filter`, `_DataTable`, `_CreateEditOffcanvas`, `_ChangeStatusOffcanvas`, `_IndexL10n` |

`CarriersIndex.cs` is not a page; it is the localizer's marker type.

**Exact form fields.** Method as MOD-0183: every `input`, `select` and `textarea` in the view, counted once.

| Form | View | Fields | Count | View model |
|---|---|---|---|---|
| Create / Edit | `_CreateEditOffcanvas.cshtml` | `formLegalEntity` (:26), `CarrierCode` (:33), `DisplayName` (:41), `SupportedModes` (:49), `ExternalReference` (:62) | **5** | `Models/SupplyChain/Carriers/CarrierViewModels.cs` |
| Change Status | `_ChangeStatusOffcanvas.cshtml` | `TargetStatus`, `ReasonCode`, and the confirmation control | **3** | same file |
| List filter | `_Filter.cshtml` | two query controls | **2** | none — query parameters |

**Golden reference: Slim.** Create/Edit is 5 fields, which is `8 ve altı`, and AGENTS.md:185 binds that to
`GoldenReferenceSlim`: create/edit offcanvas inside Index. The tree matches. There is no `Create.cshtml`,
`Edit.cshtml` or `Details.cshtml`, and under Slim there should not be. Quick View is a row action on the list,
not a page.

**Open items** — none is closed by this reconciliation:

1. **Row 2 (No-shell) is unmeasured.** Create, status change, list and filter were measured live in R-4a;
   Save View, column visibility and personalization were never exercised in any lane
   (`docs/records/audits/2026-10/mvp6-q446-gate-18-0-four-modules-01/GATE-18-0.tsv`).
2. **Row 7 (Validation): no client-side case.** Server validation runs in the suite
   (`ValidationStagesOneThroughFiveDoNotCallRepository`, `HeaderBoundaries…`); no client-side rejection was
   produced live in R-4a.
3. **Row 12 (UX states): two states missing.** Loading, denied, save-failed and conflict were measured; an
   empty list and a validation-error state were never produced live.
4. **Row 13 is MET as of 2026-10-12** on re-measurement, not on new code:
   `CarriersController.cs:27` logs operation, tenant, legal entity, correlation, status, error code,
   `IdempotentReplay`, `ListSize` and `ElapsedMs`
   (`docs/records/audits/2026-10/mvp6-stage-r-row13-01/`). `:376` withholds runtime **write**, not
   observation.

---

# MOD-0185 Routing & Load Planning

## §9 Layout & Shell Contract (proposed)

**Shell: tenant.** §22's note 1 still reads *"Loads UI scope is HELD and unbuilt"*; the tree contradicts it,
and that contradiction is listed for the owner rather than resolved here.

- **Shell.** `Layout = "_LayoutTenantShell"` at `Views/SupplyChain/Loads/Index.cshtml:9`.
- **Routes.** One frontend route, `/SupplyChain/Loads`, at
  `Controllers/SupplyChainLoadsController.cs:20` with the view action at `:43`; `api` entries at `:47`, `:61`.
- **Menu.** Only the §22 manifest page `LOADS` and its navigation keys.
- **DataTable.** `data-dt-standard="v2"` at `_DataTable.cshtml:16`.
- **Localization surface.** Seven languages, **62 keys in each of the seven**, measured.
- **Denied access.** `_AccessDenied` at `Index.cshtml:14`, inside the shell, no redirect.

The UI is **not** complete.

## §11 Frontend File Contract (proposed)

**Screens.**

| §22 PageCode | PageType | View | Partials it composes |
|---|---|---|---|
| `LOADS` | List | `Index.cshtml` | `_Filter`, `_DataTable`, `_CreateEditOffcanvas`, `_IndexL10n` |

**Exact form fields.**

| Form | View | Fields | Count | View model |
|---|---|---|---|---|
| Create / Edit | `_CreateEditOffcanvas.cshtml` | `formLegalEntity` (:24), `carrierId` (:28), `mode` (:33), `plannedDepartAt` (:43); plus the repeatable stop template (:67, :73, :74, :76), counted once per field | **8** | **none** — no file under `Models/` matches `*Load*` and the controller references no view model type; the form is driven from JavaScript against the API, fields identified by `id` |
| List filter | `_Filter.cshtml` | three query controls | **3** | none — query parameters |

**Golden reference: Slim, exactly on the boundary.** 8 fields is `8 ve altı`, so AGENTS.md:185 binds Slim and
the tree matches. **One further field makes it 9, which is `8'den fazla`, and AGENTS.md:186 then binds
Compact** — separate `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` and `_Form.cshtml`, with no create/edit
offcanvas on the list. No Loads change may add a form field without taking that restructure, or without a
pack revision that records the decision.

**Open items:**

1. **§22 says this UI is unbuilt.** Note 1 and M-04 describe a controller "to be built". It is built. Listed
   for the owner.
2. **No view model.** The other three modules in this wave have one. Whether Loads should is a design
   decision this reconciliation records rather than makes.
3. **Row 2 (No-shell) is unmeasured.** Create, list and filters were measured live in R-4b; Save View and
   column visibility are built but were never exercised live.
4. **Row 1 (Golden flow) closed 2026-10-06** by Q457's owner-applied fix, and the tracked configuration that
   fix depends on is now guarded (`docs/records/audits/2026-10/mvp6-stage-r-row1-loads-config-01/`).
5. **Row 13 MET 2026-10-12** on re-measurement: `LoadsController.cs:21` logs operation, status, error code and
   `IdempotentReplay`, with correlation on 43 of 43 lines.

---

# MOD-0186 Reverse Logistics

## §9 Layout & Shell Contract (proposed)

**Shell: tenant.** §22's page table marks the RoutePath *"(approved; not a route yet)"*; the route exists.

- **Shell.** `Layout = "_LayoutTenantShell"` at `Views/SupplyChain/Returns/Index.cshtml:10`.
- **Routes.** One frontend route, `/SupplyChain/Returns`, at
  `Controllers/SupplyChainReturnsController.cs:35` with the view action at `:75`; `api` entries at `:79`,
  `:119`, `:137`, `:162`.
- **Menu.** Only the §22 manifest page `RETURNS` and its navigation keys.
- **DataTable.** `data-dt-standard="v2"` at `_DataTable.cshtml:34`.
- **Localization surface.** Seven languages, **106 keys in each of the seven**, measured.
- **Denied access.** `_AccessDenied` at `Index.cshtml:16`.

The UI is **not** complete.

## §11 Frontend File Contract (proposed)

**Screens.**

| §22 PageCode | PageType | View | Partials it composes |
|---|---|---|---|
| `RETURNS` | List | `Index.cshtml` | `_Filter`, `_DataTable`, `_CreateEditOffcanvas`, `_DetailsQuickView`, `_IndexL10n` |

**Exact form fields.**

| Form | View | Fields | Count | View model |
|---|---|---|---|---|
| Create | `_CreateEditOffcanvas.cshtml` | `formLegalEntity` (:36), `returnShipmentId` (:43), the evidence checkbox (:85) and evidence row input (:93), `returnReasonCode` (:105), and the line row template (:123), each template counted once per field | **6** | `Models/SupplyChain/Returns/ReturnViewModels.cs` |
| List filter | `_Filter.cshtml` | three query controls | **3** | none — query parameters |

Transitions are row actions driven through `api/{returnId}/transition` (`:162`), not a form in this view.

**Golden reference: Slim.** 6 fields, `8 ve altı`, offcanvas on Index. Quick View (`_DetailsQuickView`) is a
row action, which the Compact reference treats as navigation and Slim keeps on the list; it is not a page and
not a manifest action.

**Open items:**

1. **§22 calls the route "not a route yet".** Listed for the owner.
2. **Row 3 (Contract blocker) NOT MET and not CT's.** The code and catalogue declare
   `CHANGE_STATUS` / `supplychain.returns.transition` under the R-2 owner decision while the pack still says
   API-only, and the pack's routing contract is contradicted by the running gateway catch-all. `gateway/**`
   is Protected Paths in every pack; this is the integration owner's.
3. **Row 2 (No-shell) is unmeasured.** Create, lookup, transitions, list and filter were measured live in R-2;
   Save View and column visibility were never exercised.
4. **Row 12 (UX states): the empty list was never produced live.** Five of six states are measured.
5. **Row 5 (Concurrency) MET 2026-10-12**
   (`docs/records/audits/2026-10/mvp6-stage-r-row5-returns-01/`), and **row 10 closed under the owner waiver
   of 2026-10-05** (`docs/records/decisions/2026-10/mvp6-returns-claims-evidence-row10-waiver-owner-decision-01.md`).
6. **Row 13 MET 2026-10-12** over the new HTTP seam
   (`docs/records/audits/2026-10/mvp6-stage-r-row13-02-returns-claims/`).

---

# MOD-0187 Claims Management

## §9 Layout & Shell Contract (proposed)

**Shell: tenant.** §22's M-04 describes `SupplyChainClaimsController` as "to be built"; it exists.

- **Shell.** `Layout = "_LayoutTenantShell"` at `Views/SupplyChain/Claims/Index.cshtml:10`.
- **Routes.** One frontend route, `/SupplyChain/Claims`, at
  `Controllers/SupplyChainClaimsController.cs:29` with the view action at `:65`; `api` entries at `:69`,
  `:88`, `:108`, `:135`.
- **Menu.** Only the §22 manifest page `CLAIMS` and its navigation keys.
- **DataTable.** `data-dt-standard="v2"` at `_DataTable.cshtml:34`.
- **Localization surface.** Seven languages, **95 keys in each of the seven**, measured. Note for whoever
  greps: a second, unrelated `ClaimsIndex.*.resx` set exists under `Resources/Views/CRM/Claims/` with 183
  keys; it belongs to the CRM module and is not this one.
- **Denied access.** `_AccessDenied` at `Index.cshtml:16`.

The UI is **not** complete.

## §11 Frontend File Contract (proposed)

**Screens.**

| §22 PageCode | PageType | View | Partials it composes |
|---|---|---|---|
| `CLAIMS` | List | `Index.cshtml` | `_Filter`, `_DataTable`, `_CreateEditOffcanvas`, `_DetailsQuickView`, `_IndexL10n` |

**Exact form fields.**

| Form | View | Fields | Count | View model |
|---|---|---|---|---|
| Create | `_CreateEditOffcanvas.cshtml` | `formLegalEntity` (:34), `claimShipmentId` (:42), `claimLinkCarrier` (:60), `claimReasonCode` (:71), `claimClaimedAmount` (:80), `claimCurrency` (:91), and the evidence row template (:111), counted once per field | **7** | `Models/SupplyChain/Claims/ClaimViewModels.cs` |
| List filter | `_Filter.cshtml` | three query controls | **3** | none — query parameters |

**Golden reference: Slim.** 7 fields, `8 ve altı`, offcanvas on Index.

**Open items:**

1. **The carrier-link control can never act.** `claimLinkCarrier` (:60) is disabled on all real data — Q424
   measured that the claim carrier path depends on a field no contract writes, and the fixture exists only in
   the lane database. §22's row 2 note calls it *"not a shell, but a control that can never act"*. It is a UI
   field whose backing contract does not exist; this reconciliation records it and closes nothing.
2. **§22 M-04 says the controller is to be built.** Listed for the owner.
3. **Row 3 (Contract blocker) NOT MET and not CT's** — the same gateway catch-all contradiction as MOD-0186.
4. **Row 2 (No-shell) is unmeasured**; Save View and column visibility were never exercised.
5. **Row 7 (Validation): no client case** — the resolve UUID check, the ineligible shipment and a 400/422 that
   keeps inputs were never produced live in R-4c.
6. **Row 12 (UX states): no validation-error state** produced live; five of six are measured.
7. **Row 10 closed under the owner waiver of 2026-10-05**; **row 13 MET 2026-10-12** over the new HTTP seam.
