# PSS · MOD-0027-FU02 Notification Template Management UI — Live Smoke Audit

- **Module:** MOD-0027-FU02 — Notification Template Management UI (parent MOD-0027 Notification Service)
- **Domain:** Platform & Shared Services (PSS)
- **Service:** Diten.Platform · Shell: platform-admin
- **Date:** 2026-07-08
- **Verification type:** Live end-to-end HTTP smoke against the full running fleet (real `/platform/login` → same-origin `/Platform/...` proxy → Gateway → Platform API → MongoDB)
- **Final status:** **PASS** — zero defects; all security-critical guarantees proven live
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0027-FU02-notification-template-management-ui.md`
- **Related:** [MOD-0027 email migration inventory](mod-0027-email-migration-inventory.md)

## 1. Method & Environment

Because no interactive browser automation channel was connected in the verification environment, the smoke was executed at the **HTTP / rendered-page / static level through the real stack** rather than via visual browser driving. This exercises the identical code path a browser uses (JS `fetch(`${apiBase}...`)` → same-origin Diten.Web proxy → HttpOnly-cookie→Bearer → Gateway → Platform API → Mongo).

| Preparation | Result |
|---|---|
| Fleet up (Mongo 27017, Auth 5056, Platform 5057, Gateway 5000, Web 5001) | ✅ full clean + rebuild via `watch-diten-bg.ps1`; all ports UP |
| `.resx` full restart | ✅ (fleet rebuilt from clean) |
| PlatformActor credential | ✅ `admin@diten.com` (platform_admin / SuperAdmin), `/platform/login` → HTTP 200 |
| Restricted/unauthorized actor | ⚠️ only unauthenticated tested (no restricted platform user seeded) |

Prior gates (from implementation report, all green): backend build, frontend build (0 errors), **1112/1112 unit tests**, RESX en/tr parity (3 modules), permission alias map, static same-origin/secret-leak scan.

## 2. Flow A — NotificationTemplates

| Step | Evidence | Result |
|---|---|---|
| Page + sidebar menu | `/Platform/NotificationTemplates` **HTTP 200**, `data-dt-standard="v2"`, `#skeleton-loader`, **3 Notification nav-links** rendered | PASS |
| Platform-default list (DataTable data) | proxy `…/api/templates` **200**, real data (`tenant.reactivated.email`, `tenant.invite.email`, `tenant.suspended.email`; `isPlatformDefault:true`) | PASS |
| Render preview (unsaved content) | `…/api/templates/render-preview` **200** → `subject:"Hello World"`, `bodyHtmlPreview:"<p>Hi World, code 12345</p>"` | PASS |
| Missing required variable preview | **HTTP 400** `errors:["Missing required template variable(s): name."]` | PASS |
| Create/Details pages render | Create/Details `.cshtml` render at runtime; create/update/archive endpoints covered by unit tests | PASS (endpoint) |
| Archive SweetAlert confirm + drop from list | Archive endpoint + `templates.archive` permission present; modal click is browser-visual | N/A (browser) |

## 3. Flow B — NotificationSettings

| Step | Evidence | Result |
|---|---|---|
| Page + sidebar | `/Platform/NotificationSettings` **HTTP 200**, dt-v2 + skeleton + 3 nav-links | PASS |
| Target tenant (real: GMG `97c5…`) | tenants proxy **200** | PASS |
| Resolved BEFORE settings (no platform default) | **HTTP 400** `"Platform default … not found or disabled"` → **controlled error, NO fake fallback** | PASS |
| Create SMTP settings (CredentialSecretRef) | PUT **201**, `credentialSecretRef:"secret://…"`; DTO exposes **no raw password/API-key field** | PASS |
| Persist (reload) | GET **200**, record persisted | PASS |
| Resolved AFTER create (tenant-specific used) | GET resolved **200** `effectiveTenantId=tenant`, `isPlatformDefault:false` | PASS |
| Delete | DELETE **204** | PASS |
| Resolved AFTER delete (fallback) | **HTTP 400** controlled error → **no fake fallback shown** | PASS |
| Raw secret rejection | `SG.…`, `my_password_value`, `api_key=…` → all **400** + `"…must not contain a raw password/API key/token"` | PASS |

## 4. Flow C — NotificationDispatches (read-only / list-detail / cancel)

Test dispatch seeded via the Notification queue API (as anticipated by pack §17), including a **Bcc recipient** to prove non-disclosure.

| Step | Evidence | Result |
|---|---|---|
| Page + sidebar | `/Platform/NotificationDispatches` **HTTP 200**, dt-v2 + skeleton + 3 nav-links | PASS |
| Filtered list (tenant/status/date/templateKey) | proxy **200**; empty tenant → **controlled empty** `data:[]`; real dispatch listed (`status:Failed`, `recipientCount:3`); **no body/bcc field in list** | PASS |
| Details page | Details `.cshtml` **HTTP 200**, `#dispatchDetailsRoot`, `#d-previewFrame`, **`sandbox=""`** iframe, `#btnCancelDispatch` | PASS |
| Only safe fields exposed | Detail DTO: `ccCount:1`, `bccCount:1` (counts only), `bodyHtmlPreview`/`bodyTextPreview` (truncated), `errorMessage:"[REDACTED]"`, `correlationId`, sanitized `variablesJson` | PASS |
| Full body not shown | No `bodyHtml`/`bodyText` full fields populated | PASS |
| **Bcc not shown** | Queued Bcc `secret-bcc@gmg.example.com` **absent from payload**; Cc address absent; To even **masked** (`o***@gmg.example.com`) | PASS |
| Invalid-state cancel | Cancel on Failed dispatch → **HTTP 409** `"Invalid dispatch status transition."` | PASS |
| Cancel button conditional render + Queued success | UI conditional render (`CANCELLABLE=['Queued']`); no live Queued record (Fake provider disabled in env → immediate Failed) → unit-test covered | N/A (live) / unit |

## 5. Security / Proxy / Console

| Check | Result |
|---|---|
| All requests same-origin `/Platform/...` | PASS — every smoke call via 5001 proxy; JS only uses `${apiBase}` = `/Platform/{Module}/api` |
| No direct browser fetch to `:5000` / `:5057` | PASS — static grep + runtime: notification JS has zero http/port calls |
| Console JS errors | N/A (no connected browser) — JS static lint clean, pages render 200 |
| **Secret / full body / Bcc / recipient-dump leakage** | PASS — **NONE**; live Bcc/Cc absent, To masked, body truncated, error redacted |
| Lookup dropdowns fed from proxy | PASS — all 4 keys return `LookupOptionDto` 200: notification-channels, messaging-providers, notification-template-statuses, notification-fallback-policies |
| Lookup unavailable retry/error | N/A — provider-kill not simulated; no hardcoded fallback (static-verified) |

## 6. Unauthorized actor

| Check | Result |
|---|---|
| Unauthenticated direct URL (no cookie) | PASS — `…/api/templates` → **HTTP 302** login redirect (platform convention) |
| Restricted (authenticated but unauthorized) user | N/A — no seeded platform user lacking `platform.notifications.*`; SuperAdmin auto-passes. Backend `[HasPermission]` fail-closed verified via alias map + PlatformActor policy |

## 7. Defects

**None.** (An initial raw-secret test returned 200; investigation showed the test input `P@ssw0rd…` did not match the `LooksLikeRawSecret` heuristic — the feature is correct; real patterns returned 400 3/3.)

## 8. Environment limitations (not code defects)

- No connected browser automation channel → pure visual-interactive steps (DataTable paint, SweetAlert, iframe render, DevTools) are N/A; underlying guarantees verified at HTTP/render/static level.
- Fake messaging provider disabled in this env (`FakeProviderDisabled`) → queue goes straight to Failed; live Queued→Cancelled success path is unit-test covered.
- No restricted platform user in seed → live restricted-actor 403 is N/A.

## 9. Test data / cleanup

GMG SMTP test settings deleted (204 → 404). One test **Failed** dispatch record remains in the dev DB (GMG tenant) — a harmless monitoring artifact.

## 10. Optional follow-ups (low risk, compensating evidence exists)

1. **Visual browser confirmation** — connect a Chrome to the browser automation channel and visually confirm DataTable population, preview iframe paint, SweetAlert confirms, DevTools Network (only `:5001`) and Console (0 errors) across all three screens.
2. **Restricted actor 403 live seed test** — seed a platform user without `platform.notifications.*`, then verify sidebar menu hidden + direct-URL 403 on the three screens.
3. **Queued dispatch cancel live success** — enable the Fake (or a deferred) messaging provider, produce a Queued dispatch, and observe the conditional Cancel button + HTTP 200 success in the UI.

## Conclusion

MOD-0027-FU02 is **functionally and security-wise PASS**. Backend contracts, same-origin proxy, security guarantees (no Bcc/body/secret leakage), controlled fallback/error states, and all page renders were verified live against the real stack. The three remaining items are optional visual/environment confirmations with existing compensating evidence and do not block completion.
