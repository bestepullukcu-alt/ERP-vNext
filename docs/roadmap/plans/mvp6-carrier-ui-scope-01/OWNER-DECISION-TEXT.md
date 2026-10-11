# Copyable UI scope decision

I approve the proposed MOD-0184 Carrier tenant UI design scope for pack-delta preparation and a later versioned dispatch only:

- the UI surface is limited to list, create, and status change over the published SHIPMENT-BUNDLE Carrier operations;
- the create form has exactly four user fields (`carrierCode`, `displayName`, `supportedModes`, `externalReference`) and uses GoldenReferenceSlim, tenant shell, DataTables v2, UAS-001, Premium SweetAlert2, and seven tenant languages;
- no detail-by-id, edit, delete, bulk, lookup, server paging/search/sort, Supplier ownership, or new backend behavior is added;
- frontend service egress is only through Gateway 5000; the exact gateway route, shared permission/catalog registration, and tenant navigation entry remain separately owned integration changes;
- the three permissions remain independent: `supplychain.carriers.read`, `supplychain.carriers.create`, and `supplychain.carriers.status.change`;
- the Carrier annex controls validation, tenant/LE isolation, errors, correlation, lifecycle, replay, changed-payload conflict, and same-key recovery without client-side tightening;
- the accepted bounded backend E4 decision is preserved and is not converted into UI, E5/G5, gateway, or rollout acceptance.

This approval permits applying the exact pack target SHA256 `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f` and completing the UI Phase 1.5 review. The pack target preserves the existing backend-scoped `ready-for-dev` state; that state does not authorize UI. This decision does not itself authorize UI/gateway/shared/runtime implementation. UI DEV remains HELD until Lane A's successor binds an immutable source baseline, the exact shared integration diffs and single owners are approved, UI Phase 1.5 closes, and a versioned UI dispatch is explicitly released.
