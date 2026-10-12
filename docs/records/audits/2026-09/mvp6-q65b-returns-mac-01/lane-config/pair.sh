#!/usr/bin/env bash
# Q65b lane helper (copied from Q84b lane-config/pair.sh, adapted): one K10 pair around one harness phase. No secret passes through here.
# usage: pair.sh <pairLabel> "<expect>" "<totals>" <phase> <locale>
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export GIT_OPTIONAL_LOCKS=0
set -a; . /Users/natig/mvp6-env/q65b/lane/lane.env; set +a
K=scripts/evidence-kit; W=5701; M=37994; S=$EK_DB_SUFFIX
OUT=$(dirname "$EK_EVIDENCE"); ST=/Users/natig/mvp6-env/q65b/state; RT=/Users/natig/mvp6-env; FX=$(cat /Users/natig/mvp6-env/q65b/lane/fixture-path.txt)
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{"returns":{},"return_entitlements":{},"returns_receipts":{},"returns_audit":{},"returns_outbox":{}},"totals":["returns","return_entitlements","returns_receipts","returns_audit","returns_outbox"]}' "$1" "$S" "$M"; }
name=$1; exp=$2; tot=$3; phase=$4; locale=$5
b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)")
python3 $K/k00_ctl.py "$EK_SOCK" run harness ret_runtime.mjs -- "$phase" $W "$OUT" "$ST" "$RT" "$locale" "$FX"; echo "phase $name rc $?"
a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)")
python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$tot"; echo "pair $name rc $?"
