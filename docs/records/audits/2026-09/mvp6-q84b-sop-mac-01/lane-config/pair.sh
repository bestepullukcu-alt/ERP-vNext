#!/usr/bin/env bash
# Q84b lane helper (derived from Q64d lane-config/pair.sh): one K10 pair around one harness phase. No secret passes through here.
# usage: pair.sh <pairLabel> "<expect>" "<totals>" <phase> <locale>
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export GIT_OPTIONAL_LOCKS=0
set -a; . /Users/natig/mvp6-env/q84b/lane/lane.env; set +a
K=scripts/evidence-kit; W=5801; M=38994; S=$EK_DB_SUFFIX
OUT=$(dirname "$EK_EVIDENCE")/runtime; ST=/Users/natig/mvp6-env/q84b/state; RT=/Users/natig/mvp6-env
DEM="DP-Q84B-01 1 sha256:q84b-demand-01"
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{"sandop_plans":{},"sandop_snapshots":{},"sandop_sign_offs":{},"sandop_receipts":{},"sandop_audit":{},"sandop_outbox":{}},"totals":["sandop_plans","sandop_snapshots","sandop_sign_offs","sandop_receipts","sandop_audit","sandop_outbox"]}' "$1" "$S" "$M"; }
name=$1; exp=$2; tot=$3; phase=$4; locale=$5
b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)")
python3 $K/k00_ctl.py "$EK_SOCK" run harness sop_runtime.mjs -- "$phase" $W "$OUT" "$ST" "$RT" "$locale" $DEM; echo "phase $name rc $?"
a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)")
python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$tot"; echo "pair $name rc $?"
