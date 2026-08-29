# MOD-0029-FU36C Reverse Navigation & Legacy Bypass — Implementation Audit

Date: 2026-07-25  
Verdict: PASS WITH RUNTIME GAP  
Commit/push: yapılmadı

## 1. Summary

Controlled Documents reverse Master Register navigation and legacy/direct-create bypass hardening were implemented.

## 2. Inputs reviewed

FU36/FU37 packs; FU36A/B and FU37A/B/C audits; Controlled Documents controller, views, JavaScript and resources;
existing reverse API endpoint, permissions and Master Register route were reviewed.

## 3. Frontend scope delivered

The Controlled Document Details page now contains a read-only Master Register relationship card loaded lazily
through the same-origin MVC proxy.

## 4. Reverse Master Register card

The card shows register title/id, scope/owner, class/type, register/lifecycle status, compatibility status and link
date. Only a compatible relation exposes `/DocumentManagementMasterRegister/Details/{id}`.

## 5. Missing/legacy state handling

Reverse lookup 404 and empty responses produce a neutral legacy/migration/reconciliation message. No create-link
or manual-link action is offered.

## 6. Scope mismatch/unverified state handling

Unvalidated and invalid relationships use warning state, never success. The card states that approval, training
and release readiness remain fail-closed.

## 7. Add Document redirect hardening

The explorer toolbar and normal Controlled Documents Create action route to
`/DocumentManagementMasterRegister/CreateControlledDocument`.

## 8. Template creation preservation

`/DocumentManagementControlledDocuments/Create?kind=template` and the dedicated template POST flow remain intact.

## 9. Legacy/direct create bypass hardening

The obsolete normal `POST /DocumentManagementControlledDocuments/create` returns `409 LEGACY_CREATE_RESTRICTED`.
Version upload, explorer, preview, download, share, move and favorite surfaces remain available.

## 10. Permission model

The reverse backend retains the AND gate for controlled-documents.view and master-register.view. The frontend card
is hidden without master-register.view; backend authorization remains authoritative.

## 11. Localization

Eighteen reverse-navigation/loading keys were added with exact parity across ar, en, es, fr, ru, tr and zh.

## 12. Tests/verifier results

Targeted registration/FU37C tests passed. FU24–FU29, FU36A/B, FU37A/B/C, MOD-0028-FU06 and FU36C verifiers passed.
FU36C verifier reports 19 structural checks plus seven-language resource parity.

## 13. Build results

Platform Application Debug build passed with 0 warnings/errors. Isolated Diten.Web Debug build passed with 0
errors; 14 pre-existing warnings originate from CRM, WorkCenter and ESBP files outside this scope.

## 14. Guardrails

No direct 5057/browser tenant header, TenantId input, mutation from the card, hard delete, identifier/lifecycle
automation, AuthService change, Gateway/Ocelot change or MOD-0028 provisioning change was introduced.

## 15. Remaining gaps

Authenticated browser/runtime verification remains FU36D scope. Governed language tenant-actor availability also
requires runtime proof.

## 16. Files changed

Controlled Documents MVC controller/details view/detail JS/L10n bridge/seven RESX files; additive reverse response
model/mapping; FU36C verifier; FU36/FU37/DCP/registry reconciliation; this audit.

## 17. Next recommendation

Proceed to FU36D only after explicit approval, using an authenticated tenant actor to verify Company and Corporate
registration, compatible reverse navigation, legacy state, permission denial, template creation and version upload.
