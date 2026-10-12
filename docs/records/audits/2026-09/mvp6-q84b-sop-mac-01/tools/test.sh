#!/bin/zsh
# Q84b tests. Env ONLY from each tree's own scripts/test-env/mvp6-test-mongo-env.sh (slot 8, rsq84bs8, --all-supplychain).
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1
C=~/mvp6-env/q84b/c2; Q=~/mvp6-env/q84b; mkdir -p $Q/tmp; export TMPDIR=$Q/tmp/
L=$Q/logs/test; mkdir -p $L; S=$L/TEST-RUNS.tsv; [ -f $S ] || printf "tree\tsuite\texit\tsummary\n" > $S
t() { tree=$1; n=$2; proj=$3; tn=$tree--$n
  ( cd $C/$tree/src && eval "$(scripts/test-env/mvp6-test-mongo-env.sh --slot 8 --rs rsq84bs8 --all-supplychain)" ) || { echo "env script failed"; exit 2; }
  eval "$($C/$tree/src/scripts/test-env/mvp6-test-mongo-env.sh --slot 8 --rs rsq84bs8 --all-supplychain)"
  env | grep -E '_MONGO|MONGO_URI' | sed -E 's/=.*//' | sort | paste -sd, - > $L/$tn.envnames
  echo "$(date +%H:%M:%S) start $tn" >> $L/RUN-ORDER.txt
  cd $C/$tree/build
  dotnet test $proj --no-build --disable-build-servers --results-directory $L --logger "trx;LogFileName=$tn.trx" > $L/$tn.log 2>&1; rc=$?
  echo "$(date +%H:%M:%S) end $tn rc=$rc" >> $L/RUN-ORDER.txt
  printf "%s\t%s\t%s\t%s\n" $tree $n $rc "$(grep -E '(Passed|Failed)!' $L/$tn.log | tail -1 | sed 's/^ *//')" >> $S; }
for tr in B A; do
  t $tr web-tests frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
  t $tr supplychain-tests services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj
done
echo TEST-DONE
