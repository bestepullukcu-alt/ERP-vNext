#!/usr/bin/env bash
# Q122 CU-27/NET-001 preflight probe: OPTIONS with CORS preflight headers (no token). Status + allow headers only.
O=$1; printf 'url\torigin\tstatus\tallow_methods\tallow_origin\n' > "$O"
for u in http://127.0.0.1:5900/api/shipment-bundle/claims http://127.0.0.1:5900/api/shipment-bundle/claims/00000000-0000-4000-8000-000000000001/transition http://127.0.0.1:5900/api/shipment-bundle/claimsXYZ; do
 for o in http://127.0.0.1:5901 http://localhost:5001; do
  h=$(curl -s -o /dev/null -D - -X OPTIONS -H "Origin: $o" -H 'Access-Control-Request-Method: POST' -H 'Access-Control-Request-Headers: content-type,idempotency-key,x-correlation-id' "$u")
  printf '%s\t%s\t%s\t%s\t%s\n' "$u" "$o" "$(echo "$h" | head -1 | awk '{print $2}')" "$(echo "$h" | grep -i '^access-control-allow-methods' | cut -d' ' -f2- | tr -d '\r')" "$(echo "$h" | grep -i '^access-control-allow-origin' | cut -d' ' -f2- | tr -d '\r')" >> "$O"
 done; done
