#!/bin/bash
# usage: start.sh <name> <srcroot> <api-dir> <port> <dll> [ENV=VAL ...] — start one service in background; records pid
E=$(cd "$(dirname "$0")" && pwd); name=$1; src=$2; api=$3; port=$4; dll=$5; shift 5
cd "$E/$src/$api"
env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:$port Urls=http://127.0.0.1:$port \
  JwtSettings__Secret="$(cat $E/jwt.secret)" "$@" nohup dotnet bin/Debug/net8.0/$dll > $E/log/$name.log 2>&1 &
echo $! > $E/run/$name.pid
for i in $(seq 1 120); do nc -z 127.0.0.1 $port 2>/dev/null && break; kill -0 $(cat $E/run/$name.pid) 2>/dev/null || break; sleep 0.5; done
echo "== $name pid=$(cat $E/run/$name.pid) port $port $(nc -z 127.0.0.1 $port 2>/dev/null && echo OPEN || echo CLOSED) alive=$(kill -0 $(cat $E/run/$name.pid) 2>/dev/null && echo yes || echo no)"
