#!/bin/zsh
cd ~/mvp6-env/base/build-tree
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_NOLOGO=1
S=~/mvp6-env/base/logs/build/SUMMARY.tsv; print -r -- "start\tend\ttarget\texit\twarnings\terrors" > $S
for t in \
  services/Diten.SupplyChainService/Diten.SupplyChainService.sln \
  services/Diten.AuthService/Diten.AuthService.sln \
  services/Diten.Platform/Diten.Platform.sln \
  services/Diten.MdmService/Diten.MdmService.sln \
  services/Diten.PpmService/Diten.PpmService.sln \
  services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets.Tests/Diten.BuildingBlocks.Security.Secrets.Tests.csproj \
  frontend/Diten.Web/Diten.Web.sln \
  frontend/Diten.Web.Tests/Diten.Web.Tests.csproj \
  gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj \
  tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj; do
  n=${t:t:r}; s=$(date -u +%FT%TZ)
  dotnet build $t -c Debug --disable-build-servers -p:UseSharedCompilation=false > ~/mvp6-env/base/logs/build/$n.log 2>&1; rc=$?
  w=$(grep -E '^ *[0-9]+ Warning\(s\)' ~/mvp6-env/base/logs/build/$n.log | tail -1 | awk '{print $1}')
  e=$(grep -E '^ *[0-9]+ Error\(s\)' ~/mvp6-env/base/logs/build/$n.log | tail -1 | awk '{print $1}')
  print -r -- "$s\t$(date -u +%FT%TZ)\t$t\t$rc\t$w\t$e" >> $S
done
echo BUILD-DONE
