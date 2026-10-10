# MOD-0187 Claims — tenant UI + self-registration DRAFT overlay (Q64a)

> **DRAFT — not built, not writer-complete.** Written in the chat lane without a .NET SDK, database or browser. Nothing
> here has been compiled, run or runtime-verified. Q64b (Mac lane) builds, tests and runtime-verifies this exact
> overlay. Agent PASS ≠ CT ACCEPTED.

| Field | Value |
|---|---|
| Work package / prompt | MVP6-WP-187-UI-DRAFT-01 / Q64a v1.0 |
| Lane | AL-MVP6-187-UIDRAFT-01 (DEV, draft overlay) |
| Base | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (verified at preflight and at the end) |
| Pack | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` §32 (UI), §33 (self-registration) |
| Backend read | accepted archive `mvp6-mod0187-normal-baseline-integration-01/normal-source.tar.gz` `edb759a0…` (read only) |
| Conventions read | Golden Reference Slim (checkout, read only); Shipment/Carrier UI in the A12 archive (conventions only, nothing copied) |
| Owner decisions applied | Claims Phase 1.5 approved (per prompt, see F1); SR-D4 overlay rule; modules-first (`mvp6-ct-owner-decisions-modules-first-2026-09-26.md`); draft code overlay in chat lanes; Q03a no commit |

## Contents

| Path | What |
|---|---|
| `FILE-PLAN.tsv` | every file, its future repo path, class and the pack §/PH15 row that justifies it (36 rows) |
| `overlay/frontend/…`, `overlay/services/…` | 23 module files at their future repo-relative paths (21 owned UI paths of §32.10 + provider + provider tests of §33) |
| `overlay/_shared-integration/` | SR-D4 patch-style handoff for the single integration owner (12 files + README); never mixed with module files |
| `runtime-scenarios/` | Playwright scenarios + README for Q64b (never run) |
| `STATIC-CHECKS.txt` | static checks actually run here (63/63 PASS) + dry-run of file-based test assertions (29/29 PASS) |
| `NOT-VERIFIED.md` | what could not be verified (build, analyzers, tests, runtime, …) |
| `claims-ui-draft-overlay.tar.gz` (+ `.sha256`) | archive of `overlay/` |
| `SOURCE-MANIFEST.tsv` | path → sha256 of every file in the archive |
| `SHA256SUMS` | sha256 of every file in this folder (except itself) |

## Scope coverage (pack §32 / §33)

| Item | Covered by | Status in draft |
|---|---|---|
| §32.1 Identity — no new ID | all files carry MOD-0187 only | covered |
| §32.2 Tenant shell, view folder, route, 3 states | every `.cshtml` states `Layout = "_LayoutTenantShell";`; `_DataTable.cshtml` skeleton / table+empty / error | covered (partials state it too; Razor ignores layout for partials) |
| §32.3 Bound operations only | controller: list, shipment resolve, create, transition; no by-ID GET, detail, edit, delete, bulk, import/export, paging | covered |
| §32.4 Routes, permissions, headers | `SupplyChainClaimsController` (4 adapter routes + page); keys from `ClaimPermissions.cs:4-8` + `supplychain.shipments.read`; exact target map mirrors `ClaimModels.cs:45`; no tenant/LE headers to Claims; root as `X-Correlation-Id` on POST; antiforgery on POST | covered (F8, F9) |
| §32.5 List profile | `new DataTable(…, DtDefaults.create({serverSide:false, stateSave:false}))`; filters status (single Select2 + ShowAll) and shipmentId text; 6 columns; exact amount text LTR; malformed envelope → error; Save View via `personalizationClient`; QuickView with no request | covered (A6 codes) |
| §32.6 Create offcanvas (6 fields) | `_CreateEditOffcanvas.cshtml` + `index.js`: resolve with stale guard, carrier checkbox, presence-only reason, amount string as typed, currency as typed, repeatable evidence (omitted when empty), eligibility note + disabled submit | covered |
| §32.7 Transitions | arrows from `ClaimLifecycle.cs:4-7`, exact key per target, `occurredAt` with offset (default now), `approvedAmount` only for Approved, settle label "no payment posted", `window.showConfirm` | covered (F6 G-MODAL, F7 G-DATETIME) |
| §32.8 Errors, replay, concurrency | 18 codes localized; one pending request per intent; same key + identical body text on retry; 409 blocks the intent; replay shown as completed then reload; support reference copyable | covered (F8) |
| §32.9 L10n, UAS-001, a11y | `ClaimsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (95 keys each, parity 7/7), `_IndexL10n.cshtml` + `index.l10n.js`; `_AccessDenied` gate first; LTR isolation for UUIDs/amounts; hidden offcanvases `inert` | covered (N10, N11) |
| §32.10 21 owned paths; protected paths; shared handoff | 21 files at owned paths; shared items only in `_shared-integration/` | covered |
| §32.11 Acceptance matrix | tests + Playwright scenarios mapped to CU rows (see `runtime-scenarios/README.md`) | written, not run |
| §32.12 Test expectations | 3 frontend test files; verifier run is record-only (N8) | written, not run |
| §32.13 Gaps | approved-amount list gap (F5), G-MODAL (F6), G-ICONMAP (F10), G-DATETIME (F7) | recorded |
| §32.14 Authorization boundary | see F2 | recorded |
| §33 Identity / page / 7 actions | `ClaimsManagementManifestProvider.cs` | covered |
| §33 Nav keys ×7 | `_shared-integration/sharedresource-nav-keys/*.fragment.xml` (2 keys × 7) | proposed values (N10) |
| §33 Tests M-01…M-08 | `ClaimsManagementManifestProviderTests.cs` (+ key grammar); W-01 frontend half in `SupplyChainClaimsControllerTests` | written, not run |

## FINDINGS

- **F1:** No Claims Phase 1.5 approval record was found at `docs/records/audits/2026-09/mvp6-ct-owner-decisions-ph15-loadsc-2026-09-26.md`, and none was found in the 2026-09 audits or decisions folders. The approval is taken from the Q64a prompt (A1).
- **F2:** Pack §32.14 says "UI code not authorized until an integrated target exists and a versioned UI dispatch is released". Two later owner decisions change that: modules-first (`mvp6-ct-owner-decisions-modules-first-2026-09-26.md` `63e8601e…`) and "draft code overlay in chat lanes" (CT, about 16:17). This draft follows those decisions. The pack wording should be reconciled by CT; no pack edit was made here.
- **F3 (build blocker for Q64b):** The overlay compiles only on top of the **A12 360 overlay** `7b6a0d1a…`. It needs these pieces, none of which is in the common checkout:
  - `Diten.Web.Security.JsonAdapterEndpointAttribute` and the JSON challenge branch in frontend `Program.cs`;
  - the SupplyChain registration foundation: `IModuleManifestProvider`, `ModuleRegistrationHostedService` and the Api `.csproj` reference to `Diten.BuildingBlocks.ModuleRegistration.Abstractions`;
  - `Nav.Domain.SUPPLYCHAINEXECUTION`.

  The "Isolated environment" row of PH15-UI-187 lists only HEAD, BC-SOURCE and Auth 22. Q64b must add the A12 360 overlay and the Claims archive `edb759a0…`. Both change SupplyChain `Program.cs`; the Claims archive's Api `.csproj` lacks the Abstractions reference.
- **F4:** Currency is sent exactly as typed, and the backend requires `\A[A-Z]{3}\z` (`ClaimModels.cs:32`). A lowercase value therefore gets a 400 `INVALID_REQUEST`, as CU-11 expects. There is no field-level code, so the form shows the generic localized 400 text ("safe field mapping only").
- **F5:** `approvedAmount` is not in `ClaimSummary`, so it is not listed. It appears only in the approval success message, and QuickView says so. A contract change is not authorized.
- **F6 (G-MODAL):** `showConfirm` cannot host inputs; it inserts a subtext into its dialog, per `_GlobalConfirmation.cshtml:178ff`. The transition inputs therefore live in a module-owned offcanvas inside the owned `Index.cshtml`, which avoids adding a 22nd path. Final confirmation still goes through `window.showConfirm`. CT should confirm this placement, or amend the scope to add a separate partial.
- **F7 (G-DATETIME):** `occurredAt` is a plain LTR text input, prefilled with local now and its explicit offset. The exact text is kept for retries. No shared date-time component was chosen.
- **F8 (root echo):** The pack says the lifecycle root never reaches the browser. The Claims backend, however, echoes the incoming `X-Correlation-Id` (the root) in the response header and in `error.correlationId`. The adapter therefore:
  - passes success bodies through unchanged;
  - replaces the header, and the `correlationId` inside error envelopes, with the browser's trace;
  - keeps the backend status and error code;
  - logs trace ↔ root.

  CU-23 (header = `error.correlationId`) still holds at the browser boundary. The support reference is the adapter trace, not the backend correlation. CT and the owner should confirm this approach.
- **F9:** Header policy is mixed by design:
  - Shipment lookup follows the existing Shipment adapter policy: tenant/LE headers from claims, and the browser trace.
  - The Claims family never receives tenant/LE headers (pack §32.4).
  - Both are listed as integration checks.
- **F10 (G-ICONMAP):** `ICON_MAP` in `diten-field-icons.test.js` is bound to the Task form. Claims icons are proposed in `_shared-integration/icon-map.proposal.md`.
- **F11:** The prompt names `ClaimsModuleManifestProvider`, while pack §33 names `ClaimsManagementManifestProvider` at a specific path. The pack name and path were used.
- **F12:** Different sources give different targets:
  - PH15-UI-187's L10n row says "all 12 `CLAIM_*`/`INVALID_CLAIM_TRANSITION` codes", but pack §32.9 says "all 18 annex codes". The draft localizes all 18.
  - PH15 names the SR-D4 folder `SHARED-OVERLAY/`, but the prompt says `overlay/_shared-integration/`. The prompt was followed.
- **F13:** `DtDefaults.create` replaces `language.emptyTable` with the shared `DtEmptyTable` text when that text exists (`dt-defaults.js:501-520`). The module `EmptyState` key is therefore a fallback only (N13).

## ASSUMPTIONs

- **A1:** Claims Phase 1.5 is approved, as the prompt states (see F1).
- **A2:** The transition's `shipmentId` lookup hint travels as a query parameter on the approved MVC route: `POST /SupplyChain/Claims/api/{claimId}/transition?shipmentId=`. This keeps the body byte-exact; the hint is not forwarded to the Gateway.
- **A3:** Adapter-local failures reuse existing annex codes only:
  - `FORBIDDEN`, `UNAUTHENTICATED`, `INVALID_REQUEST`, `UNSUPPORTED_MEDIA_TYPE` → the matching HTTP status;
  - Shipment 404 → 404 `CLAIM_NOT_FOUND` (the single safe-not-found);
  - Shipment 5xx or timeout → 503 `CLAIM_REFERENCE_UNAVAILABLE`;
  - null or empty root → 503 `CLAIM_REFERENCE_INCOMPLETE`;
  - malformed root → 502 `CLAIM_REFERENCE_INVALID`;
  - transport failure toward Claims → 503 `CLAIM_STORAGE_UNAVAILABLE` (outcome unknown).
- **A4:** The browser sends a fresh UUID `X-Correlation-Id` on every request. The adapter rejects a missing or invalid value with 400, the same as the Shipment adapter.
- **A5:** The antiforgery header is `RequestVerificationToken`, following the golden-slim pattern.
- **A6:** Save View uses `moduleKey: 'claims-management'`, `pageKey: 'CLAIMS'` (the §33 codes) until the integration owner confirms the personalization codes.
- **A7:** `showToast` and `showConfirm` receive resx text plus shape-checked values only: a UUID reference or a decimal amount. They are therefore safe whether the wrapper renders text or HTML.
- **A8:** The status column shows "Settled"; the transition target and action carry the "no payment posted" wording.
- **A9:** Optional `resolutionCode`, `note` and evidence rows are omitted when empty. A direct POST can still send `""`, which CU-12 covers at HTTP level.
- **A10:** The create CTA is the DataTables toolbar "Add Claim" button (golden slim). It is rendered only when the user has `create` + `shipments.read`.
- **A11:** Playwright switches culture through the locale plus a query string; Q64b may switch to the culture cookie.
- **A12:** The static checks ran in the chat-lane Linux workspace under `/tmp/q64a-checks`, not on the Mac.
- **A13:** Nav key values for `Nav.Module.CLAIMSMANAGEMENT` and `Nav.Page.CLAIMS` are proposals. The pack does not supply values, and the l10n agent reviews them.

## Applying the overlay (Q64b, isolated environment only)

1. Build the environment in this order: HEAD archive → BC-SOURCE → A12 360 overlay → Claims `normal-source` (reconcile `Program.cs`/`.csproj`, F3) → Auth 22.
2. Copy `overlay/frontend/**` and `overlay/services/**` to the same repo-relative paths.
3. For routing and the nav guard in the isolated environment only, apply `_shared-integration/` items to the *environment copy* (never the checkout). The single integration owner applies them to the real shared files at final integration (SR-D4).
4. Run `dotnet build` / `dotnet test` (frontend tests + SupplyChain tests), the static checks again, then `runtime-scenarios/`.
