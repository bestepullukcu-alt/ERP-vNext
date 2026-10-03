#!/usr/bin/env bash
# Q122 lane helper (from Q64e pair.sh): one K10 pair around one harness phase. No secret passes through here.
# usage: pair.sh <pairLabel> "<expect>" "<totals>" <phase> [extra...]
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export PATH=/Users/natig/mvp6-env/venv/bin:$PATH GIT_OPTIONAL_LOCKS=0
L=/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01/lane-config
set -a; . $L/lane.env; set +a
K=scripts/evidence-kit; W=5901; M=39994; S=$EK_DB_SUFFIX
FX="$EK_EVIDENCE/raw/fixture/org-fixture.json"; ST=$(cat /Users/natig/mvp6-env/q122/state-dir.txt); RT=/Users/natig/mvp6-env/q122/runtime
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{"claims":{},"claims_receipts":{},"claims_audit":{},"claims_outbox":{}},"totals":["claims","claims_receipts","claims_audit","claims_outbox"]}' "$1" "$S" "$M"; }
name=$1; exp=$2; tot=$3; phase=$4; shift 4
b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)")
python3 $K/k00_ctl.py "$EK_SOCK" run harness claims_runtime.mjs -- "$phase" $W "$EK_EVIDENCE" "$FX" "$ST" "$RT" "$@"; echo "phase $name rc $?"
a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)")
python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$tot"; echo "pair $name rc $?"
