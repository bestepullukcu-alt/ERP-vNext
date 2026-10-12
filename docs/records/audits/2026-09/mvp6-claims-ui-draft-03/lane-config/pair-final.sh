#!/usr/bin/env bash
# Q64e lane helper (from Q64d pair.sh): one K10 pair around one harness phase (or around a lane-DB setup step). No secret passes through here.
# usage: pair.sh <pairLabel> "<expect>" "<totals>" <phase|-> [extra]      (phase '-' = snapshot pair only, action run by caller between two calls is NOT supported)
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export PATH=/Users/natig/mvp6-env/venv/bin:$PATH GIT_OPTIONAL_LOCKS=0
set -a; . /Users/natig/mvp6-env/q64e/lane/lane-final.env; set +a
K=scripts/evidence-kit; W=5801; M=38994; S=$EK_DB_SUFFIX
FX="$EK_EVIDENCE/raw/fixture/org-fixture.json"; ST=$(cat /Users/natig/mvp6-env/q64e/lane/state-dir-final.txt); RT=/Users/natig/mvp6-env/q64e/runtime
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{"claims":{},"claims_receipts":{},"claims_audit":{},"claims_outbox":{}},"totals":["claims","claims_receipts","claims_audit","claims_outbox"]}' "$1" "$S" "$M"; }
name=$1; exp=$2; tot=$3; phase=$4; extra=${5:-}
b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)")
python3 $K/k00_ctl.py "$EK_SOCK" run harness claims_runtime.mjs -- "$phase" $W "$EK_EVIDENCE" "$FX" "$ST" "$RT" $extra; echo "phase $name rc $?"
a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)")
python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$tot"; echo "pair $name rc $?"
