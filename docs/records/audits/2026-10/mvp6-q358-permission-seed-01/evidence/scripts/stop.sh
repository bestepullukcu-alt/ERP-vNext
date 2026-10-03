#!/bin/bash
# stops only the pids this lane recorded, then the lane's mongod
E=$(cd "$(dirname "$0")" && pwd)
for n in "$@"; do pid=$(cat $E/run/$n.pid 2>/dev/null) || continue; kill $pid 2>/dev/null && echo "stopped $n pid=$pid"; done
sleep 4
