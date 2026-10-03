#!/usr/bin/env bash
set -euo pipefail

mongosh --quiet --host 127.0.0.1 --port 27187 --eval \
  'try { rs.status() } catch (e) { rs.initiate({_id:"returns_r01_rework",members:[{_id:0,host:"127.0.0.1:27187"}]}) }'
for _ in $(seq 1 30); do
  if mongosh --quiet --host 127.0.0.1 --port 27187 --eval 'quit(db.hello().isWritablePrimary ? 0 : 1)' >/dev/null 2>&1; then
    exit 0
  fi
  sleep 1
done
exit 1
