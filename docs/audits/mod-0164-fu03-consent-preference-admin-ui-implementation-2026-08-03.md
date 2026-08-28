# MOD-0164-FU03 — Consent & Preference Admin UI — Implementation Evidence

- **Date:** 2026-08-03
- **Module pack:** `execution/domains/commercial-suite/module-packs/MOD-0164-FU03-consent-preference-admin-ui.md` (PASS — ready-for-dev)
- **Service:** `frontend/Diten.Web` (UI only)
- **Golden reference:** Compact (15 form fields)
- **Branch:** `feature/crm/mod-0164-fu03-consent-preference-admin-ui`
- **Verdict:** **PARTIAL** (implementation + build + UI tests PASS; positive authenticated browser smoke deferred; canonical RBAC seed absent → documented FU02 territory fallback; several pack-listed preference fields are not in the FU02 contract → documented limitations)
- **Post-ship addenda (user-directed):** see **§24 (Golden Compact redesign of Consent Create)** and **§25 (SCOPE EXTENSION — dependent subject picker)** below.

---

## 1. Preflight

| # | Check | Result |
|---|---|---|
| 1 | Module pack file present | ✅ |
| 2 | Pack status ready-for-dev | ✅ |
| 3 | FU02 runtime/evidence PASS (65/65) | ✅ (read `ConsentsController`, `PreferencesController`, DTOs, contract, evaluator) |
| 4 | Consent/Preference Gateway routes exist | ✅ (ocelot `{everything}` wildcard; pack §15) |
| 5 | `GET /api/crm/consents/contract` exists | ✅ (`GetConsentContractHandler`) |
| 6 | `_LayoutTenantShell` narrow exception authorized | ✅ (pack §6) |
| 7 | Only `/CRM/ConsentPreferences` nav entry changed | ✅ |
| 8 | GoldenReferenceCompact sources available | ✅ (Campaign reference read) |
| 9 | GoldenReferenceSlim archive/toast/modal available | ✅ (`window.showConfirm`/`window.showToast`) |
| 10 | 7-language RESX present | ✅ (generated) |
| 11 | Existing permission pattern verified | ✅ (`RequirePage`/`PermissionClaims.HasPermission`/`IPermissionSnapshot`) |
| 12 | No direct :5061 business call | ✅ |
| 13 | No DELETE | ✅ |
| 14 | No backend/Gateway/runtime edits | ✅ |
| 15 | Pre-existing working-tree changes preserved | ✅ (only §5 paths touched) |

## 2. Dependency Confirmation

- FU02 API contract consumed verbatim: `CreateConsentRecordRequest`, `UpdateConsentRecordRequest`, `CreatePreferenceRecordRequest`, `UpdatePreferenceRecordRequest`, `ConsentRecordDto`, `PreferenceRecordDto`, `ConsentContractDto`, `ConsentEvaluationResult`.
- Immutable-on-update dimensions confirmed from the FU02 update request bodies (they omit the question dimensions) — UI mirrors this: update payload never sends `SubjectType/SubjectId/Channel/Purpose/ScopeType/ScopeId` (consent) or `SubjectType/SubjectId/Channel/PreferenceType` (preference).
- Contract vocabulary (`ConsentVocabulary`) surfaced to the UI; UI reads it first, then falls back to the runtime canonical constants only when the contract cannot be read.

## 3. Scope Confirmation

Implemented exactly the pack in-scope surface: navigation entry, consent list/detail/create/edit/archive, preference list/detail/create/edit/archive, evaluate test panel, `ConsentPreferenceSubjectPanel`, reasonCodes/provenance display, contract-driven gating, Gateway-only proxy client, permission-controlled visibility, 7-language RESX, UI tests, build, evidence. No runtime/Gateway/RBAC/registry/Mongo/MOD-0048/MOD-0155 work performed.

## 4. UI Implementation Summary

A single tenant-shell surface at `/CRM/ConsentPreferences` hosts four permission-gated tabs — **Consents**, **Preferences**, **Evaluate**, **Subject** — plus full-page Compact Create/Edit/Details for both aggregates. All business traffic flows through same-origin MVC proxy actions that forward to Gateway 5000 with the server-held bearer token and `X-Tenant-Id` claim; the browser never sees a service URL or token, and no `TenantId` is ever placed in a payload.

## 5. Protected Navigation Implementation

`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`: a single `<li>` added in the Commercial Suite group (after Campaigns), guarded by `Perms.Has("crm.consent.read")`, active route `currentPath.StartsWith("/CRM/ConsentPreferences", …OrdinalIgnoreCase)`, label from the localized shared key `ConsentPreferencesMenu` (7 languages). The Commercial Suite header boolean was minimally extended to also consider the consent read permission. `DynamicModuleMenu`, other menu items, and shell behavior are untouched.

## 6. Golden UI Implementation

- Both list surfaces use `data-dt-standard="v2"`, `#skeleton-loader`, `_Filter` collapse host, DtDefaults export/colvis/filter button, `searching:false`, colReorder, and localized empty/loading/error states.
- Create/Edit/Details are full-page Compact with a shared `_Form.cshtml`; **no** `_CreateEditOffcanvas`/`_DetailsQuickView` (Compact rule honored).
- `_Form` and `Details` share the same ordered `<section>` heading keys (consent: Identity/ConsentStatus/Evidence/ExternalReferences; preference: Identity/PreferenceContext/ExternalReferences) for section parity.
- Archive uses `window.showConfirm`; success/error use `window.showToast`. No raw `confirm`/`alert`/`Swal.fire`.

## 7. Route / Controller / View Structure

Controller `Controllers/CRM/ConsentPreferencesController.cs` (`[Route("CRM/ConsentPreferences")]`, `[Authorize]`):
`GET /` · `GET /Evaluate` · `GET /Subject` · `GET|POST /Consents/Create` · `GET|POST /Consents/{consentId}/Edit` · `GET /Consents/{consentId}` · `GET|POST /Preferences/Create` · `GET|POST /Preferences/{preferenceId}/Edit` · `GET /Preferences/{preferenceId}` · proxy: `GET api/contract`, `GET api/consents`, `GET api/consents/evaluate`, `GET api/consents/{id}`, `POST api/consents/{id}/archive`, `GET api/preferences`, `GET api/preferences/{id}`, `POST api/preferences/{id}/archive`.
Views under `Views/CRM/ConsentPreferences/**`; scripts under `wwwroot/assets/js/CRM/ConsentPreferences/**`; every Razor page sets `Layout = "_LayoutTenantShell"`.

## 8. Gateway API Client

Server-side proxy (`SendGatewayAsync`) is the only egress; base is `configuration["GatewayUrl"]` (5000). Allowlist matches pack §15 exactly. No HTTP `DELETE`, no `:5061`, no `TenantId` payload (guarded by `ContainsTenantId`). Path params are the real FU02 names `{consentId}`/`{preferenceId}`. Backend `errors`/reasonCodes are surfaced to toast/detail.

## 9. Contract-driven UI

`GET /api/crm/consents/contract` is loaded once by `index.js` and broadcast (`consent-preference:contract-ready` + `window.ConsentPreferenceContract`) to the evaluate and subject scripts. Vocabulary drives all option lists; `supportsConsentManagement`/`supportsPreferenceManagement` toggle the create buttons; contract failure shows a controlled error and disables actions (fail-closed). Create/Edit server actions also gate on the contract and fall back to runtime canonical vocabulary when it is unreadable.

## 10. Consent list/detail/create/edit/archive

- **List** columns per pack §I. Server filters: SubjectType, SubjectId, Channel, Purpose, ConsentStatus, IncludeArchived. **Unsupported filters documented, not faked:** Search, ScopeType, ScopeId, LegalBasis, Source, date range.
- **Detail** shows identity, consent question, legal/status, evidence pointer (pointer only — no file/URL render), external references, and an audit/provenance card. Never presented as a general permission flag.
- **Create/Edit** full-page Compact; required + effective-range validation; immutable question dimensions rendered read-only (disabled select + hidden field / readonly input) on Edit; consent channel list excludes `all`.
- **Archive** via `POST …/archive` + confirm/toast; archived record is read-only; no DELETE.

## 11. Preference list/detail/create/edit/archive

- **List** columns per pack §L, with the caveat below. Server filters: SubjectType, SubjectId, Channel, PreferenceType, IncludeArchived. Unsupported documented: Search, ScopeType, ScopeId, IsRestrictive.
- **`IsRestrictive` is a derived display hint** (computed from `preferenceType ∈ {do-not-contact, do-not-visit}`), not a stored FU02 field. It is labeled as a hint; the authoritative restrictive determination is the evaluate result's `CandidatePreference.Restrictive`.
- **Detail** shows the mandatory copy “A preference can restrict consent but cannot grant consent.” Archived records read-only.
- **Create/Edit** Compact; Priority `>= 1` enforced (model `[Range]` + client); immutable dimensions read-only on Edit; preference channel list includes `all`.
- **Archive** via `POST …/archive`; no DELETE.

## 12. Evaluate Test Panel

Read-only panel calls `GET /api/crm/consents/evaluate`. Requires subject/channel/purpose. Renders allowed (green) / blocked (red) / unknown (grey) badges; **unknown is never shown as allowed** (`status !== 'unknown'` note + explicit “Unknown is NOT allowed” copy). Shows EligibilityStatus, Decision, ReasonCodes, SelectionReason, MatchedConsentId, MatchedPreferenceIds, EvaluatorVersion, EvaluatedAt, plus expandable candidate-consent/candidate-preference diagnostics. A backend 400 is surfaced as an error, never coerced to allowed.

## 13. ConsentPreferenceSubjectPanel

Reusable `_SubjectPanel.cshtml` + `subject-panel.js`. Given SubjectType + SubjectId it loads that subject's consents and preferences and offers evaluate / create-prefilled quick actions. It never mutates Contact/AccountContactLink and never writes a flat ConsentStatus; consent and preference stay separate aggregates. **Embedding into a Contact / AccountContactLink parent screen is a documented follow-up** (no such parent UI in scope).

## 14. ReasonCodes / Provenance Display

`_Provenance.cshtml` renders reasonCodes as compact badges, selection reason, matched IDs (provenance only), evaluator version/time, and an expandable diagnostics panel with candidate tables. Errors surface via toast summary.

## 15. Permission / Visibility

Canonical keys: `crm.consent.read/.manage/.evaluate`, `crm.preference.read/.manage`. Documented FU02 fallback (`crm.territory.read` reads/evaluate, `crm.territory.model.manage` writes) is honored via the existing resolver — **no seed/grant, no new resolver**. Menu guard stays canonical `crm.consent.read`; if the canonical claim is absent under the fallback, the menu entry is simply hidden — **reported PARTIAL/follow-up** (MOD-0164-FU-RBAC).

## 16. RESX / Localization

`Resources/Views/CRM/ConsentPreferences/ConsentPreferencesIndex.{en,fr,es,zh,ar,ru,tr}.resx` — 135 keys each, identical key sets (verified by test). Shared menu key `ConsentPreferencesMenu` added to all 7 `SharedResource.*.resx`. All new visible text is localized; no hardcoded strings. Includes the required help copy (unknown-not-allowed, preference-cannot-grant, evaluate-read-only).

## 17. Response Shape / UI Data Guard

None of the forbidden fields (`visitPlanId, routePlanId, routeId, dueStatus, overdue, lastVisitDate, requiredVisitCount, periodType, frequencyPolicyId, campaignTargetId, segmentMembership, recommendationId, nextBestAction, workflowApprovalId, contentRenderUrl, filePayload, consentRecordPayloadAsTargetData, preferenceRecordPayloadAsTargetData`) is modeled, expected or rendered (asserted by test #25).

## 18. Tests

- New Vitest file `tests/consent-preference-admin-ui.test.js` — **28/28 PASS** (covers routes, nav guard, contract fail-closed, list/loading/empty/error, supported vs documented-unsupported filters, compact section parity, required/immutable/date validation, `all` sentinel rule, POST-not-DELETE archive, archived read-only, evidence pointer, evaluate GET + badges + provenance, subject panel, no flat ConsentStatus, no :5061, no TenantId, forbidden-field guard, showConfirm/showToast, 7-locale parity).
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` → **0 warnings, 0 errors**.
- `python3 .antigravity/scripts/verify_datatable_page.py …` → **not run (python unavailable in this environment)**; the Golden Compact contract was followed manually (v2 markers, skeleton, _Filter host, DtDefaults, section parity, required-marker/nullable rules). Re-run at closeout if python becomes available.
- Full suite: 147 passed / 9 failed across 6 files — **all 9 failures are pre-existing and unrelated** (objectives, planning-cycles, strategy-apis, strategy-periods; node16/jsdom). None touch ConsentPreferences or the two tracked files this change edits.

## 19. UI Smoke / Manual Verification

Positive authenticated browser smoke against tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` is **DEFERRED** (no live fleet/login in this run; no fake data or Mongo hand-edit performed). Manual/static verification (build + 28 UI assertions) passed. Recommended manual pass: login → menu → consent create → detail → evaluate (allowed) → restrictive preference → evaluate (blocked) → archive → archived read-only → evaluate no-consent (unknown) → verify no DELETE / Gateway-only network → TR/EN locale smoke.

## 20. Explicit Exclusions

No change to: Consent/Preference/Evaluate runtime, Campaign runtime/UI, Gateway/ocelot, MOD-0048 publish, RBAC seed/grant, registry, Mongo, backend, MOD-0155, visit/route/frequency/knowledge/brand/workflow/import-export/patient data. No hard delete, no DELETE method, no direct 5061, no TenantId payload, no flat Contact ConsentStatus.

## 21. Created / Updated Files

**Created**
- `Controllers/CRM/ConsentPreferencesController.cs`
- `Models/CRM/ConsentPreferenceViewModels.cs`
- `Views/CRM/ConsentPreferences/` — `ConsentPreferencesIndex.cs`, `Index.cshtml`, `_IndexL10n.cshtml`, `_EvaluatePanel.cshtml`, `_SubjectPanel.cshtml`, `_Provenance.cshtml`, `Consents/{_Filter,_DataTable,_Form,Create,Edit,Details}.cshtml`, `Preferences/{_Filter,_DataTable,_Form,Create,Edit,Details}.cshtml`
- `wwwroot/assets/js/CRM/ConsentPreferences/` — `index.js`, `index.l10n.js`, `consent-form.js`, `preference-form.js`, `consent-details.js`, `preference-details.js`, `evaluate.js`, `subject-panel.js`
- `Resources/Views/CRM/ConsentPreferences/ConsentPreferencesIndex.{en,fr,es,zh,ar,ru,tr}.resx`
- `tests/consent-preference-admin-ui.test.js`
- `docs/audits/mod-0164-fu03-consent-preference-admin-ui-implementation-2026-08-03.md`

**Updated**
- `Views/Shared/_LayoutTenantShell.cshtml` (single guarded nav `<li>` + minimal header condition)
- `Resources/SharedResource.{en,fr,es,zh,ar,ru,tr}.resx` (added `ConsentPreferencesMenu` only)

## 22. Final Verdict

**PARTIAL** — All in-scope UI implemented, build green, 28/28 UI tests green, Gateway-only, no DELETE/TenantId/5061, Golden Compact/Slim honored, 7-language parity complete. PARTIAL because: (a) positive authenticated browser smoke deferred; (b) canonical `crm.consent.*`/`crm.preference.*` not seeded → documented territory fallback, menu visibility follow-up; (c) pack-listed preference `ScopeType/ScopeId/IsRestrictive` and consent generic `Reason` are absent from the FU02 contract → surfaced as documented limitations / derived hints rather than fabricated fields; (d) python DataTable verifier unavailable.

## 24. Addendum — Golden Compact redesign of Consent Create (2026-08-03, user-directed)

At the user's request the Consent Create/Edit surface was re-aligned to the `GoldenReferenceCompact/Create` design standard (`Views/DevEnablement/GoldenReferenceCompact`):

- Header + breadcrumb + Cancel/Save moved **into** `Consents/_Form.cshtml`; the Save button submits via `form="consentForm"`. `Create.cshtml`/`Edit.cshtml` now only include the partial + `_ValidationScriptsPartial` + `consent-form.js`.
- Two-column layout `row g-4`: main `col-lg-8` (Identity + Legal/Status), sidebar `col-lg-4` (Evidence), full-width (External References).
- Card style `card mb-4` + `card-body p-4` + `h6 text-uppercase text-heading fw-semibold mb-4`; labels `form-label fw-medium`; validation `text-danger small mt-1`; placeholders; `needs-validation novalidate`.
- Selects use `select2 form-select` (globally loaded in `_LayoutTenantShell`); `consent-form.js` initializes Select2.
- **Preserved:** immutable-on-edit read-only behavior; `_Form`↔`Details` section-key parity (Identity → ConsentStatus → Evidence → ExternalReferences). Dates kept as `datetime-local` (consent needs time precision; golden flatpickr is date-only).

## 25. Addendum — SCOPE EXTENSION: dependent subject picker (2026-08-03, user-approved)

**Governance:** This deliberately extends the FU03 Gateway allowlist (§15, consent/preference only) to include **read-only** reads of `/api/crm/contacts` and `/api/crm/accounts`. The user was shown the boundary implication and explicitly approved (“tamam bu şekilde yapp”). It remains UI-only, Gateway-only, GET-only, write-free; the Gateway still enforces the source modules' own read permissions (`crm.contact.read` / `crm.account.read`).

**Behavior:** On the Consent **Create** form, `SubjectId` is a dependent Select2 that reacts to `SubjectType`:
- `contact` → searchable by name via `/api/crm/contacts`; `account` → via `/api/crm/accounts` (`"code — name"`). Server-side substring filter, capped at 30 results.
- `hcp` / `hco` / `account-contact-link` (no list endpoint) and any no-match → raw-GUID entry via Select2 **tags** (the “GUID fallback”). The picked/typed value posts as the `SubjectId` GUID; a non-GUID fails model binding as before.
- On **Edit**, `SubjectId` stays an immutable read-only `<input>` (never a picker).

**Files:** proxy `GET /CRM/ConsentPreferences/api/subjects` + `ParseSubjectItems`/`GetStr` (`ConsentPreferencesController.cs`); `Consents/_Form.cshtml` (create-mode select); `consent-form.js` (dependent picker + tags fallback); 2 new localized keys `SubjectPickerPlaceholder`/`SubjectPickerHelp` (7 languages → 137 keys each). Covered by test #29. Build 0/0; consent UI tests **29/29 PASS**.

**Limitations:** capped at the first 200 upstream rows filtered to 30 (no server search protocol assumed); if the operator lacks `crm.contact.read`/`crm.account.read`, the picker returns empty and the GUID fallback is used. A universal picker for hcp/hco/account-contact-link needs those modules' list endpoints (future authorization).

## 23. Next Recommended Prompt

- `MOD-0165-FU05 — Campaign / Targeting Admin UI Implementation` (already shipped as sibling — confirm), or
- `MOD-0290-FU02 — Brand/Product Runtime + UI`.
- Note: MOD-0048 consent alignment sets may be Submitted/Pending — not a blocker for this UI. MOD-0155 stays on hold. Follow-ups: **MOD-0164-FU-RBAC** (seed canonical keys) and **Contact/AccountContactLink SubjectPanel embed**.
