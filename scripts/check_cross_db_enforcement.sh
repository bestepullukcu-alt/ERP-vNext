#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

offenders=$(rg -n "new MongoClient\(" services --glob '!**/obj/**' --glob '!**/bin/**' || true)
filtered=""

while IFS= read -r line; do
  [ -z "$line" ] && continue
  path="${line%%:*}"
  normalized="${path//\\//}"

  if [[ "$normalized" == *".Persistence/"* ]] ||
     [[ "$normalized" == *"Infrastructure/DependencyInjection.cs"* ]] ||
     [[ "$normalized" == *"/Infrastructure/Persistence/"* ]] ||
     [[ "$normalized" == *"/EnterpriseStrategy.Persistence/Context/"* ]]; then
    continue
  fi

  filtered+="$line"$'\n'
done <<< "$offenders"

if [[ -n "$filtered" ]]; then
  echo "Cross-DB enforcement failed. MongoClient usage outside allowed bootstrap/persistence files:"
  printf '%s' "$filtered"
  exit 1
fi

echo "Cross-DB enforcement check passed."
