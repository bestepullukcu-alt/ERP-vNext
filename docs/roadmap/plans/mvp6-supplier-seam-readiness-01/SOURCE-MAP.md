# Exact source map

| Finding | Source pointer |
|---|---|
| MVP-6 places 0147/0148 in its delivery-owned set and SUPPLIER in consumed MVP-2 seams | `docs/analysis/workpackages/WP-MVP6-logistics.md:5-12` |
| Supply Chain domain reservation omits 0147/0148 | `execution/domains/supply-chain-execution/domain-config.md:15-34` |
| DCP-009 canonical/member boundary omits 0147/0148 | `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md:15-18`, `:51-57` |
| DCP-009 only front-loads the Supplier seam; it does not transfer module ownership | same DCP, `:95-96` |
| MOD-0147 marks SCE placement as an assumption and ownership as OPEN | `MOD-0147-supplier-performance-risk.md:20-28`, `:114-122`, `:207-228` |
| MOD-0148 marks SCE placement as an assumption, actor mapping central and ownership OPEN | `MOD-0148-supplier-portal.md:20-28`, `:102-121`, `:201-225` |
| Supplier identity contract is FROZEN, MOD-0140-owned and front-loaded for both consumers | `docs/analysis/contracts/supplier.openapi.yaml:1-18` |
| Supplier operations and fields | same contract, `:21-124` |
| Validation example/schema nullability mismatch | same contract, `:81-101` |
| Joint performance contract declares Tenant+LE and actor-derived portal identity as requirements | `docs/analysis/contracts/supplier-performance.openapi.yaml:1-24` |
| Exact five portal operations and server-derived identity behavior | same contract, `:250-355` |
| Metric/Risk dependencies are declarations without repo-local contracts | same contract, `:16-20`; repository contract inventory search |

`MOD-0147-supplier-performance-risk.md` and `MOD-0148-supplier-portal.md` pointers are relative to `execution/domains/supply-chain-execution/module-packs/`.
