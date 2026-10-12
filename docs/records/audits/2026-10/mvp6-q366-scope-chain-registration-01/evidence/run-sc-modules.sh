#!/bin/bash
# Q366 (copied from Q335; port, set and root changed): build once, then run Diten.SupplyChainService.Tests one module at a time with only that module's variable set (Q266 shape).
E=$1; T=$1/services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests
URI='mongodb://127.0.0.1:57370/?replicaSet=rsq366s'
unset MOD0183_TEST_MONGO MOD0184_TEST_MONGO MOD0185_TEST_MONGO MVP6_MOD0190_MONGO_URI MVP6_MOD0192_MONGO_URI RETURNS_MONGO_URI CLAIMS_TEST_MONGO MOD192_CHILD_MODE CLAIMS_RESTART_MODE CLAIMS_RESTART_TENANT RETURNS_RESTART_MODE RETURNS_PROCESS_EVIDENCE
mkdir -p $E/results; cd $T
s=$(date +%s); dotnet build -nodeReuse:false -p:UseSharedCompilation=false -v q > $E/results/build.log 2>&1; echo "== build rc=$? seconds=$(( $(date +%s)-s ))"; grep -E 'Warning\(s\)|Error\(s\)' $E/results/build.log
dotnet test --no-build --list-tests 2>/dev/null | sed -n '/The following Tests are available:/,$p' | tail -n +2 | sed 's/^ *//' > $E/results/all-tests.txt; echo "== listed tests: $(wc -l < $E/results/all-tests.txt | tr -d ' ')"
run() { # name var filter
  local s=$(date +%s); echo "== $1 start $(TZ=Europe/Istanbul date '+%T')"
  env $2="$URI" perl -e 'alarm 900; exec @ARGV' dotnet test --no-build --filter "$3" --logger "trx;LogFileName=$1.trx" --results-directory $E/results > $E/results/$1.log 2>&1
  echo "== $1 rc=$? seconds=$(( $(date +%s)-s ))"; grep -E '^(Passed!|Failed!)' $E/results/$1.log
}
run Shipments  MOD0183_TEST_MONGO     'FullyQualifiedName~Diten.SupplyChainService.Tests.Shipment|FullyQualifiedName~Diten.SupplyChainService.Tests.Source'
run Carriers   MOD0184_TEST_MONGO     'FullyQualifiedName~Diten.SupplyChainService.Tests.Carriers.'
run Loads      MOD0185_TEST_MONGO     'FullyQualifiedName~Diten.SupplyChainService.Tests.Loads.'
run Returns    RETURNS_MONGO_URI      'FullyQualifiedName~Diten.SupplyChainService.Tests.Returns.'
run Claims     CLAIMS_TEST_MONGO      'FullyQualifiedName~Diten.SupplyChainService.Tests.Claims.'
run SandopPlans MVP6_MOD0190_MONGO_URI 'FullyQualifiedName~Diten.SupplyChainService.Tests.SandopPlans.'
run CapacityPlans MVP6_MOD0192_MONGO_URI 'FullyQualifiedName~Diten.SupplyChainService.Tests.CapacityPlans.'
echo "== end $(TZ=Europe/Istanbul date '+%T')"
