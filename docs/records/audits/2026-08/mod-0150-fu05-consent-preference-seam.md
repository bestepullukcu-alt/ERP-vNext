# MOD-0150-FU05 — Consent / Preference Seam

**Date:** 2026-07-20 · **Service:** `Diten.CrmService` + `Diten.Web` + `Diten.AuthService` (seed) · **Verdict:** **PASS**

Read-only MOD-0164 consent/preference **seam** projected onto Contact 360. No consent engine, no capture, no approval
workflow, no hard dependency on MOD-0164 (which does not exist yet), no fake/default consent state.

---

## 1. Preflight

| Check | Result |
|---|---|
| MOD-0150 status | %90 (FU01/FU02/FU03/FU04/FU06 PASS) |
| FU01–FU06 dependency | Contact foundation + 360 overview handler + Details view present |
| MOD-0164 status | **Başlanmadı / %0 / pack yok** → no-op path is the real scenario |
| Pack D7 | APPROVED — read-only seam / SetMissing-tolerated, no hard dependency |
| Permission keys | `crm.contact.consent.read`, `crm.contact.preference.read` (pack-approved, not yet seeded) |
| 97c5 grant | filter `p.Key.StartsWith("crm.contact.")` → **auto-covers** the two new keys |
| Protected paths | no forbidden path touched |

## 2. Seam / Contract Summary

| Component | Behavior | MOD-0164 Available? | Result |
|---|---|---|---|
| `IContactConsentPreferenceReader` | `GetSummaryAsync(tenantId, contactId)` → summary | n/a | soft seam, fail-soft contract |
| `NullContactConsentPreferenceReader` (default) | returns `NotAvailable` no-op | No | `ConsentAvailable=false`, `Status="not-available"`, `Source="MOD-0164"`, no channels |
| Permission masking (handler) | neither `consent.read` nor `preference.read` | any | `NotAuthorized` summary; reader **not called** |
| Reader throws | caught → `NotAvailable` | any | Contact 360 stays up (200) |
| Future `HttpContactConsentPreferenceReader` | config-gated, replaces Null when MOD-0164 ships | Yes (future) | **not implemented** — no fake HTTP call to a non-existent endpoint |

No-op summary contract: `ConsentAvailable=false`, `PreferenceAvailable=false`, `ConsentStatus/PreferenceStatus="not-available"`,
`Source="MOD-0164"`, `Message="Consent and preference data is not available yet."`, `Channels=[]`.

## 3. Implementation Summary

- **Backend reader:** `IContactConsentPreferenceReader` (Application) + `NullContactConsentPreferenceReader` (Infrastructure,
  default DI registration). Fabricates no state, makes no network call.
- **Contact overview:** `ContactOverviewDto` gains `ConsentPreferenceSummary`. `GetContactOverviewHandler` injects the reader
  + logger; masks when unauthorized (reader skipped → no data leak), otherwise calls the reader inside a try/catch that
  degrades to `NotAvailable`. `GetContactOverviewQuery` carries `CanReadConsent`/`CanReadPreference` (default false).
- **Permission resolution:** `ContactController.Overview` reads the two perms off the caller's claims via new
  `PermissionClaims.HasPermission` and passes them into the query. Base 360 still requires only `crm.contact.overview.read`.
- **Frontend Details:** the existing Consent/Preferences placeholder is wired to the seam summary — read-only. Shows
  "not available yet" (no-op), "not authorized" (masked), or a read-only channel table if data ever arrives. **No** capture
  form, checkbox, toggle, or grant/revoke control.
- **Permissions:** `crm.contact.consent.read` + `crm.contact.preference.read` added to `DataSeeder` (module `crm-contact`);
  the existing 97c5 Admin grant auto-covers them via the `crm.contact.` prefix filter.
- **Tests:** +5 CrmService tests (no-op summary, permission masking + reader-not-called, fail-soft on throw, no-fake-state,
  no consent-capture fields on CreateContactCommand).

## 4. Changed Files

| File | Change | Why |
|---|---|---|
| `Application/Features/ConsentPreference/ContactConsentPreferenceModels.cs` | new | Summary + channel DTO + NotAvailable/NotAuthorized factories |
| `Application/Features/ConsentPreference/IContactConsentPreferenceReader.cs` | new | soft read-only seam contract |
| `Infrastructure/ConsentPreference/NullContactConsentPreferenceReader.cs` | new | default no-op reader (no fake state, no network) |
| `Application/Features/Contact/ContactModels.cs` | edit | `ContactOverviewDto.ConsentPreferenceSummary` |
| `Application/Features/Contact/Queries/ContactQueries.cs` | edit | `GetContactOverviewQuery` + permission flags |
| `Application/Features/Contact/Handlers/QueryHandlers/ContactQueryHandlers.cs` | edit | reader call, fail-soft, permission masking |
| `Api/Controllers/CRM/ContactController.cs` | edit | resolve consent/preference perms from claims |
| `Infrastructure/Authorization/PermissionClaims.cs` | new | claim-based permission check helper |
| `Infrastructure/DependencyInjection.cs` | edit | register default reader |
| `AuthService/.../Seed/DataSeeder.cs` | edit | seed the two permission definitions |
| `Diten.Web/Models/CRM/ContactViewModels.cs` | edit | summary + channel view models |
| `Diten.Web/Views/CRM/Contacts/Details.cshtml` | edit | bind placeholder to seam (read-only) |
| `Diten.Web/Resources/Views/CRM/Contacts/ContactIndex.{7 lang}.resx` | edit | +5 keys × 7 languages |
| `tests/.../ContactFoundationTests.cs` | edit | +5 FU05 tests + fakes; handler ctor updated |

## 5. Smoke Proof

| Step | Evidence | Result |
|---|---|---|
| CrmService restart | `crm:200 gw:200` | ✅ |
| Contact overview route (Gateway, no token) | `GET /api/crm/contacts/{id}/overview → 401` | ✅ routed + guarded (not 404/500) |
| Handler behavior (unit) | 77/77 tests incl. no-op summary + mask + fail-soft | ✅ |
| RESX 7-lang parity | 52 keys each, +5 new keys 5/5 per lang | ✅ |

## 6. Permission Proof

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| Caller has consent/preference perm | reader consulted, no-op summary | `Status="not-available"`, reader called | ✅ (unit) |
| Caller lacks both perms | masked, reader not called | `Status="not-authorized"`, `WasCalled=false` | ✅ (unit) |
| Base 360 without consent perm | overview still 200 | overview succeeds, block masked | ✅ (unit) |
| Permission definitions | seeded + granted to 97c5 | added; 97c5 grant covers `crm.contact.*` | ✅ (code) |

## 7. Failure Path Proof

| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| MOD-0164 absent | no-op summary, 360 works | `NotAvailable`, 200 | ✅ |
| Reader throws | caught, degrade to not-available | 360 still 200, `Status="not-available"` | ✅ (unit) |
| No token | 401 at Gateway | 401 | ✅ |

## 8. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `dotnet build CrmService.Api` | 0 err / 0 warn | scratchpad output (running svc unlocked) |
| `dotnet test CrmService.Application.Tests` | **77/77 pass** | +5 FU05 |
| `dotnet build AuthService.Api` | 0 err / 1 pre-existing warn | seed changed |
| `dotnet build Diten.Web` | 0 err / pre-existing warns | view + VM + resx |
| RESX parity (7 lang) | 52 each, +5 new 5/5 | ✅ |

## 9. Boundary / SoR Check

| Object/Capability | Owner | Touched? | Boundary Risk |
|---|---|---|---|
| Consent engine / capture / approval | MOD-0164 | No | none |
| Preference definition ownership | MOD-0164 | No | none |
| Consent/preference **read seam** | MOD-0164 → MOD-0150 read-only | Yes (read-only) | none (no-op, fail-soft) |
| Contact create/update | MOD-0150 | No consent field added | none |
| Import/export | MOD-0150 FU06 | No consent field added | none |
| AccountContactLink / AccountRelationship | MOD-0150 | No | none |
| Zone/Territory/SalesRep | out of CRM core | No | none |

## 10. Out-of-Scope Guard

| Forbidden Item | Found? | Status |
|---|---|---|
| Consent capture form / toggle / grant-revoke button | No (only comments asserting absence) | ✅ |
| MOD-0164 fake data / hardcoded granted-denied default | No | ✅ |
| Consent field on Create/Update command | No | ✅ |
| Consent field in import/export models | No | ✅ |
| Direct 5061 frontend call | No (only comment asserting absence) | ✅ |
| `crm.contact.360.read` | No | ✅ |
| Zone/MicroZone/Territory/SalesRep | No | ✅ |
| Fake HTTP call to non-existent MOD-0164 endpoint | No | ✅ |

## 11. Open Items / Blockers

| Item | Severity | Owner | Blocks Closeout? | Notes |
|---|---|---|---|---|
| Authenticated browser smoke (Details consent block render) | Low | operator | No | needs runtime 97c5 token + AuthService restart to apply the new grant |
| MOD-0164 HTTP reader | Low | MOD-0164 | No | future config-gated upgrade; no endpoint exists yet |

## 12. Registry / Status Update

- **Previous:** MOD-0150 — FU06 done, %90
- **New:** MOD-0150 — FU05 consent/preference seam done, **%95**
- **Reason:** read-only consent/preference seam implemented, no-op + fail-soft + permission-mask proven, Contact 360 stable.

## 13. Final Verdict

**PASS** — Consent / Preference read-only seam implemented; no-op behavior (MOD-0164 absent), fail-soft (reader throw), and
permission masking (no data leak) all proven; Contact overview/details stable; no consent engine/capture, no fake data, no
hard dependency, no build break, no permission leakage.

## 14. Next Recommended Prompt

**MOD-0150 Final Validation / Closeout Gate** — full-module validation across FU01–FU06 (+FU05): build/test matrix,
permission catalog + 97c5 grant reconciliation, Gateway route inventory, boundary/SoR re-check, and MOD-0150 closeout to
100% (or an explicit residual list for any deferred authenticated smokes / MOD-0164 upgrade).
