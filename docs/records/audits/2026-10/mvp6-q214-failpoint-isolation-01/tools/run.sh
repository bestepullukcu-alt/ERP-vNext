#!/bin/zsh
# usage: run.sh <label> <filter-or-ALL> [uri-suffix-override]
# Runs dotnet test --no-build against the Q214 lane mongod (127.0.0.1:32994, rsq214s2). Edits nothing.
S="${0:A:h:h}"; L="$1"; F="$2"; OPT="${3:-serverSelectionTimeoutMS=5000}"
U="mongodb://127.0.0.1:32994/?replicaSet=rsq214s2&$OPT"
for v in MVP6_MOD0192_MONGO_URI MOD0183_TEST_MONGO MOD0184_TEST_MONGO MOD0185_TEST_MONGO MVP6_MOD0190_MONGO_URI RETURNS_MONGO_URI CLAIMS_TEST_MONGO; do export $v="$U"; done
cd /Users/natig/Projects/ERP-vNext-recovery
P=services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests
A=$(date -u '+%Y-%m-%dT%H:%M:%S')
if [ "$F" = ALL ]; then
  dotnet test $P --no-build --blame-hang --blame-hang-timeout 180s --logger "trx;LogFileName=$L.trx" --results-directory "$S/results" > "$S/logs/$L.log" 2>&1
else
  dotnet test $P --no-build --filter "$F" --logger "trx;LogFileName=$L.trx" --results-directory "$S/results" > "$S/logs/$L.log" 2>&1
fi
RC=$?; B=$(date -u '+%Y-%m-%dT%H:%M:%S')
print "$L\t$F\t$OPT\t$A\t$B\texit=$RC\t$(grep -E '^(Passed|Failed)!' "$S/logs/$L.log" | sed 's/ - Diten.*//')" | tee -a "$S/results/runs.tsv"
