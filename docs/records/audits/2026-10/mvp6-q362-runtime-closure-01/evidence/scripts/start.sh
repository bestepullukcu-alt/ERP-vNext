#!/bin/bash
# usage: start.sh <name> <app-dir under src> <port> <dll> [ENV=VAL ...] — background start, pid recorded
E=$(cd "$(dirname "$0")" && pwd); name=$1; app=$2; port=$3; dll=$4; shift 4
cd "$E/src/$app"
env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:$port Urls=http://localhost:$port \
  JwtSettings__Secret="$(cat $E/jwt.secret)" "$@" nohup dotnet bin/Debug/net8.0/$dll > $E/log/$name.log 2>&1 &
echo $! > $E/run/$name.pid
for i in $(seq 1 120); do nc -z 127.0.0.1 $port 2>/dev/null && break; kill -0 $(cat $E/run/$name.pid) 2>/dev/null || break; sleep 0.5; done
echo "== $name pid=$(cat $E/run/$name.pid) port $port $(nc -z 127.0.0.1 $port 2>/dev/null && echo OPEN || echo CLOSED) alive=$(kill -0 $(cat $E/run/$name.pid) 2>/dev/null && echo yes || echo no) $(date +%T)"
