#!/usr/bin/env bash
# Q122 lane helper: one K10 pair (lane Supply Chain DB, whole-DB rule) around one supervisor harness task.
# usage: pairx.sh <pairLabel> "<expect>" "<totals>" <harness file> [args...]
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export PATH=/Users/natig/mvp6-env/venv/bin:$PATH GIT_OPTIONAL_LOCKS=0
L=/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01/lane-config
set -a; . $L/lane.env; set +a
K=scripts/evidence-kit; M=39994; S=$EK_DB_SUFFIX
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{"claims":{},"claims_receipts":{},"claims_audit":{},"claims_outbox":{}},"totals":["claims","claims_receipts","claims_audit","claims_outbox"]}' "$1" "$S" "$M"; }
name=$1; exp=$2; tot=$3; file=$4; shift 4
b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)")
python3 $K/k00_ctl.py "$EK_SOCK" run harness "$file" -- "$@"; echo "task $name rc $?"
a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)")
python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$tot"; echo "pair $name rc $?"
