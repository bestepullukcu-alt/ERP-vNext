#!/bin/zsh
cd ~/mvp6-env/base/build-tree
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1
U="mongodb://127.0.0.1:57192/?replicaSet=rsmod192"
export MOD0183_TEST_MONGO=$U MOD0184_TEST_MONGO=$U MOD0185_TEST_MONGO=$U MVP6_MOD0190_MONGO_URI=$U RETURNS_MONGO_URI=$U CLAIMS_TEST_MONGO=$U
P=services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj
L=~/mvp6-env/base/logs/test/split; mkdir -p $L; S=$L/SUMMARY.tsv; printf "suite\tfilter\texit\tsummary\n" > $S
N=Diten.SupplyChainService.Tests
for pair in \
 "shipments-root|FullyQualifiedName~$N.Shipment|FullyQualifiedName~$N.SourceIntake|FullyQualifiedName~$N.SourceClient" \
 "carriers|FullyQualifiedName~$N.Carriers." "loads|FullyQualifiedName~$N.Loads." "returns|FullyQualifiedName~$N.Returns." \
 "claims|FullyQualifiedName~$N.Claims." "sandop|FullyQualifiedName~$N.SandopPlans." "capacity|FullyQualifiedName~$N.CapacityPlans." ; do
  n=${pair%%|*}; f=${pair#*|}
  dotnet test $P --no-build --disable-build-servers --filter "$f" --logger "trx;LogFileName=$n.trx" --results-directory $L > $L/$n.log 2>&1; rc=$?
  printf "%s\t%s\t%s\t%s\n" "$n" "$f" "$rc" "$(grep -E '(Passed|Failed)!' $L/$n.log | tail -1 | sed 's/^ *//')" >> $S
done
dotnet test $P --no-build --disable-build-servers --logger "trx;LogFileName=supplychain-full-run3.trx" --results-directory $L > $L/full-run3.log 2>&1
printf "full-run3\t(none)\t%s\t%s\n" "$?" "$(grep -E '(Passed|Failed)!' $L/full-run3.log | tail -1 | sed 's/^ *//')" >> $S
echo SPLIT-DONE
