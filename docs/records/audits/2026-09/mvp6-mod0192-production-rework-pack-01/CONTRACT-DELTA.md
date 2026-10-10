# Conditional duplicate-name contract-owner delta — NOT APPROVED

Baseline: published `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` SHA256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.

Exact minimum *semantic* amendment recommendation:

- `operationId: createCapacityScenario`, POST `/capacity-plans/{capacityPlanId}/scenarios`, its existing 409 response `#/components/responses/CapacityPlanStateConflict`: add `CAPACITY_SCENARIO_NAME_CONFLICT` to `x-error-codes`, one 409 Error-envelope example, and update response description to cover exact duplicate scenario name. Keep existing codes, headers, body schema and status.
- Annex `createCapacityScenario` row: document duplicate exact parsed name under same tenant/LE/plan with simple collation; deterministic check and unique-index race converge to `409 CAPACITY_SCENARIO_NAME_CONFLICT`. Preserve plan-state and idempotency precedence; no trim or max-length rule.
- Versioned successor metadata and annex path must be chosen and validated with exact hash, OpenAPI/ref/example and consumer tests before publication. A 2.1.0 candidate is reasonable for an added response code, but strict-code consumers and same-route wire-v1 cutover make minor compatibility nonautomatic. No version or consumer consent is approved here.

Alternative: return existing `CAPACITY_PLAN_STATE_CONFLICT` for duplicate names. This avoids a new code but falsely attributes a Draft plan's name collision to lifecycle. Removing uniqueness changes an approved business rule. Recommend the explicit name-conflict amendment, subject to contract-owner choice.
