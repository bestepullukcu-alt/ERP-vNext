#!/bin/bash
# C-13 lane: start one service in background (Development), lane Mongo + lane RabbitMQ only; refuses any 27017 URI.
# usage: start.sh <name> <api-dir> <port> <dll> [ENV=VAL ...]
set -u
E=$(cd "$(dirname "$0")" && pwd); R=/Users/natig/Projects/ERP-vNext-recovery
name=$1; api=$2; port=$3; dll=$4; shift 4
URI='mongodb://127.0.0.1:35994/?replicaSet=rsc13'
for kv in "$@"; do case "$kv" in *27017*) echo "REFUSED: $kv names 27017"; exit 3;; esac; done
cd "$R/$api"
env DOTNET_ROOT=$HOME/.dotnet DOTNET_ROOT_ARM64=$HOME/.dotnet ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS=http://127.0.0.1:$port Urls=http://127.0.0.1:$port \
  Logging__LogLevel__Default=Warning Logging__LogLevel__Microsoft=Warning \
  JwtSettings__Secret="$(cat $E/jwt.secret)" \
  Eventing__Transport=RabbitMQ Eventing__Host=127.0.0.1 Eventing__Port=5672 \
  "$@" nohup dotnet bin/Debug/net8.0/$dll > $E/log/$name.log 2>&1 &
echo $! > $E/run/$name.pid
for i in $(seq 1 120); do nc -z 127.0.0.1 $port 2>/dev/null && break; kill -0 $(cat $E/run/$name.pid) 2>/dev/null || break; sleep 0.5; done
echo "== $name pid=$(cat $E/run/$name.pid) port $port $(nc -z 127.0.0.1 $port 2>/dev/null && echo OPEN || echo CLOSED) alive=$(kill -0 $(cat $E/run/$name.pid) 2>/dev/null && echo yes || echo no)"
