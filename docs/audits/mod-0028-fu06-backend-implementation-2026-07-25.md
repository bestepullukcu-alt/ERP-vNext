# MOD-0028-FU06 Corporate Collection Instance Foundation — Backend Implementation Audit

## Summary

The approved Corporate Collection Instance backend foundation is implemented. The delivery is tenant-scoped,
company-independent, idempotent, scope-aware, and offline-green. MOD-0029, frontend, AuthService, Gateway/Ocelot,
and ControlledDocument runtime contracts were not changed.

## Delivered Contract

- `CollectionScopeType.Corporate`;
- additive `ScopeOwnerId` and `CorporateOwnerId` on CollectionInstance;
- Company compatibility through the existing non-nullable CompanyId contract;
- Corporate CompanyId default omitted from Mongo through `BsonIgnoreIfDefault`;
- Corporate provisioning operation with sanitized failure state and retry;
- real CollectionInstance folder nodes materialized from an instantiable immutable baseline;
- tenant/corporate/owner/folder partition descriptors;
- deny-by-default user/role policy evaluator with no company-membership fallback;
- Mongo uniqueness for active Corporate owner/baseline/nodes and idempotency keys;
- provision, get, list, operation-get, and retry endpoints.

## Compatibility

Existing Company creation now additionally records `ScopeOwnerId = CompanyId`. Its CompanyId, scope enum value,
queries, partition literal, import, baseline, and publish behavior remain unchanged.

## Security

TenantId is supplied by server tenant context. Repositories inherit tenant and soft-delete execution filters.
Corporate request DTOs expose neither TenantId nor CompanyId. Corporate reads require explicit access-matrix
user/role grants or an approved administrative principal. Company membership is not a Corporate grant.

## Verification

- Platform API build to isolated output: PASS.
- Targeted CorporateCollection tests: 4/4 PASS.
- Full Diten.Platform Application suite: 1911/1911 PASS.
- FU06 PowerShell verifier: PASS.
- Runtime authenticated/Mongo smoke: not run in this backend implementation pass.

## Status

Module pack: `review`. Runtime implementation: `implemented-offline-green`.

## Next Step

Perform authenticated Mongo-backed runtime reconciliation for FU06. Only after that evidence is accepted may
MOD-0029-FU37 be reviewed for promotion from `draft`.
