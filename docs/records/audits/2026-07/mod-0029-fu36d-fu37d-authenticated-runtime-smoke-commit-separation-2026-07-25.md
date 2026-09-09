# MOD-0029-FU36D / FU37D Authenticated Runtime Smoke & Commit Separation Audit

Date: 2026-07-25  
Final verdict: **BLOCKED**  
Commit/push: **No**

## 1. Summary

Fleet health and tenant authentication are green, but authenticated registration is blocked before submit:
governed Language returns 403, Retention Class returns 500, and the Corporate instance list is empty. No fake
Company/Corporate success is claimed and no runtime bug was fixed in this audit.

## 2. Runtime environment

Fleet health recorded:

- Gateway 5000: Healthy.
- Web 5001: reachable after restarting only the Web process for this smoke.
- AuthService 5056: Healthy.
- Platform 5057: Healthy.
- MongoDB 27017: listening; Platform readiness reports `mongodb=Healthy`.
- Platform readiness also reports MassTransit, RabbitMQ and Hangfire storage Healthy.
- Gateway registration route returns 401 without authentication, proving it is routed and not 404.

The in-app browser could not be initialized because the machine Node runtime is v16.20.2 while the browser runtime
requires at least v22.22.0. Browser-only visual evidence is therefore an explicit gap.

## 3. Authenticated setup

Authenticated status recorded: tenant login succeeded for tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93`,
without MFA or forced password change. Credentials and tokens were neither printed nor persisted. The unified
create page returned 200. Legal Entities returned 200 with five entries.

## 4. Company registration smoke

**BLOCKED before mutation.** The Company form cannot reach a governed-ready state because Language returns 403 and
Retention returns 500. Per the frontend fail-closed contract, submission must stay disabled. No Company document,
version, register entry or operation was created, so Company partition/runtime completion is not claimed.

## 5. Corporate registration smoke

**BLOCKED before mutation.** The same lookup failures apply, and authenticated Corporate Collection Instances
returned an empty array. No provisioning or synthetic CompanyId was attempted. Corporate explicit-grant,
partition and Completed-state evidence remain open.

## 6. Governed lookup smoke

Language lookup smoke recorded: authenticated `GET governed-languages` → 403.  
Retention lookup smoke recorded: authenticated `GET DOC_RETENTION_CLASS` → 500.  
There is no free-text or fake fallback; the form correctly fails closed. Targeted follow-up is required for the
language read permission/contract and Retention reference-data runtime failure.

## 7. Operation/idempotency smoke

Operation Completed-only success is statically/test verified, but runtime create was not permitted by the lookup
gate. Same-key replay, changed-scope 409 and retry-by-operationId are therefore **not runtime-smoked**. FU36A/FU37A
tests and verifiers continue to cover duplicate prevention, immutable scope fingerprint and retry behavior.

## 8. Manual-link/scope guardrail smoke

Manual link scope mismatch runtime smoke was not possible because no governed Company/Corporate registration
records were created and Controlled Documents list returned 500 for this actor. FU37C tests/verifier remain green:
reason required, scope/owner/collection/folder mismatch blocked, Corporate explicit access required and downstream
readiness fail-closed.

## 9. Reverse navigation smoke

Authenticated reverse navigation could not be exercised against a newly created compatible relation. Browser
automation was unavailable and Controlled Documents list returned 500. FU36C verifier confirms same-origin proxy,
404 legacy state, unverified warning state, compatible-only open action and no card mutation; runtime evidence
remains open.

## 10. Add Document/template/version regression

- Normal Controlled Documents Create GET: 302 to
  `/DocumentManagementMasterRegister/CreateControlledDocument`.
- Template create GET: 200 at `?kind=template`.
- Direct legacy create POST with valid antiforgery: 409 `LEGACY_CREATE_RESTRICTED`.
- Version upload route and explorer/detail/version surfaces are structurally preserved.
- Actual version upload was not mutated during this blocked smoke.

## 11. Build/test/verifier matrix

- Platform API isolated Debug build: PASS, 0 errors; 10 pre-existing warnings.
- Web isolated Debug build: PASS, 0 errors; 14 pre-existing CRM/WorkCenter/ESBP warnings.
- Platform Application test command: PASS exit code 0.
- PASS: FU06 foundation; FU36A/B/C; FU37A/B/C; FU24–FU29.
- FAIL: `verify-mod0028-fu06-runtime-smoke-reconciliation.ps1`.
- FAIL: `verify-mod0028-fu06-mongo-index-compatibility-fix.ps1`.
- Both failures are stale governance assertions requiring FU37 to remain `draft`; FU37 is now legitimately
  `ready-for-dev`. They are not Mongo health failures, but the requested all-verifier matrix is not green.
- Scoped diff-check: PASS for the FU36D audit/verifier/governance files.
- Repo-global diff check reports the pre-existing `watch-diten.ps1:6` trailing whitespace separately.

## 12. Pack/registry reconciliation

FU36 runtime status: `implemented-with-runtime-gaps`.  
FU37 runtime status: `implemented-with-runtime-gaps`.  
DCP-004 Phase 5/FU36C remains implemented; Phase 6 runtime smoke is partial/blocked. No final completion is claimed.

## 13. Commit separation audit

The working tree is highly mixed. A single commit is unsafe. Do not use `git add .`. Registry, DCP and several
shared runtime files contain mixed changes and require `git add -p`. No commit should be prepared until the
governed lookup blockers and stale verifier disposition are handled in separate targeted tasks.

## 14. Files by commit group

Group A — MOD-0028-FU06:

- Corporate CollectionInstance entity/enums/repository/index/access/storage files.
- FU06 tests, three FU06 verifiers and FU06 audits.
- FU06 pack and only its DCP-004 hunks.

Group B — MOD-0029-FU36:

- ControlledDocumentRegistration operation/orchestration/API/repository files.
- Master Register unified-create controller/views/JS/RESX.
- Controlled Documents redirect/reverse-card/bypass files.
- FU36 tests, verifiers, audits and FU36 pack.

Group C — MOD-0029-FU37:

- Typed DocumentScope/ownership/partition amendments.
- Conditional Company/Corporate form and governed lookup UI.
- Manual-link compatibility/downstream guards and FU37 tests/verifiers/audits.
- FU37 pack.

Group D — shared governance:

- DCP-004, `module-implementation-status.md` and any module registry/capability index hunks.
- These are mixed files: stage only exact FU06/FU36/FU37 hunks with `git add -p`.

Group E — unrelated/do not include:

- Commercial Suite/CRM/Territory files and audits.
- HCM/MDM/ESBP unrelated streams.
- unrelated AuthService seed changes and Gateway/Ocelot changes.
- `.claude/settings.local.json`, logs, `.tmp`, bin/obj, watch scripts and local fleet artifacts.

## 15. Unrelated files excluded

Unrelated CRM/HCM files identified; AuthService seed, Gateway/Ocelot, local settings, logs, `.tmp`, generated
outputs and `watch-diten.ps1` are excluded from every FU36/FU37 commit recommendation.

## 16. Remaining gaps

1. Fix/authorize governed Language read contract for the tenant actor.
2. Diagnose Retention Class lookup 500.
3. Provide an existing Corporate instance plus explicit CreateDocument grant.
4. Diagnose authenticated Controlled Documents list 500.
5. Upgrade the browser-control Node runtime and repeat visual smoke.
6. Repeat Company/Corporate Completed, idempotency/retry, manual-link and reverse-navigation runtime smoke.
7. Reconcile the two stale FU06 verifier assertions in a separately authorized verifier-maintenance task.

## 17. Guardrails

No business logic, AuthService seed, Gateway/Ocelot, MOD-0028 provisioning, entity nullability, dummy CompanyId,
hard delete or runtime bug fix was introduced. No token/password was written to an audit or log. No commit/push was
performed. Scope mismatch was not bypassed and no non-Completed operation was reported as success.

## 18. Final recommendation

Verdict remains BLOCKED. Run a small targeted runtime-readiness task for governed Language/Retention and
Controlled Documents list access, provision or identify a valid Corporate test fixture/grant, reconcile the stale
FU06 verifiers, upgrade browser Node, then rerun FU36D/FU37D from the beginning. Commit separation should happen
only after that rerun; use explicit path lists and `git add -p`, never `git add .`.
