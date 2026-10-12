#!/usr/bin/env bash
# Q122 test battery on ~/mvp6-env/q122/build. Isolated lane test Mongo 127.0.0.1:57222/rsq122 (enableTestCommands=1) only.
set -u
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1
U="mongodb://127.0.0.1:57222/?replicaSet=rsq122"
export CLAIMS_TEST_MONGO=$U MOD0183_TEST_MONGO=$U MOD0184_TEST_MONGO=$U MOD0185_TEST_MONGO=$U MVP6_MOD0190_MONGO_URI=$U RETURNS_MONGO_URI=$U
D=/Users/natig/.dotnet/dotnet; Q=/Users/natig/mvp6-env/q122; R=$Q/logs/test; mkdir -p $R; S=$R/TEST-SUMMARY.tsv
[ -f $S ] || printf 'suite\tfilter\texit\tsummary\n' > $S
t() { local n=$1 proj=$2 filt=$3; shift 3
  (cd $Q/build && if [ -n "$filt" ]; then $D test $proj --no-build --disable-build-servers --filter "$filt" --results-directory $R --logger "trx;LogFileName=$n.trx" "$@" > $R/$n.log 2>&1; else $D test $proj --no-build --disable-build-servers --results-directory $R --logger "trx;LogFileName=$n.trx" "$@" > $R/$n.log 2>&1; fi); local rc=$?
  printf '%s\t%s\t%s\t%s\n' "$n" "${filt:--}" "$rc" "$(grep -E '^(Passed|Failed)!' $R/$n.log | tail -1)" >> $S; }
SC=services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj
t web frontend/Diten.Web.Tests/Diten.Web.Tests.csproj ""
t web-claims frontend/Diten.Web.Tests/Diten.Web.Tests.csproj "FullyQualifiedName~Claim"
t architecture tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj ""
t supplychain-single $SC ""
t supplychain-claims-and-provider $SC "FullyQualifiedName~Diten.SupplyChainService.Tests.Claims.|FullyQualifiedName~ClaimsManagementManifestProvider"
