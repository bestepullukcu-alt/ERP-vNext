#!/bin/zsh
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1 Q129_LANE=1
Q=~/mvp6-env/q129; L=$Q/logs/build; mkdir -p $L; S=$L/BUILD-SUMMARY.tsv; printf "tree\ttarget\texit\twarnings\terrors\n" > $S
b() { tree=$1; t=$2; n=${tree:gs|/|-}--${${t:t}:r}; cd $Q/$tree
  dotnet build $t -c Debug --disable-build-servers -p:UseSharedCompilation=false > $L/$n.log 2>&1; rc=$?
  w=$(grep -E '^ *[0-9]+ Warning\(s\)' $L/$n.log | tail -1 | awk '{print $1}'); e=$(grep -E '^ *[0-9]+ Error\(s\)' $L/$n.log | tail -1 | awk '{print $1}')
  printf "%s\t%s\t%s\t%s\t%s\n" $tree $t $rc "$w" "$e" >> $S; }
b v4/build frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
b v4/build services/Diten.SupplyChainService/Diten.SupplyChainService.sln
b v3/sab frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
echo BUILD-DONE
