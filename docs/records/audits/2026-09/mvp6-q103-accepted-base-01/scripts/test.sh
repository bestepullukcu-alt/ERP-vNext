#!/bin/zsh
cd ~/mvp6-env/base/build-tree
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1
U="mongodb://127.0.0.1:57192/?replicaSet=rsmod192"
export MOD0183_TEST_MONGO=$U MOD0184_TEST_MONGO=$U MOD0185_TEST_MONGO=$U MVP6_MOD0190_MONGO_URI=$U RETURNS_MONGO_URI=$U CLAIMS_TEST_MONGO=$U
L=~/mvp6-env/base/logs/test; S=$L/SUMMARY.tsv; printf "start\tend\tsuite\texit\tsummary\n" > $S
run() { n=$1; shift; s=$(date -u +%FT%TZ)
  dotnet test "$@" --no-build -c Debug --disable-build-servers --logger "trx;LogFileName=$n.trx" --results-directory $L > $L/$n.log 2>&1; rc=$?
  printf "%s\t%s\t%s\t%s\t%s\n" "$s" "$(date -u +%FT%TZ)" "$n" "$rc" "$(grep -E '(Passed|Failed)!' $L/$n.log | tail -1 | sed 's/^ *//')" >> $S; }
run supplychain services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj
run architecture tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj
run gateway gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj
run web frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
run secrets services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets.Tests/Diten.BuildingBlocks.Security.Secrets.Tests.csproj
echo TEST-DONE
