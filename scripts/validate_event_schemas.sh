#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SCHEMA_ROOT="$ROOT_DIR/events/schemas"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required to validate JSON schema files."
  exit 1
fi

find "$SCHEMA_ROOT" -type f -name '*.json' | while read -r file; do
  jq empty "$file"
  echo "validated: $file"
done
