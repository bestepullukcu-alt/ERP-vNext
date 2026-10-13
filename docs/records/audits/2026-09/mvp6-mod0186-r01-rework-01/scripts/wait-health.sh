#!/usr/bin/env bash
set -euo pipefail

for _ in $(seq 1 60); do
  if curl -fsS http://127.0.0.1:51862/health > /private/tmp/mvp6-mod0186-r01-rework-evidence-01/raw/health.json; then
    exit 0
  fi
  sleep 1
done
exit 1
