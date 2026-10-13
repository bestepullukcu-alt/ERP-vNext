#!/usr/bin/env bash
# Q88b lane helper (copied from Q65b/Q84b pair.sh, adapted): one K10 pair around one harness phase. No secret passes through here.
# usage: pair.sh <pairLabel> "<expect>" "<totals>" <phase> <locale>
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export GIT_OPTIONAL_LOCKS=0
set -a; . /Users/natig/mvp6-env/q88b/lane/lane.env; set +a
K=scripts/evidence-kit; W=5501; M=35994; S=$EK_DB_SUFFIX
OUT=$(dirname "$EK_EVIDENCE"); ST=/Users/natig/mvp6-env/q88b/state; RT=/Users/natig/mvp6-env; FR=/Users/natig/mvp6-env/q88b/stage/cap-v3/overlay/_shared-integration/sharedresource-nav-keys
C='"capacity_plans":{},"capacity_scenarios":{},"capacity_evaluations":{},"capacity_active_slots":{},"capacity_receipts":{},"capacity_audit":{},"capacity_outbox":{}'
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{%s},"totals":["capacity_plans","capacity_scenarios","capacity_evaluations","capacity_active_slots","capacity_receipts","capacity_audit","capacity_outbox"]}' "$1" "$S" "$M" "$C"; }
name=$1; exp=$2; tot=$3; phase=$4; locale=$5
b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)")
python3 $K/k00_ctl.py "$EK_SOCK" run harness cap_runtime.mjs -- "$phase" $W "$OUT" "$ST" "$RT" "$locale" "$FR"; echo "phase $name rc $?"
a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)")
python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$tot"; echo "pair $name rc $?"
