# Evidence — Q70 static re-checks (2026-09-26, VM /tmp only, read-only)

## 1. DataTable generic gate on the exact accepted A12 composition

Composition (same order as A12 VER-02 `raw/02-compose-source.txt`, tree manifest `e4528e14…`):
1. `git archive HEAD` (HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`)
2. `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` (`7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d`), wrapper `mvp6-shipment-a12-rework-src/` stripped
3. `docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-01/final-source.tar.gz` (`f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`)
AppleDouble `._*` files removed.

Commands (verifier `.antigravity/scripts/verify_datatable_page.py` sha256 `00148e13a259623ffa2df2ba1290cf327af4b8065c9b5c14c09a79e06ba2748c`, run with `python3 -B`):
- `verify_datatable_page.py --area SupplyChain --module Shipments --reference compact --api-profile direct-gateway <tree>` → **49 PASS / 35 FAIL** → `verifier-a12-direct-gateway.txt`
- `verify_datatable_page.py --area SupplyChain --module Shipments --reference compact --api-profile proxy <tree>` → **51 PASS / 34 FAIL** → `verifier-a12-proxy.txt`

The only FAIL difference is `direct-gateway profile uses window.API service base` (FAIL 28). The proxy run instead PASSes
"index.js uses same-origin frontend proxy endpoint (/SupplyChain/Shipments/api)" and "proxy profile avoids direct
browser Gateway calls".

Source facts in that composition: `Index.cshtml` `7a5f467f…` (lines 20 and 24 use absolute `~/Views/SupplyChain/Shipments/_Filter.cshtml` / `_IndexL10n.cshtml`); `index.js` `97f8f2a5…` line 4 `const endpoint = '/SupplyChain/Shipments/api';`; `SupplyChainShipmentsController.cs` lines 34–35 and 63 target the configured `GatewayUrl`.

Rule files: `.antigravity/rules/frontend-datatable-template.md` `02dd882f…`; `.antigravity/workflows/quality-gate-datatable.md` `2c4801e8…`; `.antigravity/workflows/add-module.md` `baf455c5…`.

## 2. Pack alignment deltas (Q27/Q28)

- The current shared packs are MOD-0190 `637690f3…6877` and MOD-0192 `edd550b8…69f7`, equal to the preimages. Neither
  shows a working-tree modification.
- `docs/roadmap/plans/mvp6-pack-alignment-01/SHA256SUMS` (`95bed499…`) gives 9/9 OK from the repo root.
- Copies of both packs went to `/tmp/q70/pa/<same relative path>`. With `GIT_CEILING_DIRECTORIES=/tmp`:
  - `git apply --check mod-0190-sop/proposed-pack.patch` (`116b7c47…`) → OK;
  - `git apply --check mod-0192-capacity/proposed-pack.patch` (`7d9b587e…`) → OK;
  - both together → OK.
- `git apply` of both gives MOD-0190 `8403d8f46059c99ef34d3fe08b3cacdbbbefb321f30f921b620eceda9180ea40` and MOD-0192
  `de81a0e289fff2732ca333357654fcd0ea7254fd9496391d2b06933d1891946c`, both equal to the targets.
- SANDOP-CAPACITY canonical: `docs/analysis/contracts/sandop-capacity.openapi.yaml` `5213b535…adab` (3.0.0).

All `/tmp` copies were removed at the end.
