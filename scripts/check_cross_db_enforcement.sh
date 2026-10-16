#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

# BL-408 — THIS GATE MUST NEVER PASS WITHOUT HAVING LOOKED. It used to run `rg … || true`. Where ripgrep is not
# installed — this dev machine, and the GitHub ubuntu-24.04 image, whose tool list has jq but no ripgrep (the
# workflow installs only jq) — rg exited 127, `|| true` swallowed it, nothing was scanned, and the step printed
# "passed".
#
# So: ripgrep when it is there, otherwise grep over the SAME scope (measured 2026-09-15: identical match list on
# the whole tree). Falling back rather than failing keeps CI checking without a CI change. And only grep/rg's
# "no match" exit (1) is allowed through; a missing tool (127) or an error (2) fails the gate.
pattern='new MongoClient\('

if command -v rg >/dev/null 2>&1; then
  scanner="rg"
  scan() { rg -n "$pattern" services --glob '!**/obj/**' --glob '!**/bin/**'; }
else
  scanner="grep -E -r"
  # rg's scope spelled for grep: obj/ and bin/ at any depth are skipped, and so are hidden directories and binary
  # files, which rg skips by default.
  scan() { grep -E -r -n -I "$pattern" services --exclude-dir=obj --exclude-dir=bin --exclude-dir='.*'; }
fi

scan_status=0
offenders=$(scan) || scan_status=$?
if [[ "$scan_status" -gt 1 ]]; then
  echo "Cross-DB enforcement could not scan: '$scanner' exited $scan_status. Failing rather than passing unchecked."
  exit 2
fi

filtered=""

# WP-CI-SERVICE-TESTS-03 FIX1 (CT decision b) — ONE named exception, by exact path, and for ONE client only.
# 61af28b01 (2026-09-23, "add isolated Market operational mode") added a second composition root for the Platform
# Market operational command mode: it builds its own MongoClient the way Infrastructure/DependencyInjection.cs does
# (allowed above by name). Its folder (Diten.Platform.Infrastructure/Persistence/) is the Platform persistence layer, but
# the "/Infrastructure/Persistence/" pattern needs a "/" before "Infrastructure", so it never matched. Moving the file
# would end the exception (CT may do so). No glob: a second `new MongoClient(` in this file, or any other file, is red.
exact_exception="services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/VerifiedMarketOperationalPersistenceServiceCollectionExtensions.cs"
exact_exception_hits=0

while IFS= read -r line; do
  [ -z "$line" ] && continue
  path="${line%%:*}"
  normalized="${path//\\//}"

  if [[ "$normalized" == "$exact_exception" ]]; then
    exact_exception_hits=$((exact_exception_hits + 1))
    if [[ "$exact_exception_hits" -le 1 ]]; then
      continue
    fi
  fi

  if [[ "$normalized" == *"/tests/"* ]] ||
     [[ "$normalized" == *".Persistence/"* ]] ||
     [[ "$normalized" == *"Infrastructure/DependencyInjection.cs"* ]] ||
     [[ "$normalized" == *"/Infrastructure/Persistence/"* ]] ||
     [[ "$normalized" == *"/EnterpriseStrategy.Persistence/Context/"* ]]; then
    continue
  fi

  filtered+="$line"$'\n'
done <<< "$offenders"

if [[ -n "$filtered" ]]; then
  echo "Cross-DB enforcement failed (scanned with $scanner). MongoClient usage outside allowed bootstrap/persistence files:"
  printf '%s' "$filtered"
  exit 1
fi

echo "Cross-DB enforcement check passed (scanned with $scanner)."
