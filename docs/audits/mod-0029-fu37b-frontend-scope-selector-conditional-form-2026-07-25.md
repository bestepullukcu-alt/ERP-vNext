# MOD-0029-FU37B Frontend Scope Selector and Conditional Form — 2026-07-25

## 1. Summary

**PASS_WITH_GAPS.** FU37B conditional frontend is implemented and static/build evidence is green. The only
frontend-foundation gap is authenticated runtime proof that the governed language lookup is available to the
tenant actor. The form fails closed when either governed lookup is unavailable.

## 2. Inputs Reviewed

The approved FU37 pack, FU37 approval/FU37A audit, FU36A/FU36B audits, MOD-0028-FU06 implementation evidence,
Master Register MVC controller, Razor form, localization bridge, registration JavaScript, Corporate Collection
Instance endpoints/models and governed lookup sources were reviewed.

## 3. Frontend Scope Delivered

The FU36B unified create page is now scope-aware without changing backend business logic. It keeps multipart
FormData, antiforgery, same-origin MVC proxy transport, permission snapshots, retry and Completed-only success.

## 4. DocumentScope Selector

The first form card contains `Company | Corporate`; Company is the default. Changing scope clears owner,
CollectionInstance and folder values, disables the inactive scope fields and updates localized guidance.

## 5. Company Conditional Flow

Company mode exposes Legal Entity, Owner Company, Company Collection Instance and Company Folder. Its payload
contains `CompanyId` and `OwnerCompanyId` and omits `CorporateOwnerId`. Existing FU36B Company behavior remains the
default.

## 6. Corporate Conditional Flow

Corporate mode hides/disables Company fields and exposes Corporate Owner, existing Corporate Collection Instance
and Corporate Folder. It sends `CorporateOwnerId` and omits Company ownership fields. No provisioning endpoint or
action is exposed. An empty instance set produces a localized blocking message.

## 7. CollectionInstance / Folder Selectors

Company nodes are loaded from the existing company-safe proxy. Corporate nodes are loaded through a new same-origin
read-only proxy to the FU06 list endpoint. Roots populate the structure selector and their path branch populates
the folder selector. Under the current MOD-0028 node-per-folder runtime contract, payload
`CollectionInstanceId` and `FolderId` both identify the selected folder node; the structure selector is the
client-side filtering parent.

## 8. Governed Language / Retention Select2

Language uses the existing governed `/api/lookups/languages` source through an MVC proxy. Retention uses the
published `qms-document-retention` reference-data set. Both are required single Select2 controls; there is no free
text or fake fallback. Missing/empty lookups disable submission.

## 9. Payload Safety

Payload always includes `DocumentScope`, governed stable language/retention values, selected target and existing
FU36 metadata. Opposite-scope owner fields are created only inside the active branch. No TenantId, tenant header,
UID/code, effective/lifecycle/approval/release/signature field or Base64 file content is generated.

## 10. Operation Response Handling

Only normalized `COMPLETED` is success and can redirect to Master Register Details. All intermediate states remain
warnings/incomplete. Retry remains permission-gated. HTTP 409, 403 and 404 receive localized conflict, access and
non-leaking mismatch messages; a new idempotency key is not silently generated after conflict.

## 11. Permission / Visibility

The page retains the existing registration create and reconcile permission snapshot checks. Corporate visibility
does not create a new permission key; the backend FU06 explicit-grant evaluator remains authoritative.

## 12. Localization

Scope labels/help, conditional selectors, missing-instance state, governed lookup failure and 409/403/404 messages
were added with exact key parity across `en`, `fr`, `es`, `zh`, `ar`, `ru` and `tr`. All seven RESX files parse as
valid XML without duplicate keys.

## 13. Tests / Verifier Results

- FU37B verifier: PASS.
- FU36B regression verifier: PASS.
- FU24: 65/65 PASS.
- FU25: 113/113 PASS.
- FU26: 130/130 PASS.
- FU27: 110/110 PASS.
- FU28: 155/155 PASS.
- FU28A: 120/120 PASS.
- FU29: 153/153 PASS.
- FU36A, FU37A and MOD-0028-FU06 backend regression verifiers: PASS.
- JavaScript syntax and seven RESX XML parses: PASS.

## 14. Build Results

The normal Web build reached only the output-copy stage and was blocked by the already-running Diten.Web process
locking `Diten.Web.exe`. The authorized isolated-output build passed with 0 errors and 14 unrelated existing
warnings.

## 15. Guardrails

No Platform backend business logic, ControlledDocument entity, MOD-0028 runtime/provisioning, AuthService or
Gateway/Ocelot file was changed for FU37B. No direct service port, browser tenant header/id, Base64 conversion,
provision action, hard delete, commit or push was introduced.

## 16. Remaining Gaps

Authenticated runtime must confirm `/api/lookups/languages` is available to the tenant actor through the deployed
Gateway policy. Manual-link scope/owner enforcement, reverse navigation and full registration/runtime smoke remain
later approved work. FU36C/FU36D remain paused.

## 17. Files Changed

FU37B changes are limited to the Master Register MVC controller, unified create Razor/form localization bridge,
registration JavaScript, Web reference-data configuration, seven MasterRegister RESX files, FU37B verifier, this
audit and FU37/DCP/implementation-status reconciliation.

## 18. Next Recommendation

Run authenticated browser smoke for both scopes with real governed lookup and Corporate access data. If the tenant
actor cannot consume the language lookup, authorize a dedicated governed language read contract before runtime
acceptance; do not reintroduce free text or a hardcoded language fallback.
