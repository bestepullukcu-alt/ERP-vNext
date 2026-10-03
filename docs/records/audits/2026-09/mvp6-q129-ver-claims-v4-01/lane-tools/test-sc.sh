#!/bin/zsh
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1 Q129_LANE=1
Q=~/mvp6-env/q129; export TMPDIR=$Q/tmp/; L=$Q/logs/test; S=$L/TEST-SUMMARY.tsv
U="mongodb://127.0.0.1:57192/?replicaSet=rsmod192"
export CLAIMS_TEST_MONGO=$U MOD0183_TEST_MONGO=$U MOD0184_TEST_MONGO=$U MOD0185_TEST_MONGO=$U MVP6_MOD0190_MONGO_URI=$U RETURNS_MONGO_URI=$U
cd $Q/v4/build; n=v4-build--supplychain-single
echo "start $(date +%H:%M:%S)" > $L/SC-window.txt
dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj --no-build --disable-build-servers --results-directory $L --logger "trx;LogFileName=$n.trx" > $L/$n.log 2>&1; rc=$?
echo "end $(date +%H:%M:%S) exit $rc" >> $L/SC-window.txt
printf "%s\t%s\t%s\t%s\t%s\n" v4/build supplychain-single - $rc "$(grep -E '(Passed|Failed)!' $L/$n.log | tail -1 | sed 's/^ *//')" >> $S
echo SC-DONE
