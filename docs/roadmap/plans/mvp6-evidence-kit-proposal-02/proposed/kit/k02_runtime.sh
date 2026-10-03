#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K02: native .NET 8 runtime check.
# Fails unless the lane dotnet is the expected native .NET 8 SDK/runtime with no global.json redirect.
# Major roll-forward (e.g. to .NET 10) never counts as .NET 8 evidence (handoff ENVIRONMENT.md).
set -u
. "$(dirname "$0")/ek_lib.sh"
ek_require_env
: "${EK_DOTNET:?}" "${EK_EXPECTED_SDK:?}" "${EK_EXPECTED_RUNTIME:?}"

RAW="$EK_EVIDENCE/raw"
[ -x "$EK_DOTNET" ] || ek_die "dotnet not executable at $EK_DOTNET"

# --info from inside the disposable source so a global.json there would show up.
( cd "$EK_WORK/source" && ek_run K02 dotnet-info "dotnet --info (source dir)" -- "$EK_DOTNET" --info ) > "$RAW/dotnet-info.txt" 2>&1 \
  || ek_die "dotnet --info failed"
"$EK_DOTNET" --list-sdks > "$RAW/dotnet-sdks.txt"
"$EK_DOTNET" --list-runtimes > "$RAW/dotnet-runtimes.txt"

fail=0
sdk=$(awk '/^\.NET SDK:/{f=1} f&&/Version:/{print $2; exit}' "$RAW/dotnet-info.txt")
[ "$sdk" = "$EK_EXPECTED_SDK" ] || { echo "SDK $sdk != expected $EK_EXPECTED_SDK"; fail=1; }
case "$sdk" in 8.0.*) ;; *) echo "SDK $sdk is not 8.0.x"; fail=1 ;; esac
for fw in Microsoft.NETCore.App Microsoft.AspNetCore.App; do
  grep -q "^$fw $EK_EXPECTED_RUNTIME " "$RAW/dotnet-runtimes.txt" || { echo "$fw $EK_EXPECTED_RUNTIME not installed"; fail=1; }
done
grep -q 'global.json file:' "$RAW/dotnet-info.txt" && ! grep -A1 'global.json file:' "$RAW/dotnet-info.txt" | grep -q 'Not found' \
  && { echo "global.json present — SDK selection is redirected"; fail=1; }
arch=$(awk '/^Host:/{f=1} f&&/Architecture:/{print $2; exit}' "$RAW/dotnet-info.txt")

# Launch policy recorded for K06: patch roll-forward only, never minor/major.
{
  printf 'field\tvalue\n'
  printf 'dotnet\t%s\n' "$EK_DOTNET"
  printf 'sdk\t%s\n' "$sdk"
  printf 'runtime_expected\t%s\n' "$EK_EXPECTED_RUNTIME"
  printf 'host_architecture\t%s\n' "$arch"
  printf 'launch_roll_forward\tLatestPatch\n'
  printf 'result\t%s\n' "$([ $fail = 0 ] && echo PASS || echo FAIL)"
} > "$RAW/runtime-check.tsv"
[ $fail = 0 ] || ek_die "K02 runtime check failed (see raw/runtime-check.tsv)"
echo "K02 PASS: SDK $sdk, runtime $EK_EXPECTED_RUNTIME, $arch"
