# Consumed Supplier seam matrix

## Contract inventory

| Artifact | SHA-256 | Current authority |
|---|---|---|
| `docs/analysis/contracts/supplier.openapi.yaml` | `87a297edfb8eabf9ecc8beff7870955f46b38413a1490a05a36e3f255d77bb00` | `SUPPLIER` 1.0.0, FROZEN, MOD-0140 owner, front-loaded for 0147/0148 |
| `docs/analysis/contracts/supplier-performance.openapi.yaml` | `9100d106527947a0a842bc5c4b580fbb2c3b6a288949798b8416a08d352e0088` | `SUPPLIER-PERFORMANCE` 1.0.0, FROZEN, joint 0147/0148 wire surface |

The draft statements saying the central Supplier contract is absent are stale. `SUPPLIER` v1 exists and is structurally valid OpenAPI 3.1. It is an identity-only consumed slice, not a live producer or actor-binding implementation.

## Operation and field coverage

| Need | Existing authority | Coverage | Exact gap / consequence |
|---|---|---|---|
| Supplier lookup | `GET /api/suppliers/{supplierId}` → `supplierId`, `name`, `status`, optional `country`, `taxId`, `contacts` | SATISFIED for tenant-level identity lookup | Consumer must not copy name/profile/contact/tax data into a local Supplier master |
| Bulk existence/status validation | `POST /api/suppliers/validate` → `supplierId`, `known`, `status` | PARTIAL | Response/result fields are not required; unknown example uses `status: null` against a non-null enum; completeness/order and malformed response handling are unspecified |
| Supplier list | `GET /api/suppliers` with optional status/cursor | PRESENT, not required by the bounded 0147/0148 command seams | Must not be used to infer actor binding or LegalEntity eligibility |
| Tenant scope | SUPPLIER description says tenant-scoped | PARTIAL | No exact tenant context/header/error behavior is declared in the operation schemas |
| LegalEntity eligibility | No SUPPLIER operation or field | MISSING | MOD-0147/0148 are Tenant+LE scoped; cross-LE eligibility cannot be inferred from tenant-level identity |
| Supplier status eligibility | Status enum `Active`, `OnHold`, `Blocked`, `Inactive` | PARTIAL | No authority says which status permits evaluation, risk registration, portal access or submission |
| Correlation and dependency errors | SUPPLIER Error has free string correlation; sample `req-1`; no required correlation header or 503 contract | MISSING for consumer acceptance | Cannot map timeout/malformed response/unknown supplier to exact 0147/0148 errors without a decision |
| Authenticated actor→supplier binding | No operation, claim contract or authority record | MISSING | SUPPLIER identity does not prove portal identity or own-record authorization |
| Actor tenant/LE relation, cardinality and revocation | No published seam | MISSING | One/many supplier choice, revocation timing and cross-LE denial are undefined |
| Portal access status | `PortalStatus.accessStatus = ACTIVE/SUSPENDED/PENDING` | PARTIAL | Mapping from base SupplierStatus is undefined |
| Own-record selection | Portal contract requires server-derived identity and 404 isolation | REQUIREMENT ONLY | No authoritative mapping producer exists; persistence predicate must be Tenant+LE+mapped Supplier once approved |
| Metric registry | Only `x-consumes: METRIC-REGISTRY` and `metricCode` text | MISSING | No operation/version/scope/UoM contract or stale/unavailable behavior exists |
| Risk register | Only `x-consumes: RISK-REGISTER` text | MISSING/AMBIGUOUS | MOD-0147 also owns SupplierRisk; it is unknown whether the external seam is taxonomy validation or another register |

## MOD-0147 owned surface and gaps

The existing contract contains exactly nine 0147 operations:

1. `listSupplierEvaluations`
2. `createSupplierEvaluation`
3. `getSupplierEvaluation`
4. `submitSupplierEvaluation`
5. `listSupplierScorecards`
6. `getSupplierScorecard`
7. `listSupplierRisks`
8. `registerSupplierRisk`
9. `changeSupplierRiskStatus`

The schemas freeze opaque `supplierId`, periods, metric inputs/results, scorecard projection, risk enums, correlation, versions and declared HTTP responses. They do not freeze:

- metric registry lookup identity/version/scope or UoM;
- measured value→score calculation;
- weight normalization/sum, decimal scale or rounding;
- overall score and RiskLevel thresholds;
- exact risk transition matrix;
- external Risk Register role;
- sourceId existence/scope validation and dependency failure behavior.

The pack prose cannot supply these missing contract decisions as implementation defaults.

## MOD-0148 owned surface and gaps

The existing contract contains exactly five bounded portal operations:

1. `getSupplierPortalStatus`
2. `listOwnPortalSubmissions`
3. `createOwnPortalSubmission`
4. `getOwnPortalSubmission`
5. `submitOwnPortalSubmission`

Create payload excludes supplier/tenant/LegalEntity fields and has `additionalProperties: false`. This correctly prevents body override. The contract defines unresolved identity as 403, inaccessible own-record as 404 and base dependency failure as 503, but there is no published mapping producer or precedence contract behind those results.

The draft pack's internal review command is not present in the frozen operation set and remains a later decision. Its proposed normal index includes LegalEntity, while its replay tuple omits LegalEntity. The common owner must decide the exact replay scope; this plan does not silently repair it.

## Acceptance scenarios for the common seam

| Scenario | Required observable result |
|---|---|
| Known eligible supplier in same tenant and LE | Exact owner-approved lookup/mapping succeeds; module may continue |
| Unknown supplier | Exact 404/error mapping; zero module writes |
| Known but ineligible status | Owner-selected exact denial; zero writes |
| Foreign tenant, foreign LE or foreign mapped supplier | 404/no existence leakage; zero writes |
| Supplier base timeout or malformed/incomplete result | Fail closed with exact 503/error precedence; zero writes |
| Unmapped/revoked supplier actor | 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED`; zero access and writes |
| Body/query attempts supplier, tenant or LE override | 400; trusted context unchanged |
| Portal own-record read | Predicate includes TenantId, LegalEntityId, mapped SupplierId and non-deleted state |
| Valid/invalid/stale metric code | Exact registry/version/scope oracle; invalid path has no evaluation/audit/outbox write |
| Risk source/transition | Exact source scope and transition matrix; invalid path has no risk/audit/outbox write |
| Dependency fixture | Explicitly test-only; does not prove live MOD-0140 or auth producer uptake |
