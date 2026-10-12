#!/bin/zsh
# Q266: run Diten.SupplyChainService.Tests one module at a time, only that module's variable set.
E=${0:A:h}; T=$E/src/services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests
URI='mongodb://127.0.0.1:57061/?replicaSet=rsq266'
unset MOD0183_TEST_MONGO MOD0184_TEST_MONGO MOD0185_TEST_MONGO MVP6_MOD0190_MONGO_URI MVP6_MOD0192_MONGO_URI RETURNS_MONGO_URI CLAIMS_TEST_MONGO MOD192_CHILD_MODE
mkdir -p $E/results; cd $T
run() { # name var filter
  local s=$(date +%s); echo "== $1 start $(date '+%T')"
  env $2="$URI" perl -e 'alarm 600; exec @ARGV' dotnet test --no-build --filter "$3" --logger "trx;LogFileName=$1.trx" --results-directory $E/results -v q > $E/results/$1.log 2>&1
  echo "== $1 rc=$? seconds=$(( $(date +%s)-s ))"; grep -E '^(Passed!|Failed!)' $E/results/$1.log
}
run Shipments  MOD0183_TEST_MONGO     'FullyQualifiedName~Diten.SupplyChainService.Tests.Shipment|FullyQualifiedName~Diten.SupplyChainService.Tests.Source'
run Carriers   MOD0184_TEST_MONGO     'FullyQualifiedName~Diten.SupplyChainService.Tests.Carriers.'
run Loads      MOD0185_TEST_MONGO     'FullyQualifiedName~Diten.SupplyChainService.Tests.Loads.'
run Returns    RETURNS_MONGO_URI      'FullyQualifiedName~Diten.SupplyChainService.Tests.Returns.'
run Claims     CLAIMS_TEST_MONGO      'FullyQualifiedName~Diten.SupplyChainService.Tests.Claims.'
run SandopPlans MVP6_MOD0190_MONGO_URI 'FullyQualifiedName~Diten.SupplyChainService.Tests.SandopPlans.'
run CapacityPlans MVP6_MOD0192_MONGO_URI 'FullyQualifiedName~Diten.SupplyChainService.Tests.CapacityPlans.'
