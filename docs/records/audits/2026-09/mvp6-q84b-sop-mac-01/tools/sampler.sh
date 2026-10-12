#!/bin/bash
# samples, once per second, every ESTABLISHED TCP connection whose remote OR local port is 27017 (the dev mongod) — bash, arguments quoted (Q121c lesson)
out=$1; stopf=$2
while [ ! -f "$stopf" ]; do
  r=$(lsof -nP '-iTCP:27017' -sTCP:ESTABLISHED -Fpcn 2>/dev/null | paste -sd' ' -)
  [ -n "$r" ] && echo "$(date +%H:%M:%S) $r" >> "$out"
  sleep 1
done
