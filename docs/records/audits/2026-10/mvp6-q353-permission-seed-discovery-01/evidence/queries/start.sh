#!/bin/bash
# usage: start.sh <name> <api-dir> <port> <dll> [ENV=VAL ...] — starts one service in background, records pid
E=$(cd "$(dirname "$0")" && pwd); name=$1; api=$2; port=$3; dll=$4; shift 4
cd "$E/src/$api"
env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:$port Urls=http://127.0.0.1:$port \
  JwtSettings__Secret="$(cat $E/jwt.secret)" "$@" nohup dotnet bin/Debug/net8.0/$dll > $E/log/$name.log 2>&1 &
echo $! > $E/run/$name.pid
for i in $(seq 1 90); do grep -q "Now listening" $E/log/$name.log && break; kill -0 $(cat $E/run/$name.pid) 2>/dev/null || break; sleep 0.5; done
echo "== $name pid=$(cat $E/run/$name.pid) listening=$(grep -c 'Now listening' $E/log/$name.log) alive=$(kill -0 $(cat $E/run/$name.pid) 2>/dev/null && echo yes || echo no)"
