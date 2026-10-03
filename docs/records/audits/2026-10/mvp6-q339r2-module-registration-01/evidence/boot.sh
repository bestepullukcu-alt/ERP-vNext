#!/bin/bash
# usage: boot.sh <name> <api-dir> <wait-after-listen-s> [ENV=VALUE ...]   — boots the copy's Api, probes /health, stops ONLY its own pid.
E=$(cd "$(dirname "$0")" && pwd); name=$1; api=$2; extra=$3; shift 3
log=$E/boots/$name.log; : > $log
cd "$api"
env ASPNETCORE_ENVIRONMENT=Development Urls=http://127.0.0.1:58339 \
  Mongo__ConnectionString='mongodb://127.0.0.1:57339/?replicaSet=rsq339' Mongo__DatabaseName=q339r2_boot \
  JwtSettings__Secret="$(cat $E/jwt.secret)" JwtSettings__Issuer=Diten JwtSettings__Audience=Diten "$@" \
  dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll >> $log 2>&1 &
pid=$!; s=$(date +%s); up=no
for i in $(seq 1 60); do grep -q "Now listening" $log && { up=yes; break; }; kill -0 $pid 2>/dev/null || break; sleep 0.5; done
echo "== $name pid=$pid now_listening=$up after_s=$(( $(date +%s)-s ))"
sleep $extra
echo "== $name GET /health -> $(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:58339/health)"
echo "== $name alive_before_stop=$(kill -0 $pid 2>/dev/null && echo yes || echo no)"
kill $pid 2>/dev/null; wait $pid 2>/dev/null
for i in $(seq 1 20); do nc -z 127.0.0.1 58339 || break; sleep 0.5; done
echo "== $name stopped; 58339 $(nc -z 127.0.0.1 58339 && echo STILL-OPEN || echo free)"
