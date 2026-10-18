---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: pass
commit:
modules: [MOD-0190]
---

# Wave 6.1 G6 — MOD-0190 S&OP tenant UI built; walked live through Web → Gateway → SupplyChain

    run       : 2026-10-18 by CT (gmg-10)
    authority : UI surface owner-approved (option A, `mvp6-sop-capacity-ui-scope-owner-decision-01`, bound to
                `UI-SCOPE-0190.md`); build scoped in `docs/roadmap/plans/mvp6-0190-0192-ui-wave-01/G6-UI-BUILD-SCOPE.md`;
                §24 manifest from the UI-revision patch `mvp6-ui-pack-patch-190-192-01`; owner "start G6 UI build".
    verdict   : **PASS** (composed, seeded non-production host). Capacity (MOD-0192) is the next G6 half.

## What was built

| Part | Files |
|---|---|
| Adapter (proxy-profile) | `Controllers/SupplyChainSandopPlansController.cs` — the six bound operations only; tenant from the claim, legal entity from `?legalEntityId=` (400 otherwise), browser trace (one UUID, else 400 `INVALID_CORRELATION_ID`), `Idempotency-Key` on POSTs; bodies forwarded byte-exact; published-code set, un-enveloped 5xx → 503 `DEPENDENCY_UNAVAILABLE`, correlation replaced by the trace. |
| Permissions | `Models/SupplyChain/SandopPlans/SandopPlanPermissions.cs` (mirror of `SandopPermissions`). |
| Entry page | `Views/SupplyChain/SandopPlans/Index.cshtml` (Create plan + Open plan by ID; no list — F190-LIST) + `_CreateEditOffcanvas.cshtml` (5 fields, GoldenReferenceSlim). |
| Workspace | `Details.cshtml` (header, snapshots, sign-offs) + `_CaptureSnapshotOffcanvas.cshtml` (4 scalars + reference repeater) + `_SignOffOffcanvas.cshtml` (snapshot/role/decision/comment, confirm). |
| Scripts | `wwwroot/assets/js/SupplyChain/SandopPlans/index.js`, `details.js` — camp `ERROR_KEY` + `t()`, one Idempotency-Key per opened form, versioned loads, scope on every call, fail-closed without scope, text-only rendering. |
| l10n | bridge + loader gain `BackToList`, `FieldRequired`; three keys that echoed their own English value renamed (`Name`→`PlanName`, `Created`→`CreatedAt`, `Provenance`→`SnapshotProvenance`); fr `SnapshotProvenance`/`RoleFinance` no longer equal English. Nav keys `Nav.Module.SOPWORKFLOWSIGNOFFS`, `Nav.Page.SANDOPPLANS` × 7 languages. |
| §24 manifest (G4 item C) | `SopWorkflowSignoffsManifestProvider` (identity, pages, actions exactly the patch tables); registered in `Program.cs` **only inside the demand-seed block** — a catalog entry on a host whose routes 404 would be dead navigation. |

**Patch-text correction:** the patch names the page key `Nav.Page.SANDOP_PLANS`; the runtime looks up
`Nav.Page.` + `NavNameLocalizer.Normalize(code)`, which strips `_` → **`Nav.Page.SANDOPPLANS`** (the Claims precedent:
`SHIPMENT_CLAIMS` → `Nav.Page.SHIPMENTCLAIMS`). The nav-L10n guard derives keys with the same method and passes.

## Live walk (guarded lane: mongod rs `rsg6` on 127.0.0.1:35190, MDM 5059, gateway 5000 with signing key, SupplyChain 5066 seeded `dp-g6`/`1`/`sha256:g6-lane`, Web 5001; 27017 untouched)

Signed in with a lane-minted test token through the Web's own `access_token` cookie bridge (set by a one-shot localhost
helper; secret and token stayed in the scratchpad). Every S&OP call went browser → `/SupplyChain/SandopPlans/api…?legalEntityId=` → gateway → 5066.

| Step | Result |
|---|---|
| Entry page | company select filled from MDM through the gateway; Create enabled |
| Create, empty | 5 × "This field is required", no request |
| Create plan | `POST …/api` **201** → redirect to `Details/{id}?legalEntityId=` |
| Workspace load | plan + snapshots + sign-offs **200 ×3**; status Draft; Record sign-off hidden (Draft) |
| Capture snapshot (+1 reference) | **201**; server moved the plan to *In review*; current snapshot set; row marked Current |
| Record sign-off (Finance/Approved, comment with markup) | confirm dialog → **201**; status stays *In review* (pack §16); comment rendered as text |
| Same role again | **409** `SIGN_OFF_ALREADY_RECORDED` → localized text + support reference; first decision kept |
| Unknown plan | **404** → safe not-found + support reference; nothing of a plan drawn |
| No `?legalEntityId=` | "Select a legal entity." and **zero** S&OP API calls |
| Arabic | RTL shell, all text localized, no missing-key warning |

Two defects found by walking it, fixed and re-walked: the Open-plan button was pushed off-screen (button moved
into the ID row; the lookup form opts out of the shared required-fields tracker with `data-no-tracker`), and
localized date-times were scrambled in Arabic (forced LTR around RTL text → `dir="auto"`). Repeater inputs gained
`aria-label`s (their labels had no `for`).

## Tests

- **Diten.Web.Tests 2570/2570.** **SupplyChain 559/560** — the one failure is the recorded
  `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` ("restart mode required"), unchanged.
- `SupplyChainSandopPlansControllerTests` — per-operation path/headers/body, 403 per missing permission (zero calls),
  400 on missing/malformed scope, key, JSON or trace, re-enveloping, view routes = manifest pages, adapter surface = six ops.
- `SandopPlansPageTests` — bridge = loader (F-1), 7 resx parity + no echo + no English placeholder (allow: fr `RefSource`),
  every read key bridged, error map ⊇ the adapter's `PublishedCodes` (reflected), scope on every call + fail closed,
  intent keys, versioned loads, no `innerHTML`, no token/scope header/service URL in the browser, UAS-001 gate first.
- `SandopPlansPageRenderTests` — real web tier: denied card only without read; entry/offcanvas/workspace by permission;
  malformed scope reaches the page empty.
- `SopWorkflowSignoffsManifestProviderTests` M-01…M-08; `CompositionRootGuardTests` +1 (seeded host registers the provider);
  `EnforcedPermissionDeclarationGuardTests` caught the now-false `sandop-plans` "no provider" entry — removed.
- **Sabotage:** un-scoping the workspace read → the scope fact red; dropping `SIGN_OFF_ALREADY_RECORDED` from the map →
  the error-map fact red; both restored byte-identical, green.

## Re-pin

`Program.cs` → `fac31608f8ac377306251a8cf8d7e3871a9522ab556b8c2cdde9a345b1713687`; MOD-0190 `:261`, MOD-0192 `:273`
(previous values kept in the cell). Capacity's provider will move it once more.

## Not covered

Capacity UI (next); G7 permission seeding to tenant roles; G8 independent VER; a real demand source (G3).
`decidedBy` is shown as the actor UUID (no user lookup in scope, UI-SCOPE-0190 §9.5).
