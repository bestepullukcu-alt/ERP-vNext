#!/bin/bash
# Q236 scratch runner. usage: run.sh <label> <fixed|original> [probe]
set -u
SP="$(cd "$(dirname "$0")" && pwd)"; LABEL="$1"; MODE="$2"; PROBE="${3:-}"
REPO=/Users/natig/Projects/ERP-vNext-recovery
SVC=$SP/services/Diten.SupplyChainService
DI=$SVC/src/Diten.SupplyChainService.Application/DependencyInjection.cs
API=$SVC/src/Diten.SupplyChainService.Api
if [ "$MODE" = fixed ]; then cp $SP/DependencyInjection.proposed.cs $DI; else cp $REPO/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/DependencyInjection.cs $DI; fi
echo "[$LABEL] DI sha256 $(shasum -a 256 $DI | cut -c1-64)"
dotnet build $API -c Debug --nologo > $SP/logs/build-$LABEL.log 2>&1; echo "[$LABEL] build rc=$? $(grep -E '^\s+[0-9]+ (Warning|Error)\(s\)' $SP/logs/build-$LABEL.log | tr -s ' ' | tr '\n' ' ')"
grep -E ": error " $SP/logs/build-$LABEL.log | sed -E 's/ \[.*//' | sort -u | head -5
[ -f $API/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll ] || exit 3
echo "[$LABEL] Application.dll sha256 $(shasum -a 256 $API/bin/Debug/net8.0/Diten.SupplyChainService.Application.dll | cut -c1-64)"
cd $API
export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
export Mongo__ConnectionString='mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000' Mongo__DatabaseName=diten_q236_unbreak
export JwtSettings__Secret="$(openssl rand -hex 32)" JwtSettings__Issuer=q236-unbreak JwtSettings__Audience=q236-unbreak
echo "[$LABEL] start $(TZ=Europe/Istanbul date '+%H:%M:%S')"
nohup dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll > $SP/logs/service-$LABEL.log 2>&1 &
PID=$!; unset JwtSettings__Secret
UP=0; for i in $(seq 1 40); do
  if ! kill -0 $PID 2>/dev/null; then break; fi
  if lsof -nP -iTCP:5061 -sTCP:LISTEN 2>/dev/null | grep -q "$PID"; then UP=1; break; fi
  sleep 0.5; done
if [ $UP = 0 ]; then wait $PID; echo "[$LABEL] NOT UP. exit code $? at $(TZ=Europe/Istanbul date '+%H:%M:%S'); port 5061 listeners: $(lsof -nP -iTCP:5061 -sTCP:LISTEN | wc -l | tr -d ' ')"
  echo "[$LABEL] unresolved-service errors: $(grep -o "Unable to resolve service for type '[^']*'" $SP/logs/service-$LABEL.log | sort | uniq -c | sed 's/Diten.SupplyChainService.//' | tr '\n' ';')"
  echo "[$LABEL] failing line: $(grep -o 'Program.cs:line [0-9]*' $SP/logs/service-$LABEL.log | head -1)"; exit 0; fi
echo "[$LABEL] UP pid $PID listening 5061 at $(TZ=Europe/Istanbul date '+%H:%M:%S')"
if [ -n "$PROBE" ]; then
  C=$(uuidgen | tr A-Z a-z)
  probe(){ n="$1"; shift; curl -s -o $SP/logs/http-$LABEL-$n.body -D $SP/logs/http-$LABEL-$n.headers -w "[$LABEL] $n status=%{http_code}" "$@"; echo " corr-header=$(grep -i '^x-correlation-id' $SP/logs/http-$LABEL-$n.headers | tr -d '\r' | cut -d' ' -f2) body=$(head -c 300 $SP/logs/http-$LABEL-$n.body)"; }
  probe health http://127.0.0.1:5061/health
  probe shipments-nocorr http://127.0.0.1:5061/api/shipment-bundle/shipments
  probe shipments-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/shipment-bundle/shipments
  probe claims-nocorr http://127.0.0.1:5061/api/shipment-bundle/claims
  probe claims-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/shipment-bundle/claims
  probe carriers-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/shipment-bundle/carriers
  probe loads-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/shipment-bundle/loads
  probe returns-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/shipment-bundle/returns
  probe capacity-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/supply-chain/capacity-plans/$C
  probe sandop-corr -H "X-Correlation-Id: $C" http://127.0.0.1:5061/api/supply-chain/sandop-plans/$C
  echo "[$LABEL] sent correlation $C"
fi
sleep 30
if kill -0 $PID 2>/dev/null; then echo "[$LABEL] STILL UP after 30 s at $(TZ=Europe/Istanbul date '+%H:%M:%S')"; else echo "[$LABEL] DIED within 30 s"; fi
kill -TERM $PID 2>/dev/null; wait $PID 2>/dev/null; echo "[$LABEL] stopped (SIGTERM) exit $?; port 5061 listeners now: $(lsof -nP -iTCP:5061 -sTCP:LISTEN | wc -l | tr -d ' ')"
tail -6 $SP/logs/service-$LABEL.log | cut -c1-200
