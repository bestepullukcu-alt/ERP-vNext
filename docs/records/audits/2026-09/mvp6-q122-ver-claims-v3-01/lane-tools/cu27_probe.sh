#!/usr/bin/env bash
# Q122 CU-27 family-routing probe (unauthenticated; no token, no secret). Gateway 5900 = kit slot 9; Web 5901.
O=$1; printf 'target\tmethod\turl\tstatus\n' > "$O"
p() { printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$(curl -s -o /dev/null -w '%{http_code}' -X "$2" "$3")" >> "$O"; }
p gateway GET  http://127.0.0.1:5900/api/shipment-bundle/claims
p gateway POST http://127.0.0.1:5900/api/shipment-bundle/claims
p gateway GET  http://127.0.0.1:5900/api/shipment-bundle/claimsXYZ
p gateway POST http://127.0.0.1:5900/api/shipment-bundle/claimsXYZ
p gateway GET  http://127.0.0.1:5900/api/shipment-bundle/claimsXYZ/00000000-0000-4000-8000-000000000001/transition
p gateway POST http://127.0.0.1:5900/api/shipment-bundle/claims/00000000-0000-4000-8000-000000000001/transition
p gateway POST http://127.0.0.1:5900/api/shipment-bundle/claimsXYZ/00000000-0000-4000-8000-000000000001/transition
p gateway OPTIONS http://127.0.0.1:5900/api/shipment-bundle/claims
p web GET http://127.0.0.1:5901/SupplyChain/ClaimsXYZ
p web GET http://127.0.0.1:5901/SupplyChain/Claims
