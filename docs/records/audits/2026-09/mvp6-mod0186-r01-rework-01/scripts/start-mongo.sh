#!/usr/bin/env bash
set -euo pipefail

EVIDENCE_ROOT=/private/tmp/mvp6-mod0186-r01-rework-evidence-01
mkdir -p "$EVIDENCE_ROOT/mongo/db" "$EVIDENCE_ROOT/mongo/log"
exec mongod --dbpath "$EVIDENCE_ROOT/mongo/db" --port 27187 --bind_ip 127.0.0.1 \
  --replSet returns_r01_rework --setParameter enableTestCommands=1 \
  --logpath "$EVIDENCE_ROOT/mongo/log/mongod.log"
