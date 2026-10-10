#!/usr/bin/env bash
# Q122 builds on ~/mvp6-env/q122/build (copy of the composed src). No build servers left behind.
set -u
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1
D=/Users/natig/.dotnet/dotnet; L=/Users/natig/mvp6-env/q122/logs/build; mkdir -p $L; S=$L/BUILD-SUMMARY.tsv
[ -f $S ] || printf 'target\texit\twarnings\terrors\n' > $S
b() { local t=$1 n; n=$(echo "$t" | tr '/' '_'); (cd /Users/natig/mvp6-env/q122/build && $D build "$t" -c Debug --disable-build-servers -p:UseSharedCompilation=false > $L/build-$n.log 2>&1); local rc=$?
  printf '%s\t%s\t%s\t%s\n' "$t" "$rc" "$(grep -E '^ +[0-9]+ Warning\(s\)' $L/build-$n.log | awk '{print $1}' | tail -1)" "$(grep -E '^ +[0-9]+ Error\(s\)' $L/build-$n.log | awk '{print $1}' | tail -1)" >> $S; }
for t in services/Diten.SupplyChainService/Diten.SupplyChainService.sln frontend/Diten.Web.Tests/Diten.Web.Tests.csproj gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj; do b $t; done
