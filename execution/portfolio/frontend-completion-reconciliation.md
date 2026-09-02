# HR/TEP/PSS Frontend Completion Reconciliation

> **Authority boundary.** This reconciliation note records frontend completion evidence only. It does not authorize new runtime, backend, API, gateway, database, service, or frontend implementation work. Backend permission enforcement remains authoritative; frontend permission checks are UX-only visibility/disable guards.

## 1. Reconciliation Status

- **Status:** PASS
- **Recorded on:** 2026-08-28
- **Open blockers:** none
- **Implementation boundary:** no runtime/backend/API/gateway/frontend implementation files are changed by this governance note.

## 2. Completed Frontend Scope

### HR Frontend Completed

- Employee Projections
- Sensitive Access
- Position Assignments
- Offboarding Cases

### TEP Frontend Completed

- Shell Metadata
- Consent/Visibility Policies
- Association Memberships
- Verified Participants
- Review Board
- Trust Levels
- Candidate Profiles

### PSS / External Provider Frontend Completed

- Organization Directory
- HRIS Sources
- Time Attendance Providers
- Payroll Integration Governance
- Payroll Sources

## 3. Evidence Summary

- Frontend completion reconciliation review: PASS
- Frontend build: PASS, 0 warning, 0 error
- Gateway JSON validation: PASS
- Scoped readonly/restricted mutation scan: PASS
- SensitiveAccess POST blocker: closed
- Active CSV/export config: removed from readonly/restricted metadata slices
- Runtime/candidate/legacy literal scan: PASS
- GatewayUrl pattern: preserved
- Direct service URL hardcode: none in frontend slices; only existing `appsettings` PlatformServiceUrl environment configuration remains
- Sensitive/raw/PII-heavy data boundary: verified
- EN/TR l10n key parity: verified
- SharedResource EN/TR duplicate key scan: PASS
- JS syntax check: PASS
- NPM test: non-blocking environment gap, `vitest` dependency not installed

## 4. Readonly / Restricted Boundary

Readonly and restricted frontend slices remain limited to list/detail or masked metadata views. Real mutation calls are closed for the completed HR, TEP, PSS, and external-provider frontend slices.

SensitiveAccess no longer performs an active POST evaluation call to the decision endpoint. The UI uses readonly health and deferred-audit endpoints only, with evaluate/review/manage behavior left as disabled placeholders unless a future explicitly authorized workflow enables mutation.

## 5. Sensitive / Export Governance

Active CSV/export configuration was removed from readonly/restricted metadata screens. Column visibility controls may remain because they only change local table display and do not export data.

Sensitive and external-provider screens remain masked/admin metadata only. Raw HRIS, payroll, time/provider payloads, credential/token/secret/password values, employee profile bodies, payroll detail, payslip, bank/tax data, national ID, DOB, home address, biometric/geolocation data, and other PII-heavy details remain out of scope.

## 6. Permission Boundary

Frontend permission checks are retained only for menu/page visibility and disabled placeholder actions. Backend `[HasPermission]` and backend-side authorization remain authoritative for all API access.

## 7. Notes

- **Medium note:** Broad Platform scans can match older Platform admin screens outside this reconciliation scope. Scoped slice scans for the completed HR/TEP/PSS frontend set are PASS.
- **Low note:** `npm test` is blocked by local environment dependency state because `vitest` is not installed. This is non-blocking for the reconciliation because `dotnet build` and JS syntax checks passed.
- **Low note:** `credentials: 'include'` matches are expected cookie/header forwarding usage. No secret or credential value exposure was found in the scoped frontend slices.

## 8. Future Follow-Ups

- Mutation-enabled workflows
- Export governance
- Candidate-facing UX
- Sensitive payroll/HRIS raw data exposure
- Gateway expansion for future modules
- Full npm/vitest dependency restore

## 9. Next Delivery Standard

New HR, TEP, and PSS module development should continue with backend/API, gateway exposure, and frontend consumption planned together. Each future module should define runtime owner/key, permission namespace, gateway exposure, data-boundary rules, frontend UX-only permission behavior, and validation scans before implementation begins.

## 10. CAND-CAP-0018 End-to-End Completion

- **Module:** CAND-CAP-0018 - Industry Exit Reference Record Registry
- **Recorded on:** 2026-08-29
- **Reconciliation status:** PASS
- **Open blockers:** none
- **Implementation boundary:** this reconciliation note does not change runtime, backend, API, gateway, database, service, or frontend implementation files.

Completed scope:

- Governance pack: done.
- Backend/API metadata-only slice: completed under `services/Diten.TalentEcosystemService/**`.
- Gateway exposure: completed for `/api/tep-exit-reference-records` and `/api/tep-exit-reference-records/{everything}`.
- Frontend restricted read-only slice: completed under `Talent Ecosystem > Exit Reference Records`.

Evidence:

- Pack status: done.
- Backend/API build: PASS.
- ExitReference targeted tests: PASS, 21/21.
- Full TEP Application tests: PASS, 142/142.
- Frontend build: PASS, 0 warning, 0 error.
- Runtime literal scan: PASS, `CAND-CAP-0018|MOD-0327` returned no matches.
- Direct service URL scan: PASS.
- Frontend mutation scan: PASS.
- Sensitive/raw/PII-heavy scan: PASS.

Permission and routing boundary:

- Backend `[HasPermission]` authorization remains authoritative.
- Frontend permission checks remain UX-only visibility/disable guards.
- GatewayUrl pattern is preserved; frontend does not call the TEP service port directly.
- Gateway permission enforcement was not added.

Data and workflow boundary:

- Exit Reference Records frontend remains restricted read-only metadata.
- Mutation/evaluate/manage/archive workflows remain closed for the completed frontend slice.
- Reference and anchor identifiers are displayed only as masked/reference-only metadata.
- Raw provider payloads, credential/token/secret/password values, and PII-heavy profile/reference details remain out of scope.

Deferred and out-of-scope:

- MOD-0027 Notification.
- MOD-0263 Notification Provider / Delivery.
- MOD-0029 Controlled Documents.
- MOD-0262 External Docs Repository.

Future follow-ups:

- Mutation-enabled workflows.
- Export governance.
- Real audit/evidence/retention integration.
- Cross-company sharing hardening.
- Candidate-facing dispute/response UX.
- Reference exchange marketplace.
- Rehire recommendation.

## 11. CAND-CAP-0019 End-to-End Completion

- **Module:** CAND-CAP-0019 - Reference Exchange Marketplace
- **Recorded on:** 2026-08-30
- **Reconciliation status:** PASS
- **Open blockers:** none
- **Implementation boundary:** this reconciliation note does not change runtime, backend, API, gateway, database, service, or frontend implementation files.

Completed scope:

- Governance pack: done.
- Backend/API metadata-only slice: completed under `services/Diten.TalentEcosystemService/**`.
- Gateway exposure: completed for `/api/tep-reference-exchange` and `/api/tep-reference-exchange/{everything}`.
- Frontend restricted read-only slice: completed under `Talent Ecosystem > Reference Exchange`.

Runtime and permission boundary:

- Runtime owner/key: `tep.reference-exchange`.
- Permission namespace:
  - `tep.reference-exchange.read`
  - `tep.reference-exchange.manage`
  - `tep.reference-exchange.evaluate`
  - `tep.reference-exchange.audit.read`
- Backend `[HasPermission]` authorization remains authoritative.
- Frontend permission checks remain UX-only visibility/disable guards.
- GatewayUrl pattern is preserved; frontend does not call the TEP service port directly.
- Gateway permission enforcement was not added.

Backend/API evidence:

- Build: PASS, 0 warning, 0 error.
- ReferenceExchange targeted tests: PASS, 23/23.
- Full TEP Application tests: PASS, 165/165.
- Production in-memory repository scan: PASS.
- Runtime literal scan: PASS, `CAND-CAP-0019|MOD-0328` returned no matches.

Gateway evidence:

- Route exposure: `/api/tep-reference-exchange`.
- Route exposure: `/api/tep-reference-exchange/{everything}`.
- Gateway JSON validation: PASS.
- Gateway build: PASS.

Frontend evidence:

- Frontend build: PASS, 0 error, with 13 pre-existing Razor nullable warnings.
- Read-only DataTable, detail/offcanvas, and summary cards are completed.
- Used frontend endpoints are GET-only:
  - `/api/tep-reference-exchange`
  - `/api/tep-reference-exchange/{id}`
  - `/api/tep-reference-exchange/{id}/audit-metadata`
- Frontend mutation scan: PASS.
- Direct service URL scan: PASS.
- Sensitive/raw/PII-heavy scan: PASS.

Data and workflow boundary:

- Marketplace request/match/transaction workflow was not started.
- Rehire recommendation was not started.
- Dispute/response UX was not started.
- Notification/document integration was not started.
- Raw provider payloads, credential/token/secret/password values, and PII-heavy profile/reference details remain out of scope.

Deferred and out-of-scope:

- MOD-0027 Notification.
- MOD-0263 Notification Provider / Delivery.
- MOD-0029 Controlled Documents.
- MOD-0262 External Docs Repository.

Future follow-ups:

- Real marketplace workflow.
- Rehire recommendation.
- Dispute/response UX.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.

## 12. CAND-CAP-0020 End-to-End Completion

- **Module:** CAND-CAP-0020 - Rehire Recommendation Network
- **Recorded on:** 2026-08-31
- **Reconciliation status:** PASS
- **Open blockers:** none
- **Implementation boundary:** this reconciliation note does not change runtime, backend, API, gateway, database, service, or frontend implementation files.

Completed scope:

- Governance pack: done.
- Backend/API metadata-only slice: completed under `services/Diten.TalentEcosystemService/**`.
- Gateway exposure: completed for `/api/tep-rehire-recommendations` and `/api/tep-rehire-recommendations/{everything}`.
- Frontend restricted read-only slice: completed and re-verified under `Talent Ecosystem > Rehire Recommendations`.

Runtime and permission boundary:

- Runtime owner/key: `tep.rehire-recommendations`.
- Permission namespace:
  - `tep.rehire-recommendations.read`
  - `tep.rehire-recommendations.manage`
  - `tep.rehire-recommendations.evaluate`
  - `tep.rehire-recommendations.audit.read`
- Backend `[HasPermission]` authorization remains authoritative.
- Frontend permission checks remain UX-only visibility/disable guards.
- GatewayUrl pattern is preserved; frontend does not call the TEP service port directly.
- Gateway permission enforcement was not added.

Backend/API evidence:

- Build: PASS, 0 warning, 0 error.
- RehireRecommendation targeted tests: PASS, 26/26.
- Full TEP Application tests: PASS, 191/191.
- Production in-memory repository scan: PASS.
- Runtime literal scan: PASS, `CAND-CAP-0020|MOD-0329` returned no matches.

Gateway evidence:

- Route exposure: `/api/tep-rehire-recommendations`.
- Route exposure: `/api/tep-rehire-recommendations/{everything}`.
- Gateway JSON validation: PASS.
- Gateway build: PASS.

Frontend evidence:

- Frontend build: PASS, 0 warning, 0 error.
- Read-only DataTable, detail/offcanvas, and summary cards are completed.
- Used frontend endpoints are GET-only:
  - `/api/tep-rehire-recommendations`
  - `/api/tep-rehire-recommendations/{id}`
  - `/api/tep-rehire-recommendations/{id}/audit-metadata`
- Frontend mutation scan: PASS, no `POST|PUT|PATCH|DELETE`.
- Direct service URL scan: PASS.
- Sensitive/raw/PII-heavy scan: PASS.

Data and workflow boundary:

- Recommendation/scoring/ranking/analytics runtime was not started.
- Automated decision behavior was not started.
- Candidate-facing self-service UX was not started.
- Candidate dispute/response UX was not started.
- Notification/document integration was not started.
- Raw provider payloads, credential/token/secret/password values, and PII-heavy data exposure remain out of scope.

Deferred and out-of-scope:

- MOD-0027 Notification.
- MOD-0263 Notification Provider / Delivery.
- MOD-0029 Controlled Documents.
- MOD-0262 External Docs Repository.

Future follow-ups:

- Real recommendation workflow.
- Scoring/ranking/analytics.
- Automated decision legal approval.
- Candidate dispute/response UX.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.

## 13. CAND-CAP-0021 End-to-End Completion

- **Module:** CAND-CAP-0021 - Candidate Response & Dispute Management
- **Recorded on:** 2026-09-01
- **Reconciliation status:** PASS
- **Open blockers:** none
- **Implementation boundary:** this reconciliation note does not change runtime, backend, API, gateway, database, service, or frontend implementation files.

Completed scope:

- Governance pack: done.
- Backend/API metadata-only slice: completed under `services/Diten.TalentEcosystemService/**`.
- Gateway exposure: completed for `/api/tep-candidate-disputes` and `/api/tep-candidate-disputes/{everything}`.
- Frontend restricted read-only slice: completed under `Talent Ecosystem > Candidate Disputes`.

Runtime and permission boundary:

- Runtime owner/key: `tep.candidate-disputes`.
- Permission namespace:
  - `tep.candidate-disputes.read`
  - `tep.candidate-disputes.manage`
  - `tep.candidate-disputes.evaluate`
  - `tep.candidate-disputes.audit.read`
- Backend `[HasPermission]` authorization remains authoritative.
- Frontend permission checks remain UX-only visibility/disable guards.
- GatewayUrl pattern is preserved; frontend does not call the TEP service port directly.
- Gateway permission enforcement was not added.

Backend/API evidence:

- Build: PASS, 0 warning, 0 error.
- CandidateDispute targeted tests: PASS, 28/28.
- Full TEP Application tests: PASS, 219/219.
- Production in-memory repository scan: PASS.
- Runtime literal scan: PASS, `CAND-CAP-0021|MOD-0331` returned no matches.

Gateway evidence:

- Route exposure: `/api/tep-candidate-disputes`.
- Route exposure: `/api/tep-candidate-disputes/{everything}`.
- Gateway JSON validation: PASS.
- Gateway build: PASS.

Frontend evidence:

- Frontend build: PASS, 0 warning, 0 error.
- Read-only DataTable, detail/offcanvas, and summary cards are completed.
- Used frontend endpoints are GET-only:
  - `/api/tep-candidate-disputes`
  - `/api/tep-candidate-disputes/{id}`
  - `/api/tep-candidate-disputes/{id}/audit-metadata`
- Frontend mutation scan: PASS.
- Direct service URL scan: PASS.
- CandidateDisputes sensitive/raw marker scan: PASS.
- Broad `TalentEcosystem` sensitive scan still reports existing DOM `document`
  references and a pre-existing ReferenceExchange dependency field; these are
  not CandidateDisputes slice findings.

Data and workflow boundary:

- Candidate-facing self-service UX was not started.
- Real dispute workflow execution was not started.
- Dispute body/free-text complaint display or persistence was not introduced.
- Attachment/document payload UI was not introduced.
- Notification/document integration was not started.
- Automated decision override, recommendation recalculation, and marketplace
  transaction workflow were not started.
- Raw provider payloads, credential/token/secret/password values, and PII-heavy
  data exposure remain out of scope.

Deferred and out-of-scope:

- MOD-0027 Notification.
- MOD-0263 Notification Provider / Delivery.
- MOD-0029 Controlled Documents.
- MOD-0262 External Docs Repository.

Future follow-ups:

- Candidate-facing UX.
- Real dispute workflow execution.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.

## 14. CAND-CAP-0022 End-to-End Completion

- **Module:** CAND-CAP-0022 - Recruitment / Applicant Intake
- **Recorded on:** 2026-09-01
- **Reconciliation status:** PASS
- **Open blockers:** none
- **Implementation boundary:** this reconciliation note does not change runtime, backend, API, gateway, database, service, or frontend implementation files.

Completed scope:

- Governance pack: done.
- Backend/API metadata-only slice: completed under `services/Diten.HumanCapitalService/**`.
- Gateway exposure: completed for `/api/applicant-intake` and `/api/applicant-intake/{everything}`.
- Frontend restricted read-only slice: completed under `Human Capital > Applicant Intake`.

Runtime and permission boundary:

- Runtime owner/key: `hcm.applicant-intake`.
- Permission namespace:
  - `hcm.applicant-intake.read`
  - `hcm.applicant-intake.manage`
  - `hcm.applicant-intake.evaluate`
  - `hcm.applicant-intake.audit.read`
- Backend `[HasPermission]` authorization remains authoritative.
- Frontend permission checks remain UX-only visibility/disable guards.
- GatewayUrl pattern is preserved; frontend does not call the HCM service port directly.
- Gateway permission enforcement was not added.

Backend/API evidence:

- Build: PASS, 0 warning, 0 error.
- ApplicantIntake targeted tests: PASS, 18/18.
- Full HCM Application tests: PASS, 78/78.
- Production in-memory repository scan: PASS.
- Runtime literal scan: PASS, `CAND-CAP-0022|MOD-0300` returned no matches.

Gateway evidence:

- Route exposure: `/api/applicant-intake`.
- Route exposure: `/api/applicant-intake/{everything}`.
- Gateway JSON validation: PASS.
- Gateway build: PASS.

Frontend evidence:

- Frontend build: PASS, 0 warning, 0 error.
- JavaScript syntax check: PASS.
- Read-only DataTable, detail/offcanvas, and summary cards are completed.
- Used frontend endpoints are GET-only:
  - `/api/applicant-intake`
  - `/api/applicant-intake/{id}`
  - `/api/applicant-intake/{id}/audit-metadata`
- Frontend mutation scan: PASS.
- Direct service URL scan: PASS.
- ApplicantIntake scoped sensitive/raw scan: PASS.
- Broad direct URL scan reports an existing demo JSON `sku: 75059` match; this is not a service URL.
- Broad HumanCapital sensitive scan reports existing DOM `document` and `credentials: include` matches; these are not ApplicantIntake sensitive data exposure findings.

Data and workflow boundary:

- Candidate-facing public application UX was not started.
- Public application form/workflow was not started.
- Resume/CV body, cover letter, free-text application narrative, and attachment/document payload display were not introduced.
- Notification/document integration was not started.
- Raw provider payloads, credential/token/secret/password values, and PII-heavy data exposure remain out of scope.

Deferred and out-of-scope:

- MOD-0027 Notification.
- MOD-0263 Notification Provider / Delivery.
- MOD-0029 Controlled Documents.
- MOD-0262 External Docs Repository.

Future follow-ups:

- Candidate-facing public UX.
- Resume/document intake.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.

## 15. CAND-CAP-0023 End-to-End Completion

- **Module:** CAND-CAP-0023 - Candidate Pipeline & Interview Management
- **Recorded on:** 2026-09-01
- **Reconciliation status:** PASS
- **Open blockers:** none
- **Implementation boundary:** this reconciliation note does not change runtime, backend, API, gateway, database, service, or frontend implementation files.

Completed scope:

- Governance pack: done.
- Backend/API metadata-only slice: completed under `services/Diten.HumanCapitalService/**`.
- Gateway exposure: completed for `/api/candidate-pipeline` and `/api/candidate-pipeline/{everything}`.
- Frontend restricted read-only slice: completed under `Human Capital > Candidate Pipeline`.

Runtime and permission boundary:

- Runtime owner/key: `hcm.candidate-pipeline`.
- Permission namespace:
  - `hcm.candidate-pipeline.read`
  - `hcm.candidate-pipeline.manage`
  - `hcm.candidate-pipeline.evaluate`
  - `hcm.candidate-pipeline.audit.read`
- Backend `[HasPermission]` authorization remains authoritative.
- Frontend permission checks remain UX-only visibility/disable guards.
- GatewayUrl pattern is preserved; frontend does not call the HCM service port directly.
- Gateway permission enforcement was not added.

Backend/API evidence:

- Build: PASS, 0 warning, 0 error.
- CandidatePipeline targeted tests: PASS, 20/20.
- Full HCM Application tests: PASS, 98/98.
- Production in-memory repository scan: PASS.
- Runtime literal scan: PASS, `CAND-CAP-0023|MOD-0301` returned no matches.

Gateway evidence:

- Route exposure: `/api/candidate-pipeline`.
- Route exposure: `/api/candidate-pipeline/{everything}`.
- Gateway JSON validation: PASS.
- Gateway build: PASS.

Frontend evidence:

- Frontend build: PASS, 0 warning, 0 error.
- Read-only DataTable, detail/offcanvas, and summary cards are completed.
- Used frontend endpoints are GET-only:
  - `/api/candidate-pipeline`
  - `/api/candidate-pipeline/{id}`
  - `/api/candidate-pipeline/{id}/audit-metadata`
- Frontend mutation scan: PASS.
- Direct service URL scan: PASS.
- CandidatePipeline scoped sensitive/raw marker scan: PASS.
- Broad direct URL scan reports an existing demo JSON `sku: 75059` match; this is not a service URL.
- Broad HumanCapital sensitive scan reports existing DOM `document` references and
  the metadata-only `DocumentDependencyState` field; these are not
  CandidatePipeline payload or integration UI findings.

Data and workflow boundary:

- Candidate-facing scheduling/application UX was not started.
- Interview notes/free-text evaluation display or persistence was not introduced.
- Resume/CV body display was not introduced.
- Attachment/document payload UI was not introduced.
- Notification/document integration was not started.
- Scoring/ranking/automated decision UI and behavior were not started.
- Raw provider payloads, credential/token/secret/password values, and PII-heavy
  data exposure remain out of scope.

Deferred and out-of-scope:

- MOD-0027 Notification.
- MOD-0263 Notification Provider / Delivery.
- MOD-0029 Controlled Documents.
- MOD-0262 External Docs Repository.

Future follow-ups:

- Candidate-facing scheduling UX.
- Real interview workflow.
- Interview notes/evaluation forms.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
