#!/bin/zsh
# Q65b build (copied from Q84b build.sh): clones of treeA/src and treeB/src into <tree>/build; SupplyChain sln + Diten.Web + Diten.Web.Tests. Debug.
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1
C=~/mvp6-env/q65b; L=~/mvp6-env/q65b/logs; S=$L/BUILD-SUMMARY.tsv; printf "tree\ttarget\texit\twarnings\terrors\tlog\n" > $S
for tr in treeA treeB; do
  [ -e $C/$tr/build ] && { echo "build dir exists: $tr"; exit 2; }
  cp -c -Rp $C/$tr/src $C/$tr/build && chmod -R u+w $C/$tr/build || exit 2
done
b() { tree=$1; t=$2; n=build-$tree--${${t:t}:r}; cd $C/$tree/build
  dotnet build $t -c Debug --disable-build-servers -p:UseSharedCompilation=false > $L/$n.log 2>&1; rc=$?
  w=$(grep -E '^ *[0-9]+ Warning\(s\)' $L/$n.log | tail -1 | awk '{print $1}'); e=$(grep -E '^ *[0-9]+ Error\(s\)' $L/$n.log | tail -1 | awk '{print $1}')
  printf "%s\t%s\t%s\t%s\t%s\t%s\n" $tree $t $rc "$w" "$e" "logs/$n.log" >> $S; }
for tr in treeB treeA; do
  for t in services/Diten.SupplyChainService/Diten.SupplyChainService.sln frontend/Diten.Web/Diten.Web.csproj frontend/Diten.Web.Tests/Diten.Web.Tests.csproj; do b $tr $t; done
done
echo BUILD-DONE
