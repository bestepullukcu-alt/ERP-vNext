#!/bin/bash
# usage: run.sh <tag> <hold-seconds>
S=$(cd "$(dirname "$0")" && pwd); TAG=$1; HOLD=$2
cd $S/tree/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api || exit 9
lsof -nP -iTCP:5061 -sTCP:LISTEN && { echo PORT-BUSY; exit 8; }
E=${3:-Development}; export ASPNETCORE_ENVIRONMENT=$E DOTNET_ENVIRONMENT=$E; echo env=$E
export Mongo__ConnectionString='mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000'
export Mongo__DatabaseName=diten_q232_unbreak
export JwtSettings__Secret=$(openssl rand -hex 32)
export JwtSettings__Issuer=q232-unbreak JwtSettings__Audience=q232-unbreak
echo "start $(date '+%F %T %z') sha(Program.cs)=$(shasum -a 256 Program.cs | cut -c1-64)"
dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll > $S/logs/service-$TAG.log 2>&1 &
PID=$!; echo pid=$PID
for i in $(seq 1 20); do sleep 1; kill -0 $PID 2>/dev/null || break; lsof -nP -iTCP:5061 -sTCP:LISTEN >/dev/null 2>&1 && break; done
if ! kill -0 $PID 2>/dev/null; then wait $PID; echo "EXITED code=$? at $(date '+%T') after ~${i}s"; exit 0; fi
echo "listening: $(lsof -nP -iTCP:5061 -sTCP:LISTEN | tail -1)"
H=(-H 'X-Correlation-Id: 11111111-2222-4333-8444-555555555555' -H 'X-Tenant-Id: 00000000-0000-0000-0000-000000000001' -H 'X-Legal-Entity-Id: 00000000-0000-0000-0000-000000000001')
n=0
for u in /health /api/shipment-bundle/shipments /api/shipment-bundle/claims /api/shipment-bundle/returns /api/shipment-bundle/carriers /api/shipment-bundle/loads /api/supply-chain/sandop-plans/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee /api/supply-chain/capacity-plans/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee; do
  n=$((n+1)); curl -sS -v "${H[@]}" http://127.0.0.1:5061$u > $S/logs/http-$TAG-$n.txt 2>&1; echo >> $S/logs/http-$TAG-$n.txt
done
for u in /api/shipment-bundle/shipments /api/shipment-bundle/claims; do n=$((n+1)); curl -sS -v http://127.0.0.1:5061$u > $S/logs/http-$TAG-$n.txt 2>&1; echo >> $S/logs/http-$TAG-$n.txt; done
sleep $HOLD
kill -0 $PID 2>/dev/null && echo "STILL UP at $(date '+%F %T %z') pid=$PID" || echo "DIED during hold"
curl -s -o /dev/null -w 'health-after-hold=%{http_code}\n' http://127.0.0.1:5061/health
kill -INT $PID; wait $PID; echo "stopped code=$? at $(date '+%T')"
lsof -nP -iTCP:5061 -sTCP:LISTEN || echo port-5061-free
