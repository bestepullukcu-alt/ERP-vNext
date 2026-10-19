#!/usr/bin/env bash
# MOD-0033-FU02 §F.3 — Development bootstrap of the MDM -> Auth -> Platform service trust chain.
#
# Writes ONE git-ignored appsettings.Development.local.json into each of the Auth, Platform and MDM API
# directories: a disposable RSA-2048 key pair (private key to Auth, public key to Platform) and a service-client
# credential pair (hash to Auth, raw secret to MDM), plus the approver candidates for the Global Product approval.
#
# It prints nothing secret, takes no secret as an argument, never overwrites an existing file, and is safe to run
# twice (the second run reports "already set up"). It touches no database and no network.
#
#   scripts/dev/setup-service-trust.sh --tenant-id <guid> --approver-user-id <guid> [--approver-user-id <guid> ...]
#
# Optional: --auth-dir / --platform-dir / --mdm-dir <dir> to write somewhere other than this checkout's API
# directories; --tenant-id may be repeated; --client-code, --issuer, --auth-base-url override the defaults.
# Guide: docs/guides/operations/service-trust-chain-runbook.md
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
auth_dir="$repo_root/services/Diten.AuthService/src/Diten.AuthService.Api"
platform_dir="$repo_root/services/Diten.Platform/src/Diten.Platform.API"
mdm_dir="$repo_root/services/Diten.MdmService/src/Diten.MdmService.Api"

passthrough=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --auth-dir)     [ "$#" -ge 2 ] || { echo "missing value for $1" >&2; exit 2; }; auth_dir="$2"; shift 2 ;;
    --platform-dir) [ "$#" -ge 2 ] || { echo "missing value for $1" >&2; exit 2; }; platform_dir="$2"; shift 2 ;;
    --mdm-dir)      [ "$#" -ge 2 ] || { echo "missing value for $1" >&2; exit 2; }; mdm_dir="$2"; shift 2 ;;
    *)              passthrough+=("$1"); shift ;;
  esac
done

# A prebuilt bootstrap assembly can be named instead of building the project (used by the automated test, which
# must not start a build while it is itself running). A path, not a secret.
if [ -n "${DITEN_TRUST_BOOTSTRAP_DLL:-}" ]; then
  exec dotnet "$DITEN_TRUST_BOOTSTRAP_DLL" \
    --auth-dir "$auth_dir" --platform-dir "$platform_dir" --mdm-dir "$mdm_dir" ${passthrough[@]+"${passthrough[@]}"}
fi

exec dotnet run --nologo \
  --project "$repo_root/services/Diten.AuthService/tools/Diten.AuthService.DevTrustBootstrap/Diten.AuthService.DevTrustBootstrap.csproj" \
  -- --auth-dir "$auth_dir" --platform-dir "$platform_dir" --mdm-dir "$mdm_dir" ${passthrough[@]+"${passthrough[@]}"}
