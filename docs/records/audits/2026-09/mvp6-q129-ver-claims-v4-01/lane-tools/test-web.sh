#!/bin/zsh
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1 Q129_LANE=1
Q=~/mvp6-env/q129; mkdir -p $Q/tmp; export TMPDIR=$Q/tmp/; L=$Q/logs/test; mkdir -p $L; S=$L/TEST-SUMMARY.tsv
[ -f $S ] || printf "tree\tsuite\tfilter\texit\tsummary\n" > $S
t() { tree=$1; n=$2; proj=$3; filt=$4; cd $Q/$tree; tn=${tree:gs|/|-}--$n
  if [ -n "$filt" ]; then dotnet test $proj --no-build --disable-build-servers --filter "$filt" --results-directory $L --logger "trx;LogFileName=$tn.trx" > $L/$tn.log 2>&1
  else dotnet test $proj --no-build --disable-build-servers --results-directory $L --logger "trx;LogFileName=$tn.trx" > $L/$tn.log 2>&1; fi; rc=$?
  printf "%s\t%s\t%s\t%s\t%s\n" $tree $n "${filt:--}" $rc "$(grep -E '(Passed|Failed)!' $L/$tn.log | tail -1 | sed 's/^ *//')" >> $S; }
W=frontend/Diten.Web.Tests/Diten.Web.Tests.csproj; F="FullyQualifiedName~ClaimIndexBehaviorTests"
t v4/build web-all $W ""
t v4/build web-ClaimIndexBehaviorTests $W "$F"
t v3/sab  web-ClaimIndexBehaviorTests-SABOTAGE $W "$F"
echo WEB-DONE
