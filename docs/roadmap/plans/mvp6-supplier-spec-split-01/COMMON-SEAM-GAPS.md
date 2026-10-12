# Single-owner Supplier seam GAP list

**Coordinator:** Central Control Tower Supplier Seam Owner. This list is the only common seam output for the split. It is a specification handoff, not a contract candidate.

## What existing SUPPLIER v1 can support

FROZEN `docs/analysis/contracts/supplier.openapi.yaml` SHA-256 `87a297edfb8eabf9ecc8beff7870955f46b38413a1490a05a36e3f255d77bb00` provides:

- `listSuppliers`, `getSupplier` and `validateSuppliers` operations;
- opaque `supplierId`, supplier `name` and `status`;
- the `Active`, `OnHold`, `Blocked` and `Inactive` status vocabulary;
- tenant-scoped request context and basic known/unknown lookup intent.

That is sufficient for a future consumer adapter to request base identity/status. It does not prove a live producer, transport deployment or compatibility of the selected new policy behaviors.

## Open common seams

| GAP | Missing authority/capability | Blocks | Required owner/disposition |
|---|---|---|---|
| SG-01 | Durable SCE/service placement in domain config, DCP-009 and registry | Pack promotion for both modules | Enterprise/domain owner with CT/SCE concurrence; separate governance mutation |
| SG-02 | Live MOD-0140 producer evidence for exact `getSupplier` identity/status semantics | Real dependency acceptance | MOD-0140 owner; fixture cannot close it |
| SG-03 | Tenant+LegalEntity Supplier eligibility authority | MOD-0147 create/risk and all MOD-0148 portal eligibility | MOD-0140 eligibility owner plus security scope owner; endpoint/carrier remains undecided |
| SG-04 | Strict OAS required/null/error/correlation repair, including declared 403/409/503 surfaces | Exact wire implementation | One CT contract writer with MOD-0140, both consumers, security and strict-consumer concurrence; version undecided |
| SG-05 | Validated issuer/subject + trusted Tenant/LE binding authority, cardinality and current revision | MOD-0148 identity | Platform auth/security owner; no email/displayName/userId inference |
| SG-06 | Revocation versus mutation-commit fence and current membership/permission check | MOD-0148 mutation/replay | Platform auth/security owner with MOD-0148; TTL cache is insufficient |
| SG-07 | Metric Registry immutable revision, scope, percentage UoM, direction, weights, validity and withdrawal/authenticity queries | MOD-0147 evaluation/submit | Metric Registry owner; producer pointer/carrier undecided |
| SG-08 | Risk taxonomy revision authority | MOD-0147 risk registration | Risk Register owner; no registry clone |
| SG-09 | Scoped source resolution for scorecard and submitted portal records | MOD-0147 SupplierRisk source validation | MOD-0147/MOD-0148 owners plus Risk owner; MANUAL/EXTERNAL_SIGNAL remain rejected in first slice |
| SG-10 | Receipt/error/correlation wire refinement and compatibility disposition | MOD-0148 replay | MOD-0148 owner, security and single contract writer |
| SG-11 | Test-only `supplier-exact-policy-fixture/1` implementation | Future simulated acceptance only | CT fixture coordinator after relevant concurrence; never production proof |

## Single-writer rule

Any successor of SUPPLIER or the shared portions of SUPPLIER-PERFORMANCE must be prepared once by the Central Control Tower Supplier Seam Owner after the relevant owners supply exact decisions. MOD-0147 and MOD-0148 may contribute requirements and consumer acceptance, but neither may independently publish or design a conflicting shared contract/producer seam.

No endpoint name, claim name, mapping-store location, transport, contract version or rollout mechanism is selected by this package.
