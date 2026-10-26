#!/usr/bin/env bash
# WP-PLT-WORKFLOW-TEMPLATES-01 — installs the product-identity approval definitions (Platform's catalog
# Seed/workflow-templates/product-identity-templates.json) in the tenants you name, through Platform's one-shot
# --ensure-workflow-templates command. A DRY RUN unless you pass --apply yourself: this script never adds it.
#
#   scripts/dev/ensure-workflow-templates.sh --tenant <guid> [--tenant <guid> ...] [--apply]
#
# Uses the Platform build the fleet already produced (--no-build); the command exits before the web host starts.
# Exit codes are the command's: 0 done · 1 failure · 2 drift / needs-owner (left as is) · 3 refused · 4 stopped part-way.
# Guide: docs/guides/operations/service-trust-chain-runbook.md §4
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
platform_dir="$repo_root/services/Diten.Platform/src/Diten.Platform.API"

has_tenant=false
for argument in "$@"; do
  if [ "$argument" = "--tenant" ]; then has_tenant=true; fi
done
if [ "$has_tenant" != true ]; then
  echo "usage: scripts/dev/ensure-workflow-templates.sh --tenant <guid> [--tenant <guid> ...] [--apply]" >&2
  exit 3
fi

cd "$platform_dir"
exec dotnet run --no-build --launch-profile http -- --ensure-workflow-templates "$@"
