#!/usr/bin/env bash
# Q64b with-FIXES lane: same phases as the delivered lane, each wrapped in a K10 pair. No secret passes through here.
set -u
cd /Users/natig/Projects/ERP-vNext-recovery
export PATH=/Users/natig/mvp6-env/venv/bin:$PATH GIT_OPTIONAL_LOCKS=0
set -a; . /Users/natig/mvp6-env/lane-fixes/lane.env; set +a
K=scripts/evidence-kit; W=5601; G=5600; M=36994; S=$EK_DB_SUFFIX
FX="$EK_EVIDENCE/raw/fixture/org-fixture.json"; ST=/Users/natig/mvp6-env/tmp/state-fixes; RT=/Users/natig/mvp6-env/runtime
mkdir -p -m 700 $ST "$EK_EVIDENCE/raw/fixture"
spec() { printf '{"label":"%s","db":"DitenSupplyChain_%s","lanePort":%s,"collections":{"claims":{},"claims_receipts":{},"claims_audit":{},"claims_outbox":{}},"totals":["claims","claims_receipts","claims_audit","claims_outbox"]}' "$1" "$S" "$M"; }
H() { python3 $K/k00_ctl.py "$EK_SOCK" run harness claims_runtime.mjs -- "$1" $W "$EK_EVIDENCE" "$FX" $ST $RT "${@:2}"; }
pair() { local name=$1 exp=$2; shift 2; local b a; b=$(bash $K/k10_snap.sh $name-before "$(spec $name-before)"); "$@"; echo "phase $name rc $?"; a=$(bash $K/k10_snap.sh $name-after "$(spec $name-after)"); python3 $K/k10_db_diff.py $b $a --expect "$exp" --totals "$exp"; echo "pair $name rc $?"; }
b=$(bash $K/k10_snap.sh fixture-before "$(spec fixture-before)"); python3 $K/k00_ctl.py "$EK_SOCK" run harness org_fixture.py -- $G $M $S "$FX"; echo "fixture rc $?"; a=$(bash $K/k10_snap.sh fixture-after "$(spec fixture-after)")
python3 $K/k10_db_diff.py $b $a --expect "claims=+4 claims_receipts=+5 claims_audit=+5 claims_outbox=+5" --totals "carrier_audit=+1 carrier_idempotency=+1 carriers=+1 claims=+4 claims_audit=+5 claims_outbox=+5 claims_receipts=+5 sce_shipment_audit=+16 sce_shipment_history=+16 sce_shipment_outbox=+16 sce_shipment_receipts=+16 sce_shipments=+6"; echo "pair fixture rc $?"
H login; echo "login rc $?"
pair vs1 "claims=+1 claims_receipts=+1 claims_audit=+1 claims_outbox=+1" H vs1
pair spec "claims=+1 claims_receipts=+1 claims_audit=+1 claims_outbox=+1" H spec
pair neg "claims=0 claims_receipts=0 claims_audit=0 claims_outbox=0" H checks-neg
pair flows "claims=+1 claims_receipts=+3 claims_audit=+3 claims_outbox=+3" H ui-flows fixed-native
mongosh --quiet "mongodb://127.0.0.1:$M/?directConnection=true" --eval "
if (db.adminCommand({getCmdLineOpts:1}).parsed.net.port!==$M) quit(2);
const d=db.getSiblingDB('DitenAuth_$S'); const T=UUID('97c59330-dbc4-4665-b29c-0c26dbb5cc93');
const r=d.roles.findOne({TenantId:T,Name:'Q64bClaimsFull'}); const p=d.permissions.findOne({Key:'supplychain.carriers.read',IsDeleted:false}); const tpl=d.rolePermissions.findOne({RoleId:r._id});
const before=d.rolePermissions.countDocuments({RoleId:r._id,IsDeleted:false});
d.rolePermissions.insertOne(Object.assign({},tpl,{_id:UUID(),PermissionId:p._id,AssignedAt:new Date(),AssignedBy:'q64b-lane-fixture-cu10',CreatedBy:'q64b-lane-fixture-cu10'}));
print(JSON.stringify({role:'Q64bClaimsFull@T1',grantAdded:'supplychain.carriers.read',grantsBefore:before,grantsAfter:d.rolePermissions.countDocuments({RoleId:r._id,IsDeleted:false})}))" > "$EK_EVIDENCE/raw/fixture/cu10-carriers-read-grant.json"
H login; echo "relogin rc $?"
pair cu10 "claims=+1 claims_receipts=+1 claims_audit=+1 claims_outbox=+1" H checks-mut
